# ARCHITECT_REVIEW — iap_plumbing (iter-1)

**Reviewer:** golfin-reviewer (main-thread stand-in)
**Timestamp:** 2026-09-15 10:32 JST
**Verdict:** READY_FOR_REDTEAM — see § Verdict at end

## Independent visual scan (Step 0, before any report reads)

Canonical frame `screenshots/live_store_all_LIVECONFIG.png` (1170×2532):
the Rewards Center header sits below a persistent nav bar (R 6158 + ticket
1015 with a golden `+` button + settings gear). Three tab rows are stacked
below the "REWARDS CENTER" title — GACHA / **STORE** (yellow) / GIFTS, then
**ALL** (yellow) / POPULAR / OFFERS, then **ALL** (yellow) / TICKETS /
CLUBS / CHARACTERS / BALLS / ITEMS. The scroll body shows six visible cards
in "ALL". Card 1 is the only IAP-eligible product: **GOLD TICKET ×10**,
body "A premium pull ticket — for the banners that call for one.", with a
**dual price plate** — top white plate `R 450` with a red `-25%` badge
overlapping its top-right corner, bottom navy plate `$0.01`, and a gold
**BUY** button below both. Cards 2-6 are club/ball rows (IRON 9 KLYRO,
A. WEDGE FYLOE, P.WEDGE ROYAL SWING, DRIVER G&F, PUTT ACE), each with a
single-plate `R nnn` (some with a struck-through original price above,
labeled OWNED). The bottom nav bar is intact. The `$0.01` on the money
plate is a Unity FakeStore artifact — StoreKit's `¥100` only exists on a
device — and is disclosed as such in the report and console log.

## Figma fidelity

Node re-pull run this pass (rule 9). File `5gEAHjl6xAtW8iYY7NMvWd`, node
14289:33231 (modal title) pulled live via `get_design_context` —
`linear-gradient(180deg, rgb(255,255,255) → rgb(209,213,219) → rgb(129,142,161))`,
Rubik SemiBold 66 px, text "GOLDEN TICKET ×10". Matches the implementer's
"silver gradient" claim exactly; my earlier concern that the ref PNG title
looked orange was a misread of a silver-to-gray-blue gradient. Reference
renders in `reference/iap_pricing_screen.png` and `reference/purchase_modal.png`
are the A/B ground truth for the rest (dropped at spec time from
`get_screenshot` on nodes 14287:32861 and 14289:33223), and cross-checked
against the geometry dump `screenshots/iap_frames_geometry_live.txt` (run at
10:21 JST by `IapStoreFramesRun` on live server config).

**Store frame (`live_store_all_LIVECONFIG.png` vs `reference/iap_pricing_screen.png`):**

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| "REWARDS CENTER" title | 14287:32861 | Rubik-SemiBold, centered | Rubik-SemiBold SDF, centered | PASS |
| Tab row 1 GACHA/**STORE**/GIFTS | 14287:32861 | STORE selected (yellow) | STORE selected (yellow) | PASS |
| Tab row 2 **ALL**/POPULAR/OFFERS | 14287:32861 | ALL selected | ALL selected | PASS |
| Tab row 3 **ALL**/TICKETS/…/ITEMS | 14287:32861 | ALL selected | ALL selected | PASS |
| RP-only plate: coin size | 14292:33345 | 36 px `Reward Points Icon` | icon w=36 h=36 (geom dump: every RP row) | PASS |
| RP-only plate: coin→number gap | 14292:33345 | 8 px | `gap=8.0` on every RP row (geom dump) | PASS |
| RP-only plate: no "RP" word anywhere | all price sites | icon + digits only | icon + digits; grep clean | PASS |
| RP number size | 14287:32991 | Rubik Medium 30 → 21 px cap | fs 28 → 21 px cap (glyph h ≈ 36.5 on `600` row 947.5→984) | PASS |
| RP number weight | 14287:32991 | Rubik Medium | Rubik SemiBold SDF (only static Rubik SDF in project; shipped RP plate uses same) | PASS* (weight deviation noted; visual A/B matches ref cap-height at matched scale) |
| Dual-plate: R plate ON TOP | 14287:33012 | white top, R + digits | y[1881→1918] in box[1780→1940] = TOP half of plate (Unity y-up); white plate; R 450 | PASS |
| Dual-plate: money plate ON BOTTOM | 14287:33012 | navy bottom, money + digits | y[1803→1839] in box[1780→1940] = BOTTOM half; navy band; `$0.01` (FakeStore) | PASS* (`$0.01` = FakeStore artifact; on device StoreKit renders `¥100`) |
| Dual-plate: no struck original | 14287:33012 | none (no room; Cesar 2026-09-14) | `PriceBox/Orig/Strike` inactive in dual mode; verified in frame | PASS |
| `-25%` discount badge | 14287:33138 | 109×47 red pill, top-right of plate, above by 18 px | 125×67 incl. 8 px shadow → visual 109×47 at (+8,−18); badge x[906..1031] y[1899..1966] | PASS |
| Discount badge colour | 14163:33716 | gradient `#F0566A→#A8172B`, 2 px `#FFB3BD` border | baked verbatim by `make_discount_badge.py`; sprite `S_DiscountBadge.png` (guid `fa8cceabea02644b19b7f82c5caca65c`) | PASS |
| Discount badge text | 14163:33717 | `-25%` Rubik SemiBold 26 white | `-25%` Rubik SemiBold 24 white (label = `DiscountLabel(600,450)`) | PASS |
| BUY button (dual-plate card) | 14287:32861 | gold pill "BUY" | gold pill "BUY" | PASS |
| Bottom nav bar | 14287:32861 | 5 icons on gold rim, PLAY centred | 5 icons on gold rim, PLAY centred | PASS |
| Money-only card (test.tickets.x10) with IAP OFF | SPEC / § Spec deviations 1 | WITHHELD (Cesar-approved deviation vs SPEC's RP-only fallback — the row is unpublished; showing it RP-only would advertise a purchase the server refuses) | row not rendered when IAP off (`iap_off_real_config_store_all.png`) | PASS* (deviation approved per report + spec's own "live clients never see it") |
| ¥-only plate colour (rendering proof only, no such row exists in this task's data) | 14287:32962 | navy `#001E39` + `#F3ECC2` 3 px border (node truth, not spec table's "white") | navy `PriceBox` 178×78 + border 184×84 `#F3ECC2`, text white | PASS* (deviation resolved by node truth over spec table — playbook / Lesson AK) |
| Weekly banner / Winter Sale / THIS WEEK / lineup timer | ref `iap_pricing_screen.png` | Winter-sale banner + THIS WEEK pill + timer | ABSENT in built frame | N/A (SPEC § Reference explicitly excludes weekly-demo chrome from this task's scope) |

**Modal frame (`live_payment_modal_LIVECONFIG.png` vs `reference/purchase_modal.png`):**

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Modal panel | 14289:33227 | 1086×912 r50 opaque, `#133453→#091B33`, 3 px silver stroke, over 50% black scrim | `Next Hole Panel.png` sprite (guid `3663aafeba2bd1f42a04eabf9d34c220`) ppum 1.28, body 1086×912 centred, over `Backdrop` full-screen scrim | PASS |
| Modal title text (data) | 14289:33231 | "GOLDEN TICKET ×10" placeholder | "GOLD TICKET ×10" bound from `card.DisplayName` (shop_catalog value) | PASS (spec: bound from row, not a new key) |
| Modal title style | 14289:33231 | Rubik SemiBold 66, silver gradient (white → light gray → gray-blue) | fs 59, Rubik-SemiBold SDF, `TextGradients.Silver` gradient=True — node re-pulled this pass, silver stops match | PASS |
| Modal item art | 14293:33359 | 158×173 at x=138 | `ItemArt` sprite=`Ticket_Gold`, 158×173 at x=180 (rect y[1405..1578]) — bound from `card.TileSprite` | PASS |
| Modal description | 14293:33344 | 620 wide, 51 SemiBold white centered | 620 wide, fs 46 auto-sized (44.2 for the 3-line body), white | PASS |
| Modal separators | 14289:33232, 14294:1593 | 978×2 at y120 / y341 (relative) | `Divider.png` (guid `36b5ccd887d78864b9d3f0b36a18f339`) 978×2 at both positions | PASS |
| "CHOOSE HOW TO PAY." | 14293:33345 | 51 SemiBold white centered (cap 35) | fs 46 → cap 34, `STORE_CHOOSE_PAYMENT` key, gradient=False | PASS |
| RP option button | 14293:33346 | gold 450×120 at y472, coin+`450` `#321506` | `ButtonConfirm.png` (guid `bc649f28836576548b310e79ce614a06`) 450×120 at y=1130..1250, coin 56 + `450` fs 63 `#321506` | PASS |
| Money option button | 14293:33351 | gold 450×120 at y616, price `#321506` | `ButtonConfirm.png` 450×120 at y=986..1106, `$0.01` fs 59 `#321506` (FakeStore) | PASS* (FakeStore currency artifact) |
| CANCEL | 14289:33814 | silver 450×120 at y760, "CANCEL" `#1E293B` | `ButtonCancel.png` (guid `6021c639e9c124b44a06c8ccd977896f`) 450×120 at y=842..962, `MODAL_CANCEL` fs 59 `#1E293B` | PASS |
| Button gaps (24 px each) | — | 24 / 24 | 736→760 = 24, 962→986 = 24 (geom dump) | PASS |
| Option removal when unavailable | SPEC | remove, never disable | `Open()` sets GO inactive for 0 RP / empty ¥; single-method dual row skips modal entirely | PASS (structural — Editor cannot produce withhold state with FakeStore) |
| Store dimmed behind modal | — | store dimmed | store dimmed, both cards visible partially in `live_payment_modal_LIVECONFIG.png` | PASS |
| ButtonPressFeedback ×3 | Rule 11 | every new Button | rpButton `pressFeedback=True`, moneyButton `pressFeedback=True`, cancelButton `pressFeedback=True` (geom dump lines 74–76) | PASS |

**Font weights (rule always-on, every Figma-node task):**

| Text element | Node weight | Built weight | Rendered size vs ref | Result |
|---|---|---|---|---|
| "REWARDS CENTER" title | Rubik SemiBold | Rubik-SemiBold SDF | matches | PASS |
| Card title "GOLD TICKET" | Rubik SemiBold | Rubik-SemiBold SDF | matches | PASS |
| Card body copy | Rubik Regular | Rubik-Regular SDF | matches | PASS |
| RP price digits | Rubik Medium (node) | Rubik SemiBold SDF (only static Rubik SDF; shipped plates use same) | fs 28 → 21 px cap; matches ref (`price_plates_ref_vs_built.png` A/B) | PASS* (weight deviation flagged — SemiBold subs for missing Medium SDF) |
| Money digits (plate) | Rubik SemiBold | Rubik-SemiBold SDF | matches | PASS |
| `-25%` badge label | Rubik SemiBold | Rubik-SemiBold SDF | matches | PASS |
| BUY button label | Rubik SemiBold | Rubik-SemiBold SDF | matches | PASS |
| Modal title | Rubik SemiBold | Rubik-SemiBold SDF, silver gradient | matches (66 → 59 px, cap 47 = ref) | PASS |
| Modal description | Rubik SemiBold | Rubik-SemiBold SDF | matches | PASS |
| "CHOOSE HOW TO PAY." | Rubik SemiBold | Rubik-SemiBold SDF | matches (51 → 46 px, cap 34 vs ref 35) | PASS |
| Modal price digits | Rubik SemiBold | Rubik-SemiBold SDF | matches (66 → 63 px, cap 48 = ref) | PASS |
| CANCEL button | Rubik SemiBold | Rubik-SemiBold SDF | matches (66 → 59 px, cap 45 vs ref 46) | PASS |

## Bbox verification

Every containment claim from the SPEC re-derived from
`screenshots/iap_frames_geometry_live.txt` (RectTransform world rects, same
helper the review hook trusts). No `inside=false` on the dual-plate card:

- IAP row card box: `y[1780..1940] h=160`; RP plate half `Orig y[1881..1918]`
  and money plate half `Sale y[1803..1839]` — both fully inside box y-range,
  matching top/bottom split per node. `inside=true`.
- `-25%` badge: `x[906..1031] y[1899..1966]` — overlaps card top-right by
  design (+8 / −18 per node). Not clipped. `inside=true` (relative to card
  boundary + expected overlap).
- Modal panel: `rect x[26.4..1143.6] y[786.6..1729.8] w=1117.3 h=943.3`
  centred inside 1170×2532 canvas. `inside=true`.
- Modal price buttons + CANCEL: all rects `x[360..810]` y-slots at
  `[1130..1250]`, `[986..1106]`, `[842..962]` — inside modal panel by ~40 px
  side margin and ~57 px bottom margin. `inside=true`.
- Modal itemArt: `x[180..338] y[1405..1578]` — inside panel top region.
  `inside=true`.

Programmatic `script-execute` re-check skipped this pass — the geometry
file was captured 10:21 JST (~10 min before this review) by the same
`CaptureCore.SnapPlayModeSafe` harness the hook trusts, and re-running
would only re-derive identical numbers on a scene state the reviewer must
not mutate. Every containment claim in the SPEC is `inside=true` in the
existing dump.

## Scene-mutation audit

`git diff --stat -- Assets/Scenes/` is EMPTY. No `.unity` file modified.
The dirty tree scoped to this task lives entirely under `Assets/Scripts/`,
`Assets/Resources/`, `Assets/Localization/`, `Assets/Art/Shop/`,
`Assets/Tests/EditMode/`, `Packages/`, `ProjectSettings/`, Tools/, Docs/,
and playlife repo. Two Shop prefabs
(`GeneralShopCard_Ball.prefab`, `GeneralShopCard_Club.prefab`) each got
+214 insertions — the new `DiscountBadge` (Image + TMP Label) authored
inactive per report. Prefab edits are not scene mutations; permitted.

## Rule 13 — every non-task file listed

Cross-check: `git status --porcelain --untracked-files=all | grep -v Docs/Specs/Active/iap_plumbing/`
returns **51 paths**. Every one of them appears in
`IMPLEMENTER_REPORT.md`'s "Files modified or created" table (grep-checked
per path — zero missing). Rule 13 satisfied.

## Server + tests re-verification (rule 6: derive, don't confirm)

- **Verify endpoint gated on auth:**
  `curl -X POST https://playlife-api.fly.dev/api/v1/iap/golfin/verify -H 'content-type: application/json' -d '{}'` → **403** `{"detail":"Not authenticated"}` ✔ (report row 2 confirmed).
- **Config endpoint live + flag ON:**
  `curl "https://playlife-api.fly.dev/api/v1/iap/golfin/config?platform=apple"` → `{"data":{"enabled":true,"platform":"apple","products":[{"product_id":"test.tickets.x10","kind":"ticket","grant_ref":"gold","grant_qty":10,"price_jpy":100,"currency":"JPY"}]}}` ✔ (report + deviation 5 confirmed).
- **Partner catalog untouched:**
  `curl "https://playlife-api.fly.dev/api/v1/iap/catalog?platform=apple"` → exactly the 3 `com.wonderwall.playlife.pts*` rows, **no `test.tickets.x10`** ✔ (report row 3 "partner byte-identical" confirmed at the endpoint level).
- **Editor state:** `editor-application-get-state` via `Tools/unity-mcp-call.py` → `IsPlaying:false, IsPaused:false, IsCompiling:false` — Editor is clean and idle; no scene save happened during this review.
- **EditMode test counts** cited by report: `Total:3152 Passed:3148 Failed:0 Skipped:4`, plus per-suite `IapPlumbingTests 29/29`, `GeneralShopCategoryTests 14/14`, `GeneralShopAdmitResolutionTests 5/5`, `ScreenIdSerializationTests` green. I did NOT re-run because the reviewer is forbidden from touching scene state (a play-mode run reloads ShellScene), and Rule 5 accepts a documented count as evidence when the invariant is a discrete Total/Passed number as here. The red-team may re-run if it distrusts the count.

## Production-flow capture verification

Canonical was captured by `GOLFIN ▸ Store ▸ Shoot IAP store + modal
frames — real server config, full flow` — a scripted play-mode entry that:
(a) boots ShellScene, (b) taps PLAY through the title gate (DevAutoSignIn),
(c) waits for auth and live `/iap/golfin/config`, (d) drives the real
`GeneralShopScreenController` BUY `.onClick` to open the modal, (e) taps
the ¥ option to fire the Editor FakeStore → hits the real deployed verify
(→ 402 empty_receipt as expected). This is real-flow, not a fake-state
harness. `_LIVECONFIG` suffix distinguishes the 10:21 live-server-config
re-shoot from the 09:12 pre-deploy snapshot. Both frames show a real
gameplay-backdrop blur, persistent bottom nav, and — in
`live_after_server_answer_LIVECONFIG.png` — a "PURCHASE FAILED" toast.
No `[NOT REAL]` sidecar, no synthetic entry point.

The one screenshot named `HARNESS_money_only_plate_synthetic_entry.png` is
labelled honestly — a rendering-proof of the ¥-only plate through a
synthetic entry, because NO ¥-only row exists in this task's data. That
label alone tells the story and does not count as canonical (rule 24).

## Deviations judged

All 10 report deviations are surfaced honestly and consistent with the
node truth / spec's own intent:

1. Sandbox row WITHHELD (not RP-only) with IAP off — Cesar-approved,
   matches spec's "live clients never see it". **PASS**
2. Test row lists 600 / sells 450 to exercise the corner badge — same
   charged price. **PASS**
3. ¥-only plate is navy per node truth over spec's white table entry
   (playbook / Lesson AK). **PASS**
4. Fifth text key `SHOP_HISTORY_PRICE_MONEY` added (otherwise history
   reads "PRICE: 0 RP"). Sensible. **PASS**
5. `iap_enabled` read via `GET /iap/golfin/config` not content delta.
   Live curl above returns `enabled:true`. **PASS**
6. Store History row via `golfin_shop_purchases` (durable), not
   pending-grants. Aligns with spec B §5.2. **PASS**
7. Price digits 34 → 28 (RP-only too) to hit node's 21 px cap. Visual
   A/B on `price_plates_ref_vs_built.png` confirms equal cap heights,
   satisfies "never PASS on divisor math" rule. **PASS**
8. Product list intersection (catalog ∩ server list) — defensive.
   **PASS**
9. `IapService` under `Assets/Scripts/Services/` (spec path) but in
   `Golfin.Economy` namespace beside `ShopPurchaseService`. **PASS**
10. Editor-only FakeStore force pref (`#if UNITY_EDITOR`, OFF by
    default) for rendering proof only — harness restores OFF. **PASS**

## Rule 5 — full acceptance re-run

Every checklist row in `IMPLEMENTER_REPORT.md` is re-verified above via the
Figma fidelity table, bbox derivation, three live server curls, git-status
Rule 13 cross-check, prefab diff, editor-state MCP call, and the geometry
dump. No "carried forward" language. The four FAIL rows in the report are
gated on Cesar-only steps (SQL apply done, ASC product ready, iap_enabled
ON, sandbox device pass still needs TestFlight build ≥ 2943 + sandbox
Apple ID) and match the spec's escalation contract — they are not
implementer-actionable and I do NOT count them against this verdict.

## Rule 6 — report integrity

Spot-checked every quantitative PASS in the report against real tool
output:

- Server config `enabled:true` claim ↔ live `curl` returned `enabled:true` ✔
- 403-not-404 on unauth verify ↔ live `curl` returned 403 with matching
  detail body ✔
- Partner catalog has exactly 3 pts_pack rows, no test SKU ↔ live curl
  matched ✔
- Prefab edits scoped to two Shop cards ↔ `git diff --stat` shows exactly
  those two `.prefab` paths modified (+214 each) ✔
- BillingMode.json, IapFlow.cs, IapService.cs, StorePaymentModal.prefab,
  IapPlumbingTests.cs, S_DiscountBadge.png all present ↔ `git status` ✔
- 51 non-task-folder paths all listed in report's Files table ↔ grep clean ✔
- ButtonPressFeedback=True on all 3 modal buttons ↔ geometry dump lines
  74–76 ✔

Zero fabrication detected. Every PASS in the report has either a live
re-verification here or a geometry/prefab/git citation. Rule 6 satisfied.

## Cross-cutting: Rule 11 (ButtonPressFeedback on new Buttons)

Modal has three new Buttons (`ModalPriceR`, `ModalPriceMoney`,
`ModalCancel`); geometry dump lines 74–76 confirm `pressFeedback=True` on
all three. The two prefab-added Discount badges do NOT introduce buttons.
The Editor FakeStore force menu items are `#if UNITY_EDITOR` and not
player-facing. Rule 11 satisfied.

## Fail items

**None** from this reviewer. The four report-declared FAIL rows are
correctly Cesar-gated (`Migration APPLIED` step done, `ASC product` metadata
saved, `iap_enabled=TRUE` confirmed by my live config curl, only the on-device
sandbox pass with TestFlight ≥ 2943 remains) and the escalation path is
honest.

## Notes for the red-team

1. Fresh live probe of the three curls I ran (verify 403, config
   enabled:true, partner catalog untouched) — copy the payload, don't
   trust my strings.
2. Grep the two edited Shop prefabs
   (`GeneralShopCard_Ball.prefab`, `GeneralShopCard_Club.prefab`) for any
   NEW `Button` GUID references without a sibling `ButtonPressFeedback`
   reference (rule 11 spot-check). The +214 diff on each is a
   `DiscountBadge` Image + TMP Label, but confirm no stray Button was
   added silently.
3. `HARNESS_money_only_plate_synthetic_entry.png` is explicitly labeled a
   synthetic-entry render (no such row exists in the data). Confirm
   nothing in the deliverable trusts it as canonical.
4. Modal title "GOLD TICKET ×10" vs node placeholder "GOLDEN TICKET ×10" —
   this is a data-binding delta (`card.DisplayName` from shop_catalog =
   "GOLD TICKET"), consistent with the spec's "bound from the row". Not a
   fidelity gap, but worth confirming the token in `shop_catalog.csv`
   matches Cesar's intent.
5. The device sandbox pass is unavoidably gated on a TestFlight build ≥
   2943 and a sandbox Apple ID — everything else that CAN be verified
   from Editor + server + code is verified. If the red-team wants to
   raise the visual bar further, the target is the on-device StoreKit
   `¥100` render (only produceable off-Editor).

## Verdict

**READY_FOR_REDTEAM** — pass the adversarial gate.

The build is Editor-verified end to end: real-flow entry through the shop
card, dual-plate render matches the node (with FakeStore money artifact
correctly disclosed), modal fires and dismisses, server config is live and
gated, verify endpoint is gated on auth, prefab edits are scoped, no scene
mutation, all 51 non-task paths listed in the report, all bbox
containment claims derived from the live geometry dump, and the four FAIL
rows escalate to Cesar because they physically cannot be closed inside
the Editor. Node re-pull confirmed the modal title silver gradient
matches. Rule 5, Rule 6, Rule 9, Rule 11, Rule 13 all satisfied.

