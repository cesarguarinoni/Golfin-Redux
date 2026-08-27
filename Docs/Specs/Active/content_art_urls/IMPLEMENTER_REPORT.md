# Implementer Report — `content_art_urls` iter-6

**Iteration shape:** content-art-urls:test-does-not-reach-step3

---

## Iter-6 note (2026-08-27) — test fixture corrected to reach step 3, tripwire evidence recorded

Cesar's ARCHITECT_REVIEW_FAIL for iter-5 identified that Part A of
`Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing` did not exercise the code path it
claimed. The iter-5 fixture built the row **without setting `row.bundled`**, so
`bundledPortraitUrl = row.bundled?.portraitUrl ?? ""` evaluated to `""`. With `bundledPortraitUrl=""`
and `row.portraitUrl=URL_A`, step 1 fired (`Cached(URL_A, "")` — URLs differ → returns the injected
sprite), short-circuiting the chain before step 2 or step 3 were reached. The regression
(`LoadRealSprite → LoadSprite`) was therefore completely invisible: the injected sprite was already
returned at step 1, so the assertion `resultA.name == "injected_loader_portrait"` passed regardless
of what step 2 did.

**The fix:** Part A now sets `bundledRow.portraitUrl = URL_A` and passes `bundledRow` as
`row.bundled`. This makes `bundledPortraitUrl = URL_A`, so `Cached(URL_A, URL_A)` — URLs agree →
returns null (step 1 null). With empty `portraitSprite`, step 2 also returns null
(`LoadRealSprite("") → null`). The chain must therefore reach **step 3** (`Cached(URL_A)`) which
returns the injected sprite.

With the regression active (`LoadRealSprite → LoadSprite`), step 2 calls `LoadSprite("", cache,
missing)` which reaches `Placeholder(folder, cache)` and returns a non-null Placeholder sprite.
The chain short-circuits at step 2, step 3 is never reached, and `resultA.name == "Placeholder"` —
the assert FAILS with `Expected: "injected_loader_portrait" But was: "Placeholder"`.

No production code was changed.

---

## Iteration baseline

```
=== iter-6 kickoff baseline ===
HEAD=c84134ce6d522c35f0c877197738b27d8dc2ae0c  (unchanged; no production edits this iteration)
DIRTY (relevant files):
  ?? Assets/Scripts/TournamentsRuntime/Tests/CatalogArtPolicyTests.cs   [new untracked]
  M  Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs                      [production changes from iter-1/2/3]
```

---

## Tripwire run (mandatory per Cesar's ARCHITECT_REVIEW_FAIL)

### Step 1 — Apply the regression

Changed line 227 of `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs`:

```diff
-               ?? LoadRealSprite(PortraitPath, row.portraitSprite)            // step 2 REAL
+               ?? LoadSprite(PortraitPath, row.portraitSprite, cache, missing) // step 2 REGRESSION-TRIPWIRE
```

### Step 2 — Run sweep with regression active

Tool: `tests-run`, assembly `Golfin.TournamentsRuntime.Tests`. Result:

```
Status: Failed
TotalTests: 1875  PassedTests: 1871  FailedTests: 1  SkippedTests: 3
Duration: 00:01:23.96
```

**Failing test:**
```
Golfin.Tournaments.WireupTests.ClubLoaderLadderTests.Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing

  Part A: with URLs agreeing (step 1 → null), empty portrait name (step 2 → null),
  and URL_A cached, the chain must reach STEP 3 and return the injected sprite —
  NOT Placeholder. Fail here means step 2 returned Placeholder (LoadSprite regression)
  and shadowed step 3: every club with a live URL but no bundled art would show a
  Placeholder instead of the downloaded image — the exact defect LoadRealSprite prevents.
  Expected string length 24 but was 11. Strings differ at index 0.
  Expected: "injected_loader_portrait"
  But was:  "Placeholder"
```

Exactly one test failed, and it is the test that is supposed to guard against this regression.

### Step 3 — Revert (byte-identical)

Restored the original line:

```diff
-               ?? LoadSprite(PortraitPath, row.portraitSprite, cache, missing) // step 2 REGRESSION-TRIPWIRE
+               ?? LoadRealSprite(PortraitPath, row.portraitSprite)            // step 2 REAL
```

Confirmed via `grep "step 2" ClubDatabaseCSV.cs`:

```
?? LoadRealSprite(PortraitPath, row.portraitSprite)            // step 2 REAL
?? LoadRealSprite(FullPath, row.portraitFull)                  // step 2 REAL
?? LoadRealSprite(ControlPath, row.controlSprite)              // step 2 REAL
```

All three portrait ladders show `LoadRealSprite`. No tripwire remains.

### Step 4 — Green sweep after revert

```
Status: Passed
TotalTests: 1875  PassedTests: 1872  FailedTests: 0  SkippedTests: 3
Duration: 00:01:23.46
```

---

## Acceptance checklist

### Item 1 — Step 1 of the resolution ladder: `CatalogArtCache.Cached(url, bundledUrl)`
**PASS.** Unchanged from iter-4. New overload returns null when `url == bundledUrl` (URLs
agree → bundled art wins at step 2). Returns null when URL not cached. Returns sprite when
`url != bundledUrl` AND sprite is cached.

### Item 2 — Step 2 uses REAL bundled sprite (no Placeholder fallback)
**PASS.** Unchanged from iter-4. `LoadRealSprite(folder, name)` returns null when the sprite
is absent — does NOT touch the shared cache and does NOT return Placeholder. Confirmed by
tripwire: regretting to `LoadSprite` caused Part A to fail.

### Item 3 — Bundled URLs threaded through to the loaders
**PASS.** Unchanged from iter-4. All four CSV loaders pass bundled URL values to their parse
methods; appended rows receive `""` for bundled URL.

### Item 4 — EditMode tests: SPEC §7 evidence (helper-level)
**PASS — helper level.** The 7 tests in `CatalogArtResolutionLadderTests` (iter-4) prove
the helper functions (`CatalogArtCache.Cached`) in isolation. These tests remain correct and
are not removed. Their scope is explicitly helper-level; the loader-level tests (Item 4L)
complement them.

| Test | What level | Which ladder step |
|------|-----------|-------------------|
| `Step1_Returns_Null_When_Overlay_URL_Equals_Bundled_URL` | helper | Step 1 null when URLs agree |
| `Step1_Returns_Sprite_When_Overlay_URL_Differs_From_Bundled_URL` | helper | Step 1 returns sprite when URLs differ |
| `Step1_Returns_Null_When_Changed_URL_Not_Yet_Cached` | helper | Step 1 null when URL not in cache |
| `Step3_Returns_Sprite_When_URL_Is_Cached` | helper | Step 3 returns cached sprite |
| `Step3_Returns_Sprite_So_Step4_Placeholder_Is_Never_Reached_For_Cached_URL` | helper | Step 4 unreachable when step 3 fires |
| `Step1_Returns_Null_For_Empty_URL` | helper | Step 1 edge: empty url |
| `Step3_Returns_Null_For_Empty_URL` | helper | Step 3 edge: empty url |

### Item 4L — EditMode tests: SPEC §7 evidence (loader-level)
**PASS — loader level.** Two loader-level tests in `ClubLoaderLadderTests` drive
`ClubDatabaseCSV.ToRuntime` via reflection. The tripwire run above proves both tests are live
regression guards.

| Test | What level | Which ladder step(s) exercised | SPEC bullet |
|------|-----------|-------------------------------|-------------|
| `Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing` Part A | loader | **Step 3** (step 1 null because URLs agree; step 2 null because name empty; step 3 returns cached sprite) | Placeholder never shadows live URL |
| `Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing` Part B | loader | **Step 2** (no URL cached; real sprite name resolves at step 2) | Bundled sprite wins when step 2 resolves |
| `Loader_BundledSprite_Wins_When_OverlayURL_Equals_BundledURL` | loader | **Step 1 null + Step 2** (URLs agree → step 1 null; real sprite name resolves at step 2) | Bundled wins when URLs agree |

**Why the iter-5 Part A did NOT reach step 3 (the diagnosis):**

In iter-5, `rowNoName = MakeRow(portraitSprite: "", portraitUrl: URL_A)` had no `bundled` set,
so `bundledPortraitUrl = row.bundled?.portraitUrl ?? "" = ""`. Then step 1 called
`Cached(URL_A, "")` — since `URL_A != ""`, step 1 returned the injected sprite immediately.
Steps 2 and 3 were never reached. The regression (`LoadSprite` at step 2 returning Placeholder)
was invisible: the injected sprite was already returned at step 1 before step 2 could do anything.

In iter-6, `bundledRowA = MakeRow(portraitSprite: "", portraitUrl: URL_A)` is passed as
`row.bundled`, making `bundledPortraitUrl = URL_A`. Then step 1 calls `Cached(URL_A, URL_A)` —
URLs agree → returns null. Step 2 calls `LoadRealSprite("", …)` → null (empty name). The chain
reaches step 3 (`Cached(URL_A)`) which returns the injected sprite. The regression now causes
step 2 to return Placeholder, which is caught by the assertion.

### Item 5 — Full EditMode sweep passes
**PASS.** Final green sweep (tool output — `tests-run`, assembly
`Golfin.TournamentsRuntime.Tests`):
```
{"Status":"Passed","TotalTests":1875,"PassedTests":1872,"FailedTests":0,"SkippedTests":3,"Duration":"00:01:23.4621720"}
```
Count unchanged from iter-5 (no new tests added this iteration — only the fixture for the
existing Part A was corrected). The 3 skipped are pre-existing `HoleCompleteDriverTests` Stage
C1 skips.

### Item 6 — Python tests unchanged
**PASS.** No Python changes this iteration. Count unchanged: 26 passed / 0 failed.

### Item 7 — No CSV test data remaining
**PASS.** Unchanged from iter-4. No test rows, no non-empty URL values.

### Item 8 — No Physics changes
**PASS.** `git diff HEAD -- Assets/Scripts/Physics/` produces zero output — confirmed at start
of this iteration (no physics files touched).

### Item 9 — Canonical screenshot via real entry path
**PASS.** Iter-4's screenshot remains canonical — no client behaviour changed this iteration.
Screenshot is `screenshots/2026-08-27_20-57-49.jpg`.

Canonical screenshot: `screenshots/2026-08-27_20-57-49.jpg`

---

## SPEC §7 — three evidence bullets (corrected step attribution)

1. **Bundled wins when URLs agree:**
   - Helper (`Step1_Returns_Null_When_Overlay_URL_Equals_Bundled_URL`): proves `CatalogArtCache.Cached(url, url)` returns null at **step 1**.
   - Loader (`Loader_BundledSprite_Wins_When_OverlayURL_Equals_BundledURL`): drives `ToRuntime` with `row.bundled.portraitUrl == row.portraitUrl == URL_A`. Step 1 returns null (URLs agree). Step 2 resolves the real bundled sprite (`"Driver-G&F"`). Asserts result is the real bundled sprite, NOT the injected URL sprite. Exercises **step 1 null + step 2 wins**.

2. **A changed URL beats bundled:**
   - Helper (`Step1_Returns_Sprite_When_Overlay_URL_Differs_From_Bundled_URL`): proves `CatalogArtCache.Cached(URL_A, URL_B)` returns the injected sprite at **step 1** (URLs differ and URL cached).
   - Loader (Part B of `Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing`): drives `ToRuntime` with a real bundled sprite name, no URL in cache. Step 2 wins (real sprite resolves). Exercises **step 2 wins when step 1 returns null** — the complementary case. The "changed URL beats bundled" case where step 1 returns a sprite and step 2 is skipped is proven by the helper test; no loader-level fixture for that sub-path exists (it would require a non-null return from step 1, which requires the two URLs to differ AND the URL to be in cache — already proven by the helper at the `Cached` level).

3. **Placeholder never shadows a live URL for clubs:**
   - Helper (`Step3_Returns_Sprite_So_Step4_Placeholder_Is_Never_Reached_For_Cached_URL`): proves `CatalogArtCache.Cached(url)` (step 3) returns the sprite so step 4 is unreachable, at the **helper level**.
   - Loader (Part A of `Loader_URL_Wins_Over_Placeholder_When_BundledNameMissing`): drives `ToRuntime` with `bundledPortraitUrl == URL_A` (step 1 null), empty `portraitSprite` (step 2 null), and URL_A cached. Asserts `result.name == "injected_loader_portrait"`, NOT Placeholder. Exercises **step 3** at the loader level. Tripwire confirmed: with `LoadSprite` at step 2, Placeholder short-circuits before step 3 and the test fails with `Expected: "injected_loader_portrait" But was: "Placeholder"`.

---

## Files modified or created

| File | Change |
|------|--------|
| `Assets/Scripts/TournamentsRuntime/Tests/CatalogArtPolicyTests.cs` | Part A fixture corrected: `bundledRowA` now has `portraitUrl=URL_A` and is passed as `row.bundled`, so the chain reaches step 3 (not step 1). Docstring updated with full diagnosis. |

No production code changed. The test count is unchanged at 1875 (same fixture, corrected setup).

Pre-existing files from iter-1/2/3/4 (unchanged this iteration):
`Assets/Scripts/CatalogArt/CatalogArt.cs`, `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs`,
`Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs`,
`Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs`, `Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs`,
`Assets/Data/Characters.csv`, `Assets/Data/Balls.csv`, `Assets/Data/Items.csv`,
`Tools/admin-dashboard/lib/banner.ts`, `Tools/admin-dashboard/lib/contentView.ts`,
`Tools/admin-dashboard/app/(panels)/_content/row-editor.tsx`.

---

## Unity authoring traps self-certification (Rule 12)

This task involves no scene/prefab edits, no UI widgets, no new Buttons. Traps C1–C8 are N/A.

## Clone provenance (Rule 19)

No reuse mandate in this spec. N/A.

## Figma fidelity (Rule 18)

No Figma node referenced in this spec. N/A.

## UI fidelity lint (Rule 21)

No UI prefab changed. N/A.
