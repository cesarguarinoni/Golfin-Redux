# Screen Deactivator for GOLFIN Redux

Automatically deactivates unnecessary screens before runtime to keep your scene clean and prevent unwanted UI elements from showing.

## Features

- ✅ **Automatic execution** before entering Play mode
- ✅ **Future-proof** - automatically detects new screens based on naming patterns
- ✅ **Easy configuration** via Inspector or Editor Window
- ✅ **Manual control** via menu command
- ✅ **Search filtering** by tag or parent object
- ✅ **Undo support**

## Quick Start

### 1. Open the Editor Window

Unity menu → **GOLFIN → Screen Deactivator Settings**

### 2. Create ScreenDeactivator (First Time Only)

If no ScreenDeactivator exists in your scene:
1. Click **"Create ScreenDeactivator"** button
2. A new GameObject will be created in your scene

### 3. Configure Active Screens

1. Click **"Refresh Screen List"** to scan all screens in the scene
2. Check the boxes next to screens that should be **active at startup**
3. For GOLFIN Redux, typically you'd check:
   - `LogoScreen` (if it's the first screen)
   - OR `LoadingScreen` (if you want to skip directly to loading)
4. Leave all other screens unchecked (HomeScreen, SettingsCanvas, etc.)

### 4. Apply Changes

Click **"Apply & Deactivate Now"** to apply the configuration

## How It Works

### Automatic Detection

The script automatically finds screens by looking for GameObjects with these keywords:
- "Screen" → LogoScreen, SplashScreen, LoadingScreen, HomeScreen
- "Panel" → SettingsPanel, NewsPanel, etc.
- "Canvas" → SettingsCanvas, HomeCanvas, etc.

### Execution Points

The deactivation runs automatically at:
1. **Before Play Mode** - Just before you hit Play in Unity Editor
2. **At Runtime (Awake)** - Safety check when the game starts

### Manual Trigger

You can also run it manually:
- **Menu**: GOLFIN → Deactivate Unnecessary Screens
- **Script**: Call `ProcessScreens()` on the ScreenDeactivator component

## Configuration

### Via Editor Window (Recommended)

Use **GOLFIN → Screen Deactivator Settings** for visual configuration with checkboxes.

### Via Inspector

Select the ScreenDeactivator GameObject and configure:

- **Active Screen Names**: Array of screen names that should remain active
  ```
  Element 0: LogoScreen
  Element 1: LoadingScreen
  ```

- **Search Root** (Optional): Drag a parent Transform to limit search scope
  - Leave empty to search entire scene
  - Set to your UI Canvas to only search UI elements

- **Screen Tag** (Optional): Filter by tag
  - Leave empty to search by name pattern
  - Set to "Screen" if you tag all your screen GameObjects

## Typical GOLFIN Redux Setup

### ShellScene.unity

For the main UI scene with Logo → Splash → Loading → Home flow:

**Active at Start:**
- `LogoScreen` (or whichever is your entry point)

**Deactivated:**
- `SplashScreen`
- `LoadingScreen`
- `HomeScreen`
- `SettingsCanvas`
- Any other panels/screens

**Why?**
Your screen flow controller (e.g., `ScreenManager`) activates each screen in sequence. Starting with everything deactivated prevents multiple screens from appearing simultaneously.

### GameplayScene.unity

If you have a separate gameplay scene:

**Active at Start:**
- `GameplayHUD` or `GameplayCanvas`

**Deactivated:**
- `PausePanel`
- `ResultScreen`
- `SettingsPanel`
- etc.

## Integration with Existing Code

The ScreenDeactivator works seamlessly with your existing screen management:

```csharp
// Your ScreenManager still controls screen flow
public class ScreenManager : MonoBehaviour
{
    void Start()
    {
        // ScreenDeactivator already ran - all screens are deactivated
        // Now activate the first screen normally
        ShowLogoScreen();
    }
    
    void ShowLogoScreen()
    {
        logoScreen.SetActive(true);
        // ... your logo animation
    }
}
```

No changes needed to your existing scripts!

## Future-Proofing

When you add new screens:
1. **No code changes needed** - just create the GameObject
2. If the name contains "Screen", "Panel", or "Canvas", it will be auto-detected
3. Open the Editor Window and click **"Refresh Screen List"**
4. Add it to the active list if needed
5. Apply changes

## Debug Logs

The script logs all actions to help you verify:

```
[ScreenDeactivator] Deactivated: SplashScreen
[ScreenDeactivator] Deactivated: HomeScreen
[ScreenDeactivator] Deactivated: SettingsCanvas
[ScreenDeactivator] Activated: LogoScreen
[ScreenDeactivator] Processed 4 screens: 1 active, 3 deactivated
[ScreenDeactivator] Pre-runtime screen deactivation complete
```

## Troubleshooting

### My screens aren't being detected

- Make sure they have "Screen", "Panel", or "Canvas" in the name
- OR tag them with a custom tag and set the `screenTag` field
- Click **"Refresh Screen List"** in the Editor Window
- Check the Console for log messages showing what was found

### Changes aren't persisting

- Make sure to **save your scene** after applying changes (Ctrl+S / Cmd+S)
- The ScreenDeactivator GameObject must not be destroyed on scene load

### Screens are still active in Play Mode

- Verify the screen names in `activeScreenNames` exactly match GameObject names
- Check the Debug logs to see what was processed
- Make sure the ScreenDeactivator GameObject is active in the scene

### Performance issues with large scenes

- Set a `searchRoot` to limit the search area (e.g., your main UI Canvas)
- Use tags to filter objects more efficiently

## File Structure

```
Assets/
├── Scripts/
│   └── UI/
│       ├── ScreenDeactivator.cs           (Main component)
│       └── Editor/
│           └── ScreenDeactivatorEditor.cs (Editor window)
└── Docs/
    └── SCREEN_DEACTIVATOR.md              (This file)
```

## Technical Details

- **Namespace**: `Golfin.UI`
- **Pattern**: Editor attribute with Play Mode state change detection
- **Search**: `FindObjectsOfType` with `includeInactive = true`
- **Editor Only**: The automatic detection only runs in Unity Editor, not in builds
- **Runtime Safety**: Still runs in Awake() as a safety check

## Created By

- **Feature Request**: Cesar Guarinoni
- **Implementation**: Kai (Aikenken Bot)
- **Date**: 2026-03-05
- **Project**: GOLFIN Redux

## See Also

- [AI_CONTEXT.md](../AI_CONTEXT.md) - Full project context
- [SettingsScreen/README.md](SettingsScreen/README.md) - Settings screen documentation
