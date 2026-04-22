# Cart Path Smoothing — Research & Findings

**Date:** 2026-04-13  
**Status:** Unsolved — 4 approaches attempted, none satisfactory  
**File:** `Tools/UHoleLite/app/app.js`, function `smoothCartPathMask()`

---

## The Problem

Hand-painted cart path strokes in UHole Lite have irregular, jaggy
edges from the brush tool. We need a "Smooth Cart Path" button that
replaces the painted mask with a clean, uniform-width (2.5m) path
that follows the same route.

The OB smoothing button works well (vectorize boundary → RDP →
Chaikin → rasterize). Cart paths are harder because we need to
extract a **centerline** from a filled region, not just smooth a
boundary.

---

## What Works

- **Smooth OB** — vectorize inverse mask boundary, RDP + Chaikin,
  rasterize back. Two passes gives clean results. ✅
- **Export pipeline** (`export-hole.mjs`) — successfully extracts
  cart path spines for Unity using: contour → farthest pair → split
  left/right → resample → average. These spines render beautifully
  in Unity as strip meshes. ✅
- Circle-brush stamping with Bresenham interpolation (no gaps). ✅
- Undo (Ctrl+Z) integration. ✅

---

## Approaches Tried

### v1 — Zhang-Suen Skeleton + RDP + Chaikin
**Result:** Connected chains but visibly zig-zaggy.

**Implementation:**
- Downsample mask to ~3px stroke width
- Zhang-Suen thinning → 1px skeleton
- Spur pruning to remove noise branches
- Chain tracing + junction merging → ordered polylines
- RDP (ε=20) + Chaikin (4 passes, open polyline)
- Circle-brush stamping at 2.5m width

**Why it failed:** Zhang-Suen is topologically correct but
geometrically noisy. The medial axis follows every bump and dip
on both edges of the hand-painted stroke. The noise frequency is
too high for RDP to fix — each zig is within ε of its neighbors,
but the cumulative effect is visibly jagged.

**Root cause:** Zhang-Suen follows pixel topology, not geometry.
Every source in the literature confirms it produces noisy output
for irregular shapes.

### v2 — Distance Transform Ridge
**Result:** Disconnected fragments, unusable.

**Implementation:**
- Chamfer distance transform on downsampled mask
- Ridge extraction (local maxima among 8 neighbors)
- Plateau handling (equal-to-max with lower neighbor)
- Zhang-Suen thinning on ridge to get 1px
- Same chain trace / smooth / stamp pipeline

**Why it failed:** The distance transform of a uniform-width stroke
produces a **plateau** (flat area of equal distance values), not a
sharp ridge. Local maximum extraction gets either nothing or
disconnected fragments because most ridge pixels have neighbors
with equal distance values.

**Root cause:** The approach works for tapered shapes but not for
roughly-uniform-width strokes like cart paths.

### v3 — Zhang-Suen + Gaussian Coordinate Blur
**Result:** Still poor quality, not smooth enough.

**Implementation:**
- Same Zhang-Suen pipeline as v1
- Replace RDP + Chaikin with Gaussian-weighted moving average on
  x,y coordinate arrays (window ≈ estWidth/dsF * 3, 2 passes)
- Light RDP (ε=3) for point count reduction

**Why it failed:** The Gaussian blur helped somewhat but the
underlying skeleton is so noisy that even heavy blurring leaves
visible wobble. The window size would need to be enormous to
fix it, which would distort intentional curves.

**Root cause:** Post-processing a fundamentally noisy signal can
only do so much. Need a cleaner signal.

### v4 — Contour-Split Centerline (Farthest Pair)
**Result:** Lost parts of the path, didn't follow original shape.

**Implementation:**
- Flood fill to find connected regions
- Trace outer contour (Moore neighborhood walk)
- Find two farthest contour points
- Split contour into left/right chains at farthest points
- Resample both chains to equal point count
- Average corresponding points → centerline
- RDP (ε=3) + Chaikin (2 passes, open polyline)
- Circle-brush stamping

**Why it failed:** Two likely issues:
1. **Contour tracing** — The Moore neighborhood walk may not have
   produced a clean, complete ordered contour of the irregular
   hand-painted region. The `traceOrderedContour` function uses a
   visited-pixel approach that can get stuck or skip sections on
   complex shapes with narrow protrusions or concavities.
2. **Farthest pair assumption** — Works for simple elongated shapes
   (like in export-hole.mjs where contours come from clean zone
   classification). Fails for complex winding paths where the two
   farthest points may not be the logical "endpoints" of the path.
   A U-shaped or S-shaped path might have farthest points that
   produce a bad left/right split.
3. **Left/right chain mismatch** — If the contour isn't cleanly
   split, the resampled chains don't correspond properly and the
   averaged centerline goes off-course.

**Root cause:** The export pipeline works because it starts from
clean, classified zone data. The GUI smoothing starts from noisy
hand-painted pixels — the contour is inherently messier.

---

## Approaches NOT Yet Tried

### A. Voronoi Diagram Skeleton
The dominant GIS approach (used by `label_centerlines` Python lib,
`centerline` Python lib, ArcGIS Polygon To Centerline, GRASS
`v.voronoi -s`).

**How it works:**
1. Trace contour of the polygon
2. Densify contour to get evenly-spaced boundary points
3. Compute Voronoi diagram of boundary points
4. Keep only Voronoi edges that are inside the polygon
5. Prune short branches (spurs)
6. Select longest path → centerline
7. Smooth with Gaussian or spline

**Pros:** Inherently smooth, operates on vector boundary geometry,
industry-proven for road centerlines.
**Cons:** Complex to implement from scratch in JS (~200+ lines for
Voronoi + pruning + path selection). Voronoi computation exists in
d3-delaunay which could be used.

### B. Maximal Inscribed Disks
From lane centerline research (PMC paper).

**How it works:**
1. For each interior pixel, find its distance to nearest contour
2. Find pixels where the inscribed circle touches two different
   contour segments → these are centerline points
3. Link centers into ordered polyline

**Pros:** Most accurate approach (max deviation <0.15m in research).
**Cons:** Most complex to implement. Needs contour segment tracking.

### C. Improved Contour-Split with Better Contour Tracing
Fix v4 by using a proper marching-squares or OpenCV-style contour
tracer instead of the simple Moore walk, and improve the farthest-
pair logic to handle winding paths.

**Pros:** Minimal change from v4, addresses the root failures.
**Cons:** May still fail on complex Y-junctions. Marching squares
gives sub-pixel accuracy which helps.

### D. Morphological Approach — Erode Then Trace
Iteratively erode the mask until only a 1px-wide spine remains.
Different from Zhang-Suen because morphological erosion uses a
circular structuring element (smoother than Zhang-Suen's
neighborhood conditions).

**Pros:** Simple concept, naturally smooth because circular erosion
averages out bumps.
**Cons:** May disconnect at narrow points. Erosion count must match
half the stroke width exactly. May still need cleanup.

### E. User Waypoint Mode
Let the user click a few waypoints along the path, then auto-fit a
smooth spline and stamp at uniform width. Sidesteps all automatic
centerline extraction.

**Pros:** Always produces correct results. Simple to implement.
**Cons:** Changes UX paradigm (not fully automatic). Requires
user interaction per path.

### F. Hybrid: Skeleton + Contour-Split Verification
Use Zhang-Suen for topology (connectivity, branches), but for each
chain, use contour-split averaging to refine the geometry. The
skeleton tells us WHERE the centerline goes, the contour averaging
tells us EXACTLY where it should be at each cross-section.

**Pros:** Best of both worlds — skeleton topology + contour geometry.
**Cons:** Complex integration.

---

## Key Insights From Research

1. **Raster skeletonization is fundamentally noisy.** Every academic
   source and GIS tool acknowledges this. Zhang-Suen, morphological
   thinning, and distance transform methods all suffer from pixel-
   level staircase artifacts on irregular inputs.

2. **Vector-based methods (Voronoi, contour-split) produce smoother
   results** because they operate on the polygon boundary geometry
   rather than individual pixels.

3. **The export pipeline's contour-split approach works** — but only
   when the input contour is clean (from zone classification). The
   GUI's hand-painted mask produces messy contours.

4. **Voronoi diagram is the industry standard** for polygon → centerline.
   Multiple libraries exist (Python: `centerline`, `label_centerlines`;
   R: `centerline`; GIS: ArcGIS, GRASS). All use Voronoi + pruning.

5. **d3-delaunay** (available via CDN) provides Voronoi computation
   in JavaScript. Could be leveraged for approach A.

6. **Maximal inscribed disks** are the most accurate approach in
   recent research but also the most complex to implement.

---

## Technical Context

### Existing Code (in app.js)
- `smoothOBMask()` — working, uses traceOBContours + RDP + Chaikin
- `smoothCartPathMask()` — current v4 implementation (broken)
- `traceOBContours()` — Moore neighborhood border trace for OB
- `traceOrderedContour()` — added in v4, may have issues
- `resampleChain()` — added in v4, arc-length resampling
- `stampCircle()` — added in v4, circle brush stamping
- `rdpSimplify()` / `chaikinSmooth()` — working geometry helpers
- `rasterizePolygon()` — scanline polygon fill

### Grid Dimensions
- Zone grid: ~2596 × 3124 pixels (0.2m/px at full res)
- Cart path width: ~12-15px at full res (~2.5m)
- Typical contour: 500-5000 points

### Export Pipeline (export-hole.mjs) — Working Reference
The export pipeline in `extractCartPathContours()` successfully
extracts cart path spines using:
1. Flood fill connected cart path regions
2. Trace contour with Moore neighborhood
3. Find two farthest contour vertices (path endpoints)
4. Split contour into left/right edge chains
5. Resample both chains to 200 points
6. Average corresponding points → centerline spine
7. RDP simplification on the spine

This produces clean spines that Unity renders as strip meshes.
The key difference: export starts from **classified zone data**
(clean boundaries), while GUI smoothing starts from **hand-painted
pixels** (noisy boundaries).

### Available Libraries (CDN)
- d3-delaunay: Voronoi diagram computation
- No other relevant geometry libraries currently loaded

---

## Recommendation for Next Attempt

**Try Voronoi (approach A)** using d3-delaunay from CDN. It's the
industry-proven approach and d3 handles the hard part (Voronoi
computation). The implementation would be:

1. Trace contour of cart path region (use `traceOBContours` which
   works for OB — adapt for cart path mask)
2. Densify contour to ~1px spacing
3. Compute Voronoi diagram of contour points via d3-delaunay
4. Filter: keep only Voronoi edges where both endpoints are inside
   the cart path mask
5. Build graph from internal Voronoi edges
6. Find longest path (= centerline)
7. Smooth with existing RDP + Chaikin
8. Stamp at uniform width

If Voronoi is too complex, fall back to **approach E (user
waypoints)** — guaranteed to work, simple to implement, just
requires a small UX change.

---

## Files Changed (across all attempts)
- `Tools/UHoleLite/app/index.html` — btn-smooth-ob, btn-smooth-cp
- `Tools/UHoleLite/app/app.js` — smoothOBMask (working),
  smoothCartPathMask (broken v4), helper functions
