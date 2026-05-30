# Architect brief — green clipping vs underlying surfaces (iter-4 FAIL)

**Date:** 2026-05-29
**For:** Architect (Cesar's claude.ai chat). Cesar rejected the iter-4 `ARCHITECT_REVIEW_PASS`.
**Symptom:** In `screenshots/h07_pad_fixed_uphill.png` the green's **bottom-left edge is clipped by a protruding mesh** — same stair-stepped over-green signature as the original defect, on a different edge/camera angle. The terrain-pad fix did not resolve it.

**Cesar's two corrections (authoritative — he sees it in-engine and designed the courses):**
1. The protruding surface in this shot is the **FAIRWAY overlay mesh**, not the terrain.
2. **It is not always the fairway** — on some holes the green sits over **terrain (rough)** with no fairway. **Both surfaces need handling.**

This supersedes the earlier "grade a terrain heightmap pad" framing.

---

## What we did (4 iterations)

1. **iter-1:** Baked schema-v2 `green.json` (continuous gradient field + Poisson height integration, ridge as barrier) and deformed the green mesh. FAIL — mesh seated on **per-vertex** terrain, inheriting the ~1.8 m macro-tilt on top of the authored ±0.23 m relief (a 1.8 m ramp nobody authored).
2. **iter-2:** Re-seated the green **flat** on one centroid datum (`greenSeatY`), killing the macro-tilt. Fixed a geo↔Lite coordinate-frame mismatch. Interior relief 0.415 m. PASS.
3. **iter-3:** Cleaned unsanctioned reimport drift. PASS.
4. **iter-4:** Cesar caught a mesh poking through the green's uphill edge. We (wrongly) blamed terrain and graded a **flat heightmap pad** under the green. PASSed on the uphill angle — but the clipping persists on the bottom-left, and Cesar identified the culprit as the fairway mesh (and, generally, whatever surface the green overlays).

## Root cause — CONFIRMED

Once the green is seated **flat** (correct, per the slope authoring), anything underneath it that still follows the ~1.8 m terrain tilt will rise above the flat green on the uphill side and protrude. There are **two underlying surfaces**, and the green+collar footprint is not correctly subtracted from either:

**A. Terrain (rough) — hole-carve UNDER-COVERS the collar.** The green already carves a terrain hole (`CreateGreenMeshes` L2502–2522, like bunkers do at L2120–2150). But its cut contour is a **centroid scale**: `greenContour × greenCollarScale(1.08) × 0.95 ≈ 1.026×` (≈ +0.3 m for H07's radius), while the collar mesh is built by **additive dilation** `DilateContour(greenContour, collarWidth = 0.6 m)` (L2664). The carve covers the green + inner half of the collar; the **outer ~half of the collar ring sits over un-carved terrain**, which protrudes through the collar on the uphill side. This is the defect on **terrain-only** greens.

**B. Fairway — not subtracted at all.** `CreateFlatZoneMeshes` -> `CreateFairwayMesh` (L4084) triangulates the fairway over its **full contour** on per-vertex terrain, with **no green cutout**. Where a green overlays a fairway, the fairway mesh follows the tilt and protrudes ~0.9 m above the flat green on the uphill side. `SetHoles` (terrain holes) does not touch it — it is a mesh, not terrain. The green-over-fairway lift `yBoost = 0.02 m` (L2532) was sized for a terrain-conforming green and is ~45x too small. This is the defect in `h07_pad_fixed_uphill.png`.

**Why iter-4's pad failed:** it graded the terrain **heightmap** — neither the right contour for terrain (the hole-carve, not the heightmap, governs terrain visibility under the green) nor the fairway surface at all. It looked better on the steep uphill face by camera coincidence.

## Recommended fix — subtract green+collar from BOTH surfaces

One footprint (the **dilated collar contour**, `DilateContour(greenContour, collarWidth)`, inset ~10% so the collar overhangs the cut — the bunker pattern), subtracted from both underlying surfaces:

1. **Terrain holes:** replace the green's `1.026×` centroid-scale carve (L2502–2522) with this dilated-collar contour. Then terrain under the entire collar is removed; the collar's outer 10% overhangs onto real terrain to seal the seam.
2. **Fairway mesh:** in `CreateFairwayMesh` (or a post-triangulation filter), **drop fairway triangles whose centroid is inside** the same dilated-collar contour. Green footprints are available (greens built at L233 before fairways at L240; or read `greens.json` as `CreateFlatZoneMeshes` already does at L4049).

Then:
- **Drop the iter-4 heightmap pad** (revert `GreenPadRecord` / the pad pass and `TerrainData_Hole07.asset`) — redundant once terrain is hole-carved under the full collar; likely lets the architect **revert the Hard Rule 4 heightmap-grading amendment** (no heightmap edit needed).
- Guards unchanged: physics unaffected (break = grid force; ball rests on the green mesh via `BakedHeightProvider`); green mesh unchanged; the collar's green->terrain ramp still blends at the edge.
- **Also check bunkers-in-fairway:** a bunker bowl in a fairway has the same exposure (fairway over the bowl). If the fairway doesn't already exclude bunkers, subtract bunker contours in the same triangle-drop pass — confirm during implementation.

**Diagnostic (settles the "same as before" doubt):** after the fix, verify (a) every terrain-hole cell inside the dilated-collar contour is `false`, and (b) zero fairway triangles remain inside it. Re-render from Cesar's exact bottom-left angle plus a terrain-only green (e.g. a hole where the green is not over a fairway) — the green/collar must be the topmost surface everywhere, cut edges hidden under the collar overhang.

## Open questions for the architect

1. **Overhang inset** amount, so the collar reliably hides the cut edge at terrain `holesResolution` (~0.3 m/cell) and the fairway tessellation on thin green lobes.
2. Whether to also cut **bunkers** (and any other overlay) out of the fairway in the same pass.
3. Confirm **reverting the Hard Rule 4 heightmap amendment** now that the fix is hole-carve + mesh-cut only.
