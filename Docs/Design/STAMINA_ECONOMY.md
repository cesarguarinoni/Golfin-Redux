# STAMINA ECONOMY — Design

**Status:** Design locked, pre-spec
**Author:** Architect (Claude.ai)
**Last updated:** 2026-06-29 22:35 JST
**Companion data:** `Docs/Design/stamina_economy.csv` (tunables)

---

## 1. Concept

Stamina is a **condition layer, not an energy gate.** It never blocks play. Playing
holes drains a character's *Condition*; low Condition **degrades their other stats**,
down to a floor. Condition refills passively over real time. The strategic payoff is
**roster rotation** — pace your star or rotate in a fresh character, exactly like
squad rotation in a football-management sim (the genre this is modelled on:
condition drains per match, recovers between, with a dedicated recovery-rate
attribute).

**Same rules in and out of tournaments.** One shared formula. Two separate pools:
the live pool (free play / 1v1) and the tournament pool (seeded from the frozen
snapshot at Register, isolated). Draining one never touches the other.

**Anti-pattern guardrails** (the failure mode the genre is infamous for — the
"rest simulator" that punishes you for playing):
- Negligible for the first several holes — you never feel it early.
- Recovery is **passive and real-time** — you do nothing but come back later.
- Never unplayable — hard floor on the penalty.
- Transparent — the degradation and the rest state are always visible on the roster.

---

## 2. Core model

Each character has a **Condition** value, `0 … MaxCondition`.

- **Tank size** `MaxCondition` is set by the **Stamina stat** (decision #4):
  higher Stamina = bigger tank = more holes before the penalty zone.
- **Drain** is a **flat per-hole cost**, applied **at hole completion** (never
  mid-hole). Stats are recomputed at the *start* of each hole and held constant
  through it, so a hole always plays at a stable feel; the step-down happens between
  holes.
- **Condition %** = `Condition / MaxCondition`. The penalty curve reads this %, so a
  bigger tank spends more holes in the comfort zone.
- **Penalty** reduces **Strength + Club Control only** (decision #1 — Stamina is the
  tank governor and Recovery is the regen governor, so degrading either creates a
  death-spiral). Smooth curve from 0 penalty in the comfort zone down to a **~67%
  floor** at empty (decision #2/#3).
- **Regen** is **passive real-time**, rate set by the **Recovery stat** (decision #4).
  Timestamp + elapsed on load.

```
MaxCondition   = TankBase + StaminaStat * TankPerStaminaPoint
ConditionPct   = Condition / MaxCondition
DrainPerHole   = DrainPerHole                       (flat, applied at hole complete)
RegenPerHour   = RegenBase  + RecoveryStat * RegenPerRecoveryPoint
```

**Penalty (applied to Strength + Club Control):**
```
if ConditionPct >= ComfortThreshold:
    penalty = 0
else:
    t       = (ComfortThreshold - ConditionPct) / ComfortThreshold   // 0..1
    penalty = FloorPenalty * pow(t, PenaltyCurveExp)                 // gentle, then steeper
effectiveStat = round( baseStat * (1 - penalty) )
```
`PenaltyCurveExp > 1` keeps it negligible just past the comfort edge and only bites as
Condition gets low. At `ConditionPct = 0`, `penalty = FloorPenalty` → effective ≈ 67%
of base. Stats never drop below that.

---

## 3. Locked decisions

| # | Decision | Value |
|---|----------|-------|
| 1 | Stats that degrade | **Strength + Club Control only** (no Recovery/Stamina — avoids spiral) |
| 2 | Penalty floor | **~67%** of base at empty Condition (`FloorPenalty ≈ 0.33`) |
| 3 | Curve shape | **Comfort zone then ramp** — negligible first ~6 holes, then steeper |
| 4 | Stat mapping | **Stamina stat = tank size (MaxCondition)** · **Recovery stat = regen rate** |
| 5 | Persistence | **Both pools persisted** (live + tournament) — save schema bump |
| 6 | Roster Stamina row | **Doubles as the live Condition meter** — fill = current Condition %, color blue→yellow→red as it depletes; the `9/27` number stays the Stamina *stat* (tank size) |

---

## 4. Stat mapping

| Stat | Role in the economy | Degrades? |
|------|--------------------|-----------|
| **Strength** | Shot power. Degraded by low Condition. | ✅ |
| **Club Control** | Shot accuracy. Degraded by low Condition. | ✅ |
| **Recovery** | **Regen rate** — how fast Condition refills over rest. | ❌ |
| **Stamina** | **Tank size** — MaxCondition. Bigger tank = lasts more holes. | ❌ |

---

## 5. Where it plugs in

One **`StaminaModel`** — a pure, CSV-driven helper (drain / regen / penalty). No
MonoBehaviour state. Consumed at **two** sites, both calling the same `Penalty()`:

1. **Gameplay stat seam — `LiveStatProviderHost`.** Already branches live vs.
   tournament and already reads the energy fields. Apply the Condition penalty here so
   *shots* use effective stats.
2. **Roster display — `CharacterDetailPanel`.** ⚠ This path **bypasses the seam** today
   (it binds raw `playerData.currentStrength` etc. straight to the bars). It must call
   `StaminaModel.Penalty()` itself to show effective vs. base. → `Penalty()` must be a
   shared pure function, not buried in the shot path.

**Two state-holders, one formula:**
- **Live pool** → `PlayerCharacterData.currentStaminaEnergy` (today `[NonSerialized]`,
  always 100). Persist to `Golfin.Save` (schema bump). Real-time regen.
- **Tournament pool** → `TournamentRoundContext` (T6 already gives the isolated pool).
  Seed from the frozen snapshot at Register. Persist too (decision #5).

**Drain relocation:** T6 shipped a placeholder **per-shot** flat depletion
(`ShotController.CommitFlick → DepleteStamina`, `StaminaCostPerShot`). Replace it with
**per-hole** drain at hole-complete. (T6 always flagged this as a placeholder.)

---

## 6. UI surfacing

The design already exists in Figma (Roster Screen, node `4065:14999`). Build status
from code audit (2026-06-29):

| Element | Figma | Code | Action |
|---------|-------|------|--------|
| Portrait low-stamina icon | ✅ | ✅ **wired & dormant** (`CharacterThumbnailCard.staminaIcon` → `IsStaminaLow()`) | Lights up for free once live Condition can drop |
| 4 stat-bar rows | ✅ | ✅ single-fill, base stats | Reuse |
| Ghost/degraded fill (Str + Club Control) | ✅ (translucent `rgba(…,0.5)` layer) | ❌ not built | Add 2nd fill `Image` per bar; bind base behind effective |
| Effective-stat feed to bars | — | ❌ panel shows raw base | Feed `StaminaModel.Penalty()` |
| Stamina row = Condition meter (blue→yellow→red) | ✅ (`Durability Bar Max/Low` tokens) | ❌ generic %-recolor only | Bar fill = Condition %; color states by Condition; number = Stamina stat |

Cleanup: `CharacterDetailPanel` has a vestigial unused `LOW_STAMINA_THRESHOLD = 0.25f`
— wire it to the real meter thresholds or remove.

Meter color states (Condition %): **blue** high → **yellow** mid → **red** low, matching
the orange/red `Parameters/Durability Bar Low` token. Thresholds in the CSV.

---

## 7. Persistence

- **Live Condition** → new serialized field on the save model + a `Condition` (or reuse
  `currentStaminaEnergy`) value with a **last-updated timestamp** for offline regen.
  Schema version bump + migration (default existing saves to full Condition).
- **Tournament Condition** → persisted on the tournament entry/round state so a
  multi-session tournament round survives an app restart. Seeded from snapshot at
  Register; isolated from the live pool.

---

## 8. Phased plan

| Phase | Scope | Tier |
|-------|-------|------|
| **0** | This design doc + tunables CSV. | Architect |
| **1** | `StaminaModel` pure core (drain/regen/penalty), CSV-driven, unit-tested. Replaces the flat per-shot constant. | 2 |
| **2** | Live wiring: per-hole drain at hole-complete, timestamp regen, penalty in `LiveStatProviderHost`, persist live Condition (schema bump). | 2/3 |
| **3** | Tournament wiring: point `TournamentRoundContext` at the same `StaminaModel`; persist tournament Condition. | 3 |
| **4** | UX: ghost fill on Str+Club Control + effective-stat feed in `CharacterDetailPanel`; Stamina row → Condition meter color states. Portrait icon already done. (Figma node `4065:14999`.) | 3 |
| **5** | Polish: curve tuning from playtest, optional RP/item top-ups, hard-gate revisit (stays deferred). | — |

Folds in: the queued `character_recovery_stamina_regen` spec **is** the regen half of
Phase 1/2.

---

## 9. Open / deferred

- Item / RP top-ups (instant Condition refill) — Phase 5, offline only for now.
- Hard-gate (refuse to play at empty) — explicitly **not** doing; revisit only if
  playtest shows the soft floor is too weak.
- Exact tunable values are first-pass — playtest and adjust in the CSV.
