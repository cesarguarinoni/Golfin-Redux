# ball_rebound_perceptibility  (Order 417)

> **Status: IMPLEMENTED — awaiting Cesar approval → Completed.** Measure-first confirmed the premise; applied the coefficient bump the spec proposed. Tier-Tune. Unblocked for measurement by Order 731 (clean baseline, landed 2026-07-17).

## Verdict

Premise **holds** (contrast Order 415, which was voided). Measured (deterministic sim, flat fairway, neutral, power=1.0): at `BallReboundPerPoint = 0.01` the full ±10 Ball.Rebound swing (reboundMul 0.90→1.10) moves total distance only **~4.8m** — below the 10m bar. Applied the spec's primary proposal: **`BallReboundPerPoint` 0.01 → 0.02**, which maps ±10 to reboundMul **0.80→1.20** (the full existing cap band, no clamp change, polarity unchanged) for a **~10.7m** delta — clears the bar and is self-limiting (max stat lands on the 1.20 cap, cannot overcorrect).

## Measurement (total distance = carry + bounce + roll)

| club | rm 0.80 | 0.90 | 1.00 | 1.10 | 1.20 | current ±10 (0.90→1.10) | 0.02 coeff (0.80→1.20) |
|---|---|---|---|---|---|---|---|
| Driver | 321.6 | 324.5 | 326.6 | 329.4 | 332.4 | **4.8m — below bar** | **10.8m — clears** |
| Iron7  | 231.1 | 233.9 | 235.9 | 238.7 | 241.7 | **4.8m — below bar** | **10.6m — clears** |

## Changes applied

1. `Assets/Scripts/Physics/Stats/StatCoefficients.cs` — `BallReboundPerPoint` 0.01 → 0.02 (comment cites Order 417).
2. `Assets/Scripts/Physics/Tests/StatResolverTests.cs` — `Stats_BallRebound_MultiplierCorrect`: Ball Rebound +10 → ReboundMultiplier **1.10 → 1.20** (lands on the 1.20 cap exactly).
3. `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` — F10 entry.

## Verification

- `Golfin.Physics.Tests` EditMode: **234 pass / 0 fail / 3 pre-existing skips** (2026-07-17).
- **Hole 1 completability: unaffected — no-op on the default ball.** `ball_golfin` has `rebound=0` → `reboundMul = 1.0` regardless of the coefficient. Only rebound-stat balls (e.g. `ball_putt_ace`, rebound=−6) see the change. No bot run needed to prove the baseline holds.
- Caps unchanged (`ReboundMultiplierMin/Max` = 0.80/1.20); polarity rule respected (floor < 1.0 < ceiling).

## Hard rules (met)

- Cap polarity unchanged. ✓
- Hole 1 completability holds (no-op on default ball). ✓

---

## Original premise (preserved — CONFIRMED by measurement, unlike 415)

> Increase the Ball.Rebound coefficient so a max-rebound ball produces a visually distinct bounce vs a min-rebound ball. At `0.01` on a ±10 range the multiplier swings 0.90–1.10 (~1–2m per the audit; measured ~4.8m) — below the 10m bar. The caps (0.80–1.20) leave room; `0.02/point` makes ±10 a full ±20% swing. (Alternative: widen caps to ±30% at 0.01 — not needed; 0.02 within the existing caps suffices.)
