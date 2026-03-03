# GOLFIN Settings Screen - Phase 1

## What's Included

This package contains everything you need to implement the Settings Screen Phase 1 for GOLFIN Redux.

### Files

1. **Scripts/**
   - `PersistentUIManager.cs` - Singleton manager for Top Bar + Bottom Nav (persists across scenes)
   - `SettingsController.cs` - Controller for Settings Screen (static panel with all menu items)

2. **Documentation/**
   - `PREFAB_STRUCTURE.md` - Complete Unity hierarchy and component setup
   - `IMPLEMENTATION_GUIDE.md` - Step-by-step instructions, troubleshooting, optimization tips
   - `README.md` - This file

### Phase 1 Features

✅ **Persistent Top Bar**
- Reward Points display
- Settings button
- Username display

✅ **Persistent Bottom Navigation Bar**
- Home, Gacha, Main Play, Inventory, Characters buttons
- Current screen highlight
- Survives scene transitions (DontDestroyOnLoad)

✅ **Settings Panel**
- Full-screen overlay
- 9 menu items (User Profile, Sound Settings, Language, Terms of Use, Privacy Policy, FAQ, About, Contact, Log Out)
- Close button (only way to close panel)
- Click logging for all buttons

### What's NOT in Phase 1

❌ Accordion expand/collapse behavior (Phase 2)
❌ Volume sliders (Phase 2)
❌ Language selection screen (Phase 2)
❌ Webview integration (Phase 3)
❌ User profile editing (Phase 3)
❌ Log out confirmation modal (Phase 3)

---

## Quick Start

1. **Import Scripts:**
   ```
   Copy Scripts/*.cs to Assets/Scripts/UI/
   ```

2. **Read the Guides:**
   - Start with `IMPLEMENTATION_GUIDE.md` for step-by-step setup
   - Reference `PREFAB_STRUCTURE.md` for exact hierarchy

3. **Build the Prefabs:**
   - Create `PersistentUI` prefab (Top Bar + Bottom Nav)
   - Create `SettingsCanvas` prefab (Settings Panel)
   - Wire up all inspector references

4. **Test:**
   - Click Settings button → Panel opens
   - Click Close button → Panel closes
   - All buttons log to Console

---

## Next Steps

### Phase 2: Accordion + Submenus
- Expand/collapse animation for User Profile, Sound Settings, Language
- Music Volume + SFX Volume sliders in Sound Settings submenu
- Language selection screen (English/Japanese)
- Only one section open at a time

### Phase 3: Full Functionality
- Webview integration for Terms/Privacy/FAQ/Contact
- User profile editing + account linking
- Log out confirmation modal + session clear
- About screen with app version + licenses

---

## Requirements

- Unity 2021.3 or higher (recommended)
- TextMeshPro package installed
- Target platforms: iOS, Android

---

## Design References

Check the GitHub repo for visual references:
`Assets/References/Settings/`
- Settings Screen.png
- User Profile.png
- Sounds.png
- Language.png
- Terms of Use.png
- Privacy.png
- FAQ.png
- About.png
- Contact.png

---

## Architecture Notes

### Singleton Pattern
Both `PersistentUIManager` and `SettingsController` use the singleton pattern:
- Only one instance exists at a time
- Accessible globally via `ClassName.Instance`
- `PersistentUIManager` uses `DontDestroyOnLoad` to survive scene transitions

### Why Persistent UI?
Top Bar and Bottom Nav are present on almost every screen (Home, Gacha, Inventory, Characters, Settings). Instead of duplicating them in each scene, we create them once and persist them. This:
- Reduces memory usage
- Prevents flicker during scene transitions
- Keeps UI state consistent (reward points, username, etc.)

### Settings Panel Lifecycle
- Starts inactive (`settingsPanel.SetActive(false)`)
- Opens when Settings button clicked
- Closes only with Close button (backdrop click disabled)
- Can be opened from any screen

---

## Customization

### Colors & Styling
All colors, fonts, sizes are configurable in the Unity Editor:
- Menu row backgrounds
- Button hover/press states
- Text colors and sizes
- Icon tints

### Menu Items
To add/remove menu items:
1. Edit `SettingsController.cs` - add new button reference
2. Update `PREFAB_STRUCTURE.md` - document new structure
3. Create new row in SettingsPanel hierarchy
4. Wire up new button in SettingsController inspector

### Animations
For smoother open/close animations, consider adding:
- Fade in/out for backdrop
- Scale animation for SettingsPanel (0.9 → 1.0)
- Slide in from bottom for mobile feel

Use DOTween or Unity Animator for best results.

---

## Support

Built by Kai for GOLFIN Redux 🚀

Questions? Check the Implementation Guide or ask in the dev chat.

Happy building! ⛳
