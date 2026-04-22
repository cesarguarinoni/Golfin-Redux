# Physics Tuning Targets — Source of Truth

**Status:** Living document. Updated as design decisions land.
**Source design sheet:** `New_Levels.xlsx` (Cesar's design guide — not source of truth, this doc is)
**Companion:** `Docs/PHYSICS_RESEARCH.md` (architecture decisions)
**Last updated:** 2026-04-21

---

## Purpose

This file is the canonical reference for all physics-affecting numbers in Golfin Redux: club distances, stat-to-physics mappings, RP costs, surface coefficients, etc.

The CSVs under `Assets/Resources/Physics/` derive from this doc. When a number changes, update this doc first, then propagate to CSVs.

---

## 1. Club carry distances (yards)

Linear mapping: **1 stat point = 6 yards of carry**. Min = 0 power points spent, Max = 20 power points spent (single rarity tier worth).

Numbers calibrated against PGA Tour averages (Tour-pro carry sits near the Max column). See "Realism alignment" notes below.

| Club       | Min | Max | Avg | Notes |
|---|---|---|---|---|
| Driver     | 225 | 345 | 285 | |
| Wood 3     | 200 | 320 | 260 | |
| Wood 5     | 170 | 290 | 230 | |
| Wood 7     | 165 | 285 | 225 | |
| Iron 3     | 145 | 265 | 205 | |
| Iron 4     | 135 | 255 | 195 | **Fixed from sheet** (was 220, broke iron progression) |
| Iron 5     | 140 | 260 | 200 | |
| Iron 6     | 125 | 245 | 185 | |
| Iron 7     | 112 | 232 | 172 | **Fixed from sheet** (was 200, broke iron progression) |
| Iron 8     | 105 | 225 | 165 | |
| Iron 9     | 95  | 215 | 155 | |
| A. Wedge   | 66  | 186 | 126 | |
| P. Wedge   | 80  | 200 | 140 | |
| S. Wedge   | 50  | 170 | 110 | |

### Realism alignment

Avg-column numbers track PGA Tour averages within ~10yd on most clubs. This means:
- A Common-tier club at base level plays roughly like an amateur
- A maxed-out Supreme club plays beyond Tour-pro range (allows progression headroom)
- Tournament-grade gameplay (mid-Mythic onward) sits in the realistic Tour-pro band

### ⚠️ Linear mapping — flagged for future revisit

**Decision (2026-04-21):** keep linear "1 point = 6yd" across all clubs. Easy for designers to reason about and CSV-tune.

**Deviation from real golf:** real club power scales nonlinearly — driver distance is dominated by club head speed (linear in club length), while wedge distance is more about consistency at high spin. A "perfect" sim would have drivers gain more yards per power point than wedges, and wedges gain more spin/control per point.

**Revisit trigger:** if playtesting shows wedge-heavy strategies feel undifferentiated, or driver progression feels flat, return here and consider per-club power coefficients.

---

## 2. Club stats → physics modifiers

Each club has 5 stats. Each stat is 0–20 points per rarity tier. Stats are cumulative across tiers (Supreme caps at 120 effective points across all six tiers).

| Stat | Range | Per-point effect | Physics target |
|---|---|---|---|
| **Power**          | 0–20 | +6 yards carry | Initial ball velocity / club head speed multiplier |
| **Accuracy**       | 0–20 | -4.25° aim cone | Aim reticle size shrinks (10° at 0, → ~180°/extreme cone clamp at high values per sheet) |
| **Lie Resistance** | 0–20 | +0.42% terrain penalty reduction | Reduces variance applied to shots from rough/sand/etc (max 50%) |
| **Durability**     | 0–20 | +2.5 hits | Club lifespan (20 base, 120 max) |
| **Loft**           | n/a  | Random (fixed at club spawn) | Launch angle in degrees |

### Notes on the Accuracy stat

The sheet says "10°–180°, 4.25° per point" which reads as "0 stat = 10° cone, 20 stat = ~95° cone reduction" (smaller cone = more accurate). Confirm directionality with Cesar in implementation phase — lower stat should = wider miss cone.

### Notes on Loft

Listed as "Random (fixed)" — meaning loft is randomized when the club instance is created, then locked. This is a club-individuality feature, not a per-shot stat. Valid loft ranges per club type need defining (drivers ~9–13°, 7-iron ~30–34°, sand wedge ~54–58°, etc.) — TBD.

---

## 3. Putter stats → physics modifiers

| Stat | Range | Per-point effect | Physics target |
|---|---|---|---|
| **Control**             | 0–20 | +0.42% off-center forgiveness | Reduces side-spin penalty on off-center hits (max 50%) |
| **Accuracy**            | 0–20 | +0.0225m attract distance | "Gravity well" radius around the hole (0.1m–1.0m) |
| **Weight**              | 0–20 | +0.15 aim cycles | Number of aiming oscillations player gets before commit (5–20) |
| **Durability**          | 0–20 | +2.5 hits | Putter lifespan (20–120) |
| **Loft**                | n/a  | Random (fixed) | Slight launch angle even on putts |

### Notes on Accuracy "gravity well"

This is a non-physical assist mechanism — the ball gets pulled subtly toward the cup when within the attract radius. Architectural rule: this is an **assist layer**, not part of the deterministic physics sim. Toggleable when assist toggle is implemented. Disabled in tournament/competitive modes.

---

## 4. Character stats → physics modifiers

| Stat | Range | Per-point effect | Physics target |
|---|---|---|---|
| **Strength**     | 0–20 | +0.625% overpower error reduction | When player overshoots power gauge, the penalty is reduced (max 75%) |
| **Club Control** | 0–20 | +0.83% arrow speed reduction | Slows the moving aim arrow, easier to time the shot (max 50% slower) |
| **Recovery**     | 0–20 | +1.25% stamina/hour | Stamina regeneration rate between sessions (50%–100%/hr) |
| **Stamina**      | 0–20 | +0.83% stamina cap | Max stamina pool (20%–120%) |

### Stamina coupling

Stamina degrades during a round (per Confluence: drops after each hole, affects performance). Low stamina degrades Strength/Club Control/Recovery effective values. Implementation: each shot reads `effective_stat = base_stat × stamina_multiplier(current_stamina)`.

---

## 5. Ball stats (per memory)

Range: -10 to +10. No rarity system. Ball lasts one hole (consumable).

Ball stats not in this design sheet — **TBD**. Likely candidates based on Confluence references:
- Power (carry distance modifier, ±10%)
- Accuracy (aim variance modifier)
- Spin (max spin rate modifier)
- Control (lie resistance modifier)
- ... others?

Action item: get ball stat list + ranges from Cesar before Phase 2 spec.

---

## 6. RP cost curve (level-up economy)

Linear: **Lv N costs (N × 5) RP per stat point**.

| Lv | RP/pt | | Lv | RP/pt | | Lv | RP/pt |
|---|---|---|---|---|---|---|---|
| 1  | 5   | | 11 | 55  | | 21 | 105 |
| 2  | 10  | | 12 | 60  | | 30 | 150 |
| 3  | 15  | | 13 | 65  | | 40 | 200 |
| 5  | 25  | | 15 | 75  | | 50 | 250 |

Total RP to max a single rarity tier (40 levels × 20 points each, summed):
- Common (Lv 10 → 39): **3,675 RP**
- Uncommon (Lv 40 → 79): **12,300 RP**
- Rare (Lv 80 → 119): **19,900 RP**
- Mythic (Lv 120 → 159): **27,900 RP**
- Legendary (Lv 160 → 199): **35,900 RP**
- Supreme (Lv 200 → 239): **43,900 RP**

Designer assumption (per sheet): 250 RP per mission, 5 min/mission, 50 RP/min, 750 RP/session (15min sessions), so:
- Common max → 4.9 sessions (~1.2 hr)
- Supreme max → 58.5 sessions (~14.6 hr)

**Physics relevance:** the time-to-max curve is what calibrates how quickly players access new physics envelopes. A player with a Mythic driver should take ~9 hours of play to fully tune it. This sets pacing for the gameplay→physics-improvement loop.

---

## 7. Realism alignment notes (carry distances)

Source: PGA Tour Trackman averages (publicly published).

| Club | Game Avg (yd) | PGA Tour avg (yd) | Amateur avg (yd) |
|---|---|---|---|
| Driver   | 285 | ~275 | 215 |
| 3-Wood   | 260 | ~243 | 195 |
| 5-Wood   | 230 | ~230 | 185 |
| 7-Wood   | 225 | ~217 | 175 |
| 3-Iron   | 205 | ~211 | 180 |
| 4-Iron   | 195 | ~203 | 170 |
| 5-Iron   | 200 | ~194 | 160 |
| 6-Iron   | 185 | ~183 | 150 |
| 7-Iron   | 172 | ~172 | 140 |
| 8-Iron   | 165 | ~160 | 130 |
| 9-Iron   | 155 | ~148 | 120 |
| A-Wedge  | 126 | ~135 | 110 |
| P-Wedge  | 140 | ~136 | 110 |
| S-Wedge  | 110 | ~106 | 80 |

Game averages sit at PGA Tour average ± ~10yd for most clubs. Acceptable for game purposes.

---

## 8. Stat stacking model — Specialized Roles (Option D)

**Decision (2026-04-21):** Each stat governs a **distinct physics input**. Stats stack multiplicatively only when they genuinely overlap on the same input. No "everything multiplies everything" stacking.

This matches Cesar's existing design intent in `New_Levels.xlsx` — character Strength is "overpower error reduction," not "extra yards." Stats are not redundant; they each have their own lane.

### Stat → physics-input map

| Physics input | Driven by | Notes |
|---|---|---|
| **Initial velocity (yards)** | Club Power × Ball Power | Multiplicative — same lane. Ball Power is narrow (±10%). |
| **Aim cone (accuracy)**       | Club Accuracy × Character Club Control | Multiplicative. Club shrinks the cone, character slows the arrow. Both reduce miss distance. |
| **Lie penalty reduction**     | Club Lie Resistance × Ball Control (TBD) | Multiplicative if both apply. Cap at 75%. |
| **Spin rate**                 | Ball Spin (TBD) × Club Loft | Loft determines spin axis baseline; ball tunes magnitude. |
| **Overpower forgiveness**     | Character Strength | **Single-source.** No other stat affects this. |
| **Stamina cap / regen**       | Character Stamina / Recovery | Single-source each. |
| **Aim cycle count (putter)**  | Putter Weight | Single-source. Putter-only mechanic. |
| **Off-center forgiveness (putter)** | Putter Control | Single-source. Putter-only mechanic. |
| **Hole gravity-well (putter)** | Putter Accuracy | **Assist layer**, not physics. Toggleable. |

### What this means in practice

Endgame example: Supreme driver (Power 120) + +10 Power ball + maxed Strength character + full stamina:
- **Velocity:** `base × (1 + 120×0.005) × (1 + 10×0.01) = base × 1.60 × 1.10 = 1.76× base`
  - (powerCoeff for clubs is 0.005 per stat point assuming 120-point cap = +60% velocity. Club avg → max range from sheet = +21% yards, so 0.005 may need to be ~0.0017 to match. Numbers TBD in calibration phase — the **principle** is what's locked.)
- **Aim:** `base_cone × (1 - 120×0.0035) × (1 - 120×0.0042)` = aggressive shrinkage, but each component caps individually.
- **Strength does NOT add velocity.** It only changes what happens when you push the gauge past 100%.
- **Stamina does NOT add velocity.** It scales the *effective* level of the character stats it gates (Strength, Club Control, Recovery).

### Why this is the right shape

- **Each stat upgrade has a felt purpose.** Investing in Strength has a different gameplay outcome than investing in Club Control. No "I have no idea what this stat does, all the bars look the same" problem.
- **Endgame numbers stay sane.** Multiplicative stacking only happens when stats genuinely share a lane (Club Power × Ball Power). Even fully maxed, the worst-case stack is ~2.0× base — no ridiculous 3-4× outliers.
- **Easier to balance.** Designers can tune one input at a time without touching the others. CSV-tunable per stat.
- **Harder to pay-to-win-stack.** No single category dominates. A maxed Supreme driver alone doesn't give the player a 3.3× endgame — they also need ball, character, stamina management to extract full value. Each axis is meaningful.
- **Cleaner rebalancing.** If wedges feel weak, tune wedge Power coefficient — doesn't ripple into Accuracy, Lie Resistance, Strength, etc.

### Hard caps (sanity ceilings)

Even within a single lane, apply soft caps per Cesar's existing design intent:
- **Velocity multiplier:** soft cap at 2.0× base (anything above is wasted points)
- **Aim cone reduction:** soft cap at 95% reduction (some miss is always possible; pure-zero cone removes shot variance, kills skill expression)
- **Lie resistance:** hard cap at 75% (matches Strength's overpower cap pattern)
- **Stamina cap:** hard cap at 120% (per sheet)
- **Off-center forgiveness:** hard cap at 50% (per sheet)

Caps live in `Assets/Resources/Physics/stat_caps.csv`. Tunable. Per-stat.

---

## 9. Physics sim implications

The numbers above feed the deterministic sim as follows:

1. At `ShotInput` construction, the controller resolves the **effective inputs**, not raw stats:
   - `velocityModifier` = `(1 + clubPower × pCoeff) × (1 + ballPower × bpCoeff)` — clamped at velocity cap
   - `aimConeRadians` = `baseConeForClub × (1 - clubAccuracy × aCoeff) × (1 - charClubControl × ccCoeff)` — clamped at min cone
   - `lieResistance` = `(1 - clubLR × lrCoeff) × (1 - ballControl × bcCoeff)` — clamped at 75%
   - `overpowerForgiveness` = `min(0.75, charStrength × 0.00625)` — single-source
   - `staminaScalar` = `f(currentStamina, charStamina)` — applies to the character-driven inputs above as a pre-multiplier on `charStrength`/`charClubControl` BEFORE the modifiers above are computed.

2. The sim consumes only the resolved modifiers, not the raw stats. This means:
   - The deterministic sim signature stays clean: `Trajectory Simulate(ShotInput input, GroundProvider ground)`.
   - Stat math is gameplay-layer concern, not physics-layer.
   - When designers retune coefficients, only the modifier-resolution layer changes, not the sim.

3. All coefficients (`pCoeff`, `aCoeff`, `lrCoeff`, etc.) and caps live in `Assets/Resources/Physics/stats.csv`. Designers tune in Unity, no recompile.

---

## 10. Open items

- [ ] Confirm Accuracy stat directionality (lower stat = wider miss cone — confirm)
- [ ] Loft random ranges per club type (drivers, irons, wedges)
- [ ] Ball stat list and ranges
- [ ] When the linear power mapping is revisited, document new per-club coefficients here
- [ ] Stamina degradation curve (per-hole drop and per-shot consumption)
- [ ] How rarity-locked stat caps interact with progression (Supreme starts at Lv 200; can a Common-rarity club ever exceed Common stat caps via use-based level-up?)
- [ ] Final per-stat coefficients (the 0.005, 0.01, etc. in section 8) — calibrate during Phase 2 against carry-distance targets in section 1.

---

## 11. Changelog

- **2026-04-21** — Initial doc. Synced from `New_Levels.xlsx`. Fixed Iron 4 (220→195) and Iron 7 (200→172) typos that broke iron progression. Linear power mapping flagged for revisit.
- **2026-04-21** — Section 8 added. Locked Specialized Roles (Option D) stat-stacking model. Each stat governs a distinct physics input; multiplicative stacking only within shared lanes; hard caps per stat.
