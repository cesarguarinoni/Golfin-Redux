# Flat Terrain Variant — Export & Import Plan

## Goal

Every time UHole Lite or UHole Geo exports a hole, it should produce **two**
export variants:

1. **Normal** — with DEM heightmap (current behavior)
2. **Flat** — identical zone data, meshes, etc. but with a perfectly flat
   heightmap (no elevation changes)

The Unity importer and TreePlacer should be able to import either variant via
separate menu items. This allows A/B comparison of any hole with and without
terrain relief.

## Naming Convention

| | Normal | Flat |
|---|---|---|
| **Export folder (Lite)** | `export/hole-01/` | `export/hole-01-flat/` |
| **Export folder (Geo)** | `export/hole-01/` | `export/hole-01-flat/` |
| **Scene (Lite)** | `Hole_01.unity` | `Hole_01_Flat.unity` |
| **Scene (Geo)** | `Hole_01_Geo.unity` | `Hole_01_Geo_Flat.unity` |
| **Data folder (Lite)** | `Data/hole-01/` | `Data/hole-01-flat/` |
| **Data folder (Geo)** | `Data/hole-01-geo/` | `Data/hole-01-geo-flat/` |
| **Terrain asset (Lite)** | `TerrainData_Hole01.asset` | `TerrainData_Hole01Flat.asset` |
| **Terrain asset (Geo)** | `TerrainData_Hole01Geo.asset` | `TerrainData_Hole01GeoFlat.asset` |
| **Manifest pipeline** | `"uhole-lite"` / `"uhole-geo"` | `"uhole-lite"` / `"uhole-geo"` |
| **Manifest terrain.heightmap_file** | `"heightmap.raw"` | `"heightmap.raw"` (flat data) |

## Pipeline Changes

### `generate-terrain.mjs` (both Lite and Geo)

**No changes.** The heightmap generation stays as-is. The flat variant is
created at export time, not generation time.

### `export-hole.mjs` (both Lite and Geo)

After assembling the normal export folder (`export/hole-XX/`), create a
second folder (`export/hole-XX-flat/`) with:

1. **Copy all files** from the normal export folder
2. **Replace `heightmap.raw`** with a flat heightmap:
   - Same resolution (2049×2049), same format (uint16be)
   - All values set to a single constant — the normalized flat height
     that maps to world Y=0
   - Read `terrain-meta.json` to get `max_elevation_m`. The flat value
     should be ~midrange so the terrain sits at a reasonable Y position.
     Use the **average elevation** of the normal heightmap as the flat
     value — this keeps the terrain at a natural height rather than at
     the min or max.

```javascript
// In export-hole.mjs, after normal export:

function createFlatExport(normalExportDir, flatExportDir, holesDir) {
  // 1. Copy all files from normal to flat
  fs.mkdirSync(flatExportDir, { recursive: true });
  for (const file of fs.readdirSync(normalExportDir)) {
    fs.copyFileSync(
      path.join(normalExportDir, file),
      path.join(flatExportDir, file)
    );
  }

  // 2. Generate flat heightmap
  const RES = 2049;
  const normalRaw = fs.readFileSync(path.join(normalExportDir, 'heightmap.raw'));
  
  // Compute average elevation from normal heightmap
  let sum = 0;
  for (let i = 0; i < RES * RES; i++) {
    sum += (normalRaw[i * 2] << 8) | normalRaw[i * 2 + 1];
  }
  const avgValue = Math.round(sum / (RES * RES));

  // Write flat heightmap (all pixels = average)
  const flatBuffer = Buffer.alloc(RES * RES * 2);
  for (let i = 0; i < RES * RES; i++) {
    flatBuffer.writeUInt16BE(avgValue, i * 2);
  }
  fs.writeFileSync(path.join(flatExportDir, 'heightmap.raw'), flatBuffer);

  // 3. Update manifest max_elevation_m to 0 (flat = no relief)
  const manifestPath = path.join(flatExportDir, 'hole-manifest.json');
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf-8'));
  manifest.terrain.max_elevation_m = 1; // minimal range, keeps math safe
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2), 'utf-8');

  console.log(`  Flat variant: avg=${avgValue} → ${flatExportDir}`);
}
```

### Why export-time, not generate-time?

- **Simpler:** No need to run generate-terrain twice or add flags
- **No manifest duplication:** The flat variant reuses all contour/zone/anchor
  data — only the heightmap changes
- **Consistent:** Both Lite and Geo get flat variants via the same logic
- **Fast:** Copying files + writing a flat buffer is ~100ms per hole

## Unity Importer Changes

### Menu Structure

```
Import > Lite > Import Hole 01 Lite .. Import Hole 18 Lite
              > Import All Holes Lite
              > Import Hole 01 Flat .. Import Hole 18 Flat
              > Import All Holes Flat
       > Geo  > Import Hole 01 Geo  .. Import Hole 18 Geo
              > Import All Holes Geo
              > Import Hole 01 Geo Flat .. Import Hole 18 Geo Flat
              > Import All Holes Geo Flat
```

### `HoleLiteImporter.cs`

Add flat variant methods. These call the same `ImportLiteHole()` but with
a different export subfolder and output paths:

```csharp
[MenuItem("Import/Lite/Import Hole 01 Flat")]
public static void LiteFlat01() { ImportLiteHoleFlat("lomond-country-club", 1); }
// ... 02-18 ...
[MenuItem("Import/Lite/Import All Holes Flat")]
public static void LiteAllFlat()
{
    for (int i = 1; i <= 18; i++)
        ImportLiteHoleFlat("lomond-country-club", i);
}

public static void ImportLiteHoleFlat(string courseId, int holeNumber)
{
    string holeId = holeNumber.ToString("D2");
    string projectRoot = Path.GetDirectoryName(Application.dataPath);

    // Read from flat export folder
    string exportPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
        courseId, "export", $"hole-{holeId}-flat");
    // Write to flat data/scene paths
    string dataDir = $"Assets/Golf/Courses/{courseId}/Data/hole-{holeId}-flat";
    string scenePath = $"Assets/Golf/Courses/{courseId}/Generated/Hole_{holeId}_Flat.unity";

    // Call the shared import logic with these paths
    ImportHoleInternal(courseId, holeNumber, exportPath, dataDir, scenePath);
}
```

This means extracting the core import logic from `ImportLiteHole()` into a
shared `ImportHoleInternal()` method that takes `exportPath`, `dataDir`, and
`scenePath` as parameters. The existing `ImportLiteHole()` calls it with the
normal paths.

### `HoleGeoImporter.cs`

Same pattern — add flat methods that call the same import logic with
`-flat` paths and `_Geo_Flat` scene suffix.

### Refactoring note

Both `ImportLiteHole` and `ImportLiteHoleFlat` share 100% of the import
logic — only the paths differ. Extract the body of `ImportLiteHole()` into:

```csharp
private static void ImportHoleInternal(
    string courseId, int holeNumber,
    string exportPath, string dataDir, string scenePath)
{
    // ... all existing import logic, using the provided paths ...
}
```

Then `ImportLiteHole()` and `ImportLiteHoleFlat()` just compute paths and
call `ImportHoleInternal()`. Same for the Geo importer.

## TreePlacer Changes

### Menu Structure

Add flat variants to TreePlacer:

```
Trees > Import All Trees Lite
      > Import All Trees Lite Flat
      > Import All Trees Geo
      > Import All Trees Geo Flat
      > Import All Trees          (runs all 4)
```

### Implementation

The existing `ImportAllTreesLiteMenuItem()` already parameterizes by scene
path and export path. Add flat versions:

```csharp
[MenuItem("Trees/Import All Trees Lite Flat")]
private static void ImportAllTreesLiteFlatMenuItem()
{
    string scenesDir = "Assets/Golf/Courses/lomond-country-club/Generated";
    string exportBase = Path.Combine(
        Application.dataPath, "..",
        "Tools/UHoleLite/output/lomond-country-club/export");

    if (TreePalette.Count == 0) ScanPrefabs();

    for (int h = 1; h <= 18; h++)
    {
        string scenePath = $"{scenesDir}/Hole_{h:D2}_Flat.unity";
        string exportPath = Path.Combine(exportBase, $"hole-{h:D2}-flat");
        // ... same logic as Lite, using flat paths ...
    }
}
```

Same pattern for `ImportAllTreesGeoFlatMenuItem()` using `_Geo_Flat` scenes
and `hole-XX-flat` exports from UHoleGeo.

Update `ImportAllTreesMenuItem()` to run all 4 variants.

## Summary of Changes

| File | Change |
|------|--------|
| `UHoleLite/scripts/export-hole.mjs` | Add `createFlatExport()` call after normal export |
| `UHoleGeo/scripts/export-hole.mjs` | Same — add `createFlatExport()` call |
| `HoleLiteImporter.cs` | Extract `ImportHoleInternal()`, add 18+1 flat menu items |
| `HoleGeoImporter.cs` | Extract `ImportHoleInternal()`, add 18+1 flat menu items |
| `TreePlacer.cs` | Add `Import All Trees Lite Flat` and `Import All Trees Geo Flat` |

## Estimated Effort

- Export changes (both tools): ~30 min
- Importer refactoring + flat menus: ~1 hour
- TreePlacer flat menus: ~30 min
- Testing: ~30 min
- **Total: ~2.5 hours**
