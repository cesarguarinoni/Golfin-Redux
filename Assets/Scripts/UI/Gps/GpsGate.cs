using System.Collections.Generic;
using GolfinRedux.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Compile-time GPS gate (punch_it_gps_variants). Decides REACHABILITY only — the GPS code
    /// itself compiles and ships in both variants. Nothing is stripped, no scene is processed,
    /// no asmdef carries a defineConstraint.
    ///
    /// TWO BUILDS, ONE CODEBASE
    ///   "punch it"     → iOS-Full      → no GOLFIN_GPS → Enabled=false → GPS screens refuse to
    ///                                    open and the Home banner that routes into them hides.
    ///   "punch it GPS" → iOS-Full-GPS  → GOLFIN_GPS    → Enabled=true  → full GPS surface.
    ///
    /// THE EDITOR IS ALWAYS ON. Cesar develops GPS daily; if this const tracked the active build
    /// profile, opening a GPS screen in the Editor would depend on which profile happened to be
    /// selected — and the surface would silently vanish mid-session. UNITY_EDITOR wins here, so
    /// the disabled branch is only ever reached in a player build (and by the two-arg overload
    /// below, which is how tests reach it at all).
    ///
    /// INVERTED vs <see cref="GolfinRedux.Demo.DemoGate"/> — deliberately.
    ///   DemoGate is an ALLOWLIST: an unlisted screen is denied, so a new screen fails loudly.
    ///   GpsGate is a DENY-LIST: an unlisted screen is allowed, because the full game is the
    ///   default and only the GPS surface is conditional.
    ///
    /// ⚠️ CONSEQUENCE OF THE DENY-LIST: A NEW GPS SCREEN MUST BE ADDED TO <see cref="GpsScreens"/>
    /// OR IT SHIPS REACHABLE IN "punch it" BUILDS. The failure is silent — the screen simply
    /// works in a build that is supposed to have no GPS in it. This list is the single source of
    /// truth: ScreenManager's top-bar/nav rule reads <see cref="IsGpsScreen"/> rather than
    /// keeping its own copy, so the two cannot drift apart.
    /// </summary>
    public static class GpsGate
    {
#if GOLFIN_GPS || UNITY_EDITOR
        public const bool Enabled = true;
#else
        public const bool Enabled = false;
#endif

        // THE GPS surface. Add new GPS screens here — see the warning above.
        static readonly HashSet<ScreenId> GpsScreens = new HashSet<ScreenId>
        {
            ScreenId.GpsHub,
            ScreenId.ScoreUpload,
            ScreenId.GpsProfile,
            ScreenId.GpsAvatar,
            ScreenId.GpsBadges,
            // auth_golf_profile — the post-signup capture + welcome tutorial. On the list for the
            // same two reasons as the rest: they are GPS surface (top-bar-only chrome), and in a
            // "punch it" build they must not be reachable — which also makes HomeScreenController's
            // first-entry trigger a no-op there, since it tests GpsGate.Enabled before offering.
            ScreenId.GpsGolfProfile,
            ScreenId.GpsWelcome,
        };

        /// <summary>Membership of the GPS surface, independent of <see cref="Enabled"/>. Used by
        /// ScreenManager's shared-chrome rule, which is about what a screen IS, not whether the
        /// build allows it.</summary>
        public static bool IsGpsScreen(ScreenId id) => GpsScreens.Contains(id);

        /// <summary>True when the screen may be shown in THIS build. Always true outside the GPS
        /// surface, so this costs one branch everywhere else.</summary>
        public static bool IsScreenAllowed(ScreenId id) => IsScreenAllowed(id, Enabled);

        /// <summary>
        /// Testable core. The const above is <c>true</c> in the Editor, which makes the disabled
        /// branch unreachable from an EditMode test — so the build state is a parameter here and
        /// tests pass <c>false</c> explicitly to exercise what a "punch it" player build does.
        /// </summary>
        internal static bool IsScreenAllowed(ScreenId id, bool gpsEnabled)
            => gpsEnabled || !GpsScreens.Contains(id);
    }
}
