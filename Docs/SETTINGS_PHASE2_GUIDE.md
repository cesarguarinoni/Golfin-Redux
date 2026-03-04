

# Settings Screen - Phase 2 Setup Guide

Phase 2 adds accordion behavior, volume sliders, and language selection to the Settings Screen.

---

## What's New in Phase 2

✅ **Accordion Behavior**
- User Profile, Sound Settings, and Language items expand/collapse on click
- Only one section open at a time
- Smooth animation with arrow rotation

✅ **Sound Settings Submenu**
- Music Volume slider (0-100%)
- SFX Volume slider (0-100%)
- Saves to PlayerPrefs
- Ready for AudioManager integration (Phase 3)

✅ **Language Submenu**
- English and Japanese toggle buttons
- Visual selection indicators (checkmarks)
- Saves preference to PlayerPrefs
- Ready for LocalizationManager integration (Phase 3)

✅ **User Profile Submenu**
- Username editing with validation (3-16 characters)
- Real-time feedback on invalid input
- Updates PersistentUI Top Bar
- Account linking placeholders (Google, Apple, Twitter - Phase 3)

---

## New Scripts Added

1. **SettingsMenuItem.cs** - Individual accordion item with expand/collapse
2. **SettingsControllerPhase2.cs** - Updated controller with accordion management
3. **SoundSettingsSubmenu.cs** - Volume sliders and audio controls
4. **LanguageSubmenu.cs** - Language selection with toggle buttons
5. **UserProfileSubmenu.cs** - Username editing and account linking

---

## Setup Instructions

### Step 1: Add New Scripts to Scene

1. **User Profile Row:**
   - Add `SettingsMenuItem` component
   - Assign button reference (the row's button)
   - Assign submenu container (child GameObject with submenu content)
   - Assign arrow icon (the right arrow image)

2. **Sound Settings Row:**
   - Add `SettingsMenuItem` component
   - Same assignments as above

3. **Language Row:**
   - Add `SettingsMenuItem` component
   - Same assignments as above

### Step 2: Build Submenus

#### User Profile Submenu

Create under UserProfileRow (as child of row):
```
UserProfileRow
├── Button (existing)
├── Icon (existing)
├── Label (existing)
├── Arrow (existing)
└── UserProfileSubmenu (new container)
    ├── UsernameSection
    │   ├── UsernameLabel ("Username")
    │   ├── UsernameInputField (TMP Input Field)
    │   ├── SaveUsernameButton (Button)
    │   └── FeedbackText (TextMeshPro, initially hidden)
    └── AccountLinkingSection (Phase 3)
        ├── LinkGoogleButton
        ├── LinkAppleButton
        └── LinkTwitterButton
```

**Wire up UserProfileSubmenu component:**
- Username Input Field → TMP_InputField
- Save Username Button → Button
- Feedback Text → TextMeshProUGUI
- Set Max Username Length: 16
- Set Min Username Length: 3

#### Sound Settings Submenu

Create under SoundSettingsRow:
```
SoundSettingsRow
├── Button (existing)
├── Icon (existing)
├── Label (existing)
├── Arrow (existing)
└── SoundSettingsSubmenu (new container)
    ├── MusicVolumeSection
    │   ├── MusicLabel ("Music Volume")
    │   ├── MusicVolumeSlider (Slider, 0-1 range)
    │   └── MusicVolumeText (TextMeshPro, shows %)
    └── SFXVolumeSection
        ├── SFXLabel ("SFX Volume")
        ├── SFXVolumeSlider (Slider, 0-1 range)
        └── SFXVolumeText (TextMeshPro, shows %)
```

**Wire up SoundSettingsSubmenu component:**
- Music Volume Slider → Slider
- Music Volume Text → TextMeshProUGUI
- SFX Volume Slider → Slider
- SFX Volume Text → TextMeshProUGUI
- Save To Player Prefs: ✅ (checked)

**Slider Settings:**
- Min Value: 0
- Max Value: 1
- Whole Numbers: ❌ (unchecked)
- Value: 0.8 (default 80%)

#### Language Submenu

Create under LanguageRow:
```
LanguageRow
├── Button (existing)
├── Icon (existing)
├── Label (existing)
├── Arrow (existing)
└── LanguageSubmenu (new container)
    ├── EnglishButton (Button)
    │   ├── Label ("English")
    │   └── Checkmark (Image, initially hidden)
    └── JapaneseButton (Button)
        ├── Label ("日本語")
        └── Checkmark (Image, initially hidden)
```

**Wire up LanguageSubmenu component:**
- English Button → Button
- Japanese Button → Button
- English Checkmark → GameObject (the checkmark icon)
- Japanese Checkmark → GameObject (the checkmark icon)
- Selected Color: Light blue (0.2, 0.6, 1, 1)
- Unselected Color: Gray (0.5, 0.5, 0.5, 1)

### Step 3: Update SettingsController

**Option A: Replace Existing (Recommended)**
1. Remove old `SettingsController` component from SettingsCanvas
2. Add `SettingsControllerPhase2` component
3. Wire up all references (see below)

**Option B: Keep Both (For Testing)**
1. Disable old `SettingsController` component
2. Add `SettingsControllerPhase2` as a new component
3. Test Phase 2, then remove old controller when ready

**Wire up SettingsControllerPhase2:**

**Settings Panel section:**
- Background → Semi-transparent backdrop GameObject
- Settings Panel → Main panel GameObject
- Close Button → Close button

**Menu Items with Accordion:**
- User Profile Item → UserProfileRow (SettingsMenuItem component)
- Sound Settings Item → SoundSettingsRow (SettingsMenuItem component)
- Language Item → LanguageRow (SettingsMenuItem component)

**Simple Menu Buttons (No Accordion):**
- Terms of Use Button → TermsOfUseRow button
- Privacy Policy Button → PrivacyPolicyRow button
- FAQ Button → FAQRow button
- About Button → AboutRow button
- Contact Button → ContactRow button
- Log Out Button → LogOutRow button

**Submenus:**
- User Profile Submenu → UserProfileSubmenu component
- Sound Settings Submenu → SoundSettingsSubmenu component
- Language Submenu → LanguageSubmenu component

### Step 4: Configure Submenu Containers

For each submenu container (UserProfileSubmenu, SoundSettingsSubmenu, LanguageSubmenu GameObject):

1. **Add VerticalLayoutGroup** (optional, for clean spacing):
   - Spacing: 10
   - Child Alignment: Upper Center
   - Child Force Expand: Width ✅, Height ❌
   - Padding: 10px all sides

2. **Set RectTransform:**
   - Anchor: Stretch-stretch (fills parent)
   - Position: 0, 0, 0
   - Initial Height: Set to desired expanded height (e.g., 200px for Sound Settings)

3. **Initial State:**
   - Should start active with proper height (SettingsMenuItem will handle hiding on start)

### Step 5: Test in Play Mode

**Test Accordion:**
1. Open Settings
2. Click User Profile → Should expand with username field
3. Click Sound Settings → User Profile should collapse, Sound Settings should expand
4. Click Language → Sound Settings should collapse, Language should expand
5. Arrow icons should rotate 90° when expanded

**Test Sound Settings:**
1. Expand Sound Settings
2. Drag Music Volume slider → Text should update (e.g., "75")
3. Drag SFX Volume slider → Text should update
4. Close and reopen Settings → Volumes should be saved
5. Check Console → Should see volume change logs

**Test Language Selection:**
1. Expand Language
2. Click Japanese button → Checkmark should appear, English checkmark disappears
3. Click English button → Checkmark moves back
4. Close and reopen Settings → Selection should be saved
5. Check Console → Should see language change logs

**Test User Profile:**
1. Expand User Profile
2. Edit username (e.g., "TestPlayer123")
3. Save button should become active
4. Click Save → Should see success message
5. Check Top Bar → Username should update
6. Try invalid username (e.g., "ab" - too short) → Should see error feedback
7. Try invalid characters (e.g., "test@123") → Should see error feedback

---

## Animation Details

### Accordion Animation

**Expand:**
- Duration: 0.3 seconds
- Curve: EaseInOut (smooth acceleration + deceleration)
- Arrow rotation: 0° → 90° (clockwise)
- Height: 0 → Full height

**Collapse:**
- Duration: 0.2 seconds (slightly faster)
- Curve: Same as expand
- Arrow rotation: 90° → 0°
- Height: Full → 0

### Customization

To adjust animation speed, select any SettingsMenuItem component:
- Expand Duration: Default 0.3s (increase for slower)
- Collapse Duration: Default 0.2s
- Expand Curve: Customize the animation curve in Inspector

---

## Common Issues & Fixes

### Issue: Accordion doesn't animate
**Fix:**
- Check SettingsMenuItem has submenu container assigned
- Check submenu container has RectTransform
- Check submenu container is active (SettingsMenuItem will hide it on Awake)
- Verify arrow icon is assigned

### Issue: Multiple items expanded at once
**Fix:**
- Check all SettingsMenuItem components are registered in SettingsControllerPhase2
- Check OnExpanded events are wired up
- Try clicking slowly (not double-clicking)

### Issue: Sliders don't save
**Fix:**
- Check SoundSettingsSubmenu "Save To Player Prefs" is enabled
- Check slider min/max values are correct (0-1)
- Check Console for save logs

### Issue: Language doesn't change visuals
**Fix:**
- For Phase 2, only checkmarks + console logs work
- Full localization integration is Phase 3
- Check checkmark GameObjects are assigned
- Check buttons are wired to LanguageSubmenu

### Issue: Username doesn't update Top Bar
**Fix:**
- Check PersistentUIManager.Instance is not null
- Check PersistentUIManager has UpdateUsername() method
- If using Phase 1 PersistentUIManager, update to Phase 2 version (see below)

---

## PersistentUIManager Update (Optional)

If your PersistentUIManager doesn't have UpdateUsername(), add this method:

```csharp
/// <summary>
/// Update the username display in the Top Bar.
/// </summary>
public void UpdateUsername(string newUsername)
{
    if (usernameText != null)
    {
        usernameText.text = newUsername;
    }
    
    Debug.Log($"[PersistentUI] Username updated: {newUsername}");
}
```

---

## What's NOT in Phase 2

❌ **Account Linking** - Buttons exist but don't work yet (Phase 3)
❌ **Webview Integration** - Terms/Privacy/FAQ/Contact open external browser (Phase 3)
❌ **AudioManager Integration** - Volume sliders save but don't control audio yet (Phase 3)
❌ **LocalizationManager Integration** - Language selection saves but doesn't switch UI language yet (Phase 3)
❌ **Log Out Confirmation** - Just logs for now (Phase 3)
❌ **About Screen Modal** - Just logs for now (Phase 3)

---

## Next Steps (Phase 3)

After Phase 2 is working:
- Integrate SoundSettingsSubmenu with AudioManager (control actual music/SFX)
- Integrate LanguageSubmenu with LocalizationManager (switch UI language)
- Implement account linking (Google, Apple, Twitter via Cognito)
- Add webview plugin (UniWebView or Vuplex) for Terms/Privacy/FAQ/Contact
- Create About screen modal with app version + licenses
- Add Log Out confirmation modal with session clear

---

## Time Estimate

- Setup (adding components, wiring): 30-40 minutes
- Testing: 10-15 minutes
- Styling (colors, spacing, polish): 15-20 minutes

**Total: ~1 hour** for complete Phase 2 implementation

---

## Files Reference

**New Scripts:**
- `Assets/Scripts/UI/SettingsMenuItem.cs`
- `Assets/Scripts/UI/SettingsControllerPhase2.cs`
- `Assets/Scripts/UI/SoundSettingsSubmenu.cs`
- `Assets/Scripts/UI/LanguageSubmenu.cs`
- `Assets/Scripts/UI/UserProfileSubmenu.cs`

**Documentation:**
- `Docs/SETTINGS_PHASE2_GUIDE.md` (this file)
- `Docs/SettingsScreen/README.md` (Phase 1 reference)

---

**Let's build Phase 2! 🚀**
