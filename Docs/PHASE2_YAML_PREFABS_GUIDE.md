# Phase 2 YAML Prefabs - Quick Setup Guide

I've created complete GameObject hierarchies as YAML prefabs to save you time! You just need to import them into your existing Settings prefab and add UI components (Images, Text, etc.).

---

## What's Included

Three ready-to-use submenu prefabs:

1. **UserProfileSubmenu.prefab** - Username editing + account linking section
2. **SoundSettingsSubmenu.prefab** - Music + SFX volume sliders
3. **LanguageSubmenu.prefab** - English/Japanese toggle buttons

Each prefab has:
- ✅ Complete GameObject hierarchy
- ✅ RectTransforms configured (positions, sizes, anchors)
- ✅ Script components added with references
- ⚠️ Missing: UI components (Image, TextMeshPro, Slider)

---

## How to Use These Prefabs

### Option 1: Import Entire Prefab (Easiest)

1. In Unity Project window, navigate to `Assets/Prefabs/UI/`
2. Drag a submenu prefab into your Hierarchy (e.g., `UserProfileSubmenu.prefab`)
3. Make it a child of the appropriate row (e.g., under `UserProfileRow`)
4. Add UI components (see below)
5. Wire up references in Inspector

### Option 2: Copy Hierarchy from YAML (Advanced)

If Unity has trouble importing the prefabs:

1. Open the `.prefab` file in a text editor
2. Copy the YAML content
3. In Unity, create a new empty GameObject
4. Select it, then paste in Inspector (Unity will create the hierarchy)
5. Add UI components

---

## Adding UI Components

Each GameObject needs its UI components added. Here's what to add:

### UserProfileSubmenu

**UsernameLabel:**
- Add `TextMeshProUGUI` component
- Set text: "Username"
- Font size: 20

**UsernameInputField:**
- Add `TMP_InputField` component
- Character limit: 16
- Content type: Standard

**SaveUsernameButton:**
- Add `Image` component (button background)
- Add `Button` component
- Add child GameObject → Add `TextMeshProUGUI` → Text: "Save"

**FeedbackText:**
- Add `TextMeshProUGUI` component
- Color: Red (1, 0, 0)
- Font size: 16
- Starts inactive

### SoundSettingsSubmenu

**MusicLabel / SFXLabel:**
- Add `TextMeshProUGUI` component
- Set text: "Music Volume" / "SFX Volume"
- Font size: 18

**MusicVolumeSlider / SFXVolumeSlider:**
- Add `Slider` component
- Min: 0, Max: 1
- Value: 0.8 (default 80%)
- Whole Numbers: unchecked
- Add child GameObjects for Background, Fill Area, Handle (Unity creates these automatically when you add Slider)

**MusicVolumeText / SFXVolumeText:**
- Add `TextMeshProUGUI` component
- Text: "80" (default)
- Font size: 18

### LanguageSubmenu

**EnglishButton / JapaneseButton:**
- Add `Image` component (button background)
- Add `Button` component

**Label (under each button):**
- Add `TextMeshProUGUI` component
- English: "English"
- Japanese: "日本語"
- Font size: 20

**Checkmark (under each button):**
- Add `Image` component
- Set sprite to a checkmark icon (✓)
- Size: 24x24
- Starts inactive (English checkmark active by default)

---

## Wire Up References

After adding UI components:

### UserProfileRow

1. Add `SettingsMenuItem` component
2. Assign:
   - Button: The row's button
   - Submenu Container: UserProfileSubmenu GameObject
   - Arrow Icon: The arrow image on the right

### UserProfileSubmenu Component

Wire up fields:
- Username Input Field: UsernameInputField (TMP_InputField)
- Save Username Button: SaveUsernameButton (Button)
- Feedback Text: FeedbackText (TextMeshProUGUI)
- Leave account linking fields empty (Phase 3)

### SoundSettingsRow

1. Add `SettingsMenuItem` component
2. Assign button, submenu container, arrow

### SoundSettingsSubmenu Component

Wire up:
- Music Volume Slider: MusicVolumeSlider (Slider)
- Music Volume Text: MusicVolumeText (TextMeshProUGUI)
- SFX Volume Slider: SFXVolumeSlider (Slider)
- SFX Volume Text: SFXVolumeText (TextMeshProUGUI)
- Save To Player Prefs: ✅ (checked)

### LanguageRow

1. Add `SettingsMenuItem` component
2. Assign button, submenu container, arrow

### LanguageSubmenu Component

Wire up:
- English Button: EnglishButton (Button)
- Japanese Button: JapaneseButton (Button)
- English Checkmark: EnglishButton/Checkmark (GameObject)
- Japanese Checkmark: JapaneseButton/Checkmark (GameObject)

---

## Estimated Time

- Import prefabs: 2 min
- Add UI components: 15-20 min
- Wire up references: 10 min
- Test: 5 min

**Total: ~30 minutes** (compared to 1 hour if building from scratch)

---

## Troubleshooting

### Prefab won't import

Try Option 2 (copy YAML manually) or:
1. Create empty GameObjects manually
2. Use the YAML as a reference for names and positions
3. Copy RectTransform values from YAML

### Missing script references

Make sure:
- All Phase 2 scripts are compiled (.meta files exist)
- Scripts are in `Assets/Scripts/UI/`
- No compilation errors in Console

### Can't find slider components

When you add a `Slider` component, Unity auto-generates:
- Background
- Fill Area → Fill
- Handle Slide Area → Handle

Just assign Fill Rect and Handle Rect in the Slider component.

---

## Full Manual Setup (If YAML Doesn't Work)

If the YAML prefabs don't work in your Unity version, follow `SETTINGS_PHASE2_GUIDE.md` Step 2 to build the hierarchies manually. The YAML files show you the exact structure needed!

---

**The YAML prefabs give you a head start - just add visuals and wire up!** 🚀
