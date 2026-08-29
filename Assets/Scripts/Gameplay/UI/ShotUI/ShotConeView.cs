using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Main cone UI coordinator. Lives on a child of the shot Canvas.
    // Subscribes to ShotController.OnStateChanged and updates all visual elements.
    //
    // World-space targeting line projection (ball position → screen) is wired
    // in Part E. Call SetCamera() and SetBallTransform() from the lab controller.
    public class ShotConeView : MonoBehaviour
    {
        // ── Wired in Inspector ────────────────────────────────────────────────
        [Header("Controller")]
        [SerializeField] private ShotController  _shotController;

        [Header("Cone")]
        [SerializeField] private ConeMeshGraphic _coneGraphic;
        [SerializeField] private float           _coneHeightPx = 1009f;

        [Header("Club handle")]
        [SerializeField] private RectTransform   _clubHandle;
        [SerializeField] private float           _handleStartYPx = 785f;
        [SerializeField] private float           _handleWidth    = 178f;
        [SerializeField] private float           _handleHeight   = 100f;
        [SerializeField] private float           _minHandleScale = 1f;
        [SerializeField] private float           _maxHandleScale = 1.3f;

        [Header("Timing slab")]
        [SerializeField] private TimingSlabGraphic _timingSlab;

        // Arrow0 only — wires the TimingSlabGraphic GO so SetupSlab can disable it on Awake.
        [Header("Arrows (legacy)")]
        [SerializeField] private RectTransform[] _arrows = new RectTransform[1];

        [Header("Slab colors")]
        [SerializeField] private Color _slabColorBase = new Color(1.00f, 0.60f, 0.60f, 0.70f);
        [SerializeField] private Color _slabColorMid  = new Color(1.00f, 0.92f, 0.65f, 0.70f);
        [SerializeField] private Color _slabColorTop  = new Color(0.68f, 0.92f, 0.68f, 0.70f);

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI _powerHUD;

        [Header("Targeting line")]
        [SerializeField] private RectTransform   _targetingLine;
        // Bend renderer — bootstrapped from _targetingLine's Image at Awake.
        // Set by InjectBendRenderer in tests.
        private AimLineBendRenderer _bendRenderer;

        [Header("Putter timing slab")]
        [SerializeField] private RectTransform   _putterTimingSlabRT;
        [SerializeField] private float           _putterTrackHeightPx = 1000f;

        [Header("Putter track (per-shot lifecycle)")]
        [SerializeField] private GameObject      _putterTrack;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Camera    _worldCamera;
        private Transform _ballTransform;
        private float     _maxCarryYards    = 250f;
        private bool      _lastArrowTrailState;
        private bool      _puttMode;
        private UnityEngine.UI.Image _putterTimingSlabImage;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetMaxCarryYards(float yards) => _maxCarryYards = yards;

        /// <summary>Read-only access to the current carry yards (used by MapViewController).</summary>
        public float MaxCarryYardsForMap => _maxCarryYards;

        /// <summary>
        /// Test injection: wire an AimLineBendRenderer without Inspector. Call before Awake-
        /// dependent paths run. Also sets CurveScale/ReachPx to sensible defaults.
        /// </summary>
        public void InjectBendRenderer(AimLineBendRenderer renderer)
        {
            _bendRenderer = renderer;
        }

        /// <summary>
        /// Test injection point. Wires _shotController, _coneGraphic, and _putterTrack
        /// without requiring Unity Inspector serialization. Call before any other method
        /// in EditMode tests. Subscribes to OnStateChanged so HandleStateChanged fires on Tick().
        /// </summary>
        public void InjectForTests(ShotController controller, ConeMeshGraphic coneGraphic,
            GameObject putterTrack = null)
        {
            if (_shotController != null)
                _shotController.OnStateChanged -= HandleStateChanged;
            _shotController = controller;
            _coneGraphic    = coneGraphic;
            _putterTrack    = putterTrack;
            if (_shotController != null)
                _shotController.OnStateChanged += HandleStateChanged;
        }

        public void SetPuttMode(bool on)
        {
            _puttMode = on;
            // APPROACH C: iron cone is permanently disabled in putter mode (original behavior).
            // PutterTrack is the actual putter aim viz; it follows per-shot lifecycle via HandleStateChanged.
            if (_coneGraphic  != null) _coneGraphic.enabled = !on;
            if (_targetingLine != null) _targetingLine.gameObject.SetActive(!on);
            // Belt-and-suspenders: ensure PutterTrack is off when exiting putter mode.
            // (EnterPutterMode sets it true; per-shot toggle from HandleStateChanged rides on top.)
            if (!on && _putterTrack != null) _putterTrack.SetActive(false);
            // Cache Image for putter slab on first enable.
            if (on && _putterTimingSlabRT != null && _putterTimingSlabImage == null)
                _putterTimingSlabImage = _putterTimingSlabRT.GetComponent<UnityEngine.UI.Image>();
            // Ensure the correct slab is visible/hidden when mode changes.
            if (_timingSlab      != null) _timingSlab.gameObject.SetActive(false);
            if (_putterTimingSlabRT != null) _putterTimingSlabRT.gameObject.SetActive(false);
        }

        public void SetCamera(Camera cam)            => _worldCamera   = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        /// <summary>
        /// Wire the PutterTrack GameObject without requiring Inspector serialization.
        /// PhysicsLabController owns the canonical reference and hands it to ShotConeView
        /// in Awake so the per-shot lifecycle subscription in HandleStateChanged can drive it.
        /// Single source of truth — do NOT also wire _putterTrack in the Inspector.
        /// </summary>
        public void SetPutterTrack(GameObject putterTrack) => _putterTrack = putterTrack;

        public void SetOutlineVisible(bool visible)
        {
            // Non-putter mode: the debug flag toggles cone outline.
            // In putter mode, _coneGraphic is permanently disabled (set in SetPuttMode).
            // Do not re-enable it here — that would be wrong.
            if (_puttMode) return;
            if (_coneGraphic != null) _coneGraphic.enabled = visible;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_coneGraphic != null) _coneGraphic.HeightPx = _coneHeightPx;
            if (_clubHandle != null)
                _clubHandle.sizeDelta = new Vector2(_handleWidth, _handleHeight);
            SetupSlab();
            SetupBendRenderer();
        }

        private void SetupBendRenderer()
        {
            if (_targetingLine == null) return;

            // The bend renderer is a UI Graphic, and a GameObject may host only ONE Graphic.
            // _targetingLine already carries the original Image, so the mesh Graphic lives on
            // a dedicated child GO ("AimLineMesh"). _targetingLine stays the wired pivot/anchor;
            // toggling its GameObject active also shows/hides the mesh child.
            if (_bendRenderer == null)
            {
                var existing = _targetingLine.Find("AimLineMesh");
                if (existing != null)
                    _bendRenderer = existing.GetComponent<AimLineBendRenderer>();
            }
            if (_bendRenderer == null)
            {
                // Construct with CanvasRenderer up-front: [RequireComponent] on a Graphic
                // base class is not reliably honoured by runtime AddComponent on a subclass,
                // and without a CanvasRenderer the mesh is built but never drawn.
                var go = new GameObject("AimLineMesh", typeof(RectTransform), typeof(CanvasRenderer));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(_targetingLine, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot            = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = Vector2.zero;
                _bendRenderer = go.AddComponent<AimLineBendRenderer>();
                _bendRenderer.raycastTarget = false;
            }

            // Bootstrap sprite + colour from the existing Image component so the look
            // is identical to the old single-rect approach (D6).
            var sourceImage = _targetingLine.GetComponent<UnityEngine.UI.Image>();
            if (sourceImage != null)
            {
                _bendRenderer.SetSpriteFrom(sourceImage);
                // Disable the source Image so only the mesh renders (not a double-draw).
                sourceImage.enabled = false;
            }

            _bendRenderer.CurveScale = ControlsConfig.Default.AimLineCurveScale;
            _bendRenderer.ReachPx    = ControlsConfig.Default.AimLineDefaultReachPx;
        }

        private void SetupSlab()
        {
            // Disable all legacy arrows
            foreach (var rt in _arrows)
                if (rt != null) rt.gameObject.SetActive(false);

            // If _timingSlab not wired in Inspector, reuse _arrows[0]'s GO
            if (_timingSlab == null && _arrows.Length > 0 && _arrows[0] != null)
            {
                var rt = _arrows[0];
                var old = rt.GetComponent<ArrowGraphic>();
                if (old != null) DestroyImmediate(old);
                if (!rt.TryGetComponent(out _timingSlab))
                    _timingSlab = rt.gameObject.AddComponent<TimingSlabGraphic>();
                rt.sizeDelta        = new Vector2(400f, _coneHeightPx);
                rt.pivot            = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
            }

            if (_timingSlab != null)
                _timingSlab.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged += HandleStateChanged;
            // Keep ShotController.PendingSpinInput in sync with SpinContext so the
            // InputSystem gesture path (non-ClubHandle) also sees the player's spin selection.
            HUD.SpinContext.OnChanged += PushSpinToPending;
            // Phase E (fade_draw_core_wiring Order 356): push mode state on every toggle.
            HUD.ShotModeContext.OnChanged += OnShotModeChanged;
            // Sync initial state in case mode is already non-default (e.g. restored from save).
            PushFadeDrawModeToController();
        }

        private void OnDisable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged -= HandleStateChanged;
            HUD.SpinContext.OnChanged -= PushSpinToPending;
            HUD.ShotModeContext.OnChanged -= OnShotModeChanged;
        }

        private void PushSpinToPending()
        {
            if (_shotController == null) return;
            _shotController.PendingSpinInput = HUD.SpinContext.Spin;
        }

        // Phase E — mode-transition aim-lock + handle re-center (D5).
        // Called whenever ShotModeContext.OnChanged fires.
        private void OnShotModeChanged()
        {
            if (_shotController == null) return;
            bool isFadeDraw = HUD.ShotModeContext.Mode == HUD.ShotMode.FadeDraw;
            _shotController.FadeDrawActive = isFadeDraw;

            if (isFadeDraw)
            {
                // Straight→FadeDraw (arming): lock aim at camera heading (D5).
                // We lock to CameraHeadingRadians because finetune will re-center to 0 on arm —
                // so the effective aim at arm time IS the camera heading, not heading+finetune-nudge.
                _shotController.FadeDrawLockedAimRad = _shotController.CameraHeadingRadians;

                // Re-center the handle so subsequent handle movement drives curve from center (D5).
                _shotController.ForceRecenterFinetune();
            }
            else
            {
                // FadeDraw→Straight (disarming): clear the aim lock; handle becomes aim-nudge again.
                _shotController.FadeDrawLockedAimRad = float.NaN;
            }
        }

        private void PushFadeDrawModeToController()
        {
            if (_shotController == null) return;
            _shotController.FadeDrawActive = HUD.ShotModeContext.Mode == HUD.ShotMode.FadeDraw;
        }

        // ── State handler ─────────────────────────────────────────────────────

        private void HandleStateChanged(ShotInputState state)
        {
            // When the shot resolves back to Idle, reset the spin context so each
            // new shot starts from the center position. ShotController cannot call
            // SpinContext.Reset() directly (circular asmdef: Input does not ref UI);
            // ShotConeView (in UI assembly) bridges the gap here.
            // spin_and_shot_shape_wiring SPEC §5.4 next-shot handoff site.
            if (state.State == Input.ShotState.Idle)
                HUD.SpinContext.Reset();

            UpdatePutterTrackVisibility(state);
            UpdateConeWidth();
            UpdateClubHandle(state);
            UpdateSlab(state);
            UpdateHUD(state);
            UpdateTargetingLine(state);
            ApplyDebugFlags();
        }

        // ── PutterTrack visibility (per-shot lifecycle) ───────────────────────
        // APPROACH C: PutterTrack (the actual putter aim viz) follows the same
        // per-shot lifecycle as the iron cone in non-putter mode:
        //   visible at Aiming states → hidden when shot fires (Resolving).
        // Iron cone (_coneGraphic) remains permanently disabled in putter mode
        // (set by SetPuttMode(true)) — it never re-enables here.
        private void UpdatePutterTrackVisibility(ShotInputState state)
        {
            if (!_puttMode || _putterTrack == null) return;
            bool aiming = state.State is ShotState.Idle
                                      or ShotState.Aiming
                                      or ShotState.Pulling
                                      or ShotState.Timing
                                      or ShotState.Flicking;
            _putterTrack.SetActive(aiming);
        }

        private void ApplyDebugFlags()
        {
            if (_shotController == null) return;
            SetOutlineVisible(_shotController.DebugFlags.ShowConeOutline);
            bool showTrail = _shotController.DebugFlags.ShowArrowTrail;
            if (showTrail && !_lastArrowTrailState)
                Debug.Log("[Debug] Arrow trail not yet implemented");
            _lastArrowTrailState = showTrail;
        }

        // ── Cone width ────────────────────────────────────────────────────────

        private void UpdateConeWidth()
        {
            if (_puttMode) return;  // putter uses default cone geometry; skip dirty-marking the mesh
            _coneGraphic.HalfAngleDeg = _shotController.ConeHalfAngleDeg;
            _coneGraphic.HeightPx     = _coneHeightPx;
        }

        // ── Club handle ───────────────────────────────────────────────────────

        private void UpdateClubHandle(ShotInputState state)
        {
            if (_clubHandle == null) return;

            float power         = Mathf.Clamp01(state.PowerNormalized);
            // Handle rests below ball at _handleStartYPx; pulling drives it down toward base (y=0).
            float handleY       = _handleStartYPx * (1f - power);
            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = _coneHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - Mathf.Clamp01(handleY / _coneHeightPx);
            float maxX          = halfBase * widthFraction;

            // HandleFinetune (LIVE), not state.ConeFinetuneX (the latched AIM value): once the
            // upswing latches the aim, the handle must keep following the finger. Using the aim
            // value here made the handle collapse toward the centreline during the flick.
            float xOffset = _puttMode ? 0f : _shotController.HandleFinetune * maxX;
            _clubHandle.anchoredPosition = new Vector2(xOffset, handleY);

            float handleScale = Mathf.Lerp(_minHandleScale, _maxHandleScale, power);
            _clubHandle.localScale = Vector3.one * handleScale;
        }

        // ── Timing slab ───────────────────────────────────────────────────────

        private void UpdateSlab(ShotInputState state)
        {
            bool show = state.State == ShotState.Timing;

            if (_puttMode)
            {
                if (_timingSlab != null) _timingSlab.gameObject.SetActive(false);
                if (_putterTimingSlabRT == null) return;
                _putterTimingSlabRT.gameObject.SetActive(show);
                if (!show) return;

                float p = Mathf.Clamp01(state.ArrowProgress01);
                // p=1 → top of track (y=0), p=0 → bottom (y=-trackHeight).
                _putterTimingSlabRT.anchoredPosition = new Vector2(0f, -_putterTrackHeightPx * (1f - p));
                if (_putterTimingSlabImage != null) _putterTimingSlabImage.color = SlabColorFromProgress(p);
                return;
            }

            if (_timingSlab == null) return;
            _timingSlab.gameObject.SetActive(show);
            if (!show) return;

            float prog = Mathf.Clamp01(state.ArrowProgress01);
            float curvePx = _coneGraphic != null ? _coneGraphic.CurvaturePx : 15f;
            _timingSlab.SetConeParams(_coneHeightPx, _shotController.ConeHalfAngleDeg, curvePx);
            _timingSlab.CurrentY01 = prog;
            _timingSlab.color      = SlabColorFromProgress(prog);
        }

        private Color SlabColorFromProgress(float p)
        {
            if (p <= ConeBandPalette.BandGoldY01)
            {
                float t = Mathf.InverseLerp(ConeBandPalette.BandRedY01, ConeBandPalette.BandGoldY01, p);
                return Color.Lerp(_slabColorBase, _slabColorMid, t);
            }
            if (p <= ConeBandPalette.BandGreenY01)
            {
                float t = Mathf.InverseLerp(ConeBandPalette.BandGoldY01, ConeBandPalette.BandGreenY01, p);
                return Color.Lerp(_slabColorMid, _slabColorTop, t);
            }
            return _slabColorTop;
        }

        /// <summary>
        /// The RED / GOLD / GREEN band colour at slab progress <paramref name="p"/> — the zone
        /// colours the player reads the cone by, straight off the live
        /// <see cref="ConeMeshGraphic"/> so a scene that has tuned them cannot disagree with the
        /// ribbon that quotes them back.
        ///
        /// NOT <see cref="SlabColorFromProgress"/>: that is the sweeping slab's own translucent
        /// tint, whose stops in LabScaffold are white → amber → green. The band lines are what
        /// make the bottom of the cone read RED, and the red band is the one the ball trail has
        /// to be able to show.
        ///
        /// Falls back to the <see cref="ConeBandPalette"/> constants only when no cone graphic is
        /// wired (putt mode disables it; test scaffolds may omit it).
        /// </summary>
        public Color BandColorFromProgress(float p)
        {
            if (_coneGraphic != null) return _coneGraphic.BandColorFor(p);
            if (p >= ConeBandPalette.BandGreenY01) return ConeBandPalette.ColorGreen;
            if (p >= ConeBandPalette.BandGoldY01)  return ConeBandPalette.ColorGold;
            return ConeBandPalette.ColorRed;
        }

        // ── HUD ───────────────────────────────────────────────────────────────

        private void UpdateHUD(ShotInputState state)
        {
            if (_powerHUD == null) return;

            bool showHUD = state.State is ShotState.Pulling
                                      or ShotState.Timing
                                      or ShotState.Flicking;
            _powerHUD.gameObject.SetActive(showHUD);

            if (!showHUD) return;

            int   pct = Mathf.RoundToInt(state.PowerNormalized * 100f);
            float yds = _maxCarryYards * state.PowerNormalized;

            // F15 (shot_timing_power §4): PowerNormalized is still the pull — the swing FELT
            // full-power, the ball just goes shorter. The timing penalty is shown alongside it
            // and only once it bites, so a well-timed shot reads exactly as it did before.
            _powerHUD.text = state.TimingPowerMul < 1f
                ? $"{pct}% \u00d7 {state.TimingPowerMul:F2}\n{yds:F0} yd"
                : $"{pct}%\n{yds:F0} yd";
        }

        // ── Targeting line ────────────────────────────────────────────────────

        private void UpdateTargetingLine(ShotInputState state)
        {
            if (_puttMode) { if (_targetingLine != null) _targetingLine.gameObject.SetActive(false); return; }
            if (_targetingLine == null) return;

            bool show = state.State is ShotState.Idle
                                    or ShotState.Aiming
                                    or ShotState.Pulling
                                    or ShotState.Timing
                                    or ShotState.Flicking;
            _targetingLine.gameObject.SetActive(show);

            if (!show || _worldCamera == null || _ballTransform == null) return;

            // ── Compute aim angle from world-space projection (same as before) ──────
            Vector3 aimDir      = new Vector3(Mathf.Cos(state.AimYawRadians), 0f, Mathf.Sin(state.AimYawRadians));
            Vector3 targetWorld = _ballTransform.position + aimDir * ControlsConfig.Default.TargetingLineLengthMeters;

            RectTransform parentRect = _targetingLine.parent as RectTransform;
            Vector3 ballScreen   = _worldCamera.WorldToScreenPoint(_ballTransform.position);
            Vector3 targetScreen = _worldCamera.WorldToScreenPoint(targetWorld);
            if (ballScreen.z < 0f) { _targetingLine.gameObject.SetActive(false); return; }

            Vector2 ballCanvasPos   = Vector2.zero;
            Vector2 targetCanvasPos = Vector2.zero;
            if (parentRect != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, ballScreen,   null, out ballCanvasPos);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, targetScreen, null, out targetCanvasPos);
            }

            Vector2 lineDir = (targetCanvasPos - ballCanvasPos).normalized;
            float   angle   = Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg - 90f;

            _targetingLine.anchoredPosition = Vector2.zero;

            // ── Feed the bend renderer ────────────────────────────────────────────
            if (_bendRenderer != null)
            {
                // FadeDraw armed: read live mode from ShotModeContext.
                bool fadeDrawArmed = HUD.ShotModeContext.Mode == HUD.ShotMode.FadeDraw;
                _bendRenderer.FadeDrawArmed = fadeDrawArmed;
                _bendRenderer.FinetuneX     = state.ConeFinetuneX;   // –1=draw, +1=fade (D5)
                _bendRenderer.AimAngleDeg   = angle;

                // Phase C — power-driven reach.
                // At Idle/Aiming: default reach from config.
                // At Pulling/Timing/Flicking: scale reach by live PowerNormalized.
                var cfg = ControlsConfig.Default;
                float defaultReach = cfg.AimLineDefaultReachPx;
                float reach;
                if (state.State is ShotState.Pulling or ShotState.Timing or ShotState.Flicking)
                {
                    float power = Mathf.Clamp01(state.PowerNormalized);
                    // Reach scales from 50% to 100% of default over [0,1] power.
                    reach = defaultReach * Mathf.Lerp(0.5f, 1.0f, power);
                }
                else
                {
                    reach = defaultReach;
                }
                _bendRenderer.ReachPx    = reach;
                _bendRenderer.CurveScale = cfg.AimLineCurveScale;
                _bendRenderer.Refresh();

                // In bend-renderer mode the root RT is just a pivot container;
                // Refresh() already applied localRotation = Quaternion.Euler(0,0,AimAngleDeg).
                // Do NOT overwrite localRotation here — that was the iter-1 bug.
            }
            else
            {
                // Fallback: old single-rect rotation (no renderer wired).
                _targetingLine.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
