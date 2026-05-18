using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §2d — RealCupDetector unit tests (5 original + 4 speed-gate tests added in cup_speed_gated_capture).
    /// Uses static seams to avoid Vector3 pin construction complexity.
    ///
    /// Speed-gate source: Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis).
    /// USGA lip-out condition at cup rim speed ≈ 5 ft/s = 1.524 m/s.
    /// Architect-locked threshold: 1.5 m/s (2026-05-14 09:30 JST).
    /// </summary>
    public class RealCupDetectorTests
    {
        // Standard regulation values used across tests.
        static readonly fp DefaultCupRadius = RealCupDetector.DefaultCupRadius; // 0.054 m
        static readonly fp BallRadius = fp.FromFloat(0.021f);   // standard golf ball radius
        static readonly fp3 PinAtOrigin = new fp3(fp.Zero, fp.Zero, fp.Zero);

        // Speed gate threshold (USGA lip-out anchor, architect-locked 2026-05-14).
        static readonly fp CupCaptureSpeed = RealCupDetector.DefaultCupCaptureSpeed; // 1.5 m/s

        // Ball position dead-centre over the cup, just below pin Y.
        static readonly fp3 CentrePosition = new fp3(fp.Zero, fp.FromFloat(-0.01f), fp.Zero);

        [SetUp]
        public void SetUp()
        {
            // No static state to reset for RealCupDetector — pure value type.
        }

        // ── Test 1: Ball inside cup XZ and below pin Y → true ────────────────────

        [Test]
        public void RealCupDetector_BallInsideCup_ReturnsTrue()
        {
            // pin=(0,0,0), ball at (0,-0.01,0), ballRadius=0.021.
            // XZ dist = 0, effRadius = 0.054 - 0.021 = 0.033 > 0. Height: -0.01 <= 0 + 0.021 ✓
            var ballPos = new fp3(fp.Zero, fp.FromFloat(-0.01f), fp.Zero);

            bool result = RealCupDetector.IsInCupStatic(
                ballPos, BallRadius, PinAtOrigin, DefaultCupRadius);

            Assert.IsTrue(result,
                "Ball directly over pin (at Y=-0.01, below pin Y=0+ballRadius) should be InCup.");
        }

        // ── Test 2: Ball outside cup XZ radius → false ───────────────────────────

        [Test]
        public void RealCupDetector_BallOutsideCupRadius_ReturnsFalse()
        {
            // pin=(0,0,0), ball at (0.1,0,0). XZ dist=0.1 >> effRadius=0.033.
            var ballPos = new fp3(fp.FromFloat(0.1f), fp.Zero, fp.Zero);

            bool result = RealCupDetector.IsInCupStatic(
                ballPos, BallRadius, PinAtOrigin, DefaultCupRadius);

            Assert.IsFalse(result,
                "Ball 0.1m away (outside effRadius ~0.033m) should not be InCup.");
        }

        // ── Test 3: Ball above cup height gate → false ────────────────────────────

        [Test]
        public void RealCupDetector_BallAboveCup_ReturnsFalse()
        {
            // pin=(0,0,0), ball at (0,5,0). Height gate: 5 > 0 + 0.021 → false.
            var ballPos = new fp3(fp.Zero, fp.FromFloat(5f), fp.Zero);

            bool result = RealCupDetector.IsInCupStatic(
                ballPos, BallRadius, PinAtOrigin, DefaultCupRadius);

            Assert.IsFalse(result,
                "Ball 5m above pin should be rejected by the height gate.");
        }

        // ── Test 4: Ball at exact effective edge → false; just inside → true ──────

        [Test]
        public void RealCupDetector_BallAtCupEdge_ConsidersBallRadius()
        {
            // effRadius = cupRadius - ballRadius = 0.054 - 0.021 = 0.033 m.
            // Place ball at XZ dist = effRadius exactly → false (not strictly <).
            // Place ball at XZ dist = effRadius - 0.001 → true.
            fp effRadius = DefaultCupRadius - BallRadius;  // 0.033 m

            var atEdge = new fp3(effRadius, fp.Zero, fp.Zero);
            bool atEdgeResult = RealCupDetector.IsInCupStatic(
                atEdge, BallRadius, PinAtOrigin, DefaultCupRadius);
            Assert.IsFalse(atEdgeResult,
                "Ball at exact effRadius edge should NOT be InCup (condition is distSq < effRadius²).");

            fp justInside = effRadius - fp.FromFloat(0.001f);
            var insidePos = new fp3(justInside, fp.Zero, fp.Zero);
            bool insideResult = RealCupDetector.IsInCupStatic(
                insidePos, BallRadius, PinAtOrigin, DefaultCupRadius);
            Assert.IsTrue(insideResult,
                "Ball just inside effRadius (effRadius - 0.001m) should be InCup.");
        }

        // ── Test 5: Ball radius larger than cup radius → always false ─────────────

        [Test]
        public void RealCupDetector_BallLargerThanCup_AlwaysReturnsFalse()
        {
            // ballRadius=0.1 > cupRadius=0.054 → effRadius <= 0 → always false.
            fp oversizedBall = fp.FromFloat(0.1f);
            var ballAtOrigin = new fp3(fp.Zero, fp.Zero, fp.Zero);

            bool result = RealCupDetector.IsInCupStatic(
                ballAtOrigin, oversizedBall, PinAtOrigin, DefaultCupRadius);

            Assert.IsFalse(result,
                "When ball radius >= cup radius, effRadius <= 0 → always false (ball cannot fit).");
        }

        // ── Speed gate tests (cup_speed_gated_capture, 2026-05-18) ────────────────
        //
        // Source: Penner, A.R. (2002) "The physics of putting." Canadian Journal of Physics 80(2): 83–96 (see lip-out analysis).
        // USGA lip-out at cup rim ≈ 5 ft/s = 1.524 m/s. Threshold = 1.5 m/s.
        //
        // All speed-gate tests use the velocity-aware static seam:
        //   IsInCupStatic(position, ballRadius, velocity, pin, cupRadius, cupCaptureSpeed)

        // ── Test 6: Slow putt (0.5 m/s) → captured ───────────────────────────────

        [Test]
        public void RealCupDetector_SlowPutt_0p5mps_InCup_Captured()
        {
            // 0.5 m/s is well below the 1.5 m/s threshold → should be captured.
            var velocity = new fp3(fp.Zero, fp.Zero, fp.FromFloat(0.5f)); // rolling toward pin

            bool result = RealCupDetector.IsInCupStatic(
                CentrePosition, BallRadius, velocity,
                PinAtOrigin, DefaultCupRadius, CupCaptureSpeed);

            Assert.IsTrue(result,
                "Putt at 0.5 m/s (below 1.5 m/s threshold) inside cup geometry should be captured.");
        }

        // ── Test 7: Medium putt (1.0 m/s) → captured (under threshold) ──────────

        [Test]
        public void RealCupDetector_MediumPutt_1p0mps_InCup_Captured()
        {
            // 1.0 m/s < 1.5 m/s threshold → should be captured.
            var velocity = new fp3(fp.Zero, fp.Zero, fp.FromFloat(1.0f));

            bool result = RealCupDetector.IsInCupStatic(
                CentrePosition, BallRadius, velocity,
                PinAtOrigin, DefaultCupRadius, CupCaptureSpeed);

            Assert.IsTrue(result,
                "Putt at 1.0 m/s (below 1.5 m/s threshold) inside cup geometry should be captured.");
        }

        // ── Test 8: Fast putt (3.0 m/s) → NOT captured (over threshold) ─────────

        [Test]
        public void RealCupDetector_FastPutt_3p0mps_InCup_NotCaptured()
        {
            // 3.0 m/s >> 1.5 m/s threshold → fly-over, NOT captured.
            var velocity = new fp3(fp.Zero, fp.Zero, fp.FromFloat(3.0f));

            bool result = RealCupDetector.IsInCupStatic(
                CentrePosition, BallRadius, velocity,
                PinAtOrigin, DefaultCupRadius, CupCaptureSpeed);

            Assert.IsFalse(result,
                "Putt at 3.0 m/s (above 1.5 m/s threshold) should NOT be captured (lip-out / fly-over).");
        }

        // ── Test 9: Boundary — at threshold + epsilon → NOT captured; − epsilon → captured ──

        [Test]
        public void RealCupDetector_BoundarySpeed_Deterministic()
        {
            // At threshold (1.5 m/s): speedSq == thresholdSq → NOT captured (condition is >, not >=).
            // One epsilon above → NOT captured.
            // One epsilon below → captured.
            fp threshold = CupCaptureSpeed; // 1.5 m/s
            fp epsilon   = fp.FromFloat(0.001f);

            // Exactly at threshold — speedSq == thresholdSq. Condition is (speedSq > thresholdSq),
            // so equal is NOT rejected → should be captured.
            var velExact = new fp3(fp.Zero, fp.Zero, threshold);
            bool atThreshold = RealCupDetector.IsInCupStatic(
                CentrePosition, BallRadius, velExact,
                PinAtOrigin, DefaultCupRadius, CupCaptureSpeed);
            Assert.IsTrue(atThreshold,
                "Putt at exactly threshold (1.5 m/s) should be captured (boundary: > not >=).");

            // Epsilon above threshold → NOT captured.
            var velAbove = new fp3(fp.Zero, fp.Zero, threshold + epsilon);
            bool aboveThreshold = RealCupDetector.IsInCupStatic(
                CentrePosition, BallRadius, velAbove,
                PinAtOrigin, DefaultCupRadius, CupCaptureSpeed);
            Assert.IsFalse(aboveThreshold,
                "Putt at threshold + 0.001 m/s should NOT be captured (above threshold).");

            // Epsilon below threshold → captured.
            var velBelow = new fp3(fp.Zero, fp.Zero, threshold - epsilon);
            bool belowThreshold = RealCupDetector.IsInCupStatic(
                CentrePosition, BallRadius, velBelow,
                PinAtOrigin, DefaultCupRadius, CupCaptureSpeed);
            Assert.IsTrue(belowThreshold,
                "Putt at threshold − 0.001 m/s should be captured (below threshold).");
        }
    }
}
