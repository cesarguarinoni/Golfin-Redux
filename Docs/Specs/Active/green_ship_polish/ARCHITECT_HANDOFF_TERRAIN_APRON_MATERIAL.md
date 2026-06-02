# ARCHITECT HANDOFF — terrain-apron material/appearance (green_ship_polish, PASS 2 follow-up #2)

**Written:** 2026-06-02 (Claude Code, post `ARCHITECT_REVIEW_PASS` → Cesar visual rejection)
**For:** the claude.ai Architect chat, to author the apron-material fix spec.
**One-line:** The terrain-apron Option-C mesh **fixes the collar↔terrain sawtooth (geometry ACCEPTED)**, but it was textured to match the fairway *fringe* instead of the *terrain* it meets, so it reads as a distinct band. Re-spec the apron's **material/normal/tiling** to replicate the surrounding **rough TerrainLayer**, and decide the **width** (subject to a hard teeth-coverage floor). No geometry/seam rework.

---

## What is ACCEPTED (do not reopen)
- The apron **ring geometry** (inner = `DilateContour(activeContour, GreenCollarWidth)` coincident-with-collar-outer by construction; outer = `+ GreenTerrainApronWidth`; annulus triangulated; sits over the raster `SetHoles` carve). Weld gap to collar = 0 by construction (red-team confirmed structurally).
- The **terrain-bordered detection** (centroid-inside-fairway → exactly {H10, H18} on Lomond; 16 fairway greens get no apron, byte-identical — proven by "exactly 2 `GreenApron_1.mat` on disk"). Reviewer banked a spec-language clarification for the half-and-half case; not a blocker.
- The collar↔fairway CDT weld, B1 fitted-plane seat, `relH`, `green.json`, schema, bake — all untouched (+215/−0 additive importer diff).
- The apron's **surface classification** (tagged `SurfaceType.Rough`, no `GreenSurfaceInfo`, excluded from `BakedHeightProvider`, plays as rough) — correct, keep it.

## What Cesar REJECTED (this handoff)
Visual inspection, H10 Scene-view, apron selected (orange inner/outer ring outline). Three observations:
1. **Material/tiling clearly different from terrain** — apron reads as a distinct smooth dark band. Should use the SAME material as terrain **and the same normal map**.
2. **Tile size 6 too small vs terrain.**
3. **Apron width 1.5 m unacceptably big.**

## ROOT CAUSE (verified in code — `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`)
The apron copied the **fairway-fringe** material, not the **terrain** it abuts.

| | Apron got (WRONG) | Terrain it meets (rough catch-all, layer 3) |
|---|---|---|
| Albedo | `T_Semirough_Albedo` (layer 2) | `T_Rough_Albedo` |
| Normal | **none** | `T_Rough_Normal`, `normalScale 0.4` |
| Mask | none | shared matte mask (smoothness=0, AO) |
| tileSize | **6** | **8** |
| Shader | URP **Lit** (`GetLitShader`) | URP **TerrainLit** (splatmap-blended) |
| Aniso | default | 16 |

### Code anchors
- **Apron material build:** L3242–3261 — `CreateZoneMaterial(dataDir, projectRoot, apronMatName, "T_Semirough_Albedo", 6f)`. Comment admits it reuses the fairway-fringe path.
- **`CreateZoneMaterial`:** L2352–2385 — **albedo + `tileScale` ONLY; no normal-map parameter.** Sets `_BaseMap`, `_BaseColor=white`, `_Smoothness 0.1`, `_Metallic 0`, double-sided. Adding "use the same normal map" requires extending this (bind `_BumpMap`/`_NormalMap` + `_BumpScale`) or a dedicated apron-material function.
- **Terrain TerrainLayers:** L1468–1598. `albedoNames`/`normalNames`/`tileSizes` arrays (L1471–1493). Rough catch-all = index 3 (`T_Rough_Albedo`/`T_Rough_Normal`/tile 8); semi-rough = index 2 (tile 6); OB = index 8 (tinted rough, tile 10). `normalScale 0.4` (L1549), matte mask (L1498–1533), aniso 16 (L1542/1564).
- **Zone-mesh material/UV reference:** `CreateZoneMaterial` callers — green (L2414), collar (`"T_Semirough_Albedo", 4f` L2421). Zone-mesh UV convention `new Vector2(wx/tileSize, wz/tileSize)` at L4928 (world-space tiling).
- **Apron width const:** `GreenTerrainApronWidth = 1.5f` (near green constants ~L53–72).
- **holes raster cell** ≈ `terrainData.size / holesResolution` ≈ 2006 m / 2049 ≈ **0.98 m** — the teeth pitch the apron must cover (original SPEC_GREEN_SEAT_TERRAIN_FRINGE.md §Root cause).

## Constraints the new spec must respect
1. **Teeth-coverage floor:** apron width must stay **> ~0.98 m** (the raster-hole cell) or the sawtooth returns. So width ∈ ~[1.0, 1.5]; "make it small" has a hard floor unless the teeth are shrunk another way (raise `holesResolution` — rejected before for ~64 MB/terrain at 8192; or a real terrain mesh-cut — bigger change). **A sub-1 m apron is a constraint trade, not a free knob — call it explicitly if you want it.**
2. **Material #1/#2 fix is the real lever for #3:** once the apron replicates the rough layer (albedo + `T_Rough_Normal` + tile 8 + normalScale 0.4 + mask), it blends into terrain and the width stops reading as a band. Recommend: fix material first, keep width at the floor (~1.0–1.2 m), re-shoot, then judge if width still reads big.
3. **Mesh-vs-terrain shader gap is pre-existing and accepted:** every zone mesh (collar/fairway/tee skirt) is URP Lit sitting against URP TerrainLit terrain and is shipped/accepted. The apron is the same pattern — matching the rough layer's albedo/normal/tile is sufficient; you do NOT need a custom shader UNLESS the edge is a strong splatmap blend (see open question 1).

## Open questions for the Architect to resolve
1. **Single material vs splatmap blend — THE architectural decision.** Is H10/H18's green-edge terrain dominantly the rough layer (→ a single `T_Rough_*` apron material matches, clean fix) or a real BLEND (rough + OB + bunker → no uniform mesh material matches → would need a triplanar/terrain-projection apron shader, or sample the alphamap per-vert)? **Check:** sample `terrainData.GetAlphamaps()` along the apron outer ring on H10/H18 (reproducible the way `/tmp/green_seam_diag.py` was in the prior handoff). Expectation: dominant rough, given the collar/fairway/tee precedent — but this is the call that decides single-material vs shader.
2. **Extend `CreateZoneMaterial` for a normal slot, or write a dedicated apron-material fn?** Cesar explicitly wants the same normal map. Either bind `_BumpMap` + `_BumpScale 0.4` in `CreateZoneMaterial` (helps any future zone that wants a normal) or a focused `CreateApronMaterial`.
3. **Match the rough layer literally, or sample the actual dominant layer per green?** Hardcoding `T_Rough_*` is simplest and almost certainly right; sampling the dominant alphamap layer at the apron ring is the data-driven version (handles a future green surrounded by OB/semi-rough). Your call on robustness vs simplicity.
4. **Target width:** propose a value within [1.0, 1.5] (or justify a teeth-shrink path for < 1.0). Note `GreenTerrainApronWidth` must stay > the raster cell.
5. **Smoothness/mask:** terrain layers use the matte mask (smoothness 0). The apron currently uses flat `_Smoothness 0.1`. Match to avoid a sheen difference at grazing angles (Cesar inspects from the grazing arc).

## Reading list (ordered)
1. This file.
2. `CESAR_REJECTION.md` (the 3 observations + root cause).
3. `SPEC_GREEN_SEAT_TERRAIN_FRINGE.md` (the accepted Option-C apron spec — material section §Change 3 is the part being re-specced; everything else stands).
4. `HoleGeoImporter.cs` regions above (L3242–3261 apron, L2352–2385 CreateZoneMaterial, L1468–1598 TerrainLayers).
5. `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` (mandatory before touching fringe/border mesh code — CLAUDE.md rule).
6. Canonical evidence of the defect: `screenshots/terrain_apron_h10_canonical_grazing.png`, `videos/terrain_apron_h10_orbit_captioned.mp4` (the distinct band is visible). Plus Cesar's annotated H10 Scene-view (pasted in chat 2026-06-02).
7. Tee-skirt precedent (importer ~L3471–3556 per prior handoff) — analogous mesh-meets-terrain that already textures to match terrain; the model for the apron material.

## Suggested deliverable
Revise `SPEC_GREEN_SEAT_TERRAIN_FRINGE.md` §Change 3 (or a new `SPEC_TERRAIN_APRON_MATERIAL.md` in this folder):
apron material = rough TerrainLayer replica (`T_Rough_Albedo` + `T_Rough_Normal` + tile 8 + normalScale 0.4 + matte mask + aniso),
normal-slot path in the material builder, target width within the teeth floor, and a gate that re-shoots H10/H18 from the grazing
arc and confirms the apron is **indistinguishable from surrounding terrain** (not just "no sawtooth"). Then set `STATUS.md = SPEC_READY`
and kick `golfin-implementer`.

## Pipeline note (review miss)
This is logged to `.claude/review_misses.log`: all 4 gates PASSed because they checked **sawtooth geometry (runs-per-row)**, not
**material-matches-terrain**. The new spec's acceptance gate should add an explicit "apron material indistinguishable from terrain
at the grazing arc" check so the reviewers test appearance, not just seam continuity.
