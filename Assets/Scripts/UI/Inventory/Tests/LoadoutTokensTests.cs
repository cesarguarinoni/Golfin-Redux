// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — LoadoutTokensTests  (publish_blocked_catalogs §6)
//
// THE PARITY HALF. `LoadoutTokens` (C#, Assembly-CSharp) and `loadoutTokens.ts` (the publish
// validator) are the same grammar in two languages, because the dashboard cannot import C# and
// the game cannot import TypeScript. They are allowed to exist twice only because
// Tools/content/tests/loadout_tokens_fixture.csv is run through BOTH: vitest reads it in
// lib/__tests__/loadoutTokens.test.ts, and this fixture reads the same file. A divergence turns
// one of the two red rather than shipping a mission with a bag it cannot fill.
//
// ASSEMBLY NOTE: LoadoutTokens lives in Assembly-CSharp (Assets/Scripts/UI/MissionSelection/, no
// asmdef), which an asmdef test assembly cannot reference — so it is reached by REFLECTION, the
// same idiom and the same reason as ClubRosterProd right next door. Everything else binds to
// PRODUCTION members too: the roster comes from ClubCsvParser via ClubRosterProd, and the shipped
// masks from MissionCsv. Nothing here re-declares a club id, a mask or a parsing rule.
//
// WHY THIS FILE IS HERE AND NOT Assets/Tests/EditMode/: the spec's §6 note says to reuse the CSV
// parse the shipping loader delegates to rather than write a second one. That parse is reached
// through ClubRosterProd, which is `internal` to THIS assembly.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Golfin.Gameplay.Missions;
using NUnit.Framework;

namespace Golfin.Inventory.Tests
{
    [TestFixture]
    public class LoadoutTokensTests
    {
        static readonly Type Tokens = ClubRosterProd.Find("GolfinRedux.UI.MissionSelection.LoadoutTokens");

        /// <summary>LoadoutTokens.Matches(clubId, name, type, token) — the string parity surface.</summary>
        static bool Matches(string clubId, string name, string type, string token)
        {
            var m = Tokens.GetMethod("Matches", BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(string), typeof(string), typeof(string), typeof(string) }, null)
                    ?? throw new InvalidOperationException("LoadoutTokens.Matches(string,string,string,string) not found.");
            return (bool)m.Invoke(null, new object?[] { clubId, name, type, token })!;
        }

        static bool IsKnown(string token)
        {
            var m = Tokens.GetMethod("IsKnown", BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(string) }, null)!;
            return (bool)m.Invoke(null, new object?[] { token })!;
        }

        // ── repo files ────────────────────────────────────────────────────────

        /// <summary>
        /// The repo root, derived from the shipped Clubs.csv ClubRosterProd already locates, so
        /// there is one path-walk in this assembly rather than two.
        /// </summary>
        static string RepoRoot()
        {
            string? clubs = ClubRosterProd.FindShippedCsv();
            Assert.IsNotNull(clubs, "Clubs.csv not found — the repo-root walk failed, so nothing below proves anything.");
            // <repo>/Assets/Resources/Data/Clubs.csv -> <repo>
            return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(clubs!)!, "..", "..", ".."));
        }

        internal const string FixtureRelativePath = "Tools/content/tests/loadout_tokens_fixture.csv";

        static List<Dictionary<string, string>> Fixture()
        {
            string path = Path.Combine(RepoRoot(), FixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"The shared parity fixture is missing: {path}");
            var rows = MissionCsv.Parse(File.ReadAllText(path));
            Assert.GreaterOrEqual(rows.Count, 13,
                "The fixture went short — a silently empty fixture makes every case below vacuous.");
            return rows;
        }

        static List<Dictionary<string, string>> Loadouts()
        {
            string path = Path.Combine(RepoRoot(),
                "Assets/Resources/Data/mission_loadouts.csv".Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), $"mission_loadouts.csv is missing: {path}");
            return MissionCsv.Parse(File.ReadAllText(path));
        }

        static IList Roster()
        {
            var rows = ClubRosterProd.Parse(ClubRosterProd.ReadShippedCsv());
            Assert.AreEqual(799, rows.Count, "The shipped club roster changed size — re-read the counts below.");
            return rows;
        }

        static IEnumerable<string> MaskTokens(string csv)
        {
            foreach (string part in (csv ?? "").Split(','))
            {
                string t = part.Trim();
                if (t.Length > 0 && t != "*") yield return t;
            }
        }

        // ── the shared fixture, row by row ────────────────────────────────────

        [Test]
        public void EveryFixtureRowMatchesWhatTheFixtureSays()
        {
            var failures = new List<string>();
            foreach (var r in Fixture())
            {
                bool expected = r["expected"].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                bool actual = Matches(r["clubId"], r["name"], r["type"], r["token"]);
                if (actual != expected)
                    failures.Add($"{r["clubId"]} ({r["name"]} / {r["type"]}) vs \"{r["token"]}\": expected {expected}, got {actual}");
            }
            Assert.IsEmpty(failures, "C# and the fixture disagree — so C# and the TypeScript validator disagree:\n"
                + string.Join("\n", failures));
        }

        [Test]
        public void TheLoftParseIsAnchoredRatherThanADigitHunt()
        {
            // The predecessor asked whether id+name "contains 7". `Iron 5 X7` was a 7-iron under it.
            Assert.IsFalse(Matches("club_iron_x", "Iron 5 X7", "Iron", "Iron7"));
            Assert.IsTrue(Matches("club_iron_x", "Iron 5 X7", "Iron", "Iron5"));
            // A loft-less iron answers only to the family token.
            Assert.IsFalse(Matches("club_iron_z", "GOLFIN Iron", "Iron", "Iron7"));
            Assert.IsTrue(Matches("club_iron_z", "GOLFIN Iron", "Iron", "Iron"));
        }

        [Test]
        public void IsKnownAcceptsTheGrammarAndNothingElse()
        {
            foreach (string t in new[] { "Driver", "wood", "Iron", "AW", "pw", "SW", "Putter", "Iron4", "Iron9" })
                Assert.IsTrue(IsKnown(t), $"\"{t}\" should be a known token.");
            foreach (string t in new[] { "A.Wedge", "Iron10", "Irons", "", "Hybrid" })
                Assert.IsFalse(IsKnown(t), $"\"{t}\" should NOT be a known token.");
        }

        // ── the shipped roster and the shipped masks ──────────────────────────

        [Test]
        public void EveryShippedSuppliedMaskResolvesEveryTokenAtItsRarity()
        {
            var roster = Roster();
            var failures = new List<string>();

            foreach (var loadout in Loadouts())
            {
                if (!loadout["kind"].Trim().Equals("supplied", StringComparison.OrdinalIgnoreCase)) continue;
                string rarity = loadout["rarity"].Trim();

                foreach (string token in MaskTokens(loadout["clubs"]))
                {
                    bool hit = false;
                    foreach (var club in roster)
                    {
                        if (!Matches(ClubRosterProd.Field<string>(club!, "id"),
                                     ClubRosterProd.Field<string>(club!, "name"),
                                     ClubRosterProd.EnumName(club!, "type"), token)) continue;
                        if (!string.Equals(ClubRosterProd.EnumName(club!, "rarity"), rarity,
                                           StringComparison.OrdinalIgnoreCase)) continue;
                        hit = true;
                        break;
                    }
                    if (!hit) failures.Add($"{loadout["id"]}: no {rarity} club answers to \"{token}\"");
                }
            }

            Assert.IsEmpty(failures, "A shipped supplied mask hands out a bag with a hole in it:\n"
                + string.Join("\n", failures));
        }

        [Test]
        public void BanIronDropsEveryIronAndOnlyIrons()
        {
            var roster = Roster();
            int irons = 0, banned = 0, wrongFamily = 0;

            foreach (var club in roster)
            {
                bool isIron = ClubRosterProd.EnumName(club!, "type") == "Iron";
                bool hit = Matches(ClubRosterProd.Field<string>(club!, "id"),
                                   ClubRosterProd.Field<string>(club!, "name"),
                                   ClubRosterProd.EnumName(club!, "type"), "Iron");
                if (isIron) irons++;
                if (hit) banned++;
                if (hit != isIron) wrongFamily++;
            }

            Assert.AreEqual(114, irons, "The roster's iron count moved.");
            Assert.AreEqual(114, banned, "`ban:Iron` must reach every iron — that is the whole fix.");
            Assert.AreEqual(0, wrongFamily, "`ban:Iron` reached, or missed, a club it should not have.");
        }

        [Test]
        public void BanIron7AndIron9ReachOnlyEighteenOfThem()
        {
            // The BUG, pinned. `ban:Iron7,Iron9` — what OWN_NO_IRONS said until this task — let
            // 96 of the 114 shipped irons (Iron 4/5/6/8) play on mission 24, "No Irons Allowed".
            var roster = Roster();
            int reached = 0;
            foreach (var club in roster)
            {
                string id = ClubRosterProd.Field<string>(club!, "id");
                string name = ClubRosterProd.Field<string>(club!, "name");
                string type = ClubRosterProd.EnumName(club!, "type");
                if (Matches(id, name, type, "Iron7") || Matches(id, name, type, "Iron9")) reached++;
            }
            Assert.AreEqual(18, reached);
        }

        [Test]
        public void OwnNoIronsShipsTheFamilyToken()
        {
            var row = Loadouts().Find(r => r["id"] == "OWN_NO_IRONS");
            Assert.IsNotNull(row, "OWN_NO_IRONS is gone from mission_loadouts.csv.");
            Assert.AreEqual("ban:Iron", row!["clubs"].Trim());
        }
    }
}
