using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Filled isoceles-triangle cone, drawn as a UIVertex mesh so width rebuilds
    // cheaply when Club Accuracy changes (SetVerticesDirty costs nothing at rest).
    // Pivot must be (0.5, 0) — apex points up, base at y=0 in local space.
    [RequireComponent(typeof(CanvasRenderer))]
    public class ConeMeshGraphic : MaskableGraphic
    {
        [SerializeField] private float _halfAngleDeg = 12.5f;
        [SerializeField] private float _heightPx     = 600f;

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

        // Half-base width in local px at the current settings.
        public float HalfBasePx => _heightPx * Mathf.Tan(_halfAngleDeg * Mathf.Deg2Rad);

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float hb = HalfBasePx;

            // Local space: pivot at bottom-center.
            // apex = top-center, baseL/R = bottom corners.
            AddVert(vh, new Vector2(0f,  _heightPx), color);   // 0 apex
            AddVert(vh, new Vector2(-hb, 0f),        color);   // 1 base-left
            AddVert(vh, new Vector2( hb, 0f),        color);   // 2 base-right

            vh.AddTriangle(0, 1, 2);
        }

        private static void AddVert(VertexHelper vh, Vector2 pos, Color32 c)
        {
            var v       = UIVertex.simpleVert;
            v.position  = pos;
            v.color     = c;
            vh.AddVert(v);
        }
    }
}
