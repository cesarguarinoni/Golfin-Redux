#nullable enable
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Golfin.Inventory
{
    /// <summary>
    /// Bidirectional segmented stat bar for ball stats (-10 to +10).
    /// 20 segments total — center = 0.
    ///   Positive values fill right of center in blue.
    ///   Negative values fill left of center in orange-red.
    ///
    /// Attach to the same GameObject as the bar Image (get-or-add at runtime).
    /// Hides the smooth-fill Image and replaces it with block segments.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class BallSegmentedBar : MonoBehaviour
    {
        private static readonly Color PositiveColor = new(0.2f,  0.5f,  0.9f,  1f);
        private static readonly Color NegativeColor = new(0.9f,  0.3f,  0.15f, 1f);
        private static readonly Color EmptyColor    = new(0.25f, 0.25f, 0.3f,  0.5f);

        private const float SegmentGap    = 2f;
        private const float CenterGap     = 4f;   // wider gap at the midpoint (0 marker)

        private readonly List<Image> _segments = new();
        private bool _initialized;
        private int  _halfCount;   // segments per side (= maxValue)

        // ── Build ─────────────────────────────────────────────────────────────

        private void Build(int maxValue)
        {
            if (_initialized) return;
            _initialized = true;
            _halfCount   = maxValue;

            // Hide the original smooth-fill Image
            GetComponent<Image>().enabled = false;

            // HorizontalLayoutGroup lays out all children
            var hlg = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = SegmentGap;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.padding               = new RectOffset(0, 0, 0, 0);

            _segments.Clear();

            // Left half  (indices 0 … maxValue-1)
            for (int i = 0; i < maxValue; i++)
                _segments.Add(AddSegment($"L{maxValue - 1 - i}"));

            // Centre divider — fixed 4 px wide, not a stat segment
            var div = new GameObject("Div", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            div.transform.SetParent(transform, false);
            div.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
            div.GetComponent<Image>().raycastTarget = false;
            var le = div.GetComponent<LayoutElement>();
            le.preferredWidth     = CenterGap;
            le.minWidth           = CenterGap;
            le.flexibleWidth      = 0f;

            // Right half (indices maxValue … 2*maxValue-1)
            for (int i = 0; i < maxValue; i++)
                _segments.Add(AddSegment($"R{i}"));
        }

        private Image AddSegment(string goName)
        {
            var go  = new GameObject(goName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.color         = EmptyColor;
            img.raycastTarget = false;
            return img;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <param name="value">Stat value in range [-maxValue … +maxValue].</param>
        /// <param name="maxValue">Half-width of the bar (10 → 20 segments total).</param>
        public void SetValue(int value, int maxValue)
        {
            if (maxValue <= 0) maxValue = 10;
            Build(maxValue);

            value = Mathf.Clamp(value, -maxValue, maxValue);

            for (int i = 0; i < _segments.Count; i++)
            {
                // Left half: segments 0 … halfCount-1
                // Segment 0 = furthest left, segment halfCount-1 = closest to centre
                // Fill from centre outward for negative values:
                //   fill if i >= (halfCount - |value|)
                if (i < _halfCount)
                {
                    int distFromCentre = _halfCount - 1 - i;   // 0 = closest, halfCount-1 = furthest
                    _segments[i].color = (value < 0 && distFromCentre < Mathf.Abs(value))
                        ? NegativeColor : EmptyColor;
                }
                // Right half: segments halfCount … 2*halfCount-1
                // Segment halfCount = closest to centre, segment 2*halfCount-1 = furthest right
                // Fill from centre outward for positive values:
                //   fill if (i - halfCount) < value
                else
                {
                    int distFromCentre = i - _halfCount;        // 0 = closest, halfCount-1 = furthest
                    _segments[i].color = (value > 0 && distFromCentre < value)
                        ? PositiveColor : EmptyColor;
                }
            }
        }
    }
}
