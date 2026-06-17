using System;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Stats;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Defaults;
using Golfin.Audio.Events;

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
        public bool    IsPutt                { get; set; }
        public float   CameraHeadingRadians  { get; set; }

        /// <summary>
        /// Spin input for the next shot (draw/fade = x, backspin/topspin = y).
        /// Set by the HUD layer (which has access to Golfin.Gameplay.UI) before CommitFlick.
        /// Golfin.Gameplay.Input cannot reference Golfin.Gameplay.UI (would be circular),
        /// so the caller pushes the value here instead of ShotController reading SpinContext.
        /// Reset to zero after each shot in TransitionToIdle().
        /// </summary>
        public Vector2 PendingSpinInput      { get; set; }

        // ── Fade/Draw mode state (D1–D5, fade_draw_core_wiring Order 356) ───────
        // Cannot read ShotModeContext directly (circular asmdef: Input does not ref UI).
        // UI layer (ShotConeView) pushes these values when mode changes.

        /// <summary>
        /// True when the Fade/Draw toggle is armed (ShotMode.FadeDraw).
        /// Pushed by ShotConeView.OnShotModeChanged. Read at CommitFlick.
        /// </summary>
        public bool FadeDrawActive { get; set; }

        /// <summary>
        /// Locked aim yaw (radians) captured when arming the Fade/Draw toggle (D5).
        /// Set by ShotConeView when mode transitions Straight→FadeDraw.
        /// Read at CommitFlick instead of the live CameraHeadingRadians when FadeDrawActive.
        /// NaN = not locked (use camera heading).
        /// </summary>
        public float FadeDrawLockedAimRad { get; set; } = float.NaN;

        // --- Debug toggles (8 flags per design §8) ---
        public ShotDebugFlags DebugFlags = ShotDebugFlags.Defaults;

        /// <summary>When true, emits a one-line snapshot at CommitFlick entry naming the bundle, override, and gate inputs.</summary>
        public bool LogResolution;

        // --- Readable state ---
        public ShotState State            { get; private set; } = ShotState.Idle;
        public float     PowerNormalized  { get; private set; }
        public float     ConeHalfAngleDeg => HalfConeAngleRad() * Mathf.Rad2Deg;

        /// <summary>True if the most recent committed flick was a full-swing shot with zero aim
        /// degradation (committed inside the clean-pass window). Putts are never "clean" for trail
        /// purposes (see spec NOTE P). Latched in CommitFlick; read by BallTrailController on Flying.</summary>
        public bool LastShotWasClean { get; private set; }

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

        /// <summary>
        /// Called by ShotConeView when the Fade/Draw toggle is armed (D5).
        /// Re-centers the cone finetune to 0 so subsequent handle movement
        /// drives the fade/draw curve from center, not carry over the old aim offset.
        /// </summary>
        public void ForceRecenterFinetune() => _coneFinetune = 0f;

#if UNITY_EDITOR
        /// <summary>
        /// Invoke PublishShotSfx directly for EditMode bus-wiring seam tests (Order 350).
        /// Sets IsPutt and PowerNormalized first so the SfxId selection logic is exercised.
        /// </summary>
        public void PublishShotSfxForTest(bool isPutt, float powerNormalized)
        {
            IsPutt          = isPutt;
            PowerNormalized = powerNormalized;
            PublishShotSfx();
        }
#endif

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

        // Fires a shot directly without gesture input. Maps accuracy preset to degradation yaw.
        // power range: 0–1.2 (same as PowerNormalized; 1.0 = 100%).
        public void FireDebugShot(float power, DebugShotAccuracy accuracy)
        {
            TransitionToIdle();
            PowerNormalized    = Mathf.Clamp(power, 0f, 1.2f);
            _coneFinetune      = 0f;
            _externalDragActive = false;
            _degradationYawRad = accuracy switch
            {
                DebugShotAccuracy.Green  => 0f,
                DebugShotAccuracy.Yellow => _config.DegradationYawDegPerPass * Mathf.Deg2Rad,
                DebugShotAccuracy.Red    => _config.DegradationYawDegPerPass * 4f * Mathf.Deg2Rad,
                _                        => 0f,
            };
            CommitFlick();
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

            if (_inputSource == null)
            {
                PublishState();
                return;
            }

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
                        // CancelOnSlowFlick=false skips the velocity threshold check.
                        bool validFlick = DebugFlags.ForcePerfectTiming
                            || !DebugFlags.CancelOnSlowFlick
                            || _inputSource.TouchVelocityPxPerSec.y >= _config.FlickVelocityThresholdPxPerSec;
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
            PendingSpinInput   = Vector2.zero;  // reset after each shot (spin is per-shot, not sticky)
            // Fade/Draw mode state resets after each shot (FadeDrawActive persists between shots
            // — the toggle is sticky — but the locked aim resets so next arm captures fresh aim).
            FadeDrawLockedAimRad = float.NaN;
        }

        private void TransitionToAiming()  => State = ShotState.Aiming;
        private void TransitionToPulling() => State = ShotState.Pulling;
        private void TransitionToTiming()  => State = ShotState.Timing;

        private void CommitFlick()
        {
            State = ShotState.Flicking;

            // ── Order 350: Swing + Hit SFX ────────────────────────────────────────
            // Published at the moment the player commits the shot. Read-only: does not
            // touch BallSimulation, BallStateMachine, or any fixed-point state.
            PublishShotSfx();

            float degradYaw = DebugFlags.ForcePerfectAim ? 0f : _degradationYawRad;
            LastShotWasClean = !IsPutt && Mathf.Approximately(degradYaw, 0f);   // latched for BallTrailController
            float finetune  = DebugFlags.DisableConeFineTune ? 0f : _coneFinetune;

            // Phase D/E (fade_draw_core_wiring Order 356):
            // FadeDraw mode: aim is LOCKED at arming time (pushed by ShotConeView as FadeDrawLockedAimRad).
            //   If lock was cleared (NaN, e.g. after a shot reset), re-lock to camera heading now.
            //   finetune drives the fade/draw curve, NOT aim.
            // Straight mode: finetune drives aim nudge within ±AimNudgeRangeRad (D4).
            //   The legacy formula (finetune * HalfConeAngleRad) is REPLACED by aim nudge.
            // Putts: unchanged (D6) — legacy formula retained.
            if (!IsPutt && FadeDrawActive)
            {
                // FadeDraw armed: use locked aim. Re-lock if NaN (per-shot re-arm after reset).
                float lockedAim = float.IsNaN(FadeDrawLockedAimRad) ? CameraHeadingRadians : FadeDrawLockedAimRad;
                _aimYawRadians = lockedAim + degradYaw;
            }
            else if (!IsPutt && !FadeDrawActive)
            {
                // Straight mode: aim nudge = finetune * AimNudgeRangeRad (D4).
                _aimYawRadians = CameraHeadingRadians + finetune * _config.AimNudgeRangeRad + degradYaw;
            }
            else
            {
                // Putt: unchanged (D6) — legacy formula retained.
                _aimYawRadians = CameraHeadingRadians + finetune * HalfConeAngleRad() + degradYaw;
            }

            float flickMag = PowerNormalized;
            if (IsPutt || DebugFlags.DisableOverpower) flickMag = Mathf.Min(flickMag, 1f);

            // Putt mode: pass PuttBaseVelocityMps as explicit override so ControlsConfig
            // drives the velocity, not whatever is in the StatBundle.
            fp baseVelOverride = IsPutt ? fp.FromFloat(_config.PuttBaseVelocityMps) : fp.Zero;

            var bundle = GetStatBundle();
#if UNITY_EDITOR
            if (LogResolution)
            {
                string clubVel    = bundle.Club.HasValue   ? bundle.Club.Value.BaseVelocityMps.ToFloat().ToString("F2")   : "n/a";
                string putterVel  = bundle.Putter.HasValue ? bundle.Putter.Value.BaseVelocityMps.ToFloat().ToString("F2") : "n/a";
                UnityEngine.Debug.Log(
                    $"[CommitFlick] IsPutt={IsPutt} bundle.IsPutt={bundle.IsPutt} " +
                    $"bundle.Club.HasValue={bundle.Club.HasValue} clubVel={clubVel}m/s " +
                    $"bundle.Putter.HasValue={bundle.Putter.HasValue} putterVel={putterVel}m/s " +
                    $"PowerNormalized={PowerNormalized:F3} flickMag={flickMag:F3} " +
                    $"PuttBaseVelocityMps={_config.PuttBaseVelocityMps:F2} " +
                    $"baseVelOverride={baseVelOverride.ToFloat():F2}m/s " +
                    $"aimYawRadians={_aimYawRadians:F3}rad");
            }
#endif
            // Spin input: read PendingSpinInput (set by HUD layer before CommitFlick).
            // Putts always use zero spin (design lock §Out of scope).
            Vector2 spinInput = IsPutt ? Vector2.zero : PendingSpinInput;
            fp spinInputX   = fp.FromFloat(spinInput.x);
            fp spinInputY   = fp.FromFloat(spinInput.y);
            fp spinMagSlope = fp.FromFloat(_config.SpinMagScaleSlope);
            fp spinTiltRad  = fp.FromFloat(_config.SpinMaxTiltRad);

            // Phase B (fade_draw_core_wiring Order 356):
            // FadeDraw armed + not putt: finetune (re-centered after arm) drives fade/draw curve.
            // Straight mode or putt: fadeDrawInput = 0 (no curve).
            fp fadeDrawInputFp    = fp.Zero;
            fp fadeDrawMaxTiltFp  = fp.Zero;
            if (!IsPutt && FadeDrawActive)
            {
                fadeDrawInputFp   = fp.FromFloat(finetune);
                fadeDrawMaxTiltFp = fp.FromFloat(_config.FadeDrawMaxTiltRad);
            }

            var (input, ballMods) = ShotInputBuilder.Build(
                bundle,
                StatCoefficients.Default,
                StatCaps.Default,
                fp.FromFloat(flickMag),
                fp.FromFloat(_aimYawRadians),
                fp.Zero, fp.Zero, fp.Zero,
                (uint)UnityEngine.Random.Range(1, int.MaxValue),
                baseVelOverride,
                spinInputX,
                spinInputY,
                spinMagSlope,
                spinTiltRad,
                fadeDrawInputFp,
                fadeDrawMaxTiltFp);

            State = ShotState.Resolving;
            OnShotResolved?.Invoke(input, ballMods);
        }

        // ─────────────────────────────── Arrow / timing ─────────────────────

        private void TickArrow(float dt)
        {
            var bundle = GetStatBundle();
            float cc = bundle.Character.ClubControl;
            float ccClamped = Mathf.Clamp(cc, 0f, 100f);
            float arrowHz = _config.BaseArrowSpeedHzAtCC0 + ccClamped * _config.ArrowSpeedHzPerCC;
            if (IsPutt) arrowHz *= _config.PuttArrowSpeedMultiplier;

            _arrowProgress += arrowHz * dt;
            if (_arrowProgress < 1f) return;

            _arrowProgress -= 1f;
            _passIndex++;

            // Putt mode skips per-pass degradation entirely (design §4).
            if (!DebugFlags.SinglePassMode && !IsPutt)
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

            if (IsPutt || DebugFlags.DisableOverpower) return 1f;

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
            return StatProviderBus.Resolve(IsPutt);
        }

        // ── Order 350: SFX helpers ────────────────────────────────────────────────

        /// <summary>
        /// Publishes exactly one Swing* and one Hit* (or HitPutt) to SfxBus.
        /// Swing type is derived from IsPutt + StatProviderBus.CurrentLabClubIndex
        /// (the only club-type signal available inside Golfin.Gameplay.Input).
        /// Hit type is derived from power band (NOTE-F spec).
        /// Read-only: zero feedback into the physics sim.
        /// </summary>
        private void PublishShotSfx()
        {
            // --- Swing SFX (NOTE-F: putter→SwingPutt; index→swing club type) ---
            SfxId swingId;
            if (IsPutt)
            {
                swingId = SfxId.SwingPutt;
            }
            else
            {
                // StatProviderBus.CurrentLabClubIndex: 0=Driver/Wood, 1=Iron, 2=Wedge
                // In live gameplay the LiveStatProviderHost sets the resolver; index acts
                // as a best-effort proxy until a ClubType signal is added to the bus.
                int labIdx = StatProviderBus.CurrentLabClubIndex;
                swingId = labIdx switch
                {
                    0 => SfxId.SwingDriver,
                    1 => SfxId.SwingIron,
                    2 => SfxId.SwingWedge,
                    _ => SfxId.SwingDefault,
                };
            }
            SfxBus.Play(swingId);

            // --- Hit SFX (NOTE-F: putter→HitPutt; else power-band) ---
            SfxId hitId;
            if (IsPutt)
            {
                hitId = SfxId.HitPutt;
            }
            else
            {
                // Power >0.8 = strong; <0.3 = weak; else default
                float power = PowerNormalized;
                if (power > 0.8f)       hitId = SfxId.HitStrong;
                else if (power < 0.3f)  hitId = SfxId.HitWeak;
                else                    hitId = SfxId.HitDefault;
            }
            SfxBus.Play(hitId);
        }

        private void PublishState()
        {
            if (OnStateChanged == null) return;
            float cc = GetStatBundle().Character.ClubControl;
            int cleanPasses = Mathf.RoundToInt(_config.MaxCleanPassesAtCC0 + cc * _config.CleanPassesPerCC);

            // Compute live aim every frame so the targeting line and any aim-driven UI
            // can pivot during Idle/Aiming/Pulling/Timing. Final committed aim still uses
            // the same formula at CommitFlick (which adds degradation).
            float finetune = DebugFlags.DisableConeFineTune ? 0f : _coneFinetune;
            float liveAim  = CameraHeadingRadians + finetune * HalfConeAngleRad();

            OnStateChanged.Invoke(new ShotInputState(
                State, PowerNormalized, _coneFinetune, _arrowProgress,
                _passIndex, _passIndex >= cleanPasses,
                IsPutt, liveAim, CameraHeadingRadians));
        }
    }
}
