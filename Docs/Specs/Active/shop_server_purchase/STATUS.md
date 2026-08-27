READY_FOR_SELF_REVIEW

Filed 2026-08-27 (Architect via Cowork). Kickoff in Docs/TellCode.md.
Both repos implemented directly by Claude Code (no subagent chain — Cesar asked for a
direct implementation).

BACKEND IS LIVE. Migration 2026_08_27_golfin_shop_purchase.sql APPLIED to prod by Cesar
2026-08-27 (all 11 verification rows as expected, bound-parser matrix exact — including
zoneless_utc reading as UTC, which is what proves `set timezone = 'UTC'` took).
playlife-api deployed v53 -> v54, image deployment-01M10JFR1RDHHXV72FERYJNKT0, confirmed
via `flyctl status` and not the deploy exit code. §2.5 smoke all green; the live
/openapi.json shows the body as {entry_id, idempotency_key, build, expected_rp_cost} with
NO user_id, so the deployed contract matches ShopPurchaseService.BuildPurchaseJson.

UI VERIFIED 2026-08-27 via the real entry path (boot -> Home -> live ShopPlusButton ->
STORE -> each chip's own onClick), captured at 1170x2532 through the sanctioned menu item.
Character and item cards render on the GeneralShopCard_Club hierarchy; bars follow
RarityStatCaps (24.0/28.0/33.3/27.3% vs caps 25/25/18/22); the six-chip row measures
1074.0px / 179.0px per chip / widest label 149.1px, no overflow; every chip filters.
Screenshots: screenshots/store_all_character_item.png + store_characters_filtered.png.

STILL OPEN:
  1. NO PURCHASE HAS BEEN COMPLETED. The dev save holds 3 RP so every BUY hits the
     affordability pre-check, and the flag-ON server branch cannot run in the Editor.
  2. Every §6 item marked *(device)* — price-is-the-server's, sale window on the server
     clock, delivery-survives-death, idempotent replay, kill switch, already-owned — needs
     a real client against the now-live endpoint.

§2.6 (closing the legacy /points/spend shop_purchase reason) is deliberately NOT shipped.
Separate commit, on Cesar's word only, once testers are on the build carrying §3.
