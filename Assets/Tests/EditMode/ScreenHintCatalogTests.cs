// ─────────────────────────────────────────────────────────────────────────────
// screen_hints — the SHIPPED screen → tip catalog, not a fixture.
//
// Holds ScreenHints.csv to the ScreenId enum, to LoadingTips.csv and to itself
// (unique, contiguous orders) on every EditMode run, so a renamed screen or a
// mistyped key fails here instead of silently hinting nothing on device.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode cannot reference Assembly-CSharp, so the
// types are reached by reflection through the shared `Hints` helper — the same
// arrangement LoadingTipCatalogTests uses via `Tips`.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfinRedux.Tests.EditMode
{
    /// <summary>Reflection seams for the screen-hint types (they live in Assembly-CSharp).</summary>
    internal static class Hints
    {
        public static readonly Type HintT  = Tips.Find("GolfinRedux.UI.ScreenHint");
        public static readonly Type StateT = Tips.Find("GolfinRedux.UI.ScreenHintState");
        public static readonly Type CatT   = Tips.Find("GolfinRedux.UI.ScreenHintCatalog");
        public static readonly Type ResT   = Tips.Find("GolfinRedux.UI.ScreenHintResolver");
        public static readonly Type StoreT = Tips.Find("GolfinRedux.UI.ScreenHintStore");

        public const string CsvPath     = "Assets/Resources/Data/ScreenHints.csv";
        public const string TipsCsvPath = "Assets/Resources/Data/LoadingTips.csv";

        public static string Const(string name)
            => (string)CatT.GetField(name, BindingFlags.Public | BindingFlags.Static)!.GetRawConstantValue()!;

        public static IList Parse(string text)
            => (IList)CatT.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, new object[] { text })!;

        public static IList ShippedHints() => Parse(File.ReadAllText(CsvPath));
        public static IList ShippedTips()  => Tips.Parse(File.ReadAllText(TipsCsvPath));

        // ── ScreenHint ───────────────────────────────────────────────────────

        public static object Hint(string screen, int order, string key)
        {
            object h = Activator.CreateInstance(HintT)!;
            HintT.GetField("screen")!.SetValue(h, screen);
            HintT.GetField("order")!.SetValue(h, order);
            HintT.GetField("key")!.SetValue(h, key);
            return h;
        }

        public static string Screen(object h) => (string)HintT.GetField("screen")!.GetValue(h)!;
        public static int    Order(object h)  => (int)HintT.GetField("order")!.GetValue(h)!;
        public static string Key(object h)    => (string)HintT.GetField("key")!.GetValue(h)!;

        /// <summary>A List&lt;ScreenHint&gt; the resolver accepts as IReadOnlyList.</summary>
        public static object Rows(params object[] hints)
        {
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(HintT))!;
            foreach (object h in hints) list.Add(h);
            return list;
        }

        // ── ScreenHintState ──────────────────────────────────────────────────

        public static object State(string[]? screens = null, string[]? keys = null)
        {
            object s = Activator.CreateInstance(StateT)!;
            StateT.GetField("seenScreens")!.SetValue(s, screens ?? Array.Empty<string>());
            StateT.GetField("seenKeys")!.SetValue(s, keys ?? Array.Empty<string>());
            return s;
        }

        public static string[] Screens(object s) => (string[])StateT.GetField("seenScreens")!.GetValue(s)!;
        public static string[] Keys(object s)    => (string[])StateT.GetField("seenKeys")!.GetValue(s)!;

        // ── Resolver ─────────────────────────────────────────────────────────

        public static string[] HintsFor(string screen, object hints, object tips, object state)
        {
            var m = ResT.GetMethod("HintsFor", BindingFlags.Public | BindingFlags.Static)!;
            var result = (IList)m.Invoke(null, new[] { screen, hints, tips, state })!;
            return result.Cast<object>().Select(Tips.Key).ToArray();
        }
    }

    [TestFixture]
    public class ScreenHintCatalogTests
    {
        static List<object> RowList() => Hints.ShippedHints().Cast<object>().ToList();

        [Test]
        public void ShippedCsv_Has36RowsOver18Screens()
        {
            List<object> rows = RowList();
            Assert.That(rows.Count, Is.EqualTo(36), "§2.2 — 36 screen → hint rows");
            Assert.That(rows.Select(Hints.Screen).Distinct().Count(), Is.EqualTo(18), "§2.2 — 18 screens");
        }

        [Test]
        public void EveryScreen_IsAScreenIdName_OrOneOfTheTwoConstants()
        {
            Type screenId = Tips.Find("GolfinRedux.UI.ScreenId");
            var valid = new HashSet<string>(Enum.GetNames(screenId))
            {
                Hints.Const("GameplayScreen"),
                Hints.Const("SettingsControlsScreen"),
            };

            string[] unknown = RowList().Select(Hints.Screen).Distinct().Where(s => !valid.Contains(s)).ToArray();
            Assert.That(unknown, Is.Empty, "screens that are neither a ScreenId nor a constant: " + string.Join(", ", unknown));

            // The two constants are exactly the two non-ScreenId entry points the spec names.
            Assert.That(Hints.Const("GameplayScreen"), Is.EqualTo("Gameplay"));
            Assert.That(Hints.Const("SettingsControlsScreen"), Is.EqualTo("SettingsControls"));
            Assert.That(Enum.GetNames(screenId), Does.Not.Contain("Gameplay").And.Not.Contain("SettingsControls"),
                "a ScreenId now shadows a catalog constant — pick one");
        }

        [Test]
        public void EveryKey_ExistsInLoadingTips()
        {
            var tipKeys = new HashSet<string>(Hints.ShippedTips().Cast<object>().Select(Tips.Key));
            string[] missing = RowList().Select(Hints.Key).Distinct().Where(k => !tipKeys.Contains(k)).ToArray();
            Assert.That(missing, Is.Empty, "hint keys with no LoadingTips.csv row: " + string.Join(", ", missing));
        }

        [Test]
        public void OrdersAreUniqueAndContiguousFromOne_PerScreen()
        {
            foreach (var group in RowList().GroupBy(Hints.Screen))
            {
                int[] orders = group.Select(Hints.Order).OrderBy(o => o).ToArray();
                int[] expected = Enumerable.Range(1, orders.Length).ToArray();
                CollectionAssert.AreEqual(expected, orders, "orders for " + group.Key + " must be 1..N with no gaps or repeats");
            }
        }

        [Test]
        public void ShippedTable_IsTheOneCesarApproved()
        {
            // §2.2, row for row. A reorder is a design change and should fail here on purpose.
            var expected = new Dictionary<string, string[]>
            {
                ["Home"]                 = new[] { "TIP_RP", "TIP_DAILY" },
                ["Roster"]               = new[] { "TIP_RARITIES", "TIP_STATS", "TIP_CONDITION", "TIP_LEVELUP" },
                ["Inventory"]            = new[] { "TIP_CLUBSTATS", "TIP_BALLS", "TIP_REPAIR" },
                ["ModeSelection"]        = new[] { "TIP_MISSIONS", "TIP_TOURNAMENT", "TIP_VERSUS" },
                ["HoleSelection"]        = new[] { "TIP_AUTOCLUB", "TIP_SURFACES" },
                ["MissionSelection"]     = new[] { "TIP_MISSIONS", "TIP_DAILY" },
                ["TournamentSelection"]  = new[] { "TIP_TOURNAMENT" },
                ["Leaderboard"]          = new[] { "TIP_LEADERBOARD" },
                ["TournamentLeaderboard"]= new[] { "TIP_LEADERBOARD" },
                ["StaminaShopSelection"] = new[] { "TIP_CONDITION" },
                ["GeneralShop"]          = new[] { "TIP_GACHA", "TIP_STORE" },
                ["GachaPrizes"]          = new[] { "TIP_GACHA" },
                ["GpsHub"]               = new[] { "TIP_GPS_WALLET", "TIP_GPS_CHECKIN", "TIP_GPS_SOCIAL" },
                ["GpsRounds"]            = new[] { "TIP_GPS_CHECKIN" },
                ["GpsGift"]              = new[] { "TIP_GPS_SOCIAL" },
                ["GpsVote"]              = new[] { "TIP_GPS_SOCIAL" },
                ["Gameplay"]             = new[] { "TIP_SWING", "TIP_ACCURACY", "TIP_GRADES", "TIP_VIEW", "TIP_CLUB", "TIP_FORECAST" },
                ["SettingsControls"]     = new[] { "TIP_CONTROLS" },
            };

            var actual = RowList().GroupBy(Hints.Screen)
                .ToDictionary(g => g.Key, g => g.OrderBy(Hints.Order).Select(Hints.Key).ToArray());

            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys);
            foreach (var kv in expected)
                CollectionAssert.AreEqual(kv.Value, actual[kv.Key], "hints for " + kv.Key);
        }

        [Test]
        public void CommentsAndHeader_AreSkipped_AndAMalformedRowWarnsOnce()
        {
            const string csv = "# a comment\n" +
                               "screen,order,key\n" +
                               "Home,1,TIP_RP\n" +
                               "\n" +
                               "Roster,x,TIP_STATS\n" +   // non-integer order → dropped
                               "Roster,2\n" +             // 2 columns → dropped
                               "Roster,3,TIP_LEVELUP\n";

            LogAssert.Expect(LogType.Warning, new Regex(@"\[ScreenHintCatalog\] line 5.*not an integer"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ScreenHintCatalog\] line 6.*expected 3 columns"));

            var rows = Hints.Parse(csv).Cast<object>().ToList();
            CollectionAssert.AreEqual(new[] { "TIP_RP", "TIP_LEVELUP" }, rows.Select(Hints.Key).ToArray());
            Assert.That(Hints.Order(rows[1]), Is.EqualTo(3));
        }

        [Test]
        public void IdFor_IsTheEnumName()
        {
            Type screenId = Tips.Find("GolfinRedux.UI.ScreenId");
            object roster = Enum.Parse(screenId, "Roster");
            var m = Hints.CatT.GetMethod("IdFor", BindingFlags.Public | BindingFlags.Static)!;
            Assert.That((string)m.Invoke(null, new[] { roster })!, Is.EqualTo("Roster"));
        }
    }
}
