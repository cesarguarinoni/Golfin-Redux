// ─────────────────────────────────────────────────────────────────────────────
// home_carousel_and_daily_timing — forget the daily on an ACCOUNT SWITCH.
//
// Two "last answer" statics describe today's daily: DailyMissionState (the shared
// fact the Home pill and the Mission Selection card agree on) and
// MissionsClient.LastDaily (the full answer the card paints from in its opening
// frame). Neither had ever been cleared on sign-in: the recipe is per-day and
// global, so a second account saw the right mission — but the streak and the
// claimed flag inside both belong to the player who fetched them, and for the
// second or so until that account's own fetch landed, they were the previous
// player's.
//
// KEYED ON THE USER ID, NOT ON THE EVENT. AuthService.SignedIn also fires on a
// token REFRESH ("establishes an authenticated session" includes it, by its own
// doc), and every other subscriber in the project re-FETCHES on it for that
// reason. Clearing on the bare event would blank the Home pill mid-session on
// every refresh — DailyMissionState.Clear() raises OnChanged, the pill leaves,
// and nothing brings it back until Home is re-entered.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.Auth;
using Golfin.Gameplay.Missions;
using UnityEngine;

namespace Golfin.UI.Home
{
    /// <summary>
    /// Forgets both daily "last answer" statics when a <see cref="AuthService.SignedIn"/> session
    /// carries a different user than the one they were fetched for.
    /// </summary>
    public static class DailyMissionSessionReset
    {
        private static bool _installed;

        /// <summary>The user the current daily facts were fetched for; null before the first session.</summary>
        private static string? _userId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_installed) return;
            // Play mode only, same reason as TelemetryHooks.Install: an edit-mode call would set
            // _installed and silently block the real install on the next play.
            if (!Application.isPlaying) return;
            _installed = true;
            AuthService.SignedIn -= OnSignedIn;
            AuthService.SignedIn += OnSignedIn;
        }

        private static void OnSignedIn(AuthSession session) => Handle(session);

        /// <summary>
        /// The decision, separated from the subscription so a test can drive it with sessions.
        /// Returns true when the daily facts were forgotten.
        /// </summary>
        public static bool Handle(AuthSession session)
        {
            string? incoming = session?.UserId;
            if (string.IsNullOrEmpty(incoming)) return false;

            bool switched = !string.IsNullOrEmpty(_userId)
                            && !string.Equals(_userId, incoming, System.StringComparison.Ordinal);
            _userId = incoming;
            if (!switched) return false;

            Debug.Log("[DailyMissionSessionReset] account switched — forgetting the daily answer and state.");
            Golfin.Economy.MissionsClient.Instance.ForgetDaily();
            DailyMissionState.Clear();
            return true;
        }

        /// <summary>Test seam: forget which user the facts belong to (and the subscription flag).</summary>
        public static void ResetForTest() { _userId = null; _installed = false; }
    }
}
