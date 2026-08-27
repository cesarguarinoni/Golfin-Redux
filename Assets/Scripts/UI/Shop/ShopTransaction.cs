// Assets/Scripts/UI/Shop/ShopTransaction.cs
// Order 517 — stamina_boost_shop
// Reusable purchase seam.  Order 610 (general Shop UI) will reference this same class.

using System;
using System.Collections.Generic;
using UnityEngine;
using Golfin.Content;
using Golfin.Economy;
using Golfin.EconomyRuntime;
using Golfin.InventorySync;
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
    ///
    /// SERVER-PRICED SINCE shop_server_purchase (§3.2), FOR THE CATALOG SHOP ONLY.
    /// <see cref="TryPurchaseCatalogEntry"/> no longer goes through <see cref="PointsSpendGate"/> when
    /// the flag is ON: the gate's job is to send the CLIENT's number, and that number is exactly what
    /// stopped being trusted. It calls <c>POST /shop/purchase</c> with the entry id instead, debits
    /// its local mirror by whatever the server says it charged, and applies the grant the server
    /// queued. The gate's two toast CONSTANTS are still reused so the refusal copy does not fork.
    ///
    /// <see cref="TryPurchase"/> — the stamina boost — is deliberately UNTOUCHED. Stamina items are
    /// not a content catalog (they live in <c>stamina_shop_items.csv</c>, which has no server-side
    /// listing to price against), so there is nothing for a server to be authoritative about yet.
    /// That is the next spec, not a gap in this one.
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

        /// <summary>Result of a general catalog purchase (club / ball / character / item).</summary>
        public enum GeneralPurchaseResult
        {
            Success,
            InsufficientRp,
            AlreadyOwned, // clubs and characters are unique (B6)
            Invalid,      // null entry / unknown ref / grant failure

            /// <summary>Server refused or unreachable — the gate already toasted. Caller stays silent.</summary>
            SpendDenied,

            /// <summary>The server's published price is not the one the card showed. NOTHING was
            /// written. <see cref="LastServerPrice"/> carries the real number; the caller re-renders
            /// and lets the player decide again. Only reachable with the flag ON.</summary>
            PriceChanged,

            /// <summary>The server will not sell this row right now — the window closed, it was
            /// deactivated, content is killed, or the thing it points at went inactive. Nothing was
            /// written. Only reachable with the flag ON.</summary>
            NotListed
        }

        /// <summary>
        /// The price the SERVER published, set alongside a
        /// <see cref="GeneralPurchaseResult.PriceChanged"/> verdict.
        ///
        /// <para>
        /// A static rather than an out-param because the verdict arrives by CALLBACK — an out-param
        /// cannot cross that boundary, and widening the callback signature would touch every existing
        /// call site for a value only one of them reads. It is written immediately before
        /// <c>onResult</c> is invoked and read synchronously inside it; the in-flight latches (this
        /// class's callers and <see cref="ShopPurchaseService"/>'s own) mean there is never a second
        /// purchase in the air to race it.
        /// </para>
        /// </summary>
        public static int LastServerPrice { get; private set; }

        /// <summary>
        /// Buys a general-shop catalog entry. TWO PATHS, chosen by <see cref="PointsBackendFlag"/>.
        ///
        /// <para>
        /// <b>Flag ON — the server owns the price (shop_server_purchase §3.2).</b> ONE call to
        /// <c>POST /shop/purchase</c> carrying the ENTRY ID, not a price. The server reads the
        /// published listing, prices it off its own clock, debits through <c>spend_pts</c> and queues
        /// the item as a grant in one transaction; the client then debits its LOCAL mirror by the
        /// SERVER's <c>charged</c> (never <c>entry.EffectiveRpCost</c>) and applies the grant through
        /// the managers. <see cref="PointsSpendGate"/> is deliberately NOT used on this branch — going
        /// through it would debit twice, and its whole job (send the client's number) is the thing
        /// being removed. Its two toast strings ARE reused, so the refusal copy stays identical
        /// everywhere.
        /// </para>
        /// <para>
        /// <b>Flag OFF — unchanged.</b> The Editor / harness / no-<c>GOLFIN_POINTS_BACKEND</c> path is
        /// byte-for-byte what it was: <see cref="PointsSpendGate"/> short-circuits synchronously and
        /// the grant happens locally at the bundled price. That is the offline and dev path and it
        /// must keep working with no server at all.
        /// </para>
        /// <para>
        /// The owned / unknown-ref / affordability pre-checks run BEFORE either branch on both. They
        /// answer instantly, never reach the server, and give the player the specific copy ("need N
        /// RP") instead of a generic refusal.
        /// </para>
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
            if (!PreCheck(entry, out GeneralPurchaseResult preVerdict))
            {
                onResult?.Invoke(preVerdict);
                return;
            }

            int cost = entry.EffectiveRpCost;
            var rpm = RewardPointsManager.Instance;
            if (rpm == null || rpm.GetPoints() < cost)
            {
                Debug.Log($"[ShopTransaction] TryPurchaseCatalogEntry: insufficient RP (need {cost}).");
                onResult?.Invoke(GeneralPurchaseResult.InsufficientRp);
                return;
            }

            if (PointsBackendFlag.Enabled)
                PurchaseServerSide(entry, cost, rpm, onGranted, onResult);
            else
                PurchaseLocally(entry, cost, rpm, onGranted, onResult);
        }

        /// <summary>
        /// Category pre-checks. Synchronous, and identical on both branches: an unknown ref or an
        /// already-owned unique is answered instantly and never costs a round trip.
        /// </summary>
        private static bool PreCheck(ShopCatalogEntry entry, out GeneralPurchaseResult verdict)
        {
            verdict = GeneralPurchaseResult.Success;

            switch (entry.Category)
            {
                case ShopCategory.Club:
                    if (ClubManager.Instance == null || ClubDatabaseCSV.Instance?.GetClub(entry.RefId) == null)
                    {
                        Debug.LogWarning($"[ShopTransaction] Unknown club '{entry.RefId}' or no ClubManager.");
                        verdict = GeneralPurchaseResult.Invalid;
                        return false;
                    }
                    if (ClubManager.Instance.IsOwned(entry.RefId))   // clubs are unique (B6)
                    {
                        verdict = GeneralPurchaseResult.AlreadyOwned;
                        return false;
                    }
                    return true;

                case ShopCategory.Character:
                    if (CharacterManager.Instance == null ||
                        CharacterDatabaseCSV.Instance?.GetCharacter(entry.RefId) == null)
                    {
                        Debug.LogWarning($"[ShopTransaction] Unknown character '{entry.RefId}' or no CharacterManager.");
                        verdict = GeneralPurchaseResult.Invalid;
                        return false;
                    }
                    if (CharacterManager.Instance.IsOwned(entry.RefId))   // characters are unique too
                    {
                        verdict = GeneralPurchaseResult.AlreadyOwned;
                        return false;
                    }
                    return true;

                case ShopCategory.Item:
                    // Items STACK — buying a second repair kit is the normal case, not a mistake,
                    // so there is no owned check. UNLIMITED is the one exception: see HoldsUnlimited.
                    if (ItemManager.Instance == null || ItemDatabaseCSV.Instance?.GetItem(entry.RefId) == null)
                    {
                        Debug.LogWarning($"[ShopTransaction] Unknown item '{entry.RefId}' or no ItemManager.");
                        verdict = GeneralPurchaseResult.Invalid;
                        return false;
                    }
                    if (HoldsUnlimited(entry))
                    {
                        verdict = GeneralPurchaseResult.AlreadyOwned;
                        return false;
                    }
                    return true;

                default: // Ball — stacks as well (and -1 means unlimited).
                    if (SaveDataHost.Instance == null || BallDatabaseCSV.Instance?.GetBall(entry.RefId) == null)
                    {
                        Debug.LogWarning($"[ShopTransaction] Unknown ball '{entry.RefId}' or no SaveDataHost.");
                        verdict = GeneralPurchaseResult.Invalid;
                        return false;
                    }
                    if (HoldsUnlimited(entry))
                    {
                        verdict = GeneralPurchaseResult.AlreadyOwned;
                        return false;
                    }
                    return true;
            }
        }

        /// <summary>
        /// True when the player already holds an UNLIMITED (-1) supply of this stackable.
        ///
        /// <para>
        /// `-1` is a sentinel, not a quantity — the default Golfin ball ships that way. Every add
        /// path deliberately leaves it alone (<c>AddQuantity</c>, <c>AddBalls</c>, <c>AddItems</c>,
        /// <c>GrantBall</c>), which is correct for a reward and CATASTROPHIC for a sale: the debit
        /// happens, the grant is a no-op, and <c>InventoryGrants.Apply</c> has already written
        /// <c>appliedGrantIds</c> and acked, so the player pays and receives nothing with the grant
        /// marked delivered.
        /// </para>
        /// <para>
        /// Refusing here is the CLIENT lock and it also covers the flag-OFF local path, where
        /// <c>GrantBall</c> would no-op with no server involved at all. The server refuses the same
        /// case independently (2026_08_29_shop_purchase_unlimited_refusal.sql) — two locks,
        /// neither relying on the other.
        /// </para>
        /// </summary>
        private static bool HoldsUnlimited(ShopCatalogEntry entry)
        {
            if (entry.Category == ShopCategory.Item)
                return ItemManager.Instance != null &&
                       ItemManager.Instance.GetItemData(entry.RefId)?.IsUnlimited == true;

            if (entry.Category == ShopCategory.Ball)
                return BallManager.Instance != null &&
                       BallManager.Instance.GetBallData(entry.RefId)?.IsUnlimited == true;

            return false;
        }

        /// <summary>
        /// Flag-ON path. The client sends WHICH listing and the server answers with what it charged
        /// and what it queued.
        /// </summary>
        private static void PurchaseServerSide(
            ShopCatalogEntry entry, int shownCost, RewardPointsManager rpm,
            Action onGranted, Action<GeneralPurchaseResult> onResult)
        {
            ShopPurchaseService.Instance.PurchaseAsync(
                entry.EntryId, shownCost, ContentBuildNumber.Current,
                outcome =>
                {
                    if (outcome == null)
                    {
                        Toast(PointsSpendGate.OfflineMessage);
                        onResult?.Invoke(GeneralPurchaseResult.SpendDenied);
                        return;
                    }

                    switch (outcome.Verdict)
                    {
                        case ShopPurchaseVerdict.Ok:
                            // The SERVER's number, never `shownCost`. If they disagree the server would
                            // have answered price_changed, so reaching here means they agree — but
                            // debiting the server's value is what makes that structural rather than
                            // merely true today.
                            rpm.SpendPoints(outcome.Charged);

                            if (!ApplyPurchaseGrant(outcome.Grant))
                            {
                                // The RP is gone and the grant is NOT applied — but it is still QUEUED
                                // server-side and unacked, so the next boot's drain delivers it. This is
                                // the exact failure the grants queue exists to make survivable; it is
                                // loud rather than silent because it should never happen after the
                                // pre-checks.
                                Debug.LogError($"[ShopTransaction] Purchased '{entry.EntryId}' but could " +
                                               $"not apply grant {outcome.Grant} locally. It stays " +
                                               "pending server-side and the next boot will drain it.");
                                onResult?.Invoke(GeneralPurchaseResult.Invalid);
                                return;
                            }

                            onGranted?.Invoke();
                            onResult?.Invoke(GeneralPurchaseResult.Success);
                            return;

                        case ShopPurchaseVerdict.Insufficient:
                            Toast(PointsSpendGate.InsufficientMessage);
                            onResult?.Invoke(GeneralPurchaseResult.InsufficientRp);
                            return;

                        case ShopPurchaseVerdict.PriceChanged:
                            LastServerPrice = outcome.Server != null ? outcome.Server.Price : 0;
                            Debug.Log($"[ShopTransaction] '{entry.EntryId}' price moved: card showed " +
                                      $"{shownCost}, server publishes {LastServerPrice}. Nothing charged.");
                            onResult?.Invoke(GeneralPurchaseResult.PriceChanged);
                            return;

                        case ShopPurchaseVerdict.AlreadyOwned:
                            onResult?.Invoke(GeneralPurchaseResult.AlreadyOwned);
                            return;

                        case ShopPurchaseVerdict.NotListed:
                            onResult?.Invoke(GeneralPurchaseResult.NotListed);
                            return;

                        case ShopPurchaseVerdict.Disabled:
                            // Unreachable: the flag was checked before this call. If it ever fires, the
                            // flag changed mid-flight and granting anything would be a guess.
                            Debug.LogError("[ShopTransaction] ShopPurchaseService answered Disabled on the " +
                                           "flag-ON branch — the flag moved mid-purchase. Nothing granted.");
                            onResult?.Invoke(GeneralPurchaseResult.Invalid);
                            return;

                        default: // Unavailable / Unknown
                            Toast(PointsSpendGate.OfflineMessage);
                            Debug.LogWarning($"[ShopTransaction] '{entry.EntryId}' not purchased: {outcome}.");
                            onResult?.Invoke(GeneralPurchaseResult.SpendDenied);
                            return;
                    }
                });
        }

        /// <summary>
        /// Flag-OFF path — the pre-shop_server_purchase body, unchanged. Offline / Editor / harness.
        /// </summary>
        private static void PurchaseLocally(
            ShopCatalogEntry entry, int cost, RewardPointsManager rpm,
            Action onGranted, Action<GeneralPurchaseResult> onResult)
        {
            PointsSpendGate.Spend(cost, SpendReasons.ShopPurchase,
                onApproved: () =>
                {
                    rpm.SpendPoints(cost);

                    // Grant dispatch — guaranteed to succeed after the pre-checks (D5: grant to
                    // inventory, no equip).
                    switch (entry.Category)
                    {
                        case ShopCategory.Club:      ClubManager.Instance.GrantClub(entry.RefId); break;
                        case ShopCategory.Character: CharacterManager.Instance.UnlockCharacter(entry.RefId); break;
                        case ShopCategory.Item:      ItemManager.Instance.AddItems(entry.RefId, 1); break;
                        default:                     GrantBall(entry.RefId); break;
                    }

                    onGranted?.Invoke();
                    onResult?.Invoke(GeneralPurchaseResult.Success);
                },
                onDenied: _ => onResult?.Invoke(GeneralPurchaseResult.SpendDenied));
        }

        /// <summary>
        /// Apply a purchased grant through the MANAGERS, then record its id, mark the save and the
        /// sync dirty, and ack it.
        ///
        /// <para>
        /// ⚠️ NOT <c>InventoryGrants.Apply</c>, and the difference matters. That static writes raw
        /// <c>SaveData</c>, which is right at BOOT (before the managers have loaded) and wrong
        /// MID-SESSION: <c>ClubManager</c> / <c>CharacterManager</c> / <c>ItemManager</c> hold their
        /// own runtime copies built at Awake, so a save-level write would be invisible to the screen
        /// the player is standing on and would be overwritten the next time a manager synced.
        /// </para>
        /// <para>
        /// ORDERING IS THE GRANTS-QUEUE ORDERING, for the same reason: apply → record the id → ack.
        /// Die before the record and the boot drain applies it (the id is not in the save, and for a
        /// club or character the manager's own unique-check makes even that a no-op). Die after the
        /// record but before the ack and the boot drain sees a duplicate and simply re-acks. Nothing
        /// is ever applied twice. Ack-then-apply, the other order, loses the item outright.
        /// </para>
        /// </summary>
        /// <returns>False when the grant could not be applied at all — the caller must NOT report
        /// success, because the item is not there.</returns>
        private static bool ApplyPurchaseGrant(ShopGrantDto grant)
        {
            if (grant == null || string.IsNullOrEmpty(grant.RefId))
            {
                Debug.LogError("[ShopTransaction] ApplyPurchaseGrant: the server reported ok with no grant.");
                return false;
            }

            int amount = Mathf.Max(1, grant.Amount);
            bool applied;

            switch (grant.Kind)
            {
                case InventoryGrants.KindClub:
                    if (ClubManager.Instance == null) { Debug.LogError("[ShopTransaction] no ClubManager."); return false; }
                    ClubManager.Instance.GrantClub(grant.RefId);
                    applied = true;
                    break;

                case InventoryGrants.KindCharacter:
                    if (CharacterManager.Instance == null) { Debug.LogError("[ShopTransaction] no CharacterManager."); return false; }
                    // False means "already owned", which after a server-side already_owned check can
                    // only be a re-applied grant — not a failure, and the id still has to be recorded.
                    CharacterManager.Instance.UnlockCharacter(grant.RefId);
                    applied = true;
                    break;

                case InventoryGrants.KindItem:
                    if (ItemManager.Instance == null) { Debug.LogError("[ShopTransaction] no ItemManager."); return false; }
                    ItemManager.Instance.AddItems(grant.RefId, amount);
                    applied = true;
                    break;

                case InventoryGrants.KindBall:
                    for (int i = 0; i < amount; i++) GrantBall(grant.RefId);
                    applied = true;
                    break;

                default:
                    Debug.LogError($"[ShopTransaction] Grant kind '{grant.Kind}' is not one the shop can " +
                                   "apply. It stays pending server-side; the boot drain handles it.");
                    return false;
            }

            if (!applied) return false;

            RecordAndAck(grant.Id);
            return true;
        }

        /// <summary>
        /// The three things that must follow a mid-session grant apply: the id goes in the save (so a
        /// boot drain cannot apply it a second time), the write-behind is told the blob moved, and the
        /// server is acked.
        /// </summary>
        private static void RecordAndAck(string grantId)
        {
            if (string.IsNullOrEmpty(grantId)) return;

            var host = SaveDataHost.Instance;
            if (host != null)
            {
                host.Data.appliedGrantIds ??= new List<string>();
                if (!host.Data.appliedGrantIds.Contains(grantId))
                    host.Data.appliedGrantIds.Add(grantId);
                host.MarkDirty();
            }
            else
            {
                Debug.LogWarning("[ShopTransaction] No SaveDataHost — the grant id was not recorded. " +
                                 "The boot drain will re-apply it, which the managers' own unique " +
                                 "checks make harmless for clubs and characters but NOT for stacks.");
            }

            // Push the new inventory blob. Without this the purchase would sit local until the next
            // event that happens to dirty the sync.
            InventorySyncService.Instance?.MarkDirty();

            // Fire-and-forget: the ack is the SERVER's idempotency lock and the id ledger above is the
            // client's. A lost ack costs one redundant drain next boot, which the ledger absorbs.
            new ApiInventoryTransport().AckGrants(new[] { grantId }, _ => { });
        }

        private static void Toast(string message)
        {
            var toast = Golfin.UI.Toast.ToastController.Instance;
            if (toast != null) toast.Show(message, 2f);
        }

        /// <summary>Increments a ball's persisted quantity (respects -1 = unlimited; UNCAPPED).</summary>
        private static void GrantBall(string ballId)
        {
            var host = SaveDataHost.Instance;
            if (host == null) { Debug.LogWarning("[ShopTransaction] GrantBall: SaveDataHost null."); return; }

            var q = host.Data.ballQuantities;
            if (q.TryGetValue(ballId, out var cur))
            {
                // UNCAPPED (2026-08-27) — a clamp here debited the player and delivered
                // nothing, and disagreed with InventoryGrants.AddQuantity, which never capped.
                if (cur >= 0) q[ballId] = cur + 1;   // -1 unlimited stays unlimited
            }
            else
            {
                q[ballId] = 1;
            }
            host.MarkDirty();
        }
    }
}
