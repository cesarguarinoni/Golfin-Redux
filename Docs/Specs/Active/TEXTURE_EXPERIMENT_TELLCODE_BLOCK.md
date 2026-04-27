## ➡️ NEXT (parallel to Phase 8) — Texture Experiment

**Spec:** `Docs/Specs/Active/TEXTURE_EXPERIMENT.md`
**Branch:** any (this is non-load-bearing — no production files touched)

**One-line summary:** Two-step experiment. Step 1 downloads CC0 PBR textures and resizes them to mobile-friendly 1024/512 in `Assets/Courses/Textures_Experimental/`. Step 2 clones Hole_01 (scene + TerrainData + TerrainLayers + overlay materials) into `Hole_01_Experimental_Geo.unity` with the new textures wired in. Production hole stays untouched. Cesar then compares side-by-side.

**Steps for Code:**

**Step 1 — texture generation (Node):**
1. `cd Tools/TextureExperiment && npm install`
2. `node prepare-textures.mjs`
3. Verify `Assets/Courses/Textures_Experimental/` has 25 texture files + README.md
4. No `.asset` / `.mat` / scene file touched — confirm with `git status`

**Step 2 — experimental Hole_01 clone (C# editor script, NEW):**
5. Write `Assets/Scripts/Editor/CourseImporter/BuildExperimentalHole01.cs` per spec section "Step 2"
6. Adds menu item `GOLFIN > Tools > Build Hole_01 Experimental Clone`
7. Run via Unity MCP `menu-item-call`
8. Outputs:
   - `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity`
   - `Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental/` (TerrainData + TerrainLayers + Materials clones)
   - `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md`
9. Verify production `Hole_01_Geo.unity` is unmodified (`git status` shows it untouched)

**Hard rules:**
- No edits to production `Hole_01_Geo.unity`, `Textures_2025(JPG)/`, or any production TerrainLayer/Material under `Data/hole-01-flat/` or sibling production hole-data dirs.
- No edits to `HoleGeoImporter.cs` or any other importer code.
- No splatmap or mask map regeneration.
- If a source URL 404s in Step 1, log and skip — do NOT substitute alternatives.
- If `sharp` install fails on Windows, surface to Cesar — don't try alternative image libs.
- If the editor script can't safely identify a TerrainLayer or overlay material in 2 attempts, surface to Architect with the specific case.

✅ DONE: [date]
- Step 1: [N textures generated, total folder size, any failed sources]
- Step 2: [scene path, TerrainLayer count duplicated, overlay material count duplicated, any warnings from clone report]

---

