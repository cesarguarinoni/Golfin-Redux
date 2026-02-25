# GOLFIN Visual QA Workflow

Step-by-step guide for keeping Unity screens matched to Figma.

---

## First Time Setup (once)

### Step 1 — Install Python dependencies
```bash
pip install requests Pillow numpy
```

### Step 2 — Install fonts in Unity
In Unity Editor: **Tools → Install Figma Fonts**

This auto-downloads Rubik + Arapey from Google Fonts and creates TMP SDF assets. You'll see the results in `Assets/Fonts/`. If any fail, it'll tell you which to download manually.

### Step 3 — Store your Figma token
Create `QA/.figma_token` with your token (already gitignored):
```
figd_YOUR_TOKEN_HERE
```

---

## Every Time You Change a Screen

### Step 1 — Build the UI in Unity
Run **Tools → Create GOLFIN UI Scene** to apply the latest code changes.

### Step 2 — Capture Unity screenshots
Run **Tools → QA → Capture All Screens**

Screenshots save to `QA/Screenshots/Unity/`.

### Step 3 — Export Figma references
```bash
cd QA
python3 visual_qa.py --export-figma
```
Downloads PNGs of each Figma screen to `QA/Screenshots/Figma/`.

### Step 4 — Run the comparison
```bash
python3 visual_qa.py --compare
```

**Output:**
- Terminal shows match % per screen (✅ >95% | 🟡 80-95% | 🔴 <80%)
- `QA/Reports/diff_*.png` — visual diff images (mismatches highlighted)
- `QA/Reports/qa_report_latest.md` — readable summary

### Step 5 — AI deep analysis (if needed)
For anything the pixel diff can't explain, upload both screenshots (Unity + Figma) to the **GOLFIN<>dev** Telegram chat and tag @aikenken_bot. You'll get:
- Exact element-by-element diff
- Specific fix instructions with pixel values
- Updated code pushed directly to the repo

---

## Quick Commands

| What | Where | Command |
|------|-------|---------|
| Install fonts | Unity | Tools → Install Figma Fonts |
| Check fonts | Unity | Tools → Check Figma Fonts |
| Build screens | Unity | Tools → Create GOLFIN UI Scene |
| Capture screenshots | Unity | Tools → QA → Capture All Screens |
| Export Figma PNGs | Terminal | `python3 QA/visual_qa.py --export-figma` |
| Compare | Terminal | `python3 QA/visual_qa.py --compare` |
| Full pipeline | Terminal | `python3 QA/visual_qa.py --full` |
| Single screen | Terminal | `python3 QA/visual_qa.py --compare --screen LoadingScreen` |
| Design tokens | Terminal | `python3 QA/visual_qa.py --tokens` |

---

## Adding New Screens

1. Add the screen to `SCREEN_MAP` in `visual_qa.py`:
```python
SCREEN_MAP = {
    ...
    "HomeScreen": {"page": "Home Screen", "node_id": "2098:3766"},
}
```

2. Node ID is the Figma page ID (visible in the URL or via the API).

---

## Preserving Your Manual Changes

When you tweak fonts, positions, or sizes directly in the Unity Inspector:

1. **Tools → QA → Export Scene Values** — saves all current values to `Assets/Code/Data/screen_values.json`
2. **Commit & push** — Kai reads this file and updates `CreateUIScreen.cs` to match
3. Next time `Create GOLFIN UI Scene` runs, your tweaks are preserved

**Tools → QA → Show Scene Changes** — shows what you've changed since last export (without overwriting).

---

## Troubleshooting

**"Rate limit exceeded"** — Figma free tier limits API calls. Wait 15-30 min and retry.

**Font not found in Unity** — Run Tools → Check Figma Fonts to see what's missing, then Tools → Install Figma Fonts.

**Screenshots are black** — Make sure at least one screen has its CanvasGroup alpha=1 before capturing.
