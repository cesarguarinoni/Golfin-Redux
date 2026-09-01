# Quick — `club_full_art_repoint` (2026-09-01) · DONE

**Ask (Cesar):** "Fix the 331 unbundled club full-art sprites", then "Check again against web admin
so nothing is broken".

## First: my "331" was wrong — the real number was 205

I reported 331 using a **case-sensitive** filesystem check. `Resources.Load` is case-INsensitive, so
15 of the 38 names resolved fine all along (`Bogeyb`→`BogeyB`, `Fairx`→`FairX`, `Vbooot`→`VBOOOT`).
Re-measured through `Resources.Load` itself: **205 refs across 23 distinct names**.

## And it was not missing art — it was a naming bug

Every one of the 23 has real, bundled 537×900 art under a different spelling. The `portraitFull`
column was generated from the BRAND NAME (`Fairway THREADS` → `FairwayThreads`) while the art files
— and the two sibling columns that work, `portraitSprite` and `controlSprite` — use the short brand
token. Zero art was missing; nothing was invented.

| CSV said (broken) | actual art | why |
|---|---|---|
| `*-FairwayThreads` (5 types) | `*-Fairway` | brand token is `FAIRWAY` |
| `*-Golfinix` (5) | `*-GolfinX` | brand token is `GOLFINIX`, art drops the second `i` |
| `*-TeepitWndrwll` (5) | `*-TeePit` | brand token is `TEEPIT` |
| `Iron/Wood/Wedge/Putter-G&F` (4) | `*-GandF` | ampersand spelled out (`Driver-G&F` is the odd one that kept `&`) |
| `Iron-Klyro` | `Iron9-Klyro` | only an Iron9 variant exists |
| `Iron-Mireo` | `Iron7-Mireo` | only an Iron7 variant exists |
| `Wedge-Fyloe` | `WedgeA-Fyloe` | only an A-wedge variant exists |
| `Wedge-RoyalSwing` | `WedgeP-RoyalSwing` | only a P-wedge variant exists |

All 23 targets were loaded through `Resources.Load` **before** any data was touched: 23/23, all
537×900, and none of the old names resolved.

## Not visible breakage

`ClubDetailPanel.cs:190` already falls back to `portraitSprite` when `portraitFull` is null, so
these rows showed the portrait rather than an empty box. This was data correctness, not a white box
— unlike the `Bar` sprites in `broken_sprite_refs`.

## Fix

`Assets/Resources/Data/Clubs.csv`, `portraitFull` column only. Column-aware guard: **205 fields
changed, all in `portraitFull`**, row count 799 unchanged, 3 leading comment lines preserved, raw
diff exactly 205 lines.

**Publishing was required, not optional.** The published catalog is an OVERLAY that overrides the
bundled CSV at runtime (`ClubDatabaseCSV` merges it), so a CSV-only fix would have been overridden
by the broken published values.

The importer reported **799 changed**, not 205. Investigated before applying: the only VALUE
difference across all 799 rows is `portraitFull` on exactly my 205; the other 794 count as "changed"
because the CSV carries three columns (`portraitUrl`, `fullUrl`, `controlUrl`) that the drafts —
and the published rows — lack as keys entirely. All three are empty, so adding them is inert.

Applied → publish gate re-run against the live drafts (799 rows, **0 errors, 0 warnings**,
`min_build` all 0, untouched) → published **clubs v1 → v2** → `export_content.py --check` clean.

**Result: every club sprite column resolves 799/799** (`portraitFull` was 594/799).
Visual: `Docs/Diagnostics/_capture/club_full_art_repointed.png` — 12 of the repointed clubs, all
real type-appropriate art.

## Web-admin re-check — and it caught a regression of MINE

| check | result |
|---|---|
| every sprite name the admin publishes, vs what the build bundles | **2469 / 2469 resolve, 0 missing** |
| all 20 catalogs, published rows, through the admin's own `validateCatalog` | 2136 rows, 5 warnings, 0 errors *(after the fix below)* |
| same, against drafts | identical |
| unpublished dirty rows across all catalogs | 1, pre-existing (see below) |
| `npm test` / `tsc --noEmit` | **246 pass** / exit 0 |
| `export_content.py --check` for `clubs` / `balls` | clean |

**REGRESSION I INTRODUCED AND FIXED.** `ball_data_wiring` made a blank `rarity` fail validation, but
scoped the carve-out to `shop_catalog` **by name**. That also broke `mission_loadouts`, whose 4 of 13
rows legitimately carry a blank rarity (there it is an optional filter on a club loadout, not a
required tier) — 21 validation errors, i.e. that catalog could no longer be published. My earlier
audit checked 8 catalogs and never looked at `mission_loadouts`.

Fixed properly: the blank rule now keys off `REQUIRED`, which is what was actually meant —
`const blankAllowed = !(REQUIRED[catalog] ?? []).includes("rarity")`. Rarity is REQUIRED in clubs,
characters, items, bags, balls, gacha_rates and gacha_pools (all **0** blank rows) and optional in
shop_catalog (7 of 8 blank) and mission_loadouts (4 of 13). No list to maintain. Regression test
added: `contentValidate.test.ts` → *"still ALLOWS a blank rarity where the column is optional"*.

## Pre-existing, NOT mine, NOT fixed

Proven pre-existing: each cross-references a column this session never wrote — the clubs diff
touched `portraitFull` and nothing else (`type`/`rarity`/`isActive` all unchanged), and each error
appears identically in published AND drafts.

1. **`mission_loadouts` — 17 errors.** Loadout `SUP_FULL` demands club types `Iron7` and `Iron9`;
   the clubs catalog only has `Driver, Wood, Iron, A.Wedge, P.Wedge, S.Wedge, Putter`. Same
   naming-family inconsistency as the bug fixed here, one level up. **This catalog cannot currently
   be published.**
2. **`gacha_pools` — 1 error.** `psc1_ball_golfin` points at the default ball. The row is already
   `isActive=false` (the operator deactivated it, per CLAUDE.md), but this rule has no
   deactivated-row carve-out the way the ticket rules do. **This catalog cannot currently be
   published either.**
3. **`shop_catalog` — 1 unpublished dirty row.** `shop_ticket_standard_50`, published `isActive=false`
   / draft `true` — the ticket row `gacha_ops_polish` left OFF SALE (commit `8c2c34d1e`), with a
   pending draft to re-enable.
4. **`texts` drift — 82 rows.** `SCORE_*` / `SU_*` / `GPS_*` from the in-flight `score_upload_flow`
   session. Zero `BALL_INFO_*` among them.
