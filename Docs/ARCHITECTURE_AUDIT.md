# Architecture Audit

> Auto-generated 2026-04-06 10:29. Do not edit manually.

## File Tree (Scripts)

```
Assets/Scripts/Audio/AudioManager.cs
Assets/Scripts/BagDatabaseCSV.cs
Assets/Scripts/BagManager.cs
Assets/Scripts/BallManager.cs
Assets/Scripts/CharacterManager.cs
Assets/Scripts/ClubManager.cs
Assets/Scripts/Debug/RewardPointsDebugPanel.cs
Assets/Scripts/Editor/Archive/ClubDetailPanelBuilder.cs
Assets/Scripts/Editor/Archive/ClubInventoryPatcher.cs
Assets/Scripts/Editor/Archive/CompareRightPanelBuilder.cs
Assets/Scripts/Editor/Archive/ExampleAutoWireScreen.cs
Assets/Scripts/Editor/Archive/FilterBarPatcher.cs
Assets/Scripts/Editor/Archive/FixBarImageTypes.cs
Assets/Scripts/Editor/Archive/LevelUpModalBuilder.cs
Assets/Scripts/Editor/Archive/LevelUpModalPatcher.cs
Assets/Scripts/Editor/Archive/MenuItemRemover.cs
Assets/Scripts/Editor/Archive/RosterCarouselBuilder.cs
Assets/Scripts/Editor/Archive/RosterMenuCleanup.cs
Assets/Scripts/Editor/Archive/RosterPhase1TestRunner.cs
Assets/Scripts/Editor/Archive/RosterPrefabBuilder.cs
Assets/Scripts/Editor/Archive/RosterScreenBuilder.cs
Assets/Scripts/Editor/Archive/RosterSystemSetupTool.cs
Assets/Scripts/Editor/BagDatabaseCSVSetup.cs
Assets/Scripts/Editor/BagManagerSetup.cs
Assets/Scripts/Editor/BagSelectionModalAutoWire.cs
Assets/Scripts/Editor/ItemManagerSetup.cs
Assets/Scripts/Editor/ScreenshotTool.cs
Assets/Scripts/ItemManager.cs
Assets/Scripts/UI/AboutSubmenu.cs
Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs
Assets/Scripts/UI/FadeController.cs
Assets/Scripts/UI/HoleData.cs
Assets/Scripts/UI/HoleDatabase.cs
Assets/Scripts/UI/HoleDatabaseLoader.cs
Assets/Scripts/UI/HomeScreenController.cs
Assets/Scripts/UI/Inventory/BagCarouselController.cs
Assets/Scripts/UI/Inventory/BagClubCard.cs
Assets/Scripts/UI/Inventory/BagClubModalController.cs
Assets/Scripts/UI/Inventory/BagDetailPanel.cs
Assets/Scripts/UI/Inventory/BagSelectionModalController.cs
Assets/Scripts/UI/Inventory/BagThumbnailCard.cs
Assets/Scripts/UI/Inventory/BallCarouselController.cs
Assets/Scripts/UI/Inventory/BallCompareController.cs
Assets/Scripts/UI/Inventory/BallData.cs
Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs
Assets/Scripts/UI/Inventory/BallDetailPanel.cs
Assets/Scripts/UI/Inventory/BallSegmentedBar.cs
Assets/Scripts/UI/Inventory/BallThumbnailCard.cs
Assets/Scripts/UI/Inventory/BallThumbnailEmptyCard.cs
Assets/Scripts/UI/Inventory/ClubCarouselController.cs
Assets/Scripts/UI/Inventory/ClubCompareController.cs
Assets/Scripts/UI/Inventory/ClubData.cs
Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs
Assets/Scripts/UI/Inventory/ClubDetailPanel.cs
Assets/Scripts/UI/Inventory/ClubFilterBar.cs
Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs
Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs
Assets/Scripts/UI/Inventory/Editor/BagClubModalAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BagsContentAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BallCarouselAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BallCompareBuilder.cs
Assets/Scripts/UI/Inventory/Editor/BallDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/BallDetailPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/BallManagerSetup.cs
Assets/Scripts/UI/Inventory/Editor/BallThumbnailCardFix.cs
Assets/Scripts/UI/Inventory/Editor/BallThumbnailEmptyCardFix.cs
Assets/Scripts/UI/Inventory/Editor/ClubCompareAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubCompareRightPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ClubDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubLevelUpModalAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubManagerSetup.cs
Assets/Scripts/UI/Inventory/Editor/ClubThumbnailCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/InventoryScreenBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ItemRightPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemsContentBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemThumbnailCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemUseClubCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ItemUseModalAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ItemUseModalBuilder.cs
Assets/Scripts/UI/Inventory/InventoryScreenController.cs
Assets/Scripts/UI/Inventory/ItemCarouselController.cs
Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs
Assets/Scripts/UI/Inventory/ItemDataRuntime.cs
Assets/Scripts/UI/Inventory/ItemDetailPanel.cs
Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs
Assets/Scripts/UI/Inventory/ItemUseClubCard.cs
Assets/Scripts/UI/Inventory/ItemUseModalController.cs
Assets/Scripts/UI/Inventory/PlayerItemData.cs
Assets/Scripts/UI/LanguageSubmenu.cs
Assets/Scripts/UI/LoadingBar.cs
Assets/Scripts/UI/LoadingScreenController.cs
Assets/Scripts/UI/LogoScreenController.cs
Assets/Scripts/UI/Modals/ModalController.cs
Assets/Scripts/UI/PersistentUIManager.cs
Assets/Scripts/UI/ProTipCard.cs
Assets/Scripts/UI/Roster/Data/AutomaticStatAllocation.cs
Assets/Scripts/UI/Roster/Data/CharacterLevelUpData.cs
Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs
Assets/Scripts/UI/Roster/Data/ManualSPAllocation.cs
Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs
Assets/Scripts/UI/Roster/Data/RarityStatCaps.cs
Assets/Scripts/UI/Roster/Data/StatAllocationStrategy.cs
Assets/Scripts/UI/Roster/Editor/CompareAutoWire.cs
Assets/Scripts/UI/Roster/Editor/CompareRightPanelDiffBuilder.cs
Assets/Scripts/UI/Roster/Editor/DetailPanelAutoWire.cs
Assets/Scripts/UI/Roster/Editor/LevelUpModalAutoWire.cs
Assets/Scripts/UI/Roster/Editor/PaginationDotSetup.cs
Assets/Scripts/UI/Roster/Editor/RosterDebugTools.cs
Assets/Scripts/UI/Roster/Editor/StatusIconBuilder.cs
Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs
Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs
Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs
Assets/Scripts/UI/Roster/UI/CarouselController.cs
Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs
Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs
Assets/Scripts/UI/Roster/UI/CompareController.cs
Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs
Assets/Scripts/UI/Roster/UI/RosterScreenController.cs
Assets/Scripts/UI/Roster/UI/StatBar.cs
Assets/Scripts/UI/ScreenDeactivator.cs
Assets/Scripts/UI/ScreenManager.cs
Assets/Scripts/UI/SettingsController.cs
Assets/Scripts/UI/SettingsControllerPhase2.cs
Assets/Scripts/UI/SettingsMenuItem.cs
Assets/Scripts/UI/SoundSettingsSubmenu.cs
Assets/Scripts/UI/SplashScreenController.cs
Assets/Scripts/UI/SwipeDetector.cs
Assets/Scripts/UI/UserProfileSubmenu.cs
Assets/Scripts/Utilities/RuntimeActiveStateManager.cs
Assets/Scripts/Utilities/TextGradients.cs
Assets/Scripts/Utilities/UIAutoWire.cs
```

## File Tree (Data)

```
Assets/Data/Bags.csv
Assets/Data/Bags.csv.meta
Assets/Data/Balls.csv
Assets/Data/Balls.csv.meta
Assets/Data/CharacterDatabase.asset
Assets/Data/CharacterDatabase.asset.meta
Assets/Data/Characters.csv
Assets/Data/Characters.csv.meta
Assets/Data/Clubs.csv
Assets/Data/Clubs.csv.meta
Assets/Data/CREATE_DATABASE.md
Assets/Data/CREATE_DATABASE.md.meta
Assets/Data/HoleDatabase.asset
Assets/Data/HoleDatabase.asset.meta
Assets/Data/HoleDatabase.csv
Assets/Data/HoleDatabase.csv.meta
Assets/Data/Items.csv
Assets/Data/Items.csv.meta
Assets/Data/LevelUpCosts.csv
Assets/Data/LevelUpCosts.csv.meta
Assets/Data/README_HOLES.md
Assets/Data/README_HOLES.md.meta
```

## MonoBehaviours

| Class | File | Singleton | Key Interfaces |
|---|---|---|---|
| BagDatabaseCSV | Assets/Scripts/BagDatabaseCSV.cs | Yes |  |
| BagManager | Assets/Scripts/BagManager.cs | Yes |  |
| BallManager | Assets/Scripts/BallManager.cs | Yes |  |
| CharacterManager | Assets/Scripts/CharacterManager.cs | Yes |  |
| ClubManager | Assets/Scripts/ClubManager.cs | Yes |  |
| ItemManager | Assets/Scripts/ItemManager.cs | Yes |  |
| AudioManager | Assets/Scripts/Audio/AudioManager.cs | Yes |  |
| RewardPointsDebugPanel | Assets/Scripts/Debug/RewardPointsDebugPanel.cs | Yes |  |
| ExampleAutoWireScreen | Assets/Scripts/Editor/Archive/ExampleAutoWireScreen.cs |  |  |
| ExampleFullyAutoWired | Assets/Scripts/Editor/Archive/ExampleAutoWireScreen.cs |  |  |
| AboutSubmenu | Assets/Scripts/UI/AboutSubmenu.cs |  |  |
| FadeController | Assets/Scripts/UI/FadeController.cs | Yes |  |
| HoleDatabaseLoader | Assets/Scripts/UI/HoleDatabaseLoader.cs | Yes |  |
| HomeScreenController | Assets/Scripts/UI/HomeScreenController.cs | Yes |  |
| LanguageSubmenu | Assets/Scripts/UI/LanguageSubmenu.cs |  |  |
| LoadingBar | Assets/Scripts/UI/LoadingBar.cs |  |  |
| LoadingScreenController | Assets/Scripts/UI/LoadingScreenController.cs |  |  |
| LogoScreenController | Assets/Scripts/UI/LogoScreenController.cs |  |  |
| PersistentUIManager | Assets/Scripts/UI/PersistentUIManager.cs | Yes |  |
| ProTipCard | Assets/Scripts/UI/ProTipCard.cs |  | IPointerClickHandler |
| ScreenDeactivator | Assets/Scripts/UI/ScreenDeactivator.cs | Yes |  |
| ScreenManager | Assets/Scripts/UI/ScreenManager.cs | Yes |  |
| SettingsController | Assets/Scripts/UI/SettingsController.cs | Yes |  |
| SettingsControllerPhase2 | Assets/Scripts/UI/SettingsControllerPhase2.cs | Yes |  |
| SettingsMenuItem | Assets/Scripts/UI/SettingsMenuItem.cs |  |  |
| SoundSettingsSubmenu | Assets/Scripts/UI/SoundSettingsSubmenu.cs | Yes |  |
| SplashScreenController | Assets/Scripts/UI/SplashScreenController.cs |  |  |
| SwipeDetector | Assets/Scripts/UI/SwipeDetector.cs |  | IBeginDragHandler, IEndDragHandler |
| UserProfileSubmenu | Assets/Scripts/UI/UserProfileSubmenu.cs | Yes |  |
| BagCarouselController | Assets/Scripts/UI/Inventory/BagCarouselController.cs | Yes |  |
| BagClubCard | Assets/Scripts/UI/Inventory/BagClubCard.cs |  |  |
| BagDetailPanel | Assets/Scripts/UI/Inventory/BagDetailPanel.cs | Yes |  |
| BagThumbnailCard | Assets/Scripts/UI/Inventory/BagThumbnailCard.cs |  |  |
| BallCarouselController | Assets/Scripts/UI/Inventory/BallCarouselController.cs | Yes |  |
| BallCompareController | Assets/Scripts/UI/Inventory/BallCompareController.cs | Yes |  |
| BallDatabaseCSV | Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs | Yes |  |
| BallDetailPanel | Assets/Scripts/UI/Inventory/BallDetailPanel.cs | Yes |  |
| BallSegmentedBar | Assets/Scripts/UI/Inventory/BallSegmentedBar.cs |  |  |
| BallThumbnailCard | Assets/Scripts/UI/Inventory/BallThumbnailCard.cs | Yes |  |
| BallThumbnailEmptyCard | Assets/Scripts/UI/Inventory/BallThumbnailEmptyCard.cs |  |  |
| ClubCarouselController | Assets/Scripts/UI/Inventory/ClubCarouselController.cs | Yes |  |
| ClubCompareController | Assets/Scripts/UI/Inventory/ClubCompareController.cs | Yes |  |
| ClubDatabaseCSV | Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs | Yes |  |
| ClubDetailPanel | Assets/Scripts/UI/Inventory/ClubDetailPanel.cs | Yes |  |
| ClubFilterBar | Assets/Scripts/UI/Inventory/ClubFilterBar.cs |  |  |
| ClubThumbnailCard | Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs | Yes |  |
| InventoryScreenController | Assets/Scripts/UI/Inventory/InventoryScreenController.cs |  |  |
| ItemCarouselController | Assets/Scripts/UI/Inventory/ItemCarouselController.cs | Yes |  |
| ItemDatabaseCSV | Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs | Yes |  |
| ItemDetailPanel | Assets/Scripts/UI/Inventory/ItemDetailPanel.cs | Yes |  |
| ItemThumbnailCard | Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs | Yes |  |
| ItemUseClubCard | Assets/Scripts/UI/Inventory/ItemUseClubCard.cs | Yes |  |
| ModalController | Assets/Scripts/UI/Modals/ModalController.cs |  |  |
| CharacterLevelUpDatabase | Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs | Yes |  |
| CharacterDatabaseCSV | Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs | Yes |  |
| RewardPointsManager | Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs | Yes |  |
| CarouselController | Assets/Scripts/UI/Roster/UI/CarouselController.cs | Yes |  |
| CharacterDetailPanel | Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs | Yes |  |
| CharacterThumbnailCard | Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs | Yes |  |
| CompareController | Assets/Scripts/UI/Roster/UI/CompareController.cs | Yes |  |
| RosterScreenController | Assets/Scripts/UI/Roster/UI/RosterScreenController.cs | Yes |  |
| StatBar | Assets/Scripts/UI/Roster/UI/StatBar.cs |  |  |
| RuntimeActiveStateManager | Assets/Scripts/Utilities/RuntimeActiveStateManager.cs |  |  |

## Singletons

- **BagManager** (Assets/Scripts/BagManager.cs) (DontDestroyOnLoad)
- **BallManager** (Assets/Scripts/BallManager.cs) (DontDestroyOnLoad)
- **CharacterManager** (Assets/Scripts/CharacterManager.cs) (DontDestroyOnLoad)
- **ClubManager** (Assets/Scripts/ClubManager.cs) (DontDestroyOnLoad)
- **ItemManager** (Assets/Scripts/ItemManager.cs) (DontDestroyOnLoad)
- **AudioManager** (Assets/Scripts/Audio/AudioManager.cs) (DontDestroyOnLoad)
- **FadeController** (Assets/Scripts/UI/FadeController.cs) (DontDestroyOnLoad)
- **PersistentUIManager** (Assets/Scripts/UI/PersistentUIManager.cs) (DontDestroyOnLoad)
- **SettingsController** (Assets/Scripts/UI/SettingsController.cs) 
- **SettingsControllerPhase2** (Assets/Scripts/UI/SettingsControllerPhase2.cs) 
- **CharacterLevelUpDatabase** (Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs) 
- **CharacterDatabaseCSV** (Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs) (DontDestroyOnLoad)
- **RewardPointsManager** (Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs) (DontDestroyOnLoad)

## Events (Action delegates)

| Class | Event |
|---|---|
| BagManager | `public event System.Action<int>? OnBagChanged;` |
| BagManager | `public event System.Action<int>? OnEquippedBagChanged;` |
| BallManager | `public event System.Action? OnInventoryChanged;` |
| CharacterManager | `public event System.Action<string>? OnCharacterLeveledUp;` |
| CharacterManager | `public event System.Action<string>? OnCharacterSelected;` |
| CharacterManager | `public event System.Action? OnRosterChanged;` |
| ClubManager | `public event System.Action<string>? OnClubEquipped;` |
| ClubManager | `public event System.Action<string>? OnClubLeveledUp;` |
| ClubManager | `public event System.Action? OnInventoryChanged;` |
| ClubManager | `public event System.Action<string>? OnClubRepaired;` |
| ItemManager | `public event System.Action? OnInventoryChanged;` |
| SettingsMenuItem | `public event System.Action<SettingsMenuItem> OnExpanded;` |
| SettingsMenuItem | `public event System.Action<SettingsMenuItem> OnCollapsed;` |
| BagCarouselController | `public event System.Action<int>? OnBagSelected;` |
| BagClubCard | `public event System.Action? OnActionClicked;` |
| BagThumbnailCard | `public event System.Action? OnClicked;` |
| BallCarouselController | `public event System.Action<string>? OnBallSelected;` |
| ClubCarouselController | `public event System.Action<string>? OnClubSelected;` |
| ClubFilterBar | `public event System.Action<ClubType?>? OnFilterChanged;` |
| ItemCarouselController | `public event System.Action<string>? OnItemSelected;` |
| ItemUseClubCard | `public event System.Action? OnUseRepairKit;` |
| RewardPointsManager | `public event System.Action<int> OnPointsChanged;` |
| CarouselController | `public event System.Action<string> OnCharacterSelected; // Change to event` |

## Serialized Fields Summary

| Class | File | SerializeField Count |
|---|---|---|
| BagDataRuntime | Assets/Scripts/BagDatabaseCSV.cs | 1 |
| CharacterManager | Assets/Scripts/CharacterManager.cs | 2 |
| AudioManager | Assets/Scripts/Audio/AudioManager.cs | 3 |
| BagSelectionModalAutoWire | Assets/Scripts/Editor/BagSelectionModalAutoWire.cs | 1 |
| AboutSubmenu | Assets/Scripts/UI/AboutSubmenu.cs | 5 |
| FadeController | Assets/Scripts/UI/FadeController.cs | 1 |
| HoleDatabaseLoader | Assets/Scripts/UI/HoleDatabaseLoader.cs | 2 |
| HomeScreenController | Assets/Scripts/UI/HomeScreenController.cs | 42 |
| LanguageSubmenu | Assets/Scripts/UI/LanguageSubmenu.cs | 6 |
| LoadingBar | Assets/Scripts/UI/LoadingBar.cs | 2 |
| LoadingScreenController | Assets/Scripts/UI/LoadingScreenController.cs | 5 |
| LogoScreenController | Assets/Scripts/UI/LogoScreenController.cs | 3 |
| ProTipCard | Assets/Scripts/UI/ProTipCard.cs | 9 |
| ScreenManager | Assets/Scripts/UI/ScreenManager.cs | 7 |
| SettingsMenuItem | Assets/Scripts/UI/SettingsMenuItem.cs | 7 |
| SoundSettingsSubmenu | Assets/Scripts/UI/SoundSettingsSubmenu.cs | 4 |
| SwipeDetector | Assets/Scripts/UI/SwipeDetector.cs | 1 |
| UserProfileSubmenu | Assets/Scripts/UI/UserProfileSubmenu.cs | 11 |
| LocalizationEditorHelper | Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs | 1 |
| BagCarouselController | Assets/Scripts/UI/Inventory/BagCarouselController.cs | 11 |
| BagClubCard | Assets/Scripts/UI/Inventory/BagClubCard.cs | 20 |
| BagClubModalController | Assets/Scripts/UI/Inventory/BagClubModalController.cs | 6 |
| BagDetailPanel | Assets/Scripts/UI/Inventory/BagDetailPanel.cs | 10 |
| BagSelectionModalController | Assets/Scripts/UI/Inventory/BagSelectionModalController.cs | 4 |
| BagThumbnailCard | Assets/Scripts/UI/Inventory/BagThumbnailCard.cs | 6 |
| BallCarouselController | Assets/Scripts/UI/Inventory/BallCarouselController.cs | 10 |
| BallCompareController | Assets/Scripts/UI/Inventory/BallCompareController.cs | 35 |
| BallDatabaseCSV | Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs | 1 |
| BallDetailPanel | Assets/Scripts/UI/Inventory/BallDetailPanel.cs | 24 |
| BallThumbnailCard | Assets/Scripts/UI/Inventory/BallThumbnailCard.cs | 6 |
| ClubCarouselController | Assets/Scripts/UI/Inventory/ClubCarouselController.cs | 9 |
| ClubCompareController | Assets/Scripts/UI/Inventory/ClubCompareController.cs | 50 |
| ClubDatabaseCSV | Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs | 1 |
| ClubDetailPanel | Assets/Scripts/UI/Inventory/ClubDetailPanel.cs | 36 |
| ClubFilterBar | Assets/Scripts/UI/Inventory/ClubFilterBar.cs | 1 |
| ClubLevelUpModalController | Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs | 57 |
| ClubThumbnailCard | Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs | 10 |
| InventoryScreenController | Assets/Scripts/UI/Inventory/InventoryScreenController.cs | 4 |
| ItemCarouselController | Assets/Scripts/UI/Inventory/ItemCarouselController.cs | 10 |
| ItemDatabaseCSV | Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs | 1 |
| ItemDetailPanel | Assets/Scripts/UI/Inventory/ItemDetailPanel.cs | 15 |
| ItemThumbnailCard | Assets/Scripts/UI/Inventory/ItemThumbnailCard.cs | 7 |
| ItemUseClubCard | Assets/Scripts/UI/Inventory/ItemUseClubCard.cs | 20 |
| ItemUseModalController | Assets/Scripts/UI/Inventory/ItemUseModalController.cs | 7 |
| ItemThumbnailCardBuilder | Assets/Scripts/UI/Inventory/Editor/ItemThumbnailCardBuilder.cs | 1 |
| ItemUseClubCardBuilder | Assets/Scripts/UI/Inventory/Editor/ItemUseClubCardBuilder.cs | 1 |
| ModalController | Assets/Scripts/UI/Modals/ModalController.cs | 2 |
| CharacterLevelUpDatabase | Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs | 1 |
| PlayerCharacterData | Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs | 14 |
| CharacterDatabase | Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs | 18 |
| CharacterDatabaseCSV | Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs | 1 |
| CarouselController | Assets/Scripts/UI/Roster/UI/CarouselController.cs | 9 |
| CharacterDetailPanel | Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs | 29 |
| CharacterThumbnailCard | Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs | 11 |
| CompareController | Assets/Scripts/UI/Roster/UI/CompareController.cs | 36 |
| LevelUpModalController | Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs | 48 |
| RosterScreenController | Assets/Scripts/UI/Roster/UI/RosterScreenController.cs | 2 |
| StatBar | Assets/Scripts/UI/Roster/UI/StatBar.cs | 7 |
| RuntimeActiveStateManager | Assets/Scripts/Utilities/RuntimeActiveStateManager.cs | 2 |

## CSV Data Files

### Bags.csv
```
id,name,rarity,thumbnail,fullImage,description,unlocked
bag_mireo,Mireo,Rare,Mireo,Mireo,Add any 8 clubs you want to take out to the field to your bag. Remember you always need at least 1 Driver and 1 Putter.,TRUE
```
(11 rows)

### Balls.csv
```
id,name,brand,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info
ball_golfin,Golfin,Golfin,0,0,0,0,0,Golfin,Golfin,"The standard Golfin ball. Perfectly balanced with no stat bonuses or penaltiesƒ?"reliable in any situation."
```
(3 rows)

### Characters.csv
```
id,name,lastName,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina,portraitSprite,portraitFull,startLevel,maxLevel,bio
char_james,James,Cartwright,Common,6,7,6,6,James,BigRosterJames,10,39,A dependable player just starting out on the tour.
```
(13 rows)

### Clubs.csv
```
id,name,type,rarity,brand,basePower,baseAccuracy,baseLieResistance,baseLoft,maxDurability,baseDistance,portraitSprite,portraitFull,startLevel,maxLevel,info
club_driver_gf,Driver G&F,Driver,Common,G&F,80,30,10,12,100,250,Driver-G&F,Driver-G&F,10,39,A reliable driver from G&F with balanced power and solid accuracy off the tee.
```
(7 rows)

### HoleDatabase.csv
```
courseNameKey,holeNumber,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount
HOLE_LOMOND_5,5,Points,100,RepairKit,10,Ball,30
```
(6 rows)

### Items.csv
```
id,name,category,rarity,restorePercent,thumbnailSprite,fullSprite,proTip,info
repairkit_common,Repair Kit,RepairKit,Common,50,RepairKit-Common,RepairKit-Common,"Clubs will automatically use the best repair kit available when you repair them from the Clubs tab.","Essential and efficient, this Repair Kit restores up to 50% of any club's durability. Designed for quick fixes and reliable performance, it's a must have for keeping your equipment in solid shape round after round."
```
(4 rows)

### LevelUpCosts.csv
```
level,cost_r,sp_reward
1,5,1
```
(241 rows)

## Quick Health

### Potential Missing Methods on CharacterManager
```
WARNING: CharacterManager.OnCharacterLeveledUp() called but not found as public method
WARNING: CharacterManager.OnCharacterSelected() called but not found as public method
```

---
End of audit.
