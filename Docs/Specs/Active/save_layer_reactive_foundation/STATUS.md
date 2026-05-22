# STATUS — `save_layer_reactive_foundation`

| Field | Value |
|---|---|
| Current state | **PIPELINE_READY** |
| Created | 2026-05-22 ~13:00 CEST |
| Q-locks recorded | 2026-05-22 ~14:30 CEST |
| Best-practice patches | 2026-05-22 ~14:35 CEST |
| Architect | claude.ai |
| Implementer | Claude Code |
| Pipeline | FULL PIPELINE |

## Timeline

- **2026-05-22 13:00 CEST** — Pre-flight + SPEC authored. Audit of 5 state-holders complete.
- **2026-05-22 14:30 CEST** — Cesar locked Q1 (single), Q2 (fail-hard), Q3 (debounced).
- **2026-05-22 14:35 CEST** — Best-practice scan run. Three additive patches landed:
  - Atomic file writes via temp + `File.Replace` (non-negotiable; prevents save corruption on power loss)
  - Async I/O via `File.WriteAllTextAsync` (prevents mobile frame hitches)
  - Newtonsoft.Json over JsonUtility (native Dictionary support, ships with Unity 6)
- **2026-05-22 — TBD** — Kickoff fired to Code.

## Q-locks (§4 of SPEC)

- Q1 — Single slot for v1
- Q2 — Fail-hard on schema bump
- Q3 — Debounced 250ms writes

## Notes

- Stage E REPLAY fix (`OnReplay → MarkHolePlayed`) currently writes to in-memory HoleProgressionService dict that resets on app restart. This SPEC closes that gap.
- New asmdef `Golfin.Save` keeps the save layer testable and lets future cloud persister live alongside without circular references.
- Migration from existing `GOLFIN_REWARD_POINTS` PlayerPrefs key is one-time and stripped after first run.
