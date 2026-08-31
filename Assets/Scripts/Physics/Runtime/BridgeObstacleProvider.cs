using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// XZ spatial grid over bridge parts (railings, piers) for O(neighbors) per-step lookup.
    /// Deliberately a beat-for-beat mirror of <see cref="TreeObstacleProvider"/>: same CellSize,
    /// same CellKey packing, same radius-aware insertion, same 3×3 gather around p0, same
    /// sorted-candidate ordering. Determinism is the whole point — two identical shots must
    /// produce two identical fixed-point trajectories, so candidate order may never depend on
    /// dictionary enumeration order.
    ///
    /// Null / absent CSV input → no bridges, zero behaviour change (logged once).
    /// </summary>
    public sealed class BridgeObstacleProvider : IBridgeObstacleProvider
    {
        // Same 10 m cell as TreeObstacleProvider, and for the same reason: insertion is
        // radius-aware (a box goes into every cell its bounding circle overlaps) and the query
        // gathers the 3×3 around p0, so cell size never narrows coverage. What it bounds is
        // step length — a step longer than ~CellSize can put p1 outside the gathered 3×3.
        private const float CellSize = 10f;

        private readonly BridgeBox[] _boxes;
        private readonly Dictionary<long, List<int>> _grid;

        private static bool _nullWarningLogged;

        private BridgeObstacleProvider(BridgeBox[] boxes)
        {
            _boxes = boxes;
            _grid  = new Dictionary<long, List<int>>();

            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                float cxw = b.CenterX.ToFloat();
                float czw = b.CenterZ.ToFloat();
                float r   = b.RadiusXZ.ToFloat();

                int cxMin = (int)System.Math.Floor((cxw - r) / CellSize);
                int cxMax = (int)System.Math.Floor((cxw + r) / CellSize);
                int czMin = (int)System.Math.Floor((czw - r) / CellSize);
                int czMax = (int)System.Math.Floor((czw + r) / CellSize);

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

            foreach (var bucket in _grid.Values)
                bucket.Sort();
        }

        /// <summary>Create a provider. Returns null (no bridges) if the list is null/empty.</summary>
        public static IBridgeObstacleProvider Create(List<BridgeBox> boxes)
        {
            if (boxes == null || boxes.Count == 0)
            {
                if (!_nullWarningLogged)
                {
                    _nullWarningLogged = true;
                    UnityEngine.Debug.Log("[BridgeObstacleProvider] No bridge parts loaded — bridge railing/pier collision disabled for this hole.");
                }
                return null;
            }
            return new BridgeObstacleProvider(boxes.ToArray());
        }

        /// <summary>
        /// Test segment p0→p1 against every candidate box; return the earliest hit.
        /// Unlike the tree provider there is no two-pass trunk/canopy split — a bridge part is
        /// solid geometry with one behaviour, so "earliest wins" is the whole rule.
        /// </summary>
        public bool TestSegment(fp3 p0, fp3 p1, out BridgeHit hit)
        {
            hit = default;

            int cx = (int)System.Math.Floor(p0.x.ToFloat() / CellSize);
            int cz = (int)System.Math.Floor(p0.z.ToFloat() / CellSize);

            List<int> candidates = GetCandidates(cx, cz);
            if (candidates == null) return false;

            bool found = false;
            fp bestFrac = fp.FromInt(2); // > 1 sentinel

            for (int ci = 0; ci < candidates.Count; ci++)
            {
                var box = _boxes[candidates[ci]];
                if (!TestBoxCrossing(p0, p1, box, out fp frac, out fp3 normal)) continue;
                if (frac >= bestFrac) continue;

                bestFrac = frac;
                hit = new BridgeHit
                {
                    Frac     = frac,
                    HitPos   = Lerp3(p0, p1, frac),
                    NormalXZ = normal,
                    Profile  = box.Profile,
                };
                found = true;
            }

            // A containment hit reports frac=0 and its own push-out position, not the lerp.
            if (found && hit.Frac == fp.Zero)
                hit.HitPos = p0;

            return found;
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
                            if (!result.Contains(bucket[i])) result.Add(bucket[i]);
                    }
                }
            }
            result?.Sort();
            return result;
        }

        /// <summary>
        /// Segment-vs-box in the box's own yaw-rotated frame: a 3-slab test on local X, local Z
        /// and world Y. Returns the earliest entry t in [0,1] and the outward WORLD XZ normal of
        /// the face crossed.
        ///
        /// CONTAINMENT GUARD — ported from <c>TreeObstacleProvider.TestTrunkCrossing</c>, whose
        /// absence cost a red-team iteration there. When p0 is ALREADY inside the box, the slab
        /// test yields tEnter &lt; 0 and would report a miss; a ball rolling in Q16.16 micro-steps
        /// can then walk clean through a railing without any single step detecting a face. So an
        /// interior p0 returns frac=0 with a push-out normal along the shallowest-penetration
        /// XZ axis. Y is excluded from the shallowest-axis choice on purpose: BridgeHit carries
        /// an XZ-only normal (like TreeHit), and a railing must push the ball sideways, never up.
        /// </summary>
        private static bool TestBoxCrossing(fp3 p0, fp3 p1, BridgeBox box,
                                            out fp frac, out fp3 normal)
        {
            frac   = fp.Zero;
            normal = fp3.Zero;

            box.ToLocalXZ(p0.x, p0.z, out fp ax, out fp az);
            box.ToLocalXZ(p1.x, p1.z, out fp bx, out fp bz);
            fp ay = p0.y, by = p1.y;

            // ── Containment guard ───────────────────────────────────────────────────
            bool insideX = ax > -box.HalfX && ax < box.HalfX;
            bool insideZ = az > -box.HalfZ && az < box.HalfZ;
            bool insideY = ay > box.BaseY  && ay < box.TopY;
            if (insideX && insideZ && insideY)
            {
                fp penXPos = box.HalfX - ax;   // distance to the +X face
                fp penXNeg = ax + box.HalfX;   // distance to the −X face
                fp penZPos = box.HalfZ - az;
                fp penZNeg = az + box.HalfZ;

                fp best = penXPos; fp lnx = fp.One,  lnz = fp.Zero;
                if (penXNeg < best) { best = penXNeg; lnx = -fp.One; lnz = fp.Zero; }
                if (penZPos < best) { best = penZPos; lnx = fp.Zero; lnz = fp.One;  }
                if (penZNeg < best) { best = penZNeg; lnx = fp.Zero; lnz = -fp.One; }

                box.ToWorldDirXZ(lnx, lnz, out fp wnx, out fp wnz);
                normal = new fp3(wnx, fp.Zero, wnz);
                frac   = fp.Zero;
                return true;
            }

            // ── 3-slab test ─────────────────────────────────────────────────────────
            fp tEnter = fp.Zero;
            fp tExit  = fp.One;
            int enterAxis = -1;      // 0 = local X, 1 = local Z, 2 = Y
            fp  enterSign = fp.One;

            if (!Slab(ax, bx, -box.HalfX, box.HalfX, 0, ref tEnter, ref tExit, ref enterAxis, ref enterSign)) return false;
            if (!Slab(az, bz, -box.HalfZ, box.HalfZ, 1, ref tEnter, ref tExit, ref enterAxis, ref enterSign)) return false;
            if (!Slab(ay, by, box.BaseY,  box.TopY,  2, ref tEnter, ref tExit, ref enterAxis, ref enterSign)) return false;

            if (tEnter < fp.Zero || tEnter > fp.One) return false;

            // A hit whose entry face is the top or bottom of the box has no XZ normal to
            // reflect about. The DECK is not represented here (the baker excludes deck-straddling
            // boxes — that surface is the Stage-B zone mesh and the ground solver owns it), so a
            // Y-face entry means the ball dropped onto a pier cap or clipped a railing rail from
            // above: reported as a miss so the ground/zone path keeps ownership of vertical
            // resolution, exactly as the tree trunk test ignores over-the-top passes.
            if (enterAxis == 2) return false;

            fp lnx2 = enterAxis == 0 ? enterSign : fp.Zero;
            fp lnz2 = enterAxis == 1 ? enterSign : fp.Zero;
            box.ToWorldDirXZ(lnx2, lnz2, out fp nx, out fp nz);
            normal = new fp3(nx, fp.Zero, nz);
            frac   = tEnter;
            return true;
        }

        /// <summary>
        /// One slab of the ray-vs-AABB test. Narrows [tEnter, tExit] and records which axis and
        /// which side produced the latest entry, so the caller can name the struck face.
        /// </summary>
        private static bool Slab(fp a, fp b, fp min, fp max, int axis,
                                 ref fp tEnter, ref fp tExit, ref int enterAxis, ref fp enterSign)
        {
            fp d = b - a;
            if (d > -fp.Epsilon && d < fp.Epsilon)
            {
                // Parallel to this slab: a miss unless the segment already lies within it.
                return a >= min && a <= max;
            }

            fp t1 = (min - a) / d;
            fp t2 = (max - a) / d;
            fp sign1 = -fp.One;   // crossing the min face → outward normal is −axis
            fp sign2 =  fp.One;
            if (t1 > t2)
            {
                fp tt = t1; t1 = t2; t2 = tt;
                fp ts = sign1; sign1 = sign2; sign2 = ts;
            }

            if (t1 > tEnter) { tEnter = t1; enterAxis = axis; enterSign = sign1; }
            if (t2 < tExit)  { tExit  = t2; }
            return tEnter <= tExit;
        }

        private static fp3 Lerp3(fp3 a, fp3 b, fp t)
            => new fp3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
    }
}
