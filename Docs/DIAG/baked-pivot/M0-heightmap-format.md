# M0 — heightmap.bytes format + loader inventory

**Scope:** wire format, existing loader API, baker source, spatial layout. Input for M2 `BakedHeightProvider`.

## File path

`Tools/UHoleGeo/output/lomond-country-club/export/hole-{01..18}/heightmap.bytes`

All 18 present. Hole-01 is 16,793,640 bytes. Expected size: `36 + Resolution² × 4 = 36 + 2049² × 4 = 16,793,640` ✓.

## Wire format (GHM1 v1)

Fixed 36-byte little-endian header followed by a row-major `[z,x]` grid of int32 Q16.16 height samples.

| offset | size | type | field | value (hole-01) |
|-------:|-----:|------|-------|-----------------|
| 0  | 4 | ASCII `'G','H','M','1'` | magic | `GHM1` |
| 4  | 4 | int32 | version | 1 |
| 8  | 4 | int32 | Resolution | 2049 |
| 12 | 4 | float32 | SizeX (m) | terrain width, e.g. 576.2 |
| 16 | 4 | float32 | SizeZ (m) | terrain length, e.g. 261.2 |
| 20 | 4 | float32 | OriginX (world space) | -SizeX/2 |
| 24 | 4 | float32 | OriginY (world space) | -ShoreDepthMeters (-0.4) |
| 28 | 4 | float32 | OriginZ (world space) | -SizeZ/2 |
| 32 | 4 | int32 | format | 1 (= Q16.16) |
| 36 | 4 × R² | int32 | heights[z·R + x] (Q16.16 raw) | signed 24.8 meters-above-origin |

Write order is row-major with `for y in 0..res: for x in 0..res: write(heights[y,x])`. `HeightmapData` indexes the flat array as `heights[iz * Resolution + ix]`, so `heights` is `[z, x]`-major at read time.

## Origin convention

`HoleGeoImporter.cs:211` places the terrain GO at `(-terrainX/2, -TerrainYOffset, -terrainZ/2)`, where `TerrainYOffset = ShoreDepthMeters = 0.4`. So world-space XZ origin is the terrain-tile's south-west corner and Y=0 is `ShoreDepthMeters` above the heightmap's lowest representable height.

Practical consequence: for a flat course, `heightmap.SampleHeight(x, z)` returns `OriginY + storedHeight`, which for a point 0.4 m above terrain-origin lands at world Y=0 — what the rest of the sim expects as "flat ground."

## Loader API

**`Golfin.Physics.Runtime.HeightmapLoader`** (already exists, no changes needed for M2):

- `HeightmapData LoadFromBytes(byte[] data)` — validates magic + version + format, reads header, allocates `int[Resolution * Resolution]`, reads all Q16.16 raw int32 samples, constructs `HeightmapData`.
- `HeightmapData LoadFromTextAsset(TextAsset asset)` — convenience wrapper. Since `heightmap.bytes` lives under `Tools/UHoleGeo/…` (outside `Assets/`), it is not currently a `TextAsset` — needs a copy into `Assets/Resources/HoleData/Hole_XX/` during M2 setup, or the loader can read from `File.ReadAllBytes` at import time. The spec implies `Assets/Resources/HoleData/Hole_XX/` as the canonical location; M2 step 1 should copy-or-reference from there.

**`Golfin.Physics.HeightmapData`** (already exists, no changes needed):

- `fp SampleHeight(fp worldX, fp worldZ)` — bilinear interpolation between the four nearest cells. Clamps out-of-bounds coordinates to the edge. Returns `OriginY + interpolatedStoredHeight`. Implements `IGroundProvider.SampleHeight(fp, fp)`.
- `fp3 SampleNormal(fp worldX, fp worldZ)` — central differences interior, one-sided at boundaries, returns normalized `(-dhdx, 1, -dhdz)`.
- No 3-arg override on `HeightmapData` itself; the default interface method in `IGroundProvider` forwards `SampleHeight(x, z, preferred)` → `SampleHeight(x, z)`. M2's `BakedHeightProvider` replaces `HeightmapData` in the sim, so the forwarding is fine.

## Source of truth

**`Golfin.Physics.Editor.PhysicsHeightmapBaker`** (`Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs`).

- Reads the scene's `Terrain.terrainData.GetHeights(0, 0, res, res)` (0..1 floats).
- Scales to meters using `terrainData.size.y`, stores as Q16.16 int32.
- Writes to `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/heightmap.bytes`.
- Validates round-trip (reads file back, samples 100 random points, asserts <1 mm diff).

Critical timing: the baker runs on the scene AFTER `HoleGeoImporter` has applied shore/cart-path/tee/overlay depressions. So the baked heightmap already encodes every depression that the visible terrain carries. M2's provider can therefore treat `heightmap.SampleHeight(x, z)` as the authoritative "terrain surface Y at this XZ" without re-applying any depression math.

## Spatial extents (hole-01 specifics, from `terrain-meta.json`)

- Resolution: 2049 × 2049 (cell size ≈ 0.281 m × 0.128 m).
- Width/length: 576.2 m × 261.2 m.
- DEM playable elevation range: 50 m.
- Raw elevation range (for reference): 122.6 m..205.6 m.

## Edge cases Architect should be aware of

1. **`heightmap.bytes` is not under `Assets/`.** M2 will either copy it to `Assets/Resources/HoleData/Hole_XX/heightmap.bytes` (renamed to `.bytes` is fine — Unity treats `.bytes` files as `TextAsset`), or add a `PhysicsHeightmapBaker` output-path change to write both. Preference: copy. Keeps the baker tool path stable.
2. **Deletion history.** `Docs/AI_CONTEXT.md` notes `heightmap.bytes` was deleted per a prior open flag. Current on-disk state confirms the file exists for all 18 holes; deletion appears reversed (hole-01 file mtime is recent). If Cesar confirms the files are "good," no rebake needed.
3. **Q16.16 range.** Stored heights are relative to `OriginY`. Signed 32-bit Q16.16 caps at ±32,767.9999. For golf-course elevations (≤ 50 m), plenty of headroom.
4. **Bilinear, not cubic.** `HeightmapData.SampleHeight` is bilinear. Acceptable for golf; won't introduce sub-cell surface noise.

## Notes for Architect

- No new loader code needed. `HeightmapLoader.LoadFromBytes` + `HeightmapData` are sufficient for M2. The M2 deliverable is purely `BakedHeightProvider` (which composes `HeightmapData` + `BakedZoneClassifier` + zone offsets).
- `IGroundProvider`'s `SampleHeight(x, z, preferred)` default implementation forwards to the 2-arg; `BakedHeightProvider.SampleHeight(x, z, preferred)` can also safely forward to its own 2-arg since baked Y is authoritative.
- The prior F-Hotfix `SurfaceSnap` type-preference logic becomes vestigial for the sim path in M3 (it stays for ball placement only, per spec "Phase F deletes them").
