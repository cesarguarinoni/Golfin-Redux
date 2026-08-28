-- =============================================================
-- store banners — the strip at the top of the Store (General Shop)
-- card list (Cesar, 2026-08-28)
-- =============================================================
-- Origin: Docs/Specs/Active/store_banner/SPEC.md §1 in the GOLFIN repo.
--
-- The Store screen shipped a hard-coded banner — WinterSaleBanner inside
-- Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab, drawing
-- Assets/Art/Shop/Banner - Winter Sale.png — that the dashboard could not see,
-- swap, schedule or switch off. This widens the same CHECK constraint that
-- 2026_08_17_tournament_banners.sql widened, so that art becomes the fourth
-- placement on the existing game_banners pipeline. No new table, no new column.
--
-- `store` behaves exactly like home_promo and rankings:
--   * AUTO-SERVED by GET /api/v1/banners, one live row;
--   * start_at / end_at / sort_order all apply;
--   * is_active is the kill switch.
-- It is NOT like tournament_modal, which is assigned per tournament and never
-- reaches that endpoint.
--
-- ⚠️ Behaviour of record (game_banners amendment A1): no live `store` row means
-- the slot is HIDDEN and the card list closes up. The bundled Winter Sale
-- sprite is an authoring placeholder, never a runtime fallback — which is what
-- makes "never create a store row" a complete way to turn the banner off.
--
-- Idempotent: safe to re-run.

-- -------------------------------------------------------------
-- 1. Widen the placement CHECK.
-- -------------------------------------------------------------
-- The constraint name is the one 2026_08_17_tournament_banners.sql created and
-- was re-confirmed on the live database before this file was written:
--
--   select conname, pg_get_constraintdef(oid)
--     from pg_constraint
--    where conrelid = 'public.game_banners'::regclass and contype = 'c';

alter table public.game_banners
  drop constraint if exists game_banners_placement_check;

alter table public.game_banners
  add constraint game_banners_placement_check
  check (placement in ('home_promo', 'rankings', 'tournament_modal', 'store'));

comment on column public.game_banners.placement is
  'Which in-game slot this banner fills. home_promo, rankings and store are auto-served '
  'by GET /api/v1/banners, one live row each. tournament_modal is NEVER served '
  'there — it reaches a player only via tournaments.modal_banner_id, inside '
  'GET /tournaments/golfin. start_at/end_at/sort_order do not apply to it.';

-- =============================================================
-- VERIFICATION  (run before deploying any code that writes a store row)
-- =============================================================
-- Migration first, deploy second (Docs/ADMIN_DASHBOARD_OPS.md §3.2).
--
-- 1. The CHECK now admits four values and still rejects everything else:
--
--   select pg_get_constraintdef(oid) from pg_constraint
--    where conrelid = 'public.game_banners'::regclass and conname = 'game_banners_placement_check';
--   -- expect: CHECK ((placement = ANY (ARRAY['home_promo'::text, 'rankings'::text, 'tournament_modal'::text, 'store'::text])))
--
-- 2. A store row can now be inserted, and nonsense still cannot:
--
--   insert into public.game_banners (placement, label) values ('store', 'smoke');
--   -- expect: 1 row
--   insert into public.game_banners (placement, label) values ('shop', 'smoke');
--   -- expect: 23514 check constraint violation
--   delete from public.game_banners where label = 'smoke';
--
-- 3. Old clients are safe to leave in the field: a build that predates
--    BannerPlacement.Store hits TryParsePlacement's default branch, logs one
--    warning and skips the row. Nothing to coordinate — server side can ship
--    ahead of the client build.
