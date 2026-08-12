// ─────────────────────────────────────────────────────────────────────────────
// points_cutover_followups item 3 — HARD sign-in gate. Decision of record
// (Cesar, 2026-08-12): there is NO guest mode.
// ─────────────────────────────────────────────────────────────────────────────
using Golfin.Auth;
using GolfinRedux.Demo;
using UnityEngine;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Deny-by-default screen gate: without a session you cannot get past the account screens.
    ///
    /// WHY A GATE AND NOT JUST A FIXED BUTTON. The hole this closes was the splash's
    /// <c>DevBypassCatcher_TEMP</c> — an invisible full-screen button that sent any stray tap
    /// straight to Home, no auth, and shipped in player builds. Deleting it closes the path that
    /// existed; this gate closes the CLASS. Since the RP cutover, a signed-out player who reaches
    /// mode select can spend nothing and earn nothing — every server debit 403s — so the honest
    /// place to stop them is the door, not four separate spend flows.
    ///
    /// Deliberately modelled on <see cref="DemoGate"/>, hooked into the same
    /// <c>ScreenManager.ShowScreen</c> seam, with the same deny-by-default posture: a screen added
    /// later is behind the gate until someone lists it as pre-auth. The failure mode is loud (a
    /// screen refuses to open, with a log line) rather than silent (paid content reachable signed out).
    ///
    /// Three ways through, all explicit:
    ///   • a real session (<c>AuthService.Session.IsAuthenticated</c>);
    ///   • a GOLFIN_DEMO build — the offline demo is a guest product by design (demo_build_slice §3.4)
    ///     and has its own, much narrower, DemoGate allowlist;
    ///   • the editor-only bot override (points_cutover_followups item 1).
    /// </summary>
    public static class AuthGate
    {
        /// <summary>
        /// Screens reachable with no session: the boot sequence and the account flow itself.
        /// <c>CreateUsername</c> is listed because it is part of the sign-up flow and can be reached
        /// while the session is still settling; it grants access to nothing.
        /// </summary>
        private static bool IsPreAuthScreen(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Logo:
                case ScreenId.Splash:
                case ScreenId.Loading:
                case ScreenId.Login:
                case ScreenId.SignUp:
                case ScreenId.EmailConfirmation:
                case ScreenId.CreateUsername:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>True when the player (or an armed bot) may see post-auth content.</summary>
        public static bool HasSession
        {
            get
            {
                // Edit mode is not a player: the gate is a runtime concern, and consulting
                // AuthService here would self-bootstrap an [AuthService] GameObject into whatever
                // scene an editor tool happened to be driving — dirtying it. Never true in a build.
                if (!Application.isPlaying) return true;

                if (DemoGate.IsDemo) return true; // offline demo is a guest product by design

#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
                if (Golfin.Dev.BotSessionOverride.Active) return true;
#endif

                return AuthService.Instance.Session.IsAuthenticated;
            }
        }

        /// <summary>
        /// May this screen open right now? Pre-auth screens always; everything else only with a
        /// session.
        /// </summary>
        public static bool IsScreenAllowed(ScreenId id) => IsPreAuthScreen(id) || HasSession;
    }
}
