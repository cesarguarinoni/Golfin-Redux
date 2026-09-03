// gps_standalone_shell §D3 — where the PLAYLIFE shell lands once the player is signed in.
#nullable enable
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>
    /// The shell's post-auth destination, in ONE place.
    ///
    /// <para>
    /// Four call sites route a freshly authenticated player in this project — the Splash's
    /// returning-user path, <c>LoginScreenController</c>, <c>CreateUsernameScreenController</c>
    /// and <c>SignUpScreenController</c>'s OAuth callback — and all four do the same two things:
    /// resolve <c>StarterGate</c> (a golf-inventory round trip) and then choose between
    /// <see cref="ScreenId.StartingCharacterSelection"/> and <see cref="ScreenId.Home"/>.
    /// </para>
    /// <para>
    /// In the shell BOTH of those are wrong, and wrong in a way that dead-ends rather than
    /// degrades. There is no roster and no starter picker: <see cref="StandaloneGate"/> refuses
    /// <see cref="ScreenId.StartingCharacterSelection"/>, so a first-run account would sit on the
    /// account screen with a button that had stopped being busy and nothing else happening. And
    /// the gate that decides it — "does this account own a starter?" — is a question about the
    /// GAME that a PLAYLIFE-only product must never make a player wait on, let alone be blocked
    /// by when it fails (<c>StarterRoute.ServerUnreachable</c> deliberately refuses to route).
    /// </para>
    /// <para>
    /// So this is a SHORT-CIRCUIT taken BEFORE <c>StarterGate.Resolve</c>, not a branch inside
    /// its callback: the point is that the request is never issued. The starter still exists
    /// server-side for an account that also plays the game — the shell simply never looks.
    /// </para>
    /// <para>
    /// IT NAMES THE HUB AND NOTHING ELSE. It used to resolve the Golf Profile offer here, through
    /// <c>GpsAuthExtrasFlow.InterceptHubEntry</c> — which looked like honesty (the boot saying
    /// where it was really going) and became a bug the moment the offer turned into a once-per-
    /// ACCOUNT decision (gps_profile_prompt_server_flag): that decision needs <c>/user/detail</c>,
    /// and the bounded wait for it lives in <c>ScreenManager.Navigate</c>. Deciding here jumped
    /// over the wait and re-offered the capture on a fresh install of an account that had already
    /// answered in the game — exactly the defect this was supposed to fix. Caught by the round-2
    /// first-run proof, not by a review.
    /// </para>
    /// <para>
    /// So <c>Navigate</c> stays the single choke point: it resolves the account flag, applies the
    /// gates, and applies the intercept — for the Home pill, for the banner deep link, and now for
    /// this boot.
    /// </para>
    /// </summary>
    public static class StandaloneShellBoot
    {
        /// <summary>
        /// True in the PLAYLIFE shell, with <paramref name="target"/> set to <see cref="ScreenId.GpsHub"/>
        /// — always. False in every other build, leaving the caller's own starter-gated routing
        /// untouched.
        ///
        /// <para>The caller passes the result to <c>ShowScreen</c>, so the hub entry goes through
        /// <c>Navigate</c> like any other and picks up the account check and the intercept there.
        /// See the class docs for why this must not decide the offer itself.</para>
        /// </summary>
        public static bool TryGetPostAuthScreen(out ScreenId target)
        {
            target = ScreenId.GpsHub;
            if (!StandaloneGate.Enabled) return false;

            Debug.Log("[StandaloneShellBoot] PLAYLIFE shell — StarterGate skipped, routing to GpsHub " +
                      "(the Golf Profile offer is decided in Navigate, once the account flag resolves).");
            return true;
        }
    }
}
