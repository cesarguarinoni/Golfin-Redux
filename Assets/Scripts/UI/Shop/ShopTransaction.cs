// Assets/Scripts/UI/Shop/ShopTransaction.cs
// Order 517 — stamina_boost_shop
// Reusable purchase seam.  Order 610 (general Shop UI) will reference this same class.

using System;
using UnityEngine;
using Golfin.Roster;
using Golfin.Save;
using Golfin.Inventory;

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Stateless purchase seam that orchestrates RP spend → stamina grant → persist.
    /// Designed to be called from StaminaShopDetailScreenController.
    ///
    /// Returns a <see cref="PurchaseResult"/> describing success or the failure reason,
    /// so callers can show the appropriate Toast without knowing the internals.
    ///
    /// Order 610 note: when the general Shop needs to sell non-stamina items,
    /// add an overload or extend PurchaseResult.GrantType.
    /// </summary>
    public static class ShopTransaction
    {
        public enum PurchaseResult
        {
            Success,
            InsufficientRp,
            StaminaFull,
            NullCharacter
        }

        /// <summary>
        /// Attempts to buy a stamina boost item for the given character.
        /// <list type="bullet">
        ///   <item>Checks stamina-full guard (blocks if currentEnergy >= maxEnergy).</item>
        ///   <item>Calls <see cref="RewardPointsManager.SpendPoints"/> — fails fast on insufficient RP.</item>
        ///   <item>Calls <see cref="StaminaRuntimeService.AddEnergy"/> — clamps to max, persists.</item>
        ///   <item>Invokes <paramref name="onGranted"/> on success.</item>
        /// </list>
        /// </summary>
        /// <param name="pcd">Character whose Condition pool receives the boost.</param>
        /// <param name="rpCost">RP cost from CSV (item.RpCost).</param>
        /// <param name="staminaAmount">STA units to add (item.Stamina).</param>
        /// <param name="onGranted">Optional callback invoked AFTER the grant (UI refresh hook).</param>
        public static PurchaseResult TryPurchase(
            PlayerCharacterData pcd,
            int rpCost,
            float staminaAmount,
            Action onGranted = null)
        {
            if (pcd == null)
            {
                Debug.LogWarning("[ShopTransaction] TryPurchase: pcd is null.");
                return PurchaseResult.NullCharacter;
            }

            // Stamina-full guard (D5)
            if (pcd.currentStaminaEnergy >= pcd.maxStaminaEnergy)
            {
                Debug.Log("[ShopTransaction] TryPurchase: stamina already full.");
                return PurchaseResult.StaminaFull;
            }

            // RP spend (SpendPoints does NOT touch lifetime-earned)
            var rpm = RewardPointsManager.Instance;
            if (rpm == null || !rpm.SpendPoints(rpCost))
            {
                Debug.Log(string.Format("[ShopTransaction] TryPurchase: insufficient RP (need {0}).", rpCost));
                return PurchaseResult.InsufficientRp;
            }

            // Grant stamina
            StaminaRuntimeService.AddEnergy(pcd, staminaAmount);

            onGranted?.Invoke();
            return PurchaseResult.Success;
        }

        // ── Order 610 — general Shop (clubs + balls) ────────────────────────────

        /// <summary>Result of a general (club/ball) catalog purchase.</summary>
        public enum GeneralPurchaseResult
        {
            Success,
            InsufficientRp,
            AlreadyOwned, // clubs are unique (B6)
            Invalid       // null entry / unknown ref / grant failure
        }

        /// <summary>
        /// Buys a general-shop catalog entry (Order 610, B5). RP-spend → grant to inventory (D5, no
        /// auto-equip): a <b>ball</b> increments SaveData.ballQuantities (respecting the -1 unlimited
        /// convention); a <b>club</b> calls ClubManager.GrantClub (Phase A). The owned/RP pre-checks run
        /// BEFORE SpendPoints so a denied grant never charges the player. The IAP swap (D2) would replace
        /// only the SpendPoints step — the grant dispatch stays identical.
        /// </summary>
        public static GeneralPurchaseResult TryPurchaseCatalogEntry(ShopCatalogEntry entry, Action onGranted = null)
        {
            if (entry == null || string.IsNullOrEmpty(entry.RefId))
            {
                Debug.LogWarning("[ShopTransaction] TryPurchaseCatalogEntry: null/empty entry.");
                return GeneralPurchaseResult.Invalid;
            }

            // ── Pre-checks (BEFORE any spend) so the grant is guaranteed and no refund is ever needed.
            if (entry.Category == ShopCategory.Club)
            {
                if (ClubManager.Instance == null || ClubDatabaseCSV.Instance?.GetClub(entry.RefId) == null)
                {
                    Debug.LogWarning($"[ShopTransaction] Unknown club '{entry.RefId}' or no ClubManager.");
                    return GeneralPurchaseResult.Invalid;
                }
                if (ClubManager.Instance.IsOwned(entry.RefId))   // clubs are unique (B6)
                    return GeneralPurchaseResult.AlreadyOwned;
            }
            else // Ball
            {
                if (SaveDataHost.Instance == null || BallDatabaseCSV.Instance?.GetBall(entry.RefId) == null)
                {
                    Debug.LogWarning($"[ShopTransaction] Unknown ball '{entry.RefId}' or no SaveDataHost.");
                    return GeneralPurchaseResult.Invalid;
                }
            }

            int cost = entry.EffectiveRpCost;
            var rpm = RewardPointsManager.Instance;
            if (rpm == null || rpm.GetPoints() < cost)
            {
                Debug.Log($"[ShopTransaction] TryPurchaseCatalogEntry: insufficient RP (need {cost}).");
                return GeneralPurchaseResult.InsufficientRp;
            }

            if (!rpm.SpendPoints(cost))
                return GeneralPurchaseResult.InsufficientRp;

            // Grant dispatch — guaranteed to succeed after the pre-checks (D5: grant to inventory, no equip).
            if (entry.Category == ShopCategory.Club)
                ClubManager.Instance.GrantClub(entry.RefId);
            else
                GrantBall(entry.RefId);

            onGranted?.Invoke();
            return GeneralPurchaseResult.Success;
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
