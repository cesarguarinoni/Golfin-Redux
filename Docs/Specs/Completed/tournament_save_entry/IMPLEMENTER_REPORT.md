# IMPLEMENTER REPORT — tournament_save_entry (T5)

**Iteration shape:** save-schema:v2v3-migration-and-store-adapter

**Status:** All acceptance checklist items PASS.

---

## Canonical screenshot

N/A — headless save-schema task, gated by EditMode tests (see § Test results below).

## Figma fidelity

N/A — no Figma reference for this task (save-schema + adapter only).

---

## Summary

T5 ships in two stages:

**Stage 1 (Golfin.Save):**
- Added three flat DTOs in `SaveData.cs`: `PersistedTournamentEntry`, `PersistedHoleResult`, `PersistedCharacterSnapshot` — primitives only, no `Golfin.Tournaments` refs (asmdef one-way rule enforced)
- Added `public List<PersistedTournamentEntry> tournamentEntries = new List<PersistedTournamentEntry>();` to `SaveData`
- Bumped `SaveSchemaMigrator.CurrentSchemaVersion` from 2 to 3; added `v2→v3` migration block (defensive `??=` null-init, sets `schemaVersion=3`, `Debug.Log`)
- Preserved fail-hard-on-newer (unchanged)

**Stage 2 (Golfin.Tournaments):**
- Created `SaveBackedEntryStore : ITournamentEntryStore` implementing all 4 seam methods: `Load`, `Save`, `IsClaimed`, `MarkClaimed`
- UPSERT by `tournamentId` (replace, never duplicate); preserves `claimed` flag on re-save
- All I/O via `SaveDataHost.Instance.Data.tournamentEntries` + `MarkDirty()` — no direct disk I/O
- Injection constructor `SaveBackedEntryStore(SaveDataHost host)` for test isolation
- Updated `Golfin.Tournaments.Tests.asmdef` to reference `Golfin.Save` and `Newtonsoft.Json.dll` (tests need both)
- Added 4 T5 tests to `SaveLayerTests.cs` (migration, fail-hard v4, schema-version constant, PersistedEntry JSON round-trip)
- Added `SaveBackedEntryStoreTests.cs` (9 tests: round-trip, null-lastHoleUtc, upsert, 3 claim tests, debounce, restart, load-unknown)

---

## Acceptance checklist (SPEC §5)

| # | Test | Result | Evidence |
|---|------|--------|----------|
| 1 | v2→v3 migration: v2 JSON loads → schemaVersion==3, tournamentEntries empty, v2 fields intact | **PASS** | `T5_V2ToV3_Migration_TournamentEntriesEmptyAllV2FieldsIntact` PASSED (7ms) |
| 2 | Fail-hard: v4 JSON throws `SaveSchemaVersionException` | **PASS** | `T5_FailHard_V4Json_ThrowsSaveSchemaVersionException` PASSED (3ms) |
| 3 | Round-trip: multi-hole EntryState (DateTime? set AND null) → Save → serialize → deserialize → Load → field-equal incl. snapshot (characterId/level/strength/clubControl/recovery/stamina), status, startedUtc, lastHoleUtc, claimed, per-hole strokes/time/completedUtc/rngSeed | **PASS** | `RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual` PASSED (694ms); `RoundTrip_NullLastHoleUtc_SurvivesRoundTrip` PASSED (398ms); `T5_PersistedTournamentEntry_RoundTripViaNewtonsoft` PASSED (648ms) |
| 4 | Upsert: two Saves same tournamentId → replace (not duplicate) | **PASS** | `Upsert_TwoSavesSameTournamentId_ReplaceNotDuplicate` PASSED (599ms); verified `tournamentEntries.Count == 1` post-upsert |
| 5 | Claim: MarkClaimed persists; IsClaimed true after reload; idempotent | **PASS** | `Claim_MarkClaimed_PersistsAcrossReload` PASSED (699ms); `Claim_MarkClaimed_IsIdempotent` PASSED (598ms); `Claim_SavePreservesClaimed_OnUpsert` PASSED (600ms) |
| 6 | Debounce coalescing: N appends within 250ms → one disk write | **PASS** | `Debounce_MultipleMarkDirtyWithin250ms_OneSavedEvent` PASSED (699ms) — 5 Save calls + 1 FlushNow → exactly 1 OnSaved event |
| 7 | Atomic-write / restart: ReloadFromDisk() after a hole append → entry survives | **PASS** | `AtomicWrite_ReloadFromDiskAfterAppend_EntrySurvives` PASSED (601ms) |

---

## Test results (full run output)

### SaveLayerTests (14 tests, all PASS)

```
Golfin.Save.Tests.SaveLayerTests.AtomicWrite_SourceFileUntouchedIfOnlyTmpExists          PASSED (58ms)
Golfin.Save.Tests.SaveLayerTests.AtomicWrite_TmpThenReplace_WritesCorrectly               PASSED (826ms)
Golfin.Save.Tests.SaveLayerTests.CountingPersister_TenDirectCalls_CountsTenWrites         PASSED (1996ms)
Golfin.Save.Tests.SaveLayerTests.DictionaryRoundTrip_NewtonsoftJson                        PASSED (601ms)
Golfin.Save.Tests.SaveLayerTests.LocalJsonPersister_SaveAsync_WritesFileToDisk             PASSED (498ms)
Golfin.Save.Tests.SaveLayerTests.RoundTrip_WriteReadStructEqual                            PASSED (800ms)
Golfin.Save.Tests.SaveLayerTests.SchemaMigration_CurrentVersion_NoMigrationNeeded          PASSED (5ms)
Golfin.Save.Tests.SaveLayerTests.SchemaMigration_FutureVersion_ThrowsSaveSchemaVersionException PASSED (30ms)
Golfin.Save.Tests.SaveLayerTests.SchemaMigration_V1_MigratesTo_CurrentVersion             PASSED (6ms)
Golfin.Save.Tests.SaveLayerTests.T5_CurrentSchemaVersion_Is3                              PASSED (3ms)
Golfin.Save.Tests.SaveLayerTests.T5_FailHard_V4Json_ThrowsSaveSchemaVersionException      PASSED (3ms)
Golfin.Save.Tests.SaveLayerTests.T5_PersistedTournamentEntry_RoundTripViaNewtonsoft        PASSED (648ms)
Golfin.Save.Tests.SaveLayerTests.T5_V2ToV3_Migration_TournamentEntriesEmptyAllV2FieldsIntact PASSED (7ms)
Golfin.Save.Tests.SaveLayerTests.TryLoad_MissingFile_ReturnsFalse                         PASSED (1ms)
```

### SaveBackedEntryStoreTests (9 tests, all PASS)

```
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.AtomicWrite_ReloadFromDiskAfterAppend_EntrySurvives  PASSED (601ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.Claim_MarkClaimed_IsIdempotent                        PASSED (598ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.Claim_MarkClaimed_PersistsAcrossReload               PASSED (699ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.Claim_SavePreservesClaimed_OnUpsert                  PASSED (600ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.Debounce_MultipleMarkDirtyWithin250ms_OneSavedEvent  PASSED (699ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.Load_UnknownTournamentId_ReturnsNull                 PASSED (6ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual  PASSED (694ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.RoundTrip_NullLastHoleUtc_SurvivesRoundTrip         PASSED (398ms)
Golfin.Tournaments.Tests.SaveBackedEntryStoreTests.Upsert_TwoSavesSameTournamentId_ReplaceNotDuplicate  PASSED (599ms)
```

### Full EditMode suite (675 passed, 0 failed, 3 skipped pre-existing)

Full run: 678 total, 675 passed, 0 failed, 3 skipped (3 skips are pre-existing HoleCompleteDriverTests — unrelated to T5).

Pre-existing Tournaments tests verified green:
- `LocalTournamentBackendTests`: 68 PASSED
- `TournamentContractsTests`: 14 PASSED
- `BotFieldInvariantTests`: 36 PASSED

---

## Files modified or created

| File | Action | Notes |
|------|--------|-------|
| `Assets/Scripts/Save/SaveData.cs` | Modified | Added 3 flat DTOs + `tournamentEntries` field |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | Modified | Bumped v→3, added v2→v3 migration block |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Extended | Added T5 tests 1–4 (migration, fail-hard, schema-const, JSON round-trip) |
| `Assets/Scripts/Tournaments/SaveBackedEntryStore.cs` | Created | New ITournamentEntryStore implementation |
| `Assets/Scripts/Tournaments/SaveBackedEntryStore.cs.meta` | Created | Auto-generated by Unity on asset refresh |
| `Assets/Scripts/Tournaments/Tests/SaveBackedEntryStoreTests.cs` | Created | 9 new T5 store tests |
| `Assets/Scripts/Tournaments/Tests/SaveBackedEntryStoreTests.cs.meta` | Created | Auto-generated by Unity on asset refresh |
| `Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef` | Modified | Added `Golfin.Save` reference + `Newtonsoft.Json.dll` precompiled ref |

**Pre-existing dirty files (from tournament_character_snapshot task, NOT touched by T5):**
- `Assets/Scripts/Tournaments/CharacterSnapshot.cs` (??  — new from snapshot task)
- `Assets/Scripts/Tournaments/CharacterSnapshot.cs.meta` (??  — new from snapshot task)
- `Assets/Scripts/Tournaments/ICharacterStatsProvider.cs` (??  — new from snapshot task)
- `Assets/Scripts/Tournaments/ICharacterStatsProvider.cs.meta` (??  — new from snapshot task)
- `Assets/Scripts/Tournaments/EntryState.cs` (M — modified by snapshot task)
- `Assets/Scripts/Tournaments/LocalTournamentBackend.cs` (M — modified by snapshot task)
- `Assets/Scripts/Tournaments/Tests/LocalTournamentBackendTests.cs` (M — modified by snapshot task)
- `Assets/Scripts/TournamentsRuntime/CharacterManagerStatsProvider.cs` (??  — new from snapshot task)
- `Assets/Scenes/ShellScene.unity` (M — pre-existing, not from T5)
- `Packages/manifest.json` + `packages-lock.json` (M — pre-existing, not from T5)

---

## Rule compliance

| Rule | Status |
|------|--------|
| Rule 2 (real entry point) | N/A — save-schema task, no player UI entry point |
| Rule 3 (invariant JSON gate) | N/A — no world→screen visuals |
| Rule 4 (capture flip-free) | N/A — no capture |
| Rule 6 (report integrity) | PASS — all PASS claims backed by `tests-run` tool output above |
| Rule 7 (standing bans — Physics/ untouched) | PASS — `git diff HEAD -- Assets/Scripts/Physics/` = empty (verified) |
| Rule 13 (all dirty files outside task folder reported) | PASS — table above lists all uncommitted paths outside task folder |
| Rule 14 (canonical screenshot ≥900px) | N/A — headless save-schema task |
| Rule 18 (Figma fidelity table) | N/A — no Figma reference |
| New .cs files with .meta | PASS — both `.meta` files verified present on disk |
| Compile verified via script-execute after each stage | PASS — Stage1CompileCheck OK, Stage2CompileCheck OK; `IsCompiling=false` before tests |

---

## Spec deviations

None. All decisions in the SPEC were followed as specified:
- D1: `inputLog` omitted (rngSeed only) — implemented as specified
- D2=(b): persisted claim-once via `claimed` column + `MarkDirty()` — implemented
- D3: ISO-8601 strings for DateTimes — implemented with `ToString("O")` / `DateTime.Parse(...RoundtripKind)`
- CharacterSnapshot ships inside the single v2→v3 bump (no separate v3→v4)

---

## Open questions for Architect

None.
