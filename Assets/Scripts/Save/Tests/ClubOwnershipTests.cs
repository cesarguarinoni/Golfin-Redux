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
    /// </summary>
    [TestFixture]
    public class ClubOwnershipTests
    {
        // Mini catalog: the 4 required starter types + 2 extra purchasable clubs.
        private static List<ClubCatalogSpec> Catalog() => new List<ClubCatalogSpec>
        {
            new ClubCatalogSpec("club_driver_gf",     10,  100, 45,  "Driver"),
            new ClubCatalogSpec("club_wood_gf",       10,  100, 45,  "Wood"),
            new ClubCatalogSpec("club_iron7_mireo",   10,  100, 45,  "Iron"),
            new ClubCatalogSpec("club_putter_golfinx",10,  100, 45,  "Putter"),
            new ClubCatalogSpec("club_iron9_klyro",   120, 80,  500, "Iron"),   // purchasable
            new ClubCatalogSpec("club_driver_klyro",  80,  90,  200, "Driver"), // purchasable
        };

        private static readonly string[] StarterIds =
            { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_putter_golfinx" };

        private static readonly string[] RequiredTypes = { "Driver", "Wood", "Iron", "Putter" };

        // ── Starter seed + bag-safety (A1 / A4) ─────────────────────────────────

        [Test]
        public void SeedStarter_OwnsOnlyStarterSet_AndBagIsPlayable()
        {
            var save = new SaveData();
            ClubOwnershipService.SeedStarter(save, Catalog(), StarterIds);

            Assert.AreEqual(4, save.ownedClubs.Count, "fresh save owns only the 4 starter clubs");
            Assert.IsTrue(ClubOwnershipService.IsOwned(save, "club_driver_gf"));
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

            Assert.AreEqual(6, save.ownedClubs.Count, "grandfather owns the full catalog");
            Assert.IsTrue(ClubOwnershipService.IsOwned(save, "club_iron9_klyro"), "extras are owned when grandfathered");
            Assert.IsTrue(ClubOwnershipService.HasPlayableBag(save, Catalog(), RequiredTypes));

            var extra = save.ownedClubs.Find(c => c.clubId == "club_iron9_klyro");
            Assert.AreEqual(0, extra!.equippedBagSlot, "non-starter grandfathered clubs are owned but unequipped");
            var driver = save.ownedClubs.Find(c => c.clubId == "club_driver_gf");
            Assert.AreEqual(1, driver!.equippedBagSlot, "starter clubs are equipped to bag 1");
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
            Assert.AreEqual(5, save.ownedClubs.Count);
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
            Assert.AreEqual(4, save.ownedClubs.Count, "no duplicate club added");
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
            Assert.AreEqual(5, loaded!.ownedClubs.Count);
            Assert.IsTrue(ClubOwnershipService.IsOwned(loaded, "club_driver_klyro"), "granted club survives round-trip");

            var d = loaded.ownedClubs.Find(c => c.clubId == "club_driver_gf");
            Assert.AreEqual(33, d!.currentLevel);
            Assert.AreEqual(42, d.currentDurability);
            Assert.AreEqual(7,  d.spentPower);
            Assert.AreEqual(2,  d.equippedBagSlot, "equip slot restored from save (bag persists)");
        }

        // ── Migration (A3 grandfather signal) ───────────────────────────────────

        [Test]
        public void Migrator_PreV6_Unseeded_SetsGrandfatherSignal()
        {
            // A real existing save predating the club layer (no ownedClubs, never seeded).
            const string v5Json = @"{ ""schemaVersion"": 5, ""rewardPoints"": 4200 }";
            var data = JsonConvert.DeserializeObject<SaveData>(v5Json);
            Assert.IsNotNull(data);

            SaveSchemaMigrator.Migrate(data!);

            Assert.AreEqual(8, data!.schemaVersion, "v5 save migrates through v6, v7, and v8; must land at CurrentSchemaVersion 8");
            Assert.IsTrue(data.grandfatherClubs, "unseeded pre-v6 save must be flagged for grandfather-all (D-A3)");
            Assert.IsFalse(data.clubOwnershipSeeded, "migrator does not seed — ClubManager does");
            Assert.IsNotNull(data.ownedClubs);
            Assert.AreEqual(4200, data.rewardPoints, "existing data preserved");
        }

        [Test]
        public void Migrator_PreV6_AlreadySeeded_DoesNotGrandfather()
        {
            // The fresh-player reload case: session 1 wrote a v2 save AFTER ClubManager starter-seeded it
            // (clubOwnershipSeeded=true). Reload migrates 2→6 but must NOT grandfather it.
            const string seededV2 = @"{ ""schemaVersion"": 2, ""clubOwnershipSeeded"": true,
                ""ownedClubs"": [ { ""clubId"": ""club_driver_gf"", ""equippedBagSlot"": 1 } ] }";
            var data = JsonConvert.DeserializeObject<SaveData>(seededV2);
            Assert.IsNotNull(data);

            SaveSchemaMigrator.Migrate(data!);

            Assert.AreEqual(8, data!.schemaVersion, "v2 save migrates through v6, v7, and v8; must land at CurrentSchemaVersion 8");
            Assert.IsFalse(data.grandfatherClubs, "an already-seeded save must never be grandfathered on migrate");
            Assert.AreEqual(1, data.ownedClubs.Count, "existing owned list preserved");
        }

        [Test]
        public void Migrator_V6_MigratesToV8_NoGrandfather()
        {
            // v6 is no longer CurrentSchemaVersion (bumped to 7 by gacha_screen Stage 1, then to 8 by gacha_history Stage 1).
            // A v6 save gets migrated to v7 (gachaTickets seeded) then v8 (per-kind balances). grandfatherClubs must NOT be set.
            var data = new SaveData { schemaVersion = 6, clubOwnershipSeeded = true };
            SaveSchemaMigrator.Migrate(data);
            Assert.AreEqual(8, data.schemaVersion, "v6 migrates to v8 (gacha_screen Stage 1 + gacha_history Stage 1)");
            Assert.IsFalse(data.grandfatherClubs, "v5→v6 block does not run on a v6-start; no grandfather signal");
#pragma warning disable CS0618
            Assert.AreEqual(10, data.gachaTickets, "v6→v7 seeds gachaTickets=10 (test grant); v8 reads it but does not clear it");
#pragma warning restore CS0618
        }
    }
}
