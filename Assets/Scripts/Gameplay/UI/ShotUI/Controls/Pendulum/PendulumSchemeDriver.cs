using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.Controls.Pendulum
{
    /// <summary>
    /// The Pendulum scheme's input driver (scheme_pendulum §3.2): pull the club head straight
    /// down for power, flick up when the swinging marker is on the red pip.
    ///
    /// <para>OWNS ITS TIMING, SO IT OWNS ITS RELEASE. It opens the swing with
    /// <c>BeginExternalDrag(ownsTiming: true)</c>, which takes the flick's arrow, its per-pass
    /// degradation and its <c>MaxTotalPasses</c> auto-cancel off the table — none of those mean
    /// anything to a marker on a bar. The corollary is that it must never call
    /// <c>EndExternalDrag</c>: that method decides for itself whether to commit, using the arrow
    /// this driver just disabled. It calls <c>CommitExternal</c>, <c>CancelExternalDrag</c> or
    /// <c>RejectExternalDrag</c> instead, one per release, always exactly one.</para>
    ///
    /// <para>THE THREE POINTER HANDLERS ARE THE SAME THREE <c>ClubHandleDragger</c> USES, on a
    /// copy of the same <c>ClubHandle</c> object with the same <c>ClubHandleSpriteBinder</c>. The
    /// player is touching the same thing in both schemes and the club they are holding paints
    /// itself the same way; only what the drag MEANS is different.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PendulumSchemeDriver : MonoBehaviour, IShotSchemeDriver,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Wiring")]
        [Tooltip("SchemeRoot_Pendulum's RectTransform — the space pull/curve pixels are measured in, " +
                 "so the maths and the drawn lane share one coordinate system.")]
        [SerializeField] private RectTransform _schemeRoot;

        [Tooltip("The club-head Image (a copy of ClubHandle, carrying ClubHandleSpriteBinder).")]
        [SerializeField] private RectTransform _handle;

        [SerializeField] private PendulumLaneView  _laneView;
        [SerializeField] private PendulumBarView   _barView;
        [SerializeField] private SchemeGradePop    _gradePop;

        [Header("Debug")]
        [Tooltip("One line per committed swing: marker offset, grade, Hz, power. Tuning aid.")]
        [SerializeField] private bool _logSwings;

        private ShotController _controller;
        private ControlsConfig _cfg = ControlsConfig.Default;

        private bool    _dragging;
        private Vector2 _originLocal;
        private float   _phase;
        private int     _sweeps;
        private float   _power;
        private float   _curve;
        private float   _hz;
        private Vector2 _handleRest;
        private CanvasGroup _handleGroup;

        // ── What the swing is JUDGED on (not what the finger is doing at release) ────
        //
        // Both of these exist for the same reason ClubHandleDragger carries _peakPower and
        // ShotController carries _timingAtLatch: the up-flick is PART OF THE GESTURE, and during
        // it the finger travels back past its own origin. Read live at OnPointerUp, `pullPx`
        // is 0, power is 0 and the swing cancels silently — the player pulls to 100%, flicks
        // perfectly, and nothing happens. (Found by driving the real pointer path end to end;
        // every EditMode test passed because a test releases without a real upswing.)
        private float _peakPower;
        private float _peakCurve;

        /// <summary>Marker offset snapshotted at the upswing REVERSAL, or NaN if none happened.
        /// Sampling at pointer-up instead would cost the player the 50–150 ms their thumb takes
        /// to leave the glass — at ~2 Hz that is 10–30% of a sweep, which is a whole band. This
        /// is the identical argument, and the identical latch, that <c>_timingAtLatch</c> makes
        /// for the flick's arrow (shot_timing_power F15 D1).</summary>
        private float _markerAtLatch = float.NaN;

        // ── IShotSchemeDriver ────────────────────────────────────────────────────

        public ControlScheme Scheme        => ControlScheme.Pendulum;
        public bool          IsImplemented => true;

        public void Bind(ShotController controller) => _controller = controller;

        public void Activate()
        {
            ResetSwing();
            ShowHandle(true);
            ApplyLayout();
            if (_controller != null) _controller.OnStateChanged += HandleStateChanged;
        }

        public void Deactivate()
        {
            if (_controller != null) _controller.OnStateChanged -= HandleStateChanged;
            ResetSwing();
        }

        private void Awake()
        {
            if (_schemeRoot == null) _schemeRoot = transform.parent as RectTransform;
            BindHandle();
        }

        /// <summary>Cache the handle's rest position and its CanvasGroup (the ClubHandle copy
        /// already carries one). Called from Awake and from the test seam, so neither path has to
        /// remember to do it.</summary>
        private void ShowHandle(bool visible)
        {
            if (_handleGroup != null) _handleGroup.alpha = visible ? 1f : 0f;
        }

        private void BindHandle()
        {
            if (_handle == null) return;
            _handleRest  = _handle.anchoredPosition;
            _handleGroup = _handle.GetComponent<CanvasGroup>();
            if (_handleGroup == null) _handleGroup = _handle.gameObject.AddComponent<CanvasGroup>();
        }

        // OnDisable, not only Deactivate: the host turns the ROOT off when the player switches
        // scheme, and a root that goes inactive never gets Deactivate called on its children.
        private void OnDisable()
        {
            if (_controller != null) _controller.OnStateChanged -= HandleStateChanged;
            ResetSwing();
        }

        // ── Layout ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Push the live stats into the drawn lane and bar. Called at Activate and again at every
        /// pointer-down, because the club (and therefore the accuracy windows, and whether this is
        /// a putt at all) can change between two swings but never during one.
        /// </summary>
        private void ApplyLayout()
        {
            bool isPutt = _controller != null && _controller.IsPutt;
            if (_laneView != null) _laneView.ApplyGeometry(_cfg, isPutt);
            RedrawBands();
        }

        /// <summary>
        /// Size the drawn bands for the power the shot would commit at RIGHT NOW. Called every
        /// drag frame, not just at Activate: the whole point of the power shrink is that the
        /// player WATCHES the green band close as they pull.
        ///
        /// <para>Drawn from <see cref="_peakPower"/> and not the live power, because the shot
        /// commits at the peak — a band that widened again when the finger eased back would be
        /// showing a target the swing is not going to be judged against, and the up-flick (where
        /// the live power falls to 0) would snap it to full width at the exact moment the player
        /// is reading it.</para>
        /// </summary>
        private void RedrawBands()
        {
            if (_barView == null) return;
            bool  isPutt = _controller != null && _controller.IsPutt;
            float acc    = _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f;
            _barView.ApplyWindows(PendulumMath.JustWindow01(acc, _peakPower, _cfg),
                                  PendulumMath.GoodWindow01(acc, _peakPower, _cfg),
                                  isPutt);
        }

        private void HandleStateChanged(ShotInputState state)
        {
            _laneView?.ApplyState(state.State);
            _barView?.ApplyState(state.State);
            if (state.State == ShotState.Idle) _barView?.SetMarker(0f);

            // The club head comes BACK here, and only here. It is hidden at commit (see
            // ShowHandle(false) in OnPointerUp) rather than on this event, because
            // ShotController does not PublishState on the Idle->Flicking transition — waiting
            // for a Flicking state that never arrives would leave the handle under a ball that
            // has already gone.
            if (state.State != ShotState.Flicking && state.State != ShotState.Resolving)
                ShowHandle(true);
        }

        // ── Gesture ──────────────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            if (_controller == null || _schemeRoot == null) return;
            if (_controller.State != ShotState.Idle) return;

            BeginSwingLocal(ToLocal(e));
            _controller.PushTouchSample(e.position);
            ProcessDragLocal(ToLocal(e));
        }

        /// <summary>
        /// Open a swing at a point in the scheme root's LOCAL space. Split out of
        /// <see cref="OnPointerDown"/> so <see cref="DriveBot"/> can start the identical swing
        /// without a <c>PointerEventData</c> — a bot has no camera, no press position and nothing
        /// for <c>RectTransformUtility</c> to project, but it does need every field this method
        /// resets, in the order it resets them.
        /// </summary>
        private void BeginSwingLocal(Vector2 originLocal)
        {
            _dragging = true;
            _phase    = 0f;
            _sweeps   = 0;
            _power    = 0f;
            _curve    = 0f;
            _peakPower = 0f;
            _peakCurve = 0f;
            _markerAtLatch = float.NaN;

            ApplyLayout();
            _gradePop?.HideImmediate();
            _originLocal = originLocal;

            // Same order ClubHandleDragger uses, and for the same reason: BeginExternalDrag resets
            // the swing (which clears PendingSpinInput), so the HUD's spin must be pushed after it.
            _controller.BeginExternalDrag(ownsTiming: true);
            _controller.PendingSpinInput = SpinContext.Spin;
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            _controller.PushTouchSample(e.position);
            ProcessDragLocal(ToLocal(e));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            _controller.PushTouchSample(e.position);   // the release closes the gate's window
            ReleaseSwing(requireFlickGate: true);
        }

        /// <summary>
        /// The release, gate and all. <paramref name="requireFlickGate"/> is false for exactly one
        /// caller — <see cref="DriveBot"/>, which never pushed a touch sample and so has no flick
        /// to measure. Everything AFTER the gate (the grade, the pop, the commit) is shared, which
        /// is what makes the grade over a bot's ball the same grade a human would have earned.
        /// </summary>
        private void ReleaseSwing(bool requireFlickGate)
        {
            _dragging = false;

            // §3.2: the gate is asked FIRST. A driver that owns its timing has no arrow to fall
            // back on, so a release that was not a flick is not a weak shot — it is not a shot.
            if (requireFlickGate && !_controller.EvaluateFlickGate())
            {
                _controller.RejectExternalDrag();
                ResetSwing();
                return;
            }

            if (_peakPower <= 0.02f)
            {
                _controller.CancelExternalDrag();
                ResetSwing();
                return;
            }

            // The swing is graded on where the marker was when the player STARTED the up-flick,
            // and powered by the deepest point of the pull — see _peakPower / _markerAtLatch.
            float m       = float.IsNaN(_markerAtLatch) ? PendulumMath.MarkerAt(_phase) : _markerAtLatch;
            float halfCone = _controller.ConeHalfAngleDeg * Mathf.Deg2Rad;
            // _peakPower, the same number RedrawBands used — the window graded against is exactly
            // the one the player was looking at.
            var   verdict = PendulumMath.Grade(m, _controller.ClubAccuracyNorm01, _peakPower,
                                               halfCone, _cfg);

            if (_logSwings)
                Debug.Log($"[Pendulum] m={m:F3} (latched={!float.IsNaN(_markerAtLatch)}) " +
                          $"grade={verdict.Grade} power={_peakPower:F2} " +
                          $"hz={_hz:F2} sweeps={_sweeps} errorYaw={verdict.ErrorYawRad:F4}rad " +
                          $"timingMul={verdict.TimingMul:F2} timing01={verdict.Timing01:F2} " +
                          $"curve={_peakCurve:F2}");

            // AimOffset01 is 0 by design: the pendulum does not aim with the handle. In Straight
            // the aim is the camera heading; in FadeDraw it is the locked heading and the lateral
            // pull becomes the CURVE — both of which AimYawFor(0) already returns.
            // The club head goes away with the ball (Cesar, 2026-09-05): once the shot is
            // committed the handle is not a control any more, and leaving it sitting under a
            // departed ball reads as stuck UI. Alpha, not SetActive — the copy carries a live
            // ClubHandleSpriteBinder that subscribes in OnEnable, and cycling the object would
            // churn those subscriptions every shot.
            ShowHandle(false);

            LastCommittedPower  = _peakPower;
            LastCommittedMarker = m;
            LastCommittedMarkerWasLatched = !float.IsNaN(_markerAtLatch);
            LastCommittedGrade     = verdict.Grade;
            LastCommittedTimingMul = verdict.TimingMul;
            LastCommittedTiming01  = verdict.Timing01;

            _gradePop?.Show(verdict.Grade);
            _controller.CommitExternal(new ShotIntent(
                powerNormalized: _peakPower,
                aimOffset01:     0f,
                errorYawRad:     verdict.ErrorYawRad,
                timingMul:       verdict.TimingMul,
                timing01:        verdict.Timing01,
                fadeDraw01:      _peakCurve));

            ResetSwing();
        }

        private void ProcessDragLocal(Vector2 local)
        {
            float   pullPx = Mathf.Max(0f, _originLocal.y - local.y);

            bool isPutt = _controller.IsPutt;
            _power = PendulumMath.Power(pullPx, _cfg, isPutt);

            // Lateral pull is the fade/draw amount, and ONLY that: in Straight mode it does
            // nothing at all, which is what makes "pull straight down" the whole gesture.
            bool fadeDraw = !isPutt && _controller.FadeDrawActive;
            _curve = fadeDraw
                ? Mathf.Clamp((local.x - _originLocal.x) / Mathf.Max(_cfg.PendulumCurveHalfWidthPx, 1f), -1f, 1f)
                : 0f;

            // The deepest point of the pull is the shot's power, and the curve is whatever the
            // lateral offset was AT that depth — the same pairing ClubHandleDragger latches.
            if (_power > _peakPower)
            {
                _peakPower = _power;
                _peakCurve = _curve;
                RedrawBands();      // the target closes as the pull deepens
            }

            // SetExternalPower keeps taking the LIVE value so the gauge and the club head track
            // the finger all the way through the flick, exactly as they do under Flick.
            _controller.SetExternalPower(_power, _curve);
            MoveHandle(pullPx, local.x - _originLocal.x, fadeDraw);
        }

        private void MoveHandle(float pullPx, float lateralPx, bool fadeDraw)
        {
            if (_handle == null) return;

            // Clamp to the deepest TICK, not to the lane height. The lane is longer than the
            // furthest useful pull by the club's lower half plus slack (PendulumLaneView derives
            // it that way), so clamping to the lane would let the club slide past the 120% line
            // into the pill's tail — travel that buys no power and reads as the line being wrong.
            float maxPull = _controller != null && _controller.IsPutt
                ? _cfg.PendulumPull100Px
                : _cfg.PendulumPull120Px;

            float y = _handleRest.y - Mathf.Clamp(pullPx, 0f, maxPull);
            float x = fadeDraw
                ? _handleRest.x + Mathf.Clamp(lateralPx, -_cfg.PendulumCurveHalfWidthPx,
                                                          _cfg.PendulumCurveHalfWidthPx)
                : _handleRest.x;
            _handle.anchoredPosition = new Vector2(x, y);
        }

        private Vector2 ToLocal(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _schemeRoot, e.position, e.pressEventCamera, out var local);
            return local;
        }

        // ── Marker ───────────────────────────────────────────────────────────────

        // A bot advances the marker itself, with a dt it chooses so the commit lands ON its
        // sampled offset instead of wherever the next frame boundary fell (see DriveBot). Update
        // must not then advance it a second time in the same frame.
        private bool _botDriving;

        private void Update() { if (!_botDriving) Advance(Time.deltaTime); }

        /// <summary>
        /// Swing the marker one frame. The marker only moves while a finger is DOWN and power has
        /// been pulled — a swing with no power has nothing to time yet, and the bar is faded out.
        /// </summary>
        private void Advance(float dt)
        {
            if (_controller == null) return;
            if (!_dragging || _peakPower <= 0f) return;

            ShotState s = _controller.State;
            if (s != ShotState.Pulling && s != ShotState.Timing) return;

            // Freeze the marker the instant ShotController says the finger reversed upward. The
            // bar keeps drawing where it froze, which is also the honest thing to show: that IS
            // the offset the shot will be graded on.
            if (_controller.IsAimLocked)
            {
                if (float.IsNaN(_markerAtLatch)) _markerAtLatch = PendulumMath.MarkerAt(_phase);
                return;
            }

            _hz = PendulumMath.Hz(_controller.CharacterClubControl, _power,
                                  _controller.OverpowerForgiveness01, _controller.IsPutt, _cfg);

            float before = _phase;
            _phase += _hz * dt;
            // One sweep = one full sinusoid cycle = the marker back where it started.
            _sweeps += Mathf.FloorToInt(_phase) - Mathf.FloorToInt(before);

            _barView?.SetMarker(PendulumMath.MarkerAt(_phase));

            // Safety mirror of the flick's MaxTotalPasses: a finger parked on the screen must not
            // swing forever. The swing is cancelled, not fired — an abandoned swing is not a shot.
            if (_sweeps >= Mathf.RoundToInt(_cfg.PendulumMaxSweeps))
            {
                _dragging = false;
                _controller.CancelExternalDrag();
                ResetSwing();
            }
        }

        private void ResetSwing()
        {
            _dragging = false;
            _phase    = 0f;
            _sweeps   = 0;
            _power    = 0f;
            _curve    = 0f;
            _peakPower = 0f;
            _peakCurve = 0f;
            _markerAtLatch = float.NaN;
            RedrawBands();          // next swing starts from the full-width target again
            // Deliberately does NOT touch the handle's alpha: ResetSwing runs immediately after a
            // commit, and showing it again here would undo the hide in the same frame.
            if (_handle != null) _handle.anchoredPosition = _handleRest;
            _barView?.SetMarker(0f);
        }

        // ── Bot seam (bot_scheme_parity §3.2) ────────────────────────────────────

        /// <summary>
        /// Swing this scheme for a BOT: pull the club down the lane, let the bar sweep, and
        /// release the instant the live marker reaches <paramref name="targetMarker01"/>.
        ///
        /// <para>IT PLAYS THE REAL UI, IT DOES NOT FAKE A RESULT. Every frame goes through the
        /// same <c>ProcessDragLocal</c> a thumb goes through and the same <c>Advance</c> the
        /// marker always uses, and the release lands in <c>ReleaseSwing</c>, so the JUST/GOOD/MISS
        /// the player watches pop over the bot's ball is the honest grade of the marker position
        /// they just watched it commit at. Nothing is injected after the commit.</para>
        ///
        /// <para>THE BOT OWNS THE CLOCK WHILE IT SWINGS. It advances the marker with a dt it
        /// chooses — capped at the real frame's dt, but shortened to land EXACTLY on the target
        /// when the target is closer than one frame away. Waiting for a frame boundary instead
        /// would quantise the commit by up to ~0.2 of the bar at 1 Hz on a 60 Hz screen, which is
        /// most of a JUST window: the difficulty calibration in §5 is a sigma on this offset, and
        /// frame noise that large would swamp it. The marker the bar DRAWS is the marker the shot
        /// is graded on either way — this only decides which instant gets drawn.</para>
        /// </summary>
        /// <param name="power01">Intended power, 0..1.2. Converted to the pull depth that
        /// produces it through this scheme's own <c>PendulumMath.Power</c> curve.</param>
        /// <param name="targetMarker01">Marker offset to commit at, −1..+1. 0 = dead on the pip
        /// (a JUST), which is what <c>BotExecutionBand.Perfect</c> asks for.</param>
        /// <param name="curve01">Fade/draw amount, −1..+1. Bots never shape a shot, so this is 0
        /// from every executor; the parameter exists because the lane can carry it.</param>
        public IEnumerator DriveBot(float power01, float targetMarker01, float curve01,
                                    float rampSeconds, float commitTol01, int maxWaitSweeps)
        {
            if (_controller == null) yield break;
            if (_controller.State != ShotState.Idle)
            {
                Debug.LogWarning($"[Pendulum] DriveBot: shot is {_controller.State}, not Idle — swing skipped.");
                yield break;
            }

            bool  isPutt    = _controller.IsPutt;
            float pullPx    = PullPxForPower(power01, isPutt);
            float lateralPx = Mathf.Clamp(curve01, -1f, 1f) * _cfg.PendulumCurveHalfWidthPx;
            float target    = Mathf.Clamp(targetMarker01, -1f, 1f);
            float ramp      = Mathf.Max(1e-3f, rampSeconds);
            float tol       = Mathf.Max(1e-4f, commitTol01);

            BeginSwingLocal(Vector2.zero);
            _botDriving = true;

            // 1. The pull. The marker starts sweeping as soon as there is power to time, exactly
            //    as it does under a thumb, so the bar is already alive during the ramp.
            float t = 0f;
            while (t < ramp)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / ramp);
                ProcessDragLocal(new Vector2(lateralPx * k, -pullPx * k));
                Advance(dt);
                yield return null;
                if (!_dragging) { _botDriving = false; yield break; }   // MaxSweeps cancelled it
            }
            ProcessDragLocal(new Vector2(lateralPx, -pullPx));

            // 2. Wait for the marker to reach the sampled offset, stepping exactly onto it when
            //    it is less than a frame away.
            int   sweepsAtStart = _sweeps;
            int   maxSweeps     = Mathf.Max(1, maxWaitSweeps);
            float bestErr       = Mathf.Abs(MarkerOffset - target);
            // Wall-clock backstop. The sweep budget above is the real limit, but it counts
            // MARKER sweeps: a config that somehow stalled the marker would leave this loop
            // spinning forever and hang the opponent's turn, and a bot must never be the thing
            // that ends a match.
            float waited = 0f;
            const float MaxWaitSeconds = 20f;
            while (_dragging)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > MaxWaitSeconds)
                {
                    Debug.LogWarning($"[Pendulum] DriveBot: marker never reached {target:F3} in " +
                                     $"{MaxWaitSeconds:F0}s (hz={_hz:F2}) — committing at m={MarkerOffset:F3}.");
                    break;
                }
                float need = DtToMarker(target);
                float step = Mathf.Min(Time.unscaledDeltaTime, need);
                Advance(step);
                if (!_dragging) break;

                float err = Mathf.Abs(MarkerOffset - target);
                if (err < bestErr) bestErr = err;
                if (err <= tol) break;

                // Nearest-pass fallback: the marker has swept past the target as many times as we
                // agreed to wait, so take the offset it is on now rather than sweeping forever.
                if (_sweeps - sweepsAtStart >= maxSweeps)
                {
                    Debug.Log($"[Pendulum] DriveBot: target {target:F3} not hit within {maxSweeps} sweeps " +
                              $"(closest {bestErr:F3}) — committing at m={MarkerOffset:F3}.");
                    break;
                }
                yield return null;
            }

            _botDriving = false;
            if (!_dragging) yield break;    // cancelled mid-wait; nothing to commit

            // 3. The release. No flick gate: the bot pushed no touch samples, so there is no
            //    flick to measure — and _markerAtLatch is left NaN, so ReleaseSwing grades the
            //    LIVE marker, i.e. exactly the one the bar is drawing this frame.
            ReleaseSwing(requireFlickGate: false);
        }

        /// <summary>
        /// Seconds until the marker next reaches <paramref name="target"/>, or +inf when it never
        /// will (a stopped marker). Solves <c>sin(theta) == target</c> for the smallest positive
        /// step forward, which is what lets a bot commit ON its sampled offset rather than on the
        /// nearest frame boundary.
        /// </summary>
        private float DtToMarker(float target)
        {
            if (_hz <= 1e-4f) return float.PositiveInfinity;

            float twoPi = 2f * Mathf.PI;
            float theta = Mathf.Repeat(_phase * twoPi, twoPi);
            float a     = Mathf.Asin(Mathf.Clamp(target, -1f, 1f));      // [-pi/2, pi/2]

            float best = float.PositiveInfinity;
            for (int k = 0; k <= 1; k++)
            {
                // The two families of solutions to sin(x) = target, one period ahead each.
                float c1 = a + twoPi * k;
                float c2 = Mathf.PI - a + twoPi * k;
                float d1 = c1 - theta;
                float d2 = c2 - theta;
                if (d1 > 1e-6f && d1 < best) best = d1;
                if (d2 > 1e-6f && d2 < best) best = d2;
            }
            if (float.IsPositiveInfinity(best)) return float.PositiveInfinity;
            return (best / twoPi) / _hz;
        }

        /// <summary>
        /// The inverse of <c>PendulumMath.Power</c>: how far down the lane a pull has to go to
        /// ask for <paramref name="power01"/>. A bot decides a POWER, but the driver — like the
        /// thumb — only understands pixels, and inverting here keeps the two curves one curve.
        /// </summary>
        private float PullPxForPower(float power01, bool isPutt)
        {
            float minPull = _cfg.PendulumMinUsefulPullPx;
            float p100    = _cfg.PendulumPull100Px;
            float p120    = _cfg.PendulumPull120Px;

            float p = Mathf.Clamp(power01, 0f, ShotController.MaxOverpowerNormalized);
            if (p <= 0f) return 0f;
            if (isPutt || p <= 1f) return minPull + Mathf.Clamp01(p) * (p100 - minPull);
            return p100 + ((p - 1f) / 0.2f) * (p120 - p100);
        }

        /// <summary>
        /// Grade a marker offset with THIS driver's live config and stats, without swinging.
        /// The bot executor needs it to map a candidate offset to the yaw it would produce so the
        /// tree probe can reject the ones that fly into a trunk — and it must be the same call
        /// <see cref="ReleaseSwing"/> makes, or the sampler would be clearing a different shot
        /// from the one that gets fired.
        /// </summary>
        public PendulumMath.Verdict GradeForBot(float m, float power01)
        {
            float acc      = _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f;
            float halfCone = (_controller != null ? _controller.ConeHalfAngleDeg : 0f) * Mathf.Deg2Rad;
            return PendulumMath.Grade(m, acc, power01, halfCone, _cfg);
        }

        /// <summary>The bot commit tolerance and sweep budget, straight off the config this driver
        /// is using — so the executor cannot pass numbers the driver disagrees with.</summary>
        public float BotCommitTol01   => _cfg.PendulumBotCommitTol01;
        public int   BotMaxWaitSweeps => Mathf.RoundToInt(_cfg.PendulumBotMaxWaitSweeps);

        // ── Test seam ────────────────────────────────────────────────────────────

        /// <summary>
        /// EditMode wiring seam. A plain MonoBehaviour gets no Awake in EditMode, so a test that
        /// only assigned the serialized fields would drive an object that never started — the same
        /// argument <c>ShotSchemeHost.ConfigureForTests</c> makes. Also injects the config so a
        /// test can drive the whole table without touching the shipped tuning.
        /// </summary>
        public void ConfigureForTests(RectTransform schemeRoot, RectTransform handle,
                                      PendulumLaneView lane, PendulumBarView bar,
                                      SchemeGradePop pop, in ControlsConfig cfg)
        {
            _schemeRoot = schemeRoot;
            _handle     = handle;
            _laneView   = lane;
            _barView    = bar;
            _gradePop   = pop;
            _cfg        = cfg;
            BindHandle();
        }

        /// <summary>EditMode seam: the same <c>Advance</c> Update calls, with an explicit dt.
        /// Deliberately the SAME method and not a copy — a test-only reimplementation of the sweep
        /// counter would be a test that passes while production drifts.</summary>
        public void TickForTests(float dt) => Advance(dt);

        /// <summary>EditMode seam: force the marker to a known offset so a test can assert the
        /// grade a release produces without racing a real sinusoid. Clears the upswing latch too,
        /// or the forced phase would be ignored in favour of a value frozen before it.</summary>
        public void SetPhaseForTests(float phase)
        {
            _phase = phase;
            _markerAtLatch = float.NaN;
        }

        /// <summary>Live marker offset −1..+1 — or the LATCHED one once the upswing froze it,
        /// so a reader always sees the number the shot will actually be graded on.</summary>
        public float MarkerOffset =>
            float.IsNaN(_markerAtLatch) ? PendulumMath.MarkerAt(_phase) : _markerAtLatch;

        /// <summary>True once the upswing reversal froze the marker for this swing.</summary>
        public bool MarkerLatched => !float.IsNaN(_markerAtLatch);

        /// <summary>The power the shot WOULD be committed at right now — the deepest point of
        /// the pull so far, not the live value (which is 0 by the time the finger leaves the
        /// glass). Zero between swings, because <c>ResetSwing</c> clears it.</summary>
        public float PeakPower => _peakPower;

        /// <summary>The peak power the LAST committed swing actually fired at, and the marker
        /// offset it was graded on. Survives the swing reset, so a test or a verification bot can
        /// read what happened AFTER the release instead of having to catch it mid-gesture.</summary>
        public float LastCommittedPower  { get; private set; }
        public float LastCommittedMarker { get; private set; } = float.NaN;

        /// <summary>Whether that committed marker came from the upswing latch (the normal case)
        /// or was read live at release (a programmatic driver with no real reversal).</summary>
        public bool  LastCommittedMarkerWasLatched { get; private set; }

        /// <summary>The verdict the driver reached on the last committed swing. Read back by the
        /// acceptance bot and compared against what <c>ShotController</c> actually committed, so
        /// the two halves of the seam are checked against each other rather than both trusted.</summary>
        public PendulumGrade LastCommittedGrade     { get; private set; }
        public float         LastCommittedTimingMul { get; private set; } = 1f;
        public float         LastCommittedTiming01  { get; private set; } = float.NaN;

        /// <summary>The live pull thresholds, straight off the config the driver is using. Public
        /// so a verification run can assert the DRAWN tick sits where the CONFIG says, instead of
        /// deriving the threshold from the tick and asserting a tautology.</summary>
        public float Pull100Px => _cfg.PendulumPull100Px;
        public float Pull120Px => _cfg.PendulumPull120Px;

        /// <summary>The live accuracy windows, as fractions of the bar's half-travel. Public so a
        /// verification run can state how wide the target actually was for the equipped club
        /// instead of re-deriving the lerp and drifting from it.</summary>
        public float JustWindow01 => PendulumMath.JustWindow01(
            _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f, _peakPower, _cfg);
        public float GoodWindow01 => PendulumMath.GoodWindow01(
            _controller != null ? _controller.ClubAccuracyNorm01 : 0.5f, _peakPower, _cfg);

        /// <summary>Live marker frequency, cycles/sec. Tuning + acceptance evidence.</summary>
        public float MarkerHz => _hz;
    }
}
