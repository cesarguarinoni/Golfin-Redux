# ARCHITECT_REVIEW — tournament_csv_loaders (T2)

**Reviewer:** golfin-reviewer
**Date:** 2026-06-26 (system clock)
**Iteration:** 2 (post-iter-1 ARCHITECT_REVIEW_FAIL)
**Verdict:** **PASS → READY_FOR_REDTEAM**

---

## Scope note — data + parsing task

Not visual. No Figma node, no scene change, no UI deliverable. The standard pixel-scan / Figma fidelity / bbox / scene-mutation / production-flow capture checks (Steps 0, 2, 2b) do not apply. The gate is the EditMode test run for `Golfin.Tournaments.Tests` plus a code/CSV walk against SPEC §3 / §5. Per the dispatch prompt's explicit instruction, the load-bearing part of this iter-2 review is **confirming the iter-1 fixture-not-real-loader gap is genuinely closed by code reading, not by report-believing**.

---

## Primary check — iter-1 FAIL is GENUINELY closed

My iter-1 reject reason: every `LoadTournaments_…` / `LoadPrizeTables_…` / `LoadBotFields_…` test re-implemented parsing inline in private fixture helpers (`ParseTournaments` / `ParsePrizes` / `ParseBotFields`) and never invoked the shipped `TournamentCsvLoader.Load*()` instance methods; `Resources.Load<TextAsset>` and the three path constants `"Data/tournaments"` / `"Data/tournament_prizes"` / `"Data/tournament_bot_fields"` were untested.

I read `Assets/Scripts/Tournaments/Tests/TournamentCsvLoaderTests.cs` directly. The gap is closed by 4 new wrapper tests at lines 608-695, each instantiating the real loader:

| Line | New test | Real-loader call | Re-asserts SPEC §5 values on real output? |
|---|---|---|---|
| L608-L625 | `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows` | `new TournamentCsvLoader().LoadTournaments()` (L611-L612) | YES — Count==6 + Lomond row: ClubId=lomond, EntryFeeRP=0, HoleSet.Count=18, SponsorKey=GOLFIN, LeagueKey=GOLD, StartUtc.Kind=Utc, EndUtc.Kind=Utc |
| L631-L651 | `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` | `new TournamentCsvLoader().LoadPrizeTables()` (L634-L635) | YES — Count==3; prize_medium band#1 RpReward=5000 + ItemRewardId="ticket_gold"; prize_medium band 4-10 ItemRewardId IsNull |
| L657-L673 | `LoadBotFields_RealLoader_ShippedCSV_Returns3Configs` | `new TournamentCsvLoader().LoadBotFields()` (L660-L661) | YES — Count==3; field_major BotCount==30; bracketWeights sum ≈ 1.0 ± 0.0001 |
| L680-L695 | `Loader_RealEnd2End_ReferentialIntegrityHolds` | All three real `Load*()` (L683-L686) + `TournamentCsvLoader.CheckReferentialIntegrity` (L693) | YES — `IsTrue(ok)` against shipped data |

Path-constant proof: the loader is wired through `LoadAsset(TournamentsPath)` → `Resources.Load<TextAsset>(path)` at `TournamentCsvLoader.cs:42-44, 119-121, 183-185, 371`. The three path constants are at `TournamentCsvLoader.cs:32-34`:

```
private const string TournamentsPath = "Data/tournaments";
private const string PrizesPath      = "Data/tournament_prizes";
private const string BotFieldsPath   = "Data/tournament_bot_fields";
```

If any of those constants were typo'd (e.g. `"Data/tournment"`), `LoadAsset` would return null → `LoadX()` returns `Array.Empty<>` / empty dict → `Assert.AreEqual(6/3/3, …)` in the new wrapper tests FAILS. The exact gate the iter-1 FAIL named. Closed.

### Inline fixture parsers retained — acceptable

The private `ParseTournaments` / `ParsePrizes` / `ParseBotFields` helpers (test file L61-L197) still exist alongside the real-loader tests. They back the lighter unit-isolation tests (`LoadTournaments_Returns6Rows`, `LoadPrizeTables_PrizeMedium_Band1_CorrectFields`, etc.). My iter-1 fix block explicitly permitted this Alternative path: *"Either path is acceptable."*

It is **not** a FAIL — the real-loader coverage layer is what was missing, and it's now present. I do flag it for the red-team as a mild maintenance smell (two parsers shaped the same way must move in lockstep when the CSV schema changes; the cleaner long-term shape would be a single `internal static Parse*Text(string)` helper carved out of the loader, called by both the production `LoadX()` wrapper and the tests). But on the iter-2 gate this is "good enough" — surface, don't block.

---

## Full SPEC §5 re-walk (PIPELINE_HARDENING Rule 5 — fresh on iter-2, not "carry-forward")

| # | SPEC §5 criterion | Tested against | Result |
|---|---|---|---|
| 1 | `LoadTournaments()` → 6 rows | `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows` (L609) calls **real** `new TournamentCsvLoader().LoadTournaments()` | PASS |
| 2 | `lomond_championship` → ClubId=lomond, EntryFeeRP=0, HoleSet.Count=18, StartUtc/EndUtc UTC + parsed instant, SponsorKey=GOLFIN, LeagueKey=GOLD | Same real-loader test re-asserts 7 fields (L615-L624). Plus the fixture-backed `LoadTournaments_LomondChampionship_AllFields` also asserts `StartUtc == new DateTime(2026,6,24,…Utc)` and `EndUtc == new DateTime(2026,6,27,…Utc)` (exact-instant equality, not just Kind=Utc) | PASS |
| 3 | `holeSet "1-18"` → 18 ids | `ExpandHoleSet_Range_Returns18Ids` (L250) calls `TournamentCsvLoader.ExpandHoleSet` directly | PASS |
| 4 | `holeSet "1,4,7"` → 3 ids | `ExpandHoleSet_CommaList_Returns3Ids` (L259) calls shipped static directly | PASS |
| 5 | `LoadPrizeTables()` → 3 tables | `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` (L632) calls **real** `new TournamentCsvLoader().LoadPrizeTables()` | PASS |
| 6 | `prize_medium` band #1 RpReward=5000 / ItemRewardId="ticket_gold" | Same real-loader test (L642-L645) | PASS |
| 7 | `prize_medium` band 4-10 ItemRewardId is null | Same real-loader test (L648-L650) — explicit `Assert.IsNull` | PASS |
| 8 | `LoadBotFields()` → 3 configs | `LoadBotFields_RealLoader_ShippedCSV_Returns3Configs` (L658) calls **real** `new TournamentCsvLoader().LoadBotFields()` | PASS |
| 9 | `field_major` BotCount=30, BracketWeights sum ≈ 1.0 ± ε | Same real-loader test (L669-L672). Plus dedicated unit test `ParseBracketWeights_AllSixBrackets_ValidInput` (L507) | PASS |
| 10 | `field_major` bracket keys ⊂ {1,10,25,50,100,180} | `LoadBotFields_FieldMajor_KeysAreValidBrackets` (L367) exercises shipped `ParseBracketWeights`; shipped CSV row is `25:…;50:…;100:…;180:…` — all keys in the set | PASS |
| 11 | Referential integrity: every tournament `prizeTableId`/`botFieldId` resolves; dangling logs error + returns false | `Loader_RealEnd2End_ReferentialIntegrityHolds` (L681) calls all three real `Load*` + `CheckReferentialIntegrity` and asserts `IsTrue(ok)`. Two negative-control tests (L432, L464) use `LogAssert.Expect` + `Assert.IsFalse(ok)` against shipped `CheckReferentialIntegrity`. | PASS |

---

## Other items independently re-verified

- **§0.1 T1 amendment (`TournamentDefinition.ResolveDelayMinutes`).** Additive change only: new property between EndUtc and EntryFeeRP; ctor signature gains `int resolveDelayMinutes` in the same position; ctor body assigns. All 4 prior call sites updated (`StubTournamentBackend.cs`, `TournamentContractsTests.cs`, `BotFieldInvariantTests.cs` x2). Round-trip test `TournamentDefinition_ResolveDelayMinutes_RoundTrip` at L518 PASSes. PASS.
- **T1 tests still green.** T1 `TournamentContractsTests` 15/15 PASS + T3 `BotFieldInvariantTests` 36/36 PASS, included in the 81/81. PASS.
- **Loader is POCO in `Golfin.Tournaments` asmdef.** `public sealed class TournamentCsvLoader`, file at `Assets/Scripts/Tournaments/TournamentCsvLoader.cs`, not a MonoBehaviour. PASS.
- **Header-name mapping + skip `#`/blank lines.** Confirmed in loader code; matches the project idiom (`ModesDatabaseCSV` / `CharacterDatabaseCSV`). `LoadTournaments_CommentLinesSkipped` (L239) covers it. PASS.
- **CSVs verbatim against SPEC §3.** I re-read all three files in iter-2. `tournaments.csv` (6 rows, sponsors PUMA / GOLFIN×4 / TAIHEIYO, leagues DIAMOND×3 / GOLD×2 / SILVER, only `gotemba_masters` has entryFee=500), `tournament_prizes.csv` (10 rows across 3 tables, band sequences correct, `ticket_gold` on `prize_medium`/1-1 + `prize_major`/2-3, `trophy_major` on `prize_major`/1-1), `tournament_bot_fields.csv` (3 rows, all three bracketWeights sums = 1.0 exact, all keys subset of {1,10,25,50,100,180}). No drift from iter-1. PASS.
- **Physics standing ban.** `git diff HEAD -- Assets/Scripts/Physics/` returns empty. PASS.
- **Files-modified table vs `git status --porcelain --untracked-files=all`.** Matches the report's table exactly. `Assets/Scenes/ShellScene.unity` modification is correctly attributed as pre-existing per the iter-1 HEARTBEAT baseline block (HEAD `5a30d696b…`). Rule 13 satisfied. PASS.
- **No scope drift in iter-2.** Only `TournamentCsvLoaderTests.cs` was modified in iter-2 (appended 4 wrapper tests). No loader edits, no CSV edits, no T1 contract edits beyond iter-1's amendment. PASS.

---

## Independent test re-run (PIPELINE_HARDENING Rule 6)

Re-executed via `unity-mcp-cli run-tool tests-run --input-file …`:

```
testMode=EditMode, testAssembly=Golfin.Tournaments.Tests, includePassingTests=true
Status=Passed, TotalTests=588, PassedTests=81, FailedTests=0, SkippedTests=0, Duration=00:00:01.99
```

The 4 new iter-2 tests appear in the per-test PASS enumeration I pulled:
- `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows` — Passed
- `LoadPrizeTables_RealLoader_ShippedCSV_Returns3Tables` — Passed
- `LoadBotFields_RealLoader_ShippedCSV_Returns3Configs` — Passed
- `Loader_RealEnd2End_ReferentialIntegrityHolds` — Passed

The implementer's 81/81 claim is genuine, not fabricated. Rule 6 satisfied.

---

## Mesh metrics

N/A — not a mesh/terrain task. (SPEC has no `green.json`, `TerrainData`, or mesh-cut/deform involvement; this is a CSV parser.)

## Figma fidelity

N/A — SPEC has no Figma NODE reference. The `13386:1758` mention in §3 is contextual ("the data enables those Figma cards when T6 lands"), not "this task renders that frame." Rule 18 does not apply.

## Bbox verification

N/A — no UI containment claims.

## Scene-mutation audit

`git diff HEAD --stat -- Assets/Scenes/` shows only `ShellScene.unity` (269 insertions / 93 deletions). The iter-1 HEARTBEAT baseline block confirmed this scene was already dirty at kickoff (HEAD `5a30d696b…`); the implementer reports it as pre-existing in iter-1 and made no further edits in iter-2. No T2-introduced scene mutations. PASS.

## Capture-mechanism audit

N/A — no video, no capture path involved.

---

## Note for the red-team (surface, not block)

One observation worth airing for the adversarial gate — explicitly **not** a FAIL on my side:

The private fixture parsers `ParseTournaments` / `ParsePrizes` / `ParseBotFields` at `TournamentCsvLoaderTests.cs` L61-L197 duplicate the shipped loader's parsing logic. Today both are exercised and both PASS. The schema-drift risk: if the CSV schema (or any DTO ctor signature) changes in a future task and the test fixture isn't updated in lockstep, the fixture-backed tests could go on PASSing while the real loader behaviour diverges from what the fixture tests assert. The 4 new real-loader wrapper tests substantially mitigate this (a header column rename without a loader update would FAIL `LoadTournaments_RealLoader_ShippedCSV_Returns6Rows`). The clean long-term shape is to carve `internal static Parse*Text(string)` helpers out of `TournamentCsvLoader.cs` and have both the production `LoadX()` wrapper and the tests call them — eliminating the parallel parser. This is a follow-up refactor candidate, not an iter-2 blocker.

---

## Verdict

**PASS → READY_FOR_REDTEAM.**

The iter-1 ARCHITECT_REVIEW_FAIL coverage gap is genuinely closed via 4 real-loader wrapper tests that instantiate `new TournamentCsvLoader()` and call the shipped `LoadTournaments()`/`LoadPrizeTables()`/`LoadBotFields()` instance methods against the `Resources/Data/` CSVs, re-asserting the SPEC §5 values on real-loader output. The `Resources.Load<TextAsset>` path and three path constants are now exercised — a typo in any of them would fail the relevant new test. Loader semantics unchanged, CSV contents unchanged (verbatim §3), §0.1 T1 amendment intact, T1 + T3 suites still green, full §5 acceptance covered including referential-integrity negative controls. 81/81 EditMode tests PASS independently re-verified (Status=Passed, TotalTests=588, PassedTests=81, FailedTests=0). No Physics edits, no T2-introduced scene mutations, files-modified table matches `git status` exactly. The retained inline fixture parsers are a noted maintenance smell, not a gate failure.

Handing to `golfin-redteam-reviewer`. Setting STATUS.md → `READY_FOR_REDTEAM`.
