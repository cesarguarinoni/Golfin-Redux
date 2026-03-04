# GOLFIN Settings Screen - Architecture Overview

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                         GAME SCENES                         │
│  (Home, Gacha, Main Play, Inventory, Characters, etc.)     │
└─────────────────────────────────────────────────────────────┘
                              ▲
                              │
                    Scene Navigation
                              │
┌─────────────────────────────▼───────────────────────────────┐
│                     PersistentUI                            │
│                   (DontDestroyOnLoad)                       │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  Top Bar                                            │   │
│  │  • Reward Points                                    │   │
│  │  • Settings Button ────────┐                       │   │
│  │  • Username                │                       │   │
│  └────────────────────────────┼───────────────────────┘   │
│                               │                            │
│  ┌────────────────────────────┼───────────────────────┐   │
│  │  Bottom Navigation Bar     │                       │   │
│  │  • Home                    │                       │   │
│  │  • Gacha                   │                       │   │
│  │  • Main Play               │                       │   │
│  │  • Inventory               │                       │   │
│  │  • Characters              │                       │   │
│  │  (Highlight current screen)│                       │   │
│  └────────────────────────────┼───────────────────────┘   │
│                               │                            │
│     PersistentUIManager       │                            │
│     (Singleton Pattern)       │                            │
└───────────────────────────────┼────────────────────────────┘
                                │
                     Opens Settings
                                │
                                ▼
┌─────────────────────────────────────────────────────────────┐
│                     SettingsCanvas                          │
│                   (Overlay, Sort Order: 100)                │
│  ┌─────────────────────────────────────────────────────┐   │
│  │  Semi-transparent Backdrop                          │   │
│  │  (Blocks interaction with screens below)            │   │
│  │                                                      │   │
│  │  ┌──────────────────────────────────────────────┐  │   │
│  │  │  Settings Panel                              │  │   │
│  │  │                                              │  │   │
│  │  │  ┌────────────────────────────────────────┐ │  │   │
│  │  │  │ User Profile         [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ Sound Settings       [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ Language             [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ Terms of Use         [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ Privacy Policy       [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ FAQ                  [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ About                [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ Contact              [>]               │ │  │   │
│  │  │  ├────────────────────────────────────────┤ │  │   │
│  │  │  │ Log Out                                │ │  │   │
│  │  │  └────────────────────────────────────────┘ │  │   │
│  │  │                                              │  │   │
│  │  │  [       CLOSE       ]                       │  │   │
│  │  └──────────────────────────────────────────────┘  │   │
│  │                                                      │   │
│  └─────────────────────────────────────────────────────┘   │
│                                                             │
│     SettingsController                                      │
│     (Singleton Pattern)                                     │
└─────────────────────────────────────────────────────────────┘
```

---

## Component Relationships

```
┌─────────────────────────┐
│  PersistentUIManager    │
│                         │
│  - Manages Top Bar      │
│  - Manages Bottom Nav   │
│  - Persists across      │
│    scene transitions    │
│  - Handles navigation   │
│  - Opens Settings ──────┼─────────┐
└─────────────────────────┘         │
                                    │
                                    ▼
                      ┌──────────────────────────┐
                      │  SettingsController      │
                      │                          │
                      │  - Opens/closes panel    │
                      │  - Handles menu clicks   │
                      │  - Manages submenus      │
                      │    (Phase 2)             │
                      └──────────────────────────┘
```

---

## Data Flow

### Opening Settings
```
User clicks Settings Button
        │
        ▼
PersistentUIManager.OnSettingsButtonClick()
        │
        ▼
SettingsController.Instance.OpenSettings()
        │
        ▼
settingsPanel.SetActive(true)
        │
        ▼
Settings Panel appears over current screen
```

### Closing Settings
```
User clicks Close Button
        │
        ▼
SettingsController.CloseSettings()
        │
        ▼
settingsPanel.SetActive(false)
        │
        ▼
Settings Panel disappears, returns to previous screen
```

### Navigation (Bottom Nav)
```
User clicks Bottom Nav button (e.g., Inventory)
        │
        ▼
PersistentUIManager.NavigateTo(Screen.Inventory)
        │
        ▼
UpdateScreenHighlight() - highlights active button
        │
        ▼
Load Inventory Scene or activate Inventory Panel
        │
        ▼
PersistentUI stays visible (DontDestroyOnLoad)
```

---

## Scene Lifecycle

### Initial Load (Home Screen)
```
1. Load Home Scene
2. Instantiate PersistentUI prefab
   └─ PersistentUIManager.Awake()
      ├─ Check for existing instance
      ├─ If none, set Instance = this
      ├─ DontDestroyOnLoad(gameObject)
      └─ Initialize buttons
3. Instantiate SettingsCanvas prefab
   └─ SettingsController.Awake()
      ├─ Check for existing instance
      ├─ If none, set Instance = this
      └─ Initialize buttons
   └─ SettingsController.Start()
      └─ settingsPanel.SetActive(false)
```

### Scene Transition (Home → Gacha)
```
1. User clicks Gacha button in Bottom Nav
2. PersistentUIManager.NavigateTo(Screen.Gacha)
3. Load Gacha Scene
   ├─ PersistentUI stays (DontDestroyOnLoad)
   ├─ SettingsCanvas stays (if exists)
   └─ Previous scene content is destroyed
4. Gacha scene content loads
5. Update Bottom Nav highlight to Gacha
```

### Opening Settings (From Any Screen)
```
1. User clicks Settings button
2. PersistentUIManager.OnSettingsButtonClick()
3. SettingsController.Instance.OpenSettings()
4. settingsPanel.SetActive(true)
   ├─ Backdrop blocks interaction with screen below
   └─ Menu items are clickable
```

---

## Memory Management

### Persistent Objects (Never Destroyed)
- **PersistentUI:** Lives for the entire game session
  - Contains Top Bar + Bottom Nav
  - Updated in real-time (reward points, username, etc.)
  - Only one instance exists (singleton)

- **SettingsCanvas:** Lives for the entire game session (after first open)
  - Overlays on top of any screen
  - Just show/hide with SetActive()
  - Only one instance exists (singleton)

### Scene-Specific Objects (Destroyed on Scene Change)
- Home screen content (buttons, panels, animations, etc.)
- Gacha screen content
- Inventory screen content
- Characters screen content
- Main Play screen content

### Optimization Tips
1. **Don't Instantiate/Destroy Repeatedly:**
   - PersistentUI: Created once at game start
   - SettingsCanvas: Created once on first Settings open, then reused

2. **Lazy Loading:**
   - SettingsCanvas can be loaded on-demand (first Settings button click)
   - Reduces memory footprint at game start

3. **Disable Raycast Target:**
   - Uncheck on all non-interactive Images/Texts
   - Reduces UI raycast overhead

---

## Extensibility

### Adding New Screens
To add a new screen (e.g., Shop):
1. Add `shopButton` to PersistentUIManager
2. Add `Screen.Shop` to the enum
3. Wire up button click: `shopButton.onClick.AddListener(() => NavigateTo(Screen.Shop))`
4. Handle navigation in `NavigateTo()` switch case

### Adding New Settings Menu Items
To add a new menu item (e.g., Notifications):
1. Add `notificationsButton` to SettingsController
2. Create NotificationsRow in SettingsPanel hierarchy
3. Wire up button in SettingsController inspector
4. Add click handler: `OnNotificationsClick()`

### Phase 2 Expansion (Accordion)
Each menu item will have:
- Static row (Button + Icon + Label + Arrow)
- Submenu container (initially hidden)
- Expand/collapse animation (DOTween or Animator)
- Only one submenu open at a time (accordion pattern)

Example structure:
```
UserProfileRow (Button)
├── LeftIcon
├── Label
├── RightArrow (rotates when expanded)
└── Submenu (hidden by default)
    ├── EditNameButton
    ├── LinkAccountButton
    └── ChangeAvatarButton
```

---

## Error Handling

### Singleton Conflicts
If multiple instances are created:
```csharp
if (Instance != null && Instance != this)
{
    Destroy(gameObject);
    return;
}
```
This prevents duplicate PersistentUI or SettingsCanvas.

### Missing References
All public references are checked before use:
```csharp
if (settingsButton != null)
{
    settingsButton.onClick.AddListener(OnSettingsButtonClick);
}
```
This prevents NullReferenceException if a reference isn't assigned.

### Scene Navigation
If a scene fails to load, the PersistentUI stays intact and the user can navigate to another screen.

---

## Testing Strategy

### Unit Tests
- Test PersistentUIManager singleton behavior
- Test SettingsController open/close logic
- Test navigation state updates

### Integration Tests
- Test scene transitions with PersistentUI persistence
- Test Settings panel opening from different screens
- Test Bottom Nav navigation flow

### Manual Tests
- Click all buttons and verify logs
- Transition between all screens and verify PersistentUI stays
- Open Settings from each screen and verify panel appears
- Close Settings and verify return to previous screen
- Check memory usage in Profiler during scene transitions

---

## Future Enhancements

### Phase 2
- Accordion expand/collapse animations
- Volume sliders (Music + SFX)
- Language selection screen
- Disable backdrop click to close

### Phase 3
- Webview integration (Terms, Privacy, FAQ, Contact)
- User profile editing
- Log out confirmation modal
- About screen (app version, licenses)

### Phase 4 (Optional)
- Settings search/filter
- Settings categories (Account, Audio, Display, etc.)
- Advanced settings (notifications, data sync, etc.)
- Settings presets (profiles)

---

## Performance Considerations

### Canvas Optimization
- Use Canvas Scaler for consistent UI across devices
- Batch UI updates to avoid multiple canvas rebuilds
- Disable Raycast Target on non-interactive elements

### Memory
- PersistentUI: ~5-10 MB (depends on icon sizes)
- SettingsCanvas: ~2-5 MB (depends on complexity)
- Total overhead: ~10-15 MB (acceptable for mobile)

### Frame Rate
- Opening/closing Settings: <16ms (60 FPS)
- Scene transitions: <33ms (30 FPS acceptable during load)
- UI interactions: <8ms (120 FPS for smooth feel)

---

## Conclusion

This architecture provides:
✅ Clean separation of concerns (Persistent UI vs Scene content)
✅ Singleton pattern for global access
✅ Memory efficiency (reuse instead of recreate)
✅ Extensible design (easy to add screens/settings)
✅ Consistent UX (persistent Top Bar + Bottom Nav)

Ready to implement Phase 2! 🚀
