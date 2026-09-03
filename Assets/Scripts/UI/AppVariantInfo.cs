// gps_standalone_shell §D6 — which of the three shipped variants this binary is.
#nullable enable

namespace GolfinRedux
{
    /// <summary>
    /// The variant label, resolved once from the compile-time defines that already decide the
    /// three builds. Telemetry stamps it on every event so one table can be split by app.
    ///
    /// <para>
    ///   "punch it"            → <see cref="Game"/>     — iOS-Full, no define.
    ///   "punch it GPS"        → <see cref="GameGps"/>  — iOS-Full-GPS, GOLFIN_GPS.
    ///   "punch it standalone" → <see cref="PlayLife"/> — iOS-Standalone, +GOLFIN_STANDALONE.
    /// </para>
    /// <para>
    /// GOLFIN_STANDALONE is tested FIRST because the shell profile defines both: the standalone
    /// IS a GPS build, and the narrower label is the informative one.
    /// </para>
    /// <para>
    /// <see cref="PlayLife"/> is deliberately the same string
    /// <c>Golfin.Gps.UnityClientPlatformProbe.IosPlayLife</c> sends as <c>client_platform</c>, so
    /// the telemetry rows and the activity/score rows name the app the same way. They are two
    /// constants because <c>Golfin.Gps</c> is an asmdef that cannot see this assembly; the define
    /// is what they genuinely share, not a reference.
    /// </para>
    /// </summary>
    public static class AppVariantInfo
    {
        public const string Game = "game";
        public const string GameGps = "game-gps";
        public const string PlayLife = "ios-playlife";

#if GOLFIN_STANDALONE
        public const string Current = PlayLife;
#elif GOLFIN_GPS
        public const string Current = GameGps;
#else
        public const string Current = Game;
#endif
    }
}
