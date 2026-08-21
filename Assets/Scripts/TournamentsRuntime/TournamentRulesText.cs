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
using System.Collections.Generic;
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

        // ── Entry-refusal modal (tournament_entry_denied_modal, Figma 13915:2273) ──

        /// <summary>
        /// The highlight colour the node uses for the requirement VALUE. Applied to non-rarity
        /// values (levels, player caps); a rarity value uses its own rarity colour instead, because
        /// one hardcoded blue cannot be right for all six and the node only happens to show
        /// Uncommon.
        /// </summary>
        private const string ValueHighlight = "#2775DD";

        /// <summary>
        /// The whole modal body: a headline naming what went wrong, then one
        /// label + coloured value pair per unmet requirement, exactly the shape of the node
        /// (<c>reason</c> / blank / <c>MINIMUM REQUIREMENT:</c> / <c>UNCOMMON</c>).
        /// <para>
        /// Every unmet requirement is listed, not just the first: sending a player to fix one rule
        /// only to be refused by the next is the failure this modal exists to prevent.
        /// </para>
        /// </summary>
        public static string DeniedBody(IReadOnlyList<TournamentRequirement>? unmet)
        {
            if (unmet == null || unmet.Count == 0) return L("tourn.denied.head.generic");

            var sb = new System.Text.StringBuilder();
            sb.Append(Headline(unmet));

            for (int i = 0; i < unmet.Count; i++)
            {
                sb.Append("\n\n");
                sb.Append(RequirementLabel(unmet[i]));
                sb.Append('\n');
                sb.Append(RequirementValue(unmet[i]));
            }
            return sb.ToString();
        }

        /// <summary>
        /// A refusal with nothing to itemise — offline, gone, or a failed handshake. Headline only.
        /// </summary>
        public static string DeniedBodySimple(string headKey) => L(headKey);

        /// <summary>
        /// The short-balance refusal. Shows BOTH numbers, because "not enough points" without the
        /// gap leaves the player to guess how far off they are — and both paths already know:
        /// the client pre-check reads the wallet, the server returns requested + total.
        /// </summary>
        public static string DeniedBodyInsufficient(long entryFee, long balance)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(L("tourn.denied.head.insufficient"));
            sb.Append("\n\n").Append(L("tourn.denied.req.entry_fee"))
              .Append('\n').Append(Colour(Num((int)entryFee), ValueHighlight));
            sb.Append("\n\n").Append(L("tourn.denied.req.your_balance"))
              .Append('\n').Append(Colour(Num((int)balance), ValueHighlight));
            return sb.ToString();
        }

        /// <summary>The server's <c>full</c> denial, which has no per-character requirement to
        /// list — just the cap it enforced.</summary>
        public static string DeniedBodyFull(int maxPlayers)
        {
            string head = L("tourn.denied.head.full");
            if (maxPlayers <= 0) return head;
            return head + "\n\n" + L("tourn.denied.req.full_cap") + "\n" + Colour(Num(maxPlayers), ValueHighlight);
        }

        /// <summary>
        /// One failure names its own rule, the way the node does. Two or more cannot — a headline
        /// that named only the first would contradict the list under it — so they share a heading
        /// and the list carries the detail.
        /// </summary>
        private static string Headline(IReadOnlyList<TournamentRequirement> unmet)
        {
            if (unmet.Count > 1) return L("tourn.denied.head.multiple");

            return unmet[0].Failure switch
            {
                TournamentEligibilityFailure.CharacterRarity => L("tourn.denied.head.char_rarity"),
                TournamentEligibilityFailure.CharacterLevel  => L("tourn.denied.head.char_level"),
                TournamentEligibilityFailure.ClubRarity      => L("tourn.denied.head.club_rarity"),
                TournamentEligibilityFailure.Full            => L("tourn.denied.head.full"),
                _                                            => L("tourn.denied.head.generic"),
            };
        }

        private static string RequirementLabel(TournamentRequirement r) => r.Failure switch
        {
            TournamentEligibilityFailure.CharacterRarity =>
                L(r.IsMaximum ? "tourn.denied.req.rarity_max" : "tourn.denied.req.rarity_min"),
            TournamentEligibilityFailure.CharacterLevel =>
                L(r.IsMaximum ? "tourn.denied.req.level_max" : "tourn.denied.req.level_min"),
            TournamentEligibilityFailure.ClubRarity => L("tourn.denied.req.club_max"),
            TournamentEligibilityFailure.Full       => L("tourn.denied.req.full_cap"),
            _                                       => string.Empty,
        };

        /// <summary>
        /// A rarity bound renders SPELLED OUT in its own rarity colour, which is what the node
        /// shows — the single letter is the RULES block's compact form and would be unreadable as
        /// the one actionable value on a refusal screen.
        /// </summary>
        private static string RequirementValue(TournamentRequirement r)
        {
            if (r.RarityBound != null) return RarityNameTag(r.RarityBound);
            if (r.LevelBound.HasValue) return Colour(Num(r.LevelBound.Value), ValueHighlight);
            return string.Empty;
        }

        /// <summary>
        /// Spelled-out rarity in its rarity colour, e.g.
        /// <c>&lt;color=#4A8FE3&gt;UNCOMMON&lt;/color&gt;</c> — アンコモン for a JP player.
        /// <para>
        /// The WORD comes from the shipped <c>RARITY_*</c> rows and the COLOUR from
        /// <see cref="RarityHelper"/>. That split matters: <c>GetRarityFullName</c> returns
        /// hardcoded English caps, and this is the one actionable value on a refusal screen, so a
        /// JP player must not be told to go find an "UNCOMMON" character. The colour still comes
        /// from the same helper <see cref="RarityTag"/> uses, so the letter and word forms can
        /// never disagree on palette.
        /// </para>
        /// </summary>
        public static string RarityNameTag(string? canonicalRarity)
        {
            int? rank = TournamentRestrictions.RarityRank(canonicalRarity);
            if (rank == null) return string.Empty;

            var rarity = (CharacterRarity)(rank.Value - 1);

            // A missing row returns the key, which would render "RARITY_UNCOMMON" at the player.
            // Fall back to the English name rather than showing them a key.
            string key  = "RARITY_" + canonicalRarity!.ToUpperInvariant();
            string word = L(key);
            if (word == key) word = RarityHelper.GetRarityFullName(rarity);

            return Colour(word, "#" + ColorUtility.ToHtmlStringRGB(RarityHelper.GetRarityColor(rarity)));
        }

        private static string Colour(string text, string hex) => "<color=" + hex + ">" + text + "</color>";

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
