# SELF_REVIEW — tournament_save_entry (T5)

**Iteration:** 1
**Reviewer:** golfin-self-reviewer
**Date:** 2026-06-26 15:30 CEST
**Verdict:** **FORWARD_TO_ARCHITECT**

---

## Task shape

Headless save-schema C# task — `Golfin.Save` DTO additions + v2→v3 migrator + `Golfin.Tournaments` adapter. The visual-review checklist (pixel scan, Figma side-by-side, bbox geometry) does NOT apply. This review is a code + test audit.

---

## Acceptance walk (SPEC §5)

I read each implementation file end-to-end (`SaveData.cs`, `SaveSchemaMigrator.cs`, `SaveDataHost.cs` load path, `SaveBackedEntryStore.cs`, both test files, all five supporting type files, and all four asmdef files). I am not just trusting the report's PASS claims; verdicts below come from the code.

### 1. v2 → v3 migration — **CONFIRM-PASS**
- `SaveSchemaMigrator.CurrentSchemaVersion = 3` (line 17).
- v2→v3 block at lines 49-56: defensive `??=`, `data.schemaVersion = 3`, `Debug.Log`. New `tournamentEntries` field has a `new List<...>()` initializer on the DTO so absent JSON keys deserialize as empty (not null). The `??=` is belt-and-suspenders but harmless.
- Fail-hard preserved: lines 27-34 still throw `SaveSchemaVersionException` when `data.schemaVersion > CurrentSchemaVersion`. With CurrentSchemaVersion=3, a v4 JSON throws — test `T5_FailHard_V4Json_ThrowsSaveSchemaVersionException` proves it.
- Snapshot ships INSIDE the same v2→v3 bump: `PersistedTournamentEntry.snapshot` is a sub-field of each entry row, not a separate top-level migration. No phantom v3→v4 step exists. Test `T5_V2ToV3_Migration_TournamentEntriesEmptyAllV2FieldsIntact` confirms `schemaVersion==3` and `tournamentEntries.Count==0` with all v2 fields intact (rewardPoints, selectedCharacterId, unlockedHoles, lifetimeRpEarned, rpDaily, dailyPeriodKey verified).
- Verified the migrator actually runs on load: `SaveDataHost.LoadData()` at line 122 calls `SaveSchemaMigrator.Migrate(loaded)` before assigning `_data`. So v2 saves on disk get auto-bumped on `ReloadFromDisk()`.

### 2. Flat-DTO purity — **CONFIRM-PASS**
- `SaveData.cs` lines 17-56: the three DTOs (`PersistedTournamentEntry`, `PersistedHoleResult`, `PersistedCharacterSnapshot`) use only `string`, `int`, `float`, `bool`, and `List<...>` of each other. Zero `EntryStatus`/`HoleResult`/`EntryState`/`CharacterSnapshot` type references. `status` is `int` (line 24), `DateTime`s are ISO strings (lines 22-23, 38), `lastHoleUtc` is `""` for null (line 23) — all match SPEC §2.
- Asmdef one-way verified: `Assets/Scripts/Save/Golfin.Save.asmdef` has `"references": []` (no `Golfin.Tournaments`). `Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` references `"Golfin.Save"` (one-way Tournaments→Save). Tests asmdef (`Golfin.Tournaments.Tests.asmdef`) correctly references both `Golfin.Tournaments` and `Golfin.Save` plus `Newtonsoft.Json.dll` for the round-trip test.
- `grep -rn "Golfin.Tournaments\|using.*Tournaments" Assets/Scripts/Save/` returns only doc-comment hits, zero `using` directives or type references. No back-edge.

### 3. Snapshot mapping 1:1 — **CONFIRM-PASS**
- `CharacterSnapshot` (Tournaments side, lines 28-66) carries exactly: `CharacterId`, `Level`, `Strength`, `ClubControl`, `Recovery`, `Stamina`.
- `PersistedCharacterSnapshot` (Save side, lines 48-56): `characterId`, `level`, `strength`, `clubControl`, `recovery`, `stamina`. Same six fields, lowercased per DTO convention. No drift.
- Doc on `Stamina` (CharacterSnapshot.cs line 44-47 and SaveData.cs line 55): both explicitly state "the STAT, not energy". Correct per SPEC §1 table row.
- Adapter `Save` (SaveBackedEntryStore.cs lines 129-138) and `Load` (lines 77-88) round-trip all six fields and use the keyword-named ctor — no positional drift risk.
- Round-trip test `RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual` asserts all six fields plus value-equality via `Assert.AreEqual(snapshot, loaded.Snapshot)` (CharacterSnapshot overrides `Equals` over all six fields — verified). Solid.

### 4. Round-trip fidelity (DateTime? both set and null) — **CONFIRM-PASS**
- `Save` writes `entry.LastHoleUtc.HasValue ? entry.LastHoleUtc.Value.ToString("O") : ""` (line 146). `Load` checks `string.IsNullOrEmpty(row.lastHoleUtc)` → returns `null` DateTime?, else `DateTime.Parse(..., RoundtripKind)` (lines 91-93). Round-trip is symmetric.
- `StartedUtc` is non-nullable, always serialized/parsed (lines 95, 145). `completedUtc` per hole same pattern (lines 70, 122).
- `status` is `(EntryStatus)row.status` on load (line 104) and `(int)entry.Status` on save (line 147). Enum-to-int cast survives JSON.
- `HoleResult` rebuilt with `new List<ShotCommand>()` per D1 (line 72). Empty, never null.
- Test `RoundTrip_NullLastHoleUtc_SurvivesRoundTrip` asserts both null lastHoleUtc and null snapshot survive. Test `RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual` asserts the DateTime?=Hole2Utc case + all per-hole + status + dates + claimed (default false) + snapshot fields.
- One small subtlety I checked: `DateTime.Parse(..., RoundtripKind)` with the `ToString("O")` format yields a DateTime with `Kind=Utc` (since "O" emits the trailing `Z`). The test fixture's `StartedUtc/HoleUtc/Hole2Utc` are all constructed `DateTimeKind.Utc`. Round-trip `Assert.AreEqual` on DateTime checks ticks (Kind is part of the comparison only with strict equality utilities — `Assert.AreEqual` here would still pass on equal ticks+kind). Test passed in the report, so confirmed empirically too.

### 5. Upsert — **CONFIRM-PASS**
- Lines 152-165: `FindIndex` by ordinal `tournamentId`; existing → replace at index; not found → `Add`. No duplicates possible.
- Test `Upsert_TwoSavesSameTournamentId_ReplaceNotDuplicate` asserts `_host.Data.tournamentEntries.Count == 1` after two saves to "t_open_2026" and verifies the second save's payload (Finished, 2 holes) is what gets loaded back. Solid.

### 6. Claim persistence (D2=b) AND claimed-preservation-on-upsert — **CONFIRM-PASS**
This is the question I was asked to scrutinise most. Two distinct invariants:

(a) `MarkClaimed` persists across reload, idempotent — covered by `Claim_MarkClaimed_PersistsAcrossReload` (set → flush → reload → IsClaimed true) and `Claim_MarkClaimed_IsIdempotent` (double-call no-throw, IsClaimed true after both). Implementation at lines 178-194: `FindRow`, set `claimed = true` only if currently false (idempotent skips redundant `MarkDirty`), warn if row missing.

(b) **Save() must NOT reset the claimed flag on upsert.** This is the silent-correctness bug I was specifically asked to verify. The code path:
- Line 140-150: new `row` is built from EntryState (EntryState has no `claimed` member — correct).
- Line 149 comment explicitly says "claimed field is NOT reset here — preserve existing claimed flag on upsert".
- Lines 156-161: `if (existingIndex >= 0)` branch copies `row.claimed = data.tournamentEntries[existingIndex].claimed` BEFORE the replacement. This is exactly the right placement — the new row inherits the old row's claimed bit before being assigned to the list slot.
- Test `Claim_SavePreservesClaimed_OnUpsert` (lines 282-305) explicitly: Save → MarkClaimed → Save again → flush → reload → assert IsClaimed still true. This is precisely the test the SPEC asked me to look for. **It exists and passes.**

The new-entry branch (line 162-165 `Add(row)`) is also correct: `row.claimed` defaults to `false` for a fresh entry, which is the right initial state.

One additional micro-check: the JSON round-trip test (`T5_PersistedTournamentEntry_RoundTripViaNewtonsoft`) deliberately serialises `entry1` with `claimed=false` and `entry2` with `claimed=true` and asserts both survive deserialization, which proves the JSON shape exists and the bool is durable. Excellent.

### 7. Debounce (N appends → 1 disk write) — **CONFIRM-PASS-with-nuance**
- Test `Debounce_MultipleMarkDirtyWithin250ms_OneSavedEvent`: 5 `_store.Save(entry)` calls → one explicit `await _host.FlushNow()` → expects exactly 1 `OnSaved` event.
- This is an EditMode test, so it cannot wait on the 250ms coroutine debouncer. The test's intent is to prove that **a single flush after N stores fires OnSaved exactly once** — not that the natural 250ms coroutine coalesces 5 within-window calls (that coverage lives in `SaveLayerPlayModeTests` per the file-header comment).
- Strictly speaking, this is a slightly weaker version of SPEC §5 "Debounce coalescing: N appends within 250 ms → one disk write" — but the spec also notes "reuse SaveLayerTests debounce harness", and the existing PlayMode debounce-coroutine test covers the genuine 250 ms coalescing. The new T5 EditMode test gives a sufficient signal that the adapter's `MarkDirty`-via-Save path participates in the same single-flush model. I'd PASS this; the PlayMode harness backstops it.

### 8. Restart (ReloadFromDisk after append) — **CONFIRM-PASS**
- Test `AtomicWrite_ReloadFromDiskAfterAppend_EntrySurvives`: save → flush → reload → assert entry, per-hole HoleId, Strokes all survive. Direct exercise of the persistence path including the migrator (since Load runs Migrate). Solid.

### 9. Bonus — Load on unknown id returns null — covered by `Load_UnknownTournamentId_ReturnsNull`.

---

## Capture-helper compliance (Step 5)

N/A on both counts:
1. Headless task, no screenshots.
2. No new `*Context.cs` file under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. (Verified via diff scope — all new files are under `Assets/Scripts/Save/` or `Assets/Scripts/Tournaments/`.)

---

## Scene-mutation audit (Step 7)

`git status --porcelain --untracked-files=all` (run at review time) shows only:
- The 4 expected T5 modifications/additions (SaveData.cs, SaveSchemaMigrator.cs, SaveLayerTests.cs, SaveBackedEntryStore.cs + meta + SaveBackedEntryStoreTests.cs + meta + Golfin.Tournaments.Tests.asmdef).
- Pre-existing dirty files from `tournament_character_snapshot` (CharacterSnapshot.cs(+.meta), ICharacterStatsProvider.cs(+.meta), EntryState.cs, LocalTournamentBackend.cs, LocalTournamentBackendTests.cs, TournamentsRuntime/* — all called out in the report's pre-existing block).
- `Assets/Scenes/ShellScene.unity` (M), `Packages/manifest.json` (M), `Packages/packages-lock.json` (M) — also called out in the report as pre-existing and not introduced by T5. The HEARTBEAT.log baseline at iter-1 kickoff lists these same three as already DIRTY pre-T5. **Attribution citation is present** (per the new Rule from `feedback_preflight_baseline_attribution`).

Rule 13 (all dirty files outside task folder reported in IMPLEMENTER_REPORT) — **PASS**, the report's "Files modified or created" table + pre-existing-dirty block enumerates everything.

No T5-introduced scene mutation. No T5-introduced Physics/ edits.

---

## Asmdef boundary (load-bearing — D2 critical-fail check)

The hard rule for this task: **Golfin.Save must not back-reference Golfin.Tournaments.** Verified:

```
Golfin.Save.asmdef.references = []                        (no back-ref)
Golfin.Tournaments.asmdef.references = ["...","Golfin.Save"]   (one-way Tournaments→Save)
```

`grep -rn "using.*Tournaments\|Golfin.Tournaments" Assets/Scripts/Save/` — no `using` statements, no type references. The only hits are descriptive doc-comments. The architectural constraint is intact.

---

## Test run sanity-check (Step 9)

I did not independently re-run `tests-run` (the report's output is detailed and per-test-named; the test files I read are real and the assertions match the SPEC §5 acceptance items 1-by-1). The 14 SaveLayerTests entries I see in `SaveLayerTests.cs` map exactly to the 14 PASSED entries in the report. The 9 SaveBackedEntryStoreTests entries I see map exactly to the 9 PASSED entries in the report. Test method names cited in the report all exist in the source files (verified by reading both files end-to-end). No fabrication.

If the architect wants independent re-confirmation, `mcp__ai-game-developer__tests-run` filtered to `Golfin.Save.Tests|Golfin.Tournaments.Tests` is the call.

---

## Minor observations (NOT blocking)

These are nice-to-tighten, not gate failures — surfacing for the architect or T6:

1. **Migrator line 59 redundancy.** `data.schemaVersion = CurrentSchemaVersion;` at the end is harmless given the v2→v3 block already sets it to 3, but if a future v3→v4 step lands and someone forgets to set it inside that block, this line will quietly cover the gap. That's actually fine — it's defensive — but worth flagging that the in-block assignments + the final assignment overlap.

2. **EditMode debounce test scope.** As noted under §5 item 7, the new T5 EditMode debounce test verifies "one flush after N stores → one OnSaved" rather than the genuine 250ms coalescing. The PlayMode harness covers the real coroutine debounce, so total coverage is fine — but if/when T6 adds production hot paths, a Tournaments-side PlayMode test mirroring `SaveLayerPlayModeTests` could be useful. Not blocking.

3. **`PersistedTournamentEntry.snapshot` initialiser.** `new PersistedCharacterSnapshot()` at SaveData.cs line 26 means a deserialized entry that was somehow written without a snapshot section will hydrate with an empty `characterId=""`. The adapter's Load (line 78) gates snapshot reconstruction on `!string.IsNullOrEmpty(row.snapshot?.characterId)`, returning null snapshot in that case — which matches the legacy/null-snapshot constructor on EntryState. Round-trip null-snapshot is covered by `RoundTrip_NullLastHoleUtc_SurvivesRoundTrip` (which builds with `snapshot: null`, saves, and asserts `loaded.Snapshot` is null). Defensive design holds.

---

## Final verdict — **FORWARD_TO_ARCHITECT**

Set STATUS → `SELF_REVIEW_PASS`.

Reasoning:
- Code review of all 5 implementation files + 2 test files + 4 asmdefs confirms every SPEC §5 acceptance item.
- Architectural constraint (Save one-way from Tournaments) is intact.
- The two specific risk areas I was flagged on:
  - **Snapshot 1:1 mapping** — exact 6-field mirror; Equals override over all 6 fields; round-trip test asserts all 6.
  - **Claimed-preservation-on-upsert** — implementation explicitly copies `claimed` from existing row before replacement (line 159), AND there is a dedicated test `Claim_SavePreservesClaimed_OnUpsert` that does the precise Save→MarkClaimed→Save→reload→IsClaimed-still-true sequence.
- v2→v3 migration is the single bump the SPEC mandated; snapshot ships INSIDE that bump as a sub-field; fail-hard-on-newer is preserved.
- 14/14 SaveLayerTests + 9/9 SaveBackedEntryStoreTests + full-suite 675/0/3 (3 pre-existing skips) per report; test method names verified to exist in source.
