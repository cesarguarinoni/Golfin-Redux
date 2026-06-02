# Red-Team Review — `green_ship_polish` (terrain-apron scope)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Reviewed at:** 2026-06-01 20:40 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS** — actively tried to break it on three axes; could not.

Scope under review: the collar↔terrain seam fix (terrain apron ring) on H10 + H18,
built additively on top of the Cesar-accepted B1 fitted-plane seat + CDT collar↔fairway
weld (`b05629ff`).

---

## Angle I captured myself (re-shot, not re-used)

- **H10 raw orbit video, native 1920×1080, grazing arc** — extracted frames at
  t=0.5/2.0/4.0/6.0/7.0/7.8 from `videos/terrain_apron_h10_orbit.mp4` (NOT the
  reviewer-blessed canonical). H10 is the proud-rim 0.157 m hole never inspected
  before this pass — highest-risk surface.
  - `/tmp/redteam_frames/h10_orbit_t6.0.png` (lowest grazing angle)
  - `/tmp/redteam_frames/h10_t6_rightedge_crop.png`, `h10_t6_leftedge_crop.png` —
    native crops of the apron↔collar inner edge AND apron↔terrain outer edge.
  - `/tmp/redteam_frames/h10_t6_apronedge.png` — native crop of the outer apron arc.
- **H18 raw orbit** at t=2.0/6.0 + native crop `/tmp/redteam_frames/h18_t6_rightedge_crop.png`.
- **H7 spotcheck orbit** seam crop `/tmp/redteam_frames/h07_spot_seam_crop.png` (prior-rejection defect #4 location).

All show smooth continuous boundary curves — no sawtooth teeth, no standing lip, no
z-fight shimmer at either the apron↔collar inner edge or the apron↔terrain outer edge.
The H10 0.157 m proud rim grades as a gentle ramp.

---

## Metrics I re-ran (my numbers, not the reviewer's)

### Runs-per-row sawtooth, reviewer band (rows 432–864, cols 192–1728, luma thr 160)
| Frame | maxRuns/row | rows>3 |
|---|---|---|
| H10 canonical | **2** | 0 |
| H18 canonical | **0** | 0 |
| H7 spotcheck | 1 | 0 |
| **B1 H18 baseline (sawtooth ref)** | **12** | **20** |

My numbers match the reviewer's exactly (H10=2, H18=0, B1=12/20). The defect was
real on B1 and is eliminated. (Caveat: this metric only catches the WHITE raster
teeth — the dark apron edge was instead verified by native-crop eyeball, above.)

### Detection re-run against import-source data (Tools/UHoleGeo export, all 18 greens)
Replicated `IsInsideContour(centroid)` + a half-and-half perimeter-leak test
(% of perimeter samples with no fairway within GreenCollarWidth=0.9 m):

| Result | Holes |
|---|---|
| Terrain-bordered (apron emitted) | **{10, 18}** — exactly matches artifact + scene |
| Fairway-clean, 0.0% terrain-leaked perimeter | all 16 others |

**No half-and-half green exists on Lomond** — every fairway-bordered green has 0.0%
of its perimeter poking into raw terrain, so the centroid-inside detection produces
zero false negatives. The spec-deviation (centroid-inside vs point-to-edge) is safe
*for this course* — independently confirmed, not taken on faith.

### Artifact + scene gating
- `find … -name "GreenApron*.mat"` → exactly 2: `hole-10-geo/`, `hole-18-geo/`.
- `m_Name: GreenApron_1` GameObjects across all 18 generated scenes → only H10 & H18 (1 GO + 1 mesh each). 16 others = 0.
- Importer diff `git diff --numstat HEAD` → **+215 / −0** (zero real deletions).
- Blessed-weld grep in diff (`CDTTriangulateWithHoles|s_greenCentroids|CreateFairwayMesh`) → **0 hits**.

### Physics (verified in scene YAML, not just claimed)
- Apron GameObject: `Golfin.Physics.Runtime.SurfaceMarker.Type = 4` (=Rough),
  `Golfin.Course.SurfaceMarker.surfaceType = 3` (=Rough), `MeshCollider` present,
  **no `GreenSurfaceInfo`** (only the `Green_1` mesh GO carries the single
  GreenSurfaceInfo in the scene) → excluded from green height provider. Ball plays Rough.

### Weld coincidence-by-construction (verified structurally, not via the circular log value)
`DilateContour(contour, GreenCollarWidth)` is a deterministic pure function
(`OffsetContourOutward`, n→n points). `CDTTriangulate` runs with
`Settings.RestoreBoundary = true` and **no** boundary refinement / Steiner insertion
on boundary edges → the collar's outer-ring verts ARE the dilated-contour points.
The collar per-vert loop assigns dilated-ring verts (d≈collarWidth → tBlend=0)
`outerRingY = terrainBaseY + SampleHeight(v) − GreenSkirtDepth`, identical to the
apron inner-ring formula. XZ and Y coincide → watertight weld. Holds.

---

## Prior-rejection defects (Rule 15 / Step 1) — each GONE

The `CESAR_REJECTION.md` was on the green-seat-rearch sub-problem (a different scope),
fixed in B1 (`b05629ff`). This additive apron pass must not regress them.

| Cesar defect | Verdict | Evidence |
|---|---|---|
| #1 Green sunken below fairway | **GONE** | H7 spotcheck shows green raised/flush; matches B1 blessed baseline |
| #2 Flag/cup floating over green | **GONE** | flag base planted on green surface in H7 spotcheck + all H10/H18 frames |
| #3 Green flat (2-tier lost) | **GONE** | H7 shows tonal 2-tier undulation; B1 proved relH spread preserved (scalar shift) |
| #4 Fairway hole visible at borders (T-junction cracks) | **GONE** | H7 seam crop `/tmp/redteam_frames/h07_spot_seam_crop.png`: smooth collar↔fairway, no grey slivers |

H7 spotcheck is visually identical to `b1_merged_h07_canonical_sw.png` (no apron ring,
unchanged weld) → blessed work untouched.

---

## Three break-attempts and why each failed

1. **Visual** — Re-shot H10's harshest grazing angle (the never-before-inspected
   0.157 m proud-rim hole) at native res and cropped both seam bands. Both the
   apron↔collar inner edge and apron↔terrain outer edge are smooth continuous arcs.
   No sawtooth, no standing lip, no z-fight. *Failed to break.*

2. **Geometric** — Re-ran runs-per-row (matched reviewer exactly), and re-ran the
   detection + half-and-half perimeter-leak test against the actual import-source
   data: exactly {H10, H18}, 0.0% leak on all 16 others. No metric sits near a
   threshold (collar=0.9 m vs nearest fairway 25 m / 33.7 m; apron slope 0.105 vs
   ceiling 0.35). Weld coincidence holds structurally. *Failed to break.*

3. **Spec-intent** — Diff is +215/−0, blessed weld functions absent from diff,
   exactly 2 apron materials + GameObjects, physics = Rough + no GreenSurfaceInfo,
   prior-rejection defects all gone on H7. The point of the SPEC (kill the
   collar↔terrain sawtooth on the only 2 terrain-bordered greens without touching
   the 16 fairway greens or the blessed weld) is fully met. *Failed to break.*

---

## Worst things found (non-blocking)

- **Stale diagnostic number in IMPLEMENTER_REPORT.** It cites H18
  `nearestFairway=22.0m`; the actual value is ~32.8 m (`reimport_report.txt`) and my
  independent computation is 33.7 m. The 22.0 m figure is wrong/stale. NOT a blocker:
  the gating decision (`isTerrainBordered=True`) is identical for any value > 0.9 m.
  Recommend correcting the report's H18 figure for hygiene.
- **`innerWeldGapMax` log value is circular.** In `CreateGreenTerrainApron` the gap is
  measured as `abs(iY − collarOuterY)` where `collarOuterY` is re-derived from the
  *identical* expression as `iY` → tautologically 0. The "0.0 mm by construction"
  claim is rhetorically self-referential. NOT a blocker: the real weld coincidence is
  established structurally (above) — the conclusion is correct even though the cited
  proof is circular. Recommend the log measure against an actual collar outer-ring
  vertex sample if this number is to be cited as evidence in future.

Neither rises to a defect Cesar would reject on sight.

---

## Verdict

**ARCHITECT_REVIEW_PASS.** Tried to break it visually, geometrically, and against
spec-intent on the highest-risk surface (H10 proud rim, first inspection); each
attempt came up empty. The sawtooth is gone, the proud rim grades as a ramp, the
weld is coincident by construction, the apron is gated to exactly {H10, H18} with no
half-and-half false-negative, the 16 fairway greens are byte-equivalent (zero apron),
the blessed weld is untouched at the diff level, and the apron plays as Rough. Two
non-blocking reporting blemishes noted for hygiene. Advancing to Cesar's approval gate.
