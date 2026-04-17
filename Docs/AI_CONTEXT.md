# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-04-17

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete (incl. Phase G stat diffs) |
| Club Inventory | ✅ Phases C–F complete |
| Balls Inventory | ✅ Phase H complete |
| Items Inventory | ✅ Phase I complete |
| Bags Inventory | ✅ Phase J complete |
| 3D Course Pipeline | ✅ Phase K prototype complete — Hole 1 with DEM terrain, water, mountains, trees, shadows |
| UHole Tool | ✅ Alignment v2 (stacked overlay), export pipeline working |
| UHole Lite | ✅ Full pipeline + GUI. Mesh overlays for all zones. |
| Leveling Economy | ✅ Rarity-based |
| Shop | Not started |
| Gameplay | Not started |

---

## Session Changes (2026-04-17 — Tee Platforms)

### Completed
- **Flat tee platforms (Parts A+B+C):** Each `zones.tee[]` polygon is now an absolute-Y flat platform with a 2m terrain skirt. Multi-tee holes get independent platforms at their own elevation. Tee meshes' interior verts are flattened and border verts resampled against the ramped terrain post-depression.
  - `TeeSkirtMeters = 2.0f`, `TeeMeshRegistration` struct, `_teePlatformYByRegionId`, `_teeMeshRegistryByRegionId` added
  - Tee removed from shared `depress` mask (no more tilted tee via relative drop)
  - `PatchTeeMeshBorderVerts()` called from `ImportHoleInternal` after `DepressTerrainUnderOverlays`
  - Log extended with `tee platforms:` and `tee skirts:` counts

### Still Open
- Verify on stress-test holes: Hole 4 (2 tees, different elevations), Hole 1 (3 tees), Hole 7 (tee near water), Hole 18 (6 tees)
- Tuning `TeeSkirtMeters` if mounds look too steep/gradual

---

## Session Changes (2026-04-15)

### Completed
- **Tee marker rework (complete):**
  - Facing: markers now face closest fairway per tee group (computed from `fairway-contours.json`)
  - Pair orientation: controlled via `perpDir = Cross(up, toFairway)` — places balls left/right relative to play direction
  - Spread: 36-direction axis scan across tee region contour, finds longest inset span (3m border margin)
  - Order: Blue marker at bottom (reversed `t` so Blue = `rangeMin`), Red at top
  - Single-area tees: center of their area (pair still faces fairway)
  - Both `HoleLiteImporter` and `HoleGeoImporter` updated with consistent coordinate mappings
- **Re-import Current Hole menu (new):**
  - `Import/Re-import Current Hole` menu item
  - Reads `HoleMetadata.importType` from open scene, shows confirmation dialog
  - Dispatches to correct importer: Lite / LiteFlat / Geo / GeoFlat
  - `HoleMetadata.cs` updated with new `importType` field
  - New file: `Assets/Scripts/Editor/CourseImporter/ReimportCurrentHole.cs`
- **Hole Debug Window (new):**
  - `Hole/Debug Tools` EditorWindow
  - **Set Camera:** top-down orthographic, reads `greens.json` to orient so green is at top of screen (CCW 90° corrected)
  - **Capture Scene:** renders scene camera to PNG via RenderTexture
  - **Capture Game:** `ScreenCapture.CaptureScreenshot`
  - Saves to `Assets/Screenshots/{SceneName}/{SceneName} - Scene/Game - {timestamp}.png`
  - New file: `Assets/Scripts/Editor/CourseImporter/HoleDebugWindow.cs`

### Still Open
- Verify Set Camera CCW 90° fix places green at top (not left) — awaiting user test

---

## Session Changes (2026-04-14)

### Completed
- **Water rework (complete):** Flat CDT meshes, contour-based depression, deeper shore slopes
  - Water surface now perfectly flat per body (single Y = min terrain height - 0.05m)
  - CDT triangulation replaces ear-clip (consistent with fairways/tees)
  - Depression moved into `DepressTerrainUnderOverlays()` (contour-based, same system as fairways)
  - `ShoreDepthMeters` 0.1→0.4m, `ShoreRadius` 2→10 cells (~3m ramp)
  - `TerrainYOffset` decoupled from `ShoreDepthMeters` (set to 0.4f)
  - Per-body absolute-Y water bed (not relative drop — handles rolling terrain)
  - Inverted underwater ramp at contour boundary (fixes terrain interpolation cliff)
  - URPWater depth range widened (0.3→0.8m)
  - Verified on Hole 01 + Hole 12

### Spec Deltas (from WATER_REWORK_BRIEF.md)
Original spec got ~70%. Key fixes that emerged from testing:
- `normalizedFlat` had to use `TerrainYOffset` not `ShoreDepthMeters`
- Relative depression broke on rolling terrain → absolute-Y per body
- Shore chamfer propagates nearest-body index for multi-body holes
- Shore blur rejected (raised cells above water) — wider radius alone sufficient
- Inverted underwater ramp needed at contour boundary to match terrain interpolation

### Still Open
- Cart path T-junction overshoot (needs new approach)
- `TerrainYOffset` could be derived from `ShoreDepthMeters` (cosmetic coupling fix)
- Interpolation-at-contour-boundary bug may affect bunkers too (flagged for future investigation)
- Test water on remaining holes beyond 01 + 12

---

## Active Work — Course Visual Polish

### Water Rework (2026-04-14) ✅
See session changes above. Full details in `Docs/WATER_REWORK_PLAN.md` (spec) and `Docs/WATER_REWORK_BRIEF.md` (implementation report).

### OB Feature Export Fix + Cart Path Overlap Avoidance (2026-04-13) ✅
- Fixed export pipeline: trees/cart paths in OB zones were lost because merged grid gives OB priority. Now uses separate `trees_mask` and `cart_path_mask` overlays.
- Trees: +60,896 pixels recovered (277K → 338K)
- Cart path skeleton clipping: extended tee-only clipping to exclude fairway (1), bunker (6), tee (10) using `terrain_grid` (base zones)
- Spine nudging (`nudgeSpinesFromContours`): iterative geometry-based push
  - 15/18 holes fully clean; 3 holes have ≤3 sub-1m residual overlaps

### Smooth Play↔Non-Play Terrain Transition (2026-04-13) ✅
- Boundary-height propagation + smoothstep ramp to Gaussian-blurred DEM

### OB↔Rough Transition (2026-04-13) ✅
- OB reuses T_Rough with darker/yellower tint, 4px splatmap boundary blend

### Cart Path Depression Fix (2026-04-13) ✅
- Smoothstep gradient, full width + 0.30m margin, splatmap edge painting

---

## UHole Lite — Map-Based Hole Pipeline ✅

Alternative to full UHole (satellite tiles + DEM). Uses official course map illustrations as textures.

### Zone Overlay Architecture (2026-04-08, updated 2026-04-14)

**Terrain splatmap = rough/semi-rough base only.** All other zones are
contour-traced mesh overlays with smooth edges:

| Zone | Approach | Mesh type |
|---|---|---|
| Green | **Mesh overlay (CDT submesh)** | `CreateGreenMeshCDT` — submesh 0 = surface, submesh 1 = collar (0.6m dilation ring) |
| Bunker | Mesh overlay (bowl) | `CreateContourMesh` — 4-ring bowl |
| Water | **Mesh overlay (flat CDT, URPWater shader)** | CDT triangulation, flat Y per body, `URPWater/Standard` shader |
| **Fairway** | **Mesh overlay (flat)** | CDT triangulation, mow stripes, inward fringe ring |
| **Tee box** | **Mesh overlay (flat)** | CDT triangulation + gradient border ring |
| **Cart path** | **Spine-based strip mesh** | Centerline extracted from contour, fixed-width ribbon, terrain-draped |
| Rough | Splatmap | Base terrain layer |
| Semi-rough | Splatmap | Terrain layer |
| OB | Splatmap | Same T_Rough texture, tinted darker via diffuseRemapMax |

### Contour Pipeline
1. **traceBorder** — Moore neighborhood trace (direction-aware walk)
2. **RDP simplification** — closed polygon. Epsilon=1.0 for fairway, default=2.0 for smaller zones
3. **Chaikin smoothing** — 2 passes default
4. **CDT triangulation** — Constrained Delaunay (BurstTriangulator) for fairway/tee/water meshes

### Terrain Depression System
- **Overlay depression:** 0.40m drop under overlay meshes to prevent z-fighting
- **Depression inset:** 0.20m inward from contour edge (fairway/tee default)
- **Cart path depression:** Spine-based polygon, full width + 0.30m margin, smoothstep gradient
- **Water depression:** Absolute-Y per body in `DepressTerrainUnderOverlays()`, inverted ramp at boundary
- **Shore slope:** Chamfer distance from water contour, ShoreRadius=10 cells, ShoreDepthMeters=0.4m, smoothstep ramp. Per-body index propagation for multi-body holes.
- **TerrainYOffset:** 0.4f (decoupled from ShoreDepthMeters). Must be ≥ ShoreDepthMeters.

### Key Learnings (accumulated)
- Splatmap edges are **inherently pixel-jagged** — mesh overlays are the answer
- Zone grid is **2596×3124** (0.2m/px) — RDP epsilon must account for this
- `traceBorder` naive 8-walk only traced 22% of fairway border — Moore neighborhood fixed it
- RDP collapses narrow corridors. Chaikin shrinks them. Uniform dilation can't fix shape-specific shrinkage.
- Cart path contour meshes spill into neighbors — spine-based strip mesh is correct approach
- `SetHoles()` is too coarse for small bunkers — contour-based mesh overlays are the correct architecture
- URP: `Shader.Find("Standard")` returns null; use `Universal Render Pipeline/Lit` with `_Smoothness`
- JPG textures fill alpha=white causing plastic sheen — mask map with A=0 fixes it
- Unity `SetHeights` uses `heights[x_index, z_index]` (not `[z, x]` as documented)
- Realistic Tree prefabs: LODGroup on child (not root) + particle systems — must instantiate as standalone GameObjects
- Morphological close (dilate + erode) destroys narrow water channels. Dilate-only or skip.
- `filesystem:edit_file` fails silently on smart/curly apostrophe mismatches — use `write_file` for full rewrites
- **Terrain interpolation at contour boundary** — Unity terrain linearly interpolates between heightmap cells. A flat mesh sitting on top of a depression boundary will hover where the contour cuts cells diagonally. Fix: inverted ramp (flush at edge, deeper in interior). May affect bunkers too (flagged for future).
- **Relative vs absolute heightmap drops** — relative drops (`h - constant`) break on rolling terrain where some cells are higher than the target surface. Use absolute Y (`set to targetY - margin`) for features like water beds.
- **Shore blur is harmful** — averaging shore cells with out-of-radius neighbors raises them above water surface. Wider radius alone is sufficient.

### On the Horizon
- Cart path T-junction overshoot (needs new approach from architect)
- `TerrainYOffset` → derived from `ShoreDepthMeters` (minor coupling fix)
- Interpolation-at-contour-boundary investigation for bunkers
- Test water on all 18 holes
- Small bunker lip polish (~0.13m above terrain)
- UHole Lite GUI completion (cart path layer, layer button bar, brush visibility)
- Remaining 17 holes beyond Hole 1 prototype
- Shooting mechanics
- Login and Reward Points integration
- Character pipeline (VRoid Studio identified as primary path; deferred)

### Pipeline Steps
1. **Scrape** — downloads hole GIFs + scorecard data
2. **Extract** — crops illustration, removes legend, upscales to 1024×
3. **Detect Tees** — HSL color matching, 72/72 tees found
4. **Classify Zones** — 11-zone HSL classification, majority filter
5. **Generate Terrain** — procedural heightmap with slope, noise, zone modifiers
6. **Export** — manifest, heightmap, texture, anchors, zones, bunkers, greens, water, fairway-contours, zone-contours

### GUI (`Tools/UHoleLite/app/`, port 4174)
- Launch: `Tools/UHoleLite/Launch GUI.bat`
- Features: hole navigation, orientation controls, view modes, draggable tee markers, zone painting, brush tool, Ctrl+Z undo, zoom/pan, Smooth OB button

### Unity Importer
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` (~large)
- Menu: `Import > Lite > Normal/Flat > Hole 01..18 + All`
- Key methods: `ApplySplatmap`, `CreateFlatZoneMeshes`, `CreateFairwayMesh`, `CreateFringeRing`, `CreateGradientBorderRing`, `CreateRaisedMesh`, `CreateZoneMeshes`, `CreateGreenMeshes`, `CreateWaterMeshes`, `DepressTerrainUnderOverlays`, `BuildSpinePolygon`, `MarkContourCells`, `MarkWorldContourCells`

### Splatmap Layers
| Index | Texture | Zone |
|---|---|---|
| 0 | T_Fairway_Light | Fairway (light mow stripe) |
| 1 | T_Green_Albedo | Green |
| 2 | T_Semirough_Albedo | Semi-rough |
| 3 | T_Rough_Albedo | Rough (catch-all base) |
| 4 | T_Bunker_Albedo | Bunker |
| 5 | T_Tee_Albedo | Tee |
| 6 | T_RoadAsphalt_Albedo | Cart path |
| 7 | T_Fairway_Dark | Dark fairway (mow stripes) |
| 8 | T_Rough_Albedo (tinted) | OB — same texture, darker via diffuseRemapMax |

### Key Files
- Pipeline: `Tools/UHoleLite/scripts/` (7 scripts + lib/ + diagnose-fairway.mjs)
- Config: `Tools/UHoleLite/config/lomond-country-club.json`
- Output: `Tools/UHoleLite/output/lomond-country-club/`
- GUI: `Tools/UHoleLite/app/`
- Docs: `Docs/BUNKER_RESEARCH.md`, `Docs/WATER_FINDINGS.md`, `Docs/WATER_REWORK_PLAN.md`, `Docs/WATER_REWORK_BRIEF.md`

### DEM Heightmap Pipeline (2026-04-09)

**GeoAlign tool** (`Tools/GeoAlign/`) — web app for geo-aligning hole
illustrations to GSI satellite imagery via control points + affine transform.
Hole 1 aligned with 6 control points, mean residual 0.8m.

**Quadratic surface fit (v4):** `height = a*x² + b*y² + c*x*y + d*x + e*y + f`
- ONE surface fit to all playable zones (fairway, green, tee, bunker, rough, semi-rough, cart path)
- Playable zones = pure quadratic surface (zero DEM detail)
- Trees/OB/background = quadratic + 75% DEM residual (5 blur passes) for mountainous terrain

**Cart path spine mesh:** Contour polygon → split at farthest points → resample
both edge chains → average = centerline spine. Unity extrudes fixed-width strip
along spine, sampling terrain height at each vertex pair.

**Mountain backdrop:** Single `Mountains.fbx` instance, scale 0.7, Y=30.

### Key Terrain Values
- Heightmap: 2049×2049 (~0.3m/cell for holes grid)
- Overlay y-offsets: fairway 0.01m, tee 0.01m (CDT), fringe 0.012m, tee border 0.008m, cart path 0.01m
- Depression: 0.40m under overlays, 0.20m inset (fairway/tee), cart path full width + 0.30m margin
- Water: flat at minTerrainH - 0.05m, absolute-Y bed 0.3m below surface, inverted ramp at boundary
- Shore: ShoreRadius=10 cells, ShoreDepthMeters=0.4m, TerrainYOffset=0.4f
- Bunker terrain hole cut: 90% scale (large), shingle overlap v7 (small <7m)
- DEM residual: 75% for trees/OB/background, 5 blur passes

---

## Tree Placement System (2026-04-10) ✅

- Export tree-zones.json from UHole Lite + TreePlacer.cs in Unity
- Mixed mode: terrain trees + standalone GameObjects (particles, complex hierarchy)
- Tree Settings editor window (Trees > Tree Settings)
- Save/Load Presets + session auto-persistence
- Directional light & shadows: soft shadows, Mixed bake, 100m distance

### Tree Brush Tool (2026-04-17) ✅

- New `Window > Trees > Brush Tool` EditorWindow (`TreeBrushTool.cs`)
- Shift+click paints N jittered trees in a radius; Ctrl+click erases; B key toggles
- Reuses TreePlacer palette/weights; no separate prefab list
- Per-folder BrushFolderSettings (scale/sink/spacing) independent of importer, persisted via EditorPrefs
- Painted standalone trees under `PaintedTrees` container (survives TreePlacer re-imports)
- Exclusion zones: same overlay-polygon test as TreePlacer; disc turns orange over excluded areas
- Full undo per stroke (terrain trees + standalone GOs)
- TreePlacer: `NormalizeLODGroup` → `internal`; added `BuildExclusionPolygonsForActiveScene()` + `IsBlockedByOverlay()`

---

## Phase K — 3D Golf Course Prototype ✅ MILESTONE COMPLETE

Official map → control points → affine transform → heightmap + aerial texture + anchors → Unity scene → walkable terrain

### Key Files
- `Docs/TellCode.md` — Unity task instructions
- `Tools/UHoleLite/docs/TASK.md` — UHole Lite task instructions
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — Unity importer (map pipeline)
- `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` — JSON data classes
- `Assets/Scripts/Editor/CourseImporter/TreePlacer.cs` — Tree placement
- `Assets/Scripts/Editor/CourseImporter/TreePlacerWindow.cs` — Tree Settings GUI

---

## Lomond Country Club Data

- **Name:** ローモンドカントリー倶楽部
- **Location:** 2570-3 Ryoocho, Kameyama, Mie 519-0222, Japan
- **Verified center:** lat 34.91318, lon 136.44164
- **Holes:** 18, Par 72
- **Hole 1:** Par 5, 531yd (Back), HDCP 9

---

## Quick Architecture

- **CSV-first** data, **Resources.Load** for sprites, **Event-driven UI**
- **Namespaces:** `Golfin.Roster`, `Golfin.Inventory`, `Golfin.CourseImport`, `Golfin.Course`
- **Singletons:** CharacterManager, ClubManager, BallManager, BagManager, ItemManager
- **Platform:** Windows (PowerShell)

## Reference Docs

- `Docs/INVENTORY_REFERENCE.md` — patterns, file locations, APIs for all inventory screens
- `Docs/TellCode.md` — architect → code instructions (Unity)
- `Tools/UHoleLite/docs/TASK.md` — architect → code instructions (UHole Lite)
- `Docs/BUNKER_RESEARCH.md`, `Docs/WATER_FINDINGS.md`, `Docs/WATER_REWORK_PLAN.md`, `Docs/WATER_REWORK_BRIEF.md`
- `CLAUDE.md` — Claude Code session rules + project architecture
