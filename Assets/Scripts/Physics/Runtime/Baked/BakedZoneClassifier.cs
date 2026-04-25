using System;
using System.Collections.Generic;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime.Baked
{
    /// <summary>
    /// ISurfaceProvider implementation that classifies a world XZ point against a
    /// baked set of zone polygons (loaded from <see cref="ZoneData"/> JSON).
    ///
    /// Replaces <see cref="SceneSurfaceProvider"/> in the sim path post-pivot
    /// (M3). Also exposes the matching zone-Y-offset so <see cref="BakedHeightProvider"/>
    /// can layer the overlay-mesh offsets on top of the heightmap terrain Y.
    ///
    /// Priority order (highest first; first matching polygon wins):
    ///
    ///   Green &gt; Sand &gt; Water &gt; GreenCollar &gt; Tee &gt; CartPath &gt; Fairway &gt; Rough (default)
    ///
    /// Implementation: flat-scan point-in-polygon for now. The spec calls out
    /// "spatial index is M2 if needed" — not needed at this scale (single hole,
    /// ~30 zones, ~hundreds of polygon vertices).
    /// </summary>
    public sealed class BakedZoneClassifier : ISurfaceProvider
    {
        // Compiled-from-ZoneData representation. Each entry is a zone polygon
        // tagged with its SurfaceType + Y offset, sorted by descending priority
        // so the first match in the loop wins.
        private struct CompiledPolygon
        {
            public SurfaceType type;
            public float       yOffset;
            public float[]     xs;     // Polygon vertex X coords
            public float[]     ys;     // Polygon vertex Y (mesh top) coords — used by SampleZoneY (Path A)
            public float[]     zs;     // Polygon vertex Z coords
            // Axis-aligned bounding box for fast-reject.
            public float       minX, maxX, minZ, maxZ;
            // True if any point of this polygon has a non-zero Y (i.e. baked
            // from a real mesh, not a synthetic test). Determines whether
            // SampleZoneY is meaningful for this polygon.
            public bool        hasMeshY;
        }

        private readonly CompiledPolygon[] polygons;
        private readonly Dictionary<SurfaceType, float> yOffsetByType;

        /// <summary>The default surface returned when no polygon contains the test point.</summary>
        public const SurfaceType DefaultSurface = SurfaceType.Fairway;

        public BakedZoneClassifier(ZoneData data)
        {
            if (data == null || data.zones == null)
            {
                polygons = Array.Empty<CompiledPolygon>();
                yOffsetByType = new Dictionary<SurfaceType, float>();
                return;
            }

            yOffsetByType = new Dictionary<SurfaceType, float>(data.zones.Count);
            var compiled = new List<CompiledPolygon>(data.zones.Count * 4);

            foreach (var group in data.zones)
            {
                SurfaceType st = group.SurfaceType;
                yOffsetByType[st] = group.yOffsetFromTerrain;

                if (group.polygons == null) continue;
                foreach (var poly in group.polygons)
                {
                    if (poly == null || poly.points == null || poly.points.Count < 3) continue;

                    int n = poly.points.Count;
                    var xs = new float[n];
                    var ys = new float[n];
                    var zs = new float[n];
                    float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
                    float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
                    bool hasMeshY = false;
                    for (int i = 0; i < n; i++)
                    {
                        xs[i] = poly.points[i].x;
                        ys[i] = poly.points[i].y;
                        zs[i] = poly.points[i].z;
                        if (xs[i] < minX) minX = xs[i];
                        if (xs[i] > maxX) maxX = xs[i];
                        if (zs[i] < minZ) minZ = zs[i];
                        if (zs[i] > maxZ) maxZ = zs[i];
                        if (ys[i] != 0f) hasMeshY = true;
                    }

                    compiled.Add(new CompiledPolygon
                    {
                        type    = st,
                        yOffset = group.yOffsetFromTerrain,
                        xs      = xs,
                        ys      = ys,
                        zs      = zs,
                        minX    = minX, maxX = maxX,
                        minZ    = minZ, maxZ = maxZ,
                        hasMeshY = hasMeshY,
                    });
                }
            }

            // Stable sort by descending priority. ".OrderBy" would allocate an enumerator
            // — use Array.Sort with a comparer for zero-alloc.
            polygons = compiled.ToArray();
            Array.Sort(polygons, (a, b) => Priority(b.type).CompareTo(Priority(a.type)));
        }

        // ── ISurfaceProvider ──────────────────────────────────────────────────
        public SurfaceType Classify(fp worldX, fp worldZ)
        {
            float x = worldX.ToFloat();
            float z = worldZ.ToFloat();

            for (int i = 0; i < polygons.Length; i++)
            {
                ref readonly var p = ref polygons[i];
                if (x < p.minX || x > p.maxX || z < p.minZ || z > p.maxZ) continue;
                if (PointInPolygon(p.xs, p.zs, x, z)) return p.type;
            }
            return DefaultSurface;
        }

        /// <summary>
        /// Y offset from the terrain heightmap to the visible top of a zone of
        /// the given type. Returns 0 for unknown / unmapped types so the baked
        /// height provider falls back to the heightmap directly.
        /// </summary>
        public float GetYOffset(SurfaceType type)
        {
            return yOffsetByType.TryGetValue(type, out float v) ? v : 0f;
        }

        /// <summary>
        /// Path A height sampler: returns the visible mesh-surface Y at (worldX, worldZ)
        /// directly from the highest-priority polygon's vertex Ys (inverse-distance-weighted).
        /// Used by <see cref="BakedHeightProvider"/> to bypass the heightmap-vs-mesh
        /// depression-band conflict.
        ///
        /// <paramref name="y"/> set to the IDW-interpolated mesh Y; <paramref name="type"/>
        /// to the matched surface type. Returns true on match.
        ///
        /// Returns false when no polygon contains the point OR the matched polygon
        /// has no mesh-Y data (synthetic test fixture). In both cases the caller
        /// should fall back to the heightmap path.
        /// </summary>
        public bool TrySampleMeshY(fp worldX, fp worldZ, out SurfaceType type, out float y)
        {
            float x = worldX.ToFloat();
            float z = worldZ.ToFloat();

            for (int i = 0; i < polygons.Length; i++)
            {
                ref readonly var p = ref polygons[i];
                if (x < p.minX || x > p.maxX || z < p.minZ || z > p.maxZ) continue;
                if (!PointInPolygon(p.xs, p.zs, x, z)) continue;

                type = p.type;

                if (!p.hasMeshY)
                {
                    y = 0f;
                    return false;
                }

                // IDW interpolation on polygon boundary vertices, p=2.
                // Y(x,z) = Σ (yi / di²) / Σ (1 / di²) with a small epsilon
                // floor on di² so vertex-coincidence doesn't divide-by-zero.
                double sumW   = 0.0;
                double sumWY  = 0.0;
                int n = p.xs.Length;
                for (int k = 0; k < n; k++)
                {
                    float dx = x - p.xs[k];
                    float dz = z - p.zs[k];
                    double d2 = dx * dx + dz * dz + 1e-9;
                    double w  = 1.0 / d2;
                    sumW  += w;
                    sumWY += w * p.ys[k];
                }
                y = (float)(sumWY / sumW);
                return true;
            }

            type = DefaultSurface;
            y    = 0f;
            return false;
        }

        // ── Priority ──────────────────────────────────────────────────────────
        // Higher number = higher priority = picked first when polygons overlap.
        private static int Priority(SurfaceType t)
        {
            switch (t)
            {
                case SurfaceType.Green:       return 100;
                case SurfaceType.Sand:        return 90;
                case SurfaceType.BunkerLip:   return 89;  // submesh of Sand; same priority band
                case SurfaceType.Water:       return 80;
                case SurfaceType.GreenCollar: return 70;
                case SurfaceType.Tee:         return 60;
                case SurfaceType.CartPath:    return 50;
                case SurfaceType.Fairway:     return 40;
                case SurfaceType.Semirough:   return 20;
                case SurfaceType.Rough:       return 10;
                case SurfaceType.OOB:         return 5;
                default:                      return 0;
            }
        }

        // ── Geometry helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Standard ray-casting point-in-polygon test. Edge points are NOT
        /// considered inside (strict-interior). Robust for axis-aligned and
        /// arbitrary polygons; handles non-convex.
        /// </summary>
        private static bool PointInPolygon(float[] xs, float[] zs, float px, float pz)
        {
            bool inside = false;
            int n = xs.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                bool intersect = ((zs[i] > pz) != (zs[j] > pz)) &&
                                 (px < (xs[j] - xs[i]) * (pz - zs[i]) / (zs[j] - zs[i] + 1e-9f) + xs[i]);
                if (intersect) inside = !inside;
            }
            return inside;
        }
    }
}
