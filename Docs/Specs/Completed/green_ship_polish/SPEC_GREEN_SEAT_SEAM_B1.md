# SPEC — PASS 2: Green seat/seam re-architecture (B1 terrain-following plane)

**Authored:** 2026-06-01 11:15 CEST / 18:15 JST (Architect)
**Status:** SPEC_READY
**Track:** `green_ship_polish` — PASS 2. Builds on PASS 1 (tier-step-fix, committed `13fe08d6`, Cesar-approved: H7 tier restored, authored step 18.5 cm). Replaces the v1 perimeter-min seat (disproven: a flat scalar datum on sloped terrain floats on the low side OR sinks on the high side — no flat value sits flush all around) and Code's raised-pad v2 draft (not chosen: false-front mound risk).
**Kickoff:** `Use the golfin-implementer subagent on "green_ship_polish" (green-seat-seam-b1)`
**Direction:** Cesar-LOCKED = **(B1)** terrain-following seat PLANE. The green follows its own ground via a fitted plane; authored `relH` rides on top UNCHANGED.

---

## Cesar's acceptance gate (ALL must hold — solution rejected otherwise)
1. **2-tier greens and slopes respected** (authored `relH` — incl. the just-restored 18.5 cm H7 tier — unchanged).
2. **Fringe is a small band** (collar ~0.9 m, no widening, no mound apron).
3. **Green does not float** (edge meets local terrain).
4. **Green and fairway do not overlap; carved hole under the fairway/terrain is NOT visible** (welded seam).
5. **Flag/cup sit ON the green surface** (not floating above / buried below).
6. **Green reads raised/proud, NOT sunken** (B1's fitted plane sits the green on its landform — the v1 sunken-bowl failure must not recur).

Single merged green+fairway mesh pre-approved as the seam fallback if a true vertex weld won't hold (surface types stay distinct via submesh materials).

## Why B1, grounded in data
- v1 used `greenSeatY = terrainBaseY + SampleHeight(centroid)` — a **single flat datum**. On H7 (terrain falls ~0.55 m centroid→W edge) the flat datum floats the low edge (original wall+see-through) OR, when referenced to perimeter-min, sinks the high side into terrain (Cesar's sunken-bowl rejection). **No flat scalar works on sloped ground.**
- B1 replaces the flat datum with a **tilted plane fitted to the green's own perimeter terrain**, so every edge meets its local grade — no float, no sink (#3, #6).
- Verified safe for the authored shape: H7 authored relH-plane gradient = **0.0177 m/m** (H5 0.0144, H9 0.0133 — all gentle, consistent). The terrain macro-gradient (~0.047 m/m on H7) is *different* in magnitude/direction, so B1 must seat on the **terrain plane** while keeping relH's own 0.0177 authored slope ON TOP — NOT replace it (replacing = B2, rejected, doubles/overrides authored slope).
- `green.json` carries NO per-vertex terrain (only scalar `heightDatumY`), so the terrain plane is **fitted at import time** via `terrain.SampleHeight` at the contour vertices — importer-side, bake untouched.

## The fix (importer-only; `HoleGeoImporter.cs`)

### Change 0 — Revert the stopped adaptive-collar attempt (still dirty in working tree)
Remove iter-14 leftovers before building B1: constants `GreenMaxRampSlope`/`GreenMaxCollarMeters` (~L75-86), the `adaptiveCollarWidth` block (~L2558-2581), restore collar + cut dilate to constant `GreenCollarWidth` (0.9 m), restore the collar Y-blend to fixed-width smoothstep (~L2845-2849). (`git checkout` the pre-iter-14 regions of those spans, then apply Changes 1-4.) Collar stays 0.9 m → small fringe (#2).

### Change 1 — Seat on a fitted terrain plane, not a scalar  (#3, #6)
Replace the scalar seat (~L2806 `greenSeatY = terrainBaseY + SampleHeight(centroid) + offset`) with a **plane**:
```
// Fit plane  seatY(x,z) = pa·x + pb·z + pc  to terrain at the green's CONTOUR vertices
// (least-squares, same 3x3 normal-equation solve pattern as the relH plane-fit).
//   for each contour vertex v:  ty = terrainBaseY + terrain.SampleHeight(v)
//   fit (pa,pb,pc) to those (v.x, v.z, ty) samples
// Interior + collar inner ring then use the PLANE, not a constant:
//   seatYAt(x,z) = pa*x + pb*z + pc + effectiveYOffset
//   interior vert:  y = seatYAt(x,z) + relH(x,z)        // authored shape rides on the plane
```
Because the plane passes through the perimeter terrain (least-squares through the contour samples), the green's edge sits AT grade all around — no float, no sink. The interior rides `+ relH` on top, so the **authored slope + 18.5 cm tier are preserved exactly** (#1) — we add a gentle terrain plane *under* relH, we do NOT modify relH.

> CRITICAL (#1 protection): relH is NOT re-tilted or de-tilted. The authored 0.0177 m/m green slope STAYS. B1 adds the terrain plane as the seat; the green's own undulation is unchanged on top. interiorY *spread* will change (the plane is not flat) but the relH *contribution* at each cell is identical — prove via: `(finalY − seatYAt(x,z))` per interior vert == original relH within float epsilon.

### Change 2 — Flag/cup seated on the plane+relH  (#5)
The pin world position (currently seated on the old centroid datum, ~L2666/L2688 — they float now) must use the same surface model:
```
pinY = seatYAt(pinX, pinZ) + relH(pinX, pinZ) + heightDatumY-consistent offset
```
Pull pin XZ from `pinCandidates[defaultPinIndex]` (present in green.json). Cup/flag sit ON the surface, move with it (#5). Confirm the flag GameObject + the hole trigger collider both use this Y.

### Change 3 — Collar ramps plane-edge → terrain  (#2, #3)
Collar inner ring = `seatYAt(edge) + relH(edge)` (on the green); outer ring = per-vertex terrain (`terrain + 0.02`, unchanged). Since the plane edge already sits ~at terrain, the collar now spans only a SMALL residual drop → the existing 0.9 m collar is sufficient, fringe stays a small band (#2). No widening (Change 0 reverted it).

### Change 4 — Weld the seam: coincident verts, ONE shared ring  (#4)
The cut-polygon-overhang approach has FAILED 3 ways (iter-8 gap, iter-14 mound, v1 see-through). Do NOT produce another cut-contour variation. Weld for real:
- Register the collar's **actual outer-ring polygon** (the dilated contour used for the collar CDT) as the green's cut contour — fairway hole edge IS the collar outer edge (no annulus, no GreenCutMargin gap).
- In the fairway pass (`CreateFairwayMesh`), **snap fairway boundary verts on the cut edge to the collar outer-ring verts** (identical XZ AND Y) → coincident verts → watertight, nothing to see through on any slope (#4).
- Point the terrain hole-carve (`SetHoles`) at the **same** outer-ring polygon → ONE ring drives collar-outer + fairway-cut + terrain-carve; they can never drift.
- Keep fairway/collar as separate submeshes/materials → surface types preserved.
- **FALLBACK (pre-approved):** if the vertex weld can't hold cleanly, emit a single merged green+fairway mesh with submesh materials. Try the weld first; if it fails twice → adversarial review, NOT a third cut-contour variation (hard rule).

## What must NOT change
- `relH` / height grid / `bake-green.mjs` / `green.json` / schema v2 — untouched. PASS 1's restored tier is sacred (#1).
- The collar width constant (0.9 m) — adaptive widening reverted (Change 0).
- Non-tier / flat greens behave: on flat terrain the fitted plane ≈ horizontal ≈ today's scalar → near-identical result.

## Honest costs
1. **Physics re-bake REQUIRED.** Seating on a plane changes absolute interior Y → `BakedHeightProvider` heights change → bit-exact gate trips. Inherent to #3/#6. The relH *contribution* is identical (proven per Change 1), so the change is a smooth per-green plane offset, not a shape change — re-establish the gate baseline and document each green's seat-plane delta.
2. **Seam pipeline (Change 4)** = the iters 5-11 + v1 failure family. Highest risk. ONE-ring discipline mandatory; 2 failed weld attempts → adversarial review, not a 3rd variation.
3. Tier *prominence* re-judged here (Cesar's PASS 1 note): once the green sits proud on its plane, confirm the 18.5 cm tier reads acceptably; if still too subtle that's a SEPARATE authoring decision, not a seat bug.

## Files touched
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — revert adaptive-collar (Change 0); plane-fit seat (Change 1); pin/flag/cup Y (Change 2); collar inner ring on plane (Change 3); weld seam + one-ring cut/carve (Change 4).
- Regenerated `Generated/Hole_NN_Geo.unity` meshes.
- Physics bake baseline re-established (mechanical; document per-green plane delta).
- NO `bake-green.mjs`, `green.json`, schema, or `relH` change.

## Hard rules
1. `HoleGeoImporter.cs` ONLY (LIVE importer; verify via `grep MenuItem`; `HoleLiteImporter` deprecated).
2. relH NEVER modified, never re-tilted (no B2). Prove relH contribution unchanged (Change 1 test). #1 non-negotiable.
3. Seat is a fitted PLANE through perimeter terrain — not a scalar (no float/sink), not the terrain macro-slope applied to the surface (that's B2).
4. ONE shared ring: collar-outer = fairway-cut = terrain-carve. No independent cut-contour.
5. Collar width = 0.9 m constant. No widening, no mound.
6. Seam: 2 failed weld attempts → adversarial review, not a 3rd cut variation. Merged-mesh fallback pre-approved.
7. Re-bake: confirm per-green delta is a smooth plane offset (relH contribution epsilon-identical), NOT a shape change.

## Verification — H7 FIRST, gate all 6 points
Per-green report line:
```
Green N: seatPlane(pa,pb,pc)=__  edgeFloatMax=__ edgeSinkMax=__ (both →0)  collarWidth=0.90
  relH-contribution delta vs HEAD: max=__ (must be ~0, proves #1)  pinY=__ onSurface=Y/N  seamWeld=weld|merged
```
H7 gate from the iter-14 reference angles (`screenshots/iter14_fairway_seam_h07_*`):
- **#1** relH contribution epsilon-identical; H7 18.5 cm tier + ridge read intact (2-tier).
- **#2** fringe small band (~0.9 m collar, no apron).
- **#3** edge meets terrain — no float gap, no lip.
- **#4** NO grey carve triangles / gap / z-fight from ANY grazing angle; seam watertight.
- **#5** flag/cup sit ON the surface (place ball near pin — rests on green, not mid-air/buried).
- **#6** green reads proud on its landform, NOT sunken (v1 bowl must not recur).
Frame-extract the orbit video and LOOK before captioning (v1 false-PASS discipline — Code already self-caught this once).

If H7 passes all 6 → reimport rest. **Spot-check matrix:** H9+H14 (steepest), H18 (Fairway_2 + 2-tier), H3+H11 (2-tier — tier intact post-seat), H5 (flattest → plane ≈ flat ≈ near-identical), H6+H12 (next-steepest). Assess iter-15 (raised ring) + iter-16 (off-center) holes: this seat model likely subsumes both — confirm subsumed or re-queue with evidence.

## Definition of done
- Changes 0-4 done; adaptive-collar reverted; ONE ring drives cut/collar/carve.
- Report: per-green seat plane, edgeFloat/Sink →0, relH-contribution delta ~0 (proves #1), pin on surface, weld-or-merged stated.
- H7: all 6 acceptance points pass — Cesar sign-off from the reference angles.
- Spot-check matrix clean; flat greens near-identical; 2-tier holes keep restored tiers; iter-15/16 assessed.
- Physics gate re-established; per-green delta documented as smooth plane offset (shape preserved).
- Importer/green EditMode tests pass (count reported).
- IMPLEMENTER_REPORT content-sanity per Lesson O (describe what each H7 capture shows at the junction + pin).

## Open items to report back
1. Per-green seat-plane (pa,pb,pc) + edgeFloat/Sink table (all 18). Flag any green where the fitted plane leaves a large residual (terrain too non-planar under the green for a single plane → may need follow-up, do NOT widen collar to mask it).
2. Weld held, or fell back to merged mesh? Which per hole.
3. iter-15/16 subsumed or still distinct? Evidence required.
4. relH-contribution delta max across all greens (the #1 proof) — single number, must be ~epsilon.
5. Does the proud green now make the 18.5 cm H7 tier read well, or is a tier-prominence authoring pass wanted (separate task)?
