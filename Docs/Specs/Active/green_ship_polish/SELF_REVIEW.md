# Self-Review — `green_ship_polish` iter-rearch (green-seat re-architecture)

**Iteration of self-review:** N=1 for the rearch track (the file's prior content was N=2 of the iter-13 2-tier-gate amendment; that track was archived when iter-14 began. iter-14 was STOPPED and reverted. This rearch is a fresh implementation off SPEC_GREEN_SEAT_REARCH.md, so it's N=1.)
**Reviewer:** golfin-self-reviewer
**Reviewed at:** 2026-06-01 08:34 CEST (system clock)
**Verdict:** **BACK_TO_IMPLEMENTER** (FAIL)

---

## Step 1 — Independent pixel scan (no spec, no report)

I extracted 16 frames at 2 fps from the implementer's own declared canonical video `videos/rearch_h07_orbit.mp4` (the deliverable that gates this whole task per CLAUDE.md Rule 17), and inspected the rearch H7 still captures.

### Rearch canonical orbit video — frames 13/14/15/16

The H7 green sits on a hillside, flag visible center-top. The collar reads as a thin ring of slightly darker green around a brighter putting surface. **Along the front (south/south-east) perimeter where the green meets the fairway, in frames 14, 15, and 16, I count a continuous row of approximately 6 to 15 small, distinctly TRIANGULAR, light-grey/whitish pennant shapes** projecting radially outward from the collar edge into the fairway. They are uniformly small, uniformly shaped, uniformly spaced — and they trail the entire SE rim. They are clearly NOT bunkers: H7 has 4 bunkers per the overhead and one is independently visible (top-right of green in frame 16, separated from the row). The pennant row sits ON the seam.

Frame 16 is the worst case — the long row of white triangles is unmistakable. Frame 13 shows fewer of them (different orbit position, different shading). Frame 15 shows ~6-8 along the lower seam. The phenomenon is consistent with carved-hole terrain showing through where the fairway-mesh triangle row and the collar-mesh triangle row do not share edges — i.e. the classic see-through gap when two polygons are coincident but their triangulations are not vertex-welded.

### Canonical still `rearch_h07_graze_w15.png`

The H7 green is in the mid-distance occupying ~12% of the frame width. The camera is much further from the green than in the iter-14 reference angle (`iter14_fairway_seam_h07_graze_w_15.png`, where the green filled ~50% of frame and the slivers were unambiguous). At this framing distance, ~30cm slivers would be 3–5 pixels and within image noise. I cannot confirm presence or absence of slivers from this still alone. A zoom-crop of the still (`/tmp/rearch_h07_canonical_cropZoom.png`) reveals what looks like a row of small white shapes along the front edge of the green; whether these are H7's front bunkers or sliver-triangles is ambiguous at this framing. **The canonical still does not reproduce the iter-14 reference angle the spec required for gating** (spec Verification line 95–99).

### Spot-check stills H3, H5, H6, H9, H11, H12, H14, H18

Most of these are taken from cameras positioned OUTSIDE the playable terrain volume — the dominant element in each is the grey/blue terrain-skirt seen from below. The greens themselves appear as tiny slivers near the horizon (10–30 pixels tall in a 1024-pixel-tall frame). At this framing:

- H3, H11 (2-tier non-regression gate): you cannot read 2-tier preservation from these angles at all.
- H9 (steepest, the borderline 1.05 m/m collar concern): the green and its collar occupy ~80×30 pixels — you cannot tell whether the collar is a "small fringe band" (PASS #2) or a near-vertical wall (FAIL #2).
- H14 (2-tier, seatYShift=0.563m): same problem — visible as a thin strip with one ambiguous white shape that might be a bunker or might be a sliver.
- H5 (flattest): the green is not clearly identifiable in the frame.
- H6, H12, H18: same — distant slivers of greens against terrain skirts.

These captures **do not gate** any acceptance criterion. They are not equivalent to the iter-14 reference angles in framing or detail.

### Iter-14 reference (the canonical defect this rearch must eliminate)

`screenshots/redteam_h07_toe_slivers_canonical_nativecrop.png` — a tight crop showing a diagonal seam between fairway (lower-left) and green (upper-right) with 4–5 distinct grey/white triangular slivers along the seam. This is the unambiguous defect signature.

`screenshots/iter14_fairway_seam_h07_graze_w_15.png` — tight orbit-grazer at the H7 green where the slivers can be clearly observed at the lower-front edge.

### Visual diff conclusion (Step 1 — before reading the report)

The rearch canonical orbit video shows what appears to be the same toe-sliver phenomenon along the H7 green's SE rim, visible in multiple consecutive frames. The signature shape (small triangular pennants projecting outward in a row at the green-fairway seam) matches the iter-14 redteam canonical defect. The slivers in the rearch video are slightly lighter in tone (whiter, less grey) than iter-14, but the shape, position, count, and seam location pattern are consistent with the same root cause: vertex misalignment between two polygonally-coincident but independently-triangulated meshes. **The defect this rearch was specifically designed to eliminate appears to still be present.**

---

## Step 2 — Spec / Code verification

### Claim #1: "Weld held" — polygon-coincidence vs vertex-welding

**Spec Change 3 (lines 56–58):** explicitly required (a) registering the collar's outer-ring polygon as the green's cut contour AND (b) "**snap the fairway boundary vertices that lie on the cut edge to the collar outer-ring vertices** (weld: identical XZ and Y). Coincident verts → watertight seam, no projection gap on slope, nothing to see through." Sub-step (b) is the actual weld.

**Spec line 60 (warning):** "Confirm the fairway CDT can accept the collar ring as a boundary constraint, or post-process snap nearest fairway-edge verts to ring verts within an epsilon. **If neither is clean, FALL BACK to single merged green+fairway mesh with submesh materials (Cesar pre-approved)** — but try the weld first."

**What was implemented (code at HoleGeoImporter.cs L2562–2611, L4828–4877):**
- Change 3a (cut polygon = collar outer ring): IMPLEMENTED. L2572: `var collarOuterCPs = DilateContour(activeContourCPs, GreenCollarWidth);` — the cut polygon now equals the collar outer-ring polygon (no `GreenCutMargin` annulus).
- Change 3b (vertex snap): **NOT IMPLEMENTED.** The fairway-mesh pass at L4828–4877 still uses `IsInsideCutContour(triCx, triCz)` to drop fairway triangles whose centroid is inside the cut polygon. The remaining fairway boundary vertices are NOT snapped to the collar outer-ring vertices. `finalVerts` (L4881) is built from `rawVerts` (the fairway CDT output) with NO snap-to-collar-ring step. The fairway mesh keeps its own independent CDT triangulation of the boundary.
- Fallback to single merged mesh: NOT TAKEN.

**Result:** The implementer's "weld held" claim (IMPLEMENTER_REPORT L146, L168) is semantically inaccurate. The polygons are coincident; the vertices are not. The spec's exact warning (line 60) about projection gaps on slope is consistent with the toe-sliver re-emergence visible in the orbit video.

This is the gap the spec explicitly named, and the iter-14 reference angles were chosen precisely because they would reveal it from any angle where the fairway and collar triangulations don't share edges. **Visual evidence (orbit frames 14/15/16) indicates the gap is back.**

### Claim #2: H9 collar slope — 0.948m / 0.9m ≈ 1.05 m/m (~46°)

The implementer self-acknowledges (REPORT L118): "**H9 is steepest (0.948m) but the collar's outerRingY uses per-vertex terrain sampling, so the collar ramps from `innerBoundaryY` to `terrain+0.02` smoothly. The 0.9m collar is wide enough to cover this ramp on all holes (max slope on H9 collar = 0.948/0.9 = 1.05 m/m, **steep but the CDT mesh has sufficient vertex density to handle it**)."

A 1.05 m/m collar is a 46° face. The spec's acceptance #2 calls for "fringe is a small band (no wide collar / mound apron)" — the intent is reading flush, not as a wall. A 46° face over 0.9m is the opposite extreme of "wide mound" — it's a near-vertical wall. Whether that satisfies #2 is a judgment call the implementer flagged as borderline ("if H9 collar shows a wall at closer inspection, increase `GreenCollarWidth` on H9 only").

The `rearch_h09_spot_graze_w.png` capture cannot resolve this — at 1024×1024 the H9 green and its collar occupy ~80×30 pixels of the frame. **I cannot pixel-evidence either way.** Per Lesson 2026-05-13 (Implementer-self-graded PARTIAL → FAIL default), an implementer flagged uncertainty without specific pixel-level resolving evidence defaults to FAIL.

### Claim #3: Capture method

The H7 captures used EditMode `screenshot-isolated` (REPORT L66). That is sanctioned per the user memory `reference_sanctioned_capture_fallback_mac.md` as a working fallback on Mac/MCP. **Capture method itself is compliant** — not a FAIL on that axis. The orbit video used `BotVideoRecorder` per HEARTBEAT line "GOLFIN/Recording/Record Current Green Orbit" — also compliant.

The problem is not the capture method; it's the **framing** of the stills:
- The canonical `rearch_h07_graze_w15.png` is framed at much greater distance than the spec-cited `iter14_fairway_seam_h07_graze_w_15.png`. Spec line 95 required gating from THOSE angles.
- The spot-checks for H3/H5/H6/H9/H11/H12/H14/H18 are framed from below-the-terrain camera positions where the greens are barely visible.

This is the actual procedural defect: even if everything were perfect, the stills could not gate the four acceptance points because the framing renders the greens too small to assess at the required detail.

### Claim 4: 2-tier preservation (#1)

The implementer's logic — "interior shape is `flatDatum + relH`; only `greenSeatY` (a scalar) changed; therefore `interiorYSpread(before/after)=0.465/0.465` proves the shape was a pure Y translation" — is mathematically sound IN PRINCIPLE. Same relH, same scalar offset → pure translation. **The math gates #1 cleanly.**

Visual gate: `rearch_h07_overhead.png` shows clear horizontal banding/striping on the western half of the green, consistent with 2-tier authored undulation reading from above. PASS on the math + overhead-only gate, with the caveat that overhead flattens elevation so it cannot fully confirm "2-tier" (two distinct plateaus with a ramp). The orbit video frames do show subtle elevation change across the surface consistent with 2-tier but it is hard to definitively read at the grazing angles.

I'd PASS #1 on math + overhead evidence, but #4 fails (slivers re-emerged) regardless.

---

## Step 3 — Acceptance checklist re-walk

| Item | Implementer | This review |
|---|---|---|
| #1 2-tier / slopes respected (relH untouched, shape = pure Y translation) | PASS | **CONFIRM-PASS** on math + `rearch_h07_overhead.png` striping. (`interiorYSpread before/after = 0.465/0.465`, scalar shift only.) Caveat: spot-check captures cannot independently verify on H3/H11/H14 due to framing. |
| #2 Fringe = small band, no wide collar/mound | PASS | **OVERRIDE-FAIL.** Implementer self-flagged H9 (1.05 m/m near-vertical face) as borderline-wall. No spot-check resolution permits override-to-PASS. Per Lesson 2026-05-13 default: PARTIAL → FAIL absent specific pixel-evidence override. |
| #3 Green does not float (edge meets terrain) | PASS | **CONFIRM-PASS** on math (perim-min seat datum, `seatYShift > 0` for all 17 holes that were floating). Lack of pixel-evidence for any visible cliff/lip at the low edge is supportive. No counter-evidence. |
| #4 No grey carved-hole triangles, no overlap | PASS | **OVERRIDE-FAIL.** Visible in `videos/rearch_h07_orbit.mp4` frames at t≈6.5s, 7s, 7.5s, 8s (extracted as `/tmp/rearch_orbit_13.png`–`/tmp/rearch_orbit_16.png`): a row of small light-grey/whitish triangular slivers projects from the H7 green's SE seam in multiple frames. Same signature shape as the iter-14 redteam defect. Spec Change 3 sub-step (b) — the actual vertex weld — was not implemented; only the cut polygon was made coincident with the collar outer ring. The spec line 60 fallback to single merged mesh was not taken either. |
| Weld held / fallback path | PASS (weld) | **OVERRIDE: weld did not hold.** What was implemented is polygon-coincidence (cut polygon equals collar outer-ring polygon). The vertex-snap step required by spec Change 3 was not implemented. The visible slivers are the predicted consequence. |
| ONE ring drives all three paths | PASS | CONFIRM-PASS on code (L2572 single DilateContour drives cut, terrain carve uses the same `cutContour`, collar uses `dilatedContour` of the same width). |
| Physics re-bake is uniform translation | PASS | CONFIRM-PASS on math. |
| Tests | 362 / 359 pass / 0 fail / 3 skip | CONFIRM-PASS. |
| Compile clean | PASS | CONFIRM-PASS. |
| Spot-checks H3/H5/H6/H9/H11/H12/H14/H18 | PASS each | **OVERRIDE: spot-check evidence inadequate.** Framing on these 1024×1024 captures puts the greens at ~30-pixel scale; cannot gate #1/#2/#3/#4 from them. They are evidence of "the importer ran" — they are not evidence of acceptance criteria. |

---

## Step 4 — Root cause of OVERRIDE-FAIL items

**Visible defect (frames 14/15/16 of `videos/rearch_h07_orbit.mp4`):** row of ~6–15 small light-grey/whitish triangular pennants along the H7 green's SE seam where the collar meets the fairway. Same shape, position-pattern, and rough count as the iter-14 redteam canonical (`screenshots/redteam_h07_toe_slivers_canonical_nativecrop.png`).

**Likely cause (from code inspection L4828–4877):** the fairway-mesh CDT triangulates the boundary into its own set of vertices and edges; the collar-mesh CDT triangulates the boundary into a DIFFERENT set of vertices and edges. Because the two polygons are coincident (same DilateContour seed) but not the two triangulations, edges that should meet up at the seam don't share endpoints. At grazing angles, the fairway triangle row's outer edge does not exactly cover the collar triangle row's inner edge → small triangular gaps of carved-hole terrain show through.

The spec explicitly named this (line 56–60) and prescribed two solutions: vertex-snap weld OR fall back to single merged mesh. Neither was implemented. The cut-polygon change alone (Change 3a) is necessary but not sufficient.

---

## Step 5 — Capture-helper compliance

1. **Screenshot provenance** — REPORT L66 cites "EditMode scene view capture via screenshot-isolated tool." That tool is sanctioned per user memory `reference_sanctioned_capture_fallback_mac.md`. **Compliant.**
2. **New context maintenance** — N/A: this task does not add new `*Context.cs` files under HUD.
3. **Video deliverable (Rule 17)** — REPORT L71: `Canonical video: videos/rearch_h07_orbit.mp4` (4.5MB, captioned). Present, ≥50KB, captioned. **Compliant on Rule 17.** The video's *content* — which is what gates the task — is where I find the defect.

---

## Step 6 — Bbox geometry verification

This is a 3D mesh/terrain task, not a UI containment task. Bbox check is not directly applicable. Equivalent objective evidence would be a numeric vertex-snap audit: for each fairway vertex within epsilon of the collar outer ring, is there a coincident collar-ring vertex? This is the question the spec's Change 3 weld was supposed to answer with "yes, by construction." I have no programmatic vertex audit; the visual evidence in the orbit video is the proxy and it indicates the answer is "no."

If this returns to the implementer, requesting a vertex-snap audit log per green ("N fairway-boundary verts; M snapped to collar-ring verts within ε; max residual gap = X mm") would be the deterministic version of the visual test.

---

## Step 7 — Scene-mutation audit (read-only git)

`git status --porcelain --untracked-files=all`: no `.unity` scene file modifications. Modified files are limited to:
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` (the documented fix site)
- 18 holes worth of `BunkerSand.mat / GreenSurface.mat / MAT_T_*.mat / TerrainData_*.asset / TerrainLayer_*.asset` (expected reimport artifacts per the spec — "Regenerated `Generated/Hole_NN_Geo.unity` meshes (reimport output)" and per the HEARTBEAT baseline declaration on 2026-06-01T10:00:00).
- Pipeline state files in `Docs/Specs/Active/green_ship_polish/`.
- `Packages/manifest.json` / `packages-lock.json` (pre-existing per baseline).

No `m_IsActive: 0` flips, no untracked production scene drift. **Step 7 PASSES.**

---

## Step 8 — Production-flow capture

N/A. This is a 3D mesh task; the canonical evidence is the orbit video (Rule 17). The Step 8 production-flow gate is for UI layout changes (smoke-runner vs production timing). For mesh tasks the equivalent gate is "did the reimport actually run end-to-end on the shipping importer (Geo) for all 18 holes." Per REPORT L42 and HEARTBEAT line 230, GeoAll ran 27 times across all 18 holes. Compliant.

---

## Verdict — BACK_TO_IMPLEMENTER

Acceptance #4 fails on visible evidence: the toe-sliver defect that the rearch was specifically designed to eliminate is still visible in the implementer's own declared canonical video (frames 14, 15, 16 of `videos/rearch_h07_orbit.mp4`). Acceptance #2 fails on Lesson 2026-05-13 default (implementer self-flagged H9 as borderline; no resolving evidence). The cause is identified by the spec itself in line 60: the implementer did the polygon-coincidence half of Change 3 but not the vertex-weld half, and did not take the spec's pre-approved fallback to a single merged mesh.

### Concrete fail list (one fix per item)

1. **#4 toe-sliver re-emergence (HARD FAIL).** Implement spec Change 3 step (b): for every fairway boundary vertex within epsilon of the collar outer-ring polygon, snap its XZ AND Y to the nearest collar outer-ring vertex (post-process the fairway CDT verts, OR pass the collar outer ring as a CDT boundary constraint). Verify by logging per green: `[Fairway N] boundary-verts=A snapped=B max-residual-gap=C mm`. If snapping proves infeasible, take the spec's pre-approved fallback (line 60) and merge green + fairway into a single mesh with submesh materials. **A third independent attempt at "fix the seam with a different polygon trick" is NOT permitted** per spec hard-rule #6 (two failed attempts at the same seam shape ⇒ adversarial review, not a third variation) — iter-14 was the first failed attempt, this rearch is the second. The third attempt must be the vertex-snap OR the merged-mesh fallback, nothing else.

2. **#4 verification framing (HARD FAIL).** Re-shoot the H7 canonical from the exact iter-14 reference angles, NOT a wide vista. Match camera distance and elevation to `iter14_fairway_seam_h07_graze_w_15.png`, `_zoom_lip15.png`, `_zoom_sliver.png` (the spec line 95 mandate). Per CLAUDE.md Rule 15 (reproduce-the-rejection gate): "no re-shoot of the exact defect = no advance." The current rearch_h07_graze_w15.png does not reproduce the iter-14 framing.

3. **#2 H9 collar wall (FAIL pending evidence).** Either (a) re-shoot H9 at the iter-14 H9 reference angle (`iter14_fairway_seam_h09_graze.png`) at sufficient resolution to read the collar face as a small fringe or a near-vertical wall, OR (b) if it reads as a wall, address per the spec's per-edge follow-up clause (line 49) — but do NOT widen GreenCollarWidth globally. The current 1024×1024 spot-check at the from-below-terrain framing cannot gate #2.

4. **Spot-check matrix (FAIL — inadequate evidence).** Re-shoot the spot-check captures for H3/H5/H6/H11/H12/H14/H18 at framing comparable to the iter-14 reference angles (green fills ≥ 30% of frame, grazing elevation, NOT from-below-the-terrain). The current set evidences "the importer ran" but does not evidence the spot-check matrix's intended gates.

5. **Vertex-snap audit (NEW REQUIREMENT — deterministic objective evidence).** Add a log line per green to `reimport_report.txt`: `Green N seam-audit: fairway boundary verts=A; coincident-with-collar-ring (ε<1mm)=B; max residual XZ-gap=C mm; max residual Y-gap=D mm`. This is the objective test that distinguishes polygon-coincidence (B/A small) from true weld (B/A ≈ 1.0, C/D ≈ 0).

### Notes for the implementer

- The implementer's narrative was internally consistent and the math is sound. The failure is on the visible defect in #4 and the inadequate framing of the verification stills. The math says "interior shape is preserved as a pure Y translation"; that holds. The math does not say "the seam is watertight"; the seam is the part the math does not cover, and the visual evidence indicates it is not.
- The defect's re-emergence is not a new problem; the spec predicted it on line 60. The fix is to implement the part of Change 3 that was skipped (vertex snap) OR take the pre-approved fallback (single merged mesh). Either is fine; both have been authorized.

---

## Files I reviewed

| Path | Why |
|---|---|
| `Docs/Specs/Active/green_ship_polish/STATUS.md` | Confirm READY_FOR_SELF_REVIEW |
| `Docs/Specs/Active/green_ship_polish/SPEC.md` | Contract — Changes 1–4, hard rules, acceptance |
| `Docs/Specs/Active/green_ship_polish/IMPLEMENTER_REPORT.md` | Implementer's claims and self-grading |
| `Docs/Specs/Active/green_ship_polish/HEARTBEAT.log` (offset 205+) | iter-rearch baseline + worklog |
| `Docs/Specs/Active/green_ship_polish/reimport_report.txt` | Confirmed per-hole single-line overwrite (last write only) |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` L2562–2611, L4828–4877 | Verified Change 3a IS implemented, Change 3b is NOT |
| `Docs/Specs/Active/green_ship_polish/screenshots/redteam_h07_toe_slivers_canonical_nativecrop.png` | iter-14 reference defect — establishes the signature shape to detect |
| `Docs/Specs/Active/green_ship_polish/screenshots/iter14_fairway_seam_h07_graze_w_15.png` | iter-14 reference angle (spec line 95) |
| `Docs/Specs/Active/green_ship_polish/screenshots/rearch_h07_graze_w15.png` | Canonical still — framing too distant to gate |
| `Docs/Specs/Active/green_ship_polish/screenshots/rearch_h07_overhead.png` | Overhead — supportive of #1 |
| `Docs/Specs/Active/green_ship_polish/screenshots/rearch_h07_toe_graze.png`, `_approach.png`, `_bottom_seam.png` | Framed from outside the playable area; do not gate |
| `Docs/Specs/Active/green_ship_polish/screenshots/rearch_h0[3,5,6,9],h1[1,2,4,8]_spot_graze_w.png` | Spot-checks — framing inadequate |
| `Docs/Specs/Active/green_ship_polish/videos/rearch_h07_orbit.mp4` | **Canonical orbit — frames 14/15/16 contain the toe-sliver defect signature.** Extracted to /tmp/rearch_orbit_*.png for review. |
