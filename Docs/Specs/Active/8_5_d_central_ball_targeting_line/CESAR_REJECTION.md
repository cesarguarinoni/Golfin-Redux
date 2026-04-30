# Cesar Rejection #2 — 8_5_d_central_ball_targeting_line

**Date:** 2026-04-30
**Status before rejection:** READY_FOR_ARCHITECT_REVIEW

## Two bugs still present in screenshot

### 1. Ball widget still not centered

The CentralBall widget is visually off-center. The implementer claimed pivot=(0.5,0.5) was already correct and blamed the full-sprite aspect ratio — but the problem persists even with the thumbnail.

**Diagnose properly this time:**
- In play mode, use script-execute to read ALL of: pivot, anchorMin, anchorMax, anchoredPosition, sizeDelta, AND the Image component's preserveAspect setting.
- Also check whether a parent Canvas Scaler is affecting the coordinate space. If the Canvas uses "Scale With Screen Size" with a reference resolution different from 1170×2532, the anchoredPosition (-48, -29) computed from Figma will land in the wrong place.
- Check if the CentralBall GO has any Layout component (LayoutElement, ContentSizeFitter) that might be overriding size/position.
- Fix whatever is actually causing the offset — do NOT just declare it correct because the pivot field reads (0.5, 0.5).

### 2. Targeting line is in the top-right corner, not on the ball

The targeting line is rendering in the top-right corner of the screen instead of being anchored to the ball's screen position. Before this task it was absent (null ballTransform → early return). Now it appears but in the wrong place.

**Root cause to investigate:**
- `SetupAtTee()` now calls `_shotConeView.SetBallTransform(ballAnimator.CurrentBall)`. What IS `ballAnimator.CurrentBall`? Read `PhysicsLabController` and the ball animator to confirm `CurrentBall` returns the Transform of the actual ball GameObject sitting on the tee — not a parent, not a container, not an animated rig root.
- Read `ShotConeView.UpdateTargetingLine` in full. How does it convert world position to UI position? Specifically: what camera does it use, and what canvas does it place the line in? There are two canvases visible in the screenshot (the HUD canvas with the ball widget, and possibly a world-space canvas for the cone). Confirm the line is being positioned in the correct canvas with the correct camera.
- Run script-execute in play mode to print: the ball world position, the result of `Camera.WorldToScreenPoint(ballPos)`, and the targeting line's current anchoredPosition — compare these three values to understand where the mismatch is.
- Fix the coordinate conversion so the line base sits on the ball's screen position.
