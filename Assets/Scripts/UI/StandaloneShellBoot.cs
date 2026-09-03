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
    /// It resolves through <see cref="Golfin.Gps.UI.GpsAuthExtrasFlow.InterceptHubEntry"/>, the
    /// same seam <c>ScreenManager.Navigate</c> uses, so the once-per-device Golf Profile capture
    /// is offered on a first run here exactly as it is when the game's Home pill opens the hub.
    /// </para>
    /// </summary>
    public static class StandaloneShellBoot
    {
        /// <summary>
        /// True in the PLAYLIFE shell, with <paramref name="target"/> set to the screen the
        /// player should land on — the hub, or the Golf Profile capture on a first run. False in
        /// every other build, leaving the caller's own starter-gated routing untouched.
        ///
        /// <para>Reads the live once-per-device flag and the live session, so it must only be
        /// called at runtime and only once the session is real — i.e. exactly where the four
        /// callers already were.</para>
        /// </summary>
        public static bool TryGetPostAuthScreen(out ScreenId target)
        {
            target = ScreenId.GpsHub;
            if (!StandaloneGate.Enabled) return false;

            target = Golfin.Gps.UI.GpsAuthExtrasFlow.InterceptHubEntry(ScreenId.GpsHub);

            // The same bookkeeping ScreenManager.Navigate does when IT diverts a hub entry: the
            // marker tells the Welcome tutorial it is closing a post-signup chain the player
            // started by asking for the hub. Set here because the intercept has already been
            // resolved by the time Navigate sees the screen.
            if (target != ScreenId.GpsHub)
                Golfin.Gps.UI.GpsAuthExtrasFlow.PendingHubEntry = true;

            Debug.Log($"[StandaloneShellBoot] PLAYLIFE shell — StarterGate skipped, routing to {target}.");
            return true;
        }
    }
}
