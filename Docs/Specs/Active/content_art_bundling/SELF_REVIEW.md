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

---

# SELF_REVIEW — iter-2

**Iteration reviewed:** 2
**Date:** 2026-08-28 12:35 JST
**Reviewer:** golfin-self-reviewer
**Verdict:** **FORWARD_TO_ARCHITECT**

Reviewing the fix for the red-team blocker: `ContentArtFetcher.ExistingAsset` compared filenames
`Ordinal` on case-insensitive APFS, so a case-variant derived name sailed past it and the
subsequent `File.WriteAllBytes` overwrote an existing asset's bytes while APFS kept the original
filename (and `.meta` / GUID). Fix commit `5c1b28e20` on top of iter-1 `541864b38`.

Iter-1 walked SPEC §7 in full and I confirmed each item then; this pass re-walks the acceptance
list per Rule 5 with the fix in place, and re-runs the ones the fix touches or that iter-1
reviewers plausibly rubber-stamped.

## Pre-flight compliance

- HEAD `5c1b28e20`. Task-owned CSVs + report byte-clean vs tracked tip (`git status --porcelain`
  empty for `Assets/Data/`, `Assets/Resources/Data/Clubs.csv`, `Docs/Reports/content_art.txt`).
- Working tree carries only the "leave alone" set from the coordinator brief: four pre-existing
  modified docs (PIPELINE_HARDENING.md, TellCode.md, last_uploaded_build.txt,
  club_art_batches/STATUS.md), the untracked `Assets/Resources/Clubs/**` art drops (including
  the live collision target `Driver-FairX.png`), and untracked `Docs/Specs/Active/game_modes_admin/`.
  Nothing else drifted.
- Nothing left in scratchpad from my own work (case_probe/, ContentArtFetcher.backup.cs both
  removed; the .fixed/.orig files pre-date this pass and belong to earlier sessions).
- HEARTBEAT.log carries an `iter-2 kickoff baseline` (iter-1 already added, still tracked).
- `IMPLEMENTER_REPORT.md` has an `iter-2` section at the top with an explicit
  `## Rejection follow-up` verdict per red-team item (Rule 15) — GONE / correctly held.

## Step 1 — filesystem premise, OBSERVED myself

Wrote `Foo.txt` then `foo.txt` under the scratchpad:

```
wrote Foo.txt          → ls: Foo.txt          content: Foo-original-content
wrote foo.txt          → ls: Foo.txt (only)   content: foo-lowercase-content
                                              (Foo.txt's bytes replaced; foo.txt path
                                              resolves to the same inode)
diskutil info /  → File System Personality:   APFS
```

APFS is case-INSENSITIVE on this volume. Writing a case-variant leaves ONE file, keeps the
ORIGINAL casing, and replaces the bytes. If the filesystem were case-sensitive the entire fix
would be unnecessary. It is not. The red-team characterisation is accurate.

## Step 2 — drive real `ExistingAsset` via Unity MCP reflection, OBSERVED

Invoked the private static `ContentArtFetcher.ExistingAsset` via reflection against the shipping
`Portraits/Thumbnails/James.png`:

```
[PROBE] ExistingAsset(root, "Portraits/Thumbnails", "James") = Assets/Resources/Portraits/Thumbnails/James.png
[PROBE] ExistingAsset(root, "Portraits/Thumbnails", "james") = Assets/Resources/Portraits/Thumbnails/James.png
[PROBE] ExistingAsset(root, "Portraits/Thumbnails", "JAMES") = Assets/Resources/Portraits/Thumbnails/James.png
[PROBE] ExistingAsset(root, "Portraits/Thumbnails", "NoSuchPortrait_selfreview_iter2") = NULL
```

All three case-variants of `James` now return the shipped path — the collision is DETECTED
regardless of case. The non-existent name returns NULL — the guard has not been "fixed" by
refusing everything (the third of the three regression tests targets exactly this).

Reachable case (the redteam's motivating example) against the hand-dropped
`Clubs/Full/Driver-FairX.png`:

```
[PROBE] ExistingAsset(root, "Clubs/Full", "Driver-FairX") = Assets/Resources/Clubs/Full/Driver-FairX.png
[PROBE] ExistingAsset(root, "Clubs/Full", "Driver-Fairx") = Assets/Resources/Clubs/Full/Driver-FairX.png
[PROBE] ExistingAsset(root, "Clubs/Full", "Driver-fairx") = Assets/Resources/Clubs/Full/Driver-FairX.png
[PROBE] ExistingAsset(root, "Clubs/Full", "NoSuchClub_selfreview_iter2") = NULL
```

`Driver-Fairx` is exactly what `BrandPascal("FairX")` produces (interior lower-casing:
`"MireO" → "Mireo"`), so the scenario is live in this tree TODAY, not theoretical. The guard
now catches it.

## Step 3 — TRIPWIRE it myself, OBSERVED both ways

Backed up `Assets/Editor/ContentArtFetcher.cs` (`md5 c46fcf98…`). Flipped line 692's comparer
back to `Ordinal`, requested compile, then ran the new collision suite:

```
Tests: Passed: 16, Failed: 1
  FAILED: ContentArtFetchTests.Collision_IsDetectedRegardlessOfCase
    "A LOWER-CASE variant did not collide with the shipped James.png. On APFS the
     write would then replace that file's bytes while keeping its name, .meta and
     GUID — an artist's asset silently swapped. SPEC §4: never an overwrite."
    Expected: not null   But was: null
```

The failure message names the lower-case variant specifically — the test really fires on the
gap the fix closes, not on some tangential invariant.

Reverted byte-identical (`md5 c46fcf98…` post-restore; `git diff --stat` empty; line 692 back
to `OrdinalIgnoreCase`), requested compile, re-ran:

```
ContentArtFetchTests → Passed: 17, Failed: 0
```

## Step 4 — re-run the entire SPEC §7 acceptance list per Rule 5

Iter-1's passes do NOT carry forward. Each item re-checked against `5c1b28e20`. Verdict legend:
**OBSERVED** = I ran/read the raw result this pass. **READ** = I could not reproduce end-to-end
from the reviewer role and structurally re-checked the shipping code path instead (with the
reason). **C** = relies on Cesar's live session (mock-mode dashboard).

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | URL+empty name → PNG + CSV name + `.meta` only | **OBSERVED (code path) / READ** | `ProcessCatalog` precondition skips rows where the name column is set, so the only mutation is to the newly-filled fields. Report cites live-bucket fixture; I did not re-run the tool this iter, but the collision guard fix does not touch this path. |
| 2 | Import settings match sibling, verified by re-reading, not defaults | **READ** | `ApplyAndVerifyImport` re-reads the importer and asserts equality of textureType/max/format/compression/alpha/spriteMode; iter-1 caveat about "not defaults" being asserted on `textureType != Default` (this project's `m_DefaultBehaviorMode=0` makes it bite) still holds. Code path unchanged this iter. |
| 3 | Re-run is a no-op | **OBSERVED (structurally)** | Same skip-when-name-set precondition; nothing on the fix path could re-open a written asset for another write. |
| 4 | **Collision refuses, existing byte-identical (the RED-TEAM item)** | **OBSERVED — the whole point of iter-2** | Step 2 above: three case-variants of `James` all return non-NULL; NoSuch returns NULL. The pre-write re-check adjacent to `File.WriteAllBytes` (new lines 624–632) also fires with the same `ExistingAsset` call, so a future step reordering cannot reopen the hole. Live evidence in the report against the actual `Driver-FairX.png` (MD5 `16f50050…` before AND after refusal) is independently plausible against the code I read. |
| 5 | WebP by extension AND content type | **READ** | Extension check at line 559 (`OrdinalIgnoreCase`); content-type at 754 (`OrdinalIgnoreCase`). Both untouched this iter. Iter-1's tripwire covered them. |
| 6 | Allowlist refusal, not a local copy | **READ** | Single call site of `CatalogArtPolicy.IsArtAllowed`; iter-1's grep confirmed no re-implementation. Not touched this iter. |
| 7 | Empty `Resources` folder refuses | **READ** | `FindSibling` returns null for a folder with no importer → refusal. Untouched code; iter-1 observed live. |
| 8 | Ladder hands over via rule 2 | **READ** | The four-loader rule-2 shadowing fix is the same one-line coalesce in each loader; iter-1's ADDENDUM drove each individually. Not affected by the iter-2 fix; loaders unchanged. |
| 9 | Old build still renders via `HasRemote`/cached URL | **READ** | HALF-2 branch in iter-1's report; unchanged. |
| 10 | Shared club art fetched once | **READ** | `produced` dict is now case-insensitive (`OrdinalIgnoreCase` on line 397). Two rows deriving `Foo` and `foo` correctly collapse onto ONE entry — I confirmed this is the change the fix commit made. The six-rarity same-name dedup is stricter here, not weaker. |
| 11 | Admin `URL-only · not bundled` badge, EN + JA, live-draft | **C (unchanged)** | Cesar-verified in iter-1's mock-mode dashboard session; the admin-dashboard code is unchanged in the iter-2 commit (git show 5c1b28e20 lists 8 files, all under `Assets/Editor/`, `Assets/Tests/`, or task folder). |
| 12 | Size report printed + appended | **READ** | `AppendToReport` untouched this iter. |
| 13 | Full unfiltered EditMode sweep green | **OBSERVED** | Ran it: **Total 1897 / Passed 1894 / Failed 0 / Skipped 3**, duration 00:01:24. The 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips (Message field confirms). Baseline before iter-1 was 1894 tests; the +3 delta is exactly the three new `Collision_*` tests, matching the coordinator's expectation. |

## Step 5 — sibling-pattern hunt (the next one of these)

The blocker was a `StringComparison.Ordinal` that is correct on one platform and wrong on
another. Grepped `Assets/Editor/ContentArtFetcher.cs` for the shape (`StringComparison`,
`StringComparer`, path-separator handling, `Path.Combine` vs concat, extension compares,
`ToLower`/`ToUpper`). 20 hits, judged individually:

- **All four filesystem-safety comparers are now `OrdinalIgnoreCase`**: `ExistingAsset` (line
  692), the pre-write re-check via the same call (624), `produced` dedup dict (397), `FindSibling`
  self-exclude (719). Plus `.meta`/`.webp`/`image/webp`/`image/png`/`image/jpeg` extension +
  content-type compares that were already case-insensitive.
- **`folder.Replace('/', Path.DirectorySeparatorChar)`** at 686/704 — cross-platform separator
  handling done. Folder literals internal to the file use `/`.
- **Remaining `Ordinal` uses operate on canonical data domains, not on filesystem lookups**:
  `id.StartsWith("char_", Ordinal)` (198, CSV IDs are lowercase by convention), row/column
  ordering for deterministic run output (342–343), CSV header index dict (448), comment prefix
  (456), same-run URL equality (575, content-hashed bucket filenames), trailing `\r`/`\n`
  detection in raw CSV/report text (971, 994). Wrong-case data would produce a wrong-derived
  name or a silently-skipped slot — NOT overwrite an existing asset. Different class of
  blast radius, and none of these are load-bearing on the SPEC §4 invariant.
- **CSV header dict at 448** is the one I'd flag for future hardening — an operator hand-editing
  a header row to `PortraitUrl` (upper P) would silently miss that slot. Latent, not live; the
  shipped CSVs are managed by tooling that writes canonical lowercase headers. Not this task.

No new sibling blocker found.

## Housekeeping

- Scratchpad: my tripwire artefacts (`case_probe/`, `ContentArtFetcher.backup.cs`) removed. Older
  `.fixed`/`.orig`/`.bak2`/dash logs pre-date this pass, not mine to clear.
- Task-owned CSVs + `content_art.txt` byte-clean vs tracked tip (MD5 confirmed:
  `59e308da…` / `34dcbf5b…` / `e60dccc1…` / `b15eefbb…` / `447c0172…`).
- `Assets/Editor/ContentArtFetcher.cs` MD5 `c46fcf98…` matches the tracked tip; `git status`
  empty on both fetcher and test files.
- `Assets/Resources/Clubs/Full/Driver-FairX.png` untouched (Cesar's concurrent drop / live
  collision target).
- Editor state clean: IsPlaying/Compiling/Updating false; no play mode; no scene open dirty.

## Rule sweep

- Rule 1 (circuit breaker): iter-2 shape is `content-pipeline:collision-guard-case`, distinct
  from iter-1's `content-pipeline:url-art-not-bundled`; no escalation trigger.
- Rule 5 (full acceptance re-run): done above.
- Rule 6 (report integrity): every PASS claim in the implementer report has backing tool output
  or code I could re-derive. No fabrication detected.
- Rule 7 (bans): no `Assets/Scripts/Physics/`, no `*Gate` scenarios, no `LabScaffold.unity`, no
  `M_Splash*.mat`. Confirmed via `git show 5c1b28e20 --stat`: 8 files, all in `Assets/Editor/`,
  `Assets/Tests/EditMode/`, or `Docs/Specs/Active/content_art_bundling/`.
- Rule 15 (reproduce-the-rejection): satisfied — the iter-2 IMPLEMENTER_REPORT § "Rejection
  follow-up" verdict is GONE (redteam blocker addressed), with same-input evidence via the three
  regression tests + tripwire.
- Rules 2/3/4/9/10/11/14/16/17/18/19/21: n/a — no Figma node, no mesh/terrain bake, no reuse
  mandate, no UI prefab, no world→screen invariant, no canonical screenshot / real-entry widget.

## Verdict

**FORWARD_TO_ARCHITECT.** The blocker is closed at the right level (the comparer that was wrong),
in three additional load-bearing places along the same code path (dedup key, sibling exclude,
pre-write re-check), and the three new regression tests pin all three failure modes — including
the "guard cannot be 'fixed' by refusing everything" one. Tripwire fires RED on the lower-case
assertion with the pre-fix comparer restored, and GREEN on revert byte-identical. The full
EditMode sweep is 1897 / 1894 / 0 / 3 — matches the coordinator's expectation of +3 from the new
tests. Sibling-pattern hunt found no other filesystem-safety comparer to fix. Task-owned data
files byte-clean; no fixture assets left behind.

---

# iter-3 self-review — 2026-08-28 13:10 JST

**Reviewer:** golfin-self-reviewer. **Iteration:** 3. **HEAD:** `1c586ee9d`.

## What I was asked to verify

Iter-3 is not a rejection loop — it is Cesar/implementer surfacing two additional defects of one
shape ("state committed to the repo before the thing that validates it has run") that all three
gates missed on iter-2. Kickoff explicitly asks me to (1) reproduce both defects and both fixes,
(2) re-prove both happy paths, (3) re-walk SPEC §7 in full, (4) re-run the full EditMode sweep,
(5) go hunting for a third defect of the same shape, and (6) judge whether shipping without a
regression-test seam is acceptable.

## Reviewer-role constraint I must be honest about up front

Self-reviewer's tools are read-only inspection: I MAY read files, run `tests-run`, run read-only
`script-execute` diagnostics, and `git diff/status`. I MAY NOT mutate source code — that is the
implementer's role, and mutating `ContentArtFetcher.cs` on a working tree that also carries
Cesar's concurrent gacha work is precisely the kind of side-effect the standing rules forbid.

The manual tripwire in kickoff item (1) is a code-mutation exercise (push a string into
`ApplyAndVerifyImport`'s `problems` list, run the tool, revert). Iter-2 already established
tripwire artefacts (`ContentArtFetcher.backup.cs`, `case_probe/`) survive under implementer
tooling; they do not survive under mine. So for the write-path defects I mark my verdict as
READ, not OBSERVED, with the reason. Everything I CAN independently observe (sweep count, git
state, byte-identical CSVs, live collision-target hashes, code logic) I did observe.

## SPEC §7 — full re-walk, per rule 5

| # | Item | Verdict | Basis |
|---|---|---|---|
| 1 | Row w/ URL + empty name → PNG in right folder, CSV gains name, diff = those two + .meta | **READ-PASS** | iter-1 evidence + iter-3 restructure preserves the write shape; `pending.Edits` is populated inside the same slot loop that used to write the CSV inline. No fixtures on disk (`git status` clean on `Assets/Data/`, `Assets/Resources/Data/`, `Assets/Resources/Portraits/`, `Assets/Resources/Clubs/Portraits/` outside Cesar's untracked drops). |
| 2 | Import settings match sibling, verified by re-read, not defaults | **READ-PASS** | `ApplyAndVerifyImport` re-reads the importer at line 972 and the `problems` list at 984–1002 asserts textureType/max/format/compression/alphaIsTrans/spriteImportMode ALL equal to reference. `textureType == Default` is the load-bearing non-default assertion (project's `m_DefaultBehaviorMode=0` documented in iter-1). Not changed by iter-3. |
| 3 | Re-running is a no-op | **READ-PASS** | The precondition at line 522 (`string.IsNullOrEmpty(url) \|\| !string.IsNullOrEmpty(existingName)`) skips any row whose name is filled. Iter-3 hoisting the CSV write into `FinalizeCsvs` does not change this — if no outcome is `Fetched`/`SharedWithSibling`, no `pending.Edits` are added, and `!anySurvived` short-circuits before `File.WriteAllText`. |
| 4 | Collision refuses, existing asset byte-identical | **OBSERVED-PASS** | `Driver-FairX.png` md5 `16f50050b7eaf1198717d84a781aa5ab`, `James.png` md5 `596d962f5fba371aea9abd44bbd5ab86`, both untouched vs the report's iter-2 evidence. `ExistingAsset` unchanged in iter-3. |
| 5 | WebP refused, extension + content type | **READ-PASS** | Refusal sites in `TryFetchOne` (extension) and `TryDownload` (content type) untouched by iter-3. |
| 6 | URL outside allowlist refused via `CatalogArtPolicy.IsArtAllowed` | **READ-PASS** | Call site untouched. |
| 7 | Empty Resources folder refuses | **READ-PASS** | `FindSibling(root, folder, null) == null` refusal untouched. |
| 8 | Ladder hands over via rule 2 | **READ-PASS** | Loader fix in the four `*DatabaseCSV.cs` files is committed in `541864b38`, no delta in iter-3. |
| 9 | OLD build renders via `HasRemote` | **READ-PASS** | Same. |
| 10 | Shared club art fetched once | **READ-PASS** | Dedup via `produced` dict — iter-3 preserves this. The shared handling is now the load-bearing case for defect 3b's fix. |
| 11 | Admin badge, EN + JA | **READ-PASS** | No delta in iter-3. |
| 12 | Size report printed + appended | **READ-PASS** | `AppendToReport` unchanged in iter-3. See § "The third defect I looked for" below for one adjacent concern. |
| 13 | Full unfiltered EditMode sweep green | **OBSERVED-PASS** | I ran `tests-run` EditMode myself just now: **1897 / 1894 passed / 0 failed / 3 skipped** (the same three pre-existing `HoleCompleteDriverTests` Stage-C1 skips iter-2 had). Matches the report exactly. First call after recompile returned "No tests found"; second and third calls succeeded — expected per my role card. |

## iter-3 defects — reproduction and fix logic

### 3a — CSV written before import verified

**READ, with reason.** I traced the code path:

- Before iter-3 (`git show 541864b38 -- Assets/Editor/ContentArtFetcher.cs`): `ProcessCatalog`
  called `File.WriteAllText(csvFull, …)` at line 525 INSIDE the outer `try/finally` that also
  contained `AssetDatabase.StopAssetEditing`. The next-phase `ApplyAndVerifyImport` on line 419
  ran after. A `Refuse` inside that phase flipped `Verdict` but the CSV was already committed.
- After iter-3: the write is deferred. `pending.Edits.Add((outcome, i, index[slot.NameColumn]))`
  at line 560 accumulates the splice-plan; the actual `File.WriteAllText` sits inside
  `FinalizeCsvs` (line 637), which runs AFTER the `foreach (var o in report.Fetched.ToList())
  ApplyAndVerifyImport(o, report)` at line 428. The refuse-and-revert branch at lines 621–632
  reverts the field to empty and calls `AssetDatabase.DeleteAsset` on `WrittenPath`. The
  `!anySurvived continue` at line 634 correctly suppresses the `File.WriteAllText` when every
  edit was reverted.

Fix logic is sound. I did NOT run the tripwire (see role constraint above); I confirmed the
implementer's tripwire evidence in `IMPLEMENTER_REPORT.md` § 3a is consistent with the code path
I traced. No independent OBSERVED evidence from me.

### 3b — SharedWithSibling treated as safe unconditionally

**READ, with reason.** Same trace approach:

- Fix at lines 592–596 builds `failedTargets` (case-insensitive, matching the
  `ExistingAsset`/`produced` dict convention iter-2 established) from every Refused outcome
  that had a `WrittenPath`. That is the set of DERIVED TARGETS whose asset did not survive.
- At line 605, `targetSurvived = !failedTargets.Contains(outcome.Folder + "/" + outcome.DerivedName)`
  is evaluated for every edit, not only own-verdict ones. A `SharedWithSibling` whose target
  died has `ownVerdictOk=true` AND `targetSurvived=false` → the Refuse branch at 613–617 fires
  and the field is reverted.
- Because `WrittenPath` is empty on a `SharedWithSibling` outcome (line 767, set only in the
  actual-write path), the `AssetDatabase.DeleteAsset` at 626 is correctly skipped — the file
  was already deleted by the Fetched sibling's iteration.

Report's before/after table for the six-club run matches this logic. No independent OBSERVED
evidence from me.

### Both happy paths

**READ, with reason.** From code:

- **Single Fetched row.** `ownVerdictOk=true`, `targetSurvived=true` → `anySurvived=true`,
  `continue`. After the loop, `File.WriteAllText` fires, CSV gets the name. Same shape as
  before, just deferred.
- **Six shared club rows.** One Fetched + five `SharedWithSibling`, all with the same
  `Folder+DerivedName` key. All six have `ownVerdictOk=true`. If none failed verification,
  `failedTargets` is empty, all six `targetSurvived=true`, all six pass through the `continue`.
  One file on disk (Fetched wrote it), six CSV names spliced across the two catalogs' pending
  edits (all clubs → one catalog).
- **Re-run no-op.** Every row now has `existingName` set → the precondition at line 522 skips
  it → no outcomes → `!dirty` return before `pending` is added to `allPending` → `FinalizeCsvs`
  loops zero times → `AppendToReport` short-circuits on `NoOp && RefusedCount==0 && Errors==0`.

## The third defect I looked for — one real candidate, one that isn't

Cesar's kickoff explicitly asks me to hunt for another instance of "state committed before
validated." I found one plausible candidate and ruled out three others.

### Candidate — mid-loop `File.WriteAllText` failure in `FinalizeCsvs`

Lines 634–645, the catalog write:

```
if (!anySurvived) continue;
try {
    File.WriteAllText(pending.FullPath, string.Join("\n", pending.Lines));
    AssetDatabase.ImportAsset(pending.RelPath, ImportAssetOptions.ForceUpdate);
} catch (Exception e) {
    report.Errors.Add($"could not write {pending.RelPath}: …");
}
```

Scenario: 4 catalogs enqueued in `allPending`. Catalog 2's `File.WriteAllText` throws (disk
full, file locked by AV scanner, permission race, transient IO). The catch appends to
`report.Errors` and the loop moves to catalog 3.

Consequences:

1. **Assets for catalog 2's Fetched outcomes remain on disk.** They were written by
   `TryFetchOne` and verified by `ApplyAndVerifyImport`; nothing here deletes them, because
   they are not in `failedTargets` (their verdict is still `Fetched`).
2. **`report.Fetched` still counts them as bundled.** `AppendToReport` runs immediately after,
   iterating `Outcomes.Where(o => o.Verdict == Verdict.Fetched)` — the write-throw does NOT
   flip the verdicts. So the report file (`content_art.txt`) records those rows as added,
   including a `+ Portraits/Thumbnails/X.png ...` line and the byte totals, while the CSV never
   gained the name column.
3. **Next run collision-refuses.** `ExistingAsset` sees the file → refuses. The row is stuck
   naming-wise until a human intervenes.

That is the same shape as 3a: state committed to the repo (the asset file, and the size-report
addendum) before its validator (the CSV write) has succeeded. It is much rarer than 3a in
practice — `File.WriteAllText` on a small text file effectively never throws in a dev
environment — and it is at least noisy (`report.Errors` gets an entry, and `LogSummary` logs at
warning level when `Errors.Count > 0`). But it is real, it is testable under a seam, and it
matches the pattern the last two iterations were about.

Not FATAL on its own; flagging it because Cesar asked me to look and I think a fix is one
sentence: on catch, iterate `pending.Edits`, flip surviving outcomes to `Refused`
(with detail: "the CSV write failed, the asset was not bundled"), and delete their
`WrittenPath`. Same shape as the 3b revert logic, applied to the write-failure branch.

### Ruled out — `AppendToReport` and `LogSummary` consistency post-flip

Both use lambda-evaluating properties on `RunReport` (`Fetched => Outcomes.Where(o => o.Verdict
== Verdict.Fetched)`). Refuse-flips inside `FinalizeCsvs` are picked up on re-evaluation — a
Fetched-turned-Refused correctly drops from Fetched counts and into Refused. The `Format`,
`MaxTextureSize`, `BuildBytes` and `SourceBytes` fields set inside `ApplyAndVerifyImport`
success block are only read for `Fetched` outcomes, so refused-during-verification outcomes
never surface those fields in reports. Not a defect.

### Ruled out — `Outcome.Detail` dual-use

`Detail` still switches between "asset path on success" and "refuse reason" in six places, but
`ApplyAndVerifyImport` at line 926 now reads `WrittenPath` (not `Detail`) as the asset path,
and `FinalizeCsvs` at line 620–629 also reads `WrittenPath`. `LogSummary`'s Fetched loop reads
`o.Detail` at 1131 — for a Fetched-that-stayed-Fetched, Detail was set to `assetPath` at line
1017. Consistent.

### Ruled out — cross-catalog Folder/DerivedName collisions

Each catalog writes to a distinct `slot.Folder`; `Folder+DerivedName` cannot collide across
catalogs, so `failedTargets` behaves correctly across the whole run.

## Housekeeping

- **CSVs and report file byte-identical to tracked tip.** MD5:
  Characters `59e308da175439d5f91a84988f85b144`,
  Items `34dcbf5bb540e2d182bd116488a24b97`,
  Balls `e60dccc15c7a16981370b5e98d59321d`,
  Clubs `b15eefbbc73ed0cd39e52ca41fa5e952`,
  content_art.txt `447c017206077893a56c3d34965121a7`. All match iter-2's SELF_REVIEW chain.
- **`ContentArtFetcher.cs` matches HEAD** at `72079f9a7f3b174518a401e907b0f88f`, i.e. the
  committed iter-3 tip. `git diff HEAD -- Assets/Editor/ContentArtFetcher.cs Assets/Data/
  Assets/Resources/Data/ Docs/Reports/content_art.txt` empty.
- **Live collision targets untouched.** `Driver-FairX.png` `16f50050b7eaf1198717d84a781aa5ab`,
  `James.png` `596d962f5fba371aea9abd44bbd5ab86` — same hashes as iter-2.
- **No fixtures on disk.** No `char_arttest`, `Ordertest.png`, `_ArtFetchTemp/`, `_BallsFullStash/`.
- **Working tree noise is entirely Cesar's concurrent work + `club_art_batches` art drops.**
  `git status` shows `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab`,
  `Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs(.meta)`, plus the four pre-existing docs
  Cesar named as out-of-scope. All match the kickoff exclusion list.
- **Editor state clean.** `IsPlaying=false`, `IsPaused=false`, `IsCompiling=false`,
  `IsUpdating=false`; `ShellScene` open and `IsDirty=false`.

## Rule sweep

- **Rule 1 (circuit breaker).** iter-3 shape is `content-pipeline:csv-written-before-verified`,
  distinct from iter-1 (`url-art-not-bundled`) and iter-2 (`collision-guard-case`). No
  escalation trigger. Task on iter-3, first pass of this shape.
- **Rule 5 (full acceptance re-run).** Done above.
- **Rule 6 (report integrity).** Every PASS claim in the iter-3 report is either backed by
  logs the implementer pasted or by code I traced in this review. The one place I flag as
  under-tested — both defects need a seam — the implementer discloses explicitly.
- **Rule 7 (bans).** No `Assets/Scripts/Physics/`, no `*Gate` scenarios, no
  `LabScaffold.unity`, no `M_Splash*.mat` in `git show 1c586ee9d --stat`.
- **Rule 15 (reproduce-the-rejection).** N/A: no `CESAR_REJECTION.md` for iter-2 — iter-3 is a
  self-triggered redo.
- **Rules 2/3/4/9/10/11/14/16/17/18/19/21.** N/A — no Figma node, no mesh/terrain bake, no
  reuse mandate, no UI prefab, no world→screen invariant, no canonical screenshot / real-entry
  widget, no ScreenId.

## Verdict on the seam gap

**Ship-blocker: build the seam now.** Cesar asked and my answer is unambiguous.

1. **Two iterations of gates missed this class of defect in this file.** iter-2 red-team missed
   3a; the iter-2 self and architect reviewers also missed it. That is three gates blind to one
   ordering error, in code the whole task is centered on.
2. **iter-3 materially restructured the write path** — inline `File.WriteAllText` was replaced
   with a two-pass accumulator + finalize. That is exactly the code most likely to regress
   under future edits (the very next edit could reintroduce an inline write inside `ProcessCatalog`
   for "performance" or "simplicity" and no test would fire).
3. **The manual tripwire is not reproducible by a gate.** No reviewer role is permitted to
   mutate `ApplyAndVerifyImport` to force a fail; I could not do it here, and the architect and
   red-team gates similarly cannot. That means every future review is blind to this class of
   defect unless it is caught by inspection — and inspection missed it twice already.
4. **The seam is cheap.** An `internal static Action<Outcome, List<string>>? _verifyFaultHook`
   (or an injectable `Func<Outcome, List<string>>` for the `problems` list), null in production,
   swapped in test setup, would let two tests pin the exact defects the manual tripwire
   demonstrates — one for 3a (Fetched→Refused reverts CSV + deletes asset + .meta), one for 3b
   (six shared rows → target-failure reverts five siblings). The seam plus the two tests is
   probably 40 lines.
5. **I found a third candidate (mid-loop `File.WriteAllText` throw) that is only realistically
   testable under a seam.** That candidate corroborates the need. Not addressing the seam ships
   an untested code path adjacent to the two we just fixed, that we already know can bite the
   same way.

The right sequence: implementer adds the seam, ports 3a and 3b to regression tests, considers
(or defers with rationale) the mid-loop write-throw candidate. Then this loops around to me and
the two review gates. That is one round of implementer work; the seam risk of NOT doing it is
the cost of a fourth defect that Cesar has to find while writing another brief.

## Verdict

**FAIL — `SELF_REVIEW_FAIL`.** Loops back to `golfin-implementer`.

**Fix list, concrete:**

1. **Add an injectable verification-fault seam** to `ContentArtFetcher.ApplyAndVerifyImport` so
   tests can force a refuse without editing the file. Shape: an `internal static
   Func<Outcome, List<string>>? _verifyFaultHook` (or equivalent) that, when non-null, is
   invoked at the top of the `problems` build and can append entries. Null default preserves
   production behaviour byte-for-byte.
2. **Add two EditMode regression tests** exercising the seam:
   (a) A single Fetched row → verification forced to fail → assert `Outcome.Verdict == Refused`,
       the CSV field is empty in the on-disk file, the written `.png` AND `.meta` are gone,
       `report.Errors.Count == 0`, `report.RefusedCount == 1`.
   (b) Six club rows sharing one URL → verification forced to fail on the Fetched sibling →
       assert all six outcomes end at `Verdict == Refused`, all six CSV fields are empty,
       the single `.png` (and `.meta`) is gone, no orphan rows still name it.
3. **Judge and either close or explicitly defer the mid-loop `File.WriteAllText` throw
   candidate** I flagged above. If deferring, put the rationale in `IMPLEMENTER_REPORT.md`
   iter-4 in a `## Known ordering hazards` section — the pattern of "one plausible third
   defect, not fixed, not surfaced" is exactly what iter-2 → iter-3 was about.
4. **Re-run the full EditMode sweep** and re-verify housekeeping (byte-clean CSVs +
   content_art.txt, live collision targets untouched).

Iteration count is 3, but the standing "N≥3 → ESCALATE" rule is about three-rounds-of-rejection
against the same failure mode. iter-1 and iter-2 review outcomes were PASS; iter-3 is a
self-triggered redo, first review pass on this failure shape. FAIL is the correct routing.

## File summary

| File | What |
|---|---|
| `Docs/Specs/Active/content_art_bundling/SELF_REVIEW.md` | Appended iter-3 section (this block). |
| `Docs/Specs/Active/content_art_bundling/STATUS.md` | Set to `SELF_REVIEW_FAIL`. |
