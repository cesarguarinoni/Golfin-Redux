# DRAFT SPEC — Green seat re-architecture v2: RAISED PAD (replaces v1 perimeter-min)

**Status:** DRAFT — authored by Claude Code 2026-06-01 for the Architect (Cesar + claude.ai) to finalize before re-kick. Do NOT kick the implementer on this draft as-is; the open design decisions (§7) need an Architect ruling first.
**Supersedes:** `SPEC.md` / `SPEC_GREEN_SEAT_REARCH.md` (v1, perimeter-min seat) — **DISPROVEN** (made greens sunken). See `CESAR_REJECTION.md`.
**Track:** `green_ship_polish` — green-fidelity ship-blockers (folds in iter-14/15/16).

---

## 1. Why v1 failed (evidence)

Cesar rejected v1 on the H7 orbit video with four observations; architect-side diagnostics confirmed three as real and located the fourth:

| # | Observation | Verdict | Cause |
|---|---|---|---|
| 1 | Green **sunken**, not raised over fairway | REAL — **invalidates v1 premise** | v1 seats the flat datum at `perimMinTerrH` (lowest perimeter terrain). Everywhere else terrain is higher → green sits below grade → bowl. Drops 0.21–0.95 m. |
| 2 | Flag + hole **float** over the green | REAL regression | `HoleGeoImporter.cs` L2666 (flag) / L2688 (cup) still use `terrainBaseY + SampleHeight(centroid)` — the OLD datum. Change 1 lowered the surface but not the pin → floats by ~seatYShift. |
| 3 | Green **looks flat** (no 2-tier) | NOT flat — false alarm | Decoded H7 relH: lower tier ~0 m (NW), upper tier ~0.474 m (SE), diagonal ridge between. Spread 0.465 m preserved (Change 1 is a pure scalar shift). The "flat" read is a perception artifact of the sunken bowl (#1) + orbit angle. |
| 4 | Carved fairway hole **still visible at borders** | CONFIRMED | v1 implemented Change 3a (coincident cut polygon) but NOT 3b (vertex weld). Independent triangulations along the shared polygon leave T-junction cracks → carve shows through on slope. |

**Root tension:** a FLAT datum (required to keep the authored relH-only interior) on SLOPED terrain cannot sit flush all around. It floats on the low side (centroid seat = original bug), sinks on the high side (perimeter-min = v1 bug), or splits the difference (mean). There is no flat-datum scalar that avoids both. v1 only traded float for sink.

---

## 2. New target — a built-up green that sits PROUD (Cesar's decision)

Model the green like a real built green: a raised pad sitting slightly **above** the surrounding grade, with the fairway/surround grading **up** to meet its edge (a natural green front / run-off), and the authored undulation (relH, incl. the 2-tier) preserved on the pad. The carve must be invisible by construction (merged mesh), not by overhang.

### Acceptance points — ALL must hold (rejected otherwise)
1. **2-tier greens and slopes respected** (authored relH unchanged).
2. **Fringe/collar is a small band** (no wide apron *mound* — see §6 mound-vs-raised distinction).
3. **Green does not float** (no visible undercut / wall / carve; the surround grades continuously to terrain).
4. **Green & fairway do not overlap; the carved hole is NOT visible** from ANY grazing angle.
5. **(NEW) Flag and hole-cup sit ON the green surface** at the pin location — no float, no sink.
6. **(NEW) Green reads as raised/proud over the fairway**, NOT sunken into a bowl.

---

## 3. The fix — proposed coordinated changes (all in `HoleGeoImporter.cs`)

### Change A — Seat datum: raised pad (satisfies #6 no-sink, supports #3)
Seat the flat datum so the pad is proud of the surrounding terrain everywhere:
```
// v1 (DISPROVEN): greenSeatY = terrainBaseY + perimMinTerrH + effectiveYOffset
// v2 (raised pad): seat above the HIGHEST perimeter terrain so the pad is proud all around.
//   perimMaxTerrH = max over CONTOUR vertices of terrain.SampleHeight(vert)
//   greenSeatY = terrainBaseY + perimMaxTerrH + raiseOffset       // raiseOffset ~0.10–0.15 m (Architect to set, §7)
```
- Interior stays `greenSeatY + relH` (flat datum + authored relH) → **#1 (slopes/2-tier) preserved, relH untouched.**
- Because the pad's lowest point now sits above all perimeter terrain, the surround must grade **up** to the pad edge (Change C). The drop the surround absorbs on the low side ≈ `(perimMaxTerrH − perimMinTerrH) + raiseOffset` (H7 ≈ 0.8 m; measure per hole).
- **Alternative the Architect may prefer (gentler):** `perimMeanTerrH + raiseOffset` — proud on average, slightly sunk only on the single highest edge, smaller surround drop. Trades a little of #6 for less surround stress on steep holes. Architect to choose (§7).

### Change B — Flag + cup follow the seat (satisfies #5)
Replace the centroid-terrain Y at L2666 / L2688 with the **green surface height at the pin**:
```
// pin from green.json pinCandidates[defaultPinIndex] (worldX/worldZ); fall back to centroid if absent
//   pinRelH = relH sampled at the pin's grid cell
//   flagY = cupY = greenSeatY + pinRelH    (+ tiny cup epsilon for z-fight)
```
So the flag base and cup rim sit exactly on the putting surface regardless of the new seat. (The pin candidate label is currently `centroid_placeholder` — fine; it carries XZ.)

### Change C — Single merged green+surround mesh; fairway grades up to the pad (satisfies #3, #4)
The cut-polygon approach failed twice (v1 3a-only, and the iter-5–11 family). **Take the pre-approved single-merged-mesh path** (Cesar approved it in v1 §Decision as the fallback; promote it to primary):
- Build ONE mesh that carries the green pad (relH interior), a thin collar fringe at pad height, and the surround that grades **down/out** from the collar outer ring to local terrain over a blend band, with the fairway connecting via **shared/coincident vertices** (no separate cut + no annulus + no T-junction).
- Keep distinct **submesh materials** (putting surface / collar / fairway) so surface types stay separable for gameplay classification (`SurfaceMarker`).
- If a fully merged mesh is too invasive, the minimum viable equivalent: keep the fairway a separate mesh but **weld its boundary verts to the collar outer-ring verts (identical XZ AND Y)** — the real Change 3b v1 skipped — AND ensure the surround grade lives in those welded verts. Either way: **coincident vertices, not just coincident polygon.**

### Change D — One boundary drives everything (keeps #4 consistent)
The collar outer ring = the fairway connection edge = the terrain-carve boundary (if any carve remains; a fully merged mesh may not need a terrain hole at all). One source-of-truth ring; no drift (carries forward the v1 single-ring discipline).

---

## 4. The surround grade — how it differs from the rejected iter-14 mound (§6 expanded)

iter-14's "mound" defect = a **flat pad floating at the centroid datum** + a **widened collar** → the wide apron exposed the floating flat pad as a pimple above terrain. The raised-pad surround is different in intent and structure:
- The green is **supposed** to be raised; the surround is a green-front/run-off grading the pad down to the fairway — a real golf feature, not an apron bolted onto a floating pad.
- The grade lives in the **fairway/surround mesh** (graded over a few metres), NOT in a widened *collar* (the collar stays a thin ~0.9 m fringe at pad height).
- **Do NOT re-introduce adaptive collar widening.** If the low-side drop needs N metres to grade naturally, that distance is in the surround/fairway blend band, with a thin constant collar at the pad lip.

The line between "correctly raised green" and "mound defect" is a visual judgement — that is why §7 routes the raise amount + blend width to the Architect.

---

## 5. What must NOT change
- `relH` / the authored height grid / `bake-green.mjs` / `green.json` / schema — untouched. 2-tier + slopes preserved (#1).
- Interior seating model stays `flatDatum + relH` (no terrain tilt injected into the interior — the rejected Option 3 doubles authored slope).
- The collar fringe stays a thin constant band (~0.9 m). No adaptive widening (iter-14 reverted, stays reverted).

---

## 6. Honest costs
1. **Physics re-bake REQUIRED**, and a LARGER absolute Y move than v1 (now raising, not lowering) → `BakedHeightProvider` golden heights change → deterministic gate trips. Mechanical re-baseline; the interior *shape* is identical (relH untouched), so confirm per-green delta is a uniform translation (max−min of delta ≈ 0).
2. **Touches the seam pipeline** (Changes C/D) — the highest-risk code family (consumed iters 5–11 and v1). The cut-polygon shape has now failed THREE ways → **per Hard rule, no more polygon/cut-contour variations.** Merged-mesh or true vertex weld only; if neither holds on the first honest attempt, `IMPLEMENTER_BLOCKED` for adversarial review.
3. **Surround/merged-mesh build is non-trivial importer work** (grading band, shared verts, submesh materials, surface markers). Multi-session.

---

## 7. Open design decisions for the Architect (must be resolved before kick)
1. **Seat datum:** perimeter-MAX + raiseOffset (proud everywhere, biggest surround drop) vs perimeter-MEAN + raiseOffset (gentler, slight sink on highest edge only). Cesar picked "raised pad (proud)" → leans MAX; confirm.
2. **raiseOffset** value (proposed 0.10–0.15 m) — how proud should the pad lip sit above local terrain?
3. **Surround blend-band width** — over how many metres does the fairway grade up to the pad? Constant, or proportional to the local drop (with a cap, to avoid an iter-14-style apron)? Define the slope ceiling that still reads as "green front," not "mound."
4. **Steep holes (H9 perimeter spread ~1 m):** does the raised pad create an unacceptably steep front? Per-hole raiseOffset/blend override, or accept a steeper false front there?
5. **Merged mesh vs welded-separate-meshes** for Change C — pick the primary; the other is the fallback.
6. **Keep a thin collar at all,** or fold it into the pad edge + surround?

---

## 8. Verification (staged, H7 FIRST) — discipline carried forward from the v1 false-PASS
Per-green report line (extend `reimport_report.txt`):
```
Green N: perimMinTerrH=__ perimMeanTerrH=__ perimMaxTerrH=__ raiseOffset=__ greenSeatY=__ surroundDropMax=__ blendWidth=__ interiorYSpread(before/after)=__/__ flagY=__ surfaceYAtPin=__
```
Gate ALL SIX acceptance points on H7 from the **iter-14 reference angles** (`screenshots/iter14_h07_after_graze_w_15.png`, `_zoom_lip15.png`, and the SE toe graze that showed the slivers) — same framing/distance, NOT a far shot.
- **#1** interiorY spread unchanged; 2-tier ridge reads in overhead (compare the relH heatmap).
- **#2** thin collar; no wide apron mound (distinct from §4).
- **#3** no undercut/wall/carve; surround grades continuously to terrain.
- **#4** NO grey carved-hole triangles at ANY grazing angle; seam watertight (coincident verts).
- **#5** flag base + cup rim sit ON the surface (`flagY ≈ surfaceYAtPin` within epsilon; verify in a close capture, not just numbers).
- **#6** green reads raised/proud, not sunken.
- **MANDATORY: frame-extract the canonical orbit video at the seam-facing frames and LOOK before captioning.** v1 captioned "No grey slivers" over visible slivers — that false-PASS must not recur. The caption must match frame-extracted truth.

Spot-check matrix after H7 passes: H9+H14 (steepest spread), H18 (Fairway_2), H5 (flattest → near-identical), H6+H12, H3/H11 (2-tier non-regression), and the iter-15/16-flagged holes (confirm subsumed or requeue).

## 9. Definition of done
- Changes A–D implemented; flag/cup sit on the surface; merged-mesh (or true vertex weld) seam — coincident verts.
- `reimport_report.txt` per-green line incl. flagY vs surfaceYAtPin and interiorYSpread(before/after) equal within epsilon.
- H7: all SIX acceptance points pass from the iter-14 reference framing; orbit video frame-verified (caption matches truth); Cesar sign-off.
- Spot-check matrix clean; 2-tier intact; physics gate re-established with documented uniform Y-shift.
- Importer/green EditMode tests pass (report count). IMPLEMENTER_REPORT content-sanity descriptions (Lesson O), not "captured".
