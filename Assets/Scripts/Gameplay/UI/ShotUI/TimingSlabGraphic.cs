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
        [SerializeField] private float _coneHeightPx     = 600f;
        [SerializeField] private float _coneHalfAngleDeg = 12.5f;
        [SerializeField] private float _slabHalfHeightPx = 6f;

        private float _currentY01;

        public float CurrentY01
        {
            get => _currentY01;
            set { _currentY01 = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        // Called each tick when the cone dimensions change (e.g. accuracy stat update).
        public void SetConeParams(float heightPx, float halfAngleDeg)
        {
            _coneHeightPx     = heightPx;
            _coneHalfAngleDeg = halfAngleDeg;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float halfBasePx = _coneHeightPx * Mathf.Tan(_coneHalfAngleDeg * Mathf.Deg2Rad);
            float centerY    = _currentY01 * _coneHeightPx;
            float topY       = centerY + _slabHalfHeightPx;
            float bottomY    = centerY - _slabHalfHeightPx;

            // Half-width narrows toward apex: widthAtY = halfBase * (1 - y / height)
            float topHW    = Mathf.Max(0f, halfBasePx * (1f - topY    / _coneHeightPx));
            float bottomHW = Mathf.Max(0f, halfBasePx * (1f - bottomY / _coneHeightPx));

            Color32 c = color;
            Add(vh, -bottomHW, bottomY, c); // 0 BL
            Add(vh,  bottomHW, bottomY, c); // 1 BR
            Add(vh,  topHW,    topY,    c); // 2 TR
            Add(vh, -topHW,    topY,    c); // 3 TL

            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);
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
