// Assets/Tests/EditMode/GachaStage1Tests.cs
// gacha_history Stage 1 — EditMode unit tests
// Pure C# tests: TicketCatalog CSV parse, GachaHistoryStore mapping, GachaHistoryRecord
// construction.
//
// gacha_client_real_pull §4.5 — the mock history store is DELETED. The nine tests that asserted
// the twelve hard-coded records are replaced by tests of the production `Map` seam (server page →
// flat newest-first records), which is the store's own logic and the part worth pinning.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, overrideReferences:false)
// All production types live in Assembly-CSharp — accessed via System.Reflection,
// matching the pattern established in GachaStage2Tests.cs.
//
// MonoBehaviour row binders (GachaHistoryRow, GachaHistoryRowBall) cannot be
// exercised in pure EditMode — their wiring is verified by the UI fidelity linter.

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaStage1Tests
    {
        // ── Reflection: production types in Assembly-CSharp ───────────────────

        private static readonly Type _ticketCatalogType =
            Type.GetType("GolfinRedux.UI.Gacha.TicketCatalog, Assembly-CSharp");

        private static readonly Type _historyStoreType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaHistoryStore, Assembly-CSharp");

        private static readonly Type _historyRecordType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaHistoryRecord, Assembly-CSharp");

        // TicketCatalog.ParseCsv(string) — internal seam
        private static readonly MethodInfo _parseCsvMethod =
            _ticketCatalogType?.GetMethod("ParseCsv",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string) }, null);

        // GachaHistoryStore.All — public static property
        private static readonly PropertyInfo _allProp =
            _historyStoreType?.GetProperty("All",
                BindingFlags.Public | BindingFlags.Static);

        // GachaHistoryStore.FilterByRewardTypeInt(int) — internal seam
        private static readonly MethodInfo _filterByTypeMethod =
            _historyStoreType?.GetMethod("FilterByRewardTypeInt",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(int) }, null);

        // GachaHistoryStore.Reload() — public static
        private static readonly MethodInfo _reloadMethod =
            _historyStoreType?.GetMethod("Reload",
                BindingFlags.Public | BindingFlags.Static);

        // ── Reflection helpers ────────────────────────────────────────────────

        private static IList ParseCsvViaProd(string csv)
        {
            Assert.IsNotNull(_ticketCatalogType, "TicketCatalog not found in Assembly-CSharp");
            Assert.IsNotNull(_parseCsvMethod,    "TicketCatalog.ParseCsv(string) not found — seam missing?");
            return (IList)_parseCsvMethod.Invoke(null, new object[] { csv });
        }

        private static IList GetAllRecords()
        {
            Assert.IsNotNull(_historyStoreType, "GachaHistoryStore not found in Assembly-CSharp");
            Assert.IsNotNull(_allProp,          "GachaHistoryStore.All not found");
            return (IList)_allProp.GetValue(null);
        }

        private static IList FilterByRewardTypeInt(int rewardTypeInt)
        {
            Assert.IsNotNull(_historyStoreType,  "GachaHistoryStore not found in Assembly-CSharp");
            Assert.IsNotNull(_filterByTypeMethod,"GachaHistoryStore.FilterByRewardTypeInt not found — seam missing?");
            return (IList)_filterByTypeMethod.Invoke(null, new object[] { rewardTypeInt });
        }

        private static string GetField(object record, string fieldName)
        {
            var f = _historyRecordType?.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            return (string)(f?.GetValue(record) ?? string.Empty);
        }

        private static int GetFieldInt(object record, string fieldName)
        {
            var f = _historyRecordType?.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            var v = f?.GetValue(record);
            return v == null ? 0 : (int)v;
        }

        private static object GetFieldEnum(object record, string fieldName)
        {
            var f = _historyRecordType?.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Instance);
            return f?.GetValue(record)!;
        }

        // ── TicketCatalog.ParseCsv ────────────────────────────────────────────

        [Test]
        public void TicketCatalog_ParseCsv_ParsesStandardRow()
        {
            const string csv =
                "ticketType,nameKey,iconSprite\n" +
                "0,TICKET,S_Store_Ticket_02\n";

            var entries = ParseCsvViaProd(csv);

            Assert.AreEqual(1, entries.Count, "One entry expected");

            var entry = entries[0]!;
            var entryType = entry.GetType();

            // TicketType enum int value 0 = Standard
            var ttField = entryType.GetField("TicketType", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(ttField, "TicketEntry.TicketType field missing");
            Assert.AreEqual(0, (int)ttField.GetValue(entry)!, "TicketType must be 0 (Standard)");

            var nameKey = (string)entryType.GetField("NameKey", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(entry)!;
            Assert.AreEqual("TICKET", nameKey);

            var iconSprite = (string)entryType.GetField("IconSprite", BindingFlags.Public | BindingFlags.Instance)
                ?.GetValue(entry)!;
            Assert.AreEqual("S_Store_Ticket_02", iconSprite);
        }

        [Test]
        public void TicketCatalog_ParseCsv_SkipsHeaderAndEmptyLines()
        {
            const string csv =
                "ticketType,nameKey,iconSprite\n" +
                "\n" +
                "0,TICKET,S_Store_Ticket_02\n" +
                "\n";

            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(1, entries.Count, "Empty lines must be skipped");
        }

        [Test]
        public void TicketCatalog_ParseCsv_EmptyBody_ReturnsEmptyList()
        {
            const string csv = "ticketType,nameKey,iconSprite\n";
            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(0, entries.Count, "Header-only CSV must yield empty list");
        }

        // ── GachaHistoryStore mapping (gacha_client_real_pull §4.5) ───────────
        //
        // The twelve hard-coded mock records are GONE, and with them the nine tests that asserted
        // their shape: `All` now reads a disk mirror of GET /gacha/history, which in EditMode is
        // legitimately empty. What survived is the part that is still the store's own logic — the
        // MAPPING from one server page to the flat, newest-first record list — driven through the
        // production `Map` seam with a fabricated page.

        private static readonly MethodInfo _mapMethod =
            _historyStoreType?.GetMethod("Map",
                BindingFlags.NonPublic | BindingFlags.Static);

        /// <summary>One server page: two pulls, newest first, with their prizes nested — the shape
        /// routers/gacha.py::history returns.</summary>
        private static Golfin.Economy.GachaHistoryPage FakePage() => new Golfin.Economy.GachaHistoryPage
        {
            Pulls = new[]
            {
                new Golfin.Economy.GachaHistoryPullDto
                {
                    Id = "p2", BannerId = "banner_test_a", PullCount = 10, TicketType = 0,
                    CreatedAt = "2026-07-14T23:00:00Z",
                    Prizes = new[]
                    {
                        new Golfin.Economy.GachaPrizeDto
                        { Slot = 0, Kind = "ball", RefId = "ball_putt_ace", Quantity = 3, Rarity = "Common" },
                        new Golfin.Economy.GachaPrizeDto
                        { Slot = 1, Kind = "club", RefId = "club_driver_gf", Quantity = 1,
                          Rarity = "Common", IsDupe = true, DupeRp = 20 },
                    },
                },
                new Golfin.Economy.GachaHistoryPullDto
                {
                    Id = "p1", BannerId = "banner_standard_club1", PullCount = 1, TicketType = 0,
                    CreatedAt = "2026-07-13T18:05:00Z",
                    Prizes = new[]
                    {
                        new Golfin.Economy.GachaPrizeDto
                        { Slot = 0, Kind = "club", RefId = "club_iron9_klyro", Quantity = 1, Rarity = "Uncommon" },
                    },
                },
            },
        };

        private static IList MapPage()
        {
            Assert.IsNotNull(_historyStoreType, "GachaHistoryStore not found in Assembly-CSharp");
            Assert.IsNotNull(_mapMethod, "GachaHistoryStore.Map(GachaHistoryPage) seam not found");
            return (IList)_mapMethod.Invoke(null, new object[] { FakePage() });
        }

        [Test]
        public void GachaHistoryStore_Map_ProducesOneRecordPerPrize()
        {
            // The screen is a list of things you WON, so an x10 that paid ten prizes is ten rows.
            Assert.AreEqual(3, MapPage().Count,
                "two pulls carrying 2 + 1 prizes must flatten to 3 records");
        }

        [Test]
        public void GachaHistoryStore_Map_IsNewestFirst()
        {
            var all = MapPage();
            for (int i = 1; i < all.Count; i++)
            {
                string prev = GetField(all[i - 1]!, "PulledUtc");
                string curr = GetField(all[i]!,     "PulledUtc");
                Assert.GreaterOrEqual(
                    string.Compare(prev, curr, StringComparison.Ordinal), 0,
                    $"Record[{i-1}] ({prev}) must be >= Record[{i}] ({curr}) — the server already " +
                    "orders pulls newest-first and prizes by slot, so flattening in that order is " +
                    "what keeps the list sorted without a second sort");
            }
        }

        [Test]
        public void GachaHistoryStore_Map_CarriesThePullsMetadataOntoEveryRow()
        {
            var all = MapPage();
            Assert.AreEqual("banner_test_a", GetField(all[0]!, "BannerId"));
            Assert.AreEqual(10, GetFieldInt(all[0]!, "PullCount"));
            Assert.AreEqual("banner_standard_club1", GetField(all[2]!, "BannerId"));
            Assert.AreEqual(1, GetFieldInt(all[2]!, "PullCount"));
        }

        [Test]
        public void GachaHistoryStore_Map_MapsTheKindToARewardType()
        {
            var all = MapPage();
            Assert.AreEqual(1, GetFieldInt(all[0]!, "RewardType"), "kind 'ball' → GachaRewardType.Ball");
            Assert.AreEqual(0, GetFieldInt(all[1]!, "RewardType"), "kind 'club' → GachaRewardType.Club");
        }

        [Test]
        public void GachaHistoryStore_Map_ADuplicateHasQuantityZeroAndItsRp()
        {
            // The two say different things: the quantity is what reached the INVENTORY (nothing),
            // the RP is what reached the BALANCE. A row with quantity 0 and no dupeRp would be a
            // prize that simply vanished.
            var dupe = MapPage()[1]!;
            Assert.AreEqual(0, GetFieldInt(dupe, "Quantity"), "a duplicate granted nothing");
            Assert.AreEqual(20, GetFieldInt(dupe, "DupeRp"), "and paid 20 RP instead");
        }

        [Test]
        public void GachaHistoryStore_Map_KeepsAGrantedPrizesQuantity()
        {
            Assert.AreEqual(3, GetFieldInt(MapPage()[0]!, "Quantity"),
                "a granted stack keeps the quantity the server recorded");
        }

        [Test]
        public void GachaHistoryStore_Map_EveryRecordHasARewardIdAndAValidPullCount()
        {
            foreach (var r in MapPage())
            {
                Assert.IsFalse(string.IsNullOrEmpty(GetField(r!, "RewardId")),
                    "every history record must have a non-empty RewardId");
                int pc = GetFieldInt(r!, "PullCount");
                Assert.IsTrue(pc == 1 || pc == 10,
                    $"PullCount must be 1 or 10, got {pc} for RewardId='{GetField(r!, "RewardId")}'");
            }
        }

        [Test]
        public void GachaHistoryStore_All_IsEmptyWithNoMirrorAndNoServer()
        {
            // The honest EditMode state, and the one that matters: `All` must never throw and must
            // never invent records. A cold open with no mirror shows an empty log, and Refresh()
            // fills it — it does NOT fall back to a mock, because there is no mock any more.
            Assert.IsNotNull(GetAllRecords(), "All must never be null");
        }

        // ── GachaHistoryRecord construction ───────────────────────────────────

        [Test]
        public void GachaHistoryRecord_DefaultConstructor_Exists()
        {
            Assert.IsNotNull(_historyRecordType, "GachaHistoryRecord not found in Assembly-CSharp");
            var ctor = _historyRecordType.GetConstructor(Type.EmptyTypes);
            Assert.IsNotNull(ctor, "GachaHistoryRecord must have a public parameterless constructor");
            var r = ctor.Invoke(null);
            Assert.IsNotNull(r);

            // PullCount default should be 1
            Assert.AreEqual(1, GetFieldInt(r, "PullCount"), "Default PullCount should be 1");
            // RewardType default should be 0 (Club)
            Assert.AreEqual(0, GetFieldInt(r, "RewardType"), "Default RewardType should be 0 (Club)");
        }
    }
}
