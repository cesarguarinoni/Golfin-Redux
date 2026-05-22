# STATUS — `puttpath_predictor_perf_and_design`

| Field | Value |
|---|---|
| Current state | **PIPELINE_READY** |
| Created (NOTES) | 2026-05-07 (Architect initial scoping) |
| Design-locked (NOTES) | 2026-05-13 (Cesar L1/L2/L3) |
| SPEC authored | 2026-05-22 ~13:30 CEST |
| Q-locks recorded | 2026-05-22 ~14:30 CEST |
| Best-practice patches | 2026-05-22 ~14:35 CEST |
| Architect | claude.ai |
| Implementer | Claude Code |
| Pipeline | FULL PIPELINE |

## Timeline

- **2026-05-07** — Architect initial NOTES.md
- **2026-05-13** — Design-locked by Cesar (L1 Sim positioning / L2 redesign / L3 baked per-region)
- **2026-05-22 13:30 CEST** — SPEC authored. Pre-flight corrected NOTES.md inaccuracies (file lives in Viewer asmdef, event is OnChanged not OnHoleLoaded).
- **2026-05-22 — moved** — folder relocated `Queued/` → `Active/`.
- **2026-05-22 14:30 CEST** — Cesar locked all 5 Qs at architect leans.
- **2026-05-22 14:35 CEST** — Best-practice scan run. Three additive patches landed:
  - `Graphics.RenderMeshInstanced` (Unity 2022+) over the legacy `DrawMeshInstanced`
  - GPU Instancing material flag + SRP Batcher precedence opt-out (URP-specific, mandatory)
  - Future-polish note: PGA 2K23/2K25 uses animated beads; arrows-for-v1 is correct, beads swap is renderer-only over same bake data
- **2026-05-22 — TBD** — Kickoff fired to Code (after save_layer_reactive_foundation ships).

## Q-locks (§4 of SPEC)

- Q1 — Cell size 0.5m
- Q2 — Color ramp <2% / 2–5% / >5%
- Q3 — Culling 10m + frustum
- Q4 — Green only for v1
- Q5 — Heatmap mode survives

## Sequencing

Runs after `save_layer_reactive_foundation` per Cesar's preferred serial pickup. No file overlap, so could run parallel — keeping serial for sanity / single Code session.
