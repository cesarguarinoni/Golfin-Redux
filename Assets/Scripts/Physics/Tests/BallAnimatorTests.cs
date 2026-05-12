using NUnit.Framework;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    public class BallAnimatorTests
    {
        GameObject _go;
        BallAnimator _animator;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("BallAnimator_TestHost");
            _animator = _go.AddComponent<BallAnimator>();
            _animator.SpawnAtForTests(Vector3.zero);
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Update_AppliesRotation_WhenBallTranslatesHorizontally()
        {
            // Arrange: ball spawned at origin, identity rotation.
            var t = _animator.InstanceForTests;
            Assert.IsNotNull(t, "Ball instance should be spawned");
            Assert.AreEqual(Quaternion.identity, t.rotation, "Spawn should reset to identity rotation");

            // Act: translate 1m along +Z, drive one Update frame.
            t.position = new Vector3(0f, 0f, 1f);
            _animator.DriveUpdateForTests();

            // Assert: rotation should be ~2664° about +X axis
            // (1m / 0.0215m radius = ~46.51 rad ≈ 2664.5°), wrapped into a quaternion.
            // We test the angle off identity, not the exact value (quaternion wrap-around makes raw eulers misleading).
            float angle;
            Vector3 axis;
            t.rotation.ToAngleAxis(out angle, out axis);
            Assert.Greater(Mathf.Abs(angle), 1e-4f, "Rotation must be non-zero after 1m translation");
            // Expected axis is Cross((0,0,1), (0,1,0)) = (-1, 0, 0). Quaternion ToAngleAxis returns a positive angle
            // and may flip the axis sign accordingly; accept either (-1,0,0) at +angle or (+1,0,0) at -angle equivalent.
            Assert.AreEqual(1f, Mathf.Abs(axis.x), 1e-3f, "Rotation axis should be ±X");
            Assert.AreEqual(0f, axis.y, 1e-3f, "Rotation axis Y should be zero");
            Assert.AreEqual(0f, axis.z, 1e-3f, "Rotation axis Z should be zero");
        }

        [Test]
        public void Update_DoesNotRotate_WhenBallStationary()
        {
            // Arrange: ball at origin, identity rotation.
            var t = _animator.InstanceForTests;
            var initialRotation = t.rotation;

            // Act: drive 60 frames of Update with NO position change.
            for (int i = 0; i < 60; i++) _animator.DriveUpdateForTests();

            // Assert: rotation unchanged.
            Assert.AreEqual(initialRotation, t.rotation, "Stationary ball should not accumulate rotation");
        }
    }
}
