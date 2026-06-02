# ball_roll_coefficient_retune

> **Status:** Queued (filed from `stat_to_physics_mapping_audit`, 2026-05-25). Tier-Tune.

## One-line

Raise `BallRollPerPoint` from 0.01 to 0.02 so a max-roll ball produces ≥10m additional roll-out distance vs a min-roll ball on Fairway.

## Why

At `BallRollPerPoint = 0.01` on a ±10 range, the roll resistance multiplier swings between 0.90 and 1.10 (with caps at 0.80 and 1.20). A 10% change in rolling resistance on a 30m Fairway roll-out changes terminal distance by ~3m. Below the 10m perceptibility bar.

At `BallRollPerPoint = 0.02`:
- Ball.Roll = -10: rollMul = 1.20 (cap-limited) → shorter roll
- Ball.Roll = +10: rollMul = 0.80 (cap-limited) → longer roll
- Cap-to-cap delta: 40% swing in friction → on a 30m roll-out, ~12m delta. **Above the 10m bar.**

This is a single-coefficient change. The `RollMultiplierMax/Min` caps (0.80–1.20) already accommodate this; the new coefficient fills the cap range at ±10 ball points. No polarity change.

## Scope

1. Change `BallRollPerPoint` from `fp.FromFloat(0.01f)` to `fp.FromFloat(0.02f)` in `StatCoefficients.Default`.
2. Update any regression tests that assert specific roll multiplier values.
3. Run `stat_lane_surface_roll` scenario: Fairway lie, Ball.Roll=-10 vs +10. Verify ≥10m roll-out delta.
4. Document in `PHYSICS_TUNING_CHANGELOG.md`.

## Hard rules

- Cap polarity cannot change.
- `RollMultiplierMax = 1.20` and `RollMultiplierMin = 0.80` must remain (no clamp boundary change).
- Hole 1 completability must hold.
