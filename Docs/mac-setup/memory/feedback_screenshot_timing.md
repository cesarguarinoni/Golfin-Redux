---
name: Unity play-mode screenshot procedure
description: Correct order of operations to capture a meaningful play-mode screenshot in GolfinRedux
type: feedback
originSessionId: cbfc7dcb-7cae-48be-bf2e-bb4361c50c2a
---
Always take Unity play-mode screenshots in this exact order:

1. Verify `IsPlaying=true, IsPaused=false` before doing anything
2. Trigger the game state via script-execute (e.g. `BeginExternalDrag` + `SetExternalPower`)
3. `sleep 1` — real wall-clock time so Unity renders the state (do NOT manually tick ShotController; that doesn't advance rendering)
4. `editor-application-set-state isPaused=true` — freezes the rendered frame without releasing the handle or changing game state
5. `ScreenCapture.CaptureScreenshot(path)` via script-execute
6. `sleep 2` — let Unity finish writing the PNG to disk
7. Compress and read

**Why:** Screenshots taken without the pause step capture mid-render or pre-render frames. Manual `Tick()` calls advance game logic but do NOT cause Unity to render a new frame. The pause locks the last fully rendered frame so the screenshot is faithful.

**How to apply:** Any time a screenshot is needed during play mode in this project, follow this exact 7-step sequence. Never skip step 3 (sleep) or step 4 (pause).
