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

        /// <summary>True once the player has answered the Golf Profile screen on this device.</summary>
        public static bool Prompted => PlayerPrefs.HasKey(PromptedKey);

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
        /// the live session and the live flag.
        /// </summary>
        public static bool ShouldOffer()
            => ShouldOffer(GpsGate.Enabled,
                           Golfin.Auth.AuthService.Instance != null
                           && Golfin.Auth.AuthService.Instance.Session.IsAuthenticated,
                           Prompted);

        /// <summary>
        /// Testable core. All three inputs are parameters so an EditMode test can exercise the
        /// "punch it" build (<paramref name="gpsEnabled"/> false → always a no-op) without the
        /// Editor's always-on <see cref="GpsGate.Enabled"/> hiding that branch.
        ///
        /// <para>
        /// SIGNED IN, not merely "has a name": the screen writes to the caller's own
        /// <c>profiles</c> row over a bearer token, so with no session there is nothing to write
        /// to and the offer would end in a 403.
        /// </para>
        /// </summary>
        internal static bool ShouldOffer(bool gpsEnabled, bool signedIn, bool alreadyPrompted)
            => gpsEnabled && signedIn && !alreadyPrompted;

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
