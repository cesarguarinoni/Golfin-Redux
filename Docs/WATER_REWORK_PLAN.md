# Water System Rework — Phase M

## Problem

The current water shore slope system is fragile. It uses a cell-radius approach
(`ShoreRadius` / `ShoreDepthMeters`) that depresses terrain around water pixels
in the heightmap. This breaks when:

- Terrain changes from the smoothing system (especially outside play area)
- The heightmap resolution vs shore radius ratio doesn't produce clean edges
- Terrain depression from other overlays interacts with shore slopes

The water mesh itself (flat opaque contour overlay) is fine. It's the
**terrain-side shore handling** that causes visual problems at shorelines.

## Observation

Every other zone has converged on the same pattern that works well:

| Zone | Mesh | Terrain handling |
|------|------|-----------------|
| Fairway | CDT contour overlay | Flat depression via `DepressTerrainUnderOverlays` |
| Tee | CDT contour overlay | Flat depression via `DepressTerrainUnderOverlays` |
| Cart path | Spine strip overlay | Flat depression (full width + margin) |
| Bunker | Bowl mesh + terrain hole | 90% inward cut, no lip needed |
| Green | Raised mesh + collar | Terrain hole under collar extent |
| **Water** | CDT contour overlay | ❌ Shore slope (radius-based, fragile) |

Water is the last zone using a different terrain approach. It should join the
others.

## Proposed Approach

### Replace shore slope with depression-based system

1. **Keep the contour mesh overlay** — the flat opaque water mesh already works.
   No mesh changes.

2. **Add water to `DepressTerrainUnderOverlays`** — depress terrain under the
   water contour polygon using the same flat uniform drop as fairways/tees:
   - Depression depth: 0.30m (slightly less than fairway's 0.40m — water mesh
     sits lower anyway)
   - Inset: none (depress right to the contour edge)

3. **Extend depression 1-2m beyond contour** — use a dilated polygon (105-110%
   scale from centroid, or a fixed 1.5m outward offset) so the depression
   extends slightly past the water mesh edge. This prevents z-fighting at
   shores without any gradient math.

4. **Remove all shore slope code** from `HoleLiteImporter.cs`:
   - Delete the `ShoreRadius` and `ShoreDepthMeters` constants
   - Delete the shore slope section in `CreateTerrain` (if any remains)
   - Remove shore slope from `CreateWaterMeshes` (if it touches terrain)
   - The `-ShoreDepthMeters` offset in `terrainGO.transform.position.y` can
     stay (it's a tiny 0.1m offset that gives headroom for all depressions)

5. **Terrain hole under water** — currently water zones get terrain holes via
   `CreateWaterMeshes`. Keep this: the terrain hole hides the terrain under
   the water mesh, and the depression ensures the terrain edge around the hole
   slopes down naturally.

### Optional polish (low priority)

- **Shore fringe ring** — a thin (0.5m) ring mesh around the water edge using
  a darker rough/mud texture, similar to the fairway fringe ring. This would
  soften the visual transition at shorelines. Can be deferred.

- **Shore terrain tint** — paint a thin strip of darker splatmap texture at
  the water edge (like the cart path edge painting). Also deferrable.

## Implementation

### File: `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

#### In `DepressTerrainUnderOverlays`:

Add water contours to the depression mask. Read `water.json`, iterate water
regions, build world-space contour polygons (with the usual 90° CCW rotation),
dilate by 1.5m, and mark cells in the `depress` array — same pattern as
fairway/tee contours.

```csharp
// --- Water depression ---
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFile>(File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour == null || w.contour.Length < 3) continue;
            var worldContour = new Vector2[w.contour.Length];
            for (int i = 0; i < w.contour.Length; i++)
            {
                worldContour[i] = new Vector2(w.contour[i].z, w.contour[i].x);
            }
            // Dilate 1.5m beyond contour edge
            var dilated = DilateContour(worldContour, centroidX, centroidZ, 1.5f);
            MarkContourCells(dilated, depress, hRes, terrainPos, terrainSize);
        }
    }
}
```

#### Remove shore slope code:

Delete any shore-slope-specific terrain modification. The `ShoreRadius` and
`ShoreDepthMeters` constants can be removed (but keep `ShoreDepthMeters` if
it's still used for the terrain Y-offset calculation — check first).

### File: `Tools/UHoleLite/scripts/generate-terrain.mjs`

**No changes needed.** Water zones already get the quadratic surface value
(the old -9999 sentinel was removed previously). The pipeline doesn't do
anything water-specific anymore.

### File: `Tools/UHoleLite/scripts/export-hole.mjs`

**No changes needed.** Water contours are already exported in `water.json`.

## Verification

1. Import hole 18 (has water along the right side) — check shoreline is clean
2. Import hole 6 (par 3 with pond) — check water edge doesn't z-fight
3. Walk around water edges in play mode — no terrain poking through the mesh
4. Check that terrain outside the water mesh (shore area) slopes down gently
   from the depression, not cliff or bump

## Estimated Effort

- Depression wiring: ~30 lines in `DepressTerrainUnderOverlays`
- Shore slope removal: ~20 lines deleted
- Testing: 2-3 holes
- Total: ~1 hour
