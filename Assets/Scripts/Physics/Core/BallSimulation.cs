using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    public static class BallSimulation
    {
        private static readonly fp Gravity = fp.FromDouble(-9.80665);  // m/s²
        private static readonly fp Dt = fp.One / fp.FromInt(240);      // 1/240 s
        private static readonly fp WorldBound = fp.FromInt(2000);      // ±2km safety
        // Precomputed to avoid Dt/2 and Dt/6 losing precision via fp division truncation.
        // Always reorder as (sum * Dt) / Two rather than sum * (Dt/Two).
        private static readonly fp Two = fp.FromInt(2);
        private static readonly fp Six = fp.FromInt(6);

        /// <summary>
        /// Phase 1 backward-compatible overload. Uses AeroConfig.Vacuum (Cd=0, Cl=0),
        /// so this path is gravity-only — identical to Phase 1 results.
        /// Phase 1 tests call this overload and must continue to pass.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground)
            => Simulate(input, ground, AeroConfig.Vacuum);

        /// <summary>
        /// Full Phase 2+ integration. Forwards to the wind-aware overload with calm wind.
        /// Existing Phase 2 tests call this signature and must continue to pass unchanged.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground, AeroConfig aero)
            => Simulate(input, ground, aero, WindConfig.Calm);

        /// <summary>
        /// Full Phase 3 integration. Wind is sampled at each RK4 sub-step using that
        /// sub-step's estimated (position, time) so drag direction stays correct mid-step.
        /// With WindConfig.Calm this is bit-exact to the Phase 2 aero-only path.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground, AeroConfig aero, WindConfig wind)
        {
            var samples = new List<TrajectorySample>(capacity: 1536);
            fp3 pos = input.origin;
            fp3 vel = input.velocity;
            fp t = fp.Zero;
            SpinState spin = input.Spin;

            samples.Add(new TrajectorySample(t, pos, vel));

            TerminationReason termination = TerminationReason.MaxDurationReached;

            int maxSteps = 60 * 240;  // 60 s hard cap
            for (int step = 0; step < maxSteps; step++)
            {
                if (t >= input.maxDuration)
                {
                    termination = TerminationReason.MaxDurationReached;
                    break;
                }

                // RK4 — wind sampled at each sub-step (position, time) so drag direction
                // is correct when wind varies with altitude or gusts over the step duration.
                fp3 w1  = WindModel.SampleWind(pos, t, wind);
                fp3 k1v = Accel(vel, w1, spin, aero);
                fp3 k1p = vel;

                fp3 pos2 = pos + (k1p * Dt) / Two;
                fp3 vel2 = vel + (k1v * Dt) / Two;
                fp3 w2   = WindModel.SampleWind(pos2, t + Dt / Two, wind);
                fp3 k2v  = Accel(vel2, w2, spin, aero);
                fp3 k2p  = vel2;

                fp3 pos3 = pos + (k2p * Dt) / Two;
                fp3 vel3 = vel + (k2v * Dt) / Two;
                fp3 w3   = WindModel.SampleWind(pos3, t + Dt / Two, wind);
                fp3 k3v  = Accel(vel3, w3, spin, aero);
                fp3 k3p  = vel3;

                fp3 pos4 = pos + k3p * Dt;
                fp3 vel4 = vel + k3v * Dt;
                fp3 w4   = WindModel.SampleWind(pos4, t + Dt, wind);
                fp3 k4v  = Accel(vel4, w4, spin, aero);
                fp3 k4p  = vel4;

                // Weighted sum — multiply before dividing to preserve Q16.16 precision.
                fp3 posNext = pos + (k1p + k2p * Two + k3p * Two + k4p) * Dt / Six;
                fp3 velNext = vel + (k1v + k2v * Two + k3v * Two + k4v) * Dt / Six;
                fp tNext = t + Dt;

                // Ground hit detection
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

                // Exponential spin decay: ω(t+Δt) = ω(t) · (1 − λ·Δt)
                if (aero.SpinDecayRate > fp.Epsilon && spin.IsSpinning)
                {
                    fp decayFactor = fp.One - (aero.SpinDecayRate * Dt);
                    spin = new SpinState(spin.Axis, spin.Rate * decayFactor);
                }

                pos = posNext;
                vel = velNext;
                t = tNext;
                samples.Add(new TrajectorySample(t, pos, vel));
            }

            return new Trajectory(samples, pos, vel, t, termination, new List<TerrainHit>());
        }

        // Wind-aware acceleration. Forwards to AeroModel with the wind vector at this sub-step.
        private static fp3 Accel(fp3 vel, fp3 wind, SpinState spin, AeroConfig cfg)
        {
            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);
            fp3 aeroForce = AeroModel.ComputeAeroForce(vel, wind, spin, cfg);
            if (cfg.BallMass <= fp.Epsilon) return gravity;
            fp3 aeroAccel = aeroForce / cfg.BallMass;
            return gravity + aeroAccel;
        }

        // Legacy wind-free wrapper used by Phase 1/2 overloads before wind threading.
        // Now forwards to wind-aware Accel with zero wind for bit-exact back-compat.
        private static fp3 Accel(fp3 vel, SpinState spin, AeroConfig cfg)
            => Accel(vel, fp3.Zero, spin, cfg);
    }
}
