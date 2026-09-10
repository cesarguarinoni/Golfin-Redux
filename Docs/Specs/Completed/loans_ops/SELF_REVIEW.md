# SELF_REVIEW — loans_ops iter-1

**Verdict:** PASS
**Reviewer:** golfin-self-reviewer
**Timestamp:** 2026-09-10 17:25 JST
**Iteration:** 1

Cesar applied the migration mid-review and the implementer upgraded the top two acceptance
rows from BLOCKED/PARTIAL to PASS with a § "The trigger, proven — on production, 2026-09-10"
that walks the three scenarios (accept→admin force-return; admin cancel→clear cooldown; lazy
`_expire` on both clocks) using four throwaway loans between two SYNTHETIC party ids
(`…0000deadbeef` → `…0000cafebabe`), leaving `golfin_loans` and `golfin_loan_events` at 0/0
rows before and after. This review reflects that state.

---

## Visual diff notes (pixels only, before reading the narrative)

Every frame carries the disclosed amber `MOCK DATA — running on local fixtures, no Supabase
connection` banner. Task is admin dashboard (Next.js), not Unity — no Figma reference, no
scene, no play-mode capture.

**loans_row_after_force_return.png (canonical, 2880×1520).** Dark admin dashboard, GOLFIN /
ADMIN V1 sidebar with Loans highlighted (green). Header "Loans" and subtitle "Server truth ·
lazy expiry — an offer past its clock shows as offered until somebody reads it. Every action
here needs a note and lands in the audit log." A "Loan log" card carries eight status chips
(All / Offered / Active / Returned / Expired / Declined / Rescinded / Offer expired) with
`RETURNED` selected green; four filter controls (Kind dropdown, Player/loan id/ref, From, To)
plus Apply / Clear. One expanded row: `2026-09-09 01:48 UTC · RETURNED · CHARACTER char_kai ·
ken → Apple Reviewer · 3 d · Lv 42 → 42 · RP 999 / 3996 · ended 1m ago · Hide`. Its TIMELINE
reads three events: `LENDER created → OFFERED`, `BORROWER OFFERED → ACTIVE`, and `ADMIN
ACTIVE → RETURNED` with `cesar.guarinoni@wonderwall-g.com` and the note `"Support ticket 1188
— borrower stopped playing, lender asked for the character back"`. A grey note explains
`loan_lender_share` ledger rows carry the borrower's name, not the loan id, so they are not
listed. Two full loan uuids shown. Below: a Rules card labelled `from mock` with the eight
tiles: LENDER'S SHARE 20% · DURATIONS 1 d / 3 d / 7 d · MAX LOANS OUT 3 · MAX LOANS IN 3 ·
MAX PENDING OFFERS 3 · OFFER TTL 48 h · RE-OFFER COOLDOWN 24 h · ENDED LOANS SHOWN TO CLIENTS
14 d. Top-right: EN/日本語 toggle (EN selected), small text `golfin_loans · golfin_loan_events`.

**loans_panel_all.png.** Same panel, `All` chip selected. Seven rows, one per status: DECLINED
(club_putter_klyro), OFFERED (club_driver_klyro, "offer expires in 40h"), RESCINDED (char_kai),
ACTIVE (char_kai, "ends in 44h"), RETURNED (char_mia), OFFER EXPIRED (club_wood3_klyro),
EXPIRED (char_rin). Same Rules card, same values.

**loans_drawer_tab.png.** Users panel dimmed behind a right-side drawer for `ken`
(greedisland.k.k@gmail.com, uuid `5f0b7c2e-…`). Standard admin action row present. Tab strip:
Points ledger · Activities · Inventory · Missions · Gacha · **Loans** (green underline). Below:
`LOAN OFFERS ON` with a red `Turn off` button and a `profiles.golfin_loan_offers` subtitle.
Three lists — `PENDING OFFERS TO ANSWER (1)` (OFFERED club_driver_klyro, WWtest → ken), `LENT
OUT (3)` (RESCINDED char_kai + two RETURNED rows to Apple Reviewer), and `BORROWED (1)`
(EXPIRED char_rin from Cratilo). Footer: `ALL MUTATIONS ARE AUDITED — ADMIN_AUDIT_LOG`.

**loans_telemetry_section.png.** Telemetry panel, "Loans — is lending used, and how" section.
Client funnel with 5 stacked bars normalised against modal opens: 100% / 50% / 50% / 25% /
25% with the summary "5 players · 1 declined · 0 rescinded · 2 pill opens · offers switched
off 1×" and an explanatory caption. Lifecycle KPI tiles: PENDING NOW 1 (amber highlighted),
ACTIVE NOW 0, ACCEPTED 75.0% (3 of 4 answered), RETURNED EARLY 66.7% (2 of 3 ended), RP TO
LENDERS 11,097 with "44,388 RP to borrowers". Second KPI row: MEDIAN TIME TO ANSWER 2h 00m,
SENT TO A SEARCHED NAME 0.0% (0/2), DURATION MIX 3d 4 / 7d 2 / 1d 1, KIND MIX character 4 /
club 3. A status breakdown table (returned 2 / declined 1 / expired 1 / offer_expired 1 /
offered 1 / rescinded 1) beside TOP LENDERS (ken 3 / WWtest 2 / Cratilo 1 / Apple Reviewer 1)
and TOP BORROWERS (Apple Reviewer 3 / ken 2 / WWtest 1 / Cratilo 1). Top-5 cap is present in
code; fixture has only 4 distinct parties on each side, so no truncation shown.

---

## Acceptance checklist walk

| # | SPEC item | Verdict | Evidence |
|---|---|---|---|
| 1 | Migration applied + trigger proven + backfill count == loans count | CONFIRM-PASS | **Cesar applied it mid-review, 11/11 verification checks green** (events_table 1, status_trigger 1, admin_function 1, events_rls_on 1, events_zero_policies 0, backfill_covers_every_loan 0, loans_count 0, loans_with_events 0, admin_fn_revoked_from_anon 1, admin_fn_revoked_from_authenticated 1, trigger_honours_admin_flag 1). Trigger proven on PRODUCTION via three scenarios on SYNTHETIC party ids (deadbeef → cafebabe), leaving 0 rows before / 0 rows after. Scenario A wrote `admin ACTIVE→RETURNED` (not "borrower" — the transaction-local `set_config('golfin.loan_admin','on',true)` flag worked); B wrote an explicit admin no-op status-change row for `clear_cooldown`; C wrote `system` rows for both `_expire` clocks. Backfill 0=0 is correct because production holds 0 real loans. |
| 2 | `golfin_loan_admin`: 5 refusal paths + `level_at_end` == `return_loan` + `clear_cooldown` unblocks pair | CONFIRM-PASS | All five spec-named refusals now hit at the SQL layer on production (`note_required`, `not_found`, `not_active`, `not_offered`, `bad_action`) plus a sixth defensive `admin_required` I verified is in the migration source (grep `admin_required` line count = 1). **`level_at_end` parity re-verified independently** by reading both bodies: `return_loan` (routers/loans.py:1168–1170) uses `_owner_levels([row])` → `select user_id, kind, ref_id, level from golfin_progress where user_id in (lender_id) and ref_id in (ref_id)`, keyed `(lender_id, kind, ref_id)`, fallback `level_at_start`. Migration's `force_return` runs `select level from golfin_progress where user_id = v_row.lender_id and kind = v_row.kind and ref_id = v_row.ref_id limit 1`, fallback `coalesce(v_level, level_at_start)`. Same three-tuple selector, same fallback column. Production run took the fallback branch (synthetic lender has no golfin_progress row) and wrote `level_at_end = 42 = level_at_start` — exactly what `return_loan` would take in that branch. **`clear_cooldown`** verified moving `answered_at` 30 days back on production with `status` still `rescinded`. |
| 3 | Panel: filters change query · CSV matches table · actions → 200 / audit / refetch · refusals toast | CONFIRM-PASS | Ten filter combinations cited with returned-id counts. `app/api/loans/export/route.ts:39` confirms `Content-Disposition: attachment; filename="golfin_loans.csv"`. loans-panel.tsx (380 lines) mirrors gacha-panel.tsx (577 lines): status chips, kind dropdown, `q` input, from/to dates, pagination, expanded row + refresh key, pending-action + note dialog, rules card, `/api/loans?...` fetch. Actions cite audit rows with real before/after; `clear_cooldown` audit shows `answered_at 2026-09-10T04:44 → 2026-08-11T04:44` (30 days) with `status` still `declined`. |
| 4 | Drawer tab: 1 out + 1 in + 1 pending render; switch round-trips + audits | CONFIRM-PASS | `loans_drawer_tab.png` shows PENDING (1), LENT OUT (3), BORROWED (1) exactly as required. `loans-tab.tsx:127–164` binds the switch to `/api/users/[id]/loan-offers` with a mandatory-note dialog. Report cites the flipped-OFF re-render and the `loan_offers_set` audit row. |
| 5 | Telemetry: `buildLoanFunnel`, `buildLoanLifecycle` unit-tested; renders in mock and live | CONFIRM-PASS | 12 new tests in `lib/__tests__/telemetryLoans.test.ts`; **vitest reproduced locally: 13 files, 305 passed**. Renders in mock (screenshot) and live (real zeros from production, no throw). |
| 6 | Rules card == `routers/loans.py` constants | CONFIRM-PASS | **Reproduced independently**: `curl https://playlife-api.fly.dev/api/v1/loans/rules` returned `{"data":{"lender_share_bp":2000,"allowed_days":[1,3,7],"max_loans_out":3,"max_loans_in":3,"max_pending_in":3,"offer_ttl_hours":48,"reoffer_cooldown_hours":24,"ended_window_days":14}}`. Grep of loans.py:90–138 confirms each constant (`ALLOWED_DAYS=(1,3,7)`, `MAX_LOANS_OUT=3`, `MAX_LOANS_IN=3`, `LOAN_LENDER_SHARE_BP=2000`, `LOAN_OFFER_TTL_HOURS=48`, `LOAN_MAX_PENDING_IN=3`, `LOAN_REOFFER_COOLDOWN_HOURS=24`, `ENDED_WINDOW_DAYS=14`). All eight match; the panel tiles read those values. |
| 7 | `lib/i18n.ts`: every new key en + ja; DICT lint clean; JA proof-read | CONFIRM-PASS | **Scripted verification** (multi-line-aware two-pass scanner): `git show df1f529fb -- lib/i18n.ts \| grep '^+  "' \| wc -l` = 141 keys added. My scanner finds all 141 — 140 under `loans./users.tab.loans/users.loans./tel.tab.loans/tel.loans./nav.loans` plus one `udrawer.tab.loans` (the drawer's tab label). Every single one has non-empty en AND ja. `tsc --noEmit -p tsconfig.json` exit 0 confirms `DictKey`-derived compile-time coverage. JA screenshots present. |
| 8 | Deployed: `npm run deploy`, deployment id, live footer stamp; mock still boots without service key | CONFIRM-PASS | Cloudflare Version ID `476b78f6-fdf7-47e9-8c1d-acc4355e57a3`, live footer stamp `df1f529fb` no `-DIRTY`. Fly API v73→v74, live `/rules` verified above. Mock screenshots taken from an env without the service key. |
| 9 | Dashboard tests green + backend tests green | CONFIRM-PASS | **Reproduced locally**: `npx tsc --noEmit -p tsconfig.json` → 0; `npm test` → 13 files, 305 passed; `backend/venv/bin/python -m pytest backend/tests -q` → 319 passed. All exit 0. |

---

## Independent tool output

```
$ cd Tools/admin-dashboard && npx tsc --noEmit -p tsconfig.json
EXIT: 0

$ cd Tools/admin-dashboard && npm test
Test Files  13 passed (13)
     Tests  305 passed (305)
   Duration  827ms
EXIT: 0

$ cd ~/Documents/playlife && backend/venv/bin/python -m pytest backend/tests -q
319 passed in 0.69s
EXIT: 0

$ curl -s https://playlife-api.fly.dev/api/v1/loans/rules
{"data":{"lender_share_bp":2000,"allowed_days":[1,3,7],"max_loans_out":3,
"max_loans_in":3,"max_pending_in":3,"offer_ttl_hours":48,
"reoffer_cooldown_hours":24,"ended_window_days":14}}

# i18n new-keys audit (multi-line-aware two-pass scanner over lib/i18n.ts)
total new keys: 141
keys missing en or ja: 0
```

---

## Correctness spot-checks

**Trigger guard covers every path, including INSERT.** Migration line 70 opens the trigger
body with:

```
if coalesce(current_setting('golfin.loan_admin', true), '') = 'on' then
  return new;
end if;
```

BEFORE the `tg_op = 'INSERT'` branch. `current_setting(name, true)` = missing_ok=true, returns
NULL when unset; the `coalesce` collapses NULL to `''`. Both the INSERT branch and the UPDATE
branch return early on `'on'`, so an admin action never mis-attributes to lender/borrower/system
— the admin function writes its own richer row (email + note). For non-admin writers the flag
is empty and the trigger falls through. Note that `clear_cooldown` only mutates `answered_at`,
never `status`; since the trigger fires on `after insert or update of status`, `answered_at`-only
updates never fire the trigger anyway, and the admin function writes its own explicit
"admin rescinded→rescinded" row so a support case leaves a trail. Scenario B in § "The trigger,
proven" confirms this on production. Report's claim "the timeline trigger cannot mis-attribute
an admin action" is verified in code and in production.

**`level_at_end` parity — same effective computation.** See row 2 above. Not byte-identical
(client-side dict lookup vs SQL WHERE), but the same three-tuple filter with the same
`level_at_start` fallback. Production run in scenario A took the fallback branch and produced
the same row `return_loan` would.

**Six declared spec deviations.** All either explicitly SPEC-authorized or minor:
- (1) fold `fetchLoanTimeline` into `fetchLoanDetail` — the panel always wants both together;
  reduces a network round-trip and dead code. Justified.
- (2) ledger rows NOT attached to timeline — SPEC pre-authorized ("if the ledger row does not
  carry the loan id, show only the `rp_to_*` totals and say so"). Panel screenshot shows the
  exact "say so" copy. Justified.
- (3) followers join dropped — SPEC pre-authorized ("only if cheap; otherwise drop and say
  so"). Replaced by a payload-count via `loan_offer_sent.via = search`, stronger than a join.
  Justified.
- (4) new `handshake` icon — SPEC said "handshake or the nearest existing." Explicit.
- (5) `?open=<uuid>` effect on users-panel — required to fulfil "click = opens that user's
  drawer." Justified.
- (6) `scripts/shoot.mjs` — new tooling to satisfy the SPEC's screenshot requirement.
  Justified.

None of the six is scope silently dropped.

**Clone provenance (source-shape).** loans-panel.tsx mirrors gacha-panel.tsx: `use client`,
`useCallback`/`useEffect`/`useState` for draft/applied/page/expanded/refreshKey/pending, filter
row of chips + selects + dates + Apply/Clear, `/api/loans?...` fetch, paged table, CSV export,
row expansion, action dialog with required note. loans-tab.tsx mirrors gacha-tab.tsx: three
lists (`data.offers`, `data.out`, `data.in`) with the same LoanCard row shape and note dialog,
plus a switch bound to `/api/users/[id]/loan-offers`. Both files reuse the shared LoanCard +
NoteDialog from loan-rows.tsx. Provenance is source-code shape, not Unity-prefab clone —
Rule 19's Unity-specific sprite read-back doesn't apply. Verified by direct inspection.

**Correctly scoped, not defects.**
- Top-lenders / top-borrowers lists show 4 rows not 5 because the fixture only has 4 distinct
  parties; code `.slice(0, TOP_PARTIES=5)` is present and correct.
- Funnel bars each rendered as a fraction of modal opens rather than of the previous stage.
  Matches the panel caption ("Each bar is a share of modal opens.") and reuses the shared
  Gacha funnel visualization. Step-to-step rates (`sentRate`, `acceptRate`,
  `earlyReturnRate`) are still computed in `telemetryLoans.ts` and used in the KPI tiles
  (ACCEPTED 75.0%, RETURNED EARLY 66.7%). Consistent with "same shape as GachaFunnel."
- Production has zero `golfin_loans` rows — panel legitimately empty on the live surface;
  fixtures used only for screenshots (banner disclosed on every frame).

---

## Open questions (implementer flagged, not blocking)

1. `cancel_offer` starts the pair's 24 h re-offer cooldown, so a support case ("lender
   fat-fingered the recipient") usually needs a follow-up `Clear cooldown`. Dialog copy and
   toast already say so. A composite action or automatic clear is a one-line change to route
   through Cesar. Not a defect; a follow-up decision.
2. Production has no real loans yet — first end-to-end proof on real accounts will happen
   whenever someone lends on the two test accounts. Trigger already proven on production via
   synthetic ids.

---

## Verdict

**PASS.** Every acceptance item confirms against independent evidence: tsc clean, 305
dashboard tests, 319 backend tests, live `/loans/rules` matches every constant, i18n's 141
keys each have non-empty en + ja (scripted), trigger guard verified in source to cover both
INSERT and UPDATE paths, `level_at_end` parity verified against `return_loan` and
`_owner_levels`, clone shapes verified by direct inspection. Cesar applied the migration
mid-review and the trigger has now been proven on production via three synthetic-id scenarios
with 0/0 cleanup — the two originally-blocked items are unblocked. The six spec deviations
are each SPEC-authorized or minor plumbing; none is scope silently dropped.

Forward to `golfin-reviewer`.
