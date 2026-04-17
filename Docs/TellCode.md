# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Replace Chamfer Distance with Exact Polygon-Edge Distance for Tee Skirt

Thank you for the detailed debug notes — they nailed the diagnosis.

**The banding is not 1-cell chamfer "Voronoi spokes." It's ~1–2m wide
spokes radiating from each polygon EDGE** (not each cell). Each tee
contour has ~56 vertices spaced ~1.5m apart. The chamfer distance
transform measures Manhattan/diagonal distance from the nearest
rasterized teeMask cell — and the rasterization turns each ~1.5m
polygon edge into a row of ~13 cells. Chamfer from that rasterized
edge produces a "plateau" of equal distances in front of the edge, and
a step up where it transitions to the next edge's plateau. Those
plateaus → those stripes.

**Blurring can't fix this.** A 5-cell Gaussian averaging a ~13-cell
plateau doesn't flatten the plateau — it just slightly softens the
transition between plateaus. Confirmed by your three experiments.

**The proven fix is to replace chamfer with exact perpendicular
distance to the nearest polygon edge.** We already solved the exact
same problem for the water shore ramp on 2026-04-17 — see
`HoleGeoImporter.cs` lines 3453–3541 for the working pattern. This
task ports that pattern into `FlattenTerrainUnderTees`.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**Scope:** Replace the per-region chamfer+lerp inside
`FlattenTerrainUnderTees` (lines ~3153–3210) with a coarse-chamfer-cull
+ exact-polygon-edge-distance pass. Remove any Gaussian blur attempts.
**No changes to:** `CreateTeeMeshFlat`, `DepressTerrainUnderOverlays`,
water/bunker/green/fairway code, mesh building, skip mask logic,
baseline clone, or the platform-raise step.

---

### Reference: the water shore pattern

Study lines 3453–3541 of `HoleGeoImporter.cs` before editing. The
relevant shape is:

```csharp
// 1. Compute cell size in world metres
float cellW = terrainSize.x / (hRes - 1);
float cellH = terrainSize.z / (hRes - 1);
float cellSize = (cellW + cellH) * 0.5f;

// 2. Coarse 4-neighbour chamfer just for culling (not used as final distance)
float[,] coarseDist = new float[hRes, hRes];
// ... populate: 0 inside the mask, MaxValue outside, forward+backward pass
//     with only +1.0f neighbours (axial only, no diagonals needed for culling)

// 3. For each cell with coarseDist <= radius+2 (coarse cull margin):
//      - compute world position
//      - iterate all polygon edges, compute exact perpendicular distance
//        to each edge segment, keep minimum
//      - if minDistM > radiusM: fine cull
//      - else: smoothstep lerp from source to target using minDistM/radiusM
```

This is what we'll mirror, adapted for tees (per-region loop, raise
instead of drop, skipMask instead of depress/cartDepress).

---

### The change

Inside `FlattenTerrainUnderTees`, **replace everything from the chamfer
distance block through the end of the smoothstep lerp loop** (currently
roughly lines 3153–3210) with a new coarse-cull + exact-distance
version. Keep the platform raise (lines ~3127–3151) and the Debug.Log
(lines ~3212–3214) untouched.

Here is the replacement block. It starts where the old
`// Chamfer distance transform outward from the tee boundary.` comment
begins and ends where the old lerp `}` closes (right before
`changed = true;`).

```csharp
// ─── Outward skirt ramp: exact polygon-edge distance ───────────
// We replace the chamfer distance transform with true perpendicular
// distance to the tee contour edges. Chamfer turns each ~1.5m
// contour edge into a row of rasterized cells all at the same
// distance, creating visible ~1-2m-wide radial "plateau spokes" on
// the slope. Exact distance has no such quantization — d is a
// continuous function of world position, so smoothstep produces a
// clean gradient.
//
// For performance we first run a cheap 4-neighbour chamfer as a
// coarse cull (so we only compute exact distance for cells near the
// tee boundary), then compute exact distance only in that ring.
//
// Same pattern as the water shore ramp (HoleGeoImporter.cs ~line 3453).

// Cell size in world metres.
float cellW = terrainSize.x / (hRes - 1);
float cellH = terrainSize.z / (hRes - 1);
float cellSize = (cellW + cellH) * 0.5f;
float skirtRadiusM = skirtRadiusCells * cellSize;

// Coarse 4-neighbour chamfer — axial only, no diagonals needed for
// culling. Cells in teeMask start at 0, others at MaxValue.
float[,] coarseDist = new float[hRes, hRes];
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
        coarseDist[z, x] = teeMask[z, x] ? 0f : float.MaxValue;
// Forward pass
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
    {
        if (x > 0 && coarseDist[z, x - 1] + 1f < coarseDist[z, x])
            coarseDist[z, x] = coarseDist[z, x - 1] + 1f;
        if (z > 0 && coarseDist[z - 1, x] + 1f < coarseDist[z, x])
            coarseDist[z, x] = coarseDist[z - 1, x] + 1f;
    }
// Backward pass
for (int z = hRes - 1; z >= 0; z--)
    for (int x = hRes - 1; x >= 0; x--)
    {
        if (x < hRes - 1 && coarseDist[z, x + 1] + 1f < coarseDist[z, x])
            coarseDist[z, x] = coarseDist[z, x + 1] + 1f;
        if (z < hRes - 1 && coarseDist[z + 1, x] + 1f < coarseDist[z, x])
            coarseDist[z, x] = coarseDist[z + 1, x] + 1f;
    }

// Exact-distance pass over cells that passed coarse cull.
// minDistM is the shortest perpendicular distance from the cell's world
// position to any edge segment of this tee's contour.
int nContour = region.contour.Length;
for (int z = 0; z < hRes; z++)
{
    for (int x = 0; x < hRes; x++)
    {
        if (teeMask[z, x]) continue;          // interior already raised
        if (skipMask[z, x]) continue;         // fairway/green preserved
        if (coarseDist[z, x] > skirtRadiusCells + 2f) continue; // coarse cull

        // World position of this heightmap cell.
        float wx = terrainPos.x + x * cellW;
        float wz = terrainPos.z + z * cellH;

        // Exact perpendicular distance to the nearest contour edge.
        float minDistM = float.MaxValue;
        for (int i = 0; i < nContour; i++)
        {
            int j = (i + 1) % nContour;
            float ax = region.contour[i].x, az = region.contour[i].z;
            float bx = region.contour[j].x, bz = region.contour[j].z;
            float edx = bx - ax, edz = bz - az;
            float len2 = edx * edx + edz * edz;
            float t2 = len2 > 1e-10f
                ? Mathf.Clamp01(((wx - ax) * edx + (wz - az) * edz) / len2)
                : 0f;
            float px = ax + t2 * edx - wx;
            float pz = az + t2 * edz - wz;
            float d = Mathf.Sqrt(px * px + pz * pz);
            if (d < minDistM) minDistM = d;
        }

        if (minDistM > skirtRadiusM) continue; // fine cull

        // t = 0 at boundary → height = maxH (top of mound)
        // t = 1 at skirtRadiusM → height = baseline (natural terrain)
        float t = minDistM / skirtRadiusM;
        t = t * t * (3f - 2f * t); // smoothstep

        float rampedH = Mathf.Lerp(maxH, baseline[z, x], t);

        // MAX so overlapping adjacent-tee skirts don't pull a cell
        // below a neighbour's ramp (same invariant as before).
        if (rampedH > heights[z, x])
        {
            heights[z, x] = rampedH;
            skirtedCount++;
        }
    }
}
```

Everything else in the function stays as-is. The `teeMask`
construction, platform raise, `baseline` and `skipMask` usage,
per-region loop, and Debug.Log at the bottom don't change.

---

### Important: remove any leftover blur code

If the current code has a `Gaussian blur on heights[]` block at the
bottom of the per-region loop (from Attempt 3 in the debug notes), or
a `dist[] blur` block between the chamfer and the lerp (from
Attempts 1–2), **delete those blocks**. They're not doing anything
useful and will only add cost. The exact-distance pass does not need
any post-processing.

Grep for `blurSigma` or `blurRadius` in `FlattenTerrainUnderTees` to
make sure nothing survives.

---

### Why this works where blur didn't

- **Blur averages neighbouring distance values.** If neighbours all
  share the same plateau value (because they all project onto the same
  rasterized edge-cell), the average is just that plateau value. The
  stripes persist.
- **Exact distance is a continuous function of world position.** Cell
  (z, x) gets a distance that depends on (wx, wz) and the exact
  polygon-edge positions (not rasterized). Cell (z, x+1) gets a
  slightly different distance. No plateaus. No stripes.
- **Proven on water.** The same swap from chamfer to exact distance
  cured the shore ramp banding on 2026-04-17. The lesson note in
  `tasks/lessons.md` is explicit: "Replaced chamfer distance with
  exact polygon-edge distance for shore ramp; all blur attempts
  failed."

---

### Performance

- Coarse chamfer: 2 × (hRes × hRes) = ~8M ops per tee. Cheap.
- Exact distance: only for cells in the skirt ring. A tee with a 1000-
  cell perimeter and 2m skirt produces ~18 × 1000 = ~18000 skirt
  cells. Each tests ~56 edges. ~1M ops per tee.
- With 3 tees per hole: ~30M ops total per import. Well under a
  second on modern hardware.
- Already budgeted — the water shore pass does exactly this and it's
  not on anyone's performance radar.

---

### Verification

Re-import the hole from the last screenshot and the regression set:

- [ ] **Stripes gone.** Skirt mound is a smooth gradient from tee
      edge to surrounding terrain — no radial spokes, no plateaus,
      no banding.
- [ ] Tee top surface still flat (unchanged — raise block untouched).
- [ ] Tee edge still crisp (no border ring — unchanged).
- [ ] Mound gradient still feels right — same 2m ramp width, same
      smoothstep curve, just without the quantization.

Regression:

- [ ] Hole 4 — big tee + small forward tee both smooth.
- [ ] Hole 1 — 3 tees, smooth on all of them.
- [ ] Hole 18 — 6 small tees, smooth on all of them.
- [ ] Hole 7 — water-adjacent tee, no regression on water shore.
- [ ] Fairway, green, water, bunker, cart path — unchanged (this
      task is tee-only).
- [ ] `Debug.Log` still reports `skirt cells: N` per hole with
      reasonable counts (should be similar to before, maybe slightly
      different due to fine cull using exact distance vs chamfer
      distance).

---

### Do NOT change

- `CreateTeeMeshFlat` — mesh is perfect.
- The platform raise (teeMask build + raise to maxH).
- `skipMask` construction (fairway + green polygons).
- `baseline` clone.
- `TeeSkirtMeters` (= 2.0f).
- The smoothstep curve or the MAX-over-existing merge.
- `DepressTerrainUnderOverlays` or the 0.40m drop under the mesh.
- Water, bunker, green, fairway, cart path behavior.
- Zone-contours schema, pipeline, or any upstream tool.

---

### Design note (for future me)

The order of fallback when you see banding in a distance-driven
height field:
1. **First, check edge spacing.** If polygon edges are ~N cells long
   each, chamfer distance will produce ~N-cell-wide plateau spokes.
   Any ramp narrower than a few times N cells will show these.
2. **Blur helps only for 1-cell Voronoi noise**, not N-cell plateau
   spokes.
3. **Exact perpendicular distance to polygon edges is the real fix.**
   Use a coarse chamfer as a cull to keep it cheap.
4. If performance were a real problem (it's not here), use a BVH /
   spatial hash of edges to accelerate the per-cell loop.

Adding this to `tasks/lessons.md` after the task completes:

> Chamfer distance stripes can come from TWO sources: 1-cell Voronoi
> spokes (blurable) and N-cell polygon-edge plateaus (NOT blurable,
> need exact distance). Blur-attempts that "do nothing" are the
> signature of the plateau form. Exact perpendicular distance to
> polygon edges is the general fix.

---

✅ DONE: 2026-04-18 — Replaced chamfer+blur block with coarse-chamfer-cull + exact polygon-edge distance. All blur blocks removed. Pattern mirrors water shore ramp (~line 3453).
