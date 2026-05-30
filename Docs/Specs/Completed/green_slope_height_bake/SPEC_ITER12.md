# SPEC ITER-12 — Boundary-height fix: bilinear sampling + 1-cell mask dilation

**Authored:** 2026-05-29 17:12 CEST / 2026-05-30 00:12 JST (Architect)
**Status:** SPEC_READY
**Kickoff:** `Use the golfin-implementer subagent on "green_slope_height_bake" (iter-12)`
**Scope:** Two coupled fixes — one in the bake, one in the importer's height sampling. Targets the verified root cause from iter-11.

---

## Root cause (verified independently, both by orchestrator instrumentation and by architect probe of `green.json` on disk)

Orchestrator instrumentation:
- Seam height (Y) high-freq, order-independent: **mean 12.53 cm, max 47.21 cm, 62% sign-flips** across the 170 contour vertices.
- Smooth XZ outline (Taubin worked), uniform 0.5 m segments, normals smooth, collar band width uniform.

Architect probe (`Assets/Resources/HoleData/Hole_07/green.json`):
- **85 of 170** contour vertices' nearest-cell sample lands on a **zero cell** (height = 0).
- The remaining 85 land on **non-zero cells** (real baked height ~12–17 cm).
- Sample sequence: `[0, 0, 0, 0, 0, 0, 0, 0, 0.166, 0, 0, 0, 0, 0.134, 0.127, 0, 0, 0.117, 0, 0, ...]` — clearly alternating between "outside cell" and "inside cell" along the contour walk.

**Mechanism (more specific than the orchestrator's predicted "outside coverage"):**
The bake fills height inside the contour. The grid is **0.5 m axis-aligned**. The contour is **0.5 m arc-length** spacing. Contour vertices are placed continuously along the polygon edge; their nearest grid cell is a discrete lookup. Where the contour runs **diagonally** past the green's perimeter cells, consecutive vertices land alternately on the inside cell and outside cell of the diagonal staircase — **like a Bresenham line stepping**. Inside-cell vertices get real height (~12–17 cm), outside-cell vertices get 0. That alternation across 170 vertices is the 12.5 cm mean / max 47 cm zig-zag, and the 62% sign-flips matches the diagonal step rate.

**This is why iter-9's contour smoothing and iter-10's terrain coupling were both wrong fixes:** the contour outline IS smooth, terrain is irrelevant. The defect is purely in **how the boundary vertex samples a discrete grid at its continuous XZ position**. Nearest-cell discretization causes the alternation; the height grid not extending past the contour magnifies it.

## Two coupled fixes

Neither alone is sufficient. Bilinear without mask dilation: stencil straddles outside cells (still pulls toward zero, just less). Mask dilation without bilinear: discrete cell lookup still alternates with reduced amplitude. Both together: continuous sampling on a continuously-valued field across the boundary, smooth everywhere.

### Fix 1 — `bake-green.mjs`: dilate the in-polygon height mask by 1 cell, outward

Where the bake currently writes height into a cell `(ix, iz)` only when `cellCenter ∈ polygon`, additionally write into cells where the **cell-rectangle** overlaps the polygon (cell intersects the contour, not just contains the centroid). The simplest correct test is `IsInsideContour(cellCenter)` **OR** `min distance from cellCenter to contour ≤ cellSize/√2` (half the cell diagonal — guarantees that any cell touching the contour at all is filled).

The height value written to a boundary-band cell (outside-but-overlapping) should be the **same value the boundary itself would have**: extrapolate by **nearest-interior-cell** (or 1-step IDW from the 3–8 neighboring in-polygon cells). This keeps the boundary band smoothly continuous with the interior — no cliff at the polygon edge inside the height field.

Result: the height grid now has valid, smooth height in a 1-cell-wide ring **outside** the contour. Any bilinear stencil centered on or near the contour boundary lands on 4 valid cells.

Hard constraints:
- Grid dimensions (`gridWidth`, `gridHeight`) and bounds (`boundsMin/Max`) **DO NOT CHANGE**. The bake already has a +0.5 m AABB pad — there is room. Confirm by adding an assert in `bake-green.mjs`: after dilation, every contour vertex's enclosing 2×2 stencil has all 4 cells non-zero.
- The slope grid is **NOT changed**. Only the height grid mask is dilated. Slope sampling uses `TrySampleSlope` outside this fix's scope; runtime physics is unaffected.
- `heightShiftMode` stays `"min"`. Min-shift is computed on the original interior cells, not the dilated band (otherwise the dilated band would shift the min). After dilation, the dilated cells' values may be ≥ 0; that's fine.

### Fix 2 — `GreenTopology.cs` + importer height sampling: bilinear interpolation

`GreenTopology.TrySampleHeight(Vector2 worldXZ, out float relHeightM)` is currently nearest-cell. Add a sibling method:

```csharp
public bool TrySampleHeightBilinear(Vector2 worldXZ, out float relHeightM)
```

- Compute `fx = (worldXZ.x − boundsMin.x) / cellSize`, `fz = (worldXZ.z − boundsMin.z) / cellSize`.
- `ix0 = floor(fx)`, `iz0 = floor(fz)`. If any of `(ix0, iz0)`, `(ix0+1, iz0)`, `(ix0, iz0+1)`, `(ix0+1, iz0+1)` lies outside the grid (`< 0` or `≥ gridWidth/Height`), fail (return false, `relHeightM = 0`) — same fail-edge contract as `TrySampleHeight`.
- `tx = fx − ix0`, `tz = fz − iz0`.
- Bilinear blend the 4 corner heights with weights `(1−tx)(1−tz)`, `tx(1−tz)`, `(1−tx)tz`, `tx tz`.
- Return true with the blended value.

In `HoleGeoImporter.cs`, the height-baked path (`CreateGreenMeshCDT` and wherever the mesh-build calls `TrySampleHeight` for the green/collar boundary vertices) replaces every `TrySampleHeight` call **for the green↔collar boundary ring** with `TrySampleHeightBilinear`. Interior vertices may keep `TrySampleHeight` (they're far from the discretization edge and the difference is sub-mm), or also switch to bilinear for consistency — implementer's call. *Document which was chosen* in the report.

Keep `TrySampleHeight` (nearest) alive — `BakedHeightProvider` and any runtime callers may rely on it. Adding bilinear is additive; not replacing.

## What this does NOT touch

- `contourResampled`, Taubin smoothing, polygon offsetting (`DilateContour` or Minkowski). All clean per iter-11.
- Collar outer-ring Y (`terrain.SampleHeight − GreenSkirtDepth`). The terrain skirt is fine.
- Slope grid, slope sampling, putt physics, `BakedHeightProvider`. The bug is in the height domain only.
- Schema version. The grid byte layout is unchanged; values within boundary-band cells change. v2 stays v2. (If the implementer feels strongly about versioning the dilated-mask format, that's a separate conversation — but consumers that ignore boundary-band cells continue to work identically.)

## Verification — two-step, decisive

### Step 1 (architect-replicable, before in-engine reimport)

Implementer adds a Node script `Tools/GreenSlope/scripts/verify-boundary-coverage.mjs` (or extends `bake-green.mjs --verify`) that, for each of the 18 holes, reports:

```
H07: 170 contour verts
  zero-cell hits (nearest):          0 / 170  ✓ (was 85)
  4-stencil all-non-zero (bilinear): 170 / 170  ✓
  seam height max |delta|:           0.83 cm (was 47.21 cm)
  seam height mean |delta|:          0.21 cm (was 12.53 cm)
```

Where "seam height delta" = `|h[i] − mean(h[i−1], h[i+1])|` along the contour, measuring the segment-frequency zig-zag that defined the bug. Drop below ~1 cm max = success.

This script reads `green.json` directly. **All 18 holes must pass** before the spec is considered done at the bake level.

### Step 2 (in-engine)

Reimport H07. The green↔collar boundary bead should be gone from any orbit angle. Re-run the iter-11 diagnostic harness with the production code (Variant A with terrain stubbed) — should now be clean across all 4 orbit frames. Cesar's eyeball sign-off on H07 first, then `--all` and reimport all 18.

The other three issues from `ARCHITECT_ESCALATION.md` (raised green ring, off-center raise, fairway breaking around green) are still on the queue, scoped to separate iters. iter-12 stops here.

## Files touched

- `Tools/GreenSlope/scripts/bake-green.mjs` — mask dilation in height-write step.
- `Tools/GreenSlope/scripts/verify-boundary-coverage.mjs` — new, the verification script.
- `Assets/Scripts/Course/Runtime/GreenTopology.cs` — add `TrySampleHeightBilinear`.
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — replace `TrySampleHeight` with `TrySampleHeightBilinear` at the green-boundary ring sample sites.
- All 18 `Assets/Resources/HoleData/Hole_NN/green.json` files regenerated by the rebake.

## Hard rules

1. Schema v2 layout unchanged. Grid dimensions and bounds unchanged. Only the values written into boundary-band cells change.
2. Slope grid untouched. Only height grid is dilated. Putt physics unaffected.
3. Nearest-cell `TrySampleHeight` stays — bilinear is additive.
4. No changes to `DilateContour`, contour resampling, Taubin smoothing, terrain skirt, fairway cut. Those are all clean.
5. The bake's existing `bake_report.txt` per hole gains the boundary-coverage stats above.

## Definition of done

- `bake-green.mjs --all` produces 18 `green.json` files where, for each: 0/N contour verts land on zero cells, all 170 (or N) bilinear stencils have all-non-zero corners, seam height max delta < 1 cm.
- `verify-boundary-coverage.mjs` runs clean on all 18.
- Reimport H07: green↔collar bead visually gone from all four orbit angles in the iter-11 diagnostic harness (run Variant A with the updated production code, capture videos).
- Cesar in-engine sign-off on H07 against the boundary bead.
- `--all` reimport: spot-check that no hole regressed; no scalloping on any boundary in any orbit shot.

## Open items the implementer should report back on

1. Whether interior vertices also switched to bilinear (or stayed nearest). Either is acceptable; just document.
2. Whether the dilated-band fill used nearest-interior-cell or 1-step IDW. Either acceptable; document the chosen approach and the visual outcome.
3. If any of the 18 holes fails the verify script after Fix 1 — meaning some contour vert still falls in a zero cell — that hole likely has a polygon segment that exits the AABB pad. Flag the hole number(s); the +0.5 m pad may need to grow.
4. If the iter-11 diagnostic harness now shows Variant A clean across all 4 orbit angles, the harness can be marked obsolete (or kept as a regression check tool). Either way, document the call.
