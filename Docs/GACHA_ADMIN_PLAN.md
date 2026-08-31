# Gacha — admin-managed, server-rolled · implementation plan

> **STATUS 2026-08-31 — DELIVERED (D pending three operational items).** A `gacha_admin_catalogs`
> DONE (`b42c8bff7`); B `gacha_server_pull` DONE (API v64, ticket ledger, ops panel); C
> `gacha_client_real_pull` DONE (`18d035cfb` — the game pulls the server; every prize kind on the
> club card). D `gacha_ops_polish` code-complete (`c0dfbaab1`…`8c2c34d1e`): rates modal, telemetry
> funnel, Gold ticket, simulate parity, foreground refresh (5b complete), default-ball guard,
> first ticket listing (100 RP / 50 tickets, deactivated until the post-`2afaf0ad5` build).
> Outstanding: Cesar applies `2026_09_02_default_ball_guard.sql`; next archive → reactivate
> `shop_ticket_standard_50`; Code refreshes the D report. Details per spec folder.

**2026-08-31 · Architect (Cowork).** Cesar's requirement, same day: *"move Gacha management to the
online admin like we did all the rest — banners, dates, prizes and drop rates from the admin,
working without a new build; Unity/CSV edits (rates, for example) inform the admin and vice versa,
like clubs and characters. Add any other controls you think we need."*

Everything in §1 was read from `GolfinRedux` (`47caf4bdf`) and `playlife` on 2026-08-31.
Standing invariant unchanged: **a client missing information never shows a broken item and never
wrongly spends** — here, tickets.

---

## 1. What is true today

| Piece | State |
|---|---|
| `Assets/Resources/Data/gacha_banners.csv` | 4 rows; `bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active`. **Not** in `Tools/content/catalogs.py` → no export/import/`--check`, no admin panel |
| `GachaBannerCatalog` (`Assets/Scripts/UI/Gacha/GachaBannerModel.cs`) | static `Resources.Load`, naive `Split(',')`, **no `ContentCatalogStore` overlay**; `GetLiveBanners()` = `Active && EndUtc > UtcNow` — there is no `startUtc`; `nameKey` holds literal display text (`"STANDARD CLUB 1"`, TODO for loc); art = bundled `Resources/Art/Gacha/Banners/<artSprite>` only |
| `GachaBannerCard.Bind` | numeric `costX1/costX10` deliberately NOT shown (authored "COST" label; fields wired, "reserved for the real-pull flow") |
| The pull | `GachaPullFlow.BuildResult(count)` → `GachaMockPrizePool` (10 static clubs). **No ticket spend, no server, no history write, no pity.** `GachaPullFlow.BuildResult` is the declared seam |
| Tickets | `TicketType { Standard = 0 }` only; balances in `SaveData.ticketBalances` → inventory blob (`InventoryProjector`, additive max-merge) — **client-asserted**; `GachaTicketManager.SpendTickets` exists, never called; **dev grant of 10** at three sites (`GachaTicketManager.Awake` + two `SaveSchemaMigrator` blocks, paired TODOs) |
| Ticket faucets | admin grant `golfin_pending_grants(kind='ticket', ref_id=<int kind>)` → `InventoryGrants.Apply` adds to the blob. `missions.csv itemRewards` `GoldTicket x1` and tournament `ticket_gold` are **display-only** — nothing grants them and no `TicketType` exists for them |
| Prizes / reveal | `PrizeRecord` is club-only; reveal modal + Prizes screen bind `BagClubCard` only (`gacha_reveal_animation`, DONE) |
| History | `GachaHistoryStore` = 12 mock records; `GachaHistoryRecord` already models Club/Ball/Character/Item/Ticket, banner, ticket type, x1/x10 |
| Backend | **zero gacha code.** Precedents to copy: `golfin_shop_purchase()` (reads the PUBLISHED `content_rows` row, prices on the server clock, `spend_pts`, writes `golfin_pending_grants`, idempotent by key, kill switch, `min_build`), `golfin_level_up()`, `routers/shop.py` (200-payload outcomes) |
| Admin | 16 catalogs on the shared `CatalogPanel` (`+ New row`, drafts → publish → export); art upload to `catalog-art` (`lib/contentArtMutations.ts`); `mirrorForCatalog` rule for anything a catalog SERVES to a spend path |
| Client art by URL | `CatalogArtCache.Cached(url, bundledUrl)` ladder on `TournamentArtService.CatalogArt` (`content_art_urls`, DONE) |
| Old GOLFIN (Confluence) | ticket types standard/premium/event, per-user pity with a "two pities coincide" bug, history endpoints, banner analytics — the feature checklist, not the design |

---

## 2. Architecture in one paragraph

**Banners, prize pools, rates and ticket types are content catalogs** — CSV ↔ admin two-way for
free (`catalogs.py` + `export/import/--check` + fastlane gate + `CatalogPanel`), bundled floor,
next-launch overlay on the client for what the *screen* shows. **The pull itself is one server
function that reads the PUBLISHED rows** — so a rate or price change is live on the next pull with
no build, cannot be cheated, and the reveal shows exactly what the server granted. No mirror table:
the function reads `content_rows` directly, as `golfin_shop_purchase()` does. Tickets become a
server ledger (the RP shape), because a server that rolls prizes but trusts the client's ticket
count has enforced nothing.

---

## 3. The catalogs (#17–#20)

All under `Assets/Resources/Data/`, seeded by migration via `seed_from_csv.py`, first export
byte-identical (the standing round-trip acceptance). Additive columns only (I4).

**3.1 `gacha_banners`** — existing CSV, id `bannerId`. Keep every column; add:

| Column | Purpose |
|---|---|
| `startUtc` | scheduling (today only `endUtc`); LIVE / SCHEDULED / ENDED badge in the panel |
| `poolId` | which `gacha_pools` pool this banner rolls from |
| `ticketType` | which ticket kind it costs (`ticket_types.id`) |
| `pityThreshold`, `pityMinRarity` | ONE pity per banner: after N pulls without ≥ `pityMinRarity`, the next pull is forced ≥ it; empty = no pity (the old game's "two pities coincide" bug is the argument for one) |
| `guaranteeMinRarityX10` | classic "at least one Rare in a x10"; empty = none |
| `maxPullsPerPlayer` | per-player lifetime cap on this banner; empty = unlimited |
| `artUrl` | admin-uploaded art, ONE image, **no text baked in** (Cesar 2026-08-31: every word on a banner is UI-authored from the row, like the countdown — so the admin controls all of it); `artSprite` stays the bundled floor |
| `nameEn`, `nameJa`, `taglineEn`, `taglineJa` | the card's text, per locale in the row (the `missions` `name_en`/`name_ja` precedent); `nameKey` stays as the fallback for rows that have none |
| `featuredRefIds` | `;`-separated refs shown on the card / rates screen (display only) |

`rulesUrl` stays; see §7 for the in-app rates modal that makes it optional.

**3.2 `gacha_rates`** — id `id`; `poolId, rarity, rateBp`. Basis points, **sum = 10 000 per
pool** (validated, blocking). Six rarity tiers. This is the number the RULES & RATES page publishes.

**3.3 `gacha_pools`** — id `id`; `poolId, kind (club|ball|character|item|ticket), refId, weight,
quantity, dupeRp, featured`. Within a rarity the roll is weighted by `weight`; rarity comes from the
referenced row (never duplicated here). `quantity` for balls/items/tickets. `dupeRp` = RP credited
instead of a club/character the player already owns (§5 decision 3).

Effective per-item odds = `rate(rarity) × weight / Σ weight(rarity, pool)` — computed and displayed,
never stored.

**3.4 `ticket_types`** — id `id` (int, matches `ticketTypeInt` persisted in saves), `key, nameKey,
iconSprite, iconUrl`. Seed `0 = Standard`, `1 = Gold` (closes the `GoldTicket` / `ticket_gold` gap
in missions and tournaments). The client enum stays for persisted ints; names and icons come from
the catalog.

Starting numbers (editable in the admin, not decisions): Common 5500 / Uncommon 2500 / Rare 1200 /
Mythic 550 / Legendary 200 / Supreme 50 bp; pity Legendary at 50; x10 guarantee Rare; `costX1`
50 / `costX10` 450 as today.

---

## 4. Admin — panels and controls

**4.1 Three catalog panels** on the shared `CatalogPanel` (`Gacha Banners`, `Gacha Pools`, `Ticket
Types`; `gacha_rates` edited inside the Pools panel as a per-pool table) with `editorExtras`:

- Banners: LIVE / SCHEDULED / ENDED / OFF badge from the windows (server clock); pool picker;
  ticket-type picker; single art upload to `catalog-art` (`uploadCatalogArt`, `gacha_banners` +
  `artUrl` registered in `contentArtMutations.ts`) with a "no text in the artwork" hint; per-locale
  title/tagline fields; "what build N sees" preview (the `min_build` filter, same as the content
  endpoint).
- Pools: grouped by `poolId`; `RefPicker` against clubs/balls/characters/items with the shop's
  **resolved preview** (name, rarity, thumbnail); per-pool **Rates table** (`gacha_rates`) with live
  %; **effective per-item odds** table; **`Simulate 10 000 pulls`** (pure TS, seeded — expected
  distribution + pity hits) as a pre-publish sanity check.

**4.2 Publish validation** (`contentValidate.ts`, blocking unless marked warn):

1. `gacha_rates`: per pool sums to 10 000; one row per rarity; `rateBp ≥ 0`.
2. `gacha_pools`: `refId` resolves in the catalog for `kind` **and is `is_active`**; `weight > 0`;
   `quantity ≥ 1`; `dupeRp ≥ 0`; `min_build ≥` the ref's `min_build` (shop G2, verbatim).
3. **Every rarity with `rateBp > 0` has ≥ 1 active pool entry** — a roll must never land on
   nothing. Entries whose rarity has rate 0 → **warn** (unreachable).
4. `gacha_banners`: `poolId` resolves; `ticketType` resolves; `costX1 ≥ 0`, `costX10 ≥ 0`;
   `endUtc > startUtc`; `pityMinRarity` / `guaranteeMinRarityX10` are rarities that exist in the
   pool with rate > 0; `sortOrder` unique among active; a LIVE banner has bundled `artSprite` OR an
   art URL; a `min_build` on the banner ≥ max `min_build` of its pool entries.
5. Deactivating a LIVE banner or lowering a live pool → the Banners panel's typed confirmation.

**4.3 `Gacha` ops panel** (live tables, not content — the Tournaments/Rewards posture, `checkAdmin`
+ `writeAudit` on every write):

- **Pause switch**: `content_settings.gacha_enabled` (beside `content_enabled`). The pull function
  refuses `not_available / disabled`; the client shows the banner with pulls disabled and a toast.
  One flag, no deploy — the plan's kill switch (§7 rail 4).
- **Pull log**: `golfin_gacha_pulls` newest-first, filters by user / banner / date, prizes
  expanded; CSV export.
- **Actual vs published odds** per banner (rolled rarity distribution over the last N pulls vs
  `gacha_rates`) — the honest-odds audit; drift beyond a threshold shows amber.
- **Per-user**: pity counters with **reset** (audited), pull count vs `maxPullsPerPlayer`, ticket
  ledger with **grant / adjust** (the Points panel's shape; replaces the blob-only ticket grant).
- Stats cards: pulls/day, tickets sunk/day, top prizes.

**4.4 Users drawer**: ticket balances + last pulls (read-only) beside the inventory tab.

---

## 5. Backend — the pull

Migration `2026_09_xx_golfin_gacha.sql` (full SQL in chat for Cesar; verification block; RLS on,
no policies):

```
golfin_tickets            (user_id, ticket_type int, balance int ≥ 0, updated_at; pk user+type)
golfin_ticket_transactions(id, user_id, ticket_type, delta, reason, idempotency_key, created_at;
                           unique(user_id, idempotency_key))
golfin_gacha_pulls        (id, user_id, banner_id, pool_id, count, ticket_type, cost, results jsonb,
                           pity_before, pity_after, build, idempotency_key, created_at;
                           unique(user_id, idempotency_key))
golfin_gacha_pity         (user_id, banner_id, counter, total_pulls; pk user+banner)
```

`golfin_gacha_pull(p_user_id, p_banner_id, p_count, p_expected_cost, p_key, p_build) returns json`
— `security definer`, EXECUTE revoked, the `golfin_shop_purchase` posture. Business outcomes are
json; only faults raise:

1. **Replay** by `(user, key)` → rebuild `ok` from the stored pull, `replayed: true`.
2. **Kill switches**: `content_enabled`, `gacha_enabled`, `gacha_banners` catalog enabled →
   `not_available / disabled`.
3. **Banner**: published, `is_active`, `startUtc ≤ now < endUtc` on the **server clock**,
   `min_build ≤ p_build` → else `not_available / banner`. `p_count ∈ {1, 10}` else `invalid_count`.
   `maxPullsPerPlayer` reached → `pull_cap`.
4. **Cost**: `costX1` / `costX10` from the row; `p_expected_cost` present and ≠ → **`cost_changed`**
   with the published cost, nothing written (the shop's `price_changed`).
5. **Rollable for this build**: load the pool entries with `min_build ≤ p_build` and active refs;
   every rarity with rate > 0 must keep ≥ 1 entry → else `not_available / pool_for_build`. (The
   client withholds the banner under the same rule, §6; two locks, neither trusts the other.)
6. **Debit tickets**: `golfin_tickets` balance ≥ cost else `insufficient`; write the transaction
   (`gacha:<banner>:x<count>`).
7. **Roll** `p_count` times: pity/guarantee first (forced minimum rarity for the pull that hits the
   threshold, and one slot of a x10 for the guarantee), then rarity by `rateBp`, then item by
   `weight`. Owned club/character → `dupeRp` via `earn_pts_v2` (action `gacha_dupe`, pts NULL,
   capped) instead of the item. Prize kind `ticket` credits `golfin_tickets` directly.
8. **Grant**: every non-dupe prize → `golfin_pending_grants` (kind per entry, `note = pull id`);
   record the pull with `results` in reveal order; update pity. One plpgsql transaction.

Response `ok`: `prizes[] {kind, refId, quantity, rarity, isDupe, dupeRp}`, `ticket_balance`,
`pity {counter, threshold}`, `pulls_used`, plus the RP fields when a dupe paid out.

Router `routers/gacha.py` at `/api/v1/gacha`: `POST /pull` (auth, user from token, body
`{banner_id, count, expected_cost, idempotency_key, build}`), `GET /history?limit=` (own pulls,
newest first — replaces the mock store), `GET /tickets` (balances). Tests `test_gacha_pull.py`
(fake-Supabase style). Deploy, `flyctl status`, smoke (403-not-404).

**Ticket ledger rules.** The ledger is the truth from day one: it starts at **0** (§9 decision 4 —
the blob's 10 were a dev grant), testers are granted from the admin. Every ticket faucet writes the
ledger AND a `pending_grants` row so the client counter follows: admin grant (Users drawer / ops
panel), `golfin_shop_purchase` for a `shop_catalog` row with `category = ticket` (the `GACHA_BUY`
path — small extension, Phase B), mission/tournament ticket rewards (separate quick task, §9 item
7). The client's `ticketBalances` becomes a cache written from server responses, like RP.

---

## 6. Client

- **`GachaBannerCatalog`** gains the standard overlay (`ContentCatalogStore.Catalog("gacha_banners")`
  patch by id, appended rows admitted, `is_active=false` drops, `RequireReady` for EditMode) and
  moves off `Split(',')` onto the parser the other loaders use (NOTE: name it in the spec — the
  quoted-field canonical form the exporter writes must round-trip). Same for `gacha_pools`,
  `gacha_rates`, `ticket_types` (tiny loaders, read-mostly).
- **Withhold rule**: a banner is shown only when its window is open (device clock, now with
  `startUtc`), its pool is rollable for this build (§5 step 5, evaluated locally over the overlaid
  pools/rates), its ticket type resolves, and art resolves (`CatalogArtCache.Cached(artUrl,
  bundledSprite)` ladder → bundled → **withheld**, never blank). Summary warning per load, the
  club-loader shape.
- **Card**: numeric `costX1` / `costX10` shown (the reserved fields — this is the real-pull flow
  they were reserved for); ticket icon from `ticket_types`; title via `LocalizationManager`.
- **`GachaPullService`** in `Golfin.Economy`, mirroring `ShopPurchaseService` exactly (`Instance`,
  `ConfigureForTest`, `PointsBackendFlag` gate inside the routine, in-flight latch, fresh key per
  attempt, `Ok → InventorySyncService.Instance.DrainGrants(...)` so the prizes are in the bag before
  the Prizes screen enters). Outcomes: `Ok, Insufficient, CostChanged,
  PullCap, NotAvailable, Unavailable, Disabled`.
- **`GachaPullFlow.Pull(count)`**: open the reveal modal **immediately** with the bag shaking
  (the shake covers the round trip — no spinner), call the service, on `Ok` hand the modal the
  server's `prizes` in order; on any refusal the modal closes and the outcome toasts
  (`Insufficient` → the existing insufficient copy; `CostChanged` → refresh the card's costs, second
  tap pays; `Disabled` → "Gacha is paused" — strings EN + JA via the importer). `BuildResult`
  disappears; `GachaMockPrizePool` is deleted.
- **`PrizeRecord`** → `{kind, refId, quantity, rarity, isDupe, dupeRp}`; the shared card binder
  gains ball / character / item / ticket (NOTE: card prefab per kind is a spec-time choice —
  `GeneralShopCard` already renders all four kinds in the shop) and a "DUPLICATE → +N RP" treatment.
- **Tickets**: `GachaTicketManager` reads the balance from `/gacha/tickets` at boot and from every
  pull response; `SpendTickets` is deleted (there is no client debit path); the three dev-grant
  sites are reverted (the paired TODO).
- **History**: `GachaHistoryStore` ← `GET /gacha/history`, raw-body disk cache
  (`RemoteNoticeSource` shape), mock removed; the pull response appends locally so the log is
  current without a refetch.
- Gate: `PointsBackendFlag.Enabled` inside the routine, exactly as `ShopPurchaseService` — no new
  flag. Flag OFF = no network and no pull (the mock is gone); the modal closes with the offline
  copy, which is the same posture the shop takes.

---

## 7. Other controls worth having (cheap, all on existing machinery)

- **Rollback** = republish a `content_versions` snapshot — already there; the ops panel's odds
  audit is what tells you to use it.
- **In-app RULES & RATES modal** built from the overlaid `gacha_rates` + pool (effective odds per
  rarity, featured items, pity text — `GACHA_PITY_*` keys already exist); `rulesUrl` becomes
  optional and the button falls back to the modal. Phase D.
- **Telemetry**: `gacha_pull` event (banner, count, rarities, pity hit) on the beta telemetry rail
  + a Telemetry-panel card. Phase D.
- **Scheduling by locale / segment, multi-currency pricing, IAP tickets**: deliberately not here
  (`ECONOMY_MASTER` §2 — RP only, no money).

---

## 8. Sequencing — four specs, each independently shippable

| # | Spec | Delivers | Game risk |
|---|---|---|---|
| A | `gacha_admin_catalogs` | §3 catalogs + seed + `catalogs.py` + three panels + validation + art upload + round-trip. **Game untouched** (the overlay lands in C) | none |
| B | `gacha_server_pull` | §5 migration + function + router + tests + `gacha_enabled` + ops panel (pause, pull log, pity reset, ticket ledger grant) + shop `category = ticket` + **live E2E** on a prod account | none until C |
| C | `gacha_client_real_pull` | §6 — overlay, withhold, art URL, loc keys, numeric costs, `GachaPullService`, multi-kind prizes, server history, ledger-backed tickets, dev-grant revert | medium — the first real spend of tickets |
| D | `gacha_ops_polish` | §7 — in-app rates modal, telemetry, actual-vs-published odds card, `featuredRefIds` on the card | low |

A and B can run in either order; C needs both. Every dashboard-touching spec carries the §23
deployment proofs; B carries the §21 live E2E (pull on prod, verify ledger row, pull row, grant
rows, pity row by SQL, then publish a rate change and pull again with no build).

---

## 9. Decisions of record (Cesar, 2026-08-31)

1. **Rates model** — rarity rate table (bp, sums to 10 000) + within-rarity weights. ✅
2. **Pity** — one pity per banner (`pityThreshold` + `pityMinRarity`, counter per user × banner,
   reset on trigger) + optional x10 guaranteed minimum rarity; **a banner may have NO pity**
   (`pityThreshold` empty or `0` = none — the validator treats both the same). ✅
3. **Duplicates** — an owned club/character rolls into `dupeRp` RP. ✅
4. **Tickets** — server ledger starting at **0**, no grandfathering of the dev 10, testers granted
   from the admin. ✅
5. **Banner delivery — 5b**: content overlay (two-way CSV, drafts/publish/validation/rollback all
   intact) **plus a gacha-only live re-apply**: `GachaBannerCatalog` re-reads on the next Rewards
   Center open after `ContentService.OnCacheRefreshed`. Banner rows have no owned-state dependency,
   so the I5 no-live-swap rule does not apply to them. Windows are evaluated on the device every
   second already, so scheduling is live in either design; the server is authoritative at pull time.
   A pure live endpoint was rejected because it takes banners out of the catalog machinery (no CSV
   round trip, no validation against pools, no `min_build`, no snapshot rollback) for a gain the
   re-apply delivers anyway. Lands in spec C. ✅
6. **Ticket purchase with RP** (`GACHA_BUY`) — in spec B via `shop_catalog category = ticket`. ✅
7. **Banner text is UI-authored** — no words in the artwork; title/tagline per locale live in the
   row and are rendered by the card like the countdown, so the admin controls every word. One
   `artUrl`, not one per locale. ✅
8. **Found, not gacha's scope — later**: mission `itemRewards` (`GoldTicket x1`, `RepairKit x1`)
   and tournament `ticket_gold` are display-only today — nothing grants them. Quick task in
   `missions.py` once `ticket_types` exists. Cesar: yes, later.
