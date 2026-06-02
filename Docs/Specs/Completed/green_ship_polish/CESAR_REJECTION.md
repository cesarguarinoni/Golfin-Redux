# Cesar rejection — terrain-apron (PASS 2 follow-up), 2026-06-02

Rejected AFTER `ARCHITECT_REVIEW_PASS` (red-team gate), on visual inspection of the H10 Scene-view
(apron mesh selected — orange inner/outer ring outline makes the apron band obvious). The collar↔terrain
**sawtooth (geometry) is fixed** — this rejection is about the apron's **material/appearance**, a dimension
the whole 4-gate pipeline never checked (every gate verified runs-per-row sawtooth, not material-matches-terrain).

## Cesar's three observations
1. **Tiling with terrain is all wrong — you can clearly see the apron as a distinct band.** Probably the wrong
   material (should be the SAME as the surrounding terrain) and it should use the same normal map.
2. **Tile size 6 is way too small compared with the terrain.**
3. **Apron width (1.5 m) is unacceptably big.**

## Root-cause diagnosis (Claude Code, verified in code — `HoleGeoImporter.cs`)
The apron was built to match the **fairway FRINGE**, not the **terrain it actually meets**. All three observations
trace to that one mistake.

- **Apron material (L3242–3244):** `CreateZoneMaterial(dataDir, projectRoot, apronMatName, "T_Semirough_Albedo", 6f)`
  — comment literally says *"Reuse the same CreateZoneMaterial path used for fairway fringe (same texture, same shader)."*
  So the apron is **semi-rough albedo, tile 6, albedo-ONLY (no normal, no mask), URP Lit**.
- **The terrain it abuts** (no fairway at H10/H18 → raw terrain) is the **rough catch-all TerrainLayer (index 3, L1475):**
  `T_Rough_Albedo` + **`T_Rough_Normal`**, **tileSize 8**, `normalScale 0.4`, shared matte mask map (smoothness), aniso 16,
  rendered through **URP TerrainLit** (splatmap-blended). (Semi-rough is index 2, tile 6 — what the apron wrongly copied.)
- **`CreateZoneMaterial` (L2352–2385) has NO normal-map slot** — albedo + tileScale only. So "use the same normal map"
  needs a small extension (bind `_BumpMap`/`_NormalMap` + `_BumpScale`), or an apron-specific material path.

### Why each observation follows
1. Wrong layer (semi-rough vs rough) + missing normal/mask → reads as a smooth, dark, distinct band. → use `T_Rough_*`.
2. Apron tile 6 vs rough-layer tile 8 → apron features denser than terrain. → match 8 (world-space).
3. The band is only *visible* because of #1/#2; matching the material makes it blend and the width stops reading as a band.
   **Hard floor: width must stay > ~0.98 m (the `holesResolution` raster-hole cell) or the sawtooth teeth re-appear** —
   that is the apron's whole reason to exist. 1.5 m → can trim toward ~1.0–1.2 m, but not below the cell without also
   shrinking the teeth another way (raise `holesResolution` — rejected in the original spec for memory cost — or a real
   terrain mesh-cut). A sub-1 m width is an architect-level constraint trade, not a free knob.

## Disposition
Cesar chose **escalate to architect** (not loop the implementer). The apron geometry/seam approach (Option C) is sound and
ACCEPTED for sawtooth; only the material/appearance + width need re-speccing. Full consolidated handoff for the claude.ai
Architect chat: **`ARCHITECT_HANDOFF_TERRAIN_APRON_MATERIAL.md`** (same folder). STATUS parked at `AWAITING_ARCHITECT_RESPEC`.
