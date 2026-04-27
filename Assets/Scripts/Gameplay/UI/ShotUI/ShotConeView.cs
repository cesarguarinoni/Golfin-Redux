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
        [SerializeField] private float           _handleYPx    = 80f;

        [Header("Timing slab")]
        [SerializeField] private TimingSlabGraphic _timingSlab;

        // Legacy arrow pool — kept for inspector compatibility; all are disabled at runtime.
        [Header("Arrows (legacy — disabled)")]
        [SerializeField] private RectTransform[] _arrows = new RectTransform[3];

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI _powerHUD;

        [Header("Targeting line")]
        [SerializeField] private RectTransform   _targetingLine;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Camera    _worldCamera;
        private Transform _ballTransform;
        private float     _maxCarryYards    = 250f;
        private bool      _lastArrowTrailState;

        // ── Public API ────────────────────────────────────────────────────────

        public void SetMaxCarryYards(float yards) => _maxCarryYards = yards;

        public void SetCamera(Camera cam)            => _worldCamera   = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        public void SetOutlineVisible(bool visible)
        {
            if (_coneGraphic != null) _coneGraphic.enabled = visible;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_coneGraphic != null) _coneGraphic.HeightPx = _coneHeightPx;
            _clubHandle?.GetComponent<ClubHandleDragger>()?.SetConeHeight(_coneHeightPx);
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
            _coneGraphic.HalfAngleDeg = _shotController.ConeHalfAngleDeg;
            _coneGraphic.HeightPx     = _coneHeightPx;
        }

        // ── Club handle ───────────────────────────────────────────────────────

        private void UpdateClubHandle(ShotInputState state)
        {
            if (_clubHandle == null) return;

            float handleY       = _coneHeightPx * (1f - Mathf.Clamp01(state.PowerNormalized));
            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = _coneHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - Mathf.Clamp01(handleY / _coneHeightPx);
            float maxX          = halfBase * widthFraction;

            _clubHandle.anchoredPosition = new Vector2(
                state.ConeFinetuneX * maxX,
                handleY);
        }

        // ── Timing slab ───────────────────────────────────────────────────────

        private void UpdateSlab(ShotInputState state)
        {
            if (_timingSlab == null) return;

            bool show = state.State == ShotState.Timing;
            _timingSlab.gameObject.SetActive(show);
            if (!show) return;

            float p = Mathf.Clamp01(state.ArrowProgress01);
            _timingSlab.SetConeParams(_coneHeightPx, _shotController.ConeHalfAngleDeg);
            _timingSlab.CurrentY01 = p;
            _timingSlab.color      = SlabColorFromProgress(p);
        }

        // Salmon (base) → cream (mid) → mint (apex) — pastel slab colors from Figma reference.
        private static Color SlabColorFromProgress(float p)
        {
            if (p <= ConeBandPalette.BandGoldY01)
            {
                float t = Mathf.InverseLerp(ConeBandPalette.BandRedY01, ConeBandPalette.BandGoldY01, p);
                return Color.Lerp(ConeBandPalette.SlabColorRed, ConeBandPalette.SlabColorGold, t);
            }
            if (p <= ConeBandPalette.BandGreenY01)
            {
                float t = Mathf.InverseLerp(ConeBandPalette.BandGoldY01, ConeBandPalette.BandGreenY01, p);
                return Color.Lerp(ConeBandPalette.SlabColorGold, ConeBandPalette.SlabColorGreen, t);
            }
            return ConeBandPalette.SlabColorGreen;
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
            if (_targetingLine == null) return;

            bool show = state.State is ShotState.Aiming
                                    or ShotState.Pulling
                                    or ShotState.Timing
                                    or ShotState.Flicking;
            _targetingLine.gameObject.SetActive(show);

            if (!show || _worldCamera == null || _ballTransform == null) return;

            Vector3 ballScreen = _worldCamera.WorldToScreenPoint(_ballTransform.position);
            if (ballScreen.z < 0f) { _targetingLine.gameObject.SetActive(false); return; }

            Vector3 aimDir       = new Vector3(
                Mathf.Cos(state.AimYawRadians), 0f, Mathf.Sin(state.AimYawRadians));
            Vector3 targetWorld  = _ballTransform.position + aimDir * ControlsConfig.Default.TargetingLineLengthMeters;
            Vector3 targetScreen = _worldCamera.WorldToScreenPoint(targetWorld);

            Vector2 lineDir = ((Vector2)targetScreen - (Vector2)ballScreen).normalized;
            float   lineLen = Vector2.Distance(ballScreen, targetScreen);
            float   angle   = Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg - 90f;

            _targetingLine.anchoredPosition = (Vector2)ballScreen;
            _targetingLine.sizeDelta        = new Vector2(_targetingLine.sizeDelta.x, lineLen);
            _targetingLine.localRotation    = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
