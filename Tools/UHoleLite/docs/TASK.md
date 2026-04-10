# TASK.md — UHole Lite Instructions for Claude Code

> Claude Code: Read this file at the start of each task.
> After completing, add a status line at the bottom.
> Handoff: `Tools/UHoleLite/docs/TASK.md`

---

## Current Task — Export Tree Zone Mask

**Goal:** Extract zone 5 (trees) data from the zone grid and export
as `tree-zones.json` with a base64-encoded binary mask + region
contours. This gives the Unity TreePlacer both pixel-level precision
(mask) and region metadata (contours, area).

### What to add

In `Tools/UHoleLite/scripts/export-hole.mjs`, add tree zone
extraction and export in the `exportHole()` function.

#### 1. Extract tree regions + build binary mask

After the existing water extraction block (near the end of
`exportHole()`), add:

```javascript
// --- Build tree-zones.json ---
const treeRegions = extractZoneContours(zonesData, terrainMeta, 5, 30, 3.0, 2);
// zone 5 = trees, min 30px (skip tiny splotches), RDP epsilon 3.0
// (trees don't need precise contours), 2 Chaikin passes

// Build binary mask from zone grid (1 = tree zone, 0 = not)
const gridBuf = Buffer.from(zonesData.grid, 'base64');
const maskW = zonesData.source_dimensions.width;
const maskH = zonesData.source_dimensions.height;
const treeMask = Buffer.alloc(maskW * maskH);
for (let i = 0; i < gridBuf.length; i++) {
  treeMask[i] = gridBuf[i] === 5 ? 1 : 0;
}

const treeZonesOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  mask_width: maskW,
  mask_height: maskH,
  mask_base64: treeMask.toString('base64'),
  meters_per_pixel: {
    x: parseFloat((terrainMeta.terrain_width_m / maskW).toFixed(4)),
    z: parseFloat((terrainMeta.terrain_length_m / maskH).toFixed(4)),
  },
  tree_region_count: treeRegions.length,
  tree_regions: treeRegions.map(r => ({
    id: r.id,
    pixel_count: r.pixel_count,
    area_m2: parseFloat(
      (r.pixel_count *
       (terrainMeta.terrain_width_m / maskW) *
       (terrainMeta.terrain_length_m / maskH)
      ).toFixed(1)
    ),
    contour: r.contour,
    center_local: r.center_local,
    size_m: r.size_m,
  })),
};

fs.writeFileSync(
  path.join(exportDir, 'tree-zones.json'),
  JSON.stringify(treeZonesOutput, null, 2),
  'utf-8'
);

if (treeRegions.length > 0) {
  const totalArea = treeZonesOutput.tree_regions
    .reduce((sum, r) => sum + r.area_m2, 0);
  console.log(`  Tree zones: ${treeRegions.length} region(s), ` +
    `${totalArea.toFixed(0)} m² total`);
} else {
  console.log(`  Tree zones: none painted`);
}
```

#### 2. Add to manifest

In the `manifest` object, add after `cart_paths_file`:

```javascript
tree_zones_file: 'tree-zones.json',
```

#### 3. Update export result log

In the export result object at the bottom of `exportHole()`,
add `treeRegionCount: treeRegions.length` and include it in
the console log in `main()`.

### Verification

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
2. Console should show `Tree zones: N region(s), NNN m² total`
   (or `none painted` if zone 5 isn't painted yet)
3. `export/hole-01/tree-zones.json` should exist with:
   - `mask_width` = 2596, `mask_height` = 3124
   - `mask_base64` = base64 string (length = ceil(2596*3124 * 4/3))
   - `tree_regions` array with contours
4. If zone 5 is painted, mask should have 1s in those areas

### Do NOT

- Change any existing zone extraction (bunkers, greens, etc.)
- Change `traceBorder`, `simplifyPolygon`, `smoothPolygon`
- Change the zone grid or splatmap pipeline
- Change `extractZoneContours` function itself
- Remove or modify any existing export files
