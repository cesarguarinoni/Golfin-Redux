using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Attach to the ClubHandle RectTransform.
    // Drag down to set power, flick UP to fire at peak power.
    // Cancels if released without an upward flick.
    [RequireComponent(typeof(RectTransform))]
    public class ClubHandleDragger : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private ShotController    _shotController;
        [SerializeField] private RectTransform     _coneRect;
        [SerializeField] private float             _coneHeightPx = 600f;
        [SerializeField] private float             _flickThresholdPxPerFrame = 20f;

        private bool  _dragging;
        private float _peakPower;
        private float _peakFinetune;

        public void OnPointerDown(PointerEventData e)
        {
            if (_shotController == null || _coneRect == null) return;
            _dragging     = true;
            _peakPower    = 0f;
            _peakFinetune = 0f;
            _shotController.BeginExternalDrag();
            ProcessDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging) return;
            ProcessDrag(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging) return;
            _dragging = false;

            // Fire only when the pointer flicks upward (toward ball/apex).
            // e.delta is in screen pixels per frame; positive Y = up.
            bool isFlick = e.delta.y >= _flickThresholdPxPerFrame && _peakPower > 0.02f;
            if (isFlick)
            {
                _shotController.SetExternalPower(_peakPower, _peakFinetune);
                _shotController.EndExternalDrag();
            }
            else
            {
                _shotController.CancelExternalDrag();
            }
        }

        private void ProcessDrag(PointerEventData e)
        {
            Camera uiCam = e.pressEventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _coneRect, e.position, uiCam, out var local);

            // Y=0 = cone base (max power), Y=coneHeightPx = apex (zero power).
            float handleY = Mathf.Clamp(local.y, 0f, _coneHeightPx);

            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = _coneHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - handleY / _coneHeightPx;
            float maxX          = halfBase * widthFraction;
            float handleX       = Mathf.Clamp(local.x, -maxX, maxX);

            float power    = 1f - handleY / _coneHeightPx;
            float finetune = maxX > 0.1f ? handleX / maxX : 0f;

            // Track peak for flick-fire.
            if (power > _peakPower)
            {
                _peakPower    = power;
                _peakFinetune = finetune;
            }

            _shotController.SetExternalPower(power, finetune);
        }
    }
}
