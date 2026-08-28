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
