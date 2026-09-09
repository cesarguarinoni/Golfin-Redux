# SPEC — `store_history`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-09-08 (Architect via Cowork). Figma `Store History Screen` node `13509:2978` in
> file `5gEAHjl6xAtW8iYY7NMvWd`. Closes the player-visible promise left by
> `GachaTabController.OnHistoryChipTapped`: "Point the STORE branch at ScreenId.StoreHistory
> once that screen ships." Decisions of record (Cesar, 2026-09-08): PRICE line = `charged_rp` RP,
> SOURCE line fixed to STORE; the ALL/TICKETS/CLUBS/CHARACTERS/BALLS/ITEMS chips ARE wired here
> (client-side filter); rows reuse the Gacha History row with the BagClubCard tile — NOT the
> Figma 180×274 art tile; **the scrollbar sits INSIDE the panel like every other scrollbar in the
> game**, not at the Figma's x=1138 outside the panel.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

The History chip on the Rewards Center STORE tab opens a **Store History** screen — the player's
own shop purchases, newest first, from the server — instead of the "Store history is coming
soon" toast. The screen is the Gacha History shell (tab strip, chip row, titled panel, paged
scroll, CLOSE) with STORE lit, a `STORE HISTORY` title, purchase rows (art tile + NAME / AMOUNT /
ACQUIRED / SOURCE / PRICE), and working category chips. Data comes from a new
`GET /api/v1/shop/history` over `golfin_shop_purchases`, mirrored to disk exactly the way
`GachaHistoryStore` mirrors `/gacha/history`, and prepended locally after a purchase so the log
is current without a refetch.

## Build playbook (Figma-node screens)

`Docs/Architecture/FIGMA_SCREEN_BUILD_PLAYBOOK.md` applies; its §7 self-diff is an acceptance
line. The deviations of record (scrollbar position, row tile, price/source content, chip row
wired) are listed in §Figma Fidelity and are NOT fidelity failures.

## Reference

- **Figma frame:** Golfin-Game-Redux / `Store History Screen ` / id `13509:2978`, file
  `5gEAHjl6xAtW8iYY7NMvWd` (render confirmed 1170×2532 from the render call's own
  `original_width/height`).
- **Reference PNG:** `reference/StoreHistory_13509-2978.png`.
- **Placeholder content:** the two GOLDEN TICKET rows, `$3.99`, `MARKETPLACE`, `2024/12/28` are
  mock. Redux has no USD price and no marketplace (`ECONOMY_MASTER` §2 — RP only).
- **Sibling built screen (ground truth for the shell):** `GachaHistoryScreen.prefab` as it
  renders in the game today — the Store History screen is that screen re-titled. Where the Figma
  and the built Gacha History shell disagree on chrome (scrollbar, tab strip metrics), the built
  shell wins.

## Figma Fidelity (Rule 18)

| Element | Figma node | Property → value |
|---|---|---|
| Tab strip GACHA / STORE / GIFTS | `13509:2985` | Same strip as Gacha History (`FiltersBlock/TabBar`). **STORE active gold `#FFD023`** (`GachaHistoryTabStrip.ActiveTabColor`), GACHA white, GIFTS 35 % white + non-interactable. |
| Chip row ALL … ITEMS | `13509:3008` | Same `FiltersBlock/CategoryRow` chips. ALL gold (`GeneralShopScreenController.ChipGold` `#EBD170`) by default; **wired** — decision of record. |
| History icon + title | `13509:3036`, `13509:3042` | Same `MainPanel/Title` block; label = `SHOP_HISTORY` "STORE HISTORY" through `LocalizedText`. |
| Separator under title | `13509:3043` | Unchanged from the cloned prefab. |
| Row (×N) | `13509:3045` | 978 wide, one per purchase, divider between rows (the existing `Divider` prefab). Col 1 = **BagClubCard tile** bound by `GachaPrizeCardBinder` (decision of record, replaces the 180×274 `Rarity Background` tile). Col 2 lines top→bottom: NAME (upper), `AMOUNT: n`, `ACQUIRED yyyy/MM/dd`, `SOURCE: STORE`, `PRICE: n RP`. Line 6 of the cloned row and **Col 3 (ticket chip) hidden**. |
| Scrollbar | `13509:3095` | **DEVIATION OF RECORD:** NOT at x=1138 outside the panel. Keep the cloned prefab's `MainPanel/…/Scrollbar` exactly where `GachaHistoryScreen.prefab` has it (inside the panel's right edge, `m_VerticalScrollbarSpacing: -3`, `m_VerticalScrollbarVisibility: 1`). Acceptance measures its rect inside `MainPanel`. |
| Empty list | — | Panel stays empty (no copy) — same as Gacha History before the first pull. Do not invent an empty-state string. |
| CLOSE | `13509:3091` | Same `CloseButtonArea` button; `GoBack(ScreenId.GeneralShop)`. |
| Side arrows | `13509:3093/3094` | Present in the node, absent from the built Gacha History shell → **not built**, same as Gacha History. |
| Top bar / nav bar | `13509:2981`, `13509:3092` | Shared bars via `ScreenManager` (the screen's own `TopUI` / `NavBarContainer` stay empty placeholders, as on GachaHistory). |

## Architecture context

- **Asmdefs:** `Golfin.Economy` (service + DTOs + endpoint), the UI assembly that holds
  `GolfinRedux.UI.Gacha` / `GolfinRedux.UI.Shop`, EditMode tests. Backend: playlife
  `backend/routers/shop.py` + `backend/tests/test_shop_purchase.py`.
- **Existing code this task reads before writing anything:**
  - `Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs` — the paged list (PageSize 12,
    RowsPerFrame 3, `PrependCount` discriminator, `PaintGate`, `UiSelection.FadeSwap`).
  - `Assets/Scripts/UI/Gacha/GachaHistoryStore.cs` — server read + disk mirror + `Prepend`.
  - `Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs` — the cloned strip (paths
    `GameScreenContent/ContentContainer/FiltersBlock/TabBar/{DailyTab,WeeklyTab,MonthlyTab}`).
  - `Assets/Scripts/UI/Gacha/GachaHistoryRow.cs` — `_metaLines[0..5]`, `_clubCard`,
    `_ticketLabel/_ticketIcon`; prefab `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab`
    (`Col2_Metadata/MetaLines/Line_*`, `Col3_Currency`).
  - `Assets/Scripts/UI/Gacha/GachaPrizeCardBinder.cs` — `Bind(GameObject cardGo, PrizeRecord)`
    binds ANY kind onto a nested `BagClubCard` (`BagClubCard.InitializePrize`).
  - `Assets/Scripts/UI/Gacha/GachaTabController.cs` — `OnHistoryChipTapped`, `_activeTab`,
    `RequestStoreTab()` / `RequestGachaTab()`.
  - `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` — `WireChip` / `RestyleChips` /
    `SetChipActive`, `ChipGold`/`ChipWhite`, `_activeCategory` (null = ALL); `ShopCategory` enum
    in `GeneralShopModel.cs` (`Club, Ball, Character, Item, Ticket`).
  - `Assets/Scripts/Economy/GachaPullService.cs` `FetchHistoryRoutine` (lines ~271–290) and
    `PointsDtos.cs` `GachaHistoryPage` — the exact shape to mirror.
  - `Assets/Scripts/UI/ScreenManager.cs` — `ScreenId`, `_gachaHistoryScreen`, the three
    `GachaHistory` switch sites (~488, ~532, ~747) and the shared-bars list (~831).
  - `Assets/Scripts/UI/Polish/LayeredPush.cs` (~136), `GameShimmerSites.cs` (~38/64),
    `PersistentUIManager.cs` (~860), `Assets/Scripts/UI/Polish/Editor/GamePolishBuilder.cs`
    (~76, ~380 ShimmerSite list).
  - playlife `backend/routers/gacha.py` `history()` (lines ~192–284) — keyset paging, `before`
    validation, `_missing_relation`, `next_before` only on a full page; and the five
    `test_history_*` tests in `backend/tests/test_gacha.py` (~243, ~467–515).
  - playlife `backend/migrations/2026_08_27_golfin_shop_purchase.sql` — `golfin_shop_purchases`
    columns: `id, user_id, entry_id, category, ref_id, amount, charged_rp, list_rp, on_sale,
    build, idempotency_key, grant_id, created_at`. **No migration in this task.**
- **Strings:** importer path, EN + JA, §5.

## Implementation

### 1. Backend — `GET /api/v1/shop/history` (playlife)

In `backend/routers/shop.py`, add `history()` as a verbatim structural copy of
`gacha.py::history()` with these substitutions:

- table `golfin_shop_purchases`, select
  `id, entry_id, category, ref_id, amount, charged_rp, list_rp, on_sale, build, created_at`,
  `.eq("user_id", user_id).order("created_at", desc=True).limit(limit)`, `.lt("created_at",
  before)` when given. One query — there is no nested table.
- `limit: int = Query(50, ge=1, le=200)`; `before` validated exactly as gacha does (400 naming
  the parameter on garbage).
- Response `{"data": {"purchases": [...rows...], "next_before": <oldest created_at | null>}}`;
  `next_before` only when `len == limit`.
- `_missing_relation` → `{"purchases": [], "next_before": None}` with a warning. Add a docstring
  line saying why this READ gets the courtesy the POST above deliberately refuses: "no purchases
  yet" is a real state every player starts in; "sold nothing" is not. Copy `_missing_relation`
  from `gacha.py` (module-local, same as `_parse_key` is).
- Auth via `Depends(get_current_user)`; user id from the token only.

Tests in `backend/tests/test_shop_purchase.py`, mirroring `test_gacha.py`:
`test_unauthenticated_history_is_403`, `test_history_is_scoped_to_the_caller`,
`test_history_is_newest_first_and_pages_by_before`, `test_history_next_before_only_on_full_page`,
`test_history_rejects_garbage_before_with_400`, `test_history_missing_table_is_empty`.

Deploy: `fly deploy` from playlife, `flyctl status`, then smoke with a real token:
`curl -H "Authorization: Bearer …" "$API/api/v1/shop/history?limit=5"` — paste the response in
the report (redact ids). No SQL to run — nothing changes in the schema.

### 2. Client — service, DTOs, endpoint (`Golfin.Economy`)

- `Endpoints.ShopHistory => BaseUrl + "/shop/history"` next to `ShopPurchase` (`Endpoints.cs`
  ~250).
- `PointsDtos.cs`: `ShopHistoryPage { [JsonProperty("purchases")] ShopPurchaseDto[] Purchases;
  [JsonProperty("next_before")] string NextBefore; }` and `ShopPurchaseDto { id, entry_id,
  category, ref_id, amount, charged_rp, list_rp, on_sale, build, created_at }` — snake_case
  `JsonProperty`s, `created_at` a **string** (raw ISO; the `RawDates` rule in `GachaHistoryStore`).
- `ShopPurchaseService.FetchHistoryAsync(int limit, Action<ShopHistoryPage> onDone)` +
  `FetchHistoryRoutine` — copy `GachaPullService.FetchHistoryRoutine` line for line:
  `PointsBackendFlag.Enabled` gate → null, clamp 1..200, `_client.Get<ShopHistoryPage>`, null on
  failure so the caller keeps what it has. Test in `ShopPurchaseServiceTests` with the fake
  client the purchase tests already use: success maps, failure → null, flag OFF → null with no
  request.

### 3. Client — record + store (`GolfinRedux.UI.Shop`, `Assets/Scripts/UI/Shop/`)

- `StoreHistoryRecord.cs` — plain DTO, public fields (tests reflect on them like
  `GachaHistoryRecord`): `ShopCategory Category; string RefId = ""; string EntryId = "";
  int Amount = 1; int ChargedRp; int ListRp; bool OnSale; string PurchasedUtc = "";`
  `Category` parsed from the row's `category` with `GeneralShopModel.ParseCategory`
  (`GeneralShopModel.cs:548`, currently `private static` — widen to `internal static`, no
  second parser). It returns `ShopCategory?`; a null (unknown string) logs a warning and skips
  the row rather than mislabelling it.
- `StoreHistoryStore.cs` — static, the `GachaHistoryStore` shape: `All` (disk mirror on first
  access, never the network), `OnChanged`, `Filter(Func<…,bool>)`, `Reload()`, `Refresh(Action?
  done = null)` → `ShopPurchaseService.Instance.FetchHistoryAsync(100, page => …)` mapping one
  record per purchase row (a purchase is already one row; no flattening), newest first,
  `WriteCache` atomic `.tmp` + replace to `persistentDataPath/store_history.json`, a null page
  keeps what it has. `Prepend(StoreHistoryRecord)` inserts at the head, mirrors, raises
  `OnChanged`. **Copy, don't generalise** — `GachaHistoryStore` stays untouched.
- **Prepend after a purchase:** in `ShopTransaction`, in the `case ShopPurchaseVerdict.Ok:`
  arm (`ShopTransaction.cs:372`), right after `rpm.SpendPoints(outcome.Charged)`, build a
  `StoreHistoryRecord { Category = entry.Category, RefId = entry.RefId, EntryId =
  entry.EntryId, Amount = 1, ChargedRp = outcome.Charged, ListRp = entry.RpCost, OnSale =
  outcome.Charged < entry.RpCost, PurchasedUtc = DateTime.UtcNow.ToString("o") }` and call
  `StoreHistoryStore.Prepend(record)`. (`ShopPurchaseResult` carries `charged` but no
  `created_at`; the server's own timestamp replaces the local one on the next `Refresh`.) This
  is the only change in `ShopTransaction`.

### 4. Client — screen

**Prefab.** Duplicate `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab` →
`Assets/Prefabs/UI/Shop/StoreHistoryScreen.prefab` (root renamed `StoreHistoryScreen`). Do not
rebuild the hierarchy; the only prefab edits are: (a) `MainPanel/Title` label → `LocalizedText`
key `SHOP_HISTORY`; (b) swap the root's `GachaHistoryScreenController` for
`StoreHistoryScreenController` and re-wire `_rowPrefab` / `_dividerPrefab` / `_scrollContent` /
`_closeButton`; (c) keep `GachaHistoryTabStrip` on the root and tick its new `_storeIsActive`
(below). **The Scrollbar, Viewport, Content, chip row, tab strip and CLOSE are not moved or
resized.**

**Row prefab.** Duplicate `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` →
`Assets/Prefabs/UI/Shop/StoreHistoryRow.prefab` with `StoreHistoryRow` replacing
`GachaHistoryRow`; wire `_clubCard` (the nested `BagClubCard`) and `_metaLines[0..5]`. `StoreHistoryRow.Bind(StoreHistoryRecord r)`:

```csharp
// Col1 — every kind on the same tile, through the one binder the reveal + Prizes + gacha log use.
var prize = new PrizeRecord(KindOf(r.Category), r.RefId, r.Amount, ResolveRarity(r), isDupe: false, dupeRp: 0);
GachaPrizeCardBinder.Bind(_clubCard.gameObject, prize);
// Col2
SetLine(0, ResolveName(r).ToUpper());
SetLine(1, string.Format(LocalizationManager.Get("SHOP_HISTORY_AMOUNT"),   r.Amount));
SetLine(2, string.Format(LocalizationManager.Get("SHOP_HISTORY_ACQUIRED"), DateOf(r.PurchasedUtc)));   // yyyy/MM/dd, RoundtripKind
SetLine(3, string.Format(LocalizationManager.Get("SHOP_HISTORY_SOURCE"),   LocalizationManager.Get("SHOP_HISTORY_SOURCE_STORE")));
SetLine(4, string.Format(LocalizationManager.Get("SHOP_HISTORY_PRICE"),    r.ChargedRp));
SetLine(5, "");                                  // unused line hidden (SetActive(false))
// Col3_Currency hidden — the Figma row has no chip; price is a text line.
```

`KindOf` maps `ShopCategory` → `PrizeRecord.Kind*` constants. `ResolveName` / `ResolveRarity`
use the same databases `GachaHistoryScreenController.ResolveName` uses (`ClubDatabaseCSV`,
`CharacterDatabaseCSV`, `ItemDatabaseCSV`, `TicketTypeCatalog`, plus the ball DB
`GachaHistoryRowBall` reads) — NOTE: check what `GachaPrizeCardBinder.BindOtherKind` expects in
`PrizeRecord.Rarity` for ball/item/ticket and match it; do not invent a rarity for kinds that
have none. `PrizeRecord`'s constructor signature is at `PrizeRecord.cs:22` — read it.

**Tab strip.** `GachaHistoryTabStrip`: add `[SerializeField] private bool _storeIsActive;`
and in `OnEnable` colour STORE gold / GACHA white when it is set (the inverse of today). Nothing
else changes; both strips keep routing through `RequestGachaTab()` / `RequestStoreTab()` +
`GoBack(ScreenId.GeneralShop)`.

**Controller.** `StoreHistoryScreenController` (`Assets/Scripts/UI/Shop/`) — the
`GachaHistoryScreenController` paging model copied with the same constants and comments trimmed
to a pointer ("paging rationale: see GachaHistoryScreenController"): `PageSize 12`,
`RowsPerFrame 3`, `_fill` cancel, `_renderedCount`, `_firstRenderedRecord` reference-identity
`PrependCount`, `NextPageEnd`, scroll-to-bottom append at `verticalNormalizedPosition <= 0.02`,
`PaintGate("[StoreHistory]", GameShimmerSites.StoreHistory)`, `RebuildList(PaintKind.Cache)` on
enable then `StoreHistoryStore.Refresh()`, `OnChanged → RepaintAnimated`, `FadeSwap` on rebuild,
`StaggerRise` page 1 only. **One row prefab** (`_rowPrefab`) — no per-kind switch. **Copy, don't
generalise; `GachaHistoryScreenController` and `GachaHistoryPagingTests` stay untouched** (that
controller is perf-tuned and its tests pin it). Make `PrependCount` / `NextPageEnd` `internal
static` again and cover them in `StoreHistoryPagingTests` (clone of `GachaHistoryPagingTests`).

**Chips (wired — decision of record).** In `Awake`, the `GeneralShopScreenController` pattern
against `GameScreenContent/ContentContainer/FiltersBlock/CategoryRow/{ALL,TICKETS,CLUBS,CHARACTERS,BALLS,ITEMS}Chip`
(confirm the path from the prefab — the chips exist there today, unwired): `_activeCategory`
(`ShopCategory?`, null = ALL) remembered for the app's lifetime like the shop's (nav_back_memory
F10 posture); tap → set, `RestyleChips()` (`ChipGold`/`ChipWhite` on `…Chip/Label`), then
`UiSelection.FadeSwap(this, ListGroup(), () => RebuildList(PaintKind.Repaint))` — this is the
§D3 site the gacha controller's comment says the chips should route through. The list source is
`_activeCategory == null ? StoreHistoryStore.All : StoreHistoryStore.Filter(r => r.Category ==
_activeCategory)`; `PrependCount` runs against the same filtered view. A repaint from a filter
change never shimmers (`PaintKind.Repaint`).

**CLOSE.** `ScreenManager.Instance.GoBack(ScreenId.GeneralShop)` — identical to Gacha History
(the remembered STORE tab is where the player came from).

### 5. Client — registration (the scene edit)

- `ScreenId`: add `StoreHistory` **at the END of the enum**. NOTE: `ScreenId` is serialized
  (`_backScreen` / `_returnTarget` fields); inserting mid-enum shifts every serialized value.
- `ScreenManager`: `[SerializeField] GameObject _storeHistoryScreen;` + the same four sites
  `GachaHistory` has (resolve ~488, pillar → `Screen.Gacha` ~532, `SetActive` ~747, shared-bars
  list ~831).
- `PersistentUIManager` ~860: `case ScreenId.StoreHistory: return "NAV_REWARDS_CENTER";`.
- `LayeredPush` ~136: `case ScreenId.StoreHistory` → the GachaHistory `Layers`.
- `GameShimmerSites.StoreHistory = "store.history"` + its entry in the all-sites list (~64) and
  the `GamePolishBuilder` `ShimmerSite` table (~380) so the builder places the shimmer block.
- `ShellScene.unity`: instantiate `StoreHistoryScreen.prefab` beside `GachaHistoryScreen`
  (inactive, same parent, same rect) and wire `ScreenManager._storeHistoryScreen`. Do it with a
  one-shot Editor menu item (`GOLFIN/Store History/Install screen` — the
  `GamePolishBuilder` `[MenuItem("GOLFIN/…")]` style) so the scene diff is the instance + one reference and nothing else;
  quote `git diff --stat Assets/Scenes/ShellScene.unity` in the report. Reconcile nothing else
  in the scene (the stale `MatchMakingModal` overrides the sim-loop notes warn about will try to
  ride along — revert them out).
- The Editor probes' `ScreenId → prefab name` switches (`GamePolishProbe` ~835,
  `GamePolishProbeC` ~1353, `GamePolishDemoRecorder` ~527): add `StoreHistory =>
  "StoreHistoryScreen"` if the switch would otherwise throw or skip; no new probe steps.

### 6. Client — the chip on the Rewards Center

`GachaTabController.OnHistoryChipTapped`: the `_activeTab != Gacha` arm becomes
`Store → ShowScreen(ScreenId.StoreHistory)`; the `Gifts` arm cannot fire (the tab is
`interactable = false`), so the toast line and the `SHOP_HISTORY_COMING_SOON` lookup are
deleted, leaving a `Debug.LogWarning` for Gifts. Update the summary comment above it. Keep the
`UiSelection.Bump` first.

### 7. Strings (importer path, EN + JA in one commit)

`Assets/Localization/LocalizationText.csv`:

| key | EN | JA |
|---|---|---|
| `SHOP_HISTORY` | `STORE HISTORY` | `ストア履歴` |
| `SHOP_HISTORY_AMOUNT` | `AMOUNT: {0}` | `数量: {0}` |
| `SHOP_HISTORY_ACQUIRED` | `ACQUIRED {0}` | `入手日 {0}` |
| `SHOP_HISTORY_SOURCE` | `SOURCE: {0}` | `入手元: {0}` |
| `SHOP_HISTORY_SOURCE_STORE` | `STORE` | `ストア` |
| `SHOP_HISTORY_PRICE` | `PRICE: {0} RP` | `価格: {0} RP` |

→ `python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN, read the
verdicts; **STOP on CONFLICTS**) → `--apply` → publish `texts` from the admin →
`export_content.py --check` clean. `SHOP_HISTORY_COMING_SOON` is retired: its code reference goes
in §6; the row itself is **deactivated by Cesar in the admin** (pipeline invariant I6 — never
delete from the CSV, the importer would re-append it). Title through a `LocalizedText` binder;
row lines through `LocalizationManager.Get` + `string.Format`. Zero new hardcoded `.text`
literals (grep quoted in the report).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Backend: six `test_history_*` tests pass; deployed (`flyctl status` pasted); live
  `GET /api/v1/shop/history?limit=5` on a signed-in account returns the account's purchases
  newest first, `next_before` null on a short page (response pasted, ids redacted); a garbage
  `before` is a 400 naming the parameter; unauthenticated is 403.
- [ ] Rewards Center STORE tab → History chip → Store History screen (no toast). GACHA tab →
  History chip → Gacha History, unchanged.
- [ ] Screen shell A/B against `GachaHistoryScreen` in play mode at 1170×2532: tab strip with
  STORE gold / GACHA white / GIFTS dimmed; title `STORE HISTORY`; chip row with ALL gold; CLOSE.
  Screenshot.
- [ ] **Scrollbar rect lies inside `MainPanel`'s rect** (dump both world rects; quote them).
  Not at the Figma's x=1138.
- [ ] Rows: one per purchase, newest first; Col 1 tile bound by `GachaPrizeCardBinder` for a
  club, a ball, a character, an item and a ticket (make one purchase of each kind on a test
  account; screenshot showing all five); Col 2 reads NAME / `AMOUNT: n` / `ACQUIRED yyyy/MM/dd` /
  `SOURCE: STORE` / `PRICE: n RP` with `n` = the row's `charged_rp` (verify one against SQL
  `select charged_rp from golfin_shop_purchases where id = …`); line 6 and Col 3 hidden.
- [ ] Buy something in the STORE, open Store History: the purchase is at the top BEFORE the
  server answers (prepend), and stays after (no duplicate once `Refresh` lands — the reference
  discriminator handles it exactly as on Gacha History; quote the `[StoreHistoryScreenController]
  prepend 1` log).
- [ ] Airplane mode → open Store History: the disk mirror draws, no error, no shimmer (cache
  paint); back online → server repaint fades in.
- [ ] Chips: tapping CLUBS shows only clubs, TICKETS only tickets, ALL restores; the chip tapped
  is gold; the list fades on change (no shimmer); leaving to the Rewards Center and returning
  keeps the chosen chip; a purchase made while CLUBS is active and of another kind does not
  appear until ALL / its chip.
- [ ] Paging: with > 12 purchases, the first page is 12 rows, scrolling to the bottom appends
  (quote the `append 12 -> 24 of N` log); the push into the screen stays inside the
  `game_polish_a` A13 gate (< 20 MB / < 50 ms worst frame over the push window — run the A13
  probe or the profiler and quote the numbers).
- [ ] `StoreHistoryPagingTests` + `ShopPurchaseServiceTests` history cases green; full EditMode
  sweep per assembly, no new failures.
- [ ] `ScreenId.StoreHistory` is the LAST enum member (quote the enum tail); `git diff --stat
  Assets/Scenes/ShellScene.unity` shows the instance + the one reference only.
- [ ] `export_content.py --check` clean for `texts`; the six keys present EN + JA; zero new
  hardcoded `.text` literals (grep quoted); `SHOP_HISTORY_COMING_SOON` no longer referenced in
  C# (grep quoted) — the admin deactivation is Cesar's step, listed in the report as pending.
- [ ] Playbook §7 self-diff against `reference/StoreHistory_13509-2978.png`, listing the four
  deviations of record as deviations, not failures.
- [ ] No white-box placeholders; all `[SerializeField]` refs wired; no Console errors related to
  this task; deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

**playlife (new/modified)**
- `backend/routers/shop.py` — `history()` + `_missing_relation`
- `backend/tests/test_shop_purchase.py` — six history tests

**GolfinRedux — new**
- `Assets/Scripts/UI/Shop/StoreHistoryRecord.cs`, `StoreHistoryStore.cs`, `StoreHistoryRow.cs`,
  `StoreHistoryScreenController.cs`
- `Assets/Scripts/UI/Shop/Editor/StoreHistoryInstaller.cs` (one-shot scene install menu item)
- `Assets/Prefabs/UI/Shop/StoreHistoryScreen.prefab`, `StoreHistoryRow.prefab`
- `Assets/Tests/EditMode/StoreHistoryPagingTests.cs`

**GolfinRedux — modified**
- `Assets/Scripts/Net/Endpoints.cs` (`ShopHistory`), `Assets/Scripts/Economy/PointsDtos.cs`
  (`ShopHistoryPage`, `ShopPurchaseDto`), `Assets/Scripts/Economy/ShopPurchaseService.cs`
  (`FetchHistoryAsync/Routine`), `Assets/Scripts/Economy/Tests/ShopPurchaseServiceTests.cs`
- `Assets/Scripts/UI/Shop/ShopTransaction.cs` (one `Prepend` call)
- `Assets/Scripts/UI/Gacha/GachaHistoryTabStrip.cs` (`_storeIsActive`),
  `GachaTabController.cs` (§6)
- `Assets/Scripts/UI/ScreenManager.cs`, `PersistentUIManager.cs`, `Polish/LayeredPush.cs`,
  `Polish/GameShimmerSites.cs`, `Polish/Editor/GamePolishBuilder.cs`, the three probe maps (§5)
- `Assets/Scenes/ShellScene.unity` (instance + one reference)
- `Assets/Localization/LocalizationText.csv` (+ regenerated `LocalizationTextTable.asset`)
- `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`

## Smoke evidence

Presentation feature — Lesson O applies. Screenshots at 1170×2532: (1) Store History with the
five kinds; (2) CLUBS chip active; (3) airplane-mode cache paint; (4) Gacha History side by side
for the shell A/B. Prose: what the scrollbar, chips and prepend visibly did. Console excerpts for
prepend / append / rebuild.

## Out of scope (do NOT do these — filed as Notion deferrals 2026-09-08)

- Purchases from any source other than the RP store (IAP, marketplace, admin grants). The
  SOURCE line is fixed to STORE; when a second source exists it becomes a field on the record.
- Keyset paging past the first 100 rows (`before` cursor on the client) — same 100-row posture
  as Gacha History; the endpoint already supports `before`.
- Wiring the chip row on **Gacha History** (still inert there) — a separate small spec, now with
  this screen's filter as the pattern.
- A sale marker (`on_sale` / `list_rp` are carried on the record but not rendered), an
  empty-state string, tapping a row to open the item, the Figma's side arrows.
- Generalising `GachaHistoryStore` / `GachaHistoryScreenController` into a shared base.
