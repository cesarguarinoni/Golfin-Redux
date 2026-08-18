# SPEC — `telemetry_admin_panel`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

A sixth panel — **Telemetry** — in the admin dashboard (`Tools/admin-dashboard`,
live at https://admin.golfin.world) so Cesar and Ken can read next week's
20-tester beta results at a glance: who played, how far they got, whether the
shot controls held up, and what crashed. Read-only; no mutations, no audit
writes.

Depends on the `beta_telemetry` spec's `telemetry_events` table (its §2.2
migration). The panel can be BUILT and verified entirely in mock mode before
that migration is applied — do not block on it.

## Reference

- Event names + payload fields: `Docs/Specs/Active/beta_telemetry/SPEC.md` §1–§2
  (the single source of truth; do not invent fields not listed there).
- Panel-shape reference: the Tournaments panel (`app/(panels)/tournaments/` +
  `app/api/tournaments/`) is the most complete existing example per
  ADMIN_DASHBOARD_OPS §3.1 — but note it is a *mutating* panel; Telemetry copies
  its structure, not its editor/mutation parts.
- No Figma. Match the existing panels' Tailwind look (cards, tables, badges —
  reuse whatever shared styling the Users/Points panels use).

## Figma Fidelity

N/A — no Figma. Visual bar: consistent with the existing five panels.

## Architecture context

- **Stack:** Next.js 15 App Router + TS + Tailwind on Cloudflare Workers
  (OpenNext), Supabase via service_role. All existing — nothing new to install
  EXCEPT no chart library: render bars/funnels with plain divs + Tailwind
  widths (a dependency added here bloats the Worker for five <div> bars).
- **Existing code referenced (verified 2026-08-18):**
  - `lib/registry.ts` — `PANELS` array + `PanelIcon` union; `"chart"` icon
    already exists in the union and is unused. Add
    `{ id: "telemetry", title: "Telemetry", icon: "chart", route: "/telemetry" }`.
  - `lib/auth.ts` — `checkAdmin()`; first line of every route handler.
  - `lib/mode.ts` / `lib/mock.ts` / `lib/mockStore.ts` — mock-mode plumbing;
    follow how the tournaments/points APIs branch to mock data.
  - `lib/supabaseAdmin.ts` — the service-role client used by existing routes.
  - `lib/format.ts` — date/number formatting helpers; reuse.
  - `app/(panels)/layout.tsx` — sidebar builds from the registry; no edit needed.
- **NOT used:** `lib/audit.ts` — read-only panel, nothing to audit.

## 1. Files

```
app/(panels)/telemetry/page.tsx          server shell (like other panels' page.tsx)
app/(panels)/telemetry/telemetry-panel.tsx   client component, tabbed sections
app/api/telemetry/summary/route.ts       GET — KPIs + funnel + per-hole + shot quality
app/api/telemetry/testers/route.ts       GET — per-tester rollup
app/api/telemetry/events/route.ts        GET — raw event explorer (filters + pagination)
lib/telemetryData.ts                     queries + aggregation (mirrors tournamentData.ts naming)
lib/mockTelemetry.ts                     mock rows: 5 fake testers, 2 sessions each, incl. one crash + one abandon
```

Every route handler: `checkAdmin()` first, 401/403 passthrough exactly like the
existing routes.

## 2. Data access pattern

Volume is tiny (20 testers × 1 week — tens of thousands of rows at worst), so:
**fetch filtered rows, aggregate in TypeScript** in `lib/telemetryData.ts`. No SQL
views, no RPCs, no migration in this spec. Every query takes a date-range filter
(`from`, `to`; default last 7 days) and is capped at **10,000 rows** per fetch
(`.limit(10000)` + a `truncated: true` flag in the response when hit, surfaced as
a badge in the UI — never silently). Columns are per `beta_telemetry` §2.2:
`event_id, user_id, session_id, name, ts, received_at, app_version, build_number,
platform, device_model, os, payload`.

Tester display names: map `user_id` → email/username the same way the Users
panel does — NOTE: reuse the exact lookup in whatever `lib/data.ts` (or the
users API) already does; do not write a second profiles query pattern. Falling
back to a truncated uuid is fine when no profile row exists.

## 3. Sections (one page, stacked; anchor tabs at top)

### 3.1 KPI cards (top row)
From `summary`: **Active testers** (distinct user_id in range) · **Sessions**
(distinct session_id) · **Rounds started** (`round_start` count) · **Holes
completed** (`hole_complete`) · **Abandons** (`round_abandoned`, styled amber
when rate >20%) · **Crashes** (`client_error` count, red when >0). Each card
shows the count; testers/sessions also show "today" beneath.

### 3.2 Funnel
Per-session progression, computed from events: session_start → reached `Home`
(screen_view) → reached `HoleSelection` OR `TournamentHoleSelection`
(screen_view) → `round_start` → `hole_complete`. Bar per stage = % of sessions
that reached it (plain div bars, width %, count labels). This is the
"where do testers stop" view.

### 3.3 Per-hole table
One row per `payload.hole` seen in `round_start`/`hole_complete`: plays,
completions, abandons, avg strokes, avg penalty strokes, OB rate (% of that
hole's `shot_taken` with `terminal == "OB"`), avg duration, `fps_low` median.
Sort by hole number. Red-tint the OB-rate cell >25% and fps_low <20.

### 3.4 Shot quality cards
From `shot_taken` / `flick_rejected` / `shot_cancelled` totals: **Flick reject
rate** = rejected / (rejected + taken) · **Cancel rate** · **OB rate overall** ·
**Avg distance by club** (small table: club, shots, avg `distance_m`). This is
the "do the controls work" view — flick reject rate is the headline number of
the whole beta, give it the biggest card.

### 3.5 Testers table
Per user: name/email, platform + device_model + os (mode of their sessions),
app_version/build (latest seen), sessions, total play time (Σ session_end
`duration_s`; count sessions with no session_end separately as "unclean exits"),
rounds, holes completed, points delta (last `points_changed.balance` − first),
crashes, last seen (`max(received_at)`). Row click → filters §3.6 to that user.

### 3.6 Event explorer
Raw table: received_at, ts, tester, name, session_id (truncated), payload
(pretty-printed, collapsed to one line, click to expand). Filters: event name
(select, populated from the data), tester (select), date range. Server-side
pagination, 100/page, ordered `received_at desc` — this endpoint queries with
`.range()` and does NOT load everything like the aggregates do.

## 4. Mock mode

`lib/mockTelemetry.ts` returns a deterministic fixture (no `Date.now()`
randomness — fixed timestamps relative to a hardcoded base date) large enough
to light up every section: 5 testers, ~10 sessions, 2 holes, one abandon, one
crash, a few flick rejects. All three routes branch to it under the same
condition existing routes use (`lib/mode.ts`). This is also how the panel is
verified before the `telemetry_events` migration lands.

## 5. Acceptance tests

1. Mock mode: `/telemetry` renders all six sections with the fixture; no console
   errors; funnel percentages are internally consistent (monotonically
   non-increasing).
2. Mock mode: event explorer filters by name and tester; pagination advances.
3. All three API routes return 401/403 without an admin session (curl, no cookie).
4. Live mode with the table EMPTY (migration applied, no rows): every section
   renders a zero/empty state — no NaN, no divide-by-zero, no crash. This is the
   state the panel will actually be in on deploy day.
5. `npm run deploy`, then `curl -s -o /dev/null -w "%{http_code}" https://admin.golfin.world/` → **302** (ADMIN_DASHBOARD_OPS §2 — a 200 means Access broke; stop).
6. Live smoke once rows exist: totals in §3.1 match hand-run counts in the
   Supabase SQL editor for the same range.

## 6. Out of scope

- Any mutation (delete/purge events, tester management) — read-only v1.
- Chart libraries, CSV export, realtime/auto-refresh (manual reload is fine).
- Retention/cleanup, sampling, the Unity/backend half (`beta_telemetry` spec).
- Backend changes on playlife-api — this panel reads Supabase directly like
  every other panel.
