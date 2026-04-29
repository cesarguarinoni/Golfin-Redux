using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Horizontal trapezoidal slab that travels up the aiming cone during Timing state.
    // Wider at the bottom than the top, matching the cone's narrowing geometry.
    // Pivot must match the cone: (0.5, 0) — Y=0 is the cone base.
    // Caller sets CurrentY01 (0=base, 1=apex) and color each tick.
    [RequireComponent(typeof(CanvasRenderer))]
    public class TimingSlabGraphic : MaskableGraphic
    {
        [SerializeField] private float _coneHeightPx     = 1009f;
        [SerializeField] private float _coneHalfAngleDeg = 12.5f;
        [SerializeField] private float _curvaturePx      = 15f;
        [SerializeField] private float _slabHalfHeightPx = 30f;

        private float _currentY01;

        public float CurrentY01
        {
            get => _currentY01;
            set { _currentY01 = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        public void SetConeParams(float heightPx, float halfAngleDeg, float curvaturePx)
        {
            _coneHeightPx     = heightPx;
            _coneHalfAngleDeg = halfAngleDeg;
            _curvaturePx      = curvaturePx;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float halfBasePx  = _coneHeightPx * Mathf.Tan(_coneHalfAngleDeg * Mathf.Deg2Rad);
            // Remap so y01=0 → bottom edge at cone base, y01=1 → top edge at cone tip.
            float usableRange = Mathf.Max(0f, _coneHeightPx - 2f * _slabHalfHeightPx);
            float centerY     = _slabHalfHeightPx + _currentY01 * usableRange;
            float topY        = centerY + _slabHalfHeightPx;
            float bottomY     = centerY - _slabHalfHeightPx;

            float topHW    = Mathf.Max(0f, halfBasePx * (1f - topY    / _coneHeightPx));
            float bottomHW = Mathf.Max(0f, halfBasePx * (1f - bottomY / _coneHeightPx));
            float wRatioTop = halfBasePx > 0f ? topHW    / halfBasePx : 0f;
            float wRatioBot = halfBasePx > 0f ? bottomHW / halfBasePx : 0f;

            Color32 c = color;
            const int N = 128;
            int vBase = vh.currentVertCount;

            for (int i = 0; i <= N; i++)
            {
                float t = (float)i / N;

                float xBot = Mathf.Lerp(-bottomHW, bottomHW, t);
                float nBot = bottomHW > 0f ? xBot / bottomHW : 0f;
                float curveBot = -_curvaturePx * wRatioBot * wRatioBot * (1f - nBot * nBot);

                float xTop = Mathf.Lerp(-topHW, topHW, t);
                float nTop = topHW > 0f ? xTop / topHW : 0f;
                float curveTop = -_curvaturePx * wRatioTop * wRatioTop * (1f - nTop * nTop);

                Add(vh, xBot, bottomY + curveBot, c);
                Add(vh, xTop, topY    + curveTop, c);
            }

            for (int i = 0; i < N; i++)
            {
                int bl = vBase + 2 * i,       tl = bl + 1;
                int br = vBase + 2 * (i + 1), tr = br + 1;
                vh.AddTriangle(bl, br, tr);
                vh.AddTriangle(bl, tr, tl);
            }
        }

        private static void Add(VertexHelper vh, float x, float y, Color32 c)
        {
            var v      = UIVertex.simpleVert;
            v.position = new Vector3(x, y, 0f);
            v.color    = c;
            vh.AddVert(v);
        }
    }
}
