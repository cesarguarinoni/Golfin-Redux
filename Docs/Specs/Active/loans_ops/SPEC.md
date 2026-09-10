# SPEC — `loans_ops` (loan telemetry + admin Loans panel)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY` (2026-09-10). **Sequenced AFTER `asset_loans_offers` closes** — it reads the offer statuses that task introduces. Do not start until `asset_loans_offers` is in `Docs/Specs/Completed/`.

## Decisions of record (Cesar, 2026-09-10)

1. **"Add lending to telemetry so we know how things are flowing."** A Loans section in the Telemetry panel: the UI funnel from the client events that already ship, and the lifecycle numbers from the server's own rows.
2. **"Be able to manage loans from the admin to help players with issues."** A Loans ops panel + a Loans tab in the Users drawer, with support actions that write the audit log.

Takes up Notion deferral **2213** (admin Loans panel — the ops half; `LOAN_LENDER_SHARE_BP` tuning stays deferred, see Out of scope).

Architect defaults — flag in the report if any bites: admin actions are limited to the five in §3.3; every action requires a note (≥ 3 chars); the per-loan history lives in a new `golfin_loan_events` table fed by a trigger, so nothing in `routers/loans.py` changes.

## Goal

When a player writes in ("my club is stuck on loan", "I never got the offer", "he took my character and disappeared"), Cesar opens the Users drawer, sees every loan that player is part of with its full timeline, and fixes it in one click — force-return, cancel a pending offer, clear a cooldown, or switch the player's offers off/on. And the Telemetry panel shows whether lending is being used at all: modal opens → offers sent → accepted / declined / expired → returned early / ran to term, plus RP split totals — so the 20 % / 1-3-7 days / 3-out limits can be judged on numbers.

## Reference

No Figma. Dashboard panels are not Figma-designed (the Gacha ops panel and the Users drawer tabs were built from their specs and `ADMIN_DASHBOARD_OPS.md` §3); this task clones those two shapes exactly: the Loans panel = the Gacha ops panel's layout (`app/(panels)/gacha/gacha-panel.tsx` — filters, table, CSV export, detail expand), the Loans tab = `app/(panels)/users/gacha-tab.tsx`. Every string bilingual via `lib/i18n.ts` `DICT` (§3.4 of the ops doc). **Polish atoms: none — this is the web dashboard, not the game; no game UI, no game strings.**

## Architecture context

- **Repo:** `Tools/admin-dashboard/` (Next.js on Cloudflare Workers, `ADMIN_DASHBOARD_OPS.md` is the operating manual — read §2, §3.0–3.4, §4 before touching it). Deploy = `npm run deploy` + the quoted Cloudflare deployment id + the footer stamp check (PIPELINE_HARDENING §23).
- **Existing code to clone from:** `app/(panels)/gacha/gacha-panel.tsx` (577 lines — filter row, paged table, CSV, pause switch), `app/api/gacha/pulls/route.ts` (read-only GET, no audit), `app/api/gacha/enabled/route.ts` (POST → `checkAdmin` → `lib/gachaMutations.setGachaEnabled` → `writeAudit`), `lib/gachaMutations.ts` (`creditTickets`, `resetPity` — the mutation + audit pattern), `lib/audit.ts` `writeAudit(adminEmail, action, targetUser, tableName, before, after)`, `app/(panels)/users/gacha-tab.tsx` + `user-drawer.tsx` (`type Tab = …` at ~28, `useState<Tab>` ~150), `app/(panels)/telemetry/telemetry-panel.tsx` (sections; the gacha funnel at ~611), `lib/telemetryGacha.ts` (`GachaEventRow`, `buildGachaFunnel` — pure, unit-tested in `lib/__tests__/telemetryGacha.test.ts`), `lib/telemetryData.ts` (`fetchTelemetrySummary` ~482, `fetchTelemetryEvents` ~671), `app/api/telemetry/summary/route.ts`, `lib/registry.ts` (panel entries ~92–98), `lib/mockGacha.ts` (mock mode — every new data function needs a mock twin, §4.5 fails closed).
- **Client events already shipped** (`RecordSafe`, no client change in this task): `loan_modal_open {kind, ref_id}`, `loan_offer_sent {kind, ref_id, days, via}`, `loan_pill_open`, `loan_offer_answered {loan_id, answer}`, `loan_offer_rescinded {loan_id}`, `loan_return {loan_id, early}`, `loan_offers_setting {on}` — in `telemetry_events` (`name`, `props` JSON, `user_id`, `ts`).
- **Server truth:** `golfin_loans` (status ∈ offered/active/returned/expired/declined/rescinded/offer_expired; `offered_at`, `offer_expires_at`, `answered_at`, `starts_at`, `ends_at`, `ended_at`, `level_at_start`, `level_at_end`, `rp_to_lender`, `rp_to_borrower`, `lender_share_bp`, `days`), `profiles.golfin_loan_offers`, `golfin_progress_events.on_behalf_of`, ledger rows `type = 'loan_lender_share'`. Lazy expiry happens on read in `routers/loans.py::_expire` — the dashboard must not assume a cron flipped anything.

## 1. Server (playlife)

### 1.1 Migration `backend/migrations/2026_09_10_golfin_loan_events.sql` (paste in chat; apply before the dashboard deploy)

```sql
create table if not exists public.golfin_loan_events (
  id          bigint generated always as identity primary key,
  loan_id     uuid not null,
  at          timestamptz not null default now(),
  from_status text,
  to_status   text not null,
  actor       text not null check (actor in ('lender','borrower','system','admin')),
  admin_email text,
  note        text
);
create index if not exists golfin_loan_events_loan_idx on public.golfin_loan_events (loan_id, at);
alter table public.golfin_loan_events enable row level security;   -- no policies: service role only

-- every status change on golfin_loans writes a row, whoever caused it (router, lazy expiry, admin).
create or replace function public.golfin_loans_log_status() returns trigger
language plpgsql security definer set search_path = public as $$
begin
  if tg_op = 'INSERT' then
    insert into golfin_loan_events (loan_id, from_status, to_status, actor)
      values (new.id, null, new.status, 'lender');
  elsif new.status is distinct from old.status then
    insert into golfin_loan_events (loan_id, from_status, to_status, actor)
      values (new.id, old.status, new.status,
              case new.status
                when 'active'        then 'borrower'   -- accept
                when 'declined'      then 'borrower'
                when 'returned'      then 'borrower'
                when 'rescinded'     then 'lender'
                when 'expired'       then 'system'
                when 'offer_expired' then 'system'
                else 'system' end);
  end if;
  return new;
end $$;
drop trigger if exists golfin_loans_status_trg on public.golfin_loans;
create trigger golfin_loans_status_trg
  after insert or update of status on public.golfin_loans
  for each row execute function public.golfin_loans_log_status();

-- the admin's five actions, one function, one transaction, always a note
create or replace function public.golfin_loan_admin(
  p_loan_id uuid, p_action text, p_admin text, p_note text)
returns json language plpgsql security definer set search_path = public as $$
declare v_row golfin_loans%rowtype; v_level int;
begin
  if p_note is null or length(trim(p_note)) < 3 then
    return json_build_object('status','note_required'); end if;
  select * into v_row from golfin_loans where id = p_loan_id for update;
  if not found then return json_build_object('status','not_found'); end if;

  if p_action = 'force_return' then
    if v_row.status <> 'active' then return json_build_object('status','not_active'); end if;
    select level into v_level from golfin_progress
      where user_id = v_row.lender_id and kind = v_row.kind and ref_id = v_row.ref_id;
    update golfin_loans set status='returned', ended_at=now(),
      level_at_end = coalesce(v_level, level_at_start) where id = p_loan_id;
  elsif p_action = 'cancel_offer' then
    if v_row.status <> 'offered' then return json_build_object('status','not_offered'); end if;
    update golfin_loans set status='rescinded', answered_at=now() where id = p_loan_id;
  elsif p_action = 'clear_cooldown' then
    -- the cooldown is derived from answered_at on rescinded/declined rows of this pair:
    -- push it out of the window without losing the history
    update golfin_loans set answered_at = answered_at - interval '30 days'
      where lender_id = v_row.lender_id and borrower_id = v_row.borrower_id
        and status in ('rescinded','declined');
  else
    return json_build_object('status','bad_action');
  end if;

  insert into golfin_loan_events (loan_id, from_status, to_status, actor, admin_email, note)
    values (p_loan_id, v_row.status,
            (select status from golfin_loans where id = p_loan_id), 'admin', p_admin, p_note);
  return json_build_object('status','ok');
end $$;
revoke execute on function public.golfin_loan_admin(uuid,text,text,text) from public, anon, authenticated;
```

Plus a backfill: one `golfin_loan_events` row per existing `golfin_loans` row (`to_status = current status, actor = 'system', note = 'backfill'`) so the timeline is never empty. Verification block at the bottom (table, trigger, function, RLS on / 0 policies, backfill count = loans count). NOTE for the implementer: read `routers/loans.py::return_loan` for how `level_at_end` is derived today and mirror it exactly in `force_return` — the SQL above is the shape, not the final text.

The two remaining admin actions (**offers on/off** for a player, and **force-expire an offer**) are plain updates: `profiles.golfin_loan_offers` (dashboard writes it directly, like every other `profiles` mutation), and `cancel_offer` above covers the offer case. `routers/loans.py` is **untouched**.

## 2. Telemetry — the Loans section

`lib/telemetryLoans.ts` (pure, tested like `telemetryGacha.ts`):

- `buildLoanFunnel(events: LoanEventRow[])` from `telemetry_events` names: `loan_modal_open` → `loan_offer_sent` → answered (`loan_offer_answered` split accept/decline) → `loan_return` (early). Rates: sent/open, accept/sent, early-return/accepted. Same shape as `GachaFunnel` so the panel's funnel component renders it.
- `buildLoanLifecycle(loans: LoanRow[])` from `golfin_loans` in the range (by `offered_at`, falling back to `starts_at`/`created_at` for pre-offers rows): counts per terminal status, **pending now** (offered, unexpired), **active now**, median hours-to-answer, days mix (1/3/7), kind mix, RP split totals (`rp_to_lender`, `rp_to_borrower`), top lenders / borrowers (5 each, display_name), share of offers that went to a non-followed player (join `followers` — NOTE: only if cheap; otherwise drop and say so).
- `fetchTelemetrySummary` gains `loans: { funnel, lifecycle }`; `summary/route.ts` passes it through; mock twin in `lib/mockTelemetry.ts`.
- `telemetry-panel.tsx`: new tab `{ id: "loans", key: "tel.tab.loans" }` + `<Section id="loans">` — funnel strip (reuse the funnel component), a KPI row (pending / active / accepted % / early-return % / RP to lenders), a status breakdown table, days + kind mix, top-5 lists. All strings `tel.loans.*` en + ja.

## 3. Admin — Loans panel + Users drawer tab

### 3.1 Data (`lib/loansData.ts`, mock twin `lib/mockLoans.ts`)

`fetchLoans({ status?, q?, kind?, from?, to?, page })` → rows from `golfin_loans` joined to `profiles` for both display names, paged 50, newest first; `q` matches lender/borrower display_name (ilike) or a loan id or a `ref_id`. `fetchLoanTimeline(loanId)` → `golfin_loan_events` + the ledger rows `type = 'loan_lender_share'` referencing this loan (NOTE: the split's ledger description carries the borrower name — match on `uuid5(key, loan_id)`? Read `golfin_loan_split` in `2026_09_09_golfin_loans.sql`; if the ledger row does not carry the loan id, show only the `rp_to_*` totals and say so). `fetchUserLoans(userId)` → out / in / offers for the drawer.

### 3.2 API routes (all `checkAdmin`)

- `GET /api/loans` (list, filters), `GET /api/loans/[id]` (row + timeline), `GET /api/loans/export` (CSV of the current filter, like `gacha/export`).
- `POST /api/loans/[id]/actions` body `{ action: 'force_return' | 'cancel_offer' | 'clear_cooldown', note }` → `lib/loanMutations.ts` → `supabaseAdmin.rpc('golfin_loan_admin', …)` → on `ok` `writeAudit(email, 'loan_' + action, lenderId, 'golfin_loans', before, after)`; refusals map to 409 with the status string.
- `POST /api/users/[id]/loan-offers` body `{ enabled }` → update `profiles.golfin_loan_offers` → `writeAudit(email, 'loan_offers_set', userId, 'profiles', {enabled: before}, {enabled: after})`.
- `GET /api/users/[id]/loans` for the drawer tab.

### 3.3 Loans panel (`app/(panels)/loans/`, registry entry right after `gacha`, icon `handshake` or the nearest existing)

Header: title + subtitle (`loans.subtitle`: "server truth · lazy expiry — an offer past its clock shows as offered until somebody reads it" — because that is what the table will show). Filter row: status chips (All / Offered / Active / Returned / Expired / Declined / Rescinded / Offer expired), kind (character / club), free-text `q`, date range. Table columns: when (offered_at), status pill, kind + `ref_id`, lender → borrower (display names, click = opens that user's drawer), days, level start → end, RP to lender / to borrower, ends/expires (relative). Row expand: the timeline (`golfin_loan_events`, actor + admin email + note) and the **action bar**: `Force return` (active only), `Cancel offer` (offered only), `Clear cooldown` (rescinded/declined only), each opening the same confirm dialog with a required note textarea → POST → toast → row refetch. CSV export button. Rules card (read-only): the five constants as they are in `routers/loans.py` (share bp, 1/3/7, 3 out, 3 in, 3 pending, 48 h TTL, 24 h cooldown) — displayed from a small `GET /api/loans/rules` that reads them from the backend `GET /api/v1/loans/rules` (add that read-only endpoint to `routers/loans.py` — the one router touch, 10 lines) so the card can never drift from the code.

### 3.4 Users drawer — Loans tab

`type Tab` gains `"loans"`; `loans-tab.tsx` cloned from `gacha-tab.tsx`: three lists (Out / In / Pending offers) with the same row shape and the same action bar per row; plus a **Loan offers** switch at the top bound to `profiles.golfin_loan_offers` (POST above) with the audit note dialog. Empty states per list.

### 3.5 Strings

Every new UI string in `lib/i18n.ts` `DICT` with `en` + `ja`: `nav.loans`, `loans.*` (≈45 keys — title, subtitle, filters, columns, statuses, actions, dialog, toasts, rules card), `users.tab.loans`, `users.loans.*`, `tel.tab.loans`, `tel.loans.*`. Zero hardcoded literals (the dashboard lint that already exists for DICT coverage must pass — quote it).

### 3.6 Audit

Every mutation lands in `admin_audit_log` with `before`/`after` (`writeAudit`); the Audit panel shows them with no change.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Migration applied by Cesar (verification output quoted); trigger proven: one offer → accept → return on prod (or the two test accounts) yields three `golfin_loan_events` rows with the right actors; lazy expiry of an offer yields a `system` row; backfill count = loans count.
- [ ] `golfin_loan_admin`: `note_required`, `not_found`, `not_active`, `not_offered`, `bad_action` each hit by a test; `force_return` sets `level_at_end` exactly as `return_loan` does (quote both); `clear_cooldown` makes the pair lendable again (router test through the real `cooldown` path).
- [ ] Panel: every filter changes the query (network tab or mock assertions); CSV matches the table; each action → 200 → audit row (SQL quoted) → table refetch; refusals show as a toast with the status.
- [ ] Drawer tab: a user with 1 out + 1 in + 1 pending shows all three; the offers switch round-trips and audits.
- [ ] Telemetry: `buildLoanFunnel` + `buildLoanLifecycle` unit-tested (empty, one full lifecycle, mixed statuses, pre-offers rows with null `offered_at`); the section renders in mock mode and live.
- [ ] Rules card values == `routers/loans.py` constants (endpoint response quoted).
- [ ] `lib/i18n.ts`: every new key en + ja; DICT lint clean; JA proof-read (report lists the keys).
- [ ] Deployed: `npm run deploy` output, Cloudflare deployment id, footer stamp read from the live site (PIPELINE_HARDENING §23); mock mode still boots with no service key (§4.5).
- [ ] Dashboard tests green (count before/after); backend tests green.

## Files / hierarchy this task touches

- playlife: `backend/migrations/2026_09_10_golfin_loan_events.sql` — NEW; `backend/routers/loans.py` — `GET /rules` only; `backend/tests/test_loans.py` — admin fn + rules
- dashboard: `app/(panels)/loans/{page,loans-panel}.tsx` — NEW; `app/(panels)/users/loans-tab.tsx` — NEW, `user-drawer.tsx`; `app/api/loans/{route,[id]/route,[id]/actions/route,export/route,rules/route}.ts` — NEW; `app/api/users/[id]/{loans,loan-offers}/route.ts` — NEW; `app/api/telemetry/summary/route.ts`; `lib/{loansData,loanMutations,mockLoans,telemetryLoans}.ts` — NEW; `lib/{telemetryData,mockTelemetry,registry,i18n}.ts`; `lib/__tests__/telemetryLoans.test.ts` — NEW; `app/(panels)/telemetry/telemetry-panel.tsx`
- `Docs/ADMIN_DASHBOARD_OPS.md` — Loans panel paragraph under §3.0; `Docs/AI_CONTEXT.md`

## Smoke evidence

Screenshots: Loans panel with each status filter, one expanded row with a timeline containing an admin action, the drawer tab for a two-account user, the Telemetry Loans section live. SQL: the audit rows for one of each action. The deploy id + footer stamp.

## Out of scope (do NOT do these) — Notion deferrals

- Tuning `LOAN_LENDER_SHARE_BP` / limits / TTL / cooldown from the admin (rules stay code constants; the card is read-only).
- Admin-created loans ("lend on behalf of a player"), editing `level_at_end` by hand, RP corrections (use the existing RP grant/adjust on the Users drawer).
- Push/notice to the player when an admin acts on their loan.
- A per-player "block" list (2235 stands).
