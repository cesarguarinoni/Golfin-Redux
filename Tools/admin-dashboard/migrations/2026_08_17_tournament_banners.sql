-- =============================================================
-- tournament_modal banners — the cross-promotion strip at the top
-- of the GOLFIN sign-up modal (Cesar, 2026-08-17)
-- =============================================================
-- Origin: game_banners §9, amended into that spec after its implementer had
-- started, never built, and then filed under Completed with §9 outstanding.
-- Refiled standalone as Docs/Specs/Active/tournament_banners/ in the GOLFIN
-- repo; this migration is that spec's §1.
--
-- The shape, and why it is split across two tables:
--   * the ARTWORK lives in public.game_banners, like every other banner, so one
--     GPS promo is uploaded once and can serve every tournament;
--   * WHICH banner a tournament shows is public.tournaments.modal_banner_id,
--     set per tournament in the Tournaments panel.
-- Upload once, assign many, switch off in one place.
--
-- Two things deliberately do NOT apply to a tournament_modal row:
--   * start_at / end_at — the TOURNAMENT's own window governs when the strip is
--     on screen, so a second schedule would just be a way to disagree with it;
--   * sort_order — there is no "pick the top one" for an assigned banner.
-- The dashboard hides both rather than showing controls that do nothing.
-- is_active still applies and is still the kill switch: switching a banner off
-- drops it from EVERY tournament using it at once, which is the point.
--
-- ⚠️ A tournament_modal row is never served by GET /api/v1/banners. It only
-- ever reaches a player attached to a tournament, through
-- GET /tournaments/golfin's `modal_banner` object.
--
-- Idempotent: safe to re-run.

-- -------------------------------------------------------------
-- 1. Widen the placement CHECK.
-- -------------------------------------------------------------
-- The constraint name below was READ from the live database rather than
-- assumed, per the spec's warning that an inline declaration may have named it
-- something else. Probe used (a deliberately failing insert over PostgREST, so
-- nothing was written):
--
--   POST /rest/v1/game_banners  {"placement":"__probe_nonsense__","label":"…"}
--   → 23514  new row for relation "game_banners" violates check constraint
--            "game_banners_placement_check"
--
-- If you need to re-confirm it before running this:
--   select conname, pg_get_constraintdef(oid)
--     from pg_constraint
--    where conrelid = 'public.game_banners'::regclass and contype = 'c';

alter table public.game_banners
  drop constraint if exists game_banners_placement_check;

alter table public.game_banners
  add constraint game_banners_placement_check
  check (placement in ('home_promo', 'rankings', 'tournament_modal'));

comment on column public.game_banners.placement is
  'Which in-game slot this banner fills. home_promo and rankings are auto-served '
  'by GET /api/v1/banners, one live row each. tournament_modal is NEVER served '
  'there — it reaches a player only via tournaments.modal_banner_id, inside '
  'GET /tournaments/golfin. start_at/end_at/sort_order do not apply to it.';

-- -------------------------------------------------------------
-- 2. The assignment.
-- -------------------------------------------------------------
-- on delete SET NULL, never cascade: deleting a banner must not delete a
-- tournament. The tournament simply loses its strip and renders the no-banner
-- state, which is a complete and correct modal (it measures 1167 instead of
-- 1411 and is what every tournament shows today).

alter table public.tournaments
  add column if not exists modal_banner_id uuid
    references public.game_banners(id) on delete set null;

comment on column public.tournaments.modal_banner_id is
  'GOLFIN: the game_banners row whose artwork is the sign-up modal''s '
  'cross-promotion strip (Figma 13892:3435, 970x252). Must reference a row with '
  'placement = ''tournament_modal'' — enforced by the dashboard, which 400s a '
  'dangling or wrong-placement id. NULL (the default, and the on-delete result) '
  'means the modal renders its no-banner state. NOT tournaments.banner_url, '
  'which is the 260x360 card art in a different bucket.';

-- =============================================================
-- VERIFICATION  (run before deploying any code that selects these)
-- =============================================================
-- Deploying a .select() that names a column which does not exist 500s the WHOLE
-- schedule endpoint for every player, not just the banner
-- (Docs/ADMIN_DASHBOARD_OPS.md §3.2). So: migration, verify, THEN deploy.
--
-- 1. The CHECK now admits three values and still rejects everything else:
--
--   select pg_get_constraintdef(oid) from pg_constraint
--    where conrelid = 'public.game_banners'::regclass and conname = 'game_banners_placement_check';
--   -- expect: CHECK ((placement = ANY (ARRAY['home_promo'::text, 'rankings'::text, 'tournament_modal'::text])))
--
-- 2. The assignment column is reachable BY NAME over PostgREST (this is the one
--    that matters — a stale PostgREST schema cache looks exactly like a
--    migration that never ran):
--
--   curl -s "$SUPABASE_URL/rest/v1/tournaments?select=slug,modal_banner_id&limit=1" \
--        -H "apikey: $KEY" -H "Authorization: Bearer $KEY"
--   -- expect a row with modal_banner_id: null   (NOT 42703 "column does not exist")
--
-- 3. A tournament_modal row can now be inserted, and nonsense still cannot:
--
--   insert into public.game_banners (placement, label) values ('tournament_modal', 'smoke');
--   -- expect: 1 row
--   insert into public.game_banners (placement, label) values ('nope', 'smoke');
--   -- expect: 23514 check constraint violation
--   delete from public.game_banners where label = 'smoke';
