# Memory Index

## Project
- [project_folder.md](project_folder.md) — Working directory path changed from "Golfin Redux" to "GolfinRedux"
- [project_tee_skirt_resolved.md](project_tee_skirt_resolved.md) — Linear-slope ramp fixed tee cliff (2026-04-20); prior radius-based attempts all failed
- [project_scene_ground_provider.md](project_scene_ground_provider.md) — SceneGroundProvider raycasts to top mesh surface; use instead of HeightmapData in Hole1 PhysicsLab
- [bug_water_color_physicslab.md](bug_water_color_physicslab.md) — Water renders gray in PhysicsLab play mode; fix attempt failed

## User
- [user_role.md](user_role.md) — User's name is Cesar; he does all Unity Editor work (prefabs, Inspector wiring)

## Feedback
- [feedback_unity_mcp_available.md](feedback_unity_mcp_available.md) — **Unity MCP tools are ALWAYS available** — never assume they're absent; always attempt the call
- [feedback_mcp_script_execute.md](feedback_mcp_script_execute.md) — Use MCP Skill/stdin directly for script-execute; don't write tmp JSON files
- [feedback_assume_work.md](feedback_assume_work.md) — Always execute tasks when asked to check task files, don't just summarize
- [feedback_uhole_geo_regen.md](feedback_uhole_geo_regen.md) — After editing terrain generation script, tell user to regenerate in UHole Geo before Unity import
- [feedback_session_signoff.md](feedback_session_signoff.md) — End every session with "See you space cowboy"
- [feedback_compile_check.md](feedback_compile_check.md) — Always verify compile via MCP script-execute after every C# edit, before declaring done
- [feedback_check_play_mode.md](feedback_check_play_mode.md) — Always verify IsPlaying=true AND IsPaused=false before screenshots; paused state shows stale frame
- [feedback_screenshot_timing.md](feedback_screenshot_timing.md) — 7-step screenshot procedure: trigger state → sleep 1 → pause editor → capture → sleep 2 → compress → read

## Workflow Rules
- **Push to GitHub after every change** — user requested this explicitly. Always run `git push` after committing.
- **Never use `git checkout -- <file>`** to undo changes — it wipes accumulated fixes. Use `Edit` for surgical reverts.

## Session Progress (last updated 2026-04-22)
- Physics Phases 0–6 ✅ COMPLETE (39/39 tests pass)
- PhysicsLab Hole1 viewer working: buttons fire, zone meshes visible, ball spawns on green
- Surface classification fix: Physics.Runtime.SurfaceMarker on all zone meshes
- Next: re-import all holes; Phase 7 stat modifiers; trees layer fix
- Status doc: `C:\Users\cesar\GolfinRedux\Docs\AI_CONTEXT.md`
- Lessons doc: `C:\Users\cesar\GolfinRedux\tasks\lessons.md`
