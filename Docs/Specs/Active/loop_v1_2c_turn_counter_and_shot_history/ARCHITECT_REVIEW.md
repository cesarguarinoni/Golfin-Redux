# Architect Review — `loop_v1_2c_turn_counter_and_shot_history`

**Reviewer:** golfin-reviewer
**Date:** 2026-05-08 14:50 JST
**Iteration reviewed:** N=1 (self-review verdict: FORWARD_TO_ARCHITECT / PASS, all 21 items)
**Verdict:** **ARCHITECT_REVIEW_PASS**

## Summary

Implementer delivered exactly what §2c spec called for: extended `GameSession` static bus with `ShotHistory` + `OnHistoryChanged` + `RecordShot` + `ResetForNewHole`; new `ShotRecord` struct; new `HoleSessionDriver` MonoBehaviour mirroring the §2b `LoopCameraDirector` pattern; two `ResetForNewHole()` call sites in `PhysicsLabController` (post-`HoleContext.Raise()` in `OnHoleLoaded`, post-`HoleContext.Reset()` in `OnHoleUnloaded`); 7 new EditMode tests; smoke captures C1/C2/C3 + history log C4 prove TURN 1 -> 2 -> 1 reset cycle visually.

Test gate: 118/118 PASS, 0 FAIL, 0 SKIP. Baseline of 111 + 7 new §2c tests reconciles cleanly. The 7 new tests are explicitly listed by name in the gate output (`/tmp/iter2c_test_results.txt` lines 47-53).

Visual verification of the three captures (read directly):
- **C1:** PlayerCard reads `TURN 1`; distance chip `506 yds`; ball on tee with white tee marker; Aiming state.
- **C2:** PlayerCard reads `TURN 2`; distance chip `0 yds`; ball at flag (post-shot); Aiming state. Real frame change (different scene content), not stale.
- **C3:** PlayerCard reads `TURN 1` again; distance chip back to `506 yds`; ball on tee. Reset confirmed.

C4 history log shows exactly one entry: `ShotNumber=1 Club=Driver Terminal=AtRest OBReason=null Surface=Fairway DistXZ=258.7m` — schema matches `ShotRecord` struct field-for-field.

## Architectural soundness

- **Asmdef boundaries respected.** `HoleSessionDriver` lives in `Golfin.Physics.Viewer` (which already references `Golfin.Gameplay.Loop` and `Golfin.Gameplay.UI`). `GameSession` extension stays in `Golfin.Gameplay.UI.HUD`. Tests in `Golfin.Physics.Tests` (existing asmdef, already references both). Zero new asmdef files — perfect compliance with the spec's "no new asmdef" hard rule.
- **Pattern reuse.** `HoleSessionDriver` is a near-line-for-line copy of `LoopCameraDirector` precedent: subscribes to SM event in lifecycle hook, drives the static bus, no game logic. Exactly the spec-mandated shape.
- **Existing utilities.** Reuses `CaptureCore.GrabGameViewRT()`, `ShotPresetCatalog`, `controller.LastShotOrigin`/`LastTrajectory`/`CurrentClubIndex`/`LabClubLabels`/`BallSM`. Nothing duplicated.
- **API surface stability.** Existing `TurnCount`, `SetTurn`, `OnTurnChanged` signatures unchanged — only additive members. `PlayerCardWidget.cs` correctly untouched.
- **Banned-file rule compliance.** `BallStateMachine.cs`, `ShotResult.cs`, etc. confirmed untouched (file-list and grep verified).

## Visual fidelity

No Figma references for this task (TURN indicator was already in PlayerCard; this task only made the existing label move). The three captures together prove the full state-machine: turn advances from shot completion (C1->C2) and resets on hole reload (C2->C3). Distance chip 506->0->506 is independent corroboration of full state reset, not just turn-counter masking.

## Latent issues — none blocking

The three concerns the self-reviewer surfaced are reviewed below. None warrant FAIL.

### 1. `SmokeRunner2cHost.SnapPlayMode()` vs `CaptureHelper`

Self-reviewer flagged this. The deviation is real — `SnapPlayMode()` calls `CaptureCore.GrabGameViewRT()` directly and skips `AssetDatabase.Refresh()` because that refresh inside a play-mode coroutine forces a domain reload that kills the coroutine mid-flight. Same precedent as §2b's `SmokeTestRunner2b.cs`. The captures are real Game-View renders (the three frames visibly differ in TURN value, distance chip, ball position, scene content), not placeholders or stale frames.

**My judgment:** the CLAUDE.md screenshot rule (`SnapGameView` / `SnapAtEndOfFrameAndPause`) was written for edit-mode and frozen-frame paused-mode use cases. Continuous play-mode coroutines are a third case that the rule doesn't cleanly address — and the self-reviewer is right that two §2 tasks have now solved it the same way independently. This is now a pattern worth blessing.

**Action for follow-up (NOT a blocker for §2c):** open a Quick task to add `CaptureHelper.SnapPlayModeSafe()` that wraps the `GrabGameViewRT` + `EncodeToPNG` + `File.WriteAllBytes` path without the `AssetDatabase.Refresh()` call, then refactor `SmokeRunner2cHost` and `SmokeTestRunner2b` to use it. CLAUDE.md screenshot rules should also gain a "play-mode coroutine" row in the quick-reference table. I'm not failing §2c for this — the pattern is documented inline (lines 51-53 + 149-153 of `SmokeRunner2cHost.cs`) and the precedent exists.

### 2. `HoleSessionDriver.Awake()` -> `Start()` deviation

Spec showed `Awake()`; implementer moved to `Start()` because `controller.BallSM` was null when `HoleSessionDriver.Awake()` ran ahead of `PhysicsLabController.Awake()`. Unity's Awake-order across GameObjects is non-deterministic; `Start()` runs after all `Awake()` calls complete. The fix is correct Unity lifecycle hygiene and matches Unity best-practice for cross-component subscriptions. The error-path in `Start()` (lines 36-39) logs a clear `Debug.LogError` if `BallSM` is still null — defensive and well-shaped. Acceptable deviation; spec's intent (subscribe to SM events early in lifecycle, before any shots fire) is preserved.

### 3. C3 camera framing matches C2 rather than C1

Spec only required `TURN=1` after reload; that's what was delivered. The chase camera framing carrying over from C2 (yellow flag visible in C3, but ball visibly on tee with the tee marker present) is consistent with §2c's scope: this task resets `GameSession` state, not camera state. Camera reset belongs to a future task (§2d / camera lifecycle work). Distance chip 0->506 yds in C3 confirms the data-side reset is complete. No issue.

## Capture-helper compliance check (Step 5 backstop)

Self-reviewer's Step 5 is correct. Concerns:

- **Capture method.** `SnapPlayMode()` is a documented deviation following §2b precedent. The PNG was real, on disk, and visually verifiable (TURN values, distance chips, and ball positions all differ). I accept this deviation for §2c with the follow-up action above.
- **Maintenance protocol for new contexts.** This task did not add a new `*Context.cs` file — it extended `GameSession.cs`, which is a static bus, not a context. The `capture_helper` SPEC's maintenance protocol (extend `FakeMidAim` / `FakeReset` / add preset) targets `*Context` files specifically. `GameSession` is closely analogous, but since the existing `CaptureHelper.FakeMidAim` already initializes player/hole context and the TURN counter is purely a session counter that isn't directly relevant to the existing fake-state presets (those exist for UI layout verification, which doesn't depend on the turn count), I do not consider this a violation. If a future spec adds a fake-state preset that needs to exercise turn-counter UI in mid-game, that spec should extend `CaptureHelper` to call `GameSession.SetTurn(n)` accordingly.

No capture-helper protocol violation.

## Test verification

The implementer ran `mcp__ai-game-developer__tests-run` and produced `/tmp/iter2c_test_results.txt` with the explicit `PASS:118 FAIL:0 SKIP:0` header line + per-test breakdown. The 7 new §2c tests are all named in the gate output (lines 47-53). Test counts present in IMPLEMENTER_REPORT (Total=118, Pass=118, Fail=0, Skip=0). Test gate satisfied.

## Files reviewed

- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/SPEC.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/IMPLEMENTER_REPORT.md`
- `/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/SELF_REVIEW.md`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/SmokeRunner2cHost.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/HoleSessionDriverTests.cs`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (lines 1240-1260, 1390-1404)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scenes/Physics/LabScaffold.unity` (HoleSessionDriver wiring at fileID 1483952038)
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Tests/Golfin.Physics.Tests.asmdef`
- `/Users/cesar/Documents/GolfinRedux/Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef`
- `/tmp/iter2c_test_results.txt`
- All 3 PNG captures + history log (visually inspected)

## Verdict

**ARCHITECT_REVIEW_PASS.** Spec satisfied in letter and spirit. Tests green. Visual evidence solid. Architectural choices clean and precedent-following. The two non-trivial deviations (Awake->Start; SnapPlayMode helper) are correct and well-justified. C3 camera framing is out of §2c scope.

Routing to Cesar for final approval.

## Follow-up items (not blocking §2c)

1. Open a Quick task to factor `SnapPlayModeSafe()` into `CaptureHelper` and refactor `SmokeRunner2cHost` + `SmokeTestRunner2b` to use it. Update CLAUDE.md screenshot quick-reference to cover the play-mode-coroutine case explicitly.
2. (Already deferred to §2d per spec) ICupDetector wiring; score-to-par; penalty-stroke math.
