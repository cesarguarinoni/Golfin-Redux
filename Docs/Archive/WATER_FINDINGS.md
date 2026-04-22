# Water Implementation — Findings & Next Steps

## What We Tried (and Why It Failed)

### 1. Basin Mesh (4-ring concentric rings + terrain holes + transparent material)
- **Problem:** Serrated terrain-hole edges visible through transparent water
- **Why:** Terrain holes are discrete grid cells (~0.5m), creating staircase edges. Transparent material reveals them.
- **Verdict:** Over-engineered. Other golf games don't do this.

### 2. Morphological Close (dilate + erode)
- **Problem:** Erode step destroyed the entire lake — narrow sections got wiped out
- **Verdict:** Never use erode on water masks. Killed.

### 3. Tree Absorption (absorb zone-5 pixels adjacent to water)
- **Problem:** Inflated the water contour. Trees added mass to the shape. Various approaches to filter absorbed trees from contour all had side effects (losing lake bodies, pointy coasts, fat shapes).
- **Verdict:** Too fragile. Removed entirely. Manual painting in GUI is more reliable.

### 4. Dilate-Only (grow mask by N pixels, no erode)
- **Problem:** Made everything fatter. Thin waterways became rivers.
- **Verdict:** Removed.

### 5. Flat Plane (current — GEOMETRY IS CORRECT)
- **Problem:** Shape doesn't match the zone map. Water appears "fat" and has pointy coasts despite zone map being accurate.
- **Key insight:** The flat plane approach itself is correct (how all golf games do it). The contour extraction pipeline is the issue.

## The Actual Problem

The contour pipeline (traceBorder → RDP simplification → Chaikin smoothing) introduces shape distortion:

- **RDP with epsilon 2.0** straightens curves, making narrow features wider and inlets shallower
- **Chaikin smoothing (2 passes)** inflates concave sections outward while shrinking convex sections — net effect on irregular lake shapes is visible expansion
- **traceBorder** walks the pixel border via 8-connected neighbors — the walk can miss border pixels or take shortcuts, creating an imperfect representation

These distortions are small on tiny bunkers (< 20m wide) but visually significant on large water bodies (50-100m+ wide) with complex coastlines.

## The Right Fix for Tomorrow

**Skip the contour pipeline entirely for water.** Since we're using flat planes (no terrain holes, no basin mesh), we don't need a smooth mathematical contour. We can render water directly from the zone grid pixels.

### Approach: Rasterized Water Quad

Instead of tracing contours → simplifying → smoothing → building triangle fan mesh:

1. In the export, output the **raw bounding box and pixel data** for each water region (or just pass the zones.json through — it's already there)
2. In Unity, create a simple **textured quad** covering the water region's bounding box
3. Apply a **texture/alpha mask** generated from the actual zone grid pixels — water pixels = blue, non-water = transparent
4. The mask perfectly matches the zone map because it IS the zone map

This gives pixel-perfect water boundaries that exactly match what you see in the Hole Viewer. No contour tracing, no RDP, no Chaikin, no distortion.

### Alternative: Use Splatmap Layer for Water

Even simpler — we already have the splatmap pipeline painting zone 7 as rough. Instead:
- Add a water terrain layer (blue texture)  
- Paint zone 7 as the water layer in the splatmap
- No separate mesh needed at all — the terrain texture IS the water

The water "surface" would just be a painted area on the flat terrain. For gameplay, add invisible trigger colliders (box/polygon) over water areas for ball detection.

This is arguably the simplest possible approach and guarantees perfect zone-map matching since the splatmap already uses the zone grid.

### Implementation Comparison

| Approach | Pros | Cons |
|----------|------|------|
| Contour mesh (current) | Smooth edges | Shape distortion, complex pipeline |
| Rasterized quad + mask | Pixel-perfect match | Slightly pixelated edges at close zoom |
| Splatmap water layer | Zero extra geometry, perfect match | No separate mesh for collider, no visual depth |

### Recommendation

**Splatmap approach** is the path of least resistance — change `ZoneToLayer` to map zone 7 to a water terrain layer instead of rough, and add a water texture. The splatmap blur will give soft edges. For gameplay, generate simple box colliders from the water region bounding boxes.

If we want a distinct visual layer (for future water shader, reflections, etc.), the **rasterized quad** approach keeps the flat plane concept but eliminates contour distortion.

## Next Steps (Water Polish)

1. **Shore slope / collar mesh** — same approach as green collar. 2-3 rings
   descending from terrain height down to the water plane (Y=0.05).
   Semi-rough or rough texture on the slope. Gives shorelines a natural
   "the land dips into the water" look instead of a flat color boundary.
2. **SDF tuning** — if jaggies remain, try `sdfSpread` 4.0-5.0 or compute
   SDF on a 2x upscaled mask for more gradient resolution.
3. **Water shader** — future batch. Reflections, animated ripples, etc.
   Will be tackled together with a full texture/material polish pass
   (all current textures look plastic).
4. **GUI smooth tool** — longer-term idea: a smoothing brush in Hole Viewer
   that reshapes zone boundaries at the source (zone grid) before export.
   Would fix jaggies at the data level instead of downstream processing.

## Current State of Code

- `export-hole.mjs`: `extractWaterMasks()` replaces old `extractWaterContours()`. Exports per-region bbox + base64 binary mask. No contour pipeline (no RDP, no Chaikin). Schema v2.0.0.
- `HoleLiteImporter.cs`: `CreateWaterMeshes()` reads bbox + mask, computes SDF at import time, creates textured quad with alpha cutout. No `CreateFlatWaterMesh()` or `CreateWaterMaterial()` (deleted).
- `TASK.md` (UHole Lite): Needs cleanup to reflect current state.
- `TellCode.md`: Current task is Water Option 2 with SDF smoothing.

## Files Modified Today

- `Tools/UHoleLite/scripts/export-hole.mjs` — many iterations, now clean
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — flat plane water
- `Tools/UHoleLite/docs/TASK.md` — outdated, needs cleanup
- `Docs/TellCode.md` — outdated, needs update
