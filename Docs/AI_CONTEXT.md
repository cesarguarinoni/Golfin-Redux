# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-04-07

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete (incl. Phase G stat diffs) |
| Club Inventory | ✅ Phases C–F complete |
| Balls Inventory | ✅ Phase H complete |
| Items Inventory | ✅ Phase I complete |
| Bags Inventory | ✅ Phase J complete |
| 3D Course Pipeline | ✅ Phase K prototype complete — Hole 1 walkable |
| UHole Tool | ✅ Alignment v2 (stacked overlay), export pipeline working |
| UHole Lite | ✅ Full pipeline + GUI with orientation, tee dragging, zone painting, heightmap view |
| Leveling Economy | ✅ Rarity-based |
| Shop | Not started |
| Gameplay | Not started |

---

## UHole Lite — Map-Based Hole Pipeline ✅

Alternative to full UHole (satellite tiles + DEM). Uses official course map illustrations as textures. Pipeline processes all 18 Lomond CC holes.

### Pipeline Steps (all complete)
1. **Scrape** — downloads hole GIFs + scorecard data
2. **Extract** — crops illustration, removes legend, upscales to 1024×
3. **Detect Tees** — HSL color matching, 72/72 tees found
4. **Classify Zones** — 11-zone HSL classification, majority filter
5. **Generate Terrain** — procedural heightmap with slope, noise, zone modifiers
6. **Export** — clean packages with manifest, heightmap, texture, anchors, zones

### GUI (`Tools/UHoleLite/app/`, port 4174)
- Launch: `Tools/UHoleLite/Launch GUI.bat`
- Features: hole navigation (18 holes), orientation controls (rotate/flip), view modes (Map/Zones/Overlay/Height), draggable tee markers, zone painting (brush + flood fill), zone legend/codex, brush size slider, Ctrl+Z undo, zoom/pan, "Regen Heightmap" button, save all
- Server: `scripts/dev-server.mjs` — APIs for course data, orientation, tees, zones, heightmap PNG, regen

### Unity Importer
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
- Menu: `GOLFIN > Import Hole (Lite) > Hole 01..18 + All 18`
- Heightmap: `heights[res-1-hy, hx]` (vertical flip only)
- Texture: simple copy, no rotation
- Anchors: `Vector3(anchor.local.x, 0, -anchor.local.z)`

### Known Issue: Bunker Terrain at 129×129
Small bunkers near greens appear as mounds rather than depressions. Root cause: 129×129 heightmap resolution too coarse — each cell covers ~4×5 zone pixels, blur pass averages small depressions with surrounding high terrain. **Resolution: switch to separate bunker meshes.** See `Docs/BUNKER_RESEARCH.md`.

### Next Steps (bunker meshes approach)
1. Stop depressing bunkers in heightmap — keep terrain smooth where bunkers are
2. Export bunker contour polygons from zone grid
3. In Unity importer, use `TerrainData.SetHoles()` to cut terrain at bunker locations
4. Generate/place bowl-shaped bunker meshes under the terrain holes
5. Add sand material + bunker collider for ball physics

### Key Files
- Pipeline: `Tools/UHoleLite/scripts/` (7 scripts + lib/)
- Config: `Tools/UHoleLite/config/lomond-country-club.json`
- Output: `Tools/UHoleLite/output/lomond-country-club/`
- GUI: `Tools/UHoleLite/app/` (index.html, app.js, styles.css)
- Docs: `Tools/UHoleLite/docs/TASK.md`, `Docs/BUNKER_RESEARCH.md`

---

## Phase K — 3D Golf Course Prototype ✅ MILESTONE COMPLETE

**End-to-end pipeline proven for Hole 1:**

Official map → control points → affine transform → heightmap + aerial texture + anchors → Unity scene → walkable terrain

### UHole Pipeline (Tools/UHole/)

| Step | Status |
|---|---|
| Source Intake | ✅ Official site scraped, 18 hole GIFs, scorecard data |
| GSI Basemap | ✅ 340 aerial tiles (z17) + 9 DEM tiles (z14), center corrected to 34.9115, 136.4370 |
| Alignment Tool v2 | ✅ Stacked overlay (official map on GSI), visual alignment, anchor placement |
| Affine Transform | ✅ 5 control points, mean residual 3.59m, max 8.95m |
| Export Pipeline | ✅ heightmap.raw (129×129), aerial-tiles.json (6 tiles), anchors.json (6 anchors) |
| Unity Importer | ✅ HoleImporter.cs — terrain, satellite texture, anchor markers, walk camera |

### Key Files

- `Tools/UHole/docs/TASK.md` — UHole task instructions
- `Docs/TellCode.md` — Unity task instructions
- `Assets/Scripts/Editor/CourseImporter/HoleImporter.cs` — Unity importer (satellite pipeline)
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — Unity importer (map pipeline)
- `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` — JSON data classes
- `Assets/Scripts/HoleMetadata.cs` — runtime hole metadata component
- `Assets/Scripts/Debug/WalkCamera.cs` — WASD + mouse look camera, Q/E vertical float, Space to ground

---

## Lomond Country Club Data

- **Name:** ローモンドカントリー倶楽部
- **Location:** 2570-3 Ryoocho, Kameyama, Mie 519-0222, Japan
- **Verified center:** lat 34.91318, lon 136.44164 (clubhouse, Google Maps)
- **Course center:** lat 34.9115, lon 136.4370 (estimated)
- **Holes:** 18, Par 72
- **Hole 1:** Par 5, 531yd (Back), HDCP 9

---

## Key Lessons (accumulated)

### Unity
- `GameObject.Find` misses inactive — use `Resources.FindObjectsOfTypeAll` filtered by `go.scene.isLoaded`
- `FindObjectOfType<T>()` misses inactive — pass `true` (includeInactive) in editor scripts
- ModalController assumes root stays active; only modalPanel child is toggled
- Graphic Raycaster must accompany any Canvas on child panels
- Raycast Target must be disabled on non-interactive Images
- Figma ÷ 1.4 = Unity TMP font size
- Unity `heights[row,col]`: Unity `SetHeights` uses `[x_index, z_index]` NOT `[z, x]` — empirically confirmed via diagnostic markers
- Unity `TerrainLayer`: U=0 → min X, V=0 → min Z
- Unity `Texture2D.GetPixels()`: pixels[0] = bottom-left (south-west for map tiles)
- **Bunkers should be separate meshes**, not heightmap depressions — 129×129 is too coarse for small features

### UHole / UHole Lite
- GSI tile server: `cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg`
- GSI DEM: 256×256 CSV grid per tile, values in meters, `e` = no data
- Nominatim/OpenStreetMap lookups unreliable for Japanese golf courses — verify with Google Maps
- Official map images have info panels on the left — don't use full image for bounds
- Affine transform extrapolates poorly beyond control point range
- Lomond CC hole diagram URL pattern: `lomond-cc.com/wp-content/themes/templateB/images/course_e{NN}.gif`
- Zone classification: 11 zones (background, fairway, green, semi_rough, rough, trees, bunker, water, cart_path, ob, tee_box)
- Tee detection: HSL→cluster→shape filter→mutual proximity, 72/72 high confidence
- Heightmap orientation for UHole Lite: `heights[res-1-hy, hx]` = vertical flip only

---

## Quick Architecture

- **CSV-first** data, **Resources.Load** for sprites, **Event-driven UI**
- **Namespaces:** `Golfin.Roster`, `Golfin.Inventory`, `Golfin.CourseImport`, `Golfin.Debug`
- **Singletons:** CharacterManager, ClubManager, BallManager, BagManager, ItemManager, etc.
- **Platform:** Windows (PowerShell)
- **Ball stats:** -10 to +10 range, no rarity, no level

## Reference Docs

- `Docs/INVENTORY_REFERENCE.md` — patterns, file locations, APIs for all inventory screens
- `Docs/Rules.md` — design constraints, conventions
- `Docs/Tasks.md` — current checklist
- `Docs/TellCode.md` — architect → code instructions (Unity)
- `Tools/UHole/docs/TASK.md` — architect → code instructions (UHole)
- `Tools/UHoleLite/docs/TASK.md` — architect → code instructions (UHole Lite)
- `Docs/BUNKER_RESEARCH.md` — bunker implementation research + plan
- `CLAUDE.md` — Claude Code session rules + project architecture
