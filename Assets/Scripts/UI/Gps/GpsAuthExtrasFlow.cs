// auth_golf_profile §2 — the once-per-device offer that puts the Golf Profile capture in front
// of a newly signed-in player, and the flag that stops it happening twice.
#nullable enable
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
    /// WHY THE TRIGGER LIVES ON HOME AND NOT ON THE AUTH SCREENS. There are four ways to arrive
    /// authenticated — password sign-in, sign-up, an OAuth deep link, and a restored session on a
    /// cold start — and three different controllers route out of them
    /// (<c>LoginScreenController</c>, <c>SignUpScreenController</c>,
    /// <c>ResetPasswordScreenController</c>), each choosing between
    /// <c>StartingCharacterSelection</c>, <c>CreateUsername</c> and <c>Home</c>. Every one of those
    /// paths ends at Home. Hooking Home therefore covers all four with one branch and cannot be
    /// bypassed by a route that gets added later.
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

        /// <summary>Editor/QA seam: forget the answer so the next Home entry offers again.</summary>
        public static void ClearPrompted()
        {
            PlayerPrefs.DeleteKey(PromptedKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Should Home hand off to the Golf Profile capture right now? Reads the live gate, the
        /// live session and the live flag.
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
    }
}
