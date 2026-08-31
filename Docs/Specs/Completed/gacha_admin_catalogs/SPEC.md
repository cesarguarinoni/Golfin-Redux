# SPEC — `gacha_admin_catalogs`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-31 (Architect via Cowork). Spec **A** of four in `Docs/GACHA_ADMIN_PLAN.md`
> (§8). Cesar's requirement, same day: *"move Gacha management to the online admin like we did
> all the rest — banners, dates, prizes and drop rates from the admin, working without a new
> build; Unity/CSV edits inform the admin and vice versa."* Decisions of record: plan §9
> (rates = rarity table + weights; pity per banner, may be none; dupes → RP; ticket ledger from 0;
> 5b overlay + re-apply; **banner text is UI-authored, never in the artwork**).
>
> PIPELINE_HARDENING §23 applies: the dashboard half is not done until deployed, deployment id
> and footer stamp quoted. §21: the world-check here is the CSV round trip on prod, run and pasted.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Make the four gacha tables **content catalogs #17–#20** — `gacha_banners` (existing CSV,
extended), `gacha_rates`, `gacha_pools`, `ticket_types` (new CSVs) — with seed, export/import/
`--check`, three admin panels, publish validation, art upload, and the two-way loop proven by the
round-trip acceptance. **The game's behaviour does not change in this task.** The only client
edit is a parser rail (§3) so the exporter's canonical CSV cannot break the current banner loader
in the next build. Server pull (spec B) and the client overlay/real pull (spec C) come after.

## 1. What is true today (verified 2026-08-31, `47caf4bdf`)

| Piece | State |
|---|---|
| `Assets/Resources/Data/gacha_banners.csv` | 4 rows, 9 columns `bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active`; NOT in `Tools/content/catalogs.py` |
| `GachaBannerCatalog.ParseCsv` (`Assets/Scripts/UI/Gacha/GachaBannerModel.cs`) | `line.Split(',')`, positional `cols[0..8]`, rows with `< 9` columns skipped; 15 EditMode tests in `Assets/Tests/EditMode/GachaStage2Tests.cs` call `ParseCsv(string)` and `GetLiveBanners(entries, nowUtc)` via reflection |
| Banner art | bundled `Resources/Art/Gacha/Banners/GachaBanner_StandardClub1.png`, **882 × 1448** (measured) |
| Pipeline | 16 catalogs in `catalogs.py`; `seed_from_csv.py` generates the seed migration; `export_content.py --check` is a fastlane gate; `import_content.py` is the CSV → draft path |
| Admin | `CatalogPanel` (`app/(panels)/_content/catalog-panel.tsx`) with `editorExtras`, `renderCell`, `columns`; multi-catalog tabs precedent `app/(panels)/mission-components/components-panel.tsx` (`TABS` → one `CatalogPanel` per tab); `RefPicker` in `app/(panels)/shop/`; `lib/contentValidate.ts` (`REQUIRED_COLUMNS`, `NUMERIC`, `ID_COLUMN`, `validateCatalog` with `warn(...)`); `lib/contentArtMutations.ts` (`ALLOWED_CATALOGS`, `ALLOWED_COLUMNS`, `uploadCatalogArt` → bucket `catalog-art`); `PanelId` is derived from `nav.*` DICT keys in `lib/i18n.ts` |
| Rarity | six tiers Common, Uncommon, Rare, Mythic, Legendary, Supreme; `contentValidate.ts` already checks `rarity` on clubs/characters/items — reuse that constant |

## 2. CSVs — the data model (all under `Assets/Resources/Data/`)

Additive columns only (I4). Blank = "not set". Booleans `true`/`false`. Timestamps ISO-8601 UTC
with `Z`. Ids `^[a-z0-9_]+$` (the `isValidNewRowId` rule).

### 2.1 `gacha_banners.csv` — extend in place (keep the 9 existing columns and their values)

New columns, appended in this order:

| Column | Meaning |
|---|---|
| `startUtc` | window start; blank = always started. Client + server treat `startUtc ≤ now < endUtc` |
| `poolId` | `gacha_pools.poolId` this banner rolls from |
| `ticketType` | `ticket_types.id` the pull costs (`costX1`/`costX10` are in this ticket) |
| `pityThreshold` | pulls without ≥ `pityMinRarity` before the next pull is forced ≥ it. **Blank or `0` = no pity** |
| `pityMinRarity` | rarity the pity guarantees; required iff `pityThreshold > 0` |
| `guaranteeMinRarityX10` | one slot of every x10 is ≥ this rarity; blank = no guarantee |
| `maxPullsPerPlayer` | lifetime per-player pull cap on this banner; blank = unlimited |
| `artUrl` | admin-uploaded art (one image, **no text in it**); `artSprite` stays the bundled floor |
| `nameEn`, `nameJa` | card title per locale (UI-rendered). `nameKey` stays as fallback |
| `taglineEn`, `taglineJa` | optional second line; whether the card renders it is spec C's call |
| `featuredRefIds` | `;`-separated refs the card / rates screen highlights (display only) |

Row values after the edit (the four existing rows; `…` = the 9 existing columns unchanged):

```
…,startUtc,poolId,ticketType,pityThreshold,pityMinRarity,guaranteeMinRarityX10,maxPullsPerPlayer,artUrl,nameEn,nameJa,taglineEn,taglineJa,featuredRefIds
banner_standard_club1 …,2026-01-01T00:00:00Z,pool_standard_club1,0,50,Legendary,Rare,,,STANDARD CLUB 1,スタンダードクラブ 1,,,club_pwedge_royal;club_putter_golfinx
banner_test_a         …,2026-01-01T00:00:00Z,pool_standard_club1,0,,,,,,TEST BANNER A,テストバナー A,,,
banner_test_b         …,2026-01-01T00:00:00Z,pool_standard_club1,0,30,Rare,Uncommon,,,TEST BANNER B,テストバナー B,,,club_awedge_fyloe
banner_inactive       …,2026-01-01T00:00:00Z,pool_standard_club1,0,,,,,,INACTIVE BANNER,非アクティブバナー,,,
```

`banner_test_a` deliberately has **no pity** — it is the acceptance case for decision 2.

### 2.2 `gacha_rates.csv` — NEW, id `id`

```
id,poolId,rarity,rateBp
pool_standard_club1_common,pool_standard_club1,Common,5500
pool_standard_club1_uncommon,pool_standard_club1,Uncommon,2500
pool_standard_club1_rare,pool_standard_club1,Rare,1200
pool_standard_club1_mythic,pool_standard_club1,Mythic,550
pool_standard_club1_legendary,pool_standard_club1,Legendary,200
pool_standard_club1_supreme,pool_standard_club1,Supreme,50
```

Basis points; **sum per pool = 10 000**; exactly one row per (pool, rarity). Starting numbers,
not decisions — Cesar tunes them in the admin.

### 2.3 `gacha_pools.csv` — NEW, id `id`

```
id,poolId,kind,refId,rarity,weight,quantity,dupeRp,featured
psc1_driver_gf,pool_standard_club1,club,club_driver_gf,Common,100,1,20,false
psc1_wood_gf,pool_standard_club1,club,club_wood_gf,Common,100,1,20,false
psc1_ball_golfin,pool_standard_club1,ball,ball_golfin,Common,60,3,0,false
psc1_iron9_klyro,pool_standard_club1,club,club_iron9_klyro,Uncommon,100,1,40,false
psc1_repairkit_common,pool_standard_club1,item,repairkit_common,Common,40,1,0,false
psc1_iron7_mireo,pool_standard_club1,club,club_iron7_mireo,Rare,100,1,80,false
psc1_repairkit_rare,pool_standard_club1,item,repairkit_rare,Rare,40,1,0,false
psc1_awedge_fyloe,pool_standard_club1,club,club_awedge_fyloe,Mythic,100,1,160,false
psc1_repairkit_mythic,pool_standard_club1,item,repairkit_mythic,Mythic,30,1,0,false
psc1_pwedge_royal,pool_standard_club1,club,club_pwedge_royal,Legendary,100,1,300,true
psc1_putter_golfinx,pool_standard_club1,club,club_putter_golfinx,Supreme,100,1,600,true
```

`kind ∈ club|ball|character|item|ticket`. `rarity` is **always filled** on the entry: for
`club|character|item` it must EQUAL the referenced row's `rarity` (blocking — the editor auto-fills
it from the picked ref and locks it); for `ball|ticket`, which have no rarity, it is the
operator's choice. Within a rarity the roll is weighted by `weight`. `quantity` for
balls/items/tickets (clubs/characters are 1). `dupeRp` = RP paid instead of an already-owned
club/character (plan §5 step 7). The rarities were read from `Clubs.csv` / `Items.csv` on
2026-08-31 — the validator re-checks them, do not trust this table over the catalog.

### 2.4 `ticket_types.csv` — NEW, id `id`

```
id,key,nameEn,nameJa,iconSprite,iconUrl
0,standard,Ticket,チケット,,
1,gold,Gold Ticket,ゴールドチケット,,
```

`id` is the integer persisted in saves as `ticketTypeInt` (`TicketType.Standard = 0`); `1` is the
`GoldTicket` / `ticket_gold` the missions and tournaments already name. `iconSprite` blank until
art exists (spec C decides the bundled icon; NOTE: the current card's ticket icon is authored in
the prefab — name it in spec C, not here). Never renumber; append only.

## 3. Client — one rail, no behaviour change

`GachaBannerCatalog.ParseCsv` (`GachaBannerModel.cs`) becomes **header-indexed and quote-aware**:
parse line 0 into a `column → index` map, parse each row with a quote-aware splitter (copy
`ModesDatabaseCSV.ParseCsvLine`, or lift it to a shared helper if one already exists — NOTE: check
`ContentFields` / `Golfin.Content` for a public splitter before adding a third copy), read the nine
existing fields **by name**, ignore unknown columns, and skip a row only when `bannerId` is blank.
Reason: `export_content.py` writes QUOTE_MINIMAL canonical form; a `taglineEn` containing a comma
would shift every column after it under `Split(',')`, and the bundled floor of the next build is
whatever the exporter wrote. The new columns are NOT read in this task (spec C adds them to
`GachaBannerEntry`).

Tests: the 15 existing `GachaStage2Tests` pass **unmodified** (they feed 9-column CSV strings —
header-indexed parsing must accept them byte-for-byte). Add: a row with a quoted comma-bearing
field parses to the same entry; a CSV with the 22-column header parses the four seed rows with
identical `BannerId/CostX1/CostX10/EndUtc/Active`; a row missing `bannerId` is skipped.

Nothing else in `Assets/` changes. `GachaBannerCard`, `GachaCarouselController`, `GachaPullFlow`,
the mock pool and the history mock are untouched.

## 4. Pipeline

- `Tools/content/catalogs.py` `CATALOGS` += four entries, in this order, with a header comment
  in the file's established style (why they exist, which the server will read — spec B reads
  ALL FOUR from `content_rows` directly, no mirror):
  ```python
  Catalog("gacha_banners", "Assets/Resources/Data/gacha_banners.csv", "bannerId"),
  Catalog("gacha_rates",   "Assets/Resources/Data/gacha_rates.csv",   "id"),
  Catalog("gacha_pools",   "Assets/Resources/Data/gacha_pools.csv",   "id"),
  Catalog("ticket_types",  "Assets/Resources/Data/ticket_types.csv",  "id"),
  ```
  Update the docstring's CSV-facts list (row counts).
- Seed: `python3 Tools/content/seed_from_csv.py --catalogs gacha_banners gacha_rates gacha_pools ticket_types`
  → `playlife/backend/migrations/2026_08_31_content_gacha_seed.sql` (+ copy under
  `Tools/admin-dashboard/migrations/`). **Full SQL pasted in chat for Cesar** (WORKFLOW_NOTES
  rule); he applies it; verify over PostgREST (row counts per catalog) before the panels are
  deployed.
- Round trip (the standing acceptance): after the seed, `export_content.py` leaves all four CSVs
  **byte-identical** and `--check` is clean. `Tools/content/tests/` gains the four catalogs in
  whatever table-driven test enumerates catalogs; `python3 -m unittest discover Tools/content/tests`
  green.
- `Tools/content/README.md`, `Docs/TESTFLIGHT_RUNBOOK.md` catalog lists: 16 → 20.

## 5. Admin dashboard (`Tools/admin-dashboard`)

Read `Docs/ADMIN_DASHBOARD_OPS.md` §2–§4 first. All strings in `lib/i18n.ts` `DICT`, **en + ja**;
no player strings here.

### 5.1 Registration

- `lib/i18n.ts`: `nav.gacha-banners` ("Gacha Banners" / "ガチャバナー"), `nav.gacha-pools`
  ("Gacha Pools" / "ガチャ排出プール"), `nav.ticket-types` ("Ticket Types" / "チケット種別") — the
  `PanelId` union derives from these.
- `lib/registry.ts`: three `PanelDef`s after Shop, routes `/gacha-banners`, `/gacha-pools`,
  `/ticket-types`. Add a `"ticket"` entry to the `PanelIcon` union + `components/PanelIcon.tsx`
  (24×24 stroke ticket outline, existing style) and use it for all three.
- `app/(panels)/gacha-banners/{page,gacha-banners-panel}.tsx`,
  `app/(panels)/gacha-pools/{page,gacha-pools-panel}.tsx` (TABS `Pools | Rates`, the
  `components-panel.tsx` pattern — one `CatalogPanel` per tab, `key={catalog}`),
  `app/(panels)/ticket-types/{page,ticket-types-panel}.tsx`. `page.tsx` files mirror
  `app/(panels)/modes/page.tsx` verbatim in shape (`force-dynamic`, `metadata.title`).
- `lib/contentValidate.ts`:
  ```ts
  REQUIRED_COLUMNS.gacha_banners = ["bannerId","nameKey","artSprite","costX1","costX10","endUtc","sortOrder","active","poolId","ticketType"]
  REQUIRED_COLUMNS.gacha_rates   = ["id","poolId","rarity","rateBp"]
  REQUIRED_COLUMNS.gacha_pools   = ["id","poolId","kind","refId","rarity","weight","quantity"]
  REQUIRED_COLUMNS.ticket_types  = ["id","key","nameEn","nameJa"]
  NUMERIC.gacha_banners = ["costX1","costX10","sortOrder","pityThreshold","maxPullsPerPlayer"]
  NUMERIC.gacha_rates   = ["rateBp"];  NUMERIC.gacha_pools = ["weight","quantity","dupeRp"];  NUMERIC.ticket_types = ["id"]
  ID_COLUMN: gacha_banners "bannerId", gacha_rates "id", gacha_pools "id", ticket_types "id"
  ```
  `lib/contentView.ts` `CATALOG_VIEWS`: list columns per catalog (banners: bannerId, nameEn, state
  badge, poolId, ticketType, costX1, costX10, startUtc, endUtc, pityThreshold, sortOrder, active).
- Mock mode (`lib/mockContent.ts` / fixtures): the four catalogs with the §2 rows so every panel
  is exercisable with `MOCK_MODE=1`.

### 5.2 Gacha Banners panel

`CatalogPanel catalog="gacha_banners"` with:

- `renderCell` **state badge** (untranslated, Tournaments style): `OFF` if `active=false` or
  the row is `is_active=false`; else `SCHEDULED` if `startUtc > now`; `ENDED` if `endUtc ≤ now`;
  else `LIVE`. Server clock (the page is server-rendered; pass `now` from the server component).
- `editorExtras`: **pool picker** (select over distinct `poolId` in `gacha_pools` drafts+published),
  **ticket type picker** (select over `ticket_types` rows, label `nameEn (id)`), **rarity selects**
  for `pityMinRarity` / `guaranteeMinRarityX10` (six tiers + blank), **art upload** for `artUrl`
  (below), and the per-locale text fields grouped as *Title EN / JA*, *Tagline EN / JA* with the
  hint (en + ja): *"Rendered by the card as UI text. Do not bake any text into the artwork."*
  `editorHiddenColumns` for everything the extras render.
- Amber banner (the shop's component): *"Banners are catalog rows: publish makes them the next
  build's floor and the overlay for installed builds (spec C). Pulls still run on the mock until
  `gacha_server_pull` ships."* en + ja.

**Art upload.** `lib/contentArtMutations.ts`: `ALLOWED_CATALOGS` += `"gacha_banners"`,
`ALLOWED_COLUMNS` += `"artUrl"`. Object naming and hashing unchanged
(`gacha_banners-<bannerId>-artUrl-<hash12>.<ext>`), bucket `catalog-art`, same MIME list and
byte cap as the other catalog art (NOTE: quote the cap from the file in the report). Target
dimensions **882 × 1448** (the bundled banner, measured 2026-08-31 — re-measure rather than
trust) with the existing drift → amber, never a block. The editor previews the uploaded image at
card aspect.

### 5.3 Gacha Pools panel — tabs `Pools` | `Rates`

**Pools tab** (`catalog="gacha_pools"`), grouped visually by `poolId` (sort by poolId, rarity
order, then weight desc; a subtle group header row per pool is fine):

- `editorExtras`: **kind select** → **`RefPicker`** against the catalog for that kind
  (`club→clubs, ball→balls, character→characters, item→items, ticket→ticket_types` — extend
  `SHOP_CATEGORY_TO_CATALOG` or add a sibling map, do not fork the picker) with the shop's
  **resolved preview** (name, rarity, thumbnail); on pick, **`rarity` auto-fills from the ref and is
  read-only** for club/character/item, editable for ball/ticket; `rowId` prefilled
  `<poolId>_<refId>` on a new row; `featured` as a checkbox.
- Above the table, per pool: an **Effective odds** table — for each entry
  `rate(rarity)/10000 × weight / Σ weight(pool, rarity)` — and a **`Simulate 10 000 pulls`**
  button: pure TS in `lib/gachaOdds.ts` (`effectiveOdds(rates, pool)`, `simulate(rates, pool,
  banner, n, seed)` with a seeded PRNG — mulberry32 is fine), rolling exactly the way spec B will
  (pity/guarantee first, then rarity by `rateBp`, then weight). Output: rarity distribution
  observed vs published, pity hits, guarantee hits. This function is the reference the server
  function is checked against in spec B — keep it pure and unit-tested.

**Rates tab** (`catalog="gacha_rates"`): the standard table plus a per-pool **sum indicator**
(`Σ = 10 000 ✓` / `Σ = 9 850 ✗`) rendered above the rows, live from drafts.

### 5.4 Ticket Types panel

Plain `CatalogPanel catalog="ticket_types"`; hint on `id`: *"Integer persisted in player saves.
Never renumber; append only."* Art columns are NOT registered for upload in this task (icons are
spec C's).

### 5.5 Publish validation (`validateCatalog`, blocking unless marked warn)

Context: `ctx.otherCatalogs` must carry `gacha_pools`, `gacha_rates`, `ticket_types`, and the
five ref catalogs for cross-checks (extend what it loads if needed).

`gacha_rates`
1. `rarity` ∈ six tiers; `rateBp` integer `0…10000`.
2. Per `poolId`: exactly one active row per rarity (missing → error naming the rarity; duplicate
   → error).
3. Per `poolId`: Σ `rateBp` over active rows = **10 000** (error with the actual sum).
4. `poolId` has ≥ 1 active `gacha_pools` entry (a rate table for an empty pool → error).

`gacha_pools`
5. `kind` ∈ `club|ball|character|item|ticket`; `refId` resolves in the catalog for `kind` **and that
   row is `is_active`**; for `ticket`, `refId` is a `ticket_types.id`.
6. `rarity` ∈ six tiers; for `club|character|item` it **equals the ref row's `rarity`** (error
   naming both).
7. `weight ≥ 1`; `quantity ≥ 1`; `dupeRp ≥ 0` (blank = 0); `featured` parses as bool.
8. `min_build ≥` the ref row's `min_build` (shop G2, verbatim).
9. **Reachability**: for the entry's pool, if `gacha_rates` gives this rarity `rateBp = 0` →
   **warn** "unreachable"; and for every (pool, rarity) with `rateBp > 0` there must be ≥ 1 active
   entry → **error** (a roll that lands on nothing). Run this check from BOTH catalogs' publish so
   neither publish order can leave the pair inconsistent.

`gacha_banners`
10. `poolId` resolves to a pool with a complete rate table (rules 2–3 satisfied); `ticketType`
    resolves to an active `ticket_types` row.
11. `costX1 ≥ 0`, `costX10 ≥ 0`; `costX10 > 10 × costX1` → **warn** ("a x10 that costs more
    than ten x1s"). A discount is normal; a premium is probably a typo.
12. `startUtc`/`endUtc` parse as ISO-8601 UTC; `endUtc > startUtc` when both set.
13. `pityThreshold` blank or `0` ⇒ `pityMinRarity` must be blank (warn if not: "ignored");
    `pityThreshold > 0` ⇒ `pityMinRarity` required and ∈ tiers with `rateBp > 0` in the pool.
    `guaranteeMinRarityX10`, when set, ∈ tiers with `rateBp > 0` in the pool.
14. `maxPullsPerPlayer` blank or ≥ 1.
15. A row that is LIVE (rule §5.2 badge) must have `nameEn` AND `nameJa` (the texts rule-5
    analogue) and `artSprite` OR `artUrl`.
16. `artUrl`, when set, passes the existing catalog-art URL validator (bucket `catalog-art`).
17. `sortOrder` unique among `active=true` rows → **warn**.
18. `featuredRefIds`: each `;`-token resolves in some pool entry of the banner's pool → warn.

`ticket_types`
19. `id` integer ≥ 0, unique; `key` `^[a-z0-9_]+$` unique; `nameEn`, `nameJa` non-empty.
20. Deactivating a `ticket_types` row referenced by an active banner → error.

Rules 1–20 get vitest coverage in the dashboard suite (positive + negative per rule; the suite
already runs in `cf-deploy.sh`). `simulate`/`effectiveOdds` get their own tests (sum of effective
odds per pool = 1 ± 1e-9; with a fixed seed the distribution is deterministic; pity forces the
rarity at the threshold).

### 5.6 Audit, deploy

- Creation/edit/publish ride the existing `content_row_create` / edit / publish audit actions —
  nothing new. Art upload rides the existing catalog-art upload action.
- `npm run deploy` → Cloudflare deployment id + footer stamp = HEAD quoted in the report (§23).
  Post-deploy: `curl -s -o /dev/null -w "%{http_code}\n" https://admin.golfin.world/` → 302.

## 6. Sequencing

1. §2 CSVs + §3 parser rail + §4 `catalogs.py` → EditMode sweep + `Tools/content` tests green.
2. Seed migration generated → **SQL in chat** → Cesar applies → PostgREST row counts verified →
   `export_content.py` byte-identical, `--check` clean (paste the output).
3. §5 panels + validation + art upload + vitest → `npm run build` → `npm run deploy` → §23 proofs.
4. World-check (§21): edit `costX10` of `banner_test_b` in the CSV → `import_content.py --apply`
   → the Gacha Banners publish drawer shows exactly that one change → publish → export →
   byte-identical → `--check` clean. Then the reverse: change `rateBp` in the admin (keep the sum)
   → publish → export → the CSV carries it. Paste both.
5. Docs: `Tools/content/README.md`, `TESTFLIGHT_RUNBOOK.md`, `ADMIN_DASHBOARD_OPS.md` panel list,
   `AI_CONTEXT.md`, `TellCode.md` CURRENT STATE. Hand the file list to Code's commit (Cowork does
   not commit).

## 7. Acceptance (Implementer fills `IMPLEMENTER_REPORT.md`, PASS/FAIL with what was measured)

- [ ] Four catalogs seeded on prod; `export_content.py` byte-identical for all four; `--check`
      clean; `Tools/content` tests green with 20 catalogs.
- [ ] Both world-check round trips (§6 step 4) run on prod and pasted.
- [ ] Gacha Banners panel: badges LIVE / SCHEDULED / ENDED / OFF correct for the four seed rows
      (`banner_inactive` = OFF; `banner_test_b` ENDED after 2026-11-30 — simulate by editing
      `endUtc` in a draft and reading the badge); pool/ticket/rarity pickers; art upload lands in
      `catalog-art` under the hashed name and re-upload of the same bytes yields the same URL; a
      text-in-artwork hint visible en + ja.
- [ ] Gacha Pools panel: picking `club_iron7_mireo` auto-fills `rarity = Rare` and locks it; picking
      a ball leaves it editable; effective-odds table sums to 100 % per pool; Simulate shows a
      distribution within ±1.5 pt of published at 10 000 pulls with the default seed, and pity hits
      > 0 for `banner_standard_club1`, = 0 for `banner_test_a`.
- [ ] Validation: each of rules 1–20 has a failing fixture that is refused (or warned) with the
      named message — quote the vitest run. Specifically: rates summing to 9 850 → refused; a
      Legendary rate with no Legendary entry → refused; `pityThreshold = 0` with a `pityMinRarity`
      → warn only; `rarity = Common` on `club_iron7_mireo` → refused naming `Rare`.
- [ ] `GachaStage2Tests` (15) pass unmodified; the three new parser tests pass; Rewards Center in
      the Editor shows the same three live banners as before this task (screenshot).
- [ ] Zero new hardcoded player strings (grep quoted); all DICT keys have en + ja.
- [ ] Deployment id + footer stamp quoted; Access curl → 302.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files this task touches

**New**
- `Assets/Resources/Data/{gacha_rates,gacha_pools,ticket_types}.csv` (+ `.meta`)
- `playlife/backend/migrations/2026_08_31_content_gacha_seed.sql` (+ dashboard copy)
- `Tools/admin-dashboard/app/(panels)/gacha-banners/*`, `gacha-pools/*`, `ticket-types/*`
- `Tools/admin-dashboard/lib/gachaOdds.ts` (+ tests), validator tests

**Modified**
- `Assets/Resources/Data/gacha_banners.csv` — 13 new columns
- `Assets/Scripts/UI/Gacha/GachaBannerModel.cs` — header-indexed, quote-aware `ParseCsv`
- `Assets/Tests/EditMode/GachaStage2Tests.cs` — three added tests only
- `Tools/content/catalogs.py`, `Tools/content/tests/*`, `Tools/content/README.md`
- `Tools/admin-dashboard/lib/{i18n,registry,contentValidate,contentView,contentArtMutations,mockContent}.ts`,
  `components/PanelIcon.tsx`
- `Docs/TESTFLIGHT_RUNBOOK.md`, `Docs/ADMIN_DASHBOARD_OPS.md`, `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`

## Out of scope (do NOT do these)

- Anything the game does with the new columns — overlay, withhold rule, art-by-URL ladder,
  `startUtc`, numeric costs on the card, loc titles, the 5b re-apply (**spec C**).
- The pull, ticket ledger, pity state, pull log, pause switch, ops panel, `category = ticket`
  shop rows (**spec B**).
- Ticket-type icons, in-app rates modal, telemetry (**spec D**).
- Mission / tournament ticket grants (later quick task).
- Deleting `GachaMockPrizePool` / `GachaHistoryStore` mocks; removing the dev ticket grant.
