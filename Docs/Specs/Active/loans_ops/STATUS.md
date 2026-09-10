READY_FOR_REDTEAM

loans_ops iter-3, 2026-09-10.

Redo after the red-team FAIL. Its blocker was real: clockLine had a `default` arm reading
"answered {rel}", so a lender rescind and a 48h lapse both rendered as the recipient answering,
on the panel whose job is explaining why an offer disappeared.

Fixed the SHAPE, not the instance. That was the FOURTH defect of one shape in this task -- a
status classified by an incomplete list -- so per PIPELINE_HARDENING section 22 every judgement
about a loan status or its clocks now lives in one pure module, lib/loanStatus.ts, whose header
names all four. ALL_LOAN_STATUSES is exported and the tests ITERATE it, so a status added later
fails the suite instead of landing in a default arm. Restoring the old default turns four tests
red, including a cross-check that ANSWERED_STATUSES and clockLabel cannot drift apart.

Also fixed from the same pass: red-team wart W1. actionsFor took a bare status, so a lapsed
offerered row still showed Cancel offer -- and the server guards on the status column, so that
action would have SUCCEEDED and started a 24h cooldown on a pair whose offer had already died.
It now reads the clock.

Dashboard 312 -> 324 tests (14 files), backend 319, tsc exit 0.
Deployed: Cloudflare version 70a0c4f4-f20f-4b6e-a31e-5aa6769b55f6; GET /api/version on the live
site answers {"commit":"8f823d7cb","stamped":true}.
