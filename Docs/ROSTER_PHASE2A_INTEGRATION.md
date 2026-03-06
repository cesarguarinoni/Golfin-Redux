# Roster Screen - ScreenManager Integration

**Date:** 2026-03-06  
**Status:** ✅ COMPLETE  
**Commit:** `🔗 13dfbc5`  
**Focus:** Integrating Roster into existing UI architecture

---

## 🎯 What Was Integrated

Roster screen is now a **first-class main screen** managed by ScreenManager, same as Home/Logo/Loading.

### Before Integration
```
ScreenManager
├── Logo
├── Splash
├── Loading
└── Home

Roster Screen = Standalone (not managed)
navCharactersButton = TODO
```

### After Integration
```
ScreenManager
├── Logo
├── Splash
├── Loading
├── Home
└── Roster ✅ (NEW!)

HomeScreenController
└── navCharactersButton → ShowScreen(ScreenId.Roster) ✅
```

---

## 📝 Files Updated

### 1. **ScreenManager.cs**
Added Roster to screen management:
```csharp
public enum ScreenId
{
    Logo,
    Splash,
    Loading,
    Home,
    Roster  // ✅ NEW
}

[SerializeField] private GameObject _rosterScreen;  // ✅ NEW

private void ApplyScreen(ScreenId screenId)
{
    // ... existing logic ...
    if (_rosterScreen != null) _rosterScreen.SetActive(screenId == ScreenId.Roster);
}
```

### 2. **HomeScreenController.cs**
Wired Characters button:
```csharp
if (navCharactersButton != null)
    navCharactersButton.onClick.AddListener(() => OnNavClicked(ScreenId.Roster)); // ✅ WAS TODO

private void OnNavClicked(ScreenId target)
{
    switch (target)
    {
        // ...
        case ScreenId.Roster:
            screenManager.ShowScreen(ScreenId.Roster);
            break;
    }
}

private void SetActiveNav(ScreenId active)
{
    // ...
    if (navCharactersIcon != null)
        navCharactersIcon.color = active == ScreenId.Roster ? navActiveColor : navNormalColor;
}
```

### 3. **ScreenDeactivator.cs**
Added comment (no functional change needed):
```csharp
public string[] activeScreenNames = new string[]
{
    "LogoScreen",
    "LoadingScreen"
    // RosterScreen managed by ScreenManager, doesn't need to be active at start
};
```

### 4. **RosterCarouselBuilder.cs** (Major Refactor)
Updated to create Roster in Canvas + auto-wire:
```csharp
// Find Canvas (same as Home/Logo)
var canvas = FindCanvasParent();

// Create RosterScreen as CHILD of Canvas (not root)
var rosterScreenGO = new GameObject("RosterScreen");
rosterScreenGO.transform.SetParent(canvas.transform, false);

// Auto-wire to ScreenManager
WireScreenManager(rosterScreenGO);

private static void WireScreenManager(GameObject rosterScreenGO)
{
    var screenManager = Object.FindObjectOfType<GolfinRedux.UI.ScreenManager>();
    if (screenManager != null)
    {
        // Auto-assign _rosterScreen reference
    }
}
```

---

## 🧪 Setup & Testing

### Step 1: Delete Old RosterScreen (If It Exists)
```
If you already created RosterScreen earlier:
- Right-click RosterScreen in Hierarchy
- Delete
```

### Step 2: Run Builder Tool
```
Tools → GOLFIN → Build Roster Carousel (Phase 2a)
```

**What happens:**
- ✅ RosterScreen created as child of Canvas
- ✅ All UI components created (Header, Carousel, Arrows, Dots)
- ✅ Auto-wired to ScreenManager._rosterScreen
- ✅ Dialog confirms success

**Console logs:**
```
[RosterCarouselBuilder] ✓ RosterScreen auto-wired to ScreenManager
[RosterCarouselBuilder] ✅ Carousel built successfully!
```

### Step 3: Enter Play Mode
```
Click Play button (▶️)
```

### Step 4: Test Navigation
**Click Characters button (bottom nav):**
- ✅ Roster screen slides in
- ✅ Characters button highlights (cyan/active color)
- ✅ Home screen is hidden

**Click Home button:**
- ✅ Roster screen slides out
- ✅ Home screen slides in
- ✅ Home button highlights

**Click Characters again:**
- ✅ Roster screen reappears
- ✅ Navigation smooth (fade if FadeController present)

### Step 5: Test Carousel (Inside Roster)
Same as Phase 2a testing:
- ✅ Left/right arrows scroll
- ✅ Placeholder cards created
- ✅ Selection works (console logs: "Selected: char_X")
- ✅ Pagination dots update

---

## ✅ Success Checklist

**Architecture:**
- [ ] ScreenId.Roster added to enum
- [ ] _rosterScreen reference in ScreenManager
- [ ] ApplyScreen() handles Roster
- [ ] HomeScreenController wires navCharactersButton

**Builder Tool:**
- [ ] RosterScreen created as child of Canvas (not root)
- [ ] Auto-wires to ScreenManager
- [ ] Shows success dialog

**Navigation:**
- [ ] Characters button works (was TODO, now functional)
- [ ] Clicking switches to Roster screen
- [ ] Clicking Home switches back
- [ ] Button highlights update correctly

**Carousel:**
- [ ] Carousel initializes in Roster screen
- [ ] Navigation works (arrows, dots)
- [ ] Selection works (placeholder cards clickable)

---

## 🎯 Screen Hierarchy

**ShellScene Hierarchy:**
```
ShellSceneRoot
└── Canvas
    ├── LogoScreen (managed by ScreenManager)
    ├── SplashScreen (managed by ScreenManager)
    ├── LoadingScreen (managed by ScreenManager)
    ├── HomeScreen (managed by ScreenManager)
    ├── RosterScreen (managed by ScreenManager) ✅ NEW
    ├── PersistentUI
    │   ├── TopBar (R display, Settings, Username)
    │   └── BottomNav (Home, Gacha, Tee, Inventory, Characters) ✅ Characters active
    └── SettingsPanel (overlay, managed by SettingsController - not here)
```

---

## 📋 Key Differences: Main Screen vs Overlay

| Aspect | Main Screen (Roster) | Overlay (Settings) |
|--------|---------------------|-------------------|
| Manager | ScreenManager | SettingsController |
| Location | Canvas child | Canvas child (top layer) |
| OnClick | ShowScreen() | OpenSettings() |
| Sort Order | 0-10 | 100 (on top) |
| Shows/Hides | Other screens hide | Stays on top of current |
| Bottom Nav | Controls it | Doesn't control nav |

---

## 🚀 What's Next

### Phase 2b (Detail Panel)
- Create CharacterThumbnailCard prefab (replace placeholders)
- Create CharacterDetailPanel (shows stats, bio, buttons)
- Wire detail panel to carousel selection

### Phase 2c (Level-Up Modal)
- Create LevelUpModal (SP allocation UI)
- Wire Level-Up button to modal
- Test full flow: Select card → Show details → Level up → Allocate SP

### Phase 3 (Polish)
- Visual styling (portraits, animations)
- Localization keys
- Audio feedback
- Integration with gacha/inventory screens

---

## 💡 Design Patterns Used

1. **ScreenManager Pattern**
   - Enum-based screen IDs
   - Centralized show/hide logic
   - Fade support (if FadeController present)
   - Easy to add new screens

2. **Bottom Nav Pattern**
   - Button → OnNavClicked(ScreenId)
   - SetActiveNav() highlights current
   - Icon color changes (active vs normal)

3. **Editor Tool Pattern**
   - One-click setup (no manual inspector wiring for basics)
   - Auto-detect Canvas parent
   - Auto-wire ScreenManager reference
   - Clear success dialog

---

## 🔄 Integration Points

```
User clicks Characters button
    ↓
HomeScreenController.navCharactersButton fires
    ↓
OnNavClicked(ScreenId.Roster) called
    ↓
SetActiveNav(Roster) highlights button
    ↓
screenManager.ShowScreen(ScreenId.Roster) called
    ↓
ScreenManager.ApplyScreen() activates RosterScreen GameObject
    ↓
RosterScreenController.OnEnable() fires
    ↓
CarouselController.PopulateCarousel() creates cards
    ↓
Roster screen visible with carousel ready
```

---

**Created by:** Kai  
**For:** Cesar Guarinoni  
**Status:** Ready for Phase 2b (Detail Panel)  
**Commit:** `🔗 13dfbc5`
