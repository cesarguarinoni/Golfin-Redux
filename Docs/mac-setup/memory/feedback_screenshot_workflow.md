---
name: Screenshot workflow for play-mode verification
description: Correct method for capturing Unity play-mode screenshots showing specific ShotController states
type: feedback
originSessionId: c1b942a2-c56c-476e-97a0-29f4be105a11
---
Use `ScreenshotHelper` (`Assets/Scripts/Debug/ScreenshotCapture/ScreenshotHelper.cs`) for all play-mode state screenshots. Direct `ExecuteMenuItem` calls from `script-execute` capture the previous frame's back buffer and always show the wrong state.

**Why:** `ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` saves whatever was rendered before `script-execute` ran. The state change and screenshot are in the same frame — too early. Also, `TickArrow` resets `ShotController` state after `MaxTotalPasses` (within seconds), so a second `script-execute` call is often too late.

**How to apply:**

```csharp
// Step 1 — add once per play session (re-add if it auto-destroys after each capture)
script-execute: 
  var labRoot = GameObject.Find("LabRoot");
  labRoot.AddComponent<ScreenshotHelper>();

// Step 2 — capture at any power level
script-execute:
  var h = FindFirstObjectByType<ScreenshotHelper>();
  h.Capture(1f);   // 1f = full pull, 0f = tip, 0.5f = mid
```

The coroutine waits `stabiliseFrames` (default 4) frames, re-asserting the power state each frame to guard against TickArrow resets, then fires the GOLFIN screenshot menu. Screenshot appears in `Assets/Screenshots/`.

**Never use:** `EditorApplication.isPaused = true` before screenshotting — the frozen back buffer shows the old state.
