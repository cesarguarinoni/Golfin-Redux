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
        // INSPECTOR FALLBACKS, not the source of truth (flick_shot_view §3.2). Awake overwrites
        // every one of these from ControlsConfig; they are what an Editor scene opened without a
        // config falls back to, and they are kept in step with the csv so the edit-mode view is
        // honest. Cone height is the number the ball anchor depends on — see
        // ShotLayoutMath.FlickLaneDepthBelowBall.
        [SerializeField] private float           _coneHeightPx   = 792f;
        [Tooltip("Ball centre to cone apex. The cone hangs this + its height below the ball. " +
                 "0 = the apex sits ON the ball, which is how Flick ships.")]
        [SerializeField] private float           _coneApexGapPx  = 0f;

        [Header("Club handle")]
        [SerializeField] private RectTransform   _clubHandle;
        [SerializeField] private float           _handleStartYPx = 540f;
        [SerializeField] private float           _handleWidth    = 178f;
        [SerializeField] private float           _handleHeight   = 100f;
        [SerializeField] private float           _minHandleScale = 1f;
        [SerializeField] private float           _maxHandleScale = 1.3f;

        [Header("Power marks (flick_pull_mapping D3)")]
        [Tooltip("The 100% and 120% labels beside the cone. LABELS ONLY — Cesar, 2026-09-07: " +
                 "\"remove the 100% and 120% lines, leave only the labels\". Children of the cone " +
                 "mesh so they ride and fade with it; PLACED from controls.csv in " +
                 "ApplyConfiguredGeometry, never from an authored y.")]
        [SerializeField] private TextMeshProUGUI _label100;
        [SerializeField] private TextMeshProUGUI _label120;
        [Tooltip("Gap between the cone's EDGE at the label's own height and the label. The " +
                 "Pendulum lane's 76px side offset IS this: its lane is 120 wide, so 60 + 16. A " +
                 "CONE is a different width at every height, so the gap is what ports across.")]
        [SerializeField] private float           _labelGapPx = 16f;

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
        [SerializeField] private float           _putterTrackHeightPx = 792f;
        [Tooltip("Ball centre to the putter track's TOP edge. 0 = the track touches the ball, " +
                 "which is both what the live runtime already did and what Cesar asked for.")]
        [SerializeField] private float           _putterTrackTopBelowBallPx = 0f;

        [Header("Putter track (per-shot lifecycle)")]
        [SerializeField] private GameObject      _putterTrack;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Camera    _worldCamera;
        private Transform _ballTransform;
        private float     _maxCarryYards    = 250f;
        private bool      _lastArrowTrailState;
        private bool      _puttMode;
        private UnityEngine.UI.Image _putterTimingSlabImage;
        private ControlsConfig? _cfgOverride;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetMaxCarryYards(float yards) => _maxCarryYards = yards;

        /// <summary>The resolved cone height, handle rest and putter track length, AFTER the
        /// config has been folded in. Read by the acceptance run and the layout tests rather than
        /// re-derived from rects that four components each write a piece of.</summary>
        public float ConeHeightPx        => _coneHeightPx;
        public float HandleRestYPx       => _handleStartYPx;
        public float PutterTrackHeightPx => _putterTrackHeightPx;

        /// <summary>
        /// Test seam: the <see cref="ControlsConfig"/> <see cref="Awake"/> reads instead of
        /// <see cref="ControlsConfig.Default"/>. Call BEFORE Awake.
        ///
        /// <para>A zeroed config means "no config" and every serialized fallback stands, which is
        /// the case an Editor scene opened on its own is in.</para>
        /// </summary>
        public void InjectControlsConfig(ControlsConfig cfg) => _cfgOverride = cfg;

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
            // The 120% label is hidden on a putt (the putt caps at 1.0), so the marks are
            // re-placed on every mode flip and not only on Awake.
            ApplyMarkGeometry(_cfgOverride ?? ControlsConfig.Default);
        }

        public void SetCamera(Camera cam)            => _worldCamera   = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        /// <summary>
        /// Wire the PutterTrack GameObject without requiring Inspector serialization.
        /// PhysicsLabController owns the canonical reference and hands it to ShotConeView
        /// in Awake so the per-shot lifecycle subscription in HandleStateChanged can drive it.
        /// Single source of truth — do NOT also wire _putterTrack in the Inspector.
        /// </summary>
        public void SetPutterTrack(GameObject putterTrack)
        {
            _putterTrack = putterTrack;
            ApplyPutterTrackGeometry();
        }

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
            ApplyConfiguredGeometry();
            if (_coneGraphic != null) _coneGraphic.HeightPx = _coneHeightPx;
            if (_clubHandle != null)
                _clubHandle.sizeDelta = new Vector2(_handleWidth, _handleHeight);
            SetupSlab();
            SetupBendRenderer();
        }

        /// <summary>
        /// Fold <c>controls.csv</c> into the cone's geometry (flick_shot_view §3.2).
        ///
        /// <para>ONE PLACE, BEFORE ANYTHING READS THE FIELDS. The cone's height used to live on
        /// four objects at once — this view, <see cref="ConeMeshGraphic"/>,
        /// <see cref="TimingSlabGraphic"/> and the mesh's own scene-authored <c>-1160</c> — and
        /// that is why the ball could not be moved: nothing could re-cut the cone without all four
        /// agreeing. They are all derived from <see cref="ControlsConfig.FlickConeHeightPx"/> here,
        /// so <c>ShotLayoutMath.FlickLaneDepthBelowBall</c> (which the D6 clamp uses) and the mesh
        /// that is actually drawn are the same arithmetic.</para>
        ///
        /// <para>A non-positive key means "not configured" and leaves the serialized fallback
        /// alone. The handle rest is a FRACTION of the height on purpose (D3): the drag reads power
        /// off the whole cone, so an absolute rest would silently change what touching the club
        /// registers as the day the cone is re-cut.</para>
        ///
        /// <para>Public and idempotent — every field is recomputed from the config rather than
        /// scaled from its own current value — because Awake does not run for a component added in
        /// EditMode, and the tests that pin these numbers have to reach the production path.</para>
        /// </summary>
        public void ApplyConfiguredGeometry()
        {
            ControlsConfig cfg = _cfgOverride ?? ControlsConfig.Default;

            if (cfg.FlickConeHeightPx        > 0f) _coneHeightPx        = cfg.FlickConeHeightPx;
            // >= 0, not > 0: ZERO IS THE SHIPPING VALUE — the apex sits on the ball — so the
            // usual "non-positive means unconfigured" test would silently reinstate a gap.
            // Gated on the cone height instead, which is never legitimately zero.
            if (cfg.FlickConeHeightPx        > 0f) _coneApexGapPx       = cfg.FlickConeApexGapPx;
            if (cfg.FlickPutterTrackHeightPx > 0f) _putterTrackHeightPx = cfg.FlickPutterTrackHeightPx;
            // Gated on the HEIGHT for the same reason the apex gap is: zero is the shipping value.
            if (cfg.FlickPutterTrackHeightPx > 0f) _putterTrackTopBelowBallPx = cfg.FlickPutterTrackTopBelowBallPx;
            if (cfg.FlickHandleStartY01      > 0f) _handleStartYPx      = cfg.FlickHandleStartY01 * _coneHeightPx;

            // The mesh is pivoted at its BASE and drawn upward, so hanging it apexGap + height
            // below the ball is what puts the apex on the gap and the base on the baseline. Inside
            // BallSpace, so this y is ball-relative and the anchor move carries it.
            if (_coneGraphic != null)
            {
                RectTransform coneRT = _coneGraphic.rectTransform;
                coneRT.anchoredPosition =
                    new Vector2(coneRT.anchoredPosition.x, -(_coneApexGapPx + _coneHeightPx));
            }

            ApplyPutterTrackGeometry();
            ApplyMarkGeometry(cfg);
        }

        /// <summary>
        /// Place the 100% / 120% labels from <c>controls.csv</c> (flick_pull_mapping D3).
        ///
        /// <para>LABELS, NO LINES. The spec asked for tick lines with labels and they were built;
        /// Cesar, on seeing them: <i>"remove the 100% and 120% lines, leave only the labels"</i>.
        /// The cone already draws its own band lines, and a second family of horizontal rules
        /// across it — one of which landed 10.8px under the DUFF band line — read as noise rather
        /// than as two different meanings. The heights are unchanged; only the rules are gone.</para>
        ///
        /// <para>THE LABEL IS THE CONFIG, PLACED — the same rule <c>PendulumLaneView</c> follows.
        /// The 120% label is at the cone's BASE (y = 0) because D2 rests the club
        /// <c>FlickPull120Px</c> above it, and the 100% label is
        /// <c>FlickPull120Px - FlickPull100Px</c> = 108px up. Neither offset is authored: retune
        /// either threshold and the label moves with the power it names, which is the class of
        /// drift the Pendulum ticks were pulled into the config to kill.</para>
        ///
        /// <para>A putt hides the 120% label and keeps the 100% one — a putt caps at 1.0
        /// (<c>FlickPullMath.Power</c>), so a 120% mark on it would name a power the player can
        /// never reach. Re-applied from <see cref="SetPuttMode"/> for that reason, not only on
        /// Awake.</para>
        ///
        /// <para>An unconfigured cone (<c>FlickPull120Px</c> non-positive) leaves the scene's own
        /// placement alone, the same "no config" rule the geometry above follows.</para>
        /// </summary>
        private void ApplyMarkGeometry(in ControlsConfig cfg)
        {
            if (cfg.FlickPull120Px <= 0f) return;
            PlaceLabel(_label100, FlickPullMath.Mark100YPx(cfg), true);
            PlaceLabel(_label120, FlickPullMath.Mark120YPx(cfg), !_puttMode);
            RefreshLabelOffsets();
        }

        /// <summary>y is cone-local (0 = base). x is <see cref="RefreshLabelOffsets"/>'s job,
        /// because it depends on how wide the cone is at this y and that moves with the club.</summary>
        private void PlaceLabel(TextMeshProUGUI label, float y, bool shown)
        {
            if (label == null) return;
            if (label.gameObject.activeSelf != shown) label.gameObject.SetActive(shown);
            if (!shown) return;
            var rt = label.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y);
        }

        /// <summary>
        /// Park each label just outside the cone's EDGE at its own height.
        ///
        /// <para>THE PENDULUM'S 76 IS NOT A CONSTANT, it is 60 + 16 — half of a 120-wide lane,
        /// plus a gap. Ported literally onto a cone it put both labels inside the cone body,
        /// because the cone is ~186px wide at the 100% height and ~216 at the base. What ports
        /// across is the GAP; the half-width has to come from the cone's live geometry, which
        /// moves with Club Accuracy every time the club changes. In putt mode the putter track is
        /// what the label sits beside instead.</para>
        /// </summary>
        private void RefreshLabelOffsets()
        {
            if (_label100 == null && _label120 == null) return;
            ControlsConfig cfg = _cfgOverride ?? ControlsConfig.Default;
            if (cfg.FlickPull120Px <= 0f) return;

            if (_puttMode)
            {
                RectTransform track = ResolvePutterTrackRect();
                float puttW = track != null ? track.rect.width : 0f;
                if (puttW <= 0f) return;
                OffsetLabel(_label100, puttW);
                OffsetLabel(_label120, puttW);
                return;
            }

            float halfAngleDeg = _shotController != null
                ? _shotController.ConeHalfAngleDeg
                : (_coneGraphic != null ? _coneGraphic.HalfAngleDeg : 0f);
            float halfBase = _coneHeightPx * Mathf.Tan(halfAngleDeg * Mathf.Deg2Rad);

            OffsetLabel(_label100, ConeWidthAt(FlickPullMath.Mark100YPx(cfg), halfBase));
            OffsetLabel(_label120, ConeWidthAt(FlickPullMath.Mark120YPx(cfg), halfBase));
        }

        /// <summary>The cone's full width at cone-local height <paramref name="y"/>.</summary>
        private float ConeWidthAt(float y, float halfBase)
        {
            if (_coneHeightPx <= 0f) return 0f;
            return 2f * Mathf.Max(0f, halfBase * (1f - Mathf.Clamp01(y / _coneHeightPx)));
        }

        private void OffsetLabel(TextMeshProUGUI label, float widthAtThatHeight)
        {
            if (label == null) return;
            var rt = label.rectTransform;
            rt.anchoredPosition = new Vector2(widthAtThatHeight * 0.5f + _labelGapPx, rt.anchoredPosition.y);
        }

        /// <summary>
        /// Size and place the putter track: top <c>_putterTrackTopBelowBallPx</c> under the ball,
        /// bottom on the shared baseline (flick_shot_view D5).
        ///
        /// <para>RE-ASSERTED WHENEVER THE TRACK COMES UP, not only on Awake, because
        /// <c>PhysicsLabController.EnterPutterMode</c> writes the same rect through its own
        /// <c>AlignPutterTrackToBall</c> — and that file is under <c>Assets/Scripts/Physics/</c>,
        /// which is not editable. The two now AGREE (both put the top on the ball), so this is no
        /// longer a correction; it is what keeps the HEIGHT right, which that call never sets, and
        /// what keeps the pair from silently diverging if either number is ever tuned.</para>
        ///
        /// <para>The rect stays TOP-anchored to <c>BallSpace</c>, exactly as authored, and the y is
        /// derived from the parent's half-height rather than the scene's <c>-1453</c> — same number
        /// on a 2532 canvas, and right on every other one.</para>
        /// </summary>
        private void ApplyPutterTrackGeometry()
        {
            RectTransform track = ResolvePutterTrackRect();
            if (track == null) return;

            if (track.TryGetComponent(out PutterTrackGraphic graphic))
                graphic.HeightPx = _putterTrackHeightPx;
            track.sizeDelta = new Vector2(track.sizeDelta.x, _putterTrackHeightPx);

            if (track.parent is RectTransform parent)
                track.anchoredPosition = new Vector2(
                    track.anchoredPosition.x,
                    -(parent.rect.height * 0.5f + _putterTrackTopBelowBallPx));
        }

        /// <summary>The track's rect, whether or not <c>PhysicsLabController</c> has handed it over
        /// yet — on Awake it usually has not, and the slab's parent IS the track.</summary>
        private RectTransform ResolvePutterTrackRect()
        {
            if (_putterTrack != null) return _putterTrack.transform as RectTransform;
            return _putterTimingSlabRT != null ? _putterTimingSlabRT.parent as RectTransform : null;
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
            {
                // ALSO on the wired path, not just the _arrows[0] fallback above: the slab is a
                // child of the cone mesh and its rect is what culls/raycasts it, so a rect left at
                // the old 1009 would outlive the cone it belongs to.
                RectTransform slabRT = _timingSlab.rectTransform;
                slabRT.sizeDelta = new Vector2(slabRT.sizeDelta.x, _coneHeightPx);
                _timingSlab.gameObject.SetActive(false);
            }
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
            if (aiming) ApplyPutterTrackGeometry();
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
            RefreshLabelOffsets();
        }

        // ── Club handle ───────────────────────────────────────────────────────

        private void UpdateClubHandle(ShotInputState state)
        {
            if (_clubHandle == null) return;

            // NOT Clamp01: the base of the cone is 120% now, and clamping here would have drawn
            // an overpowered club at the 100% line while the finger was 108px lower.
            float power         = Mathf.Clamp(state.PowerNormalized, 0f,
                                              ShotController.MaxOverpowerNormalized);
            // THE EXACT INVERSE of the mapping ClubHandleDragger reads the finger through
            // (flick_pull_mapping §3.2), so the drawn club and the finger coincide at 0%, 100% and
            // 120% rather than merely agreeing in shape. Bots reach this same line: they publish a
            // normalised power through SetExternalPower and never place the handle themselves.
            float handleY       = FlickPullMath.ConeLocalYForPower(
                                      power, _handleStartYPx, _coneHeightPx,
                                      _cfgOverride ?? ControlsConfig.Default,
                                      _shotController != null && _shotController.IsPutt);
            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = _coneHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - Mathf.Clamp01(handleY / _coneHeightPx);
            float maxX          = halfBase * widthFraction;

            // HandleFinetune (LIVE), not state.ConeFinetuneX (the latched AIM value): once the
            // upswing latches the aim, the handle must keep following the finger. Using the aim
            // value here made the handle collapse toward the centreline during the flick.
            float xOffset = _puttMode ? 0f : _shotController.HandleFinetune * maxX;
            _clubHandle.anchoredPosition = new Vector2(xOffset, handleY);

            // Scale still saturates at 100%: the overpower ramp is read off the gauge's red arc
            // and the club's depth past the 100% line, not off a club that keeps growing.
            float handleScale = Mathf.Lerp(_minHandleScale, _maxHandleScale, Mathf.Clamp01(power));
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
