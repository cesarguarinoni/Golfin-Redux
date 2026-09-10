READY_FOR_ARCHITECT_REVIEW

loans_ops iter-2, 2026-09-10.

Redo after ARCHITECT_REVIEW_FAIL. All three findings addressed:

- F1 (blocking) medianHoursToAnswer counted `rescinded` rows, whose answered_at is the LENDER
  withdrawing rather than the recipient answering. Guard changed from an exclusion list to an
  inclusion list (ANSWERED_STATUSES). The old test had PINNED the bug; corrected, and two
  regressions added that were proven to fail against the old code before the fix was kept.
- F2 (design) Cesar decided 2026-09-10: widen the drawer as its OWN section. BORROWED stays
  accepted-only; a fourth section "Offers that went nowhere" carries declined / rescinded /
  offer_expired.
- F7 (cleanup) the funnel rates were renamed (sentRateOfOpens / acceptRateOfSent /
  earlyReturnRateOfAccepted) so the collision cannot revive, AND wired onto the screen, since
  SPEC section 2 asks for exactly those three.

Migration is applied (11/11) and the trigger was proven on production during iter-1.
Dashboard 305 -> 307 tests, backend 319, tsc exit 0.
Deployed: Cloudflare version 586b6c80-dfde-47a2-a938-19d1b12fdb7a, live footer stamp da3175337.
