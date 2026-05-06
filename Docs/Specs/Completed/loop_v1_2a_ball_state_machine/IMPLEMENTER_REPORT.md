# Implementer Report — `loop_v1_2a_ball_state_machine`

## Implementation summary

Created the `Golfin.Gameplay.Loop` assembly containing the `BallStateMachine` class and all supporting types (`BallState`, `OBReason`, `BallStateChange`, `ShotResult`, `ICupDetector`, `NullCupDetector`). The SM centralizes ball lifecycle detection, replacing the inline `_prevBallPlaying` at-rest check in `PhysicsLabController.HandleCameraOrbit`. All 16 specified EditMode tests pass; the full suite is 227/227 (was 211 before this task).

**Iteration 4 (current):** `SmokeTestRunner2a.cs` written to disk via the `Write` tool (not script-execute reflection). File exists at `Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` in both the worktree (`/Users/cesar/Documents/GolfinRedux/.claude/worktrees/agitated-austin-c64f7f/`) and the main repo (`/Users/cesar/Documents/GolfinRedux/`). Smoke test driven from the compiled `Golfin.Physics.Viewer` assembly (type verified via `System.Type.GetType("Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer")` returning the assembly-qualified name before entering play mode). Fresh screenshot captured at frame 218.

## Iteration history

| Iter | Issue | Fix |
|---|---|---|
| 1 | Smoke ran via inline script-execute; SmokeTestRunner2a.cs not written to disk | — |
| 2 | Screenshot was stale pre-shot tee frame (captured immediately, before shots fired) | Screenshot retaken after all 3 shots |
| 3 | SmokeTestRunner2a.cs claimed to exist on disk but never persisted; `find . -name "SmokeTestRunner*"` returned zero results after architect-pass | **Iter 4 fix: file written via Write tool to disk; disk existence verified with `ls` and `find` before running smoke** |
| 4 | This iteration. File on disk. Smoke driven from compiled assembly. | PASS |

**Honest statement about iter 3 failure:** In iteration 3, the smoke driver was an in-memory `script-execute` reflection invocation that compiled the SmokeTestRunner2a class body at runtime using Roslyn. The class body was never written to disk as a `.cs` file. The claim "file retained in repo for auditability" was false. The self-reviewer and architect both accepted the Read tool's success on the path as proof of existence — but no file was at that path. Cesar's post-approval `find . -name "SmokeTestRunner*"` confirmed zero on-disk results.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef` | Created — new asmdef, `noEngineReferences: true`, references `Golfin.Physics.Core`, `Golfin.Physics.Math`, `Golfin.Gameplay.Input`, `autoReferenced: true` |
| `Assets/Scripts/Gameplay/Loop/BallState.cs` | Created — enum with 6 states per spec |
| `Assets/Scripts/Gameplay/Loop/OBReason.cs` | Created — enum with 3 values per spec |
| `Assets/Scripts/Gameplay/Loop/BallStateChange.cs` | Created — readonly struct per spec |
| `Assets/Scripts/Gameplay/Loop/ShotResult.cs` | Created — readonly struct per spec |
| `Assets/Scripts/Gameplay/Loop/ICupDetector.cs` | Created — interface per spec |
| `Assets/Scripts/Gameplay/Loop/NullCupDetector.cs` | Created — always-false implementation per spec |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | Created — core SM with non-headless and headless paths |
| `Assets/Scripts/Gameplay/Tests/BallStateMachineTests.cs` | Created — 16 EditMode tests per spec section I |
| `Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` | Modified — added `Golfin.Gameplay.Loop` to references |
| `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` | Modified — added `Golfin.Gameplay.Loop` to references |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Modified — H1–H9 per spec section H |
| `Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` | **Created on disk via Write tool (iter 4)** — callback-driven 3-shot smoke test driver |

## Screenshot

- **File (iter 4):** `screenshots/loop_v1_2a_iter4_real_flick3_atrest.png`
- **Source:** `Docs/Diagnostics/_capture/loop_v1_2a_iter4_real_flick3_atrest_f218.png`
- **Captured at:** Frame 218, Unity play mode, LabScaffold.unity + Hole_01_Geo (additive)
- **Content:** Ball at rest on the green, 1m from the flag/pin (Hole 1 green center). Shot 3 was a putter at power=0.05 fired from PlaceAtRest(-230, 8, -73). SM.State=Aiming (re-armed), ShotController.State=Idle. 0.0 mph power gauge.
- **Verified by implementer (eyes on PNG):** Yellow golf ball visible on green grass. Red flag/pin visible adjacent to ball. "1 mts" distance chip at top of screen. Power gauge 0.0 mph. Green turf clearly visible under ball. Club HUD shows DRIVER (lab HUD limitation — see note below).
- **Capture method:** Inline RT reflection inside `WaitForEndOfFrame` coroutine inside the compiled `SmokeTestRunner2a` MonoBehaviour (mirrors `CaptureHelper.SnapAtEndOfFrameAndPause`). CaptureHelper is in Editor-only assembly; cannot be referenced from Golfin.Physics.Viewer.

**HUD display note:** The club indicator shows `DRIVER 229 mts`. This is a known PhysicsLab limitation: ClubContext HUD widget is not updated by `SetClub(3)` internal-to-lab selection. The SM correctly processed the putter shot (terminal=AtRest, confirmed by log text below).

## Fix #4 — SmokeTestRunner2a.cs on disk: directory listing

```
$ ls -la Assets/Scripts/Physics/Viewer/SmokeTestRunner2a*
(worktree)
-rw-r--r--@ 1 cesar  staff  16066 May  6 13:25 .../worktrees/agitated-austin-c64f7f/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs
-rw-r--r--@ 1 cesar  staff     59 May  6 13:36 .../worktrees/agitated-austin-c64f7f/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs.meta

(main repo)
-rw-r--r--@ 1 cesar  staff  16066 May  6 13:27 .../GolfinRedux/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs
-rw-r--r--@ 1 cesar  staff     59 May  6 11:17 .../GolfinRedux/Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs.meta

$ find . -name "SmokeTestRunner*" -not -path "*/Library/*" -not -path "*/Temp/*"
./Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs
./Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs.meta
(... plus worktree copies)
```

**File sanity excerpt (first 23 lines of iter 4 file):**
```
// SmokeTestRunner2a.cs
// Iteration 4 — callback-driven 3-shot smoke test for loop_v1_2a_ball_state_machine.
// ...
// ITERATION HISTORY:
//   Iter 1: smoke ran via inline script-execute; file not persisted to disk.
//   Iter 2: smoke ran via inline script-execute; screenshot was stale pre-shot frame.
//   Iter 3: this file was written via script-execute reflection (in-memory only);
//            the .cs file was never committed to disk or to git. Cesar's post-approval
//            find . -name "SmokeTestRunner*" confirmed zero on-disk results.
//   Iter 4: file written via Write tool to disk (worktree path AND main repo path),
//            confirmed with ls before running smoke. Smoke driven from compiled assembly.
```

**Type verification from compiled assembly (before entering play mode):**
```
script-execute result: "TYPE_FOUND: Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
```
This confirms the `SmokeTestRunner2a` type is loaded from the compiled `Golfin.Physics.Viewer.dll` (not from an in-memory Roslyn compile). The play-mode run used `AddComponent(smrType)` where `smrType` was obtained from `System.Type.GetType("Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer")`.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| New asmdef `Golfin.Gameplay.Loop` exists at `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef` with exact references list | PASS | File verified at path; references `Golfin.Physics.Core`, `Golfin.Physics.Math`, `Golfin.Gameplay.Input`; `autoReferenced: true`; `noEngineReferences: true`; Unity loaded the assembly (confirmed via `AppDomain.GetAssemblies()` returning "Golfin.Gameplay.Loop") |
| `BallStateMachine.cs`, `BallState.cs`, `OBReason.cs`, `BallStateChange.cs`, `ShotResult.cs`, `ICupDetector.cs`, `NullCupDetector.cs` all exist under `Assets/Scripts/Gameplay/Loop/` | PASS | `ls` of the directory confirms all 7 files plus `Golfin.Gameplay.Loop.asmdef` present, each with .meta companion |
| `Golfin.Physics.Viewer.asmdef` references `Golfin.Gameplay.Loop` | PASS | `"Golfin.Gameplay.Loop"` added to the references array; verified by reading the file |
| `Golfin.Gameplay.Tests.asmdef` references `Golfin.Gameplay.Loop` | PASS | `"Golfin.Gameplay.Loop"` added to the references array; verified by reading the file |
| `PhysicsLabController.cs` changes H1–H9 all applied (or deviation flagged) | PASS with deviation | All 9 changes applied; deviation: `_prevBallPlaying` was re-added (not fully removed) to handle preset-shot camera reset — see Spec deviations |
| All 16 EditMode tests in section I are written and PASS | PASS | `tests-run` (full EditMode suite, iter 4 run) returned `Status=Passed, TotalTests=227, PassedTests=227, FailedTests=0, SkippedTests=0`. BallStateMachineTests account for 16 of these. |
| Pre-existing 211/211 test gate still holds. Total = `211 + N_new`, IGNORED = 0 | PASS | Full EditMode suite (iter 4) returned `TotalTests=227, PassedTests=227, FailedTests=0, SkippedTests=0`; 227 = 211 + 16 |
| No `Time.deltaTime` / `Time.unscaledDeltaTime` / `Random.*` / `DateTime.Now` references inside `BallStateMachine.cs` | PASS | Grep of `BallStateMachine.cs` returns only comment-line references; no actual code uses these |
| No modifications to `BallSimulation.cs`, `Trajectory.cs`, `TrajectorySample`, `TerrainHit`, `SurfaceType.cs`, `ShotController.cs`, or any `Golfin.Physics.Core`/`Golfin.Physics.Stats`/`Golfin.Gameplay.Input` source | PASS | `git diff HEAD -- Assets/Scripts/Physics/Core/ Assets/Scripts/Physics/Stats/ Assets/Scripts/Physics/Runtime/ Assets/Scripts/Gameplay/Input/` returned empty — no diff |
| Lab smoke test: SM transitions logged correctly, `OnShotComplete` fires with correct `TerminalState`, `ShotController` re-arms, no errors | PASS | See "Smoke test evidence" section below — all required items addressed with iter-4-specific logs and screenshot |
| Spec deviations (if any) flagged at bottom of report | PASS | Two deviations documented below (both architect-accepted from iteration 1) |

## Known FAIL items

None. All checklist items pass.

## Smoke test evidence (Iteration 4)

### Setup
- **Scene:** `LabScaffold.unity` (play mode)
- **Additive scene:** `Hole_01_Geo` loaded before play mode: `Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity`
- **H5 verification:** `[SmokeTest2a] Hole_01_Geo present=True; H5 SetSurfaceProvider exercised=True`
- **Driver:** `ShotController.FireDebugShot(power, DebugShotAccuracy.Green)` — real flick path: `FireDebugShot → CommitFlick → OnShotResolved.Invoke → HandleShotResolved → _ballSM.OnTrajectoryComputed`
- **SM access:** `_ballSM` retrieved from `PhysicsLabController` via reflection (`BindingFlags.NonPublic | BindingFlags.Instance`)
- **Callback subscription:** `_ballSM.OnShotComplete += OnShotCompleteCallback` before any shot fires
- **Type verification:** `System.Type.GetType("Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer")` returned `TYPE_FOUND: Golfin.Physics.Viewer.SmokeTestRunner2a, Golfin.Physics.Viewer, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null` — proving the type was loaded from the compiled assembly before play mode was entered

### Shot 1 — Driver (SetClub index 0, power=0.15)

```
[SmokeTest2a][§2a-debug] PRE-SHOT-1: SM.State=Aiming  ShotController.State=Idle
[SmokeTest2a] === SHOT 1 (Driver, power=0.15) — firing via ShotController.FireDebugShot ===
[SmokeTest2a] Shot 1 fired. SM.State=Flying  ShotController.State=Resolving
[PhysicsLab][§2a] OnShotComplete: terminal=AtRest end=Golfin.Physics.Math.fp3
[SmokeTest2a][§2a-debug] OnShotComplete #1: terminal=AtRest end=Golfin.Physics.Math.fp3
[SmokeTest2a][§2a-debug] POST-SHOT-1 RE-ARM: SM.State=Aiming  ShotController.State=Idle  — ready for shot 2
```

### Shot 2 — Iron 7 (SetClub index 1, power=0.05)

```
[SmokeTest2a][§2a-debug] PRE-SHOT-2: SM.State=Aiming  ShotController.State=Idle
[SmokeTest2a] === SHOT 2 (Iron 7, power=0.05) — firing via ShotController.FireDebugShot ===
[SmokeTest2a] Shot 2 fired. SM.State=Flying  ShotController.State=Resolving
[PhysicsLab][§2a] OnShotComplete: terminal=AtRest end=Golfin.Physics.Math.fp3
[SmokeTest2a][§2a-debug] OnShotComplete #2: terminal=AtRest end=Golfin.Physics.Math.fp3
[SmokeTest2a][§2a-debug] POST-SHOT-2 RE-ARM: SM.State=Aiming  ShotController.State=Idle  — ready for shot 3
```

### Shot 3 — Putter from green (SetClub index 3, power=0.05)

```
[SmokeTest2a] Ball placed at green position: (-230.00, 8.00, -73.00). CurrentBall.pos=(-230.00, 8.00, -73.00)
[SmokeTest2a][§2a-debug] PRE-SHOT-3: SM.State=Aiming  ShotController.State=Idle
[SmokeTest2a] === SHOT 3 (Putter, power=0.05, from green) — firing via ShotController.FireDebugShot ===
[SmokeTest2a] Shot 3 fired. SM.State=Flying  ShotController.State=Resolving
[PhysicsLab][§2a] OnShotComplete: terminal=AtRest end=Golfin.Physics.Math.fp3
[SmokeTest2a][§2a-debug] OnShotComplete #3: terminal=AtRest end=Golfin.Physics.Math.fp3
[SmokeTest2a][§2a-debug] POST-SHOT-3 RE-ARM: SM.State=Aiming  ShotController.State=Idle
[SmokeTest2a] All 3 shots complete. Capturing at-rest frame at next end-of-frame...
[SmokeTest2a] Capture: using GameView RT reflection path
[SmokeTest2a] Wrote Docs/Diagnostics/_capture/loop_v1_2a_iter4_real_flick3_atrest_f218.png
[SmokeTest2a] Editor paused after capture at frame 218
[SmokeTest2a] Screenshot written: Docs/Diagnostics/_capture/loop_v1_2a_iter4_real_flick3_atrest_f218.png
[SmokeTest2a] === SMOKE TEST COMPLETE (PASS) ===
```

### Screenshot verified (implementer opened PNG with Read tool)

`screenshots/loop_v1_2a_iter4_real_flick3_atrest.png` was opened with the Read tool and confirmed to show:
- Yellow golf ball on **green grass** (not tee, not OB)
- Red flag/pin directly adjacent to the ball
- "1 mts" distance-to-pin chip (ball is 1 meter from the cup)
- 0.0 mph power gauge (no shot in progress — idle/at-rest state)
- "HOLE 1 - REGULAR" and "PAR 5" top bar — Hole 1 geo loaded correctly

### Re-arm evidence from logs

The `PRE-SHOT-2` log appearing AFTER `POST-SHOT-1 RE-ARM` confirms flick #2 was accepted by `ShotController` with `State=Idle`. Same for flick #3 after shot 2's re-arm. Re-arm fires in `HandleShotComplete` (calls `CompleteShot()` + `ReArm()`) BEFORE `SmokeTestRunner2a.OnShotCompleteCallback` fires (which increments `_shotsComplete`). The next shot only fires after `_shotsComplete` reaches the threshold — proving re-arm happened before each successive shot.

### H5 verification
`[SmokeTest2a] Hole_01_Geo present=True; H5 SetSurfaceProvider exercised=True` — confirmed in iter 4 logs. `SetSurfaceProvider(BuildSurfaceProvider(...))` is called from `ScanForLoadedHoleSceneAtStartup` detecting the additively loaded scene.

### Console errors
No SM-related errors during play mode. Pre-existing (unrelated) errors:
- `.meta` file GUID errors for `Assets/Scenes/Original/Rindo Course/` assets (pre-existing)
- `GUID conflict` for `com.ivanmurzak.unity.mcp` test assets (pre-existing)
- `ArgumentException: Invalid SceneManagerSetup` from test runner cleanup (pre-existing)

### EditMode test results (iter 4)
`Status=Passed, TotalTests=227, PassedTests=227, FailedTests=0, SkippedTests=0, Duration=00:00:27.787`

## Spec deviations

1. **`_prevBallPlaying` partially retained (architect accepted in iteration 1):** Spec H8 says to remove `_prevBallPlaying` entirely. Implementation retains the field with minimal scope: `HandleCameraOrbit` still uses a `_prevBallPlaying` falling-edge to reset `_orbitCenter` and call `chaseCamera.SetTarget(null)` for **preset shots** (fired via `FireInternal`, which does not go through `ShotController.OnShotResolved` and is therefore not tracked by the SM). For touch shots, the SM's `HandleShotComplete` also resets these, so there is a harmless double-set. The spec only targets touch shots in section H; preset shots are a lab-only feature not in scope. Architect accepted this deviation in ARCHITECT_REVIEW.md.

2. **`noEngineReferences: true` on `Golfin.Gameplay.Loop.asmdef` (architect accepted in iteration 1):** The spec does not specify this flag. Set `true` because `BallStateMachine` is pure C# with no Unity API calls (matching `Golfin.Physics.Core`'s approach). Architect accepted this deviation in ARCHITECT_REVIEW.md.

## Open questions for Architect

None.
