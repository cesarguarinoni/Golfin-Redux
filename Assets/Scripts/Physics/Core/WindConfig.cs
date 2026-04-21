using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Per-shot wind conditions. Steady base vector + optional gust envelope
    /// + optional altitude-based speed multiplier. Deterministic given seed.
    /// Pure data; loaded by PhysicsConfigLoader from Resources/Physics/wind.csv
    /// or synthesized per-shot from design-side values.
    /// </summary>
    public struct WindConfig
    {
        /// <summary>Base wind vector in world-space m/s. +X east, +Z north.</summary>
        public fp3 BaseVelocity;

        /// <summary>Gust amplitude as fraction of |BaseVelocity|. 0 = no gusts. 0.2 = ±20% variation.</summary>
        public fp GustAmplitude;

        /// <summary>Gust frequency in Hz — roughly how often the gust cycle oscillates. 0.3–0.8 Hz is typical.</summary>
        public fp GustFrequency;

        /// <summary>Altitude speed multiplier: wind at height Y scales as (1 + AltitudeFactor · Y / AltitudeRefMeters). 0 = no profile.</summary>
        public fp AltitudeFactor;

        /// <summary>Reference altitude in meters (typically 10m). Unused if AltitudeFactor is 0.</summary>
        public fp AltitudeRefMeters;

        /// <summary>PRNG seed for gusts. Same seed → same gust sequence → reproducible trajectories.</summary>
        public uint Seed;

        public static WindConfig Calm => new WindConfig
        {
            BaseVelocity      = fp3.Zero,
            GustAmplitude     = fp.Zero,
            GustFrequency     = fp.Zero,
            AltitudeFactor    = fp.Zero,
            AltitudeRefMeters = fp.FromInt(10),
            Seed              = 0,
        };

        public bool IsActive =>
            fpMath.Dot(BaseVelocity, BaseVelocity) > fp.Epsilon || GustAmplitude > fp.Epsilon;
    }
}
