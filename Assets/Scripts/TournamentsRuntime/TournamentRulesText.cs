// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentRulesText
// The sign-up modal's RULES block (Figma 13892:3254) and the entry-refusal
// toasts, rendered FROM DATA (tournament_restrictions §2 / §3).
//
// Pure and static, in the same spirit as TournamentDisplayName / TournamentVenueLine:
// it takes a definition and returns a string, so what the block reads for any given
// tournament is gated by a test rather than by a screenshot — in BOTH languages.
//
// THE FIVE ORIGINAL KEYS SURVIVE AS THE NULL FALLBACKS. A tournament with no
// authored restriction — every row of the shipped tournaments.csv, and any
// dashboard row the operator left blank — renders exactly the five strings it
// rendered before this file existed. A value only ever replaces its own line.
//
// ⚠️ ONE INTENDED CHANGE OF COPY: the old `tourn.rules.gear` reads "Supplied by
// GOLFIN", which was display fiction — no gear was ever supplied. The server
// backfilled every existing tournament to gear_rule='own', so a SERVER-fed
// tournament now reads "Own clubs". The old string still shows when the field is
// genuinely absent (CSV/offline). SPEC §2 calls this out as intended.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Globalization;
using Golfin.Roster;
using UnityEngine;

namespace Golfin.Tournaments
{
    public static class TournamentRulesText
    {
        /// <summary>An absent <c>char_level_min</c> means "from the bottom".</summary>
        private const int OpenLevelMin = 1;

        /// <summary>An absent <c>char_level_max</c> means "to the top" — the same 999 the
        /// dashboard writes for an unbounded ceiling.</summary>
        private const int OpenLevelMax = 999;

        // ── The block ─────────────────────────────────────────────────────────

        /// <summary>The RULES body: five lines, joined at RUNTIME rather than authored
        /// pre-joined, so one line can change length in one language without disturbing the
        /// others.</summary>
        public static string Body(TournamentDefinition? def)
            => string.Join("\n", new[]
            {
                MaxPlayersLine(def),
                DivisionsLine(def),
                PerDivisionLine(def),
                GearLine(def),
                CharactersLine(def),
            });

        public static string MaxPlayersLine(TournamentDefinition? def)
            => def?.MaxPlayers == null
                ? L("tourn.rules.max_players")
                : F("tourn.rules.max_players_n", Num(def.MaxPlayers.Value));

        public static string DivisionsLine(TournamentDefinition? def)
            => def?.DivisionType switch
            {
                TournamentDivisionType.Open       => L("tourn.rules.divisions_open"),
                TournamentDivisionType.Level      => L("tourn.rules.divisions_level"),
                TournamentDivisionType.RarityBand => L("tourn.rules.divisions_rarity"),
                _                                 => L("tourn.rules.divisions"),
            };

        public static string PerDivisionLine(TournamentDefinition? def)
            => def?.PlayersPerDivision == null
                ? L("tourn.rules.per_division")
                : F("tourn.rules.per_division_n", Num(def.PlayersPerDivision.Value));

        public static string GearLine(TournamentDefinition? def)
        {
            if (def?.GearRule == null) return L("tourn.rules.gear");
            if (def.GearRule == TournamentGearRule.Supplied) return L("tourn.rules.gear_supplied");

            // Own, with or without a ceiling on the bag.
            return def.ClubRarityMax == null
                ? L("tourn.rules.gear_own")
                : F("tourn.rules.gear_own_max", RarityTag(def.ClubRarityMax));
        }

        public static string CharactersLine(TournamentDefinition? def)
        {
            bool hasRarity = def?.CharRarityMin != null || def?.CharRarityMax != null;
            bool hasLevel  = def?.CharLevelMin  != null || def?.CharLevelMax  != null;

            if (!hasRarity && !hasLevel) return L("tourn.rules.characters");

            if (hasRarity && hasLevel)
                return F("tourn.rules.chars_rarity_level_band", RarityBandText(def!), LevelBandText(def!));

            return hasRarity
                ? F("tourn.rules.chars_rarity_band", RarityBandText(def!))
                : F("tourn.rules.chars_level_band",  LevelBandText(def!));
        }

        // ── Refusal copy ──────────────────────────────────────────────────────

        /// <summary>
        /// The toast for a refused CONFIRM. It NAMES the rule that refused — a bare "you cannot
        /// enter" leaves the player with no idea which of five lines they failed.
        /// <para>
        /// Shared by both paths on purpose: the client gate decides it for the local backend, and
        /// the server's <c>full</c>/<c>ineligible</c> denials are mapped onto the same failures, so
        /// a refusal reads identically whether the client or the server was the one to notice.
        /// </para>
        /// </summary>
        public static string DenialMessage(TournamentEligibilityFailure failure, TournamentDefinition? def)
            => failure switch
            {
                TournamentEligibilityFailure.CharacterRarity =>
                    F("tourn.entry.denied.char_rarity", def != null ? RarityBandText(def) : string.Empty),
                TournamentEligibilityFailure.CharacterLevel =>
                    F("tourn.entry.denied.char_level", def != null ? LevelBandText(def) : string.Empty),
                TournamentEligibilityFailure.ClubRarity =>
                    F("tourn.entry.denied.club_rarity", RarityTag(def?.ClubRarityMax)),
                TournamentEligibilityFailure.Full =>
                    F("tourn.entry.denied.full", Num(def?.MaxPlayers ?? 0)),
                _ => L("tourn.entry.denied.generic"),
            };

        /// <summary>The <c>full</c> toast, which needs the cap the SERVER enforced rather than the
        /// one the definition happens to carry (a stale schedule can disagree).</summary>
        public static string FullMessage(int maxPlayers)
            => maxPlayers > 0
                ? F("tourn.entry.denied.full", Num(maxPlayers))
                : L("tourn.entry.denied.generic");

        /// <summary>Map the server's <c>ineligible</c> reason onto the shared failure vocabulary.
        /// An unknown reason from a newer server falls to <see cref="TournamentEligibilityFailure.None"/>,
        /// which the caller renders as the generic refusal rather than mis-naming a rule.</summary>
        public static TournamentEligibilityFailure ParseServerReason(string? reason) => reason switch
        {
            "char_rarity" => TournamentEligibilityFailure.CharacterRarity,
            "char_level"  => TournamentEligibilityFailure.CharacterLevel,
            _             => TournamentEligibilityFailure.None,
        };

        // ── Band text ─────────────────────────────────────────────────────────

        /// <summary>e.g. <c>"R – L"</c>, each letter in its rarity colour. An absent bound is
        /// rendered as the extreme of the ladder, which is exactly what it means.</summary>
        public static string RarityBandText(TournamentDefinition def)
        {
            string min = RarityTag(def.CharRarityMin ?? TournamentRestrictions.RarityLadder[0]);
            string max = RarityTag(def.CharRarityMax
                                   ?? TournamentRestrictions.RarityLadder[TournamentRestrictions.RarityLadder.Length - 1]);
            return min + " – " + max;
        }

        /// <summary>e.g. <c>"Lv 80 – 160"</c>.</summary>
        public static string LevelBandText(TournamentDefinition def)
            => "Lv " + Num(def.CharLevelMin ?? OpenLevelMin) + " – " + Num(def.CharLevelMax ?? OpenLevelMax);

        /// <summary>
        /// A rarity as its single letter in its own rarity colour — <c>R</c>, <c>L</c>, <c>M</c> —
        /// e.g. <c>&lt;color=#E84D3D&gt;L&lt;/color&gt;</c> (Cesar, 2026-08-19).
        /// <para>
        /// Both the letter and the colour come from <see cref="RarityHelper"/>, the project's one
        /// source for rarity presentation — the badge on every card and every carousel already
        /// reads C/U/R/M/L/S in exactly these colours, so the RULES block cannot drift from them.
        /// The letters are also language-neutral, which is why nothing here is localized.
        /// </para>
        /// <para>
        /// Rich text, so every consumer must render through TMP: the RULES body and
        /// <c>ToastController</c>'s <c>TMP_Text</c> both do.
        /// </para>
        /// </summary>
        public static string RarityTag(string? canonicalRarity)
        {
            int? rank = TournamentRestrictions.RarityRank(canonicalRarity);
            if (rank == null) return string.Empty;

            // The ladder IS CharacterRarity, 1-based — pinned by
            // RarityLadderPinTests.The_rarity_ladder_matches_CharacterRaritys_declaration_order.
            var rarity = (CharacterRarity)(rank.Value - 1);

            return "<color=#" + ColorUtility.ToHtmlStringRGB(RarityHelper.GetRarityColor(rarity)) + ">"
                 + RarityHelper.GetRarityLabel(rarity) + "</color>";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string L(string key) => LocalizationManager.Get(key);

        /// <summary>
        /// Localize then format. <c>LocalizationManager.Get</c> returns the KEY when a row is
        /// missing, and a key carries no <c>{0}</c>, so a missing row degrades to the bare key
        /// instead of throwing a FormatException in front of the player.
        /// </summary>
        private static string F(string key, params object[] args)
        {
            string pattern = L(key);
            try   { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
            catch (System.FormatException) { return pattern; }
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
