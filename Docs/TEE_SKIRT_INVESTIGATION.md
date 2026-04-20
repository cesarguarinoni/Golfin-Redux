# Tee Skirt Cliff — Investigation Findings for Architect

**Date:** 2026-04-20  
**Status:** Unresolved. Repo is at `a4ce180e` (pre-session baseline).

---

## What Was Attempted This Session

### Attempt 1 — Unit conversion in `FlattenTerrainUnderTees` (architect spec)

Applied `drop = (maxH - baseline[z, x]) * terrainSize.y` to convert
normalized → world metres before dividing by `TeeMaxRampSlope`.

**Result:** Mounds gone, tee areas appeared "under a cliff."  
**Why:** The unit fix produced a ~30m skirt radius for Hole 7 Tee 5
(7m drop / 0.35 slope × 1.5 = 30m). The 30m skirt raises ALL terrain
within 30m of the tee to near-`maxH`, erasing the natural hill contour
and creating a raised plateau. Cart path depression then cut into this
plateau, producing the cliff walls visible in the screenshot.

---

### Attempt 2 — Mesh-based skirt ring (submesh 2 on tee mesh)

Removed `FlattenTerrainUnderTees` call entirely. Added a quad-strip
skirt ring as submesh 2 of `CreateTeeMeshWithInsetBorder`:
- Inner edge: `platformY` (flat, matches tee surface)
- Outer edge: `terrain.SampleHeight()` at 6m beyond the contour

Motivated by `safe-baseline-quadratic-terrain` tag (no
`FlattenTerrainUnderTees`, no cliff) and `LESSONS_FRINGE_BORDER_MESHES.md`
(mesh submesh = correct approach for terrain-mesh boundary transitions).

**Result:** Disaster. Screenshot showed concentric rings covering the
entire hole and jagged tears in the tee area.  
**Why (analysis):**

1. **Winding / flipping failure.** The winding-check heuristic uses the
   first triangle of the ring, which may be nearly degenerate (zero-area
   on a nearly-flat outer edge). One bad flip reverses ALL skirt quads,
   producing inside-out geometry visible as the concentric ring artifacts.

2. **6m is too narrow** for a 7m hillside drop. At 6m the outer edge
   is already several metres below `platformY`, so the ring's inner-to-
   outer height difference is enormous. The resulting mesh is nearly
   vertical, not a gentle ramp. The terrain below is not modified — the
   ring floats above it on the downhill side, exposing the underside.

3. **`DilateContour` on a non-convex tee contour** may produce
   self-intersecting polygons at sharp concave corners. Corresponding
   vertex pairing (inner[i] ↔ outer[i]) breaks where the dilated contour
   self-intersects, producing degenerate quads and the tears visible in
   the screenshot.

4. **No material for the skirt.** Reusing `mat` (tee surface material)
   on submesh 2 extends the bright tee green out 6m — the concentric
   circles in the screenshot are exactly the 6m tee skirt rings drawn
   over the whole hole.

---

## What the Safe Baseline Actually Did

`safe-baseline-quadratic-terrain` (tag `12fadcd6`, 2026-04-15):
- **No `FlattenTerrainUnderTees`** in `ImportHoleInternal`.
- Tee contours go into the **same `depress` mask as fairways** (0.4m
  flat depression), not the separate `teeDepress` (0.05m).
- The tee mesh function called is NOT `CreateTeeMeshWithInsetBorder`
  (which flattens all verts to `platformY`). The baseline used a simpler
  mesh that **conforms to terrain** — it does NOT call `platformY`
  flattening.

**Why this worked:** On the smooth quadratic terrain of the baseline,
tees sit on a gently curved surface. The mesh conforms to that surface
— slightly non-flat, but no visible cliff because there's no hard
`maxH` cutoff anywhere.

**Why this approach fails now:** The current tee mesh
`CreateTeeMeshWithInsetBorder` unconditionally flattens all verts to
`platformY = max(sampled)`. On a steep hillside, `maxH` is at the
uphill corner of the tee. Without terrain modification, the downhill
edge of the flat mesh is metres above the terrain → floating tee mesh
with a visible gap underneath.

---

## Root Cause (Structural)

`CreateTeeMeshWithInsetBorder` was designed in tandem with
`FlattenTerrainUnderTees`. They form a coupled system:

1. `FlattenTerrainUnderTees` raises terrain inside the tee to `maxH`
   and applies a skirt ramp outside.
2. `CreateTeeMeshWithInsetBorder` flattens the mesh to `maxH` —
   relying on terrain already being flat underneath.

Neither can be removed without breaking the other. The 2m skirt is
fine on flat/gentle terrain; it fails on steep hillside tees because
2m at 0.35 slope only handles 0.7m of height drop, not the 7m on Hole 7.

The architect's unit fix is mathematically correct — the skirt radius
formula DOES produce the right number (30m). But 30m is too wide to
look natural: it erases the hill topography.

---

## What the Architect Needs to Decide

### Option A — Per-cell adaptive radius (not per-tee worst case)

Currently `worstAdaptiveM` is the worst drop anywhere in the 60m
neighborhood. This means the gentle uphill sides of a tee also get a
30m skirt (the same as the steep downhill side). This erases the hill.

A per-cell radius (`adaptiveM` varies by distance to nearest contour
edge and local drop at that cell) would give:
- Uphill side (small drop): 2m skirt
- Downhill side (7m drop): 30m skirt

**Known issue (from existing code comments):** Per-cell radius was
previously tried and caused "sawtooth teeth at the bottom of the mound"
because the outer boundary of the skirt becomes irregular. The comment
says this is why uniform radius was chosen. Architect needs to re-
evaluate whether the teeth could be mitigated (e.g. with a blur pass or
distance-based smoothing).

### Option B — Redesign the tee mesh to not flatten to `platformY`

Remove the `platformY` flattening from `CreateTeeMeshWithInsetBorder`.
Let the tee surface conform to the (FlattenTerrainUnderTees-modified)
terrain exactly, with no mesh-level flattening. Then `FlattenTerrainUnderTees`
with a modest fixed skirt (4–6m) handles all the work. The tee surface
won't be perfectly flat on a steep hillside, but neither is a real tee
on a hillside course.

### Option C — Cap skirt by slope at each contour vertex

Instead of a per-tee worst-case radius, compute the radius PER CONTOUR
EDGE SEGMENT (not per cell): for each segment of the tee polygon,
measure the drop normal to that edge at 1m increments, stop extending
when the drop fits within `TeeMaxRampSlope`. Apply different radii to
different sides of the tee. The outer boundary of the skirt would vary
by side — downhill side gets 30m, uphill/flat sides get 2m — but the
boundary between skirt zones would follow contour edges, which are
straight lines, so no sawtooth.

### Option D — Two-pass: thin terrain skirt + mesh-based outer blend

Keep `FlattenTerrainUnderTees` with a small fixed skirt (4–6m) so the
tee mesh always has terrain flush underneath. For the remaining drop
beyond 6m, leave terrain unmodified. Accept that the 6m cliff (smaller
than before) is still present, but use a separate "mound blend mesh"
farther out — sampled from unmodified terrain. This separates the
"keep mesh flush" concern from the "smooth terrain to horizon" concern.

---

## Files / Constants for Reference

| Symbol | Value | Location |
|---|---|---|
| `TeeSkirtMeters` | 2.0m | `HoleGeoImporter.cs` ~line 54 |
| `TeeMaxRampSlope` | 0.35 | `HoleGeoImporter.cs` ~line 63 |
| `TeeMaxSkirtMeters` | 60.0m | `HoleGeoImporter.cs` ~line 67 |
| `FlattenTerrainUnderTees` | call at ~line 241 | `ImportHoleInternal` |
| `CreateTeeMeshWithInsetBorder` | ~line 4361 | platformY flatten ~line 4406 |
| Hole 7 Tee 5 log | `platform h=0.7616, worst adaptive skirt=2.0m` | pre-fix |
| Estimated drop | ~7m | Hole 7, Tee 5 downhill side |
| Estimated radius needed | ~30m | at `TeeMaxRampSlope=0.35` |
