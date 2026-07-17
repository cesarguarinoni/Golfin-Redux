# strength_velocity_short_game_scaling  (Order 415)

> **Status: VOID — refuted by measurement (2026-07-17). No coefficient retune warranted; physics baseline untouched.**
> Filed from `stat_to_physics_mapping_audit` (2026-05-25) as Tier-Tune. Unblocked for measurement by Order 731 (landed 2026-07-17), which removed the resolver-side double-stamina lane so `effStrength` is raw `Character.Strength` and the Strength→velocity coupling could be measured on a clean baseline. Measured, and the premise did not hold — same shape as 731's voiding of the audit's F-LANA-2a/2b.

## Verdict

The audit premise — *"Strength→velocity is imperceptible (3–8m, below the 10m bar) on short-game clubs"* — is **false at the current coefficient** (`CharStrengthVelocityPerPoint = 0.004`). Strength already produces a large, perceptible carry delta across the short game in both absolute and relative terms. The only band below a 10m **absolute** delta is the very shortest chip (~23m carry), where 10m would be ~40% of the shot — a nonsensical bar. **Keep the coefficient at 0.004. No change. No F-entry** (nothing moves physics output).

## Measurement (deterministic sim — flat fairway, power=flick, Strength LOW=5 vs HIGH=50, neutral ball, lab club Power=50)

Method: `StatModifierResolver.Resolve` (coeff passed as a param, swept without touching config) → `launch = baseVel × flick × VelocityMultiplier` → `BallSimulation.Simulate` on `FlatGround(0)` / `Fairway` / `AeroConfig.Default` / calm. Independent `Simulate` per sample (Lesson V). Ran via `script-execute` post-731, post-bot-rehab (physics core confirmed unchanged by the bot-rehab commit `82b63715c`).

**Short-game regime, current coeff 0.004** (`flick → carry : HIGH−LOW delta (relative)`):

| Club | 0.25 | 0.35 | 0.45 | 0.55 | 0.70 |
|---|---|---|---|---|---|
| **Wedge** | 23m: **8.1m** (36%) — only sub-bar | 42m: 14.0m (33%) | 65m: 18.6m (29%) | 89m: 22.8m (26%) | 123m: 23.8m (19%) |
| **Iron7** | 29m: 10.7m (36%) | 54m: 19.5m (36%) | 86m: 28.2m (33%) | 122m: 32.6m (27%) | 171m: 33.6m (20%) |

Full-power (flick 1.0) all clubs clear easily: Driver 37.5m, Iron7 20.2m, Wedge 14.2m delta.

**Why a retune is the wrong tool:** the Strength bonus is *multiplicative* (Str 50 vs 5 ≈ +17.6% launch velocity → a fixed **percentage** of carry, ~19–36%). A multiplicative boost cannot deliver a constant **absolute** short-game delta without inflating the long game. Raising the coeff overcorrects badly:

| coeff | Wedge delta range | Iron7 delta range | Driver delta |
|---|---|---|---|
| 0.004 (current) | 8–24m | 11–34m | 37.5m |
| 0.008 | 17–43m | 21–61m | 61.7m |
| 0.012 | 27–59m | 35–84m | 70m+ |

At 0.008–0.012 Strength becomes dominant (60–84m short-game swings, 60–70m+ driver) — clearly unbalanced vs the other stats. The one genuinely sub-bar case (shortest chips) is unreachable by the velocity coefficient and, at 36% relative, is already very perceptible; it does not justify a Tier-Redesign secondary-coupling mechanic.

## Downstream

- Order 731 also unblocked **417 `ball_rebound_perceptibility`** (still Queued) — it is unaffected by this void and remains ready to measure.
- Measurement script preserved in the session scratchpad (`415_measure.cs`) if a re-measure is ever wanted (e.g. against real equipped-club stats rather than the lab Power=50 reference — that shifts absolute carries but not the qualitative conclusion, since the Strength delta is a ~17.6% velocity boost regardless of club).

---

## Original premise (preserved for history — refuted above)

> Re-tune `CharStrengthVelocityPerPoint` so the Strength→velocity coupling produces a perceptible carry delta (≥10m) on short-game clubs (Iron7, Wedge) as well as the driver. F7's `0.004` was calibrated for the driver; the audit estimated the same 10% velocity bonus is only 3–8m on a 30–80m wedge — below the bar. Options considered: (1) global coefficient bump, (2) per-club scaling, (3) route Strength surplus into a secondary short-game effect. Hard rule was: any change keeps the driver delta ≥10m. — All moot: measurement shows the short game already clears the bar at 0.004.
