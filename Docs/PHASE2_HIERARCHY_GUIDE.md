# Phase 2 - Manual Hierarchy Creation Guide

Since YAML prefabs had issues, here's a step-by-step guide to create the hierarchies manually. **Much more reliable!**

Copy-paste GameObject names directly from this doc to avoid typos.

---

## UserProfile Submenu (15 min)

### 1. Create Container

Under `UserProfileRow`:
1. Right-click → Create Empty → Name: `UserProfileSubmenu`
2. Add component: `User Profile Submenu` script
3. RectTransform:
   - Anchor: Top-stretch (top-left + top-right)
   - Position Y: 0
   - Height: 250

### 2. Create UsernameSection

Under `UserProfileSubmenu`:
1. Create Empty → Name: `UsernameSection`
2. RectTransform:
   - Anchor: Top-stretch
   - Pos Y: -10
   - Width: -40 (offset from edges)
   - Height: 150

### 3. Create Username Fields

Under `UsernameSection`:

**A. UsernameLabel:**
- Create → UI → Text - TextMeshPro → Name: `UsernameLabel`
- Text: "Username"
- Font Size: 20
- RectTransform: Anchor top-left, Pos (20, -15), Size (200, 30)

**B. UsernameInputField:**
- Create → UI → InputField - TextMeshPro → Name: `UsernameInputField`
- Character Limit: 16
- RectTransform: Anchor top-stretch, Pos Y: -50, Width: -40, Height: 40

**C. SaveUsernameButton:**
- Create → UI → Button - TextMeshPro → Name: `SaveUsernameButton`
- Child Text: "Save"
- RectTransform: Anchor top-left, Pos (20, -100), Size (150, 40)

**D. FeedbackText:**
- Create → UI → Text - TextMeshPro → Name: `FeedbackText`
- Text: (empty)
- Font Size: 16
- Color: Red
- RectTransform: Anchor top-stretch, Pos Y: -150, Width: -40, Height: 30
- **Set to inactive** (uncheck in Inspector)

### 4. Wire Up UserProfileSubmenu

Select `UserProfileSubmenu` GameObject:
- Username Input Field: Drag `UsernameInputField`
- Save Username Button: Drag `SaveUsernameButton`
- Feedback Text: Drag `FeedbackText`
- Max Username Length: 16
- Min Username Length: 3

---

## Sound Settings Submenu (10 min)

### 1. Create Container

Under `SoundSettingsRow`:
1. Create Empty → Name: `SoundSettingsSubmenu`
2. Add component: `Sound Settings Submenu` script
3. RectTransform:
   - Anchor: Top-stretch
   - Pos Y: 0
   - Height: 180

### 2. Create MusicVolumeSection

Under `SoundSettingsSubmenu`:
1. Create Empty → Name: `MusicVolumeSection`
2. RectTransform: Top-stretch, Pos Y: -10, Width: -40, Height: 80

Under `MusicVolumeSection`:

**A. MusicLabel:**
- UI → Text - TextMeshPro → Name: `MusicLabel`
- Text: "Music Volume"
- Font Size: 18
- RectTransform: Top-left anchor, Pos (20, -15), Size (200, 30)

**B. MusicVolumeSlider:**
- UI → Slider → Name: `MusicVolumeSlider`
- Min: 0, Max: 1, Value: 0.8
- Whole Numbers: **unchecked**
- RectTransform: Top-stretch, Pos (20, -50), Width: -240, Height: 20
- Direction: Left to Right

**C. MusicVolumeText:**
- UI → Text - TextMeshPro → Name: `MusicVolumeText`
- Text: "80"
- Font Size: 18
- Alignment: Right
- RectTransform: Top-right anchor, Pos (-20, -50), Size (60, 30)

### 3. Create SFXVolumeSection

Under `SoundSettingsSubmenu`:
1. Create Empty → Name: `SFXVolumeSection`
2. RectTransform: Top-stretch, Pos Y: -100, Width: -40, Height: 80

Under `SFXVolumeSection` (same as Music, but with "SFX"):

**A. SFXLabel:**
- Text: "SFX Volume"
- (same settings as MusicLabel)

**B. SFXVolumeSlider:**
- (same settings as MusicVolumeSlider)

**C. SFXVolumeText:**
- Text: "80"
- (same settings as MusicVolumeText)

### 4. Wire Up SoundSettingsSubmenu

Select `SoundSettingsSubmenu`:
- Music Volume Slider: Drag `MusicVolumeSlider`
- Music Volume Text: Drag `MusicVolumeText`
- SFX Volume Slider: Drag `SFXVolumeSlider`
- SFX Volume Text: Drag `SFXVolumeText`
- Save To Player Prefs: ✅ (checked)

---

## Language Submenu (8 min)

### 1. Create Container

Under `LanguageRow`:
1. Create Empty → Name: `LanguageSubmenu`
2. Add component: `Language Submenu` script
3. RectTransform:
   - Anchor: Top-stretch
   - Pos Y: 0
   - Height: 120

### 2. Create EnglishButton

Under `LanguageSubmenu`:
1. UI → Button → Name: `EnglishButton`
2. RectTransform: Top-stretch, Pos Y: -10, Width: -40, Height: 50

Under `EnglishButton`:

**A. Label:**
- UI → Text - TextMeshPro → Name: `Label`
- Text: "English"
- Font Size: 20
- RectTransform: Stretch (fill parent), Margins: 0

**B. Checkmark:**
- UI → Image → Name: `Checkmark`
- Set sprite to checkmark icon (✓)
- Color: Green or Blue
- RectTransform: Right-center anchor, Pos (-20, 0), Size (24, 24)
- **Set to active** (English default)

### 3. Create JapaneseButton

Under `LanguageSubmenu`:
1. UI → Button → Name: `JapaneseButton`
2. RectTransform: Top-stretch, Pos Y: -70, Width: -40, Height: 50

Under `JapaneseButton`:

**A. Label:**
- Text: "日本語"
- (same settings as English Label)

**B. Checkmark:**
- (same as English Checkmark)
- **Set to inactive** (English is default)

### 4. Wire Up LanguageSubmenu

Select `LanguageSubmenu`:
- English Button: Drag `EnglishButton`
- Japanese Button: Drag `JapaneseButton`
- English Checkmark: Drag `EnglishButton/Checkmark` (the GameObject, not Image)
- Japanese Checkmark: Drag `JapaneseButton/Checkmark`
- Selected Color: (0.2, 0.6, 1, 1) - Light blue
- Unselected Color: (0.5, 0.5, 0.5, 1) - Gray

---

## Add SettingsMenuItem to Rows

For each row (UserProfile, SoundSettings, Language):

1. Select the row GameObject (e.g., `UserProfileRow`)
2. Add Component → `Settings Menu Item`
3. Assign:
   - **Button:** The row's button component
   - **Submenu Container:** The submenu you just created
   - **Arrow Icon:** The arrow image on the right side of the row
4. Animation settings (defaults are fine):
   - Expand Duration: 0.3
   - Collapse Duration: 0.2

---

## Quick Reference: GameObject Names

Copy-paste these names to avoid typos:

**UserProfile:**
```
UserProfileSubmenu
  UsernameSection
    UsernameLabel
    UsernameInputField
    SaveUsernameButton
    FeedbackText
  AccountLinkingSection
```

**SoundSettings:**
```
SoundSettingsSubmenu
  MusicVolumeSection
    MusicLabel
    MusicVolumeSlider
    MusicVolumeText
  SFXVolumeSection
    SFXLabel
    SFXVolumeSlider
    SFXVolumeText
```

**Language:**
```
LanguageSubmenu
  EnglishButton
    Label
    Checkmark
  JapaneseButton
    Label
    Checkmark
```

---

## Estimated Time

- UserProfile: 15 min
- SoundSettings: 10 min
- Language: 8 min
- Wire up SettingsMenuItem (3 rows): 5 min

**Total: ~38 minutes**

---

## Tips

**Duplicate GameObjects quickly:**
- Create one button/section, configure it, then Duplicate (Ctrl+D / Cmd+D)
- Just change the text and positions

**Use Layout Groups (optional):**
- Add VerticalLayoutGroup to sections for automatic spacing
- Spacing: 10, Child Force Expand: Width only

**Check anchors:**
- Use top-stretch for full-width elements
- Use top-left for fixed-position elements
- Use top-right for right-aligned elements (like volume text)

---

**Follow this guide and you'll have Phase 2 working in ~40 minutes!** 🚀
