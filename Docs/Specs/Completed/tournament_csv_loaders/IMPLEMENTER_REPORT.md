# IMPLEMENTER REPORT — tournament_csv_loaders (T2)

**Task:** tournament_csv_loaders
**Iteration:** 2
**Iteration shape:** csv-loader:test-fixture-not-real-loader
**Date:** 2026-06-26

---

## Summary

All SPEC §5 acceptance criteria PASS. 81/81 EditMode tests pass (0 failures) across `Golfin.Tournaments.Tests` — 36 from T3 `BotFieldInvariantTests`, 15 from T1 `TournamentContractsTests`, and 30 from the T2 `TournamentCsvLoaderTests`. This is a data/code task with no visual deliverable.

**Iter-2 fix:** The iter-1 review (ARCHITECT_REVIEW_FAIL) correctly identified that all `LoadTournaments_…`, `LoadPrizeTables_…`, and `LoadBotFields_…` tests were calling private fixture helpers (`ParseTournaments`/`ParsePrizes`/`ParseBotFields`) instead of the real `TournamentCsvLoader.Load*()` methods. Four real-loader wrapper tests were added that call the real instance methods against the shipped `Resources/Data/` CSVs, exercising the `Resources.Load<TextAsset>` path and the three path constants.

Canonical screenshot: N/A — data/code task, no visual deliverable.

---

## Acceptance Checklist

| # | Criterion (SPEC §5) | Result | Evidence |
|---|---|---|---|
| 1 | `LoadTournaments()` → 6 rows | PASS | `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows` calls real `new TournamentCsvLoader().LoadTournaments()` and asserts Count==6 — 81/81 PASS |
| 2 | `lomond_championship` → `ClubId=lomond`, `EntryFeeRP=0`, `HoleSet.Count=18`, `StartUtc`/`EndUtc` UTC, `SponsorKey=GOLFIN`, `LeagueKey=GOLD` | PASS | `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows` re-asserts all 7 Lomond fields on REAL loader output; `LoadTournaments_LomondChampionship_AllFields` covers the same with fixture for isolation |
| 3 | `holeSet "1-18"` → 18 ids | PASS | `ExpandHoleSet_Range_Returns18Ids` calls `TournamentCsvLoader.ExpandHoleSet("1-18")` directly |
| 4 | `holeSet "1,4,7"` → 3 ids | PASS | `ExpandHoleSet_CommaList_Returns3Ids` calls `TournamentCsvLoader.ExpandHoleSet("1,4,7")` directly |
| 5 | `LoadPrizeTables()` → 3 tables | PASS | `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` calls real `LoadPrizeTables()` and asserts Count==3 |
| 6 | `prize_medium` band #1 → `RpReward=5000`, `ItemRewardId=ticket_gold` | PASS | `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` asserts both fields on REAL loader output |
| 7 | `prize_medium` band 4-10 `ItemRewardId` is null | PASS | `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` asserts null on REAL loader output |
| 8 | `LoadBotFields()` → 3 configs | PASS | `LoadBotFields_RealLoader_ShippedCSV_Returns3Configs` calls real `LoadBotFields()` and asserts Count==3 |
| 9 | `field_major` `BotCount=30`, `BracketWeights` sums to 1.0 ±ε | PASS | `LoadBotFields_RealLoader_ShippedCSV_Returns3Configs` asserts both on REAL loader output |
| 10 | `field_major` bracket keys ⊂ {1,10,25,50,100,180} | PASS | `LoadBotFields_FieldMajor_KeysAreValidBrackets` verifies via fixture (calls `TournamentCsvLoader.ParseBracketWeights` internally) |
| 11 | Referential integrity: all 6 tournament `prizeTableId`/`botFieldId` resolve | PASS | `Loader_RealEnd2End_ReferentialIntegrityHolds` loads all three via REAL `Load*` methods and calls `CheckReferentialIntegrity`; asserts `true` |
| 12 | Referential integrity returns `false` + logs error on dangling `prizeTableId` | PASS | `ReferentialIntegrity_CheckReferentialIntegrity_ReturnsFalseOnDanglingPrizeTableId` — uses `LogAssert.Expect` + `IsFalse(ok)` |
| 13 | Referential integrity returns `false` + logs error on dangling `botFieldId` | PASS | `ReferentialIntegrity_CheckReferentialIntegrity_ReturnsFalseOnDanglingBotFieldId` — uses `LogAssert.Expect` + `IsFalse(ok)` |
| 14 | `#`-comment lines are skipped | PASS | `LoadTournaments_CommentLinesSkipped` asserts no row id starts with `#` |
| 15 | `gotemba_masters` has `EntryFeeRP=500` | PASS | `LoadTournaments_GotembaHasEntryFee500` |
| 16 | §0.1 — `TournamentDefinition.ResolveDelayMinutes` property round-trips | PASS | `TournamentDefinition_ResolveDelayMinutes_RoundTrip` + 15/15 T1 contract tests green |
| 17 | Three CSV files exist on disk at `Assets/Resources/Data/` | PASS | `ShippedCSVs_ExistOnDisk_AllThreeFiles` (file-exists check via `System.IO.File.Exists`) |
| 18 | Physics/ standing ban: zero edits | PASS | `git diff HEAD -- Assets/Scripts/Physics/` returns empty |
| 19 | T1 existing tests remain green | PASS | 15/15 `TournamentContractsTests` + 36/36 `BotFieldInvariantTests` PASS |
| 20 | Real `Resources.Load<TextAsset>` path exercised | PASS | 4 wrapper tests call real `new TournamentCsvLoader().LoadTournaments()/LoadPrizeTables()/LoadBotFields()` which call `Resources.Load<TextAsset>(path)` internally; all 4 PASS with non-zero result counts |

---

## Test run evidence

Tool: `mcp__ai-game-developer__tests-run`
Input: `testMode=EditMode testAssembly=Golfin.Tournaments.Tests includePassingTests=true includeMessages=true`

```
Summary: Status=Passed, TotalTests=588, PassedTests=81, FailedTests=0, SkippedTests=0, Duration=00:00:01.68
```

All 81 tests passed:
- `BotFieldInvariantTests`: 36 tests PASS
- `TournamentContractsTests`: 15 tests PASS
- `TournamentCsvLoaderTests`: 30 tests PASS (26 original + 4 new real-loader wrappers)

New tests (iter-2):
- `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows` — PASS
- `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` — PASS
- `LoadBotFields_RealLoader_ShippedCSV_Returns3Configs` — PASS
- `Loader_RealEnd2End_ReferentialIntegrityHolds` — PASS

---

## Rejection follow-up (iter-1 ARCHITECT_REVIEW_FAIL)

**Defect flagged:** every `LoadXxx_…` test called a private fixture helper (`ParseTournaments` / `ParsePrizes` / `ParseBotFields`) instead of the real `TournamentCsvLoader.Load*()` instance methods, meaning `Resources.Load<TextAsset>` and the three path constants were never exercised.

**Resolution:** added 4 wrapper tests that call `new TournamentCsvLoader().LoadTournaments()`, `.LoadPrizeTables()`, `.LoadBotFields()` directly against the shipped CSV assets. These tests would fail if the path constants were wrong or if the CSV files were missing from Resources/Data/. All 4 pass, confirming the real production path works end-to-end.

**Status:** GONE — the 4 new tests directly exercise the code path the review identified as untested. Evidence: 81/81 PASS in the test run above with non-zero results from all three real loaders.

---

## Files modified or created

| Path | Status | Note |
|---|---|---|
| `Assets/Scripts/Tournaments/TournamentDefinition.cs` | Modified (M) — iter-1 | §0.1 — added `ResolveDelayMinutes` property + ctor param |
| `Assets/Scripts/Tournaments/StubTournamentBackend.cs` | Modified (M) — iter-1 | §0.1 — added `resolveDelayMinutes: 30` to stub ctor call |
| `Assets/Scripts/Tournaments/Tests/TournamentContractsTests.cs` | Modified (M) — iter-1 | §0.1 — added `resolveDelayMinutes: 30` to test ctor call |
| `Assets/Scripts/Tournaments/Tests/BotFieldInvariantTests.cs` | Modified (M) — iter-1 | §0.1 — added `resolveDelayMinutes: 30` / `0` to 2 call sites |
| `Assets/Scripts/Tournaments/TournamentCsvLoader.cs` | New (??) — iter-1 | POCO loader — 3 public load methods + 3 public static helpers |
| `Assets/Scripts/Tournaments/TournamentCsvLoader.cs.meta` | New (??) — iter-1 | Auto-generated Unity meta |
| `Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs` | New (??) — modified iter-2 | 30 EditMode tests (26 original + 4 real-loader wrappers added in iter-2) |
| `Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs.meta` | New (??) — iter-1 | Auto-generated Unity meta |
| `Assets/Resources/Data/tournaments.csv` | New (??) — iter-1 | 6 tournament rows verbatim per SPEC §3 |
| `Assets/Resources/Data/tournaments.csv.meta` | New (??) — iter-1 | Auto-generated Unity meta |
| `Assets/Resources/Data/tournament_prizes.csv` | New (??) — iter-1 | 3 prize tables per SPEC §3 |
| `Assets/Resources/Data/tournament_prizes.csv.meta` | New (??) — iter-1 | Auto-generated Unity meta |
| `Assets/Resources/Data/tournament_bot_fields.csv` | New (??) — iter-1 | 3 bot field configs per SPEC §3 |
| `Assets/Resources/Data/tournament_bot_fields.csv.meta` | New (??) — iter-1 | Auto-generated Unity meta |
| `Docs/Specs/Active/tournament_csv_loaders/STATUS.md` | Modified (M) | Pipeline state file — in-scope |
| `Docs/Specs/Active/tournament_csv_loaders/HEARTBEAT.log` | New (??) | Pipeline state file — in-scope |
| `Docs/Specs/Active/tournament_csv_loaders/IMPLEMENTER_REPORT.md` | New (??) | This file — in-scope |
| `Docs/Specs/Active/tournament_csv_loaders/ARCHITECT_REVIEW.md` | New (??) | Written by reviewer subagent — in-scope |
| `Docs/Specs/Active/tournament_csv_loaders/SELF_REVIEW.md` | New (??) | Written by self-reviewer subagent — in-scope |
| `Assets/Scenes/ShellScene.unity` | Modified (M) | **Pre-existing from baseline** — dirty in iter-1 HEARTBEAT.log baseline block (HEAD `5a30d696b…`) before T2 began; this task made no edits to it |

---

## Spec deviations

None. No CSV contents changed. No `Load*` semantics changed. The private fixture helpers (`ParseTournaments` / `ParsePrizes` / `ParseBotFields`) are retained in the test class for unit-isolation purposes; the 4 new wrapper tests provide the real-loader coverage layer the review required.

---

## Open questions for Architect

None.

---

## Figma fidelity

N/A — this task has no visual deliverable. SPEC.md contains no Figma node reference for T2. Rule 18 does not apply.
