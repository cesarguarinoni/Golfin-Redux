using System;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Defaults;

namespace Golfin.Gameplay.Input
{
    public class ShotController : MonoBehaviour
    {
        // --- Injectable seams ---
        [SerializeField] private InputSystemSource _inputSystemSource;
        private IShotInputSource _inputSource;
        private ControlsConfig   _config = ControlsConfig.Default;
        private StatBundle       _statBundle;
        private bool             _statBundleOverridden;

        // --- Public config ---
        public bool  IsPutt                { get; set; }
        public float CameraHeadingRadians  { get; set; }

        // --- Debug toggles ---
        public bool DisableOverpower       = false;  // clamp at 100%
        public bool ForcePerfectTiming     = false;  // flick always succeeds
        public bool ForcePerfectAim        = false;  // degradation yaw zeroed
        public bool SinglePassMode         = false;  // skip degradation
        public bool DisableConeFinetuning  = false;  // aim = camera only

        // --- Readable state ---
        public ShotState State            { get; private set; } = ShotState.Idle;
        public float     PowerNormalized  { get; private set; }
        public float     ConeHalfAngleDeg => HalfConeAngleRad() * Mathf.Rad2Deg;

        // --- Internal state ---
        private float _pullDistancePx;
        private float _arrowProgress;
        private int   _passIndex;
        private float _degradationYawRad;
        private float _aimYawRadians;
        private float _coneFinetune;
        private bool  _wasTouching;

        // --- Events ---
        public event Action<ShotInputState>                     OnStateChanged;
        public event Action<ShotInput, BallPhysicsModifiers>    OnShotResolved;

        // --- Test injection API ---
        public void InjectInputSource(IShotInputSource source)  => _inputSource = source;
        public void InjectConfig(ControlsConfig cfg)            => _config = cfg;
        public void InjectStatBundle(StatBundle bundle)         { _statBundle = bundle; _statBundleOverridden = true; }
        public void ClearStatBundleOverride()                   => _statBundleOverridden = false;

        // Call when the ball comes to rest (or explicitly from a test)
        public void CompleteShot() => TransitionToIdle();

        // ── External drag API (ClubHandle → drives shot without pixel-pull math) ──

        public bool IsExternalDragActive => _externalDragActive;
        private bool _externalDragActive;

        public void BeginExternalDrag()
        {
            if (State != ShotState.Idle) return;
            _externalDragActive = true;
            TransitionToAiming();
            PublishState();
        }

        public void SetExternalPower(float powerNormalized, float coneFinetune)
        {
            if (!_externalDragActive) return;
            PowerNormalized = Mathf.Clamp01(powerNormalized);
            _coneFinetune   = Mathf.Clamp(coneFinetune, -1f, 1f);
            if (State == ShotState.Aiming && powerNormalized > 0f)
                TransitionToTiming();
            PublishState();
        }

        public void EndExternalDrag()
        {
            if (!_externalDragActive) return;
            _externalDragActive = false;
            if (State == ShotState.Timing && PowerNormalized > 0f)
                CommitFlick();
            else
                TransitionToIdle();
        }

        public void CancelExternalDrag()
        {
            if (!_externalDragActive) return;
            _externalDragActive = false;
            TransitionToIdle();
        }

        private void Awake()
        {
            if (_inputSource == null && _inputSystemSource != null)
                _inputSource = _inputSystemSource;
        }

        private void Update() => Tick(Time.deltaTime);

        public void Tick(float dt)
        {
            // External drag path: arrow still ticks even with no input source.
            if (_externalDragActive)
            {
                if (State == ShotState.Timing) TickArrow(dt);
                PublishState();
                return;
            }

            if (_inputSource == null) return;

            bool touching    = _inputSource.IsTouching;
            bool justLifted  = _wasTouching && !touching;
            bool justTouched = !_wasTouching && touching;
            _wasTouching = touching;

            switch (State)
            {
                case ShotState.Idle:
                    if (!_externalDragActive && justTouched) TransitionToAiming();
                    break;

                case ShotState.Aiming:
                    if (_externalDragActive) break;
                    if (justLifted) { TransitionToIdle(); break; }
                    if (touching)
                    {
                        if (ComputePullPx() > _config.PullStartThresholdPx)
                            TransitionToPulling();
                    }
                    break;

                case ShotState.Pulling:
                    if (_externalDragActive) break;
                    if (justLifted) { TransitionToIdle(); break; }
                    if (touching)
                    {
                        _pullDistancePx = ComputePullPx();
                        PowerNormalized = ComputePower(_pullDistancePx);
                        _coneFinetune   = ComputeFinetune();
                        if (PowerNormalized > 0f) TransitionToTiming();
                    }
                    break;

                case ShotState.Timing:
                    if (_externalDragActive) { TickArrow(dt); break; }
                    if (justLifted)
                    {
                        bool validFlick = ForcePerfectTiming ||
                            _inputSource.TouchVelocityPxPerSec.y >= _config.FlickVelocityThresholdPxPerSec;
                        if (validFlick) CommitFlick();
                        else            TransitionToIdle();
                        break;
                    }
                    if (touching)
                    {
                        _pullDistancePx = ComputePullPx();
                        PowerNormalized = ComputePower(_pullDistancePx);
                        _coneFinetune   = ComputeFinetune();
                        TickArrow(dt);
                    }
                    break;

                // Flicking is transient — fully handled inside CommitFlick()
                case ShotState.Flicking:
                    break;

                case ShotState.Resolving:
                    break;
            }

            PublishState();
        }

        // ─────────────────────────────── Transitions ────────────────────────

        private void TransitionToIdle()
        {
            State              = ShotState.Idle;
            PowerNormalized    = 0f;
            _pullDistancePx    = 0f;
            _arrowProgress     = 0f;
            _passIndex         = 0;
            _degradationYawRad = 0f;
            _coneFinetune      = 0f;
            _aimYawRadians     = 0f;
        }

        private void TransitionToAiming()  => State = ShotState.Aiming;
        private void TransitionToPulling() => State = ShotState.Pulling;
        private void TransitionToTiming()  => State = ShotState.Timing;

        private void CommitFlick()
        {
            State = ShotState.Flicking;

            float degradYaw = ForcePerfectAim ? 0f : _degradationYawRad;
            float finetune  = DisableConeFinetuning ? 0f : _coneFinetune;

            _aimYawRadians = CameraHeadingRadians + finetune * HalfConeAngleRad() + degradYaw;

            float flickMag = PowerNormalized;
            if (IsPutt || DisableOverpower) flickMag = Mathf.Min(flickMag, 1f);

            var bundle = GetStatBundle();
            var (input, ballMods) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default,
                StatCaps.Default,
                fp.FromFloat(flickMag),
                fp.FromFloat(_aimYawRadians),
                fp.Zero, fp.Zero, fp.Zero,
                (uint)UnityEngine.Random.Range(1, int.MaxValue));

            State = ShotState.Resolving;
            OnShotResolved?.Invoke(input, ballMods);
        }

        // ─────────────────────────────── Arrow / timing ─────────────────────

        private void TickArrow(float dt)
        {
            var bundle = GetStatBundle();
            float cc = bundle.Character.ClubControl;
            float arrowHz = _config.BaseArrowSpeedHzAtCC0 + cc * _config.ArrowSpeedHzPerCC;
            if (IsPutt) arrowHz *= _config.PuttArrowSpeedMultiplier;

            _arrowProgress += arrowHz * dt;
            if (_arrowProgress < 1f) return;

            _arrowProgress -= 1f;
            _passIndex++;

            if (!SinglePassMode)
            {
                int cleanPasses = Mathf.RoundToInt(_config.MaxCleanPassesAtCC0 + cc * _config.CleanPassesPerCC);
                if (_passIndex >= cleanPasses)
                    _degradationYawRad += _config.DegradationYawDegPerPass * Mathf.Deg2Rad;
            }

            if (_passIndex >= Mathf.RoundToInt(_config.MaxTotalPasses))
                TransitionToIdle();
        }

        // ─────────────────────────────── Helpers ────────────────────────────

        private float ComputePullPx()
        {
            return Mathf.Max(0f, _inputSource.TouchOriginPx.y - _inputSource.TouchPositionPx.y);
        }

        private float ComputePower(float pullPx)
        {
            if (pullPx < _config.MinUsefulPullPx) return 0f;

            if (pullPx <= _config.Max100PercentPullPx)
                return (pullPx - _config.MinUsefulPullPx) /
                       (_config.Max100PercentPullPx - _config.MinUsefulPullPx);

            if (IsPutt || DisableOverpower) return 1f;

            float range = _config.MaxOverpowerPullPx - _config.Max100PercentPullPx;
            return Mathf.Min(1f + ((pullPx - _config.Max100PercentPullPx) / range) * 0.2f, 1.2f);
        }

        private float ComputeFinetune()
        {
            float dx = _inputSource.TouchPositionPx.x - _inputSource.TouchOriginPx.x;
            return Mathf.Clamp(dx / 150f, -1f, 1f);  // 150px = approx half-cone width; refined in Part D
        }

        private float HalfConeAngleRad()
        {
            float accNorm = GetClubAccuracyNorm();
            float halfDeg = Mathf.Lerp(_config.ConeHalfAngleAtAcc0Deg,
                                        _config.ConeHalfAngleAtAcc100Deg, accNorm);
            return halfDeg * Mathf.Deg2Rad;
        }

        private float GetClubAccuracyNorm()
        {
            var b = GetStatBundle();
            if (!b.IsPutt && b.Club.HasValue)    return b.Club.Value.Accuracy    / 120f;
            if (b.IsPutt  && b.Putter.HasValue)  return b.Putter.Value.Accuracy  / 120f;
            return 0.5f;
        }

        private StatBundle GetStatBundle()
        {
            if (_statBundleOverridden) return _statBundle;
            return IsPutt
                ? DefaultStatProvider.BuildPuttBundle()
                : DefaultStatProvider.BuildSwingBundle();
        }

        private void PublishState()
        {
            if (OnStateChanged == null) return;
            float cc = GetStatBundle().Character.ClubControl;
            int cleanPasses = Mathf.RoundToInt(_config.MaxCleanPassesAtCC0 + cc * _config.CleanPassesPerCC);
            OnStateChanged.Invoke(new ShotInputState(
                State, PowerNormalized, _coneFinetune, _arrowProgress,
                _passIndex, _passIndex >= cleanPasses,
                IsPutt, _aimYawRadians, CameraHeadingRadians));
        }
    }
}
