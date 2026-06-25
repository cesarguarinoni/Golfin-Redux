// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — StubTournamentBackend
// Compile-gate stub. Returns empty/fixed data so T2–T4 and UI can compile
// against the frozen ITournamentBackend shape before LocalTournamentBackend (T4)
// exists. SPEC §5: no game logic here.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;

namespace Golfin.Tournaments
{
    /// <summary>
    /// In-memory stub that implements <see cref="ITournamentBackend"/> with
    /// empty/fixed return values. Used as a compile-gate so downstream tasks
    /// (T2 CSV loaders, T3 bot field, T4 backend, T5 save, UI) can be built
    /// against the frozen contract before <c>LocalTournamentBackend</c> (T4) exists.
    /// <para>
    /// Contains <b>no game logic</b>. Every method returns a safe, non-null value
    /// that exercises the full type surface. Replace with
    /// <c>LocalTournamentBackend</c> at T4.
    /// </para>
    /// </summary>
    public sealed class StubTournamentBackend : ITournamentBackend
    {
        // ── Pre-built fixed data ──────────────────────────────────────────────

        private static readonly TournamentDefinition StubDefinition = new TournamentDefinition(
            id:           "stub_tournament",
            nameKey:      "TOURNAMENT_STUB_NAME",
            clubId:       "lomond",
            holeSet:      new[] { "h1", "h2", "h3", "h4", "h5", "h6", "h7", "h8", "h9" },
            startUtc:     DateTime.UtcNow.AddDays(-1),
            endUtc:       DateTime.UtcNow.AddDays(1),
            entryFeeRP:   0L,
            prizeTableId: "stub_prizes",
            botFieldId:   "stub_bots",
            sponsorKey:   "TOURNAMENT_STUB_SPONSOR",
            leagueKey:    "TOURNAMENT_STUB_LEAGUE"
        );

        private static readonly EntryState StubEntry = new EntryState(
            tournamentId: "stub_tournament",
            characterId:  "char_frodo",
            perHole:      new List<HoleResult>(),
            startedUtc:   DateTime.UtcNow,
            lastHoleUtc:  null,
            status:       EntryStatus.InProgress
        );

        private static readonly TournamentLeaderboardEntry StubLeaderboardEntry = new TournamentLeaderboardEntry
        {
            Rank         = 1,
            IsTie        = false,
            DisplayName  = "StubPlayer",
            CharacterId  = "char_frodo",
            Level        = 1,
            Strokes      = 36,
            Thru         = 9,
            TimeSeconds  = 600f,
            IsPlayer     = true,
            IsDNF        = false,
            IsProvisional = true,
        };

        private static readonly TournamentResult StubResult = new TournamentResult(
            finalRank:    1,
            isTie:        false,
            prizeRP:      1000L,
            itemRewardId: null,
            claimed:      false
        );

        // ── ITournamentBackend ────────────────────────────────────────────────

        /// <inheritdoc/>
        public IReadOnlyList<TournamentDefinition> GetTournaments()
            => new[] { StubDefinition };

        /// <inheritdoc/>
        public TournamentDefinition GetTournament(string id)
            => StubDefinition;

        /// <inheritdoc/>
        public EntryState Register(string id, long entryPaymentRP, string characterId)
            => StubEntry;

        /// <inheritdoc/>
        public EntryState? GetMyEntry(string id)
            => StubEntry;

        /// <inheritdoc/>
        public EntryState SubmitHoleResult(string id, HoleResult result)
            => StubEntry;

        /// <inheritdoc/>
        public IReadOnlyList<TournamentLeaderboardEntry> GetLeaderboard(string id)
            => new[] { StubLeaderboardEntry };

        /// <inheritdoc/>
        public TournamentResult? GetResults(string id)
            => StubResult;

        /// <inheritdoc/>
        public void ClaimPrize(string id)
        {
            // Stub: no-op. LocalTournamentBackend (T4) implements the real grant.
        }
    }
}
