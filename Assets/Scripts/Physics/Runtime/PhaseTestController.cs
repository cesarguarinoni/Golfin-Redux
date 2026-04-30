using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Math.Unity;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Phase 2 test driver. Fires aero and vacuum trajectories simultaneously so the effect of
    /// drag + Magnus lift is visually obvious: cyan reaches further (vacuum), yellow falls short (aero).
    /// Replaces Phase1TestController for interactive testing; Phase 1 scene is preserved as baseline.
    /// </summary>
    public class PhaseTestController : MonoBehaviour
    {
        [Header("Shot Mode")]
        public bool UseAero = true;
        [Tooltip("Club ID from clubs.csv — used when UseAero is true")]
        public string ClubId = "club_iron7_mireo";

        [Header("Manual Override (when UseAero = false)")]
        [Range(5f, 80f)] public float ManualSpeedMps = 52.5f;
        [Range(1f, 60f)] public float ManualAngleDeg = 16.3f;

        [Header("LineRenderers")]
        [Tooltip("Yellow — trajectory with drag + Magnus lift")]
        public LineRenderer aeroLine;
        [Tooltip("Cyan — vacuum trajectory (gravity only)")]
        public LineRenderer vacuumLine;
        public bool showAeroLine = true;
        public bool showVacuumLine = true;

        [Header("Ball Playback")]
        public Transform ball;
        [Range(0.1f, 3f)] public float playbackSpeed = 1f;
        public bool autoReplay = true;

        private Trajectory _aeroTrajectory;
        private Trajectory _vacuumTrajectory;
        private float _playbackTime;

        void Start() => FireShot();

        [ContextMenu("Fire Shot")]
        public void FireShot()
        {
            var aeroConfig = PhysicsConfigLoader.LoadAeroConfig();

            fp speed, angleDeg;
            SpinState spin = SpinState.None;
            float expectedCarryYd = 0f;

            if (UseAero)
            {
                var clubs = PhysicsConfigLoader.LoadClubSpecs();
                ClubSpec club = default;
                bool found = false;
                foreach (var c in clubs)
                {
                    if (c.Id == ClubId) { club = c; found = true; break; }
                }

                if (!found)
                {
                    Debug.LogWarning($"[PhaseTest] Club '{ClubId}' not found in clubs.csv — using Iron7 defaults");
                    speed    = fp.FromFloat(52.5f);
                    angleDeg = fp.FromFloat(16.3f);
                    float rpsDefault = 7097f * 2f * Mathf.PI / 60f;
                    spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rpsDefault));
                }
                else
                {
                    speed    = club.BallSpeedMps;
                    angleDeg = club.LaunchAngleDeg;
                    expectedCarryYd = club.ExpectedCarryYd.ToFloat();
                    float rps = club.SpinRateRpm.ToFloat() * 2f * Mathf.PI / 60f;
                    // Backspin axis: -X when ball travels +Z (produces upward Magnus lift)
                    spin = new SpinState(new fp3(fp.FromInt(-1), fp.Zero, fp.Zero), fp.FromFloat(rps));
                }
            }
            else
            {
                speed    = fp.FromFloat(ManualSpeedMps);
                angleDeg = fp.FromFloat(ManualAngleDeg);
            }

            float angleRad = angleDeg.ToFloat() * Mathf.Deg2Rad;
            var origin    = new fp3(fp.Zero, fp.FromDouble(0.05), fp.Zero);
            var velocity  = new fp3(
                fp.Zero,
                fp.FromDouble(speed.ToFloat() * Mathf.Sin(angleRad)),
                fp.FromDouble(speed.ToFloat() * Mathf.Cos(angleRad)));
            var maxDur    = fp.FromInt(30);
            var groundY   = new FlatGround(fp.FromDouble(0.05));

            // Aero shot (with drag + backspin lift)
            var aeroInput   = new ShotInput(origin, velocity, maxDur, spin);
            _aeroTrajectory = BallSimulation.Simulate(aeroInput, groundY, aeroConfig);

            // Vacuum shot (gravity only — same launch, no aero, no spin)
            var vacInput     = new ShotInput(origin, velocity, maxDur);
            _vacuumTrajectory = BallSimulation.Simulate(vacInput, groundY);

            _playbackTime = 0;

            float aeroM   = _aeroTrajectory.finalPosition.z.ToFloat();
            float aeroYd  = aeroM * 1.09361f;
            float vacuumM = _vacuumTrajectory.finalPosition.z.ToFloat();
            float vacuumYd = vacuumM * 1.09361f;

            string clubInfo = UseAero ? $"club={ClubId}" : $"manual {speed}m/s {angleDeg}°";
            string expected  = expectedCarryYd > 0f ? $" expected={expectedCarryYd:F0}yd ({expectedCarryYd * 0.9144f:F1}m)" : "";
            Debug.Log($"[PhaseTest] {clubInfo} | aero carry={aeroM:F1}m ({aeroYd:F0}yd) | vacuum carry={vacuumM:F1}m ({vacuumYd:F0}yd){expected}");

            ApplyLine(aeroLine,   _aeroTrajectory,   showAeroLine);
            ApplyLine(vacuumLine, _vacuumTrajectory, showVacuumLine);
        }

        private static void ApplyLine(LineRenderer lr, Trajectory traj, bool visible)
        {
            if (lr == null) return;
            lr.enabled = visible;
            if (!visible) return;
            lr.positionCount = traj.samples.Count;
            for (int i = 0; i < traj.samples.Count; i++)
                lr.SetPosition(i, traj.samples[i].position.ToVector3());
        }

        void Update()
        {
            if (ball == null) return;
            var traj = _aeroTrajectory ?? _vacuumTrajectory;
            if (traj == null || traj.samples.Count == 0) return;

            _playbackTime += Time.deltaTime * playbackSpeed;
            float totalTime = traj.finalTime.ToFloat();
            if (_playbackTime >= totalTime)
            {
                if (autoReplay) _playbackTime = 0;
                else _playbackTime = totalTime;
            }

            var s = traj.samples;
            int i1 = s.Count - 1;
            for (int i = 1; i < s.Count; i++)
            {
                if (s[i].time.ToFloat() >= _playbackTime) { i1 = i; break; }
            }
            int i0 = System.Math.Max(0, i1 - 1);
            float t0 = s[i0].time.ToFloat(), t1 = s[i1].time.ToFloat();
            float frac = t1 > t0 ? (_playbackTime - t0) / (t1 - t0) : 0;
            ball.position = Vector3.Lerp(s[i0].position.ToVector3(), s[i1].position.ToVector3(), frac);
        }

        void OnValidate()
        {
            if (Application.isPlaying) FireShot();
        }
    }
}
