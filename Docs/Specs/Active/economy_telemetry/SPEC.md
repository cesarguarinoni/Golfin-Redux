# SPEC — `economy_telemetry`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-09-11 (Architect via Cowork) from Cesar's requirement the same day: *"add markers
> for telemetry to transactions and any useful visualizers in the web admin … check online so we
> track the correct metrics."* Plan context: `Docs/Economy/MONETIZATION_PLAN.md` §7.
> Everything in §1 was read from `GolfinRedux` and `playlife` on 2026-09-11.
>
> Standing rules: the ledger is the truth, telemetry is the behaviour view (gacha_ops_polish §3);
> dashboard strings in `DICT` en + ja; §23 deployment proofs; **no player-facing text in this
> task**; no Unity UI beyond event hooks.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Every RP transaction becomes **queryable by what it was** (surface, item, rarity, rotation, sale,
build), every spend *intent* the ledger never sees (a BUY tap that failed for lack of RP) is
recorded, and one **Economy** panel in the admin answers the questions a live-ops economy needs
answered weekly — is RP inflating or deflating, what are players spending on, what did this
week's rotation sell, what do players want and can't afford, and (from Phase 1/2 on) what ads
and IAP bring in — using the metric definitions the industry actually uses, so the numbers are
comparable to published benchmarks.

---

## 1. What is true today

| Piece | State |
|---|---|
| Ledger | `points_transactions (user_id, type, amount, currency, description, idempotency_key, created_at)` — every earn (`earn_pts_v2`, `type = action`) and spend (`spend_pts`, `description = reason`, e.g. `shop:<entryId>`, `gacha:<banner>:x<count>`) lands here. **No structured context**: what was bought, its rarity, whether it was a rotation row, on sale, from which build — all inferable only by parsing `description` and joining other tables |
| Domain tables | `golfin_shop_purchases` (entry, category, ref, charged, list_rp, on_sale, build), `golfin_gacha_pulls` (results jsonb, pity, cost), `golfin_ticket_transactions`, `golfin_progress` level-ups, tournament entries, loans (`asset_loans`), gifts (`gift_pts`) — each already rich, none linked to its ledger row |
| Client telemetry | `TelemetryService.RecordSafe(name, () => payload)` → `POST /api/v1/telemetry/events` → `telemetry_events (event_id, user_id, session_id, name, ts, received_at, app_version, build_number, platform, …, payload)`. Names in `TelemetryEventNames` (`TelemetryConfig.cs:50`): session/screen/round/shot/hole/`points_changed`/`level_up`/`daily_pill_tap` + the five `gacha_*` events. **No shop events at all** — a BUY tap that fails on `insufficient` leaves no trace anywhere |
| Admin | `app/(panels)/telemetry/telemetry-panel.tsx`: `Card` KPI tiles, `Section`, a CSS-bar funnel, the Gacha funnel card (`lib/telemetryGacha.ts` pure aggregation + vitest), 7-day range control; `lib/telemetryData.ts::scanEvents(range)`. **No chart library** (package.json: next/react/supabase only) — charts are CSS bars; mock mode via `lib/mockTelemetry.ts` |
| Reference player | `ECONOMY_MASTER` §1: ≈ 300 RP/day earn, ≈ 50 recurring spend, net ≈ 250 (311 with Missions) — the number every "days to afford" reads against |

---

## 2. The metrics, and why these (research 2026-09-11)

Standard definitions so the panel's numbers can be put beside published benchmarks
(D1 ≈ 22 % median / 25–33 % top quartile; D7 ≈ 3.4–3.9 % median; hybrid-casual ARPDAU
$0.15–0.50; rewarded eCPM tier-1 $15–40 — `MONETIZATION_PLAN` §2.4). Economy-health metrics
have no published benchmarks; the rule from the design literature is **source ÷ sink ≈ 1 with
deliberate small imbalances, and watch segment divergence** (power vs casual).

| Group | Metric | Definition (exact) |
|---|---|---|
| **Economy health** | Sources / Sinks per day | Σ ledger `amount` where amount > 0 (by `type`) / Σ where amount < 0 (by reason class) |
| | Sink/source ratio | sinks ÷ sources, daily + 7-day; segment split by player decile of net earn |
| | Net RP creation | sources − sinks per day; cumulative = RP in circulation delta |
| | RP in circulation | Σ `profiles.total_points` (snapshot) — the inflation curve |
| | Balance distribution | histogram of `total_points` over active-7d players (median, p90, p99, Gini) — hoarding signal |
| | Days to afford | (price of a Legendary club 1,500 / Mythic char 1,600 / Supreme 3,000) ÷ median net RP per active-day |
| **Spend mix** | RP spent by category | shop clubs / balls / characters / tickets / items, level-ups, tournament entry, stamina, gacha (tickets), gifts, loans-share — from `meta.category` (§3) |
| | Top items | by units and by RP, 7d / 30d |
| | Rotation sell-through | per `rotation_id`: units per pinned row, buyers ÷ DAU-of-week, RP taken, NEW-row share vs permanent rows |
| | Gacha | pulls/day, tickets sunk, pity hits %, dupe RP refunded, RP → tickets conversion (ticket shop) |
| **Demand** | Wanted-but-couldn't | `shop_buy_result status=insufficient` by item, shortfall distribution (p50/p90) — the pricing signal |
| | Buy funnel | shop_view → buy_tap → ok, by category and by rotation |
| **Engagement × economy** | DAU, sessions/DAU, stickiness DAU/MAU | from `session_start` (already) |
| | Spender segments | players by RP spent 7d: 0 / 1–500 / 501–2,000 / 2,000+ (share and RP share) |
| **Ads (Phase 1)** | Opt-in rate, impressions/DAU, completion, est. revenue = impressions × eCPM/1000, RP granted via ads ÷ total sources | from `ad_*` ledger meta + `ad_view` events (placeholders until Phase 1) |
| **IAP (Phase 2)** | Paying users, conversion (payers ÷ MAU), ARPDAU, ARPPU, revenue by SKU, refund rate | from `golfin_entitlements` (placeholders until 1c) |

Deliberately NOT in this task: LTV, CPI/ROAS (no UA spend), cohort retention curves (the
Telemetry panel's existing tester KPIs cover retention for the beta; a cohort view is a
Deferred row).

---

## 3. Transaction markers — one additive column, written by the functions we own

`2026_09_xx_points_transactions_meta.sql`: `alter table public.points_transactions add column
if not exists meta jsonb;` + a GIN index on `meta`. **Nullable, additive**: the partner app's
writers never touch it (`total_points` invariant untouched). Full SQL in chat for Cesar.

`spend_pts` and `earn_pts_v2` gain an optional trailing `p_meta jsonb default null` (a new
overload — the existing signatures keep working; the routers are not changed) that is written
to the row. Then each function we own passes structured meta:

| Writer | `meta` (all keys optional; `surface` + `category` always) |
|---|---|
| `golfin_shop_purchase` | `{surface:'shop', category, entry_id, ref_id, rarity, rotation_id, on_sale, list_rp, charged, build}` — rarity read from the referenced catalog row, `rotation_id` from `shop_catalog.rotationId` |
| `golfin_gacha_pull` (ticket txn AND the `gacha_dupe` earn) | `{surface:'gacha', category:'gacha', banner_id, pool_id, count, rotation_id, pity_forced}` |
| `golfin_level_up` | `{surface:'roster', category:'level_up', character_id, from_level, to_level}` |
| tournament entry (`tournaments_golfin.py` spend) | `{surface:'tournament', category:'entry', tournament_id, band}` |
| stamina boost purchase | `{surface:'stamina', category:'stamina', shop_id, item_id}` |
| loans 20 % share (`asset_loans`) | `{surface:'loan', category:'loan_share', loan_id, asset_kind, asset_id}` |
| gifts (`golfin_gift_pts` / `golfin_gift_purchase`) | `{surface:'gps_gift', category:'gift', item_id?}` |
| earns via `earn_pts_v2` (hole/replay/1v1/missions/daily/GPS visit) | `{surface, category:'earn', action, hole?, mission_id?, venue_id?}` — the `game_point_actions.action` is already `type`; meta adds the *which* |
| admin grants (`lib/mutations.ts`) | `{surface:'admin', category:'grant', admin_email}` |

Rule (`ECONOMY_MASTER` §2 gets one line): **every new spend or earn path writes `meta.surface`
and `meta.category`**; the admin's Points panel shows `meta` in the row expander so an
unlabelled transaction is visible on sight. Backfill: a one-off migration block parses the
existing `description` prefixes (`shop:`, `gacha:`, `level_up:`, …) into `meta` for rows since
2026-08-12 — best effort, reported counts, never fails the migration.

---

## 4. Client behaviour events (`TelemetryHooks` + call sites, `RecordSafe` only)

| Event | Where | Payload |
|---|---|---|
| `shop_view` | `GeneralShopScreenController.OnEnable` | `tab` (store/gacha/gifts), `category` filter, `rotation_id` (live), `cards`, `balance` |
| `shop_filter_change` | `WireChip` handler | `category` |
| `shop_buy_tap` | `HandleBuy` entry | `entry_id, category, ref_id, rarity, price, on_sale, rotation_id, is_new, balance_before, affordable` |
| `shop_buy_result` | `ShopPurchaseService` outcome (the gacha `gacha_pull_result` shape) | `entry_id, status` (ok/insufficient/price_changed/not_listed/already_owned/unavailable), `shortfall` when insufficient, `latency_ms` |
| `lineup_rollover` | `weekly_rotation_client` roll-over seam | `from_rotation_id, to_rotation_id, open_screen` |
| `level_up_tap` / `level_up_result` | roster BOOST / level-up flow | `character_id, from, to, cost, status` |
| `stamina_buy_result` | stamina shop | `shop_id, item_id, cost, status` |
| `ad_*` | reserved names only (`ad_offer_shown`, `ad_start`, `ad_complete`, `ad_reward`) — constants added, no call sites until Phase 1 |

Constants in `TelemetryEventNames`; a `TelemetryEventNamesTests` pin that every constant is
snake_case and unique (extend if one exists). `points_changed` stays as the reconciliation
signal (client balance vs server ledger).

---

## 5. Admin — **Economy** panel (`app/(panels)/economy/`, route `/economy`, after Telemetry)

Registered in `lib/registry.ts` (`nav.economy` "Economy" / "エコノミー", icon `coins` — add to
`PanelIcon`). Same skeleton as the Telemetry panel: `Section`s, `Card` tiles, the range control
(default 7 d, 30 d option), mock mode. **Charts = small inline-SVG components** in
`components/charts/` (`BarSeries`, `StackedBars`, `Histogram`, `Sparkline`) — no dependency
added; every chart has a `<title>` and a data table toggle (accessibility + copy-paste to Ken).

Data: `lib/economyData.ts` reading three **SQL views** created by the migration (so the
dashboard never scans the ledger raw):

- `v_economy_daily (day, kind source|sink, category, surface, rp, txns, users)` — grouped from
  `points_transactions` + `meta` (rows with null meta fall into `category = 'unlabelled'` — the
  panel shows that share as an amber card so backfill gaps are visible).
- `v_economy_balances (bucket, players)` + scalar `rp_in_circulation`, `median`, `p90`, `p99`
  over players active in the last 7 d (`telemetry_events.session_start` or ledger activity).
- `v_rotation_sales (rotation_id, entry_id, ref_id, rarity, category, units, rp, buyers)` from
  `golfin_shop_purchases` joined to `shop_catalog` published rows.

Pure aggregation in `lib/economyMetrics.ts` (vitest: ratio, net, days-to-afford, segments,
funnel, sell-through) — the `telemetryGacha.ts` pattern.

Sections, top to bottom:

1. **Health** — cards: sink/source ratio (7 d, with the 1.0 rule as the hint), net RP/day, RP in
   circulation (sparkline 30 d), median balance, days-to-afford (Legendary club / Mythic
   character / Supreme). Stacked daily bars: sources by action vs sinks by category.
2. **Spend mix** — stacked bars by category; Top items table (units, RP, buyers).
3. **Rotation** — picker (live + last 8); per-row table: name, rarity, price, units, buyers,
   buyers ÷ DAU-of-week, RP; totals; NEW vs permanent share; a "did the Legendary sell" card.
4. **Demand** — buy funnel (view → tap → ok) by category; wanted-but-couldn't table
   (entry, taps, shortfall p50/p90) sorted by taps.
5. **Gacha** — link + the existing funnel card reused (`telemetryGacha.ts`), plus tickets sunk /
   dupe RP refunded / pity-hit % from `golfin_gacha_pulls`.
6. **Players** — spender segments (share of players / share of RP), balance histogram, DAU and
   stickiness (from the telemetry scan, not recomputed).
7. **Ads · IAP** — the Phase 1 / 1c cards defined in §2, rendering `—` with an "arrives with
   Phase N" hint until their sources exist (so the panel's shape is final now and the specs
   that add ads/IAP only fill data).

Users drawer: the Points tab gains the `meta` expander (surface / category / ref) per row.
Telemetry panel: nothing removed; the Gacha funnel card gets a link to `/economy#gacha`.

Every string in `DICT` en + ja. `npm run deploy` + §23 proofs.

---

## 6. Sequencing

1. playlife: `meta` migration + view migration (**both SQL in chat**), function overloads,
   writers per §3, backfill block; tests `test_points_meta.py` (fake-Supabase style: the
   routers still pass, meta optional). Deploy, smoke.
2. Unity: §4 events + tests; EditMode sweep.
3. Dashboard: §5 panel + charts + vitest + mock; deploy; §23 proofs.
4. Live E2E (§21): one shop purchase, one gacha pull, one level-up on Cesar's account → three
   ledger rows with `meta` (SQL quoted); one deliberate insufficient BUY → `shop_buy_result
   status=insufficient` row with `shortfall`; the Economy panel shows all four within the range.

## 7. Acceptance

- [ ] `meta` column + GIN index live; every §3 writer's row carries `surface` + `category`
      (SQL per writer quoted); backfill counts reported; partner-app writers untouched (diff).
- [ ] Both `spend_pts` / `earn_pts_v2` old signatures still work (router tests green).
- [ ] §4 events arrive in `telemetry_events` from one Editor session (SQL pasted); names pinned
      by test.
- [ ] Economy panel: all seven sections render with live data; the unlabelled share card is
      amber when > 0; days-to-afford uses the live median net earn (formula quoted);
      rotation section shows `wk_2026_38` when live; Ads/IAP cards show placeholders with hints.
- [ ] `economyMetrics` vitest green (ratio, net, histogram buckets, segments, funnel, sell-through,
      days-to-afford), charts have titles + data tables; mock mode exercises every section.
- [ ] Users drawer Points tab shows `meta`.
- [ ] Live E2E §6.4 quoted; deployment id + version stamp; Access 302; `/health` + smoke routes 200.
- [ ] Strings: no player-facing keys in this task (report says so); every dashboard string in
      `DICT` en + ja.

## Files this task touches

**playlife:** `backend/migrations/2026_09_xx_points_transactions_meta.sql`,
`2026_09_xx_economy_views.sql`, `routers/points.py` (optional meta passthrough only),
`routers/telemetry.py` untouched, tests. **Unity:** `Assets/Scripts/Telemetry/TelemetryConfig.cs`,
`Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs`, `GeneralShopScreenController.cs`,
`ShopPurchaseService.cs`, roster level-up flow, stamina shop, tests. **Dashboard:**
`app/(panels)/economy/{page,economy-panel}.tsx`, `components/charts/*`, `components/PanelIcon.tsx`,
`lib/{economyData,economyMetrics,mockEconomy}.ts` (+ tests), `lib/registry.ts`, `lib/i18n.ts`,
`app/(panels)/users/user-drawer.tsx`, `app/(panels)/telemetry/telemetry-panel.tsx` (link).
`Docs/Economy/ECONOMY_MASTER.md` (the meta rule line), `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`.

## Out of scope (Notion Deferred rows filed by the Architect)

- Cohort retention curves by first-purchase week / LTV projection.
- Alerting (Slack/email when sink/source drifts past a threshold).
- Per-player economy timeline in the Users drawer beyond the meta expander.
- A chart library; exporting panels to CSV (the data tables are copy-pasteable).
- Ads and IAP data sources themselves (Phase 1 / 1c specs fill the cards).
