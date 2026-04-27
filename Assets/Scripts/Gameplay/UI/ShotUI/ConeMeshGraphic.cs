using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Filled isoceles-triangle cone with a semi-transparent grey fill and three
    // horizontal colored band lines (red at base, gold mid, green near apex).
    // Band Y positions and colors are read from ConeBandPalette but can be
    // overridden per-instance via the Inspector for future timing redesigns.
    // Pivot must be (0.5, 0) — apex points up, base at y=0 in local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConeMeshGraphic : MaskableGraphic
    {
        [SerializeField] private float _halfAngleDeg = 12.5f;
        [SerializeField] private float _heightPx     = 600f;

        [Header("Fill")]
        [SerializeField] private Color _fillColor = new Color(200f / 255f, 200f / 255f, 200f / 255f, 90f / 255f);

        [Header("Bands")]
        [SerializeField] private float _bandRedY01       = ConeBandPalette.BandRedY01;
        [SerializeField] private float _bandGoldY01      = ConeBandPalette.BandGoldY01;
        [SerializeField] private float _bandGreenY01     = ConeBandPalette.BandGreenY01;
        [SerializeField] private Color _bandRedColor     = new Color(0x8B / 255f, 0x2A / 255f, 0x2A / 255f);
        [SerializeField] private Color _bandGoldColor    = new Color(0xA7 / 255f, 0x7C / 255f, 0x2A / 255f);
        [SerializeField] private Color _bandGreenColor   = new Color(0x58 / 255f, 0x69 / 255f, 0x44 / 255f);
        // Band half-height always reads from ConeBandPalette — not a per-instance override.

        public float HalfAngleDeg
        {
            get => _halfAngleDeg;
            set { _halfAngleDeg = value; SetVerticesDirty(); }
        }

        public float HeightPx
        {
            get => _heightPx;
            set { _heightPx = value; SetVerticesDirty(); }
        }

        public float HalfBasePx => _heightPx * Mathf.Tan(_halfAngleDeg * Mathf.Deg2Rad);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float   hb   = HalfBasePx;
            Color32 fill = _fillColor;

            // Filled cone triangle
            AddVert(vh, new Vector2(0f,  _heightPx), fill);
            AddVert(vh, new Vector2(-hb, 0f),        fill);
            AddVert(vh, new Vector2( hb, 0f),        fill);
            vh.AddTriangle(0, 1, 2);

            // Three horizontal band lines
            AddBandLine(vh, _bandRedY01,   (Color32)_bandRedColor);
            AddBandLine(vh, _bandGoldY01,  (Color32)_bandGoldColor);
            AddBandLine(vh, _bandGreenY01, (Color32)_bandGreenColor);
        }

        private void AddBandLine(VertexHelper vh, float y01, Color32 c)
        {
            float yCenter = y01 * _heightPx;
            float hw      = HalfBasePx * Mathf.Max(0f, 1f - y01);
            float top     = yCenter + ConeBandPalette.BandHalfHeightPx;
            float bottom  = yCenter - ConeBandPalette.BandHalfHeightPx;

            int idx = vh.currentVertCount;
            AddVert(vh, new Vector2(-hw, bottom), c);
            AddVert(vh, new Vector2( hw, bottom), c);
            AddVert(vh, new Vector2( hw, top),    c);
            AddVert(vh, new Vector2(-hw, top),    c);
            vh.AddTriangle(idx,     idx + 1, idx + 2);
            vh.AddTriangle(idx,     idx + 2, idx + 3);
        }

        private static void AddVert(VertexHelper vh, Vector2 pos, Color32 c)
        {
            var v      = UIVertex.simpleVert;
            v.position = pos;
            v.color    = c;
            vh.AddVert(v);
        }
    }
}
