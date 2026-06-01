# Cesar rejection — green-seat-rearch (iter-rearch), 2026-06-01

Rejected mid-pipeline (after SELF_REVIEW_FAIL) on visual inspection of the H7 orbit video. Four observations, with architect-side diagnostics appended.

## Cesar's four observations
1. **The green is sunken instead of raised over the fairway.**
2. **The flag and hole are floating over the green.**
3. **The green seems flat (no 2-tier separation)** — "check geometry to confirm."
4. **The hole in the fairway is still visible at the borders.**

## Diagnostic findings (Claude Code, same session)

**#1 — REAL; flaw in the spec's core approach.** Spec Change 1 seats the green datum at `perimMinTerrH` (the single lowest terrain point over the contour). Everywhere else on the perimeter terrain is higher → the green sits below local grade → sunken bowl. Per-green drops 0.21–0.95 m (IMPLEMENTER_REPORT table). Perimeter-min traded the iter-14 low-side float for a high-side sink. A flat datum on sloped terrain cannot sit flush all around — it floats (centroid seat) or sinks (perimeter-min) or both (mean). **Perimeter-min is the wrong datum; the seat model needs an architect decision.**

**#2 — REAL regression from Change 1.** `HoleGeoImporter.cs` L2666 (flag) and L2688 (cup) still compute Y as `terrainBaseY + terrain.SampleHeight(centroid) + greenYOffset + yBoost` — the OLD centroid datum. Change 1 lowered the green surface to perimeter-min (~0.78 m on H7) but did not update flag/cup → they float by ~seatYShift. Fix: place flag/cup at the green SURFACE height at the pin (`greenSeatY + relH(pin)`), pin from `pinCandidates[defaultPinIndex]`.

**#3 — NOT flat; 2-tier present in data.** Decoded H7 `green.json heightGridBase64` (54×61, cellSize 0.5): lower tier ~0 m (NW), upper tier ~0.474 m (SE), diagonal ridge between them (steep-cell band, max slope 0.627 m/m). interiorY spread 0.465 m preserved (Change 1 is a pure scalar shift). The "flat" read is a perception artifact of the sunken bowl (#1) + orbit angle, not lost geometry. Heatmap: `/tmp/h07_relH_heatmap.png` (shared in chat).

**#4 — CONFIRMED (also caught by self-review).** Change **3b** (snap fairway-edge verts to collar outer-ring verts) was never implemented; only 3a (coincident cut polygon). Independent triangulations along the shared polygon leave T-junction cracks → carve shows through on slope. Visible in the implementer's own canonical video `videos/rearch_h07_orbit.mp4` ~t=7 s (frame extracted, slivers at the SE seam) while the caption falsely reads "No grey slivers."

## Disposition
Spec premise (perimeter-min flat seat) disproven. Needs an Architect seat-datum re-spec. #2 (flag/cup follow seat) and #4 (weld or pre-approved single-merged-mesh) are mechanical fixes that ride along with whatever seat model is chosen. Awaiting Cesar's direction.
