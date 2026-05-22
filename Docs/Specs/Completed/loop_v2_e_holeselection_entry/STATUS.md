# STATUS — `loop_v2_e_holeselection_entry`

| Field | Value |
|---|---|
| Current state | **DONE** |
| Created | 2026-05-22 ~09:00 CET |
| Architect | claude.ai |
| Implementer | Claude Code (Part B only) |
| Pipeline (Part A) | SURGICAL — shipped by Architect |
| Pipeline (Part B) | TELLCODE |

## Timeline

- **2026-05-22 ~08:50 CET** — Pre-flight done. Stage E wiring confirmed clean; one architectural gap surfaced (REPLAY → no progression/rewards write).
- **2026-05-22 ~09:00 CET** — Cesar confirmed (B): REPLAY must write progression + grant rewards so Hole 2 unlocks after a first-clear-into-REPLAY.
- **2026-05-22 ~09:05 CET** — Architect applied Part A surgical fix (`OnReplay` adds `WriteProgressionIfSuccess()` + `GrantRewards()`), added two regression tests (`Modal_ReplayOnSuccessWritesProgression`, `Modal_ReplayOnFailedDoesNotWriteProgression`), updated controller doc-comment.
- **2026-05-22 ~09:05 CET** — Part B SPEC handed off to Code via TELLCODE.
- **2026-05-22 ~07:30 CEST** — Part B implemented (commit `dc449bc4`): `HoleSelectionEntryToReplayRewards` scenario + dispatch case + menu item — all 3 files additive `#if UNITY_EDITOR`. Scenario ran clean end-to-end (8 captures, no errors); `ShellScene.unity` diff empty.
- **2026-05-22 ~07:35 CEST** — Part A regression tests run: `HoleCompleteModalControllerTests` 9/9 green, incl. `Modal_ReplayOnSuccessWritesProgression` + `Modal_ReplayOnFailedDoesNotWriteProgression`.
- **2026-05-22 ~07:35 CEST** — Cesar verified the visual gate: `s06` first-clear (x100/x10/x5) vs `s08` replay-clear (x50/x5/x2) — `rewards` vs `replayRewards` pools visibly distinct, no CSV change needed. Stage E **DONE**.

## Part A change-set (for git scoping)

- `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs`
- `Assets/Scripts/Gameplay/Tests/HoleCompleteModal/HoleCompleteModalControllerTests.cs`

## Part B change-set (anticipated)

- `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` (add coroutine)
- `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` (add dispatch case)
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` (add MenuItem)

## Resolved notes

- Hole 1's `rewards` (Points 100 / RepairKit 10 / Ball 5) vs `replayRewards` (Points 50 / RepairKit 5 / Ball 2) are distinct in `HoleDatabase.csv` — visual gate works as-is, no CSV change needed.
- Hole-card action button GO name is `ActionButton` (HoleCard prefab; `actionButton` SerializeField on `HoleCardController`). Only Hole 1's card auto-expands, so it is the single active `Button` by that name — `FindButton` resolves it unambiguously.
