# SPIKE FINDINGS — Carve-Inset (drop the apron) — green_ship_polish

**Executed:** 2026-06-02 (Claude Code main thread / architect-metrics)
**Supersedes recommendation in:** `SPIKE_FINDINGS_APRON_INVISIBILITY.md` (apron-material path — Cesar rejected: apron can't blend imperceptibly).
**Cesar's directive:** "Do away with the apron. Move the carve a bit more *inside* the fringe ring. That may hide the staircase holes poking out."

---

## Verdict: WORKS on H10 (the worst case). Recommend adopting.

Insetting the terrain hole-carve inward (so its rasterized staircase teeth land **under** the collar drape) and **dropping the apron mesh entirely** produces a clean collar→terrain boundary with no visible teeth, no band, and no terrain poke-through. The only visible boundary is the collar's own smooth mesh outer edge meeting natural rough terrain — i.e. the **expected fringe→rough transition** of a real green.

## Why it works (geometry)
- The carve boundary used to sit AT the collar outer edge (`DilateContour(activeContour, GreenCollarWidth=0.9m)`), so the raster teeth coincided with the collar edge and poked into open air → sawtooth. The apron was a band to cover them (rejected: can't colour-match terrain).
- The collar mesh **drapes over the terrain**: its outer ring Y = `terrain.SampleHeight + 0.02m` (`HoleGeoImporter.cs` L2981), blending to the seat plane at the green edge. So terrain *under* the collar stays buried.
- Insetting the carve to `DilateContour(activeContour, 0.9 − Δ)` pulls the teeth to radius `0.9−Δ`, **under** the collar. The visible boundary becomes the collar's smooth outer mesh edge (clean by construction). Teeth never reach open air.

## Read-only poke-through probe (on the existing collar mesh — valid because the inset doesn't change the collar)
Terrain-above-collar in the now-exposed band `[0.9−Δ, 0.9]`, H10 (worst intrusion, 0.19m):

| Inset Δ | Exposed band (d≥) | Verts poking through | Max poke-through |
|---|---|---|---|
| 0.15m | 0.75 | 0 | −0.013m (collar 1.3cm above terrain everywhere) |
| **0.20m** | **0.70** | **1** | **+0.003m (2.7mm — negligible)** |
| 0.30m | 0.60 | 3 | +0.013m |
| 0.45m | 0.45 | 5 | +0.016m |

Global max poke-through anywhere under the collar = **1.76cm** — vs **16.9cm** for the no-carve test (Q2a). Teeth pitch ≈ 0.11m, so Δ=0.20 tucks the staircase ~1.8 cells under the collar edge with essentially zero poke-through. **Chosen Δ = 0.20m.**

## Visual confirmation (H10, reimported with Δ=0.20, apron dropped, collar UNSELECTED)
- `screenshots/spike_apron/carveinset_h10_seam_closeup_nw.png` — tight low-grazing NW seam: smooth curved collar→rough boundary, no teeth.
- `screenshots/spike_apron/carveinset_h10_seam_closeup_sw.png` — SW seam: clean diagonal boundary, no teeth (white = adjacent bunker).
- `screenshots/spike_apron/carveinset_h10_topdown.png` — near top-down: clean elliptical green edge, no sawtooth ring, no band, no terrain through the putting surface.
- `screenshots/spike_apron/carveinset_h10_graze_nw.png`, `carveinset_h10_graze_n_low.png` — wide grazing arcs: green+collar reads as a natural green in rough.

## Post-reimport geometry verification (script-execute)
- `GreenApron_1` GameObject: **absent** (apron dropped). ✓
- Green-surface verts over solid (un-carved) terrain: **0 / 1563** → carve still fully covers the putting surface; no poke-through inside the green. ✓
- Total carved cells = 41987 (vs ~42809 original) — only ~820 fewer, consistent with a 0.20m boundary inset (not under-carving). ✓
- `[SPIKE2] Green 1: carve INSET 0.20m -> boundary at collarWidth-inset=0.70m`. ✓

## H18 — not yet reimported, but geometrically strictly safer
H18 intrusion is 0.075m vs H10's 0.19m, so the analytic poke-through threshold (`intrusion > ((1−t)/t)·0.02`) is met with even more margin. Expect a clean result; **recommend a confirming reimport+capture before finalizing the spec.**

## PROMOTED TO PRODUCTION + VERIFIED (2026-06-02, Cesar approved)
The scratch was promoted to the real fix in `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`:
- New const `GreenCarveInset = 0.20f` (replaces `SpikeCarveInset`; full doc comment).
- Production `carveContour` inset for terrain-bordered greens (gated on the inline fairway-inside check), before the raster loop; raster carves `carveContour`; `cutContour` (collar outer) still registered for the fairway pass unchanged → fairway weld untouched.
- **Apron path DELETED**: removed the `CreateGreenTerrainApron` call site, the whole `CreateGreenTerrainApron` method (~175 lines), and the `GreenTerrainApronWidth` const. Net importer diff vs HEAD: **+72 / −3** (the apron approach was +215).
- `reimport_report.txt` diagnostic line corrected to report the actual carve boundary (`boundaryWidth=0.70m carveInset=0.20m (no apron)`).

**Verification:**
- Compile: clean (only pre-existing unrelated `.meta` GUID warnings).
- H10 + H18 reimported via production code: `GreenApron_1` absent (0 `GreenApron` refs in both `.unity` scenes); green-surface verts over un-carved terrain = 0/1563 (H10), 0/2104 (H18); exposed-band poke-through max +2.7mm (H10), 0 / −10.2mm (H18, collar above terrain everywhere).
- Visual (collar unselected): clean collar→rough seam at grazing, no teeth, no band — `screenshots/spike_apron/carveinset_h10_*.png`, `carveinset_h18_*.png`.
- EditMode tests: **359 pass / 0 fail / 3 skip** (baseline-identical; the 3 skips are pre-existing HoleComplete stage-C1 skips).

**Status:** `AWAITING_CESAR_SHIP` — code + scenes done and verified, NOT yet committed. On Cesar's "Done": commit importer + H10/H18 Geo scenes/terrain (scoped), push, move task folder to `Completed/`.

## Recommended path for the final spec
1. Make the inset-carve the default for terrain-bordered greens: terrain carve = `DilateContour(activeContour, GreenCollarWidth − GreenCarveInset)`, `GreenCarveInset = 0.20m`. Keep `cutContour` = collar outer ring for the fairway weld (untouched; terrain-bordered greens have no fairway anyway).
2. Drop the apron path entirely for terrain-bordered greens (remove `CreateGreenTerrainApron` call; the apron material/width problem disappears with the mesh).
3. Acceptance gate: H10 + H18 grazing-arc captures (collar unselected) show no teeth, no band, no poke-through, and the green-interior carve coverage check stays 0 solid-under-green.
