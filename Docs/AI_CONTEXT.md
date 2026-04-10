# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-04-09 (evening)

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete (incl. Phase G stat diffs) |
| Club Inventory | ✅ Phases C–F complete |
| Balls Inventory | ✅ Phase H complete |
| Items Inventory | ✅ Phase I complete |
| Bags Inventory | ✅ Phase J complete |
| 3D Course Pipeline | ✅ Phase K prototype complete — Hole 1 walkable with DEM terrain, water shader, mountains |
| UHole Tool | ✅ Alignment v2 (stacked overlay), export pipeline working |
| UHole Lite | ✅ Full pipeline + GUI. Mesh overlays for all zones. |
| Leveling Economy | ✅ Rarity-based |
| Shop | Not started |
| Gameplay | Not started |

---

## UHole Lite — Map-Based Hole Pipeline ✅

Alternative to full UHole (satellite tiles + DEM). Uses official course map illustrations as textures.

### Zone Overlay Architecture (2026-04-08)

**Terrain splatmap = rough/semi-rough base only.** All other zones are
contour-traced mesh overlays with smooth edges:

| Zone | Approach | Mesh type |
|---|---|---|
| Green | Mesh overlay (raised) | `CreateRaisedMesh` — collar + putting surface |
| Bunker | Mesh overlay (bowl) | `CreateContourMesh` — 4-ring bowl |
| Water | **Mesh overlay (URPWater shader)** | Ear-clip contour mesh + `URPWater/Standard` shader |
| **Fairway** | **Mesh overlay (flat)** | Ear-clip triangulation, mow stripes, inward fringe ring |
| **Tee box** | **Mesh overlay (flat)** | Ear-clip triangulation + gradient border ring |
| **Cart path** | **Spine-based strip mesh** | Centerline extracted from contour, fixed-width ribbon, terrain-draped |
| Rough | Splatmap | Base terrain layer |
| Semi-rough | Splatmap | Terrain layer |

### Contour Pipeline
1. **traceBorder** — Moore neighborhood trace (direction-aware walk, fixed from naive 8-walk that only traced 22% of fairway)
2. **RDP simplification** — closed polygon. Epsilon=1.0 for fairway (grid is 2596×3124 = 0.2m/px, so old epsilon=3.0 was 15px tolerance, too aggressive). Default=2.0 for smaller zones.
3. **Chaikin smoothing** — 2 passes for fairway, 2 default. Corner-cutting shrinks narrow corridors; fewer passes = less shrinkage.
4. **Mesh creation** — ear-clip triangulation for concave shapes (centroid-fan escapes concave curves)

### Key Learnings (2026-04-08)
- Splatmap edges are **inherently pixel-jagged** — no amount of SDF/blur/contour-rasterization fixes it. Mesh overlays are the answer.
- Zone grid is **2596×3124** (0.2m/px) — much higher resolution than the 794×956 illustration texture. RDP epsilon must account for this.
- `traceBorder` naive 8-walk only traced 22% of fairway border — Moore neighborhood trace fixed it.
- RDP collapses narrow corridors because points on both walls are close to the simplification line.
- Chaikin shrinks narrow corridors (corner-cutting pulls inward). More passes = more shrinkage on narrow shapes.
- Uniform polygon dilation can't fix shape-specific shrinkage — it bloats wide sections and pushes into neighbors.
- Cart path contour meshes spill into bunkers/other zones — splatmap is acceptable for cart paths.

### On the Horizon
- ~~Fix water (broken after DEM changes — sunken too low)~~ ✅ Fixed — terrain.SampleHeight() at each vertex
- Fix bunker 7 (2.2m radius terrain poke-through)
- GeoAlign remaining 17 holes
- Trees (auto-place in tree zones, cover steep terrain)
- Illumination (directional light, shadows)
- Visual/texture polish pass
- Full 18-hole pipeline beyond Hole 1 prototype
1. **Scrape** — downloads hole GIFs + scorecard data
2. **Extract** — crops illustration, removes legend, upscales to 1024×
3. **Detect Tees** — HSL color matching, 72/72 tees found
4. **Classify Zones** — 11-zone HSL classification, majority filter
5. **Generate Terrain** — procedural heightmap with slope, noise, zone modifiers
6. **Export** — manifest, heightmap, texture, anchors, zones, bunkers, greens, water, fairway-contours, zone-contours

### GUI (`Tools/UHoleLite/app/`, port 4174)
- Launch: `Tools/UHoleLite/Launch GUI.bat`
- Features: hole navigation, orientation controls, view modes, draggable tee markers, zone painting, brush tool, Ctrl+Z undo, zoom/pan

### Unity Importer
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
- Menu: `GOLFIN > Import Hole (Lite) > Hole 01..18 + All 18`
- Key methods: `ApplySplatmap`, `CreateFlatZoneMeshes`, `CreateFairwayMesh`, `CreateFringeRing`, `CreateGradientBorderRing`, `CreateRaisedMesh`, `CreateZoneMeshes`, `CreateGreenMeshes`, `CreateWaterMeshes`

### Key Files
- Pipeline: `Tools/UHoleLite/scripts/` (7 scripts + lib/ + diagnose-fairway.mjs)
- Config: `Tools/UHoleLite/config/lomond-country-club.json`
- Output: `Tools/UHoleLite/output/lomond-country-club/`
- GUI: `Tools/UHoleLite/app/`
- Docs: `Docs/BUNKER_RESEARCH.md`, `Docs/WATER_FINDINGS.md`

### DEM Heightmap Pipeline (2026-04-09)

**GeoAlign tool** (`Tools/GeoAlign/`) — web app for geo-aligning hole
illustrations to GSI satellite imagery via control points + affine transform.
Hole 1 aligned with 6 control points, mean residual 0.8m.

**Quadratic surface fit (v4):** `height = a*x² + b*y² + c*x*y + d*x + e*y + f`
- ONE surface fit to all playable zones (fairway, green, tee, bunker, rough, semi-rough, cart path)
- Captures valleys, ridges, two-tier terrain while remaining mathematically smooth
- Playable zones = pure quadratic surface (zero DEM detail)
- Trees/OB/background = quadratic + 75% DEM residual (5 blur passes) for mountainous terrain
- DEM source: GSI DEM5A tiles (z15, ~5m/pixel lidar)
- Resolution: 1025×1025 to match Unity terrain directly

**Water shader:** `URPWater/Standard` at `Assets/Art/3D/Props/URPWater/`.
Color mode Colors, single normal map, edge fade ON, foam/caustics/waves OFF,
normal speed zeroed, low specular. Per-vertex terrain-following with -0.1m
offset, underwater depression only (no shore lip).

**Cart path spine mesh:** Contour polygon → split at farthest points → resample
both edge chains → average = centerline spine. Unity extrudes fixed-width strip
along spine, sampling terrain height at each vertex pair. Open polyline (not closed).

**Mountain backdrop:** Single `Mountains.fbx` instance (pre-built ring mesh),
scale 0.7, Y position 30. `Mountain.png` texture with transparency.

### Key Terrain Values
- Overlay y-offsets: fairway 0.08m, tee 0.08m, fringe 0.10m, tee border 0.06m, cart path 0.04m
- Bunker terrain hole cut: 90% scale (works for ≥3m radius bunkers)
- DEM residual: 75% for trees/OB/background, 5 blur passes
- Quadratic surface fit uses only playable zone DEM samples

### Known Issues
- ~~**Bunker 7** (2.2m radius): terrain poke-through at rim corners~~: ✅ Fixed — increased heightmap from 1025 to 2049 (holes grid 2048×2048, ~0.3m/cell). Small bunkers (shorterAxis<7m) also get depth scaled relative to size. Shingle overlap (v7) kept as belt-and-suspenders for small bunkers.
- ~~**Water broken**~~: ✅ Fixed — water mesh now samples terrain height at each contour vertex instead of using fixed Y=0.05.



---

## Phase K — 3D Golf Course Prototype ✅ MILESTONE COMPLETE

Official map → control points → affine transform → heightmap + aerial texture + anchors → Unity scene → walkable terrain

### Key Files
- `Tools/UHole/docs/TASK.md` — UHole task instructions
- `Docs/TellCode.md` — Unity task instructions
- `Assets/Scripts/Editor/CourseImporter/HoleImporter.cs` — Unity importer (satellite pipeline)
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — Unity importer (map pipeline)
- `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs` — JSON data classes

---

## Lomond Country Club Data

- **Name:** ローモンドカントリー倶楽部
- **Location:** 2570-3 Ryoocho, Kameyama, Mie 519-0222, Japan
- **Verified center:** lat 34.91318, lon 136.44164
- **Holes:** 18, Par 72
- **Hole 1:** Par 5, 531yd (Back), HDCP 9

---

## Key Lessons (accumulated)

### Unity
- `GameObject.Find` misses inactive — use `Resources.FindObjectsOfTypeAll` filtered by `go.scene.isLoaded`
- Graphic Raycaster must accompany any Canvas; Raycast Target off on non-interactive Images
- Figma ÷ 1.4 = Unity TMP font size
- `TerrainLayer` assets must be saved via `AssetDatabase.CreateAsset` inside `Assets/`
- `AssetDatabase.FindAssets` does fuzzy matching — use `FindTextureExact` helper
- URP: use `Universal Render Pipeline/Lit` with `_Smoothness` (not `Standard`/`_Glossiness`)
- Matte mask map (A=0) prevents plastic sheen on terrain

### UHole / UHole Lite
- GSI tile server: `cyberjapandata.gsi.go.jp/xyz/seamlessphoto/{z}/{x}/{y}.jpg`
- Zone grid resolution (2596×3124) is much higher than texture (794×956) — RDP epsilon must account for this
- Moore neighborhood trace > naive 8-connected walk for border tracing
- Ear-clip triangulation > centroid-fan for concave shapes
- Mesh overlays > splatmap painting for smooth zone edges

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
- `Docs/BUNKER_RESEARCH.md`, `Docs/WATER_FINDINGS.md`
- `CLAUDE.md` — Claude Code session rules + project architecture
