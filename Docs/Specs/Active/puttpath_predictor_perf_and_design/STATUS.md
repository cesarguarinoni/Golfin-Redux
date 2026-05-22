# STATUS — `puttpath_predictor_perf_and_design`

| Field | Value |
|---|---|
| Current state | **SPEC_READY pending Cesar Q-locks (Q1–Q5 in SPEC §4)** |
| Created (NOTES) | 2026-05-07 (Architect initial scoping) |
| Design-locked (NOTES) | 2026-05-13 (Cesar L1/L2/L3) |
| SPEC authored | 2026-05-22 ~13:30 CEST |
| Architect | claude.ai |
| Implementer | Claude Code (pending Q-locks + sequencing after save layer) |
| Pipeline | FULL PIPELINE (visual fidelity + runtime spatial math) |

## Timeline

- **2026-05-07** — Architect initial NOTES.md
- **2026-05-13** — Design-locked by Cesar (L1 Sim positioning / L2 redesign / L3 baked per-region)
- **2026-05-22 13:30 CEST** — SPEC authored. Pre-flight confirmed file locations and APIs; corrected NOTES.md inaccuracies (file lives in Viewer asmdef not UI/HUD; event is OnChanged not OnHoleLoaded).
- **2026-05-22 — moved** — folder relocated `Docs/Specs/Queued/` → `Docs/Specs/Active/` once SPEC landed.

## Open questions

See SPEC §4. Five locks needed before kickoff:
- Q1 — cell size (lean: 0.5m)
- Q2 — color ramp thresholds (lean: <2% / 2–5% / >5%)
- Q3 — visible-cell culling (lean: 10m radius + frustum)
- Q4 — GreenCollar included (lean: no, Green only v1)
- Q5 — heatmap mode survives (lean: yes, free)

## Sequencing

Runs after `save_layer_reactive_foundation`. No file overlap, so could run parallel — recommend serial for sanity.
