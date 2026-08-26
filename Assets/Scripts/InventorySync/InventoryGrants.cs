// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the admin grants queue, client side.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §4
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Save;
using Newtonsoft.Json;

namespace Golfin.InventorySync
{
    /// <summary>One row of <c>golfin_pending_grants</c>, as the client sees it.</summary>
    public sealed class InventoryGrant
    {
        [JsonProperty("id")]         public string Id = "";
        [JsonProperty("kind")]       public string Kind = "";
        [JsonProperty("ref_id")]     public string RefId = "";
        [JsonProperty("amount")]     public int    Amount = 1;
        [JsonProperty("note")]       public string? Note;
        [JsonProperty("created_at")] public string? CreatedAt;
    }

    /// <summary>Server reply shape: <c>{"data":{"grants":[…]}}</c>.</summary>
    public sealed class InventoryGrantList
    {
        [JsonProperty("grants")] public List<InventoryGrant> Grants = new List<InventoryGrant>();
    }

    /// <summary>
    /// Applying admin grants to the local save.
    ///
    /// <para>
    /// IDEMPOTENT BY GRANT ID, ON BOTH SIDES, AND THE CLIENT SIDE IS THE ONE THAT MATTERS. The
    /// server stamps <c>applied_at</c> when the client acks — but the client applies FIRST and acks
    /// SECOND, so there is a window where a grant is applied and the ack never lands (the app dies,
    /// the network drops). Without a client-side record the next boot would re-drain that grant and
    /// apply it a second time; <c>SaveData.appliedGrantIds</c> is what closes it.
    /// </para>
    /// <para>
    /// The ordering is deliberate and the other one is worse: ack-then-apply loses the grant
    /// outright if the app dies in between, and a lost grant is a support ticket while a redundant
    /// ack is nothing.
    /// </para>
    /// <para>
    /// ADDITIVE-ONLY, ENFORCED IN THREE PLACES: <c>amount &gt; 0</c> is a CHECK constraint in the
    /// schema, the admin UI cannot express a negative, and <see cref="Apply"/> below ignores any
    /// non-positive amount. A grant cannot take anything away — spends have a server path and this
    /// is not it.
    /// </para>
    /// </summary>
    public static class InventoryGrants
    {
        public const string KindClub      = "club";
        public const string KindCharacter = "character";
        public const string KindItem      = "item";
        public const string KindBall      = "ball";
        public const string KindTicket    = "ticket";
        public const string KindHole      = "hole";

        /// <summary>Result of a drain: what changed and what to ack.</summary>
        public readonly struct ApplyResult
        {
            /// <summary>Every grant id that is now accounted for locally — the newly applied ones
            /// AND the already-applied ones. Both get acked: an already-applied id whose ack was
            /// lost must be retried, or it comes back on every single boot forever.</summary>
            public readonly List<string> AckIds;

            /// <summary>How many grants actually changed the save this time.</summary>
            public readonly int AppliedCount;

            /// <summary>How many were skipped because their id was already recorded.</summary>
            public readonly int DuplicateCount;

            public ApplyResult(List<string> ackIds, int applied, int duplicates)
            {
                AckIds = ackIds;
                AppliedCount = applied;
                DuplicateCount = duplicates;
            }

            public bool Changed => AppliedCount > 0;
        }

        /// <summary>
        /// Apply every grant the save has not already seen, and report what to ack.
        ///
        /// <para>
        /// Quantities ADD (<c>+= amount</c>) rather than take a max, unlike the merge. That is not
        /// an inconsistency: the merge reconciles two views of the SAME history, where max is the
        /// only non-destructive answer; a grant is NEW history, and three repair kits granted twice
        /// is six. Applying it exactly once is what the id ledger guarantees.
        /// </para>
        /// </summary>
        public static ApplyResult Apply(IEnumerable<InventoryGrant>? grants, SaveData save,
                                        IInventoryCatalog? catalog)
        {
            var ack = new List<string>();
            int applied = 0, duplicates = 0;
            if (grants == null || save == null) return new ApplyResult(ack, 0, 0);

            catalog ??= EmptyInventoryCatalog.Instance;
            save.appliedGrantIds ??= new List<string>();
            var seen = new HashSet<string>(save.appliedGrantIds);

            foreach (var g in grants)
            {
                if (g == null || string.IsNullOrEmpty(g.Id) || string.IsNullOrEmpty(g.RefId)) continue;
                if (g.Amount <= 0) continue;    // additive-only; a non-positive grant is not a spend

                ack.Add(g.Id);

                if (!seen.Add(g.Id)) { duplicates++; continue; }
                save.appliedGrantIds.Add(g.Id);

                if (ApplyOne(g, save, catalog)) applied++;
            }

            return new ApplyResult(ack, applied, duplicates);
        }

        private static bool ApplyOne(InventoryGrant g, SaveData save, IInventoryCatalog catalog)
        {
            switch (g.Kind)
            {
                case KindClub:
                {
                    save.ownedClubs ??= new List<PersistedClub>();
                    // Clubs are unique — no stacking — so a grant of an owned club is a no-op, the
                    // same rule ClubOwnershipService.Grant already holds.
                    foreach (var c in save.ownedClubs)
                        if (c != null && c.clubId == g.RefId) return false;

                    var club = catalog.TryGetClubDefault(g.RefId, out var d)
                        ? InventoryProjector.CloneClub(d)
                        : new PersistedClub { clubId = g.RefId };
                    club.clubId = g.RefId;
                    club.equippedBagSlot = 0;    // granted UNEQUIPPED (D5 = no auto-equip)
                    save.ownedClubs.Add(club);
                    return true;
                }

                case KindCharacter:
                {
                    save.ownedCharacters ??= new List<PersistedCharacter>();
                    foreach (var c in save.ownedCharacters)
                        if (c != null && c.characterId == g.RefId)
                        {
                            if (c.isOwned) return false;
                            c.isOwned = true;    // unlocking a locked-with-progress row IS the grant
                            return true;
                        }

                    var ch = catalog.TryGetCharacterDefault(g.RefId, out var cd)
                        ? InventoryProjector.CloneCharacter(cd)
                        : new PersistedCharacter { characterId = g.RefId };
                    ch.characterId = g.RefId;
                    ch.isOwned = true;
                    save.ownedCharacters.Add(ch);
                    return true;
                }

                case KindItem:
                    save.itemQuantities ??= new Dictionary<string, int>();
                    return AddQuantity(save.itemQuantities, g.RefId, g.Amount);

                case KindBall:
                    save.ballQuantities ??= new Dictionary<string, int>();
                    return AddQuantity(save.ballQuantities, g.RefId, g.Amount);

                case KindTicket:
                {
                    if (!int.TryParse(g.RefId, out int kind)) return false;
                    save.ticketBalances ??= new List<PersistedTicketBalance>();
                    foreach (var t in save.ticketBalances)
                        if (t != null && t.ticketTypeInt == kind) { t.balance += g.Amount; return true; }
                    save.ticketBalances.Add(new PersistedTicketBalance
                        { ticketTypeInt = kind, balance = g.Amount });
                    return true;
                }

                case KindHole:
                {
                    if (!int.TryParse(g.RefId, out int hole)) return false;
                    save.unlockedHoles ??= new List<int>();
                    if (save.unlockedHoles.Contains(hole)) return false;
                    save.unlockedHoles.Add(hole);
                    return true;
                }

                default:
                    // An unknown kind is still ACKED (it was added to `ack` before this call) and
                    // still recorded as applied. It came from a server that knows something this
                    // build does not; leaving it pending would re-deliver it on every boot forever,
                    // which is a growing queue rather than a recoverable one.
                    return false;
            }
        }

        private static bool AddQuantity(IDictionary<string, int> into, string key, int amount)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (!into.TryGetValue(key, out int have)) { into[key] = amount; return true; }
            if (have < 0) return false;   // already unlimited (-1); adding to it means nothing
            into[key] = have + amount;
            return true;
        }
    }
}
