# ARCHITECT_REVIEW — `gacha_server_pull`

**Verdict: PASS**, with one probe still owed before spec C is kicked off (§ "Condition").
Architect via Cowork, 2026-08-31. Reviewed commits `43f9f5d76` → `d2fee8a84` (GolfinRedux) and
`01241fc` (playlife) against the SPEC, checking the repo and the live systems — not the report.

## Verified in the repos

- playlife `2026_09_01_golfin_gacha.sql`: five tables as §2 (plus `pity_forced` /
  `guarantee_forced` flags on the pull row); `golfin_ticket_credit` is a single
  `on conflict … do update … returning balance` (the upsert race the report describes is
  genuinely closed); EXECUTE revoked on all five functions; pity forces on
  `(v_counter + 1) >= v_thr` (:1127) and resets on `v_rank >= v_pity_rank` (:1163) — SPEC §3
  verbatim; the x10 guarantee re-rolls the LAST slot after the ten are rolled (:1167) — SPEC §3
  verbatim; `pool_for_build` (:1069) names the offending rarity; `cost_changed` (:983) and
  `pool_for_build` both return BEFORE the ticket debit (:1106), so nothing is written on either.
- `routers/gacha.py`: `/pull`, `/history`, `/tickets`, all `Depends(get_current_user)`;
  mounted at `/api/v1/gacha` in `main.py`.
- Dashboard: `gacha` panel registered; `app/api/gacha/{enabled,export,odds,pulls,users/[id]/{pity,tickets}}`;
  `inventory/route.ts` redirects `kind = ticket` to `golfin_ticket_credit` with the integer-ref
  guard; `lib/gachaOdds.ts` pity corrected to `counter + 1 >= threshold` (:259) with the
  reasoning comment.

## Verified live (Cesar's Chrome session)

- Sidebar footer stamp reads **`83564c011`** = the dashboard commit the report names.
- `/gacha`: LIVE badge on `content_settings.gacha_enabled` + "Pause the gacha"; stats cards
  (293 pulls, 21 060 tickets, 21 940 dupe RP — matching the §8 footprint); pull log with
  email/banner/date filters, `Show prizes`, Export CSV. The log shows the real E2E rows.

## Deviations — accepted, one flagged

1. **Pity off-by-one fix in spec A's `simulate()`** — correct; the server implements §3 and the
   reference had to follow. Good catch, well tested.
2. Five functions instead of three (two roll internals) — right call; verification names them.
3. `quantity` honoured for `ticket` only + G3-Q — right; extending it would change live listings.
4. `grant_id` nullable — unavoidable and handled.
5. `golfin_ref_owned` swapped into the shop — accepted (the 08-27 comment already claimed it).
6. `/history` + `/tickets` degrade on a missing table, `/pull` does not — the right reading.
7. Odds audit drops the pull's highest slot once per flag — conservative, documented. Fine.
8. Dashboard deploy held until the migration landed — that is `ADMIN_DASHBOARD_OPS` §3.2
   applied correctly, not a deviation.
9. **`simulate()` guarantee semantics differ from §3** — the report explains it honestly: TS
   decides from the first nine slots and forces slot 9; the server rolls slot 9 and re-rolls
   only if all ten missed. Prize law identical (the conditional-distribution argument is right);
   the FLAG rate differs (≈13.4 % vs ≈10.7 %), so the admin's "guarantee hits" number does not
   match what the server will log. Not blocking — folded into spec D as a two-line fix so
   `simulate()` follows §3 literally.

## Condition — before spec C is kicked off

**Acceptance #6 (`pool_for_build`) is implemented but was never exercised against Postgres.**
The report's reason (blast radius of publishing a 9999-min_build entry) does not hold: the
shop-ticket probe in the same report shows the safe technique — insert a throwaway banner +
pool + rate rows straight into `content_rows` (a second `poolId`, never touched by a real
banner), pull against it with `build = 2000` and `build = 9999`, assert
`not_available / pool_for_build / rarity = Supreme` then `ok`, delete the rows. Ten minutes,
zero effect on real banners, and it is the server half of the invariant spec C's client
withhold rule leans on ("two locks, neither trusts the other"). Run it together with the §8
revert below and paste the two responses into the report.

## For Cesar — two decisions

1. **The §8 footprint on `cesar.guarinoni@wonderwall-g.com`**: RP 823 → 22 663, 49 945 tickets,
   293 pulls, **117 unapplied gacha grants that WILL land in your inventory at next launch**
   (6 clubs, 29 balls, 26 items among them). Recommendation: **revert**, through the ledger so
   the correction is itself auditable — Code has the script ready. Leaving it means your test
   account's RP and bag stop reflecting anything real.
2. Approve B → `Completed/`; then C (`gacha_client_real_pull`) is unblocked once the
   `pool_for_build` probe above is pasted.

## Notes (no action)

- Report §"Things worth knowing": `ShopTransaction.ApplyPurchaseGrant` has no `ticket` case —
  confirms why G1-T must stay a hard error until `TICKET_SHOP_BUILD` is set after the C archive
  (spec D §5). Spec C adds `GeneralShopCard.BindTicket`; the `ApplyPurchaseGrant` ticket case
  (credit nothing locally — the ledger is the truth — then `RefreshFromServer`) is added to C's
  §4.4 as a one-liner.
- `STATUS.md` used `AWAITING_MIGRATION` mid-task — fine; better than a misleading state.
- The free-banner (cost 0) pity-counter race is documented; acceptable for now, revisit if a
  free banner ever ships.
