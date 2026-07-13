# RED-TEAM REVIEW — gacha_screen Stage 1 (re-verify single blocker fix)

**Date:** 2026-07-12 10:41 JST
**Scope:** Re-verify ONLY the one blocker from the prior FAIL (cross-referenced test-grant seed sites). All other Stage 1 items were cleared on the prior pass and are not re-litigated.
**Verdict:** ARCHITECT_REVIEW_PASS

## The prior FAIL floor
Prior pass FAILed on exactly one item: the two test-grant seed sites (SaveSchemaMigrator v6→v7 and GachaTicketManager.Awake) did not cross-reference each other, risking a partial ship-revert (revert one → emptied balances silently refill to 10).

## Verification of the fix

### 1. Cross-references present and MUTUALLY findable — GONE (blocker resolved)
- **GachaTicketManager.cs lines 50-53** (over the `gachaTickets == 0 → DEFAULT_STARTING_TICKETS` guard):
  > `TODO: remove this Awake guard when reverting the test grant to 0.`
  > `ALSO revert the paired seed in SaveSchemaMigrator.cs (v6→v7 block, \`data.gachaTickets = 10\`). Both sites must be reverted together — reverting only one leaves emptied balances silently refilling to 10.`
  Names the OTHER site by file + block + exact code line. ✅
- **SaveSchemaMigrator.cs lines 103-107** (over the `data.gachaTickets = 10` migration seed):
  > `TODO: revert test grant to 0 before ship.`
  > `ALSO revert the paired seed in GachaTicketManager.Awake (the \`gachaTickets == 0 → DEFAULT_STARTING_TICKETS\` guard, ~line 51). Both sites must be reverted together — reverting only one leaves emptied balances silently refilling to 10.`
  Names the OTHER site by file + method + guard expression + line. ✅

A dev landing on EITHER TODO is pointed to the other by concrete identifiers (file, block/method, exact expression). "Revert both together" is explicit at both sites, with the failure mode spelled out. Mutually findable — satisfied.

### 2. Edit is comment-only (no logic/behavior change) — CONFIRMED
- `git diff` on SaveSchemaMigrator.cs shows the v6→v7 logic block intact: `if (data.schemaVersion < 7) { data.gachaTickets = 10; data.schemaVersion = 7; }`. `CurrentSchemaVersion = 7` unchanged.
- GachaTicketManager.cs lines 54-58 read directly: guard `if (SaveDataHost.Instance.Data.gachaTickets == 0) { ... = DEFAULT_STARTING_TICKETS; MarkDirty(); }` unchanged; `DEFAULT_STARTING_TICKETS = 10` (line 23) unchanged.
- The only changes are the added comment lines. Behavior identical.

### 3. No regression / no other seed site missed — CONFIRMED
- Grep of all `gachaTickets =` assignments across `Assets/Scripts`: exactly TWO production seed sites (GachaTicketManager.Awake, SaveSchemaMigrator v6→v7) — the two the pair now cross-references. Every other hit is a test fixture (`GachaTicketTests.cs`, `ClubOwnershipTests.cs`), not a ship-revert site. No third grant site was left un-cross-referenced.
- Orchestrator re-ran GachaTicketTests: 11/11 PASS (compile clean, behavior unchanged) — consistent with a comment-only edit.

## Break attempts
- **Partial-revert trap (the original blocker):** attempted to imagine a dev reverting one site. Now blocked — each TODO explicitly names the other site AND the silent-refill consequence. Failed to break.
- **Hidden third seed site:** grepped for any other `gachaTickets =` / `DEFAULT_STARTING_TICKETS` write. Only the two referenced production sites plus test fixtures. Failed to break.
- **Comment claims logic that isn't there:** verified the referenced expressions (`data.gachaTickets = 10`, `gachaTickets == 0 → DEFAULT_STARTING_TICKETS`) actually exist at the cited locations. They do. Failed to break.

Blocker resolved; nothing else regressed. Advancing to Cesar.

---

# RED-TEAM REVIEW — gacha_screen Stage 2 (re-verify single blocker fix, iter-3)

**Date:** 2026-07-13 11:31 JST
**Scope:** Re-verify ONLY the one Stage 2 blocker from the prior FAIL — the 7 catalog/filter tests exercised LOCAL mirrors (EntryRow/ParseCsvDirect/FilterLive), giving the production parser/filter zero real coverage. All other Stage 2 items were cleared on the prior pass and are not re-litigated.
**Verdict:** ARCHITECT_REVIEW_PASS

## The prior FAIL floor
The 3 `CsvParse_*` and 4 `GetLiveBanners_*` tests called local in-test copies, not `GachaBannerCatalog`. A shipped parser/filter regression would not fail them (circular gate).

## Verification of the fix (files read in full, not trusted from report)

### 1. Tests now target PRODUCTION, non-circular — CONFIRMED
- `GachaStage2Tests.cs` line 32-33: `_catalogType = Type.GetType("GolfinRedux.UI.Gacha.GachaBannerCatalog, Assembly-CSharp")`. The 3 CsvParse tests → `ParseCsvViaProd` → `_parseCsvMethod.Invoke` where `_parseCsvMethod` is `GachaBannerCatalog.ParseCsv(string)` (NonPublic|Static) via reflection (lines 44-47, 78-83). The 4 GetLiveBanners tests → `GetLiveViaProd` → the 2-param `GachaBannerCatalog.GetLiveBanners(entries,nowUtc)` seam, resolved by `FindLiveOverload()` filtering NonPublic|Static named GetLiveBanners with 2 params (lines 60-68, 89-94). `GachaBannerEntry` is the production type from Assembly-CSharp (line 36-37), created via `Activator.CreateInstance` — no local data class.
- Mirror grep: `ParseCsvDirect|FilterLive|EntryRow|class .*Row` appears ONLY in the comment at line 14 documenting the deletion; NO definition remains. `GachaBannerCatalog` referenced 22× in the test file (was zero pre-fix).
- Non-circular: a broken production `ParseCsv` (wrong col index, no header-skip, bad date fallback) or `GetLiveBanners` (`>=` vs `>`, missing sort, wrong active check) is observed directly through the reflected invoke and WOULD fail the asserts.

### 2. Seams real + behavior-preserving refactor — CONFIRMED
- `GachaBannerModel.cs`: `internal static List<GachaBannerEntry> ParseCsv(string)` (line 107) and `internal static List<GachaBannerEntry> GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime)` (line 72) are real methods.
- Public delegators: `GetLiveBanners()` (line 61) = `EnsureLoaded(); return GetLiveBanners(_entries, DateTime.UtcNow)`; `LoadFromCsv()` (line 88) = `_entries = ParseCsv(asset.text)`. The seam filter (Active && EndUtc > nowUtc, sort SortOrder asc) matches the documented shipping behavior and the `IsLive` property (`>`). Parse logic (header-skip, `<9` col skip, `DateTime.MaxValue` fallback) unchanged. Pure extraction — behavior identical.
- Production consumer `GachaCarouselController.cs:130` calls the public `GachaBannerCatalog.GetLiveBanners()` — the shipping path IS the delegator that routes through the tested seam. The test exercises the real code the game runs.
- `[assembly: InternalsVisibleTo("GolfinRedux.Tests.EditMode")]` (line 17) targets the exact asmdef name (`GolfinRedux.Tests.EditMode.asmdef` → `"name": "GolfinRedux.Tests.EditMode"`). Correct. (Redundant for the reflection path, harmless.)
- Only ONE production `GachaBannerCatalog` / `ParseCsv` / 2-param `GetLiveBanners` exists in the codebase (grep) — no duplicate/hidden production copy.

### 3. Can't be defeated — CONFIRMED
- Seam guards gate on existence: `Assert.IsNotNull(_parseCsvMethod, ...)` / `Assert.IsNotNull(_getLiveOverloadMethod, ...)` fire BEFORE invoke (lines 81, 92). If a seam were deleted, the MethodInfo is null → test FAILS at the guard, not a silent pass.
- Exception-swallow: `MethodInfo.Invoke` on a throwing seam raises `TargetInvocationException` → test FAILS. No try/catch swallow in the helpers.
- Hidden copy path: none — grep confirms no local parse/filter/data mirror survives.

## Break attempts (all failed)
- **Circular residue:** hunted for any surviving local mirror the tests still call. Only the deletion comment remains; every catalog/filter assertion runs through Assembly-CSharp reflection. Failed to break.
- **Refactor changed behavior:** compared delegators vs seams — filter predicate, sort, parse col-mapping, date fallback all identical to the documented Stage 2 behavior and to the `IsLive` property. Failed to break.
- **Test passes with a dead seam:** the IsNotNull guards + Invoke exception propagation make a missing/throwing seam a hard FAIL. Failed to break.

Files: both are new/untracked Stage 2 files (no prior committed version to diff; evaluated as-is). Main-thread run: `GachaStage2Tests` 15/15 PASS. Single blocker resolved; no already-cleared item re-litigated; nothing regressed. Advancing to Cesar.
