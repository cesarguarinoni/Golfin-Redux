-- ============================================================================
-- 2026_09_01_golfin_gacha.sql
-- gacha_server_pull — THE PULL BECOMES AUTHORITATIVE.
--
-- Spec:  GolfinRedux/Docs/Specs/Active/gacha_server_pull/SPEC.md §2
-- Plan:  GolfinRedux/Docs/GACHA_ADMIN_PLAN.md §8 spec B, decisions §9
-- Needs: 2026_08_12_points_spend_idempotency.sql   (earn_pts_v2, game_point_actions)
--        2026_08_24_content_catalog.sql            (content_rows / content_catalogs)
--        2026_08_26_content_global_kill_switch.sql (content_settings)
--        2026_08_26_golfin_inventory.sql           (golfin_pending_grants, profiles.golfin_inventory)
--        2026_08_27_golfin_shop_purchase.sql       (golfin_shop_parse_bound, golfin_shop_purchases)
--        2026_08_31_content_gacha_seed.sql         (the four gacha catalogs — spec A)
--
-- WHAT THIS REPLACES. Nothing yet, and that is the point: until now the gacha
--   existed ONLY on the client (`GachaPullFlow` rolls a mock against the bundled
--   CSVs and hands the player the prize itself, tickets counted in a save blob).
--   A modified client could hand itself a Supreme club on every pull and the
--   server would never see a pull happen at all. This migration builds the
--   server half FIRST — schema, ledger, roll — so that when the client half
--   lands (spec C) there is an authority for it to call. The client is NOT
--   changed here.
--
-- ONE FUNCTION, ONE TRANSACTION, exactly as `golfin_shop_purchase` does it.
--   `golfin_gacha_pull` reads the PUBLISHED `gacha_banners` row, checks its
--   window against the SERVER CLOCK, debits a TICKET through the new ledger,
--   rolls the prizes against the PUBLISHED `gacha_rates` x `gacha_pools`, and
--   queues the grants — all inside one plpgsql call, i.e. one transaction. There
--   is therefore no window in which the ticket is gone and the prize does not
--   exist: if anything after the debit fails, the debit rolls back with it.
--
-- TICKETS BECOME A SERVER LEDGER, NOT A COUNTER IN A SAVE FILE. `golfin_tickets`
--   holds the balance and `golfin_ticket_transactions` holds every movement that
--   produced it, with an idempotency key on each. The ONLY writer is
--   `golfin_ticket_credit`; the pull, the admin panel and (from
--   2026_09_01_shop_purchase_tickets.sql) the shop all go through it. Decision
--   of record (plan §9): the ledger STARTS AT ZERO for everyone — the blob
--   counter is not migrated, because it is client-asserted and importing it
--   would launder invented tickets into the authoritative ledger.
--
-- EVERY REFUSAL IS A RETURNED JSON, NEVER AN EXCEPTION. insufficient /
--   cost_changed / pull_cap / unknown_banner / invalid_count / not_available
--   (+reason) are business outcomes the client branches on, exactly like
--   `spend_pts`'s "insufficient" and `golfin_shop_purchase`'s "price_changed".
--   Only a genuine fault raises.
--
-- ── THE ROLL, AND WHY IT IS WRITTEN TWICE ───────────────────────────────────
-- `Tools/admin-dashboard/lib/gachaOdds.ts::simulate()` implements the same
-- three steps in TypeScript so an operator can see the distribution BEFORE
-- publishing. This function is the only authority at runtime; the TS one never
-- decides a real prize. They are pinned to each other by DISTRIBUTION
-- (SPEC §7), not by shared code, because they cannot share code — one runs in
-- Postgres and one in a browser. The order is load-bearing in both:
--
--   1. pity / x10-guarantee decide a FORCED MINIMUM RARITY for the slot
--   2. the rarity is drawn over the six tiers by `rateBp` (10 000 = 100 %)
--   3. the item is drawn INSIDE that rarity by `weight`
--
-- Rarity-then-weight is what makes the published rate table TRUE regardless of
-- how many items sit in a rarity. A flat weighted draw across all rarities
-- would make the rate table a decoration.
--
-- ⚠️ PITY FIRES ON THE `pityThreshold`-TH PULL, i.e. when `counter + 1 >=
-- threshold` (SPEC §3 step 1, and its acceptance test: "threshold 3 → the 3rd
-- pull after two sub-min prizes is forced"). `simulate()` shipped with
-- `counter >= threshold`, which fires one pull later; it is corrected in the
-- same change as this migration so the two agree. See the report's deviations.
--
-- ── DUPES ───────────────────────────────────────────────────────────────────
-- Published odds are PER ITEM and ownership-independent (plan §9 decision 3):
-- owning a club does not change its chance of being rolled, it changes what the
-- roll PAYS. A duplicate club/character pays `dupeRp` into RP through
-- `earn_pts_v2` and queues no grant. Ownership is evaluated slot by slot as the
-- grants are written, so a x10 that rolls the same club twice pays the second
-- one as a dupe — which is what a player would see happen on any other gacha.
--
-- STATUS: NOT YET APPLIED. Paste into the Supabase SQL editor (project
--   wmszyghwwkaptgqdunel), then run the VERIFICATION block at the bottom and
--   paste its output back.
--   The Supabase SQL editor will warn "creates a table without enabling RLS" on
--   each `create table` — a FALSE POSITIVE; it lints the statement in isolation
--   and the enable comes later. `*_rls = 1` with `*_policies = 0` is the proof,
--   and zero policies IS deny-all.
-- Idempotent: safe to re-run.
-- ============================================================================


-- ============================================================================
-- §2a  The ticket ledger — balance + movements
-- ============================================================================
-- TWO TABLES, NOT ONE, for the reason `golfin_shop_purchases` is not
-- `points_transactions`: the balance is what the game asks for on every screen
-- and must be one indexed read, and the movements are what an operator needs
-- when a player says "I had ten". A single append-only table would make the
-- common read a SUM over history; a single balance row would make the support
-- question unanswerable.

create table if not exists public.golfin_tickets (
  user_id     uuid        not null,
  ticket_type int         not null,
  balance     int         not null default 0 check (balance >= 0),
  updated_at  timestamptz not null default now(),
  primary key (user_id, ticket_type)
);

comment on table public.golfin_tickets is
  'Authoritative ticket balance per (player, ticket type). A MISSING ROW IS A '
  'REAL BALANCE OF ZERO, not an error — the ledger starts empty for everyone '
  '(plan §9: the client blob counter is deliberately not migrated). Written '
  'only by public.golfin_ticket_credit().';

create table if not exists public.golfin_ticket_transactions (
  id              uuid        primary key default gen_random_uuid(),
  user_id         uuid        not null,
  ticket_type     int         not null,
  delta           int         not null check (delta <> 0),
  balance_after   int         not null,
  reason          text        not null,
  created_by      text,
  idempotency_key uuid        not null,
  created_at      timestamptz not null default now(),
  unique (user_id, idempotency_key)
);

comment on table public.golfin_ticket_transactions is
  'Every movement of every ticket balance. reason is '
  '''gacha:<banner>:x<n>'' | ''admin_grant'' | ''admin_adjust'' | '
  '''shop:<entryId>'' | ''gacha_prize:<pullId>''. created_by carries the admin '
  'email for the admin_* reasons and is null otherwise. (user_id, '
  'idempotency_key) is the replay ledger.';

-- The drawer reads "this player's last N movements", newest first.
create index if not exists golfin_ticket_tx_user_idx
  on public.golfin_ticket_transactions (user_id, created_at desc);


-- ============================================================================
-- §2b  The pull ledger
-- ============================================================================
-- Replay ledger for the whole call (step 2) AND the row the admin Gacha panel's
-- pull log and odds audit are built from. `pity_forced` / `guarantee_forced`
-- are stored rather than recomputed because the odds audit EXCLUDES forced
-- slots from its comparison — a forced slot is supposed to skew, and an audit
-- that could not tell them apart would flag every pity-heavy banner as broken.

create table if not exists public.golfin_gacha_pulls (
  id               uuid        primary key default gen_random_uuid(),
  user_id          uuid        not null,
  banner_id        text        not null,
  pool_id          text        not null,
  pull_count       int         not null check (pull_count in (1, 10)),
  ticket_type      int         not null,
  cost             int         not null check (cost >= 0),
  pity_before      int         not null,
  pity_after       int         not null,
  pity_forced      boolean     not null default false,
  guarantee_forced boolean     not null default false,
  build            int         not null default 0,
  idempotency_key  uuid        not null,
  created_at       timestamptz not null default now(),
  unique (user_id, idempotency_key)
);

comment on table public.golfin_gacha_pulls is
  'One row per server-authoritative pull. Replay ledger (user_id, '
  'idempotency_key). cost 0 is legal — a free banner. Written only by '
  'public.golfin_gacha_pull().';

create index if not exists golfin_gacha_pulls_user_idx
  on public.golfin_gacha_pulls (user_id, created_at desc);

create index if not exists golfin_gacha_pulls_banner_idx
  on public.golfin_gacha_pulls (banner_id, created_at desc);

-- A ROW TABLE, NOT A jsonb COLUMN ON THE PULL, because three readers query it:
-- the history endpoint, the odds audit (group by rarity), and ownership
-- (§2.2 — "has this player ever been paid this club by the gacha?"). A jsonb
-- column would make all three a scan.
create table if not exists public.golfin_gacha_prizes (
  pull_id  uuid    not null references public.golfin_gacha_pulls(id) on delete cascade,
  slot     int     not null,
  kind     text    not null check (kind in ('club','ball','character','item','ticket')),
  ref_id   text    not null,
  quantity int     not null default 1 check (quantity >= 1),
  rarity   text    not null,
  is_dupe  boolean not null default false,
  dupe_rp  int     not null default 0,
  grant_id uuid,
  primary key (pull_id, slot)
);

comment on table public.golfin_gacha_prizes is
  'One row per prize slot, in REVEAL ORDER (slot = roll order, forced slot '
  'wherever it landed). dupe_rp is the RP ACTUALLY CREDITED after the '
  'game_point_actions.gacha_dupe cap, not the catalog''s advertised number, so '
  'the panel''s "dupes paid" total is the truth. grant_id is null for dupes and '
  'for ticket prizes (those credit the ticket ledger instead).';

create index if not exists golfin_gacha_prizes_owned_idx
  on public.golfin_gacha_prizes (kind, ref_id);


-- ============================================================================
-- §2c  Pity — per player, per banner
-- ============================================================================
-- PER BANNER, not per pool: two banners can share a pool and still be different
-- promises to the player ("50 pulls on THIS banner"). `counter` is pulls since
-- the last prize of at least `pityMinRarity`; `total_pulls` is the lifetime
-- count the `maxPullsPerPlayer` cap is measured against.

create table if not exists public.golfin_gacha_pity (
  user_id     uuid        not null,
  banner_id   text        not null,
  counter     int         not null default 0 check (counter >= 0),
  total_pulls int         not null default 0 check (total_pulls >= 0),
  updated_at  timestamptz not null default now(),
  primary key (user_id, banner_id)
);

comment on table public.golfin_gacha_pity is
  'Pity counter and lifetime pull count per (player, banner). counter is pulls '
  'since the last prize of at least the banner''s pityMinRarity and stays 0 on '
  'a banner with no pity. total_pulls is what maxPullsPerPlayer is measured '
  'against. Admin "Reset pity" writes counter = 0 and never touches '
  'total_pulls.';


-- ============================================================================
-- §2d  RLS — on, with no policies, on all five
-- ============================================================================
-- service_role bypasses RLS, so the API writes and the admin dashboard reads,
-- while anon/authenticated get nothing over PostgREST. Same posture as
-- golfin_shop_purchases, golfin_pending_grants and the content tables. A tester
-- must not be able to read, let alone insert, a ticket balance directly — the
-- whole point of moving tickets off the device is that the client stops being
-- the authority on how many it has.

alter table public.golfin_tickets              enable row level security;
alter table public.golfin_ticket_transactions  enable row level security;
alter table public.golfin_gacha_pulls          enable row level security;
alter table public.golfin_gacha_prizes         enable row level security;
alter table public.golfin_gacha_pity           enable row level security;

revoke all on table public.golfin_tickets             from anon, authenticated;
revoke all on table public.golfin_ticket_transactions from anon, authenticated;
revoke all on table public.golfin_gacha_pulls         from anon, authenticated;
revoke all on table public.golfin_gacha_prizes        from anon, authenticated;
revoke all on table public.golfin_gacha_pity          from anon, authenticated;

grant select, insert, update, delete on table public.golfin_tickets             to service_role;
grant select, insert, update, delete on table public.golfin_ticket_transactions to service_role;
grant select, insert, update, delete on table public.golfin_gacha_pulls         to service_role;
grant select, insert, update, delete on table public.golfin_gacha_prizes        to service_role;
grant select, insert, update, delete on table public.golfin_gacha_pity          to service_role;


-- ============================================================================
-- §2e  The pause switch + the dupe earn action
-- ============================================================================
-- `gacha_enabled` is a SECOND, narrower kill switch beside `content_enabled`.
-- Pulling remote content globally would also close the shop, the missions and
-- the mode fees; pausing the gacha must not. The admin panel writes this row
-- and `golfin_gacha_pull` reads it (step 3).

insert into public.content_settings (key, value) values ('gacha_enabled', true)
on conflict (key) do nothing;

-- `pts` is NULL because the amount is the POOL ENTRY's `dupeRp`, which varies
-- per item — the same "blank pts means the caller supplies the amount, bounded
-- by the caps" mode hole scores and tournament prizes use. `max_per_event` 1000
-- is the ceiling a single dupe may pay; the Supreme club in the seeded pool
-- pays 600, so the cap is headroom, not a squeeze. NO daily_cap: a player who
-- spends 200 tickets in a day should be paid for all of them, and the ticket
-- price is the real limiter.
insert into public.game_point_actions (action, pts, max_per_event, daily_cap, once_per_user)
values ('gacha_dupe', null, 1000, null, false)
on conflict (action) do nothing;


-- ============================================================================
-- §2.1  public.golfin_ticket_credit — the ONLY writer of golfin_tickets
-- ============================================================================
-- Every faucet and every drain goes through here: the pull's debit, the pull's
-- ticket prizes, the admin grant/adjust, and the shop's ticket category. One
-- writer is what makes "the ledger and the balance agree" a property of the
-- schema rather than a thing four call sites each have to remember.
--
-- REPLAY BY (user, key) returns the STORED balance_after, not the current
-- balance: the answer to "what did this call do" must not change because a
-- later call moved the balance. `replayed: true` says which it is.
--
-- A DEBIT THAT WOULD GO NEGATIVE WRITES NOTHING and returns `insufficient`
-- with the balance, so the same key can succeed later once the player has the
-- tickets — the same contract `spend_pts` has for RP.

create or replace function public.golfin_ticket_credit(
  p_user_id     uuid,
  p_ticket_type int,
  p_delta       int,
  p_reason      text,
  p_key         uuid,
  p_created_by  text
)
returns json
language plpgsql
security definer
set search_path = public
as $$
declare
  v_prior   public.golfin_ticket_transactions%rowtype;
  v_balance int;
  v_active  boolean;
begin
  if p_user_id is null then
    raise exception 'golfin_ticket_credit: p_user_id is required';
  end if;
  if p_key is null then
    raise exception 'golfin_ticket_credit: p_key (idempotency key) is required';
  end if;
  if p_ticket_type is null then
    raise exception 'golfin_ticket_credit: p_ticket_type is required';
  end if;
  if coalesce(p_delta, 0) = 0 then
    raise exception 'golfin_ticket_credit: p_delta must be non-zero';
  end if;
  if p_reason is null or btrim(p_reason) = '' then
    raise exception 'golfin_ticket_credit: p_reason is required';
  end if;

  -- ── REPLAY ───────────────────────────────────────────────────────────────
  select * into v_prior
    from public.golfin_ticket_transactions
   where user_id = p_user_id and idempotency_key = p_key
   limit 1;

  if found then
    return json_build_object(
      'status',      'ok',
      'ticket_type', v_prior.ticket_type,
      'balance',     v_prior.balance_after,
      'delta',       v_prior.delta,
      'replayed',    true
    );
  end if;

  -- ── THE TYPE MUST BE A PUBLISHED, ACTIVE ticket_types ROW ────────────────
  -- Ticket type ids are persisted in player saves and referenced by banners and
  -- shop rows; crediting a type nobody publishes creates a balance no screen can
  -- ever name. Refused as a business outcome, not an exception: an admin typing
  -- 7 into the grant modal is a mistake to report, not a 500.
  select is_active into v_active
    from public.content_rows
   where catalog = 'ticket_types' and row_id = p_ticket_type::text
   limit 1;

  if not found or v_active is not true then
    return json_build_object('status', 'unknown_ticket_type', 'ticket_type', p_ticket_type);
  end if;

  -- ── LOCK THE BALANCE ROW ─────────────────────────────────────────────────
  -- Insert-or-lock in ONE statement, and the no-op `do update` is the whole
  -- point of it.
  --
  -- ⚠️ `on conflict DO NOTHING` FOLLOWED BY A SELECT IS THE CLASSIC UPSERT RACE
  -- AND MUST NOT BE USED HERE. Under READ COMMITTED, if a concurrent
  -- transaction inserted the row and committed after this statement's snapshot
  -- was taken, DO NOTHING yields — and the following `select … for update`
  -- finds nothing, because the row is invisible to that snapshot. The balance
  -- would read as 0, the `update` would match no rows, and a transaction row
  -- would be written claiming a `balance_after` that was never true.
  --
  -- `DO UPDATE` takes the row lock and RETURNS the live row in both branches,
  -- so there is no window and no second read. The SET is a genuine no-op —
  -- `updated_at` is written properly a few lines down.
  insert into public.golfin_tickets (user_id, ticket_type, balance)
  values (p_user_id, p_ticket_type, 0)
  on conflict (user_id, ticket_type) do update
     set updated_at = golfin_tickets.updated_at
  returning balance into v_balance;

  v_balance := coalesce(v_balance, 0);

  if v_balance + p_delta < 0 then
    return json_build_object(
      'status',      'insufficient',
      'ticket_type', p_ticket_type,
      'balance',     v_balance,
      'requested',   -p_delta,
      'shortfall',   -(v_balance + p_delta)
    );
  end if;

  v_balance := v_balance + p_delta;

  update public.golfin_tickets
     set balance = v_balance, updated_at = now()
   where user_id = p_user_id and ticket_type = p_ticket_type;

  insert into public.golfin_ticket_transactions
    (user_id, ticket_type, delta, balance_after, reason, created_by, idempotency_key)
  values
    (p_user_id, p_ticket_type, p_delta, v_balance, btrim(p_reason),
     nullif(btrim(coalesce(p_created_by, '')), ''), p_key);

  return json_build_object(
    'status',      'ok',
    'ticket_type', p_ticket_type,
    'balance',     v_balance,
    'delta',       p_delta,
    'replayed',    false
  );
end;
$$;

revoke execute on function public.golfin_ticket_credit(uuid, int, int, text, uuid, text)
  from public, anon, authenticated;
grant execute on function public.golfin_ticket_credit(uuid, int, int, text, uuid, text)
  to service_role;


-- ============================================================================
-- §2.2  public.golfin_ref_owned — "does this player already have it?"
-- ============================================================================
-- EXTRACTED, NOT DUPLICATED. `golfin_shop_purchase` step 8 inlines three of
-- these four reads; this adds the fourth (a non-dupe gacha prize) and gives the
-- gacha one name to call. Four sources, in decreasing order of how much they can
-- be trusted:
--
--   a) a prior shop purchase        — authoritative, written by the server
--   b) a non-dupe gacha prize       — authoritative, written by the server
--   c) an UNAPPLIED pending grant   — authoritative, the item is already owed
--   d) the inventory blob           — BEST EFFORT, it is client-asserted
--
-- (d) exists so a club the player got from the starter set or an admin grant is
-- read as owned; it is not something to rely on. A malformed blob must never
-- make a legitimate prize into a dupe, hence the exception block: on any error
-- the blob contributes nothing.
--
-- `golfin_shop_purchase` is deliberately NOT rewritten to call this in this
-- migration (minimal diff; the shop's copy is proven in production and a
-- behaviour-preserving swap is worth its own change).

create or replace function public.golfin_ref_owned(
  p_user_id uuid,
  p_kind    text,
  p_ref     text
)
returns boolean
language plpgsql
stable
security definer
set search_path = public
as $$
declare
  v_kind     text := lower(btrim(coalesce(p_kind, '')));
  v_ref      text := btrim(coalesce(p_ref, ''));
  v_blob     jsonb;
  v_elem     jsonb;
  v_blob_key text;
begin
  if p_user_id is null or v_ref = '' then
    return false;
  end if;

  -- Only club and character are unique. An item, a ball or a ticket STACKS, so
  -- "owned" is not a question that has an answer for them.
  if v_kind not in ('club', 'character') then
    return false;
  end if;

  perform 1
     from public.golfin_shop_purchases
    where user_id = p_user_id and category = v_kind and ref_id = v_ref
    limit 1;
  if found then return true; end if;

  perform 1
     from public.golfin_gacha_prizes z
     join public.golfin_gacha_pulls g on g.id = z.pull_id
    where g.user_id = p_user_id
      and z.kind = v_kind
      and z.ref_id = v_ref
      and z.is_dupe = false
    limit 1;
  if found then return true; end if;

  perform 1
     from public.golfin_pending_grants
    where user_id = p_user_id and kind = v_kind and ref_id = v_ref
      and applied_at is null
    limit 1;
  if found then return true; end if;

  -- InventoryCodec wire keys: KClubs="clubs", KChars="characters", KId="id".
  -- A default-state entry is a BARE ID STRING; a non-default one is an object
  -- carrying "id". For a character the codec writes "own" ONLY when it is
  -- FALSE, so a present row with no "own" key is owned.
  v_blob_key := case when v_kind = 'club' then 'clubs' else 'characters' end;

  begin
    select golfin_inventory into v_blob from public.profiles where id = p_user_id;

    if v_blob is not null and jsonb_typeof(v_blob -> v_blob_key) = 'array' then
      for v_elem in select jsonb_array_elements(v_blob -> v_blob_key)
      loop
        if jsonb_typeof(v_elem) = 'string' and (v_elem #>> '{}') = v_ref then
          return true;
        elsif jsonb_typeof(v_elem) = 'object' and (v_elem->>'id') = v_ref then
          if v_kind = 'character' and (v_elem->>'own') = 'false' then
            null;
          else
            return true;
          end if;
        end if;
      end loop;
    end if;
  exception when others then
    return false;
  end;

  return false;
end;
$$;

revoke execute on function public.golfin_ref_owned(uuid, text, text)
  from public, anon, authenticated;
grant execute on function public.golfin_ref_owned(uuid, text, text)
  to service_role;




-- ============================================================================
-- §2.3a  The two draws, extracted so the roll reads like its TS twin
-- ============================================================================
-- These are the plpgsql halves of `gachaOdds.ts`'s `drawRarity` and
-- `drawEntry`. They are separate functions for ONE reason: the roll calls each
-- of them from more than one place (the normal slot, the pity fallback, the x10
-- guarantee re-roll), and three inline copies of a cumulative weight walk is
-- three places for the walk to drift apart. The parity claim in SPEC §7 is a
-- claim about these twelve lines; they should be readable next to the TS.
--
-- NOT part of the module's contract — service_role only, same as everything
-- else here — and VOLATILE, because they call random().

create or replace function public.golfin_gacha_draw_tier(
  p_bp     int[],   -- basis points per ladder index 1..6
  p_weight int[],   -- total rollable entry weight per ladder index 1..6
  p_floor  int      -- lowest ladder index allowed (1 = no floor)
)
returns int
language plpgsql
volatile
as $$
declare
  v_total  int := 0;
  v_i      int;
  v_ticket double precision;
begin
  -- A tier with rate but no rollable entry is NOT a candidate. On a valid pool
  -- step 8 has already refused that case; the filter is here so the fallback
  -- paths (pity subset, guarantee subset) cannot pick an unpayable tier.
  for v_i in greatest(coalesce(p_floor, 1), 1) .. 6 loop
    if p_bp[v_i] > 0 and p_weight[v_i] > 0 then
      v_total := v_total + p_bp[v_i];
    end if;
  end loop;

  if v_total <= 0 then
    return null;
  end if;

  -- `random() * total` is in [0, total), so the LAST eligible bucket always wins
  -- the residue and floating-point drift can never fall through to null.
  v_ticket := random() * v_total;
  for v_i in greatest(coalesce(p_floor, 1), 1) .. 6 loop
    if p_bp[v_i] > 0 and p_weight[v_i] > 0 then
      v_ticket := v_ticket - p_bp[v_i];
      if v_ticket < 0 then
        return v_i;
      end if;
    end if;
  end loop;

  for v_i in reverse 6 .. greatest(coalesce(p_floor, 1), 1) loop
    if p_bp[v_i] > 0 and p_weight[v_i] > 0 then
      return v_i;
    end if;
  end loop;
  return null;
end;
$$;

create or replace function public.golfin_gacha_draw_entry(
  p_entries jsonb,   -- the pool, already filtered to what THIS build can roll
  p_rarity  text,
  p_total   int      -- Σ weight of p_rarity's rollable entries
)
returns jsonb
language plpgsql
volatile
as $$
declare
  v_row    jsonb;
  v_last   jsonb;
  v_ticket double precision;
begin
  if coalesce(p_total, 0) <= 0 then
    return null;
  end if;

  v_ticket := random() * p_total;
  for v_row in
    select x from jsonb_array_elements(p_entries) x
     where x->>'rarity' = p_rarity and (x->>'weight')::int > 0
  loop
    v_last   := v_row;
    v_ticket := v_ticket - (v_row->>'weight')::int;
    if v_ticket < 0 then
      return v_row;
    end if;
  end loop;

  return v_last;
end;
$$;

revoke execute on function public.golfin_gacha_draw_tier(int[], int[], int)
  from public, anon, authenticated;
grant execute on function public.golfin_gacha_draw_tier(int[], int[], int)
  to service_role;

revoke execute on function public.golfin_gacha_draw_entry(jsonb, text, int)
  from public, anon, authenticated;
grant execute on function public.golfin_gacha_draw_entry(jsonb, text, int)
  to service_role;


-- ============================================================================
-- §2.3  public.golfin_gacha_pull — read the banner, debit, roll, grant, record
-- ============================================================================

create or replace function public.golfin_gacha_pull(
  p_user_id       uuid,
  p_banner_id     text,
  p_count         int,
  p_expected_cost int,
  p_key           uuid,
  p_build         int
)
returns json
language plpgsql
security definer
set search_path = public
set timezone = 'UTC'
as $$
declare
  -- The six tiers, in ladder order, EXACTLY as the catalogs and
  -- contentValidate.RARITIES spell them. Compared case-sensitively: a row
  -- saying "rare" is an authoring error the validator already refuses, and
  -- silently accepting it here would make the published table a lie.
  c_ladder constant text[] := array['Common','Uncommon','Rare','Mythic','Legendary','Supreme'];

  v_banner      text := btrim(p_banner_id);
  v_now         timestamptz := now();

  -- replay
  v_prior       public.golfin_gacha_pulls%rowtype;

  -- kill switches
  v_flag        boolean;
  v_cat_off     int;

  -- banner
  v_bdata       jsonb;
  v_bmin        int;
  v_bactive     boolean;
  v_ok          boolean;
  v_start       timestamptz;
  v_end         timestamptz;
  v_pool        text;
  v_ticket_raw  text;
  v_ticket_type int;
  v_tt_active   boolean;

  -- cost
  v_cost_raw    text;
  v_cost        int;

  -- pity / cap
  v_counter     int := 0;
  v_total       int := 0;
  v_cap_raw     text;
  v_cap         int;
  v_thr         int := 0;
  v_pity_min    text;
  v_pity_rank   int := 0;
  v_pity_on     boolean := false;
  v_guar        text;
  v_guar_rank   int := 0;

  -- rates + entries
  v_bp          int[] := array[0,0,0,0,0,0];
  v_rate_sum    int := 0;
  v_entries     jsonb := '[]'::jsonb;
  v_tier_w      int[] := array[0,0,0,0,0,0];
  v_w           int;
  v_rate_row    record;
  v_i           int;

  -- the roll
  v_slot        int;
  v_floor       int;
  v_forced      boolean;
  v_rank        int;
  v_entry       jsonb;
  v_slot_rank   int[] := '{}';
  v_slot_entry  jsonb := '[]'::jsonb;
  v_slot_before int[] := '{}';
  v_slot_pity   boolean[] := '{}';
  v_best        int := 0;
  v_pity_forced boolean := false;
  v_guar_forced boolean := false;

  -- debit + payout
  v_credit      json;
  v_status      text;
  v_pull_id     uuid;
  v_kind        text;
  v_ref         text;
  v_qty         int;
  v_dupe_rp     int;
  v_dupe_cap    int;
  v_is_dupe     boolean;
  v_grant_id    uuid;
  v_rp_earned   int := 0;
  v_rp          json := null;
  v_balance     int := 0;
  v_prizes      json;
begin
  -- ── 1. ARGUMENTS ─────────────────────────────────────────────────────────
  if p_user_id is null then
    raise exception 'golfin_gacha_pull: p_user_id is required';
  end if;
  if p_key is null then
    raise exception 'golfin_gacha_pull: p_key (idempotency key) is required';
  end if;
  if v_banner is null or v_banner = '' then
    raise exception 'golfin_gacha_pull: p_banner_id is required';
  end if;

  -- x1 and x10 are the only two things the UI can ask for, and the pull row's
  -- CHECK says so too. A business outcome rather than an exception so a client
  -- bug reads as a refusal, not as "the server is down".
  if p_count is null or p_count not in (1, 10) then
    return json_build_object('status', 'invalid_count', 'count', p_count);
  end if;

  -- ── 2. REPLAY ────────────────────────────────────────────────────────────
  -- Read-only, and rebuilt from the STORED pull + prizes rather than by rolling
  -- again: a replay must return the same prizes the player already saw.
  -- Balances are read fresh, the way golfin_shop_purchase's replay reads points.
  select * into v_prior
    from public.golfin_gacha_pulls
   where user_id = p_user_id and idempotency_key = p_key
   limit 1;

  if found then
    select coalesce(json_agg(json_build_object(
             'slot', z.slot, 'kind', z.kind, 'ref_id', z.ref_id,
             'quantity', z.quantity, 'rarity', z.rarity,
             'is_dupe', z.is_dupe, 'dupe_rp', z.dupe_rp, 'grant_id', z.grant_id
           ) order by z.slot), '[]'::json)
      into v_prizes
      from public.golfin_gacha_prizes z
     where z.pull_id = v_prior.id;

    -- dupe_rp is the amount ACTUALLY credited (post-cap), which is why the
    -- replay can rebuild `rp.earned` by summing it rather than re-reading the
    -- points ledger.
    select coalesce(sum(z.dupe_rp), 0) into v_rp_earned
      from public.golfin_gacha_prizes z
     where z.pull_id = v_prior.id and z.is_dupe;

    select coalesce(balance, 0) into v_balance
      from public.golfin_tickets
     where user_id = p_user_id and ticket_type = v_prior.ticket_type;
    v_balance := coalesce(v_balance, 0);

    if v_rp_earned > 0 then
      select json_build_object(
               'earned',       v_rp_earned,
               'activity_pts', coalesce(activity_pts, 0),
               'gift_pts',     coalesce(gift_pts, 0),
               'total_points', coalesce(total_points, 0))
        into v_rp
        from public.profiles where id = p_user_id;
    end if;

    select counter, total_pulls into v_counter, v_total
      from public.golfin_gacha_pity
     where user_id = p_user_id and banner_id = v_prior.banner_id;

    -- The banner's pity/cap numbers are re-read LIVE and are display only; the
    -- numbers that matter (what was charged, what was rolled) come from the
    -- stored row. A banner deleted since the pull leaves them null rather than
    -- making the replay fail.
    select data into v_bdata
      from public.content_rows
     where catalog = 'gacha_banners' and row_id = v_prior.banner_id limit 1;

    v_thr := case when btrim(coalesce(v_bdata->>'pityThreshold','')) ~ '^\d+$'
                  then btrim(v_bdata->>'pityThreshold')::int else null end;
    v_cap := case when btrim(coalesce(v_bdata->>'maxPullsPerPlayer','')) ~ '^\d+$'
                  then btrim(v_bdata->>'maxPullsPerPlayer')::int else null end;

    return json_build_object(
      'status',           'ok',
      'pull_id',          v_prior.id,
      'banner_id',        v_prior.banner_id,
      'count',            v_prior.pull_count,
      'ticket_type',      v_prior.ticket_type,
      'charged',          v_prior.cost,
      'ticket_balance',   v_balance,
      'prizes',           v_prizes,
      'pity', json_build_object(
        'counter',    coalesce(v_counter, 0),
        'threshold',  nullif(coalesce(v_thr, 0), 0),
        'min_rarity', nullif(btrim(coalesce(v_bdata->>'pityMinRarity','')), ''),
        'forced',     v_prior.pity_forced),
      'guarantee_forced', v_prior.guarantee_forced,
      'pulls_used',       coalesce(v_total, 0),
      'pull_limit',       v_cap,
      'rp',               v_rp,
      'replayed',         true
    );
  end if;

  -- ── 3. KILL SWITCHES ─────────────────────────────────────────────────────
  -- Truthiness copied from routers/content.py::_global_enabled and from
  -- golfin_shop_purchase: a missing row (or an unreadable table) is ENABLED,
  -- only an explicit false disables. Fail-open, because a transient read
  -- failure must not close the gacha.
  begin
    select value into v_flag from public.content_settings
     where key = 'content_enabled' limit 1;
  exception when others then
    v_flag := null;
  end;
  if v_flag is false then
    return json_build_object('status', 'not_available', 'reason', 'disabled');
  end if;

  -- The gacha's OWN switch. Narrower than content_enabled on purpose: pausing
  -- the gacha must not also close the shop, the missions and the mode fees.
  begin
    select value into v_flag from public.content_settings
     where key = 'gacha_enabled' limit 1;
  exception when others then
    v_flag := null;
  end;
  if v_flag is false then
    return json_build_object('status', 'not_available', 'reason', 'paused');
  end if;

  -- ALL FOUR catalogs, because a pull reads all four. Killing `gacha_pools`
  -- alone would otherwise leave a banner that charges a ticket and rolls
  -- against nothing.
  begin
    select count(*) into v_cat_off
      from public.content_catalogs
     where name in ('gacha_banners', 'gacha_rates', 'gacha_pools', 'ticket_types')
       and is_enabled is false;
  exception when others then
    v_cat_off := 0;
  end;
  if coalesce(v_cat_off, 0) > 0 then
    return json_build_object('status', 'not_available', 'reason', 'disabled');
  end if;

  -- ── 4. THE BANNER, ON THE SERVER CLOCK ───────────────────────────────────
  select data, min_build, is_active
    into v_bdata, v_bmin, v_bactive
    from public.content_rows
   where catalog = 'gacha_banners' and row_id = v_banner
   limit 1;

  if not found then
    return json_build_object('status', 'unknown_banner');
  end if;

  -- TWO active flags, and they mean different things: `is_active` is the row's
  -- publish state (an operator deactivated it), `data->>'active'` is the
  -- banner's own column the client reads. Either being off hides the banner, so
  -- either must refuse the pull.
  if v_bactive is not true
     or lower(btrim(coalesce(v_bdata->>'active', ''))) <> 'true' then
    return json_build_object('status', 'not_available', 'reason', 'inactive');
  end if;

  if coalesce(v_bmin, 0) > coalesce(p_build, 0) then
    return json_build_object('status', 'not_available', 'reason', 'min_build');
  end if;

  -- Same parser, same matrix, as the shop: absent == unbounded, start
  -- INCLUSIVE, end EXCLUSIVE, unparseable FAILS CLOSED. `set timezone = 'UTC'`
  -- above is load-bearing — a zone-less bound must not be read in whatever
  -- timezone the connection happens to carry.
  select ok, ts into v_ok, v_start from public.golfin_shop_parse_bound(v_bdata->>'startUtc');
  if not v_ok then
    return json_build_object('status', 'not_available', 'reason', 'unparseable_bound');
  end if;

  select ok, ts into v_ok, v_end from public.golfin_shop_parse_bound(v_bdata->>'endUtc');
  if not v_ok then
    return json_build_object('status', 'not_available', 'reason', 'unparseable_bound');
  end if;

  if v_start is not null and v_now < v_start then
    return json_build_object('status', 'not_available', 'reason', 'window');
  end if;
  if v_end is not null and v_now >= v_end then
    return json_build_object('status', 'not_available', 'reason', 'window');
  end if;

  v_pool := btrim(coalesce(v_bdata->>'poolId', ''));
  if v_pool = '' then
    return json_build_object('status', 'not_available', 'reason', 'pool_for_build');
  end if;

  -- ── 5. TICKET TYPE ───────────────────────────────────────────────────────
  -- `ticket_types.id` IS the row id and IS an integer — golfin_tickets keys on
  -- an int because the client's TicketType enum does. A non-integer is an
  -- authoring error the validator refuses; refusing it here too keeps the
  -- function safe against a hand-edited row.
  v_ticket_raw := btrim(coalesce(v_bdata->>'ticketType', ''));
  if v_ticket_raw !~ '^\d+$' then
    return json_build_object('status', 'not_available', 'reason', 'ticket_type');
  end if;
  v_ticket_type := v_ticket_raw::int;

  select is_active into v_tt_active
    from public.content_rows
   where catalog = 'ticket_types' and row_id = v_ticket_raw
   limit 1;
  if not found or v_tt_active is not true then
    return json_build_object('status', 'not_available', 'reason', 'ticket_type');
  end if;

  -- ── 6. THE PER-PLAYER CAP ────────────────────────────────────────────────
  select counter, total_pulls into v_counter, v_total
    from public.golfin_gacha_pity
   where user_id = p_user_id and banner_id = v_banner;
  v_counter := coalesce(v_counter, 0);
  v_total   := coalesce(v_total, 0);

  v_cap_raw := btrim(coalesce(v_bdata->>'maxPullsPerPlayer', ''));
  if v_cap_raw ~ '^\d+$' then
    v_cap := v_cap_raw::int;
    -- Checked for the WHOLE x10, not per slot: a x10 that would cross the cap is
    -- refused entirely rather than paying out four prizes and stopping.
    if v_total + p_count > v_cap then
      return json_build_object('status', 'pull_cap', 'limit', v_cap, 'used', v_total);
    end if;
  else
    v_cap := null;
  end if;

  -- ── 7. COST, AND THE EXPECTED-COST GUARD ─────────────────────────────────
  -- `data` values are CSV cells, i.e. STRINGS. An unparseable cost is an
  -- authoring error and must not be coerced into a number the player is
  -- charged. ZERO IS VALID here, unlike the shop's rpCost: a free banner is a
  -- real promotion, and `^\d+$` accepts "0" while rejecting "" and "abc".
  v_cost_raw := btrim(coalesce(
    v_bdata->>(case when p_count = 10 then 'costX10' else 'costX1' end), ''));
  if v_cost_raw !~ '^\d+$' then
    return json_build_object('status', 'not_available', 'reason', 'invalid_price');
  end if;
  v_cost := v_cost_raw::int;

  -- The client showed the player a number. It must not be charged a different
  -- one silently — it re-renders the banner at the server cost and asks again.
  if p_expected_cost is not null and p_expected_cost <> v_cost then
    return json_build_object('status', 'cost_changed', 'cost', v_cost);
  end if;

  -- ── 8. THE POOL, FOR THIS BUILD ──────────────────────────────────────────
  -- THE SERVER'S COPY OF THE CLIENT WITHHOLD RULE (spec C): an entry whose
  -- `min_build` is above the caller's build, or whose referenced row is
  -- deactivated, is NOT rollable for this player. Two locks, neither trusting
  -- the other — the client hides what it cannot render, the server refuses to
  -- pay what the client could not show.
  --
  -- The sum counts EVERY active rate row of the pool, including one whose
  -- `rarity` is not on the ladder. That is deliberate: such a row can never be
  -- rolled, so counting it breaks the 10 000 sum and the pool is refused, which
  -- is the fail-closed direction.
  for v_rate_row in
    select btrim(coalesce(data->>'rarity', '')) as rarity,
           case when btrim(coalesce(data->>'rateBp', '')) ~ '^\d+$'
                then btrim(data->>'rateBp')::int else 0 end as bp
      from public.content_rows
     where catalog = 'gacha_rates'
       and is_active
       and btrim(coalesce(data->>'poolId', '')) = v_pool
  loop
    v_i := array_position(c_ladder, v_rate_row.rarity);
    if v_i is not null then
      v_bp[v_i] := v_bp[v_i] + v_rate_row.bp;
    end if;
    v_rate_sum := v_rate_sum + v_rate_row.bp;
  end loop;

  -- The rate table is a PROMISE to the player and 100 % is what it promises. A
  -- pool that does not sum to 10 000 is one the admin panel already refuses to
  -- publish; refusing to roll it is the second lock.
  if v_rate_sum <> 10000 then
    return json_build_object('status', 'not_available', 'reason', 'rates');
  end if;

  select coalesce(jsonb_agg(e.item order by e.ord), '[]'::jsonb)
    into v_entries
    from (
      select r.row_id as ord,
             jsonb_build_object(
               'kind',     lower(btrim(coalesce(r.data->>'kind', ''))),
               'ref',      btrim(coalesce(r.data->>'refId', '')),
               'rarity',   btrim(coalesce(r.data->>'rarity', '')),
               'weight',   case when btrim(coalesce(r.data->>'weight', '')) ~ '^\d+$'
                                then btrim(r.data->>'weight')::int else 0 end,
               'quantity', greatest(1, case when btrim(coalesce(r.data->>'quantity', '')) ~ '^\d+$'
                                            then btrim(r.data->>'quantity')::int else 1 end),
               'dupe_rp',  case when btrim(coalesce(r.data->>'dupeRp', '')) ~ '^\d+$'
                                then btrim(r.data->>'dupeRp')::int else 0 end
             ) as item
        from public.content_rows r
        join public.content_rows rf
          on rf.catalog = case lower(btrim(coalesce(r.data->>'kind', '')))
                            when 'club'      then 'clubs'
                            when 'ball'      then 'balls'
                            when 'character' then 'characters'
                            when 'item'      then 'items'
                            when 'ticket'    then 'ticket_types'
                            else '<no-such-catalog>'
                          end
         and rf.row_id = btrim(coalesce(r.data->>'refId', ''))
         and rf.is_active
       where r.catalog = 'gacha_pools'
         and r.is_active
         and coalesce(r.min_build, 0) <= coalesce(p_build, 0)
         and btrim(coalesce(r.data->>'poolId', '')) = v_pool
    ) e;

  for v_i in 1 .. 6 loop
    select coalesce(sum((x->>'weight')::int), 0)
      into v_w
      from jsonb_array_elements(v_entries) x
     where x->>'rarity' = c_ladder[v_i]
       and (x->>'weight')::int > 0;
    v_tier_w[v_i] := coalesce(v_w, 0);
  end loop;

  -- EVERY RATED TIER MUST BE PAYABLE. A tier with a rate but no rollable entry
  -- would silently redistribute its probability across the others, which is the
  -- published table quietly becoming false. `pool_for_build` names the real
  -- cause: the pool is fine, it is this BUILD that cannot see all of it.
  for v_i in 1 .. 6 loop
    if v_bp[v_i] > 0 and v_tier_w[v_i] <= 0 then
      return json_build_object(
        'status', 'not_available', 'reason', 'pool_for_build', 'rarity', c_ladder[v_i]);
    end if;
  end loop;

  -- Pity and guarantee are read AFTER the pool, so an unrollable pool never
  -- charges. `pityThreshold` blank or 0 means NO pity (plan §9 decision 2) —
  -- and so does a blank or unknown `pityMinRarity`, so a half-filled banner
  -- never silently acquires one.
  if btrim(coalesce(v_bdata->>'pityThreshold', '')) ~ '^\d+$' then
    v_thr := btrim(v_bdata->>'pityThreshold')::int;
  end if;
  v_pity_min  := btrim(coalesce(v_bdata->>'pityMinRarity', ''));
  v_pity_rank := coalesce(array_position(c_ladder, v_pity_min), 0);
  v_pity_on   := v_thr > 0 and v_pity_rank > 0;

  v_guar      := btrim(coalesce(v_bdata->>'guaranteeMinRarityX10', ''));
  v_guar_rank := coalesce(array_position(c_ladder, v_guar), 0);

  -- ── 9. THE DEBIT ─────────────────────────────────────────────────────────
  -- Nothing has been written yet, so an `insufficient` is returned VERBATIM and
  -- the same key can succeed later. A FREE banner skips the call entirely rather
  -- than crediting 0 — golfin_ticket_credit refuses a zero delta, and a ledger
  -- row that moved nothing is noise.
  --
  -- ⚠️ THIS CALL IS ALSO THE PER-PLAYER LOCK. golfin_ticket_credit takes
  -- `for update` on (user, ticket_type) and holds it to commit, so two
  -- concurrent pulls by the same player serialize here and the pity counter
  -- read in step 6 cannot go stale under them. The one residual is a FREE
  -- banner (cost 0), which takes no lock: two simultaneous free pulls can lose
  -- one counter increment. `total_pulls` is incremented from the table's own
  -- value in step 11 and is exact either way.
  if v_cost > 0 then
    v_credit := public.golfin_ticket_credit(
      p_user_id, v_ticket_type, -v_cost,
      'gacha:' || v_banner || ':x' || p_count, p_key, null);
    v_status := v_credit->>'status';

    if v_status = 'insufficient' then
      return v_credit;
    end if;
    if v_status = 'unknown_ticket_type' then
      return json_build_object('status', 'not_available', 'reason', 'ticket_type');
    end if;
    if v_status is distinct from 'ok' then
      raise exception 'golfin_gacha_pull: golfin_ticket_credit returned unexpected status %', v_status;
    end if;
  end if;

  -- ── 10. THE ROLL (SPEC §3) ───────────────────────────────────────────────
  -- Slot by slot: forced minimum (pity) → tier by rateBp → entry by weight →
  -- pity update. Nothing is written here; the slots are decided first so that a
  -- failure mid-roll leaves the debit to roll back with it.
  for v_slot in 0 .. p_count - 1 loop
    v_slot_before := v_slot_before || v_counter;

    -- `counter + 1 >= threshold` — the threshold-th pull is the forced one, so
    -- a threshold of 3 means at most two sub-minimum prizes in a row. (SPEC §3
    -- step 1 and its acceptance test.)
    v_forced := v_pity_on and (v_counter + 1) >= v_thr;
    v_floor  := case when v_forced then v_pity_rank else 1 end;

    v_rank := public.golfin_gacha_draw_tier(v_bp, v_tier_w, v_floor);

    if v_rank is null and v_forced then
      -- The forced subset pays nothing (every tier at or above pityMinRarity has
      -- rate 0, or no rollable entry). Take pityMinRarity itself if it is
      -- payable, else fall back to an unforced draw rather than paying nothing.
      -- The validator refuses such a banner, so this is the belt to that braces.
      if v_tier_w[v_pity_rank] > 0 then
        v_rank := v_pity_rank;
      else
        v_rank := public.golfin_gacha_draw_tier(v_bp, v_tier_w, 1);
      end if;
    end if;

    if v_rank is null then
      raise exception 'golfin_gacha_pull: pool % has no rollable tier', v_pool;
    end if;

    v_entry := public.golfin_gacha_draw_entry(v_entries, c_ladder[v_rank], v_tier_w[v_rank]);
    if v_entry is null then
      raise exception 'golfin_gacha_pull: tier % of pool % has no rollable entry',
        c_ladder[v_rank], v_pool;
    end if;

    v_slot_rank  := v_slot_rank || v_rank;
    v_slot_entry := v_slot_entry || jsonb_build_array(v_entry);
    v_slot_pity  := v_slot_pity || v_forced;
    if v_forced then v_pity_forced := true; end if;
    if v_rank > v_best then v_best := v_rank; end if;

    -- The counter resets on a pull that REACHED the rarity, however it got
    -- there — a pity that fires resets itself, and so does a lucky Legendary.
    if v_pity_on then
      v_counter := case when v_rank >= v_pity_rank then 0 else v_counter + 1 end;
    end if;
  end loop;

  -- THE x10 GUARANTEE. Applied to the LAST slot, after the ten are rolled: a
  -- guarantee that fired on slot 0 would open every x10 on its best prize,
  -- which is the opposite of how a guarantee reads. It NEVER LOWERS a slot — if
  -- slot 9 was itself pity-forced, the floor is the higher of the two — and the
  -- pity counter is rewound to the value that slot started with and re-applied,
  -- so a re-roll cannot leave the counter counting a prize that was discarded.
  if p_count = 10 and v_guar_rank > 0 and v_best < v_guar_rank then
    v_floor := greatest(v_guar_rank, case when v_slot_pity[10] then v_pity_rank else 1 end);
    v_rank  := public.golfin_gacha_draw_tier(v_bp, v_tier_w, v_floor);

    if v_rank is null and v_tier_w[v_guar_rank] > 0 then
      v_rank := v_guar_rank;
    end if;

    if v_rank is not null then
      v_entry := public.golfin_gacha_draw_entry(v_entries, c_ladder[v_rank], v_tier_w[v_rank]);
      if v_entry is not null then
        v_slot_rank[10] := v_rank;
        v_slot_entry    := jsonb_set(v_slot_entry, '{9}', v_entry);
        v_guar_forced   := true;
        if v_rank > v_best then v_best := v_rank; end if;
        if v_pity_on then
          v_counter := case when v_rank >= v_pity_rank
                            then 0 else v_slot_before[10] + 1 end;
        end if;
      end if;
    end if;
  end if;

  -- ── 11. RECORD, GRANT, PAY ───────────────────────────────────────────────
  -- The pull row FIRST, because the grant note, the dupe ledger description and
  -- the ticket-prize reason all carry the pull id — a prize that cannot be
  -- traced back to its pull is a support ticket nobody can answer.
  insert into public.golfin_gacha_pulls
    (user_id, banner_id, pool_id, pull_count, ticket_type, cost,
     pity_before, pity_after, pity_forced, guarantee_forced, build, idempotency_key)
  values
    (p_user_id, v_banner, v_pool, p_count, v_ticket_type, v_cost,
     v_slot_before[1], v_counter, v_pity_forced, v_guar_forced,
     coalesce(p_build, 0), p_key)
  returning id into v_pull_id;

  -- THE FUNCTION CAPS; `earn_pts_v2` DOES NOT. earn_pts_v2 never reads
  -- game_point_actions — the router does that for /points/earn-game — so a
  -- caller that does not cap itself is a caller a catalog edit can hand any
  -- number to. A missing action row means no cap, which is the same posture
  -- the earn path takes.
  select max_per_event into v_dupe_cap
    from public.game_point_actions where action = 'gacha_dupe';

  for v_slot in 0 .. p_count - 1 loop
    v_entry    := v_slot_entry -> v_slot;
    v_kind     := v_entry->>'kind';
    v_ref      := v_entry->>'ref';
    v_qty      := (v_entry->>'quantity')::int;
    v_dupe_rp  := 0;
    v_is_dupe  := false;
    v_grant_id := null;

    -- Ownership is evaluated SLOT BY SLOT, with the previous slots' grants
    -- already in the queue, so a x10 that rolls the same club twice pays the
    -- second one as a dupe. Any other order would hand the player two of a
    -- thing that cannot stack.
    if v_kind in ('club', 'character')
       and public.golfin_ref_owned(p_user_id, v_kind, v_ref) then

      v_is_dupe := true;
      v_dupe_rp := (v_entry->>'dupe_rp')::int;
      if v_dupe_cap is not null then
        v_dupe_rp := least(v_dupe_rp, v_dupe_cap);
      end if;

      -- dupeRp 0 is legal and means "this dupe pays nothing" (balls and items
      -- never reach here; a club can still be authored at 0). No ledger row is
      -- written for it — earn_pts_v2 refuses a non-positive amount, and a
      -- 0-point transaction would be noise in the player's history.
      if v_dupe_rp > 0 then
        perform public.earn_pts_v2(
          p_user_id, 'gacha_dupe', v_dupe_rp,
          'gacha:' || v_pull_id || ':' || v_ref,
          md5(p_key::text || ':' || v_slot)::uuid);
        v_rp_earned := v_rp_earned + v_dupe_rp;
      end if;

    elsif v_kind = 'ticket' then
      -- A ticket prize is a ledger CREDIT, never a pending grant: the ledger is
      -- the authority on ticket balances from this migration on, and a grant
      -- would deliver the ticket into the client blob instead.
      if v_ref !~ '^\d+$' then
        raise exception 'golfin_gacha_pull: ticket prize refId % is not an integer', v_ref;
      end if;
      v_credit := public.golfin_ticket_credit(
        p_user_id, v_ref::int, v_qty,
        'gacha_prize:' || v_pull_id,
        md5(p_key::text || ':' || v_slot)::uuid, null);
      if (v_credit->>'status') is distinct from 'ok' then
        raise exception 'golfin_gacha_pull: ticket prize credit returned %', v_credit->>'status';
      end if;

    else
      -- club / character (new), item, ball → the existing grants queue, which
      -- already knows how to apply all four kinds and is idempotent on both
      -- sides. A pull is "a grant the player paid a ticket for".
      insert into public.golfin_pending_grants
        (user_id, kind, ref_id, amount, note, created_by)
      values
        (p_user_id, v_kind, v_ref, v_qty, 'gacha:' || v_pull_id, 'gacha')
      returning id into v_grant_id;
    end if;

    insert into public.golfin_gacha_prizes
      (pull_id, slot, kind, ref_id, quantity, rarity, is_dupe, dupe_rp, grant_id)
    values
      (v_pull_id, v_slot, v_kind, v_ref, v_qty,
       c_ladder[v_slot_rank[v_slot + 1]], v_is_dupe, v_dupe_rp, v_grant_id);
  end loop;

  -- `total_pulls` is incremented from the TABLE's value, not from the one read
  -- in step 6, so it stays exact even if that read went stale.
  insert into public.golfin_gacha_pity (user_id, banner_id, counter, total_pulls, updated_at)
  values (p_user_id, v_banner, v_counter, v_total + p_count, now())
  -- The table name in an ON CONFLICT SET expression is UNQUALIFIED: a
  -- schema-qualified `public.golfin_gacha_pity.total_pulls` is parsed as a
  -- three-part column reference and resolves against no FROM entry.
  on conflict (user_id, banner_id) do update
     set counter     = excluded.counter,
         total_pulls = golfin_gacha_pity.total_pulls + p_count,
         updated_at  = now();

  select total_pulls into v_total
    from public.golfin_gacha_pity
   where user_id = p_user_id and banner_id = v_banner;

  select coalesce(balance, 0) into v_balance
    from public.golfin_tickets
   where user_id = p_user_id and ticket_type = v_ticket_type;
  v_balance := coalesce(v_balance, 0);

  if v_rp_earned > 0 then
    select json_build_object(
             'earned',       v_rp_earned,
             'activity_pts', coalesce(activity_pts, 0),
             'gift_pts',     coalesce(gift_pts, 0),
             'total_points', coalesce(total_points, 0))
      into v_rp
      from public.profiles where id = p_user_id;
  end if;

  -- `prizes` is read back from the table rather than rebuilt from the arrays:
  -- what the client is told it received is exactly what was recorded, by
  -- construction, and the two cannot drift.
  select coalesce(json_agg(json_build_object(
           'slot', z.slot, 'kind', z.kind, 'ref_id', z.ref_id,
           'quantity', z.quantity, 'rarity', z.rarity,
           'is_dupe', z.is_dupe, 'dupe_rp', z.dupe_rp, 'grant_id', z.grant_id
         ) order by z.slot), '[]'::json)
    into v_prizes
    from public.golfin_gacha_prizes z
   where z.pull_id = v_pull_id;

  return json_build_object(
    'status',           'ok',
    'pull_id',          v_pull_id,
    'banner_id',        v_banner,
    'count',            p_count,
    'ticket_type',      v_ticket_type,
    'charged',          v_cost,
    'ticket_balance',   v_balance,
    'prizes',           v_prizes,
    'pity', json_build_object(
      'counter',    v_counter,
      'threshold',  case when v_pity_on then v_thr else null end,
      'min_rarity', case when v_pity_on then v_pity_min else null end,
      'forced',     v_pity_forced),
    'guarantee_forced', v_guar_forced,
    'pulls_used',       v_total,
    'pull_limit',       v_cap,
    'rp',               v_rp,
    'replayed',         false
  );
end;
$$;

-- SECURITY: same reasoning as spend_pts / earn_pts_v2 / golfin_shop_purchase. A
-- security-definer function that debits another user's ledger AND hands out
-- inventory must be backend-only. Postgres grants EXECUTE to PUBLIC on new
-- functions by default and PostgREST would expose it at
-- /rest/v1/rpc/golfin_gacha_pull — an `authenticated` grant would let any
-- logged-in client pull for anyone, at whatever p_expected_cost it felt like
-- sending, with whatever p_build unlocked the whole pool.
revoke execute on function public.golfin_gacha_pull(uuid, text, int, int, uuid, int)
  from public, anon, authenticated;
grant execute on function public.golfin_gacha_pull(uuid, text, int, int, uuid, int)
  to service_role;


-- ── VERIFICATION — run this after applying, paste the output ────────────────
--
-- ⚠️ WRAPPED IN A SUBQUERY ON PURPOSE. The Supabase SQL editor appends
-- `limit 100` to whatever you run and its rewriter injects that into the LAST
-- select of a bare `union all` chain, which is a syntax error. Selecting from a
-- derived table gives the limit somewhere valid to land. Do not unwrap this.

select chk, value, expect
  from (
    select 1 as ord, 'tables' as chk, count(*)::int as value, '5 expected' as expect
      from information_schema.tables
     where table_schema = 'public'
       and table_name in ('golfin_tickets', 'golfin_ticket_transactions',
                          'golfin_gacha_pulls', 'golfin_gacha_prizes', 'golfin_gacha_pity')
    union all
    select 2, 'tables_rls', count(*)::int, '5 expected'
      from pg_class c join pg_namespace n on n.oid = c.relnamespace
     where n.nspname = 'public' and c.relrowsecurity
       and c.relname in ('golfin_tickets', 'golfin_ticket_transactions',
                         'golfin_gacha_pulls', 'golfin_gacha_prizes', 'golfin_gacha_pity')
    union all
    select 3, 'tables_policies', count(*)::int, '0 expected (zero policies IS deny-all)'
      from pg_policies
     where schemaname = 'public'
       and tablename in ('golfin_tickets', 'golfin_ticket_transactions',
                         'golfin_gacha_pulls', 'golfin_gacha_prizes', 'golfin_gacha_pity')
    union all
    select 4, 'ticket_tx_unique_key', count(*)::int, '1 expected (user_id, idempotency_key)'
      from pg_indexes
     where schemaname = 'public' and tablename = 'golfin_ticket_transactions'
       and indexdef like '%UNIQUE%' and indexdef like '%idempotency_key%'
    union all
    select 5, 'pulls_unique_key', count(*)::int, '1 expected (user_id, idempotency_key)'
      from pg_indexes
     where schemaname = 'public' and tablename = 'golfin_gacha_pulls'
       and indexdef like '%UNIQUE%' and indexdef like '%idempotency_key%'
    union all
    select 6, 'fn_contract', count(*)::int,
           '3 expected (ticket_credit, ref_owned, gacha_pull)'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public'
       and p.proname in ('golfin_ticket_credit', 'golfin_ref_owned', 'golfin_gacha_pull')
    union all
    select 7, 'fn_roll_internals', count(*)::int,
           '2 expected (draw_tier, draw_entry)'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public'
       and p.proname in ('golfin_gacha_draw_tier', 'golfin_gacha_draw_entry')
    union all
    select 8, 'fn_not_client_callable',
           case when bool_or(has_function_privilege('authenticated', p.oid, 'execute'))
                then 1 else 0 end,
           '0 expected (none of the five may be callable by authenticated)'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public'
       and p.proname in ('golfin_ticket_credit', 'golfin_ref_owned', 'golfin_gacha_pull',
                         'golfin_gacha_draw_tier', 'golfin_gacha_draw_entry')
    union all
    select 9, 'gacha_enabled_row', count(*)::int, '1 expected, value true'
      from public.content_settings where key = 'gacha_enabled' and value
    union all
    select 10, 'gacha_dupe_action', count(*)::int, '1 expected'
      from public.game_point_actions where action = 'gacha_dupe'
    union all
    select 11, 'dep_earn_pts_v2', count(*)::int, '1 expected (dependency)'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'earn_pts_v2'
    union all
    select 12, 'dep_parse_bound', count(*)::int, '1 expected (dependency)'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'golfin_shop_parse_bound'
    union all
    select 13, 'seed_gacha_banners', count(*)::int, '>=1 expected (spec A seed applied)'
      from public.content_rows where catalog = 'gacha_banners'
    union all
    select 14, 'seed_gacha_rates', count(*)::int, '6 expected (spec A seed applied)'
      from public.content_rows where catalog = 'gacha_rates'
    union all
    select 15, 'seed_gacha_pools', count(*)::int, '11 expected (spec A seed applied)'
      from public.content_rows where catalog = 'gacha_pools'
    union all
    select 16, 'seed_ticket_types', count(*)::int, '2 expected (spec A seed applied)'
      from public.content_rows where catalog = 'ticket_types'
  ) v
 order by ord;


-- ── SMOKE (substitute <U> with a real user uuid) ────────────────────────────
--
-- 0) The ledger starts empty for everyone — this is the DECISION, not a bug:
--      select * from golfin_tickets where user_id = '<U>';        -- expect 0 rows
--
-- 1) INSUFFICIENT — nothing written, so the same key can succeed later:
--      select public.golfin_gacha_pull('<U>', 'banner_standard_club1', 1, null,
--               '11111111-0000-0000-0000-000000000001', 99999);
--      -- expect status=insufficient, balance 0. Then:
--      select count(*) from golfin_gacha_pulls
--       where idempotency_key = '11111111-0000-0000-0000-000000000001';   -- expect 0
--
-- 2) GRANT TICKETS (this is what the admin panel calls):
--      select public.golfin_ticket_credit('<U>', 0, 1000, 'admin_grant',
--               gen_random_uuid(), 'cesar.guarinoni@wonderwall-g.com');
--      select * from golfin_tickets where user_id = '<U>';                -- expect 1000
--
-- 3) COST CHANGED — nothing written:
--      select public.golfin_gacha_pull('<U>', 'banner_standard_club1', 1, 999,
--               '11111111-0000-0000-0000-000000000002', 99999);
--      -- expect status=cost_changed, cost=50
--      select count(*) from golfin_ticket_transactions
--       where idempotency_key = '11111111-0000-0000-0000-000000000002';   -- expect 0
--
-- 4) HAPPY PATH x1:
--      select public.golfin_gacha_pull('<U>', 'banner_standard_club1', 1, 50,
--               '11111111-0000-0000-0000-000000000003', 99999);
--      -- expect status=ok, charged=50, ticket_balance=950, 1 prize
--      select delta, balance_after, reason from golfin_ticket_transactions
--       where idempotency_key = '11111111-0000-0000-0000-000000000003';
--      -- expect -50, 950, 'gacha:banner_standard_club1:x1'
--
-- 5) REPLAY — same key, same prizes, balance UNCHANGED:
--      select public.golfin_gacha_pull('<U>', 'banner_standard_club1', 1, 50,
--               '11111111-0000-0000-0000-000000000003', 99999);
--      -- expect replayed=true and the SAME pull_id / prizes
--      select balance from golfin_tickets where user_id='<U>' and ticket_type=0; -- STILL 950
--
-- 6) x10 + GUARANTEE:
--      select public.golfin_gacha_pull('<U>', 'banner_standard_club1', 10, 450,
--               gen_random_uuid(), 99999);
--      -- expect 10 prizes; guarantee_forced is true whenever the ten would
--      -- otherwise have been all-Common/Uncommon
--
-- 7) PAUSE:
--      update content_settings set value = false where key = 'gacha_enabled';
--      select public.golfin_gacha_pull('<U>', 'banner_standard_club1', 1, null,
--               gen_random_uuid(), 99999);   -- expect not_available / paused
--      update content_settings set value = true  where key = 'gacha_enabled';
--
-- 8) NO PITY on banner_test_a — pityThreshold is blank:
--      select pity_forced, pity_before, pity_after from golfin_gacha_pulls
--       where user_id = '<U>' and banner_id = 'banner_test_a';
--      -- expect pity_forced false and both counters 0 on every row
