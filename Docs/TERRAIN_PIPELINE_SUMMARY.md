# Terrain Pipeline Summary — Ravine Carving Investigation
*April 2026 — for architect review*

## Problem Statement

Hole 7 had a strong diagonal artifact in the generated heightmap. The ravine that crosses the hole diagonally was appearing as a straight band perpendicular to the tee→green axis, instead of following its real geographic path.

---

## Root Cause 1 — Spline+Quadratic Surface (generate-terrain.mjs)

The terrain generator built a **synthetic surface** via:
1. A monotone cubic spline along the tee→green axis
2. A quadratic residual in the cross-axis direction

The tee→green axis on Hole 7 is diagonal. When the spline was sampled, each point along the axis got projected **perpendicular to the diagonal direction**. The ravine crosses that axis at one specific position — so every cell that projects to that axis position inherited the ravine's depth, creating a false diagonal band spanning the entire map.

**Fix:** Replace the entire synthetic surface with `blur(rawDem, sigma=10m)`. A Gaussian blur of the real DEM has no axis, no projection, no diagonal artifacts.

---

## Root Cause 2 — Boundary Height Propagation (HoleGeoImporter.cs)

After terrain generation, the Unity importer had a post-process block:
1. Build an `isPlayArea` mask from the zone grid
2. Chamfer distance transform to find nearest play-area cell for each OB cell
3. Propagate the play-area boundary height into OB via a forward+backward pass
4. Smoothstep blend OB cells from that `boundaryHeight` over `TransitionCells=80`

Where two propagation fronts from different boundary cells met, they carried **different heights**. This created Voronoi-style ridges — visible as "seesaw" patterns at play-area/OB zone boundaries.

**Fix:** Disable the entire block (`if (false && loadedRaw)`). The smoothed DEM approach produces a naturally continuous surface across the entire heightmap — no post-processing is needed.

---

## New Pipeline

```
rawDem (5m GSI DEM5A tiles, NaN-filled via neighbor propagation)
    ↓
blur(rawDem, sigma=10m)         ← separable Gaussian
    ↓
heightmap[0..1] (remapped to elev±25m range)
    ↓
.raw uint16 file (2049×2049)
    ↓
Unity: apply directly to TerrainData (no boundary post-processing)
```

### Why 10m sigma?
At 10m sigma, the blur is gentle enough to preserve large terrain features (ravines 100m+ wide) while eliminating DEM noise and tile boundary artifacts. Tested across all 18 holes — all produce smooth, geographically faithful shapes.

---

## Ravine Carving (DISABLED, not needed)

A ravine detection+carving system was prototyped but left disabled:

```javascript
if (false && carvedRegions.length > 0) { // carve disabled — smoothed DEM is sufficient
```

**How it worked:**
1. High-pass filter: `ravineResidual = rawDem - blur(rawDem, 15m)` — isolates sharp local features
2. Threshold + flood-fill connected components below −2m residual
3. For qualifying regions (area > 50 cells), apply Gaussian carve

**Why it's disabled:** With the smoothed DEM, ravines at real geographic scale are already preserved at sufficient depth. The carve is only necessary if ravines were artificially flattened — they weren't.

**The high-pass approach is correct:** Using `rawDem - blur(rawDem, 15m)` instead of the earlier `rawDem - heightmap` ensures ravine detection is independent of any synthetic surface, making it usable even if a synthetic component is reintroduced later.

---

## Files Changed

| File | Change |
|---|---|
| `Tools/UHoleGeo/scripts/generate-terrain.mjs` | Replaced spline+quadratic with `blur(rawDem, 10m)`. Ravine carve left in but disabled. |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` | Disabled boundary-height propagation block. Added Play Mode stop at import entry. |
| `Assets/Scripts/Editor/CourseImporter/TreePlacerWindow.cs` | Deselect all trees before loading a preset. |

---

## Tunable Constants (generate-terrain.mjs)

```javascript
const TERRAIN_SMOOTH_SIGMA_M = 10.0;   // DEM blur sigma in meters
const RAVINE_DETECT_SIGMA_M = 15.0;    // high-pass sigma for ravine detection
const RAVINE_THRESHOLD_M = -2.0;       // residual depth to classify as ravine cell
const RAVINE_MIN_AREA_CELLS = 50;      // minimum connected component size
```

---

## Test Results (All 18 holes, smoothed DEM)

All holes pass with `DEM5A` source. Sigma-in-cells varies by hole size:
- Small holes (Hole 4, 6, 15): ~50–147 cells — blur is strong but terrain features are preserved
- Large holes (Hole 13, 18): ~45–172 cells — large features intact

Hole 7 specifically: `sigma=69.8 cells, radius=210` — ravine depth confirmed sufficient without carve.
