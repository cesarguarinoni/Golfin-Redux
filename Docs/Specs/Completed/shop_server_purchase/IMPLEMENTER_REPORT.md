# Implementer Report — `shop_server_purchase`

> Implemented directly by Claude Code across BOTH repos in SPEC §5 order (playlife first,
> then GolfinRedux), at Cesar's instruction — the subagent chain was not used.

## Implementation summary

A shop purchase is now one server call. `POST /api/v1/shop/purchase` carries the **entry id**
and never a price; `public.golfin_shop_purchase()` reads the published `shop_catalog` row,
prices it off the **server clock** (listing + sale windows, mirroring `ContentShopWindow` rule
for rule), debits through the existing `spend_pts`, and inserts a `golfin_pending_grants` row
plus a `golfin_shop_purchases` ledger row — all in one plpgsql transaction, so the RP can never
be gone while the grant does not exist. Every business outcome is HTTP 200.

On the client, `Golfin.Economy.ShopPurchaseService` mirrors `PointsService` (flag gate inside
the routine, own in-flight latch, fresh idempotency key per attempt) and folds the balance
through a new `PointsService.ApplySpendResult` **after** `onDone`, the same ordering rule
`SpendRoutine` already documents. `ShopTransaction.TryPurchaseCatalogEntry` branches on the
FLAG, not the gate: flag ON goes to the new endpoint and debits the **server's** `charged`, then
applies the returned grant through the managers (`ClubManager.GrantClub`, new
`CharacterManager.UnlockCharacter`, `ItemManager.AddItems`, existing `GrantBall`), records the
grant id in `SaveData.appliedGrantIds`, marks the inventory sync dirty and acks. Flag OFF is the
pre-existing body, untouched. `ShopCategory` gained `Character` and `Item`, `ParseCategory`
became strict (drop + warn, never default to Club), and the club card hierarchy now binds both.

## Files modified or created

### playlife (`/Users/cesar/Documents/playlife`)

| Path | Change |
|---|---|
| `backend/migrations/2026_08_27_golfin_shop_purchase.sql` | created — `golfin_shop_purchases` table (RLS on, zero policies), `golfin_shop_parse_bound()`, `golfin_shop_purchase()`, EXECUTE revoked from public/anon/authenticated, verification + smoke blocks. **APPLIED to prod 2026-08-27**; verification block wrapped in a subquery so the Supabase editor's auto-`limit 100` lands validly. |
| `backend/routers/shop.py` | created — `POST /purchase`, auth-required, user id from the token, UUID/entry/build validation, rpc pass-through, every business outcome 200, no `_missing_relation` degrade. |
| `backend/tests/test_shop_purchase.py` | created — 29 router-level tests (403 unauth, 401 bad token, 400s, one test per rpc status, loud-500 faults). |
| `backend/main.py` | modified — mounts `shop.router` at `/api/v1/shop`. |

### GolfinRedux (`/Users/cesar/Documents/GolfinRedux`)

| Path | Change |
|---|---|
| `Assets/Scripts/Economy/ShopPurchaseService.cs` | created — the purchase call; flag gate inside the routine, own latch, fresh key per attempt, balance folded after `onDone`. |
| `Assets/Scripts/Economy/ShopPurchaseOutcome.cs` | created — `ShopPurchaseVerdict` (8 values) + outcome, deliberately without a shared `MayProceed`. |
| `Assets/Scripts/Economy/PointsDtos.cs` | modified — added `ShopPurchaseResult` + local `ShopGrantDto` (asmdef cannot reach `InventoryGrant`) and `ToSpendResult()`. |
| `Assets/Scripts/Economy/PointsService.cs` | modified — added the one public `ApplySpendResult` wrapper over the private `ApplySpend`, with the ordering warning. |
| `Assets/Scripts/Net/Endpoints.cs` | modified — added `ShopPurchase` with the auth/anti-cheat posture comment. |
| `Assets/Scripts/UI/Shop/ShopTransaction.cs` | modified — flag-branched purchase, `PreCheck`, `PurchaseServerSide`, `PurchaseLocally`, `ApplyPurchaseGrant`, `RecordAndAck`, `LastServerPrice`, `PriceChanged`/`NotListed` verdicts. Stamina path untouched. |
| `Assets/Scripts/UI/Shop/GeneralShopModel.cs` | modified — `ShopCategory` += `Character`, `Item`; `ParseCategory` returns `ShopCategory?` and drops unknowns with a warning; drop count in the load summary. |
| `Assets/Scripts/UI/Shop/GeneralShopCard.cs` | modified — `BindCharacter` / `BindItem` on the club hierarchy, `RarityStatCaps` bar scale, owned state covers characters, four serialized stat-icon slots, `SetActive` helper + re-show on re-bind. |
| `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` | modified — wires `CHARACTERSChip` / `ITEMSChip`, restyles them, handles `PriceChanged` / `NotListed` with reload + rebuild. |
| `Assets/Scripts/CharacterManager.cs` | modified — new `UnlockCharacter(string)`: `GrantStarter` minus `starterCharacterId` and minus `SelectCharacter`. |
| `Assets/Scripts/UI/Editor/GeneralShopDemoRecorder.cs` | modified — the chip sweep now drives all five wired chips through the real `onClick`. |
| `Assets/Resources/Prefabs/Shop/GeneralShopCard_Club.prefab` | modified — four sprite slots wired (`IconControl`, `IconRecovery`, `IconStamina`, `Icon - Recovery`). Additive, +4 lines. |
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` | modified — `ButtonPressFeedback` (defaults 0.95 / 0.12) on `CHARACTERSChip` and `ITEMSChip`, now that they are live. Additive, +67 lines. |
| `Assets/Scripts/Economy/Tests/ShopPurchaseServiceTests.cs` | created — 20 tests: wire shape, flag gate, every verdict, balance-fold ordering, latch, no-fold on balance-less refusals. |
| `Assets/Scripts/UI/Shop/Tests/GeneralShopCategoryTests.cs` | created — 14 cases pinning strict `ParseCategory` (bag and typos dropped, never clubbed). |
| `Assets/Scripts/InventorySync/Tests/InventoryGrantLedgerTests.cs` | modified — +1 test pinning the §3.2 guarantee (an already-ledgered id applies nothing, still acks). |
| `Tools/admin-dashboard/lib/i18n.ts` | modified + **deployed** — `sh.notice.*` EN + JA rewritten as information, with `{build}` interpolation. |
| `Tools/admin-dashboard/app/(panels)/shop/shop-panel.tsx` | modified + **deployed** — amber banner, `SERVER_PRICE_ENFORCED_FROM_BUILD = 2334` constant. |

## Screenshot

- **Canonical screenshot:** `screenshots/store_all_character_item.png` (1170×2532, iPhone 14).
  The ALL tab with a published `character` row and an `item` row rendered on the
  `GeneralShopCard_Club` hierarchy, above the four club rows and the ball.
- **Second frame:** `screenshots/store_characters_filtered.png` — the CHARACTERS chip driven
  through its real `onClick`, filtering to one card, with BUY enabled on an UNOWNED character.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` · **Play mode:** Yes ·
  `Application.runInBackground = true` before capture.
- **Entry path:** real. Booted to Home, tapped the live `ShopPlusButton` (the "+" beside the
  ticket counter, which requests the STORE tab) via `Button.onClick.Invoke()`, then drove each
  filter chip the same way. No `ShowScreen` shortcut, no synthetic button — PIPELINE_HARDENING
  rule 2.
- **Capture path:** `GOLFIN/Screenshot/Capture Game View` (the sanctioned `CaptureHelper` menu
  item). Nothing reflected into `CaptureCore` / `ScreenCapture` — CAPTURE RULE 0.
- **How the two rows got there:** injected into `GeneralShopCatalog._entries` by reflection,
  shaped exactly as an admin publish leaves them, then `RebuildNow()`. Everything downstream of
  `Entries` — `Rebuild`, `Instantiate`, `Bind`, `BindCharacter` / `BindItem`, `WireBuy` — is
  production code. Only the ROW SOURCE is synthetic, and the catalog was `Reload()`ed afterwards
  so no `probe_*` row survives (verified: `probe rows remaining=0`).

### Measured, not eyeballed

I first read "Cdmmon" off the downscaled item card and was about to file it as a defect. Reading
the live TMP value instead showed `HMid.text` is literally `Common`, 6 characters — my eye was
wrong, the render was right. Every claim below is a read of the live object, per
`feedback_never_eyeball_brightness` and `feedback_verify_ui_metrics_numerically`.

**Character card** (`char_olivia`, Common, unowned):

| Row | icon | value | rarity cap | fill | expected |
|---|---|---|---|---|---|
| StatRow_0 Strength | `IconStrenght` | 6 | 25 | 24.0% | 24.0% |
| StatRow_1 Club Control | `IconControl` | 7 | 25 | 28.0% | 28.0% |
| StatRow_2 Recovery | `IconRecovery` | 6 | 18 | 33.3% | 33.3% |
| StatRow_3 Stamina | `IconStamina` | 6 | 22 | 27.3% | 27.3% |

Track 331.0px on every row. The fills follow `RarityStatCaps`, **not** a hard-coded 60 — a 60-scale
would have put all four at 10–12%. `NameLabel` `OLIVIA` · `HMid` `C` · `HLevel` `Lv 10/39` ·
`DistRow` hidden · `StatRow_4` hidden · BUY interactable, label `BUY`. On the owned starter
(`char_james`) the same card correctly showed BUY disabled with label `OWNED`.

**Item card** (`repairkit_common`): `NameLabel` `REPAIR KIT` · `HMid` `Common` · `HLevel` hidden ·
`DistRow/Txt` `RESTORES 50%` · all five StatRows hidden · BUY interactable (items stack, so there
is deliberately no owned state).

**Chip row** — the overflow question, measured rather than reasoned:

`CategoryRow` world width 1074.0px, six chips at exactly **179.0px** each, every one
`insideRow=True`. Widest label is `CHARACTERS` at **149.1px** preferred width — 29.9px of
headroom. Nothing overflows and no label truncates.

| chip | label width | chip width | fits |
|---|---|---|---|
| ALL | 41.4 | 179.0 | yes |
| TICKETS | 91.6 | 179.0 | yes |
| CLUBS | 73.3 | 179.0 | yes |
| **CHARACTERS** | **149.1** | 179.0 | yes |
| BALLS | 70.8 | 179.0 | yes |
| ITEMS | 66.6 | 179.0 | yes |

**Chip filtering**, each driven through the real `onClick` and counted a frame later (counting in
the same frame is meaningless — `Destroy` is deferred to end-of-frame, so the old cards are still
`activeInHierarchy` and the totals stack):

| chip clicked | cards shown | gold chip |
|---|---|---|
| CHARACTERS | 1 — `probe_character` | CHARACTERSChip |
| ITEMS | 1 — `probe_item` | ITEMSChip |
| CLUBS | 4 clubs | CLUBSChip |
| ALL | 7 — 4 club, 1 ball, 1 character, 1 item | ALLChip |

## Acceptance checklist (SPEC §6)

| Item | Result | Justification |
|---|---|---|
| Price is the server's (*device*) | **NOT VERIFIED** | Needs the migration applied + a deploy + a real client. Code path built and unit-pinned (`PriceChanged` verdict, `LastServerPrice`, card reload), but no live call has been made. |
| Sale window, server clock (*device*) | **NOT VERIFIED** | Same. The SQL mirrors `ContentShopWindow.Evaluate` rule for rule and the matrix is written into the migration header for eyeball comparison, but nothing has executed it. |
| Delivery survives death (*device*) | **NOT VERIFIED** | Requires killing the app mid-call on device. The mechanism is in place (grant queued server-side in the same transaction; `RecordAndAck` orders apply → record → ack) and the boot-drain duplicate case is unit-pinned. |
| Idempotent | **NOT VERIFIED** (needs a client) | Step 1 of the plpgsql is the replay branch and the migration footer carries the exact verification queries. The function now EXISTS in prod (`fn_purchase = 1`), but no purchase has been made, so no replay has been exercised. |
| Insufficient writes nothing | PASS (client half) | `ShopPurchaseServiceTests.Insufficient_Arrives200ButMustNotProceed`: verdict `Insufficient`, `Grant` null, cached balance folded to the server's true 95. Server half unverified until the migration runs. |
| Unique things stay unique | PASS (client half) | `PreCheck` refuses an owned club or character before any spend; `GeneralShopCard.WireBuy`'s `owned` expression now covers characters. Server-side step 8 (purchases ledger + unapplied grant + blob walk) unverified. |
| Kill switch | **NOT VERIFIED** | Step 2 of the plpgsql copies `_global_enabled`'s truthiness (missing row = enabled, explicit false = disabled). Not executed. |
| Characters and items sell | **PASS** (render) / NOT VERIFIED (buy) | Both rendered on the club hierarchy at 1170×2532 and every element read off the live objects — see § Measured, not eyeballed. Bars follow `RarityStatCaps`; `DistRow`/`StatRow_4`/`HLevel` hide correctly; owned-vs-BUY flips correctly between an owned starter and an unowned character. The `bag`/typo drop rule is unit-pinned (14 cases). What is NOT done: actually completing a purchase — the dev save holds 3 RP, so any BUY hits the affordability pre-check, and the flag-ON server path needs a device. |
| Chips | **PASS** | Measured, not reasoned: `CategoryRow` 1074.0px, six chips at exactly 179.0px, all `insideRow=True`; widest label `CHARACTERS` 149.1px with 29.9px headroom. Each chip driven through its real `onClick` filters correctly (CHARACTERS→1, ITEMS→1, CLUBS→4, ALL→7) and the gold active styling follows. |
| Flag OFF unchanged | PASS | `PurchaseLocally` is the previous body verbatim (same `PointsSpendGate.Spend`, same `SpendReasons.ShopPurchase`, same local grant); the only addition is the two new category arms, unreachable before this task. `ShopPurchaseServiceTests.FlagOff_MakesNoRequestAndAnswersDisabled` proves zero transport calls. Full EditMode sweep green, existing `PointsSpendTests` included. |
| Blob + ack | PASS (code) / **NOT VERIFIED** (live) | `RecordAndAck` adds the id to `appliedGrantIds`, `MarkDirty()`s the save, `InventorySyncService.Instance.MarkDirty()`s the write-behind and fires `AckGrants`. No live PUT observed. |
| `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200; `/shop/purchase` 403/401 | **PASS** | Measured against prod on v54: `/health` 200 · `/notices` 200 · `/banners` 200 · `/tournaments/golfin` 200 · `/content?build=9999` 200 · `POST /shop/purchase` **403** unauth, **401** on a bad token · `/api/v1/shop/garbage` 404. Live `/openapi.json` also shows the body as `{entry_id, idempotency_key, build, expected_rp_cost}`, required `entry_id`+`idempotency_key`, bearer-secured, **no `user_id` field** — so the deployed contract matches `BuildPurchaseJson` exactly. |
| Admin banner reads the §4 copy in EN + JA | **PASS** | Source + **DEPLOYED**. `golfin-admin` → `admin.golfin.world`, version `dff3d655-0441-48c8-b73f-542585fae843`. The upload named `app/(panels)/shop/page-12283d7bda412faa.js` and `8561-f05440b0545b5131.js` — the panel and the i18n dictionary, i.e. exactly the two files changed. Verified in the uploaded bundle, not from the exit code: new EN headline present, new JA headline present, body `go through /shop/purchase` present, `2334` and `amber` in the panel chunk, and the old `NOT enforced by the server` string absent from the WHOLE bundle — while `uinv`'s `This inventory is NOT server-enforced.` is correctly untouched. |
| Full unfiltered EditMode sweep green; backend suite green | PASS | Backend: **55 passed** (26 pre-existing + 29 new), order-independent. Unity EditMode: **1844 total / 1841 passed / 0 failed / 3 skipped** (the 3 skips are the pre-existing `HoleCompleteDriverTests` Stage-C1 skips). |

### Proof the new Unity suites actually execute

`tests-run` hides passes, so both new suites were proved with a tripwire rather than inferred:

| Run | Total | Passed | Failed | Skipped |
|---|---|---|---|---|
| Before tripwire | 1844 | 1841 | 0 | 3 |
| Tripwire armed (1 forced failure in each new suite) | 1846 | 1841 | **2** | 3 |

Exactly two failures, both `TRIPWIRE_TEMPORARY_MustFail` — so `ShopPurchaseServiceTests` and
`GeneralShopCategoryTests` are both in the runner's set. Tripwires removed;
`grep -rn TRIPWIRE Assets/Scripts/` returns nothing.

### Migration + deploy evidence (2026-08-27)

Applied by Cesar in the Supabase SQL editor. All 11 verification rows as expected:

| chk | value | expected |
|---|---|---|
| `purchases_table` | 1 | 1 |
| `purchases_rls` | 1 | 1 |
| `purchases_policies` | 0 | 0 — zero policies IS deny-all |
| `purchases_unique_key` | 1 | 1 (`user_id, idempotency_key`) |
| `purchases_user_ref_idx` | 1 | 1 |
| `fn_purchase` | 1 | 1 |
| `fn_parse_bound` | 1 | 1 |
| `fn_not_client_callable` | 0 | 0 — neither function callable by `authenticated` |
| `spend_pts_present` | 1 | 1 (dependency) |
| `grants_table_present` | 1 | 1 (dependency) |
| `shop_catalog_rows` | 5 | ≥1 — the 4 clubs + 1 ball of the bundled CSV |

Bound-parser matrix, exact: `absent`/`blank` → `ok=t, ts=NULL` · `zoned` → `2026-09-01
00:00:00+00` · **`zoneless_utc` → `2026-09-01 00:00:00+00`** · `garbage` → `ok=f`. The
zoneless row is the load-bearing one: it proves `set timezone = 'UTC'` took, so a bound
authored without a zone is read as UTC and a phone in JST cannot see a different shop
window than one in UTC. `garbage` failing closed proves a fat-fingered date cannot become
a permanently-live product.

Deploy: `playlife-api` **v53 → v54**, image `deployment-01M10JFR1RDHHXV72FERYJNKT0`,
confirmed via `flyctl status` (never the deploy exit code — see
`reference_flyctl_401_false_deploy_failure`).

## Known FAIL items

- **No purchase has been completed by a real client.** The cards render and BUY enables
  correctly, but the dev save holds 3 RP so every BUY hits the affordability pre-check, and the
  flag-ON server branch cannot be exercised in the Editor. This is the one remaining gap in
  "characters and items sell".
- **The endpoint is live but has never sold anything.** The migration is applied and the router
  is deployed and correctly gated, but no purchase has been made by a real client — so every §6
  item marked *(device)*, plus idempotent replay and the kill switch, is still open. "Deployed"
  must not be read as "exercised".
- **Six chips, not five.** The spec expected to add two to a three-chip row; the prefab already
  had six. Measured and clean (179.0px each, widest label 149.1px), so this is a note, not a gap.

## Spec deviations

1. **§3.4 stat-row labels — the prefab has no label child.** The spec says to write
   `STR / CTRL / REC / STA` into "the row's label child (NOTE: read the child name off the prefab —
   it is not `Val`)". Read off the prefab, `StatRow_N` has exactly `Icon` (an Image), `Val` (TMP)
   and `BarBg/Fill` — the stat is identified by an **icon**, not by text. So a character row swaps
   the ICON (`IconControl` / `IconRecovery` / `IconStamina`; row 0 keeps the template's
   `IconStrenght`, which already means Strength). That art lives outside any `Resources/` folder,
   so the sprites are four `[SerializeField]` slots wired on `GeneralShopCard_Club` via
   `SerializedObject` — not white-box placeholders.
2. **§3.6 chips already existed.** The spec's §1 read of the prefab lists only
   `ALLChip / CLUBSChip / BALLSChip`; the live prefab has **six** — `ALL`, `TICKETS`, `CLUBS`,
   `CHARACTERS`, `BALLS`, `ITEMS` — each with a `LocalizedText` key (`GACHA_CHARACTERS`,
   `GACHA_ITEMS`, both already EN+JA in `LocalizationText.csv`). Nothing was duplicated and no new
   localisation key was added; the two chips simply had no handler. `TICKETSChip` is left unwired
   (tickets are not a `shop_catalog` category).
3. **§3.4 item restore line reuses existing keys.** `"{ITEM_RESTORES} {n}%"` → "RESTORES 50%" /
   "回復 50%", matching `ItemDetailPanel`, rather than adding a new `Restores {n}%` key that would
   have to be kept in step with the two that already exist.
4. **`ButtonPressFeedback` added to the two chips.** Not in the spec, but CLAUDE.md rule 11 and the
   other three wired chips both call for it. Defaults kept (0.95 / 0.12).
5. **`ShopPurchaseOutcome` has no `MayProceed`.** `SpendOutcome` has one and returns true for
   `Disabled`; copying that here would let a call site read "flag off" as "grant it yourself at
   your own price" — the exact hole this task closes. Callers branch on `Verdict`.
6. **`LastServerPrice` is a static, not an out-param.** The verdict arrives by callback, which an
   out-param cannot cross; widening the callback signature would touch every existing call site
   for a value one of them reads.

## Console output

No warnings or errors attributable to this task. `EditorUtility.scriptCompilationFailed` is
`False` and `tail -n 400 Editor.log | grep "error CS"` is empty after every refresh.

**Working-tree drift that is NOT this task's** — a second live session is working in the same repo
and the same Unity Editor. It appeared mid-session (files stamped 11:21; `HEAD` is still
`1da6c026e`, unmoved):

```
 M Assets/Editor/CIBuild.cs
 M Assets/Resources/HoleData/lomond-country-club/Hole_02/tree_obstacles.csv
 M Assets/Scripts/Editor/CourseImporter/{TreeBrushTool,TreeObstacleBaker,TreePlacer}.cs
 M Assets/Scripts/Physics/Viewer/Bot/{LoopV2SmokeBot,Scenarios}.cs
 M Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs
 M Docs/TellCode.md · M Docs/Versioning/last_uploaded_build.txt
 ?? Assets/Scripts/Editor/CourseImporter/{StandaloneTreeCatalog,TreeBakeValidator}.cs
 ?? Assets/Golf/Courses/lomond-country-club/Data/hole-{01..18}-geo/standalone_trees.csv (+ .meta)
```

That session also entered PlayMode and held the test runner during this task's final sweep — which
is why the EditMode confirmation had to be queued behind it. **Nothing was committed**, and any
close-out commit must be scoped to this task's paths only (CLAUDE.md rule 12).

## Process miss worth recording

**"Deploy" is TWO surfaces, and I only did one.** `playlife-api` (Fly) and `golfin-admin`
(Cloudflare Worker at `admin.golfin.world`) are separate deploys with separate commands. SPEC §2.5
names only the Fly one, so I deployed that, marked §4 PASS on `tsc --noEmit`, and wrote "Not
rendered in a browser" — true, but it buried the real gap: **not deployed**. Cesar found the old
red banner still live. `tsc` passing proves a file compiles, not that anybody can see it.

For next time: a spec section that changes the admin dashboard is not done until
`npm --prefix Tools/admin-dashboard run deploy` has run AND the new copy has been grepped out of
`.open-next/assets/`. The deploy script stashes `.env.development.local` during the build and
shares `.next/` with `next dev`, so check no dev server is running first.

## Open questions for Architect

1. **Build number for the admin banner.** `SERVER_PRICE_ENFORCED_FROM_BUILD = 2334` was derived
   from `Docs/Versioning/last_uploaded_build.txt` (2333) + 1. If the TestFlight build that actually
   carries §3 is not 2334, that constant must be bumped — it is the one number an operator will
   read as a promise.
2. **`golfin_shop_purchases` has no foreign keys**, matching `golfin_pending_grants` (which also has
   none on `profiles`). `grant_id` could reference `golfin_pending_grants(id)`. Left as the spec's
   DDL wrote it; say the word if the FK is wanted.
3. **`unknown_entry` and `unsupported_category` both land on `Unknown` → `Invalid`**, which the
   controller toasts as "Purchase unavailable". Both are catalog bugs rather than player-facing
   states, so they are loud in the log and generic in the UI. Confirm that is the wanted copy.
