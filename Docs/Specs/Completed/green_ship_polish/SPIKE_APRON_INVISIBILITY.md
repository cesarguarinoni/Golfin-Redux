# SPIKE — terrain-apron invisibility investigation (green_ship_polish)

**Authored:** 2026-06-02 11:45 CEST / 18:45 JST (Architect)
**Status:** SPIKE — investigation only, NO production code changes, NO close-out. Produces findings + captures that decide the final apron spec.
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (apron-invisibility-spike)`
**Why:** Cesar's bar is ABSOLUTE — *"if I can tell the apron is there without selecting it, we have failed."* The apron mesh uses URP **Lit**; the rough terrain uses URP **TerrainLit**. Every existing zone mesh (collar/fairway/tee) is Lit-imitating-TerrainLit and is "accepted" only because it sits on a *different* texture (fairway/collar/tee) where a slight lighting difference reads as an expected surface transition. The apron is the FIRST case where a Lit mesh must vanish into the SAME rough texture — so any shader-driven lighting difference has nowhere to hide. Before committing the material spec we must know, by evidence, whether "invisible" is even achievable with a mesh, and by which method. **Do NOT ship anything from this spike — measure and report.**

---

## The two questions this spike must answer

### Q1 — Can a mesh be made INDISTINGUISHABLE from the rough terrain at the grazing arc?
Test, in ascending order of effort, on **H10** (the worse case — 0.19 m proud rim — and not yet visually scrutinized). Each test = render the apron with that approach, capture from the grazing arc with the apron **NOT selected** (no gizmo), and judge against the surrounding rough. STOP at the first approach that is genuinely invisible.

- **T1 — Hardened URP Lit match.** Apron on URP Lit with EVERY channel forced to the rough TerrainLayer (index 3): `T_Rough_Albedo`, `T_Rough_Normal` (normalScale 0.4), the shared matte mask (smoothness 0, AO), tile 8, aniso 16 — AND world-space UV **phase** matched to the terrain splat projection (same tile size is not enough; the texture pattern must be CONTINUOUS across the seam — confirm the apron's `(wx/tileSize, wz/tileSize)` origin/phase equals the terrain's, so the rough pattern doesn't jump at the apron edge).
  - Capture T1 vs terrain at grazing. Is there ANY visible difference — lighting/specular at grazing angle, normal-map relief direction, texture-phase jump, color? Report precisely WHAT differs (this is the data that proves/disproves the cheap path).

- **T2 — TerrainLit (or matched-BRDF) on the mesh.** Only if T1 shows a residual shader-lighting difference. Try rendering the apron mesh through **URP TerrainLit** as a single-layer (rough) setup, OR a Lit variant tuned to match TerrainLit's BRDF. Determine whether TerrainLit can even be driven on a non-terrain mesh in this project (it expects splatmap/control-texture inputs — does it fall back gracefully to a single layer, or break?).
  - Capture T2 vs terrain at grazing. Does eliminating the shader-class difference close the gap? Report whether TerrainLit-on-mesh is feasible here at all.

### Q2 — Is the apron even NEEDED on H10 / H18? (the "remove the problem" path)
The apron exists to hide the raster-hole sawtooth, and the hole exists because terrain intrudes above the green surface. Intrusion is small: **H18 ~0.075 m, H10 ~0.19 m**. Test whether the carve (and therefore the apron) can simply be dropped:

- **Q2a — No carve, pad covers it.** For H18 first (smallest intrusion), disable the terrain hole-carve for that green and reimport. Does the green pad/collar fully cover the un-carved terrain, or does raw terrain poke up through the putting surface anywhere? Capture top-down + grazing. Measure max terrain-above-surface penetration (reuse the per-green intrusion probe from `ARCHITECT_HANDOFF_TERRAIN_SEAM.md`). If the pad covers it → **no hole, no seam, no apron needed on H18.** Repeat for H10 (0.19 m — likelier to poke through).
  - REPORT per green: does terrain penetrate the surface with no carve? Max penetration (m)? Visible?

- **Q2b — (only if Q2a shows penetration) local high-res holes.** Note `terrainData.holesResolution` is currently unset (→ 2049, ~0.98 m cells). Could it be raised for *just these terrains* to shrink teeth below visibility without the apron? Report the memory cost at the resolution that makes teeth invisible (e.g. 4096 ≈ ? MB, 8192 ≈ ~64 MB per terrain) and whether 2 terrains' worth is tolerable on mobile. This is a fallback, not preferred.

## What to produce (deliverable)
A findings doc `SPIKE_FINDINGS_APRON_INVISIBILITY.md` in the task folder with:
1. **Q1 verdict:** the lowest-effort approach (T1 / T2 / neither) that is genuinely invisible at the grazing arc — with the comparison captures (apron NOT selected) as evidence. If NOTHING with a mesh is invisible, say so plainly.
2. **Q2 verdict:** per H10/H18 — can the carve be dropped (pad covers intrusion)? If yes for either, that green needs no apron at all.
3. **Recommended path** for the final spec, with the trade: e.g. "H18 → drop carve (Q2a passes); H10 → needs carve + TerrainLit apron (T1 failed on grazing specular, T2 invisible)." Whatever the evidence shows.
4. Captures saved to `screenshots/spike_apron/` (T1/T2/Q2a per hole, grazing + top-down, apron unselected, native res, frame-extracted — LOOK before captioning, N=3 discipline).

## Constraints
- **Investigation only.** Use a scratch branch or keep changes uncommitted; do NOT modify the shipped importer behavior, do NOT close anything, do NOT touch the accepted apron geometry / B1 seat / collar↔fairway weld as a side effect. Revert all spike edits after capturing.
- `HoleGeoImporter.cs` is where the apron material/shader is built (L3242–3261) and where holesResolution/carve live — spike edits there are fine but MUST be reverted (this is a measurement, not a fix).
- Judge invisibility from the **grazing arc** (Cesar's inspection angle), apron **unselected**. Top-down alone is not sufficient (it hid the original sawtooth).
- Read `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` first (CLAUDE.md rule).

## Acceptance of the spike
Not a ship gate — the spike is "done" when the findings doc answers Q1 + Q2 with evidence captures, and recommends a path. Cesar + Architect then choose the final spec from the findings. No production commit from the spike itself (captures + findings doc may be committed as evidence).

## Why each question matters to the final spec
- If **T1 is invisible** → the current material spec (hardened) ships as-is. Cheapest.
- If **only T2 is invisible** → final spec switches the apron to TerrainLit/matched-BRDF (bigger importer change, but the only thing that meets the bar).
- If **NO mesh is invisible** → we MUST go Q2 (remove the carve/apron) or accept teeth-shrink (Q2b). 
- If **Q2a passes** for a green → that green is solved with LESS code (no apron at all), regardless of Q1.
The spike prevents committing a material spec that can't meet Cesar's absolute bar — which a blind attempt would likely do, since the Lit-vs-TerrainLit difference is exactly at the grazing angle he inspects.
