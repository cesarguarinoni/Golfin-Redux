using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    // Upward-pointing arrow: stem rectangle + triangular head.
    // Pivot must be (0.5, 0) — arrow tip points up, base at y=0.
    // Set color externally to tint the whole arrow (MaskableGraphic.color).
    [RequireComponent(typeof(CanvasRenderer))]
    public class ArrowGraphic : MaskableGraphic
    {
        [SerializeField] private float _stemWidthPx  = 8f;
        [SerializeField] private float _headWidthPx  = 22f;
        [SerializeField] private float _headHeightPx = 16f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float totalH = rectTransform.rect.height;
            float stemH  = Mathf.Max(0f, totalH - _headHeightPx);
            float sw2    = _stemWidthPx * 0.5f;
            float hw2    = _headWidthPx * 0.5f;

            // Stem (rectangle, bottom-center to mid)
            Add(vh, -sw2,   0f,    color); // 0 BL
            Add(vh,  sw2,   0f,    color); // 1 BR
            Add(vh,  sw2,  stemH,  color); // 2 TR
            Add(vh, -sw2,  stemH,  color); // 3 TL
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(0, 2, 3);

            // Head (triangle, wide base → narrow tip)
            Add(vh, -hw2,  stemH,  color); // 4 base-left
            Add(vh,  hw2,  stemH,  color); // 5 base-right
            Add(vh,  0f,   totalH, color); // 6 tip
            vh.AddTriangle(4, 5, 6);
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
