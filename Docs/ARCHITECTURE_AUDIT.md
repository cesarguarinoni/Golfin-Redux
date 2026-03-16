# Architecture Audit: MonoBehaviour Summary & Relationships

> Generated 2026-03-16. Covers all 31 MonoBehaviours and 10 supporting data classes in `Assets/Scripts/`.

---

## Dependency Graph (High-Level)

```
                        ┌──────────────────┐
                        │  CharacterManager │ (Singleton, DontDestroyOnLoad)
                        │  - owns roster    │
                        │  - level-up logic │
                        └──────┬───────────┘
                               │ events: OnCharacterLeveledUp,
                               │         OnCharacterSelected,
                               │         OnRosterChanged
               ┌───────────────┼───────────────────────┐
               ▼               ▼                        ▼
    ┌──────────────────┐ ┌───────────────┐  ┌─────────────────────┐
    │RosterScreenCtrl  │ │CarouselCtrl   │  │CharacterDetailPanel │
    │ - subscribes to  │ │ - populates   │  │ - subscribes to     │
    │   managers       │ │   cards from  │  │   carousel selection│
    │ - displays RP    │ │   CharMgr     │  │ - reads CharMgr     │
    └──────┬───────────┘ └──────┬────────┘  └─────────────────────┘
           │                    │
           │                    ▼
           │           ┌──────────────────┐
           │           │ThumbnailCard     │
           │           │ - OnClicked      │
           │           │ - reads CharMgr  │
           │           └──────────────────┘
           │
           ▼
    ┌──────────────────┐      ┌──────────────────┐
    │RewardPointsManager│◄────│HomeScreenCtrl    │
    │ (Singleton)       │     │ - reads RP, holes│
    │ OnPointsChanged   │     │ - triggers nav   │
    └──────────────────┘      └──────┬───────────┘
                                     │
                                     ▼
                              ┌──────────────────┐
                              │  ScreenManager   │◄──── LogoScreenCtrl
                              │  - screen nav    │◄──── SplashScreenCtrl
                              │  - fade via      │◄──── LoadingScreenCtrl
                              │    FadeController│
                              └──────────────────┘
                                     │
                                     ▼
                              ┌──────────────────┐
                              │  FadeController  │ (Singleton)
                              │  - CanvasGroup   │
                              │    alpha fades   │
                              └──────────────────┘

    ┌──────────────────┐      ┌──────────────────┐
    │PersistentUIManager│─────│SettingsCtrlPhase2│
    │ (Singleton)       │     │ - accordion menus│
    │ - top/bottom bars │     │ - submenu refs   │
    └──────────────────┘      └──────┬───────────┘
                                     │ wires
                    ┌────────┬───────┼────────┬────────┐
                    ▼        ▼       ▼        ▼        ▼
              UserProfile SoundSub Language  About  SettingsMenuItem
              Submenu     Submenu  Submenu   Submenu  (accordion item)
                 │           │
                 │           ▼
                 │     AudioManager (Singleton)
                 ▼
           PersistentUIManager (username broadcast)
```

---

## Singletons (7)

| Class | Location | DontDestroyOnLoad | Purpose |
|---|---|---|---|
| `CharacterManager` | `Scripts/CharacterManager.cs` | Yes | Character roster, selection, level-up |
| `AudioManager` | `Scripts/Audio/AudioManager.cs` | Yes | Music/SFX playback, volume persistence |
| `FadeController` | `Scripts/UI/FadeController.cs` | Optional | Screen fade transitions |
| `PersistentUIManager` | `Scripts/UI/PersistentUIManager.cs` | Yes | Top bar + bottom nav visibility |
| `RewardPointsManager` | `Scripts/UI/Roster/Managers/RewardPointsManager.cs` | Yes | R-point currency management |
| `CharacterLevelUpDatabase` | `Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs` | No | Level economy CSV lookup |
| `CharacterDatabaseCSV` | `Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` | Yes | Runtime CSV character loader |

---

## Events

| Publisher | Event | Signature | Subscribers |
|---|---|---|---|
| `CharacterManager` | `OnCharacterLeveledUp` | `Action<string>` (characterId) | RosterScreenController, CharacterDetailPanel |
| `CharacterManager` | `OnCharacterSelected` | `Action<string>` (characterId) | RosterScreenController |
| `CharacterManager` | `OnRosterChanged` | `Action` | RosterScreenController, CarouselController |
| `RewardPointsManager` | `OnPointsChanged` | `Action<int>` (newPoints) | RosterScreenController, HomeScreenController |
| `CarouselController` | `OnCharacterSelected` | `Action<string>` (characterId) | CharacterDetailPanel |
| `CharacterThumbnailCard` | `OnClicked` | `Action` | CarouselController (internal wiring) |
| `SettingsMenuItem` | `OnExpanded` | `Action<SettingsMenuItem>` | SettingsControllerPhase2 |
| `SettingsMenuItem` | `OnCollapsed` | `Action<SettingsMenuItem>` | SettingsControllerPhase2 |
| `SwipeDetector` | `onSwipeLeft` | `UnityEvent` | Inspector-wired (carousel, news) |
| `SwipeDetector` | `onSwipeRight` | `UnityEvent` | Inspector-wired (carousel, news) |

---

## MonoBehaviour Catalog (31)

### Core Managers

#### CharacterManager
- **File:** `Assets/Scripts/CharacterManager.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Central hub for character ownership, selection, level-up, and stat allocation.
- **Dependencies:** CharacterDatabase, CharacterLevelUpDatabase, ManualSPAllocation, PlayerCharacterData
- **Events:** OnCharacterLeveledUp, OnCharacterSelected, OnRosterChanged
- **Serialized:** characterDatabase, levelUpDatabase

#### AudioManager
- **File:** `Assets/Scripts/Audio/AudioManager.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Global music/SFX playback with pooled AudioSources and PlayerPrefs persistence.
- **Dependencies:** AudioSource, PlayerPrefs
- **Serialized:** musicSource, sfxSources, musicVolume, sfxVolume, sfxPoolSize

#### RewardPointsManager
- **File:** `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Manages R-point currency with earn/spend/check affordability.
- **Dependencies:** PlayerPrefs
- **Events:** OnPointsChanged
- **Serialized:** None

#### CharacterLevelUpDatabase
- **File:** `Assets/Scripts/UI/Roster/Data/CharacterLevelUpDatabase.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Loads level-up progression from CSV (cost per level, SP reward per level).
- **Dependencies:** TextAsset (CSV), CharacterLevelUpData
- **Serialized:** levelUpCostsCsv

#### CharacterDatabaseCSV
- **File:** `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Runtime CSV loader for character templates (alternative to ScriptableObject database).
- **Dependencies:** TextAsset (CSV), Sprite[]
- **Serialized:** charactersCSV, characterPortraits

### Screen Navigation

#### ScreenManager
- **File:** `Assets/Scripts/UI/ScreenManager.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Central screen navigation controlling Logo, Splash, Loading, Home, Roster activation.
- **Dependencies:** FadeController, GameObject references per screen
- **Serialized:** _initialScreen, _logoScreen, _splashScreen, _loadingScreen, _homeScreen, _rosterScreen

#### FadeController
- **File:** `Assets/Scripts/UI/FadeController.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Fade-to/from-black using CanvasGroup alpha. Used by ScreenManager for transitions.
- **Dependencies:** CanvasGroup
- **Serialized:** _defaultDuration

#### PersistentUIManager
- **File:** `Assets/Scripts/UI/PersistentUIManager.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Controls top bar (RP display, username, settings button) and bottom nav bar visibility.
- **Dependencies:** SettingsController, SettingsControllerPhase2, Button, Image, TextMeshProUGUI
- **Serialized:** topBarPanel, bottomNavPanel, navigation buttons, highlight images, text fields

### Screen Controllers

#### LogoScreenController
- **File:** `Assets/Scripts/UI/LogoScreenController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Orchestrates fade-in/hold/fade-out logo sequence, then auto-transitions to Splash.
- **Dependencies:** CanvasGroup, ScreenManager
- **Serialized:** _fadeInDuration, _holdDuration, _fadeOutDuration

#### SplashScreenController
- **File:** `Assets/Scripts/UI/SplashScreenController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** START button handler, transitions to Loading screen.
- **Dependencies:** ScreenManager
- **Serialized:** None (wired via Inspector onClick)

#### LoadingScreenController
- **File:** `Assets/Scripts/UI/LoadingScreenController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Progress bar animation with minimum display time, then transitions to Home.
- **Dependencies:** LoadingBar, ScreenManager, TextMeshProUGUI, LocalizationManager
- **Serialized:** loadingBar, progressText, nowLoadingText, screenManager, minLoadingTime

#### HomeScreenController
- **File:** `Assets/Scripts/UI/HomeScreenController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Home hub screen: news carousel, character display, next hole panel, navigation buttons.
- **Dependencies:** ScreenManager, PersistentUIManager, SettingsController, HoleDatabaseLoader, LocalizationManager
- **Serialized:** screenManager, text fields, buttons, sprites, holeDatabase, news config

### Roster UI

#### RosterScreenController
- **File:** `Assets/Scripts/UI/Roster/UI/RosterScreenController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Top-level roster screen; displays RP, subscribes to CharacterManager and RewardPointsManager events.
- **Dependencies:** CarouselController, CharacterManager, RewardPointsManager, TextMeshProUGUI
- **Serialized:** rewardPointsText, carousel

#### CarouselController
- **File:** `Assets/Scripts/UI/Roster/UI/CarouselController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Horizontal character card carousel with pagination, arrow nav, and card selection.
- **Dependencies:** CharacterManager, CharacterThumbnailCard (prefab), CharacterDetailPanel, ScrollRect, Button
- **Events:** OnCharacterSelected
- **Serialized:** contentParent, characterCardPrefab, detailPanel, arrows, pagination, cardsPerPage, scrollSmoothness

#### CharacterDetailPanel
- **File:** `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Full character detail modal with portrait, stats, level, bio. Updates on carousel selection.
- **Dependencies:** CarouselController (subscribes to OnCharacterSelected), CharacterManager
- **Serialized:** characterImage, text fields, levelUpButton, selectButton

#### CharacterThumbnailCard
- **File:** `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Individual carousel card (portrait, name, rarity badge, level, selection highlight).
- **Dependencies:** CharacterManager, RarityHelper, Button
- **Events:** OnClicked
- **Serialized:** portraitImage, nameText, rarityLabelText, levelText, badge/highlight/background images, cardButton

#### StatBar
- **File:** `Assets/Scripts/UI/Roster/UI/StatBar.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Reusable stat visualization: icon, label, animated fill bar, value text with color coding.
- **Dependencies:** Image, TextMeshProUGUI
- **Serialized:** icon, label, fillBar, valueText, normalColor, criticalColor, maxColor

### Settings System

#### SettingsController (Phase 1)
- **File:** `Assets/Scripts/UI/SettingsController.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Phase 1 flat settings menu with static button list.
- **Dependencies:** Button, Image, TextMeshProUGUI
- **Serialized:** background, settingsPanel, 9 menu buttons with icons/labels/arrows

#### SettingsControllerPhase2
- **File:** `Assets/Scripts/UI/SettingsControllerPhase2.cs`
- **Inherits:** MonoBehaviour (Singleton)
- **Purpose:** Phase 2 accordion settings with expandable menu items and submenu integration.
- **Dependencies:** SettingsMenuItem, UserProfileSubmenu, SoundSettingsSubmenu, LanguageSubmenu, AboutSubmenu, ModalController
- **Serialized:** background, settingsPanel, 4 accordion items, 5 buttons, 4 submenu refs, logOutModal

#### SettingsMenuItem
- **File:** `Assets/Scripts/UI/SettingsMenuItem.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Individual accordion item with expand/collapse animation and arrow rotation.
- **Events:** OnExpanded, OnCollapsed
- **Serialized:** button, submenuContainer, arrowIcon, expandDuration, collapseDuration, expandCurve, submenuHeight

#### UserProfileSubmenu
- **File:** `Assets/Scripts/UI/UserProfileSubmenu.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Username editing with validation; account linking UI (Phase 3).
- **Dependencies:** PersistentUIManager (username broadcast), TMP_InputField, Button
- **Serialized:** usernameInputField, saveUsernameButton, feedbackText, link buttons/indicators, username length constraints

#### SoundSettingsSubmenu
- **File:** `Assets/Scripts/UI/SoundSettingsSubmenu.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Music/SFX volume sliders with real-time AudioManager integration.
- **Dependencies:** AudioManager, Slider, TextMeshProUGUI
- **Serialized:** musicVolumeSlider, sfxVolumeSlider, musicVolumeText, sfxVolumeText

#### LanguageSubmenu
- **File:** `Assets/Scripts/UI/LanguageSubmenu.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Language selection (English/Japanese) with LocalizationManager integration.
- **Dependencies:** LocalizationManager (subscribes to OnLanguageChanged), Button, PlayerPrefs
- **Serialized:** englishButton, japaneseButton, checkmarks, selectedColor, unselectedColor

#### AboutSubmenu
- **File:** `Assets/Scripts/UI/AboutSubmenu.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Displays app version and license text.
- **Dependencies:** TextMeshProUGUI, Application.version
- **Serialized:** versionText, licensesText, appName, useApplicationVersion, fallbackVersion

### Modals & Utilities

#### ModalController
- **File:** `Assets/Scripts/UI/Modals/ModalController.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Base class for modal dialogs with fade animation, backdrop, and Show/Hide lifecycle.
- **Dependencies:** Button, CanvasGroup
- **Serialized:** modalPanel, backdrop, closeButton, useAnimation, animationDuration

#### SwipeDetector
- **File:** `Assets/Scripts/UI/SwipeDetector.cs`
- **Inherits:** MonoBehaviour (IBeginDragHandler, IEndDragHandler)
- **Purpose:** Detects horizontal swipes for carousel/news navigation.
- **Events:** onSwipeLeft (UnityEvent), onSwipeRight (UnityEvent)
- **Serialized:** swipeThreshold, onSwipeLeft, onSwipeRight

#### ProTipCard
- **File:** `Assets/Scripts/UI/ProTipCard.cs`
- **Inherits:** MonoBehaviour (IPointerClickHandler)
- **Purpose:** Rotating tip display with auto-cycle and tap-to-advance on Loading screen.
- **Dependencies:** TextMeshProUGUI, Image, LocalizedText, LayoutRebuilder
- **Serialized:** headerText, tipText, tapNextText, tipSprites, tipKeys, autoCycleInterval, textFadeDuration

#### LoadingBar
- **File:** `Assets/Scripts/UI/LoadingBar.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Simple animated progress bar using Image.fillAmount.
- **Dependencies:** Image
- **Serialized:** fillImage, smoothSpeed

#### ScreenDeactivator
- **File:** `Assets/Scripts/UI/ScreenDeactivator.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Editor/runtime tool to auto-deactivate non-active screens for clean scene state.
- **Dependencies:** None (uses reflection/FindObjectsOfType)
- **Serialized:** activeScreenNames, searchRoot, screenTag

### Data Loaders

#### HoleDatabaseLoader
- **File:** `Assets/Scripts/UI/HoleDatabaseLoader.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Parses hole CSV at runtime into HoleDatabase with reward data.
- **Dependencies:** TextAsset, HoleData, HoleDatabase
- **Serialized:** holeDatabaseCSV, autoLoadOnAwake

#### ExampleAutoWireScreen
- **File:** `Assets/Scripts/UI/ExampleAutoWireScreen.cs`
- **Inherits:** MonoBehaviour
- **Purpose:** Demo/template showing UIAutoWire usage for naming-convention-based component discovery.
- **Dependencies:** UIAutoWire, Button, Image, TextMeshProUGUI
- **Serialized:** panel, background, closeButton, confirmButton, titleText, iconImage

---

## Data Classes (Non-MonoBehaviour)

| Class | File | Type | Purpose |
|---|---|---|---|
| `PlayerCharacterData` | `Roster/Data/PlayerCharacterData.cs` | Plain C# | Runtime character instance: level, SP earned/spent, stats, pending allocation |
| `StatAllocationStrategy` | `Roster/Data/StatAllocationStrategy.cs` | Abstract | Base for SP allocation strategies |
| `ManualSPAllocation` | `Roster/Data/ManualSPAllocation.cs` | Concrete | Player-controlled SP allocation (current default) |
| `AutomaticStatAllocation` | `Roster/Data/AutomaticStatAllocation.cs` | Concrete | Auto SP distribution with multiple formulas (future use) |
| `RarityStatCaps` | `Roster/Data/RarityStatCaps.cs` | Static | Rarity-based stat maximums (Common 25 to Supreme 50) |
| `CharacterLevelUpData` | `Roster/Data/CharacterLevelUpData.cs` | Plain C# | Single level record: level, cost_r, sp_reward |
| `CharacterDatabase` | `Roster/Managers/CharacterDatabase.cs` | ScriptableObject | Container for CharacterData templates + CharacterRarity enum + RarityHelper |
| `CharacterData` | (inside CharacterDatabase.cs) | Serializable | Character template: base stats, portrait, rarity, localization keys |
| `HoleData` | `UI/HoleData.cs` | Serializable | Hole definition with localization key and rewards list |
| `HoleDatabase` | `UI/HoleDatabase.cs` | ScriptableObject | Container for HoleData collection |
| `UIAutoWire` | `Utilities/UIAutoWire.cs` | Static | Component auto-discovery by hierarchy path |

---

## Relationship Patterns

### 1. Singleton Access
Most runtime communication flows through singletons accessed via `Instance`:
- `CharacterManager.Instance` — roster queries and mutations
- `RewardPointsManager.Instance` — currency checks
- `AudioManager.Instance` — volume control
- `PersistentUIManager.Instance` — top/bottom bar updates

### 2. Event-Driven Updates
UI updates propagate via C# `Action` delegates, not direct polling:
- CharacterManager fires roster/selection/level-up events
- RewardPointsManager fires OnPointsChanged
- CarouselController fires OnCharacterSelected (forwarded from card clicks)
- SettingsMenuItem fires OnExpanded/OnCollapsed

### 3. Screen Lifecycle
`ScreenManager` activates/deactivates screen GameObjects. `FadeController` wraps transitions. Each screen controller (Logo, Splash, Loading, Home, Roster) manages its own initialization in `OnEnable`.

### 4. Settings Accordion Chain
`SettingsControllerPhase2` → `SettingsMenuItem[]` → individual submenus (`UserProfileSubmenu`, `SoundSettingsSubmenu`, `LanguageSubmenu`, `AboutSubmenu`). Only one item expanded at a time, enforced by SettingsControllerPhase2.

### 5. Data Flow: Character Level-Up
```
User taps Level Up → CharacterDetailPanel
  → CharacterManager.LevelUp(characterId)
    → CharacterLevelUpDatabase.GetLevelData(level)
    → RewardPointsManager.Spend(cost)
    → PlayerCharacterData.AddSP(earned)
    → ManualSPAllocation (player allocates)
    → fires OnCharacterLeveledUp
      → RosterScreenController updates
      → CharacterDetailPanel refreshes
```

### 6. Dual Settings Controllers
Both `SettingsController` (Phase 1, flat) and `SettingsControllerPhase2` (accordion) exist. `PersistentUIManager` references both; the active one depends on which is enabled in the scene.
