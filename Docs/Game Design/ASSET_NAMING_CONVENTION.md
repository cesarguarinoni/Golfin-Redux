# GOLFIN Redux — Asset & File Naming Convention

**Author:** Claude (Architect)  
**Date:** 2026-03-25  
**Status:** ACTIVE — follow for all new assets, rename existing assets during cleanup passes  
**Based on:** Original GDD Cataloguing Naming Convention (simplified for solo dev)

---

## 1. General Rules

- **No spaces in filenames or folder names** — use PascalCase or hyphens
- **Prefixes identify asset type** at a glance
- **Suffixes identify variant** (size, state, rarity)
- **Brands and character names are proper nouns** — keep original casing (FYLOE, MireO, GolfinX)
- **Folders group by screen/system**, not by file type
- **Resources/ folder** uses the exact names referenced in code — don't rename without updating CSV/code

---

## 2. Prefix System

| Prefix | Type | Example |
|---|---|---|
| `S_` | Sprite (2D image used in UI) | `S_Menu_Driver_GF.png` |
| `T_` | Texture (applied to 3D model) | `T_Driver_FYLOE.png` |
| `MESH_` | 3D Mesh/Model | `MESH_Driver_FYLOE.fbx` |
| `BG_` | Background image | `BG_Home_Main.png` |
| `ICO_` | Icon (small UI icon) | `ICO_Power.png` |
| `FX_` | Effect/particle sprite | `FX_LevelUp_Sparkle.png` |
| `SFX_` | Sound effect | `SFX_ButtonTap.wav` |
| `MUS_` | Music track | `MUS_Home_Theme.mp3` |
| `ANIM_` | Animation clip | `ANIM_Character_Idle.anim` |

**For Redux Phase (UI-only):** Most assets are `S_` (sprites), `BG_` (backgrounds), and `ICO_` (icons). 3D prefixes will be used when gameplay assets are added.

---

## 3. Naming Patterns by Category

### Characters

| Asset Type | Pattern | Example |
|---|---|---|
| Menu/Roster portrait | `S_Char_{Name}` | `S_Char_James.png` |
| Full-body portrait | `S_CharFull_{Name}` | `S_CharFull_James.png` |
| Homescreen portrait | `S_CharHome_{Name}` | `S_CharHome_James.png` |
| 3D model (future) | `MESH_Char_{Name}` | `MESH_Char_James.fbx` |
| Model texture (future) | `T_Char_{Name}` | `T_Char_James.png` |

### Clubs

| Asset Type | Pattern | Example |
|---|---|---|
| Menu/Inventory portrait | `S_Club_{Type}-{Brand}` | `S_Club_Iron7-Mireo.png` |
| Full detail image | `S_ClubFull_{Type}-{Brand}` | `S_ClubFull_Iron7-Mireo.png` |
| In-game control sprite (future) | `S_ClubCtrl_{Type}-{Brand}` | `S_ClubCtrl_Iron7-Mireo.png` |
| 3D model (future) | `MESH_Club_{Type}-{Brand}` | `MESH_Club_Iron7-Mireo.fbx` |

**Club type shorthand:**
- `Driver`, `Wood3`, `Wood5`, `Wood7`
- `Iron3` through `Iron9`
- `AWedge`, `PWedge`, `SWedge`
- `Putter`

### Balls (future)

| Asset Type | Pattern | Example |
|---|---|---|
| Menu sprite | `S_Ball_{Brand}` | `S_Ball_Mireo.png` |
| In-game sprite | `S_BallCtrl_{Brand}` | `S_BallCtrl_Mireo.png` |

### Items (future)

| Asset Type | Pattern | Example |
|---|---|---|
| Menu sprite | `S_Item_{ItemName}` | `S_Item_RepairKit.png` |
| Premium variant | `S_Item_{ItemName}Premium` | `S_Item_RepairKitPremium.png` |

### UI Elements

| Asset Type | Pattern | Example |
|---|---|---|
| Background | `BG_{Screen}_{Variant}` | `BG_Home_Main.png` |
| Button image | `S_Btn_{Name}_{State}` | `S_Btn_Equip_Gold.png` |
| Icon | `ICO_{Name}` | `ICO_Power.png`, `ICO_Settings.png` |
| Rarity background | `S_Rarity_{Name}` | `S_Rarity_Common.png` |
| Status icon | `ICO_Status_{Name}` | `ICO_Status_Selected.png` |
| Navigation icon | `ICO_Nav_{Name}` | `ICO_Nav_Home.png` |
| Rim/border image | `S_Rim_{Variant}` | `S_Rim_GoldGradient.png` |
| Divider line | `S_Divider_{Variant}` | `S_Divider_White.png` |
| Arrow | `ICO_Arrow_{Direction}` | `ICO_Arrow_Left.png` |
| Dot indicator | `ICO_Dot_{State}` | `ICO_Dot_Active.png` |

### Courses (future — gameplay)

| Asset Type | Pattern | Example |
|---|---|---|
| Course mesh | `MESH_{ClubName}_{CourseName}_{Hole}` | `MESH_Lomond_Rindo_1.fbx` |
| Course texture | `T_{ClubName}_{CourseName}_{Hole}` | `T_Lomond_Rindo_1.png` |
| Course background | `BG_Course_{CourseName}_{Hole}` | `BG_Course_Lomond_5.png` |

---

## 4. Folder Structure

```
Assets/
├── Art/                           # Source art (not loaded at runtime)
│   ├── Characters/                # Character source images
│   ├── Clubs/                     # Club source images
│   │   ├── Portraits/
│   │   └── Full/
│   ├── HomeScreen/
│   ├── LoadingScreen/
│   ├── LogoScreen/
│   ├── SplashScreen/
│   ├── RosterScreen/
│   ├── ClubsInventory/
│   ├── Rarities/
│   └── Settings/
│
├── Resources/                     # Runtime-loaded assets (Resources.Load)
│   ├── Characters/
│   │   └── Homescreen/            # Homescreen character portraits
│   ├── Clubs/
│   │   ├── Portraits/             # Club thumbnails (carousel cards)
│   │   └── Full/                  # Club full images (detail panel)
│   ├── Portraits/
│   │   ├── FullBody/              # Character full-body (detail panel)
│   │   └── Thumbnails/            # Character thumbnails (carousel cards)
│   └── Rarities/                  # Shared rarity backgrounds
│
├── Data/                          # CSV data files
│   ├── Characters.csv
│   ├── Clubs.csv
│   └── LevelUpCosts.csv
│
├── Localization/                  # Localization CSVs
│   └── LocalizationText.csv
│
├── Prefabs/                       # Unity prefabs
│   └── UI/
│       ├── Roster/
│       └── Inventory/
│
├── References/                    # Design reference images (not in build)
│   ├── HomeScreen/
│   ├── RosterScreen/
│   ├── Inventory/
│   └── Settings/
│
├── Screenshots/                   # Dev screenshots (gitignored)
│
├── Scripts/                       # C# code
│   ├── CharacterManager.cs
│   ├── ClubManager.cs
│   ├── Audio/
│   ├── Debug/
│   ├── Editor/
│   │   └── Archive/               # Deprecated editor scripts
│   ├── UI/
│   │   ├── Editor/
│   │   ├── Inventory/
│   │   │   └── Editor/
│   │   ├── Modals/
│   │   └── Roster/
│   │       ├── Data/
│   │       ├── Editor/
│   │       ├── Managers/
│   │       └── UI/
│   └── Utilities/
│       ├── TextGradients.cs
│       ├── RuntimeActiveStateManager.cs
│       └── UIAutoWire.cs
│
└── Scenes/
    ├── ShellScene.unity
    └── GameplayScene.unity
```

---

## 5. Resources/ Naming (Critical — Code References These)

Files inside `Resources/` are loaded by name via `Resources.Load<Sprite>("path/name")`. These names are stored in CSVs (`portraitSprite`, `portraitFull` columns). **Do NOT rename files in Resources/ without updating the corresponding CSV values.**

| Resources Path | CSV Column | Naming Rule |
|---|---|---|
| `Portraits/Thumbnails/{Name}` | Characters.csv → `portraitSprite` | Match character's first name exactly |
| `Portraits/FullBody/{Name}` | Characters.csv → `portraitFull` | `BigRoster{Name}` (legacy, keep) |
| `Characters/Homescreen/{Name}` | Characters.csv → name match | Match character's first name |
| `Clubs/Portraits/S_Menu_{Type}_{BRAND}` | Clubs.csv → `portraitSprite` | `S_Menu_{ArtType}_{BRANDTAG}` — the 792 generated rows; the 4 legacy files use `{ClubType}-{Brand}` |
| `Clubs/Full/{Type}-{Brand}` | Clubs.csv → `portraitFull` | `{ArtType}-{Brand}` or `Placeholder` |
| `Clubs/Controls/S_Controls_{Type}_{BRAND}` | Clubs.csv → `controlSprite` | `S_Controls_{ArtType}_{BRANDTAG}` |
| `Items/Thumbnails/{Name}-{Rarity}` | Items.csv → `thumbnailSprite` | `{Pascal(name)}-{rarity}`, e.g. `RepairKit-Common` |
| `Items/Full/{Name}-{Rarity}` | Items.csv → `fullSprite` | `{Pascal(name)}-{rarity}` |
| `Balls/Thumbnails/{Name}` | Balls.csv → `thumbnailSprite` | `{Pascal(name)}` — Balls.csv has no `rarity` column, so the suffix is omitted (`ball_putt_ace` → `PuttAce`) |
| `Balls/Full/{Name}` | Balls.csv → `fullSprite` | `{Pascal(name)}` |
| `Rarities/{RarityName}` | RarityHelper.cs code | `Common`, `Uncommon`, `Rare`, `Mythic`, `Legendary`, `Supreme` |

**Items and balls share one rule** (added 2026-08-28 with `content_art_bundling` §4): `{Pascal(name)}-{rarity}`
from the row's OWN `name` and `rarity` columns, with the `-{rarity}` suffix **omitted when the catalog
carries no rarity column**. It is stated here because the existing names are *not* derivable from the id —
`repairkit_common` gives you no way to reach `RepairKit-Common` — so anything generating these names has to
read the row. One rule reproduces both folders exactly: `("Repair Kit","Common") → RepairKit-Common`,
`("Putt Ace","") → PuttAce`.

**`ArtType` / `BRANDTAG` for clubs** (`Tools/club-gen/generate_clubs.py:136-143`): `ArtType` is the `type`
column, except that `A.Wedge` / `P.Wedge` / `S.Wedge` all collapse to `Wedge` — the three wedges share one
art set. `BRANDTAG` is the brand's alphanumerics, upper-cased (`G&F` → `GF`, `MireO` → `MIREO`); the
`{Brand}` form used by `Clubs/Full` is the brand title-cased with spaces removed (`ROYAL SWING` →
`RoyalSwing`). Each of the three club folders keeps its own prefix — a bare `{Type}-{Brand}` file in
`Clubs/Controls` would be the only one of 78 without `S_Controls_`.

> `Assets/Editor/ContentArtFetcher.cs` (`GOLFIN/Content/Fetch URL Art`) derives names by exactly these
> rules when it pulls an admin-uploaded URL into `Resources/`. Change a rule here and change it there.

---

## 6. Script Naming Convention

| Type | Pattern | Example |
|---|---|---|
| Manager (singleton) | `{System}Manager` | `CharacterManager`, `ClubManager` |
| Database loader | `{System}DatabaseCSV` | `CharacterDatabaseCSV`, `ClubDatabaseCSV` |
| Data class | `{System}Data` or `Player{System}Data` | `ClubData`, `PlayerClubData` |
| Screen controller | `{Screen}ScreenController` | `RosterScreenController`, `InventoryScreenController` |
| Detail panel | `{System}DetailPanel` | `CharacterDetailPanel`, `ClubDetailPanel` |
| Compare controller | `{System}CompareController` | `CompareController`, `ClubCompareController` |
| Carousel controller | `{System}CarouselController` | `CarouselController`, `ClubCarouselController` |
| Thumbnail card | `{System}ThumbnailCard` | `CharacterThumbnailCard`, `ClubThumbnailCard` |
| Modal controller | `{Name}ModalController` | `LevelUpModalController` |
| Editor builder | `{What}Builder` | `ClubDetailPanelBuilder`, `InventoryScreenBuilder` |
| Editor auto-wire | `{What}AutoWire` | `DetailPanelAutoWire`, `ClubCompareAutoWire` |
| Utility | Descriptive name | `TextGradients`, `RuntimeActiveStateManager`, `RarityHelper` |

---

## 7. CSV Column Naming

| Column Purpose | Pattern | Example |
|---|---|---|
| Unique ID | `id` | `char_james`, `club_iron7_mireo` |
| Display name | `name` | `James`, `Iron 7 Mireo` |
| Enum/type value | lowercase | `rarity`, `type` |
| Base stat | `base{StatName}` | `basePower`, `baseAccuracy` |
| Sprite reference | `portrait{Variant}` | `portraitSprite`, `portraitFull` |
| Max value | `max{Thing}` | `maxLevel`, `maxDurability` |
| Description text | `bio` or `info` | Character uses `bio`, Club uses `info` |

---

## 8. Unity Hierarchy Naming

| Object Type | Pattern | Example |
|---|---|---|
| Screen root | `{Screen}Screen` | `RosterScreen`, `InventoryScreen` |
| Panel | `{Name}Panel` | `DetailPanel`, `RightPanel`, `LeftPanel` |
| Button | `{Action}Button` | `LevelUpButton`, `EquipButton`, `CompareButton` |
| Text (TMP) | `{Name}Text` | `ClubNameText`, `RarityLabel`, `LevelText` |
| Image | `{Name}Image` or `{Name}` | `ClubImage`, `Bar`, `Background` |
| Container | `{Name}Section` or `{Name}Container` | `CarouselSection`, `StatsPanel` |
| Row (stat, info) | `{Name}Row` | `PowerRow`, `AccuracyRow` |
| Icon | `{Name}Icon` | `EquippedIcon`, `LevelUpReadyIcon` |
| Spacer | `{Name}Spacer` | `EquipSpacer` |

---

## 9. Localization Key Naming

| Pattern | Example |
|---|---|
| `{SCREEN}_{ELEMENT}` | `ROSTER_TITLE`, `HOME_PLAY` |
| `{SCREEN}_{STAT}` | `CLUB_POWER`, `CLUB_ACCURACY` |
| `{SCREEN}_{ACTION}` | `CLUB_EQUIP`, `CLUB_EQUIPPED`, `MODAL_CONFIRM` |
| `{SCREEN}_{TEXT}` | `COMPARE_EMPTY_PROMPT`, `CLUB_IN_BAG` |
| `RARITY_{NAME}` | `RARITY_COMMON`, `RARITY_SUPREME` |
| `TIP_{NAME}` | `TIP_CLUB`, `TIP_FORECAST` |

---

## 10. What NOT to Rename (Legacy)

These names are wired throughout the project. Renaming would break references:
- `BigRoster{Name}` portraits in Resources/Portraits/FullBody/ — legacy naming from Phase 2
- `CharacterThumbnailCardGlowUp` prefab name
- Character ID format `char_{name}` in Characters.csv
- Club ID format `club_{type}_{brand}` in Clubs.csv
- Localization keys already in LocalizationText.csv

When adding NEW assets, follow this convention. Rename legacy assets only during dedicated cleanup sprints with full regression testing.
