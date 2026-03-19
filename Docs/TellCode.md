# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-20)

### 1. Create Screenshot Tool

Create an Editor script `Assets/Scripts/Editor/ScreenshotTool.cs`:

```csharp
using UnityEngine;
using UnityEditor;
using System.IO;

public static class ScreenshotTool
{
    private static readonly string ScreenshotDir = "Assets/Screenshots";

    [MenuItem("GOLFIN/Screenshot/Capture Game View")]
    public static void CaptureGameView()
    {
        if (!Directory.Exists(ScreenshotDir))
            Directory.CreateDirectory(ScreenshotDir);

        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        string path = $"{ScreenshotDir}/screenshot_{timestamp}.png";

        ScreenCapture.CaptureScreenshot(path);
        Debug.Log($"[ScreenshotTool] Saved to {path}");

        EditorApplication.delayCall += () => AssetDatabase.Refresh();
    }

    [MenuItem("GOLFIN/Screenshot/Capture Named")]
    public static void CaptureNamed()
    {
        if (!Directory.Exists(ScreenshotDir))
            Directory.CreateDirectory(ScreenshotDir);

        string path = EditorUtility.SaveFilePanel("Save Screenshot", ScreenshotDir, "screenshot", "png");
        if (!string.IsNullOrEmpty(path))
        {
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[ScreenshotTool] Saved to {path}");
            EditorApplication.delayCall += () => AssetDatabase.Refresh();
        }
    }
}
```

### 2. Create Image Compression Script

Create `Docs/compress_screenshots.ps1`:

```powershell
# Compresses all .png files in a folder to max 800px wide
# Saves to a _compressed subfolder
# Usage: powershell -File Docs/compress_screenshots.ps1 "Assets/Screenshots"
# Also run on references: "Assets/References/Roster Screen", "Assets/References/Inventory"
# Requires: pip install Pillow

param([string]$folder)

python -c @"
import os, sys
from PIL import Image

folder = sys.argv[1]
out = os.path.join(folder, '_compressed')
os.makedirs(out, exist_ok=True)

for f in os.listdir(folder):
    if f.lower().endswith('.png') and not f.endswith('.meta'):
        img = Image.open(os.path.join(folder, f))
        ratio = 800 / max(img.size)
        if ratio < 1:
            img = img.resize((int(img.size[0]*ratio), int(img.size[1]*ratio)), Image.LANCZOS)
        img.save(os.path.join(out, f), optimize=True)
        print(f'Compressed {f}')
"@ $folder
```

Run `pip install Pillow` first if not installed. Then compress existing references:
```powershell
pip install Pillow
powershell -File Docs/compress_screenshots.ps1 "Assets/References/Roster Screen"
```

### 3. Add to CLAUDE.md under "Debugging Unity"

Add this section:

```markdown
### Screenshots for visual review
Take a screenshot of the Game View for Claude (architect) to compare against references:
- In Unity Play mode, navigate to the screen you want to capture
- Menu: GOLFIN > Screenshot > Capture Game View
- Screenshot saves to Assets/Screenshots/screenshot_YYYY-MM-DD_HH-mm-ss.png
- Claude (architect) reads it directly via filesystem access at C:\Users\cesar\GolfinRedux
- Reference images are in Assets/References/ with _compressed subfolders for comparison
- Screenshots and references must be compressed (max 800px wide) for Claude to read them

### TellCode.md workflow
- Claude (architect) writes instructions to Docs/TellCode.md
- Read this file at the start of each task
- After completing, add a status line at the bottom
```

### 4. Clean up loose files in project root

Move these stale .cs files out of the project root (they are old copies — the real files are in Assets/Scripts):
- `C:\Users\cesar\GolfinRedux\CharacterDetailPanel.cs`
- `C:\Users\cesar\GolfinRedux\RosterScreenController.cs`
- `C:\Users\cesar\GolfinRedux\StatBar.cs`

Move them to `Assets/Scripts/Editor/Archive/` or delete them.

---

## Completed Tasks

✅ DONE: 2026-03-20 — Task 1: ScreenshotTool.cs created at Assets/Scripts/Editor/ScreenshotTool.cs
✅ DONE: 2026-03-20 — Task 2: compress_screenshots.ps1 created at Docs/compress_screenshots.ps1
✅ DONE: 2026-03-20 — Task 3: CLAUDE.md updated with full screenshot + TellCode.md workflow sections
✅ DONE: 2026-03-20 — Task 4: Deleted stale root-level CharacterDetailPanel.cs, RosterScreenController.cs, StatBar.cs
