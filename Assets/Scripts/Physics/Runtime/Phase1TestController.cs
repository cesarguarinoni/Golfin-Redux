using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Math.Unity;

namespace Golfin.Physics.Runtime
{
    public class Phase1TestController : MonoBehaviour
    {
        [Header("Shot Input")]
        [Range(5f, 80f)] public float launchSpeed = 50f;      // m/s
        [Range(5f, 80f)] public float launchAngleDeg = 25f;   // degrees above horizontal, along +Z

        [Header("Refs")]
        public Transform ball;
        public LineRenderer trajectoryLine;

        [Header("Playback")]
        [Range(0.1f, 3f)] public float playbackSpeed = 1f;
        public bool autoReplay = true;

        private Trajectory _trajectory;
        private float _playbackTime;

        void Start() => FireShot();

        [ContextMenu("Fire Shot")]
        public void FireShot()
        {
            float angleRad = launchAngleDeg * Mathf.Deg2Rad;
            var input = new ShotInput(
                origin: new fp3(fp.Zero, fp.FromDouble(0.05), fp.Zero),
                velocity: new fp3(
                    fp.Zero,
                    fp.FromDouble(launchSpeed * Mathf.Sin(angleRad)),
                    fp.FromDouble(launchSpeed * Mathf.Cos(angleRad))),
                maxDuration: fp.FromInt(30));

            _trajectory = BallSimulation.Simulate(input, new FlatGround(fp.FromDouble(0.05)));
            _playbackTime = 0;

            Debug.Log($"[Phase1Test] Shot: speed={launchSpeed} m/s, angle={launchAngleDeg}°, " +
                      $"samples={_trajectory.samples.Count}, " +
                      $"range={_trajectory.finalPosition.z.ToFloat():F1} m, " +
                      $"flight time={_trajectory.finalTime.ToFloat():F2} s, " +
                      $"termination={_trajectory.termination}");

            if (trajectoryLine == null) return;
            trajectoryLine.positionCount = _trajectory.samples.Count;
            for (int i = 0; i < _trajectory.samples.Count; i++)
                trajectoryLine.SetPosition(i, _trajectory.samples[i].position.ToVector3());
        }

        void Update()
        {
            if (_trajectory == null || _trajectory.samples.Count == 0) return;

            _playbackTime += Time.deltaTime * playbackSpeed;
            float totalTime = _trajectory.finalTime.ToFloat();

            if (_playbackTime >= totalTime)
            {
                if (autoReplay) { _playbackTime = 0; }
                else { _playbackTime = totalTime; }
            }

            var samples = _trajectory.samples;
            int i1 = samples.Count - 1;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].time.ToFloat() >= _playbackTime) { i1 = i; break; }
            }
            int i0 = System.Math.Max(0, i1 - 1);
            float t0 = samples[i0].time.ToFloat();
            float t1 = samples[i1].time.ToFloat();
            float frac = t1 > t0 ? (_playbackTime - t0) / (t1 - t0) : 0;
            Vector3 p0 = samples[i0].position.ToVector3();
            Vector3 p1 = samples[i1].position.ToVector3();

            if (ball != null)
                ball.position = Vector3.Lerp(p0, p1, frac);
        }

        void OnValidate()
        {
            if (Application.isPlaying && ball != null && trajectoryLine != null)
                FireShot();
        }
    }
}
