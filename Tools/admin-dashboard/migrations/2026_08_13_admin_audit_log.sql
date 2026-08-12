-- ============================================================================
-- Migration: 2026_08_13_admin_audit_log
-- Project:   GOLFIN Admin Dashboard (PLAYLIFE Supabase project)
-- Purpose:   Audit trail for admin dashboard actions. Every future mutation
--            performed through the dashboard writes one row here (see
--            lib/audit.ts → writeAudit). v1 of the dashboard is read-only,
--            so this table starts empty — it is created now so the write
--            path is wired and testable before mutations ship.
-- Idempotent: safe to run repeatedly (IF NOT EXISTS / OR REPLACE style).
-- ============================================================================

create table if not exists public.admin_audit_log (
  id          uuid primary key default gen_random_uuid(),
  at          timestamptz not null default now(),
  admin_email text not null,
  action      text not null,
  target_user uuid,
  table_name  text,
  before      jsonb,
  after       jsonb
);

comment on table public.admin_audit_log is
  'Audit trail for GOLFIN admin dashboard actions. One row per admin mutation: who (admin_email), what (action/table_name), on whom (target_user), and the before/after snapshots. Written only via service_role from the dashboard server.';

-- ----------------------------------------------------------------------------
-- SECURITY
-- RLS enabled with NO policies: anon and authenticated roles can see nothing.
-- service_role bypasses RLS, which is exactly the only writer/reader we want.
-- Grants are revoked explicitly as defense in depth on top of RLS.
-- ----------------------------------------------------------------------------
alter table public.admin_audit_log enable row level security;

revoke all on table public.admin_audit_log from anon;
revoke all on table public.admin_audit_log from authenticated;

grant select, insert on table public.admin_audit_log to service_role;

-- ----------------------------------------------------------------------------
-- STAGING VERIFICATION
-- Run after applying on staging, before production:
--
--   1. Table exists with expected shape:
--        select column_name, data_type from information_schema.columns
--        where table_schema = 'public' and table_name = 'admin_audit_log'
--        order by ordinal_position;
--      -- expect: id/uuid, at/timestamptz, admin_email/text, action/text,
--      --         target_user/uuid, table_name/text, before/jsonb, after/jsonb
--
--   2. RLS is on and there are no policies:
--        select relrowsecurity from pg_class
--        where oid = 'public.admin_audit_log'::regclass;          -- expect: t
--        select count(*) from pg_policies
--        where schemaname = 'public' and tablename = 'admin_audit_log'; -- 0
--
--   3. anon/authenticated are locked out (via PostgREST with anon key):
--        GET /rest/v1/admin_audit_log  -- expect: 401/permission denied
--
--   4. service_role can insert + read back:
--        insert into public.admin_audit_log (admin_email, action)
--        values ('staging-check@wonderwall-g.com', 'staging_verification');
--        select count(*) from public.admin_audit_log
--        where action = 'staging_verification';                   -- expect: 1
--        delete from public.admin_audit_log
--        where action = 'staging_verification';
-- ============================================================================
