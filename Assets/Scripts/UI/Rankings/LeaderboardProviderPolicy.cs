// ─────────────────────────────────────────────────────────────────────────────
// UI/Rankings — LeaderboardProviderPolicy
//
// The one rule that decides whether this session reads the real board or the
// local fakes, extracted from LeaderboardManager so it is a pure function that an
// EditMode test can exercise without a scene (same split as BannerPolicy next to
// BannerService, and ServerBalanceSync next to ServerBalanceSyncBehaviour).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.UI.Rankings
{
    /// <summary>Which <see cref="ILeaderboardProvider"/> a session should run on.</summary>
    public enum LeaderboardProviderKind
    {
        /// <summary><c>LocalFakeLeaderboardProvider</c> — client-side fakes + the SaveData accumulators.</summary>
        LocalFake,
        /// <summary><c>BackendLeaderboardProvider</c> — the server board every player shares.</summary>
        Backend
    }

    public static class LeaderboardProviderPolicy
    {
        /// <summary>
        /// leaderboard_backend SPEC §4.
        ///
        /// <para><b>A bot run NEVER reaches the backend.</b> <c>BotSessionOverride</c> installs a fake
        /// local identity whose token is the literal string
        /// <c>BOT_SESSION_OVERRIDE_NOT_A_REAL_TOKEN</c> — so <paramref name="signedIn"/> is TRUE during
        /// a bot run and an auth check alone would happily aim requests at production with a token the
        /// server will reject. The override is checked FIRST for exactly that reason: bots are offline
        /// by design, and the deterministic local board is what their captures are supposed to show.</para>
        ///
        /// <para>A signed-out player also stays on the fakes: every leaderboard endpoint requires a
        /// bearer token, so the backend has nothing to tell them.</para>
        /// </summary>
        public static LeaderboardProviderKind Choose(bool botSessionOverrideActive, bool signedIn)
        {
            if (botSessionOverrideActive) return LeaderboardProviderKind.LocalFake;
            return signedIn ? LeaderboardProviderKind.Backend : LeaderboardProviderKind.LocalFake;
        }
    }
}
