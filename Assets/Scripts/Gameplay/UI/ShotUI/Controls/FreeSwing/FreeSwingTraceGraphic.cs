using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.Controls.FreeSwing
{
    /// <summary>
    /// The finger trace, as one mesh: a round-capped, round-joined polyline through the driver's
    /// sample buffer, with the node's drop shadow drawn into the same mesh underneath it
    /// (Figma <c>FingerTrace</c> 14145:38393 — <c>stroke-width 8</c>, <c>stroke-linecap round</c>,
    /// <c>stroke-linejoin round</c>).
    ///
    /// <para>GEOMETRY, NOT A SPRITE, for the same reason <see cref="Needle.NeedleArcGraphic"/> is:
    /// the shape is the player's own gesture, which is different on every swing and grows sixty
    /// times a second. There is no PNG of that. It is the identical call
    /// <c>PowerGaugeGraphic</c>, <c>ConeMeshGraphic</c> and <c>PutterTrackGraphic</c> already
    /// make in this project.</para>
    ///
    /// <para>ROUND JOINS ARE DISCS, NOT MITRES. A quad per segment plus a disc at every interior
    /// vertex reproduces <c>stroke-linejoin: round</c> exactly and — unlike a mitre — cannot
    /// spike when the finger reverses through 180°, which is precisely what this gesture does at
    /// the bottom of the backswing. The caps are the same disc at the two ends.</para>
    ///
    /// <para>THE SHADOW IS AN OFFSET COPY, NOT A BLUR. The node's filter is a 2px Gaussian and a
    /// UI mesh cannot blur; the alternative is a uGUI <c>Shadow</c> component, which UI Rule 21
    /// reads as fabricated chrome and which could not follow a mesh anyway. Drawn into THIS mesh
    /// so the two can never drift apart by a frame.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class FreeSwingTraceGraphic : MaskableGraphic
    {
        [Header("Figma: FingerTrace (14145:38393)")]
        [Tooltip("Stroke width in canvas px. Node: stroke-width 8.")]
        [SerializeField] private float _width = 8f;
        [Tooltip("Segments per round join/cap disc. 12 keeps a 4px radius's chord error under " +
                 "a tenth of a pixel, which is well under a canvas pixel at this canvas.")]
        [SerializeField] private int _capSegments = 12;

        [Header("Shadow (SVG filter0_d: dy 2, black 40%)")]
        [SerializeField] private Color _shadowColor  = new Color(0f, 0f, 0f, 0.4f);
        [SerializeField] private Vector2 _shadowOffset = new Vector2(0f, -2f);
        [SerializeField] private bool  _drawShadow   = true;

        private readonly List<Vector2> _points = new List<Vector2>(128);

        /// <summary>How many samples the trace is currently drawing. Read back by the tests and
        /// the acceptance run — "the trace is drawing" is a vertex count, not an opinion about a
        /// screenshot.</summary>
        public int PointCount => _points.Count;

        /// <summary>The stroke width as drawn, so a fidelity check reads the live value rather
        /// than the node's.</summary>
        public float Width { get => _width; set { _width = value; SetVerticesDirty(); } }

        public Color ShadowColor { get => _shadowColor; set { _shadowColor = value; SetVerticesDirty(); } }

        /// <summary>
        /// Replace the whole polyline. One call per frame from the view, rather than an Add per
        /// sample, so the mesh is never left half-built across two <c>SetVerticesDirty</c> passes
        /// and the graphic has no opinion about how the driver buffers.
        /// </summary>
        public void SetPoints(IReadOnlyList<Vector2> points)
        {
            _points.Clear();
            if (points != null)
                for (int i = 0; i < points.Count; i++) _points.Add(points[i]);
            SetVerticesDirty();
        }

        public void Clear()
        {
            if (_points.Count == 0) return;
            _points.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_points.Count == 0 || _width <= 0f) return;

            if (_drawShadow && _shadowColor.a > 0f)
                AddStroke(vh, _shadowOffset, _shadowColor);
            AddStroke(vh, Vector2.zero, color);
        }

        /// <summary>One pass of the polyline: a quad per segment, a disc per vertex.</summary>
        private void AddStroke(VertexHelper vh, Vector2 offset, Color32 c)
        {
            float half = _width * 0.5f;

            // A single-sample gesture is a dot, and drawing it is what makes the trace appear on
            // the very first frame of a touch rather than on the second.
            for (int i = 0; i < _points.Count; i++)
                AddDisc(vh, _points[i] + offset, half, c);

            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 a = _points[i] + offset, b = _points[i + 1] + offset;
                Vector2 d = b - a;
                float   L = d.magnitude;
                if (L < 1e-4f) continue;           // a stationary finger adds no segment, only its disc
                Vector2 n = new Vector2(-d.y, d.x) / L * half;
                AddQuad(vh, a - n, a + n, b + n, b - n, c);
            }
        }

        private static void AddQuad(VertexHelper vh, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color32 c)
        {
            int b = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = p0; vh.AddVert(v);
            v.position = p1; vh.AddVert(v);
            v.position = p2; vh.AddVert(v);
            v.position = p3; vh.AddVert(v);
            vh.AddTriangle(b, b + 1, b + 2);
            vh.AddTriangle(b + 2, b + 3, b);
        }

        private void AddDisc(VertexHelper vh, Vector2 centre, float radius, Color32 c)
        {
            int n = Mathf.Max(_capSegments, 6);
            int b = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = centre; vh.AddVert(v);
            for (int i = 0; i <= n; i++)
            {
                float a = (i / (float)n) * Mathf.PI * 2f;
                v.position = new Vector2(centre.x + Mathf.Cos(a) * radius,
                                         centre.y + Mathf.Sin(a) * radius);
                vh.AddVert(v);
            }
            for (int i = 0; i < n; i++) vh.AddTriangle(b, b + 1 + i, b + 2 + i);
        }
    }
}
