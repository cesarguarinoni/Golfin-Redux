// auth_golf_profile §2 — the once-per-device offer that puts the Golf Profile capture in front
// of a newly signed-in player, and the flag that stops it happening twice.
//
// gps_profile_prompt_on_entry — the TRIGGER moved. It used to fire on the first Home entry; it now
// fires on the first entry into the GPS surface. See the class docs.
#nullable enable
using GolfinRedux.UI;
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Decides whether the post-signup Golf Profile capture should be offered, and records that
    /// it was answered.
    ///
    /// <para>
    /// A STATIC HELPER RATHER THAN LOGIC INSIDE <c>HomeScreenController</c> for one reason: the
    /// decision has to be testable. <see cref="GpsGate.Enabled"/> is a compile-time const that is
    /// <c>true</c> in the Editor, so the "punch it" branch is unreachable from an EditMode test
    /// unless the build state is a parameter — exactly the seam
    /// <see cref="GpsGate.IsScreenAllowed(GolfinRedux.UI.ScreenId, bool)"/> already carries, and
    /// <see cref="ShouldOffer(bool, bool, bool)"/> mirrors it.
    /// </para>
    /// <para>
    /// WHY THE TRIGGER IS THE GPS ENTRY AND NOT HOME (gps_profile_prompt_on_entry, device-pass
    /// finding #2). A fresh install must land on the GAME and stay there — the first thing a new
    /// player saw used to be a golf-profile form for a surface they had not asked for. The offer
    /// now sits on the boundary it belongs to: the first navigation INTO
    /// <see cref="ScreenId.GpsHub"/>, wherever it comes from — the Home pill, the home_promo
    /// banner's <c>golfin://gps</c> internal route, or (later) the standalone shell's boot.
    /// </para>
    /// <para>
    /// Hooking <c>ScreenManager.Navigate</c> rather than any one caller is what makes that
    /// "wherever it comes from" true: every forward navigation in the game funnels through it, so
    /// a GPS entry point added later is covered without being told about this flow. That is the
    /// same argument that used to justify hooking Home — one choke point no route can bypass —
    /// applied to the right choke point.
    /// </para>
    /// </summary>
    public static class GpsAuthExtrasFlow
    {
        /// <summary>
        /// PlayerPrefs key. Set when the player ANSWERS the screen (SAVE succeeded, or "Skip for
        /// now"), never when it is merely shown — so a player who kills the app while looking at
        /// it is offered once more rather than losing the screen forever. Per device, which is
        /// what PlayerPrefs is: a second account on the same phone is not re-offered, and Cesar
        /// accepted that in the SPEC (§2, "existing accounts get one offer too").
        /// </summary>
        public const string PromptedKey = "gps_profile_prompted";

        /// <summary>True once the player has answered the Golf Profile screen on THIS DEVICE, in
        /// THIS app. A cache in front of <see cref="PromptedOnAccount"/>, not the truth.</summary>
        public static bool PromptedLocally => PlayerPrefs.HasKey(PromptedKey);

        /// <summary>
        /// gps_profile_prompt_server_flag — the account-wide answer.
        ///
        /// <para>
        /// <c>profiles.golf_profile_prompted_at</c> is stamped when the screen is ANSWERED — saved
        /// or skipped — in any app, on any device. Cesar, 2026-09-03: "if I log in for the first
        /// time to GPS but had already logged in from Game and selected my user/colour, that
        /// screen should be skipped (and vice versa)." With the standalone GOLFIN GPS app shipping
        /// from this same codebase, one account routinely has two installs on one phone, and the
        /// PlayerPrefs flag — per device AND per app — asked the second one all over again.
        /// </para>
        /// <para>
        /// The local flag survives as a CACHE: it is what makes the common case (this device has
        /// already answered) cost zero round trips. The server flag is what makes the answer
        /// travel. Either one being set means answered — never both, never only the server.
        /// </para>
        /// <para>
        /// Only null-vs-not is read. The timestamp is never parsed; see
        /// <c>UserDetailDto.GolfProfilePromptedAt</c> for why it is carried as a string.
        /// </para>
        /// </summary>
        public static bool PromptedOnAccount
        {
            get
            {
                var detail = Golfin.Social.UserService.Instance?.LastDetail;
                return detail != null && !string.IsNullOrEmpty(detail.GolfProfilePromptedAt);
            }
        }

        /// <summary>True once the player has answered the screen, on this device or any other.</summary>
        public static bool Prompted => PromptedLocally || PromptedOnAccount;

        /// <summary>Record that the screen was answered. Idempotent.</summary>
        public static void MarkPrompted()
        {
            PlayerPrefs.SetInt(PromptedKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>Editor/QA seam: forget the answer so the next GPS entry offers again.</summary>
        public static void ClearPrompted()
        {
            PlayerPrefs.DeleteKey(PromptedKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Should a GPS entry hand off to the Golf Profile capture right now? Reads the live gate,
        /// the live session, the live local flag and the live server flag.
        ///
        /// <para>
        /// SIDE EFFECT, and the only one in this class outside <see cref="MarkPrompted"/>: when
        /// the SERVER says answered and this device does not know it yet, the local flag is
        /// written here (§5). That is the new-install case — the shell's first launch on a phone
        /// whose game already answered — and re-caching means the second launch decides for free
        /// instead of waiting on <c>/user/detail</c> again.
        /// </para>
        /// </summary>
        public static bool ShouldOffer()
        {
            bool signedIn = Golfin.Auth.AuthService.Instance != null
                            && Golfin.Auth.AuthService.Instance.Session.IsAuthenticated;

            // §5 — re-cache before deciding, so the write happens on the pass that LEARNS it.
            if (signedIn && PromptedOnAccount && !PromptedLocally)
            {
                Debug.Log("[GpsAuthExtrasFlow] server says this account already answered the Golf " +
                          "Profile — caching the local flag; this install will never offer it.");
                MarkPrompted();
            }

            return ShouldOffer(GpsGate.Enabled, signedIn, PromptedLocally, PromptedOnAccount);
        }

        /// <summary>
        /// Testable core. Every input is a parameter so an EditMode test can exercise the
        /// "punch it" build (<paramref name="gpsEnabled"/> false → always a no-op) without the
        /// Editor's always-on <see cref="GpsGate.Enabled"/> hiding that branch, and can walk the
        /// whole local × server truth table without PlayerPrefs or a live row.
        ///
        /// <para>
        /// SIGNED IN, not merely "has a name": the screen writes to the caller's own
        /// <c>profiles</c> row over a bearer token, so with no session there is nothing to write
        /// to and the offer would end in a 403.
        /// </para>
        /// <para>
        /// The two flags are OR'd, never AND'd. Either one being set means the account has
        /// answered: the local flag alone is the ordinary returning player (and the offline case,
        /// where the server flag is simply unknowable), the server flag alone is a fresh install
        /// of either app. Requiring both would re-ask every returning player the moment the
        /// network was down.
        /// </para>
        /// </summary>
        internal static bool ShouldOffer(bool gpsEnabled, bool signedIn,
                                         bool promptedLocally, bool promptedOnAccount)
            => gpsEnabled && signedIn && !promptedLocally && !promptedOnAccount;

        // ── gps_profile_prompt_server_flag §3 — the one round trip the decision may need ──

        /// <summary>
        /// How long a hub entry may be held waiting for <c>/user/detail</c> before it gives up and
        /// proceeds WITHOUT offering.
        ///
        /// <para>
        /// There has to be a bound. <c>ApiClient.TimeoutSeconds</c> is 30, and this sits in front
        /// of a navigation — on a bad network an unbounded wait would freeze the player on the
        /// Splash for half a minute with no spinner, which is far worse than the thing being
        /// prevented. 2.5 s is comfortably longer than the round trip actually takes (the same
        /// call the hub makes on every entry) and shorter than the Splash fade is annoying.
        /// </para>
        /// <para>
        /// Giving up resolves to "do not offer", never to "offer": the failure this exists to
        /// prevent is asking a player who already answered, and a missed offer costs one more
        /// chance on the next entry.
        /// </para>
        /// </summary>
        private const float AccountFlagBudgetSeconds = 2.5f;

        /// <summary>
        /// True when this navigation's offer decision genuinely cannot be made yet: it is a hub
        /// entry, the gate and the session say the offer is live, this device has no local flag —
        /// and <c>/user/detail</c> has not answered, so the account flag is unknown.
        ///
        /// <para>
        /// The local flag is checked FIRST and short-circuits, which is what keeps the common case
        /// (a returning player on a device that has already answered) free of any round trip at
        /// all. Only a device that has never answered can reach the fetch — i.e. a first launch,
        /// which is exactly the case this feature exists for.
        /// </para>
        /// </summary>
        public static bool NeedsAccountCheck(ScreenId requested)
        {
            if (requested != ScreenId.GpsHub) return false;
            if (!GpsGate.Enabled) return false;
            if (PromptedLocally) return false;
            if (Golfin.Auth.AuthService.Instance == null
                || !Golfin.Auth.AuthService.Instance.Session.IsAuthenticated) return false;

            var svc = Golfin.Social.UserService.Instance;
            return svc != null && svc.LastDetail == null && !svc.DetailAttempted;
        }

        /// <summary>
        /// Fetch <c>/user/detail</c> (once), then run <paramref name="then"/> — whatever the
        /// outcome, and never later than <see cref="AccountFlagBudgetSeconds"/>.
        ///
        /// <para>
        /// <paramref name="then"/> is invoked EXACTLY ONCE on every path, because it is a resumed
        /// navigation: dropping it strands the player on the screen they were leaving, and running
        /// it twice would push the same screen twice. <c>UserService.EnsureDetail</c> marks the
        /// attempt before it yields, so the resumed navigation cannot re-enter this wait.
        /// </para>
        /// </summary>
        public static void EnsureAccountFlagThen(System.Action then)
        {
            Golfin.Net.ApiClient.Instance.Run(EnsureAccountFlagRoutine(then));
        }

        private static System.Collections.IEnumerator EnsureAccountFlagRoutine(System.Action then)
        {
            bool answered = false;
            float startedAt = Time.realtimeSinceStartup;

            Golfin.Social.UserService.Instance.EnsureDetail(_ => answered = true);

            while (!answered && Time.realtimeSinceStartup - startedAt < AccountFlagBudgetSeconds)
                yield return null;

            if (answered)
                Debug.Log($"[GpsAuthExtrasFlow] account flag resolved in " +
                          $"{Time.realtimeSinceStartup - startedAt:0.00}s — prompted_at=" +
                          (PromptedOnAccount ? "set" : "null"));
            else
                Debug.Log($"[GpsAuthExtrasFlow] /user/detail did not answer within " +
                          $"{AccountFlagBudgetSeconds:0.0}s — continuing to the hub WITHOUT offering " +
                          $"(never nag; the next entry retries).");

            then?.Invoke();
        }

        /// <summary>
        /// gps_profile_prompt_on_entry §2 — set when an intercept diverts a hub entry into the
        /// capture, cleared on either exit of the Welcome tutorial. A one-shot marker of "the
        /// player is inside the post-signup chain because they asked for the hub", NOT an input to
        /// any decision here: the offer is still decided solely by <see cref="ShouldOffer()"/>.
        /// It is in-memory on purpose — a chain abandoned by force-quitting is not a chain still
        /// in flight on the next launch.
        /// </summary>
        public static bool PendingHubEntry { get; set; }

        /// <summary>
        /// The seam. Given the screen a caller asked for, return the screen it should actually
        /// navigate to: <see cref="ScreenId.GpsGolfProfile"/> when this is the first entry into the
        /// GPS hub and the offer applies, otherwise the id unchanged.
        ///
        /// <para>
        /// SIDE-EFFECT FREE, so it can be called from anywhere without ordering hazards and
        /// asserted in an EditMode test. The caller that acts on a diversion is the one that sets
        /// <see cref="PendingHubEntry"/> — <c>ScreenManager.Navigate</c> today, the standalone GPS
        /// shell's boot path later, both calling this same function rather than re-deriving the
        /// rule.
        /// </para>
        /// <para>
        /// THE HUB TEST COMES FIRST, and <see cref="ShouldOffer()"/> is only reached for a hub
        /// entry. This runs on EVERY forward navigation in the game, and ShouldOffer touches
        /// <c>AuthService.Instance</c>, whose getter CREATES the singleton — a DontDestroyOnLoad
        /// host, which throws outside play mode. Ordering the test this way keeps an ordinary
        /// screen change free of a session read, and keeps <c>NavBackMemoryTests</c> (which drives
        /// Navigate from EditMode) able to run at all.
        /// </para>
        /// </summary>
        public static ScreenId InterceptHubEntry(ScreenId requested)
            => requested == ScreenId.GpsHub && ShouldOffer() ? ScreenId.GpsGolfProfile : requested;

        /// <summary>
        /// Testable core, for the same reason <see cref="ShouldOffer(bool, bool, bool)"/> has one:
        /// the live decision is unreachable from an EditMode test in both directions.
        ///
        /// <para>
        /// ONLY <see cref="ScreenId.GpsHub"/> IS INTERCEPTED. Diverting any other GPS screen would
        /// break the chain itself — the capture and the tutorial are GPS screens, so a rule
        /// phrased as "any GPS screen" would bounce <see cref="ScreenId.GpsGolfProfile"/> back to
        /// itself forever.
        /// </para>
        /// </summary>
        internal static ScreenId InterceptHubEntry(ScreenId requested, bool offer)
            => offer && requested == ScreenId.GpsHub ? ScreenId.GpsGolfProfile : requested;
    }
}
