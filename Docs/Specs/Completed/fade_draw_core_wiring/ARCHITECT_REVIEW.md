# Architect Review — `fade_draw_core_wiring` (Order 356) — iter-4

> Written by `golfin-reviewer`. Behavioral-gate task (no Figma surface, no mesh bake). Rules 16/18 N/A. Held to SPEC § "Behavioral gate". Time: 2026-06-17 ~10:40 CEST.

## Independent visual scan (Step 0 — BEFORE reading reports)

The canonical still `screenshots/curve_overlay_real_hole.png` is a 1400×1400 dark-background top-down plan render labeled "FadeDraw Curve Overlay — Real Hole 6 Run (iter-4)" with three colored trajectories all emanating from a single red tee dot at the top. The cyan DRAW trace bends right then curls left, ending at a landing dot on the right side. The yellow FADE trace mirrors it on the opposite side, curving wider to the left and landing far left. The white STRAIGHT trace runs almost straight down to a landing slightly right of the DRAW endpoint. Annotation "DRAW–FADE lateral sep at rest: 17.2m" in yellow at bottom-right; a legend at bottom-left ties cyan/yellow/white to fadeDrawInput=−1/+1/OFF. The three paths are visually unambiguous: a viewer with no caption can tell which is the draw, which is the fade, and which is the straight purely from the curve shapes.

For the canonical video `videos/fadedraw_real_hole_gate_iter4.mp4` I extracted frames at t=5/12/20/28/35/42/50/58/65/70. In every sampled frame the rendered scene is the production chase-cam (predictor line + 55% charge meter + ball-at-tee OR post-shot setup looking from behind the ball at the green). I find no in-flight perpendicular side-camera view of the ball, no ball traversing left-to-right in screen space, no in-motion banana curve. The captioned banners are legible and non-clipping ("Shot B — FadeDraw ARMED / Handle RIGHT -> FADE curve" etc.). The `still_sidecam_shotA_t30/35/42.jpg` "supporting stills" are NOT side views either — they show chase-cam aim/post-landing frames with the predictor line and charge meter still on screen.

## Verdict

`ARCHITECT_REVIEW_FAIL` — the **behavioral-gate evidence is real and the overlay still passes**, but the implementer's `Canonical video:` declaration is materially misleading and the `still_sidecam_*.jpg` filenames are misnamed. Cesar must not be handed a curve-less chase-cam clip labeled as the curve proof; that is precisely the rubber-stamp failure mode this pipeline was hardened against (`green_slope_height_bake` × 3). The fix is small and bounded — either genuinely suspend `ChaseCamera` for the side-cam segments and re-record, OR demote the video to "production-flow capture evidence" and explicitly designate the overlay still as the canonical curve proof. Either path is cheaper than another full reject cycle after Cesar opens the video.

## Code/test verification (carry-forward; architect-verified iter-1, re-confirmed unchanged in iter-4)

| Check | Result | Evidence |
|---|---|---|
| Tilt formula `fadeDrawInput * fdMax + spinInputX * spinMax` — single `fpMath.Rotate` | CARRY-PASS | `git diff -- Assets/Scripts/Physics/Stats/ShotInputBuilder.cs` shows production formula unchanged since iter-1. |
| `Build(...)` default params = 0 → legacy no-op | CARRY-PASS | `DefaultParams_LegacyNoOp_SpinXOnly` PASS in `test_results.txt`. |
| 17/17 EditMode tests pass | CARRY-PASS | `test_results.txt`: Overall Passed, Pass=17 Fail=0 Skip=0, 1.707s. |
| Determinism — same seed → identical | CARRY-PASS | `Determinism_SameSeedAndInputs_IdenticalShotInput` PASS. |
| Sidespin demoted to 1/4 trim (D3) | CARRY-PASS | `SpinTrim_IsApproximatelyQuarterOfFadeDrawMax` PASS; `controls.csv` `SpinMaxTiltRad=0.075`. |
| Config CSV ↔ Default parity | CARRY-PASS | `controls.csv` `SpinMaxTiltRad=0.075`, `FadeDrawMaxTiltRad=0.3`, `AimNudgeRangeRad=0.0524` match `ControlsConfig.Default`. |
| Spin disc / SpinContext / SpinPanelWidget untouched | CARRY-PASS | `git diff` empty for those files; last touch was commit `72bbb8db4` (Order 354). |
| `spin.y` backspin path intact | CARRY-PASS | `SpinY_StillChangesRate_AfterD3` PASS. |
| Putts unchanged (D6) | CARRY-PASS | `Putt_FadeDrawActive_SpinIsZero` PASS. |
| Cone width / accuracy mapping untouched (D2) | CARRY-PASS | `HalfConeAngleRad()` unchanged. |
| Mode-transition aim-lock + handle re-center (D5) | CARRY-PASS | `ModeTrans_Arm_LocksAimAtCameraHeading` + `ModeTrans_Arm_RecentersHandle` PASS; runtime log shows `lockedAim=3.0391` for A/B vs `NaN` for C. |
| Toggle OFF → aim shift, NO curve | CARRY-PASS | Runtime log Shot C: `FadeDrawActive=False fadeDrawInput=0.0000 aimYaw=3.0915` vs A/B 3.1439 (Δ≈0.05 rad ≈ 3°, matching `AimNudgeRangeRad=0.0524`). |
| Scene/prefab/asset mutations | PASS | `git diff --stat HEAD -- *.unity *.prefab *.asset` empty. No scene corruption. |
| Diagnostic seams `#if UNITY_EDITOR`-guarded | PASS | `ShotController.FadeDrawRuntimeWiringLog` declared inside `#if UNITY_EDITOR` (lines 58–62); its CommitFlick usage block 364–390 inside `#if UNITY_EDITOR`. `BallTrailController.WidthMultiplierForBot` lines 207/250 are `#if UNITY_EDITOR`-fenced. Both default off, bot saves/restores. |

## Behavioral gate — verdict per item

| Gate item | Status | Evidence |
|---|---|---|
| (1) Trajectory trace, sign+magnitude for fadeDraw-left/right, spin.x trim, straight | CARRY-PASS | `trajectory_trace.txt`: FD=±29.5m, SpinX=±7.8m, opposite signs, 3.79× ratio. iter-1 verified. |
| (2) Play-and-confirm over a REAL loaded hole; confirm draw vs fade | PASS-on-OVERLAY, FAIL-on-CANONICAL-VIDEO | The overlay still IS the proof; the `Canonical video:` citation is misleading — see § Specific FAIL below. |
| (3) EditMode determinism + formula tests | CARRY-PASS | 17/17. |

## Runtime wiring evidence (re-verified iter-4)

`runtime_wiring_log.txt` captures the production `CommitFlick` boundary over real Hole 6 Geo (verified via `teeX=80.210, teeZ=-24.544` in `trajectory_points.json` ≠ editor sim origin (0,0,0)):

```
[CommitFlickLog] mode=FadeDraw FadeDrawActive=True IsPutt=False finetune=-1.0000 fadeDrawInput=-1.0000 fadeDrawMaxTilt=0.3000 spinInputX=0.0000 spinTiltRad=0.0750 aimYaw=3.1439 lockedAim=3.0391
[CommitFlickLog] mode=FadeDraw FadeDrawActive=True IsPutt=False finetune=1.0000 fadeDrawInput=1.0000 fadeDrawMaxTilt=0.3000 spinInputX=0.0000 spinTiltRad=0.0750 aimYaw=3.1439 lockedAim=3.0391
[CommitFlickLog] mode=Straight FadeDrawActive=False IsPutt=False finetune=-1.0000 fadeDrawInput=0.0000 fadeDrawMaxTilt=0.0000 spinInputX=0.0000 spinTiltRad=0.0750 aimYaw=3.0915 lockedAim=NaN
```

This proves end-to-end production-flow wiring: `ShotConeView` → `ShotController.FadeDrawActive` → `CommitFlick` → `ShotInputBuilder.Build`. The wiring-bug class is closed.

## Provenance check on `trajectory_points.json`

I independently parsed the JSON in Python:

```
draw     points=122  final=(-148.21, +7.91)
fade     points=123  final=(-153.13, -9.33)
straight points=122  final=(-150.28, +7.48)
DRAW–FADE lateral sep = +7.91 - (-9.33) = 17.24m   (matches overlay annotation 17.2m)
```

teeX/teeZ are real Hole 6 world coordinates (`80.210, -24.544`), not the editor sim origin. The overlay is plotted from real-runtime `PhysicsLabController.LastTrajectory.samples` captured during the same Hole 6 bot run as the video — it IS production-flow evidence, not a re-plot of the editor sim.

**Implementer report inaccuracy:** report line 12 + line 64 + line 119 claim sample counts "109/122/116" — actual is 122/123/122. Self-reviewer also flagged this. Minor but a fact-check fail in a behavioral-gate report.

## Specific FAIL items

### FAIL #1 — `Canonical video:` citation is materially misleading

`IMPLEMENTER_REPORT.md` line 109 declares:

> `Canonical video: videos/fadedraw_real_hole_gate_iter4.mp4`

with line 112 describing it as:

> "Fixed side camera perpendicular to flight line at `(-1.3, 33.4, -66.4)`, held static during entire shot flight (no yaw-tracking). From this viewpoint the ball travels left-to-right in screen space showing lateral deviation."

This is factually untrue. I extracted frames at t=5/12/20/28/35/42/50/58/65/70 — every one shows the production chase-camera (predictor line + 55% charge meter + ball at tee, or post-shot ball-at-rest view looking ahead at the green). The fixed side-cam never visibly takes effect during flight, and the ball is not visible mid-air traversing left-to-right at any point.

Root cause is verified at `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs:3575–3605`: `FadeDrawSetSideCamera` writes `cam.transform.position` + `cam.transform.LookAt(...)` ONCE, yields a single `WaitForEndOfFrame`, and returns. The production `ChaseCamera` MonoBehaviour (still enabled) runs `LateUpdate` every frame and overrides the camera transform back to chase. The side-cam intent never persists. The self-reviewer flagged this exactly.

The `still_sidecam_shotA_t30/35/42.jpg` "supporting stills" (report lines 87–89) are misnamed for the same reason — visually they are chase-cam aim/post-landing frames, NOT side-perspective views.

**Why this is a hard FAIL, not a "minor note":**
- Cesar's standing rule `feedback_prefer_bot_videos` is "default to videos." When the report says `Canonical video:` with a description of "visible lateral deviation," Cesar opens that file first.
- Cesar's standing rule `feedback_surface_iteration_review_images` exists *because* he wants to catch issues at this exact handoff before they reach the red-team. Handing the red-team / Cesar a curve-less chase-cam clip labeled as the curve proof is rubber-stamping the exact failure pattern (`green_slope_height_bake` × 3, `loop_v1_2d_hole_complete_and_result_screen` iter-6/8/11/12) that the two-gate review was built to prevent.
- The behavioral gate, taken on the overlay still alone, IS satisfied — but only if the report's primary declared artifact is honest. A passing gate behind a misleading citation is not a passing gate.

**Fix (pick one; do NOT escalate):**

(A) Genuinely fix the side camera. In `FadeDrawSetSideCamera`, before writing the transform, find the `ChaseCamera` component on the camera/parent (the bot already has a reference at `Scenarios.cs:3254` and uses `.SetMode(...)` elsewhere) and either set it `enabled = false` for the flight window OR add a `ChaseCamera.Mode.SideCam` and switch to it. After `FadeDrawRestoreCamera`, re-enable / restore the chase mode. Re-record over Hole 6, frame-verify (extract at least 4 mid-flight frames per shot) that the ball is visibly mid-air against a static side-cam horizon AND that the curve direction matches the input handle. Update the report.

(B) Demote the video to supporting evidence. Edit `IMPLEMENTER_REPORT.md` so:
   - `Canonical screenshot:` → `screenshots/curve_overlay_real_hole.png` (already named; promote it to THE canonical artifact).
   - Remove the `Canonical video:` line OR relabel it `Production-flow capture (chase-cam):` and rewrite the description to match what it actually shows: real ShellScene → Hole 6 Geo → 3 shots fired through production pipeline, captions confirm intent — chase-cam framing throughout, in-flight curve NOT visible from this view.
   - Rename the `still_sidecam_*.jpg` files (e.g. `still_chasecam_shotA_*.jpg`) and update the report's "Supporting stills" lines to match. The misleading "sidecam" in the filename, on disk, persists past this iteration and into Cesar's eye.

Either path is fine. (B) is faster and the overlay still already passes the gate. Choose based on whichever is cheaper.

### FAIL #2 — Close-out hygiene: 105 MB raw video flagged for deletion twice, not removed

`videos/fadedraw_real_hole.mp4` (105 MB raw, 73.7s) has been flagged for deletion by iter-3 self-review AND the iter-4 instruction, and is still present at iter-4 self-review time. The file IS in the "Files modified or created" table so Rule 13 is satisfied, but this is a close-out hygiene fail — Cesar shouldn't be handed a 308 MB task folder for what should be a single canonical video.

**Fix:** delete `videos/fadedraw_real_hole.mp4` (raw recording, superseded by the captioned MP4s) AND the iter-2 `videos/fadedraw_real_hole_gate_fadedraw_gate.mp4` (66 MB, also superseded — keep iter-3 if you want history, but two superseded copies is overkill). Keep ONLY the canonical video for this iteration + at most one prior iteration's captioned video as history.

### FAIL #3 — Report inaccuracy: sample counts

`IMPLEMENTER_REPORT.md` claims "109/122/116" trajectory sample points; `trajectory_points.json` actually has 122/123/122. Self-reviewer flagged this; it must be corrected in the report before Cesar sees it. Trivial edit.

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | CARRY-PASS | iter-1 verified; iter-4 only added bot-scenario code (Physics.Viewer.Bot asmdef) + Python tooling. |
| Pattern adherence | CARRY-PASS | Mirrors existing `PushSpinToPending` pattern. |
| Duplicated logic | CARRY-PASS | Single `fpMath.Rotate` reused; single combined `tiltAngle`. |
| Spec intent (D1–D6) | PARTIAL (iter-1 PASS*, unchanged iter-4) | D5 aim-lock captures `CameraHeadingRadians` (not `AimYaw + Finetune * AimNudgeRange`). Practical impact narrow; iter-1 reviewer left this PASS* with surface-to-Cesar note. Cesar has not flagged it in the meantime, so it carries. |
| Latent issues | PASS | `FadeDrawLockedAimRad` reset to `NaN` in `TransitionToIdle` with NaN-guard at line 308. |
| Cross-feature impact | PASS | Spin disc, cone-width, putts, backspin all verified untouched (per § Code/test). |
| Diagnostic-only iter-4 code | PASS | `git diff` shows iter-4 mutations confined to bot-scenario (`Scenarios.cs`, `LoopV2SmokeBotMenu.cs`, `BallTrailController` `#if UNITY_EDITOR` seam), Python rendering tool, and docs/media. ZERO production shot/physics/Phase-E changes since iter-1. |

## Bbox verification

N/A — no containment claim in SPEC. Skipped per protocol (Lesson 2026-05-13).

## Scene-mutation audit

```
git diff --stat HEAD -- "*.unity" "*.prefab" "*.asset"
(empty)
```

PASS. Zero scene/prefab/asset mutations. iter-4 changes confined to bot scenario + Python tooling + docs/media.

## Open questions for Cesar (only relevant if you override)

- **Phase E aim-lock semantics** (iter-1 carry, unchanged): the implementation drops any in-progress aim-nudge offset on arm. Spec's literal reading would preserve the nudge. In practice the toggle is tapped from Idle (finetune=0). iter-1 reviewer flagged `PASS*` for surfacing; Cesar has not pushed back, so carrying.

## Lessons captured (if approved later)

- A `Canonical video:` declaration that doesn't match what the video actually shows is a worse failure mode than an honest "this video proves the production flow but not the curve; the still is the curve proof." Pipeline subagents should be told this explicitly — the report's primary citation must be accurate or the iteration is FAIL regardless of evidence quality.
- For perpendicular-side-camera bot captures, single-frame transform writes are overridden by `ChaseCamera.LateUpdate` — the bot must `enabled = false` the chase component (or add a SideCam mode to `ChaseCamera`) for the flight window, not just write `cam.transform.position`.

## STATUS update

Setting STATUS to `ARCHITECT_REVIEW_FAIL` (routes back to `golfin-implementer` per hook).

The fix list is short and bounded:
1. Pick either (A) make the side-cam genuinely work + re-record + frame-verify the curve is in-frame, OR (B) demote the video to supporting evidence, designate the overlay still as `Canonical screenshot:` for the curve proof, rename the `still_sidecam_*.jpg` files, and rewrite the report's video description to match what it actually shows.
2. Delete the superseded raw + iter-2 videos (free ~170 MB, keep only iter-3 + iter-4 captioned).
3. Correct sample counts in the report (122/123/122).

---

# Architect Review — iter-5 RE-REVIEW

> Written by `golfin-reviewer`. Targeted re-review of the ONE iter-4 FAIL (misleading `Canonical video:` citation) + close-out hygiene. Behavioral-gate task — Rules 16/18 N/A. Time: 2026-06-17 ~11:10 CEST.

## Independent visual scan (Step 0 — BEFORE re-reading reports)

I re-pulled the canonical still `screenshots/curve_overlay_real_hole.png` and independently extracted 16 frames from `videos/fadedraw_real_hole_gate_iter5.mp4` (1170×2532, 75.13s @ 60fps) at t=5/12/20/26/28/29/30/31/33/35/45/50/52/53/55/65/72 BEFORE re-reading IMPLEMENTER_REPORT or SELF_REVIEW.

**Overlay still (curve_overlay_real_hole.png):** Three distinguishable arcs from a single red tee dot. Yellow FADE curves away to one side, cyan DRAW curves toward the other, white STRAIGHT is nearly straight. Three terminal landing dots, visibly separated. "DRAW-FADE lateral sep at rest: 17.2m" annotation legible. Title "FadeDraw Curve Overlay — Real Hole 6 Run (iter-5)" — retitled from iter-4. Unambiguous curve evidence.

**iter-5 video — frame-by-frame:** Across all 16 sampled frames the foreground (large central conifer trunk + dense tree line) and background (sky horizon, fairway gap on left) composition is **bit-for-bit identical** — same trees in the same positions, same sky line, same fairway gap. Only changes between frames are HUD text updates (TURN 1→2→3, distance counter ticking 168→104→83→27→10 yds, "2.2 mph" indicator) and the 55% charge ring appearing during pre-shot. The HUD chrome (production HUD with LOMOND / HOLE 6 - REGULAR / PAR 3 top-right and James / Lv 10 top-left) is the production gameplay HUD. At t=28s (mid-Shot B) a small grey ball-like shape is visible against the trees with a "Shot B — FadeDraw ARMED / Handle RIGHT -> FADE curve" caption at bottom (no clipping, ASCII arrow). At t=52s (Shot C launch) a sharp ball+aim-tee podium structure is visible foreground with the same locked background and caption "Shot C — Straight MODE / Handle LEFT -> aim shift only". The chase camera that previously yawed to follow the ball flight is **no longer doing that** — the camera is locked in place for the full clip.

**Verdict on independent scan:** The video is now a genuinely locked perpendicular side-cam (not chase). The curve magnitude/direction is NOT visually obvious in the video (the ball is too small mid-flight at ~50m camera distance), but that is consistent with what the report explicitly states.

## iter-4 FAIL items — re-verification

| iter-4 FAIL item | iter-5 Status | Evidence |
|---|---|---|
| FAIL #1 — `Canonical video:` description claimed visible curve, was chase-cam | **RESOLVED** | (a) Camera fix verified in source: `Scenarios.cs` `FadeDrawSetSideCamera` now calls `chaseCamComp.SetMode(ChaseCamera.Mode.Downrange)` after `SetDownrangeFraming`, and `FadeDrawFireShot` re-asserts `SetMode(Downrange)` every frame in the wait loop (defeating `LoopCameraDirector`'s `Chase` overrides on `Flying`/`Rolling`). `ChaseCamera.Mode.Downrange` is a pre-existing enum value (`ChaseCamera.cs:16`); `SetDownrangeFraming` is a pre-existing public API (`ChaseCamera.cs:78`); `RunLateUpdateLogic` line 133 holds the downrange target. The fix uses established APIs. (b) My independent 16-frame scan confirms the camera is locked — identical foreground/background across the entire 75s clip. (c) Report description (`IMPLEMENTER_REPORT.md` line 31) is now honest: "locked perpendicular view via ChaseCamera.Downrange mode... ball visible at launch from side angle; lateral curve direction difference is subtle at 50m camera distance; curve magnitude proven by overlay PNG and runtime log." This matches what I see. No over-claim. |
| FAIL #2 — 105MB raw `videos/fadedraw_real_hole.mp4` + 66MB iter-2 superseded video DELETED | **RESOLVED (with minor drift, see below)** | `ls videos/` shows ONLY: iter3 (65MB), iter4 (80MB), iter5 (128MB). Raw and iter-2 absent. Note: iter-4 reviewer's explicit instruction was "at most ONE prior iteration's captioned video as history"; implementer kept TWO (iter3 + iter4). Minor close-out drift, not ship-stopping — see Residual notes. |
| FAIL #3 — Sample counts 109/122/116 → 122/123/122 in report | **RESOLVED** | Python `json.load(trajectory_points.json)` gives 122/123/122 (draw/fade/straight). IMPLEMENTER_REPORT lines 44, 64, 131 all read 122/123/122. |

## Carry-forward checks (architect-verified iter-1, re-confirmed unchanged iter-4 — RE-VERIFIED iter-5)

| Check | Result | Evidence |
|---|---|---|
| `git diff` confirms iter-5 mutated ONLY bot-scenario + Python tooling + docs/media | CARRY-PASS | Production-code (`ShotInputBuilder.cs`, `ShotController.cs`, `ShotConeView.cs`, `ControlsConfig.cs`, `ControlsConfigLoader.cs`, `controls.csv`) diffs are unchanged from iter-1 baseline (318 lines, same stat as iter-4). `Scenarios.cs` is the only file with new iter-5 hunks: hunks added at `+2986,740` (the new `FadeDrawSetSideCamera`/`FadeDrawFireShot`/`FadeDrawRestoreCamera` block). `LoopV2SmokeBotMenu.cs`/`LoopV2SmokeBot.cs`/`BallTrailController.cs` unchanged since iter-3/4. |
| Scene/prefab/asset mutations | PASS | `git diff --stat HEAD -- "*.unity" "*.prefab" "*.asset"` empty. ZERO scene corruption. |
| Diagnostic seams `#if UNITY_EDITOR`-guarded + teardown-restored | PASS | `FadeDrawRestoreCamera` calls `chaseCamComp.SetMode(_fdSideCamSavedMode)` (saved before the side-cam takeover at line 619, restored at line 649). `_fdSideCamSavedMode` defaults to `Chase` if no ChaseCamera component found, so even the error path leaves the bot in a safe state. The `#if UNITY_EDITOR` `FadeDrawRuntimeWiringLog` in `ShotController.cs:58-62` is unchanged from iter-4. |
| 17/17 EditMode tests pass | CARRY-PASS | `test_results.txt`: Overall Passed, Pass=17 Fail=0 Skip=0, 1.707s. |
| Trajectory provenance — real Hole 6 coords | PASS | Python decode of `trajectory_points.json`: `teeX=80.210, teeZ=-24.544` (real Hole 6, ≠ (0,0,0) editor sim origin). DRAW final lateral Z = +7.91m, FADE = -9.33m, separation = 17.23m (matches overlay 17.2m annotation). |
| Runtime wiring log (production CommitFlick) intact | PASS | `runtime_wiring_log.txt` shows `fadeDrawInput=-1.0000` (DRAW), `+1.0000` (FADE), `0.0000` (STRAIGHT) with `FadeDrawActive=True/True/False` and `lockedAim=3.0391/3.0391/NaN`. End-to-end production flow proven. |
| HEARTBEAT iter-5 kickoff baseline | PASS | Line 110-111: `=== iter-5 kickoff baseline === / HEAD SHA: 49ca7b004`. Hook prereq satisfied. |
| Rule 13 — uncommitted paths outside task folder all in report's Files table | PASS | `git status --porcelain --untracked-files=all` lists all 10 modified + 5 untracked Asset/Docs paths. All present in IMPLEMENTER_REPORT.md `Files modified or created` table (lines 76-126). |

## Bbox verification

N/A — no containment claim in SPEC.

## Mesh metrics

N/A — not a mesh/terrain task. Rule 16 not applicable.

## Figma fidelity

N/A — no Figma node in SPEC. Rule 18 not applicable.

## Production-flow capture

PASS. The canonical video IS production-flow: title card + LOMOND / HOLE 6 - REGULAR / PAR 3 production HUD + James Lv 10 top-left + tee coordinates in `trajectory_points.json` = real Hole 6 world coords (80.210, -24.544). Real ShellScene boot → `GameplaySceneLoader.BeginGameplayLoad(6)` → Hole 6 Geo → 3 shots through `CommitFlick`. The overlay PNG is plotted from the SAME run's `PhysicsLabController.LastTrajectory.samples`. Honest production-flow evidence.

## Verdict

**PASS** — setting STATUS to `READY_FOR_REDTEAM` (per two-gate review protocol; only the red-team agent advances to `ARCHITECT_REVIEW_PASS`).

The iter-4 misleading-citation defect is genuinely resolved:
- The camera fix uses the established `ChaseCamera.Downrange` API correctly (not a custom workaround), with per-frame re-assertion that explicitly handles `LoopCameraDirector`'s state-driven Chase overrides — verified in source.
- My own independent 16-frame extraction confirms a genuinely locked side cam: identical foreground/background composition across the entire 75s clip.
- The report description has been rewritten to honestly state what the video shows ("ball visible at launch from side angle; lateral curve direction difference is subtle at 50m camera distance; curve magnitude proven by overlay PNG and runtime log") — no over-claim of a visible banana curve.
- The misnamed `still_sidecam_*.jpg` files are renamed to `still_chasecam_*.jpg`. The new `iter5_sidecam_shotA_inflight.jpg` IS legitimately a side-cam frame (camera now locked).
- The 105MB raw + iter-2 66MB superseded videos are deleted.
- Sample counts corrected.

The behavioral gate was already satisfied by the overlay still + runtime wiring log in iter-4; the iter-5 fix removed the one remaining defect (misleading citation) without introducing any new code path into production. Production code is unchanged from iter-1; iter-5 mutations are confined to the bot scenario (`Scenarios.cs`) + docs/media in the task folder.

## Single most important residual risk for the red-team

**The video alone is not curve evidence — the curve proof lives entirely in the overlay PNG + runtime wiring log + trajectory JSON.** If the red-team opens the video first and expects to see a visible banana, they will not — and that is by design per the honest report description. The red-team should:
1. Open `screenshots/curve_overlay_real_hole.png` FIRST as the canonical curve proof (17.2m DRAW-FADE separation, three distinguishable arcs).
2. Open `runtime_wiring_log.txt` to confirm production `CommitFlick` produced the three different `fadeDrawInput` values.
3. Open `trajectory_points.json` to confirm real Hole 6 tee coords (80.210, -24.544 ≠ editor sim origin).
4. THEN open the video as production-flow evidence (locked side-cam, real ShellScene boot, 3 captioned shots).

If the red-team treats the video as the canonical curve evidence, they will FAIL it — and that is exactly what this report should NOT cause. The implementer's description handles this correctly, but the citation order matters.

## Minor close-out drift (not ship-stopping; flag for the DONE close-out commit)

- The implementer kept THREE captioned videos (iter3 + iter4 + iter5, 273MB total) when the iter-4 reviewer's explicit instruction was "at most ONE prior iteration's captioned video as history." The iter4 captioned mp4 should be deleted on Cesar's close-out commit (keep iter3 for history + iter5 as canonical). This is a 80MB cleanup; not worth another FAIL cycle.
- The iter-5 video itself is 128MB (well above the 50MB Telegram cap; the daily_report two-pass re-encode will handle it). No action needed.

## Lessons captured (if approved later)

- The `ChaseCamera.Downrange` mode + per-frame `SetMode` re-assertion is the correct pattern when a bot scenario needs to take over the camera for a specific window over a `LoopCameraDirector`-managed shot. The single-frame transform-write pattern (iter-4) does NOT work because `LateUpdate` overrides + state-machine `SetMode(Chase)` on `Flying`/`Rolling` will both revert it. This pattern should be added to the bot-scenario reference doc when one exists.
- When a video is supporting evidence but not the canonical curve proof, the report's `Canonical video:` description must state what the video does AND does not show. "Locked side-cam over real production flow, curve magnitude proven by overlay PNG" is the right shape.

---

# RED-TEAM REVIEW — `fade_draw_core_wiring` (Order 356)

> Written by `golfin-redteam-reviewer` (adversarial gate, the ONLY agent that may write `ARCHITECT_REVIEW_PASS`). Time: 2026-06-17 11:12 CEST. I did not trust the reviewer's numbers — I re-ran them all myself, and added cross-checks the reviewer did not perform.

## Verdict: `ARCHITECT_REVIEW_PASS`

I actively tried three ways to break this and could not. The behavioral-gate claim holds under independent re-derivation. Two non-blocking items flagged for Cesar at the bottom.

## Evidence I re-generated / re-computed (did not re-use)

**1. DRAW–FADE separation — recomputed from `trajectory_points.json` endpoints (NOT taken on faith):**
- DRAW final lateral = **+7.907 m**, FADE = **−9.328 m**, STRAIGHT = **+7.478 m**.
- Separation = |+7.907 − (−9.328)| = **17.235 m** → matches the claimed/overlay 17.2 m. PASS.
- Signs are OPPOSITE (DRAW +, FADE −): draw and fade bend opposite ways. PASS.

**2. Curve-vs-aim-shift bow analysis — the test the reviewer never ran.** I fit each path's own tee→landing straight chord and measured max lateral deviation (bow) from it:
- DRAW bow = **−2.085 m** (curved flight)
- FADE bow = **+2.309 m** (curved flight, opposite direction)
- STRAIGHT bow = **+0.059 m** (a straight line — aim shift, NO curve)
- This is the decisive proof of the spec's core claim: fade/draw curves, straight-mode handle only nudges aim. STRAIGHT's large final lateral (+7.48 m) is the aim tilt, not a bend.

**3. Independent physical cross-check of the aim nudge.** Predicted straight-mode lateral landing from a 3° aim nudge over 150 m downrange = tan(0.0524)×150 = **7.87 m**; observed STRAIGHT final lateral = **+7.48 m** (agree within ~5%, the rest is drag). Confirms STRAIGHT's offset IS the configured `AimNudgeRangeRad`, not a curve.

**4. Runtime-log internal reconciliation — the second test the reviewer never ran.** From `runtime_wiring_log.txt`: FadeDraw A/B `aimYaw=3.1439, lockedAim=3.0391` → implied degradYaw = **0.1048 rad (6.00°)**. Straight C `aimYaw=3.0915` with finetune=−1, AimNudge=0.0524 → implied degradYaw = 3.0915−3.0391+0.0524 = **0.1048 rad (6.00°)**. Both shots yield the EXACT same degradation yaw — the per-branch `CommitFlick` aim formula reconciles perfectly across all three shots. No hidden inconsistency.

**5. Config internal consistency (recomputed):** `SpinMaxTiltRad/FadeDrawMaxTiltRad` = 0.075/0.3 = **exactly 0.25** (D3 quarter-trim); `AimNudgeRangeRad` = 0.0524 = **3.002°**; `FadeDrawMaxTiltRad` = 0.3 = 17.19°. CSV ↔ `ControlsConfig.Default` parity verified by diff.

**6. Provenance — overlay is real-runtime, NOT a re-plot of the iter-1 editor sim.** JSON tee = (80.21, −24.544) = real Hole 6 world coords ≠ (0,0,0). The iter-1 editor-sim `trajectory_trace.txt` shows ±29.56 m / ±7.8 m deviations (flat-ground); the runtime JSON shows +7.9/−9.3 m — genuinely different datasets, so the overlay is the real Hole 6 run, not a re-plot. Editor.log confirms the bot loaded Hole 6 via `BeginGameplayLoad(6)` through the production HoleSelection flow and the run completed ("=== Scenario complete ===", no exception).

## Prior-rejection replay (each defect: GONE / PRESENT)

| Prior FAIL | Verdict | Proof I generated |
|---|---|---|
| iter-1 reviewer: no real-pipeline capture (sim only) | **GONE** | Runtime log captured at production `CommitFlick` over real Hole 6 (tee 80.21,−24.544); overlay plotted from same run's `LastTrajectory.samples`; numbers differ from editor sim (29.56 vs 7.9), proving it's the runtime path. |
| iter-2/3 self-review: curve not visible (chase/overhead) | **GONE** | Top-down overlay shows 3 distinguishable arcs; my bow analysis quantifies them (DRAW 2.085 m / FADE 2.309 m bow vs STRAIGHT 0.059 m). I recomputed the 17.2 m, did not accept on faith. |
| iter-4 reviewer: misleading `Canonical video:` over-claiming a visible curve | **GONE** | iter-5 report (line 31, 151) honestly states curve is "subtle at 50m camera distance; curve magnitude proven by overlay PNG and runtime log" — NO over-claim of a visible banana. Camera fix is the established `ChaseCamera.Downrange` API + per-frame `SetMode` re-assertion (verified in source 1707/1743/1749). I confirmed the iter-5 description does not re-introduce the iter-4 defect. |

## Three break-attempts (all FAILED — could not break it)

- **Visual:** Opened the overlay myself. Yellow FADE bows left, cyan DRAW bows right (visibly curved near the tee), white STRAIGHT runs as a clean straight chord. No mislabeled artifact; arcs match my recomputed endpoints to the metre. Could not find a wrong pixel/seam. The video is honestly demoted to production-flow evidence; opening it first would NOT mislead because the report says so explicitly.
- **Geometric:** Tried to show STRAIGHT is secretly curving (it lands near DRAW at +7.48 vs +7.91). Bow analysis killed it: STRAIGHT bow = 0.059 m = a straight line; its offset = the 3° aim nudge (predicted 7.87 m, observed 7.48 m). No metric sits near a failure threshold — the separation (17.2 m) and ratio (3.79×) are comfortably above any minimum.
- **Spec-intent:** Tried to find a wiring path that violates D3/D6 or leaks diagnostics into production. Putts get `fadeDrawInput=0` (guarded by `!IsPutt`); `spin.y`/SpinContext/SpinPanelWidget/cone-width/ShotModeContext all show ZERO git diff; the three `#if UNITY_EDITOR` seams (`FadeDrawRuntimeWiringLog`, `WidthMultiplierForBot`, side-cam mode swap) are default-off and Editor.log confirms teardown ("FadeDrawRuntimeWiringLog disabled", "Restored: FadeDrawActive=false … finetune=0"). Production code unchanged from iter-1; iter-2..5 added only bot-scenario `Scenarios.cs` + tests + Python + docs/media. No scene/prefab/asset diff. Could not find leakage.

## Tests
17/17 EditMode PASS per `test_results.txt` (9 `FadeDrawTiltTests` + 8 `FadeDrawWiringTests` — note: the report's "9+7+1" phrasing is loose; the file lists 9 tilt + 8 wiring = 17). I read the assertions: they genuinely test formula signs (`FadeDrawNegative_ProducesOppositeAxisToPositive`), additive combination, 0.25 quarter-trim, aim-nudge L/R mapping, FadeDraw aim-lock, arm re-center, putt zero-spin, spin.y regression, determinism, and legacy no-op. Pre-existing `ShotInputBuilderTests` show ZERO diff (still pass with default-0 new params). I could not re-run via `tests-run` MCP in this read-only Bash session, but verified the recorded results against the assertion bodies and the Editor.log clean-compile state (no `error CS`).

## Phase E deviation — decision for Cesar (PASS-with-note, NOT a blocker)
On arm, the code locks `FadeDrawLockedAimRad = CameraHeadingRadians` rather than the spec's literal `AimYaw + Finetune*AimNudgeRange`. The in-code comment (ShotConeView `OnShotModeChanged`) justifies it: the handle re-centers to 0 on arm, so effective aim = camera heading + 0. Mathematically identical for the normal Idle-toggle gesture (finetune=0). Diverges ONLY if the player has already deflected the handle and toggles mid-deflection — then the in-progress aim nudge is dropped. This was explicitly flagged as a non-blocking open question across iter-1 and carried by the reviewer; I agree it is a judgment call for Cesar, not a defect. Surfacing it.

## Minor close-out drift for Cesar's DONE commit (non-blocking)
- THREE captioned videos kept (iter3 65 MB + iter4 80 MB + iter5 128 MB canonical); the iter-4 reviewer asked for "at most one prior". Delete the iter4 mp4 on close-out (keep iter3 history + iter5 canonical). ~80 MB cleanup, not worth a FAIL cycle.
- Bot completion banner in Editor.log still reads "iter-4" (cosmetic stale string in `Scenarios.cs` log text); the run that wrote `trajectory_points.json` is the iter-5 run (~10:46–10:47 CEST). Harmless.

## STATUS update
Setting STATUS to `ARCHITECT_REVIEW_PASS` — hands to Cesar for final approval. The behavioral gate is satisfied: end-to-end production wiring proven (runtime log), curve magnitude proven (overlay + recomputed 17.2 m separation + bow analysis), straight-mode-no-curve proven (0.059 m bow + aim-nudge magnitude match), scope clean (production code = iter-1, zero scene/spin/putt/cone diff), 17/17 tests pass.
