# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Tee Markers (Replace Raised Mesh with FBX Props)

**Goal:** Remove the tee raised mesh approach. Replace debug anchor
cylinders with proper FBX tee marker props from
`Assets/Art/3D/Props/TeeMarkers/`. Two markers per tee, placed on the
terrain surface. Splatmap already handles the tee texture (zone 10 →
`T_Tee_Albedo`), so no terrain changes needed.

---

### Part A: Remove tee raised mesh

#### A1. Remove `CreateTeeMeshes()` call from `ImportLiteHole()`

Find and delete this block:
```csharp
EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating tees...", 0.56f);
CreateTeeMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);
```

#### A2. Delete `CreateTeeMeshes()` method entirely

#### A3. Delete tee data classes (`TeesFileData`, `TeeRegionData`) if they exist

#### A4. Remove tee-related constants

Delete `TeeHeight` and `TeeCollarScale` from the top of the class.

#### A5. Remove tee export from `export-hole.mjs`

Delete the `// --- Build tees.json ---` block from `exportHole()`.
Remove `tees_file` from the manifest object.
Remove `teeCount` from the return object and console.log line.

**Do NOT** revert the `extractZoneContours()` optional params
(`rdpEpsilon`, `smoothPasses`) — those are still useful for future zones.

---

### Part B: Replace anchor debug cylinders with FBX tee markers

#### B1. Create green tee marker

There's no green FBX marker. Duplicate the white one:
- Copy `MESH_WhiteTee.fbx` → keep same mesh
- Create a new material `MAT_GreenTee.mat` in
  `Assets/Art/3D/Props/TeeMarkers/Materials/`
- Use the same shader as the other tee materials (check `MAT_WhiteTee.mat`)
- Set color to green: `new Color(0.2f, 0.6f, 0.2f)` (adjust if the
  other materials use a texture instead of flat color — match the pattern)

#### B2. Update `PlaceAnchorMarker()` to use FBX markers

Replace the current method. For tee anchors (`type` contains "tee"),
instantiate the FBX mesh prefab instead of `CreatePrimitive(Cylinder)`.

Color mapping:
- `tee_back` → `MESH_BlueTee` + `MAT_BlueTee`
- `tee_regular` → `MESH_WhiteTee` + `MAT_GreenTee` (green material on white mesh)
- `tee_front` → `MESH_WhiteTee` + `MAT_WhiteTee`
- `tee_ladies` → `MESH_RedTee` + `MAT_RedTee`

For each tee anchor, place **2 markers** spaced ~3m apart perpendicular
to the tee-to-green direction. To determine perpendicular direction:
- Find the green anchor (or centroid of green zone) for forward direction
- Cross with up vector to get left/right
- Place marker A at position + 1.5m left, marker B at position + 1.5m right
- If no green direction can be determined, default to spacing along X axis

Each marker should:
- Load mesh: `AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/3D/Props/TeeMarkers/MESH_{Color}Tee.fbx")`
- Instantiate with `PrefabUtility.InstantiatePrefab()` or `Object.Instantiate()`
- Apply the correct material from `Assets/Art/3D/Props/TeeMarkers/Materials/MAT_{Color}Tee.mat`
- Position on terrain surface: `terrain.SampleHeight(pos)` + small Y offset (0.01m)
- Parent under `Anchors` root

For non-tee anchors (if any exist in the future), keep the current
cylinder debug marker approach.

#### B3. Naming convention

Name the GameObjects:
- `TeeMarker_back_L`, `TeeMarker_back_R`
- `TeeMarker_regular_L`, `TeeMarker_regular_R`
- `TeeMarker_front_L`, `TeeMarker_front_R`
- `TeeMarker_ladies_L`, `TeeMarker_ladies_R`

---

### Verification

1. Re-export Hole 1: `node scripts/export-hole.mjs lomond-country-club 1`
   - [ ] No `tees.json` generated (removed)
   - [ ] No errors, bunkers/greens/water still export

2. Re-import Hole 1 in Unity: `GOLFIN > Import Hole (Lite) > Hole 01`
   - [ ] No raised tee meshes in scene
   - [ ] 8 tee marker objects (2 per tee × 4 tees)
   - [ ] Blue markers at back tee, Green at regular, White at front, Red at ladies
   - [ ] Markers sitting on terrain surface
   - [ ] Markers spaced ~3m apart at each tee position
   - [ ] Tee area texture visible on terrain (splatmap unchanged)
   - [ ] Greens, bunkers, water unaffected
   - [ ] No console errors

3. Re-import Hole 12:
   - [ ] Tee markers + water + bunkers + greens all coexist
   - [ ] No console errors

### Do NOT

- Modify splatmap pipeline or zone textures
- Modify bunker, green, or water mesh generation
- Modify shore slope logic
- Remove `extractZoneContours()` optional params

---

## Previous Completed Tasks

✅ DONE: 2026-04-01 — Phase J: Bags Inventory Screen
✅ DONE: 2026-03-31 — Phase I2 Item Use → Club Selection Modal
✅ DONE: 2026-03-31 — Phase I1 Items Inventory
✅ DONE: 2026-03-27 — Phase H Balls Inventory
✅ DONE: 2026-03-26 — Phase G Character Compare stat diff labels
✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal
✅ DONE: 2026-03-26 — Phase E2 Club Repair One-Tap
✅ DONE: 2026-03-26 — Phase E3 Bag Selection Modal
✅ DONE: 2026-03-26 — Phase E3b Bags CSV + Data-Driven Bag Slots
✅ DONE: 2026-03-26 — Phase E4 Bag ↔ Club management
✅ DONE: 2026-03-26 — Phase F Level Up Modal polish (SP allocation UI)
✅ DONE: 2026-03-30 — Fix Club Filter Bar: 8→6 tabs + unified WEDGES
✅ DONE: 2026-03-30 — Fix filter button raycast targets
✅ DONE: 2026-04-06 — Phase K Steps 1-8: HoleImporter + HoleLiteImporter terrain pipeline
✅ DONE: 2026-04-07 — Phase K-Surface Validation: Test Terrain Layers + Test Zone Alignment debug tools
✅ DONE: 2026-04-07 — Phase K-Surface Task 1: Splatmap importer
✅ DONE: 2026-04-07 — V1 Bunker meshes (bounding-box bowls — dead end)
✅ DONE: 2026-04-07 — Bunker V2: contour export + flat terrain + contour mesh importer
✅ DONE: 2026-04-07 — Bunker V2 polish: Chaikin smoothing, closed-polygon RDP, 1025 heightmap res, sand glow fix
✅ DONE: 2026-04-07 — Green zone meshes: contour export, raised mesh, SurfaceMarker component
✅ DONE: 2026-04-07 — Green collar: semi-rough slope as separate mesh, collar/surface split
✅ DONE: 2026-04-07 — Phase 2 Water Zone Meshes: water contour export + basin mesh importer + transparent material
✅ DONE: 2026-04-07 — Morphological close for water fragments + re-export
✅ DONE: 2026-04-07 — Water tree absorption + dilate-only (replaced morphological close)
✅ DONE: 2026-04-07 — Fix water border gaps: rim expanded to 105%, terrain cut at 100%
✅ DONE: 2026-04-07 — Simplified water to flat plane: no basin, no terrain holes, opaque material
✅ DONE: 2026-04-07 — Water Option 2: Rasterized quad + alpha mask (pixel-perfect water boundaries)
✅ DONE: 2026-04-07 — Water SDF texture: signed distance field for smooth edges
✅ DONE: 2026-04-08 — Water Shore Slope: terrain depression near water edges via heightmap modification at import time
✅ DONE: 2026-04-08 — Tee Area raised mesh + collar (reverted — wrong approach)
✅ DONE: 2026-04-08 — Tee Markers: removed raised mesh, replaced debug cylinders with FBX tee marker props (2 per tee, 4 colors)
