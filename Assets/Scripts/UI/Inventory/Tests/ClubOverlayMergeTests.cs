// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — ClubOverlayMergeTests
//
// SPEC §1: "extend ClubCsvParser's EditMode tests, do not fork it." These drive
// the REAL ClubCsvParser.Parse(csv, overlay) through the same reflection helper
// the rest of this fixture family uses, against the SHIPPED Clubs.csv — so a
// merge rule can never pass against a private copy of the roster.
//
// The rule under test is field-by-field, not row-for-row: a published row is a
// SPARSE PATCH that overrides the columns it names and leaves everything else
// bundled. Getting that backwards is how an operator editing basePower blanks
// every sprite name on the row.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;

namespace Golfin.Inventory.Tests
{
    [TestFixture]
    public class ClubOverlayMergeTests
    {
        private const string KnownId = "club_driver_gf";

        private static string ShippedCsv()
        {
            string? text = ClubRosterProd.ReadShippedCsv();
            if (text == null)
                Assert.Inconclusive($"Shipped {ClubRosterProd.CsvRelativePath} not found. " +
                                    "This test only runs in a full project checkout.");
            return text!;
        }

        private static Dictionary<string, string?> Data(params (string k, string? v)[] pairs)
        {
            var d = new Dictionary<string, string?>();
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }

        // ── The baseline: no overlay changes nothing ──────────────────────────

        [Test]
        public void NoOverlay_ProducesExactlyTheBundledRoster()
        {
            string csv = ShippedCsv();

            IList bundled = ClubRosterProd.Parse(csv);
            IList merged  = ClubRosterProd.Parse(csv, null);

            Assert.AreEqual(bundled.Count, merged.Count,
                "Parse(csv) and Parse(csv, null) must be the same roster — Phase 1 behaviour is the floor");
            CollectionAssert.AreEqual(ClubRosterProd.AllIds(bundled), ClubRosterProd.AllIds(merged));
        }

        // ── Field-by-field merge ──────────────────────────────────────────────

        [Test]
        public void APublishedColumn_OverridesOnlyThatColumn()
        {
            string csv = ShippedCsv();

            object baseline = ClubRosterProd.Row(ClubRosterProd.Parse(csv), KnownId)!;
            Assert.IsNotNull(baseline, $"{KnownId} must exist in the shipped roster");

            string bundledName    = ClubRosterProd.Field<string>(baseline, "name");
            string bundledSprite  = ClubRosterProd.Field<string>(baseline, "portraitSprite");
            int    bundledMaxDur  = ClubRosterProd.Field<int>(baseline, "maxDurability");

            var overlay = ClubRosterProd.Overlay("clubs",
                (KnownId, true, Data(("basePower", "99"))));

            object merged = ClubRosterProd.Row(ClubRosterProd.Parse(csv, overlay), KnownId)!;

            Assert.AreEqual(99, ClubRosterProd.Field<int>(merged, "basePower"),
                "the published column wins");
            Assert.AreEqual(bundledName, ClubRosterProd.Field<string>(merged, "name"),
                "a column the overlay did not name must keep its bundled value");
            Assert.AreEqual(bundledSprite, ClubRosterProd.Field<string>(merged, "portraitSprite"),
                "…especially the sprite names — this is how an operator editing one stat would " +
                "otherwise blank the art on the row");
            Assert.AreEqual(bundledMaxDur, ClubRosterProd.Field<int>(merged, "maxDurability"));

            Assert.IsTrue(ClubRosterProd.Field<bool>(merged, "overlayApplied"));
            Assert.IsFalse(ClubRosterProd.Field<bool>(merged, "overlayAppended"));
        }

        [Test]
        public void APublishedBlankCell_DoesNotBlankTheBundledValue()
        {
            // The overlay is a sparse PATCH. A blank cell means "not specified", not "set to empty" —
            // otherwise every column the admin panel round-trips as empty would wipe its bundled value.
            string csv = ShippedCsv();

            object baseline = ClubRosterProd.Row(ClubRosterProd.Parse(csv), KnownId)!;
            string bundledName = ClubRosterProd.Field<string>(baseline, "name");

            var overlay = ClubRosterProd.Overlay("clubs",
                (KnownId, true, Data(("name", ""), ("brand", "   "), ("basePower", "77"))));

            object merged = ClubRosterProd.Row(ClubRosterProd.Parse(csv, overlay), KnownId)!;

            Assert.AreEqual(bundledName, ClubRosterProd.Field<string>(merged, "name"));
            Assert.AreEqual(77, ClubRosterProd.Field<int>(merged, "basePower"),
                "…while the columns that DO carry a value still apply");
        }

        [Test]
        public void AnUnknownPublishedColumn_IsIgnoredWithoutBreakingTheRow(  )
        {
            // I4: a new admin column must not need a client change to be safe.
            string csv = ShippedCsv();

            var overlay = ClubRosterProd.Overlay("clubs",
                (KnownId, true, Data(("someColumnThisBuildHasNeverHeardOf", "x"), ("baseAccuracy", "44"))));

            object merged = ClubRosterProd.Row(ClubRosterProd.Parse(csv, overlay), KnownId)!;

            Assert.AreEqual(44, ClubRosterProd.Field<int>(merged, "baseAccuracy"),
                "the known column applies and the unknown one is simply not read");
        }

        [Test]
        public void MaxDurabilityAndMaxLevel_AreOverlayable()
        {
            // These two are the ones the clamp reads, so they get their own assertion.
            string csv = ShippedCsv();

            var overlay = ClubRosterProd.Overlay("clubs",
                (KnownId, true, Data(("maxDurability", "60"), ("maxLevel", "25"), ("startLevel", "5"))));

            object merged = ClubRosterProd.Row(ClubRosterProd.Parse(csv, overlay), KnownId)!;

            Assert.AreEqual(60, ClubRosterProd.Field<int>(merged, "maxDurability"));
            Assert.AreEqual(25, ClubRosterProd.Field<int>(merged, "maxLevel"));
            Assert.AreEqual(5,  ClubRosterProd.Field<int>(merged, "startLevel"));
        }

        [Test]
        public void StartLevel_IsReadFromTheShippedCsv()
        {
            // The column has been in Clubs.csv all along and the parser never read it. It is the
            // lower bound of the level clamp, so a zero here would silently widen the band.
            object row = ClubRosterProd.Row(ClubRosterProd.Parse(ShippedCsv()), KnownId)!;
            Assert.AreEqual(10, ClubRosterProd.Field<int>(row, "startLevel"),
                "club_driver_gf is Common → startLevel 10 in the shipped CSV");
        }

        // ── I6 — deactivate, never delete ─────────────────────────────────────

        [Test]
        public void ADeactivatedRow_StaysInTheRosterAndIsMarkedInactive()
        {
            // I6: is_active=false means gone from the shop and the pools, still fully renderable in
            // the bag of a player who owns one. Dropping the row would make an owned club
            // un-renderable, which is the failure this invariant exists to prevent.
            string csv = ShippedCsv();

            var overlay = ClubRosterProd.Overlay("clubs",
                (KnownId, /*isActive:*/ false, Data(("basePower", "80"))));

            IList rows = ClubRosterProd.Parse(csv, overlay);
            object? row = ClubRosterProd.Row(rows, KnownId);

            Assert.IsNotNull(row, "a deactivated club must STILL be in the roster");
            Assert.IsFalse(ClubRosterProd.Field<bool>(row!, "isActive"));
            Assert.AreEqual(80, ClubRosterProd.Field<int>(row!, "basePower"),
                "and its published stats still apply — deactivated is not frozen");
        }

        [Test]
        public void ABundledRowWithNoOverlay_IsActiveByDefault()
        {
            object row = ClubRosterProd.Row(ClubRosterProd.Parse(ShippedCsv(), null), KnownId)!;
            Assert.IsTrue(ClubRosterProd.Field<bool>(row, "isActive"),
                "a catalog the server has never spoken about cannot have been deactivated");
        }

        // ── Append ────────────────────────────────────────────────────────────

        [Test]
        public void AnOverlayRowWithANewId_IsAppendedAfterEveryBundledRow()
        {
            string csv = ShippedCsv();
            int bundledCount = ClubRosterProd.Parse(csv).Count;

            var overlay = ClubRosterProd.Overlay("clubs",
                ("club_brand_new", true, Data(
                    ("id", "club_brand_new"), ("name", "Brand New"), ("type", "Driver"),
                    ("rarity", "Rare"), ("basePower", "70"), ("maxDurability", "120"),
                    ("maxLevel", "119"))));

            IList rows = ClubRosterProd.Parse(csv, overlay);

            Assert.AreEqual(bundledCount + 1, rows.Count);

            object appended = ClubRosterProd.Row(rows, "club_brand_new")!;
            Assert.IsNotNull(appended);
            Assert.AreEqual("Brand New", ClubRosterProd.Field<string>(appended, "name"));
            Assert.AreEqual(120, ClubRosterProd.Field<int>(appended, "maxDurability"));
            Assert.AreEqual("Rare", ClubRosterProd.EnumName(appended, "rarity"));
            Assert.IsTrue(ClubRosterProd.Field<bool>(appended, "overlayAppended"));

            // Order matters: the roster is index-addressed in places, so appended rows go LAST.
            var ids = ClubRosterProd.AllIds(rows);
            Assert.AreEqual("club_brand_new", ids[ids.Count - 1],
                "appended rows must land after every bundled one");
        }

        [Test]
        public void AnAppendedRowWithNoId_IsSkipped()
        {
            string csv = ShippedCsv();
            int bundledCount = ClubRosterProd.Parse(csv).Count;

            // The envelope id exists but the data bag carries no `id` column, so ParseRow — which
            // reads the id out of the columns — has nothing to key on and drops it.
            var overlay = ClubRosterProd.Overlay("clubs",
                ("club_ghost", true, Data(("name", "Ghost"))));

            Assert.AreEqual(bundledCount, ClubRosterProd.Parse(csv, overlay).Count,
                "a row that cannot produce an id is not appended");
        }

        // ── The provenance fields the runtime adapter's §5 veto depends on ────

        [Test]
        public void APatchedRowCarriesItsPreMergeSelf()
        {
            // ClubDatabaseCSV reverts to `bundled` when a published sprite does not resolve (§5).
            // If that reference were ever dropped, the veto would silently become a no-op.
            string csv = ShippedCsv();

            var overlay = ClubRosterProd.Overlay("clubs",
                (KnownId, true, Data(("portraitSprite", "Sprite-That-Does-Not-Exist"))));

            object merged = ClubRosterProd.Row(ClubRosterProd.Parse(csv, overlay), KnownId)!;

            object? bundled = ClubRosterProd.Field<object>(merged, "bundled");
            Assert.IsNotNull(bundled, "a patched row must carry the pre-merge row for §5 to fall back to");
            Assert.AreEqual("Sprite-That-Does-Not-Exist",
                ClubRosterProd.Field<string>(merged, "portraitSprite"));
            Assert.AreNotEqual("Sprite-That-Does-Not-Exist",
                ClubRosterProd.Field<string>(bundled!, "portraitSprite"),
                "…and that fallback must still hold the ORIGINAL sprite name");
        }

        [Test]
        public void AnOverlayNamingIdsTheRosterDoesNotHave_DoesNotDisturbTheBundledRows()
        {
            string csv = ShippedCsv();
            IList bundled = ClubRosterProd.Parse(csv);

            var overlay = ClubRosterProd.Overlay("clubs",
                ("club_unrelated", true, Data(("id", "club_unrelated"), ("name", "Unrelated"))));

            IList merged = ClubRosterProd.Parse(csv, overlay);

            Assert.AreEqual(bundled.Count + 1, merged.Count);
            foreach (var id in ClubRosterProd.AllIds(bundled))
                Assert.IsNotNull(ClubRosterProd.Row(merged, id), $"{id} must survive the merge");
        }
    }
}
