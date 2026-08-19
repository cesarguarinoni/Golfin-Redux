// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — TournamentEligibilityTests (tournament_restrictions)
//
// ASSEMBLY: Golfin.Tournaments.Tests. Headless — no scene, no singletons, no
// Unity lifecycle — because the decision itself is pure: TournamentEligibility
// takes ranks, and the signup modal is only the adapter that reads them off
// CharacterManager / BagManager.
//
// COVERAGE (SPEC § Acceptance 1 + 2)
//   §1  Normalisation — unknown enum/rarity degrades to null (unrestricted),
//                       never throws; case/whitespace tolerated; 0 is "no cap"
//   §2  Rarity band    — below min / above max / in band / bounds inclusive
//   §3  Level band     — below / above / in / open-ended
//   §4  Club cap       — violated by one equipped club; `supplied` skips it
//   §5  Unrestricted   — an all-null definition is ALWAYS eligible, including
//                        for a character whose rarity/level cannot be resolved
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Golfin.Tournaments.Tests
{
    [TestFixture]
    public class TournamentEligibilityTests
    {
        // ── Fixture ───────────────────────────────────────────────────────────

        private static TournamentDefinition Def(
            string? category = null,
            int?    maxPlayers = null,
            int?    playersPerDivision = null,
            string? divisionType = null,
            string? charRarityMin = null,
            string? charRarityMax = null,
            int?    charLevelMin = null,
            int?    charLevelMax = null,
            string? gearRule = null,
            string? clubRarityMax = null)
            => new TournamentDefinition(
                id: "t1", nameKey: "NAME_T1", clubId: "club_lomond",
                holeSet: new[] { "h1" },
                startUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
                endUtc:   new DateTime(2026, 8, 8, 0, 0, 0, DateTimeKind.Utc),
                resolveDelayMinutes: 30, entryFeeRP: 0L,
                prizeTableId: "pt1", botFieldId: "bf_empty",
                sponsorKey: "", leagueKey: "",
                category: category, maxPlayers: maxPlayers,
                playersPerDivision: playersPerDivision, divisionType: divisionType,
                charRarityMin: charRarityMin, charRarityMax: charRarityMax,
                charLevelMin: charLevelMin, charLevelMax: charLevelMax,
                gearRule: gearRule, clubRarityMax: clubRarityMax);

        private const int Common = 1, Uncommon = 2, Rare = 3, Mythic = 4, Legendary = 5, Supreme = 6;

        private static TournamentEligibilityFailure Eval(
            TournamentDefinition def, int? rarity = Rare, int? level = 100, params int[] clubs)
            => TournamentEligibility.Evaluate(def, rarity, level, clubs);

        // ═════════════════════════════════════════════════════════════════════
        // §1  Normalisation — degrade, never throw
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Unrestricted_definition_carries_all_nulls()
        {
            var def = Def();

            Assert.IsNull(def.Category);
            Assert.IsNull(def.MaxPlayers);
            Assert.IsNull(def.PlayersPerDivision);
            Assert.IsNull(def.DivisionType);
            Assert.IsNull(def.CharRarityMin);
            Assert.IsNull(def.CharRarityMax);
            Assert.IsNull(def.CharLevelMin);
            Assert.IsNull(def.CharLevelMax);
            Assert.IsNull(def.GearRule);
            Assert.IsNull(def.ClubRarityMax);
            Assert.IsFalse(def.HasEntryRestrictions);
        }

        [Test]
        public void Backfilled_defaults_apply_only_to_the_effective_accessors()
        {
            // The distinction the RULES block depends on: "the server said nothing" must render the
            // pre-existing localized line, while the GATE still reasons with the backfilled default.
            var def = Def();

            Assert.IsNull(def.GearRule,     "GearRule stays null so the RULES line falls back.");
            Assert.AreEqual(TournamentGearRule.Own,       def.EffectiveGearRule);
            Assert.AreEqual(TournamentCategory.Sponsor,   def.EffectiveCategory);
            Assert.AreEqual(TournamentDivisionType.Level, def.EffectiveDivisionType);
        }

        [Test]
        public void Wire_values_parse_into_the_enums()
        {
            var def = Def(category: "competitive", divisionType: "rarity_band", gearRule: "supplied");

            Assert.AreEqual(TournamentCategory.Competitive,     def.Category);
            Assert.AreEqual(TournamentDivisionType.RarityBand,  def.DivisionType);
            Assert.AreEqual(TournamentGearRule.Supplied,        def.GearRule);
        }

        [Test]
        public void Unknown_enum_values_from_a_newer_server_degrade_instead_of_throwing()
        {
            TournamentDefinition? def = null;
            Assert.DoesNotThrow(() => def = Def(
                category: "seasonal_v2", divisionType: "handicap", gearRule: "rental",
                charRarityMin: "Ultra", clubRarityMax: "Godlike"));

            Assert.IsNull(def!.Category,      "An unknown category must not throw and must not guess.");
            Assert.IsNull(def.DivisionType);
            Assert.IsNull(def.GearRule);
            Assert.IsNull(def.CharRarityMin,  "An unknown rarity degrades to unrestricted.");
            Assert.IsNull(def.ClubRarityMax);
            Assert.IsFalse(def.HasEntryRestrictions,
                "A definition whose every restriction failed to parse must gate nothing at all.");
        }

        [Test]
        public void Rarity_names_are_canonicalised_case_insensitively()
        {
            var def = Def(charRarityMin: " rare ", charRarityMax: "LEGENDARY");

            Assert.AreEqual("Rare",      def.CharRarityMin);
            Assert.AreEqual("Legendary", def.CharRarityMax);
        }

        [Test]
        public void Zero_and_negative_counts_mean_no_cap_not_a_cap_of_zero()
        {
            var def = Def(maxPlayers: 0, playersPerDivision: -5, charLevelMin: 0);

            Assert.IsNull(def.MaxPlayers,         "A 0 cap would otherwise mean nobody may enter.");
            Assert.IsNull(def.PlayersPerDivision);
            Assert.IsNull(def.CharLevelMin);
        }

        // ═════════════════════════════════════════════════════════════════════
        // §2  Character rarity band
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Rarity_below_the_minimum_is_refused()
            => Assert.AreEqual(TournamentEligibilityFailure.CharacterRarity,
                Eval(Def(charRarityMin: "Rare"), rarity: Uncommon));

        [Test]
        public void Rarity_above_the_maximum_is_refused()
            => Assert.AreEqual(TournamentEligibilityFailure.CharacterRarity,
                Eval(Def(charRarityMax: "Rare"), rarity: Mythic));

        [Test]
        public void Rarity_inside_the_band_is_eligible()
            => Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(charRarityMin: "Rare", charRarityMax: "Legendary"), rarity: Mythic));

        [Test]
        public void The_band_bounds_are_inclusive_on_both_ends()
        {
            var def = Def(charRarityMin: "Rare", charRarityMax: "Legendary");

            Assert.AreEqual(TournamentEligibilityFailure.None, Eval(def, rarity: Rare),
                "min is inclusive — a Rare character belongs in a Rare–Legendary tournament.");
            Assert.AreEqual(TournamentEligibilityFailure.None, Eval(def, rarity: Legendary),
                "max is inclusive.");
        }

        [Test]
        public void An_unresolvable_rarity_is_refused_ONLY_when_a_band_is_set()
        {
            Assert.AreEqual(TournamentEligibilityFailure.CharacterRarity,
                Eval(Def(charRarityMin: "Rare"), rarity: null),
                "A character that cannot prove its rarity cannot enter a rarity-restricted " +
                "tournament — the same branch the server takes.");

            Assert.AreEqual(TournamentEligibilityFailure.None, Eval(Def(), rarity: null),
                "…but an unrestricted tournament never reaches that branch.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // §3  Character level band
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Level_below_the_minimum_is_refused()
            => Assert.AreEqual(TournamentEligibilityFailure.CharacterLevel,
                Eval(Def(charLevelMin: 80), level: 40));

        [Test]
        public void Level_above_the_maximum_is_refused()
            => Assert.AreEqual(TournamentEligibilityFailure.CharacterLevel,
                Eval(Def(charLevelMax: 120), level: 160));

        [Test]
        public void Level_inside_the_band_is_eligible()
            => Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(charLevelMin: 80, charLevelMax: 160), level: 120));

        [Test]
        public void An_open_ended_level_band_only_constrains_the_end_it_sets()
        {
            Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(charLevelMin: 80), level: 999), "No ceiling was set.");
            Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(charLevelMax: 80), level: 1), "No floor was set.");
        }

        [Test]
        public void An_unknown_level_is_refused_ONLY_when_a_band_is_set()
        {
            Assert.AreEqual(TournamentEligibilityFailure.CharacterLevel,
                Eval(Def(charLevelMin: 80), level: null));
            Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(), level: null));
        }

        [Test]
        public void Rarity_is_evaluated_before_level_so_the_toast_names_the_first_broken_rule()
        {
            // Both bands fail. Server order is rarity → level; matching it keeps the client and
            // server toasts identical for the same character.
            var def = Def(charRarityMin: "Legendary", charLevelMin: 200);

            Assert.AreEqual(TournamentEligibilityFailure.CharacterRarity,
                Eval(def, rarity: Common, level: 10));
        }

        // ═════════════════════════════════════════════════════════════════════
        // §4  Equipped-bag club cap
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void One_club_above_the_cap_refuses_the_whole_bag()
            => Assert.AreEqual(TournamentEligibilityFailure.ClubRarity,
                Eval(Def(gearRule: "own", clubRarityMax: "Rare"),
                     clubs: new[] { Common, Common, Mythic, Uncommon }));

        [Test]
        public void A_bag_entirely_at_or_below_the_cap_is_eligible()
            => Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(gearRule: "own", clubRarityMax: "Rare"),
                     clubs: new[] { Common, Rare, Uncommon }));

        [Test]
        public void Supplied_gear_skips_the_club_check_entirely()
            => Assert.AreEqual(TournamentEligibilityFailure.None,
                Eval(Def(gearRule: "supplied", clubRarityMax: "Common"),
                     clubs: new[] { Supreme, Legendary }),
                "Under `supplied` the player's own clubs are not what gets played, so capping " +
                "them would refuse an entry the rule does not actually restrict.");

        [Test]
        public void An_absent_bag_clears_every_ceiling()
        {
            var def = Def(gearRule: "own", clubRarityMax: "Common");

            Assert.AreEqual(TournamentEligibilityFailure.None,
                TournamentEligibility.Evaluate(def, Rare, 100, null), "null bag");
            Assert.AreEqual(TournamentEligibilityFailure.None,
                TournamentEligibility.Evaluate(def, Rare, 100, new List<int>()), "empty bag");
        }

        [Test]
        public void The_club_cap_applies_when_gear_rule_was_never_served()
            => Assert.AreEqual(TournamentEligibilityFailure.ClubRarity,
                Eval(Def(clubRarityMax: "Rare"), clubs: new[] { Supreme }),
                "gear_rule null means the backfilled `own`, so a served club cap still bites.");

        // ═════════════════════════════════════════════════════════════════════
        // §5  Unrestricted is always eligible
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void An_unrestricted_tournament_admits_anyone()
        {
            var def = Def();

            Assert.AreEqual(TournamentEligibilityFailure.None,
                TournamentEligibility.Evaluate(def, Common, 1, new List<int> { Supreme }));
            Assert.AreEqual(TournamentEligibilityFailure.None,
                TournamentEligibility.Evaluate(def, Supreme, 999, new List<int>()));
            Assert.AreEqual(TournamentEligibilityFailure.None,
                TournamentEligibility.Evaluate(def, null, null, null),
                "Not even an unresolvable character is refused by a tournament with no rules.");
        }

        [Test]
        public void A_null_definition_never_refuses()
            => Assert.AreEqual(TournamentEligibilityFailure.None,
                TournamentEligibility.Evaluate(null!, null, null, null));

        // ═════════════════════════════════════════════════════════════════════
        // §6  The rank ladder itself
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void The_rarity_ladder_is_the_servers_RARITY_RANK_ladder()
        {
            // backend/routers/tournaments_golfin.py:
            //   RARITY_RANK = {"Common":1,"Uncommon":2,"Rare":3,"Mythic":4,"Legendary":5,"Supreme":6}
            Assert.AreEqual(1, TournamentRestrictions.RarityRank("Common"));
            Assert.AreEqual(2, TournamentRestrictions.RarityRank("Uncommon"));
            Assert.AreEqual(3, TournamentRestrictions.RarityRank("Rare"));
            Assert.AreEqual(4, TournamentRestrictions.RarityRank("Mythic"));
            Assert.AreEqual(5, TournamentRestrictions.RarityRank("Legendary"));
            Assert.AreEqual(6, TournamentRestrictions.RarityRank("Supreme"));
            Assert.IsNull(TournamentRestrictions.RarityRank("Ultra"));
            Assert.IsNull(TournamentRestrictions.RarityRank(null));
        }
    }
}
