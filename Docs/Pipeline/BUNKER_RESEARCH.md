# Bunker Implementation Research — 2026-04-06

## The Problem

At 129×129 heightmap resolution, small bunkers near the green only occupy 1-3 heightmap pixels. The blur pass averages these with surrounding higher terrain, pushing them back UP into mounds. The green zone's flat-but-elevated area makes this worse — bunkers adjacent to the green are surrounded by smooth, high terrain that dominates the blur.

## Industry Approaches

Research across TGC Designer Tools, Perfect Golf, Unity golf course communities, UE4 golf visualization projects, and Houdini golf generators reveals **three main approaches** to bunkers:

### Approach 1: Separate Mesh (Recommended)
- **Most professional golf games use bunkers as separate 3D mesh objects** placed ON TOP of the terrain, not carved INTO the heightmap
- Use Unity's **Paint Holes** feature to cut the terrain where the bunker sits
- Place a pre-modeled bunker mesh (bowl shape) underneath the hole
- The bunker mesh has its own collider for ball physics
- Advantages: crisp edges, consistent depth, no resolution issues
- Used by: Perfect Golf, most UE4 golf visualizations, Houdini golf generators
- **This is the approach we should adopt**

### Approach 2: Higher Resolution Heightmap
- Increase heightmap from 129×129 to 513×513 or 1025×1025
- Each bunker would then occupy ~20-50 heightmap pixels, enough for proper bowl shaping
- Disadvantages: larger file sizes, slower terrain generation, may be overkill for mobile
- Used by: TGC Designer Tools with real LIDAR data (2049×2049)

### Approach 3: Terrain Sculpting Post-Import
- Import the base terrain, then manually sculpt bunkers using Unity's terrain brush tools
- This is what most golf course recreation hobbyists do
- Time-intensive, not automatable
- Not viable for our pipeline

## Recommended Plan: Hybrid Approach

For GolfinRedux, use **Approach 1** for bunkers:

1. **Pipeline generates bunker metadata** (position, shape outline from zone grid, approximate size)
2. **Unity importer creates bunker GameObjects** — either:
   - A procedural mesh (bowl shape generated from the zone contour)
   - Or a prefab bunker bowl scaled to fit
3. **Paint holes in the terrain** at bunker locations using `TerrainData.SetHoles()`
4. **Place the bunker mesh below terrain surface** with a sand material
5. **Bunker collider** for ball physics (different friction/bounce from fairway)

### Implementation Steps
1. In `generate-terrain.mjs`: stop trying to depress bunkers in the heightmap. Keep terrain smooth/flat where bunkers are.
2. In `export-hole.mjs`: export bunker contours as polygon data (not just zone index)
3. In `HoleLiteImporter.cs`: create bunker GameObjects with bowl meshes and paint holes in terrain
4. Create a `BunkerMeshGenerator.cs` utility to procedurally generate bowl meshes from contour data

### Why Green-Adjacent Bunkers Fail Specifically
The green zone applies noise flattening (`green_flatness: 0.15`) and a drainage slope (`applyGreenSlope()`), making the green area smooth and slightly elevated. When a bunker pixel gets depressed by -3m, it creates a sharp dip surrounded by smooth high terrain. The blur pass (radius 2) averages this sharp dip with the smooth high surroundings, washing out the depression entirely or even pushing it up. Larger bunkers survive because they have more interior pixels far from the blur boundary.

## For Tomorrow

- The heightmap-based bunker depression approach has been pushed as far as it can go at 129×129 resolution
- The next step is implementing separate bunker meshes — this is a Phase K task for the Unity importer
- The current zone painting + regen heightmap workflow in the GUI is still useful for tuning zone boundaries
- Don't spend more time trying to fix bunker depressions in the heightmap — switch to mesh-based bunkers

## V1 Status (2026-04-07)

V1 bunker meshes are implemented as simple elliptical bowls derived from
bunker region bounding boxes. **V1 is a dead end.** Key issues:

- **SetHoles() is too coarse**: 128×128 hole grid for ~630×520m terrain =
  ~5m per cell. Small bunkers (5-15m) get 1-3 cells, rounding produces
  cuts that overshoot the bowl lip on one side while leaving terrain
  visible on the other. No ratio of hole-size to lip-size reliably works.
- **Bowl-over-terrain z-fights**: Without SetHoles, terrain renders through
  the bowl mesh. renderQueue overrides don't reliably beat Unity terrain
  in URP.
- **Shape**: Uniform ellipses from bounding boxes, not actual zone contours.
- **Depth**: Parabolic bowl, no directional variation.

## V2 Status (2026-04-07)

V2 contour-based bunker meshes are working on flat terrain:

- **Contour extraction**: Moore-neighbor border tracing → RDP simplification
  (closed polygon) → Chaikin smoothing (2 iterations) → CCW winding
- **Terrain holes**: 1025 heightmap resolution (1024 holes grid, ~0.6m/cell)
  with contour-traced point-in-polygon cutting at 90% scale
- **Mesh**: 4-ring bowl (rim → inner → mid → deep → center) following
  contour shape, sand material, MeshCollider
- **Splatmap**: Bunker zones mapped to rough texture (layer 3) instead of
  sand (layer 4) — mesh handles sand surface, prevents blur glow

**Next:** Re-enable heightmap, verify contour meshes work with real elevation.
