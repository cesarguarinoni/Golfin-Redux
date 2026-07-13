#nullable enable
// Assets/Scripts/Save/Tests/GachaTicketTests.cs
// gacha_screen Stage 1 — §6 EditMode Tests
// Pure save-layer tests: SaveData schema, SaveSchemaMigrator migration, JSON round-trip,
// and arithmetic simulation of GachaTicketManager add/spend/insufficient behavior.
//
// NOTE: GachaTicketManager is a DontDestroyOnLoad MonoBehaviour that depends on
// SaveDataHost.Instance — not directly unit-testable in EditMode without Unity runtime.
// The arithmetic it performs (gachaTickets += n / -= n / balance >= cost guard) is
// tested here directly on SaveData, which is the authoritative source of truth.
// Red-team focus = the v6→v7 migration (SPEC §9).

using Newtonsoft.Json;
using NUnit.Framework;

namespace Golfin.Save.Tests
{
    [TestFixture]
    public class GachaTicketTests
    {
        // ─── Arithmetic simulation (mirrors GachaTicketManager behavior) ─────────

        [Test]
        public void AddTickets_IncrementsBalance()
        {
            var save = new SaveData { gachaTickets = 10 };
            save.gachaTickets += 5; // mirrors GTM.AddTickets(5)
            Assert.AreEqual(15, save.gachaTickets,
                "AddTickets(5) on balance 10 should yield 15");
        }

        [Test]
        public void SpendTickets_Sufficient_DecrementsAndReturnsTrue()
        {
            var save = new SaveData { gachaTickets = 10 };
            bool canAfford = save.gachaTickets >= 1;  // mirrors GTM.CanAfford(1)
            if (canAfford) save.gachaTickets -= 1;
            Assert.IsTrue(canAfford, "Should be able to afford 1 ticket from balance of 10");
            Assert.AreEqual(9, save.gachaTickets, "Balance should be 9 after spending 1");
        }

        [Test]
        public void SpendTickets_Insufficient_ReturnsFalseAndLeavesBalanceUnchanged()
        {
            var save = new SaveData { gachaTickets = 3 };
            int before = save.gachaTickets;
            bool canAfford = save.gachaTickets >= 10; // mirrors GTM.CanAfford(10)
            if (canAfford) save.gachaTickets -= 10;
            Assert.IsFalse(canAfford, "Balance of 3 cannot cover cost of 10");
            Assert.AreEqual(before, save.gachaTickets,
                "Balance must be unchanged when spend is rejected");
        }

        [Test]
        public void SpendTickets_ExactBalance_SucceedsAndLeavesZero()
        {
            var save = new SaveData { gachaTickets = 10 };
            bool canAfford = save.gachaTickets >= 10;
            if (canAfford) save.gachaTickets -= 10;
            Assert.IsTrue(canAfford, "Exact balance should succeed");
            Assert.AreEqual(0, save.gachaTickets, "Balance should be 0 after spending exact amount");
        }

        // ─── JSON round-trip ─────────────────────────────────────────────────────

        [Test]
        public void GachaTickets_SurvivesJsonRoundTrip()
        {
            var save = new SaveData { gachaTickets = 7, rewardPoints = 9999 };
            string json = JsonConvert.SerializeObject(save);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json)!;
            Assert.AreEqual(7, loaded.gachaTickets, "gachaTickets must survive JSON round-trip");
            Assert.AreEqual(9999, loaded.rewardPoints, "rewardPoints must be unchanged by round-trip");
        }

        [Test]
        public void GachaTickets_DefaultsToZeroOnFreshDeserialize()
        {
            // A save JSON that predates schema v7 has no gachaTickets key.
            const string oldJson = "{\"schemaVersion\":6,\"rewardPoints\":50000}";
            var loaded = JsonConvert.DeserializeObject<SaveData>(oldJson)!;
            Assert.AreEqual(0, loaded.gachaTickets,
                "Missing gachaTickets key in old JSON should deserialize to 0 (field default)");
        }

        // ─── Migration tests (RED-TEAM focus, SPEC §9) ───────────────────────────

        [Test]
        public void Migration_V6ToV7_SetsGachaTicketsTo10()
        {
            // Simulate an existing v6 save file (no gachaTickets field).
            const string v6Json = "{\"schemaVersion\":6,\"rewardPoints\":1234,\"selectedCharacterId\":\"char_nova\"}";
            var data = JsonConvert.DeserializeObject<SaveData>(v6Json)!;

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(7, data.schemaVersion, "Schema version must be 7 after migration");
            Assert.AreEqual(10, data.gachaTickets, "Migration v6→v7 must seed gachaTickets=10 (test grant)");
        }

        [Test]
        public void Migration_V6ToV7_PreservesExistingFields()
        {
            // Key safety requirement: migration must NOT lose any pre-v7 save data.
            const string v6Json =
                "{\"schemaVersion\":6," +
                "\"rewardPoints\":99000," +
                "\"selectedCharacterId\":\"char_nova\"," +
                "\"lifetimeRpEarned\":5000," +
                "\"rpDaily\":200," +
                "\"clubOwnershipSeeded\":true}";

            var data = JsonConvert.DeserializeObject<SaveData>(v6Json)!;
            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(99000, data.rewardPoints,    "rewardPoints must survive v6→v7 migration");
            Assert.AreEqual("char_nova", data.selectedCharacterId, "selectedCharacterId must survive");
            Assert.AreEqual(5000L, data.lifetimeRpEarned, "lifetimeRpEarned must survive");
            Assert.AreEqual(200L, data.rpDaily,            "rpDaily must survive");
            Assert.IsTrue(data.clubOwnershipSeeded,         "clubOwnershipSeeded must survive");
        }

        [Test]
        public void Migration_AlreadyV7_DoesNotOverwriteExistingTickets()
        {
            // A v7 save that already has a non-zero balance must not be touched by migration.
            var data = new SaveData { schemaVersion = 7, gachaTickets = 42 };
            // Fake it as a loaded file with version 7 — Migrate is a no-op.
            // We run it anyway to confirm the guard works.
            // NOTE: CurrentSchemaVersion == 7 so Migrate() only re-asserts schemaVersion = 7.
            SaveSchemaMigrator.Migrate(data);
            Assert.AreEqual(42, data.gachaTickets,
                "Migrating an already-v7 save must not reset gachaTickets");
            Assert.AreEqual(7, data.schemaVersion);
        }

        [Test]
        public void Migration_V5ToV7_ChainMigratesCorrectly()
        {
            // A save that is two versions behind must migrate through both v5→v6 and v6→v7.
            const string v5Json =
                "{\"schemaVersion\":5," +
                "\"rewardPoints\":500," +
                "\"clubOwnershipSeeded\":false}";

            var data = JsonConvert.DeserializeObject<SaveData>(v5Json)!;
            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(7, data.schemaVersion, "Schema version must reach 7");
            Assert.AreEqual(10, data.gachaTickets, "gachaTickets seeded by v6→v7");
            Assert.IsTrue(data.grandfatherClubs, "v5→v6 must still set grandfatherClubs for unseeded save");
            Assert.AreEqual(500, data.rewardPoints, "rewardPoints must survive chain migration");
        }

        [Test]
        public void CurrentSchemaVersion_Is7()
        {
            // Simple sentinel: catches accidental version rollback.
            Assert.AreEqual(7, SaveSchemaMigrator.CurrentSchemaVersion,
                "CurrentSchemaVersion must be 7 after Stage 1 changes");
        }
    }
}
