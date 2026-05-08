# Quick: factor `CaptureCore.SnapPlayModeSafe()`

## Why

The §2c task (`loop_v1_2c_turn_counter_and_shot_history`) needed a play-mode-safe synchronous capture helper because:

- It runs inside a long-lived coroutine (`SmokeRunner2cHost.RunSequence`).
- It must call `AssetDatabase.Refresh()` *never* — refreshing while scripts are pending recompile forces Unity to exit play mode and kills the coroutine.
- It needs the absolute path back as a string so the runner can log each capture.

`CaptureCore` already had two capture entry points but neither fit:
- `SnapGameViewWithLabel` returns the path but always calls `AssetDatabase.Refresh()`.
- `SnapAtEndOfFrameAndPause` is play-mode-aware and skips refresh, but it's a coroutine, doesn't return the path, and pauses the editor by default.

So the §2c implementer wrote a private static `SnapPlayMode(label)` inside the runner. The architect-review accepted it but flagged "factor a `CaptureHelper.SnapPlayModeSafe()` so callers stop duplicating it" as a follow-up.

## Scope

1. Add `CaptureCore.SnapPlayModeSafe(string label)` that mirrors the inlined helper:
   - Sync (no coroutine), returns absolute path string.
   - Uses `GrabGameViewRT()` (with `ScreenCapture.CaptureScreenshotAsTexture()` fallback).
   - Uses `Object.Destroy` in play mode, `Object.DestroyImmediate` in edit mode.
   - **Never** calls `AssetDatabase.Refresh()`.
   - Logs the written path.
2. Mirror it on `CaptureHelper.SnapPlayModeSafe(string label)` (thin editor-side passthrough — same rationale as the existing `SnapGameViewWithLabel` / `SnapAtEndOfFrameAndPause` mirrors).
3. Refactor `SmokeRunner2cHost` to call `CaptureCore.SnapPlayModeSafe()` and delete the local helper.
4. Update CLAUDE.md screenshot quick-reference: add a row for the play-mode-coroutine case and a paragraph explaining `SnapPlayModeSafe` vs `SnapAtEndOfFrameAndPause`.

## Out of scope

- No §2b runner exists to refactor — the closest precedent (`Iter4ShotCapture.cs`) already uses the public API `CaptureCore.SnapAtEndOfFrameAndPause(label, skipPause: true)` correctly.
- `MatchmakingCaptureRunner.cs` carries its own private `GrabGameViewRT` reflection clone; not consolidated here. Separate concern, leave alone.

## Files touched

- `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs` — added `SnapPlayModeSafe`.
- `Assets/Scripts/Editor/CaptureHelper.cs` — added thin editor passthrough.
- `Assets/Scripts/Physics/Viewer/SmokeRunner2cHost.cs` — replaced 3 call sites + deleted the local helper.
- `CLAUDE.md` — added a row + paragraph in the screenshot quick-reference.

## Verification

- Unity console: 0 compile errors after `assets-refresh` (Unity stayed in play mode with `IsCompiling: false`).
- No public API changed — `SnapGameViewWithLabel`, `SnapAtEndOfFrameAndPause`, `GrabGameViewRT`, `SnapWhenStateReached`, `SnapWhenModeReached` all unchanged. Only additions.
- `HoleSessionDriverTests` not affected — they exercise `GameSession` and `BuildShotRecordStatic`, not the smoke runner.

## Status

Done — awaiting Cesar's eyeball.
