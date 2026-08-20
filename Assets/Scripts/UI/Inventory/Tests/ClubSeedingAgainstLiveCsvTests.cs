// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — ClubSeedingAgainstLiveCsvTests
//
// The blocker gate. ClubOwnershipTests exercises the seeding rules against a 7-row
// mini catalog; this fixture runs the SAME rules against the real 799-row Clubs.csv
// with the REAL id lists reflected off ClubManager. That combination is what was
// missing: the grandfather path read "seed the catalog", which was indistinguishable
// from "seed the 7 legacy clubs" until the catalog grew.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Golfin.Save;
using NUnit.Framework;

namespace Golfin.Inventory.Tests
{
    [TestFixture]
    public class ClubSeedingAgainstLiveCsvTests
    {
        private IList _rows = null!;

        [SetUp]
        public void SetUp()
        {
            string? text = ClubRosterProd.ReadShippedCsv();
            if (text == null)
            {
                Assert.Inconclusive(
                    $"Shipped {ClubRosterProd.CsvRelativePath} not found. " +
                    "This test only runs in a full project checkout.");
            }
            _rows = ClubRosterProd.Parse(text);
        }

        /// <summary>
        /// The live catalog, in the shape ClubOwnershipService consumes. Levels/durability are not
        /// what this fixture asserts, so they are uniform — the id set and the club type are.
        /// </summary>
        private List<ClubCatalogSpec> LiveCatalog() =>
            _rows.Cast<object>()
                 .Select(r => new ClubCatalogSpec(
                     ClubRosterProd.Field<string>(r, "id"),
                     10, 100, 0,
                     ClubRosterProd.EnumName(r, "type")))
                 .ToList();

        // The real shipped lists, read off ClubManager — never re-declared here, so these tests
        // cannot pass against a stale local copy of the ids.
        private static string[] StarterIds     => ClubRosterProd.IdList("DefaultBagIds");
        private static string[] GrandfatherIds => ClubRosterProd.IdList("LegacyGrandfatherIds");
        private static string[] LegacyBagIds   => ClubRosterProd.IdList("LegacyDefaultBagIds");

        // ── The blocker ───────────────────────────────────────────────────────

        /// <summary>
        /// Grandfather seeding against the LIVE CSV must yield exactly the legacy set.
        /// Before the pin this granted all 799 clubs — free, persisted, irreversible.
        /// </summary>
        [Test]
        public void Grandfather_AgainstLiveCsv_YieldsExactlyTheLegacySet()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedGrandfather(save, LiveCatalog(), GrandfatherIds, LegacyBagIds);

            var owned = save.ownedClubs.Select(c => c.clubId).OrderBy(i => i).ToArray();

            CollectionAssert.AreEqual(GrandfatherIds.OrderBy(i => i).ToArray(), owned,
                "a grandfathered player must receive the legacy clubs and NOTHING else");
            Assert.AreEqual(7, owned.Length, "the legacy set is the 7 rows that shipped before the roster expansion");
            Assert.Less(owned.Length, _rows.Count,
                $"REGRESSION: grandfather granted {owned.Length} of {_rows.Count} catalog clubs — " +
                "the seed is reading the catalog again instead of the pinned id list");
        }

        /// <summary>Starter seeding against the LIVE CSV must yield exactly the 7 GOLFIN commons.</summary>
        [Test]
        public void Starter_AgainstLiveCsv_YieldsExactlyTheGolfinCommons()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, LiveCatalog(), StarterIds);

            var owned = save.ownedClubs.Select(c => c.clubId).OrderBy(i => i).ToArray();

            CollectionAssert.AreEqual(StarterIds.OrderBy(i => i).ToArray(), owned,
                "a new player must start with exactly the GOLFIN Common set");
            Assert.AreEqual(7, owned.Length);
            foreach (var c in save.ownedClubs)
                Assert.AreEqual(1, c.equippedBagSlot, $"{c.clubId} must be equipped to bag 1");
        }

        // ── The id lists must actually exist in the shipped CSV ───────────────

        [Test]
        public void EveryStarterId_ExistsInTheShippedCsv()
        {
            var ids = new HashSet<string>(ClubRosterProd.AllIds(_rows));
            var missing = StarterIds.Where(i => !ids.Contains(i)).ToList();
            Assert.IsEmpty(missing,
                $"a starter id absent from Clubs.csv is silently dropped by SeedStarter, leaving the " +
                $"new player short a club type: {string.Join(", ", missing)}");
        }

        [Test]
        public void EveryGrandfatherId_ExistsInTheShippedCsv()
        {
            var ids = new HashSet<string>(ClubRosterProd.AllIds(_rows));
            var missing = GrandfatherIds.Where(i => !ids.Contains(i)).ToList();
            Assert.IsEmpty(missing,
                $"a legacy id absent from Clubs.csv means a grandfathered player loses that club: " +
                $"{string.Join(", ", missing)}");
        }

        // ── Bag shape ─────────────────────────────────────────────────────────

        [Test]
        public void StarterSet_IsOneClubOfEveryType_AndFitsTheBag()
        {
            var typeById = LiveCatalog().ToDictionary(s => s.clubId, s => s.clubType);
            var types    = StarterIds.Select(i => typeById[i]).ToList();

            CollectionAssert.AreEquivalent(
                new[] { "Driver", "Wood", "Iron", "P_Wedge", "A_Wedge", "S_Wedge", "Putter" },
                types,
                "the starter bag is one club of each of the 7 types");
            Assert.LessOrEqual(StarterIds.Length, 8, "a bag holds at most MAX_CLUBS_PER_BAG (8) clubs");
        }

        [Test]
        public void StarterSet_IsAllCommonRarity_AndAllGolfinBrand()
        {
            var byId = _rows.Cast<object>().ToDictionary(r => ClubRosterProd.Field<string>(r, "id"), r => r);

            foreach (var id in StarterIds)
            {
                Assert.IsTrue(byId.ContainsKey(id), $"{id} missing from Clubs.csv");
                Assert.AreEqual("Common", ClubRosterProd.EnumName(byId[id], "rarity"),
                    $"{id} must be Common — the starter set is the neutral lowest-rarity baseline");
                Assert.AreEqual("GOLFIN", ClubRosterProd.Field<string>(byId[id], "brand"),
                    $"{id} must be the GOLFIN house brand");
            }
        }

        [Test]
        public void StarterBag_IsPlayable()
        {
            var save = new SaveData();
            var catalog = LiveCatalog();
            ClubOwnershipService.SeedStarter(save, catalog, StarterIds);

            Assert.IsTrue(
                ClubOwnershipService.HasPlayableBag(
                    save, catalog,
                    new[] { "Driver", "Wood", "Iron", "Putter" },
                    new[] { new[] { "A_Wedge", "P_Wedge", "S_Wedge" } }),
                "the GOLFIN starter bag must satisfy the A4 bag-safety invariant");
        }

        [Test]
        public void GrandfatheredBag_IsPlayable()
        {
            var save = new SaveData();
            var catalog = LiveCatalog();
            ClubOwnershipService.SeedGrandfather(save, catalog, GrandfatherIds, LegacyBagIds);

            Assert.IsTrue(
                ClubOwnershipService.HasPlayableBag(
                    save, catalog,
                    new[] { "Driver", "Wood", "Iron", "Putter" },
                    new[] { new[] { "A_Wedge", "P_Wedge", "S_Wedge" } }),
                "an existing player must not be left with an unplayable bag by the pin");
        }
    }
}
