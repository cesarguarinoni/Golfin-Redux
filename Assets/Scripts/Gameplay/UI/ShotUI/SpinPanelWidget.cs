using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Config;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Spin selector UX (Order 354, iter-6 circular-dim + no-whitewash fix).
    ///
    /// iter-5 fixed the donut-hole-oversize bug (anchor fix + BallSpriteVisualRadiusFrac).
    /// iter-6 (Cesar rejection #2) fixes two visual regressions from iter-5:
    ///
    ///   D-1 CIRCULAR DIM: iter-5 GenerateDonutTexture only had an INNER hole cutoff.
    ///     Pixels beyond the ball's edge stayed DARK → the 600×600 square Image showed
    ///     a square box of dim, not a circular rim. Fix: add outerRadiusFrac parameter
    ///     (= visualFrac ≈ 0.957 of the texture half-width) — pixels beyond the ball's
    ///     circular edge are alpha=0. The texture is now a true circular annulus:
    ///       alpha=0 inside hole (spin-allowed), dark in ring, alpha=0 outside ball circle.
    ///
    ///   D-2 NO WHITE WASH: SpinActiveDisc (_activeDiscRt) had a Knob sprite at alpha=0.35
    ///     covering the ENTIRE disc interior → bright white overlay on the ball. The spec
    ///     requires the pristine ball inside the cut. Fix: set _activeDiscRt Image alpha=0
    ///     (invisible fill). The disc edge delineation comes from the donut feather only.
    ///
    /// D3 physics contract preserved: normalization divisor = _ballImageRadius = cap.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SpinPanelWidget : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        // ─── Inspector references ──────────────────────────────────────────────
        [SerializeField] private Image         _ballImage;
        [SerializeField] private RectTransform _spinDot;
        [SerializeField] private Sprite        _defaultBallSprite;
        [SerializeField] private GameObject    _aimingCone;
        [SerializeField] private GameObject    _centralBall;   // 1a: hidden while open

        // Gray-out donut: a full-size Image (600x600 = BallImage size) that receives a
        // runtime-generated donut texture: transparent inside the active disc, dark outside.
        // _activeDiscRt is a thin outline ring at the disc edge for visual delineation.
        // _grayOutMaskRt is unused in iter-2 but kept to avoid breaking serialized wiring.
        [SerializeField] private RectTransform _activeDiscRt;      // thin outline ring at disc edge
        [SerializeField] private RectTransform _grayOutRt;         // full-size donut Image (600x600)
        [SerializeField] private RectTransform _grayOutMaskRt;     // unused in iter-2; kept for wiring

        // Kept for legacy wiring; actual close is driven by a runtime Button (see Open()).
        [SerializeField] private OutsideClickCatcher _dimBackground;

        // Donut texture resolution (power-of-2 for GPU compat).
        const int DonutTexSize = 512;

        // ─── Runtime state ────────────────────────────────────────────────────
        Button     _dimCloseBtn;
        GameObject _dimGo;
        float      _activePxRadius;     // set each Open() from BallContext.SelectedSpinStat

        /// <summary>
        /// Visible painted ball radius in canvas pixels — measured at runtime from
        /// BallImage rect × BallSpriteVisualRadiusFrac (accounts for sprite alpha padding).
        /// Used as both the disc cap and the px→spin normalization divisor (disc edge = ±1.0).
        /// </summary>
        float      _ballImageRadius;

        Texture2D  _donutTex;           // runtime-generated donut; reused/overwritten each Open()

        // ─── Open / Close ─────────────────────────────────────────────────────

        public void Open()
        {
            // Set ball sprite FIRST so rect is correct when we measure it below.
            if (_ballImage != null)
                _ballImage.sprite = BallContext.SelectedThumbnail != null
                    ? BallContext.SelectedThumbnail
                    : _defaultBallSprite;

            // ── Fix SpinGrayOut stretched anchors (iter-5 root-cause fix) ──────────────
            // SpinGrayOut was authored with anchorMin=(0,0) anchorMax=(1,1) (stretch-to-parent).
            // With parent BallImage at 600×600 and stretch anchors, rect.width = 1200 (not 600).
            // This made the donut texture hole appear 2× as large as the ball.
            // Reset to center-anchored so sizeDelta=(600,600) → rect.width=600 (= BallImage).
            if (_grayOutRt != null)
            {
                _grayOutRt.anchorMin = new Vector2(0.5f, 0.5f);
                _grayOutRt.anchorMax = new Vector2(0.5f, 0.5f);
                _grayOutRt.pivot     = new Vector2(0.5f, 0.5f);
                _grayOutRt.anchoredPosition = Vector2.zero;
                // sizeDelta stays (600,600) — now renders at 600×600 like BallImage
                _grayOutRt.sizeDelta = new Vector2(600f, 600f);
            }

            // Measure ball radius from live RectTransform × sprite visual fraction.
            // BallImage rect.width = 600 canvas-px. The painted ball circle is smaller than
            // the full rect due to transparent alpha-padding in the sprite asset.
            // BallSpriteVisualRadiusFrac (default 0.957, tunable in controls.csv) converts
            // RT half-width → visible-ball-edge radius: 300 × 0.957 ≈ 287 canvas-px.
            // This is the radius at which the donut hole edge aligns with the painted ball rim.
            float visualFrac = ControlsConfig.Default.BallSpriteVisualRadiusFrac;
            if (visualFrac <= 0f || visualFrac > 1f) visualFrac = 0.957f;  // guard against bad CSV

            _ballImageRadius = (_ballImage != null)
                ? _ballImage.rectTransform.rect.width * 0.5f * visualFrac
                : 220f;  // safe fallback for null-ref safety

            // 1a: hide central ball
            if (_centralBall != null) _centralBall.SetActive(false);
            // hide aim cone
            if (_aimingCone  != null) _aimingCone.SetActive(false);

            // 1c: compute active disc radius from ball spin stat
            float floor     = ControlsConfig.Default.SpinSelectorFloorRadius01;
            float radius01  = Mathf.Lerp(floor, 1f, (BallContext.SelectedSpinStat + 10f) / 20f);
            _activePxRadius = radius01 * _ballImageRadius;

            ActivateDim();
            gameObject.SetActive(true);

            // Size the active disc visual
            UpdateDiscVisuals();

            SnapDotToCurrent();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (_dimCloseBtn != null) _dimCloseBtn.onClick.RemoveListener(Close);
            if (_dimGo       != null) _dimGo.SetActive(false);
            // 1a: restore central ball
            if (_centralBall != null) _centralBall.SetActive(true);
            if (_aimingCone  != null) _aimingCone.SetActive(true);
        }

        // ─── IPointerDownHandler / IDragHandler (1c continuous drag) ──────────

        public void OnPointerDown(PointerEventData eventData)
        {
            ApplyDragPoint(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            ApplyDragPoint(eventData);
        }

        void ApplyDragPoint(PointerEventData eventData)
        {
            // Convert screen point → local rect point.
            // The drag surface is _ballImage which is a child of this panel.
            RectTransform dragRect = _ballImage != null
                ? _ballImage.rectTransform
                : GetComponent<RectTransform>();

            Camera cam = eventData.pressEventCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    dragRect, eventData.position, cam, out Vector2 local))
                return;

            // pxFromCenter is the vector from disc center to touch point (in dragRect local pixels).
            Vector2 pxFromCenter = local; // dragRect pivot is center (0.5, 0.5)

            // Radial clamp to active disc
            if (pxFromCenter.magnitude > _activePxRadius)
                pxFromCenter = pxFromCenter.normalized * _activePxRadius;

            // Position dot
            if (_spinDot != null)
                _spinDot.anchoredPosition = pxFromCenter;

            // Push spin value: disc edge → spin ±1.0.
            // Normalise by _ballImageRadius (same divisor as the cap) so disc-edge = ±1.0 (D3 contract).
            Vector2 value = pxFromCenter / _ballImageRadius;
            SpinContext.SetSpin(value);
        }

        // ─── Legacy entry point (kept for ActionButtonsBuilder compatibility) ──
        // NOTE: SelectPosition is RETIRED — the builder no longer wires 5 buttons.
        // Keeping the signature (as a no-op stub) to avoid a build error if any
        // stale scene serialization still holds a reference to it.
        [System.Obsolete("Replaced by continuous drag in Order 354. Do not call.")]
        public void SelectPosition(int idx)
        {
            // no-op — continuous drag path replaced this
            Debug.LogWarning("[SpinPanelWidget] SelectPosition called; this is obsolete (Order 354).");
        }

        // ─── Internals ────────────────────────────────────────────────────────

        void ActivateDim()
        {
            if (_dimGo == null)
            {
                if (_dimBackground != null)
                    _dimGo = _dimBackground.gameObject;
                else if (transform.parent != null)
                {
                    var t = transform.parent.Find("OutsideClickCatcher_Spin");
                    if (t != null) _dimGo = t.gameObject;
                }
            }
            if (_dimGo == null) return;

            _dimCloseBtn = _dimGo.GetComponent<Button>();
            if (_dimCloseBtn == null)
            {
                _dimCloseBtn = _dimGo.AddComponent<Button>();
                var img = _dimGo.GetComponent<Image>();
                if (img != null) _dimCloseBtn.targetGraphic = img;
            }
            _dimCloseBtn.onClick.RemoveListener(Close);
            _dimCloseBtn.onClick.AddListener(Close);
            _dimGo.SetActive(true);
        }

        void UpdateDiscVisuals()
        {
            // ── 1. Generate the circular-annulus donut texture on _grayOutRt ─────
            // iter-6 FIX D-1: Texture must be a CIRCULAR ANNULUS — alpha=0 inside the hole,
            // dark in the ring between hole and ball edge, and alpha=0 OUTSIDE the ball circle.
            // Previously only the inner hole was transparent; all corners of the 600×600 square
            // were dark → square box silhouette. Now we pass outerRadiusFrac = visualFrac so
            // pixels beyond the ball's circular edge become transparent.
            if (_grayOutRt != null)
            {
                var grayImg = _grayOutRt.GetComponent<Image>();
                if (grayImg != null)
                {
                    // After the Open() anchor fix grayOutHalfPx = 300 (= BallImage half-width).
                    float grayOutHalfPx = _grayOutRt.rect.width * 0.5f;
                    if (grayOutHalfPx < 1f) grayOutHalfPx = 300f;  // guard against Layout-not-run-yet

                    float holeRadiusFrac = _activePxRadius / grayOutHalfPx;
                    float visualFrac = ControlsConfig.Default.BallSpriteVisualRadiusFrac;
                    if (visualFrac <= 0f || visualFrac > 1f) visualFrac = 0.957f;
                    // Inner hole capped to visible ball edge
                    holeRadiusFrac = Mathf.Min(holeRadiusFrac, visualFrac);
                    // Outer boundary = visual ball edge (nothing outside ball is dimmed)
                    float outerRadiusFrac = visualFrac;

                    Texture2D donut = GenerateDonutTexture(DonutTexSize, holeRadiusFrac, outerRadiusFrac, darkAlpha: 0.55f);
                    if (_donutTex != null)
                        Destroy(_donutTex);
                    _donutTex = donut;

                    grayImg.sprite = Sprite.Create(
                        donut,
                        new Rect(0, 0, DonutTexSize, DonutTexSize),
                        new Vector2(0.5f, 0.5f));
                    grayImg.color = Color.white;  // texture alpha drives dim per-pixel
                    grayImg.type  = Image.Type.Simple;
                    grayImg.preserveAspect = false;
                    grayImg.raycastTarget  = false;
                }
            }

            // ── 2. Disc delineation — iter-6 FIX D-2: REMOVE white wash ─────────
            // iter-5 set SpinActiveDisc (_activeDiscRt) to alpha=0.35 Knob covering the
            // whole disc interior → white wash over ball. Spec requires pristine ball inside
            // the cut. Fix: set Image alpha=0 (invisible). Edge delineation is provided by
            // the donut feather at the inner hole boundary.
            if (_activeDiscRt != null)
            {
                _activeDiscRt.sizeDelta = new Vector2(_activePxRadius * 2f, _activePxRadius * 2f);
                var outlineImg = _activeDiscRt.GetComponent<Image>();
                if (outlineImg != null)
                {
                    // alpha=0 — no overlay on ball (D-2 fix). The donut feather provides
                    // sufficient visual delineation of the active area boundary.
                    outlineImg.color = new Color(1f, 1f, 1f, 0f);
                    outlineImg.raycastTarget = false;
                }
            }

            // _grayOutMaskRt: unused; leave as-is
        }

        /// <summary>
        /// Generates a Texture2D that is a CIRCULAR ANNULUS (iter-6 D-1 fix):
        ///   - alpha=0  inside the inner hole (radius = holeRadiusFrac * texHalf)
        ///   - dark (RGBA 0,0,0,darkAlpha) in the ring between hole and ball edge
        ///   - alpha=0  OUTSIDE the ball circle (radius = outerRadiusFrac * texHalf)
        ///
        /// Both boundaries have a small feather band for anti-aliasing.
        /// This ensures no dim pixels appear in the square corners of the Image rect.
        /// </summary>
        static Texture2D GenerateDonutTexture(int texSize, float holeRadiusFrac, float outerRadiusFrac, float darkAlpha)
        {
            var tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;

            float center        = texSize * 0.5f;
            float holeRadiusPx  = holeRadiusFrac  * center;   // inner boundary (hole edge)
            float outerRadiusPx = outerRadiusFrac * center;   // outer boundary (ball edge)

            // Small feather band at each boundary for smooth antialiasing
            float feather = Mathf.Max(2f, texSize * 0.008f);  // ~4px at 512

            Color dark  = new Color(0f, 0f, 0f, darkAlpha);
            Color clear = new Color(0f, 0f, 0f, 0f);

            var pixels = new Color[texSize * texSize];
            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dx   = x - center;
                    float dy   = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    Color pixel;

                    if (dist <= holeRadiusPx - feather)
                    {
                        // Inside active disc — fully transparent (pristine ball shows through)
                        pixel = clear;
                    }
                    else if (dist <= holeRadiusPx + feather)
                    {
                        // Feather at inner hole edge: clear → dark
                        float t = (dist - (holeRadiusPx - feather)) / (feather * 2f);
                        pixel = Color.Lerp(clear, dark, t);
                    }
                    else if (dist <= outerRadiusPx - feather)
                    {
                        // In the dim ring between hole and ball edge
                        pixel = dark;
                    }
                    else if (dist <= outerRadiusPx + feather)
                    {
                        // Feather at outer ball edge: dark → clear
                        float t = (dist - (outerRadiusPx - feather)) / (feather * 2f);
                        pixel = Color.Lerp(dark, clear, t);
                    }
                    else
                    {
                        // Outside the ball circle — fully transparent (no square corners)
                        pixel = clear;
                    }

                    pixels[y * texSize + x] = pixel;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        void SnapDotToCurrent()
        {
            // Place dot at current SpinContext.Spin, radially clamped to the active disc.
            // Reconstruct px position using _ballImageRadius so Spin=1.0 maps to disc-edge.
            Vector2 pxPos = SpinContext.Spin * _ballImageRadius;
            if (pxPos.magnitude > _activePxRadius)
                pxPos = pxPos.normalized * _activePxRadius;
            if (_spinDot != null)
                _spinDot.anchoredPosition = pxPos;
        }
    }
}
