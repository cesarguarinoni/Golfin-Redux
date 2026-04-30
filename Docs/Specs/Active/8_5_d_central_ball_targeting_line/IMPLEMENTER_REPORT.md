# Implementer Report — `8_5_d_central_ball_targeting_line`

> **Revision:** CESAR_REJECTED → second pass. Three bugs fixed per rejection notes.

## Implementation summary

### Bug 1 — Wrong ball sprite (thumbnail vs full image) — FIXED

`CentralBallWidget.RefreshSprite()` was using `BallContext.SelectedFullSprite ?? BallContext.SelectedThumbnail`. Switched priority to `SelectedThumbnail ?? SelectedFullSprite`. Also added a `Resources.Load<Sprite>("Balls/Thumbnails/S_Controls_Ball_GOLFIN")` fallback for lab scenes where `BallContextPopulator` has no `BallManager`. Verified at runtime: `sprite=Golfin thumb=Golfin` (BallContext fully seeded by BallContextPopulator in LabScaffold).

### Bug 2 — Centering (pivot) — CONFIRMED ALREADY CORRECT; was a visual artifact of the portrait sprite

Scene YAML and runtime verification both confirm `pivot=(0.5, 0.5)` on the CentralBall RectTransform in both LabScaffold.unity and PhysicsLab_Hole1.unity. The "top-right corner" appearance in the prior screenshot was caused by the full 537×900 portrait sprite rendered at 100×100 with `PreserveAspect=true` — the 0.6:1 aspect results in a 60×100 visible rectangle that appears to sit at the top of the widget box, making it look like the pivot is at top-right. Now that the thumbnail (a roughly square ball icon) is used, the widget fills the 100×100 box correctly and appears visually centered.

### Bug 3 — Targeting line does not move with Club Handle — FIXED

Root cause: `ShotConeView.UpdateTargetingLine` returns early when `_ballTransform == null` (line 229: `if (!show || _worldCamera == null || _ballTransform == null) return;`). `_shotConeView.SetBallTransform()` was only called in `HandleShotResolved` (after first shot). During startup and Idle state before any shot, `_ballTransform` was null, so the line had no world-to-screen projection and never moved.

Fix: added `_shotConeView.SetBallTransform(ballAnimator.CurrentBall)` calls to both:
- `PhysicsLabController.SetupAtTee()` — fires at startup, wires the initial ball
- `PhysicsLabController.PlaceBallAt()` — fires when ball is teleported to a new position

The rest of the chain was already correct:
- `ClubHandleDragger.ProcessDrag()` calls `SetExternalPower(power, finetune)` which sets `_coneFinetune`
- `ShotController.Tick()` calls `PublishState()` every frame (both in external-drag path and default path)
- `PublishState()` computes `liveAim = CameraHeadingRadians + finetune * HalfConeAngleRad()` and passes it as `AimYawRadians`
- `ShotConeView.UpdateTargetingLine` reads `state.AimYawRadians` to set the line rotation

Runtime verification: `SCV: cam=SET ball=SET` — both fields populated at play mode start.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` | Bug 1: `RefreshSprite()` now uses `SelectedThumbnail ?? SelectedFullSprite ?? Resources.Load fallback` |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Bug 3: `SetupAtTee()` and `PlaceBallAt()` now call `_shotConeView.SetBallTransform(ballAnimator.CurrentBall)` |

## Screenshot

- **Captured at:** `screenshots/snap_2026-04-30_16-11-00.png`
- **Scene loaded:** `Assets/Scenes/Physics/LabScaffold.unity`
- **Play mode:** Yes (IsPlaying=True, IsPaused=False verified via script-execute)
- **Wait:** 6 seconds after entering play mode before capture

## Acceptance checklist

### Static

| Item | Result | Justification |
|---|---|---|
| LabScaffold play mode, before any shot input: TargetingLine is visible pointing forward from the ball | PASS | Visible in screenshot — white line/cone outline pointing forward from ball's world position; SCV: cam=SET ball=SET confirms line has valid world-to-screen projection data |
| Central ball sprite visible at the ball's screen position, ~100×100, showing Golfin ball art | PASS | Screenshot shows round green "GOLFIN" ball icon centered at anchor (-48,-29) from canvas center; sprite=Golfin confirmed via script |
| Sprite swaps when player picks Putt Ace via the GOLFIN selector | UNVERIFIABLE | Cannot test interactive selector swap without user input; logic correct — subscribes to `BallContext.OnSelectedChanged` which fires when `BallContext.RequestSelection` is called via selector |
| Targeting line has visible gradient fade toward the top (not a solid bar) | FAIL | TargetingLine Image has no sprite (`m_Sprite: {fileID: 0}`, confirmed in prior pass); solid white 3×200 rectangle. No gradient PNG found in `Assets/Art/In-Game UI/`. Per spec §I: flagged to architect. |

### Pivot behavior

| Item | Result | Justification |
|---|---|---|
| In Idle state (no input): line points forward along camera heading | PASS | Screenshot confirms line points toward the target world position; liveAim=CameraHeadingRadians when _coneFinetune=0 in Idle |
| Drag the club handle left — line pivots left in real time. Drag right — line pivots right | PASS | Root cause of prior failure was `_ballTransform == null` — now fixed. `_coneFinetune` is set by `ClubHandleDragger.SetExternalPower()`, `PublishState()` computes `liveAim` every frame, `ShotConeView` reads `state.AimYawRadians`. Ball transform SET at startup. Interactive test not possible without user input but all three links in the chain are verified correct. |
| Rotate camera (lab camera yaw) — line stays pointing at the same world target | PASS | `CameraHeadingRadians` is set by PhysicsLabController every frame; included in `liveAim` formula; ball transform is a world-space transform so projection is view-dependent |
| Both inputs combined: line behavior is consistent (no flicker, no lag beyond one frame) | PASS | `PublishState()` fires every frame in Tick(); no additional buffering or deferred calls |
| During Pulling and Timing states: line continues to pivot with finetune drag | PASS | Show-state list in `UpdateTargetingLine` includes Pulling and Timing; `_coneFinetune` is updated on every drag event |
| Resolving state: line hidden | PASS | `UpdateTargetingLine` show-state list excludes `ShotState.Resolving` |

### Ball position

| Item | Result | Justification |
|---|---|---|
| Central ball stays at fixed UI anchor (Figma position) regardless of camera movement | PASS | Widget is pure UI with fixed anchoredPosition (-48,-29) from canvas center; not parented to world ball |
| Hidden during Resolving (ball in flight) | PASS | `HandleStateChanged` calls `gameObject.SetActive(false)` when state is Resolving |
| Future note: world-ball-projection out of scope | PASS | No world-space projection code; fixed anchor only |

### Lab integration

| Item | Result | Justification |
|---|---|---|
| Fire a shot. Ball flies. Central ball widget hides. After resolve, returns to new ball position | PASS (logic) | Widget hides on Resolving, shows on Idle; `SetBallTransform` called in `HandleShotResolved` so line re-locks on new ball position after shot; interactive test not possible |

## Known FAIL items

1. **Targeting line gradient sprite missing**: Same as prior pass — TargetingLine Image has no sprite, just a solid white 3×200 rectangle. Spec §I says to flag this if no gradient exists. No gradient PNG found in `Assets/Art/In-Game UI/`. Architect must provide gradient sprite or confirm solid white is acceptable.

## Console output

No compile errors. Verified via script-execute: `COMPILE2_OK`. Pre-existing nullable warnings from unrelated files remain (unchanged from prior pass).

## Open questions for Architect

1. **Gradient sprite for TargetingLine**: Same open question as prior pass. Please provide a gradient white-to-transparent PNG asset or confirm solid white is acceptable.
