# UI Auto-Wire Guide

**No more manual Inspector dragging!** Use `UIAutoWire` to automatically find and wire UI components by name.

---

## Quick Start

### 1. Follow Naming Conventions

Name your GameObjects descriptively in the hierarchy:

```
MyScreen
├── Background
└── Panel
    ├── Title
    ├── CloseButton
    └── ConfirmButton
```

### 2. Use UIAutoWire in Your Script

```csharp
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.Utilities;

public class MyScreen : MonoBehaviour
{
    // Optional public fields (can still override in Inspector if needed)
    public GameObject panel;
    public Button closeButton;
    public Button confirmButton;

    private void Awake()
    {
        // Auto-find by name
        panel = UIAutoWire.FindGameObject(transform, "Panel");
        closeButton = UIAutoWire.FindComponent<Button>(transform, "Panel/CloseButton");
        confirmButton = UIAutoWire.FindComponent<Button>(transform, "Panel/ConfirmButton");

        // Auto-wire button clicks
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);
    }

    private void OnCloseClicked()
    {
        panel.SetActive(false);
    }
}
```

### 3. Test - No Inspector Wiring Needed!

Just enter Play Mode. Components are found automatically.

---

## API Reference

### Find GameObject

```csharp
GameObject panel = UIAutoWire.FindGameObject(transform, "Panel");
GameObject nested = UIAutoWire.FindGameObject(transform, "Panel/Background");
```

### Find Component

```csharp
Button btn = UIAutoWire.FindComponent<Button>(transform, "Panel/CloseButton");
TMPro.TextMeshProUGUI text = UIAutoWire.FindComponent<TMPro.TextMeshProUGUI>(transform, "Panel/Title");
Image icon = UIAutoWire.FindComponent<Image>(transform, "Panel/Icon");
```

### Safe Find (No Warnings)

```csharp
if (UIAutoWire.TryFindComponent<Button>(transform, "Panel/CloseButton", out Button btn))
{
    // Found!
}
else
{
    // Not found, but no warning logged
}
```

### Wire Button Click

```csharp
// Auto-find and wire in one line
Button closeBtn = UIAutoWire.WireButton(transform, "Panel/CloseButton", OnCloseClicked);
```

### Wire Multiple Buttons

```csharp
UIAutoWire.WireButtons(transform, new Dictionary<string, UnityAction>
{
    { "Panel/CloseButton", OnClose },
    { "Panel/ConfirmButton", OnConfirm },
    { "Panel/CancelButton", OnCancel }
});
```

### Find All Children

```csharp
// Find all buttons under a container
Button[] buttons = UIAutoWire.FindComponents<Button>(transform, "ButtonContainer");
```

---

## Naming Conventions

### ✅ Good Names

```
Panel                    ← Clear purpose
Background              ← Clear purpose
CloseButton             ← Clear type + purpose
TitleText               ← Clear type + purpose
PlayerIcon              ← Clear purpose
SettingsPanel/Title     ← Clear hierarchy
```

### ❌ Bad Names

```
GameObject              ← Too generic
GameObject (1)          ← Unity default
Image                   ← Just the type
btn                     ← Too short
MyButton123             ← Unclear purpose
```

### Suffixes (Recommended)

- Buttons: `CloseButton`, `ConfirmButton`, `PlayButton`
- Panels: `SettingsPanel`, `InfoPanel`, `RewardPanel`
- Text: `TitleText`, `DescriptionText`, `ScoreText`
- Images: `PlayerIcon`, `BackgroundImage`, `RewardIcon`

---

## Comparison: Manual vs Auto-Wire

### Manual (Old Way) ❌

**In Unity Inspector:**
1. Create script
2. Add script to GameObject
3. Drag 20+ fields from Hierarchy → Inspector
4. Repeat for every prefab instance
5. If you rename a GameObject, references break
6. Re-drag everything again

**Time:** 5-10 minutes per screen

---

### Auto-Wire (New Way) ✅

**In Unity Inspector:**
1. Create script
2. Add script to GameObject
3. Done!

**Time:** 30 seconds per screen

**In Code:**
```csharp
private void Awake()
{
    // 3 lines = 20 fields wired
    panel = UIAutoWire.FindGameObject(transform, "Panel");
    closeButton = UIAutoWire.WireButton(transform, "Panel/CloseButton", OnClose);
    titleText = UIAutoWire.FindComponent<TMPro.TextMeshProUGUI>(transform, "Panel/Title");
}
```

---

## When to Use

### ✅ Use Auto-Wire For:
- **New UI screens** (future Gacha, Inventory, etc.)
- **Simple prefabs** with clear naming
- **Rapid prototyping**
- **Screens with many components** (10+ fields)

### ❌ Don't Use Auto-Wire For:
- **Already wired scripts** (Settings, Home, etc.) - leave them as-is!
- **Dynamic UI** (spawned at runtime)
- **Shared prefabs** (inconsistent naming)

---

## Example: Settings Screen (If We Were to Rebuild It)

**Current Settings (Manual - Don't Touch!):**
- 9 buttons manually dragged
- 9 icons manually dragged
- 9 labels manually dragged
- Total: 30+ Inspector assignments

**If We Used Auto-Wire (Hypothetical):**
```csharp
private void Awake()
{
    // Background & panel
    background = UIAutoWire.FindGameObject(transform, "Background");
    settingsPanel = UIAutoWire.FindGameObject(transform, "SettingsPanel");
    
    // Buttons
    UIAutoWire.WireButtons(transform, new Dictionary<string, UnityAction>
    {
        { "SettingsPanel/CloseButton", CloseSettings },
        { "SettingsPanel/UserProfileRow/Button", OnUserProfileClick },
        { "SettingsPanel/SoundSettingsRow/Button", OnSoundSettingsClick },
        { "SettingsPanel/LanguageRow/Button", OnLanguageClick },
        // etc...
    });
}
```

Total: ~15 lines vs 30+ Inspector drags!

---

## Migration Guide (For Future Screens)

### Step 1: Design with Good Names

When creating a new screen:
1. Name GameObjects clearly: `Panel`, `CloseButton`, `TitleText`
2. Use nested paths: `Panel/CloseButton` (not `CloseButton` at root)

### Step 2: Write Auto-Wire Code

```csharp
private void Awake()
{
    panel = UIAutoWire.FindGameObject(transform, "Panel");
    UIAutoWire.WireButton(transform, "Panel/CloseButton", OnClose);
}
```

### Step 3: Skip Inspector Wiring

Just enter Play Mode - components wire automatically!

---

## Benefits

✅ **Faster:** 10x faster than manual dragging  
✅ **Refactor-friendly:** Rename in code, not Inspector  
✅ **Version control:** Changes show in code diffs, not scene files  
✅ **Less error-prone:** Can't forget to drag a field  
✅ **Self-documenting:** Code shows what connects to what  

---

## FAQ

**Q: What if I rename a GameObject?**  
A: Just update the string in code. Much faster than re-dragging in Inspector.

**Q: Can I still manually override in Inspector?**  
A: Yes! Keep fields public and check `if (field == null)` before auto-wiring.

**Q: What if the GameObject doesn't exist?**  
A: UIAutoWire logs a warning and returns null. Your code should check for null.

**Q: Does this work with prefabs?**  
A: Yes! Works perfectly with prefabs. Changes apply to all instances.

**Q: Should I convert existing screens?**  
A: **No!** Leave existing screens (Settings, Home) as-is. Use auto-wire for **new** screens only.

---

## Files

- `Assets/Scripts/Utilities/UIAutoWire.cs` - The utility class
- `Assets/Scripts/UI/ExampleAutoWireScreen.cs` - Example usage (delete after learning)
- `Docs/UI_AUTO_WIRE_GUIDE.md` - This guide

---

**For new UI screens, use auto-wire. For existing screens, leave them alone!** 🚀
