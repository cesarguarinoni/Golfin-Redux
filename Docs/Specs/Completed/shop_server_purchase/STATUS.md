DONE

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

THE -1 "UNLIMITED" SWALLOW IS CLOSED, 2026-08-27. `-1` in a quantity map is a
SENTINEL, not a quantity (the default Golfin ball ships that way), and every add
path leaves it alone — right for a reward, catastrophic for a sale: the debit
happened, the add no-opped, and `InventoryGrants.Apply` had already acked and
written `appliedGrantIds`, so the player paid and received nothing with the grant
marked delivered. Reachable, too: balls have no uniqueness check anywhere and the
shop lists `shop_ball_putt_ace`. Never hit — no ball has ever been bought.

Closed at BOTH locks, neither relying on the other:
  server  2026_08_29_shop_purchase_unlimited_refusal.sql — step 8b refuses
          ball/item held at a negative quantity BEFORE the debit, returned as
          `already_owned` + `reason: "unlimited"` (a NEW status would fall through
          the client's exact-string verdict mapping). APPLIED to prod 2026-08-27,
          all 11 verification rows exact — including the three that prove the new
          refusal is in the deployed body AND that the 08-27 `ref_inactive` and
          08-28 `ref_min_build` refusals survived the replace. No deploy rode with
          it (no API source changed; v55 was already correct). §2.5 smoke re-run
          green on all eight probes.
  client  ShopTransaction.HoldsUnlimited returns AlreadyOwned, and
          GeneralShopCard.WireBuy renders the disabled OWNED chip instead of BUY.
          This also covers the flag-OFF local path, where GrantBall would no-op
          with no server involved at all.

STILL OPEN:
  1. The HAPPY PATH is what 2350 proved. Of the §6 *(device)* list, "price is the
     server's" is now evidenced (charged == the published price, computed server-side,
     with the build recorded). NOT yet exercised: sale window on the server clock,
     delivery-survives-death (kill the app between debit and apply), idempotent replay,
     the kill switch, and already-owned — the last of which is now trivially reachable,
     since char_mike is owned and a second BUY must refuse WITHOUT debiting.

§2.6 SHIPPED 2026-08-27 on Cesar's word — the cutover is complete. POST /points/spend
now refuses reason "shop_purchase" with 400 "shop purchases go through /shop/purchase",
BEFORE the rpc, so nothing is written and no idempotency key is burned. Compared
case-insensitively against the stripped reason.

Why it was safe to close at that moment, measured rather than assumed: build 2350 (the
first client that calls /shop/purchase) was on TestFlight, and the ledger's ENTIRE spend
history was 128 rows — 125 mode_entry_fee, 1 character_level_up, 1 admin test, 1
shop:shop_char_mike. ZERO shop_purchase debits, ever. The legacy door had never
successfully sold anything to anyone, so closing it could not break a flow that had ever
run. What it DOES change: a pre-2350 client that taps BUY now fails cleanly instead of
self-granting at its bundled price.

Backend: playlife 357ce7f, deployed playlife-api v54 -> v55 (image
deployment-01M1159SB99179ZMWNJD038X9A, confirmed via flyctl status and live probes, never
the deploy exit code). /health /notices /banners /tournaments/golfin /content all 200;
/shop/purchase and /points/spend both 403 unauth. Tests: a NEW 8-test suite, because
/points/spend had none at all — tripwire-proven (disabling the refusal fails exactly the
4 refusal tests). Full backend suite 63 passed. NOTE: no interpreter on Cesar's Mac had
fastapi, so the backend suite had never actually been runnable there; backend/venv (already
gitignored) now carries the test deps.
