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

FIRST REAL PURCHASE COMPLETED 2026-08-27 by Cesar on build 2350 — `shop_char_mike`,
150 RP. Verified in the database afterwards, not taken from the device:

  golfin_shop_purchases  charged_rp 150, list_rp 150, on_sale false, build 2350,
                         key 024246bc-7939-4025-9a63-d17e6b291609
  golfin_pending_grants  character/char_mike x1, note "shop:shop_char_mike", by "shop"
  points_transactions    -150 activity, description "shop:shop_char_mike", SAME key
  profiles               golfin_inventory characters now [char_olivia, char_james, char_mike]

All three rows carry the IDENTICAL created_at, 2026-08-27T07:58:32.828409Z — to the
microsecond. That is the one-function-one-transaction guarantee (§2, step 10) visible
in the data: there was no instant at which the RP was gone and the grant did not exist.
The grant's applied_at is 148 ms later, so the client drained the queue on the spot.

STILL OPEN:
  1. The HAPPY PATH is what 2350 proved. Of the §6 *(device)* list, "price is the
     server's" is now evidenced (charged == the published price, computed server-side,
     with the build recorded). NOT yet exercised: sale window on the server clock,
     delivery-survives-death (kill the app between debit and apply), idempotent replay,
     the kill switch, and already-owned — the last of which is now trivially reachable,
     since char_mike is owned and a second BUY must refuse WITHOUT debiting.

§2.6 (closing the legacy /points/spend shop_purchase reason) is deliberately NOT shipped.
Separate commit, on Cesar's word only, once testers are on the build carrying §3.
