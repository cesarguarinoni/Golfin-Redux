# Settings Screen Phase 1 - Implementation Guide

## Quick Start (Step-by-Step)

### Step 1: Import Scripts
1. Copy `Scripts/PersistentUIManager.cs` to `Assets/Scripts/UI/`
2. Copy `Scripts/SettingsController.cs` to `Assets/Scripts/UI/`
3. Let Unity compile the scripts

### Step 2: Create PersistentUI Prefab
1. Create a new Canvas: `GameObject > UI > Canvas`
2. Rename it to `PersistentUI`
3. Set Canvas settings:
   - Render Mode: Screen Space - Overlay
   - Sort Order: 0
4. Add `PersistentUIManager` script component
5. Create the Top Bar structure (see PREFAB_STRUCTURE.md)
6. Create the Bottom Navigation Bar structure
7. Wire up all references in PersistentUIManager inspector
8. Save as prefab: `Assets/Prefabs/UI/PersistentUI.prefab`

### Step 3: Create SettingsCanvas Prefab
1. Create a new Canvas: `GameObject > UI > Canvas`
2. Rename it to `SettingsCanvas`
3. Set Canvas settings:
   - Render Mode: Screen Space - Overlay
   - Sort Order: 100 (above PersistentUI)
4. Add `SettingsController` script component
5. Create the SettingsPanel structure (see PREFAB_STRUCTURE.md)
6. Wire up all references in SettingsController inspector
7. Save as prefab: `Assets/Prefabs/UI/SettingsCanvas.prefab`

### Step 4: Scene Setup
1. Add `PersistentUI` prefab to your main scene (Home Screen)
2. Add `SettingsCanvas` prefab to the same scene
3. Make sure PersistentUI is above SettingsCanvas in the Hierarchy (render order)

### Step 5: Test
1. Enter Play Mode
2. Click Settings button in Top Bar → Settings panel should open
3. Click Close button → Settings panel should close
4. Click any menu item → Check Console for Debug.Log messages
5. Click Bottom Nav buttons → Check Console for navigation logs
6. Exit Play Mode and re-enter → PersistentUI should persist (check Console for singleton messages)

---

## Common Issues & Fixes

### Issue: PersistentUI disappears when changing scenes
**Fix:** Make sure `DontDestroyOnLoad(gameObject)` is called in `PersistentUIManager.Awake()`. Check Console for singleton messages.

### Issue: Buttons not clickable
**Fix:** 
- Make sure each button has a `Button` component
- Check Canvas `Graphic Raycaster` is enabled
- Make sure buttons have an `Image` component (even if transparent)
- Check EventSystem exists in the scene

### Issue: Settings panel doesn't open
**Fix:**
- Check `SettingsController.Instance` is not null in `PersistentUIManager.OnSettingsButtonClick()`
- Make sure `settingsPanel` reference is assigned in SettingsController inspector
- Check `settingsPanel.SetActive(true)` is being called

### Issue: Text not showing (TextMeshPro)
**Fix:**
- Import TextMeshPro Essentials: `Window > TextMeshPro > Import TMP Essential Resources`
- Assign a TextMeshPro font to all TextMeshProUGUI components
- Check text color is not transparent or same as background

### Issue: UI looks squashed/stretched on different devices
**Fix:**
- Use Canvas Scaler with "Scale With Screen Size"
- Set Reference Resolution to your target resolution (e.g., 1080x1920 for portrait)
- Use Anchors properly (stretch, center, etc.)

---

## Optimization Tips

### 1. Use Object Pooling for Modals
Instead of Instantiate/Destroy, keep SettingsCanvas in the scene and just SetActive(true/false).

### 2. Lazy Initialize SettingsCanvas
Don't activate SettingsCanvas until the user clicks Settings button for the first time:
```csharp
private void OnSettingsButtonClick()
{
    if (settingsCanvasInstance == null)
    {
        settingsCanvasInstance = Instantiate(settingsCanvasPrefab);
    }
    SettingsController.Instance?.OpenSettings();
}
```

### 3. Batch UI Updates
When updating multiple UI elements (e.g., reward points, username), do it in one frame to avoid multiple canvas rebuilds.

### 4. Disable Raycast Target on Non-Interactive Elements
Uncheck "Raycast Target" on all Images/Texts that don't need to be clickable (icons, labels, backgrounds).

---

## Testing Checklist

### Functionality
- [ ] Settings button opens settings panel
- [ ] Close button closes settings panel
- [ ] All menu items log clicks
- [ ] Bottom nav buttons log navigation
- [ ] Current screen highlight updates correctly

### Persistence
- [ ] PersistentUI survives scene changes
- [ ] Only one PersistentUI instance exists (singleton)
- [ ] Top Bar and Bottom Nav stay on screen during transitions

### Visual
- [ ] All icons load correctly
- [ ] All text renders properly (English + Japanese)
- [ ] Colors match design mockups
- [ ] Layout adapts to different screen sizes
- [ ] Buttons have hover/press feedback (if implemented)

### Performance
- [ ] No lag when opening/closing settings
- [ ] No memory leaks (check Profiler)
- [ ] Canvas rebuild count is low (check Frame Debugger)

---

## Phase 2 Prep

When you're ready for Phase 2 (accordion expand/collapse), you'll need:

### New Scripts
- `SettingsMenuItem.cs` - Individual menu item with expand/collapse logic
- `SettingsSubmenu.cs` - Container for expanded content (volume sliders, etc.)

### Animation
- Install DOTween: `Window > Package Manager > Add package by name > com.demigiant.dotween`
- Or use Unity's built-in Animation system

### Additional Prefabs
- SoundSettingsSubmenu (with Music Volume + SFX Volume sliders)
- UserProfileSubmenu (with account linking buttons - static for now)
- LanguageSelectionScreen (full screen with English/Japanese options)

### New References in SettingsController
```csharp
[Header("Submenus")]
public GameObject soundSettingsSubmenu;
public Slider musicVolumeSlider;
public Slider sfxVolumeSlider;
public GameObject userProfileSubmenu;
public GameObject languageSelectionScreen;
```

---

## Resources

### Unity Documentation
- [Canvas](https://docs.unity3d.com/Manual/UICanvas.html)
- [Button](https://docs.unity3d.com/Manual/script-Button.html)
- [Layout Groups](https://docs.unity3d.com/Manual/UILayoutGroup.html)
- [TextMeshPro](https://docs.unity3d.com/Manual/com.unity.textmeshpro.html)

### External Libraries (Optional)
- **DOTween:** Smooth animations - https://dotween.demigiant.com/
- **UniWebView:** In-app webviews - https://uniwebview.com/
- **Vuplex:** Cross-platform webviews - https://store.vuplex.com/

### Design Tools
- **Figma:** UI mockups - https://www.figma.com/
- **9-Slice Sprites:** https://docs.unity3d.com/Manual/9SliceSprites.html

---

## Support

If you run into issues:
1. Check Console for errors
2. Verify all inspector references are assigned
3. Review PREFAB_STRUCTURE.md for correct hierarchy
4. Test in isolation (create a test scene with just PersistentUI + SettingsCanvas)
5. Ask Kai for help! 😊
