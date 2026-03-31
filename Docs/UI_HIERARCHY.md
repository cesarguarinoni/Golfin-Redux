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

## Key Notes

- **Character stat rows** use `Name+Bar/StatsName`, `Name+Bar/Bar`, `DiffLabel`, `StatNumber`
- **Club stat rows** use `StatsName`, `Bar`, `DiffLabel`, `StatNumber` (no Name+Bar wrapper)
- **DiffLabel** is hidden by default, shown only during compare mode
- **CompareInfoPanel** is always a clone of the parent RightPanel — same child paths
- **ModalController root GO stays active** — only `modalPanel` child is toggled via Show/Hide
- **Buttons** that are direct children of RightPanel (CompareButton, SelectButton, CloseCompareButton) are NOT inside ButtonsPanel
