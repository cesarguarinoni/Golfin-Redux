using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.Input;

// SPEC DEVIATION (file path): spec places this in Assets/Scripts/UI/HUD/ (Assembly-CSharp).
// However Golfin.Physics.Math is autoReferenced:false, making fp type inaccessible from
// Assembly-CSharp. The predictor requires fp for BallSimulation.Simulate() and ShotInputBuilder.Build().
// Solution: place in Assets/Scripts/Physics/Viewer/ (Golfin.Physics.Viewer asmdef), which
// already references Golfin.Physics.Math, Golfin.Physics.Core, Golfin.Physics.Stats,
// and Golfin.Gameplay.Input. All functional behaviour is identical to the spec.
// When BagManager/BallManager inventory singletons are needed, a future migration can
// add an Assembly-CSharp reference to Golfin.Physics.Viewer (or use a context bus).

namespace Golfin.Physics.Viewer
{
    public class PuttPathPredictor : MonoBehaviour
    {
        [SerializeField] private ShotController   _shotController;
        [SerializeField] private Camera           _worldCamera;
        [SerializeField] private Transform        _ballTransform;
        [SerializeField] private PuttPathRenderer _renderer;
        [SerializeField] private RectTransform    _canvasRect;

        [Header("Re-prediction thresholds")]
        [SerializeField] private float _aimDeltaThresholdDeg = 0.3f;
        [SerializeField] private float _powerDeltaThreshold  = 0.01f;
        [SerializeField] private float _sampleIntervalSec    = 0.05f;

        // Constant matching ControlsConfig.Default.PuttBaseVelocityMps.
        private const float PuttBaseVelocityMps = 5f;

        // Providers — pulled from PhysicsLabController on enable.
        private IGroundProvider  _ground;
        private ISurfaceProvider _surfaces;

        // Cached state for delta detection.
        private float _lastAimYaw;
        private float _lastPower;
        private bool  _hasCache;

        // Driven by PhysicsLabController after ShotDebugFlags.PuttPathHeatmap changes.
        public bool HeatmapMode
        {
            get => _renderer != null && _renderer.HeatmapMode;
            set { if (_renderer != null) _renderer.HeatmapMode = value; }
        }

        private void OnEnable()
        {
            _hasCache = false;
            if (_shotController != null)
                _shotController.OnStateChanged += OnState;

            var lab = Object.FindObjectOfType<PhysicsLabController>();
            if (lab != null)
            {
                _ground   = lab.GetGround();
                _surfaces = lab.GetSurfaces();
            }
        }

        private void OnDisable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged -= OnState;
            if (_renderer != null) _renderer.SetPath(null, null);
            _hasCache = false;
        }

        // Wires the correct providers after a hole load/unload.
        public void RefreshProviders(IGroundProvider ground, ISurfaceProvider surfaces)
        {
            _ground   = ground;
            _surfaces = surfaces;
        }

        public void SetBallTransform(Transform ball)   => _ballTransform = ball;
        public void SetCamera(Camera cam)              => _worldCamera   = cam;

        private void OnState(ShotInputState state)
        {
            if (state.State == ShotState.Idle || state.State == ShotState.Resolving)
            {
                if (_renderer != null) _renderer.SetPath(null, null);
                _hasCache = false;
                return;
            }

            if (_hasCache)
            {
                float aimDelta   = Mathf.Abs(Mathf.DeltaAngle(_lastAimYaw * Mathf.Rad2Deg, state.AimYawRadians * Mathf.Rad2Deg));
                float powerDelta = Mathf.Abs(state.PowerNormalized - _lastPower);
                if (aimDelta < _aimDeltaThresholdDeg && powerDelta < _powerDeltaThreshold)
                    return;
            }

            // Sync heatmap flag from ShotController debug flags.
            if (_renderer != null && _shotController != null)
                _renderer.HeatmapMode = _shotController.DebugFlags.PuttPathHeatmap;

            Predict(state.PowerNormalized, state.AimYawRadians);
            _lastAimYaw = state.AimYawRadians;
            _lastPower  = state.PowerNormalized;
            _hasCache   = true;
        }

        private static StatBundle BuildPuttBundle()
        {
            return new StatBundle(
                PutterStats.DefaultPutter,
                BallStats.Neutral,
                CharacterStats.Neutral,
                fp.FromFloat(100f),
                fp.FromFloat(100f));
        }

        private void Predict(float powerNormalized, float aimYawRadians)
        {
            if (_renderer == null) return;

            var bundle = BuildPuttBundle();

            Vector3 origin = _ballTransform != null ? _ballTransform.position : Vector3.zero;

            var (input, ballMods) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default, StatCaps.Default,
                fp.FromFloat(Mathf.Clamp01(powerNormalized)),
                fp.FromFloat(aimYawRadians),
                fp.FromFloat(origin.x), fp.FromFloat(origin.y), fp.FromFloat(origin.z),
                seed: 42u,
                baseVelocityOverrideMps: fp.FromFloat(PuttBaseVelocityMps));

            var ground   = _ground   ?? (IGroundProvider)new FlatGround(fp.Zero);
            var surfaces = _surfaces ?? (ISurfaceProvider)new ConstantSurfaceProvider(SurfaceType.Green);

            var traj = BallSimulation.Simulate(
                input, ground,
                AeroConfig.Default, WindConfig.Calm,
                surfaces, SurfaceConfig.Default,
                PuttConfig.Default, ballMods);

            var pts    = new List<Vector2>();
            var speeds = new List<float>();
            float nextSampleT = 0f;

            if (traj.samples != null)
            {
                foreach (var s in traj.samples)
                {
                    float t = s.time.ToFloat();
                    if (t < nextSampleT) continue;
                    nextSampleT += _sampleIntervalSec;

                    Vector3 worldPos = new Vector3(s.position.x.ToFloat(),
                                                   s.position.y.ToFloat(),
                                                   s.position.z.ToFloat());

                    if (_worldCamera == null) continue;
                    Vector3 screen = _worldCamera.WorldToScreenPoint(worldPos);
                    if (screen.z < 0f) continue;

                    Vector2 canvas;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out canvas);
                    pts.Add(canvas);

                    Vector3 vel = new Vector3(s.velocity.x.ToFloat(),
                                              s.velocity.y.ToFloat(),
                                              s.velocity.z.ToFloat());
                    speeds.Add(vel.magnitude);
                }
            }

            float topSpeed = bundle.IsPutt && bundle.Putter.HasValue
                ? bundle.Putter.Value.BaseVelocityMps.ToFloat()
                : PuttBaseVelocityMps;
            _renderer.TopSpeedMps = topSpeed;

            if (pts.Count >= 2 && powerNormalized > 0.001f)
                _renderer.SetPath(pts, speeds);
            else
                _renderer.SetPath(null, null);
        }
    }
}
