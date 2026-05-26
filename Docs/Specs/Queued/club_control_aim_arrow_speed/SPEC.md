# club_control_aim_arrow_speed

> **Status:** Queued (filed from `stat_to_physics_mapping_audit`, 2026-05-25). Tier-Tune.

## One-line

Make Character.ClubControl and Club.Accuracy perceptible to the player in isolation by also driving the aim-arrow oscillation speed (in addition to the existing hidden aim-cone reduction).

## Why

The current aim-cone reduction from ClubControl/Club.Accuracy is sub-perceptible at LOW-MID stat ranges (≤8.75% cone reduction → <3m lateral improvement on a 200m shot). The stat feels like a no-op because the player cannot see the reduced cone and the carry impact is below the 10m bar.

Aim-arrow oscillation speed is a visible, real-time feedback mechanism that makes the player feel the benefit of high-accuracy stats. A HIGH ClubControl character should visibly oscillate the aim arrow more slowly, giving the player more time to find the correct aim window.

## Scope

1. Add a `AimArrowSpeedMultiplier` output to `ResolvedShotModifiers` sourced from `aimConeReduction` (use the existing value, don't add a new coefficient).
2. Wire into the aim-arrow animation speed in `ShotConeView` or `ClubHandleDragger`.
3. Verify: bot scenario showing LOW vs HIGH ClubControl with visible aim-arrow speed difference in the captioned video.

## Hard rules

- Must not change the `aimConeReduction` computation — this is purely additive output.
- No new coefficient needed (derive from existing aimConeReduction).
- Tests must stay at or above current baseline.

## Out of scope

- Changing the aim-cone geometry (that's a separate balancing concern).
- Putter aim cycles (already implemented via `aimCycles` output).
