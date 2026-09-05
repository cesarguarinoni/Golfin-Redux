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

        /// <summary>
        /// Landing distance (METRES) of the target the player placed in map view.
        /// -1 = no target mapped. Written by MapViewController.CloseImmediate(); read by
        /// PowerGaugeWidget to draw the "flick to here" notch (power_gauge_target_marker).
        ///
        /// Stored in METRES, deliberately NOT normalized: a club change after the target was
        /// placed just moves the marker (the fraction is re-derived against the new club's
        /// carry) instead of lying about a distance that never changed.
        ///
        /// READ-ONLY with respect to the shot: nothing in ComputePower / CommitFlick consumes
        /// this — it is a HUD readout, not a power recalibration.
        /// Cleared at CommitFlick (one marker per mapped shot).
        /// </summary>
        public float MapTargetCarryM          { get; set; } = -1f;

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

        /// <summary>
        /// Read-only view of the current cone finetune value (−1..1).
        /// Used by MapViewController to mirror the Fade/Draw bend direction in the map guide line
        /// (Order 352 — map_view_aiming). Write path remains internal to ShotController.
        /// </summary>
        public float ConeFinetune => _aimFinetune;

        /// <summary>
        /// LIVE handle offset (−1..1) — always tracks the finger, even while the aim is latched.
        /// Display-only: read by ShotConeView to position the club handle. The aim/shape value is
        /// <see cref="ConeFinetune"/>, which freezes at the upswing reversal.
        /// </summary>
        public float HandleFinetune => _coneFinetune;

        // --- Debug toggles (8 flags per design §8) ---
        public ShotDebugFlags DebugFlags = ShotDebugFlags.Defaults;

        // ── Flick gate + aim lock (SHOT_FLICK_FIX_SPEC) ─────────────────────────
        // Applies to the human touch path only (samples pushed by ClubHandleDragger).
        // Programmatic drivers (bots, capture drivers, tests) push no samples and are
        // never gated — see EvaluateFlickGate().

        [Header("Flick gate (SHOT_FLICK_FIX_SPEC — Bug 1)")]
        [Tooltip("Minimum upward release speed in screen-heights/sec. 0 = gate off.")]
        [SerializeField] private float _minFlickSpeed = 1.2f;

        [Tooltip("Seconds of touch history averaged to measure the release speed. " +
                 "Windowed averaging makes a load stutter read LOW instead of spiking.")]
        [SerializeField] private float _flickSampleWindow = 0.08f;

        [Tooltip("Seconds. A sample pair spanning longer than this is a hitch frame and " +
                 "is never trusted as the basis for the flick velocity.")]
        [SerializeField] private float _stutterFrameThreshold = 0.1f;

        [Tooltip("Bypass the windowed gate and fall back to the legacy single-frame check.")]
        [SerializeField] private bool _debugDisableFlickGate;

        [Header("Aim lock (SHOT_FLICK_FIX_SPEC — Bug 2)")]
        [Tooltip("Screen-heights of cumulative upward travel from the lowest finger point " +
                 "that latches the aim. Cumulative-since-lowest means micro-jitter never latches.")]
        [SerializeField] private float _reversalThreshold = 0.01f;

        [Tooltip("Bypass the aim latch — the targeting line tracks through the upswing (old behavior).")]
        [SerializeField] private bool _debugDisableAimLock;

        /// <summary>True when the windowed flick gate owns the release decision.
        /// False = caller should fall back to its legacy single-frame check (debug parity).</summary>
        public bool FlickGateActive => !_debugDisableFlickGate && _minFlickSpeed > 0f;

        /// <summary>Last speed measured by EvaluateFlickGate, in screen-heights/sec. Tuning aid.</summary>
        public float LastFlickSpeedScreenHeights { get; private set; }

        /// <summary>True once the upswing reversal has latched the aim for this swing.</summary>
        public bool IsAimLocked => _aimLocked;

        /// <summary>When true, emits a one-line snapshot at CommitFlick entry naming the bundle, override, and gate inputs.</summary>
        public bool LogResolution;

        // --- Readable state ---
        public ShotState State            { get; private set; } = ShotState.Idle;
        public float     PowerNormalized  { get; private set; }
        public float     ConeHalfAngleDeg => HalfConeAngleRad() * Mathf.Rad2Deg;

        /// <summary>True if the most recent committed flick was a full-swing shot with zero aim
        /// degradation (committed inside the clean-pass window). Putts are never "clean" for trail
        /// purposes (see spec NOTE P). Latched in CommitFlick.
        ///
        /// NOTE: currently has NO readers. It used to drive the ball trail's gold "perfect shot"
        /// ribbon; the trail is now painted in the timing slab's own colour at the flick
        /// (BallTrailController.LaunchColor), so gold there means "flicked on the gold band", not
        /// "clean pass". Kept because it is the only surviving handle on clean-pass-ness — delete
        /// it deliberately, not by accident.</summary>
        public bool LastShotWasClean { get; private set; }

        /// <summary>Arrow progress (0..1) sampled when the aim latched on the current swing,
        /// or NaN when no touch swing latched. Read by tests and the resolution log.</summary>
        public float LastTimingAtLatch => _timingAtLatch;

        /// <summary>Power multiplier the last committed flick paid for its timing (F15).
        /// 1.0 for every sampleless driver and whenever ForcePerfectTiming is on.</summary>
        public float LastTimingPowerMul { get; private set; } = 1f;

        /// <summary>Arrow progress the LAST COMMITTED flick was judged on (shot_timing_telemetry D1).
        /// NaN when that flick had no touch sample (bots, capture drivers, EditMode tests).
        ///
        /// Distinct from <see cref="LastTimingAtLatch"/>, which reads the LIVE swing sample and is
        /// wiped by <c>ResetSwingSamples()</c> as soon as the shot completes — by the time the ball
        /// rests and the telemetry record is built, it is always NaN. This one is latched next to
        /// <see cref="LastTimingPowerMul"/> in CommitFlick and survives until the next commit.</summary>
        public float LastCommittedTiming01 { get; private set; } = float.NaN;

        // --- Internal state ---
        private float _pullDistancePx;
        private float _arrowProgress;
        private int   _passIndex;
        private float _degradationYawRad;
        private float _aimYawRadians;
        private float _coneFinetune;   // LIVE — follows the finger, drives the handle sprite
        private float _aimFinetune;    // AIM  — mirrors _coneFinetune until the upswing latch, then frozen
        private bool  _wasTouching;

        // Flick gate / aim lock state (SHOT_FLICK_FIX_SPEC)
        private const int SampleBufferSize = 6;
        private readonly Vector2[] _samplePos  = new Vector2[SampleBufferSize];
        private readonly float[]   _sampleTime = new float[SampleBufferSize];
        private int   _sampleCount;          // total pushed this swing (may exceed buffer size)
        private bool  _aimLocked;
        private float _lowestTouchY = float.NaN;

        /// <summary>Arrow progress captured at the instant the aim latched — the moment the
        /// player reacted to the slab (F15 D1). NaN = this swing never latched (programmatic
        /// driver, or the latch was re-opened), which means no timing penalty.</summary>
        private float _timingAtLatch = float.NaN;

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
        public void ForceRecenterFinetune()
        {
            _coneFinetune = 0f;
            _aimFinetune  = 0f;   // a re-arm must not restore a stale latched aim
        }

        // ── Flick gate + aim lock (SHOT_FLICK_FIX_SPEC) ─────────────────────────

        /// <summary>
        /// Push one touch sample (screen px) for this swing. Called every frame the finger is
        /// down by the pointer handler that owns the swing gesture (ClubHandleDragger), plus
        /// once at release. Feeds both the windowed flick gate (Bug 1) and the upswing aim
        /// latch (Bug 2). Callers that never push samples are never gated.
        /// </summary>
        public void PushTouchSample(Vector2 screenPosPx)
        {
            _samplePos[_sampleCount % SampleBufferSize]  = screenPosPx;
            _sampleTime[_sampleCount % SampleBufferSize] = Time.unscaledTime;
            _sampleCount++;

            if (_debugDisableAimLock) return;

            // Latch on cumulative upward travel since the LOWEST point of the swing, so
            // micro-jitter (up 2px, down 2px) never latches but a real upswing latches
            // within a frame or two. Aim = club position at the bottom of the swing.
            if (float.IsNaN(_lowestTouchY) || screenPosPx.y < _lowestTouchY)
            {
                _lowestTouchY = screenPosPx.y;
                if (_aimLocked)
                {
                    // The "reversal" was a wobble: the thumb came back down. Re-open the aim so
                    // lateral aiming at the cone base keeps steering the line (shot_aim_parity D3).
                    _aimLocked     = false;
                    _aimFinetune   = _coneFinetune;
                    _timingAtLatch = float.NaN;   // F15: the timing sample died with the latch
                }
                return;
            }

            float h = Screen.height;
            if (h <= 0f) return;
            if ((screenPosPx.y - _lowestTouchY) / h >= _reversalThreshold)
            {
                _aimLocked     = true;   // _aimFinetune already holds the bottom-of-swing value
                // F15 D1: the slab as the player saw it when they started the upflick. Sampling
                // here rather than at release matters — the finger leaves 50–150 ms later, and at
                // 2 Hz that is 10–30% of the cone, i.e. a whole band.
                _timingAtLatch = _arrowProgress;
            }
        }

        /// <summary>
        /// True if the release qualifies as a flick. Measured as the windowed average over
        /// <see cref="_flickSampleWindow"/> using unscaled time, so a hitch frame reads LOW
        /// instead of spiking. Returns true when no samples were pushed (programmatic driver:
        /// bots, capture drivers, tests) — the gate is for human touch input only.
        /// </summary>
        public bool EvaluateFlickGate()
        {
            LastFlickSpeedScreenHeights = 0f;

            if (!FlickGateActive) return true;
            if (_sampleCount == 0) return true;      // programmatic driver — not a touch swing
            if (_sampleCount < 2)  return false;     // a tap has no measurable travel

            int newest = (_sampleCount - 1) % SampleBufferSize;
            int stored = Mathf.Min(_sampleCount, SampleBufferSize);

            // Walk back to the oldest sample still inside the window.
            int oldest = newest;
            for (int step = 1; step < stored; step++)
            {
                int idx = (_sampleCount - 1 - step + SampleBufferSize * 2) % SampleBufferSize;
                if (_sampleTime[newest] - _sampleTime[idx] > _flickSampleWindow) break;
                oldest = idx;
            }

            // Every stored sample is older than the window (one very long frame) — fall back to
            // the immediately-previous sample so the hitch is measured rather than ignored. Its
            // dt then trips the stutter check below, which is the intended outcome.
            if (oldest == newest)
                oldest = (_sampleCount - 2 + SampleBufferSize * 2) % SampleBufferSize;

            float dt = _sampleTime[newest] - _sampleTime[oldest];
            if (dt <= 0f) return false;
            if (dt > _stutterFrameThreshold) return false;   // hitch frame — never trusted

            float h = Screen.height;
            if (h <= 0f) return false;

            LastFlickSpeedScreenHeights = ((_samplePos[newest].y - _samplePos[oldest].y) / dt) / h;

#if UNITY_EDITOR
            if (LogResolution)
                UnityEngine.Debug.Log(
                    $"[FlickGate] speed={LastFlickSpeedScreenHeights:F2} screen-heights/s " +
                    $"min={_minFlickSpeed:F2} dt={dt:F3}s samples={_sampleCount} " +
                    $"pass={LastFlickSpeedScreenHeights >= _minFlickSpeed} aimLocked={_aimLocked}");
#endif
            return LastFlickSpeedScreenHeights >= _minFlickSpeed;
        }

        /// <summary>
        /// Write the live handle offset. The aim value follows it only until the upswing latch —
        /// after that the handle keeps tracking the finger while the aim stays put, which is the
        /// whole point of the latch (Cesar: "the handle should still follow the finger, it is the
        /// aiming that should not change on the UP flick").
        /// </summary>
        private void SetLiveFinetune(float v)
        {
            _coneFinetune = v;
            if (!_aimLocked) _aimFinetune = v;
        }

        private void ResetSwingSamples()
        {
            _sampleCount    = 0;
            _aimLocked    = false;
            _aimFinetune  = 0f;
            _lowestTouchY   = float.NaN;
            _timingAtLatch  = float.NaN;
        }

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

        /// <summary>
        /// Editor-only seam: pre-set _coneFinetune while in Idle state so that
        /// MapViewController.Open() snapshots a non-zero value for criterion-6 bend demonstration.
        /// (map_view_aiming Order 352 — capture driver sets this BEFORE tapping Open.)
        /// NOT callable in production (Idle→ExternalDrag transition is the production path).
        /// </summary>
        public void SetFinetuneForCapture(float v) => _coneFinetune = _aimFinetune = Mathf.Clamp(v, -1f, 1f);
#endif

        // ── External drag API (ClubHandle → drives shot without pixel-pull math) ──

        public bool IsExternalDragActive => _externalDragActive;
        private bool _externalDragActive;

        /// <summary>True when the active external driver is the timing authority for this swing
        /// (control_scheme_seam §3.1). Flick leaves it false and is untouched.</summary>
        private bool _ownsTiming;

        /// <summary>
        /// The parameterless overload is PRESERVED deliberately rather than folded into an
        /// optional parameter: Editor bots and capture drivers resolve this method reflectively
        /// with <c>Type.EmptyTypes</c> (ClubControlArrowDemoRecorder, PowerGaugeMarkerDemoRecorder,
        /// PowerGaugeMarkerVerifyBot, PracticeMapDuringShotDemoRecorder). One method with a
        /// default argument returns null at every one of those lookup sites.
        /// </summary>
        public void BeginExternalDrag() => BeginExternalDrag(false);

        /// <param name="ownsTiming">control_scheme_seam §3.1 — the driver is the timing authority.
        /// <see cref="Tick"/> then skips <see cref="TickArrow"/> in <c>Timing</c>: no arrow, no
        /// per-pass degradation, no <c>MaxTotalPasses</c> auto-cancel, <c>_arrowProgress</c> stays
        /// 0. Such a driver calls <see cref="CommitExternal"/> or <see cref="CancelExternalDrag"/>
        /// itself and never <see cref="EndExternalDrag"/>.</param>
        public void BeginExternalDrag(bool ownsTiming)
        {
            if (State != ShotState.Idle) return;
            _externalDragActive = true;
            _ownsTiming         = ownsTiming;
            ResetSwingSamples();
            TransitionToAiming();
            PublishState();
        }

        /// <summary>
        /// Ceiling on <see cref="PowerNormalized"/>. 1.2 = the 120% overpower pull, the same
        /// number <see cref="ComputePower"/> has always capped the flick at and the same one
        /// <see cref="FireDebugShot"/> clamps to. Named here because the pendulum's lane reads it
        /// as geometry (the 120% tick) rather than inferring it from a literal.
        /// </summary>
        public const float MaxOverpowerNormalized = 1.2f;

        /// <param name="powerNormalized">0..<see cref="MaxOverpowerNormalized"/>. Was Clamp01 until
        /// scheme_pendulum §3.1: the flick's own <c>ClubHandleDragger</c> derives power as
        /// <c>1 - handleY/coneHeight</c> and so can never exceed 1.0, which is why widening the
        /// ceiling leaves Flick byte-identical. Putts are still clamped to 1.0, but at
        /// <see cref="CommitExternal"/>/<see cref="CommitFlick"/> where they always were — a
        /// driver that publishes 1.2 on a putt sees the gauge read 120% and the shot fly 100%,
        /// exactly as the flick does today.</param>
        public void SetExternalPower(float powerNormalized, float coneFinetune)
        {
            if (!_externalDragActive) return;
            PowerNormalized = Mathf.Clamp(powerNormalized, 0f, MaxOverpowerNormalized);
            // Aim latched at the upswing reversal: lateral finger movement stops steering the
            // line, which just freezes at its last value (SHOT_FLICK_FIX_SPEC Bug 2).
            SetLiveFinetune(Mathf.Clamp(coneFinetune, -1f, 1f));
            if (State == ShotState.Aiming && powerNormalized > 0f)
                TransitionToTiming();
            PublishState();
        }

        /// <param name="bypassFlickGate">Debug escape hatch (ClubHandleDragger._releaseToFire):
        /// commit on any release without measuring the flick.</param>
        public void EndExternalDrag(bool bypassFlickGate = false)
        {
            if (!_externalDragActive) return;
            _externalDragActive = false;

            // Too slow to be a flick → reset the swing, no shot. TransitionToIdle IS the
            // existing power-reset path, so the player just pulls back again.
            bool validFlick = bypassFlickGate || EvaluateFlickGate();

            if (State == ShotState.Timing && PowerNormalized > 0f && validFlick)
                CommitFlick();
            else
            {
                if (!validFlick) FlickRejected?.Invoke(LastFlickSpeedScreenHeights);
                TransitionToIdle();
            }
        }

        public void CancelExternalDrag()
        {
            if (!_externalDragActive) return;
            _externalDragActive = false;
            ShotCancelled?.Invoke();
            TransitionToIdle();
        }

        /// <summary>
        /// Reject the release that just happened: the player let go too slowly to be a flick, so
        /// the swing resets and no shot is taken (scheme_pendulum §3.1).
        ///
        /// <para>This is the failed-gate branch of <see cref="EndExternalDrag"/>, and nothing
        /// more. A driver that owns its own timing never calls EndExternalDrag — it decides for
        /// itself whether the release counted, because only it knows where its marker was — but
        /// the CONSEQUENCE of a failed release has to stay one implementation, or the toast and
        /// the reset drift apart between schemes.</para>
        /// </summary>
        public void RejectExternalDrag()
        {
            if (!_externalDragActive) return;
            _externalDragActive = false;
            FlickRejected?.Invoke(LastFlickSpeedScreenHeights);
            TransitionToIdle();
        }

        // ── Read-only stat seams for scheme drivers (scheme_pendulum §3.1) ───────
        // A driver grades a swing against the same stats the shot pipeline resolves it with.
        // Exposing the three it needs READ-ONLY keeps StatProviderBus/StatModifierResolver
        // plumbing out of every future driver, and keeps the numbers single-sourced: a driver
        // that resolved its own bundle could grade against stats the shot never used.

        /// <summary>Club (or putter) Accuracy as 0..1 — the same value <see cref="HalfConeAngleRad"/>
        /// lerps the cone with, so a scheme's accuracy windows and the cone cannot disagree.</summary>
        public float ClubAccuracyNorm01 => GetClubAccuracyNorm();

        /// <summary>Character Club Control, raw points. Drives arrow/marker speed.</summary>
        public int CharacterClubControl => GetStatBundle().Character.ClubControl;

        /// <summary>
        /// Overpower forgiveness 0..1 — how much of an over-100% pull the character absorbs.
        ///
        /// <para>Mirrors <c>StatModifierResolver</c> step 6 (<c>effStrength * CharStrengthPerPoint</c>).
        /// The resolver caps at <c>StatCaps.Default.OverpowerForgivenessMax</c> = 0.75, which
        /// 120 Strength reaches exactly; this clamp is Clamp01 per spec §3.1 and so only differs
        /// above 160 Strength, which no rarity cap reaches. It reads the bundle's raw Strength
        /// rather than the stamina-scaled <c>effStrength</c> for the same reason
        /// <c>StatResolverTests</c> asserts forgiveness is stamina-invariant.</para>
        /// </summary>
        public float OverpowerForgiveness01 => Mathf.Clamp01(
            GetStatBundle().Character.Strength * StatCoefficients.Default.CharStrengthPerPoint.ToFloat());

        // ── Telemetry signals (beta_telemetry SPEC §1 #6/#7) ──────────────────────
        // STATIC because the subscriber is the telemetry layer, which comes up at boot and
        // must not race a per-hole ShotController instance into existence.
        //
        // Golfin.Gameplay.Input is autoReferenced:false, so Assembly-CSharp cannot see these
        // directly — ShotTelemetryRelay (Golfin.Gameplay.UI) re-raises them where the hooks
        // can subscribe. Raising an event nobody listens to costs a null check.

        /// <summary>Player released the drag but the flick was too slow to fire.
        /// Argument is <see cref="LastFlickSpeedScreenHeights"/> at the moment of rejection.</summary>
        public static event System.Action<float> FlickRejected;

        /// <summary>Player abandoned a drag without releasing into a shot.</summary>
        public static event System.Action ShotCancelled;

        // Fires a shot directly without gesture input. Maps accuracy preset to degradation yaw.
        // power range: 0–1.2 (same as PowerNormalized; 1.0 = 100%).
        public void FireDebugShot(float power, DebugShotAccuracy accuracy, float coneFinetune = 0f)
        {
            TransitionToIdle();
            PowerNormalized    = Mathf.Clamp(power, 0f, 1.2f);
            // Carry the fade/draw shaping only when armed; the default 0 keeps every existing
            // caller firing dead-straight exactly as before. This lets a debug/bot shot curve.
            _coneFinetune      = _aimFinetune = (!IsPutt && FadeDrawActive) ? Mathf.Clamp(coneFinetune, -1f, 1f) : 0f;
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
                // A driver that owns its own timing gets no arrow: no _arrowProgress, no
                // per-pass degradation and no MaxTotalPasses auto-cancel (control_scheme_seam §3.1).
                if (State == ShotState.Timing && !_ownsTiming) TickArrow(dt);
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
                        SetLiveFinetune(ComputeFinetune());
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
                        SetLiveFinetune(ComputeFinetune());
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
            _ownsTiming        = false;
            _coneFinetune      = 0f;
            _aimFinetune       = 0f;
            _aimYawRadians     = 0f;
            PendingSpinInput   = Vector2.zero;  // reset after each shot (spin is per-shot, not sticky)
            // Unlatch the aim + drop the touch history on EVERY path back to pull-back:
            // min-flick-speed failure, slow-release power reset, arrow timeout, shot complete.
            ResetSwingSamples();
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

            // One marker per mapped shot: the map target dies with the stroke that used it.
            // Deliberately NOT in TransitionToIdle — a failed flick (slow release, arrow
            // timeout, cancelled drag) routes there too, and re-pulling after a fumbled flick
            // must keep the marker the player just placed on the map.
            MapTargetCarryM = -1f;

            // ── Order 350: Swing + Hit SFX ────────────────────────────────────────
            // Published at the moment the player commits the shot. Read-only: does not
            // touch BallSimulation, BallStateMachine, or any fixed-point state.
            PublishShotSfx();

            float degradYaw = DebugFlags.ForcePerfectAim ? 0f : _degradationYawRad;
            LastShotWasClean = !IsPutt && Mathf.Approximately(degradYaw, 0f);   // latched; no reader today (see the property doc)
            float finetune  = DebugFlags.DisableConeFineTune ? 0f : _aimFinetune;

            // shot_aim_parity D1/D2: ONE formula, shared with PublishState (the targeting line).
            // Straight + putt map the handle to ±halfCone; FadeDraw uses the aim locked at arming
            // time and spends the handle on the curve instead. Degradation is the only extra term.
            float aimYaw = AimYawFor(finetune) + degradYaw;

            // F15 D6: the timing multiplier lands AFTER the overpower clamp, so a 120% pull
            // flicked on red is 84% — overpowering does not buy you out of bad timing.
            float timingMul = TimingPowerMultiplier();

            float flickMag = PowerNormalized;
            if (IsPutt || DebugFlags.DisableOverpower) flickMag = Mathf.Min(flickMag, 1f);
            flickMag *= timingMul;

            // Spin input: read PendingSpinInput (set by HUD layer before CommitFlick).
            // Putts always use zero spin (design lock §Out of scope).
            Vector2 spinInput = IsPutt ? Vector2.zero : PendingSpinInput;

            // Phase B (fade_draw_core_wiring Order 356):
            // FadeDraw armed + not putt: finetune (re-centered after arm) drives fade/draw curve.
            // Straight mode or putt: fadeDrawInput = 0 (no curve).
            // NOTE the max-tilt term is gated on the MODE, not on the input value: an armed
            // FadeDraw with the handle dead-centre still passes FadeDrawMaxTiltRad today, and
            // deriving it from "input != 0" would quietly change that.
            bool  fadeDrawOn      = !IsPutt && FadeDrawActive;
            float fadeDrawInput   = fadeDrawOn ? finetune : 0f;
            float fadeDrawMaxTilt = fadeDrawOn ? _config.FadeDrawMaxTiltRad : 0f;

#if UNITY_EDITOR
            if (LogResolution)
            {
                var logBundle = GetStatBundle();
                fp logBaseVel = IsPutt ? fp.FromFloat(_config.PuttBaseVelocityMps) : fp.Zero;
                string clubVel    = logBundle.Club.HasValue   ? logBundle.Club.Value.BaseVelocityMps.ToFloat().ToString("F2")   : "n/a";
                string putterVel  = logBundle.Putter.HasValue ? logBundle.Putter.Value.BaseVelocityMps.ToFloat().ToString("F2") : "n/a";
                UnityEngine.Debug.Log(
                    $"[CommitFlick] IsPutt={IsPutt} bundle.IsPutt={logBundle.IsPutt} " +
                    $"bundle.Club.HasValue={logBundle.Club.HasValue} clubVel={clubVel}m/s " +
                    $"bundle.Putter.HasValue={logBundle.Putter.HasValue} putterVel={putterVel}m/s " +
                    $"PowerNormalized={PowerNormalized:F3} flickMag={flickMag:F3} " +
                    $"PuttBaseVelocityMps={_config.PuttBaseVelocityMps:F2} " +
                    $"baseVelOverride={logBaseVel.ToFloat():F2}m/s " +
                    $"halfCone={HalfConeAngleRad() * Mathf.Rad2Deg:F1}deg finetune={finetune:F3} " +
                    $"timing01={_timingAtLatch:F2} timingMul={timingMul:F2} " +
                    $"aimYawRadians={aimYaw:F3}rad");
            }
#endif
            ResolveAndPublish(flickMag, aimYaw, timingMul, _timingAtLatch,
                              spinInput, fadeDrawInput, fadeDrawMaxTilt);
        }

        /// <summary>
        /// Commit a shot a NON-FLICK control scheme has already judged (control_scheme_seam §3.1).
        ///
        /// <para>Everything scheme-specific is already inside <paramref name="i"/>; this method
        /// only applies the rules that belong to the shot pipeline rather than to any one scheme:
        /// the debug flags, the putt/overpower clamp, the putt spin lock, and the single
        /// <see cref="AimYawFor"/> formula the live targeting line also uses.</para>
        ///
        /// <para>Flick does NOT come through here — <see cref="CommitFlick"/> keeps its own maths
        /// and calls the same <c>ResolveAndPublish</c> tail, which is what keeps the shipping
        /// scheme byte-identical.</para>
        /// </summary>
        public void CommitExternal(in ShotIntent i)
        {
            // Pulling is legal as well as Timing: a driver may have no arrow phase at all.
            if (!_externalDragActive || (State != ShotState.Timing && State != ShotState.Pulling))
            {
                Debug.LogWarning(
                    $"[ShotController] CommitExternal ignored — externalDrag={_externalDragActive} state={State}. " +
                    "A scheme driver must BeginExternalDrag and reach Pulling/Timing first.");
                return;
            }

            State = ShotState.Flicking;
            _externalDragActive = false;

            // Same one-marker-per-mapped-shot rule as CommitFlick, and for the same reason.
            MapTargetCarryM = -1f;

            // Order 350: SFX first, before any of the resolve maths — as in CommitFlick.
            PublishShotSfx();

            float finetune = DebugFlags.DisableConeFineTune ? 0f : i.AimOffset01;
            float errorYaw = DebugFlags.ForcePerfectAim    ? 0f : i.ErrorYawRad;
            float aimYaw   = AimYawFor(finetune) + errorYaw;

            LastShotWasClean = !IsPutt && Mathf.Approximately(errorYaw, 0f);

            float timingMul = DebugFlags.ForcePerfectTiming ? 1f : i.TimingMul;

            float mag = i.PowerNormalized;
            if (IsPutt || DebugFlags.DisableOverpower) mag = Mathf.Min(mag, 1f);
            mag *= timingMul;

            Vector2 spinInput     = IsPutt ? Vector2.zero : PendingSpinInput;
            float   fadeDrawInput = IsPutt ? 0f : i.FadeDraw01;
            float   fadeDrawTilt  = IsPutt ? 0f : _config.FadeDrawMaxTiltRad;

            ResolveAndPublish(mag, aimYaw, timingMul, i.Timing01,
                              spinInput, fadeDrawInput, fadeDrawTilt);
        }

        /// <summary>
        /// The scheme-agnostic tail of every shot: latch the telemetry snapshots, build the
        /// <c>ShotInput</c> and raise <see cref="OnShotResolved"/>. Physics, telemetry, SFX
        /// consumers, the tournament <c>ShotCommand</c> and stamina all hang off this one call,
        /// so a new control scheme adds a driver and changes NOTHING below this line.
        /// </summary>
        private void ResolveAndPublish(float flickMag, float aimYawRad, float timingMul, float timing01,
                                       Vector2 spinInput, float fadeDrawInput, float fadeDrawMaxTiltRad)
        {
            _aimYawRadians     = aimYawRad;
            LastTimingPowerMul = timingMul;
            // Snapshot for telemetry: _timingAtLatch dies with ResetSwingSamples at CompleteShot.
            LastCommittedTiming01 = timing01;

            // Putt mode: pass PuttBaseVelocityMps as explicit override so ControlsConfig
            // drives the velocity, not whatever is in the StatBundle.
            fp baseVelOverride = IsPutt ? fp.FromFloat(_config.PuttBaseVelocityMps) : fp.Zero;

            var bundle = GetStatBundle();

            fp spinInputX   = fp.FromFloat(spinInput.x);
            fp spinInputY   = fp.FromFloat(spinInput.y);
            fp spinMagSlope = fp.FromFloat(_config.SpinMagScaleSlope);
            fp spinTiltRad  = fp.FromFloat(_config.SpinMaxTiltRad);

            fp fadeDrawInputFp   = fp.FromFloat(fadeDrawInput);
            fp fadeDrawMaxTiltFp = fp.FromFloat(fadeDrawMaxTiltRad);

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

            // Phase 3 (stamina_tournament_wiring, D4): per-shot drain REMOVED.
            // Tournament pool is drained once per hole in LocalTournamentBackend.SubmitHoleResult,
            // keeping the pool constant within a hole. The pool is read by the Phase-2
            // LiveStatProviderHost.ResolveLive seam unchanged — no edit needed there.

            State = ShotState.Resolving;
            OnShotResolved?.Invoke(input, ballMods);
        }

        // ─────────────────────────────── Arrow / timing ─────────────────────

        /// <summary>
        /// Power multiplier for the timing of this swing (F15, SHOT_CONTROLS_DESIGN §3.4:
        /// "off-time flicks reduce power"). Piecewise-linear through the SAME band edges the
        /// slab is coloured with, so the colour under the ball at the moment of the upflick is
        /// literally the multiplier: red base → <c>TimingPowerMulRed</c>, gold line →
        /// <c>TimingPowerMulGold</c>, green line and above → 1.0.
        ///
        /// Returns 1.0 — a byte-identical shot — when there is no timing to judge (D4): bots,
        /// capture drivers, FireDebugShot, the legacy IInputSource path and EditMode tests push
        /// no touch samples so they never latch, and ForcePerfectTiming opts out explicitly.
        /// </summary>
        private float TimingPowerMultiplier()
        {
            if (DebugFlags.ForcePerfectTiming || float.IsNaN(_timingAtLatch)) return 1f;

            float t     = Mathf.Clamp01(_timingAtLatch);
            float gold  = _config.TimingBandGoldY01;
            float green = _config.TimingBandGreenY01;

            if (t >= green) return 1f;
            if (t >= gold)
                return Mathf.Lerp(_config.TimingPowerMulGold, 1f,
                                  (t - gold) / Mathf.Max(1e-4f, green - gold));
            return Mathf.Lerp(_config.TimingPowerMulRed, _config.TimingPowerMulGold,
                              t / Mathf.Max(1e-4f, gold));
        }

        private void TickArrow(float dt)
        {
            var bundle = GetStatBundle();
            float cc = bundle.Character.ClubControl;
            float ccClamped = Mathf.Clamp(cc, 0f, 100f);
            float arrowHz = _config.BaseArrowSpeedHzAtCC0 + ccClamped * _config.ArrowSpeedHzPerCC;
            // F13: the CC line has a negative slope and no natural floor — past
            // CC = Base/|Slope| it goes negative, the arrow runs backwards, never completes a
            // pass, and the shot never auto-cancels. Clamp BEFORE the putt multiplier: applying
            // it after would raise a high-CC putt back up to the floor and break the invariant
            // that putts are always slower than swings at equal CC (ShotControllerPuttModeTests.F1).
            arrowHz = Mathf.Max(arrowHz, _config.MinArrowSpeedHz);
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

        /// <summary>
        /// Aim yaw WITHOUT per-pass degradation — the SINGLE source of truth for where the ball
        /// goes. Used by PublishState (the live targeting line) and CommitFlick (which adds
        /// degradYaw). If these ever disagree the line lies — see ShotAimParityTests.
        /// </summary>
        private float AimYawFor(float finetune)
        {
            if (!IsPutt && FadeDrawActive)
            {
                // Aim was locked when the toggle was armed (D5, Order 356). Handle = curve, not aim.
                // NaN = lock cleared by a shot reset → re-lock to the live camera heading now.
                return float.IsNaN(FadeDrawLockedAimRad) ? CameraHeadingRadians : FadeDrawLockedAimRad;
            }
            // Straight swing AND putt: handle position maps to ±halfCone (SHOT_CONTROLS_DESIGN §3.3).
            return CameraHeadingRadians + finetune * HalfConeAngleRad();
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
            float finetune = DebugFlags.DisableConeFineTune ? 0f : _aimFinetune;
            float liveAim  = AimYawFor(finetune);

            OnStateChanged.Invoke(new ShotInputState(
                State, PowerNormalized, _aimFinetune, _arrowProgress,
                _passIndex, _passIndex >= cleanPasses,
                IsPutt, liveAim, CameraHeadingRadians,
                TimingPowerMultiplier()));
        }
    }
}
