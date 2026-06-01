# SPEC — Green seating & seam re-architecture (replaces iter-14/15/16)

**Authored:** 2026-05-31 09:30 CEST / 16:30 JST (Architect)
**Status:** SPEC_READY
**Track:** `green_ship_polish` — supersedes the iter-14 adaptive-collar approach (STOPPED, reverted) and folds in iter-15 (raised ring) + iter-16 (off-center raise), which the evidence indicates are the **same rigid-centroid-seat artifact** seen from other holes. To be confirmed against 15/16 captures during verification, not assumed.
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (green-seat-rearch)`

---

## Cesar's four hard acceptance points (ALL must hold — solution rejected otherwise)
1. **2-tier greens and slopes are respected** (authored undulation unchanged).
2. **Fringe is a small band** (no wide collar / mound apron).
3. **Green does not float** (edge meets local terrain).
4. **Green and fairway do not overlap, and the carved hole under the fairway (or terrain, if the green is not on a fairway) is not visible.**

Cesar accepts a single fairway+green mesh if necessary, provided surface types stay distinct. (Read shows we do NOT need a full single mesh — a welded shared ring achieves #4 with far less risk. See Decision below.)

---

## Root cause — VERIFIED in code + bake data (not inferred)

`relH` (the green height grid) is the **authored slope/tier shape** from arrows+ridges via Gauss-Seidel relaxation, **min-shifted to ≥ 0**, terrain-independent (`bake-green.mjs` L672, L765–775). Confirmed against H7 `green.json`: interior field min 0.000 / max 0.474 m; the **West (leading) edge interior relH ≈ 0.035 m** (green wants to sit ~flush there) while centroid-region ≈ 0.251 m. Authored macro-gradient 0.019 m/m SE vs terrain 0.047 m/m E (report D4 line) — **different magnitude AND direction**, so `relH` carries none of the terrain landform.

In `HoleGeoImporter.cs` `CreateGreenMeshCDT`:
- **Seat (L2806):** `greenSeatY = terrainBaseY + terrain.SampleHeight(CENTROID) + effectiveYOffset` — ONE flat datum at **centroid** terrain height.
- **Interior (L2829):** `rawVerts[i].y = greenSeatY + relH` — flat datum + authored shape. (Correct; must stay.)
- **Collar (L2831–2850):** ramps `innerBoundaryY (= greenSeatY + relH)` → `outerRingY (= terrain + 0.02)` over the collar width, per-vertex.

On a green whose terrain falls from centroid to the leading edge (H7: ~0.55 m), the **flat centroid-referenced seat** sits ~0.55 m above the low-edge terrain. The green's own edge relH there is ~0.035 m, so the surface floats ~0.5 m proud → the collar must drop ~0.5 m over 0.9 m (a wall) and its near-vertical face has no horizontal projection → **fails to cover the fairway cut annulus → grey carved-hole triangles show through** (Cesar's correction confirms the "slivers" are the carve, not interpenetration). The stopped iter-14 attempt widened the collar to grade the wall → exposed the flat interior seat as a **mound** (failed #2 and #3).

**Why H7 only:** the defect tracks **centroid→leading-edge terrain drop within the footprint**, not approach steepness (H9/H14 steeper but seat evenly → clean; H18 same Fairway_2 → clean). iter-15/16 ("raised ring", "off-center raise") are very likely this same artifact on other holes — verify.

## Decision: NOT a full single mesh

The seam is already loosely coupled: the green registers `cutContour` into `s_greenCutContours` (L2605); the fairway pass drops triangles whose centroid is inside it (L4811–4853). They share the **polygon** but **not vertices** → projection gap on slope = the see-through. The collar already duplicates boundary verts for per-material UVs (L2913–2929) and already lands its outer ring on per-vertex terrain (L2843). So we achieve #4 by **welding the fairway cut to the collar's exact outer ring** (shared/coincident verts, no annulus) — two meshes, distinct materials/surface types, watertight. A full merged mesh is unnecessary and higher-risk. (Single-mesh remains the fallback if welding proves infeasible; see Risk.)

## The fix (4 coordinated changes, each mapped to an acceptance point)

### Change 1 — Seat datum: centroid-terrain → perimeter-minimum-terrain  (satisfies #3, no-float)
Replace the seat reference (L2800–2812) so the green's **lowest edge** meets local terrain instead of its **centre**:
```
// OLD: greenSeatY = terrainBaseY + SampleHeight(centroid) + effectiveYOffset
// NEW: seat so the green's perimeter sits ON terrain at its lowest edge.
//   perimMinTerrH = min over CONTOUR vertices of terrain.SampleHeight(vert)
//   greenSeatY = terrainBaseY + perimMinTerrH + effectiveYOffset
```
Effect: the whole interior (`greenSeatY + relH`) shifts DOWN by `(centroidTerrH − perimMinTerrH)` (~0.5 m on H7), so the lowest edge sits at terrain → no float. **Interior shape unchanged** (still flat datum + relH) → no slope doubling. relH untouched.

> NOTE (implementer flag): perimeter-MIN seats the lowest point flush and lets higher-terrain edges sit slightly proud (small collar drop there — fine, that's a normal fringe). If any hole's terrain RISES steeply on one edge above the green datum, that edge's collar would need to ramp UP; confirm the collar blend handles innerBoundaryY < outerRingY gracefully (it lerps either direction — verify no clamp assumes down-only). If a hole shows an over-proud high edge, flag for a per-edge follow-up — do NOT widen the collar globally (that was the failed approach).

### Change 2 — Revert adaptive-collar entirely  (satisfies #2, small fringe)
Remove the stopped iter-14 code: constants `GreenMaxRampSlope` (L75–80), `GreenMaxCollarMeters` (L81–86); the `adaptiveCollarWidth` block (L2558–2581); restore the collar dilate and cut dilate to the constant `GreenCollarWidth` (0.9 m); restore the collar Y-blend (L2845–2849) to fixed-width smoothstep (`localRampWidth` → `collarWidth`). With Change 1 the edge is already near terrain, so a 0.9 m collar absorbs only a small drop → **fringe is a small band**. (Simplest path: `git checkout` the pre-iter-14 version of these regions, then apply Changes 1/3/4.)

### Change 3 — Weld fairway cut to collar outer ring  (satisfies #4, no see-through / no overlap)
Today: green registers a separately-built `cutContour = dilate(green, collarWidth − GreenCutMargin)` (L2594–2605); fairway drops triangles inside it; collar "covers" by overhang. Replace with a welded shared boundary:
- Register the collar's **actual outer-ring polygon** (the `dilatedContour` used for the collar CDT, L2723) as the green's cut contour — so the fairway hole edge IS the collar's outer edge (no `GreenCutMargin` gap, no annulus).
- In the fairway pass (`CreateFairwayMesh`, L4749+), after dropping triangles inside the green cut polygon, **snap the fairway boundary vertices that lie on the cut edge to the collar outer-ring vertices** (weld: identical XZ and Y). Coincident verts → watertight seam, no projection gap on slope, nothing to see through.
- Keep meshes/materials separate (fairway mat vs collar mat) → surface types preserved.

> Implementer: the collar outer ring Y = `terrain + 0.02` per-vertex (L2843); the fairway surface near its boundary also follows terrain. Snapping fairway-edge verts to the collar outer-ring verts (same XZ, copy Y) makes them share the exact rim. Confirm the fairway CDT can accept the collar ring as a boundary constraint, or post-process snap nearest fairway-edge verts to ring verts within an epsilon. If neither is clean, FALL BACK to single merged green+fairway mesh with submesh materials (Cesar pre-approved) — but try the weld first.

### Change 4 — Terrain hole-carve follows the same welded ring  (keeps #4 consistent)
The terrain carve (`SetHoles`) currently uses the same `cutContour` (L2632). Point it at the **same welded outer-ring polygon** so terrain is carved exactly under the collar footprint — no carved terrain peeking beyond the collar, no double-coverage z-fight. ONE shared ring drives: collar outer edge, fairway cut, terrain carve. (Extends the existing "one shared cut value" discipline, L56–61, from a scalar to the actual ring.)

## What must NOT change
- **`relH` / the authored height grid** — not read differently, not modified. `bake-green.mjs`, `green.json`, schema v2 — all untouched. Slopes + 2-tier shape preserved (#1).
- **The interior seating model** stays `flatDatum + relH` (flat datum, just referenced to perimeter-min instead of centroid). NO terrain tilt injected into the interior (that was the rejected Option 3 — it would double the authored slope).
- **Collar width** stays the original `GreenCollarWidth` (0.9 m) — adaptive widening fully reverted.

## Honest costs (stated, not hidden)
1. **Physics re-bake REQUIRED.** Change 1 moves the green's absolute Y down on low-seat holes → `BakedHeightProvider` heights change → the bit-exact deterministic gate WILL trip. This is inherent to "green doesn't float" (#3); there is no version without it. The interior *shape* is identical (same relH, same flat datum), so the re-bake is mechanical: regenerate the baked heights, re-establish the gate baseline, and confirm only an expected uniform Y-shift per green (NOT a shape change). Report per-green Y-shift.
2. **Touches the shared cut/collar/fairway seam pipeline** (Changes 3/4) — the exact code family that consumed iters 5–11. Highest risk. Enforce the single-ring-source-of-truth discipline (Change 4). Two failed attempts at the same seam shape ⇒ stop and escalate to adversarial review, do NOT try a third variation.
3. **Multi-session Tier-3 track**, not a quick fix. Likely subsumes iter-15/16.

## Files touched
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — seat datum (Change 1); revert adaptive-collar constants + block + blend (Change 2); register collar outer ring as cut contour + weld fairway edge verts (Change 3); point terrain carve at the same ring (Change 4).
- Regenerated `Generated/Hole_NN_Geo.unity` meshes (reimport output).
- Physics bake baseline / golden heights for the gate (re-established; mechanical, document the per-green Y-shift).
- NO change to `bake-green.mjs`, `green.json`, schema, or `relH`.

## Hard rules
1. `HoleGeoImporter.cs` ONLY for importer logic (LIVE importer; `HoleLiteImporter` deprecated — verify via `grep MenuItem`).
2. Do NOT modify `relH` / the height grid / interior seating model. Interior stays `flatDatum + relH`. #1 is non-negotiable.
3. ONE shared ring drives collar outer edge + fairway cut + terrain carve (Change 4). They can never drift (carry-forward of L56–61 discipline, escalated from scalar to ring).
4. Collar width = constant `GreenCollarWidth` (0.9 m). Adaptive widening fully reverted. No mound.
5. No green-interior tilt from terrain (rejected Option 3 — doubles authored slope).
6. Seam work (Changes 3/4): two failed attempts at the same shape ⇒ adversarial review, not a third variation.
7. Re-bake is mechanical: confirm per-green Y-shift is a uniform translation, NOT a shape change (interiorY spread must match pre-change spread within float epsilon).

## Verification — staged, H7 FIRST
Per-green report line (extend existing `reimport_report.txt`):
```
Green N: centroidTerrH=__ perimMinTerrH=__ seatYShift=__  collarWidth=0.90  edgeDropMax=__  interiorYSpread(before/after)=__/__
```
Gate ALL FOUR points on H7 from the iter-14 reference angles (`iter14_fairway_seam_h07_graze_w_15.png`, `_zoom_lip15.png`, `_zoom_sliver.png`):
- **#1:** interiorY spread unchanged vs HEAD (authored slope intact); ridge + 2-tier read identical (H7 is 2-tier). `BakedHeightProvider` shape delta = pure uniform Y translation.
- **#2:** fringe is a small band (collar ≈ 0.9 m, no wide apron).
- **#3:** green edge sits ON terrain at the low side — no visible float/mound; grazing profile shows green meeting ground, not a pad lip.
- **#4:** NO grey carved-hole triangles at the toe from ANY grazing angle; no green↔fairway gap or z-fight; seam watertight.

If H7 passes all four → reimport rest. **Spot-check matrix:** H9 + H14 (steepest, were clean — confirm still clean, small seatYShift), H18 (Fairway_2, was clean), H5 (flattest → seatYShift ≈ 0, near-identical), H6 + H12 (next-steepest, never captured), the other 2-tier holes (H3/H11) for tier non-regression, and **a hole flagged for iter-15 (raised ring) and iter-16 (off-center) to confirm this fix subsumes them** — if it does, close 15/16 against this track; if not, they stay queued.

## Definition of done
- Changes 1–4 implemented; adaptive-collar code fully reverted.
- ONE ring drives collar outer / fairway cut / terrain carve.
- `reimport_report.txt` shows per-green seatYShift + interiorYSpread(before/after) equal within epsilon (proves shape preserved).
- H7: all four acceptance points pass — Cesar sign-off from the iter-14 reference angles.
- Spot-check matrix clean; flat greens near-identical; steep-clean holes still clean; 2-tier ridge/slope intact; iter-15/16 holes assessed (subsumed or still-queued, stated).
- Physics gate re-established with documented per-green uniform Y-shift (shape unchanged). Importer/green EditMode tests pass (report count).
- IMPLEMENTER_REPORT: content-sanity description of what each H7 verification capture shows at the junction per Lesson O (not "captured").

## Open items to report back
1. Per-green seatYShift table (all 18). Flag any hole where perimeter-min seating makes an opposite (high) edge proud enough to need a per-edge follow-up.
2. Did the weld (Change 3) hold, or did it fall back to single merged mesh? Report which.
3. Confirm iter-15 (raised ring) and iter-16 (off-center) are subsumed by this fix or still distinct. Evidence (captures) required either way.
4. Confirm `BakedHeightProvider` per-green delta is a uniform translation (max - min of the delta field ≈ 0 within epsilon) — proves #1.
