using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class HoleIndicatorWidget : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] RectTransform _root;          // sliding root — top-LEFT anchored to canvas; widget mutates anchoredPosition.x
        [SerializeField] RectTransform _dataChip;      // static child of _root (the 100x100 chip visual; doesn't move independently)
        [SerializeField] RectTransform _arrowLine;     // child of _root; pivot top-center; travels around chip perimeter 10px inset
        [SerializeField] TMP_Text      _distanceText;

        [Header("Tracking config")]
        [SerializeField] float _edgePaddingPx = 48f;   // right-side margin from canvas edge
        [SerializeField] float _leftMinX      = 164f;  // wind indicator right edge (148) + 16px gap
        [SerializeField] float _fadeStartDot  = 0.3f;  // dot(cam.forward, toPin) at which fade begins; 0=90°, 0.3≈73°, 0.5=60°

        Camera      _cam;
        Transform   _ballTransform;
        CanvasGroup _canvasGroup;

        public void SetCamera(Camera cam)            => _cam           = cam;
        public void SetBallTransform(Transform ball) => _ballTransform = ball;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            HoleContext.OnChanged += OnHoleChanged;
            OnHoleChanged();
        }
        void OnDisable() { HoleContext.OnChanged -= OnHoleChanged; }
        void OnHoleChanged() { /* values applied next LateUpdate */ }

        void LateUpdate()
        {
            if (_cam == null || _root == null) return;
            if (HoleContext.PinWorld == Vector3.zero) return;

            // Distance text
            Transform ball = _ballTransform;
            float meters = ball != null ? Vector3.Distance(ball.position, HoleContext.PinWorld) : 0f;
            float yards  = meters * 1.0936133f;
            if (_distanceText != null) _distanceText.text = $"{yards:F0} yds";

            // Project pin to canvas coords
            Vector3 pinScreen = _cam.WorldToScreenPoint(HoleContext.PinWorld);
            var canvas     = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRect = canvas.GetComponent<RectTransform>();
            float canvasWidth  = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;
            float scaleFactor  = canvas.scaleFactor;
            if (scaleFactor <= 0f) scaleFactor = 1f;

            float pinCanvasX        = pinScreen.x / scaleFactor;
            float pinCanvasYFromTop = canvasHeight - (pinScreen.y / scaleFactor);
            bool  pinBehind         = pinScreen.z < 0f;

            // Fade: fully visible when pin is well within front arc; gone by 90°; stays gone past 90°.
            // _fadeStartDot controls how many degrees before 90° the fade begins (0.3 ≈ 73°, 0.5 = 60°).
            if (_canvasGroup != null)
            {
                Vector3 toPin = HoleContext.PinWorld - _cam.transform.position;
                float dist = toPin.magnitude;
                float dot = dist > 0.001f ? Vector3.Dot(_cam.transform.forward, toPin / dist) : 1f;
                // dot/_fadeStartDot: 1 at fade-start angle → alpha=1; 0 at 90° → alpha=0; negative → 0
                _canvasGroup.alpha = _fadeStartDot > 0.001f ? Mathf.Clamp01(dot / _fadeStartDot) : (dot > 0f ? 1f : 0f);
            }

            // Determine on-screen / off-screen
            bool flagOffLeft  = pinBehind || pinCanvasX < 0f;
            bool flagOffRight = !pinBehind && pinCanvasX > canvasWidth;
            bool flagOnScreen = !pinBehind && !flagOffLeft && !flagOffRight;

            // Chip width for clamp
            float chipWidth = _dataChip != null ? _dataChip.rect.width : 100f;

            // Compute desired root X (root is top-left anchored, so X is in canvas-space from left edge)
            float targetX;
            if (flagOnScreen)
            {
                targetX = pinCanvasX - chipWidth * 0.5f;
            }
            else if (flagOffLeft)
            {
                targetX = _leftMinX;
            }
            else
            {
                targetX = canvasWidth - chipWidth - _edgePaddingPx;
            }
            targetX = Mathf.Clamp(targetX, _leftMinX, canvasWidth - chipWidth - _edgePaddingPx);

            var rootPos = _root.anchoredPosition;
            rootPos.x = targetX;
            _root.anchoredPosition = rootPos;

            // Tail: pivot travels around all 4 chip edges, 10px inset, always pointing toward pin
            if (_arrowLine != null)
            {
                _arrowLine.gameObject.SetActive(true);

                // Chip center in canvas-space (Y measured from canvas top)
                float chipCenterCanvasX    = _root.anchoredPosition.x + chipWidth * 0.5f;
                float chipCenterYFromTop   = -_root.anchoredPosition.y + 50f; // anchoredPosition.y is negative

                float dx        = pinCanvasX - chipCenterCanvasX;
                float dyFromTop = pinCanvasYFromTop - chipCenterYFromTop;

                // Inset rect half-extents: chip is 100×100, inset 10px → 40px from center
                const float halfExt = 40f;

                // Ray-rect intersection: find smallest positive t so center + t*dir hits the inset boundary
                float t = float.MaxValue;
                if (Mathf.Abs(dx) > 0.001f)
                {
                    float tx = (dx > 0f ? halfExt : -halfExt) / dx;
                    if (tx > 0f) t = Mathf.Min(t, tx);
                }
                if (Mathf.Abs(dyFromTop) > 0.001f)
                {
                    float ty = (dyFromTop > 0f ? halfExt : -halfExt) / dyFromTop;
                    if (ty > 0f) t = Mathf.Min(t, ty);
                }

                if (t < float.MaxValue - 1f)
                {
                    // Hit point in canvas-space
                    float hitCanvasX    = chipCenterCanvasX + t * dx;
                    float hitYFromTop   = chipCenterYFromTop + t * dyFromTop;

                    // Convert to root-local RectTransform coords (root anchored top-left)
                    var tailPos = _arrowLine.anchoredPosition;
                    tailPos.x = hitCanvasX - _root.anchoredPosition.x;
                    tailPos.y = -_root.anchoredPosition.y - hitYFromTop; // root Y is negative; local Y is positive-up
                    _arrowLine.anchoredPosition = tailPos;

                    // Rotate tail to point away from center (toward pin)
                    Vector2 dir = new Vector2(dx, -dyFromTop); // flip Y for Unity's Y-up
                    float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _arrowLine.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);
                }
                else
                {
                    // Pin exactly at chip center — default to bottom-center
                    var tailPos = _arrowLine.anchoredPosition;
                    tailPos.x = chipWidth * 0.5f;
                    tailPos.y = -90f;
                    _arrowLine.anchoredPosition = tailPos;
                    _arrowLine.localRotation = Quaternion.identity;
                }
            }
        }
    }
}
