---
name: Check play/pause state before screenshots
description: Always verify Unity is playing AND not paused before taking screenshots
type: feedback
originSessionId: 951b3430-56ee-43b2-9e3f-290ca0b8a2c9
---
Always call `editor-application-get-state` before capturing a screenshot and verify both `IsPlaying: true` AND `IsPaused: false`. A paused game view renders the last frame before pause — dynamic content (ball sprites, context-driven icons) won't load.

**Why:** Took a screenshot while paused; ball thumbnail showed as white because the widget's Resources.Load hadn't run yet. Cesar caught it.

**How to apply:** At the top of any screenshot workflow, run the state check. If `IsPlaying=false`, enter play mode via `EditorApplication.isPlaying = true` (script-execute). If `IsPaused=true`, unpause before capturing.
