# GOLFIN Redux — Architecture Document

> Last updated: 2026-02-25

## Overview

GOLFIN Redux is a premium, simulation-style golf game built in Unity. The project follows a **UI-first development approach** — building all UI screens and systems before gameplay, then integrating progressively.

**Target platforms:** iOS / Android (iPhone-first, 1170×2532 reference resolution)
**Engine:** Unity (C#)
**Art style:** Premium, somber, realistic (references: Golf Super Club, PGA Tour 2K, Golf Clash)

---

## Development Phases

| Phase | Focus | Duration | Status |
|-------|-------|----------|--------|
| 1 | UI Screens & Systems | ~6 weeks | 🔵 In Progress |
| 2 | Core Gameplay (simulation swing) | 1-2 months | ⬜ Planned |
| 3 | Platform Integration | TBD | ⬜ Planned |
| 4 | Multiplayer | TBD | ⬜ Planned |
| 5 | Content Expansion & Addressables | TBD | ⬜ Planned |

---

## Project Structure

```
Assets/
├── Art/
│   └── UI/
│       ├── golfin_logo.png          # GOLFIN logo sprite
│       └── Tips/                     # Pro tip illustration images
│           ├── tip_first.png
│           ├── tip_flick.png
│           └── ...
├── Code/
│   └── Scripts/
│       ├── Core/
│       │   ├── GameBootstrap.cs      # Entry point, startup sequence
│       │   ├── ScreenBase.cs         # Abstract base for all screens
│       │   └── ScreenManager.cs      # Singleton, screen transitions
│       ├── Screens/
│       │   ├── LogoScreen.cs         # Logo display (no interaction)
│       │   ├── LoadingScreen.cs      # Loading bar + pro tips
│       │   └── SplashScreen.cs       # Title screen with buttons
│       ├── UI/
│       │   ├── LoadingBar.cs         # Animated progress bar
│       │   ├── PressableButton.cs    # Button with press-down state
│       │   └── ProTipCard.cs         # Auto-cycling tip card
│       ├── Localization/
│       │   ├── LocalizationManager.cs # CSV-based localization singleton
│       │   └── LocalizedText.cs      # Auto-localize any TMP text
│       └── Editor/
│           └── CreateUIScreen.cs     # Editor tool: auto-builds scene
├── Resources/
│   └── Data/
│       └── localization.csv          # All localized strings
└── Scenes/
    └── Startup.unity                 # Main startup scene
```

---

## Core Systems

### 1. Screen Management

**Pattern:** Singleton `ScreenManager` + `ScreenBase` abstract class

```
GameBootstrap (entry point)
    → ScreenManager.ShowImmediate(logoScreen)     // Phase 1: Logo
    → ScreenManager.TransitionTo(loadingScreen)   // Phase 2: Loading
    → ScreenManager.TransitionTo(splashScreen)    // Phase 3: Splash
```

- **ScreenBase** provides `Show()`, `Hide()`, `FadeIn()`, `FadeOut()` via `CanvasGroup`
- **ScreenManager** handles crossfade transitions (configurable `fadeDuration`)
- Each screen overrides `OnScreenEnter()` / `OnScreenExit()` for lifecycle hooks
- All screens live under a single `Canvas` (Screen Space - Overlay)

### 2. Localization

**Pattern:** CSV-based, runtime-loaded from `Resources/`

**CSV format:**
```csv
key,en,ja,es
btn_start,START,スタート,INICIAR
tip_forecast,"LANDING {gold}FORECAST{/gold}...",着地{gold}予測{/gold}は...,EL {gold}PRONÓSTICO{/gold}...
```

- **LocalizationManager** (singleton): Loads CSV at `Awake()`, provides `GetText(key)`
- **LocalizedText** (component): Attach to any `TextMeshProUGUI` → auto-updates on language change
- **Gold highlighting:** `{gold}text{/gold}` tags → converted to TMP `<color>` rich text
- **Language switch:** `LocalizationManager.Instance.SetLanguage("ja")` → fires `OnLanguageChanged` event
- **CSV path:** `Resources/Data/localization` (loaded via `Resources.Load`)

**Supported languages:** English (en), Japanese (ja), Spanish (es)

### 3. ProTipCard System

**Pattern:** Single card component that cycles through tips

- Tips defined as localization keys in Inspector (`tipKeys` array)
- Optional illustration images per tip (`tipImages` array)
- Auto-cycles on timer (`autoCycleInterval`, default 8s)
- Tap to advance immediately (`IPointerClickHandler`)
- Crossfade animation between tips (`textFadeDuration`)
- **Auto-resizing:** Uses `VerticalLayoutGroup` + `ContentSizeFitter`
  - Card height grows/shrinks based on text length and image presence
  - Padding: 40px all sides, 20px spacing
  - Anchored at top, grows downward

### 4. PressableButton

**Pattern:** Visual feedback component for buttons

- On press: scales to `pressedScale` (0.95) + tints with `pressedTint`
- On release: smoothly returns to original state (`transitionSpeed`)
- Fires `onClick` UnityEvent
- Optional `AudioClip` on press
- Implements `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`

### 5. LoadingBar

**Pattern:** Animated pill-shaped progress bar

- `SetProgress(float)` — smooth animation toward target
- `SetProgressImmediate(float)` — instant jump
- Color gradient from `fillColorStart` → `fillColorEnd` based on progress
- Optional glow image that follows the fill edge
- Uses `Image.Type.Filled` (Horizontal fill method)

---

## Scene Hierarchy

Generated automatically by `Tools → Create GOLFIN UI Scene`:

```
Scene Root
├── Managers
│   ├── LocalizationManager
│   ├── ScreenManager
│   └── GameBootstrap          ← refs: logoScreen, loadingScreen, splashScreen
├── Canvas (Screen Space - Overlay, 1170×2532)
│   ├── LogoScreen
│   │   ├── Background         (black, full stretch)
│   │   └── Logo               (centered, 608×139, Y 38.5%)
│   ├── LoadingScreen
│   │   ├── Background         (full stretch, golf course image)
│   │   ├── ProTipCard         (VerticalLayoutGroup, auto-resize)
│   │   │   ├── Header         ("PRO TIP", 52px)
│   │   │   ├── Divider        (gold line, 3px)
│   │   │   ├── TipText        (38px, auto-height)
│   │   │   ├── TipImage       (456px height)
│   │   │   └── TapNextText    ("TAP FOR NEXT TIP", 24px)
│   │   ├── NowLoadingText     ("NOW LOADING", 72px, Y 82.5%)
│   │   ├── LoadingBarBG       (842×32, Y 87.5%)
│   │   │   ├── LoadingBarFill (filled image, blue gradient)
│   │   │   └── LoadingBarGlow (white, follows fill edge)
│   │   └── DownloadProgress   ("X / 267 MB", 28px, Y 90%)
│   └── SplashScreen
│       ├── Background         (golf course art)
│       ├── TitleArea
│       │   ├── PresentsText   ("GOLFIN presents", 58px)
│       │   ├── ShieldLogo     (175×200)
│       │   └── SubtitleText   ("The Invitational", 100px italic)
│       ├── BottomGradient     (dark overlay, bottom 27%)
│       ├── StartButton        (480×130, green #5CBF2A, Y 83.5%)
│       │   └── Text           ("START", 72px white)
│       └── CreateAccountButton (680×100, transparent, Y 91.2%)
│           └── Text           ("CREATE ACCOUNT", 62px white)
└── EventSystem
```

---

## Editor Automation

### CreateUIScreen.cs (`Tools → Create GOLFIN UI Scene`)

- Creates the **entire scene hierarchy** from code
- **Auto-wires all Inspector references** via reflection (`SetPrivateField`)
- **Positions match reference designs** (pixel-accurate at 1170×2532)
- **Adds all components:** ScreenBase subclasses, ProTipCard, LoadingBar, PressableButton, LocalizedText
- **Sets localization keys** on all static text elements
- **Zero manual Inspector work** after running

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| UI-first development | Lower risk, visual progress early, reusable across future titles |
| CSV localization (not Unity Localization package) | Simpler, editable in Google Sheets/Excel, no package dependencies |
| Single scene for startup flow | Simpler state management, CanvasGroup fading for transitions |
| No gacha system | Removed for simplification; fixed upgrade paths instead |
| Serverless initially | No backend until multiplayer phase |
| Addressables deferred | Until content roadmap exists |
| Editor script for scene setup | Eliminates manual Inspector wiring, reproducible builds |
| ContentSizeFitter on ProTipCard | Tips vary in text length; card must adapt |

---

## Simplified Game Design (vs Original)

| Original | Redux |
|----------|-------|
| 4 character stats | 2-3 stats |
| 6 club parameters | 3-4 parameters |
| 6-tier rarity | 3-4 tiers |
| Gacha acquisition | Fixed upgrade paths |
| Traits system | Removed |
| Lifetime system | Removed |
| Condition system | Removed |
| Skill point distribution | Predefined levels |

---

## Key References

| Resource | Location |
|----------|----------|
| GitHub (Cesar's project) | https://github.com/cesarguarinoni/Golfin-Redux |
| GitHub (reference scripts) | https://github.com/kenken1130/golfin-redux-ui |
| UI Design mockups | https://drive.google.com/drive/folders/1CVMW8FyFVsZa7rKYUmfuTTXgQthz7ySg |
| Original design doc (1202p) | https://drive.google.com/file/d/1g8aEYuMOACSh_zyzPTmWoDJzCxz27RQo/ |
| Backlog spreadsheet | https://docs.google.com/spreadsheets/d/1IHUCty7TjLjzFAqfSbJ13SmJPLwvoeaQNu9SSj7EMo0/ |

---

## Team

| Name | Role | Contact |
|------|------|---------|
| Ken Komatsu (kenken) | Project Lead, Founder & CEO | ken@wonderwall-g.com |
| Cesar Guarinoni | Game Planner / Developer | cesar.guarinoni@wonderwall-g.com |
| Kai (AI) | Dev support, code gen, architecture | @aikenken_bot |

---

## Fonts

The reference designs use a geometric sans-serif throughout. **Montserrat** (Google Fonts, free) is the closest match.

### Required Font Assets

Download [Montserrat from Google Fonts](https://fonts.google.com/specimen/Montserrat), then create TMP Font Assets:

| Font File | TMP Asset Name | Used For |
|-----------|---------------|----------|
| `Montserrat-Black.ttf` | `Montserrat-Black SDF` | "NOW LOADING" (72px) |
| `Montserrat-Bold.ttf` | `Montserrat-Bold SDF` | "PRO TIP", buttons, download text |
| `Montserrat-SemiBold.ttf` | `Montserrat-SemiBold SDF` | Tip body text |
| `Montserrat-Italic.ttf` | `Montserrat-Italic SDF` | "TAP FOR NEXT TIP" |

### Setup Steps

1. Download Montserrat family from Google Fonts
2. In Unity: **Window → TextMeshPro → Font Asset Creator**
3. Source Font: drag in each `.ttf` file
4. Atlas Resolution: 2048×2048
5. Character Set: Extended ASCII (or Unicode if JP needed)
6. Click **Generate Font Atlas** → **Save** to `Assets/Fonts/`
7. Name each asset exactly as above (e.g. `Montserrat-Bold SDF`)
8. The `CreateUIScreen` tool will auto-assign them

**Note:** If fonts aren't found, the script falls back to TMP default with a warning in Console.

For Japanese text, create a separate JP font asset (e.g. Noto Sans JP) and set it as the fallback font in each Montserrat asset.

---

## Open Questions

1. **Monetization model** — Gacha removed, replacement TBD (biggest risk)
2. **Club acquisition** — How do players get new clubs?
3. **Stamina/energy system** — Keep or remove?
4. **Gear system** — Simplify or cut?
5. **NFT integration** — Status TBD
6. **Font selection** — Montserrat chosen as primary; need Noto Sans JP for Japanese fallback

---

## Changelog

| Date | Change |
|------|--------|
| 2025-02-25 | Initial architecture doc |
| 2025-02-25 | Phase 1 UI: Logo, Loading, Splash screens |
| 2025-02-25 | Auto-wiring editor script |
| 2025-02-25 | Exact layout positions from reference designs |
| 2025-02-25 | ProTipCard auto-resize with VerticalLayoutGroup |
| 2025-02-25 | TitleArea → single image, Montserrat fonts on all text |
