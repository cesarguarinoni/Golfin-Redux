# ball_simulation_peak_y_logging

> **Status:** Queued (filed from `spin_and_shot_shape_wiring` close-out, 2026-05-26 10:20 CEST). P3 — Low. Tooling.

## One-line

Add `peakY` tracking to `BallSimulation` (or emit it via `DiagShotLogger`) so future visual-gate SPECs can use numeric apex thresholds instead of "visually verified from video."

## Why

The `spin_and_shot_shape_wiring` close-out (Q1) downgraded the TOPSPIN criterion from a numeric "Δ carry ≥3m or Δ total ≥8m" to a visual "lower apex than CENTER" check, because:
- The current physics model's Magnus sign-flip produces *downward* lift for true topspin → carry/total shorter not longer (verified against `AeroModel.cs:89` `liftDir = Cross(spin.Axis, vRelHat)`).
- "Lower apex" is the correct numeric signal of the Magnus direction flip.
- But `BallSimulation` does not currently expose a `peakY` metric — so we accepted a visual-only criterion for v1.

This SPEC adds the missing data-line so the next time a visual gate cares about apex height, it can use a number instead of an eyeball.

## Scope

1. In `BallSimulation.SimulateAirborne` (or wherever the per-step pos loop lives), track the max Y reached during flight.
2. Surface `peakY` via:
   - A new field on `Trajectory` (struct/class — pick whichever matches existing pattern).
   - A `[Build]` or `[Apex]` log line via `DiagShotLogger` at trajectory finalization (timestamped, in fp.ToFloat() format consistent with existing log lines).
3. Update `live_stat_log.txt` parser in `Docs/Scripts/build_bot_video.py` if useful for captions.
4. Add 1–2 EditMode tests in `BallSimulationTests.cs` (or equivalent) asserting `peakY > origin.Y` for a non-trivial shot, and that flat trajectory has `peakY ≈ origin.Y`.

## Hard rules

- Additive change only. Existing `Trajectory` consumers compile unchanged.
- No physics-model behavior changes — `peakY` is observation, not control.
- No `[Build]` log format break — append the new field, don't reorder.

## Out of scope

- Logging additional trajectory metrics (descent angle, time-at-apex, etc.). File separately if wanted.
- Replacing the visual-gate criterion retroactively in `spin_and_shot_shape_wiring` (that task is closed; future tasks can use the new metric).

## Sequence

Independent — can fire any time. Fires before any future visual-gate SPEC that wants apex numbers.
