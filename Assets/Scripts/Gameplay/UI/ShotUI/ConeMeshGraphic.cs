using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Filled isoceles-triangle cone with semi-transparent gradient fill and three
    // curved horizontal band lines (red at base, gold mid, green near apex).
    // Fill is subdivided into _strips vertical quads so:
    //   - edges are smooth (no staircase artefact)
    //   - alpha fades from full at center-line to transparent at outer edges
    //   - base and band lines arc upward at the center (_curvaturePx)
    // Pivot must be (0.5, 0) — apex points up, base at y=0 in local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConeMeshGraphic : MaskableGraphic
    {
        [SerializeField] private float _halfAngleDeg  = 12.5f;
        [SerializeField] private float _heightPx      = 600f;
        [SerializeField] private int   _strips        = 64;
        [SerializeField] private float _curvaturePx   = 15f;

        [Header("Fill")]
        [SerializeField] private Color _fillColor = new Color(200f / 255f, 200f / 255f, 200f / 255f, 90f / 255f);

        [Header("Bands")]
        [SerializeField] private float _bandRedY01       = ConeBandPalette.BandRedY01;
        [SerializeField] private float _bandGoldY01      = ConeBandPalette.BandGoldY01;
        [SerializeField] private float _bandGreenY01     = ConeBandPalette.BandGreenY01;
        [SerializeField] private Color _bandRedColor     = new Color(0x8B / 255f, 0x2A / 255f, 0x2A / 255f);
        [SerializeField] private Color _bandGoldColor    = new Color(0xA7 / 255f, 0x7C / 255f, 0x2A / 255f);
        [SerializeField] private Color _bandGreenColor   = new Color(0x58 / 255f, 0x69 / 255f, 0x44 / 255f);

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

            // Cone fill: N vertical trapezoid strips.
            //   - gradient alpha: 0 at x=±hb, full at x=0
            //   - curved base: arcs up by _curvaturePx at center
            for (int i = 0; i < N; i++)
            {
                float xL = Mathf.Lerp(-hb, hb, (float)i       / N);
                float xR = Mathf.Lerp(-hb, hb, (float)(i + 1) / N);

                float nL = xL / hb;                          // −1 … +1
                float nR = xR / hb;

                float yTopL = _heightPx * (1f - Mathf.Abs(nL));  // cone silhouette at xL
                float yTopR = _heightPx * (1f - Mathf.Abs(nR));

                float yBotL = _curvaturePx * (1f - nL * nL);     // curved base
                float yBotR = _curvaturePx * (1f - nR * nR);

                if (yBotL >= yTopL && yBotR >= yTopR) continue;

                float aL = 1f - Mathf.Abs(nL);
                float aR = 1f - Mathf.Abs(nR);

                int idx = vh.currentVertCount;
                AddVert(vh, new Vector2(xL, yBotL), WithAlpha(_fillColor, _fillColor.a * aL));
                AddVert(vh, new Vector2(xR, yBotR), WithAlpha(_fillColor, _fillColor.a * aR));
                AddVert(vh, new Vector2(xR, yTopR), WithAlpha(_fillColor, _fillColor.a * aR));
                AddVert(vh, new Vector2(xL, yTopL), WithAlpha(_fillColor, _fillColor.a * aL));
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
            }

            // Three curved band lines (same arc as the base)
            AddBandLine(vh, _bandRedY01,   (Color32)_bandRedColor,   N / 2);
            AddBandLine(vh, _bandGoldY01,  (Color32)_bandGoldColor,  N / 2);
            AddBandLine(vh, _bandGreenY01, (Color32)_bandGreenColor, N / 2);
        }

        private void AddBandLine(VertexHelper vh, float y01, Color32 c, int N)
        {
            float yCenter = y01 * _heightPx;
            float hw      = HalfBasePx * Mathf.Max(0f, 1f - y01);
            float halfH   = ConeBandPalette.BandHalfHeightPx;

            for (int i = 0; i < N; i++)
            {
                float xL = Mathf.Lerp(-hw, hw, (float)i       / N);
                float xR = Mathf.Lerp(-hw, hw, (float)(i + 1) / N);

                float nL     = hw > 0f ? xL / hw : 0f;
                float nR     = hw > 0f ? xR / hw : 0f;
                float curveL = _curvaturePx * (1f - nL * nL);   // same arc as base
                float curveR = _curvaturePx * (1f - nR * nR);

                int idx = vh.currentVertCount;
                AddVert(vh, new Vector2(xL, yCenter - halfH + curveL), c);
                AddVert(vh, new Vector2(xR, yCenter - halfH + curveR), c);
                AddVert(vh, new Vector2(xR, yCenter + halfH + curveR), c);
                AddVert(vh, new Vector2(xL, yCenter + halfH + curveL), c);
                vh.AddTriangle(idx, idx + 1, idx + 2);
                vh.AddTriangle(idx, idx + 2, idx + 3);
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
