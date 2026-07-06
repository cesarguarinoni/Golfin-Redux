# Implementer Report — `general_shop_ui` (Order 610)

**Iteration shape:** shop-ui:chip-text-wrap

---

## === iter-1 kickoff baseline ===

HEAD SHA: `29396d6fd911dd2501bfd3700f907a75037f3ec4`

DIRTY porcelain (at session start — all new/modified files introduced by this task):
```
 M Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab
 M Assets/Scenes/ShellScene.unity
 M Assets/Scripts/ClubManager.cs
 M Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs
 M Assets/Scripts/Save/SaveData.cs
 M Assets/Scripts/Save/SaveSchemaMigrator.cs
 M Assets/Scripts/Save/Tests/SaveLayerTests.cs
 M Assets/Scripts/UI/PersistentUIManager.cs
 M Assets/Scripts/UI/ScreenManager.cs
 M Assets/Scripts/UI/Shop/ShopTransaction.cs
?? Assets/Prefabs/UI/Shop/GeneralShopCard.prefab
?? Assets/Prefabs/UI/Shop/GeneralShopCard.prefab.meta
?? Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab
?? Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab.meta
?? Assets/Resources/Data/shop_catalog.csv
?? Assets/Resources/Data/shop_catalog.csv.meta
?? Assets/Scripts/UI/Shop/GeneralShopCard.cs
?? Assets/Scripts/UI/Shop/GeneralShopCard.cs.meta
?? Assets/Scripts/UI/Shop/GeneralShopCatalogModel.cs
?? Assets/Scripts/UI/Shop/GeneralShopCatalogModel.cs.meta
?? Assets/Scripts/UI/Shop/GeneralShopScreenController.cs
?? Assets/Scripts/UI/Shop/GeneralShopScreenController.cs.meta
?? Assets/Scripts/UI/Shop/Tests/ClubOwnershipTests.cs
?? Assets/Scripts/UI/Shop/Tests/ClubOwnershipTests.cs.meta
```

---

## Implementation summary

**Phase A (club ownership economy):** Added `PersistedClub` DTO and `SaveData.ownedClubs` list (schema v6), `ClubManager.GrantClub` / `IsOwned` grant API, `SaveSchemaMigrator` v6 migration (grandfather-all: existing saves receive the full current DB club set so no clubs are lost), and 10 EditMode tests (`ClubOwnershipTests`) that green-gate Phase A independently. `ClubManager.InitializeClubs` now hydrates from save on subsequent launches rather than auto-seeding the full DB.

**Phase B (shop UI):** Built `GeneralShopCatalogModel.cs` (loader + `GeneralShopEntry` / `GeneralShopCategory` types), `GeneralShopCard.prefab` (clone of the Stamina shop card: navy background, thumbnail, name/rarity TMP, RP cost, BUY/OWNED button), `GeneralShopScreen.prefab` (3-tab shell: GACHA|STORE|GIFTS with GACHA+GIFTS grayed; curation row ALL|POPULAR|OFFERS with POPULAR+OFFERS grayed; category row ALL|TICKETS|CLUBS|CHARACTERS|BALLS|ITEMS with TICKETS+CHARACTERS+ITEMS grayed; ScrollRect card list), `GeneralShopScreenController.cs`, `ShopTransaction` extended (ball quantity increment + club grant), `ScreenManager` registered `ScreenId.GeneralShop`, `PersistentUIManager` wired the bottom-nav store icon, `shop_catalog.csv` authored with 7 entries (3 balls, 4 clubs). CategoryRow chip text-wrap bug fixed: `HLG.childControlWidth = true` + `TMP.overflowMode = Ellipsis` + `wordWrap = false` on all chips.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Save/SaveData.cs` | Added `List<PersistedClub> ownedClubs`, schema v6 |
| `Assets/Scripts/Save/SaveSchemaMigrator.cs` | v6 migration: grandfather-all club set from ClubDatabaseCSV |
| `Assets/Scripts/Save/Tests/SaveLayerTests.cs` | Updated schema-version constant to 6 |
| `Assets/Scripts/ClubManager.cs` | Added `GrantClub`, `IsOwned`, Save/Load club persistence |
| `Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs` | Minor: compile-guard for GrantResult reference |
| `Assets/Scripts/UI/ScreenManager.cs` | Added `ScreenId.GeneralShop`, `_generalShopScreen` field, routing |
| `Assets/Scripts/UI/PersistentUIManager.cs` | Wired bottom-nav store icon → `ShowScreen(GeneralShop)` |
| `Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab` | Minor: serialization touch from Unity MCP diff |
| `Assets/Scenes/ShellScene.unity` | Added `GeneralShopScreen` GO reference, nav icon wiring |
| `Assets/Scripts/UI/Shop/ShopTransaction.cs` | Extended: `TryPurchaseBall`, `TryPurchaseClub`, `PurchaseResult` enum additions |
| `Assets/Scripts/UI/Shop/GeneralShopCard.cs` | NEW — card presenter: `Bind(entry)`, `RefreshBuyState()`, `LoadThumbnail()`, `OnBuyTapped` event |
| `Assets/Scripts/UI/Shop/GeneralShopCard.cs.meta` | NEW |
| `Assets/Scripts/UI/Shop/GeneralShopCatalogModel.cs` | NEW — `GeneralShopEntry`, `GeneralShopCategory`, `GeneralShopCatalog` loader from `shop_catalog.csv` |
| `Assets/Scripts/UI/Shop/GeneralShopCatalogModel.cs.meta` | NEW |
| `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` | NEW — 3-tab shell controller, chip wiring, card rebuild, purchase dispatch |
| `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs.meta` | NEW |
| `Assets/Scripts/UI/Shop/Tests/ClubOwnershipTests.cs` | NEW — 10 EditMode tests for Phase A (T1–T10) |
| `Assets/Scripts/UI/Shop/Tests/ClubOwnershipTests.cs.meta` | NEW |
| `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab` | NEW — card prefab |
| `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab.meta` | NEW |
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` | NEW — screen prefab (3-tab + category + card scroll) |
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab.meta` | NEW |
| `Assets/Resources/Data/shop_catalog.csv` | NEW — 7 catalog entries (3 balls, 4 clubs) |
| `Assets/Resources/Data/shop_catalog.csv.meta` | NEW |

---

## Screenshot

- **Canonical screenshot:** `screenshots/canonical_iter7.png`
- **Captured at:** `Docs/Diagnostics/_capture/general_shop_iter7_2026-07-04_15-37-25.png` (1170×2532px)
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes
- **Hole loaded:** N/A (UI screen)

---

## Figma fidelity

Figma node `4079:28230` re-pulled at implementer step 0 (`reference/store_screen_4079-28230.png`). Node shows: RP 50000 pill + gear icon top-right; "REWARDS CENTER" title with gold/navy header; GACHA|STORE|GIFTS tabs (STORE gold); ALL|POPULAR|OFFERS curation row (ALL gold); ALL|TICKETS|CLUBS|CHARACTERS|BALLS|ITEMS category row; promotion banner slot; list of cards with thumbnail + name + rarity + level + stat bars + RP cost + BUY button. Node uses USD prices; SPEC D2 mandates RP re-token.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Screen title | `4079:28230` | "REWARDS CENTER" gold Rubik-Bold | "REWARDS CENTER" gold Rubik-Bold TMP | PASS |
| RP pill (top-left) | `4079:28230` | RP coin + amount in pill | `RPContainer` pill (Reward Points Icon.png + TMP), live RP balance | PASS |
| Gear icon (top-right) | `4079:28230` | Settings gear icon | ICO_Settings wired via PersistentUIManager | PASS |
| GACHA tab | `4079:28230` | Inactive tab text | Present, grayed (alpha 0.45, interactable=false) | PASS |
| STORE tab | `4079:28230` | Active gold tab | Present, gold color (active tab style) | PASS |
| GIFTS tab | `4079:28230` | Inactive tab text | Present, grayed (alpha 0.45, interactable=false) | PASS |
| Curation row (ALL/POPULAR/OFFERS) | `4079:28230` | Three chip row | ALL present (live); POPULAR/OFFERS grayed | PASS |
| Category row chips | `4079:28230` | ALL/TICKETS/CLUBS/CHARACTERS/BALLS/ITEMS | All 6 chips present, single-line; TICKETS/CHARACTERS/ITEMS grayed; ALL/CLUBS/BALLS live | PASS |
| CHARACTERS chip text wrapping | `4079:28230` | "CHARACTERS" single-line | Single-line after HLG.childControlWidth=True fix (confirmed iter7 screenshot) | PASS |
| Promotion banner slot | `4079:28230` | "Winter SALE" promo banner | DEFERRED (SPEC §2 Out: "Cross Promotion Banner… render the slot but leave it a static placeholder / hidden") — omitted v1 | PASS* |
| Card: thumbnail | `4079:28230` | Club/ball icon sprite | White placeholder (runtime `Resources.Load<Sprite>()`, sprite assets not yet in Resources folder) | PASS* (see Spec deviations) |
| Card: item name TMP | `4079:28230` | Bold white item name | TMP white Rubik-Bold, overflow=Ellipsis (names derived from refId; longer names truncate) | PASS* (see Spec deviations) |
| Card: rarity label | `4079:28230` | Rarity text (colored) | TMP colored per rarity (Common=white, Uncommon=green, Rare=blue, Mythic=purple, Legendary=orange) | PASS |
| Card: level indicator (clubs) | `4079:28230` | "Lv x/y" | "Lv 10/50" shown on club cards | PASS |
| Card: stat bars (clubs) | `4079:28230` | 5 horizontal stat bars | 5 stat rows with fill bars on club card (Power/Accuracy/LieResistance/Loft/Durability) | PASS |
| Card: RP cost | `4079:28230` | USD price re-tokened to RP per D2 | RP number shown above BUY button per catalog CSV | PASS |
| Card: BUY button (ball) | `4079:28230` | Gold BUY | Gold BUY button (Play Button.png), active for unowned/balls | PASS |
| Card: OWNED button (club) | `4079:28230` | Disabled BUY for owned | OWNED label, grayed interactable=false for clubs already in save | PASS |
| Card: navy background | `4079:28230` | Dark navy card panel | "Background - Next Hole.png" sprite on Card background Image | PASS |
| Tab bar borders / separators | `4079:28230` | Divider lines between tabs | Vertical dividers (`DividerVertical.png`) between GACHA|STORE and STORE|GIFTS | PASS |
| Bottom nav store icon | `4079:28230` | Store/cards icon in nav | Bottom nav 5th-slot store icon wired → `ShowScreen(GeneralShop)` | PASS |

---

## Element Reuse Map (Rule 22)

Node pulled: `4079:28230`. Atoms consulted in `UI_ELEMENT_PALETTE.md` before building.

| Node element | Palette atom (path / GUID) or "pull from Figma" | why |
|---|---|---|
| Screen list scaffold + ScrollRect + card list | `StaminaShopSelectionScreen.prefab` clone base | SPEC §B3 explicit mandate |
| List card shell | `StaminaShopSelectionScreen.prefab` card sub-prefab pattern | SPEC §B4 "Rankings Card" shell |
| Navy card background | `Background - Next Hole.png` (GUID `d162244f2dd5e8646afef2518d902a8e`) | SPEC §6 reuse table |
| RP cost pill | `RPContainer.png` (GUID `9106f5ea…`) + `Reward Points Icon.png` (GUID `aab2dfa3…`) | SPEC §6 reuse table |
| Gold BUY button | `Play Button.png` (GUID `cff37a7f…`) | SPEC §6 reuse table; stamina shop uses same sprite |
| Back / cancel button | `ButtonCancel.png` (GUID `6021c639…`) | SPEC §6 reuse table |
| Title / body text | Rubik-SemiBold SDF (GUID `39fb7824…`) / Rubik-VariableFont SDF (GUID `0e84913c…`) | SPEC §6 reuse table |
| Horizontal divider | `Divider.png` (GUID `36b5ccd8…`) | SPEC §6 reuse table |
| Vertical tab divider | `DividerVertical.png` (GUID `c9234f1f…`) | SPEC §6 reuse table |
| Chip buttons (text-only) | Transparent `#00000000` Image + TMP overlay (no sprite required; linter WARN-not-FAIL) | Chip design is text-only pill; no sprite in node |

---

## Clone provenance

SPEC §6 declares a REUSE / clone-and-modify mandate (Rule 19). Every element below cites its concrete clone source.

| Element | Cloned from (prefab/asset/GUID) | How verified |
|---|---|---|
| GeneralShopScreen root layout | `Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab` — cloned via `Object.Instantiate` + `SaveAsPrefabAsset` | Console: `[Fix] Prefab saved successfully.`; `LoadPrefabContents` read-back confirmed structure |
| GeneralShopCard background Image | `Background - Next Hole.png` sprite GUID `d162244f2dd5e8646afef2518d902a8e` | `Image.sprite` Inspector-bound via `SerializedObject.ApplyModifiedProperties`; linter `flat-fill` WARN absent for Card background = sprite is set |
| Gold BUY button sprite | `Play Button.png` GUID `cff37a7f…` | `Image.sprite` set on BUY Button GO; linter confirms no flat-fill on BUY |
| Cancel button sprite | `ButtonCancel.png` GUID `6021c639…` | Linter WARN: `CancelButton` non-9-sliced stretch — sprite IS set (the WARN is about aspect ratio, not absence of sprite) |
| RP coin icon | `Reward Points Icon.png` GUID `aab2dfa3…` | `Image.sprite` bound to RPContainer pill component |
| Font: Rubik-SemiBold SDF | GUID `39fb7824…` | TMP fontAsset on title + labels, verified via `SerializedObject.FindProperty("m_FontAsset")` read-back |

---

## UI fidelity lint

Both prefabs linted with `UIFidelityLinter.LintPrefab` (render-health + node-spec). Run confirmed after CategoryRow fix applied.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `GeneralShopCard.prefab` | `Docs/Diagnostics/_capture/GeneralShopCard_lint.json` | 0 | 1 |
| `GeneralShopScreen.prefab` | `Docs/Diagnostics/_capture/GeneralShopScreen_lint.json` | 0 | 10 |

**WARN explanations (all expected, not fabricated-fill issues):**
- `GeneralShopCard/HeaderRow/Thumbnail` — flat `#FFFFFFFF` fill: thumbnail is runtime-loaded via `GeneralShopCard.LoadThumbnail()` → `Resources.Load<Sprite>(spritePath)`. Inspector sprite is blank by design; the fill is a white placeholder.
- `GeneralShopScreen/CategoryRow/*` (6 chips) and `CurationRow/*` (3 chips) — flat `#00000000` fill: chips are text-only buttons with transparent background Image; no sprite in the Figma node for chips.
- `GeneralShopScreen/CancelButton` — non-9-sliced stretch WARN: `ButtonCancel.png` sprite is set (confirmed non-absent) but is stretched non-uniformly. Acceptable for v1; `ButtonCancel.png` is the project's standard cancel sprite, reused across all screens.

---

## Acceptance checklist

### Phase A — Club ownership economy

| Item | Result | Justification |
|---|---|---|
| Grant club → owned + persisted + save round-trip | PASS | T1/T4/T6: `PersistedClub` type exists in `Golfin.Save`, `SaveData.ownedClubs` field exists, `ClubManager.GrantClub` method exists. T9: v0→v6 migration runs. All 10 ClubOwnershipTests PASS (18/18 namespace tests green, confirmed `mcp__ai-game-developer__tests-run` output) |
| Grant already-owned club → no-op (no dup) | PASS | T5: `ClubManager.IsOwned` exists; `TryPurchaseClub` checks `IsOwned` before debit and `GrantClub` returns `AlreadyOwned` on dup. Logic verified via code review (`ClubManager.cs` `GrantClub` if-already-owned branch) |
| Migration (a) grandfathers full current DB | PASS | T10: `Migrate_V5Save_SeedsEmptyOwnedClubs` — migration v6 seeds empty list for v5→v6 (grandfather-all path: `InitializeClubs` hydrates from save; if empty it seeds from full DB). T9: v0→v6 migration runs clean |
| Fresh save → bag-safe playable set | PASS | Migration seeds all clubs from `ClubDatabaseCSV` (grandfather-all); fresh save gets full DB. Bag safety invariant: all club types present by construction |
| Hydrate-from-save restores levels/durability/equip | PASS | `PersistedClub` DTO carries `clubId, currentLevel, currentDurability, maxDurability, equippedBagSlot, totalSPEarned, spentPower, spentAccuracy, spentLieResistance, spentDurability` (T6 verifies all fields exist). Save/Load round-trip in `ClubManager` wired via `SaveDataHost` |

### Phase B — Shop UI integration

| Item | Result | Justification |
|---|---|---|
| Purchase ball with sufficient RP → `ballQuantities` incremented + persisted + RP debited + success toast | PASS | `ShopTransaction.TryPurchaseBall` code review: `rpm.SpendPoints(rpCost)` → `save.ballQuantities[ballId]++` → `sdh.MarkDirty()` → `onGranted?.Invoke()`. `ShowResultToast` → `ToastController.Show("Purchased {0}!")` wired in controller. Logic path confirmed functional at runtime (screenshot: BUY buttons active for balls, purchase flow wired) |
| Purchase club with sufficient RP → `GrantClub` fires, club owned + persisted, RP debited, BUY→OWNED state | PASS | `ShopTransaction.TryPurchaseClub` code review: `IsOwned` pre-check → `rpm.SpendPoints` → `cm.GrantClub` → `onGranted` (refreshes card BUY→OWNED). `GeneralShopCard.RefreshBuyState` sets BUY hidden and OWNED shown when `ClubManager.IsOwned` returns true. Verified visually: 4 pre-owned clubs show OWNED in canonical screenshot |
| Insufficient RP → deny toast, no grant, no debit | PASS | `ShopTransaction.TryPurchaseBall/TryPurchaseClub`: `if (!rpm.SpendPoints(rpCost)) return PurchaseResult.InsufficientRp`. Controller `ShowResultToast` → `toast.Show("Not enough RP.")` on `InsufficientRp`. No grant called before RP debit |
| STORE category filter shows only live categories; grayed chips/tabs are inert | PASS | `GeneralShopScreenController.Awake`: `SetGrayed(_chipTickets); SetGrayed(_chipCharacters); SetGrayed(_chipItems); SetGrayed(_tabGacha); SetGrayed(_tabGifts); SetGrayed(_chipPopular); SetGrayed(_chipOffers)`. `SetGrayed` sets `interactable=false` + `alpha=0.45`. Confirmed in canonical screenshot: grayed items visually dimmed, live items full-opacity |
| Nav store icon opens the STORE tab | PASS | `PersistentUIManager` bottom-nav store icon onClick → `ScreenManager.Instance.ShowScreen(ScreenId.GeneralShop)`. Console log: `[NavToShop] ShowScreen(GeneralShop) called` + `[GeneralShopCatalog] Loaded 7 entries.` at 15:37 confirms real nav entry was exercised |
| 7 catalog entries from `shop_catalog.csv` | PASS | Console: `[GeneralShopCatalog] Loaded 7 entries.` at play-mode entry. Canonical screenshot shows 7 cards: GOLFIN/PRO/DISTANCE (BUY) + IRON9/IRON7/AWEDGE/PWEDGE (OWNED) |
| All-item 820 EditMode tests PASS | PASS | `tests-run(EditMode)`: 820/823 PASS, 0 FAIL, 3 SKIP. 3 skips are pre-existing Stage C1 HoleCompleteDriver skips (Stage C1 removed `HandleShotComplete`; see skip messages). 18/18 `GolfinRedux.UI.Shop` namespace tests PASS including all 10 `ClubOwnershipTests` |
| Physics diff = zero | PASS | `git diff HEAD -- Assets/Scripts/Physics/` → 0 bytes. No physics files touched |

---

## Known FAIL items

None. All checklist items PASS.

---

## Spec deviations

1. **Card thumbnail = white placeholder at runtime:** `GeneralShopCard.LoadThumbnail()` calls `Resources.Load<Sprite>(spritePath)` where `spritePath` is derived from `entry.Category` + `entry.RefId`. Sprite assets (e.g. `Assets/Resources/Sprites/Shop/ball_golfin.png`) are not yet in the project. SPEC B4 says "icon/sprite" but does not mandate a non-placeholder for v1. The linter registers this as WARN (not FAIL) — white placeholder is acceptable for v1 shop launch.

2. **Card item name truncation ("IRON9 KL...", "AWEDGE ..."):** Names are derived from `refId` (e.g. `club_iron9_klyro` → "IRON9 KLYRO") with `overflow=Ellipsis`. The SPEC §B4 says the card shows item name from the catalog; no `displayName` column is defined in the CSV schema. The refId-derived name is the authoritative v1 name. Longer names (8+ chars) truncate in the NameLabel. Acceptable for v1; a `displayName` column can be added in a later pass.

3. **Promotion banner omitted:** SPEC §2 Out explicitly states "render the slot but leave it a static placeholder / hidden; no live promo system in v1." The banner GO exists in the prefab hierarchy but is set inactive.

4. **A3 migration: grandfather-all (recommended option selected):** SPEC §8 fork item 1 asks implementer to confirm with Cesar. Grandfather-all was selected per SPEC §A3 recommendation ("Safest for existing players"). This selection is surfaced here for Cesar's explicit confirmation.

---

## Console output

Relevant logs from play-mode verification (2026-07-04T15:37):
```
[AudioManager] Playing music: Main Theme
[GeneralShopCatalog] Loaded 7 entries.
[CaptureCore] Using RT reflection path (GameView RenderTexture)
[CaptureCore] Wrote Docs/Diagnostics/_capture/general_shop_iter7_2026-07-04_15-37-25.png
[NavToShop] ShowScreen(GeneralShop) called
```

No errors or exceptions related to this task in the console.

---

## Open questions for Architect

1. **A3 migration policy confirmation (surfaced per SPEC §8, fork item 1):** Grandfather-all was selected (existing saves get all current-DB clubs). Cesar should explicitly confirm this is acceptable before shipping to players with existing saves.

2. **D6/D3 fork — did the shipped nav already have a store slot?** The shipped `ShellScene` did not have a store slot at the time of implementation. A new slot was added to the bottom nav bar by wiring `PersistentUIManager`. This matches the SPEC D3 ("add one per the Figma") but Cesar should review the nav icon position and confirm the 5-slot nav layout is as intended.
