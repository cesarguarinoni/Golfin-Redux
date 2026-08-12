// Assets/Scripts/UI/Shop/ShopTransaction.cs
// Order 517 — stamina_boost_shop
// Reusable purchase seam.  Order 610 (general Shop UI) will reference this same class.

using System;
using UnityEngine;
using Golfin.Economy;
using Golfin.EconomyRuntime;
using Golfin.Roster;
using Golfin.Save;
using Golfin.Inventory;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Stateless purchase seam that orchestrates RP spend → grant → persist.
    /// Designed to be called from StaminaShopDetailScreenController / GeneralShopScreenController.
    ///
    /// ASYNC SINCE points_cutover_followups (item 2). Both entry points used to debit RP locally
    /// and return a verdict on the caller's own stack. That made the shop the ONE spend flow the
    /// Slice-2 cutover missed: with <c>PointsBackendEnabled</c> ON the purchase debited the local
    /// balance only, and the next server refresh overwrote it — the player got the item and their
    /// points back. A self-refunding shop.
    ///
    /// The fix is the same shape the other four flows already use: everything runs through
    /// <see cref="PointsSpendGate"/>, which debits SERVER-SIDE FIRST and only then runs the grant.
    /// Because that is a round-trip, the verdict now arrives by callback. With the flag OFF the gate
    /// short-circuits synchronously, so <paramref name="onResult"/> still fires on the caller's own
    /// stack frame and nothing about the offline behaviour changes.
    ///
    /// The gate owns the refusal TOAST (so the "Connection required" / "Not enough Reward Points"
    /// copy stays identical everywhere). Callers must therefore stay silent on
    /// <see cref="PurchaseResult.SpendDenied"/> instead of adding a second toast.
    /// </summary>
    public static class ShopTransaction
    {
        public enum PurchaseResult
        {
            Success,
            InsufficientRp,
            StaminaFull,
            NullCharacter,

            /// <summary>The server refused or could not be reached. The gate has already toasted —
            /// the caller should clear its busy state and say nothing.</summary>
            SpendDenied
        }

        /// <summary>
        /// Attempts to buy a stamina boost item for the given character.
        /// <list type="bullet">
        ///   <item>Checks stamina-full guard (blocks if currentEnergy >= maxEnergy).</item>
        ///   <item>Pre-checks affordability, then debits SERVER-SIDE via <see cref="PointsSpendGate"/>.</item>
        ///   <item>Only once the debit lands: local <see cref="RewardPointsManager.SpendPoints"/>
        ///         + <see cref="StaminaRuntimeService.AddEnergy"/> (clamps to max, persists).</item>
        ///   <item>Invokes <paramref name="onGranted"/> on success, then <paramref name="onResult"/>.</item>
        /// </list>
        /// The pre-checks are synchronous, so an unaffordable or already-full purchase answers
        /// immediately and never reaches the server.
        /// </summary>
        /// <param name="pcd">Character whose Condition pool receives the boost.</param>
        /// <param name="rpCost">RP cost from CSV (item.RpCost).</param>
        /// <param name="staminaAmount">STA units to add (item.Stamina).</param>
        /// <param name="onGranted">Optional callback invoked AFTER the grant (UI refresh hook).</param>
        /// <param name="onResult">Verdict callback. ALWAYS invoked exactly once.</param>
        public static void TryPurchase(
            PlayerCharacterData pcd,
            int rpCost,
            float staminaAmount,
            Action onGranted = null,
            Action<PurchaseResult> onResult = null)
        {
            if (pcd == null)
            {
                Debug.LogWarning("[ShopTransaction] TryPurchase: pcd is null.");
                onResult?.Invoke(PurchaseResult.NullCharacter);
                return;
            }

            // Stamina-full guard (D5)
            if (pcd.currentStaminaEnergy >= pcd.maxStaminaEnergy)
            {
                Debug.Log("[ShopTransaction] TryPurchase: stamina already full.");
                onResult?.Invoke(PurchaseResult.StaminaFull);
                return;
            }

            // Affordability pre-check BEFORE the server round-trip: a player who plainly cannot pay
            // gets the specific "need N RP" copy instead of the gate's generic refusal toast.
            var rpm = RewardPointsManager.Instance;
            if (rpm == null || rpm.GetPoints() < rpCost)
            {
                Debug.Log(string.Format("[ShopTransaction] TryPurchase: insufficient RP (need {0}).", rpCost));
                onResult?.Invoke(PurchaseResult.InsufficientRp);
                return;
            }

            PointsSpendGate.Spend(rpCost, SpendReasons.StaminaBoost,
                onApproved: () =>
                {
                    // Local debit mirrors the server's (SpendPoints does NOT touch lifetime-earned).
                    rpm.SpendPoints(rpCost);
                    StaminaRuntimeService.AddEnergy(pcd, staminaAmount);
                    onGranted?.Invoke();
                    onResult?.Invoke(PurchaseResult.Success);
                },
                onDenied: _ => onResult?.Invoke(PurchaseResult.SpendDenied));
        }

        // ── Order 610 — general Shop (clubs + balls) ────────────────────────────

        /// <summary>Result of a general (club/ball) catalog purchase.</summary>
        public enum GeneralPurchaseResult
        {
            Success,
            InsufficientRp,
            AlreadyOwned, // clubs are unique (B6)
            Invalid,      // null entry / unknown ref / grant failure

            /// <summary>Server refused or unreachable — the gate already toasted. Caller stays silent.</summary>
            SpendDenied
        }

        /// <summary>
        /// Buys a general-shop catalog entry (Order 610, B5). Server RP-debit → grant to inventory
        /// (D5, no auto-equip): a <b>ball</b> increments SaveData.ballQuantities (respecting the -1
        /// unlimited convention); a <b>club</b> calls ClubManager.GrantClub (Phase A). The owned/RP
        /// pre-checks run BEFORE the debit so a denied grant never charges the player, and the debit
        /// runs before the grant so a refused debit never hands one out. The IAP swap (D2) would
        /// replace only the spend step — the grant dispatch stays identical.
        /// </summary>
        /// <param name="onResult">Verdict callback. ALWAYS invoked exactly once.</param>
        public static void TryPurchaseCatalogEntry(
            ShopCatalogEntry entry,
            Action onGranted = null,
            Action<GeneralPurchaseResult> onResult = null)
        {
            if (entry == null || string.IsNullOrEmpty(entry.RefId))
            {
                Debug.LogWarning("[ShopTransaction] TryPurchaseCatalogEntry: null/empty entry.");
                onResult?.Invoke(GeneralPurchaseResult.Invalid);
                return;
            }

            // ── Pre-checks (BEFORE any spend) so the grant is guaranteed and no refund is ever needed.
            if (entry.Category == ShopCategory.Club)
            {
                if (ClubManager.Instance == null || ClubDatabaseCSV.Instance?.GetClub(entry.RefId) == null)
                {
                    Debug.LogWarning($"[ShopTransaction] Unknown club '{entry.RefId}' or no ClubManager.");
                    onResult?.Invoke(GeneralPurchaseResult.Invalid);
                    return;
                }
                if (ClubManager.Instance.IsOwned(entry.RefId))   // clubs are unique (B6)
                {
                    onResult?.Invoke(GeneralPurchaseResult.AlreadyOwned);
                    return;
                }
            }
            else // Ball
            {
                if (SaveDataHost.Instance == null || BallDatabaseCSV.Instance?.GetBall(entry.RefId) == null)
                {
                    Debug.LogWarning($"[ShopTransaction] Unknown ball '{entry.RefId}' or no SaveDataHost.");
                    onResult?.Invoke(GeneralPurchaseResult.Invalid);
                    return;
                }
            }

            int cost = entry.EffectiveRpCost;
            var rpm = RewardPointsManager.Instance;
            if (rpm == null || rpm.GetPoints() < cost)
            {
                Debug.Log($"[ShopTransaction] TryPurchaseCatalogEntry: insufficient RP (need {cost}).");
                onResult?.Invoke(GeneralPurchaseResult.InsufficientRp);
                return;
            }

            PointsSpendGate.Spend(cost, SpendReasons.ShopPurchase,
                onApproved: () =>
                {
                    rpm.SpendPoints(cost);

                    // Grant dispatch — guaranteed to succeed after the pre-checks (D5: grant to
                    // inventory, no equip).
                    if (entry.Category == ShopCategory.Club)
                        ClubManager.Instance.GrantClub(entry.RefId);
                    else
                        GrantBall(entry.RefId);

                    onGranted?.Invoke();
                    onResult?.Invoke(GeneralPurchaseResult.Success);
                },
                onDenied: _ => onResult?.Invoke(GeneralPurchaseResult.SpendDenied));
        }

        /// <summary>Increments a ball's persisted quantity (respects -1 = unlimited; caps at 99).</summary>
        private static void GrantBall(string ballId)
        {
            var host = SaveDataHost.Instance;
            if (host == null) { Debug.LogWarning("[ShopTransaction] GrantBall: SaveDataHost null."); return; }

            var q = host.Data.ballQuantities;
            if (q.TryGetValue(ballId, out var cur))
            {
                if (cur >= 0) q[ballId] = Mathf.Min(cur + 1, 99); // -1 unlimited stays unlimited
            }
            else
            {
                q[ballId] = 1;
            }
            host.MarkDirty();
        }
    }
}
