# SPEC — `8_3_topbar`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Iteration 3 patches (2026-04-28, after architect PASS but Cesar review found 2 polish issues)

Iteration 2 landed cleanly through all three pipeline stages, but Cesar's manual review caught two issues. These override anything earlier in the doc.

### Fix 5 — Portrait and HoleMap frames need rounded corners (radius 8)

Figma has `cornerRadius: 8` on both `In-game Portrait` containers. Iteration 2 omitted this (Unity Image has no native cornerRadius). The current frames are sharp 180×180 squares; should be 180×180 with 8px rounded corners.

**Approach:** use a `Mask` component on `PortraitContainer` and `HoleMapContainer` with a rounded-rect sprite. Steps:

1. Create or import a rounded-rect sprite asset:
   - Path: `Assets/Art/UI/RoundedRect_R8.png`
   - Size: 32×32 white square with 8px corner radius (transparent corners)
   - Import settings: `Sprite Mode = Single`, `Mesh Type = Tight`, set 9-slice borders to 8px on all sides via Sprite Editor (so it scales without distorting corners at 180×180)
   - If creating from scratch is too heavy: alternatively use Unity's built-in `UI/Skin/UISprite` (a 9-sliced rounded sprite that ships with Unity). Acceptable v1 fallback.

2. On `PortraitContainer` and `HoleMapContainer`:
   - Add a `Mask` component
   - The container's existing `Image` (which currently has no sprite) gets the rounded-rect sprite assigned, with `Image Type = Sliced`
   - The `Mask.showMaskGraphic` defaults to `true` — leave it so the rounded shape itself is visible (acts as the frame outline)
   - Children (`RarityBackground`, `Portrait`, `HoleMapBackground`, `HoleMap`) now render only inside the rounded shape

3. Verify: corners of the visible portrait/holemap frame should be rounded with radius 8 in the screenshot.

If the implementer hits issues with mask + sprite combinations, fallback path: create the rounded shape as a single Image with the rounded-rect sprite tinted to the rarity color, and put the portrait sprite ON TOP with the same rounded-rect sprite as a `Mask`. Document the chosen approach in the report.

### Fix 6 — Chips touch at center, need visible gap

In Unity the two chip stacks meet near the center of the screen with no breathing room. Figma has a clear ~118px gap between them. Cesar wants a visible center gap restored. Note: this is a tonight-only stopgap; the true root cause (Unity-vs-Figma size mismatch, ~1.20×) is being investigated separately tomorrow under `Docs/Specs/Queued/FIGMA_UNITY_SIZE_MISMATCH.md`. Don't try to make this match Figma exactly tonight.

**Change:** shorten `ChipStack` width from 298 to **248** (= 50px less per stack, 100px less total at center). Position the slack so it opens on the screen-center-facing side of each stack.

- **Player ChipStack:** `anchoredPosition = (180, -10)`, `SizeDelta = (248, 160)`. Same anchor `(0, 1)`, pivot `(0, 1)`. Was 298 wide; now 248. The 50px less appears between the chip stack's right edge and the player card's right edge (center-facing side).
- **Hole ChipStack:** `anchoredPosition = (50, -10)`, `SizeDelta = (248, 160)`. Same anchor `(0, 1)`, pivot `(0, 1)`. Was at `(0, -10)` 298 wide; now offset 50px from the hole card's left edge, 248 wide. The 50px appears between the hole card's left edge and the chip stack's left edge (center-facing side). HoleMap is unchanged — still flush against the card's right edge.

Individual chip widths (each chip is a child of ChipStack) follow the VLG's width control and become 248px automatically. No per-chip changes needed.

**Updated layout summary:**

```
PlayerCard (RectTransform 478×180, anchor=(0,1), pivot=(0,1), pos=(48,-158))
├── PortraitContainer 180×180 at pos=(0,0)
└── ChipStack 248×160 at pos=(180,-10)   ← width changed: 298 → 248

HoleCard (RectTransform 478×180, anchor=(1,1), pivot=(1,1), pos=(-48,-158))
├── ChipStack 248×160 at pos=(50,-10)    ← was (0,-10) at 298 wide
└── HoleMapContainer 180×180 at pos=(0,0) ← unchanged (anchor right)
```

Text readability: "HOLE 1 - REGULAR" at 248px wide should still fit comfortably (it fit in 298px, and longest-text-vs-width has 50px of margin to spare).

### Updated acceptance checklist (additions for iteration 3)

- [ ] PortraitContainer has rounded corners (radius 8) visible in screenshot
- [ ] HoleMapContainer has rounded corners (radius 8) visible in screenshot
- [ ] PortraitContainer uses Mask component with rounded-rect sprite (or documented fallback approach)
- [ ] Player ChipStack is 248 wide (not 298), positioned at (180, -10)
- [ ] Hole ChipStack is 248 wide (not 298), positioned at (50, -10)
- [ ] Visible center gap between player chip stack right edge and hole chip stack left edge has clearly increased vs iteration 2 screenshot
- [ ] All chip text remains readable (no clipping) at the new 248 width: USERNAME, Lv N, TURN N, LOMOND, HOLE 1 - REGULAR, PAR N

### What's NOT changing this iteration

- Settings position `(-58, -24)` — stays.
- Player chip text alignment `Middle Left` — stays.
- Hole chip text alignment `Middle Right` — stays.
- RarityBackground / HoleMapBackground colors — stay.
- Portrait sprite / HoleMap sprite assignments — stay.
- Card positions, card sizes — stay.

### Deferred to tomorrow's investigation

Cesar identified that Unity's 180×180 portraits are rendering at a Figma-equivalent of ~216×216 — i.e., everything in Unity is bigger than its Figma counterpart by roughly 1.20×. This is NOT the 1170/1080 = 1.083 canvas scale ratio. There's a second factor we don't yet understand. Investigation deferred to a separate task. Do not adjust sizes for this in iteration 3 — we want to fix the root cause once, not patch every spec separately.

---

This is Phase 8.3 redo: top-bar UI for Shot UI in LabScaffold. Player card (left), hole info card (right), settings button (top-right, alone on its own row above the cards). Two prior attempts were rejected (whole pipeline regressed to white boxes / fused chip bars / wrong settings sprite). This spec is the third attempt and is the inaugural use of the new multi-agent pipeline.

## Status

See `STATUS.md`.

## Goal

Render the top-bar HUD elements at the top of the Shot UI canvas in LabScaffold so they match the Figma reference frame `In-Game - Shot Tests 9` (file `5gEAHjl6xAtW8iYY7NMvWd`, page `In-game`, node id `4065:15675`). Three independent widgets:

1. **Player card** — top-left. Portrait + 3-row chip stack (USERNAME, Lv N, TURN N).
2. **Hole card** — top-right (mirror). Hole map thumbnail + 3-row chip stack (LOMOND, HOLE N - REGULAR, PAR N).
3. **Settings button** — own row above the cards, top-right. Single 86×86 white circle with navy gear glyph.

Widgets must render real data when CharacterManager / HoleContext are populated, and real default sprites when they aren't. **No white boxes, ever.**

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` ("Golfin Game Redux", paid plan)
- **Figma page:** `In-game`
- **Figma frame:** `In-Game - Shot Tests 9` (component, id `4065:15675`)
- **Reference PNG:** `Docs/Reference/In-game UI/Initial State.png`
- **Placeholder vs canonical content notes:**
  - Figma chip text values (`USERNAME`, `Lv 13`, `TURN 5`, `LOMOND`, `HOLE 1 - LADY'S`, `PAR 5`) are PLACEHOLDER mockup content. v1 reads real values: real player name (or "PLAYER" if missing), real level via PlayerCharacterData.currentLevel, real turn (always 1 in v1 — no turn system yet), real hole name, "REGULAR" tee for v1 (not LADY'S — Figma's LADY'S was mockup), real par via HoleMetadata.par.
  - "Rarity Background" layer behind portrait IS in the Figma but is so subtle it doesn't read on the PNG. v1 omits the rarity background; flag as polish follow-up.

## Architecture context

- **Asmdef boundaries affected:** Widget code lives in `Golfin.Gameplay.UI`. This asmdef has `autoReferenced: true` which forbids a direct ref to `Assembly-CSharp` (cycle). Use the static-context + populator pattern from Blueprint §1: widgets read from a static context class in `Golfin.Gameplay.UI` namespace; a `MonoBehaviour` populator in `Assets/Scripts/UI/HUD/` (which compiles into `Assembly-CSharp`) subscribes to manager events and writes to the static context.
- **Existing code referenced:**
  - `CharacterManager` (global, `Assets/Scripts/CharacterManager.cs`) — `Instance.GetSelectedCharacterId()`, `GetPlayerCharacter(id) -> PlayerCharacterData`, events `OnCharacterSelected`, `OnRosterChanged`, `OnCharacterLeveledUp`.
  - `CharacterDatabaseCSV` (global, `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs`) — `Instance.GetCharacter(id) -> CharacterDataRuntime` with `characterName`, `characterLastName`, `rarity`, `portraitSprite` (already loaded from `Resources/Portraits/Thumbnails/`).
  - `PlayerCharacterData` — `currentLevel`. Name/portrait/rarity live on the template (`CharacterDataRuntime`).
  - `HoleMetadata` (`Golfin.CourseImport`, `Assets/Scripts/HoleMetadata.cs`) — MonoBehaviour on Hole_XX_Geo scene root. Fields: `holeNumber`, `par`, `championshipYards`. **No tee field**; tee is hardcoded "REGULAR" for v1.
  - `PhysicsLabController.OnHoleLoaded(string sceneName)` (`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`) — fires when a hole scene loads; populator hooks here to refresh `HoleContext`.
- **Existing assets referenced:**
  - Default portrait: `Resources/Portraits/Thumbnails/Camila.png`
  - Default hole map: `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole 1.png`
  - TMP font: `Assets/Fonts/Rubik-VariableFont_wght SDF.asset`
  - Settings gear sprite: `Assets/Art/In-Game UI/Icon - Settings.png` — Code MUST inspect this asset and document in IMPLEMENTER_REPORT whether it's (a) gear glyph only on transparent bg or (b) white circle WITH gear baked in.
- **Manager APIs used:** All read-only. No new manager methods needed.

## Implementation

### Layer 1 — Data plumbing (do this first)

Create or update two static context classes (in `Golfin.Gameplay.UI.HUD` namespace) and two populator MonoBehaviours (compile into Assembly-CSharp):

**`PlayerContext`** (static, `Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerContext.cs` in `Golfin.Gameplay.UI`):

```csharp
namespace Golfin.Gameplay.UI.HUD
{
    public static class PlayerContext
    {
        public static string DisplayName { get; private set; } = "PLAYER";
        public static int Level { get; private set; } = 1;
        public static int Turn { get; private set; } = 1;
        public static UnityEngine.Sprite PortraitSprite { get; private set; }

        public static event System.Action OnChanged;

        public static void Set(string name, int level, int turn, UnityEngine.Sprite portrait)
        {
            DisplayName = name; Level = level; Turn = turn; PortraitSprite = portrait;
            OnChanged?.Invoke();
        }
    }
}
```

**`PlayerContextPopulator`** (MonoBehaviour, `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs`, no asmdef so compiles into Assembly-CSharp):

```csharp
using Golfin.Gameplay.UI.HUD;
using Golfin.Roster;
using UnityEngine;

public class PlayerContextPopulator : MonoBehaviour
{
    void OnEnable() {
        if (CharacterManager.Instance != null) {
            CharacterManager.Instance.OnCharacterSelected += OnCharSelected;
            CharacterManager.Instance.OnCharacterLeveledUp += OnCharLeveledUp;
        }
        Refresh();
    }
    void OnDisable() {
        if (CharacterManager.Instance != null) {
            CharacterManager.Instance.OnCharacterSelected -= OnCharSelected;
            CharacterManager.Instance.OnCharacterLeveledUp -= OnCharLeveledUp;
        }
    }
    void OnCharSelected(string id) => Refresh();
    void OnCharLeveledUp(string id) => Refresh();

    void Refresh()
    {
        var cm = CharacterManager.Instance;
        if (cm == null) { PlayerContext.Set("PLAYER", 1, 1, null); return; }
        var id = cm.GetSelectedCharacterId();
        if (string.IsNullOrEmpty(id)) { PlayerContext.Set("PLAYER", 1, 1, null); return; }

        var pc = cm.GetPlayerCharacter(id);
        var template = CharacterDatabaseCSV.Instance != null ? CharacterDatabaseCSV.Instance.GetCharacter(id) : null;

        string name = template != null ? (template.characterName ?? "PLAYER") : "PLAYER";
        int level = pc != null ? pc.currentLevel : 1;
        int turn = 1; // v1: no turn system yet
        Sprite portrait = template != null ? template.portraitSprite : null;
        PlayerContext.Set(name, level, turn, portrait);
    }
}
```

**`HoleContext`** (already exists from attempt 1; verify or recreate):

```csharp
namespace Golfin.Gameplay.UI.HUD
{
    public static class HoleContext
    {
        public static string CourseName { get; private set; } = "LOMOND";
        public static int HoleNumber { get; private set; } = 1;
        public static string TeeName { get; private set; } = "REGULAR";
        public static int Par { get; private set; } = 4;
        public static UnityEngine.Sprite HoleMapSprite { get; private set; }

        public static event System.Action OnChanged;

        public static void Set(string course, int hole, string tee, int par, UnityEngine.Sprite map)
        {
            CourseName = course; HoleNumber = hole; TeeName = tee; Par = par; HoleMapSprite = map;
            OnChanged?.Invoke();
        }
    }
}
```

**`HoleContextPopulator`** — subscribe to `PhysicsLabController.OnHoleLoaded` (Code: verify this event exists; if not, add it OR have the populator scan for `HoleMetadata` on scene-loaded events). Read `HoleMetadata.holeNumber` and `par`. Course name "LOMOND" hardcoded for v1 (single-course MVP). Tee "REGULAR" hardcoded for v1. Hole map sprite: load by convention `Resources/HoleMaps/Lomond - Hole {n}.png` OR fall back to widget's `_defaultHoleMap` slot.

### Layer 2 — Widget hierarchy and layout

Build three widgets under `ShotUI_Canvas` (1170×2532 reference resolution).

**Hierarchy and exact RectTransform values:**

```
PlayerCard (RectTransform 478x180, anchor=(0,1), pivot=(0,1), pos=(48,-158))
├── Portrait (RectTransform 180x180, anchor=(0,1), pivot=(0,1), pos=(0,0), Image with cornerRadius 8)
└── ChipStack (RectTransform 298x160, anchor=(0,1), pivot=(0,1), pos=(180,-10), VerticalLayoutGroup)
    ├── UsernameChip (Image solid navy + TMP child, Layout Element prefHeight=48)
    ├── LevelChip
    └── TurnChip

HoleCard (RectTransform 478x180, anchor=(1,1), pivot=(1,1), pos=(-48,-158))
├── ChipStack (RectTransform 298x160, anchor=(0,1), pivot=(0,1), pos=(0,-10), VerticalLayoutGroup)
│   ├── CourseChip
│   ├── HoleChip
│   └── ParChip
└── HoleMap (RectTransform 180x180, anchor=(1,1), pivot=(1,1), pos=(0,0), Image with cornerRadius 8)

Settings (RectTransform 86x86, anchor=(1,1), pivot=(1,1), pos=(-106,-24), Button)
├── BackgroundCircle (Image, white circle sprite, anchored stretch-stretch)
└── GearIcon (Image, gear glyph 63x65, centered) — only if Icon - Settings.png is glyph-only
```

**`VerticalLayoutGroup` on each `ChipStack`:** `Padding=0`, `Spacing=8`, `Child Alignment=Upper Left`, `Control Child Size: Width=true, Height=false`, `Use Child Scale=false`, `Child Force Expand: Width=true, Height=false`.

**Chip styling:** Root Image with `Color=#001E39` (navy `r:0, g:0.118, b:0.224`), `Sprite=None` (Unity uses default UI sprite for solid color), `Image Type=Simple`. NO 9-slice. NO corner radius. Just a flat navy rect 298x48. `Layout Element { Preferred Height=48 }`.

**TMP child of each chip:** `RectTransform` stretch-stretch with `Left=10, Right=10, Top=0, Bottom=0`. `Text Wrapping=Disabled`. Font asset: `Rubik-VariableFont_wght SDF`. **Font Style: Bold** (matches Figma's Rubik Medium converted; Code: verify weight visually). Font size 23. Color white. **Alignment: Middle Right** for BOTH cards (player AND hole — both right-aligned in the Figma).

**Settings button structure** depends on whether `Icon - Settings.png` is (a) glyph-only or (b) bg-and-glyph:

- (a) glyph-only → use the structure above (`BackgroundCircle` + `GearIcon`). `BackgroundCircle` uses Unity's built-in `UI/Skin/Knob` sprite or any 1-px white sprite + a circular sprite mask. `GearIcon` 63×65 centered, navy tint if PNG is grayscale.
- (b) bg-and-glyph → just one Image with this sprite, no child. Skip the `BackgroundCircle`/`GearIcon` split.

**Code MUST inspect `Icon - Settings.png` and document which case in IMPLEMENTER_REPORT.**

### Layer 3 — Bind data to widgets

**`PlayerCardWidget` MonoBehaviour** in `Golfin.Gameplay.UI.HUD` namespace. Subscribe to `PlayerContext.OnChanged` in `OnEnable`, unsubscribe in `OnDisable`, refresh on enable. Inspector slots:

```csharp
[SerializeField] Image _portrait;
[SerializeField] TextMeshProUGUI _usernameText;
[SerializeField] TextMeshProUGUI _levelText;
[SerializeField] TextMeshProUGUI _turnText;
[SerializeField] Sprite _defaultPortrait; // wire to Camila.png in Inspector
```

Refresh logic:
```csharp
_portrait.sprite = PlayerContext.PortraitSprite ?? _defaultPortrait;
_usernameText.text = PlayerContext.DisplayName.ToUpperInvariant();
_levelText.text = $"Lv {PlayerContext.Level}";
_turnText.text = $"TURN {PlayerContext.Turn}";
```

**`HoleCardWidget`** mirrors structure. Slots: `_holeMap`, `_courseText`, `_holeText`, `_parText`, `_defaultHoleMap`. Refresh:
```csharp
_holeMap.sprite = HoleContext.HoleMapSprite ?? _defaultHoleMap;
_courseText.text = HoleContext.CourseName.ToUpperInvariant();
_holeText.text = $"HOLE {HoleContext.HoleNumber} - {HoleContext.TeeName}";
_parText.text = $"PAR {HoleContext.Par}";
```

**`SettingsButton`** — minimal MonoBehaviour with `Button.onClick` wired to log "Settings clicked" for v1. No actual settings menu yet.

### Placeholder rule (CRITICAL — this is what attempts 1 and 2 violated)

**No white boxes, ever.** Wire `_defaultPortrait` to `Camila.png` and `_defaultHoleMap` to `Lomond - Hole 1.png` IN THE INSPECTOR before reporting done. The widget must render a real image even if the populator never fires, even in edit mode.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. The Implementer cannot mark the task done without filling every line. The self-reviewer will reject any report with unfilled or unjustified checklist items.

- [ ] Settings is on its OWN row at top (Y=24, Y-bottom=110), NOT on the cards row
- [ ] Settings is a single 86×86 white circle with navy gear (~63×65) centered inside it
- [ ] Settings position: anchored top-right with anchoredPosition (-106, -24)
- [ ] Player card RectTransform is 478×180 at anchoredPosition (48, -158) with anchor=(0,1) pivot=(0,1)
- [ ] Hole card RectTransform is 478×180 at anchoredPosition (-48, -158) with anchor=(1,1) pivot=(1,1)
- [ ] Both cards are 48px from their respective screen edges (verify by inspecting RectTransform values)
- [ ] Cards row top edge starts at Y=158 (BELOW the settings row, with ~24px gap)
- [ ] Portrait is 180×180 with cornerRadius 8, dominates the player card; chip stack is 298×160 next to it
- [ ] Hole map is 180×180 with cornerRadius 8, dominates the hole card; chip stack is 298×160 next to it
- [ ] Chip stack offset 10px from card top (vertically near-centered with 10px slack top+bottom)
- [ ] Chips are flat navy `#001E39` rectangles, 298×48 each, no sprite, no corner radius
- [ ] Chip text right-aligned on BOTH cards (Middle Right)
- [ ] Chip text font is Rubik-VariableFont_wght SDF (Medium-equivalent weight), size 23, color white
- [ ] Chip text values readable end-to-end (no clipping): USERNAME, Lv N, TURN N, LOMOND, HOLE 1 - REGULAR, PAR N
- [ ] Portrait visible (real sprite — Camila or whoever is selected — NOT a white box)
- [ ] Hole map visible (real sprite — Hole 1 — NOT a white box)
- [ ] Player card Lv shows actual level from PlayerCharacterData (not hardcoded "Lv 1" unless that's the actual selected character's level)
- [ ] Settings gear color matches reference (navy `#001E39`)
- [ ] No white-box placeholders visible anywhere in the screenshot
- [ ] All `[SerializeField]` references wired in the Inspector (no missing-reference warnings)
- [ ] Unity Console has no errors related to this task
- [ ] `_defaultPortrait` wired to `Camila.png` in Inspector
- [ ] `_defaultHoleMap` wired to `Lomond - Hole 1.png` in Inspector
- [ ] `Icon - Settings.png` asset inspection documented in report (case (a) or (b))
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerContext.cs` — create (in Golfin.Gameplay.UI asmdef)
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` — verify or create
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/PlayerCardWidget.cs` — create (revise existing if from attempt 1)
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleCardWidget.cs` — verify or create
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/SettingsButton.cs` — verify or create
- `Assets/Scripts/UI/HUD/PlayerContextPopulator.cs` — create (compiles into Assembly-CSharp)
- `Assets/Scripts/UI/HUD/HoleContextPopulator.cs` — verify or create
- `Assets/Scenes/LabScaffold.unity` — modify scene hierarchy: add the three widgets under `ShotUI_Canvas` with the RectTransform values above; wire SerializeField slots; add the two Populator MonoBehaviours to a `_HUD` GameObject in the scene

## Out of scope (do NOT do these)

- Rarity background behind portrait — flagged as polish follow-up, not in v1
- Settings actually opening a menu — v1 just logs on click
- Tee selection UI — v1 hardcodes "REGULAR"
- Turn system — v1 hardcodes turn=1
- Multi-course support — v1 hardcodes "LOMOND"
- Touching the existing cone, power gauge, or club handle widgets — those were authored against 1080-wide canvas and are tolerated as-is for 8.3
- Setting `Indicator - Wind-Hole.png` 9-slice borders — that sprite is no longer used for chips
