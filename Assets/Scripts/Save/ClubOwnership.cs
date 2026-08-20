#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Save
{
    /// <summary>Result of a club grant (RP already spent by the caller before Grant()).</summary>
    public enum ClubGrantResult
    {
        Success,      // newly owned
        AlreadyOwned, // clubs are unique — no stacking; grant is a no-op
        Invalid       // unknown clubId (validated by the caller, not this pure layer)
    }

    /// <summary>
    /// Plain, Unity-free description of a club's initial persisted state, built by ClubManager
    /// from ClubDatabaseCSV and handed to the pure ownership service. Keeping the DB read on the
    /// Assembly-CSharp side lets all economy logic (seed / grant / bag-safety) live here, pure and
    /// EditMode-testable (asmdef test assemblies cannot reference Assembly-CSharp).
    /// clubType is the ClubType enum name (string) so Golfin.Save stays enum/dep-free.
    /// </summary>
    public readonly struct ClubCatalogSpec
    {
        public readonly string clubId;
        public readonly int    startingLevel;
        public readonly int    maxDurability;
        public readonly int    totalSPEarned;
        public readonly string clubType;

        public ClubCatalogSpec(string clubId, int startingLevel, int maxDurability,
                               int totalSPEarned, string clubType)
        {
            this.clubId        = clubId;
            this.startingLevel = startingLevel;
            this.maxDurability = maxDurability;
            this.totalSPEarned = totalSPEarned;
            this.clubType      = clubType;
        }
    }

    /// <summary>
    /// Pure club-ownership economy: seeding (grandfather / starter), granting, and the bag-safety
    /// invariant. Operates only on SaveData + plain ClubCatalogSpec — no Unity, no Resources,
    /// no MonoBehaviour — so Phase A (the save-schema risk surface) is fully EditMode-testable.
    /// ClubManager is the thin runtime adapter that builds the catalog and mirrors the results.
    /// </summary>
    public static class ClubOwnershipService
    {
        /// <summary>True if clubId is in the owned list (membership == ownership).</summary>
        public static bool IsOwned(SaveData save, string clubId)
        {
            if (save.ownedClubs == null) return false;
            foreach (var c in save.ownedClubs)
                if (c.clubId == clubId) return true;
            return false;
        }

        /// <summary>Build a PersistedClub at its initial state for a given spec + bag slot.</summary>
        public static PersistedClub MakePersisted(ClubCatalogSpec spec, int bagSlot) => new PersistedClub
        {
            clubId            = spec.clubId,
            currentLevel      = spec.startingLevel,
            currentDurability = spec.maxDurability, // start full
            maxDurability     = spec.maxDurability,
            equippedBagSlot   = bagSlot,
            totalSPEarned     = spec.totalSPEarned,
        };

        /// <summary>
        /// D-A3 grandfather seed: an existing player who predates club persistence keeps the clubs
        /// that existed when their save was written. <paramref name="grandfatherIds"/> is that
        /// PINNED set; ids in <paramref name="starterBagIds"/> are equipped to bag slot 1 so the bag
        /// is populated. Catalog entries outside the pin are purchasable, not-yet-owned templates.
        ///
        /// <para>
        /// <b>Why the pin is a parameter and not a call-site filter.</b> This method used to seed
        /// the ENTIRE catalog. That was harmless while the catalog was 7 rows and catastrophic the
        /// moment Clubs.csv grew to 799: a single grandfathered load would have granted every club
        /// in the game for free, permanently, and persisted it. The set a grandfathered player is
        /// owed is a property of the economy, so it is stated here — a future roster expansion
        /// cannot silently widen it again.
        /// </para>
        /// </summary>
        public static void SeedGrandfather(SaveData save, IReadOnlyList<ClubCatalogSpec> catalog,
                                           IReadOnlyCollection<string> grandfatherIds,
                                           IReadOnlyCollection<string> starterBagIds)
        {
            save.ownedClubs = new List<PersistedClub>(grandfatherIds.Count);
            foreach (var spec in catalog)
            {
                if (!grandfatherIds.Contains(spec.clubId)) continue;
                int slot = starterBagIds.Contains(spec.clubId) ? 1 : 0;
                save.ownedClubs.Add(MakePersisted(spec, slot));
            }
        }

        /// <summary>
        /// Fresh-save starter set: seed ONLY the starter clubs (the default bag) into ownedClubs,
        /// equipped to slot 1. Everything else in the catalog is a purchasable, not-yet-owned template.
        /// </summary>
        public static void SeedStarter(SaveData save, IReadOnlyList<ClubCatalogSpec> catalog,
                                       IReadOnlyCollection<string> starterBagIds)
        {
            save.ownedClubs = new List<PersistedClub>();
            foreach (var spec in catalog)
                if (starterBagIds.Contains(spec.clubId))
                    save.ownedClubs.Add(MakePersisted(spec, 1));
        }

        /// <summary>
        /// Grant a club (RP already spent by the caller). Idempotent: an already-owned club is a
        /// no-op (no dup, no stat reset). New clubs are added UNEQUIPPED (D5 = no auto-equip).
        /// </summary>
        public static ClubGrantResult Grant(SaveData save, ClubCatalogSpec spec)
        {
            save.ownedClubs ??= new List<PersistedClub>();
            if (IsOwned(save, spec.clubId)) return ClubGrantResult.AlreadyOwned;
            save.ownedClubs.Add(MakePersisted(spec, 0));
            return ClubGrantResult.Success;
        }

        /// <summary>
        /// Bag-safety invariant (A4): the EQUIPPED clubs must cover every required club type, so a
        /// fresh save never yields an unplayable bag. requiredTypes are ClubType enum names.
        ///
        /// requiredRoleGroups (optional, Order 761): each inner collection is a "wedge role" —
        /// the group is satisfied when ANY ONE of its types is equipped. This prevents exact-subtype
        /// false-failures: a P_Wedge satisfies the "wedge" role even though the role also lists
        /// A_Wedge and S_Wedge. Pass null (or omit) for legacy callers.
        /// </summary>
        public static bool HasPlayableBag(SaveData save, IReadOnlyList<ClubCatalogSpec> catalog,
                                          IReadOnlyCollection<string> requiredTypes,
                                          System.Collections.Generic.IEnumerable<System.Collections.Generic.IEnumerable<string>>? requiredRoleGroups = null)
        {
            if (save.ownedClubs == null) return false;
            var typeByClub = new Dictionary<string, string>(catalog.Count);
            foreach (var s in catalog) typeByClub[s.clubId] = s.clubType;

            var equippedTypes = new HashSet<string>();
            foreach (var c in save.ownedClubs)
                if (c.equippedBagSlot > 0 && typeByClub.TryGetValue(c.clubId, out var t))
                    equippedTypes.Add(t);

            foreach (var req in requiredTypes)
                if (!equippedTypes.Contains(req)) return false;

            // Role groups: each group satisfied when ANY ONE alternative type is equipped.
            if (requiredRoleGroups != null)
                foreach (var group in requiredRoleGroups)
                {
                    bool groupSatisfied = false;
                    foreach (var alt in group)
                        if (equippedTypes.Contains(alt)) { groupSatisfied = true; break; }
                    if (!groupSatisfied) return false;
                }

            return true;
        }
    }
}
