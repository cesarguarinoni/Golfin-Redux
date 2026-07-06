# general_shop_ui — WIP status (paused 2026-07-06)

**Where it stands:** the original subagent build was reverted (fabricated provenance / built-from-scratch — see `CESAR_REJECTION.md` + `Docs/Reports/POSTMORTEM_general_shop_ui_fabricated_provenance.md`). The screen was then rebuilt LIVE on the main thread from real cloned atoms. Paused here to work on `pipeline_verification_gates`; resume from this note.

## DONE + Cesar-approved
- `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` — true clone of `TournamentSelectionScreen` (tab strip, navy panel, scroll list + scrollbar). BG = Figma-extracted `Assets/Art/Shop/Background - Rewards.png`. Tabs GACHA│STORE(gold)│GIFTS.
- `Assets/Prefabs/UI/Shop/GeneralShopCard.prefab` — true clone of `TournamentSelectionCard`.
- **The card** (Cesar: "Perfect") — measured 1:1 to Figma node `13509:3214`: 978×274, rounded-left rarity tile (`Resources/Rarities/*`), club portrait (`Resources/Clubs/Portraits/*`), one-line header (name│rarity│Lv), distance row, stat bars, compact two-tone price box (RP coin + non-bold number, struck original / sale), gold BUY.
- **Clubs** = continuous rounded pill bars (`S_PillStadium`, ppu 13). **Balls** = segmented bidirectional bars (matching `BallDetailPanel`/`BallSegmentedBar`) with real ball stats (Power/Rebound/WindCut/Roll/Spin, −10..+10).
- 4 sample cards hand-bound (2 clubs, 2 balls) for visual fidelity.

## DONE since resume (2026-07-06)
1. ✅ **Two filter rows** — curation (ALL│POPULAR│OFFERS) + category (ALL│TICKETS│CLUBS│CHARACTERS│BALLS│ITEMS). Cloned from the approved TabBar (BackgroundTabs pill + Rubik-SemiBold labels + DividerVertical). Node `4079:28230`: h44, 20px, ALL gold `#EBD170`, dead chips gray `#818EA1` (D6), live chips (CLUBS/BALLS) white. Inserted as a gap-12 FilterGroup between TabBar and the navy panel. Self-verified 1:1 (`screenshots/screen_iter2.png`).
2. ✅ **Winter SALE banner** — Figma node `4077:637` exported (`Assets/Art/Shop/Banner - Winter Sale.png`, 978×252, rounded-20), first item in the card list. ⚠️ **Art is a watermarked Adobe Stock preview** (`Stock #185667077`) — cropped/cleaned for the placeholder; **needs a licensed replacement before ship** (same status as 517's art-final note). Slot is per-SPEC a static placeholder (no live promo system v1).
4. ✅ **Phase A — club-ownership economy** (SPEC §4, save-schema change). `PersistedClub` DTO + `SaveData.ownedClubs`/`clubOwnershipSeeded`/`grandfatherClubs`; pure `ClubOwnershipService` (seed/grant/bag-safety, EditMode-testable in `Golfin.Save`); migrator **v5→v6** with the **grandfather-all** signal (Cesar's choice); `ClubManager` rewritten to seed-or-hydrate + `GrantClub` (A5) + persist on equip/level/repair. 10 new `ClubOwnershipTests` + 3 version-pinned tests updated (Save + Stamina suites). **Full EditMode gate 819 pass / 0 fail / 3 pre-existing skips.**

## Phase B — IN PROGRESS
### ✅ Data foundation done + verified (2026-07-06)
- `Assets/Resources/Data/shop_catalog.csv` — 5 entries (4 clubs + 1 ball; only 2 balls exist and `ball_golfin` is the unlimited default). Columns: entryId, category, refId, rpCost, saleRpCost, sortOrder, popular, offer.
- `GeneralShopModel.cs` — `ShopCatalogEntry` + `ShopCategory` + `GeneralShopCatalog` loader (mirrors 517 `ShopCatalog`). `HasSale`/`EffectiveRpCost` logic verified via live script-exec (4 clubs / 1 ball, prices correct).
- `ShopTransaction.TryPurchaseCatalogEntry` (B5) — RP-spend → dispatch **ball**→`SaveData.ballQuantities` (−1 unlimited respected, cap 99) / **club**→`ClubManager.GrantClub`. Existence + owned + manager pre-checks run BEFORE spend, so the grant is guaranteed and there is **no refund path** (avoids EarnPoints' leaderboard/SFX side effects). New `GeneralPurchaseResult` enum {Success, InsufficientRp, AlreadyOwned, Invalid}. Compiles clean.

### 🔎 Finding that shapes the next pass (2026-07-06)
The card-binding controller is a **PLAY-MODE** task, not edit-mode:
- `ClubDatabaseCSV.Instance` / `BallDatabaseCSV.Instance` are **null in edit mode** (runtime singletons; load on Awake in play mode). A data-driven `Bind()` reads them, so it can only run/verify in play mode.
- The approved cards encode stat value by **Fill RectTransform width (clubs)** and **per-segment brightness (balls)** — NOT `fillAmount` (all clubs read `fill=1`). Those values were hand-set, so the bind must reproduce the width/segment mapping from real DB stats.
- ⇒ The card-binding + controller + nav + BUY wiring + the real-play capture are ONE play-mode-integrated pass (the capture IS the verification). Committed backend (Phase A + Phase B data) already de-risks the economy/purchase logic underneath it.

### ✅ Card-binding controller done + verified live (2026-07-06, play mode)
- `GeneralShopCard.cs` — data-only bind on the approved card structure (Find-by-path; never touches layout). Club continuous bars = Fill width @331px/60-unit (durability=cur/max); ball segmented bars = 21 cells (+V→R0..R(V-1), −V→innermost left). Clean club portrait (`portraitSprite`), rarity tile from `Resources/Rarities/{Rarity}`, price with sale strikethrough, BUY→OWNED for owned clubs (B6). Long-name overlap fixed via `ConstrainName()` (242px box + auto-shrink + ellipsis).
- `GeneralShopScreenController.cs` — loads catalog, instantiates the right template per entry (banner stays atop), filters by active category chip (ALL/CLUBS/BALLS live), BUY→`TryPurchaseCatalogEntry`. Chips got Button+ButtonPressFeedback.
- Templates extracted to `Resources/Prefabs/Shop/GeneralShopCard_{Club,Ball}.prefab`; hand-set cards removed from the screen prefab (now runtime-generated).
- **Verified live** (`screenshots/play_cards_iter3.png`): 5 cards generate with real DB data; DRIVER G&F shows **OWNED** (live starter-seed working); **purchase proven** — RP 81345→79845 (−1500 sale price), iron9 owned False→True, 2nd buy AlreadyOwned + no double-charge.
- ⚠️ Dev-save note: the purchase test left `club_iron9_klyro` owned + 1500 RP spent on the dev save (valid game state, not corruption).

### ✅ Task 5 — nav entry DONE + verified live via REAL player path (2026-07-06)
- `ScreenId.GeneralShop` + `ScreenManager` registration (field `_generalShopScreen`, activation, isMenuScreen + **showBars** — the showBars line needed a separate edit; different indentation from isMenuScreen meant the first replace missed it). `GeneralShopScreen` instantiated under `Canvas/ScreensRoot` (inactive), SM field wired.
- **Nav slot (fork #6):** the bottom-nav **Gacha button was a dead no-op** (gacha unimplemented). Wired it → `ShowScreen(GeneralShop)` (`PersistentUIManager.NavigateTo` Gacha case). Non-destructive; forward-compatible (the hub's GACHA tab is the future gacha entry). Top-bar title "REWARDS CENTER" + Gacha nav highlight added to `HighlightScreen`.
- **Real-path capture** (`screenshots/shop_nav_final.png`): booted ShellScene → clicked the real Gacha nav button → Rewards Center opens with persistent top bar (RP pill + REWARDS CENTER title above the tabs), filter rows, banner, 5 data-driven cards. Clicked A.WEDGE's **real BUY button** → 'BUY'→'OWNED', club granted, RP 79,845→76,845 (−3000). Full nav→shop→buy path proven.
- **Clone-artifact bug caught by the real capture** (isolated renders hid it): the cloned screen still carried `TournamentSelectionScreenController` (populated 6 tournament cards + destroyed the banner when the tournament backend was live) and the card templates carried a stray `TournamentSelectionCard`. Both stripped from the prefabs. Also fixed `ContentArea` posY 36→−19 (clone diverged from sibling) so the tab bar clears the persistent title.
- ShellScene diff = **+125/−0** additive (guardrail: no existing object's active-state flipped).

## FEATURE COMPLETE — open items for Cesar
- **Nav-slot choice (fork #6):** Gacha button opens the Rewards Center. Bless or redirect (dedicated store icon / different slot).
- **Banner art:** watermarked Adobe Stock placeholder — needs a licensed replacement before ship.
- **Not yet committed:** the whole UI layer (screen + card prefabs + scripts + ShellScene registration + shop art) is uncommitted, pending Cesar's OK to commit.
- Minor/optional: club bar fill is flat blue (Figma has a subtle gradient).

### ⚠️ Data-model friction to note (ball rarity)
`BallDataRuntime` has **no rarity field** (Balls.csv: power/rebound/wind/roll/spin only), but the card design shows a rarity tile + rarity label. Default plan: add an optional `rarity` display column to `shop_catalog.csv` used for ball cards (clubs keep authoritative DB rarity); ball tile+label render from it. No change to Balls.csv. Flag for Cesar in case he wants balls shown differently (e.g. brand instead of rarity).

## Open decisions still needing Cesar (Phase B forks, low-risk defaults chosen above)
- RP price authoring per catalog row (design values, not $-derived).
- Already-owned club UX (default: disabled BUY + "OWNED" label).
- Ball catalog scope (which `ball_*` ids for sale; stacking vs `-1` unlimited).
- Banner art: watermarked placeholder → licensed replacement.

## Fidelity method to reuse on resume
Measure the node → build to px → crop built element at exact bounds (`camera.WorldToViewportPoint` on the card `RectTransform` corners) → 1:1 overlay vs node render → drive deltas to ~0 → THEN surface. (Memory `feedback_measure_and_selfverify_before_showing`.)

STATUS.md first line kept as `CESAR_REJECTED` only as a pipeline placeholder; the card is approved — the accurate state is "WIP paused, card approved, screen incomplete" per this note.
