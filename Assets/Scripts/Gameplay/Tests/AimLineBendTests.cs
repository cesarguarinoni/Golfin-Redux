using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for the fade/draw aim-line bend (Order 355).
    ///
    /// Tests cover:
    ///   1. Curve sign: ConeFinetuneX sign maps to correct lateral direction (D5).
    ///   2. Magnitude monotone: |lateral| grows with |ConeFinetuneX|.
    ///   3. Straight mode: zero curve when not armed.
    ///   4. Power scaling: higher power → larger reach.
    ///   5. Full-deflection symmetry: draw vs fade lateral are opposite in sign.
    /// </summary>
    [TestFixture]
    public class AimLineBendTests
    {
        private GameObject          _rendererGO;
        private AimLineBendRenderer _renderer;

        private const float kCurveScale = 0.35f;   // iter-2: updated from 0.18
        private const float kReachPx    = 500f;   // iter-2: updated from 400

        [SetUp]
        public void SetUp()
        {
            _rendererGO = new GameObject("AimLineBendRenderer_Test");
            _renderer   = _rendererGO.AddComponent<AimLineBendRenderer>();

            // Prime with test values matching ControlsConfig.Default
            _renderer.ReachPx    = kReachPx;
            _renderer.CurveScale = kCurveScale;
            _renderer.AimAngleDeg = 0f;   // pointing up (+Y)
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_rendererGO);
            // Reset ShotModeContext to avoid cross-test pollution.
            ShotModeContext.Reset();
        }

        // ── Helper ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Lateral offset (local X) of the bend tip (curve param t=1), including the
        /// Phase-D clamp. The renderer is now a single-mesh Graphic, so the tip lateral
        /// is read from the pure curve function rather than a child segment transform.
        /// </summary>
        float GetTipLateralX() => _renderer.TipLateralX;

        // ── Test 1: Curve sign — handle LEFT = DRAW = negative X lateral ─────────

        [Test]
        public void DrawSign_HandleLeft_CurvesNegativeX()
        {
            // D5: fadeDrawInput −1 = DRAW → handle left → line curves LEFT = negative local X.
            _renderer.FadeDrawArmed = true;
            _renderer.FinetuneX     = -1f;   // full draw

            float tipX = GetTipLateralX();

            Assert.Less(tipX, 0f,
                $"Draw (FinetuneX=-1) should bend line to negative local X (left). Got {tipX:F2}px");
        }

        [Test]
        public void FadeSign_HandleRight_CurvesPositiveX()
        {
            // D5: fadeDrawInput +1 = FADE → handle right → line curves RIGHT = positive local X.
            _renderer.FadeDrawArmed = true;
            _renderer.FinetuneX     = 1f;   // full fade

            float tipX = GetTipLateralX();

            Assert.Greater(tipX, 0f,
                $"Fade (FinetuneX=+1) should bend line to positive local X (right). Got {tipX:F2}px");
        }

        // ── Test 2: Magnitude monotone — |lateral| grows with |FinetuneX| ───────

        [Test]
        public void Magnitude_GrowsWithFinetuneX()
        {
            _renderer.FadeDrawArmed = true;

            // Measure lateral at 3 finetune values
            _renderer.FinetuneX = 0.25f;
            float tipQ = Mathf.Abs(GetTipLateralX());

            _renderer.FinetuneX = 0.5f;
            float tipH = Mathf.Abs(GetTipLateralX());

            _renderer.FinetuneX = 1.0f;
            float tipF = Mathf.Abs(GetTipLateralX());

            Assert.Less(tipQ, tipH,
                $"|lateral| at finetune=0.25 ({tipQ:F2}) must be < at 0.5 ({tipH:F2})");
            Assert.Less(tipH, tipF,
                $"|lateral| at finetune=0.5 ({tipH:F2}) must be < at 1.0 ({tipF:F2})");
        }

        // ── Test 3: Straight mode — no bend when not armed ───────────────────────

        [Test]
        public void StraightMode_NotArmed_ZeroLateral()
        {
            _renderer.FadeDrawArmed = false;
            _renderer.FinetuneX     = 1f;   // handle fully right, but mode=Straight

            float tipX = GetTipLateralX();

            Assert.AreEqual(0f, tipX, 0.01f,
                $"Straight mode must produce zero lateral offset regardless of FinetuneX. Got {tipX:F4}px");
        }

        [Test]
        public void StraightMode_ZeroFinetuneX_ZeroLateral()
        {
            _renderer.FadeDrawArmed = true;
            _renderer.FinetuneX     = 0f;   // centered handle — armed but no deflection

            float tipX = GetTipLateralX();

            Assert.AreEqual(0f, tipX, 0.01f,
                $"Centered handle (FinetuneX=0) must produce zero lateral even when armed. Got {tipX:F4}px");
        }

        // ── Test 4: Power scaling — larger reach → larger absolute tip ───────────

        [Test]
        public void PowerScaling_LargerReach_LargerAbsoluteTip()
        {
            _renderer.FadeDrawArmed = true;
            _renderer.FinetuneX     = 1f;

            _renderer.ReachPx = 200f;
            float tipSmall = Mathf.Abs(GetTipLateralX());

            _renderer.ReachPx = 400f;
            float tipMid = Mathf.Abs(GetTipLateralX());

            _renderer.ReachPx = 600f;
            float tipLarge = Mathf.Abs(GetTipLateralX());

            Assert.Less(tipSmall, tipMid,
                $"Larger reach must produce larger tip lateral. Small={tipSmall:F2}, Mid={tipMid:F2}");
            Assert.Less(tipMid, tipLarge,
                $"Larger reach must produce larger tip lateral. Mid={tipMid:F2}, Large={tipLarge:F2}");
        }

        // ── Test 5: Draw vs Fade — equal magnitude, opposite sign ────────────────

        [Test]
        public void DrawVsFade_OppositeSigns()
        {
            _renderer.FadeDrawArmed = true;

            _renderer.FinetuneX = -1f;
            float drawTipX = GetTipLateralX();

            _renderer.FinetuneX = 1f;
            float fadeTipX = GetTipLateralX();

            Assert.IsTrue(drawTipX * fadeTipX < 0f,
                $"Draw ({drawTipX:F2}px) and Fade ({fadeTipX:F2}px) must produce opposite-sign lateral offsets");
        }

        // ── Test 6: ControlsConfig knobs propagate ────────────────────────────────

        [Test]
        public void ControlsConfig_DefaultValues_ArePresent()
        {
            var cfg = ControlsConfig.Default;
            Assert.AreEqual(500f,  cfg.AimLineDefaultReachPx, 0.01f,
                "AimLineDefaultReachPx default should be 500 (iter-2 calibration)");
            Assert.AreEqual(0.55f, cfg.AimLineCurveScale, 0.001f,
                "AimLineCurveScale default should be 0.55 (iter-4 — more pronounced bend per Cesar)");
        }
    }
}
