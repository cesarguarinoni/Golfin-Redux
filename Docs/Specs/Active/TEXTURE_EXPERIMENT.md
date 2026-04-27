# Spec: Texture Experiment Phase 2 — Source Replacements + Clone Script Fixes

**Status:** Active — handoff to Claude Code
**Handoff file:** `Docs/TellCode.md` (pointer block) + this file
**Date:** 2026-04-28
**Supersedes:** Phase 1 outputs (`Hole_01_Experimental_Geo.unity`, `hole-01-experimental/`, `Textures_Experimental/`) which had visible defects.

## Recap — what went wrong in Phase 1

After reviewing screenshots (`screenshot_2026-04-28_06-01-06.jpg` through `_06-04-51.jpg`), 6 distinct defects in the experimental scene:

1. **Rough is brown** — Poly Haven `aerial_grass_rock` reads as dirt, not green wild grass
2. **Semi-rough is too striking** — brighter/more vivid than fairway, inverting the mow hierarchy
3. **Greens still look unchanged** — clone script didn't repoint the shared `MAT_Green.mat` overlay material (only caught per-hole `MAT_T_*` materials)
4. **Bunkers still look unchanged** — same root cause as #3 (shared `MAT_Bunkers.mat` not duplicated)
5. **Tee surface looks identical to fairway** — spec mistake: Tee was mapped to Grass002 same as fairway, with only an 8% brightness shift. Needs its own distinct source.
6. **Tee gradient border ring uses old texture** — same root cause as #3 (shared border material not duplicated).
7. **Everything looks flat (no normals)** — verified: experimental texture `.meta` files have `textureType: 0` (Default) and `sRGBTexture: 1`. Unity is not unpacking them as normal maps. Normals exist on disk but Unity reads them as color data.

## Goal

Fix all 7 defects in one round-trip. Re-generate textures with better sources where needed, fix the clone script to catch all overlay materials, set normal map import settings programmatically, then re-run end-to-end.

## Two tracks (parallel; Code can do them in either order, but BOTH must complete before validation)

---

## Track A — Texture source replacements (Step 1)

### A.1 — New texture source map

Update `Tools/TextureExperiment/manifest.json`. Replacements only — keep all other slots as Phase 1.

| Slot | OLD source (Phase 1) | NEW source (Phase 2) | Reason |
|---|---|---|---|
| **T_Rough** | Poly Haven `aerial_grass_rock` | **ambientCG `Grass005` (2K-JPG)** | Grass005 is a tall/wild meadow texture — green, dense, longer blade simulation. Matches "rough" visual role. |
| **T_Semirough** | ambientCG `Grass004` (too vivid) | **ambientCG `Grass002` (2K-JPG) with brightness −10%** | Re-uses fairway's source but darker. This deliberately makes semi-rough feel like fairway grass that hasn't been mowed quite as tight — same species, longer cut, darker shade. |
| **T_Tee / T_TeeDark / T_TeeDark_NoBorder** | Grass002 with brightness shifts | **ambientCG `Grass001` (2K-JPG) with appropriate brightness shifts** | Grass001 = tight putting-green texture. Tee boxes are mowed close like greens but greener-on-fairway-side. Re-using Green's source for tees is intentional — both are tight-mow surfaces. |

For tees specifically:
- `T_Tee_Albedo` = Grass001 base brightness (no shift)
- `T_TeeDark_Albedo` = Grass001 brightness −10%
- `T_TeeDark_Albedo_NoBorder` = Grass001 brightness −10%
- `T_Tee_Normal` = Grass001 normal
- `T_TeeDark_Normal` = Grass001 normal (shared)
- `T_TeeDark_Albedo_NoBorder_Normal` = Grass001 normal (shared)

For semi-rough:
- `T_Semirough_Albedo` = Grass002 brightness −10%
- `T_Semirough_Normal` = Grass002 normal (shared with fairway, no brightness shift on normal)

### A.2 — Source URLs (verified accessible 2026-04-27)

- ambientCG Grass001: `https://ambientcg.com/get?file=Grass001_2K-JPG.zip` (already in Phase 1, used for green)
- ambientCG Grass002: `https://ambientcg.com/get?file=Grass002_2K-JPG.zip` (already in Phase 1, used for fairway)
- ambientCG Grass005: `https://ambientcg.com/get?file=Grass005_2K-JPG.zip` (NEW; replaces Poly Haven for rough)

If Grass005 returns a 404 or yields visibly bad output (e.g. bright/yellow rather than wild green), fall back to ambientCG Grass003 darker variant (brightness −5%, but rename outputs to T_Rough_*) and surface the issue to Architect with the actual delivered images.

### A.3 — Manifest update + re-run

Code edits `Tools/TextureExperiment/manifest.json` to reflect the new mapping. The `_Source/` cache may already have Grass001/002/004 ZIPs from Phase 1; only Grass005 is a new download. Grass004 entries can be removed (no longer needed).

Run: `cd Tools/TextureExperiment && node prepare-textures.mjs`

Expected output: 25 files in `Assets/Courses/Textures_Experimental/` (overwriting existing files where slots are reassigned).

### A.4 — Acceptance for Track A

- All 25 expected texture files present
- T_Rough_Albedo visibly looks like green wild grass (not brown/rocky)
- T_Semirough_Albedo visibly looks darker than T_Fairway_Light (fairway's lightest variant)
- T_Tee_Albedo visibly looks like tight putting-green grass (similar to T_Green_Albedo but slightly different brightness)
- README.md updated with new source attributions

---

## Track B — Clone script fixes (Step 2)

### B.1 — Add normal map import settings step

When the script generates the experimental textures (Track A), or re-runs over existing experimental textures, it must call `AssetDatabase.ImportAsset` on each `_Normal.jpg` file with these settings, before duplicating any TerrainLayer or Material:

```csharp
foreach (var normalAssetPath in experimentalNormalPaths)
{
    var importer = AssetImporter.GetAtPath(normalAssetPath) as TextureImporter;
    if (importer == null) continue;
    importer.textureType = TextureImporterType.NormalMap;
    importer.convertToNormalMap = false;  // already a normal map, don't re-convert
    importer.sRGBTexture = false;          // normals are linear, not sRGB
    importer.SaveAndReimport();
}
```

The list of normal paths to fix:
- `T_Fairway_Normal.jpg`
- `T_Green_Normal.jpg`
- `T_Fringe_Normal.jpg`
- `T_Semirough_Normal.jpg`
- `T_Rough_Normal.jpg`
- `T_Bunker_Normal.jpg`
- `T_Tee_Normal.jpg`
- `T_TeeDark_Normal.jpg`
- `T_TeeDark_Albedo_NoBorder_Normal.jpg`
- `T_OOB_Normal.jpg`
- `T_RoadAsphalt_Normal.jpg`

(All `_Normal.jpg` files in the folder. Use a glob.)

### B.2 — Expand overlay material discovery

Phase 1's clone script duplicated only 4 overlay materials (`MAT_T_Fairway_Mix`, `MAT_T_Semirough_Albedo`, `MAT_T_Tee_Albedo`, `MAT_T_RoadAsphalt_Albedo`) — the per-hole materials in `Data/hole-01-geo/`. It missed the SHARED overlay materials in `Assets/Courses/Materials (Shared by courses)/`:

- `MAT_Bunkers.mat` (used by all bunker MeshRenderers)
- `MAT_Bunkers_Dark.mat`
- `MAT_Green.mat` (used by green MeshRenderers)
- `MAT_Fringe.mat` (used by green collar / fringe rings)
- `MAT_Tee.mat` and `MAT_Tee_Dark.mat` (used by tee surfaces — note: confirmed via inspection these point at production T_Tee_Albedo, NOT the per-hole `MAT_T_Tee_Albedo`)
- `MAT_Fairway.mat` and `MAT_Fairway_Dark.mat`
- `MAT_Rough.mat`
- `MAT_Semirough.mat`
- `MAT_Road.mat`
- `MAT_OOB.mat`
- `MAT_Hole.mat` (the cup material — leave as-is, not texture-relevant)

The clone script must:
1. Walk every `MeshRenderer` in the duplicated experimental scene (recurse the entire root hierarchy, not just specific containers).
2. For each MeshRenderer, iterate ALL `sharedMaterials` slots (a renderer can have multiple).
3. For each material referenced, check if its `m_Name` matches any of: `^MAT_(Bunkers|Green|Fringe|Tee|Fairway|Rough|Semirough|Road|OOB)(_Dark)?$` OR `^MAT_T_.*$` (per-hole).
4. For each MATCHING material that hasn't been duplicated yet:
   - Locate the source asset path (`AssetDatabase.GetAssetPath(material)`)
   - Duplicate it to `Assets/Courses/Materials (Shared by courses)/Experimental/` (or `hole-01-experimental/` for per-hole `MAT_T_*` ones — keep existing convention) with suffix `_Experimental`
   - In the duplicate, repoint `_BaseMap`, `_MainTex`, AND `_BumpMap` to the experimental textures by filename match (per Phase 1 logic). If any of these properties is null on the source, skip that property.
   - Preserve the source's `m_Scale`, `m_Offset`, `_BumpScale`, `_BaseColor`, all `m_Floats`, all `m_Colors` — only repoint texture references. **DO NOT change scale**, e.g. MAT_Tee's scale of (14, 14) must stay (14, 14) on the experimental copy.
5. Repoint the MeshRenderer's `sharedMaterials` slot at the duplicate.
6. After scene walk, save the scene.

### B.3 — Filename-to-experimental-texture mapping

The script's filename-match logic for repointing material textures must be robust. Build a lookup dictionary at the start:

```
Production filename → Experimental filename
T_Fairway_Light.jpg → Textures_Experimental/T_Fairway_Light.jpg
T_Fairway_Dark.jpg → Textures_Experimental/T_Fairway_Dark.jpg
T_Fairway_Mix.jpg → Textures_Experimental/T_Fairway_Mix.jpg  (NOTE: Mix may not exist in Phase 1; falls back to Light)
T_Green_Albedo.jpg → Textures_Experimental/T_Green_Albedo.jpg
T_Fringe_Albedo.jpg → Textures_Experimental/T_Fringe_Albedo.jpg
T_Semirough_Albedo.jpg → Textures_Experimental/T_Semirough_Albedo.jpg
T_Rough_Albedo.jpg → Textures_Experimental/T_Rough_Albedo.jpg
T_Bunker_Albedo.jpg → Textures_Experimental/T_Bunker_Albedo.jpg
T_BunkerDark_Albedo.jpg → Textures_Experimental/T_BunkerDark_Albedo.jpg
T_Tee_Albedo.jpg → Textures_Experimental/T_Tee_Albedo.jpg
T_TeeDark_Albedo.jpg → Textures_Experimental/T_TeeDark_Albedo.jpg
T_TeeDark_Albedo_NoBorder.jpg → Textures_Experimental/T_TeeDark_Albedo_NoBorder.jpg
T_OOB_Albedo.jpg → Textures_Experimental/T_OOB_Albedo.jpg
T_RoadAsphalt_Albedo.jpg → Textures_Experimental/T_RoadAsphalt_Albedo.jpg
T_Fairway_Normal.jpg → Textures_Experimental/T_Fairway_Normal.jpg
(... and similar for all _Normal pairs)
```

If a production texture has no experimental counterpart (e.g. a hole-specific texture not in our set), log a warning to the clone report and leave that slot unchanged on the duplicated material (graceful fallback).

### B.4 — Verify TerrainLayer normal scale + smoothness preserved

When duplicating each TerrainLayer, the script must preserve:
- `m_NormalScale: 0.4` (production value — verified)
- `m_SmoothnessSource: 1` (production value — uses mask map, not albedo alpha)
- `m_MaskMapTexture` GUID (kept as-is — production 4×4 mask)
- `m_TileSize` (production-specific per layer, do not change)

The script should NOT zero these out. Read source TerrainLayer YAML, copy fields, swap only diffuse + normal texture GUIDs.

### B.5 — Tear down Phase 1 outputs first

Before re-running, the script must DELETE:
- `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity` (and `.meta`)
- `Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental/` (whole folder)
- `Assets/Courses/Materials (Shared by courses)/Experimental/` (whole folder, may not exist yet on first run)
- `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md` (will be regenerated)

This guarantees a clean re-run, no stale Phase 1 references.

### B.6 — Updated clone report

The new `HOLE01_CLONE_REPORT.md` should explicitly list:
- Number of MeshRenderers walked
- Number of unique materials encountered
- Number of materials duplicated (split: shared vs per-hole)
- Number of materials skipped because no texture remap was needed (e.g. MAT_Hole)
- Number of normal textures whose import settings were updated
- Each duplication: source path → destination path
- Warnings: production textures with no experimental counterpart, materials whose texture properties were null, etc.

Acceptance gate for the report: must list at least these material name stems being duplicated: `Bunkers`, `Green`, `Fringe`, `Tee`, `Fairway`. If any of those is missing from the report, the script failed to find them and Code must investigate before declaring done.

---

## Acceptance criteria (full Phase 2)

- [ ] All 25 experimental textures regenerated with new sources for Rough, Semi-rough, Tee variants
- [ ] All `_Normal.jpg` files have `textureType: NormalMap` and `sRGBTexture: 0` in their .meta
- [ ] `Hole_01_Experimental_Geo.unity` exists and opens cleanly with no missing reference errors
- [ ] `Assets/Courses/Materials (Shared by courses)/Experimental/` exists and contains duplicates of at least: `MAT_Bunkers_Experimental.mat`, `MAT_Green_Experimental.mat`, `MAT_Fringe_Experimental.mat`, `MAT_Tee_Experimental.mat` (and their `_Dark` variants if present in scene)
- [ ] `Assets/Courses/Textures_2025(JPG)/` is unmodified (verify via `git status`)
- [ ] Production `Hole_01_Geo.unity` is unmodified
- [ ] No production TerrainLayer or Material under `Data/hole-01-flat/` or shared `Materials (Shared by courses)/` (excluding the new `Experimental/` subfolder) is modified
- [ ] HOLE01_CLONE_REPORT.md explicitly lists Bunkers, Green, Fringe, Tee material duplications
- [ ] Total disk delta under 60 MB

After Code reports done, Cesar will:
1. Open `Hole_01_Experimental_Geo.unity` and walk the camera through the hole
2. Take screenshots from same angles as Phase 1 (fairway-down-corridor, green+bunker, fringe collar close-up, tee box)
3. Compare against `Hole_01_Geo.unity`
4. Decide GO (textures promote; spec next phase for the remaining 17 holes) or NO-GO (specific feedback, another iteration).

---

## Iteration budget

- Track A: 1 attempt. If Grass005 doesn't deliver a green wild-grass look, fall back to Grass003 darkened, surface to Architect.
- Track B: 2 attempts. The shared-material discovery is the trickiest part. If 2 attempts can't produce a clone where Bunkers / Green / Fringe / Tee are visibly different in the experimental scene, surface to Architect with specific failure (e.g. "MeshRenderer for Green_1 references material X which has no `_BaseMap` property").

---

## Hard rules

- No edits to production `Hole_01_Geo.unity`, `Textures_2025(JPG)/`, any production TerrainLayer, OR any production Material in `Materials (Shared by courses)/` outside the new `Experimental/` subfolder.
- No edits to `HoleGeoImporter.cs`, `HoleLiteImporter.cs`, or any other importer code — this is still a clone-and-edit task.
- No splatmap or mask map regeneration.
- If a source URL 404s, log and skip — do NOT substitute alternatives without checking with Architect.
- If Grass005's brightness/saturation looks wildly off the description (delivered output is brown, blue, etc.), surface to Architect with delivered images BEFORE wiring it into the clone.
- Preserve `_BaseColor` tints on duplicated materials — `MAT_Bunkers` has a warm cream tint `(1, 0.894, 0.703)` and that tint is intentional. Do NOT zero it out on the duplicate.

---

## Notes for the spec author (debugging context)

- The `MAT_T_*` per-hole materials in `Data/hole-01-geo/` exist because HoleGeoImporter creates them at import time (one set per hole). These are different from the SHARED `MAT_*` materials in the Materials folder.
- The shared materials use URP Lit shader (GUID `933532a4fcc9baf4fa0491de14d08ed7`).
- Material has `_NORMALMAP` keyword set in `m_ValidKeywords` — that's why having a properly-imported normal texture matters for the visible shading.
- Per-hole `MAT_T_*` materials use SAME shader. Property names (`_BaseMap`, `_MainTex`, `_BumpMap`) are identical.
- One subtlety: `MAT_Green.mat` has a `m_Parent: {fileID: 2100000, guid: 3de83aa8b3bfe404ab6dd9bd4e09db76, type: 2}` — it inherits from a parent material. When duplicating, copy the file directly and clear `m_Parent: {fileID: 0}` on the duplicate so it stands alone (or keep the parent reference; either works as long as texture overrides apply).
