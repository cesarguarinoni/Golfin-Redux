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

        [Header("Putter timing slab")]
        [SerializeField] private RectTransform   _putterTimingSlabRT;
        [SerializeField] private float           _putterTrackHeightPx = 1000f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Camera    _worldCamera;
        private Transform _ballTransform;
        private float     _maxCarryYards    = 250f;
        private bool      _lastArrowTrailState;
        private bool      _puttMode;
        private UnityEngine.UI.Image _putterTimingSlabImage;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetMaxCarryYards(float yards) => _maxCarryYards = yards;

        public void SetPuttMode(bool on)
        {
            _puttMode = on;
            if (_coneGraphic  != null) _coneGraphic.enabled = !on;
            if (_targetingLine != null) _targetingLine.gameObject.SetActive(!on);
            // Cache Image for putter slab on first enable.
            if (on && _putterTimingSlabRT != null && _putterTimingSlabImage == null)
                _putterTimingSlabImage = _putterTimingSlabRT.GetComponent<UnityEngine.UI.Image>();
            // Ensure the correct slab is visible/hidden when mode changes.
            if (_timingSlab      != null) _timingSlab.gameObject.SetActive(false);
            if (_putterTimingSlabRT != null) _putterTimingSlabRT.gameObject.SetActive(false);
        }

        public void SetCamera(Camera cam)            => _worldCamera   = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        public void SetOutlineVisible(bool visible)
        {
            if (_coneGraphic != null) _coneGraphic.enabled = visible && !_puttMode;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_coneGraphic != null) _coneGraphic.HeightPx = _coneHeightPx;
            if (_clubHandle != null)
                _clubHandle.sizeDelta = new Vector2(_handleWidth, _handleHeight);
            SetupSlab();
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
        }

        private void OnDisable()
        {
            if (_shotController != null)
                _shotController.OnStateChanged -= HandleStateChanged;
        }

        // ── State handler ─────────────────────────────────────────────────────

        private void HandleStateChanged(ShotInputState state)
        {
            UpdateConeWidth();
            UpdateClubHandle(state);
            UpdateSlab(state);
            UpdateHUD(state);
            UpdateTargetingLine(state);
            ApplyDebugFlags();
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
            if (_puttMode) return;  // cone is disabled; skip dirty-marking the mesh
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

            float xOffset = _puttMode ? 0f : state.ConeFinetuneX * maxX;
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
            _powerHUD.text = $"{pct}%\n{yds:F0} yd";
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

            // Line is always anchored at canvas center (same position as CentralBallWidget).
            // Only rotation is computed from world-space aim direction.
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
            _targetingLine.localRotation    = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
