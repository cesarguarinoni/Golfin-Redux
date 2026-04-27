## ➡️ ACTIVE — Texture Experiment Phase 2 (revision)

**Spec:** `Docs/Specs/Active/TEXTURE_EXPERIMENT.md`
**Branch:** any (this is non-load-bearing — no production files touched)
**Replaces:** the previous "NEXT — Texture Experiment" pointer; supersedes Phase 1 outputs.

**Why a Phase 2:** Phase 1 ran end-to-end but produced 7 specific defects (rough brown, semirough too vivid, greens/bunkers/tee-borders unchanged, tees identical to fairway, everything flat). Root causes:
1. Wrong texture sources for rough/semi-rough/tee
2. Clone script only caught per-hole `MAT_T_*` materials and missed the SHARED `MAT_Bunkers` / `MAT_Green` / `MAT_Fringe` / `MAT_Tee` etc. in `Assets/Courses/Materials (Shared by courses)/`
3. Normal map JPGs were imported with `textureType: 0` (Default) and `sRGBTexture: 1` — Unity reads them as color, not normals → flat shading

**One-line summary:** Two parallel tracks. Track A swaps source images for Rough (→ ambientCG Grass005), Semi-rough (→ Grass002 darkened), and Tee (→ Grass001). Track B fixes the clone script to (i) walk ALL MeshRenderers, (ii) catch shared MAT_* materials in addition to per-hole MAT_T_* ones, (iii) duplicate them to `Materials (Shared by courses)/Experimental/`, (iv) set `textureType: NormalMap` + `sRGBTexture: false` on every `_Normal.jpg`. Then tear down Phase 1 outputs and re-run end-to-end.

**Steps for Code:**

**Step 0 — clean up Phase 1:**
- Delete `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/` (folder)
- Delete `Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental/` (folder)
- Delete `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md`

**Track A — Texture sources:**
1. Edit `Tools/TextureExperiment/manifest.json` per spec section A.1 (rough → Grass005, semi-rough → Grass002 −10%, tee variants → Grass001)
2. `cd Tools/TextureExperiment && node prepare-textures.mjs`
3. Verify all 25 textures present, T_Rough_Albedo is visibly green wild grass

**Track B — Clone script:**
4. Update `Assets/Scripts/Editor/CourseImporter/BuildExperimentalHole01.cs` per spec sections B.1–B.6:
   - B.1: After Track A, set `textureType=NormalMap`, `sRGBTexture=false` on all `Textures_Experimental/*_Normal.jpg`
   - B.2: Walk EVERY MeshRenderer in the duplicated scene; iterate ALL `sharedMaterials`; catch BOTH `MAT_T_*` (per-hole) AND `MAT_Bunkers|Green|Fringe|Tee|Fairway|Rough|Semirough|Road|OOB` (shared, with optional `_Dark`)
   - B.3: Use a filename → experimental-texture lookup dict; repoint `_BaseMap` + `_MainTex` + `_BumpMap`; preserve `m_Scale`, `_BaseColor`, all floats, all colors
   - B.4: TerrainLayer duplicates must preserve `m_NormalScale: 0.4`, `m_SmoothnessSource: 1`, `m_MaskMapTexture` GUID, `m_TileSize`
   - B.5 covered by Step 0 above
   - B.6: HOLE01_CLONE_REPORT.md must list Bunkers, Green, Fringe, Tee duplications by name (acceptance gate)
5. Run `GOLFIN > Tools > Build Hole_01 Experimental Clone`
6. Verify production scene + production materials (excluding new `Experimental/` subfolders) are unmodified — `git status` shows only additions

**Hard rules:**
- No edits to production scene, production TerrainLayers, or production materials in `Materials (Shared by courses)/` outside the new `Experimental/` subfolder
- No edits to `HoleGeoImporter.cs` or any other importer code
- No splatmap or mask map regeneration
- Preserve `_BaseColor` tints on duplicated materials (e.g. `MAT_Bunkers` warm cream tint must survive)
- If Grass005 delivers wrong-looking output (brown/yellow/blue), surface to Architect WITH delivered images, do NOT swap to a third source silently
- Iteration budget: 1 attempt for Track A, 2 attempts for Track B

✅ DONE: [date]
- Track A: [N textures regenerated, sources used, brightness shifts applied, any failed sources]
- Track B: [N MeshRenderers walked, N materials encountered, N duplicated split shared/per-hole, N normals reimported, clone report path]

---

