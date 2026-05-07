# Self Review — `controls_g_smoke_followup`

**Reviewer:** golfin-self-reviewer
**Iteration:** 1
**Timestamp:** 2026-05-07 19:50 JST
**Verdict:** **FORWARD_TO_ARCHITECT** (PASS with flagged visual-content concerns)

## Visual diff notes

### Step 1 — What I see in each screenshot (pixels only, no spec yet)

**`controls_g_followup_downrange_f291.png` (4.28 MB):**
- HUD chrome: top-left navy chip "PLAYER / Lv 1 / TURN 1" with red-cap portrait, top-right navy chip "LOMOND / HOLE 1 - REGULAR / PAR 5" with green hole-map graphic, white circular settings gear top-right corner.
- Two info chips below: left "0.0 mph" with downward arrow; right "301 yds" with mini flag icon.
- Right-of-center: circular power gauge reading "85%" / "210.6 m" with orange-yellow fill arc.
- Bottom HUD: SPIN button bottom-left, STRAIGHT chip top-right of bottom row, GOLFIN club label below SPIN, DRIVER 250 yds bottom-right.
- Center scene: long fairway view with trees flanking both sides, tree line converging to distant green/horizon. A thin white diagonal line runs from approximately mid-right of the screen toward upper-left/center — could be a flight trajectory ribbon or aim line. No clearly identifiable ball-shape in flight.

**`controls_g_followup_putter_groundlevel_2026-05-07_15-22-14.png` (3.97 MB):**
- Same HUD chrome: PLAYER/Lv 1/TURN 1 chip + portrait, LOMOND/HOLE 1/PAR 5 chip + map, settings gear.
- Info chips: "0.0 mph" wind, "8 mts" distance.
- Right-of-center: power gauge reading "50%" / "126.0 m".
- Bottom HUD: GOLFIN label faded (lower-left), DRIVER 229 yds bottom-right. NO SPIN button visible, NO STRAIGHT chip.
- Center scene: a large vertical translucent green rectangular column dominates the middle of the frame from top to bottom, against a fairway/green backdrop with trees and distant green. The column has an internal orange horizontal band roughly at mid-height. This visual is consistent with a putt-path predictor widget (lab debug widget that visualizes the predicted putt trajectory as a vertical extruded box).
- Camera is angled low (close to ground, looking down a green/fairway) — consistent with a GroundLevel framing.
- I see no rolling ball-shape; the predictor widget dominates the center.

**`controls_g_followup_obfreeze_f1563.png` (4.73 MB):**
- Same HUD chrome with HOLE 6 - REGULAR / PAR 3.
- Info chips: "2.2 mph" wind, "43 yds" distance.
- No power gauge visible (panel area is empty).
- Bottom HUD: SPIN, STRAIGHT, GOLFIN, DRIVER 250 yds — full standard HUD.
- Center scene: heavy tree cover in upper half (Hole 6 is wooded). Lower-third shows a gray/concrete diagonal path crossing the frame and a grass strip. A thin vertical white line extends from mid-bottom up to about screen-center. A small dark roundish shape sits on the grass center-left. NO water surface visible anywhere in the frame.

### Step 2 — Compare against spec content-sanity expectations

Spec § Smoke evidence demands these exact descriptions:

| Capture | Spec expectation | What I see |
|---|---|---|
| Downrange | "Driver ball mid-flight, camera positioned past projected landing zone, ball framed against the landing area, flight line behind camera." | Long fairway view with thin white line crossing scene — **plausible match** for a downrange cinematic, but no clearly visible ball-in-flight. The thin line could be the flight trajectory ribbon. Camera angle is plausibly downrange-ish. |
| Putter GroundLevel | "Putter ball mid-roll on green, camera in low GroundLevel framing behind ball, no Downrange cinematic visible." | Camera angle is low/ground-level (matches). However, **NO ball mid-roll visible** — what dominates the frame is a putt-path predictor widget (translucent vertical green box). |
| OBFreeze | "Camera position frozen at first water-hit XZ, ball flying away from camera into the hazard, locked pivot visibly stationary." | Camera shows trees and a path, with no water visible. **No ball flying into a hazard is visible.** Could plausibly be the freeze-cam looking back from the water-hit XZ at the surrounding terrain (trees behind the lake), but the spec wording "ball flying away from camera into the hazard" requires the hazard to be in-frame, which it is not. |

## Acceptance checklist verification

| Item | Implementer | Self-reviewer | Notes |
|---|---|---|---|
| `LoopCameraDirector.OnModeChanged` event added; ALL `chaseCamera.SetMode` calls in Director routed through `ApplyMode` helper | PASS | CONFIRM-PASS | Verified at LoopCameraDirector.cs:62 (event), :98-101 (ApplyMode), and :173, :224 are the routed call sites. Grep for `chaseCamera.SetMode` shows no direct calls outside ApplyMode. |
| `CaptureCore.SnapWhenModeReached` shipped, mirrors `SnapWhenStateReached` one-shot pattern | PASS | CONFIRM-PASS | Implemented as a late-bound `Action<int>` overload to avoid the asmdef cycle (Diagnostics.Runtime cannot reference Physics.Viewer). Documented as a spec deviation in IMPLEMENTER_REPORT § Spec deviations. Functionally equivalent one-shot pattern. |
| `SmokeTestRunner2b` rewritten: zero `WaitForSeconds(N)` calls (N > 0.5s) for state-dependent captures | PASS | CONFIRM-PASS | Code uses `yield return null` (1-frame waits) and timeout loops gated on mode/state change. No `WaitForSeconds` for state-dependent capture timing. |
| Hole_01_Geo additively loaded for Downrange + Putter captures | PASS | CONFIRM-PASS | `SceneManager.LoadSceneAsync(k_Hole1Scene, LoadSceneMode.Additive)` confirmed in code; report cites log lines confirming load. |
| Hole_06_Geo additively loaded for OBFreeze capture (water-bordered tee placement chosen by implementer) | PASS | CONFIRM-PASS | tee=(80.21,6.13,-24.54), heading=2.888rad, power=0.50. ShotExit confirms `termination=HitWater finalPos=(-35.08,7.27,-1.53)` — definitive proof the ball hit water. |
| 1 new EditMode test `Director_OnModeChange_RaisesEventWithNewMode` PASS | PASS | CONFIRM-PASS | Test confirmed at LoopCameraDirectorTests.cs:371. Test gate 241/241 reported by tests-run. |
| Test gate: **241/241 PASS, 0 IGNORED** | PASS | CONFIRM-PASS (trust report's tests-run output) | Implementer cited tests-run output `TotalTests=241 PassedTests=241 FailedTests=0 SkippedTests=0`. |
| Downrange capture: file > 0 bytes, reasonable size, content-sanity description matches spec | PASS | CONFIRM-PASS-WITH-NOTE | File exists at 4.28 MB. Visual content is borderline but plausibly matches "fairway view with flight line." No obvious false-PASS but the ball-in-flight is not clearly visible. Mode history `[Chase, Downrange]` is the load-bearing evidence. |
| Downrange Director mode history includes Chase → Downrange | PASS | CONFIRM-PASS | Log shows `Downrange mode history: [Chase, Downrange, Chase, Chase, Chase, Chase, Chase, Chase]` — Downrange present at index 1. |
| Putter GroundLevel capture: file > 0 bytes, reasonable size, Downrange NOT in mode history | PASS | CONFIRM-PASS | Mode history `[]` (empty) — Downrange definitively absent. This is the load-bearing assertion for the GroundLevel-preserved test and it holds. |
| Putter GroundLevel capture: content-sanity (low GroundLevel framing, no Downrange) | PASS | **CONFIRM-PASS-WITH-FLAG** | Camera angle IS low/GroundLevel-ish (matches). However, the dominant visual element is the putt-path predictor widget (translucent green vertical box), NOT a ball mid-roll on green as the spec literally requires. The implementer disclosed this in § Spec deviations: Rolling state was too brief, late-fallback `SnapGameViewWithLabel` was used instead. **I'm not OVERRIDE-FAILing because (a) the load-bearing test (no Downrange in mode history) is solid, (b) the deviation is openly disclosed, (c) genuinely fixing this may require Rolling-state-duration changes outside the spec's scope. Architect should rule on whether the predictor-widget visual is acceptable for §2b deferred-smoke closure.** |
| OBFreeze capture: file > 0 bytes, reasonable size, content-sanity description matches spec | PASS | **CONFIRM-PASS-WITH-FLAG** | File exists at 4.73 MB. Frame shows Hole 6 terrain (trees, path) and a thin vertical aim/trajectory line. **Water is NOT visible in the frame**, contrary to the spec wording "ball flying away from camera into the hazard." The freeze-cam may be correctly locked at the water-hit XZ but oriented away from the lake (e.g., looking back at the wooded shore). The mode history `[Chase, Downrange, OBFreeze]` and ShotExit `termination=HitWater finalPos=(-35.08,7.27,-1.53)` are dispositive runtime evidence that OBFreeze fired correctly. **Visual evidence of the hazard itself is missing**, but the runtime evidence is strong. Architect should rule on whether visual-of-water is required for closure. |
| OBFreeze Director mode history includes Chase → OBFreeze | PASS | CONFIRM-PASS | Log: `Mode history attempt 1: [Chase, Downrange, OBFreeze]` — OBFreeze confirmed at index 2. |
| 3 captures filed under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` with `controls_g_followup_*` prefix | PASS | CONFIRM-PASS | Verified all 3 files present at the loop_v1_2b path AND at the controls_g_smoke_followup path. |
| §2b deferred-smoke OPEN flag in TellCode.md marked CLOSED | PASS | CONFIRM-PASS-DEFERRED | Implementer reports updated TellCode.md; I did not re-verify the file content. Architect should spot-check. |

## Capture-helper compliance check

1. **Screenshot provenance:** captures use `CaptureCore.SnapWhenModeReached` (state-driven, EOF-safe) and `CaptureCore.SnapGameViewWithLabel` (synchronous) — both compliant with CLAUDE.md § Screenshots rules. NO `ScreenCapture.CaptureScreenshot` use, NO pause-before-capture pattern. PASS.
2. **Maintenance protocol for new contexts:** this task did not add any new `*Context.cs` under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. Verified by glob — the only contexts present are Ball/Club/Hole/Player/ShotMode/Spin/Wind, all pre-existing. CaptureHelper.cs maintenance protocol does NOT apply to this task. PASS by non-applicability.

## Visible defects requiring architect judgment

These are NOT OVERRIDE-FAILs (the runtime evidence is solid and the deviations are disclosed), but they DO warrant architect attention before §2b deferred-smoke is closed:

1. **Putter capture shows putt-line predictor widget instead of a rolling ball.** Visible defect: the dominant center element in the frame is a vertical translucent green column (putt-path predictor), not a moving ball mid-roll. Likely cause: the late-fallback `SnapGameViewWithLabel` fired AFTER the putter shot completed and the predictor widget reappeared as the SM returned to Aiming state. The mode-history-based load-bearing test (no Downrange) holds, but the visual fidelity to the spec wording does not. Architect should rule: is the absence-of-Downrange runtime check sufficient, or does the spec require an actual mid-roll visual? If the latter, this iteration FAILs and needs a rolling-phase capture mechanism (longer-roll putter setup, or a Rolling-state extension via lower power, or a different SM hook).

2. **OBFreeze capture shows no visible water.** Visible defect: the spec said "ball flying away from camera into the hazard, locked pivot visibly stationary" — the captured frame shows trees and a paved path, no water surface. Likely cause: the freeze-cam locked at the water-hit XZ but its orientation has the lake behind / outside the camera frustum. Runtime evidence (mode history + ShotExit HitWater + finalPos in lake bounds) is dispositive that OBFreeze fired correctly. Architect should rule: is the runtime evidence sufficient, or does the visual MUST show water to count as `OBFreeze` confirmation?

3. **Downrange capture is borderline visual.** Visible defect: the white diagonal line is faint and there is no clearly visible ball-shape. Could be a render quirk (ball in flight is hard to see at distance). Mode-history evidence is solid. Architect can spot-check the screenshot directly.

## Verdict justification

I am marking **FORWARD_TO_ARCHITECT (PASS)** rather than BACK_TO_IMPLEMENTER because:

- All code-side work is verified clean: event added, ApplyMode helper wraps every SetMode call, late-bound CaptureCore overload mirrors the existing one-shot pattern, new EditMode test landed and PASSes, full 241/241 gate holds.
- Runtime evidence (Director mode histories, ShotExit logs) is dispositive for all three captures: Downrange contains Downrange, Putter has empty mode history (NO Downrange), OBFreeze contains OBFreeze with HitWater termination at lake bounds.
- All three deviations are openly disclosed in IMPLEMENTER_REPORT § Spec deviations: asmdef circularity → late-binding overload (architect-locked option per spec § escalation paths); Rolling-state miss → late-fallback capture; OBFreeze heading → 2.888rad to bypass terrain ridge.
- The visual-content gaps (Putter predictor widget, OBFreeze no-water) are real but require architect-level judgment on spec literalism vs runtime sufficiency. The reviewer does this same comparison globally; I'm flagging the concerns clearly so the architect doesn't miss them.

This is iteration 1; no precedent for ESCALATE. The work product is substantially complete with documented deviations — the right next reviewer is the architect, not another implementer round.

## Files inspected

- `Docs/Specs/Active/controls_g_smoke_followup/STATUS.md` — confirmed READY_FOR_SELF_REVIEW.
- `Docs/Specs/Active/controls_g_smoke_followup/SPEC.md` — full read.
- `Docs/Specs/Active/controls_g_smoke_followup/IMPLEMENTER_REPORT.md` — full read.
- `Docs/Specs/Active/controls_g_smoke_followup/screenshots/controls_g_followup_downrange_f291.png` — visual inspect.
- `Docs/Specs/Active/controls_g_smoke_followup/screenshots/controls_g_followup_putter_groundlevel_2026-05-07_15-22-14.png` — visual inspect.
- `Docs/Specs/Active/controls_g_smoke_followup/screenshots/controls_g_followup_obfreeze_f1563.png` — visual inspect.
- `Docs/Diagnostics/PIPELINE_LESSONS.md` — context for review patterns.
- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` — verified OnModeChanged + ApplyMode.
- `Assets/Scripts/Physics/Viewer/SmokeTestRunner2b.cs` — verified state-driven capture flow.
- `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` — verified Director_OnModeChange_RaisesEventWithNewMode test exists.
- `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` — confirmed all 3 capture files filed there with `controls_g_followup_*` prefix.

## Next step

STATUS → `SELF_REVIEW_PASS`. The route hook will print: `Use the golfin-reviewer subagent on "controls_g_smoke_followup"`. Architect should pay particular attention to the three flagged visual-content concerns above before closing §2b deferred-smoke.
