# IMPLEMENTER_REPORT — `content_art_bundling`

**Iteration shape:** `content-pipeline:url-art-not-bundled`
**Iteration:** 1
**Date:** 2026-08-28
**HEAD at kickoff:** `632d42417` (see `HEARTBEAT.log` § iter-1 kickoff baseline)

---

## Headline

`GOLFIN/Content/Fetch URL Art` exists, runs outside the build lane, and produces a reviewable
git diff. Every refusal in SPEC §3 fires with a specific message. The E2E ran against the **live
`catalog-art` bucket** (PIPELINE_HARDENING §21) and found the thing acceptance item 8 was written
to find:

> **Rule 2 was shadowed.** All four loaders defaulted "the bundled row's URL" to `""` when there
> was no overlay, so a *bundled* row carrying a URL compared its own URL against `""` — always
> "different" — and **rule 1 served the cached download in front of the build's own sprite.**
> The asset this whole task exists to produce would have landed in every build and never been
> used, silently. Fixed in all four loaders; observed red before, green after.

---

## Files modified or created

Every uncommitted path outside this task folder (Rule 13). `git status --porcelain
--untracked-files=all` at close of iteration.

### Mine

| File | What |
|---|---|
| `Assets/Editor/ContentArtFetcher.cs` (+`.meta`) | **NEW.** The tool. `GOLFIN/Content/Fetch URL Art` + the MenuItem-free `Run()`. Reads the four repo CSVs, refuses per §3, downloads, derives the name per §4, copies + verifies import settings per §5, splices the name into the CSV, appends the §6 size summary, prints the `import → publish → export` closing instruction. |
| `Assets/Editor/ContentArtValidator.cs` | `WriteReport` now PRESERVES everything from `ContentArtFetcher.LogMarker` to EOF when it rewrites `content_art.txt`. Without it the two tools silently erase each other and the fetch log survives exactly until the next build. |
| `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` | **Rule-2 shadowing fix.** `bundledPortraitUrl`/`bundledFullUrl` default to `null`, coalescing to the row's own URL — "no overlay ⇒ nothing changed ⇒ step 1 must not fire". |
| `Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs` | Same fix. |
| `Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs` | Same fix. |
| `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs` | Same fix (`row.bundled?.xUrl ?? row.xUrl` instead of `?? ""`). |
| `Assets/Tests/EditMode/ContentArtFetchTests.cs` (+`.meta`) | **NEW.** 14 tests on the derivation table + CSV splice; 3 more (`ContentArtLadderHandoverTests`) on the handover, the genuine-re-upload case, and the OLD-build case. |
| `Assets/Tests/EditMode/GolfinRedux.Tests.EditMode.asmdef` | `+ "Golfin.Content"` — the handover tests need `ContentCatalogStore` / `ContentCatalog` / `ContentRow`. Assembly-CSharp types stay on reflection (an asmdef cannot reference a predefined assembly). |
| `Docs/Game Design/ASSET_NAMING_CONVENTION.md` | §5 table gains the items/balls rule (Architect correction 1 asked for it *in the same commit*), the clubs Controls row, and the `ArtType`/`BRANDTAG` definitions. |
| `Docs/TESTFLIGHT_RUNBOOK.md` | §9 answer 3 — the release-prep ordering: `Fetch URL Art` → `import --apply` → publish → `export` → commit → lane. |
| `Tools/admin-dashboard/lib/contentView.ts` | `ART_URL_TO_SPRITE_COLUMN` + `urlOnlyArtColumns()` — the §9.2 predicate. |
| `Tools/admin-dashboard/app/(panels)/_content/badges.tsx` | `UrlOnlyBadge`. |
| `Tools/admin-dashboard/app/(panels)/_content/catalog-panel.tsx` | Badge in the row list's state column, beside `OFF`. |
| `Tools/admin-dashboard/app/(panels)/_content/row-editor.tsx` | Badge in the editor header, read off the LIVE draft so uploading art shows it at once. |
| `Tools/admin-dashboard/lib/i18n.ts` | `c.badge.urlOnly` + `c.badge.urlOnlyHint`, EN + JA. |
| `Tools/admin-dashboard/lib/mockContent.ts` | `mock_char_urlonly` — a fixture row in that state so the badge has something to render against, same purpose as the deliberately-disabled `balls` catalog. |
| `Tools/admin-dashboard/lib/contentArtMutations.ts` | The upload refusal still said *"Use JPG, PNG or WebP"* after WebP was removed in `c15998c30`, and the header comment still listed WebP. Both corrected — a message telling an operator to upload the one format the whole feature refuses is a live defect in this feature's blast radius. |
| `Docs/Specs/Active/content_art_bundling/*` | This report, `STATUS.md`, `HEARTBEAT.log`. |
| `Docs/Specs/Completed/content_art_urls/` | The approved move (Cesar's instruction). |

### NOT mine — pre-existing or concurrent, reported per Rule 13

| Path | Evidence |
|---|---|
| `Docs/PIPELINE_HARDENING.md`, `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt`, `Docs/Specs/Active/club_art_batches/STATUS.md` | **Pre-existing** — all four appear in the iter-1 kickoff DIRTY block in `HEARTBEAT.log`: `` M Docs/PIPELINE_HARDENING.md ``, `` M Docs/TellCode.md ``, `` M Docs/Versioning/last_uploaded_build.txt ``, `` M Docs/Specs/Active/club_art_batches/STATUS.md ``. Untouched this iteration. |
| `Assets/Resources/Clubs/Full/*-FairX.png`, `Assets/Resources/Clubs/Portraits/S_Menu_*_FAIRLOFT.png` (+ `.meta`) | **Pre-existing** — in the kickoff DIRTY block, e.g. `` ?? Assets/Resources/Clubs/Full/Driver-FairX.png `` and `` ?? Assets/Resources/Clubs/Portraits/S_Menu_Driver_FAIRLOFT.png ``. `club_art_batches` art drops. |
| `Assets/Resources/Clubs/Controls/S_Controls_*_FAIRLOFT.png`, `Assets/Resources/Clubs/Full/*-Fairloft.png` | **Concurrent, not mine.** These appeared DURING the session (mtimes 10:48:22–10:48:25, and later), after my kickoff baseline. They are `club_art_batches` art drops of the same shape as the pre-existing ones. Proof they are not the tool's: every asset the tool wrote is enumerated in its own run report, and across every acceptance run that list was exactly `Portraits/Thumbnails/Arttest.png` and `Clubs/Portraits/S_Menu_Driver_ZENITH.png` — both deleted at cleanup. The Editor is shared with Cesar's own work. |

**Fixtures: all removed.** `Assets/Data/Characters.csv`, `Assets/Data/Items.csv`,
`Assets/Data/Balls.csv`, `Assets/Resources/Data/Clubs.csv` and `Docs/Reports/content_art.txt`
are byte-identical to HEAD (`git status --porcelain` on those paths is empty). No fixture asset
survives.

---

## Deviations from the SPEC, and why

Three. All are the spec's own principle applied to data the spec did not have in front of it.

1. **Clubs derive PER FOLDER, not one `{Type}-{Brand}` for all three columns.**
   `Clubs/Controls` holds 78 files and every one is `S_Controls_{ArtType}_{BRANDTAG}`;
   `Clubs/Portraits` holds 88 and 84 are `S_Menu_{ArtType}_{BRANDTAG}`. Writing `Wedge-Fairloft`
   into `Clubs/Controls` would create the only file in that folder without the prefix — which is
   *exactly* the defect Architect correction 1 raised about `S_Char_Zoe` in
   `Portraits/Thumbnails`. So each slot follows its own folder, matching
   `Tools/club-gen/generate_clubs.py:141-143` verbatim. `{Type}-{Brand}` remains the `Clubs/Full`
   rule, which is where the spec's example (`Iron7-Mireo`) actually lives.

2. **`{Pascal(name)}-{rarity}` omits the suffix when the catalog has no `rarity` column.**
   `Balls.csv` has none (`id,name,brand,power,…`) and its two shipped names are bare
   `Pascal(name)` — `ball_putt_ace` / "Putt Ace" → `PuttAce`. One rule then reproduces BOTH
   folders exactly: `("Repair Kit","Common") → RepairKit-Common`, `("Putt Ace","") → PuttAce`.
   Written into the naming doc that way.

3. **The OLD-build half lands on rung 3, not rung 1.** The spec (Architect correction 3) says the
   old build "resolves through rule 1 (cached URL)". Mechanically it is rung **3**: rung 1 fires
   only when the overlay URL DIFFERS from the bundled one, and in this scenario the URL is
   unchanged — only the NAME was published. Both are cached-URL rungs and the outcome is
   identical (renders, not withheld). Reported rather than quietly relabelled; `HALF 1c` below
   exercises the genuine rung-1 case separately so both are covered.

Also worth flagging, and NOT changed: **the report file is shared by two tools that write it
differently.** `ContentArtValidator` rewrites `content_art.txt` whole; §6 says to append to it.
Appending alone would have meant the fetch log survived until the next `Validate Catalog Art` run
and then vanished. The validator now carries the appended section forward. That is a one-tool,
15-line change, and the alternative — a second report file — is what §6 exists to prevent.

---

## Acceptance (SPEC §7)

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | Row with URL + empty name → PNG lands in the right folder, CSV gains the name, diff is those two + `.meta` | **PASS** | Fixture `char_arttest` + the live bucket URL. Run reported `1 asset(s) added, 78.6 KB source → 53.5 KB in build`. `git status` after: `M Assets/Data/Characters.csv`, `?? Arttest.png`, `?? Arttest.png.meta` and nothing else in `Assets/Data`/`Assets/Resources`. CSV diff is one line, and the only field that changed is `portraitSprite` → `Arttest`. |
| 2 | Import settings match the sibling, verified by READING THE IMPORTER BACK, and are not the defaults | **PASS** | Read back independently of the tool's own claim: `new: type=Sprite sprite=Single max=2048 fmt=AutomaticCompressed comp=Compressed alphaIsTrans=True ppu=100` vs `ref (James.png): type=Sprite sprite=Single max=2048 fmt=AutomaticCompressed comp=Compressed alphaIsTrans=True ppu=100`. `Resources.Load<Sprite>("Portraits/Thumbnails/Arttest") != null : True`. **Non-default assertion:** this project's `m_DefaultBehaviorMode` is `0` (Mode3D), so a fresh PNG imports as `textureType=Default` and `Resources.Load<Sprite>` returns **null** on it — the tool asserts `textureType != Default`, which is the assertion that actually bites. See § "one honest caveat" below on `maxTextureSize`/`format`. |
| 3 | Re-running is a no-op | **PASS** | Second run: `noop=True fetched=0 refused=0 outcomes=0`. MD5 of `Characters.csv`, `Arttest.png`, `content_art.txt` identical before/after (`891c1bfb… / 30bd449a… / fe3ae619…` both times); `git status` diff-of-diffs empty. |
| 4 | Collision refuses, existing asset byte-identical | **PASS** | `char_JAMES` derives `James`. Refused: *"Assets/Resources/Portraits/Thumbnails/James.png already exists — a collision is never an overwrite."* `James.png` MD5 `596d962f5fba371aea9abd44bbd5ab86` before and after. The row's name column stayed empty. |
| 5 | WebP refused by extension AND by content type, with a message that says why | **PASS** | **Extension, live:** `char_webpart` at a `.webp` URL → *"WebP is refused — Unity does not import it natively… Re-upload as PNG or JPG."* No network call made. **Content type:** unreachable through the real bucket by construction (its `allowedMimeTypes` is `image/jpeg` + `image/png`, so a WebP cannot get in). Demonstrated per PIPELINE_HARDENING §20 — see § Tripwires. |
| 6 | URL outside the allowlist refused, by `CatalogArtPolicy.IsArtAllowed`, not a local copy | **PASS** | `char_evilart` at `https://evil.example.com/wrapped/catalog-art/y.png` (right substring, wrong host) → *"URL is outside the allowlist — CatalogArtPolicy.IsArtAllowed said no."* Single call site; `grep` shows no second implementation of the check. |
| 7 | An empty `Resources` folder refuses rather than guessing | **PASS** | `Balls/Full`'s two textures moved aside (`Balls/Full now has 0 texture(s)`), `ball_emptyfolder` → *"Assets/Resources/Balls/Full has no reference texture to copy import settings from."* Restored afterwards: `2 texture(s), stash folder gone = True`. |
| 8 | **The ladder hands over** — resolves via rule 2, sprite identity logged not inferred | **PASS, after a fix** | See § Ladder handover. Identity is `AssetDatabase.GetAssetPath(sprite)`: a `Resources` asset has one, a cache-decoded sprite is created at runtime and has none. |
| 9 | **The OLD build still renders it** — file stripped, name + URL kept, `HasRemote` gets it past the guard | **PASS** | See § Ladder handover, HALF 2. |
| 10 | Shared club art fetched once — six rarity rows, one download | **PASS** | Six `club_driver_zenith_*` rows, one shared URL → `1 asset(s) added … 5 row(s) share fetched art`, one file on disk (`S_Menu_Driver_ZENITH.png`, 80,500 bytes), zero collision refusals, and all six CSV rows gained the name (`grep -c` = 6). |
| 11 | Admin `URL-only · not bundled` badge (§9.2), row list + editor, EN + JA | **PASS — verified in the running app** | Cesar signed in to the mock-mode dashboard 2026-08-28; I drove the rest. Row list, editor, both languages, and the live-draft behaviour all confirmed on screen — see § Admin badge. 11/11 predicate cases, `tsc` clean, `npm run build` exit 0. |
| 12 | Size report printed and appended to `Docs/Reports/content_art.txt` | **PASS** | Pasted below, and confirmed to SURVIVE a subsequent `Validate Catalog Art` rewrite. |
| 13 | Full unfiltered EditMode sweep green | **PASS** | **1894 / 1891 passed / 0 failed / 3 pre-existing skips.** |

### One honest caveat on item 2

The spec asks for `format` + `maxTextureSize` "asserted non-default". The tool asserts them
**equal to the reference**, and reports the numbers — but it deliberately does NOT assert they
differ from Unity's defaults, because the reference art itself sits at `maxTextureSize 2048` /
format `Automatic`, which ARE the Unity defaults. An assertion that they differ would be a
statement the data cannot support, and it would fail on every correctly-configured asset in the
project. The non-default assertion is made on `textureType` instead, where it is both true and
load-bearing: `Default` is what a fresh import produces here, and `Resources.Load<Sprite>` returns
null on it. That is written into the code comment, not just here.

---

## Ladder handover (acceptance 8 + 9) — and the bug it found

Driven through the **real** `CharacterDatabaseCSV`, against the **live** bucket fixture, with the
on-disk cache **deliberately warm** — a cold cache would make every rung but 2 return null and the
assertion would pass on a loader with no ordering at all.

### Before the fix — HALF 1 failed

```
=== HALF 1 — current build: asset bundled, no overlay. Expect RULE 2. ===
  bundled asset present: True
  portraitSpriteName : 'Arttest'
  renderable         : True
  sprite             : 170x343
  AssetDatabase path : ''                       ← runtime sprite: NOT the bundled asset
  RUNG               : 1 or 3 — CACHED URL
```

Cause: `ParseCharacterFromCSV(f)` — the bundled parse — left `bundledPortraitUrl` at its `""`
default, so `CatalogArtCache.Cached(url, "")` saw `url != ""`, concluded "the overlay re-uploaded
art", and returned the cached download. Same shape in all four loaders
(`ClubDatabaseCSV` had `row.bundled?.portraitUrl ?? ""`).

### After the fix — all four cases correct

```
=== HALF 1 — CURRENT build: asset bundled, NO overlay. Expect RULE 2. ===
  AssetDatabase path : 'Assets/Resources/Portraits/Thumbnails/Arttest.png'
  RUNG               : 2 — BUNDLED sprite by name

=== HALF 1b — control: char_james (no URL at all). Expect RULE 2. ===
  AssetDatabase path : 'Assets/Resources/Portraits/Thumbnails/James.png'
  RUNG               : 2 — BUNDLED sprite by name

=== HALF 1c — GENUINE re-upload (overlay URL ≠ bundled URL). Expect RULE 1 (cached). ===
  AssetDatabase path : ''
  RUNG               : 1 or 3 — CACHED URL          ← rung 1 preserved by the fix

  [strip] MoveAsset err=''  Resources.Load(Arttest) -> False

=== HALF 2 — OLD build: asset ABSENT, name published by overlay. Must NOT be withheld. ===
  portraitSpriteName : 'Arttest'
  renderable         : True
  sprite             : 170x343
  AssetDatabase path : ''
  RUNG               : 1 or 3 — CACHED URL          ← ContentSpriteGuard let it through on HasRemote

RESTORE err='' fileBack=True tempGone=True Resources.Load -> True
```

HALF 2 is the case §8 keeps the URL for and the one most likely to regress silently: the build
receives a published sprite NAME it does not carry, `ContentSpriteGuard` does **not** veto the
overlay (because `SpriteRef.HasRemote` is true), and the row renders from the cached URL instead
of being withheld.

### Clubs verified separately — the loader that could have differed

The rule-2 fix is the same one line in all four loaders, but **clubs have a fourth rung**
(Placeholder), and `content_art_urls` spent three iterations making sure the stand-in does not
shadow the URL. So clubs were driven directly rather than assumed, cache warm, through the real
`ClubDatabaseCSV`:

| Bundled club row | Resolved via |
|---|---|
| Real bundled art (`Driver-G&F`) **+** a URL | **rung 2** — `Assets/Resources/Clubs/Portraits/Driver-G&F.png` |
| No bundled art (`NoSuchSprite_probe`) **+** a URL | **rung 3** — cached URL, runtime sprite, **NOT** rung 4 Placeholder |

Both correct. The second row is the ordering the earlier task fought for, and the fix preserves
it. Items and balls take the identical one-line change with no fourth rung and are covered by the
full sweep.

---

## Tripwires — PIPELINE_HARDENING §20

Three. Each was observed RED against the live response, then reverted and re-verified
byte-identical to a backup.

| Tripwire | Change | Observed |
|---|---|---|
| **Ladder handover guard** | `bundledPortraitUrl ?? portraitUrl` → `?? ""` (the pre-fix behaviour) in `CharacterDatabaseCSV` | Sweep **1894 / 1890 / 1 FAILED** — `ContentArtLadderHandoverTests.BundledSpriteWins_EvenWhenTheRowsOwnUrlIsCached`: *"Expected: `Assets/Resources/Portraits/Thumbnails/James.png` But was: `<string.Empty>`"*. Reverted byte-identical → **1894 / 1891 / 0**. |
| **500 KB cap** | `MaxBytes = 500 * 1024` → `1024` | Against the real 80,500-byte response: *"78.6 KB exceeds the 1 KB upload cap (contentArtMutations.ts CATALOG_ART_SPEC.maxBytes) — a file this large did not come through the admin's upload path."* Reverted byte-identical. |
| **WebP by content type** | the comparison's expected value flipped `"image/webp"` → `"image/png"` | Against the real `Content-Type: image/png` response: *"the server returned image/webp — Unity does not import WebP natively…"*. Proves the branch executes on the live response header rather than being dead code. Reverted byte-identical. |

The last two exist because the real bucket cannot produce either condition: its `fileSizeLimit`
is 500 KB and its `allowedMimeTypes` is `image/jpeg` + `image/png`. Refusing them anyway is the
point — the tool must not depend on the upload path having held.

**One more failure worth reporting**, because it was found by a test rather than by reasoning:
the first full sweep came back `1894 / 1890 / 1 FAILED` on
`Splice_KeepsATrailingCarriageReturnInsideTheLastField`. A CRLF file's trailing `\r` lives inside
the **last field's raw span**, so splicing that field would have eaten it and rewritten the line
ending of every row touched. All four CSVs are LF-only today, so it was latent, not live — but
"leave every other byte alone" is the entire premise of splicing rather than re-serialising.
`SetField` now carries the `\r` across; the test is unchanged.

---

## The size summary (SPEC §6)

Printed by the run, and appended verbatim to `Docs/Reports/content_art.txt`:

```
── fetched art (GOLFIN/Content/Fetch URL Art)
build 2374 · 1 asset(s) added, 78.6 KB source → 53.5 KB in build
   characters: 1 added, 78.6 KB source → 53.5 KB in build
     + Portraits/Thumbnails/Arttest                        78.6 KB →   53.5 KB  AutomaticCompressed 2048  (char_arttest portraitUrl)
```

With refusals and shared art (the six-club run):

```
── fetched art (GOLFIN/Content/Fetch URL Art)
build 2374 · 1 asset(s) added, 78.6 KB source → 53.5 KB in build, 5 row(s) share fetched art, 4 refused
   characters: 0 added, 0 B source → 0 B in build
     ! Portraits/Thumbnails/James       REFUSED — …already exists — a collision is never an overwrite…
     ! Portraits/Thumbnails/Evilart     REFUSED — URL is outside the allowlist — CatalogArtPolicy.IsArtAllowed said no…
     ! Portraits/Thumbnails/Webpart     REFUSED — WebP is refused — Unity does not import it natively…
   balls: 0 added, 0 B source → 0 B in build
     ! Balls/Full/EmptyFolder           REFUSED — …has no reference texture to copy import settings from…
   clubs: 1 added, 78.6 KB source → 53.5 KB in build
     + Clubs/Portraits/S_Menu_Driver_ZENITH   78.6 KB → 53.5 KB  AutomaticCompressed 2048  (club_driver_zenith_common portraitUrl)
     = Clubs/Portraits/S_Menu_Driver_ZENITH   shared  (club_driver_zenith_legendary portraitUrl)
     = Clubs/Portraits/S_Menu_Driver_ZENITH   shared  (club_driver_zenith_mythic portraitUrl)
     = Clubs/Portraits/S_Menu_Driver_ZENITH   shared  (club_driver_zenith_rare portraitUrl)
     = Clubs/Portraits/S_Menu_Driver_ZENITH   shared  (club_driver_zenith_supreme portraitUrl)
     = Clubs/Portraits/S_Menu_Driver_ZENITH   shared  (club_driver_zenith_uncommon portraitUrl)
```

**The in-build number is measured, not estimated** — `Profiler.GetRuntimeMemorySizeLong` on the
imported texture (54,784 bytes for the 170×343 fixture), which is the side of §10.2's
122 MB-source / ~50 MB-in-build ratio that actually matters.

**The shared-file contract holds.** Running `GOLFIN/Content/Validate Catalog Art` afterwards, which
rewrites the whole file:

```
BEFORE validate: fetch-log marker present = True
AFTER  validate: fetch-log marker present = True
```

---

## Admin badge (§9.2)

Predicate `urlOnlyArtColumns(catalog, row)` — 11/11:

```
PASS  characters    URL set, name empty -> BADGE                  want=[portraitUrl] got=[portraitUrl]
PASS  characters    URL set AND name set -> no badge (bundled)    want=[] got=[]
PASS  characters    name set, no URL -> no badge                  want=[] got=[]
PASS  characters    both slots url-only -> both listed            want=[portraitUrl,fullUrl] got=[portraitUrl,fullUrl]
PASS  characters    whitespace-only name counts as EMPTY          want=[portraitUrl] got=[portraitUrl]
PASS  characters    whitespace-only URL counts as ABSENT          want=[] got=[]
PASS  clubs         controlUrl is a slot too                      want=[controlUrl] got=[controlUrl]
PASS  items         thumbnailUrl slot                             want=[thumbnailUrl] got=[thumbnailUrl]
PASS  balls         fullUrl slot                                  want=[fullUrl] got=[fullUrl]
PASS  shop_catalog  non-art catalog -> never a badge              want=[] got=[]
PASS  texts         non-art catalog -> never a badge              want=[] got=[]
ALL PASS
```

Placed in the row list's **state column** (beside `OFF`; both can be true at once) and in the
**editor header**, where it reads off the live draft so uploading art shows it immediately and it
clears the moment the name column is filled. EN + JA in `i18n.ts`; the label itself is
untranslated for the same reason as `LIVE`/`SCHEDULED` (it names columns), the hover explanation
is translated.

### Verified in the running app, 2026-08-28

Cesar signed in to the mock-mode dashboard (`MOCK_MODE=1 npm run dev`, banner reading
*"MOCK DATA — running on local fixtures, no Supabase connection"*); everything below is what the
browser actually rendered.

| Check | Result |
|---|---|
| **Row list discriminates** | `mock_char_urlonly` (URL set, name empty) → `URL-only · not bundled` in the State column. `mock_char` (name set) → `—`. Two rows, same catalog, same screen. |
| **Editor placement** | Drawer header, under *"Drafts are never served to the game. Publish is the gate."* — above the fields, where the operator sees it before editing anything. |
| **Reads the LIVE draft, not the saved row** | Typed `MockUrlOnly` into `portraitSprite` → **badge vanished immediately**, nothing saved. Cleared the field → **badge came back**. Both directions, no round-trip. |
| **JA** | Label `URL のみ・未同梱` in the 状態 column. Tooltip renders in full with the column interpolated: *「portraitUrl には URL がありますが、対応するスプライト名の列が空のため…Unity で GOLFIN/Content/Fetch URL Art を実行すると Resources/ に取り込まれ、名前が入ります。」* |
| **Editor in JA** | Header reads `下書き行を編集 | mock_char_urlonly | 閉じる | 下書きはゲームに配信されません。公開が唯一のゲートです。 | URL のみ・未同梱`. |

**One correction the visual pass produced.** The component's doc comment claimed the label was
untranslated "for the same reason as LIVE/SCHEDULED". The code translates it — correctly, because
unlike `LIVE` or a rarity name this is descriptive prose about pipeline state, not a value stored
in the row, and an English label in a JA session reads as broken. The comment was wrong, not the
code; corrected. Nothing else changed.

Nothing was saved or published: the draft edits were made and reverted in the drawer, and the
fixtures are local-only.

To see it again:

```bash
cd Tools/admin-dashboard && MOCK_MODE=1 npm run dev
```

---

## Test results

| Suite | Result |
|---|---|
| **EditMode, full unfiltered sweep** | **1894 total / 1891 passed / 0 failed / 3 skipped** — the 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips, unchanged. |
| `Tools/content` pytest | **26 passed** |
| Admin dashboard `npx tsc --noEmit` | clean |
| Admin dashboard `npm run build` | exit 0 |
| Unity compile | no errors; warnings are the pre-existing project-wide `CS0618`/`CS8618` set |

Baseline before this task was 1877 tests; +17 is exactly the 14 + 3 added here.

---

## Editor state

Play mode off, no scene dirty (`ShellScene` `IsDirty:false`), no temp folders left
(`Assets/_ArtFetchTemp` and `Assets/_BallsFullStash` both created and deleted inside their
scripts, verified gone), no fixture rows in any CSV, no fixture assets in `Resources/`, the E2E's
seeded cache entry removed from `persistentDataPath`.

---

## Out of scope, untouched (SPEC §8)

Retiring bundled art; running inside the build lane; choosing art; 3D/hole content; clearing the
URL after bundling; `Characters/Homescreen` (`homeUrl`, filed as a follow-up on
`content_art_urls`). Zero edits to `Assets/Scripts/Physics/`, no `*Gate` scenarios, no
`LabScaffold.unity` changes, no `M_Splash*.mat` changes.

---

## Not committed

The tree is uncommitted at the end of this iteration, deliberately: close-out is Cesar's call and
the working tree also carries concurrent `club_art_batches` art that is not mine. Per CLAUDE.md
rule 12 the close-out commit must run `git status --porcelain --untracked-files=all` first and
stage only this task's paths.
