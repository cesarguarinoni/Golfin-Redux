using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Samples wind velocity at a given (position, time) from a WindConfig.
    /// Deterministic: same config + same (pos, t) → same wind vector.
    ///
    /// Model: steady base vector, plus a gust envelope that sinusoidally
    /// modulates magnitude over time with seed-derived phase, plus an
    /// optional linear altitude profile.
    /// </summary>
    public static class WindModel
    {
        public static fp3 SampleWind(fp3 position, fp time, WindConfig cfg)
        {
            if (!cfg.IsActive) return fp3.Zero;

            fp3 wind = cfg.BaseVelocity;

            // Gust envelope: multiply magnitude by (1 + A · sin(2π·f·t + φ)).
            // φ is derived from seed so different seeds give different gust timing.
            if (cfg.GustAmplitude > fp.Epsilon && cfg.GustFrequency > fp.Epsilon)
            {
                fp phase = SeedToPhase(cfg.Seed);
                fp angle = fpMath.TwoPi * cfg.GustFrequency * time + phase;
                fp gust  = fp.One + cfg.GustAmplitude * fpMath.Sin(angle);
                wind = wind * gust;
            }

            // Altitude profile: wind scales linearly with Y.
            // At Y=0, multiplier is 1. At Y=AltitudeRefMeters, multiplier is 1 + AltitudeFactor.
            if (cfg.AltitudeFactor > fp.Epsilon && cfg.AltitudeRefMeters > fp.Epsilon)
            {
                fp altScale = fp.One + cfg.AltitudeFactor * (position.y / cfg.AltitudeRefMeters);
                // Clamp to prevent negative wind below ground or absurdly high aloft.
                altScale = fpMath.Clamp(altScale, fp.Half, fp.FromInt(3));
                wind = wind * altScale;
            }

            return wind;
        }

        /// <summary>Deterministic uint-to-phase hash. Result is in [0, 2π).</summary>
        private static fp SeedToPhase(uint seed)
        {
            // Splitmix-style hash, then scale into [0, 2π). Pure integer → fp; no float.
            ulong x = seed;
            x = (x ^ (x >> 16)) * 0x7FEB352Dul;
            x = (x ^ (x >> 15)) * 0x846CA68Bul;
            x = x ^ (x >> 16);
            // Bottom 16 bits as Q16.16 raw: range [0, 65535] → fp [0, ~1.0), then × 2π.
            fp frac = fp.FromRaw((long)(x & 0xFFFFu));
            return frac * fpMath.TwoPi;
        }
    }
}
