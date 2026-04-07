# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Tee Area Auto-Mesh

**Goal:** Export tee box contours and import as slightly raised meshes
in Unity, similar to greens but lower and with less aggressive smoothing.

---

### Part A: Export (`Tools/UHoleLite/scripts/export-hole.mjs`)

#### A1. Add optional params to `extractZoneContours()`

Change the signature from:
```javascript
function extractZoneContours(zonesData, terrainMeta, targetZone, minPixels = 8)
```
to:
```javascript
function extractZoneContours(zonesData, terrainMeta, targetZone, minPixels = 8, rdpEpsilon = 2.0, smoothPasses = 2)
```

Then replace the hardcoded values in the function body:
- `const RDP_EPSILON = 2.0;` → use the `rdpEpsilon` parameter
- `smoothPolygon(simplified, 2)` → `smoothPolygon(simplified, smoothPasses)`

#### A2. Add tee export to `exportHole()`

After the greens export block and before the water export block, add:

```javascript
  // --- Build tees.json ---
  const tees = extractZoneContours(zonesData, terrainMeta, 10, 15, 1.5, 1);
  // zone 10 = tee_box, min 15px, RDP 1.5 (keep squarer shape), 1 Chaikin pass (smooth but not round)

  const teesOutput = {
    schema_version: '1.0.0',
    hole_number: holeNumber,
    tee_count: tees.length,
    height_m: 0.08,
    tees: tees,
  };

  fs.writeFileSync(
    path.join(exportDir, 'tees.json'),
    JSON.stringify(teesOutput, null, 2),
    'utf-8'
  );

  if (tees.length > 0) {
    const contourStats = tees.map(t =>
      `#${t.id}: ${t.contour.length}pts`
    ).join(', ');
    console.log(`  Tee contours: ${contourStats}`);
  }
```

#### A3. Update manifest

Add `tees_file: 'tees.json'` to the manifest object (after `greens_file`).

#### A4. Update export result

Add `teeCount: tees.length` to the return object and update the
console.log line to include tee count.

---

### Part B: Import (`Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`)

#### B1. Add tunable constants

Near the existing `ShoreRadius` / `ShoreDepthMeters` statics:

```csharp
/// <summary>Height of tee surface above terrain in meters.</summary>
public static float TeeHeight = 0.08f;
/// <summary>Scale factor for tee collar (1.0 = no collar beyond contour).</summary>
public static float TeeCollarScale = 1.05f;
```

#### B2. Add `CreateTeeMeshes()` method

Model this after `CreateGreenMeshes()` but with these differences:
- Uses `tees.json` instead of `greens.json`
- Uses `TeeHeight` (0.08m) instead of `greenHeight` (0.15m)
- Uses `TeeCollarScale` (1.05) instead of `greenCollarScale` (1.08)
- Uses `T_Tee_Albedo` texture for the surface (fall back to
  `T_Green_Albedo` if not found — tee grass is similar)
- Uses `T_Semirough_Albedo` for collar (same as green collar)
- SurfaceMarker: `SurfaceType.Tee` (if it exists, otherwise
  `SurfaceType.Fairway` as closest match — add a NOTE)
- Terrain holes: cut at 95% of collar scale (same as greens)

Use `CreateRaisedMesh()` — it already handles contour, collar rings,
surface mesh, and colliders. Just pass the tee-specific params.

#### B3. Add data classes

```csharp
[System.Serializable]
private class TeesFileData
{
    public string schema_version;
    public int hole_number;
    public int tee_count;
    public float height_m;
    public TeeRegionData[] tees;
}

[System.Serializable]
private class TeeRegionData
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;
    public LocalCoord center_local;
}
```

If `ContourPoint` and `LocalCoord` already exist (from bunker/green
data classes), reuse them.

#### B4. Hook into `ImportLiteHole()`

Add a progress bar step and call `CreateTeeMeshes()` after greens,
before water:

```csharp
EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating tees...", 0.56f);
CreateTeeMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);
```

#### B5. Check SurfaceType enum

Look at `Golfin.Course.SurfaceType` — if `Tee` exists, use it. If not,
add it. If you'd rather not modify the enum right now, use `Fairway` and
leave a `// TODO: add SurfaceType.Tee` comment.

---

### Verification

1. Re-export Hole 1: `node scripts/export-hole.mjs lomond-country-club 1`
   - [ ] Console shows `Tee contours: #1: Npts, #2: Npts, ...`
   - [ ] `tees.json` exists in export folder with contour data
   - [ ] Existing bunker/green/water export unchanged

2. Re-import Hole 1 in Unity: `GOLFIN > Import Hole (Lite) > Hole 01`
   - [ ] Tee areas appear as slightly raised platforms
   - [ ] Edges are smooth (no jagged staircase)
   - [ ] Shape is squarer than greens (less rounded)
   - [ ] Each tee has SurfaceMarker component
   - [ ] Collar slope visible around tee edges
   - [ ] Greens, bunkers, water still work

3. Re-import Hole 12 (has water):
   - [ ] Tees + water + bunkers + greens all coexist
   - [ ] No console errors

### Do NOT

- Modify `traceBorder()`, `simplifyPolygon()`, `smoothPolygon()`, `ensureCCW()`
- Modify bunker, green, or water mesh generation
- Modify the splatmap or heightmap pipeline
- Modify shore slope logic

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
