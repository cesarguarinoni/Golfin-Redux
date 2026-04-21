using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Complete deterministic input for a single shot. Everything the simulator
    /// needs to produce an identical trajectory on any platform.
    /// Phase 1 uses only origin, velocity, maxDuration. Phase 2 adds Spin.
    /// </summary>
    public readonly struct ShotInput
    {
        public readonly fp3 origin;
        public readonly fp3 velocity;
        public readonly fp maxDuration;
        public readonly SpinState Spin;
        public readonly uint seed;

        // Phase 1 constructor — no spin. SpinState defaults to SpinState.None.
        public ShotInput(fp3 origin, fp3 velocity, fp maxDuration, uint seed = 0)
        {
            this.origin = origin;
            this.velocity = velocity;
            this.maxDuration = maxDuration;
            this.Spin = SpinState.None;
            this.seed = seed;
        }

        // Phase 2 constructor — with spin.
        public ShotInput(fp3 origin, fp3 velocity, fp maxDuration, SpinState spin, uint seed = 0)
        {
            this.origin = origin;
            this.velocity = velocity;
            this.maxDuration = maxDuration;
            this.Spin = spin;
            this.seed = seed;
        }
    }
}
