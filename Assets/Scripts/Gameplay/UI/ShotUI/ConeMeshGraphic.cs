using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Subdivided cone mesh with:
    //   - gradient: transparent at center axis → opaque at sides (darker center, lighter borders)
    //   - curved base and band lines: center dips DOWN (concave base, matching reference arc)
    //   - shared-vertex grid: no seam artefacts between strips
    // Pivot must be (0.5, 0) — apex points up, base at y=0 in local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConeMeshGraphic : MaskableGraphic
    {
        [SerializeField] private float _halfAngleDeg = 12.5f;
        [SerializeField] private float _heightPx     = 600f;
        [SerializeField] private int   _strips       = 128;
        [SerializeField] private float _curvaturePx  = 15f;

        [Header("Fill")]
        [SerializeField] private Color _fillColor = new Color(200f / 255f, 200f / 255f, 200f / 255f, 90f / 255f);

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
            // Gradient: transparent at center (x=0), opaque at edges (x=±hb).
            // Base arc: center dips down by _curvaturePx (concave base).
            int fillBase = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float x    = Mathf.Lerp(-hb, hb, (float)i / N);
                float n    = x / hb;                                         // −1 … +1
                float yTop = _heightPx * (1f - Mathf.Abs(n));               // cone silhouette
                float yBot = -_curvaturePx * (1f - n * n);                  // concave base arc
                float a    = Mathf.Lerp(0.05f, 1f, Mathf.Abs(n));           // edge brighter
                var   c    = WithAlpha(_fillColor, _fillColor.a * a);
                AddVert(vh, new Vector2(x, yBot), c);   // index fillBase + 2*i
                AddVert(vh, new Vector2(x, yTop), c);   // index fillBase + 2*i + 1
            }
            for (int i = 0; i < N; i++)
            {
                int bl = fillBase + 2 * i,      tl = fillBase + 2 * i + 1;
                int br = fillBase + 2 * (i + 1), tr = fillBase + 2 * (i + 1) + 1;
                vh.AddTriangle(bl, br, tr);
                vh.AddTriangle(bl, tr, tl);
            }

            // ── Band lines ────────────────────────────────────────────────────
            AddBandLine(vh, _bandRedY01,   (Color32)_bandRedColor);
            AddBandLine(vh, _bandGoldY01,  (Color32)_bandGoldColor);
            AddBandLine(vh, _bandGreenY01, (Color32)_bandGreenColor);
        }

        private void AddBandLine(VertexHelper vh, float y01, Color32 c)
        {
            float yCenter = y01 * _heightPx;
            float hw      = HalfBasePx * Mathf.Max(0f, 1f - y01);
            float halfH   = ConeBandPalette.BandHalfHeightPx;
            int   N       = Mathf.Max(8, _strips / 2);

            int bandBase = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float x     = Mathf.Lerp(-hw, hw, (float)i / N);
                float n     = hw > 0f ? x / hw : 0f;
                float curve = -_curvaturePx * (1f - n * n);   // same concave arc as base
                AddVert(vh, new Vector2(x, yCenter - halfH + curve), c);  // 2*i
                AddVert(vh, new Vector2(x, yCenter + halfH + curve), c);  // 2*i + 1
            }
            for (int i = 0; i < N; i++)
            {
                int bl = bandBase + 2 * i,       tl = bandBase + 2 * i + 1;
                int br = bandBase + 2 * (i + 1), tr = bandBase + 2 * (i + 1) + 1;
                vh.AddTriangle(bl, br, tr);
                vh.AddTriangle(bl, tr, tl);
            }
        }

        private static Color32 WithAlpha(Color c, float a) =>
            new Color(c.r, c.g, c.b, Mathf.Clamp01(a));

        private static void AddVert(VertexHelper vh, Vector2 pos, Color32 c)
        {
            var v      = UIVertex.simpleVert;
            v.position = pos;
            v.color    = c;
            vh.AddVert(v);
        }
    }
}
