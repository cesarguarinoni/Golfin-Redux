DONE

Approved by Cesar 2026-09-01. Spec D of `Docs/GACHA_ADMIN_PLAN.md` §8 — the last of the four.

All of §2, §3, §4, §4b, §4c, §4d, §4e and §5 are built, verified and pushed
(c0dfbaab1 · 832992d5c · e1996ccc9 · bb2a95bad · 19f0c8c2b · 87ad42357 · 9e242302b ·
d6db2d4c7 · 4c9954069 · b42812bdd · 6c9af340b · 2afaf0ad5 · 8c2c34d1e).

Dashboard deployed twice: 87ad42357 (a71683bd-8328-46c8-a7b7-906cda179cbf) and
d6db2d4c7 (14a538c6-d14a-435f-8a69-94a4a410f963). TestFlight builds 2534 and 2537.
Catalogs published: texts v20, ticket_types v2, balls v6, gacha_banners v7, shop_catalog v6.
`export_content.py --check` clean. Full EditMode sweep 2146/0. Dashboard vitest 233.
`2026_09_02_default_ball_guard.sql` applied by Cesar.

── ONE OPEN THREAD, deliberately left ──────────────────────────────────────────

`shop_ticket_standard_50` (50 Standard tickets / 100 RP / minBuild 2536) is published
but **`is_active = false`**. It is off sale because TestFlight build 2537 was archived
BEFORE the four ticket-path fixes in 2afaf0ad5, and on 2537 the card has no price, no
BUY, and the purchase is refused as an unknown ball. `min_build` is immutable so the
floor could not be raised; `is_active` is the reversible lever.

TO FINISH: on the next build that carries 2afaf0ad5, set the row active in the admin
Shop panel (or flip `is_active` and publish `shop_catalog`). No code remains.

The listing itself is already proven end to end — the live purchase on 2537's
predecessor wrote `golfin_shop_purchases` amount 50 / charged_rp 100, moved
`golfin_tickets` 2840 → 2890, and created no pending grant row.

See IMPLEMENTER_REPORT.md § 11 for the closing audit, including what was NOT verified.
