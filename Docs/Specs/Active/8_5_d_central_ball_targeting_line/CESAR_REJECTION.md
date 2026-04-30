# Cesar Rejection — 8_5_d_central_ball_targeting_line

**Date:** 2026-04-30
**Status before rejection:** READY_FOR_ARCHITECT_REVIEW

## Three bugs to fix before review

### 1. Wrong ball sprite — use thumbnail, not full image

`CentralBallWidget` is using `BallContext.SelectedFullSprite` (which is the full portrait/card image, e.g. 537×900px). At 100×100 it renders wrong.

Use the thumbnail instead: `Assets/Resources/Balls/Thumbnails/`. Check `BallContext` for a thumbnail property, OR load the thumbnail sprite from `Resources.Load<Sprite>("Balls/Thumbnails/{ballId}")`. The thumbnail should be a small round/ball-sized icon, not the full art card.

In the screenshot the ball widget appears as a blurry landscape card snippet — it should look like a small golf ball icon.

### 2. Wrong centering — pivot is at top-right corner, not center

In the screenshot the ball image's top-right corner lands at the Figma anchor point, meaning the pivot or the anchor math is wrong. The widget should be centered at anchoredPosition (-48, -29).

Fix: ensure the `RectTransform` pivot is exactly (0.5, 0.5) AND the Image component's pivot is (0.5, 0.5). When the implementer wired the scene GO via YAML, the pivot may have defaulted to (0, 1) or similar. Read the current pivot values from the scene and correct them.

### 3. Targeting line does not move with Club Handle drag

In play mode the line is completely static — it does not pivot left/right when the Club Handle (the drag UI for aiming) is dragged. This is the critical behavior the spec requires.

Root cause to investigate:
- The implementer assumed `_coneFinetune` drives aim. Verify this is actually what the Club Handle sets. Read `ShotController.cs` to find which field is mutated by handle drag input (could be `_coneFinetune`, `_finetune`, or something else).
- Verify `PublishState()` is actually called every frame during Idle/Aiming states (check if it's only called on state transitions, not in `Tick()`).
- Verify `liveAim` is actually being used in `ShotConeView.UpdateTargetingLine` to set the line rotation — maybe the line reads a different field from `ShotInputState`.

Print-debug or use script-execute to read the live `AimYawRadians` value from a published state while in play mode to confirm it's changing as the handle is dragged.
