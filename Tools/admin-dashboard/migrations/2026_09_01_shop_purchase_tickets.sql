-- ============================================================================
-- 2026_09_01_shop_purchase_tickets.sql
-- The shop can sell a TICKET, and it sells it into the LEDGER.
--
-- Spec:  GolfinRedux/Docs/Specs/Active/gacha_server_pull/SPEC.md §5.2
-- Plan:  GolfinRedux/Docs/GACHA_ADMIN_PLAN.md §9 ("RP → ticket purchase lives here")
-- Needs: 2026_08_29_shop_purchase_unlimited_refusal.sql (the function this replaces)
--        2026_09_01_golfin_gacha.sql (golfin_ticket_credit, golfin_ref_owned)
--
-- ⚠️ APPLY 2026_09_01_golfin_gacha.sql FIRST. This body calls two functions that
--    migration creates; running this one alone leaves a function that raises
--    `function golfin_ticket_credit does not exist` on every ticket sale.
--
-- ⚠️ APPEND-ONLY, AS ALWAYS. The 08-27 / 08-28 / 08-29 migrations are the record
--    of what was applied on those days and are not edited. This is a
--    `create or replace` carrying the whole body forward, so the diff between
--    the files reads as the change itself.
--
-- WHAT CHANGES, EXACTLY — four things and nothing else:
--
--   1. STEP 7 accepts `category = ticket`, resolving `refId` in `ticket_types`.
--      A new optional `quantity` column says how many the listing sells; blank
--      is 1, which is what every already-published row means.
--
--   2. STEP 8's four inlined ownership reads become one call to
--      `public.golfin_ref_owned` (2026_09_01_golfin_gacha.sql §2.2). That
--      function is those reads PLUS a non-dupe gacha prize — so a club won from
--      the gacha now refuses the shop sale without depending on the client
--      having pushed its inventory blob first. The 08-27 comment already
--      claimed that behaviour ("…already got from the starter set, THE GACHA or
--      an admin grant"); this is what makes it true.
--
--   3. STEP 10 branches: a ticket credits `golfin_ticket_credit` and queues NO
--      pending grant. The returned `grant` object still describes the delivery
--      (kind `ticket`, amount = quantity) but carries `id: null`, because there
--      is nothing to ack.
--
--   4. `golfin_shop_purchases.grant_id` becomes NULLABLE, because of 3.
--
-- ⚠️ THE CLIENT HALF DOES NOT EXIST YET, AND THIS IS WHY NO TICKET ROW MAY BE
--    PUBLISHED. The shipped build's `ShopTransaction.ApplyPurchaseGrant`
--    (Assets/Scripts/UI/Shop/ShopTransaction.cs) switches on `grant.kind` over
--    club / character / item / ball and falls to a `default:` that logs an error
--    and returns false — so a ticket purchase would charge the RP, credit the
--    server ledger correctly, and then show the player a failure. (`grant.id =
--    null` itself IS tolerated: `ShopGrantDto.Id` defaults to "" and
--    `RecordAndAck` returns early on an empty id.) The client case is spec C's.
--    Until the build carrying it is archived, admin validator rule G1-T
--    (`TICKET_SHOP_BUILD = 0` in `Tools/admin-dashboard/lib/buildGates.ts`)
--    makes a `category = ticket` row a hard publish error. The function is
--    live-and-unreachable on purpose — the same posture the shop itself shipped
--    in.
--
-- STATUS: NOT YET APPLIED.
-- Idempotent: safe to re-run.
-- ============================================================================


-- ── The purchase ledger's grant_id becomes optional ────────────────────────
-- A ticket sale has no queue row to point at. The column stays a plain uuid
-- (there is no FK to drop) and every non-ticket purchase still writes one, so
-- a NULL here means exactly one thing: "delivered somewhere other than the
-- grants queue". The replay path already coalesces every field it reads off the
-- grant, so a null grant_id degrades to the purchase row's own values.

alter table public.golfin_shop_purchases
  alter column grant_id drop not null;

comment on column public.golfin_shop_purchases.grant_id is
  'golfin_pending_grants.id for the four grantable categories. NULL for '
  'category = ticket, which is delivered into golfin_tickets by '
  'golfin_ticket_credit() instead — there is no queue row to ack.';


create or replace function public.golfin_shop_purchase(
  p_user_id     uuid,
  p_entry_id    text,
  p_build       int,
  p_expected_rp int,
  p_key         uuid
)
returns json
language plpgsql
security definer
set search_path = public
set timezone = 'UTC'
as $$
declare
  v_now             timestamptz := now();

  -- replay
  v_prior           public.golfin_shop_purchases%rowtype;
  v_grant           public.golfin_pending_grants%rowtype;
  v_activity        int;
  v_gift            int;
  v_total           int;
  v_from_activity   int;
  v_from_gift       int;

  -- kill switches
  v_global_enabled  boolean;
  v_cat_enabled     boolean;

  -- entry
  v_data            jsonb;
  v_min_build       int;
  v_is_active       boolean;

  -- windows
  v_ok              boolean;
  v_start           timestamptz;
  v_end             timestamptz;
  v_sale_start      timestamptz;
  v_sale_end        timestamptz;
  v_sale_open       boolean;

  -- price
  v_list            int;
  v_sale            int;
  v_price           int;
  v_priced_on_sale  boolean;

  -- reference
  v_category        text;
  v_ref             text;
  v_ref_catalog     text;
  v_ref_active      boolean;
  v_ref_min_build   int;
  v_quantity        int;

  -- ticket delivery (2026_09_01_shop_purchase_tickets)
  v_credit          json;

  -- uniqueness
  v_owned           boolean := false;
  v_blob            jsonb;
  v_elem            jsonb;
  v_blob_key        text;

  -- debit + delivery
  v_spend           json;
  v_status          text;
  v_grant_id        uuid;
  v_grant_at        timestamptz;
  v_note            text;
begin
  if p_user_id is null then
    raise exception 'golfin_shop_purchase: p_user_id is required';
  end if;
  if p_key is null then
    raise exception 'golfin_shop_purchase: p_key (idempotency key) is required';
  end if;
  if p_entry_id is null or btrim(p_entry_id) = '' then
    raise exception 'golfin_shop_purchase: p_entry_id is required';
  end if;

  v_note := 'shop:' || p_entry_id;

  -- ── 1. REPLAY ────────────────────────────────────────────────────────────
  -- The key already bought something. Rebuild the ok shape from the stored row
  -- and its grant, with the CURRENT balances. No second debit, no second grant.
  --
  -- The bucket split is re-read from points_transactions (the same query shape
  -- spend_pts's own replay guard uses) rather than by re-calling spend_pts:
  -- this path must be provably read-only, and a spend_pts call whose ledger rows
  -- had been deleted by hand would debit a second time.

  select * into v_prior
    from public.golfin_shop_purchases
   where user_id = p_user_id and idempotency_key = p_key
   limit 1;

  if found then
    select * into v_grant
      from public.golfin_pending_grants
     where id = v_prior.grant_id
     limit 1;

    select coalesce(activity_pts, 0), coalesce(gift_pts, 0), coalesce(total_points, 0)
      into v_activity, v_gift, v_total
      from public.profiles
     where id = p_user_id;

    select coalesce(sum(case when currency = 'activity' then -amount else 0 end), 0),
           coalesce(sum(case when currency = 'gift'     then -amount else 0 end), 0)
      into v_from_activity, v_from_gift
      from public.points_transactions
     where user_id = p_user_id
       and type = 'spend'
       and idempotency_key in (p_key, md5(p_key::text || ':gift')::uuid);

    return json_build_object(
      'status',        'ok',
      'entry_id',      v_prior.entry_id,
      'category',      v_prior.category,
      'ref_id',        v_prior.ref_id,
      'charged',       v_prior.charged_rp,
      'list_rp',       v_prior.list_rp,
      'on_sale',       v_prior.on_sale,
      'grant', json_build_object(
        'id',         v_prior.grant_id,
        'kind',       coalesce(v_grant.kind, v_prior.category),
        'ref_id',     coalesce(v_grant.ref_id, v_prior.ref_id),
        'amount',     coalesce(v_grant.amount, v_prior.amount),
        'note',       coalesce(v_grant.note, 'shop:' || v_prior.entry_id),
        'created_at', coalesce(v_grant.created_at, v_prior.created_at)
      ),
      'spent',         coalesce(v_from_activity, 0) + coalesce(v_from_gift, 0),
      'from_activity', coalesce(v_from_activity, 0),
      'from_gift',     coalesce(v_from_gift, 0),
      'activity_pts',  coalesce(v_activity, 0),
      'gift_pts',      coalesce(v_gift, 0),
      'total_points',  coalesce(v_total, 0),
      'replayed',      true
    );
  end if;

  -- ── 2. KILL SWITCHES ─────────────────────────────────────────────────────
  -- When the operator has pulled remote content, the server must not SELL from
  -- it either. Truthiness copied from routers/content.py::_global_enabled — a
  -- missing row (or an unreadable table) is ENABLED, only an explicit false
  -- disables. Fail-open there and here for the same reason: a transient read
  -- failure must not close the shop.

  begin
    select value into v_global_enabled
      from public.content_settings
     where key = 'content_enabled'
     limit 1;
  exception when others then
    v_global_enabled := null;
  end;

  if v_global_enabled is false then
    return json_build_object('status', 'not_listed', 'reason', 'disabled');
  end if;

  select is_enabled into v_cat_enabled
    from public.content_catalogs
   where name = 'shop_catalog'
   limit 1;

  if v_cat_enabled is false then
    return json_build_object('status', 'not_listed', 'reason', 'disabled');
  end if;

  -- ── 3. THE ENTRY ─────────────────────────────────────────────────────────

  select data, min_build, is_active
    into v_data, v_min_build, v_is_active
    from public.content_rows
   where catalog = 'shop_catalog' and row_id = p_entry_id
   limit 1;

  if not found then
    return json_build_object('status', 'unknown_entry');
  end if;

  if v_is_active is not true then
    return json_build_object('status', 'not_listed', 'reason', 'inactive');
  end if;

  if coalesce(v_min_build, 0) > coalesce(p_build, 0) then
    return json_build_object('status', 'not_listed', 'reason', 'min_build');
  end if;

  -- ── 4. WINDOWS, SERVER CLOCK ─────────────────────────────────────────────
  -- See the matrix in the header. All four bounds are parsed BEFORE any of them
  -- is compared, so an unparseable SALE bound fails the row closed even when the
  -- listing window itself is wide open.

  select ok, ts into v_ok, v_start      from public.golfin_shop_parse_bound(v_data->>'startAt');
  if not v_ok then return json_build_object('status','not_listed','reason','unparseable_bound'); end if;

  select ok, ts into v_ok, v_end        from public.golfin_shop_parse_bound(v_data->>'endAt');
  if not v_ok then return json_build_object('status','not_listed','reason','unparseable_bound'); end if;

  select ok, ts into v_ok, v_sale_start from public.golfin_shop_parse_bound(v_data->>'saleStartAt');
  if not v_ok then return json_build_object('status','not_listed','reason','unparseable_bound'); end if;

  select ok, ts into v_ok, v_sale_end   from public.golfin_shop_parse_bound(v_data->>'saleEndAt');
  if not v_ok then return json_build_object('status','not_listed','reason','unparseable_bound'); end if;

  if v_start is not null and v_now <  v_start then
    return json_build_object('status', 'not_listed', 'reason', 'window');
  end if;

  -- EXCLUSIVE: at exactly endAt the row is already gone.
  if v_end is not null and v_now >= v_end then
    return json_build_object('status', 'not_listed', 'reason', 'window');
  end if;

  v_sale_open := (v_sale_start is null or v_now >= v_sale_start)
             and (v_sale_end   is null or v_now <  v_sale_end);

  -- ── 5. PRICE ─────────────────────────────────────────────────────────────
  -- `data` values are CSV cells, i.e. STRINGS — an unparseable rpCost is an
  -- authoring error and must not be coerced into a number the player is charged.

  begin
    v_list := nullif(btrim(coalesce(v_data->>'rpCost', '')), '')::int;
  exception when others then
    v_list := null;
  end;

  if v_list is null or v_list <= 0 then
    return json_build_object('status', 'not_listed', 'reason', 'invalid_price');
  end if;

  begin
    v_sale := nullif(btrim(coalesce(v_data->>'saleRpCost', '')), '')::int;
  exception when others then
    v_sale := null;
  end;

  -- Same rule as ShopCatalogEntry.HasSale: a sale price only applies inside the
  -- sale window, only when set, and only when it is actually a discount.
  v_price := case
               when v_sale_open and v_sale is not null and v_sale > 0 and v_sale < v_list then v_sale
               else v_list
             end;
  v_priced_on_sale := v_price < v_list;

  -- ── 6. EXPECTED PRICE ────────────────────────────────────────────────────
  -- The client showed the player a number. It must not be charged a different
  -- one silently — it re-renders the card at the server price and asks again.

  if p_expected_rp is not null and p_expected_rp <> v_price then
    return json_build_object(
      'status',  'price_changed',
      'price',   v_price,
      'list_rp', v_list,
      'on_sale', v_priced_on_sale
    );
  end if;

  -- ── 7. CATEGORY → GRANT KIND + REFERENCE ─────────────────────────────────
  -- `bag` is publishable from the admin panel but is NOT grantable — the grants
  -- queue has no bag kind — so refusing it is correct. Listing one would be a
  -- card that can only ever fail.
  --
  -- `ticket` (NEW, gacha_server_pull §5.2) is the fifth, and it is the only one
  -- that does NOT ride the grants queue: tickets live in `golfin_tickets` from
  -- 2026_09_01_golfin_gacha.sql on, and a pending grant would deliver one into
  -- the client blob instead — a second, unauthoritative counter for the same
  -- thing. It resolves in `ticket_types`, whose row_id IS the integer id.

  v_category := lower(btrim(coalesce(v_data->>'category', '')));

  if v_category not in ('club', 'character', 'item', 'ball', 'ticket') then
    return json_build_object('status', 'unsupported_category', 'category', v_category);
  end if;

  v_ref := btrim(coalesce(v_data->>'refId', ''));

  v_ref_catalog := case v_category
                     when 'club'      then 'clubs'
                     when 'character' then 'characters'
                     when 'item'      then 'items'
                     when 'ball'      then 'balls'
                     when 'ticket'    then 'ticket_types'
                   end;

  -- `quantity` is a NEW, OPTIONAL shop_catalog column: how many the listing
  -- sells in one purchase. Blank or unparseable is 1, which is what every row
  -- published before this migration means.
  --
  -- ⚠️ IT IS READ FOR `ticket` ONLY, deliberately. Honouring it for balls and
  -- items would change what already-published listings deliver — a live
  -- behaviour change to rows an operator wrote under the old meaning — and that
  -- is not this task. Validator rule G3-Q refuses a quantity other than 1 on a
  -- non-ticket row so the column cannot silently mean nothing.
  v_quantity := case
                  when v_category = 'ticket'
                   and btrim(coalesce(v_data->>'quantity', '')) ~ '^[1-9][0-9]*$'
                  then btrim(v_data->>'quantity')::int
                  else 1
                end;

  -- `ticket_types.id` IS the row id and IS an integer — golfin_tickets keys on
  -- an int because the client's TicketType enum does. The `::int` cast in step
  -- 10 would raise on a hand-edited non-numeric id, and a 500 is the wrong
  -- answer for a mis-authored row: refuse it as a listing instead. `not_listed`
  -- because the client matches statuses by exact string and already handles it;
  -- the added `reason` is ignored by clients that do not read it.
  if v_category = 'ticket' and v_ref !~ '^[0-9]+$' then
    return json_build_object('status', 'not_listed', 'reason', 'invalid_ref');
  end if;

  select is_active, min_build into v_ref_active, v_ref_min_build
    from public.content_rows
   where catalog = v_ref_catalog and row_id = v_ref
   limit 1;

  if not found or v_ref_active is not true then
    return json_build_object('status', 'not_listed', 'reason', 'ref_inactive');
  end if;

  -- shop_stocking §4 — NEVER SELL WHAT THE CALLER'S BUILD CANNOT SEE.
  --
  -- The shop row's own min_build is checked in step 3. This is the OTHER one:
  -- the min_build of the thing it sells. A client on build N whose bundled CSVs
  -- and content response both stop at N would take delivery of a club that is
  -- not in its catalog at all — the grant applies, the inventory carries an id
  -- nothing resolves, and the player has paid for a blank row.
  --
  -- The admin refuses to publish this shape (validator rule G2), and the client
  -- withholds such a row from the store (GeneralShopCatalog.Admit). This is the
  -- third lock, and it is the one that does not depend on either of the other
  -- two having been used: the function does not get to assume the admin panel
  -- was the thing that wrote the row.
  if coalesce(v_ref_min_build, 0) > coalesce(p_build, 0) then
    return json_build_object('status', 'not_listed', 'reason', 'ref_min_build');
  end if;

  -- ── 8. UNIQUENESS (clubs and characters only — everything else stacks) ───
  -- SWAPPED to `public.golfin_ref_owned` (2026_09_01_golfin_gacha.sql §2.2),
  -- which is the four inlined reads that used to live here plus one more:
  --
  --   a) a prior purchase row       — authoritative, written by this function
  --   b) a NON-DUPE gacha prize     — authoritative, written by golfin_gacha_pull  [NEW]
  --   c) an UNAPPLIED pending grant — authoritative, the item is already owed
  --   d) the inventory blob         — BEST EFFORT, it is client-asserted data
  --
  -- (b) IS A REAL BEHAVIOUR CHANGE and the intended one: this function's own
  -- comment already said the blob read exists so that "a club the player
  -- already got from the starter set, THE GACHA or an admin grant is refused
  -- BEFORE the debit". Now that the gacha writes a server-side record, that
  -- refusal no longer depends on the client having pushed its blob first.
  --
  -- The extraction is behaviour-preserving for (a), (c) and (d): the blob rules
  -- (bare id = owned, `own:false` = listed but not owned, malformed blob
  -- contributes nothing) are carried across verbatim.

  if v_category in ('club', 'character')
     and public.golfin_ref_owned(p_user_id, v_category, v_ref) then
    return json_build_object('status', 'already_owned', 'ref_id', v_ref);
  end if;

  -- ── 8b. AN UNLIMITED STACKABLE IS NOT A SALE ─────────────────────────────
  --
  -- `-1` in the inventory blob is a SENTINEL meaning "unlimited", not a
  -- quantity. The default Golfin ball ships that way. EVERY add path in the
  -- client deliberately leaves it alone — InventoryGrants.AddQuantity,
  -- BallManager.AddBalls, ItemManager.AddItems, ShopTransaction.GrantBall — which
  -- is right for a reward and catastrophic for a sale.
  --
  -- Without this branch: the debit happens here in step 9, the grant is queued,
  -- and the client's InventoryGrants.Apply writes appliedGrantIds and ACKS the
  -- grant BEFORE calling ApplyOne. The add is then a no-op. The player has paid
  -- and received nothing, with the grant permanently marked delivered — the
  -- ledger says they were served and the inventory says otherwise, which is the
  -- worst shape a purchase bug can take.
  --
  -- Refused BEFORE the debit, so there is nothing to refund and no key burned.
  -- Reported as `already_owned` (with `reason: 'unlimited'`) rather than a NEW
  -- status ON PURPOSE: the client matches statuses by exact string, so an
  -- unrecognised one would fall through its verdict mapping — and "you already
  -- have an unlimited supply" is exactly what already_owned means here. The
  -- extra `reason` is additive and ignored by clients that do not read it.
  --
  -- Clubs and characters are covered by step 8 above; this is only the two
  -- STACKABLE categories, which by definition have no uniqueness check.
  --
  -- BEST EFFORT, like step 8's blob read: the blob is client-asserted data, so a
  -- malformed one must never block a legitimate sale. Any error means "not
  -- unlimited" and the sale proceeds.

  if v_category in ('ball', 'item') then
    v_owned := false;
    v_blob_key := case when v_category = 'ball' then 'balls' else 'items' end;

    begin
      select golfin_inventory into v_blob from public.profiles where id = p_user_id;

      if v_blob is not null
         and jsonb_typeof(v_blob -> v_blob_key) = 'object'
         and (v_blob -> v_blob_key ->> v_ref) is not null
         and (v_blob -> v_blob_key ->> v_ref)::numeric < 0 then
        v_owned := true;
      end if;
    exception when others then
      v_owned := false;
    end;

    if v_owned then
      return json_build_object(
        'status',  'already_owned',
        'ref_id',  v_ref,
        'reason',  'unlimited'
      );
    end if;
  end if;

  -- ── 9. DEBIT ─────────────────────────────────────────────────────────────
  -- The reason string is what the admin Points panel shows on the ledger row —
  -- `shop:<entryId>` alone makes every purchase auditable with no admin change.
  -- An insufficient answer is returned VERBATIM: nothing was written, so the
  -- same key can succeed later once the player has the points.

  v_spend  := public.spend_pts(p_user_id, v_price, v_note, p_key);
  v_status := v_spend->>'status';

  if v_status = 'insufficient' then
    return v_spend;
  end if;

  if v_status is distinct from 'ok' then
    raise exception 'golfin_shop_purchase: spend_pts returned unexpected status %', v_status;
  end if;

  -- ── 10. GRANT + LEDGER, SAME TRANSACTION ─────────────────────────────────
  -- A plpgsql function is one transaction: if either insert fails, spend_pts's
  -- debit rolls back with it. That is the whole reason the debit and the grant
  -- live in one function instead of two API calls.

  if v_category = 'ticket' then
    -- NO PENDING GRANT. The ticket ledger is the authority; a grant row would
    -- put the same tickets into the client blob as well, and the blob counter
    -- is exactly what this ledger replaces.
    --
    -- A DERIVED key (`<p_key>:ticket`), not p_key itself, so this credit cannot
    -- collide with a gacha debit that happened to be issued under the same key,
    -- and so a replay of the purchase replays this credit too — the same idiom
    -- spend_pts uses for its `:gift` bucket row.
    v_credit := public.golfin_ticket_credit(
      p_user_id, v_ref::int, v_quantity, v_note,
      md5(p_key::text || ':ticket')::uuid, null);

    if (v_credit->>'status') is distinct from 'ok' then
      raise exception 'golfin_shop_purchase: ticket credit returned %', v_credit->>'status';
    end if;

    v_grant_id := null;
    v_grant_at := now();
  else
    insert into public.golfin_pending_grants (user_id, kind, ref_id, amount, note, created_by)
    values (p_user_id, v_category, v_ref, 1, v_note, 'shop')
    returning id, created_at into v_grant_id, v_grant_at;
  end if;

  insert into public.golfin_shop_purchases
    (user_id, entry_id, category, ref_id, amount, charged_rp, list_rp, on_sale,
     build, idempotency_key, grant_id)
  values
    (p_user_id, p_entry_id, v_category, v_ref, v_quantity, v_price, v_list, v_priced_on_sale,
     coalesce(p_build, 0), p_key, v_grant_id);

  return json_build_object(
    'status',        'ok',
    'entry_id',      p_entry_id,
    'category',      v_category,
    'ref_id',        v_ref,
    'charged',       v_price,
    'list_rp',       v_list,
    'on_sale',       v_priced_on_sale,
    -- For a ticket this is a DESCRIPTION of what was delivered, not a queue row:
    -- `id` is null because there is nothing to ack. The client's
    -- `ShopGrantDto.Id` defaults to "" and `RecordAndAck` no-ops on an empty id,
    -- so a null is tolerated by the shipped build — but its
    -- `ApplyPurchaseGrant` has no `ticket` case and would log an error. That is
    -- why validator rule G1-T keeps a `category = ticket` row unpublishable
    -- until the spec-C build is archived. See the report.
    'grant', json_build_object(
      'id',         v_grant_id,
      'kind',       v_category,
      'ref_id',     v_ref,
      'amount',     v_quantity,
      'note',       v_note,
      'created_at', v_grant_at
    ),
    'spent',         (v_spend->>'spent')::int,
    'from_activity', (v_spend->>'from_activity')::int,
    'from_gift',     (v_spend->>'from_gift')::int,
    'activity_pts',  (v_spend->>'activity_pts')::int,
    'gift_pts',      (v_spend->>'gift_pts')::int,
    'total_points',  (v_spend->>'total_points')::int,
    'replayed',      coalesce((v_spend->>'replayed')::boolean, false)
  );
end;
$$;

-- SECURITY, restated. Re-issued rather than assumed.
revoke execute on function public.golfin_shop_purchase(uuid, text, int, int, uuid)
  from public, anon, authenticated;
grant execute on function public.golfin_shop_purchase(uuid, text, int, int, uuid)
  to service_role;


-- ── VERIFICATION — run this after applying, paste the output ────────────────
--
-- ⚠️ WRAPPED IN A SUBQUERY ON PURPOSE (the Supabase editor appends `limit 100`
-- to the last select of a bare `union all` chain, which is a syntax error).
--
-- Rows 1-4: the function is still the backend-only one it was. Rows 5-6: the
-- bound parser survived the replace — row 6 is the one that would show a lost
-- `set timezone = 'UTC'`. Rows 7-10: the three refusals that were already there
-- are STILL there and the new capability is present, so this replace added one
-- thing without dropping three. Row 11: the nullable column.

select chk, value, expect
  from (
    select 1 as ord, 'fn_purchase' as chk, count(*)::int as value, '1 expected' as expect
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'golfin_shop_purchase'

    union all
    select 2, 'fn_is_security_definer', count(*)::int, '1 expected'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'golfin_shop_purchase' and p.prosecdef

    union all
    select 3, 'fn_not_client_callable', count(*)::int, '0 expected — neither role may execute'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'golfin_shop_purchase'
       and (has_function_privilege('authenticated', p.oid, 'execute')
         or has_function_privilege('anon', p.oid, 'execute'))

    union all
    select 4, 'dep_ticket_credit', count(*)::int,
           '1 expected — 2026_09_01_golfin_gacha.sql must be applied FIRST'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'golfin_ticket_credit'

    union all
    select 5, 'dep_ref_owned', count(*)::int, '1 expected'
      from pg_proc p join pg_namespace n on n.oid = p.pronamespace
     where n.nspname = 'public' and p.proname = 'golfin_ref_owned'

    union all
    select 6, 'bound_zoneless_reads_as_utc',
           (select case when ok and ts = timestamptz '2026-09-01T00:00:00Z' then 1 else 0 end
              from public.golfin_shop_parse_bound('2026-09-01 00:00:00')),
           '1 expected — proves set timezone = UTC survived the replace'

    union all
    select 7, 'fn_sells_tickets',
           (select case when prosrc like '%golfin_ticket_credit%' then 1 else 0 end
              from pg_proc p join pg_namespace n on n.oid = p.pronamespace
             where n.nspname = 'public' and p.proname = 'golfin_shop_purchase'),
           '1 expected — the NEW capability is in the deployed body'

    union all
    select 8, 'fn_still_has_unlimited_refusal',
           (select case when prosrc like '%''unlimited''%' then 1 else 0 end
              from pg_proc p join pg_namespace n on n.oid = p.pronamespace
             where n.nspname = 'public' and p.proname = 'golfin_shop_purchase'),
           '1 expected — the 08-29 refusal must survive'

    union all
    select 9, 'fn_still_has_ref_min_build',
           (select case when prosrc like '%ref_min_build%' then 1 else 0 end
              from pg_proc p join pg_namespace n on n.oid = p.pronamespace
             where n.nspname = 'public' and p.proname = 'golfin_shop_purchase'),
           '1 expected — the 08-28 refusal must survive'

    union all
    select 10, 'fn_still_refuses_owned',
           (select case when prosrc like '%already_owned%' then 1 else 0 end
              from pg_proc p join pg_namespace n on n.oid = p.pronamespace
             where n.nspname = 'public' and p.proname = 'golfin_shop_purchase'),
           '1 expected — the 08-27 uniqueness refusal must survive the extraction'

    union all
    select 11, 'grant_id_is_nullable',
           (select case when is_nullable = 'YES' then 1 else 0 end
              from information_schema.columns
             where table_schema = 'public' and table_name = 'golfin_shop_purchases'
               and column_name = 'grant_id'),
           '1 expected'
  ) v
 order by ord;


-- ── SMOKE — TEST ROWS ONLY, and delete them afterwards ─────────────────────
--
-- ⚠️ DO NOT PUBLISH A REAL `category = ticket` SHOP ROW. See the header: the
-- shipped client cannot apply one. This inserts a DRAFT-less content_rows row
-- directly, exercises the server path, and removes it again.
--
--   insert into content_rows (catalog, row_id, data, min_build, is_active, version)
--   values ('shop_catalog', 'tmp_ticket_probe',
--           '{"entryId":"tmp_ticket_probe","category":"ticket","refId":"0",
--             "rpCost":"100","quantity":"5","sortOrder":"999"}'::jsonb,
--           0, true, 1);
--
--   select public.golfin_shop_purchase('<U>', 'tmp_ticket_probe', 999999, 100,
--            '99999999-9999-9999-9999-999999999999');
--   -- expect status=ok, grant.kind=ticket, grant.id=null, grant.amount=5
--
--   select ticket_type, balance from golfin_tickets where user_id = '<U>';
--   -- expect +5 on type 0
--   select delta, reason, created_by from golfin_ticket_transactions
--    where user_id = '<U>' order by created_at desc limit 1;
--   -- expect 5, 'shop:tmp_ticket_probe', created_by null
--   select count(*) from golfin_pending_grants
--    where user_id = '<U>' and note = 'shop:tmp_ticket_probe';   -- expect 0
--
--   -- clean up:
--   delete from golfin_shop_purchases where entry_id = 'tmp_ticket_probe';
--   delete from golfin_ticket_transactions
--    where reason = 'shop:tmp_ticket_probe';
--   delete from content_rows where catalog='shop_catalog' and row_id='tmp_ticket_probe';
--   -- (the +5 balance stays; adjust it down from the admin Gacha tab if it matters)
