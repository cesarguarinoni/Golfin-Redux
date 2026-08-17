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
            string? titleJa = null)
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
        }
    }
}
