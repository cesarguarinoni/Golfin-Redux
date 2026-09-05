using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>
    /// One annulus segment — a band of given thickness on an ellipse, spanning a given angular
    /// sweep about a given centre angle. Every curved element in this scheme is an instance of it:
    /// the three power rings, the overpower crescent, the accuracy arc, and the two zones.
    ///
    /// <para>GEOMETRY, NOT A SPRITE, AND THAT IS THE POINT. This scheme's radii are DERIVED (a
    /// ring is drawn where the club head lands at that power, so it moves when the pull thresholds
    /// are retuned) and its zone widths are DERIVED (the drawn blue segment must be exactly the
    /// graded window, at the peak power, every drag frame). Neither can come from a baked PNG at a
    /// fixed size: a 1052px ring would have to be re-baked on every retune, and a zone whose angle
    /// changes 60 times a second cannot be a sprite at all. A mesh is exact at any radius and any
    /// angle, has no import settings to get wrong, and cannot suffer the 9-slice corner collapse
    /// that UI Rule 21 exists to catch. <c>PowerGaugeGraphic</c>, <c>ConeMeshGraphic</c> and
    /// <c>PutterTrackGraphic</c> are the same call in this project already.</para>
    ///
    /// <para>ANGLES ARE DEGREES FROM THE TOP, POSITIVE CLOCKWISE — the same convention
    /// <c>PowerGaugeGraphic</c> uses (<c>x = sin θ</c>, <c>y = cos θ</c>). It is also the needle's
    /// own convention: the needle sits at <c>n × 90°</c>, so a zone of half-width <c>w</c> (a
    /// fraction of the 90° half-sweep) is drawn at <c>±w × 90°</c> with no conversion in between.
    /// That absence of a conversion is what makes "the graded window is the one that was drawn"
    /// checkable rather than hopeful.</para>
    ///
    /// <para>THE RADIUS IS EVALUATED ALONG THE RAY, not parametrically. For the circular swing arc
    /// the two agree. For the PUTT'S FLATTENED ELLIPSE they do not, and the ray version is the
    /// correct one here: the needle is a straight bar rotated by <c>n × 90°</c>, so the point of
    /// the arc the player reads as "where the needle is" is the point the ray at that angle hits.
    /// A parametric sweep would drift away from the needle tip near the ends of a putt arc, and
    /// the picture would stop agreeing with the verdict.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class NeedleArcGraphic : MaskableGraphic
    {
        [Header("Ellipse (canvas px, outer edge)")]
        [SerializeField] private float _radiusX   = 230f;
        [SerializeField] private float _radiusY   = 230f;
        [Tooltip("Band thickness, measured inward along the ray from the outer edge.")]
        [SerializeField] private float _thickness = 44f;

        [Header("Sweep (degrees from the top, positive clockwise)")]
        [SerializeField] private float _centerDeg = 0f;
        [Tooltip("Total sweep. 360 draws a closed ring; 180 centred on 0 draws the top half.")]
        [SerializeField] private float _sweepDeg  = 180f;

        [Tooltip("Quads per 360 degrees. 96 keeps a 526px ring's chord error under a third of a pixel.")]
        [SerializeField] private int   _segmentsPerTurn = 96;

        public float RadiusX   { get => _radiusX;   set => Set(ref _radiusX,   value); }
        public float RadiusY   { get => _radiusY;   set => Set(ref _radiusY,   value); }
        public float Thickness { get => _thickness; set => Set(ref _thickness, value); }
        public float CenterDeg { get => _centerDeg; set => Set(ref _centerDeg, value); }
        public float SweepDeg  { get => _sweepDeg;  set => Set(ref _sweepDeg,  value); }

        /// <summary>Half the sweep, in degrees — the number a zone is actually specified by, and
        /// the one a test or an invariant dump reads back to compare against the graded window.</summary>
        public float HalfSweepDeg => _sweepDeg * 0.5f;

        /// <summary>Set the whole ellipse at once. One call per layout, so a view never leaves the
        /// mesh half-updated across two <c>SetVerticesDirty</c> passes.</summary>
        public void SetEllipse(float radiusX, float radiusY, float thickness)
        {
            if (Mathf.Approximately(_radiusX, radiusX) && Mathf.Approximately(_radiusY, radiusY)
                && Mathf.Approximately(_thickness, thickness)) return;
            _radiusX = radiusX; _radiusY = radiusY; _thickness = thickness;
            SetVerticesDirty();
        }

        /// <summary>Set the segment's angular extent. <paramref name="halfSweepDeg"/> is the
        /// half-width each side of <paramref name="centerDeg"/>.</summary>
        public void SetSweep(float centerDeg, float halfSweepDeg)
        {
            float sweep = Mathf.Clamp(halfSweepDeg * 2f, 0f, 360f);
            if (Mathf.Approximately(_centerDeg, centerDeg) && Mathf.Approximately(_sweepDeg, sweep)) return;
            _centerDeg = centerDeg; _sweepDeg = sweep;
            SetVerticesDirty();
        }

        private void Set(ref float field, float value)
        {
            if (Mathf.Approximately(field, value)) return;
            field = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float sweep = Mathf.Clamp(_sweepDeg, 0f, 360f);
            if (sweep <= 0f || _thickness <= 0f || _radiusX <= 0f || _radiusY <= 0f) return;

            float step  = 360f / Mathf.Max(_segmentsPerTurn, 8);
            int   count = Mathf.Max(1, Mathf.CeilToInt(sweep / step));
            float start = _centerDeg - sweep * 0.5f;
            Color32 c   = color;

            for (int i = 0; i < count; i++)
            {
                float a0 = start + sweep * (i       / (float)count);
                float a1 = start + sweep * ((i + 1) / (float)count);
                AddQuad(vh, a0, a1, c);
            }
        }

        private void AddQuad(VertexHelper vh, float deg0, float deg1, Color32 c)
        {
            Vector2 o0 = OuterAt(deg0), o1 = OuterAt(deg1);
            Vector2 i0 = Inner(o0),     i1 = Inner(o1);

            int b = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = c;
            v.position = i0; vh.AddVert(v);
            v.position = o0; vh.AddVert(v);
            v.position = o1; vh.AddVert(v);
            v.position = i1; vh.AddVert(v);
            vh.AddTriangle(b, b + 1, b + 2);
            vh.AddTriangle(b, b + 2, b + 3);
        }

        /// <summary>Where the ray at <paramref name="deg"/> (from the top, clockwise) leaves the
        /// outer ellipse.</summary>
        private Vector2 OuterAt(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float sx = Mathf.Sin(r), cy = Mathf.Cos(r);
            // r(θ) for an axis-aligned ellipse along the ray (sinθ, cosθ).
            float denom = Mathf.Sqrt(sx * sx * _radiusY * _radiusY + cy * cy * _radiusX * _radiusX);
            float rad   = denom > 1e-4f ? (_radiusX * _radiusY) / denom : 0f;
            return new Vector2(sx * rad, cy * rad);
        }

        /// <summary>The inner edge: the outer point pulled <see cref="_thickness"/> px straight
        /// back toward the centre. Along the ray rather than as a second ellipse, so the band's
        /// thickness is the number the design states even where the ellipse is flattened.</summary>
        private Vector2 Inner(Vector2 outer)
        {
            float len = outer.magnitude;
            if (len <= 1e-4f) return Vector2.zero;
            return outer * (Mathf.Max(len - _thickness, 0f) / len);
        }
    }
}
