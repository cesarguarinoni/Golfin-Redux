using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Attach to the ClubHandle RectTransform.
    // Drag DOWN toward cone base to set power; flick UP to fire at peak power.
    // _releaseToFire: enable in Inspector to fire on any release (old pull-and-hold behavior).
    [RequireComponent(typeof(RectTransform))]
    public class ClubHandleDragger : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private ShotController       _shotController;
        [SerializeField] private RectTransform        _coneRect;
        [SerializeField] private ConeMeshGraphic      _coneGraphic;
        [SerializeField] private TeeIdleGlowController _glowController;
        [Tooltip("The view that RESOLVES the club's rest height from controls.csv. Power is " +
                 "measured from that rest, so the dragger and the drawn club read one number. " +
                 "Wired by FlickConeTicksBuilder — never Find()-ed at runtime.")]
        [SerializeField] private ShotConeView         _coneView;

        private float ConeHeightPx => _coneGraphic != null ? _coneGraphic.HeightPx : 1009f;

        /// <summary>Cone-local y the club rests at, i.e. the ZERO of the pull. Off the view when
        /// it is wired (its Awake has already folded the config in); otherwise re-derived from the
        /// same two keys, so an unwired test rig reads the same rest rather than falling back to
        /// the base and silently restoring the old base-relative mapping.</summary>
        private float HandleRestYPx => _coneView != null
            ? _coneView.HandleRestYPx
            : FlickPullMath.RestYPx(ConeHeightPx, ControlsConfig.Default);

        [Header("Flick Settings")]
        [Tooltip("LEGACY single-frame check. Only used when ShotController's windowed flick gate " +
                 "is disabled (debugDisableFlickGate) — kept so that toggle restores old behavior exactly.")]
        [Range(0f, 200f)]
        [SerializeField] private float _flickThresholdPxPerFrame = 5f;

        [Tooltip("If true, releasing the handle always fires (no flick required). Good for debugging.")]
        [SerializeField] private bool _releaseToFire = false;

        public bool ReleaseToFire { get => _releaseToFire; set => _releaseToFire = value; }

        private bool  _dragging;
        private float _peakPower;
        private float _peakFinetune;

        public void OnPointerDown(PointerEventData e)
        {
            if (_shotController == null || _coneRect == null) return;
            _dragging     = true;
            _peakPower    = 0f;
            _peakFinetune = 0f;
            // Notify glow controller before the drag begins so the glow disarms cleanly.
            _glowController?.OnHandleTouched();
            // Push current spin selection before starting the drag so CommitFlick sees it.
            _shotController.PendingSpinInput = HUD.SpinContext.Spin;
            _shotController.BeginExternalDrag();   // clears the previous swing's touch history
            _shotController.PushTouchSample(e.position);
            ProcessDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            // Push BEFORE ProcessDrag so that the frame the aim latches snapshots the
            // bottom-of-swing finetune, not this frame's already-risen one.
            _shotController.PushTouchSample(e.position);
            ProcessDrag(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;
            _shotController.PushTouchSample(e.position);   // release position closes the window

            bool hasPower = _peakPower > 0.02f;

            if (!hasPower)
            {
                _shotController.CancelExternalDrag();
                return;
            }

            // SetExternalPower is a no-op on finetune once the aim latched, so the frozen
            // bottom-of-swing aim survives this call.
            _shotController.SetExternalPower(_peakPower, _peakFinetune);

            if (_shotController.FlickGateActive)
            {
                // Windowed, stutter-proof gate lives in ShotController; a failed gate resets
                // the swing there rather than firing.
                _shotController.EndExternalDrag(bypassFlickGate: _releaseToFire);
            }
            else
            {
                // debugDisableFlickGate — legacy single-frame behavior, unchanged.
                bool legacyFlick = e.delta.y >= _flickThresholdPxPerFrame;
                if (_releaseToFire || legacyFlick) _shotController.EndExternalDrag(bypassFlickGate: true);
                else                               _shotController.CancelExternalDrag();
            }
        }

        private void ProcessDrag(PointerEventData e)
        {
            Camera uiCam = e.pressEventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _coneRect, e.position, uiCam, out var local);

            // Y=0 = cone base (120%), Y=HandleRestYPx = the club's rest (0%), Y=coneHeightPx = apex.
            // Still clamped to the whole cone: a finger ABOVE the rest is a zero pull, not a
            // negative one, and D5 keeps the lateral reach reading off this same y.
            float handleY = Mathf.Clamp(local.y, 0f, ConeHeightPx);

            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = ConeHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - handleY / ConeHeightPx;
            float maxX          = halfBase * widthFraction;
            float handleX       = Mathf.Clamp(local.x, -maxX, maxX);

            // POWER IS THE TRAVEL FROM THE CLUB'S REST, not the height above the base
            // (flick_pull_mapping D1). It used to be 1 - handleY/ConeHeightPx, which read 31.8%
            // the moment the club was touched and had no 120% at all — the finger ran out of cone
            // at 1.0. The base is now 120%, and a putt caps at 1.0 the way every scheme does.
            float pullPx   = Mathf.Max(0f, HandleRestYPx - handleY);
            float power    = FlickPullMath.Power(pullPx, ControlsConfig.Default, _shotController.IsPutt);
            float finetune = maxX > 0.1f ? handleX / maxX : 0f;

            if (power > _peakPower)
            {
                _peakPower    = power;
                _peakFinetune = finetune;
            }

            _shotController.SetExternalPower(power, finetune);
        }
    }
}
