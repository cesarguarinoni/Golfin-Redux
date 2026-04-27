using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class PowerGaugeGraphic : MaskableGraphic
    {
        [SerializeField] private float _innerRadius  = 80f;
        [SerializeField] private float _outerRadius  = 100f;
        [SerializeField] private int   _segmentCount = 64;

        // Gradient stops along the arc: green (0°) → yellow (180°) → red (360°) → maroon (overpower)
        [SerializeField] private Color _colorGreen  = new Color(0.20f, 0.90f, 0.10f, 1f);
        [SerializeField] private Color _colorYellow = new Color(1.00f, 0.85f, 0.00f, 1f);
        [SerializeField] private Color _colorRed    = new Color(0.90f, 0.15f, 0.10f, 1f);
        [SerializeField] private Color _colorMaroon = new Color(0.55f, 0.00f, 0.12f, 1f);

        private float _progress01;

        public float Progress01
        {
            get => _progress01;
            set
            {
                if (Mathf.Approximately(_progress01, value)) return;
                _progress01 = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float fillAngleDeg = _progress01 * 360f;
            Debug.Log($"[PowerGaugeGraphic] OnPopulateMesh — progress={_progress01:F3} fill={fillAngleDeg:F1}° innerR={_innerRadius} outerR={_outerRadius} segs={_segmentCount}");

            if (fillAngleDeg <= 0f) return;

            float stepDeg   = 360f / _segmentCount;
            int   totalSegs = Mathf.CeilToInt(fillAngleDeg / stepDeg);

            for (int i = 0; i < totalSegs; i++)
            {
                float a0 = i * stepDeg;
                float a1 = Mathf.Min((i + 1) * stepDeg, fillAngleDeg);
                AddQuad(vh, a0, a1, ArcColor(a0), ArcColor(a1));
            }
        }

        private void AddQuad(VertexHelper vh, float deg0, float deg1, Color c0, Color c1)
        {
            float rad0 = deg0 * Mathf.Deg2Rad;
            float rad1 = deg1 * Mathf.Deg2Rad;

            var ia = new Vector3(Mathf.Sin(rad0) * _innerRadius, Mathf.Cos(rad0) * _innerRadius);
            var oa = new Vector3(Mathf.Sin(rad0) * _outerRadius, Mathf.Cos(rad0) * _outerRadius);
            var ib = new Vector3(Mathf.Sin(rad1) * _innerRadius, Mathf.Cos(rad1) * _innerRadius);
            var ob = new Vector3(Mathf.Sin(rad1) * _outerRadius, Mathf.Cos(rad1) * _outerRadius);

            int b = vh.currentVertCount;
            var v = UIVertex.simpleVert;

            v.color = c0; v.position = ia; vh.AddVert(v); // b
            v.color = c0; v.position = oa; vh.AddVert(v); // b+1
            v.color = c1; v.position = ob; vh.AddVert(v); // b+2
            v.color = c1; v.position = ib; vh.AddVert(v); // b+3

            vh.AddTriangle(b, b + 1, b + 2);
            vh.AddTriangle(b, b + 2, b + 3);
        }

        // Arc color follows a gradient baked into the arc's geometry:
        // 12-o'clock (0°) = green, 6-o'clock (180°) = yellow, full circle (360°) = red.
        // Past 360° (overpower wrap) = maroon.
        private Color ArcColor(float angleDeg)
        {
            if (angleDeg >= 360f) return _colorMaroon;
            float t = angleDeg / 360f;
            return t <= 0.5f
                ? Color.Lerp(_colorGreen,  _colorYellow, t * 2f)
                : Color.Lerp(_colorYellow, _colorRed,    (t - 0.5f) * 2f);
        }
    }
}
