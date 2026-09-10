# SPEC — `weekly_rotation_admin`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-09-08 (Architect via Cowork) from Cesar's requirement the same day: *"a weekly
> rotation of clubs/balls/characters in the shop and gacha, including admin controls where
> missing."* Plan context: `Docs/Economy/MONETIZATION_PLAN.md` §1.2 (project mirror
> `claude/MONETIZATION_PLAN.md`). Everything in §1 was read from `GolfinRedux` (`0b8bce61a`)
> and `playlife` on 2026-09-08.
>
> Companion: `weekly_rotation_client` (badges, countdown, mid-session reload) — starts AFTER
> this one has published its first rotation, because the client work needs live rows to test.
>
> Standing rules: RP only — nothing here is paid (`ECONOMY_MASTER` §2); a client missing
> information never shows a broken item and never wrongly spends; CSV edits through the
> importer; dashboard strings in `DICT` EN + JA; §23 deployment proofs; no device pass.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Every Monday 00:00 UTC (09:00 JST) the STORE tab shows a fresh **Weekly Lineup** — a
rarity-quota'd subset of the 799 clubs, the 20 balls and the locked characters — and the GACHA
tab shows a **Weekly Featured** banner whose featured clubs are rate-up within their rarity.
The week is authored **in the admin as one unit**, generated deterministically, previewed,
published through the normal drawer, and needs **no build**. Nothing leaves the RP economy:
prices come from the rarity ladder, everything that rotates out rotates back in later, and
Supreme stays earn-only unless Cesar flips the quota.

Today all of this is *possible by hand* (shop rows and banners already carry windows, the
server already prices and rolls on its clock) and *impractical* (12–15 rows + a banner + a
pool + rates, five catalogs, every week, by typing). This task adds the authoring unit, the
generator, the one-click publish, and the two server rules that make rotation safe.

---

## 1. What is true today (read 2026-09-08)

| Piece | State |
|---|---|
| `shop_catalog` | 5 rows; `entryId,category,refId,rpCost,saleRpCost,sortOrder,popular,offer,rarity,startAt,endAt,saleStartAt,saleEndAt,quantity,is_active`. Windows honoured on the client (`ContentShopWindow.Evaluate`, evaluated ONCE at load) and on the server (`golfin_shop_purchase` step 4, server clock). `popular` / `offer` unused (curation chips grayed) |
| `gacha_banners` | `startUtc/endUtc` windows, `poolId`, `featuredRefIds` (display only — no rate-up), one pity per banner keyed `(user_id, banner_id)` in `golfin_gacha_pity` |
| `gacha_pools` / `gacha_rates` | per-pool entries with `weight` + `featured` flag (display); rates in bp per pool, validator requires every rarity with rate > 0 to have ≥ 1 active entry |
| Admin | `CatalogPanel` (`+ New row`, drafts → Review & publish per catalog, `editorExtras`), `upsertDraftRow(adminEmail, catalog, {rowId, data, minBuild, isActive, expectNew})`, `publishCatalog`, `validateCatalog` with `ctx.otherCatalogs`; `lib/gachaOdds.ts` exports `mulberry32(seed)`, `effectiveOdds`, `simulate`; Daily Missions panel = the precedent for a **deterministic generator with PREVIEW + PIN** (`daily-panel.tsx` header comment) |
| Prices | `ECONOMY_MASTER` §3 ladder — clubs Common 100 / Uncommon 200 / Rare 400 / Mythic 800 / Legendary 1,500 / Supreme 3,000; characters 200 / 400 / 800 / 1,600 / 3,000 / 6,000; **no ball ladder exists** (§3.3 proposes one) |
| Catalog counts | clubs 799 (133–134 per rarity; 114–115 per type over 7 types); balls 20 (Common 5 / Uncommon 6 / Rare 5 / Mythic 3 / Legendary 1; `ball_golfin` is default-infinite, never listed — validator + server already refuse it); characters 12 in `Characters.csv` (the generator reads the catalog, so the count is not a constant anywhere) |
| `catalogs.py` | 20 catalogs; new catalogs are seeded by migration via `seed_from_csv.py`, then must round-trip byte-identical |
| Build gates | `SHOP_CATEGORY_STRICT_BUILD = 2350`, `TICKET_SHOP_BUILD = 2534` (`lib/buildGates.ts`) — G1/G1-T/G2 rules in `contentValidate.ts` |

---

## 2. Design in one paragraph

A **rotation** is a row in a new `rotations` catalog: a window, quotas, a seed. The
**generator** (pure TS in `lib/rotation.ts`, seeded by the rotation id) turns that row into
**draft rows in the existing catalogs** — shop rows with the rotation's window, one gacha
banner + one pool + its rates — tagged with a new additive `rotationId` column. Publishing is the
normal drawer, five catalogs in a fixed order, behind one button. The spend paths do not learn
anything new: the server already refuses out-of-window shop rows and banners on its clock. The
only server change is **pity that survives the week** (`pityGroup`). No new endpoint, no new
runtime table, no live table — a rotation is content.

---

## 3. Catalog changes

### 3.1 New catalog #21 — `rotations` (`Assets/Resources/Data/rotations.csv`, id `rotationId`)

| Column | Meaning |
|---|---|
| `rotationId` | `wk_<ISO-year>_<ISO-week>`, e.g. `wk_2026_38` |
| `startUtc`, `endUtc` | the week; `endUtc` exclusive. Generator default Mon 00:00 UTC → next Mon 00:00 UTC |
| `nameEn`, `nameJa` | lineup title shown by the client (`weekly_rotation_client`); default `WEEKLY LINEUP` / `今週のラインナップ` |
| `clubQuota` | `Common:3;Uncommon:2;Rare:2;Mythic:1;Legendary:1;Supreme:0` — count per rarity |
| `ballQuota` | `Common:1;Uncommon:1;Rare:1;Mythic:0;Legendary:0` |
| `characterQuota` | `Any:1` — one locked character per week, any rarity (rarity keys also accepted) |
| `gachaFeaturedCount` | `2` — featured clubs on the weekly banner, drawn from the week's shop clubs, highest rarities first |
| `gachaBasePoolId` | pool the weekly pool is cloned from (`pool_standard_club1`) |
| `featuredWeightMul` | `3` — weight multiplier for featured entries inside their rarity (rate-up) |
| `excludeWeeks` | `4` — a ref featured in the last N rotations is not eligible |
| `seed` | integer; default = FNV-1a of `rotationId`; **changing it and regenerating gives a different lineup** (the PIN/regenerate control) |
| `pinnedClubs`, `pinnedBalls`, `pinnedCharacter`, `pinnedFeatured` | `;`-separated refIds. **Pins win:** when present the generator uses them verbatim for that bucket and only fills what is blank from the quotas (the Daily-Missions PIN idea, per week). This is how the year plan (§3.1a) is data, not code |
| `materializedAt` | ISO timestamp set by the generator when rows were written; blank = not generated |
| `is_active` | standard |

Seed CSV: **the 52 rows in `reference/rotations_seed.csv`** (wk_2026_38 → wk_2027_37, every week
pinned — the year plan, §3.1a); copy it to `Assets/Resources/Data/rotations.csv` verbatim,
`materializedAt` blank on every row. Migration `2026_09_xx_content_rotations_seed.sql`
via `seed_from_csv.py` (full SQL in chat for Cesar). `catalogs.py` 20 → 21; README /
TESTFLIGHT_RUNBOOK counts; round-trip byte-identical; `Tools/content/tests` table gains it.

### 3.1a The year plan (Architect, 2026-09-08 — `Docs/Economy/GOLFIN_Rotation_Plan_2026-27.xlsx`)

The 52 seed rows are not random: each week is pinned from a plan generated against the live
catalogs with exactly the §4.1 rules (quotas, 7-type spread, 4-week exclusion, starters and the
default ball excluded, Supreme excluded) plus a theme layer the generator does not know about —
the headline type cycles weekly, the **brand of the week** cycles through all 20 brands (step 7
mod 20), and 12 **marquee weeks** on the JST calendar (Sports Day, Halloween, Holiday, Year-End,
New Year Hatsuuri, Valentine, Sakura, Spring Major, Golden Week, Summer Links, Obon, Season
Finale) carry two Legendary clubs and a Legendary character when the exclusion allows. Balls run
a 6-week pattern so the single Legendary ball surfaces every 6th week. Verified: 0 exclusion
violations, all 7 types every week, 457 distinct clubs (64 Legendaries) over the year. Two rows
(`wk_2027_01`, `wk_2027_18`) pin **Freda (Supreme)** — flagged DECISION in the workbook; if Cesar
says no, clear `pinnedCharacter` and set `characterQuota` to `Any:1` on those two rows before
seeding. The generator's own output for an unpinned row is still seeded by `rotationId`, so an
admin who clears the pins gets a reproducible lineup, not the plan's.

### 3.2 Additive columns (through the importer — plan → `--apply` → publish → `--check` clean)

- `shop_catalog.rotationId` — blank on the 5 existing rows.
- `gacha_banners.rotationId` — blank on the 4 existing rows.
- `gacha_banners.pityGroup` — blank on existing rows. Banners sharing a group share ONE pity
  counter (§5). The weekly banner uses `weekly`.

`REQUIRED_COLUMNS` unchanged (all three are optional). `CATALOG_VIEWS` list columns gain
`rotationId` on Shop and Gacha Banners.

### 3.3 Ball price ladder (new, editable — `ECONOMY_MASTER` §3 gets the line)

Balls stack (`quantity`, cap 99) and nothing consumes them yet, so a listing is a one-time
buy: **quantity 10** at Common 30 / Uncommon 60 / Rare 120 / Mythic 250 / Legendary 500 RP.
Constants in `lib/rotation.ts` beside the club and character ladders (which are copied from
`ECONOMY_MASTER` §3 verbatim). All three ladders are the generator's defaults, and every
generated draft row is editable before publish — the ladder is a starting point, not a lock.

---

## 4. Admin — Rotations panel (`app/(panels)/rotations/`)

Registered after Shop in `lib/registry.ts` (`nav.rotations` — "Rotations" / "週替わり"; icon
`cart`). A `CatalogPanel` for `rotations` (so `+ New row`, drafts, publish, export all work)
with `editorExtras` and, ABOVE the table, the **Lineup workbench** for the selected rotation:

**4.1 Generate / Preview.** `generateLineup(rotation, ctx) → Lineup` in `lib/rotation.ts`
(pure, vitest):

0. **Pins first.** For each bucket (clubs / balls / character / featured) with a non-blank
   `pinned*` value, the refs are taken verbatim (each must resolve, be `is_active`, and not be
   the default ball — else a **blocking** error naming the ref) and count against the quota; steps
   1–3 fill only the remainder. Pinned refs are exempt from the exclusion rule (the plan already
   honours it) but a pinned ref inside the exclusion window shows the R4 **warn**.
1. Eligible refs = published + draft rows of `clubs` / `balls` / `characters` that are
   `is_active`, not `isDefault` (balls), not `min_build > row.minBuild` (G2 by construction),
   and not present in any `shop_catalog` row whose `rotationId` is one of the last
   `excludeWeeks` rotations (by `startUtc`).
2. RNG = `mulberry32(seed)`. For each quota bucket, shuffle the eligible refs of that rarity and
   take the count; clubs additionally **spread across types** — no second club of a type until
   every type has one (a quota larger than 7 wraps). Characters: `Any` picks from all locked
   rarities; the FTUE starters (`Characters.csv` starter flag — read it) are never picked.
3. Shortfall (a bucket cannot be filled) → the lineup carries a **warning** per bucket; the
   panel shows it amber; generation still succeeds with what it found.
4. Output rows:
   - `shop_catalog`: `shop_<rotationId>_<refId>` — `category`, `refId`, `rpCost` from the
     ladder, `saleRpCost` blank, `sortOrder` = 100 × rarity rank + index, `startAt/endAt` =
     the rotation window, `saleStartAt/saleEndAt` blank, `quantity` (balls 10, else blank),
     `rotationId`, `is_active` true, `minBuild` = max(`SHOP_CATEGORY_STRICT_BUILD`, ref
     `min_build`) so G1/G2 pass by construction.
   - `gacha_rates`: `<poolId>_<rarity>` rows copied from `gachaBasePoolId`.
   - `gacha_pools`: `pool_<rotationId>` = every active entry of the base pool copied
     (`id = p<rotationId>_<refId>`), plus the `gachaFeaturedCount` featured clubs **inserted or
     re-weighted** to `weight × featuredWeightMul`, `featured = true`, `dupeRp` copied from the
     base entry of the same rarity (or the base pool's median if absent).
   - `gacha_banners`: `banner_<rotationId>` — `nameEn/nameJa` = the rotation's, `taglineEn/Ja`
     "Featured this week" / "今週のピックアップ", `artSprite = GachaBanner_Weekly` (§4.4),
     `costX1/costX10/ticketType/pityThreshold/pityMinRarity/guaranteeMinRarityX10` copied from
     the base pool's first active banner, `pityGroup = weekly`, `poolId = pool_<rotationId>`,
     `startUtc/endUtc` = the window, `featuredRefIds` = the featured clubs, `sortOrder` 0 (first
     in the carousel), `rotationId`, `active` true.
   - The rotation row itself: `materializedAt = now`.

   The **preview** renders all of it — cards with name / rarity / type / price, the banner's
   effective odds via `effectiveOdds` (featured rows visibly boosted), warnings — and writes
   nothing.

**4.2 Materialize.** One button, typed confirmation when the rotation was already materialized
(it will overwrite that rotation's draft rows; rows of OTHER rotations are never touched).
Writes every §4.1 row with `upsertDraftRow` (`expectNew` false — regenerating is an edit),
sequentially, and reports counts per catalog. `writeAudit` action `rotation_materialize`
with the rotation id + seed.

**4.3 Publish rotation.** One button → `publishCatalog` in the order **`gacha_rates` →
`gacha_pools` → `gacha_banners` → `shop_catalog` → `rotations`** (dependencies first, so a
validator that reads `ctx.otherCatalogs` sees published targets), each with the standard
validation; the first failure stops the chain and the panel shows which catalog and why.
Typed confirmation once, naming the five catalogs. Audit `rotation_publish`. **This publishes
whatever drafts those catalogs hold** — the confirmation text says so, and the drawer's
diff for each catalog is linked from the result so Cesar can look before confirming.

**4.4 Calendar + housekeeping.** Above the table: the next 8 weeks as a strip — each cell LIVE /
SCHEDULED / GENERATED / NOT GENERATED / ENDED from the server clock and `materializedAt`;
clicking a missing week creates the draft rotation row with defaults copied from the latest
one (`seed` recomputed). **Archive ended** button: sets `is_active = false` on every
`shop_catalog` / `gacha_banners` / `gacha_pools` / `gacha_rates` draft row whose `rotationId`
belongs to a rotation with `endUtc < now − 7 d` (I6: deactivate, never delete), then the
normal publish. Placeholder banner art: `Assets/Resources/Art/Gacha/Banners/GachaBanner_Weekly.png`
derived from `GachaBanner_StandardClub1` by the `TicketIconDerive` re-tint approach (blue
`#4A8FE5` multiply), committed — Cesar replaces it via the admin `artUrl` upload whenever.

**4.5 Validator rules (`contentValidate.ts`, blocking unless marked warn).**

- R1 `rotations`: `endUtc > startUtc`; `rotationId` matches `^wk_\d{4}_\d{2}$`; quotas parse
  (`Rarity:int;…`, `Any:int` for characters); `gachaBasePoolId` resolves; overlapping windows
  between two active rotations → **error**; a gap between consecutive active rotations →
  **warn**.
- R2 `shop_catalog` / `gacha_banners`: a non-blank `rotationId` must resolve to a `rotations`
  row and the row's window must lie inside the rotation's window (equal is fine).
- R3 `gacha_banners`: all active banners sharing a `pityGroup` must have identical
  `pityThreshold` and `pityMinRarity` — a shared counter with different thresholds is
  undefined.
- R4 (warn) `shop_catalog`: a `rotationId` row whose ref appeared in a rotation within
  `excludeWeeks` — the generator never does this; a hand edit can.
- Existing rules stay: default ball, G1, G1-T, G2, pool-rollable.

**4.6 Strings** — all in `lib/i18n.ts` `DICT`, en + ja: nav, workbench labels, the five
button/confirmation texts, R1–R4 messages, calendar states. No player strings in this task
(the client spec owns them).

**4.7 Mock mode** — `lib/mockContent.ts` gets the `rotations` catalog + the seed row so the
panel, generator and publish chain are exercisable with `MOCK_MODE=1`.

---

## 5. Backend — pity that survives the week (playlife, one migration)

`2026_09_xx_gacha_pity_group.sql`: `create or replace function public.golfin_gacha_pull` (the
`2026_09_02_default_ball_guard.sql` body is the base — copy it whole, change only this):
`v_pity_key := coalesce(nullif(btrim(v_row.data->>'pityGroup'), ''), v_banner)` and use
`v_pity_key` wherever the function reads or writes `golfin_gacha_pity.banner_id` (lines ~195,
~346, ~696–707 in the current body). No schema change — the column keeps its name; its meaning
becomes "pity key". `maxPullsPerPlayer` keeps counting per **banner** (`total_pulls` is read
by banner id — keep a second read keyed by `v_banner` for that check, so a weekly cap, if ever
set, is per week). Comment on the table updated. Verification block: two banners with
`pityGroup = weekly`, pull on A to counter 3, pull on B → counter continues at 4. **Full SQL in
chat for Cesar.** Deploy, `flyctl status`, smoke (`/health`, `/gacha/tickets` 403 unauth).

The admin **Gacha ops panel** per-user pity table shows the key (banner id or group) and the
reset still works per key.

---

## 6. Sequencing

1. §3.1 catalog + seed migration (SQL in chat) → `catalogs.py` → round-trip → §3.2 columns via
   the importer → publish → `--check` clean.
2. §4 panel + `lib/rotation.ts` + validator + mock — one commit set; vitest for the generator
   (determinism: same seed ⇒ same lineup; exclusion; type spread; quota shortfall warning;
   featured weight), for R1–R4, and for the publish-order chain (mocked `publishCatalog`).
3. §5 migration (SQL in chat) → Cesar applies → deploy → smoke.
4. **Live E2E (§21):** on prod, generate `wk_2026_38`, preview, materialize, publish; SQL
   quoted: 5 catalogs at new versions, the shop rows and banner present with the window; pull
   the weekly banner on Cesar's account twice with the next rotation's banner also live (make a
   second short rotation for the test, then deactivate it) → pity continues across the two.
5. `export_content.py` → commit the CSVs → `npm run deploy` + §23 proofs.

## 7. Acceptance

- [ ] `rotations` seeded with the 52 planned rows (`reference/rotations_seed.csv`); export byte-identical; `--check` clean for `rotations`,
      `shop_catalog`, `gacha_banners`; 21 catalogs in README + runbook.
- [ ] Rotations panel: calendar shows `wk_2026_38` SCHEDULED; PREVIEW of a pinned row renders
      exactly the pinned refs (vitest: pins ⇒ identical lineup regardless of seed); PREVIEW of a
      row with pins cleared renders 9 clubs (3/2/2/1/1, seven types before any repeat) + 3 balls +
      1 character + a banner with 2 featured rows boosted ×3 in `effectiveOdds`; changing `seed`
      changes that lineup; the same seed twice is byte-identical (vitest quotes the hash); a pin
      that does not resolve blocks with the ref named.
- [ ] MATERIALIZE writes the draft rows (counts quoted); a second MATERIALIZE asks for typed
      confirmation and leaves other rotations' rows untouched (vitest + live).
- [ ] PUBLISH ROTATION publishes the five catalogs in order; a deliberate R3 violation stops
      the chain at `gacha_banners` naming the rule; nothing after it is published.
- [ ] R1 overlap error, R1 gap warn, R2, R3, R4 each have a vitest.
- [ ] Ended rotations: ARCHIVE deactivates their rows; the server refuses a purchase of an
      archived row (`not_listed / inactive`) and a pull on an archived banner (`not_available / banner`).
- [ ] Pity migration: verification block passes; live E2E in §6.4 quoted.
- [ ] Gacha ops panel per-user pity shows the group key; reset works.
- [ ] Mock mode exercises the full panel.
- [ ] `npm run build` green; vitest green; backend suite green; deployment id + version stamp
      quoted; Access 302; `/health` and the four smoke routes 200.
- [ ] Strings: this task adds NO player-facing keys (admin `DICT` en + ja only — every new string listed in the report). If the implementer finds a player-facing string is needed, it goes through the importer → admin publish and is listed in the report, never code-only.
- [ ] `ECONOMY_MASTER.md` §3 gains the ball ladder line + a "weekly rotation" paragraph
      (Architect will review the wording — write it, flag it).

## Files this task touches

**Unity repo (data + tools only — no C# in this spec):** `Assets/Resources/Data/rotations.csv`
(new), `shop_catalog.csv`, `gacha_banners.csv` (columns), `Assets/Resources/Art/Gacha/Banners/GachaBanner_Weekly.png`
(new placeholder), `Tools/content/catalogs.py`, `Tools/content/tests/*`, `Tools/content/README.md`,
`Docs/TESTFLIGHT_RUNBOOK.md`, `Docs/Economy/ECONOMY_MASTER.md`, `Docs/AI_CONTEXT.md`.
**Dashboard:** `app/(panels)/rotations/{page,rotations-panel,lineup-workbench}.tsx` (new),
`lib/rotation.ts` (+ `__tests__`), `lib/contentValidate.ts`, `lib/contentView.ts`,
`lib/registry.ts`, `lib/i18n.ts`, `lib/mockContent.ts`, `app/(panels)/gacha/*` (pity key label).
**playlife:** `backend/migrations/2026_09_xx_content_rotations_seed.sql`,
`2026_09_xx_gacha_pity_group.sql` (+ copies under `Tools/admin-dashboard/migrations/`).

## Out of scope (do NOT do these)

- Any Unity C# — badges, countdown, "NEW" tags, mid-session reload → `weekly_rotation_client`.
- Automatic weekly generation on a schedule (a cron that materializes + publishes without a
  human). The calendar makes the missing week obvious; a scheduled publish is a later decision
  because it removes the review step.
- Per-player rotations / personalised shops; item rotation (repair kits stay permanent listings).
- Weekly sales (`saleRpCost`), the POPULAR / OFFERS curation chips, `stockLimit`, `minPlayerLevel`.
- Rotating ticket types or ticket prices; any paid SKU (`MONETIZATION_PLAN` §0).
- A server-side "one rotation live at a time" guard — the validator R1 overlap rule is the lock;
  the spend paths already refuse out-of-window rows individually.
