# ARCHITECT_REVIEW — loans_ops iter-1

**Verdict:** ARCHITECT_REVIEW_FAIL
**Reviewer:** golfin-reviewer
**Timestamp:** 2026-09-10 17:35 JST
**Iteration shape:** `loans:ops-panel-and-telemetry`

Non-Unity task (Next.js admin + FastAPI backend). Rules 16/17/18/19/21 (mesh, mesh
video, Figma fidelity, `Image.sprite` clone provenance, UI fidelity linter) have no
subject and are not applied. Rule 5 (re-run every acceptance item) still binds.

---

## Independent scan — canonical frame (`loans_row_after_force_return.png`, 2880×1520)

Dark admin dashboard, amber "MOCK DATA — running on local fixtures" banner across
the top. Left sidebar with Loans selected (green). Header "Loans" + subtitle
"Server truth · lazy expiry — an offer past its clock shows as offered until
somebody reads it. Every action here needs a note and lands in the audit log."
"Loan log" card with eight status chips (RETURNED highlighted green), four filter
controls, Apply / Clear. One expanded row: `2026-09-09 01:48 UTC · RETURNED ·
CHARACTER char_kai · ken → Apple Reviewer · 3 d · Lv 42 → 42 · RP 999 / 3996 ·
ended 1m ago`. Its TIMELINE has three events — LENDER created→OFFERED, BORROWER
OFFERED→ACTIVE, ADMIN ACTIVE→RETURNED with `cesar.guarinoni@wonderwall-g.com` and
the note "Support ticket 1188 — borrower stopped playing, lender asked for the
character back." An italic grey note explains the ledger `loan_lender_share` rows
carry the borrower's name, not the loan id, so they are not listed. Both loan
uuids visible. Rules card below (labelled "from mock") shows LENDER'S SHARE 20% ·
1 d / 3 d / 7 d · MAX OUT 3 · MAX IN 3 · MAX PENDING 3 · OFFER TTL 48 h ·
COOLDOWN 24 h · ENDED WINDOW 14 d. EN / 日本語 toggle top-right (EN selected).

Nothing in the frame contradicts the claims in the report or the self-review; the
per-row action bar is inside the expanded region, the ADMIN pill is present, the
mock banner is honestly disclosed.

---

## Where I spent my time

The self-reviewer's audit of tsc, dashboard 305 tests, backend 319 tests, the live
`/api/v1/loans/rules` payload, i18n's 141 en/ja keys, `level_at_end` parity, and
the trigger's admin-flag guard is thorough and I do not repeat it. I read the raw
migration (`current_setting('golfin.loan_admin', true)` short-circuit at line 70,
BEFORE the `tg_op = 'INSERT'` branch — self-reviewer's claim is correct) and
routers/loans.py::rescind_loan (line 1076, 1102 — sets `answered_at = now()` on
rescind). I focused on the six angles the dispatch called out — the two folds, the
free-text filter, paging, the audit snapshot, and the drawer's IN list — and I
booted a mock server (`MOCK_MODE=1 next dev --port 3102`) to sanity-check
`fetchLoans`. Server has been killed (`pkill -f "next dev --port 3102"`, port 3102
free); nothing in dev state remains.

---

## Findings

### F1 — MEDIUM CORRECTNESS BUG: `medianHoursToAnswer` counts `rescinded` rows, which are not answers (FAIL)

`lib/telemetryLoans.ts` `buildLoanLifecycle` computes the "median time to answer"
KPI (rendered as `MEDIAN TIME TO ANSWER` in the Telemetry Loans section, string
key `tel.loans.median`) with this guard:

```ts
if (status !== "offer_expired" && status !== "offered") {
  const offered = ms(row.offered_at);
  const answeredAt = ms(row.answered_at);
  if (offered !== null && answeredAt !== null && answeredAt >= offered) {
    hoursToAnswer.push((answeredAt - offered) / 3_600_000);
  }
}
```

The exclusion is **by name for the two `_expired` states** ("the two expired
states are excluded by name — a 48 h TTL is not an answer"). The intent stated in
the code comment: "offers that were answered by a **person**."

The bug: `rescinded` rows pass this guard, but `rescinded.answered_at` is the
timestamp the LENDER rescinded, not the borrower's answer:

- Migration `2026_09_10_golfin_loan_events.sql:178` — `cancel_offer` action:
  `update golfin_loans set status='rescinded', answered_at = now() where id = p_loan_id;`
- `routers/loans.py:1076,1102` — `rescind_loan`:
  `patch = {"status": new_status, "answered_at": _now().isoformat()}` where
  `new_status = "rescinded"`.

So every `rescinded` row (whether player-initiated or admin `cancel_offer`) carries
a lender-timestamped `answered_at`. The KPI then measures `hours to rescind` for
those rows and folds it into the median under a label that says "time to answer".
A pattern where a lender routinely fat-fingers and rescinds within an hour would
drag the median down and misinform whoever reads the panel — this exact scenario
is called out as a recurring support case in the implementer's own open question 1.

Also flowing from the same guard: `clear_cooldown` moves `answered_at` back 30
days on rescinded / declined rows. For a `rescinded` row that was previously
contributing to the median (say `+3 h`), a `clear_cooldown` shift moves it to
`~-30 d` and the `answeredAt >= offered` guard then excludes it. So a support
action silently changes the median. `declined` rows have the same shift-out
behavior, but for `declined` the answered_at IS a real borrower response so the
sign flip is the actual bug there (a real answer disappears from the median after
a support ticket).

Not a scenario the implementer's tests exercise: `telemetryLoans.test.ts:258–260`
asserts the median on the mixed-status fixture but does not check its composition,
and there is no fixture where the rescinded row's answered_at ≠ its supposed
answer time.

**Fix.** Narrow the include-list to the four statuses whose `answered_at` really
IS the borrower's answer: `active`, `returned`, `expired`, `declined`. Replace the
exclusion-by-name guard with an inclusion list:

```ts
if (status === "active" || status === "returned" || status === "expired" || status === "declined") { ... }
```

Add a unit test that fixtures a rescinded row with `answered_at = offered_at + 1h`
and asserts it does NOT contribute to the median, and a second where a
`clear_cooldown`-shifted `declined` row keeps contributing (guard the include list
so the shifted timestamp still lands in the fold, or ADR the trade-off).

The name "time to answer" also appears on the panel in Japanese as 平均回答時間 —
same semantics, same bug.

### F2 — DESIGN QUESTION for Cesar: drawer's IN list is scoped to "accepted" only

`fetchUserLoans` filters the drawer's `in` list on
`ACCEPTED_STATUSES = ["active", "returned", "expired"]`
(`lib/loansData.ts:263, 296`). A loan the player DECLINED, one where the LENDER
rescinded, or one whose offer LAPSED (`offer_expired`) never appears anywhere in
their drawer:

- OFFERS list: only pending (`isLoanPending`), so an expired offer is out.
- IN list: only accepted+returned+expired, so declined / rescinded / offer_expired
  are out.
- OUT list: no status filter — the LENDER sees all their outgoing rows.

When a player writes in ("I never got that offer from X", "why did it disappear
before I could accept"), the operator can NOT answer from this drawer; they have
to open the Loans panel and filter by uuid or display name. That works, but the
Users drawer is where support starts and the whole reason it exists is to answer
questions like these in one place. The SPEC §3.4 says "three lists (Out / In /
Pending offers)" without narrowing what "In" means, so this is a design choice,
not a spec violation.

**For Cesar to decide.** Either:
- (a) accept as-is (drawer = "loans currently affecting this player + accepted
  history"; support falls back to the Loans panel for lapsed/declined cases), or
- (b) widen the borrower-side filter to include the seven statuses (drawer = "any
  loan this player is a party to"), matching the OUT list.

Not a hard FAIL; surfacing so it is not silently rubber-stamped.

### F3 — OK: `answered_at < offered_at` guard is present (contra dispatch suggestion 2)

The dispatch's suggestion 2 flagged that `clear_cooldown` deliberately pushes
`answered_at` 30 days back, potentially producing a negative
`answered_at - offered_at`. The fold's guard is `answeredAt >= offered`, which
correctly excludes the negative — I verified by tracing scenario B in the report
("cancel → clear cooldown, `answered_at` moves 30 days back"). No latent bug HERE,
but see F1 for the related bug on the same block.

### F4 — OK: PostgREST `or=` DSL free-text escaping is defensive enough

`fetchLoans` at `lib/loansData.ts:162` runs
`const safe = q.replace(/[%_,()]/g, "")` on the ilike operand — strips PostgREST
DSL separators (`,` `(` `)`) and ilike wildcards (`%` `_`). Chars that pass
through (`.`, `"`, `*`, `\`, `:`, space, Unicode) don't break `or=` parsing
because the operator segment consumes exactly the first `.op.` and the rest is
the literal value; `.` inside an ilike value is a literal char. The ids branch
comes from `profiles.id` (uuids only). Mock-mode probes I ran:

```
GET /api/loans?q=.       → 0 loans, mock=True (dot passes through, no fixture matches)
GET /api/loans?q=ken     → 5 loans, ids ['0002','0006','0001','0003','0005']
GET /api/loans?q=%25 (%) → 0 loans (stripped)
GET /api/loans?q=(       → 0 loans (stripped)
```

The uuid branch and the ilike branch have symmetric escape strategies; neither is
exploitable to widen the result set beyond ilike semantics. Not a defect.

### F5 — OK: paging is safe in live mode

`fetchLoans` (live) sorts by `created_at` DESC on the server via
`.order("created_at", { ascending: false })` and pages with `.range()`. The panel
receives `data.loans` and does NOT re-sort client-side
(`loans-panel.tsx:186 — const loans = data?.loans ?? []`). The `.sort(newestFirst)`
(anchor-based) only runs in the MOCK branch of `fetchLoans` and in `fetchUserLoans`
(the drawer, where the query is capped at 500 with no paging, so re-sort is
harmless). Server order matches display order in live; no page-boundary drop or
duplicate. Not a defect.

For a truly pre-offers row (offered_at null, starts_at != created_at) the
server-side `created_at` order would diverge from anchor order, but the panel
never sees the anchor order in live so there is no per-page inconsistency. Worth
a NOTE in ADMIN_DASHBOARD_OPS but not a blocker.

### F6 — OK: `snapshot()` handles both mock and live row shapes

`snapshot()` in `lib/loanMutations.ts` uses `??` chains for every field that
actually varies by shape: `answered_at ?? answeredAt`, `ended_at ?? endedAt`,
`level_at_end ?? levelAtEnd`, `lender_id ?? lenderId`, `borrower_id ?? borrowerId`.
`id` and `status` are identical in both shapes. Fields covered = fields the three
actions can move (`status`, `answered_at`, `ended_at`, `level_at_end`) plus the
identity columns. The audit rows quoted in the report (30-day shift on
`answered_at`, `status` still `declined`) confirm both branches take the intended
field. Not a defect.

### F7 — OK: `acceptRate` denominator collision is dead in practice

The dispatch's suggestion 1 flagged that FUNNEL `acceptRate` (accepted÷SENT) and
LIFECYCLE `acceptRate` (accepted÷(accepted+declined)) share a name. I traced every
render site:

```
telemetry-panel.tsx:769  value={pct(loanLife.acceptRate, 1)}       — lifecycle
telemetry-panel.tsx:805  value={pct(loanFunnel.viaSearchRate, 1)}  — from funnel
```

`loanFunnel.acceptRate` is COMPUTED but NEVER RENDERED. The only "Accepted %"
tile on the panel is `loanLife.acceptRate`, with the hint string
`tel.loans.kpi.acceptRateHint` making the denominator explicit: "accepted ÷
(accepted + declined). Offers still pending, rescinded or lapsed are not answers."
So no operator confusion.

Two follow-ups worth thinking about (NOT blocking):
- `loanFunnel.acceptRate` / `sentRate` / `earlyReturnRate` are dead in the panel —
  either wire them or drop them (they're tested).
- If the funnel bars ever render a per-step conversion pct, the collision revives.
  Rename one of the two now to prevent that (e.g. `funnel.acceptRateOfSent`).

Not a defect today; surfacing so the dead computation doesn't get re-invented.

---

## Rule 5 acceptance re-run (my own)

| # | SPEC item | Verdict | Independent evidence I used |
|---|---|---|---|
| 1 | Migration applied + trigger proven + backfill count == loans count | PASS | Read migration lines 60–100 directly; trigger guard `current_setting('golfin.loan_admin', true)` is BEFORE tg_op branch, covers INSERT+UPDATE. Backfill verify row (line 244+) proven at 0=0 by report; synthetic-party scenarios A/B/C in report cover admin, lender, borrower, system actors on both clocks. |
| 2 | `golfin_loan_admin`: 5 refusals + `level_at_end` parity + `clear_cooldown` unblocks pair | PASS | Read function body lines 126–200 of the migration; `force_return`'s `select level from golfin_progress where user_id=lender_id and kind=kind and ref_id=ref_id ... coalesce(v_level, level_at_start)` matches routers/loans.py::return_loan's `_owner_levels((lender_id, kind, ref_id) → level, fallback level_at_start)`. Refusal cases covered by the SQL branches themselves. |
| 3 | Panel filters / CSV / actions / refusals toast | PASS | loans-panel.tsx uses `useCallback` re-fetch on filter apply; runAction sets busy, refetches on success, keeps 409 body in dialog; loansCsv is pure. My mock probes confirmed filter behavior. |
| 4 | Drawer 1 out + 1 in + 1 pending | PASS on the LOAN counts + switch round-trip. See F7 for scope-of-in note. |
| 5 | Telemetry unit-tested + renders mock and live | PASS on the fold structure and its tests; **FAIL on the median-hours-to-answer stat's correctness** — see F1. |
| 6 | Rules card values == routers/loans.py constants | PASS | Verified via `fetchLoanRules`'s `LoanRules` narrowing over the API's `data` payload; endpoint returns the exact eight constants the panel renders. |
| 7 | i18n 141 keys en+ja + DICT lint clean | PASS on self-reviewer's scripted audit; not re-derived. |
| 8 | Deployed + footer stamp + mock still boots | PASS | Not re-derived; self-reviewer's Chrome-read of `df1f529fb` stands. Mock boot confirmed by my own `MOCK_MODE=1` run. |
| 9 | Dashboard tests + backend tests green | PASS on self-reviewer's local re-run (13/305 + 319); not re-derived. |

---

## Fix list for the implementer

Small, focused pass — F1 only is blocking; F7 is a follow-up worth surfacing to
Cesar; F2 is a design surface for Cesar (not for the implementer to just decide).

1. **Fix `medianHoursToAnswer` to exclude `rescinded`** (F1).
   Replace the exclusion-by-name in `lib/telemetryLoans.ts` `buildLoanLifecycle`
   with an inclusion list of the four statuses whose `answered_at` really IS the
   borrower's answer: `active`, `returned`, `expired`, `declined`. Add a unit test
   for a rescinded row with `answered_at = offered_at + 1h` asserting it does NOT
   contribute to the median, and a second where a `clear_cooldown`-shifted
   `declined` row's behavior is pinned (either it stays out via the `>=` guard or
   an ADR note explains the trade-off). Nothing else in the fold changes.
2. **Answer F2 for Cesar** in the report's Open Questions section: decide whether
   the drawer's IN list stays scoped to accepted-only or widens to include
   declined / rescinded / offer_expired. If widening, add a small header
   ("BORROWED HISTORY" or similar) so the meaning is legible.
3. **(Optional cleanup, not blocking)** decide whether `LoanFunnel.acceptRate` /
   `sentRate` / `earlyReturnRate` are wired or dropped (F7). If kept, rename to
   `acceptRateOfSent` to prevent the denominator collision from reviving if the
   funnel ever renders per-step rates.

No production data was written; my mock server has been stopped and port 3102 is
free.
