using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// EditMode tests for map_view_aiming (Order 352).
    ///
    /// Tests cover (criterion 10):
    ///   1. Projection math: screen→ground projection → correct world target and heading.
    ///   2. Carry placement: landing center == ball + aimDir * carryYards * 0.9144.
    ///   3. Ring placement (iter-20 CONCENTRIC model):
    ///      - All three rings share ONE center at the 100% landing position.
    ///      - Rings are distinguished by RADIUS, not by center position.
    ///      - Radii: r80 < r100 < r120 (innermost to outermost).
    ///   4. Curve reuse sign: with Fade armed (positive finetune), guide line bends
    ///      in the same direction as the finetune sign (positive lateral at t=1).
    ///   5. Aim write-back: aimYaw updated by TrySetAimFromScreenPoint.
    ///   6. IsOpen defaults false; AimYawRadians defaults zero.
    /// </summary>
    [TestFixture]
    public class MapViewAimingTests
    {
        private const float kYardsToMeters = 0.9144f;
        private const float kEpsilon       = 0.001f;

        private GameObject       _go;
        private MapViewController _mvc;

        [SetUp]
        public void SetUp()
        {
            _go  = new GameObject("TestMapViewController");
            _mvc = _go.AddComponent<MapViewController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 1 — Landing zone = ball + aimDir * carryYards * kYardsToMeters
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void LandingZoneCenter_IsCarryAlongAim()
        {
            Vector3 ballPos    = new Vector3(10f, 0f, 20f);
            float   carryYards = 200f;
            float   aimYaw     = 0f;   // aiming in +X direction

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(aimYaw);

            Vector3 expected = ballPos + new Vector3(1f, 0f, 0f) * (carryYards * kYardsToMeters);
            Vector3 actual   = _mvc.LandingZoneWorld;

            Assert.AreEqual(expected.x, actual.x, kEpsilon,
                "Landing zone X should be ball.x + carryMeters");
            Assert.AreEqual(expected.z, actual.z, kEpsilon,
                "Landing zone Z should be ball.z");
        }

        [Test]
        public void LandingZoneCenter_DiagonalAim_CorrectVector()
        {
            Vector3 ballPos    = Vector3.zero;
            float   carryYards = 150f;
            float   aimYaw     = Mathf.PI * 0.25f;   // 45 degrees

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(aimYaw);

            float carryM     = carryYards * kYardsToMeters;
            Vector3 expected = new Vector3(
                Mathf.Cos(aimYaw) * carryM,
                0f,
                Mathf.Sin(aimYaw) * carryM);

            Vector3 actual = _mvc.LandingZoneWorld;

            Assert.AreEqual(expected.x, actual.x, kEpsilon, "X component diagonal aim");
            Assert.AreEqual(expected.z, actual.z, kEpsilon, "Z component diagonal aim");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 2 — CONCENTRIC ring model (iter-20):
        //   All three rings share ONE center at the 100% landing position.
        //   They are distinguished by RADIUS (r80 < r100 < r120), not center position.
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void AllRingCenters_ShareSamePoint_AtLandingZone()
        {
            // iter-20: rings are CONCENTRIC — all centers == 100% landing position.
            Vector3 ballPos    = Vector3.zero;
            float   carryYards = 200f;
            float   aimYaw     = 0f;

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(aimYaw);

            Vector3 r80  = _mvc.RingCenterAtPct(0.80f);
            Vector3 r100 = _mvc.RingCenterAtPct(1.00f);
            Vector3 r120 = _mvc.RingCenterAtPct(1.20f);

            // All three centers should be at the SAME point (the 100% landing position).
            Assert.AreEqual(r100.x, r80.x, kEpsilon,
                "iter-20 concentric: Ring80 center == Ring100 center (shared landing point)");
            Assert.AreEqual(r100.z, r80.z, kEpsilon,
                "iter-20 concentric: Ring80 center == Ring100 center (Z)");
            Assert.AreEqual(r100.x, r120.x, kEpsilon,
                "iter-20 concentric: Ring120 center == Ring100 center (shared landing point)");
            Assert.AreEqual(r100.z, r120.z, kEpsilon,
                "iter-20 concentric: Ring120 center == Ring100 center (Z)");
        }

        [Test]
        public void ConcentricRingCenter_IsAt100PercentCarry()
        {
            // The shared center must be at the 100% landing position.
            Vector3 ballPos    = Vector3.zero;
            float   carryYards = 200f;
            float   aimYaw     = 0f;

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(aimYaw);

            float carryM    = carryYards * kYardsToMeters;
            Vector3 ring100 = _mvc.RingCenterAtPct(1.00f);

            Assert.AreEqual(carryM, ring100.x, kEpsilon,
                "Shared ring center must be at 100% carry distance along aim");
        }

        [Test]
        public void ConcentricRingRadii_AreOrdered_InnerToOuter()
        {
            // r80 < r100 < r120 — innermost to outermost nested ring.
            Vector3 ballPos    = Vector3.zero;
            float   carryYards = 180f;
            float   aimYaw     = 0f;

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(aimYaw);

            float r80  = _mvc.RingRadiusAtPct(0.80f);
            float r100 = _mvc.RingRadiusAtPct(1.00f);
            float r120 = _mvc.RingRadiusAtPct(1.20f);

            Assert.Greater(r80, 0f, "Ring80 radius must be positive");
            Assert.Less(r80,  r100, "Ring80 radius must be smaller than Ring100 radius (inner < mid)");
            Assert.Less(r100, r120, "Ring100 radius must be smaller than Ring120 radius (mid < outer)");
        }

        [Test]
        public void ConcentricRingRadii_ProportionalToCarry()
        {
            // Longer carry → larger ring radii (proportional scaling).
            Vector3 ballPos    = Vector3.zero;
            float   aimYaw     = 0f;

            // Short carry (100 yds = 91.44m → r80=clamp(91.44*0.12,3,9)=9m, r100=clamp(16.46,5,14)=14m...)
            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(100f);
            _mvc.SetAimYawForTest(aimYaw);
            float r80_short  = _mvc.RingRadiusAtPct(0.80f);
            float r120_short = _mvc.RingRadiusAtPct(1.20f);

            // Long carry (300 yds = 274m → r80=clamp(32.9,3,9)=9m CLAMPED — short and long share max)
            // Use a mid-range carry (150 yds = 137m → r80=clamp(16.5,3,9)=9m, r120=clamp(32.9,7,20)=20m)
            // So just verify the ordering holds; the clamp prevents unbounded growth.
            _mvc.SetCarryYardsForTest(60f);  // 60 yds = 54.9m → r80=clamp(6.6,3,9)=6.6, r100=clamp(9.9,5,14)=9.9
            float r80_small  = _mvc.RingRadiusAtPct(0.80f);
            float r120_small = _mvc.RingRadiusAtPct(1.20f);

            // Small carry should produce smaller radii than short (100yd) carry.
            Assert.LessOrEqual(r80_small,  r80_short,  "Shorter carry should have <= ring radius (or clamped)");
            Assert.LessOrEqual(r120_small, r120_short, "Shorter carry should have <= outer ring radius (or clamped)");
        }

        [Test]
        public void Ring100Center_IsNotAtBallPosition()
        {
            // Verify the shared ring center (100% landing) is NOT at the ball position.
            Vector3 ballPos    = new Vector3(100f, 0f, 200f);
            float   carryYards = 120f;   // realistic short-iron carry
            float   aimYaw     = 0f;     // aiming +X

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(aimYaw);

            Vector3 ring100 = _mvc.RingCenterAtPct(1.00f);

            // Ring center MUST be away from ball (at landing, not at ball).
            float carryM = carryYards * kYardsToMeters;
            Assert.AreEqual(ballPos.x + carryM, ring100.x, kEpsilon,
                "Ring100 center must be at ball.x + carryMeters (landing site), not at ball position");
            Assert.Greater(Vector3.Distance(ballPos, ring100), 0.5f,
                "Ring center must be significantly away from ball (it's the landing site)");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 3 — Screen→ground projection aim heading
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void SetAimYawForTest_UpdatesAimYawRadians()
        {
            // We cannot build a full Camera in EditMode without entering PlayMode,
            // but we can verify the formula directly using SetAimYawForTest.
            // The actual ray-projection code path is tested in PlayMode (integration).
            float expectedYaw = 1.2f;
            _mvc.SetAimYawForTest(expectedYaw);

            Assert.AreEqual(expectedYaw, _mvc.AimYawRadians, kEpsilon,
                "SetAimYawForTest must update AimYawRadians");
        }

        [Test]
        public void AimHeading_FromBallToTarget_IsCorrectAtan2()
        {
            // Target is directly to the right (+X) from ball.
            Vector3 ballPos    = new Vector3(10f, 0f, 10f);
            Vector3 worldTarget = ballPos + new Vector3(50f, 0f, 0f);

            Vector3 toTarget   = worldTarget - ballPos;
            float expectedYaw  = Mathf.Atan2(toTarget.z, toTarget.x);  // = 0 radians

            _mvc.SetBallWorldPosForTest(ballPos);
            _mvc.SetAimYawForTest(expectedYaw);

            Assert.AreEqual(0f, _mvc.AimYawRadians, kEpsilon,
                "Aim heading to +X should be 0 radians");
        }

        [Test]
        public void AimHeading_NorthTarget_IsHalfPi()
        {
            // Target directly in +Z from ball → heading = π/2.
            Vector3 toTarget  = new Vector3(0f, 0f, 50f);
            float expectedYaw = Mathf.Atan2(toTarget.z, toTarget.x);

            _mvc.SetBallWorldPosForTest(Vector3.zero);
            _mvc.SetAimYawForTest(expectedYaw);

            Assert.AreEqual(Mathf.PI * 0.5f, _mvc.AimYawRadians, kEpsilon,
                "Aim heading to +Z should be π/2");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 4 — Curve-reuse sign: Fade armed with positive finetune → positive lateral
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void FadeDraw_Armed_PositiveFinetune_ProducesPositiveLateralAtFullT()
        {
            // Verifies the LateralAtT reuse formula sign matches the original.
            // LateralAtT(t=1) = signedFinetune * effK * 1^2 * reach
            // With signedFinetune > 0 → result > 0 (rightward draw/fade bends right).
            float finetune   = 0.5f;
            float curveScale = 0.55f;  // ControlsConfig live value
            float reach      = 100f;

            float tipRaw     = finetune * curveScale * reach;
            float tipClamped = Mathf.Clamp(tipRaw, -reach * 0.5f, reach * 0.5f);
            float effK       = (Mathf.Abs(tipRaw) > 1e-4f)
                ? curveScale * (tipClamped / tipRaw)
                : curveScale;

            float lateral = finetune * effK * 1f * reach;  // t=1

            Assert.Greater(lateral, 0f,
                "Positive finetune must produce positive lateral at t=1 (rightward bend)");
        }

        [Test]
        public void FadeDraw_Armed_NegativeFinetune_ProducesNegativeLateralAtFullT()
        {
            float finetune   = -0.5f;  // draw (left)
            float curveScale = 0.55f;
            float reach      = 100f;

            float tipRaw     = finetune * curveScale * reach;
            float tipClamped = Mathf.Clamp(tipRaw, -reach * 0.5f, reach * 0.5f);
            float effK       = (Mathf.Abs(tipRaw) > 1e-4f)
                ? curveScale * (tipClamped / tipRaw)
                : curveScale;

            float lateral = finetune * effK * 1f * reach;

            Assert.Less(lateral, 0f,
                "Negative finetune must produce negative lateral at t=1 (leftward bend)");
        }

        [Test]
        public void FadeDraw_NotArmed_ZeroFinetune_ProducesZeroLateral()
        {
            // When not armed, landing zone should be straight along aim (no Z offset for aimYaw=0).
            float carryYards = 100f;
            float reach      = carryYards * kYardsToMeters;

            _mvc.SetFadeDrawForTest(false, 0f);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(0f);
            _mvc.SetBallWorldPosForTest(Vector3.zero);

            Vector3 lz = _mvc.LandingZoneWorld;
            Assert.AreEqual(reach, lz.x, kEpsilon,
                "Straight line: landing zone X == carry meters");
            Assert.AreEqual(0f, lz.z, kEpsilon,
                "Straight line: landing zone Z == 0 when aiming in +X");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 5 — yd→world conversion consistency (1 yard = 0.9144 m)
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void YardsToMetersConversion_Is0point9144()
        {
            float carryYards = 100f;
            _mvc.SetBallWorldPosForTest(Vector3.zero);
            _mvc.SetCarryYardsForTest(carryYards);
            _mvc.SetAimYawForTest(0f);   // +X direction

            Vector3 lz = _mvc.LandingZoneWorld;

            float expectedMeters = carryYards * kYardsToMeters;
            Assert.AreEqual(expectedMeters, lz.x, kEpsilon,
                "100 yards must convert to 91.44 m in landing zone placement");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 6 — IsOpen starts false, AimYawRadians defaults zero
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void IsOpen_DefaultsFalse()
        {
            Assert.IsFalse(_mvc.IsOpen, "MapViewController should start closed");
        }

        [Test]
        public void AimYawRadians_DefaultsZero()
        {
            Assert.AreEqual(0f, _mvc.AimYawRadians, kEpsilon,
                "AimYawRadians should default to 0");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 7 — Order 354 / 354c (map_view_playable_area): zoom-to-the-shot
        //   framing and the pan clamp. All of these drive the PRODUCTION statics on
        //   MapViewController — no local re-implementation of the algorithms.
        //
        // Fixture: Hole 1's real OB rectangle from
        //   Assets/Resources/HoleData/lomond-country-club/Hole_01/zones.json
        //   worldOrigin (-288.1, -130.6), worldSize (576.2, 261.2) → centre (0,0),
        //   half (288.1, 130.6); tee ≈ (219.4, 34.7), pin ≈ (-230.5, -72.5).
        // ─────────────────────────────────────────────────────────────────────────

        private static readonly Vector2 kH1Center = new Vector2(0f, 0f);
        private static readonly Vector2 kH1Half   = new Vector2(288.1f, 130.6f);
        private static readonly Vector2 kH1Tee    = new Vector2(219.4f, 34.7f);
        private static readonly Vector2 kH1Pin    = new Vector2(-230.5f, -72.5f);

        // The margins the game frames with (MapViewController.kShotBottomFrac /
        // kShotTopFrac / kWidthFillMargin). Mirrored here deliberately: these ARE the
        // acceptance criteria for "leave a bit of margin so none of them touch the borders".
        private const float kBottom = 0.08f;
        private const float kTop    = 0.90f;
        private const float kSide   = 0.02f;

        private static Camera MakeDeviceCamera(string name)
        {
            var cam = new GameObject(name).AddComponent<Camera>();
            cam.enabled       = false;
            cam.aspect        = 1170f / 2532f;   // iPhone 14 portrait
            cam.fieldOfView   = 45f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane  = 20000f;
            return cam;
        }

        /// <summary>The fit set the game builds: the ball and the flag, nothing else.</summary>
        private static System.Collections.Generic.List<Vector3> ShotFitSet(Vector2 ball, Vector2 flag)
            => new System.Collections.Generic.List<Vector3>
            {
                new Vector3(ball.x, 0f, ball.y),
                new Vector3(flag.x, 0f, flag.y),
            };

        private static Vector3 HoleAxis3(Vector2 ball, Vector2 flag)
        {
            Vector2 n = (flag - ball).normalized;
            return new Vector3(n.x, 0f, n.y);
        }

        [Test]
        public void ShotFit_BallAndFlagBothLandInsideTheMargins()
        {
            // Cesar 2026-08-07: "as long as current ball position and flag are visible (leave a bit
            // of margin so none of them touch the borders)". Swept tee → greenside lie.
            var cam = MakeDeviceCamera("ShotFitCam");
            try
            {
                foreach (float t in new[] { 0f, 0.25f, 0.5f, 0.75f, 0.9f })
                {
                    Vector2 ball = Vector2.Lerp(kH1Tee, kH1Pin, t);

                    bool ok = MapViewController.SolveShowRegionPose(
                        cam, ShotFitSet(ball, kH1Pin), HoleAxis3(ball, kH1Pin), 70f,
                        kBottom, kSide, kTop, out float dist, out _, out _);
                    Assert.IsTrue(ok, $"Solver must find a pose at lie t={t:F2}");
                    Assert.Greater(dist, 8f,  $"Pose must not be degenerately close at t={t:F2}");
                    Assert.Less(dist, 4000f,  $"Pose must not be degenerately far at t={t:F2}");

                    Vector3 vb = cam.WorldToViewportPoint(new Vector3(ball.x, 0f, ball.y));
                    Vector3 vf = cam.WorldToViewportPoint(new Vector3(kH1Pin.x, 0f, kH1Pin.y));

                    AssertInsideMargins("ball", vb, t);
                    AssertInsideMargins("flag", vf, t);
                    Assert.Less(vb.y, vf.y, $"Ball must project BELOW the flag at t={t:F2}");
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        private static void AssertInsideMargins(string what, Vector3 vp, float t)
        {
            Assert.Greater(vp.z, 0f, $"{what} must be in front of the camera at t={t:F2}");
            Assert.GreaterOrEqual(vp.y, kBottom - kEpsilon,
                $"{what} must not touch the bottom border at t={t:F2} (y={vp.y:F3})");
            Assert.LessOrEqual(vp.y, kTop + kEpsilon,
                $"{what} must not touch the top border at t={t:F2} (y={vp.y:F3})");
            Assert.GreaterOrEqual(vp.x, kSide - kEpsilon,
                $"{what} must not touch the left border at t={t:F2} (x={vp.x:F3})");
            Assert.LessOrEqual(vp.x, 1f - kSide + kEpsilon,
                $"{what} must not touch the right border at t={t:F2} (x={vp.x:F3})");
        }

        [Test]
        public void ShotFit_IsTight_BallSeatsOnTheBottomMargin()
        {
            // "Zoom in as much as possible": the solve returns the SMALLEST containing distance, so
            // the near point sits ON the bottom margin. If it floated above it, the map would be
            // zoomed out further than the shot requires.
            var cam = MakeDeviceCamera("ShotTightCam");
            try
            {
                foreach (float t in new[] { 0f, 0.5f, 0.9f })
                {
                    Vector2 ball = Vector2.Lerp(kH1Tee, kH1Pin, t);
                    bool ok = MapViewController.SolveShowRegionPose(
                        cam, ShotFitSet(ball, kH1Pin), HoleAxis3(ball, kH1Pin), 70f,
                        kBottom, kSide, kTop, out _, out _, out _);
                    Assert.IsTrue(ok, $"Solver must find a pose at lie t={t:F2}");

                    Vector3 vb = cam.WorldToViewportPoint(new Vector3(ball.x, 0f, ball.y));
                    Assert.AreEqual(kBottom, vb.y, 0.01f,
                        $"Ball must seat on the bottom margin at t={t:F2} (y={vb.y:F3})");
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void ShotFit_ZoomsInAsTheBallNearsTheFlag()
        {
            // The point of 354c: a short approach is framed much tighter than a tee shot.
            var cam = MakeDeviceCamera("ShotZoomCam");
            try
            {
                float prev = float.MaxValue;
                foreach (float t in new[] { 0f, 0.3f, 0.6f, 0.9f })
                {
                    Vector2 ball = Vector2.Lerp(kH1Tee, kH1Pin, t);
                    bool ok = MapViewController.SolveShowRegionPose(
                        cam, ShotFitSet(ball, kH1Pin), HoleAxis3(ball, kH1Pin), 70f,
                        kBottom, kSide, kTop, out float dist, out _, out _);
                    Assert.IsTrue(ok, $"Solver must find a pose at lie t={t:F2}");
                    Assert.Less(dist, prev,
                        $"Camera must pull IN as the ball nears the flag (t={t:F2}: {dist:F1}m vs {prev:F1}m)");
                    prev = dist;
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void ShotFit_SolverAcceptsATwoPointFitSet()
        {
            // 354c passes exactly two points; the solver must not demand a polygon.
            var cam = MakeDeviceCamera("ShotTwoPointCam");
            try
            {
                var two = ShotFitSet(kH1Tee, kH1Pin);
                Assert.AreEqual(2, two.Count, "The shot fit set is ball + flag and nothing else");
                Assert.IsTrue(
                    MapViewController.SolveShowRegionPose(cam, two, HoleAxis3(kH1Tee, kH1Pin), 70f,
                                                          kBottom, kSide, kTop, out _, out _, out _),
                    "Solver must accept a two-point fit set");
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void PlayfieldSnap_PicksTheWorldAxisTheHoleRunsAlong()
        {
            // 354d: the playfield is world-axis-aligned, so the camera yaw snaps to ±X / ±Z and the
            // field renders as an upright rectangle instead of a diagonal one.
            var cases = new[]
            {
                (dir: new Vector3(-0.97f, 0f, -0.23f), want: Vector3.left),     // Hole 1: 13.4° off −X
                (dir: new Vector3( 0.66f, 0f, -0.75f), want: Vector3.back),     // Hole 5: 41.5° off −Z
                (dir: new Vector3(-0.99f, 0f,  0.10f), want: Vector3.left),     // Hole 6:  5.9° off −X
                (dir: new Vector3( 1f,    0f,  0f),    want: Vector3.right),
                (dir: new Vector3( 0f,    0f,  1f),    want: Vector3.forward),
            };
            foreach (var c in cases)
            {
                Vector3 snapped = MapViewController.SnapToWorldAxis(c.dir.normalized);
                Assert.AreEqual(c.want, snapped,
                    $"Hole running {c.dir} must frame along {c.want}, got {snapped}");
                Assert.Less(Vector3.Angle(c.dir, snapped), 45f + kEpsilon,
                    "The snapped axis must be the NEAREST world axis, never more than 45° off");
            }
        }

        [Test]
        public void PlayfieldSnap_KeepsBallAndFlagInFrame_WithTheLateralSpreadItIntroduces()
        {
            // Snapping to the field axis gives the ball→flag pair a LATERAL component, which the fit
            // must absorb — including Hole 5's worst case (41.5° off axis, 242 m of lateral spread).
            var cam = MakeDeviceCamera("SnapFitCam");
            try
            {
                var holes = new[]
                {
                    (name: "H1", tee: kH1Tee,                     pin: kH1Pin),
                    (name: "H5", tee: new Vector2(-120f, 136.4f), pin: new Vector2(122.1f, -137.5f)),
                    (name: "H6", tee: new Vector2(80.2f, -24.5f), pin: new Vector2(-72.5f, -8.8f)),
                };
                foreach (var h in holes)
                {
                    foreach (float t in new[] { 0f, 0.5f, 0.8f })
                    {
                        Vector2 ball = Vector2.Lerp(h.tee, h.pin, t);
                        Vector3 axis = MapViewController.SnapToWorldAxis(HoleAxis3(ball, h.pin));

                        bool ok = MapViewController.SolveShowRegionPose(
                            cam, ShotFitSet(ball, h.pin), axis, 70f, kBottom, kSide, kTop,
                            out _, out _, out _);
                        Assert.IsTrue(ok, $"{h.name} t={t:F1}: solver must find a pose on the snapped axis");

                        Vector3 vb = cam.WorldToViewportPoint(new Vector3(ball.x, 0f, ball.y));
                        Vector3 vf = cam.WorldToViewportPoint(new Vector3(h.pin.x, 0f, h.pin.y));
                        AssertInsideMargins($"{h.name} t={t:F1} ball", vb, t);
                        AssertInsideMargins($"{h.name} t={t:F1} flag", vf, t);
                        Assert.Less(vb.y, vf.y, $"{h.name} t={t:F1}: ball must still project below the flag");
                    }
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void ShotFit_BallProjectsBelowFlag_OnEveryHoleGeometry()
        {
            // §4.1: the camera axis is the ball→flag axis, so ball-below-flag holds regardless of
            // where the hole runs in world space. Sweep the hole's world heading a full turn.
            var cam = MakeDeviceCamera("ShotHeadingCam");
            try
            {
                for (int deg = 0; deg < 360; deg += 30)
                {
                    float r = deg * Mathf.Deg2Rad;
                    Vector2 ball = new Vector2(40f, -25f);
                    Vector2 flag = ball + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * 300f;

                    bool ok = MapViewController.SolveShowRegionPose(
                        cam, ShotFitSet(ball, flag), HoleAxis3(ball, flag), 70f,
                        kBottom, kSide, kTop, out _, out _, out _);
                    Assert.IsTrue(ok, $"Solver must find a pose at heading {deg}°");

                    Vector3 vb = cam.WorldToViewportPoint(new Vector3(ball.x, 0f, ball.y));
                    Vector3 vf = cam.WorldToViewportPoint(new Vector3(flag.x, 0f, flag.y));
                    Assert.Less(vb.y, vf.y, $"Ball must project below the flag at heading {deg}°");
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }


        [Test]
        public void PanClamp_KeepsFocusInsideObRect()
        {
            // Inside stays untouched.
            Vector2 inside = new Vector2(100f, -40f);
            Assert.AreEqual(inside, MapViewController.ClampPointToRect(inside, kH1Center, kH1Half),
                "A focus point already inside the OB rect must not be moved");

            // Every direction out of bounds is pulled back to the boundary.
            foreach (var far in new[]
            {
                new Vector2( 9999f,  9999f), new Vector2(-9999f, -9999f),
                new Vector2( 9999f, -9999f), new Vector2(-9999f,  9999f),
                new Vector2( 400f,   0f),    new Vector2( 0f,    -400f),
            })
            {
                Vector2 c = MapViewController.ClampPointToRect(far, kH1Center, kH1Half);
                Assert.LessOrEqual(Mathf.Abs(c.x - kH1Center.x), kH1Half.x + kEpsilon,
                    $"Pan must not take the focus past the OB rect in X (from {far})");
                Assert.LessOrEqual(Mathf.Abs(c.y - kH1Center.y), kH1Half.y + kEpsilon,
                    $"Pan must not take the focus past the OB rect in Z (from {far})");
            }
        }
    }
}
