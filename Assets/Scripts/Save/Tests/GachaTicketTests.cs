#nullable enable
// Assets/Scripts/Save/Tests/GachaTicketTests.cs
// gacha_screen Stage 1 + gacha_history Stage 1 — EditMode Tests
// Pure save-layer tests: SaveData schema, SaveSchemaMigrator migration, JSON round-trip,
// and arithmetic simulation of GachaTicketManager add/spend/insufficient behavior.
//
// NOTE: GachaTicketManager is a DontDestroyOnLoad MonoBehaviour that depends on
// SaveDataHost.Instance — not directly unit-testable in EditMode without Unity runtime.
// The arithmetic it performs is now over ticketBalances[Standard], which is simulated
// here directly on SaveData. Red-team focus: v6→v7→v8 migration chain.

using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Golfin.Save.Tests
{
    [TestFixture]
    public class GachaTicketTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────

        private static PersistedTicketBalance? FindStandard(SaveData data)
        {
            foreach (var b in data.ticketBalances)
                if (b.ticketTypeInt == 0) return b;
            return null;
        }

        private static int GetStandard(SaveData data)
            => FindStandard(data)?.balance ?? 0;

        private static void SetStandard(SaveData data, int value)
        {
            var entry = FindStandard(data);
            if (entry != null) { entry.balance = value; return; }
            data.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = value });
        }

        // ── Arithmetic simulation (mirrors GachaTicketManager v8 behavior) ────
        // GTM now reads/writes ticketBalances, not gachaTickets.

        [Test]
        public void AddTickets_IncrementsBalance()
        {
            var save = new SaveData();
            SetStandard(save, 10);
            SetStandard(save, GetStandard(save) + 5); // mirrors GTM.AddTickets(TicketType.Standard, 5)
            Assert.AreEqual(15, GetStandard(save),
                "AddTickets(5) on balance 10 should yield 15");
        }

        [Test]
        public void SpendTickets_Sufficient_DecrementsAndReturnsTrue()
        {
            var save = new SaveData();
            SetStandard(save, 10);
            bool canAfford = GetStandard(save) >= 1; // mirrors GTM.CanAfford(TicketType.Standard, 1)
            if (canAfford) SetStandard(save, GetStandard(save) - 1);
            Assert.IsTrue(canAfford, "Should be able to afford 1 ticket from balance of 10");
            Assert.AreEqual(9, GetStandard(save), "Balance should be 9 after spending 1");
        }

        [Test]
        public void SpendTickets_Insufficient_ReturnsFalseAndLeavesBalanceUnchanged()
        {
            var save = new SaveData();
            SetStandard(save, 3);
            int before = GetStandard(save);
            bool canAfford = GetStandard(save) >= 10; // mirrors GTM.CanAfford(TicketType.Standard, 10)
            if (canAfford) SetStandard(save, GetStandard(save) - 10);
            Assert.IsFalse(canAfford, "Balance of 3 cannot cover cost of 10");
            Assert.AreEqual(before, GetStandard(save),
                "Balance must be unchanged when spend is rejected");
        }

        [Test]
        public void SpendTickets_ExactBalance_SucceedsAndLeavesZero()
        {
            var save = new SaveData();
            SetStandard(save, 10);
            bool canAfford = GetStandard(save) >= 10;
            if (canAfford) SetStandard(save, GetStandard(save) - 10);
            Assert.IsTrue(canAfford, "Exact balance should succeed");
            Assert.AreEqual(0, GetStandard(save), "Balance should be 0 after spending exact amount");
        }

        // ── JSON round-trip ───────────────────────────────────────────────────

        [Test]
        public void TicketBalances_SurvivesJsonRoundTrip()
        {
            var save = new SaveData { rewardPoints = 9999 };
            save.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = 7 });
            string json = JsonConvert.SerializeObject(save);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json)!;
            Assert.AreEqual(7, GetStandard(loaded), "ticketBalances[Standard] must survive JSON round-trip");
            Assert.AreEqual(9999, loaded.rewardPoints, "rewardPoints must be unchanged by round-trip");
        }

        [Test]
        public void TicketBalances_DefaultsToEmptyOnFreshDeserialize()
        {
            // A save JSON that predates schema v8 has no ticketBalances key.
            const string oldJson = "{\"schemaVersion\":7,\"rewardPoints\":50000}";
            var loaded = JsonConvert.DeserializeObject<SaveData>(oldJson)!;
            Assert.IsNotNull(loaded.ticketBalances,
                "ticketBalances field must be initialized to empty list even when absent from JSON");
            Assert.AreEqual(0, loaded.ticketBalances.Count,
                "No Standard entry should exist in a v7 save before migration");
        }

        // ── Migration tests ───────────────────────────────────────────────────

        [Test]
        public void CurrentSchemaVersion_Is9()
        {
            // Simple sentinel: catches accidental version rollback.
            // Bumped 8→9 by Order 761 (wedge backfill signal, v8→v9 migration block).
            Assert.AreEqual(9, SaveSchemaMigrator.CurrentSchemaVersion,
                "CurrentSchemaVersion must be 9 after Order 761 wedge backfill (gacha_history Stage 1 was 8)");
        }

        [Test]
        public void Migration_V6ToV8_SetsStandardTicketsTo10()
        {
            // Simulate an existing v6 save file.
            const string v6Json = "{\"schemaVersion\":6,\"rewardPoints\":1234,\"selectedCharacterId\":\"char_nova\"}";
            var data = JsonConvert.DeserializeObject<SaveData>(v6Json)!;

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(9, data.schemaVersion, "Schema version must be 9 after full migration chain (v6→v7→v8→v9)");
            // v6→v7 seeds gachaTickets=10, v7→v8 carries that to ticketBalances[Standard]=10
            Assert.AreEqual(10, GetStandard(data),
                "Migration v6→v8 must seed Standard balance to 10 (test grant, carried from v6→v7)");
        }

        [Test]
        public void Migration_V6ToV8_PreservesExistingFields()
        {
            const string v6Json =
                "{\"schemaVersion\":6," +
                "\"rewardPoints\":99000," +
                "\"selectedCharacterId\":\"char_nova\"," +
                "\"lifetimeRpEarned\":5000," +
                "\"rpDaily\":200," +
                "\"clubOwnershipSeeded\":true}";

            var data = JsonConvert.DeserializeObject<SaveData>(v6Json)!;
            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(9, data.schemaVersion);
            Assert.AreEqual(99000, data.rewardPoints,    "rewardPoints must survive chain migration");
            Assert.AreEqual("char_nova", data.selectedCharacterId, "selectedCharacterId must survive");
            Assert.AreEqual(5000L, data.lifetimeRpEarned, "lifetimeRpEarned must survive");
            Assert.AreEqual(200L, data.rpDaily,            "rpDaily must survive");
            Assert.IsTrue(data.clubOwnershipSeeded,         "clubOwnershipSeeded must survive");
        }

        [Test]
        public void Migration_V7ToV8_PreservesBalance()
        {
            // A v7 save that already has a non-zero balance must carry it to ticketBalances.
#pragma warning disable CS0618
            var data = new SaveData { schemaVersion = 7, gachaTickets = 42 };
#pragma warning restore CS0618

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(9, data.schemaVersion, "Schema must be 9 after v7→v8→v9");
            Assert.AreEqual(42, GetStandard(data),
                "Existing gachaTickets balance (42) must be preserved in ticketBalances[Standard]");
        }

        [Test]
        public void Migration_V7ToV8_Idempotent_WhenStandardAlreadyPresent()
        {
            // If ticketBalances already has a Standard entry, v7→v8 must NOT overwrite it.
#pragma warning disable CS0618
            var data = new SaveData { schemaVersion = 7, gachaTickets = 99 };
#pragma warning restore CS0618
            data.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = 5 });

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(9, data.schemaVersion);
            Assert.AreEqual(5, GetStandard(data),
                "Pre-existing Standard entry must not be overwritten by v7→v8 migration");
            int count = 0;
            foreach (var b in data.ticketBalances)
                if (b.ticketTypeInt == 0) count++;
            Assert.AreEqual(1, count, "There must be exactly one Standard entry after migration");
        }

        [Test]
        public void Migration_V5ToV8_ChainMigratesCorrectly()
        {
            // A save two versions behind must migrate through v5→v6→v7→v8.
            const string v5Json =
                "{\"schemaVersion\":5," +
                "\"rewardPoints\":500," +
                "\"clubOwnershipSeeded\":false}";

            var data = JsonConvert.DeserializeObject<SaveData>(v5Json)!;
            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(9, data.schemaVersion, "Schema version must reach 9 (v5→v6→v7→v8→v9)");
            Assert.AreEqual(10, GetStandard(data), "Standard balance seeded by v6→v7→v8 chain");
            Assert.IsTrue(data.grandfatherClubs, "v5→v6 must still set grandfatherClubs for unseeded save");
            Assert.AreEqual(500, data.rewardPoints, "rewardPoints must survive chain migration");
        }

        [Test]
        public void Migration_AlreadyV8_AdvancesToV9_PreservesBalanceAndRp()
        {
            // A v8 save passed to Migrate() runs the v8→v9 wedge-backfill step.
            // clubOwnershipSeeded defaults false → wedgeBackfillPending stays false.
            // ticketBalances and rewardPoints must be untouched; schemaVersion advances to 9.
            var data = new SaveData { schemaVersion = 8, rewardPoints = 777 };
            data.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = 25 });

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(9, data.schemaVersion,
                "v8 save must advance to v9 (v8→v9 wedge-backfill step, Order 761)");
            Assert.AreEqual(25, GetStandard(data), "Standard balance must be preserved by v8→v9");
            Assert.AreEqual(777, data.rewardPoints, "rewardPoints must be preserved by v8→v9");
            Assert.IsFalse(data.wedgeBackfillPending,
                "wedgeBackfillPending must stay false when clubOwnershipSeeded=false");
        }
    }
}
