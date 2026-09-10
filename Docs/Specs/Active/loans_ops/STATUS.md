ARCHITECT_REVIEW_FAIL

loans_ops iter-1, architect review FAIL 2026-09-10 17:35 JST.

F1: buildLoanLifecycle's medianHoursToAnswer counts `rescinded` rows whose
answered_at is the lender's rescind timestamp (SQL migration line 178,
routers/loans.py:1076,1102), not the borrower's answer. Under a label reading
"MEDIAN TIME TO ANSWER" the stat is measuring time-to-rescind for those rows.
Fix by inclusion list (active, returned, expired, declined) + unit test.

F2 open for Cesar: drawer IN list scoped to accepted-only excludes declined /
rescinded / offer_expired, so a "why did that offer disappear?" support case
is not answerable from the drawer alone.

F7 optional cleanup: LoanFunnel.acceptRate / sentRate / earlyReturnRate are
computed but never rendered — wire or drop; rename to prevent denominator
collision reviving.

The other five acceptance items PASS.

Next: golfin-implementer to fix F1 (blocking) + answer F2 to Cesar.
