using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Procedural MaskableGraphic for the putter track vertical lane.
    /// Draws a gradient body (darker at center, lighter at edges) plus three
    /// horizontal band lines (green / amber / red) at the heights specified in the spec.
    /// Pivot (0.5, 1) — top-center, matching the cone's anchoring convention.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class PutterTrackGraphic : MaskableGraphic
    {
        [SerializeField] float _width             = 140f;
        [SerializeField] float _height            = 1000f;
        [SerializeField] float _greenBandHeight   = 200f;
        [SerializeField] float _amberBandHeight   = 300f;
        [SerializeField] float _bandLineThickness = 4f;

        [SerializeField] Color _greenBandColor  = new Color32(0x62, 0x73, 0x52, 0xFF);
        [SerializeField] Color _amberBandColor  = new Color32(0x8F, 0x72, 0x40, 0xFF);
        [SerializeField] Color _redBandColor    = new Color32(0x7A, 0x3E, 0x3E, 0xFF);
        [SerializeField] Color _gradientEdge    = new Color(0f, 0f, 0f, 0.15f);
        [SerializeField] Color _gradientCenter  = new Color(0f, 0f, 0f, 0.5f);

        // Y-axis: pivot is top-center, so top = y=0, bottom = y=-_height.

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float halfW  = _width * 0.5f;
            float top    = 0f;
            float bottom = -_height;

            // ── Gradient body: 6-vertex 2-quad strip (left edge / center / right edge) ──
            // Left column: x = -halfW, color = _gradientEdge
            // Center column: x = 0,     color = _gradientCenter
            // Right column: x = +halfW, color = _gradientEdge

            // v0  left-top
            // v1  left-bottom
            // v2  center-top
            // v3  center-bottom
            // v4  right-top
            // v5  right-bottom

            var v0 = UIVertex.simpleVert; v0.position = new Vector3(-halfW, top,    0); v0.color = _gradientEdge;
            var v1 = UIVertex.simpleVert; v1.position = new Vector3(-halfW, bottom, 0); v1.color = _gradientEdge;
            var v2 = UIVertex.simpleVert; v2.position = new Vector3(0f,     top,    0); v2.color = _gradientCenter;
            var v3 = UIVertex.simpleVert; v3.position = new Vector3(0f,     bottom, 0); v3.color = _gradientCenter;
            var v4 = UIVertex.simpleVert; v4.position = new Vector3(halfW,  top,    0); v4.color = _gradientEdge;
            var v5 = UIVertex.simpleVert; v5.position = new Vector3(halfW,  bottom, 0); v5.color = _gradientEdge;

            // Assign default UV
            v0.uv0 = new Vector4(0f, 1f); v1.uv0 = new Vector4(0f, 0f);
            v2.uv0 = new Vector4(0.5f, 1f); v3.uv0 = new Vector4(0.5f, 0f);
            v4.uv0 = new Vector4(1f, 1f); v5.uv0 = new Vector4(1f, 0f);

            vh.AddVert(v0); // 0
            vh.AddVert(v1); // 1
            vh.AddVert(v2); // 2
            vh.AddVert(v3); // 3
            vh.AddVert(v4); // 4
            vh.AddVert(v5); // 5

            // Left quad: v0 v1 v3 v2
            vh.AddTriangle(0, 1, 3);
            vh.AddTriangle(0, 3, 2);

            // Right quad: v2 v3 v5 v4
            vh.AddTriangle(2, 3, 5);
            vh.AddTriangle(2, 5, 4);

            // ── Band lines ──
            // Green: y = -_greenBandHeight
            AddBandLine(vh, -_greenBandHeight, _greenBandColor, halfW);

            // Amber: y = -(_greenBandHeight + _amberBandHeight) = -500
            AddBandLine(vh, -(_greenBandHeight + _amberBandHeight), _amberBandColor, halfW);

            // Red: y = -_height (bottom edge)
            AddBandLine(vh, -_height, _redBandColor, halfW);
        }

        private void AddBandLine(VertexHelper vh, float yCenter, Color col, float halfW)
        {
            float halfT  = _bandLineThickness * 0.5f;
            float yTop   = yCenter + halfT;
            float yBot   = yCenter - halfT;

            int baseIdx = vh.currentVertCount;

            var vA = UIVertex.simpleVert; vA.position = new Vector3(-halfW, yTop, 0); vA.color = col;
            var vB = UIVertex.simpleVert; vB.position = new Vector3(-halfW, yBot, 0); vB.color = col;
            var vC = UIVertex.simpleVert; vC.position = new Vector3( halfW, yBot, 0); vC.color = col;
            var vD = UIVertex.simpleVert; vD.position = new Vector3( halfW, yTop, 0); vD.color = col;

            vh.AddVert(vA); // baseIdx + 0
            vh.AddVert(vB); // baseIdx + 1
            vh.AddVert(vC); // baseIdx + 2
            vh.AddVert(vD); // baseIdx + 3

            vh.AddTriangle(baseIdx + 0, baseIdx + 1, baseIdx + 2);
            vh.AddTriangle(baseIdx + 0, baseIdx + 2, baseIdx + 3);
        }
    }
}
