# SPEC — Terrain-apron MATERIAL fix (green_ship_polish, PASS 2 follow-up #2)

**Authored:** 2026-06-02 11:00 CEST / 18:00 JST (Architect)
**Status:** SPEC_READY
**Track:** `green_ship_polish` — material/appearance fix for the terrain-apron ring. The apron **geometry is ACCEPTED** (sawtooth gone, weld gap 0, surface-classified rough, 16 fairway greens byte-identical). This re-specs ONLY §Change 3 (material) of `SPEC_GREEN_SEAT_TERRAIN_FRINGE.md` + the width value. **No geometry, seam, detection, or classification rework.**
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (terrain-apron-material)`
**Scope:** importer-only (`HoleGeoImporter.cs`); affects only the apron material on the 2 terrain-bordered greens (H10, H18).

---

## Defect (Cesar visual rejection, H10 Scene-view)
The apron mesh fixes the collar↔terrain sawtooth (ACCEPTED) but was textured with the **fairway-fringe** material, not the **terrain (rough)** it meets, so it reads as a distinct dark smooth band. Three observations: (1) material/tiling differs from terrain + no normal map; (2) tile size 6 too small; (3) 1.5 m width reads too big.

## Root cause (verified in code)
Apron material build (L3242–3261) calls `CreateZoneMaterial(..., "T_Semirough_Albedo", 6f)` — the fairway-fringe path: semi-rough albedo (layer 2), **no normal map**, tile 6, URP Lit, smoothness 0.1. The terrain it abuts is the **rough catch-all (layer 3)**: `T_Rough_Albedo` + `T_Rough_Normal` (normalScale 0.4) + matte mask (smoothness 0) + tile 8 + aniso 16, via URP TerrainLit. Mismatch on every channel → distinct band.

## ARCHITECTURAL DECISION — single rough material (NOT a splatmap blend)
**Verified in the alphamap build (L1332–1372):** the importer classifies each terrain cell to **one** layer at weight 1.0 (`alphamap[ay,ax,layer] = 1.0f`) from the `zones` raster via `ZoneToLayer`. The ONLY blend anywhere is rough↔OB (L1372–1457), and OB shares rough's base texture (just tinted) — no texture seam even there. So H10/H18's green-edge terrain is **dominantly the rough layer at weight 1.0, not a multi-layer splat blend.**
→ A single `T_Rough_*` apron material matches the terrain. **No triplanar / alphamap-sampling / custom shader needed.** This is the same mesh(Lit)-vs-terrain(TerrainLit) pattern already shipped+accepted for collar/fairway/tee-skirt; replicating the rough layer's albedo+normal+tile+mask on a Lit material is sufficient (the pre-existing Lit-vs-TerrainLit shader gap is accepted and not in scope).

## The fix

### Change A — extend the material builder with a normal-map slot
`CreateZoneMaterial` (L2352–2385) currently binds albedo + tileScale only. Add **optional** normal-map params (keeps every existing caller working — pass null/default → identical output, prove byte-identical):
```csharp
// extend signature (optional args, default null/0 → unchanged for existing callers):
CreateZoneMaterial(dataDir, projectRoot, matName, albedoName, tileScale,
                   string normalName = null, float normalScale = 0f,
                   string maskName = null, float smoothness = 0.1f)
// when normalName != null: load it, set _BumpMap (+ enable _NORMALMAP keyword), _BumpScale = normalScale
// when maskName   != null: bind the mask; set _Smoothness = smoothness, _Metallic = 0
```
(Reusable for any future zone wanting a normal. A dedicated `CreateApronMaterial` is the alternative — pick whichever is cleaner, but the existing fairway/collar callers MUST stay byte-identical: verify their .mat output unchanged.)

### Change B — build the apron material as a rough-layer replica
Replace the apron material build (L3242–3261) so it replicates the rough TerrainLayer (index 3, the values at L1471–1564):
```
albedo      = "T_Rough_Albedo"
normal      = "T_Rough_Normal",  normalScale = 0.4
mask        = the shared matte mask (smoothness 0, AO) used by the terrain layers (L1498–1533)
tileScale   = 8        // match rough layer tile (was 6)
aniso       = 16       // match terrain layers (Lit aniso via texture import settings if not material-settable; match as close as the Lit shader allows)
smoothness  = 0        // matte, match terrain (was 0.1 — caused grazing-angle sheen)
```
World-space UV tiling stays `(wx/tileSize, wz/tileSize)` (L4928) with tileSize now 8 — so the apron's texel density matches the terrain it meets.

### Change C — width to the teeth-coverage floor
`GreenTerrainApronWidth = 1.5f` → **`1.1f`**. Hard floor is the raster-hole cell ≈ 0.98 m (below it the sawtooth returns); 1.1 m clears it with margin. The material fix (A+B) is the real lever for "band too big" — once the apron textures like terrain, the remaining 1.1 m won't read as a distinct band. Do NOT go below ~1.0 m without a teeth-shrink path (raise holesResolution — rejected, ~64 MB/terrain; or terrain mesh-cut — bigger change); a sub-1 m apron is a constraint trade, not a free knob.

## What must NOT change
- Apron **geometry** (rings, weld, annulus triangulation, over-the-carve placement) — accepted.
- Apron **surface classification** (`SurfaceType.Rough`, excluded from `BakedHeightProvider`, plays as rough) — correct, keep.
- **Terrain-bordered detection** ({H10,H18}), the collar↔fairway CDT weld, B1 seat, `relH`, `green.json`, schema, bake — untouched.
- **Existing `CreateZoneMaterial` callers** (green L2414, collar L2421, fairway, tee) — must emit byte-identical .mat (the new params default to the old behavior). PROVE IT.

## Hard rules
1. `HoleGeoImporter.cs` ONLY. Verify `grep MenuItem` (live importer).
2. Material/width only — NO geometry, seam, detection, or classification change.
3. Existing zone-material callers byte-identical (new normal/mask params are optional & default-off).
4. Apron material = rough-layer replica (`T_Rough_Albedo` + `T_Rough_Normal` 0.4 + matte mask + tile 8 + smoothness 0). Single material (alphamap is single-layer weight-1.0 — verified; no shader).
5. Width ≥ ~1.0 m (teeth floor 0.98 m). 1.1 m specified.
6. `LESSONS_FRINGE_BORDER_MESHES.md` mandatory read before touching this code (CLAUDE.md rule).
7. 16 fairway greens still emit NO apron + byte-identical (regression guard).

## Verification — appearance gate (the gate the prior pass MISSED)
Prior gates checked sawtooth geometry (runs/row) and passed while the material was wrong. **Add an explicit appearance gate.** Re-shoot H10 + H18 from the grazing arc (the angle Cesar inspects), apron NOT selected (no gizmo outline):
- **Apron is INDISTINGUISHABLE from surrounding terrain** — no distinct band in albedo, tiling, normal detail, or grazing-angle sheen. This is the acceptance bar, not "no sawtooth."
- Sawtooth still absent (geometry unchanged — confirm no regression).
- H10 proud-rim still graded (geometry unchanged).
- Ball on apron still plays as rough (classification unchanged).
- 16 fairway greens: no apron, .mat byte-identical; existing collar/fairway/green .mat byte-identical (Change A regression).
- Frame-extract the orbit at native res; LOOK before captioning (false-clean slipped twice — N=3 discipline; the review-miss is logged).

## Files touched
- `HoleGeoImporter.cs` — extend `CreateZoneMaterial` normal/mask slot (Change A); apron material = rough replica (Change B); `GreenTerrainApronWidth 1.5→1.1` (Change C).
- Regenerated `GreenApron_1.mat` (×2, H10/H18) + their `Hole_10/18_Geo.unity`. No other .mat changes (prove).
- NO bake/schema/`green.json`/physics-gate change.

## Definition of done
- `CreateZoneMaterial` normal/mask slot added; existing callers byte-identical (proven).
- Apron material replicates rough layer (albedo+normal 0.4+matte mask+tile 8+smoothness 0); width 1.1 m.
- H10 + H18 grazing-arc re-shoot: apron **indistinguishable from terrain** — Cesar sign-off (this is the gate).
- Sawtooth absent, proud rim graded, plays-as-rough — all unchanged.
- 16 fairway greens + existing zone .mat byte-identical.
- EditMode tests pass (count). IMPLEMENTER_REPORT content-sanity per Lesson O — describe what the grazing shots show (does the apron disappear into terrain?), not "captured."

## Open items to report back
1. Did extending `CreateZoneMaterial` keep all existing callers byte-identical? (the regression proof)
2. Could `_Smoothness 0` + matte mask + `T_Rough_Normal` be matched on the URP **Lit** apron well enough to be indistinguishable from the **TerrainLit** terrain, or does a residual lighting difference remain at grazing angle? If residual, report it — may accept (pre-existing Lit-vs-TerrainLit gap) or escalate.
3. At 1.1 m + matched material, does width still read as a band? If yes, the teeth floor blocks going lower — flag for a teeth-shrink decision, don't silently shrink below 1.0 m.
4. Confirm H10/H18 apron-edge terrain is indeed single-layer rough (no unexpected OB/semi-rough at those specific green edges) — the single-material assumption.
