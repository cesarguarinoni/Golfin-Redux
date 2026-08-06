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
            => Simulate(input, ground, aero, wind, surfaces, surfaceCfg, puttCfg, ballMods,
                        trees, CupSpec.Disabled);

        // ── Phase 8 (in-sim cup capture + lip-out) ────────────────────────────────────

        /// <summary>
        /// Phase 8 entry (cup_capture_and_lipout, 2026-08-05). Adds a <see cref="CupSpec"/> so
        /// the roll and putt integrators know the cup exists and can actually change the ball's
        /// path at it:
        ///   CAPTURE — speed at or below cup.CaptureSpeed while over the open cup: integration
        ///             terminates there and a fall-in is synthesized (§4.4), so the animation
        ///             shows the ball dropping below the lip and the trajectory ENDS at the cup.
        ///             Termination is <see cref="TerminationReason.CupCapture"/>.
        ///   LIP-OUT — speed above the gate while crossing the cup mouth: one deterministic
        ///             velocity deflection off the far rim (§4.5), then normal roll physics.
        ///
        /// cup.Enabled == false (CupSpec.Disabled) → behaviour is bit-exact identical to the
        /// Phase 7 9-arg path. That is the blocking determinism gate for this phase, mirroring
        /// the trees=null gate of Phase 7 and the Neutral gate of Phase 6.
        ///
        /// Scope: roll and putt phases only. An airborne ball landing straight in the cup
        /// (chip-in) is NOT handled here — it needs entry-angle gating and is v2 backlog. The
        /// post-hoc RealCupDetector scan in BallStateMachine still covers that case via its
        /// own height gate, so behaviour there is unchanged.
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
            ITreeObstacleProvider trees,
            in CupSpec cup)
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
                                    aero.BallRadius, samples, hits, ballMods, trees, cup);
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
                                        aero.BallRadius, samplesList, hitsList, ballMods, trees, cup);
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

        // ── In-sim cup: geometry, capture, lip-out ────────────────────────────────────
        // cup_capture_and_lipout (2026-08-05). All of this is dead code when
        // cup.Enabled == false, which is what keeps the legacy path bit-exact.

        /// <summary>
        /// Once the ball has lipped out, it must leave the mouth by this margin before another
        /// impulse may fire. Guarantees one impulse per crossing (§4.5 single-fire rule).
        /// </summary>
        private static readonly fp LipRearmClearance = fp.FromFloat(0.02f); // 20 mm past the rim

        private enum CupStepAction { None, Capture, LipOut }

        /// <summary>Per-run cup bookkeeping carried through a roll/putt integration.</summary>
        private struct CupRunState
        {
            public bool LipFired;   // an impulse has fired for the current mouth crossing
            public bool HopActive;  // rendering the post-lip-out vertical hop
            public fp   HopT;       // time since the hop started
            public fp   HopDip;     // pop velocity (m/s) imparted by the rim on the lip-out
        }

        /// <summary>
        /// §4.3 per-step cup zone test. Returns what the integrator should do this step and
        /// maintains the single-fire lip-out latch. Zone tests use the closest point on the
        /// prev→curr XZ segment, so a fast step that straddles the cup still registers.
        ///
        /// XZ-only by design (§4.2): in roll/putt the ball is on the ground by construction
        /// (pos.y = SampleHeight + ballRadius), so there is no Y test — which also sidesteps
        /// the height-gate fragility measured in RealCupDetector. On Hole 6 the baked green
        /// height varies ±1.2 mm across the 33 mm capture disc while the gate ceiling is the
        /// authored pin Y, so 20 of 39 in-radius samples were being rejected on height alone.
        /// See Docs/Physics/CUP_CAPTURE_STEP0_DIAGNOSIS.md.
        /// </summary>
        private static CupStepAction CupStep(in fp3 pos, in fp3 posNext, in fp3 vel,
                                             fp ballRadius, in CupSpec cup, ref CupRunState st)
        {
            long distSqRaw = CupSegmentDistSqRaw(pos, posNext, cup.Pin, cup.Radius.raw);

            // Re-arm the single-fire lip-out once the ball is clear of the mouth (§4.5).
            if (st.LipFired)
            {
                long clearRaw = cup.Radius.raw + LipRearmClearance.raw;
                if (distSqRaw > clearRaw * clearRaw) st.LipFired = false;
            }

            long mouthRaw = cup.Radius.raw;
            if (distSqRaw >= mouthRaw * mouthRaw) return CupStepAction.None;

            fp speedSq = fpMath.Dot(vel, vel);
            fp gateSq  = cup.CaptureSpeed * cup.CaptureSpeed;

            // Capture uses the same effective radius as RealCupDetector: the ball centre must
            // be inside (cupRadius − ballRadius) for the ball to fit through the mouth.
            long effRaw = cup.Radius.raw - ballRadius.raw;
            bool captureZone = effRaw > 0 && distSqRaw < effRaw * effRaw;

            // A ball that has already lipped out on THIS crossing may not be captured on the
            // way back out, even though the deflection dropped it under the gate (a 1.55 m/s
            // putt leaves the rim at 1.08 m/s and is still over the mouth). Capturing there
            // would read on screen as the ball vanishing at the rim with no visible deflection,
            // and would make the speed gate cosmetic for everything up to ~2.1 m/s. The latch
            // clears once the ball is clear of the mouth, so §4.5's "may come back and drop on
            // the rebound" still works — it just has to actually come back.
            if (captureZone && speedSq <= gateSq && !st.LipFired) return CupStepAction.Capture;
            if (speedSq > gateSq && !st.LipFired)
            {
                st.LipFired = true;
                return CupStepAction.LipOut;
            }
            // Slow graze in the lip ring (at or below the gate but outside effRadius): no
            // interaction in v1 — the ball rolls past exactly as it does today (§4.3).
            return CupStepAction.None;
        }

        /// <summary>
        /// Display-only vertical offset for the post-lip-out hop. The roll/putt integrators
        /// snap the ball to the surface every step and project out the normal velocity
        /// component, so a real vy would be erased the same step (§4.5 fallback). Emitting the
        /// pop as a short ballistic offset on the SAMPLE keeps the rattle visible without
        /// touching the physics state. ~4.6 mm peak over ~0.06 s at the default 0.30 m/s.
        /// </summary>
        private static fp3 ApplyLipHop(in fp3 pos, in CupSpec cup, ref CupRunState st)
        {
            if (!st.HopActive) return pos;
            st.HopT = st.HopT + Dt;
            // HopDip now carries the pop VELOCITY (m/s) computed at the impulse from the
            // horizontal speed the rim removed — see ComputeLipPopVy.
            fp yOff = st.HopDip * st.HopT + (Gravity * st.HopT * st.HopT) / Two;
            if (yOff <= fp.Zero) { st.HopActive = false; return pos; }
            return new fp3(pos.x, pos.y + yOff, pos.z);
        }

        /// <summary>
        /// Squared XZ distance from the cup centre to the segment a→b, returned in fp-raw²
        /// units (i.e. Q32.32) so callers can compare against radius.raw * radius.raw.
        ///
        /// Deliberately computed in exact long integer arithmetic on fp.raw rather than in fp:
        /// at the 1/240 s step a 1.5 m/s ball moves ~6 mm, whose squared length is only ~2
        /// fp16.16 LSBs. The textbook `t = dot/lenSq` parameterisation divides by that and
        /// loses almost all of its precision. Integer ops are exact AND deterministic across
        /// platforms, so this is both more accurate and no weaker on the determinism contract.
        ///
        /// Segment (not endpoint) test per §4.3: putt steps are far smaller than the 108 mm
        /// mouth, but roll-phase entry speeds can be high enough to straddle the cup in one dt.
        /// </summary>
        private static long CupSegmentDistSqRaw(in fp3 a, in fp3 b, in fp3 pin, long radiusRaw)
        {
            long apx = a.x.raw - pin.x.raw, apz = a.z.raw - pin.z.raw;
            long bpx = b.x.raw - pin.x.raw, bpz = b.z.raw - pin.z.raw;

            long dSqA = apx * apx + apz * apz;
            long dSqB = bpx * bpx + bpz * bpz;
            long best = dSqA < dSqB ? dSqA : dSqB;

            long dx = bpx - apx, dz = bpz - apz;
            long segSq = dx * dx + dz * dz;
            if (segSq == 0) return best;

            // Cheap reject: the segment cannot reach the cup unless the nearer endpoint is
            // within (segLen + radius). Squared and loosened via (p+q)² ≤ 2(p²+q²), which is
            // conservative — it never rejects a real intersection. Also bounds the magnitudes
            // entering the cross product below, so `cross` cannot overflow.
            long reachSq = 2 * (segSq + radiusRaw * radiusRaw);
            if (best > reachSq) return best;

            // Closest point is interior only when the projection parameter lies in (0,1).
            // t = -(AP·D)/|D|²  →  test the numerator against segSq, no division needed.
            long num = -(apx * dx + apz * dz);
            if (num <= 0 || num >= segSq) return best;

            // Interior: perpendicular distance = |cross| / segLen. Divide by an exact integer
            // sqrt instead of squaring the cross product (which would be Q64.64 and overflow).
            // The division loses < 1 raw unit (~15 µm) — negligible against a 54 mm mouth.
            long segLen = ISqrt(segSq);
            if (segLen == 0) return best;
            long cross = dx * apz - dz * apx;
            if (cross < 0) cross = -cross;
            long perp = cross / segLen;
            long perpSq = perp * perp;
            return perpSq < best ? perpSq : best;
        }

        /// <summary>
        /// Test/diagnostic seam over the cup segment-distance test the integrators use (§4.3).
        /// Returns the XZ distance from <paramref name="pin"/> to the segment a→b.
        ///
        /// Exposed because the tunneling guard is otherwise unreachable from the public API:
        /// on a Green the bounce loop's tangential friction bleeds a shot down to well under
        /// 1 m/s before the roll phase starts, so no step ever straddles the 108 mm mouth in
        /// practice. The guard is defensive, and this is how it gets verified. Pure geometry,
        /// no state — safe to expose.
        /// </summary>
        public static fp CupDistanceToSegmentXZ(fp3 a, fp3 b, fp3 pin, fp cupRadius)
            => fp.FromRaw(ISqrt(CupSegmentDistSqRaw(a, b, pin, cupRadius.raw)));

        /// <summary>Exact floor(sqrt(n)) for n ≥ 0 via integer Newton. Pure integer → deterministic.</summary>
        private static long ISqrt(long n)
        {
            if (n <= 0) return 0;
            long x = n, y = (x + 1) >> 1;
            while (y < x) { x = y; y = (x + n / x) >> 1; }
            return x;
        }

        /// <summary>
        /// How far the ball sinks into the open mouth while crossing it, expressed as a
        /// fraction of the ball radius and clamped to [0,1]. This is what makes the lip-out
        /// speed-dependent, and it is the physical quantity the whole interaction hinges on.
        ///
        /// While the ball's centre is over the open mouth it is unsupported and falls freely:
        ///     t_over = chord / speed,  chord = 2·√(R² − off²),  dip = ½·g·t_over²
        /// where `off` is the perpendicular distance of the crossing from the cup centre.
        /// A ball that has fallen a full ball-radius by the time it reaches the far wall is
        /// caught; one that has barely dipped skims over the top and is hardly touched.
        ///
        /// Sanity check against the architect-locked gate: on a dead-centre crossing
        /// (chord = 108 mm) dip reaches one ball radius at ≈1.5 m/s — which is exactly the
        /// USGA/Penner capture speed this project already uses. The model reproduces the
        /// locked constant rather than contradicting it.
        /// Source: Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics
        /// 80(2): 83–96 (capture/lip-out analysis).
        ///
        /// Replaces the original §4.5 behaviour, which applied the SAME 30% speed loss and the
        /// same reversal at every speed. Measured on the first implementation: a dead-centre
        /// crossing at 2.9 m/s came straight back at 2.03 m/s (180°, ratio 0.700 at every
        /// offset) — a squash-ball-off-a-wall read, not a lip-out. It also made the 1.5 m/s
        /// gate a cliff: 1.49 m/s dropped, 1.51 m/s returned at 70% pace.
        /// </summary>
        /// <summary>
        /// Perpendicular distance from the cup centre to the ball's LINE of travel (XZ).
        ///
        /// This — not the distance at the trigger step — is the crossing offset that sets the
        /// free-fall chord. The lip-out fires on the first step whose segment enters the mouth,
        /// where the ball is by definition ≈ one cup radius from the centre; feeding that in
        /// would give chord = 2·√(R² − R²) ≈ 0 and hence dip ≈ 0 for every crossing, silently
        /// disabling the interaction. Measured while building this: every dead-centre crossing
        /// from 1.6–4.0 m/s reported 0° deflection because of exactly that.
        /// </summary>
        private static fp LipCrossingOffset(in fp3 pos, in fp3 vel, in fp3 pin)
        {
            fp vx = vel.x, vz = vel.z;
            fp vLenSq = vx * vx + vz * vz;
            if (vLenSq <= fp.Zero) return fp.Zero;
            fp vLen = fpMath.Sqrt(vLenSq);
            fp dx = pin.x - pos.x;
            fp dz = pin.z - pos.z;
            // |cross(d, v)| / |v| — distance from the pin to the infinite line through pos along v.
            fp cross = dx * vz - dz * vx;
            if (cross < fp.Zero) cross = -cross;
            return cross / vLen;
        }

        private static fp ComputeLipDipFraction(fp offsetDist, fp speed, fp cupRadius, fp ballRadius)
        {
            if (speed <= fp.Epsilon || ballRadius <= fp.Zero) return fp.Zero;

            fp rSq   = cupRadius * cupRadius;
            fp offSq = offsetDist * offsetDist;
            if (offSq >= rSq) return fp.Zero;              // grazing the rim: no free-fall span

            fp halfChord = fpMath.Sqrt(rSq - offSq);
            fp chord     = halfChord * Two;

            fp tOver = chord / speed;
            fp dip   = (-Gravity) * (tOver * tOver) / Two; // Gravity is negative; use magnitude

            fp frac = dip / ballRadius;
            if (frac <= fp.Zero) return fp.Zero;
            return frac > fp.One ? fp.One : frac;
        }

        /// <summary>
        /// §4.5 lip-out: one deterministic deflection off the rim, scaled by how far the ball
        /// actually sank into the mouth (<see cref="ComputeLipDipFraction"/>).
        ///
        /// Decompose the horizontal velocity about n = the XZ unit vector pin→ball, then blend
        /// the radial component between "passes straight over" and "bounces off the far wall":
        ///     vRad' = vRad · (1 − dip·(1 + LipRestitution))
        ///     vTan' = vTan · (1 − dip·(1 − LipSpeedDamping))
        ///
        ///   dip → 0  (fast skim):      both factors → 1, the ball runs over the top of the
        ///                              hole essentially untouched — the real "went straight
        ///                              over it" outcome.
        ///   dip → 1  (just over gate): vRad' = −LipRestitution·vRad, a genuine rebound off the
        ///                              far wall, and the tangential component takes the full
        ///                              rim friction. This is the violent rattle.
        ///
        /// LipRestitution now genuinely governs the rebound magnitude. In the previous version
        /// the result was rescaled to LipSpeedDamping·|v| unconditionally, so LipRestitution
        /// only ever set the direction and the outgoing speed was 0.700·|v| at every offset and
        /// every speed — tuning it down could not soften the bounce at all.
        ///
        /// Vertical pop is NOT applied to the velocity — see the hop offset in the integrators.
        /// </summary>
        /// <summary>
        /// Outward unit normal of the cup wall at the point where the ball's chord EXITS the
        /// mouth — i.e. the bit of wall it actually hits.
        ///
        /// This is what produces a lateral kick. The deflection used to be taken about the
        /// entry radial (pin→ball at the trigger step), which for a straight crossing is very
        /// nearly anti-parallel to the direction of travel: the tangential component came out
        /// ~0, so the ball could only ever be slowed straight down its own line (measured: ≤4°
        /// of deflection at every offset). The far wall's normal is angled away from the line
        /// of travel by roughly asin(off/R), so an off-centre crossing now gets a real sideways
        /// push — the horseshoe / spin-out read.
        ///
        /// Returns false when the geometry degenerates (zero speed, or the ball is already
        /// outside the mouth), in which case the caller leaves the velocity alone.
        /// </summary>
        private static bool TryCupExitNormal(in fp3 pos, in fp3 vel, in CupSpec cup, out fp3 n)
        {
            n = fp3.Zero;
            fp vLenSq = vel.x * vel.x + vel.z * vel.z;
            if (vLenSq <= fp.Zero) return false;
            fp vLen = fpMath.Sqrt(vLenSq);
            fp dx = vel.x / vLen, dz = vel.z / vLen;

            fp wx = pos.x - cup.Pin.x, wz = pos.z - cup.Pin.z;
            fp b = wx * dx + wz * dz;
            fp c = wx * wx + wz * wz - cup.Radius * cup.Radius;
            fp disc = b * b - c;
            if (disc < fp.Zero) return false;

            fp s = fpMath.Sqrt(disc) - b;                 // forward root: where the chord leaves
            fp ex = wx + dx * s, ez = wz + dz * s;        // exit point relative to the cup centre
            fp eLenSq = ex * ex + ez * ez;
            if (eLenSq <= fp.Zero) return false;
            fp eLen = fpMath.Sqrt(eLenSq);
            n = new fp3(ex / eLen, fp.Zero, ez / eLen);
            return true;
        }

        private static fp3 ApplyLipOut(in fp3 vel, in fp3 pos, in CupSpec cup, fp dipFraction)
        {
            fp3 velXZ = new fp3(vel.x, fp.Zero, vel.z);
            fp speedSq = fpMath.Dot(velXZ, velXZ);
            if (speedSq <= fp.Zero || dipFraction <= fp.Zero) return vel;

            // Radial outcome hinges on whether the ball has sunk far enough to STRIKE the far
            // wall (dip ≥ 1 ball-radius, i.e. below its own equator) or merely clips the rim on
            // the way over:
            //   dip  < 1 : clears the far rim — keeps going forward, bled down toward
            //              LipRestitution of its radial pace as the clip gets heavier.
            //   dip >= 1 : hits the wall — rebounds at LipRestitution, i.e. comes back out.
            // Magnitudes match across that boundary and only the SIGN flips, which is what a
            // marginal lip-out actually looks like: it either just gets through or just comes
            // back, at similar pace.
            //
            // Deliberately NOT a linear blend from +vRad to −e·vRad. That form crosses zero at
            // dip ≈ 0.74 and stopped the ball dead on the rim — measured 1.945 m/s in, 0.083
            // m/s out, which is not a thing a golf ball does.
            fp radFactor = dipFraction >= fp.One
                ? -cup.LipRestitution
                : fp.One - dipFraction * (fp.One - cup.LipRestitution);
            fp tanFactor = fp.One - dipFraction * (fp.One - cup.LipSpeedDamping);

            // Split about the FAR WALL's normal — the surface the ball actually strikes.
            if (!TryCupExitNormal(pos, velXZ, cup, out fp3 n)) return vel;

            fp vn = velXZ.x * n.x + velXZ.z * n.z;               // outward (toward the far wall)
            fp3 vNorm = new fp3(n.x * vn, fp.Zero, n.z * vn);
            fp3 vTan  = velXZ - vNorm;
            fp3 vOut  = vNorm * radFactor + vTan * tanFactor;
            return new fp3(vOut.x, vel.y, vOut.z);
        }

        /// <summary>
        /// Upward pop imparted by the far wall, as a velocity. Scaled by the horizontal speed
        /// the rim actually took out of the ball: hitting an angled wall converts part of the
        /// horizontal impulse into vertical, so a heavy clip pops and a clean skim does not.
        ///
        /// <see cref="CupSpec.LipPopVy"/> is the conversion FRACTION (dimensionless), not an
        /// absolute m/s — see its doc comment. A clip that costs the ball 0.6 m/s at the
        /// default 1.0 gives ≈0.6 m/s up, i.e. a ~2 cm hop, which is what a real rattled putt
        /// looks like. The previous absolute 0.30 m/s scaled by dip produced a 0.4 mm hop at
        /// speed — invisible.
        /// </summary>
        private static fp ComputeLipPopVy(in fp3 velIn, in fp3 velOut, in CupSpec cup)
        {
            fp inSpeed  = fpMath.Sqrt(velIn.x * velIn.x + velIn.z * velIn.z);
            fp outSpeed = fpMath.Sqrt(velOut.x * velOut.x + velOut.z * velOut.z);
            fp lost = inSpeed - outSpeed;
            if (lost <= fp.Zero) return fp.Zero;
            return lost * cup.LipPopVy;
        }

        /// <summary>
        /// §4.4 capture: truncate at the capture step and synthesize the fall-in, so the
        /// animator naturally shows the ball dropping below the lip. Y falls under gravity
        /// (explicit Euler on vy — monotonic, and immune to the fp precision cliff a τ² form
        /// hits at small τ); XZ lerps from the capture point to the pin over the same window.
        /// Pure fp: no Random, no Time. Appends the terminal stop hit and returns CupCapture.
        /// </summary>
        private static Trajectory FinishCupCapture(
            fp3 capPos, fp3 capVel, fp capT, in CupSpec cup, fp ballRadius,
            List<TrajectorySample> samples, List<TerrainHit> hits)
        {
            // Ball centre rests on the cup floor: pin.y − depth + ballRadius.
            fp bottomY = cup.Pin.y - cup.Depth + ballRadius;
            fp3 bottom = new fp3(cup.Pin.x, bottomY, cup.Pin.z);

            // Guard against a degenerate spec (depth ~0 / ball already below the floor):
            // emit the terminal hit directly rather than looping forever.
            if (capPos.y <= bottomY)
            {
                hits.Add(new TerrainHit(capT, bottom, capVel, fp3.Zero, SurfaceType.Green, true));
                return new Trajectory(samples, bottom, fp3.Zero, capT, TerminationReason.CupCapture, hits);
            }

            fp dropTotal = capPos.y - bottomY;
            fp vy = fp.Zero;
            fp y  = capPos.y;
            fp t  = capT;
            fp3 p = capPos;

            // Cap the fall at 1 s of steps — a 0.10 m drop takes ~0.14 s (34 steps); the cap is
            // pure belt-and-braces against a pathological Depth value.
            const int MaxFallSteps = 240;
            for (int i = 0; i < MaxFallSteps; i++)
            {
                vy = vy + Gravity * Dt;      // Gravity is negative
                y  = y + vy * Dt;
                t  = t + Dt;

                if (y <= bottomY)
                {
                    p = bottom;
                    samples.Add(new TrajectorySample(t, p, fp3.Zero));
                    break;
                }

                // XZ eases toward the pin in proportion to how far the ball has fallen, so the
                // ball is centred over the cup by the time it reaches the floor.
                fp fallen = capPos.y - y;
                fp s = fallen / dropTotal;                    // 0 → 1 across the drop
                fp3 xz = new fp3(
                    capPos.x + (cup.Pin.x - capPos.x) * s,
                    y,
                    capPos.z + (cup.Pin.z - capPos.z) * s);
                p = xz;
                samples.Add(new TrajectorySample(t, p, new fp3(fp.Zero, vy, fp.Zero)));
            }

            hits.Add(new TerrainHit(t, bottom, capVel, fp3.Zero, SurfaceType.Green, true));
#if UNITY_EDITOR
            if (DiagShotLogger != null)
                DiagShotLogger(
                    $"[ShotExit] termination={TerminationReason.CupCapture} " +
                    $"finalPos=({bottom.x.ToFloat():F2},{bottom.y.ToFloat():F2},{bottom.z.ToFloat():F2}) " +
                    $"finalT={t.ToFloat():F2}s samples={samples.Count} hits={hits.Count} " +
                    $"captureSpeed={fpMath.Sqrt(fpMath.Dot(capVel, capVel)).ToFloat():F3}m/s " +
                    $"dropDist={dropTotal.ToFloat():F3}m");
#endif
            return new Trajectory(samples, bottom, fp3.Zero, t, TerminationReason.CupCapture, hits);
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
            BallPhysicsModifiers ballMods, ITreeObstacleProvider trees, in CupSpec cup)
        {
            fp3 pos = startPos;
            fp3 vel = startVel;
            fp  t   = startT;

            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);
            var cupState = new CupRunState();

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

                // ── In-sim cup (roll phase) ───────────────────────────────────────────
                // Runs on the prev→next segment before the tree test: a tree inside the cup
                // is not a real layout, so the ordering between them is immaterial.
                if (cup.Enabled)
                {
                    var cupAct = CupStep(pos, posNext, vel, ballRadius, cup, ref cupState);
                    if (cupAct == CupStepAction.Capture)
                    {
                        t = t + Dt;
                        samples.Add(new TrajectorySample(t, posNext, vel));
                        return FinishCupCapture(posNext, vel, t, cup, ballRadius, samples, hits);
                    }
                    if (cupAct == CupStepAction.LipOut)
                    {
                        fp speedNow = fpMath.Sqrt(fpMath.Dot(new fp3(vel.x, fp.Zero, vel.z),
                                                             new fp3(vel.x, fp.Zero, vel.z)));
                        fp lipOffset = LipCrossingOffset(pos, vel, cup.Pin);
                        fp dip = ComputeLipDipFraction(lipOffset, speedNow, cup.Radius, ballRadius);
                        fp3 velBefore = vel;
                        vel = ApplyLipOut(vel, posNext, cup, dip);
                        cupState.HopActive = true;
                        cupState.HopT      = fp.Zero;
                        cupState.HopDip    = ComputeLipPopVy(velBefore, vel, cup);
                        // Re-integrate this step from the deflected velocity so the ball
                        // actually changes direction AT the rim rather than one step later.
                        posNext = new fp3(pos.x + vel.x * Dt, fp.Zero, pos.z + vel.z * Dt);
                        posNext = new fp3(posNext.x,
                            ground.SampleHeight(posNext.x, posNext.z, surface) + ballRadius,
                            posNext.z);
                    }
                }

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
                samples.Add(new TrajectorySample(t, ApplyLipHop(pos, cup, ref cupState), vel));

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
            BallPhysicsModifiers ballMods, ITreeObstacleProvider trees, in CupSpec cup)
        {
            fp3 pos = startPos;
            fp3 vel = startVel;
            fp  t   = startT;

            fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);
            var cupState = new CupRunState();

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

                // ── In-sim cup (putt phase) ───────────────────────────────────────────
                // This is the path that fixes the reported bug: a putt arriving at or below
                // CupCaptureSpeed now TERMINATES here with a synthesized drop, instead of
                // rolling on past the hole for several more seconds.
                if (cup.Enabled)
                {
                    var cupAct = CupStep(pos, posNext, vel, ballRadius, cup, ref cupState);
                    if (cupAct == CupStepAction.Capture)
                    {
                        t = t + Dt;
                        samples.Add(new TrajectorySample(t, posNext, vel));
                        return FinishCupCapture(posNext, vel, t, cup, ballRadius, samples, hits);
                    }
                    if (cupAct == CupStepAction.LipOut)
                    {
                        fp speedNow = fpMath.Sqrt(fpMath.Dot(new fp3(vel.x, fp.Zero, vel.z),
                                                             new fp3(vel.x, fp.Zero, vel.z)));
                        fp lipOffset = LipCrossingOffset(pos, vel, cup.Pin);
                        fp dip = ComputeLipDipFraction(lipOffset, speedNow, cup.Radius, ballRadius);
                        fp3 velBefore = vel;
                        vel = ApplyLipOut(vel, posNext, cup, dip);
                        cupState.HopActive = true;
                        cupState.HopT      = fp.Zero;
                        cupState.HopDip    = ComputeLipPopVy(velBefore, vel, cup);
                        posNext = new fp3(pos.x + vel.x * Dt, fp.Zero, pos.z + vel.z * Dt);
                        posNext = new fp3(posNext.x,
                            ground.SampleHeight(posNext.x, posNext.z, surface) + ballRadius,
                            posNext.z);
                    }
                }

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
                samples.Add(new TrajectorySample(t, ApplyLipHop(pos, cup, ref cupState), vel));

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
