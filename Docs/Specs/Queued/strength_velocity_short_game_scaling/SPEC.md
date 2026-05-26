# strength_velocity_short_game_scaling

> **Status:** Queued (filed from `stat_to_physics_mapping_audit`, 2026-05-25). Tier-Tune.

## One-line

Re-tune `CharStrengthVelocityPerPoint` so the Strength→velocity coupling produces a perceptible carry delta (≥10m) on short-game clubs (Iron7, Wedge) as well as the driver.

## Why

The F7 patch (`CharStrengthVelocityPerPoint = 0.004f`) was calibrated for a driver (~200–440m carry range). At that scale, a 10% velocity bonus for HIGH Strength produces ≥26m delta — clearly above the 10m perceptibility bar. On a wedge approach shot (30–80m carry), the same 10% bonus is only 3–8m — below the bar and invisible to the player.

Root cause: a single coefficient cannot be optimal for both driver and wedge carry scales. Options:
1. Increase the coefficient globally (risk: driver becomes too dominant at HIGH Strength).
2. Scale the coefficient by club type (more complex, more targeted).
3. Route the Strength surplus into a secondary short-game effect (e.g., spin amplitude or roll-out, not velocity).

## Scope

1. Measure: run the stat_lane_surface_roll bot scenario (from stat_to_physics_mapping_audit §Methodology) with wedge club, LOW vs HIGH Strength, at fixed power=1.0. Measure carry delta.
2. Propose: either a coefficient retune or a secondary short-game coupling.
3. Implement: single-coefficient change (if global retune) or a new resolver lane (if secondary coupling — this would escalate to Tier-Redesign).
4. Test: regression against existing `Stats_CharStrength50_VelocityMultiplierGreaterThan_Strength5` test + new wedge-specific assertion.

## Hard rules

- Any coefficient change must keep the driver carry delta (HIGH vs LOW) ≥10m.
- Tests must stay at or above the current physics baseline.

## Out of scope

- Putter lanes.
- Session-level stamina mechanics.
