# UHole Geo — Satellite-Based Course Pipeline (Phase N)

## Overview

UHole Geo is a new tool that replaces UHole Lite's illustration-based workflow
with GSI satellite orthophotos. It coexists with UHole Lite — both tools can
run side by side. The Unity importer reads from either tool's export folder
based on a menu selection.

**Name:** UHole Geo (satellite-based, geographic coordinates)
**Tool folder:** `Tools/UHoleGeo/`
**Export folder:** `Tools/UHoleGeo/output/<course-id>/export/hole-XX/`
**Pipeline ID:** `"uhole-geo"` (in manifest `pipeline` field)
**Unity menu:** `GOLFIN > Import Hole > From Geo > Hole 01..18`

Note: The existing Lite importer menu items will also be reorganized:
`GOLFIN > Import Hole (Lite) > ...` → `GOLFIN > Import Hole > From Lite > ...`

## What Changes vs UHole Lite

| Aspect | UHole Lite | UHole Geo |
|--------|-----------|-----------|
| Source image | Course guide illustration (GIF) | GSI satellite tile crop (PNG) |
| Coordinate system | Affine transform (6+ control points) | Direct lat/lon bounding box |
| Terrain sizing | Yardage × 1.3, aspect ratio from illustration | Computed from bounding box (haversine) |
| Zone classification | Auto from illustration colors + manual override | Manual painting only (on satellite backdrop) |
| Heightmap↔zone alignment | ~5-8m error from affine residuals | Pixel-perfect (same coordinate grid) |
| 90° CCW rotation | Yes (illustrations are portrait) | No (satellite is north-up, terrain is north-up) |
| DEM sampling | Affine → lat/lon → DEM | Direct pixel → lat/lon → DEM |

## What Stays the Same

- Manual zone painting workflow (separate layer PNGs)
- Brush tools, smoothing options in GUI
- Zone layer structure: zones grid, trees mask, cart path mask, OB mask
- DEM5A elevation sampling (same tiles, same `dem5a.mjs` library)
- Contour tracing pipeline (traceBorder, RDP, Chaikin)
- All export JSON formats (bunkers, greens, fairways, water, cart paths, etc.)
- Export file structure (hole-manifest.json, heightmap.raw, zones.json, etc.)
- Unity importer logic (reads same JSON formats — only the export path changes)

## Folder Structure

```
Tools/
├── UHoleLite/          ← existing, untouched
│   ├── app/
│   ├── scripts/
│   ├── output/
│   └── ...
└── UHoleGeo/           ← new
    ├── app/
    │   ├── index.html
    │   ├── app.js
    │   └── styles.css
    ├── scripts/
    │   ├── dev-server.mjs
    │   ├── fetch-satellite.mjs   ← NEW: download + stitch GSI tiles
    │   ├── classify-zones.mjs    ← simplified: manual PNG only, no auto
    │   ├── detect-tees.mjs       ← reuse from UHoleLite (copy or symlink)
    │   ├── generate-terrain.mjs  ← simplified: no affine, no rotation
    │   ├── export-hole.mjs       ← simplified: no rotation
    │   └── run-all.mjs
    │   └── lib/
    │       ├── dem5a.mjs         ← shared (copy or symlink)
    │       ├── terrain.mjs       ← shared
    │       └── tiles.mjs         ← shared
    ├── config/
    │   └── lomond-country-club.json
    ├── output/
    │   └── lomond-country-club/
    │       ├── course.json
    │       ├── holes/
    │       │   ├── 01/
    │       │   │   ├── hole-bounds.json     ← bounding box definition
    │       │   │   ├── satellite.png        ← stitched satellite image
    │       │   │   ├── zones-painted.png    ← your hand-painted zones
    │       │   │   ├── trees-mask.png       ← manual tree mask
    │       │   │   ├── cart-path-mask.png   ← manual cart path mask
    │       │   │   ├── ob-mask.png          ← manual OB mask
    │       │   │   ├── zones.json           ← output from classify-zones
    │       │   │   ├── tees.json
    │       │   │   ├── terrain-meta.json
    │       │   │   └── heightmap.raw
    │       │   └── 02/ ...
    │       └── export/
    │           └── hole-01/                 ← same structure as UHoleLite
    │               ├── hole-manifest.json
    │               ├── heightmap.raw
    │               ├── texture.png          ← satellite image (or painted)
    │               ├── zones.json
    │               ├── anchors.json
    │               ├── bunkers.json
    │               ├── greens.json
    │               ├── water.json
    │               ├── fairway-contours.json
    │               ├── cart-paths.json
    │               ├── zone-contours.json
    │               └── tree-zones.json
    └── package.json
```

## Hole Definition: `hole-bounds.json`

Created by the GUI's map bounding box selector. No manual coordinate entry.

```json
{
  "schema_version": "1.0.0",
  "course_id": "lomond-country-club",
  "hole_number": 7,
  "par": 4,
  "stroke_index": 5,
  "championship_yards": 430,
  "tees": {
    "back":    { "yards": 430, "color": "blue" },
    "regular": { "yards": 410, "color": "green" },
    "front":   { "yards": 385, "color": "white" },
    "ladies":  { "yards": 335, "color": "red" }
  },
  "bounds": {
    "north": 34.9088,
    "south": 34.9045,
    "east": 136.4349,
    "west": 136.4286
  },
  "gsi_zoom": 18,
  "image_file": "satellite.png",
  "image_dimensions": { "width": 1048, "height": 716 }
}
```

The `tees` and `par` data can be pulled from `course.json` (already scraped)
or entered manually in the GUI.

## Key Scripts

### `fetch-satellite.mjs` (NEW)

Downloads GSI satellite tiles for the bounding box at the specified zoom level,
stitches them into a single PNG per hole.

```
Usage: node scripts/fetch-satellite.mjs lomond-country-club 7
```

Input: `hole-bounds.json` (bounds + zoom)
Output: `satellite.png` (stitched image, north-up)

Tile source: `https://cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg`
(same GSI tile server we already use)

### `generate-terrain.mjs` (SIMPLIFIED)

No affine transform. No rotation. Direct bounding box → pixel → lat/lon:

```javascript
for (let hy = 0; hy < RES; hy++) {
  for (let hx = 0; hx < RES; hx++) {
    const nx = hx / (RES - 1);
    const ny = hy / (RES - 1);

    // Direct lat/lon from bounding box — no affine, no control points
    const lat = bounds.north - ny * (bounds.north - bounds.south);
    const lon = bounds.west  + nx * (bounds.east  - bounds.west);

    const elev = sampleDem5a(demTiles, lat, lon);
    heightmap[hy * RES + hx] = elev;
  }
}
```

No post-rotation needed. The heightmap pixel grid is already aligned with
the satellite image and the zone grid. `hy=0` = north, `hy=RES-1` = south.
`hx=0` = west, `hx=RES-1` = east.

Terrain dimensions computed from bounding box:
```javascript
const terrainWidthM  = haversine(bounds.north, bounds.west, bounds.north, bounds.east);
const terrainLengthM = haversine(bounds.north, bounds.west, bounds.south, bounds.west);
```

The quadratic surface fit, residual blending, and all Phase L terrain relief
improvements apply identically — they operate on the heightmap grid regardless
of how it was populated.

### `export-hole.mjs` (SIMPLIFIED)

No contour rotation (no `(x,z) → (z,x)` swaps). Contour points are exported
in the same coordinate system as the satellite image and heightmap:

```javascript
// UHole Lite did: worldX = contour.z, worldZ = contour.x  (90° CCW)
// UHole Geo does: worldX = contour.x, worldZ = contour.y  (identity)
```

The `center_local` and `size_m` fields are computed directly from lat/lon
via the bounding box mapping.

Manifest `pipeline` field: `"uhole-geo"`

### `classify-zones.mjs` (SIMPLIFIED)

No auto-classification from illustration colors. Reads the manually painted
zone PNG (`zones-painted.png`) and converts it to the zones grid format.

The color → zone mapping stays the same as UHole Lite's manual override mode.
Trees mask, cart path mask, and OB mask are read from separate PNGs.

## GUI

### New: Map Bounding Box Selector

The main new GUI feature. Shown when creating a new hole or editing bounds.

- Full-screen map view using GSI satellite tiles (Leaflet.js or similar)
- Map centered on course coordinates (from `course.json` or config)
- Draggable/resizable rectangle overlay
- Rectangle corners displayed as lat/lon (read-only, for verification)
- Terrain dimensions shown in meters (auto-computed from rectangle)
- "Confirm" button → saves `hole-bounds.json` + triggers `fetch-satellite.mjs`

### Existing features carried over:

- Canvas with hole image (now satellite instead of illustration)
- Layer tabs: Zones, Trees, Cart Paths, OB
- Brush tools (zone type selector, brush size)
- Smoothing button (zone boundary blurring)
- Layers bar with toggle visibility
- Export button (runs pipeline)

## Unity Importer

### New file: `HoleGeoImporter.cs`

A copy of `HoleLiteImporter.cs` with these changes:

1. **Export path:** reads from `Tools/UHoleGeo/output/` instead of
   `Tools/UHoleLite/output/`

2. **Menu items:** `GOLFIN > Import Hole (Geo) > Hole 01..18`

3. **Pipeline check:** validates `manifest.pipeline == "uhole-geo"`

4. **No 90° CCW rotation anywhere:**
   - Heightmap: `heights[y, x]` loaded directly (same as now — no change)
   - Texture: NOT rotated (remove `RotateTexture90CCW` call)
   - Zone grid lookup: direct mapping (remove the X/Z swap)
     ```csharp
     // UHole Lite:  gx = normZ * (smW-1), gy = normX * (smH-1)
     // UHole Geo:   gx = normX * (smW-1), gy = normZ * (smH-1)
     ```
   - Contour points: `worldX = contour.x`, `worldZ = contour.y`
     (no `(z, x)` swap)
   - Anchors: `worldPos = new Vector3(anchor.local.x, 0, anchor.local.y)`
   - Green centroid: `new Vector3(gc.x, 0, gc.y)`

5. **Terrain dimensions:** `terrainX = manifest.terrain.terrain_width_m`
   and `terrainZ = manifest.terrain.terrain_length_m` — NO swap.
   (UHole Lite swaps these because the illustration is portrait and the
   terrain is landscape after rotation. Satellite images are already in
   the correct orientation.)

6. **Splatmap rotation:** remove the X/Y swap in `ApplySplatmap`.
   Direct: `gx = normX * (zoneW-1)`, `gy = normY * (zoneH-1)`.

Everything else (bunker bowl mesh, green raised mesh, fairway CDT, cart path
spine strip, water overlay, depression, trees, mountains, lighting, camera)
stays exactly the same — these work in world space and don't care about the
source coordinate system.

## Implementation Status

**UHole Geo is already implemented** (built with Claude Code). The tool folder,
scripts, GUI, and Unity importer are all in place.

### Current menu structure (implemented):
```
Import > Lite > Import Hole 01 Lite .. Import Hole 18 Lite / Import All Holes Lite
Import > Geo  > Import Hole 01 Geo  .. Import Hole 18 Geo  / Import All Holes Geo
```

### Scene and Asset Naming (implemented):

To allow side-by-side comparison of the same hole from both pipelines,
Geo imports use a `Geo` suffix:

| | Lite | Geo |
|---|---|---|
| Scene | `Hole_01.unity` | `Hole_01_Geo.unity` |
| Data folder | `Data/hole-01/` | `Data/hole-01-geo/` |
| Terrain asset | `TerrainData_Hole01.asset` | `TerrainData_Hole01Geo.asset` |

This means you can import hole 1 from both Lite and Geo, then switch between
`Hole_01.unity` and `Hole_01_Geo.unity` to compare them directly.

The `HoleGeoImporter.cs` appends the suffix in `ImportGeoHole()`:

```csharp
string scenePath = $"{generatedDir}/Hole_{holeId}_Geo.unity";
string dataDir = $"Assets/Golf/Courses/{courseId}/Data/hole-{holeId}-geo";
```

## Implementation Order

1. **Create `Tools/UHoleGeo/` folder structure** and `package.json`
2. **Copy shared libs** (`dem5a.mjs`, `terrain.mjs`, `tiles.mjs`)
3. **Write `fetch-satellite.mjs`** — tile download + stitch
4. **Write `hole-bounds.json` schema** and manual creation for 1-2 test holes
5. **Simplify `generate-terrain.mjs`** — remove affine, remove rotation
6. **Simplify `export-hole.mjs`** — remove contour rotation
7. **Simplify `classify-zones.mjs`** — manual PNG only
8. **Create `HoleGeoImporter.cs`** — copy of Lite, remove all rotation logic
9. **Build GUI** — map selector + painting canvas (carry over from UHole Lite)
10. **Test on holes 4 and 7** — verify heightmap alignment, visual quality

## Dependencies

- Phase L (Terrain Relief) should be done first — the improved residual
  blending applies to both Lite and Geo pipelines
- Phase M (Water Rework) is independent — applies to the Unity importer
  which is shared
- Leaflet.js (or similar) for the map view — CDN, no npm dependency needed

## Estimated Effort

- Scripts (fetch, generate, export, classify): 2-3 days
- Unity importer (HoleGeoImporter.cs): 1 day
- GUI (map selector + painting): 2-3 days
- Testing all 18 holes: 1-2 days
- **Total: ~1-1.5 weeks**
