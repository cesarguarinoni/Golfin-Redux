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

        // The lane's own geometry terms. HandleRest is what the CLAMP uses — the finger's offset
        // below the ball. ClubHalfHeight / LaneTail only describe how far the drawn pill extends
        // past the finger, and 50 here is the SPEC's value; the live scene ships 150 because the
        // club head scales to 3x, which is exactly why the clamp must not depend on it.
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
            _cfg.PendulumPull120Px, HandleRest);

        private float PendulumLaneEnd(float h) => ShotLayoutMath.LaneEndY(
            PendulumBallY(h), _cfg.PendulumPull120Px, HandleRest, ClubHalfHeight, LaneTail);

        /// <summary>Where the club head's centre — the finger — ends up at a 120% pull. THIS is
        /// what the baseline guards (D3), not the lane's rounded end.</summary>
        private float PendulumHandleY(float h) => ShotLayoutMath.HandleYAtFullPull(
            PendulumBallY(h), _cfg.PendulumPull120Px, HandleRest);

        /// <summary>Canvas y of the shared bottom baseline for a given height.</summary>
        private float BaselineY(float h) => -h * 0.5f + BaselinePx;

        // ── AnchorY ──────────────────────────────────────────────────────────────

        [Test]
        public void AnchorY_MapsAViewportFractionOntoTheCentreOriginedCanvas()
        {
            Assert.AreEqual(-304f, ShotLayoutMath.AnchorY(0.38f, H_2532), 0.5f,
                "0.38 from the bottom is 62% from the top — the Figma 14153:4602 ball height.");
            Assert.AreEqual(0f, ShotLayoutMath.AnchorY(0.5f, H_2532), 1e-4f,
                "0.5 must be exactly the canvas centre — where all four schemes sat before " +
                "shot_view_layout, and Flick until flick_shot_view D1.");
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
        public void OnTheReferenceDevice_TheAuthoredAnchorWins_AndTheFlickStillClearsTheBaseline()
        {
            Assert.AreEqual(-304f, PendulumBallY(H_2532), 0.5f,
                "2532 is tall enough for the whole pull, so the clamp must not fire.");

            // The 120% handle is what the baseline protects; on this device it lands 74px ABOVE
            // it rather than exactly on it (the constraint is >=, not equality).
            Assert.GreaterOrEqual(PendulumHandleY(H_2532), BaselineY(H_2532),
                "a 120% flick must never be released below the action buttons' baseline");
            Assert.AreEqual(74f, PendulumHandleY(H_2532) - BaselineY(H_2532), 1f);

            // And with the lane geometry the SPEC was written against, the pill's own end still
            // lands where §2 predicted. It only hangs lower once the club head is scaled up.
            Assert.AreEqual(-1092f, PendulumLaneEnd(H_2532), 0.5f);
        }

        [Test]
        public void TheClampIgnoresTheClubHeadSize_SoScalingTheHeadCannotCostFraming()
        {
            // The whole of shot_view_layout D3 in one assertion. `ClubHalfHeight` went 50 -> 150
            // when the club head started scaling to 3x; a clamp that guarded the lane's END would
            // have read that as 100px less room and raised the ball to viewport 0.418, four points
            // of horizon short of the Figma frame. The finger is at the head's CENTRE, so the head
            // growing below it changes nothing about where the flick is released.
            float ball = PendulumBallY(H_2532);
            Assert.AreEqual(ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Pendulum, H_2532), ball, 1e-3f);

            // The pill's tail does now hang below the baseline at the live club size — accepted:
            // it is 120px wide down the centre and the action buttons are at x +/-382..527.
            float laneEndAtLiveClub = ShotLayoutMath.LaneEndY(ball, _cfg.PendulumPull120Px, HandleRest, 150f, LaneTail);
            Assert.Less(laneEndAtLiveClub, BaselineY(H_2532));
            Assert.Greater(laneEndAtLiveClub, -H_2532 * 0.5f,
                "however far the tail hangs, it must still be ON the screen");
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
        public void OnSixteenByNine_TheBallIsRaisedSoTheFlickLandsExactlyOnTheBaseline()
        {
            Assert.Greater(PendulumBallY(H_16x9),
                           ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Pendulum, H_16x9),
                           "16:9 has no room for a 648px pull below 0.38, so the clamp must fire.");

            Assert.AreEqual(BaselineY(H_16x9), PendulumHandleY(H_16x9), 1e-3f,
                "when the clamp is what wins, the 120% handle sits ON the baseline, not above it.");
            Assert.AreEqual(-1040f + 170f, PendulumHandleY(H_16x9), 1e-3f);
        }

        [Test]
        public void OnFourByThree_TheInvariantHolds_AndTheBallEndsUpAboveTheCanvasCentre()
        {
            Assert.AreEqual(BaselineY(H_4x3), PendulumHandleY(H_4x3), 1e-3f);

            // Documented, expected, and ugly: a tablet is short enough that a full-length pull
            // pushes the ball above the middle of the screen. The clamp chooses a reachable flick
            // over the Figma framing on purpose; a per-aspect anchor table is the follow-up (§5).
            Assert.AreEqual(108f, PendulumBallY(H_4x3), 0.5f);
            Assert.Greater(PendulumBallY(H_4x3), 0f);
        }

        // ── The one scheme with no lane ──────────────────────────────────────────

        [Test]
        public void Needle_TakesItsAnchorOnEveryAspect_BecauseItsRingIsDrawnAroundTheBall()
        {
            foreach (float h in new[] { H_2532, H_16x9, H_4x3 })
                Assert.AreEqual(ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Needle, h),
                    ShotLayoutMath.ResolveBallY(_cfg.BallAnchorViewportY_Needle, h, BaselinePx,
                                                false, _cfg.NeedlePull120Px, HandleRest),
                    1e-4f, $"Needle's ring is drawn around the ball, so no clamp at height {h}");
        }

        // ── Flick: the cone IS the lane (flick_shot_view D1/D2/D6) ───────────────

        /// <summary>Flick's clamp depth — the cone BASE below the ball, not a club head at the end
        /// of a pull. <c>ShotConeView</c> hangs the mesh at exactly this, so the guard and the
        /// drawn cone are the same arithmetic.</summary>
        private float FlickDepth => ShotLayoutMath.FlickLaneDepthBelowBall(
            _cfg.FlickConeApexGapPx, _cfg.FlickConeHeightPx);

        private float FlickBallY(float h) => ShotLayoutMath.ResolveBallY(
            _cfg.BallAnchorViewportY_Flick, h, BaselinePx, true, FlickDepth);

        [Test]
        public void Flick_JoinsTheOtherThreeAtThirtyEightPercent_WithItsConeBaseOnTheBaseline()
        {
            Assert.AreEqual(0.38f, _cfg.BallAnchorViewportY_Flick, 1e-6f,
                "flick_shot_view D1 retires the 0.5 anchor the control_scheme_seam parity kept.");
            Assert.AreEqual(792f, _cfg.FlickConeHeightPx, 1e-4f);
            Assert.AreEqual(0f, _cfg.FlickConeApexGapPx, 1e-4f,
                "the apex sits ON the ball — how the scene has always shipped, and what Cesar " +
                "asked for on 2026-09-07. The SPEC's 151 was derived from a 1009px cone that only " +
                "ever existed as a C# default.");
            Assert.AreEqual(792f, FlickDepth, 1e-4f, "0 apex gap + 792 cone");

            float ball = FlickBallY(H_2532);
            Assert.AreEqual(-304f, ball, 0.5f, "2532 is tall enough, so the authored anchor wins.");
            Assert.AreEqual(PendulumBallY(H_2532), ball, 0.5f,
                "All four schemes frame the ball identically now — that is the whole task.");

            // The base is the number the acceptance run measures on ConeMesh.anchoredPosition.
            Assert.AreEqual(-1096f, ball - FlickDepth, 0.5f, "cone base");
            Assert.AreEqual(BaselineY(H_2532), ball - FlickDepth, 0.5f,
                "and the base IS the shared bottom baseline, not merely near it.");
            Assert.AreEqual(ball, ball - _cfg.FlickConeApexGapPx, 1e-4f,
                "cone apex — ON the ball, no gap");
        }

        [Test]
        public void Flick_OnAShortScreen_RaisesTheBallSoTheConeBaseStaysOnTheBaseline()
        {
            foreach (float h in new[] { H_16x9, H_4x3 })
            {
                float ball = FlickBallY(h);
                Assert.Greater(ball, ShotLayoutMath.AnchorY(_cfg.BallAnchorViewportY_Flick, h),
                    $"at {h} there is not 792px below 0.38, so the D6 clamp must raise the ball");
                Assert.AreEqual(BaselineY(h), ball - FlickDepth, 0.5f,
                    $"the cone base lands exactly on the baseline at {h}");
                Assert.Less(ball, h * 0.5f, $"and the ball stays on screen at {h}");
            }
        }

        [Test]
        public void Flick_HandleRestIsAFractionOfTheCone_SoAReCutCannotChangeWhatTouchingItReads()
        {
            float rest = _cfg.FlickHandleStartY01 * _cfg.FlickConeHeightPx;
            Assert.AreEqual(540f, rest, 1f, "0.6818 x 792 — the pull from rest to 100%");
            Assert.AreEqual(-556f, FlickBallY(H_2532) - (_cfg.FlickConeApexGapPx +
                                                         _cfg.FlickConeHeightPx - rest), 1f,
                "handle rest in canvas y");

            // PULL PARITY IS THE POINT OF THE VALUE (Cesar, 2026-09-07). This is the assertion that
            // would catch a Pendulum retune silently un-matching Flick, which is the whole reason
            // the number was chosen rather than authored.
            Assert.AreEqual(_cfg.PendulumPull100Px, rest, 1f,
                "a thumb travels the same distance to 100% in Flick as in Pendulum");
            Assert.AreEqual(_cfg.FreeSwingPull100Px, rest, 1f, "and in Free Swing");

            // The other end of that trade, asserted so it cannot drift unnoticed: ClubHandleDragger
            // reads power off the WHOLE cone height, so a shorter pull starts with more power
            // already dialled in. 0.828 on the old 1160px cone read 17% at touch; this reads 32%.
            Assert.AreEqual(0.6818f, _cfg.FlickHandleStartY01, 1e-4f);
            Assert.AreEqual(0.3182f, 1f - _cfg.FlickHandleStartY01, 1e-4f,
                "power the drag reads the instant the finger lands on the club at rest");
        }

        // ── Free Swing shares the Pendulum's lane numbers ────────────────────────

        [Test]
        public void FreeSwing_ResolvesToTheSameBallAndLaneEndAsThePendulum()
        {
            float fsBall = ShotLayoutMath.ResolveBallY(_cfg.BallAnchorViewportY_FreeSwing, H_2532,
                                                       BaselinePx, true, _cfg.FreeSwingPull120Px,
                                                       HandleRest);
            Assert.AreEqual(PendulumBallY(H_2532), fsBall, 1e-4f);
            Assert.AreEqual(-1022f, ShotLayoutMath.HandleYAtFullPull(fsBall, _cfg.FreeSwingPull120Px,
                                                                     HandleRest), 0.5f);
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
