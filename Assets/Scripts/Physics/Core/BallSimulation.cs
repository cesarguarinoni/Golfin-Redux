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

        private static readonly fp RollTransitionThreshold = fp.FromFloat(0.5f); // m/s vertical to switch to roll
        private static readonly fp BackspinCrMultiplier    = fp.FromFloat(1.15f);

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

        // ──────────────────────────────────────────────────────────────────────────────
        // Phase 4: Surface interaction (bounce + roll). New most-general overload.
        // Phase 1–3 overloads remain unchanged above; they do NOT forward here so
        // their bit-exact behaviour is preserved.
        // ──────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full Phase 4 integration. Runs airborne RK4 phase via Phase 3 overload,
        /// then bounces and rolls the ball to a stop using per-surface coefficients.
        /// With ConstantSurfaceProvider(Fairway) + SurfaceConfig.Default the airborne
        /// portion is bit-exact identical to Simulate(input, ground, aero, wind).
        /// </summary>
        public static Trajectory Simulate(
            ShotInput input,
            IGroundProvider ground,
            AeroConfig aero,
            WindConfig wind,
            ISurfaceProvider surfaces,
            SurfaceConfig surfaceCfg)
        {
            // ── Airborne phase ────────────────────────────────────────────────────────
            var airborne = Simulate(input, ground, aero, wind);

            if (airborne.termination != TerminationReason.HitGround)
                return new Trajectory(airborne.samples, airborne.finalPosition,
                    airborne.finalVelocity, airborne.finalTime, airborne.termination,
                    new List<TerrainHit>());

            var samples    = new List<TrajectorySample>(airborne.samples);
            var hits       = new List<TerrainHit>();
            fp3 pos        = airborne.finalPosition;
            fp3 vel        = airborne.finalVelocity;
            fp  t          = airborne.finalTime;

            // Approximate spin state at impact (decay is slow; good enough for Cr multiplier).
            SpinState spin = input.Spin;
            if (aero.SpinDecayRate > fp.Epsilon && input.Spin.IsSpinning)
            {
                fp decay = fp.One - aero.SpinDecayRate * t;
                spin = decay > fp.Zero
                    ? new SpinState(input.Spin.Axis, input.Spin.Rate * decay)
                    : new SpinState(input.Spin.Axis, fp.Zero);
            }

            // ── Bounce loop ───────────────────────────────────────────────────────────
            int maxBounces = 12;
            for (int bounce = 0; bounce < maxBounces; bounce++)
            {
                SurfaceType surface = surfaces.Classify(pos.x, pos.z);
                SurfaceCoefficients coeff = surfaceCfg[surface];

                // Water: terminate immediately.
                if (surface == SurfaceType.Water)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitWater, hits);
                }

                // Ground normal: use heightmap gradient if available, else flat-up.
                fp3 normal = (ground is HeightmapData hm)
                    ? hm.SampleNormal(pos.x, pos.z)
                    : new fp3(fp.Zero, fp.One, fp.Zero);

                // Decompose velocity into normal + tangent components.
                fp  vn       = fpMath.Dot(vel, normal);  // negative: ball going into ground
                fp3 vNormal  = normal * vn;
                fp3 vTangent = vel - vNormal;

                // Backspin check: if spin.Axis opposes horizontal velocity direction, ball checks.
                fp cr = coeff.Restitution;
                if (spin.IsSpinning)
                {
                    fp3 vHoriz = new fp3(vel.x, fp.Zero, vel.z);
                    if (fpMath.Dot(spin.Axis, vHoriz) < fp.Zero)
                        cr = cr * BackspinCrMultiplier;
                }

                // Apply bounce physics.
                // Normal: reflect with restitution (vn is negative so -cr*vn is positive/upward).
                fp3 vNormalOut  = normal * (-(cr * vn));
                fp  mu          = coeff.TangentFriction;
                fp3 vTangentOut = vTangent * (fp.One - mu);
                fp3 velOut      = vNormalOut + vTangentOut;

                // After first bounce, spin effectively zeroes (ball is skidding / tumbling).
                spin = new SpinState(input.Spin.Axis, fp.Zero);

                fp speed  = fpMath.Sqrt(fpMath.Dot(velOut, velOut));
                fp vnOut  = fpMath.Dot(velOut, normal); // positive = bouncing upward

                // ── Immediate stop ────────────────────────────────────────────────────
                if (speed <= coeff.StopSpeed)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
                }

                hits.Add(new TerrainHit(t, pos, vel, velOut, surface, false));
                vel = velOut;

                // ── Roll transition ───────────────────────────────────────────────────
                if (vnOut < RollTransitionThreshold)
                {
                    // Project velocity into the tangent plane to start roll.
                    fp3 vRoll = vel - normal * fpMath.Dot(vel, normal);
                    return RunRollPhase(pos, vRoll, t, ground, surfaces, surfaceCfg,
                                        aero.BallRadius, samples, hits);
                }

                // ── Another airborne arc after bounce ─────────────────────────────────
                var nextInput   = new ShotInput(pos, vel, fp.FromInt(30), new SpinState(input.Spin.Axis, fp.Zero));
                var nextAirborne = Simulate(nextInput, ground, aero, wind);

                // Append samples with absolute time offset (skip index 0 — duplicate of current pos).
                for (int i = 1; i < nextAirborne.samples.Count; i++)
                {
                    var s = nextAirborne.samples[i];
                    samples.Add(new TrajectorySample(t + s.time, s.position, s.velocity));
                }

                t   = t + nextAirborne.finalTime;
                pos = nextAirborne.finalPosition;
                vel = nextAirborne.finalVelocity;

                if (nextAirborne.termination != TerminationReason.HitGround)
                    return new Trajectory(samples, pos, vel, t, nextAirborne.termination, hits);

                // Continue bounce loop at new ground contact.
            }

            return new Trajectory(samples, pos, vel, t, TerminationReason.MaxBouncesExceeded, hits);
        }

        /// <summary>
        /// Roll integrator. Ball stays in contact with the heightmap surface, decelerating
        /// due to rolling resistance and slope gravity, until stop speed or water.
        /// </summary>
        private static Trajectory RunRollPhase(
            fp3 startPos, fp3 startVel, fp startT,
            IGroundProvider ground, ISurfaceProvider surfaces, SurfaceConfig surfaceCfg,
            fp ballRadius, List<TrajectorySample> samples, List<TerrainHit> hits)
        {
            fp3 pos = startPos;
            fp3 vel = startVel;
            fp  t   = startT;

            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);

            // Set initial Y to ground + radius so ball sits on surface.
            pos = new fp3(pos.x, ground.SampleHeight(pos.x, pos.z) + ballRadius, pos.z);

            int stopConsecutive = 0;
            const int StopStepsRequired = 10; // 10 × 1/240 s ≈ 42 ms
            fp prevSpeedSq = fp.Zero; // track speed² to avoid Sqrt precision issues

            int maxRollSteps = 60 * 240; // 60 s hard cap
            for (int step = 0; step < maxRollSteps; step++)
            {
                SurfaceType surface = surfaces.Classify(pos.x, pos.z);
                SurfaceCoefficients coeff = surfaceCfg[surface];

                // Water check during roll.
                if (surface == SurfaceType.Water)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitWater, hits);
                }

                // Ground normal at current position.
                fp3 normal = (ground is HeightmapData hm)
                    ? hm.SampleNormal(pos.x, pos.z)
                    : new fp3(fp.Zero, fp.One, fp.Zero);

                // Project velocity onto tangent plane (remove residual normal component).
                vel = vel - normal * fpMath.Dot(vel, normal);

                // Gravity component along the slope surface.
                fp3 aGravityTangent = gravity - normal * fpMath.Dot(gravity, normal);

                // Rolling resistance (proportional deceleration).
                fp3 aResistance = vel * (-coeff.RollingResistance);

                // Integrate velocity and position.
                vel = vel + (aGravityTangent + aResistance) * Dt;

                // Project position: xz advance, y follows terrain.
                fp3 posNext = new fp3(
                    pos.x + vel.x * Dt,
                    fp.Zero, // placeholder, set below
                    pos.z + vel.z * Dt);
                posNext = new fp3(posNext.x,
                    ground.SampleHeight(posNext.x, posNext.z) + ballRadius,
                    posNext.z);

                t   = t + Dt;
                pos = posNext;
                samples.Add(new TrajectorySample(t, pos, vel));

                // Stop detection using speed² to avoid fpMath.Sqrt underestimation.
                // Only count toward stop when decelerating (speed² < prev), not during
                // initial acceleration from rest on a slope.
                fp speedSq    = fpMath.Dot(vel, vel);
                fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
                if (speedSq < stopThresh && speedSq <= prevSpeedSq)
                {
                    stopConsecutive++;
                    if (stopConsecutive >= StopStepsRequired)
                    {
                        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
                    }
                }
                else
                {
                    stopConsecutive = 0;
                }
                prevSpeedSq = speedSq;

                // World bounds check.
                if (pos.x > WorldBound || pos.x < -WorldBound ||
                    pos.z > WorldBound || pos.z < -WorldBound)
                    return new Trajectory(samples, pos, vel, t, TerminationReason.ExitedWorldBounds, hits);
            }

            // Time cap hit during roll — treat as stopped.
            hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, SurfaceType.Fairway, true));
            return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
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
