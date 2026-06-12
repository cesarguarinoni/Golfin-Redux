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

#if UNITY_EDITOR
        // Wired by the runtime layer (PhysicsLabController) to UnityEngine.Debug.LogError.
        // Null-safe: if not wired, assertion is silently skipped.
        public static System.Action<string> DiagErrorLogger;

        /// <summary>
        /// Wired by the runtime layer to Debug.Log. Emits a single line at sim entry
        /// (with the result of IsPutt() and the gate inputs) and another at termination.
        /// Null-safe; zero overhead when unwired.
        /// </summary>
        public static System.Action<string> DiagShotLogger;

        /// <summary>
        /// Wired by the runtime layer to Debug.Log. Emits a throttled snapshot every
        /// `RollLogStrideSteps` (default 24 = 10 Hz at the 240 Hz sim rate) from
        /// inside RunRollPhase and RunPuttPhase. Null-safe; zero overhead when unwired.
        /// </summary>
        public static System.Action<string> DiagRollLogger;

        /// <summary>How often (in sim steps) DiagRollLogger fires. 24 = 10 Hz at 240 Hz dt.</summary>
        public static int RollLogStrideSteps = 24;

        static void CheckTerrainInvariant(IGroundProvider ground, SurfaceType surface, fp3 pos)
        {
            if (DiagErrorLogger == null) return;
            float gY = ground.SampleHeight(pos.x, pos.z, surface).ToFloat();
            if (pos.y.ToFloat() < gY - 0.02f)
                DiagErrorLogger(
                    $"[Terrain] Ball below surface! surface={surface} " +
                    $"ballY={pos.y.ToFloat():F3} groundY={gY:F3} " +
                    $"xz=({pos.x.ToFloat():F2},{pos.z.ToFloat():F2})");
        }
#endif

        // ── Phase 1 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Phase 1 backward-compatible overload. Uses AeroConfig.Vacuum (Cd=0, Cl=0),
        /// so this path is gravity-only — identical to Phase 1 results.
        /// Phase 1 tests call this overload and must continue to pass.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground)
            => Simulate(input, ground, AeroConfig.Vacuum);

        // ── Phase 2 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full Phase 2+ integration. Forwards to the wind-aware overload with calm wind.
        /// Existing Phase 2 tests call this signature and must continue to pass unchanged.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground, AeroConfig aero)
            => Simulate(input, ground, aero, WindConfig.Calm);

        // ── Phase 3 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Full Phase 3 integration with wind. Forwards to the internal airborne integrator
        /// with BallPhysicsModifiers.Neutral so Phase 1–3 tests remain bit-exact.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground, AeroConfig aero, WindConfig wind)
            => SimulateAirborne(input, ground, aero, wind, BallPhysicsModifiers.Neutral);

        // ── Phase 4 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Phase 4 overload — forwards to Phase 5 7-arg with PuttConfig.Default.
        /// Non-putt shots are bit-exact identical to calling the 7-arg explicitly.
        /// </summary>
        public static Trajectory Simulate(
            ShotInput input,
            IGroundProvider ground,
            AeroConfig aero,
            WindConfig wind,
            ISurfaceProvider surfaces,
            SurfaceConfig surfaceCfg)
            => Simulate(input, ground, aero, wind, surfaces, surfaceCfg, PuttConfig.Default);

        // ── Phase 5 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Phase 5 entry. Forwards to the Phase 6 overload with BallPhysicsModifiers.Neutral
        /// so all Phase 1–5 tests remain bit-exact.
        /// </summary>
        public static Trajectory Simulate(
            ShotInput input,
            IGroundProvider ground,
            AeroConfig aero,
            WindConfig wind,
            ISurfaceProvider surfaces,
            SurfaceConfig surfaceCfg,
            PuttConfig puttCfg)
            => Simulate(input, ground, aero, wind, surfaces, surfaceCfg, puttCfg, BallPhysicsModifiers.Neutral);

        // ── Phase 6 ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Phase 6 entry. Adds BallPhysicsModifiers for ball-driven runtime scalars:
        ///   Rebound  — multiplies surface restitution at each bounce.
        ///   Roll     — multiplies rolling resistance during roll and putt phases.
        ///   WindCut  — reduces effective wind-delta magnitude before aero drag is computed.
        ///
        /// With BallPhysicsModifiers.Neutral (all multipliers = 1, WindCutFraction = 0) this
        /// is bit-exact identical to the Phase 5 7-arg path — the blocking gate for this phase.
        /// </summary>
        public static Trajectory Simulate(
            ShotInput input,
            IGroundProvider ground,
            AeroConfig aero,
            WindConfig wind,
            ISurfaceProvider surfaces,
            SurfaceConfig surfaceCfg,
            PuttConfig puttCfg,
            BallPhysicsModifiers ballMods)
            => Simulate(input, ground, aero, wind, surfaces, surfaceCfg, puttCfg, ballMods, null);

        // ── Phase 7 (tree collisions) ─────────────────────────────────────────────────

        /// <summary>
        /// Phase 7 entry. Adds ITreeObstacleProvider for deterministic tree trunk + canopy
        /// collision in all sim phases (airborne RK4, bounce arcs, roll, putt).
        ///
        /// trees=null → behaviour is bit-exact identical to the Phase 6 8-arg path.
        /// Design: trunk = hard XZ reflect + trunkRestitution (M5b pattern);
        ///         canopy = ONE-TIME entry impulse (vel *= canopyHitDamping) on the step the
        ///                  ball crosses from outside to inside the canopy cylinder (airborne only).
        ///                  No per-step force while inside; no cut on exit. Each fresh re-entry
        ///                  fires its own cut. (D3 revised 2026-06-12: per-step drag was rejected
        ///                  as slow-motion; discrete impulse at contact then normal ballistics.)
        ///
        /// Ordering: trunk check runs BEFORE ground check within a step; earliest frac wins.
        /// Roll/putt phases: trunk-only (canopy floor is above a rolling ball by construction).
        /// </summary>
        public static Trajectory Simulate(
            ShotInput input,
            IGroundProvider ground,
            AeroConfig aero,
            WindConfig wind,
            ISurfaceProvider surfaces,
            SurfaceConfig surfaceCfg,
            PuttConfig puttCfg,
            BallPhysicsModifiers ballMods,
            ITreeObstacleProvider trees)
        {
#if UNITY_EDITOR
            if (DiagShotLogger != null)
            {
                SurfaceType originSurface = surfaces.Classify(input.origin.x, input.origin.z);
                fp speedSq = fpMath.Dot(input.velocity, input.velocity);
                fp speed   = fpMath.Sqrt(speedSq);
                fp vySq    = input.velocity.y * input.velocity.y;
                bool puttGateEligibleSurface =
                    originSurface == SurfaceType.Green ||
                    originSurface == SurfaceType.GreenCollar ||
                    originSurface == SurfaceType.Tee;
                bool puttGateSpeedOk = speed.ToFloat() < 8.0f;
                bool puttGateAngleOk = vySq.ToFloat() <= speedSq.ToFloat() * 0.067f;
                DiagShotLogger(
                    $"[ShotEntry] origin=({input.origin.x.ToFloat():F2},{input.origin.y.ToFloat():F2},{input.origin.z.ToFloat():F2}) " +
                    $"vel=({input.velocity.x.ToFloat():F3},{input.velocity.y.ToFloat():F3},{input.velocity.z.ToFloat():F3}) " +
                    $"|v|={speed.ToFloat():F3}m/s spin={input.Spin.Rate.ToFloat():F1}rad/s " +
                    $"originSurface={originSurface} " +
                    $"isPuttGate=(speedOk={puttGateSpeedOk}, angleOk={puttGateAngleOk}, surfaceOk={puttGateEligibleSurface}) " +
                    $"ballMods=(rebound={ballMods.ReboundMultiplier.ToFloat():F3}, roll={ballMods.RollResistanceMultiplier.ToFloat():F3}, windCut={ballMods.WindCutFraction.ToFloat():F3})");
            }
#endif
            if (IsPutt(input, surfaces))
            {
                var samples = new List<TrajectorySample>(capacity: 512);
                var hits    = new List<TerrainHit>();

                SurfaceType originSurface = surfaces.Classify(input.origin.x, input.origin.z);
                fp3 startPos = new fp3(
                    input.origin.x,
                    ground.SampleHeight(input.origin.x, input.origin.z, originSurface) + aero.BallRadius,
                    input.origin.z);
                fp3 normal0 = (ground is HeightmapData hm0)
                    ? hm0.SampleNormal(startPos.x, startPos.z)
                    : new fp3(fp.Zero, fp.One, fp.Zero);
                fp3 startVel = input.velocity - normal0 * fpMath.Dot(input.velocity, normal0);

                samples.Add(new TrajectorySample(fp.Zero, startPos, startVel));

                return RunPuttPhase(startPos, startVel, fp.Zero,
                                    ground, surfaces, surfaceCfg, puttCfg,
                                    aero.BallRadius, samples, hits, ballMods, trees);
            }

            // ── Airborne phase ────────────────────────────────────────────────────────
            var airborne = SimulateAirborne(input, ground, aero, wind, ballMods, trees);

            if (airborne.termination != TerminationReason.HitGround)
            {
#if UNITY_EDITOR
                if (DiagShotLogger != null)
                    DiagShotLogger(
                        $"[ShotExit] termination={airborne.termination} " +
                        $"finalPos=({airborne.finalPosition.x.ToFloat():F2},{airborne.finalPosition.y.ToFloat():F2},{airborne.finalPosition.z.ToFloat():F2}) " +
                        $"finalT={airborne.finalTime.ToFloat():F2}s samples={airborne.samples.Count} hits=0");
#endif
                return new Trajectory(airborne.samples, airborne.finalPosition,
                    airborne.finalVelocity, airborne.finalTime, airborne.termination,
                    new List<TerrainHit>());
            }

            var samplesList = new List<TrajectorySample>(airborne.samples);
            var hitsList    = new List<TerrainHit>();
            fp3 pos         = airborne.finalPosition;
            fp3 vel         = airborne.finalVelocity;
            fp  t           = airborne.finalTime;

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

                if (surface == SurfaceType.Water)
                {
                    hitsList.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
#if UNITY_EDITOR
                    if (DiagShotLogger != null)
                        DiagShotLogger(
                            $"[ShotExit] termination={TerminationReason.HitWater} " +
                            $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                            $"finalT={t.ToFloat():F2}s samples={samplesList.Count} hits={hitsList.Count}");
#endif
                    return new Trajectory(samplesList, pos, fp3.Zero, t, TerminationReason.HitWater, hitsList);
                }
                if (surface == SurfaceType.OOB)
                {
                    hitsList.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
#if UNITY_EDITOR
                    if (DiagShotLogger != null)
                        DiagShotLogger(
                            $"[ShotExit] termination={TerminationReason.HitOOB} " +
                            $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                            $"finalT={t.ToFloat():F2}s samples={samplesList.Count} hits={hitsList.Count}");
#endif
                    return new Trajectory(samplesList, pos, fp3.Zero, t, TerminationReason.HitOOB, hitsList);
                }

                fp3 normal = (ground is HeightmapData hm)
                    ? hm.SampleNormal(pos.x, pos.z)
                    : new fp3(fp.Zero, fp.One, fp.Zero);

                fp  vn       = fpMath.Dot(vel, normal);
                fp3 vNormal  = normal * vn;
                fp3 vTangent = vel - vNormal;

                fp cr = coeff.Restitution;
                if (spin.IsSpinning)
                {
                    fp3 vHoriz = new fp3(vel.x, fp.Zero, vel.z);
                    if (fpMath.Dot(spin.Axis, vHoriz) < fp.Zero)
                        cr = cr * BackspinCrMultiplier;
                }
                // Phase 6: ball Rebound stat multiplies restitution at each bounce.
                cr = cr * ballMods.ReboundMultiplier;

                fp3 vNormalOut  = normal * (-(cr * vn));
                fp  mu          = coeff.TangentFriction;
                fp3 vTangentOut = vTangent * (fp.One - mu);
                fp3 velOut      = vNormalOut + vTangentOut;

                spin = new SpinState(input.Spin.Axis, fp.Zero);

                fp speed  = fpMath.Sqrt(fpMath.Dot(velOut, velOut));
                fp vnOut  = fpMath.Dot(velOut, normal);

                if (speed <= coeff.StopSpeed)
                {
                    hitsList.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
#if UNITY_EDITOR
                    if (DiagShotLogger != null)
                        DiagShotLogger(
                            $"[ShotExit] termination={TerminationReason.BallStopped} " +
                            $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                            $"finalT={t.ToFloat():F2}s samples={samplesList.Count} hits={hitsList.Count}");
#endif
                    return new Trajectory(samplesList, pos, fp3.Zero, t, TerminationReason.BallStopped, hitsList);
                }

                hitsList.Add(new TerrainHit(t, pos, vel, velOut, surface, false));
                vel = velOut;

                if (vnOut < RollTransitionThreshold)
                {
                    fp3 vRoll = vel - normal * fpMath.Dot(vel, normal);
                    return RunRollPhase(pos, vRoll, t, ground, surfaces, surfaceCfg,
                                        aero.BallRadius, samplesList, hitsList, ballMods, trees);
                }

                var nextInput    = new ShotInput(pos, vel, fp.FromInt(30), new SpinState(input.Spin.Axis, fp.Zero));
                var nextAirborne = SimulateAirborne(nextInput, ground, aero, wind, ballMods, trees);

                for (int i = 1; i < nextAirborne.samples.Count; i++)
                {
                    var s = nextAirborne.samples[i];
                    samplesList.Add(new TrajectorySample(t + s.time, s.position, s.velocity));
                }

                t   = t + nextAirborne.finalTime;
                pos = nextAirborne.finalPosition;
                vel = nextAirborne.finalVelocity;

                if (nextAirborne.termination != TerminationReason.HitGround)
                {
#if UNITY_EDITOR
                    if (DiagShotLogger != null)
                        DiagShotLogger(
                            $"[ShotExit] termination={nextAirborne.termination} " +
                            $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                            $"finalT={t.ToFloat():F2}s samples={samplesList.Count} hits={hitsList.Count}");
#endif
                    return new Trajectory(samplesList, pos, vel, t, nextAirborne.termination, hitsList);
                }
            }

#if UNITY_EDITOR
            if (DiagShotLogger != null)
                DiagShotLogger(
                    $"[ShotExit] termination={TerminationReason.MaxBouncesExceeded} " +
                    $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                    $"finalT={t.ToFloat():F2}s samples={samplesList.Count} hits={hitsList.Count}");
#endif
            return new Trajectory(samplesList, pos, vel, t, TerminationReason.MaxBouncesExceeded, hitsList);
        }

        // ── Internal airborne integrator ──────────────────────────────────────────────

        /// <summary>
        /// RK4 airborne integrator. Phase 6 addition: WindCutFraction reduces the wind
        /// vector magnitude before aero drag is computed, so a higher Wind Cut ball drifts
        /// less. With BallPhysicsModifiers.Neutral (WindCutFraction=0), wind is unchanged
        /// and results are bit-exact with the original Phase 3 path.
        /// </summary>
        private static Trajectory SimulateAirborne(
            ShotInput input,
            IGroundProvider ground,
            AeroConfig aero,
            WindConfig wind,
            BallPhysicsModifiers ballMods,
            ITreeObstacleProvider trees = null)
        {
            var samples = new List<TrajectorySample>(capacity: 1536);
            fp3 pos = input.origin;
            fp3 vel = input.velocity;
            fp  t   = fp.Zero;
            SpinState spin = input.Spin;

            samples.Add(new TrajectorySample(t, pos, vel));

            TerminationReason termination = TerminationReason.MaxDurationReached;

            // Phase 6: scale sampled wind by (1 − WindCutFraction) before aero drag.
            // At Neutral, scale = 1.0 — multiply is exact in Q16.16, bit-perfect.
            fp windCutScale = fp.One - ballMods.WindCutFraction;

            int maxSteps = 60 * 240;  // 60 s hard cap
            for (int step = 0; step < maxSteps; step++)
            {
                if (t >= input.maxDuration)
                {
                    termination = TerminationReason.MaxDurationReached;
                    break;
                }

                fp3 w1  = WindModel.SampleWind(pos, t, wind) * windCutScale;
                fp3 k1v = Accel(vel, w1, spin, aero);
                fp3 k1p = vel;

                fp3 pos2 = pos + (k1p * Dt) / Two;
                fp3 vel2 = vel + (k1v * Dt) / Two;
                fp3 w2   = WindModel.SampleWind(pos2, t + Dt / Two, wind) * windCutScale;
                fp3 k2v  = Accel(vel2, w2, spin, aero);
                fp3 k2p  = vel2;

                fp3 pos3 = pos + (k2p * Dt) / Two;
                fp3 vel3 = vel + (k2v * Dt) / Two;
                fp3 w3   = WindModel.SampleWind(pos3, t + Dt / Two, wind) * windCutScale;
                fp3 k3v  = Accel(vel3, w3, spin, aero);
                fp3 k3p  = vel3;

                fp3 pos4 = pos + k3p * Dt;
                fp3 vel4 = vel + k3v * Dt;
                fp3 w4   = WindModel.SampleWind(pos4, t + Dt, wind) * windCutScale;
                fp3 k4v  = Accel(vel4, w4, spin, aero);
                fp3 k4p  = vel4;

                fp3 posNext = pos + (k1p + k2p * Two + k3p * Two + k4p) * Dt / Six;
                fp3 velNext = vel + (k1v + k2v * Two + k3v * Two + k4v) * Dt / Six;
                fp  tNext   = t + Dt;

                // ── Tree collision (airborne) ─────────────────────────────────────────
                // Run BEFORE the ground check so a trunk stop doesn't tunnel through terrain.
                // Trunk: earliest XZ crossing → M5b-style interpolate to hit, reflect XZ vel.
                // Canopy (ENTRY crossing: outside p0 → inside p1): one-time vel *= canopyHitDamping;
                //   normal ballistics (gravity/drag/magnus) resume immediately. Not applied
                //   per-step while inside; not applied on exit. Each fresh re-entry fires one cut.
                if (trees != null && trees.TestSegment(pos, posNext, out TreeHit treeHit))
                {
                    if (treeHit.IsTrunk)
                    {
                        // ── frac=0 containment guard: ball is ALREADY INSIDE the trunk cylinder ──
                        // The containment guard in TestTrunkCrossing fires when p0 is inside the trunk
                        // (dist < trunkRadius) and returns frac=0, hitPos=p0, NormalXZ=outward push.
                        // Without special-casing, tHitAbs = t + 0 = t and pos = hitPos = pos → zero
                        // time/position progress → the integrator loops to the cap (14 400 steps),
                        // leaving the ball floating mid-air against the trunk (PROBE7 stuck-ball defect,
                        // red-team iter-6 ARCHITECT_REVIEW_FAIL).
                        //
                        // Fix: when frac=0 (already-inside), push the ball OUT of the trunk cylinder
                        // along NormalXZ to just beyond trunkRadius, then advance to tNext/posNext
                        // (pushed) unconditionally — mirroring the roll/putt handler which advances
                        // t=t+Dt and pos=posNext unconditionally and never sticks.
                        //
                        // For the descending-onto-trunk case (vy<0, XZ inside trunk at/above trunkTopY,
                        // including near-zero XZ velocity where NormalXZ is degenerate): kill XZ velocity,
                        // keep the descent, and advance t=tNext so the ground-crossing check can terminate
                        // the shot normally. The ball lands on the ground below the trunk.
                        if (treeHit.Frac == fp.Zero)
                        {
                            fp trunkR = treeHit.Profile.TrunkRadius;
                            // Check if NormalXZ is degenerate (near-zero XZ — straight-down approach).
                            fp nLenSq = treeHit.NormalXZ.x * treeHit.NormalXZ.x
                                      + treeHit.NormalXZ.z * treeHit.NormalXZ.z;
                            fp3 pushedPos;
                            fp3 velOut;
                            if (nLenSq > fp.FromFloat(0.001f))
                            {
                                // Push the ball outside the trunk along the outward normal.
                                pushedPos = new fp3(
                                    treeHit.HitPos.x + treeHit.NormalXZ.x * (trunkR + fp.FromFloat(0.01f)),
                                    posNext.y,
                                    treeHit.HitPos.z + treeHit.NormalXZ.z * (trunkR + fp.FromFloat(0.01f)));
                                // Mirror: reflect XZ off the trunk normal; keep vy; apply restitution.
                                fp dotXZ2 = velNext.x * treeHit.NormalXZ.x + velNext.z * treeHit.NormalXZ.z;
                                fp3 velRefl2 = new fp3(
                                    velNext.x - fp.FromInt(2) * dotXZ2 * treeHit.NormalXZ.x,
                                    velNext.y,
                                    velNext.z - fp.FromInt(2) * dotXZ2 * treeHit.NormalXZ.z);
                                fp restitution2 = treeHit.Profile.TrunkRestitution;
                                velOut = new fp3(
                                    velRefl2.x * restitution2,
                                    velRefl2.y,
                                    velRefl2.z * restitution2);
                            }
                            else
                            {
                                // Degenerate NormalXZ (straight-down / near-axis descent):
                                // kill XZ velocity, keep vy, let the ball fall through and land.
                                // Advance pos along the descent direction.
                                pushedPos = posNext;
                                velOut = new fp3(fp.Zero, velNext.y, fp.Zero);
                            }
                            // Advance unconditionally to tNext (mirror roll/putt handler).
                            samples.Add(new TrajectorySample(tNext, pushedPos, velOut));
                            pos = pushedPos; vel = velOut; t = tNext;
                            continue;
                        }

                        // ── Normal frac>0 trunk crossing ──
                        // Interpolate to hit position.
                        fp3 hitPos = treeHit.HitPos;
                        // Reflect XZ velocity about the outward normal; Y unchanged.
                        fp dotXZ = velNext.x * treeHit.NormalXZ.x + velNext.z * treeHit.NormalXZ.z;
                        fp3 velReflected = new fp3(
                            velNext.x - fp.FromInt(2) * dotXZ * treeHit.NormalXZ.x,
                            velNext.y,
                            velNext.z - fp.FromInt(2) * dotXZ * treeHit.NormalXZ.z);
                        fp restitution = treeHit.Profile.TrunkRestitution;
                        fp3 velOut2 = new fp3(
                            velReflected.x * restitution,
                            velReflected.y,
                            velReflected.z * restitution);
                        fp tHitAbs = t + (tNext - t) * treeHit.Frac;
                        samples.Add(new TrajectorySample(tHitAbs, hitPos, velOut2));
                        pos = hitPos; vel = velOut2; t = tHitAbs;
                        // Continue integration from the hit point (restart step).
                        continue;
                    }
                    else
                    {
                        // Canopy entry crossing: one-time velocity impulse. Do not interrupt trajectory.
                        // Ball was outside canopy at pos, crosses into canopy at posNext.
                        // Apply canopyHitDamping ONCE; subsequent in-canopy steps are NOT damped.
                        // Normal ballistics (gravity/drag/magnus) resume immediately after this cut.
                        fp damp = treeHit.Profile.CanopyHitDamping;
                        velNext = velNext * damp;
                    }
                }

                // M5b: signed-distance level-detector replaces the previous
                // edge-detector (`posNext.y <= groundY && pos.y > groundY`).
                // The old check compared previous-frame ball-Y against
                // current-frame ground-Y at the new XZ — a category error that
                // missed crossings when the ground rose between frames (ball
                // tunneled into a slope at near-tangential incidence).
                // See Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md.
                fp groundYprev = ground.SampleHeight(pos.x,     pos.z);
                fp groundYnext = ground.SampleHeight(posNext.x, posNext.z);
                fp signedPrev  = pos.y     - groundYprev;   // > 0 = above ground at start
                fp signedNext  = posNext.y - groundYnext;   // < 0 = below ground at end

                if (signedNext <= fp.Zero && signedPrev > fp.Zero)
                {
                    fp denom = signedPrev - signedNext;
                    fp frac  = denom.raw == 0 ? fp.Zero : signedPrev / denom;
                    fp3 hitPos = new fp3(
                        pos.x + (posNext.x - pos.x) * frac,
                        groundYnext, // hit Y is the ground Y at the within-step crossing XZ
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

                if (posNext.x > WorldBound || posNext.x < -WorldBound ||
                    posNext.z > WorldBound || posNext.z < -WorldBound)
                {
                    termination = TerminationReason.ExitedWorldBounds;
                    samples.Add(new TrajectorySample(tNext, posNext, velNext));
                    pos = posNext; vel = velNext; t = tNext;
                    break;
                }

                if (aero.SpinDecayRate > fp.Epsilon && spin.IsSpinning)
                {
                    fp decayFactor = fp.One - (aero.SpinDecayRate * Dt);
                    spin = new SpinState(spin.Axis, spin.Rate * decayFactor);
                }

                pos = posNext;
                vel = velNext;
                t   = tNext;
                samples.Add(new TrajectorySample(t, pos, vel));
            }

            return new Trajectory(samples, pos, vel, t, termination, new List<TerrainHit>());
        }

        // ── Roll phase ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Roll integrator. Ball stays in contact with the heightmap surface, decelerating
        /// due to rolling resistance and slope gravity, until stop speed or water.
        /// Phase 6: ballMods.RollResistanceMultiplier scales rolling resistance each step.
        /// With Neutral (multiplier=1.0), behaviour is bit-exact with Phase 1–5.
        /// </summary>
        private static Trajectory RunRollPhase(
            fp3 startPos, fp3 startVel, fp startT,
            IGroundProvider ground, ISurfaceProvider surfaces, SurfaceConfig surfaceCfg,
            fp ballRadius, List<TrajectorySample> samples, List<TerrainHit> hits,
            BallPhysicsModifiers ballMods, ITreeObstacleProvider trees = null)
        {
            fp3 pos = startPos;
            fp3 vel = startVel;
            fp  t   = startT;

            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);

            // Classify once before the initial snap so we ground to the correct surface.
            SurfaceType initSurface = surfaces.Classify(pos.x, pos.z);
            pos = new fp3(pos.x, ground.SampleHeight(pos.x, pos.z, initSurface) + ballRadius, pos.z);

            int stopConsecutive = 0;
            const int StopStepsRequired = 10;
            fp prevSpeedSq = fp.Zero;

            int maxRollSteps = 60 * 240;
            for (int step = 0; step < maxRollSteps; step++)
            {
                SurfaceType surface = surfaces.Classify(pos.x, pos.z);
                SurfaceCoefficients coeff = surfaceCfg[surface];

                if (surface == SurfaceType.Water)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitWater, hits);
                }
                if (surface == SurfaceType.OOB)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitOOB, hits);
                }

                fp3 normal = (ground is HeightmapData hm)
                    ? hm.SampleNormal(pos.x, pos.z)
                    : new fp3(fp.Zero, fp.One, fp.Zero);

#if UNITY_EDITOR
                if (DiagRollLogger != null && step > 0 && (step % RollLogStrideSteps) == 0)
                {
                    fp gDotN  = fpMath.Dot(gravity, normal);
                    fp3 gTan  = gravity - normal * gDotN;
                    fp slopeMag = fpMath.Sqrt(fpMath.Dot(gTan, gTan));
                    fp speed    = fpMath.Sqrt(fpMath.Dot(vel, vel));
                    DiagRollLogger(
                        $"[RollStep] t={t.ToFloat():F3}s step={step} " +
                        $"pos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                        $"surface={surface} k={coeff.RollingResistance.ToFloat():F3} " +
                        $"rollMul={ballMods.RollResistanceMultiplier.ToFloat():F3} " +
                        $"stopSpeed={coeff.StopSpeed.ToFloat():F3} " +
                        $"|gTan|={slopeMag.ToFloat():F3}m/s² " +
                        $"|v|={speed.ToFloat():F4}m/s stopConsec={stopConsecutive}");
                }
#endif

                vel = vel - normal * fpMath.Dot(vel, normal);

                fp3 aGravityTangent = gravity - normal * fpMath.Dot(gravity, normal);
                // Phase 6: ball Roll stat scales rolling resistance (multiplier < 1 = farther roll).
                fp3 aResistance = vel * (-(coeff.RollingResistance * ballMods.RollResistanceMultiplier));

                vel = vel + (aGravityTangent + aResistance) * Dt;

                fp3 posNext = new fp3(
                    pos.x + vel.x * Dt,
                    fp.Zero,
                    pos.z + vel.z * Dt);
                posNext = new fp3(posNext.x,
                    ground.SampleHeight(posNext.x, posNext.z, surface) + ballRadius,
                    posNext.z);

                // ── Tree trunk collision (roll phase) ─────────────────────────────────
                // Canopy damping is airborne-only (a rolling ball's height < canopy floor in
                // typical layouts). Only trunk XZ reflect is tested here.
                if (trees != null && trees.TestSegment(pos, posNext, out TreeHit rollTreeHit)
                    && rollTreeHit.IsTrunk)
                {
                    fp dotXZ = vel.x * rollTreeHit.NormalXZ.x + vel.z * rollTreeHit.NormalXZ.z;
                    fp3 velReflected = new fp3(
                        vel.x - fp.FromInt(2) * dotXZ * rollTreeHit.NormalXZ.x,
                        vel.y,
                        vel.z - fp.FromInt(2) * dotXZ * rollTreeHit.NormalXZ.z);
                    fp restitution = rollTreeHit.Profile.TrunkRestitution;
                    vel = new fp3(
                        velReflected.x * restitution,
                        velReflected.y,
                        velReflected.z * restitution);
                    posNext = rollTreeHit.HitPos;
                    posNext = new fp3(posNext.x,
                        ground.SampleHeight(posNext.x, posNext.z, surface) + ballRadius,
                        posNext.z);
                }

                t   = t + Dt;
                pos = posNext;
#if UNITY_EDITOR
                CheckTerrainInvariant(ground, surface, pos);
#endif
                samples.Add(new TrajectorySample(t, pos, vel));

                fp speedSq    = fpMath.Dot(vel, vel);
                fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
                // Phase A C.1+C.2 fix: tolerance window on clause 2.
                // At sub-stopSpeed velocities, per-component fp16.16 rounding (LSB ≈ 1.5e-5)
                // can tick speedSq UP by 1 LSB even on flat ground with no real acceleration
                // — when one velocity component rounds down and another doesn't, vx²+vy²+vz²
                // nets a 1-LSB increase. That breaks strict <= non-increase and stalls the
                // stop-counter (captured: 75 s on flat CartPath with stopConsec stuck at 0).
                // We allow speedSq to tick up by up to 5% of stopSpeed² per step and still
                // count the step toward the stop streak. 5% sized so: (a) >> 1 LSB at all
                // realistic stopSpeeds (0.04–0.10), and (b) << genuine uphill re-acceleration
                // on a 2° real-course slope, which preserves clause 2's uphill safety guard.
                fp stopEpsilon = stopThresh * fp.FromFloat(0.05f);
                if (speedSq < stopThresh && speedSq <= prevSpeedSq + stopEpsilon)
                {
                    stopConsecutive++;
                    if (stopConsecutive >= StopStepsRequired)
                    {
                        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
#if UNITY_EDITOR
                        if (DiagShotLogger != null)
                            DiagShotLogger(
                                $"[ShotExit] termination={TerminationReason.BallStopped} " +
                                $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                                $"finalT={t.ToFloat():F2}s samples={samples.Count} hits={hits.Count}");
#endif
                        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
                    }
                }
                else
                {
                    stopConsecutive = 0;
                }
                prevSpeedSq = speedSq;

                if (pos.x > WorldBound || pos.x < -WorldBound ||
                    pos.z > WorldBound || pos.z < -WorldBound)
                    return new Trajectory(samples, pos, vel, t, TerminationReason.ExitedWorldBounds, hits);
            }

            hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, SurfaceType.Fairway, true));
#if UNITY_EDITOR
            if (DiagShotLogger != null)
                DiagShotLogger(
                    $"[ShotExit] termination={TerminationReason.BallStopped} " +
                    $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                    $"finalT={t.ToFloat():F2}s samples={samples.Count} hits={hits.Count}");
#endif
            return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
        }

        // ── Putt phase ────────────────────────────────────────────────────────────────

        private static bool IsPutt(ShotInput input, ISurfaceProvider surfaces)
        {
            fp speedSq    = fpMath.Dot(input.velocity, input.velocity);
            fp maxSpeed   = fp.FromFloat(8.0f);
            fp maxSpeedSq = maxSpeed * maxSpeed;
            if (speedSq > maxSpeedSq) return false;

            fp vySq    = input.velocity.y * input.velocity.y;
            fp sin15Sq = fp.FromFloat(0.067f);
            if (vySq > speedSq * sin15Sq) return false;

            SurfaceType origin = surfaces.Classify(input.origin.x, input.origin.z);
            return origin == SurfaceType.Green
                || origin == SurfaceType.GreenCollar
                || origin == SurfaceType.Tee;
        }

        private static bool IsPuttSurface(SurfaceType s)
            => s == SurfaceType.Green || s == SurfaceType.GreenCollar;

        /// <summary>
        /// Putt-tuned roll integrator. Phase 6: ballMods.RollResistanceMultiplier scales
        /// rolling resistance on every step, matching the roll phase injection.
        /// </summary>
        private static Trajectory RunPuttPhase(
            fp3 startPos, fp3 startVel, fp startT,
            IGroundProvider ground, ISurfaceProvider surfaces,
            SurfaceConfig surfaceCfg, PuttConfig puttCfg,
            fp ballRadius, List<TrajectorySample> samples, List<TerrainHit> hits,
            BallPhysicsModifiers ballMods, ITreeObstacleProvider trees = null)
        {
            fp3 pos = startPos;
            fp3 vel = startVel;
            fp  t   = startT;

            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);

            int stopConsecutive = 0;
            const int StopStepsRequired = 10;
            fp prevSpeedSq = fp.Zero;

            int maxPuttSteps = 60 * 240;
            for (int step = 0; step < maxPuttSteps; step++)
            {
                SurfaceType surface = surfaces.Classify(pos.x, pos.z);

                if (surface == SurfaceType.Water)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitWater, hits);
                }
                if (surface == SurfaceType.OOB)
                {
                    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitOOB, hits);
                }

                SurfaceCoefficients coeff = IsPuttSurface(surface)
                    ? puttCfg[surface]
                    : surfaceCfg[surface];

                fp3 normal = (ground is HeightmapData hm)
                    ? hm.SampleNormal(pos.x, pos.z)
                    : new fp3(fp.Zero, fp.One, fp.Zero);

#if UNITY_EDITOR
                if (DiagRollLogger != null && step > 0 && (step % RollLogStrideSteps) == 0)
                {
                    fp gDotN  = fpMath.Dot(gravity, normal);
                    fp3 gTan  = gravity - normal * gDotN;
                    fp slopeMag = fpMath.Sqrt(fpMath.Dot(gTan, gTan));
                    fp speed    = fpMath.Sqrt(fpMath.Dot(vel, vel));
                    DiagRollLogger(
                        $"[PuttStep] t={t.ToFloat():F3}s step={step} " +
                        $"pos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                        $"surface={surface} k={coeff.RollingResistance.ToFloat():F3} " +
                        $"rollMul={ballMods.RollResistanceMultiplier.ToFloat():F3} " +
                        $"stopSpeed={coeff.StopSpeed.ToFloat():F3} " +
                        $"|gTan|={slopeMag.ToFloat():F3}m/s² " +
                        $"|v|={speed.ToFloat():F4}m/s stopConsec={stopConsecutive}");
                }
#endif

                vel = vel - normal * fpMath.Dot(vel, normal);

                fp3 aGravityTangent = gravity - normal * fpMath.Dot(gravity, normal);
                // Phase 6: ball Roll stat scales rolling resistance during putts too.
                fp3 aResistance     = vel * (-(coeff.RollingResistance * ballMods.RollResistanceMultiplier));
                vel = vel + (aGravityTangent + aResistance) * Dt;

                fp3 posNext = new fp3(
                    pos.x + vel.x * Dt,
                    fp.Zero,
                    pos.z + vel.z * Dt);
                posNext = new fp3(posNext.x,
                    ground.SampleHeight(posNext.x, posNext.z, surface) + ballRadius,
                    posNext.z);

                // ── Tree trunk collision (putt phase) ─────────────────────────────────
                if (trees != null && trees.TestSegment(pos, posNext, out TreeHit puttTreeHit)
                    && puttTreeHit.IsTrunk)
                {
                    fp dotXZ = vel.x * puttTreeHit.NormalXZ.x + vel.z * puttTreeHit.NormalXZ.z;
                    fp3 velReflected = new fp3(
                        vel.x - fp.FromInt(2) * dotXZ * puttTreeHit.NormalXZ.x,
                        vel.y,
                        vel.z - fp.FromInt(2) * dotXZ * puttTreeHit.NormalXZ.z);
                    fp restitution = puttTreeHit.Profile.TrunkRestitution;
                    vel = new fp3(
                        velReflected.x * restitution,
                        velReflected.y,
                        velReflected.z * restitution);
                    posNext = puttTreeHit.HitPos;
                    posNext = new fp3(posNext.x,
                        ground.SampleHeight(posNext.x, posNext.z, surface) + ballRadius,
                        posNext.z);
                }

                t   = t + Dt;
                pos = posNext;
#if UNITY_EDITOR
                CheckTerrainInvariant(ground, surface, pos);
#endif
                samples.Add(new TrajectorySample(t, pos, vel));

                fp speedSq    = fpMath.Dot(vel, vel);
                fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
                // Phase A C.1+C.2 fix: tolerance window on clause 2 (same fix as RunRollPhase).
                // 5% of stopSpeed² absorbs fp-rounding noise without admitting genuine slope
                // re-acceleration. See RunRollPhase comment for full derivation.
                fp stopEpsilon = stopThresh * fp.FromFloat(0.05f);
                if (speedSq < stopThresh && speedSq <= prevSpeedSq + stopEpsilon)
                {
                    stopConsecutive++;
                    if (stopConsecutive >= StopStepsRequired)
                    {
                        hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
#if UNITY_EDITOR
                        if (DiagShotLogger != null)
                            DiagShotLogger(
                                $"[ShotExit] termination={TerminationReason.BallStopped} " +
                                $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                                $"finalT={t.ToFloat():F2}s samples={samples.Count} hits={hits.Count}");
#endif
                        return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
                    }
                }
                else stopConsecutive = 0;
                prevSpeedSq = speedSq;

                if (pos.x > WorldBound || pos.x < -WorldBound ||
                    pos.z > WorldBound || pos.z < -WorldBound)
                    return new Trajectory(samples, pos, vel, t, TerminationReason.ExitedWorldBounds, hits);
            }

            hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, SurfaceType.Green, true));
#if UNITY_EDITOR
            if (DiagShotLogger != null)
                DiagShotLogger(
                    $"[ShotExit] termination={TerminationReason.BallStopped} " +
                    $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
                    $"finalT={t.ToFloat():F2}s samples={samples.Count} hits={hits.Count}");
#endif
            return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
        }

        // ── Acceleration helpers ──────────────────────────────────────────────────────

        private static fp3 Accel(fp3 vel, fp3 wind, SpinState spin, AeroConfig cfg)
        {
            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);
            fp3 aeroForce = AeroModel.ComputeAeroForce(vel, wind, spin, cfg);
            if (cfg.BallMass <= fp.Epsilon) return gravity;
            fp3 aeroAccel = aeroForce / cfg.BallMass;
            return gravity + aeroAccel;
        }

        private static fp3 Accel(fp3 vel, SpinState spin, AeroConfig cfg)
            => Accel(vel, fp3.Zero, spin, cfg);
    }
}
