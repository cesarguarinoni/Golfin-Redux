using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    public static class BallSimulation
    {
        // Phase 1 constants — later these move to CSV-backed config
        private static readonly fp Gravity = fp.FromDouble(-9.80665);  // m/s², Y axis
        private static readonly fp Dt = fp.One / fp.FromInt(240);       // 1/240 s
        private static readonly fp WorldBound = fp.FromInt(2000);       // ±2km safety
        // Precomputed to avoid Dt/2 and Dt/6 losing precision via fp division truncation.
        // Use (sum * Dt) / Two and (sum * Dt) / Six, not sum * (Dt/Two).
        private static readonly fp Two = fp.FromInt(2);
        private static readonly fp Six = fp.FromInt(6);

        /// <summary>
        /// Integrate ball flight from input.origin with input.velocity until one of:
        /// - maxDuration reached
        /// - ball y falls below ground.SampleHeight at (x, z)
        /// - position exits ±WorldBound on x or z
        ///
        /// Returns deterministic Trajectory. Same inputs → same bytes, every time,
        /// every platform.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground)
        {
            var samples = new List<TrajectorySample>(capacity: 1536);
            fp3 pos = input.origin;
            fp3 vel = input.velocity;
            fp t = fp.Zero;

            samples.Add(new TrajectorySample(t, pos, vel));

            TerminationReason termination = TerminationReason.MaxDurationReached;

            // RK4 step count = ceil(maxDuration / dt), with safety ceiling
            int maxSteps = 60 * 240;  // 60 seconds of integration hard cap
            for (int step = 0; step < maxSteps; step++)
            {
                if (t >= input.maxDuration)
                {
                    termination = TerminationReason.MaxDurationReached;
                    break;
                }

                // RK4 integration — vacuum trajectory, acceleration is constant gravity.
                // Using RK4 now so Phase 2+ (velocity-dependent drag/Magnus) fits without refactor.
                fp3 k1v = Accel(pos, vel);
                fp3 k1p = vel;

                // Multiply by Dt first, then divide — avoids losing 0.5 raw units
                // in Q16.16 when computing Dt/2 or Dt/6 via fp division.
                fp3 pos2 = pos + k1p * Dt / Two;
                fp3 vel2 = vel + k1v * Dt / Two;
                fp3 k2v = Accel(pos2, vel2);
                fp3 k2p = vel2;

                fp3 pos3 = pos + k2p * Dt / Two;
                fp3 vel3 = vel + k2v * Dt / Two;
                fp3 k3v = Accel(pos3, vel3);
                fp3 k3p = vel3;

                fp3 pos4 = pos + k3p * Dt;
                fp3 vel4 = vel + k3v * Dt;
                fp3 k4v = Accel(pos4, vel4);
                fp3 k4p = vel4;

                fp3 posNext = pos + (k1p + k2p * Two + k3p * Two + k4p) * Dt / Six;
                fp3 velNext = vel + (k1v + k2v * Two + k3v * Two + k4v) * Dt / Six;
                fp tNext = t + Dt;

                // Ground hit detection — interpolate between pos and posNext
                fp groundY = ground.SampleHeight(posNext.x, posNext.z);
                if (posNext.y <= groundY && pos.y > groundY)
                {
                    fp dy = pos.y - posNext.y;
                    fp above = pos.y - groundY;
                    fp frac = dy.raw == 0 ? fp.Zero : above / dy;
                    fp3 hitPos = new fp3(
                        pos.x + (posNext.x - pos.x) * frac,
                        groundY,
                        pos.z + (posNext.z - pos.z) * frac);
                    fp3 hitVel = new fp3(
                        vel.x + (velNext.x - vel.x) * frac,
                        vel.y + (velNext.y - vel.y) * frac,
                        vel.z + (velNext.z - vel.z) * frac);
                    fp tHit = t + (tNext - t) * frac;
                    samples.Add(new TrajectorySample(tHit, hitPos, hitVel));
                    pos = hitPos; vel = hitVel; t = tHit;
                    termination = TerminationReason.HitGround;
                    break;
                }

                // World bounds
                if (posNext.x > WorldBound || posNext.x < -WorldBound ||
                    posNext.z > WorldBound || posNext.z < -WorldBound)
                {
                    termination = TerminationReason.ExitedWorldBounds;
                    samples.Add(new TrajectorySample(tNext, posNext, velNext));
                    pos = posNext; vel = velNext; t = tNext;
                    break;
                }

                pos = posNext;
                vel = velNext;
                t = tNext;
                samples.Add(new TrajectorySample(t, pos, vel));
            }

            return new Trajectory(samples, pos, vel, t, termination, new List<TerrainHit>());
        }

        /// <summary>
        /// Acceleration as a function of position and velocity.
        /// Phase 1: gravity only. Phase 2 adds drag and Magnus lift (velocity-dependent).
        /// </summary>
        private static fp3 Accel(fp3 pos, fp3 vel)
        {
            return new fp3(fp.Zero, Gravity, fp.Zero);
        }
    }
}
