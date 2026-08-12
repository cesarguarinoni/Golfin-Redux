# GOLFIN Admin Dashboard (v1)

Internal admin tool for the GOLFIN game over the PLAYLIFE Supabase project
(Postgres + Supabase Auth). **v1 is read-only** — user list, profile detail,
points ledger, activities, economy catalog — plus login. Designed to grow
panel-by-panel via `lib/registry.ts`.

Stack: Next.js (App Router) + TypeScript + Tailwind CSS. Fully self-contained
in this folder (intended location in the Unity repo: `Tools/admin-dashboard`).

## Run

```bash
npm install
npm run dev        # http://localhost:3000
```

Production build:

```bash
npm run build
npm start
```

With no configuration at all, the app boots straight into **mock mode** (see
below) — no Supabase project needed.

## Environment setup

Copy `.env.local.example` to `.env.local` and fill in:

| Var | Meaning |
| --- | --- |
| `SUPABASE_URL` | Supabase project URL (server-side) |
| `SUPABASE_SERVICE_ROLE_KEY` | service_role key — **server-side only**, never shipped to the browser (`lib/supabaseAdmin.ts` is guarded by the `server-only` package) |
| `NEXT_PUBLIC_SUPABASE_URL` | same project URL, exposed to the browser for login |
| `NEXT_PUBLIC_SUPABASE_ANON_KEY` | anon key for the browser login client |
| `ADMIN_EMAILS` | comma-separated allowlist; non-listed signed-in users get a "not an admin" page. Enforced server-side on every data route. |
| `MOCK_MODE` | set `1` to force mock mode even with a service key present |

`.gitignore` already excludes `.env.local`. No secrets live in the code.

## Mock vs live behavior

**Mock mode** is active when `SUPABASE_SERVICE_ROLE_KEY` is absent **or**
`MOCK_MODE=1`:

- A yellow **MOCK DATA** banner shows on every page.
- Login accepts any email with any password (form is labeled MOCK MODE); the
  admin allowlist is still enforced after sign-in, so a non-allowlisted email
  lands on the "not an admin" page — same flow as live.
- All data comes from `lib/mock.ts` — 5 fixture users mirroring the live DB
  snapshot from 2026-08-12, plus ledger rows and the economy catalog.
- Default allowlist (when `ADMIN_EMAILS` unset):
  `cesar.guarinoni@wonderwall-g.com,cesar.guarinoni@gmail.com`.
- `writeAudit()` logs to the server console instead of Postgres.

**Live mode** (service key present, `MOCK_MODE` unset):

- A red **PRODUCTION — live PLAYLIFE database** banner shows on every page.
- Login is real Supabase email/password (`@supabase/ssr` cookie sessions,
  refreshed in `middleware.ts`).
- Data routes use the service_role client server-side:
  `auth.admin.listUsers()` joined with `public.profiles`, per-user
  `points_transactions` / `activities` on drawer open.

## RP rule

The game's Reward Points == `total_points` (= `activity_pts` + `gift_pts`).
The UI displays **RP** as `total_points` everywhere; the activity/gift split
appears only in the user detail drawer.

## Migration

`migrations/2026_08_13_admin_audit_log.sql` creates `public.admin_audit_log`
(the audit trail for future mutation features; unused by the v1 UI but wired
via `lib/audit.ts`).

Apply it in the Supabase SQL editor (or `psql`) against **staging first**, run
the verification queries in the file's footer, then apply to production. The
script is idempotent — safe to re-run.

## Architecture

```
app/(panels)/users/     Users panel (list, filters, stat cards, detail drawer)
app/login, app/not-admin, app/api/…   auth + read-only data routes
lib/registry.ts         panel registry — the sidebar builds itself from this
lib/supabaseAdmin.ts    service_role client (server-only)
lib/data.ts             data access, mock/live branching
lib/mock.ts             fixtures
lib/audit.ts            writeAudit → admin_audit_log
lib/auth.ts             session + allowlist gate (used by every data route)
migrations/             SQL migrations
```

Adding a panel: create `app/(panels)/<id>/page.tsx`, add an entry to
`PANELS` in `lib/registry.ts`. Done — the sidebar picks it up.
