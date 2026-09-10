ARCHITECT_REVIEW_PASS

loans_ops iter-3, 2026-09-10 18:15 JST.

Red-team gate: genuinely tried the six named attacks + a Rule-5 re-run and could not break it.
- Q1 repro reproduced exactly 4 red on the old default arm, restored green.
- Q2 four-file move dropped nothing: re-exports intact, imports rewired, tsc 0, 324 tests.
- Q3 ALL_LOAN_STATUSES load-bearing: an 8th status fails 3 table tests.
- Q4 (sharpest) the stuck-state premise is FALSE per routers/loans.py: _is_locked gates offered
  on the clock, so a lapsed offer is already unlocked; withholding cancel_offer is correct and
  prevents a spurious 24h cooldown the router deliberately refuses. No operator stranded.
- Q5 clockLabel default is reachable only for an unknown status; no known status hides a clock.
Backend 319 green (untouched), live /rules matches, prod tables 0/0 (read-only, no writes),
deployed 8f823d7cb == reviewed code.
