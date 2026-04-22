# Terrain Spline Profile Plan

## Goal

Replace the pure quadratic surface (safe baseline) with a **cubic
spline along the tee→green axis** that captures real elevation
terraces from DEM data, combined with the quadratic's cross-axis
terms for side-to-side slope. No DEM grid noise, no residual
blending, no zone masks.

## Current Safe Baseline (committed as `terrain-safe-baseline`)

- Quadratic surface fit to playable zone DEM samples
- Applied to ENTIRE heightmap (playable + non-playable identical)
- Smooth, no mesh breakage, no transition artifacts
- But: no terraces, no real elevation profile

## The Approach

### Step 1: Sample DEM at Key Points Along the Axis

Define the play axis as a straight line from tee centroid to green
centroid (extend to dogleg centerline later). Sample DEM elevation
at N evenly-spaced points along this axis (N = 8-10).

Each sample is a single `sampleDem5a(lat, lon)` call — one number.
No grid, no noise. The 5m DEM resolution means each sample represents
a ~5m area's average elevation, which is exactly what we want for
terrain-scale features.

### Step 2: Fit Cubic Spline Through Sample Points

Natural cubic spline through the N points. This produces a smooth
curve that passes through every sample point exactly. If the DEM
says "tee at 140m, mid-fairway at 132m, green at 135m," the spline
captures: high → drop → flat → rise. No grid staircase because
there are only N points, not millions.

### Step 3: Decompose Quadratic into Along-Axis and Cross-Axis

The existing quadratic `h = a*x² + b*y² + c*x*y + d*x + e*y + f`
captures both along-axis slope and cross-axis slope. We want to
REPLACE the along-axis component with the spline but KEEP the
cross-axis component.

Decomposition:
1. Define axis unit vector `A = normalize(green - tee)` in heightmap
   coords
2. Define cross vector `C = perpendicular(A)`
3. For any point `P`, project: `along = dot(P - tee, A)`,
   `cross = dot(P - tee, C)`
4. Quadratic along-axis: evaluate quadratic at `tee + along * A`
5. Quadratic cross-axis: `quadratic(P) - quadratic(tee + along * A)`
   = the cross-axis residual

Final height:
```
height(P) = spline(along) + quadratic_cross(P)
```

Where `quadratic_cross(P)` = full quadratic at P minus quadratic
projected onto the axis. This gives the spline's elevation profile
(terraces, drops) plus the quadratic's side-to-side curvature.

### Step 4: Clamp at Boundaries

For cells beyond tee (along < 0) or beyond green (along > 1),
clamp the spline parameter to 0 or 1. The cross-axis component
still varies naturally. This prevents spline extrapolation.

## What Happens to Existing Meshes

### CDT (fairway, tee) — SAFE ✅
CDT calls `terrain.SampleHeight()` per vertex AFTER the heightmap
is loaded. The spline produces a smooth surface → CDT vertices
follow it smoothly. No grid bumps → no mesh breakage.

Key: the spline has no discontinuities. A terrace is a steep but
SMOOTH section. The CDT mesh will slope steeply there, which is
correct — the fairway IS going downhill.

### Fringe Ring — SAFE ✅
Fringe ring samples terrain at the same points as the fairway
contour edge. Same smooth surface → same smooth heights. The 2mm
Y-offset (0.012 vs 0.01) keeps fringe above fairway. On a steep
spline section, both meshes slope equally — no eating.

### Cart Path Spine Strip — SAFE ✅
Spine strip samples terrain at left/right vertex pairs. Smooth
spline → smooth terrain under the strip. Depression margin (0.3m)
handles the slope.

### Green Raised Mesh — SAFE ✅
Green samples terrain height and adds `greenHeight`. The spline
elevation at the green centroid gives the correct base height.
The green is small enough that the spline is nearly flat across it.

### Bunker Bowls — SAFE ✅
Bunker rim samples terrain height. Bowl depth is relative to rim.
Smooth spline → smooth rim → correct bowl.

### Water — SAFE ✅
Water uses min terrain height across contour. Smooth spline →
consistent min height. Shore depression and bed depth unchanged.

### Depression System — SAFE ✅
Depression drops terrain 0.4m under overlay footprints. Works the
same regardless of the underlying surface shape. The spline doesn't
change the depression mechanism.

### Zone Boundaries — SAFE ✅
There ARE no zone boundaries in the terrain anymore. The spline +
quadratic_cross is applied to ALL cells identically. No per-zone
fractions, no transition ramps, no zone masks. The only zone-
specific behavior is in the Unity importer (overlay meshes, splatmap)
which is unchanged.

## Why This Works Where Residual Blending Failed

| Problem | Residual approach | Spline approach |
|---------|-------------------|-----------------|
| 5m grid noise | Blur to remove → insufficient or breaks zones | No grid — only 8-10 samples |
| Zone boundary artifacts | Per-zone fractions create discontinuities | No per-zone anything |
| Narrow zone distortion | Masked blur breaks on thin strips | No masks at all |
| Small hole scaling | Large blur kernel exceeds zone width | Spline adapts to any hole size |
| Mesh breakage | Bumpy terrain → bumpy CDT → terrain pokes through | Smooth terrain → smooth CDT |

## Dogleg Extension (Future)

For dogleg holes, replace the straight tee→green axis with the
fairway centerline (tee centroid → fairway centroid → green centroid).
Sample DEM along this curved path. Project cells onto nearest point
on the path. Same spline math, curved axis.

## Implementation Location

**File:** `Tools/UHoleGeo/scripts/generate-terrain.mjs`
**Function:** `generateTerrainDEM()`

Replace the section after "Apply the quadratic surface" (currently
the "SAFE MODE" block) with:

1. Compute tee→green axis in heightmap coords
2. Sample DEM at N points along axis
3. Fit cubic spline
4. For each cell: `height = spline(along) + quadratic_cross(x, y)`

The quadratic fitting stays (needed for cross-axis terms). The
spline replaces it as the along-axis elevation source.

## Tuning Parameters

- `N_SPLINE_POINTS = 10` — number of DEM samples along axis.
  More = captures finer features, but 5m DEM can't resolve features
  smaller than 5m anyway. 10 points on a 500m hole = 50m spacing.
- Spline tension: natural cubic spline (tension = 0) for smooth
  curves. Could use Catmull-Rom if overshooting is a problem.

## Verification

Test on Hole 4 (Par 3, 138yd, pronounced terraces):
- [ ] Tee area visibly higher than fairway
- [ ] Fairway at lower elevation (matching real photo)
- [ ] Green at its correct elevation
- [ ] Smooth transitions (no grid staircase)
- [ ] All overlay meshes intact (no terrain poking through)
- [ ] Cart paths following terrain slope
- [ ] Water/bunkers unaffected

Test on Hole 1 (Par 5, 531yd, long with water):
- [ ] Gentle slope tee to green (531yd = subtle elevation change)
- [ ] No artifacts at water zone
- [ ] All 18 holes generate without errors
