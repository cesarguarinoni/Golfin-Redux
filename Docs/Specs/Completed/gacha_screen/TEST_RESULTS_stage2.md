# Stage 2 EditMode test results (run by main thread via Unity MCP tests-run, 2026-07-13)

`GolfinRedux.Tests.EditMode.GachaStage2Tests` — **15 passed, 0 failed, 0 skipped**.

Catalog:
- CsvParse_LockedColumns_AllFieldsCorrect
- CsvParse_MalformedRow_Skipped
- CsvParse_BadEndUtcDate_DefaultsToMaxValue
- GetLiveBanners_ExcludesInactive
- GetLiveBanners_ExcludesPastEndUtc
- GetLiveBanners_SortsBySortOrder
- GetLiveBanners_AllExpired_ReturnsEmpty

Countdown formatter:
- FormatCountdown_MultiDay / _ExactOneDay / _LessThanOneDay / _LessThanOneHour /
  _LessThanOneMinute / _59Seconds / _Zero_ReturnsZeroS / _Negative_ReturnsZeroS

Runner: `mcp ai-game-developer tests-run` (EditMode), 2026-07-13, Status=Passed.
Stage 1 suites (GachaTicket 11 / SaveLayer 15 / ClubOwnership 9) remain green (unchanged this stage).

## iter-3 update (circular-gate fix, 2026-07-13)
The 7 catalog/filter tests now target the PRODUCTION type:
- 3 `CsvParse_*` → `GachaBannerCatalog.ParseCsv(string)` (internal seam) via reflection
- 4 `GetLiveBanners_*` → `GachaBannerCatalog.GetLiveBanners(entries, nowUtc)` (internal seam) via reflection
Local `EntryRow`/`ParseCsvDirect`/`FilterLive` mirrors DELETED. `[InternalsVisibleTo("GolfinRedux.Tests.EditMode")]`
added; public LoadFromCsv/GetLiveBanners delegate to the seams (pure refactor). Re-run: 15/15 PASS (main thread).
