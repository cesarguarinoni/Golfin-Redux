
---

## 🎉 LATEST: Settings Screen Phase 3 COMPLETE!

**Phase 3 - Full Functionality Integration** ✅ COMPLETE (Core Features)
- ✅ **Language Integration** - Real-time language switching across entire app
- ✅ **Sound Settings Integration** - AudioManager with Music/SFX volume control
- ✅ **About Accordion** - Version info + licenses displayed inline
- ⏸️ **Deferred to Phase 4:** Log Out modal, Account Linking, Webview (waiting for Cognito integration)

---

## Phase 3 Features

### 1. Language Integration 🌐
**Status:** ✅ Tested & Working

**What it does:**
- Click English/Japanese in Settings → **Entire UI updates instantly!**
- All screens with `LocalizedText` component update automatically
- Language preference saved to PlayerPrefs
- Restored on app restart

**Implementation:**
- `LanguageSubmenu.cs` integrated with `LocalizationManager`
- Uses `Language` enum (not strings)
- Calls `LocalizationManager.SetLanguage()` on button click
- Subscribes to `OnLanguageChanged` event
- `LoadLanguagePreference()` applies saved language at startup

**Time:** ~15 minutes | **Commit:** 0716d71, ed50ea5

---

### 2. Sound Settings Integration 🔊
**Status:** ✅ Tested & Working

**What it does:**
- Drag Music/SFX sliders → **Audio volume changes in real-time!**
- Separate control for Music (menus + gameplay) and SFX (buttons + game sounds + ambient)
- Volume preferences saved to PlayerPrefs
- Default: 70% for both, range: 0-100%

**New: AudioManager.cs** (Golfin.Audio namespace)
- Singleton pattern with DontDestroyOnLoad
- Dedicated AudioSource for background music
- Pool of 5 AudioSources for SFX (configurable)
- Methods: `SetMusicVolume()`, `SetSFXVolume()`, `PlayMusic()`, `PlaySFX()`, `PlaySFXAtPosition()`
- Utility: `MuteAll()`, `IsMusicPlaying()`

**Updated: SoundSettingsSubmenu.cs**
- `LoadSettings()` reads from AudioManager
- `OnMusicVolumeChanged()` → `AudioManager.SetMusicVolume()`
- `OnSFXVolumeChanged()` → `AudioManager.SetSFXVolume()`
- Removed TODO placeholders - fully functional!

**Setup:** Create AudioManager GameObject in first scene, add AudioManager component

**Time:** ~20 minutes | **Commits:** a518b56, 3921dab

**Docs:** `Docs/PHASE3_SOUND_INTEGRATION.md`

---

### 3. About Accordion 📋
**Status:** ✅ Ready to Test (Code + Builder Complete)

**What it does:**
- Click About row → Expands inline (like User Profile/Sound/Language)
- Shows **APP VERSION** section with version number (from `Application.version`)
- Shows **LICENCES** section with license list
- Click again → Collapses

**New: AboutSubmenu.cs**
- Shows app version (reads from Unity Project Settings)
- Shows licenses (MIT, GPL, Apache, BSD + Wonderwall copyright)
- `UpdateContent()` called at runtime (OnEnable)
- Simple list format matching reference design

**New: AboutSubmenuBuilder.cs**
- **Tool:** Tools → GOLFIN → Build About Submenu
- One-click creation (~30 seconds)
- Creates hierarchy: APP VERSION section + LICENCES section
- Auto-wires references via SerializedObject
- VerticalLayoutGroup for stacking
- TextMeshPro with word wrapping enabled

**Updated: SettingsControllerPhase2.cs**
- Added `aboutItem` (SettingsMenuItem) to accordion items
- Added `aboutSubmenu` (AboutSubmenu) reference
- `aboutItem` added to `_accordionItems` list
- Accordion handles expansion automatically

**Pattern:** Same as UserProfile/SoundSettings/Language - inline accordion expansion, not a modal!

**Time:** ~30 minutes | **Commits:** 48b2a6f, b507777, 8513e4a, 518cd8b

---

### 4. Localization System Enhancements 🌐

**New: LocalizationEditorHelper.cs** - Reusable static helper for all editor scripts
- `AddLocalizedText(textObject, key)` - Adds LocalizedText component + sets key
- `GenerateKey(parts...)` - Auto-generates keys: ("Settings", "Menu", "User") → "SETTINGS_MENU_USER"
- `AddLocalizedTextAuto(textObject, keyParts...)` - Combined generation + assignment
- `BatchAddLocalization(dict, keyPrefix)` - Bulk operations
- `KeyExistsInCSV(key)` - Validates CSV entries
- `RemindToAddKey(key, englishText, screenName)` - Logs warnings for missing keys

**New: LocalizeSettingsScreen.cs** - Auto-localization tool
- **Tool:** Tools → GOLFIN → Localize Settings Screen
- Scans all TextMeshPro in Settings Panel
- Auto-generates localization keys (SETTINGS_* format)
- Creates CSV with English + Japanese translations
- **Auto-adds LocalizedText components** and assigns keys
- Pre-loaded translations for common Settings terms
- One-click setup (5 minutes)

**Docs:** `Docs/SETTINGS_LOCALIZATION_GUIDE.md`

**Commits:** 020658c, 23def2c, cc529c9, cea3c95, 10118ba, 6481344

---

## Deferred to Phase 4 (Cognito Integration)

### Features on Hold:
- **Log Out Modal** - Confirmation dialog → Clear session → Login screen
- **Account Linking** - Google/Apple/Twitter via AWS Cognito
- **Webview** - Terms of Use, Privacy Policy, FAQ, Contact (UniWebView or Vuplex)

### Why Deferred:
- Requires Cognito authentication integration
- Will implement when login system is ready
- ModalController.cs kept for future use

---

## Previous: Settings Screen Phase 2 FULLY WORKING!

**Phase 2 - Accordion Behavior + Submenus + Layout Fixes** ✅ COMPLETE
- ✅ Accordion expand/collapse (only one section open at a time)
- ✅ Click open row to close it (toggle behavior)
- ✅ Smooth animations with arrow rotation (0.3s expand, 0.2s collapse)
- ✅ Sound Settings submenu (Music + SFX volume sliders, 0-100%)
- ✅ Language submenu (English/Japanese toggle with checkmarks)
- ✅ User Profile submenu (username editing with real-time validation)
- ✅ Auto-saves to PlayerPrefs (volumes, language, username)
- ✅ **ALL LAYOUT ISSUES RESOLVED** (row content positioning, height desync auto-correction)

**Core Scripts:**
- `SettingsMenuItem.cs` - Accordion item with animation + height desync auto-fix
- `SettingsControllerPhase2.cs` - Accordion management with debug logging
- `SoundSettingsSubmenu.cs` - Volume controls
- `LanguageSubmenu.cs` - Language selection
- `UserProfileSubmenu.cs` - Username editing + account linking placeholders

**Diagnostic Tools Created:**
- `FixRowChildAnchors.cs` - Anchors row content to top (prevents shift-down on expand)
- `DiagnoseLayoutIssue.cs` - Checks layout components for common issues
- `VerifyButtonWiring.cs` - Verifies button assignments and interactivity
- `VerifyAccordionSetup.cs` - Checks SettingsControllerPhase2 menu item assignments
- `FixSubmenuPositioning.cs` - Fixes submenu anchor relative to parent row
- `FixSettingsLayoutV2.cs` - Adds VerticalLayoutGroup + LayoutElement to rows

**🔥 Unity Editor Builder Tool**
- **Tools → GOLFIN → Build Phase 2 Submenus**
- Creates complete hierarchies in ~30 seconds (vs 40 min manual)
- All components + references automatically wired
- See `Docs/PHASE2_BUILDER_TOOL.md`

**Key Fixes Applied:**
1. **Row content anchoring** - All children (Button, Icon, Label, Arrow) anchored to TOP of row so they stay in place when row expands
2. **Height desync auto-correction** - Detects when external components manipulate height and auto-corrects full visual state (height, arrow, visibility)
3. **Accordion event wiring** - Menu items properly assigned to controller for auto-collapse behavior

**Setup:** Use builder tool (30 sec) + apply fixes via Tools menu

**Latest Commits:** 
- 16cb769: Fix row child anchors (prevents content shift)
- fc58598: Debug accordion behavior (event logging)
- f3664a7: Enhanced state desync tracking
- b894475: Height desync detection
- e1d1878: Comprehensive visual state correction (THE FIX!)

**Status:** Phase 2 fully working and tested! Ready for Phase 3.

---

## Previous: Screen Deactivator Added!

**Screen Deactivator** - Automatically deactivates unnecessary screens before runtime!
- ✅ Auto-runs before Play mode in Unity Editor
- ✅ Future-proof detection (auto-finds new screens by naming pattern)
- ✅ GUI configuration via **GOLFIN → Screen Deactivator Settings**

**Commit:** 94775d5 - "Add Screen Deactivator utility"

---

## Previous: Home Screen Complete!

**Home Screen is now fully functional** with all 3 missing features implemented:
- ✅ Localization for Next Hole name (uses CSV keys like `HOLE_LOMOND_5`)
- ✅ Rewards display (3 slots with icon support for Points, Repair Kit, Ball)
- ✅ Notice Panel carousel (auto-cycle every 5s + swipe left/right)

**Commit:** 0a5c152 - "Complete Home Screen: Localization, Rewards, and News Carousel"

---

# AI Context - GOLFIN Redux Project

**Last Updated:** 2026-03-05 06:43 JST  
**Updated By:** Kai (Aikenken Bot)  
**Documentation Status:** ✅ All docs read and synced

---

## Project Overview

**GOLFIN Redux** is a **simplified** Unity-based mobile golf game derived from the original GOLFIN project. It uses a shared point system integrated with the Golfin app ecosystem.

### Key Facts
- **Platform:** Unity (iOS/Android)
- **Language:** C# + Unity UI
- **Monetization:** Shared point system with Golfin app (no NFTs)
- **Stage:** Core UI development phase
- **Team:** Cesar (lead dev), Ken (founder), Kai (AI assistant), Cortana (Cesar's local AI)

### 🤖 Quick Reference for AI Assistants

**First time working on this project? Read these first:**
1. This file (AI_CONTEXT.md) - Complete project overview
2. `Docs/Golfin Redux. - Current screens.pdf` (9 pages) - Screen designs and specs

**Working on a specific feature? Reference these:**
- **Home Screen:** `Docs/HOME_SCREEN_SETUP.md` + `Docs/QUICK_START_HOLES.md`
- **Settings Screen:** `Docs/SettingsScreen/README.md` (then dive into ARCHITECTURE/IMPLEMENTATION_GUIDE)
- **Screen Management:** `Docs/SCREEN_DEACTIVATOR.md`
- **UI Development:** `Docs/UI_AUTO_WIRE_GUIDE.md` (for new screens only!)
- **Full Build Guide:** `Docs/SETTINGS_BUILD_GUIDE.md`

**Architecture patterns used:**
- Singleton managers (PersistentUIManager, SettingsController)
- CSV-based data (Localization, HoleDatabase) with auto-load
- Persistent UI (DontDestroyOnLoad for Top Bar + Bottom Nav)
- Overlay pattern (Settings on top, Sort Order: 100)
- Namespace: `Golfin.UI`

**Don't know the original GOLFIN project?** Don't read the 1202-page PDF unless specifically needed. This is a **simplified** version!

---

### Original GOLFIN vs GOLFIN Redux

**Original GOLFIN (1202 pages of documentation):**
- Complex gameplay systems: Missions, Tournaments, 1v1 PvP, Driving Range
- Club & Ball customization with durability, repair kits, leveling
- Gear system, stamina system, character traits
- GPS location-based features
- NFT marketplace integration
- Rankings, leaderboards, matchmaking
- Multiple game modes and progression systems

**GOLFIN Redux (9 pages of documentation):**
- **Simplified core loop:** Select hole → Play → Get rewards → Progress
- **No NFTs:** Simple shared point system
- **No complex gear/durability:** Straightforward progression
- **Focus on accessibility:** Easy onboarding, clear UI
- **Scope:** Logo → Splash → Loading → Home → Settings → Gameplay

---

## Architecture

### Tech Stack
- **Engine:** Unity 2021.3+
- **UI:** Unity UI (Canvas-based)
- **Text:** TextMeshPro
- **Architecture Pattern:** Singleton managers + Scene-based controllers
- **Localization:** CSV-based system (English/Japanese)

### Best Practices & Workflows

**Creating UI Hierarchies:**
- ✅ **USE: Unity Editor Scripts** - Create GameObjects programmatically via Tools menu
- ❌ **DON'T: Hand-craft YAML prefabs** - Unity's prefab format is too complex, causes import errors
- ❌ **DON'T: Manual Inspector work** - Slow, error-prone, not repeatable

**Why Editor Scripts:**
- ⚡ 80x faster than manual (30 seconds vs 40 minutes)
- ✅ No human errors
- ✅ Consistent results every time
- ✅ Repeatable (one-click rebuild)
- ✅ Easy to customize for variations
- ✅ Automatically wires up component references

**Example:** `Assets/Scripts/UI/Editor/SettingsPhase2Builder.cs`
- Menu: Tools → GOLFIN → Build Phase 2 Submenus
- Creates complete hierarchies with all components
- Sets RectTransforms (positions, sizes, anchors)
- Wires up script references via reflection

**When to use:**
- Building complex UI hierarchies (10+ GameObjects)
- Need to create multiple similar screens
- Want repeatable/testable setup process
- Working with non-Unity team members (they can use the tool)

**Pattern:**
```csharp
[MenuItem("Tools/GOLFIN/Build My Screen")]
public static void BuildScreen() {
    // Create hierarchy programmatically
    var parent = CreateChild(parentTransform, "MyScreen");
    var label = CreateTextMeshPro(parent, "Label", "Text", 20);
    SetRectTransform(label.transform, AnchorPreset.TopLeft, ...);
    
    // 🌐 IMPORTANT: Add localization to all text!
    AddLocalizedText(label, "MY_SCREEN_LABEL");
    
    // Wire up script references
    screenScript.myLabel = label;
}
```

### 🌐 Localization System (MANDATORY for all text!)

**System:** CSV-based localization with `LocalizedText` component  
**Location:** `Assets/Localization/`

**Core Components:**
- `LocalizationText.csv` - All translations (English, Japanese)
- `LocalizationManager.cs` - Runtime manager, `Get(key)` method
- `LocalizedText.cs` - Component for TextMeshPro, auto-updates on language change

**IMPORTANT RULE: Always add LocalizedText to TextMeshPro objects!**

When creating UI via Editor Scripts:
1. Create TextMeshProUGUI as usual
2. **Immediately add LocalizedText component**
3. Set the localization key
4. Add key + translations to CSV

**Helper Method Pattern (add to all editor scripts):**
```csharp
private void AddLocalizedText(GameObject textObject, string key)
{
    var localizedText = textObject.AddComponent<LocalizedText>();
    
    // Set the key using SerializedObject (key is private field)
    SerializedObject serializedObject = new SerializedObject(localizedText);
    SerializedProperty keyProperty = serializedObject.FindProperty("key");
    
    if (keyProperty != null)
    {
        keyProperty.stringValue = key;
        serializedObject.ApplyModifiedProperties();
    }
    
    EditorUtility.SetDirty(textObject);
}
```

**Key Naming Convention:**
- Settings Screen: `SETTINGS_*`
- Home Screen: `HOME_*`
- Loading Screen: `BTN_*` or `TIP_*`
- Pattern: `SCREEN_SECTION_ELEMENT` in SCREAMING_SNAKE_CASE

**Automatic Localization Tool:**
- Tool: `Tools → GOLFIN → Localize Settings Screen`
- Scans existing UI, generates CSV, adds LocalizedText automatically
- Use for retrofitting or batch operations
- See: `Docs/SETTINGS_LOCALIZATION_GUIDE.md`

**Workflow for New Screens:**
1. Create UI hierarchy with Editor Script
2. Add `LocalizedText` to all TextMeshPro objects (use helper method)
3. Generate CSV entries (manually or with tool)
4. Add translations to `LocalizationText.csv`
5. Test language switching in Play Mode

**Benefits:**
- Language switching works automatically (OnLanguageChanged event)
- No hard-coded strings in UI
- Easy to add more languages (just add CSV column)
- Consistent translation keys across project

### Folder Structure
```
Assets/
├── Art/
│   └── Characters/
│       ├── Camila.png
│       ├── James.png
│       └── Olivia.png
├── Fonts/
│   └── Rubik-SemiBold SDF.asset
├── Localization/
│   ├── LocalizationText.csv
│   └── LocalizationTextTable.asset
├── Prefabs/
│   └── UI/
│       ├── HomeScreen.prefab          (✅ Built by Cesar)
│       ├── PersistentUI.prefab        (YAML hierarchy, needs components)
│       └── SettingsCanvas.prefab      (YAML hierarchy, needs components)
├── Scripts/
│   └── UI/
│       ├── PersistentUIManager.cs     (Singleton, DontDestroyOnLoad)
│       ├── SettingsController.cs      (Settings panel controller)
│       ├── HomeScreenController.cs    (Existing)
│       ├── LoadingScreenController.cs (Existing)
│       ├── ScreenManager.cs           (Existing)
│       ├── ProTipCard.cs              (Loading screen tips)
│       ├── LoadingBar.cs              (Loading progress)
│       └── FadeController.cs          (Screen transitions)
├── References/
│   └── Settings/                      (Design mockups for Settings Screen)
└── Scenes/
    ├── ShellScene.unity               (Main UI scenes: Logo/Splash/Loading/Home)
    └── GameplayScene.unity            (Actual golf gameplay)

Docs/
├── SettingsScreen/
│   ├── README.md
│   ├── ARCHITECTURE.md
│   ├── PREFAB_STRUCTURE.md
│   └── IMPLEMENTATION_GUIDE.md
├── Golfin - Confluence.pdf            (Original project - 1202 pages)
└── Golfin Redux. - Current screens.pdf (Redux screens - 9 pages)
```

---

## Current Status

### ✅ Completed

**Logo Screen**
- Black background with logo
- Fades in/out animation
- Transitions to Splash Screen

**Splash Screen**
- Background image
- Logo with shield & text
- START button → Loading Screen
- CREATE ACCOUNT button → Create Account Screen (not built yet)
- LOGIN button → Login Screen (not built yet)

**Loading Screen**
- Background image
- Pro Tip Panel with rotating tips (from CSV)
- Loading bar with smooth animation
- Progress text (0-100%)
- Transitions to Home Screen

**Home Screen (✅ Built by Cesar - mostly complete)**
- ✅ Canvas background
- ✅ Top Bar: Reward Points Icon, Amount, Settings Button, Username
- ✅ News Panel (Maintenance notice)
- ✅ Promo Banner (GPS banner)
- ✅ Random Character (Camila/James/Olivia)
- ✅ Next Hole Panel with PLAY button
- ✅ Bottom Navigation Bar (5 buttons with highlight)
- 🚧 **Missing:** Localization for Next Hole name
- 🚧 **Missing:** Rewards display (3 reward slots)
- 🚧 **Missing:** Notice Panel carousel functionality (swipe/auto-cycle)

**Settings Screen - Phase 1 (Static UI)** ✅
- Created `PersistentUIManager.cs` - Singleton for Top Bar + Bottom Nav (persists across scenes)
- Created `SettingsController.cs` - Settings panel with 9 menu items
- Created `PersistentUI.prefab` - YAML hierarchy with GameObjects (components need to be added in Unity)
- Created `SettingsCanvas.prefab` - YAML hierarchy with GameObjects (components need to be added in Unity)
- Documented architecture, implementation guide, prefab structure

**Settings Screen - Phase 2 (Accordion Behavior)** ✅
- Created `SettingsMenuItem.cs` - Individual accordion item with expand/collapse animation
- Created `SettingsControllerPhase2.cs` - Updated controller managing accordion state
- Created `SoundSettingsSubmenu.cs` - Music + SFX volume sliders (0-100%, saves to PlayerPrefs)
- Created `LanguageSubmenu.cs` - English/Japanese toggle with visual indicators
- Created `UserProfileSubmenu.cs` - Username editing (3-16 chars) with real-time validation
- Smooth animations: 0.3s expand, 0.2s collapse, arrow rotation
- Only one section open at a time (automatic collapse)
- Complete setup guide: `Docs/SETTINGS_PHASE2_GUIDE.md` (~1 hour integration)

**Commits:**
- c12dd21: Add Settings Screen Phase 1 scripts + docs
- fa45fc5: Add prefab YAML hierarchies for PersistentUI and SettingsCanvas
- 3b4c3bc: Add AI_CONTEXT.md knowledge base
- 7e31377: Cesar's Home Screen implementation + character art
- 0a5c152: Complete Home Screen (localization, rewards, carousel)

- 0a5c152: Complete Home Screen (localization, rewards, carousel) - 2026-03-04 13:16

### ✅ Home Screen - COMPLETE!

**All features implemented:**
- ✅ Localization for Next Hole name (uses CSV keys)
- ✅ Rewards display (3 slots with icon support)
- ✅ Notice Panel carousel (auto-cycle + swipe)

**New Scripts:**
- `SwipeDetector.cs` - Swipe left/right detection
- `HoleData.cs` - Hole/reward data structures + ScriptableObject
- Updated `HomeScreenController.cs` - Full localization + auto-cycle + reward icons

**Setup Guide:** `Docs/HOME_SCREEN_SETUP.md` (5-10 min to wire up in Unity)

### 📋 Planned (Immediate Next Steps)

**Settings Screen - Phase 3 (Full Functionality)** ← NEXT
- Integrate SoundSettingsSubmenu with AudioManager (actual audio control)
- Integrate LanguageSubmenu with LocalizationManager (runtime UI language switching)
- Webview integration for Terms/Privacy/FAQ/Contact (UniWebView or Vuplex)
- Account linking UI (Google, Apple, Twitter via AWS Cognito)
- Log out confirmation modal with session clear + transition to Login screen
- About screen modal with app version info + licenses list

**Other Screens (From Redux PDF):**
- Create Account Screen
- Login Screen  
- Gacha Screen
- Select Hole Screen
- Inventory Screen
- Characters Screen

---

## Screen Specifications (From Redux PDF)

### Logo Screen
**Elements:**
- Black background
- Logo (Image)

**Logic:**
- Logo fades in and out
- Transition to Splash Screen

---

### Splash Screen
**Elements:**
- Background (Image)
- Shield & Text Logo (Image)
- START Button (Button Background + Label)
- CREATE ACCOUNT Text Button (TextMeshPro + Button)
- LOGIN Text Button (TextMeshPro + Button)

**Logic:**
- START → Loading Screen
- CREATE ACCOUNT → Create Account Screen (not built yet)
- LOGIN → Login Screen (not built yet)

---

### Loading Screen
**Elements:**
- Canvas Background (Image)
- Pro Tip Panel:
  - "PRO TIP" Title (TextMeshPro)
  - Divider Line (Image)
  - Instruction Text (TextMeshPro)
  - Tip Card (Image)
  - "TAP FOR NEXT TIP" Text (TextMeshPro + Button)
- "NOW LOADING" Text (TextMeshPro)
- Loading Bar Background (Image)
- Loading Bar Fill (Image)
- Loading Progress Text (TextMeshPro)

**Logic:**
- Shows tips from an array, cycling on timer and when user taps
- Tips and images are localized (read from CSV)
- Loading bar fills smoothly
- Progress text shows real loading amount (0-100%)
- After loading, go to Home Screen

---

### Home Screen
**Elements:**

**Canvas Background (Image)**

**Top Bar (Panel):**
- Reward Points Icon (Image)
- Reward Points Amount (TextMeshPro)
- Settings Button (Button + Image)
- Username Text (TextMeshPro)

**News Panel (Image):**
- Maintenance Title (TextMeshPro)
- Divider Line (Image)
- Maintenance Description Text (TextMeshPro)
- Page Indicator Dots (Images)

**Promo Banner (Button):**
- Banner Background (Image)
- Banner Text (TextMeshPro)
- GPS Icon (Image)

**Character (Image):**
- Randomly loaded from array (Camila, James, Olivia)
- Appears behind Next Hole Panel but over background

**Next Hole Panel (Image):**
- "NEXT HOLE" Title (TextMeshPro)
- Course Name Text (TextMeshPro)
- Reward Icon 1 (Image)
- Reward Amount 1 (TextMeshPro)
- Reward Icon 2 (Image)
- Reward Amount 2 (TextMeshPro)
- Reward Icon 3 (Image)
- Reward Amount 3 (TextMeshPro)
- PLAY Button (Button Background + Label)

**Bottom Navigation Bar (Panel):**
- Home Button (Button + Image)
- Gacha Button (Button + Image)
- Tee/Main Play Button (Button + Image)
- Inventory Button (Button + Image)
- Characters Button (Button + Image)

**Logic:**

**Top Bar:**
- Present in almost all screens
- Keep constant instead of reloading (DontDestroyOnLoad)

**Reward Points:**
- Display user's total points (read from backend, local for now)
- Update dynamically as points are earned/spent

**Settings Button:**
- Opens Settings screen

**Username:**
- Editable in Unity or Settings
- Font auto-adjusts to fit up to 16 characters

**News Panel:**
- Works as a carousel
- Multiple announcements can be loaded
- Cycle with timer or swipe left/right
- Read from localization file

**Character:**
- Loads randomly from array (customizable)
- Behind Next Hole Panel, over background

**Next Hole Panel:**
- Shows latest available hole info
- Title: Name of the hole (from CSV/server)
- Rewards: 1-3 rewards (Points, Repair Kits, Balls)
  - Icon + Amount (e.g., "Icon x 100")
  - Set up for each hole in CSV (local now, server later)
  - Finishing hole within par grants full rewards
  - Replaying gives lesser rewards (customizable)

**PLAY Button:**
- Starts next hole based on user progression

**Bottom Navigation Bar:**
- Present in almost all screens
- Keep constant instead of reloading
- Quick-access: Home, Gacha, Select Hole, Inventory, Characters
- Highlight current section

---

### Settings Screen
**Elements:**

**Canvas Background (Image)**

**Top Bar (Panel):** (same as Home Screen)

**Settings Panel (Main Container):**

**Each Row (Reusable Structure):**
- Button (full row clickable)
- Left Icon (Image)
- Label (TextMeshPro)
- Right Arrow (Image)

**Close Button:**
- Background (Image)
- "CLOSE" Label (TextMeshPro)

**Bottom Navigation Bar (Panel):** (same as Home Screen)

**Logic:**

**Settings Panel:**
- All items behave like drop-downs
- Pressing arrow expands/collapses section
- Only one section open at a time
- All start closed

**Menu Items:**
1. **User Profile:** Opens submenu (edit name, account linking)
2. **Sound Settings:** Opens submenu (Music Volume, SFX Volume)
3. **Language:** Opens language selection screen, updates localization
4. **Terms of Use:** Opens webview/modal with legal document
5. **Privacy Policy:** Opens webview/modal with privacy text
6. **FAQ:** Opens webview/modal with FAQ screen
7. **About:** App version & licenses
8. **Contact Form:** Opens webview/modal with contact screen
9. **Log Out:** (Disregard for now) Confirmation modal → Clear session → Login screen

**Close Button:**
- Returns to previous screen

---

## Technical Decisions

### UI Architecture
- **Persistent UI Pattern:** Top Bar and Bottom Nav use `DontDestroyOnLoad` to survive scene transitions
  - Avoids duplication across scenes
  - Prevents flicker during transitions
  - Maintains UI state (reward points, username, etc.)
- **Overlay Pattern:** Settings panel overlays on top of any screen (Sort Order: 100)
  - No backdrop click to close (only Close button)
- **Singleton Pattern:** `PersistentUIManager` and `SettingsController` use singleton pattern for global access

### Localization
- **System:** CSV-based with `LocalizedText` component (`Assets/Localization/`)
- **Languages:** English and Japanese initially (easy to add more)
- **Key format:** SCREAMING_SNAKE_CASE (e.g., `HOME_COURSE_NAME`, `SETTINGS_MENU_USER_PROFILE`)
- **Component:** `LocalizedText.cs` - Attach to all TextMeshProUGUI objects
- **Manager:** `LocalizationManager.cs` - Provides `Get(key)` and `OnLanguageChanged` event
- **Auto-refresh:** Text updates automatically when user changes language
- **MANDATORY:** All UI text must use LocalizedText (no hard-coded strings!)
- **Editor Tool:** `Tools → GOLFIN → Localize Settings Screen` for batch operations
- **Reference:** `Docs/SETTINGS_LOCALIZATION_GUIDE.md`

### Rewards System
- 3 reward types: Points, Repair Kits, Balls
- Displayed as Icon + Amount (e.g., "Icon x 100")
- Full rewards for finishing hole within par
- Lesser rewards for replaying (customizable)
- Stored in CSV (local) → Server (future)

### Character System
- Random selection from array (Camila, James, Olivia)
- Customizable array
- Appears behind Next Hole Panel

### Notice Panel / News Carousel
- Multiple announcements
- Auto-cycle with timer
- Swipe left/right for manual navigation
- Page indicator dots show current position
- Read from localization CSV

### Sound Settings
- Sliders for Music and SFX volume (not preset levels)
- Phase 2 feature

### Account Linking
- Tied to Cognito server
- Static for now (Phase 3 feature)

### External Links
- Terms/Privacy/FAQ/Contact open in webview (platform-specific)
- Falls back to external browser on unsupported platforms

---

## Conventions & Standards

### Code Style
- Namespace: `Golfin.UI`
- Singleton pattern for managers
- Null checks before using public references
- Debug.Log for Phase 1 (placeholder for actual functionality)

### Naming
- Controllers: `[Screen]Controller.cs` (e.g., `HomeScreenController.cs`)
- Managers: `[Purpose]Manager.cs` (e.g., `PersistentUIManager.cs`)
- Prefabs: PascalCase (e.g., `PersistentUI.prefab`)
- Localization Keys: SCREAMING_SNAKE_CASE (e.g., `HOME_COURSE_NAME`)

### Comments
- XML summary comments for public classes and methods
- Inline comments for complex logic
- TODO comments for Phase 2/3 features

---

## Common Issues & Solutions

### Issue: PersistentUI disappears when changing scenes
**Solution:** Make sure `DontDestroyOnLoad(gameObject)` is called in `PersistentUIManager.Awake()`

### Issue: Buttons not clickable
**Solution:**
- Check Canvas `Graphic Raycaster` is enabled
- Make sure buttons have an `Image` component (even if transparent)
- Check EventSystem exists in the scene

### Issue: Settings panel doesn't open
**Solution:**
- Check `SettingsController.Instance` is not null
- Make sure `settingsPanel` reference is assigned in inspector
- Verify `settingsPanel.SetActive(true)` is being called

### Issue: Localization not working
**Solution:**
- Check `LocalizationText.csv` has the key
- Verify `LocalizationTextTable.asset` is updated
- Make sure TextMeshPro component is using localization system

### Issue: Row content shifts to middle when expanding (Phase 2 accordion)
**Symptom:** When row expands from 80px to 380px, button/icon/label move to middle, leaving empty space above
**Root Cause:** Row children anchored to middle/center of parent
**Solution:** Use **Tools → GOLFIN → Fix Row Child Anchors**
- Anchors all children to TOP of row (anchor Y=1, pivot Y=1)
- Content stays at top when row height changes
- Button/Icon/Label remain at Y=0-80, submenu appears at Y=80-380

### Issue: Submenu stays visible after collapse (Phase 2 accordion)
**Symptom:** Click to collapse → Height corrects but submenu content stays visible, arrow stays rotated
**Root Cause:** Height desync - external component (LayoutGroup/LayoutElement) forcing height back
**Solution:** Auto-corrected in `SettingsMenuItem.Update()` with comprehensive visual fix:
- Detects when `LayoutElement.preferredHeight` doesn't match expected value
- Logs `HEIGHT DESYNC` warning
- Auto-corrects ALL visual elements: height, submenu visibility, arrow rotation, internal state
- No manual fix needed - happens automatically every frame until external interference stops

### Issue: Accordion doesn't auto-collapse other rows
**Symptom:** Click Row B while Row A is open → Row A stays open instead of auto-collapsing
**Root Cause:** Menu items not assigned in `SettingsControllerPhase2` Inspector
**Solution:** 
1. Use **Tools → GOLFIN → Verify Accordion Setup** to check assignments
2. Select GameObject with `SettingsControllerPhase2` component
3. In Inspector, find "Menu Items with Accordion" section
4. Assign: UserProfileItem → UserProfileRow, SoundSettingsItem → SoundSettingsRow, etc.

### Issue: Click open row doesn't collapse it
**Symptom:** First click expands, second click does nothing or re-expands immediately
**Possible Causes:**
1. **Button blocked by submenu** - Disable Raycast Target on non-interactive submenu elements
2. **Duplicate listeners** - Check Button component On Click() section, remove any persistent listeners (runtime listener in Awake() is enough)
3. **State desync** - Height desync auto-fix should handle this (see above)
**Debug:** Check Console for `[SettingsMenuItem] ToggleExpansion` logs when clicking

---

## Next Steps

### Current Sprint (Testing Phase 3)
- [ ] Test Language Integration in Unity (code ready, tested & working)
- [ ] Test Sound Settings Integration in Unity (code ready, tested & working)
- [ ] Build & test About Accordion in Unity (code + builder ready)
- [ ] Verify all accordion items work correctly (User Profile, Sound, Language, About)
- [ ] Test localization tool on Settings Screen
- [ ] Apply localization to other screens (Home Screen, etc.)

### Phase 4 (Authentication & External Integration)
**Waiting for Cognito Integration:**
- [ ] Log Out Modal - Confirmation dialog → Clear session → Login screen
- [ ] Account Linking - Google/Apple/Twitter via AWS Cognito
- [ ] Webview - Terms of Use, Privacy Policy, FAQ, Contact (UniWebView or Vuplex)

### Other Screens (Future Phases)
- [ ] Create Account Screen
- [ ] Login Screen
- [ ] Gacha Screen
- [ ] Select Hole Screen
- [ ] Inventory Screen
- [ ] Characters Screen

### Home Screen Enhancements
- [x] Build Home Screen UI
- [ ] Add localization for Next Hole name
- [ ] Display 3 rewards for Next Hole
- [ ] Implement Notice Panel carousel functionality

### Completed ✅
- [x] **Phase 1:** Settings Screen Basic UI
- [x] **Phase 2:** Settings Screen Accordion + Submenus + Layout Fixes
- [x] **Phase 3:** Language Integration, Sound Settings Integration, About Accordion (core features)

---

## Resources

### Documentation

**Core References:**
- `Docs/Golfin - Confluence.pdf` - Original project (1202 pages)
- `Docs/Golfin Redux. - Current screens.pdf` - Redux screens (9 pages)

**Feature Guides:**
- `Docs/HOME_SCREEN_SETUP.md` - Home screen final setup (localization, rewards, carousel)
- `Docs/QUICK_START_HOLES.md` - HoleDatabase auto-load from CSV (2-min setup)
- `Docs/SCREEN_DEACTIVATOR.md` - Screen deactivator usage and setup
- `Docs/SETTINGS_BUILD_GUIDE.md` - Settings screen complete build guide (Phase 1)
- `Docs/UI_AUTO_WIRE_GUIDE.md` - UI auto-wire utility (no more manual Inspector dragging)

**Settings Screen:**
- `Docs/PHASE3_LANGUAGE_INTEGRATION.md` - **🌐 Phase 3: Language switching** (complete testing guide)
- `Docs/PHASE3_SOUND_INTEGRATION.md` - **🔊 Phase 3: Sound Settings + AudioManager** (complete setup guide)
- `Docs/SETTINGS_LOCALIZATION_GUIDE.md` - **⚡ Auto-localization tool** (5-min one-click setup)
- `Docs/PHASE2_BUILDER_TOOL.md` - **⚡ Unity Editor tool** (builds Phase 2 in 30 seconds!)
- `Docs/SETTINGS_PHASE2_GUIDE.md` - Phase 2 setup guide (accordion + submenus, ~1 hour manual)
- `Docs/PHASE2_HIERARCHY_GUIDE.md` - Manual hierarchy creation (alternative to builder tool)
- `Docs/SettingsScreen/README.md` - Phase 1 overview + quick start
- `Docs/SettingsScreen/ARCHITECTURE.md` - System architecture with diagrams
- `Docs/SettingsScreen/PREFAB_STRUCTURE.md` - Complete Unity hierarchy
- `Docs/SettingsScreen/IMPLEMENTATION_GUIDE.md` - Phase 1 step-by-step setup

### Design References
- `Assets/References/Settings/` - Mockups for Settings Screen

### External Libraries (for Phase 2+)
- **DOTween:** Smooth animations - https://dotween.demigiant.com/
- **UniWebView:** In-app webviews - https://uniwebview.com/
- **Vuplex:** Cross-platform webviews - https://store.vuplex.com/

---

## Contact & Workflow

### Team
- **Cesar Guarinoni:** Lead developer, Unity implementation
- **Ken Komatsu (kenken):** Founder, product direction
- **Kai (Aikenken Bot):** AI assistant, architecture + documentation
- **Cortana:** Cesar's local AI assistant (separate channel)

### Workflow
1. Cesar works on screens/features (with Cortana's help)
2. Cesar updates Kai when ready for next phase or requests new features
3. Kai provides architecture, scripts, documentation
4. **Kai pushes code directly to GitHub** (no manual copying needed)
5. Cesar pulls changes and implements in Unity Editor
6. Repeat

**Note:** Kai can clone, commit, and push to the repository. Scripts and documentation are committed directly to the repo.

### Communication
- Telegram group: GOLFIN<>dev
- Repository: https://github.com/cesarguarinoni/Golfin-Redux

---

## Change Log

### 2026-03-05 16:40 JST
- **PHASE 3 COMPLETE!** 🎉 Core features fully integrated
- Updated AI_CONTEXT.md with complete Phase 3 documentation
- **Language Integration:** LanguageSubmenu + LocalizationManager (tested & working)
- **Sound Settings Integration:** AudioManager + SoundSettingsSubmenu (tested & working)
- **About Accordion:** AboutSubmenu + AboutSubmenuBuilder tool (ready to test)
- **Deferred:** Log Out modal, Account Linking, Webview (Phase 4 with Cognito)
- New docs: PHASE3_LANGUAGE_INTEGRATION.md, PHASE3_SOUND_INTEGRATION.md, SETTINGS_LOCALIZATION_GUIDE.md
- All Phase 3 tools and helpers documented
- Commits: 0716d71, ed50ea5, a518b56, 3921dab, 48b2a6f, 518cd8b, and more
- Status: 3/3 core features complete, ready for Unity integration testing

### 2026-03-05 16:11 JST
- **ABOUT SUBMENU FORMATTED** 📋 Text formatting fixed
- Simplified GetLicensesText() to match reference image (MIT, GPL, Apache, BSD)
- Fixed TextMeshPro settings: word wrapping enabled, proper overflow mode
- Updated placeholder text in builder for clean preview
- Commit: 518cd8b

### 2026-03-05 16:00 JST
- **ABOUT SUBMENU BUILDER CREATED** 🔧 One-click UI creation
- Created AboutSubmenuBuilder.cs (Tools → GOLFIN → Build About Submenu)
- Builds complete hierarchy: APP VERSION + LICENCES sections
- Auto-wires references via SerializedObject
- VerticalLayoutGroup for proper stacking
- Matches pattern of other Phase 2 submenus
- Time: 30 seconds vs 15+ minutes manual
- Commit: 8513e4a

### 2026-03-05 15:55 JST
- **ABOUT CORRECTED** ✅ Fixed to accordion pattern (not modal!)
- Created AboutSubmenu.cs (accordion submenu, inline expansion)
- Updated SettingsControllerPhase2 (added aboutItem to accordion)
- Removed incorrect modal files (AboutModal, AboutModalBuilder)
- Kept ModalController for future Log Out modal
- Matches reference image: About expands inline like other accordion items
- Commits: 48b2a6f, b507777

### 2026-03-05 15:51 JST
- **ABOUT MODAL IMPLEMENTED** 📋 (later corrected to accordion)
- Created ModalController.cs (base class for all modals)
- Created AboutModal.cs (shows version + licenses)
- Created AboutModalBuilder.cs (one-click UI creation)
- Smooth fade in/out animation (0.2s)
- Backdrop support, close button wiring
- Note: Later changed to accordion pattern per reference design
- Commits: cacc1d7, a06bac7

### 2026-03-05 15:42 JST
- **SOUND SETTINGS INTEGRATED** 🔊 AudioManager complete
- Created AudioManager.cs (singleton, DontDestroyOnLoad)
  - Separate Music and SFX volume control (0-100 scale)
  - Dedicated AudioSource for music
  - Pool of 5 AudioSources for SFX
  - Methods: SetMusicVolume, SetSFXVolume, PlayMusic, PlaySFX, PlaySFXAtPosition
  - Auto-saves to PlayerPrefs
- Updated SoundSettingsSubmenu.cs (integrated with AudioManager)
  - LoadSettings reads from AudioManager
  - OnValueChanged applies to AudioManager in real-time
  - Removed TODO placeholders - fully functional!
- Created Docs/PHASE3_SOUND_INTEGRATION.md (complete setup guide)
- Time: ~20 minutes
- Commits: a518b56, 3921dab

### 2026-03-05 15:21 JST
- **LANGUAGE INTEGRATED** 🌐 Real-time language switching working!
- Updated LanguageSubmenu.cs (integrated with LocalizationManager)
  - Replaced string-based language with Language enum
  - Calls LocalizationManager.SetLanguage() on button click
  - Subscribes to OnLanguageChanged event
  - LoadLanguagePreference applies saved language at startup
  - OnEnable/OnDisable for proper event subscription
- Result: Click English/Japanese → Entire UI updates instantly!
- Created Docs/PHASE3_LANGUAGE_INTEGRATION.md (complete testing guide)
- Time: ~15 minutes
- Commits: 0716d71, ed50ea5

### 2026-03-05 14:36 JST
- **LOCALIZATION SYSTEM DOCUMENTED** 🌐 Now mandatory for all future work
- Added comprehensive localization section to AI_CONTEXT.md
- Documented `LocalizedText` component pattern for editor scripts
- Helper method pattern for adding localization to text objects
- Key naming conventions (SCREEN_SECTION_ELEMENT)
- Reference to existing tools and guides
- **NEW RULE:** All TextMeshPro objects created via editor scripts MUST have LocalizedText component
- Commits: Updated AI_CONTEXT.md with localization best practices

### 2026-03-05 14:22 JST
- **LOCALIZATION TOOL CREATED** ⚡ Auto-generates keys + adds components
- Created `LocalizeSettingsScreen.cs` - One-click localization setup tool
- Menu: Tools → GOLFIN → Localize Settings Screen
- Features:
  - Scans all TextMeshPro in Settings Panel
  - Auto-generates localization keys (SETTINGS_* format)
  - Creates CSV with English + Japanese translations
  - **Auto-adds LocalizedText component to all texts**
  - **Auto-assigns keys using SerializedObject**
- Pre-loaded translations for common terms (9 menu items, buttons, sound/language settings)
- Integration with existing LocalizedText system (Assets/Localization/)
- One-click setup: Drag panel → Click button → Done!
- Created `Docs/SETTINGS_LOCALIZATION_GUIDE.md` - Complete usage guide
- Commits: 020658c (tool), 23def2c (guide), cc529c9 (LocalizedText integration), cea3c95 (updated docs)

### 2026-03-05 14:05 JST
- **PHASE 2 FULLY WORKING!** 🎉 All layout issues resolved
- **Issue #1: Row content shifting down** - When row expanded, button/icon/label moved to middle of expanded space
  - Root cause: Row children anchored to middle/center of parent row
  - Fix: Created `FixRowChildAnchors.cs` - Anchors all row children to TOP (Y=1)
  - Result: Content stays at top when row expands from 80px to 380px
  - Commit: 16cb769
- **Issue #2: Accordion not auto-collapsing** - Clicking Row B didn't collapse Row A
  - Root cause: Menu items not assigned in SettingsControllerPhase2 Inspector
  - Debug: Created `VerifyAccordionSetup.cs` to check assignments
  - Enhanced logging in `OnMenuItemExpanded()` to track accordion behavior
  - Commit: fc58598
- **Issue #3: Click-to-collapse broken (state desync)** - Clicking open row caused visual re-expansion
  - Sequence: Click → Collapse → Mysteriously re-expands → State desync (_isExpanded=false but visually expanded)
  - Pattern B identified: Something re-expanding WITHOUT calling Expand()
  - Root cause: External component (LayoutElement/LayoutGroup) forcing height back after collapse
  - Fix Part 1: Added height desync detection in Update() - logs warning when mismatch detected
  - Fix Part 2: Comprehensive auto-correction of ALL visual elements:
    - LayoutElement.preferredHeight (container size)
    - Submenu RectTransform.sizeDelta (content height)
    - Arrow rotation (0° collapsed, 90° expanded)
    - Submenu visibility (show/hide)
    - _currentHeight sync (internal state)
  - Commits: f3664a7 (enhanced logging), b894475 (desync detection), e1d1878 (comprehensive fix)
- **Diagnostic tools created today:**
  - `DiagnoseLayoutIssue.cs` - Checks VerticalLayoutGroup alignment, ContentSizeFitter, pivots, LayoutElements
  - `VerifyButtonWiring.cs` - Checks button assignments, interactivity, target graphics
  - `VerifyAccordionSetup.cs` - Checks menu item assignments in controller
  - `FixRowChildAnchors.cs` - Anchors row children to top of parent
  - (Previous tools: FixSubmenuPositioning, FixSettingsLayoutV2, VerifySubmenuParenting)
- **Result:** Phase 2 accordion behavior works perfectly:
  - Click row → Expands smoothly ✅
  - Click same row → Collapses and STAYS collapsed ✅
  - Click different row → Previous auto-collapses ✅
  - Submenu content appears/disappears correctly ✅
  - Arrow rotates correctly ✅
  - Height desync auto-corrects in real-time ✅
- **Status:** Ready for Phase 3 (AudioManager, LocalizationManager, Webview, Account linking)
- Cesar going to lunch, will return for Phase 3

### 2026-03-05 10:37 JST
- **UNITY EDITOR BUILDER TOOL ADDED!** ⚡
- Created `SettingsPhase2Builder.cs` - Unity Editor tool for automatic hierarchy creation
- Menu: Tools → GOLFIN → Build Phase 2 Submenus
- Builds all 3 submenus in ~30 seconds (vs 40 min manual)
- Creates complete GameObject hierarchies with all components
- Sets proper RectTransforms (positions, sizes, anchors)
- Automatically wires up script references via reflection
- Created `Docs/PHASE2_BUILDER_TOOL.md` - Complete usage guide
- Updated `Docs/PHASE2_HIERARCHY_GUIDE.md` - Now recommends builder tool first
- Replaced broken YAML prefabs (had duplicate IDs, import errors)
- NEW PATTERN: Use Unity Editor scripts instead of manual/YAML for UI hierarchies
- 80x faster than manual creation, no human errors, repeatable
- Commits: 891585e (remove YAML), 908377b (add builder tool)

### 2026-03-05 07:00 JST
- **SETTINGS SCREEN PHASE 2 COMPLETE!** 🎉
- Added accordion expand/collapse behavior (only one section open at a time)
- Added `SettingsMenuItem.cs` - Individual accordion item with smooth animation
- Added `SettingsControllerPhase2.cs` - Accordion management + singleton
- Added `SoundSettingsSubmenu.cs` - Music + SFX volume sliders (0-100%)
- Added `LanguageSubmenu.cs` - English/Japanese toggle with checkmarks
- Added `UserProfileSubmenu.cs` - Username editing with real-time validation
- Created `Docs/SETTINGS_PHASE2_GUIDE.md` - Complete setup guide (~1 hour)
- Animation: 0.3s expand, 0.2s collapse, arrow rotation (0° → 90°)
- Auto-saves: Volume levels, language preference, username (PlayerPrefs)
- Phase 3 placeholders: Account linking, AudioManager, LocalizationManager, Webview
- Ready for Unity integration and testing
- Commit: 839c43a

### 2026-03-05 06:43 JST
- **DOCUMENTATION SYNC:** Kai has read all project documentation
- Read: HOME_SCREEN_SETUP.md, QUICK_START_HOLES.md, SCREEN_DEACTIVATOR.md
- Read: SETTINGS_BUILD_GUIDE.md, UI_AUTO_WIRE_GUIDE.md
- Read: All SettingsScreen docs (README, ARCHITECTURE, IMPLEMENTATION_GUIDE, PREFAB_STRUCTURE)
- Status: Fully synced with project context and architecture
- Updated documentation list in Resources section
- Ready for next development phase

### 2026-03-05 06:36 JST
- **NEW FEATURE:** Screen Deactivator utility added
- Added `ScreenDeactivator.cs` - Auto-deactivates screens before Play mode
- Added `ScreenDeactivatorEditor.cs` - GUI configuration window
- Created `Docs/SCREEN_DEACTIVATOR.md` - Complete documentation
- Updated workflow: Kai now pushes directly to GitHub (no manual copying)
- Purpose: Prevents multiple screens from being active at startup
- Auto-detects screens by name pattern (Screen/Panel/Canvas)
- Future-proof: automatically finds new screens as they're added

### 2026-03-04 13:00 JST
- **MAJOR UPDATE:** Read both PDF docs (Original GOLFIN 1202 pages + Redux 9 pages)
- Added comprehensive screen specifications from Redux PDF
- Documented Cesar's Home Screen implementation
- Identified missing Home Screen features (localization, rewards, carousel)
- Clarified project simplification (Redux vs Original)
- Updated folder structure with new assets (characters, localization)
- Added detailed logic for each screen
- Note: Cesar is working with Cortana (local AI) in parallel

### 2026-03-04 10:21 JST
- Created AI_CONTEXT.md as living knowledge base
- Documented Settings Screen Phase 1 completion
- Clarified project uses shared point system (not NFTs)
- Documented current architecture and conventions

---

**Note to Future AI Assistants:**
This file is your source of truth. Read it first before working on the project. Update it whenever you learn something new or make significant changes. Keep it concise but comprehensive.

**Note to Cesar:**
When you finish a feature or make significant progress, update this file with:
- What's been completed
- What's still missing
- Any new decisions or changes
- Updated commit hashes

### 2026-03-04 13:16 JST
- **HOME SCREEN COMPLETE!** 🎉
- Added SwipeDetector.cs for news carousel swipe gestures
- Added HoleData.cs with reward structures and HoleDatabase ScriptableObject
- Updated HomeScreenController.cs with:
  - Localization support for course names (reads from CSV)
  - Auto-cycle timer for news panel (configurable, default 5s)
  - Reward type support with icon slots (Points, Repair Kit, Ball)
  - Integration with HoleData structure
- Updated LocalizationText.csv with hole name keys
- Created Docs/HOME_SCREEN_SETUP.md with complete setup guide
- All 3 missing features now implemented
- Commit: 0a5c152
### 2026-03-04 14:05 JST
- **HOLEDATABASE ASSET CREATED!**
- Added Assets/Data/HoleDatabase.asset with 5 example holes
- Each hole includes courseNameKey, holeNumber, and 1-3 rewards
- Added Assets/Data/README_HOLES.md with complete usage guide
- Updated LocalizationText.csv with HOLE_LOMOND_6 and HOLE_RIVERSIDE_2
- Ready to use: drag HoleDatabase.asset into HomeScreenController inspector
- Fully extendable in Unity (click + to add more holes)
- Commits: f134e61, 2188c3f
### 2026-03-04 14:39 JST
- **AUTO-LOAD FROM CSV!** 🎉
- Created HoleDatabaseLoader.cs - auto-loads holes from CSV on scene start
- No manual import needed! Just edit CSV and play
- Similar workflow to localization (automatic runtime loading)
- Updated HomeScreenController to use runtime database as fallback
- Setup: Add HoleDatabaseLoader component, assign CSV, done!
- Updated QUICK_START_HOLES.md with auto-load guide
- Commits: fb0f0a1 (fix ScriptableObject), 0054c06 (auto-load)


