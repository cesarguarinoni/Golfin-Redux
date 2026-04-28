# Spec: Texture Experiment Phase 2 — Source Replacements + Clone Script Fixes [CLOSED 2026-04-28]

> **STATUS: CLOSED.** Phase 2 was implemented and validated 2026-04-28. Net positive but not promotion-ready. Findings, real-world reference (Lomond CC, Mie, Japan), agronomic facts, and a future plan are documented in **`Docs/Specs/Queued/TEXTURE_EXPERIMENT_FINDINGS_AND_PLAN.md`**. The bunker sand swap is the only standalone-promotion candidate from this experiment.

---

## Phase 2 closing summary (added 2026-04-28)

**Wins**
- Bunker sand (Ground054 + warm cream tint) is visibly better than production. Recommend standalone promotion (own spec, ~30min Code work).
- Rough is now actually green (Grass005 worked).
- Fairway base reads as believable mowed grass with normals working.
- Cart path stays good.
- Clone script architecture is solid: shared-vs-per-hole material discovery and normal-map import settings (textureType:NormalMap + sRGBTexture:false) both work end-to-end and are re-runnable.

**Still off after Phase 2**
- Tee gradient border ring still uses old material (clone script doesn't catch it — likely created inline by `CreateGradientBorderRing` with a hole-specific naming scheme that doesn't match the discovery regex).
- Fairway lost its mow stripes (splatmap-painted Light/Dark variants no longer read as different bands; mow stripes are really a shader effect).
- Rough/shader tone clash at material boundaries (smoothness mismatch between TerrainLayer mask map and overlay material scalar smoothness).
- Greens and tees are too dark (real bentgrass greens are LIGHTER than fairway/rough; brightness shifts went the wrong direction).
- General flatness persists despite normals working (likely `m_NormalScale: 0.4` too conservative + ambient contribution flat).

**Why we're closing instead of going Phase 3 immediately**

Pure source-substitution is hitting diminishing returns. Each round delivers ~50–70% of the visible improvement we wanted but leaves 30–50% on the table because of factors no albedo can fix: no mow stripes (shader), no grain anisotropy (shader), no height blending at boundaries (shader), and smoothness mismatches between TerrainLayer and overlay materials (architecture).

Next big visual jump comes from shader work, not more textures. See the Findings & Plan doc for ranked future plans (mow stripe shader → macro variation → grain highlights → one more source pass → height blending).

---

## Original spec (Phase 2 implementation, retained for reference)

**Status:** Active — handoff to Claude Code
**Handoff file:** `Docs/TellCode.md` (pointer block) + this file
**Date:** 2026-04-28
**Supersedes:** Phase 1 outputs (`Hole_01_Experimental_Geo.unity`, `hole-01-experimental/`, `Textures_Experimental/`) which had visible defects.

### Recap — what went wrong in Phase 1

After reviewing screenshots (`screenshot_2026-04-28_06-01-06.jpg` through `_06-04-51.jpg`), 6 distinct defects in the experimental scene:

1. **Rough is brown** — Poly Haven `aerial_grass_rock` reads as dirt, not green wild grass
2. **Semi-rough is too striking** — brighter/more vivid than fairway, inverting the mow hierarchy
3. **Greens still look unchanged** — clone script didn't repoint the shared `MAT_Green.mat` overlay material (only caught per-hole `MAT_T_*` materials)
4. **Bunkers still look unchanged** — same root cause as #3 (shared `MAT_Bunkers.mat` not duplicated)
5. **Tee surface looks identical to fairway** — spec mistake: Tee was mapped to Grass002 same as fairway, with only an 8% brightness shift. Needs its own distinct source.
6. **Tee gradient border ring uses old texture** — same root cause as #3 (shared border material not duplicated).
7. **Everything looks flat (no normals)** — verified: experimental texture `.meta` files have `textureType: 0` (Default) and `sRGBTexture: 1`. Unity is not unpacking them as normal maps. Normals exist on disk but Unity reads them as color data.

### Goal

Fix all 7 defects in one round-trip. Re-generate textures with better sources where needed, fix the clone script to catch all overlay materials, set normal map import settings programmatically, then re-run end-to-end.

### Two tracks (parallel; Code can do them in either order, but BOTH must complete before validation)

#### Track A — Texture source replacements (Step 1)

##### A.1 — New texture source map

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

##### A.2 — Source URLs (verified accessible 2026-04-27)

- ambientCG Grass001: `https://ambientcg.com/get?file=Grass001_2K-JPG.zip`
- ambientCG Grass002: `https://ambientcg.com/get?file=Grass002_2K-JPG.zip`
- ambientCG Grass005: `https://ambientcg.com/get?file=Grass005_2K-JPG.zip`

##### A.3 — Manifest update + re-run

Code edits `Tools/TextureExperiment/manifest.json`. Run `cd Tools/TextureExperiment && node prepare-textures.mjs`.

Expected output: 25 files in `Assets/Courses/Textures_Experimental/`.

#### Track B — Clone script fixes (Step 2)

##### B.1 — Add normal map import settings step

```csharp
foreach (var normalAssetPath in experimentalNormalPaths)
{
    var importer = AssetImporter.GetAtPath(normalAssetPath) as TextureImporter;
    if (importer == null) continue;
    importer.textureType = TextureImporterType.NormalMap;
    importer.convertToNormalMap = false;
    importer.sRGBTexture = false;
    importer.SaveAndReimport();
}
```

##### B.2 — Expand overlay material discovery

Walk every MeshRenderer in the duplicated scene, iterate ALL sharedMaterials slots, match against `^MAT_(Bunkers|Green|Fringe|Tee|Fairway|Rough|Semirough|Road|OOB)(_Dark)?$` OR `^MAT_T_.*$`. Duplicate to `Materials (Shared by courses)/Experimental/` (shared) or `hole-01-experimental/` (per-hole). Repoint `_BaseMap`, `_MainTex`, `_BumpMap`. Preserve `m_Scale`, `_BaseColor`, all floats, all colors.

##### B.4 — Verify TerrainLayer normal scale + smoothness preserved

Preserve `m_NormalScale: 0.4`, `m_SmoothnessSource: 1`, `m_MaskMapTexture` GUID, `m_TileSize`.

##### B.5 — Tear down Phase 1 outputs first

Delete the four Phase 1 output folders + clone report before re-running.

##### B.6 — Updated clone report

Must list Bunkers, Green, Fringe, Tee duplications by name (acceptance gate).

### Hard rules (Phase 2)

- No edits to production scene, production TerrainLayers, or production materials in `Materials (Shared by courses)/` outside the new `Experimental/` subfolder.
- No edits to `HoleGeoImporter.cs` or any other importer code.
- No splatmap or mask map regeneration.
- Preserve `_BaseColor` tints on duplicated materials.
- Preserve `m_Scale` on duplicated materials.

### Final state of Phase 2 outputs (as of 2026-04-28)

- `Assets/Courses/Textures_Experimental/` — 25 textures, normal maps imported as NormalMap/linear
- `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity` — exists, opens cleanly
- `Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental/` — TerrainData, 9 TerrainLayers, per-hole material clones
- `Assets/Courses/Materials (Shared by courses)/Experimental/` — shared material clones (bunkers, green, fringe, tee, fairway, rough, semirough, road, OOB)
- `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md` — full duplication log

These can stay as reference for future passes, or be deleted (~60 MB cleanup) at Cesar's discretion.
