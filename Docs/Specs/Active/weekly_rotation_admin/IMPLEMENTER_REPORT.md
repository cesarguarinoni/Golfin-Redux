# IMPLEMENTER_REPORT — `weekly_rotation_admin`

**Iteration shape:** `rotations:generator-and-publish-chain`
**Iteration:** 3
**Canonical screenshot:** `screenshots/rotations_workbench_preview.png` (2880×2600)

---

## What was built

A **rotation** is a row of a new `rotations` catalog (#21): a window, per-rarity quotas, a seed
and the four pinned lists that carry the year plan. `lib/rotation.ts` is a PURE generator —
pins first, verbatim, blocking on a ref that does not resolve; then `mulberry32(seed)` fills only
what the pins left blank, by rarity quota, spreading clubs across the seven types and skipping
anything the last `excludeWeeks` rotations listed. It emits DRAFT rows of the four existing
catalogs — 13 shop rows carrying the week's window and ladder price, the weekly pool cloned from
the base pool with the two featured clubs at ×3, its rate table, one banner in `pityGroup =
weekly` — and the rotation row stamped `materializedAt`. The **Rotations panel** is a plain
`CatalogPanel` with a **Lineup workbench** above it: an 8-week calendar, PREVIEW (writes
nothing), MATERIALIZE (one `upsertDraftRow` per row; typed confirmation on a re-materialize),
PUBLISH ROTATION (the ordinary `publishCatalog`, five catalogs in dependency order, stop on the
first refusal; typed `PUBLISH`), ARCHIVE ENDED (deactivate, never delete). Validator rules
R1–R4 and a ball price band. On the server, `golfin_gacha_pull` keys the pity COUNTER by
`coalesce(pityGroup, banner_id)` so pity survives the week; the cap stays per banner.

**The design decision worth stating.** Everything goes through the two writes the drawer already
has — `upsertDraftRow` per row and `publishCatalog` per catalog — so there is **no new endpoint
and no server code path** to name a `rotation_materialize` / `rotation_publish` audit action.
The audit log carries every write under the actions it already has (`content.draft.*:<catalog>`
per row, `content.publish:<catalog>` per catalog with `rotation_publish wk_2026_38 seed=…` in the
publish note). That is the spec's "no new endpoint" (§2) taken literally; the named audit action
is the one line of §4.2/§4.3 it cost — flagged below.

**One spec point that could not ship as written** (details under § Deviations): the ball
listing is **one ball**, not ten (`golfin_shop_purchase` reads `quantity` for tickets only and
rule G3-Q refuses anything else on a ball row). The pity migration was applied by Cesar and the
§6.4 E2E ran on prod afterwards (checklist row 7).

---

## Files modified or created

### Unity repo (data + tools — no C#) — commits `d74f4f7bc`, `80e5cb8ba`

| Path | Change |
|---|---|
| `Assets/Resources/Data/rotations.csv` | **NEW** — the 52-week plan, `reference/rotations_seed.csv` in LF form (the reference is CRLF; the exporter's canonical form is LF). Seeded on prod at v1; export byte-identical (md5 `11a9211b…` before and after); now v2 with `wk_2026_38.materializedAt` stamped. |
| `Assets/Resources/Data/shop_catalog.csv` | `+rotationId` (blank on the 8 shipped rows) and, after the live publish, the 13 `shop_wk_2026_38_*` rows (v7). |
| `Assets/Resources/Data/gacha_banners.csv` | `+rotationId`, `+pityGroup` (blank on the 4 shipped rows) and `banner_wk_2026_38` (v10). |
| `Assets/Resources/Data/gacha_pools.csv`, `gacha_rates.csv` | `pool_wk_2026_38`'s 12 entries (v3) and 6 rate rows (v4). |
| `Assets/Resources/Data/content_version.txt` | `rotations=2`, the four bumped cursors. |
| `Assets/Resources/Art/Gacha/Banners/GachaBanner_Weekly.png` (+ `.meta`) | **NEW** — the Standard Club 1 art re-tinted to `#4A8FE5` by luminance remap (the `TicketIconDerive` approach, gain chosen to keep the source's mean luminance), 882×1448, Sprite import settings copied from `GachaBanner_TestA.png.meta`. |
| `Tools/content/catalogs.py` | Catalog #21 `rotations` (id `rotationId`); `IS_ACTIVE_COLUMN`; docstring facts. |
| `Tools/content/seed_from_csv.py` | `seed_rows()` splits an `is_active` CSV column OUT of `data` and uses it as the row flag — the rule the importer and exporter already applied; `rotations.csv` is the first CSV to carry the column at seed time. |
| `Tools/content/tests/test_catalog_registry.py` | Count 20 → 21; `TestRotationsIsRegistered` (the 52 planned ids present and pinned, LF, `materializedAt` blank-or-instant, the seeder rule, a `false` cell seeds inactive). **iter-2:** the seeded gacha rows and the plan are pinned BY ID, never by count — a rotation publish appends to four catalogs every week. Suite 48 → **53, all passing** (`Ran 53 tests … OK`, re-run AFTER the live publishes). |
| `Tools/content/README.md`, `Docs/TESTFLIGHT_RUNBOOK.md` | Twenty-one catalogs; the `rotations` row; the seeder note. |
| `Tools/content/export_content.py`, `tests/test_export_check.py` | **iter-3:** R3 (masked / conflicting art) exempts a rotation-tagged banner on the `GachaBanner_Weekly` stand-in — Deviation 14. |
| `Tools/admin-dashboard/migrations/2026_09_11_content_rotations_seed.sql`, `…/2026_09_11_gacha_pity_group.sql` | Mirrors of the two playlife migrations. |
| `Docs/Economy/ECONOMY_MASTER.md` | §3: the ball ladder line (one-ball, with the ten-ball caveat) and a "Weekly rotation" paragraph — **Architect to review the wording**. |

### admin dashboard — commits `05f0f7da1`, `d2dc096c7`, `f8063af6b`

| Path | Change |
|---|---|
| `lib/rotation.ts` | **NEW** — parsers (`parseQuota`, `parseRefList`, `fnv1a32`, `rotationSeed`, ISO weeks), `generateLineup`, `fillPlan`, `recentRotations` / `recentlyFeaturedRefs`, `lineupCanonical` / `lineupHash`, `materializeLineup` (+ stale-row deactivation), `publishRotationChain`, `calendarWeeks` / `rotationState` / `newRotationRow`, `archivableRotations` / `archiveRows`; the three ladders; `ROTATION_PUBLISH_ORDER`. |
| `lib/__tests__/rotation.test.ts` | **NEW** — 34 tests (determinism + pinned hash, pins ⇒ identical, blocking pins, fill plan, the unpinned draw, type-spread wrap, shop/rates/pool/banner rows, featured ×3 in `effectiveOdds`, exclusion, shortfall, materialize counts + other rotations untouched + stale deactivation, publish order + stop, ISO weeks, calendar states, create-week, archive, the permanent-listing warning). |
| `lib/__tests__/rotationValidate.test.ts` | **NEW** — 20 tests: R1 (clean seed, overlap = error, gap = warn, id/window/quota grammar, base pool, commas/dupes, numeric knobs), R2 (shop + banners, not-loaded is an error), R3 (threshold, rarity, blank≡0, inactive ignored), R4 (warn, window is by rotation count, own pins count), the ball band, and the `ticket_types` load regression. |
| `lib/contentValidate.ts` | `rotations` in REQUIRED / NUMERIC / ID_COLUMN; `checkRotationTag` (R2) on shop + banners; R3 over `pityGroup`; R4 on shop; the `rotations` block (R1); `ballBand`; `SHOP_REFERENCED_CATALOGS` derived from the category map (the `ticket_types` fix). Imports from `rotation.ts`, never the reverse. |
| `lib/contentView.ts` | `rotations` in `CONTENT_CATALOGS` + `CATALOG_VIEWS`; `rotationId` column + facet on Shop; `pityGroup` + `rotationId` columns on Gacha Banners. |
| `lib/contentData.ts` | `SEARCH_COLUMN.rotations = nameEn`; `rotationId` filterable on shop; `REFERENCED_CATALOGS` re-exported from the pure module. |
| `lib/contentMutations.ts` | `publishCatalog` loads `rotations` for `shop_catalog` and `gacha_banners`, and `gacha_pools` for `rotations`. |
| `lib/registry.ts`, `lib/i18n.ts` | The `rotations` panel (icon `cart`); **72 new DICT keys**, en + ja (listed under § Strings). |
| `lib/mockContent.ts`, `lib/mockGacha.ts` | The `rotations` catalog + two rows (one pinned to the mock refs, one open); a `weekly` GROUP pity row. |
| `lib/types.ts`, `lib/gachaData.ts` | `PlayerPityRow.isGroup` / `bannerIds`; the per-user pity read resolves a group key to its active members' threshold (R3 makes them identical), cap per banner only. |
| `app/(panels)/rotations/{page,rotations-panel,lineup-workbench}.tsx` | **NEW** — the panel and the workbench. |
| `app/(panels)/_content/catalog-panel.tsx` | `reloadToken` prop, so a banner that writes drafts of the catalog can make the table refetch. |
| `app/(panels)/shop/shop-panel.tsx` | `rotationId` field rendered explicitly in the editor (the shipped rows predate the column). |
| `app/(panels)/gacha-banners/gacha-banners-panel.tsx` | `pityGroup` + `rotationId` fields in the pity block, with the R3 hint. |
| `app/(panels)/users/gacha-tab.tsx` | The pity table labels a GROUP key (`GROUP · n`, members in the tooltip) and reads "n pulls on this key". |
| `scripts/shoot.mjs` | `select:<value>` step; `fill:` falls back to an open dialog's text input. |

### playlife — commit `df84963`

| Path | Change |
|---|---|
| `backend/migrations/2026_09_11_content_rotations_seed.sql` | **NEW** — generated by `seed_from_csv.py --catalogs rotations`; **applied** over PostgREST 2026-09-11 (data only). |
| `backend/migrations/2026_09_11_gacha_pity_group.sql` | **NEW** — `golfin_gacha_pull` re-issued with the pity key (5 hunks against `2026_09_02_default_ball_guard.sql`), table + column comments, a Part 2 verification block (two free banners in group `weekly`, 3 pulls on A then 1 on B ⇒ counter 4, inside ROLLBACK). Parse-checked with pglast (SQL + both plpgsql bodies). **NOT APPLIED — DDL, Cesar's SQL-editor step.** |

No backend Python changed; `routers/gacha.py` passes the function's JSON through. Backend suite **319 passed** (unchanged count).

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `rotations` seeded with the 52 planned rows; export byte-identical; `--check` clean for `rotations`, `shop_catalog`, `gacha_banners`; 21 catalogs in README + runbook | **PASS** | Seed applied over PostgREST: `content_rows: 52 first/last wk_2026_38 wk_2027_36 versions {1} active True min_build {0}`, `content_drafts: 52`, `content_catalogs: published_version 1`, `content_versions: v1 by seed_from_csv.py`, and `is_active in data? False, keys 18` (the seeder rule). Export: `cmp` before/after → **byte-identical**, md5 `11a9211bc77e365d4d8dbe34fb42cea6` both sides; the CRLF reference file hashes to the same value once `\r` is stripped. `--check` after the live publish: **`clean — no file would change, no catalog has drifted`**. README + runbook say twenty-one; `test_the_table_holds_twenty_one_catalogs` pins it. |
| Rotations panel: calendar shows `wk_2026_38` SCHEDULED; PREVIEW of a pinned row renders exactly the pinned refs (vitest: pins ⇒ identical regardless of seed); a cleared row renders 9 clubs (3/2/2/1/1, seven types before any repeat) + 3 balls + 1 character + a banner with 2 featured rows boosted ×3 in `effectiveOdds`; changing `seed` changes it; same seed twice is byte-identical (hash quoted); an unresolvable pin blocks naming the ref | **PASS** | **Live** (`admin.golfin.world/rotations`, prod): PREVIEW of `wk_2026_38` rendered exactly the plan — 9 pinned clubs in pin order (`club_driver_bogeyb_legendary` 1500 … `club_wood_bogeyb_common` 100), `ball_ace_attire/clover_pro/putt_ace`, `char_mike`, banner `banner_wk_2026_38 · pool_wk_2026_38 · pity 50→Legendary · group weekly`, odds `club_driver_bogeyb_legendary★ 300 1.500%` (base Legendary 0.500%) and `club_wood_bogeyb_mythic★ 300 3.837%` (base Mythic 1.279%), "Rows this will write: … (33) · Lineup hash 0e56b2a6 · seed 3287013188". After publish the cell reads **SCHEDULED** (materialized + published, window in the future). **vitest** (`rotation.test.ts`): `pins > a fully pinned row renders exactly the pinned refs, regardless of seed` (seeds 1 vs 999999 ⇒ identical rows in all four catalogs); `the unpinned draw` ⇒ 9 clubs `{Common:3,Uncommon:2,Rare:2,Mythic:1,Legendary:1}`, 7 distinct types with counts ∈ {1,2}, 3 balls `{Common:1,Uncommon:1,Rare:1}`, 1 non-starter character; `boosts the two featured clubs ×3` ⇒ `pOf(featured)/pOf(base Legendary) ≈ 3` and `p = 200/10000 × 300/400`; `changing the seed changes the lineup`; `the same seed twice is byte-identical, and the hash is pinned` ⇒ **`8838dbcb`** (two clocks, canonical form blanks `materializedAt`); `a pin that does not resolve BLOCKS with the ref named` ⇒ `pinnedClubs: "club_nope" is not a club — it does not resolve.`, zero rows. |
| MATERIALIZE writes the draft rows (counts quoted); a second MATERIALIZE asks for typed confirmation and leaves other rotations' rows untouched (vitest + live) | **PASS** | **Live prod:** `wk_2026_38: wrote 6 rate rows, 12 pool entries, 1 banner, 13 shop rows and the rotation row; 0 stale row(s) deactivated.` **Mock:** first run `6 / 6 / 1 / 4 / rotation row`; the second run on the same week opened `Materialize wk_2026_38 again?` with the MATERIALIZE button disabled until `wk_2026_38` was typed; after it, `wk_2026_39`'s rows read back **byte-identical** (`other_rotation_rows_untouched: true`) and `wk_2026_38.materializedAt` was re-stamped. **vitest** `materialize > a second materialize overwrites this rotation's rows and leaves other rotations' rows untouched`: counts `{gacha_rates 6, gacha_pools 9, gacha_banners 1, shop_catalog 13, rotations 1, deactivated 0}` on the first run; a different seed draws different ids, the first run's rows not reproduced are switched OFF (`counts.deactivated === stale.length`, every fresh id active, exactly 13 live shop rows for the week), and `wk_2026_39`'s rows are JSON-identical before/after. The stale-row deactivation was a design gap the test found: a re-seed produces different `shop_<rotation>_<ref>` ids, so without it a week would have listed both lineups. |
| PUBLISH ROTATION publishes the five catalogs in order; a deliberate R3 violation stops the chain at `gacha_banners` naming the rule; nothing after it is published | **PASS** | **Mock, clean run:** `All five catalogs published: gacha_rates v10000, gacha_pools v10000, gacha_banners v10000, shop_catalog v10000, rotations v10000.` **Mock, deliberate R3** (`mock_banner_live` moved into group `weekly` at threshold 30): `Stopped at gacha_banners: 1 validation error(s); nothing was published. — banner_wk_2026_38: R3: pityGroup "weekly" is shared with mock_banner_live, but pityThreshold/pityMinRarity differ (30/Legendary vs 50/Legendary). … Nothing after it was published.` — `/api/content` afterwards: `gacha_rates v10001, gacha_pools v10001, gacha_banners v10000 (dirty 1), shop_catalog v10000, rotations v10000`. **vitest** `publish chain > publishes the five catalogs in dependency order` (calls `["gacha_rates","gacha_pools","gacha_banners","shop_catalog","rotations"]`) and `stops at the first failure and publishes nothing after it` (calls end at `gacha_banners`, `stoppedAt === "gacha_banners"`). **Live prod:** the chain stopped at `shop_catalog` on a PRE-EXISTING bug (the shop publish never loaded `ticket_types`, so `shop_ticket_standard_50` "did not exist") — rates v4 / pools v3 / banners v10 landed, shop and rotations did not, exactly as designed; fixed in `d2dc096c7` (list derived from the category map + regression test), redeployed, and the last two catalogs published through the normal drawer: `shop_catalog v7 — 13 added` (2 warnings, both on old placeholder rows), `rotations v2 — 1 changed`. |
| R1 overlap error, R1 gap warn, R2, R3, R4 each have a vitest | **PASS** | `rotationValidate.test.ts`: `overlapping windows between two ACTIVE rotations are an ERROR` (`wk_2026_39 … overlaps wk_2026_38`, and deactivating one clears it), `a gap between consecutive active rotations is a WARNING only`, `R2: an unknown rotationId is an error, and so is a window outside the rotation's` (+ blank window on a tagged row fails both bounds; blank `rotationId` untouched; catalog-not-loaded is an error), `R3: a shared pityGroup with a different threshold is an ERROR naming the rule` (+ rarity, blank≡0, inactive ignored), `R4: a ref the previous rotation listed WARNS on a hand edit, and never blocks` (+ the window counts ROTATIONS by `startUtc`, a rotation's own pins count). 20 tests, plus R1 shape/grammar. |
| Ended rotations: ARCHIVE deactivates their rows; the server refuses a purchase of an archived row (`not_listed / inactive`) and a pull on an archived banner (`not_available / banner`) | **PASS** | **Mock:** a rotation dated 2026-08-24→31 was materialized, ARCHIVE ENDED showed `17`, the dialog named it, and afterwards `17 row(s) deactivated across 1 rotation(s)`; read-back: shop 4/4 inactive, banner 1/1, pools 6/6, rates 6/6, every other row still active. **vitest** `archive: rotations ended > 7 days ago, and only their ACTIVE rows, deactivated` (13 + 1 + 9 + 6 rows, nothing of the other rotation / the permanent listing / the standard pool or banner; already-inactive rows not re-emitted). **Server half, live on prod via the RPCs with a synthetic user (both refuse before any write; 0 pulls / 0 purchases recorded):** `golfin_shop_purchase(shop_ticket_standard_50)` — a published `is_active=false` row — → `{"status":"not_listed","reason":"inactive"}`; `golfin_gacha_pull(banner_inactive)` → `{"status":"not_available","reason":"inactive"}` (the shipped reason is `inactive`, not `banner`). And the week's own rows BEFORE their window: `golfin_shop_purchase(shop_wk_2026_38_club_driver_bogeyb_legendary)` → `not_listed / window`, `golfin_gacha_pull(banner_wk_2026_38)` → `not_available / window` — the server clock, not the client, opens the week on Monday. No rotation has ended yet, so ARCHIVE itself has nothing to do on prod until 2026-09-28. |
| Pity migration: verification block passes; live E2E in §6.4 quoted | **PASS** | **Migration applied by Cesar 2026-09-11** (SQL editor, Part 1 then Part 2; he pasted Part 2's last result `gacha_pull_client_callable_expected_0 = 0` — the batch reaching that SELECT means every ASSERT in the DO block held, and the OLD function would have failed `expected key weekly, got null`). **Live §6.4 on prod, re-derived from the tables, not from the paste:** two test rotations `wk_2026_36` (03:16:35 → 03:36:35Z) and `wk_2026_37` (03:36:35 → 04:06:35Z), quotas Common:1 / Common:1 / Any:0, materialized through the panel (`6 rate rows, 11 pool entries, 1 banner, 2 shop rows` each), banners set free (`costX1 0`) so the ledger stayed untouched, published through PUBLISH ROTATION in one run (`gacha_rates v5, gacha_pools v4, gacha_banners v11, shop_catalog v8, rotations v3`). Pulls as Cratilo (`f2636482-…`): **A x1 → `pity: {counter 1, key "weekly"}`**, **A x1 → `counter 2`**; after A's window: `not_available / window`; **B x1 → `pity: {counter 3, threshold 50, min_rarity Legendary, key "weekly", forced false}, pulls_used 1`** — `golfin_gacha_pulls`: `(banner_wk_2026_36, pity_before 0 → 1)`, `(banner_wk_2026_36, 1 → 2)`, `(banner_wk_2026_37, 2 → 3)`; `golfin_gacha_pity`: `weekly counter=3 total=3 · banner_wk_2026_36 counter=0 total=2 · banner_wk_2026_37 counter=0 total=1`. The counter CONTINUED across the two banners; the cap count stayed per banner. Then both rotations and all 42 generated rows deactivated and published (`rates v6, pools v5, banners v12, shop v9, rotations v4`) — over PostgREST + the `content_publish` RPC because the Chrome Access session expired mid-run (a deactivation-only change set; `--check` clean after); an archived banner inside its window → `not_available / inactive`, an archived shop row → `not_listed / inactive`. |
| Gacha ops panel per-user pity shows the group key; reset works | **PASS** | `screenshots/users_gacha_tab_pity_group.png` — the drawer's Pity section lists `weekly` with a `GROUP · 2` badge (members in the tooltip), `4 / 50 to Legendary · 4 pulls on this key`, Reset enabled; `resetPity` already keys on `banner_id`, which IS the key, so the DELETE route resets a group row unchanged (mock: `MOCK_PLAYER_PITY.find(p => p.bannerId === "weekly")`). Live read path: `fetchPlayerGacha` resolves a key with no banner of that id to the active banners whose `pityGroup` equals it. |
| Mock mode exercises the full panel | **PASS** | `MOCK_MODE=1` on :3100: calendar (MISSING / NOT GENERATED / GENERATED / SCHEDULED all seen), create-week (`Created draft rotation wk_2026_37 (2026-09-07T00:00:00Z → 2026-09-14T00:00:00Z)`, defaults copied from the latest row, FNV seed `3136914637`), PREVIEW (pinned week; open week → amber shortfall warnings, `screenshots/rotations_workbench_warnings.png`), MATERIALIZE, typed re-materialize, PUBLISH ROTATION clean and R3-stopped, ARCHIVE ENDED, the JA render (`rotations_workbench_preview_ja.png`), the Users drawer group row. All 9 screenshots are mock frames (the MOCK DATA banner is on every one). |
| `npm run build` green; vitest green; backend suite green; deployment id + version stamp quoted; Access 302; `/health` and the smoke routes 200 | **PASS** | Content suite **`Ran 53 tests … OK`** re-run after the live publishes and the E2E (iter-2; iter-1 had run it before the publishes, and 8 seed-time count pins then broke — see Deviation 12). `--check: clean` after the last export. Deploy #1 `b3d528c0-4148-4269-a20e-ebb9ea47b541` (stamp `05f0f7da1`), #2 `eebbd47e-9ebf-4521-9edc-329aa63fa354` (`d2dc096c7`, the ticket_types fix), #3 `0ff72acc-d587-4baa-bfe6-dae2299453ef` (`f8063af6b`, HEAD of the dashboard); each ran the suite first (375 → 377 → **378 passed**) then the OpenNext build. Live `/api/version` in Chrome: `{"commit":"f8063af6b","stamped":true}`. Shell: `curl https://admin.golfin.world/api/version` → **302** to `cloudflareaccess.com` (Access fronting the origin). API: `flyctl status` both machines **v74**; `/health` → **200** `{"status":"ok"}`; `/api/v1/gacha/tickets` → **403** `Not authenticated`; `/api/v1/content?since=0&catalogs=rotations` → **200** `rotations v2 full`, `…catalogs=gacha_banners` → **200** `v10`. Backend `pytest -q` → **319 passed**. No backend Python changed, so no Fly deploy was made (nothing to ship). |
| Strings: no player-facing keys; admin `DICT` en + ja only, every new string listed | **PASS** | **72 new keys**, scripted lint: `missing/empty en or ja: none`, `{var} mismatch between en/ja: none`; `tsc --noEmit` exit 0 (`DictKey` is derived from `DICT`). No `LocalizationText.csv` change. List under § Strings. |
| `ECONOMY_MASTER.md` §3 gains the ball ladder line + a "weekly rotation" paragraph | **PASS** | Both written under §3, each opening with "Architect to review the wording"; the ball line states the one-ball reality and names the server change a ten-ball listing needs. |

---

## Evidence, quoted

### vitest — determinism, pins, featured, order

```
✓ determinism > the same seed twice is byte-identical, and the hash is pinned   hash 8838dbcb
✓ determinism > changing the seed changes the lineup
✓ pins > a fully pinned row renders exactly the pinned refs, regardless of seed  (seed 1 ≡ seed 999999)
✓ pins > a pin that does not resolve BLOCKS with the ref named                  "pinnedClubs: "club_nope" is not a club — it does not resolve."
✓ the unpinned draw > renders 9 clubs (3/2/2/1/1), seven types before any repeat, 3 balls, 1 character
✓ the unpinned draw > copies every ACTIVE base entry, resets featured, and boosts the two featured clubs ×3 in effectiveOdds
✓ materialize > writes every row through the upsert, in publish order, and reports the counts
     {gacha_rates: 6, gacha_pools: 9, gacha_banners: 1, shop_catalog: 13, rotations: 1, deactivated: 0}
✓ publish chain > stops at the first failure and publishes nothing after it     calls: rates, pools, banners — stoppedAt gacha_banners
Test Files 16 passed · Tests 378 passed   (was 324)
```

### Prod state after the live publish (PostgREST, `content_rows`)

```
content_catalogs: gacha_banners 10 · gacha_pools 3 · gacha_rates 4 · rotations 2 · shop_catalog 7
shop_catalog where rotationId = wk_2026_38 (13 rows, sortOrder asc, all 2026-09-14T00:00:00Z → 2026-09-21T00:00:00Z, is_active true, v7):
  club_driver_bogeyb_legendary 1500 (100) · club_wood_bogeyb_mythic 800 (200) · club_iron_bogeyb_rare 400 (300)
  club_awedge_bogeyb_rare 400 (301) · ball_putt_ace 120 (302) · club_pwedge_bogeyb_uncommon 200 (400)
  club_swedge_bogeyb_uncommon 200 (401) · ball_clover_pro 60 (402, min_build 2544) · club_putter_bogeyb_common 100 (500)
  club_driver_bogeyb_common 100 (501) · club_wood_bogeyb_common 100 (502) · ball_ace_attire 30 (503, min_build 2544) · char_mike 200 (504)
banner_wk_2026_38: poolId pool_wk_2026_38 · pityGroup weekly · pity 50/Legendary · 50/450 · featured club_driver_bogeyb_legendary;club_wood_bogeyb_mythic · artSprite GachaBanner_Weekly · artUrl '' · v10
pool_wk_2026_38: 12 entries, featured [(club_driver_bogeyb_legendary, 300), (club_wood_bogeyb_mythic, 300)]; rates 6 rows, sum 10000
rotations/wk_2026_38: materializedAt 2026-09-11T02:55:13Z · seed 3287013188 · v2
```

### Pity migration — the verification block's expected output (Part 2, self-rolling-back)

```
after A x3: counter=3 key=weekly
after B x1: counter=4 key=weekly
pity rows: weekly counter=4 total=4 | zz_pity_a counter=0 total=3 | zz_pity_b counter=0 total=1
PASS: pity continued across two banners sharing pityGroup=weekly
leftover_rows_expected_0 = 0 · leftover_banners_expected_0 = 0 · gacha_pull_client_callable_expected_0 = 0
```

---

## Deviations and findings

1. **Ball listing is one ball, not ten (§3.3).** `golfin_shop_purchase` reads `quantity` for
   `category = ticket` only ("honouring it for balls and items would change what
   already-published listings deliver"), and validator rule G3-Q refuses any other value on a
   non-ticket row — the very first vitest on a generated ball row hit it. A `quantity: 10` would
   be refused at publish, and if it were not it would charge 30 RP for a single ball. The
   generator writes ball rows with `quantity` blank at the ladder price; ECONOMY_MASTER says so.
   Ten balls per listing is a server change (purchase function + G3-Q), not a content edit.
2. **No `rotation_materialize` / `rotation_publish` audit ACTION.** With no new endpoint there is
   no server code to name one. Every write is audited under the existing actions; the publish
   note carries `rotation_publish <id> seed=<seed>`. If the Architect wants the named action, it
   is one small route (or a server action) — a decision, not a gap I could close inside "no new
   endpoint".
3. **The additive columns were a no-op for the importer, by design.** A missing key IS a blank
   cell to the importer, the exporter and the validator, so the plan read `same` on all 12 rows
   and wrote nothing; `--check` is clean. Published rows gain the key on their next edit.
4. **A pre-existing bug, found by the live chain and fixed:** a `shop_catalog` publish never
   loaded `ticket_types`, so the Shop drawer could not publish at all while
   `shop_ticket_standard_50` existed (since 2026-08-31; v5/v6 went through the RPC).
   `SHOP_REFERENCED_CATALOGS` is now derived from `SHOP_CATEGORY_TO_CATALOG` and pinned by a test
   (`d2dc096c7`).
5. **A stale draft that would have put the ticket listing back on sale.** `shop_ticket_standard_50`
   is published inactive (Cesar's `8c2c34d1e`, OFF SALE until a build carries the ticket fixes)
   but its draft — saved 3 minutes before that commit — was still active; any `shop_catalog`
   publish would have reactivated it. I aligned the draft with the published row
   (`is_active=false`, `updated_by` says why). Putting it back on sale is Cesar's call.
6. **Two of the week's picks are also permanent placeholder listings** (`shop_char_mike` 150 RP
   vs the week's `char_mike` 200 RP; `shop_ball_putt_ace` 50 RP vs 120 RP). The generator now
   WARNS about a pick that has an active untagged shop row (`f8063af6b`); retiring the
   shop_stocking placeholders is a decision.
7. **The plan's two `Legendary:1` weeks pin a non-Legendary character** (`wk_2026_42`
   `char_camila`, `wk_2026_53` `char_ean` — the exclusion window had no Legendary left). Pins
   count against the quota by TOTAL first (`fillPlan`), so those weeks get ONE character, the
   pinned one, not two; pinned so a Rare on a Legendary week is the plan's own choice.
8. **`reference/rotations_seed.csv` is CRLF; the last id is `wk_2027_36`, not `_37`** (ISO 2026
   has 53 weeks). The repo CSV is the LF form; the plan content is verbatim.
9. **Exclusion also reads the recent rotations' own pinned lists**, not only their shop rows, so
   an unmaterialized planned week already counts — a superset of the spec's rule.
10. **`sortOrder` ranks Supreme 0 … Common 500** (hero items first); `index` runs per rarity.
11. **The weekly banner is withheld on the installed builds until it has art.** 2873/2874 do
    not bundle `GachaBanner_Weekly.png`, and `GachaBannerModel` withholds a banner whose art
    resolves neither by `artUrl` nor by `artSprite`. `banner_wk_2026_38` goes live Monday with
    `artUrl` blank — see the pendings.
13. **The archive trap (iter-3, found after ARCHITECT_REVIEW_PASS, fixed in `70464d323`).** The
    first `gacha_banners` publish through the drawer AFTER an archive was refused:
    `banner_wk_2026_36: Pool "pool_wk_2026_36" has no active rate table` — rule 10 ran on the
    ARCHIVED banner whose pool and rates ARCHIVE ENDED had just deactivated, so the one publish
    the archive flow needs next could never succeed (my own E2E cleanup had gone over the RPC,
    which skips the validator, so nothing caught it; three gates passed it). Rule 15 shape audit —
    *"a rule that resolves a row against OTHER rows, and errors on a row that is itself
    inactive"* — every site in `contentValidate.ts`, including the ones that were fine:

    | Site | Cross-row? | Guarded on `row.isActive` before | Verdict |
    |---|---|---|---|
    | shop rule 6 — category / refId resolves / ref active / default ball | yes | **no** | TRAP (an archived week's row whose ref is retired later) — **fixed** |
    | shop G1 / G1-T / G3-Q / G2 | yes | yes | fine |
    | shop rule 8 (price band) | read only, warn | no | fine (warning) |
    | shop R2 `checkRotationTag` | yes | **no** | TRAP — **fixed** |
    | shop R4 | yes | yes | fine |
    | level_up_costs coverage / ceiling | catalog-level | n/a | fine |
    | gacha_rates ↔ gacha_pools (`checkRatesAgainstPool`, rules 2–4, 9) | yes | active rows only by construction | fine |
    | gacha_pools rule 5 (kind / ref / active / default ball) | yes | yes | fine |
    | gacha_pools rule 6 (rarity equals the ref's) | compares only when the ref is found | no | fine (a retired ref is still found) |
    | gacha_pools rule 8 (min_build) | yes | yes | fine |
    | gacha_banners rule 10 — pool rate table / entries / ticketType | yes | **no** | **THE TRAP** — fixed |
    | gacha_banners rule 13 — pity rarity payable in the pool | yes | **no** | latent (an inactive pool yields an empty rolled set, so it happened not to fire) — fixed for consistency |
    | gacha_banners rule 18 — featured refs in the pool (warn) | yes | **no** | noise on archived banners — fixed |
    | gacha_banners R2 | yes | **no** | TRAP — **fixed** |
    | gacha_banners R3 | yes | yes | fine |
    | ticket_types rule 20 (a type charged by ACTIVE banners) | yes | yes (the banners' flag) | fine |
    | rotations R1 base pool has active entries | yes | **no** | TRAP once a base pool is retired — **fixed** |
    | rotations R1 overlap / gap | active rows only | yes | fine |
    | missions ↔ components, mission_loadouts ↔ clubs | yes | no | same shape, **not changed here**: no deactivation path retires those referents today; flagged for the Architect |

    Fix: every cross-row rule runs on active rows only — the carve-out pools rule 5 and the shop's
    build gates already made, for the reason they give (a deactivated row reaches no player: the
    server refuses it, the client hides it; deactivation must stay the way out, I6). Sane-row rules
    (costs, windows, rarity shape, numeric) still run on every row. Four tests pin it
    (`archived rows never block a publish`), suite **382**; deployed as `70464d323`
    (CF `29e5e6d8-432a-49ba-8a53-0d54fb0d02c0`, live `/api/version` = `70464d323`), and the
    same `gacha_banners` publish then went through the validated route: **v13**.
14. **`export --check` R3 and the weekly stand-in (iter-3, fixed in `b0a70282a`).** The first
    `artUrl` on a weekly banner failed `--check`: `polish_regressions_0909` R3 refuses a banner
    whose `artUrl` is set while `artSprite` is not its own derived name (`GachaBanner_Wk202638`)
    — "the placeholder MASKS the uploaded art" — and the release lane refuses to build on a
    failing `--check`, so every week from now on would have blocked TestFlight. The rule was
    written for the accidental case (nobody ran Fetch URL Art); the spec's §4.4 design IS that
    state on purpose — 52 banners a year cannot each bundle a PNG — and `GachaBannerArt.Resolve`
    only lets a bundled sprite win when it is the row's own name (a shared stand-in is step 4,
    "draw this while the URL downloads"), so nothing is masked. Both R3 halves (masked, and
    conflicting uploads under one sprite) now exempt exactly a rotation-tagged banner on
    `GachaBanner_Weekly`; an un-tagged banner on it, or a tagged banner on any other shared
    sprite, still fails. 3 tests (`TestWeeklyStandinIsNotMaskedArt`), content suite **56**;
    `--check: clean` with the uploaded art in place.
12. **The CSVs carry the E2E's two archived test rotations, and the tests were re-pinned as
    invariants (iter-2, from the self-review).** `rotations.csv` now has 54 rows — the 52 planned
    weeks plus `wk_2026_36` / `wk_2026_37` with `is_active=false` — and `gacha_banners.csv`,
    `gacha_rates.csv`, `gacha_pools.csv`, `shop_catalog.csv` carry their (inactive) rows; the
    exporter appended an `is_active` column to `gacha_rates.csv` and `gacha_banners.csv` for
    the first time. They stay: I6 (deactivate, never delete) is the pipeline's own rule, and the
    exporter mirrors what prod holds. The content suite's seed-time COUNT pins (`gacha_banners 4`,
    `gacha_rates 6`, `gacha_pools 11`, `rotations 52`, `materializedAt` blank) were the wrong
    shape for a catalog a rotation appends to every Monday — iter-1 ran the suite before the
    live publishes and missed that they broke afterwards. They are now pinned BY ID (the seeded
    rows are present; the 52 planned ids are present and pinned; a set `materializedAt` is an
    ISO instant; `is_active` follows the CSV cell), which survives every weekly publish.

---

## Iteration 2 — the self-review's fail list

`golfin-self-reviewer` returned `BACK_TO_IMPLEMENTER` on iter-1 with one finding in three parts:
8 of the 53 content-suite tests failed after the live publishes, and the report had not said so
(the suite had been run before the publishes); the 54-row `rotations.csv` was not disclosed as a
deviation. Both addressed, nothing else changed:

1. **Content suite back to green — as invariants, not as today's numbers** (`Tools/content/
   tests/test_catalog_registry.py`). The reviewer's Option B, but bumping the counts to
   `7 / 24 / 45` would have failed again on Monday's publish; the seeded gacha rows and the 52
   planned rotation ids are pinned by id, `materializedAt` blank-or-ISO-instant, `is_active`
   follows the CSV cell. Option A (delete the two test rotations and their rows from the CSVs)
   was not taken: I6 says deactivate, never delete, and the exporter mirrors prod. Re-run:
   **`Ran 53 tests in 0.078s — OK`**.
2. **Deviation 12** written (above).
3. The suite result and `--check: clean` are now quoted in checklist row 10.

`catalogs.py`'s CSV-facts docstring says the four catalogs grow by construction.

---

## Iteration 3 — after ARCHITECT_REVIEW_PASS, on Cesar's answers

Cesar (2026-09-11): upload the placeholder as the weekly banner's `artUrl` (no new art before
Tuesday); leave the ticket listing off until a rotation carries it; retire the two colliding
shop_stocking placeholders; approval and the Architect decisions wait until Tuesday.

- **`banner_wk_2026_38.artUrl`** = the blue placeholder, uploaded through the Gacha Banners row
  editor's real upload control (JPEG q90, 245 KB — the route caps art at 500 KB, the bundled PNG is
  1.2 MB; the upload copy is kept under `reference/GachaBanner_Weekly_upload.jpg`, md5
  `8f3cfbef…` = the bytes the CDN serves, `200 image/jpeg 244928`). Published **gacha_banners
  v13**. Installed builds now render the banner Monday.
- **`shop_char_mike` and `shop_ball_putt_ace` deactivated** through the row route and published:
  **shop_catalog v10 — 2 deactivated**. `shop_ticket_standard_50` stays off.
- **The archive trap (Deviation 13)** was found by that very publish, fixed, tested, deployed
  (`70464d323`) and the publish re-run through the validated route. The code changed after the
  gates' PASS, so STATUS goes back through the chain (iter-3).
- **`--check` R3 refused the uploaded art (Deviation 14)** — the weekly stand-in is now the one
  deliberate shared sprite; `--check: clean`; content suite 56 OK; dashboard 382.

---

## Strings — the 72 new `DICT` keys (all en + ja)

`nav.rotations`, `c.facet.rotation`, `sh.rotation.help`, `gb.pityGroup.hint`, `ugac.pityGroup`,
`ugac.pityGroupHint`, `ugac.pityUsage`, and `ro.*`: `title`, `intro`, `workbench`, `calendar`,
`calendar.hint`, `state.{LIVE,SCHEDULED,GENERATED,NOT_GENERATED,ENDED,MISSING}`, `create`,
`created`, `selected`, `none`, `window`, `materializedAt`, `notMaterialized`, `seed`,
`seed.randomize`, `seed.hint`, `preview`, `preview.hint`, `materialize`, `materialize.hint`,
`materialize.confirm.title`, `materialize.confirm.body`, `materialize.done`, `publish`,
`publish.hint`, `publish.confirm.title`, `publish.confirm.body`, `publish.diffs`, `publish.done`,
`publish.stopped`, `publish.step.ok`, `publish.step.fail`, `archive`, `archive.hint`,
`archive.confirm.title`, `archive.confirm.body`, `archive.done`, `archive.none`, `confirm.type`,
`lineup.{clubs,balls,characters,banner,odds,rows,hash}`, `pinned`, `featured`, `warnings`,
`blocked`, `odds.col.{ref,rarity,weight,p}`, `unpublished`, `loading`, `loadFailed`, `reload`,
`eligible`, `editor.pins`. No player-facing text was added.

---

## Screenshots

Captured by `scripts/shoot.mjs` against the MOCK server (:3100) — every frame carries the
`MOCK DATA — running on local fixtures, no Supabase connection` banner. Production frames were
read in Cesar's Chrome and are QUOTED above, not filed.

| File | What it shows |
|---|---|
| `rotations_workbench_preview.png` | **CANONICAL.** The calendar (GENERATED cells), `wk_2026_38` selected, seed 9999, the pinned preview: 2 clubs (one FEATURED), the ball, the character, the weekly banner, the odds table with the ★ row at 300 / 55 %, the rows/hash line. |
| `rotations_workbench_warnings.png` | The open week: the amber shortfall list (nothing eligible after exclusion on the 2-club mock catalog) and the character it could fill. |
| `rotations_calendar.png` | The panel on load. |
| `rotations_publish_confirm.png` | The five-catalog confirmation with the diff links and `PUBLISH` typed. |
| `rotations_materialize_confirm.png` | The re-materialize confirmation with the rotation id typed. |
| `rotations_workbench_preview_ja.png` | The same preview in Japanese. |
| `users_gacha_tab_pity_group.png` | The Users drawer's Pity section: `weekly · GROUP · 2`, `4 / 50 to Legendary`, Reset enabled. |
| `gacha_banners_pity_group_column.png`, `shop_rotation_column.png` | The two panels with the new columns. |

---

## Deploy

**Dashboard — DONE**, three times (each `npm run deploy` = suite → OpenNext build → wrangler):
`b3d528c0-4148-4269-a20e-ebb9ea47b541` (`05f0f7da1`), `eebbd47e-9ebf-4521-9edc-329aa63fa354`
(`d2dc096c7`), **`0ff72acc-d587-4baa-bfe6-dae2299453ef` (`f8063af6b`, current)**. Live
`/api/version` read in Chrome: `{"commit":"f8063af6b","stamped":true}`. Shell: **302** to
Cloudflare Access. The `PUBLISH ROTATION` literal is in the deployed DICT chunk
(`8561-ebb6e3bf95215fb9.js`) and the panel chunk `app/(panels)/rotations/page-43cc1bd0cb820aa2.js`
was uploaded.

**API (playlife) — no deploy needed.** No Python changed. `flyctl status`: both machines **v74**,
`/health` 200, `/api/v1/gacha/tickets` 403 unauthenticated, content delta 200 for `rotations`
and `gacha_banners`.

**Database:** rotations seed **applied** (data, PostgREST). Pity migration **applied by Cesar**
(SQL editor, Part 1 + Part 2), then proven live (row 7).

---

## Manual verification needed

- Decisions for the Architect (Tuesday, per Cesar): one-ball vs ten-ball listing; the named
  `rotation_*` audit action; ECONOMY_MASTER §3 wording; the two Freda (Supreme) weeks; the
  remaining four shop_stocking club placeholders (`shop_club_driver_gf` collides with
  `wk_2026_39`'s pins) and the missions/loadouts cross-row rules that share the archive-trap
  shape but have no deactivation path today.
- Validator (R1–R4) and generator messages are English-only, following the existing
  `ContentProblem.message` convention (every other rule's text is English too); the DICT carries
  the panel's own strings in both languages.
