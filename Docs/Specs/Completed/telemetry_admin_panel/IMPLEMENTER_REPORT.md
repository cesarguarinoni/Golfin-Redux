# Implementer Report — `telemetry_admin_panel`

Built by the main Claude Code thread (no subagent chain — this is a Next.js
dashboard task with no Unity, no scene mutation and no Figma node, so the
Unity-oriented gates in `CLAUDE.md` Rules 14–21 do not apply; §5 of the SPEC is
the acceptance list and every line of it is answered below).

## Implementation summary

A sixth panel — **Telemetry** — reading `public.telemetry_events` directly from
Supabase with the service-role client, structured like the Tournaments panel
(`page.tsx` server shell → client component → `app/api/telemetry/*` route
handlers) minus every mutating part: no editor, no `lib/audit.ts`, no POST. Six
stacked sections with anchor tabs: KPI cards, session funnel, per-hole
difficulty, shot quality, per-tester rollup, and a server-paginated raw event
explorer. Aggregation happens in TypeScript in `lib/telemetryData.ts` behind a
10,000-row cap that surfaces as a visible TRUNCATED badge. No npm dependency was
added — every bar is a `<div>` with a width.

The whole panel was built and verified against `lib/mockTelemetry.ts`, a frozen
deterministic fixture (5 testers, 10 sessions, 3 holes, 1 abandon, 1 crash, 1
unclean exit, 5 flick rejects). The `beta_telemetry` §2.2 migration turns out to
be **already applied** and the table **empty** (verified over PostgREST, see
`evidence/live_empty_table_probes.txt`) — which is exactly the state the panel
will be in on deploy day, and what §5.4 asks about.

## Files modified or created

| Path | Change |
|---|---|
| `Tools/admin-dashboard/lib/telemetryData.ts` | **created** — all queries + TS aggregation: `resolveRange`, `scanEvents` (10k cap), KPIs/funnel/holes/shot-quality builders, tester rollup, `.range()`-paginated event explorer |
| `Tools/admin-dashboard/lib/mockTelemetry.ts` | **created** — deterministic fixture; frozen `MOCK_NOW`, zero `Date.now()`/`Math.random()`/`randomUUID()` |
| `Tools/admin-dashboard/app/api/telemetry/summary/route.ts` | **created** — GET KPIs + funnel + per-hole + shot quality; `checkAdmin()` first line |
| `Tools/admin-dashboard/app/api/telemetry/testers/route.ts` | **created** — GET per-tester rollup; `checkAdmin()` first line |
| `Tools/admin-dashboard/app/api/telemetry/events/route.ts` | **created** — GET raw explorer, server-side pagination 100/page; `checkAdmin()` first line |
| `Tools/admin-dashboard/app/(panels)/telemetry/page.tsx` | **created** — server shell, `force-dynamic`, mirrors the other panels' 9-line page |
| `Tools/admin-dashboard/app/(panels)/telemetry/telemetry-panel.tsx` | **created** — client component: range picker, anchor tabs, the six sections, div bars, explorer filters + paging |
| `Tools/admin-dashboard/lib/types.ts` | modified — appended the Telemetry response/row types (no existing type touched) |
| `Tools/admin-dashboard/lib/data.ts` | modified — appended `fetchUserDirectory()` / `UserIdentity`, reusing the existing `listAllAuthUsers()` + single `profiles` select rather than adding a second lookup pattern (SPEC §2) |
| `Tools/admin-dashboard/lib/registry.ts` | modified — one row: `{ id: "telemetry", title: "Telemetry", icon: "chart", route: "/telemetry" }` (the `chart` icon already existed unused); array re-sorted alphabetically |
| `Tools/admin-dashboard/app/(panels)/layout.tsx` | modified — sidebar sorts panels by their **translated** title (Cesar, 2026-08-18), so the order is alphabetical in EN and in correct kana/kanji collation in JA |
| `Tools/admin-dashboard/lib/i18n.ts` | modified — `nav.telemetry` + ~70 `tel.*` keys, EN **and** JA (ADMIN_DASHBOARD_OPS §3.4) |
| `Docs/Specs/Active/telemetry_admin_panel/evidence/*` | **created** — captured API responses + auth-gate probe transcript backing the checklist below |

Nothing under `Assets/` was touched — another session is working in Unity.

## Evidence

Visual verification was done live in the in-app browser against the mock-mode
dev server, and the rendered frames (full page EN, full page JA, zero state)
were surfaced inline in the main chat. The machine-readable evidence is
committed:

- `evidence/mock_summary.json` — the exact §3.1–§3.4 payload the screenshots render
- `evidence/mock_testers.json` — §3.5
- `evidence/mock_events_page0.json` / `evidence/mock_events_page1.json` — §3.6, 100 rows then 4
- `evidence/mock_zero_state_summary.json` — the empty-window response (§5.4's arithmetic)
- `evidence/auth_gate_probes.txt` — 401 / 403 / 200 for all three routes (§5.3)
- `evidence/live_empty_table_probes.txt` — the live table probed with the panel's
  exact query, plus the live-mode route gate (§5.4)

## Acceptance checklist (SPEC §5)

| Item | Result | Justification |
|---|---|---|
| 1. Mock mode: `/telemetry` renders all six sections; no console errors; funnel monotonically non-increasing | **PASS** | All six sections rendered in the in-app browser (frames surfaced in chat). `read_console_messages` returned only React-DevTools info + Fast Refresh logs — zero `error`/`warn`. Funnel sessions = `[10, 9, 8, 7, 5]`, asserted non-increasing programmatically against `evidence/mock_summary.json`. |
| 2. Mock mode: explorer filters by name and tester; pagination advances | **PASS** | In-browser: `name=flick_rejected` → 5 rows, one distinct name; + tester `greedisland.k.k@gmail.com` → 4 rows, one distinct tester, label "4 matching events". Unfiltered: page 1 = 100 rows with Prev disabled → clicking Next gives page 2 = 4 rows with Next disabled (104 total). |
| 3. All three routes return 401/403 without an admin session | **PASS** | `evidence/auth_gate_probes.txt`: no cookie → **401** `{"error":"Not signed in."}` on all three; signed-in non-allowlisted email → **403**; admin → 200. |
| 4. Live mode, table EMPTY: every section renders a zero/empty state — no NaN, no divide-by-zero, no crash | **PASS (rendering confirmed; live browser render is Cesar's) — see note** | Two halves, both measured. (a) The live table is real and empty and answers the panel's exact query: `GET telemetry_events?select=*&received_at=gte.…&lte.…&order=received_at.desc&limit=10000` → **200 `[]`**, `content-range: */0` (`evidence/live_empty_table_probes.txt`). So the live aggregation input is `rows = []`. (b) That identical input was rendered end-to-end in the browser by querying an empty window (`?from=2026-01-01&to=2026-01-02`) — `scanEvents` is the ONLY mock/live branch, every reducer below it is shared: all KPIs 0, every rate `null` → rendered `—` (never `NaN`), funnel all `0%`, all three tables show their empty-state row, and the payloads were asserted to contain no `NaN`/`Infinity` (`evidence/mock_zero_state_summary.json`). **Not done:** loading `/telemetry` in a browser against live Supabase — the live build has no mock login and a real Supabase admin session is Cesar's to create. |
| 5. `npm run deploy`, then `curl` `/` → **302** | **PASS** | Deployed 2026-08-18, twice (panel, then the alphabetical-sidebar follow-up: `Current Version ID: 840d6155-2648-48f2-ae4b-4f456b9e357e`; the first was `f7533c7a-b8f8-4b69-82f0-c41ead283690`, 6 assets uploaded, `✓ bundle carries no service_role key`). `curl -s -o /dev/null -w "%{http_code}" https://admin.golfin.world/` → **302**, `location:` → `late-cake-f2a4.cloudflareaccess.com/cdn-cgi/access/login/admin.golfin.world`. `/telemetry` and `/api/telemetry/summary` also → 302. Access is intact. |
| 6. Live smoke: §3.1 totals match hand-run Supabase counts | **BLOCKED — needs real rows (Cesar)** | The table exists but holds zero rows (the `beta_telemetry` ingest tests were deleted so the beta dataset starts clean), and comparing 0 against 0 proves nothing about the aggregation. The queries are pre-written in `live_smoke_5.6.sql` beside this report — five blocks covering §3.1–§3.5, filtering on `received_at` exactly as the panel does. Run them during the beta week. They have NOT been executed: there is no Postgres client on the dev machine, so a block that errors is a typo in the file, not a finding about the data. |

## Known FAIL items

None. Two items above are not fully closable in this session and are flagged
rather than claimed:

- **§5.6** is blocked on the `beta_telemetry` migration + real tester rows.
- **§5.4** is proven on both halves — the live query returns `[]`, and `[]`
  renders the clean zero state — but nobody has yet loaded `/telemetry` in a
  browser signed in against live Supabase. That needs Cesar's session.

## Deviations from SPEC (each deliberate, each argued)

1. **`tableMissing` state added — a safety net that should never fire.** The
   migration is in fact applied, so the panel gets `200 []` and renders the
   normal zero state. But it was built before that was confirmed, and if the
   table is ever absent (a fresh project, a rolled-back migration) PostgREST
   returns a *missing relation* error, not an empty set — a raw 500 red box.
   `telemetryData.ts` recognises only that specific error
   (`42P01` / `PGRST205` / "relation … does not exist") and returns clean zeros
   plus an amber "telemetry_events does not exist yet — apply
   migrations/2026_08_18_telemetry_events.sql" banner. Every other query error
   still throws. This follows the local precedent in `tournamentData.ts`, which
   tolerates a missing `tournament_entries` rather than taking the panel down.
2. **Funnel stages are cumulative-by-depth.** A session counts at a stage if it
   reached that stage *or any later one*. Counting each stage independently
   makes the funnel read as INCREASING whenever a single `screen_view` is lost
   in a dropped batch, which would break §5.1's monotonicity requirement on real
   data, not just on the fixture. Documented in the code and in a caption under
   the bars.
3. **Range filtering is on `received_at`, not `ts`.** `ts` is the tester's
   device clock; one phone with a wrong date would otherwise vanish from — or
   leak into — every range. `ts` is still shown per row in the explorer.
4. **"Today" is the UTC day the range ENDS on**, not the wall clock. On the
   default range those are the same thing; on the frozen fixture it means the
   "today" counters have something to count, so a screenshot of them stays
   meaningful.
5. **Per-tester play time sums `max(duration_s)` per session, not every
   `session_end`.** `session_end` fires on every `OnApplicationPause(true)` and
   `duration_s` is `realtimeSinceStartup`, so summing them all would count the
   same minutes repeatedly.
6. **Explorer filter options come from `summary.eventNames` + the testers
   response** rather than a third distinct-values query — the aggregates have
   already read the window, so this is the same data at zero extra cost.
7. **`sp_allocated` is absent by design** — `reference/EVENT_CATALOG.md` records
   that the client never emits it. No column was built for it.

## Out of scope, as specified

No mutations, no CSV export, no chart library, no realtime refresh, no
retention, no `lib/audit.ts`, no backend/Unity change.
