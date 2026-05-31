# Self-Review — `green_ship_polish` iter-13 (2-tier gate amendment)

**Iteration of self-review:** N=2 for iter-13 task (N=1 covered the drop-scaled amendment, archived inline; N=2 = this 2-tier-gate root-cause amendment).
**Reviewer:** golfin-self-reviewer
**Reviewed at:** 2026-05-30 14:10 JST (system clock)
**Verdict:** **FORWARD_TO_ARCHITECT** (PASS)

---

## Step 1 — Pixel scan (no spec, no report)

I sampled four frames at t=0/2/4/6s from the canonical H14 video `videos/green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4`, plus the H07 orbit at the same timestamps. Independent pixel description below — written before consulting the report's claims.

### H14 (canonical) at 18° grazing

- **t=0s:** Grazing view of an oval green from one side. Flag stands centered with the surface visible all around. Green reads as a single continuous mound; surface curves smoothly down to the surrounding fairway. **No visible step, ledge, or cliff anywhere on the surface.** Sandy bunker in upper-left mid-distance. Caption sits in upper-center as semi-transparent dark box (does NOT occlude the green body — sits on the sky/background above the horizon).
- **t=2s:** Rotated ~90° around the green. Surface still reads as a single smooth dome — gentle convex rise from near edge with **no perceptible discontinuity** across its entire visible face. No tier line, no ridge crease.
- **t=4s:** Different orbit quadrant. Surface uniformly smooth; faint contour banding from texture but no geometric step. Background hills clean.
- **t=6s:** Fourth orbit position. Same single-surface read; smooth curvature top-to-bottom, no cliff or stair-step. Bunker and pond in background.

**Conclusion from independent pixel scan:** at 18° grazing — where a 23cm cliff would absolutely be visible from this angle — H14 shows ZERO interior cliff. The surface reads as one continuous gently-domed green. The swale referenced in the spec is subtle but consistent with arrow-driven IDW interpolation across a single region.

### H07 (tier hole, barrier retained) at 18° grazing

- **t=0s:** Oval green with flag, gentle visible tier shading — upper-right slightly higher than lower-left. Transition reads as a smooth ramp not a cliff.
- **t=2s:** Orbit angle clearly shows two tiers — a back/upper plateau and a front/lower plateau separated by a smooth ramp. No staircase, no bumps.
- **t=4s:** Top-down-ish angle, tier line discernible as a slight shading curve, ramped not cliffed.
- **t=6s:** Tier still subtle but present, ramp continuous.

**Conclusion:** H07's 2-tier character is preserved — tier readable, ramp smooth, no staircase. Not over-smoothed away by any change to the bake's tier-hole path.

### Notable observation

The **H07 video does NOT visually contain the caption** the report claims ("H07 iter-13 2-tier gate — region=2 (genuine tier) / ~8% ramp-width=8.89m — 0 interior cliffs"). Verified by inspecting frames at t=0/1/2/3/4/5/6/7s — no overlay rendered. The H14 canonical video DOES have its caption. This is a minor secondary-deliverable inconsistency with the report. The canonical video is H14, which is captioned; H07 is a supporting clip whose visual purpose (confirm tier intact) is still served by the orbital footage. **Noted, not a FAIL** — surfaced for architect awareness.

---

## Step 2 — Re-run the gate independently

I executed `node Tools/GreenSlope/scripts/verify-ridge.mjs --all` from the current checkout:

```
=== Summary ===
Holes verified: 18/18
Interior cliff gate (all holes): 18/18 PASS
Ridge-band gate (tier holes only: 3/7/11/18): 18/18 PASS
All gates combined: 18/18 PASS

Per-hole interior cliff summary:
  H01 [single]: ✓ interior cliffs=0     H10 [single]: ✓ interior cliffs=0
  H02 [single]: ✓ interior cliffs=0     H11 [2-tier]: ✓ interior cliffs=0
  H03 [2-tier]: ✓ interior cliffs=0     H12 [single]: ✓ interior cliffs=0
  H04 [single]: ✓ interior cliffs=0     H13 [single]: ✓ interior cliffs=0
  H05 [single]: ✓ interior cliffs=0     H14 [single]: ✓ interior cliffs=0
  H06 [single]: ✓ interior cliffs=0     H15 [single]: ✓ interior cliffs=0
  H07 [2-tier]: ✓ interior cliffs=0     H16 [single]: ✓ interior cliffs=0
  H08 [single]: ✓ interior cliffs=0     H17 [single]: ✓ interior cliffs=0
  H09 [single]: ✓ interior cliffs=0     H18 [2-tier]: ✓ interior cliffs=0
```

H14: `interior cliffs (|Δh|>5cm): 0  maxΔh=1.2cm` — was 23cm pre-fix. Single region confirmed.
H13: 0 cliffs, maxΔh=1.2cm, single region.
H06: 0 cliffs, maxΔh=1.1cm, single region.
H03/H07/H11/H18: 2-region; all show `band continuity ✓`; perp slope max 2.1–3.4% well under the 12% cap.

### Gate implementation audit (verify-ridge.mjs)

Read `Tools/GreenSlope/scripts/verify-ridge.mjs` lines 160–207 (`interiorCliffScan`). Confirmed:

1. **Side-agnostic:** scans every 4-connected adjacent active cell pair (`[cz, cx+1]` and `[cz+1, cx]`). No filtering on region/side/authored-ridge-relative — this is NOT the old same-side check that let H14 hide.
2. **Whole-green-interior:** iterates all active cells (those inside the contour polygon); not restricted to a band around the ridge.
3. **Edge exclusion present:** `EDGE_EXCLUSION_M = 1.0` (line 60). For each cell, pre-computes `edgeDist[]` via `distToContourEdge` (segment distance to nearest contour edge). A pair is skipped if either cell's edge distance < 1.0m. Verify-output proves the exclusion works: e.g. H07 reports `interior pairs checked: 3299  edge-excluded: 642` — 642 edge-band pairs correctly excluded.
4. **Threshold 5cm:** `INTERIOR_CLIFF_THRESHOLD_M` line 60.

### Gate sanity — does it actually catch cliffs?

I cloned the post-fix H14 `green.json`, injected a synthetic 23cm cliff (added +0.23m to all cells with `cx >= midX, 5 <= cz < gridH-5`), then ran the same scan logic against it:

```
Synthetic-cliff H14: interior cliffs=46, maxΔh=23.9cm
✓ Gate correctly DETECTS injected 23cm cliff
```

The gate is NOT trivially passing — a real 23cm interior jump is caught (46 flagged cells, max 23.9cm). The 18/18 PASS on the actual baked greens therefore reflects genuine bake quality, not a broken gate.

---

## Step 3 — Region counts

Verified via `verify-ridge.mjs` output and `bake-green.mjs` source (line 1075: `applyRidgeBarrier = ridgePresent && TWO_TIER_HOLES.has(holeNum)`; line 1085: `regionCount = applyRidgeBarrier ? authoredRegionCount : 1`):

| Hole | Region count | Status |
|------|--------------|--------|
| H03  | 2 | tier (barrier retained) |
| H07  | 2 | tier (barrier retained) |
| H11  | 2 | tier (barrier retained) |
| H18  | 2 | tier (barrier retained) |
| H06  | 1 | single (barrier removed — was incorrectly barrier'd previously) |
| H13  | 1 | single (barrier removed) |
| H14  | 1 | single (barrier removed — was the 23cm-cliff regression source) |
| All other 11 holes | 1 | single (no traced ridge in authoring) |

Exactly the 4 PDF-documented 2-tier holes get the barrier; H06/H13/H14 are correctly single-region; all single-region holes report 0 interior cliffs.

---

## Step 4 — Video integrity

`ffprobe` on canonical H14 video `green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4`:

```
width=1920  height=1080  r_frame_rate=60/1  duration=7.816667  nb_frames=469
```

- **r_frame_rate=60/1** — far above ≥30/1 floor, NOT a 1/2 slideshow. PASS.
- **Resolution 1920×1080** — long edge ≥ 900. PASS.
- **Real orbital motion:** mean greyscale frame-pixel difference between sampled frames 8.6–13.4 (192×108 downsample). Viewpoint clearly changes across the four sampled timestamps. PASS.
- **Captioned:** "H14 iter-13 2-tier gate — region=1 (single) / 0 interior cliffs — swale from arrows, no cliff". Caption is a semi-transparent dark overlay positioned upper-center, ABOVE the visible green surface (over sky/background). Does NOT occlude the green body in any sampled frame. PASS.
- **Angle:** 18° grazing — surface topology visible. PASS.

H07 supporting video:
- 1920×1080 @ 60fps, 7.85s — passes motion gate (mean frame diff 23.2–23.96).
- **Caption claimed in report not visible in actual video.** Minor secondary-deliverable inaccuracy — see Step 1 "Notable observation". Does not invalidate the visual evidence that H07's tier is intact.

---

## Step 5 — Mutation audit

`git status --porcelain --untracked-files=all` + `git diff --stat HEAD`:

**In-scope code changes (3 files):**
- `Tools/GreenSlope/scripts/bake-green.mjs` — 402 lines changed. TWO_TIER_HOLES gate, classifyRegions trigger widened, smoothRidgeBand no-op for non-tier. As designed.
- `Tools/GreenSlope/scripts/verify-ridge.mjs` — created/modified. interiorCliffScan + TWO_TIER_HOLES kept in sync with bake. As designed.
- `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs` — `GreenOrbitElevationDeg` 38f → 18f with documented comment. Deliberate tool tweak per SPEC ("If the /green-orbit default elevation (38°) is too high to read the surface, lower GreenOrbitElevationDeg … for these clips and note it"). Acceptable.

**In-scope data regen:**
- All 18 `Assets/Resources/HoleData/Hole_NN/green.json` re-baked. `Hole_06/green.json` is `??` (new file — git shows no prior tracked version, meaning `--all` bake now produces output for H06 under the gate, which it previously did not for this hole). The report mistakenly says H06 was "re-baked"; in reality it's a first-time bake output. **Cosmetic report inaccuracy, not a defect** — in-scope file.
- `Tools/GreenSlope/bake_report.txt` — regenerated.

**Pre-existing dirty paths (attributed in HEARTBEAT.log `=== iter-13 2-tier-gate kickoff baseline ===` block at line 85):**
- `Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/*.mat` / `.asset` — present at iter-13 kickoff (heartbeat line 5–13).
- `Assets/Golf/Courses/lomond-country-club/Data/hole-14-geo/*.mat` / `.asset` — present at amendment kickoff (heartbeat line 98–106). NOT new this iter.
- `Assets/Plugins/NuGet/*.dll`, `Packages/manifest.json`, `Packages/packages-lock.json` — environment, attributed.
- `.claude/hooks/__pycache__/*.pyc` — environment, expected.

**Untracked debug/diagnostic files (pre-existing, out of scope, no requirement to clean):** `Assets/Scenes/Debug/Hole_07_Geo_Diagnostic.unity`, `Assets/Scripts/Editor/CourseImporter/Debug/GreenVariantDiagnostic.cs`, `Tools/GreenSlope/screenshots/holes/*.png`, `Tools/GreenSlope/scripts/capture-all-holes.mjs`, `Docs/Diagnostics/_capture/*.png`.

**No unexpected scene mutations.** No production `.unity` file was touched. No `m_IsActive: 0` flips or RectTransform changes anywhere.

---

## Step 6 — Bbox geometry check

Not applicable. This is a 3D mesh/terrain bake task with no UI containment claims. The numeric interior-Δh gate (Step 2) replaces the containment-bbox check for this domain — and that gate I re-ran independently with 18/18 PASS plus a sanity injection test.

---

## Step 7 — Capture-helper compliance

The orbit videos were produced by `HoleFlyoverRecorder.cs` (Unity Recorder-driven), not `CaptureHelper.SnapGameView()`. This is the canonical bot-video path established by `puttpath_predictor_perf_and_design` and standardized in `feedback_prefer_bot_videos.md`. The canonical screenshot is a frame extract from that video, also sanctioned. Compliant with the screenshots / video rules. No new HUD context was added that would trigger CaptureHelper maintenance protocol.

---

## Final verdict

**FORWARD_TO_ARCHITECT** (PASS) — STATUS set to `SELF_REVIEW_PASS`.

### Reasoning

The 2-tier gate amendment delivers exactly the architectural change the spec demands. Independent verification confirms:

1. **H14 is now a single smooth swale with no cliff** — confirmed visually at 18° grazing across four orbit angles. A 23cm cliff would be obvious at this angle; none is present.
2. **The interior-Δh gate is genuinely side-agnostic, whole-green, and edge-excluded** — confirmed by reading source and by seeing the gate correctly catch a synthetic injected 23cm cliff (46 flagged cells, max 23.9cm).
3. **All 18 holes pass the gate** — independently re-run by me, not just reported.
4. **The 4 PDF-documented 2-tier holes (3/7/11/18) retain their tier character** — H07 visibly shows a smooth two-tier ramp at grazing angle, not over-smoothed.
5. **No out-of-scope mutations.** Pre-existing dirty paths attributed via HEARTBEAT baseline. Code changes confined to bake/verify scripts + a documented recorder elevation tweak.

### Minor items surfaced (not blocking; architect may handle if desired)

1. **H07 supporting orbit video lacks the rendered caption** that the implementer's report claims is present. Visual content is correct (tier intact); only the overlay is missing. The canonical video (H14) is correctly captioned.
2. **`Hole_06/green.json` was reported as "re-baked" but is actually a new file** — git shows no prior tracked version. In-scope, just a cosmetic inaccuracy in the file table.
3. **`HoleFlyoverRecorder.cs GreenOrbitElevationDeg = 18f`** is a deliberate tool tweak for grazing capture. Worth a follow-up decision whether to revert to 38° after this task or keep grazing as the default for green-review clips.

---

## Files

| Path | Why relevant |
|------|--------------|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/green_ship_polish/SELF_REVIEW.md` | This review |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/green_ship_polish/STATUS.md` | Set to `SELF_REVIEW_PASS` |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/green_ship_polish/videos/green_orbit_h14_2tier_h14_2tier_gate_orbit.mp4` | Canonical video — captioned, 60fps, 1920×1080, real orbital motion |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/green_ship_polish/screenshots/h14_2tier_gate_grazing_frame2s.png` | Canonical screenshot — 1920×1080 grazing |
| `/Users/cesar/Documents/GolfinRedux/Tools/GreenSlope/scripts/verify-ridge.mjs` | Audited; interior-Δh scan side-agnostic + edge-excluded; gate catches synthetic cliffs |
| `/Users/cesar/Documents/GolfinRedux/Tools/GreenSlope/scripts/bake-green.mjs` | TWO_TIER_HOLES gate implementation verified |
