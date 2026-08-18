// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentBackendPolicy
//
// The one rule that decides whether this session plays the shared server board or
// the deterministic local sim, extracted from TournamentService so it is a pure
// function an EditMode test can exercise without a scene.
//
// Same split, same shape and the same reasoning as LeaderboardProviderPolicy next
// to BackendLeaderboardProvider (leaderboard_backend §4) — deliberately, so the
// two cutovers cannot drift apart on who is allowed to reach production.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.Tournaments
{
    /// <summary>Which <see cref="ITournamentBackend"/> a session should run on.</summary>
    public enum TournamentBackendKind
    {
        /// <summary><c>LocalTournamentBackend</c> — bundled/cached schedule, deterministic bot sim,
        /// local entry store. The offline and bot-capture path.</summary>
        Local,

        /// <summary><c>RemoteTournamentBackend</c> — server entry, server board, offline submit queue.</summary>
        Remote
    }

    public static class TournamentBackendPolicy
    {
        /// <summary>
        /// tournament_async_board SPEC §3 (provider selection), mirroring
        /// <c>LeaderboardProviderPolicy.Choose</c>.
        ///
        /// <para><b>A bot run NEVER reaches the backend.</b> <c>BotSessionOverride</c> installs a
        /// fake local identity whose token is the literal string
        /// <c>BOT_SESSION_OVERRIDE_NOT_A_REAL_TOKEN</c> — so <paramref name="signedIn"/> is TRUE
        /// during a bot run and an auth check alone would happily aim entry POSTs at production
        /// with a token the server rejects, AND pollute a live tournament's human-entry count (which
        /// is what retires the bot field, one-way). The override is checked FIRST for exactly that
        /// reason: bots are offline by design, and the deterministic local board is what their
        /// captures are supposed to show.</para>
        ///
        /// <para><b>Demo builds stay local too.</b> A demo build has no sign-in flow to speak of and
        /// must never write into a live tournament; <c>DemoGate.IsDemo</c> is a compile-time const,
        /// so this branch folds away entirely in the full game.</para>
        ///
        /// <para>A signed-out player also stays local: all four tournament endpoints require a
        /// bearer token, so the server has nothing to tell them.</para>
        /// </summary>
        public static TournamentBackendKind Choose(bool botSessionOverrideActive, bool signedIn, bool isDemo)
        {
            if (botSessionOverrideActive) return TournamentBackendKind.Local;
            if (isDemo)                   return TournamentBackendKind.Local;
            return signedIn ? TournamentBackendKind.Remote : TournamentBackendKind.Local;
        }
    }
}
