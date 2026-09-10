# IMPLEMENTER_REPORT — `loans_ops`

**Iteration shape:** `loans:ops-panel-and-telemetry`
**Iteration:** 1
**Canonical screenshot:** `screenshots/loans_row_after_force_return.png` (2880×1520)

---

## What was built

The ops half of the loan system. A **Loans panel** cloned from the Gacha ops panel (filters,
paged log, CSV of the filtered set, row → detail), a **Loans tab** in the Users drawer cloned
from `gacha-tab.tsx` plus the recipient's offers switch, and a **Loans section** in Telemetry
folded by a new pure `lib/telemetryLoans.ts`. Behind them, a migration that gives every loan a
timeline — fed by a **trigger**, so `routers/loans.py` needed only the one read-only
`GET /loans/rules` the Rules card reads — and one SQL function carrying the three support
actions in a single transaction with a mandatory note.

**The design decision worth stating.** The timeline is written by a trigger on `golfin_loans`
rather than by the router, because three different things flip a loan's status today: the six
endpoints, the lazy expiry inside `GET /loans` (which runs on whichever client reads first), and
now an admin. A timeline written by the router would have missed the other two. The actor is
inferred from the transition — `active` is only ever reached by an accept, `rescinded` is a
lender verb, the two `*expired` states are the clock — with one exception: an admin's forced
return is **indistinguishable** from a borrower's, so `golfin_loan_admin` raises a
transaction-local flag the trigger honours and writes its own row carrying the email and the note.

---

## Files modified or created

### playlife (committed `6cd9239`)

| Path | Change |
|---|---|
| `backend/migrations/2026_09_10_golfin_loan_events.sql` | **NEW** — `golfin_loan_events` (RLS on, zero policies), the status trigger, `golfin_loan_admin(loan, action, admin, note)`, a backfill, and 11 verification checks. |
| `backend/routers/loans.py` | `GET /loans/rules` only — 24 lines, read-only, unauthenticated. Nothing else in the module was touched. |
| `backend/tests/test_loans.py` | **+4 tests** — the rules payload, `/rules` not shadowing `/{loan_id}`, and the cooldown clear through the real `cooldown` path. |

### admin dashboard

| Path | Change |
|---|---|
| `lib/telemetryLoans.ts` | **NEW** — `buildLoanFunnel` (the seven `loan_*` events) and `buildLoanLifecycle` (`golfin_loans` rows), plus `isLoanLive` / `isLoanPending` / `loanAnchorMs`. Pure, no clock of its own. |
| `lib/__tests__/telemetryLoans.test.ts` | **NEW** — 12 tests over both folds and the CSV. |
| `lib/loansData.ts` | **NEW** — `fetchLoans`, `fetchLoanDetail`, `fetchLoanRaw`, `fetchUserLoans`, `fetchLoanLifecycle`, `fetchLoanRules`. Every one has a mock branch. |
| `lib/loanMutations.ts` | **NEW** — `runLoanAdminAction` (through the RPC, never an `update`) and `setLoanOffers`; both `writeAudit`. |
| `lib/loansCsv.ts` | **NEW** — `loansToCsv`, pure so it is testable. |
| `lib/mockLoans.ts` | **NEW** — 7 fixture loans covering all seven statuses, their timelines, the offers map and the rules; held on `globalThis` (see § Two things found by building it). |
| `lib/types.ts` | `LoanAdminRow`, `LoanEventDto`, `LoanStatus`, the four response shapes, `LoanRules`; `TelemetrySummaryResponse` gains `loans`. |
| `lib/telemetryData.ts` | `fetchTelemetrySummary` gains `loans: { funnel, lifecycle }`, issued alongside the event scan. |
| `lib/mockTelemetry.ts` | `SessionPlan.loans` — six lend-flow steps emitted on Home with the client's own payload keys. |
| `lib/registry.ts` | The `loans` panel entry (icon `handshake`), right after `gacha`. |
| `lib/i18n.ts` | **141 new keys**, every one `en` + `ja`. |
| `components/PanelIcon.tsx` | The `handshake` icon. |
| `app/(panels)/loans/{page,loans-panel,loan-rows}.tsx` | **NEW** — the panel, and the row/timeline/action-bar/dialog components the drawer tab reuses. |
| `app/(panels)/users/loans-tab.tsx` | **NEW** — three lists + the offers switch. |
| `app/(panels)/users/user-drawer.tsx` | `Tab` gains `"loans"`, its fetch effect, its render branch. |
| `app/(panels)/users/users-panel.tsx` | `?open=<uuid>` opens that drawer, so a name in the Loans panel is a link. |
| `app/api/loans/{route,[id]/route,[id]/actions/route,export/route,rules/route}.ts` | **NEW** — all five `checkAdmin` first. |
| `app/api/users/[id]/{loans,loan-offers}/route.ts` | **NEW** — the drawer's read and the switch's write. |
| `app/(panels)/telemetry/telemetry-panel.tsx` | The `loans` tab and `<Section id="loans">`. |
| `wrangler.jsonc` | `vars.PLAYLIFE_API_URL` — a public URL, not a secret. |
| `scripts/shoot.mjs` | **NEW** — headless-Chrome screenshotter, mock-mode only (see § Screenshots). |
| `Docs/ADMIN_DASHBOARD_OPS.md` | A Loans paragraph under § 3.0, leading with the lazy-expiry trap. |
| `Docs/AI_CONTEXT.md` | Session entry. |

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Migration applied by Cesar (verification output quoted); trigger proven on a real offer → accept → return; lazy expiry yields a `system` row; backfill count = loans count | **BLOCKED — Cesar** | I cannot run DDL (no Postgres connection string; PostgREST has no DDL path — `ADMIN_DASHBOARD_OPS.md` § 3.2). The SQL is written, **parse-checked with pglast v8.4** (10 statements + both plpgsql bodies parsed, not just the DDL around them), and is in the chat ready to paste. ⚠️ **Production currently holds ZERO `golfin_loans` rows** (`select id,status` → `rows: 0`), so check 6 will read `0 expected` and rows 7/8 will both be `0` — the backfill has nothing to backfill, and the trigger can only be proven by making a real offer afterwards. |
| `golfin_loan_admin`: `note_required` / `not_found` / `not_active` / `not_offered` / `bad_action` each hit by a test; `force_return` sets `level_at_end` exactly as `return_loan`; `clear_cooldown` makes the pair lendable again | **PASS (three ways, one row deferred)** | **Refusals**, exercised end-to-end through the live route in mock mode: `not_active` → 409 `"only an ACTIVE loan can be force-returned"`, `not_offered` → 409, short note → 400 `"note (string, 3–500 chars) is required"`, unknown loan → 404, `nuke_it` → 400 `"action must be one of force_return, cancel_offer, clear_cooldown"`. **`level_at_end`** — the SQL is `coalesce(v_level, level_at_start)` where `v_level` is `select level from golfin_progress where user_id = v_row.lender_id and kind = v_row.kind and ref_id = v_row.ref_id`; `return_loan` (lines 1144-1147) is `levels.get((row["lender_id"], row["kind"], row["ref_id"]), row["level_at_start"])` over `_owner_levels`, which selects the same three columns from the same table. Same lookup, same fallback. **`clear_cooldown`** is pinned in `TestAdminClearCooldown` through the REAL `cooldown` path in `POST /loans`: refused before, `ok` after the rows look the way the function leaves them, with the declined row still present. **Deferred:** `note_required` and `bad_action` are refused by the TypeScript layer before the RPC is reached, so the SQL's own copies of those two guards are unexercised until the migration lands. They are belt-and-braces, not the gate. |
| Panel: every filter changes the query; CSV matches the table; each action → 200 → audit row → refetch; refusals toast | **PASS** | Ten filter combinations, each asserted on the returned ids: `status=returned` → 2, `rescinded` → 2, `offer_expired` → 1, `kind=club` → 3, `q=Cratilo` → 2 (lender on one, borrower on the other), `q=char_kai` → 2, `q=<uuid>` → 1, `q=zzzznotaplayer` → **0** (an empty table, not the unfiltered one), a date window → 4, `status=bogus` → **400**. **CSV matches the table byte for byte:** for `kind=club`, `csvIds == tableIds` → `true`, 3 rows + header, `filename="golfin_loans.csv"`. **Actions:** all three ran 200 through the real widgets (`loans_action_dialog.png` is the dialog mid-type; `loans_row_after_force_return.png` is the result). **Audit:** 5 rows, actions `loan_force_return` / `loan_cancel_offer` / `loan_clear_cooldown` / `loan_offers_set`, each with a real `before`/`after` — the `clear_cooldown` row shows `answered_at` moving `2026-09-10T04:44` → `2026-08-11T04:44` (30 days) with `status` still `declined`. |
| Drawer tab: a user with 1 out + 1 in + 1 pending shows all three; the offers switch round-trips and audits | **PASS** | `screenshots/loans_drawer_tab.png` — ken: **PENDING OFFERS TO ANSWER (1)**, **LENT OUT (3)**, **BORROWED (1)**. The switch was flipped through the real button + note dialog: the tab re-rendered **OFF** with the red frame and the `not_accepting` explanation, and `loan_offers_set` landed in the audit log. |
| Telemetry: `buildLoanFunnel` + `buildLoanLifecycle` unit-tested (empty, one full lifecycle, mixed statuses, pre-offers rows with null `offered_at`); the section renders in mock and live | **PASS** | 12 tests, **305 total** (was 293). All four named cases are present, plus a stringified payload, the top-5 cap, and a case proving the router's predicates are applied to the TIMESTAMPS rather than the status column (an `active` row past `ends_at` counts 0 in `activeNow` while still counting 2 under `byStatus.active`). Renders in mock (`loans_telemetry_section.png`) and in live against production (`fetchLoanLifecycle` → `{total: 0, pendingNow: 0, activeNow: 0}` — real zeros, no throw). |
| Rules card values == `routers/loans.py` constants (endpoint response quoted) | **PASS** | Live, from the deployed API: `{"lender_share_bp":2000,"allowed_days":[1,3,7],"max_loans_out":3,"max_loans_in":3,"max_pending_in":3,"offer_ttl_hours":48,"reoffer_cooldown_hours":24,"ended_window_days":14}`. The card renders exactly those (`loans_panel_all.png`: 20% · 1d/3d/7d · 3 · 3 · 3 · 48 h · 24 h · 14 d). `TestRules` pins the payload against the module's own constant NAMES **and** against the literal values, so a rename breaks the test before the card shows a stale number. `/api/v1/loans/rulez` → **404** and `POST /api/v1/loans/rules/accept` → **403**, which is what makes the 200 mean "mounted and correct" rather than "something answers". |
| `lib/i18n.ts`: every new key en + ja; DICT lint clean; JA proof-read | **PASS** | **141 new keys**, scripted check: `missing/empty en or ja: none`, `{var} mismatch between en/ja: none`. `tsc --noEmit` exits **0** — `DictKey` is derived from `DICT`, so a `t("…")` for a key that does not exist is a compile error, which is the lint. Rendered HTML scanned for leaked keys on `/loans`, `/telemetry`, `/users` in **both** languages: clean, six for six. JA rendering read on the frames: `loans_panel_ja.png`, `loans_drawer_tab_ja.png`, `loans_telemetry_section_ja.png`. |
| Deployed: `npm run deploy` output, Cloudflare deployment id, footer stamp read live; mock mode still boots with no service key | **SEE § Deploy** | Filled in below. |
| Dashboard tests green (count before/after); backend tests green | **PASS** | Dashboard **293 → 305** (12 new, 13 files). Backend **315 → 319** (4 new). Both suites run clean; `npm run deploy` runs the dashboard suite itself and aborts on failure. |

---

## Two things found by building it

**1. Mock state must live on `globalThis`, not at module scope.** The first version exported
`MOCK_LOANS` as a module-level array. In dev, the panel's `GET` and its `POST` are different
route bundles with their own module instances, so `/api/users/:id/loans` kept showing a loan
that `/api/loans/:id/actions` had just force-returned as ACTIVE. `lib/mockStore.ts` records the
same lesson for `venues` ("only globalThis is shared between them"); `mockLoans.ts` now follows
it. Verified after the fix: the drawer route reports `status: "returned"` for the row the panel
route wrote.

**2. The screenshot script needed a row-targeted step.** An expanded row's button reads "Hide"
rather than "Timeline & actions", so it drops out of the match list and every index after it
shifts by one — which silently pointed the post-force-return shot at the wrong loan. Both the
fix and the reason are in the script's own docstring, so the next person does not rediscover it.

---

## Screenshots

Captured by `scripts/shoot.mjs` — headless Chrome over CDP, driven through the **real controls**
(the status chips, the row expander, the action button, the note textarea via React's own value
setter, the dialog's confirm, the drawer's tab). It writes real PNG files because an agent's
browser-pane screenshot reaches only the agent and writes nothing to disk. It sets the dashboard's
own mock session cookie, so it works against **mock mode only** — every frame carries the
`MOCK DATA — running on local fixtures, no Supabase connection` banner, which is the honest label
for what these are.

| File | What it shows |
|---|---|
| `loans_row_after_force_return.png` | **CANONICAL.** The timeline after a force return: `LENDER created → OFFERED`, `BORROWER OFFERED → ACTIVE`, `ADMIN ACTIVE → RETURNED` with `cesar.guarinoni@wonderwall-g.com` and the note. |
| `loans_panel_all.png` | The panel unfiltered — 7 loans, all seven statuses, and the Rules card. |
| `loans_filter_{offered,active,returned,expired,declined,rescinded,offer_expired}.png` | Each status chip, one per file. |
| `loans_action_dialog.png` | The Force-return dialog with its copy and the typed note. |
| `loans_row_rescinded_clear_cooldown.png` | A rescinded row: two lender events and a `Clear cooldown` action bar. |
| `loans_drawer_tab.png` | ken's drawer: the switch ON, 1 pending, 3 out, 1 borrowed. |
| `loans_telemetry_section.png` | The funnel strip, the KPI row, the status table and both top-5 lists. |
| `loans_audit_rows.png` | `loan_force_return` in the Audit panel with its before/after. |
| `loans_panel_ja.png`, `loans_drawer_tab_ja.png`, `loans_telemetry_section_ja.png` | The same three surfaces in Japanese. |

---

## Deploy

**API (playlife) — DONE.** `flyctl deploy` from `backend/`, **v73 → v74**, image
`playlife-api:deployment-01M255040QH3DP8W1E5AF95CTW`. Verified live, not by exit code
(memory: a 401 mid-run is not a failed deploy): `GET /api/v1/loans/rules` → **200** with the eight
constants, `/api/v1/loans/rulez` → **404**, `POST /api/v1/loans/rules/accept` → **403**.

**Dashboard — see the line below.**

<!-- DEPLOY_ID -->

**Deploying before the migration is safe here, and that was checked rather than assumed.**
`ADMIN_DASHBOARD_OPS.md` § 3.2 says migration first because code referencing a missing object
500s. Every read in this task degrades instead, and the degradation was probed **against
production with the migration not applied**:

* `golfin_loan_events` → PostgREST `PGRST205 "Could not find the table 'public.golfin_loan_events' in the schema cache"`; `isMissingRelation()` on that exact string → **true**, so the timeline renders `notMigrated` naming the file.
* `golfin_loan_admin` → `PGRST202 "Could not find the function public.golfin_loan_admin(...)"`; the mutation's matcher on that exact string → **true**, so an action answers **503** naming `2026_09_10_golfin_loan_events.sql` rather than a bare 500.
* `fetchLoans` / `fetchUserLoans` / `fetchLoanLifecycle` / `fetchLoanRules` all returned real values against production (zeros, and the live rules) with no throw.

**Mock mode boots with no service key** (§ 4.5): every screenshot above was taken from the
`admin-dashboard-mock` server, which runs on `MOCK_MODE=1` with the service key absent from its
environment.

---

## Spec deviations

1. **`fetchLoanTimeline` is folded into `fetchLoanDetail`** rather than being a second exported
   function. The panel never wants one without the other, and two exports would mean two reads
   of the same row.
2. **The ledger rows are NOT attached to the timeline**, which the spec allowed for ("if the
   ledger row does not carry the loan id, show only the `rp_to_*` totals and say so"). Read
   `golfin_loan_split` in `2026_09_09_golfin_loans.sql`: the lender's row is written by
   `earn_pts_v2` with description `<action> · loan share from <borrower display_name>` and
   idempotency key `md5(round_key || ':' || loan_id)` — a one-way hash. Neither carries the loan
   id back. The expanded row shows `rp_to_lender` / `rp_to_borrower` and a line saying exactly why.
3. **The followers join was dropped**, which the spec allowed ("only if cheap; otherwise drop and
   say so"). It is not needed: the client already ships `loan_offer_sent.via` = `search` |
   `followed`, so "how many offers went to somebody the lender does not follow" is a payload
   count rather than a join. The card is **Sent to a searched name**.
4. **A `handshake` icon was added** to `PanelIcon.tsx`. The spec said "`handshake` or the nearest
   existing"; every existing icon would have read as another gacha or content panel.
5. **`users-panel.tsx` gained a 12-line `?open=<uuid>` effect** so the spec's "click = opens that
   user's drawer" works. It reads `window.location.search` rather than `useSearchParams` so the
   client component needs no Suspense boundary, and clears the parameter after use.
6. **`scripts/shoot.mjs` is new tooling**, not in the spec's file list. Without it this task
   would have had no screenshot files at all.

---

## Console output

No errors. `read_console_messages` on the panel after a full action cycle: `No console logs.`
Dev-server log across the session shows only compile/route lines, no warnings from this code.

---

## Open questions for Architect

1. **The `cancel_offer` → cooldown coupling.** Cancelling an offer starts the pair's 24 h
   re-offer cooldown, because it goes through the same `rescinded` status a real rescind uses.
   For a support case ("the lender fat-fingered the recipient") that is usually the wrong outcome
   and needs a second click on `Clear cooldown`. The dialog copy and the success toast both say
   so. A `cancel_offer_and_clear` action, or making the clear automatic, is a one-line change if
   you want it — I did not add a fourth action beyond the five in § 3.3.
2. **Production has no loans yet.** The panel is correct and empty. Nothing in this task can be
   proven against real rows until someone lends something on the two test accounts.
