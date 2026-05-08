# STATUS — `loop_v1_2d_hole_complete_and_result_screen`

## Pipeline state

`PENDING_LOCKS` — Architect drafted SPEC. Cesar must confirm 7 locks (Q1–Q7) in chat before this advances to `SPEC_READY`. After locks confirmed:
1. Architect runs `Figma:get_design_context` for node `12987-4556`
2. Architect patches §E layout values + §F hierarchy
3. Architect updates SPEC Status timestamp + Locked decisions section
4. Folder moves Queued → Active
5. TellCode pointer updated
6. Cesar fires kickoff:
   ```
   Use the golfin-implementer subagent on "loop_v1_2d_hole_complete_and_result_screen"
   ```

## History

- 2026-05-09 <session-start JST> — Architect drafted SPEC.md to `Docs/Specs/Queued/`. Verified architectural touch-points by code walk: ICupDetector, NullCupDetector, BallStateMachine cup-scan path (lines 166–211), ShotResult struct, PhysicsLabController OnHoleLoaded/OnHoleUnloaded/HandleShotComplete, HoleContext.PinWorld, GameSession (§2c). One factual correction recorded vs KICKOFF_TOMORROW.md: cup detection is a one-shot scan over all trajectory.samples in `OnTrajectoryComputed`'s `default:` branch, not a per-Rolling-tick check.

## Locks pending

See SPEC.md § Locked decisions. All 7 locks (Q1–Q7) read PENDING.

## Files this task will touch

- `Assets/Scripts/Gameplay/Loop/RealCupDetector.cs` (NEW, ~50 lines)
- `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` (NEW, ~80 lines)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` (NEW, ~80 lines)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (~10 lines added across 3 sites: OnHoleLoaded SetCupDetector, OnHoleUnloaded NullCupDetector revert, HandleShotComplete InCup gate, new internal RearmAfterHoleComplete accessor)
- `Assets/Scripts/Physics/Tests/RealCupDetectorTests.cs` (NEW, 5 tests)
- `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` (NEW, 3 tests)
- `Assets/Scenes/LabScaffold.unity` (Editor MCP component-add for HoleCompleteDriver + HoleCompleteWidget GO + UI hierarchy)
