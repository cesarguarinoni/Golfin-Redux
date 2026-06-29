# Implementer Report — `stamina_model` (Stamina Economy Phase 1)

**Iteration shape:** stamina-model:clean-start

## Implementation summary

Built the pure CSV-driven `StaminaConfig` (readonly struct + `Parse(string csv)`) and `StaminaModel` (static pure helper with drain/regen/penalty/meter math) as specified. Created a new leaf asmdef `Golfin.Core.Stamina` so the model is reachable from both Assembly-CSharp (`LiveStatProviderHost`) and future roster UI callers with no new dependency cycle. Added a thin `StaminaConfigLoader` bootstrap (the only class that touches `Resources.Load<TextAsset>`) and deployed the runtime CSV to `Assets/Resources/Gameplay/stamina_economy.csv`. All 28 EditMode unit tests pass.

## Assembly placement — rationale

**Chosen assembly:** new leaf `Golfin.Core.Stamina` (`Assets/Scripts/Core/Stamina/Golfin.Core.Stamina.asmdef`, `autoReferenced: true`, no `references` entries, no scene/UI deps).

**Rationale:** Both `LiveStatProviderHost` (the gameplay stat seam) and `CharacterDatabaseCSV` / `CharacterDetailPanel` (roster) live in **Assembly-CSharp** — the implicit default assembly that auto-references every asmdef with `autoReferenced: true`. No existing explicit asmdef is imported by both Assembly-CSharp and `Golfin.Roster` code simultaneously without creating a cycle (the explicit asmdefs like `Golfin.Gameplay.Loop` and `Golfin.Tournaments` are not referenced by roster UI code). Creating a new leaf asmdef with `autoReferenced: true` is the correct pattern: Assembly-CSharp picks it up automatically, and it can also be explicitly listed in any test or named asmdef's `references` array. No cycle is introduced (the library has zero dependencies of its own).

**Test asmdef:** `Golfin.Core.Stamina.Tests` (`Assets/Scripts/Core/Stamina/Tests/Golfin.Core.Stamina.Tests.asmdef`) — Editor-only, references `Golfin.Core.Stamina`, uses `nunit.framework.dll`.

## CSV loading convention — matched pattern

The runtime bootstrap `StaminaConfigLoader.Load()` matches `Golfin.Gameplay.Config.ControlsConfigLoader` exactly:
- Same load call: `Resources.Load<TextAsset>("Gameplay/stamina_economy")` → reads `Assets/Resources/Gameplay/stamina_economy.csv`
- Same parse loop: split on `\n`, trim each line, skip blank + `#`-comment lines, skip the header row (`key,value,notes`), split on `,`, use `float.TryParse` with `InvariantCulture`
- Only the bootstrap touches `Resources` — `StaminaConfig.Parse(string csvText)` and all `StaminaModel` methods are IO-free

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Core/Stamina/Golfin.Core.Stamina.asmdef` | Created — leaf asmdef, autoReferenced:true, no dependencies |
| `Assets/Scripts/Core/Stamina/StaminaConfig.cs` | Created — pure readonly struct + `Parse(string csvText)` |
| `Assets/Scripts/Core/Stamina/StaminaModel.cs` | Created — pure static helper (drain, regen, penalty, meter, flags) |
| `Assets/Scripts/Core/Stamina/StaminaConfigLoader.cs` | Created — thin bootstrap; only file touching Resources |
| `Assets/Scripts/Core/Stamina/Tests/Golfin.Core.Stamina.Tests.asmdef` | Created — Editor-only test asmdef |
| `Assets/Scripts/Core/Stamina/Tests/StaminaModelTests.cs` | Created — 28 EditMode NUnit tests |
| `Assets/Resources/Gameplay/stamina_economy.csv` | Created — runtime copy of `Docs/Design/stamina_economy.csv` |

Unity auto-generates `.meta` files for each of the above; they appear in `git status` as untracked alongside the source files — all expected, all within scope.

## Canonical evidence (tests-run output)

This task has no visual deliverable. The canonical gate is the EditMode test run:

```
Test run: Golfin.Core.Stamina.Tests.StaminaModelTests
Status:  Passed
Total:   28
Passed:  28
Failed:  0
Skipped: 0
Duration: ~1.89 s
```

All 28 test methods in `Golfin.Core.Stamina.Tests.StaminaModelTests` passed on first run after clean compilation. Test names:

- `Parse_ReadsAll12Keys` — PASSED
- `MaxCondition_StaminaStat9_Returns114` — PASSED
- `MaxCondition_StaminaStat27_Returns222` — PASSED
- `MaxCondition_StaminaStat0_Returns60` — PASSED
- `DrainForHole_Returns8` — PASSED
- `RegenPerHour_Recovery9_Returns30` — PASSED
- `RegenPerHour_Recovery40_Returns92` — PASSED
- `RegenForElapsed_Recovery9_2Hours_Returns60` — PASSED
- `RegenForElapsed_ZeroElapsed_Returns0` — PASSED
- `RegenForElapsed_NegativeElapsed_Returns0` — PASSED
- `ConditionPct_Half_Returns0_5` — PASSED
- `ConditionPct_Overflow_ClampsTo1` — PASSED
- `ConditionPct_Negative_ClampsTo0` — PASSED
- `PenaltyFor_AtOrAboveComfort_ReturnsZero` — PASSED
- `EffectiveStat_AboveComfort_EqualToBase` — PASSED
- `PenaltyFor_AtZero_EqualsFloorPenalty` — PASSED
- `EffectiveStat_AtZeroCondition_Returns13` — PASSED
- `PenaltyFor_IsMonotonic_And_BelowFloor` — PASSED
- `MeterState_0_70_ReturnsHigh` — PASSED
- `MeterState_0_45_ReturnsMid` — PASSED
- `MeterState_0_20_ReturnsLow` — PASSED
- `IsLowConditionFlag_0_20_ReturnsTrue` — PASSED
- `IsLowConditionFlag_0_30_ReturnsFalse` — PASSED
- `IsDegraded_LowercaseStrength_ReturnsTrue` — PASSED
- `IsDegraded_Recovery_ReturnsFalse` — PASSED
- `IsDegraded_ClubControl_ReturnsTrue` — PASSED
- `IsDegraded_Unknown_ReturnsFalse` — PASSED
- `MaxCondition_BeforeConfigure_Throws` — PASSED

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| New files compile; project builds; no changes to any OUT-of-scope file | PASS | `assets-refresh` returned `[Success]` with no errors. `git diff HEAD -- Assets/Scripts/Physics/` shows zero diff. All new files are under `Assets/Scripts/Core/Stamina/` and `Assets/Resources/Gameplay/`. |
| All unit tests pass in EditMode | PASS | `tests-run` (class:StaminaModelTests): 28/28 PASS, 0 FAIL, 0 SKIP. Backed by the full test-run JSON output above. |
| Chosen assembly + rationale recorded; no new asmdef cycle | PASS | New leaf asmdef `Golfin.Core.Stamina` with `autoReferenced:true`, zero `references` entries — zero risk of cycle. Assembly-CSharp picks it up automatically. Rationale documented in § Assembly placement above. |
| Runtime CSV loads via the existing CSV convention (cite matched pattern) | PASS | `StaminaConfigLoader.Load()` uses `Resources.Load<TextAsset>("Gameplay/stamina_economy")` — identical call pattern to `ControlsConfigLoader` in `Golfin.Gameplay.Config.ControlsConfigLoader.cs`. CSV placed at `Assets/Resources/Gameplay/stamina_economy.csv`. |
| `StaminaModel`/`StaminaConfig.Parse` carry no `Resources`/IO dependency | PASS | Only `StaminaConfigLoader.cs` contains `using UnityEngine;` and the `Resources.Load` call. `StaminaModel.cs` and `StaminaConfig.cs` have no `using UnityEngine` directive — they are pure System types only. |
| `StaminaConfig.Parse` reads all 12 keys; `DegradedStats = {Strength, ClubControl}` | PASS | Verified by `Parse_ReadsAll12Keys` test — PASSED. |
| `MaxCondition(9) == 114`, `MaxCondition(27) == 222`, `MaxCondition(0) == 60` | PASS | Three separate test methods all PASSED. |
| `DrainForHole() == 8` | PASS | `DrainForHole_Returns8` PASSED. |
| `RegenPerHour(9) == 30`, `RegenPerHour(40) == 92` | PASS | Both test methods PASSED. |
| `RegenForElapsed(9, 2h) == 60`; zero elapsed → 0; negative elapsed → 0 | PASS | Three `RegenForElapsed_*` tests all PASSED. |
| `ConditionPct(57, 9) == 0.5 (±ε)`, overflow → 1, negative → 0 | PASS | Three `ConditionPct_*` tests all PASSED. |
| `PenaltyFor(0.80) == 0` → `EffectiveStat(20, 0.80) == 20` | PASS | Both tests PASSED. |
| `PenaltyFor(0.0) == 0.33` → `EffectiveStat(20, 0.0) == 13` | PASS | Both tests PASSED. |
| `PenaltyFor(0.20) < PenaltyFor(0.05)` and both < 0.33 | PASS | `PenaltyFor_IsMonotonic_And_BelowFloor` PASSED. |
| `MeterState(0.70) == High`, `MeterState(0.45) == Mid`, `MeterState(0.20) == Low` | PASS | All three `MeterState_*` tests PASSED. |
| `IsLowConditionFlag(0.20) == true`, `IsLowConditionFlag(0.30) == false` | PASS | Both tests PASSED. |
| `IsDegraded("strength") == true`, `IsDegraded("Recovery") == false` | PASS | Both tests PASSED (case-insensitive). |
| Calling `MaxCondition` before `Configure` throws | PASS | `MaxCondition_BeforeConfigure_Throws` PASSED — `InvalidOperationException` raised. |

## Known FAIL items

None.

## Spec deviations

- `StaminaModel.ResetForTests()` is declared `public` (not `internal`) inside `#if UNITY_EDITOR`. The SPEC did not specify the access modifier for this helper. `internal` would require `InternalsVisibleTo` to cross the asmdef boundary; `public`+`UNITY_EDITOR` guard achieves the same isolation (stripped from player builds) without extra boilerplate. This is a non-observable deviation.

## Console output

No errors or warnings related to this task. The `assets-refresh` call after all files were created returned `[Success] Assets refresh completed: AssetDatabase`.

## Open questions for Architect

None.
