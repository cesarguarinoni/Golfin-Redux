#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Golfin.Save.Tests
{
    /// <summary>
    /// EditMode tests for Order 610 Phase A — the club-ownership economy.
    /// Pure: SaveData + ClubOwnershipService + the migrator; no Unity runtime, no Resources
    /// (asmdef test assemblies cannot reference Assembly-CSharp, so all economy logic lives in
    /// Golfin.Save and ClubManager is the thin runtime adapter). This is the save-schema risk
    /// surface the SPEC flags for red-team focus (§10): migration + bag-safety.
    ///
    /// Order 761 additions (2026-07-19):
    ///   - Catalog updated to include club_pwedge_royal (P_Wedge) in starter set.
    ///   - New tests: v8→v9 migration signal, HasPlayableBag wedge role-group, cohort (c) fresh save.
    /// </summary>
    [TestFixture]
    public class ClubOwnershipTests
    {
        // Mini catalog: the 5 required starter types (including Order 761 wedge) + 2 extra purchasable clubs.
        private static List<ClubCatalogSpec> Catalog() => new List<ClubCatalogSpec>
        {
            new ClubCatalogSpec("club_driver_gf",     10,  100, 45,  "Driver"),
            new ClubCatalogSpec("club_wood_gf",       10,  100, 45,  "Wood"),
            new ClubCatalogSpec("club_iron7_mireo",   10,  100, 45,  "Iron"),
            new ClubCatalogSpec("club_pwedge_royal",  10,  100, 45,  "P_Wedge"),  // Order 761 — default wedge
            new ClubCatalogSpec("club_putter_golfinx",10,  100, 45,  "Putter"),
            new ClubCatalogSpec("club_iron9_klyro",   120, 80,  500, "Iron"),     // purchasable
            new ClubCatalogSpec("club_driver_klyro",  80,  90,  200, "Driver"),   // purchasable
        };

        private static readonly string[] StarterIds =
            { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_pwedge_royal", "club_putter_golfinx" };

        private static readonly string[] RequiredTypes = { "Driver", "Wood", "Iron", "Putter" };

        // Wedge role-group (mirrors ClubManager.RequiredBagTypeGroups, Order 761).
        // Each inner array = one role; the group is satisfied when ANY one alternative is equipped.
        private static readonly string[][] WedgeRoleGroup = new[]
        {
            new[] { "A_Wedge", "P_Wedge", "S_Wedge" },
        };

        // ── Starter seed + bag-safety (A1 / A4) ─────────────────────────────────

        [Test]
        public void SeedStarter_OwnsOnlyStarterSet_AndBagIsPlayable()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, Catalog(), StarterIds);

            Assert.AreEqual(5, save.ownedClubs.Count, "fresh save owns only the 5 starter clubs (incl. wedge, Order 761)");
            Assert.IsTrue(ClubOwnershipService.IsOwned(save, "club_driver_gf"));
            Assert.IsTrue(ClubOwnershipService.IsOwned(save, "club_pwedge_royal"), "wedge must be in starter set (Order 761)");
            Assert.IsFalse(ClubOwnershipService.IsOwned(save, "club_iron9_klyro"), "extra clubs are purchasable, not owned");
            foreach (var c in save.ownedClubs)
                Assert.AreEqual(1, c.equippedBagSlot, $"{c.clubId} must be equipped to bag 1");
            Assert.IsTrue(ClubOwnershipService.HasPlayableBag(save, Catalog(), RequiredTypes),
                "starter set must cover all required club types (A4 bag-safety)");
        }

        [Test]
        public void HasPlayableBag_False_WhenRequiredTypeMissing()
        {
            var save = new SaveData();
            // Seed only 3 of 4 required types (no Putter).
            var partial = new[] { "club_driver_gf", "club_wood_gf", "club_iron7_mireo" };
            ClubOwnershipService.SeedStarter(save, Catalog(), partial);
            Assert.IsFalse(ClubOwnershipService.HasPlayableBag(save, Catalog(), RequiredTypes),
                "a bag missing a required type (Putter) is not playable");
        }

        // ── Grandfather seed (A3) ───────────────────────────────────────────────

        [Test]
        public void SeedGrandfather_OwnsFullCatalog_StarterEquipped()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedGrandfather(save, Catalog(), StarterIds);

            Assert.AreEqual(7, save.ownedClubs.Count, "grandfather owns the full catalog (5 starters + 2 purchasable)");
            Assert.IsTrue(ClubOwnershipService.IsOwned(save, "club_iron9_klyro"), "extras are owned when grandfathered");
            Assert.IsTrue(ClubOwnershipService.IsOwned(save, "club_pwedge_royal"), "wedge is owned when grandfathered");
            Assert.IsTrue(ClubOwnershipService.HasPlayableBag(save, Catalog(), RequiredTypes));

            var extra = save.ownedClubs.Find(c => c.clubId == "club_iron9_klyro");
            Assert.AreEqual(0, extra!.equippedBagSlot, "non-starter grandfathered clubs are owned but unequipped");
            var driver = save.ownedClubs.Find(c => c.clubId == "club_driver_gf");
            Assert.AreEqual(1, driver!.equippedBagSlot, "starter clubs are equipped to bag 1");
            var wedge = save.ownedClubs.Find(c => c.clubId == "club_pwedge_royal");
            Assert.AreEqual(1, wedge!.equippedBagSlot, "wedge is a starter club; must be equipped to bag 1 (Order 761)");
        }

        // ── Grant (A5) ──────────────────────────────────────────────────────────

        [Test]
        public void Grant_NewClub_ReturnsSuccess_AddsUnequipped()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, Catalog(), StarterIds);

            var spec = Catalog().Find(s => s.clubId == "club_iron9_klyro");
            var result = ClubOwnershipService.Grant(save, spec);

            Assert.AreEqual(ClubGrantResult.Success, result);
            Assert.AreEqual(6, save.ownedClubs.Count, "5 starters + 1 granted = 6");
            var granted = save.ownedClubs.Find(c => c.clubId == "club_iron9_klyro");
            Assert.IsNotNull(granted);
            Assert.AreEqual(0, granted!.equippedBagSlot, "D5: grant does not auto-equip");
            Assert.AreEqual(120, granted.currentLevel, "granted club uses its starting level");
            Assert.AreEqual(80,  granted.maxDurability);
            Assert.AreEqual(80,  granted.currentDurability, "granted club starts at full durability");
        }

        [Test]
        public void Grant_AlreadyOwned_IsNoOp_NoDupNoStatReset()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, Catalog(), StarterIds);

            // Simulate the owned driver having leveled up.
            var owned = save.ownedClubs.Find(c => c.clubId == "club_driver_gf");
            owned!.currentLevel = 25;

            var spec = Catalog().Find(s => s.clubId == "club_driver_gf");
            var result = ClubOwnershipService.Grant(save, spec);

            Assert.AreEqual(ClubGrantResult.AlreadyOwned, result);
            Assert.AreEqual(5, save.ownedClubs.Count, "no duplicate club added (5-club starter set, Order 761)");
            Assert.AreEqual(25, save.ownedClubs.Find(c => c.clubId == "club_driver_gf")!.currentLevel,
                "granting an owned club must not reset its progress");
        }

        // ── Persistence round-trip (A2 / A6) ────────────────────────────────────

        [Test]
        public void Grant_Persists_AndRoundTripsThroughJson()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, Catalog(), StarterIds);
            ClubOwnershipService.Grant(save, Catalog().Find(s => s.clubId == "club_driver_klyro"));

            // mutate a club's state to prove full-state persistence
            var driver = save.ownedClubs.Find(c => c.clubId == "club_driver_gf");
            driver!.currentLevel = 33; driver.currentDurability = 42; driver.spentPower = 7; driver.equippedBagSlot = 2;

            string json = JsonConvert.SerializeObject(save);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(6, loaded!.ownedClubs.Count, "5 starters + 1 granted = 6");
            Assert.IsTrue(ClubOwnershipService.IsOwned(loaded, "club_driver_klyro"), "granted club survives round-trip");

            var d = loaded.ownedClubs.Find(c => c.clubId == "club_driver_gf");
            Assert.AreEqual(33, d!.currentLevel);
            Assert.AreEqual(42, d.currentDurability);
            Assert.AreEqual(7,  d.spentPower);
            Assert.AreEqual(2,  d.equippedBagSlot, "equip slot restored from save (bag persists)");
        }

        // ── Migration (A3 grandfather signal + wedge backfill signal) ────────────

        [Test]
        public void Migrator_PreV6_Unseeded_SetsGrandfatherSignal()
        {
            // A real existing save predating the club layer (no ownedClubs, never seeded).
            const string v5Json = @"{ ""schemaVersion"": 5, ""rewardPoints"": 4200 }";
            var data = JsonConvert.DeserializeObject<SaveData>(v5Json);
            Assert.IsNotNull(data);

            SaveSchemaMigrator.Migrate(data!);

            // Must migrate through v6 (grandfather), v7, v8 (gacha), v9 (wedge) → CurrentSchemaVersion 9
            Assert.AreEqual(9, data!.schemaVersion, "v5 save migrates through v6→v9; must land at CurrentSchemaVersion 9");
            Assert.IsTrue(data.grandfatherClubs, "unseeded pre-v6 save must be flagged for grandfather-all (D-A3)");
            Assert.IsFalse(data.clubOwnershipSeeded, "migrator does not seed — ClubManager does");
            // clubOwnershipSeeded=false → v8→v9 must NOT set wedgeBackfillPending (fresh path gets wedge from DefaultBagIds)
            Assert.IsFalse(data.wedgeBackfillPending, "unseeded save must NOT set wedgeBackfillPending (gets wedge via grandfather seed)");
            Assert.IsNotNull(data.ownedClubs);
            Assert.AreEqual(4200, data.rewardPoints, "existing data preserved");
        }

        [Test]
        public void Migrator_PreV6_AlreadySeeded_DoesNotGrandfather()
        {
            // The fresh-player reload case: session 1 wrote a v2 save AFTER ClubManager starter-seeded it
            // (clubOwnershipSeeded=true). Reload migrates 2→9 but must NOT grandfather it.
            const string seededV2 = @"{ ""schemaVersion"": 2, ""clubOwnershipSeeded"": true,
                ""ownedClubs"": [ { ""clubId"": ""club_driver_gf"", ""equippedBagSlot"": 1 } ] }";
            var data = JsonConvert.DeserializeObject<SaveData>(seededV2);
            Assert.IsNotNull(data);

            SaveSchemaMigrator.Migrate(data!);

            // Must migrate through v6, v7, v8 (gacha), v9 (wedge) → CurrentSchemaVersion 9
            Assert.AreEqual(9, data!.schemaVersion, "v2 save migrates through v6→v9; must land at CurrentSchemaVersion 9");
            Assert.IsFalse(data.grandfatherClubs, "an already-seeded save must never be grandfathered on migrate");
            Assert.AreEqual(1, data.ownedClubs.Count, "existing owned list preserved");
            // clubOwnershipSeeded=true → v8→v9 MUST set wedgeBackfillPending (grant+equip wedge on next load)
            Assert.IsTrue(data.wedgeBackfillPending, "an already-seeded save must be flagged for wedge backfill (Order 761)");
        }

        [Test]
        public void Migrator_V6_MigratesToV9_NoGrandfather()
        {
            // v6 is no longer CurrentSchemaVersion (bumped through v7, v8 gacha, then v9 wedge backfill).
            // A v6 save gets migrated to v7 (gachaTickets seeded) then v8 (per-kind balances) then v9 (wedge).
            // grandfatherClubs must NOT be set (it was already set at v5→v6 time, not re-set here).
            var data = new SaveData { schemaVersion = 6, clubOwnershipSeeded = true };
            SaveSchemaMigrator.Migrate(data);
            Assert.AreEqual(9, data.schemaVersion, "v6 migrates to v9 (gacha Stage 1 + gacha_history + wedge backfill)");
            Assert.IsFalse(data.grandfatherClubs, "v5→v6 block does not run on a v6-start; no grandfather signal");
#pragma warning disable CS0618
            Assert.AreEqual(10, data.gachaTickets, "v6→v7 seeds gachaTickets=10 (test grant); v8 reads it but does not clear it");
#pragma warning restore CS0618
            // clubOwnershipSeeded=true → v8→v9 must set wedgeBackfillPending
            Assert.IsTrue(data.wedgeBackfillPending, "a v6 seeded save must be flagged for wedge backfill after v8→v9 (Order 761)");
        }

        // ── Order 761: v8→v9 migration signal ───────────────────────────────────

        [Test]
        public void Migrator_V8_SeededSave_SetsWedgeBackfillPending()
        {
            // Cohort (a) grandfathered + cohort (b) fresh-seeded-post-610 both have clubOwnershipSeeded=true.
            // The v8→v9 migration sets the backfill signal for both. ClubManager then grants+equips on load.
            var data = new SaveData { schemaVersion = 8, clubOwnershipSeeded = true };
            SaveSchemaMigrator.Migrate(data);
            Assert.AreEqual(9, data.schemaVersion, "v8 seeded save must migrate to v9");
            Assert.IsTrue(data.wedgeBackfillPending,
                "an already-seeded existing-player save must be flagged for wedge backfill (Order 761)");
        }

        [Test]
        public void Migrator_V8_UnseededSave_DoesNotSetWedgeBackfillPending()
        {
            // An unseeded v8 save = a save that somehow reached v8 without seeding clubs
            // (edge case; should not happen in production, but the migrator must be safe).
            var data = new SaveData { schemaVersion = 8, clubOwnershipSeeded = false };
            SaveSchemaMigrator.Migrate(data);
            Assert.AreEqual(9, data.schemaVersion);
            Assert.IsFalse(data.wedgeBackfillPending,
                "an unseeded save must NOT set the wedge backfill flag (wedge comes from DefaultBagIds + grandfather seed)");
        }

        // ── Order 761: cohort (c) — fresh save path ─────────────────────────────

        [Test]
        public void FreshSaveData_WedgeBackfillPending_IsFalse()
        {
            // Cohort (c): brand-new SaveData never runs Migrate() → wedgeBackfillPending stays false.
            // The wedge is seeded via DefaultBagIds (ClubManager.SeedStarter) instead of backfill.
            // This test proves the two paths never collide.
            var save = new SaveData();
            Assert.IsFalse(save.wedgeBackfillPending,
                "brand-new SaveData must have wedgeBackfillPending=false (gets wedge from DefaultBagIds, not migration)");
        }

        // ── Order 761: HasPlayableBag wedge role-group ───────────────────────────

        [Test]
        public void HasPlayableBag_WedgeRoleGroup_PWedge_Satisfies()
        {
            // The default bag (Order 761) carries P_Wedge → must satisfy the wedge role group.
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, Catalog(), StarterIds); // includes club_pwedge_royal (P_Wedge)
            Assert.IsTrue(
                ClubOwnershipService.HasPlayableBag(save, Catalog(), RequiredTypes, WedgeRoleGroup),
                "a bag with P_Wedge equipped must satisfy the wedge role group");
        }

        [Test]
        public void HasPlayableBag_WedgeRoleGroup_AWedge_Satisfies()
        {
            // A bag with A_Wedge instead of P_Wedge must also satisfy the wedge role group.
            var extraCatalog = new List<ClubCatalogSpec>(Catalog())
            {
                new ClubCatalogSpec("club_awedge_fyloe", 10, 100, 45, "A_Wedge"),
            };
            var save = new SaveData();
            // Seed with A_Wedge instead of P_Wedge
            ClubOwnershipService.SeedStarter(save, extraCatalog,
                new[] { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_awedge_fyloe", "club_putter_golfinx" });
            Assert.IsTrue(
                ClubOwnershipService.HasPlayableBag(save, extraCatalog, RequiredTypes, WedgeRoleGroup),
                "A_Wedge must also satisfy the wedge role group (any sub-type counts)");
        }

        [Test]
        public void HasPlayableBag_WedgeRoleGroup_NoWedge_Fails()
        {
            // A bag with NO wedge of any sub-type must fail the wedge role group.
            var save = new SaveData();
            // Seed with the old 4-club set (Driver/Wood/Iron7/Putter — no wedge)
            ClubOwnershipService.SeedStarter(save, Catalog(),
                new[] { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_putter_golfinx" });
            Assert.IsFalse(
                ClubOwnershipService.HasPlayableBag(save, Catalog(), RequiredTypes, WedgeRoleGroup),
                "a bag with NO wedge (no A_Wedge / P_Wedge / S_Wedge) must fail the wedge role group");
        }

        // ── Order 761: wedge backfill run-once invariant ─────────────────────────

        [Test]
        public void WedgeBackfillFlag_IsFalse_OnFreshSave_AfterMigrate_WhenNotSeeded()
        {
            // Run migrate on a completely fresh (never-seeded) save: must never set the flag.
            // Covers the no-regression-to-seed-gate hard gate (§ Hard gates, gate 4).
            var data = new SaveData();
            // Fresh SaveData schemaVersion defaults to 2; Migrate will run all blocks.
            // clubOwnershipSeeded=false → v8→v9 must NOT set wedgeBackfillPending.
            SaveSchemaMigrator.Migrate(data);
            Assert.IsFalse(data.wedgeBackfillPending,
                "Migrate on a truly fresh (never-seeded) save must not set wedgeBackfillPending");
        }
    }
}
