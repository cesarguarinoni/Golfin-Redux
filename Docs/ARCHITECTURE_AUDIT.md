# Architecture Audit

> Auto-generated 2026-03-25 09:51. Do not edit manually.

## File Tree (Scripts)

```
Assets/Scripts/Audio/AudioManager.cs
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
Assets/Scripts/Editor/ScreenshotTool.cs
Assets/Scripts/UI/AboutSubmenu.cs
Assets/Scripts/UI/Editor/LocalizationEditorHelper.cs
Assets/Scripts/UI/FadeController.cs
Assets/Scripts/UI/HoleData.cs
Assets/Scripts/UI/HoleDatabase.cs
Assets/Scripts/UI/HoleDatabaseLoader.cs
Assets/Scripts/UI/HomeScreenController.cs
Assets/Scripts/UI/Inventory/ClubCarouselController.cs
Assets/Scripts/UI/Inventory/ClubCompareController.cs
Assets/Scripts/UI/Inventory/ClubData.cs
Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs
Assets/Scripts/UI/Inventory/ClubDetailPanel.cs
Assets/Scripts/UI/Inventory/ClubFilterBar.cs
Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs
Assets/Scripts/UI/Inventory/Editor/ClubCompareAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubCompareRightPanelBuilder.cs
Assets/Scripts/UI/Inventory/Editor/ClubDetailPanelAutoWire.cs
Assets/Scripts/UI/Inventory/Editor/ClubManagerSetup.cs
Assets/Scripts/UI/Inventory/Editor/ClubThumbnailCardBuilder.cs
Assets/Scripts/UI/Inventory/Editor/InventoryScreenBuilder.cs
Assets/Scripts/UI/Inventory/InventoryScreenController.cs
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
Assets/Data/LevelUpCosts.csv
Assets/Data/LevelUpCosts.csv.meta
Assets/Data/README_HOLES.md
Assets/Data/README_HOLES.md.meta
```

## MonoBehaviours

| Class | File | Singleton | Key Interfaces |
|---|---|---|---|
| CharacterManager | Assets/Scripts/CharacterManager.cs | Yes |  |
| ClubManager | Assets/Scripts/ClubManager.cs | Yes |  |
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
| ClubCarouselController | Assets/Scripts/UI/Inventory/ClubCarouselController.cs | Yes |  |
| ClubCompareController | Assets/Scripts/UI/Inventory/ClubCompareController.cs | Yes |  |
| ClubDatabaseCSV | Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs | Yes |  |
| ClubDetailPanel | Assets/Scripts/UI/Inventory/ClubDetailPanel.cs | Yes |  |
| ClubFilterBar | Assets/Scripts/UI/Inventory/ClubFilterBar.cs |  |  |
| ClubThumbnailCard | Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs | Yes |  |
| InventoryScreenController | Assets/Scripts/UI/Inventory/InventoryScreenController.cs |  |  |
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

- **CharacterManager** (Assets/Scripts/CharacterManager.cs) (DontDestroyOnLoad)
- **ClubManager** (Assets/Scripts/ClubManager.cs) (DontDestroyOnLoad)
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
| CharacterManager | `public event System.Action<string>? OnCharacterLeveledUp;` |
| CharacterManager | `public event System.Action<string>? OnCharacterSelected;` |
| CharacterManager | `public event System.Action? OnRosterChanged;` |
| ClubManager | `public event System.Action<string>? OnClubEquipped;` |
| ClubManager | `public event System.Action<string>? OnClubLeveledUp;` |
| ClubManager | `public event System.Action? OnInventoryChanged;` |
| SettingsMenuItem | `public event System.Action<SettingsMenuItem> OnExpanded;` |
| SettingsMenuItem | `public event System.Action<SettingsMenuItem> OnCollapsed;` |
| ClubCarouselController | `public event System.Action<string>? OnClubSelected;` |
| ClubFilterBar | `public event System.Action<ClubType?>? OnFilterChanged;` |
| RewardPointsManager | `public event System.Action<int> OnPointsChanged;` |
| CarouselController | `public event System.Action<string> OnCharacterSelected; // Change to event` |

## Serialized Fields Summary

| Class | File | SerializeField Count |
|---|---|---|
| CharacterManager | Assets/Scripts/CharacterManager.cs | 2 |
| AudioManager | Assets/Scripts/Audio/AudioManager.cs | 3 |
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
| ClubCarouselController | Assets/Scripts/UI/Inventory/ClubCarouselController.cs | 9 |
| ClubCompareController | Assets/Scripts/UI/Inventory/ClubCompareController.cs | 48 |
| ClubDatabaseCSV | Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs | 1 |
| ClubDetailPanel | Assets/Scripts/UI/Inventory/ClubDetailPanel.cs | 33 |
| ClubFilterBar | Assets/Scripts/UI/Inventory/ClubFilterBar.cs | 1 |
| ClubThumbnailCard | Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs | 10 |
| InventoryScreenController | Assets/Scripts/UI/Inventory/InventoryScreenController.cs | 4 |
| ModalController | Assets/Scripts/UI/Modals/ModalController.cs | 2 |
| CharacterLevelUpDatabase | Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs | 1 |
| PlayerCharacterData | Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs | 14 |
| CharacterDatabase | Assets/Scripts/UI/Roster/Managers/CharacterDatabase.cs | 18 |
| CharacterDatabaseCSV | Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs | 1 |
| CarouselController | Assets/Scripts/UI/Roster/UI/CarouselController.cs | 9 |
| CharacterDetailPanel | Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs | 29 |
| CharacterThumbnailCard | Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs | 11 |
| CompareController | Assets/Scripts/UI/Roster/UI/CompareController.cs | 32 |
| LevelUpModalController | Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs | 48 |
| RosterScreenController | Assets/Scripts/UI/Roster/UI/RosterScreenController.cs | 2 |
| StatBar | Assets/Scripts/UI/Roster/UI/StatBar.cs | 7 |
| RuntimeActiveStateManager | Assets/Scripts/Utilities/RuntimeActiveStateManager.cs | 2 |

## CSV Data Files

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
