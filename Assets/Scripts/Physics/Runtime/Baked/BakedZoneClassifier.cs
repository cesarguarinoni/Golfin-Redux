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
    /// Replaces the legacy scene-raycast surface provider (removed in Phase F)
    /// in the sim path post-pivot (M3). Also exposes the matching zone-Y-offset
    /// so <see cref="BakedHeightProvider"/> can layer the overlay-mesh offsets
    /// on top of the heightmap terrain Y.
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

        // Per-zone triangulated mesh: surface type → packed triangle data.
        // Populated by BakeZoneJsonTool from every mesh + triangle index of
        // every MeshFilter of that type. Used by TrySampleMeshY for exact
        // barycentric Y interpolation — eliminates IDW residual entirely.
        private struct CompiledMesh
        {
            public float[] xs;
            public float[] ys;
            public float[] zs;
            public int[]   indices; // groups of 3
        }
        private readonly Dictionary<SurfaceType, CompiledMesh> meshByType;

        // OB mask state (decoded once from base64 at construction).
        private readonly byte[]  obMaskBits;
        private readonly int     obMaskWidth;
        private readonly int     obMaskHeight;
        private readonly float   obWorldOriginX;
        private readonly float   obWorldOriginZ;
        private readonly float   obCellW;     // worldSizeX / width
        private readonly float   obCellH;     // worldSizeZ / height
        private readonly bool    hasObMask;

        /// <summary>The default surface returned when no polygon contains the test point.</summary>
        public const SurfaceType DefaultSurface = SurfaceType.Fairway;

        public BakedZoneClassifier(ZoneData data)
        {
            meshByType = new Dictionary<SurfaceType, CompiledMesh>();

            if (data == null || data.zones == null)
            {
                polygons = Array.Empty<CompiledPolygon>();
                yOffsetByType = new Dictionary<SurfaceType, float>();
                return;
            }

            // Decode OB mask if present.
            if (data.obMask != null
                && data.obMask.width > 0 && data.obMask.height > 0
                && !string.IsNullOrEmpty(data.obMask.maskBase64))
            {
                obMaskBits     = Convert.FromBase64String(data.obMask.maskBase64);
                obMaskWidth    = data.obMask.width;
                obMaskHeight   = data.obMask.height;
                obWorldOriginX = data.obMask.worldOriginX;
                obWorldOriginZ = data.obMask.worldOriginZ;
                obCellW        = data.obMask.worldSizeX / obMaskWidth;
                obCellH        = data.obMask.worldSizeZ / obMaskHeight;
                hasObMask      = obCellW > 0 && obCellH > 0 && obMaskBits.Length > 0;
            }

            yOffsetByType = new Dictionary<SurfaceType, float>(data.zones.Count);
            var compiled = new List<CompiledPolygon>(data.zones.Count * 4);

            foreach (var group in data.zones)
            {
                SurfaceType st = group.SurfaceType;
                yOffsetByType[st] = group.yOffsetFromTerrain;

                // Compile triangulated mesh for this zone (Path β).
                if (group.mesh != null
                    && group.mesh.vertices != null && group.mesh.vertices.Count > 0
                    && group.mesh.indices  != null && group.mesh.indices.Count  >= 3)
                {
                    int v = group.mesh.vertices.Count;
                    var cm = new CompiledMesh
                    {
                        xs = new float[v],
                        ys = new float[v],
                        zs = new float[v],
                    };
                    for (int i = 0; i < v; i++)
                    {
                        cm.xs[i] = group.mesh.vertices[i].x;
                        cm.ys[i] = group.mesh.vertices[i].y;
                        cm.zs[i] = group.mesh.vertices[i].z;
                    }
                    cm.indices = new int[group.mesh.indices.Count];
                    for (int i = 0; i < cm.indices.Length; i++)
                        cm.indices[i] = group.mesh.indices[i];
                    meshByType[st] = cm;
                }

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

            // Polygon zones first — they always trump the OB mask (a fairway
            // overlapping an OB-marked terrain cell IS fairway, not OOB).
            for (int i = 0; i < polygons.Length; i++)
            {
                ref readonly var p = ref polygons[i];
                if (x < p.minX || x > p.maxX || z < p.minZ || z > p.maxZ) continue;
                if (PointInPolygon(p.xs, p.zs, x, z)) return p.type;
            }

            // No polygon match: consult the OB mask if baked.
            if (hasObMask && IsObAt(x, z)) return SurfaceType.OOB;

            return DefaultSurface;
        }

        private bool IsObAt(float x, float z)
        {
            int ix = (int)((x - obWorldOriginX) / obCellW);
            int iz = (int)((z - obWorldOriginZ) / obCellH);
            if (ix < 0 || ix >= obMaskWidth)  return false;
            if (iz < 0 || iz >= obMaskHeight) return false;
            int bitIdx  = iz * obMaskWidth + ix;
            int byteIdx = bitIdx >> 3;
            int bitMod  = bitIdx & 7;
            return (obMaskBits[byteIdx] & (1 << bitMod)) != 0;
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

                // Path β: barycentric interpolation over the per-zone triangulated
                // mesh. Exact (no interpolation residual) when the test point
                // lands inside any triangle of this zone.
                if (meshByType.TryGetValue(p.type, out CompiledMesh mesh)
                    && TryBarycentricSample(mesh, x, z, out float baryY))
                {
                    y = baryY;
                    return true;
                }

                if (!p.hasMeshY)
                {
                    y = 0f;
                    return false;
                }

                // Final fallback: boundary IDW (synthetic / single-mesh fixtures).
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

        // ── Barycentric helper ────────────────────────────────────────────────

        /// <summary>
        /// Walk the triangle list, find the first triangle whose XZ projection
        /// contains (x, z), and return the barycentric-interpolated Y. Returns
        /// false if no triangle contains the point (caller falls back to IDW).
        /// </summary>
        private static bool TryBarycentricSample(in CompiledMesh mesh, float x, float z,
                                                 out float y)
        {
            int triCount = mesh.indices.Length / 3;
            for (int t = 0; t < triCount; t++)
            {
                int i  = t * 3;
                int ia = mesh.indices[i];
                int ib = mesh.indices[i + 1];
                int ic = mesh.indices[i + 2];

                float ax = mesh.xs[ia], az = mesh.zs[ia];
                float bx = mesh.xs[ib], bz = mesh.zs[ib];
                float cx = mesh.xs[ic], cz = mesh.zs[ic];

                // AABB pre-reject for speed.
                float minX = ax < bx ? (ax < cx ? ax : cx) : (bx < cx ? bx : cx);
                float maxX = ax > bx ? (ax > cx ? ax : cx) : (bx > cx ? bx : cx);
                if (x < minX || x > maxX) continue;
                float minZ = az < bz ? (az < cz ? az : cz) : (bz < cz ? bz : cz);
                float maxZ = az > bz ? (az > cz ? az : cz) : (bz > cz ? bz : cz);
                if (z < minZ || z > maxZ) continue;

                // Barycentric coordinates in XZ projection.
                float denom = (bz - cz) * (ax - cx) + (cx - bx) * (az - cz);
                if (denom > -1e-12f && denom < 1e-12f) continue; // degenerate

                float u = ((bz - cz) * (x - cx) + (cx - bx) * (z - cz)) / denom;
                float v = ((cz - az) * (x - cx) + (ax - cx) * (z - cz)) / denom;
                float w = 1f - u - v;

                // Inside (with 1e-5 slack on each edge for numerical safety).
                const float Eps = 1e-5f;
                if (u < -Eps || v < -Eps || w < -Eps) continue;

                y = u * mesh.ys[ia] + v * mesh.ys[ib] + w * mesh.ys[ic];
                return true;
            }

            y = 0f;
            return false;
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
