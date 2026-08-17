-- ============================================================================
-- Migration: 2026_08_17_game_banners
-- Project:   PLAYLIFE Supabase (wmszyghwwkaptgqdunel) — GOLFIN game banners
-- Purpose:   Put the two banner images that ship inside the GOLFIN build under
--            admin control: the Home promo strip (`home_promo`) and the
--            Rankings screen banner (`rankings`). Written by the admin
--            dashboard's Banners panel; read by the game through
--            `GET /api/v1/banners` on playlife-api (service key, no auth).
-- Spec:      Docs/Specs/Active/game_banners/SPEC.md §1
-- Idempotent: safe to re-run (IF NOT EXISTS style throughout).
-- APPLIED TO PROD: <date> (Cesar, Supabase SQL editor). Post-apply
--            verification is at the bottom of this file.
-- ============================================================================

create table if not exists public.game_banners (
  id           uuid primary key default gen_random_uuid(),
  placement    text not null check (placement in ('home_promo', 'rankings')),
  label        text not null,
  image_url_en text,
  image_url_ja text,
  link_url     text,
  start_at     timestamptz,
  end_at       timestamptz,
  sort_order   integer not null default 0,
  is_active    boolean not null default false,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now()
);

-- The endpoint's only query shape: filter by placement + is_active, then take
-- the first row by (sort_order desc, created_at desc).
create index if not exists game_banners_live_idx
  on public.game_banners (placement, is_active, sort_order desc, created_at desc);

comment on table public.game_banners is
  'Admin-controlled banner images for the GOLFIN client. One live row per '
  'placement; served by GET /api/v1/banners. The bundled sprite stays in the '
  'build and remains the fallback — nothing here can make a slot go blank.';

comment on column public.game_banners.placement is
  'Which in-game slot this fills. Exactly two exist and both are hard-coded in '
  'the build: home_promo (Canvas/ScreensRoot/HomeScreen/PromoBanner) and '
  'rankings (RankingsScreen/ContentArea/Banner). The CHECK constraint is the '
  'guard — a typo here is a banner that silently never appears.';

comment on column public.game_banners.label is
  'ADMIN-ONLY name so the row is findable in the panel. Never sent to the '
  'client, never rendered to a player. All player-visible copy is baked into '
  'the artwork (SPEC D2: image per locale, no text fields).';

comment on column public.game_banners.image_url_en is
  'Public Storage URL of the English artwork, inside the game-banners bucket. '
  'Content-hashed and immutable — replacing an image produces a NEW url, which '
  'is what lets the client key its disk cache on the url with no invalidation '
  'story. Null is legal on a draft; a row with is_active = true and neither '
  'image set is rejected by the dashboard.';

comment on column public.game_banners.image_url_ja is
  'Japanese counterpart of image_url_en. The client picks by '
  'LocalizationManager.CurrentLanguage and falls back to the other locale when '
  'this is null, so a JA-only or EN-only banner is a valid configuration.';

comment on column public.game_banners.is_active is
  'The admin on/off switch. Defaults to FALSE — deliberately unlike '
  'tournaments.is_active, which defaults true because rows already existed and '
  'had to stay visible. A banner row is new, and saving a draft must not '
  'publish it to every player mid-edit. false = the game never learns it '
  'exists.';

comment on column public.game_banners.start_at is
  'Optional schedule window start (UTC). Null = live as soon as is_active.';

comment on column public.game_banners.end_at is
  'Optional schedule window end (UTC), EXCLUSIVE. Null = no expiry. Echoed to '
  'the client as expires_at so a banner cached on-device whose window closed '
  'while the player was offline is dropped client-side.';

comment on column public.game_banners.sort_order is
  'Tie-break within a placement: highest wins, then newest created_at. Only '
  'one row per placement is ever served.';

-- ----------------------------------------------------------------------------
-- SECURITY
-- RLS on with NO policies: anon and authenticated see nothing. Both readers —
-- the admin dashboard and the FastAPI app — use the service key, which bypasses
-- RLS. Grants are revoked explicitly as defence in depth on top of RLS.
-- ----------------------------------------------------------------------------
alter table public.game_banners enable row level security;

revoke all on table public.game_banners from anon;
revoke all on table public.game_banners from authenticated;

grant select, insert, update, delete on table public.game_banners to service_role;

-- ============================================================================
-- VERIFICATION
-- Run after applying, BEFORE deploying anything that reads these columns
-- (Docs/ADMIN_DASHBOARD_OPS.md §3.2 — code that reads a missing column 500s).
--
--   1. Column list is what this file declares:
--        select column_name, data_type, is_nullable, column_default
--          from information_schema.columns
--         where table_schema = 'public' and table_name = 'game_banners'
--         order by ordinal_position;
--      -- expect: id/uuid, placement/text, label/text, image_url_en/text,
--      --         image_url_ja/text, link_url/text, start_at/timestamptz,
--      --         end_at/timestamptz, sort_order/integer, is_active/boolean,
--      --         created_at/timestamptz, updated_at/timestamptz
--
--   2. The placement CHECK actually bites:
--        insert into public.game_banners (placement, label)
--        values ('home_banner', 'should fail');   -- expect: check violation
--
--   3. RLS is on and there are no policies:
--        select relrowsecurity from pg_class
--         where oid = 'public.game_banners'::regclass;                  -- t
--        select count(*) from pg_policies
--         where schemaname = 'public' and tablename = 'game_banners';   -- 0
--
--   4. Over PostgREST with the SERVICE key (this is the check that settles a
--      half-landed migration — it distinguishes "the statement never ran" from
--      a stale PostgREST schema cache):
--        GET $SUPABASE_URL/rest/v1/game_banners?limit=1&select=*
--        -- expect: 200 []
--
--   5. Over PostgREST with the ANON key:
--        GET $SUPABASE_URL/rest/v1/game_banners?limit=1
--        -- expect: 401 / permission denied
-- ============================================================================
