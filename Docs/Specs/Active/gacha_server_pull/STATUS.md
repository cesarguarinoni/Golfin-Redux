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

ONE ITEM SHORT OF FULL, FLAGGED NOT HIDDEN: acceptance #6 (`pool_for_build`) is
implemented and reviewed but NOT exercised live — proving it needs a pool entry published
to prod at min_build 9999, and publishing one to prove a refusal was not worth the blast
radius. Every sibling refusal on that code path is covered.

AWAITING CESAR: (a) approval, and (b) a decision on the §8 footprint left on
cesar.guarinoni@wonderwall-g.com — RP 823 -> 22 663, 49 945 tickets, 117 unapplied gacha
grants. Not reverted: adjusting a live RP balance is Cesar's call. The revert is ready.
