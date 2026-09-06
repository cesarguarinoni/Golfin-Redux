using TMPro;
using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// map_view_v2 §7 — the pin chip drawn on the map, which is the REAL in-game
    /// <see cref="HoleIndicatorWidget"/> chip rather than the code-built 48 px yellow flag the map used
    /// to draw. Cesar's complaint was precisely that the two did not match, so this does not re-model
    /// the chip: it <c>Instantiate</c>s the live HUD GameObject and drives the clone by hand.
    ///
    /// Clone provenance (PIPELINE_HARDENING Rule 19): the source is the scene's
    /// <c>ShotUI_Canvas/HoleIndicator</c> — the same object <c>PhysicsLabController</c> feeds — found at
    /// runtime by component type, not by name and not by a hand-rebuilt hierarchy. Cloning the live
    /// object rather than extracting a prefab first means ZERO scene edits and means the map picks up
    /// any future restyle of the HUD chip for free.
    ///
    /// Layout rule (the fidelity table): the tail's FADING end sits on the pin's screen point and the
    /// 100×100 chip hangs off the other end, on whichever side of the pin has room — BELOW the pin when
    /// the pin is in the top half of the screen (the normal map case, and what the Figma shows), ABOVE
    /// it otherwise. The tail is never shorter than <c>pinTailMinPx</c>, so the chip cannot cover the
    /// hole; it only grows past that when the chip would otherwise leave the safe area.
    /// </summary>
    public class MapPinIndicator
    {
        /// <summary>Chip edge length in the source's own units (the HUD chip is authored 100×100).</summary>
        private const float kChipPx = 100f;
        /// <summary>How far inside the chip edge the prefab anchors its tail (HoleIndicatorWidget's 10 px inset).</summary>
        private const float kTailInsetPx = 10f;

        private readonly RectTransform _root;
        private readonly RectTransform _chip;
        private readonly RectTransform _tail;
        private readonly TMP_Text      _distanceText;
        private readonly float         _uiScale;

        /// <summary>Screen-space rect the chip occupied on the last <see cref="Place"/>. Empty until then.</summary>
        public Rect ChipScreenRect { get; private set; }

        /// <summary>
        /// Chip AND tail together. This is what other UI has to keep out of: dodging only the chip
        /// still let the readout sit under the tail, which then drew across its header.
        /// </summary>
        public Rect IndicatorScreenRect { get; private set; }
        public bool IsValid => _root != null;

        private MapPinIndicator(RectTransform root, RectTransform chip, RectTransform tail,
                                TMP_Text distanceText, float uiScale)
        {
            _root = root; _chip = chip; _tail = tail; _distanceText = distanceText; _uiScale = uiScale;
        }

        /// <summary>
        /// Clone the live HUD chip under <paramref name="parent"/>. Returns null when there is no
        /// HoleIndicatorWidget in the scene (e.g. a bare test scene) — the caller then keeps the map's
        /// legacy flag icon rather than drawing nothing.
        /// </summary>
        public static MapPinIndicator CloneFromScene(Transform parent, float uiScale)
        {
            var src = Object.FindObjectOfType<HoleIndicatorWidget>(true);
            if (src == null)
            {
                Debug.LogWarning("[MapView v2] No HoleIndicatorWidget in the scene — map pin chip falls back to the legacy flag icon.");
                return null;
            }

            var clone = Object.Instantiate(src.gameObject, parent);
            clone.name = "MapView_PinChip";

            // The clone's own widget would fight us for the RectTransform every LateUpdate (and would
            // re-subscribe to HoleContext). `enabled = false` stops it THIS frame; Destroy only lands at
            // the end of it, which is one LateUpdate too late.
            var w = clone.GetComponent<HoleIndicatorWidget>();
            if (w != null) { w.enabled = false; Object.Destroy(w); }
            // Ditto the CanvasGroup fade the widget's Awake installs — it is driven by the gameplay
            // camera's forward vector, which is meaningless while the map camera is the one rendering.
            var cg = clone.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = false; cg.interactable = false; }

            var root = clone.GetComponent<RectTransform>();
            var chip = FindChild(root, "DataChip");
            var tail = FindChild(root, "ArrowLine");
            var txt  = clone.GetComponentInChildren<TMP_Text>(true);

            // Re-anchor the ROOT to the canvas's bottom-left so it can be positioned in raw screen px
            // (the HUD anchors it top-left and slides it along x). The children keep anchoring to the
            // root's own top-left corner, so the chip/tail layout inside the root is untouched.
            root.anchorMin = root.anchorMax = Vector2.zero;
            root.pivot     = new Vector2(0f, 1f);
            root.localScale = Vector3.one * uiScale;

            return new MapPinIndicator(root, chip, tail, txt, uiScale);
        }

        private static RectTransform FindChild(RectTransform root, string name)
        {
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                if (rt.name == name) return rt;
            return null;
        }

        public void SetActive(bool on)
        {
            if (_root != null && _root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
        }

        public void SetDistance(float meters, HoleIndicatorWidget.DistanceUnit unit)
        {
            if (_distanceText != null) _distanceText.text = HoleIndicatorWidget.FormatDistance(meters, unit);
        }

        /// <summary>
        /// Place the chip for one frame.
        /// <paramref name="pinScreen"/> is the pin's point in camera pixel space;
        /// <paramref name="tailMinPx"/> is the serialized <c>_pinTailMinPx</c> floor;
        /// <paramref name="screenW"/>/<paramref name="screenH"/> and <paramref name="insetPx"/> define
        /// the safe area the chip has to stay inside.
        /// </summary>
        public void Place(Vector2 pinScreen, float tailMinPx, float screenW, float screenH, float insetPx)
        {
            if (_root == null) return;

            float chipLocal = _chip != null && _chip.rect.width > 1f ? _chip.rect.width : kChipPx;
            float chipHalf  = chipLocal * 0.5f * _uiScale;
            float tailPx    = Mathf.Max(tailMinPx, 0f) * _uiScale;
            float reach     = chipHalf + tailPx;

            // dirToPin points from the CHIP toward the PIN, so Vector2.down means the chip sits ABOVE
            // the pin with its tail dropping straight onto it.
            //
            // That is the preferred pose, and it is a deliberate departure from the Figma (Cesar,
            // 2026-09-05: "the flag indicator should reaccommodate to 'stand' over the hole, the tail
            // pointing directly down to the hole, when moving close to the hole"). B1's mock hangs the
            // chip BELOW the pin, which reads as a label dangling off the flag; standing it on the hole
            // reads as a marker planted in it, and it matches the in-game HUD chip's own posture.
            // The chip only flips below when there is no room above — near the top edge of the frame.
            Vector2 dirToPin = Vector2.down;

            bool FitsWith(Vector2 d)
            {
                float cy = pinScreen.y - d.y * reach;
                return cy - chipHalf >= insetPx && cy + chipHalf <= screenH - insetPx;
            }
            if (!FitsWith(dirToPin) && FitsWith(-dirToPin)) dirToPin = -dirToPin;

            PlaceAlong(pinScreen, dirToPin, tailPx, screenW, screenH, insetPx);
        }

        /// <summary>
        /// map_view_v2 §7 (extended, Cesar 2026-09-04) — the general case: put the tail's fading TIP on
        /// <paramref name="tipScreen"/> and hang the chip back along −<paramref name="dirToPin"/>.
        ///
        /// This is what lets the OFF-SCREEN pin keep the real chip too. The spec carved that state out
        /// ("keeps its current sprites"), but the map camera frames ball + club carry, so on every par 4
        /// and par 5 the pin is outside the frame — meaning the carve-out would have shown the old
        /// yellow flag on 14 of 18 holes, which is the exact thing this task exists to replace.
        /// </summary>
        public void PlaceAlong(Vector2 tipScreen, Vector2 dirToPin, float tailPx,
                               float screenW, float screenH, float insetPx)
        {
            if (_root == null) return;
            if (dirToPin.sqrMagnitude < 1e-6f) dirToPin = Vector2.up;
            dirToPin.Normalize();

            float chipLocal = _chip != null && _chip.rect.width > 1f ? _chip.rect.width : kChipPx;
            float chipHalf  = chipLocal * 0.5f * _uiScale;

            Vector2 chipCentre = tipScreen - dirToPin * (chipHalf + tailPx);
            chipCentre.x = Mathf.Clamp(chipCentre.x, insetPx + chipHalf, screenW - insetPx - chipHalf);
            chipCentre.y = Mathf.Clamp(chipCentre.y, insetPx + chipHalf, screenH - insetPx - chipHalf);

            // Root pivot is its TOP-LEFT corner and the chip occupies the root's top-left square.
            _root.anchoredPosition = new Vector2(chipCentre.x - chipHalf, chipCentre.y + chipHalf);
            ChipScreenRect = new Rect(chipCentre.x - chipHalf, chipCentre.y - chipHalf,
                                      chipHalf * 2f, chipHalf * 2f);
            // Union of the chip and the tail's run out to the tip.
            IndicatorScreenRect = Rect.MinMaxRect(
                Mathf.Min(ChipScreenRect.xMin, tipScreen.x), Mathf.Min(ChipScreenRect.yMin, tipScreen.y),
                Mathf.Max(ChipScreenRect.xMax, tipScreen.x), Mathf.Max(ChipScreenRect.yMax, tipScreen.y));

            if (_tail == null) return;
            _tail.gameObject.SetActive(true);

            // The tail is authored pivot-top-centre and extends along its local −Y, so a Z rotation of
            // (angle + 90°) aims that −Y at the pin — the identity HoleIndicatorWidget uses.
            float angleDeg = Mathf.Atan2(dirToPin.y, dirToPin.x) * Mathf.Rad2Deg;
            _tail.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 90f);

            // Anchor the tail on the chip's perimeter, inset, in the direction of travel — a ray/rect
            // hit in the root's own coordinates, so it works for any angle rather than just up/down.
            float halfExt = chipLocal * 0.5f - kTailInsetPx;
            Vector2 dLocal = new Vector2(dirToPin.x, -dirToPin.y);   // root Y grows downward
            float t = float.MaxValue;
            if (Mathf.Abs(dLocal.x) > 0.001f) t = Mathf.Min(t, halfExt / Mathf.Abs(dLocal.x));
            if (Mathf.Abs(dLocal.y) > 0.001f) t = Mathf.Min(t, halfExt / Mathf.Abs(dLocal.y));
            if (t >= float.MaxValue - 1f) t = halfExt;
            Vector2 centreLocal = new Vector2(chipLocal * 0.5f, -chipLocal * 0.5f);
            _tail.anchoredPosition = centreLocal + dLocal * t;

            // Length: from that perimeter point out to the tip, in root-local units.
            float gapPx = Vector2.Distance(chipCentre, tipScreen) - halfExt * _uiScale;
            _tail.sizeDelta = new Vector2(_tail.sizeDelta.x, Mathf.Max(gapPx, 1f) / _uiScale);
        }

        /// <summary>
        /// map_view_v2 §7 — the invariant the fidelity table demands: the chip never covers the hole.
        /// Distance from the pin to the NEAREST point of the chip, in screen px.
        /// </summary>
        public float GapToPinPx(Vector2 pinScreen)
        {
            if (_root == null || ChipScreenRect.width <= 0f) return float.NaN;
            Vector2 nearest = new Vector2(
                Mathf.Clamp(pinScreen.x, ChipScreenRect.xMin, ChipScreenRect.xMax),
                Mathf.Clamp(pinScreen.y, ChipScreenRect.yMin, ChipScreenRect.yMax));
            return Vector2.Distance(nearest, pinScreen);
        }
    }
}
