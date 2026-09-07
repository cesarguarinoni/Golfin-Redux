using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEditor;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Golfin.Gameplay.UI.Controls.FreeSwing;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics;
using Golfin.Physics.Stats;
using Golfin.Physics.Math;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// shot_view_layout_followup §1 — the drawn pill stops at the shared bottom baseline, and the
    /// pull does not.
    ///
    /// <para>THE TWO HALVES ARE THE POINT. `ClubHalfHeight` went 50 -> 150 for the 3x club head, so
    /// the pill's rounded tail hung 96px past the row the action buttons sit on. Trimming the pill
    /// is cosmetic; trimming the PULL would silently cost the player 96px of the travel that buys
    /// power. The drivers clamp on <c>cfg.*Pull120Px</c> and never read <c>LaneHeight</c>, and the
    /// last fixture here holds them to that with the cap actually applied.</para>
    /// </summary>
    [TestFixture]
    public class LaneEndCapTests
    {
        private const float H_2532 = 2532f;
        private const float H_16x9 = 2080f;

        // The live scene's lane geometry: 3x club head, so 150 rather than the 50 the original
        // spec's arithmetic was written against.
        private const float HandleRest     = 70f;
        private const float ClubHalfHeight = 150f;
        private const float LaneTail       = 20f;
        private const float FollowThrough  = 160f;

        private ControlsConfig _cfg;

        [SetUp]
        public void SetUp() => _cfg = ControlsConfig.Default;

        private float BaselineY(float h) => -h * 0.5f + ShotLayoutMath.Baseline(_cfg.BottomBaselinePx, 0f);

        private float BallY(float h) => ShotLayoutMath.ResolveBallY(
            _cfg.BallAnchorViewportY_Pendulum, h, ShotLayoutMath.Baseline(_cfg.BottomBaselinePx, 0f),
            true, _cfg.PendulumPull120Px, HandleRest);

        // ── The arithmetic ───────────────────────────────────────────────────────

        [Test]
        public void OnTheReferenceDevice_ThePendulumPillIsTrimmedToTheBaseline()
        {
            float ball    = BallY(H_2532);                                   // -303.84
            float deepest = HandleRest + _cfg.PendulumPull120Px;             // 718, from the lane top
            float derived = deepest + ClubHalfHeight + LaneTail;             // 888 — what it wants to be

            float height = ShotLayoutMath.CappedLaneHeight(derived, deepest, ball, BaselineY(H_2532));

            Assert.AreEqual(792f, height, 0.5f, "888 derived minus the 96px that hung past the baseline");
            Assert.AreEqual(BaselineY(H_2532), ball - height, 0.5f, "the pill now ENDS on the baseline");
            Assert.AreEqual(-1096f, ball - height, 0.5f);
        }

        [Test]
        public void OnTheReferenceDevice_TheFreeSwingPillIsTrimmedToTheSameBaseline()
        {
            // Free Swing's lane starts FollowThroughPx ABOVE the ball, so both its height and its
            // deepest tick are measured from a top edge that is 160px higher up.
            float ball    = BallY(H_2532);
            float laneTop = ball + FollowThrough;
            float deepest = FollowThrough + HandleRest + _cfg.FreeSwingPull120Px;   // 878
            float derived = deepest + ClubHalfHeight + LaneTail;                    // 1048

            float height = ShotLayoutMath.CappedLaneHeight(derived, deepest, laneTop, BaselineY(H_2532));

            Assert.AreEqual(-1096f, laneTop - height, 0.5f, "both lanes end on one line");
            Assert.Less(height, derived, "the cap must actually bite here");
        }

        [Test]
        public void OnSixteenByNine_TheEightPixelFloorBeatsTheCap()
        {
            // 16:9 is the case the floor exists for. The ball clamp has already put the 120% TICK
            // exactly on the baseline, so a pill capped AT the baseline would draw its rounded end
            // straight through the tick. The floor wins by those 8px and the pill ends just below
            // the line instead — which is the lesser of the two wrongs, and deliberate.
            float ball    = BallY(H_16x9);                                   // -152
            float deepest = HandleRest + _cfg.PendulumPull120Px;             // 718
            float derived = deepest + ClubHalfHeight + LaneTail;             // 888

            Assert.AreEqual(BaselineY(H_16x9), ball - deepest, 0.5f,
                "precondition: at 16:9 the 120% tick is already ON the baseline");

            float height = ShotLayoutMath.CappedLaneHeight(derived, deepest, ball, BaselineY(H_16x9));

            Assert.AreEqual(deepest + ShotLayoutMath.MinTailBelowDeepestTickPx, height, 1e-3f);
            Assert.AreEqual(BaselineY(H_16x9) - ShotLayoutMath.MinTailBelowDeepestTickPx,
                            ball - height, 1e-3f, "8px below the line, not on it");
        }

        [Test]
        public void ACapThatWouldCutIntoTheTicks_IsIgnored()
        {
            float ball    = BallY(H_2532);
            float deepest = HandleRest + _cfg.PendulumPull120Px;
            float derived = deepest + ClubHalfHeight + LaneTail;

            // A baseline halfway up the lane: absurd, and the floor still protects the 120% line.
            float height = ShotLayoutMath.CappedLaneHeight(derived, deepest, ball, ball - 300f);

            Assert.AreEqual(deepest + ShotLayoutMath.MinTailBelowDeepestTickPx, height, 1e-3f);
        }

        [Test]
        public void APutt_IsShortEnoughThatTheCapNeverBites()
        {
            float ball    = BallY(H_2532);
            float deepest = HandleRest + _cfg.PendulumPull100Px;             // a putt has no 120% tick
            float derived = deepest + ClubHalfHeight + LaneTail;             // 780

            float height = ShotLayoutMath.CappedLaneHeight(derived, deepest, ball, BaselineY(H_2532));

            Assert.AreEqual(derived, height, 1e-3f, "a putt's pill already stops short of the line");
        }

        [Test]
        public void NoCapSet_DrawsExactlyWhatItDrewBefore()
        {
            float deepest = HandleRest + _cfg.PendulumPull120Px;
            float derived = deepest + ClubHalfHeight + LaneTail;

            Assert.AreEqual(derived,
                ShotLayoutMath.CappedLaneHeight(derived, deepest, 0f, float.NegativeInfinity), 1e-4f,
                "an Editor scene with no ShotLayoutController must be untouched");
            Assert.AreEqual(derived,
                ShotLayoutMath.CappedLaneHeight(derived, deepest, float.NaN, -1096f), 1e-4f,
                "and so must a view with no canvas to measure its top edge against");
        }

        // ── The live view, and the pull it must not touch ────────────────────────

        [Test]
        public void TheDrawnPillIsCapped_AndAOneTwentyPullStillPublishesOneTwenty()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var canvasRt = (RectTransform)canvasGo.transform;
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;  // so the rect is OURS
            canvasRt.sizeDelta = new Vector2(1170f, H_2532);

            var laneRootGo = new GameObject("PendulumLaneRoot", typeof(RectTransform));
            var laneRootRt = (RectTransform)laneRootGo.transform;
            laneRootRt.SetParent(canvasRt, false);
            Centre(laneRootRt);
            laneRootRt.anchoredPosition = new Vector2(0f, BallY(H_2532));       // where the ball is

            var pillGo = new GameObject("PowerLane", typeof(RectTransform));
            var pillRt = (RectTransform)pillGo.transform;
            pillRt.SetParent(laneRootRt, false);
            Centre(pillRt);
            pillRt.pivot = new Vector2(0.5f, 1f);          // top edge on the ball, as the builder authors it
            pillRt.anchoredPosition = Vector2.zero;
            pillRt.sizeDelta = new Vector2(120f, 0f);

            var lane = laneRootGo.AddComponent<PendulumLaneView>();
            var so = new SerializedObject(lane);
            so.FindProperty("_lane").objectReferenceValue = pillRt;
            so.FindProperty("_handleRestBelowBall").floatValue = HandleRest;
            so.FindProperty("_clubHalfHeight").floatValue      = ClubHalfHeight;
            so.FindProperty("_laneTailPx").floatValue          = LaneTail;
            so.ApplyModifiedPropertiesWithoutUndo();

            var scGo = new GameObject("LaneEndCap_SC");
            var sc   = scGo.AddComponent<ShotController>();
            sc.InjectConfig(_cfg);
            sc.InjectStatBundle(new StatBundle(ClubStats.DefaultDriver, BallStats.Neutral,
                CharacterStats.Neutral, fp.FromInt(100), fp.FromInt(100)));

            var rootGo   = new GameObject("SchemeRoot_Pendulum", typeof(RectTransform));
            var handleGo = new GameObject("PendulumHandle", typeof(RectTransform));
            handleGo.transform.SetParent(rootGo.transform, false);
            ((RectTransform)handleGo.transform).anchoredPosition = new Vector2(0f, -HandleRest);

            var driver = rootGo.AddComponent<PendulumSchemeDriver>();
            driver.ConfigureForTests((RectTransform)rootGo.transform,
                                     (RectTransform)handleGo.transform,
                                     lane, null, null, _cfg);

            try
            {
                // The cap arrives BEFORE Activate, which is where ApplyGeometry derives the height —
                // the same order ShotLayoutController and ShotSchemeHost.Apply put them in.
                lane.SetLaneEndCapY(BaselineY(H_2532));
                driver.Bind(sc);
                driver.Activate();

                Assert.AreEqual(792f, lane.LaneHeight, 0.5f, "the DRAWN pill is trimmed");
                Assert.AreEqual(792f, pillRt.sizeDelta.y, 0.5f, "and the rect actually carries it");

                var corners = new Vector3[4];
                pillRt.GetWorldCorners(corners);
                Assert.AreEqual(-1096f, canvasRt.InverseTransformPoint(corners[0]).y, 0.5f,
                    "pill bottom sits on the baseline in canvas space");

                // ...and the pull is untouched. 120% of travel still publishes 120% of power,
                // even though the club head at that pull now overhangs the pill's end by ~100px.
                driver.OnPointerDown(At(500f, 900f));
                driver.OnDrag(At(500f, 900f - _cfg.PendulumPull120Px));

                Assert.AreEqual(1.2f, sc.PowerNormalized, 1e-4f,
                    "the driver clamps on PendulumPull120Px, never on LaneHeight");
                // The head's CENTRE at full pull is still inside the pill (718 of 792); it is its
                // BOTTOM EDGE, a further ClubHalfHeight down, that overhangs — which is the whole
                // reason the pill needed capping rather than the pull.
                float headCentreAtFullPull = HandleRest + _cfg.PendulumPull120Px;
                Assert.Less(headCentreAtFullPull, lane.LaneHeight, "the finger stays on the pill");
                Assert.Greater(headCentreAtFullPull + ClubHalfHeight, lane.LaneHeight,
                    "and the 3x head really does now hang past the drawn end");
            }
            finally
            {
                driver.Deactivate();
                Object.DestroyImmediate(rootGo);
                Object.DestroyImmediate(scGo);
                Object.DestroyImmediate(canvasGo);
            }
        }

        private static void Centre(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static PointerEventData At(float x, float y)
            => new PointerEventData(EventSystem.current) { position = new Vector2(x, y) };
    }
}
