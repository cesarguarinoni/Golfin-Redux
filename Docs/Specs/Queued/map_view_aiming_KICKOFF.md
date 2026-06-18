# KICKOFF — `map_view_aiming` (Order 352)

> Status: **SCOPING** — render-source decision locked, rest open. NO SPEC yet, do NOT implement.
> Resume in a fresh conversation: run the session-start protocol, finish the open forks, then write the SPEC into `Docs/Specs/Active/map_view_aiming/`.

## Decision LOCKED (Cesar, this session)
- **Render source = B: live ORTHOGRAPHIC top-down render of the hole geometry → RenderTexture.**
- Rejected A (scale the static per-hole PNG): needs per-hole image↔world calibration (×18) and markers drift — unacceptable on an aiming screen.
- Reuse pattern: `HoleFlyoverRecorder` already spawns a 2nd camera (depth 10, renders on top, fits to renderer/green bounds). Adapt perspective→ORTHO, point top-down, render to RT.

## Anchors (verified this session)
- Entry: `Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs` — tap the Hole Map. Current map = static sprite array `_holeMaps[18]` = `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {i}.png`. The live RT replaces/augments this.
- Camera pattern to adapt: `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs` (2nd cam, bounds-fit, render-on-top).
- Live hole geometry: `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs` (HoleGeo scenes) + `Assets/Scripts/Course/Runtime/GreenTopology.cs`; geometry is loaded in the gameplay scene → renderable by a 2nd ortho cam.
- World→screen aim projection: reuse the just-shipped fade/draw arc — `ShotConeView` (targeting line, ball→screen/target→screen projection) + `ShotController` (AimYawRadians, ConeFinetuneX). Aim on the map projects through the ortho cam's matrix.

## OPEN — resolve during scoping BEFORE the SPEC
1. **Aim interaction** on the map: drag-to-aim, read-only overview, or tap-to-aim?
2. **Markers:** ball / flag / hazards / landing zone — which are shown?
3. **Zoom / pan** behaviour?
4. **1v1 behaviour:** show the opponent? per-player view?
5. **Figma node** for the full-screen map screen — NEEDED. Get the node-id from Cesar, or confirm no design exists (then intent-driven, like the spin selector was). File key `5gEAHjl6xAtW8iYY7NMvWd`.

## Tier
FULL PIPELINE (Tier 3) — visual fidelity + runtime world→screen aim projection.

## Session-start checklist (this project)
- `cd /Users/cesar/Documents/GolfinRedux`; `git pull` — NOTE: pull HUNG on network at end of last session; retry / check connection. (Was working on local HEAD.)
- Read `Docs/AI_CONTEXT.md` headline + `Docs/TellCode.md` pointer block.
- Read `Docs/Reference/Figma_Lessons.md` BEFORE any Figma work (sandbox has NO outbound network for image fetch — delegate image pulls to Code).
- Notion GOLFIN_Roadmap data source `364b3e97-02b7-8190-b82b-000ba7847856`; Order 352 page `37cb3e97-02b7-81e6-9c5b-dff6dc1d080a` (P2, Queued).
- Scope before spec. Figma `get_metadata` + `get_design_context` (clientFrameworks=unity, clientLanguages=csharp, excludeScreenshot=true). Surface forks before speccing. Classify tier out loud.
- Visual/capture gate lessons from the just-finished arc: capture over a REAL loaded hole (never LabScaffold); check ABSOLUTE bounds at full res 1170×2532; no look-regression; normal play + real UI toggle + NO camera-fighting (no Downrange/overhead to force a view); lock camera before recording.

## Carryover / housekeeping
- **Cesar to manually delete** the `🗑️ DELETE ME — duplicate of Order 340` Notion page (id `366b3e97-02b7-815c-aeb8-efba1d04f486`) — the connected Notion tools cannot delete/trash pages (create/update/move only).
- Fade/draw + spin arc DONE: 354 `spin_selector_ux`, 356 `fade_draw_core_wiring`, 355 `fade_draw_aim_line_bend`. Cesar to human-confirm fade/draw FEEL now that the bent aim line is on screen (the feel-check deferred from 356).
