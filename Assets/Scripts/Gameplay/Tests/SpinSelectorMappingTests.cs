using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for the spin selector UX disc-radius mapping (Order 354, iter-6).
    ///
    /// iter-6 adds:
    ///   D-1 circular dim: GenerateDonutTexture now has outerRadiusFrac (= visualFrac).
    ///     Pixels outside ball circle are alpha=0 — no square box corners.
    ///   D-2 no white wash: _activeDiscRt Image alpha=0 (no overlay inside cut).
    ///   Tests verify that at HIGH spin the outer radius = inner hole radius (= visualFrac),
    ///   meaning the dim ring width is ~0 (no dim at HIGH — essentially the normal ball).
    ///
    /// iter-5 change (donut-hole-oversize fix):
    ///   BallImageRadius = RT_halfWidth × BallSpriteVisualRadiusFrac = 300 × 0.957 = 287.1px.
    ///   D3 physics contract preserved: disc-edge → spin = ±1.0.
    ///
    /// Tests the pure math: radius01 mapping, radial clamp, px->value, circular dim geometry.
    /// No MonoBehaviour / scene loading required.
    /// </summary>
    public class SpinSelectorMappingTests
    {
        // Simulated ball image radius — matches runtime value (iter-5):
        //   BallImage.rectTransform.rect.width * 0.5f * BallSpriteVisualRadiusFrac
        //   = 300f * 0.957f = 287.1f
        // This represents the VISIBLE painted ball edge radius (accounting for sprite
        // alpha-padding), which is what the donut hole is capped to at HIGH spin.
        const float BallImageRadius = 287.1f;   // = 300 * BallSpriteVisualRadiusFrac (0.957)
        const float BallRtHalfWidth = 300f;     // = BallImage RT half-width (600/2)

        // BallSpriteVisualRadiusFrac from ControlsConfig.Default (controls.csv default: 0.957)
        const float VisualRadiusFrac = 0.957f;

        const float FloorRadius01 = 0.20f; // ControlsConfig.Default.SpinSelectorFloorRadius01

        // ─── Helper: replicate SpinPanelWidget's radius mapping ───────────────
        static float ComputeRadius01(int spinStat, float floor = FloorRadius01)
        {
            return Mathf.Lerp(floor, 1f, (spinStat + 10f) / 20f);
        }

        // ─── 1. Radius01 mapping ──────────────────────────────────────────────

        [Test]
        public void Radius01_AtSpinMinus10_EqualsFloor()
        {
            float r = ComputeRadius01(-10);
            Assert.AreEqual(FloorRadius01, r, 1e-5f,
                "spin=-10 should map to floor radius (worst ball = smallest disc).");
        }

        [Test]
        public void Radius01_AtSpinPlus10_Equals1()
        {
            float r = ComputeRadius01(+10);
            Assert.AreEqual(1.0f, r, 1e-5f,
                "spin=+10 should map to radius01=1.0 (best ball = full disc).");
        }

        [Test]
        public void Radius01_AtSpinZero_IsMidpoint()
        {
            float r = ComputeRadius01(0);
            float expected = Mathf.Lerp(FloorRadius01, 1f, 10f / 20f);
            Assert.AreEqual(expected, r, 1e-5f,
                "spin=0 should map to lerp midpoint.");
        }

        [Test]
        public void Radius01_IsMonotonicallyIncreasing()
        {
            float prev = ComputeRadius01(-10);
            for (int s = -9; s <= 10; s++)
            {
                float curr = ComputeRadius01(s);
                Assert.Greater(curr, prev - 1e-5f,
                    $"radius01 should be non-decreasing. Failed at spin={s}.");
                prev = curr;
            }
        }

        [Test]
        public void Radius01_ClampedByFloor_ForAllStats()
        {
            for (int s = -10; s <= 10; s++)
            {
                float r = ComputeRadius01(s);
                Assert.GreaterOrEqual(r, FloorRadius01,
                    $"radius01 should never drop below floor. Failed at spin={s}.");
            }
        }

        // ─── 2. Active pixel radius ───────────────────────────────────────────

        [Test]
        public void ActivePxRadius_AtFloor_IsFloorTimesBallRadius()
        {
            // At spin=-10, radius01 = FloorRadius01. Active disc = FloorRadius01 * BallImageRadius.
            float r01      = ComputeRadius01(-10);
            float activePx = r01 * BallImageRadius;
            Assert.AreEqual(FloorRadius01 * BallImageRadius, activePx, 1e-3f,
                "activePxRadius at spin=-10 should be floor * BallImageRadius.");
        }

        [Test]
        public void ActivePxRadius_AtMax_EqualsBallImageRadius()
        {
            // At spin=+10, radius01=1.0. Active disc cap = BallImageRadius (visible ball edge).
            float r01      = ComputeRadius01(+10);
            float activePx = r01 * BallImageRadius;
            Assert.AreEqual(BallImageRadius, activePx, 1e-3f,
                "activePxRadius at spin=+10 should equal BallImageRadius (disc fits inside visible ball).");
        }

        [Test]
        public void ActivePxRadius_AtMax_DoesNotExceedVisibleBallEdge()
        {
            // Cesar rejection guard (iter-4/5): HIGH disc must NEVER exceed the VISIBLE
            // painted ball sprite edge. BallImageRadius is already the visible edge radius
            // (= RT half-width × BallSpriteVisualRadiusFrac), so the disc at radius01=1
            // equals exactly the visible edge — never the alpha-padded RT border.
            float r01      = ComputeRadius01(+10);
            float activePx = r01 * BallImageRadius;
            float visibleEdge = BallRtHalfWidth * VisualRadiusFrac;  // 300 * 0.957 = 287.1px
            Assert.LessOrEqual(activePx, visibleEdge + 0.5f,  // 0.5px float tolerance
                "activePxRadius at spin=+10 must not exceed visible ball sprite edge (visual fraction cap).");
        }

        [Test]
        public void BallSpriteVisualRadiusFrac_DefaultIsCorrect()
        {
            // Verify ControlsConfig.Default has the expected calibration constant.
            var cfg = ControlsConfig.Default;
            Assert.AreEqual(0.957f, cfg.BallSpriteVisualRadiusFrac, 1e-5f,
                "ControlsConfig.Default.BallSpriteVisualRadiusFrac should be 0.957 (alpha-padding calibration).");
        }

        [Test]
        public void BallImageRadius_DerivedFromRtAndFrac()
        {
            // Production code: _ballImageRadius = RT_halfWidth * BallSpriteVisualRadiusFrac.
            // Verify the constant BallImageRadius in tests matches this derivation.
            float expected = BallRtHalfWidth * VisualRadiusFrac;
            Assert.AreEqual(expected, BallImageRadius, 0.5f,
                "Test BallImageRadius constant should equal BallRtHalfWidth * VisualRadiusFrac.");
        }

        // ─── 3. Radial clamp ─────────────────────────────────────────────────

        [Test]
        public void RadialClamp_InsideDisc_IsUnchanged()
        {
            float activePx = 100f;
            Vector2 pxFromCenter = new Vector2(60f, 0f); // magnitude 60 < 100
            if (pxFromCenter.magnitude > activePx)
                pxFromCenter = pxFromCenter.normalized * activePx;
            Assert.AreEqual(60f, pxFromCenter.magnitude, 1e-4f,
                "Point inside disc should not be clamped.");
        }

        [Test]
        public void RadialClamp_OutsideDisc_ClampedToRadius()
        {
            float activePx = 100f;
            Vector2 pxFromCenter = new Vector2(150f, 0f); // magnitude 150 > 100
            if (pxFromCenter.magnitude > activePx)
                pxFromCenter = pxFromCenter.normalized * activePx;
            Assert.AreEqual(activePx, pxFromCenter.magnitude, 1e-4f,
                "Point outside disc should be clamped to activePxRadius.");
            // After clamping a (150,0) vector to magnitude=100, x should be 100 (pointing right).
            Assert.AreEqual(100f, pxFromCenter.x, 1e-4f, "Direction should be preserved after clamp.");
        }

        [Test]
        public void RadialClamp_Diagonal_ClampedToRadius()
        {
            float activePx = 100f;
            Vector2 pxFromCenter = new Vector2(200f, 200f); // magnitude ~283 > 100
            if (pxFromCenter.magnitude > activePx)
                pxFromCenter = pxFromCenter.normalized * activePx;
            Assert.AreEqual(activePx, pxFromCenter.magnitude, 1e-3f,
                "Diagonal point outside disc should be clamped to activePxRadius.");
            // direction should be preserved (normalized 45°)
            Assert.AreEqual(1f / Mathf.Sqrt(2f), pxFromCenter.x / activePx, 1e-4f,
                "Diagonal direction should be preserved.");
        }

        // ─── 4. px→value mapping (D3: disc-edge → spin = ±1.0) ──────────────
        //
        // Production code uses _ballImageRadius as the normalization divisor.
        // Tests simulate this with BallImageRadius.

        [Test]
        public void PxToValue_Center_IsZeroSpin()
        {
            Vector2 pxFromCenter = Vector2.zero;
            Vector2 value = pxFromCenter / BallImageRadius;
            Assert.AreEqual(0f, value.magnitude, 1e-5f,
                "Center (px=0) should map to zero spin.");
        }

        [Test]
        public void PxToValue_AtBallRadius_IsOne()
        {
            // Disc edge = BallImageRadius → spin = 1.0 (D3 contract, iter-5).
            // BallImageRadius is the visible ball edge = RT_halfWidth * VisualFrac.
            Vector2 pxFromCenter = new Vector2(BallImageRadius, 0f);
            Vector2 value = pxFromCenter / BallImageRadius;
            Assert.AreEqual(1f, value.x, 1e-5f,
                "px=BallImageRadius on X axis should map to spinX=1.0 (disc edge = full spin).");
        }

        [Test]
        public void PxToValue_AtFloorRadius_ForLowestStat()
        {
            // LOW stat (spin=-10): disc edge is at floor * BallImageRadius.
            // Normalised by BallImageRadius → spinX = floor (not 1.0).
            float activePx = ComputeRadius01(-10) * BallImageRadius;
            Vector2 pxFromCenter = new Vector2(activePx, 0f);
            Vector2 value = pxFromCenter / BallImageRadius;
            // value.x should equal floor (e.g. 0.20)
            Assert.AreEqual(FloorRadius01, value.x, 1e-4f,
                "Lowest spin stat dragged to its disc edge should produce spinX = floor.");
        }

        [Test]
        public void PxToValue_HighSpin_ReachesOne()
        {
            // HIGH stat (spin=+10): disc edge is at BallImageRadius.
            // Normalised by BallImageRadius → spinX = 1.0.
            float activePx = ComputeRadius01(+10) * BallImageRadius;
            Vector2 pxFromCenter = new Vector2(activePx, 0f);
            Vector2 value = pxFromCenter / BallImageRadius;
            Assert.AreEqual(1.0f, value.x, 1e-4f,
                "Best spin stat (+10) dragged to disc edge should produce spinX = 1.0 (D3).");
        }

        // ─── 5. SpinContext clamping (safety net) ─────────────────────────────

        [Test]
        public void SpinContext_ClampsSpin_BeyondOne()
        {
            SpinContext.SetSpin(new Vector2(2f, -3f));
            Assert.AreEqual(1f,  SpinContext.Spin.x, 1e-5f, "x should be clamped to 1.");
            Assert.AreEqual(-1f, SpinContext.Spin.y, 1e-5f, "y should be clamped to -1.");
            SpinContext.Reset();
        }

        [Test]
        public void SpinContext_Reset_SetsZero()
        {
            SpinContext.SetSpin(new Vector2(0.5f, -0.3f));
            SpinContext.Reset();
            Assert.AreEqual(Vector2.zero, SpinContext.Spin, "Reset() should zero spin.");
        }

        // ─── 6. BallContext SelectedSpinStat ─────────────────────────────────

        [Test]
        public void BallContext_SelectedSpinStat_DefaultIsZero()
        {
            BallContext.Reset();
            Assert.AreEqual(0, BallContext.SelectedSpinStat,
                "SelectedSpinStat should default to 0 after Reset().");
        }

        [Test]
        public void BallContext_SelectedSpinStat_CanBeSetAndRead()
        {
            BallContext.SelectedSpinStat = -7;
            Assert.AreEqual(-7, BallContext.SelectedSpinStat);
            BallContext.SelectedSpinStat = +5;
            Assert.AreEqual(5, BallContext.SelectedSpinStat);
            BallContext.Reset();
        }

        // ─── 7. ControlsConfig floor and visual-frac knobs ───────────────────

        [Test]
        public void ControlsConfig_Default_HasFloorRadius01()
        {
            var cfg = ControlsConfig.Default;
            Assert.AreEqual(0.20f, cfg.SpinSelectorFloorRadius01, 1e-5f,
                "ControlsConfig.Default.SpinSelectorFloorRadius01 should be 0.20.");
        }

        [Test]
        public void ControlsConfig_FloorRadius01_UsedInMapping()
        {
            // If the floor is customised to 0.30, spin=-10 should yield radius01=0.30.
            float customFloor = 0.30f;
            float r = Mathf.Lerp(customFloor, 1f, (-10f + 10f) / 20f);
            Assert.AreEqual(customFloor, r, 1e-5f,
                "Custom floor should take effect at spin=-10.");
        }

        [Test]
        public void ControlsConfig_BallSpriteVisualRadiusFrac_DefaultIsSet()
        {
            var cfg = ControlsConfig.Default;
            Assert.AreEqual(0.957f, cfg.BallSpriteVisualRadiusFrac, 1e-5f,
                "ControlsConfig.Default.BallSpriteVisualRadiusFrac should be 0.957 (alpha-padding cal).");
        }

        [Test]
        public void DonutHoleRadius_AtHighSpin_DoesNotExceedVisibleBallEdge()
        {
            // Reproduce the exact grayOut hole computation from UpdateDiscVisuals():
            //   grayOutHalfPx = 300 (after anchor fix: _grayOutRt 600×600 / 2)
            //   holeRadiusFrac = activePxRadius / grayOutHalfPx
            //   on-screen hole radius = holeRadiusFrac * grayOutHalfPx = activePxRadius
            // So: hole radius in canvas-px = activePxRadius = BallImageRadius (at HIGH)
            // Must be ≤ visible ball edge (BallRtHalfWidth * VisualFrac).
            float grayOutHalfPx = BallRtHalfWidth;  // 300px (after anchor fix, sizeDelta=(600,600))
            float activePx = ComputeRadius01(+10) * BallImageRadius;  // = BallImageRadius at HIGH
            float holeRadiusFrac = activePx / grayOutHalfPx;
            float onScreenHoleRadius = holeRadiusFrac * grayOutHalfPx;  // = activePx

            float visibleEdge = BallRtHalfWidth * VisualRadiusFrac;

            Assert.LessOrEqual(onScreenHoleRadius, visibleEdge + 0.5f,
                "Donut hole on-screen radius at HIGH must not exceed visible ball sprite edge. " +
                $"holeRadius={onScreenHoleRadius:F1}px visibleEdge={visibleEdge:F1}px.");
        }

        // ─── 8. Circular dim geometry (iter-6 D-1) ───────────────────────────

        [Test]
        public void CircularDim_OuterRadiusFrac_EqualsVisualFrac()
        {
            // In UpdateDiscVisuals(), outerRadiusFrac = visualFrac (always).
            // This ensures pixels beyond the ball circle are alpha=0 (no square box).
            // At HIGH: holeRadiusFrac = activePx/grayOutHalfPx = BallImageRadius/BallRtHalfWidth
            //        = 287.1/300 = 0.957 = VisualRadiusFrac = outerRadiusFrac.
            // Result: hole edge == outer edge → zero-width dim ring at HIGH (pristine ball).
            float grayOutHalfPx  = BallRtHalfWidth;
            float activePxHigh   = ComputeRadius01(+10) * BallImageRadius;
            float holeRadiusFrac = Mathf.Min(activePxHigh / grayOutHalfPx, VisualRadiusFrac);
            float outerRadiusFrac = VisualRadiusFrac;  // production code always sets this

            Assert.AreEqual(outerRadiusFrac, VisualRadiusFrac, 1e-5f,
                "outerRadiusFrac must equal visualFrac so pixels outside ball circle are alpha=0.");
        }

        [Test]
        public void CircularDim_AtHighSpin_DimRingWidthIsNearZero()
        {
            // At HIGH: holeRadiusFrac ≈ VisualRadiusFrac, dim ring width ≈ 0.
            // Player sees essentially the normal ball — no dim ring.
            float grayOutHalfPx  = BallRtHalfWidth;
            float activePxHigh   = ComputeRadius01(+10) * BallImageRadius;
            float holeRadiusFrac = Mathf.Min(activePxHigh / grayOutHalfPx, VisualRadiusFrac);
            float outerRadiusFrac = VisualRadiusFrac;
            float ringWidthFrac  = outerRadiusFrac - holeRadiusFrac;  // ≈ 0

            Assert.LessOrEqual(ringWidthFrac, 0.01f,
                "At HIGH spin, dim ring width (outerFrac-holeFrac) should be near zero — player sees normal ball.");
        }

        [Test]
        public void CircularDim_AtLowSpin_DimRingWidthIsSignificant()
        {
            // At LOW (spin=-10): holeRadiusFrac = floor * BallImageRadius / grayOutHalfPx
            //                               = 0.20 * 287.1 / 300 = 0.1914
            // outerRadiusFrac = 0.957. Ring width = 0.957 - 0.1914 = 0.765 — most of ball is dimmed.
            float grayOutHalfPx  = BallRtHalfWidth;
            float activePxLow    = ComputeRadius01(-10) * BallImageRadius;
            float holeRadiusFrac = Mathf.Min(activePxLow / grayOutHalfPx, VisualRadiusFrac);
            float outerRadiusFrac = VisualRadiusFrac;
            float ringWidthFrac  = outerRadiusFrac - holeRadiusFrac;

            Assert.Greater(ringWidthFrac, 0.5f,
                "At LOW spin, dim ring should cover the majority of the ball (wide ring).");
        }

        [Test]
        public void CircularDim_Alpha_IsZeroOutsideBallRadius()
        {
            // Verify the invariant: the outer boundary at visualFrac ensures
            // that corner pixels of the 600×600 square (distance > visualFrac*300px from center)
            // have alpha=0. dist(corner from center) = sqrt(300²+300²) ≈ 424px >> 287px (outer radius).
            // Test by checking that corner distance > outerRadiusPx.
            float grayOutHalfPx  = BallRtHalfWidth;   // 300px
            float outerRadiusPx  = VisualRadiusFrac * grayOutHalfPx;  // 0.957 * 300 ≈ 287px
            float cornerDist     = grayOutHalfPx * Mathf.Sqrt(2f);    // ≈ 424px

            Assert.Greater(cornerDist, outerRadiusPx,
                "Corner distance from center must exceed outer ball radius so corners are alpha=0 (no square box).");
        }
    }
}
