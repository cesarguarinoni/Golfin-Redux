# Settings Screen - Complete Build Guide (Phase 1)

You have the YAML hierarchy and scripts. Now let's add all the UI components and wire everything up!

---

## Current State

✅ Scripts: PersistentUIManager.cs, SettingsController.cs  
✅ YAML Prefab: SettingsCanvas.prefab (empty GameObjects)  
❌ No UI components added yet  
❌ Not wired up  
❌ Top/Bottom bars not visible  

---

## Part 1: Setup PersistentUI (Top & Bottom Bars)

The Top Bar and Bottom Nav should be **persistent** across all screens, including Settings.

### 1.1 Create PersistentUI Prefab

**In Scene Hierarchy:**
1. Right-click → **UI → Canvas** → Rename to `PersistentUI`
2. Set Canvas properties:
   - Render Mode: Screen Space - Overlay
   - Sort Order: 0

3. **Add PersistentUIManager Script:**
   - Select `PersistentUI`
   - Add Component → **Persistent UI Manager**

### 1.2 Create Top Bar

**Under PersistentUI:**
1. Right-click PersistentUI → **UI → Panel** → Rename to `TopBar`
2. Anchor to top: 
   - Anchor: Top-stretch
   - Height: 100px
3. Add components to TopBar children:

**RewardPointsIcon:**
- Create: UI → Image → Rename to `RewardPointsIcon`
- Position: Top-left
- Size: 40x40

**RewardPointsText:**
- Create: UI → Text - TextMeshPro → Rename to `RewardPointsText`
- Position: Next to icon
- Text: "0"

**SettingsButton:**
- Create: UI → Button → Rename to `SettingsButton`
- Position: Top-right
- Size: 50x50
- Add Icon child (Image)

**UsernameText:**
- Create: UI → Text - TextMeshPro → Rename to `UsernameText`
- Position: Top-center
- Text: "Player"

### 1.3 Create Bottom Navigation Bar

**Under PersistentUI:**
1. Right-click PersistentUI → **UI → Panel** → Rename to `BottomNavigationBar`
2. Anchor to bottom:
   - Anchor: Bottom-stretch
   - Height: 100px

3. Create 5 buttons (each with an icon Image child + Highlight Image child):
   - `HomeButton` (with Highlight child - disabled by default)
   - `GachaButton` (with Highlight)
   - `MainPlayButton` (with Highlight)
   - `InventoryButton` (with Highlight)
   - `CharactersButton` (with Highlight)

4. Position evenly across bottom (use Horizontal Layout Group or manual positioning)

### 1.4 Wire Up PersistentUIManager

**Select PersistentUI GameObject:**

**Top Bar section:**
- Drag `TopBar` → Top Bar Panel
- Drag `RewardPointsIcon` → Reward Points Icon
- Drag `RewardPointsText` → Reward Points Text
- Drag `SettingsButton` → Settings Button
- Drag `UsernameText` → Username Text

**Bottom Navigation Bar section:**
- Drag `BottomNavigationBar` → Bottom Nav Panel
- Drag each button → corresponding field
- Drag each button's icon → corresponding icon field
- Drag each button's Highlight → corresponding highlight field

### 1.5 Save as Prefab

1. Drag `PersistentUI` from Hierarchy into `Assets/Prefabs/UI/`
2. Name it `PersistentUI.prefab`

---

## Part 2: Build Settings Screen

### 2.1 Open SettingsCanvas Prefab

1. In Project window, double-click `Assets/Prefabs/UI/SettingsCanvas.prefab` (or SettingsScreen if you renamed it)
2. This opens Prefab Edit Mode

### 2.2 Add Canvas Component

**Select SettingsCanvas root GameObject:**
1. Add Component → **Canvas**
2. Render Mode: Screen Space - Overlay
3. Sort Order: 100 (above PersistentUI)
4. Add Component → **Canvas Scaler**
5. Add Component → **Graphic Raycaster**
6. Add Component → **Settings Controller** script

### 2.3 Add Semi-Transparent Background

**Select Background GameObject (child of SettingsCanvas):**
1. Add Component → **Image**
2. Color: Black with 50% alpha (rgba: 0, 0, 0, 128)
3. Raycast Target: ✅ (blocks clicks below)
4. Anchor: Stretch (covers full screen)

### 2.4 Build Settings Panel

**Select SettingsPanel GameObject:**
1. Add Component → **Image** (panel background)
2. Color: Dark gray or use a sprite
3. Size: 800x1200 (adjust for your design)
4. Position: Center of screen

### 2.5 Build Settings List

**Select SettingsList GameObject:**
1. Add Component → **Vertical Layout Group**
2. Settings:
   - Spacing: 10
   - Child Alignment: Upper Center
   - Child Force Expand: Width ✅, Height ❌
   - Child Control Size: Width ✅, Height ✅

### 2.6 Add Components to Each Menu Row

For **each row** (UserProfileRow, SoundSettingsRow, etc.):

1. Add Component → **Button**
2. Add Component → **Image** (row background)
3. Add Component → **Horizontal Layout Group**
   - Spacing: 20
   - Child Alignment: Middle Left
   - Padding: Left 20, Right 20

**For each row's children:**

**LeftIcon:**
- Add Component → **Image**
- Size: 40x40
- Assign icon sprite (gear, sound, language, etc.)

**Label:**
- Add Component → **Text - TextMeshPro**
- Text: "User Profile" (or appropriate label)
- Font Size: 24-28
- Alignment: Left, Center

**RightArrow:**
- Add Component → **Image**
- Size: 24x24
- Assign arrow sprite (→)
- Rotation: 0° (will rotate on expand later)

**Note:** LogOutRow doesn't have a RightArrow (no expansion)

### 2.7 Add Close Button

**Select CloseButton GameObject:**
1. Add Component → **Button**
2. Add Component → **Image** (button background)
3. Position: Bottom-center of SettingsPanel
4. Size: 250x60

**Select CloseButton → Label child:**
- Add Component → **Text - TextMeshPro**
- Text: "CLOSE"
- Font Size: 24
- Alignment: Center

### 2.8 Wire Up SettingsController

**Select SettingsCanvas root GameObject:**

**Settings Panel section:**
- Drag `SettingsPanel` → Settings Panel
- Drag `CloseButton` → Close Button

**Settings Menu Items section:**
- Drag each row button → corresponding button field
  - UserProfileRow → User Profile Button
  - SoundSettingsRow → Sound Settings Button
  - (etc.)

**Menu Item Icons section:**
- Drag each LeftIcon → corresponding icon field

**Menu Item Labels section:**
- Drag each Label → corresponding label field

**Menu Item Right Arrows section:**
- Drag each RightArrow → corresponding arrow field
- (LogOut has no arrow)

### 2.9 Save Prefab

1. Click "Save" in Prefab Edit Mode toolbar
2. Exit Prefab Edit Mode (click `<` arrow or Scene tab)

---

## Part 3: Integrate into Scene

### 3.1 Add PersistentUI to Scene

1. Open your main scene (e.g., `ShellScene`)
2. Drag `PersistentUI.prefab` into Hierarchy
3. Make sure it's at root level (not nested under another GameObject)

### 3.2 Add SettingsScreen to Scene

1. Drag `SettingsCanvas.prefab` (or SettingsScreen) into Hierarchy
2. Make sure it's BELOW PersistentUI in hierarchy (so it renders on top)
3. By default, SettingsPanel should be inactive (SetActive = false)

### 3.3 Wire PersistentUI → SettingsController

**Select PersistentUI GameObject:**

In PersistentUIManager:
- Find SettingsController in the scene
- Drag SettingsCanvas (or SettingsScreen) GameObject into any field that needs it

Actually, the scripts should auto-find each other via singleton pattern.

### 3.4 Test

**Enter Play Mode:**
1. Click Settings button in Top Bar → Settings panel should open
2. Click Close button → Settings panel should close
3. Click any menu item → Check Console for click logs
4. Bottom Nav should still be visible (not covered by Settings)

---

## Part 4: Styling Tips

### Colors
- **Settings Panel Background:** Dark semi-transparent (rgba: 30, 30, 30, 240)
- **Row Background:** Lighter gray when hovered
- **Text:** White or light gray
- **Close Button:** Accent color (blue, green, etc.)

### Fonts
- Use consistent font across all labels
- Settings title/labels: 24-28pt
- Button text: 22-26pt

### Icons
- Size: 40x40 for left icons, 24x24 for arrows
- Style: Consistent icon set (line icons, filled icons, etc.)
- Color: Tint to match theme

### Spacing
- Row spacing: 10-15px
- Row height: 80-90px
- Panel padding: 40px all sides
- Close button margin: 30px from bottom

---

## Troubleshooting

### Settings button doesn't work
- Check SettingsController.Instance is not null
- Check settingsPanel reference is assigned
- Check settingsPanel.SetActive(true) is being called

### Settings panel doesn't close
- Check Close button has Button component
- Check Close button onClick is wired to SettingsController.CloseSettings

### Top Bar not visible in Settings
- Check PersistentUI is in the scene
- Check PersistentUI Canvas Sort Order is 0 (below Settings)
- Check TopBar is active

### Bottom Nav not visible in Settings
- Check PersistentUI BottomNavigationBar is active
- Check Settings Background doesn't cover it (reduce height if needed)
- Or: Make Settings Panel smaller so Bottom Nav shows below it

### Menu items don't log clicks
- Check each row has Button component
- Check SettingsController has button references assigned
- Check Console for errors

---

## Next Steps (Phase 2)

After Phase 1 is complete and working:
- Add expand/collapse behavior (accordion)
- Add Sound Settings submenu (sliders)
- Add Language selection submenu
- Smooth animations (arrow rotation, panel expand)

---

## Time Estimate

- Part 1 (PersistentUI): 15-20 min
- Part 2 (Settings Screen): 20-25 min
- Part 3 (Integration): 5-10 min
- Part 4 (Styling): 10-15 min

**Total: ~50-70 minutes** for a complete, styled Settings Screen Phase 1.

---

**Take your time, and let me know when you're done or if you hit any issues!** 🚀
