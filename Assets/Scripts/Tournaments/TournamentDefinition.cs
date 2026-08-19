// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentDefinition
// One row from tournaments.csv (GDD §9). Loaded by T2 (tournament_csv_loaders).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Immutable definition of one tournament — maps directly to a row in
    /// <c>tournaments.csv</c> (GDD §9). Identical for v1 local and future remote
    /// backends (GDD §8 forward-compat).
    /// </summary>
    public sealed class TournamentDefinition
    {
        /// <summary>Stable string id; also seeds the deterministic bot field.</summary>
        public string Id { get; }

        /// <summary>Localization key (JP/EN) for the tournament name.</summary>
        public string NameKey { get; }

        /// <summary>
        /// The club/course id for this tournament.
        /// Formerly <c>courseId</c> in the GDD §9 schema;
        /// renamed to <c>clubId</c> to align with the T1 DTO spec §2.
        /// </summary>
        public string ClubId { get; }

        /// <summary>
        /// Explicit ordered list of hole ids in this tournament.
        /// Using an explicit list (not a range) so any subset of a course's holes
        /// can be combined (e.g. Lomond holes 1-9, or a 6-hole custom set).
        /// GDD §9: holeSet column; SPEC §7 flag resolved: explicit hole-id list.
        /// </summary>
        public IReadOnlyList<string> HoleSet { get; }

        /// <summary>Tournament opens for entries/play at this UTC instant.</summary>
        public DateTime StartUtc { get; }

        /// <summary>
        /// Tournament window closes at this UTC instant.
        /// At endUtc, all in-progress entries are auto-submitted (DNF if incomplete).
        /// </summary>
        public DateTime EndUtc { get; }

        /// <summary>
        /// Minutes after <see cref="EndUtc"/> before results are finalised and prizes granted.
        /// GDD §9 resolveDelayMinutes — per-tournament delay so the backend can verify
        /// and rank all submissions before the leaderboard is sealed.
        /// Added in T2 (§0.1 approved) as an additive amendment to the T1 contract.
        /// </summary>
        public int ResolveDelayMinutes { get; }

        /// <summary>
        /// RP entry fee. 0 = free entry.
        /// Debited once via RewardPointsManager at Register() time (T4).
        /// </summary>
        public long EntryFeeRP { get; }

        /// <summary>
        /// Reference to the prize table in tournament_prizes.csv (GDD §10).
        /// Resolved by T4 at prize-grant time.
        /// </summary>
        public string PrizeTableId { get; }

        /// <summary>
        /// Reference to the bot field config in tournament_bot_fields.csv (GDD §7).
        /// Used by T3 (tournament_bot_field) to pre-roll the deterministic bot scores.
        /// </summary>
        public string BotFieldId { get; }

        /// <summary>Localization key for the presenting sponsor (single mark, GDD §16 U3).</summary>
        public string SponsorKey { get; }

        /// <summary>Localization/filter key for the league this tournament belongs to.</summary>
        public string LeagueKey { get; }

        /// <summary>
        /// Server-authored display title, e.g. <c>"PUMA Summer Slam"</c>.
        /// <para>
        /// A tournament's identity is NOT its venue (Cesar, 2026-08-14): it can be brand-led,
        /// and a tournament created in the dashboard has no localization key in the shipped
        /// build. This is a rung of the display ladder
        /// <c>localize(NameKey) → TitleJa (JP only) → Title → Id</c> — see
        /// <c>TournamentDisplayName.Resolve</c>. Without it, "add a tournament with no new
        /// build" would be false for its name.
        /// </para>
        /// <para>
        /// For CSV rows this carries the raw <c>nameKey</c> column value (see
        /// <c>TournamentCsvLoader.LoadTournaments</c>): the dashboard's CSV export writes
        /// <c>nameKey ?? title</c> into that one column, so a dashboard-named tournament
        /// round-tripped through the CSV would otherwise resolve nowhere and fall to its slug.
        /// A key that resolves is unaffected — rung 1 still wins.
        /// </para>
        /// </summary>
        public string? Title { get; }

        /// <summary>
        /// Server-authored Japanese display title. Null for CSV rows.
        /// <para>
        /// GOLFIN ships EN + JP, but the only bilingual path for a tournament name was
        /// <c>NameKey</c> — a localization key resolved against <c>LocalizationText.csv</c>,
        /// which ships INSIDE the build, so the dashboard can only ever reference keys that
        /// already exist. A tournament named in the panel therefore had exactly one name, in
        /// one language. This is the second rung of the ladder and is consulted
        /// <b>only when <c>LocalizationManager.CurrentLanguage == Language.Japanese</c></b>:
        /// an English player must never see it, even when <see cref="Title"/> is empty.
        /// </para>
        /// <para>
        /// ⚠️ Interim by design (Cesar, 2026-08-17: "we will move the Localization to the
        /// editor in the future"). Two title columns do not scale to a third language and
        /// nothing here should be read as a decision that they would.
        /// </para>
        /// </summary>
        public string? TitleJa { get; }

        /// <summary>
        /// Server-authored card artwork URL. Always null for CSV rows, and null for a server row
        /// whose URL failed the <c>TournamentArtPolicy</c> host allowlist — a refused URL is
        /// indistinguishable from no URL to everything downstream, which is the point.
        /// </summary>
        public string? BannerUrl { get; }

        /// <summary>
        /// Operator-authored English description blurb, shown in the sign-up modal's info row
        /// (Figma <c>13892:3250</c>). Null for CSV rows — the bundled <c>tournaments.csv</c>
        /// deliberately gains no description column.
        /// <para>
        /// This is NOT <c>public.tournaments.description</c>: that column is GPS-owned, predates
        /// this work and is single-locale. Overloading it would put two products' meanings in one
        /// field. See <c>Docs/Specs/Active/tournament_signup_modal/SPEC.md</c> §1.
        /// </para>
        /// </summary>
        public string? DescriptionEn { get; }

        /// <summary>
        /// Operator-authored Japanese description blurb. Consulted <b>only</b> when
        /// <c>LocalizationManager.CurrentLanguage == Language.Japanese</c> — the same JP-only
        /// asymmetry <see cref="TitleJa"/> has, and for the same reason: an English player must
        /// never fall into Japanese copy. See <c>TournamentDescription.Resolve</c>.
        /// </summary>
        public string? DescriptionJa { get; }

        /// <summary>
        /// Optional build-time localization key for the blurb. Outranks both columns when it
        /// resolves, because a shipped key is a real translation pair; a key that does NOT resolve
        /// falls through silently and is never rendered raw.
        /// </summary>
        public string? DescriptionKey { get; }

        /// <summary>
        /// English artwork for the sign-up modal's cross-promotion strip (Figma
        /// <c>13892:3435</c>, 970 × 252), or null when the tournament has no banner assigned.
        /// <para>
        /// Sourced from a <c>game_banners</c> row with <c>placement = 'tournament_modal'</c>,
        /// joined server-side. The server has already applied <c>is_active</c>, so a switched-off
        /// banner arrives here as null and the modal renders its no-banner state.
        /// </para>
        /// <para>
        /// ⚠️ NOT <see cref="BannerUrl"/>. That is the 260 × 360 card art in the
        /// <c>tournament-art</c> bucket; this is a 970 × 252 strip in <c>game-banners</c>.
        /// </para>
        /// </summary>
        public string? ModalBannerImageUrlEn { get; }

        /// <summary>
        /// Japanese artwork for the same strip. Preferred for JP players, with EN as the
        /// fallback and vice versa — the ladder is <c>BannerService.ResolveImageUrl</c>, shared
        /// with the Home and Rankings slots rather than reimplemented.
        /// </summary>
        public string? ModalBannerImageUrlJa { get; }

        /// <summary>
        /// Where tapping the strip goes, or null for a non-interactive banner. Passed through
        /// raw: it is gated by <c>BannerPolicy.IsLinkAllowed</c> when the button's interactable
        /// state is set, and again at the moment of the tap.
        /// </summary>
        public string? ModalBannerLinkUrl { get; }

        // ── Category + entry restrictions (tournament_restrictions, 2026-08-18) ──
        // Every one of these is NULL for a CSV row and null means UNRESTRICTED, so the
        // shipped-CSV path behaves exactly as it did before this block existed. The three
        // enums are nullable rather than defaulted for the same reason the RULES block needs:
        // "the server did not say" and "the server said `level`" must render differently —
        // null falls back to the pre-existing localized line, a value renders the real one.
        // Effective*, below, is what the GATE reads, where the backfilled default does apply.

        /// <summary>What kind of tournament this is. Presentation only — <c>Competitive</c>
        /// carries no stat normalization in this build (that is a later phase, deliberately not
        /// implied by this field).</summary>
        public TournamentCategory? Category { get; }

        /// <summary>Human entry cap, bots excluded, or null for uncapped. Only the SERVER can
        /// enforce it — it is the one that counts the entries — so this is display-only here.</summary>
        public int? MaxPlayers { get; }

        /// <summary>Players per division, or null.</summary>
        public int? PlayersPerDivision { get; }

        /// <summary>How the field is split, or null when the server did not say.</summary>
        public TournamentDivisionType? DivisionType { get; }

        /// <summary>Lowest allowed character rarity, canonical-cased
        /// (<c>"Common"</c>…<c>"Supreme"</c>), or null for no floor. An unrecognised value from a
        /// newer server is normalised to null — unrestricted — rather than throwing.</summary>
        public string? CharRarityMin { get; }

        /// <summary>Highest allowed character rarity, or null for no ceiling.</summary>
        public string? CharRarityMax { get; }

        /// <summary>Lowest allowed character level, or null.</summary>
        public int? CharLevelMin { get; }

        /// <summary>Highest allowed character level, or null.</summary>
        public int? CharLevelMax { get; }

        /// <summary>Whose clubs are played, or null when the server did not say.</summary>
        public TournamentGearRule? GearRule { get; }

        /// <summary>Highest allowed club rarity in the equipped bag, or null for no cap. Enforced
        /// CLIENT-SIDE only — the server never sees the bag — and only under
        /// <see cref="TournamentGearRule.Own"/>.</summary>
        public string? ClubRarityMax { get; }

        /// <summary>The gear rule the ENTRY GATE applies: the served value, or the backfilled
        /// <see cref="TournamentGearRule.Own"/> when the server said nothing. Kept separate from
        /// <see cref="GearRule"/> so display can still tell "unset" from "own".</summary>
        public TournamentGearRule EffectiveGearRule => GearRule ?? TournamentGearRule.Own;

        /// <summary>The category the game reasons with: served, else the backfilled
        /// <see cref="TournamentCategory.Sponsor"/>.</summary>
        public TournamentCategory EffectiveCategory => Category ?? TournamentCategory.Sponsor;

        /// <summary>The division split the game reasons with: served, else the backfilled
        /// <see cref="TournamentDivisionType.Level"/>.</summary>
        public TournamentDivisionType EffectiveDivisionType => DivisionType ?? TournamentDivisionType.Level;

        /// <summary>True when at least one rule could refuse an entry. False for every CSV row,
        /// and for a server row the dashboard left unrestricted.</summary>
        public bool HasEntryRestrictions
            => CharRarityMin != null || CharRarityMax != null
            || CharLevelMin  != null || CharLevelMax  != null
            || (ClubRarityMax != null && EffectiveGearRule == TournamentGearRule.Own);

        public TournamentDefinition(
            string id,
            string nameKey,
            string clubId,
            IReadOnlyList<string> holeSet,
            DateTime startUtc,
            DateTime endUtc,
            int resolveDelayMinutes,
            long entryFeeRP,
            string prizeTableId,
            string botFieldId,
            string sponsorKey,
            string leagueKey,
            // Appended and optional so every existing positional call site — the CSV loader and
            // every test fixture — compiles untouched.
            string? title = null,
            string? bannerUrl = null,
            string? titleJa = null,
            string? descriptionEn = null,
            string? descriptionJa = null,
            string? descriptionKey = null,
            string? modalBannerImageUrlEn = null,
            string? modalBannerImageUrlJa = null,
            string? modalBannerLinkUrl = null,
            // Restrictions arrive as the RAW wire strings/ints and are normalised HERE rather than
            // at each call site, so the CSV loader, the mapper and every test fixture get the same
            // degrade-never-throw treatment: an unknown enum or rarity name becomes null, i.e.
            // unrestricted, and can never reach the eligibility gate in a shape it cannot read.
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
        {
            Id                   = id;
            NameKey              = nameKey;
            ClubId               = clubId;
            HoleSet              = holeSet;
            StartUtc             = startUtc;
            EndUtc               = endUtc;
            ResolveDelayMinutes  = resolveDelayMinutes;
            EntryFeeRP           = entryFeeRP;
            PrizeTableId         = prizeTableId;
            BotFieldId           = botFieldId;
            SponsorKey           = sponsorKey;
            LeagueKey            = leagueKey;
            Title                = title;
            BannerUrl            = bannerUrl;
            TitleJa              = titleJa;
            DescriptionEn        = descriptionEn;
            DescriptionJa        = descriptionJa;
            DescriptionKey       = descriptionKey;
            ModalBannerImageUrlEn = modalBannerImageUrlEn;
            ModalBannerImageUrlJa = modalBannerImageUrlJa;
            ModalBannerLinkUrl    = modalBannerLinkUrl;

            Category            = TournamentRestrictions.ParseCategory(category);
            MaxPlayers          = TournamentRestrictions.PositiveOrNull(maxPlayers);
            PlayersPerDivision  = TournamentRestrictions.PositiveOrNull(playersPerDivision);
            DivisionType        = TournamentRestrictions.ParseDivisionType(divisionType);
            CharRarityMin       = TournamentRestrictions.CanonicalRarity(charRarityMin);
            CharRarityMax       = TournamentRestrictions.CanonicalRarity(charRarityMax);
            CharLevelMin        = TournamentRestrictions.PositiveOrNull(charLevelMin);
            CharLevelMax        = TournamentRestrictions.PositiveOrNull(charLevelMax);
            GearRule            = TournamentRestrictions.ParseGearRule(gearRule);
            ClubRarityMax       = TournamentRestrictions.CanonicalRarity(clubRarityMax);
        }
    }
}
