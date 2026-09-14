// ─────────────────────────────────────────────────────────────────────────────
// ScreenIdSerializationTests — finished_tournament_leaderboard_route (2026-09-14)
//
// ScreenId is stored BY VALUE in prefabs and in ShellScene: every
// `[SerializeField] ScreenId` field on a screen controller is an integer in YAML.
// On 2026-08-29 `MissionSelection` was slotted into the enum at index 8 without a
// value; every stored id from 8 upward then named the screen one slot over, and a
// finished tournament's LEADERBOARD button opened the hole selection for two weeks.
// (`Docs/Specs/Quick/finished_tournament_leaderboard_route.md`.)
//
// Two gates:
//   1. the enum's values are pinned — an insert, a reorder or a value change fails here;
//   2. every serialized ScreenId field in the project names the screen its author
//      chose — the audit table of that task, made permanent. A new serialized
//      ScreenId field belongs in `Sites`.
//
// Reflection, as NavBackMemoryTests: this assembly cannot reference Assembly-CSharp.
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class ScreenIdSerializationTests
    {
        private static readonly Type ScreenIdType =
            Type.GetType("GolfinRedux.UI.ScreenId, Assembly-CSharp");

        // ── Gate 1 — the pinned values ───────────────────────────────────────

        /// <summary>
        /// Name → value, as stored on disk. Append a row for a new member (next free number);
        /// never edit or remove one — a retired member keeps its number so nothing stored
        /// can silently become another screen.
        /// </summary>
        private static readonly (string name, int value)[] Pinned =
        {
            ("Logo", 0), ("Splash", 1), ("Loading", 2), ("Home", 3), ("Roster", 4),
            ("Inventory", 5), ("HoleSelection", 6), ("ModeSelection", 7),
            ("MissionSelection", 8), ("Leaderboard", 9), ("TournamentHoleSelection", 10),
            ("TournamentLeaderboard", 11), ("TournamentSelection", 12),
            ("StaminaShopSelection", 13), ("StaminaShopDetail", 14), ("GeneralShop", 15),
            ("GachaHistory", 16), ("GachaPrizes", 17), ("GpsHub", 18), ("ScoreUpload", 19),
            ("GpsProfile", 20), ("GpsAvatar", 21), ("GpsBadges", 22), ("GpsGolfProfile", 23),
            ("GpsWelcome", 24), ("GpsGift", 25), ("GpsVote", 26), ("GpsRounds", 27),
            ("Login", 28), ("CreateUsername", 29), ("SignUp", 30), ("EmailConfirmation", 31),
            ("ResetPassword", 32), ("StartingCharacterSelection", 33), ("StoreHistory", 34),
        };

        [Test]
        public void EnumTypeIsFound()
        {
            Assert.NotNull(ScreenIdType, "GolfinRedux.UI.ScreenId not found in Assembly-CSharp");
        }

        [Test]
        public void EveryPinnedMemberKeepsItsValue()
        {
            var names = new HashSet<string>(Enum.GetNames(ScreenIdType));
            var wrong = new List<string>();
            foreach (var (name, value) in Pinned)
            {
                if (!names.Contains(name))
                {
                    wrong.Add($"{name} is gone — a retired member keeps its number (rename it, never delete it)");
                    continue;
                }
                int actual = Convert.ToInt32(Enum.Parse(ScreenIdType, name));
                if (actual != value)
                    wrong.Add($"{name} = {actual}, pinned {value}");
            }
            Assert.IsEmpty(wrong,
                "ScreenId values moved — every prefab/scene field storing one of these now names " +
                "another screen. Restore the value; give a NEW member the next free number.\n  " +
                string.Join("\n  ", wrong));
        }

        [Test]
        public void ValuesAreUnique()
        {
            var values = Enum.GetValues(ScreenIdType).Cast<object>().Select(Convert.ToInt32).ToList();
            var dupes = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.IsEmpty(dupes, "Two ScreenId members share a value: " + string.Join(", ", dupes));
        }

        [Test]
        public void NewMembersTakeTheNextFreeNumber()
        {
            // Anything not pinned is new: it must sit above the pinned range, never in a gap
            // between two pinned values, or a stored field could resolve to it.
            int maxPinned = Pinned.Max(p => p.value);
            var pinnedNames = new HashSet<string>(Pinned.Select(p => p.name));
            var low = Enum.GetNames(ScreenIdType)
                .Where(n => !pinnedNames.Contains(n))
                .Select(n => (n, v: Convert.ToInt32(Enum.Parse(ScreenIdType, n))))
                .Where(t => t.v <= maxPinned)
                .Select(t => $"{t.n} = {t.v}")
                .ToList();
            Assert.IsEmpty(low,
                $"A new ScreenId member must take a value above {maxPinned} (and a row in Pinned): " +
                string.Join(", ", low));
        }

        // ── Gate 2 — every serialized ScreenId field on disk ────────────────

        public readonly struct Site
        {
            public readonly string Asset;     // project-relative YAML file
            public readonly string Script;    // project-relative .cs whose .meta GUID owns the field
            public readonly string Field;     // serialized field name
            public readonly string Expected;  // ScreenId member name the author chose

            public Site(string asset, string script, string field, string expected)
            {
                Asset = asset; Script = script; Field = field; Expected = expected;
            }

            public override string ToString() => $"{Path.GetFileName(Asset)} {Path.GetFileNameWithoutExtension(Script)}.{Field} → {Expected}";
        }

        private const string Tourn = "Assets/Scripts/UI/Tournaments/";
        private const string Shop  = "Assets/Scripts/UI/Shop/";

        /// <summary>
        /// Every `[SerializeField] ScreenId` in the project and the screen its author chose.
        /// The six that were off by one on 2026-09-14 are the tournament and stamina-shop rows;
        /// the CLOSE fallback of the hole selection had pointed at ModeSelection since the day
        /// before TournamentSelection existed.
        /// </summary>
        private static readonly Site[] Sites =
        {
            new Site("Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab",
                     Tourn + "TournamentSelectionScreenController.cs", "_leaderboardTarget",   "TournamentLeaderboard"),
            new Site("Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab",
                     Tourn + "TournamentSelectionScreenController.cs", "_holeSelectionTarget", "TournamentHoleSelection"),
            new Site("Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab",
                     Tourn + "TournamentSignupModalController.cs",     "_holeSelectionTarget", "TournamentHoleSelection"),
            new Site("Assets/Scenes/ShellScene.unity",
                     Tourn + "TournamentLeaderboardScreenController.cs", "_backScreen",        "TournamentHoleSelection"),
            new Site("Assets/Scenes/ShellScene.unity",
                     Tourn + "TournamentHoleSelectionScreenController.cs", "_backScreen",      "TournamentSelection"),
            new Site("Assets/Scenes/ShellScene.unity",
                     Tourn + "TournamentDevEntryButton.cs",             "_target",             "TournamentSelection"),
            new Site("Assets/Prefabs/UI/Shop/StaminaShopDetailScreen.prefab",
                     Shop + "StaminaShopDetailScreenController.cs",     "_backTarget",         "StaminaShopSelection"),
            new Site("Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab",
                     Shop + "StaminaShopSelectionScreenController.cs",  "_returnTarget",       "Roster"),
            new Site("Assets/Scenes/ShellScene.unity",
                     "Assets/Scripts/UI/ScreenManager.cs",              "_initialScreen",      "Logo"),
        };

        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);

        [Test, TestCaseSource(nameof(Sites))]
        public void SerializedFieldNamesTheAuthoredScreen(Site site)
        {
            int expected = Convert.ToInt32(Enum.Parse(ScreenIdType, site.Expected));
            var stored = ReadSerializedInts(site.Asset, site.Script, site.Field);

            Assert.IsNotEmpty(stored,
                $"{site}: no MonoBehaviour block of that script carries `{site.Field}` in {site.Asset} — " +
                "field renamed, script moved, or the object left the file; update Sites.");
            foreach (int value in stored)
            {
                string actualName = Enum.IsDefined(ScreenIdType, value) ? Enum.GetName(ScreenIdType, value) : "<undefined>";
                Assert.AreEqual(expected, value,
                    $"{site}: stored {value} ({actualName}), expected {expected} ({site.Expected}). " +
                    "Re-serialize the field through the Unity API (SerializedObject), not by hand.");
            }
        }

        [Test]
        public void EverySerializedScreenIdFieldInTheProjectIsListed()
        {
            // The complement of Sites: any `[SerializeField] ScreenId` declared anywhere under
            // Assets/Scripts that no Site row covers. Keeps the table honest when a screen grows
            // a new target field.
            var declared = new List<(string script, string field)>();
            var rx = new Regex(@"\[SerializeField\]\s*(?:private|protected|internal|public)?\s*ScreenId\s+(_?\w+)");
            foreach (string root in new[] { "Assets/Scripts", "Assets/Editor" })
            {
                string dir = Path.Combine(ProjectRoot, root);
                if (!Directory.Exists(dir)) continue;
                foreach (string cs in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (Match m in rx.Matches(File.ReadAllText(cs)))
                        declared.Add((cs.Substring(ProjectRoot.Length + 1).Replace('\\', '/'), m.Groups[1].Value));
                }
            }
            Assert.IsNotEmpty(declared, "No serialized ScreenId field found under Assets/Scripts — the regex is stale.");

            var covered = new HashSet<string>(Sites.Select(s => s.Script + "#" + s.Field));
            var missing = declared.Where(d => !covered.Contains(d.script + "#" + d.field))
                                  .Select(d => $"{d.script} {d.field}").ToList();
            Assert.IsEmpty(missing,
                "Serialized ScreenId fields with no Site row (add one with the authored target):\n  " +
                string.Join("\n  ", missing));
        }

        // ── YAML ─────────────────────────────────────────────────────────────

        /// <summary>
        /// All `<field>: N` values inside MonoBehaviour blocks whose m_Script GUID is the
        /// script's .meta GUID. Unity YAML: documents start with `--- !u!`; a MonoBehaviour is
        /// `!u!114`; the script reference line carries the GUID; fields are `  name: value`.
        /// </summary>
        private static List<int> ReadSerializedInts(string asset, string script, string field)
        {
            string guid = ScriptGuid(script);
            string[] lines = File.ReadAllLines(Path.Combine(ProjectRoot, asset));
            var scriptRx = new Regex(@"m_Script: \{fileID: \d+, guid: ([0-9a-f]{32})");
            var fieldRx = new Regex(@"^\s+" + Regex.Escape(field) + @": (-?\d+)\s*$");

            var result = new List<int>();
            bool inBlock = false;     // inside a MonoBehaviour document of the wanted script
            foreach (string line in lines)
            {
                if (line.StartsWith("--- !u!", StringComparison.Ordinal)) { inBlock = false; continue; }
                var sm = scriptRx.Match(line);
                if (sm.Success) { inBlock = sm.Groups[1].Value == guid; continue; }   // m_Script precedes the fields
                if (!inBlock) continue;
                var fm = fieldRx.Match(line);
                if (fm.Success) result.Add(int.Parse(fm.Groups[1].Value));
            }
            return result;
        }

        private static string ScriptGuid(string script)
        {
            string meta = Path.Combine(ProjectRoot, script + ".meta");
            Assert.IsTrue(File.Exists(meta), $"{meta} not found — script moved? update Sites.");
            var m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
            Assert.IsTrue(m.Success, $"no guid in {meta}");
            return m.Groups[1].Value;
        }
    }
}
