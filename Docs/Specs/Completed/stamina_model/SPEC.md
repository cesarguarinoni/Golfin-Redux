# SPEC — stamina_model (Stamina Economy, Phase 1)

**Tier:** 3 (full pipeline)
**Status:** Active
**Author:** Architect (Claude.ai)
**Created:** 2026-06-29 22:40 JST
**Design source:** `Docs/Design/STAMINA_ECONOMY.md` · `Docs/Design/stamina_economy.csv`

---

## 1. Goal

Build the **pure, CSV-driven `StaminaModel`** — the drain / regen / penalty math that
the entire stamina economy stands on. This is foundation only: **no wiring, no save,
no UI, no drain relocation.** Those are Phases 2–4. Deliver a unit-tested pure helper
plus its config loader.

Read `Docs/Design/STAMINA_ECONOMY.md` first — §2 (model), §4 (stat mapping), §5
(where it plugs in) are the relevant context. Do **not** implement §5/§6/§7 here.

## 2. Scope

**IN**
- `StaminaModel` — static pure helper (drain, regen, penalty, meter state, flags).
- `StaminaConfig` — value struct holding the tunables, with a pure `Parse(string csv)`.
- A thin runtime bootstrap that loads the CSV TextAsset and calls `StaminaModel.Configure`.
- EditMode unit tests covering every formula + edge case.
- Place the runtime CSV in the project's existing game-data CSV location.

**OUT (do not touch this phase)**
- `LiveStatProviderHost`, `CharacterDetailPanel`, `TournamentRoundContext`, save schema,
  the T6 per-shot `StaminaCostPerShot` drain. No relocation of the existing drain call.
- No UI, no portrait icon, no ghost bars.

## 3. Assembly placement

`StaminaModel` must be callable later from **both** the gameplay stat seam
(`LiveStatProviderHost`, Assembly-CSharp) **and** `Golfin.Roster` (the detail panel).
- Place it in the lowest-level shared assembly that both already reference, or can
  reference **without creating a cycle**.
- If no suitable existing assembly exists, create a new **leaf** asmdef
  `Golfin.Core.Stamina` (no scene/UI deps) that both reference.
- **Verify against the actual asmdef dependency graph** and record the chosen assembly
  + rationale in the implementation notes. Do not introduce a UI→gameplay cycle.

The math must be unit-testable in EditMode **without** loading a scene or Resources —
keep `StaminaModel` + `StaminaConfig.Parse` free of `UnityEngine.Resources`/IO. Only the
thin bootstrap touches `Resources`/`TextAsset`.

## 4. CSV

- **Authored source:** `Docs/Design/stamina_economy.csv` (committed).
- **Runtime copy:** copy it into the project's existing CSV load location and load it
  the **same way the existing CSVs load** — inspect `CharacterDatabaseCSV.cs` for the
  convention (path, `Resources.Load<TextAsset>` vs StreamingAssets, parsing) and match
  it. Do not invent a new loading mechanism.
- **Schema:** two columns `key,value` (plus a free-text `notes` column to ignore).
  `degraded_stats` is a semicolon-separated list. Parsing must be tolerant of the
  header row, blank lines, and surrounding whitespace.

## 5. API surface (pure)

```csharp
public readonly struct StaminaConfig
{
    public readonly float DrainPerHole;
    public readonly float TankBase;
    public readonly float TankPerStaminaPoint;
    public readonly float RegenBasePerHour;
    public readonly float RegenPerRecoveryPoint;
    public readonly float ComfortThresholdPct;   // 0..1
    public readonly float FloorPenalty;          // 0..1
    public readonly float PenaltyCurveExp;
    public readonly float MeterHighPct;          // 0..1
    public readonly float MeterMidPct;           // 0..1
    public readonly float LowConditionFlagPct;   // 0..1
    public readonly IReadOnlyCollection<string> DegradedStats; // e.g. {"Strength","ClubControl"}

    public static StaminaConfig Parse(string csvText);   // pure, no IO
}

public enum MeterColorState { High, Mid, Low }   // blue / yellow / red

public static class StaminaModel
{
    public static void  Configure(StaminaConfig config);       // call once at boot
    public static bool  IsConfigured { get; }

    public static int   MaxCondition(int staminaStat);                 // tank size
    public static float DrainForHole();                                // flat per-hole cost
    public static float ConditionPct(float condition, int staminaStat);// clamp 0..1
    public static float RegenPerHour(int recoveryStat);
    public static float RegenForElapsed(int recoveryStat, System.TimeSpan elapsed); // condition pts, >=0
    public static float PenaltyFor(float conditionPct);                // 0..FloorPenalty
    public static int   EffectiveStat(int baseStat, float conditionPct); // for degraded stats
    public static bool  IsDegraded(string statName);                   // per DegradedStats
    public static MeterColorState MeterState(float conditionPct);
    public static bool  IsLowConditionFlag(float conditionPct);
}
```

## 6. Formulas (exact)

```
MaxCondition(sta)        = round(TankBase + sta * TankPerStaminaPoint)
DrainForHole()           = DrainPerHole
ConditionPct(c, sta)     = clamp01( c / MaxCondition(sta) )            // MaxCondition>0
RegenPerHour(rec)        = RegenBasePerHour + rec * RegenPerRecoveryPoint
RegenForElapsed(rec, dt) = max(0, RegenPerHour(rec) * dt.TotalHours)   // dt<=0 -> 0

PenaltyFor(pct):
    if pct >= ComfortThresholdPct: return 0
    t = (ComfortThresholdPct - pct) / ComfortThresholdPct              // 0..1
    return FloorPenalty * pow(clamp01(t), PenaltyCurveExp)

EffectiveStat(base, pct) = RoundToInt( base * (1 - PenaltyFor(pct)) )

MeterState(pct):  pct >= MeterHighPct -> High
                  pct >= MeterMidPct  -> Mid
                  else                -> Low
IsLowConditionFlag(pct) = pct < LowConditionFlagPct
```
- `EffectiveStat` is intended for the degraded stats (Strength, Club Control). For a
  non-degraded stat, callers should not apply it; `IsDegraded(name)` is the gate.
- `RegenForElapsed` returns the points gained; the **caller** clamps `current + gained`
  to `MaxCondition`. `StaminaModel` does not own state.

## 7. Edge cases

- `staminaStat = 0` → `MaxCondition = round(TankBase)`.
- `MaxCondition = 0` guard → `ConditionPct` returns 0 (no divide-by-zero).
- `condition` above max or below 0 → `ConditionPct` clamps to 0..1.
- `pct >= ComfortThresholdPct` → penalty exactly 0 → `EffectiveStat == base`.
- `pct = 0` → penalty `= FloorPenalty` → `EffectiveStat == RoundToInt(base*(1-FloorPenalty))`.
- `PenaltyFor` is monotonic non-increasing in `pct`.
- `IsDegraded` is case-insensitive; unknown stat → false.
- Calling any method before `Configure` → throw a clear `InvalidOperationException`
  (or assert) — never silently use zeros.

## 8. Unit tests (EditMode, NUnit)

Match the existing test style/location (see `Assets/Scripts/Gameplay/Tests/
TournamentRoundLoopTests.cs`). Configure from `StaminaConfig.Parse` of the authored CSV
text. With the **default CSV** values, assert:

- `StaminaConfig.Parse` reads all 12 keys; `DegradedStats = {Strength, ClubControl}`.
- `MaxCondition(9) == 114`, `MaxCondition(27) == 222`, `MaxCondition(0) == 60`.
- `DrainForHole() == 8`.
- `RegenPerHour(9) == 30`, `RegenPerHour(40) == 92`.
- `RegenForElapsed(9, 2h) == 60`; `RegenForElapsed(9, 0) == 0`; negative dt → 0.
- `ConditionPct(57, 9) == 0.5` (±epsilon); `ConditionPct(999, 9) == 1`; `ConditionPct(-5, 9) == 0`.
- `PenaltyFor(0.80) == 0` → `EffectiveStat(20, 0.80) == 20`.
- `PenaltyFor(0.0) == 0.33` → `EffectiveStat(20, 0.0) == 13` (`round(20*0.67)`).
- `PenaltyFor(0.20) < PenaltyFor(0.05)` and both `< 0.33` (monotonic, floored).
- `MeterState(0.70) == High`, `MeterState(0.45) == Mid`, `MeterState(0.20) == Low`.
- `IsLowConditionFlag(0.20) == true`, `IsLowConditionFlag(0.30) == false`.
- `IsDegraded("strength") == true`, `IsDegraded("Recovery") == false`.
- Calling `MaxCondition` before `Configure` throws.

## 9. Acceptance criteria

- New files compile; project builds; no changes to any OUT-of-scope file.
- All unit tests above pass in EditMode.
- Chosen assembly + rationale recorded; no new asmdef cycle.
- Runtime CSV loads via the existing CSV convention (cite the matched pattern).
- `StaminaModel`/`StaminaConfig.Parse` carry no `Resources`/IO dependency.

## 10. Notes / flags

- Default tunables are first-pass for playtest; the model must read them from the CSV,
  never hard-code them.
- `meter_mid_pct` (0.30) sits slightly above `low_condition_flag_pct` (0.25) on purpose:
  the meter turns red just before the portrait alarm trips. Intended, not a bug.
- If the existing CSV loader can't be cleanly reused, flag it (`NOTE:`) rather than
  forking a second loader — surface it for Architect review.
