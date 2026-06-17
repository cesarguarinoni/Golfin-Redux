# Cesar Rejection — `fade_draw_core_wiring` (Order 356)

Rejected on sight after `ARCHITECT_REVIEW_PASS`. The canonical video `videos/fadedraw_real_hole_gate_iter5.mp4` is NOT a real-playing-conditions capture. Confirmed defects:

1. **Not playing conditions / shot fired from up in the trees.** The `FadeDrawRealHoleGate` scenario forces `ChaseCamera.Mode.Downrange` to a fixed side-elevated position (`FadeDrawSetSideCamera`, `Scenarios.cs:3585`) and re-asserts it every frame during flight (`Scenarios.cs:3563`). That fixed position sits among/points at the forest behind Hole 6's green. On top of that the scenario fires a **Driver (club 0)** on a **168yd par 3**, so the ball sails past the green into the trees. Result: the whole clip is shot against a wall of forest — not how the game plays.

2. **Wrong UI / buttons.** The bot arms fade/draw by setting `ShotController.FadeDrawActive` directly and bypasses the real UI toggle. So at t=1s the on-screen ShotMode button still reads **"STRAIGHT"** while the caption claims "FadeDraw ARMED" — a direct contradiction — and the club button reads **"DRIVER 0 yrds"** (uninitialized lab state). The capture never exercised the real `UI toggle → ShotModeContext → ShotConeView → ShotController` arming path; it shortcut straight to `FadeDrawActive`.

3. **Flipped frame.** Classic `BotVideoRecorder` y-flip: the scenario switches camera mode (Chase ↔ Downrange) around/within the recording window, changing render state after `StartRecording`. Per `reference_botvideorecorder_yflip_fix`, all render/camera state must be locked BEFORE recording starts.

## Root cause (orchestrator error — owned)

The architect/orchestrator (me) steered the pipeline to **fight the camera**. When the self-reviewer reported the normal chase cam "couldn't show the lateral curve," I directed the implementer toward overhead, then fixed side / Downrange framings to force the curve to be visible. That is the exact anti-pattern Cesar's standing rule `feedback_gameplay_video_use_normal_play` forbids (the `tree_collisions` lesson: *"use normal play + normal chase camera; fix the SHOT (low/flat), don't fight the camera (Downrange/podium)"*). The reviewers then graded against "is the curve visible from any angle" instead of "is this a normal-play clip," so the non-gameplay capture sailed through all gates.

The feature code itself is correct and proven (17/17 EditMode tests + the `runtime_wiring_log.txt` CommitFlick values + the trajectory math). The defect is entirely in HOW the gameplay proof was captured.

## Fix (next iteration)

- **Normal play, normal chase camera. No camera-mode switching** (this also removes the y-flip). Use the clean scenario pattern (`Scenarios.cs:1874` "No camera tricks, no Downrange mode, no per-frame camera override").
- **Arm fade/draw through the REAL UI path** (`ShotModeContext.Toggle`) so the on-screen toggle button actually reads the armed state — exercising the full UI→arming chain, not just `FadeDrawActive`.
- **Sensible shot for the hole** so the ball stays in play (don't fire a Driver into the woods on a par 3). Pick a club/hole/aim where the ball lands on/near target and the draw vs fade reads as the ball curving in normal play. Fix the SHOT, not the camera.
- Keep the overlay PNG + runtime log + tests as supporting evidence; the gameplay proof is a normal-play clip.

STATUS → `CESAR_REJECTED`.
