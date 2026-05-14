# SPEC AMENDMENT — `putter_aim_yaw_in_groundlevel`

**Date:** 2026-05-14 12:00 JST
**Author:** Architect (claude.ai)
**Supersedes:** Original SPEC + iter-1 through iter-5 in working tree

## What changed

After 5 implementer iterations, the original spec's L4 ("Reuse `ChaseCamera.GroundLevel`") was identified as the root cause. `GroundLevel` mode introduced a parallel camera framing system that fought with `ApplyCameraYaw`, requiring putter-specific branches in `HandleShotComplete`, an early-return in `LoopCameraDirector`, and a special-case `Mode.GroundLevel` branch in `ChaseCamera` — none of which could coexist with iron's working code path without race conditions.

Cesar directive 2026-05-14 11:30 JST: putter Aiming must place the 3D ball at the same on-screen vertical position as iron Aiming. That ruled out `GroundLevel` for Aiming. Since Aiming is the load-bearing camera state, this rules out `GroundLevel` for putter entirely (no point flipping modes mid-stroke).

## Revised L4

**L4 (revised):** Putter uses `ChaseCamera.Mode.Chase` for EVERYTHING (Aiming, Flying, Rolling, AtRest). The camera does not know putter mode exists. Only the SHOT physics (velocity, trajectory, surface coefficients) distinguish putter from iron. Putter Aiming code path is byte-identical to iron Aiming code path.

`ChaseCamera.Mode.GroundLevel` enum value remains in the codebase but is unused. Reintroduction for green-reading / low-angle framing is a future spec (likely tied to Order 110 predictor redesign), not piggybacked on putter mode.

## Execution plan

### Step 1 — clear current task's working-tree churn

```bash
git restore Assets/Scripts/Physics/Viewer/ChaseCamera.cs
git restore Assets/Scripts/Physics/Viewer/PhysicsLabController.cs
git restore Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs
```

Zero ghost code from iter-1 through iter-5. Iter-5's `ChaseCamera` null-target early-return is discarded here and re-added cleanly in Step 3.

### Step 2 — remove §2f camera divergences from HEAD

**File: `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`**

2a. `EnterPutterMode` — delete the `chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel)` call and its `[§2f] EnterPutterMode...` debug log. Keep all other UI changes in the method (track visibility, ball selector fade, action button row, central ball putter mode flag, etc.).

2b. `ExitPutterMode` — delete the matching `chaseCamera.SetMode(ChaseCamera.Mode.Chase)` call and its `[§2f] ExitPutterMode...` debug log. Keep all other UI restoration.

2c. `RepositionBallWithLookDir` — delete `if (_shotController != null && _shotController.IsPutt && chaseCamera != null) chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel);`

2d. `SetupAtTee` — delete the identical `if (_shotController.IsPutt && chaseCamera != null) chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel);` block.

2e. `HandleShotComplete` AtRest case — delete the `if (willFlipToPutter) { ... break; }` block entirely. Recompute `target` and `willFlipFromPutter` if still needed, but the willFlipToPutter early-out is gone. All AtRest paths flow through the §2e pin-aim rotation + `ApplyCameraYaw` sequence regardless of club. (Note: §2e pin-aim rotation runs even for putter — this is acceptable because the rotation only triggers when XZ distance to pin > 1cm, and the user can still drag-orbit afterward.)

**File: `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`**

2f. `HandleStateChanged` — delete the `if (isPutt && (change.Next == BallState.Flying || change.Next == BallState.Rolling || change.Next == BallState.AtRest)) return;` block (lines ~198-202). After deletion, putts go through the same `ApplyMode` + `SetTarget(null)` path as iron.

### Step 3 — add the null-target early-return guardrail (CORRECTED 2026-05-14 12:30 JST)

**File: `Assets/Scripts/Physics/Viewer/ChaseCamera.cs`**

In `RunLateUpdateLogic`, replace the existing "Chase + null-target early-return" with one that covers both orbit-based modes:

```csharp
// GUARDRAIL: orbit-based modes (Chase, GroundLevel) cede the camera transform
// to PhysicsLabController.ApplyCameraYaw when target is null. Pivot/focus-based
// modes (Overhead, Downrange, CupZoom, OBFreeze) operate from explicit world
// points (_obFreezePivot, _cupZoomFocus, etc.) and intentionally run with null
// target after a Director-driven terminal-state transition that clears it.
//
// Pre-2026-05-14: this early-return only applied to Mode.Chase, which let the
// GroundLevel branch (originally §2f's putter framing) race with ApplyCameraYaw
// in Aiming. Extending to GroundLevel is forward-looking: post-§2f-revert,
// GroundLevel is unused in production, but any future low-angle mode (Order 110
// predictor redesign etc.) will inherit this guardrail automatically.
if (_target == null && (_mode == Mode.Chase || _mode == Mode.GroundLevel)) return;
```

This is the only ADDITIVE change in the entire amendment. CupZoom and OBFreeze cinematics remain functional (they use explicit pivots, target-null is normal for them).

### Step 4 — update tests

Any EditMode test that asserts `ChaseCamera.SetMode(GroundLevel)` is called on `EnterPutterMode` / `SetClub(Putter)` / putter shot lifecycle WILL fail. Those assertions must be deleted or inverted (assert `Chase` is preserved). Test files to audit:

- `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs`
- Any `PhysicsLab*Tests.cs` or `ChaseCamera*Tests.cs` that touches putter-mode framing

Architect-Implementer Note: Step 4 may surface 1-3 broken tests. If broken-test count exceeds 5, escalate `BLOCKED` — likely indicates a test invariant we didn't anticipate.

## Forbidden patterns (hard rules for any future putter-mode work)

1. **No `chaseCamera.SetMode(...)` calls from `EnterPutterMode` / `ExitPutterMode`.** The camera mode is irrelevant to putter UI state.
2. **No `isPutt` checks in `LoopCameraDirector.HandleStateChanged`.** Camera state transitions are club-agnostic.
3. **No putter-specific branches in `PhysicsLabController.HandleShotComplete`.** AtRest is AtRest regardless of club.
4. **No re-introduction of `Mode.GroundLevel` in any code path.** If a future spec wants low-angle framing, it gets its own design pass.
5. **No "defense-in-depth" guards that silently mask bugs.** If a putter behaves differently than iron in any camera-related code path, that's a regression, not a feature.

## Definition of done

- Steps 1-4 executed in order, each verified before proceeding.
- Test suite green (baseline +/- the test-file edits from Step 4).
- Cesar manually verifies in Lab:
  - Hit iron approach onto green → camera frames ball at AtRest, ball at same on-screen vertical position as before.
  - Auto-switch to putter triggers (UI changes: track visible, ball selector faded, etc.) but camera framing is unchanged from iron AtRest.
  - First putt fires, camera tracks ball through Flying/Rolling, AtRest → camera re-frames ball at same on-screen vertical position.
  - Second putt: drag to aim, no wobble, no jitter, camera moves smoothly with mouse drag.
  - Switch back to iron manually → no visible camera artifact.

## What stays in `EnterPutterMode` / `ExitPutterMode`

All non-camera behavior is preserved:

- `_shotConeView.SetPuttMode(true/false)`
- `_powerGaugeWidget.SetUnitMode(...)` and putt range
- `_holeIndicatorWidget.SetUnitMode(...)`
- `ClubButtonWidget` unit mode
- `_putterTrack.SetActive(true/false)` + `AlignPutterTrackToBall()`
- `_puttPathRoot.SetActive(...)`
- `_puttPathPredictor.enabled = ...`
- Action button row visibility
- `_ballSelectorCanvasGroup` alpha/interactable/blocksRaycasts
- `_centralBall.SetPuttMode(true/false)`

This is correct UI behavior. The bug was specifically the camera mode flip.

## Mea culpa

My §2f L4 decision ("Reuse `ChaseCamera.GroundLevel`") was the original sin. The five-iteration spiral existed because each iter attempted to make `GroundLevel` coexist with `ApplyCameraYaw` for Aiming, which is structurally impossible without one of them ceding ownership. This amendment cedes `GroundLevel` for putter entirely.
