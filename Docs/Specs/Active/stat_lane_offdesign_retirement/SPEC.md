# stat_lane_offdesign_retirement

> **Status:** SPEC_READY
> **Order:** 731 (Notion GOLFIN_Roadmap) — Phase "Loop v2", P2 — Medium
> **Tier:** 3 — FULL PIPELINE (runtime physics/resolver math → always Tier 3, standing rule)
> **Filed:** 2026-07-16 17:05 JST (Architect)
> **Handoff file:** `Docs/Specs/Active/stat_lane_offdesign_retirement/SPEC.md` (this file)
> **Blocks:** Orders 415 (`strength_velocity_short_game_scaling`) + 417 (`ball_rebound_perceptibility`) — both are MEASURE-FIRST and their measurements are contaminated until this lands.

---

## One-line

Delete the two off-design lanes in `StatModifierResolver.Resolve` — the `staminaMultiplier` block and the `CharClubControlPerPoint` → aim-cone term — so each stat drives exactly the effect the design assigns it, and so the Tier-Tune measurement track has a clean baseline.

---

## Design authority

`Docs/Game Design/SHOT_CONTROLS_DESIGN.md` — **Status: Active design (v1)**, §6 "Stat → behavior map" (marked *Authoritative*):

| Stat | Design says it affects |
|---|---|
| Character Club Control | Number of "clean" arrow passes; **arrow speed** |
| Club Accuracy | **Cone width** (= aim rotation range AND error tolerance) |
| Character Strength | Overpower forgiveness |

Confirmed by `Docs/Game Design/GAME_DESIGN_CHANGELOG.md` (2026-03-21): `| Club Control | Arrow/timing speed |`.

**Cesar rulings, 2026-07-16:**
1. *"Club control = arrow speed. Accuracy = Cone."*
2. *"Depletion does not happen during matches anymore. Stats are fixed once you start the match and they are all affected by stamina."*

Ruling 2 is already implemented — at the **provider**, not the resolver. See Defect A.

---

## Defects (both verified at HEAD `7f2c89096`)

### Defect A — stamina is applied twice

`LiveStatProviderHost.BuildCharacterStats` (Option C, D1 LOCKED) already bakes condition into the stats it puts in the bundle:

```csharp
int eStr  = StaminaModel.IsDegraded("Strength")    ? StaminaModel.EffectiveStat(str,  conditionPct) : str;
int eCtrl = StaminaModel.IsDegraded("ClubControl") ? StaminaModel.EffectiveStat(ctrl, conditionPct) : ctrl;
return new CharacterStats(strength: eStr, clubControl: eCtrl, recovery: rec, stamina: sta);
```

`StatModifierResolver.cs:9–18` then scales those **already-degraded** stats by stamina **again**, off the same pool:

```csharp
fp staminaFraction   = bundle.CurrentStamina / bundle.MaxStamina;   // same pool that produced conditionPct
fp staminaMultiplier = fpMath.Max(coeffs.StaminaFloorFraction, staminaFraction);
staminaMultiplier    = fpMath.Min(staminaMultiplier, fp.One);
fp effStrength       = fp.FromInt(bundle.Character.Strength)    * staminaMultiplier;   // 2nd hit
fp effClubControl    = fp.FromInt(bundle.Character.ClubControl) * staminaMultiplier;   // 2nd hit
```

The resolver lane is the pre-Stamina-Economy mechanism. Nobody retired it when Option C shipped (Phases 1–5, 2026-06-29 → 07-03).

**Why it's worse than simple double-counting:** `StaminaModel.PenaltyFor()` returns **0 at or above `ComfortThresholdPct`** — the designed model applies no penalty until the character is actually tired, then follows `FloorPenalty × t^PenaltyCurveExp`, gated per-stat by `_config.DegradedStats`. The resolver lane has **no comfort threshold, no curve, and no `IsDegraded` gate** — it is raw `current/max`. So at 90% condition the provider correctly applies **nothing** and the resolver silently applies **×0.90 anyway**. Below the comfort threshold the two compound. The resolver's cruder model is overriding the designed one from downstream.

**Blast radius:** every Strength lane (velocity F7 + overpower) and the ClubControl aim-cone lane, at any condition < 100%.

### Defect B — ClubControl drives the aim cone (off-design)

`StatModifierResolver.cs:41`:

```csharp
fp charControlReduction = effClubControl * coeffs.CharClubControlPerPoint;   // 0.0035/point
fp unreducedFraction    = (fp.One - clubAccReduction) * (fp.One - charControlReduction);
```

Per §6 and ruling 1, cone belongs to **Club Accuracy**; ClubControl belongs to **arrow speed** (which is live — `ShotController.TickArrow:407`). This lane is a redundant second stat doing Accuracy's job. It is also the lane the audit measured at a limp ~2.93m lateral at 200m and misdiagnosed as "sub-threshold, needs a redesign."

---

## Audit reconciliation (context — do not re-derive)

`Docs/Physics/STAT_LANE_AUDIT.md` findings **F-LANA-2a** and **F-LANA-2b** are **VOID**. The audit proposed, as a Tier-Redesign, an aim-arrow-oscillation mechanic that had already shipped in April. It only read `StatModifierResolver.cs` and never opened `SHOT_CONTROLS_DESIGN.md` or `ShotController.cs`. The follow-up spec `club_control_aim_arrow_speed` **must never be written**. The real ClubControl finding is a coefficient range mismatch — filed separately as Order 732.

---

## Scope — exactly two deletions

### Change 1 — remove the resolver's stamina lane

In `Assets/Scripts/Physics/Stats/StatModifierResolver.cs`:

- Delete the `staminaFraction` / `staminaMultiplier` block (lines ~9–15).
- `effStrength` becomes `fp.FromInt(bundle.Character.Strength)` — raw from the bundle, which is *already* provider-degraded.
- Delete `effClubControl` entirely (its only consumer is Change 2, which also goes).
- Replace the deleted block with a comment naming `LiveStatProviderHost.BuildCharacterStats` as the single stamina application point, so this never gets re-added.

`bundle.CurrentStamina` / `bundle.MaxStamina` stay on the struct (other consumers may read them); they simply stop feeding the resolver.

### Change 2 — remove the ClubControl aim-cone term

In Step 3, delete `charControlReduction`. The lane collapses to Club-Accuracy single-source:

```
aimConeReduction = clubAccReduction         // clamped to caps.AimConeReductionMax as before
```

Keep the `AimConeReductionMax` clamp and the `fp.Zero` floor exactly as they are.

### Coefficients that go dead

`StaminaFloorFraction` (0.20) and `CharClubControlPerPoint` (0.0035) lose their only consumers. **Do not delete the fields** from `StatCoefficients` / `PhysicsConfigLoader` in this order — leave them with a `// DEAD — retired by Order 731` comment. Removing config keys is a separate cleanup and would widen the blast radius.

---

## What must NOT change

- **Putter lanes** (Steps 6–7 putter block): untouched.
- **Ball lanes** (Step 8: rebound / roll / windCut): untouched. Order 417 owns rebound.
- **`ShotController.TickArrow`**: **CORRECT AS-IS.** It reads raw `bundle.Character.ClubControl` *because the provider already degraded it*. Do not add a stamina multiplier here. This was flagged as a suspected bug during scoping and cleared on inspection — do not "fix" it.
- **`CharStrengthVelocityPerPoint` (F7, 0.004)**: not a coefficient change in this order. Order 415 owns it.
- **`ShotController.HalfConeAngleRad()`** (input-layer cone from Club.Accuracy): untouched.
- Spin, lie resistance, Club.Power, Ball.Power lanes: untouched.

---

## Hard gates

1. **Hole 1 completability** — ≤7 strokes, default character. Standing gate on any physics change (`PHYSICS_TUNING_CHANGELOG.md` header). Non-negotiable.
2. **FALLBACK path must be bit-identical.** `DefaultStatProvider` hardcodes `currentStamina=100f, maxStamina=100f` → `staminaFraction = 1.0` → the deleted lane is already a no-op there. **Verify** `CharacterStats.Neutral.ClubControl` — if it is `0`, Change 2 is likewise a no-op on FALLBACK and the whole path is bit-identical. **Prove this with a before/after terminal-position comparison, don't assert it.** If it is non-zero, report the delta and escalate rather than absorbing it.
3. **Tests at or above the physics baseline.**
4. New **F9 entry** in `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` — mandatory for anything that moves physics output.

---

## Expected behavioural deltas (predict, then measure)

State these *before* running, then compare. Do not rationalise a surprise after the fact.

| Condition | Change 1 (stamina) | Change 2 (cone) |
|---|---|---|
| 100% condition | **No change** (multiplier was 1.0) | Cone reduction drops by the ClubControl term → shots slightly **less** accurate |
| < 100% condition | Strength/ClubControl **rise** (double penalty removed) → more carry when tired | same as above |

At Club.Accuracy=50 / CC=25 (Common cap), `aimConeReduction` goes 0.279 → 0.21. That is a real, intended gameplay change: accuracy is Accuracy's job now.

---

## Tests

- **Retire/rewrite** `Assets/Scripts/Physics/Tests/StatResolverTests.cs:117` `Stats_ZeroStamina_FloorPreservesCharStats` — it asserts the resolver-side floor that this order deletes. Replace with a test asserting the resolver is **stamina-agnostic**: same `CharacterStats` + different `CurrentStamina` → identical `ResolvedShotModifiers`.
- **Add** a test asserting `aimConeReduction` is a pure function of `Club.Accuracy` — varying `Character.ClubControl` must not move it.
- **Add** a provider-side test asserting stamina still degrades stats exactly once, via `StaminaModel.EffectiveStat`, including the above-comfort-threshold case (90% condition → **no** penalty).
- Sweep `StatResolverTests.cs` for any other assertion that bakes in `staminaMultiplier` or `CharClubControlPerPoint`.

---

## Traps

- **Lesson V** — any same-start stat comparison MUST `ResetToTee()` between samples. The audit's phantom 106.5m roll "delta" came from firing the HIGH shot from the LOW shot's terminal position; corrected delta was 0.1m. Bit-identity checks in gate 2 are exactly this shape.
- **Lesson W** — asmdef build order can veto a parameter-pass design; `StatProviderBus` static state is the canonical workaround. Not expected to bite here (deletions only), but do not "solve" anything by adding a cross-asmdef reference.
- **Do not** treat this as licence to retune coefficients. This order deletes lanes. 415/417/732 own the numbers.
- Bot-recorded video is the default visual gate. No manual play.

---

## Out of scope

- Any coefficient retune (415 / 417 / 732).
- Removing the dead `StaminaFloorFraction` / `CharClubControlPerPoint` config keys.
- **Open question, deliberately not answered here:** the physics-side `aimConeReduction` now duplicates the input-side cone in `ShotController.HalfConeAngleRad()` — both single-source from Club.Accuracy. Whether two cone systems should coexist is a design question for Cesar, filed separately, not resolved in this order.

---

## Definition of done

1. Both lanes deleted; comment planted naming the provider as the sole stamina application point.
2. FALLBACK bit-identity **proven**, not asserted.
3. Hole 1 ≤7 strokes, bot video.
4. Tests green at or above baseline, with the three test changes above.
5. F9 landed in `PHYSICS_TUNING_CHANGELOG.md`.
6. Cesar-approved → Active → Completed, Notion 731 Done + Closed.
