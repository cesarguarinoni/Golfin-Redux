using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Subdivided cone mesh with:
    //   - gradient: semi-transparent black at center spine → semi-transparent gray at edges
    //   - curved base and band lines: center dips DOWN; all arcs share the same circle radius
    //   - shared-vertex grid: no seam artefacts between strips
    //   - silhouette edge feather: outer _edgeFadePx canvas units fade to alpha=0
    //   - band lines feathered top/bottom by BandFeatherPx for smooth anti-aliasing
    // Pivot must be (0.5, 0) — apex points up at y=_heightPx, base at y≈0 in local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConeMeshGraphic : MaskableGraphic
    {
        [SerializeField] private float _halfAngleDeg = 12.5f;
        [SerializeField] private float _heightPx     = 1009f;
        [SerializeField] private int   _strips       = 512;
        [SerializeField] private float _curvaturePx  = 15f;

        [Header("Fill")]
        [SerializeField] private Color _centerColor = new Color(0f,             0f,             0f,             0.50f);
        [SerializeField] private Color _fillColor   = new Color(200f / 255f, 200f / 255f, 200f / 255f, 90f / 255f);

        [Tooltip("Fraction of half-width that stays at the dark center color (0 = point, 0.5 = 50% of cone is dark)")]
        [SerializeField] [Range(0f, 0.99f)] private float _centerDarkFraction = 0f;

        [Tooltip("Canvas pixels at each silhouette edge that fade to transparent (anti-aliases the cone border)")]
        [SerializeField] private float _edgeFadePx = 8f;

        [Header("Bands")]
        [SerializeField] private float _bandRedY01     = ConeBandPalette.BandRedY01;
        [SerializeField] private float _bandGoldY01    = ConeBandPalette.BandGoldY01;
        [SerializeField] private float _bandGreenY01   = ConeBandPalette.BandGreenY01;
        [SerializeField] private Color _bandRedColor   = new Color(0x8B / 255f, 0x2A / 255f, 0x2A / 255f);
        [SerializeField] private Color _bandGoldColor  = new Color(0xA7 / 255f, 0x7C / 255f, 0x2A / 255f);
        [SerializeField] private Color _bandGreenColor = new Color(0x58 / 255f, 0x69 / 255f, 0x44 / 255f);

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

            float hb = HalfBasePx;
            int   N  = Mathf.Max(8, _strips);

            // ── Cone fill ─────────────────────────────────────────────────────
            // Shared-vertex column grid: (N+1) columns × 2 rows (bottom / top).
            // Gradient: black at center spine → gray at edges, feathered at silhouette.
            float edgeFadeN = hb > 0f ? _edgeFadePx / hb : 0f;  // fade zone in normalized coords

            int fillBase = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float x    = Mathf.Lerp(-hb, hb, (float)i / N);
                float absN = hb > 0f ? Mathf.Abs(x / hb) : 0f;  // 0 at center, 1 at edge
                float yTop = _heightPx * (1f - absN);             // cone silhouette
                float yBot = -_curvaturePx * (1f - absN * absN);  // concave base arc

                // Gradient: flat dark zone then ramp to fill color
                float t         = Mathf.Clamp01((absN - _centerDarkFraction) / Mathf.Max(0.001f, 1f - _centerDarkFraction));
                Color baseColor = Color.Lerp(_centerColor, _fillColor, t);

                // Silhouette edge feather: fade to alpha=0 in outer edgeFadePx canvas units
                float edgeAlpha = Mathf.Clamp01(Mathf.InverseLerp(1f, 1f - edgeFadeN, absN));
                baseColor.a    *= edgeAlpha;

                var c = (Color32)baseColor;
                AddVert(vh, new Vector2(x, yBot), c);   // index fillBase + 2*i
                AddVert(vh, new Vector2(x, yTop), c);   // index fillBase + 2*i + 1
            }
            for (int i = 0; i < N; i++)
            {
                int bl = fillBase + 2 * i,       tl = fillBase + 2 * i + 1;
                int br = fillBase + 2 * (i + 1), tr = fillBase + 2 * (i + 1) + 1;
                vh.AddTriangle(bl, br, tr);
                vh.AddTriangle(bl, tr, tl);
            }

            // ── Band lines ────────────────────────────────────────────────────
            AddBandLine(vh, _bandRedY01,   (Color32)_bandRedColor);
            AddBandLine(vh, _bandGoldY01,  (Color32)_bandGoldColor);
            AddBandLine(vh, _bandGreenY01, (Color32)_bandGreenColor);
        }

        // Each column has 4 vertices (outer-bottom → inner-bottom → inner-top → outer-top).
        // Feather zones (BandFeatherPx) above/below the solid band fade alpha to 0,
        // anti-aliasing both the top and bottom edges of the line.
        private void AddBandLine(VertexHelper vh, float y01, Color32 c)
        {
            float yCenter  = y01 * _heightPx;
            float hw       = HalfBasePx * Mathf.Max(0f, 1f - y01);
            float halfH    = ConeBandPalette.BandHalfHeightPx;
            float feather  = ConeBandPalette.BandFeatherPx;
            int   N        = Mathf.Max(8, _strips);       // full strip count for smooth arc edges
            float hb       = HalfBasePx;
            Color32 c0     = new Color32(c.r, c.g, c.b, 0);  // transparent border color

            int bandBase = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float x      = Mathf.Lerp(-hw, hw, (float)i / N);
                float n      = hw > 0f ? x / hw : 0f;
                float wRatio = hb > 0f ? hw / hb : 0f;
                // Scale to same circle radius as base: sagitta ∝ (half-width)²
                float curve  = -_curvaturePx * wRatio * wRatio * (1f - n * n);

                // 4 verts per column: outer-bottom, inner-bottom, inner-top, outer-top
                AddVert(vh, new Vector2(x, yCenter - halfH - feather + curve), c0); // 4*i + 0
                AddVert(vh, new Vector2(x, yCenter - halfH           + curve), c);  // 4*i + 1
                AddVert(vh, new Vector2(x, yCenter + halfH           + curve), c);  // 4*i + 2
                AddVert(vh, new Vector2(x, yCenter + halfH + feather + curve), c0); // 4*i + 3
            }

            // 3 quad strips per column pair: bottom-feather, solid, top-feather
            for (int i = 0; i < N; i++)
            {
                int b0 = bandBase + 4 * i;
                int b1 = bandBase + 4 * (i + 1);

                // bottom feather quad
                vh.AddTriangle(b0 + 0, b1 + 0, b1 + 1);
                vh.AddTriangle(b0 + 0, b1 + 1, b0 + 1);

                // solid quad
                vh.AddTriangle(b0 + 1, b1 + 1, b1 + 2);
                vh.AddTriangle(b0 + 1, b1 + 2, b0 + 2);

                // top feather quad
                vh.AddTriangle(b0 + 2, b1 + 2, b1 + 3);
                vh.AddTriangle(b0 + 2, b1 + 3, b0 + 3);
            }
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
