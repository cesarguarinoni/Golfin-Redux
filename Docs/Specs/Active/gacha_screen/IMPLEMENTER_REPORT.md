# Implementer Report — `gacha_screen` STAGE 2 — iter-3 (test-coverage fix)

**Iteration shape:** `tests:target_production_type`

## Implementation summary

Stage 2 iter-3 is a test-only fix. The red-team reviewer flagged that 7 of the 15 Stage 2 EditMode tests exercised LOCAL COPY logic (`EntryRow`, `ParseCsvDirect`, `FilterLive`) rather than the real `GachaBannerCatalog` production code — a classic circular gate (`feedback_tests_must_target_production_type`). The fix has two parts: (1) two `internal` testable seam methods were added to `GachaBannerModel.cs` (`ParseCsv(string)` and `GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime)`) — the production public methods now delegate to these seams, so behavior is a pure refactor; (2) the test file was rewritten to delete all local mirrors and rewire the 7 tests to call the production seams via `System.Reflection` (the established project pattern for cross-`Assembly-CSharp` test access). All 15/15 tests pass. No prefab, CSV, scene, or Stage 0/1 production code was changed.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/UI/Gacha/GachaBannerModel.cs` | Modified — added `[assembly: InternalsVisibleTo]`, extracted `internal static ParseCsv(string)` seam from `LoadFromCsv()`, extracted `internal static GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime)` seam from public `GetLiveBanners()`. Pure refactor: production behavior identical. |
| `Assets/Tests/EditMode/GachaStage2Tests.cs` | Rewritten — deleted `EntryRow` struct, `ParseCsvDirect()`, `FilterLive()`. Added reflection infrastructure (`_catalogType`, `_entryType`, `_parseCsvMethod`, `_getLiveOverloadMethod`, helpers). Rewired 7 tests (`CsvParse_*` x 3, `GetLiveBanners_*` x 4) to call production code via reflection. Kept 8 `FormatCountdown_*` tests unchanged. |

## Screenshot

Stage 2 iter-3 is a test-only fix. No UI was changed. The canonical screenshot from iter-2 remains the visual deliverable for Stage 2.

Canonical screenshot: `screenshots/gacha_stage2_canonical.png`

(No new screenshot required — zero UI changes in this iteration.)

## Rejection follow-up

The red-team ARCHITECT_REVIEW_FAIL was a single defect: circular test gate on 7 catalog/filter tests. No Cesar visual rejection exists for Stage 2 iter-3 (no `CESAR_REJECTION.md` for this stage).

| Flagged defect | Verdict | Evidence |
|---|---|---|
| 7 tests use local `EntryRow`/`ParseCsvDirect`/`FilterLive` mirrors, not production code | RESOLVED | Grep confirms all three local mirror symbols are ABSENT. 15/15 tests PASS against production seams via reflection. |

## Production invocation proof (closing the circular gate)

The only acceptable gate: the 7 rewired tests call `GachaBannerCatalog` production methods via `System.Reflection`. If the production parser or filter regresses, these tests FAIL.

### grep — no local mirrors remain

```
grep -n "ParseCsvDirect\|FilterLive\|EntryRow\|struct " Assets/Tests/EditMode/GachaStage2Tests.cs
(no output — all three local mirror symbols are absent)
```

### grep — production type is invoked

```
grep -n "GachaBannerCatalog" Assets/Tests/EditMode/GachaStage2Tests.cs

Line 33:  Type.GetType("GolfinRedux.UI.Gacha.GachaBannerCatalog, Assembly-CSharp");
Lines 43-49: _parseCsvMethod and _getLiveOverloadMethod wired to GachaBannerCatalog seams
Lines 80-92: Assert.IsNotNull guards — tests FAIL immediately if seam is missing from production code
Lines 155,183,195: CsvParse_* tests comment "Calls PRODUCTION GachaBannerCatalog.ParseCsv"
Lines 217,232,247,263: GetLiveBanners_* tests comment "Calls PRODUCTION GachaBannerCatalog.GetLiveBanners"
```

### Test run result (mcp__ai-game-developer__tests-run)

```
testClass: GachaStage2Tests, testMode: EditMode
Summary: Status=Passed, TotalTests=848, PassedTests=15, FailedTests=0
Duration: 00:00:00.683

GachaStage2Tests.CsvParse_BadEndUtcDate_DefaultsToMaxValue  — Passed (0.141s)
GachaStage2Tests.CsvParse_LockedColumns_AllFieldsCorrect    — Passed (0.113s)
GachaStage2Tests.CsvParse_MalformedRow_Skipped              — Passed
GachaStage2Tests.FormatCountdown_59Seconds                  — Passed
GachaStage2Tests.FormatCountdown_ExactOneDay                — Passed
GachaStage2Tests.FormatCountdown_LessThanOneDay             — Passed
GachaStage2Tests.FormatCountdown_LessThanOneHour            — Passed
GachaStage2Tests.FormatCountdown_LessThanOneMinute          — Passed
GachaStage2Tests.FormatCountdown_MultiDay                   — Passed
GachaStage2Tests.FormatCountdown_Negative_ReturnsZeroS      — Passed
GachaStage2Tests.FormatCountdown_Zero_ReturnsZeroS          — Passed
GachaStage2Tests.GetLiveBanners_AllExpired_ReturnsEmpty     — Passed
GachaStage2Tests.GetLiveBanners_ExcludesInactive            — Passed
GachaStage2Tests.GetLiveBanners_ExcludesPastEndUtc          — Passed
GachaStage2Tests.GetLiveBanners_SortsBySortOrder            — Passed
```

15/15 PASS. Zero failures.

## Figma fidelity

Not applicable — Stage 2 iter-3 is a test-file fix with no UI changes. The Figma fidelity table was submitted and passed in Stage 2 iter-2.

## UI fidelity lint

Not applicable — no prefab was changed in this iteration. The lint results (GachaBannerCard: fail=0, GeneralShopScreen: fail=0) were submitted in Stage 2 iter-2 and are unchanged.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `ParseCsvDirect`, `FilterLive`, `EntryRow` struct deleted from test file | PASS | grep on `GachaStage2Tests.cs` returns no output for all three symbols — confirmed absent |
| 7 rewired tests call `GachaBannerCatalog` production code via reflection | PASS | `_parseCsvMethod = _catalogType?.GetMethod("ParseCsv", NonPublic|Static, ...)` + `_getLiveOverloadMethod` found by `Parameters.Length==2` loop — both resolve to real production methods; 7 invocation sites confirmed via grep |
| Tests FAIL if production seam is missing (not silently green) | PASS | Each helper has `Assert.IsNotNull(_parseCsvMethod, "... seam missing?")` and `Assert.IsNotNull(_getLiveOverloadMethod, "... seam missing?")` — a removed seam causes immediate test FAIL |
| 15/15 EditMode tests pass | PASS | `tests-run` result: `Status=Passed, PassedTests=15, FailedTests=0` (full output above) |
| 8 `FormatCountdown_*` tests unchanged | PASS | FormatCountdown tests call `GachaCarouselController.FormatCountdown` via same reflection path as iter-2; all 8 pass unchanged |
| `GachaBannerModel.cs` production behavior is a pure refactor (no change to public API or logic) | PASS | Public `GetLiveBanners()` delegates to `GetLiveBanners(_entries, DateTime.UtcNow)`; `LoadFromCsv()` delegates to `ParseCsv(asset.text)` — identical logic, identical results, unchanged public surface |
| `[assembly: InternalsVisibleTo]` placed after `using` directives (C# grammar) | PASS | Compilation clean after fix (`assets-refresh` returned Success; `IsCompiling=false`; no `error CS1529` in Editor.log) |
| No production carousel/catalog/countdown behavior changed | PASS | Only two internal seam methods added to `GachaBannerCatalog`; `GachaBannerCard.cs`, `GachaCarouselController.cs`, `GachaBannerCard.prefab`, `GeneralShopScreen.prefab`, `ShellScene.unity`, CSV, Stage 0/1 code — all untouched |
| `git diff HEAD -- Assets/Scripts/Physics/` shows no diff | PASS | No Physics files touched; only Gacha scripts and Tests changed |

## Known FAIL items

None.

## Spec deviations

None. The seam methods use `internal` (not `public`) per the testable-seam pattern. `InternalsVisibleTo` exposes them to the test assembly. Tests still use `System.Reflection` per the established project pattern for `Assembly-CSharp` cross-assembly access (consistent with `StaminaShopAddEnergyTests.cs`).

## Console output

```
Assets refresh completed: AssetDatabase   (no compilation errors)
IsCompiling: false                        (confirmed before report)
[GachaBannerCatalog] Loaded 3 banner entries.   (from gacha_banners.csv in Resources/Data/)
```

## Open questions for Architect

None.
