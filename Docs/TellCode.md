# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Flag at Green Centroid

**Goal:** Place `Flag.fbx` at the centroid of each green on import.

**File:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

---

### What to do

In `CreateGreenMeshes()`, after each green mesh is created and parented,
instantiate the flag prop at the green centroid.

1. Load the flag prefab:
   ```csharp
   var flagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
       "Assets/Art/3D/Props/Flag/Flag.fbx");
   ```

2. For each green, after `meshGO.transform.SetParent(greensRoot.transform)`:
   ```csharp
   if (flagPrefab != null)
   {
       var flag = Object.Instantiate(flagPrefab);
       flag.name = $"Flag_{green.id}";
       // Position at green centroid, on top of the raised green surface
       float flagY = surfaceY + greenHeight;
       flag.transform.position = new Vector3(centroidX, flagY, centroidZ);
       flag.transform.SetParent(greensRoot.transform);
   }
   ```

   `surfaceY` and `greenHeight` and `centroidX`/`centroidZ` are already
   computed in the foreach loop — just use them.

3. If the flag looks too big or small, check the FBX scale. You may need
   to adjust `flag.transform.localScale`. Leave a `// TODO: tune scale`
   comment if unsure.

4. Load the flag material and apply it:
   ```csharp
   var flagMat = AssetDatabase.LoadAssetAtPath<Material>(
       "Assets/Art/3D/Props/Flag/MAT_Flag.mat");
   ```
   Apply to all renderers on the flag if the FBX doesn't auto-assign it.

5. **Add the hole cup** at the same position as the flag, slightly
   recessed into the green surface.

   - Create a flat disc mesh (or use `GameObject.CreatePrimitive(PrimitiveType.Cylinder)`
     scaled to near-zero height).
   - Diameter: 0.108m (10.8cm — regulation golf cup is 4.25").
   - If using a cylinder: scale `(0.108f, 0.001f, 0.108f)`, position
     at `flagY - 0.005f` (slightly below green surface).
   - Apply `MAT_Hole.mat` from
     `Assets/Courses/Materials (Shared by courses)/MAT_Hole.mat`
   - Name: `Hole_{green.id}`
   - Parent under `greensRoot`
   - Remove the collider from the cylinder primitive (it's just visual).

### Verification

- [ ] Re-import Hole 1 — flag appears on the green
- [ ] Flag is at green centroid, sitting on the green surface (not floating/buried)
- [ ] Re-import Hole 12 — flag + water + bunkers + tee markers all coexist
- [ ] No console errors

### Do NOT

- Modify green mesh generation
- Modify any other pipeline
- Add flag to anchors.json or export pipeline (future: green editor will handle pin placement)

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
✅ DONE: 2026-04-08 — Water Shore Slope: terrain depression near water edges
✅ DONE: 2026-04-08 — Tee Area raised mesh + collar (reverted — wrong approach)
✅ DONE: 2026-04-08 — Tee Markers: FBX props replacing debug cylinders, green mat created
✅ DONE: 2026-04-08 — Flag at green centroid + hole cup (regulation 0.108m cylinder disc)
