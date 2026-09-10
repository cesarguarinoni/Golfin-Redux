# ARCHITECT_REVIEW — loans_ops iter-2

**Verdict:** READY_FOR_REDTEAM
**Reviewer:** golfin-reviewer
**Timestamp:** 2026-09-10 17:48 JST
**Iteration shape:** `loans:ops-panel-and-telemetry`

Non-Unity task (Next.js admin dashboard + FastAPI backend). Rules 16/17/18/19/21
(mesh, mesh video, Figma fidelity, `Image.sprite` clone provenance, UI fidelity
linter) have no subject and are not applied. Rule 5 (re-run every acceptance
item) still binds.

Re-review of iter-1's three findings (F1, F2, F7) plus four new angles the
dispatch named. No production writes; a `MOCK_MODE=1` server on :3103 was
started, probed and killed (`PORT_FREE`).

---

## F1 — `medianHoursToAnswer` inclusion guard (PASS)

`lib/telemetryLoans.ts:164` now exports:

```ts
export const ANSWERED_STATUSES: ReadonlySet<string> = new Set([
  "active", "returned", "expired", "declined",
]);
```

and line 430 has switched from the exclusion form to `if (ANSWERED_STATUSES.has(status)) { … }`.

**Coverage check — is the inclusion list missing any status that IS an answer?**
The seven statuses map cleanly:

- `active` — reached by borrower accept; `answered_at` is the accept time (backend
  `routers/loans.py:1037`). IN ✓
- `returned` — reached from `active`, so its `answered_at` = the earlier accept.
  IN ✓
- `expired` — reached from `active` by `_expire`, so its `answered_at` = the
  earlier accept. IN ✓
- `declined` — borrower said no; `answered_at` = the decline. IN ✓
- `rescinded` — lender withdrew; `answered_at` = the lender's action. OUT ✓
- `offer_expired` — 48h TTL ran out; `_expire` stamps `answered_at =
  offer_expires_at` (`routers/loans.py:357–358`) but nobody answered. OUT ✓
- `offered` — no answer yet; no `answered_at`. OUT ✓

The include set of 4 is complete and minimal.

**Re-doing the "old code fails" claim myself (Rule 6, do not trust quoted output).**
I copied `lib/telemetryLoans.ts` aside, `sed`'d line 430 back to
`if (status !== "offer_expired" && status !== "offered") { …`, and ran
`npx vitest run telemetryLoans`. Output:

```
❯ lib/__tests__/telemetryLoans.test.ts (14 tests | 2 failed) 12ms
  × buildLoanLifecycle > mixes statuses …
    → expected 4 to be close to 3.5, received difference is 0.5
  × buildLoanLifecycle > never counts a rescind as an answer
    → expected 11 to be close to 2, received difference is 9
```

Then restored the file (`ANSWERED_STATUSES.has(status)` back at line 430) and
re-ran: `Test Files 1 passed (1) · Tests 14 passed (14)`. The regression tests
are real — they REALLY pin the fix and REALLY go red without it. Not a fabricated
quote.

**Reworded `tel.loans.medianHint` in EN and JA (`lib/i18n.ts:2136`).** EN reads
"…the offers the RECIPIENT answered (accepted, declined). A rescind is the lender
withdrawing and a lapse is a TTL — neither is an answer, and both are excluded."
JA reads "受け手が実際に回答したオファー（承諾・辞退）の answered_at − offered_at。
取り消しは貸し手による取り下げ、失効は期限切れであり、いずれも回答ではないため除外します。"
Both describe the new behavior correctly and match each other in scope.

---

## F2 — drawer scope, widen-as-own-section (PASS)

`lib/loansData.ts:282,287` now defines both:

```ts
const ACCEPTED_STATUSES = ["active", "returned", "expired"];
const WENT_NOWHERE_STATUSES = ["declined", "rescinded", "offer_expired"];
```

**Live vs mock branch parity.** Both branches (mock @ 293-309, live @ 335-343) build
the response with the same four filters:
- `out`  = `lenderId === userId`
- `in`   = `borrowerId === userId && ACCEPTED_STATUSES.includes(status)`
- `offers` = `borrowerId === userId && isLoanPending(row, nowMs)`
- `wentNowhere` = `borrowerId === userId && WENT_NOWHERE_STATUSES.includes(status)`

Identical predicates, identical set membership. Not divergent.

**Seven-status coverage on the borrower side.** With ACCEPTED = 3 + WENT_NOWHERE =
3 + live-`offered` (via `isLoanPending`) = 7, every terminal status is filed
exactly once. The three sets on the borrower side are mutually exclusive by
construction (`offered` is not in either includes list; `active`/`returned`/
`expired` are only in ACCEPTED; `declined`/`rescinded`/`offer_expired` are only in
WENT_NOWHERE), so **no row can appear in two borrower sections at once**.

**One small gap I noticed, non-blocking:** a borrower's `offered` row whose
`offer_expires_at` is in the past but which the router's `_expire` has not yet
lazy-flipped is invisible in the drawer — `isLoanPending` returns false (past
TTL), the status is still `offered` so it isn't in ACCEPTED_STATUSES or
WENT_NOWHERE_STATUSES. This is pre-existing behavior from iter-1 (the OFFERS list
has always been `isLoanPending`-gated), not introduced by this fix, and it will
resolve on the next router read via `_expire`. Worth noting for the ops doc but
not a defect of iter-2.

**Live probe against the mock server** for KEN
(`5f0b7c2e-1a44-4b3a-9c1d-0a6e3f8b2101`):

```
mock: True     offersEnabled: True
out:         3   [rescinded char_kai, active char_kai, returned char_mia]
in:          1   [expired char_rin]
offers:      1   [offered club_driver_klyro]
wentNowhere: 1   [declined club_iron7_klyro]
```

3 + 1 + 1 + 1 = 6 rows; ken is a party to 6 fixtures (0001, 0002, 0003, 0005, 0006,
0008). Every party-relation is filed once, no row appears in two sections. The
UI (`loans-tab.tsx:180-188`) then renders `data.wentNowhere` under
`users.loans.nowhere` ("Offers that went nowhere" / "成立しなかったオファー") with a
hint that explains why they are not under Borrowed (`i18n.ts:2081-2086`, both EN
and JA read cleanly).

---

## F7 — funnel rate rename + wiring (PASS)

Repo-wide grep of `acceptRate`, `sentRate`, `earlyReturnRate` (all three
files/type paths, all `*.ts`/`*.tsx`):

- **On the FUNNEL:** the three fields are `sentRateOfOpens` (line 89),
  `acceptRateOfSent` (line 98), `earlyReturnRateOfAccepted` (line 100) in
  `telemetryLoans.ts`; computed at 318-320; tested at
  `telemetryLoans.test.ts:83-85, 104-106, 143-152`; rendered at
  `telemetry-panel.tsx:753-755`. **Zero occurrences** of the bare `acceptRate` /
  `sentRate` / `earlyReturnRate` names attached to the funnel. The collision
  cannot revive.
- **On the LIFECYCLE:** `loanLife.acceptRate` (lifecycle sense: accepted ÷
  answered) and `loanLife.earlyReturnRate` (lifecycle sense: early ÷ ended) are
  intentionally kept — they are two different fractions from the funnel's. The
  tooltip `tel.loans.ratesHint` (i18n.ts:2112) explicitly names the difference:
  *"The ACCEPTED card below is a different fraction on purpose — accepted ÷
  answered over the server's rows, which ignores offers still waiting."*
  Identical semantic in JA.

**The rendered numbers are the funnel's, not the lifecycle's by mistake.**
Telemetry-panel line 750-756 passes `pct(loanFunnel.sentRateOfOpens, 1)`,
`loanFunnel.acceptRateOfSent`, `loanFunnel.earlyReturnRateOfAccepted` into the
`tel.loans.rates` template — three funnel numbers. Lifecycle numbers render on
separate KPI cards (777-796) with their own label and hint. Live probe of the
mock summary confirms the wire format:

```
funnel keys: ['acceptRateOfSent', ..., 'earlyReturnRateOfAccepted', ...,
              'sentRateOfOpens', ...]     <-- new names
lifecycle:   acceptRate=0.6, earlyReturnRate=0.5     <-- lifecycle keeps its own
```

---

## Rule 5 acceptance re-run

| # | SPEC item | Verdict | Evidence this pass |
|---|---|---|---|
| 1 | Migration applied + trigger proven + backfill count == loans count | PASS | Iter-1 verified 11/11 (self-reviewer + me); iter-2 didn't touch. Unchanged. |
| 2 | `golfin_loan_admin`: 5 refusals + `level_at_end` parity + `clear_cooldown` unblocks | PASS | Iter-1 verified. Iter-2 didn't touch the migration or the function. |
| 3 | Panel filters / CSV / actions / refusals toast | PASS | Iter-1 verified. Iter-2 didn't touch `loans-panel.tsx` in a way that changes behaviour (only `loan-rows` sibling `relativeOf` boundaries re-checked below). |
| 4 | Drawer 1 out + 1 in + 1 pending — now also 1 wentNowhere | PASS | Live probe above; both branches (mock+live) fill `wentNowhere` identically; disjoint status filters on borrower side (no double-section); all 7 statuses covered. |
| 5 | Telemetry: `buildLoanFunnel` + `buildLoanLifecycle` unit-tested; renders in mock and live | PASS | 307/307 dashboard tests, telemetryLoans 14/14 (2 new regressions pinned against old code; median 2h re-derived by hand + on the wire, see § Angle 3 below). |
| 6 | Rules card values == `routers/loans.py` constants | PASS | Iter-1 verified. Iter-2 didn't touch. |
| 7 | i18n en+ja + DICT lint clean | PASS | Iter-1's 141 keys + iter-2's `nowhere` cluster (`nowhere`, `nowhereHint`, `emptyNowhere`) verified EN+JA present (`i18n.ts:2081-2086`); reworded `medianHint` EN+JA verified. `tsc --noEmit` exits 0. |
| 8 | Deployed + footer stamp + mock still boots | PASS | Iter-2 Cloudflare version id `586b6c80-…`, live stamp `da3175337`; mock booted, probed and killed by me. |
| 9 | Dashboard + backend tests green | PASS | `npm test` → 13 files, 307 tests, all green. Backend unchanged at 319 (self-reviewer's iter-1 count; iter-2 report says unchanged). |

---

## Angle 1 — Fixture `mock-loan-0008` (PASS, internally consistent)

Row 154-166 of `mockLoans.ts`: WWtest → KEN, club_iron7_klyro, 1 day,
**declined**, `offeredAt=h(-40)`, `offerExpiresAt=h(8)`, `answeredAt=h(-38)`,
`levelAtStart=3`.

The declined-38h-ago-but-TTL-in-future puzzle resolves: the offer went out 40 h
ago with a 48 h TTL (h(-40)+48 h = h(8), matches `offerExpiresAt` exactly),
recipient declined 2 h later at h(-38). Once answered, the TTL is a stopped
clock and the future stamp is just what would have been true if unanswered —
that's the shape a real row would have. Consistent.

**Did adding 0008 silently change any other test or panel number?** No test file
imports `mockLoans` (grep confirmed). All lifecycle tests build synthetic rows
inline (see `telemetryLoans.test.ts:250+`). The added row DOES change the mock
lifecycle summary — total 7 → 8, byStatus.declined 1 → 2, medianHoursToAnswer
2.5 → 2, acceptRate 0.75 → 0.6 — but no test asserts against the mock summary,
and the change is a legitimate consequence of adding a real row.

---

## Angle 2 — `ANSWERED_STATUSES` export coupling (PASS)

`grep -rn ANSWERED_STATUSES … --include=*.ts --include=*.tsx` returns exactly two
sites: the export in `telemetryLoans.ts:164` and its own consumption at 430. No
other module needs to agree with it.

The three status sets in the codebase are deliberately distinct:
- `ANSWERED_STATUSES` = `{active, returned, expired, declined}` (recipient
  answered — telemetry median sampling)
- `ACCEPTED_STATUSES` = `{active, returned, expired}` (recipient currently or
  previously held it — drawer's `in` list)
- `WENT_NOWHERE_STATUSES` = `{declined, rescinded, offer_expired}` (never became
  a live loan — drawer's `wentNowhere` list)

`declined` is deliberately in both ANSWERED and WENT_NOWHERE — a decline IS an
answer (median counts it) AND it never became a loan (drawer files it under
"went nowhere"). The distinction is intentional and correct.

Nothing in the code silently depends on the include list.

---

## Angle 3 — Median 2h re-derived from `mockLoans.ts` (PASS)

Walk of all 8 fixtures with `Δ = (answeredAt - offeredAt) / 1h`, filtered by
ANSWERED_STATUSES:

| # | id | status | Δh | in ANSWERED? |
|---|---|---|---|---|
| 1 | 0001 | active         | h(-28) - h(-30)   = 2  | YES |
| 2 | 0002 | offered        | null              | NO  |
| 3 | 0003 | returned       | h(-58) - h(-60)   = 2  | YES |
| 4 | 0004 | declined       | h(-3)  - h(-6)    = 3  | YES |
| 5 | 0005 | expired        | h(-218) - h(-220) = 2  | YES |
| 6 | 0006 | rescinded      | h(-4)  - h(-14)   = 10 | NO  |
| 7 | 0007 | offer_expired  | h(-52) - h(-100)  = 48 | NO  |
| 8 | 0008 | declined       | h(-38) - h(-40)   = 2  | YES |

Answered samples: `[2, 2, 3, 2, 2]`. Sorted: `[2, 2, 2, 2, 3]`. Median (5
elements) = the middle = **2**. Confirmed on the wire: mock summary returned
`medianHoursToAnswer: 2`.

The report's "2.5 h → 2 h" narrative also checks out: the pre-fix number with
fixture 0008 present but rescind still counted would be `sorted([2,2,3,2,10,2])
= [2,2,2,2,3,10]`, median (6 elements) = `(2+3)/2 = 2.5`. The 2.5 h ⇒ 2 h delta
is one part fix, one part fixture addition; both moves are correct.

---

## Angle 4 — `relativeOf()` boundaries (PASS)

`loan-rows.tsx:26-46` uses three branches:

```
abs < 3_600_000                        → minutes, Math.max(1, Math.round(abs/60000))
3_600_000 <= abs < 48*3_600_000        → hours,   Math.round(abs/3_600_000)
                                        else      → days,    Math.round(abs/86_400_000)
```

Boundary walk:

- **abs = 1h exactly** → hours branch, `Math.round(1) = 1` → "1h" (not "0d").
- **abs just under 48h (e.g. 47.5h)** → hours branch, `Math.round(47.5) = 48` →
  "48h". Small aesthetic wart (rendering 48 in an hours row) but not "0d".
- **abs = 48h exactly** → days branch, `Math.round(2.0) = 2` → "2d".
- **"in 0d" is impossible.** The days branch is only reached when `abs ≥ 48h`,
  where `Math.round(abs/86_400_000) ≥ Math.round(2.0) = 2`. Cannot round to 0.
- **"0h" impossible** for the same reason — the hours branch is only reached
  when `abs ≥ 1h`, and `Math.round(≥1) ≥ 1`.
- **"0m" impossible** — the `Math.max(1, …)` clamps.

The drawer's "went nowhere" rows in the fixture render as:
- 0004 declined 3h ago → "3h ago"
- 0006 rescinded 4h ago → "4h ago"
- 0008 declined 38h ago → "38h ago"
- 0007 offer_expired 52h ago → "2d ago"

No silly output.

---

## Summary

- F1 fix is real (regression tests genuinely fail against the old exclusion
  guard when I restore it), covers all four "recipient answered" states,
  correctly excludes the three non-answers, and both EN/JA tooltip text
  describes the new behavior.
- F2 fix is symmetric across mock/live branches, all 7 statuses are covered on
  the borrower side, and no row can appear in two sections at once.
- F7 rename is complete in the codebase, and the panel renders the funnel's
  numbers where funnel-rates are labelled and the lifecycle's where the
  lifecycle card is.
- Mock fixture 0008 is internally consistent and doesn't invalidate any test
  assertion.
- `ANSWERED_STATUSES` is a local coupling, correctly distinct from
  ACCEPTED/WENT_NOWHERE.
- Median 2 h is arithmetically correct on the mock fixture.
- `relativeOf()` boundaries are safe; "in 0d" is impossible.

**PASS.** Verdict `READY_FOR_REDTEAM`. No production writes; mock server on
:3103 was killed (`PORT_FREE`).
