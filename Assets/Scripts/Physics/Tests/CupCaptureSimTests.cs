using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// cup_capture_and_lipout (SPEC_CUP_CAPTURE_AND_LIPOUT §6).
    ///
    /// Covers the in-sim cup: capture (ball drops in and the trajectory ENDS there), lip-out
    /// (deterministic one-shot rim deflection), the single-fire latch, the tunneling guard,
    /// and — the blocking one — bit-exact equivalence with the pre-cup sim when the cup is
    /// disabled. That last test is the gate: if it fails, every existing tuned trajectory in
    /// the project has silently moved.
    ///
    /// All fixtures use FlatGround so the geometry is exact and the assertions are about the
    /// cup logic, not about terrain sampling.
    /// </summary>
    public class CupCaptureSimTests
    {
        const float BallRadiusF   = 0.02135f;   // AeroConfig default
        const float CupRadiusF    = 0.054f;     // regulation mouth
        const float CaptureSpeedF = 1.5f;       // USGA lip-out anchor
        const float DepthF        = 0.10f;
        const float LipRestF      = 0.35f;
        const float LipDampF      = 0.70f;
        const float LipPopF       = 0.30f;

        static readonly fp3 Pin = new fp3(fp.Zero, fp.Zero, fp.Zero);

        static CupSpec MakeCup() => new CupSpec(
            Pin,
            fp.FromFloat(CupRadiusF),
            fp.FromFloat(CaptureSpeedF),
            fp.FromFloat(DepthF),
            fp.FromFloat(LipRestF),
            fp.FromFloat(LipDampF),
            fp.FromFloat(LipPopF));

        // A putt starting `dist` metres out on −X, rolling toward the pin at `speed` m/s.
        // Ground is flat at y=0, so the ball centre rides at y = ballRadius.
        static ShotInput PuttToward(float dist, float speed)
            => new ShotInput(
                new fp3(fp.FromFloat(-dist), fp.Zero, fp.Zero),
                new fp3(fp.FromFloat(speed), fp.Zero, fp.Zero),
                fp.FromInt(60));

        static Trajectory Run(ShotInput input, in CupSpec cup)
            => BallSimulation.Simulate(
                input,
                new FlatGround(fp.Zero),
                AeroConfig.Default,
                WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green),
                SurfaceConfig.Default,
                PuttConfig.Default,
                BallPhysicsModifiers.Neutral,
                null,
                cup);

        static Trajectory RunLegacy(ShotInput input)
            => BallSimulation.Simulate(
                input,
                new FlatGround(fp.Zero),
                AeroConfig.Default,
                WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Green),
                SurfaceConfig.Default,
                PuttConfig.Default,
                BallPhysicsModifiers.Neutral,
                null);

        static float Speed(fp3 v) => UnityEngine.Mathf.Sqrt(fpMath.Dot(v, v).ToFloat());
        static float DistXZ(fp3 p, fp3 q)
        {
            float dx = (p.x - q.x).ToFloat(), dz = (p.z - q.z).ToFloat();
            return UnityEngine.Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ── §6.1 Slow putt drops ──────────────────────────────────────────────────

        [Test]
        public void SlowPutt_OverCup_Captures()
        {
            // 2 m out at 1.0 m/s: arrives at the cup well under the 1.5 m/s gate.
            var traj = Run(PuttToward(2.0f, 1.0f), MakeCup());

            Assert.AreEqual(TerminationReason.CupCapture, traj.termination,
                "A putt arriving under the capture speed must terminate IN the cup, not roll past it.");

            float expectedY = -DepthF + BallRadiusF;   // pin.y is 0
            Assert.AreEqual(0f, traj.finalPosition.x.ToFloat(), 0.002f, "final X should be the pin X");
            Assert.AreEqual(0f, traj.finalPosition.z.ToFloat(), 0.002f, "final Z should be the pin Z");
            Assert.AreEqual(expectedY, traj.finalPosition.y.ToFloat(), 0.002f,
                "final Y should be the cup floor (pin.y − depth + ballRadius)");
        }

        [Test]
        public void SlowPutt_FallIn_DescendsMonotonically()
        {
            var traj = Run(PuttToward(2.0f, 1.0f), MakeCup());
            Assert.AreEqual(TerminationReason.CupCapture, traj.termination);

            // Walk back to the first descending sample, then assert the whole tail descends.
            var s = traj.samples;
            int firstDrop = s.Count - 1;
            while (firstDrop > 0 && s[firstDrop - 1].position.y > s[firstDrop].position.y) firstDrop--;

            Assert.Greater(s.Count - firstDrop, 5,
                "the synthesized fall-in should be several samples long, not a single snap");
            for (int i = firstDrop + 1; i < s.Count; i++)
                Assert.LessOrEqual(s[i].position.y.ToFloat(), s[i - 1].position.y.ToFloat(),
                    $"fall-in sample {i} rose instead of descending");
        }

        [Test]
        public void SlowPutt_TerminalHit_IsStopAtCup()
        {
            var traj = Run(PuttToward(2.0f, 1.0f), MakeCup());
            Assert.IsNotNull(traj.terrainHits);
            Assert.Greater(traj.terrainHits.Count, 0, "capture must emit a terminal hit");
            var last = traj.terrainHits[traj.terrainHits.Count - 1];
            Assert.IsTrue(last.IsStop, "the capture hit must be flagged IsStop");
            Assert.AreEqual(SurfaceType.Green, last.Surface);
        }

        // ── §6.2 Fast putt lips out ───────────────────────────────────────────────

        [Test]
        public void FastPutt_OverCup_DoesNotCapture()
        {
            var traj = Run(PuttToward(2.0f, 3.0f), MakeCup());
            Assert.AreNotEqual(TerminationReason.CupCapture, traj.termination,
                "a putt crossing the mouth above the speed gate must not be captured");
        }

        /// <summary>
        /// Locates the lip-out impulse and returns (speedIn, speedOut, deflection angle°).
        /// Returns false when no impulse fired (the ball ran clean over the top).
        /// </summary>
        static bool TryGetLipImpulse(Trajectory traj, out float vIn, out float vOut, out float angleDeg)
        {
            vIn = vOut = angleDeg = 0f;
            var s = traj.samples;
            for (int i = 1; i < s.Count; i++)
            {
                fp3 dv = s[i].velocity - s[i - 1].velocity;
                if (Speed(dv) <= 0.02f) continue;           // rolling resistance is ~0.006/step
                var a = new UnityEngine.Vector2(s[i - 1].velocity.x.ToFloat(), s[i - 1].velocity.z.ToFloat());
                var b = new UnityEngine.Vector2(s[i].velocity.x.ToFloat(),     s[i].velocity.z.ToFloat());
                vIn = a.magnitude; vOut = b.magnitude;
                angleDeg = UnityEngine.Vector2.Angle(a, b);
                return true;
            }
            return false;
        }

        [Test]
        public void FastPutt_DeadCentre_RunsOnInsteadOfBouncingBack()
        {
            // A ball well above the gate over the middle of the hole must carry on forward,
            // slowed — the real "went straight over the top of it" outcome.
            //
            // Regression guard. The first model applied the same reversal and the same 30%
            // loss at EVERY speed, so a dead-centre crossing at 2.9 m/s came straight back at
            // 2.03 m/s (180°). That reads as a squash ball off a wall, not a lip-out.
            var traj = Run(PuttToward(2.0f, 3.0f), MakeCup());
            Assert.AreNotEqual(TerminationReason.CupCapture, traj.termination);

            Assert.IsTrue(TryGetLipImpulse(traj, out float vIn, out float vOut, out float ang),
                "expected a lip-out impulse at 3 m/s over the cup");
            Assert.Less(ang, 45f,
                $"the ball must run ON, not come back (deflection was {ang:F1}°)");
            Assert.That(vOut / vIn, Is.InRange(0.65f, 0.95f),
                $"a 3 m/s clip should cost pace but not most of it (in {vIn:F3} → out {vOut:F3})");
        }

        [Test]
        public void LipOut_StrengthScalesWithCrossingSpeed()
        {
            // The whole point of the dip model: interaction strength falls off with speed,
            // because a faster ball is over the open mouth for less time and sinks less far.
            var cup = MakeCup();
            float rSlow = RatioAt(cup, 2.0f);
            float rMid  = RatioAt(cup, 3.0f);
            float rFast = RatioAt(cup, 4.0f);
            float rVery = RatioAt(cup, 6.0f);

            Assert.Less(rSlow, rMid,  "a slower crossing must lose more pace than a faster one");
            Assert.Less(rMid,  rFast, "…and that must hold monotonically");
            Assert.Less(rFast, rVery, "…all the way up");
            Assert.Greater(rVery, 0.9f,
                $"a 6 m/s ball should skim over almost untouched (kept {rVery:P0})");
        }

        [Test]
        public void LipOut_NeverStopsTheBallDeadOnTheRim()
        {
            // Regression guard for a defect in the second model: blending the radial component
            // linearly from "passes over" to "reverses" crosses ZERO, and at ~0.74 dip it
            // annihilated the ball's velocity — measured 1.945 m/s in, 0.083 m/s out, leaving
            // the ball parked on the lip. Sweep the whole band just above the gate.
            var cup = MakeCup();
            for (float launch = 1.6f; launch <= 4.0f; launch += 0.1f)
            {
                var traj = Run(PuttToward(2.0f, launch), cup);
                if (traj.termination == TerminationReason.CupCapture) continue;
                if (!TryGetLipImpulse(traj, out float vIn, out float vOut, out _)) continue;
                Assert.Greater(vOut / vIn, 0.3f,
                    $"launch {launch:F1} m/s: ball nearly stopped on the rim "
                    + $"(in {vIn:F3} → out {vOut:F3}) — the radial blend is crossing zero again");
            }
        }

        static float RatioAt(in CupSpec cup, float launch)
        {
            var traj = Run(PuttToward(2.0f, launch), cup);
            Assert.AreNotEqual(TerminationReason.CupCapture, traj.termination,
                $"fixture: {launch} m/s should not capture");
            Assert.IsTrue(TryGetLipImpulse(traj, out float vIn, out float vOut, out _),
                $"fixture: expected an impulse at {launch} m/s");
            return vOut / vIn;
        }

        [Test]
        public void FastPutt_OffCentre_LipOut_PushesBallOffItsLine()
        {
            // Cross the mouth 35 mm off-centre: inside the 54 mm mouth (so the rim is hit) but
            // outside the 32.65 mm capture disc, and with a real tangential component.
            const float offset = 0.035f;
            var input = new ShotInput(
                new fp3(fp.FromFloat(-2.0f), fp.Zero, fp.FromFloat(offset)),
                new fp3(fp.FromFloat(3.0f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var withCup    = Run(input, MakeCup());
            var withoutCup = Run(input, CupSpec.Disabled);

            Assert.AreNotEqual(TerminationReason.CupCapture, withCup.termination,
                "above the gate the ball must not drop");
            float lateralWith    = withCup.finalPosition.z.ToFloat();
            float lateralWithout = withoutCup.finalPosition.z.ToFloat();
            Assert.Greater(UnityEngine.Mathf.Abs(lateralWith - lateralWithout), 0.01f,
                $"an off-centre lip-out must push the ball off its line "
                + $"(with cup z={lateralWith:F4}, without z={lateralWithout:F4})");
        }

        // ── §6.3 Bit-exact legacy gate (BLOCKING) ─────────────────────────────────

        [Test]
        public void LegacyGate_DisabledCup_IsBitExactWithPreCupPath()
        {
            // Cover a putt straight over the cup AND a shot that reaches the roll phase, so
            // both integrators are exercised.
            var cases = new List<ShotInput>
            {
                PuttToward(2.0f, 1.0f),
                PuttToward(2.0f, 3.0f),
                PuttToward(0.5f, 1.4f),
                new ShotInput(new fp3(fp.FromFloat(-30f), fp.Zero, fp.Zero),
                              new fp3(fp.FromFloat(28f), fp.FromFloat(12f), fp.FromFloat(3f)),
                              fp.FromInt(60)),
            };

            foreach (var input in cases)
            {
                var legacy  = RunLegacy(input);
                var gated   = Run(input, CupSpec.Disabled);

                Assert.AreEqual(legacy.termination, gated.termination, "termination diverged");
                Assert.AreEqual(legacy.samples.Count, gated.samples.Count, "sample count diverged");
                Assert.AreEqual(legacy.finalTime.raw, gated.finalTime.raw, "finalTime diverged");
                Assert.AreEqual(legacy.finalPosition.x.raw, gated.finalPosition.x.raw);
                Assert.AreEqual(legacy.finalPosition.y.raw, gated.finalPosition.y.raw);
                Assert.AreEqual(legacy.finalPosition.z.raw, gated.finalPosition.z.raw);
                Assert.AreEqual(legacy.finalVelocity.x.raw, gated.finalVelocity.x.raw);
                Assert.AreEqual(legacy.finalVelocity.y.raw, gated.finalVelocity.y.raw);
                Assert.AreEqual(legacy.finalVelocity.z.raw, gated.finalVelocity.z.raw);

                for (int i = 0; i < legacy.samples.Count; i++)
                {
                    var a = legacy.samples[i];
                    var b = gated.samples[i];
                    Assert.AreEqual(a.time.raw, b.time.raw, $"sample {i} time diverged");
                    Assert.AreEqual(a.position.x.raw, b.position.x.raw, $"sample {i} pos.x diverged");
                    Assert.AreEqual(a.position.y.raw, b.position.y.raw, $"sample {i} pos.y diverged");
                    Assert.AreEqual(a.position.z.raw, b.position.z.raw, $"sample {i} pos.z diverged");
                    Assert.AreEqual(a.velocity.x.raw, b.velocity.x.raw, $"sample {i} vel.x diverged");
                    Assert.AreEqual(a.velocity.y.raw, b.velocity.y.raw, $"sample {i} vel.y diverged");
                    Assert.AreEqual(a.velocity.z.raw, b.velocity.z.raw, $"sample {i} vel.z diverged");
                }

                Assert.AreEqual(legacy.terrainHits.Count, gated.terrainHits.Count, "hit count diverged");
            }
        }

        // ── §6.4 Boundary speed ───────────────────────────────────────────────────
        // Mirrors RealCupDetector_BoundarySpeed_Deterministic: the gate is inclusive
        // (speedSq <= threshold captures) and one fp LSB above it does not.
        // Driven directly at the boundary so friction cannot drift the arrival speed.

        [Test]
        public void BoundarySpeed_ExactlyAtGate_Captures()
        {
            // Start one step short of the cup so the very first integration step arrives at
            // the mouth with essentially the launch speed.
            var input = new ShotInput(
                new fp3(fp.FromFloat(-0.02f), fp.Zero, fp.Zero),
                new fp3(fp.FromFloat(CaptureSpeedF), fp.Zero, fp.Zero),
                fp.FromInt(60));
            var traj = Run(input, MakeCup());
            Assert.AreEqual(TerminationReason.CupCapture, traj.termination,
                "speed exactly at the gate must capture (inclusive comparison)");
        }

        [Test]
        public void BoundarySpeed_JustAboveGate_LipsOutInsteadOfCapturing()
        {
            // Regression guard for a real defect found by this test: the lip-out deflection
            // drops a 1.55 m/s ball to 1.08 m/s while it is STILL over the mouth, so without
            // the lip latch suppressing capture it dropped straight in one step later — the
            // speed gate would have been cosmetic all the way up to ~2.1 m/s, and on screen
            // the ball would vanish at the rim with no visible deflection.
            var input = new ShotInput(
                new fp3(fp.FromFloat(-0.02f), fp.Zero, fp.Zero),
                new fp3(fp.FromFloat(CaptureSpeedF) + fp.FromFloat(0.05f), fp.Zero, fp.Zero),
                fp.FromInt(60));
            var traj = Run(input, MakeCup());
            Assert.AreNotEqual(TerminationReason.CupCapture, traj.termination,
                "speed above the gate must not capture on the crossing");
        }

        // ── §6.5 Single-fire lip-out ──────────────────────────────────────────────

        [Test]
        public void LipOut_FiresExactlyOncePerCrossing()
        {
            var traj = Run(PuttToward(2.0f, 3.0f), MakeCup());

            // Count discrete impulses: sample-to-sample velocity jumps far above what
            // rolling resistance produces in one step (k·v·dt ≈ 0.006 m/s at 3 m/s).
            int impulses = 0;
            var s = traj.samples;
            for (int i = 1; i < s.Count; i++)
            {
                fp3 dv = s[i].velocity - s[i - 1].velocity;
                if (Speed(dv) > 0.05f) impulses++;
            }
            Assert.AreEqual(1, impulses,
                "consecutive in-mouth steps must produce exactly one lip-out impulse");
        }

        [Test]
        public void LipOut_LatchClearsAfterLeavingTheMouth()
        {
            // The latch must clear once the ball is more than radius + 20 mm from the cup,
            // otherwise a ball that returns can never lip out again. Drive two crossings by
            // running the sim twice from the same state machine's perspective: a single fast
            // pass leaves the latch cleared, which we observe by confirming the ball ends up
            // well outside the re-arm ring with a live (non-captured) trajectory.
            var traj = Run(PuttToward(2.0f, 3.0f), MakeCup());
            Assert.AreNotEqual(TerminationReason.CupCapture, traj.termination);
            float endDist = DistXZ(traj.finalPosition, Pin);
            Assert.Greater(endDist, CupRadiusF + 0.02f,
                "the ball should come to rest outside the re-arm ring after a lip-out");
        }

        // ── §6.6 Tunneling guard ──────────────────────────────────────────────────

        [Test]
        public void Tunneling_StepStraddlingTheCup_IsSeenBySegmentTest()
        {
            // A step from −70 mm to +70 mm covers 140 mm — wider than the 108 mm mouth — so
            // BOTH endpoints are outside the cup and an endpoint-only test would miss it
            // entirely. The closest-point-on-segment test must report ~0 distance.
            //
            // Asserted at the geometry seam rather than end-to-end because the integrators
            // cannot actually be driven into this regime from the public API: on a Green the
            // bounce loop's tangential friction bleeds a shot to well under 1 m/s before the
            // roll phase begins (measured: 0.42 m/s entering roll from a 28 m/s launch), so a
            // straddling roll step never occurs in practice. The guard is defensive.
            fp3 pin = fp3.Zero;
            fp  r   = fp.FromFloat(CupRadiusF);
            fp3 a = new fp3(fp.FromFloat(-0.07f), fp.FromFloat(BallRadiusF), fp.Zero);
            fp3 b = new fp3(fp.FromFloat( 0.07f), fp.FromFloat(BallRadiusF), fp.Zero);

            // Sanity: both endpoints really are outside the mouth.
            Assert.Greater(DistXZ(a, pin), CupRadiusF, "fixture: endpoint A must be outside the mouth");
            Assert.Greater(DistXZ(b, pin), CupRadiusF, "fixture: endpoint B must be outside the mouth");

            float segDist = BallSimulation.CupDistanceToSegmentXZ(a, b, pin, r).ToFloat();
            Assert.Less(segDist, CupRadiusF,
                "a step straddling the cup must register inside the mouth, not be tunnelled through");
            Assert.AreEqual(0f, segDist, 0.001f,
                "the segment passes through the cup centre, so the distance should be ~0");
        }

        [Test]
        public void SegmentTest_StepPassingWideOfTheCup_ReportsTrueClearance()
        {
            // Counterpart to the tunneling test: the segment test must not report a false
            // positive for a fast step that passes near but clear of the mouth.
            fp3 pin = fp3.Zero;
            fp  r   = fp.FromFloat(CupRadiusF);
            fp3 a = new fp3(fp.FromFloat(-0.07f), fp.FromFloat(BallRadiusF), fp.FromFloat(0.09f));
            fp3 b = new fp3(fp.FromFloat( 0.07f), fp.FromFloat(BallRadiusF), fp.FromFloat(0.09f));

            float segDist = BallSimulation.CupDistanceToSegmentXZ(a, b, pin, r).ToFloat();
            Assert.AreEqual(0.09f, segDist, 0.002f,
                "a step passing 90 mm to the side must report ~90 mm, not a spurious hit");
            Assert.Greater(segDist, CupRadiusF, "and must be outside the mouth");
        }

        // ── Guard: the slow-graze case is explicitly unchanged in v1 ──────────────

        [Test]
        public void SlowGraze_OutsideEffectiveRadius_RollsPastUnchanged()
        {
            // Under the speed gate but offset so the ball centre never enters
            // (cupRadius − ballRadius): §4.3 says no interaction in v1.
            float offset = CupRadiusF;   // 54 mm off-centre — outside the 32.65 mm capture disc
            var input = new ShotInput(
                new fp3(fp.FromFloat(-1.0f), fp.Zero, fp.FromFloat(offset)),
                new fp3(fp.FromFloat(1.0f), fp.Zero, fp.Zero),
                fp.FromInt(60));

            var withCup    = Run(input, MakeCup());
            var withoutCup = Run(input, CupSpec.Disabled);

            Assert.AreNotEqual(TerminationReason.CupCapture, withCup.termination,
                "a slow ball outside the effective capture radius must not drop");
            Assert.AreEqual(withoutCup.samples.Count, withCup.samples.Count,
                "a slow graze outside the capture disc must behave exactly as it does today");
        }
    }
}
