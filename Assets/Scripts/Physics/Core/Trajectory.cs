using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Output of BallSimulation.Simulate. Deterministic: identical inputs
    /// produce identical trajectory data bit-for-bit on any platform.
    /// </summary>
    public sealed class Trajectory
    {
        public readonly List<TrajectorySample> samples;
        public readonly fp3 finalPosition;
        public readonly fp3 finalVelocity;
        public readonly fp finalTime;
        public readonly TerminationReason termination;

        // Phase 2+ will populate this; Phase 1 leaves it empty.
        public readonly List<TerrainHit> terrainHits;

        public Trajectory(List<TrajectorySample> samples, fp3 finalPosition,
                          fp3 finalVelocity, fp finalTime, TerminationReason termination,
                          List<TerrainHit> terrainHits)
        {
            this.samples = samples;
            this.finalPosition = finalPosition;
            this.finalVelocity = finalVelocity;
            this.finalTime = finalTime;
            this.termination = termination;
            this.terrainHits = terrainHits;
        }
    }

    public readonly struct TrajectorySample
    {
        public readonly fp time;
        public readonly fp3 position;
        public readonly fp3 velocity;
        public TrajectorySample(fp time, fp3 position, fp3 velocity)
        { this.time = time; this.position = position; this.velocity = velocity; }
    }

    public readonly struct TerrainHit
    {
        public readonly fp time;
        public readonly fp3 position;
        public readonly fp3 velocityBefore;
        public readonly fp3 velocityAfter;
        public readonly int surfaceId;
        public TerrainHit(fp time, fp3 position, fp3 vBefore, fp3 vAfter, int surfaceId)
        { this.time = time; this.position = position; this.velocityBefore = vBefore;
          this.velocityAfter = vAfter; this.surfaceId = surfaceId; }
    }

    public enum TerminationReason
    {
        MaxDurationReached,
        HitGround,
        StoppedRolling,     // Phase 4+
        ExitedWorldBounds,
    }
}
