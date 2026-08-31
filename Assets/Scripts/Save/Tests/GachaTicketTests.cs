#nullable enable
// Assets/Scripts/Save/Tests/GachaTicketTests.cs
// gacha_screen Stage 1 + gacha_history Stage 1 — EditMode Tests
// Pure save-layer tests: SaveData schema, SaveSchemaMigrator migration, JSON round-trip,
// and arithmetic simulation of GachaTicketManager's add behaviour.
//
// gacha_client_real_pull §4.4: SpendTickets is gone and the dev grant of 10 is gone from all
// three sites, so the spend simulations and the "seeded to 10" migration assertions went with
// them — ticketBalances is now a DISPLAY CACHE of the server ledger, which starts at 0.
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

        // SpendTickets is DELETED (gacha_client_real_pull §4.4), and with it the three tests that
        // simulated its arithmetic. They are not replaced by SetFromServer equivalents here: a
        // client-side subtraction is exactly the behaviour the task removed, so a test asserting
        // one would be a test defending the bug. What replaces it is the server's own coverage
        // (golfin_ticket_credit's insufficient path, gacha_server_pull §7) plus the §7 live E2E.
        //
        // The one client-side invariant left worth pinning is that a fresh save starts at ZERO.

        [Test]
        public void FreshSave_HasNoTickets()
        {
            var save = new SaveData();
            Assert.AreEqual(0, GetStandard(save),
                "A fresh save must hold NO tickets — the ledger starts at 0 for every player " +
                "(plan §9) and a client-seeded balance is one the server refuses to spend.");
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
        public void CurrentSchemaVersion_IsMonotonicAndAtLeastV10()
        {
            // A ROLLBACK sentinel, not a pin.
            //
            // This test used to assert the literal 9, and it went red the moment
            // starting_character_selection shipped v10 — so did fifteen others, and the suite sat
            // red for long enough that a real regression could have hidden in it. Renamed and
            // re-aimed by content_overlay_catalogs: what is worth catching is the version going
            // BACKWARDS (a bad merge dropping a migration block), which a floor catches and a
            // literal only catches by breaking on every legitimate bump.
            //
            // Raise the floor deliberately when a migration lands; never to make a red test green.
            Assert.GreaterOrEqual(SaveSchemaMigrator.CurrentSchemaVersion, 10,
                "CurrentSchemaVersion must not go backwards. v10 = starting_character_selection " +
                "(v9 was Order 761's wedge backfill, v8 gacha_history Stage 1).");
        }

        [Test]
        public void Migration_V6ToV8_LeavesStandardTicketsAtZero()
        {
            // Simulate an existing v6 save file.
            const string v6Json = "{\"schemaVersion\":6,\"rewardPoints\":1234,\"selectedCharacterId\":\"char_nova\"}";
            var data = JsonConvert.DeserializeObject<SaveData>(v6Json)!;

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion,
                "A v6 save must migrate all the way to CurrentSchemaVersion");
            // The v6→v7 test grant of 10 is GONE (gacha_client_real_pull §4.4) — the ledger is
            // server-side and starts at 0. v7→v8 carries the (now zero) legacy value forward.
            Assert.AreEqual(0, GetStandard(data),
                "Migration v6→v8 must leave the Standard balance at 0: the client no longer grants " +
                "itself tickets, and a seeded balance is one /gacha/pull would refuse to spend.");
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

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion,
                "must migrate all the way to CurrentSchemaVersion");
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

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion,
                "A v7 save must migrate all the way to CurrentSchemaVersion");
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

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion,
                "must migrate all the way to CurrentSchemaVersion");
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

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion,
                "A v5 save must migrate all the way to CurrentSchemaVersion");
            Assert.AreEqual(0, GetStandard(data),
                "The v6→v7→v8 chain no longer seeds a test grant (gacha_client_real_pull §4.4).");
            Assert.IsTrue(data.grandfatherClubs, "v5→v6 must still set grandfatherClubs for unseeded save");
            Assert.AreEqual(500, data.rewardPoints, "rewardPoints must survive chain migration");
        }

        [Test]
        public void Migration_AlreadyV8_AdvancesToCurrent_PreservesBalanceAndRp()
        {
            // A v8 save passed to Migrate() runs the v8→v9 wedge-backfill step and every block
            // after it. clubOwnershipSeeded defaults false → wedgeBackfillPending stays false.
            // ticketBalances and rewardPoints must be untouched; schemaVersion reaches CURRENT.
            var data = new SaveData { schemaVersion = 8, rewardPoints = 777 };
            data.ticketBalances.Add(new PersistedTicketBalance { ticketTypeInt = 0, balance = 25 });

            SaveSchemaMigrator.Migrate(data);

            Assert.AreEqual(SaveSchemaMigrator.CurrentSchemaVersion, data.schemaVersion,
                "must migrate all the way to CurrentSchemaVersion");
            Assert.AreEqual(25, GetStandard(data), "Standard balance must be preserved by v8→v9");
            Assert.AreEqual(777, data.rewardPoints, "rewardPoints must be preserved by v8→v9");
            Assert.IsFalse(data.wedgeBackfillPending,
                "wedgeBackfillPending must stay false when clubOwnershipSeeded=false");
        }
    }
}
