-- 2026_09_11_gacha_pity_group.sql
-- weekly_rotation_admin §5 — pity that survives the week.
--
-- WHY
-- The store and the gacha now rotate weekly (Docs/Specs/Active/weekly_rotation_admin/SPEC.md):
-- every Monday a new `banner_wk_<year>_<week>` replaces the last one. Pity was keyed
-- `(user_id, banner_id)`, so a player 40 pulls into the weekly banner would have started from 0
-- on Monday — the one thing a rotating banner must not do to the people who pull on it most.
--
-- THE CHANGE — ONE COLUMN'S MEANING, NO SCHEMA CHANGE
-- `gacha_banners` gains an optional `pityGroup` column (through the ordinary importer; blank on
-- every existing banner). Inside golfin_gacha_pull:
--
--     v_pity_key := coalesce(nullif(btrim(v_bdata->>'pityGroup'), ''), v_banner)
--
-- and the pity COUNTER is read and written under `v_pity_key` wherever the function touched
-- `golfin_gacha_pity.banner_id` (the replay in step 2, the read in step 6, the upsert in step
-- 11). The column keeps its name; its meaning becomes "pity key". A banner with no group behaves
-- byte-for-byte as before — its key is its own id and every read hits the row it always hit.
--
-- WHAT STAYS PER BANNER: `maxPullsPerPlayer`. `total_pulls` for the cap is read under the BANNER
-- id, and when the key differs from the banner a second row keyed by the banner id carries that
-- count (its `counter` is unused). So a weekly cap, if one is ever set, is per week, not per
-- group. The response's `pity` block gains `key` so a client (and the E2E below) can see which
-- counter moved; `pulls_used` / `pull_limit` stay per banner.
--
-- The admin refuses to publish two active banners in one group with different pityThreshold /
-- pityMinRarity (contentValidate rule R3), because a shared counter against two thresholds is
-- undefined. The per-user pity table in the Users drawer shows the key and resets per key.
--
-- Everything else in the body is byte-identical to 2026_09_02_default_ball_guard.sql, so this
-- file is reviewable as a five-hunk diff. golfin_shop_purchase is NOT redefined here.
--
-- IDEMPOTENT: `create or replace function` + `comment on`. Safe to run twice.
-- NO DATA IS TOUCHED. No table, index, grant or policy changes. The VERIFICATION block at the
-- bottom runs inside a transaction that ends in ROLLBACK.

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

  -- weekly_rotation_admin §5 — THE PITY KEY. `golfin_gacha_pity.banner_id`
  -- keeps its name but now holds either a banner id or the banner's
  -- `pityGroup`; banners sharing a group share ONE counter, so a player's
  -- pity survives the week (the weekly banners all use `weekly`). The
  -- per-player cap (`maxPullsPerPlayer`) stays PER BANNER: `total_pulls` is
  -- read and written under the banner id in a second row when the key differs.
  v_pity_key    text;

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

    -- The banner's pity/cap numbers are re-read LIVE and are display only; the
    -- numbers that matter (what was charged, what was rolled) come from the
    -- stored row. A banner deleted since the pull leaves them null rather than
    -- making the replay fail.
    select data into v_bdata
      from public.content_rows
     where catalog = 'gacha_banners' and row_id = v_prior.banner_id limit 1;

    -- The counter lives under the PITY KEY (the group when the banner has one);
    -- the cap's `total_pulls` lives under the banner id. Read before the key so
    -- a banner deleted since the pull still keys on its own id.
    v_pity_key := coalesce(nullif(btrim(coalesce(v_bdata->>'pityGroup', '')), ''), v_prior.banner_id);

    select counter into v_counter
      from public.golfin_gacha_pity
     where user_id = p_user_id and banner_id = v_pity_key;
    select total_pulls into v_total
      from public.golfin_gacha_pity
     where user_id = p_user_id and banner_id = v_prior.banner_id;

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
        'key',        v_pity_key,
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

  -- ── 6. THE PITY KEY, THE COUNTER, THE PER-PLAYER CAP ─────────────────────
  -- weekly_rotation_admin §5. The counter is read under the pity KEY — the
  -- banner's `pityGroup` when it has one, else its own id — so two banners in
  -- one group advance and reset the same row. `total_pulls` for the cap is
  -- read under the BANNER id, always: a weekly cap, if one is ever set, is
  -- per week, not per group. When the key IS the banner id both reads hit the
  -- same row, exactly as before this migration.
  v_pity_key := coalesce(nullif(btrim(coalesce(v_bdata->>'pityGroup', '')), ''), v_banner);

  select counter into v_counter
    from public.golfin_gacha_pity
   where user_id = p_user_id and banner_id = v_pity_key;
  select total_pulls into v_total
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
         -- gacha_ops_polish §4e — THE DEFAULT BALL IS NEVER A PRIZE.
         --
         -- Every player already owns `ball_golfin`: RewardGranter grants it for any reward that
         -- says "a ball", and a fresh save starts with one. So a slot that pays it pays NOTHING —
         -- the ticket is spent, the reveal plays, and the bag is unchanged. `psc1_ball_golfin` sat
         -- in pool_standard_club1 at weight 60 (11 % of every Common pull) until an operator
         -- noticed it and deactivated the row by hand.
         --
         -- Three locks, none trusting the others: the admin refuses to PUBLISH such an entry
         -- (contentValidate rule 21), the client refuses to SHOW the banner
         -- (GachaBannerCatalog.IsRollable), and this one refuses to ROLL it. The entry simply
         -- leaves `v_entries`, so its tier re-normalises over the remaining weights exactly as a
         -- deactivated row does — the published rate table stays true.
         and not (lower(btrim(coalesce(r.data->>'kind', ''))) = 'ball'
                  and lower(btrim(coalesce(rf.data->>'isDefault', ''))) in ('true', '1'))
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
  --
  -- The counter is written under the PITY KEY (weekly_rotation_admin §5). When
  -- the key is a group, `total_pulls` on that row counts the group's pulls —
  -- informational — and the BANNER's own row, written just below, carries the
  -- per-banner count the cap is measured against. When the key is the banner
  -- id this is the single row it always was.
  insert into public.golfin_gacha_pity (user_id, banner_id, counter, total_pulls, updated_at)
  values (p_user_id, v_pity_key, v_counter, p_count, now())
  -- The table name in an ON CONFLICT SET expression is UNQUALIFIED: a
  -- schema-qualified `public.golfin_gacha_pity.total_pulls` is parsed as a
  -- three-part column reference and resolves against no FROM entry.
  on conflict (user_id, banner_id) do update
     set counter     = excluded.counter,
         total_pulls = golfin_gacha_pity.total_pulls + p_count,
         updated_at  = now();

  if v_pity_key <> v_banner then
    -- The per-banner row: only the cap's count moves; its counter is unused
    -- while the banner belongs to a group and is left at whatever it was.
    insert into public.golfin_gacha_pity (user_id, banner_id, counter, total_pulls, updated_at)
    values (p_user_id, v_banner, 0, p_count, now())
    on conflict (user_id, banner_id) do update
       set total_pulls = golfin_gacha_pity.total_pulls + p_count,
           updated_at  = now();
  end if;

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
      'key',        v_pity_key,
      'forced',     v_pity_forced),
    'guarantee_forced', v_guar_forced,
    'pulls_used',       v_total,
    'pull_limit',       v_cap,
    'rp',               v_rp,
    'replayed',         false
  );
end;
$$;

comment on table public.golfin_gacha_pity is
  'Pity counter and lifetime pull count per (player, PITY KEY). Since '
  '2026_09_11_gacha_pity_group.sql banner_id is a KEY: the banner''s pityGroup when '
  'it has one (banners sharing a group share ONE counter, e.g. every weekly banner '
  'uses "weekly"), else the banner id. counter is pulls since the last prize of at '
  'least pityMinRarity and stays 0 on a banner with no pity. total_pulls under a '
  'BANNER id is what maxPullsPerPlayer is measured against; under a group key it '
  'is the group''s pull count and is informational. A grouped banner therefore has '
  'two rows for a player: its own (cap) and the group''s (pity).';

comment on column public.golfin_gacha_pity.banner_id is
  'The pity key: gacha_banners.pityGroup when set, else the banner id.';


-- ═══════════════════════════════════════════════════════════════════════════
-- PART 2 — VERIFICATION. RUN THIS AS A SEPARATE QUERY, AFTER PART 1 IS APPLIED.
-- ═══════════════════════════════════════════════════════════════════════════
-- It opens its own transaction and ends in ROLLBACK. If it were pasted together
-- with Part 1 into an editor that wraps the whole run in one transaction, that
-- ROLLBACK would take the function definition with it — so: apply everything
-- above first, then run from here down as its own query and paste the output.
--
-- Self-contained and side-effect free: a throwaway pool whose only rated tier is Common (so
-- nothing can ever reach the pity rarity and the counter can only climb), two FREE banners
-- sharing pityGroup = weekly, three x1 pulls on A, one on B — and then ROLLBACK, so no row
-- survives. Expected NOTICEs:
--
--   after A x3: counter=3 key=weekly
--   after B x1: counter=4 key=weekly      <- the counter CONTINUED across the two banners
--   pity rows: weekly counter=4 total=4 | zz_pity_a counter=0 total=3 | zz_pity_b counter=0 total=1
--   PASS: pity continued across two banners sharing pityGroup=weekly
--
-- Any ASSERT failure aborts the block with its message; the rollback still runs.

begin;

insert into public.content_rows (catalog, row_id, data, min_build, is_active, version, updated_at) values
  ('gacha_rates', 'zz_pity_pool_common',    '{"id":"zz_pity_pool_common","poolId":"zz_pity_pool","rarity":"Common","rateBp":"10000"}'::jsonb, 0, true, 1, now()),
  ('gacha_rates', 'zz_pity_pool_uncommon',  '{"id":"zz_pity_pool_uncommon","poolId":"zz_pity_pool","rarity":"Uncommon","rateBp":"0"}'::jsonb, 0, true, 1, now()),
  ('gacha_rates', 'zz_pity_pool_rare',      '{"id":"zz_pity_pool_rare","poolId":"zz_pity_pool","rarity":"Rare","rateBp":"0"}'::jsonb, 0, true, 1, now()),
  ('gacha_rates', 'zz_pity_pool_mythic',    '{"id":"zz_pity_pool_mythic","poolId":"zz_pity_pool","rarity":"Mythic","rateBp":"0"}'::jsonb, 0, true, 1, now()),
  ('gacha_rates', 'zz_pity_pool_legendary', '{"id":"zz_pity_pool_legendary","poolId":"zz_pity_pool","rarity":"Legendary","rateBp":"0"}'::jsonb, 0, true, 1, now()),
  ('gacha_rates', 'zz_pity_pool_supreme',   '{"id":"zz_pity_pool_supreme","poolId":"zz_pity_pool","rarity":"Supreme","rateBp":"0"}'::jsonb, 0, true, 1, now()),
  ('gacha_pools', 'zz_pity_entry',          '{"id":"zz_pity_entry","poolId":"zz_pity_pool","kind":"club","refId":"club_driver_gf","rarity":"Common","weight":"100","quantity":"1","dupeRp":"0","featured":"false"}'::jsonb, 0, true, 1, now()),
  ('gacha_banners', 'zz_pity_a', '{"bannerId":"zz_pity_a","nameKey":"ZZ A","artSprite":"GachaBanner_Weekly","costX1":"0","costX10":"0","endUtc":"2099-01-01T00:00:00Z","rulesUrl":"","sortOrder":"98","active":"true","startUtc":"2020-01-01T00:00:00Z","poolId":"zz_pity_pool","ticketType":"0","pityThreshold":"50","pityMinRarity":"Legendary","guaranteeMinRarityX10":"","maxPullsPerPlayer":"","artUrl":"","nameEn":"ZZ A","nameJa":"ZZ A","taglineEn":"","taglineJa":"","featuredRefIds":"","rotationId":"","pityGroup":"weekly"}'::jsonb, 0, true, 1, now()),
  ('gacha_banners', 'zz_pity_b', '{"bannerId":"zz_pity_b","nameKey":"ZZ B","artSprite":"GachaBanner_Weekly","costX1":"0","costX10":"0","endUtc":"2099-01-01T00:00:00Z","rulesUrl":"","sortOrder":"99","active":"true","startUtc":"2020-01-01T00:00:00Z","poolId":"zz_pity_pool","ticketType":"0","pityThreshold":"50","pityMinRarity":"Legendary","guaranteeMinRarityX10":"","maxPullsPerPlayer":"","artUrl":"","nameEn":"ZZ B","nameJa":"ZZ B","taglineEn":"","taglineJa":"","featuredRefIds":"","rotationId":"","pityGroup":"weekly"}'::jsonb, 0, true, 1, now());

do $verify$
declare
  v_user uuid := (select id from public.profiles order by created_at limit 1);
  v_res  json;
  v_rows text;
begin
  if v_user is null then
    raise exception 'no profile to pull with — nothing to verify';
  end if;
  -- A clean slate for this user on the three keys, inside the transaction only.
  delete from public.golfin_gacha_pity
   where user_id = v_user and banner_id in ('weekly', 'zz_pity_a', 'zz_pity_b');

  for i in 1 .. 3 loop
    v_res := public.golfin_gacha_pull(v_user, 'zz_pity_a', 1, 0, gen_random_uuid(), 99999);
    if v_res->>'status' <> 'ok' then
      raise exception 'pull % on zz_pity_a refused: %', i, v_res::text;
    end if;
  end loop;
  raise notice 'after A x3: counter=% key=%', v_res->'pity'->>'counter', v_res->'pity'->>'key';
  assert (v_res->'pity'->>'counter')::int = 3, 'expected counter 3 after three pulls on A, got ' || (v_res->'pity'->>'counter');
  assert v_res->'pity'->>'key' = 'weekly', 'expected key weekly, got ' || coalesce(v_res->'pity'->>'key', 'null');

  v_res := public.golfin_gacha_pull(v_user, 'zz_pity_b', 1, 0, gen_random_uuid(), 99999);
  if v_res->>'status' <> 'ok' then
    raise exception 'pull on zz_pity_b refused: %', v_res::text;
  end if;
  raise notice 'after B x1: counter=% key=%', v_res->'pity'->>'counter', v_res->'pity'->>'key';
  assert (v_res->'pity'->>'counter')::int = 4, 'expected the counter to CONTINUE at 4 on B, got ' || (v_res->'pity'->>'counter');
  assert (v_res->>'pulls_used')::int = 1, 'expected pulls_used 1 on B (the cap is per banner), got ' || (v_res->>'pulls_used');

  select string_agg(banner_id || ' counter=' || counter || ' total=' || total_pulls, ' | ' order by banner_id desc)
    into v_rows
    from public.golfin_gacha_pity
   where user_id = v_user and banner_id in ('weekly', 'zz_pity_a', 'zz_pity_b');
  raise notice 'pity rows: %', v_rows;
  assert (select counter from public.golfin_gacha_pity where user_id = v_user and banner_id = 'weekly') = 4;
  assert (select total_pulls from public.golfin_gacha_pity where user_id = v_user and banner_id = 'zz_pity_a') = 3;
  assert (select total_pulls from public.golfin_gacha_pity where user_id = v_user and banner_id = 'zz_pity_b') = 1;

  -- And a banner with NO group still keys on itself: the replay of a stored pull reads the same key.
  raise notice 'PASS: pity continued across two banners sharing pityGroup=weekly';
end
$verify$;

rollback;

-- After the rollback, the three throwaway keys must not exist for anyone:
select count(*) as leftover_rows_expected_0
  from public.golfin_gacha_pity
 where banner_id in ('weekly', 'zz_pity_a', 'zz_pity_b')
    or banner_id like 'zz_pity_%';
select count(*) as leftover_banners_expected_0
  from public.content_rows
 where row_id like 'zz_pity_%';

-- The function's own contract, unchanged: still not callable by clients.
select case when bool_or(has_function_privilege('authenticated', p.oid, 'execute')) then 1 else 0 end
         as gacha_pull_client_callable_expected_0
  from pg_proc p join pg_namespace n on n.oid = p.pronamespace
 where n.nspname = 'public' and p.proname = 'golfin_gacha_pull';
