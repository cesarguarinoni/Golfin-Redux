# SPEC — `gacha_server_pull`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-31 (Architect via Cowork). Spec **B** of `Docs/GACHA_ADMIN_PLAN.md` §8.
> Depends on **A** (`gacha_admin_catalogs`) for the four catalogs; A and B may be implemented in
> either order but B's live E2E needs A's seed applied. Decisions of record: plan §9 — rarity bp
> table + weights; pity per banner, **may be none**; dupes → RP; **ticket ledger starts at 0**;
> RP → ticket purchase lives here.
>
> PIPELINE_HARDENING §21 (live E2E, run and pasted) and §23 (dashboard deploy proofs) apply.
> WORKFLOW_NOTES: every migration's full SQL is pasted in chat for Cesar.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

The pull becomes **one server function that reads the PUBLISHED catalogs**: banner window and
price on the server clock, tickets debited from a server ledger, prizes rolled server-side from
`gacha_rates` × `gacha_pools` with pity and x10 guarantee, prizes queued through
`golfin_pending_grants`, the pull recorded — one transaction, idempotent by key, every business
outcome a 200 payload. Plus the ticket ledger and its faucets, a pause switch, and the **Gacha**
ops panel. **No client change** — `GachaPullFlow` still plays the mock until spec C; the
world-check here is curl + SQL.

## 1. What is true today (verified 2026-08-31)

| Piece | State |
|---|---|
| Backend gacha | none. `routers/`: no gacha; `migrations/`: no gacha |
| Template | `2026_08_27_golfin_shop_purchase.sql` → `golfin_shop_purchase(p_user_id, p_entry_id, p_build, p_expected_rp, p_key) returns json`: replay by `(user, key)` → `kill switches` (`content_settings.content_enabled`, `content_catalogs.is_enabled`, fail-open on read error) → row from `content_rows` (`is_active`, `min_build`, windows via `golfin_shop_parse_bound`) → `price_changed` guard → ownership (shop purchases + pending grants + blob) → `spend_pts` → grant insert → purchase row → `ok` json carrying every `spend_pts` field. `security definer`, EXECUTE revoked |
| `spend_pts(p_user_id, p_amount, p_reason, p_key)`, `earn_pts_v2(p_user_id, p_action, p_pts, p_description, p_key)` | `2026_08_12_points_spend_idempotency.sql`. **`earn_pts_v2` does NOT read `game_point_actions` caps** — the router does; a function calling it must cap itself |
| Grants | `golfin_pending_grants(kind in club,character,item,ball,ticket,hole)`; drained by `GET /api/v1/user/golfin-grants`, acked by `POST …/golfin-grants/ack` (`routers/golfin_inventory.py`); admin queues one via `POST /api/users/:id/inventory` (dashboard `app/api/users/[id]/inventory/route.ts`); kind `ticket` carries `ref_id = <ticketTypeInt>` |
| Tickets | client-only: `SaveData.ticketBalances` → blob; dev grant of 10 |
| Router shape | `routers/shop.py` — auth from token, body without user id, 200 payloads, **no `_missing_relation` courtesy**; tests `tests/test_shop_purchase.py` (fake-Supabase harness) |
| Kill switch UI | `app/api/content/enabled/route.ts` writes `content_settings.content_enabled` with audit |
| Users drawer | `inventory-tab.tsx` has a Tickets section reading the blob; `action-modals.tsx` grant modal (`kind === "ticket"` numeric) |
| Catalog data the function reads (from A) | `gacha_banners` (`poolId, ticketType, costX1, costX10, startUtc, endUtc, active, pityThreshold, pityMinRarity, guaranteeMinRarityX10, maxPullsPerPlayer`), `gacha_rates` (`poolId, rarity, rateBp`), `gacha_pools` (`poolId, kind, refId, rarity, weight, quantity, dupeRp`), `ticket_types` (`id`) — all in `content_rows.data` jsonb |

## 2. Migration `2026_09_01_golfin_gacha.sql` (playlife + dashboard copy)

Header/comment/VERIFICATION style of `2026_08_27_golfin_shop_purchase.sql`. RLS on, zero
policies, service-role grants only, on every table. `comment on` every table.

```sql
create table if not exists public.golfin_tickets (
  user_id     uuid    not null,
  ticket_type int     not null,
  balance     int     not null default 0 check (balance >= 0),
  updated_at  timestamptz not null default now(),
  primary key (user_id, ticket_type)
);

create table if not exists public.golfin_ticket_transactions (
  id              uuid primary key default gen_random_uuid(),
  user_id         uuid not null,
  ticket_type     int  not null,
  delta           int  not null check (delta <> 0),
  balance_after   int  not null,
  reason          text not null,           -- 'gacha:<banner>:x<n>' | 'admin_grant' | 'admin_adjust' | 'shop:<entryId>' | 'gacha_prize:<pull>'
  created_by      text,                    -- admin email for admin_*; null otherwise
  idempotency_key uuid not null,
  created_at      timestamptz not null default now(),
  unique (user_id, idempotency_key)
);
create index if not exists golfin_ticket_tx_user_idx on public.golfin_ticket_transactions (user_id, created_at desc);

create table if not exists public.golfin_gacha_pulls (
  id              uuid primary key default gen_random_uuid(),
  user_id         uuid not null,
  banner_id       text not null,
  pool_id         text not null,
  pull_count      int  not null check (pull_count in (1, 10)),
  ticket_type     int  not null,
  cost            int  not null check (cost >= 0),
  pity_before     int  not null,
  pity_after      int  not null,
  pity_forced     boolean not null default false,
  guarantee_forced boolean not null default false,
  build           int  not null default 0,
  idempotency_key uuid not null,
  created_at      timestamptz not null default now(),
  unique (user_id, idempotency_key)
);
create index if not exists golfin_gacha_pulls_user_idx   on public.golfin_gacha_pulls (user_id, created_at desc);
create index if not exists golfin_gacha_pulls_banner_idx on public.golfin_gacha_pulls (banner_id, created_at desc);

create table if not exists public.golfin_gacha_prizes (
  pull_id   uuid not null references public.golfin_gacha_pulls(id) on delete cascade,
  slot      int  not null,                 -- 0..9, reveal order
  kind      text not null check (kind in ('club','ball','character','item','ticket')),
  ref_id    text not null,
  quantity  int  not null default 1 check (quantity >= 1),
  rarity    text not null,
  is_dupe   boolean not null default false,
  dupe_rp   int  not null default 0,
  grant_id  uuid,                          -- golfin_pending_grants.id; null for dupes and tickets
  primary key (pull_id, slot)
);
create index if not exists golfin_gacha_prizes_owned_idx on public.golfin_gacha_prizes (kind, ref_id);

create table if not exists public.golfin_gacha_pity (
  user_id     uuid not null,
  banner_id   text not null,
  counter     int  not null default 0 check (counter >= 0),   -- pulls since the last >= pityMinRarity
  total_pulls int  not null default 0 check (total_pulls >= 0),
  updated_at  timestamptz not null default now(),
  primary key (user_id, banner_id)
);

insert into public.content_settings (key, value) values ('gacha_enabled', true)
on conflict (key) do nothing;

insert into public.game_point_actions (action, pts, max_per_event, daily_cap, once_per_user)
values ('gacha_dupe', null, 1000, null, false)
on conflict (action) do nothing;
```

`golfin_gacha_prizes` is a row table, not a jsonb column, because three readers need to query
it: history, the odds audit, and ownership (§3.3).

### 2.1 `public.golfin_ticket_credit(p_user_id uuid, p_ticket_type int, p_delta int, p_reason text, p_key uuid, p_created_by text) returns json`

The ONLY writer of `golfin_tickets`. `security definer`, EXECUTE revoked from public/anon/
authenticated. Replay by `(user, key)` → returns the stored `balance_after`, `replayed: true`.
`p_delta > 0` credits; `p_delta < 0` debits and returns `{"status":"insufficient","balance":n}`
when the balance would go negative (nothing written). Upserts the balance row `for update`,
inserts the transaction, returns `{"status":"ok","ticket_type","balance","delta","replayed"}`.
`p_ticket_type` must be an active `ticket_types` row in `content_rows` → else
`{"status":"unknown_ticket_type"}`. Called by the pull (debit), the pull (ticket prizes), the
admin grant/adjust, and the shop (§5).

### 2.2 `public.golfin_ref_owned(p_user_id uuid, p_kind text, p_ref text) returns boolean`

Extracted, not duplicated: the ownership test `golfin_shop_purchase` inlines (shop purchases +
pending grants with `applied_at is null` + the inventory blob with the `own = false` character
rule) **plus** `golfin_gacha_prizes` (`kind, ref_id`, `is_dupe = false`) joined to the user's pulls.
`golfin_shop_purchase` is NOT rewritten to call it in this task (minimal diff; follow-up noted in
the report) — except §5, which touches that function anyway; do the swap there if the diff stays
small.

### 2.3 `public.golfin_gacha_pull(p_user_id uuid, p_banner_id text, p_count int, p_expected_cost int, p_key uuid, p_build int) returns json`

`security definer`, `set search_path = public`, `set timezone = 'UTC'`, EXECUTE revoked. Every
business outcome is json; only faults raise. Steps, in order:

1. **Args**: `p_user_id`, `p_key`, `p_banner_id` required (raise); `p_count in (1, 10)` else
   `{"status":"invalid_count"}`.
2. **Replay**: `golfin_gacha_pulls` by `(user, key)` → rebuild the `ok` shape (§2.4) from the
   stored pull + prizes, current ticket balance, `replayed: true`. Read-only.
3. **Kill switches** (fail-open on read error, the shop's truthiness): `content_enabled` false →
   `not_available / disabled`; `gacha_enabled` false → `not_available / paused`;
   `content_catalogs.is_enabled` false for ANY of `gacha_banners`, `gacha_rates`, `gacha_pools`,
   `ticket_types` → `not_available / disabled`.
4. **Banner**: `content_rows` `gacha_banners` / `p_banner_id`; missing → `unknown_banner`;
   `is_active` false or `data->>'active' <> 'true'` → `not_available / inactive`; `min_build >
   p_build` → `not_available / min_build`; `startUtc`/`endUtc` via `golfin_shop_parse_bound`
   (unparseable → `not_available / unparseable_bound`); `now < start` or `now >= end` →
   `not_available / window`.
5. **Ticket type**: `ticketType` resolves to an active `ticket_types` row → else `not_available /
   ticket_type`.
6. **Cap**: `maxPullsPerPlayer` set and `golfin_gacha_pity.total_pulls + p_count >` it →
   `{"status":"pull_cap","limit":n,"used":m}`.
7. **Cost**: `costX1` / `costX10` parsed as the shop parses `rpCost` (blank/invalid → `not_available
   / invalid_price`; **0 is valid** — a free banner). `p_expected_cost is not null and <> cost` →
   `{"status":"cost_changed","cost":n}`; nothing written.
8. **Pool for this build**: rates = active `gacha_rates` rows with `data->>'poolId' = pool`;
   entries = active `gacha_pools` rows with the same `poolId`, `min_build ≤ p_build`, and whose
   ref row (`clubs|balls|characters|items|ticket_types` by `kind`) is `is_active`. Σ `rateBp`
   over the six tiers must be 10 000 (else `not_available / rates`); **every tier with
   `rateBp > 0` must have ≥ 1 entry** (else `not_available / pool_for_build`). This is the server's
   copy of the client withhold rule (spec C) — two locks, neither trusts the other.
9. **Debit**: `golfin_ticket_credit(p_user_id, ticket_type, -cost, 'gacha:'||banner||':x'||count,
   p_key, null)` — `insufficient` returned as-is with the balance (nothing else written). Cost 0
   skips the call.
10. **Roll** (§3) `p_count` slots → prize list. Per prize: `club|character` owned per
    `golfin_ref_owned` → `is_dupe = true`, `dupe_rp` from the entry (0 allowed) credited via
    `earn_pts_v2(p_user_id, 'gacha_dupe', dupe_rp, 'gacha:'||pull_id||':'||ref, md5(p_key||':'||slot)::uuid)`
    when `dupe_rp > 0`, capped at `game_point_actions.gacha_dupe.max_per_event` (the function caps;
    `earn_pts_v2` does not); `ticket` → `golfin_ticket_credit(+quantity, 'gacha_prize:'||pull_id,
    md5(p_key||':'||slot)::uuid)`; everything else → `golfin_pending_grants(kind, ref_id,
    quantity, note = 'gacha:'||pull_id, created_by = 'gacha')`.
11. **Record**: `golfin_gacha_pulls` row, one `golfin_gacha_prizes` row per slot, upsert
    `golfin_gacha_pity` (`counter` per §3, `total_pulls += p_count`). One plpgsql transaction —
    any failure after step 9 rolls the debit back.

### 2.4 `ok` shape

```json
{"status":"ok","pull_id":"…","banner_id":"…","count":10,"ticket_type":0,"charged":450,
 "ticket_balance":12,
 "prizes":[{"slot":0,"kind":"club","ref_id":"club_iron7_mireo","quantity":1,"rarity":"Rare",
            "is_dupe":false,"dupe_rp":0,"grant_id":"…"}, …],
 "pity":{"counter":3,"threshold":50,"min_rarity":"Legendary","forced":false},
 "guarantee_forced":true,
 "pulls_used":14,"pull_limit":null,
 "rp":{"earned":0,"activity_pts":…,"gift_pts":…,"total_points":…},
 "replayed":false}
```

`rp` carries the `earn_pts_v2` balance fields when any dupe paid out (so the client folds the RP
balance the way it folds a spend); `null` otherwise. `prizes` is in **reveal order** = roll
order, with the forced/guarantee slot wherever it landed — no re-sorting by rarity (the reveal
modal's surprise depends on it).

## 3. The roll — one algorithm, two implementations

This is the algorithm `lib/gachaOdds.ts` `simulate()` (spec A) implements in TS. **Both must
match**; §7 tests the match by distribution.

Definitions: `rates[r]` = bp per tier `r` (Common…Supreme, ordered); `entries[r]` = rollable
entries of tier `r`; `≥ r` = tier index ≥ r's index; `pity.counter` = pulls since the last prize
of tier ≥ `pityMinRarity`; pity is ON iff `pityThreshold > 0` (blank/0 = OFF).

For each slot `s = 0 … count-1`:

1. **Pity check** (ON only): if `pity.counter + 1 ≥ pityThreshold` → this slot is **forced**: roll
   the tier among `≥ pityMinRarity` with their `rateBp` renormalised over that subset; if that
   subset sums to 0, take `pityMinRarity` itself. Set `pity_forced = true`.
2. Else roll the tier over all six by `rateBp` (`random() * 10000` walked cumulatively — tiers with
   0 bp are never hit).
3. Roll the entry within the tier by `weight` (cumulative walk over `entries[r]`).
4. Update pity: tier ≥ `pityMinRarity` → `counter = 0`; else `counter += 1`. (OFF: leave 0.)

After the `count` slots, **x10 guarantee** (`count = 10` and `guaranteeMinRarityX10` set): if no
slot is ≥ the guarantee tier, re-roll **the last slot** (`s = 9`) as in step 1 with the subset
`≥ guaranteeMinRarityX10`, set `guarantee_forced = true`, and re-run step 4 for that slot from
the counter value it had before its original roll. The guarantee never lowers a slot.

Dupe detection (step 10 of §2.3) happens after the roll and never changes the roll — published
odds are per-item, ownership-independent (decision 3).

Rarity order and names are the six tiers as written in the catalogs (`Common, Uncommon, Rare,
Mythic, Legendary, Supreme`); compare case-sensitively, exactly as the validator does.

## 4. Router `routers/gacha.py` at `/api/v1/gacha` (mount in `main.py`)

All three routes `Depends(get_current_user)`; user id from the token, never the body. No
`_missing_relation` courtesy. Business outcomes 200; 401/403 auth; 400 malformed; 500 faults.

- `POST /pull` body `{banner_id: str ≤ 80, count: 1|10, expected_cost: int|null,
  idempotency_key: uuid, build: int ≥ 0}` → `golfin_gacha_pull(...)` verbatim.
- `GET /history?limit=50&before=<iso>` → the caller's pulls newest-first with prizes nested
  (one query on pulls + one `in (…)` on prizes — not one per pull), `{"data":{"pulls":[…],
  "next_before":…}}`. `limit` ≤ 200.
- `GET /tickets` → `{"data":{"balances":[{"ticket_type":0,"balance":12},…]}}` from
  `golfin_tickets` (a missing row is balance 0 — that IS a real state, unlike a purchase).

Tests `tests/test_gacha.py`, the `test_shop_purchase.py` harness: 403 unauth; 400 on bad key /
count 5 / negative build; every status in §2.3 passes through as 200; history nesting; tickets
default 0. Deploy `flyctl deploy`, image id via `flyctl status`, smoke: existing routes 200,
`/gacha/pull` 403-not-404, `/gacha/tickets` 403-not-404.

## 5. Ticket faucets — every credit is a ledger write

1. **Admin grant / adjust**: dashboard `POST /api/users/:id/inventory` with `kind = "ticket"` no
   longer inserts a pending grant — it calls `golfin_ticket_credit(user, ref_id::int, amount,
   'admin_grant', uuid, adminEmail)`; a new `adjust` path (negative delta allowed, Points-panel
   posture) with reason `admin_adjust`. Audit actions `ticket_grant` / `ticket_adjust` with
   before/after balance. `inventory-tab.tsx` Tickets section reads `golfin_tickets` (via
   `GET /api/users/:id/inventory` extended), not the blob, and shows the last 20 ticket
   transactions. Blob tickets are labelled "device counter (legacy)" until spec C retires them.
2. **Shop `category = ticket`** — new migration `2026_09_01_shop_purchase_tickets.sql`
   (`create or replace golfin_shop_purchase`, append-only migrations rule): categories gain
   `ticket` with ref catalog `ticket_types`; no uniqueness check for tickets; on `ok` credit via
   `golfin_ticket_credit(+quantity, 'shop:'||entry, md5(p_key||':ticket')::uuid)` where quantity =
   `data->>'quantity'` (new optional `shop_catalog` column, default 1, NUMERIC in the validator)
   and **no pending grant** — the `grant` object in the response is returned with `kind = ticket`,
   `id = null`, `amount = quantity`. NOTE: check `ShopPurchaseResult` (`Assets/Scripts/Economy/
   PointsDtos.cs`) tolerates `grant.id = null`; if not, say so in the report — the client half is
   spec C's and no ticket row may be published before it (next line).
   Dashboard: `SHOP_CATEGORY_TO_CATALOG.ticket = "ticket_types"`, `RefPicker` support, and rule
   **G1-T**: a `category = ticket` row needs `minBuild ≥ TICKET_SHOP_BUILD` (`lib/buildGates.ts`,
   `0` until the spec-C build is archived → error, the `SHOP_CATEGORY_STRICT_BUILD` mechanism
   verbatim). Server tests extend `test_shop_purchase.py`.
3. **Gacha ticket prizes** — §2.3 step 10.
4. Mission / tournament ticket rewards — NOT here (plan §9 item 8).

The `golfin_pending_grants` CHECK keeps `'ticket'` (existing rows); nothing new writes it.

## 6. Dashboard — the **Gacha** ops panel + pause

Read `ADMIN_DASHBOARD_OPS.md` §2–§4. `nav.gacha` ("Gacha" / "ガチャ") in `DICT`, `PanelDef`
before Gacha Banners, icon `ticket`. Live tables, not content — `checkAdmin()` + `writeAudit()`
on every write, mock branch on every read/write, `force-dynamic` everywhere.

- **Pause switch** (top of the panel): `content_settings.gacha_enabled`; `app/api/gacha/enabled/
  route.ts` mirrors `app/api/content/enabled/route.ts` (audit `gacha_pause` / `gacha_resume`);
  typed confirmation to pause (player-facing, instant). Copy: *"Paused: every pull is refused
  with `not_available / paused`; banners stay visible."* en + ja.
- **Pull log**: `golfin_gacha_pulls` newest-first, filters user email (join `profiles`), banner,
  date range; row expands to prizes (kind, ref resolved to name + rarity chip, dupe → RP);
  pity/guarantee flags; page size 50; **Export CSV** of the filtered set.
- **Odds audit** per banner: over the last N pulls (selector 100 / 1 000 / all), rolled tier
  distribution vs `gacha_rates` for the banner's pool, per tier: published %, observed %, delta;
  delta beyond ±2 pt on ≥ 1 000 slots → amber. Forced slots (pity/guarantee) are counted
  separately and excluded from the comparison — they are supposed to skew.
- **Per user** (in the Users drawer, new *Gacha* tab beside Inventory/Missions): ticket balances +
  grant/adjust (§5.1), pity counters per banner with **Reset** (sets `counter = 0`, audit
  `gacha_pity_reset`), `total_pulls` vs cap, last 20 pulls.
- Stats cards: pulls today / 7 d, tickets sunk today / 7 d, dupes paid (RP) 7 d.
- Routes under `app/api/gacha/`: `pulls`, `odds`, `enabled`, `users/[id]/{tickets,pity}`.

Vitest: the odds-audit aggregation (pure function over prize rows) and the CSV export shape.
Deploy: `npm run deploy`, deployment id + footer stamp = HEAD quoted, Access curl → 302.

## 7. Tests + parity

- Backend suite green (`test_gacha.py`, extended `test_shop_purchase.py`).
- **SQL verification block** in the migration (the shop's style): fn present ×3, EXECUTE not
  client-callable, unique indexes, `gacha_enabled` row, `gacha_dupe` action.
- **Roll parity**: a SQL-side harness (a `do $$` block or a test-only function, verification
  section) that calls `golfin_gacha_pull` 2 000 × x10 on a throwaway prod user against
  `banner_standard_club1` and aggregates `golfin_gacha_prizes` by tier, next to
  `simulate(seed, 2 000 × 10)` from `lib/gachaOdds.ts`: every tier within **±1.5 pt** of each
  other AND of published, non-forced slots only; pity hits ≈ expected. Paste both tables. Then
  delete the throwaway user's rows (SQL in chat).
- Dupe path: pull until a club repeats → `is_dupe = true`, `points_transactions` row
  `gacha_dupe` with the entry's `dupeRp`, no grant row.

## 8. Live E2E (§21) — curl + SQL, on a prod account, pasted

1. Grant 1 000 Standard tickets to Cesar's prod account from the Users drawer → `golfin_tickets`
   row + `golfin_ticket_transactions` row `admin_grant` by SQL.
2. `POST /api/v1/gacha/pull` with a real bearer token: x1 → `ok`, ledger −50, pull row, prize
   row, grant row (or dupe RP), pity row; replay same key → `replayed: true`, ledger unchanged.
3. x10 → 10 prizes, `guarantee_forced` observed at least once across a few pulls on
   `banner_standard_club1`.
4. `expected_cost = 999` → `cost_changed`, nothing written (row counts unchanged).
5. **Publish a rate change in the admin** (keep the sum) → next pull rolls under it (paste the
   `gacha_rates` version and the pull's timestamp); **publish `costX1 = 60`** → a pull with
   `expected_cost = 50` → `cost_changed / 60`. No deploy, no build.
6. Pause from the panel → `not_available / paused`; resume → `ok`.
7. `banner_test_a` (no pity): 60 pulls, `pity_forced` never true, `counter` stays 0.
8. Drain: `GET /api/v1/user/golfin-grants` lists the gacha grants with `note = gacha:<pull>`.

## 9. Sequencing

1. §2 migration → SQL in chat → Cesar applies → verification block pasted.
2. §2.1–2.3 functions + §4 router + tests → deploy → smoke.
3. §5 (shop migration + dashboard gates) → SQL in chat → Cesar applies → deploy.
4. §6 panel → `npm run deploy` → §23 proofs.
5. §7 parity, §8 E2E — pasted in the report.
6. Docs: `ADMIN_DASHBOARD_OPS.md` (panel + pause), `Tools/content/README.md` (server reads four
   more catalogs — the `mirrorForCatalog` rule does NOT apply: nothing is mirrored, the function
   reads `content_rows`; say so), `AI_CONTEXT.md`, `TellCode.md`.

## 10. Acceptance (PASS/FAIL with what was measured)

- [ ] §8 steps 1–8 run on prod and pasted (SQL results, curl bodies).
- [ ] §7 parity tables pasted, within ±1.5 pt; throwaway rows deleted.
- [ ] Every §2.3 status reachable in the backend tests; `cost_changed` and `insufficient` write
      nothing (asserted by row counts in the fake).
- [ ] x10 guarantee: with a pool whose Rare+ rate is set to 1 bp in a test fixture, every x10
      still contains ≥ 1 Rare+ and `guarantee_forced = true`.
- [ ] Pity: threshold 3 in a fixture → the 3rd pull after two sub-min prizes is ≥ min and
      `pity_forced = true`; counter resets to 0; `pityThreshold = 0` → never forced.
- [ ] `pool_for_build`: an entry with `min_build = 9999` as the ONLY Supreme entry → pull with
      `build = 2000` → `not_available / pool_for_build`; with `build = 9999` → ok.
- [ ] Admin ticket grant writes the ledger, not `golfin_pending_grants`; Users drawer shows the
      ledger balance and the transaction; adjust −N refuses below 0.
- [ ] Shop: a `category = ticket` row cannot be published while `TICKET_SHOP_BUILD = 0`
      (G1-T); the server function credits tickets for such a row (test only).
- [ ] Panel: pause/resume audited; pull log filters + export; odds audit excludes forced slots
      (fixture-checked in vitest); pity reset audited.
- [ ] Three deploy proofs: `flyctl status` image id ×2 (API, then after §5), dashboard
      deployment id + footer stamp; Access curl 302.
- [ ] Backend suite, dashboard vitest + `npm run build` green. Zero player strings.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files this task touches

**New** — `playlife/backend/migrations/2026_09_01_golfin_gacha.sql`,
`2026_09_01_shop_purchase_tickets.sql` (+ dashboard copies); `playlife/backend/routers/gacha.py`;
`tests/test_gacha.py`; `Tools/admin-dashboard/app/(panels)/gacha/*`, `app/api/gacha/**`,
`app/(panels)/users/gacha-tab.tsx`, `lib/gachaData.ts`, `lib/gachaMutations.ts`, `lib/buildGates.ts`
(`TICKET_SHOP_BUILD`), mock fixtures.

**Modified** — `playlife/backend/main.py`; `tests/test_shop_purchase.py`;
`Tools/admin-dashboard/lib/{i18n,registry,types,contentValidate,contentView}.ts`,
`app/api/users/[id]/inventory/route.ts`, `app/(panels)/users/{inventory-tab,action-modals}.tsx`,
`app/(panels)/shop/shop-panel.tsx` (ticket category); `Docs/ADMIN_DASHBOARD_OPS.md`,
`Tools/content/README.md`, `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`.

## Out of scope (do NOT do these)

- Any Unity change: `GachaPullFlow`, `GachaTicketManager`, the blob, the dev grant, history
  screen, `PointsDtos` (**spec C**).
- The 5b overlay/re-apply, art-by-URL, card text (**spec C**).
- Publishing a real `category = ticket` shop row (needs the spec-C build; G1-T enforces it).
- Mission / tournament ticket grants; telemetry; in-app rates modal (**spec D** / later).
- Rewriting `golfin_shop_purchase` beyond §5.2.
