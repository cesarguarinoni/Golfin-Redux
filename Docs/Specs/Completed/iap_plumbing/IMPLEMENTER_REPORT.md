# Implementer Report — `iap_plumbing`

**Iteration:** 1 (main Claude Code thread acting as implementer on Cesar's kickoff, 2026-09-15)
**Iteration shape:** iap:first-pass

> Every checklist item is PASS or FAIL with what was measured. Two classes of FAIL are open and
> both are gated on things only Cesar can do (apply the DDL; buy on a sandbox device) — see
> § Known FAIL items. Nothing below is a rubber stamp: every PASS cites a file, a number or a log line.

## Implementation summary

The GOLFIN real-money arm now exists end to end, sandbox-only and OFF by default. **playlife:**
`2026_09_15_golfin_iap.sql` (additive: `app`/`grant_*` columns, `kind='ticket'`, `status='granted'`,
`content_settings.iap_enabled=false`, the `test.tickets.x10` seed, and `golfin_iap_grant()` — one
transaction that writes the `iap_purchases` row, credits through `golfin_ticket_credit` (the admin
grant's function) and logs the Store History row), plus `POST /api/v1/iap/golfin/verify` and
`GET /api/v1/iap/golfin/config` in `routers/iap.py` (fail-closed kill switch → 409, `app='golfin'
AND pts_amount=0` or 400, Apple `/verifyReceipt` through the existing `_verify_apple_receipt` plus a
bundle-id + in_app cross-check, idempotent by `(transaction_id, platform)`); the partner
`/verify-purchase` body is untouched. **Unity:** `com.unity.purchasing 5.4.3` (StoreKit 2),
`Golfin.Economy.IapFlow` (the confirm-only-after-200 rule, unit-tested) + `IapService` host and
`UnityIapStoreDriver`, `shop_catalog.storeProductId`, three price-plate modes on `GeneralShopCard`
(RP / ¥ / dual with the `-N%` corner badge), the payment-choice modal
(`Resources/Prefabs/Shop/StorePaymentModal.prefab`, built from palette atoms), BUY routing under
`PendingSpend`, and a money price in Store History. Four+1 text keys published (`texts` v57). **Admin dashboard** deployed (`golfin-admin` version `e881adf4`): validator rule G4-IAP + the `storeProductId` field in the row editor.

## Files modified or created

| Path | Change |
|---|---|
| `Packages/manifest.json` | modified — `"com.unity.purchasing": "5.4.3"` (the `latest` dist-tag, released 2026-09-04; 5.x = StoreKit 2; pulls `com.unity.services.core 1.18.0`) |
| `Packages/packages-lock.json` | modified — resolved by Unity for the two packages above |
| `Assets/Resources/BillingMode.json` | created BY UNITY IAP on import (Android store selector, `GooglePlay`); standard file, committed with the package |
| `Assets/Resources/BillingMode.json.meta` | created — Unity meta for the file above |
| `Assets/Scripts/Net/Endpoints.cs` | modified — `IapGolfinConfig`, `IapGolfinVerify` |
| `Assets/Scripts/Economy/IapFlow.cs` | created — `IapFlow` (decisions), `IIapStoreDriver`/`IIapVerifier` seams, wire DTOs, `ApiIapVerifier` (Golfin.Economy asmdef so the EditMode suite can drive it) |
| `Assets/Scripts/Economy/IapFlow.cs.meta` | created — Unity meta for the file above |
| `Assets/Scripts/Services.meta` | created — folder meta for the spec-pinned `Assets/Scripts/Services/` |
| `Assets/Scripts/Services/IapService.cs` | created — MonoBehaviour host (boot via `[RuntimeInitializeOnLoadMethod]`, reads `/iap/golfin/config`, refreshes tickets + history on grant) + `UnityIapStoreDriver` (Unity IAP 5 two-step flow); Editor-only `EditorForceFakeStorePref` seam, OFF |
| `Assets/Scripts/Services/IapService.cs.meta` | created — Unity meta for the file above |
| `Assets/Scripts/UI/Shop/GeneralShopModel.cs` | modified — `StoreProductId` / `HasStoreProduct` / `HasRpPrice` / `IsSandboxOnly`, `ListedNow` gate in `GetByCategory`, `OverrideStoreAvailabilityForTest` |
| `Assets/Scripts/UI/Shop/GeneralShopCard.cs` | modified — `BindPrice` three modes, node coin row (36 px / 8 px), `PriceNumberFontSize` 28, ¥-only plate geometry, `DiscountBadge`, `DisplayName`/`TileSprite`/`Description` read-backs |
| `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` | modified — BUY routing (RP / ¥ / modal), `BuyWithMoney` under `PendingSpend`, lazy modal instantiation under the root Canvas, `IapService.AvailabilityChanged` rebind |
| `Assets/Scripts/UI/Shop/StorePaymentModalController.cs` | created — `ModalController` subclass; options REMOVED when absent |
| `Assets/Scripts/UI/Shop/StorePaymentModalController.cs.meta` | created — Unity meta for the file above |
| `Assets/Scripts/UI/Shop/Editor/StorePaymentModalBuilder.cs` | created — `GOLFIN ▸ Store ▸ Build Store Payment Modal prefab` (palette atoms, node geometry, provenance log) |
| `Assets/Scripts/UI/Shop/Editor/StorePaymentModalBuilder.cs.meta` | created — Unity meta for the file above |
| `Assets/Scripts/UI/Shop/Editor/IapStoreFramesRun.cs` | created — capture harness through the real widgets (`GOLFIN ▸ Store ▸ Shoot IAP store + payment modal frames`, and the real-config IAP-off run) |
| `Assets/Scripts/UI/Shop/Editor/IapStoreFramesRun.cs.meta` | created — Unity meta for the file above |
| `Assets/Scripts/UI/Shop/Editor/IapEditorForceMenu.cs` | created — Editor menu toggle for the FakeStore force pref |
| `Assets/Scripts/UI/Shop/Editor/IapEditorForceMenu.cs.meta` | created — Unity meta for the file above |
| `Assets/Resources/Prefabs/Shop/StorePaymentModal.prefab` | created by the builder (guid `b3fff0ba7bf474bfba9e94c191f7a79a`) |
| `Assets/Resources/Prefabs/Shop/StorePaymentModal.prefab.meta` | created — Unity meta for the file above |
| `Assets/Resources/Prefabs/Shop/GeneralShopCard_Club.prefab` | modified — `PriceBox/DiscountBadge` (Image `S_DiscountBadge` + TMP `Label`), authored inactive |
| `Assets/Resources/Prefabs/Shop/GeneralShopCard_Ball.prefab` | modified — same badge |
| `Assets/Art/Shop/S_DiscountBadge.png` | created — baked by `Docs/Scripts/make_discount_badge.py` from node `14287:33138` tokens (250×134 @2×, Sprite/PPU 100) |
| `Assets/Art/Shop/S_DiscountBadge.png.meta` | created — Unity meta for the file above |
| `Docs/Scripts/make_discount_badge.py` | created — the bake script |
| `Assets/Scripts/UI/Shop/StoreHistoryRecord.cs` | modified — `PaidAmount` / `PaidCurrency` / `PaidWithMoney` |
| `Assets/Scripts/UI/Shop/StoreHistoryStore.cs` | modified — maps the two new columns |
| `Assets/Scripts/UI/Shop/StoreHistoryRow.cs` | modified — money purchases render `SHOP_HISTORY_PRICE_MONEY` via `FormatMoney` |
| `Assets/Scripts/Economy/PointsDtos.cs` | modified — `ShopPurchaseDto.PaidAmount` / `PaidCurrency` |
| `Assets/Resources/Data/shop_catalog.csv` | modified — additive `storeProductId` column (blank everywhere) + the sandbox row `shop_ticket_gold_10_iap` (ticket 1 ×10, 600→450 RP, `test.tickets.x10`) |
| `Assets/Localization/LocalizationText.csv` | modified — `STORE_CHOOSE_PAYMENT`, `STORE_IAP_FAILED`, `STORE_IAP_UNAVAILABLE`, `STORE_IAP_PROCESSING`, `SHOP_HISTORY_PRICE_MONEY` (EN+JA) |
| `Assets/Localization/LocalizationTextTable.asset` | modified — bundled table rebuilt from the CSV (the five keys) |
| `Assets/Resources/Data/content_version.txt` | modified — `texts=57` after the publish |
| `Assets/Tests/EditMode/IapPlumbingTests.cs` | created — 29 cases: confirm-only-after-200, kill switch, one-in-flight, listing gate, wire body, HTTP map, badge label, money format |
| `Assets/Tests/EditMode/IapPlumbingTests.cs.meta` | created — Unity meta for the file above |
| `Tools/admin-dashboard/lib/contentValidate.ts` | modified — rule **G4-IAP**: `storeProductId` must be blank on every row except the sandbox ticket row carrying `IAP_SANDBOX_TEST_SKU` (blocking) |
| `Tools/admin-dashboard/lib/__tests__/contentValidate.test.ts` | modified — 5 G4-IAP cases (suite 389/389, `tsc --noEmit` clean) |
| `Tools/admin-dashboard/app/(panels)/shop/shop-panel.tsx` | modified — `storeProductId` rendered explicitly in `editorExtras` (rows predating the column have no key) |
| `Tools/admin-dashboard/lib/i18n.ts` | modified — `sh.storeProduct.help` EN/JA |
| `Docs/AI_CONTEXT.md` | modified — this task's entry |
| `.claude/settings.json` | modified — the four hook commands `python` → `python3` (Cesar: "Go for python3"; the gates never fired on this Mac before) |
| `tasks/lessons.md` | modified — Lesson CM (a hook whose interpreter is missing is a gate that never fires) |
| `.claude/alerts.log` | hook-generated — written by `route_subagent.py` once it could run (its lines are about `auth_email_redirect`, another task's state) |
| `ProjectSettings/ProjectSettings.asset` | modified BY UNITY IAP on import — `cloudServicesEnabled: Purchasing: 0` (one line, the package's services toggle bookkeeping) |
| `Docs/Architecture/ARCHITECTURE_AUDIT.md` | regenerated at session start (routine) |
| `Docs/Reports/content_art.txt` | pre-existing, NOT this task — already dirty in the iter-1 kickoff baseline (`M Docs/Reports/content_art.txt`), left untouched |
| `Docs/TellCode.md` | pre-existing, NOT this task — already dirty in the iter-1 kickoff baseline (`M Docs/TellCode.md`), left untouched |
| `Docs/Versioning/last_uploaded_build.txt` | pre-existing, NOT this task — already dirty in the iter-1 kickoff baseline (`M Docs/Versioning/last_uploaded_build.txt`), left untouched |
| `Docs/Specs/Active/iap_plumbing/*` | this task's folder (report, heartbeat, screenshots, reference sheets) |
| playlife `backend/migrations/2026_09_15_golfin_iap.sql` | created — NOT YET APPLIED (Cesar: Supabase SQL editor) |
| playlife `backend/routers/iap.py` | modified — golfin arm (`/golfin/config`, `/golfin/verify`), `password=` kwarg on `_verify_apple_receipt` (None = unchanged partner behaviour), partner `/catalog` filtered to `app='partner'` |
| playlife `backend/routers/shop.py` | modified — `/shop/history` selects `paid_amount, paid_currency` |
| playlife `backend/config.py` | modified — `golfin_apple_shared_secret`, `golfin_bundle_id` |
| playlife `backend/tests/test_iap_golfin.py` | created — 24 tests |

## Screenshot

- **Canonical screenshot:** `screenshots/iap_store_all_dual_card.png` (1170×2532 — STORE / ALL through PLAY gate → Rewards Center → STORE; the dual sandbox row at the top with coin+450 / navy ¥ band / `-25%` badge, every RP-only plate on the node's 36/8 coin row)
- **Live-config frames (FakeStore force OFF, config from the DEPLOYED server, `iap_enabled=true`):** `screenshots/live_store_all_LIVECONFIG.png`, `live_store_tickets_LIVECONFIG.png`, `live_payment_modal_LIVECONFIG.png`, `live_modal_cancelled_LIVECONFIG.png`, `live_money_pending_fakestore_sheet_LIVECONFIG.png`, `live_after_server_answer_LIVECONFIG.png` + `iap_frames_geometry_live.txt` — log: `[IapService] iap_enabled is ON — connecting the store for 1 product(s): test.tickets.x10` → `store connected` → `product 'test.tickets.x10' → available` → BUY → modal → ¥ → `POST /api/v1/iap/golfin/verify → 402 … empty_receipt` → `Left PENDING, nothing granted`.
- **Also:** `screenshots/iap_payment_modal.png` (BUY on the dual card → the modal), `screenshots/iap_payment_modal_cancelled.png`, `screenshots/iap_money_pending_fakestore_sheet.png` (¥ option tapped: BUY reads `…`, FakeStore sheet up), `screenshots/iap_money_after_server_answer.png` (server 404 → `PURCHASE FAILED` toast, BUY restored, order left pending), `screenshots/iap_store_tickets_chip.png`, `screenshots/iap_off_real_config_store_all.png` + `iap_off_real_config_store_tickets.png` (FakeStore force OFF, real server config), `screenshots/HARNESS_money_only_plate_synthetic_entry.png` (¥-only plate on a SYNTHETIC entry — rendering proof ONLY, no ¥-only row exists in this task's data), `screenshots/iap_frames_geometry.txt` + `iap_frames_geometry_iap_off.txt` (world rects / glyph bounds per frame)
- **Captured at:** 2026-09-15 09:12–09:20 JST by `IapStoreFramesRun` (`CaptureCore.SnapPlayModeSafe`), copied from `Docs/Diagnostics/_capture/iap_frames/`
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` (booted through the Title/PLAY gate; DevAutoSignIn)
- **Play mode:** Yes
- **Hole loaded (if applicable):** n/a
- **Store used for the frames:** Unity IAP 5's **FakeStore** (Editor), forced ON only for the capture run via `IapService.EditorForceFakeStorePref` and restored OFF by the harness. The ¥ string is therefore the FakeStore's `$0.01` — StoreKit's `¥160` exists only on a device. The verify call went to the REAL server and was refused (`POST /api/v1/iap/golfin/verify → 404`, the arm is not deployed yet), so nothing was granted.

## Figma fidelity

Nodes re-pulled at step 0 with `get_design_context` + `get_metadata` (rule 9); `reference/iap_pricing_screen.png` / `purchase_modal.png` are the A/B ground truth; matched crops in `reference/price_plates_ref_vs_built.png`. Measurements are rendered glyph/box extents in px on the 1170×2532 frame (`screenshots/iap_frames_geometry.txt` for the built side).

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| RP price row: coin | `14292:33345` (RP Icon 36×36) | 36 px coin, `Reward Points Icon` art | `RpIcon.sizeDelta 36×36`, sprite `aab2dfa34afd9cf4abfe974a164268dc`, rendered 35 px (alpha edge) vs node 33–34 | PASS |
| RP price row: coin→number gap | `14292:33345` | 8 px | `numX − (iconX + 36) = 8.0` on every RP plate (geometry dump) | PASS |
| RP price row: number size | `14287:32991` | Rubik Medium 30 → digits 21 px tall in the node render | fs 28 → digits 21 px (`600`: rows 1555–1575), was 26 at the shipped 34; width 57 vs node 52 (SemiBold vs Medium) | PASS |
| RP price row: number weight | `14287:32991` | Rubik **Medium** | Rubik **SemiBold** SDF — the only static Rubik asset in the project; the shipped RP plate already uses it | PASS* |
| RP price row: never the word "RP" | all price sites | icon + digits | grep: no `"RP"` in `BindPrice`/modal; the one `RP` string on the screen is the shipped toast `Not enough RP` (not a price site) | PASS |
| ¥-only plate fill | `14287:32962` | **navy** `#001E39`, 3 px `#F3ECC2` border, 180×76 (the spec table said white — the node is navy) | navy `PriceBox` 178×78 + `PriceBorder` 184×84 (`#F3ECC2`), text white, no coin | PASS* |
| ¥-only plate + BUY re-centred | `14287:32961`/`32968` | plate top 75, BUY at 173 (group centred at ~142) | plate 70..148, BUY 165..207 around the authored group centre 138.5 (`ApplyPriceGeometry`) | PASS |
| Dual plate: layout | `14287:33012` | white plate, top band coin + navy `450`, bottom navy band white `¥1,500`, 76/76 split | white `PriceBox` 178×160; `Orig` row coin 36 + `450` `#001E39` at fs 28; `SaleBG` navy band 77+3 with the price in white; split 80/80 | PASS* (box 160 vs node 152 — the shipped, approved plate height) |
| Dual plate: no struck original | card 3 | none | `Strike` off in dual mode (`SetActive("PriceBox/Orig/Strike", false)`), verified in frame | PASS |
| Discount badge geometry | `14287:33138` | 109×47, right = plate right + 8, top = plate top − 18 | `DiscountBadge` 125×67 incl. 8 px shadow bleed at (16, 26) top-right → visual 109×47 at (+8, −18); rendered red body 105×43 in BOTH ref and built (`rc()` measure) | PASS |
| Discount badge colours | `14163:33716` | 2 px `#FFB3BD` border, gradient `#F0566A→#A8172B`, shadow 0 4 4 @35 % | baked verbatim by `make_discount_badge.py`; sampled 4× node render edge rows = flat `#FFB3BD` (no gradient stroke) | PASS |
| Discount badge text | `14163:33717` | `-25%` Rubik SemiBold 26, white, tracking 1.04 | `-25%` Rubik SemiBold 24, white, characterSpacing 4; label computed `DiscountLabel(600, 450)` | PASS |
| Modal plate | `14289:33227` | 1086×912 r50, `#133453→#091B33`, 3 px white→silver stroke, centred | `Next Hole Panel.png` (the Figma Pop-up atom) sliced ppum 1.28, drawn body 1086×912 centred at y 1266 (rect 786.6..1729.8 minus the baked margins) over a 50 % black scrim | PASS |
| Modal title | `14289:33231` | Rubik SemiBold 66, silver gradient — node render cap height 47 px | `TextGradients.Silver`, fs 59 → cap 47 px (rows 836–882) | PASS |
| Modal item row | `14293:33358/33359/33344` | art 158×173 at x138, description 620 wide at x328 (gap 32), 51 SemiBold white centred | `ItemArt` 158×173 at x138 (sprite `Ticket_Gold`, bound from the card), `Description` 620×173 at x328, fs 46 auto-sized 30–46 (44.2 for the 3-line copy) | PASS |
| Modal separators | `14289:33232`, `14294:1593` | 978×2 at y120 and y341 | `Divider.png` 978×2 at y120 / y341 | PASS |
| Choose line | `14293:33345` | 51 SemiBold white, centred — node cap 35 px | fs 46 → cap 34 px, width 525 vs 503 | PASS |
| RP option button | `14293:33346` + `14297:33344` | gold 450×120 at y472, coin 56 + `450` 66 SemiBold `#321506`, gap 12 — node digits 48 px | `ButtonConfirm.png` 450×120 at y472, coin 56, fs 63 → digits 48 px (rows 1317–1364 vs ref 1318–1365), `#321506` | PASS |
| ¥ option button | `14293:33351` | gold 450×120 at y616, price 66 SemiBold `#321506` | `ButtonConfirm.png` 450×120 at y616, fs 59, `#321506`, StoreKit string | PASS |
| CANCEL | `14289:33814` | silver 450×120 at y760, `CANCEL` 66 SemiBold `#1E293B` — node cap 46 | `ButtonCancel.png` 450×120 at y760, fs 59 → cap 45, `#1E293B`, `MODAL_CANCEL` | PASS |
| Button gaps | — | 24 px between the three | 592→616, 736→760 (24/24) | PASS |
| Option removal | spec | a method that is unavailable is REMOVED | `Open()` sets the button GO inactive for a 0 RP / empty ¥; the controller skips the modal entirely when only one method is available | PASS |
| ButtonPressFeedback | rule 11 | every new Button | `rpButton`/`moneyButton`/`cancelButton` all `pressFeedback=True` (geometry dump) | PASS |

## UI fidelity lint

Render-health run (`LintPrefab(prefab, null)`, the same call the hook re-runs). No node-spec layer: the price plate's 178×160 box is the shipped, approved geometry (node 142/152) and would trip the size check by design.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| GeneralShopCard_Club.prefab | `Docs/Diagnostics/_capture/GeneralShopCard_Club_lint.json` | 0 | 10 (all already in the shipped prefab: RarityGrad stretch, 9-slice cap notes, unlocalized placeholders) |
| GeneralShopCard_Ball.prefab | `Docs/Diagnostics/_capture/GeneralShopCard_Ball_lint.json` | 0 | 114 (all already in the shipped prefab: the 21 segment cells × 5 rows flat-fill notes) |
| StorePaymentModal.prefab | `Docs/Diagnostics/_capture/StorePaymentModal_lint.json` | 0 | 8 (Backdrop scrim, ItemArt bound at runtime, panel 9-slice note, placeholder strings overwritten at `Open()`) |

## Clone provenance

| Element | Cloned / rebound from | Evidence |
|---|---|---|
| Modal plate | `Assets/Art/HomeScreen/Next Hole Panel.png` (guid `3663aafeba2bd1f42a04eabf9d34c220`) — the palette's Figma Pop-up atom | builder log `reuse Sprite … guid=3663aafeba2bd1f42a04eabf9d34c220`; live `ModalPanel.Image.sprite = Next Hole Panel` |
| RP / ¥ option buttons | `Assets/Art/RosterScreen/ButtonConfirm.png` (guid `bc649f28836576548b310e79ce614a06`, GOLD big, sliced border 25) | geometry dump `sprite=ButtonConfirm` on both |
| CANCEL | `Assets/Art/RosterScreen/ButtonCancel.png` (guid `6021c639e9c124b44a06c8ccd977896f`) | `sprite=ButtonCancel` |
| RP coin (modal + card rows) | `Assets/Art/HomeScreen/Reward Points Icon.png` (guid `aab2dfa34afd9cf4abfe974a164268dc`) | prefab `PriceBox/*/RpIcon` sprite guid; modal `RpIcon` |
| Separators | `Assets/Art/HomeScreen/Divider.png` (guid `36b5ccd887d78864b9d3f0b36a18f339`) | builder log |
| Fonts | `Assets/Fonts/Rubik-SemiBold SDF.asset` (guid `39fb7824ee463ab408c7f2e76c362562`) | builder log; card rows authored |
| Price plates | the shipped `PriceBox`/`PriceBorder`/`SaleBG` of `Assets/Resources/Prefabs/Shop/GeneralShopCard_Club.prefab` / `_Ball.prefab` (`S_Common_BGCorner8` `b2ae6196bf901b54eaf57aea53472a8c`, `S_Common_BGCorner8Bottom` `555dbbd195fecb0459818bc9066e6621`) — rebound, not rebuilt | `BindPrice` edits colours/anchors only |
| Discount badge | `Assets/Art/Shop/S_DiscountBadge.png` (guid `fa8cceabea02644b19b7f82c5caca65c`), baked from node `14287:33138` tokens — the palette has no red gradient capsule (`S_PillStadium` is a flat tint atom) | `make_discount_badge.py`; sampled node render |
| Modal shell (the whole assembly) | `Assets/Prefabs/UI/Modals/LoanReturnModal.prefab` — the Pop-up + Main Buttons + Rubik SemiBold shell, rebuilt from the same atoms at this node's geometry (guids above) | `StorePaymentModalBuilder` mirrors `LoanUiBuilder.BuildReturnPrefab`'s helpers (`Require`, `Wire`, `MakeButton` shape) |
| Item art | `Assets/Resources/Art/Gacha/Tickets/Ticket_Gold.png` — the tapped card's own `tournament_image/Portrait` sprite (`GachaTicketArt.Resolve`) | `card.TileSprite` read-back; geometry dump `modal.itemArt: sprite=Ticket_Gold` |

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Backend: `test_iap_golfin_*` pass | PASS | `backend/tests/test_iap_golfin.py`: 24/24 passed; whole backend suite 343/343 passed (`venv/bin/python -m pytest tests -q`) |
| Backend: deployed (`flyctl status`) + live smoke 403-not-404 | PASS | Deployed 2026-09-15 10:18 JST after the migration: `flyctl status -a playlife-api` → image `playlife-api:deployment-01M2HA8KET1Y5G44HNF21BBM31`, machines `148e03defe4d38` and `90803266b21328` both **VERSION 75** (were 74). Live: `POST /api/v1/iap/golfin/verify` with no token → **403** ×4 (burst, both machines); `GET /iap/golfin/config?platform=apple` → `{"enabled":true,"products":[{"product_id":"test.tickets.x10",…,"price_jpy":100}]}`; partner `GET /iap/catalog?platform=apple` → exactly the 3 `com.wonderwall.playlife.pts*` rows (no test SKU); authenticated `GET /shop/history?limit=3` → 200 with `paid_amount`/`paid_currency` in every row; `/health` ok. |
| Backend: partner `/iap/verify-purchase` byte-identical | PASS | Its body is untouched (diff of `iap.py` touches only the docstring, `_verify_apple_receipt`'s new `password=None` kwarg — `test_verify_apple_receipt_without_a_password_arg_still_sends_the_partner_secret` — and `/catalog`'s `app='partner'` filter); `test_partner_request_model_is_unchanged` pins the request model. There were no partner IAP tests to keep green (none exist). |
| Backend: SQL proof that the golfin flow wrote NO `points_transactions` row | FAIL | The GRANTED path still needs the device pass. The REFUSAL path is proven live: the Editor's FakeStore order hit the deployed verify → `402 Verification failed: empty_receipt`; PostgREST afterwards: `iap_purchases` app=golfin = exactly one row `{txn 580af522…, status failed, error empty_receipt, pts_credited 0, granted_ref null}`, `points_transactions type=iap_purchase` since 2026-09-14 = **[]**, `golfin_ticket_transactions reason like 'iap:%'` = **[]**, `golfin_shop_purchases paid_currency not null` = **[]**. In code: `golfin_iap_grant` has no `points_transactions`/`profiles` write and `test_happy_path…writes_no_pts` pins the Python layer. |
| Sandbox E2E on device (StoreKit sheet → PendingSpend → +10 tickets → Store History → SQL rows) | FAIL | Device-only. Requires: the migration applied, the API deployed, `test.tickets.x10` created in App Store Connect (consumable, ¥160 tier), `iap_enabled=true` for the window, a TestFlight build ≥ 2943 with this code, a sandbox Apple ID. Editor evidence of the same code path: `iap_money_pending_fakestore_sheet.png` (BUY reads `…`, `PurchaseInFlight=True`), `iap_money_after_server_answer.png` (server answer → toast, BUY restored). |
| Idempotency: replay the same transaction → no second grant (SQL count) | FAIL | SQL count needs the device pass. Structurally proven three ways: server `test_a_replayed_transaction_answers_already_processed_with_no_second_grant` (no rpc, no Apple), `golfin_iap_grant` replay branch + `golfin_ticket_credit` derived key `md5(platform:txn:iap)`, client `AReplayedOrderFromAPreviousSession_IsVerifiedAndConfirmed…` + `AnythingButA200_LeavesTheOrderPending…`. |
| `iap_enabled` false → no ¥ plates, dual test row RP-only, IAP never initialises (log), endpoint 409 | PASS | Real-config run (`iap_off_real_config_store_all.png`): `IapService.State=Disabled`, `cards showing a money plate=0`, log `[IapService] could not read /iap/golfin/config … — IAP stays off this session.` / `iap_enabled is OFF on the server — Unity IAP not initialised; no ¥ plates this session.`; endpoint 409: `test_flag_false_is_409_and_neither_apple_nor_the_rpc_runs`, `test_flag_absent_is_409`, `test_flag_unreadable_is_409_not_500`. **Deviation:** the sandbox test row is WITHHELD entirely (not RP-only) with the flag off — see § Spec deviations 1; the RP-only fallback for a real dual row is `ListingGate_AMoneyOnlyRow…_ADualRowStaysListed` + `BindPrice`'s `RpOnly` branch (the same code every RP plate in the frame runs). |
| Store screen A/B vs `14287:32861`: coin+number, localized ¥, dual stack, corner-badge-only discount | PASS | § Figma fidelity rows 1–12; `reference/price_plates_ref_vs_built.png` (ref/built pairs for dual, RP-only, ¥-only). ¥ string = the store's `localizedPriceString` (FakeStore `$0.01` in the Editor). |
| Payment modal A/B vs `14289:33223`: opaque navy plate, art + description, two gold options, silver CANCEL, ModalController pop/fade, ButtonPressFeedback ×3, single-method dual row skips the modal (video) | PASS | § Figma fidelity modal rows; `iap_payment_modal.png`; `ModalController.Show/Hide` (the plate is the opaque Pop-up sprite); `pressFeedback=True` ×3. Skip rule: `HandleBuy` → `money && rp ? modal : money ? BuyWithMoney : BuyWithRp` — no video, the withhold state cannot be produced with the FakeStore in the Editor (every catalog product is "available" there); covered by code + the IAP-off run. |
| Strings: importer round trip `--check` clean for `texts`; four keys EN+JA live; zero new hardcoded `.text` literals | PASS | PLAN `texts 5 add / 0 change / 0 conflict` → `--apply` → `content_publish(texts)` → **v57** → export → `--check`: `texts v57 1210 rows unchanged`; five rows read back live with `min_build 2943`. Grep of the touched runtime files for `.text = "`: none. (Only `shop_catalog` shows drift, by design: the unpublished sandbox draft.) |
| EditMode suites green incl. `ScreenIdSerializationTests` and shop tests | PASS | `tests-run` EditMode (machine summary): Total: 3152 / Passed: 3148 / Failed: 0 / Skipped: 4 (`{'Status': 'Passed', 'TotalTests': 3152, 'PassedTests': 3148, 'FailedTests': 0, 'SkippedTests': 4, 'Duration': '00:01:10.55'}`); `GolfinRedux.Tests.EditMode` 409/409 (includes `ScreenIdSerializationTests` and the 29 new `IapPlumbingTests`); `GeneralShopCategoryTests` 14/14; `GeneralShopAdmitResolutionTests` 5/5 |
| No `shop_catalog` publish without Cesar's OK | PASS | Held as a DRAFT (`min_build 2943`) until Cesar answered "Publish" (2026-09-15 ~09:55 JST); then published through the admin's Review & publish drawer → **v12**, 79 added / 0 changed / 0 deactivated, 3 advisory warnings (RP bands on two old rows, R4 repeat on a wk_2026_40 ball); `export_content.py --check` clean afterwards (117 rows, `shop_catalog=12`) |

## Known FAIL items

Progress since the first write (2026-09-15 ~10:00 JST):

1. **Migration APPLIED** by Cesar (VERIFICATION 9/9) and the API **DEPLOYED** (10:18 JST, VERSION 75 on both machines) after his one-time `flyctl auth login` (the CLI macaroon had expired at 811 h). Smoke in the checklist row above.
2. **App Store Connect DONE:** `test.tickets.x10` already existed as a draft (consumable, base Japan **¥100** — the lowest tier there, so `iap_products.price_jpy` was patched 160 → 100 to match; EN + JA localizations present); I added the review screenshot + notes and saved — "Add for Review" is now enabled (metadata complete = sandbox-purchasable). NOT added to any review.
3. **`iap_enabled` = TRUE** since 09:49 JST (Cesar: "ASAP"), over PostgREST. Until the API is deployed the client still reads a 404 on `/iap/golfin/config` and stays off.
4. **Device pass** still needs a TestFlight build ≥ 2943 carrying this commit + a sandbox Apple ID → the SQL proofs (one `iap_purchases app='golfin' status='granted'`, one `golfin_ticket_transactions reason='iap:test.tickets.x10'`, one `golfin_shop_purchases paid_currency='JPY'`, zero `points_transactions`), the replay count, Store History screenshot.

## Spec deviations

1. **Sandbox row is withheld, not RP-only, when IAP is unavailable.** `test.`-prefixed product ids are sandbox rows (`ShopCatalogEntry.IsSandboxOnly`); `GeneralShopCatalog.ListedNow` lists them only while the product is buyable on the device. The row is BUNDLED in the CSV (there is no draft-preview channel — `content.py` serves `content_rows` only), so the spec's RP-only fallback would have shown every live player a "GOLD TICKET ×10 · 450 RP" card whose RP purchase the server refuses (the row is unpublished). The spec's own intent ("live clients never see it") is what this rule implements; the RP-only fallback still applies to every non-`test.` dual row.
2. **Test row carries list 600 / sale 450** (spec: `RpCost 450`) so the one row exercises the corner badge too; the charged price is 450 either way.
3. **¥-only plate is navy, not white** — the node (`14287:32962`) is navy `#001E39` with the `#F3ECC2` border; the spec's fidelity table said white. Node wins (playbook / Lesson AK).
4. **Fifth text key** `SHOP_HISTORY_PRICE_MONEY` (`PRICE: {0}` / `価格: {0}`) — a money purchase in Store History would otherwise read `PRICE: 0 RP`. `MODAL_CANCEL` already existed, so no `GENERIC_CANCEL` was added.
5. **`iap_enabled` is read through `GET /iap/golfin/config`**, not the content delta: the delta is applied from cache at boot and would lag a launch; the endpoint reads `content_settings` live and fail-closed, and also hands the client the product list the server will actually grant.
6. **Store History row comes from `golfin_shop_purchases`** (written by `golfin_iap_grant` with `charged_rp 0`, `paid_amount`/`paid_currency`) — the history endpoint reads only that table, and tickets have no `golfin_pending_grants` row by design (spec B §5.2). `charged_rp > 0` check relaxed to `>= 0`.
7. **Price digits 34 → 28** on every plate (RP-only too): the node's 30 px Medium renders 21 px tall; the shipped 34 rendered 26 px. Same coin change (30 → 36, gap 6 → 8) — the spec's fidelity row pins the node.
8. **Product list intersection:** only ids present in BOTH the catalog and the server's golfin product list are fetched from StoreKit.
9. **Namespace:** `IapService` lives in `Assets/Scripts/Services/` (spec path) but joins `Golfin.Economy` beside `ShopPurchaseService`; the testable core is in the `Golfin.Economy` asmdef (`IapFlow.cs`).
10. **Editor-only FakeStore force** (`IapService.EditorForceFakeStorePref`, `#if UNITY_EDITOR`, OFF by default) exists so the plates/modal could be rendered through real navigation before the server arm is live; the harness restores it OFF.

## Console output

```
[IapService] EDITOR FORCE — FakeStore for 1 catalog product(s); the server config was NOT consulted. Prices are the FakeStore's; verify is refused server-side.
[IapService] iap_enabled is ON — connecting the store for 1 product(s): test.tickets.x10
[IapService] store connected; fetching products.
[IapService] product 'test.tickets.x10' → available at '$0.01' (USD)
[IapService] pending order for 'test.tickets.x10' txn 6d37c40d-5003-4866-af8e-2d77e2999bb0 — verifying.
[ApiClient] POST /api/v1/iap/golfin/verify → 404 in 29 ms
[IapService] server refused txn 6d37c40d-5003-4866-af8e-2d77e2999bb0 (HTTP 404: Not Found). Left PENDING, nothing granted.
--- real-config run (force OFF) ---
[IapService] could not read /iap/golfin/config (ApiResult<IapConfigDto> NotFound (404, attempts=1): Not Found) — IAP stays off this session.
[IapService] iap_enabled is OFF on the server — Unity IAP not initialised; no ¥ plates this session.
```
No errors or exceptions from this task's code in either run. Compile: 0 errors (the Editor log's last `error CS` lines are from intermediate edits, all resolved).

## Open questions for Architect

- Shared secret: `_verify_apple_receipt` now sends `settings.golfin_apple_shared_secret` (empty ⇒ none) for the golfin bundle. Consumable receipts validate without one; if Apple answers 21004 in sandbox, set the game's app-specific secret as a Fly secret.
- Legacy `/verifyReceipt` is deprecated by Apple but functional; the JWS is stored on the row (`verification_response.jws`) for the App Store Server API move that the deferred Phase 1c owns.
- The FakeStore re-delivered the previous session's pending fake order on the next Editor play (a second `verify → 404`) — exactly the relaunch-replay path; on device the same happens until the server answers 200.
