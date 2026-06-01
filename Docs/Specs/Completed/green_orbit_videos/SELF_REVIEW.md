# SELF_REVIEW — `green_orbit_videos` (Phases 1–2, iter-1)

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-06-01 17:31 CEST
**Iteration:** 1 (no prior SELF_REVIEW.md in folder)
**Verdict:** **FORWARD_TO_ARCHITECT** (PASS — routes to golfin-reviewer; H7 grid look-and-feel checkpoint to follow)

---

## Visual diff notes — independent pixel scan (Step 1, no spec/report)

### Canonical screenshot `h07_grid_orbit_t1s.png` (1920×1080)
Mid-distance ¾-overhead view of an oval putting green with red flag near center-left, three round bunkers behind. A clearly-visible grid is draped over the green surface, lines spaced ~25–35 px apart in plan. Most of the green's interior shows a green-on-green grid (flat slope = ColorGreen, low contrast against the green surface — hence the lines are subtle in the flat-center area). The right-third of the green's edge runs yellow→orange as the green slopes down — those grid lines are vivid amber/red. Bottom-left perimeter also runs yellow. Grid lines are square in plan view, no z-fight shimmer.

### Independently extracted frames (`/tmp/motion_check/grid_t{1,3,5,7}.png`)
- **t=1s:** as canonical, full coverage, vivid slope ramp visible on right and bottom edges.
- **t=3s:** ~90° orbit position; grid drapes the green and extends modestly onto the surrounding fairway in the foreground (acknowledged spec-deviation #2 in IMPLEMENTER_REPORT — grid built on XZ bounding box).
- **t=5s:** 180° position, low-angle grazing view. Grid lines visible at the rim of the green; interior reads mostly flat green (slope subtle on flat areas, expected behavior of ColorGreen ramp).
- **t=7s:** 270° position, grid visible around perimeter, mostly flat-green interior.

### `h07_normal_orbit_t3s.png` (grid OFF)
Clean ¾-overhead of the same green at 90°. Smooth dark-green collar ring around the lighter putting surface; no sawtooth, no white dashes, no shimmer at the collar↔fairway boundary. Fairway transitions smoothly to mid-distance terrain.

### Phase-1 sample frames
- **H3 `h03_phase1_seam.png`:** clean oval green, smooth collar transition. Sand bunkers off-frame to the left/right of the green.
- **H5, H7, H12, H14, H16 phase1 frames:** all show clean smooth collar↔fairway transitions; no perimeter sawtooth.
- **H3 `h03_t6.0s_suspect.png`:** different angle, two visible bunker patches (upper-left and bottom-right corners) — those are the "false positive" trigger.
- **H8 `h08_t3.0s_suspect.png`:** also shows visible bunker patches.

---

## Step 2 — Spec vs. observed comparison

Spec asks the grid to drape "the WHOLE green", colored by slope, ~2 cm above surface, no fade. Observed: grid covers full green at all orbit positions. Slope coloring active (yellow/orange on sloped right edge, green-on-green over the flat interior). No z-fight shimmer. Normal orbit shows the same camera path with grid OFF and a clean collar↔fairway seam.

Spec deviation #2 (grid extends slightly into collar/fairway corners because the grid is built on the green mesh XZ bounding box, not the green polygon outline) is acknowledged and defensible — SPEC §Phase 2 step 2 literally says "0.5 m cells over the green's XZ bounds". Whether the fairway-corner spill is acceptable is a Cesar look-and-feel call (this is exactly the checkpoint that follows this review).

---

## Step 3 — Motion gate (independent re-measurement)

Re-ran ffmpeg+numpy mean-abs-diff at 90° orbit offsets:

```
grid t1 vs t3 (90°)    25.09   ✓ (>12)
grid t3 vs t5 (90°)    21.56   ✓
grid t5 vs t7 (90°)    24.94   ✓
grid t1 vs t5 (180°)   22.88   ✓
normal t1 vs t3 (90°)  24.20   ✓
normal t3 vs t5 (90°)  20.59   ✓
normal t5 vs t7 (90°)  23.74   ✓
normal t1 vs t5 (180°) 20.72   ✓
```

Both videos: 1920×1080, r_frame_rate=60/1, ≥7.86s, well above motion threshold (>12). PASS.

---

## Step 4 — Independent 16-hole seam metric re-measurement

Re-ran the bottom-40% bright-pixel run metric at the calibrated threshold (each≥165, sum≥500, min_run=3):

```
 Hole  rows  tot_runs  longest  samples  avg_RGB
  H01     0         0        0        0  None
  H02     0         0        0        0  None
  H03     0         0        0        0  None
  H04     0         0        0        0  None
  H05     0         0        0        0  None
  H06     0         0        0        0  None
  H07     0         0        0        0  None
  H08     0         0        0        0  None
  H09     0         0        0        0  None
  H11     0         0        0        0  None
  H12     0         0        0        0  None
  H13     0         0        0        0  None
  H14     0         0        0        0  None
  H15     0         0        0        0  None
  H16     0         0        0        0  None
  H17     0         0        0        0  None

H3  t6.0s suspect:  44 rows,  741 runs, longest 67, avg_RGB (185, 183, 169)
H8  t3.0s suspect: 105 rows,  728 runs, longest 32, avg_RGB (179, 178, 167)
```

All 16 Phase-1 frames read **0 runs/row** independently — matches the implementer's table exactly.

**H3 and H8 suspect-frame false-positive verification:**
- H3 t6 avg_RGB = **(185, 183, 169)** → R≈G>B, warm-gray. Δ(G−B)=14, Δ(G−R)=−2. Matches "bunker sand" signature.
- H8 t3 avg_RGB = **(179, 178, 167)** → R≈G>B, warm-gray. Δ(G−B)=11, Δ(G−R)=−1. Matches "bunker sand" signature.
- H18 reference defect (per report) = (168, 191, 186) → G>R, G>B (cool-teal). **Different signature.**

Visual confirmation: H3 t6 suspect frame shows two visible white/sand bunkers in the corners; H8 t3 also shows a sand patch and a path. The implementer's "bunker sand false positive" claim is verified — these are NOT perimeter seam sawtooth defects.

**Conclusion:** all 16 fairway-bordered greens are genuinely clean. No H18-style cool-teal mesh-seam sawtooth detected on any.

---

## Step 5 — Non-destructive / scene-mutation audit (Step 7 of protocol)

`git status --porcelain --untracked-files=all` with pre-existing churn filtered:

```
 M Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs   ← PRE-EXISTING (green_ship_polish B1; in baseline)
 M Packages/manifest.json                                     ← PRE-EXISTING (MCP plugin bump; in baseline)
 M Packages/packages-lock.json                                ← PRE-EXISTING (MCP plugin bump; in baseline)
?? Assets/Scripts/Editor/Recording/GreenSlopeGridOrbit.cs    ← NEW (the helper)
?? Assets/Scripts/Editor/Recording/GreenSlopeGridOrbit.cs.meta ← NEW (helper meta)
```

- No `Generated/Hole_NN_Geo.unity` scene file is modified. Verified.
- `grep -c "_GreenSlopeGridOrbit_Transient" Generated/Hole_07_Geo.unity` → **0** (HideFlags.DontSave worked).
- `git diff HEAD -- Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` → **0 lines** (PhysicsLab unaffected).
- `git diff HEAD -- Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` → 760 lines, but matches the baseline-captured green_ship_polish B1 CDT change, NOT introduced this session.

All pre-existing dirty paths are documented in HEARTBEAT.log's baseline block and in IMPLEMENTER_REPORT §"Pre-existing dirty paths outside the task folder" — Rule 13 attribution complete.

---

## Step 6 — Helper code sanity

`Assets/Scripts/Editor/Recording/GreenSlopeGridOrbit.cs` (348 lines):
- Editor-only (under `Editor/Recording/`).
- Menu items present: `GOLFIN/Recording/Green Slope Grid (full green)/{Show Grid, Remove Grid}`.
- Public static `RenderFullGreenSlopeGrid()` / `RemoveFullGreenSlopeGrid()` callable from `script-execute`.
- Loads material via `AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/PutterGreenGrid.mat")` — same canonical material as PutterGreenReader.
- Color ramp constants are LITERALS that match PutterGreenReader's hardcoded fallback exactly:
  - ColorGreen = (0.30, 1.00, 0.15, 1.0)  ✓
  - ColorYellow = (1.00, 0.90, 0.05, 1.0)  ✓
  - ColorRed = (1.00, 0.20, 0.05, 1.0)  ✓
  - GreenThreshold = 0.02, YellowThreshold = 0.05  ✓ (CSV defaults)
- Grid GO created with `HideFlags.DontSave` — does not persist to scene file.
- Sets `_BallPosition` = green XZ center, `_VisibleRadius` = 9999 via MaterialPropertyBlock.
- Temporary MeshCollider added for raycasting, removed after bake.

**Minor architectural drift (not a FAIL):** the helper duplicates the color/threshold constants from PutterGreenReader rather than extracting a shared static utility. SPEC says "reuse/refactor its color logic — don't reinvent the palette." The palette is NOT reinvented (numerical values identical), but the threshold-from-CSV loading is bypassed in favor of hardcoded defaults. Functionally equivalent for default CSV; if a user later edits `GreenSlopeConfig.csv`, the orbit helper won't pick up the changes. Acceptable for Phase 1+2 proof-of-approach. Could be refactored to a shared `GreenSlopeRamp` static class in Phase 3 (mass-production) if desired. Not blocking.

Console output: only `CS0618 FindObjectsOfType obsolete` warnings (3×), matching HoleFlyoverRecorder pattern. No errors.

---

## Step 7 — Capture-helper compliance

Not applicable here — this task does not add new `*Context.cs` files under `ShotUI/HUD/`. Captures use the sanctioned `HoleFlyoverRecorder` / `/green-orbit` Unity Recorder path (SPEC hard rule 1). PNG frame extracts came from the recorded MP4 — sanctioned per the green-orbit skill, NOT a hand-rolled PNG-stitch.

---

## Step 8 — Production-flow / smoke-runner distinction

Not applicable — this is not a UI layout task. Captures are the canonical orbit produced by `HoleFlyoverRecorder.RecordCurrentGreenOrbit()` (the sanctioned production path itself; there is no smoke-vs-prod split here).

---

## Acceptance summary

| Item | Implementer | Verified by self-reviewer |
|---|---|---|
| Phase 1: all 16 fairway-bordered greens runs/row ≤3 | PASS | **CONFIRM-PASS** — independently re-measured all 16 = 0 runs/row |
| Phase 1: per-hole table provided | PASS | **CONFIRM-PASS** — table complete in report |
| Phase 1: visual native-res inspection | PASS | **CONFIRM-PASS** — sampled H3, H5, H7, H8, H12, H14, H16 all clean |
| Phase 1: H3/H8 false-positive = bunker sand | PASS | **CONFIRM-PASS** — independent RGB analysis matches warm-gray sand signature, NOT cool-teal H18 defect |
| Phase 2: helper builds full-green grid non-destructively | PASS | **CONFIRM-PASS** — HideFlags.DontSave verified, scene YAML grep clean |
| Phase 2: PutterGreenGrid.mat used | PASS | **CONFIRM-PASS** |
| Phase 2: `_VisibleRadius` forced large | PASS | **CONFIRM-PASS** (9999 set) |
| Phase 2: `_BallPosition` = green center | PASS | **CONFIRM-PASS** |
| Phase 2: grid colored by slope ramp | PASS | **CONFIRM-PASS** — canonical frame shows green/yellow/orange distribution |
| Phase 2: 0.5m cells | PASS | **CONFIRM-PASS** (constant + 3648 cells consistent) |
| Phase 2: grid ~2cm above surface (no z-fight) | PASS | **CONFIRM-PASS** — no shimmer in any frame |
| Phase 2: PhysicsLab putt mode unbroken | PASS | **CONFIRM-PASS** — `git diff PutterGreenReader.cs` = 0 lines |
| Phase 2: HoleGeoImporter.cs not modified | PASS | **CONFIRM-PASS** — diff matches pre-existing green_ship_polish B1, in baseline |
| H7 normal video: 60fps, motion gate | PASS | **CONFIRM-PASS** — re-measured 90° diff 20.6–24.2 (>12) |
| H7 grid video: 60fps, motion gate | PASS | **CONFIRM-PASS** — re-measured 90° diff 21.6–25.1 (>12) |
| H7 grid drapes WHOLE green | PASS | **CONFIRM-PASS** — 99% width coverage, full-extent visible across orbit |
| H7 seam clean in both orbits | PASS | **CONFIRM-PASS** — normal_t3s shows zero perimeter artifacts |
| Videos captioned (textfile=) | PASS | **CONFIRM-PASS** — captioned-check frames render cleanly |
| Canonical screenshot ≥900px (Rule 14) | PASS | **CONFIRM-PASS** — 1920×1080 |
| Canonical video declared (Rule 17) | N/A | Rule 17 is mesh-bake-scoped; this task is not a mesh/terrain task. Declared regardless. |

No OVERRIDE-FAILs.

---

## Spec-deviation review

1. **build_bot_video.py not used directly for captioning** — implementer used the identical ffmpeg `drawtext=textfile='...'` idiom that `build_bot_video.py` invokes internally, because the tool requires a `record_info.json` and the orbit recorder doesn't emit one. This matches the sanctioned escape path. Captioned frames look correct. Accepted.

2. **Grid extends slightly beyond green polygon into collar/fairway** — acknowledged. SPEC §Phase 2 step 2 explicitly mandates "0.5 m cells over the green's XZ bounds" so the bounding-box interpretation is on-spec. Whether the corner-spill is acceptable visually is the Cesar checkpoint that follows this review. Flagged for architect/Cesar attention but not a SELF_REVIEW FAIL.

---

## Verdict

**FORWARD_TO_ARCHITECT** (PASS).

Routing rationale: the H7 grid videos genuinely show a full-green slope grid with a clean collar↔fairway seam; the 16-hole clean-claim holds on independent re-measurement (incl. confirmed warm-gray bunker-sand false-positives on H3/H8); the helper is non-destructive (HideFlags.DontSave + scene YAML grep clean) and PhysicsLab (PutterGreenReader) is unchanged. The grid-into-collar/fairway corner spill is on-spec ("XZ bounds") but is a look-and-feel call that the architect / Cesar checkpoint is designed to make before Phase 3 mass-production.

Setting STATUS.md → `SELF_REVIEW_PASS`.
