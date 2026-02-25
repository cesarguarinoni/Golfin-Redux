# GOLFIN Visual QA Pipeline

Automated visual comparison between Unity builds and Figma designs.

## Setup

```bash
pip install requests Pillow numpy
```

## Workflow

### 1. Export Figma reference screenshots
```bash
cd QA
python3 visual_qa.py --export-figma
```
This exports all screens from Figma as PNGs to `QA/Screenshots/Figma/`.

### 2. Capture Unity screenshots
In Unity Editor: **Tools → QA → Capture All Screens**

This saves screenshots to `QA/Screenshots/Unity/`.

### 3. Run comparison
```bash
python3 visual_qa.py --compare
```

### 4. Or do everything at once
```bash
python3 visual_qa.py --full
```

### 5. Compare a specific screen
```bash
python3 visual_qa.py --compare --screen LoadingScreen
```

## Output

- `QA/Reports/qa_report_latest.json` — Machine-readable report
- `QA/Reports/qa_report_latest.md` — Human-readable markdown
- `QA/Reports/diff_*.png` — Visual diff images (differences highlighted)

## Screen Map

| Unity Name | Figma Page | Node ID |
|-----------|------------|---------|
| LogoScreen | Logo | 2622:843 |
| SplashScreen | Splash Screen | 2032:327 |
| LoadingScreen | Loading | 4096:1181 |

Add new screens to `SCREEN_MAP` in `visual_qa.py`.

## AI-Powered Deep Analysis

For detailed UI/UX analysis beyond pixel diff:
1. Upload both Unity + Figma screenshots to the Telegram dev chat
2. Ask Kai to compare them — it'll return a structured diff with exact fix instructions

## Figma Token

Store your token in `QA/.figma_token` (gitignored) or set `FIGMA_TOKEN` env var.
