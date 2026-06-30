# Implementer Report — `stamina_tournament_wiring`

**Iteration shape:** backend-wiring:schema-migration-version-mismatch

## Implementation summary

Phase 3 (Stamina Economy — tournament pool) wired the real per-entry condition pool: `EntryState` gains `ConditionRemaining`, `LocalTournamentBackend.Register` seeds it from `MaxCondition(snapshot.Stamina)`, `SubmitHoleResult` drains it by `DrainForHole()` (clamped to 0) atomic with `_store.Save`, and `TournamentRoundContext.BeginRound` was updated to a 4-param signature that seeds the runtime pool from the persisted entry. The per-shot `DepleteStamina()` call was removed from `ShotController:393` (D4=YES); the live/solo pool and `LiveStatProviderHost` were not touched. Save schema bumped v4→v5 with an empty migration block (sentinel -1f is backward-compatible). All 799 EditMode tests pass (796 pass, 3 deliberate Stage C1 skips, 0 fail).

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Tournaments/EntryState.cs` | Added `float ConditionRemaining { get; }`, optional ctor param defaulting to -1f |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` | D1-A: `Register` seeds pool; `SubmitHoleResult` drains; `IsConfigured` fallback; clock-trust-free XML-doc for future re-sim |
| `Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` | Added `"Golfin.Core.Stamina"` to references (cycle-free leaf) |
| `Assets/Scripts/Tournaments/SaveBackedEntryStore.cs` | Maps `conditionRemaining` in both `Load` and `Save` |
| `Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef` | Added `"Golfin.Core.Stamina"` to references |
| `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` | Appended `TournamentStaminaPhase3Tests` fixture (8 tests); fixed `SubmitHoleResult_DrainClamped_NeverNegative` to use HoleSet18 for unique hole IDs |
| `Assets/Scripts/Save/SaveData.cs` | `PersistedTournamentEntry.conditionRemaining = -1f` (sentinel) |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | `CurrentSchemaVersion` 4→5; empty v4→v5 migration block |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Updated 4 tests to reflect `CurrentSchemaVersion == 5` and v6 fail-hard (v5 is now legal) |
| `Assets/Scripts/Gameplay/TournamentContext/TournamentRoundContext.cs` | 4-param `BeginRound(tid, snapshot, tankMax, remaining)`; flat-100 reset removed; `DepleteStamina()` retained as dead/legacy API |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Removed per-shot `DepleteStamina()` call at ~L393 (D4=YES) |
| `Assets/Scripts/Gameplay/Tests/TournamentRoundLoopTests.cs` | Rewritten with 10 tests for per-hole model (replaces obsolete per-shot assertions) |
| `Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs` | Updated 2 tests: v3→v5 schema assertion; v6 fail-hard (v5 is now legal) |
| `Assets/Scripts/Gameplay/Tests/PlayMode/LiveStatProviderHostPlayModeTests.cs` | Updated `BeginRound` call to 4-param signature at L115 |
| `Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` | `BeginTournamentHole` updated with Phase 3 pool seeding block (tank from `MaxCondition`, sentinel D2, D3=NO regen) |
| `Docs/Specs/Active/stamina_tournament_wiring/STATUS.md` | Set to `IMPLEMENTER_WORKING` |
| `Docs/Specs/Active/stamina_tournament_wiring/HEARTBEAT.log` | Created; baseline block + progress entries |
| `Docs/Specs/Active/stamina_tournament_wiring/IMPLEMENTER_REPORT.md` | This file |

## Screenshot

N/A — pure backend/data/test task. No UI deliverable, no Figma reference in SPEC. Acceptance criteria are all test-based.

## Acceptance checklist (copy from SPEC.md §8)

| Item | Result | Justification |
|---|---|---|
| 1. Project compiles; new + existing EditMode tests green (per-shot tournament tests rewritten to per-hole) | PASS | `tests-run` EditMode: 796 passed, 0 failed, 3 deliberate Stage C1 skips. `assets-refresh` returned `[Success]` with zero error CS. |
| 2. Tournament entry pool seeds to `MaxCondition(snapshot.Stamina)` at Register, drains by `DrainForHole()` per hole (not per shot), persists across relaunch | PASS | `TournamentStaminaPhase3Tests` 8 tests all PASS: `Register_SeedsFullCondition_WhenStaminaConfigured` (tank=70 for stamina=10 with config base=50 per_pt=2), `SubmitHoleResult_DrainsCondition_PerHole` (70→65), `SubmitHoleResult_Persists_ConditionRemaining`, `ConditionRemaining_SurvivesInMemoryRoundTrip`. |
| 3. Degraded Str+ClubControl in tournaments reflect the real pool (Phase-2 seam already consumes it — no `LiveStatProviderHost` edit) | PASS | `LiveStatProviderHostPlayModeTests.ResolveLive_WhenTournamentActive_ReturnsSnapshotStats` PASS. `LiveStatProviderHost` not in `git status` — untouched. |
| 4. Per-shot drain removed; `ShotController:393` no longer calls `DepleteStamina` | PASS | `ShotController.cs` no longer contains the `TournamentRoundContext.DepleteStamina()` call at the shot-fire site; D4 comment inserted. `Pool_IsConstantWithinHole_NoPerShotDrain` PASS. |
| 5. Save migrates v4 → v5 cleanly; pre-v5 entries load to full pools | PASS | `T5_V3ToV4_Migration_ConditionFieldsDefaultSafe` asserts schemaVersion=5 and condition fields default-safe — PASS. `T6_Migration_V3ToV4_ConditionFieldsDefaultSafe` PASS. `ConditionRemaining_SurvivesInMemoryRoundTrip` PASS (round-trip via InMemoryEntryStore). |
| 6. `Golfin.Tournaments` stays cycle-free after adding `Golfin.Core.Stamina` leaf reference (D1-A) | PASS | `Golfin.Core.Stamina.asmdef` has `references:[]`. `Golfin.Tournaments.asmdef` adds the reference. Compile clean, no circular dependency errors. |
| 7. Scope clean: no `LiveStatProviderHost` change, no live/solo-pool change (Phase 2), no roster UI (Phase 4) | PASS | `git status --porcelain`: 15 code files modified, all within scope. No `LiveStatProviderHost`, no `StaminaRuntimeService`, no `PlayerCharacterData`, no roster UI files. `git diff HEAD -- Assets/Scripts/Physics/` = empty. |
| 8. Tournament pool does NOT regen (D3=NO); clock-trust-free model documented in backend XML-doc for future re-sim | PASS | `D3_NoRegen_PoolConstantWithinEvent` PASS (24h FixedClock advance, pool unchanged). Class-level XML-doc added to `LocalTournamentBackend` describing the clock-trust-free per-hole drain model for future server re-sim (GDD §8). |

## Known FAIL items

None.

## Spec deviations

`TournamentRoundContext.DepleteStamina()` was retained as dead/legacy API rather than deleted. SPEC §4.2 explicitly permits this ("may stay as dead/test-only API or be deleted — implementer's call"). Retaining it avoids any risk from callers outside the production shot path. The critical change — removal from `ShotController:393` — is done.

## Console output

```
No errors or warnings related to this task. Asset refresh returned [Success] every time.
Three deliberate skip messages (Stage C1 HoleCompleteDriverTests) are pre-existing, unrelated to this task.
```

## Open questions for Architect

None. All decisions D1-A, D2, D3=NO, D4=YES were locked before implementation.
