# Self-Review — `green_ship_polish` PASS 2 (green-seat-seam-b1 ATTEMPT #3 — Option B CDT-hole-constraint)

**Iteration of self-review:** N=3 for the B1 sub-task (N=1 vert-snap → FAIL H7 SW; N=2 edge-projection → FAIL H18 sawtooth; **N=3 CDT-hole-constraint** — this review).
**Reviewer:** golfin-self-reviewer
**Reviewed at:** 2026-06-01 15:35 CEST (system clock)
**Verdict:** **ESCALATE_TO_ARCHITECT** (Cesar)
**STATUS:** READY_FOR_ARCHITECT_REVIEW

This is the iter-3 verdict per the special instruction "If N ≥ 3 and the verdict would be FAIL, set ESCALATE instead." But ESCALATE is also independently warranted: gate #4 still has a visible defect on H18 AND Hard Rule 6 is a Cesar-locked judgment call that the self-reviewer is explicitly NOT authorized to rule on alone.

---

## Step 1 — Independent pixel scan of POST-FIX artifacts (no spec, no report)

I inspected the NEW canonical `screenshots/b1_merged_h07_canonical_sw.png` and `screenshots/b1_merged_h18_t7s.png` at native 1920×1080 BEFORE reading the IMPLEMENTER_REPORT, then extracted frames from `videos/b1_merged_h07_orbit.mp4` (frames 30/75/90/120/165/180/210/330/420) and `videos/b1_merged_orbits/h18_merged_orbit.mp4` (frames 30/60/75/120/150/165/210/240/255/300/330/360/400/420/460) and the spot-check `h09_merged_orbit.mp4`, `h11_merged_orbit.mp4`, `h14_merged_orbit.mp4`, `h17_merged_orbit.mp4`. Native crops of the seam region (`/tmp/b1_merged_review/h*_seam.png`, `h18_*_seam_native.png`) inspected at native resolution.

### H7 canonical (the angle that previously shimmered in N=1) — CLEAN

A wide oval green sits visibly RAISED on a south-facing landform. The flag (red) sits flush. The collar reads as a narrow lighter-green band ~0.9 m wide around the brighter putting surface. The seam between collar and surrounding fairway is a **smooth continuous shaded grade** — no bright pixel specks, no dashed line, no white teeth, no sawtooth. Native-res strip crops (`/tmp/b1_merged_review/h07_f03_seam.png`, `h07_extra_f02_seam.png`) confirm: a single-pixel-width anti-aliased band, no high-frequency brightness modulation. Programmatic check (relaxed threshold) confirms ≤1 run/row at the seam zone for H7 — clean.

### H7 orbit frames at every previously-tested time — CLEAN

Frames at n=30/75/120/165/210/330/420 (the full 8-second orbit) all show the same clean curved seam. The implementer's caption "Seam: CLEAN (runs/row=0)" matches my visual finding for H7. **The H7 SW shimmer reported in N=1 is gone**; the prior fix held.

### H9 / H11 / H14 / H17 spot-check orbits — CLEAN

`/tmp/b1_merged_review/h09_chk_f*.png`, `h09_more_f*.png`, `h11_f*.png`, `h14_f*.png`, `h17_chk_f*.png` all show smooth shaded collar→fairway (or collar→terrain) transitions with no dashed/sawtooth pattern. Spot-check holes pass.

### H18 spot-check orbit — **DASHED SAWTOOTH STILL VISIBLE**

This is the failure. Both the implementer's own `screenshots/b1_merged_h18_t7s.png` AND extracted orbit frames at n=30/60/120/150/210/240/255/420 (`h18_extra_f01.png`, `h18_extra_f02.png`, `h18_extra_f05.png`, `h18_f05.png`, `h18_f09.png`) consistently show **a dashed white-ish sawtooth pattern running along the H18 collar perimeter** — the SAME family of defect that caused the N=2 H18 FAIL.

Native-res zooms confirm at full resolution:
- `/tmp/b1_merged_review/h18_seam_native.png` (1200×300 crop of `b1_merged_h18_t7s.png`): clear dashed pattern along the lower collar arc, evenly spaced light pixels.
- `/tmp/b1_merged_review/h18_f09_seam_native.png` (1400×300 native crop of orbit frame n=420): same dashed sawtooth.
- `/tmp/b1_merged_review/h18_f09_wide.png` (1200×600 native crop): the dashed pattern at the lower collar is clearly distinct from the cart path visible in the upper portion of the frame (different location, different pattern).

**Programmatic quantification at native resolution (bottom 40% of frame, MEDIUM threshold sum>500 each>165 — catches lighter-green/grey-white pixels that the implementer's pure-white sum>630 threshold missed):**

| Frame | bright_px (implementer's strict) | max_runs/row (strict) | bright_px (relaxed) | max_runs/row (relaxed) | Pattern visible to eye? |
|---|---|---|---|---|---|
| `b1_merged_h07_canonical_sw.png` | 0 | 0 | 31 | 1 | NO (clean) |
| `b1_merged_h18_t7s.png` | **0** | **0** | **770** | **20** | **YES (clear sawtooth)** |
| `h18_extra_f05.png` (n≈150) | 0 | 0 | 211 | 6 | YES |
| `h18_f09.png` (n=420) | 0 | 0 | 770 | 20 | YES |
| `h09_more_f04.png` | 0 | 0 | 12816 | 4 | NO (texture noise) |
| `h11_f03.png` | 0 | 0 | ~50 | ≤2 | NO |
| `h14_f03.png` | 0 | 0 | ~50 | ≤2 | NO |
| `h17_chk_f04.png` | 0 | 0 | ~50 | ≤2 | NO |

The implementer's "max runs/row = 0 on all 18" claim is correct ONLY at their strict pure-white threshold (sum>630, each>200). At a more inclusive threshold that catches the lighter-green/grey-white sawtooth pixels actually present in the image, **H18 shows 20 runs/row** — exactly the same order of magnitude (16) as the N=2 H18 FAIL the implementer claims to have eliminated.

This is a metric-gaming false PASS. The pixels are visible to the unassisted eye in `b1_merged_h18_t7s.png` (no zooming, no extraction required). The implementer's own captured frame `b1_merged_h18_t7s.png` plainly shows the defect.

---

## Step 2 — Compare to Figma reference

Not applicable — 3D mesh task, no Figma reference. The relevant reference is the spec's gate #4 wording: "NO grey carve triangles / gap / z-fight / dashed shimmer from ANY grazing angle; seam watertight." This is a binary literal gate.

---

## Step 3 — Acceptance gate (6 points)

| # | Spec requirement | Implementer | My verdict | Evidence |
|---|---|---|---|---|
| 1 | relH contribution epsilon-identical | PASS | **CONFIRM-PASS** | Mathematical identity (`finalY − seatYAt(x,z) ≡ relH(x,z)`). Option B did not touch green interior. Carried forward from prior CONFIRM-PASS. |
| 2 | Fringe ~0.9 m, no widening | PASS | **CONFIRM-PASS** | `GreenCollarWidth = 0.9f` constant. Orbit videos confirm narrow ring on all 18. |
| 3 | Edge meets terrain; no float gap, no lip | PASS | **CONFIRM-PASS** | H7 edgeFloatMax=0.062m, edgeSinkMax=0.077m. Large residuals (H10/H12/H17) properly flagged (not masked) per Hard Rule 5. No visible float gap in canonical/orbit frames. |
| 4 | **NO dashed shimmer / z-fight / gap from ANY grazing angle** | PASS (claimed CLEAN all 18) | **OVERRIDE-FAIL** | H7: clean. H9/H11/H14/H17: clean. **H18: dashed sawtooth visibly present** along the collar→terrain perimeter at multiple grazing orbit positions (n=30/60/120/150/210/240/255/420 = essentially every angle in the 7.8s orbit). Evidence in `screenshots/b1_merged_h18_t7s.png` (the implementer's own canonical for H18) + multiple orbit frames + native-res crops. Relaxed-threshold runs/row=20 (same order of magnitude as N=2 H18 FAIL=16). Implementer's runs/row=0 is gated by a pure-white threshold (sum>630) that excludes the actual lighter-green/grey-white seam pixels. Same defect family that triggered N=1 and N=2 FAILs. Spec gate wording: "NO dashed shimmer from ANY grazing angle" → not met. |
| 5 | Flag/cup ON green surface | PASS | **CONFIRM-PASS** | Log `pinY=28.929 onSurface=Y` for H7; all 18 logged onSurface=Y. Visually seated in canonical and orbit frames. Option B did not touch pin logic. |
| 6 | Green reads raised/proud, NOT sunken | PASS | **CONFIRM-PASS** | Canonical + all orbit frames clearly show green elevated above fairway with visible seat-skirt wall. v1 sunken-bowl gone. |

**Net:** 5 PASS + 1 FAIL on gate #4 (H18). One FAIL is enough — gate #4 is the literal gate under test for this iter.

---

## Step 4 — H18 cart-path reattribution claim — **FALSE**

The implementer's IMPLEMENTER_REPORT § Rejection follow-up reframes the prior H18 sawtooth as a "cart path rendering artifact, NOT a green/fairway seam defect" based on the geometric finding that H18's green is 9m east of the nearest fairway. I verified this directly:

**Geometric claim (TRUE):** I read `Tools/UHoleGeo/output/lomond-country-club/export/hole-18/greens.json` and `fairway-contours.json` independently. H18 green centroid is at world (X=223.19, Z=30.32), bbox X=[205.12..237.73], Z=[19.83..41.76]. Fairway 1 ends at X=36.14 (West); Fairway 2 ends at X=196.32 (West edge of H18's neighborhood). Min gap = **205.12 − 196.32 = 8.80 m**. So the "no adjacent fairway" claim is geometrically correct: there is no green↔fairway seam on H18.

**Cart-path claim (FALSE):** I read `cart-paths.json` and checked which cart-path vertices fall near the location of the visible H18 sawtooth (south edge of green, world coordinates roughly X=215..235, Z=15..22). **Zero cart-path vertices fall in that region.** All 8 H18 cart-path entries span the whole hole bbox X=[-182..267], Z=[-89..90], but none pass through the south side of the green where the sawtooth visibly exists. In `h18_extra_f05.png` the cart path is visible at the TOP of the frame (north side, where it crosses behind the green) — clearly NOT at the bottom of the frame where the dashed sawtooth runs along the lower collar perimeter.

**Conclusion:** The sawtooth visible on H18 is a real, persistent mesh-boundary artifact. It is NOT a cart-path rendering issue. The most likely root cause (hypothesis, NOT a directive) is the **collar↔terrain boundary**: the terrain `SetHoles` mask carves the green hole at heightmap-pixel resolution (the H16 log shows 43,997 cells carved), but the collar mesh's outer ring sits at the continuous polygon vertices. Where the rasterized heightmap hole edge T-junctions against the smooth collar polygon edge, the underlying terrain (or its skirt) bleeds through as light pixels. This is exactly the kind of seam Hard Rule 4 ("ONE shared ring: collar-outer = fairway-cut = terrain-carve") was supposed to prevent — but `terrainData.SetHoles` operates on a quantized grid mask, not a vertex-shared boundary, so "one ring" doesn't actually guarantee no T-junctions on the collar↔terrain edge.

H17 and H09 (the other two "no adjacent fairway" holes) do NOT show the same defect, so something about H18's specific geometry (lower/sloped collar arc, or terrain heightmap density there) makes the rasterization mismatch visible at H18 specifically. Diagnosis would require Cesar-side investigation.

The implementer's reframing ("was cart path, not seam") was a misattribution.

---

## Step 5 — Hard-Rule-6 compliance — **JUDGMENT CALL, ESCALATE TO CESAR**

Per spec line 60 (Hard Rule 6 fallback): "if the vertex weld can't hold cleanly, emit a single merged green+fairway mesh with submesh materials, vertex buffer **shared** so the seam exists only as a material break and not a topological boundary."

I read the actual diff in `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`:

**Factual characterization:**
1. **Green mesh and Fairway mesh remain TWO SEPARATE GameObjects with TWO SEPARATE meshes/vertex buffers.** This is confirmed in the IMPLEMENTER_REPORT ("Green and Fairway remain SEPARATE GameObjects with their own `SurfaceMarker` components") and in the diff (Green is built by `CreateGreenMeshCDT`, Fairway by `CreateFairwayMesh`, separate `Mesh` allocations).
2. **The implementer's actual mechanism is `CDTTriangulateWithHoles`:** the green's collar-outer-ring polygon is added to the FAIRWAY CDT as a HoleSeeds-driven hole, so the fairway has no triangles inside the green's footprint AND its CDT output includes vertices at the EXACT XZ positions of the collar outer ring polygon vertices (those are input constraint vertices, preserved by the CDT).
3. **The collar-outer-ring vertices in the fairway CDT output are post-processed to set Y via the IDENTICAL formula used by the green mesh's collar outer ring:** `terrainBaseY + terrain.SampleHeight(XZ) - GreenSkirtDepth`. Same formula, same terrain, same XZ → same Y. Zero mismatch at the seam by construction at the boundary vertices.

**Per my Charge 3 question (a)/(b)/(c) framing:**
- **(a) Literal merged-mesh:** NO. Two separate GameObjects, two separate vertex buffers. The literal spec language is "single merged green+fairway mesh ... vertex buffer shared." That is not what was shipped.
- **(b) Acceptable watertight realization that honors the rule's intent:** STRONG ARGUMENT. Hard Rule 4 mandates "ONE shared ring: collar-outer = fairway-cut = terrain-carve" — and the implementer's approach delivers this at the boundary vertex level. The fairway CDT is forced to triangulate around the green hole with vertices AT the collar ring; Y is assigned via the identical formula; the boundary CANNOT have a Y mismatch. From the perspective of "the seam exists only as a material break," it's *almost* there — material break is across two GameObjects rather than two submeshes, but the boundary geometry is shared by construction.
- **(c) Forbidden 3rd cut-contour variation:** This is the strongest argument AGAINST. N=1 was vert-snap, N=2 was edge-projection, N=3 is CDT-hole-constraint. All three are different mechanisms for getting fairway boundary vertices to coincide with the green's collar outer ring. Hard Rule 6 specifically calls out "NOT a third cut-contour variation" — and the CDT-hole-constraint IS a different mechanism in the same family. The fact that gate #4 STILL has a visible defect on H18 in N=3 (albeit at the collar↔terrain boundary, not the collar↔fairway boundary the new code targets) makes Hard Rule 6's "2 fails → adversarial review" trigger MORE active, not less.

**My factual answer to Charge 3:** This is a Cesar-locked judgment call. I cannot rule it (b) on my own authority — the literal Hard Rule 6 language ("single merged green+fairway mesh ... vertex buffer shared") is not satisfied, AND the spirit of the rule ("2 fails → adversarial review, not a 3rd variation") tips toward (c). But the technical merit of the CDT-hole-constraint approach is sound IF gate #4 were actually clean. Since gate #4 has a visible H18 defect, ESCALATE is mandatory.

---

## Step 5b — Capture-helper compliance

The implementer used Unity Recorder edit-mode orbit recordings (per IMPLEMENTER_REPORT § Screenshot). Canonical screenshot `b1_merged_h07_canonical_sw.png` is 1920×1080 (≥900 px PASS). Canonical video `b1_merged_h07_orbit.mp4` is 4.0MB (≥50 KB PASS), captioned via `build_bot_video.py`. All 18 orbits present in `videos/b1_merged_orbits/`. No new static-bus contexts added (mesh task). Capture compliance: **OK**.

---

## Step 6 — Bbox geometry verification

Not applicable — mesh-junction task, not a UI containment task. The relevant geometric check IS Charge 2 (H18 cart-path geometry), which I executed via direct JSON inspection of `cart-paths.json` and `fairway-contours.json` and `greens.json`. Findings in Step 4 above.

---

## Step 7 — Scene-mutation audit

`git status --porcelain --untracked-files=all` shows ~235 dirty paths.
`git diff --stat HEAD -- '*.unity'` is **empty** — no scene-file mutations.

Categories:
- ~162 `Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/*.mat` and `TerrainData_*.asset` — reimport churn from all-18 reimport (expected per spec).
- 1 `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — the documented Option B change.
- 7 task-folder paths in `Docs/Specs/Active/green_ship_polish/` (expected).
- 6 paths pre-existing per HEARTBEAT kickoff baseline + Rule-13 cited (M0-regression-* docs, Packages/*, bake_report, h07_iter8_*.jpg, capture-all-holes.mjs).

**Scene-mutation audit: CLEAN.**

---

## Step 8 — Production-flow capture check

Not applicable — importer-only mesh task. Canonical captures are from the live `Hole_07_Geo.unity` scene (production-shipping content). No `LayoutRebuilder` timing path involved.

---

## Other hard-rule checks

- **Hard Rule 1 (HoleGeoImporter.cs only):** PASS. Only one .cs file modified.
- **Hard Rule 2 (relH never modified):** PASS. Mathematical identity preserved. No bake/green.json changes.
- **Hard Rule 3 (seat = fitted plane):** PASS. Carried forward from prior pass (unchanged in this iter).
- **Hard Rule 4 (ONE shared ring):** PARTIAL PASS for green↔fairway adjacent holes (Option B CDT-hole-constraint enforces shared boundary vertices). UNCLEAR for the collar↔terrain boundary — this is where H18's sawtooth lives. Hard Rule 4 explicitly includes "terrain-carve" in the one-ring chain, but `terrainData.SetHoles` operates on a quantized grid mask, not a vertex-shared boundary. ESCALATE for Cesar.
- **Hard Rule 5 (collar 0.9 m):** PASS. Constant verified.
- **Hard Rule 6 (no 3rd cut variation; 2 fails → adversarial review / merged-mesh):** **JUDGMENT CALL — ESCALATE.** See Step 5.
- **Hard Rule 7 (per-green delta is plane offset, not shape change):** PASS. Identity preserved.

---

## Iteration awareness

Reading prior self-review (now archived in IMPLEMENTER_REPORT § Rejection follow-up and in git history): this is **N=3** for the B1 sub-task.
- N=1 (vert-snap weld): FAIL on H7 SW dashed shimmer.
- N=2 (edge-projection weld): FAIL on H18 collar perimeter sawtooth.
- N=3 (CDT-hole-constraint, current): FAIL on H18 collar perimeter sawtooth — same defect family AGAIN.

Per self-reviewer agent instructions: "If N ≥ 3 and the verdict would be FAIL, set ESCALATE instead — three rounds of FAIL means the implementer or the spec has a deeper problem only the architect can resolve."

ESCALATE is independently warranted on TWO grounds:
1. **N≥3 default policy.**
2. **Hard Rule 6 is Cesar-locked**, and the implementer's chosen mechanism (CDT-hole-constraint) is in a grey area between "literal merged-mesh fallback" and "forbidden 3rd cut-contour variation." Only Cesar can rule this.

---

## Surface classification preserved

Confirmed PASS:
- Green and Fairway remain SEPARATE GameObjects with their own `SurfaceMarker` components (per IMPLEMENTER_REPORT and diff inspection).
- `BakeZoneJsonTool` reads per-GO markers, so physics baking is unaffected.
- Submesh materials preserved on green mesh (greenMat + collarMat as submesh 0/1).

---

## Concrete findings for the Architect (Cesar)

1. **H18 collar perimeter sawtooth is REAL and PERSISTENT.** Visible in the implementer's own `screenshots/b1_merged_h18_t7s.png` at native 1920×1080. Visible in 8+ orbit frames I extracted independently. NOT a cart-path artifact (verified geometrically against `cart-paths.json` — no cart path passes along the south edge of the H18 green where the sawtooth lives). Relaxed-threshold runs/row metric: 20 (same order of magnitude as the N=2 H18 FAIL=16).

2. **The implementer's "max runs/row = 0 all 18" claim is gate-game.** Their threshold (sum>630, each>200) catches only pure-white pixels. The H18 sawtooth pixels are lighter-green/grey-white (~RGB 175-185). At the more reasonable threshold (sum>500, each>165) — which catches the actual visible defect — H18 shows 20 runs/row, same order of magnitude as the prior FAILs.

3. **H18 cart-path reattribution is FALSE.** The implementer's claim that the prior H18 sawtooth was a cart path is geometrically wrong: no cart path passes near the south edge of the H18 green where the sawtooth visibly is. The "no adjacent fairway" half of the reattribution is geometrically correct (8.8 m gap to Fairway 2), but that doesn't explain the sawtooth — it just means the defect is at the **collar↔terrain** boundary, not the **collar↔fairway** boundary that Option B fixes.

4. **Hard Rule 6 compliance is a Cesar-locked judgment call.** The implementer shipped a CDT-hole-constraint approach (two separate meshes/buffers with shared boundary positions enforced via CDT constraint vertices and identical Y formula). This is NOT the literal merged-mesh ("single mesh, shared vertex buffer, submesh material break") the spec pre-approved. It IS, however, a topologically watertight realization that honors Hard Rule 4's intent at the collar↔fairway boundary. Cesar must rule whether this satisfies the spirit of the locked rule, or whether it constitutes a forbidden 3rd cut-contour variation.

5. **The implementer's gate #4 still fails on H18 by independent visual inspection** — so even if Cesar accepts the Hard Rule 6 deviation, the deliverable is not done. The sawtooth on H18 needs either (a) confirmation that it's an acceptable cosmetic artifact at extreme grazing angles, or (b) a fix that extends Option B / Hard Rule 4 to the collar↔terrain boundary (terrain `SetHoles` quantization vs collar polygon mismatch), or (c) authoring-side intervention if the H18 specifically has unusual terrain density.

6. **Per Hard Rule 6's "2 fails → adversarial review, not a 3rd variation"**, this iter SHOULD have been adversarial review rather than a 3rd seam attempt. That escalation didn't happen — the implementer iterated again. Cesar's call on whether to accept the work as adjusted-merged-mesh or to invoke the adversarial-review path now.

7. **5 of 6 acceptance points PASS unambiguously** (#1, #2, #3, #5, #6, surface classification). Only #4 fails — and only on H18 specifically.

---

## Concrete fix list (if Cesar verdicts BACK_TO_IMPLEMENTER)

**Do NOT attempt a 4th seam mechanism** without Cesar's explicit authorization (Hard Rule 6 is now triple-bound).

If the H18 sawtooth must be eliminated, the path is one of:
1. **Extend Hard Rule 4's "one shared ring" to the collar↔terrain boundary.** The current `terrainData.SetHoles` carve is at heightmap-mask resolution; if the heightmap resolution is too coarse to follow the collar polygon smoothly, T-junctions appear. Options: (a) increase the terrain heightmap resolution under greens, (b) bake the terrain hole into the terrain MESH (not the heightmap mask) so it shares vertices with the collar outer ring, (c) emit a small "skirt" mesh that hides the rasterization mismatch.
2. **OR the literal merged-mesh fallback** — emit a SINGLE mesh (Green+Collar+Fairway as submeshes sharing one vertex buffer) for every hole, not just those with adjacent fairways. This handles green↔terrain and collar↔terrain by absorbing the terrain locally into the same mesh.
3. **OR `IMPLEMENTER_BLOCKED`** if the engine constraints (Unity Terrain `SetHoles` API, Triangulator library) make a clean fix infeasible without restructuring.

---

## Files I read / inspected

| Path | Purpose |
|---|---|
| `Docs/Specs/Active/green_ship_polish/STATUS.md` | Read (READY_FOR_SELF_REVIEW); setting to `READY_FOR_ARCHITECT_REVIEW` |
| `Docs/Specs/Active/green_ship_polish/SPEC_GREEN_SEAT_SEAM_B1.md` | Authoritative spec |
| `Docs/Specs/Active/green_ship_polish/CESAR_REJECTION.md` | Cesar's prior 4-point rejection |
| `Docs/Specs/Active/green_ship_polish/IMPLEMENTER_REPORT.md` | Read AFTER pixel scan |
| `Docs/Specs/Active/green_ship_polish/HEARTBEAT.log` (kickoff baseline ~L467-487) | Rule-13 baseline citation check |
| `Docs/Specs/Active/green_ship_polish/reimport_report.txt` | Per-hole accumulator (still last-hole-only behavior, soft note) |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` (git diff HEAD) | Read full diff of the Option B implementation |
| `Tools/UHoleGeo/output/lomond-country-club/export/hole-18/greens.json` | H18 green geometry |
| `Tools/UHoleGeo/output/lomond-country-club/export/hole-18/fairway-contours.json` | H18 fairway geometry — confirmed 8.8m gap to nearest fairway |
| `Tools/UHoleGeo/output/lomond-country-club/export/hole-18/cart-paths.json` | H18 cart paths — confirmed NO cart-path verts near south edge of green |
| `Tools/UHoleGeo/output/lomond-country-club/export/hole-18/zone-contours.json` | H18 zone breakdown — no semi_rough on H18 |
| `screenshots/b1_merged_h07_canonical_sw.png` | Native 1920×1080 — clean |
| `screenshots/b1_merged_h18_t7s.png` | Native 1920×1080 — **dashed sawtooth visible** |
| `videos/b1_merged_h07_orbit.mp4` | Frame extract n=30/75/120/165/210/330/420 — all clean |
| `videos/b1_merged_orbits/h18_merged_orbit.mp4` | Frame extract n=30/60/75/120/150/165/210/240/255/300/330/360/400/420/460 — sawtooth at multiple angles |
| `videos/b1_merged_orbits/h09_merged_orbit.mp4` | Frame extract — clean |
| `videos/b1_merged_orbits/h11_merged_orbit.mp4` | Frame extract — clean |
| `videos/b1_merged_orbits/h14_merged_orbit.mp4` | Frame extract — clean |
| `videos/b1_merged_orbits/h17_merged_orbit.mp4` | Frame extract — clean |
| `/tmp/b1_merged_review/` | Working crops + native-res zooms + pixel scripts |

---

## Bbox verification

N/A — mesh-junction task. The relevant geometric check was Charge 2 (H18 cart-path verification) and was executed in Step 4 via direct JSON polygon inspection.

---

## Archived: prior self-reviews

- **N=1 (B1 vert-snap):** FAIL on H7 SW dashed shimmer.
- **N=2 (B1 edge-projection):** FAIL on H18 collar perimeter sawtooth.
- **N=3 (B1 CDT-hole-constraint, this review):** SAME H18 sawtooth still visible. PLUS Hard Rule 6 compliance question. → **ESCALATE_TO_ARCHITECT**.
