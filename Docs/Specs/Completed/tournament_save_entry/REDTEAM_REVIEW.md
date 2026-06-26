# REDTEAM_REVIEW — tournament_save_entry (T5)

**Red-team reviewer:** golfin-redteam-reviewer
**Date:** 2026-06-26 15:46 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**

> Headless save-schema C# task. No Figma/screenshot/video/mesh gates apply.
> Gate = EditMode test suite + code correctness. This is an adversarial gate:
> I re-ran the binary tests MYSELF, re-derived every claim from source, and
> probed edge cases the test suite does NOT cover via live `script-execute`.
> I tried to break it three ways and could not.

---

## 1. BINARY TEST RE-RUN (I ran it; the prior reviewer could not)

Driven via `unity-mcp-cli run-tool tests-run` against the live Editor
(PID 6219, MCP server reachable on :21573). This MCP build **ignores
`testFilter`** and always runs the full EditMode suite, returning only
non-passed rows. The authoritative summary from my own run:

```
Status: Passed   TotalTests: 678   Passed: 675   Failed: 0   Skipped: 3
```

The 3 skips are the pre-existing `HoleCompleteDriverTests` (Stage C1 no-ops),
unrelated to T5. **0 failures across the entire suite** — matches the report's
675/0/3 claim exactly.

**Fabrication check (test names are NOT invented):** I grepped the live
`Editor.log` from my own run and confirmed the T5 test methods actually
executed (their `LogAssert.Expect`/`Debug.LogError`/`Debug.LogWarning` stack
traces appear with correct source line numbers):
- `T5_V2ToV3_Migration_TournamentEntriesEmptyAllV2FieldsIntact` (SaveLayerTests.cs:325)
- `T5_FailHard_V4Json_ThrowsSaveSchemaVersionException` (SaveLayerTests.cs:358)
- `RoundTrip_EntryStateWithSnapshot_SaveLoadFieldEqual` (SaveBackedEntryStoreTests.cs:117)
- `Claim_SavePreservesClaimed_OnUpsert` (SaveBackedEntryStoreTests.cs:301)
- `Upsert_TwoSavesSameTournamentId_ReplaceNotDuplicate` (SaveBackedEntryStoreTests.cs:213)
- `AtomicWrite_ReloadFromDiskAfterAppend_EntrySurvives` (SaveBackedEntryStoreTests.cs:355)

All 14 SaveLayerTests `[Test]` methods + 9 SaveBackedEntryStoreTests `[Test]`
methods exist in source (read end-to-end) and the suite reports 0 failed.
No fabricated test result. Report integrity (Rule 6): PASS.

---

## 2. LIVE EDGE-CASE PROBES (script-execute, read-only — the part no test covers)

### Probe set 1 — degenerate-input robustness
```
A_status99           : Load OK, status=(int)99 (no throw)        [benign — C# enum cast never throws; store only writes (int)real-enum]
B_badstart           : THREW FormatException (malformed startedUtc)
B2_emptystart        : THREW FormatException (empty startedUtc)
C_markclaimed_missing: no-op, no throw, IsClaimed=False          [correct — SPEC flow Saves before claiming]
D_badhole_empty_completedUtc : THREW FormatException
E_status_NotEntered  : match=True
E_status_InProgress  : match=True
E_status_Finished    : match=True
E_status_DNF         : match=True
```

### Probe set 2 — migration + claim across REAL disk
```
M1_v2load (real v2 JSON, no tournamentEntries key):
   schemaVersion=3  rp=777  entriesNull=False  entriesCount=0  holes=2   ✓
M2_v4_failhard: SaveSchemaVersionException thrown=True                    ✓
C1_claim_disk (write→FlushNow→ReloadFromDisk→fresh store): IsClaimed=True  ✓
C2_freshclaim_false (brand-new entry, existingIndex<0 branch): IsClaimed=False ✓
C3_claim_preserved_disk_upsert (claim→Save→flush→reload): IsClaimed=True   ✓
D1_default_schema = 2 (SaveData ctor default)
```

**Reading of the FormatException results (B/B2/D):** `DateTime.Parse` throws on
a malformed OR empty ISO string. This is **NOT a reachable defect for this
gate**: `EntryState.StartedUtc` is non-nullable and `Save` always writes
`ToString("O")`, so the store's own write path can never emit an empty/garbage
`startedUtc` or `completedUtc`. The only way to hit it is hand-corruption of the
JSON or a *future* schema that adds rows without these fields — neither is a v2→v3
concern (v2 saves carry zero entries). A `try/catch` hardening of Load is a
reasonable T6 nice-to-have; it is **not a blocker** and not a regression.
Logged as a non-blocking observation, not a FAIL.

---

## 3. PRIOR-REJECTION REPLAY

`CESAR_REJECTION.md` — **absent**. No prior Cesar rejection for this task. Nothing
to re-shoot. (This is iter-1; HEARTBEAT confirms a single iteration.)

---

## 4. ATTACK RESULTS PER FLAGGED ITEM

| # | Attack | Result |
|---|--------|--------|
| 1 | v2→v3 migration / v4 fail-hard / no phantom v3→v4 | **GONE/SOUND** — M1+M2 probes prove v2→3 single bump, v4 still throws. Snapshot is a sub-field of `PersistedTournamentEntry`, not a separate migration (SaveData.cs:26). |
| 2 | Flat-DTO purity + no asmdef cycle | **SOUND** — `Golfin.Save.references=[]`; `grep` of Save/ shows zero `using Tournaments`/type refs (doc-comments only); `Golfin.UI.Rankings.Core.references=[]` (no back-edge). Tests asmdef is an Editor-only DAG leaf. The 678-test suite compiled+ran = the graph is provably acyclic. DTOs are primitives only; status=int (SaveData.cs:24); DateTimes=ISO strings. |
| 3 | Snapshot 1:1 both directions | **SOUND** — exact 6-field mirror (characterId/level/strength/clubControl/recovery/stamina). Save maps all 6 (SaveBackedEntryStore.cs:132-137); Load maps all 6 via keyword ctor (81-86); both files document stamina=STAT. Round-trip test asserts all 6 fields + `CharacterSnapshot.Equals` (override over all 6). |
| 4 | Claim preservation on upsert + missing-row MarkClaimed | **SOUND** — line 159 `row.claimed = existing.claimed` (existingIndex≥0 branch only); fresh entry → `claimed=false` (probe C2); claim survives disk upsert (probe C3); MarkClaimed on missing tid = warn+no-op, IsClaimed stays false (probe C). No silent un-claim, no silent drop. |
| 5 | Round-trip edge cases (null↔"", every status int↔enum, empty InputLog, malformed dates) | **SOUND** — all 4 EntryStatus values round-trip (probe E); null lastHoleUtc ↔ "" symmetric; HoleResult rebuilt with `new List<ShotCommand>()` (never null). Malformed/empty date strings throw FormatException but are unreachable from the write path (see §2). |
| 6 | Upsert replace-not-duplicate, ordinal FindIndex | **SOUND** — `FindIndex` with `StringComparison.Ordinal`; replace at index / Add; test asserts Count==1 post double-save. |
| 7 | Debounce coverage gap | **ACCEPTABLE (ruled, not waived)** — see §5. |

---

## 5. DEBOUNCE RULING (the task asked me to rule firmly)

**Ruling: the EditMode "5 Saves + 1 FlushNow = 1 OnSaved" variant is ACCEPTABLE
for this gate. The genuine 250ms coalescing does NOT need re-proving here.**

Load-bearing facts:
- `git diff --stat HEAD -- Assets/Scripts/Save/SaveDataHost.cs` is **EMPTY** —
  T5 added zero new debounce/timer logic. `MarkDirty`/`DebounceWrite`/`FlushNow`
  are pre-existing and unchanged.
- The genuine time-based coalescing is already covered by
  `SaveLayerPlayModeTests.Debounce_TenMarkDirtyCallsWithinOneFrame_CollapseToOneWrite`
  (10 MarkDirty in one frame → wait 400ms → exactly 1 disk write). A coroutine
  timer can ONLY be tested in PlayMode; EditMode has no frame scheduler.
- What T5 introduced — the adapter's `Save()`→`MarkDirty()` path participating
  in the single-flush model — is exactly what the new EditMode test proves.
- SPEC §5 explicitly says "reuse SaveLayerTests debounce harness," delegating
  the coroutine coverage to the existing PlayMode test.

Requiring a PlayMode re-test of unchanged infrastructure would be gold-plating.
Not a blocker. (Flagged for T6 only if production hot paths warrant a
Tournaments-side PlayMode mirror.)

---

## 6. THREE BREAK-ATTEMPTS (all failed — why)

1. **Data-shape / corruption attack** (probe set 1): out-of-range status int,
   malformed + empty ISO date strings, MarkClaimed on a non-existent tid. The
   only throws (FormatException on bad/empty dates) are **unreachable from the
   store's own write path** — `startedUtc` is non-nullable and always serialized
   `ToString("O")`. Could not promote this to a real blocker.

2. **Migration / save-corruption attack** (probe set 2): hand-built real v2 JSON
   with the key absent, a v4 JSON, claim-survives-disk, fresh-claim-false,
   claim-preserved-on-disk-upsert. Every one came back correct against the live
   runtime. v2 fields intact, v4 fail-hard intact, no claim leak, no claim drop.
   Could not corrupt the save or break the single v2→v3 bump.

3. **Architectural / asmdef-cycle attack**: searched for any `Golfin.Save →
   Golfin.Tournaments` back-edge (asmdef references, `using` directives, type
   refs) and any transitive cycle via `Golfin.UI.Rankings.Core`. Save has
   `references=[]`; Rankings.Core has `references=[]`; Save/ contains zero
   Tournaments `using`/types (doc-comments only). The full 678-test compile+run
   is itself proof the graph is acyclic. Could not find a back-edge.

---

## 7. BOUNDARY / RULE-13 ATTRIBUTION

`git diff --stat HEAD -- Assets/Scripts/Physics/` = EMPTY (no Physics edits).
HEARTBEAT iter-1 baseline (HEAD 679144e6a) correctly attributes the entire
`tournament_character_snapshot` DIRTY block (EntryState.cs, LocalTournamentBackend.cs,
CharacterSnapshot.cs(+meta), ICharacterStatsProvider.cs(+meta), TournamentsRuntime/*,
ShellScene.unity, Packages/manifest+lock) as pre-existing at T5 kickoff. T5's own
new files (`SaveBackedEntryStore.cs`(+meta), `SaveBackedEntryStoreTests.cs`(+meta))
are untracked; its mods (`SaveData.cs`, `SaveSchemaMigrator.cs`, `SaveLayerTests.cs`,
`Golfin.Tournaments.Tests.asmdef`) are in the diff. Note: the snapshot dependency's
*code* is uncommitted-but-present in the working tree, which is why T5 compiles and
all tests pass — the dependency is satisfied in the tree. Not a T5 defect.

---

## VERDICT — ARCHITECT_REVIEW_PASS

Single strongest reason to trust it: **I re-ran the binary suite myself (678/675/0/3,
0 failures), confirmed the T5 test names actually executed from the live Editor.log,
and then independently proved the three highest-risk behaviors against the live
runtime via script-execute** — the real-v2-JSON migration (schemaVersion→3, v2 fields
intact, entries empty), the v4 fail-hard guard (still throws), and the
relaunch-double-claim guard (claim survives a real disk write+reload, fresh entries
default unclaimed, claim preserved on disk upsert). The DTO shape and v2→v3 migration
that ship to every player's save are correct in both directions. Three break-attempts
failed. Hand to Cesar for final approval.
