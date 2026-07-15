// Assets/Tests/EditMode/GachaStage1Tests.cs
// gacha_history Stage 1 — EditMode unit tests
// Pure C# tests: TicketCatalog CSV parse, GachaHistoryStore ordering/filter,
// GachaHistoryRecord construction.
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

        // ── GachaHistoryStore ordering + filter ───────────────────────────────

        [Test]
        public void GachaHistoryStore_All_IsNotEmpty()
        {
            var all = GetAllRecords();
            Assert.IsNotNull(all);
            Assert.Greater(all.Count, 0, "Mock store must contain at least one record");
        }

        [Test]
        public void GachaHistoryStore_All_IsSortedNewestFirst()
        {
            var all = GetAllRecords();
            for (int i = 1; i < all.Count; i++)
            {
                string prev = GetField(all[i - 1]!, "PulledUtc");
                string curr = GetField(all[i]!,     "PulledUtc");
                Assert.GreaterOrEqual(
                    string.Compare(prev, curr, StringComparison.Ordinal), 0,
                    $"Record[{i-1}] ({prev}) must be >= Record[{i}] ({curr}) — list must be newest-first");
            }
        }

        [Test]
        public void GachaHistoryStore_All_ContainsAtLeastTwelveRecords()
        {
            var all = GetAllRecords();
            Assert.GreaterOrEqual(all.Count, 12, "Mock store must contain >=12 records (7 clubs + 5 balls)");
        }

        [Test]
        public void GachaHistoryStore_All_ContainsClubAndBallRecords()
        {
            var all = GetAllRecords();
            bool hasClub = false, hasBall = false;
            foreach (var r in all)
            {
                int rt = GetFieldInt(r!, "RewardType");
                if (rt == 0) hasClub = true; // GachaRewardType.Club = 0
                if (rt == 1) hasBall = true; // GachaRewardType.Ball = 1
            }
            Assert.IsTrue(hasClub, "Mock store must contain at least one Club record");
            Assert.IsTrue(hasBall, "Mock store must contain at least one Ball record");
        }

        [Test]
        public void GachaHistoryStore_Filter_ByClub_OnlyReturnsClubs()
        {
            // GachaRewardType.Club = int 0
            var clubs = FilterByRewardTypeInt(0);
            Assert.Greater(clubs.Count, 0, "At least one Club record expected");
            foreach (var r in clubs)
            {
                int rt = GetFieldInt(r!, "RewardType");
                Assert.AreEqual(0, rt, $"Filter(Club) returned non-Club record: {GetField(r!, "RewardId")}");
            }
        }

        [Test]
        public void GachaHistoryStore_Filter_ByBall_OnlyReturnsBalls()
        {
            // GachaRewardType.Ball = int 1
            var balls = FilterByRewardTypeInt(1);
            Assert.Greater(balls.Count, 0, "At least one Ball record expected");
            foreach (var r in balls)
            {
                int rt = GetFieldInt(r!, "RewardType");
                Assert.AreEqual(1, rt, $"Filter(Ball) returned non-Ball record: {GetField(r!, "RewardId")}");
            }
        }

        [Test]
        public void GachaHistoryStore_BallRecords_UseKnownBallIds()
        {
            var balls = FilterByRewardTypeInt(1);
            var knownIds = new[] { "ball_golfin", "ball_putt_ace" };
            foreach (var r in balls)
            {
                string id = GetField(r!, "RewardId");
                CollectionAssert.Contains(knownIds, id,
                    $"Ball record uses unknown ballId '{id}' — must be one of: ball_golfin, ball_putt_ace");
            }
        }

        [Test]
        public void GachaHistoryStore_AllRecords_HaveNonEmptyRewardId()
        {
            foreach (var r in GetAllRecords())
                Assert.IsFalse(string.IsNullOrEmpty(GetField(r!, "RewardId")),
                    "Every history record must have a non-empty RewardId");
        }

        [Test]
        public void GachaHistoryStore_AllRecords_HaveValidPullCount()
        {
            foreach (var r in GetAllRecords())
            {
                int pc = GetFieldInt(r!, "PullCount");
                Assert.IsTrue(pc == 1 || pc == 10,
                    $"PullCount must be 1 or 10, got {pc} for RewardId='{GetField(r!, "RewardId")}'");
            }
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
