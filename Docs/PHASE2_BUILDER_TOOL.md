# Phase 2 Builder Tool - Quick Start

**Automatic submenu creation in 30 seconds!**

---

## What It Does

The Phase 2 Builder Tool automatically creates all three Settings submenus with:
- ✅ Complete GameObject hierarchies
- ✅ All UI components (TextMeshPro, Sliders, Buttons, Images)
- ✅ Proper RectTransforms (positions, sizes, anchors)
- ✅ Script components with references wired up
- ✅ Default values configured (colors, font sizes, slider ranges)

**No manual work needed!**

---

## How to Use

### Step 1: Open the Tool

In Unity: **Tools → GOLFIN → Build Phase 2 Submenus**

### Step 2: Assign Parent Rows

Drag your Settings rows into the fields:
- **User Profile Row**: Drag the `UserProfileRow` GameObject
- **Sound Settings Row**: Drag the `SoundSettingsRow` GameObject
- **Language Row**: Drag the `LanguageRow` GameObject

### Step 3: Build!

Click **"Build All Submenus"** (or build them individually)

### Step 4: Done!

The tool creates:

**UserProfileSubmenu:**
- UsernameLabel, UsernameInputField, SaveUsernameButton, FeedbackText
- UserProfileSubmenu component with references wired

**SoundSettingsSubmenu:**
- MusicVolumeSlider, MusicVolumeText, SFXVolumeSlider, SFXVolumeText
- SoundSettingsSubmenu component with references wired

**LanguageSubmenu:**
- EnglishButton, JapaneseButton with Labels and Checkmarks
- LanguageSubmenu component with references wired

---

## Time Comparison

| Method | Time Required |
|--------|--------------|
| **Builder Tool** | ~30 seconds ⚡ |
| Manual Creation | ~38 minutes |
| Broken YAML Prefabs | ∞ (didn't work) |

---

## Features

### Automatic Component Creation

**TextMeshPro:**
- White text color
- Proper font sizes
- Correct alignment

**Sliders:**
- Range: 0-1 (0-100%)
- Default: 0.8 (80%)
- Background + Fill + Handle created
- Fill color: Light blue

**Buttons:**
- Dark gray background
- White text labels
- Proper target graphic

**Input Fields:**
- Character limit: 16
- Text component created
- Proper text viewport

### Automatic Wire-Up

All script references are automatically connected:
- Input fields → Script fields
- Buttons → Script fields
- Sliders → Script fields
- Text components → Script fields

No manual dragging in Inspector needed!

### Proper RectTransforms

All GameObjects have:
- Correct anchors (top-stretch, top-left, top-right, etc.)
- Proper positions
- Correct sizes
- Right pivot points

---

## After Building

### Add SettingsMenuItem to Rows

For each row, you still need to:
1. Add `SettingsMenuItem` component to the row GameObject
2. Assign:
   - **Button**: The row's button component
   - **Submenu Container**: The submenu that was just created
   - **Arrow Icon**: The arrow image on the right

This takes ~1 minute per row (3 minutes total).

### Customize (Optional)

You can customize after building:
- Change colors
- Adjust font sizes
- Reposition elements
- Add sprites/icons

The tool gives you a working foundation!

---

## Troubleshooting

### Tool doesn't appear in menu

Make sure:
- The script is in `Assets/Scripts/UI/Editor/` folder
- Unity has compiled it (check Console for errors)
- No compilation errors in the project

### "Build All" button is grayed out

Assign at least one row field before building.

### Submenu already exists

The tool creates new GameObjects. If a submenu already exists:
1. Delete the old one
2. Run the tool again

Or build individually to only replace specific submenus.

### References not wired up

The tool uses reflection to set private fields. If it fails:
- Check script field names match (case-sensitive)
- Check serialization (fields should be [SerializeField])
- Wire up manually in Inspector (the GameObjects are still created correctly)

---

## Technical Details

**What the tool creates:**

- GameObjects with RectTransform
- UI components (Image, Button, Slider, TextMeshPro, TMP_InputField)
- Script components (UserProfileSubmenu, SoundSettingsSubmenu, LanguageSubmenu)
- Proper parent-child hierarchy
- Correct anchoring and positioning

**What you need to add:**

- SettingsMenuItem component to parent rows (3 times)
- Wire up button, submenu, arrow references (if reflection failed)
- Sprites for checkmarks (optional)
- Custom styling (optional)

---

## Source Code

The builder tool is at:
`Assets/Scripts/UI/Editor/SettingsPhase2Builder.cs`

Feel free to customize it for your needs!

---

**Build Phase 2 in 30 seconds instead of 40 minutes!** ⚡
