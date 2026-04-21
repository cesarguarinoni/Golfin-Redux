using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Complete deterministic input for a single shot. Everything the simulator
    /// needs to produce an identical trajectory on any platform.
    /// Phase 1 uses only origin, velocity, maxDuration. Spin / wind / surface
    /// fields are reserved for Phases 2+ and can be default-valued for now.
    /// </summary>
    public readonly struct ShotInput
    {
        public readonly fp3 origin;
        public readonly fp3 velocity;
        public readonly fp maxDuration;

        // Phase 2+ fields — unused in Phase 1 but declared for ABI stability.
        public readonly fp3 spinAxis;
        public readonly fp spinRateRadPerSec;
        public readonly uint seed;

        public ShotInput(fp3 origin, fp3 velocity, fp maxDuration,
                         fp3 spinAxis = default, fp spinRateRadPerSec = default,
                         uint seed = 0)
        {
            this.origin = origin;
            this.velocity = velocity;
            this.maxDuration = maxDuration;
            this.spinAxis = spinAxis;
            this.spinRateRadPerSec = spinRateRadPerSec;
            this.seed = seed;
        }
    }
}
