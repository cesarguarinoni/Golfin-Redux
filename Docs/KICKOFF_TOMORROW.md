# Kickoff for next session

> **Last session:** 2026-05-08 ended with §2c shipped + camera regression closed. This file is what to read when opening a fresh chat tomorrow.

## State at end-of-session 2026-05-08

**Loop v1 progress:**
- §2a Ball state machine ✅ closed 2026-05-06
- §2b Camera transitions ✅ closed 2026-05-07
- §2c Turn counter + shot history ✅ closed 2026-05-08
- §2d Hole-complete detection + result screen ⬅ NEXT
- §2e "Next shot" handoff
- §2f Putter Phase 2: in-context tuning

**Camera regression saga closed:** controls_h iter-8 partial revert shipped 2026-05-08. Camera works as it did pre-§2b. Apex zoom-out was researched, deemed off-pattern vs shipped golf games (PGA TOUR/2K23/TV broadcast all use cuts, not continuous zooms), explicitly rejected. See `Docs/Game Design/CAMERA_SYSTEM_FUTURE_DESIGN.md` § Research note 2026-05-08 for full findings.

**Active spec folder:** empty (`Docs/Specs/Active/` has only `_TEMPLATE`).

**Forward-flagged polish items** (none on Loop v1 critical path):
- `controls_i_ball_visual_rotation` (Phase 10 Polish, Order 260, Deferred) — ball slides instead of rolls. NOTES at `Docs/Specs/Queued/controls_i_ball_visual_rotation/NOTES.md`. Cesar locked Option A first.
- OBFreeze camera framing question — visible-water deferred per TellCode flag.
- HUD ClubContext static-bus drift — triage before §2f per TellCode flag.
- Camera System Future Design doc at `Docs/Game Design/CAMERA_SYSTEM_FUTURE_DESIGN.md` is the must-read for any future camera mode.

## §2d — what tomorrow's task is

**One-liner:** ship a real `ICupDetector` impl, wire it into `PhysicsLabController.OnHoleLoaded`, and add a minimal result screen that fires on `OnShotComplete(terminal=InCup)` showing strokes / par / score-to-par.

**Why §2d is small:** the foundation is all stubbed. Verified 2026-05-08 by Architect code walk:
- `Assets/Scripts/Gameplay/Loop/ICupDetector.cs` interface exists
- `NullCupDetector.cs` is the current stub (always returns false)
- `BallStateMachine.SetCupDetector(ICupDetector)` is the runtime swap-in
- `BallStateMachine.cs:184` already calls `_cupDetector.IsInCup(sample.position, ballRadius)` during Rolling — when it returns true, terminal becomes `InCup` automatically
- §2c's `ShotRecord` already has `TerminalState` field — ShotHistory records InCup shots automatically
- `HoleContext.PinWorld` is already populated in `PhysicsLabController.OnHoleLoaded` (line ~1230 reads Flag GO position)

**§2d scope (estimated half-day to one day):**
1. **Real CupDetector** — class implementing `ICupDetector.IsInCup(fp3 position, fp ballRadius)`. Reads pin position via constructor injection (or static `HoleContext.PinWorld`). Returns true if `XZ distance from position to pin < cupRadius - ballRadius` AND `position.y < pin.y + cupDepth`. Cup radius is regulation 54mm (0.054m); cup depth is at least 4 inches (0.1016m). Architectural note: detector reads in fp3, so values pass through `fp.FromFloat` once at construction.
2. **Wire on hole load** — `PhysicsLabController.OnHoleLoaded`, after `HoleContext.Raise()` and `GameSession.ResetForNewHole()`, call `_ballSM.SetCupDetector(new RealCupDetector(HoleContext.PinWorld, ...))`.
3. **Result screen UI** — minimal modal/overlay. Shows: hole number, par, strokes (= `GameSession.ShotHistory.Count`), score-to-par (= strokes - par formatted as "+1" / "−1" / "Par"). One "Continue" button that hides the modal. NOT a polished UI — just the data.
4. **Fire result screen** — new MonoBehaviour subscribed to `BallStateMachine.OnShotComplete`. On terminal=InCup, show modal. Lives in `Golfin.Physics.Viewer` or `Golfin.Gameplay.UI.ShotUI`, mirroring `HoleSessionDriver` (§2c) pattern. Probably named `HoleCompleteDriver`.
5. **Tests** — 4-6 EditMode tests:
   - `RealCupDetector_BallInsideCup_ReturnsTrue`
   - `RealCupDetector_BallOutsideCupRadius_ReturnsFalse`
   - `RealCupDetector_BallAboveCup_ReturnsFalse`
   - `RealCupDetector_BallAtCupEdge_ConsidersBallRadius`
   - `HoleCompleteDriver_OnInCupTerminal_ShowsModal`
   - `HoleCompleteDriver_OnAtRestTerminal_DoesNotShowModal`
6. **Manual verification per Lesson O** — fire a putter shot on Hole 1 close to cup, verify InCup terminal fires, modal appears with correct strokes/par.

**Open questions for SPEC-lock time:**
- Where does `PinWorld` live for the detector — constructor inject (immutable) or static read (live-updates if pin moves)? Lean: constructor inject; pin doesn't move during a hole.
- Cup detection: instantaneous (any sample inside the cup) or sustained (ball must rest inside)? §2a's existing code samples during Rolling so any-sample-inside is the current contract. Lean: keep as-is.
- Result modal — bare HUD overlay or proper modal pattern with `ModalController`? Lean: minimal HUD overlay first, polish in §3c.
- Continue button next action — fire `BallStateMachine.ReArm()`? Move to next hole? Just hide the modal? Lean: just hide modal for §2d scope; §3d handles inter-hole flow.
- Test seam for `HoleCompleteDriver` — same as §2c's HoleSessionDriver pattern (`InjectForTests` helper).

**Files §2d will touch:**
- `Assets/Scripts/Gameplay/Loop/RealCupDetector.cs` (NEW, ~30 lines)
- `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` (NEW, ~50 lines)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (~3 line addition in OnHoleLoaded)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` (NEW, ~80 lines for the simple modal)
- LabScaffold.unity (Unity Editor MCP component-add for HoleCompleteDriver + HoleCompleteWidget GO)
- Tests new file ~50 lines / 6 tests

**Test gate:** baseline + 6 new tests. Implementer confirms baseline first.

## Kickoff line for tomorrow

When ready to start, the architect can spec §2d and Cesar fires:

```
Use the golfin-implementer subagent on "loop_v1_2d_hole_complete_and_result_screen"
```

But the spec doesn't exist yet. Tomorrow morning, first thing for the architect is:

1. Read this file
2. Read `Docs/Specs/Active/_TEMPLATE/SPEC.md` for format
3. Read `Docs/Specs/Completed/loop_v1_2c_turn_counter_and_shot_history/SPEC.md` for the pattern this task mirrors
4. Write `Docs/Specs/Queued/loop_v1_2d_hole_complete_and_result_screen/SPEC.md`
5. Move to Active, create Notion entry, update TellCode pointer
6. Cesar confirms locks on the 5 open questions, then fires kickoff

That's the first ~30 minutes of tomorrow's session. After that, implementer pipeline runs.

## Personal note for Cesar

Today was exhausting. The camera saga was the kind of session that drains every reserve and leaves you questioning the workflow itself. But you ended with:
- §2c shipped clean
- Camera back to working state
- Three durable assets in the repo (HandleShotResolved order fix, Pipeline Lesson O, Camera System Future Design doc)
- Solid research that prevents the next round of camera mistakes

The day wasn't zero-net even though it felt that way. Tomorrow you start with all your tools intact and §2d is the smallest task in the Loop v1 cluster. Wake up, coffee, fire the architect, and you'll close §2d before lunch.

Sleep well.
