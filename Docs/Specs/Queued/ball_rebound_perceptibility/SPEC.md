# ball_rebound_perceptibility

> **Status:** Queued (filed from `stat_to_physics_mapping_audit`, 2026-05-25). Tier-Tune.

## One-line

Increase the Ball.Rebound coefficient so a max-rebound ball produces a visually distinct bounce compared to a min-rebound ball in a standard bot scenario.

## Why

At `BallReboundPerPoint = 0.01` on a ±10 range, the rebound multiplier swings between 0.90 and 1.10 — a 20% swing in surface restitution. On a driver landing at ~30 m/s with Cr ≈ 0.5 (Fairway), the bounce height and secondary carry change by ≈1–2m. This is below the 10m perceptibility bar.

To make Ball.Rebound a meaningful differentiator without changing the lane or clamp polarity, the coefficient can be raised. The `ReboundMultiplierMax = 1.20` and `ReboundMultiplierMin = 0.80` caps give room up to ±20% swing before touching the clamp. With a coefficient of `0.02/point` (doubling the current value), a ±10 ball produces ±20% rebound → ~3–4m secondary carry delta. Still below the bar but twice as impactful.

Alternatively, widening the cap range to ±30% (0.70–1.30) at the current 0.01 coefficient would require the clamp change — this escalates to Tier-Tune (clamp change allowed in this tier).

## Scope

1. Measure current rebound delta: run `stat_lane_surface_roll` scenario with ball Rebound=-10 vs +10 on a Fairway lie. Record secondary carry delta.
2. Propose: either `BallReboundPerPoint` coefficient bump or `Rebound{Min,Max}` cap adjustment.
3. Implement + regression test.
4. Verify: bot side-by-side with visible bounce difference.

## Hard rules

- Cap polarity cannot change (floor must remain < 1.0, ceiling must remain > 1.0).
- Hole 1 completability must hold (≤7 strokes with default character).
