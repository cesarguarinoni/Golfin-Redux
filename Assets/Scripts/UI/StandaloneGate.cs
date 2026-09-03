// ─────────────────────────────────────────────────────────────────────────────
// gps_standalone_shell §D4 — the shell's reachability gate.
// PLAYLIFE as a Unity thin-shell from THIS project: one codebase, a third variant.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Golfin.Gps.UI;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Compile-time gate for the "punch it standalone" variant — the PLAYLIFE shell. Decides
    /// REACHABILITY only: nothing is stripped, no asmdef carries a defineConstraint, the golf
    /// code still compiles. What makes the shell small is the scene list (ShellScene only) plus
    /// IL2CPP stripping, not this file.
    ///
    /// THREE BUILDS, ONE CODEBASE (with <see cref="GpsGate"/> and <c>DemoGate</c>)
    ///   "punch it"            → iOS-Full       → no define        → the game, GPS gated off.
    ///   "punch it GPS"        → iOS-Full-GPS   → GOLFIN_GPS       → the game + the GPS surface.
    ///   "punch it standalone" → iOS-Standalone → +GOLFIN_STANDALONE → the GPS surface ONLY.
    ///
    /// AN ALLOWLIST, LIKE <c>DemoGate</c> AND UNLIKE <see cref="GpsGate"/>. The shell's product
    /// definition is "PLAYLIFE features only", so a screen added later must be listed to be
    /// reachable — a new gacha/tournament/shop screen must NOT quietly appear in the PLAYLIFE
    /// app because someone forgot this file existed. The failure is loud (a refusal with a log
    /// line) rather than silent (golf content in a golf-free product).
    ///
    /// THE EDITOR IS ALWAYS OFF here, the exact inverse of <see cref="GpsGate"/>'s always-on.
    /// Cesar develops the GAME daily; a gate that turned itself on in the Editor would delete
    /// Home, the nav bar and every golf screen out from under him. So there is no
    /// <c>|| UNITY_EDITOR</c>, and the enabled branch is reached in the Editor only by the
    /// two-arg overloads below (how the tests reach it) or by temporarily adding
    /// GOLFIN_STANDALONE to the global player defines — build-profile defines do not reach
    /// editor compilation (see <c>Docs/Specs/Active/gps_standalone_shell/IMPLEMENTER_REPORT.md</c>).
    ///
    /// HOME IS REWRITTEN, NOT REFUSED (§D4). <see cref="ScreenId.Home"/> does not exist in the
    /// shell, but a dozen call sites land on it as their "sane default" — the Welcome tutorial's
    /// SKIP, the hub's BackPill fallback, every <c>GoBack</c> whose history ran dry. Refusing
    /// those would strand the player on the screen they were leaving; rewriting them to
    /// <see cref="ScreenId.GpsHub"/> makes the shell's root the thing the game's root meant.
    /// </summary>
    public static class StandaloneGate
    {
#if GOLFIN_STANDALONE
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif

        /// <summary>
        /// The PLAYLIFE surface: the pre-auth boot/account screens plus every GPS screen.
        ///
        /// <para>Pre-auth is deliberately spelled out here rather than delegated to
        /// <c>AuthGate</c>: that class answers "may this open with no session", which is a
        /// different question from "does this screen exist in this product", and reusing it
        /// would silently widen this list the day a post-auth screen joined its allowlist. The
        /// two lists agreeing today is asserted by <c>StandaloneGateTests</c>.</para>
        /// </summary>
        static readonly HashSet<ScreenId> PreAuthScreens = new HashSet<ScreenId>
        {
            ScreenId.Logo,
            ScreenId.Splash,
            ScreenId.Loading,
            ScreenId.Login,
            ScreenId.SignUp,
            ScreenId.EmailConfirmation,
            ScreenId.CreateUsername,
            ScreenId.ResetPassword,
        };

        /// <summary>
        /// Membership of the shell's surface, independent of <see cref="Enabled"/>. GPS screens
        /// are read from <see cref="GpsGate.IsGpsScreen"/> rather than copied, so a GPS screen
        /// added later is in the shell the moment it is on the GPS list — one source of truth,
        /// the same argument that put ScreenManager's chrome rule on <c>GpsGate</c>.
        /// </summary>
        public static bool IsShellScreen(ScreenId id)
            => PreAuthScreens.Contains(id) || GpsGate.IsGpsScreen(id);

        /// <summary>
        /// The screen this navigation should actually land on. <see cref="ScreenId.Home"/> →
        /// <see cref="ScreenId.GpsHub"/> in the shell; everything else unchanged, in every build.
        /// </summary>
        public static ScreenId Rewrite(ScreenId id) => Rewrite(id, Enabled);

        /// <summary>Testable core — the build state is a parameter for the same reason
        /// <see cref="GpsGate.IsScreenAllowed(ScreenId, bool)"/> takes one.</summary>
        internal static ScreenId Rewrite(ScreenId id, bool standalone)
            => standalone && id == ScreenId.Home ? ScreenId.GpsHub : id;

        /// <summary>
        /// May this screen open in THIS build? Always true outside the shell variant, so this
        /// costs one branch in the game. Call it with the id AFTER <see cref="Rewrite"/>.
        /// </summary>
        public static bool IsScreenAllowed(ScreenId id) => IsScreenAllowed(id, Enabled);

        /// <summary>Testable core. See <see cref="Rewrite(ScreenId, bool)"/>.</summary>
        internal static bool IsScreenAllowed(ScreenId id, bool standalone)
            => !standalone || IsShellScreen(Rewrite(id, standalone));
    }
}
