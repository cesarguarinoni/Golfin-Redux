# AI Context - GOLFIN Redux Project

**Last Updated:** 2026-03-04 10:21 JST  
**Updated By:** Kai (Aikenken Bot)

---

## Project Overview

**GOLFIN Redux** is a Unity-based mobile golf game with a shared point system integrated with the Golfin app ecosystem.

### Key Facts
- **Platform:** Unity (iOS/Android)
- **Language:** C# + Unity UI (no NFTs)
- **Monetization:** Shared point system with Golfin app
- **Stage:** Core UI development phase
- **Team:** Cesar (lead dev), Ken (founder), Kai (AI assistant)

---

## Architecture

### Tech Stack
- **Engine:** Unity 2021.3+
- **UI:** Unity UI (Canvas-based)
- **Text:** TextMeshPro
- **Architecture Pattern:** Singleton managers + Scene-based controllers

### Folder Structure
```
Assets/
├── Prefabs/
│   └── UI/
│       ├── PersistentUI.prefab         (Top Bar + Bottom Nav)
│       └── SettingsCanvas.prefab       (Settings Screen overlay)
├── Scripts/
│   └── UI/
│       ├── PersistentUIManager.cs      (Singleton, DontDestroyOnLoad)
│       ├── SettingsController.cs       (Settings panel controller)
│       ├── HomeScreenController.cs     (Existing)
│       ├── LoadingScreenController.cs  (Existing)
│       ├── ScreenManager.cs            (Existing)
│       └── [other UI controllers]
├── References/
│   └── Settings/                       (Design mockups for Settings Screen)
└── Scenes/
    └── [game scenes]

Docs/
└── SettingsScreen/
    ├── README.md
    ├── ARCHITECTURE.md
    ├── PREFAB_STRUCTURE.md
    └── IMPLEMENTATION_GUIDE.md
```

---

## Current Status

### ✅ Completed (Phase 1)

**Settings Screen - Phase 1 (Static UI)**
- Created `PersistentUIManager.cs` - Singleton for Top Bar + Bottom Nav (persists across scenes)
- Created `SettingsController.cs` - Settings panel with 9 menu items
- Created `PersistentUI.prefab` - YAML hierarchy with GameObjects (components need to be added in Unity)
- Created `SettingsCanvas.prefab` - YAML hierarchy with GameObjects (components need to be added in Unity)
- Documented architecture, implementation guide, prefab structure

**What Works:**
- Top Bar: Reward Points display, Settings button, Username display
- Bottom Nav: 5 buttons (Home, Gacha, Main Play, Inventory, Characters) with highlight system
- Settings Panel: 9 menu items (User Profile, Sound Settings, Language, Terms of Use, Privacy Policy, FAQ, About, Contact, Log Out)
- All buttons log to Console on click

**What's Still Needed:**
- Add UI components (Button, Image, TextMeshProUGUI) to prefab GameObjects in Unity Editor
- Wire up inspector references in PersistentUIManager and SettingsController
- Add prefabs to scene and test

**Commits:**
- c12dd21: Add Settings Screen Phase 1 scripts + docs
- fa45fc5: Add prefab YAML hierarchies for PersistentUI and SettingsCanvas

### 🚧 In Progress

**UI Screens (Cesar working on):**
- [Cesar is building out additional screens - details TBD]

### 📋 Planned (Future Phases)

**Settings Screen - Phase 2 (Accordion Behavior)**
- Expand/collapse animation for User Profile, Sound Settings, Language
- Music Volume + SFX Volume sliders
- Language selection screen (English/Japanese)
- Only one section open at a time
- DOTween or Unity Animator for smooth transitions

**Settings Screen - Phase 3 (Full Functionality)**
- Webview integration for Terms/Privacy/FAQ/Contact
- User profile editing + account linking (Cognito)
- Log out confirmation modal + session clear
- About screen (app version, licenses)

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

### Sound Settings
- Sliders for Music and SFX volume (not preset levels)
- Phase 2 feature

### Language Support
- English and Japanese initially
- Expandable to more languages later

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

---

## Next Steps

### Immediate (Cesar's Work)
- [ ] Build out additional screens
- [ ] Update project with latest changes
- [ ] Let Kai know when ready for next phase

### Phase 2 (Settings Screen Accordion)
- [ ] Create `SettingsMenuItem.cs` for expand/collapse logic
- [ ] Create `SettingsSubmenu.cs` for expanded content
- [ ] Add DOTween or Unity Animator for smooth animations
- [ ] Implement Music/SFX volume sliders
- [ ] Implement language selection screen

### Phase 3 (Settings Screen Full Functionality)
- [ ] Integrate webview plugin (UniWebView or Vuplex)
- [ ] Implement user profile editing
- [ ] Implement Cognito account linking
- [ ] Create log out confirmation modal
- [ ] Create About screen with version info

---

## Resources

### Documentation
- `Docs/SettingsScreen/README.md` - Overview + quick start
- `Docs/SettingsScreen/ARCHITECTURE.md` - System architecture with diagrams
- `Docs/SettingsScreen/PREFAB_STRUCTURE.md` - Complete Unity hierarchy
- `Docs/SettingsScreen/IMPLEMENTATION_GUIDE.md` - Step-by-step setup guide

### Design References
- `Assets/References/Settings/` - Mockups for Settings Screen

### External Libraries (for Phase 2/3)
- **DOTween:** Smooth animations - https://dotween.demigiant.com/
- **UniWebView:** In-app webviews - https://uniwebview.com/
- **Vuplex:** Cross-platform webviews - https://store.vuplex.com/

---

## Contact & Workflow

### Team
- **Cesar Guarinoni:** Lead developer, Unity implementation
- **Ken Komatsu (kenken):** Founder, product direction
- **Kai (Aikenken Bot):** AI assistant, architecture + documentation

### Workflow
1. Cesar works on screens/features
2. Cesar updates Kai when ready for next phase
3. Kai provides architecture, scripts, documentation
4. Cesar implements in Unity Editor
5. Repeat

### Communication
- Telegram group: GOLFIN<>dev
- Repository: https://github.com/cesarguarinoni/Golfin-Redux

---

## Change Log

### 2026-03-04 10:21 JST
- Created AI_CONTEXT.md as living knowledge base
- Documented Settings Screen Phase 1 completion
- Clarified project uses shared point system (not NFTs)
- Documented current architecture and conventions

---

**Note to Future AI Assistants:**
This file is your source of truth. Read it first before working on the project. Update it whenever you learn something new or make significant changes. Keep it concise but comprehensive.
