# SPIKE FINDINGS — Terrain-Apron Invisibility Investigation
# green_ship_polish — apron-invisibility-spike

**Executed:** 2026-06-02 (golfin-implementer)
**Status:** SPIKE_DONE
**Ref spec:** `SPIKE_APRON_INVISIBILITY.md`

---

## Executive Summary

**Q1 verdict:** T1 (hardened URP Lit + T_Rough + normal + mask + smooth=0 + world-space UV wx/8,wz/8) achieves genuine blending with terrain. The apron is NOT visually distinguishable from the surrounding rough terrain at the grazing arc. T2 (TerrainLit on mesh) is broken — renders near-black without a splatmap control texture.

**Q2 verdict:** Cannot drop the carve for either H10 or H18. Both have terrain intrusion that would be visibly above the green surface if the carve were removed. H10 is catastrophic (0.189m intrusion, ~17cm above green surface); H18 is significant (~5.5cm above green surface).

**Recommended path:** Proceed with T1 spec (SPEC_TERRAIN_APRON_MATERIAL.md already authored): Lit + T_Rough_Albedo + T_Rough_Normal (0.4) + matte mask + smooth=0 + world-space UV (wx/8, wz/8) + width narrowed to 1.1m. The carve must remain on both H10 and H18.

---

## Q1 — Can a mesh be made INDISTINGUISHABLE from rough terrain at grazing?

### T1 — Hardened URP Lit match (TESTED)

**Approach:** Patched GreenApron_1.mat on H10 to:
- Albedo: `T_Rough_Albedo` (was: T_Semirough_Albedo)
- Normal: `T_Rough_Normal`, normalScale=0.4 (was: none)
- Mask: `MatteMaskMap` (smoothness=0, AO) (was: none)
- Smoothness: 0 (was: 0.1)
- Shader: URP Lit (unchanged)
- UV: world-space `(wx/8, wz/8)` patched directly on mesh verts (was: gradient `(ix+iz)/6f`)

**UV phase analysis (critical):** The CURRENT apron UV formula in `HoleGeoImporter.cs` (L3171–3188) uses a gradient UV:
```csharp
float vTile = (ix + iz) / 6f;
uvs[i] = new Vector2(0f, vTile);  // inner ring: u=0, v=diagonal-sum/6
uvs[ni + i] = new Vector2(1f, vTile);  // outer ring: u=1, v=diagonal-sum/6
```
This is NOT world-space tiling. The terrain uses `(wx/8, wz/8)` world-space projection. BOTH texture match AND UV phase fix are required for T1 to work.

**T1 visual result:**

| Before (original) | T1 (T_Rough + world-space UV) |
|---|---|
| `screenshots/terrain_apron_h10_canonical_grazing.png` | `screenshots/spike_apron/t1_h10_canonical_grazing.png` |

**BEFORE:** Visibly distinct dark band — immediately obvious as separate material from terrain. Color (semi-rough vs rough) and UV smear (gradient vs world-space projection) both contribute.

**AFTER T1:** The apron band is NOT distinguishable from the surrounding rough terrain at the grazing arc. The rough texture (albedo + normal relief) is phase-matched to the terrain splat projection, so no texture seam is visible at the apron edge. The transition from collar to terrain reads as expected — a surface with consistent rough texture, not an artificial ring.

**Residual difference (Lit vs TerrainLit BRDF):** A subtle BRDF difference exists between URP Lit and URP TerrainLit at grazing angles even at smoothness=0. However, this pre-existing Lit-vs-TerrainLit gap is accepted and shipped for ALL zone meshes (collar, fairway, tee-skirt). At the grazing arc, the difference is below Cesar's stated bar — the apron does not read as a separate element.

**T1 verdict: PASS.** The hardened Lit approach (T_Rough albedo+normal+mask+smooth=0+world-space UV) makes the apron genuinely invisible at the grazing arc. This is the cheapest viable approach.

### T2 — TerrainLit on mesh (TESTED)

**Approach:** Switched apron shader to `Universal Render Pipeline/Terrain/Lit` with T_Rough as _Splat0.

**Result:** **BROKEN/UNUSABLE.** Without a splatmap control texture (`_Control`), TerrainLit renders the mesh near-black. The mesh renders essentially invisible-but-dark — worse than the original.

See: `screenshots/spike_apron/t2_h10_canonical_grazing.png` — the apron appears as an almost-black ring, which is MORE visible than the original semi-rough material.

**Root cause:** TerrainLit is designed for terrain GameObjects with a splatmap. On a plain mesh, it requires `_Control` (a float4 texture where RGBA = layer weights for 4 splats). Without it, layer 0 has zero weight → black. Making TerrainLit work on a mesh requires generating a fake 1x1 control texture with layer 0 at full weight — a non-trivial additional step that adds shader complexity.

**T2 verdict: FAIL (and unnecessary, since T1 passes).** Do not pursue TerrainLit on the apron mesh.

### Q1 Summary

Lowest-effort approach that is genuinely invisible: **T1 — hardened URP Lit with T_Rough replica.**

Key requirements for T1 to work:
1. `T_Rough_Albedo` (not T_Semirough_Albedo)
2. `T_Rough_Normal`, `_BumpScale=0.4`
3. Matte mask map (smoothness=0, AO) — eliminates grazing-angle sheen
4. Smoothness=0 (was 0.1 — 0.1 caused visible specular at grazing angles)
5. World-space UVs `(wx/8, wz/8)` — MANDATORY, not the current gradient UV scheme

The UV fix (point 5) requires changing the UV generation in `CreateGreenTerrainApron()` at L3164–3188. This is in scope for `SPEC_TERRAIN_APRON_MATERIAL.md` (it's part of the "rough-layer replica" spec).

---

## Q2 — Can the carve/apron be dropped?

### Terrain Measurement Data (from script-execute, analytical)

| Hole | holesResolution | Cell size | Carved cells | Terrain intrusion* | Green height above terrain |
|---|---|---|---|---|---|
| H10 | 2048 | 0.1125m | 42809 | 0.189m (spec: sinkMax) | +0.02m (GreenSkirtDepth) |
| H18 | 2048 | 0.0882m | 77202 | 0.075m (spec: sinkMax) | +0.02m (GreenSkirtDepth) |

*Intrusion = max height natural terrain rises above the fitted green seat plane at the collar edge (from `ARCHITECT_HANDOFF_TERRAIN_SEAM.md` sinkMax data). Net poke-through above green surface = intrusion − 0.02m.

### Q2a — Can the carve be dropped?

**H10 (0.189m intrusion):** Terrain would poke **~0.169m above the green surface** with no carve. This is 16.9cm — extremely visible. The visual test confirms this:

See: `screenshots/spike_apron/q2a_h10_nocarve_grazing.png` — With no carve, H10 shows the terrain visibly intersecting the green mesh, creating a scalloped irregular border. **Not acceptable.**

**H18 (0.075m intrusion):** Terrain would poke **~0.055m above the green surface** with no carve. This is 5.5cm — visible as a distinct step/lip.

See: `screenshots/spike_apron/q2a_h18_nocarve_grazing.png` — With no carve, H18 shows a dark depression ring around the green perimeter where terrain and green mesh interact. **Not acceptable — the green collar visibly sits on terrain instead of looking like a continuous surface.**

**Q2a verdict: CANNOT drop carve for either H10 or H18.**

### Q2b — Could local high-res holes make teeth invisible?

The current holesResolution=2048 gives teeth of 0.1125m (H10) and 0.0882m (H18) — much finer than the ~0.98m estimated in the spec (that estimate assumed the full-course terrain size, not the per-hole terrain dimensions).

**Corrected memory cost:**
- Current (2048): ~16 MB per terrain
- 4096: ~64 MB per terrain
- 8192: ~256 MB per terrain

At 2048 resolution, teeth are 0.1125m × 0.0882m — already quite fine. Whether these are visible depends on viewing distance and terrain slope, but they're the root cause of the collar-to-terrain step.

Importantly: **even if the teeth were invisible (very high resolution), the terrain INTRUSION problem would remain** — the terrain surface would still protrude above the green mesh by 0.169m (H10) or 0.055m (H18). Finer teeth only address the stair-step pattern, not the height mismatch.

**Q2b verdict: Not useful for the intrusion problem. Only the carve (which creates the hole) prevents the terrain from appearing above the green surface.**

---

## Recommended Path

**H10 and H18 both:** Retain the terrain carve + the apron ring. Implement T1 material spec (SPEC_TERRAIN_APRON_MATERIAL.md):

### Required changes to `HoleGeoImporter.cs`:

**Change A: UV fix (mandatory for phase matching)**
Replace the current gradient UV scheme in `CreateGreenTerrainApron` (L3164–3188):
```csharp
// CURRENT (wrong — gradient UV):
float vTile = (ix + iz) / 6f;
uvs[i] = new Vector2(0f, vTile);
// REQUIRED (world-space, phase-matched to terrain):
uvs[i] = new Vector2(ix / 8f, iz / 8f);  // tileSize=8 matches rough layer
```
This change is essential — the world-space UV is what makes the texture phase-match the terrain's TerrainLit projection.

**Change B: Material = T_Rough replica** (SPEC_TERRAIN_APRON_MATERIAL.md §Change A+B)
- albedo: T_Rough_Albedo
- normal: T_Rough_Normal, normalScale=0.4
- mask: MatteMaskMap (smooth=0, AO)
- Smooth: 0, Metallic: 0
- `CreateZoneMaterial` needs normal/mask slot (or dedicated function)

**Change C: Width 1.5→1.1m** (SPEC_TERRAIN_APRON_MATERIAL.md §Change C)
The teeth pitch is 0.1125m on H10 — a 1.1m apron is far above the floor (0.1125m). Could go narrower, but 1.1m provides comfortable margin and the spec already chose 1.1m.

### Trade-off

If T1 is implemented as specified (world-space UV + T_Rough material): **the apron will be invisible at the grazing arc**, matching Cesar's absolute bar. The remaining Lit-vs-TerrainLit BRDF gap (always present for zone meshes) is accepted and not visually problematic at smooth=0 with a rough texture.

If only the texture is fixed WITHOUT the UV fix: partial improvement (better color, same UV smear) — may not pass Cesar's bar. Both must change.

---

## Evidence Captures

All captures in `screenshots/spike_apron/`:

| File | What it shows |
|---|---|
| `terrain_apron_h10_canonical_grazing.png` | BEFORE — original semi-rough material, visible dark band |
| `terrain_apron_h18_canonical_grazing.png` | BEFORE — H18 original, visible band |
| `t1_h10_canonical_grazing.png` | T1 — T_Rough+normal+mask+UV, from canonical NW grazing angle |
| `t1_h10_grazing_low.png` | T1 — lower grazing angle, apron blends with terrain |
| `t1_h10_graze_n_closeup.png` | T1 — N close-up, collar-to-terrain transition |
| `t1_h10_graze_sw.png` | T1 — SW grazing, terrain continuous around green |
| `t2_h10_canonical_grazing.png` | T2 — TerrainLit on mesh, near-black (BROKEN) |
| `q2a_h10_nocarve_grazing.png` | Q2a H10 — no carve, terrain visibly poking through green |
| `q2a_h10_nocarve_topdown.png` | Q2a H10 — top-down, terrain-green intersection rings |
| `q2a_h18_nocarve_grazing.png` | Q2a H18 — no carve, terrain lip visible around collar |
| `q2a_h18_nocarve_topdown.png` | Q2a H18 — top-down, dark depression band without carve |

---

## Spike Cleanup Verification

All spike edits reverted:
- `H10/GreenApron_1.mat`: reverted to T_Semirough_Albedo, tile 6, no normal, smooth 0.1 (via Unity AssetDatabase API)
- `H18/GreenApron_1.mat`: unmodified (was not touched in spike)
- `H10 terrain carve`: 42809 cells restored (confirmed by script-execute query)
- `H18 terrain carve`: 77202 cells restored (confirmed by script-execute query)
- `SpikeApronInvisibility.cs`: deleted
- Mesh UVs: in-memory only, not saved (scene NOT saved during spike)
- `git diff` of tracked files outside this task folder: zero spike edits in tracked files (TerrainData .asset modifications are pre-existing from before the spike, verified against initial git status)

**Note on GreenApron_1.mat git status:** These files show as `??` (untracked) because they are importer-generated and not tracked in git. They exist on disk in their reverted state (T_Semirough_Albedo). The spike-modified versions were reverted via Unity API before the end of the spike. This is the same pre-spike state.

---

## Open Questions Resolved by This Spike

1. **UV phase matching required?** YES — without world-space UV (wx/8, wz/8), even the correct texture will have phase mismatch and be visible as a smeared band.

2. **Is the Lit-vs-TerrainLit BRDF gap a problem?** NO — at smooth=0 with a rough texture, the gap is below the visual threshold at the grazing angle Cesar inspects. Pre-existing on all zone meshes.

3. **Is TerrainLit-on-mesh viable without extra work?** NO — renders near-black without a splatmap control texture. Not worth pursuing.

4. **Can either green drop the carve?** NO — both H10 (0.189m) and H18 (0.075m) have terrain intrusion that would be visibly above the green surface.

5. **Are the teeth pitch measurements correct?** The spec estimated ~0.98m per cell based on full-course terrain size. ACTUAL per-hole terrain sizes give: H10=0.1125m, H18=0.0882m per cell. Both are already fine-grained. The apron width floor is much lower than estimated (~0.15m would cover H10's teeth), but the 1.1m width serves other purposes (visual coverage of the transition zone, margin above the teeth floor).
