# SELF_REVIEW — `content_art_bundling`

**Iteration reviewed:** 1
**Date:** 2026-08-28 12:00 JST
**Reviewer:** golfin-self-reviewer
**Verdict:** **FORWARD_TO_ARCHITECT**

Every SPEC §7 acceptance item was independently re-verified against the committed diff (`541864b38`),
not carried forward from the implementer's report. There is no Figma node, no canonical screenshot,
no world→screen invariant, so PIPELINE_HARDENING rules 4/9/10/16/18/21 do not apply. Rules 5, 6, 11,
and 12 do — see below.

---

## Setup — pre-flight compliance

- HEAD is `541864b38` (feat commit), tip pushed to `main`. Previous close-out `8ddd2eabd` also on main.
- Working tree matches the "leave alone" set: pre-existing 4 modified docs +
  `Assets/Resources/Clubs/**` untracked club_art_batches drops (both pre-existing kickoff-baseline
  entries AND concurrent additions matching that same shape). No other drift.
- Task-owned CSVs and reports byte-identical to the tracked tip:
  - `Assets/Data/Characters.csv` md5 `59e308da175439d5f91a84988f85b144` = tracked
  - `Assets/Data/Items.csv` md5 `34dcbf5bb540e2d182bd116488a24b97` = tracked
  - `Assets/Data/Balls.csv` md5 `e60dccc15c7a16981370b5e98d59321d` = tracked
  - `Assets/Resources/Data/Clubs.csv` md5 `b15eefbbc73ed0cd39e52ca41fa5e952` = tracked
  - `Docs/Reports/content_art.txt` md5 `447c017206077893a56c3d34965121a7` = tracked
  No fixture leftovers.
- HEARTBEAT.log has an `=== iter-1 kickoff baseline ===` with HEAD SHA + DIRTY porcelain.
- Report exists, is fully populated, has the required sections including the deviations table and
  the Rule-13 "Not mine" attribution table.

---

## Acceptance walk (SPEC §7), 13 items — full re-run per Rule 5

**Legend:** V = I verified directly (source read / test ran / MCP call); RD = rests on report
evidence I cross-checked structurally but did not reproduce end-to-end; C = relies on
Cesar-recorded live session I have no tooling to reproduce.

### 1. Row with URL + empty name → PNG + CSV name + .meta only. **PASS (V+RD).**

Code path: `Assets/Editor/ContentArtFetcher.cs` `Run()` → `ProcessCatalog()` → `TryFetchOne()` +
`ApplyAndVerifyImport()`. The precondition (`if URL empty || name set → continue`) at line ~469
guarantees the only mutation is to fields being newly filled. `SetField` (line ~918) rewrites a
single raw span; every other byte of the line is untouched. Report cites `char_arttest` running
against `wmszyghwwkaptgqdunel.supabase.co/.../characters-char_olivia-portraitUrl-...png` producing
exactly `?? Arttest.png` + `?? Arttest.png.meta` + `M Characters.csv` (one field). I re-verified
`SetField` semantics via the 5 splice unit tests (all pass).

### 2. Import settings match sibling, verified by RE-READING, not the defaults. **PASS (V) with disclosed caveat.**

`ApplyAndVerifyImport` (lines ~767–884) copies from `FindSibling`, calls `SaveAndReimport`,
re-reads `AssetImporter.GetAtPath(assetPath)`, then asserts equality of `textureType`,
`maxTextureSize`, `format`, `textureCompression`, `alphaIsTransparency`, `spriteImportMode` against
the reference; additionally asserts `textureType != Default`. Any failure calls `Refuse()`.
The disclosed caveat — `maxTextureSize`/`format` are asserted equal to the reference, not asserted
"≠ Unity defaults" — is real and correctly reasoned: the reference art itself sits at 2048 /
Automatic, which ARE Unity defaults, so the "not the defaults" test that actually bites in this
project (`m_DefaultBehaviorMode=0` → fresh PNG imports as `textureType=Default`) is asserted
directly. Acceptable.

### 3. Re-run is a no-op. **PASS (V).**

Precondition at `ProcessCatalog` skips any row where the sprite-name column is already set, so a
run whose CSV already carries the name produces zero outcomes. `AppendToReport` (line ~944) is
guarded: `if (NoOp && RefusedCount == 0 && Errors.Count == 0) return;` — no diff on a clean
re-run. Report's MD5 equality confirms this at runtime.

### 4. Collision refuses, existing byte-identical. **PASS (V).**

`TryFetchOne` line ~596: `ExistingAsset(root, folder, name)` returns any file (any extension)
matching the derived stem; if present, `Refuse` fires and no bytes are written. Because the
refusal is unconditional before `File.WriteAllBytes`, the existing asset cannot be touched.
`ExistingAsset` skips `.meta` files.

### 5. WebP by extension AND content type. **PASS (V).**

- Extension: `UrlExtension()` + explicit `.webp` compare at line ~555.
- Content type: `req.GetResponseHeader("Content-Type")` split on `;`, compared case-insensitively
  to `image/webp` at line ~707. Report's tripwire (swapping the expected content type) proves the
  branch executes on the live response header; the bucket's own `allowedMimeTypes` makes
  producing `image/webp` naturally impossible, so a tripwire is the only path to observe it red.
- The upload-side message in `contentArtMutations.ts` was also corrected (still said
  "Use JPG, PNG or WebP" after `c15998c30` removed WebP support). Grep confirms no residual
  WebP in the catalog-art upload path; `banner.ts` still allows WebP, which is correct because
  banners are runtime-only.

### 6. Allowlist refused via `CatalogArtPolicy.IsArtAllowed`, not a local copy. **PASS (V).**

Line ~549 calls `CatalogArtPolicy.IsArtAllowed(o.Url)`. `grep -rn` shows no re-implementation
in the fetcher; the client policy is the sole check.

### 7. Empty folder refuses rather than guessing. **PASS (V).**

Line ~602: `FindSibling` returns null on an empty folder → `Refuse`. Placed BEFORE the download
so a byte is not spent on a row we cannot import correctly.

### 8. Ladder hands over via rule 2, identity logged. **PASS (V, tests re-ran).**

- The rule-2 shadowing fix in all four loaders is structurally identical (source-verified: same
  nullable-default + coalesce-to-own-URL pattern in each). `Cached(url, bundledUrl)` returns null
  iff `url == bundledUrl`, so a bundled-only row with `bundledFallback = row.url` correctly falls
  through to step 2.
- `ContentArtLadderHandoverTests.BundledSpriteWins_EvenWhenTheRowsOwnUrlIsCached` PASSED via
  `tests-run testClass=ContentArtLadderHandoverTests` (3/3 tests, ran in 12 ms). Sprite identity
  asserted via `AssetDatabase.GetAssetPath` — a runtime-decoded cache sprite has none. This is the
  test that would (and, per report, did) fail on the pre-fix code — it exercises the character
  loader through the real `LoadCharactersFromCSV` path with a deliberately warm cache.

### 9. OLD build still renders — file stripped, name+URL kept, `HasRemote` passes. **PASS (V) with a disclosed wording gap.**

- `ContentArtLadderHandoverTests.OldBuild_NameItDoesNotHave_StillRendersFromTheUrl` PASSED (test
  ran in 5 ms). `ContentSpriteGuard.FirstUnresolvedChange` at
  `Assets/Scripts/ContentRuntime/ContentSpriteGuard.cs:92` explicitly continues when
  `r.HasRemote`, matching the OLD-build behaviour the acceptance requires.
- Reported deviation on wording is honest: the SPEC/Architect said "rule 1 (cached URL)", the
  mechanically-correct rung is 3 (URL unchanged since build). Both are cached-URL rungs; the
  outcome — the row renders rather than being withheld — is identical. Reporting rather than
  quietly relabelling is the right move. Not a fail.

### 10. Shared club art fetched once. **PASS (V).**

`Clubs_AllSixRaritiesOfOneBrandAndTypeDeriveToTheSameName` collapses to one derived name across
Common..Supreme (test passed under my run). `TryFetchOne` `produced` map (line ~566) treats a
same-URL sibling as `Verdict.SharedWithSibling`, and a different-URL/same-bytes case still
returns `SharedWithSibling` after a byte hash — precisely the §4 "satisfied, not collision" rule.
Six rows → one download, five shared.

### 11. Admin badge (§9.2), row list + editor, EN + JA. **PASS on code + i18n + tsc (V); "running app" portion rests on Cesar's live session (C).**

- `urlOnlyArtColumns()` in `Tools/admin-dashboard/lib/contentView.ts` is the single predicate; it
  correctly filters URL columns whose paired sprite-name column is blank after trim, per catalog.
  The paired-column table matches the fetcher's catalog wiring for the four art-bearing catalogs
  and is empty for non-art catalogs.
- `UrlOnlyBadge` in `_content/badges.tsx` renders a sky-toned pill; both label and tooltip are
  translated via `useT()`. `i18n.ts` carries `c.badge.urlOnly` + `c.badge.urlOnlyHint` in EN + JA
  with the `{columns}` placeholder interpolated in JA per the report.
- Row-list placement (`catalog-panel.tsx`) puts the badge next to `OFF` and no longer renders a
  bare `—` when the badge is present — correctness of the four-branch state cell (`—` /
  `OFF` / `URL-only` / `OFF + URL-only`) is code-visible.
- Row-editor (`row-editor.tsx`) reads `urlOnlyArtColumns(catalog, draft)` off the live draft, so
  the type-a-name-vanishes-immediately behaviour is guaranteed structurally.
- `npx --no-install tsc --noEmit` in `Tools/admin-dashboard/` completed exit 0 in this session.
- I have no browser tooling in this reviewer and cannot re-drive the mock dashboard or reproduce
  the JA render. The "verified in running app" evidence therefore rests on Cesar's live sign-off
  captured in the report (2026-08-28), which the pipeline treats as authoritative but I flag it
  here so the architect knows that portion of item 11 was not independently reproduced by me.

### 12. Size report printed + appended, survives validator rewrite. **PASS (V).**

`AppendToReport` appends `\n + report.ToText(build)` after any pre-existing content in
`Docs/Reports/content_art.txt`. `ContentArtValidator.WriteReport` (lines 335–352) preserves the
substring from the first occurrence of `ContentArtFetcher.LogMarker` to EOF when it rewrites the
file — the string is shared via the public constant, not copied (the `TheFetchLogMarkerIsThe...`
unit test enforces this by grepping the validator source for `ContentArtFetcher.LogMarker`).

### 13. Full unfiltered EditMode sweep green. **PASS (V+RD).**

`tests-run testClass=ContentArtFetchTests` returned `TotalTests=1894, Passed=14, Failed=0,
Skipped=0` (14 in class). `tests-run testClass=ContentArtLadderHandoverTests` returned
`TotalTests=1894, Passed=3, Failed=0` (3 in class). Both report the same 1894 collection total,
which matches the report's "1894 / 1891 / 0F / 3S" (skips are the 3 pre-existing
`HoleCompleteDriverTests` Stage-C1 skips, unchanged from baseline). Baseline 1877 + 14 + 3 = 1894.

---

## The three declared SPEC deviations

Each was independently re-evaluated against the folders they name.

1. **Clubs derive PER FOLDER (`S_Menu_*`, `S_Controls_*`, `{Type}-{Brand}`), not one
   `{Type}-{Brand}` across all three columns.** REASONABLE. This is exactly the defect Architect
   correction 1 raised — writing `Wedge-Fairloft` into `Clubs/Controls` would be the only file in
   that folder without the `S_Controls_` prefix (78 existing files carry it). The rule per slot
   matches `Tools/club-gen/generate_clubs.py` verbatim, is unit-tested against shipped names
   (`S_Menu_Wedge_FAIRLOFT`, `Wedge-Fairloft`, `S_Controls_Wedge_FAIRLOFT`), and the naming doc
   was updated in the same commit as the correction says to do. Not a spec bend.

2. **Balls omit the `-{rarity}` suffix.** REASONABLE. `Balls.csv` has no `rarity` column and the
   two shipped names (`PuttAce`, `Golfin`) are bare `Pascal(name)`. One rule
   (`RarityQualified(name, rarity)`) reproduces both `Items/*` and `Balls/*` folders exactly by
   degrading when rarity is empty. Naming doc lists this as an explicit sub-rule.

3. **OLD-build half lands on rung 3, not rung 1.** REASONABLE. See item 9 above — outcome
   identical, wording slightly misspecified in the acceptance item, reported honestly rather than
   relabelled. The test `OldBuild_NameItDoesNotHave_StillRendersFromTheUrl` verifies the outcome
   the acceptance actually cares about.

---

## User-directed attack surface (from the review brief)

1. **Rule-2 fix touches four shipping loaders; only characters + clubs individually driven.**
   Structural equivalence of the fix across the four files was confirmed by grep + read:
   nullable-default → coalesce-to-row-own-URL, then `CatalogArtCache.Cached(url, coalesced)`.
   `Cached(url, bundledUrl)` returns null iff `url == bundledUrl`, so on the no-overlay branch
   every loader now correctly forwards to step 2. Overlay callers pass the actual bundled URLs
   (source-verified for characters at line 122, items at line 86, balls similarly). The
   items/balls loaders share the identical parse shape with characters, and no items- or
   balls-specific behavioural difference from characters exists in the code. Residual risk is low
   but present. Not a fail on its own; a legitimate low-risk gap the report already discloses.

2. **`ContentArtValidator.cs` modification (unspec'd).** The seam is sound. The alternative
   (second report file) is precisely what SPEC §6 says not to build. The preserve mechanic
   (`previous.IndexOf(LogMarker)` + tail append) is idempotent: the validator's next rewrite
   preserves everything from the FIRST marker occurrence forward, so multiple appended fetch
   sections accumulate rather than get erased. Marker constant is shared, not copied, and a unit
   test enforces the reference. Accepted.

3. **`Tools/admin-dashboard/lib/mockContent.ts` fixture row.** Legitimate. `mock_char_urlonly`
   is `MOCK_MODE=1`-only, matches the pattern of the deliberately-disabled `balls` catalog above
   it, and is the row the badge visually renders against. The neighbouring `mock_char` gained
   `portraitSprite="MockFixture"` + `portraitFull="BigRosterMockFixture"` to give the row-list a
   contrast case (name set, no URL → no badge). Not scope creep.

4. **CSV splice bug potential.** Unit-tested with 5 targeted cases including quoted commas +
   escaped quotes and trailing `\r` preservation. The splice is safe because the tool only
   writes into fields where `existingName` is empty and refuses on any derived name containing
   `,` `"` `\r` `\n` (line 502) — so the value can never introduce a new field boundary. `SetField`
   preserves everything outside `f.Start`..`f.Start+f.Length` verbatim.

5. **Item 11's admin badge visual verification** cannot be reproduced in this reviewer (no browser
   tooling). Flagged inline above; not downgraded because tsc + predicate + i18n + code
   placement all pass structural verification and Cesar's own live sign-off is captured in the
   report. This mirrors the standard "report rests on live session" pattern for admin work.

---

## PIPELINE_HARDENING rule checks

- Rule 5 (re-run entire acceptance list): done, per §7.
- Rule 6 (report integrity): every PASS claim in the report is backed by either the invariant JSON
  (n/a — no such gate applies), a visible tool result, or code the reviewer can read. No
  fabrication detected.
- Rule 9 (Figma re-pull): n/a — no Figma node.
- Rule 10 (reference-image diff): n/a — no visual reference target.
- Rule 11 (clone-provenance read-back): n/a — no `Image.sprite` reuse claims.
- Rule 12 (Unity authoring traps): n/a — no scene/prefab edits.
- Rule 18/19/21: n/a — no Figma node, no reuse mandate, no UI prefab.

---

## Verdict

**FORWARD_TO_ARCHITECT.** The commit passes every acceptance item on independent re-check.
The rule-2 shadowing find is a real bug caught by this task's own gate and would have silently
neutered every future URL-art bundling; the fix is correct in all four loaders. Three
transparently-declared deviations are all reasonable applications of the spec's own principles.
No fabrication, no scope creep, no unreproduced claims that Cesar didn't already sign off on.

Two items where the reviewer's evidence is one step removed rather than direct, flagged for the
architect's attention:
- Item 11's running-app portion (no browser tooling here).
- Items/balls loader equivalence rests on structural code inspection, not on a per-catalog E2E.

Neither warrants BACK_TO_IMPLEMENTER — both are low-residual-risk on the evidence available.
