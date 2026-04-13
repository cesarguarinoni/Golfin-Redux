# Terrain Relief Improvement Plan — Phase L

## Problem Statement

The current heightmap pipeline uses a **single quadratic surface fit** across the entire
hole, then only adds back DEM residual (75%) in non-playable zones (trees/OB/background).
Playable zones get **0% residual** — pure quadratic surface.

This means:
- Ravines, cliffs, and valleys adjacent to the fairway are flattened out
- Mounds that should dip downward may appear raised
- The terrain looks unnaturally smooth in the rough areas between play zones
- Holes 4 and 7 are representative examples: Hole 4 has a pronounced drop left of the
  green (confirmed by course description: "グリーン左斜面下に落ちると苦戦します"), and
  Hole 7 has a ravine near the ladies tee — both are missing in-game

The Kendall tau test confirmed DEM data is correct (tau=0.7 N-S, 0.52 L-R), but the
quadratic surface is washing out local features — especially where the DEM dips 20-30m
below the fitted surface in a 50m span.

## Requirements

1. **Preserve overlay meshes** — fairway, green, tee, bunker, cart path meshes must NOT
   change. They sit on top of the terrain and sample `terrain.SampleHeight()`. The terrain
   under them (depressed by `DepressTerrainUnderOverlays`) doesn't matter visually.

2. **Improve terrain shape in rough/semi-rough/trees/OB** — these zones are visible as
   bare terrain. Ravines, mounds, and slopes must come through from the DEM.

3. **No bumpy micro-detail** — we want macro features (ravines, ridges, slopes) not
   5m-resolution DEM noise that makes the terrain look like crumpled paper.

4. **Smooth transitions** — where the improved rough terrain meets an overlay mesh edge,
   there should be no cliff or discontinuity.

## Approach: Zone-Aware Residual Blending

Instead of 0% residual in playable zones and 75% in non-playable zones, use a **graduated
residual system** based on zone type and distance from overlay mesh edges.

### Step 1: Keep the quadratic surface as the BASE

The quadratic fit is good for establishing the overall tee-to-green slope and preventing
wild elevation swings. Keep it as-is.

### Step 2: Zone-based residual fractions (change in `generate-terrain.mjs`)

Current:
- Playable zones (fairway, green, tee, bunker, semi-rough, cart path, rough): **0% residual**
- Non-playable zones (trees, OB, background): **75% residual** with 60-cell ramp

Proposed:
- **Overlay zones** (fairway, green, tee, bunker, cart path): **0% residual** — these are
  covered by meshes, terrain underneath is hidden
- **Semi-rough** (zone 3): **20% residual** — gentle terrain variation, close to fairway
- **Rough** (zone 4): **40% residual** — moderate terrain relief visible
- **Trees** (zone 5): **65% residual** — significant relief, but slightly smoothed
- **OB** (zone 9): **65% residual** — same as trees
- **Background** (zone 0): **75% residual** — full terrain character

### Step 3: Three-stage smoothing to eliminate bumpiness

The DEM has 5m grid resolution. At 2049 heightmap cells over ~400m terrain,
that's ~25 cells per DEM pixel. Raw DEM residual looks like a staircase of
25-cell-wide plateaus — which renders as the wavy/lumpy mounds already
visible on the current terrain.

Simple zone-masked blur passes (3×3 kernel) can't kill 25-cell-wide bumps
without hundreds of passes. Instead, use a three-stage pipeline:

**Stage A**: Gaussian pre-smooth the entire residual grid (radius=8, sigma=6.0)
**Stage B**: Zone-masked post-smooth after applying residual fractions
**Stage C**: Final global Gaussian pass to kill zone boundary seams

See implementation details below for parameters.

### Step 4: Distance-based transition at overlay edges

At the boundary where an overlay mesh meets bare terrain, the terrain height must match
what `SampleHeight()` returns for the mesh vertices. Currently `DepressTerrainUnderOverlays`
handles this by dropping terrain under overlays.

The concern: if rough terrain near a fairway edge has a ravine, but the fairway mesh edge
samples terrain at the quadratic-surface level, there could be a visible cliff at the
mesh boundary.

**Solution**: Use a distance ramp from overlay zones. Cells within N cells of an overlay
zone get a blended residual fraction that ramps from 0% (at the boundary) to the target
zone fraction (at distance N). This creates a smooth terrain transition at mesh edges.

- N = 30 cells (~4.5m at 2049 resolution over 300m terrain) — enough to avoid visible
  cliffs, small enough to not flatten ravines that are 10m+ from fairway edges.

### Step 5: Verify mesh conformance

After the new residual is applied, overlay meshes (which sample `terrain.SampleHeight()`)
will naturally follow the new terrain. **No mesh changes needed** — they already drape
to whatever the terrain is. The depression step happens after terrain generation.

The only risk is if a ravine is SO steep that the mesh vertex spacing (1m grid from CDT)
can't follow it and creates stretched triangles. This is unlikely given 20-40% residual
fractions and blurring, but worth checking visually.

## Implementation Details

### File: `Tools/UHoleLite/scripts/generate-terrain.mjs`

#### Change 1: Replace binary playable/non-playable split with per-zone fractions

In `generateHeightmapDEM()`, after the quadratic surface is applied to the entire
heightmap, replace the current single-category residual application with:

```javascript
// Zone-specific residual fractions
const ZONE_RESIDUAL = {
  0: 0.75,   // background
  1: 0.0,    // fairway (overlay mesh covers it)
  2: 0.0,    // green (overlay mesh)
  3: 0.20,   // semi-rough
  4: 0.40,   // rough
  5: 0.65,   // trees
  6: 0.0,    // bunker (overlay mesh)
  7: 0.0,    // water (overlay mesh)
  8: 0.0,    // cart path (overlay mesh)
  9: 0.65,   // OB
  10: 0.0,   // tee box (overlay mesh)
};
```

#### Change 2: Distance ramp from overlay zones

Build a distance field from overlay zones (zones with 0% residual). For each non-overlay
cell, if it's within RAMP_CELLS of an overlay zone, reduce its residual fraction
proportionally:

```
effectiveFraction = zoneFraction * smoothstep(dist / RAMP_CELLS)
```

Where RAMP_CELLS = 30.

#### Change 3: Apply blurred residual per zone

For each zone, blur the raw DEM residual (raw - quadratic) with zone-appropriate passes,
then blend:

```
finalHeight = quadraticSurface + blurredResidual * effectiveFraction
```

#### Change 4: Update zone-masked smoothing passes

```javascript
const ZONE_SMOOTH_PASSES = {
  3: 12,   // semi-rough: very heavy smoothing — must feel like a gentle slope
  4: 8,    // rough: heavy smoothing — broad contours only, no DEM pixel noise
  5: 5,    // trees: moderate smoothing
  9: 5,    // OB: moderate smoothing
  0: 3,    // background: light smoothing
};
```

#### CRITICAL: Anti-bumpiness safeguards

The existing terrain already has a wavy/lumpy appearance on bare rough (visible
as gentle mounds ~5-10m across on the grass). This comes from DEM 5m grid
resolution artifacts surviving the current smoothing. The new residual system
MUST NOT make this worse — it should ideally improve it.

The 3×3 weighted blur smooths features proportional to sqrt(passes) * cellSize.
At 2049 resolution over ~400m terrain, cellSize ≈ 0.2m. So:

- 20 passes → smoothing radius ≈ 0.2 * sqrt(20) ≈ 0.9m effective
- That's still not enough to kill 5m DEM grid bumps

The zone-masked blur alone won't cut it at this resolution. We need a
**two-stage approach**:

**Stage A: Pre-smooth the raw residual** (before per-zone application)
- Compute residual = rawDEM - quadraticSurface for ALL cells
- Apply a single Gaussian blur (radius=8, sigma=6.0) to the entire residual
  grid. This is ~1.6m effective radius at 0.2m/cell — kills DEM pixel noise
  but preserves 15m+ features (ravines, ridges)
- This pre-smoothed residual is what gets multiplied by zone fractions

**Stage B: Post-smooth per zone** (after residual is applied)
- Apply zone-masked blur passes to further soften each zone:
  - Semi-rough: 6 passes
  - Rough: 4 passes  
  - Trees/OB: 3 passes
  - Background: 2 passes

**Stage C: Final global pass**
- One Gaussian blur (radius=4, sigma=3.0) across ALL non-overlay cells
- Kills zone boundary seams and any remaining ripples

With this three-stage approach, the minimum surviving feature scale is:
- Pre-smooth kills < ~3m features
- Post-smooth kills another ~1-2m
- Final pass kills another ~1m
- Net result: only features > ~15-20m survive → ravine-scale, not bump-scale

The existing bumpiness in the screenshot should also improve because the
current pipeline's smoothing will be replaced by this more aggressive system.

If during testing any zone still looks bumpy, increase the pre-smooth
Gaussian radius. That's the single most effective knob.

### File: `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

**No changes needed.** The importer reads heightmap.raw and applies it as-is. The
smoothing in the importer (Gaussian blur in non-play areas + boundary height propagation)
operates on top of whatever the pipeline produces. This existing smoothing is actually
complementary — it further softens the OB/background areas. Since we're now handling
per-zone residuals in the pipeline, we could potentially simplify the importer's smoothing
later, but for now leave it untouched.

### File: `Tools/UHoleLite/scripts/export-hole.mjs`

**No changes needed.** It just copies heightmap.raw.

## Verification Plan

After implementing, re-generate and import holes 4, 7, and 1 (as baseline):

1. **Hole 4** (par 3, 138y): Check that the left-of-green slope is visible. The course
   description says dropping left of the green is trouble — there should be a visible
   downslope.

2. **Hole 7** (par 4, 430y): Check the ravine near the ladies tee. The DEM shows a
   ~25m elevation drop from fairway to the ravine floor. With 40% residual in rough,
   this should appear as a ~10m dip — noticeable but not extreme.

3. **Hole 1** (par 5, 531y): Baseline check that the existing terrain still looks good
   and overlay meshes still sit properly.

4. **Run verify-dem-sample.mjs** on holes 4 and 7 to check Kendall tau improves from
   0.5→0.7+ on the cross-axis.

## Estimated Effort

- Pipeline changes (generate-terrain.mjs): ~100 lines changed — medium complexity
- Testing: re-generate terrain for 3 holes, import, walk around
- Total: ~2-3 hours implementation + testing

## Future Considerations

- If specific holes need hand-tuned residual fractions, we could add per-hole overrides
  in the course config JSON
- The Unity-side heightmap smoothing (in HoleLiteImporter.cs) could eventually be
  simplified since the pipeline now handles zone-aware blending, but this is optional
  and low priority
