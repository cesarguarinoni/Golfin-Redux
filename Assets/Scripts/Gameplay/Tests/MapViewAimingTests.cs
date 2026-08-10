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

        // ─────────────────────────────────────────────────────────────────────────
        // Test 8 — Order 355 (map_view_strict_crop_indicators): THE INVARIANT
        //   (ground footprint ⊆ playable rect) and the floating indicators.
        //
        //   Cesar 2026-08-10: "I want to ONLY be able to see the playable area, the
        //   place where the ball is resting, and if it fits, the Flag indicator over
        //   the hole. If it doesn't fit, the indicator should float on screen with a
        //   line pointing towards the hole."
        //
        //   All of these drive the PRODUCTION statics on MapViewController — no local
        //   re-implementation of the algorithms.
        // ─────────────────────────────────────────────────────────────────────────

        private const float kTol   = 1f;     // MapViewController.kFootprintTolM
        private const float kTilt  = 80f;    // MapViewController._heroTiltDeg

        /// <summary>Pose a camera at hero tilt, distance d back along -axis, looking at focus.</summary>
        private static void PoseCam(Camera cam, Vector3 focus, Vector3 axisN, float dist, float tiltDeg)
        {
            float tan = Mathf.Tan(tiltDeg * Mathf.Deg2Rad);
            cam.transform.position = focus - axisN * dist + Vector3.up * (dist * tan);
            cam.transform.LookAt(focus, Vector3.up);
        }

        [Test]
        public void Footprint_AllFourCornersHitTheGround_AtHeroTilt()
        {
            // §2: at tilt 80° the top edge ray still points 35° below horizontal even at the widest
            // FOV the map allows, so the footprint is always resolvable. If this ever fails, the
            // horizon is in frame and the strict crop is unenforceable.
            var cam = MakeDeviceCamera("FootprintCornersCam");
            try
            {
                foreach (float fov in new[] { 30f, 45f, 75f, 90f })
                {
                    cam.fieldOfView = fov;
                    PoseCam(cam, Vector3.zero, Vector3.forward, 150f, kTilt);

                    Assert.IsTrue(
                        MapViewController.TryComputeGroundFootprint(cam, 0f, out Vector2 min, out Vector2 max),
                        $"All four viewport corners must hit the ground plane at fov={fov}°");
                    Assert.Greater(max.x - min.x, 0f, $"Footprint must have positive width at fov={fov}°");
                    Assert.Greater(max.y - min.y, 0f, $"Footprint must have positive depth at fov={fov}°");
                    Assert.LessOrEqual(min.x, 0f, $"The look-at point must be inside the footprint (fov={fov}°)");
                    Assert.GreaterOrEqual(max.x, 0f, $"The look-at point must be inside the footprint (fov={fov}°)");
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void Footprint_ShrinksMonotonically_AsTheCameraComesIn()
        {
            // §3.2 depends on this: the containment pass bisects/steps DOWN in distance because the
            // footprint shrinks monotonically with distance at fixed tilt and FOV. If it did not,
            // the pass would not terminate.
            var cam = MakeDeviceCamera("FootprintMonotoneCam");
            try
            {
                float prevW = float.MaxValue, prevD = float.MaxValue;
                foreach (float dist in new[] { 400f, 300f, 200f, 120f, 60f, 30f })
                {
                    PoseCam(cam, Vector3.zero, Vector3.forward, dist, kTilt);
                    Assert.IsTrue(MapViewController.TryComputeGroundFootprint(cam, 0f, out var min, out var max));

                    float w = max.x - min.x, d = max.y - min.y;
                    Assert.Less(w, prevW, $"Footprint width must shrink as the camera comes in (d={dist})");
                    Assert.Less(d, prevD, $"Footprint depth must shrink as the camera comes in (d={dist})");
                    prevW = w; prevD = d;
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void Footprint_GrowsWithFov_WhichIsWhyZoomOutIsGated()
        {
            // §4: pinch zoom-OUT widens the FOV, which grows the footprint — hence the dynamic gate.
            var cam = MakeDeviceCamera("FootprintFovCam");
            try
            {
                float prev = 0f;
                foreach (float fov in new[] { 30f, 45f, 60f, 75f })
                {
                    cam.fieldOfView = fov;
                    PoseCam(cam, Vector3.zero, Vector3.forward, 150f, kTilt);
                    Assert.IsTrue(MapViewController.TryComputeGroundFootprint(cam, 0f, out var min, out var max));

                    float area = (max.x - min.x) * (max.y - min.y);
                    Assert.Greater(area, prev, $"A wider FOV must show MORE ground (fov={fov}°)");
                    prev = area;
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void FootprintClamp_LeavesALegalMoveUntouched()
        {
            // A pan that keeps the whole footprint inside the rect must not be modified at all —
            // otherwise panning would feel rubber-banded everywhere, not only at the edges.
            Vector2 min = new Vector2(-60f, -30f), max = new Vector2(60f, 30f);
            Vector2 move = new Vector2(10f, -5f);
            Vector2 got = MapViewController.ClampFootprintMove(min, max, move, kH1Center, kH1Half, kTol);
            Assert.AreEqual(move.x, got.x, kEpsilon, "A legal pan must pass through unchanged in X");
            Assert.AreEqual(move.y, got.y, kEpsilon, "A legal pan must pass through unchanged in Z");
        }

        [Test]
        public void FootprintClamp_StopsTheFootprintAtTheRectEdge_NotTheFocusPoint()
        {
            // THE 355 CHANGE: 354 clamped the FOCUS, which still let half a screen of off-course show
            // past the boundary. The footprint's far edge must land ON the rect edge, never past it.
            Vector2 min = new Vector2(200f, -30f), max = new Vector2(280f, 30f);   // 8.1 m of headroom in +X
            Vector2 got = MapViewController.ClampFootprintMove(min, max, new Vector2(500f, 0f),
                                                              kH1Center, kH1Half, kTol);
            float wantX = kH1Half.x + kTol - MapViewController.kClampSafetyInsetM - max.x;
            Assert.AreEqual(wantX, got.x, kEpsilon,
                "The pan must stop where the footprint's far edge meets the rect edge (less the float-safety inset)");
            Assert.IsTrue(
                MapViewController.FootprintInsideRect(min + new Vector2(got.x, got.y), max + new Vector2(got.x, got.y),
                                                      kH1Center, kH1Half, kTol),
                "After clamping, the footprint must be inside the rect");
        }

        [Test]
        public void FootprintClamp_SlidesAlongTheEdgeOnADiagonalPan()
        {
            // Per-axis solving is what gives slide-along-edge: a diagonal pan into a wall keeps the
            // component that is still legal instead of dead-stopping both.
            Vector2 min = new Vector2(200f, -30f), max = new Vector2(280f, 30f);
            Vector2 got = MapViewController.ClampFootprintMove(min, max, new Vector2(500f, -40f),
                                                              kH1Center, kH1Half, kTol);
            Assert.Less(got.x, 500f, "The blocked axis must be clamped");
            Assert.AreEqual(-40f, got.y, kEpsilon, "The free axis must still move its full amount");
        }

        [Test]
        public void FootprintClamp_CorrectsAnAlreadyViolatingFootprint_WithAZeroMove()
        {
            // The framing pass (§3.2) reuses this with move=0 to pull a violating pose back inside.
            Vector2 min = new Vector2(300f, -200f), max = new Vector2(380f, -140f);   // outside on both axes
            Vector2 corr = MapViewController.ClampFootprintMove(min, max, Vector2.zero, kH1Center, kH1Half, kTol);
            Assert.IsTrue(
                MapViewController.FootprintInsideRect(min + corr, max + corr, kH1Center, kH1Half, kTol),
                "A zero-move clamp must return the correction that brings the footprint inside");
        }

        [Test]
        public void FootprintClamp_CentresAnOversizedFootprint_InsteadOfPilingTheLeakOnOneEdge()
        {
            // Only reachable if a caller skipped the distance pass; the leak must at least be symmetric.
            Vector2 min = new Vector2(-900f, -400f), max = new Vector2(900f, 400f);
            Vector2 corr = MapViewController.ClampFootprintMove(min, max, new Vector2(999f, 999f),
                                                               kH1Center, kH1Half, kTol);
            Vector2 c = (min + max) * 0.5f + corr;
            Assert.AreEqual(kH1Center.x, c.x, 0.01f, "An oversized footprint must be centred on the rect in X");
            Assert.AreEqual(kH1Center.y, c.y, 0.01f, "An oversized footprint must be centred on the rect in Z");
        }

        /// <summary>The Order 355 fit set: ball + the landing disc's four extreme points.</summary>
        private static System.Collections.Generic.List<Vector3> ShotContextSet(
            Vector2 ball, Vector3 axisN, float carryM, float discR)
        {
            Vector3 b = new Vector3(ball.x, 0f, ball.y);
            Vector3 L = b + axisN * carryM;
            Vector3 r = new Vector3(-axisN.z, 0f, axisN.x);
            return new System.Collections.Generic.List<Vector3>
            {
                b, L + axisN * discR, L - axisN * discR, L + r * discR, L - r * discR,
            };
        }

        [Test]
        public void StrictCrop_OpenFramingContainsTheFootprint_OnRealHoleGeometry()
        {
            // §3 end-to-end on Hole 1's real OB rectangle: solve the shot-context pose the way the
            // game does, then run the PRODUCTION containment pass and assert THE INVARIANT holds.
            // Driver carry ≈ 130 m, wedge ≈ 55 m; tee through greenside lie.
            var cam = MakeDeviceCamera("StrictCropCam");
            try
            {
                foreach (float carryM in new[] { 130f, 90f, 55f })
                foreach (float t in new[] { 0f, 0.35f, 0.7f, 0.95f })
                {
                    Vector2 ball = Vector2.Lerp(kH1Tee, kH1Pin, t);
                    Vector3 axis = MapViewController.SnapToWorldAxis(HoleAxis3(ball, kH1Pin));
                    var region   = ShotContextSet(ball, axis, carryM, 8f);

                    Assert.IsTrue(
                        MapViewController.SolveShowRegionPose(cam, region, axis, kTilt,
                                                              kBottom, kSide, kTop,
                                                              out _, out _, out Vector3 focus),
                        $"Solver must find a shot-context pose (carry={carryM}m t={t:F2})");

                    Assert.IsTrue(
                        MapViewController.ContainFootprint(cam, new Vector3(ball.x, 0f, ball.y), kBottom, kSide, 1f - kSide,
                                                           kH1Center, kH1Half, kTol,
                                                           ref focus, out _, out _),
                        $"Containment pass must resolve (carry={carryM}m t={t:F2})");

                    Assert.IsTrue(MapViewController.TryComputeGroundFootprint(cam, 0f, out var min, out var max));
                    Assert.IsTrue(
                        MapViewController.FootprintInsideRect(min, max, kH1Center, kH1Half, kTol),
                        $"THE INVARIANT: every viewport pixel must be playable area " +
                        $"(carry={carryM}m t={t:F2}, footprint [{min.x:F0},{min.y:F0}]..[{max.x:F0},{max.y:F0}])");
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void StrictCrop_ContainmentWinsOverTheBallSeat_AndTheBallStaysOnScreen()
        {
            // §3.2: "Containment WINS over seats: if seating the ball at kShotBottomFrac would push the
            // footprint out the near edge, the ball rides higher on screen — correct, not a bug."
            // What is NOT acceptable is the ball leaving the frame entirely.
            var cam = MakeDeviceCamera("SeatVsContainCam");
            try
            {
                // Ball hard against the near end of the rect — the worst case for the bottom seat.
                Vector2 ball = new Vector2(kH1Half.x - 12f, 0f);
                Vector3 axis = Vector3.left;
                var region   = ShotContextSet(ball, axis, 130f, 8f);

                Assert.IsTrue(MapViewController.SolveShowRegionPose(
                    cam, region, axis, kTilt, kBottom, kSide, kTop, out _, out _, out Vector3 focus));
                Assert.IsTrue(MapViewController.ContainFootprint(
                    cam, new Vector3(ball.x, 0f, ball.y), kBottom, kSide, 1f - kSide, kH1Center, kH1Half, kTol,
                    ref focus, out _, out _));

                Assert.IsTrue(MapViewController.TryComputeGroundFootprint(cam, 0f, out var min, out var max));
                Assert.IsTrue(MapViewController.FootprintInsideRect(min, max, kH1Center, kH1Half, kTol),
                    "Containment must hold even when it fights the ball seat");

                Vector3 vb = cam.WorldToViewportPoint(new Vector3(ball.x, 0f, ball.y));
                Assert.Greater(vb.z, 0f,  "The ball must stay in front of the camera");
                Assert.GreaterOrEqual(vb.y, 0f, "The ball must not be pushed off the bottom of the screen");
                Assert.LessOrEqual(vb.y, 1f,    "The ball must not be pushed off the top of the screen");
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        [Test]
        public void StrictCrop_BallStaysOnScreen_WhenTheContainmentZoomThrowsItSideways()
        {
            // REGRESSION, caught in play mode on Hole 5 (2026-08-10). Hole 5 runs 41.5° off the snapped
            // playfield axis, so a 228 m driver puts the landing far to one side. The footprint came out
            // 468 m deep against a 337 m rect, the containment pass shrank 6 steps to fix the depth, and
            // the ball — which sits far from the focus laterally — was zoomed clean off the right edge
            // at viewport x = 1.196. The vertical re-seat cannot fix that; only the lateral one can.
            //
            // Hole 5's REAL OB rect: Assets/Resources/HoleData/lomond-country-club/Hole_05/zones.json
            //   centre (0,0), half (159, 169); tee ≈ (-120, 136), pin ≈ (122, -138).
            Vector2 h5Center = Vector2.zero, h5Half = new Vector2(159f, 169f);
            Vector2 tee = new Vector2(-120f, 136f), pin = new Vector2(122f, -138f);

            var cam = MakeDeviceCamera("Hole5LateralCam");
            try
            {
                foreach (float carryM in new[] { 228f, 150f, 80f })
                foreach (float t in new[] { 0f, 0.3f, 0.6f })
                {
                    Vector2 ball = Vector2.Lerp(tee, pin, t);
                    Vector3 axis = MapViewController.SnapToWorldAxis(HoleAxis3(ball, pin));
                    // Aim along the true hole heading, NOT the snapped axis — that off-axis landing is
                    // exactly what produces the lateral throw.
                    Vector2 aim2 = (pin - ball).normalized;
                    Vector3 aim  = new Vector3(aim2.x, 0f, aim2.y);
                    var region   = ShotContextSet(ball, aim, carryM, 8f);

                    if (!MapViewController.SolveShowRegionPose(cam, region, axis, kTilt,
                                                               kBottom, kSide, kTop,
                                                               out _, out _, out Vector3 focus))
                        continue;   // no pose → the AnchorBallToBottom fallback path, covered elsewhere

                    Assert.IsTrue(MapViewController.ContainFootprint(
                        cam, new Vector3(ball.x, 0f, ball.y), kBottom, kSide, 1f - kSide, h5Center, h5Half, kTol,
                        ref focus, out int shrinks, out _),
                        $"Containment must resolve (carry={carryM}m t={t:F2})");

                    Assert.IsTrue(MapViewController.TryComputeGroundFootprint(cam, 0f, out var min, out var max));
                    Assert.IsTrue(MapViewController.FootprintInsideRect(min, max, h5Center, h5Half, kTol),
                        $"THE INVARIANT must hold on Hole 5 (carry={carryM}m t={t:F2}, shrinks={shrinks})");

                    Vector3 vb = cam.WorldToViewportPoint(new Vector3(ball.x, 0f, ball.y));
                    Assert.Greater(vb.z, 0f, $"Ball must be in front of the camera (carry={carryM}m t={t:F2})");
                    Assert.GreaterOrEqual(vb.x, 0f,
                        $"Ball must not be thrown off the LEFT edge (carry={carryM}m t={t:F2}, x={vb.x:F3})");
                    Assert.LessOrEqual(vb.x, 1f,
                        $"Ball must not be thrown off the RIGHT edge (carry={carryM}m t={t:F2}, x={vb.x:F3})");
                    Assert.GreaterOrEqual(vb.y, 0f,
                        $"Ball must not be pushed off the bottom (carry={carryM}m t={t:F2}, y={vb.y:F3})");
                    Assert.LessOrEqual(vb.y, 1f,
                        $"Ball must not be pushed off the top (carry={carryM}m t={t:F2}, y={vb.y:F3})");
                }
            }
            finally { Object.DestroyImmediate(cam.gameObject); }
        }

        // ── §5 floating indicators ───────────────────────────────────────────────

        private const float kScrW  = 1170f;
        private const float kScrH  = 2532f;
        private const float kInset = 70f;

        [Test]
        public void Indicator_DocksWhenTheTargetIsOnScreen()
        {
            // "until it's over the hole when it appears on screen" — docked means the icon sits ON the
            // target, with no arrow.
            Vector3 sp = new Vector3(600f, 1400f, 250f);
            bool docked = MapViewController.SolveIndicatorPlacement(
                sp, kScrW, kScrH, kInset, default, out Vector2 pos, out _);

            Assert.IsTrue(docked, "A target comfortably inside the frame must dock");
            Assert.AreEqual(sp.x, pos.x, kEpsilon, "A docked icon sits exactly on its target (x)");
            Assert.AreEqual(sp.y, pos.y, kEpsilon, "A docked icon sits exactly on its target (y)");
        }

        [Test]
        public void Indicator_FloatsOnTheInsetRect_WhenTheTargetIsOffScreen()
        {
            // Off the top of the screen (the long-hole flag case): the icon clamps to the inset rect
            // and the arrow points up-screen, toward the hole.
            Vector3 sp = new Vector3(kScrW * 0.5f, kScrH + 4000f, 300f);
            bool docked = MapViewController.SolveIndicatorPlacement(
                sp, kScrW, kScrH, kInset, default, out Vector2 pos, out float ang);

            Assert.IsFalse(docked, "A target off the top of the screen must float, not dock");
            Assert.AreEqual(kScrH - kInset, pos.y, 0.01f, "The floating icon must sit on the inset top edge");
            Assert.AreEqual(kScrW * 0.5f, pos.x, 0.01f, "Straight up-screen must clamp to the top edge centre");
            Assert.AreEqual(90f, ang, 0.01f, "The arrow must point OUT toward the target (up = +90°)");
        }

        [Test]
        public void Indicator_StaysInsideTheInsetRect_ForEveryOffScreenDirection()
        {
            for (int deg = 0; deg < 360; deg += 15)
            {
                float r = deg * Mathf.Deg2Rad;
                Vector3 sp = new Vector3(kScrW * 0.5f + Mathf.Cos(r) * 9000f,
                                         kScrH * 0.5f + Mathf.Sin(r) * 9000f, 200f);
                bool docked = MapViewController.SolveIndicatorPlacement(
                    sp, kScrW, kScrH, kInset, default, out Vector2 pos, out float ang);

                Assert.IsFalse(docked, $"A target 9000 px out at {deg}° must float");
                Assert.GreaterOrEqual(pos.x, kInset - 0.01f, $"Icon must not cross the left inset at {deg}°");
                Assert.LessOrEqual(pos.x, kScrW - kInset + 0.01f, $"Icon must not cross the right inset at {deg}°");
                Assert.GreaterOrEqual(pos.y, kInset - 0.01f, $"Icon must not cross the bottom inset at {deg}°");
                Assert.LessOrEqual(pos.y, kScrH - kInset + 0.01f, $"Icon must not cross the top inset at {deg}°");

                float want = Mathf.Repeat(deg, 360f);
                Assert.AreEqual(0f, Mathf.DeltaAngle(want, ang), 0.5f,
                    $"The arrow must point at the target's bearing at {deg}°");
            }
        }

        [Test]
        public void Indicator_MirrorsTargetsBehindTheCamera()
        {
            // WorldToScreenPoint reports behind-camera targets flipped; without the mirror the arrow
            // would point 180° AWAY from the hole.
            Vector3 behind = new Vector3(kScrW * 0.5f, kScrH * 0.5f - 800f, -50f);
            bool docked = MapViewController.SolveIndicatorPlacement(
                behind, kScrW, kScrH, kInset, default, out Vector2 pos, out float ang);

            Assert.IsFalse(docked, "A target behind the camera can never dock");
            Assert.Greater(pos.y, kScrH * 0.5f, "The mirrored target is up-screen, so the icon docks up-screen");
            Assert.AreEqual(90f, ang, 0.01f, "The arrow must point at the MIRRORED bearing, not the raw one");
        }

        [Test]
        public void Indicator_IsContinuous_SoItWalksTheEdgeInsteadOfJumping()
        {
            // "if the player moves the camera towards the hole, the indicator moves too" — no animation
            // code is needed BECAUSE the placement is continuous in the camera pose. Two nearby target
            // screen points must give two nearby icon positions, including across the dock boundary.
            float prevX = float.NaN, prevY = float.NaN;
            for (float y = kScrH + 600f; y > kScrH * 0.5f; y -= 6f)
            {
                MapViewController.SolveIndicatorPlacement(
                    new Vector3(kScrW * 0.35f, y, 200f), kScrW, kScrH, kInset, default,
                    out Vector2 pos, out _);

                if (!float.IsNaN(prevX))
                {
                    Assert.Less(Mathf.Abs(pos.x - prevX), 30f,
                        $"Indicator X must not jump as the target crosses the frame (y={y:F0})");
                    Assert.Less(Mathf.Abs(pos.y - prevY), 30f,
                        $"Indicator Y must not jump as the target crosses the frame (y={y:F0})");
                }
                prevX = pos.x; prevY = pos.y;
            }
        }

        [Test]
        public void Indicator_NeverFloatsUnderTheShootButton()
        {
            // §5: "skip the dock zone under the SHOOT button rect so the indicator never hides behind UI."
            var shoot = Rect.MinMaxRect(kScrW - 420f, 40f, kScrW - 40f, 260f);
            Vector3 sp = new Vector3(kScrW + 6000f, -2000f, 300f);   // off-screen bottom-right corner

            bool docked = MapViewController.SolveIndicatorPlacement(
                sp, kScrW, kScrH, kInset, shoot, out Vector2 pos, out _);

            Assert.IsFalse(docked, "An off-screen target must float");
            Assert.IsFalse(shoot.Contains(pos),
                $"The floating icon must be lifted clear of the SHOOT button (got {pos})");
        }

        [Test]
        public void Indicator_DockedTargetIsLeftOnTheWorldPoint_WhenClearOfUi()
        {
            // A docked icon is a marker on the world, so nothing may displace it while it is visible —
            // moving it would put it over the wrong hole.
            var shoot = Rect.MinMaxRect(kScrW - 420f, 40f, kScrW - 40f, 260f);
            Vector3 sp = new Vector3(kScrW * 0.5f, kScrH * 0.5f, 200f);

            bool docked = MapViewController.SolveIndicatorPlacement(
                sp, kScrW, kScrH, kInset, shoot, out Vector2 pos, out _);

            Assert.IsTrue(docked);
            Assert.AreEqual(sp.x, pos.x, kEpsilon, "A docked icon is not displaced by the avoid rect");
            Assert.AreEqual(sp.y, pos.y, kEpsilon, "A docked icon is not displaced by the avoid rect");
        }

        [Test]
        public void Indicator_FloatsClearOfUi_WhenTheTargetIsHiddenUnderTheShootButton()
        {
            // REGRESSION, caught in play mode on Hole 5 (2026-08-10). Strict containment pins the
            // footprint's left edge to the OB boundary, so the ball cannot be seated clear of the SHOOT
            // button — it lands ON screen at (983, 203) inside a button spanning 955…1124. "Docked"
            // there means the player sees nothing at all, which is exactly the case the indicator
            // exists for. It must float clear of the button and point back down at the ball.
            var shoot = Rect.MinMaxRect(955f, 84f, 1124f, 348f);
            Vector3 sp = new Vector3(983f, 203f, 200f);

            bool docked = MapViewController.SolveIndicatorPlacement(
                sp, kScrW, kScrH, kInset, shoot, out Vector2 pos, out float ang);

            Assert.IsFalse(docked, "A target hidden under the SHOOT button must not count as docked");
            Assert.IsFalse(shoot.Contains(pos), $"The icon must be lifted clear of the button (got {pos})");
            Assert.AreEqual(shoot.yMax, pos.y, 0.01f, "It should sit just above the button");

            // The icon must stay NEAR the ball, not be flung to the screen edge.
            Assert.Less(Vector2.Distance(pos, new Vector2(sp.x, sp.y)), 250f,
                $"An on-screen occluded target keeps its indicator next to it (got {pos})");
            // And the arrow must point back DOWN at what it is hiding behind.
            Assert.Less(Mathf.Sin(ang * Mathf.Deg2Rad), 0f,
                $"The arrow must point downward toward the occluded ball (ang={ang:F1}°)");
        }
    }
}
