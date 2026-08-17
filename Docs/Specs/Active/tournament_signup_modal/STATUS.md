READY_FOR_ARCHITECT_REVIEW

Iteration 1. Sections 1–6 built and shipping-ready; the banner half is stubbed behind
`TryResolveModalBanner` per SPEC §7 (do not block on `game_banners` §9).

The three previously FAIL-blocked items are all CLOSED (2026-08-17):

- Em-dash colour — **accepted as-is by Cesar** ("Date M dash is ok too"); no change needed.
- Migration applied to prod and `list_golfin`'s `.select(...)` extended + deployed; the
  three description columns are live on all six tournaments.
- Banner assignment round-trip — delivered by the follow-on `tournament_banners` task.

Also corrected: an earlier hand-off summary claimed the Japanese RULES body overflowed its
180px box at 199.7px. That number was never measured and is wrong — the JA rules fit.

Everything else PASSes: 128/128 EditMode, UI fidelity lint 0 FAIL, four 1170×2532 captures
through the real `SIGN UP` onClick path, both layout states measured at exactly 1411 / 1167.
