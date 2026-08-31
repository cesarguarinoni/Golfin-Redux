DONE_PENDING_CESAR_APPROVAL

Built and PROVEN ON PROD 2026-08-31 (Claude Code, direct implementation — no subagent
pipeline: this task touches zero Unity assets, so there is no screenshot, no Figma node
and no scene to review).

Both migrations APPLIED by Cesar and verified: file 1 16/16 rows as expected, file 2 11/11.

All three deploy surfaces are live:
  API       playlife-api:deployment-01M1B5F2YV1ZJT84RX7RSGN5WW (v64)
  Dashboard version bbfdb132-ed74-4507-9f4b-ee7bb2b99536, stamped 83564c011
  Access    https://admin.golfin.world/ -> 302 cloudflareaccess.com

SPEC §7 roll parity: PASS. banner_test_a (no pity, no guarantee — every slot comparable
on both sides) 2 000 x10 = 20 000 slots; worst |SQL - published| 0.63 pt, worst |SQL - TS
simulate| 0.90 pt against a ±1.50 tolerance. banner_standard_club1 2 000 x10: worst
|non-forced - published| 0.36 pt; pity 9.40 %/pull vs the simulator's 9.35 %. Throwaway
user and all 4 000+ of its rows deleted, 0 orphans.

SPEC §8 live E2E: PASS, all eight steps through the real API with a real bearer token,
plus the §5.2 shop ticket sale. Pasted in IMPLEMENTER_REPORT.md Part 2.

EVERY SPEC §10 ACCEPTANCE ITEM IS NOW PASS. Acceptance #6 (`pool_for_build`) was closed
with the throwaway-rows-in-content_rows pattern Cesar pointed at (my own shop probe had
already shown it): build 2000 and the boundary 9998 both refuse `pool_for_build / Supreme`,
9999 pays the Supreme club. 1.0 s window, all rows deleted.

THE §8 PROD FOOTPRINT IS REVERTED (Cesar asked). cesar.guarinoni@wonderwall-g.com is back
to activity 823 / gift 0 / total 823, avatar level 3 / xp 633, 102 points_transactions and
its 4 pre-existing pending grants. All five gacha tables are globally empty again. Verified
three ways that do not depend on the write: the surviving ledger SUMS to 823, REPLAYS to
(3, 633), and the surviving grant ids are identical to the pre-test set. Full backup kept.

AWAITING CESAR: approval only. Nothing is outstanding.
