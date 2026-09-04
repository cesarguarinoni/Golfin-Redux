using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// map_view_v2 §6 — the HUD-style chip that follows the map's landing point L and reads out the
    /// distance the player has placed plus the distance from there to the pin.
    ///
    /// Built in code (the spec allows either a prefab or code, "but the LOOK must match WindIndicator"):
    /// a white rounded body with a coloured header strip, exactly the two-tone chip the WindIndicator /
    /// HoleIndicator backplate draws. Rounded corners come from a generated 9-sliced sprite rather than
    /// an <c>Outline</c> component (PIPELINE_HARDENING §12 C5) and the drop shadow is a second, offset
    /// copy of the same sprite behind the body — a <c>Shadow</c> component only shifts the graphic, it
    /// cannot blur, and stacking two graphics is what the Figma's (0,4)/blur-8/α30 actually is.
    ///
    /// Two states, driven entirely by <see cref="Set"/>:
    ///   normal    header navy  <c>#001E39</c>  "195 yd"                       body "to pin 123 yd"
    ///   over-range header red  <c>#F23A33</c>  "232 yd · OUT OF RANGE"        body "Driver max 215 yd — …"
    ///
    /// Every string comes from <see cref="LocalizationManager"/>; the distances go through
    /// <see cref="HoleIndicatorWidget.FormatDistance"/> so the chip, the pin chip and the HUD chip can
    /// never disagree about "yds" vs "mts".
    /// </summary>
    public class MapTargetReadoutWidget : MonoBehaviour
    {
        // Design tokens, in Figma px on the 1170×2532 frame. The caller scales the whole chip by the
        // device/canvas ratio, so these stay the numbers that are in the fidelity table.
        public const float kHeaderFontPx = 44f;
        public const float kBodyFontPx   = 23f;
        public const float kPadXPx       = 26f;
        public const float kHeaderHPx    = 78f;
        public const float kBodyHPx      = 52f;
        public const float kShadowDyPx   = -4f;

        private static readonly Color kNavy  = new Color(0f, 30f / 255f, 57f / 255f, 1f);   // #001E39
        private static readonly Color kRed   = new Color(242f / 255f, 58f / 255f, 51f / 255f, 1f); // #F23A33
        private static readonly Color kShade = new Color(0f, 0f, 0f, 0.30f);

        private RectTransform _root;
        private RectTransform _shadowRT;
        private Image         _bodyImg;
        private Image         _headerImg;
        private RectTransform _headerRT;
        private RectTransform _bodyRT;
        private TMP_Text      _headerText;
        private TMP_Text      _bodyText;

        public RectTransform Root => _root;

        /// <summary>
        /// Assemble the chip under <paramref name="parent"/>.
        /// The three plates take DIFFERENT 9-sliced sprites so the navy header and the white body meet
        /// on a square seam (only the card's outer corners are rounded);
        /// <paramref name="font"/> is lifted off a live HUD text so the chip inherits Rubik rather
        /// than falling back to LiberationSans.
        /// </summary>
        public static MapTargetReadoutWidget Build(Transform parent, Sprite shadowSprite,
                                                   Sprite headerSprite, Sprite bodySprite,
                                                   TMP_FontAsset font, float uiScale)
        {
            var go = new GameObject("MapTargetReadout");
            go.transform.SetParent(parent, false);
            var w  = go.AddComponent<MapTargetReadoutWidget>();
            w._root = go.GetComponent<RectTransform>();
            if (w._root == null) w._root = go.AddComponent<RectTransform>();
            w._root.anchorMin = w._root.anchorMax = Vector2.zero;
            w._root.pivot     = new Vector2(0f, 0.5f);

            // pixelsPerUnitMultiplier keeps the 8 px corner an 8 px corner after the chip is scaled to
            // the live surface: the generated sprite is baked at 1 px-per-unit, so the multiplier is the
            // inverse of the scale (PIPELINE_HARDENING §12 C3 / Rule 21's 9-slice-collapse check).
            float ppuMul = uiScale > 0.001f ? 1f / uiScale : 1f;

            // Three different plates on purpose: the shadow is the whole card so it keeps all four
            // corners, but the header and the body only round their OUTER edge — their shared seam is
            // square on both sides so the navy and the white actually touch.
            w._shadowRT  = MakePlate(go.transform, "Shadow", shadowSprite, kShade,  ppuMul).rectTransform;
            w._bodyImg   = MakePlate(go.transform, "Body",   bodySprite,   Color.white, ppuMul);
            w._bodyRT    = w._bodyImg.rectTransform;
            w._headerImg = MakePlate(go.transform, "Header", headerSprite, kNavy,  ppuMul);
            w._headerRT  = w._headerImg.rectTransform;

            w._headerText = MakeText(w._headerRT, "HeaderText", font, kHeaderFontPx * uiScale,
                                     Color.white, FontStyles.Bold);
            w._bodyText   = MakeText(w._bodyRT,   "BodyText",   font, kBodyFontPx   * uiScale,
                                     kNavy, FontStyles.Normal);
            return w;
        }

        private static Image MakePlate(Transform parent, string name, Sprite sprite, Color color, float ppuMul)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.sprite        = sprite;
            img.color         = color;
            img.type          = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = ppuMul;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            return img;
        }

        private static TMP_Text MakeText(RectTransform parent, string name, TMP_FontAsset font,
                                         float fontSize, Color color, FontStyles style)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t   = go.AddComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize      = fontSize;
            t.color         = color;
            t.fontStyle     = style;
            t.alignment     = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode  = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return t;
        }

        /// <summary>
        /// Push one frame of state. Returns the chip's laid-out size in canvas units, so the owner can
        /// decide whether the chip still fits on the right of L or has to flip to the left.
        /// </summary>
        public Vector2 Set(float carryM, float toPinM, bool over, string clubName, float maxReachM,
                           HoleIndicatorWidget.DistanceUnit unit, float uiScale)
        {
            string carry = HoleIndicatorWidget.FormatDistance(carryM, unit);
            if (_headerText != null)
            {
                _headerText.text = over
                    ? carry + "  ·  " + LocalizationManager.Get("MAPVIEW_OUT_OF_RANGE")
                    : carry;
            }
            if (_bodyText != null)
            {
                _bodyText.text = over
                    ? string.Format(LocalizationManager.Get("MAPVIEW_MAX_HINT"),
                                    clubName, HoleIndicatorWidget.FormatDistance(maxReachM, unit))
                    : string.Format(LocalizationManager.Get("MAPVIEW_TO_PIN"),
                                    HoleIndicatorWidget.FormatDistance(toPinM, unit));
            }
            if (_headerImg != null) _headerImg.color = over ? kRed : kNavy;

            // Width is whatever the longer of the two lines needs — the over-range body is a whole
            // sentence, so a fixed width would either truncate it or leave the normal chip cavernous.
            float padX     = kPadXPx * uiScale;
            float headerW  = _headerText != null ? _headerText.GetPreferredValues().x : 0f;
            float bodyW    = _bodyText   != null ? _bodyText.GetPreferredValues().x   : 0f;
            float w        = Mathf.Max(headerW, bodyW) + padX * 2f;
            float headerH  = kHeaderHPx * uiScale;
            float bodyH    = kBodyHPx   * uiScale;
            float h        = headerH + bodyH;

            if (_root != null) _root.sizeDelta = new Vector2(w, h);
            SetPlate(_headerRT, 0f,        w, headerH);
            SetPlate(_bodyRT,  -headerH,   w, bodyH);
            SetPlate(_shadowRT, kShadowDyPx * uiScale, w, h);
            return new Vector2(w, h);
        }

        private static void SetPlate(RectTransform rt, float y, float w, float h)
        {
            if (rt == null) return;
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta        = new Vector2(w, h);
        }

        public void SetVisible(bool on)
        {
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }
    }
}
