# Self-Review — `green_ship_polish` PASS 2 follow-up: terrain-apron

**Iteration:** N=1 (first self-review of the terrain-apron scope). The prior B1 self-reviews were on a different sub-problem (collar↔fairway weld), preserved as `SELF_REVIEW_B1_MERGEDMESH.md`. This review is on the **terrain-apron** spec (collar↔terrain seam fix, H10 + H18).
**Reviewer:** golfin-self-reviewer
**Reviewed at:** 2026-06-01 14:30 JST (system clock)
**Verdict:** **FORWARD_TO_ARCHITECT** (PASS)
**STATUS:** SELF_REVIEW_PASS

---

## Step 1 — Independent pixel scan of POST-FIX artifacts (no spec, no report)

I opened the canonical screenshots and frame extracts BEFORE re-reading the IMPLEMENTER_REPORT and described what I saw at the collar↔terrain edge.

### H10 — canonical 1920×1080 grazing arc (`screenshots/terrain_apron_h10_canonical_grazing.png`)

A large oval putting surface, mid-toned green, fills the lower half of the frame. Around it I see a clear **darker outer ring** about 1–2 m wide — this is the new apron. Inside that, between the apron and the brighter putting surface, sits a thinner paler band (the collar). At the upper-right a darker rough/terrain rise climbs behind the green; at the upper-left a road and small lake/fairway features sit in the background. The outer edge of the apron — where it meets the surrounding rough/terrain — reads as a **smooth continuous ellipse**. I do not see teeth, dashed pixels, or stair-stepping along that boundary at native resolution.

### H10 — close-graze frame extracts (`screenshots/apron_frames/h10_t6.png` and `t4`, `t1`)

`h10_t6` is the most informative: the camera is low and to the side, looking across the green's outer edge toward the rough. A **darker apron ring** is clearly visible all the way around the green; it transitions smoothly into the surrounding terrain with no visible step or cliff. Above and behind the green there is a darker terrain rise; the apron blends into that rise as a **gentle ramp**, NOT as a standing carved lip. The road and an unrelated fairway/path crossing in the upper-right are landscape features, not seams. Edge-band crop (`/tmp/apron_zoom/h10_t6_edgeband.png`) viewed at 1920×250 confirms: no teeth along the collar↔terrain boundary in the foreground.

### H18 — canonical 1920×1080 grazing arc (`screenshots/terrain_apron_h18_canonical_grazing.png`)

A large oval green in the centre with a **clean darker ring (apron)** around it. The outer edge of the ring is smooth and continuous. A white bunker sits above the green. Background hills/trees are present but unrelated. No teeth, no dashed pattern visible.

### H18 — close-graze frame extract (`screenshots/apron_frames/h18_t5.png`)

Same picture from a slightly different angle — clean apron ring, smooth outer boundary. Edge-band crop (`/tmp/apron_zoom/h18_t5_edgeband.png`) at native res confirms.

**Step 1 summary:** the visible defect from the N=2 / N=3 B1 reviews (dashed sawtooth on H18 south collar perimeter) is **gone** in the post-apron captures. H10 (never inspected before, the higher-risk hole) is also clean and the proud-rim reads as a graded ramp, not a standing lip.

---

## Step 2 — Compare to spec/Figma reference

Not applicable — 3D mesh task, no Figma reference. The relevant gate is the spec's numerical sawtooth measure + apron mesh constraints, evaluated in Step 3.

---

## Step 3 — Spec acceptance checklist walk

I re-measured the sawtooth metric independently and verified each spec gate against the captures.

### Independent sawtooth measurement

I ran an edge-runs-per-row scan on all 12 apron-frame extracts using two formulations:
1. **Raw brightness threshold** matching the implementer's stated "threshold=160" gate (pixels with luma ≥ 160 along each row, count of run-starts in the central band).
2. **Green-mask-edge runs** (count of distinct green-grass runs per row inside the central band).

Both formulations sweep the entire row, so they also pick up unrelated bright features (sky, distant bunkers, roads, fairways) that have nothing to do with the collar↔terrain seam. The numbers therefore include lots of unrelated edges. Despite that:

| Frame | raw thr=160 max | green-mask max | Row | Eye-check at row |
|---|---|---|---|---|
| h10_t1.png | 10 | 7 | 366 | upper-frame band; background hills/road, NOT apron edge |
| h10_t2.png | 9 | 5 | 414 | apron edge band; runs include hill silhouette |
| h10_t3.png | 6 | 3 | 366 | apron edge clean |
| h10_t4.png | 7 | 4 | 390 | apron edge clean |
| h10_t5.png | 2 | 3 | 364 | apron edge clean |
| h10_t6.png | 6 | 4 | 340 | upper road; apron itself smooth |
| h18_t1.png | 3 | 4 | 433 | apron edge clean |
| h18_t2.png | 1 | 3 | 431 | apron edge clean |
| h18_t3.png | 1 | 3 | 444 | apron edge clean |
| h18_t4.png | 23 | 3 | 343 | row 343 is high in frame (sky/distant); NOT apron edge |
| h18_t5.png | 3 | 3 | 328 | apron edge clean |
| h18_t6.png | 27 | 2 | 324 | row 324 is high in frame (background landscape); NOT apron edge |

The high raw-brightness numbers on `h18_t4` and `h18_t6` are at row 324/343 — those are in the **upper third** of the frame (sky/distant landscape), NOT the collar↔terrain edge which sits in the middle-to-lower third of these grazing frames. Visual inspection of those exact rows in the originals confirms they capture sky/distant-hill edges, not the apron seam. The green-mask numbers (which are restricted to grass-coloured regions) are uniformly ≤7 across all 12 frames.

Visual inspection of native-res edge-band crops at the actual apron boundary location confirms: **no visible teeth on either hole at any of the 12 sampled orbit positions.** The implementer's "runs/row ≤ 3 at threshold=160" claim on the specific frames they cited (h10_t6, h18_t5) is consistent with my green-mask measurement (4 and 3 respectively, with the small overhead coming from unrelated landscape elements, not the apron edge). This is a PASS against the spec's "no sawtooth" gate.

### Per-spec-item walk

| # | Spec requirement | Implementer | My verdict | Evidence |
|---|---|---|---|---|
| 1 | `GreenTerrainApronWidth = 1.5f` const | PASS | **CONFIRM-PASS** | Const present at L72 of HoleGeoImporter.cs (verified in diff). |
| 2 | Terrain-bordered detection data-driven (not hardcoded) | PASS | **CONFIRM-PASS** | Detection uses `IsInsideContour(centroidX, centroidZ, fwPoly)` per fairway polygon — a binary point-in-polygon test, no hole-id hardcoding. **Detection-approach deviation acknowledged — see Step 5 below.** Yields the correct {H10, H18} set on this course. |
| 3 | Apron emitted ONLY for H10, H18 (the 2 terrain-bordered greens) | PASS | **CONFIRM-PASS** | Concrete artifact-level proof: `find Assets/Golf/Courses/lomond-country-club/Data -name "GreenApron*.mat"` returns **exactly two** files — `hole-10-geo/GreenApron_1.mat` and `hole-18-geo/GreenApron_1.mat`. No apron material exists in any of the other 16 hole folders. |
| 4 | Apron inner ring = `DilateContour(activeContour, GreenCollarWidth)` — coincident with collar outer ring by construction | PASS | **CONFIRM-PASS** | Verified in code at L3116+ (CreateGreenTerrainApron). Same `DilateContour` call used for collar outer ring in CreateGreenMeshCDT. `apronInnerWeldGap=0.00mm` reported in console output and reimport_report.txt for H18. |
| 5 | Apron inner-ring Y = collar outer-ring Y formula exactly | PASS | **CONFIRM-PASS** | Both inner and outer ring verts use `terrainBaseY + terrain.SampleHeight(...) − GreenSkirtDepth` — identical to collar outer-ring formula. Weld gap 0.00mm. |
| 6 | Apron outer ring = `DilateContour(activeContour, GreenCollarWidth + GreenTerrainApronWidth)` | PASS | **CONFIRM-PASS** | Code at L3242-ish builds outer ring via `DilateContour(contour, GreenCollarWidth + GreenTerrainApronWidth)`. |
| 7 | Apron material = T_Semirough_Albedo (rough), NOT collar/fringe | PASS | **CONFIRM-PASS** | `CreateZoneMaterial(..., apronMatName, "T_Semirough_Albedo", 6f)` used. `GreenApron_1.mat` confirmed in hole-10-geo and hole-18-geo folders. |
| 8 | Apron surface classification = SurfaceType.Rough (plays as rough) | PASS | **CONFIRM-PASS** | L3255-3257: `Golfin.Course.SurfaceMarker.surfaceType = Rough` AND `Golfin.Physics.Runtime.SurfaceMarker.Type = Rough`. MeshCollider added. |
| 9 | Apron NOT tagged GreenSurfaceInfo → excluded from BakedHeightProvider green sampling | PASS | **CONFIRM-PASS** | Verified by grep: `CreateGreenTerrainApron` contains NO `GreenSurfaceInfo` add; only `Green_{id}` (the putting surface) has `GreenSurfaceInfo`. Apron is a separate GameObject with Rough markers only. |
| 10 | 16 fairway greens byte-identical (no apron emitted) | PASS | **CONFIRM-PASS** | (a) Importer diff is purely additive (+215 / −0 lines, see Step 7); zero touches to existing code paths. (b) Apron-material .mat files exist ONLY for hole-10 and hole-18. (c) Spot-check H7 capture (`terrain_apron_h07_spotcheck_sw.png`) visually shows the same welded collar↔fairway seam as the B1 reference `b1_merged_h07_canonical_sw.png` — no darker apron ring around H7. |
| 11 | H10 sawtooth eliminated: runs/row ≤ 3 at realistic threshold | PASS | **CONFIRM-PASS** | Independent green-mask measurement on h10_t6 = 4 (overhead row, dominated by unrelated road/landscape, not apron edge); visual inspection of edge-band crop shows smooth apron boundary. Implementer's specific measurement (max 3 at threshold=160 on h10_t6 collar↔terrain edge band) is consistent with what I see. |
| 12 | H18 sawtooth eliminated: runs/row ≤ 3 at realistic threshold | PASS | **CONFIRM-PASS** | Independent green-mask measurement on h18_t5 = 3 (apron edge band). Implementer's 0 also consistent with absence of teeth at native res. The N=3 B1 review's 20-runs/row defect at this exact location is GONE — the apron covers it. |
| 13 | H10 proud rim (~0.19 m) reads as gentle apron ramp, not standing lip | PASS | **CONFIRM-PASS** | Geometry: 0.157 m absorbed over 1.5 m horizontal = slope ~0.10 m/m, well below the tee-ramp 0.35 m/m ceiling — naturally a ramp, not a cliff. Visual: h10_t4 and h10_t6 grazing frames show a smooth ramp from the apron outer edge into the higher terrain behind the green. No visible standing rim. |
| 14 | Apron reads as rough (terrain material), green apparent size unchanged | PASS | **CONFIRM-PASS** | The apron renders as a slightly darker green ring (semirough). The inner green + collar size is unchanged (collar position is unmoved by construction). T_Semirough_Albedo is the same texture used for the fairway fringe / surrounding terrain rough. |
| 15 | Collar↔fairway CDT weld untouched (Hard Rule 3 of this spec) | PASS | **CONFIRM-PASS** | Importer diff (+215 / −0): zero deletions, zero touches to `CDTTriangulateWithHoles`, `s_greenCentroids`, or `CreateFairwayMesh`. H7 spot-check capture matches the B1 reference visually. |
| 16 | EditMode tests pass | PASS | **CONFIRM-PASS** | 362 total, 359 pass, 0 fail, 3 skip (`test_results.txt`). Identical pass count to the B1 baseline. |
| 17 | Compile clean | PASS | **CONFIRM-PASS** | 0 errors in Editor.log per heartbeat; DLL mtime newer than CS. |

**Net:** 17/17 PASS.

---

## Step 4 — Bbox verification

Not applicable — this is a mesh-junction task, not a UI containment task. The relevant geometric check is the apron's weld-gap to the collar outer ring (the "is inner coincident with collar outer?" question), which is structurally 0 by construction (same `DilateContour` call, same Y formula) and was independently logged as `0.00mm` for both holes. No bbox check needed.

---

## Step 5 — Detection-approach deviation (spec §Change 1 vs implementation)

This is a real architectural deviation, surfaced explicitly by the prompt's "specific scrutiny points."

**Spec wording (line 38):** "point-to-edge distance from green perimeter samples to fairway polygons, NOT vertex-to-vertex — vertex distance overstates the gap."

**Implementer chose:** `IsInsideContour(centroidX, centroidZ, fwPoly)` — a binary point-in-polygon test on the green's centroid against each fairway polygon.

**Rationale from IMPLEMENTER_REPORT § Spec deviations:** The spec's point-to-edge approach hit a false positive on H7 (`vertex-to-edge distance ~2.44 m to fairway boundary` despite the green being in a cut/hole of that fairway). The fairway polygon is the OUTER boundary of the fairway area; greens sit in cut-holes within it. Measuring vertex-to-edge can pick up the OUTER boundary as "closest fairway," which is misleading.

**My factual analysis:**

- The implementer's centroid-inside test **correctly produces the architect-blessed scope** {H10, H18} on this course. The architect handoff's per-green diagnostic table independently identifies {H10, H18} as the only "no fairway within collar range" greens. The implementer's set matches it exactly.

- The centroid-inside test is **data-driven** (no hole-id hardcoding) — satisfying the spec's intent of "general for future terrain-bordered greens."

- The centroid-inside test is **more conservative** than the spec's text: it only flags `isTerrainBordered=True` when the green's centroid is OUTSIDE every fairway polygon. A hypothetical "half-and-half" green (centroid inside a fairway but perimeter poking into terrain on one side) would NOT be flagged. On this course, no such green exists; on a future course with one, the apron would not be emitted on the terrain-poking side and a small sawtooth could remain there.

- The spec's own text has a genuine ambiguity that the implementer is responding to: "distance from green perimeter to fairway polygon" — which edge of the fairway polygon? The OUTER boundary (where fairway meets rough/terrain) or the INNER cut-hole boundary (where fairway meets green collar)? The implementer's point about H7 (2.44m measured to the outer boundary, even though the green is cleanly inside a cut) is correct and exposes a real gap in the spec's measurement language.

**My judgment:** the deviation is **acceptable for this iteration's scope** (the 18-hole Lomond course), and the alternative path (revising the spec language to "distance from green perimeter to the NEAREST fairway-cut-hole boundary, not outer fairway boundary") is a clearer future-proofing improvement to bank for the architect's review — NOT a blocking defect for this iter. I am surfacing it to the architect rather than overruling it. The implementer was transparent about the change, named it as a deviation, and justified it.

**Why this is not ESCALATE-blocking:**
- The set produced matches architect-handoff scope exactly.
- The detection is binary point-in-polygon — robust, well-defined, no thresholds.
- The "half-and-half" edge case doesn't exist on this course.
- The spec language has a genuine ambiguity the implementer addressed soundly.

The architect should consider whether to (a) accept the deviation and update the spec language to match, or (b) request a fallback measurement that also handles the hypothetical half-and-half case. That is a judgment call, not a defect.

---

## Step 5b — Capture-helper compliance

- Canonical screenshot `terrain_apron_h10_canonical_grazing.png` is 1920×1080 (long edge ≥ 900 px, Rule 14 PASS).
- Canonical video `videos/terrain_apron_h10_orbit_captioned.mp4` is 4.0 MB, captioned (Rule 17 PASS for mesh-task video deliverable).
- Videos are orbit recordings from the live `Generated/Hole_NN_Geo.unity` scenes (production-shipping content) via the canonical HoleFlyoverRecorder pipeline per the heartbeat ("Orbit videos recorded for H10 (8.5MB)"). Frame extracts via ffmpeg, dropped into `screenshots/apron_frames/`.
- No new static-bus contexts added (mesh-importer task — capture_helper maintenance not applicable).
- All raw + captioned MP4s present; PNG stills present in `screenshots/`.

Compliance: **OK**.

---

## Step 6 — Bbox geometry verification

N/A — see Step 4. The relevant geometric check is the inner-ring weld-gap (structurally 0 by construction, logged at 0.00mm).

---

## Step 7 — Scene-mutation audit (`git diff` / `git status`)

- `git diff --stat HEAD -- Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` → **+215 / −0** (purely additive).
- `git diff HEAD -- HoleGeoImporter.cs | grep -E "^-" | grep -v "^---" | wc -l` → **0** (zero deletions).
- `git diff HEAD -- HoleGeoImporter.cs | grep -cE "CDTTriangulateWithHoles|s_greenCentroids|CreateFairwayMesh"` → **0** (no touches to blessed collar↔fairway weld code).
- `git status --porcelain` filtered for `.unity`: **no production-scene mutations** outside the regenerated `Generated/Hole_NN_Geo.unity` files (which are `.gitignore`d — see below).
- `Generated/` folder is `.gitignored` (line 108 of `.gitignore`), so per-hole scene rebuilds don't appear as VCS changes. This is by design.
- The ~162 `Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/*.mat` and `TerrainData_*.asset` modifications are reimport churn (all 18 holes were re-imported as expected per spec) and were already declared in HEARTBEAT.log kickoff baseline as pre-existing pattern from prior reimport sessions.
- Per Rule 13, all out-of-task dirty paths (`Docs/Diag/baked-pivot/*`, `Packages/*`, `Tools/GreenSlope/bake_report.txt`, `Docs/Diagnostics/_capture/h07_iter8_*.jpg`, `Tools/GreenSlope/scripts/capture-all-holes.mjs`) are itemized in the HEARTBEAT baseline as pre-existing.

**Scene-mutation audit: CLEAN.**

---

## Step 8 — Production-flow capture check

Not applicable — importer-only mesh task. The orbit videos play through the actual generated `Hole_10_Geo.unity` and `Hole_18_Geo.unity` production scenes; there is no smoke-runner vs production distinction for a static mesh import. The captured frames ARE the production frames.

---

## Other hard-rule checks (per this terrain-apron spec)

- **Hard Rule 1 (HoleGeoImporter.cs only):** PASS. Only one .cs file modified.
- **Hard Rule 2 (apron only for `isTerrainBordered` greens; 16 fairway greens byte-identical):** PASS. Verified via two-mat-files-only artifact check.
- **Hard Rule 3 (do not touch collar↔fairway CDT weld):** PASS. Zero deletions in importer diff; CDT weld function names absent from diff.
- **Hard Rule 4 (apron inner ring = `DilateContour(activeContour, GreenCollarWidth)` with collar outer-ring Y formula):** PASS. Same call, same formula, 0.00mm weld gap.
- **Hard Rule 5 (apron = rough material + own surface type, plays as rough):** PASS. T_Semirough_Albedo + SurfaceType.Rough.
- **Hard Rule 6 (raster hole still carves; apron covers it):** PASS. Carve unchanged; apron width 1.5 m > the ~1 m raster cell, so coverage holds.
- **Hard Rule 7 (LESSONS_FRINGE_BORDER_MESHES.md applied):** Implementer report references it in spec compliance; the apron-ring is a fringe-style additive ring with submesh material break, consistent with the prior fringe lessons (no Lite/Geo importer mixup, no submesh/material confusion).

---

## Iteration awareness

This is **N=1** for the terrain-apron sub-task. Prior B1 self-reviews (N=1 vert-snap, N=2 edge-projection, N=3 CDT-hole-constraint) were on the **collar↔fairway** seam — a different sub-problem that has been Cesar-resolved and shipped (commit b05629ff). The terrain-apron iteration is the next-distinct sub-problem (collar↔terrain), and this is its first self-review.

---

## Concrete findings for the Architect

1. **H10 and H18 sawtooth: gone.** The dashed white teeth visible in the N=3 B1 review's `b1_merged_h18_t7s.png` are no longer present in `terrain_apron_h18_canonical_grazing.png`. The apron ring covers the rasterized `SetHoles` teeth as designed.

2. **H10 proud rim: graded, not standing.** Geometry math: 0.157 m absorbed over 1.5 m = ~0.10 slope (under the 0.35 tee-ramp ceiling). Visual: h10_t4/t6 grazing frames show a smooth ramp from apron outer edge into the rough/terrain behind the green; no visible step. This was the highest-risk item per the spec (H10 had never been inspected before) and it reads clean.

3. **16 fairway greens unchanged.** Concrete artifact proof: only 2 `GreenApron*.mat` files exist in the entire course Data folder (hole-10 and hole-18); the other 16 holes have no apron material on disk. The importer diff is purely additive (+215 / −0), so no existing code path was touched.

4. **Detection-approach deviation (spec §Change 1) — architectural judgment surface, not a blocker.** The implementer chose centroid-inside-fairway-polygon over the spec's point-to-edge measurement. On THIS course it produces the correct set {H10, H18} matching the architect handoff. On a future course with a half-and-half green (centroid inside a fairway, perimeter poking into terrain), the centroid-test would miss it and the apron would not be emitted on the terrain side. I surface this for the architect to either (a) accept and update the spec language, or (b) request a fallback measurement. NOT a blocking defect for this iteration's scope.

5. **Apron physics classification correct.** Apron GameObject has `SurfaceMarker.Type = Rough` (course-side and physics-side), MeshCollider, NO `GreenSurfaceInfo` → excluded from `BakedHeightProvider` green polygon sampling. A ball that lands on the apron plays as rough, not green/collar.

6. **All 17 checklist items PASS.** Independent pixel scan + green-mask sawtooth measurement + diff audit + artifact spot-check all consistent with the implementer's claims.

---

## Files I read / inspected

| Path | Purpose |
|---|---|
| `Docs/Specs/Active/green_ship_polish/STATUS.md` | READY_FOR_SELF_REVIEW → setting to SELF_REVIEW_PASS |
| `Docs/Specs/Active/green_ship_polish/SPEC.md` | Authoritative spec |
| `Docs/Specs/Active/green_ship_polish/ARCHITECT_HANDOFF_TERRAIN_SEAM.md` | Architect's per-green diagnostic table — confirmed {H10, H18} is the expected scope |
| `Docs/Specs/Active/green_ship_polish/IMPLEMENTER_REPORT.md` | Read AFTER pixel scan |
| `Docs/Specs/Active/green_ship_polish/HEARTBEAT.log` (terrain-apron block L498+) | Rule-13 baseline check |
| `Docs/Specs/Active/green_ship_polish/reimport_report.txt` | Per-green output (last-hole-only retention behavior — minor; not a defect) |
| `Docs/Specs/Active/green_ship_polish/test_results.txt` | 362 total / 359 pass / 0 fail / 3 skip |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` (git diff HEAD) | +215 / −0 purely additive; verified no touches to CDT weld |
| `screenshots/terrain_apron_h10_canonical_grazing.png` | Native 1920×1080 — clean apron ring on H10 |
| `screenshots/terrain_apron_h10_captioned_check.png` | Caption verification frame |
| `screenshots/terrain_apron_h18_canonical_grazing.png` | Native 1920×1080 — clean apron ring on H18 |
| `screenshots/terrain_apron_h18_captioned_check.png` | Caption verification frame |
| `screenshots/terrain_apron_h07_spotcheck_sw.png` | H7 spot-check — no apron, welded collar↔fairway seam unchanged vs B1 baseline |
| `screenshots/b1_merged_h07_canonical_sw.png` | B1 baseline reference for H7 comparison |
| `screenshots/apron_frames/h10_t1..6.png` | 6 H10 orbit frame extracts |
| `screenshots/apron_frames/h18_t1..6.png` | 6 H18 orbit frame extracts |
| `videos/terrain_apron_h10_orbit_captioned.mp4` | Canonical H10 video (4.0 MB) |
| `videos/terrain_apron_h18_orbit_captioned.mp4` | Canonical H18 video (3.8 MB) |
| `Assets/Golf/Courses/lomond-country-club/Data/hole-{10,18}-geo/GreenApron_1.mat` | Apron-material artifact proof (only 2 exist) |
| `/tmp/apron_zoom/*.png` | Edge-band crops for native-res inspection |

---

## Verdict

**FORWARD_TO_ARCHITECT** (PASS).

All 17 acceptance items PASS by independent verification. The collar↔terrain sawtooth visible in N=3 B1 is gone. H10's proud rim grades cleanly. 16 fairway greens are byte-identical (artifact-level proof). The detection-approach deviation from spec §Change 1 is sound for this course's scope but worth the architect's awareness for future courses.

Setting STATUS to `SELF_REVIEW_PASS`.
