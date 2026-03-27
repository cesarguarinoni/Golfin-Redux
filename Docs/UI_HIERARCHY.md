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

## Key Notes

- **Character stat rows** use `Name+Bar/StatsName`, `Name+Bar/Bar`, `DiffLabel`, `StatNumber`
- **Club stat rows** use `StatsName`, `Bar`, `DiffLabel`, `StatNumber` (no Name+Bar wrapper)
- **DiffLabel** is hidden by default, shown only during compare mode
- **CompareInfoPanel** is always a clone of the parent RightPanel — same child paths
- **ModalController root GO stays active** — only `modalPanel` child is toggled via Show/Hide
- **Buttons** that are direct children of RightPanel (CompareButton, SelectButton, CloseCompareButton) are NOT inside ButtonsPanel
