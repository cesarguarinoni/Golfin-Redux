using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// aim_camera_ball_centering — pure-math coverage for the full-swing aim framing solver.
    ///
    /// Both assertions are POSITION-TRACE style (Lesson O): we drive the real solver, apply its
    /// pose to a real <see cref="Camera"/>, and assert on <c>WorldToViewportPoint</c> — i.e. on
    /// Unity's own projection, not on a re-implementation of the solver's trigonometry.
    /// </summary>
    public class AimCameraFramingTests
    {
        const float PortraitAspect = 1170f / 2532f;   // iPhone 14, the shipping capture format

        GameObject _camGO;
        Camera     _cam;

        [SetUp]
        public void SetUp()
        {
            _camGO = new GameObject("AimFramingTestCam");
            _cam   = _camGO.AddComponent<Camera>();
            _cam.orthographic = false;
            _cam.aspect       = PortraitAspect;   // explicit: EditMode has no Game View to derive from
        }

        [TearDown]
        public void TearDown()
        {
            if (_camGO != null) Object.DestroyImmediate(_camGO);
        }

        // ── Solver: ball lands on the 2D widget's viewport point ────────────────────────────

        [Test]
        public void SolveAimCameraPose_ProjectsBallAtTargetViewportPoint(
            [Values(50f, 60f, 70f)] float fov,
            [Values(0.40f, 0.4234f, 0.50f)] float targetVy)
        {
            var ball    = new Vector3(12.5f, 3.25f, -40f);
            var lookDir = new Vector3(0.6f, 0f, 0.8f);   // arbitrary yaw, already unit-length

            PhysicsLabController.SolveAimCameraPose(
                ball, lookDir, distanceM: 3f, heightM: 1.4f,
                verticalFovDeg: fov, targetViewportY: targetVy,
                out Vector3 camPos, out Quaternion camRot);

            _cam.fieldOfView = fov;
            _cam.transform.SetPositionAndRotation(camPos, camRot);

            Vector3 vp = _cam.WorldToViewportPoint(ball);

            Assert.Greater(vp.z, 0f, "Ball must be IN FRONT of the aim camera.");
            Assert.AreEqual(0.5f,     vp.x, 0.01f, $"viewport X (fov={fov}, vy={targetVy})");
            Assert.AreEqual(targetVy, vp.y, 0.01f, $"viewport Y (fov={fov}, vy={targetVy})");
        }

        [Test]
        public void SolveAimCameraPose_PlacesCameraBehindAndAboveTheBall()
        {
            var ball    = Vector3.zero;
            var lookDir = Vector3.forward;

            PhysicsLabController.SolveAimCameraPose(
                ball, lookDir, distanceM: 3f, heightM: 1.4f,
                verticalFovDeg: 60f, targetViewportY: 0.4234f,
                out Vector3 camPos, out Quaternion _);

            Assert.AreEqual(new Vector3(0f, 1.4f, -3f).x, camPos.x, 1e-4f);
            Assert.AreEqual(new Vector3(0f, 1.4f, -3f).y, camPos.y, 1e-4f);
            Assert.AreEqual(new Vector3(0f, 1.4f, -3f).z, camPos.z, 1e-4f);

            // Ball distance ≈ sqrt(3² + 1.4²) = 3.31 m — the whole point of the task
            // (legacy framing sat the ball at ~8.54 m).
            Assert.AreEqual(3.31f, Vector3.Distance(camPos, ball), 0.01f);
        }

        [Test]
        public void SolveAimCameraPose_YawFollowsLookDirection()
        {
            var ball    = new Vector3(5f, 0f, 5f);
            var lookDir = new Vector3(1f, 0f, 0f);   // +X ⇒ yaw 90°

            PhysicsLabController.SolveAimCameraPose(
                ball, lookDir, 3f, 1.4f, 60f, 0.4234f,
                out Vector3 camPos, out Quaternion camRot);

            Assert.AreEqual(90f, camRot.eulerAngles.y, 0.01f, "yaw must follow lookDir");
            Assert.AreEqual(2f,  camPos.x, 1e-4f, "camera sits 3 m back along -X");

            // Viewport contract must hold for this yaw too.
            _cam.fieldOfView = 60f;
            _cam.transform.SetPositionAndRotation(camPos, camRot);
            Vector3 vp = _cam.WorldToViewportPoint(ball);
            Assert.AreEqual(0.5f,    vp.x, 0.01f);
            Assert.AreEqual(0.4234f, vp.y, 0.01f);
        }

        // ── Tee clamp ──────────────────────────────────────────────────────────────────────

        static float ExpectedClamp(float lateral, float fov, float aspect, float safeFrac)
        {
            float tanHalfH = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * aspect;
            return lateral / (tanHalfH * safeFrac);
        }

        [Test]
        public void SolveAimDistance_WideMarkersPullTheCameraBack()
        {
            var ball    = Vector3.zero;
            var lookDir = Vector3.forward;
            // ±2 m lateral, level with the ball along the look direction.
            var markers = new List<Vector3> { new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f) };

            float d = PhysicsLabController.SolveAimDistance(
                ball, lookDir, markers,
                baseDistanceM: 3f, maxDistanceM: 100f,
                verticalFovDeg: 60f, aspect: PortraitAspect, safeFrac: 0.9f);

            float expected = ExpectedClamp(2f, 60f, PortraitAspect, 0.9f);
            Assert.AreEqual(expected, d, 1e-3f, "d = lateral / (tan(fovH/2)·safeFrac)");
            Assert.Greater(d, 3f, "±2 m markers on a portrait screen MUST force a pull-back");
        }

        [Test]
        public void SolveAimDistance_IsCappedAtMaxDistance()
        {
            var markers = new List<Vector3> { new Vector3(-2f, 0f, 0f), new Vector3(2f, 0f, 0f) };

            float d = PhysicsLabController.SolveAimDistance(
                Vector3.zero, Vector3.forward, markers,
                baseDistanceM: 3f, maxDistanceM: 8f,
                verticalFovDeg: 60f, aspect: PortraitAspect, safeFrac: 0.9f);

            Assert.Greater(ExpectedClamp(2f, 60f, PortraitAspect, 0.9f), 8f,
                "fixture sanity: the unclamped requirement must exceed the cap");
            Assert.AreEqual(8f, d, 1e-4f, "clamp must saturate at _aimCamMaxDistanceM");
        }

        [Test]
        public void SolveAimDistance_NarrowMarkersLeaveTheCloseFraming()
        {
            var markers = new List<Vector3> { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f) };

            float d = PhysicsLabController.SolveAimDistance(
                Vector3.zero, Vector3.forward, markers,
                baseDistanceM: 3f, maxDistanceM: 8f,
                verticalFovDeg: 60f, aspect: PortraitAspect, safeFrac: 0.9f);

            Assert.Less(ExpectedClamp(0.5f, 60f, PortraitAspect, 0.9f), 3f,
                "fixture sanity: ±0.5 m must already fit at the base distance");
            Assert.AreEqual(3f, d, 1e-4f, "no pull-back when the markers already fit");
        }

        [Test]
        public void SolveAimDistance_NoMarkersMeansNoClamp()
        {
            float d = PhysicsLabController.SolveAimDistance(
                Vector3.zero, Vector3.forward, new List<Vector3>(),
                baseDistanceM: 3f, maxDistanceM: 8f,
                verticalFovDeg: 60f, aspect: PortraitAspect, safeFrac: 0.9f);

            Assert.AreEqual(3f, d, 1e-4f);

            float dNull = PhysicsLabController.SolveAimDistance(
                Vector3.zero, Vector3.forward, null,
                3f, 8f, 60f, PortraitAspect, 0.9f);

            Assert.AreEqual(3f, dNull, 1e-4f);
        }

        [Test]
        public void SolveAimDistance_MarkersAheadOfTheBallNeedLessPullBack()
        {
            var behind = new List<Vector3> { new Vector3(-2f, 0f, 0f) };
            var ahead  = new List<Vector3> { new Vector3(-2f, 0f, 4f) };   // 4 m down the look dir

            float dLevel = PhysicsLabController.SolveAimDistance(
                Vector3.zero, Vector3.forward, behind, 3f, 100f, 60f, PortraitAspect, 0.9f);
            float dAhead = PhysicsLabController.SolveAimDistance(
                Vector3.zero, Vector3.forward, ahead, 3f, 100f, 60f, PortraitAspect, 0.9f);

            Assert.AreEqual(dLevel - 4f, dAhead, 1e-3f,
                "along-track offset subtracts from the required camera distance 1:1");
        }

        /// <summary>
        /// End-to-end: the clamped distance really does keep the markers inside the safe
        /// horizontal band once the solved pose is applied to a real camera.
        /// </summary>
        [Test]
        public void ClampedPose_KeepsTeeMarkersOnScreen()
        {
            var ball    = Vector3.zero;
            var lookDir = Vector3.forward;
            var markers = new List<Vector3> { new Vector3(-1.5f, 0f, 0.3f), new Vector3(1.5f, 0f, 0.3f) };

            float d = PhysicsLabController.SolveAimDistance(
                ball, lookDir, markers, 3f, 20f, 60f, PortraitAspect, 0.9f);

            PhysicsLabController.SolveAimCameraPose(
                ball, lookDir, d, 1.4f, 60f, 0.4234f,
                out Vector3 camPos, out Quaternion camRot);

            _cam.fieldOfView = 60f;
            _cam.transform.SetPositionAndRotation(camPos, camRot);

            foreach (var m in markers)
            {
                Vector3 vp = _cam.WorldToViewportPoint(m);
                Assert.Greater(vp.z, 0f, $"marker {m} must be in front of the camera");
                Assert.That(vp.x, Is.InRange(0.05f, 0.95f), $"marker {m} viewport X");
            }

            // …and the ball is still pinned to the widget point at the clamped distance.
            Vector3 ballVp = _cam.WorldToViewportPoint(ball);
            Assert.AreEqual(0.5f,    ballVp.x, 0.01f);
            Assert.AreEqual(0.4234f, ballVp.y, 0.01f);
        }
    }
}
