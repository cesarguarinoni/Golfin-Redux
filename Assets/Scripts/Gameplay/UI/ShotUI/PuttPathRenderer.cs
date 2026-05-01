using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Procedural MaskableGraphic that renders the predicted putt path as a polyline.
    /// Call SetPath() from PuttPathPredictor each time aim or power changes.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PuttPathRenderer : MaskableGraphic
    {
        private List<Vector2> _points;
        private List<float>   _speeds;

        // Public API -----------------------------------------------------------

        [SerializeField] private float _lineWidthPx = 16f;

        public bool  HeatmapMode  { get; set; } = false;
        public float TopSpeedMps  { get; set; } = 5f;
        public float LineWidthPx
        {
            get => _lineWidthPx;
            set { _lineWidthPx = value; SetVerticesDirty(); }
        }

        /// <summary>
        /// Set the path to render. Pass null / empty to hide.
        /// canvasPoints: positions in the PuttPathRoot's local rect-transform space.
        /// speedMps: one value per point, used in heatmap mode.
        /// </summary>
        public void SetPath(List<Vector2> canvasPoints, List<float> speedMps)
        {
            _points = (canvasPoints != null && canvasPoints.Count >= 2) ? canvasPoints : null;
            _speeds = speedMps;
            SetVerticesDirty();
        }

        // Internal -------------------------------------------------------------

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (_points == null || _points.Count < 2)
                return;

            // Measure total path length to reject degenerate input.
            float totalLen = 0f;
            for (int i = 0; i < _points.Count - 1; i++)
                totalLen += Vector2.Distance(_points[i], _points[i + 1]);

            if (totalLen < 1f)
                return;

            int n = _points.Count;
            float halfW = LineWidthPx * 0.5f;

            for (int i = 0; i < n - 1; i++)
            {
                Vector2 p0 = _points[i];
                Vector2 p1 = _points[i + 1];

                Vector2 dir  = (p1 - p0);
                float segLen = dir.magnitude;
                if (segLen < 0.001f) continue;
                dir /= segLen;

                Vector2 perp = new Vector2(-dir.y, dir.x) * halfW;

                float t0 = n > 1 ? (float)i / (n - 1) : 0f;
                float t1 = n > 1 ? (float)(i + 1) / (n - 1) : 0f;

                Color c0 = GetSegmentColor(i,     t0);
                Color c1 = GetSegmentColor(i + 1, t1);

                int baseIdx = vh.currentVertCount;

                var vA = UIVertex.simpleVert; vA.position = new Vector3(p0.x + perp.x, p0.y + perp.y, 0); vA.color = c0;
                var vB = UIVertex.simpleVert; vB.position = new Vector3(p0.x - perp.x, p0.y - perp.y, 0); vB.color = c0;
                var vC = UIVertex.simpleVert; vC.position = new Vector3(p1.x - perp.x, p1.y - perp.y, 0); vC.color = c1;
                var vD = UIVertex.simpleVert; vD.position = new Vector3(p1.x + perp.x, p1.y + perp.y, 0); vD.color = c1;

                vh.AddVert(vA); // 0
                vh.AddVert(vB); // 1
                vh.AddVert(vC); // 2
                vh.AddVert(vD); // 3

                vh.AddTriangle(baseIdx + 0, baseIdx + 1, baseIdx + 2);
                vh.AddTriangle(baseIdx + 0, baseIdx + 2, baseIdx + 3);
            }
        }

        private Color GetSegmentColor(int pointIndex, float t)
        {
            if (HeatmapMode)
            {
                float speed = (_speeds != null && pointIndex < _speeds.Count) ? _speeds[pointIndex] : 0f;
                float norm  = TopSpeedMps > 0f ? Mathf.Clamp01(speed / TopSpeedMps) : 0f;
                return HeatmapColor(norm);
            }
            else
            {
                // Blue #477EC1 with alpha fading from 1.0 at start to 0.2 at end.
                var baseBlue = new Color(0x47 / 255f, 0x7E / 255f, 0xC1 / 255f, 1f);
                float alpha  = Mathf.Lerp(1f, 0.2f, t);
                baseBlue.a   = alpha;
                return baseBlue;
            }
        }

        private static Color HeatmapColor(float t)
        {
            // green → yellow → red
            var green  = new Color(0f,   1f,   0f,   1f);
            var yellow = new Color(1f,   1f,   0f,   1f);
            var red    = new Color(1f,   0f,   0f,   1f);

            if (t <= 0.5f)
                return Color.Lerp(green, yellow, t * 2f);
            else
                return Color.Lerp(yellow, red, (t - 0.5f) * 2f);
        }
    }
}
