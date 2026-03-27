#nullable enable
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Golfin.Inventory
{
    /// <summary>
    /// Segmented stat bar for ball stats (-10 to +10).
    /// Attach to the same GameObject as an existing bar Image.
    /// On first SetValue call, hides the smooth-fill Image and builds
    /// N block segments as children, laid out by HorizontalLayoutGroup.
    /// Blue for positive values, orange-red for negative.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class BallSegmentedBar : MonoBehaviour
    {
        private static readonly Color PositiveColor = new(0.2f,  0.5f,  0.9f,  1f);
        private static readonly Color NegativeColor = new(0.9f,  0.3f,  0.15f, 1f);
        private static readonly Color EmptyColor    = new(0.25f, 0.25f, 0.3f,  0.5f);

        private const float SegmentGap = 2f;

        private readonly List<Image> _segments = new();
        private bool _initialized;

        // ── Initialization ────────────────────────────────────────────────────

        private void Build(int count)
        {
            if (_initialized) return;
            _initialized = true;

            // Hide the original smooth-fill Image
            GetComponent<Image>().enabled = false;

            // HorizontalLayoutGroup on this GO sizes the children
            var hlg = gameObject.GetComponent<HorizontalLayoutGroup>();
            if (hlg == null) hlg = gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing              = SegmentGap;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment       = TextAnchor.MiddleLeft;
            hlg.padding              = new RectOffset(0, 0, 0, 0);

            _segments.Clear();
            for (int i = 0; i < count; i++)
            {
                var go  = new GameObject($"Seg{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);

                var img = go.GetComponent<Image>();
                img.color         = EmptyColor;
                img.raycastTarget = false;
                _segments.Add(img);
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <param name="value">Stat value (-maxValue to +maxValue).</param>
        /// <param name="maxValue">Maximum absolute value (determines segment count).</param>
        public void SetValue(int value, int maxValue)
        {
            if (maxValue <= 0) maxValue = 10;
            Build(maxValue);

            int    filled    = Mathf.Abs(value);
            Color  fillColor = value >= 0 ? PositiveColor : NegativeColor;

            for (int i = 0; i < _segments.Count; i++)
                _segments[i].color = i < filled ? fillColor : EmptyColor;
        }
    }
}
