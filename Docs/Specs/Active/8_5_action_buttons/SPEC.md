# SPEC — `8_5_action_buttons`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state.

## Goal

Wire the 2×2 action button cluster at the bottom of the in-game shot HUD per Figma `In-Game - Shot Tests 9` (4065:15675). Two real selectors (Driver = club, Golfin = ball) with hold-and-slide overlays driven by inventory data; two visual-state toggles (Spin opens a center-screen placeholder modal, Fade/Draw cycles between Straight ↔ Fade/Draw). Visual fidelity is the primary acceptance gate. Physics integration is **out of scope** — Spin and Fade/Draw are state-bus + visuals only.

## Reference

- **Figma frame:** page `In-game` / frame `In-Game - Shot Tests 9` / id `4065:15675` in file `5gEAHjl6xAtW8iYY7NMvWd`.
- **Figma sub-frames (selectors):** `Selector - Club` id `10550:99728`, `Selector - Ball` id `10550:99730` (canonical hold-and-slide overlays).
- **Reference PNGs (visual diff companions):**
  - `Docs/Reference/In-game UI/In-Game - Shot Tests 9.png` — bottom button row in context.
  - `Docs/Reference/In-game UI/Selector - Club.png` — club picker overlay.
  - `Docs/Reference/In-game UI/Selector - Ball.png` — ball picker overlay.
  - `Docs/Reference/In-game UI/Straight Shot.png` — Straight button state.

### Placeholder vs canonical content notes

- The **DRIVER label "195.7 yrds"** in Figma is a placeholder — wire to real `ClubDataRuntime.baseDistance` in v1.
- The **GOLFIN ∞ label** is canonical — Golfin ball is the unlimited default (`PlayerBallData.IsUnlimited` returns true for it; `BallManager.GetQuantityDisplay` returns `"∞"`).
- The **SPIN icon** in Figma is the small ball-with-arrow icon. The Spin sub-panel itself is placeholder for v1 — Cesar's instruction: "big ball center of screen with a selection dot, placeholder UI for now."
- The **FADE/DRAW two-line label** in Figma is one of two canonical states. The other state is **STRAIGHT** (single-line label, upward-arrow icon). Per Cesar: button starts as STRAIGHT, taps cycle to FADE/DRAW and back.
- **Layout discrepancy** (flag-only, no action needed): `In-Game - Shot Tests 6` has Spin/Driver paired and Golfin/Straight paired (selectors on left, modes on right — arguably more ergonomic for left/right thumb usage). Cesar pinned `Shot Tests 9` as source of truth, so **9's layout wins**: top row SPIN+FADE/DRAW, bottom row GOLFIN+DRIVER. Note for future design clarification only; do not change the layout in 8.5.

## Architecture context

### Asmdef boundaries

- **Widgets** live in `Assets/Scripts/Gameplay/UI/ShotUI/` (asmdef `Golfin.Gameplay.UI`, `autoReferenced: true`, no `Assembly-CSharp` ref). This is the same asmdef that hosts `PlayerCardWidget`, `HoleCardWidget`, `PowerGaugeWidget`, etc.
- **Static contexts** live in `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` (same asmdef) — sibling files to `PlayerContext.cs`, `HoleContext.cs`, `WindContext.cs`, `GameSession.cs`.
- **Populator MonoBehaviours** live in `Assets/Scripts/UI/HUD/` (Assembly-CSharp bucket, can see `BagManager`/`ClubManager`/`BallManager`/`ClubDatabaseCSV`/`BallDatabaseCSV`). This is the same folder that hosts `PlayerContextPopulator.cs`.
- This is the **canonical asmdef workaround** documented in `Docs/Architecture/RUNTIME_BLUEPRINT.md` §2/§3. Do not deviate; do not add `Assembly-CSharp` to the `Golfin.Gameplay.UI` asmdef references — it would create a build cycle.

### Existing code referenced

- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerContext.cs` — pattern for the new `*Context` statics.
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` — pattern for the new `*Context` statics.
- `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs` — pattern for the new populators.
- `Assets/Scripts/Gameplay/UI/ShotUI/PowerGaugeWidget.cs` — pattern for `ShotController.OnStateChanged` subscription.
- `Assets/Scripts/Gameplay/UI/ShotUI/ClubSelectionBroadcast.cs` — existing static bus from 8.2.5, already raised by `PhysicsLabController.SetClub()`. Reused (no changes).
- `Assets/Scripts/Editor/CanvasScalerMigration/IndicatorWidgetBuilder.cs` — pattern for the new `ActionButtonsBuilder` editor builder.

### Manager APIs (verified against source 2026-04-29)

```csharp
// ClubManager (Assembly-CSharp)
public static ClubManager Instance { get; }
public PlayerClubData? GetClubData(string clubId);
public ClubDataRuntime? GetTemplate(string clubId);            // delegates to ClubDatabaseCSV
public event Action<string>? OnClubEquipped;
public event Action<string>? OnClubLeveledUp;
public event Action? OnInventoryChanged;

// BagManager (Assembly-CSharp, no namespace)
public static BagManager Instance { get; }
public int EquippedBagSlot { get; }                            // 1-based, 0=none
public List<PlayerClubData> GetClubsInBag(int bagSlot);
public event Action<int>? OnBagChanged;
public event Action<int>? OnEquippedBagChanged;

// BallManager (Assembly-CSharp, no namespace)
public static BallManager Instance { get; }
public PlayerBallData? GetBallData(string ballId);
public List<string> GetAllOwnedBallIds();                      // excludes qty=0
public string GetQuantityDisplay(string ballId);               // "∞" or "x99"
public event Action? OnInventoryChanged;

// ClubDataRuntime fields used:
//   string name, ClubType type, int baseDistance, Sprite? portraitSprite
//   string GetTypeLabel()  → "DRIVER"/"WOOD"/"IRON"/"A. WEDGE"/"P. WEDGE"/"S. WEDGE"/"PUTTER"

// BallDataRuntime fields used:
//   string name, Sprite? thumbnailSprite, Sprite? fullSprite

// PhysicsLabController (Golfin.Physics.Viewer) — existing
public static readonly string[] LabClubLabels = { "Driver", "Iron 7", "Wedge", "Putter" };
public int CurrentClubIndex { get; private set; }              // 0..3
public event Action<int> OnClubChanged;
public void SetClub(int index);                                // also raises ClubSelectionBroadcast
```

> **NEEDS-VERIFICATION:** `ClubDatabaseCSV.GetClub(clubId)` and `BallDatabaseCSV.GetBall(ballId)` are referenced but I have not confirmed signatures. Code: open both files first, confirm method names + return types, and adjust populator calls if they differ. If they do not exist as `GetClub` / `GetBall`, surface to Architect — do NOT add new methods.

### Existing assets

- `Assets/Art/In-Game UI/Button - All.png` — pre-baked white-top + navy-bottom card with cream `#F3ECC2` border + drop shadow. **Use this as the button background sprite** instead of layering primitives. **Verify import settings: `textureType=Sprite` (not Default).**
- `Assets/Art/In-Game UI/Icon - Spin.png` — Spin button glyph (ball with rotation arrow).
- `Assets/Art/In-Game UI/Icon - DrawFade.png` — Fade/Draw button glyph (curved arc).
- `Assets/Art/In-Game UI/Icon - Straight.png` — Straight button glyph (upward arrow).
- `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` — TMP font (per existing 8.x convention; **NOT** `Rubik-SemiBold SDF`).
- Club portraits: `ClubDataRuntime.portraitSprite` is pre-loaded from `Resources/Clubs/Thumbnails/{name}` at CSV parse time. Use the field directly, do not Resources.Load again.
- Ball thumbnails / full: `BallDataRuntime.thumbnailSprite` (small) + `fullSprite` (big, used by Spin sub-panel center ball).

## Implementation

The work is in five layers, build them in this order:

**Layer 1** — static contexts (asmdef side, no manager state).
**Layer 2** — populators (Assembly-CSharp side, bridge managers ↔ contexts).
**Layer 3** — the four button widgets + the selector overlay widget.
**Layer 4** — the Spin sub-panel (placeholder big ball + 5-position dot).
**Layer 5** — `ActionButtonsBuilder` editor menu to create + wire the hierarchy in `LabScaffold.unity`.

### Layout reference (1170-wide canvas, 1 Figma px = 1 Unity unit)

Per Figma `4065:15675`, the bottom buttons are TWO `Bottom Buttons` rows nested inside `Game Screen Content` (which has its own padding `pt=24, pb=96, px=48`). Each row is `flex justify-between` with `padding=10`. The two rows have a 24px gap between them.

The cleanest way to author this in Unity: anchor each of the four buttons individually to the bottom-left or bottom-right corner of the canvas. The Figma's vertical-flex container with content alignment is a tooling artifact; in Unity we just pin them.

**Button RectTransform values (canvas root = 1170×2532):**

| Button     | Anchor       | Pivot     | anchoredPosition | Size      |
|------------|--------------|-----------|------------------|-----------|
| SPIN       | (0,0) BL     | (0,0)     | (58,  360)       | 145×240   |
| FADE/DRAW  | (1,0) BR     | (1,0)     | (-58, 360)       | 145×240   |
| GOLFIN     | (0,0) BL     | (0,0)     | (58,  96)        | 145×240   |
| DRIVER     | (1,0) BR     | (1,0)     | (-58, 96)        | 145×240   |

**Derivation:** Figma's outer `Game Screen Content` has `pb=96` (so bottom row of buttons sits 96px above canvas bottom). The two rows have 24px gap, button height 240, so top row sits at 96 + 240 + 24 = 360. Within each row the inner `padding=10` puts buttons at `48+10=58` from edge. **If round 1 visual diff shows a Y mismatch, the 360 value is the first knob to turn — adjust within ±20px to land 1:1 with the reference.**

### Button card visuals

All four buttons share the same skeleton:

```
{ButtonName}                 RectTransform 145×240, anchor + pivot per table above
├── CardBG       Image       sprite=Button - All.png, type=Simple, stretch-fill parent (anchor 0,0 / 1,1)
├── IconArea     RectTransform 180×120, anchor (0.5, 1) top-center, pivot (0.5, 1), pos (0, 0)
│   └── Icon     Image       sprite per button (see below), preserveAspect=true,
│                            anchor stretch-stretch with padding insets per button
├── PrimaryText  TMP_Text    fontSize 30, Rubik Medium, white, alignment Center,
│                            anchor (0,0)/(1,0) bottom-stretch, pivot (0.5,0)
│                            anchoredPosition (0, 65), sizeDelta = (0, 36)         (single-line variant)
└── SecondaryText TMP_Text   fontSize 30, Rubik Medium, white, alignment Center,
                             richText = true                                       (used by DRIVER + GOLFIN)
                             anchor (0,0)/(1,0) bottom-stretch, pivot (0.5,0)
                             anchoredPosition (0, 24), sizeDelta = (0, 36)         (only used for DRIVER + GOLFIN)
```

**Y values for labels:** the Figma navy-data half is 120px tall starting at the bottom of the card. The two text positions inside that half are at `top=19` and `top=60` (per Figma `2483:7448` / `2483:7449`). Converting to bottom-anchored Unity: PrimaryText center sits at `120 - 19 - 18 = 83` from bottom of LabelArea → use `anchoredPosition.y = 65` (LabelArea origin at button bottom). SecondaryText at `120 - 60 - 18 = 42` → `anchoredPosition.y = 24`. **If the visual diff shows label positions off, those Y values are the second knob.**

**IconArea is 180px wide, but the button itself is 145px wide. This is intentional** — Figma authoring shows the icon overflowing the card edges (`Ball Portrait` frame `w=180` inside a `w=145` button). It produces the bleed effect visible in the reference PNG. Set `Image.preserveAspect=true` and **disable `Mask` / `RectMask2D`** on the card root; the icon visually overflows the 145 frame within IconArea's 180×120 bounds.

**Icon padding (anchor stretch-stretch within IconArea, then inset):**

| Button     | Icon sprite              | Icon RT inset                                        |
|------------|--------------------------|------------------------------------------------------|
| SPIN       | `Icon - Spin.png`        | offsetMin (33, 0), offsetMax (-33, 0) → 114×120      |
| FADE/DRAW  | `Icon - DrawFade.png`    | offsetMin (33, 0), offsetMax (-33, 0) → 114×120      |
| STRAIGHT   | `Icon - Straight.png`    | offsetMin (33, 0), offsetMax (-33, 0) → 114×120      |
| GOLFIN     | `BallContext.SelectedThumbnail` | offsetMin (50, 0), offsetMax (-50, 0) → 80×120  |
| DRIVER     | `ClubContext.SelectedPortrait`  | offsetMin (33, 0), offsetMax (-33, 0) → 114×120 |

**Label content per button:**

| Button     | PrimaryText      | SecondaryText              | Notes |
|------------|-------------------|----------------------------|-------|
| SPIN       | `SPIN`           | (hidden)                   | Static |
| FADE/DRAW  | `FADE/\nDRAW`    | (hidden)                   | Two-line; `enableWordWrapping=true`, line height 36 stacks. **State-driven** — see below. |
| STRAIGHT   | `STRAIGHT`       | (hidden)                   | Single-line state |
| GOLFIN     | `{ball.name.ToUpper()}` (e.g. `GOLFIN`) | `{quantityDisplay}` (`∞` / `x99`) | Both visible. |
| DRIVER     | `{template.GetTypeLabel()}` (e.g. `DRIVER`) | `{distance}<size=20><b> yrds</b></size>` | Both visible, rich-text on second line. |

**Tap behavior** (DRIVER + GOLFIN buttons only): tap opens the `SelectorOverlay`. Outside-tap closes it. Tapping a card commits selection and closes. **Hold-and-slide is OUT of scope for v1** — file as polish follow-up if Cesar wants it later.

### Static context classes

Create four files in `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`:

#### `ClubContext.cs`

```csharp
using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Static bus for the player's currently-selected club (drives the DRIVER action button + selector).
    /// Populated by ClubContextPopulator (Assembly-CSharp side) which reads BagManager/ClubManager.
    /// Widgets request a selection change via OnSelectionRequested; the populator (Assembly-CSharp side)
    /// listens and calls SelectByIndex() — this is the cross-asmdef return path.
    /// </summary>
    public static class ClubContext
    {
        public static string  SelectedClubId    = "";
        public static string  SelectedTypeLabel = "DRIVER";
        public static int     SelectedDistance  = 0;
        public static Sprite? SelectedPortrait  = null;
        public static System.Collections.Generic.List<ClubEntry> EquippedBag = new();
        public static int     SelectedIndex     = 0;

        public static event Action? OnSelectedChanged;
        public static event Action? OnBagChanged;
        public static event Action<int>? OnSelectionRequested;  // widget → populator

        public static void RaiseSelectedChanged() => OnSelectedChanged?.Invoke();
        public static void RaiseBagChanged()      => OnBagChanged?.Invoke();
        public static void RequestSelection(int idx) => OnSelectionRequested?.Invoke(idx);

        public static void Reset()
        {
            SelectedClubId    = "";
            SelectedTypeLabel = "DRIVER";
            SelectedDistance  = 0;
            SelectedPortrait  = null;
            EquippedBag.Clear();
            SelectedIndex     = 0;
            RaiseBagChanged();
            RaiseSelectedChanged();
        }
    }

    public class ClubEntry
    {
        public string  ClubId       = "";
        public string  TypeLabel    = "";
        public int     Distance     = 0;
        public Sprite? Portrait     = null;
        public int     LabClubIndex = 0;
    }
}
```

#### `BallContext.cs`

```csharp
using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class BallContext
    {
        public static string  SelectedBallId          = "";
        public static string  SelectedNameLabel       = "GOLFIN";
        public static string  SelectedQuantityDisplay = "∞";
        public static Sprite? SelectedThumbnail       = null;
        public static Sprite? SelectedFullSprite      = null;
        public static System.Collections.Generic.List<BallEntry> OwnedBalls = new();
        public static int     SelectedIndex           = 0;

        public static event Action? OnSelectedChanged;
        public static event Action? OnBagChanged;
        public static event Action<int>? OnSelectionRequested;

        public static void RaiseSelectedChanged() => OnSelectedChanged?.Invoke();
        public static void RaiseBagChanged()      => OnBagChanged?.Invoke();
        public static void RequestSelection(int idx) => OnSelectionRequested?.Invoke(idx);

        public static void Reset()
        {
            SelectedBallId          = "";
            SelectedNameLabel       = "GOLFIN";
            SelectedQuantityDisplay = "∞";
            SelectedThumbnail       = null;
            SelectedFullSprite      = null;
            OwnedBalls.Clear();
            SelectedIndex           = 0;
            RaiseBagChanged();
            RaiseSelectedChanged();
        }
    }

    public class BallEntry
    {
        public string  BallId          = "";
        public string  NameLabel       = "";
        public string  QuantityDisplay = "";
        public Sprite? Thumbnail       = null;
        public Sprite? FullSprite      = null;
    }
}
```

#### `ShotModeContext.cs`

```csharp
using System;

namespace Golfin.Gameplay.UI.HUD
{
    public enum ShotMode { Straight, FadeDraw }

    public static class ShotModeContext
    {
        public static ShotMode Mode = ShotMode.Straight;
        public static event Action? OnChanged;
        public static void Toggle()
        {
            Mode = Mode == ShotMode.Straight ? ShotMode.FadeDraw : ShotMode.Straight;
            OnChanged?.Invoke();
        }
        public static void Reset() { Mode = ShotMode.Straight; OnChanged?.Invoke(); }
    }
}
```

#### `SpinContext.cs`

```csharp
using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    public static class SpinContext
    {
        public static Vector2 Spin = Vector2.zero;
        public static event Action? OnChanged;
        public static void SetSpin(Vector2 v)
        {
            Spin = new Vector2(Mathf.Clamp(v.x, -1f, 1f), Mathf.Clamp(v.y, -1f, 1f));
            OnChanged?.Invoke();
        }
        public static void Reset() { Spin = Vector2.zero; OnChanged?.Invoke(); }
    }
}
```

### Populators

Create two files in `Assets/Scripts/UI/HUD/` (Assembly-CSharp bucket):

#### `ClubContextPopulator.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.UI.HUD
{
    public class ClubContextPopulator : MonoBehaviour
    {
        void OnEnable()
        {
            var bag = BagManager.Instance;
            var clb = ClubManager.Instance;
            if (bag != null) { bag.OnBagChanged += OnBagChangedHandler; bag.OnEquippedBagChanged += OnEquippedBagChangedHandler; }
            if (clb != null) { clb.OnClubEquipped += OnClubEquippedHandler; clb.OnInventoryChanged += Refresh; }
            ClubContext.OnSelectionRequested += SelectByIndex;
            Refresh();
        }
        void OnDisable()
        {
            var bag = BagManager.Instance;
            var clb = ClubManager.Instance;
            if (bag != null) { bag.OnBagChanged -= OnBagChangedHandler; bag.OnEquippedBagChanged -= OnEquippedBagChangedHandler; }
            if (clb != null) { clb.OnClubEquipped -= OnClubEquippedHandler; clb.OnInventoryChanged -= Refresh; }
            ClubContext.OnSelectionRequested -= SelectByIndex;
        }
        void OnBagChangedHandler(int _) => Refresh();
        void OnEquippedBagChangedHandler(int _) => Refresh();
        void OnClubEquippedHandler(string _) => Refresh();

        void Refresh()
        {
            var bag = BagManager.Instance;
            var db  = ClubDatabaseCSV.Instance;
            if (bag == null || db == null) { ClubContext.Reset(); return; }

            int slot = bag.EquippedBagSlot;
            if (slot <= 0)               { ClubContext.Reset(); return; }

            var clubs = bag.GetClubsInBag(slot) ?? new List<PlayerClubData>();
            var entries = new List<ClubEntry>(clubs.Count);
            foreach (var pc in clubs)
            {
                var t = db.GetClub(pc.clubId);   // VERIFY: signature in ClubDatabaseCSV.cs
                if (t == null) continue;
                entries.Add(new ClubEntry
                {
                    ClubId       = pc.clubId,
                    TypeLabel    = t.GetTypeLabel(),
                    Distance     = t.baseDistance,
                    Portrait     = t.portraitSprite,
                    LabClubIndex = MapClubTypeToLabIndex(t.type),
                });
            }
            ClubContext.EquippedBag = entries;

            int newIdx = 0;
            if (!string.IsNullOrEmpty(ClubContext.SelectedClubId))
            {
                int found = entries.FindIndex(e => e.ClubId == ClubContext.SelectedClubId);
                if (found >= 0) newIdx = found;
            }
            SelectByIndex(newIdx);
            ClubContext.RaiseBagChanged();
        }

        void SelectByIndex(int idx)
        {
            if (ClubContext.EquippedBag.Count == 0)
            {
                ClubContext.SelectedClubId    = "";
                ClubContext.SelectedTypeLabel = "DRIVER";
                ClubContext.SelectedDistance  = 0;
                ClubContext.SelectedPortrait  = null;
                ClubContext.SelectedIndex     = 0;
                ClubContext.RaiseSelectedChanged();
                return;
            }
            idx = Mathf.Clamp(idx, 0, ClubContext.EquippedBag.Count - 1);
            var e = ClubContext.EquippedBag[idx];
            ClubContext.SelectedClubId    = e.ClubId;
            ClubContext.SelectedTypeLabel = e.TypeLabel;
            ClubContext.SelectedDistance  = e.Distance;
            ClubContext.SelectedPortrait  = e.Portrait;
            ClubContext.SelectedIndex     = idx;
            ClubContext.RaiseSelectedChanged();
        }

        static int MapClubTypeToLabIndex(ClubType type) => type switch
        {
            ClubType.Driver  => 0,
            ClubType.Wood    => 0,
            ClubType.Iron    => 1,
            ClubType.A_Wedge => 2,
            ClubType.P_Wedge => 2,
            ClubType.S_Wedge => 2,
            ClubType.Putter  => 3,
            _                => 0,
        };
    }
}
```

#### `BallContextPopulator.cs`

```csharp
using System.Collections.Generic;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.UI.HUD
{
    public class BallContextPopulator : MonoBehaviour
    {
        void OnEnable()
        {
            var bm = BallManager.Instance;
            if (bm != null) bm.OnInventoryChanged += Refresh;
            BallContext.OnSelectionRequested += SelectByIndex;
            Refresh();
        }
        void OnDisable()
        {
            var bm = BallManager.Instance;
            if (bm != null) bm.OnInventoryChanged -= Refresh;
            BallContext.OnSelectionRequested -= SelectByIndex;
        }

        void Refresh()
        {
            var bm = BallManager.Instance;
            var db = BallDatabaseCSV.Instance;
            if (bm == null || db == null) { BallContext.Reset(); return; }

            var ids = bm.GetAllOwnedBallIds() ?? new List<string>();
            var entries = new List<BallEntry>(ids.Count);
            foreach (var id in ids)
            {
                var t = db.GetBall(id);   // VERIFY: signature in BallDatabaseCSV.cs
                if (t == null) continue;
                entries.Add(new BallEntry
                {
                    BallId          = id,
                    NameLabel       = t.name.ToUpper(),
                    QuantityDisplay = bm.GetQuantityDisplay(id),
                    Thumbnail       = t.thumbnailSprite,
                    FullSprite      = t.fullSprite,
                });
            }
            BallContext.OwnedBalls = entries;

            int newIdx = 0;
            if (!string.IsNullOrEmpty(BallContext.SelectedBallId))
            {
                int found = entries.FindIndex(e => e.BallId == BallContext.SelectedBallId);
                if (found >= 0) newIdx = found;
            }
            SelectByIndex(newIdx);
            BallContext.RaiseBagChanged();
        }

        void SelectByIndex(int idx)
        {
            if (BallContext.OwnedBalls.Count == 0)
            {
                BallContext.SelectedBallId          = "";
                BallContext.SelectedNameLabel       = "GOLFIN";
                BallContext.SelectedQuantityDisplay = "∞";
                BallContext.SelectedThumbnail       = null;
                BallContext.SelectedFullSprite      = null;
                BallContext.SelectedIndex           = 0;
                BallContext.RaiseSelectedChanged();
                return;
            }
            idx = Mathf.Clamp(idx, 0, BallContext.OwnedBalls.Count - 1);
            var e = BallContext.OwnedBalls[idx];
            BallContext.SelectedBallId          = e.BallId;
            BallContext.SelectedNameLabel       = e.NameLabel;
            BallContext.SelectedQuantityDisplay = e.QuantityDisplay;
            BallContext.SelectedThumbnail       = e.Thumbnail;
            BallContext.SelectedFullSprite      = e.FullSprite;
            BallContext.SelectedIndex           = idx;
            BallContext.RaiseSelectedChanged();
        }
    }
}
```

### Widgets

Create the following files in `Assets/Scripts/Gameplay/UI/ShotUI/`:

#### `ActionButtonWidget.cs` (shared base)

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Gameplay.UI.ShotUI
{
    public abstract class ActionButtonWidget : MonoBehaviour
    {
        [SerializeField] protected Button   _button;
        [SerializeField] protected Image    _iconImage;
        [SerializeField] protected TMP_Text _primaryText;
        [SerializeField] protected TMP_Text _secondaryText;

        protected virtual void OnEnable()
        {
            if (_button != null) _button.onClick.AddListener(OnClick);
            Refresh();
        }
        protected virtual void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        protected abstract void Refresh();
        protected abstract void OnClick();
    }
}
```

#### `SpinButtonWidget.cs`

```csharp
using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SpinButtonWidget : ActionButtonWidget
    {
        [SerializeField] private SpinPanelWidget _spinPanel;
        protected override void Refresh() { /* static */ }
        protected override void OnClick() { if (_spinPanel != null) _spinPanel.Open(); }
    }
}
```

#### `FadeDrawButtonWidget.cs`

```csharp
using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class FadeDrawButtonWidget : ActionButtonWidget
    {
        [SerializeField] private Sprite _iconStraight;
        [SerializeField] private Sprite _iconFadeDraw;

        protected override void OnEnable()
        {
            base.OnEnable();
            ShotModeContext.OnChanged += Refresh;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            ShotModeContext.OnChanged -= Refresh;
        }

        protected override void Refresh()
        {
            if (ShotModeContext.Mode == ShotMode.Straight)
            {
                if (_iconImage   != null) _iconImage.sprite = _iconStraight;
                if (_primaryText != null) _primaryText.text = "STRAIGHT";
            }
            else
            {
                if (_iconImage   != null) _iconImage.sprite = _iconFadeDraw;
                if (_primaryText != null) _primaryText.text = "FADE/\nDRAW";
            }
            if (_secondaryText != null) _secondaryText.gameObject.SetActive(false);
        }

        protected override void OnClick() => ShotModeContext.Toggle();
    }
}
```

#### `BallButtonWidget.cs`

```csharp
using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class BallButtonWidget : ActionButtonWidget
    {
        [SerializeField] private SelectorOverlayWidget _selectorOverlay;
        [SerializeField] private Sprite _defaultThumbnail;

        protected override void OnEnable()
        {
            base.OnEnable();
            BallContext.OnSelectedChanged += Refresh;
            BallContext.OnBagChanged      += Refresh;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            BallContext.OnSelectedChanged -= Refresh;
            BallContext.OnBagChanged      -= Refresh;
        }

        protected override void Refresh()
        {
            if (_iconImage != null)
                _iconImage.sprite = BallContext.SelectedThumbnail != null ? BallContext.SelectedThumbnail : _defaultThumbnail;
            if (_primaryText   != null) _primaryText.text   = BallContext.SelectedNameLabel;
            if (_secondaryText != null) _secondaryText.text = BallContext.SelectedQuantityDisplay;
        }

        protected override void OnClick()
        {
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Ball);
        }
    }
}
```

#### `ClubButtonWidget.cs`

```csharp
using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class ClubButtonWidget : ActionButtonWidget
    {
        [SerializeField] private SelectorOverlayWidget _selectorOverlay;
        [SerializeField] private Sprite _defaultPortrait;

        protected override void OnEnable()
        {
            base.OnEnable();
            ClubContext.OnSelectedChanged += Refresh;
            ClubContext.OnBagChanged      += Refresh;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            ClubContext.OnSelectedChanged -= Refresh;
            ClubContext.OnBagChanged      -= Refresh;
        }

        protected override void Refresh()
        {
            if (_iconImage != null)
                _iconImage.sprite = ClubContext.SelectedPortrait != null ? ClubContext.SelectedPortrait : _defaultPortrait;
            if (_primaryText != null) _primaryText.text = ClubContext.SelectedTypeLabel;
            if (_secondaryText != null)
            {
                _secondaryText.richText = true;
                _secondaryText.text = $"{ClubContext.SelectedDistance}<size=20><b> yrds</b></size>";
            }
        }

        protected override void OnClick()
        {
            if (_selectorOverlay != null) _selectorOverlay.Open(SelectorOverlayWidget.Kind.Club);
        }
    }
}
```

### Selector overlay

Per Figma `10550:99728` (club) and `10550:99730` (ball): outer container width 148, vertical card stack, gap 12, prev/next chevron arrows top + bottom (96×48 rotated, 24px py). One shared widget handles both kinds.

```
SelectorOverlay   RectTransform 148 × ~744 (height grows with card count, max 744 to match Figma)
├── ArrowUp         RectTransform 96×48, anchor (0.5, 1) top-center, pivot (0.5, 1), pos (0, -24)
├── CardsContainer  RectTransform 148 × content, anchor (0.5, 0.5) center, with VerticalLayoutGroup spacing=12
│   └── (cards instantiated at runtime)
└── ArrowDown       RectTransform 96×48, anchor (0.5, 0) bottom-center, pivot (0.5, 0), pos (0, 24)
```

The overlay sits above the source button. Anchor + pivot is set per-Open so the bottom edge of the overlay aligns with the top edge of the source button:

- **Club overlay** (above DRIVER, bottom-right): anchor (1,0)/(1,0), pivot (1,0), `anchoredPosition = (-58, 96 + 240 + 12) = (-58, 348)` → bottom edge of overlay is 12px above DRIVER button's top.
- **Ball overlay** (above GOLFIN, bottom-left): anchor (0,0)/(0,0), pivot (0,0), `anchoredPosition = (58, 348)`.

#### `SelectorOverlayWidget.cs`

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SelectorOverlayWidget : MonoBehaviour
    {
        public enum Kind { Club, Ball }

        [SerializeField] private RectTransform _root;
        [SerializeField] private Transform     _cardsContainer;
        [SerializeField] private GameObject    _cardPrefab;
        [SerializeField] private Button        _arrowUp;
        [SerializeField] private Button        _arrowDown;
        [SerializeField] private OutsideClickCatcher _outsideClickCatcher; // see below

        [SerializeField] private Vector2 _anchoredPositionForClub  = new(-58f, 348f);
        [SerializeField] private Vector2 _anchoredPositionForBall  = new( 58f, 348f);

        Kind _kind;

        void OnEnable()
        {
            if (_outsideClickCatcher != null) _outsideClickCatcher.OnOutsideClick = Close;
        }

        public void Open(Kind kind)
        {
            _kind = kind;
            gameObject.SetActive(true);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(true);

            if (kind == Kind.Club)
            {
                _root.anchorMin = _root.anchorMax = new Vector2(1f, 0f);
                _root.pivot     = new Vector2(1f, 0f);
                _root.anchoredPosition = _anchoredPositionForClub;
            }
            else
            {
                _root.anchorMin = _root.anchorMax = new Vector2(0f, 0f);
                _root.pivot     = new Vector2(0f, 0f);
                _root.anchoredPosition = _anchoredPositionForBall;
            }
            Populate();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (_outsideClickCatcher != null) _outsideClickCatcher.gameObject.SetActive(false);
        }

        void Populate()
        {
            for (int i = _cardsContainer.childCount - 1; i >= 0; i--)
                DestroyImmediate(_cardsContainer.GetChild(i).gameObject);

            if (_kind == Kind.Club)
            {
                for (int i = 0; i < ClubContext.EquippedBag.Count; i++)
                {
                    int captured = i;
                    var entry = ClubContext.EquippedBag[i];
                    var go = Instantiate(_cardPrefab, _cardsContainer);
                    var card = go.GetComponent<SelectorCardWidget>();
                    card.SetClub(entry, () => { ClubContext.RequestSelection(captured); ClubSelectionBroadcast.Raise(entry.LabClubIndex); Close(); });
                }
            }
            else
            {
                for (int i = 0; i < BallContext.OwnedBalls.Count; i++)
                {
                    int captured = i;
                    var entry = BallContext.OwnedBalls[i];
                    var go = Instantiate(_cardPrefab, _cardsContainer);
                    var card = go.GetComponent<SelectorCardWidget>();
                    card.SetBall(entry, () => { BallContext.RequestSelection(captured); Close(); });
                }
            }
        }
    }

    /// <summary>
    /// Full-screen transparent Image that catches outside-taps and fires a callback. Sibling of the overlay,
    /// rendered BELOW it in the canvas hierarchy. Builder makes one of these per overlay.
    /// </summary>
    public class OutsideClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public System.Action OnOutsideClick;
        public void OnPointerClick(PointerEventData _) => OnOutsideClick?.Invoke();
    }
}
```

#### `SelectorCardWidget.cs`

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SelectorCardWidget : MonoBehaviour
    {
        [SerializeField] private Button   _button;
        [SerializeField] private Image    _icon;
        [SerializeField] private TMP_Text _primaryText;
        [SerializeField] private TMP_Text _secondaryText;

        Action? _onTap;

        public void SetClub(ClubEntry e, Action onTap)
        {
            _onTap = onTap;
            if (_icon != null) _icon.sprite = e.Portrait;
            if (_primaryText != null) _primaryText.text = e.TypeLabel;
            if (_secondaryText != null)
            {
                _secondaryText.richText = true;
                _secondaryText.text = $"{e.Distance}<size=20><b> yrds</b></size>";
            }
            WireButton();
        }

        public void SetBall(BallEntry e, Action onTap)
        {
            _onTap = onTap;
            if (_icon != null) _icon.sprite = e.Thumbnail;
            if (_primaryText != null) _primaryText.text = e.NameLabel;
            if (_secondaryText != null)
            {
                _secondaryText.richText = false;
                _secondaryText.text = e.QuantityDisplay;
            }
            WireButton();
        }

        void WireButton()
        {
            if (_button == null) return;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onTap?.Invoke());
        }
    }
}
```

### Spin sub-panel

#### `SpinPanelWidget.cs`

```csharp
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SpinPanelWidget : MonoBehaviour
    {
        [SerializeField] private Image         _ballImage;
        [SerializeField] private RectTransform _spinDot;
        [SerializeField] private OutsideClickCatcher _dimBackground;

        readonly Vector2[] _positions = {
            new(   0f,    0f), // 0 center
            new(   0f,  220f), // 1 top
            new(   0f, -220f), // 2 bottom
            new(-220f,    0f), // 3 left
            new( 220f,    0f), // 4 right
        };
        readonly Vector2[] _values = {
            new(0f, 0f), new(0f, 1f), new(0f, -1f), new(-1f, 0f), new(1f, 0f)
        };

        void OnEnable()
        {
            if (_dimBackground != null) _dimBackground.OnOutsideClick = Close;
        }

        public void Open()
        {
            gameObject.SetActive(true);
            if (_ballImage != null) _ballImage.sprite = BallContext.SelectedFullSprite;
            SnapDotToCurrent();
        }

        public void Close() => gameObject.SetActive(false);

        void SnapDotToCurrent()
        {
            int idx = 0;
            for (int i = 0; i < _values.Length; i++)
                if (Mathf.Approximately(_values[i].x, SpinContext.Spin.x) &&
                    Mathf.Approximately(_values[i].y, SpinContext.Spin.y)) { idx = i; break; }
            if (_spinDot != null) _spinDot.anchoredPosition = _positions[idx];
        }

        // Builder wires 5 invisible buttons over the ball, each calling SelectPosition(i).
        public void SelectPosition(int idx)
        {
            idx = Mathf.Clamp(idx, 0, _positions.Length - 1);
            if (_spinDot != null) _spinDot.anchoredPosition = _positions[idx];
            SpinContext.SetSpin(_values[idx]);
        }
    }
}
```

### Idle-only interaction

#### `ActionButtonsRoot.cs`

```csharp
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class ActionButtonsRoot : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;
        [SerializeField] private CanvasGroup    _group;

        void Awake()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }
        void OnEnable()
        {
            if (_shotController != null) _shotController.OnStateChanged += Handle;
        }
        void OnDisable()
        {
            if (_shotController != null) _shotController.OnStateChanged -= Handle;
        }
        void Handle(ShotInputState s)
        {
            bool idle = s.State == ShotState.Idle;
            _group.interactable   = idle;
            _group.blocksRaycasts = idle;
        }
    }
}
```

The four buttons live under a wrapper GO `ActionButtons_Cluster` that owns this component. The selector overlay + spin panel are SIBLINGS of the cluster (direct children of `ShotUI_Canvas`) so they remain raycastable independently.

### PhysicsLabController one-line edit

The lab currently calls `ClubSelectionBroadcast.Raise(index)` from `SetClub()` to *publish* changes. The widget needs to *consume* the broadcast so that picking a club in the selector overlay actually swaps the lab club. Add this in `Awake()`:

```csharp
ClubSelectionBroadcast.OnClubChanged += OnClubBroadcastReceived;
```

And in `OnDestroy()`:

```csharp
ClubSelectionBroadcast.OnClubChanged -= OnClubBroadcastReceived;
```

Plus the handler:

```csharp
void OnClubBroadcastReceived(int index)
{
    if (index == CurrentClubIndex) return;  // re-entrancy guard: SetClub() raises Broadcast, this guard prevents the loop
    SetClub(index);
}
```

This is the only `PhysicsLabController.cs` edit. Touches no physics state; pure event subscription.

### Editor builder

Create `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` with menu `GOLFIN/Build/Build Action Buttons (8.5)`. Modeled on `IndicatorWidgetBuilder.cs`. Responsibilities:

1. Load active scene; find `ShotUI_Canvas`. Error if not found.
2. Remove existing `ActionButtons_Cluster`, `SelectorOverlay`, `SpinPanel`, `OutsideClickCatcher_*` GameObjects under `ShotUI_Canvas`.
3. Coerce TextureImporter to `Sprite` for: `Button - All.png`, `Icon - Spin.png`, `Icon - DrawFade.png`, `Icon - Straight.png` (mirror IndicatorWidgetBuilder's Wind Arrow handling).
4. Load font `Assets/Fonts/Rubik-VariableFont_wght SDF.asset`.
5. Build the four buttons under `ActionButtons_Cluster` per the layout table.
6. Build `SelectorOverlay` (initially inactive) with a built-in `_cardPrefab` GO created in code (148×240 card with same skeleton as bottom buttons).
7. Build the matching `OutsideClickCatcher` (full-screen transparent Image, sibling, lower in hierarchy than overlay), one per overlay.
8. Build `SpinPanel` (initially inactive) with full-screen dim, 600×600 ball, 60×60 dot, five invisible 200×200 buttons wired to `SelectPosition(0..4)`.
9. Wire all `[SerializeField]` references via `SerializedObject.FindProperty + ApplyModifiedProperties()`.
10. Add `ClubContextPopulator` + `BallContextPopulator` to `LabRoot` if not already present.
11. Save the scene.

### Default state in LabScaffold (no BagManager/BallManager present)

- Populators silently no-op when `BagManager.Instance` / `BallManager.Instance` are null (LabScaffold case).
- Contexts keep defaults: SelectedTypeLabel="DRIVER", SelectedDistance=0, SelectedNameLabel="GOLFIN", SelectedQuantityDisplay="∞".
- DRIVER button shows `_defaultPortrait` (wire to any `Resources/Clubs/Thumbnails/...` sprite Code resolves; a driver image preferred if available).
- GOLFIN button shows `_defaultThumbnail` (similar; pick any ball thumbnail).
- Selector overlays open with **zero cards** in LabScaffold — that is per-spec acceptable for v1.

## Acceptance checklist

Each item must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

### Layout fidelity (vs `In-Game - Shot Tests 9.png`)

- [ ] Four buttons visible in 2×2 corners: SPIN top-left, FADE/DRAW top-right, GOLFIN bottom-left, DRIVER bottom-right
- [ ] Each button is 145×240, white card with cream `#F3ECC2` border and drop shadow (visual match to `Button - All.png`)
- [ ] Bottom row sits ~96px above canvas bottom; top row sits ~360px above canvas bottom
- [ ] Each button's left/right edge is ~58px from the canvas edge
- [ ] Icons visually overflow the 145-wide card (bleed effect) — IconArea 180×120 anchored top-center, no mask
- [ ] Label background is solid navy `#001E39` covering the bottom 120px of the card (this is part of `Button - All.png`)

### Data wiring (DRIVER + GOLFIN)

- [ ] DRIVER label shows `ClubContext.SelectedTypeLabel` ("DRIVER" by default; "IRON" / "P. WEDGE" / etc. when bag has those clubs)
- [ ] DRIVER yards label shows `{distance}<size=20><b> yrds</b></size>` — number bigger than "yrds" suffix
- [ ] DRIVER icon shows `ClubContext.SelectedPortrait` when bag is populated, falls back to `_defaultPortrait` otherwise (NOT a white box)
- [ ] GOLFIN label shows `BallContext.SelectedNameLabel` ("GOLFIN" default)
- [ ] GOLFIN secondary label shows `BallContext.SelectedQuantityDisplay` (`∞` for default ball)
- [ ] GOLFIN icon shows `BallContext.SelectedThumbnail` (NOT a white box)

### Selector overlay

- [ ] Tap DRIVER → vertical card stack appears ABOVE the button with all clubs in the equipped bag
- [ ] Tap GOLFIN → vertical card stack appears ABOVE the button with all owned balls
- [ ] Each card mirrors the bottom-button visual exactly (same 148×240 card, same internal layout)
- [ ] Up + down chevron arrows visible above/below the stack (use `Icon - Straight.png` rotated, OR flag if a chevron asset is needed)
- [ ] Tapping a card commits the selection: bottom button label/icon updates, overlay closes
- [ ] Tapping outside the overlay closes it without committing
- [ ] In LabScaffold (no managers): tap DRIVER and GOLFIN — selector opens with zero cards; does NOT crash

### Toggles + sub-panel

- [ ] Top-right button starts as STRAIGHT (single-line label, upward-arrow icon `Icon - Straight.png`)
- [ ] Tap STRAIGHT → button becomes FADE/DRAW (two-line label, curved-arc icon `Icon - DrawFade.png`)
- [ ] Tap FADE/DRAW → button cycles back to STRAIGHT
- [ ] `ShotModeContext.Mode` updates correspondingly (verify via Debug.Log on toggle)
- [ ] Tap SPIN → SpinPanel opens center-screen with the current `BallContext.SelectedFullSprite` rendered at 600×600
- [ ] Tap one of the 5 cardinal-position invisible buttons on the ball → dot snaps to that position; `SpinContext.Spin` updates
- [ ] Tap dim background → SpinPanel closes
- [ ] On reopen, dot is at the previously-selected position (state persists across opens)

### Lab integration (DRIVER selector → physics)

- [ ] Picking a card in the DRIVER selector swaps the lab's `CurrentClubIndex` — verify by firing a shot after picking a different club and observing different yardage
- [ ] No re-entrancy / infinite loop on club change (re-entrancy guard works)
- [ ] In LabScaffold (no inventory): DRIVER selector is empty, lab club picker (existing) still works

### Idle-only interaction

- [ ] During shot states (`PowerGaugeWidget` is alpha=1) the four action buttons are non-interactive: `ActionButtonsRoot.CanvasGroup.interactable=false, blocksRaycasts=false`
- [ ] Returning to Idle re-enables them

### Asset + scene wiring

- [ ] `Button - All.png` import settings: `textureType=Sprite` (Code MUST coerce via TextureImporter if needed)
- [ ] All `[SerializeField]` refs wired in inspector (none null) — verify by entering playmode with NullReferenceException logging on
- [ ] No white-box placeholders visible in the screenshot
- [ ] Unity Console has no errors related to this task during scene load + 30s of playmode interaction

### Visual diff

Implementer must produce a side-by-side at `Docs/Specs/Active/8_5_action_buttons/screenshots/diff-v1.png`. Reference (`In-Game - Shot Tests 9.png` cropped to bottom 600px) on left, current playmode on right, scaled to identical dimensions. If any layout-fidelity item above is FAIL, surface to Architect with the diff attached. Do NOT submit DONE.

### Spec deviations

- [ ] If any verified-as-NEEDS-VERIFICATION API mismatch was found (`ClubDatabaseCSV.GetClub`, `BallDatabaseCSV.GetBall`), it's flagged in the report with what was used instead.

## Files this task touches

**New (Assembly-CSharp):**
- `Assets/Scripts/UI/HUD/ClubContextPopulator.cs`
- `Assets/Scripts/UI/HUD/BallContextPopulator.cs`

**New (Golfin.Gameplay.UI):**
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/ClubContext.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/BallContext.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/ShotModeContext.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/SpinContext.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/SpinButtonWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/FadeDrawButtonWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/BallButtonWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs`  (also defines `OutsideClickCatcher`)
- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/ActionButtonsRoot.cs`

**New (Editor):**
- `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs`

**Modified:**
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — add `ClubSelectionBroadcast.OnClubChanged` subscription in `Awake`, unsubscribe in `OnDestroy`, handler with re-entrancy guard. **Do NOT touch any other physics code.**
- `Assets/Scenes/Physics/LabScaffold.unity` — populated by builder.

**Modified (asset import):**
- `Assets/Art/In-Game UI/Button - All.png.meta` (textureType=Sprite if not already)
- `Assets/Art/In-Game UI/Icon - Spin.png.meta`
- `Assets/Art/In-Game UI/Icon - DrawFade.png.meta`
- `Assets/Art/In-Game UI/Icon - Straight.png.meta`

**Documentation:**
- `Docs/Architecture/RUNTIME_BLUEPRINT.md` — add `ClubContext` / `BallContext` / `ShotModeContext` / `SpinContext` patterns under §3. Document `BallDatabaseCSV.GetBall` and `ClubDatabaseCSV.GetClub` signatures once verified.

## Out of scope (do NOT do these)

- **No physics wiring for ShotMode or Spin.** `ShotInputBuilder.cs` and `BallSimulation.cs` stay untouched. ShotMode and Spin contexts are state-bus + visuals only in v1.
- **No selection-state visual variants** for the buttons. Default Unity Button color tint on press is acceptable.
- **No hold-and-slide gesture** in v1 — tap-to-open + tap-to-select is the bar. File hold as polish if Cesar requests later.
- **No continuous-drag dot** in the Spin panel — five cardinal positions only.
- **No new methods on `ClubManager` / `BagManager` / `BallManager` / `ClubDatabaseCSV` / `BallDatabaseCSV`.** Surface to Architect if a method is missing.
- **No menu-screen or main-flow hookup** — LabScaffold only.
- **Do NOT modify `Golfin.Gameplay.UI.asmdef`** — references are correct as-is per Blueprint §2.

## Stop conditions

- Functional: 2 attempts max for any single button widget compile cycle. Surface if button N fails to compile twice.
- Visual: 5 rounds max for the layout-fidelity diff. Surface with side-by-side after round 5 if not visually 1:1.
- API mismatch: surface immediately if any manager method assumed by this spec doesn't exist as written.
