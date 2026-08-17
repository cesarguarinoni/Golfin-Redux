-- ============================================================================
-- Migration: 2026_08_17_tournament_description
-- Project:   PLAYLIFE Supabase (wmszyghwwkaptgqdunel) — GOLFIN tournaments
-- Purpose:   Give a GOLFIN tournament an operator-authored description blurb,
--            shown in the sign-up modal's info row (Figma 13892:3250) beside
--            the 260×360 card art. Written by the admin dashboard's Tournaments
--            panel; read by the game through `GET /api/v1/tournaments/golfin`.
-- Spec:      Docs/Specs/Active/tournament_signup_modal/SPEC.md §1
-- Idempotent: safe to re-run (IF NOT EXISTS throughout).
-- APPLIED TO PROD: <date> (Cesar, Supabase SQL editor). Post-apply
--            verification is at the bottom of this file.
--
-- WHY NOT public.tournaments.description
-- --------------------------------------
-- That column already exists (2026_04_24_memberships_tournaments.sql:51). It is
-- GPS-owned, already carries GPS copy, and is single-locale. Reusing it would
-- put two products' meanings in one field and give the GOLFIN modal no way to
-- be bilingual — the same mistake `is_active` was created to avoid when
-- `status` was the tempting reuse. These are three new, GOLFIN-owned columns.
-- ============================================================================

alter table public.tournaments
  add column if not exists description_en  text,
  add column if not exists description_ja  text,
  add column if not exists description_key text;

comment on column public.tournaments.description_en is
  'GOLFIN sign-up modal blurb, English. Operator-authored, ≤600 chars (the Figma '
  'box is fixed at 360px tall). Empty/NULL hides the modal''s whole info row — '
  'thumbnail included. NOT public.tournaments.description, which is GPS-owned.';

comment on column public.tournaments.description_ja is
  'GOLFIN sign-up modal blurb, Japanese. Shown ONLY to players whose language is '
  'Japanese — an English player never falls into it, even when description_en is '
  'empty (they get no blurb and the row collapses). Mirrors title_ja''s asymmetry.';

comment on column public.tournaments.description_key is
  'Optional BUILD-TIME localization key (LocalizationText.csv). Outranks both '
  'columns in both languages when it resolves, because a shipped key is a real '
  'translation pair. A key that does not resolve in the player''s build falls '
  'through silently and is never rendered raw. Same shape rule as name_key.';

-- ── Post-apply verification (run in the SQL editor, then over PostgREST) ─────
--
--   select column_name, data_type
--     from information_schema.columns
--    where table_schema = 'public'
--      and table_name   = 'tournaments'
--      and column_name in ('description_en', 'description_ja', 'description_key')
--    order by column_name;
--   -- expect exactly 3 rows, all `text`
--
-- Then confirm PostgREST sees them (it caches the schema; the columns must be
-- reachable BY NAME or the API select in routers/tournaments.py::list_golfin
-- 400s for the whole schedule, not just the blurb):
--
--   curl -s "$SUPABASE_URL/rest/v1/tournaments?select=slug,description_en,description_ja,description_key&limit=1" \
--        -H "apikey: $SUPABASE_SERVICE_KEY" -H "Authorization: Bearer $SUPABASE_SERVICE_KEY"
--
-- See Docs/ADMIN_DASHBOARD_OPS.md §3.2 — migration first, verify over PostgREST,
-- then deploy the API change that selects them.
