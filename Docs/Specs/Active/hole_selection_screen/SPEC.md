# SPEC — `hole_selection_screen` — Hole Selection Screen (Lomond, 18 holes, expandable cards)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state.

## Goal

A new full-screen `HoleSelection` screen reachable by tapping the centre **Tee** button on the persistent bottom nav (the `mainPlayButton` on `PersistentUIManager`, which currently logs "not yet implemented"). The screen presents Lomond Country Club's 18 holes as a vertically scrolling list of "hole cards". Each card has three states:

- **Collapsed** (default) — title, subtitle, three reward chips. ~284 px tall.
- **Expanded** — collapsed content + separator + hole image + Lomond strategy description + separator + reward chips + separator + PLAY/REPLAY button. ~820 px tall.
- **Locked** — always collapsed, dimmed, not interactable.

Tapping a collapsed card expands it (and centres it in the scroll viewport). Tapping an expanded card collapses it. **Only one card can be expanded at a time** — expanding card B auto-collapses card A. Tapping a locked card does nothing. The PLAY/REPLAY button on the expanded card opens the existing `MatchmakingModalController` with that hole's index. Holes the player has already played show **REPLAY** (with `replayRewards`); holes the player has not yet played show **PLAY** (with `rewards`). On first run only Hole 1 is unlocked-and-unplayed; Holes 2–18 are locked. Played-state and lock-state are inspector-tunable for testing because no save system exists yet (per `📌 NEXT — Controls finetuning`'s gating, save state is Loop v2 territory).

Two filter rows above the cards (Country Club / Tee) are **visual placeholders** in this task — they render exactly per Figma but click-to-filter is out of scope (follow-up spec). Filters appear at exact Figma positions/styling; the count fragments (`28/72`, `10/18`, etc.) are hardcoded literal strings.

This is the second off-roadmap Mac-environment task in a row, intended to close the menu-side gap before items C/D land. It does NOT touch any physics, save state, or networking code.

## Reference

- **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` (Golfin Game Redux), page **"Hole Selection Screen"** (id `12885:87551`).
  - **Frame "Hole Selection - Next"** id `12961:1694` (1170×2532). Use this as the **default screen layout**.
  - **Frame "Hole Selection - Replay"** id `12885:89895` (1170×2532). Use the **expanded card** in this frame (id `12885:90963`) as the canonical expanded-card reference. The "Next" frame doesn't show an expanded state — use the Replay frame's expanded card with the title/button label swapped per `Mode` (see Implementation §3).
- **Reference PNGs (Implementer captures these from Figma at start of work, 1170×2532 PNG):**
  - `Docs/Specs/Active/hole_selection_screen/screenshots/figma-next.png` — full Next frame (`12961:1694`).
  - `Docs/Specs/Active/hole_selection_screen/screenshots/figma-replay.png` — full Replay frame (`12885:89895`).
  - `Docs/Specs/Active/hole_selection_screen/screenshots/figma-expanded-card.png` — the expanded card alone (`12885:90963`).
  - `Docs/Specs/Active/hole_selection_screen/screenshots/figma-collapsed-card.png` — a collapsed card alone (`12961:1728`).
- **Canvas reference:** 1170 × 2532, Match=0. **1 Figma px = 1 Unity unit.** No conversion factor (`Docs/Architecture/RUNTIME_BLUEPRINT.md` §1).
- **Lomond official site:** `https://www.lomond-cc.com/course/` for the per-hole table (par, handicap, yardages — already extracted in Step 1). Per-hole strategy GIFs at `https://www.lomond-cc.com/wp-content/themes/templateB/images/course_eNN.gif` for `NN` in `01..18`. **Implementer captures these in Step 1.5.**

### Placeholder vs canonical content notes

- **Card title (mode-dependent):** Expanded card top text is "REPLAY HOLE" (Replay mode) or "PLAY HOLE" (Play mode). 45 px Rubik SemiBold, silver gradient. The mode is determined per-card by `HoleProgressionService.HasPlayed(holeNumber)`.
- **Card subtitle:** Always `Lomond Country Club - Hole {N} - Par {P}`. 39 px Rubik SemiBold, white. (Pulled from `HoleData.courseNameKey` localized + `holeNumber` + `par`.)
- **Filter pills:** Static labels matching Figma exactly. No click logic (see "Out of scope").
  - Row 1 (Course): `LOMOND 28/72` (active, gold `#EBD170`) | `YAITA - KIKYOU` (locked, silver gradient, lock icon to left of label).
  - Row 2 (Tee): `LADIES 18/18` (active, white) | `FRONT 10/18` (active, gold `#EEDC9A`) | `REGULAR 0/18` (locked, silver gradient, lock icon) | `BACK 0/18` (locked, silver gradient, lock icon).
  - Lock icon: same Figma "Vector" used in the filter rows (id e.g. `12961:1707`, `12961:1720`). Implementer should export this as a `Sprite` (call it `Filter_Lock_Icon`) and assign to the locked-pill prefab variant.
- **Hole image:** **Single combined image per hole**, replacing the Figma's separate `Hole 1 - Green 1` + `Hole 1 - Map 2` + dotted-path Ellipse cluster. The combined image fills the Tutorial frame's left half (749 × 288 area in Figma). For Hole 1, use the asset Cesar designated; for Holes 2–18, use a screamingly-obvious magenta placeholder (see Implementation §4 "Hole image asset convention").
- **Description text:** Per-hole strategy text **captured from the Lomond official site `course_eNN.gif` images, OCR'd from Japanese, then translated to English by Architect**. Stored as a localization key in `HoleData.descriptionKey`. **Captured in Step 1.5 — descriptions ship with real text on day one.**
- **Rewards (mode-dependent):** Up to 3 reward chips per card. Play mode reads `HoleData.rewards`; Replay mode reads `HoleData.replayRewards`. Both lists are 0–3 entries. Empty rewards are deactivated. Icon mapping is identical to `HomeScreenController.SetupRewardRow` — Points / RepairKit / Ball sprites, `xN` amount text.

## Architecture context

**Asmdef boundaries affected:**
- `Assembly-CSharp` only. All new types live in this assembly. No new asmdefs.
- `HoleSelectionScreenController` and `HoleCardController` go in a new namespace `GolfinRedux.UI.HoleSelection`.
- `HoleProgressionService` is a tiny POCO singleton (no MonoBehaviour) in namespace `GolfinRedux.UI.HoleSelection` — no asmdef impact.
- The Editor auto-wire script lives in `Assets/Scripts/UI/HoleSelection/Editor/` and is `#if UNITY_EDITOR`-gated, mirroring `MatchmakingModalAutoWire.cs`.

**Existing code referenced (do NOT modify the bodies; only the call sites listed in "Existing code modified" below):**
- `Golfin.UI.PersistentUIManager.NavigateTo(Screen.MainPlay)` — the entrypoint we retarget. Currently in the `default` switch arm logging "not yet implemented".
- `GolfinRedux.UI.ScreenManager` — owns the `ScreenId` enum and the `ApplyScreen(ScreenId)` switch. We add `HoleSelection` to the enum and add a SerializeField + arm to ApplyScreen.
- `GolfinRedux.UI.HoleData` — extended (see Implementation §1).
- `GolfinRedux.UI.HoleDatabase` — unchanged shape; just gets more entries in its `holes` list.
- `GolfinRedux.UI.HoleDatabaseLoader` — extended parsing for new CSV columns (see Implementation §1).
- Editor importer at `Assets/Editor/HoleDatabaseImporter.cs` — extended parsing for new CSV columns.
- `Localization.LocalizationManager.Get(string key)` — used for `courseNameKey` and `descriptionKey`. Missing keys return the key string itself, which is acceptable for the placeholder period.
- `Golfin.UI.Matchmaking.MatchmakingModalController.Open(int holeIndex)` — invoked when the user taps PLAY/REPLAY. Already exists and works.

**Manager APIs added (NEW):**
- `GolfinRedux.UI.HoleSelection.HoleProgressionService.Instance` — POCO singleton, lazy-initialized. Public surface:
  - `bool IsUnlocked(int holeNumber)` → `holeNumber == 1` by default; otherwise reads inspector overrides.
  - `bool HasPlayed(int holeNumber)` → `false` by default; otherwise reads inspector overrides.
  - `void SetUnlockedOverride(int holeNumber, bool unlocked)` — for inspector + tests.
  - `void SetPlayedOverride(int holeNumber, bool played)` — for inspector + tests.
- A companion MonoBehaviour `HoleProgressionDebug` (in same namespace) exposes inspector arrays so Cesar can flip lock/played states from the Unity Inspector at edit-time and they're applied to the singleton at `Awake()`. One instance lives on `ShellSceneRoot` next to `ScreenManager`.

**Existing assets referenced:**
- `Assets/Data/HoleDatabase.csv` — extended in place (new columns).
- `Assets/Data/HoleDatabase.asset` — re-imported via `GOLFIN > Import Holes from CSV` after CSV is updated.
- `Assets/Resources/Sprites/RewardIcon_Points.png` / `RepairKit.png` / `Ball.png` — already wired in `HomeScreenController` "Reward Icons" header. Implementer pulls the same three sprite references and assigns them to `HoleCardController`'s SerializeField slots.

## Implementation

### Step 0 — reference walk-through (Implementer reads before coding)

1. Capture the four Figma reference PNGs listed in "Reference → Reference PNGs" above and save to `Docs/Specs/Active/hole_selection_screen/screenshots/`.
2. Open `Assets/Scenes/ShellScene.unity` in the editor. Confirm `HomeScreen` and `RosterScreen` already exist as child GameObjects of the Canvas. There's no `HoleSelectionScreen` GameObject yet — Implementer creates it (Step 5).
3. Open `Assets/Prefabs/UI/HomeScreen.prefab` and locate the `NextHole` GameObject. Note the gradient/border styling — the cards in this task reuse the same gradient palette (`#133453` → `#091B33` background, white 3 px border, 50 px corner radius, drop shadow `0px 10px 10px rgba(0,0,0,0.4)`). Don't copy the prefab; just reference its visual conventions.
4. Read `MatchmakingModalController.Open(int holeIndex)` to confirm the entrypoint signature (already used by `HomeScreenController`).

### Step 1 — Extend `HoleData` + CSV pipeline

Edit `Assets/Scripts/UI/HoleData.cs`. Add four new fields to `HoleData` (do NOT modify any existing field or method):

```csharp
public int par;
public string descriptionKey;             // Localization key for the strategy text (e.g. "HOLE_LOMOND_1_DESC")
public string holeImageName;              // Name of the combined hole+green image in Resources/HoleImages/ (e.g. "Hole_01")
public List<HoleReward> replayRewards = new();   // Rewards shown when REPLAY button is shown (i.e. hole already played)
```

The existing `rewards` list now explicitly means **"Play rewards"** (shown when the player has not yet played this hole). The existing `AddReward` method continues to write to `rewards`. Add a parallel method:

```csharp
public void AddReplayReward(RewardType type, int amount)
{
    replayRewards.Add(new HoleReward(type, amount));
}
```

Edit `Assets/Data/HoleDatabase.csv`. New header (replaces the existing one):

```
courseNameKey,holeNumber,par,descriptionKey,holeImageName,windSpeedMph,windDirectionDegrees,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount,replayReward1Type,replayReward1Amount,replayReward2Type,replayReward2Amount,replayReward3Type,replayReward3Amount
```

Populate **all 18 Lomond holes** with par + yardage info (yardage is metadata for now — only par is rendered in this task, but capture the data while we have it; future Hole Detail screen will use it). Use the par values from the official Lomond table (verified at `https://www.lomond-cc.com/course/`, copied below for convenience):

| Hole | Par | Hole | Par |
|---|---|---|---|
| 1  | 5 | 10 | 4 |
| 2  | 4 | 11 | 3 |
| 3  | 4 | 12 | 4 |
| 4  | 3 | 13 | 5 |
| 5  | 4 | 14 | 4 |
| 6  | 3 | 15 | 3 |
| 7  | 4 | 16 | 4 |
| 8  | 5 | 17 | 4 |
| 9  | 4 | 18 | 5 |

Total = 72.

For each hole, emit a row with:
- `courseNameKey` = `HOLE_LOMOND_{N}` (e.g. `HOLE_LOMOND_1`).
- `holeNumber` = N.
- `par` = value from the table above.
- `descriptionKey` = `HOLE_LOMOND_{N}_DESC` (e.g. `HOLE_LOMOND_1_DESC`).
- `holeImageName` = `Hole_{N:D2}` (e.g. `Hole_01`, `Hole_02`, …, `Hole_18`).
- `windSpeedMph`, `windDirectionDegrees` — preserve existing values for Holes 5 and 6 (1.5/45 and 2.2/90 respectively, from the current CSV); use 0/0 for the other 16 rows.
- Reward columns — for Hole 5 and Hole 6, preserve the existing reward set as the **Play reward** (Hole 5 = Points 100 / RepairKit 10 / Ball 30; Hole 6 = Points 200 / RepairKit 30). For all other 16 holes, default Play rewards = `Points 100, RepairKit 10, Ball 5`.
- Replay reward columns — for **all 18 holes**, default Replay rewards = `Points 50, RepairKit 5, Ball 2`. (Halved Play rewards is a reasonable starting differential; Cesar can re-tune from CSV later.)

Drop the existing `HOLE_RIVERSIDE_*` and `HOLE_HIGHLAND_*` rows — they're stubs from a previous iteration and don't fit the Lomond-only model.

Add a `HOLE_LOMOND_{N}` localization-key entry for each hole to `Assets/Resources/Localization/strings_en.csv` (or whichever file the active LocalizationManager loads — Implementer verifies). The course-name key value: `HOLE_LOMOND_{N}` → `Lomond Country Club  - Hole {N}` (note: Figma uses TWO spaces between "Club" and the hyphen — preserve verbatim).

The description-key entries (`HOLE_LOMOND_{N}_DESC`) are added in Step 1.5 once the strategy text is captured.

Edit `Assets/Editor/HoleDatabaseImporter.cs` and `Assets/Scripts/UI/HoleDatabaseLoader.cs` to parse the new column layout. Both files have near-identical parsing blocks; both must be updated. Specifically:

- After the existing `holeNumber` parse, parse `par` (column index 2) as int.
- Parse `descriptionKey` (col 3), `holeImageName` (col 4) as string.
- Wind columns shift to indices 5 and 6 (was 2 and 3).
- Play rewards shift to columns 7–12 (was 4–9): typeIdx = `7 + r*2`, amountIdx = `8 + r*2`.
- Replay rewards live at columns 13–18: typeIdx = `13 + r*2`, amountIdx = `14 + r*2`.
- Replay reward parser uses `hole.AddReplayReward(...)` instead of `AddReward(...)`.

Update the `HelpBox` text in `HoleDatabaseImporter` to reflect the new column layout.

After running the importer the resulting `HoleDatabase.asset` should have exactly 18 entries, in hole-number order.

### Step 1.5 — Capture per-hole strategy text from Lomond website

The Lomond official site has 18 strategy GIFs at `https://www.lomond-cc.com/wp-content/themes/templateB/images/course_eNN.gif` for `NN` in `01..18`. Each GIF is a hole-layout map with **Japanese strategy text overlaid**. Implementer's job: download all 18, OCR the Japanese, save the raw Japanese to a temporary file, and ping Architect. **Architect will then translate the Japanese to English and write back the per-hole English strings as a CSV snippet for Implementer to paste into the localization file.**

Concrete steps:

1. Create `Docs/Specs/Active/hole_selection_screen/lomond-source/` (working dir, NOT shipped).
2. Download all 18 GIFs into that folder using `curl` or equivalent. URLs: `https://www.lomond-cc.com/wp-content/themes/templateB/images/course_e01.gif` … `e18.gif`. Use a User-Agent header that identifies a real browser (default `curl` UA may be blocked); `Mozilla/5.0` works fine. Pace requests (e.g. 1s sleep between) to be polite to the host.
3. OCR each GIF for Japanese text. Implementer's choice of OCR engine — **Tesseract** with Japanese language pack (`tesseract image.gif - -l jpn`) is the default, but if a different tool produces cleaner output for this image style, use that and document the choice in `IMPLEMENTER_REPORT.md`. Save the raw OCR output to `lomond-source/hole_NN_jp.txt` for each hole.
4. Inspect each output file. OCR on stylized overlay text often produces noise (mid-line breaks, mistaken characters, layout bleed). Manually clean each file to a single coherent paragraph of strategy text — drop yardage tables, hole numbers, par labels, and any HUD-like boilerplate. Keep ONLY the prose strategy advice. If a hole's GIF has no strategy text (some Lomond holes may be image-only), mark the file with `[NO_STRATEGY_TEXT]` and leave the field empty.
5. Concatenate all 18 cleaned Japanese strings into a single file `Docs/Specs/Active/hole_selection_screen/lomond-source/all_holes_jp.txt`, formatted as:
   ```
   === Hole 1 ===
   <Japanese strategy text for hole 1>

   === Hole 2 ===
   <Japanese strategy text for hole 2>

   ... etc through Hole 18
   ```
6. Set STATUS.md to `WAITING_ON_ARCHITECT_TRANSLATION` and commit + push. Architect (claude.ai) will read `all_holes_jp.txt`, translate each hole to English, and write back `Docs/Specs/Active/hole_selection_screen/lomond-source/all_holes_en.txt` plus a ready-to-paste localization snippet at `Docs/Specs/Active/hole_selection_screen/lomond-source/desc_keys_en.csv` (in the same row format as the active localization CSV). Architect then sets STATUS.md back to `READY_FOR_IMPLEMENTATION_RESUME`.
7. Implementer pulls, reads `desc_keys_en.csv`, pastes those 18 rows into the active localization CSV (or appends — wherever new keys belong in that file). Verify each `HOLE_LOMOND_{N}_DESC` key resolves at runtime.

**Tone target for translations** (Architect note to self): match the Figma example caption style — compact, second-person-implicit, golf-strategy register. The Figma sample for the Hole 6 card reads *"The tee shot is best aimed at the Sslopping area in the center of the two tiered fairway, where the right side is wide. The landing spot of the second shot is crucial."* Length: roughly 1–3 short sentences per hole. No chest-thumping marketing copy. If a Lomond GIF is text-only flavour ("welcome to the front nine!" type filler), translate faithfully but flag for Cesar's review.

If Step 1.5's OCR step fails entirely (Tesseract refuses the gif format, Japanese pack unavailable, etc.), Implementer falls back to: download the gifs only, save them to `lomond-source/`, set STATUS to `WAITING_ON_ARCHITECT_TRANSLATION` with a note in the implementer report that OCR failed. Architect can then either OCR manually or instruct further.

### Step 2 — `HoleProgressionService` + `HoleProgressionDebug`

Create `Assets/Scripts/UI/HoleSelection/HoleProgressionService.cs`:

```csharp
namespace GolfinRedux.UI.HoleSelection
{
    /// <summary>
    /// Per-hole unlock + played state. POCO singleton — no MonoBehaviour, no DontDestroyOnLoad.
    /// In this task the only writers are the inspector debug component (HoleProgressionDebug)
    /// and tests. When real save state lands (Loop v2), this service becomes the read API
    /// over the save layer; nothing else changes for callers.
    /// </summary>
    public class HoleProgressionService
    {
        private static HoleProgressionService _instance;
        public static HoleProgressionService Instance => _instance ??= new HoleProgressionService();

        private readonly Dictionary<int, bool> _unlockOverrides = new();
        private readonly Dictionary<int, bool> _playedOverrides = new();

        public bool IsUnlocked(int holeNumber)
        {
            if (_unlockOverrides.TryGetValue(holeNumber, out var v)) return v;
            return holeNumber == 1; // default: only Hole 1
        }

        public bool HasPlayed(int holeNumber)
        {
            return _playedOverrides.TryGetValue(holeNumber, out var v) && v;
        }

        public void SetUnlockedOverride(int holeNumber, bool unlocked) => _unlockOverrides[holeNumber] = unlocked;
        public void SetPlayedOverride(int holeNumber, bool played)     => _playedOverrides[holeNumber] = played;
    }
}
```

Create `Assets/Scripts/UI/HoleSelection/HoleProgressionDebug.cs`:

```csharp
[System.Serializable]
public struct HoleProgressionEntry
{
    public int holeNumber;
    public bool unlocked;
    public bool played;
}

namespace GolfinRedux.UI.HoleSelection
{
    /// <summary>
    /// Inspector debug surface for HoleProgressionService.
    /// Lives on ShellSceneRoot. At Awake() it pushes its overrides into the service.
    /// REMOVE or no-op once real save state lands (Loop v2).
    /// </summary>
    public class HoleProgressionDebug : MonoBehaviour
    {
        [SerializeField] private List<HoleProgressionEntry> overrides = new();

        private void Awake()
        {
            foreach (var e in overrides)
            {
                HoleProgressionService.Instance.SetUnlockedOverride(e.holeNumber, e.unlocked);
                HoleProgressionService.Instance.SetPlayedOverride(e.holeNumber, e.played);
            }
        }
    }
}
```

Add one instance of `HoleProgressionDebug` to `ShellSceneRoot` in `ShellScene.unity` (next to `ScreenManager`). Default `overrides` list: empty (so the service uses its built-in defaults).

### Step 3 — `HoleCardController`

Create `Assets/Scripts/UI/HoleSelection/HoleCardController.cs`. Namespace `GolfinRedux.UI.HoleSelection`. MonoBehaviour, no inheritance.

```csharp
public enum HoleCardState { Collapsed, Expanded, Locked }
public enum HoleCardMode  { Play, Replay }
```

Inspector fields (all `[SerializeField]`):

**Layout containers (one per state — controller toggles `SetActive`):**
- `RectTransform rootRect` — the card's own RectTransform (for height calculation by parent's auto-layout).
- `GameObject collapsedContainer` — vertical layout group containing the collapsed sub-tree (title / subtitle / rewards row).
- `GameObject expandedContainer` — vertical layout group containing the expanded sub-tree (everything from collapsed PLUS image+description+button).

**Title + subtitle (referenced from BOTH containers — Implementer can either duplicate the TMP nodes per state or share a single nested layout. Simpler is to have separate nodes per container so the layouts are independent; auto-wire writes to all four references):**
- `TextMeshProUGUI titleTextCollapsed` — empty in collapsed state (the Figma's collapsed card uses the same area for "REPLAY MISSION" / "PLAY HOLE" subtitle text — see Figma `12961:1728` for the collapsed style. **Note:** the collapsed Figma shows "REPLAY MISSION" + "9 - Risk and Reward - Hole 5" as title+subtitle; for our task replace with `{PLAY|REPLAY} HOLE` + `Lomond Country Club - Hole {N} - Par {P}`).
- `TextMeshProUGUI subtitleTextCollapsed`.
- `TextMeshProUGUI titleTextExpanded`.
- `TextMeshProUGUI subtitleTextExpanded`.

**Hole image + description (expanded only):**
- `Image holeImage` — the combined hole+green image. Set via `Resources.Load<Sprite>($"HoleImages/{holeImageName}")`. If null, fall back to `Resources.Load<Sprite>("HoleImages/Missing")`.
- `TextMeshProUGUI descriptionText` — localized strategy text from `HoleData.descriptionKey`.

**Rewards (one row in collapsed, one row in expanded — duplicated):**
- `GameObject[] collapsedRewardSlots` (length 3) — each slot has a child `Image` for icon and a child `TextMeshProUGUI` for amount.
- `Image[] collapsedRewardIcons` (length 3)
- `TextMeshProUGUI[] collapsedRewardAmounts` (length 3)
- `GameObject[] expandedRewardSlots` (length 3) — same layout, in the expanded container.
- `Image[] expandedRewardIcons` (length 3)
- `TextMeshProUGUI[] expandedRewardAmounts` (length 3)

**Reward icon sprites (assigned in inspector):**
- `Sprite pointsIcon`, `Sprite repairKitIcon`, `Sprite ballIcon` — match the same three sprites used by `HomeScreenController` "Reward Icons" header.

**PLAY/REPLAY button (expanded only):**
- `Button actionButton` — the gradient pill button at the bottom of the expanded card.
- `TextMeshProUGUI actionButtonLabel` — the button's text (`"PLAY"` or `"REPLAY"`).

**Tap interaction:**
- `Button cardTapButton` — covers the card's full area; raises the expand/collapse event. (Implementer can also use a single `Button` on the root with `CanvasGroup` blocking for the action button, but a dedicated full-area Button is simpler.)

**Locked overlay (always-collapsed appearance):**
- `GameObject lockedOverlay` — semi-transparent dark overlay with a centred lock icon. Active only in `HoleCardState.Locked`.

**Public surface:**

```csharp
public int HoleNumber { get; private set; }
public HoleCardMode Mode { get; private set; }
public HoleCardState State { get; private set; }

/// <summary>
/// Raised when this card is tapped. Parent controller decides whether to expand/collapse
/// based on locked status (parent enforces single-expanded invariant).
/// </summary>
public event System.Action<HoleCardController> OnCardTapped;

/// <summary>
/// Raised when the user taps PLAY/REPLAY on the expanded card.
/// Parent forwards to MatchmakingModalController.Open(holeIndex).
/// </summary>
public event System.Action<HoleCardController> OnActionButtonClicked;

/// <summary>
/// Bind a hole's data and initial state. Called once by the parent after instantiation.
/// </summary>
public void Bind(HoleData hole, HoleCardMode mode, HoleCardState state);

/// <summary>
/// Switch state. Caller is responsible for the single-expanded invariant.
/// </summary>
public void SetState(HoleCardState state);
```

`Bind` populates titles, subtitles, rewards (mode determines `rewards` vs `replayRewards`), image, description, action-button label (`PLAY` or `REPLAY`), then calls `SetState(state)`. `SetState` toggles the appropriate containers + lockedOverlay. `Awake` wires `cardTapButton.onClick` → raise `OnCardTapped`, and `actionButton.onClick` → raise `OnActionButtonClicked`.

Behaviour notes:
- In `Locked` state the card visually matches `Collapsed` (same height, same content rendered) **plus** `lockedOverlay` is on AND `cardTapButton.interactable = false`.
- In `Locked` state, the reward chips still render their icons but with reduced alpha (use `0.4f` on the icon `Image.color` and the amount `TextMeshProUGUI.color`).
- The card's preferred height comes from the active container's vertical-layout auto-sizing — the parent scroll-list's `VerticalLayoutGroup` + `ContentSizeFitter` handles repositioning when state changes. No tween in this task; the snap is instantaneous.

### Step 4 — Hole image asset convention

- **Resources path:** `Assets/Resources/HoleImages/Hole_{NN}.png` for N in 01–18, plus `Assets/Resources/HoleImages/Missing.png`.
- **Hole 1:** Use the asset Cesar designated. Implementer downloads from Figma asset URL `https://www.figma.com/api/mcp/asset/1fca825f-161a-42ba-b5b1-140a82f7bb56` (the `Hole 1 - Map 2` image visible on Figma node `12885:90977`), saves as `Assets/Resources/HoleImages/Hole_01.png`, sets Texture Type = `Sprite (2D and UI)`.
- **Holes 2–18:** Use a magenta/cyan placeholder. Implementer creates `Assets/Resources/HoleImages/Hole_02.png` through `Hole_18.png` as **identical copies** of `Missing.png`. The placeholder must be visually unmistakable: solid magenta `#FF00FF` background, 749×288 px, large white text "MISSING IMAGE - HOLE XX" centred. Implementer can generate this via an editor script or Photoshop/equivalent — a one-off task.
- **`Missing.png`:** identical to the per-hole placeholder pattern but text reads "MISSING IMAGE".

This means after this task ships there are 18 placeholder hole images in tree (and one Missing.png fallback). The actual art for each hole is captured by Cesar from the official Lomond website later and replaces the per-hole files, no code change needed.

### Step 5 — `HoleSelectionScreenController` + scene wiring

Create `Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs`. Namespace `GolfinRedux.UI.HoleSelection`. MonoBehaviour.

Inspector fields:

**Top-bar / persistent:**
- (None — top bar and bottom nav are handled by `PersistentUIManager` + `ScreenManager.ApplyScreen` exactly as for Home/Roster/Inventory.)

**Filter rows (visual placeholders, no behaviour):**
- `GameObject filtersContainer` — vertical layout containing the two filter rows.
- (Pills inside are pure visual prefab instances. No serialized references on the controller — they're inert.)

**Cards list:**
- `ScrollRect cardsScrollRect` — vertical-only ScrollRect.
- `RectTransform cardsContent` — the ScrollRect's `content`, has `VerticalLayoutGroup` (spacing `24` from the Figma "gap-[8px]" * 1170/375 ≈ 24 px) + `ContentSizeFitter` (Vertical = Preferred Size).
- `HoleCardController cardPrefab` — the card prefab (NOT the auto-wire scene instance; the prefab asset).

**Matchmaking modal (forwarded from the card):**
- `Golfin.UI.Matchmaking.MatchmakingModalController matchmakingModal` — same singleton-style reference `HomeScreenController` already holds.

**Hole database:**
- `HoleDatabase holeDatabase` — same `Assets/Data/HoleDatabase.asset` reference, falls back to `HoleDatabaseLoader.RuntimeDatabase` when null (mirrors `HomeScreenController.LoadNextHole` pattern).

Lifecycle:

- `OnEnable`:
  1. Resolve database (asset → runtime fallback).
  2. Clear `cardsContent` of any prior children.
  3. For each `HoleData` in the database (sorted by `holeNumber` ascending):
     - Instantiate `cardPrefab` under `cardsContent`.
     - Determine `mode`: `HoleProgressionService.Instance.HasPlayed(hole.holeNumber) ? Replay : Play`.
     - Determine `state`: `!HoleProgressionService.Instance.IsUnlocked(hole.holeNumber) ? Locked : Collapsed`.
     - Call `card.Bind(hole, mode, state)`.
     - Subscribe `card.OnCardTapped += HandleCardTapped`.
     - Subscribe `card.OnActionButtonClicked += HandleActionClicked`.
     - Add to local list `_cards`.
  4. Reset scroll position to top: `cardsScrollRect.verticalNormalizedPosition = 1f`.
- `OnDisable`: unsubscribe all card events; clear `_cards`.

Tap handling (`HandleCardTapped(HoleCardController card)`):

1. If `card.State == Locked`: do nothing (defensive — the card already disables its own tap button, but belt-and-suspenders).
2. If `card.State == Expanded`: collapse it (`card.SetState(Collapsed)`) and return.
3. If `card.State == Collapsed`:
   a. Find the currently-expanded card (if any) in `_cards` and call `SetState(Collapsed)` on it.
   b. Call `card.SetState(Expanded)`.
   c. **Centre the expanded card in the scroll viewport** — start a coroutine that waits one frame (to let the layout group resize), then computes the normalized scroll position to centre `card.rootRect` and applies it to `cardsScrollRect.verticalNormalizedPosition`. Implementation:
      ```csharp
      private IEnumerator CentreCardNextFrame(HoleCardController card)
      {
          yield return null;
          Canvas.ForceUpdateCanvases();
          var content = cardsScrollRect.content;
          var viewport = cardsScrollRect.viewport;
          var cardRt = card.rootRect;

          // Position of card's centre in content-local space, measured from content top.
          float cardCentreFromTop = -cardRt.anchoredPosition.y + cardRt.rect.height * 0.5f;
          float scrollableHeight = content.rect.height - viewport.rect.height;
          if (scrollableHeight <= 0f) yield break;

          float targetCentreFromTop = cardCentreFromTop - viewport.rect.height * 0.5f;
          float normalized = Mathf.Clamp01(1f - targetCentreFromTop / scrollableHeight);
          cardsScrollRect.verticalNormalizedPosition = normalized;
      }
      ```
      No tween — instantaneous snap. (Tweened scroll-to is a polish item for a later spec.)

Action button handling (`HandleActionClicked(HoleCardController card)`):

```csharp
if (matchmakingModal != null)
    matchmakingModal.Open(card.HoleNumber - 1); // HoleData.holeNumber is 1-based; HoleDatabase index is 0-based
else
    Debug.LogWarning("[HoleSelection] No matchmaking modal wired — action button is dead.");
```

(`MatchmakingModalController.Open(int)` takes a hole **index** matching `HoleDatabase.GetHole(int)`. The CSV is sorted by `holeNumber` so `index = holeNumber - 1`.)

### Step 6 — Add `HoleSelection` to `ScreenManager`

Edit `Assets/Scripts/UI/ScreenManager.cs`:

1. In the `ScreenId` enum, add `HoleSelection,` after `Inventory`.
2. Add field: `[SerializeField] private GameObject _holeSelectionScreen;` after `_inventoryScreen`.
3. In `ApplyScreen(ScreenId screenId)`, add a line near the others:
   ```csharp
   if (_holeSelectionScreen != null)
       _holeSelectionScreen.SetActive(screenId == ScreenId.HoleSelection);
   ```
4. In the `showBars` boolean expression, add `|| screenId == ScreenId.HoleSelection`.

### Step 7 — Retarget `PersistentUIManager.NavigateTo(Screen.MainPlay)`

Edit `Assets/Scripts/UI/PersistentUIManager.cs`. In the `NavigateTo(Screen screen)` switch, add a new arm:

```csharp
case Screen.MainPlay:
    sm.ShowScreen(GolfinRedux.UI.ScreenId.HoleSelection);
    break;
```

Place it before the `default:` arm. The `default` arm stays for `Gacha` and `Settings` which still aren't implemented.

The HomeScreen-side `navTeeButton` (which is a separate button living on `HomeScreenController`, currently routed to `ScreenId.Loading` as a TODO) — also retarget it. In `HomeScreenController.Awake()`, change:
```csharp
if (navTeeButton != null) navTeeButton.onClick.AddListener(() => OnNavClicked(ScreenId.Loading));     // TODO: Hole select
```
to:
```csharp
if (navTeeButton != null) navTeeButton.onClick.AddListener(() => OnNavClicked(ScreenId.HoleSelection));
```

(Both Tee buttons — the persistent one and the HomeScreen-internal one — now route to the same place. This is intentional and matches the existing precedent for Home/Inventory/Roster which are also dual-wired.)

### Step 8 — Build the scene + prefab

Two new prefabs + one scene addition:

**Prefab: `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab`**

Build by hand in the Unity editor (this is a UI prefab — the scene authoring tools handle layout cleanly). Hierarchy:

```
HoleCard                          (RectTransform, w=978 (auto from layout), h=auto, anchors top-stretch)
  CollapsedContainer              (VerticalLayoutGroup, padding 24/16/16/16, spacing 16)
    TitleArea
      Title                       (TMP, "REPLAY HOLE", 45px Rubik SemiBold, silver gradient, centred)
      Subtitle                    (TMP, "Lomond Country Club  - Hole 1 - Par 5", 39px Rubik SemiBold, white, centred)
    Separator                     (Image, height 2, white 30% alpha)
    RewardsRow                    (HorizontalLayoutGroup, spacing 32, height 72)
      RewardSlot1                 (HLayout)
        Reward1Icon               (Image, size 42)
        Reward1Amount             (TMP, "x10", 51px Rubik SemiBold, white)
      RewardSlot2                 (same)
      RewardSlot3                 (same)
  ExpandedContainer               (VerticalLayoutGroup, padding 24/16/16/16, spacing 24, INACTIVE by default)
    TitleAreaExp                  (same TMP setup as above — "PLAY HOLE" / "REPLAY HOLE")
      TitleExp
      SubtitleExp
    SeparatorExp1
    Tutorial                      (HorizontalLayoutGroup)
      HoleImage                   (Image, w=438, h=288 — half of expanded width minus padding for the image + half for the description)
      DescriptionText             (TMP, w=500, "The tee shot is best aimed…", 30px Rubik Medium, white)
    SeparatorExp2
    RewardsRowExp                 (same as collapsed RewardsRow)
      RewardSlot1Exp
      RewardSlot2Exp
      RewardSlot3Exp
    SeparatorExp3
    ActionButton                  (Button, gradient sheen, 348×120)
      Label                       (TMP, "PLAY", 66px Rubik SemiBold, dark slate)
  LockedOverlay                   (Image, full-card, semi-transparent dark, INACTIVE by default, contains centred lock Sprite)
  CardTapButton                   (Button, full-card, transparent — covers everything except ActionButton when expanded)
```

Add a `HoleCardController` MonoBehaviour to the root and wire the SerializeField references via the auto-wire script (Step 9).

**Prefab: `Assets/Prefabs/UI/HoleSelection/FilterPill.prefab`** (optional convenience — Implementer can also build the filter pills inline in the scene since they're static)

Six instances appear in the screen: one per filter label. Two visual variants:
- **Active**: gradient or coloured text (gold `#EBD170` / `#EEDC9A` / white per the Figma per-pill)
- **Locked**: silver gradient text + lock icon to the left of the text

Active-vs-locked is currently determined by the inspector value of a SerializeField bool on each pill instance — there's no runtime swap in this task.

**Scene addition: `Assets/Scenes/ShellScene.unity`**

- Add a new GameObject `HoleSelectionScreen` as a child of the same Canvas that hosts `HomeScreen`/`RosterScreen`/`InventoryScreen`.
- `RectTransform`: anchors stretch-stretch, fill the canvas.
- Add a `HoleSelectionScreenController` component.
- Build the layout: top bar area (kept clear — `PersistentUIManager`'s top bar overlays it), filter rows below, ScrollRect filling the remainder, bottom nav area (kept clear — `PersistentUIManager`'s bottom nav overlays it). Visible content area is `48px` from each side per Figma.
- Inside the ScrollRect's `Content`, add a `VerticalLayoutGroup` (spacing 24, padding 0/0/0/0) + `ContentSizeFitter` (Vertical = Preferred Size). Width-stretch so cards take full content width.
- Wire `HoleSelectionScreenController.cardsScrollRect`, `cardsContent`, `cardPrefab`, `matchmakingModal`, `holeDatabase` via the auto-wire script.
- Wire `ScreenManager._holeSelectionScreen` to this new GameObject.
- Default `_holeSelectionScreen.SetActive(false)`.

### Step 9 — Editor auto-wire

Create `Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionAutoWire.cs`. Mirror `Assets/Scripts/UI/Matchmaking/Editor/MatchmakingModalAutoWire.cs` — same `WireTMP` / `WireImage` / `WireButton` / `WireGameObject` helper pattern, same `MenuItem("GOLFIN/Wire/Hole Selection")` registration, same `EditorUtility.DisplayDialog` summary at the end.

Wire targets, three sub-tasks invoked from the same menu item:

**A. Wire the `HoleCard` prefab** (open prefab in isolation, wire fields on the root `HoleCardController`):

| Field | Path on prefab | Component |
|---|---|---|
| `rootRect` | `.` | RectTransform |
| `collapsedContainer` | `CollapsedContainer` | GameObject |
| `expandedContainer` | `ExpandedContainer` | GameObject |
| `titleTextCollapsed` | `CollapsedContainer/TitleArea/Title` | TextMeshProUGUI |
| `subtitleTextCollapsed` | `CollapsedContainer/TitleArea/Subtitle` | TextMeshProUGUI |
| `titleTextExpanded` | `ExpandedContainer/TitleAreaExp/TitleExp` | TextMeshProUGUI |
| `subtitleTextExpanded` | `ExpandedContainer/TitleAreaExp/SubtitleExp` | TextMeshProUGUI |
| `holeImage` | `ExpandedContainer/Tutorial/HoleImage` | Image |
| `descriptionText` | `ExpandedContainer/Tutorial/DescriptionText` | TextMeshProUGUI |
| `collapsedRewardSlots[0..2]` | `CollapsedContainer/RewardsRow/RewardSlot{N}` | GameObject |
| `collapsedRewardIcons[0..2]` | `CollapsedContainer/RewardsRow/RewardSlot{N}/Reward{N}Icon` | Image |
| `collapsedRewardAmounts[0..2]` | `CollapsedContainer/RewardsRow/RewardSlot{N}/Reward{N}Amount` | TextMeshProUGUI |
| `expandedRewardSlots[0..2]` | `ExpandedContainer/RewardsRowExp/RewardSlot{N}Exp` | GameObject |
| `expandedRewardIcons[0..2]` | `ExpandedContainer/RewardsRowExp/RewardSlot{N}Exp/Reward{N}IconExp` | Image |
| `expandedRewardAmounts[0..2]` | `ExpandedContainer/RewardsRowExp/RewardSlot{N}Exp/Reward{N}AmountExp` | TextMeshProUGUI |
| `actionButton` | `ExpandedContainer/ActionButton` | Button |
| `actionButtonLabel` | `ExpandedContainer/ActionButton/Label` | TextMeshProUGUI |
| `cardTapButton` | `CardTapButton` | Button |
| `lockedOverlay` | `LockedOverlay` | GameObject |
| `pointsIcon` / `repairKitIcon` / `ballIcon` | (Sprite assets — pull from `HomeScreenController` instance in scene) | Sprite |

**B. Wire the `HoleSelectionScreen` GameObject in `ShellScene.unity`:**

| Field | Path | Component |
|---|---|---|
| `cardsScrollRect` | `Content/CardsScrollView` | ScrollRect |
| `cardsContent` | `Content/CardsScrollView/Viewport/Content` | RectTransform |
| `cardPrefab` | (Asset) `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab` | HoleCardController |
| `matchmakingModal` | (Scene) `MatchMakingModal` GameObject | MatchmakingModalController |
| `holeDatabase` | (Asset) `Assets/Data/HoleDatabase.asset` | HoleDatabase |
| `filtersContainer` | `Content/Filters` | GameObject |

**C. Wire the new `_holeSelectionScreen` field on `ScreenManager`** to the `HoleSelectionScreen` GameObject (same pattern as `_homeScreen`/`_inventoryScreen` etc.).

Auto-wire dialog reports total fields wired. Implementer aims for 0 failures on a fresh `ShellScene.unity` + `HoleCard.prefab`.

### Step 10 — Smoke test sequence

After wiring + CSV import + Step 1.5 translation round-trip:

1. Open `Assets/Scenes/ShellScene.unity`.
2. Run `GOLFIN > Import Holes from CSV` with `Assets/Data/HoleDatabase.csv` → `Assets/Data/HoleDatabase.asset`. Confirm "Imported 18 holes" dialog.
3. Run `GOLFIN > Wire > Hole Selection`. Confirm dialog reports ≥ 30 fields wired and 0 failures.
4. Enter Play mode. Wait for shell to settle on the Home screen.
5. Tap the centre **Tee** button on the bottom nav (`mainPlayButton` on PersistentUI). Confirm: ScreenManager fades to black, `HoleSelectionScreen` activates, top + bottom bars stay visible.
6. Confirm: filters render exactly per Figma — Row 1 with `LOMOND 28/72` (gold) and `YAITA - KIKYOU` (silver + lock); Row 2 with `LADIES 18/18` / `FRONT 10/18` / `REGULAR 0/18` / `BACK 0/18`. Tapping pills does nothing (no error).
7. Confirm: 18 hole cards render in the ScrollRect, sorted Hole 1 → Hole 18. Hole 1 is collapsed and interactable; Holes 2–18 are locked (dimmed overlay, lock icon centred).
8. Tap Hole 1 card. Confirm: it expands smoothly (no tween needed — instant snap is fine). The expanded card scrolls to the centre of the viewport. Title reads "PLAY HOLE", subtitle reads "Lomond Country Club  - Hole 1 - Par 5", image shows the Hole 1 placeholder, **description reads the real translated Lomond Hole 1 strategy text** (English, captured via Step 1.5), three reward chips show Points x100 / RepairKit x10 / Ball x5, button label reads "PLAY".
9. Tap Hole 1 again — it collapses.
10. Stop Play, edit `HoleProgressionDebug.overrides` on `ShellSceneRoot`: add an entry `{holeNumber=1, unlocked=true, played=true}`. Re-enter Play. Tap Tee → tap Hole 1. Confirm title now reads "REPLAY HOLE", button label reads "REPLAY", reward chips read Points x50 / RepairKit x5 / Ball x2.
11. Tap PLAY (or REPLAY) on Hole 1's expanded card. Confirm: the existing matchmaking modal opens for Hole index 0. After it locks ("OPPONENT FOUND"), tap Cancel — return to Hole Selection screen with Hole 1 still expanded.
12. Stop Play, edit `HoleProgressionDebug.overrides`: add `{holeNumber=2, unlocked=true, played=false}`. Re-enter Play, navigate to Hole Selection. Confirm Hole 2 is now collapsed-and-interactable (no overlay). Expand it. Confirm title reads "PLAY HOLE" with subtitle "Lomond Country Club  - Hole 2 - Par 4" and the description shows the real translated Hole 2 strategy text.
13. Take play-mode screenshots during steps 8, 10 (REPLAY mode), and 11 (matchmaking modal triggered from Hole Selection). Save to `Docs/Specs/Active/hole_selection_screen/screenshots/<timestamp>_<step>.jpg`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. The Implementer cannot mark the task done without filling every line. The self-reviewer will reject any report with unfilled or unjustified checklist items.

**Data layer:**
- [ ] `HoleData` has new fields `par`, `descriptionKey`, `holeImageName`, `replayRewards` exactly as specified; existing fields untouched.
- [ ] `HoleData.AddReplayReward(RewardType, int)` exists and appends to `replayRewards`.
- [ ] `Assets/Data/HoleDatabase.csv` has the new 19-column header and exactly 18 data rows for Lomond Holes 1–18.
- [ ] All 18 par values match the official Lomond table reproduced in Implementation §1.
- [ ] CSV row for Hole 5 preserves wind 1.5/45 and Play rewards Points 100 / RepairKit 10 / Ball 30.
- [ ] CSV row for Hole 6 preserves wind 2.2/90 and Play rewards Points 200 / RepairKit 30.
- [ ] Stub rows `HOLE_RIVERSIDE_*` and `HOLE_HIGHLAND_*` are removed from the CSV.
- [ ] Both `HoleDatabaseImporter.cs` and `HoleDatabaseLoader.cs` parse the new column layout; `HelpBox` text is updated in the importer.
- [ ] After running `GOLFIN > Import Holes from CSV`, `HoleDatabase.asset` contains exactly 18 entries in hole-number order, each with non-empty `descriptionKey` and `holeImageName`, and at least one entry in both `rewards` and `replayRewards`.
- [ ] Localization file has 18 course-name keys (`HOLE_LOMOND_1` through `HOLE_LOMOND_18`) populated in Step 1.

**Lomond strategy text capture (Step 1.5):**
- [ ] All 18 GIFs downloaded from `https://www.lomond-cc.com/wp-content/themes/templateB/images/course_eNN.gif` to `Docs/Specs/Active/hole_selection_screen/lomond-source/`.
- [ ] Per-hole OCR output saved to `lomond-source/hole_NN_jp.txt` and manually cleaned to coherent strategy paragraphs (or marked `[NO_STRATEGY_TEXT]` where the GIF has no text).
- [ ] `lomond-source/all_holes_jp.txt` exists in the expected `=== Hole N ===` format.
- [ ] STATUS.md was set to `WAITING_ON_ARCHITECT_TRANSLATION` and committed to trigger Architect translation.
- [ ] `lomond-source/desc_keys_en.csv` was received from Architect and pasted into the active localization CSV.
- [ ] All 18 `HOLE_LOMOND_{N}_DESC` keys resolve at runtime to non-placeholder English text.

**Progression service:**
- [ ] `HoleProgressionService` exists as POCO singleton; `IsUnlocked(1)` returns true by default; `IsUnlocked(2..18)` returns false by default.
- [ ] `HoleProgressionService.HasPlayed(N)` returns false for all N by default.
- [ ] `HoleProgressionDebug` is on `ShellSceneRoot`; with empty `overrides` the defaults hold.
- [ ] Setting an override entry in inspector for Hole 1 with `played=true` causes `HoleProgressionService.HasPlayed(1)` to return true at runtime.

**Card prefab + controller:**
- [ ] `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab` exists with the hierarchy listed in Implementation §8.
- [ ] `HoleCardController` exists in namespace `GolfinRedux.UI.HoleSelection` with the public surface listed in Implementation §3.
- [ ] `Bind(HoleData, HoleCardMode, HoleCardState)` populates titles, subtitles, image, description, rewards (mode-correct list), action-button label, and final state.
- [ ] `SetState(Collapsed|Expanded|Locked)` correctly toggles `collapsedContainer`/`expandedContainer`/`lockedOverlay` and `cardTapButton.interactable`.
- [ ] In `Locked` state, `cardTapButton.onClick` does NOT raise `OnCardTapped`.
- [ ] In `Locked` state, reward icons + amounts have alpha 0.4.

**Image asset convention:**
- [ ] `Assets/Resources/HoleImages/Hole_01.png` is the Figma `Hole 1 - Map 2` image (asset URL specified in Implementation §4).
- [ ] `Assets/Resources/HoleImages/Hole_02.png` through `Hole_18.png` are 17 magenta-with-text "MISSING IMAGE - HOLE NN" placeholders, 749×288.
- [ ] `Assets/Resources/HoleImages/Missing.png` exists as the fallback.
- [ ] `Resources.Load<Sprite>("HoleImages/Hole_05")` returns the placeholder for Hole 5.
- [ ] When `holeImageName` resolves to a missing sprite, the controller falls back to `Missing.png`.

**Screen controller + scene:**
- [ ] `HoleSelectionScreenController` exists in namespace `GolfinRedux.UI.HoleSelection`.
- [ ] `OnEnable` instantiates exactly one card per `HoleData` in the database, in hole-number order.
- [ ] Single-expanded invariant holds — expanding card B auto-collapses card A.
- [ ] Centre-on-expand: after a card is expanded, its rect-centre is within ±50 px of the ScrollRect viewport centre.
- [ ] Tapping a locked card produces no expand/collapse and no error log.
- [ ] Tapping PLAY on an expanded `Play`-mode card calls `MatchmakingModalController.Open(holeNumber - 1)`.
- [ ] Tapping REPLAY on an expanded `Replay`-mode card calls `MatchmakingModalController.Open(holeNumber - 1)`.

**ScreenManager + nav wiring:**
- [ ] `ScreenId.HoleSelection` exists in the enum.
- [ ] `ScreenManager._holeSelectionScreen` is wired to the in-scene `HoleSelectionScreen` GameObject.
- [ ] `ScreenManager.ApplyScreen(HoleSelection)` activates only `HoleSelectionScreen` and shows the persistent bars.
- [ ] `PersistentUIManager.NavigateTo(Screen.MainPlay)` calls `ScreenManager.ShowScreen(ScreenId.HoleSelection)`.
- [ ] `HomeScreenController.navTeeButton` listener is updated from `ScreenId.Loading` to `ScreenId.HoleSelection`.

**Filters (visual-only):**
- [ ] Filter row 1 shows `LOMOND 28/72` (gold `#EBD170`) and `YAITA - KIKYOU` (silver gradient, lock icon).
- [ ] Filter row 2 shows `LADIES 18/18` (white), `FRONT 10/18` (gold `#EEDC9A`), `REGULAR 0/18` (silver + lock), `BACK 0/18` (silver + lock).
- [ ] Tapping any filter pill does nothing and produces no error log.

**Auto-wire:**
- [ ] `HoleSelectionAutoWire.cs` exists, registered as `GOLFIN/Wire/Hole Selection`.
- [ ] On a fresh `ShellScene.unity` + `HoleCard.prefab`, the auto-wire dialog reports ≥ 30 fields wired and 0 failures.
- [ ] Auto-wire also sets `ScreenManager._holeSelectionScreen` and `HoleSelectionScreenController.matchmakingModal`.

**Smoke test:**
- [ ] All 13 smoke-test steps in Implementation §10 produce the described observation.
- [ ] Three play-mode screenshots saved to `Docs/Specs/Active/hole_selection_screen/screenshots/`.
- [ ] No console errors related to this task during the smoke test.

**General:**
- [ ] No new asmdefs.
- [ ] No `.meta` files renamed.
- [ ] No physics scripts modified.
- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

**Modified:**
- `Assets/Scripts/UI/HoleData.cs` — add four fields + one method.
- `Assets/Data/HoleDatabase.csv` — replace contents (new header + 18 Lomond rows).
- `Assets/Scripts/UI/HoleDatabaseLoader.cs` — extend column parsing.
- `Assets/Editor/HoleDatabaseImporter.cs` — extend column parsing + update HelpBox text.
- `Assets/Scripts/UI/ScreenManager.cs` — add `HoleSelection` to enum + SerializeField + ApplyScreen arm + showBars condition.
- `Assets/Scripts/UI/PersistentUIManager.cs` — add `MainPlay` arm in `NavigateTo`.
- `Assets/Scripts/UI/HomeScreenController.cs` — change `navTeeButton` target screen.
- `Assets/Resources/Localization/strings_en.csv` (or whichever the active LocalizationManager loads) — add 18 course-name keys (Step 1) + 18 description keys (Step 1.5).
- `Assets/Scenes/ShellScene.unity` — add `HoleSelectionScreen` GameObject + `HoleProgressionDebug` component.

**Created:**
- `Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs`
- `Assets/Scripts/UI/HoleSelection/HoleCardController.cs`
- `Assets/Scripts/UI/HoleSelection/HoleProgressionService.cs`
- `Assets/Scripts/UI/HoleSelection/HoleProgressionDebug.cs`
- `Assets/Scripts/UI/HoleSelection/Editor/HoleSelectionAutoWire.cs`
- `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab`
- `Assets/Resources/HoleImages/Hole_01.png` (Figma asset)
- `Assets/Resources/HoleImages/Hole_02.png` … `Hole_18.png` (17 placeholders)
- `Assets/Resources/HoleImages/Missing.png` (fallback)
- `Docs/Specs/Active/hole_selection_screen/lomond-source/` — working dir for the Step 1.5 OCR/translation round-trip (Japanese text + downloaded GIFs). NOT shipped — gitignore-or-delete on task close.

**NOT modified:**
- `MatchmakingModalController.cs` — already supports `Open(int)` from the matchmaking_modal task.
- `HomeScreenController` next-hole panel logic — it still reads `rewards` (= Play rewards), unchanged behaviour.
- Any physics scripts. Any test scripts. Any scene other than `ShellScene.unity`.

## Out of scope (do NOT do these)

- **Functional filtering.** The filter pills are visual-only; clicking does nothing. Filtering the card list by Course or Tee is a separate spec.
- **Pill state changes.** `LOMOND 28/72` doesn't change to `LOMOND 29/72` when the player completes a hole. The counts are hardcoded literal strings matching the Figma.
- **Save state.** Lock and played status come from `HoleProgressionService` which has zero persistence — overrides are inspector-only and reset every domain reload. Real persistence is Loop v2.
- **Hole image art.** Implementer does NOT capture per-hole images from the Lomond website. Holes 2–18 ship as magenta placeholders that scream "missing"; Cesar replaces them later. (Strategy *text* IS captured, in Step 1.5 — that's separate from the *image* art.)
- **Tween / animation.** Card expand/collapse is an instant snap. Scroll-to-centre is also instant (no smooth scroll). Tweened transitions are a polish spec.
- **Hole detail page.** The card's expanded view IS the hole detail. There's no separate `HoleDetail` screen. (The yardage data captured in CSV is for a future Hole Detail screen but is not rendered in this task.)
- **Multi-course support.** Only Lomond is in the CSV. The "YAITA - KIKYOU" filter pill is visual-only; there's no second course's data.
- **Bottom-nav highlight.** When on Hole Selection screen, the existing `PersistentUIManager` highlight system can highlight `mainPlay` or stay on the last screen — Implementer follows whatever the existing highlight system does naturally, no special handling required for this task.
- **Auto-progress on PLAY.** Tapping PLAY just opens the matchmaking modal (existing behaviour). It doesn't mark the hole as played; that hook lands when Loop v1 completes.
- **Loading state for the screen.** Hole list renders synchronously from the database — no spinner needed.
- **Asmdef changes, prefab variants, ScriptableObject inheritance reorgs** — all out of scope.
- **Step 1.5 translation pass.** Implementer captures + OCRs Japanese; Architect translates to English. Implementer does NOT translate the captured Japanese text — even via machine translation — because the strategy register matters and Architect owns tone.

## Open questions for Architect (Implementer fills if blocked)

> Surface here if anything in the spec is genuinely ambiguous. Do NOT silently invent resolutions.

(empty)
