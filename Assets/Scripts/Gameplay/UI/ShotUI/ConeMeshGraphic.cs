using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Subdivided cone mesh with:
    //   - gradient: semi-transparent black at center spine → semi-transparent gray at edges
    //   - curved base and band lines: center dips DOWN; all arcs share the same circle radius
    //   - shared-vertex grid: no seam artefacts between strips
    //   - silhouette feather strips: separate transparent outer band along both edges,
    //     offset in the perpendicular-to-silhouette direction for true diagonal anti-aliasing
    //   - band lines feathered top/bottom by BandFeatherPx
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

        [Tooltip("Width in canvas px of the perpendicular feather strip along each silhouette edge")]
        [SerializeField] private float _edgeFadePx = 4f;

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

        public float CurvaturePx => _curvaturePx;

        public float HalfBasePx => _heightPx * Mathf.Tan(_halfAngleDeg * Mathf.Deg2Rad);

        /// <summary>
        /// F15 (shot_timing_power, D3): re-sync the band lines from <see cref="ConeBandPalette"/>
        /// — i.e. from ControlsConfig — before the first mesh build. The three fields below are
        /// serialized, so a prefab authored before the edges moved into config would keep drawing
        /// its baked-in values while the gameplay multiplier used the config's. Reading them back
        /// here means the drawn band and the power penalty can never disagree.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            bool changed = !Mathf.Approximately(_bandRedY01,   ConeBandPalette.BandRedY01)
                        || !Mathf.Approximately(_bandGoldY01,  ConeBandPalette.BandGoldY01)
                        || !Mathf.Approximately(_bandGreenY01, ConeBandPalette.BandGreenY01);
            if (!changed) return;

            _bandRedY01   = ConeBandPalette.BandRedY01;
            _bandGoldY01  = ConeBandPalette.BandGoldY01;
            _bandGreenY01 = ConeBandPalette.BandGreenY01;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float hb = HalfBasePx;
            int   N  = Mathf.Max(8, _strips);

            // ── Cone fill ─────────────────────────────────────────────────────
            // Shared-vertex column grid: (N+1) columns × 2 rows (bot/top).
            // No edge alpha here — silhouette anti-aliasing is handled by the
            // separate feather strips below, which fade perpendicularly to the edge.
            int fillBase = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float x    = Mathf.Lerp(-hb, hb, (float)i / N);
                float absN = hb > 0f ? Mathf.Abs(x / hb) : 0f;
                float yTop = _heightPx * (1f - absN);
                float yBot = -_curvaturePx * (1f - absN * absN);
                float t    = Mathf.Clamp01((absN - _centerDarkFraction) / Mathf.Max(0.001f, 1f - _centerDarkFraction));
                var   c    = (Color32)Color.Lerp(_centerColor, _fillColor, t);
                AddVert(vh, new Vector2(x, yBot), c);
                AddVert(vh, new Vector2(x, yTop), c);
            }
            for (int i = 0; i < N; i++)
            {
                int bl = fillBase + 2 * i,       tl = fillBase + 2 * i + 1;
                int br = fillBase + 2 * (i + 1), tr = fillBase + 2 * (i + 1) + 1;
                vh.AddTriangle(bl, br, tr);
                vh.AddTriangle(bl, tr, tl);
            }

            // ── Silhouette feather strips ─────────────────────────────────────
            // Two thin strips (left and right) whose inner vertices sit exactly
            // on the cone silhouette (opaque) and outer vertices are offset by
            // _edgeFadePx in the PERPENDICULAR direction to the silhouette
            // (transparent). This produces true diagonal anti-aliasing.
            AddSilhouetteFeather(vh, _edgeFadePx);

            // ── Band lines ────────────────────────────────────────────────────
            AddBandLine(vh, _bandRedY01,   (Color32)_bandRedColor);
            AddBandLine(vh, _bandGoldY01,  (Color32)_bandGoldColor);
            AddBandLine(vh, _bandGreenY01, (Color32)_bandGreenColor);
        }

        // Feather strips along left and right silhouette edges.
        // For right edge: inner vertex at (x, yTop), outer at (x+ox, yTop+oy)
        // where (ox,oy) is the outward-perpendicular direction scaled by featherPx.
        private void AddSilhouetteFeather(VertexHelper vh, float featherPx)
        {
            if (featherPx <= 0f) return;

            float hb = HalfBasePx;
            int   N  = Mathf.Max(8, _strips);

            // Outward normal of right silhouette (pointing right+up away from cone).
            // Silhouette direction: from (hb,0) to (0,heightPx) → (-hb, heightPx).
            // Rotate 90° clockwise for outward normal: (heightPx, hb). Normalize.
            float len = Mathf.Sqrt(hb * hb + _heightPx * _heightPx);
            float ox  = featherPx * (_heightPx / len);
            float oy  = featherPx * (hb        / len);

            Color32 cTransp = new Color32(0, 0, 0, 0);

            // Right silhouette: t=0 at base (hb,0), t=1 at apex (0,heightPx)
            int rb = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float t    = (float)i / N;
                float x    = hb * (1f - t);
                float y    = _heightPx * t;
                float absN = 1f - t;
                float tc   = Mathf.Clamp01((absN - _centerDarkFraction) / Mathf.Max(0.001f, 1f - _centerDarkFraction));
                Color32 c  = (Color32)Color.Lerp(_centerColor, _fillColor, tc);
                AddVert(vh, new Vector2(x,      y     ), c);       // inner — on silhouette, opaque
                AddVert(vh, new Vector2(x + ox, y + oy), cTransp); // outer — beyond silhouette, transparent
            }
            for (int i = 0; i < N; i++)
            {
                int a = rb + 2 * i, b = rb + 2 * (i + 1);
                vh.AddTriangle(a,     b,     b + 1);
                vh.AddTriangle(a,     b + 1, a + 1);
            }

            // Left silhouette: mirror — outer offset goes in the -x direction
            int lb = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float t    = (float)i / N;
                float x    = -hb * (1f - t);
                float y    = _heightPx * t;
                float absN = 1f - t;
                float tc   = Mathf.Clamp01((absN - _centerDarkFraction) / Mathf.Max(0.001f, 1f - _centerDarkFraction));
                Color32 c  = (Color32)Color.Lerp(_centerColor, _fillColor, tc);
                AddVert(vh, new Vector2(x,      y     ), c);       // inner
                AddVert(vh, new Vector2(x - ox, y + oy), cTransp); // outer
            }
            for (int i = 0; i < N; i++)
            {
                int a = lb + 2 * i, b = lb + 2 * (i + 1);
                vh.AddTriangle(a,     b + 1, b    );  // reversed winding for left side
                vh.AddTriangle(a,     a + 1, b + 1);
            }
        }

        // Fully opaque band line: 2 vertices per column (bottom, top), no feather.
        private void AddBandLine(VertexHelper vh, float y01, Color32 c)
        {
            float yCenter = y01 * _heightPx;
            float hw      = HalfBasePx * Mathf.Max(0f, 1f - y01);
            float halfH   = ConeBandPalette.BandHalfHeightPx;
            int   N       = Mathf.Max(8, _strips / 2);
            float hb      = HalfBasePx;

            int bandBase = vh.currentVertCount;
            for (int i = 0; i <= N; i++)
            {
                float x      = Mathf.Lerp(-hw, hw, (float)i / N);
                float n      = hw > 0f ? x / hw : 0f;
                float wRatio = hb > 0f ? hw / hb : 0f;
                float curve  = -_curvaturePx * wRatio * wRatio * (1f - n * n);
                AddVert(vh, new Vector2(x, yCenter - halfH + curve), c);
                AddVert(vh, new Vector2(x, yCenter + halfH + curve), c);
            }
            for (int i = 0; i < N; i++)
            {
                int b0 = bandBase + 2 * i;
                int b1 = bandBase + 2 * (i + 1);
                vh.AddTriangle(b0,     b1,     b1 + 1);
                vh.AddTriangle(b0,     b1 + 1, b0 + 1);
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
