using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    public enum PresetScene { Range, Hole1, Dashboard }

    /// <summary>
    /// One canonical shot configuration for the physics lab.
    /// Velocity is in m/s world space; Range presets fire along +X.
    /// </summary>
    public readonly struct ShotPreset
    {
        public readonly string        Id;
        public readonly string        DisplayName;
        public readonly PresetScene   Scene;
        public readonly fp3           Origin;
        public readonly fp3           Velocity;
        public readonly SpinState     Spin;
        public readonly WindConfig    Wind;
        public readonly string        Notes;
        public readonly bool          HasSurfaceOverride;
        public readonly SurfaceType   SurfaceOverride;

        public ShotPreset(string id, string name, PresetScene scene,
            fp3 origin, fp3 velocity, SpinState spin, WindConfig wind, string notes,
            bool hasSurfaceOverride = false, SurfaceType surfaceOverride = SurfaceType.Fairway)
        {
            Id                 = id;
            DisplayName        = name;
            Scene              = scene;
            Origin             = origin;
            Velocity           = velocity;
            Spin               = spin;
            Wind               = wind;
            Notes              = notes;
            HasSurfaceOverride = hasSurfaceOverride;
            SurfaceOverride    = surfaceOverride;
        }
    }
}
