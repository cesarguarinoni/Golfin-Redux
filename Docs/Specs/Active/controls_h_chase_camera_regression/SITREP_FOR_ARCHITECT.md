# Sit-rep for architect — controls_h_chase_camera_regression
*2026-05-08, after iter-5 R5 revert*

## TL;DR

Two architectural questions are blocking this task. The implementer has burned five iterations and the camera-mode side is still wrong. Cesar wants you to design the fix end-to-end before more code lands.

- **Q1 (camera modes — known):** Cesar ruled Option B in chat. Downrange must release at touchdown so Rolling stays in Chase. Current attempt does the routing but produces a **violent ground-snap** at the Downrange→Chase transition and the camera ends up parked while the ball rolls past. Smooth-blend logic is missing.
- **Q2 (aiming pin — new):** During Aiming for shot 2+, the camera is **not pinned to the ball at rest**. First-shot Aiming works (no `_target` set yet — `ApplyCameraYaw` writes the Transform unopposed). After shot 1 resolves, `_target` is the ball, and `ChaseCamera.LateUpdate` overrides any manual positioning. Five iterations have failed to land a clean ownership model for this case.

## Original problem (the one this task started for)

`controls_g` refactored `PhysicsLabController.HandleShotResolved` to fire `_ballSM.OnTrajectoryComputed(...)` synchronously. That fires `Aiming→Flying` BEFORE `_lastShotOrigin/Dir` are cached and BEFORE `BallAnimator.Play()` spawns the new ball — so the Director sees stale origin/dir and a destroyed target. Camera went static during flight on the touch path.

That root cause is fixed (Fix A, B, C, D, E, F, G + tests). The chase camera does follow during Flying. The two questions above are emergent issues that surfaced when Cesar manually verified beyond the original scope.

## Iter history (compact)

| Iter | What landed | Cesar verdict |
|---|---|---|
| 1 | Code fixes A–G; mid-flight screenshot evidence | Self-rev FAIL — screenshots showed pre-shot tee frames |
| 2 | Re-captured via off-screen-RT (`screenshot-camera`); architect-PASS | Cesar overturned manually after play |
| 3 | R1 chase-distance tighter (5m→3m, 2.5m→1.8m); R2 cinematic logging; R3 narrow (AtRest→Chase ModeMap entry); R4 first-shot pan priming via `Start()`+`GetDefaultLookDirection()`; tests added | Reviewer-FAIL (R3 too narrow for Cesar's complaint, R4 only hit one branch) |
| 4 | R4 widened to call `GetDefaultLookDirection()` unconditionally; gate 110→112 with R3-revised tests; screenshots still wrong | Self-rev escalated to architect (Q1 + Q2 question round) |
| 5 | R3-revised: Downrange release at touchdown via `progress >= predictedCarry`. R5: new `ChaseCamera.UpdateOrbitDirection(Vector3)` + `HandleCameraOrbit` routes through it in Chase mode + `SetupAtTee` calls `ResetToOrigin`. Test 14 (theatre). | Cesar manual reject: snap is violent; first-shot pan now broken; second-shot pan "loose" / camera snaps back to ball |

After iter-5 reject Cesar said "roll back the aiming changes to when it worked, take the camera-modes back to architect." I reverted only the R5 plumbing (see § "What I just reverted"). R3-revised stays in code pending your design. The HandleShotResolved order-of-ops fix and all of A–G stay.

## What I just reverted (this turn)

| File | Reverted change |
|---|---|
| `PhysicsLabController.HandleCameraOrbit` (Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:633–651 in iter-5) | Removed the `if (Chase) UpdateOrbitDirection else ApplyCameraYaw` branch. Restored unconditional `ApplyCameraYaw(cam)`. |
| `PhysicsLabController.SetupAtTee` (line 496 in iter-5) | Removed trailing `chaseCamera?.ResetToOrigin(teePos, lookDir)`. |
| `ChaseCamera.UpdateOrbitDirection(Vector3)` (whole method) | Deleted. |
| `LoopCameraDirectorTests.cs` Test 14 | Deleted (the `Assert.Pass` theatre test the self-reviewer flagged in iter-5). |

EditMode gate green after revert (`Status: Passed, FailedTests: 0, SkippedTests: 0`).

## What stays in code (your design will build on top)

- HandleShotResolved order fix (A) + FireInternal SM/Director routing (B) + BallStateMachine docstring (C) + integration test (D) + Lesson O (E) + SPEC template update (F) + SmokeTestRunner moves (G).
- iter-3 ChaseCamera tuning: `_followDistance=3f`, `_followHeight=1.8f`, `smoothTime=0.08f`, all serialized.
- iter-3 R3 narrow ModeMap entry: `AtRest → Chase`.
- iter-3 R4 priming: `Start()` calls `GetDefaultLookDirection()` unconditionally and writes `_cameraYaw` + `_shotController.CameraHeadingRadians`. This is what lets first-shot pan work via `ApplyCameraYaw`.
- iter-5 R3-revised release-at-touchdown logic in `LoopCameraDirector.TickCinematicCut` (lines 166–182): when `mode == Downrange` during Flying and ball XZ progress reaches predictedCarry, releases to `Chase` with live ball as target. **No smooth blend** — relies on SmoothDamp's 0.08s glide. Cesar reports the resulting transition is violent.
- Iter-5 Test 13 (`Director_DownrangeReleased_WhenBallPassesTouchdown`) — real test, kept.
- New diagnostic: top-center HUD `CAM: <Mode>   BALL: <State>` via `Assets/Scripts/Physics/Viewer/CameraModeDebugHUD.cs` (Editor-only, auto-bootstraps on scene load when a `ChaseCamera` is present). Use this when describing future bug reports.

## What works right now (post-revert)

- Code A–G correct in principle.
- First-shot Aiming sideways pan responds to mouse drag. (R4 priming + ApplyCameraYaw, no `_target` yet.)
- Camera tracks ball during Flying.
- Tests pass (245/0 fail/0 skip per the latest tests-run, though MCP reports a quirky leaf count — see iter-4/iter-5 notes).
- Two-consecutive-shots instance-ID log is dispositive of the original regression.

## What's broken right now

### Q2 — Aiming camera on shot 2+ is not pinned to the ball

Repro per Cesar's chat: fire shot 1 → ball settles → re-aim for shot 2 → camera does NOT stay pinned to the resting ball. Previous iter-5 attempt also produced a "loose" feel where the camera "snaps back to the ball" rather than staying locked.

**Why this happens (code trace):**

`ChaseCamera.LateUpdate` (Assets/Scripts/Physics/Viewer/ChaseCamera.cs:90–151):

```
if (_target == null && _mode == Mode.Chase) return;     // line 93
Vector3 focus = _target != null ? _target.position : _shotOrigin;
...
case Mode.Chase:
    desiredPos = focus - _launchDir * _followDistance + Vector3.up * (_followHeight + FollowHeightOffset);
    desiredRot = Quaternion.LookRotation(focus - desiredPos);
    break;
transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _velocity, smoothTime);
```

State at start of shot 2's Aiming:
- `_target` = the live ball (Director sets it on AtRest per iter-3 ModeMap).
- `_mode` = `Chase`.
- `_launchDir` = launch dir from shot 1 (or from `Start()` priming if untouched).

Result: LateUpdate every frame computes a position relative to the ball using the **stale `_launchDir` from shot 1**. Camera is "pinned to the ball" by the chase math, but at the angle of shot 1, not the current aim. `ApplyCameraYaw` from `HandleCameraOrbit` writes `cam.transform.position` from `_orbitCenter` + manual yaw, but LateUpdate immediately overrides it because `_target != null`.

This is the architectural conflict that has burned three iterations:

- **Approach A (iter-3/iter-4):** PhysicsLabController owns position via `ApplyCameraYaw` writing to `_orbitCenter`. Works only when `_target == null`. Breaks once a ball exists at rest.
- **Approach B (iter-5 R5):** Wrote a new `ChaseCamera.UpdateOrbitDirection(Vector3)` that updated `_launchDir` so chase-math would orbit the target. Broke first-shot (no target → LateUpdate early-exits, camera doesn't move at all). For shot 2+, the SmoothDamp glide felt loose and the camera "snapped back to the ball" rather than orbiting.

**The architectural question for Q2:**

Who owns camera position during Aiming when `_target` is set, and what's the math?

Three candidate designs:

1. **ChaseCamera grows an explicit orbit-yaw input.** Add `void SetOrbitYaw(float radians)` that overrides `_launchDir` for Chase math. PhysicsLabController writes the yaw from pan input every frame. Chase math becomes `desiredPos = focus + Quaternion.AngleAxis(yaw, Vector3.up) * (-Vector3.forward) * followDistance + up*followHeight`. Pin is preserved (focus = ball.position). Yaw is honoured. Single source of truth in ChaseCamera.

2. **Aiming uses a different ChaseCamera Mode.** Add `Mode.Aiming` to the enum. In Aiming mode, `desiredPos = focus + (orbit-rotated offset)`, no SmoothDamp (or very short smoothTime so pan feels responsive). Director switches to `Aiming` on `OnStateChanged(Aiming)` and back to `Chase` on `OnStateChanged(Flying)`.

3. **Clear `_target` on Aiming entry; restore on Flying entry.** Director calls `SetTarget(null)` when SM enters Aiming (after the AtRest hold). LateUpdate early-exits. PhysicsLabController's `ApplyCameraYaw` owns position via `_orbitCenter`. On `Aiming→Flying`, Director calls `SetTarget(currentBall)`. Risk: first frame of Flying has `_target` set but ball hasn't moved, so chase math snaps to behind-ball pose — same family as the violent snap from Q1.

Each option has trade-offs around: where the math lives, whether SmoothDamp glide feels right for pan, how the Aiming→Flying handoff blends, how the `_orbitCenter` field stays in sync.

Cesar's iter-5 attempt was a partial #1 (added the method, but kept SmoothDamp's full smoothTime so pan felt sluggish, and didn't gate it for the no-target first-shot case). If you pick #1, the spec needs to call out the smoothTime override for pan input.

### Q1 — Downrange→Chase release is violent

R3-revised in code releases Downrange when ball XZ progress crosses `predictedCarry`. The camera leaves Downrange's static park and Chase math takes over. The two poses can be tens of metres apart vertically and laterally — Downrange is parked downrange of the green at some elevated cinematic position; Chase wants to be 3m behind the ball at 1.8m up. SmoothDamp at 0.08s is a glide over ~0.16s but the magnitude is too large for that smoothTime to look smooth.

**The architectural question for Q1:**

What's the smooth-blend strategy for a large-magnitude mode transition?

- **Option α — Long blend window** for this specific transition only: lerp `transform.position` from Downrange pose to Chase pose over (say) 0.4–0.6s. Director enters a synthetic `BlendingDownrangeToChase` state for that window.
- **Option β — Anticipate touchdown**: start moving the Downrange pose toward the eventual Chase pose during the last 30% of the cinematic, so when the release fires, the gap is small enough for SmoothDamp to handle.
- **Option γ — Skip the cinematic entirely once the ball is close to landing.** Switch to Chase early (e.g., when descent crosses some altitude or progress threshold). Cinematic is only the apex of the arc, not the whole flight.
- **Option δ — Two-phase release**: ChaseCamera positions itself near the predicted landing point during the last fraction of Downrange, then SmoothDamp from there to the live-ball-relative pose at touchdown.

Cesar already ruled Option B for the higher-level design (Rolling stays in Chase). The Q1 question is purely about how to make the transition pleasant.

## Code locations to read

| File | Why |
|---|---|
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` (90–151) | LateUpdate. The Chase-math early-exit at line 93 + the chase pose math at 142–143 are the pivot point for Q2 design choices. |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` (165–185) | TickCinematicCut + R3-revised release-at-touchdown logic. The point where Q1 blend would fire. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (581–660) | HandleCameraOrbit + ApplyCameraYaw. Current owner of Aiming-camera position when `_target == null`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (456–501) | SetupAtTee. Where `_orbitCenter`, `_cameraYaw`, and `CameraHeadingRadians` are primed. |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | SM transition table. AtRest→Aiming via ReArm; Flying entry from OnTrajectoryComputed. |
| `Docs/Specs/Active/controls_h_chase_camera_regression/SPEC.md` § "Iteration 5 amendments" | The Q1=B / Q2=manual-verify rulings + R5 spec language Cesar wrote in chat. Loosened hard rules 2, 6, 7. |
| `Docs/Specs/Active/controls_h_chase_camera_regression/CESAR_REJECTION.md` | Iter-5 rejection note. |
| `Docs/Specs/Active/controls_h_chase_camera_regression/SELF_REVIEW.md` | iter-5 self-review (FORWARD verdict). |
| `Docs/Specs/Active/controls_h_chase_camera_regression/ARCHITECT_REVIEW.md` | iter-5 reviewer PASS with caveats. Was overturned. |
| `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson O + macOS addendum | Methodology lesson on dispatch-vs-visual evidence. Captures-on-macOS-via-Game-View-RT addendum. |

## Test gate baseline

After my revert: full EditMode passes, 0 failed, 0 skipped. Leaf count slightly down from 112 (Test 14 removed). The remaining R3-revised Test 13 stays in. New tests for Q1 / Q2 designs go on top.

## Diagnostic available for future iterations

Top-center debug HUD shows `CAM: <Mode>   BALL: <State>` live. Editor-only, auto-bootstraps on scene load via `[RuntimeInitializeOnLoadMethod]`. Cesar can read it during play and tell us exactly which mode the camera is in at the moment any new bug surfaces. Use it as the verification protocol — if a bug-report says "camera does X" and the HUD says `CAM: Y`, the user-visible problem maps to Y, not the assumed mode.

## What's NOT changing without your input

- No new code lands in Aiming-camera or Downrange-blend until you spec the design.
- Cesar's chat-side ruling stands: Option B for R3 (touchdown release + Rolling stays in Chase).
- Hard rule 2 in SPEC stays loosened: `LoopCameraDirector.cs` and `ChaseCamera.cs` ARE editable.
- The iter-5 R5 method `UpdateOrbitDirection` is GONE. If your design wants something like it, it needs to be designed fresh — don't restore the iter-5 version.

## Recommended deliverables from architect

1. Pick a design for Q2 (1, 2, 3, or a hybrid). Spell out the math, the field/method additions, and the SM-transition wiring.
2. Pick a strategy for Q1 (α, β, γ, δ, or other). Spell out what blend window or pose-anticipation logic ChaseCamera or LoopCameraDirector grows.
3. List the new tests Q1 + Q2 should add (concrete assertions, not theatre).
4. State whether the existing iter-3 `AtRest → Chase` ModeMap entry stays, or whether your Q2 design replaces it with an Aiming-mode dispatch.
5. State whether `_orbitCenter` (currently in `PhysicsLabController`) becomes redundant under your design or stays as a separate concept.
6. Estimate test-count delta from baseline so the implementer's gate target is clear.

Cesar will paste the spec amendment into SPEC.md § "Iteration 6 amendments" once you write it.
