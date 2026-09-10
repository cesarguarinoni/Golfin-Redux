# IMPLEMENTER_REPORT — `loans_ops`

**Iteration shape:** `loans:ops-panel-and-telemetry`
**Iteration:** 3
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
| Migration applied by Cesar (verification output quoted); trigger proven on a real offer → accept → return; lazy expiry yields a `system` row; backfill count = loans count | **PASS** | **Applied by Cesar 2026-09-10, 11/11 green** — `events_table 1`, `status_trigger 1`, `admin_function 1`, `events_rls_on 1`, `events_zero_policies 0`, `backfill_covers_every_loan 0`, `loans_count 0`, `loans_with_events 0`, `admin_fn_revoked_from_anon 1`, `admin_fn_revoked_from_authenticated 1`, `trigger_honours_admin_flag 1`. Re-derived from the database rather than taken from the paste: `golfin_loan_events` now answers **200** (it was 404 an hour earlier) and the RPC answers `{"status":"not_found"}` instead of `PGRST202`. **The trigger was then proven on production** through a full lifecycle on four throwaway loans between two SYNTHETIC party ids (`…0000deadbeef` → `…0000cafebabe`), so no real account was touched and no real asset was ever locked — see § The trigger, proven. Rows 7/8 read `0 = 0` because production holds no loans, so the backfill correctly had nothing to backfill. |
| `golfin_loan_admin`: `note_required` / `not_found` / `not_active` / `not_offered` / `bad_action` each hit by a test; `force_return` sets `level_at_end` exactly as `return_loan`; `clear_cooldown` makes the pair lendable again | **PASS** | **All five refusals now hit at the SQL layer itself**, on production, plus a sixth the spec did not list: `note_required` (a 1-char note), `admin_required` (an empty admin), `not_found` (an unknown id), `not_active` (force-returning an already-returned loan), `not_offered` (force-returning an offer), `bad_action` (`nuke_it`). The first three and `bad_action` were the ones deferred while the function did not exist; they are no longer deferred. The same six are refused a second time by the TypeScript layer with the same statuses mapped to 400/404/409 — exercised end-to-end through the live route in mock mode. **`level_at_end`** — the SQL is `coalesce(v_level, level_at_start)` over `select level from golfin_progress where user_id = lender_id and kind = kind and ref_id = ref_id`; `return_loan` (lines 1144-1147) is `levels.get((lender_id, kind, ref_id), level_at_start)` over `_owner_levels`, which selects the same three columns from the same table. Same lookup, same fallback — and the production run took the fallback branch and wrote `level_at_end = 42` from `level_at_start = 42`, as `return_loan` would. **`clear_cooldown`** moved `answered_at` **30 days** back on production with `status` still `rescinded`, and is pinned in `TestAdminClearCooldown` through the REAL `cooldown` path in `POST /loans`: refused before, `ok` after. |
| Panel: every filter changes the query; CSV matches the table; each action → 200 → audit row → refetch; refusals toast | **PASS** | Ten filter combinations, each asserted on the returned ids: `status=returned` → 2, `rescinded` → 2, `offer_expired` → 1, `kind=club` → 3, `q=Cratilo` → 2 (lender on one, borrower on the other), `q=char_kai` → 2, `q=<uuid>` → 1, `q=zzzznotaplayer` → **0** (an empty table, not the unfiltered one), a date window → 4, `status=bogus` → **400**. **CSV matches the table byte for byte:** for `kind=club`, `csvIds == tableIds` → `true`, 3 rows + header, `filename="golfin_loans.csv"`. **Actions:** all three ran 200 through the real widgets (`loans_action_dialog.png` is the dialog mid-type; `loans_row_after_force_return.png` is the result). **Audit:** 5 rows, actions `loan_force_return` / `loan_cancel_offer` / `loan_clear_cooldown` / `loan_offers_set`, each with a real `before`/`after` — the `clear_cooldown` row shows `answered_at` moving `2026-09-10T04:44` → `2026-08-11T04:44` (30 days) with `status` still `declined`. |
| Drawer tab: a user with 1 out + 1 in + 1 pending shows all three; the offers switch round-trips and audits | **PASS** | `screenshots/loans_drawer_tab.png` — ken: **PENDING OFFERS TO ANSWER (1)**, **LENT OUT (3)**, **BORROWED (1)**. The switch was flipped through the real button + note dialog: the tab re-rendered **OFF** with the red frame and the `not_accepting` explanation, and `loan_offers_set` landed in the audit log. |
| Telemetry: `buildLoanFunnel` + `buildLoanLifecycle` unit-tested (empty, one full lifecycle, mixed statuses, pre-offers rows with null `offered_at`); the section renders in mock and live | **PASS** | 12 tests, **305 total** (was 293). All four named cases are present, plus a stringified payload, the top-5 cap, and a case proving the router's predicates are applied to the TIMESTAMPS rather than the status column (an `active` row past `ends_at` counts 0 in `activeNow` while still counting 2 under `byStatus.active`). Renders in mock (`loans_telemetry_section.png`) and in live against production (`fetchLoanLifecycle` → `{total: 0, pendingNow: 0, activeNow: 0}` — real zeros, no throw). |
| Rules card values == `routers/loans.py` constants (endpoint response quoted) | **PASS** | Live, from the deployed API: `{"lender_share_bp":2000,"allowed_days":[1,3,7],"max_loans_out":3,"max_loans_in":3,"max_pending_in":3,"offer_ttl_hours":48,"reoffer_cooldown_hours":24,"ended_window_days":14}`. The card renders exactly those (`loans_panel_all.png`: 20% · 1d/3d/7d · 3 · 3 · 3 · 48 h · 24 h · 14 d). `TestRules` pins the payload against the module's own constant NAMES **and** against the literal values, so a rename breaks the test before the card shows a stale number. `/api/v1/loans/rulez` → **404** and `POST /api/v1/loans/rules/accept` → **403**, which is what makes the 200 mean "mounted and correct" rather than "something answers". |
| `lib/i18n.ts`: every new key en + ja; DICT lint clean; JA proof-read | **PASS** | **141 new keys**, scripted check: `missing/empty en or ja: none`, `{var} mismatch between en/ja: none`. `tsc --noEmit` exits **0** — `DictKey` is derived from `DICT`, so a `t("…")` for a key that does not exist is a compile error, which is the lint. Rendered HTML scanned for leaked keys on `/loans`, `/telemetry`, `/users` in **both** languages: clean, six for six. JA rendering read on the frames: `loans_panel_ja.png`, `loans_drawer_tab_ja.png`, `loans_telemetry_section_ja.png`. |
| Deployed: `npm run deploy` output, Cloudflare deployment id, footer stamp read live; mock mode still boots with no service key | **SEE § Deploy** | Filled in below. |
| Dashboard tests green (count before/after); backend tests green | **PASS** | Dashboard **293 → 305** (12 new, 13 files). Backend **315 → 319** (4 new). Both suites run clean; `npm run deploy` runs the dashboard suite itself and aborts on failure. |

---

## Iteration 2 — the review's fail list

`golfin-reviewer` returned `ARCHITECT_REVIEW_FAIL` on iter-1 with one blocking bug, one design
question and one cleanup. All three are addressed. Dashboard tests **305 → 307**; backend
unchanged at 319; `tsc --noEmit` exit 0.

### F1 (blocking) — `medianHoursToAnswer` counted rescinds. FIXED.

The reviewer is right, and the code said so in its own hint string. `buildLoanLifecycle` guarded by
EXCLUSION — `status !== "offer_expired" && status !== "offered"` — which let `rescinded` through.
But `answered_at` on a rescinded row is when the **lender withdrew**: `routers/loans.py::_terminal_answer`
stamps it for a decline AND a rescind, and `golfin_loan_admin`'s `cancel_offer` does the same. The
recipient never answered. A card labelled **Median time to answer** was averaging in the lender's
change of mind, and the tooltip admitted it — it read "(accept, decline, rescind)".

The guard is now an **inclusion** list, `ANSWERED_STATUSES = {active, returned, expired, declined}`,
exported so it is nameable and testable. The tooltip was rewritten in both languages to say a
rescind and a lapse are not answers.

**The old test pinned the bug**, asserting median 4 over a set whose rescinded row contributed 10 h.
Corrected to 3.5, and two regression tests added. **Both were proven to fail against the old code**
before the fix was kept — restoring the exclusion guard turns the suite red with exactly:

```
× buildLoanLifecycle > mixes statuses …        → expected 4 to be close to 3.5
× buildLoanLifecycle > never counts a rescind as an answer → expected 11 to be close to 2
```

The second regression also pins that a rescind still counts everywhere ELSE (`total`, `byStatus`) —
only the median ignores it. A third new test pins the `answered_at < offered_at` case that
`clear_cooldown` deliberately creates: the sample is dropped rather than counted negative.

On the mock fixture the number moved **2.5 h → 2 h**, hand-checked against the five real answers
(2, 2, 3, 2, 2) with the 10 h rescind excluded.

### F2 (design) — the drawer's BORROWED scope. DECIDED BY CESAR, IMPLEMENTED.

The reviewer was right that this was asymmetric: LENT OUT showed every status while BORROWED showed
only `active`/`returned`/`expired`, so "why did that offer disappear?" was unanswerable in the
drawer. Cesar's call (2026-09-10): **widen it, as its own section.** BORROWED stays accepted-only so
its count keeps meaning "things they actually held", and a fourth section — **Offers that went
nowhere** — carries `declined` / `rescinded` / `offer_expired`, with a line explaining why they are
not filed under BORROWED. Between the four sections no row the player is a party to is invisible.

`UserLoansResponse` gains `wentNowhere`, filled in both the live and the mock branch. A mock fixture
(`…mock-loan-0008`, WWtest → ken, declined) was added so the section renders with content rather
than an empty state: `screenshots/loans_drawer_tab.png` now shows all four, **OFFERS THAT WENT
NOWHERE (1)** among them.

### F7 (cleanup) — the two `acceptRate`s. RENAMED AND WIRED.

The reviewer found the collision I flagged for it was latent rather than live: the funnel's three
rates were computed and tested but never rendered, so only the lifecycle's `acceptRate` reached the
screen. Both halves are fixed rather than one:

* **Renamed**, so the collision cannot revive: `sentRate` → `sentRateOfOpens`, `acceptRate` →
  `acceptRateOfSent`, `earlyReturnRate` → `earlyReturnRateOfAccepted`. The doc comment on
  `acceptRateOfSent` names the other one and says why the denominators differ.
* **Wired**, because SPEC § 2 asks for exactly these three ("sent/open, accept/sent,
  early-return/accepted") and they were the part of that line not on screen. They now render as a
  line under the funnel bars, with a tooltip that points at the ACCEPTED card below and says its
  fraction is deliberately different.


### Found while writing the brief for red-team — a hole the review under-called

`golfin-reviewer` passed iter-2 but noted "one small pre-existing gap (stale `offered` past TTL
invisible until `_expire` fires)" and waved it through. It is not small, and it is not
pre-existing — it was **introduced by the F2 fix**, in the one place it could do the most damage.

An `offered` row whose 48 h clock has run out is **not pending** (so not in OFFERS), **not
accepted** (so not in BORROWED), and its status column still literally reads `offered` (so not in
the went-nowhere list). It appeared in **no section of the drawer at all** — while my own comment
above the filters claimed "no row a borrower is party to is invisible in the drawer".

Because expiry is LAZY, that is not a corner: a row keeps saying `offered` until some client's
`GET /loans` gets around to flipping it. And it is exactly the case the went-nowhere section was
added for — a player asking "why did that offer disappear?" about an offer that lapsed while
nobody was looking.

**Fixed by construction rather than by adding a fourth status list.** The three borrower lists now
come from ONE total classifier, `borrowerSection(row, borrowerId, nowMs)`, in the pure module so it
is testable. A lapsed offer is filed as went-nowhere on the strength of its **clock**, which is
what `_expire` will stamp it as anyway — the same "trust the timestamps, not the status column"
rule the lifecycle card already follows.

**Five tests**, the important one enumerating all seven statuses and asserting each lands in exactly
one section, so a status added later fails here rather than vanishing. The hole was demonstrated
before it was fixed: a throwaway test reconstructing the shipped three-list filters verbatim
returned `[false, false, false]` for a lapsed row — matched by none of them — and the classifier
returns `wentNowhere` for the same row.

A mock fixture (`…mock-loan-0009`, Cratilo → ken, `offered`, clock ran out 4 h ago) makes the state
visible: ken's drawer now shows **OFFERS THAT WENT NOWHERE (2)**, the declined club and the lapsed
character. Dashboard tests **307 → 312**.

### The three angles the reviewer cleared

PostgREST `or=` escaping, page-boundary drops, and the `snapshot()` `??` chains were each checked
and found sound; its reasoning matches the code as I read it. No change.

---

## Iteration 3 — red-team's blocker, and the shape behind it

`golfin-redteam-reviewer` returned `ARCHITECT_REVIEW_FAIL` on **a fourth defect of the same
shape**, and it was right.

### The defect

`clockLine()` in `loan-rows.tsx` was a `switch` on status with arms for `offered` / `active` /
`returned` / `expired` and a **`default` reading "answered {rel}"**. Three statuses fell into that
default and only one of them is an answer:

| status | what the row rendered | truth |
|---|---|---|
| `declined` | answered 3h ago | ✅ the recipient answered |
| `rescinded` | **answered 4h ago** | ❌ the LENDER pulled the offer back |
| `offer_expired` | **answered 2d ago** | ❌ a 48 h clock ran out unseen |

Both wrong labels are visible in the iter-1 screenshots I read and surfaced — `answered 2d ago`
next to `OFFER EXPIRED`, `answered 4h ago` next to `RESCINDED`. I looked at those frames and did
not see it, which is the "read the whole frame, not just your feature" lesson landing again. The
green 19-test suite never asserted a clock LABEL, only the relative-time arithmetic behind it.

It is the highest-damage spot for this bug: the panel exists to answer "why did that offer
disappear?", and for a lapsed offer it told the operator the recipient had responded.

### The shape, and what was done about it

Four defects, one shape: **a status classified by an incomplete list, in a file that had no
business making that judgement alone.**

1. `medianHoursToAnswer` guarded by exclusion → counted `rescinded`.
2. A lapsed `offered` row matched none of the drawer's three lists → rendered nowhere.
3. Both shipped under comments asserting the opposite of the code.
4. `clockLine`'s `default` → called a rescind and a lapse an answer.

Fixing the fourth instance alone would have left the conditions for a fifth. Per
`PIPELINE_HARDENING` § 22 the shape was fixed instead: **every judgement about a loan's status or
its clocks now lives in one pure module, `lib/loanStatus.ts`** — `isLoanLive`, `isLoanPending`,
`loanAnchorMs`, `ANSWERED_STATUSES`, `borrowerSection`, and the two that moved out of the React
component, `clockLabel` and `actionsFor`. Its header names all four defects so the next reader
knows why it exists. `telemetryLoans.ts` keeps the two folds and imports from it; `loan-rows.tsx`
now only RENDERS the label the module chooses.

`ALL_LOAN_STATUSES` is exported and **the tests iterate it**, so a status added later fails the
suite instead of landing in a `default`. `lib/__tests__/loanStatus.test.ts` — 17 tests — asserts a
per-status table for `clockLabel`, another for `actionsFor`, another for `borrowerSection`, and a
cross-check that `ANSWERED_STATUSES` and `clockLabel` cannot drift apart about what an answer is.
Restoring the old `default` arm turns **four** of them red, including that cross-check.

### Two more things the same pass fixed

* **`clockLabel` has a truthful fallback.** An unrecognised status now renders "last changed
  {rel}" — never a claim about who acted. That is what let `rescinded` inherit "answered".
* **`actionsFor` reads the clock, not just the column** (red-team's non-blocking wart W1). It took
  a bare status, so a lapsed-but-unswept offer still showed **Cancel offer** — and the server's
  guard is on the status column, so the action would have SUCCEEDED and started a 24 h cooldown on
  a pair whose offer had already died on its own. Same for **Force return** on an `active` row past
  `ends_at`. Both are now withheld once the clock has run out.

Three new bilingual strings: `loans.rescindedAt` ("pulled back"), `loans.lapsedAt` ("lapsed
unanswered"), `loans.changedAt` ("last changed"). The `DictKey` type caught all three as compile
errors before they existed, which is the DICT lint doing its job.

Verified in the running panel — all nine fixture rows now say something true:
`RESCINDED → pulled back 4h ago`, `OFFER EXPIRED → lapsed unanswered 2d ago`,
`DECLINED → answered 3h ago`, `OFFERED (lapsed) → offer lapsed 4h ago — flips on next read`.

Dashboard tests **312 → 324**, 14 files. Backend 319 unchanged. `tsc --noEmit` exit 0.

---

## The trigger, proven — on production, 2026-09-10

The spec asked for this on prod. It was run there, on **four throwaway loans between two
SYNTHETIC party ids** (`00000000-0000-4000-8000-0000deadbeef` → `…0000cafebabe`) rather than on
real accounts: no player's asset was ever locked, nothing appeared in anyone's client, and all
four loans plus every event they produced were deleted at the end. `golfin_loans` and
`golfin_loan_events` both held **0 rows before and 0 rows after** — quoted below.

**A. offer → accept → admin force return.** Each step wrote exactly one row, with the actor the
trigger inferred:

```
lender    created   -> offered
borrower  offered   -> active
admin     active    -> returned  [cesar.guarinoni@wonderwall-g.com] "Support 1188 - lender asked for it back"
```

That third line is the whole reason the transaction-local flag exists. A forced return is
byte-for-byte the same transition a borrower's return makes, so without the flag it would read
`borrower`. It reads `admin`, with the email and the note. `golfin_loan_admin` answered
`{"status":"ok","action":"force_return","from_status":"active","to_status":"returned"}` and the
row came back `status=returned`, `level_at_end=42` — the `level_at_start` fallback, because a
synthetic lender has no `golfin_progress` row, which is exactly the branch `return_loan` takes in
the same situation.

Then, on that same loan: a repeat → `not_active`, the wrong verb (`cancel_offer` on a returned
loan) → `not_offered`, and `nuke_it` → `bad_action`.

**B. offer → admin cancel → clear the cooldown it started.** `answered_at` moved **30 days** back
and `status` stayed `rescinded`, so the history is intact and the window is over:

```
lender    created   -> offered
admin     offered   -> rescinded  [cesar.guarinoni@wonderwall-g.com] "Support 1189 - wrong recipient"
admin     rescinded -> rescinded  [cesar.guarinoni@wonderwall-g.com] "Support 1189 - let them re-offer"
```

The third line is a status change of nothing, deliberately: `clear_cooldown` moves a timestamp
rather than a status, and an admin touching a loan has to leave a trace either way.

**C. the lazy expiry `routers/loans.py::_expire` performs → a `system` row, on both clocks.**

```
lender    created   -> offered        system  offered  -> offer_expired      (the 48 h offer clock)
lender    created   -> active         system  active   -> expired            (the loan's own clock)
```

**Cleanup.** `golfin_loans now: 0 rows`, `golfin_loan_events now: 0 rows`.

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

**Dashboard — DONE.** `npm run deploy` (which ran the suite first: **13 files, 305 tests, all
green**, `telemetryLoans.test.ts` among them, then the OpenNext build, then wrangler).

| | |
|---|---|
| **Cloudflare Version ID** | iter-1 `476b78f6-fdf7-47e9-8c1d-acc4355e57a3` · iter-2 `586b6c80-dfde-47a2-a938-19d1b12fdb7a` · iter-2b `c5171f03-e999-4097-a0f4-e0a1050fd157` · **iter-3 `70a0c4f4-f20f-4b6e-a31e-5aa6769b55f6`**. Each redo shipped because each changed something that renders on production. |
| Worker | `golfin-admin` → `admin.golfin.world` (custom domain) |
| Assets | 9 new or modified uploaded, 94 already there; startup 24 ms |
| New binding | `env.PLAYLIFE_API_URL ("https://playlife-api.fly.dev")` |
| § 2 Access check | `curl https://admin.golfin.world/` → **302** (Access is protecting it) |
| § 23 stamp, built worker | `grep` of `api/version/route.js` → `df1f529fb`, `da3175337`, `3080690e5`, `8f823d7cb`. **No `-DIRTY`** on any of the four. |
| § 23 stamp, **live site** | `GET /api/version` on admin.golfin.world, read in Cesar's Chrome, answers **`{"commit":"8f823d7cb","stamped":true}`** — the iter-3 commit. ⚠️ **The sidebar footer is not a reliable stamp: it is a CACHED page.** Twice it kept showing the previous commit after a deploy, which reads exactly like a silent failure, and the second time a `?cachebust=` query string did not clear it either. `/api/version` is `force-dynamic` and therefore uncached — it is the authoritative read, and the § 23 memory has been corrected to say so. |

**Deploying before the migration was safe, and that was checked rather than assumed** (the migration has since landed, so this section is now history — it is kept because it is the evidence that the ordering was safe).
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
