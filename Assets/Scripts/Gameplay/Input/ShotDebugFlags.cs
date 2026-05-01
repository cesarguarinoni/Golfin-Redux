namespace Golfin.Gameplay.Input
{
    public struct ShotDebugFlags
    {
        public bool ShowConeOutline;       // default true
        public bool ShowArrowTrail;        // default false (not yet implemented)
        public bool CancelOnSlowFlick;     // default true — slow lift cancels shot
        public bool SinglePassMode;        // default false — skip degradation system
        public bool DisableOverpower;      // default false — hard clamp at 1.0x
        public bool DisableConeFineTune;   // default false — aim is camera-only
        public bool ForcePerfectTiming;    // default false
        public bool ForcePerfectAim;       // default false
        public bool PuttPathHeatmap;       // default false — putter path color-codes ball speed

        public static ShotDebugFlags Defaults => new ShotDebugFlags
        {
            ShowConeOutline     = true,
            ShowArrowTrail      = false,
            CancelOnSlowFlick   = true,
            SinglePassMode      = false,
            DisableOverpower    = false,
            DisableConeFineTune = false,
            ForcePerfectTiming  = false,
            ForcePerfectAim     = false,
            PuttPathHeatmap     = false,
        };
    }
}
