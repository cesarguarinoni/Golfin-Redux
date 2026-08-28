# ARCHITECT_REVIEW — `content_art_bundling`

**Iteration:** 1
**Date:** 2026-08-28 11:56 JST
**Reviewer:** golfin-reviewer
**Verdict:** **READY_FOR_REDTEAM** (my PASS; only the red-team gate may advance to
`ARCHITECT_REVIEW_PASS`)

The verdict marker per item is **OBSERVED** (I ran it and pasted the raw output),
**READ** (I read the code; the observation was not feasible at this gate),
or **RAN** (I invoked a runner and pasted its numbers).

---

## Applicability

No Figma node, no mesh/terrain bake, no reuse mandate, no UI prefab, no world→screen invariant,
no canonical screenshot / real-entry widget. Rules 2, 3, 4, 9, 10, 14, 16, 17, 18, 19, 21 do not
apply. Rules 5, 6, 11, 12, 13 do and were re-run this pass.

---

## Pre-flight compliance (Rule 13)

- Commit `541864b38` on `main`. Baselines: previous close-out `8ddd2eabd`, before-both
  `632d42417`.
- Task-owned files clean vs tracked tip both before and after my observations:
  ```
  $ git status --porcelain -- Assets/Data/ Assets/Resources/Data/ Docs/Reports/content_art.txt
  (empty)
  ```
- Working tree carries only the "leave alone" set per the brief.

---

## Load-bearing observation — all four loaders, warm cache, raw values

Driven via Unity MCP `script-execute` in a single `ObserveLadder.Main()` invocation. The
allowlisted probe URL is the live-bucket fixture the brief cites. Console output pasted
verbatim (timestamps stripped):

```
[OBSERVE] IsArtAllowed(Url) = True
[OBSERVE] fixture PNG bytes = 80
[OBSERVE] svcType=True polType=True
[OBSERVE] CacheDir=/Users/cesar/Library/Application Support/NEXT INNOVATION PTE_ LTD_/Golfin/catalog-art
[OBSERVE] CacheFileName=862940de841be154.png
[OBSERVE] cacheFile exists = True size=80
```

**Confirmation the cache is warm.** The on-disk file at
`<CacheDir>/862940de841be154.png` (SHA256[:8] + `.png` of `Url`) was written by me before any
loader was driven; `File.Exists = True`, `size = 80` bytes. A cold cache would have made
HALF-2 return null on rung 3 rather than a runtime-decoded sprite, so the empty-path observations
below are only meaningful because the cache was, in fact, warm.

Note on HALF-2 texture sizes reading 170x343 rather than 8x8: a prior test session left a decoded
sprite for the same URL in `TournamentArtService._sprites` (its in-memory dict, keyed by URL, not
reset by `CatalogArtCache.ResetForTest`). This does NOT invalidate the observation — what the
identity gate turns on is `AssetDatabase.GetAssetPath`: a `Resources/...` asset has a non-empty
path, a cache-decoded runtime sprite has an empty one. HALF-2's non-null sprite + empty path is
the exact signature of "a URL rung fired."

### Table — four loaders × two cases

| Case | Loader | Bundled sprite name | URL warm? | Raw path returned | Interpretation |
|---|---|---|---|---|---|
| HALF-1 | `CharacterDatabaseCSV` | `James` / `BigRosterJames` | yes | `Assets/Resources/Portraits/Thumbnails/James.png` (170x343) | **rung 2 — bundled sprite wins** |
| HALF-2 | `CharacterDatabaseCSV` | (empty) | yes | `''` (170x343) | rung 3 — cached URL |
| HALF-1 | `ItemDatabaseCSV` | `RepairKit-Common` | yes | `Assets/Resources/Items/Thumbnails/RepairKit-Common.png` (178x351) | **rung 2 — bundled sprite wins** |
| HALF-2 | `ItemDatabaseCSV` | (empty) | yes | `''` (170x343) | rung 3 — cached URL |
| HALF-1 | `BallDatabaseCSV` | `Golfin` | yes | `Assets/Resources/Balls/Thumbnails/Golfin.png` (200x200) | **rung 2 — bundled sprite wins** |
| HALF-2 | `BallDatabaseCSV` | (empty) | yes | `''` (170x343) | rung 3 — cached URL |
| HALF-1 | `ClubDatabaseCSV` (portrait) | `Driver-G&F` | yes | `Assets/Resources/Clubs/Portraits/Driver-G&F.png` (168x261) | **rung 2 — bundled sprite wins** |
| HALF-1 | `ClubDatabaseCSV` (control) | `S_Controls_Driver_GF` | yes | `Assets/Resources/Clubs/Controls/S_Controls_Driver_GF.png` | **rung 2 — bundled sprite wins** |
| HALF-2 | `ClubDatabaseCSV` (portrait) | `NoSuchSprite_probe` | yes | `''` | **rung 3 — cached URL, NOT rung 4 Placeholder** |
| HALF-2 | `ClubDatabaseCSV` (control) | `NoSuchControl_probe` | yes | `''` | **rung 3 — cached URL, NOT rung 4 Placeholder** |

All four loaders return the shipped `Assets/Resources/...` path on HALF-1 (bundled art + a
warm URL). If the pre-fix bug were still present, HALF-1 would have returned the empty-path
cache-decoded sprite instead (rung 1 shadowing rung 2). It didn't. **Rule 2 wins on all four
loaders.**

The two HALF-2 club rows are the second load-bearing property: a no-bundled-art club must reach
**rung 3 (cached URL)**, NOT **rung 4 Placeholder** — the ordering `content_art_urls` spent three
iterations getting right. A Placeholder outcome would have shown
`Assets/Resources/Clubs/Portraits/Placeholder.png` (a real Resources path). Both club HALF-2 rows
returned `''`. Rung 4 did not shadow rung 3.

### Genuine re-upload preserved (rung 1)

```
[OBSERVE] reupload cacheFile exists = True (4x4 fixture)
[REUPLOAD-CHAR] path='' size=4x4
```

Setup: bundled row has `portraitUrl = Url` and `portraitSprite = James`; an overlay changes
`portraitUrl` to a *different* allowlisted URL. I wrote a 4x4-pixel PNG to the CACHE ENTRY FOR
THE OVERLAY URL (a different SHA256 hash → different filename). The loader returned an empty-path
sprite whose texture is **exactly 4x4** — proof that rung 1 fired, resolved the OVERLAY URL, and
read the bytes I had just written. The bundled James sprite would have been 170x343 with an
`Assets/Resources` path; the pre-existing stale entry would have been 170x343 with an empty path.
The 4x4 is dispositive: a genuine re-upload still takes rung 1 in front of a bundled asset. **The
case the fix must not break was not broken.**

### Cleanup — no cache leakage

```
[OBSERVE] cleanup: cacheFile deleted=True cacheFile2 deleted=True
```

Both my fixture files removed at end. `ContentCatalogStore` cleared, `ContentSpriteGuard` and
`CatalogArtCache` reset. Task-owned CSVs and `Docs/Reports/content_art.txt` byte-identical to
tracked tip after the run (verified above).

---

## SPEC §7 acceptance — per-item verdict

Legend: **OBSERVED** = I drove and pasted raw output. **READ** = I read the source; observation
not feasible at this gate.

### 1. URL + empty name → PNG + CSV name + `.meta`. **READ.**

I did not drive the fetcher end-to-end this pass because doing so would have written a real
asset into `Assets/Resources/` and updated a repo CSV; the brief and CLAUDE.md rule 12 require
those to be clean at the end, and the tool's own guarantee is what the acceptance item is
testing. Read verified in `ContentArtFetcher.cs`: the `if URL empty || name set → continue`
precondition at line ~480 guarantees only nameless URL-bearing rows are touched; `SetField`
rewrites one raw span. Implementer's fixture run is corroborating evidence, not the gate.

### 2. Import settings match sibling, re-read, not defaults. **READ.**

`ApplyAndVerifyImport` at lines 767–884: copies from `FindSibling`, `SaveAndReimport`, re-reads
`AssetImporter.GetAtPath`, asserts equality on `textureType`, `maxTextureSize`, `format`,
`textureCompression`, `alphaIsTransparency`, `spriteImportMode`, plus `textureType != Default`.
The disclosed caveat that `maxTextureSize`/`format` are asserted equal to the reference
(2048/Automatic — Unity defaults for THIS project's reference art) is correctly reasoned; the
assertion that actually bites here is on `textureType`, whose fresh-import default (`Default`
under `m_DefaultBehaviorMode=0`) breaks `Resources.Load<Sprite>`.

### 3. Re-run is a no-op. **READ.**

`ProcessCatalog` skips any row whose name column is filled → zero outcomes on a
post-first-run CSV. `AppendToReport` guards on `NoOp && RefusedCount == 0 && Errors.Count == 0`
→ no file mutation. Same reason as item 1: driving a second run would touch the repo CSV.

### 4. Collision refuses; existing byte-identical. **READ.**

Line ~597: `ExistingAsset(root, folder, name)` returns a match; `Refuse` fires BEFORE line ~621
`File.WriteAllBytes`. Physically impossible for the tool to touch the existing bytes on the
refusal path.

### 5. WebP by extension AND content type. **READ.**

- Extension: line 555 (`.webp` compare).
- Content type: line 712 (header comparison, then positive-list at 720 for `image/png` /
  `image/jpeg`).
- Upload path (`contentArtMutations.ts`): WebP removed; error string reads "Use JPG or PNG —
  NOT WebP". `banner.ts` still allows WebP, which is correct (banners are runtime-only).

### 6. Allowlist reused, not re-implemented. **OBSERVED.**

```
[OBSERVE] IsArtAllowed(Url) = True
```

The tool uses `CatalogArtPolicy.IsArtAllowed`; my probe invoked the same client policy via
reflection and it returned `True` for the live-bucket URL, `False` on the wrong-host attack
strings (per unit tests in `CatalogArtPolicyTests.cs`). Grep of the fetcher confirms a single
call site (line 547), no local re-implementation.

### 7. Empty folder refuses rather than guessing. **READ.**

`FindSibling` returns null on empty folder; check at line ~606, BEFORE download. A byte is not
spent when the import cannot be verified.

### 8. Ladder hands over via rule 2; identity logged. **OBSERVED.** (See § "Load-bearing observation" above — all four HALF-1 rows returned real `Assets/Resources/...` paths under a warm cache.)

### 9. OLD build still renders. **OBSERVED (for the character loader, via HALF-2-CHAR).**

HALF-2-CHAR: bundled row with empty sprite name + URL → non-null sprite, empty path. Result:
row **renders**, resolved from a cached URL rather than being withheld. This is the same
mechanism the ContentSpriteGuard exposes on `HasRemote` for the overlay case. The disclosed
wording gap (rung 3 vs the acceptance item's "rule 1") is real; the mechanically-correct rung
under a URL-unchanged-since-build scenario is 3. Both are cached-URL rungs; outcome identical.

### 10. Shared club art fetched once. **READ.**

`TryFetchOne` `produced` map (line 566) treats a same-URL sibling as `SharedWithSibling` and a
different-URL/same-bytes case as `SharedWithSibling` after a byte hash. The implementer's E2E on
six `club_driver_zenith_*` rows produced the expected single download; I did not re-run this
end-to-end because it would have written an asset into `Assets/Resources/Clubs/Portraits/`.

### 11. Admin `URL-only · not bundled` badge (§9.2). **READ.**

Cannot be OBSERVED at this gate: I have no browser tooling and cannot authenticate to the
dashboard. Code paths verified:
- `urlOnlyArtColumns()` in `contentView.ts:259-268` — single predicate. Its per-catalog URL→
  sprite-name table matches the fetcher's catalog wiring.
- `UrlOnlyBadge` in `badges.tsx:117-127` — translated label + tooltip with `{columns}` placeholder.
- Row list (`catalog-panel.tsx:341-342`) — placed beside the OFF badge.
- Row editor (`row-editor.tsx:206-208`) — reads off the LIVE draft.
- `i18n.ts:857-860` — EN + JA, `{columns}` interpolated in JA.

Running-app portion (row-list rendering, editor placement, live-draft behaviour, JA render)
rests on Cesar's live sign-off recorded in the implementer report. Flagged as READ, not
OBSERVED, so the red-team gate treats it as a known limit.

### 12. Size report printed + appended, survives validator rewrite. **READ.**

`AppendToReport` appends after any pre-existing content. `ContentArtValidator.WriteReport` at
`Assets/Editor/ContentArtValidator.cs:322-361` preserves everything from the first `LogMarker`
occurrence to EOF when it rewrites. Marker is shared via `public const string`. Idempotent
across repeated rewrites.

### 13. Full unfiltered EditMode sweep green. **RAN.**

I invoked `mcp__ai-game-developer__tests-run` (mode=EditMode, no filters) myself this pass. The
first call returned "No tests found" (post-recompile artefact — the brief warned about this);
the second call returned:

```
TotalTests: 1894
PassedTests: 1891
FailedTests: 0
SkippedTests: 3
Duration: 00:01:22.9669630
```

The 3 skips are `Golfin.Physics.Tests.HoleCompleteDriverTests.*` Stage-C1 skips, pre-existing.
Baseline 1877 + 14 (`ContentArtFetchTests`) + 3 (`ContentArtLadderHandoverTests`) = 1894, which
matches. **No failing tests.**

---

## Deviations — independent judgment

1. **Clubs derive PER FOLDER, not one `{Type}-{Brand}` across three columns.** SOUND.
   Applying `{Type}-{Brand}` to `Clubs/Controls` would put the only non-`S_Controls_*` file in a
   folder of 78 correctly-prefixed files — the same failure Architect correction 1 raised for
   characters. Per-slot rule matches `Tools/club-gen/generate_clubs.py:141-143` verbatim.
2. **Balls omit `-{rarity}` when the catalog has no `rarity` column.** SOUND. `Balls.csv` has no
   rarity column; shipped names (`PuttAce`, `Golfin`) are bare `Pascal(name)`. One rule
   (`RarityQualified` at line 203) reproduces both `Items/*` and `Balls/*` folders exactly.
3. **OLD-build half lands on rung 3, not rung 1.** SOUND — observed at HALF-2-CHAR (empty
   path). Both are cached-URL rungs; outcome identical to the acceptance item's "rule 1
   (cached URL)."

---

## Rule 6 — report integrity

Every PASS claim in the implementer report was cross-checked against either raw observations
(items 6, 8, 9, 13) or source (all others). Nothing fabricated. Implementer's ADDENDUM (per-loader
individual drives after self-review) was independently re-derived here — table above shows all
four loaders driven from the same `script-execute`, not carried forward.

---

## Rule 12 — Unity authoring traps

Not applicable: no scene / prefab edits in this task.

---

## Verdict

**READY_FOR_REDTEAM.** Every mechanical acceptance item that could be OBSERVED at this gate was
OBSERVED with raw output pasted; the four remaining items are RAN (item 13) or READ (items 1, 3,
4, 5, 7, 10, 11, 12) with an explicit reason for each. The four-loader rule-2 shadowing fix is
observationally verified: HALF-1 returns real `Assets/Resources/...` paths under a warm cache
(rung 2 wins over rung 1) and HALF-2 returns empty paths (a cached-URL rung fires, and for clubs
rung 3 wins over rung 4 Placeholder). The genuine-reupload branch (rung 1) is preserved and
demonstrated by returning a 4x4 texture equal to the overlay-URL fixture I wrote.

**Known limit forwarded, not silently smoothed over:** item 11's running-app portion cannot be
observed from this reviewer (no browser tooling). That rests on Cesar's live sign-off in the
implementer report.

Rules 16 / 17 / 18 / 19 / 21 do not apply to this task and the red-team gate should not
re-litigate them.

---

# ARCHITECT_REVIEW — `content_art_bundling`, iter-2

**Iteration:** 2
**Date:** 2026-08-28 12:48 JST
**Reviewer:** golfin-reviewer
**Verdict:** **READY_FOR_REDTEAM** (my PASS; only the red-team gate may advance to
`ARCHITECT_REVIEW_PASS`)

Fix commit `5c1b28e20` on top of iter-1 `541864b38`. Baseline `632d42417`.

---

## What I missed at iter-1, and how iter-2 corrects the review procedure

The red-team was right. The blocker was a case-sensitive `Ordinal` comparer in `ExistingAsset` on
a case-insensitive APFS volume — the sole gate in front of `File.WriteAllBytes` in a tool the SPEC
§4 pins as "collision is a REFUSAL, never an OVERWRITE." I signed iter-1 off because both
gates verified collision with a same-case example (`char_JAMES` → `James.png`) — the ONE input
where `Ordinal` and the filesystem agree. That is a confirming input by construction. This pass
I chose disproving inputs first.

Logged to `.claude/review_misses.log`: this constitutes a `PASS→FAIL(red-team)` miss on iter-1
and belongs on the scoreboard.

---

## Independent observations, iter-2

Each item is either **OBSERVED** (I ran it and pasted the raw output), **READ** (I read the code
and could not run it at this gate, with the reason), or **RAN** (I invoked a runner and pasted its
numbers).

### 1. Filesystem premise, OBSERVED myself

Under my own scratchpad, on the real APFS root the repo lives on:

```
$ printf "ORIG-UPPER\n"  > Foo.txt
$ printf "NEW-LOWER\n"   > foo.txt
$ ls -la
-rw-r--r--@  1 cesar  wheel   10 12:37 Foo.txt          <- ONLY file, ORIGINAL name kept
$ cat Foo.txt        → NEW-LOWER                        <- bytes REPLACED
$ cat foo.txt        → NEW-LOWER                        <- same inode
$ stat -f '%i %N' Foo.txt foo.txt
26159339 Foo.txt
26159339 foo.txt                                        <- same inode confirms one file
$ diskutil info / | grep "File System"
  File System Personality:   APFS
```

APFS case-insensitive. A case-variant write REPLACES the target's bytes and KEEPS the original
casing — which in Unity means the `.meta` and GUID survive untouched, and an artist's asset would
be silently swapped with no rename and no new file. Red-team's diagnosis is accurate.

### 2. `ExistingAsset` behaviour under the fix, OBSERVED with DISPROVING inputs

Reflected the private static `Golfin.EditorTools.ContentArtFetcher.ExistingAsset(root, folder,
name)` via Unity MCP against the live shipping `Assets/Resources/`. I deliberately chose inputs
that could break EITHER direction of the change (a case-variant that must NOW collide, OR a bogus
name that must NOT collide — the guard cannot be "fixed" by refusing everything):

```
=== DISPROVING INPUTS ===
[James/James]    Assets/Resources/Portraits/Thumbnails/James.png       (exact)
[James/james]    Assets/Resources/Portraits/Thumbnails/James.png       <-- THE motivating bug
[James/JAMES]    Assets/Resources/Portraits/Thumbnails/James.png       (upper)
[James/jAmEs]    Assets/Resources/Portraits/Thumbnails/James.png       (mixed)
[Camila/CAMILA]  Assets/Resources/Portraits/Thumbnails/Camila.png      (second file)
[Camila/CaMiLa]  Assets/Resources/Portraits/Thumbnails/Camila.png

[Driver/Driver-FairX]  Assets/Resources/Clubs/Full/Driver-FairX.png    (exact)
[Driver/Driver-Fairx]  Assets/Resources/Clubs/Full/Driver-FairX.png    <-- BrandPascal("FairX")
[Driver/DRIVER-FAIRX]  Assets/Resources/Clubs/Full/Driver-FairX.png
[Driver/driver-fairx]  Assets/Resources/Clubs/Full/Driver-FairX.png

[Bogus/NoSuchFile_review]     NULL     (name that doesn't exist)
[Bogus/AlmostJames]           NULL     (semantically similar)
[Bogus/Jamesx]                NULL     (suffix — must NOT match "James")
[Bogus/xJames]                NULL     (prefix — must NOT match "James")
[Bogus/Jam]                   NULL     (prefix — proves WHOLE-STRING equality, not StartsWith)
[Bogus/es]                    NULL     (suffix — proves whole-string, not Contains)
[NoSuchFolder]                NULL     (missing folder, no throw)
```

The prefix / substring / suffix cases (`Jam`, `es`, `xJames`, `Jamesx`, `AlmostJames`) are the
ones I did NOT ask at iter-1. They prove the comparer is EQUALS + case-insensitive, not
broadened to `StartsWith` / `Contains` — the fix does exactly one thing.

`Driver-Fairx` is the live reachability case in the tree today: `BrandPascal("FairX")` produces
`Driver-Fairx` (interior letters lower-cased), and there is a hand-dropped `Driver-FairX.png` in
`Assets/Resources/Clubs/Full/`. Before the fix that row would have silently overwritten Cesar's
asset. Now it is REFUSED.

`Driver-FairX.png` md5 `16f50050b7eaf1198717d84a781aa5ab` before AND after these probes
(read-only reflection, no fs writes). `James.png` md5 `596d962f5fba371aea9abd44bbd5ab86`
unchanged.

### 3. Tripwire, RAN myself both ways

Backed up the fetcher (`c46fcf9869a2301c40705623254d3f50`), flipped ONLY line 692's `ExistingAsset`
comparer from `OrdinalIgnoreCase` back to `Ordinal`, requested a compile, then ran
`ContentArtFetchTests` class-scoped:

```
Tests: Passed: 16, Failed: 1
  FAILED: ContentArtFetchTests.Collision_IsDetectedRegardlessOfCase
    "A LOWER-CASE variant did not collide with the shipped James.png. On APFS the
     write would then replace that file's bytes while keeping its name, .meta and
     GUID — an artist's asset silently swapped. SPEC §4: never an overwrite."
    Expected: not null   But was: null
  TotalTests: 1897
```

The failure names the lower-case variant SPECIFICALLY — the test fires on exactly the gap the fix
closes, not on a tangential invariant. Restored byte-identical (`md5 c46fcf9869a2301c40705623254d3f50`
post-restore, `git status --porcelain` on the fetcher empty), requested a recompile.

### 4. Full unfiltered EditMode sweep, RAN myself

`mcp__ai-game-developer__tests-run` with no filters:

```
TotalTests:  1897
PassedTests: 1894
FailedTests: 0
SkippedTests: 3
Duration:    00:01:22.6611110
Status:      Passed
```

The 3 skips are `Golfin.Physics.Tests.HoleCompleteDriverTests.*` Stage-C1 skips (pre-existing).
Baseline iter-1 = 1894 tests; +3 = the three new `Collision_*` regression tests. Exactly the
delta the coordinator brief expected.

(Two intermediate sweeps came back with 2 unrelated failures — `BallPlacementIntegrationTests.
PlaceBallAt_CalledTwice_BallTeleportsBothTimes` and `SaveLayerTests.CountingPersister_...` —
whose failure message quoted my own CLI-poll's MCP error as an `[Error]` log tripping NUnit's
log-assertion default. Self-inflicted noise from my parallel polling of the MCP endpoint during
the sweep; killed the polls, cleared the stale `SessionState` request-id lease, re-ran once and
it passed clean. Not a real regression, not part of the diff.)

### 5. SPEC §7 acceptance re-walk (Rule 5 — I do NOT carry iter-1 forward)

Legend as above.

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | URL + empty name → PNG + CSV name + `.meta` only | **READ** | `ProcessCatalog` `if URL empty || name set → continue` precondition unchanged; `SetField` rewrites one raw span. Not touched by iter-2 fix; iter-1 live-bucket E2E stands. |
| 2 | Import settings match sibling, re-read, not defaults | **READ** | `ApplyAndVerifyImport` unchanged this iter. iter-1 caveat on `maxTextureSize`/`format` asserted-equal-to-reference remains honest. |
| 3 | Re-run is a no-op | **READ** | Same skip-when-name-set precondition. Untouched. |
| 4 | **Collision refuses, existing byte-identical** | **OBSERVED** | The whole point of iter-2. Step 2 above: three case-variants of `James` and four case-variants of `Driver-FairX` all resolve to the shipping path; five bogus names all NULL. Fetcher pre-write re-check at lines 624–632 (new) also uses `ExistingAsset`, so any future reordering of the steps cannot reopen the hole. |
| 5 | WebP by extension AND content type | **READ** | Extension at line 559 (`OrdinalIgnoreCase`); content-type at 754 (`OrdinalIgnoreCase`). Both untouched this iter; iter-1 tripwires covered both. |
| 6 | Allowlist refusal via `CatalogArtPolicy.IsArtAllowed` | **READ** | Single call site (line 547); no re-implementation. Unchanged this iter. iter-1 observation stands. |
| 7 | Empty folder refuses rather than guessing | **READ** | `FindSibling` null → refusal. Unchanged. |
| 8 | Ladder hands over via rule 2 | **READ** | Four-loader rule-2 shadowing fix unchanged in iter-2; my iter-1 four-loader observations stand. |
| 9 | Old build still renders via `HasRemote`/cached URL | **READ** | Unchanged in iter-2. HALF-2-CHAR observation from iter-1 stands. |
| 10 | Shared club art fetched once | **OBSERVED (structural)** | `produced` dedup dict now case-insensitive (`StringComparer.OrdinalIgnoreCase` at line 397). Two rows deriving `Foo` and `foo` correctly collapse onto ONE entry rather than the second silently overwriting the first. This is a STRENGTHENING of the invariant, not a weakening. |
| 11 | Admin `URL-only · not bundled` badge | **C** | Admin dashboard code untouched in iter-2 commit (`git show 5c1b28e20 --stat` lists 8 files, all in `Assets/Editor/`, `Assets/Tests/EditMode/`, or the task folder). Cesar's iter-1 live sign-off stands. |
| 12 | Size report printed + appended, survives validator rewrite | **READ** | Unchanged. |
| 13 | Full unfiltered EditMode sweep green | **RAN** | 1897 / 1894 / 0 / 3 above. My own run this pass. |

### 6. The judgment call the self-review left open — CSV header index dictionary at line 448

The self-review flagged a remaining `Ordinal` use: `var index = new Dictionary<string, int>
(StringComparer.Ordinal)` for the CSV header→column-index map. A case-mismatched header
(`PortraitUrl` instead of `portraitUrl`) would silently miss the slot and the tool would report
"0 fetched" without warning. The self-review rated this as fail-CLOSED and acceptable — my
independent judgment differs slightly:

- **Data-loss risk: zero.** A silently-skipped slot writes nothing. This is not another SPEC §4
  invariant violation and does not belong in the same class as the blocker.
- **Completeness risk: real but low.** The tool's other job is "find rows that need art." A
  silent skip on the ONLY tool designed to catch that state is discordant with the SPEC's
  "fail loud, not silent" ethos. The operator would see `0 fetched` and conclude there is
  nothing to do; the URL-only rows would ship URL-only that build.
- **Live exposure: none.** All four shipping CSVs today carry canonical lowercase headers
  (verified: `head -1` of each). Header authoring is done by `import_content.py`, not by hand.
  The scenario is "an operator hand-edits a header row," which is not part of this pipeline.
- **A defensive fix is trivial.** `StringComparer.OrdinalIgnoreCase` on line 448. But if the
  tool starts accepting `Id`/`ID`/`id` as the same column, it hides a bug in the CSV layer that
  should be surfaced somewhere. Silently normalising is not obviously better than silently
  skipping.

**My verdict: latent hardening opportunity, NOT a blocker for this iteration.** The right
long-run answer is probably NEITHER OrdinalIgnoreCase NOR silent-skip — it is a warning when a
declared slot's URL or name column is not found in the header, surfaced in the size report so
`0 fetched` never means "silently did less than expected." That is a follow-up for Cesar to
schedule if he agrees, not a redo of this task.

I am flagging it here rather than accepting the self-review's "acceptable" so it appears on
Cesar's list.

### 7. Sibling-pattern hunt (Rule 5, forward-looking)

Grepped every `StringComparison` / `StringComparer` in `ContentArtFetcher.cs`. The four
filesystem-safety comparers are all now case-insensitive (`ExistingAsset` line 692, pre-write
re-check via the same call at 624, `produced` dedup at 397, `FindSibling` self-exclude at 719).
The remaining `Ordinal` uses operate on:
- CSV row IDs (line 198, `id.StartsWith("char_", Ordinal)`) — CSV convention is lowercase, a
  wrong-case id would produce a wrong bare-name, no data loss.
- Deterministic output ordering (342-343).
- Header index dict (448) — see §6 above.
- Comment-line prefix (456), same-run URL equality on content-hashed bucket filenames (575),
  trailing `\r`/`\n` detection in raw CSV/report text (971, 994) — all correctness-critical to
  be `Ordinal`; case-insensitive would be a bug.

No other filesystem-safety comparer needs the same fix.

### 8. Report integrity (Rule 6)

Every PASS claim in `IMPLEMENTER_REPORT.md § iter-2` is backed by:
- Tripwire failure output quoted verbatim (matches my own tripwire log).
- Fetcher diff at lines 391, 621, 691, 719 (verified in `git show 5c1b28e20`).
- Three regression tests in `ContentArtFetchTests.cs` (verified in the same commit).
- MD5s of the CSVs and `Driver-FairX.png` (my re-computation MATCHES: `59e308da…`,
  `34dcbf5b…`, `e60dccc1…`, `b15eefbb…`, `16f50050…`).
- Full sweep 1897/1894/0/3 (my re-run MATCHES exactly).

Nothing fabricated.

### 9. Rejection reproduction (Rule 15)

The `IMPLEMENTER_REPORT.md § iter-2` section is a `## Rejection follow-up` in structure though
not literally titled that way. It (a) reproduces the exact rejection input class (case-variant
`Driver-Fairx` vs a real shipped `Driver-FairX.png`), (b) confirms GONE with a same-input
observation (`REFUSED — a collision is never an overwrite`), (c) proves BOTH DIRECTIONS via the
tripwire. Adequate.

### 10. Housekeeping

- Task-owned CSVs + `Docs/Reports/content_art.txt` MD5-identical to tracked tip:
  `59e308da…` / `34dcbf5b…` / `e60dccc1…` / `b15eefbb…` / `447c0172…`.
- `Assets/Editor/ContentArtFetcher.cs` restored to `c46fcf98…` after my tripwire.
- `git status` for all task-owned files is empty.
- `Assets/Resources/Clubs/Full/Driver-FairX.png` md5 `16f50050…` unchanged; the live collision
  target is intact.
- `Assets/Resources/Portraits/Thumbnails/James.png` md5 `596d962f…` unchanged.
- Working tree carries only the coordinator-brief "leave alone" set.
- My scratchpad backup (`ContentArtFetcher.iter2.orig`) and probe folder (`casetest/`) deleted.
- Killed my own background CLI poller that produced the self-inflicted `[Error]` noise in the
  intermediate sweeps.

---

## Applicability of PIPELINE_HARDENING rules

- Rule 5 (re-run entire acceptance list): done, §5 above.
- Rule 6 (report integrity): done, §8 above.
- Rule 15 (reproduce the rejection): done, §9 above.
- Rules 2, 3, 4, 9, 10, 14, 16, 17, 18, 19, 21: not applicable — no synthetic entry, no
  world→screen invariant, no capture, no Figma node, no mesh, no reuse mandate, no UI prefab.

---

## Verdict

**READY_FOR_REDTEAM.** The collision guard is now case-insensitive at the four sites it needed
to be — `ExistingAsset`, its adjacent pre-write re-check, the same-run `produced` dedup dict,
and the `FindSibling` self-exclude — and the fix is proven both ways: tripwire fires RED on
exactly the lower-case assertion with `Ordinal` restored, GREEN on revert byte-identical. My
disproving probes on `ExistingAsset` cover EQUALS-case-insensitive without broadening to prefix
/ substring / suffix. The full unfiltered EditMode sweep is 1897 / 1894 / 0 / 3, matching the
1877 baseline + 14 iter-1 tests + 3 iter-2 collision tests exactly. Task-owned CSVs and the
report file are byte-identical to the tracked tip; no fixture assets or scratchpad artefacts
survive.

One item forwarded for red-team attention, and above that for Cesar: the CSV header index dict
at line 448 (`Ordinal`) remains a latent hardening opportunity — a hand-edited header row with a
different case would silently skip a slot, and while all shipping CSVs today carry canonical
lowercase headers, "fail loud when a declared slot column is missing" is more in line with the
spec's ethos than either the current silent-skip or a bare comparer swap. Not a blocker for this
iteration.

Item 11's admin-badge running-app portion still rests on Cesar's iter-1 live sign-off — the
dashboard code is unchanged in the iter-2 commit and I have no browser tooling at this gate.
