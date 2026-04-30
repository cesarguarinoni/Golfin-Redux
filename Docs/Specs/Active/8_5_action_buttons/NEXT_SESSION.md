# Next session kickoff — 2026-04-30

## State at handoff (2026-04-29 evening JST)

- Code is mid-implementation on `8_5_action_buttons` (Tier 3 pipeline).
- SPEC.md, STATUS.md, IMPLEMENTER_REPORT.md, SELF_REVIEW.md, ARCHITECT_REVIEW.md scaffolded in `Docs/Specs/Active/8_5_action_buttons/`.
- TellCode A.5 line updated to point at the per-task folder.
- Cesar will review Code's work tomorrow morning.

## Tomorrow's order of business

1. **Review Code's 8.5 output.** Read `IMPLEMENTER_REPORT.md` + screenshots first. Check what passed and what didn't.
2. **Finish Selectors** — they aren't working right now. Diagnose first (input wiring? populator empty list? overlay anchor?), then fix.
3. **Tie selector to lab test clubs.** Lab currently has 4 hardcoded clubs in `PhysicsLabController.LabClubs[]` (Driver / Iron 7 / Wedge / Putter). For 8.5 we want the inventory selector to show: 1 Driver, 1 Iron, 1 Wood (currently MISSING — needs to be added), 1 Putter. Either:
   - Seed the inventory CSV with these 4 clubs in the equipped bag, OR
   - Have `ClubContextPopulator.Refresh()` synthesize a 4-entry list when no real inventory exists in LabScaffold.
   - Decide which after seeing what Code shipped.
4. **Finish Spin picker.** Polish the placeholder — confirm the 5-position dot reads/writes `SpinContext.Spin` correctly, big ball uses `BallContext.SelectedFullSprite`, dim background closes.
5. **Add the central ball** in the shot scene + **direction line reacts to club handle movement.** This is new scope — central ball at the tee position, direction line (the existing aim cone trail?) rotates as the club handle / aim input moves. Cesar to clarify exact interaction at session start.

## Files to read first

- `Docs/Specs/Active/8_5_action_buttons/IMPLEMENTER_REPORT.md` — what Code said it built
- `Docs/Specs/Active/8_5_action_buttons/SPEC.md` — the spec
- `Docs/Specs/Active/8_5_action_buttons/screenshots/` — visual diff
- `Docs/TellCode.md` — confirm A.5 line still says SPEC_READY (or DONE if Code closed it)
- `Docs/Architecture/RUNTIME_BLUEPRINT.md` §1 (UI coords) and §7 (ShotUI hierarchy)
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — LabClubs[] for the test-club seeding question
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerContext.cs` + `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs` — pattern reference

## Open API verifications from spec

- `ClubDatabaseCSV.GetClub(string)` — confirm signature
- `BallDatabaseCSV.GetBall(string)` — confirm signature

If either differs from spec, populator code in Code's output likely needs a one-line fix.

## Standing rules (from memory)

- 1 Figma px = 1 Unity unit at 1170 ref
- Anchor convention matters — verify in builder when reading widget coords
- Asset-side fixes beat code-side compensations
- Three-tier classification: 8.5 was Tier 3. Whatever comes next, classify out loud before starting.

## Tool playbook (locked 2026-04-29)

- **filesystem** = repo reads/writes (default)
- **Desktop Commander** = content search only
- **Windows-MCP:PowerShell** = fallback for tricky writes
- **Figma** = design context

Cesar is switching the "Always Load" tool setting to keep these warm across sessions.

## Kickoff message for fresh chat

```
Picking up GOLFIN Redux 8.5 review. Code worked overnight on the action buttons + selectors — read Docs/Specs/Active/8_5_action_buttons/IMPLEMENTER_REPORT.md and screenshots/ first.

Today's order:
1. Review Code's 8.5 output
2. Fix selectors (not working)
3. Tie selector to lab test clubs (1 Driver, 1 Iron, 1 Wood-MISSING, 1 Putter)
4. Finish Spin picker
5. Add central ball + direction line reacts to club handle movement

Read NEXT_SESSION.md in the spec folder for the full handoff.
```
