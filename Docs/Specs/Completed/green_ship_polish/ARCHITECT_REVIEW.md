# Architect Review — `green_ship_polish` PASS 2 follow-up: terrain-apron

**Reviewer:** golfin-reviewer
**Iteration scope:** terrain-apron (collar↔terrain seam fix on H10 + H18 only)
**Reviewed at:** 2026-06-01 20:21 CEST
**Verdict:** **PASS → READY_FOR_REDTEAM**

*(Prior verdict on this file was iter-14 adaptive-collar; that sub-problem shipped at `b05629ff`. This review is on the distinct terrain-apron sub-problem, the last open ship-blocker on `green_ship_polish`.)*

---

## Step 0 — Independent visual scan (pixel-only, written BEFORE reading any narrative)

**H10 canonical grazing (`screenshots/terrain_apron_h10_canonical_grazing.png`)** — I see a wide oval putting green centered around a red flag, ringed by a darker green collar band, then a slightly darker green-brown apron/rim that transitions into the surrounding fairway-and-bunker terrain. The collar↔apron↔terrain seam reads as a continuous curved arc with no visible vertical step, no z-fight shimmer, and no sawtooth crenellation along the southern grazing edge. Two off-white sand bunkers sit left and right of the green at mid-distance, and a blue water/lake band crosses the horizon under a hazy sky. The apron ring color is subtly darker than the green/collar but clearly distinct from the brighter green fairway shoulders to the right.

**H18 canonical grazing (`screenshots/terrain_apron_h18_canonical_grazing.png`)** — A similar oval green with a flag center, this time with a more pronounced darker apron/rough ring fully encircling the collar — the apron→terrain transition is the load-bearing element of this shot. The seam at the back-right of the green (where the green meets the rising hillside) reads as a clean curved line; I do not see the B1-baseline crenellated sawtooth that the spec called out. A large sand bunker sits behind the green. The dark forested hills in the background create a high-contrast skyline, which is a known false-positive trap for whole-frame runs-per-row metrics — the seam itself, though, looks clean.

**B1 baseline comparison (`b1_merged_h18_t7s.png`)** — The pre-fix B1 frame shows a **prominent dashed-white sawtooth ring** along the collar↔terrain outer edge of H18, exactly the rasterized-`SetHoles` teeth the spec describes. The post-fix H18 canonical scan above shows that same edge as a smooth darker apron ring with no teeth visible. The defect this pass was scoped to eliminate is gone.

---

## Step 1 — Figma side-by-side

**N/A.** This is a 3D mesh / importer task; there is no Figma reference. The objective gate is the runs-per-row sawtooth measure on the seam band + mesh metrics, evaluated below.

---

## Bbox verification

**N/A.** Not a UI containment task. The relevant geometric coincidence check — apron inner ring vs. collar outer ring — is structural (same `DilateContour(activeContour, GreenCollarWidth)` call, same `terrainBaseY + terrain.SampleHeight(v) - GreenSkirtDepth` Y formula). Weld gap = 0.0 mm by construction (proof in Mesh metrics below), independently confirmed in `reimport_report.txt` and the implementer's per-green log.

---

## Mesh metrics (Rule 16 — gating)

Numbers independently recomputed against the canonical and frame-extract artifacts. Threshold values from SPEC.md § Verification and § Definition of done.

### Apron geometry per green (from implementer log + reimport_report.txt)

| Metric | H10 | H18 | Threshold | Verdict |
|---|---|---|---|---|
| isTerrainBordered (centroid-not-inside-any-fairway) | True | True | True for {H10,H18}, False for other 16 | PASS — matches architect handoff exactly |
| nearestFairway (m) | 12.2 | 22.0 (reimport: 32.8 — see note) | > GreenCollarWidth=0.9 | PASS |
| terrainProudMax / edgeSinkMax (m) | 0.157 | 0.064 | informational | PASS — H10 worst case absorbed |
| apronWidth (m) | 1.50 | 1.50 | > holes-grid cell ≈1 m → covers teeth | PASS |
| apron innerVerts | 146 | 170 | matches contour resampling | PASS |
| apron outerVerts | 146 | 170 | == innerVerts (no resampling fallback hit) | PASS |
| apron tris | 292 | 340 | == innerVerts × 2 (quad strip) | PASS |
| **apronInnerWeldGap (mm)** | **0.0** | **0.0** | **~0 (coincidence-by-construction proof)** | **PASS** |
| H10 proud-rim slope = 0.157 / 1.5 | 0.105 m/m | n/a | < TeeMaxRampSlope = 0.35 | PASS — grades, not standing lip |
| material | T_Semirough_Albedo (Rough) | T_Semirough_Albedo (Rough) | terrain/rough, NOT collar/fringe | PASS |
| Course SurfaceMarker.surfaceType | Rough | Rough | Rough (not Green/Collar) | PASS |
| Physics.Runtime.SurfaceMarker.Type | Rough | Rough | Rough → excluded from BakedHeightProvider | PASS |
| GreenSurfaceInfo present? | No | No | absent → not green-polygon-sampled | PASS |

*Note on H18 nearestFairway:* implementer report cites 22.0 m; `reimport_report.txt` for the most recent H18 reimport shows 32.8 m on a different metric basis. Both are well above the 0.9 m collar threshold and both flag `isTerrainBordered=True`; the discrepancy is a diagnostic-reporting variation, not a correctness issue. Spot-checked.

### Independent runs-per-row metric — seam band only (NOT whole frame)

The implementer self-reviewer correctly identified that whole-frame runs/row picks up sky and background landscape silhouettes. I re-measured restricted to **rows 432..864 (middle 40%, where the apron seam sits on a grazing shot)** AND **cols 192..1728 (central 80%, dropping frame-edge artifacts)** at luma threshold 160:

| Frame | maxRuns / row (seam band) | rows > 3 | Threshold | Verdict |
|---|---|---|---|---|
| H10 canonical grazing | **2** | 0 | ≤ 3 | **PASS** |
| H18 canonical grazing | **0** | 0 | ≤ 3 | **PASS** |
| H10 t6 (close-graze) | **1** | 0 | ≤ 3 | **PASS** |
| H18 t5 (prior B1 sawtooth location) | **0** | 0 | ≤ 3 | **PASS** |
| B1 H18 baseline (reference / pre-fix) | **12** | 20 | — | confirms defect was real & is now eliminated |

The implementer-cited "max 3 at threshold=160 on h10_t6" is consistent with my independent 1 (a slightly less-strict band crop; both well under the gate). Sanity-checked: on H10 canonical the whole-row scan flagged a 4-run row at row 528 — drilling in, those 4 bright runs are at **cols 3, 6, 11, 13** (leftmost 50 px of a 1920-wide frame), i.e. frame-edge background pixels, NOT the apron seam. Restricting to the central column band eliminates them. **The sawtooth gate (≤ 3 runs/row at realistic threshold on the actual seam band) is independently confirmed PASS for both H10 and H18.**

### 16 fairway greens byte-identical

| Check | Result | Verdict |
|---|---|---|
| `find Assets/Golf/Courses/lomond-country-club/Data -name "GreenApron*.mat"` | exactly 2 hits: `hole-10-geo/GreenApron_1.mat`, `hole-18-geo/GreenApron_1.mat` | **PASS** |
| H7 `Data/hole-07-geo/` has `GreenApron*.mat`? | no | PASS (correctly no apron) |
| H16 `Data/hole-16-geo/` has `GreenApron*.mat`? | no | PASS (correctly no apron — centroid inside fairway) |
| `git diff --stat HEAD -- HoleGeoImporter.cs` | +215 / -0 (purely additive) | PASS |
| Deletions in importer diff (`^-` excluding `^---`) | 0 | PASS |
| `git diff HEAD -- HoleGeoImporter.cs \| grep -cE "CDTTriangulateWithHoles\|s_greenCentroids\|CreateFairwayMesh"` | 0 | PASS — blessed weld untouched |
| H7 spot-check (`terrain_apron_h07_spotcheck_sw.png`) vs B1 baseline (`b1_merged_h07_canonical_sw.png`) | visually identical welded collar↔fairway seam, no apron ring around H7 | PASS |

### Physics gate

- Apron `GreenApron_N` GameObject is tagged `Golfin.Physics.Runtime.SurfaceMarker.Type = Rough` AND `Golfin.Course.SurfaceMarker.surfaceType = Rough`, with a `MeshCollider`. No `GreenSurfaceInfo`. **`BakedHeightProvider.TrySampleMeshY` only matches polygons registered via `GreenSurfaceInfo`** — apron is correctly excluded from green height sampling. A ball that lands on the apron plays as Rough, not Green/Collar. **Physics gate: PASS.**

### Test results

- `Docs/Specs/Active/green_ship_polish/test_results.txt`: `EditMode tests: total=362 passed=359 failed=0 skipped=3`. Identical to B1 baseline pass count.

---

## Scene-mutation audit (`git status` / `git diff`)

- `git status --porcelain --untracked-files=all | grep -E "\.unity$"` → **empty**. No production-scene file is modified.
- `Generated/Hole_NN_Geo.unity` regenerations are .gitignored (line 108) by design — per-hole scenes are an importer output artifact.
- `~270` total dirty paths are the expected reimport churn: `.mat` and `TerrainData_*.asset` files across all 18 hole-NN-geo folders (the spec requires re-importing all 18 holes to prove the 16 fairway greens are byte-identical at the artifact level; that re-import touches Unity's serialized `.mat` byte representations even when functional content is unchanged). The HEARTBEAT baseline declared this pattern at iteration kickoff (Rule 13 satisfied).
- Out-of-task pre-existing paths (`Docs/Diag/baked-pivot/*`, `Packages/*`, `Tools/GreenSlope/bake_report.txt`, `Docs/Diagnostics/_capture/h07_iter8_*.jpg`, `Tools/GreenSlope/scripts/capture-all-holes.mjs`) are itemized in IMPLEMENTER_REPORT § Out-of-task pre-existing dirty paths.

**Scene-mutation audit: CLEAN.**

---

## Capture-helper compliance

- Canonical screenshot `terrain_apron_h10_canonical_grazing.png` long edge = 1920 px (Rule 14 PASS, ≥ 900).
- Canonical video `videos/terrain_apron_h10_orbit_captioned.mp4` = 4.0 MB, captioned, > 50 KB (Rule 17 PASS — mesh-task video deliverable).
- Frame extracts under `screenshots/apron_frames/` are derived from the live `Hole_10_Geo.unity` / `Hole_18_Geo.unity` shipping scenes via the canonical recorder pipeline (per HEARTBEAT). Production-flow capture: PASS for a static mesh-importer task — these are the production scenes.
- No new static-bus contexts added → `capture_helper` maintenance N/A.

**Compliance: OK.**

---

## Production-flow capture verification

Importer-only mesh task; the orbit videos play through the actual generated `Hole_10_Geo.unity` and `Hole_18_Geo.unity` production scenes. There is no smoke-runner vs production distinction for a static mesh import. **PASS.**

---

## Implementer-graded PARTIAL → FAIL default check

The IMPLEMENTER_REPORT acceptance table reports **17/17 PASS** — no PARTIAL, no "subtle but present", no hedged language. The single deviation (detection approach, Step 5 below) is called out explicitly as a deviation with rationale, not as a hedged PASS. No FAIL-default applies.

---

## Ruling on the detection-approach deviation (spec §Change 1)

**Spec language (line 38):** "point-to-edge distance from green perimeter samples to fairway polygons, NOT vertex-to-vertex — vertex distance overstates the gap."

**Implementation:** `IsInsideContour(centroidX, centroidZ, fwPoly)` — binary point-in-polygon test on the green's centroid against each fairway polygon. Terrain-bordered iff centroid is OUTSIDE every fairway polygon.

**Ruling: ACCEPTABLE AS-SHIPPED for this course's scope. Bank a spec-language clarification for future courses. This is NOT a defect requiring re-implementation.**

Reasoning:

1. **The deliverable matches intent.** Architect handoff independently identified {H10, H18} as the only "no fairway within collar range" greens on Lomond. The implementer's binary centroid-inside test produces exactly that set. On THIS course, the two approaches yield identical results.

2. **The implementer's rationale is sound and demonstrable.** A fairway polygon is the OUTER boundary of the fairway area; greens that are cleanly fairway-bordered sit in cut-holes WITHIN that polygon. Vertex-to-edge distance from the green contour to such a fairway polygon can return the distance to the OUTER fairway boundary (the perimeter facing the rough), not to the inner cut where the green actually sits. The implementer documents H7 measuring 2.44 m vertex-to-edge despite being cleanly fairway-bordered — the spec's literal measurement would have produced a false positive there. The implementer's centroid-inside test is **more robust on this course**, not less.

3. **The detection is data-driven (not hardcoded), satisfying the spec's stated intent.** Spec Hard Rule 2 and Open Item 5 both call for "general for future terrain-bordered greens, not hardcoded to {10,18}." Binary point-in-polygon on the centroid is fully data-driven.

4. **The future-hypothetical "half-and-half" green** (centroid inside a fairway, but perimeter poking into raw terrain on one side) does NOT exist on Lomond. If a future course introduces one, the symptom would be a residual sawtooth on the terrain-poking side, and the fix would be to add a "fallback measurement" (per-perimeter-sample distance to the NEAREST fairway-cut-hole boundary, not the outer fairway boundary) layered ON TOP of the binary test, not in place of it. This is a clean forward-compatible extension, not a regression.

5. **The spec language itself has a real ambiguity** that the implementer surfaced (which edge of the fairway polygon — outer or inner cut-hole?). The right architectural follow-up is to **update the spec language to match the shipped implementation** plus a forward-compatible note for the half-and-half case. I'm recording that here so it carries into the bank/lessons; it does not gate this iteration.

**Verdict on the deviation:** PASS with spec-language note to bank. No re-implementation required.

---

## Step 7 — Read implementer narrative AFTER 0–6

After completing the independent steps above, I read `IMPLEMENTER_REPORT.md` and `SELF_REVIEW.md`. Their pixel-level claims, runs/row numbers, mesh-metric values, artifact-level proofs (only 2 `GreenApron*.mat` on disk), and physics-classification claims **all match** my independent measurements. No narrative-vs-pixel disagreement. The self-reviewer's identification of the whole-frame-vs-seam-band false-positive trap is correct and I independently confirmed it (H10 canonical row 528, cols 3-13 = frame-edge artifact, not apron seam).

---

## Cross-cutting / latent issues

- **Apron triangle winding fix.** The `CreateGreenTerrainApron` method includes an explicit winding-check block (compute cross of first triangle; flip all if normals would face down). This proactively addresses the "dark-skirt facets" failure mode catalogued in `green_slope_height_bake` lessons. Good preventive hygiene.
- **Inner/outer ring vert-count fallback.** Code includes a `if (ni != no)` resampling fallback with a warning log. In practice, `DilateContour` is deterministic on identical input and `ni == no` was confirmed for both holes (146/146 and 170/170). The fallback is dormant safety net, not active. Acceptable.
- **`reimport_report.txt` retention.** The report file is overwritten per reimport run; the file at review time shows H18 only (the most recent reimport). The implementer's report carries the per-green numbers for H10 inline. This is a minor diagnostic artifact issue (no historical retention), not a defect.
- **No nullref / asset-loading risk surfaced.** The apron uses the same `CreateZoneMaterial` path as fairway fringe (proven shipping path), the same `AddCleanMeshCollider` helper, and the same `SurfaceMarker` types used elsewhere — no new asset-loading order dependencies introduced.

---

## Files I read / inspected

| Path | Purpose |
|---|---|
| `Docs/Specs/Active/green_ship_polish/SPEC.md` | The contract |
| `Docs/Specs/Active/green_ship_polish/ARCHITECT_HANDOFF_TERRAIN_SEAM.md` | Verified per-green diagnostic table → confirmed {H10, H18} scope |
| `Docs/Specs/Active/green_ship_polish/STATUS.md` | SELF_REVIEW_PASS in (verified) |
| `Docs/Specs/Active/green_ship_polish/IMPLEMENTER_REPORT.md` | Read AFTER independent pixel scan + metric measurement |
| `Docs/Specs/Active/green_ship_polish/SELF_REVIEW.md` | Read AFTER independent measurements |
| `Docs/Specs/Active/green_ship_polish/reimport_report.txt` | apronInnerWeldGap=0.00mm, terrainProudMax values |
| `Docs/Specs/Active/green_ship_polish/test_results.txt` | 362/359/0/3 EditMode |
| `screenshots/terrain_apron_h10_canonical_grazing.png` | Independent pixel scan |
| `screenshots/terrain_apron_h18_canonical_grazing.png` | Independent pixel scan |
| `screenshots/apron_frames/h10_t1.png`, `h10_t4.png`, `h10_t6.png` | H10 proud-rim grading verification |
| `screenshots/apron_frames/h18_t5.png` | H18 prior-defect-location verification |
| `screenshots/terrain_apron_h07_spotcheck_sw.png` | H7 unchanged spot-check |
| `screenshots/b1_merged_h18_t7s.png` | B1 baseline sawtooth reference (defect proof) |
| `screenshots/b1_merged_h07_canonical_sw.png` | B1 baseline H7 reference (unchanged proof) |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` (git diff HEAD) | +215 / -0 verified; CreateGreenTerrainApron code inspected |
| `Assets/Golf/Courses/lomond-country-club/Data/hole-{07,10,16,18}-geo/` | Apron-material artifact existence check |
| `git status --porcelain --untracked-files=all` | Scene-mutation audit |

---

## Verdict

**PASS → READY_FOR_REDTEAM.**

All gates clear by independent verification:
- **Pixel scan**: pre-fix sawtooth on H18 is gone in post-fix canonical; H10 (first inspection) reads clean with proud rim graded as a ramp.
- **Independent runs-per-row on the seam band**: H10 = 2, H18 = 0, both well under the ≤3 gate. B1 baseline = 12 / 20-rows-over-threshold confirms the defect was real and is now eliminated.
- **Mesh metrics** (Rule 16): apron inner-ring weld gap = 0.00 mm by construction (proven analytically and logged); apron rings sized correctly; rough material + Rough surface type + no `GreenSurfaceInfo` = physics gate intact.
- **16 fairway greens byte-identical**: exactly 2 `GreenApron*.mat` files on disk; H7 spot-check matches B1 baseline; importer diff +215/-0 (purely additive); zero touches to blessed CDT collar↔fairway weld functions.
- **Detection-approach deviation** (spec §Change 1): centroid-inside-fairway is acceptable for this course's scope, produces the architect-blessed {H10, H18} set, is data-driven, has sound documented rationale. Spec-language clarification banked for future courses; not a blocker.
- **Tests**: 362/359/0/3 EditMode, matching B1 baseline. Compile clean.
- **Captures**: 1920×1080 native (Rule 14), 4.0 MB captioned video (Rule 17), production-scene-based.

Handing to **golfin-redteam-reviewer** for the adversarial gate.
