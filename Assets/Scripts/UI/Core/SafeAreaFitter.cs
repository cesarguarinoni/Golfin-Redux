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

        [Tooltip("Baseline inset per edge already handled by the layout, in screen pixels. 0 = apply the FULL " +
                 "safe area (default). When >0, only the EXCESS beyond this baseline is applied. Used by the top " +
                 "bar (safe_area_top_bar): the chrome is authored to already clear the iPhone 14 notch " +
                 "(47pt = 141px), so with baseline 141 it does NOT move on an iPhone 14 and moves only the extra " +
                 "on a larger cutout — e.g. the 14 Pro Max Dynamic Island (59pt = 177px) nudges 177-141 = 36px.")]
        [SerializeField] private float _baselineInsetPixels = 0f;

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

            // Per-edge inset in pixels.
            float leftInset   = safeArea.x;
            float bottomInset = safeArea.y;
            float rightInset  = Screen.width  - (safeArea.x + safeArea.width);
            float topInset    = Screen.height - (safeArea.y + safeArea.height);

            // Optional baseline: the content is already laid out to clear this much inset, so apply only the
            // EXCESS beyond it (0 = apply the full safe area, original behaviour). This makes the move relative
            // to a reference device (the iPhone 14 notch) instead of an absolute shift.
            if (_baselineInsetPixels > 0f)
            {
                leftInset   = Mathf.Max(0f, leftInset   - _baselineInsetPixels);
                rightInset  = Mathf.Max(0f, rightInset  - _baselineInsetPixels);
                topInset    = Mathf.Max(0f, topInset    - _baselineInsetPixels);
                bottomInset = Mathf.Max(0f, bottomInset - _baselineInsetPixels);
            }

            // Convert pixel-space insets to normalised anchor coordinates
            var anchorMin = new Vector2(leftInset / Screen.width, bottomInset / Screen.height);
            var anchorMax = new Vector2(
                1f - rightInset / Screen.width,
                1f - topInset   / Screen.height);

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;

            _lastSafeArea    = safeArea;
            _lastOrientation = Screen.orientation;
        }
    }
}
