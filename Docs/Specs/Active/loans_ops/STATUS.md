ARCHITECT_REVIEW_FAIL

loans_ops iter-2, 2026-09-10 18:00 JST. Red-team gate.

Blocker: a FOURTH defect of the known shape (a status the code forgot to
special-case). `clockLine()` in app/(panels)/loans/loan-rows.tsx:86-108 drops
`rescinded` and `offer_expired` into its `default` arm and labels both
"answered {rel}" — but nobody answered either: offer_expired is a lapsed 48h
TTL, rescinded is the lender withdrawing. Self-verified with a verbatim-code
repro against fixtures 0007/0006: renders "answered 2d ago" / "answered 4h ago".
The ops panel exists to answer "why did that offer disappear?"; for a lapsed
offer it tells the operator the recipient answered. telemetryLoans.ts:157-162
(the iter-2 F1 fix) already documents that these two are NOT answers — clockLine
contradicts its sibling file. Green 19-test suite never exercises the clock label.

Fix the whole shape in one pass (§22): give rescinded + offer_expired their own
clock labels (EN+JA), keep default for declined, add an enumerate-all-7 clock-
label test. Two non-blocking warts (W1 stale-row actions, N1 mock/live q-uuid)
noted in ARCHITECT_REVIEW.md.

Full shape-audit table (every classify-by-status/clock site, incl. the fine
ones) and break-attempts in ARCHITECT_REVIEW.md § RED-TEAM REVIEW.
