# STATUS — `loop_v1_2d_hole_complete_and_result_screen`

## Pipeline state

`SPEC_READY` — Architect locked SPEC + FIGMA_EXTRACT 2026-05-09 07:15 JST. Cesar fires:

```
Use the golfin-implementer subagent on "loop_v1_2d_hole_complete_and_result_screen"
```

## History

- **2026-05-09 06:58 JST** — Architect drafted initial SPEC.md (pre-Figma) under Queued/. 7 locks pending.
- **2026-05-09 07:00 JST** — Cesar confirmed Q1=Yes (canonical), Q2=Failed-on-over-par, Q3-Q5=lean, Q6 explanation accepted, Q7=leave-as-is + cosmetic-pass note. Added debug-button request.
- **2026-05-09 07:05 JST** — Architect extracted node 12987-4556. **Discovered** it is the in-game "BOGEY" banner overlay (NOT the result screen). Surfaced mismatch.
- **2026-05-09 07:08 JST** — Cesar provided 4 correct frame URLs: 12988-5223, 12988-4902, 12988-5466, 12987-4316.
- **2026-05-09 07:10 JST** — Architect extracted all 4 nodes. Discovered the design is a full-screen 2-card layout (current hole + next hole), not a small modal. Variant matrix has 3 functional states keyed off `(isFailed, hasPersonalBest)`. Surfaced scope mismatch + recommended cuts.
- **2026-05-09 07:13 JST** — Cesar confirmed full design fidelity (no design alterations), placeholder for missing data, top bar / nav bar excluded from LabScaffold (Q3), Failed→RETRY / Success→REPLAY for default no-PB.
- **2026-05-09 07:15 JST** — Architect rewrote SPEC.md with full design, wrote FIGMA_EXTRACT.md companion, added Q8 (no-PB default lean confirmed). Set SPEC_READY.

## Locks (all confirmed)

- **Q1** ✅ Figma nodes 12988-5223 / 12988-4902 / 12988-5466 / 12987-4316 are canonical.
- **Q2** ✅ Failed = score > 0. Bogey/double-bogey "pass with lesser rewards" → Loop v2.
- **Q3** ✅ Constructor-inject pin into `RealCupDetector`.
- **Q4** ✅ XZ + height-guarded cup detection. First-sample wins.
- **Q5** ✅ Buttons close + re-arm in §2d.
- **Q6** ✅ HandleShotComplete gates re-arm on AtRest/OB only.
- **Q7** ✅ HoleSessionDriver turn-advance unchanged in §2d. Cosmetic-pass TODO logged.
- **Q8** ✅ §2d default = no PB. Failed → RETRY + Card 2 LOCKED. Widget API exposes `bool hasPersonalBest` for §2e/save-layer pivot.
- **Q3 (top bar / nav bar)** ✅ Excluded from LabScaffold; structure prepares for full-impl drop-in.

## Files this task will touch

- `Assets/Scripts/Gameplay/Loop/RealCupDetector.cs` (NEW, ~50 lines)
- `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` (NEW, ~120 lines)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` (NEW, ~70 lines)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteCardWidget.cs` (NEW, ~180 lines)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteData.cs` (NEW struct, ~80 lines)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (~10 lines added: 2 SetCupDetector sites, HandleShotComplete gate, new RearmAfterHoleComplete accessor)
- `Assets/Scripts/Gameplay/UI/ShotUI/DebugShotPanel.cs` (~10 lines added: _holeOutBtn field + handler + onClick wiring + comment update)
- `Assets/Scripts/Physics/Tests/RealCupDetectorTests.cs` (NEW, 5 tests)
- `Assets/Scripts/Physics/Tests/HoleCompleteDriverTests.cs` (NEW, 4 tests)
- `Assets/Scenes/LabScaffold.unity` (Editor MCP component-add for HoleCompleteDriver + HoleCompleteWidget hierarchy + DebugShotPanel HoleOutBtn child)
- `Assets/Art/ResultScreen/Placeholders/` (NEW — implementer creates simple placeholder images for map/lock/separator/darken/rewards icons)

## Subagent kickoff

```
Use the golfin-implementer subagent on "loop_v1_2d_hole_complete_and_result_screen"
```
