# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-23) — Club Inventory Visual Fixes + System Improvements

The user has done significant manual fixes in Unity. The architect compared the updated screenshot against Figma and the Roster reference. Below are remaining fixes. Items removed by user are marked SKIP.

### Font Size Ratio: Figma ÷ 1.4 = Unity TMP size
```
Figma 66px → Unity 47px (EQUIP button)
Figma 51px → Unity 36px (screen titles)  
Figma 48px → Unity 34px (INFO header)
Figma 45px → Unity 32px (club name, rarity, level)
Figma 39px → Unity 28px (button text, RP counter)
Figma 33px → Unity 24px (stat names, stat values, body text)
Figma 30px → Unity 21px (tab labels)
Figma 20px → Unity 14px (filter bar labels)
```
**Add this ratio table to Docs/AI_CONTEXT.md under Design Decisions for future reference.**

### Figma Spacing Reference (Figma px → Unity px at ÷1.4 ratio)

These are the KEY spacing values from the Figma design. Use these to fix element positioning:
```
FULL SCREEN: 1170 × 2532 Figma → ~835 × 1808 Unity equivalent
Top UI (persistent bar): 313 Figma → 224 Unity (this is the TopBar already, 321px in scene)
Bottom nav: 263 Figma → 188 Unity (this is BottomNavBar, 196px in scene)

INSIDE CONTENT AREA (between top bar and bottom nav):
- Horizontal padding: 48 Figma → 34 Unity (each side)
- Tab bar to filter bar gap: 12 Figma → 9 Unity
- Filter bar to carousel gap: 12 Figma → 9 Unity  
- Carousel section height: 353 Figma → 252 Unity (cards are 343 Figma → 245 Unity)
- Pagination dots row: ~24 Figma → 17 Unity
- Carousel to detail panel gap: 24 Figma → 17 Unity
- Detail panel fills remaining height
- Detail panel to bottom nav: essentially flush (panel stretches to fill)

INSIDE DETAIL PANEL:
- Internal padding: 24 Figma → 17 Unity (all sides)
- Left panel width: ~46% of detail panel
- Right panel width: ~54% of detail panel
- Gap between left and right panels: border line (1px white)
- Vertical gap between sections in right panel: 24 Figma → 17 Unity
- Gap between stat rows: 24 Figma → 17 Unity
- Stat icon width: 55 Figma → 39 Unity
- Stat icon height: 45 Figma → 32 Unity
- Stat bar height: 20 Figma → 14 Unity
- Stat bar gap from name: 5 Figma → 4 Unity
- Stat value column width: 115 Figma → 82 Unity
```

---

### Fix 1: Screenshot Tool — Auto-compress to JPG

Update `Assets/Scripts/Editor/ScreenshotTool.cs`. After capturing, auto-resize and save as JPG so the file stays under 1MB without needing the Python compress script:

```csharp
[MenuItem("GOLFIN/Screenshot/Capture Game View")]
public static void CaptureGameView()
{
    if (!Directory.Exists(ScreenshotDir))
        Directory.CreateDirectory(ScreenshotDir);

    string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    string pngPath = $"{ScreenshotDir}/screenshot_{timestamp}.png";

    ScreenCapture.CaptureScreenshot(pngPath);
    Debug.Log($"[ScreenshotTool] Captured to {pngPath}");

    // Schedule compression after Unity writes the file
    EditorApplication.delayCall += () =>
    {
        EditorApplication.delayCall += () =>
        {
            CompressToJpg(pngPath, 800);
            AssetDatabase.Refresh();
        };
    };
}

private static void CompressToJpg(string pngPath, int maxWidth)
{
    if (!File.Exists(pngPath)) return;

    var bytes = File.ReadAllBytes(pngPath);
    var tex = new Texture2D(2, 2);
    tex.LoadImage(bytes);

    int newW = tex.width > maxWidth ? maxWidth : tex.width;
    float ratio = (float)newW / tex.width;
    int newH = Mathf.RoundToInt(tex.height * ratio);

    var rt = RenderTexture.GetTemporary(newW, newH);
    Graphics.Blit(tex, rt);
    var resized = new Texture2D(newW, newH, TextureFormat.RGB24, false);
    RenderTexture.active = rt;
    resized.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
    resized.Apply();
    RenderTexture.active = null;
    RenderTexture.ReleaseTemporary(rt);

    var jpg = resized.EncodeToJPG(75);
    string jpgPath = pngPath.Replace(".png", ".jpg");
    File.WriteAllBytes(jpgPath, jpg);

    // Clean up the large PNG
    File.Delete(pngPath);
    // Also delete the .meta file Unity creates for the PNG
    string metaPath = pngPath + ".meta";
    if (File.Exists(metaPath)) File.Delete(metaPath);

    Object.DestroyImmediate(tex);
    Object.DestroyImmediate(resized);
    Debug.Log($"[ScreenshotTool] Compressed to {jpgPath} ({jpg.Length / 1024}KB)");
}
```

---

### Fix 2: SKIP — "INVENTORY" header removal
User already removed it manually. For future screens: the screen title goes in the PersistentUI top bar username area, not as a separate header row in the screen content.

---

### Fix 3: EQUIP Button 24px from Bottom

Add a flexible spacer before the EQUIP button in the right panel's VerticalLayoutGroup. This pushes EQUIP to the bottom:

```csharp
// In ClubDetailPanelBuilder or manually:
// Before the EQUIP button, add a spacer with flexibleHeight
var spacer = new GameObject("EquipSpacer");
spacer.transform.SetParent(rightPanel.transform);
// Set sibling index to just before EQUIP button
spacer.transform.SetSiblingIndex(equipButton.transform.GetSiblingIndex());
var le = spacer.AddComponent<LayoutElement>();
le.flexibleHeight = 1; // Eats all remaining space
```

Then on the right panel's VerticalLayoutGroup, set `padding.bottom = 24`.

---

### Fix 4: TextGradients Utility — Reusable Gold/Silver Gradient

Create `Assets/Scripts/Utilities/TextGradients.cs`:

```csharp
using TMPro;
using UnityEngine;

namespace Golfin.Utilities
{
    /// <summary>
    /// Reusable TMP vertex gradients for gold/silver text styling.
    /// Used across tabs, filters, settings, and any UI with gradient text.
    /// </summary>
    public static class TextGradients
    {
        // Silver: top #FFFFFF → bottom #818EA1
        public static readonly VertexGradient Silver = new VertexGradient(
            new Color32(255, 255, 255, 255),   // top-left
            new Color32(255, 255, 255, 255),   // top-right
            new Color32(129, 142, 161, 255),   // bottom-left
            new Color32(129, 142, 161, 255)    // bottom-right
        );

        // Gold: top #FCF195 → bottom #BB7F1D
        public static readonly VertexGradient Gold = new VertexGradient(
            new Color32(252, 241, 149, 255),
            new Color32(252, 241, 149, 255),
            new Color32(187, 127, 29, 255),
            new Color32(187, 127, 29, 255)
        );

        public static void ApplySilver(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.enableVertexGradient = true;
            text.colorGradient = Silver;
        }

        public static void ApplyGold(TextMeshProUGUI text)
        {
            if (text == null) return;
            text.enableVertexGradient = true;
            text.colorGradient = Gold;
        }

        public static void ApplyFlat(TextMeshProUGUI text, Color color)
        {
            if (text == null) return;
            text.enableVertexGradient = false;
            text.color = color;
        }
    }
}
```

Then update these scripts to use it:

**InventoryScreenController.RefreshTabVisuals():**
- Active tab: `TextGradients.ApplyGold(label)`
- Inactive tabs: `TextGradients.ApplySilver(label)`
- REMOVE the underline indicator approach — the active state is ONLY the gold/silver text color change

**ClubFilterBar.UpdateHighlights():**
- Active filter: `TextGradients.ApplyGold(label)`
- Inactive filters: `TextGradients.ApplySilver(label)`
- REMOVE any Image color tinting on filter buttons

Add `using Golfin.Utilities;` to both files.

---

### Fix 5: Filter Bar Dividers

Add thin vertical divider Images between filter buttons inside the HorizontalLayoutGroup. Each divider:
- Width: 1px (use LayoutElement preferredWidth = 1, flexibleWidth = 0)
- Height: match filter bar inner height (~24px, use LayoutElement preferredHeight = 24)
- Color: rgba(255, 255, 255, 0.3)
- Image component with raycastTarget = false

Add these in `ClubFilterBar.Start()` or in the builder, between each pair of filter buttons. They should work inside HLG without breaking sizing because they have fixed dimensions via LayoutElement.

If this still breaks the HLG, alternative: just skip dividers on the filter bar. The Figma shows them but they're subtle enough that the UI works without them.

---

### Fix 6: Content Area 12px from Tab Bar

Set the spacing between the tab bar and the content area to 12px. If the parent uses a VerticalLayoutGroup, set `spacing = 12`. If manual, adjust the content area's `offsetMax.y` to start 12px below the tab bar.

Also set 12px gap between filter bar and carousel.

---

### Fix 7: Portrait Names — Two Lines

In `ClubThumbnailCard.Initialize()`, display club name on 2 lines — type on top, brand on bottom:

```csharp
if (nameText != null)
{
    // Parse: "Iron 7 Mireo" → type "IRON 7", brand "MIREO"
    // Or: "Driver G&F" → type "DRIVER", brand "G&F"  
    string fullName = template.name; // e.g., "Iron 7 Mireo"
    string brand = template.brand;   // e.g., "MireO"
    
    // Remove the brand from the full name to get the type portion
    string typePart = fullName;
    if (!string.IsNullOrEmpty(brand))
    {
        int brandIndex = fullName.IndexOf(brand, System.StringComparison.OrdinalIgnoreCase);
        if (brandIndex >= 0)
            typePart = fullName.Substring(0, brandIndex).Trim();
    }
    
    nameText.text = $"{typePart.ToUpper()}\n{brand.ToUpper()}";
}
```

---

### Fix 8: RuntimeActiveStateManager

Create `Assets/Scripts/Utilities/RuntimeActiveStateManager.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Ensures correct initial active states at runtime.
/// Catches Inspector mistakes (objects left on/off during editing).
/// Script Execution Order: -300 (before everything).
/// </summary>
public class RuntimeActiveStateManager : MonoBehaviour
{
    [Header("Force Active at Runtime Start")]
    [SerializeField] private GameObject[] forceActive;

    [Header("Force Inactive at Runtime Start")]
    [SerializeField] private GameObject[] forceInactive;

    private void Awake()
    {
        if (forceActive != null)
            foreach (var go in forceActive)
                if (go != null) go.SetActive(true);

        if (forceInactive != null)
            foreach (var go in forceInactive)
                if (go != null) go.SetActive(false);
    }
}
```

Add to Script Execution Order at -300. Attach to a root-level manager object in the scene.

---

### Fix 9: Carousel Arrows + Dots — Reuse from Roster

The Roster carousel already has working arrow buttons and pagination dots. Don't create new sprites — reuse the exact same ones.

1. Find what sprites/prefabs the Roster `CarouselController` uses for `leftArrowButton`, `rightArrowButton`, and `paginationDotPrefab`
2. Wire the same sprites/prefabs to `ClubCarouselController`'s equivalent fields
3. If the arrow buttons don't exist in the Club Inventory hierarchy yet, create them with the same structure as the Roster arrows:
   - Left and right of the carousel scroll view
   - Image component with the arrow sprite
   - Button component
   - Position: vertically centered on the carousel, horizontally at the edges

---

### Fix 10: Fix Font Sizes — Verify All TMP in Club Detail Panel

Read every TMP component in the ClubDetailPanel hierarchy. Cross-check against this table and fix any mismatches:

| Element | Target Unity size | Font weight |
|---|---|---|
| Club name ("DRIVER G&F") | 32 | SemiBold |
| Rarity ("COMMON") | 32 | SemiBold |
| Level ("Lv 10") | 32 | SemiBold |
| Level max ("/39") | 24 | Regular |
| Stat names ("POWER") | 24 | Medium |
| Stat values ("80/100") | 24 | Bold |
| Durability values ("100/100") | 24 | Bold + 14 for "/100" |
| Distance ("250 yd") | 24 | Bold |
| Button text ("LEVEL UP", "REPAIR") | 28 | SemiBold |
| COMPARE button | 28 | SemiBold |
| EQUIP button | 47 | SemiBold |
| INFO header | 34 | SemiBold |
| INFO body text | 24 | Regular |

Also check the Roster screen has matching sizes for equivalent elements (stat names, stat values, buttons).

---

### Fix 11: Element Spacing and Positioning

These are the key spacing values that need to match the Figma design. Check and fix each:

**Carousel area:**
- Carousel card area should be ~245px tall (Unity)
- Cards should have ~6px horizontal gap between them
- Carousel to detail panel gap: ~17px
- Pagination dots: 17px below cards, centered

**Detail panel:**
- Internal padding: 17px on all sides
- Left panel: ~46% width, right panel: ~54% width
- Vertical gap between right panel sections: 17px
- Right panel padding bottom: 24px (for EQUIP spacing)

**Stat rows:**
- Each stat row: icon (39w × 32h) + gap 12px + name+bar column (flexible) + gap 12px + value column (82px wide)
- Gap between stat rows: 17px
- Stat bar track height: 14px
- Gap between stat name and bar: 4px

**Buttons:**
- LEVEL UP + REPAIR: side by side, each ~50% width minus gaps
- COMPARE: full width
- EQUIP: full width, 24px above panel bottom edge
- Button height: ~38px for regular, ~85px for EQUIP

---

### Fix 12: SKIP — Club image data binding
User confirmed images are now correct.

---

### Reminders
- Font ratio: Figma ÷ 1.4 = Unity TMP size
- Platform: Windows (PowerShell, no bash/chmod/sed)
- Use `== null` not `??` for Unity objects
- All new text uses `LocalizationManager.Get("KEY")`
- Verify `using` directives before committing (Rule 0 in CLAUDE.md)
- Push to GitHub after completing
- For future screens: title goes in PersistentUI top bar, not as separate header
- For future image needs: reuse existing project assets, don't make the user manually add sprites that are already in the project

---

## Completed Tasks

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update, root cleanup
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — Visual fixes: ScreenshotTool auto-JPG, TextGradients utility, tab/filter gold-silver gradients, filter bar dividers, 2-line club card names, RuntimeActiveStateManager, ClubInventoryPatcher (Fixes 3/6/9/10/11), font ratio table in AI_CONTEXT.md
