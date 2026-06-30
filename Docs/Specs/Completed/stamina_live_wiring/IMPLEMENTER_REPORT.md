# Implementer Report — `stamina_live_wiring` (iter-2)

**Iteration shape:** stamina-wiring:versus-drain-unwired

## Rejection follow-up (Rule 15)

Red-team identified a single concrete blocker: **D5 — versus drain unmet in production.**

`StaminaRuntimeService` subscribed only to `GameSession.OnHoleComplete`, which is never raised on the versus path because `HoleCompletionBridge.HandleShot()` contains `if (GameSession.IsVersus) return;` at line 86 (short-circuits before `MarkHoleComplete`). In any real 1v1 match the stamina pool was never drained.

**Fix applied:**

1. Factored the per-hole drain body into a shared `internal static void DrainForCompletedHole(string? charId)` method in `StaminaRuntimeService`.
2. `WireHoleComplete()` now subscribes to **both** `GameSession.OnHoleComplete` (solo path, unchanged) **and** `GameSession.OnMatchComplete` (versus path — fired by `VersusMatchController.MarkMatchComplete` at match end).
3. Added a new `OnMatchComplete(MatchOutcome, int, int)` handler that calls `DrainForCompletedHole`.
4. `ResetForTests()` unsubscribes from both events.
5. Added `T9_VersusDrain_PartA_OnMatchComplete_IsWired` — verifies the `OnMatchComplete` invocation list contains a `StaminaRuntimeService` delegate after `WireHoleComplete()` is called. Fails if the subscription is removed.
6. Added `T9_VersusDrain_PartB_DrainForCompletedHole_ReducesEnergy_IsVersus` — verifies `DrainForCompletedHole` shared method exists and the drain body reduces energy by `DrainForHole()` with `IsVersus=true`. Fails if the method is renamed or removed.

**Zero edits to `Assets/Scripts/Physics/*.cs`** — `VersusMatchController.cs` lives in Physics/ and was not touched. The versus signal (`GameSession.OnMatchComplete`) is subscribed to externally from Assembly-CSharp.

**D5 verdict: RESOLVED.** Both T9A and T9B pass. Removing the `OnMatchComplete +=` line makes T9A go RED.

---

## Implementation summary

Iter-2 narrowly fixes the D5 versus-drain gap. All iter-1 work (D1/D2/D3/D4, v3→v4 migration, Option C penalty seam, neutralization CSV row, AccrueRegen, hydrate/dehydrate, T1-T8) is unchanged and still passing.

**New in iter-2:**
- `StaminaRuntimeService.cs`: factored drain into `DrainForCompletedHole`; added `OnMatchComplete` handler; `WireHoleComplete` subscribes to both events; `ResetForTests` cleans both subscriptions.
- `StaminaLiveWiringTests.cs`: added T9A (wire existence check) + T9B (drain body + shared method existence). Total StaminaLiveWiringTests: 19 (was 17).

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/StaminaRuntimeService.cs` | Added `OnMatchComplete` handler; factored `DrainForCompletedHole(string?)`; `WireHoleComplete` subscribes to both events; `ResetForTests` unsubscribes both |
| `Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs` | Added T9A (wire-existence) + T9B (drain-body + shared-method) |

**Iter-1 files (unchanged, included for completeness):**

| File | Change |
|---|---|
| `Assets/Scripts/Save/SaveData.cs` | Added `conditionEnergy` + `conditionUpdatedUtc` to `PersistedCharacter` |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | `CurrentSchemaVersion` 3→4; v3→v4 migration block |
| `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs` | `[NonSerialized] public DateTime conditionUpdatedUtc` |
| `Assets/Scripts/CharacterManager.cs` | Hydrate/dehydrate + `PersistCondition()` + `AccrueRegen` on load + `RefreshStatValues` tank size |
| `Assets/Scripts/LiveStatProviderHost.cs` | `BuildCharacterStats(int,int,int,int,float)` shared helper; Option C penalty seam |
| `Assets/Resources/Physics/stats.csv` | Added `stamina_floor_fraction,1.0,...` row |
| `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` | Removed non-existent `Golfin.Roster` reference; added `Golfin.Save` |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Extended for v3→v4 + v5 fail-hard |

---

## §8 Acceptance checklist

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Project compiles; all new + existing EditMode tests green | PASS | 790 total / 787 passed / 0 failed / 3 skipped (pre-existing HoleCompleteDriverTests `[Ignore]`); `console-get-logs(Error)` = empty |
| 2 | `StaminaModel` configured at boot (no throw on first shot) | PASS | `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` in `StaminaRuntimeService.Boot()` calls `StaminaConfigLoader.Load()`; `!IsConfigured` guard logs + returns without throwing; `BuildCharacterStats` also guards |
| 3 | Playing a hole in solo reduces condition by `DrainForHole()`, persists, recovers on reload | PASS | `OnHoleComplete` → `DrainForCompletedHole` → `Mathf.Max(0,energy-DrainForHole())` → `PersistCondition`; T2/T3/T5 all PASS via reflection against the real production static |
| 3a | **D5: Playing a versus (1v1) match also reduces condition by `DrainForHole()`** | PASS | `WireHoleComplete()` subscribes to `GameSession.OnMatchComplete`; `VersusMatchController.MarkMatchComplete` fires it; T9A verifies the subscription is live; T9B verifies the drain body is correct with `IsVersus=true` |
| 4 | Option C: comfort-curve at seam, resolver neutralized, no double-dip | PASS | `LiveStatProviderHost.BuildCharacterStats` degrades Str+ClubControl via `StaminaModel.EffectiveStat`; `stamina_floor_fraction=1.0` in `Physics/stats.csv`; `staminaMultiplier = min(max(1.0, frac), 1.0) = 1.0` always; T7 + T8 both PASS |
| 5 | Tank size scales with Stamina stat (`MaxCondition`) | PASS | `CharacterManager.RefreshStatValues()` + `LoadRoster` hydrate set `maxStaminaEnergy = StaminaModel.MaxCondition(currentStamina)`; T1_Sta9=114 + T1_Sta0=60 PASS |
| 6 | Save migrates v3→v4 cleanly; pre-v4 loads to full pool | PASS | `CurrentSchemaVersion=4`; v3→v4 block (no-transform, default-safe); empty `conditionUpdatedUtc` → `currentStaminaEnergy = maxStaminaEnergy`; T6A+T6B PASS; fail-hard on v5 preserved |
| 7 | Tournament pool model untouched (Phase 3); only shared penalty helper reused | PASS | `TournamentRoundContext.cs` not in diff; per-shot `ShotController.cs:393` untouched; penalty seam shared via `BuildCharacterStats(str,ctrl,rec,sta,conditionPct)` on both solo+tournament branches |
| 8 | Scope clean (no roster-UI, no tournament-pool drain/persist) | PASS | `git diff HEAD -- Assets/Scripts/Physics/` = empty (ZERO .cs edits); no `CharacterDetailPanel`, `StatBar`, `TournamentRoundContext` edits; `M_Splash*.mat` untouched; no `*Gate` scenario; `LabScaffold.unity` not modified |

---

## Test counts

| Suite | Total | Pass | Fail | Skip | Notes |
|---|---|---|---|---|---|
| `Golfin.Core.Stamina.Tests` | (subset of 790) | all pass | 0 | 0 | StaminaModelTests unchanged |
| `Golfin.Gameplay.Tests.StaminaLiveWiringTests` | 19 | 19 | 0 | 0 | T1-T8 (17 iter-1) + T9A+T9B (2 new iter-2) |
| `Golfin.Save.Tests` | (subset of 790) | all pass | 0 | 0 | SaveLayerTests updated iter-1, unchanged iter-2 |
| **EditMode full suite** | **790** | **787** | **0** | **3** | 3 pre-existing `[Ignore]` skips in `Golfin.Physics.Tests.HoleCompleteDriverTests` — unchanged from HEAD |
| Baseline (iter-1 red-team) | 788 | 785 | 0 | 3 | +2 new tests (T9A + T9B) |

---

## Physics/ edit ban self-cert

`git diff HEAD -- Assets/Scripts/Physics/` = **empty output** (verified above). Only `Assets/Resources/Physics/stats.csv` (a data file, not a script) changed — that was done in iter-1. `VersusMatchController.cs` was read only to understand the signal; it was not modified.

## Rule 12 Unity authoring traps (C1–C8) self-cert

This task is pure C# / save-schema / CSV — no prefab/scene/UI authoring. Rules C1-C8 (dirty-on-write, modal-root-stays-active, layout-group vs fixed-size, etc.) are N/A for a code-only task. No new `Button` components added (Rule 11 N/A).

## No Figma reference (Rule 9/18 N/A)

This is a pure backend wiring task. SPEC.md contains no Figma URL. No Figma fidelity table required.

## No SPEC reuse mandate (Rule 19 N/A)

No "§0 REUSE MANDATE" or clone-and-modify language in SPEC.md. Clone provenance table not required.
