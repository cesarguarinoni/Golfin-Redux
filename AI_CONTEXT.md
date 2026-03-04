
---

## 🎉 LATEST: Screen Deactivator Added!

**Screen Deactivator** - Automatically deactivates unnecessary screens before runtime!
- ✅ Auto-runs before Play mode in Unity Editor
- ✅ Future-proof detection (auto-finds new screens by naming pattern)
- ✅ GUI configuration via **GOLFIN → Screen Deactivator Settings**
- ✅ Prevents multiple screens from being active at startup

**New Scripts Added:**
- `ScreenDeactivator.cs` - Main component with auto-detection
- `ScreenDeactivatorEditor.cs` - Editor window for easy configuration

**Setup:** See `Docs/SCREEN_DEACTIVATOR.md` for full guide.

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

**Settings Screen - Phase 1 (Static UI)**
- Created `PersistentUIManager.cs` - Singleton for Top Bar + Bottom Nav (persists across scenes)
- Created `SettingsController.cs` - Settings panel with 9 menu items
- Created `PersistentUI.prefab` - YAML hierarchy with GameObjects (components need to be added in Unity)
- Created `SettingsCanvas.prefab` - YAML hierarchy with GameObjects (components need to be added in Unity)
- Documented architecture, implementation guide, prefab structure

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


### 📋 Planned (Immediate Next Steps)

**Settings Screen - Phase 2 (Accordion Behavior)**
- Expand/collapse animation for User Profile, Sound Settings, Language
- Music Volume + SFX Volume sliders
- Language selection screen (English/Japanese)
- Only one section open at a time
- DOTween or Unity Animator for smooth transitions

**Other Screens (From Redux PDF):**
- Create Account Screen
- Login Screen
- Gacha Screen
- Select Hole Screen
- Inventory Screen
- Characters Screen

**Settings Screen - Phase 3 (Full Functionality)**
- Webview integration for Terms/Privacy/FAQ/Contact
- User profile editing + account linking (Cognito)
- Log out confirmation modal + session clear
- About screen (app version, licenses)

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
- CSV-based system (`LocalizationText.csv`)
- English and Japanese initially
- Key-based lookup (e.g., `HOME_COURSE_NAME`)
- All UI text reads from CSV

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

---

## Next Steps

### Immediate (Cesar's Work)
- [x] Build Home Screen UI
- [ ] Add localization for Next Hole name
- [ ] Display 3 rewards for Next Hole
- [ ] Implement Notice Panel carousel functionality

### Phase 2 (Settings Screen Accordion)
- [ ] Create `SettingsMenuItem.cs` for expand/collapse logic
- [ ] Create `SettingsSubmenu.cs` for expanded content
- [ ] Add DOTween or Unity Animator for smooth animations
- [ ] Implement Music/SFX volume sliders
- [ ] Implement language selection screen

### Phase 3 (Other Screens)
- [ ] Create Account Screen
- [ ] Login Screen
- [ ] Gacha Screen
- [ ] Select Hole Screen
- [ ] Inventory Screen
- [ ] Characters Screen

### Phase 4 (Settings Screen Full Functionality)
- [ ] Integrate webview plugin (UniWebView or Vuplex)
- [ ] Implement user profile editing
- [ ] Implement Cognito account linking
- [ ] Create log out confirmation modal
- [ ] Create About screen with version info

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

**Settings Screen (Phase 1):**
- `Docs/SettingsScreen/README.md` - Overview + quick start
- `Docs/SettingsScreen/ARCHITECTURE.md` - System architecture with diagrams
- `Docs/SettingsScreen/PREFAB_STRUCTURE.md` - Complete Unity hierarchy
- `Docs/SettingsScreen/IMPLEMENTATION_GUIDE.md` - Step-by-step setup guide

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


