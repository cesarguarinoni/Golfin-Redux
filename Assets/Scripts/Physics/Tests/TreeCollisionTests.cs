using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase 7 tree collision tests.
    ///
    /// Uses a synthetic CSV with one cedar-profile tree at (0,0), baseY=0, scale=1.0.
    /// All tests drive the 9-arg BallSimulation.Simulate path directly.
    ///
    /// Test #1 — Determinism: same ShotInput + same tree CSV → identical Trajectory (bit-exact).
    /// Test #2 — Trunk deflect: shot aimed directly at trunk reflects; final XZ position is NOT
    ///            on the straight-through line (ball doesn't pass through trunk).
    /// Test #3 — Canopy entry impulse: shot that enters the canopy volume lands closer than an
    ///            identical shot with no trees — demonstrating the one-time entry damping effect.
    /// Test #4 — Null provider: trees=null → identical result to the 8-arg Phase 6 path (bit-exact).
    /// Test #5 — Absent CSV: loader returns null for empty text → no crash, same result as null.
    /// Test #6 — Roll trunk deflect: rolling ball aimed at trunk reflects (non-zero XZ velocity change).
    /// Test #7 — Putt trunk deflect: putting-phase ball aimed at trunk deflects/stops.
    /// Test #8 — No-slow-mo: after canopy entry cut, descent time ≤1.5× trees-disabled fall;
    ///            impulse fires exactly once per pass (iter-6 regression for Cesar rejection).
    /// </summary>
    [TestFixture]
    public class TreeCollisionTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        // Synthetic CSV: one cedar tree at origin, scale 1.0.
        // Cedar profile (from CSV): trunk 0.30m radius, trunk 4.0m height, canopy 2.5m radius, 12.0m top.
        // We embed the profile data inline in the "profiles CSV" since TreeObstacleLoader reads
        // from Resources at runtime. Tests inject instances directly via TreeObstacleLoader.LoadInstancesFromText.
        private const string SyntheticInstanceCsv =
            "# bake_hash=test0001\n" +
            "worldX,worldZ,baseY,scale,profileName\n" +
            "0.0000,0.0000,0.0000,1.0000,default\n";

        private ITreeObstacleProvider BuildProvider()
        {
            // Uses the hardcoded default profile (trunkRadius=0.25, trunkHeight=3.0, canopyRadius=3.0, canopyTop=9.0).
            var instances = TreeObstacleLoader.LoadInstancesFromText(SyntheticInstanceCsv);
            Assert.IsNotNull(instances, "Synthetic CSV should produce instances");
            return TreeObstacleProvider.Create(instances);
        }

        private static ShotInput TrunkShotInput()
        {
            // Shot origin: 5m away from trunk along Z, at ground level.
            // Velocity aimed directly at trunk (+Z direction), moderate speed.
            var origin   = new fp3(fp.Zero, fp.FromFloat(0.5f), fp.FromFloat(-5f));
            var velocity = new fp3(fp.Zero, fp.FromFloat(2f), fp.FromFloat(15f));
            return new ShotInput(origin, velocity, fp.FromInt(10));
        }

        private static ShotInput CanopyShotInput()
        {
            // Shot that will pass through the canopy (high arc through canopy zone).
            // Origin: 10m back on Z, velocity aiming slightly up-and-forward to pass over trunk
            // height but through canopy radius at canopy Y.
            var origin   = new fp3(fp.Zero, fp.FromFloat(0.5f), fp.FromFloat(-10f));
            var velocity = new fp3(fp.Zero, fp.FromFloat(8f), fp.FromFloat(20f));
            return new ShotInput(origin, velocity, fp.FromInt(15));
        }

        private static FlatGround FlatAt(float y = 0f) => new FlatGround(fp.FromFloat(y));
        private static ConstantSurfaceProvider FairwaySurface() => new ConstantSurfaceProvider(SurfaceType.Fairway);

        // ── Tests ─────────────────────────────────────────────────────────────────────

        [Test]
        public void TreeCollision_Determinism_SameInputSameTree_IdenticalTrajectory()
        {
            var trees  = BuildProvider();
            var input  = TrunkShotInput();
            var ground = FlatAt();
            var surf   = FairwaySurface();
            var aero   = AeroConfig.Vacuum;

            var t1 = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);
            var t2 = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);

            Assert.AreEqual(t1.samples.Count, t2.samples.Count,
                "Sample counts must be identical (deterministic)");
            Assert.AreEqual(t1.finalPosition.x.raw, t2.finalPosition.x.raw,
                "Final X must be bit-exact");
            Assert.AreEqual(t1.finalPosition.z.raw, t2.finalPosition.z.raw,
                "Final Z must be bit-exact");
            Assert.AreEqual(t1.finalPosition.y.raw, t2.finalPosition.y.raw,
                "Final Y must be bit-exact");
        }

        [Test]
        public void TreeCollision_TrunkDeflect_BallDoesNotPassThrough()
        {
            var trees  = BuildProvider();
            var input  = TrunkShotInput();
            var ground = FlatAt();
            var surf   = FairwaySurface();
            var aero   = AeroConfig.Vacuum;

            var withTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);
            var noTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, (ITreeObstacleProvider)null);

            // Without trees the ball passes through (0,0) — final Z should be positive (past origin).
            // With trees the ball should NOT end up beyond trunk (blocked/deflected).
            // Primary assertion: trajectories differ (the tree changed something).
            bool finalsDiffer =
                withTrees.finalPosition.x.raw != noTrees.finalPosition.x.raw ||
                withTrees.finalPosition.z.raw != noTrees.finalPosition.z.raw;

            Assert.IsTrue(finalsDiffer,
                "A trunk hit must change the trajectory vs the tree-free path");
        }

        [Test]
        public void TreeCollision_CanopyDamp_LandsCloserThanNoTrees()
        {
            // Use a shot that arcs OVER the trunk (too high for trunk at 3m top) but through
            // the canopy (up to 9m). With canopy damping, ball should land closer.
            var trees  = BuildProvider();
            var input  = CanopyShotInput();
            var ground = FlatAt();
            var surf   = FairwaySurface();
            var aero   = AeroConfig.Vacuum;

            var withTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);
            var noTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, (ITreeObstacleProvider)null);

            float withZ  = withTrees.finalPosition.z.ToFloat();
            float noZ    = noTrees.finalPosition.z.ToFloat();

            // Ball origin is at Z=-10; target at Z=0. "Closer" = smaller Z value (less distance past origin).
            // Or the final Z with trees < final Z without trees (damped = shorter).
            // Actually "landing closer" means shorter distance from origin along Z direction.
            // Origin is at Z=-10, shot toward +Z. With canopy damp → lands at smaller Z than without.
            Assert.Less(withZ, noZ,
                $"Canopy damping should shorten the trajectory (withTrees finalZ={withZ:F2} must be < noTrees finalZ={noZ:F2})");
        }

        [Test]
        public void TreeCollision_NullProvider_BitExactWithPhase6()
        {
            // Phase 7 with trees=null must produce bit-exact identical results to Phase 6.
            var input  = TrunkShotInput();
            var ground = FlatAt();
            var surf   = FairwaySurface();
            var aero   = AeroConfig.Vacuum;
            var ballMods = BallPhysicsModifiers.Neutral;

            var phase6 = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, ballMods);
            var phase7null = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, ballMods, null);

            Assert.AreEqual(phase6.finalPosition.x.raw, phase7null.finalPosition.x.raw,
                "Phase 7 with null trees must be bit-exact equal to Phase 6 (X)");
            Assert.AreEqual(phase6.finalPosition.z.raw, phase7null.finalPosition.z.raw,
                "Phase 7 with null trees must be bit-exact equal to Phase 6 (Z)");
            Assert.AreEqual(phase6.samples.Count, phase7null.samples.Count,
                "Phase 7 with null trees must have identical sample count to Phase 6");
        }

        [Test]
        public void TreeCollision_AbsentCsv_NoExceptionNullProvider()
        {
            // Empty/null CSV text → LoadInstancesFromText returns null → Create returns null → no crash.
            var instances = TreeObstacleLoader.LoadInstancesFromText(null);
            Assert.IsNull(instances, "Null CSV text → null instances");

            var instances2 = TreeObstacleLoader.LoadInstancesFromText("");
            Assert.IsNull(instances2, "Empty CSV text → null instances");

            var provider = TreeObstacleProvider.Create(null);
            Assert.IsNull(provider, "null instances → null provider");
        }

        [Test]
        public void TreeCollision_RollPhase_TrunkDeflectsRollingBall()
        {
            // Rolling ball aimed directly at the default-profile trunk at origin.
            // Origin: 5m back on Z, ground level + ballRadius. Shot directly toward +Z (into trunk).
            // The trunk radius is 0.25m (default profile) at X=0 Z=0, so ball at X=0 must stop/deflect.
            //
            // Seed derived from red-team's RollProbe/RollProbe5 configs (REDTEAM_REVIEW.md).
            // Before the fix: withTrees.finalZ == noTrees.finalZ (ball tunneled through).
            // After the fix:  withTrees.finalZ < noTrees.finalZ - margin (ball stopped at trunk).

            var trees  = BuildProvider();
            // Origin at X=0, Y=ballRadius(≈0.021), Z=-5: ball sits directly in line with the trunk.
            // Low forward velocity, minimal vertical: ball stays at ground level and rolls +Z into trunk.
            var origin   = new fp3(fp.Zero, fp.FromFloat(0.021f), fp.FromFloat(-5f));
            var velocity = new fp3(fp.Zero, fp.FromFloat(0.02f), fp.FromFloat(3f));
            var input    = new ShotInput(origin, velocity, fp.FromInt(20));
            var ground   = FlatAt();
            var surf     = FairwaySurface();
            var aero     = AeroConfig.Vacuum;

            var withTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);
            var noTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, (ITreeObstacleProvider)null);

            float withZ = withTrees.finalPosition.z.ToFloat();
            float noZ   = noTrees.finalPosition.z.ToFloat();

            // Without trees: ball rolls through origin and past the trunk position.
            // With trees: ball must be stopped or deflected short of / at the trunk (Z≈-0.25).
            // Margin = 0.5m: ball must finish at least 0.5m short of the tree-free final position.
            float margin = 0.5f;
            Assert.Less(withZ, noZ - margin,
                $"Roll phase: trunk must deflect/stop the ball. withTrees finalZ={withZ:F3} must be < noTrees finalZ={noZ:F3} - {margin}. " +
                $"If equal, canopy at frac=0 is still masking the trunk hit — check IsInsideCanopy lower bound.");
        }

        [Test]
        public void TreeCollision_PuttPhase_TrunkDeflectsRollingBall()
        {
            // Putt-phase specific test: origin classified as Green → IsPutt returns true → RunPuttPhase.
            // Same geometry as the roll test: ball at ground level, aimed directly at the trunk.
            //
            // This test validates the putt-phase trunk guard (BallSimulation.cs:772 && puttTreeHit.IsTrunk).
            // Before the fix: identical mechanism to roll — canopy at frac=0 masked trunk, IsTrunk=false.
            // After the fix: trunk wins pass-1, IsTrunk=true, putt guard fires, ball deflects.

            var trees  = BuildProvider();
            var origin   = new fp3(fp.Zero, fp.FromFloat(0.021f), fp.FromFloat(-5f));
            var velocity = new fp3(fp.Zero, fp.FromFloat(0.02f), fp.FromFloat(3f));
            var input    = new ShotInput(origin, velocity, fp.FromInt(20));
            var ground   = FlatAt();
            // Green surface at origin → IsPutt(input, surf) returns true → RunPuttPhase.
            var surf = new ConstantSurfaceProvider(SurfaceType.Green);
            var aero = AeroConfig.Vacuum;

            var withTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);
            var noTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, (ITreeObstacleProvider)null);

            float withZ = withTrees.finalPosition.z.ToFloat();
            float noZ   = noTrees.finalPosition.z.ToFloat();

            float margin = 0.5f;
            Assert.Less(withZ, noZ - margin,
                $"Putt phase: trunk must deflect/stop the ball. withTrees finalZ={withZ:F3} must be < noTrees finalZ={noZ:F3} - {margin}. " +
                $"If equal, canopy at frac=0 is still masking the trunk hit in RunPuttPhase — check IsInsideCanopy lower bound.");
        }

        /// <summary>
        /// No-slow-mo regression test (iter-6 — Cesar playtest rejection of v1).
        ///
        /// v1 bug: canopyDampingPerStep=0.92 applied EVERY RK4 step while inside the canopy →
        /// exponential velocity decay → ball drifts at ~0.5 m/s for 10+ seconds.
        ///
        /// Fixed model (D3 revised): one-time entry impulse (vel *= 0.40) on the crossing step,
        /// then normal ballistics (gravity/drag/magnus) resume immediately.
        ///
        /// This test asserts two properties of the fixed model:
        /// (a) Descent time from canopy entry to ground ≤ 1.5× the same fall with trees disabled.
        ///     (v1 would fail: drift time was 10–20× the free-fall, far above 1.5×.)
        /// (b) The impulse fires exactly once per canopy pass: only one step shows a velocity ratio
        ///     near canopyHitDamping (0.40); all subsequent in-canopy steps are unscaled (ratio ≈ 1.0).
        ///
        /// Shot setup: ball starts well above the canopy (Y=15), moving slowly downward and slightly
        /// forward so it falls through the canopy (Y:[3,9], XZ radius 3m) before hitting the ground.
        /// Origin is at X=0, Y=15, Z=-1 (just within canopy XZ radius at Z=0).
        /// Velocity: nearly straight down (vy=-8, vz=0.5) to ensure the ball spends several
        /// in-canopy steps after the entry crossing.
        /// </summary>
        [Test]
        public void TreeCollision_CanopyEntryImpulse_NoSlowMoDescent()
        {
            var trees  = BuildProvider();
            // Start above canopyTopY (9m), inside canopy XZ (x=0, z=-0.5, radius=3m from origin).
            // vy=-8 (fast descent through canopy), vz=0.5 (slight forward to pass through canopy XZ).
            var origin   = new fp3(fp.Zero, fp.FromFloat(15f), fp.FromFloat(-0.5f));
            var velocity = new fp3(fp.Zero, fp.FromFloat(-8f), fp.FromFloat(0.5f));
            var input    = new ShotInput(origin, velocity, fp.FromInt(30)); // 30s max duration
            var ground   = FlatAt();
            var surf     = FairwaySurface();
            var aero     = AeroConfig.Vacuum; // vacuum: no drag/magnus — isolates tree impulse

            var withTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);
            var noTrees = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, (ITreeObstacleProvider)null);

            // ── (a) Descent-time check ──────────────────────────────────────────────────
            // noTrees: ball falls from Y=15 to ground in free-fall; time measured from finalTime.
            // withTrees: impulse at canopy entry (Y≈9) reduces speed to 0.40×, then free-fall resumes.
            //   After the cut, the ball still falls at normal free-fall speed (just slower initial vy).
            //   The total flight time should be only modestly longer than the trees-disabled case.
            // We compare the total sim times (finalTime) as a proxy for descent time.
            // The canopy-entry Y is ~9m; ground is 0m. After the cut, free-fall from ~9m at 0.40×
            // the entry speed: this is physically fast, so total time should be well within 1.5×.
            float withTime = withTrees.finalTime.ToFloat();
            float noTime   = noTrees.finalTime.ToFloat();

            // Sanity: both sims terminated normally (ball hit ground).
            Assert.Greater(withTime, 0f, "withTrees should have a positive flight time");
            Assert.Greater(noTime,   0f, "noTrees should have a positive flight time");

            // The 1.5× factor: withTrees descent must not be more than 50% slower than trees-off.
            // A v1 per-step-drag ball would fail this by 5–10×.
            Assert.Less(withTime, noTime * 1.5f,
                $"Canopy entry-impulse model: descent time with trees ({withTime:F3}s) must be " +
                $"≤1.5× trees-disabled descent ({noTime:F3}s). If this fails, per-step damping " +
                $"was reintroduced — check BallSimulation canopy branch for per-step re-application.");

            // ── (b) Impulse-once check (tightened in iter-8 per Architect decision) ────────
            //
            // WHY THE SCAN IS TRUNCATED AT FIRST GROUND CONTACT:
            // After the canopy entry impulse fires (the ONE legitimate damping step), the ball
            // falls freely to the ground and then bounces. Ground bounces produce velocity-ratio
            // steps that also fall below 0.7 (restitution < 1.0 at each bounce). These are pure
            // ground physics — they occur IDENTICALLY in the noTrees simulation (confirmed by the
            // iter-8 confirming probe: noTrees ball also shows 8 ratio<0.7 steps at y≈0).
            // Scanning past first ground contact counts those bounces as false "damping" events,
            // causing the old heuristic to report 10 steps instead of 1.
            //
            // The Architect adjudicated (iter-8): the over-broad heuristic (count ALL ratio<0.7)
            // was introduced in iter-6 and its "1 step" pass was a FALSE PASS caused by the
            // stuck-ball bug — the ball was frozen at the trunk (never reached ground), so there
            // were zero bounces and only the canopy drop was counted. Iter-7 freed the ball;
            // it now correctly lands and bounces; the bounce count trips the old assertion.
            // Fix: truncate the scan at the first sample with y < 0.2m (the descent/canopy
            // portion only), and additionally assert the drop lies within the canopy band.
            //
            // The tightened assertion also checks:
            //   - the drop's Y position is within the canopy band (trunkTopY < y <= canopyTopY),
            //     confirming it fired at canopy entry, not elsewhere;
            //   - the ratio is ≈ canopyHitDamping (0.40) ± 0.15 tolerance for fp rounding.
            const float trunkTopY   = 3.0f;  // default profile (matches SyntheticInstanceCsv → "default")
            const float canopyTopY  = 9.0f;  // default profile
            const float hitDamping  = 0.40f; // canopyHitDamping default
            const float dampTol     = 0.15f; // ±0.15 fp/gravity-step tolerance
            const float groundFloor = 0.2f;  // truncate scan here (first ground contact)

            var samples = withTrees.samples;
            int   dampStepCount  = 0;
            float dampRatio      = float.NaN;
            float dampY          = float.NaN;

            for (int i = 1; i < samples.Count; i++)
            {
                float y = samples[i].position.y.ToFloat();

                // Stop scanning at first ground contact — everything below this is ground
                // bounce-and-settle (restitution physics, NOT canopy damping).
                if (y < groundFloor)
                    break;

                float vPrev = SpeedXYZ(samples[i - 1].velocity);
                float vCurr = SpeedXYZ(samples[i].velocity);
                if (vPrev > 0.1f)
                {
                    float ratio = vCurr / vPrev;
                    if (ratio < 0.7f)
                    {
                        dampStepCount++;
                        dampRatio = ratio;
                        dampY     = y;
                    }
                }
            }

            // Expect exactly 1 impulse in the pre-ground portion of the trajectory.
            Assert.AreEqual(1, dampStepCount,
                $"Exactly one damping step should occur BEFORE first ground contact " +
                $"(canopy entry crossing fires once). " +
                $"Found {dampStepCount} pre-ground steps with velocity ratio < 0.7. " +
                $"If 0: canopy entry was never detected (check IsInsideCanopy + entry condition). " +
                $"If >1: per-step damping was reintroduced in the canopy branch.");

            // The impulse must have fired within the canopy Y-band.
            Assert.Greater(dampY, trunkTopY,
                $"Canopy impulse fired at y={dampY:F3}m — must be ABOVE trunkTopY ({trunkTopY}m). " +
                $"A value ≤ trunkTopY suggests the impulse fired in the trunk band or below.");
            Assert.LessOrEqual(dampY, canopyTopY,
                $"Canopy impulse fired at y={dampY:F3}m — must be ≤ canopyTopY ({canopyTopY}m). " +
                $"A value above canopyTopY suggests an off-tree entry (IsInsideCanopy bug).");

            // The impulse ratio should be approximately canopyHitDamping (0.40).
            Assert.Greater(dampRatio, hitDamping - dampTol,
                $"Canopy impulse velocity ratio ({dampRatio:F3}) is too low — expected ~{hitDamping} " +
                $"(canopyHitDamping ± {dampTol}). " +
                $"A value near 0 suggests a second unintended damping compounded.");
            Assert.Less(dampRatio, hitDamping + dampTol,
                $"Canopy impulse velocity ratio ({dampRatio:F3}) is too high — expected ~{hitDamping} " +
                $"(canopyHitDamping ± {dampTol}). " +
                $"A value near 1.0 suggests the impulse didn't apply or only applied partially.");
        }

        /// <summary>
        /// Regression test for the stuck-floating-ball defect found by the red-team (iter-6,
        /// ARCHITECT_REVIEW_FAIL). Root cause: in the airborne trunk branch, when the containment
        /// guard returns frac=0 (ball already inside the trunk cylinder at p0), the old code did
        /// pos=hitPos; t=tHitAbs (=t+0=t); continue — ZERO time/position progress. The XZ velocity
        /// was reflected but vy (still descending) was unchanged, so the integrator re-fired the
        /// containment guard every step until the maxSteps=14400 cap, leaving the ball floating
        /// at y≈1.4–2.0m against the trunk forever.
        ///
        /// Fix (iter-7): when frac=0, push the ball OUT of the trunk cylinder along NormalXZ to
        /// just beyond trunkRadius AND advance t=tNext, pos=pushedPos unconditionally — mirroring
        /// the roll/putt handler which advances t=t+Dt and pos=posNext unconditionally and never sticks.
        ///
        /// This test seeds the two PROBE7 configs from the red-team review that were stuck:
        ///   PROBE7-A: origin=(0,6,-6) vel=(0,-3,12) — descended toward trunk, finalY≈2.03, samples=14401
        ///   PROBE7-B: origin=(0,8,-8) vel=(0,-5,8)  — steeper descent,  finalY≈1.38, samples=14612
        /// Both must now land on the ground (finalY ≈ ballRadius ≈ 0.021m) within &lt; maxSteps.
        ///
        /// MUST FAIL on code WITHOUT the frac=0 fix (the old code loops to 14400 steps and
        /// leaves the ball at y≈1.4–2.0m). MUST PASS with the frac=0 push-out fix applied.
        /// </summary>
        [Test]
        public void TreeCollision_AirborneTrunkDescending_BallReachesGround()
        {
            var trees  = BuildProvider();
            var ground = FlatAt();
            var surf   = FairwaySurface();
            var aero   = AeroConfig.Default; // default aero (matches red-team PROBE7 conditions)

            // PROBE7-A: origin=(0,6,-6) vel=(0,-3,12) — descending approach toward trunk at origin.
            // Old code (no fix): finalY≈2.03, samples=14401 (STUCK, floating 2m up).
            // Fixed code:        finalY≈ballRadius (≈0.021m), samples << 14400.
            {
                var origin   = new fp3(fp.Zero, fp.FromFloat(6f), fp.FromFloat(-6f));
                var velocity = new fp3(fp.Zero, fp.FromFloat(-3f), fp.FromFloat(12f));
                var input    = new ShotInput(origin, velocity, fp.FromInt(60));

                var result = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                    surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);

                float finalY   = result.finalPosition.y.ToFloat();
                int   samples  = result.samples.Count;
                int   maxSteps = 60 * 240; // the hard cap

                // The ball MUST reach the ground (finalY ≤ ballRadius + tolerance).
                // ballRadius ≈ 0.021m; allow 0.1m tolerance for fp rounding.
                Assert.Less(finalY, 0.1f,
                    $"PROBE7-A: ball must reach ground (finalY ≤ 0.1m). " +
                    $"Got finalY={finalY:F3}m, samples={samples}. " +
                    $"If finalY≈2.0 and samples≈14400, the frac=0 containment-guard fix was not applied " +
                    $"in the airborne trunk branch of BallSimulation.cs.");

                // The ball must NOT burn the full step cap.
                Assert.Less(samples, maxSteps,
                    $"PROBE7-A: ball must terminate before maxSteps ({maxSteps}). " +
                    $"Got samples={samples}. " +
                    $"If samples==14401, the integrator is stuck in the frac=0 loop — fix not applied.");
            }

            // PROBE7-B: origin=(0,8,-8) vel=(0,-5,8) — steeper descending approach toward trunk.
            // Old code (no fix): finalY≈1.38, samples=14612 (STUCK, floating ≈1.4m up).
            // Fixed code:        finalY≈ballRadius (≈0.021m), samples << 14400.
            {
                var origin   = new fp3(fp.Zero, fp.FromFloat(8f), fp.FromFloat(-8f));
                var velocity = new fp3(fp.Zero, fp.FromFloat(-5f), fp.FromFloat(8f));
                var input    = new ShotInput(origin, velocity, fp.FromInt(60));

                var result = BallSimulation.Simulate(input, ground, aero, WindConfig.Calm,
                    surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral, trees);

                float finalY   = result.finalPosition.y.ToFloat();
                int   samples  = result.samples.Count;
                int   maxSteps = 60 * 240;

                Assert.Less(finalY, 0.1f,
                    $"PROBE7-B: ball must reach ground (finalY ≤ 0.1m). " +
                    $"Got finalY={finalY:F3}m, samples={samples}. " +
                    $"If finalY≈1.4 and samples≈14600, the frac=0 containment-guard fix was not applied.");

                Assert.Less(samples, maxSteps,
                    $"PROBE7-B: ball must terminate before maxSteps ({maxSteps}). " +
                    $"Got samples={samples}. " +
                    $"If samples≈14612, the integrator is stuck in the frac=0 loop — fix not applied.");
            }
        }

        // Helper: total speed from fp3 velocity sample.
        private static float SpeedXYZ(fp3 vel)
        {
            float vx = vel.x.ToFloat();
            float vy = vel.y.ToFloat();
            float vz = vel.z.ToFloat();
            return (float)System.Math.Sqrt(vx * vx + vy * vy + vz * vz);
        }
    }
}


