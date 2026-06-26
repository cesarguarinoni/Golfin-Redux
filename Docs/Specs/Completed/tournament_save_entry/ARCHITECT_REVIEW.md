# ARCHITECT_REVIEW — tournament_save_entry (T5)

**Reviewer:** golfin-reviewer
**Date:** 2026-06-26 15:34 CEST
**Verdict:** **PASS → READY_FOR_REDTEAM**

> Headless save-schema C# task. No Figma/screenshot/video/mesh-metrics gates apply
> (Rules 14/15/16/17/18 N/A). This review is a rigorous independent code + test
> audit per PIPELINE_HARDENING Rule 5 — every SPEC §5 acceptance item was
> re-verified by reading source, not by carrying forward prior PASSes.

---

## Files audited end-to-end

| Path | Role |
|---|---|
| `Assets/Scripts/Save/SaveData.cs` | DTOs + `tournamentEntries` field |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | v2→v3 migration + fail-hard |
| `Assets/Scripts/Save/SaveDataHost.cs` (lines 110-150) | Confirmed `Migrate` runs on load |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | 14 tests, +4 T5-specific |
| `Assets/Scripts/Tournaments/SaveBackedEntryStore.cs` | Store adapter |
| `Assets/Scripts/Tournaments/Tests/SaveBackedEntryStoreTests.cs` | 9 store tests |
| `Assets/Scripts/Tournaments/{EntryState,HoleResult,TournamentEnums,ITournamentEntryStore,CharacterSnapshot}.cs` | Runtime types being mapped |
| `Assets/Scripts/Save/Golfin.Save.asmdef` | Asmdef one-way |
| `Assets/Scripts/Tournaments/Golfin.Tournaments.asmdef` | Asmdef one-way |
| `Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef` | Test refs |

---

## Acceptance walk (independent re-derivation per SPEC §5)

### 1. v2 → v3 migration — **PASS**

Re-derived from `SaveSchemaMigrator.cs`:
- Line 17: `CurrentSchemaVersion = 3` ✓
- Lines 27-34: `> CurrentSchemaVersion` throws `SaveSchemaVersionException` (fail-hard
  preserved; v4 reading on a v3 build throws — confirmed by test
  `T5_FailHard_V4Json_ThrowsSaveSchemaVersionException`).
- Lines 49-56: v2→v3 block — `??=` defensive null-init of `tournamentEntries`,
  sets `schemaVersion=3`, `Debug.Log`. The `??=` is belt-and-suspenders
  (Newtonsoft leaves missing keys at the `new List<>()` initializer default), but
  harmless and self-documenting.
- **No phantom v3→v4 step.** The snapshot ships INSIDE this single bump because
  `PersistedCharacterSnapshot` is a sub-field of `PersistedTournamentEntry`
  (SaveData.cs line 26), not a separate top-level migration. Confirmed.
- `SaveDataHost.LoadData()` (line 122) calls `SaveSchemaMigrator.Migrate(loaded)`
  before assigning `_data` — so v2 saves on disk get auto-bumped on
  `ReloadFromDisk()`. Path verified.

Test `T5_V2ToV3_Migration_TournamentEntriesEmptyAllV2FieldsIntact` (SaveLayerTests.cs
lines 298-341) deserializes a hand-built v2 JSON (no `tournamentEntries` key),
runs `Migrate`, asserts `schemaVersion==3`, `tournamentEntries.Count==0`, AND
every v2 field intact (`rewardPoints`, `selectedCharacterId`, `unlockedHoles`,
`lifetimeRpEarned`, `rpDaily`, `dailyPeriodKey`). Solid.

### 2. Flat-DTO purity + asmdef one-way — **PASS** (the critical-fail architectural check)

**DTO purity** (SaveData.cs lines 17-56):
- `PersistedTournamentEntry`: `string`, `int`, `bool`, `List<PersistedHoleResult>`,
  `PersistedCharacterSnapshot`. Status is `int` (line 24). DateTimes are ISO
  strings (lines 22-23). Zero `EntryStatus`/`HoleResult`/`EntryState`/`CharacterSnapshot`
  type references.
- `PersistedHoleResult`: primitives only; `inputLog` correctly omitted per D1.
- `PersistedCharacterSnapshot`: 6 primitive fields, no runtime refs.

**Asmdef one-way** (the load-bearing check — back-edge = critical architectural fail):
```
Golfin.Save.asmdef.references       = []                                       ✓ ZERO
Golfin.Tournaments.asmdef.references = ["Golfin.UI.Rankings.Core", "Golfin.Save"] ✓ Tournaments→Save only
Golfin.Tournaments.Tests.asmdef.references = ["Golfin.Tournaments", "Golfin.UI.Rankings.Core", "Golfin.Save"]
```

Independent check via grep:
```
$ grep -rn "using.*Tournaments\|Golfin\.Tournaments" Assets/Scripts/Save/
```
Returns only **doc-comment** hits (SaveData.cs lines 8, 15, 31, 44, 105 — all in
`///` or `//` comments). Zero `using` directives, zero type references. The
one-way constraint is intact.

**Cycle check for the test-asmdef change:** `Golfin.Tournaments.Tests` references
both `Golfin.Tournaments` AND `Golfin.Save`, but Tests is editor-only
(`includePlatforms: ["Editor"]`) and a leaf in the DAG (nothing references it).
A leaf adding more in-edges cannot create a cycle. ✓

### 3. Snapshot mapping 1:1 — **PASS**

| `CharacterSnapshot` (runtime, lines 28-47) | `PersistedCharacterSnapshot` (SaveData lines 48-56) |
|---|---|
| `CharacterId : string` | `characterId : string` |
| `Level : int` | `level : int` |
| `Strength : int` | `strength : int` |
| `ClubControl : int` | `clubControl : int` |
| `Recovery : int` | `recovery : int` |
| `Stamina : int` ("the STAT, not energy") | `stamina : int` ("the STAT, not energy") |

Exact 6-field mirror. Both files explicitly document `stamina = STAT`. The
adapter (`SaveBackedEntryStore.cs` lines 80-87 Load, 129-138 Save) uses the
keyword-named ctor — no positional drift risk if the field order ever changes.

Test `RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual` (lines 94-152) asserts
all 6 fields individually AND `Assert.AreEqual(snapshot, loaded.Snapshot)` —
which exercises `CharacterSnapshot.Equals` (an override defined over all 6
fields, CharacterSnapshot.cs lines 72-81). Solid.

### 4. Round-trip (DateTime? set + null, status, HoleResult empty InputLog) — **PASS**

Re-derived from SaveBackedEntryStore.cs:
- Save `LastHoleUtc` (line 146): `HasValue ? ToString("O") : ""`.
- Load `lastHoleUtc` (lines 91-93): `IsNullOrEmpty ? null : DateTime.Parse(..., RoundtripKind)`.
- Symmetric ✓.
- `StartedUtc` non-nullable: always serialized/parsed (lines 95, 145).
- Per-hole `completedUtc` same pattern (lines 70, 122). `ToString("O")` emits
  trailing `Z`, `RoundtripKind` re-creates `DateTimeKind.Utc` — `DateTime.Equals`
  compares ticks+kind, so round-trip equality is exact.
- `status` cast: `(EntryStatus)row.status` on load (line 104), `(int)entry.Status`
  on save (line 147). Enum order frozen by `TournamentEnums.cs` (already shipped
  in T1) so int↔enum is stable.
- `HoleResult` rebuilt with `new List<ShotCommand>()` (line 72) per D1: empty
  InputLog, never null.

Tests: `RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual` covers DateTime?=set
+ full per-hole + status + snapshot + claimed=default. `RoundTrip_NullLastHoleUtc_SurvivesRoundTrip`
covers DateTime?=null AND snapshot=null. Both PASS.

### 5. Upsert (replace, not duplicate) — **PASS**

SaveBackedEntryStore.cs lines 152-165:
```csharp
int existingIndex = data.tournamentEntries.FindIndex(
    r => string.Equals(r.tournamentId, entry.TournamentId, StringComparison.Ordinal));
if (existingIndex >= 0) {
    row.claimed = data.tournamentEntries[existingIndex].claimed; // preserve
    data.tournamentEntries[existingIndex] = row;                 // REPLACE
} else {
    data.tournamentEntries.Add(row);                              // INSERT
}
```
`FindIndex` is Ordinal (no culture-sensitivity surprise). Existing → replace at
index. Not found → Add. Duplicates impossible.

Test `Upsert_TwoSavesSameTournamentId_ReplaceNotDuplicate` (lines 181-223):
double-Save same `tournamentId`, flush, reload, asserts `Count==1` and the
second save's payload (`Finished`, 2 holes) is what loads back. PASS.

### 6. Claim persistence (D2=(b)) AND claimed-preservation-on-upsert — **PASS** (the critical correctness point)

This is the silent-corruption bug the SPEC asked me to scrutinise hardest.
**Independent code re-derivation:**

- `EntryState` has no `claimed` field (verified — the runtime type does not carry
  it; it lives only on the persisted row). Correct boundary.
- `Save` builds `row` from `entry` (lines 140-150) — `row.claimed` defaults to
  `false` for the new `PersistedTournamentEntry`.
- **Line 149 comment explicitly states the invariant:** "claimed field is NOT
  reset here — preserve existing claimed flag on upsert".
- **Line 158-159: `row.claimed = data.tournamentEntries[existingIndex].claimed;`**
  ← this is THE line that prevents the silent un-claim bug. The new row inherits
  the existing row's `claimed` bit BEFORE being written into the list slot. The
  placement (before the assignment, inside the existing-branch only) is exactly
  correct.
- New-entry branch (line 164 `Add(row)`): `row.claimed` is `false` — the right
  initial state for a fresh entry.

`MarkClaimed` (lines 178-194): `FindRow` → set `claimed=true` only if currently
false (idempotent — skips redundant `MarkDirty`) → `MarkDirty()`. Missing-row
case logs warning and no-ops (defensive).

`IsClaimed` (lines 171-175): `FindRow` → return `row.claimed`. Unknown id → false.

**Tests covering this:**
- `Claim_MarkClaimed_PersistsAcrossReload` — Save → IsClaimed false → MarkClaimed
  → flush → reload → IsClaimed true.
- `Claim_MarkClaimed_IsIdempotent` — double-MarkClaimed doesn't throw, IsClaimed
  still true after reload.
- `Claim_SavePreservesClaimed_OnUpsert` (lines 282-305) — **the exact test the
  SPEC asked me to verify exists:** Save → MarkClaimed → Save again → flush →
  reload → assert IsClaimed still true. **Present and passes.**

This is the highest-stakes correctness item in T5 (relaunch double-claim = real
money/RP exploit risk). Implementation, comment, and dedicated test all line up.

### 7. Debounce coalescing — **PASS-with-flag**

Test `Debounce_MultipleMarkDirtyWithin250ms_OneSavedEvent` (lines 309-334): 5
`_store.Save(entry)` calls → one explicit `await _host.FlushNow()` → expects
exactly 1 `OnSaved` event.

This is the slightly weaker "N stores + explicit FlushNow = 1 OnSaved" variant
of SPEC §5's "N appends within 250 ms → one disk write." The self-reviewer
flagged this and noted the genuine 250ms coroutine coalescing lives in
`SaveLayerPlayModeTests`.

**Reviewer judgment: acceptable, not a blocker.**
- The SPEC text explicitly says "reuse SaveLayerTests debounce harness" — the
  PlayMode harness IS the existing coverage of the coroutine timer.
- An EditMode test cannot reasonably exercise a 250ms coroutine (no scheduler).
- The new test still proves the adapter's `MarkDirty`-via-Save path participates
  in the single-flush model — which is the part T5 introduced.
- Total coverage (EditMode flush coalescing + pre-existing PlayMode 250ms
  coroutine) is adequate.

Flagged for T6 as a backlog item: a Tournaments-side PlayMode mirror of
`SaveLayerPlayModeTests` if production hot paths warrant it.

### 8. Restart (ReloadFromDisk after append → survives) — **PASS**

Test `AtomicWrite_ReloadFromDiskAfterAppend_EntrySurvives` (lines 339-364):
Save → flush → reload → assert entry, per-hole HoleId, Strokes all survive.
`ReloadFromDisk` re-runs `Migrate` (verified in SaveDataHost.LoadData line 122),
so this also exercises the migrator round-trip on a freshly-written v3 save. ✓

### 9. Bonus — `Load_UnknownTournamentId_ReturnsNull` — covered.

---

## Test re-run

`mcp__ai-game-developer__tests-run` is not available to this reviewer agent
(scoped tool list). I verified by other means:

1. **Test method enumeration matches report exactly.**
   ```
   SaveLayerTests.cs:           14 [Test] methods → report claims 14/14 PASSED ✓
   SaveBackedEntryStoreTests.cs:  9 [Test] methods → report claims  9/9 PASSED ✓
   ```
   All test names cited in the report appear verbatim in source (grep-confirmed):
   `T5_V2ToV3_Migration_…`, `T5_FailHard_V4Json_…`, `T5_CurrentSchemaVersion_Is3`,
   `T5_PersistedTournamentEntry_RoundTripViaNewtonsoft`,
   `RoundTrip_EntryStateWithSnapshot_…`, `RoundTrip_NullLastHoleUtc_…`,
   `Upsert_…`, `Claim_MarkClaimed_PersistsAcrossReload`,
   `Claim_MarkClaimed_IsIdempotent`, `Claim_SavePreservesClaimed_OnUpsert`,
   `Debounce_…`, `AtomicWrite_ReloadFromDiskAfterAppend_…`, `Load_Unknown…`.

2. **Test assertions match SPEC §5 acceptance items** by independent reading
   (verdicts §1-§9 above).

3. **No fabricated tool output:** every PASS claim in the report corresponds to
   a real test in source whose assertions I have inspected.

If the red-team wants a binary re-confirmation, the call is
`mcp__ai-game-developer__tests-run` filtered to
`Golfin.Save.Tests|Golfin.Tournaments.Tests`. The implementer's report shows the
full per-test PASS list with timings (3-826ms) and the full-suite roll-up
(675/0/3).

---

## Asmdef cycle audit (critical-fail check)

Already covered in §2 above. To restate the load-bearing finding for the
red-team:

```
Golfin.Save.asmdef.references           = []
Golfin.Tournaments.asmdef.references    = ["Golfin.UI.Rankings.Core", "Golfin.Save"]
Golfin.Tournaments.Tests.asmdef.references = ["Golfin.Tournaments", "Golfin.UI.Rankings.Core", "Golfin.Save"]

grep -rn "using.*Tournaments\|Golfin\.Tournaments" Assets/Scripts/Save/
  → zero `using` directives, only doc-comment text
```

No back-edge. Test asmdef change introduces no cycle (Tests is a DAG leaf).
**PASS.**

---

## Minor observations (NOT blocking)

The self-reviewer flagged three minor items; my independent assessment of each:

1. **Migrator line 59 redundancy** (`data.schemaVersion = CurrentSchemaVersion;`
   after the v2→v3 block already sets it to 3). Harmless. Acts as a backstop for
   any future migration step that forgets the in-block bump. Defensive code, not
   a defect. Not a blocker.

2. **EditMode debounce test scope.** See §7 above — acceptable; the PlayMode
   harness backstops the 250ms coroutine. Backlog flag for T6, not a blocker.

3. **Null-snapshot default-init hydration path.**
   `PersistedTournamentEntry.snapshot = new PersistedCharacterSnapshot()`
   initializer (SaveData.cs line 26) means a deserialized entry written without
   a snapshot section will hydrate with an empty `characterId=""`. The Load
   adapter (line 78) gates snapshot reconstruction on
   `!string.IsNullOrEmpty(row.snapshot?.characterId)` → returns null
   `CharacterSnapshot` → matches the legacy `EntryState` ctor (no-snapshot
   overload). Test `RoundTrip_NullLastHoleUtc_SurvivesRoundTrip` builds with
   `snapshot: null`, saves, asserts `loaded.Snapshot` is null. Defensive design
   holds. Not a blocker.

---

## Scene-mutation audit (Rule for pipeline hardening)

Self-reviewer ran `git status --porcelain --untracked-files=all` and confirmed
only the 7 expected T5 paths + the pre-existing `tournament_character_snapshot`
DIRTY block, all attributed in HEARTBEAT.log's iter-1 kickoff baseline (lines
3-26). Re-verified by reading HEARTBEAT.log directly — the baseline lists
`ShellScene.unity`, `Packages/manifest.json`, `packages-lock.json`,
`EntryState.cs`, `LocalTournamentBackend.cs`,
`LocalTournamentBackendTests.cs`, the four `CharacterSnapshot`/`ICharacterStatsProvider`
files, the `TournamentsRuntime/` files, and the `tournament_character_snapshot`
spec docs as already DIRTY pre-T5. Rule 13 compliance PASS.

No T5-introduced scene mutation. No T5-introduced Physics/ edits (verified
self-reviewer's `git diff HEAD -- Assets/Scripts/Physics/` finding).

---

## Report integrity (PIPELINE_HARDENING Rule 6)

Every PASS claim in `IMPLEMENTER_REPORT.md` is backed by:
- A specific named test in source that I read end-to-end.
- A specific code path I traced and verified.

Zero fabricated test results, zero fabricated approval quotes, zero unbacked
PASS assertions. **Rule 6 PASS.**

---

## Verdict

**PASS → STATUS = READY_FOR_REDTEAM**

Reasoning summary:
- **Schema migration:** v2→v3 ships in a single bump (no phantom v3→v4); fail-hard-
  on-newer preserved; tournamentEntries defaults empty; all v2 fields intact.
- **Architectural boundary intact:** `Golfin.Save` has zero back-references to
  `Golfin.Tournaments` (asmdef references = `[]`; grep confirms no `using` or
  type ref); Tournaments→Save one-way; test asmdef change introduces no cycle.
- **Flat-DTO purity:** primitives only; status as int; DateTimes as ISO strings;
  no runtime-type leakage.
- **Snapshot 1:1:** exact 6-field mirror including stamina=STAT documentation;
  `CharacterSnapshot.Equals` override exercised by the round-trip test.
- **Round-trip fidelity:** DateTime? set+null both symmetric; status int↔enum;
  HoleResult rebuilt with empty InputLog per D1.
- **Upsert:** Ordinal FindIndex, replace not duplicate, tested at `Count==1`.
- **D2 claim persistence (the critical correctness point):** `Save` explicitly
  preserves `existing.claimed` on upsert (line 158-159), AND a dedicated test
  `Claim_SavePreservesClaimed_OnUpsert` exercises the exact
  Save→MarkClaimed→Save→reload→IsClaimed-still-true sequence. No silent un-claim.
- **Debounce:** EditMode flush-coalescing covered; 250ms coroutine coverage
  delegated to the existing `SaveLayerPlayModeTests` per SPEC text.
- **Restart:** ReloadFromDisk after append → entry survives (also exercises the
  migrator round-trip on a v3-written save).
- **Tests:** 14/14 SaveLayerTests + 9/9 SaveBackedEntryStoreTests + full-suite
  675/0/3 per report; every cited test name verified to exist in source.
- **Boundary:** No T5-introduced scene mutation or Physics edits; all dirty
  files outside the task folder are attributed in HEARTBEAT baseline.

Hand to **golfin-redteam-reviewer** for the adversarial gate.
