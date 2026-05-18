# STATUS — `phase_b_surface_tuning` Stage 2

**Current:** `SPEC_READY`
**Pipeline tier:** Tier 2 TELLCODE
**Stage:** 2 of 2 (final). Stage 1 (diagnostic harness) shipped 2026-05-18, CSV captured.

## State log

- 2026-05-18 — Architect wrote Stage 2 SPEC.md. Status: SPEC_READY.
  - Source data: `Docs/Specs/Completed/phase_b_surface_tuning/captures/20260518_122845/sweep.csv`
  - Filter: Cesar-specified (`surface_target ∈ {Fairway, Green, Sand}`, `end_surface == surface_target`, `source_hole ∈ {1, 9}`, plus all putts)
  - Proposed k bumps: Fairway 0.18→0.23, Green 0.12→0.34, Sand 0.70→1.02
  - Out of scope: putt.csv, Rough, Tee, test-band tightening (separate Notion 111)
  - Architect-observed anomalies filed in SPEC §Caveats #4 and #5: roll-path spin response and putt-k GreenCollar inversion
