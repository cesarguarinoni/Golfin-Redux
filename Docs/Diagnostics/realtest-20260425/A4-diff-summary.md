# A4 — Load Determinism Diff Summary
# Generated: 2026-04-25 08:32:52

## Shot 1
  Cycle 1: 0 rows loaded.
  Cycle 2: 0 rows loaded.
  Cycle 3: 0 rows loaded.
  Row counts: [0, 0, 0] — DIFFER
  Cycle 1: No fall-through. Min gap=0.0000m
  Cycle 2: No fall-through. Min gap=0.0000m
  Cycle 3: No fall-through. Min gap=0.0000m

## Shot 2
  Cycle 1: 643 rows loaded.
  Cycle 2: 643 rows loaded.
  Cycle 3: 643 rows loaded.
  Row counts: [643, 643, 643] — MATCH
  Trajectory: BIT-IDENTICAL across all 3 cycles.
  Cycle 1: No fall-through. Min gap=0.0000m
  Cycle 2: No fall-through. Min gap=0.0000m
  Cycle 3: No fall-through. Min gap=0.0000m

## Shot 3
  Cycle 1: 640 rows loaded.
  Cycle 2: 640 rows loaded.
  Cycle 3: 640 rows loaded.
  Row counts: [640, 640, 640] — MATCH
  Trajectory: BIT-IDENTICAL across all 3 cycles.
  Cycle 1: No fall-through. Min gap=0.0000m
  Cycle 2: No fall-through. Min gap=0.0000m
  Cycle 3: No fall-through. Min gap=0.0000m

## Shot 4
  Cycle 1: 11 rows loaded.
  Cycle 2: 11 rows loaded.
  Cycle 3: 11 rows loaded.
  Row counts: [11, 11, 11] — MATCH
  Trajectory: BIT-IDENTICAL across all 3 cycles.
  Cycle 1: No fall-through. Min gap=0.0000m
  Cycle 2: No fall-through. Min gap=0.0000m
  Cycle 3: No fall-through. Min gap=0.0000m

## Shot 5
  Cycle 1: 2147 rows loaded.
  Cycle 2: 2147 rows loaded.
  Cycle 3: 2147 rows loaded.
  Row counts: [2147, 2147, 2147] — MATCH
  Trajectory: BIT-IDENTICAL across all 3 cycles.
  Cycle 1: No fall-through. Min gap=0.0000m
  Cycle 2: No fall-through. Min gap=0.0000m
  Cycle 3: No fall-through. Min gap=0.0000m

## Verdict
**Outcome:** Trajectories diverge across cold loads but no fall-through observed in these 5 shots.
**Recommended path:** Ambiguous — Architect reviews CSVs. Non-determinism present but may not cause fall-through in these specific shots.
