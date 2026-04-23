using UnityEngine;
using UnityEngine.EventSystems;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Attach to the ClubHandle RectTransform.
    // Pointer down on the handle → drives ShotController via external-drag API.
    // Power and aim are derived from the handle's position within the cone geometry,
    // so the handle follows the pointer 1:1 (clamped inside the cone outline).
    [RequireComponent(typeof(RectTransform))]
    public class ClubHandleDragger : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private ShotController    _shotController;
        [SerializeField] private RectTransform     _coneRect;   // ConeMesh RectTransform
        [SerializeField] private float             _coneHeightPx = 600f;

        private bool _dragging;

        public void OnPointerDown(PointerEventData e)
        {
            if (_shotController == null || _coneRect == null) return;
            _dragging = true;
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
            _shotController.EndExternalDrag();
        }

        private void ProcessDrag(PointerEventData e)
        {
            // Convert screen position to cone-local canvas space.
            // Pass null camera for Screen Space Overlay canvas.
            Camera uiCam = e.pressEventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _coneRect, e.position, uiCam, out var local);

            // Clamp Y to [0, coneHeightPx]: 0 = base (max power), coneHeightPx = apex (zero power).
            float handleY = Mathf.Clamp(local.y, 0f, _coneHeightPx);

            // Cone width at this Y.
            float halfAngleRad  = _shotController.ConeHalfAngleDeg * Mathf.Deg2Rad;
            float halfBase      = _coneHeightPx * Mathf.Tan(halfAngleRad);
            float widthFraction = 1f - handleY / _coneHeightPx;
            float maxX          = halfBase * widthFraction;

            // Clamp X inside cone outline.
            float handleX = Mathf.Clamp(local.x, -maxX, maxX);

            // Power: 0 at apex, 1 at base.
            float power    = 1f - handleY / _coneHeightPx;
            float finetune = maxX > 0.1f ? handleX / maxX : 0f;

            _shotController.SetExternalPower(power, finetune);
        }
    }
}
