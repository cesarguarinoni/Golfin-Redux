# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-04-06

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
| Leveling Economy | ✅ Rarity-based |
| Shop | Not started |
| Gameplay | Not started |

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
- `Assets/Scripts/Editor/CourseImporter/HoleImporter.cs` — Unity importer
- `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` — JSON data classes
- `Assets/Scripts/HoleMetadata.cs` — runtime hole metadata component
- `Assets/Scripts/Debug/WalkCamera.cs` — WASD + mouse look camera
- `Assets/Golf/Courses/lomond-country-club/Generated/Hole_01.unity` — the scene

### Coordinate Conventions

- **UHole local coords:** `x = dLonM` (east=+X), `z = -dLatM` (north=-Z)
- **Unity terrain:** positioned at `(-width/2, 0, -length/2)`, extends +X and +Z
- **Heightmap:** `heights[res-1-x, res-1-y]` — Unity SetHeights uses `[x_index, z_index]`, NOT `[z, x]` as docs imply
- **Texture:** sampled pixel-by-pixel from geo coords, `U=0→west, V=0→north`, no flips
- **Anchors:** raw `(local.x, 0, local.z)` — no negation
- **Anchor world coords:** from basemap tile geo-bounds (v2 alignment tool), not affine transform

### Open Items for Next Phase

1. **Texture-terrain alignment** — FIXED. Diagnostic revealed Unity `SetHeights` uses `heights[x_index, z_index]` (not `[z, x]` as docs imply). Fix: `heights[res-1-x, res-1-y]`. Bump and red square now in same corner. Diagnostic markers need removing (Step 9 in TellCode.md).
2. **Height verification** — need to validate in-game terrain elevations match the real golf course. Current elevation range 120.81m–205.62m (85m total) includes surrounding forested hillsides — may look exaggerated. Walk around and compare terrain shape to known features.
3. **DEM5A (5m lidar) integrated** — 25/25 tiles fetched, full Lomond coverage. 98.7% DEM5A sampling, z14 fallback for 220 edge pixels. Files: `scripts/lib/dem5a.mjs`, updated `fetch-gsi-basemap.mjs` and `export-hole.mjs`.
4. **Bounds too generous** — 80m padding captures steep forested hillsides beyond the fairway, inflating the elevation range. Consider reducing padding or adding fairway-edge anchors.

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

### UHole
- GSI tile server: `cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg`
- GSI DEM: 256×256 CSV grid per tile, values in meters, `e` = no data
- Nominatim/OpenStreetMap lookups unreliable for Japanese golf courses — verify with Google Maps
- Official map images have info panels on the left — don't use full image for bounds
- Affine transform extrapolates poorly beyond control point range
- Anchor world coords from basemap tile geo-bounds are more reliable than affine-derived coords

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
- `CLAUDE.md` — Claude Code session rules + project architecture
