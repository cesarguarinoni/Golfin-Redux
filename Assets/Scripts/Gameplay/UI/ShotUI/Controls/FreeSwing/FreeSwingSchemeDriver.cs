using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>
    /// The Free Swing scheme's input driver (scheme_freeswing §3.2). ONE continuous touch: pull
    /// the club head down the lane for power, then drag back up — and the shot fires the FRAME
    /// THE FINGER CROSSES THE IMPACT LINE, not on the release (decision 5).
    ///
    /// <para>THE RELEASE IS NOT AN EVENT IN THIS SCHEME. That is the whole design, and it is why
    /// <c>OnPointerUp</c> here is a CANCEL path and nothing else: firing on the lift would mean
    /// the shot happens after the swing has already visibly finished, and the player would be
    /// aiming at a moment they cannot see. Crossing a drawn line is a moment they can. Once a
    /// swing has committed, every further pointer event on that touch is ignored — exactly one
    /// commit per touch, enforced by <see cref="_committed"/> rather than hoped for.</para>
    ///
    /// <para>OWNS ITS TIMING, SO IT OWNS ITS RELEASE. <c>BeginExternalDrag(ownsTiming: true)</c>
    /// takes the flick's arrow, its per-pass degradation and its <c>MaxTotalPasses</c>
    /// auto-cancel off the table; the corollary is that it must never call
    /// <c>EndExternalDrag</c>, which would decide for itself using the arrow this driver just
    /// disabled. <c>CommitExternal</c> or <c>CancelExternalDrag</c>, once per swing.</para>
    ///
    /// <para>NO FLICK GATE, AND ITS OWN SAMPLE BUFFER. <c>ShotController.PushTouchSample</c> is
    /// Flick's gate ring — widening it to carry what this scheme measures would change the
    /// shipping scheme, which this track must never do. So the samples live here, capped at
    /// <c>FreeSwingSampleWindow</c>, and they carry the one thing the flick's ring does not: a
    /// timestamp per sample, because tempo is half of this scheme's verdict.</para>
    ///
    /// <para>ONE SEAM ADDITION, and it is in the UI: <c>ActionButtonsRoot.SetFadeDrawVisible</c>.
    /// The upstroke's own path shapes the shot here (decision 3), so a FADE/DRAW toggle would be
    /// a second and contradicting way to ask for the same thing. It is hidden by OPACITY —
    /// <c>SetActive(false)</c> would let the layout group re-centre SPIN. <c>ShotController.cs</c>
    /// has no diff at all.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class FreeSwingSchemeDriver : MonoBehaviour, IShotSchemeDriver,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        /// <summary>Which half of the one gesture the finger is on. Not derivable from
        /// <c>ShotController.State</c>: both halves are <c>ShotState.Timing</c>, and "is the
        /// player still loading or already swinging" is a question only the scheme can answer.</summary>
        private enum Phase { Idle, Back, Up }

        [Header("Wiring")]
        [Tooltip("SchemeRoot_FreeSwing's RectTransform — the space every pull, offset and trace " +
                 "point is measured in, so the maths and the drawn lane share one coordinate system.")]
        [SerializeField] private RectTransform _schemeRoot;

        [Tooltip("The club-head Image (a copy of ClubHandle, carrying ClubHandleSpriteBinder).")]
        [SerializeField] private RectTransform _handle;

        [SerializeField] private FreeSwingLaneView     _laneView;
        [SerializeField] private FreeSwingTraceView    _traceView;
        [SerializeField] private FreeSwingAnalyzerChip _analyzerChip;
        [SerializeField] private SchemeGradePop        _gradePop;

        [Tooltip("The bottom button row that owns the FADE/DRAW toggle. Hidden while this scheme " +
                 "is live — see the class remarks.")]
        [SerializeField] private ActionButtonsRoot _actionButtons;

        [Header("Feel")]
        [Tooltip("Lateral travel the club head is allowed either side of the lane centre. The " +
                 "path IS the point in this scheme, so the club follows the finger sideways too — " +
                 "but not out of its own pill.")]
        [SerializeField] private float _handleLateralClampPx = 70f;
        [Tooltip("Seconds the club head takes to ease back to rest after a cancelled swing.")]
        [SerializeField] private float _handleReturnSeconds = 0.15f;

        [Header("Debug")]
        [Tooltip("One line per committed swing: impact px, path deg, tempo ratio, up speed, grade.")]
        [SerializeField] private bool _logSwings;

        private ShotController _controller;
        private ControlsConfig _cfg = ControlsConfig.Default;

        /// <summary>
        /// The clock every time-based measure reads. Injectable because tempo and the duff
        /// threshold are SECONDS, and <c>Time.unscaledTime</c> does not advance in EditMode — a
        /// test that could not drive the clock could not test half this scheme at all.
        /// </summary>
        private Func<float> _clock = () => Time.unscaledTime;

        private Phase   _phase = Phase.Idle;
        private bool    _dragging;
        private bool    _committed;
        private Vector2 _origin;
        private Vector2 _prevPos;
        private float   _tLastSample;

        private float   _peakPull;
        private float   _peakPower;

        private Vector2 _reversalPos;
        private float   _backSeconds;
        private float   _upSeconds;
        private float   _upLengthPx;

        // The driver's OWN buffers. `_samples` is what the trace draws; `_upSamples` is what the
        // path maths reads. Two, and not one with an index, because the trace buffer evicts its
        // oldest entry at the cap and an index into it would silently rot.
        private readonly List<Vector2> _samples   = new List<Vector2>(128);
        private readonly List<Vector2> _upSamples = new List<Vector2>(128);

        private Vector2 _handleRest;
        private Vector2 _handleReturnFrom;
        private float   _handleReturnT = 1f;
        private CanvasGroup _handleGroup;

        // ── IShotSchemeDriver ────────────────────────────────────────────────────

        public ControlScheme Scheme        => ControlScheme.FreeSwing;
        public bool          IsImplemented => true;

        public void Bind(ShotController controller) => _controller = controller;

        public void Activate()
        {
            ResetSwing();
            ShowHandle(true);
            ApplyLayout();
            HideFadeDrawToggle();
            if (_analyzerChip != null) _analyzerChip.SetHoldSeconds(_cfg.FreeSwingAnalyzerSeconds);
            if (_controller != null) _controller.OnStateChanged += HandleStateChanged;
        }

        public void Deactivate()
        {
            if (_controller != null) _controller.OnStateChanged -= HandleStateChanged;
            RestoreFadeDrawToggle();
            ResetSwing();
            ResetVisualsForNextSwing();
        }

        private void Awake()
        {
            if (_schemeRoot == null) _schemeRoot = transform as RectTransform;
            BindHandle();
        }

        // OnDisable, not only Deactivate: the host turns the ROOT off when the player switches
        // scheme, and a root that goes inactive never gets Deactivate called on its children.
        private void OnDisable()
        {
            if (_controller != null) _controller.OnStateChanged -= HandleStateChanged;
            RestoreFadeDrawToggle();
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

        // ── The Fade/Draw toggle ─────────────────────────────────────────────────

        /// <summary>
        /// Take the FADE/DRAW button off the screen, and DISARM the mode if it was armed.
        ///
        /// <para>Disarmed through <c>ShotModeContext.Toggle()</c> — the existing path — rather
        /// than by writing <c>ShotController.FadeDrawActive</c> directly, because that flag is
        /// only half of arming: <c>ShotConeView.OnShotModeChanged</c> also owns
        /// <c>FadeDrawLockedAimRad</c>, and clearing one without the other would leave
        /// <c>AimYawFor</c> reading a stale locked heading for every Free Swing shot while the
        /// toggle the player could use to fix it was invisible.</para>
        /// </summary>
        private void HideFadeDrawToggle()
        {
            if (ShotModeContext.Mode == ShotMode.FadeDraw) ShotModeContext.Toggle();
            _actionButtons?.SetFadeDrawVisible(false);
        }

        private void RestoreFadeDrawToggle() => _actionButtons?.SetFadeDrawVisible(true);

        // ── Layout ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Push the live stats into the drawn lane. Called at Activate and again at every
        /// pointer-down, because the club (and therefore the impact window, and whether this is a
        /// putt at all) can change between two swings but never during one.
        /// </summary>
        private void ApplyLayout()
        {
            bool isPutt = _controller != null && _controller.IsPutt;
            _laneView?.ApplyGeometry(_cfg, isPutt);
            _laneView?.RefreshLabels();
            RedrawImpactWindow();
        }

        /// <summary>
        /// Size the green window for the power the shot WOULD commit at right now — from the PEAK
        /// pull, every drag frame, so the target closes as the player pulls and the target they
        /// watched close is the one they are graded against (carry-over 2).
        /// </summary>
        private void RedrawImpactWindow()
        {
            if (_laneView == null) return;
            float acc = _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f;
            _laneView.ApplyImpactWindow(FreeSwingMath.ImpactWindowPx(acc, _peakPower, _cfg));
        }

        private void HandleStateChanged(ShotInputState state)
        {
            // The LANE may fade at Resolving — it is a power gauge, and stale the moment the ball
            // is in the air. The CHIP and the TRACE must not, which is why neither is driven from
            // here: CommitExternal reaches Resolving synchronously, and forwarding it would drop
            // the result readout about two frames after the shot (carry-over 7, Needle §10).
            _laneView?.ApplyState(state.State);
            _traceView?.ApplyState(state.State);

            if (state.State == ShotState.Idle) ResetVisualsForNextSwing();

            // The club head comes BACK here, and only here. Hidden at commit rather than on a
            // Flicking event, because ShotController does not PublishState on the Idle->Flicking
            // transition — waiting for a Flicking state that never arrives would leave the handle
            // under a ball that has already gone (the scar the Pendulum test pins).
            if (state.State != ShotState.Flicking && state.State != ShotState.Resolving)
                ShowHandle(true);
        }

        // ── The one touch ────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            if (_controller == null || _schemeRoot == null) return;
            if (_controller.State != ShotState.Idle) return;

            BeginSwingLocal(ToLocal(e));
        }

        /// <summary>
        /// Open a swing at a point in the scheme root's LOCAL space. Split out of
        /// <see cref="OnPointerDown"/> so <see cref="DriveBot"/> can start the identical swing
        /// without a <c>PointerEventData</c> — a bot has no camera and no press position for
        /// <c>RectTransformUtility</c> to project, but it needs every field this method resets,
        /// in the order it resets them, and it needs the clock zeroed at the same instant.
        /// </summary>
        private void BeginSwingLocal(Vector2 origin)
        {
            _dragging     = true;
            _committed    = false;
            _phase        = Phase.Back;
            _origin       = origin;
            _prevPos      = _origin;
            _reversalPos  = _origin;
            _peakPull     = 0f;
            _peakPower    = 0f;
            _backSeconds  = 0f;
            _upSeconds    = 0f;
            _upLengthPx   = 0f;
            _handleReturnT = 1f;
            _tLastSample  = _clock();

            _samples.Clear();
            _upSamples.Clear();
            PushSample(_origin);

            ApplyLayout();
            _gradePop?.HideImmediate();
            _analyzerChip?.HideImmediate();
            _traceView?.SetPoints(_samples);
            _traceView?.SetSwinging();
            ShowHandle(true);

            // Same order ClubHandleDragger and the other two drivers use, and for the same
            // reason: BeginExternalDrag resets the swing (which clears PendingSpinInput), so the
            // HUD's spin has to be pushed after it.
            _controller.BeginExternalDrag(ownsTiming: true);
            _controller.PendingSpinInput = SpinContext.Spin;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging || _committed) return;
            ProcessDrag(ToLocal(e));
        }

        /// <summary>
        /// One sample of the gesture. Public so an acceptance run and the video recorder can drive
        /// the REAL path — the same method the pointer events call — rather than a test-only hook
        /// that could pass while production drifts.
        /// </summary>
        public void ProcessDrag(Vector2 pos)
        {
            if (!_dragging || _committed || _controller == null) return;

            float t  = _clock();
            // CLAMPED (carry-over 9). A hitch is not the player's fault, and this scheme measures
            // two things in seconds. An unclamped 0.4 s frame mid-upswing would report a swing
            // three times slower than the thumb actually moved and turn a good shot into a duff.
            float dt = Mathf.Clamp(t - _tLastSample, 0f, FreeSwingMath.MaxStepSeconds);
            _tLastSample = t;

            PushSample(pos);
            _traceView?.SetPoints(_samples);

            bool  isPutt = _controller.IsPutt;
            float pull   = _origin.y - pos.y;          // + = the finger is BELOW where it started

            if (_phase == Phase.Back)
            {
                _backSeconds += dt;

                if (pull > _peakPull) _peakPull = pull;
                _peakPower = FreeSwingMath.Power(_peakPull, _cfg, isPutt);

                // The PEAK, not the live pull. In this scheme the finger comes back UP through
                // the whole gesture, so a live reading would walk the gauge, the map ring and the
                // putt predictor back down to zero on the way to the shot.
                _controller.SetExternalPower(_peakPower, 0f);
                RedrawImpactWindow();
                MoveHandle(pull, pos.x - _origin.x);

                // The reversal: the first sample that moves back UP the screen, once the pull is
                // deep enough to have been a backswing at all. The MinUsefulPullPx floor is what
                // stops a thumb that twitches on touch-down from arming the upswing at 0% power.
                if (pos.y > _prevPos.y && _peakPull >= _cfg.FreeSwingMinUsefulPullPx)
                {
                    _phase       = Phase.Up;
                    _reversalPos = pos;
                    _upSeconds   = 0f;
                    _upLengthPx  = 0f;
                    _upSamples.Clear();
                }
            }
            else if (_phase == Phase.Up)
            {
                _upSeconds  += dt;
                _upLengthPx += (pos - _prevPos).magnitude;
                PushUpSample(pos);

                // A SECOND BACKSWING, not thumb noise. The slop is the whole difference between a
                // deliberate double pump — one shot, at the deeper power — and a shaky upstroke
                // that would otherwise keep re-arming and never produce a tempo at all.
                if (pos.y < _reversalPos.y - _cfg.FreeSwingReversalSlopPx)
                {
                    _phase      = Phase.Back;
                    _upSeconds  = 0f;
                    _upLengthPx = 0f;
                    _upSamples.Clear();
                    // _peakPull is deliberately KEPT: the deeper of the two pulls is the one the
                    // player asked for, and resetting it would punish a double pump.
                }
                else if (pos.y - _origin.y >= ImpactCrossOffsetPx)
                {
                    // CROSSED. Interpolate where between the last two samples the crossing
                    // actually happened, rather than taking this frame's x: at 500 px/s a 16 ms
                    // frame is 8 px of travel, which is a third of the tightest impact window.
                    Commit(CrossingPoint(_prevPos, pos));
                    return;
                }

                MoveHandle(pull, pos.x - _origin.x);
            }

            _prevPos = pos;
        }

        /// <summary>
        /// How far above its touch origin the finger must travel for the club head to reach the
        /// impact line. Read off the LANE, so the drawn line and the graded crossing are one
        /// number — see <see cref="FreeSwingLaneView.ImpactCrossOffsetPx"/> for why it is not 0.
        /// </summary>
        public float ImpactCrossOffsetPx =>
            _laneView != null ? _laneView.ImpactCrossOffsetPx : 0f;

        /// <summary>Linear interpolation of the crossing between the two straddling samples,
        /// returned as the club head's lateral offset from the lane centre in canvas px —
        /// positive to the player's RIGHT, which is the sign <c>FreeSwingMath.ImpactYawRad</c>
        /// reads.</summary>
        private float CrossingPoint(Vector2 before, Vector2 after)
        {
            float target = _origin.y + ImpactCrossOffsetPx;
            float span   = after.y - before.y;
            float k      = span > 1e-4f ? Mathf.Clamp01((target - before.y) / span) : 1f;
            float x      = Mathf.Lerp(before.x, after.x, k);
            return x - _origin.x;
        }

        /// <summary>
        /// The lift. In this scheme it is ONLY a cancel: a finger that goes up without crossing
        /// the line never took a shot, and a finger lifted after the crossing is lifting off a
        /// shot that already fired.
        /// </summary>
        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;

            if (_committed) return;      // the release after a commit is not an event

            _controller?.CancelExternalDrag();
            _handleReturnFrom = _handle != null ? _handle.anchoredPosition : _handleRest;
            _handleReturnT    = 0f;
            _phase            = Phase.Idle;
            _traceView?.SetResult();     // the trace fades out with the cancelled swing
        }

        private void Commit(float impactPx)
        {
            _committed = true;
            _dragging  = false;
            _phase     = Phase.Idle;

            bool  isPutt   = _controller.IsPutt;
            float halfCone = _controller.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float acc      = _controller.ClubAccuracyNorm01;
            float cc       = ClubControlNorm01;

            float pathDeg = FreeSwingMath.PathDeg(_reversalPos,
                                                  new Vector2(_origin.x + impactPx,
                                                              _origin.y + ImpactCrossOffsetPx),
                                                  _upSamples);
            float ratio   = FreeSwingMath.TempoRatio(_backSeconds, _upSeconds);
            float speed   = FreeSwingMath.UpSpeed(_upLengthPx, _upSeconds);

            var v = FreeSwingMath.Grade(impactPx, pathDeg, ratio, speed, _peakPower,
                                        acc, cc, halfCone, isPutt, _cfg);

            // Republish at the PEAK before committing. Everything downstream of the seam — the
            // power gauge, the putt path predictor, the map ring — must be showing the power this
            // shot is going to fire at rather than whatever the finger last reported on its way
            // through the impact line (Needle §1 carry-over).
            _controller.SetExternalPower(_peakPower, 0f);

            // The club head goes away with the ball. Alpha, not SetActive — the copy carries a
            // live ClubHandleSpriteBinder that subscribes in OnEnable, and cycling the object
            // would churn those subscriptions every shot.
            ShowHandle(false);
            _traceView?.SetResult();
            _analyzerChip?.Show(v);
            _gradePop?.Show(v.Grade);

            if (_logSwings)
                Debug.Log($"[FreeSwing] impact={impactPx:F1}px window={v.ImpactWindowPx:F1} " +
                          $"path={pathDeg:F2}deg fadeDraw={v.FadeDraw01:F2} " +
                          $"tempo={ratio:F2} (back={_backSeconds:F2}s up={_upSeconds:F2}s) " +
                          $"speed={speed:F0}px/s power={_peakPower:F2} grade={v.Grade} " +
                          $"errorYaw={v.ErrorYawRad:F4}rad timingMul={v.TimingMul:F2}");

            LastVerdict           = v;
            LastCommittedPower    = _peakPower;
            LastCommittedBackSeconds = _backSeconds;
            LastCommittedUpSeconds   = _upSeconds;
            CommitCount++;

            // AimOffset01 is 0 by design: this scheme does not aim with the handle. The aim is
            // the camera heading (the Fade/Draw lock is disarmed at Activate), and the lateral
            // information the handle carries becomes the IMPACT error and the PATH curve instead.
            _controller.CommitExternal(new ShotIntent(
                powerNormalized: _peakPower,
                aimOffset01:     0f,
                errorYawRad:     v.ErrorYawRad,
                timingMul:       v.TimingMul,
                timing01:        v.Timing01,
                fadeDraw01:      v.FadeDraw01));

            // Deliberately NOT ResetSwing: the trace, the chip and the pop ARE the result
            // display, and they stay up until the shot resolves back to Idle.
            _peakPull  = 0f;
            _peakPower = 0f;
        }

        // ── Buffers ──────────────────────────────────────────────────────────────

        private void PushSample(Vector2 pos) => Push(_samples, pos);
        private void PushUpSample(Vector2 pos) => Push(_upSamples, pos);

        /// <summary>Append, evicting the oldest past the configured window. A cap and not an
        /// unbounded list because a finger can rest on the glass for a minute, and an
        /// ever-growing polyline would rebuild an ever-growing mesh every frame.</summary>
        private void Push(List<Vector2> buffer, Vector2 pos)
        {
            buffer.Add(pos);
            int cap = Mathf.Max(8, Mathf.RoundToInt(_cfg.FreeSwingSampleWindow));
            while (buffer.Count > cap) buffer.RemoveAt(0);
        }

        // ── Handle ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The club head follows the finger, VERTICALLY AND LATERALLY.
        ///
        /// <para>Laterally too, unlike Pendulum and Needle, and that is not a flourish: in this
        /// scheme the lateral position at the crossing IS the impact and the shape of the path IS
        /// the curve, so a club head that only moved up and down would be hiding the two things
        /// the player is being graded on.</para>
        /// </summary>
        private void MoveHandle(float pullPx, float lateralPx)
        {
            if (_handle == null) return;

            // Clamped to the lane, not to some authored travel: the deepest tick and this clamp
            // are the same config field, so the club head cannot slide past the line it is
            // aiming at — nor past the impact line it is swinging up to.
            float rest    = _laneView != null ? _laneView.HandleRestBelowBall : 70f;
            float maxPull = (_controller != null && _controller.IsPutt)
                ? _cfg.FreeSwingPull100Px
                : _cfg.FreeSwingPull120Px;
            float minPull = -(rest + _cfg.FreeSwingFollowThroughPx);   // the follow-through above the ball

            float y = _handleRest.y - Mathf.Clamp(pullPx, minPull, maxPull);
            float x = _handleRest.x + Mathf.Clamp(lateralPx, -_handleLateralClampPx, _handleLateralClampPx);
            _handle.anchoredPosition = new Vector2(x, y);
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Ease the club head home after a cancel. The same method
        /// <see cref="TickForTests"/> drives, so a test cannot pass against a copy.</summary>
        private void Tick(float dt)
        {
            if (_handleReturnT >= 1f || _handle == null) return;
            _handleReturnT = Mathf.Clamp01(_handleReturnT + dt / Mathf.Max(_handleReturnSeconds, 1e-3f));
            _handle.anchoredPosition = Vector2.Lerp(_handleReturnFrom, _handleRest,
                                                    Mathf.SmoothStep(0f, 1f, _handleReturnT));
        }

        private Vector2 ToLocal(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _schemeRoot, e.position, e.pressEventCamera, out var local);
            return local;
        }

        /// <summary>Character Club Control as 0..1 against the 120 the config's <c>*AtCC120</c>
        /// keys are named for. Its own normalisation, and not <c>ClubAccuracyNorm01</c>: in this
        /// scheme Accuracy buys lateral tolerance at impact while Control buys tolerance of a
        /// shaky thumb, and conflating them would make one stat do both jobs.</summary>
        private float ClubControlNorm01 =>
            _controller != null ? Mathf.Clamp01(_controller.CharacterClubControl / 120f) : 0.5f;

        // ── Reset ────────────────────────────────────────────────────────────────

        private void ResetSwing()
        {
            _dragging   = false;
            _committed  = false;
            _phase      = Phase.Idle;
            _peakPull   = 0f;
            _peakPower  = 0f;
            _backSeconds = _upSeconds = _upLengthPx = 0f;
            _samples.Clear();
            _upSamples.Clear();
            _handleReturnT = 1f;
            RedrawImpactWindow();      // the next swing starts from the full-width window again
            // Deliberately does NOT touch the handle's alpha: this runs immediately after a
            // commit on some paths, and showing it again here would undo the hide in-frame.
            if (_handle != null) _handle.anchoredPosition = _handleRest;
        }

        /// <summary>The Idle half of the reset: put the chrome away once the ball has settled.</summary>
        private void ResetVisualsForNextSwing()
        {
            ResetSwing();
            _laneView?.HideImmediate();
            _traceView?.HideImmediate();
            _analyzerChip?.HideImmediate();
        }

        // ── Bot seam (bot_scheme_parity §3.2) ────────────────────────────────────

        /// <summary>
        /// Swing this scheme for a BOT by feeding SYNTHETIC SAMPLES into the driver's own buffer
        /// in real time: straight back to the depth <paramref name="power01"/> asks for, then
        /// straight up, crossing the impact line at <paramref name="impactOffsetPx"/> at the
        /// sampled <paramref name="tempoRatio"/>.
        ///
        /// <para>SYNTHETIC SAMPLES, NOT A SYNTHETIC RESULT. Every point goes through the public
        /// <see cref="ProcessDrag"/> — the same method the pointer events call — so the trace
        /// draws, the reversal is detected the normal way, the tempo is measured off the real
        /// clock and the shot fires on the driver's own impact-line crossing. The analyzer chip
        /// the player reads afterwards is the driver's own verdict on a swing it genuinely
        /// watched happen.</para>
        ///
        /// <para>THE PATH IS A STRAIGHT CHORD from the reversal to the crossing, so
        /// <c>FreeSwingMath.PathDeg</c> measures zero bow and <c>FadeDraw01</c> is 0: bots never
        /// shape a shot. The lateral information they DO carry is the impact offset, which is the
        /// scheme's aim error.</para>
        ///
        /// <para>AND THEY NEVER DUFF. The upstroke duration is derived from
        /// <paramref name="upSpeedPxPerSec"/> and the path's own length rather than being an
        /// independent knob, so the speed the driver measures is the speed asked for. A duff is a
        /// thumb failing to move, not a golfer lacking skill, and putting one in the difficulty
        /// model would make low-level bots fail in a way no human ever fails on purpose.</para>
        /// </summary>
        public IEnumerator DriveBot(float power01, float impactOffsetPx, float tempoRatio,
                                    float upSpeedPxPerSec)
        {
            if (_controller == null) yield break;
            if (_controller.State != ShotState.Idle)
            {
                Debug.LogWarning($"[FreeSwing] DriveBot: shot is {_controller.State}, not Idle — swing skipped.");
                yield break;
            }

            bool  isPutt = _controller.IsPutt;
            float pullPx = PullPxForPower(power01, isPutt);

            Vector2 reversal = new Vector2(0f, -pullPx);
            Vector2 crossing = new Vector2(impactOffsetPx, ImpactCrossOffsetPx);
            float   lengthPx = (crossing - reversal).magnitude;

            // Solve the two durations from the two things the error model actually sampled: the
            // up-speed fixes how long the upstroke takes, and the tempo ratio then fixes the
            // backswing. Driving them the other way round would let a long pull silently turn
            // into a duff.
            float upSeconds   = lengthPx / Mathf.Max(upSpeedPxPerSec, 1f);
            float backSeconds = upSeconds / Mathf.Max(tempoRatio, 1e-3f);

            BeginSwingLocal(Vector2.zero);

            // 1. Straight back. Accumulated with the SAME per-frame clamp the driver applies to
            //    its own dt, so the seconds this loop counts are the seconds the driver counts.
            float acc  = 0f;
            float prev = Time.unscaledTime;
            while (acc < backSeconds && _dragging && !_committed)
            {
                yield return null;
                float now = Time.unscaledTime;
                acc  += Mathf.Clamp(now - prev, 0f, FreeSwingMath.MaxStepSeconds);
                prev  = now;
                ProcessDrag(new Vector2(0f, -pullPx * Mathf.Clamp01(acc / backSeconds)));
            }
            if (_committed || !_dragging) yield break;

            // 2. Arm the upswing at (essentially) the reversal itself, in the same frame, so the
            //    upstroke's measured seconds start at 0 rather than one frame in — a frame of
            //    upswing charged to the backswing is a few percent off the tempo the model asked
            //    for, and tempo is half this scheme's verdict.
            ProcessDrag(Vector2.Lerp(reversal, crossing, 0.002f));

            // 3. Straight up along the chord, through the impact line. ProcessDrag commits the
            //    frame it crosses, interpolating the crossing between the two straddling samples
            //    — both of which are ON the chord, so the impact it reads is exactly the offset
            //    that was sampled.
            acc  = 0f;
            prev = Time.unscaledTime;
            while (!_committed && _dragging)
            {
                yield return null;
                float now = Time.unscaledTime;
                acc  += Mathf.Clamp(now - prev, 0f, FreeSwingMath.MaxStepSeconds);
                prev  = now;
                float u = acc / Mathf.Max(upSeconds, 1e-3f);
                ProcessDrag(Vector2.LerpUnclamped(reversal, crossing, u));
                if (u > 1.5f)
                {
                    Debug.LogWarning("[FreeSwing] DriveBot: the impact line was never crossed — cancelling.");
                    _controller.CancelExternalDrag();
                    _dragging = false;
                    yield break;
                }
            }
        }

        /// <summary>
        /// The inverse of <c>FreeSwingMath.Power</c>: how deep the backswing has to go to ask for
        /// <paramref name="power01"/>. A bot decides a POWER, but the driver — like the thumb —
        /// only understands pixels, and inverting here keeps the two curves one curve.
        /// </summary>
        private float PullPxForPower(float power01, bool isPutt)
        {
            float minPull = _cfg.FreeSwingMinUsefulPullPx;
            float p100    = _cfg.FreeSwingPull100Px;
            float p120    = _cfg.FreeSwingPull120Px;

            float p = Mathf.Clamp(power01, 0f, ShotController.MaxOverpowerNormalized);
            // Never shallower than the reversal floor: below FreeSwingMinUsefulPullPx the upswing
            // is not armed at all and the "swing" would sit on the glass until it timed out.
            if (isPutt || p <= 1f)
                return Mathf.Max(minPull, minPull + Mathf.Clamp01(p) * (p100 - minPull));
            return p100 + ((p - 1f) / 0.2f) * (p120 - p100);
        }

        /// <summary>
        /// Map a candidate impact offset to the yaw it would produce, with THIS driver's live
        /// config and stats. The bot executor needs it so the tree probe can reject the samples
        /// that fly into a trunk — and it must be the same call <see cref="Commit"/> reaches
        /// through <c>FreeSwingMath.Grade</c>, or the sampler would be clearing a different shot
        /// from the one that gets fired.
        /// </summary>
        public float ImpactYawRadForBot(float impactPx, float power01)
        {
            float acc      = _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f;
            float halfCone = (_controller != null ? _controller.ConeHalfAngleDeg : 0f) * Mathf.Deg2Rad;
            return FreeSwingMath.ImpactYawRad(impactPx, acc, power01, halfCone, _cfg);
        }

        /// <summary>The error model's two scales, straight off the config this driver is using:
        /// the impact miss range the sigma is expressed in, the ideal tempo it is centred on, and
        /// the duff speed a bot doubles to stay clear of.</summary>
        public float ImpactMissPx => _cfg.FreeSwingImpactMissPx;
        public float IdealTempo   => _cfg.FreeSwingIdealTempo;

        /// <summary>The tempo tolerance for a power the bot has not swung yet. The public
        /// <see cref="TempoWindow"/> reads <c>_peakPower</c>, which is 0 between swings — asking
        /// it before the pull would size the window for a shot nobody is taking.</summary>
        public float TempoWindowForBot(float power01)
            => FreeSwingMath.TempoWindow(ClubControlNorm01, power01, _cfg);

        /// <summary>The duff floor the bot doubles. See <see cref="DriveBot"/>.</summary>
        public float DuffSpeedForBot => _cfg.FreeSwingDuffSpeedPxPerSec;

        // ── Test / acceptance seams ──────────────────────────────────────────────

        /// <summary>
        /// EditMode wiring seam. A plain MonoBehaviour gets no Awake in EditMode, so a test that
        /// only assigned the serialized fields would drive an object that never started — the
        /// same argument <c>ShotSchemeHost.ConfigureForTests</c> makes. Also injects the config
        /// and the CLOCK, without which no test could drive tempo at all.
        /// </summary>
        public void ConfigureForTests(RectTransform schemeRoot, RectTransform handle,
                                      FreeSwingLaneView lane, FreeSwingTraceView trace,
                                      FreeSwingAnalyzerChip chip, SchemeGradePop pop,
                                      ActionButtonsRoot actionButtons,
                                      in ControlsConfig cfg, Func<float> clock = null)
        {
            _schemeRoot    = schemeRoot;
            _handle        = handle;
            _laneView      = lane;
            _traceView     = trace;
            _analyzerChip  = chip;
            _gradePop      = pop;
            _actionButtons = actionButtons;
            _cfg           = cfg;
            if (clock != null) _clock = clock;
            BindHandle();
        }

        /// <summary>EditMode seam: the same <c>Tick</c> Update calls, with an explicit dt.</summary>
        public void TickForTests(float dt) => Tick(dt);

        /// <summary>The verdict the last committed swing fired with. Survives the reset, so a
        /// test or a verification bot reads what happened AFTER the shot rather than racing the
        /// gesture.</summary>
        public FreeSwingMath.Verdict LastVerdict { get; private set; }

        /// <summary>How many swings this driver has committed. The assertion behind "exactly one
        /// commit per touch" — a count, not a hope.</summary>
        public int CommitCount { get; private set; }

        public float LastCommittedPower       { get; private set; }
        public float LastCommittedBackSeconds { get; private set; }
        public float LastCommittedUpSeconds   { get; private set; }

        /// <summary>The power the shot WOULD commit at right now — the deepest point of the pull
        /// so far, not the live value. Zero between swings.</summary>
        public float PeakPower => _peakPower;
        public float PeakPullPx => _peakPull;

        /// <summary>True once the finger has reversed and the upswing is live.</summary>
        public bool IsUpstroke => _phase == Phase.Up;
        public bool IsBackswing => _phase == Phase.Back;

        /// <summary>The samples the trace is drawing, and the upstroke the path is measured from.
        /// Read-only views, so an acceptance dump can report them without being able to inject.</summary>
        public IReadOnlyList<Vector2> Samples   => _samples;
        public IReadOnlyList<Vector2> UpSamples => _upSamples;

        /// <summary>The live pull thresholds and windows, straight off the config the driver is
        /// using. Public so a verification run asserts the DRAWN geometry against the CONFIG
        /// instead of deriving the threshold from the drawing and asserting a tautology.</summary>
        public float MinUsefulPullPx   => _cfg.FreeSwingMinUsefulPullPx;
        public float Pull100Px         => _cfg.FreeSwingPull100Px;
        public float Pull120Px         => _cfg.FreeSwingPull120Px;
        public float FollowThroughPx   => _cfg.FreeSwingFollowThroughPx;
        public float DuffSpeedPxPerSec => _cfg.FreeSwingDuffSpeedPxPerSec;
        public float ImpactWindowPx => FreeSwingMath.ImpactWindowPx(
            _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f, _peakPower, _cfg);
        public float TempoWindow => FreeSwingMath.TempoWindow(ClubControlNorm01, _peakPower, _cfg);
    }
}
