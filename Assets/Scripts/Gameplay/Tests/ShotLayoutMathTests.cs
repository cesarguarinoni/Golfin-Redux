using NUnit.Framework;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// shot_view_layout §3.6 — the shot view's framing arithmetic, pinned on the three aspects
    /// the game actually ships on.
    ///
    /// <para>WHY THIS IS A TEST AND NOT A SCREENSHOT: the numbers that matter here are two
    /// competing constraints — "put the ball at the Figma height" and "never let the pull lane
    /// run under the action buttons" — and which one wins depends on the screen. A 1170x2532
    /// capture only ever shows the case where the first wins. The 16:9 and 4:3 cases below are
    /// exactly the ones a phone screenshot cannot prove.</para>
    ///
    /// <para>Canvas heights are the width-normalised ones the CanvasScaler produces at a 1170
    /// reference width: 2532 (iPhone 14 Pro), 2080 (16:9) and 1560 (4:3).</para>
    /// </summary>
    [TestFixture]
    public class ShotLayoutMathTests
    {
        private const float H_2532 = 2532f;   // 1170x2532 — the reference device
        private const float H_16x9 = 2080f;   // 1170 * 16/9
        private const float H_4x3  = 1560f;   // 1170 * 4/3

        private ControlsConfig _cfg;

        // The lane's own three geometry terms, as authored on PendulumLaneView / FreeSwingLaneView.
        private const float HandleRest     = 70f;
        private const float ClubHalfHeight = 50f;
        private const float LaneTail       = 20f;

        [SetUp]
        public void SetUp() => _cfg = ControlsConfig.Default;

        /// <summary>The baseline with no safe-area inset — the Editor / Game View case. The
        /// inset's own effect has its own test above; every framing case below is about aspect.</summary>
        private float BaselinePx => ShotLayoutMath.Baseline(_cfg.BottomBaselinePx, 0f);

        private float PendulumBallY(float h) => ShotLayoutMath.ResolveBallY(
            _cfg.BallAnchorViewportY_Pendulum, h, BaselinePx, true,
            _cfg.PendulumPull120Px, HandleRest, ClubHalfHeight, LaneTail);

        private float PendulumLaneEnd(float h) => ShotLayoutMath.LaneEndY(
            PendulumBallY(h), _cfg.PendulumPull120Px, HandleRest, ClubHalfHeight, LaneTail);

        /// <summary>Canvas y of the shared bottom baseline for a given height.</summary>
        private float BaselineY(float h) => -h * 0.5f + BaselinePx;

        // ── AnchorY ──────────────────────────────────────────────────────────────

        [Test]
        public void AnchorY_MapsAViewportFractionOntoTheCentreOriginedCanvas()
        {
            Assert.AreEqual(-304f, ShotLayoutMath.AnchorY(0.38f, H_2532), 0.5f,
                "0.38 from the bottom is 62% from the top — the Figma 14153:4602 ball height.");
            Assert.AreEqual(0f, ShotLayoutMath.AnchorY(0.5f, H_2532), 1e-4f,
                "0.5 must be exactly the canvas centre, which is where Flick stays (D1).");
        }

        // ── Baseline ─────────────────────────────────────────────────────────────

        [Test]
        public void Baseline_TakesTheAuthoredValueUntilTheSafeAreaInsetIsDeeper()
        {
            // A 34pt home indicator on a 1170-wide device is ~102 canvas px: 102 + 60 = 162 < 170,
            // so the authored baseline already clears it and nothing moves.
            Assert.AreEqual(170f, ShotLayoutMath.Baseline(170f, 102f), 1e-4f);

            // A deeper inset wins, and keeps its 60px of clearance above the gesture edge.
            Assert.AreEqual(210f, ShotLayoutMath.Baseline(170f, 150f), 1e-4f);
        }

        // ── The reference device: the anchor wins ────────────────────────────────

        [Test]
        public void OnTheReferenceDevice_TheAuthoredAnchorWins_AndTheLaneStillClearsTheBaseline()
        {
            Assert.AreEqual(-304f, PendulumBallY(H_2532), 0.5f,
                "2532 is tall enough for the whole lane, so D6 must not clamp.");

            // The lane end is the thing D6 protects; on this device it lands just ABOVE the
            // baseline rather than exactly on it (D4: the constraint is >=, not equality).
            Assert.GreaterOrEqual(PendulumLaneEnd(H_2532), BaselineY(H_2532),
                "the pull lane must never end below the action buttons' baseline");
            Assert.AreEqual(-1092f, PendulumLaneEnd(H_2532), 0.5f);
        }

        [Test]
        public void OnTheReferenceDevice_TheOneTwentyTickSitsWhereTheSpecSaysItDoes()
        {
            // The drawn tick is at ball - (rest + pull120): the place the CLUB HEAD lands, which
            // is what PendulumLaneView.ApplyGeometry draws and what the acceptance run measures.
            float tick120 = PendulumBallY(H_2532) - (HandleRest + _cfg.PendulumPull120Px);
            Assert.AreEqual(-1022f, tick120, 2f);
        }

        // ── Short screens: D6 raises the ball instead of burying the lane ────────

        [Test]
        public void OnSixteenByNine_TheBallIsRaisedSoTheLaneEndsExactlyOnTheBaseline()
        {
            Assert.Greater(PendulumBallY(H_16x9),
                           ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Pendulum, H_16x9),
                           "16:9 has no room for a 648px pull below 0.38, so D6 must clamp upward.");

            Assert.AreEqual(BaselineY(H_16x9), PendulumLaneEnd(H_16x9), 1e-3f,
                "when the clamp is what wins, the lane end sits ON the baseline, not above it.");
            Assert.AreEqual(-1040f + 170f, PendulumLaneEnd(H_16x9), 1e-3f);
        }

        [Test]
        public void OnFourByThree_TheInvariantHolds_AndTheBallEndsUpAboveTheCanvasCentre()
        {
            Assert.AreEqual(BaselineY(H_4x3), PendulumLaneEnd(H_4x3), 1e-3f);

            // Documented, expected, and ugly: a tablet is short enough that a full-length lane
            // pushes the ball above the middle of the screen. D6 chooses a playable lane over
            // the Figma framing on purpose; a per-aspect anchor table is the follow-up (§5).
            Assert.AreEqual(178f, PendulumBallY(H_4x3), 0.5f);
            Assert.Greater(PendulumBallY(H_4x3), 0f);
        }

        // ── The two schemes with no lane ─────────────────────────────────────────

        [Test]
        public void FlickAndNeedle_TakeTheirAnchorOnEveryAspect_BecauseNeitherDrawsALane()
        {
            foreach (float h in new[] { H_2532, H_16x9, H_4x3 })
            {
                Assert.AreEqual(ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Flick, h),
                    ShotLayoutMath.ResolveBallY(_cfg.BallAnchorViewportY_Flick, h, BaselinePx,
                                                false, 0f, HandleRest, ClubHalfHeight, LaneTail),
                    1e-4f, $"Flick must be untouched at canvas height {h} (control_scheme_seam parity)");

                Assert.AreEqual(ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Needle, h),
                    ShotLayoutMath.ResolveBallY(_cfg.BallAnchorViewportY_Needle, h, BaselinePx,
                                                false, _cfg.NeedlePull120Px, HandleRest, ClubHalfHeight, LaneTail),
                    1e-4f, $"Needle's ring is drawn around the ball, so no clamp at height {h}");
            }
        }

        [Test]
        public void Flick_StaysOnTheCanvasCentre_SoItsSceneAuthoredConeCannotLeaveTheScreen()
        {
            Assert.AreEqual(0.5f, _cfg.BallAnchorViewportY_Flick, 1e-6f);
            Assert.AreEqual(0f, ShotLayoutMath.ResolveBallY(_cfg.BallAnchorViewportY_Flick, H_2532,
                                                            BaselinePx, false, 0f,
                                                            HandleRest, ClubHalfHeight, LaneTail), 1e-4f);
        }

        // ── Free Swing shares the Pendulum's lane numbers ────────────────────────

        [Test]
        public void FreeSwing_ResolvesToTheSameBallAndLaneEndAsThePendulum()
        {
            float fsBall = ShotLayoutMath.ResolveBallY(_cfg.BallAnchorViewportY_FreeSwing, H_2532,
                                                       BaselinePx, true, _cfg.FreeSwingPull120Px,
                                                       HandleRest, ClubHalfHeight, LaneTail);
            Assert.AreEqual(PendulumBallY(H_2532), fsBall, 1e-4f);
            Assert.AreEqual(-1092f, ShotLayoutMath.LaneEndY(fsBall, _cfg.FreeSwingPull120Px,
                                                            HandleRest, ClubHalfHeight, LaneTail), 0.5f);
        }

        // ── The power gauge ──────────────────────────────────────────────────────

        [Test]
        public void PowerHud_AnchoredTopRight_PutsItsCentreAtThirtyPercentFromTheTop()
        {
            // The 200x200 PowerHUD, pivot (1,1): 200 * (1 - 0.5). Written as the expression the
            // controller uses rather than as 100, so a re-pivot of the widget breaks this test
            // instead of silently sliding the gauge half its height.
            const float pivotOffsetAboveCentre = 200f * (1f - 0.5f);
            float y = ShotLayoutMath.PowerHudAnchoredY(_cfg.PowerGaugeViewportY, H_2532, pivotOffsetAboveCentre);
            Assert.AreEqual(-660f, y, 1f);

            // Read the centre back out the way the canvas would: anchor top (+H/2), then the
            // anchoredPosition, then back DOWN to the rect's middle.
            float centreY = H_2532 * 0.5f + y - pivotOffsetAboveCentre;
            Assert.AreEqual(ShotLayoutMath.AnchorY(_cfg.PowerGaugeViewportY, H_2532), centreY, 1f);
        }
    }
}
