// ─────────────────────────────────────────────────────────────────────────────
// TournamentRestrictionsClientTests — the client half of tournament_restrictions
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests (named EditMode test asmdef).
// The production types under test (TournamentScheduleMapper, TournamentRulesText,
// TournamentEnterResponseDto, TournamentSignupModalController) live in
// Assembly-CSharp, which an asmdef cannot reference, so they are reached by
// REFLECTION — the same access pattern RemoteScheduleTests and
// TournamentServiceWireupTests already use. The AsmCSharp / AsyncProd helpers in
// this namespace are reused rather than re-declared.
//
// COVERAGE (SPEC § Acceptance)
//   §1  Mapper           — all 10 fields carried; absent fields → unrestricted;
//                          unknown enum strings degrade; CSV rows unrestricted
//   §2  Rank ladder      — pinned to Golfin.Roster.CharacterRarity's declaration
//                          order (the modal converts with (int)rarity + 1)
//   §3  RULES block      — restricted renders real values in EN and JA; an
//                          unrestricted tournament renders TODAY's five strings
//   §4  Denial copy      — each failure names its own rule; server `reason`
//                          strings map onto the same vocabulary
//   §5  Server denials   — `full` / `ineligible` are 200-shaped answers
//   §6  Widget click     — CONFIRM on an ineligible tournament registers nothing
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Tournaments;
using NUnit.Framework;
using UnityEngine;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Golfin.Tournaments.WireupTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // §0  Shared payload + fixture helpers
    // ═════════════════════════════════════════════════════════════════════════

    internal static class RestrictionPayloads
    {
        /// <summary>One fully-restricted tournament plus one with every restriction absent.</summary>
        internal const string Schedule = @"{""data"": {
  ""fetched_at"": ""2026-08-19T00:00:00+00:00"",
  ""tournaments"": [
    {
      ""slug"": ""restricted_cup"", ""title"": ""Restricted Cup"", ""course_id"": ""club_lomond"",
      ""hole_set"": ""1-3"", ""start_at"": ""2026-08-01T00:00:00+00:00"",
      ""end_at"": ""2026-08-08T00:00:00+00:00"", ""resolve_delay_minutes"": 30,
      ""entry_fee_pts"": 0, ""bot_field_id"": ""bf_test"",
      ""category"": ""competitive"", ""max_players"": 64, ""players_per_division"": 32,
      ""division_type"": ""rarity_band"",
      ""char_rarity_min"": ""Rare"", ""char_rarity_max"": ""Legendary"",
      ""char_level_min"": 80, ""char_level_max"": 160,
      ""gear_rule"": ""own"", ""club_rarity_max"": ""Mythic"",
      ""prize_bands"": [{""rank_from"": 1, ""rank_to"": 1, ""rp_reward"": 100, ""item_reward_id"": null}]
    },
    {
      ""slug"": ""open_cup"", ""title"": ""Open Cup"", ""course_id"": ""club_lomond"",
      ""hole_set"": ""1-3"", ""start_at"": ""2026-08-01T00:00:00+00:00"",
      ""end_at"": ""2026-08-08T00:00:00+00:00"", ""resolve_delay_minutes"": 30,
      ""entry_fee_pts"": 0, ""bot_field_id"": ""bf_test"",
      ""prize_bands"": [{""rank_from"": 1, ""rank_to"": 1, ""rp_reward"": 100, ""item_reward_id"": null}]
    }
  ]
}}";

        /// <summary>A newer dashboard authoring vocabulary this build has never met.</summary>
        internal const string ScheduleUnknownEnums = @"{""data"": {
  ""fetched_at"": ""2026-08-19T00:00:00+00:00"",
  ""tournaments"": [
    {
      ""slug"": ""future_cup"", ""course_id"": ""club_lomond"", ""hole_set"": ""1-3"",
      ""start_at"": ""2026-08-01T00:00:00+00:00"", ""end_at"": ""2026-08-08T00:00:00+00:00"",
      ""resolve_delay_minutes"": 30, ""entry_fee_pts"": 0, ""bot_field_id"": ""bf_test"",
      ""category"": ""seasonal_v2"", ""division_type"": ""handicap"", ""gear_rule"": ""rental"",
      ""char_rarity_min"": ""Ultra"", ""club_rarity_max"": ""Godlike"",
      ""prize_bands"": [{""rank_from"": 1, ""rank_to"": 1, ""rp_reward"": 100, ""item_reward_id"": null}]
    }
  ]
}}";

        internal const string EnterFull =
            @"{""data"": {""entered"": false, ""status"": ""full"", ""max_players"": 100}}";

        internal const string EnterIneligibleRarity =
            @"{""data"": {""entered"": false, ""status"": ""ineligible"", ""reason"": ""char_rarity""}}";

        internal const string EnterIneligibleLevel =
            @"{""data"": {""entered"": false, ""status"": ""ineligible"", ""reason"": ""char_level""}}";
    }

    /// <summary>Reflection handles onto the Assembly-CSharp production types this file exercises.</summary>
    internal static class RestrictionProd
    {
        internal static readonly Type Mapper    = AsmCSharp.GetType("Golfin.Tournaments.TournamentScheduleMapper");
        internal static readonly Type RulesText = AsmCSharp.GetType("Golfin.Tournaments.TournamentRulesText");

        internal static IReadOnlyDictionary<string, BotFieldConfig> BotFields => new Dictionary<string, BotFieldConfig>
        {
            ["bf_test"] = new BotFieldConfig("bf_test", 0, new Dictionary<string, float>(), 0f, 0f, 0f),
        };

        /// <summary>
        /// TryMapJson → the mapped definitions. TournamentSchedule itself lives in Assembly-CSharp
        /// and cannot be NAMED here, but its Definitions property is
        /// <c>IReadOnlyList&lt;TournamentDefinition&gt;</c> — a Golfin.Tournaments type — so the
        /// assertions need no further reflection.
        /// </summary>
        internal static IReadOnlyList<TournamentDefinition> MapOrFail(string json)
        {
            var m = Mapper.GetMethod("TryMapJson", BindingFlags.Public | BindingFlags.Static)!;
            object? schedule = m.Invoke(null, new object?[] { json, BotFields, "test" });
            Assert.IsNotNull(schedule, "The payload must map — a mapping failure hides the field assertions.");

            return (IReadOnlyList<TournamentDefinition>)
                schedule!.GetType().GetProperty("Definitions")!.GetValue(schedule)!;
        }

        internal static TournamentDefinition Find(IReadOnlyList<TournamentDefinition> defs, string id)
        {
            foreach (var d in defs) if (d.Id == id) return d;
            throw new AssertionException($"'{id}' is not in the mapped schedule.");
        }

        /// <summary>The expected coloured-letter markup for a rarity, derived from the SAME
        /// RarityHelper the badges use — so this asserts the RULES block reuses the project's
        /// rarity presentation rather than re-deriving a second palette.</summary>
        internal static string Tag(string canonicalRarity)
            => Call("RarityTag", canonicalRarity);

        internal static string Call(string method, params object?[] args)
        {
            var m = RulesText.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                    ?? throw new AssertionException($"TournamentRulesText.{method} not found.");
            return (string)m.Invoke(null, args)!;
        }
    }

    /// <summary>
    /// Installs a real localization table for the duration of a fixture, so the RULES assertions
    /// read the SHIPPED copy rather than a bare key. The previous language is restored in TearDown
    /// — a leaked Japanese language would silently change every later fixture in the run.
    /// </summary>
    internal sealed class LocalizationScope : IDisposable
    {
        private readonly Language _saved;

        public LocalizationScope(Language language)
        {
            _saved = LocalizationManager.CurrentLanguage;

            var table = ScriptableObject.CreateInstance<LocalizationTextTable>();
#if UNITY_EDITOR
            var csv = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Localization/LocalizationText.csv");
            Assert.IsNotNull(csv, "Assets/Localization/LocalizationText.csv must exist.");
            foreach (var line in csv!.text.Split('\n'))
            {
                var cols = line.Split(',');
                if (cols.Length < 3 || cols[0] == "key") continue;
                table.rows.Add(new LocalizedTextRow
                {
                    key = cols[0].Trim(), english = cols[1], japanese = cols[2].TrimEnd('\r'),
                });
            }
#endif
            LocalizationManager.Initialize(table, language);
        }

        public void Dispose() => LocalizationManager.SetLanguage(_saved);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §1  Mapper — the 10 fields survive the wire
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class RestrictionMapperTests
    {
        [Test]
        public void All_ten_restriction_fields_are_carried_through_to_the_definition()
        {
            var def = RestrictionProd.Find(RestrictionProd.MapOrFail(RestrictionPayloads.Schedule),
                                           "restricted_cup");

            Assert.AreEqual(TournamentCategory.Competitive,    def.Category);
            Assert.AreEqual(64,                                def.MaxPlayers);
            Assert.AreEqual(32,                                def.PlayersPerDivision);
            Assert.AreEqual(TournamentDivisionType.RarityBand, def.DivisionType);
            Assert.AreEqual("Rare",                            def.CharRarityMin);
            Assert.AreEqual("Legendary",                       def.CharRarityMax);
            Assert.AreEqual(80,                                def.CharLevelMin);
            Assert.AreEqual(160,                               def.CharLevelMax);
            Assert.AreEqual(TournamentGearRule.Own,            def.GearRule);
            Assert.AreEqual("Mythic",                          def.ClubRarityMax);
            Assert.IsTrue(def.HasEntryRestrictions);
        }

        [Test]
        public void A_tournament_with_no_restriction_fields_maps_to_an_unrestricted_definition()
        {
            var def = RestrictionProd.Find(RestrictionProd.MapOrFail(RestrictionPayloads.Schedule),
                                           "open_cup");

            Assert.IsNull(def.Category);
            Assert.IsNull(def.MaxPlayers,
                "An ABSENT max_players must not deserialise as 0 — a cap of zero admits nobody.");
            Assert.IsNull(def.PlayersPerDivision);
            Assert.IsNull(def.DivisionType);
            Assert.IsNull(def.CharRarityMin);
            Assert.IsNull(def.CharLevelMax);
            Assert.IsNull(def.GearRule);
            Assert.IsNull(def.ClubRarityMax);
            Assert.IsFalse(def.HasEntryRestrictions);
        }

        [Test]
        public void An_unknown_vocabulary_from_a_newer_server_degrades_rather_than_dropping_the_row()
        {
            var def = RestrictionProd.Find(
                RestrictionProd.MapOrFail(RestrictionPayloads.ScheduleUnknownEnums), "future_cup");

            Assert.IsNull(def.Category);
            Assert.IsNull(def.DivisionType);
            Assert.IsNull(def.GearRule);
            Assert.IsNull(def.CharRarityMin);
            Assert.IsNull(def.ClubRarityMax);
            Assert.IsFalse(def.HasEntryRestrictions,
                "Nothing in the restriction block may make a tournament undisplayable — the whole " +
                "schedule must not go down over a string this build has not met.");
        }

        [Test]
        public void The_shipped_csv_composes_unrestricted_definitions()
        {
            // The offline path gains no columns: every CSV row must behave exactly as it did
            // before this feature existed.
            var loader = new TournamentCsvLoader();
            var defs   = loader.LoadTournaments();

            Assert.Greater(defs.Count, 0, "tournaments.csv must load.");
            foreach (var d in defs)
                Assert.IsFalse(d.HasEntryRestrictions,
                    $"CSV row '{d.Id}' must be unrestricted — the bundled CSV has no restriction columns.");
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §2  The rank ladder is pinned to CharacterRarity
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class RarityLadderPinTests
    {
        [Test]
        public void The_rarity_ladder_matches_CharacterRaritys_declaration_order()
        {
            // TournamentSignupModalController converts with `(int)template.rarity + 1`, and the
            // server ranks Common=1 … Supreme=6. Reordering Golfin.Roster.CharacterRarity — or
            // inserting a rarity in the middle of it — would silently shift every band by one, so
            // the three ladders are pinned together HERE rather than trusted to stay in step.
            Type rarity = AsmCSharp.GetType("Golfin.Roster.CharacterRarity");
            var  names  = Enum.GetNames(rarity);

            Assert.AreEqual(TournamentRestrictions.RarityLadder.Length, names.Length,
                "CharacterRarity and TournamentRestrictions.RarityLadder must have the same length.");

            for (int i = 0; i < names.Length; i++)
            {
                Assert.AreEqual(TournamentRestrictions.RarityLadder[i], names[i],
                    $"Ladder position {i} disagrees.");
                Assert.AreEqual(i + 1, TournamentRestrictions.RarityRank(names[i]),
                    $"(int){names[i]} + 1 must be its server rank.");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §3  RULES block
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class RulesBlockTests
    {
        private static TournamentDefinition Restricted()
            => RestrictionProd.Find(RestrictionProd.MapOrFail(RestrictionPayloads.Schedule), "restricted_cup");

        private static string Tag(string rarity) => RestrictionProd.Tag(rarity);

        private static TournamentDefinition Unrestricted()
            => RestrictionProd.Find(RestrictionProd.MapOrFail(RestrictionPayloads.Schedule), "open_cup");

        [Test]
        public void An_unrestricted_tournament_renders_exactly_the_five_original_strings()
        {
            using (new LocalizationScope(Language.English))
            {
                string body = RestrictionProd.Call("Body", Unrestricted());

                Assert.AreEqual(string.Join("\n", new[]
                {
                    LocalizationManager.Get("tourn.rules.max_players"),
                    LocalizationManager.Get("tourn.rules.divisions"),
                    LocalizationManager.Get("tourn.rules.per_division"),
                    LocalizationManager.Get("tourn.rules.gear"),
                    LocalizationManager.Get("tourn.rules.characters"),
                }), body,
                "A tournament with no authored restriction must render byte-for-byte what it " +
                "rendered before this feature existed.");
            }
        }

        [Test]
        public void A_restricted_tournament_renders_its_real_values_in_english()
        {
            using (new LocalizationScope(Language.English))
            {
                var def  = Restricted();
                string body = RestrictionProd.Call("Body", def);

                StringAssert.Contains("64",  body, "the authored max_players");
                StringAssert.Contains("32",  body, "the authored players_per_division");
                StringAssert.Contains("Rarity band", body);
                StringAssert.Contains("Own clubs",   body);
                StringAssert.Contains("80",  body);
                StringAssert.Contains("160", body);

                // Rarities render as their coloured LETTER (Cesar, 2026-08-19), never spelled out.
                StringAssert.Contains(Tag("Mythic"),    body, "the club-rarity ceiling");
                StringAssert.Contains(Tag("Rare"),      body);
                StringAssert.Contains(Tag("Legendary"), body);
                Assert.IsFalse(body.Contains("LEGENDARY"),
                    "The spelled-out rarity name must not survive anywhere in the block.");

                Assert.IsFalse(body.Contains("Unlimited"),
                    "An authored cap must replace the Unlimited fallback, not sit beside it.");
                Assert.IsFalse(body.Contains("tourn.rules."),
                    "Every key used by the block must exist in LocalizationText.csv.");
            }
        }

        [Test]
        public void A_restricted_tournament_renders_japanese_when_the_player_is_japanese()
        {
            using (new LocalizationScope(Language.Japanese))
            {
                string body = RestrictionProd.Call("Body", Restricted());

                StringAssert.Contains("最大参加人数：64", body);
                StringAssert.Contains("ディビジョン：レアリティ別", body);
                StringAssert.Contains("自分のクラブ", body);
                StringAssert.Contains(Tag("Rare"), body,
                    "The rarity letter is language-neutral and identical in both locales.");
                Assert.IsFalse(body.Contains("tourn.rules."),
                    "Every key must have a Japanese column too.");
                Assert.IsFalse(body.Contains("MAX PLAYERS"),
                    "A Japanese player must not fall through to the English literal.");
            }
        }

        [Test]
        public void Every_rarity_renders_as_its_own_coloured_single_letter()
        {
            // Cesar, 2026-08-19: rarity shows as the FIRST LETTER, in the rarity colour.
            // The letters are the product requirement, so they are spelled out here rather than
            // re-derived from the helper under test.
            var expected = new Dictionary<string, string>
            {
                ["Common"] = "C", ["Uncommon"] = "U", ["Rare"]      = "R",
                ["Mythic"] = "M", ["Legendary"] = "L", ["Supreme"]  = "S",
            };

            var seenColours = new HashSet<string>();
            foreach (var kv in expected)
            {
                string tag = RestrictionProd.Tag(kv.Key);

                StringAssert.IsMatch(@"^<color=#[0-9A-F]{6}>.</color>$", tag,
                    $"'{kv.Key}' must render as exactly one coloured character, got '{tag}'.");
                StringAssert.Contains(">" + kv.Value + "<", tag,
                    $"'{kv.Key}' must render as '{kv.Value}'.");
                Assert.IsFalse(tag.ToUpperInvariant().Contains(kv.Key.ToUpperInvariant()),
                    "The spelled-out name must not appear.");

                seenColours.Add(tag.Substring(tag.IndexOf('#'), 7));
            }

            Assert.AreEqual(expected.Count, seenColours.Count,
                "Each rarity must carry its OWN colour — two rarities sharing one would make the " +
                "letter the only distinction and defeat the point of colouring it.");
        }

        [Test]
        public void The_gear_line_reads_supplied_only_when_the_server_says_supplied()
        {
            using (new LocalizationScope(Language.English))
            {
                // SPEC §2's one intended copy change: the old `tourn.rules.gear` says "Supplied by
                // GOLFIN", which was display fiction. It survives ONLY as the null fallback.
                StringAssert.Contains("Supplied", RestrictionProd.Call("GearLine", Unrestricted()),
                    "Absent gear_rule keeps the pre-existing string.");
                StringAssert.Contains("Own clubs", RestrictionProd.Call("GearLine", Restricted()),
                    "gear_rule='own' renders the truth instead.");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4  Refusal copy
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class DenialCopyTests
    {
        private static TournamentDefinition Restricted()
            => RestrictionProd.Find(RestrictionProd.MapOrFail(RestrictionPayloads.Schedule), "restricted_cup");

        private static string Tag(string rarity) => RestrictionProd.Tag(rarity);

        [Test]
        public void Each_failure_names_its_own_rule()
        {
            using (new LocalizationScope(Language.English))
            {
                var def = Restricted();

                string rarity = RestrictionProd.Call("DenialMessage", TournamentEligibilityFailure.CharacterRarity, def);
                string level  = RestrictionProd.Call("DenialMessage", TournamentEligibilityFailure.CharacterLevel,  def);
                string club   = RestrictionProd.Call("DenialMessage", TournamentEligibilityFailure.ClubRarity,      def);

                StringAssert.Contains(Tag("Rare"),      rarity, "the band the player missed");
                StringAssert.Contains(Tag("Legendary"), rarity);
                StringAssert.Contains("80",             level);
                StringAssert.Contains("160",            level);
                StringAssert.Contains(Tag("Mythic"),    club);

                Assert.AreNotEqual(rarity, level,
                    "A refusal that does not distinguish the rules leaves the player guessing.");
                Assert.IsFalse(rarity.Contains("tourn.entry."), "Every denial key must exist.");
                Assert.IsFalse(club.Contains("tourn.entry."));
            }
        }

        [Test]
        public void Server_reason_strings_map_onto_the_same_vocabulary()
        {
            var parse = RestrictionProd.RulesText.GetMethod("ParseServerReason",
                BindingFlags.Public | BindingFlags.Static)!;

            Assert.AreEqual(TournamentEligibilityFailure.CharacterRarity,
                parse.Invoke(null, new object?[] { "char_rarity" }));
            Assert.AreEqual(TournamentEligibilityFailure.CharacterLevel,
                parse.Invoke(null, new object?[] { "char_level" }));
            Assert.AreEqual(TournamentEligibilityFailure.None,
                parse.Invoke(null, new object?[] { "char_handicap" }),
                "A reason from a newer server falls back to the generic refusal rather than " +
                "mis-naming a rule the player did not break.");
        }

        [Test]
        public void The_full_message_uses_the_cap_the_server_enforced()
        {
            using (new LocalizationScope(Language.English))
            {
                StringAssert.Contains("100", RestrictionProd.Call("FullMessage", 100));
                Assert.IsFalse(RestrictionProd.Call("FullMessage", 0).Contains("0 players"),
                    "A missing cap must not render as a field of zero.");
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4b  Entry-denied modal copy (Figma 13915:2273)
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class DeniedModalCopyTests
    {
        private static TournamentDefinition Def()
            => RestrictionProd.Find(RestrictionProd.MapOrFail(RestrictionPayloads.Schedule), "restricted_cup");

        private static IReadOnlyList<TournamentRequirement> Unmet(int? rarity, int? level, int[]? clubs)
            => TournamentEligibility.UnmetRequirements(Def(), rarity, level, clubs);

        private static string Body(IReadOnlyList<TournamentRequirement> u)
            => RestrictionProd.Call("DeniedBody", u);

        [Test]
        public void A_single_failure_names_its_own_rule_the_way_the_node_does()
        {
            using (new LocalizationScope(Language.English))
            {
                // The node's headline, verbatim: "YOUR CHARACTER IS OUTSIDE THIS TOURNAMENT'S
                // RARITY RANGE." then MINIMUM REQUIREMENT / value.
                string body = Body(Unmet(rarity: 1, level: 100, clubs: null));

                StringAssert.Contains("RARITY RANGE", body);
                StringAssert.Contains("MINIMUM REQUIREMENT:", body);
                StringAssert.Contains("RARE", body, "the band floor, spelled out");
                Assert.IsFalse(body.Contains("tourn.denied."), "every key must exist");
            }
        }

        [Test]
        public void Several_failures_share_a_heading_and_list_every_one()
        {
            using (new LocalizationScope(Language.English))
            {
                string body = Body(Unmet(rarity: 1, level: 5, clubs: new[] { 6 }));

                StringAssert.Contains("DO NOT MEET", body,
                    "a headline naming only the first rule would contradict the list beneath it");
                StringAssert.Contains("MINIMUM REQUIREMENT:",   body);
                StringAssert.Contains("MINIMUM LEVEL:",         body);
                StringAssert.Contains("MAXIMUM CLUB RARITY:",   body);
            }
        }

        [Test]
        public void A_breached_ceiling_says_maximum_not_minimum()
        {
            using (new LocalizationScope(Language.English))
            {
                string body = Body(Unmet(rarity: 6, level: 100, clubs: null));   // Supreme > Legendary

                StringAssert.Contains("MAXIMUM ALLOWED:", body);
                Assert.IsFalse(body.Contains("MINIMUM REQUIREMENT:"),
                    "Telling a player their MINIMUM is X when they exceeded the MAXIMUM is the " +
                    "opposite instruction.");
            }
        }

        [Test]
        public void The_rarity_value_is_localized_and_carries_its_rarity_colour()
        {
            // The one actionable value on the screen: a JP player must not be sent to find an
            // "UNCOMMON" character. Colour still comes from RarityHelper, so the spelled-out and
            // letter forms cannot disagree on palette.
            using (new LocalizationScope(Language.English))
                StringAssert.Contains("RARE", RestrictionProd.Call("RarityNameTag", "Rare"));

            using (new LocalizationScope(Language.Japanese))
            {
                string ja = RestrictionProd.Call("RarityNameTag", "Rare");
                StringAssert.Contains("レア", ja);
                Assert.IsFalse(ja.Contains("RARE"), "English must not leak into the JP value.");
                StringAssert.IsMatch(@"^<color=#[0-9A-F]{6}>.+</color>$", ja);
            }
        }

        [Test]
        public void The_full_denial_shows_the_cap_the_server_enforced()
        {
            using (new LocalizationScope(Language.English))
            {
                string body = RestrictionProd.Call("DeniedBodyFull", 100);
                StringAssert.Contains("FULL", body.ToUpperInvariant());
                StringAssert.Contains("100", body);

                Assert.IsFalse(RestrictionProd.Call("DeniedBodyFull", 0).Contains("MAXIMUM PLAYERS"),
                    "A missing cap must not render an empty requirement line.");
            }
        }

        [Test]
        public void The_short_balance_refusal_shows_the_fee_AND_what_the_player_holds()
        {
            using (new LocalizationScope(Language.English))
            {
                string body = RestrictionProd.Call("DeniedBodyInsufficient", 500L, 120L);

                StringAssert.Contains("500", body, "the fee");
                StringAssert.Contains("120", body,
                    "and the balance — 'not enough points' without the gap leaves the player to " +
                    "guess how far off they are, and both paths already know the number.");
                Assert.IsFalse(body.Contains("tourn.denied."));
            }
        }

        [Test]
        public void Every_refusal_headline_resolves_in_both_languages()
        {
            // These are the paths that used to be toasts. A missing row would put a raw key in
            // front of the player on the one screen that is supposed to explain the refusal.
            var keys = new[]
            {
                "tourn.denied.head.insufficient", "tourn.denied.head.offline",
                "tourn.denied.head.unavailable",  "tourn.denied.head.failed",
                "tourn.denied.req.entry_fee",     "tourn.denied.req.your_balance",
            };

            foreach (var lang in new[] { Language.English, Language.Japanese })
                using (new LocalizationScope(lang))
                    foreach (var k in keys)
                        Assert.AreNotEqual(k, LocalizationManager.Get(k),
                            $"'{k}' has no {lang} row — it would render as the raw key.");
        }

        [Test]
        public void An_empty_list_still_renders_something_rather_than_a_blank_modal()
        {
            using (new LocalizationScope(Language.English))
            {
                string body = Body(new List<TournamentRequirement>());
                Assert.IsNotEmpty(body);
                Assert.IsFalse(body.Contains("tourn.denied."));
            }
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §5  Server denials are 200-shaped answers
    // ═════════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class ServerDenialDtoTests
    {
        [Test]
        public void A_full_field_is_a_200_the_client_must_not_read_as_success()
        {
            object dto = AsyncProd.Read(AsyncProd.EnterDto, RestrictionPayloads.EnterFull)!;

            Assert.AreEqual(false, AsyncProd.Field(dto, "Entered"));
            Assert.AreEqual(true,  AsyncProd.Prop(dto, "IsFull"));
            Assert.AreEqual(false, AsyncProd.Prop(dto, "IsInsufficient"));
            Assert.AreEqual(100,   AsyncProd.Field(dto, "MaxPlayers"));
        }

        [Test]
        public void An_ineligible_character_is_a_200_carrying_the_rule_that_refused_it()
        {
            object rarity = AsyncProd.Read(AsyncProd.EnterDto, RestrictionPayloads.EnterIneligibleRarity)!;
            object level  = AsyncProd.Read(AsyncProd.EnterDto, RestrictionPayloads.EnterIneligibleLevel)!;

            Assert.AreEqual(true, AsyncProd.Prop(rarity, "IsIneligible"));
            Assert.AreEqual("char_rarity", AsyncProd.Field(rarity, "Reason"));
            Assert.AreEqual("char_level",  AsyncProd.Field(level,  "Reason"));
            Assert.AreEqual(false, AsyncProd.Field(rarity, "Entered"));
        }
    }
    // ═════════════════════════════════════════════════════════════════════════
    // §6  Widget click — CONFIRM on an ineligible tournament
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drives the REAL CONFIRM button of the REAL TournamentSignupModalController, the same way
    /// tournament_signup_modal's flag-ON denial test does. A synthetic call to the private
    /// OnConfirm would prove the method refuses; it would NOT prove the button is wired to the
    /// method that refuses, which is the thing that actually ships.
    /// <para>
    /// The gate must sit BEFORE the payment path, so the observable proof is that NO ENTRY EXISTS
    /// afterwards: <c>CompleteSignup</c> registers before it navigates, so a missing entry rules
    /// out both the debit and the navigation in one assertion.
    /// </para>
    /// </summary>
    [TestFixture]
    public class IneligibleConfirmWidgetTests
    {
        /// <summary>Plain UTC clock. <c>TimeProviderClock</c> would drag in
        /// <c>Golfin.UI.Rankings.Core</c>, which this test asmdef deliberately does not reference —
        /// the window here only needs to be open.</summary>
        private sealed class WallClock : ITournamentClock
        {
            public DateTime UtcNow => DateTime.UtcNow;
        }

        private const string ModalType   = "GolfinRedux.UI.Tournaments.TournamentSignupModalController";
        private const string ServiceType = "Golfin.Tournaments.TournamentService";

        private readonly List<GameObject> _toDestroy = new List<GameObject>();
        private object? _savedService;
        private object? _savedCharCsv;
        private object? _savedCharMgr;
        private object? _savedRewardPts;
        private Golfin.Save.SaveDataHost? _savedSaveHost;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        [SetUp]
        public void SetUp()
        {
            _savedService   = AsmCSharp.GetStaticInstance(ServiceType);
            _savedCharCsv   = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterDatabaseCSV");
            _savedCharMgr   = AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterManager");
            _savedRewardPts = AsmCSharp.GetStaticInstance("Golfin.Roster.RewardPointsManager");
            _savedSaveHost  = Golfin.Save.SaveDataHost.Instance;

            // ── SaveDataHost + RewardPointsManager ────────────────────────────
            // The ELIGIBLE control click walks the real spend path, and even a fee of 0 goes
            // through RewardPointsManager → SaveDataHost. An in-memory persister keeps the run
            // off disk: this fixture must never touch a real save file.
            SetSaveDataHost(null);
            var saveGo = NewGo("TEST_SaveDataHost");
            var host   = saveGo.AddComponent<Golfin.Save.SaveDataHost>();
            host.SetPersister(new NullPersister());
            if (Golfin.Save.SaveDataHost.Instance == null) SetSaveDataHost(host);

            AsmCSharp.ClearSingleton("Golfin.Roster.RewardPointsManager");
            var rpGo   = NewGo("TEST_RewardPointsManager");
            var rpComp = AsmCSharp.AddComponent(rpGo, "Golfin.Roster.RewardPointsManager");
            if (AsmCSharp.GetStaticInstance("Golfin.Roster.RewardPointsManager") == null)
                AsmCSharp.SetStaticField("Golfin.Roster.RewardPointsManager", "Instance", rpComp);

            // ── CharacterDatabaseCSV + CharacterManager, from the shipped Characters.csv ──
            // char_james is Common / level 10, which is the character the ineligible cases use.
            AsmCSharp.ClearSingleton("Golfin.Roster.CharacterDatabaseCSV");
            var csvGo   = NewGo("TEST_CharacterDatabaseCSV");
            var csvComp = AsmCSharp.AddComponent(csvGo, "Golfin.Roster.CharacterDatabaseCSV");
#if UNITY_EDITOR
            var charCsv = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/Characters.csv");
            Assert.IsNotNull(charCsv, "Assets/Data/Characters.csv must exist as a TextAsset.");
            AsmCSharp.SetField(csvComp, "charactersCSV", charCsv);
            AsmCSharp.CallMethod(csvComp, "LoadCharactersFromCSV");
#else
            Assert.Inconclusive("Requires AssetDatabase (EditMode only).");
#endif
            if (AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterDatabaseCSV") == null)
                AsmCSharp.SetStaticField("Golfin.Roster.CharacterDatabaseCSV", "Instance", csvComp);

            AsmCSharp.ClearSingleton("Golfin.Roster.CharacterManager");
            var cmGo   = NewGo("TEST_CharacterManager");
            var cmComp = AsmCSharp.AddComponent(cmGo, "Golfin.Roster.CharacterManager");
            if (AsmCSharp.GetStaticInstance("Golfin.Roster.CharacterManager") == null)
            {
                AsmCSharp.SetStaticField("Golfin.Roster.CharacterManager", "Instance", cmComp);
                AsmCSharp.CallMethod(cmComp, "LoadRoster");
            }
            AsmCSharp.SetField(cmComp, "selectedCharacterId", "char_james");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _toDestroy)
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _toDestroy.Clear();

            RestoreSingleton(ServiceType, _savedService);
            RestoreSingleton("Golfin.Roster.CharacterDatabaseCSV", _savedCharCsv);
            RestoreSingleton("Golfin.Roster.CharacterManager", _savedCharMgr);
            RestoreSingleton("Golfin.Roster.RewardPointsManager", _savedRewardPts);
            SetSaveDataHost(_savedSaveHost);
        }

        /// <summary>SaveDataHost lives in the Golfin.Save asmdef, so its auto-property backing
        /// field is reached directly rather than through the Assembly-CSharp helper.</summary>
        private static void SetSaveDataHost(Golfin.Save.SaveDataHost? value)
            => typeof(Golfin.Save.SaveDataHost)
                .GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)?
                .SetValue(null, value);

        private GameObject NewGo(string name)
        {
            var go = new GameObject(name);
            _toDestroy.Add(go);
            return go;
        }

        private static void RestoreSingleton(string typeName, object? saved)
        {
            AsmCSharp.ClearSingleton(typeName);
            try { AsmCSharp.SetStaticField(typeName, "Instance", saved); } catch { /* ignore */ }
        }

        // ── Fixture ───────────────────────────────────────────────────────────

        private static TournamentDefinition Def(
            string? charRarityMin = null, int? charLevelMin = null)
            => new TournamentDefinition(
                id: "widget_cup", nameKey: "NAME_WIDGET", clubId: "club_lomond",
                holeSet: new[] { "h1" },
                startUtc: DateTime.UtcNow.AddDays(-1), endUtc: DateTime.UtcNow.AddDays(6),
                resolveDelayMinutes: 30, entryFeeRP: 0L,
                prizeTableId: "pt1", botFieldId: "bf_test",
                sponsorKey: "", leagueKey: "",
                charRarityMin: charRarityMin, charLevelMin: charLevelMin);

        /// <summary>
        /// A TournamentService whose Backend is the given local backend. The GameObject is created
        /// INACTIVE so <c>Awake</c> never runs: the production Awake loads the CSV schedule and
        /// fires a network refresh, neither of which belongs in this test.
        /// </summary>
        private LocalTournamentBackend InstallService(TournamentDefinition def)
        {
            var prize   = new PrizeTable("pt1", new List<PrizeBand> { new PrizeBand(1, 1, 100L, null) });
            var backend = new LocalTournamentBackend(
                definitions: new List<TournamentDefinition> { def },
                prizeTables: new Dictionary<string, PrizeTable> { ["pt1"] = prize },
                botFields:   new Dictionary<string, BotFieldConfig>(RestrictionProd.BotFields),
                botGen:      new BotFieldGenerator(new List<FakePlayerRow>(), new List<BotScoreBracketRow>()),
                clock:       new WallClock(),
                store:       new InMemoryEntryStore(),
                rp:          new FakeRewardPointsService(10_000L),
                items:       new FakeItemRewardService(),
                pars:        new FakeHoleParProvider(4));

            AsmCSharp.ClearSingleton(ServiceType);
            var go = new GameObject("TEST_TournamentService");
            go.SetActive(false);                     // keeps Awake (CSV + network) from running
            _toDestroy.Add(go);
            var service = AsmCSharp.AddComponent(go, ServiceType);
            AsmCSharp.SetStaticField(ServiceType, "Instance", service);
            AsmCSharp.SetField(service, "<Backend>k__BackingField", backend);

            return backend;
        }

        /// <summary>
        /// The modal with a REAL Button on CONFIRM, wired by the modal's own <c>Awake</c>.
        /// <para>
        /// ⚠️ EDITMODE LIFECYCLE: Unity does not run <c>Awake</c> for a MonoBehaviour without
        /// <c>[ExecuteInEditMode]</c>, so it is invoked explicitly — the same pattern
        /// <c>HoleCompleteModalControllerTests</c> documents. It is still the PRODUCTION wiring
        /// code doing the wiring; nothing here re-implements the listener, which is the whole
        /// point of clicking the widget instead of calling <c>OnConfirm</c> directly.
        /// </para>
        /// <para>
        /// The serialized reference is injected BEFORE Awake because Awake is what reads it: a
        /// button assigned afterwards would never be connected and the click would prove nothing.
        /// </para>
        /// </summary>
        private Button BuildModal(string tournamentId)
        {
            var go = NewGo("TEST_SignupModal");
            go.AddComponent<Canvas>();

            var buttonGo = new GameObject("ConfirmButton");
            buttonGo.transform.SetParent(go.transform, false);
            var button = buttonGo.AddComponent<Button>();

            var modal = AsmCSharp.AddComponent(go, ModalType);
            AsmCSharp.SetField(modal, "_confirmButton", button);
            AsmCSharp.SetField(modal, "_tournamentId",  tournamentId);

            AsmCSharp.CallMethod(modal, "Awake");   // wires CONFIRM → OnConfirm
            return button;
        }

        /// <summary>
        /// The log line the client gate emits. Asserting on it turns "nothing registered" from
        /// weak evidence into PROOF: an unwired button, an early return on a missing character, or
        /// a service that never resolved would all leave no entry behind too, and would all fail
        /// this expectation.
        /// </summary>
        private static void ExpectGateRefusal(TournamentEligibilityFailure failure)
            => LogAssert.Expect(LogType.Log, new Regex(
                @"refused by the client gate \(" + failure + @"\)"));

        // ── Tests ─────────────────────────────────────────────────────────────

        [Test]
        public void CONFIRM_on_a_rarity_ineligible_tournament_registers_nothing()
        {
            // char_james is Common; the tournament wants Rare or better.
            var backend = InstallService(Def(charRarityMin: "Rare"));
            var confirm = BuildModal("widget_cup");

            ExpectGateRefusal(TournamentEligibilityFailure.CharacterRarity);
            confirm.onClick.Invoke();

            Assert.IsNull(backend.GetMyEntry("widget_cup"),
                "An ineligible CONFIRM must not reach the payment path. CompleteSignup registers " +
                "BEFORE it navigates, so the absence of an entry rules out the debit AND the " +
                "navigation together.");
        }

        [Test]
        public void CONFIRM_on_a_level_ineligible_tournament_registers_nothing()
        {
            // char_james starts at level 10 (Common start level); the tournament wants 80+.
            var backend = InstallService(Def(charLevelMin: 80));
            var confirm = BuildModal("widget_cup");

            ExpectGateRefusal(TournamentEligibilityFailure.CharacterLevel);
            confirm.onClick.Invoke();

            Assert.IsNull(backend.GetMyEntry("widget_cup"));
        }

        [Test]
        public void CONFIRM_on_an_unrestricted_tournament_still_registers()
        {
            // The control. Without it, a gate that refused EVERYTHING would pass the two tests
            // above and ship a modal nobody can enter through.
            var backend = InstallService(Def());
            var confirm = BuildModal("widget_cup");

            confirm.onClick.Invoke();

            Assert.IsNotNull(backend.GetMyEntry("widget_cup"),
                "An eligible player must still get through the same button. This is ALSO the " +
                "proof that Awake wired CONFIRM at all — without it, a button connected to " +
                "nothing would satisfy the two ineligible tests above for the wrong reason.");
        }
    }
}
