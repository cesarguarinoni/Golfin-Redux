# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-04-13

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

## Active Work — Course Visual Polish

### OB Feature Export Fix + Cart Path Overlap Avoidance (2026-04-13) ✅
- Fixed export pipeline: trees/cart paths in OB zones were lost because merged grid gives OB priority. Now uses separate `trees_mask` and `cart_path_mask` overlays.
- Trees: +60,896 pixels recovered (277K → 338K)
- Cart path skeleton clipping: extended tee-only clipping to exclude fairway (1), bunker (6), tee (10) using `terrain_grid` (base zones)
- Fixed bug: `cart_path_mask` stamping overwrote original zones, breaking tee clipping
- Spine nudging (`nudgeSpinesFromContours`): iterative geometry-based push that ensures 2.5m-wide strip doesn't overlap fairway/bunker/tee contour polygons
  - Two-case: center-inside → push to boundary + clearance; edge-only → small perpendicular nudge
  - 10 passes with progressive smoothing decay
  - 15/18 holes fully clean; 3 holes have ≤3 sub-1m residual overlaps

### Smooth Play↔Non-Play Terrain Transition (2026-04-13) ✅
- Replaced linear blend (Lerp smoothed↔raw via blendFactor) with boundary-height propagation
- Non-play terrain now starts at adjacent play-area height (no cliff)
- Smoothstep ramp from boundary height to Gaussian-blurred DEM over TransitionCells
- Play area remains untouched (raw DEM detail preserved)

### OB↔Rough Transition (2026-04-13) ✅
- OB texture changed to reuse T_Rough with darker/yellower tint via `diffuseRemapMax`
- OB tile size changed to 10f (vs 8f for rough) for visual variation
- 4px splatmap boundary blend (chamfer distance + smoothstep crossfade, 40% mix)
- Smooth OB button added to UHole Lite GUI (vectorize → RDP → Chaikin → rasterize)

### Cart Path Depression Fix (2026-04-13) ✅
Three-strategy fix for visible terrain cliff at cart path edges:
1. **Deeper inset (0.50m)** — depression polygon starts well inside road edge (was 0.10m)
2. **Smoothstep ramp** — distance-based gradual slope from edge (0%) to center (100% drop) instead of flat 0.40m chop
3. **Splatmap edge painting** — 2px strip of cart path texture (layer 6) painted on terrain at road edges

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
4. **CDT triangulation** — Constrained Delaunay (BurstTriangulator) for fairway/tee/cart path meshes

### Terrain Depression System
- **Overlay depression:** 0.40m drop under overlay meshes to prevent z-fighting
- **Depression inset:** 0.20m inward from contour edge (fairway/tee default)
- **Cart path depression:** Spine-based polygon, inset from road edge (being improved — see Active Work)
- **Water depression:** Separate system, underwater cells pushed below water mesh surface
- **Shore slope:** Chamfer distance from water boundary, configurable radius + depth

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

### On the Horizon
- Cart path depression fix (in progress)
- Visual polish pass: textures, water shader (long-term), small bunker lip (~0.13m above terrain)
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
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` (~182KB, ~3900 lines)
- Menu: `GOLFIN > Import Hole (Lite) > Hole 01..18 + All 18`
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
| 9 | T_WaterBed_DarkBlue (generated) | Water bed — solid dark blue under water meshes |

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
- Playable zones = pure quadratic surface (zero DEM detail)
- Trees/OB/background = quadratic + 75% DEM residual (5 blur passes) for mountainous terrain

**Cart path spine mesh:** Contour polygon → split at farthest points → resample
both edge chains → average = centerline spine. Unity extrudes fixed-width strip
along spine, sampling terrain height at each vertex pair.

**Mountain backdrop:** Single `Mountains.fbx` instance, scale 0.7, Y=30.

### Key Terrain Values
- Heightmap: 2049×2049 (~0.3m/cell for holes grid)
- Overlay y-offsets: fairway 0.01m, tee 0.01m (CDT), fringe 0.012m, tee border 0.008m, cart path 0.01m
- Depression: 0.40m under overlays, 0.20m inset (fairway/tee), 0.50m inset (cart path — new)
- Bunker terrain hole cut: 90% scale (large), shingle overlap v7 (small <7m)
- DEM residual: 75% for trees/OB/background, 5 blur passes

---

## Tree Placement System (2026-04-10) ✅

- Export tree-zones.json from UHole Lite + TreePlacer.cs in Unity
- Mixed mode: terrain trees + standalone GameObjects (particles, complex hierarchy)
- Tree Settings editor window (Trees > Tree Settings)
- Save/Load Presets + session auto-persistence
- Directional light & shadows: soft shadows, Mixed bake, 100m distance

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
- `Docs/BUNKER_RESEARCH.md`, `Docs/WATER_FINDINGS.md`
- `CLAUDE.md` — Claude Code session rules + project architecture
