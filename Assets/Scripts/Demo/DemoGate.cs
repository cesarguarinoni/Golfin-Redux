using System.Collections.Generic;
using GolfinRedux.UI;

namespace GolfinRedux.Demo
{
    /// <summary>
    /// Compile-time demo gate (demo_build_slice §3.2). When GOLFIN_DEMO is defined
    /// — via the iOS-Demo / Android-Demo build profiles — only the allowlisted
    /// screens may open. Everything else is denied by default: a screen added later
    /// is locked until it is explicitly listed here, so the failure mode is loud
    /// (a screen refuses to open) instead of silent (full-game content ships).
    /// </summary>
    public static class DemoGate
    {
#if GOLFIN_DEMO
        public const bool IsDemo = true;
#else
        public const bool IsDemo = false;
#endif

        // ALLOWLIST — deny by default. A new screen is locked until listed here.
        static readonly HashSet<ScreenId> Allowed = new HashSet<ScreenId>
        {
            ScreenId.Logo,
            ScreenId.Splash,
            ScreenId.Loading,
            ScreenId.Home,
        };

        /// <summary>
        /// True when the screen may be shown. Always true outside a demo build, so
        /// this is a no-op that costs one branch in the full game.
        /// </summary>
        public static bool IsScreenAllowed(ScreenId id) => !IsDemo || Allowed.Contains(id);

        /// <summary>
        /// Raw allowlist membership, independent of IsDemo. Used by the build-time
        /// DemoSceneProcessor, which must decide what to strip based on the BUILD's demo
        /// state — not the editor's own compiled define (the editor's active build profile
        /// can differ from the profile being built). Runtime callers want IsScreenAllowed.
        /// </summary>
        public static bool IsOnAllowlist(ScreenId id) => Allowed.Contains(id);
    }
}
