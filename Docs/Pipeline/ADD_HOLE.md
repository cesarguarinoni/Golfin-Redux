# Adding a New Hole — End to End

> Canonical procedure for taking a hole from satellite imagery to a fully playable, sim-ready Unity scene.
> Last updated: 2026-04-25 (post-baked-data-sim pivot).

---

## TL;DR

For each hole, three artifacts must exist before the sim will run on it:

1. **`Assets/Golf/Courses/<course>/Generated/Hole_XX_Geo.unity`** — the visible scene (terrain + zone meshes + cart paths + water + props).
2. **`Assets/Resources/HoleData/Hole_XX/heightmap.bytes`** — Q16.16 fixed-point height grid the sim uses for ball Y.
3. **`Assets/Resources/HoleData/Hole_XX/zones.json`** — polygon classifier + per-zone Y offsets the sim uses to know what surface the ball is on.

Steps 1 → 2 → 3 must be done **in that order**. Re-baking heightmap or zones for a hole that's already shipped is safe and idempotent.

---

## Pipeline overview

```
UHoleGeo (Node CLI/GUI)        Unity (Editor)
─────────────────────────      ──────────────────────────────────────
1. fetch-satellite             4. Import > Geo > Normal > Import Hole XX Geo
2. classify-zones              5. Import > Bake Physics Heightmap > Bake Hole XX
3. generate-terrain            6. GOLFIN > Tools > Bake Zone JSON (Active Hole)
   export-hole                 7. (Manual) Place props, cart paths, bridges
                               8. (Manual) Smoke test in PhysicsLab
```

Steps 1–3 run outside Unity and produce the export package at
`Tools/UHoleGeo/output/<course>/export/hole-XX/`.

Steps 4–6 run inside Unity and produce the three sim-required artifacts above.

Steps 7–8 are manual polish + verification.

---

## Step 1 — UHoleGeo CLI: produce the export package

**Tool:** `Tools/UHoleGeo/` (newer pipeline; UHoleLite is the legacy variant — do not start new holes there).
**Who runs it:** Cesar.
**Why not Code:** the GUI lets you visually verify zone classification before exporting; CLI-only runs lose that check.

### 1a. From the GUI (recommended for new holes)

```
Double-click Tools/UHoleGeo/Launch GUI.bat
```

The GUI walks you through fetching satellite tiles, classifying zones (greens, bunkers, fairway, water, cart paths), and generating the terrain DEM. Inspect each step's preview before advancing.

### 1b. From the CLI (re-export of an already-classified hole)

```powershell
cd Tools/UHoleGeo
node scripts/run-all.mjs lomond-country-club 7        # one hole
node scripts/run-all.mjs lomond-country-club --all     # all 18
```

`run-all.mjs` runs `classify-zones → generate-terrain → export-hole` in sequence.

### Output

```
Tools/UHoleGeo/output/<course>/export/hole-XX/
  ├─ heightmap.raw          ← raw float terrain, source of truth
  ├─ zones.json             ← UHoleGeo's zone classification (NOT the sim's zones.json)
  ├─ greens.json
  ├─ bunkers.json
  ├─ fairway-contours.json
  ├─ cart-paths.json
  ├─ water.json
  ├─ tree-zones.json
  ├─ anchors.json           ← tee + green anchors with lat/lon
  ├─ texture.png            ← satellite imagery for the ground texture
  └─ hole-manifest.json
```

Note: the `zones.json` written here is UHoleGeo's intermediate format. The **sim's** `zones.json` (the polygon classifier) is generated later in Step 4 from the imported Unity scene.

---

## Step 2 — Unity import: build `Hole_XX_Geo.unity`

**Tool:** `Import` menu in Unity (`HoleGeoImporter.cs`).
**Who runs it:** Cesar.
**Why not Code:** the importer modifies a scene file Cesar typically wants to inspect mid-build, and Unity's progress dialogs require focus.

```
Unity menu:
  Import > Geo > Normal > Import Hole XX Geo
```

This reads `Tools/UHoleGeo/output/<course>/export/hole-XX/` and produces:

```
Assets/Golf/Courses/<course>/Generated/Hole_XX_Geo.unity
```

What the importer does:

- Builds a Unity Terrain from `heightmap.raw` (with water shore depression, overlay depression, and DEM-blur smoothing baked in).
- Generates **flat CDT overlay meshes** for each zone type (greens, bunkers with bowls, fairway+fringe+tee-border as one parent submeshed mesh, tee pads, cart paths along splines, water).
- Tags every zone GameObject with `Course.SurfaceMarker.surfaceType`.
- Tags every zone GameObject with `Physics.Runtime.SurfaceMarker.Type` (this is what `BakeZoneJsonTool` reads in Step 4).
- Places anchor markers (tee + flag) and tee marker prefabs.
- Applies the satellite texture as the terrain albedo.
- Saves the scene.

There is also `Import > Geo > Flat > Import Hole XX Geo (Flat)` which produces a `_Flat` variant for testing pipeline orthogonality. Not needed for normal play.

---

## Step 3 — Bake the physics heightmap

**Tool:** `Import > Bake Physics Heightmap` menu (`PhysicsHeightmapBaker.cs`).
**Who runs it:** Cesar (single hole) or Code (all holes, if you want a one-shot script — see "Automation candidate" below).
**Why it's separate from Step 2:** the heightmap is derived from the *final* Unity Terrain (after all overlay depressions, shore depressions, and zone offsets have been applied). It must be baked AFTER Step 2 because the importer is what produces the depressed-and-smoothed terrain.

```
Unity menu:
  Import > Bake Physics Heightmap > Bake Hole XX        # single hole
  Import > Bake Physics Heightmap > Bake Current Hole   # active scene
  Import > Bake Physics Heightmap > Bake All Holes      # batch
```

What it does:

- Reads the `Hole_XX_Geo.unity` Terrain.
- Quantizes Y values to Q16.16 fixed-point.
- Writes a 36-byte header (`GHM1` + version + res + sizeX/Z + posX/Y/Z + format) followed by raw fixed-point grid.

Output:

```
Assets/Resources/HoleData/Hole_XX/heightmap.bytes
```

The sim's `BakedHeightProvider` reads this directly via `HeightmapLoader` at runtime.

---

## Step 4 — Bake the zone JSON

**Tool:** `GOLFIN > Tools > Bake Zone JSON` menu (`BakeZoneJsonTool.cs`).
**Who runs it:** Cesar (single hole) or Code (all holes).
**Why it must run AFTER Step 2:** the tool walks the Unity scene's zone-mesh GameObjects, extracts boundary contours from each `MeshFilter`, projects to XZ, and reads `Physics.Runtime.SurfaceMarker.Type` to get the surface type. No imported scene = nothing to walk.

```
Unity menu:
  GOLFIN > Tools > Bake Zone JSON (Active Hole)        # one hole — must be loaded
  GOLFIN > Tools > Bake Zone JSON (All Holes)          # batch
```

What it does:

- For every GameObject with both a `SurfaceMarker` and a `MeshFilter`: extracts the mesh's boundary polygon(s), pools all triangles into a per-zone `ZoneMesh` (used by the sim for exact barycentric Y interpolation).
- Reads the Terrain's OB-named layer alphamap and packs it into a base64 bit-mask.
- Writes the result as JSON.

Output:

```
Assets/Resources/HoleData/Hole_XX/zones.json
```

The sim's `BakedZoneClassifier` reads this at runtime to answer "what surface is the ball on?" via point-in-polygon, with priority order Green > Sand > Water > GreenCollar > Tee > CartPath > Fairway > Rough.

---

## Step 5 — Manual polish (per hole, as needed)

Things that are NOT part of the automated pipeline and require manual placement in Unity:

- **Tee marker prefabs.** The importer places generic tee markers; for showcase holes, swap to styled ones from `Assets/Art/3D/Props/TeeMarkers/`.
- **Bridges.** Use the Bridge Placement Tool (placed bridges are persisted via `ManualSceneSnapshot` so a re-import doesn't blow them away).
- **Trees / detail layers.** Tree placer + manual brush. These are persisted via `ManualSceneSnapshot` — capture the snapshot AFTER placement so the data survives re-imports.
- **Lighting / skybox.** Per-hole RenderSettings should already be sane post-import; double-check fog, ambient, reflection probes if anything looks off.

If you change anything that affects collision geometry (rare — usually only bridges add new colliders), **re-run Steps 3 and 4** for that hole. The sim won't see new colliders unless they're in the heightmap or zones.json.

---

## Step 6 — Smoke test

**Tool:** PhysicsLab + LabScaffold + Hole Picker.

```
1. Open Assets/Scenes/Physics/LabScaffold.unity
2. GOLFIN > Physics Lab > Hole Picker → Load Hole XX
3. Enter Play mode
4. Use the "Place Ball" dropdown to drop the ball on each surface category present:
   - Tee, Green, every Bunker, every Fairway segment, near each Water body
5. For each placement: fire a default driver (or putter on green) — confirm:
   - Ball settles without falling through.
   - Surface classification matches what you'd expect (check console DiagPerStep if uncertain).
6. Run the regression test: Window > General > Test Runner > PlayMode >
   BakedPivotRegressionTests > RegressionTest_*  (24/24 must PASS).
```

If anything fails, grab a per-step CSV with `BallSimulation.DiagPerStepEnabled = true` and surface to Architect.

---

## Who does what — split between Cesar and Code

| Step | Tool | Who | Why |
|------|------|-----|-----|
| 1 | UHoleGeo GUI / CLI | Cesar | Visual verification of classification |
| 2 | `Import > Geo > Normal > Import Hole XX Geo` | Cesar | Inspect scene mid-import |
| 3 | `Import > Bake Physics Heightmap > Bake Hole XX` | Cesar (one-off) or Code (batch) | Mechanical |
| 4 | `GOLFIN > Tools > Bake Zone JSON (Active Hole)` | Cesar (one-off) or Code (batch) | Mechanical |
| 5 | Manual placement (props, bridges) | Cesar | Aesthetic judgment |
| 6 | PhysicsLab smoke test | Cesar (eyes-on) + Code (regression test) | Visual + automated |

**Code is allowed to:**
- Run Steps 3 and 4 on any combination of holes (e.g. "rebake all 18").
- Run the regression tests in Step 6.
- Write a one-shot menu item that chains Step 3 + Step 4 (see "Automation candidate" below) — but this is an optimization, not required.

**Code must NOT:**
- Run Step 1 unsupervised. Bad classification is hard to detect from CLI output alone; the GUI preview is the safety net.
- Run Step 2 without Cesar's nod. The importer overwrites the existing `Hole_XX_Geo.unity` and any in-progress manual edits in that scene are lost.
- Skip Step 3 or Step 4. The sim relies on both files; missing either means falling back to scene raycasts (the very thing the baked-data pivot was meant to eliminate).

---

## Automation candidate (not built, low priority)

A single menu item `GOLFIN > Tools > Rebake Sim Data (Active Hole)` that runs Step 3 + Step 4 in sequence on whatever Hole_XX_Geo scene is currently active. Saves ~5 seconds per hole when iterating. Spec it if/when it starts to feel tedious.

---

## File / directory reference

| Path | Purpose |
|------|---------|
| `Tools/UHoleGeo/` | Active satellite-to-export pipeline (Node CLI + GUI) |
| `Tools/UHoleLite/` | Legacy zone-overlay pipeline (do not start new holes here) |
| `Tools/UHoleGeo/output/<course>/export/hole-XX/` | UHoleGeo export package |
| `Assets/Golf/Courses/<course>/Generated/Hole_XX_Geo.unity` | Imported Unity scene |
| `Assets/Resources/HoleData/Hole_XX/heightmap.bytes` | Sim heightmap |
| `Assets/Resources/HoleData/Hole_XX/zones.json` | Sim zone classifier |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` | Step 2 |
| `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` | Step 3 |
| `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` | Step 4 |
| `Assets/Scripts/Physics/Runtime/Baked/BakedHeightProvider.cs` | Sim consumer of Step 3 output |
| `Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs` | Sim consumer of Step 4 output |
| `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs` | The 24-shot regression gate |

---

## Known pitfalls

- **Re-importing a hole (Step 2) wipes manual edits in `Hole_XX_Geo.unity`.** Capture a `ManualSceneSnapshot` before re-importing, restore after.
- **Step 4 uses `Physics.Runtime.SurfaceMarker`, not `Course.SurfaceMarker`.** Both are present post-import; the importer adds the Physics one explicitly. If a hole was imported pre-2026-04-25 with an older importer, Course markers may exist without Physics markers — run `GOLFIN > Tools > Sync Physics Surface Markers` first (legacy migration tool, kept as a backward-compat alias).
- **Heightmap depression band (known M2 issue).** Inside the dilated mesh boundary "ring", the heightmap and the visible mesh disagree by up to 40 cm. Sim height agreement is 100% within 5 cm (mean 0.45 cm) on Hole_01, so this is rarely a problem in play, but if a hole shows a visibly-floating or visibly-sunk ball at rest, see `Docs/Diagnostics/baked-pivot/M2-height-agreement.md` for the known divergence histogram and remediation paths.
- **`Hole_XX_Geo_Flat.unity` is for pipeline testing, not play.** Don't bake heightmaps or zones for `_Flat` scenes — the data goes to a separate path and is unused by the sim.

---

## Quick checklist for adding Hole 19+ to a future course

```
[ ] 1. UHoleGeo: classify + generate terrain + export
[ ] 2. Unity: Import > Geo > Normal > Import Hole 19 Geo
[ ] 3. Unity: Import > Bake Physics Heightmap > Bake Hole 19
[ ] 4. Unity: GOLFIN > Tools > Bake Zone JSON (Active Hole)
[ ] 5. Manual: tee markers, bridges, trees, lighting check
[ ] 6. PhysicsLab smoke test: 5 placements + regression test pass
```
