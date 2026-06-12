using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// XZ spatial grid over tree instances for O(neighbors) per-step lookup.
    /// Grid cell size = max canopy diameter across all profiles (default 9.0 m → 2×4.5).
    /// Implements ITreeObstacleProvider for injection into BallSimulation.
    ///
    /// Iteration order is deterministic: grid cells are dictionary-keyed by (ix,iz) as a
    /// single long; candidates list is sorted by TreeInstance array index before testing.
    ///
    /// Null / absent CSV input → no trees, zero behaviour change (logged once).
    /// </summary>
    public sealed class TreeObstacleProvider : ITreeObstacleProvider
    {
        // Cell size in meters; must be >= max canopy diameter for no missed adjacents.
        // We use 10m to cover the 2×4.5=9m worst case with margin.
        private const float CellSize = 10f;
        private static readonly fp FpCellSize = fp.FromFloat(CellSize);

        private readonly TreeInstance[] _trees;
        private readonly Dictionary<long, List<int>> _grid; // (cellX,cellZ) → tree indices

        private static bool _nullWarningLogged;

        private TreeObstacleProvider(TreeInstance[] trees)
        {
            _trees = trees;
            _grid  = new Dictionary<long, List<int>>();

            // Insert each tree into all grid cells that its canopy radius overlaps.
            for (int i = 0; i < trees.Length; i++)
            {
                var t = trees[i];
                float wx = t.WorldX.ToFloat();
                float wz = t.WorldZ.ToFloat();
                float r  = t.CanopyRadius.ToFloat();  // max of trunk/canopy

                int cxMin = (int)System.Math.Floor((wx - r) / CellSize);
                int cxMax = (int)System.Math.Floor((wx + r) / CellSize);
                int czMin = (int)System.Math.Floor((wz - r) / CellSize);
                int czMax = (int)System.Math.Floor((wz + r) / CellSize);

                for (int cx = cxMin; cx <= cxMax; cx++)
                for (int cz = czMin; cz <= czMax; cz++)
                {
                    long key = CellKey(cx, cz);
                    if (!_grid.TryGetValue(key, out var bucket))
                    {
                        bucket = new List<int>();
                        _grid[key] = bucket;
                    }
                    if (!bucket.Contains(i))
                        bucket.Add(i);
                }
            }

            // Sort each bucket for stable iteration order.
            foreach (var bucket in _grid.Values)
                bucket.Sort();
        }

        /// <summary>
        /// Create a provider from a list of instances. Returns null (no trees) if list is null/empty.
        /// </summary>
        public static ITreeObstacleProvider Create(List<TreeInstance> instances)
        {
            if (instances == null || instances.Count == 0)
            {
                if (!_nullWarningLogged)
                {
                    _nullWarningLogged = true;
                    UnityEngine.Debug.Log("[TreeObstacleProvider] No tree instances loaded — tree collision disabled for this hole.");
                }
                return null;
            }
            return new TreeObstacleProvider(instances.ToArray());
        }

        /// <summary>
        /// Test segment p0→p1 for tree collisions. Returns:
        ///   true with isTrunk=true  → trunk crossing at earliest frac, reflect velocity.
        ///   true with isTrunk=false → canopy ENTRY crossing (!inside(p0) &amp;&amp; inside(p1)) → one-time entry impulse.
        ///   false → no tree interaction.
        ///
        /// Trunk ALWAYS outranks canopy: a trunk crossing at any frac wins over any canopy
        /// hit. Canopy is only reported when NO trunk hit (from ANY candidate tree) was found.
        ///
        /// Canopy is entry-only: the ball is outside the canopy at p0 and inside at p1.
        /// A ball already inside (from a previous step) does NOT trigger a second impulse.
        /// Each re-entry (ball exits and re-enters) fires its own cut — entry is detected fresh
        /// each step. (D3 revised 2026-06-12: per-step drag rejected as slow-motion; discrete
        /// impulse at contact then normal ballistics resume.)
        /// </summary>
        public bool TestSegment(fp3 p0, fp3 p1, out TreeHit hit)
        {
            hit = default;

            // Which cell does p0 sit in?
            int cx = (int)System.Math.Floor(p0.x.ToFloat() / CellSize);
            int cz = (int)System.Math.Floor(p0.z.ToFloat() / CellSize);

            // Gather candidates from the 3×3 neighborhood (segment can cross at most 2 cells).
            // We check 3×3 around p0 to handle short segments bridging cells.
            List<int> candidates = GetCandidates(cx, cz);
            if (candidates == null) return false;

            // Pass 1: test ALL candidates for trunk crossings first.
            // A trunk hit at any frac beats any canopy hit (canopy is always frac=0).
            // This is the critical fix: without a two-pass approach, a canopy-frac=0 from
            // an overlapping tree was setting bestFrac=0 and permanently masking the trunk
            // hit from the SAME tree (trunkFrac > 0 could never beat bestFrac=0).
            bool trunkFound = false;
            fp bestTrunkFrac = fp.FromInt(2); // > 1 sentinel

            for (int ci = 0; ci < candidates.Count; ci++)
            {
                int idx = candidates[ci];
                var tree = _trees[idx];

                if (TestTrunkCrossing(p0, p1, tree, out fp trunkFrac, out fp3 trunkNormal))
                {
                    if (trunkFrac < bestTrunkFrac)
                    {
                        bestTrunkFrac = trunkFrac;
                        fp3 hitPos = Lerp3(p0, p1, trunkFrac);
                        hit = new TreeHit
                        {
                            Frac     = trunkFrac,
                            HitPos   = hitPos,
                            NormalXZ = trunkNormal,
                            IsTrunk  = true,
                            Profile  = tree.Profile
                        };
                        trunkFound = true;
                    }
                }
            }

            // If any trunk hit was found, return it immediately. Canopy never masks trunk.
            if (trunkFound)
                return true;

            // Pass 2: no trunk hit found — detect canopy ENTRY crossing.
            // D3 (revised): canopy = one-time impulse at the step where the ball transitions
            // from outside the canopy (p0) to inside the canopy (p1).
            // A ball already inside (from a prior step) does NOT trigger a second impulse.
            // Normal ballistics (gravity/drag/magnus) resume immediately after the cut.
            // Each fresh re-entry fires its own cut.
            for (int ci = 0; ci < candidates.Count; ci++)
            {
                int idx = candidates[ci];
                var tree = _trees[idx];

                if (!IsInsideCanopy(p0, tree) && IsInsideCanopy(p1, tree))
                {
                    hit = new TreeHit
                    {
                        Frac     = fp.Zero,
                        HitPos   = p0,
                        NormalXZ = fp3.Zero,
                        IsTrunk  = false,
                        Profile  = tree.Profile
                    };
                    return true;
                }
            }

            return false;
        }

        // ── Private helpers ─────────────────────────────────────────────────────────

        private static long CellKey(int cx, int cz) => ((long)(cx + 100000) << 32) | (uint)(cz + 100000);

        private List<int> GetCandidates(int cx, int cz)
        {
            List<int> result = null;
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                long key = CellKey(cx + dx, cz + dz);
                if (_grid.TryGetValue(key, out var bucket))
                {
                    if (result == null)
                    {
                        result = new List<int>(bucket);
                    }
                    else
                    {
                        for (int i = 0; i < bucket.Count; i++)
                        {
                            int idx = bucket[i];
                            if (!result.Contains(idx))
                                result.Add(idx);
                        }
                    }
                }
            }
            if (result != null)
                result.Sort();
            return result;
        }

        /// <summary>
        /// Ray-vs-infinite-XZ-cylinder test, then clamp to Y range [baseY, trunkTopY].
        /// Returns frac in [0,1] and outward XZ normal at the crossing.
        ///
        /// CONTAINMENT GUARD (iter-4 fix): when p0 is already inside the trunk XZ cylinder
        /// (c &lt; 0) and within the trunk Y range, return frac=0 and push-out normal from p0.
        /// This handles the case where a small fp step fails to detect the wall crossing due to
        /// Q16.16 precision loss in the discriminant (b²-4ac rounds to 0 for near-tangential
        /// micro-steps). Without this guard, a rolling ball near trunk speed can tunnel through
        /// the trunk in small increments without any individual step detecting the wall.
        /// </summary>
        private static bool TestTrunkCrossing(fp3 p0, fp3 p1, TreeInstance tree,
                                               out fp frac, out fp3 normal)
        {
            frac   = fp.Zero;
            normal = fp3.Zero;

            // XZ relative to trunk axis.
            fp dx = p0.x - tree.WorldX;
            fp dz = p0.z - tree.WorldZ;
            fp vx = p1.x - p0.x;
            fp vz = p1.z - p0.z;

            fp r = tree.TrunkRadius;

            // ── Containment guard ─────────────────────────────────────────────────────
            // If p0 is already inside the trunk cylinder AND within the trunk Y range,
            // report a hit at frac=0 with push-out normal. This prevents fp precision loss
            // on micro-steps (small discriminant → rounds to 0 → false miss).
            fp cCheck = dx * dx + dz * dz - r * r;
            if (cCheck < fp.Zero)
            {
                fp p0y = p0.y;
                if (p0y >= tree.BaseY && p0y <= tree.TrunkTopY)
                {
                    // Push-out normal: unit XZ vector from trunk axis toward p0.
                    fp len0 = fpMath.Sqrt(dx * dx + dz * dz);
                    if (len0 <= fp.Epsilon)
                    {
                        fp vLen = fpMath.Sqrt(vx * vx + vz * vz);
                        normal = vLen > fp.Epsilon
                            ? new fp3(vx / vLen, fp.Zero, vz / vLen)
                            : new fp3(fp.One, fp.Zero, fp.Zero);
                    }
                    else
                    {
                        normal = new fp3(dx / len0, fp.Zero, dz / len0);
                    }
                    frac = fp.Zero;
                    return true;
                }
            }

            // Quadratic: |P + t*V|² = r²  in XZ
            fp a = vx * vx + vz * vz;
            if (a <= fp.Epsilon) return false; // no XZ movement

            fp b = fp.FromInt(2) * (dx * vx + dz * vz);
            fp c = dx * dx + dz * dz - r * r;

            fp disc = b * b - fp.FromInt(4) * a * c;
            if (disc < fp.Zero) return false; // misses cylinder

            // sqrt of fp disc
            fp sqrtDisc = fpMath.Sqrt(disc);

            fp t0 = (-b - sqrtDisc) / (fp.FromInt(2) * a);
            fp t1 = (-b + sqrtDisc) / (fp.FromInt(2) * a);

            // We want the entry (smallest positive t within [0,1]).
            fp tHit;
            if (t0 >= fp.Zero && t0 <= fp.One)
                tHit = t0;
            else if (t1 >= fp.Zero && t1 <= fp.One)
                tHit = t1;
            else
                return false;

            // Check Y range of hit.
            fp hitY = p0.y + (p1.y - p0.y) * tHit;
            fp trunkBottom = tree.BaseY;
            fp trunkTop    = tree.TrunkTopY;

            if (hitY < trunkBottom || hitY > trunkTop)
                return false;

            // Compute outward XZ normal at hit.
            fp hitX = p0.x + vx * tHit - tree.WorldX;
            fp hitZ = p0.z + vz * tHit - tree.WorldZ;
            fp len  = fpMath.Sqrt(hitX * hitX + hitZ * hitZ);
            if (len <= fp.Epsilon)
            {
                // Ball is exactly on axis — push outward in direction of travel.
                fp vLen = fpMath.Sqrt(vx * vx + vz * vz);
                normal = vLen > fp.Epsilon
                    ? new fp3(vx / vLen, fp.Zero, vz / vLen)
                    : new fp3(fp.One, fp.Zero, fp.Zero);
            }
            else
            {
                normal = new fp3(hitX / len, fp.Zero, hitZ / len);
            }

            frac = tHit;
            return true;
        }

        /// <summary>
        /// Returns true if point p is inside the canopy cylinder (XZ distance &lt; canopyRadius
        /// and Y in [trunkTopY, canopyTopY]).
        ///
        /// Used to detect ENTRY crossings: called once for p0 (outside check) and once for p1
        /// (inside check). Returns true only when BOTH conditions hold — ball transitioned from
        /// outside to inside the canopy in this step.
        ///
        /// CRITICAL: the lower bound is TrunkTopY, NOT BaseY. Using BaseY caused ground-level
        /// rolling balls (y ≈ 0.02) to be classified "inside canopy", which masked trunk hits.
        /// The SPEC §D1 two-cylinder model: trunk occupies [baseY, trunkTopY], canopy occupies
        /// [trunkTopY, canopyTopY]. These volumes do not overlap.
        /// </summary>
        private static bool IsInsideCanopy(fp3 p, TreeInstance tree)
        {
            if (p.y < tree.TrunkTopY || p.y > tree.CanopyTopY)
                return false;

            fp dx = p.x - tree.WorldX;
            fp dz = p.z - tree.WorldZ;
            fp distSq = dx * dx + dz * dz;
            fp rSq = tree.CanopyRadius * tree.CanopyRadius;
            return distSq < rSq;
        }

        private static fp3 Lerp3(fp3 a, fp3 b, fp t)
            => new fp3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }
}
