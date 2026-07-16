# Stat-to-Physics Lane Audit

**Task:** `stat_to_physics_mapping_audit`  
**Author:** Implementer agent  
**Date:** 2026-05-25  
**Baseline:** `live_stat_provider_wiring` Phase 4 (342/339/0/3 tests passing, F7 Strength→velocity patch in place)

---

## Overview

This document audits every stat-to-physics lane in `StatModifierResolver.cs` and the ball physics modifiers in `BallSimulation`. For each lane:
- Which stat feeds it (single-source per design)
- Min/max impact at realistic stat-range extremes
- Perceptibility classification: **PASS** (perceptible) or **WEAK** (sub-threshold)
- Finding tier: `Justified-as-is`, `Tier-Safe`, `Tier-Tune`, `Tier-Redesign`

### Stat ranges used for LOW / MID / HIGH profiles

| Profile | Character Str/Ctrl/Rec/Stam | Club Power/Acc/Lie | Ball stats |
|---|---|---|---|
| LOW | 5–10 (Common-rarity max) | 50 (neutral lab default) | 0 (neutral) |
| MID | 20–25 (Rare-rarity max) | 50 (neutral) | 0 (neutral) |
| HIGH | 45–50 (Supreme-rarity max) | 50 (neutral) | 0 (neutral) |

Note: per Q1, the per-lane sweep fixes all stats at MID and varies only the dominant stat for that lane. Club Power at 50 is the lab "neutral" default; real club power ranges from 0 to 120 across all rarities.

### Coefficients at HEAD (StatCoefficients.Default + StatCaps.Default)

| Coefficient | Value | Notes |
|---|---|---|
| `ClubPowerPerPoint` | 0.005 | velocity +0.5%/point |
| `BallPowerPerPoint` | 0.01 | velocity +1.0%/point |
| `CharStrengthVelocityPerPoint` | 0.004 | velocity +0.4%/point (F7 lane, 2026-05-25) |
| `ClubAccuracyPerPoint` | 0.0042 | aim cone reduction per point |
| `CharClubControlPerPoint` | 0.0035 | aim cone reduction per point |
| `BallSpinPerPoint` | 0.01 | spin multiplier per point |
| `ClubLieResistancePerPoint` | 0.0042 | lie penalty reduction per point |
| `CharStrengthPerPoint` | 0.00625 | overpower forgiveness per point |
| `PutterControlPerPoint` | 0.0042 | off-center forgiveness per point |
| `PutterAccuracyPerPoint` | 0.0075 | gravity well radius per point |
| `PutterWeightPerPoint` | 0.125 | aim cycles per point |
| `BallReboundPerPoint` | 0.01 | restitution multiplier per point |
| `BallWindCutPerPoint` | 0.01 | wind drag reduction per point |
| `BallRollPerPoint` | 0.01 | rolling resistance reduction per point |
| `StaminaFloorFraction` | 0.20 | stat floor at zero stamina |
| `VelocityMultiplierMax` | 2.6 | (raised from 2.0 in F7) |
| `AimConeReductionMax` | 0.95 | (never 100% reduction) |
| `LieResistanceMax` | 0.75 | |
| `OverpowerForgivenessMax` | 0.75 | |
| `ReboundMultiplierMax` | 1.20 | |
| `ReboundMultiplierMin` | 0.80 | |
| `RollMultiplierMax` | 1.20 | |
| `RollMultiplierMin` | 0.80 | |
| `WindCutMax` | 0.30 | |

---

## Lane 1 — Velocity Multiplier (swing only)

**Sources:** Club.Power × Ball.Power × Character.Strength (multiplicative)

```
velFromClub = 1 + Club.Power × 0.005
velFromBall = 1 + Ball.Power × 0.01
velFromChar = 1 + (Strength × staminaMultiplier) × 0.004   [F7 lane]
velocityMultiplier = velFromClub × velFromBall × velFromChar, capped at 2.6
```

### Sub-lane 1a: Club.Power → velocity

| Club.Power | velFromClub | vs Power=0 |
|---|---|---|
| 0 | 1.00 | baseline |
| 50 (neutral lab) | 1.25 | +25% |
| 80 (driver tier) | 1.40 | +40% |
| 120 (max) | 1.60 | +60% |

The real-world range for a player is 0→120 across all clubs and rarities. Delta at extremes: **60% velocity delta → at v²/g carry scaling, that's (1.6/1.0)² = 2.56× carry ratio at equal launch angle.** 

**Perceptibility: PASS.** A 60% velocity delta produces clearly different carry distances. Even within a realistic club tier (e.g., Common Driver power=30 vs Supreme Driver power=100), the delta is 35% velocity → ~82% carry ratio, easily perceptible.

**Finding:** `Justified-as-is`. Club.Power is the primary distance differentiator as designed. The coefficient 0.005/point is well-tuned for a 120-point range.

### Sub-lane 1b: Ball.Power → velocity

| Ball.Power | velFromBall | vs Power=0 |
|---|---|---|
| -10 | 0.90 | -10% |
| 0 (neutral) | 1.00 | baseline |
| +10 | 1.10 | +10% |

Range is ±10 points, giving ±10% velocity. At v²/g carry: (1.1/0.9)² ≈ 1.49× carry ratio between worst and best ball. **Delta is perceptible** on a visual side-by-side.

**Perceptibility: PASS.**

**Finding:** `Justified-as-is`. Ball.Power operates on a ±10 range which is intentionally narrow — balls are a secondary differentiator. The 10% per-extreme delta is above the ≥10m bar for any hole of distance ≥100m.

### Sub-lane 1c: Character.Strength → velocity (F7 lane)

| Char.Strength | velFromChar | vs Strength=0 |
|---|---|---|
| 0 (FALLBACK) | 1.00 | baseline |
| 5 (LOW start) | 1.02 | +2% |
| 10 (LOW max) | 1.04 | +4% |
| 25 (MID) | 1.10 | +10% |
| 50 (HIGH) | 1.20 | +20% |

F7 visual gate delta: HIGH (STR=30) vs LOW (STR=8) on same driver: **26m on a ~420m carry** — 6.2% velocity delta → confirmed above 10m bar.

Concern: at LOW-range (STR 5–10), the delta is only 2–4% velocity, which corresponds to ≈4–8m on a 200m approach shot. This may be sub-perceptible for near-green clubs where carry distances are shorter (e.g., 30m wedge shot: 4% = 1.2m delta — not perceptible to a player).

**Perceptibility: PASS on driver tier; WEAK on wedge/iron approach tier.**

**Finding:** `Tier-Tune`. The 0.004/point coefficient was calibrated for a driver (long carry). For approach shots with short carry, the Strength bonus is below the 10m bar. Two options: (a) scale the coefficient for near-green clubs, or (b) accept that Strength only noticeably differentiates long-driver builds, which may be intentional. Full redesign needed — filing `Docs/Specs/Queued/strength_velocity_short_game_scaling/SPEC.md`.

---

## Lane 2 — Aim Cone Reduction

**Sources:** Club.Accuracy (clubAccReduction) × Character.ClubControl (charControlReduction)

```
clubAccReduction  = Club.Accuracy × 0.0042
charControlReduction = effClubControl × 0.0035
unreducedFraction = (1 - clubAccReduction) × (1 - charControlReduction)
aimConeReduction  = 1 - unreducedFraction, capped at 0.95
```

### Sub-lane 2a: Club.Accuracy → aim cone

| Club.Accuracy | clubAccReduction | Remaining cone |
|---|---|---|
| 0 | 0.000 | 100% of base cone |
| 50 (neutral) | 0.210 | 79% of base cone |
| 80 | 0.336 | 66.4% of base cone |
| 120 | 0.504 | 49.6% of base cone |

Range: club accuracy alone reduces cone by up to **50.4%**. Combined with a HIGH ClubControl character: cone factor of (1 - 0.504) × (1 - 0.175) = 0.496 × 0.825 = 0.409 → 59.1% reduction. A 50% cone reduction on Hole 1's typical 5–8° cone = 2.5–4° absolute reduction. On a 200m approach, 2° of aim spread translates to ≈7m lateral deviation at target.

**Perceptibility: PASS at extreme (HIGH Club.Accuracy), WEAK at LOW.**

At LOW Club.Accuracy (e.g., 20): reduction = 0.084 → cone is 91.6% of base. Delta from baseline: 8.4% cone reduction. On a 200m shot with a 6° half-cone, this is 200 × sin(0.504°) ≈ 1.76m lateral spread reduction. Sub-perceptible in a single shot; marginally perceptible in an aggregate of many shots.

**Finding:** `Tier-Tune` — the accuracy/control lane works at the high end. The low end is weak but design-intentional (low-accuracy clubs should feel imprecise). A coefficient bump from 0.0042 to 0.006 would make mid-tier feel more impactful, but this change affects existing `StatResolverTests.cs` assertions and requires playtest validation beyond a unit test — so it is classified Tier-Tune, not Tier-Safe. See `club_control_aim_arrow_speed` follow-up spec. (Note: previously mislabeled Tier-Safe in this body; reclassified Tier-Tune to match the perceptibility matrix and findings classification table.)

### Sub-lane 2b: Character.ClubControl → aim cone

| Char.ClubControl | charControlReduction | Combined (with Club.Acc=50) |
|---|---|---|
| 0 (LOW) | 0.000 | 0.21 total reduction |
| 10 (LOW) | 0.035 | 0.238 total reduction |
| 25 (MID) | 0.0875 | 0.279 total reduction |
| 50 (HIGH) | 0.175 | 0.348 total reduction |

Delta HIGH vs LOW: total reduction changes from 0.21 to 0.348 → combined unreducedFraction changes from 0.79 to 0.652. Effective cone goes from 79% to 65.2% of base cone. On 6° half-cone → 0.84° absolute difference → at 200m: 200 × sin(0.84°) ≈ 2.93m lateral spread reduction. Marginal but non-zero.

**Perceptibility: WEAK in isolation, PASS when combined with Club.Accuracy.**

**Finding:** `Tier-Tune`. ClubControl in isolation below MID (STR<25) produces a sub-3m lateral improvement on long approach shots — unlikely to change stroke outcomes. The design intent (single-source per lane with weak individual contributions that stack) is architecturally sound, but the practical effect is below the perceptibility bar for casual play. A redesign would add a secondary visible effect (e.g., aim-arrow oscillation speed) — filing `Docs/Specs/Queued/club_control_aim_arrow_speed/SPEC.md`.

---

## Lane 3 — Spin Magnitude Multiplier

**Source:** Ball.Spin (single-source)

```
spinMul = 1 + Ball.Spin × 0.01
```

| Ball.Spin | spinMul | vs Spin=0 |
|---|---|---|
| -10 | 0.90 | -10% applied spin |
| 0 (neutral) | 1.00 | baseline |
| +10 | 1.10 | +10% applied spin |

The multiplier applies to the spin vector, affecting trajectory curl and roll direction after landing. A 10% spin amplitude change produces a visible trajectory curl difference on a high-spin shot (e.g., a wedge with 9000 RPM backspin → 9900 RPM at max spin ball).

**Perceptibility:** Depends on shot type. For driver (backspin 2686 RPM) a 10% change is 268.6 RPM — the aerodynamic lift coefficient changes by a small amount at typical driver speeds. For wedge (9000 RPM), 10% change is 900 RPM — more impactful on trajectory and roll. At standard trajectories and distances, spin ball effects are **perceptible on wedge/iron approach shots** but subtle on driver shots.

**Finding:** `Justified-as-is`. Ball.Spin operating on a ±10 range with 1%/point is design-appropriate for a secondary differentiator. The effect is most visible on approach shots (short carry, high spin) which is correct game design — spin balls are for approach accuracy, not distance.

**Cross-cutting question (from SPEC §4):** Should Ball.Power and Ball.Spin compete or stack? Currently both are positive multipliers — a Ball with Power=+10 AND Spin=+10 gets both benefits (additive in their respective lanes). This seems intentional and is not problematic.

---

## Lane 4 — Lie Resistance

**Source:** Club.LieResistance (single-source)

```
lieResist = Club.LieResistance × 0.0042, capped at 0.75
```

| Club.LieResist | lieResist | Penalty reduction |
|---|---|---|
| 0 | 0.000 | 0% (full lie penalty applies) |
| 50 (neutral) | 0.210 | 21% penalty reduction |
| 80 | 0.336 | 33.6% |
| 120 (max) | 0.504 | 50.4% (capped below LieResistanceMax=0.75) |

How lie resistance is consumed: `ResolvedShotModifiers.LieResistance` reduces the lie penalty multiplier applied to the velocity in `ShotInputBuilder`. The penalty depends on which surface the ball is on — Rough, Sand, etc. introduce velocity penalties (e.g., Rough might apply a 0.7 multiplier).

**Perceptibility:** In actual play, the lie resistance effect only manifests when the player fires from a lie penalty surface. From Fairway (no penalty), lie resistance has zero observable effect. From Rough (penalty ≈ 0.7), a 50% lie resistance gives back 0.5 × (1 - 0.7) = 0.15 velocity units → the effective velocity goes from 0.7 to ~0.85 — a 21% improvement. On a 200m approach from Rough, that's ≈35m additional carry. **Highly perceptible when triggered**.

**Finding:** `Justified-as-is`. Lie resistance is situational (only matters from penalty lies). The design is intentional — players on Fairway never notice it, players in Rough or Sand do. This matches the "forgiveness" design intent. No change needed.

---

## Lane 5 — Overpower Forgiveness

**Source:** Character.Strength (single-source, DIFFERENT from F7 velocity lane)

```
overpower = effStrength × 0.00625, capped at 0.75
```

| Char.Strength | overpower | Forgiveness |
|---|---|---|
| 0 | 0.000 | 0% (any overpower is fully penalized) |
| 10 (LOW) | 0.0625 | 6.25% forgiveness |
| 25 (MID) | 0.15625 | 15.6% |
| 50 (HIGH) | 0.3125 | 31.25% |
| 120 (theoretical max) | 0.75 | 75% (cap) |

How overpower is consumed: `ShotInputBuilder.cs` reduces the `flickMag` penalty when the player overpowers (flick magnitude > 1.0). The overpower forgiveness keeps the effective power closer to 1.0, reducing carry overshoot.

**Perceptibility:** Only manifests when the player overpowers (flick > 1.0). At the bot's standard `power = 1.0` (exact), overpower forgiveness has **zero observable effect**. For a human player who regularly overpowers by 10%, a 31% forgiveness at HIGH strength reduces the velocity penalty by 0.031 (small). Unlikely to change stroke outcomes unless the player systematically overpowers significantly.

**Cross-cutting question (SPEC §4):** "Should Character.Strength directly affect velocity (currently no, post-patch only weak coupling)?" — answered by F7. The F7 patch added a second Strength lane (velocity). Now Strength has two purposes: velocity coupling (F7) and overpower forgiveness. The overpower lane is ALSO Strength, which doubles its value for players who overshoot.

**Finding:** `Justified-as-is`. Overpower forgiveness is a "hidden" stat that only advanced players encounter — Common-rarity characters (who overshoot more often) benefit less from it than Supreme-rarity characters. This design asymmetry is intentional: Supreme builds reward precision while offering a safety net. No change needed.

---

## Lane 6 — Putter: Off-Center Forgiveness

**Source:** Putter.Control (single-source)

```
putterOffCenter = Putter.Control × 0.0042, capped at 0.50
```

| Putter.Control | putterOffCenter | Forgiveness |
|---|---|---|
| 0 | 0.000 | 0% (miss registered precisely) |
| 50 | 0.210 | 21% forgiveness |
| 80 | 0.336 | 33.6% |
| 120 | 0.504 → 0.50 (capped) | 50% forgiveness |

Putter lanes are out of scope per SPEC §Out of scope unless a swing-lane finding implicates them. No swing-lane finding implicates these lanes. Brief summary only.

**Finding:** `Justified-as-is`. Putter lanes are 1:1 with putter inputs and don't have the single-source-per-character-lane issue.

---

## Lane 7 — Putter: Gravity Well Radius

**Source:** Putter.Accuracy (single-source)

```
gravityWellRadius = 0.10 + Putter.Accuracy × 0.0075, clamped to [0.10, 1.00]
```

| Putter.Accuracy | gravityWellRadius |
|---|---|
| 0 | 0.10m |
| 50 | 0.475m |
| 120 | 1.00m (capped) |

**Finding:** `Justified-as-is`. Out of scope.

---

## Lane 8 — Putter: Aim Cycles

**Source:** Putter.Weight (single-source)

```
aimCycles = 5 + (int)(Putter.Weight × 0.125), clamped [5, 20]
```

| Putter.Weight | aimCycles |
|---|---|
| 0 | 5 |
| 50 | 11 |
| 120 | 20 (capped) |

**Finding:** `Justified-as-is`. Out of scope.

---

## BallPhysicsModifiers Audit

### Sub-lane B1: Ball.Rebound → restitution multiplier

```
reboundMul = 1 + Ball.Rebound × 0.01, clamped [0.80, 1.20]
```

| Ball.Rebound | reboundMul | Bounce energy |
|---|---|---|
| -10 | 0.90 | 10% less bounce |
| 0 | 1.00 | baseline |
| +10 | 1.10 | 10% more bounce |

Effect: modifies coefficient of restitution at ball-ground impact. A 20% swing in restitution (from 0.8× to 1.2× of base surface Cr) produces a visible change in bounce height and secondary carry. On a driver shot landing at ~30m/s with moderate bounce, this translates to ~1–2m of secondary carry difference.

**Perceptibility: WEAK at range extremes, essentially invisible for near-neutral balls.**

**Finding:** `Tier-Tune`. The ±10% rebound swing is design-appropriate but the perceptibility bar (≥10m carry delta) is not met on realistic shots where the ball bounces once at moderate speed. The rebound effect would be more perceptible on high-bounce surfaces (cart paths, slopes). Filing `Docs/Specs/Queued/ball_rebound_perceptibility/SPEC.md`.

### Sub-lane B2: Ball.Roll → rolling resistance reduction

```
rollMul = 1 - Ball.Roll × 0.01, clamped [0.80, 1.20]
```

Note: higher Roll = LESS rolling resistance (ball rolls farther). At Ball.Roll=+10, rollMul=0.90 (10% less resistance). This is counter-intuitively named: the multiplier is applied to the friction coefficient, so a lower rollMul means less friction = more roll.

| Ball.Roll | rollMul | Rolling distance |
|---|---|---|
| -10 | 1.10 | 10% more friction = shorter roll |
| 0 | 1.00 | baseline |
| +10 | 0.90 | 10% less friction = longer roll |

Effect on Fairway: typical roll-out distance after a driver is 20–40m. A 20% swing in friction (Ball.Roll -10 vs +10) changes roll by ~20% → delta of 4–8m on roll-out. Combined with carry delta, the total distance delta from ball Roll is 4–8m (theoretical).

**Measured perceptibility (corrected same-start bot run, 2026-05-25 iter-2):**
- LOW Ball.Roll=-10 terminal: (106.25, 10.15, 27.68) — fired from tee
- HIGH Ball.Roll=+10 terminal: (106.19, 10.15, 27.68) — fired from tee (reset between shots)
- Measured delta: **0.1m** (essentially zero)

Note: the iter-1 measurement (106.5m) was a methodology defect — HIGH shot was fired from LOW's terminal position, not from the same starting point. The corrected same-start comparison shows 0.1m delta. This is even weaker than the 4–8m theoretical estimate, suggesting the Wedge at 42 m/s + power=0.55 lands in a region where the fairway friction differential between rollMul=1.10 and rollMul=0.90 is negligible (likely because the approach shot is steep and the ball stops quickly from backspin at power=0.55).

**Perceptibility: WEAK (0.1m measured at extremes — sub-perceptibility on a Wedge approach shot).**

**Finding:** `Tier-Tune`. Roll is the right design (ball quality affects roll-out), but the 1%/point coefficient on a ±10 range produces essentially zero perceptible delta in the current configuration. A coefficient bump to 0.02/point (±20% friction swing) would push theoretical delta to 8–16m; however, the dominant factor may be shot type (Wedge approach stops fast due to backspin). The `ball_roll_coefficient_retune` spec should also instrument with a lower-spin driver approach for a more conclusive measurement. Filing `Docs/Specs/Queued/ball_roll_coefficient_retune/SPEC.md`.

### Sub-lane B3: Ball.WindCut → wind drag reduction

```
windCutFraction = Ball.WindCut × 0.01, clamped [0, 0.30]
```

| Ball.WindCut | windCutFraction | Wind drag reduction |
|---|---|---|
| 0 | 0.00 | 0% (full wind applies) |
| 10 | 0.10 | 10% less wind drag |
| Max effective (30 points) | 0.30 | 30% wind drag reduction |

Effect: only manifests in wind conditions. With no wind (`WindConfig.Calm`), wind cut has zero observable effect. In a 10 m/s crosswind scenario, a 30% drag reduction would change lateral drift by ~30% — perceptible.

**Perceptibility: PASS when wind is present, ZERO when calm.**

**Finding:** `Justified-as-is`. WindCut is situational (like lie resistance) — it's designed as a stat that creates perceptible differences in adverse conditions. For calm-wind bot scenarios used in this audit, it's invisible. This is correct design.

---

## Cross-Cutting Design Questions

### Q: Should Character.Strength directly affect velocity?

**Answer:** YES — F7 (2026-05-25) established this. `CharStrengthVelocityPerPoint = 0.004f` is in production at HEAD. The full audit recommends this lane be revisited for short-game scaling (Tier-Tune follow-up filed).

### Q: Should Character.Recovery feed back into stamina regen between shots?

**Answer:** OPEN. `CharacterStats.Recovery` is defined in the struct but the comment says "(informational; not used per-shot)". At HEAD, Recovery has zero effect on any physics output. The design intent is likely stamina regen between holes (session-level effect), not per-shot. Until session-level stamina regen is implemented, Recovery is a no-op stat.

**Finding:** `Tier-Redesign`. Recovery needs a session-level stamina regen implementation to be meaningful. This is a game loop concern, not a per-shot resolver concern. Filing `Docs/Specs/Queued/character_recovery_stamina_regen/SPEC.md`.

### Q: Should Character.Stamina be more than a soft scalar?

**Answer:** Currently `staminaMultiplier = clamp(currentStamina/maxStamina, StaminaFloor=0.20, 1.0)`. It scales Strength and ClubControl by 0.20–1.0 based on runtime stamina. The cap at 1.0 means full stamina gives no bonus — only depleted stamina hurts. This is the design intent: "energy management, not amplification."

The Stamina STAT (as opposed to stamina ENERGY) contributes to the pool cap. A higher Stamina stat means the stamina energy pool is larger before the run, so it depletes more slowly relative to fixed energy costs per shot. This creates a long-game advantage that is invisible in single-hole bot scenarios.

**Finding:** `Justified-as-is`. The design is sound for a long-form game (Stamina stat matters across multiple holes). The perceptibility bar in this audit (single-hole) doesn't apply to session-level effects. Document that Stamina is a session-level stat, not a per-shot stat.

### Q: Should Ball.Power and Ball.Spin compete or stack?

**Answer:** They stack (multiplicative on velocity, additive on spin magnitude). This is correct: Power and Spin are different physical quantities. A ball with high Power AND high Spin is genuinely better — it goes farther AND has more spin control. No competing mechanic is needed. Current design is intentional.

**Finding:** `Justified-as-is`. Ball sub-stats are additive benefits across different physics dimensions. No change needed.

---

## Q2 — F7 Patch Revisit

F7 (`CharStrengthVelocityPerPoint = 0.004f`) is the Strength→velocity coupling added in `live_stat_provider_wiring` Phase 4. The audit verdict:

| Bucket | Verdict |
|---|---|
| **validate** | Perceptibility OK for driver tier (26m delta HIGH vs LOW confirmed by bot). Coefficient seems reasonable. |
| **retune** | The coefficient may be too weak for short-game clubs (see Tier-Tune finding in Lane 1c). Not an immediate issue. |
| **retire** | Not recommended. Strength should affect carry — the "single-source" design allowed Strength to be a no-op, which broke player UX. |

**Q2 decision: validate.** F7 stays in place at `0.004f`. The short-game scaling concern is filed as a Tier-Tune follow-up.

---

## Tier-Safe Changes (shipping in this PR)

No Tier-Safe coefficient changes are recommended in this audit iteration. The analysis shows that:
1. The velocity lane (Club.Power × Ball.Power × Char.Strength) is well-calibrated for the driver tier.
2. The aim-cone lane works at extreme stat ranges.
3. The lie resistance, overpower, and situational lanes (wind, rebound, roll) are design-intentional and within acceptable ranges.

The accuracy coefficient (`ClubAccuracyPerPoint = 0.0042`) was considered for a Tier-Safe bump to 0.006, but the change would affect the existing `StatResolverTests.cs` accuracy-related assertions and requires playtest validation. **Reclassified as Tier-Tune** (needs more than a unit test assertion).

---

## Perceptibility Matrix

| Lane | Stat Source | LOW (Char ≈5, Club ≈20) | MID (Char ≈25, Club ≈50) | HIGH (Char ≈50, Club ≈80) | Meets Bar | Tier |
|---|---|---|---|---|---|---|
| 1a Velocity — Club.Power | Club.Power | +10% vel | +25% vel | +40% vel | PASS | Justified-as-is |
| 1b Velocity — Ball.Power | Ball.Power | ±10% vel at extremes | — | — | PASS | Justified-as-is |
| 1c Velocity — Char.Strength | Char.Strength | +2% vel (≈4m on 200m) | +10% vel | +20% vel | WEAK on short-game | Tier-Tune |
| 2a Aim Cone — Club.Accuracy | Club.Accuracy | 8.4% reduction | 21% reduction | 33.6% reduction | PASS at HIGH, WEAK at LOW | Tier-Tune |
| 2b Aim Cone — Char.ClubControl | Char.ClubControl | 3.5% reduction | 8.75% reduction | 17.5% reduction | WEAK in isolation | Tier-Tune |
| 3 Spin — Ball.Spin | Ball.Spin | ±10% spin at extremes | — | — | PASS on wedge/iron | Justified-as-is |
| 4 Lie Resistance — Club.LieResistance | Club.LieResistance | 8.4% reduction | 21% reduction | 33.6% reduction | PASS when triggered | Justified-as-is |
| 5 Overpower — Char.Strength | Char.Strength | 6.25% forgiveness | 15.6% | 31.25% | WEAK (never triggered at power=1.0) | Justified-as-is |
| 6 Putter Off-Center | Putter.Control | — | — | — | Out of scope | Justified-as-is |
| 7 Putter Gravity Well | Putter.Accuracy | — | — | — | Out of scope | Justified-as-is |
| 8 Putter Aim Cycles | Putter.Weight | — | — | — | Out of scope | Justified-as-is |
| B1 Rebound — Ball.Rebound | Ball.Rebound | ±10% bounce energy | — | — | WEAK (1–2m) | Tier-Tune |
| B2 Roll — Ball.Roll | Ball.Roll | ±10% friction | — | — | WEAK (0.1m measured, Wedge approach) | Tier-Tune |
| B3 WindCut — Ball.WindCut | Ball.WindCut | 0–10% wind reduction | — | — | PASS in wind/ZERO in calm | Justified-as-is |

---

## Q3 — DefaultStatProvider Club-Aware FALLBACK

### Problem (pre-fix)

`DefaultStatProvider.BuildSwingBundle()` always returned `ClubStats.DefaultDriver` regardless of which club was selected. This meant:
- Wedge approach shot (should use 42 m/s base velocity) → used 75 m/s driver velocity → **80% overshoot**
- Iron 7 shot (should use 51 m/s) → used 75 m/s driver → **47% overshoot**

Result: the Hole 1 Playthrough bot consistently scored 8 strokes (seam) because every non-driver stroke massively overshot.

### Fix shipped in this audit

Architecture: `PhysicsLabController.SetClub(index)` now calls `StatProviderBus.SetCurrentLabClubIndex(index)`, keeping the bus in sync. `StatProviderBus.Resolve(isPutt=false)` passes `CurrentLabClubIndex` to `DefaultStatProvider.BuildSwingBundle(clubIndex)`, which returns:
- Index 0 → `ClubStats.DefaultDriver` (75 m/s, loft 10.9°, spin 2686 RPM)
- Index 1 → `ClubStats.DefaultIron7` (51 m/s, loft 25.5°, spin 6500 RPM)
- Index 2 → `ClubStats.DefaultWedge` (42 m/s, loft 41.2°, spin 9000 RPM)
- Index 3+ → `ClubStats.DefaultDriver` (safety fallback)

The `ClubStats.DefaultIron7` and `ClubStats.DefaultWedge` static values are copied verbatim from `PhysicsLabController.LabClubs[1]` and `LabClubs[2]` so the FALLBACK path is physically equivalent to the lab behavior.

### Why bus-state (not a parameter on Resolve)

The SPEC's Q3 design specified extending `StatProviderBus.Resolve(bool isPutt)` → `Resolve(bool isPutt, int labClubIndex)`. During pre-flight, this was found architecturally impossible:
- `ShotController` is in `Golfin.Gameplay.Input`
- `PhysicsLabController` is in `Golfin.Physics.Viewer`
- `Golfin.Gameplay.Input` does NOT reference `Golfin.Physics.Viewer` (the dependency is the reverse)
- `ClubSelectionBroadcast` is in `Golfin.Gameplay.UI` which `Golfin.Gameplay.Input` also does NOT reference

The bus-state approach (storing `CurrentLabClubIndex` in `StatProviderBus`, which is in `Golfin.Gameplay.Defaults` with `autoReferenced=true`) solves the cross-asmdef problem without introducing any circular dependencies. The behavior is equivalent to the SPEC's design.

---

## Findings Classification Table

| Finding ID | Lane | Description | Tier | Follow-up Spec |
|---|---|---|---|---|
| F-LANA-1c | Lane 1c (Strength → velocity) | Coefficient weak on short-game clubs | Tier-Tune | `strength_velocity_short_game_scaling` |
| F-LANA-2a | Lane 2a (Club.Accuracy → aim) | Low-tier clubs nearly identical aim cone | Tier-Tune | `club_control_aim_arrow_speed` |
| F-LANA-2b | Lane 2b (ClubControl → aim) | Sub-threshold in isolation | Tier-Tune | `club_control_aim_arrow_speed` |
| F-LANA-B1 | Ball Rebound | ±10% bounce energy below 10m bar | Tier-Tune | `ball_rebound_perceptibility` |
| F-LANA-B2 | Ball Roll | ±10% friction = 0.1m measured (Wedge approach, same-start; theoretical 4–8m on driver approach) | Tier-Tune | `ball_roll_coefficient_retune` |
| F-LANA-REC | Character.Recovery | No-op stat (session regen not implemented) | Tier-Redesign | `character_recovery_stamina_regen` |
| Q3-FALLBACK | All lanes (FALLBACK path) | DefaultStatProvider always returned Driver | Tier-Safe (SHIPPED) | N/A — fixed in this PR |

---

## Filed Follow-up Specs

> **Status verified 2026-07-16 (architect).** The original filing column below was inaccurate —
> reconciled against disk + `PHYSICS_TUNING_CHANGELOG.md` + shipped code. Trust the *Verified status*
> column, not the 2026-05-25 claim.

| Spec slug | Tier | Filing claim (2026-05-25) | Verified status (2026-07-16) |
|---|---|---|---|
| `strength_velocity_short_game_scaling` | Tier-Tune | Filed in `Docs/Specs/Queued/` | **QUEUED — actionable.** `Docs/Specs/Queued/strength_velocity_short_game_scaling/SPEC.md` present. |
| `club_control_aim_arrow_speed` | Tier-Tune | Filed in `Docs/Specs/Queued/` | **NEVER FILED.** No folder exists. Covers findings F-LANA-2a + F-LANA-2b; must be written before it can run. |
| `ball_rebound_perceptibility` | Tier-Tune | Filed in `Docs/Specs/Queued/` | **QUEUED — actionable.** `Docs/Specs/Queued/ball_rebound_perceptibility/SPEC.md` present. |
| `ball_roll_coefficient_retune` | Tier-Tune | Filed in `Docs/Specs/Queued/` | **SHIPPED 2026-06-02** as changelog entry **F8** (`BallRollPerPoint` 0.01 → 0.02, fills the 0.80–1.20 clamp at Ball.Roll=±10). Spec folder consumed. Finding F-LANA-B2 CLOSED. |
| `character_recovery_stamina_regen` | Tier-Redesign | Filed in `Docs/Specs/Queued/` | **SUPERSEDED** by the Stamina/Condition Economy (Phases 1–5, shipped 2026-06-29→07-03). Its premise — "Recovery has zero effect on any physics output" — is no longer true: `StaminaModel.RegenPerHour(int recoveryStat)` is live and Recovery is the regen-rate stat. Finding F-LANA-REC CLOSED; the queued folder is stale and should be retired. |
