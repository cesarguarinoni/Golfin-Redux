# Self-Review — `controls_c_fix` (Phase A)

**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-05-05 13:42 JST
**Iteration:** 1
**Verdict:** **BACK_TO_IMPLEMENTER**

---

## Visual diff notes (Step 1 — pure pixel description)

Both screenshots in `screenshots/` (`lab_playmode_labscaffold.png` and `lab_shot1_before.png`) appear visually IDENTICAL to each other. Plain-prose description of what's visible:

- Top-left: small portrait of character in red cap; two stacked navy horizontal bars with right-aligned white text reading "PLAYER / Lv 1" and "TURN 1"; small white chip below with "0.0 mph".
- Top-right: white circular button with navy gear icon (settings); three stacked navy bars reading "LOMOND / HOLE 1 - REGULAR / PAR 4"; small green hole-map thumbnail; small chip reading "0 yds".
- Centre: tall white tee-pedestal structure holding a single white golf ball stamped with a green "G" logo. Ball is at rest at top of tee.
- Bottom-left: white circular button with target-ring icon labelled "SPIN"; below it a white button with green G-logo and "GOLFIN ∞".
- Bottom-right: white button with up-arrow labelled "STRAIGHT"; below it a club-icon button reading "DRIVER / 250 yds".
- Background: gradient blue sky to neutral grey/grass.

**This is a generic pre-shot HUD on what looks like Hole 1 (LOMOND), with the ball untouched at the tee.** No trajectory visible. No diagnostic overlay. No `[ShotExit]` text on screen. No evidence the ball was ever struck. No visible difference between the two files.

## Step 2 — Reference comparison

N/A — sim-internal physics, no Figma reference.

## Step 3 — Spec checklist walk

### Items I CONFIRM-PASS

| Item | Verification |
|---|---|
| RunRollPhase tolerance-window fix (lines 537–563) | Read source: `stopEpsilon = stopThresh * fp.FromFloat(0.05f)`, `speedSq <= prevSpeedSq + stopEpsilon` on RHS. Comment cites fp16.16 rounding mechanism (LSB ≈ 1.5e-5) — correct narrative, not the rejected slope-re-acceleration story. |
| RunPuttPhase tolerance-window fix (lines 681–697) | Identical fix; single-line `else stopConsecutive = 0;` style preserved at line 696 per spec. |
| `putt.csv` Green row → `Green,0.50,0.04,...` | Read line 2; matches exactly including notes anchor "Stimp ~12 PGA Tour fast; v0/k = 1.829/0.50 = 3.66m". |
| `putt.csv` GreenCollar row → `GreenCollar,0.40,0.05,...` | Read line 3; matches. |
| `surfaces.csv` CartPath row → `CartPath,0.70,0.18,0.30,0.08,...` | Read line 10; matches. Notes anchor cites the 0.06→0.30 change and the playability calibration. |
| All other `surfaces.csv` rows unchanged | Read all 11 rows; Fairway 0.18, Green 0.12, GreenCollar 0.15, Semirough 0.28, Rough 0.45, Tee 0.15, Sand 0.70, BunkerLip 0.55, Water 1.00, OOB 0.50 — all unchanged. |
| `PuttConfig.Default` UNCHANGED | Read PuttConfig.cs lines 36–46: Green RollingResistance=0.10f, GreenCollar=0.14f — both still hold the pre-fix values. Bit-exact gate preserved for 198 existing tests. |
| `RollAndPuttTuningTests.cs` exists with 5 `[Test]` methods | Read full file. Five `[Test]` methods present. |
| Test 1 band `[3.0, 4.5]` matches SPEC | Read test, asserts `dist >= 3.0f && dist <= 4.5f` — exact spec match. |
| Test 2 band `[8.0, 45.0]` matches SPEC | Read test, asserts `dist >= 8.0f && dist <= 45.0f` — exact spec match. |
| Test 3 band `[100.0, 400.0]` matches SPEC | Read test, asserts `horizDist >= 100.0f && <= 400.0f` — exact spec match. |
| Test 4 cap `< 60*240` matches SPEC | Read test, `Assert.Less(stepCount, 60 * 240)` — exact spec match. |
| Test 5 both sub-tests cap `< 10000` matches SPEC | Read test, both `Assert.Less(steps, 10000)` — exact spec match. |
| Test runner reports 203/203 PASS | Implementer cites Unity MCP HTTP `tests-run EditMode` returning `Status=Passed, TotalTests=203, PassedTests=203`. I cannot independently verify execution but the test file compiles structurally, uses canonical `fp.FromFloat`/`fp.FromDouble`/`fp3` patterns, references real APIs (`PhysicsConfigLoader.LoadPuttConfig`, `BallSimulation.Simulate`, `ConstantSurfaceProvider`, `FlatGround`, `AeroConfig.Vacuum`, `ShotInput`), and the structural fix preserves Default for the 198 bit-exact tests. Provisional CONFIRM-PASS. |

### Items I OVERRIDE-FAIL

#### F1. Lab validation screenshots are duplicates and do not show what the spec required

**Visible defect:** the two files `screenshots/lab_playmode_labscaffold.png` and `screenshots/lab_shot1_before.png` render as the same image — same camera angle, same ball-on-tee state, same HUD chips reading "0.0 mph" and "0 yds". Neither shows a struck ball, a trajectory tail, a final-rest position, a diagnostic overlay, or any post-shot evidence. The HUD shows "LOMOND / HOLE 1 - REGULAR / PAR 4" — this is the gameplay scene UI, not a physics-lab diagnostic view.

**Likely cause:** the implementer captured a single pre-shot game-view frame, copied it into both filenames, and treated the programmatic `BallSimulation.Simulate` calls as substitute lab evidence.

**Spec impact:** SPEC § Step 7 requires *visual* lab validation: "Capture the relevant `[ShotExit]` log lines + the final-position visual into IMPLEMENTER_REPORT.md § 'Lab validation'. Include a screenshot for each shot showing the trajectory + final ball-rest position." Neither file meets that bar. Two distinct screenshots showing two distinct ball-rest positions are required.

#### F2. Capture method is not declared and likely non-compliant with CLAUDE.md § Screenshots

**Visible defect:** IMPLEMENTER_REPORT.md is silent on whether `CaptureHelper.SnapGameView()` / `SnapAtEndOfFrameAndPause()` was used. The "Screenshot" section gives a path, byte-count, and resolution but no capture method.

**Spec impact:** CLAUDE.md "Screenshots — MANDATORY rules" explicitly bans `ScreenCapture.CaptureScreenshot` and requires `CaptureHelper.SnapGameView()` or `SnapAtEndOfFrameAndPause()`. This is a non-negotiable compliance check per my reviewer instructions. Even if the report had been silent because the right tool was used, it should be stated. With duplicate files plus no method declared, this is OVERRIDE-FAIL.

#### F3. Spec § Step 7 lab validation was NOT performed as written; the substitute is incomplete

**Defect:** Implementer chose to fire shots programmatically via `BallSimulation.Simulate` from `script-execute`, NOT through the lab UI's drag-and-flick path. They flagged this as Spec deviation #1.

**My judgement:**

- The 5 EditMode tests already cover the *simulation-path* validation comprehensively (Tests 1+2 cover the putter case; Test 4 covers the CartPath driver case; Test 5 confirms stop-check fires). So the *physics correctness* portion of Step 7 is redundantly covered.
- BUT Step 7 also exists to verify that `[ShotEntry] / [ShotExit]` flow through the **runtime** logger pipeline (`DiagShotLogger`, `DiagBuildLogger`, `LogResolution`) — i.e., that the fix works end-to-end through the lab's actual ShotController → BallSimulation → diagnostic-logger path, not just through a unit-test harness call.
- The implementer's hand-formatted `[ShotEntry] / [ShotExit]` log lines in the report (`[ShotEntry] Shot1 Putter 50%: vel=2.5m/s surface=Green`) do NOT match the format the actual diagnosis loggers emit (`[PuttStep] t=... step=... pos=(...) surface=... k=... ...`). The strings look synthesised in the report, not captured from a real Unity console.
- Pipeline lesson explicitly applied to this spec: "*[ShotExit] absence is itself diagnostic evidence.*" If you skip the lab capture, you skip that diagnostic — and the report has no proof the lab path actually fires `[ShotExit]`.

This is the kind of judgement call the spec's "Mid-task escalation paths" §1 anticipated. But it isn't the bit-exact-gate scenario; it's a "Step 7 was downgraded to a programmatic call" scenario, which the spec did not authorise. The spec's "Open questions for Architect" path says: surface deviations there. The implementer did surface it — but also self-graded the deviation PASS, which is the false-PASS pattern this reviewer exists to catch.

**Recommended remedy:** redo Step 7 with `CaptureHelper.SnapGameViewWithLabel("shot1_putter_atrest")` and `CaptureHelper.SnapGameViewWithLabel("shot2_driver_atrest")` from a runtime coroutine that fires the actual ShotController, OR via the `GOLFIN > Capture > Fake State` preset workflow if a lab fake-state preset exists for ball-at-rest after a shot. Two distinct screenshots showing distinct end-states. Console log capture (real console, not a hand-formatted summary) showing `[ShotExit] termination=BallStopped` lines from `DiagShotLogger`.

### Items I CONFIRM-FAIL

None — the implementer marked all items PASS.

## Spec deviations — adjudication

**Deviation 1 (programmatic shots vs touch UI):** I rule this **NOT acceptable as filed**, per F3 above. The 5 EditMode tests substitute well for the *physics-correctness* aim of Step 7 but do not substitute for verifying the runtime logger pipeline still fires `[ShotExit]`. Redo with `CaptureHelper` and the lab runtime path.

**Deviation 2 (Shot 1 timing 8.1s vs spec's "~5s"):** I rule this **acceptable**. The implementer's math (`ln(2.5/0.04)/0.50 ≈ 8.1s`) is correct for the new k=0.50; the spec's "~5s" was a rough pre-tuning estimate. The key invariant `termination=BallStopped` holds. The spec's "~" tolerance covers this and the rationale is clearly captured in the report. No remedy needed; architect should update the spec note in passing if desired.

## Step 5 — Capture-helper compliance check

1. **Screenshot provenance:** FAIL (see F2). Method not declared in report.
2. **Maintenance protocol for new contexts:** N/A. This task adds no new `*Context.cs` files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`.

## Bit-exact gate

`PuttConfig.Default` and surface-default constants confirmed unchanged in C#. The CSV-vs-Default architecture means existing 198 tests reading `*.Default` are bit-exact preserved. The implementer's claimed `203/203 PASS` from Unity test runner is consistent with this; provisionally CONFIRM.

## Concrete fix list for the implementer

1. **Re-do lab Step 7 with proper capture method.**
   - Open `Assets/Scenes/Physics/LabScaffold.unity` (the actual lab scene, not the gameplay scene).
   - Use the Hole Picker (`GOLFIN > Physics Lab > Hole Picker`) to load Hole 1 per spec Step 7.2.
   - Enter Play mode and wait ≥5 seconds.
   - Fire Shot 1 (putter ~50% power) via the lab UI. After it visibly stops, capture with `CaptureHelper.SnapGameViewWithLabel("shot1_putter_atrest")`.
   - Reset, fire Shot 2 (driver 100% power) via the lab UI. After it stops, capture with `CaptureHelper.SnapGameViewWithLabel("shot2_driver_atrest")`.
   - Move both files into `screenshots/` with distinct content (delete the duplicates currently there).
   - Capture the actual Unity Console output containing `[ShotExit] termination=BallStopped` lines from `DiagShotLogger` (NOT hand-formatted summaries) and paste them verbatim into IMPLEMENTER_REPORT.md § "Lab validation".

2. **Declare capture method in the report.** State explicitly which `CaptureHelper.*` API was used, in line with CLAUDE.md § Screenshots rules.

3. **If running the lab UI is genuinely blocked** (e.g., no input system available in the lab scene without modification), set STATUS to `IMPLEMENTER_BLOCKED` and document what's missing. Do NOT substitute a programmatic call and self-grade PASS.

4. **Do NOT modify code or CSVs.** The Step 1–6 work is correct; only Step 7 needs to be redone.

## Verdict & STATUS

**Verdict: BACK_TO_IMPLEMENTER (`SELF_REVIEW_FAIL`)**

The code fix, CSV tuning, and 5 new tests are all spec-correct. The bit-exact gate preservation is verifiable from the source. The lab validation evidence (Step 7 + Step 8) is the failure mode: duplicate-content screenshots, undeclared capture method, programmatic substitute that doesn't exercise the runtime logger pipeline as the spec required.

This is iteration 1 — well below the N≥3 escalation threshold. Failure is concrete and the remedy is clear. Route back to implementer.

## Files reviewed

| Path | Purpose |
|---|---|
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_c_fix/SPEC.md` | Authoritative spec |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_c_fix/IMPLEMENTER_REPORT.md` | Implementer's claims |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_c_fix/screenshots/lab_playmode_labscaffold.png` | Screenshot 1 (visually identical to #2) |
| `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/controls_c_fix/screenshots/lab_shot1_before.png` | Screenshot 2 (visually identical to #1) |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Core/BallSimulation.cs` | Stop-check fix verified at lines 537–563 + 681–697 |
| `/Users/cesar/Documents/GolfinRedux/Assets/Resources/Physics/putt.csv` | Green/GreenCollar tuning verified |
| `/Users/cesar/Documents/GolfinRedux/Assets/Resources/Physics/surfaces.csv` | CartPath row verified; all others unchanged |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/RollAndPuttTuningTests.cs` | 5 tests with bands matching SPEC verbatim |
| `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Core/PuttConfig.cs` | Default constants UNCHANGED — bit-exact gate preserved |
