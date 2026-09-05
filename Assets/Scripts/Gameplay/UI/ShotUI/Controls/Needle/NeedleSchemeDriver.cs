using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>
    /// The Needle scheme's input driver — player-facing name "Tap Timing" (scheme_needle §3.2).
    /// Two touches: pull the club head back inside a power circle and RELEASE to commit the power,
    /// then TAP anywhere on the shot area while a needle sweeps the accuracy arc once.
    ///
    /// <para>OWNS ITS TIMING, SO IT OWNS ITS RELEASE. <c>BeginExternalDrag(ownsTiming: true)</c>
    /// takes the flick's arrow, its per-pass degradation and its <c>MaxTotalPasses</c> auto-cancel
    /// off the table. The corollary is that it must never call <c>EndExternalDrag</c>, which would
    /// decide for itself using the arrow this driver just disabled; it calls
    /// <c>CommitExternal</c> or <c>CancelExternalDrag</c> instead, exactly once per swing.</para>
    ///
    /// <para>NO FLICK GATE, unlike Pendulum. The release here is not the shot — it is the END of
    /// the power gesture, and the shot happens on a separate tap that may be seconds later.
    /// Measuring how fast the thumb left the glass would reject a perfectly good lay-up for being
    /// gentle, which is precisely what the gesture asks for. So <c>RejectExternalDrag</c> is never
    /// called and no touch samples are pushed: without samples the aim-reversal latch never
    /// engages either, which is what it should do here, because there IS no upswing to latch — the
    /// drawn aim line keeps following the finger all the way to the release.</para>
    ///
    /// <para>THE CONTROLLER'S <c>Timing</c> STATE SPANS BOTH OF THIS SCHEME'S PHASES. That is why
    /// the driver keeps its own <see cref="_phase"/>: <c>ShotController</c> only needs to know that
    /// an external drag is live and that this driver will commit it, while "is the player pulling
    /// or is the needle sweeping" is a question only the scheme can answer. No seam addition was
    /// needed for it — <c>Tick</c> already returns early for an owns-timing external drag, so the
    /// swing simply waits, and <c>ShotInProgressUiGate</c> only closes at <c>Flicking</c>, i.e.
    /// after the tap, so the tap area is still live when the tap arrives.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NeedleSchemeDriver : MonoBehaviour, IShotSchemeDriver,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>Which of the two touches the player is on. See the class remarks for why this
        /// cannot be read off <c>ShotController.State</c>.</summary>
        private enum Phase { Idle, Pull, Needle }

        /// <summary>Longest frame the needle will advance by. See <see cref="Advance"/>.</summary>
        private const float MaxNeedleStepSeconds = 1f / 30f;

        [Header("Wiring")]
        [Tooltip("SchemeRoot_Needle's RectTransform — the space pull/curve pixels are measured in, " +
                 "so the maths and the drawn circle share one coordinate system.")]
        [SerializeField] private RectTransform _schemeRoot;

        [Tooltip("The club-head Image (a copy of ClubHandle, carrying ClubHandleSpriteBinder).")]
        [SerializeField] private RectTransform _handle;

        [SerializeField] private NeedlePowerCircleView _circleView;
        [SerializeField] private NeedleArcView         _arcView;
        [SerializeField] private NeedleTapCatcher      _tapCatcher;
        [SerializeField] private SchemeGradePop        _gradePop;

        [Header("Feel")]
        [Tooltip("Seconds the club head takes to snap back to the ball after the release.")]
        [SerializeField] private float _handleReturnSeconds = 0.15f;

        [Header("Debug")]
        [Tooltip("One line per committed swing: needle offset, grade, sweep seconds, power.")]
        [SerializeField] private bool _logSwings;

        private ShotController _controller;
        private ControlsConfig _cfg = ControlsConfig.Default;

        private Phase   _phase = Phase.Idle;
        private bool    _dragging;
        private Vector2 _originLocal;
        private float   _power;
        private float   _curve;
        private float   _needle = -1f;
        private float   _sweepSec = 1f;
        private Vector2 _handleRest;
        private Vector2 _handleReturnFrom;
        private float   _handleReturnT = 1f;
        private CanvasGroup _handleGroup;

        // ── What the swing is JUDGED on (not what the finger is doing at release) ────
        //
        // The identical argument ClubHandleDragger's _peakPower and PendulumSchemeDriver's make:
        // the release is part of the gesture and the finger travels while it happens. Read live at
        // OnPointerUp, a thumb that has already started lifting reports a shallower pull than the
        // one the player chose. Committing the PEAK is also what makes carry-over 2 honest — the
        // zones are drawn from this same number, so the target the player watched close is the
        // target they are graded against.
        private float _peakPower;
        private float _peakCurve;

        // ── IShotSchemeDriver ────────────────────────────────────────────────────

        public ControlScheme Scheme        => ControlScheme.Needle;
        public bool          IsImplemented => true;

        public void Bind(ShotController controller) => _controller = controller;

        public void Activate()
        {
            ResetSwing();
            ShowHandle(true);
            ApplyLayout();
            if (_tapCatcher != null) _tapCatcher.OnTapped += OnTap;
            if (_controller != null) _controller.OnStateChanged += HandleStateChanged;
        }

        public void Deactivate()
        {
            if (_tapCatcher != null) _tapCatcher.OnTapped -= OnTap;
            if (_controller != null) _controller.OnStateChanged -= HandleStateChanged;
            ResetSwing();
        }

        private void Awake()
        {
            if (_schemeRoot == null) _schemeRoot = transform.parent as RectTransform;
            BindHandle();
        }

        // OnDisable, not only Deactivate: the host turns the ROOT off when the player switches
        // scheme, and a root that goes inactive never gets Deactivate called on its children.
        private void OnDisable()
        {
            if (_tapCatcher != null) _tapCatcher.OnTapped -= OnTap;
            if (_controller != null) _controller.OnStateChanged -= HandleStateChanged;
            ResetSwing();
        }

        private void BindHandle()
        {
            if (_handle == null) return;
            _handleRest  = _handle.anchoredPosition;
            _handleGroup = _handle.GetComponent<CanvasGroup>();
            if (_handleGroup == null) _handleGroup = _handle.gameObject.AddComponent<CanvasGroup>();
        }

        private void ShowHandle(bool visible)
        {
            if (_handleGroup != null) _handleGroup.alpha = visible ? 1f : 0f;
        }

        // ── Layout ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Push the live stats into the drawn circle and arc. Called at Activate and again at every
        /// pointer-down, because the club (and therefore the accuracy windows, and whether this is
        /// a putt at all) can change between two swings but never during one.
        /// </summary>
        private void ApplyLayout()
        {
            bool isPutt = _controller != null && _controller.IsPutt;
            _circleView?.ApplyGeometry(_cfg, isPutt);
            _arcView?.ApplyGeometry(_cfg, isPutt);
            RedrawZones();
        }

        /// <summary>
        /// Size the drawn zones for the power the shot WOULD commit at right now. Called every drag
        /// frame, not just at Activate: the whole point of the power shrink is that the player
        /// watches the blue zone close as they pull.
        ///
        /// <para>Drawn from <see cref="_peakPower"/> and not the live power, because the shot
        /// commits at the peak — a zone that widened again when the finger eased back would be
        /// showing a target the swing is not going to be judged against.</para>
        /// </summary>
        private void RedrawZones()
        {
            if (_arcView == null) return;
            bool  isPutt = _controller != null && _controller.IsPutt;
            float acc    = _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f;
            _arcView.ApplyWindows(NeedleMath.PerfectZone01(acc, _peakPower, _cfg),
                                  NeedleMath.GoodZone01(acc, _peakPower, _cfg),
                                  isPutt);
        }

        private void HandleStateChanged(ShotInputState state)
        {
            _circleView?.ApplyState(state.State);

            // The arc is deliberately NOT driven by the state alone, for two separate reasons.
            //
            // It must not come up EARLY: the pull phase and the needle phase are both
            // ShotState.Timing, and an arc that appeared with the circle would be showing a target
            // before the power that sizes it exists. So it is raised at the release, not by a state.
            //
            // And it must not go down EARLY either. The shared fading view drops its target at
            // Resolving — right for the Pendulum's bar, which is stale the moment the ball is in
            // the air, and wrong here: the frozen needle, the tap pip and the zone the tap landed
            // in ARE the result readout, and the node's Result frame draws them at full opacity.
            // CommitExternal reaches Resolving synchronously, so forwarding it faded the arc out
            // ~2 frames after the tap; the acceptance capture measured the navy at (34,55,53)
            // against its own (10,38,55) and then (70,93,42) — i.e. grass — one shot later.
            // Only Idle puts it away, and ResetVisualsForNextSwing does that.
            if (_phase == Phase.Needle || state.State == ShotState.Idle)
            {
                LastStateForwardedToArc = state.State;
                _arcView?.ApplyState(state.State);
            }

            if (state.State == ShotState.Idle) ResetVisualsForNextSwing();

            // The club head comes BACK here, and only here. It is hidden at commit rather than on
            // a Flicking event, because ShotController does not PublishState on the Idle->Flicking
            // transition — waiting for a Flicking state that never arrives would leave the handle
            // under a ball that has already gone (the scar the Pendulum test now pins).
            if (state.State != ShotState.Flicking && state.State != ShotState.Resolving)
                ShowHandle(true);
        }

        // ── Touch 1: the pull ────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            if (_controller == null || _schemeRoot == null) return;
            if (_controller.State != ShotState.Idle) return;

            BeginSwingLocal(ToLocal(e));
            ProcessDragLocal(ToLocal(e));
        }

        /// <summary>
        /// Open a swing at a point in the scheme root's LOCAL space. Split out of
        /// <see cref="OnPointerDown"/> so <see cref="DriveBot"/> can start the identical swing
        /// without a <c>PointerEventData</c> — a bot has no camera and no press position for
        /// <c>RectTransformUtility</c> to project, but it needs every field this method resets,
        /// in the order it resets them.
        /// </summary>
        private void BeginSwingLocal(Vector2 originLocal)
        {
            _dragging  = true;
            _phase     = Phase.Pull;
            _power     = 0f;
            _curve     = 0f;
            _peakPower = 0f;
            _peakCurve = 0f;
            _needle    = -1f;
            _handleReturnT = 1f;

            ApplyLayout();
            _gradePop?.HideImmediate();
            _arcView?.HideImmediate();
            _circleView?.SetDimmed(false);
            _originLocal = originLocal;

            // Same order ClubHandleDragger uses, and for the same reason: BeginExternalDrag resets
            // the swing (which clears PendingSpinInput), so the HUD's spin must be pushed after it.
            _controller.BeginExternalDrag(ownsTiming: true);
            _controller.PendingSpinInput = SpinContext.Spin;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            ProcessDragLocal(ToLocal(e));
        }

        private void ProcessDragLocal(Vector2 local)
        {
            float   pullPx = Mathf.Max(0f, _originLocal.y - local.y);

            bool isPutt = _controller.IsPutt;
            _power = NeedleMath.Power(pullPx, _cfg, isPutt);

            // Lateral pull is the fade/draw amount, and ONLY that: in Straight mode it does
            // nothing at all, which is what makes "pull straight down" the whole gesture.
            bool fadeDraw = !isPutt && _controller.FadeDrawActive;
            _curve = fadeDraw
                ? Mathf.Clamp((local.x - _originLocal.x) / Mathf.Max(_cfg.NeedleCurveHalfWidthPx, 1f), -1f, 1f)
                : 0f;

            if (_power > _peakPower)
            {
                _peakPower = _power;
                _peakCurve = _curve;
                RedrawZones();      // the target closes as the pull deepens
            }

            // The LIVE value, so the gauge and the club head track the finger all the way, exactly
            // as they do under Flick.
            _controller.SetExternalPower(_power, _curve);
            MoveHandle(pullPx, local.x - _originLocal.x, fadeDraw);
        }

        // ── The release: power is committed, the needle starts ───────────────────

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            ReleasePower();
        }

        /// <summary>
        /// End the power gesture and start the needle. Split out of <see cref="OnPointerUp"/> so
        /// <see cref="DriveBot"/> reaches the needle phase down the same path a thumb does —
        /// including the republish-at-peak, the sweep-seconds resolve and the arming of the real
        /// tap catcher, which is what the bot's tap then goes through.
        /// </summary>
        private void ReleasePower()
        {
            _dragging = false;

            // A touch that never became a pull is not a shot. This subsumes the flick's
            // PullStartThresholdPx: NeedleMinUsefulPullPx (40px) is above it (30px), so any
            // release inside the threshold is already power 0 here.
            if (_peakPower <= 0.02f)
            {
                _controller.CancelExternalDrag();
                ResetSwing();
                return;
            }

            _phase  = Phase.Needle;
            _needle = -1f;

            // Republish at the PEAK. Everything downstream of the seam — the power gauge, the
            // putt path predictor, the map ring — spends the whole needle phase showing the power
            // this shot is going to fire at, rather than whatever the finger last reported on its
            // way off the glass.
            _controller.SetExternalPower(_peakPower, _peakCurve);

            _sweepSec = NeedleMath.SweepSeconds(_controller.CharacterClubControl, _peakPower,
                                                _controller.OverpowerForgiveness01,
                                                _controller.IsPutt, _cfg);

            _handleReturnFrom = _handle != null ? _handle.anchoredPosition : _handleRest;
            _handleReturnT    = 0f;

            _circleView?.SetDimmed(true);
            _arcView?.ApplyState(_controller.State);
            _arcView?.SetNeedle(_needle);
            _arcView?.ShowTapPip(false, 0f);
            _arcView?.ShowTapHint(true);
            _tapCatcher?.SetArmed(true);
        }

        // ── Touch 2: the tap (or the SHANK timeout) ──────────────────────────────

        /// <summary>The second touch. Public so the tap catcher can forward to it and so an
        /// acceptance run can drive the real entry point rather than a test-only hook.</summary>
        public void OnTap()
        {
            if (_phase != Phase.Needle) return;
            float halfCone = _controller.ConeHalfAngleDeg * Mathf.Deg2Rad;
            Commit(NeedleMath.Grade(_needle, _controller.ClubAccuracyNorm01, _peakPower,
                                    halfCone, _cfg), _needle);
        }

        private void Commit(in NeedleMath.Verdict verdict, float n)
        {
            _phase = Phase.Idle;
            _tapCatcher?.SetArmed(false);

            _needle = Mathf.Clamp(n, -1f, 1f);
            _arcView?.SetNeedle(_needle);          // freeze it where it was read
            _arcView?.ShowTapPip(true, _needle);
            _arcView?.ShowTapHint(false);
            _gradePop?.Show(verdict.Grade);

            if (_logSwings)
                Debug.Log($"[Needle] n={_needle:F3} grade={verdict.Grade} power={_peakPower:F2} " +
                          $"sweepSec={_sweepSec:F2} errorYaw={verdict.ErrorYawRad:F4}rad " +
                          $"timingMul={verdict.TimingMul:F2} timing01={verdict.Timing01:F2} " +
                          $"curve={_peakCurve:F2}");

            LastCommittedPower    = _peakPower;
            LastCommittedNeedle   = _needle;
            LastCommittedGrade    = verdict.Grade;
            LastCommittedTimingMul = verdict.TimingMul;
            LastCommittedTiming01  = verdict.Timing01;
            LastCommittedErrorYawRad = verdict.ErrorYawRad;

            // The club head goes away with the ball: once the shot is committed the handle is not
            // a control any more, and leaving it under a departed ball reads as stuck UI. Alpha,
            // not SetActive — the copy carries a live ClubHandleSpriteBinder that subscribes in
            // OnEnable, and cycling the object would churn those subscriptions every shot.
            ShowHandle(false);

            // AimOffset01 is 0 by design: this scheme does not aim with the handle. In Straight the
            // aim is the camera heading; in FadeDraw it is the locked heading and the lateral pull
            // becomes the CURVE — both of which AimYawFor(0) already returns.
            _controller.CommitExternal(new ShotIntent(
                powerNormalized: _peakPower,
                aimOffset01:     0f,
                errorYawRad:     verdict.ErrorYawRad,
                timingMul:       verdict.TimingMul,
                timing01:        verdict.Timing01,
                fadeDraw01:      _peakCurve));

            // Deliberately NOT ResetSwing: the pip, the frozen needle and the pop are the result
            // display, and they stay up until the shot resolves back to Idle.
            _peakPower = 0f;
            _peakCurve = 0f;
        }

        // ── The sweep ────────────────────────────────────────────────────────────

        // A bot advances the needle itself, with a dt it chooses so the tap lands ON its sampled
        // offset instead of wherever the next frame boundary fell (see DriveBot). Update must not
        // then advance it a second time in the same frame.
        private bool _botDriving;

        private void Update() { if (!_botDriving) Advance(Time.deltaTime); }

        /// <summary>
        /// Move the needle one frame, and ease the club head home.
        ///
        /// <para>The needle crosses the arc ONCE. Running off the right end is not a miss the
        /// player can wait out — it is a <b>SHANK</b>, committed on the spot, which is what stops
        /// "do not tap" from being a way to abandon a swing for free the way the Pendulum's
        /// MaxSweeps cancel is.</para>
        /// </summary>
        private void Advance(float dt)
        {
            if (_controller == null) return;

            // Ease the club head back to the ball across the needle phase's opening frames.
            if (_handleReturnT < 1f && _handle != null)
            {
                _handleReturnT = Mathf.Clamp01(_handleReturnT + dt / Mathf.Max(_handleReturnSeconds, 1e-3f));
                _handle.anchoredPosition = Vector2.Lerp(_handleReturnFrom, _handleRest,
                                                        Mathf.SmoothStep(0f, 1f, _handleReturnT));
            }

            if (_phase != Phase.Needle) return;

            // The needle travels the full -1..+1 in SweepSeconds, hence the 2. CLAMPED PER FRAME:
            // a hitch is not the player's fault, and an unclamped dt teleports the needle across
            // the arc while they are watching it. The acceptance run caught a 0.21 s frame right
            // after the release — 43% of the arc in one step, which no reaction could survive.
            // Capping at a 30 fps step costs a slow device a fractionally longer sweep and buys
            // back the one thing a timing scheme cannot lose.
            _needle += 2f * Mathf.Min(dt, MaxNeedleStepSeconds) / Mathf.Max(_sweepSec, 1e-3f);

            if (_needle >= 1f)
            {
                float halfCone = _controller.ConeHalfAngleDeg * Mathf.Deg2Rad;
                Commit(NeedleMath.Shank(halfCone, _cfg), 1f);
                return;
            }

            _arcView?.SetNeedle(_needle);
        }

        // ── Handle ───────────────────────────────────────────────────────────────

        private void MoveHandle(float pullPx, float lateralPx, bool fadeDraw)
        {
            if (_handle == null) return;

            // Clamp to the deepest RING, not to some authored travel: the rings are drawn at
            // HandleRestBelowBall + the pull thresholds, so this clamp and those rings are the
            // same numbers and the club head cannot slide past the circle it is aiming at.
            float maxPull = _controller != null && _controller.IsPutt
                ? _cfg.NeedlePull100Px
                : _cfg.NeedlePull120Px;

            float y = _handleRest.y - Mathf.Clamp(pullPx, 0f, maxPull);
            float x = fadeDraw
                ? _handleRest.x + Mathf.Clamp(lateralPx, -_cfg.NeedleCurveHalfWidthPx,
                                                          _cfg.NeedleCurveHalfWidthPx)
                : _handleRest.x;
            _handle.anchoredPosition = new Vector2(x, y);
        }

        private Vector2 ToLocal(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _schemeRoot, e.position, e.pressEventCamera, out var local);
            return local;
        }

        // ── Reset ────────────────────────────────────────────────────────────────

        private void ResetSwing()
        {
            _dragging  = false;
            _phase     = Phase.Idle;
            _power     = 0f;
            _curve     = 0f;
            _peakPower = 0f;
            _peakCurve = 0f;
            _needle    = -1f;
            _handleReturnT = 1f;
            _tapCatcher?.SetArmed(false);
            RedrawZones();          // the next swing starts from the full-width target again
            // Deliberately does NOT touch the handle's alpha: this runs immediately after a commit
            // on the cancel paths too, and showing it again here would undo the hide in-frame.
            if (_handle != null) _handle.anchoredPosition = _handleRest;
        }

        /// <summary>The Idle half of the reset: put the chrome away once the ball has settled.</summary>
        private void ResetVisualsForNextSwing()
        {
            ResetSwing();
            _arcView?.HideImmediate();
            _circleView?.SetDimmed(false);
        }

        // ── Bot seam (bot_scheme_parity §3.2) ────────────────────────────────────

        /// <summary>
        /// Swing this scheme for a BOT: pull the club back inside the circle, release at
        /// <paramref name="power01"/>, then TAP the frame the live needle reaches
        /// <paramref name="targetNeedle01"/>.
        ///
        /// <para>THE TAP GOES THROUGH THE REAL <see cref="NeedleTapCatcher"/>. Not
        /// <see cref="OnTap"/> directly: the catcher is the object a thumb hits, it is armed and
        /// disarmed by the phase, and routing the bot around it would leave the one piece of this
        /// scheme that only ever runs under a real finger untested by every bot run. The direct
        /// call is kept only as a fallback for a driver whose catcher was never wired (EditMode),
        /// and it says so on the log when it fires.</para>
        ///
        /// <para>A BOT NEVER CHOOSES TO SHANK. The executor clamps its sample to ±0.98, so the
        /// needle is always tapped before it runs off the end — a shank is a player failing to
        /// act, not a skill level.</para>
        ///
        /// <para>The sub-frame stepping is the Pendulum's argument verbatim: the needle is
        /// advanced with a dt the bot chooses so the tap lands ON the sampled offset instead of
        /// up to a frame past it, because the difficulty calibration is a sigma on that offset.</para>
        /// </summary>
        public IEnumerator DriveBot(float power01, float targetNeedle01, float curve01,
                                    float rampSeconds, float commitTol01)
        {
            if (_controller == null) yield break;
            if (_controller.State != ShotState.Idle)
            {
                Debug.LogWarning($"[Needle] DriveBot: shot is {_controller.State}, not Idle — swing skipped.");
                yield break;
            }

            bool  isPutt    = _controller.IsPutt;
            float pullPx    = PullPxForPower(power01, isPutt);
            float lateralPx = Mathf.Clamp(curve01, -1f, 1f) * _cfg.NeedleCurveHalfWidthPx;
            float target    = Mathf.Clamp(targetNeedle01, -0.98f, 0.98f);
            float ramp      = Mathf.Max(1e-3f, rampSeconds);
            float tol       = Mathf.Max(1e-4f, commitTol01);

            BeginSwingLocal(Vector2.zero);
            _botDriving = true;

            // 1. The pull, inside the power circle. The needle does not exist yet — this scheme's
            //    first touch is power only, which is exactly what makes it two touches.
            float t = 0f;
            while (t < ramp)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / ramp);
                ProcessDragLocal(new Vector2(lateralPx * k, -pullPx * k));
                Advance(dt);
                yield return null;
            }
            ProcessDragLocal(new Vector2(lateralPx, -pullPx));

            // 2. The release: power is committed, the arc comes up and the needle starts.
            ReleasePower();
            if (_phase != Phase.Needle) { _botDriving = false; yield break; }   // pull too shallow

            // 3. Wait for the needle to reach the sampled offset, stepping exactly onto it when it
            //    is less than a frame away.
            // Wall-clock backstop, for the reason the Pendulum's has one: the needle reaching the
            // end of the arc is the real limit, but a bot must never be the thing that hangs a turn.
            float waited = 0f;
            const float MaxWaitSeconds = 20f;
            while (_phase == Phase.Needle)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > MaxWaitSeconds)
                {
                    Debug.LogWarning($"[Needle] DriveBot: needle never reached {target:F3} in " +
                                     $"{MaxWaitSeconds:F0}s (sweepSec={_sweepSec:F2}) — tapping at n={_needle:F3}.");
                    break;
                }
                float need = DtToNeedle(target);
                float step = Mathf.Min(Time.unscaledDeltaTime, need);
                Advance(step);
                if (_phase != Phase.Needle) break;              // ran off the end: a SHANK
                if (Mathf.Abs(_needle - target) <= tol) break;
                yield return null;
            }

            _botDriving = false;
            if (_phase != Phase.Needle) yield break;            // already committed (shank)

            // 4. The tap, through the object a thumb would have hit.
            if (_tapCatcher != null && _tapCatcher.IsArmed) _tapCatcher.OnPointerDown(null);
            if (_phase == Phase.Needle)
            {
                if (_tapCatcher != null && _tapCatcher.IsArmed)
                    Debug.LogWarning("[Needle] DriveBot: the tap catcher swallowed the tap (no subscriber) " +
                                     "— falling back to the driver's own OnTap.");
                OnTap();
            }
        }

        /// <summary>
        /// Seconds until the needle reaches <paramref name="target"/>, or +inf once it is already
        /// past. The needle is linear in time — <c>_needle += 2·dt/sweepSec</c> — so this is the
        /// exact inverse of <see cref="Advance"/>'s step.
        /// </summary>
        private float DtToNeedle(float target)
        {
            float remaining = target - _needle;
            if (remaining <= 0f) return float.PositiveInfinity;
            return remaining * Mathf.Max(_sweepSec, 1e-3f) * 0.5f;
        }

        /// <summary>
        /// The inverse of <c>NeedleMath.Power</c>: how far back a pull has to go to ask for
        /// <paramref name="power01"/>. A bot decides a POWER, but the driver — like the thumb —
        /// only understands pixels, and inverting here keeps the two curves one curve.
        /// </summary>
        private float PullPxForPower(float power01, bool isPutt)
        {
            float minPull = _cfg.NeedleMinUsefulPullPx;
            float p100    = _cfg.NeedlePull100Px;
            float p120    = _cfg.NeedlePull120Px;

            float p = Mathf.Clamp(power01, 0f, ShotController.MaxOverpowerNormalized);
            if (p <= 0f) return 0f;
            if (isPutt || p <= 1f) return minPull + Mathf.Clamp01(p) * (p100 - minPull);
            return p100 + ((p - 1f) / 0.2f) * (p120 - p100);
        }

        /// <summary>
        /// Grade a needle offset with THIS driver's live config and stats, without swinging. The
        /// bot executor needs it to map a candidate offset to the yaw it would produce so the tree
        /// probe can reject the ones that fly into a trunk — and it must be the same call
        /// <see cref="OnTap"/> makes, or the sampler would be clearing a different shot from the
        /// one that gets fired.
        /// </summary>
        public NeedleMath.Verdict GradeForBot(float n, float power01)
        {
            float acc      = _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f;
            float halfCone = (_controller != null ? _controller.ConeHalfAngleDeg : 0f) * Mathf.Deg2Rad;
            return NeedleMath.Grade(n, acc, power01, halfCone, _cfg);
        }

        /// <summary>The bot tap tolerance, straight off the config this driver is using — so the
        /// executor cannot pass a number the driver disagrees with.</summary>
        public float BotCommitTol01 => _cfg.NeedleBotCommitTol01;

        // ── Test seam ────────────────────────────────────────────────────────────

        /// <summary>
        /// EditMode wiring seam. A plain MonoBehaviour gets no Awake in EditMode, so a test that
        /// only assigned the serialized fields would drive an object that never started — the same
        /// argument <c>ShotSchemeHost.ConfigureForTests</c> makes. Also injects the config so a
        /// test can drive the whole table without touching the shipped tuning.
        /// </summary>
        public void ConfigureForTests(RectTransform schemeRoot, RectTransform handle,
                                      NeedlePowerCircleView circle, NeedleArcView arc,
                                      NeedleTapCatcher catcher, SchemeGradePop pop,
                                      in ControlsConfig cfg)
        {
            _schemeRoot = schemeRoot;
            _handle     = handle;
            _circleView = circle;
            _arcView    = arc;
            _tapCatcher = catcher;
            _gradePop   = pop;
            _cfg        = cfg;
            BindHandle();
        }

        /// <summary>EditMode seam: the same <c>Advance</c> Update calls, with an explicit dt.
        /// Deliberately the SAME method and not a copy — a test-only reimplementation of the sweep
        /// would be a test that passes while production drifts.</summary>
        public void TickForTests(float dt) => Advance(dt);

        /// <summary>EditMode seam: put the needle at a known offset so a test can assert the grade
        /// a tap produces without racing a real sweep.</summary>
        public void SetNeedleForTests(float n)
        {
            _needle = Mathf.Clamp(n, -1f, 1f);
            _arcView?.SetNeedle(_needle);
        }

        /// <summary>The last shot state this driver passed on to the arc's fading view — or
        /// <c>Flicking</c> as the sentinel for "nothing has been forwarded yet this run". Public so
        /// a test can pin the ROUTING RULE (the arc must not be told about Resolving) rather than
        /// an alpha, which no EditMode fixture can observe because Update never runs there.</summary>
        public ShotState LastStateForwardedToArc { get; private set; } = ShotState.Flicking;

        /// <summary>Live needle offset, −1 (left end) … +1 (right end).</summary>
        public float NeedleOffset => _needle;

        /// <summary>True while the needle is sweeping and a tap would be graded.</summary>
        public bool IsNeedlePhase => _phase == Phase.Needle;

        /// <summary>The power the shot WOULD commit at right now — the deepest point of the pull so
        /// far, not the live value. Zero between swings.</summary>
        public float PeakPower => _peakPower;

        /// <summary>Seconds the CURRENT sweep takes end to end. Tuning and acceptance evidence:
        /// "trackable by eye" is this number being ≥ 1.0 at Club Control 0.</summary>
        public float SweepSeconds => _sweepSec;

        /// <summary>What the last committed swing actually fired at. Survives the reset, so a test
        /// or a verification bot can read what happened AFTER the tap instead of catching it
        /// mid-gesture.</summary>
        public float       LastCommittedPower      { get; private set; }
        public float       LastCommittedNeedle     { get; private set; } = float.NaN;
        public NeedleGrade LastCommittedGrade      { get; private set; }
        public float       LastCommittedTimingMul  { get; private set; } = 1f;
        public float       LastCommittedTiming01   { get; private set; } = float.NaN;
        public float       LastCommittedErrorYawRad { get; private set; }

        /// <summary>The live pull thresholds, straight off the config the driver is using. Public
        /// so a verification run can assert the DRAWN ring sits where the CONFIG says instead of
        /// deriving the threshold from the ring and asserting a tautology.</summary>
        public float Pull80Px  => _cfg.NeedlePull80Px;
        public float Pull100Px => _cfg.NeedlePull100Px;
        public float Pull120Px => _cfg.NeedlePull120Px;

        /// <summary>The live accuracy windows, as fractions of the arc's 90° half-sweep.</summary>
        public float PerfectZone01 => NeedleMath.PerfectZone01(
            _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f, _peakPower, _cfg);
        public float GoodZone01 => NeedleMath.GoodZone01(
            _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f, _peakPower, _cfg);
    }
}
