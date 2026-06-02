# ARCHITECT HANDOFF — collar↔terrain seam (green_ship_polish, PASS 2 follow-up)

**Written:** 2026-06-01 (Claude Code, post green-seat-seam-b1 N=3 self-review ESCALATE)
**For:** the claude.ai Architect chat, to author the collar↔terrain fix spec.
**One-line:** The B1 fitted-plane seat is accepted and the CDT weld cleanly fixes the collar↔**fairway** seam; the remaining ship-blocker is a **collar↔terrain** sawtooth on greens not bordered by fairway (H18 is the canonical case). This file consolidates everything needed to spec the fix.

---

## Cesar's rulings that bound this work (do not relitigate)
- **Q1 — CDT-hole-constraint is BLESSED** for the collar↔fairway seam. The implementer kept green and fairway as separate meshes/vertex-buffers but inserts the collar outer-ring polygon as a CDT constraint hole in the fairway CDT, so the boundary verts coincide by construction (identical XZ + identical Y formula). Cesar accepted this as honoring Hard Rule 6's intent. **Keep it — do not reopen or re-weld the collar↔fairway seam.**
- **Q2 — the collar↔terrain sawtooth goes to the Architect** (this doc). It is a *distinct* seam from collar↔fairway and was not anticipated as separate in the B1 spec.
- **Hard Rule 6 still binds:** the seam has now burned cut-polygon-overhang (iter-8 gap, iter-14 mound, v1 see-through), vertex-snap weld (H7 SW shimmer), and edge-projection weld (H18). No 4th *collar↔fairway* seam-mechanism variation. The collar↔terrain fix is a new sub-problem, not another attempt at the same seam.

---

## What's fixed vs not (verified — Claude Code looked at both frames)
- **FIXED — seat re-arch:** terrain-following fitted plane (least-squares through contour-vertex terrain samples). Resolved Cesar-rejection #1/#2/#3/#6 — green reads proud (not sunken), flag/cup on surface, 2-tier preserved, `relH`-contribution delta = 0.000 (authored shape mathematically untouched). Gates #1,2,3,5,6 PASS.
- **FIXED — collar↔fairway seam (H7):** CDT-hole-constraint weld → 0 runs/row, visually clean. `screenshots/b1_merged_h07_canonical_sw.png`.
- **NOT FIXED — collar↔terrain seam (H18):** dashed white sawtooth along the south collar perimeter, visible to the naked eye. `screenshots/b1_merged_h18_t7s.png`. The implementer's "0 runs/row on all 18" was measured at a pure-white threshold; at a realistic threshold H18 = ~20 runs/row (prior FAIL was 16). The implementer's "it's a cart path" reattribution is FALSE — cart path is on the north side, teeth are on the south collar edge (verified against `cart-paths.json`).

---

## ROOT CAUSE (verified in code)
The collar↔fairway seam is welded via shared CDT boundary verts. The collar↔**terrain** boundary is NOT a vertex boundary at all: the terrain hole is carved by a **rasterized `SetHoles` mask** quantized to `terrainData.holesResolution`. A continuous collar polygon edge meeting a stair-stepped raster hole edge → T-junctions → sawtooth. H7 hides it (perimeter is mostly fairway, which IS welded); H18 has no adjacent fairway (8.8 m gap) so its entire perimeter is collar↔terrain → teeth all around. Hard Rule 4's "one shared ring (collar-outer = fairway-cut = terrain-carve)" aimed at this but cannot achieve it, because `SetHoles` is a boolean raster mask, not a shared-vertex boundary.

### Code anchors (`Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`, 6468 lines)
- **The raster carve (defect source):** L2638–2687 — `cutContour = DilateContour(activeContour, GreenCollarWidth)` (continuous collar outer ring), then per-cell `IsInsideContour(cellWorldX, cellWorldZ, cutContour)` on the holes grid → `terrainData.SetHoles`. Quantization happens here.
- `IsInsideContour` raster test: L1984. Other holes-grid carve paths: L294–380, L2044–2101, L2441–2443.
- **Collar mesh build + outer-ring Y** (where the collar meets the carved edge): `CreateGreenMeshCDT` L2816; collar blend + `outerRingY = terrainBaseY + perVertTerrainH - GreenSkirtDepth` at L2934 (`GreenSkirtDepth = -0.02`, collar sits 2 cm above terrain).
- **The blessed weld (preserve — do NOT touch):** `CDTTriangulateWithHoles` L4574 / HoleSeeds L4661, fed by `s_greenCentroids` L89. `CreateFairwayMesh` L4977.
- Constants: `GreenCollarWidth = 0.9f` (L53), `GreenCutMargin = 0.25f` (L61), `GreenSkirtDepth = -0.02f` (L72).

---

## Reading list (ordered)
1. `Docs/Specs/Active/green_ship_polish/SELF_REVIEW.md` — N=3 review, the full diagnosis + measurements + cart-path refutation.
2. `screenshots/b1_merged_h07_canonical_sw.png` (clean) vs `screenshots/b1_merged_h18_t7s.png` (teeth) — side by side.
3. `Docs/Specs/Active/green_ship_polish/SPEC_GREEN_SEAT_SEAM_B1.md` — the B1 spec (and its assumption that "one ring" would cover collar↔terrain — the broken assumption).
4. `Docs/Specs/Active/green_ship_polish/IMPLEMENTER_REPORT.md` — what shipped (CDT-hole-constraint), per-hole data, the over-strict measurements to be skeptical of.
5. `HoleGeoImporter.cs` regions above.
6. `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` — mandatory before touching fringe/border mesh code (CLAUDE.md rule); directly on point.
7. `Docs/TellCode.md` CURRENT STATE — the PASS structure + seam failure family.
8. Tee-skirt precedent — analogous "pad meets sloped terrain," solved with a linear-slope ramp/skirt (importer ~L3471–3556 per the B1 spec reference). Model for a terrain-side apron.
9. H18 data: `Tools/UHoleGeo/output/lomond-country-club/export/hole-18/greens.json` + `fairway-contours.json` (confirms no adjacent fairway → canonical test hole).

---

## Problem statement (for the new spec)
The B1 fitted-plane seat is accepted, and the CDT-hole-constraint cleanly welds the collar↔**fairway** seam (Cesar-blessed). The remaining defect: where a green's collar meets **raw terrain** (not fairway), the terrain hole is carved by a rasterized `SetHoles` mask quantized to `terrainData.holesResolution`, so its stair-stepped edge can't align with the collar's continuous polygon → a T-junction sawtooth. Visible on any green not fully bordered by fairway; **H18 (no adjacent fairway) is the canonical case and primary acceptance hole.**

**Author a fix for the collar↔terrain boundary that:**
- does NOT reopen or re-weld the collar↔fairway seam (CDT approach stays);
- does NOT introduce a new collar↔fairway seam mechanism (Hard Rule 6);
- preserves the fitted-plane seat (gates #1,2,3,5,6) and `relH` (delta must stay ~0);
- keeps surface classification intact (green/collar/fairway/terrain separately identifiable for physics + rendering).

**Option space already surfaced (cheapest → costliest) — for the Architect to evaluate/choose:**
- (a) accept as cosmetic on non-fairway greens (likely rejected — it's a visible ship-blocker);
- (b) a thin terrain-side fringe apron the collar laps *over*, so the visible edge is mesh-over-mesh not mesh-meets-raster (tee-skirt-style — likely smallest real fix);
- (c) replace the raster `SetHoles` carve with a real terrain mesh-cut along the collar polygon (bigger change, but a true vertex boundary);
- (d) extend the merged/CDT mesh to absorb a terrain apron ring.

**Acceptance gate for the fix:** runs-per-row ≤ 3 (at a realistic lighter-pixel threshold, not pure-white) on ALL 18 holes — measured per hole, no hole assumed clean (H18 was eyeballed-clean once and wasn't). H18 + H7 are the mandatory before/after holes. Verify via orbit video frame-extraction at native res on the grazing arc; LOOK before captioning (a false "clean" caption has slipped through twice on this track).

## Per-green diagnostic — which greens actually need the fix (computed 2026-06-01)
Computed from the export contours + `heightmap.raw` (pre-depression terrain, the same surface the importer fits the seat plane to). Cross-validated: H16 sink/float (0.008/0.009 m) matches `reimport_report.txt` exactly. Script: `/tmp/green_seam_diag.py` (reproducible).

- **(a) fairway within collar range (0.9 m) of the green perimeter?** 16/18 greens = YES (100% of perimeter) → collar↔fairway seam, already welded clean by the CDT approach. **Only H10 and H18 = NO fairway** (nearest fairway 12.2 m / 22.0 m away) → entire perimeter is collar↔terrain → the rasterized `SetHoles` seam → sawtooth.
- **(b) does real terrain exceed the seated surface, and by how much?** `sinkMax` = max height natural terrain rises above the fitted seat plane at the green edge:

| border | holes | sinkMax range |
|---|---|---|
| FAIRWAY (welded — fine) | 1–9, 11–17 (16 greens) | 0.008–0.216 m (covered by welded fairway mesh) |
| **TERRAIN (needs fix)** | **H10** | **0.189 m** (terrain proud — visible rim lip + raster T-junction) |
| **TERRAIN (needs fix)** | **H18** | **0.075 m** (gentle terrain — mostly flat raster T-junction) |

**Fix scope:** only terrain-bordered greens (H10, H18 on this course). The fix MUST handle the terrain-proud case (H10 ~0.19 m above the seat → carved rim stands up), not just the flat T-junction (H18). Keep it general for future terrain-bordered greens. H10 has not been visually inspected yet — likely worse than H18; shoot it before/after.

## Suggested deliverable
`Docs/Specs/Active/green_ship_polish/SPEC_GREEN_SEAT_TERRAIN_FRINGE.md` (same task folder), then set `STATUS.md = SPEC_READY` and kick `golfin-implementer`.
