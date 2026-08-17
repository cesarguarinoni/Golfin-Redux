READY_FOR_ARCHITECT_REVIEW

Iteration 1. Sections 1–6 built and shipping-ready; the banner half is stubbed behind
`TryResolveModalBanner` per SPEC §7 (do not block on `game_banners` §9).

Three items are FAIL-blocked on things outside this repo or this task, so this goes to
architect review rather than self-review (hook rule 1):

- Em-dash colour `#C7D6EB` — needs a §5.2 amendment to touch the combined `_dateLineText` string.
- Migration not applied (Cesar's Supabase step) and the one-line `playlife/backend`
  `list_golfin` `.select(...)` change — that repo is not checked out here.
- Banner assignment round-trip — `game_banners` §9 has not landed.

Everything else PASSes: 128/128 EditMode, UI fidelity lint 0 FAIL, four 1170×2532 captures
through the real `SIGN UP` onClick path, both layout states measured at exactly 1411 / 1167.
