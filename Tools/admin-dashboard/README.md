# GOLFIN Admin Dashboard

Internal admin tool for the GOLFIN game over the PLAYLIFE Supabase project
(Postgres + Supabase Auth). Four panels — **Users** (list, detail,
admin mutations, RP adjust), **Points** (global ledger viewer), **Tournaments**
(schedule authoring, rank-band prize editor, card-art upload), **Audit Log**
(admin_audit_log viewer) — plus login. Designed to grow panel-by-panel via
`lib/registry.ts`.

Stack: Next.js (App Router) + TypeScript + Tailwind CSS. Fully self-contained
in this folder (intended location in the Unity repo: `Tools/admin-dashboard`).

## ⚠️ Never run `npm run build` while `npm run dev` is running

`next dev` and `next build` share the same `.next/` directory and overwrite each
other's artifacts. A build run against a live dev server leaves the running
server serving HTML that references chunk files the build deleted: every
`/_next/static/chunks/*.js` and the stylesheet return **404**, so the page loads
unstyled and never hydrates — no login, no panels, nothing clickable. The server
logs look clean, which is what makes it confusing.

Recovery: stop the server, `rm -rf .next`, start it again.

Related trap, and it bites when starting the server from tooling rather than a
terminal: if `NODE_ENV` is inherited as `production`, `next dev` compiles
middleware in a mode the Edge sandbox rejects and every page 500s with
`EvalError: Code generation from strings disallowed for this context`. The
startup banner warns about a "non-standard NODE_ENV" one line before it happens.
Start with `NODE_ENV=development npm run dev` when in doubt.


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

## Admin mutations (Users panel, phase 2)

All mutations: server-enforce the `ADMIN_EMAILS` allowlist on the route
handler (never just in UI), write one row to `admin_audit_log` via
`lib/audit.ts` (with before/after snapshots), and show a confirmation modal
(labeled **MOCK** in mock mode). In mock mode they mutate the in-memory
fixtures (`lib/mockStore.ts`) so the UI visibly updates.

| Action | Endpoint | Live implementation |
| --- | --- | --- |
| Edit username | `PATCH /api/users/:id` | writes `profiles.display_name` **and** auth `user_metadata.display_name` (mirrors the prod sync trigger's shape); inline edit in the drawer header |
| Resend confirmation | `POST /api/users/:id/actions` | `auth.resend({type:'signup'})` — disabled when already confirmed |
| Send password reset | `POST /api/users/:id/actions` | `auth.resetPasswordForEmail(email)` |
| Manually confirm email | `POST /api/users/:id/actions` | `auth.admin.updateUserById(id, {email_confirm:true})` |
| Ban / Unban | `POST /api/users/:id/actions` | `updateUserById` with `ban_duration:'876000h'` (≈100y) / `'none'` |
| Delete user | `DELETE /api/users/:id` | `auth.admin.deleteUser(id)` — type-the-email double confirm (also verified server-side) plus a red cascade warning: FK cascade removes `profiles`, `points_transactions`, `activities` |

### Adjust RP (grant / deduct)

In the drawer: **Adjust RP** — signed integer amount plus a required reason
(max 200 chars). The ledger description becomes `admin: <reason>`.

- **Positive** amounts (live): `rpc earn_pts_v2(p_user_id, p_action:
  'manual_admin_grant', p_amount, p_description, p_key: random uuid)`.
- **Negative** amounts (live): `rpc spend_pts(p_user_id, p_amount:
  abs(amount), p_description, p_key)`; a `{status:'insufficient'}` payload is
  surfaced as a friendly 409 error.
- Mock mode simulates both paths, including the insufficient branch and the
  debit order (**activity points first, then gift points** — a big deduction
  can produce two spend rows, one per currency).
- Both directions write an `rp_adjust` audit row with the profile's
  activity/gift/total snapshot before and after.

VERIFIED 2026-08-13 against the deployed functions (`pg_get_function_arguments`
on the live project): `earn_pts_v2(p_user_id uuid, p_action text, p_pts integer,
p_description text, p_key uuid)` and `spend_pts(p_user_id uuid, p_amount integer,
p_reason text, p_key uuid)` — these are what `lib/mutations.ts` calls.

## Points panel

Global `points_transactions` viewer (read-only): reverse-chronological,
25/page, filters for currency (activity/gift), type, user email search, and
date range. Columns: when, user email, type, signed amount + currency,
description, idempotency key (truncated, monospace). Live mode reads the most
recent 500 rows.

## Audit Log panel

Read-only viewer of `public.admin_audit_log` (when, admin, action, target
user, table, before/after JSON — hover for full payloads). Starts empty; it
fills as soon as mutations run against the live database. In mock mode it
shows the in-memory audit entries created by mock mutations.

## Migration

`migrations/2026_08_13_admin_audit_log.sql` creates `public.admin_audit_log`
(the audit trail for future mutation features; unused by the v1 UI but wired
via `lib/audit.ts`).

Apply it in the Supabase SQL editor (or `psql`) against **staging first**, run
the verification queries in the file's footer, then apply to production. The
script is idempotent — safe to re-run.

## Architecture

```
app/(panels)/users/     Users panel (list, filters, stat cards, drawer, mutations)
app/(panels)/points/    Points panel (global ledger viewer)
app/(panels)/audit/     Audit Log panel (admin_audit_log viewer)
app/login, app/not-admin, app/api/…   auth + data/mutation routes
lib/registry.ts         panel registry — the sidebar builds itself from this
lib/supabaseAdmin.ts    service_role client (server-only)
lib/data.ts             read access, mock/live branching
lib/mutations.ts        phase-2 mutations (server-only, all audited)
lib/mock.ts             fixture seed data
lib/mockStore.ts        mutable in-memory mock DB (globalThis-backed)
lib/audit.ts            writeAudit → admin_audit_log (mock: in-memory log)
lib/auth.ts             session + allowlist gate (used by every route)
migrations/             SQL migrations
```

Adding a panel: create `app/(panels)/<id>/page.tsx`, add an entry to
`PANELS` in `lib/registry.ts`. Done — the sidebar picks it up.
