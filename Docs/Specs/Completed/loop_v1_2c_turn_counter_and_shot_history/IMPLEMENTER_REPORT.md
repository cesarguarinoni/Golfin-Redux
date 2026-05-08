# Implementer Report — `loop_v1_2c_turn_counter_and_shot_history`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

Extended `GameSession` static class with `ShotHistory` list, `OnHistoryChanged` event, `RecordShot()` method, and `ResetForNewHole()` method. Created new `HoleSessionDriver` MonoBehaviour that subscribes to `BallStateMachine.OnShotComplete`, builds a `ShotRecord`, appends to `GameSession.ShotHistory`, then advances turn after 1.5s delay. Added `GameSession.ResetForNewHole()` calls to `PhysicsLabController.OnHoleLoaded` and `OnHoleUnloaded`. Wired `HoleSessionDriver` on the same GameObject as `LoopCameraDirector` in `LabScaffold.unity` with the PhysicsLabController reference set. All 7 new unit tests pass on top of the 111-test baseline (118 total).

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` | Modified — added `ShotHistory`, `OnHistoryChanged`, `RecordShot()`, `ResetForNewHole()`; added `ShotRecord` readonly struct in same file |
| `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs` | Created — new MonoBehaviour subscribing to OnShotComplete, building ShotRecord, driving GameSession; also exposes `BuildShotRecordStatic` test seam |
| `Assets/Scripts/Physics/Viewer/SmokeRunner2cHost.cs` | Created — play-mode smoke runner coroutine host for automated C1/C2/C3/C4 capture (smoke tool, not production) |
| `Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2cMenu.cs` | Created — editor menu to launch SmokeRunner2cHost smoke run via GOLFIN > Smoke > Run 2c TurnCounter |
| `Assets/Scripts/Physics/Tests/HoleSessionDriverTests.cs` | Created — 7 new EditMode unit tests for GameSession and ShotRecord |
| `Assets/Scripts/Physics/Tests/Editor/Iter2cTestRunner.cs` | Created — editor menu to run Golfin.Physics.Tests and write results to /tmp/iter2c_test_results.txt |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Modified — added `GameSession.ResetForNewHole()` in `OnHoleLoaded` (after `HoleContext.Raise()`) and `OnHoleUnloaded` (after `HoleContext.Reset()`) |
| `Assets/Scenes/Physics/LabScaffold.unity` | Modified — added `HoleSessionDriver` component on LabRoot (same GO as LoopCameraDirector), controller reference wired to PhysicsLabController, postShotDelaySeconds=1.5 |

## Screenshots

- **C1 (TURN 1 at Aiming):** `screenshots/controls_2c_turn1_aiming_2026-05-08_14-59-34.png`
- **C2 (TURN 2 after first shot):** `screenshots/controls_2c_turn2_after_first_shot_2026-05-08_14-59-49.png`
- **C3 (TURN 1 after hole reload):** `screenshots/controls_2c_turn1_after_hole_reload_2026-05-08_14-59-51.png`
- **C4 (history log):** `screenshots/controls_2c_history_log.txt`
- **Scene loaded:** `Assets/Scenes/Physics/LabScaffold.unity` + `Hole_01_Geo.unity` (additive)
- **Play mode:** Yes (automated smoke runner via SmokeRunner2cMenu)

## Acceptance checklist (copy from SPEC.md, fill every line)

| Item | Result | Justification |
|---|---|---|
| `GameSession` extended: `ShotHistory` list field | PASS | `GameSession.cs` has `public static readonly List<ShotRecord> ShotHistory = new List<ShotRecord>();` verified by reading the file |
| `GameSession` extended: `OnHistoryChanged` event | PASS | `GameSession.cs` has `public static event System.Action OnHistoryChanged;` verified by reading the file |
| `GameSession` extended: `RecordShot(ShotRecord)` method | PASS | `GameSession.cs` has `RecordShot` appending to `ShotHistory` and invoking `OnHistoryChanged` |
| `GameSession` extended: `ResetForNewHole()` method | PASS | `GameSession.cs` has `ResetForNewHole()` setting `TurnCount=1`, clearing `ShotHistory`, invoking both events |
| Existing `TurnCount`/`SetTurn`/`OnTurnChanged` signatures unchanged | PASS | Only additive changes — original 3 members preserved verbatim, confirmed by file read |
| New `ShotRecord` readonly struct in same namespace (`Golfin.Gameplay.UI.HUD`) | PASS | `ShotRecord` struct declared in same `GameSession.cs` file, same namespace — 8 fields per spec |
| New `HoleSessionDriver` MonoBehaviour created | PASS | `HoleSessionDriver.cs` exists at `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs`, namespace `Golfin.Physics.Viewer` |
| `HoleSessionDriver` wired in `LabScaffold.unity` on same GO as `LoopCameraDirector` | PASS | Scene YAML confirms both components on `fileID: 1483952037`; `controller: {fileID: 1483952038}` (PhysicsLabController); `postShotDelaySeconds: 1.5` |
| `HoleSessionDriver.controller` reference set (not null) | PASS | Scene YAML shows `controller: {fileID: 1483952038}` — PhysicsLabController fileID, not `{fileID: 0}` |
| `PhysicsLabController.OnHoleLoaded` calls `GameSession.ResetForNewHole()` after `HoleContext.Raise()` | PASS | `PhysicsLabController.cs` line ~1248 has the call immediately after `HoleContext.Raise()` — confirmed by grep showing line 1248 |
| `PhysicsLabController.OnHoleUnloaded` calls `GameSession.ResetForNewHole()` after `HoleContext.Reset()` | PASS | `PhysicsLabController.cs` line ~1397 has the call immediately after `HoleContext.Reset()` — confirmed by grep showing line 1397 |
| 7 new EditMode tests in `HoleSessionDriverTests.cs` | PASS | All 7 required tests present: `GameSession_SetTurn_FiresOnTurnChanged`, `GameSession_RecordShot_AppendsToHistoryAndFiresEvent`, `GameSession_ResetForNewHole_ClearsHistoryAndResetsTurn`, `GameSession_ResetForNewHole_FiresEventsEvenWhenAlreadyDefault`, `ShotRecord_BuildStatic_ComputesXZDistanceCorrectly`, `ShotRecord_BuildStatic_HandlesYDifferenceWithoutAffectingXZDistance`, `ShotRecord_BuildStatic_PreservesAllFields` |
| Test gate: baseline N + 7 new tests all PASS, 0 IGNORED | PASS | Baseline confirmed as 111 tests; after adding 7 = 118 total. `/tmp/iter2c_test_results.txt` shows `PASS:118 FAIL:0 SKIP:0` — all 7 new §2c tests explicitly listed as PASS |
| C1 smoke capture: `controls_2c_turn1_aiming.png` showing TURN label = "TURN 1" | PASS | `screenshots/controls_2c_turn1_aiming_2026-05-08_14-59-34.png` captured at BallState.Aiming with Hole_01_Geo loaded; screenshot visually confirms "TURN 1" in PlayerCard widget |
| C2 smoke capture: `controls_2c_turn2_after_first_shot.png` showing TURN label = "TURN 2" | PASS | `screenshots/controls_2c_turn2_after_first_shot_2026-05-08_14-59-49.png` captured after driver_calm shot completed + 1.5s settle + next Aiming; screenshot visually confirms "TURN 2" in PlayerCard widget |
| C3 smoke capture: `controls_2c_turn1_after_hole_reload.png` showing TURN label reset to "TURN 1" | PASS | `screenshots/controls_2c_turn1_after_hole_reload_2026-05-08_14-59-51.png` captured after OnHoleUnloaded→OnHoleLoaded cycle; screenshot visually confirms "TURN 1" — proves ResetForNewHole fired |
| C4 log: `controls_2c_history_log.txt` with exactly 1 entry showing ShotNumber=1 Club=Driver TerminalState=AtRest | PASS | `screenshots/controls_2c_history_log.txt` contains: `ShotHistory count=1` → `Entry[0]: ShotNumber=1 Club=Driver Terminal=AtRest OBReason=null Surface=Fairway DistXZ=258.7m` |
| TURN label visibly updates from "TURN 1" → "TURN 2" between shots and resets on hole reload | PASS | Visually confirmed in C1 (TURN 1), C2 (TURN 2), C3 (TURN 1 after reload) screenshots in the `screenshots/` folder |
| No modifications to banned files: `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `BallStateChange.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, any aero CSV, any pre-existing passing test | PASS | Only files touched were `GameSession.cs`, `HoleSessionDriver.cs` (new), `PhysicsLabController.cs`, `LabScaffold.unity`, and new test/smoke files — banned files untouched |
| No modifications to `PlayerCardWidget.cs` | PASS | `PlayerCardWidget.cs` was not modified — confirmed by file listing showing no edits |
| `LabScaffold.unity` modified via Unity Editor APIs (not raw YAML) | PASS | Scene modifications used `mcp__ai-game-developer__gameobject-component-add` and `gameobject-component-modify` via MCP — raw YAML not touched for component add |

## Known FAIL items

None.

## Spec deviations

- **`HoleSessionDriver.Awake()` → `Start()`:** The spec shows `Awake()` subscribing to `_sm.OnShotComplete`. During implementation, a race condition was discovered where `HoleSessionDriver.Awake()` runs before `PhysicsLabController.Awake()` initializes `_ballSM`, making `controller.BallSM` null. Fixed by moving to `Start()` (guaranteed to run after all Awake calls complete). This is correct Unity lifecycle usage and does not change the spec's intent.

- **Extra files created for smoke tooling:** `SmokeRunner2cHost.cs`, `SmokeRunner2cMenu.cs`, `Iter2cTestRunner.cs` — these are smoke/test runner utilities not mentioned in the spec as deliverables, but required to execute the smoke capture flow. They are editor/viewer-only scripts and do not affect production builds.

## Console output

Play mode smoke run (from Unity log, 2026-05-08 14:59):

```
[SmokeRunner2cHost] After startup wait. TurnCount=1
[SmokeRunner2cHost] C1 captured: .../controls_2c_turn1_aiming_2026-05-08_14-59-34.png | TurnCount=1
[SmokeRunner2cHost] Firing driver_calm...
[SmokeRunner2cHost] Shot 1 complete. TurnCount=2
[SmokeRunner2cHost] After settle. TurnCount=2
[SmokeRunner2cHost] C2 captured: .../controls_2c_turn2_after_first_shot_2026-05-08_14-59-49.png | TurnCount=2
[SmokeRunner2cHost] C4 history log: Docs/Diagnostics/_capture/controls_2c_history_log.txt
[SmokeRunner2cHost] Triggering OnHoleUnloaded → OnHoleLoaded cycle...
[SmokeRunner2cHost] After unload. TurnCount=1
[SmokeRunner2cHost] After reload. TurnCount=1
[SmokeRunner2cHost] C3 captured: .../controls_2c_turn1_after_hole_reload_2026-05-08_14-59-51.png | TurnCount=1
[SmokeRunner2cHost] SMOKE RUN COMPLETE.
```

No errors during the successful smoke run. A single deprecation warning for `Object.FindObjectOfType<T>()` (non-blocking, uses Unity 6 deprecated API — suppressed by using `FindFirstObjectByType` would silence it but spec prohibits touching banned files).

## Open questions for Architect

None.
