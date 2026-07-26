using UnityEngine;

namespace GolfinRedux.UI.Core
{
    /// <summary>
    /// Insets a RectTransform to match Screen.safeArea so UI elements avoid
    /// the Dynamic Island (top notch) and home indicator (bottom) on notch/
    /// Dynamic Island iPhones. Attach to a full-screen canvas panel that
    /// wraps the top-level UI containers you want inset.
    ///
    /// ORDER 930 ATTACHMENT NOTE: This component is authored here but NOT yet
    /// attached to any production canvas. Cesar decides which panels receive
    /// it (Phase A2 – smoke test). Full UI inset pass deferred to Order 930.
    ///
    /// Usage:
    ///   1. Create an empty child GameObject under your Canvas named "SafeArea".
    ///   2. Set its RectTransform to stretch (anchorMin=0,0 / anchorMax=1,1 / offsets=0).
    ///   3. Attach this component.
    ///   4. Move your top-level UI panels inside "SafeArea".
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("GolfinRedux/UI/Safe Area Fitter")]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Tooltip("If true, re-apply every frame (needed for orientation changes). If false, only apply on Awake.")]
        [SerializeField] private bool _pollEveryFrame = true;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea = Rect.zero;
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (!_pollEveryFrame) return;

            // Recalculate only when safe area or orientation actually changes
            if (Screen.safeArea != _lastSafeArea || Screen.orientation != _lastOrientation)
                Apply();
        }

        private void Apply()
        {
            if (_rectTransform == null)
                _rectTransform = GetComponent<RectTransform>();

            Rect safeArea = Screen.safeArea;

            // Guard against zero screen size (can happen in Editor before first render)
            if (Screen.width == 0 || Screen.height == 0) return;

            // Convert pixel-space safe area to normalised anchor coordinates
            var anchorMin = new Vector2(safeArea.x / Screen.width, safeArea.y / Screen.height);
            var anchorMax = new Vector2(
                (safeArea.x + safeArea.width)  / Screen.width,
                (safeArea.y + safeArea.height) / Screen.height);

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea    = safeArea;
            _lastOrientation = Screen.orientation;
        }
    }
}
