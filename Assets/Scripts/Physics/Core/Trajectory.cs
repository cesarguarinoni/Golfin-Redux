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

    public struct TerrainHit
    {
        public fp Time;
        public fp3 Position;
        public fp3 VelocityIn;     // before bounce
        public fp3 VelocityOut;    // after bounce (zero if this hit ended sim — water, stop)
        public SurfaceType Surface;
        public bool IsStop;        // true = final resting hit, not a bounce
        public TerrainHit(fp time, fp3 position, fp3 vIn, fp3 vOut, SurfaceType surface, bool isStop)
        { Time = time; Position = position; VelocityIn = vIn; VelocityOut = vOut;
          Surface = surface; IsStop = isStop; }
    }

    public enum TerminationReason
    {
        MaxDurationReached,
        HitGround,          // first airborne→ground contact (Phase 1–3 endpoint)
        ExitedWorldBounds,
        BallStopped,        // roll phase reached stop_speed on near-flat surface
        HitWater,           // terminated by water hazard
        HitOOB,             // terminated by out-of-bounds zone
        MaxBouncesExceeded, // safety cap; shouldn't happen in practice
        // cup_capture_and_lipout (2026-08-05): the roll/putt integrator captured the ball in
        // the cup and synthesized the fall-in. The trajectory ENDS at the cup bottom, so the
        // animator plays the drop and BallStateMachine reports InCup the moment it finishes.
        // APPEND-ONLY: existing enum values are order-sensitive (serialized in bot goldens).
        CupCapture,
    }
}
