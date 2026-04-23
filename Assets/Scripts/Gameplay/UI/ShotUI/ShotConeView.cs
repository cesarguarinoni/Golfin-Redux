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
        [SerializeField] private float           _coneHeightPx = 600f;

        [Header("Club handle")]
        [SerializeField] private RectTransform   _clubHandle;
        [SerializeField] private float           _handleYPx    = 80f;

        [Header("Arrows (pool of 3)")]
        [SerializeField] private RectTransform[] _arrows = new RectTransform[3];

        [Header("HUD")]
        [SerializeField] private TextMeshProUGUI _powerHUD;

        [Header("Targeting line")]
        [SerializeField] private RectTransform   _targetingLine;

        // ── Runtime state ─────────────────────────────────────────────────────
        private Camera    _worldCamera;
        private Transform _ballTransform;
        private float     _maxCarryYards = 250f;

        // ── Public API ────────────────────────────────────────────────────────

        // Set before the shot to drive the yards readout.
        public void SetMaxCarryYards(float yards) => _maxCarryYards = yards;

        // Wired by the lab controller in Part E for world-space line projection.
        public void SetCamera(Camera cam)           => _worldCamera   = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_coneGraphic != null) _coneGraphic.HeightPx = _coneHeightPx;
            HideArrows();
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
            UpdateArrows(state);
            UpdateHUD(state);
            UpdateTargetingLine(state);
        }

        // ── Cone width ────────────────────────────────────────────────────────

        private void UpdateConeWidth()
        {
            _coneGraphic.HalfAngleDeg = _shotController.ConeHalfAngleDeg;
            _coneGraphic.HeightPx     = _coneHeightPx;
        }

        // ── Club handle ───────────────────────────────────────────────────────

        // Follows touch: slides from apex (power=0) toward base (power=100%) vertically,
        // and horizontally within the cone outline at that Y.
        private void UpdateClubHandle(ShotInputState state)
        {
            if (_clubHandle == null) return;

            // Y: apex (top) at power=0, base (bottom) at power=100%.
            float handleY       = _coneHeightPx * (1f - Mathf.Clamp01(state.PowerNormalized));
            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = _coneHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - Mathf.Clamp01(handleY / _coneHeightPx);
            float maxX          = halfBase * widthFraction;

            _clubHandle.anchoredPosition = new Vector2(
                state.ConeFinetuneX * maxX,
                handleY);
        }

        // ── Arrows ────────────────────────────────────────────────────────────

        private const float ArrowPhaseStep = 1f / 3f;

        private void HideArrows()
        {
            foreach (var a in _arrows)
                if (a != null) a.gameObject.SetActive(false);
        }

        private void UpdateArrows(ShotInputState state)
        {
            bool showArrows = state.State == ShotState.Timing;
            for (int i = 0; i < _arrows.Length; i++)
            {
                var arrow = _arrows[i];
                if (arrow == null) continue;

                arrow.gameObject.SetActive(showArrows);
                if (!showArrows) continue;

                float progress = (state.ArrowProgress01 + i * ArrowPhaseStep) % 1f;
                float arrowY   = progress * _coneHeightPx;

                // X stays at center axis (arrows travel straight up the cone).
                arrow.anchoredPosition = new Vector2(0f, arrowY);
            }
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

            int   pct  = Mathf.RoundToInt(state.PowerNormalized * 100f);
            float yds  = _maxCarryYards * state.PowerNormalized;
            _powerHUD.text = $"{pct}%\n{yds:F0} yd";
        }

        // ── Targeting line ────────────────────────────────────────────────────

        // Part D: placeholder thin vertical line above cone apex.
        // Part E: replace with world→screen projection along aim heading.
        private void UpdateTargetingLine(ShotInputState state)
        {
            if (_targetingLine == null) return;

            bool show = state.State is ShotState.Aiming
                                    or ShotState.Pulling
                                    or ShotState.Timing
                                    or ShotState.Flicking;
            _targetingLine.gameObject.SetActive(show);

            if (!show || _worldCamera == null || _ballTransform == null) return;

            // World-space projection (active once Part E wires camera + ball).
            Vector3 ballScreen = _worldCamera.WorldToScreenPoint(_ballTransform.position);
            if (ballScreen.z < 0f) { _targetingLine.gameObject.SetActive(false); return; }

            Vector3 aimDir     = new Vector3(
                Mathf.Cos(state.AimYawRadians), 0f, Mathf.Sin(state.AimYawRadians));
            Vector3 targetWorld  = _ballTransform.position + aimDir * ControlsConfig.Default.TargetingLineLengthMeters;
            Vector3 targetScreen = _worldCamera.WorldToScreenPoint(targetWorld);

            Vector2 lineDir  = ((Vector2)targetScreen - (Vector2)ballScreen).normalized;
            float   lineLen  = Vector2.Distance(ballScreen, targetScreen);
            float   angle    = Mathf.Atan2(lineDir.y, lineDir.x) * Mathf.Rad2Deg - 90f;

            _targetingLine.anchoredPosition = (Vector2)ballScreen;
            _targetingLine.sizeDelta        = new Vector2(_targetingLine.sizeDelta.x, lineLen);
            _targetingLine.localRotation    = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
