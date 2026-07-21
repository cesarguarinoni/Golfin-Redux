# UI_HIERARCHY.md — Scene UI Paths Reference

> Quick-reference for all established UI hierarchies.
> Use this in TellCode specs instead of spelling out full paths each time.
> Updated: 2026-03-26

---

## Roster Screen (Character System)

Root: `RosterScreen/CarouselSection/DetailPanel`

### DetailPanel (CharacterDetailPanel)
```
DetailPanel
├── LeftPanel
│   └── Character                              (Image — full-body portrait)
├── RightPanel
│   ├── CharacterNamePanel
│   │   └── CharacterNameText                  (TMP)
│   ├── RarityPanel/RarityRow
│   │   ├── RarityText                         (TMP — child 0)
│   │   └── LevelPanel
│   │       ├── LevelText                      (TMP — child 1)
│   │       └── LevelTextMax                   (TMP — child 2)
│   ├── CharacterStatsPanel
│   │   ├── CharacterStats1                    (Strength)
│   │   │   ├── Name+Bar
│   │   │   │   ├── StatsName                  (TMP)
│   │   │   │   └── Bar                        (Image — fill bar)
│   │   │   ├── DiffLabel                      (TMP — compare diff, hidden by default)
│   │   │   └── StatNumber                     (TMP)
│   │   ├── CharacterStats2                    (Club Control) — same children
│   │   ├── CharacterStats3                    (Recovery) — same children
│   │   └── CharacterStats4                    (Stamina) — same children
│   ├── ButtonsPanel
│   │   ├── LevelUpButton                      (Button)
│   │   └── BoostButton                        (Button)
│   ├── CompareButton                          (Button — direct child of RightPanel)
│   ├── CloseCompareButton                     (Button — direct child of RightPanel)
│   ├── SelectButton                           (Button — direct child of RightPanel)
│   │   └── Text (TMP)
│   ├── SwapButton                             (Button — inside ButtonsPanel)
│   └── BioPanel
│       └── BioText                            (TMP)
├── CompareRightPanel
│   ├── ComparePlaceholder
│   │   └── PlaceholderText                    (TMP)
│   └── CompareInfoPanel                       (clone of RightPanel — same child paths)
│       ├── CharacterNamePanel/CharacterNameText
│       ├── RarityPanel/RarityRow/...
│       ├── CharacterStatsPanel/CharacterStats1-4/...  (incl. DiffLabel)
│       ├── ButtonsPanel/LevelUpButton, BoostButton
│       ├── CompareButton
│       ├── SelectButton / Text (TMP)
│       └── BioPanel/BioText
└── VerticalDivider                            (GO — shown during compare mode)
```

### LevelUpModal (LevelUpModalController : ModalController)
```
LevelUpModal                                   (root GO stays active, only ModalPanel toggles)
├── Backdrop                                   (GO)
└── ModalPanel                                 (GO — toggled by Show/Hide)
    ├── HeaderSection
    │   └── CharacterNameText                  (TMP)
    ├── InfoSection
    │   ├── RarityLevelRow
    │   │   ├── RarityLabel                    (TMP)
    │   │   └── LevelText                      (TMP)
    │   ├── NextLevelRow
    │   │   ├── NextLevelLabel                 (TMP — localized)
    │   │   └── NextLevelValue                 (TMP)
    │   ├── CostRow
    │   │   ├── CostLabel                      (TMP — localized)
    │   │   └── CostValue                      (TMP)
    │   └── RewardRow
    │       ├── RewardLabel                    (TMP — localized)
    │       └── RewardValue                    (TMP)
    ├── LevelUpButton                          (Button)
    │   └── Text                               (TMP — localized)
    ├── SPSection
    │   ├── AvailableSPRow
    │   │   ├── AvailableSPLabel               (TMP — localized)
    │   │   └── AvailableSPValue               (TMP)
    │   ├── StatRow_Strength
    │   │   ├── StatBar/Bar                    (Image — blue)
    │   │   ├── StatBar/BarPending             (Image — orange)
    │   │   ├── StatValueCurrent               (TMP)
    │   │   ├── StatValueMax                   (TMP)
    │   │   ├── PendingLabel                   (TMP — "+N")
    │   │   └── PlusButton                     (Button)
    │   ├── StatRow_ClubControl                (same children)
    │   ├── StatRow_Recovery                   (same children)
    │   ├── StatRow_Stamina                    (same children)
    │   └── ResetButton                        (Button)
    │       └── Text                           (TMP — localized)
    └── FooterSection
        ├── CancelButton                       (Button)
        │   └── Text                           (TMP — localized)
        └── ConfirmButton                      (Button)
            └── Text                           (TMP — localized)
```

---

## Club Inventory Screen

Root: `InventoryScreen/ClubDetailPanel`

### ClubDetailPanel (ClubDetailPanel)
```
ClubDetailPanel
├── LeftPanel
│   ├── ClubImage                              (Image)
│   └── InfoSection
│       ├── InfoHeader                         (TMP)
│       └── InfoText                           (TMP)
├── RightPanel
│   ├── ClubNameText                           (TMP)
│   ├── RarityLevelRow
│   │   ├── RarityLabel                        (TMP)
│   │   ├── LevelText                          (TMP)
│   │   └── LevelTextMax                       (TMP)
│   ├── StatsPanel
│   │   ├── PowerRow
│   │   │   ├── StatsName                      (TMP)
│   │   │   ├── Bar                            (Image)
│   │   │   ├── DiffLabel                      (TMP — compare only)
│   │   │   └── StatNumber                     (TMP)
│   │   ├── AccuracyRow                        (same children)
│   │   ├── LieResistanceRow                   (same children)
│   │   ├── LoftRow                            (same children)
│   │   ├── DurabilityRow                      (same children)
│   │   └── DistanceRow
│   │       ├── StatsName                      (TMP)
│   │       ├── DistanceValue                  (TMP — not a bar)
│   │       └── DiffLabel                      (TMP — compare only)
│   ├── ButtonsPanel
│   │   ├── LevelUpButton                      (Button)
│   │   └── RepairButton                       (Button)
│   ├── CompareButton                          (Button)
│   ├── EquipButton                            (Button)
│   │   └── Text (TMP)
│   ├── SwapButton                             (Button)
│   └── BagLabel                               (TMP)
├── CompareRightPanel
│   ├── ComparePlaceholder/PlaceholderText     (TMP)
│   └── CompareInfoPanel                       (clone of RightPanel — same child paths)
└── VerticalDivider
```

### ClubLevelUpModal (ClubLevelUpModalController : ModalController)
```
ClubLevelUpModal
├── Backdrop
└── ModalPanel
    ├── HeaderSection/ClubNameText             (TMP)
    ├── InfoSection
    │   ├── RarityLevelRow/RarityLabel, LevelText
    │   ├── NextLevelRow/NextLevelLabel, NextLevelValue
    │   ├── CostRow/CostLabel, CostValue
    │   └── RewardRow/RewardLabel, RewardValue
    ├── LevelUpButton / Text
    ├── SPSection
    │   ├── AvailableSPRow/AvailableSPLabel, AvailableSPValue
    │   ├── StatRow_Power      (allocatable — Bar, BarPending, StatValueCurrent, StatValueMax, PendingLabel, PlusButton)
    │   ├── StatRow_Accuracy   (allocatable)
    │   ├── StatRow_LieRes     (allocatable)
    │   ├── StatRow_Loft       (fixed — Bar, StatValueCurrent, StatValueMax, PlusButton disabled)
    │   ├── StatRow_Durability (allocatable)
    │   └── ResetButton / Text
    └── FooterSection
        ├── CancelButton / Text
        └── ConfirmButton / Text
```

### BagSelectionModal (BagSelectionModalController : ModalController)
```
BagSelectionModal
├── Backdrop
└── ModalPanel
    ├── HeaderSection/HeaderText               (TMP)
    ├── BagGrid                                (GridLayoutGroup — bag slot cards)
    └── FooterSection/CloseButton              (Button)
```

---

---

## Items Inventory Screen

Root: `InventoryScreen/ItemsContent`

### ItemDetailPanel (ItemDetailPanel)
```
ItemsContent
├── ItemCarousel                                 (ItemCarouselController — horizontal scroll)
│   └── [ItemThumbnailCard prefabs]
│       ├── Background                           (Image — rarity bg)
│       ├── ItemImage                            (Image — thumbnail sprite)
│       ├── RarityBadge                          (TMP — "C"/"R"/"M")
│       └── NameText                             (TMP — "{name}\n{rarity}")
└── ItemDetailPanel                              (ItemDetailPanel)
    ├── LeftPanel
    │   ├── ItemImage                            (Image — full sprite)
    │   └── BrandText                            (TMP — "GOLFIN")
    └── RightPanel
        ├── ItemNameText                         (TMP)
        ├── RarityText                           (TMP — colored rarity)
        ├── QuantityText                         (TMP — "x3")
        ├── RestoresHeader                       (TMP — "RESTORES")
        ├── EffectIcon                           (Image)
        ├── EffectText                           (TMP — "DURABILITY 50%")
        ├── ProTipHeader                         (TMP — "*PRO TIP")
        ├── ProTipText                           (TMP)
        ├── InfoHeader                           (TMP — "INFO")
        ├── InfoText                             (TMP)
        ├── CompareButton                        (Button — always disabled)
        └── UseButton                            (Button — gold, disabled if qty=0)
```

### ItemUseModal (ItemUseModalController : ModalController)
```
ItemUseModal                                     (root — ModalController, full-screen overlay)
├── Background                                   (Image — RepairBackground.png, dark overlay)
└── ModalPanel                                   (toggled by Show/Hide)
    └── ModalContainer                           (VerticalLayoutGroup)
        ├── TitleText                            (TMP — "SELECT CLUB")
        ├── TopDivider                           (Image — thin white line)
        ├── FilterBar                            (ClubFilterBar — HorizontalLayoutGroup)
        │   ├── ALLFilter / Button / Text        (TMP)
        │   ├── DRIVERSFilter / Button / Text
        │   ├── WOODSFilter / Button / Text
        │   ├── IRONSFilter / Button / Text
        │   ├── WEDGESFilter / Button / Text
        │   └── PUTTERSFilter / Button / Text
        ├── ScrollArea                           (ScrollRect — vertical)
        │   ├── Viewport                         (RectMask2D)
        │   │   └── GridContent                  (GridLayoutGroup — 4 columns, ContentSizeFitter)
        │   │       └── [ItemUseClubCard prefabs — spawned at runtime]
        │   └── Scrollbar                        (Scrollbar — vertical, right edge)
        │       └── Sliding Area/Handle
        ├── BottomDivider                        (Image — thin white line)
        └── CancelButton                         (Button)
            └── Text                             (TMP — "CANCEL")
```

### ItemUseClubCard prefab (180 × 410, VerticalLayoutGroup)
```
ItemUseClubCard
├── CardTop                                      (Image — rarity bg sprite, 140h)
│   ├── Portrait                                 (Image — club portrait, preserveAspect)
│   ├── RarityBadge                              (TMP — "R" etc., top-left)
│   └── LevelBadge                               (TMP — "Lv10", top-right)
├── NameText                                     (TMP — "DRIVER\nG&F")
├── StatsPanel                                   (VerticalLayoutGroup — 5 rows)
│   ├── StatRow_Power      / Bar (Image filled) + StatNum (TMP)
│   ├── StatRow_Accuracy   / Bar + StatNum
│   ├── StatRow_LieRes     / Bar + StatNum
│   ├── StatRow_Loft       / Bar + StatNum
│   └── StatRow_Durability / Bar + StatNum
├── DistanceRow                                  (HorizontalLayoutGroup)
│   ├── DistLabel                                (TMP — "DIST")
│   └── DistanceValue                            (TMP — "150 yd")
├── ButtonRow                                    (HorizontalLayoutGroup — both disabled)
│   ├── LevelUpBtn / Text
│   └── RepairBtn / Text
└── UseRepairKitBtn                              (Button — gold, full-width)
    └── UseRepairKitText                         (TMP — "USE REPAIR KIT")
```

---

## Bags Inventory Screen

Root: `InventoryScreen/BagsContent`

### BagsContent (BagCarouselController + BagDetailPanel)
```
BagsContent
├── BagCarousel                                  (BagCarouselController — horizontal scroll)
│   ├── [BagThumbnailCard instances]              (unlocked bags)
│   │   ├── BagImage                             (Image — thumbnail sprite)
│   │   ├── BagLabel                             (TMP — bag name)
│   │   ├── RarityBadge                          (Image — rarity bg)
│   │   │   └── Text                             (TMP — "R")
│   │   └── EquippedIcon                         (GO — toggled)
│   └── [BagSlotLockedPrefab instances]           (locked bags)
├── BagDetailPanel                               (BagDetailPanel)
│   ├── InfoArea
│   │   ├── BagFullImage                         (Image — full-size bag sprite)
│   │   ├── BagNameText                          (TMP — "BAG 1")
│   │   ├── EquippedIcon                         (GO — toggled)
│   │   └── DescriptionText                      (TMP — bag description from CSV)
│   └── ClubGrid                                 (GridLayoutGroup — 4×2)
│       ├── [BagSwapClubCard instances]           (equipped clubs — uses ItemUseClubCard)
│       │   ├── CardTop/Portrait, RarityBadge, LevelBadge
│       │   ├── NameText
│       │   ├── StatsPanel (5 stat rows + DistanceRow)
│       │   ├── ButtonRow (LevelUpBtn, RepairBtn)
│       │   └── SwapBtn / SwapText                (TMP — "SWAP")
│       └── [BagEmptyClubCard instances]          (empty slots — "EQUIP CLUB" button)
└── EquipBagButton                               (Button — gold/silver)
    └── Text                                     (TMP — "EQUIPPED" / "EQUIP")
```

### BagClubModal (BagClubModalController : ModalController)
```
BagClubModal                                     (root — ModalController, full-screen overlay)
├── Backdrop                                     (GO)
└── ModalPanel                                   (toggled by Show/Hide)
    └── ModalContainer                           (VerticalLayoutGroup)
        ├── TitleText                            (TMP — "SWAP CLUB" / "EQUIP CLUB")
        ├── FilterBar                            (ClubFilterBar — 6 buttons)
        │   ├── ALLFilter / Button / Text
        │   ├── DRIVERSFilter / Button / Text
        │   ├── WOODSFilter / Button / Text
        │   ├── IRONSFilter / Button / Text
        │   ├── WEDGESFilter / Button / Text
        │   └── PUTTERSFilter / Button / Text
        ├── ScrollArea                           (ScrollRect — vertical)
        │   ├── Viewport                         (RectMask2D)
        │   │   └── GridContent                  (GridLayoutGroup — 4 columns)
        │   │       └── [BagClubCard prefabs — spawned at runtime]
        │   └── Scrollbar                        (Scrollbar — vertical)
        └── CancelButton                         (Button)
            └── Text                             (TMP — "CANCEL")
```

### BagClubCard prefab (183 × 410, same as BagSwapClubCard)
```
BagClubCard
├── Background                                   (Image — rarity bg)
│   ├── CardTop                                  (Image — rarity bg sprite)
│   │   ├── Portrait                             (Image — club portrait)
│   │   ├── RarityBadge                          (TMP — "R")
│   │   ├── LevelBadge                           (TMP — "Lv10")
│   │   └── NameText                             (TMP — "DRIVER\nG&F")
│   ├── StatsPanel                               (VerticalLayoutGroup)
│   │   ├── DistanceRow / Image + DistanceValue
│   │   ├── StatRow_Power / Image + Bar + StatNum
│   │   ├── StatRow_Accuracy / Image + Bar + StatNum
│   │   ├── StatRow_LieRes / Image + Bar + StatNum
│   │   ├── StatRow_Loft / Image + Bar + StatNum
│   │   └── StatRow_Durability / Image + Bar + StatNum
│   └── ButtonRow                                (HorizontalLayoutGroup)
│       ├── LevelUpBtn / Text                    (disabled)
│       └── RepairBtn / Text                     (disabled)
├── Rim                                          (Image — decorative border)
└── SwapBtn                                      (Button — action: SWAP / EQUIP)
    └── SwapText                                 (TMP — action label)
```

### MatchMakingModal (MatchmakingModalController : ModalController, namespace `Golfin.UI.Matchmaking`)
```
MatchMakingModal                                 (root — controller stays active)
├── BG                                           (Image — backdrop, alpha 0.85, anchors full-stretch via PrefabInstance overrides)
└── ContentArea                                  (= modalPanel field; toggled by Show/Hide)
    ├── TitleText                                (TMP — "FINDING OPPONENT" → "OPPONENT FOUND")
    │                                              dot cycle uses 3 fixed-width <alpha=#00> slots so base text never shifts
    ├── PlayerCard                               (CharacterThumbnailCard — uses InitializeFromTemplate(charId, level))
    │   ├── Portrait + Rim
    │   ├── LevelBadge / RarityBadge
    │   ├── UsernameText                         (TMP — fake usernames capped at ≤8 chars)
    │   └── RankText                             (TMP — "RANK: #NNN")
    ├── VsLabel                                  (TMP — "Vs.")
    ├── OpponentCard                             (CharacterThumbnailCard)
    ├── NextHoleSection
    │   ├── NextHoleLabel                        (TMP — "NEXT HOLE")
    │   ├── HoleNameText                         (TMP — "Lomond Country Club - Hole 5")
    │   └── RewardRow                            (3 reward slot icons + values from CSV/home contract)
    └── CancelButton                             (Button — dismisses modal, restores hidden home elements)
        └── Text                                 (TMP — "CANCEL")
```

**Home elements hidden while modal is open** (refs serialized on the controller, restored via `OnHide()` and `OnDisable()` safety net):
- `homeMaintenanceNotice` → `HomeScreen/.../NoticePanel`
- `homeNextHolePanel` → `HomeScreen/.../NextHolePanel` (the bottom strip with the gold PLAY button — duplicate of modal content)

---

### HoleCompleteModal (HoleCompleteModalController, ShellScene Canvas — `Golfin.UI.Modals.Result`)
```
HoleCompleteModal                                (root — child Canvas overrideSorting=true, sortingOrder=900, + GraphicRaycaster)
└── HoleCompleteWidget                           (the lab two-card widget reused verbatim — HoleCompleteWidget.cs)
    ├── DimBackground                            (Image — full-screen scene dim)
    ├── Card1 (HoleCompleteCardWidget)           (current hole)
    │   ├── SuccessHeader / FailedHeader         (one shown per terminal state)
    │   ├── Subhead                              (TMP — "Lomond Country Club  - Hole N - Par P")
    │   ├── CurrentBody → HoleMapLarge + StatsBlockText  (TEE OFF / STROKES / BEST / TIME / BEST)
    │   ├── RewardsRow                           (CoinReward / RepairReward / BallReward)
    │   └── ReplayButton / RetryButton           (REPLAY on success, RETRY on failed)
    └── Card2 (HoleCompleteCardWidget)           (next hole — hidden on Hole 18)
        ├── NextHeader / LockedHeader            (NEXT gold when unlocked, LOCKED gray when failed + next not unlocked)
        ├── NextBody → NextHoleMapLarge + NextHoleDescText
        ├── RewardsRow
        └── PlayButton                           (PLAY — loads next hole; hidden when LOCKED)
```
Driven by `HoleCompleteModalController` (subscribes `GameSession.OnHoleComplete`). Card2 reward `CountText`/slot widths were widened (120/180 px) so "x100" fits on one line.

### Toast (ToastController, ShellScene Canvas — `Golfin.UI.Toast`)
```
Toast                                            (root — child Canvas overrideSorting=true, sortingOrder=950)
├── CanvasGroup                                  (fade in/hold/out)
└── Text                                         (TMP — e.g. "COURSE CLEARED!")
```
Singleton; `ToastController.Show(message, seconds)`. Fired by the modal on Hole 18 success.

---

### In-Game 1v1 HUD (LabScaffold.unity — gameplay HUD, versus-only; Phase 1)

Lives in the additive gameplay HUD scene `Assets/Scenes/Physics/LabScaffold.unity`, not ShellScene. All versus elements are gated behind `GameSession.IsVersus`; the solo/Practice HUD is unchanged.

```
HUD root
├── PlayerCard            (PlayerCardWidget, _playerIndex=0 — P1, active, top-left, reads PlayerContext)
├── PlayerCard_P2         (CLONE of PlayerCard; PlayerCardWidget _playerIndex=1 — opponent, top-right,
│                          mirrored by anchors: parameters left / portrait right; default inactive,
│                          activated by VersusHudController in versus; reads MatchContext.Players[1])
├── TurnBanner            (TurnBannerWidget — full-width 1170×210 band, 3px #818EA1 top+bottom borders,
│                          Rubik-SemiBold SDF + TMP auto-size; Show(text, fromLeft): YOUR TURN slides
│                          from LEFT, OPPONENT'S TURN from RIGHT; CanvasGroup fade; starts hidden)
└── (mini-map / HoleCard) (versus: relocated above the Fade/Draw button, image-only — hole-info data
                           card hidden, right-edge aligned to the bottom buttons; solo: top-right, unchanged)
```

- **Orchestrator:** `VersusHudController` (on HUD root) — activates P2 + relocates the mini-map in versus; serialized `_miniMapVersusPos`. `[SerializeField] _debugForceVersus` ships **false**; the Phase-1 debug toggle drives a NON-serialized `_runtimeDebugForceVersus` so captures can't bake a versus state into the scene.
- **Data layer:** static `MatchContext` (`Scripts/Gameplay/UI/ShotUI/HUD/MatchContext.cs`) — `Players[0/1]`, `ActiveIndex`, `SetActive(i)` (1.0/0.50 opacity swap), `OnChanged`/`OnActiveChanged`. Slot 0 from `PlayerContextPopulator`, slot 1 from `MatchmakingModalController` at OPPONENT FOUND.
- Clone gate: `PlayerCard_P2` carries the same `PlayerCardWidget` script GUID `c9b16932b3e429543aa96a954ce0ccbf` as P1 (cloned, never rebuilt).
- Phase 2 (not built): bot AI, turn-flow, win/tie + winner banner, driving the per-turn banner from real gameplay.

---

## Stamina Boost Shop (Order 517 — ShellScene, `GolfinRedux.UI.Shop`)

First shop. Two `ScreenManager` screens mounted under `ScreensRoot` (ScreenIds `StaminaShopSelection`, `StaminaShopDetail`); persistent top bar + nav shown on both. Entered via the roster **Boost** button (`CharacterDetailPanel.OnBoostClicked` → `StaminaShopSession.SelectedCharacterId` + `ShowScreen(StaminaShopSelection)`). Data from `Assets/Resources/Data/stamina_shops.csv` (10 MIE shops) + `stamina_shop_items.csv` (30 items) via `ShopCatalog`/`ShopModel`/`ShopItemModel`. Background: `Assets/Art/Shop/Background - Shop.png`. Built by reusing existing atoms (navy panels, `S_PillStadium` pills, RP pill, Play/Cancel buttons, SDF fonts), node-exact vs Figma 13156 (selection) / 13330 (detail).

```
StaminaShopSelectionScreen        (StaminaShopSelectionScreenController)
├── BOOST STAMINA title + region/prefecture filter pills (StaminaShopRegionPill / StaminaShopPrefecturePill)
└── CardsPanel  (cloned from Tournament Selection: BackgroundCardsContainer + ScrollRect + Viewport + Scrollbar)
    └── StaminaShopCard × N     (whole-card tap → OnCardTapped; storefront r32, FEATURED badge,
                                 category/name/tagline, hours + View-on-Maps, daily-bonus chip [recovery icon],
                                 derived STA range + navy RP pill, chevron)

StaminaShopDetailScreen           (StaminaShopDetailScreenController)
├── StaminaShopHeroCard          (cover-fit hero photo, OPEN NOW + FEATURED two-layer pills, category/name/address)
├── StaminaShopInfoCard          (3 cols: LOCATION / HOURS / SIGNATURE, hairline dividers)
└── StaminaShopMenuPanel         (MENU header + gold DAILY BONUS chip [recovery icon]; embedded CANCEL button)
    └── StaminaMenuRow × N       (tier badge HIGH/MED/LIGHT, item image, name/desc, +STA, RP cost, BUY button)
```

- **Purchase:** `StaminaMenuRow` BUY → `ShopTransaction.TryPurchase` → `RewardPointsManager.SpendPoints` + `StaminaRuntimeService.AddEnergy` (clamps to max, persists); returns `Success` / `InsufficientRp` / `StaminaFull` / `NullCharacter`. BUY disables when stamina is full. Covered by `Golfin.UI.Shop.Tests` (EditMode, reflection into Assembly-CSharp).
- **Daily-bonus chip** uses `Assets/Art/Shop/Icon - Recovery.png` (recovery-circle), not the lightning icon.

---

---

## Account / Auth Screens (login_signup_screens — 2026-07-21)

Four screens under `Canvas > ScreensRoot`, registered in `ScreenManager` (ScreenId: Login, CreateUsername, SignUp, EmailConfirmation). Prefabs at `Assets/Prefabs/UI/Account/`. Controllers at `Assets/Scripts/UI/Account/` (namespace `Golfin.UI.Account`).

```
ScreensRoot
├── LoginScreen         (LoginScreenController)
│   ├── Background      (S_Login_SplashBG full-screen)
│   ├── Scrim           (rgba 0,0,0 alpha 0.1 — flat fill, intentional)
│   ├── TopBand         (S_Login_TopBG_Navy 9-sliced, h=313, title "GOLFIN ACCOUNT" Rubik SemiBold 51px white)
│   └── CardBorder      (S_Common_BGCorner20, w=1074, navy gradient #133453→#091B33, pad=48)
│       └── CardBody    (ScrollRect > Content > VLG gap=48)
│           ├── SectionHeader   "LOGIN WITH EMAIL"
│           ├── EmailGroup      (EMAIL label + TMP_InputField)
│           ├── PasswordGroup   (PASSWORD label + TMP_InputField + EyeButton inside)
│           ├── ForgotPassword  (Button + ButtonPressFeedback, green text)
│           ├── LoginButton     (Button + ButtonPressFeedback, green gradient)
│           ├── Separator       (Divider.png)
│           ├── ServiceHeader   "LOGIN WITH  A SERVICE"
│           ├── GooglePill      (Button + ButtonPressFeedback, white w=670 h=150 r=90 3px black border)
│           ├── ApplePill       (Button + ButtonPressFeedback, same pill)
│           ├── Separator       (Divider.png)
│           ├── CancelButton    (Button + ButtonPressFeedback, ButtonCancel.png silver)
│           └── FooterRow       ("No account?" + CreateAccountButton green text)

├── CreateUsernameScreen  (CreateUsernameScreenController)
│   └── [same shell] CardBody: SectionHeader + UsernameGroup + BodyText (3 paragraphs) + CreateButton + CancelButton

├── SignUpScreen          (SignUpScreenController)
│   └── [same shell] CardBody: SectionHeader + EmailGroup + PasswordGroup + PasswordRules (5 rows: ICO_RuleCross/ICO_RuleTick + text) + CreateButton + Separator + ServiceHeader + GooglePill + ApplePill + Separator + CancelButton + FooterRow

└── EmailConfirmationScreen  (EmailConfirmationScreenController)
    └── [same shell] CardBody: SectionHeader + DescriptionText (4 paragraphs) + ResendButton (silver) + InstructionText + LoginButton (green)
```

**Password-rule rows** (SignUp only): each row has `RuleIcon` (Image, ICO_RuleCross/ICO_RuleTick 48px) + `RuleText` (TMP Rubik Regular 39px, white/green). Live toggle driven by `PasswordRequirements.Check(value)` on `onValueChanged`.

**Eye toggle** (Login + SignUp password fields): EyeButton sits INSIDE the password TMP_InputField container. `OnEyeToggle()` flips `contentType` Password↔Standard and calls `ForceLabelUpdate()`.

**Navigation stubs** (Phase 1 — ScreenManager wired in ShellScene):
- Login: CreateAccount→SignUp, Cancel→Splash; Login/ForgotPw/Google/Apple are `// TODO(Phase 2)` stubs
- SignUp: Create→EmailConfirmation, LoginHere→Login, Cancel→Login; Google/Apple stubs
- CreateUsername: Create→stub, Cancel→back
- EmailConfirmation: Resend→stub, Login→Login

---

## Key Notes

- **Character stat rows** use `Name+Bar/StatsName`, `Name+Bar/Bar`, `DiffLabel`, `StatNumber`
- **Club stat rows** use `StatsName`, `Bar`, `DiffLabel`, `StatNumber` (no Name+Bar wrapper)
- **DiffLabel** is hidden by default, shown only during compare mode
- **CompareInfoPanel** is always a clone of the parent RightPanel — same child paths
- **ModalController root GO stays active** — only `modalPanel` child is toggled via Show/Hide
- **Buttons** that are direct children of RightPanel (CompareButton, SelectButton, CloseCompareButton) are NOT inside ButtonsPanel
