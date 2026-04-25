# M0 — UHoleGeo outputs inventory

**Scope:** what `Tools/UHoleGeo/` emits per hole and how it relates to the M1 `BakeZoneJsonTool` + M2 `BakedHeightProvider`.

## Per-hole output directory

`Tools/UHoleGeo/output/lomond-country-club/holes/{01..18}/`

Confirmed present for all 18 holes. Contents of `holes/01/`:

| file | size (bytes) | purpose |
|------|-------------:|---------|
| `heightmap.raw` | 8,396,802 | 2049×2049 uint16-BE DEM, 0..50 m normalized. Source for the Unity Terrain's heightmap. Used at import time, not at runtime. |
| `hole-bounds.json` | 813 | GSI lat/lon bounds + championship yards + tee colors + satellite image dims. Hole metadata. |
| `tees.json` | 986 | 4 tee entries (`tee_back / tee_regular / tee_front / tee_ladies`) in normalized [0,1] + pixel coords. Feeds `HoleGeoImporter` tee-pad generator. |
| `terrain-meta.json` | 628 | Heightmap resolution, physical `terrain_width_m`/`terrain_length_m`, min/max elevation, DEM source, green/tee centroids, `slope_drop_m`. The runtime-interesting scalar for the pivot is `resolution: 2049`, `terrain_width_m: 576.2`, `terrain_length_m: 261.2`. |
| `zones.json` | 12,671,604 | **Raster**, not vector. Fields: `zone_index` (0 background → 10 tee_box), `zone_stats`, `grid_encoding: "base64_uint8"`, `grid: <base64 string>`. Decodes to a 2048×928 uint8 pixel grid, one zone index per pixel. |
| `zones.png` | — | Debug visualization. |
| `satellite.png` | — | Source imagery. |

## Separate export directory

`Tools/UHoleGeo/output/lomond-country-club/export/hole-{XX,XX-flat}/`

Contains the runtime binary artifacts produced by `PhysicsHeightmapBaker` (Editor tool):

| file | size (bytes) | purpose |
|------|-------------:|---------|
| `heightmap.bytes` | 16,793,640 (hole-01) | **This is the file M2 reads.** 36-byte header + `2049²` int32 Q16.16 heights, row-major `[z,x]`. Built by `PhysicsHeightmapBaker` from the in-scene `Terrain.terrainData` AFTER `HoleGeoImporter` applies shore/cart/tee/overlay depressions, so the stored height is the final post-depression terrain Y at that XZ. |
| `heightmap.raw` | 8,396,802 | Pre-Unity DEM in uint16-BE. Not used at runtime. |

All 18 hole-export folders have `heightmap.bytes`. The `-flat` siblings exist for hole-01..18 but carry no `heightmap.bytes` (flat variants, not relevant to the pivot).

## Key takeaway for M1 / M2

- **Raster source, vector target.** `zones.json` is a pixel grid, not polygons. The spec's JSON schema (`polygons: [[[x,z],...]]`) is NOT what UHoleGeo emits today. M1's `BakeZoneJsonTool` therefore walks the *Unity scene's* zone-mesh hierarchy (Green/Bunker/Fairway/Tee/CartPath/Water GOs produced by `HoleGeoImporter`) and extracts contour polygons from their MeshFilters + XZ projection. This is exactly what the spec's step 4 describes.
- **Heightmap is bit-exactly already there.** `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/heightmap.bytes` + `HeightmapLoader.LoadFromBytes` + `HeightmapData` form the M2 ground provider almost for free.
- **Terrain metadata is in `terrain-meta.json`,** not the heightmap header (except for `resolution`, `sizeX/Z`, `posX/Y/Z` which ARE in the header per `HeightmapLoader`).

## Path conventions

| Role | Path pattern |
|------|--------------|
| UHoleGeo per-hole exports (DEM + vector JSON + raster grid) | `Tools/UHoleGeo/output/lomond-country-club/holes/{01..18}/` |
| Physics baker outputs (runtime binary) | `Tools/UHoleGeo/output/lomond-country-club/export/hole-{01..18}/heightmap.bytes` |
| Unity course material + TerrainData | `Assets/Golf/Courses/lomond-country-club/Data/hole-{01..18}-geo/` |
| Unity generated hole scenes | `Assets/Golf/Courses/lomond-country-club/Generated/Hole_{01..18}_Geo.unity` |
| Spec-proposed runtime zone data (NEW in M1) | `Assets/Resources/HoleData/Hole_{XX}/zones.json` |

## Notes for Architect

- `HoleData/Hole_XX/zones.json` does not exist yet; M1 creates it. Path is under `Assets/Resources/` per spec so `Resources.Load<TextAsset>` can read it at runtime.
- The raster `zones.json` emitted by UHoleGeo is fine to keep around for debugging/visualization but is not consumed by the new sim.
- `heightmap.bytes` for all 18 holes already exists — M2's `BakedHeightProvider` can load any of them today; no rebake needed for M2 kickoff.
