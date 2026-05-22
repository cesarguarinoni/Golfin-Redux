# STATUS — `save_layer_reactive_foundation`

| Field | Value |
|---|---|
| Current state | **SPEC_READY pending Cesar Q-locks (Q1/Q2/Q3 in SPEC §4)** |
| Created | 2026-05-22 ~13:00 CEST |
| Architect | claude.ai |
| Implementer | Claude Code (pending Q-locks) |
| Pipeline | FULL PIPELINE recommended |

## Timeline

- **2026-05-22 13:00 CEST** — Pre-flight + SPEC authored. Audit of 5 state-holders complete; surface decisions locked except Q1/Q2/Q3.

## Open questions

See SPEC §4. Three locks needed before kickoff:
- Q1 — single save slot vs multi-slot (lean: single for v1)
- Q2 — schema-bump fail-hard vs fail-soft (lean: fail-hard)
- Q3 — write trigger granularity (lean: debounced 250ms on every OnChanged)

## Notes

- Stage E REPLAY fix (`OnReplay → MarkHolePlayed`) currently writes to in-memory HoleProgressionService dict that resets on app restart. This SPEC closes that gap.
- New asmdef `Golfin.Save` keeps the save layer testable and lets future cloud persister live alongside without circular references.
- Migration from existing `GOLFIN_REWARD_POINTS` PlayerPrefs key is one-time and stripped after first run.
