# Settings Screen - Unity Prefab Structure (Phase 1)

## Overview
This document describes the complete Unity prefab hierarchy for the Settings Screen. All UI elements are static buttons at this stage (no expand/collapse behavior yet).

---

## Root Objects

### 1. PersistentUI (Singleton, DontDestroyOnLoad)
This object persists across all scenes and contains Top Bar + Bottom Nav.

```
PersistentUI (Canvas - Screen Space Overlay)
├── PersistentUIManager (Script)
├── TopBar (Panel)
│   ├── Background (Image)
│   ├── RewardPointsIcon (Image)
│   ├── RewardPointsText (TextMeshProUGUI)
│   ├── SettingsButton (Button)
│   │   └── SettingsIcon (Image)
│   └── UsernameText (TextMeshProUGUI)
│
└── BottomNavigationBar (Panel)
    ├── Background (Image)
    ├── HomeButton (Button)
    │   ├── HomeIcon (Image)
    │   └── Highlight (Image - disabled by default)
    ├── GachaButton (Button)
    │   ├── GachaIcon (Image)
    │   └── Highlight (Image - disabled by default)
    ├── MainPlayButton (Button)
    │   ├── MainPlayIcon (Image)
    │   └── Highlight (Image - disabled by default)
    ├── InventoryButton (Button)
    │   ├── InventoryIcon (Image)
    │   └── Highlight (Image - disabled by default)
    └── CharactersButton (Button)
        ├── CharactersIcon (Image)
        └── Highlight (Image - disabled by default)
```

---

### 2. SettingsCanvas (Scene-Specific)
This canvas is instantiated when settings are opened. It overlays the current screen.

```
SettingsCanvas (Canvas - Screen Space Overlay, Sort Order: 100)
├── SettingsController (Script)
├── Background (Image - semi-transparent black overlay)
│
└── SettingsPanel (Panel - main container)
    ├── PanelBackground (Image)
    ├── SettingsList (Vertical Layout Group)
    │   ├── UserProfileRow (Button - full width clickable)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "User Profile"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── SoundSettingsRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "Sound Settings"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── LanguageRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "Language"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── TermsOfUseRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "Terms of Use"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── PrivacyPolicyRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "Privacy Policy"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── FaqRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "FAQ"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── AboutRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "About"
    │   │   └── RightArrow (Image)
    │   │
    │   ├── ContactRow (Button)
    │   │   ├── LeftIcon (Image)
    │   │   ├── Label (TextMeshProUGUI) "Contact"
    │   │   └── RightArrow (Image)
    │   │
    │   └── LogOutRow (Button - red/destructive styling)
    │       ├── LeftIcon (Image)
    │       └── Label (TextMeshProUGUI) "Log Out"
    │
    └── CloseButton (Button - bottom of panel)
        ├── Background (Image)
        └── Label (TextMeshProUGUI) "CLOSE"
```

---

## Component Setup

### PersistentUIManager
- **Script Location:** `Scripts/PersistentUIManager.cs`
- **Attached To:** PersistentUI GameObject
- **Inspector Settings:**
  - Top Bar References: Drag & drop TopBar panel + all child elements
  - Bottom Navigation Bar References: Drag & drop BottomNavigationBar panel + all buttons
  - Current Screen Highlight: Drag & drop each button's Highlight image

### SettingsController
- **Script Location:** `Scripts/SettingsController.cs`
- **Attached To:** SettingsCanvas GameObject
- **Inspector Settings:**
  - Settings Panel: Drag & drop SettingsPanel GameObject
  - Close Button: Drag & drop CloseButton
  - Settings Menu Items: Drag & drop all row buttons (UserProfileRow, SoundSettingsRow, etc.)
  - Menu Item Icons: Drag & drop each row's LeftIcon
  - Menu Item Labels: Drag & drop each row's Label (TextMeshProUGUI)
  - Menu Item Right Arrows: Drag & drop each row's RightArrow (except LogOutRow)

---

## Styling Recommendations

### Menu Rows
- **Height:** 80-100px
- **Spacing:** 10px between rows
- **Background:** Semi-transparent white or gradient
- **Hover/Press:** Scale animation (0.95x) or color tint

### Icons
- **Size:** 40x40px
- **Padding:** 20px from left edge

### Labels
- **Font Size:** 24-28pt
- **Color:** White or dark gray (depending on background)
- **Alignment:** Center-left

### Right Arrows
- **Size:** 24x24px
- **Padding:** 20px from right edge
- **Rotation:** 0° (pointing right)

### Close Button
- **Width:** 200-300px
- **Height:** 60px
- **Position:** Bottom center of SettingsPanel
- **Margin:** 30px from bottom edge

---

## Layout Constraints

### SettingsPanel
- **Anchor:** Center
- **Size:** 800x1200px (adjust for different screen sizes)
- **Padding:** 40px all sides

### SettingsList (Vertical Layout Group)
- **Spacing:** 10px
- **Child Alignment:** Upper Center
- **Child Force Expand:** Width = true, Height = false
- **Child Control Size:** Width = true, Height = true

### Each Row (Horizontal Layout Group)
- **Spacing:** 20px
- **Child Alignment:** Middle Left
- **Padding:** Left = 20px, Right = 20px

---

## Canvas Settings

### PersistentUI Canvas
- **Render Mode:** Screen Space - Overlay
- **Pixel Perfect:** True (optional, for crisp UI)
- **Sort Order:** 0
- **Canvas Scaler:** Scale With Screen Size
  - Reference Resolution: 1080x1920 (portrait) or 1920x1080 (landscape)
  - Match: Width or Height (depends on orientation)

### SettingsCanvas
- **Render Mode:** Screen Space - Overlay
- **Sort Order:** 100 (appears above PersistentUI)
- **Canvas Scaler:** Same as PersistentUI

---

## Asset Import

### Icons
Import icons from your asset library or design tool. Suggested sizes:
- Left Icons: 64x64px (or higher resolution, scaled down)
- Right Arrows: 48x48px
- Button Backgrounds: Use 9-slice for scalability

### Fonts
- Import TextMeshPro fonts with proper character sets for English + Japanese
- Set up Font Asset in TextMeshPro settings

---

## Phase 1 Implementation Checklist

- [ ] Create PersistentUI prefab with TopBar + BottomNav
- [ ] Attach PersistentUIManager script
- [ ] Wire up all buttons in PersistentUIManager inspector
- [ ] Create SettingsCanvas prefab
- [ ] Attach SettingsController script
- [ ] Create all menu rows with Button, Icon, Label, Arrow
- [ ] Wire up all menu row buttons in SettingsController inspector
- [ ] Style all UI elements (colors, fonts, sizes)
- [ ] Test opening/closing settings panel
- [ ] Test all button click logs (Debug.Log should fire for each button)
- [ ] Test PersistentUI across scene transitions (should not reload)

---

## Next Steps (Phase 2)

Phase 2 will add:
- Accordion expand/collapse behavior for User Profile, Sound Settings, Language
- Smooth animations (DOTween or Unity Animator)
- Sound Settings submenu with Music Volume + SFX Volume sliders
- Language selection screen (English/Japanese)
- Disable clicking backdrop to close (only Close button works)

---

## Reference Images
Check `Assets/References/Settings/` for visual reference:
- Settings Screen.png - Main layout
- User Profile.png - Expanded view
- Sounds.png - Expanded view
- Language.png - Expanded view
- Terms of Use.png - Webview example
- Privacy.png - Webview example
- FAQ.png - Webview example
- About.png - Version info modal
- Contact.png - Contact form webview
