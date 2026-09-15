# SPEC — `iap_plumbing`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. `SPEC_READY` (Architect, 2026-09-15 07:37 JST).

## Goal

Stand up the real-money purchase pipeline end to end and prove it **in sandbox/TestFlight only**, using one
consumable test SKU (`test.tickets.x10` — 10 Golden Tickets): Unity IAP (StoreKit 2) purchase → playlife
server receipt verification → server-side grant through the existing pending-grants queue → ticket ledger
credit → visible in Store History. Alongside it, the STORE tab learns **dual pricing**: a catalog row may
carry an RP price, a money price, or both — with the card treatments and the payment-choice modal designed
on the Figma Store page. **Nothing goes live**: `iap_enabled` stays OFF in production, the test SKU is
sandbox-only, and the MONETIZATION_PLAN §0 no-P2W rules stand unchanged for anything real players can reach
(plan §1.3, revised 2026-09-14 — the test SKU is the one sanctioned, temporary exception to "tickets never
have a product id", and it never ships).

## Build playbook (Figma-node screens)

The card-price and modal work builds from Figma nodes — implementer and EVERY reviewer work
`Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md`. Its §7 self-diff is an acceptance line. Run
`get_design_context` on the nodes below at step 0 (Lesson AK — the token table here is convenience,
the node is truth).

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd`, page **Store**
- **Screen frame:** `Store Screen — IAP Pricing (RP / ¥ / both)` — id `14287:32861`
- **Modal frame:** `Store — Purchase Modal (dual price)` — id `14289:33223`
- **Reference PNGs dropped to `reference/`:** `iap_pricing_screen.png` (14287:32861),
  `purchase_modal.png` (14289:33223)
- **Placeholder vs canonical:** ticket art, copy ("ALLOWS TO PULL…"), and all prices/amounts are
  placeholders; ¥ values on live cards come from StoreKit's localized price string, never hardcoded.
  The Winter Sale banner, weekly-lineup chrome and DRIVER G&F card are context from the weekly demo,
  not part of this task.

### Fidelity table (reconcile against nodes, do not trust blindly)

| Element | Figma node | Figma value | Unity |
|---|---|---|---|
| RP price row (coin + number) | `14292:33345` / `14292:33346` / `14292:33347` | 36 px RP coin + number, 8 px gap, centered in plate; **icon always, never the word "RP"** (Cesar 2026-09-14) | `PriceBox` row: RP coin sprite (same asset as the Top UI RP counter) + TMP number; extend `GeneralShopCard.BindPrice` |
| ¥ price plate | `14287:32967` (single), `14287:33017` (dual, bottom) | white plate, navy price text (#001E39) | localized `productMetadata.localizedPriceString`; plate = existing PriceBox visual |
| Dual-price card | card 3 of `14287:32861` | RP row plate on top, ¥ plate below, in the one PriceBox column | both plates in `PriceBox`; **discount = corner `-N%` badge ONLY** (`14287:33138`) — no struck-through originals on dual rows (no room; Cesar 2026-09-14). Single-priced rows keep today's struck-orig treatment |
| Payment modal plate | `14289:33227` | navy plate 1086×912 r50 over 50 % black scrim, OPAQUE | `ModalController` dialog; opaque plate rule (screen_hints) |
| Modal item row | `14293:33358` (art `14293:33359`, desc `14293:33344`) | item art 158×173 + description, 32 px gap | bind card's tile sprite + its description string |
| Modal option buttons | `14293:33346` (RP; coin+number overlay row `14297:33344`), `14293:33351` (¥) | stacked vertically, 24 px gap, **both gold**; RP option = coin + number, ¥ option = localized price | `Main Buttons` gold 450×120; coin composed INSIDE the button layout (the Figma coin is an overlay approximation) |
| Modal cancel | `14289:33814` | silver, below the options | standard silver button; row re-centres if an option is absent — a button with nothing to do is REMOVED, never disabled |
| Choose-payment line | `14293:33345` | centered, above options | `STORE_CHOOSE_PAYMENT` key |
| Figma trap | — | `Main Buttons` hug their label; instances here were relabelled with `Button Container = FILL` | Unity buttons are 450×120 regardless |

## Architecture context

- **Polish atoms** (mandatory, name-checked in review): `PendingSpend.BeginOn(buyButton, …)` around the
  ENTIRE purchase round-trip (StoreKit sheet → server verify → grant ack) — never a hand-rolled spinner;
  `ModalController` for the payment-choice modal (Pop + backdrop Fade come free);
  `Golfin.UI.Polish.ButtonPressFeedback` on every new button (file `Assets/Scripts/UI/ButtonPressFeedback.cs`);
  `UiMotion.Pop/Fade` for plate/price state changes, `Stop` in `OnDisable`; no shimmer anywhere here
  (server verify shows PendingSpend, not ShimmerHost).
- Client shop stack: `Assets/Scripts/UI/Shop/` — `GeneralShopScreenController` (BUY → 
  `ShopTransaction.TryPurchaseCatalogEntry`, `GeneralPurchaseResult` switch), `GeneralShopCard`
  (`BindPrice` / `WireBuy` / `HidePriceAndBuy`, `PriceBox/Orig`, `PriceBox/SaleBG/Sale/Num`,
  `PriceNavy = #001E39`), `GeneralShopModel` (`ShopCatalogEntry`, `GeneralShopCatalog.GetByCategory`,
  category parser that DROPS unknown categories — keep that posture).
- Tickets are a server ledger (`gacha_server_pull`): `GachaTicketManager` reads `/gacha/tickets`; there is
  NO client debit/credit path — the grant lands server-side and the client refreshes.
- Grant queue: reuse the exact path an admin ticket grant takes (`golfin_pending_grants` → the client's
  existing drain). NOTE: implementer locates the admin ticket-grant function in the playlife repo and
  calls the same one — do not write a second grant path.
- playlife `backend/routers/iap.py` is the PARTNER app's: `/iap/verify-purchase` credits `activity_pts`
  directly (= `total_points` = RP). **The Golfin arm must never touch that code path** — it is the T2
  money→RP chain (plan §5.6). Add a separate endpoint; shared helpers (`_verify_apple_receipt`) are fine.
- `ScreenId` is PINNED (`Logo = 0 … StoreHistory = 34`); if a new screen id is ever needed here (it should
  not be — the modal is a modal), the next free number is 35 and `ScreenIdSerializationTests` must pass.
- Unity IAP is NOT in `Packages/manifest.json` yet — add `com.unity.purchasing` (pin the current LTS
  version; NOTE: flag the exact version in the report).

## Server work (playlife repo — `/Users/cesar/Documents/playlife`)

1. Migration `2026_09_15_golfin_iap.sql` (additive):
   - `iap_purchases`: add `app text default 'partner'`, `granted_ref text`, `granted_qty int`.
   - `iap_products`: add `app text default 'partner'`, `kind text`, `grant_ref text`, `grant_qty int`.
   - Seed one row: `(product_id 'test.tickets.x10', platform 'apple', app 'golfin', kind 'ticket',
     grant_ref 'gold', grant_qty 10, pts_amount 0, price_jpy <lowest tier>, is_active true)`.
2. `POST /api/v1/iap/golfin/verify` (new, in `iap.py`, auth required): request = platform, product_id,
   transaction_id, receipt/JWS. Validates with Apple (reuse `_verify_apple_receipt`; sandbox 21007/21008
   flip already handled). Rules: product must have `app='golfin'` AND `pts_amount = 0` (hard 400 otherwise —
   the partner pts arm is unreachable from this endpoint by construction); idempotent by
   `(transaction_id, platform)` exactly as the partner arm; on valid → ONE transaction: `iap_purchases`
   row (`app='golfin'`, status `granted`) + ticket grant through the SAME function the admin ticket grant
   uses. Never a pts write, never a client-trusted grant.
3. Kill switch: the endpoint refuses (409) when `content_settings.iap_enabled` is not true. Set the flag
   TRUE only in the environment/window Cesar is testing in; default false.
4. Tests (`test_iap_golfin_*`): happy path grants exactly once; replayed transaction_id returns
   already_processed with no second grant; a partner product id on the golfin endpoint is 400; a
   `pts_amount > 0` row is 400; flag off is 409; unauthenticated 403. Deploy proof: `flyctl status` + live
   403-not-404 smoke on the new route.

## Client work (GolfinRedux repo)

1. `IapService` (new, `Assets/Scripts/Services/IapService.cs`; NOTE: match the namespace convention of the
   neighbouring services — flag if unclear): initializes Unity IAP with the golfin product list from the
   shop catalog (`storeProductId` column, §Data), exposes `Purchase(productId, onResult)`, and on
   `ProcessPurchase` sends the receipt to `/iap/golfin/verify`, returning `PurchaseProcessingResult.Pending`
   until the server answers 200 — **the transaction is confirmed only after the server grants** (relaunch
   replays it, verify is idempotent, nothing is lost). Gate the whole service on `iap_enabled` from content
   settings: flag off ⇒ no IAP init, no ¥ plates anywhere.
2. `GeneralShopCard.BindPrice` extension: rows with `RpCost` only — unchanged (plus the coin icon per the
   fidelity table if the current PriceBox lacks it; reconcile against the node). Rows with `storeProductId`
   only — ¥ plate with the localized price. Rows with both — stacked plates per card 3. Discount handling
   per the fidelity table. Withhold rule: `storeProductId` set but IAP unavailable (flag off, store init
   failed, product missing) ⇒ render as RP-only when `RpCost` exists, hide the row entirely when it is
   ¥-only — a BUY that cannot work is not shown.
3. BUY routing in `GeneralShopScreenController`: RP-only → today's `TryPurchaseCatalogEntry` path,
   untouched. ¥-only → `IapService.Purchase` under `PendingSpend`. Both → payment-choice modal
   (`ModalController`, nodes above); RP option → RP path, ¥ option → IAP path, CANCEL closes. If exactly
   one payment method is currently available on a dual row, skip the modal and go straight to it (a
   one-option chooser is a button row with a button that has nothing to choose).
4. Store History: a money purchase appears exactly as its granted item (`SOURCE: STORE`); the record comes
   from the server row, not a client write. Verify the existing `StoreHistoryScreenController` path picks
   it up; extend the record mapping only if the new source string requires it.

## Data / admin

- `shop_catalog` gains additive column `storeProductId` (empty everywhere except the test row). Test row:
  `category = ticket`, `grant gold ×10`, `RpCost 450` (so the row exercises BOTH prices), `storeProductId
  test.tickets.x10`, **`min_build` = the test build number** so live clients never see it even if published.
- ⚠️ **Publish hazard (AI_CONTEXT 2026-09-14):** `shop_catalog` currently has ~78 unpublished draft rows —
  a catalog publish ships every pending draft. Do NOT publish `shop_catalog` for this task without Cesar
  reviewing the pending set first; staging the row as a draft and testing against a draft-preview env is
  acceptable for the sandbox run.
- Validator rule (dashboard, `rotation.ts`/catalog validation home): a row whose ref is stat-carrying
  (club/ball/character/item-with-stats, or ticket rows other than `test.tickets.x10`) must have
  `storeProductId` empty — blocking. Money never sits next to a stat item (plan §1.3).

## Strings (two-way importer → admin publish; acceptance line)

All via `Assets/Localization/LocalizationText.csv`, EN + JA in the same commit →
`import_content.py --catalogs texts` PLAN → apply → publish `texts` from the admin → `export_content.py
--check` clean. No hardcoded `.text` literals (grep quoted in report). Keys added (none retired):

| Key | EN | JA |
|---|---|---|
| `STORE_CHOOSE_PAYMENT` | CHOOSE HOW TO PAY. | 支払い方法を選択してください。 |
| `STORE_IAP_FAILED` | PURCHASE FAILED | 購入に失敗しました |
| `STORE_IAP_UNAVAILABLE` | STORE UNAVAILABLE | ストアを利用できません |
| `STORE_IAP_PROCESSING` | PROCESSING… | 処理中… |

Modal title/description/prices are bound from the row (name key, description key, prices), not new keys.
NOTE: the modal CANCEL — reuse the existing generic cancel key if one exists; add `GENERIC_CANCEL`
(EN CANCEL / JA キャンセル) only if none does (flag which in the report).

## Architect defaults (Cesar did not decide these — report flags them)

- Test SKU id `test.tickets.x10`, reference name "TEST Golden Tickets x10", lowest JPY tier. ASC product
  ids are unreusable after deletion, so the `test.` prefix keeps the real namespace clean.
- Dual-price BUY opens the payment-choice modal (vs two tappable price plates on the card).
- Test row carries `RpCost 450` purely to exercise the dual path in one row.
- `iap_enabled` refusal code 409.

## Out of scope (filed as deferrals in Notion GOLFIN_Roadmap)

- Android / Play Billing arm of the golfin verify path.
- `noads.premium` SKU (blocked on MONETIZATION_PLAN §2.5 decision with Ken).
- Full `iap_products` two-way catalog + admin Entitlements tab + refund webhooks (ASSN v2) — the real
  Phase 1c, after the beta reset.
- Restore-purchases UI (App Store review requirement the day a non-consumable ships; the consumable test
  doesn't need it).
- Retiring the test SKU (window off + ASC remove-from-sale) after validation — Cesar's call when done.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Backend: `test_iap_golfin_*` pass; deployed (`flyctl status` pasted); live smoke: new route
  403-not-404 unauthenticated; partner `/iap/verify-purchase` behavior byte-identical (its tests untouched
  and green); SQL proof quoted that the golfin flow wrote NO `points_transactions` row.
- [ ] Sandbox E2E on device (TestFlight/dev build, sandbox Apple ID): buy `test.tickets.x10` → StoreKit
  sandbox sheet → PendingSpend on the button the whole round trip → ticket balance +10 from
  `/gacha/tickets` (no client credit) → purchase visible in Store History → SQL: one `iap_purchases`
  row `app='golfin'`, one grant row, ledger row for tickets. Screenshot each step.
- [ ] Idempotency: replay the same transaction (relaunch before Confirm) → no second grant (SQL count
  quoted).
- [ ] `iap_enabled` false → no ¥ plates anywhere, dual test row renders RP-only, IAP never initializes
  (log quoted); endpoint 409.
- [ ] Store screen at 1170×2532 A/B vs `14287:32861` (playbook §7 self-diff): RP prices render coin+number
  (never the word "RP" — check every price site touched), ¥ plate localized, dual card stacks both with
  corner-badge-only discount. Screenshots.
- [ ] Payment modal A/B vs `14289:33223`: opaque navy plate over scrim, item art + description, two gold
  stacked options (coin+number / localized ¥), silver CANCEL; ModalController pop/fade; ButtonPressFeedback
  on all three; single-available-method dual row skips the modal (video).
- [ ] Strings: importer round trip `--check` clean for `texts`; all four keys EN+JA live in admin; zero new
  hardcoded `.text` literals (grep output quoted).
- [ ] EditMode suites green, incl. `ScreenIdSerializationTests` and shop tests
  (`GeneralShopCategoryTests`, `GeneralShopAdmitResolutionTests`).
- [ ] No `shop_catalog` publish happened without Cesar's explicit OK on the pending-draft review (state
  which path was used).
