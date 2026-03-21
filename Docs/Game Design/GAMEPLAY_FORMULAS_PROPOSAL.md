# GOLFIN Redux — Simplified Gameplay Formulas Proposal

**Author:** Claude (Architect)  
**Date:** 2026-03-21  
**Status:** PROPOSAL — Review before implementation  
**Reference:** Old Gameplay Formulas.xlsx, Old Control.docx, New Levels.xlsx

---

## Design Philosophy

The old system used deeply nested formulas with square roots, multiple randomizers, accuracy coefficients, shot power multipliers, spin calculations, recovery coefficients, and terrain modifiers — all interacting with each other in ways that were hard to predict, tune, or debug.

The new system follows three rules:
1. **Every stat maps to exactly one gameplay effect** — no stat affects two things
2. **All formulas are linear** — multiply or add, never sqrt, pow, or log
3. **Players can predict outcomes** — if I invest 1 point in Power, I know I'll get X more yards

---

## 1. Club Stats — What Each Does

### POWER → Distance (yards)

The simplest and most important formula. Power determines how far the ball goes.

```
Max Distance = ClubBaseDistance + (PowerPoints × PointsPerYard)
Actual Distance = Max Distance × SwingPower%
```

Where:
- `ClubBaseDistance` = the minimum distance for that club type at 0 Power points (from New Levels.xlsx Min column)
- `PowerPoints` = total invested Power SP (0-20)
- `PointsPerYard` = 1 yard per point (from spreadsheet — so 20 SP = 20 extra yards)
- `SwingPower%` = how far the player pulled back (0.0 to 1.0 for normal, up to 1.2 for overpower)

**Example: Driver, Common, 10 Power points**
- Base: 225 yd (Min from spreadsheet)
- Bonus: 10 × 1 = 10 yd
- Max distance at 100% swing: 235 yd
- At 80% swing: 188 yd

**Distance ranges by club type (from New Levels.xlsx):**

| Club | Min (0 Power) | Max (20 Power) | Per Point |
|---|---|---|---|
| Driver | 225 | 345 | 6.0 |
| Wood 3 | 200 | 320 | 6.0 |
| Wood 5 | 170 | 290 | 6.0 |
| Wood 7 | 165 | 285 | 6.0 |
| Iron 3 | 145 | 265 | 6.0 |
| Iron 4 | 160 | 280 | 6.0 |
| Iron 5 | 140 | 260 | 6.0 |
| Iron 6 | 125 | 245 | 6.0 |
| Iron 7 | 140 | 260 | 6.0 |
| Iron 8 | 105 | 225 | 6.0 |
| Iron 9 | 95 | 215 | 6.0 |
| A. Wedge | 66 | 186 | 6.0 |
| P. Wedge | 80 | 200 | 6.0 |
| S. Wedge | 50 | 170 | 6.0 |

> NOTE: The spreadsheet says "Points per Yards = 1" but the actual range (Max - Min = 120) divided by 20 points = 6 yards per point. The "1" likely means 1 Power point = 1 unit of distance scaling. For implementation, use `(Max - Min) / 20` to get the actual yards per SP. I've used 6.0 above.

### ACCURACY → Fade/Draw Control Angle

Accuracy determines the maximum angle the player can curve the ball intentionally (fade = right curve, draw = left curve). Higher accuracy = wider control angle = more shot-shaping options.

```
MaxFadeDrawAngle = MinAngle + (AccuracyPoints × PointsPerAngle)
```

Where:
- `MinAngle` = 10° (minimum curve control at 0 Accuracy — almost straight only)
- `MaxAngle` = 180° (full fade/draw range at 20 Accuracy — from spreadsheet)
- `PointsPerAngle` = (180 - 10) / 20 = 8.5° per point
- Positive angle = fade (right), Negative = draw (left)
- The player selects curve direction and magnitude via a UI slider (0 to MaxAngle each way)

**How it works in gameplay:**
- At 0 Accuracy: player can only curve ±10° — basically straight shots
- At 10 Accuracy: player can curve ±95° — moderate shot shaping
- At 20 Accuracy: player can curve ±180° — full hook/slice control

**No randomness in accuracy.** The ball curves exactly where the player aims. The skill expression comes from CHOOSING the right curve, not from random deviation. This replaces the old system's "accuracy coefficient" and "accuracy deviation" which added unpredictable scatter.

### TERRAIN RESISTANCE → Penalty Reduction

When hitting from rough, sand, deep rough, or other bad lies, the ball loses distance and accuracy. Terrain Resistance reduces this penalty.

```
TerrainPenalty = BasePenalty × (1 - TerrainResistancePoints × PenaltyReductionPerPoint)
```

Where:
- `BasePenalty` = terrain-specific multiplier:
  - Fairway: 0% penalty (1.0× distance)
  - Semi-rough: 15% penalty (0.85× distance)
  - Rough: 30% penalty (0.70× distance)
  - Deep rough: 50% penalty (0.50× distance)
  - Bunker: 40% penalty (0.60× distance)
- `TerrainResistancePoints` = 0-20
- `PenaltyReductionPerPoint` = 2.5% per point (from spreadsheet: max 50% reduction at 20 points)
- Penalty can never go below 0% (full resistance caps at removing the terrain penalty entirely)

**Example: Hitting from Rough with 12 Terrain Resistance points**
- Base penalty: 30%
- Reduction: 12 × 2.5% = 30%
- Effective penalty: 30% × (1 - 0.30) = 21%
- Distance multiplier: 0.79×

**Example: Hitting from Rough with 20 Terrain Resistance points (maxed)**
- Base penalty: 30%
- Reduction: 20 × 2.5% = 50%
- Effective penalty: 30% × (1 - 0.50) = 15%
- Distance multiplier: 0.85×

Note: Even at max Terrain Resistance, rough still penalizes you — it just hurts less. You can never fully negate terrain.

### DURABILITY → Performance Degradation

Durability is a consumable stat that depletes with use. As it drops, ALL other club stats are proportionally reduced.

```
EffectiveStat = BaseStat × DurabilityMultiplier

DurabilityMultiplier = 0.5 + (0.5 × CurrentDurability / MaxDurability)
```

This means:
- At 100% durability: multiplier = 1.0 (full performance)
- At 50% durability: multiplier = 0.75 (25% stat reduction)
- At 0% durability: multiplier = 0.50 (50% stat reduction — club still works but badly)

**Durability depletion:**
- 1 durability point lost per hole played
- Durability SP (0-20) determines the MaxDurability value:
  - Min: 20 holes (0 Durability SP)
  - Max: 120 holes (20 Durability SP)
  - Per point: 5 extra max durability (from spreadsheet: `(120-20)/20 = 5`)

**Repair restores CurrentDurability to MaxDurability** (costs Repair Kits, not RP).

### LOFT → Fixed by Club Type

Loft is NOT a stat the player invests in. It's a fixed property of each club type that determines the launch angle / trajectory arc. It affects how the ball flies (high arc vs low line drive) but is not player-controlled.

| Club Type | Loft |
|---|---|
| Driver | 12° |
| Wood 3 | 15° |
| Wood 5 | 18° |
| Wood 7 | 21° |
| Iron 3-5 | 24-28° |
| Iron 6-8 | 30-36° |
| Iron 9 | 40° |
| P. Wedge | 45° |
| A. Wedge | 50° |
| S. Wedge | 56° |
| Putter | 3° |

This is used by the physics engine for trajectory calculation. Players don't allocate SP to Loft.

---

## 2. Character Stats — What Each Does

### STRENGTH → Overpower Error Margin

Overpower = swinging past 100%. More Strength means less penalty when overpowering.

```
OverpowerPenalty% = OverpowerAmount% × (1 - StrengthPoints × ReductionPerPoint)
```

Where:
- `OverpowerAmount%` = how far past 100% the player swung (0-20%)
- `StrengthPoints` = 0-20
- `ReductionPerPoint` = 3.75% per point (from spreadsheet: max 75% error reduction at 20 points)
- The penalty manifests as random directional deviation (left/right scatter)

**Example: Player overswings to 115% power with 10 Strength**
- Overpower amount: 15%
- Reduction: 10 × 3.75% = 37.5%
- Effective error: 15% × (1 - 0.375) = 9.375%
- Ball deviates up to ±9.375° from intended direction

**Example: Same overswing with 20 Strength (maxed)**
- Effective error: 15% × (1 - 0.75) = 3.75%
- Much tighter shot — overpower is nearly free

**At 0 Strength:**
- Overpowering to 120% gives 20% error — massive deviation
- This makes overpowering risky for weak characters, rewarding investment

### CLUB CONTROL → Arrow Speed (Timing System)

The shot timing system uses converging arrows/circles that the player must tap at the right moment. Club Control determines how fast these arrows move — slower = easier to time.

```
ArrowSpeed = MaxSpeed × (1 - ClubControlPoints × SpeedReductionPerPoint)
```

Where:
- `MaxSpeed` = fastest arrow speed (hardest — at 0 Club Control)
- `ClubControlPoints` = 0-20
- `SpeedReductionPerPoint` = 2.5% per point (max 50% speed reduction at 20 points)
- Minimum speed is capped at 50% of max (never trivially easy)

**Example: Max speed = 100 units/sec, 15 Club Control**
- Reduction: 15 × 2.5% = 37.5%
- Arrow speed: 100 × (1 - 0.375) = 62.5 units/sec

The timing window (how close to perfect you need to be) stays constant — Club Control only affects how fast the arrows approach, giving the player more time to react.

### STAMINA REGEN → Recovery Rate

How quickly stamina energy regenerates over real time.

```
RecoveryPerHour% = MinRecovery + (StaminaRegenPoints × RecoveryPerPoint)
```

Where:
- `MinRecovery` = 50% per hour (from spreadsheet)
- `MaxRecovery` = 100% per hour (at 20 points — full recovery in 1 hour)
- `RecoveryPerPoint` = (100 - 50) / 20 = 2.5% per point
- Stamina regenerates passively in real time, even when not playing

### STAMINA → Character Durability

Works identically to club Durability but for the character. As stamina energy depletes, character stats (Strength, Club Control) degrade.

```
EffectiveCharStat = BaseCharStat × StaminaMultiplier

StaminaMultiplier = 0.5 + (0.5 × CurrentStamina / MaxStamina)
```

- Min stamina: 20 (0 Stamina SP) — enough for ~20 holes
- Max stamina: 120 (20 Stamina SP) — enough for ~120 holes
- Per point: 5 extra max stamina
- 1 stamina depleted per hole
- When low, character plays worse (slower regen, less strength)

---

## 3. Shot Execution Flow (Simplified)

The old system had 4+ inputs per shot (zone drop, direction slide, power pullback, flick timing, spin selection). The new system has 3:

```
1. AIM    → Tap on the map to set target direction
2. CURVE  → Optional: set fade/draw amount (limited by Accuracy stat)
3. SWING  → Pull back to set power (0-120%), tap at right moment for timing
```

### Step 1: Aim
Player taps on the course map to set where the ball should go. A landing prediction circle shows the estimated landing zone based on current club's max distance at 100% power.

### Step 2: Curve (Optional)
If the player wants to fade or draw, they adjust a slider. The maximum curve angle depends on Accuracy stat. Most casual players can skip this entirely.

### Step 3: Swing
Player pulls back on the swing meter to set power (0-120%). Pulling past 100% enters overpower territory — more distance but Strength-dependent error. Then the timing arrows converge — tap when they align for a perfect shot. Arrow speed depends on Club Control stat.

### Final Distance Calculation

```
RawDistance = ClubBaseDistance + (PowerSP × YardsPerPoint)
PoweredDistance = RawDistance × SwingPower%
TerrainAdjusted = PoweredDistance × (1 - TerrainPenalty)
DurabilityAdjusted = TerrainAdjusted × DurabilityMultiplier
StaminaAdjusted = DurabilityAdjusted × StaminaMultiplier

FinalDistance = StaminaAdjusted
```

No randomizer. No square roots. No accuracy coefficients. The ball goes exactly where the math says. Skill expression comes from the player's choices (club selection, power, curve) and timing (arrow tap), not from hidden RNG.

### Directional Deviation (only from overpower or bad timing)

```
TimingError° = (1 - TimingAccuracy) × MaxTimingDeviation
OverpowerError° = OverpowerAmount% × (1 - StrengthReduction)
TotalDeviation° = TimingError° + OverpowerError°

FinalDirection = AimedDirection ± random(0, TotalDeviation°)
```

Where:
- `TimingAccuracy` = 0.0 (missed completely) to 1.0 (perfect tap)
- `MaxTimingDeviation` = 15° (worst case for completely missed timing)
- Overpower error as described in Strength section
- The ± random is the ONLY randomness in the entire system, and it's bounded by player skill

---

## 4. Comparison: Old vs New

| Aspect | Old System | New System |
|---|---|---|
| Distance formula | Base + power/2 + strength/2 + 5%×spin + randomizer(1-15) | Base + (PowerSP × 6) × SwingPower% |
| Accuracy | Coefficient × deviation × recovery interaction | Accuracy → fade/draw angle only. No scatter. |
| Terrain | Complex recovery coefficients, separate for accuracy and power | Simple % penalty × (1 - resistance%) |
| Spin | 5 types (top/back/no/calculated/side), each with sub-formulas | Removed from stat system. Fixed by club type loft. |
| Randomness | Randomizer(1-15) on every shot + accuracy deviation | Only from bad timing or overpower. Bounded. |
| Overpower | Complex acceleration of aiming circles + accuracy coefficient | Simple: more distance, Strength-dependent scatter |
| Durability | Not clear from old formulas | Linear degradation: 0.5 + 0.5 × (current/max) |
| Stamina | Character stamina existed but interaction unclear | Mirrors club durability. Degrades character stats. |
| Shot inputs | Zone drop + direction slide + power pull + flick + spin | Aim tap + optional curve + power/timing swing |

---

## 5. Putter — Special Case

Putters use different stats but the same formula philosophy:

| Putter Stat | Effect | Formula |
|---|---|---|
| Control | Forgiveness for off-center hits (reduces penalty) | `Penalty × (1 - ControlSP × 2.5%)` |
| Accuracy | "Gravity well" — ball attracted to hole from short distance | `AttractDistance = 0.1m + AccuracySP × 0.0225m` |
| Weight | Extra aiming cycles before arrow resets | `Cycles = 5 + WeightSP × 0.15` |
| Durability | Same as regular clubs | Same formula |

---

## 6. Implementation Notes

All formulas use only `+`, `-`, `×`, `/` — no `Mathf.Sqrt`, `Mathf.Pow`, or `Mathf.Log`. Every value is deterministic except the bounded random deviation from bad timing/overpower.

Constants to define in a `GameplayConstants.cs` or CSV:
- Per-club base distances (from New Levels.xlsx)
- Per-club loft angles (fixed table)
- Terrain penalty percentages (per terrain type)
- Durability degradation formula parameters
- Arrow speed range for Club Control
- Overpower error reduction per Strength point

All these are tunable without code changes — put them in a CSV or ScriptableObject for live balancing.

---

## 7. Open Questions for Later

1. **Wind:** Not addressed here. Should wind be a simple additive offset to final position, or affect trajectory during flight?
2. **Spin:** Removed from the stat system (loft is fixed). Should there be any player-controlled spin beyond fade/draw? The old system had top/back/side spin.
3. **Ball types:** The old system mentions "ball power parameter." Should different balls modify distance, spin, or other properties?
4. **Elevation:** Uphill/downhill affects distance. Simple multiplier (0.9× uphill, 1.1× downhill) or more nuanced?
5. **Club degradation during a round:** Does durability drop per hole or per shot? Current proposal says per hole.
