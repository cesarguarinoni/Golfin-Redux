using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// §2d — RealCupDetector unit tests (5 required).
    /// Uses the static IsInCupStatic seam to avoid Vector3 pin construction complexity.
    /// </summary>
    public class RealCupDetectorTests
    {
        // Standard regulation values used across tests.
        static readonly fp DefaultCupRadius = RealCupDetector.DefaultCupRadius; // 0.054 m
        static readonly fp BallRadius = fp.FromFloat(0.021f);   // standard golf ball radius
        static readonly fp3 PinAtOrigin = new fp3(fp.Zero, fp.Zero, fp.Zero);

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
    }
}
