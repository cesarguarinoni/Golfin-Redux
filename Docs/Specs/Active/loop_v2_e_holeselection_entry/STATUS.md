# STATUS — `loop_v2_e_holeselection_entry`

| Field | Value |
|---|---|
| Current state | **PART_A_SHIPPED / PART_B_SPEC_READY** |
| Created | 2026-05-22 ~09:00 CET |
| Architect | claude.ai |
| Implementer | Claude Code (Part B only) |
| Pipeline (Part A) | SURGICAL — shipped by Architect |
| Pipeline (Part B) | TELLCODE |

## Timeline

- **2026-05-22 ~08:50 CET** — Pre-flight done. Stage E wiring confirmed clean; one architectural gap surfaced (REPLAY → no progression/rewards write).
- **2026-05-22 ~09:00 CET** — Cesar confirmed (B): REPLAY must write progression + grant rewards so Hole 2 unlocks after a first-clear-into-REPLAY.
- **2026-05-22 ~09:05 CET** — Architect applied Part A surgical fix (`OnReplay` adds `WriteProgressionIfSuccess()` + `GrantRewards()`), added two regression tests (`Modal_ReplayOnSuccessWritesProgression`, `Modal_ReplayOnFailedDoesNotWriteProgression`), updated controller doc-comment.
- **2026-05-22 — TBD** — Part B SPEC handed off to Code via TELLCODE.

## Part A change-set (for git scoping)

- `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs`
- `Assets/Scripts/Gameplay/Tests/HoleCompleteModal/HoleCompleteModalControllerTests.cs`

## Part B change-set (anticipated)

- `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` (add coroutine)
- `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` (add dispatch case)
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` (add MenuItem)

## Open notes

- Hole 1's `rewards` vs `replayRewards` in the CSV may be identical; if so, Implementer should flag and Cesar will either pick a different hole for the visual gate OR adjust the CSV.
- The hole-card action button's GO name needs prefab inspection (Implementer's first step).
