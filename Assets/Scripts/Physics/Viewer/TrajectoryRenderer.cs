using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Renders a completed Trajectory as a color-coded LineRenderer plus sphere markers
    /// at each TerrainHit. No physics — pure visualization of a pre-computed Trajectory.
    /// Color legend: White=airborne, Orange=bouncing, Green=rolling, Cyan=putting.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TrajectoryRenderer : MonoBehaviour
    {
        LineRenderer _line;
        readonly List<GameObject> _markers = new List<GameObject>();
        GameObject _restMarker;

        // Colors
        static readonly Color AirborneColor  = Color.white;
        static readonly Color BounceColor    = new Color(1f, 0.5f, 0f, 1f);   // orange
        static readonly Color RollColor      = Color.green;
        static readonly Color PuttColor      = Color.cyan;
        static readonly Color GoldColor      = new Color(1f, 0.84f, 0f, 1f);  // rest marker
        static readonly Color GhostColor     = new Color(1f, 1f, 1f, 0.3f);

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.widthMultiplier = 0.08f;
            _line.useWorldSpace   = true;
            _line.positionCount   = 0;
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void Draw(Trajectory t)
        {
            Clear();
            if (t == null || t.samples == null || t.samples.Count == 0) return;

            int n = t.samples.Count;
            var positions = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var s = t.samples[i];
                positions[i] = new Vector3(
                    s.position.x.ToFloat(),
                    s.position.y.ToFloat(),
                    s.position.z.ToFloat());
            }

            _line.positionCount = n;
            _line.SetPositions(positions);

            // Determine phases and build gradient
            bool isPutt = DetectPutt(t);
            _line.colorGradient = BuildGradient(t, isPutt);

            // Terrain-hit sphere markers
            foreach (var hit in t.terrainHits)
            {
                if (hit.IsStop) continue; // rest marker handled separately
                CreateMarker(
                    new Vector3(hit.Position.x.ToFloat(), hit.Position.y.ToFloat(), hit.Position.z.ToFloat()),
                    SurfaceColor(hit.Surface), 0.3f);
            }

            // Rest marker (gold)
            var fp = t.finalPosition;
            _restMarker = CreateMarker(
                new Vector3(fp.x.ToFloat(), fp.y.ToFloat(), fp.z.ToFloat()),
                GoldColor, 0.5f);
        }

        /// <summary>Dim the current trajectory for Fire&amp;Compare ghost display.</summary>
        public void SetGhost(bool ghost)
        {
            if (_line == null) return;
            var col = ghost ? GhostColor : Color.white;
            var g   = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(col, 0f), new GradientColorKey(col, 1f) },
                new[] { new GradientAlphaKey(col.a, 0f), new GradientAlphaKey(col.a, 1f) });
            _line.colorGradient = g;
            foreach (var m in _markers)
                if (m != null) m.SetActive(!ghost);
            if (_restMarker != null) _restMarker.SetActive(!ghost);
        }

        public void Clear()
        {
            if (_line != null) _line.positionCount = 0;
            foreach (var m in _markers)
                if (m != null) Destroy(m);
            _markers.Clear();
            if (_restMarker != null) { Destroy(_restMarker); _restMarker = null; }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        bool DetectPutt(Trajectory t)
        {
            // Putt: single stop hit, no airborne arc (all samples within 0.5m of start Y)
            if (t.terrainHits == null || t.terrainHits.Count != 1) return false;
            if (!t.terrainHits[0].IsStop) return false;
            float startY = t.samples[0].position.y.ToFloat();
            foreach (var s in t.samples)
                if (s.position.y.ToFloat() > startY + 0.5f) return false;
            return true;
        }

        Gradient BuildGradient(Trajectory t, bool isPutt)
        {
            var g = new Gradient();

            if (isPutt)
            {
                g.SetKeys(
                    new[] { new GradientColorKey(PuttColor, 0f), new GradientColorKey(PuttColor, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                return g;
            }

            int n = t.samples.Count;
            if (n <= 1)
            {
                g.SetKeys(
                    new[] { new GradientColorKey(AirborneColor, 0f), new GradientColorKey(AirborneColor, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
                return g;
            }

            // Find sample index of first TerrainHit and roll-start
            float firstHitTime    = float.MaxValue;
            float rollStartTime   = float.MaxValue;

            if (t.terrainHits != null && t.terrainHits.Count > 0)
            {
                firstHitTime = t.terrainHits[0].Time.ToFloat();

                // Roll starts after the last non-stop bounce
                for (int i = t.terrainHits.Count - 1; i >= 0; i--)
                {
                    if (!t.terrainHits[i].IsStop)
                    {
                        rollStartTime = t.terrainHits[i].Time.ToFloat();
                        break;
                    }
                }
                // If all hits are the stop, roll = first hit
                if (rollStartTime == float.MaxValue)
                    rollStartTime = firstHitTime;
            }

            // Convert times to normalized position fractions
            float totalTime  = t.samples[n - 1].time.ToFloat();
            float fFirst     = (totalTime > 0f) ? (firstHitTime  / totalTime) : 1f;
            float fRoll      = (totalTime > 0f) ? (rollStartTime / totalTime) : 1f;
            fFirst = Mathf.Clamp01(fFirst);
            fRoll  = Mathf.Clamp01(fRoll);

            const float eps = 0.001f;
            var colorKeys = new List<GradientColorKey>();
            var alphaKeys = new List<GradientAlphaKey> {
                new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };

            colorKeys.Add(new GradientColorKey(AirborneColor, 0f));

            if (fFirst < 1f - eps)
            {
                colorKeys.Add(new GradientColorKey(AirborneColor, Mathf.Max(0f, fFirst - eps)));
                colorKeys.Add(new GradientColorKey(BounceColor,   fFirst));
            }

            if (fRoll > fFirst + eps && fRoll < 1f - eps)
            {
                colorKeys.Add(new GradientColorKey(BounceColor, Mathf.Max(fFirst, fRoll - eps)));
                colorKeys.Add(new GradientColorKey(RollColor,   fRoll));
            }

            colorKeys.Add(new GradientColorKey(
                (fRoll < 1f - eps) ? RollColor : ((fFirst < 1f - eps) ? BounceColor : AirborneColor), 1f));

            // Gradient allows max 8 color keys
            if (colorKeys.Count > 8) colorKeys.RemoveRange(8, colorKeys.Count - 8);

            g.SetKeys(colorKeys.ToArray(), alphaKeys.ToArray());
            return g;
        }

        GameObject CreateMarker(Vector3 pos, Color col, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position   = pos;
            go.transform.localScale = Vector3.one * size;

            // Disable collider so it doesn't interfere with SceneSurfaceProvider raycasts
            var collider = go.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                mat.color = col;
                rend.material = mat;
            }

            _markers.Add(go);
            return go;
        }

        static Color SurfaceColor(SurfaceType s)
        {
            switch (s)
            {
                case SurfaceType.Fairway:     return new Color(0.4f, 0.8f, 0.4f);
                case SurfaceType.Green:       return new Color(0f, 1f, 0.2f);
                case SurfaceType.GreenCollar: return new Color(0.6f, 0.9f, 0.2f);
                case SurfaceType.Rough:       return new Color(0.1f, 0.45f, 0.1f);
                case SurfaceType.Sand:        return new Color(0.87f, 0.72f, 0.53f);
                case SurfaceType.CartPath:    return Color.gray;
                case SurfaceType.Water:       return Color.blue;
                default:                      return Color.magenta;
            }
        }
    }
}
