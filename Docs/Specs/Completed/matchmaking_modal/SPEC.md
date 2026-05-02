# SPEC — `matchmaking_modal` — Fake Matchmaking Modal (Mac environment test)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state.

## Goal

Add a fake matchmaking experience that opens when the player taps the Home screen's "Next Hole" Play button. A pre-existing modal prefab (`MatchMakingModal`) becomes a runtime-driven controller that animates a "FINDING OPPONENT…" status with cycling dots, populates the player's portrait once, cycles through other characters on the opponent side as if scanning the matchmaking pool, populates the next-hole info from the same `HoleDatabase` the Home screen reads, and freezes on a final opponent after a configurable delay (default 5 s) by swapping the title to "OPPONENT FOUND". The Cancel button hides the modal and returns the player to the Home screen. **This is also the first end-to-end smoke test of the Mac dev environment.** No real networking, no real opponents, no real reward grant — purely cosmetic state transitions wired to existing data. Off-roadmap; runs in parallel with the gating Controls-finetuning work (item C).

## Reference

- **Figma frame:** `Matchmaking Screen` / id `12865:1095` in file `5gEAHjl6xAtW8iYY7NMvWd` (canonical node id confirmed by Cesar mid-pipeline 2026-05-02; the originally-cited `12813:77056` was an earlier draft that was moved/renamed in Figma).
- **Reference PNG:** `Docs/Specs/Active/matchmaking_modal/screenshots/figma-reference.png` — Implementer takes this from the Figma frame above (1170×2532 viewport, exported as PNG) at start of work, before touching code.
- **Placeholder vs canonical content notes:**
  - "FINDING OPPONENT…" — canonical title text during search; gets swapped to "OPPONENT FOUND" when the search completes.
  - "USERNAME" / "RANK: #233" / "RANK: #200" under the portraits — placeholders. Use random fake values from a small inspector-tunable list (see Implementation §3). **Username max length: 8 chars** to fit the Username TMP rect without clipping (calibrated iter 2; the 8-char cap is the canonical contract for any future fake-username list — `fakeOpponentUsernames` defaults must obey it).
  - Opponent character (Elizabeth at Lv 7 in the Figma) — placeholder; cycles at runtime through the full character roster minus the player's selection.
  - Player portrait/name/level on the left — pull from `CharacterManager.GetSelectedCharacterId()` + `CharacterDatabaseCSV` + `PlayerCharacterData.currentLevel`.
  - Hole label "Lomond Country Club - Hole 5" — pull from `HoleDatabase` at the same index `HomeScreenController` is showing on the Next Hole panel (see Implementation §4).
  - Reward icons + amounts — pull from the same `HoleData.rewards` list `HomeScreenController.SetNextHoleFromData` already reads. **The Figma-shown values `x10 / x10 / x10` are placeholders only.** The runtime/canonical values come from `Assets/Data/HoleDatabase.asset` + `Characters.csv` (Lomond 5 = Points 100 / RepairKit 10 / Ball 30 as of iter 2). Do NOT chase Figma's reward numbers if they disagree with the home-screen / CSV contract.
- **Canvas reference:** 1170 × 2532, Match=0. **1 Figma px = 1 Unity unit.** No conversion factor (`Docs/Architecture/RUNTIME_BLUEPRINT.md` §1).
- **Backdrop alpha:** `0.85` (calibrated iter 3 — the prefab default of `0.5` reads as "too light" against the home-screen background; `0.85` is the canonical value for any future modal that needs to dim a bright/sunset backdrop).
- **Home-screen elements hidden while modal is open:** the modal must hide both `Canvas/ScreensRoot/HomeScreen/NoticePanel` (maintenance notice strip) and `Canvas/ScreensRoot/HomeScreen/NextHolePanel` (the gold PLAY button + reward strip) while showing, restore both on `OnHide`, and additionally restore in `OnDisable` as a safety net. Wired cross-hierarchy by the auto-wire (HomeScreen lives under a different branch from MatchMakingModal). Added per Cesar's mid-iter-3 request 2026-05-02.

## Architecture context

**Asmdef boundaries affected:**
- `Assembly-CSharp` — `MatchmakingModalController` lives here. It needs to read `CharacterManager` (Golfin.Roster), `CharacterDatabaseCSV` (Golfin.Roster), `HoleDatabase`/`HoleData`/`HoleDatabaseLoader` (GolfinRedux.UI), and the existing `ModalController` base (Golfin.UI.Modals). All of those are already Assembly-CSharp side, so no asmdef changes needed.
- No new asmdefs.
- The Editor auto-wire script lives in `Assets/Scripts/UI/Matchmaking/Editor/` and is `#if UNITY_EDITOR`-gated, mirroring `ItemUseModalAutoWire.cs` etc.

**Existing code referenced (do NOT modify the bodies; only the call sites listed in "Existing code modified" below):**
- `Golfin.UI.Modals.ModalController` (`Assets/Scripts/UI/Modals/ModalController.cs`) — base class; we override `OnShow` / `OnHide`. `Show()` and `Hide()` from the base handle the panel + backdrop activation and fade. The base's `closeButton` field is what we wire the Cancel button to.
- **`modalPanel` wiring convention (canonical for inheritors of `ModalController`):** wire `modalPanel` to the modal's **content sub-tree**, NOT to the modal's root GameObject. Reason: `ModalController.Awake()` calls `modalPanel.SetActive(false)` at startup. If `modalPanel` is the root, the controller deactivates itself and any coroutines started later never run. For this prefab, that means `modalPanel = MatchMakingModal/ContentArea`. Architect (2026-05-02) is promoting this into the canonical convention for any future `ModalController` subclass — the root stays active, the content area is what fades in/out. Future modal specs should explicitly pin `modalPanel` to a non-root child.
- `Golfin.Roster.CharacterManager.Instance.GetSelectedCharacterId()` — returns the currently-selected character ID for the player.
- `Golfin.Roster.CharacterManager.Instance.GetPlayerCharacter(string id)` — returns `PlayerCharacterData` (has `.currentLevel`). Used to populate the player card.
- `Golfin.Roster.CharacterDatabaseCSV.Instance.GetAllCharacters()` — returns `List<CharacterDataRuntime>`. Used to build the opponent pool.
- `Golfin.Roster.CharacterThumbnailCard.Initialize(string charId)` — the existing populate path. Works for the **player** card because the player owns themselves. **Does NOT work for opponents** (it errors on the missing `PlayerCharacterData`), so we add a sibling method (see "Existing code modified").
- `GolfinRedux.UI.HoleDatabase` (`Assets/Scripts/UI/HoleDatabase.cs`) — ScriptableObject list of `HoleData`. The asset is `Assets/Data/HoleDatabase.asset`.
- `GolfinRedux.UI.HoleData` (`Assets/Scripts/UI/HoleData.cs`) — `courseNameKey`, `holeNumber`, `rewards`.
- `GolfinRedux.UI.HoleDatabaseLoader.RuntimeDatabase` / `HoleDatabaseLoader.GetHole(int)` — runtime CSV-loaded fallback when no `.asset` is wired.
- `Localization.LocalizationManager.Get(string key)` — used to translate `courseNameKey` (e.g. `"HOLE_LOMOND_5"`) to display text.

**Existing assets referenced:**
- Prefab: `Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab` (guid `2bd69f22d1298854f9d7905d7375fef8`). Already instantiated in `Assets/Scenes/ShellScene.unity` as a root child named `MatchMakingModal` (GameObject `m_IsActive: 0`, fileID-target `8802540626514172154`). Hierarchy:
  ```
  MatchMakingModal              (root; this is the "modalPanel" target)
    BG                          (full-screen 50% black backdrop)
    ContentArea                 (vertical layout group)
      TitleArea
        League                  (button + image — leave passive; not wired in this task)
      InfoArea                  (vertical layout, this is what holds the visible card)
        Status                  (TMP — "FINDING OPPONENT...")
        Portraits               (horizontal layout)
          User1Info             (vertical layout — PLAYER side)
            CharacterThumbnailCardGlowUp  (instance of guid 1feae2335c8842c4aaacde3075ae0e54, RectTransform alias 4139510916288053968)
            Username            (TMP)
            Rank                (TMP — "RANK: #255")
          UserLabel             (TMP — "Vs.")
          User2Info             (vertical layout — OPPONENT side)
            CharacterThumbnailCardGlowUp  (instance, alias 1715581169768446893)
            Username            (TMP)
            Rank                (TMP — "RANK: #255")
        Divider
        HoleTitle               (TMP — "HOLE")
        HoleInfo                (TMP — "Lomond Country Club  - Hole 5")
        Divider
        Rewards                 (horizontal layout)
          Reward Row1
            Reward1Icon         (Image)
            Reward1Amount       (TMP — "x10")
          Reward Row2
            Reward2Icon
            Reward2Amount
          Reward Row3
            Reward3Icon
            Reward3Amount
        Divider
        CancelButton            (Button + Image)
          Text                  (TMP — "CANCEL")
  ```
  All node names verified in the prefab YAML; do not rename or reparent them. The prefab already has correct sizing, fonts, layout groups, and divider sprites — this task is binding behaviour, NOT visual rebuilding.
- The `CharacterThumbnailCardGlowUp` prefab (guid `1feae2335c8842c4aaacde3075ae0e54`) is the same one used by Roster + the Olivia/Elizabeth instances in the matchmaking prefab. It already has a `CharacterThumbnailCard` component on it.
- Reward sprites — already wired on the `Reward1Icon` / `Reward2Icon` / `Reward3Icon` Images in the prefab. Implementer should reuse the same icon swap logic as `HomeScreenController.SetupRewardRow` (Points / RepairKit / Ball → corresponding `Sprite` on the new `MatchmakingModalController`'s reward-icon SerializeField slots, populated from the prefab defaults).

**Manager APIs used:**
- `CharacterManager.Instance.GetSelectedCharacterId()` → `string`
- `CharacterManager.Instance.GetPlayerCharacter(string id)` → `PlayerCharacterData?`
- `CharacterDatabaseCSV.Instance.GetAllCharacters()` → `List<CharacterDataRuntime>`
- `CharacterDatabaseCSV.Instance.GetCharacter(string id)` → `CharacterDataRuntime?`
- `LocalizationManager.Get(string)` → `string`
- `HoleDatabase.GetHole(int)` → `HoleData?` (preferred when a `.asset` reference is wired)
- `HoleDatabaseLoader.GetHole(int)` → `HoleData?` (runtime CSV fallback)

## Implementation

### Step 0 — reference walk-through (Implementer reads before coding)

Open the Figma frame `12813:77056` and take a 1170×2532 PNG export to `Docs/Specs/Active/matchmaking_modal/screenshots/figma-reference.png`. Open `Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab` in the Unity editor and confirm the hierarchy listed above is present. **You are NOT to modify the prefab visually** — fonts, sizes, divider sprites, layout groups are all final. The work is: add one MonoBehaviour + extend one existing class + add one editor auto-wire + 1 line of new wiring on `HomeScreenController`.

### Step 1 — Extend `CharacterThumbnailCard` to support template-only opponents

The current `CharacterThumbnailCard.Initialize(string)` requires `PlayerCharacterData` to exist for the character ID. Opponents in the matchmaking modal aren't owned by the player, so a parallel populate path is needed.

Add a new public method to `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` (do NOT touch any other method):

```csharp
/// <summary>
/// Populate the card from CSV template data only — no PlayerCharacterData required.
/// Used by matchmaking and other UI that displays characters the player doesn't own.
/// Status icons (selected / level-up-ready / stamina) are forced off in this mode.
/// </summary>
public void InitializeFromTemplate(string charId, int displayLevel)
{
    characterId = charId;

    var csvData = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
    if (csvData == null)
    {
        Debug.LogError($"[CharacterThumbnailCard] InitializeFromTemplate: character {charId} not in CSV.");
        return;
    }

    CharacterRarity rarity = csvData.rarity;
    var rarityLabel = RarityHelper.GetRarityLabel(rarity);
    var rarityBadgeTextColor = RarityHelper.GetRarityBadgeTextColor(rarity);

    if (portraitImage != null && csvData.portraitSprite != null)
        portraitImage.sprite = csvData.portraitSprite;

    if (nameText != null)
        nameText.text = csvData.characterName;

    if (rarityBadgeImage != null)
        rarityBadgeImage.enabled = false;

    if (rarityLabelText != null)
    {
        rarityLabelText.text = rarityLabel;
        rarityLabelText.color = rarityBadgeTextColor;
    }

    if (levelText != null)
        levelText.text = $"Lv {displayLevel}";

    if (backgroundImage != null)
    {
        var bgSprite = Resources.Load<Sprite>($"Rarities/{rarity}");
        if (bgSprite != null)
        {
            backgroundImage.sprite = bgSprite;
            backgroundImage.color  = Color.white;
        }
    }

    // No button wiring in template mode — opponents aren't tappable.
    // Force all status icons off — no PlayerCharacterData to query.
    if (selectedIcon != null)      selectedIcon.SetActive(false);
    if (levelUpReadyIcon != null)  levelUpReadyIcon.SetActive(false);
    if (staminaIcon != null)       staminaIcon.SetActive(false);
}
```

That's the entire diff to `CharacterThumbnailCard.cs` — one new method, no behavioural change to anything else.

### Step 2 — Create `MatchmakingModalController.cs`

Path: `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs`. Namespace: `Golfin.UI.Matchmaking`. Subclasses `Golfin.UI.Modals.ModalController`.

Public surface:
- `void Open()` — entrypoint called by `HomeScreenController`. Resolves player character, hole index, rewards, then `Show()`.
- `void Open(int holeIndex)` — overload that takes an explicit hole index (defaults to the value `HomeScreenController` is currently displaying — see Step 4).
- Inherits `Hide()` from base; `closeButton` is wired in the inspector to the prefab's `CancelButton`.

Inspector fields (all `[SerializeField]`):
- **Player side:**
  - `CharacterThumbnailCard playerCard` — points to `MatchMakingModal/ContentArea/InfoArea/Portraits/User1Info/CharacterThumbnailCardGlowUp` instance.
  - `TextMeshProUGUI playerUsernameText` — `User1Info/Username`.
  - `TextMeshProUGUI playerRankText` — `User1Info/Rank`.
- **Opponent side:**
  - `CharacterThumbnailCard opponentCard` — `User2Info/CharacterThumbnailCardGlowUp`.
  - `TextMeshProUGUI opponentUsernameText` — `User2Info/Username`.
  - `TextMeshProUGUI opponentRankText` — `User2Info/Rank`.
- **Status / hole / rewards:**
  - `TextMeshProUGUI statusText` — `InfoArea/Status`.
  - `TextMeshProUGUI holeTitleText` — `InfoArea/HoleTitle` (typically static "HOLE", but exposed for localization).
  - `TextMeshProUGUI holeInfoText` — `InfoArea/HoleInfo`.
  - `GameObject rewardRow1`, `Image reward1Icon`, `TextMeshProUGUI reward1Amount`
  - `GameObject rewardRow2`, `Image reward2Icon`, `TextMeshProUGUI reward2Amount`
  - `GameObject rewardRow3`, `Image reward3Icon`, `TextMeshProUGUI reward3Amount`
  - `Sprite pointsIcon`, `Sprite repairKitIcon`, `Sprite ballIcon` — copy the same three Sprite references from `HomeScreenController`'s "Reward Icons" header (sprite GUIDs are visible in the prefab's existing `Reward1Icon`/`Reward2Icon`/`Reward3Icon` Image components — implementer can either re-use those defaults via auto-wire or assign manually). Document any deviation.
- **Cancel button:**
  - `Button cancelButton` — `InfoArea/CancelButton`. Wire it to `closeButton` on the base ModalController via auto-wire (so the base class hooks `Hide`).
- **Tunables (Header "Tunables"):**
  - `float searchDurationSeconds = 5f` — total time from Open until "OPPONENT FOUND" lock.
  - `float opponentCycleIntervalSeconds = 0.3f` — seconds between opponent portrait swaps during the search.
  - `float dotCycleIntervalSeconds = 0.4f` — seconds between dot states (1 → 2 → 3 → 1).
  - `string statusSearchingText = "FINDING OPPONENT"` — base text; controller appends the dots.
  - `string statusFoundText = "OPPONENT FOUND"` — final text after lock.
  - `string[] fakeOpponentUsernames` — small inspector list for random opponent display names. Default values: `{ "GolfWarrior", "BirdieHunter", "EagleEye", "ParBuster", "GreenKing", "SwingMaster", "AceShooter", "FairwayPro" }`. Player username falls back to `"You"` when no real value exists (no global UserData class yet — see "Out of scope").
  - `Vector2Int fakeRankRange = new Vector2Int(50, 999)` — inclusive range for the random `RANK: #N` displayed under each portrait.
  - `Vector2Int fakeOpponentLevelRange = new Vector2Int(1, 50)` — inclusive range for random opponent level display (no level data on `CharacterDataRuntime`; we're faking it).
- **Hole source:**
  - `HoleDatabase holeDatabase` — optional reference to the same `Assets/Data/HoleDatabase.asset` `HomeScreenController` uses. Falls back to `HoleDatabaseLoader.GetHole(...)` when null. (Same pattern as `HomeScreenController.LoadNextHole`.)
  - `int defaultHoleIndex = 0` — used when `Open()` is called with no argument and there's no externally-supplied index.

Lifecycle:
- `Awake()` — call `base.Awake()` (which wires `closeButton` to `Hide`). Optionally cache `WaitForSeconds` instances for the coroutines.
- `OnShow()` (override) — kick off two coroutines: `DotCycleRoutine` (loops) and `OpponentScanRoutine` (one-shot, ends after `searchDurationSeconds`). Both are stopped in `OnHide` to avoid leaking.
- `OnHide()` (override) — stop both coroutines, clear status text.
- `Open([int holeIndex = -1])`:
  1. Resolve `holeIndex` — argument if `>= 0`, else `defaultHoleIndex`.
  2. Resolve player character ID + level via `CharacterManager.Instance.GetSelectedCharacterId()` + `GetPlayerCharacter(id).currentLevel`. If null/empty, log a warning and bail (`return` without calling `Show()`).
  3. Populate player card via `playerCard.Initialize(playerCharId)` (this is the existing path — works because the player owns themselves).
  4. Populate `playerUsernameText` with `"You"` (placeholder until UserData exists). Populate `playerRankText` with `$"RANK: #{Random.Range(fakeRankRange.x, fakeRankRange.y + 1)}"` once at Open (does NOT cycle — only the opponent rank cycles).
  5. Build the opponent pool: `CharacterDatabaseCSV.Instance.GetAllCharacters()` minus the player's `characterId`. If the resulting list is empty (defensive — only happens if the roster has only one character), fall back to the full list.
  6. Resolve hole: try `holeDatabase.GetHole(holeIndex)`, then `HoleDatabaseLoader.GetHole(holeIndex)`, then a hardcoded stub (`courseNameKey = "HOLE_LOMOND_5"`, three `x10` rewards as Points/RepairKit/Ball). Apply via `ApplyHole(HoleData)` (private helper that mirrors `HomeScreenController.SetNextHoleFromData`).
  7. Call `Show()`.

Coroutines:
- `DotCycleRoutine`: loop forever. Maintains an int `dotCount` cycling 1 → 2 → 3 → 1. Each tick sets `statusText.text = statusSearchingText + new string('.', dotCount)`. Yields `dotCycleIntervalSeconds`.
- `OpponentScanRoutine`:
  1. `float elapsed = 0f;`
  2. Loop while `elapsed < searchDurationSeconds`: pick a random entry from the opponent pool (avoid repeating the immediately previous pick when the pool has ≥ 2 entries). Call `opponentCard.InitializeFromTemplate(pickId, randomLevelInPickRange)` where `randomLevelInPickRange = Random.Range(fakeOpponentLevelRange.x, fakeOpponentLevelRange.y + 1)`. Set `opponentUsernameText.text` to a random entry from `fakeOpponentUsernames`; set `opponentRankText.text` to `$"RANK: #{Random.Range(fakeRankRange.x, fakeRankRange.y + 1)}"`. Yield `opponentCycleIntervalSeconds`. Add `opponentCycleIntervalSeconds` to `elapsed`.
  3. After the loop: stop the dot-cycle coroutine. Set `statusText.text = statusFoundText` (no trailing dots). Leave the final opponent on screen unchanged. **Do not** auto-close — the modal stays open until the player taps Cancel. (Per Cesar's confirmation 2026-05-02: "freezes for the env test".)

### Step 3 — Hole + reward population helper

Add a private method on `MatchmakingModalController`:

```csharp
private void ApplyHole(HoleData hole)
{
    if (hole == null) return;

    if (holeTitleText != null)
        holeTitleText.text = LocalizationManager.Get("HOME_NEXT_HOLE"); // or a dedicated MATCHMAKING_HOLE key if added later — see "Out of scope"

    if (holeInfoText != null)
        holeInfoText.text = LocalizationManager.Get(hole.courseNameKey);

    for (int i = 0; i < 3; i++)
    {
        if (i < hole.rewards.Count)
            SetupRewardRow(i, hole.rewards[i].type, hole.rewards[i].amount);
        else
            HideRewardRow(i);
    }
}
```

`SetupRewardRow` and `HideRewardRow` are byte-for-byte equivalents of the same-named methods on `HomeScreenController` — copy them in (they're <30 lines combined). Yes, this is duplication; resolving it via a shared helper is out of scope (see "Out of scope" below).

**Note on the `holeTitleText`:** the prefab currently shows "HOLE" — that's fine as a hard-coded label in this task. If `LocalizationManager.Get("HOME_NEXT_HOLE")` returns "NEXT HOLE", that's also fine for the env test. Implementer: use the same key the Home screen uses (`"HOME_NEXT_HOLE"`) so behaviour matches. If Cesar wants a separate key later, that's a separate spec.

### Step 4 — Wire the trigger into `HomeScreenController`

Currently, `HomeScreenController.OnPlayClicked` does:
```csharp
private void OnPlayClicked()
{
    Debug.Log("[HomeScreen] PLAY clicked");
    if (screenManager != null)
        screenManager.ShowScreen(ScreenId.Loading);
}
```

Change it to open the matchmaking modal instead. Two minimum-diff steps:

1. Add a serialized reference at the bottom of the field list:
   ```csharp
   [Header("Matchmaking")]
   [SerializeField] private Golfin.UI.Matchmaking.MatchmakingModalController matchmakingModal;
   ```
2. Replace the body of `OnPlayClicked`:
   ```csharp
   private void OnPlayClicked()
   {
       Debug.Log("[HomeScreen] PLAY clicked");
       if (matchmakingModal != null)
       {
           matchmakingModal.Open(currentHoleIndex);
           return;
       }
       // Legacy fallback if matchmaking isn't wired in this scene
       if (screenManager != null)
           screenManager.ShowScreen(ScreenId.Loading);
   }
   ```

Implementer also needs to add `using Golfin.UI.Matchmaking;` at the top of `HomeScreenController.cs` (or fully-qualify as shown above — either is fine, follow the file's existing style).

The bottom-nav `mainPlayButton` on `PersistentUIManager` is **not** wired in this task. It currently logs a "not yet implemented" warning when tapped, and that stays the case. (Cesar's wording was "When you hit Play", and the only currently-functional Play is the HomeScreen Next Hole button. Hooking the bottom-nav button is a separate decision.)

### Step 5 — Editor auto-wire

Create `Assets/Scripts/UI/Matchmaking/Editor/MatchmakingModalAutoWire.cs`. Mirror `Assets/Scripts/UI/Inventory/Editor/ItemUseModalAutoWire.cs` — same `WireTMP` / `WireImage` / `WireButton` helper pattern, same `MenuItem("GOLFIN/Wire/Matchmaking Modal")` registration, same `EditorUtility.DisplayDialog` summary at the end.

Wire targets (paths relative to the `MatchMakingModal` root in `ShellScene.unity`):
| Inspector field | Path | Component |
|---|---|---|
| `modalPanel` (base) | `.` (the controller's own GO) | (GameObject) |
| `backdrop` (base) | `BG` | GameObject |
| `closeButton` (base) | `ContentArea/InfoArea/CancelButton` | Button |
| `playerCard` | `ContentArea/InfoArea/Portraits/User1Info/CharacterThumbnailCardGlowUp` | CharacterThumbnailCard |
| `playerUsernameText` | `ContentArea/InfoArea/Portraits/User1Info/Username` | TextMeshProUGUI |
| `playerRankText` | `ContentArea/InfoArea/Portraits/User1Info/Rank` | TextMeshProUGUI |
| `opponentCard` | `ContentArea/InfoArea/Portraits/User2Info/CharacterThumbnailCardGlowUp` | CharacterThumbnailCard |
| `opponentUsernameText` | `ContentArea/InfoArea/Portraits/User2Info/Username` | TextMeshProUGUI |
| `opponentRankText` | `ContentArea/InfoArea/Portraits/User2Info/Rank` | TextMeshProUGUI |
| `statusText` | `ContentArea/InfoArea/Status` | TextMeshProUGUI |
| `holeTitleText` | `ContentArea/InfoArea/HoleTitle` | TextMeshProUGUI |
| `holeInfoText` | `ContentArea/InfoArea/HoleInfo` | TextMeshProUGUI |
| `rewardRow1` | `ContentArea/InfoArea/Rewards/Reward Row1` | GameObject |
| `reward1Icon` | `ContentArea/InfoArea/Rewards/Reward Row1/Reward1Icon` | Image |
| `reward1Amount` | `ContentArea/InfoArea/Rewards/Reward Row1/Reward1Amount` | TextMeshProUGUI |
| `rewardRow2` | `ContentArea/InfoArea/Rewards/Reward Row2` | GameObject |
| `reward2Icon` | `ContentArea/InfoArea/Rewards/Reward Row2/Reward2Icon` | Image |
| `reward2Amount` | `ContentArea/InfoArea/Rewards/Reward Row2/Reward2Amount` | TextMeshProUGUI |
| `rewardRow3` | `ContentArea/InfoArea/Rewards/Reward Row3` | GameObject |
| `reward3Icon` | `ContentArea/InfoArea/Rewards/Reward Row3/Reward3Icon` | Image |
| `reward3Amount` | `ContentArea/InfoArea/Rewards/Reward Row3/Reward3Amount` | TextMeshProUGUI |
| `cancelButton` | `ContentArea/InfoArea/CancelButton` | Button |
| `holeDatabase` | (Asset) `Assets/Data/HoleDatabase.asset` | HoleDatabase (loaded via AssetDatabase) |
| `pointsIcon` / `repairKitIcon` / `ballIcon` | Pull from existing sprites referenced on `Reward1Icon` / `Reward2Icon` / `Reward3Icon` Images (the prefab already has Points / RepairKit / Ball sprites assigned to those three slots in that order; copy the `Sprite` reference of each `Image.sprite` into the matching field). | Sprite |

**Reward icon mapping note:** the prefab's three reward-icon slots are pre-populated with sprites that look like the Points/RepairKit/Ball sprites in that order (verified via sprite GUIDs in the prefab YAML at lines 86, 1956, and 1404). Auto-wire should grab those three sprites in slot order and assign `pointsIcon`, `repairKitIcon`, `ballIcon` accordingly. If the auto-wire can't determine which sprite is which type from the prefab alone, it should leave them unassigned and report which fields need manual assignment.

Auto-wire must also wire the `HomeScreenController.matchmakingModal` field on the same scene's `HomeScreen` GameObject. Find the `HomeScreenController` instance, find the `MatchmakingModalController` instance, set the SerializedProperty `matchmakingModal` to it. Mirror the cross-wiring pattern in `ItemDetailPanel.useModal` setup at the end of `ItemUseModalAutoWire`.

### Step 6 — Smoke test sequence

After wiring:
1. Open `Assets/Scenes/ShellScene.unity`.
2. Run `GOLFIN > Wire > Matchmaking Modal` from the menu. Confirm the dialog shows ≥ 22 fields wired and 0 failures.
3. Enter Play mode. Wait at least 5 s for `OnEnable` paths and `CharacterDatabaseCSV` to load.
4. From the Home screen, tap the Next Hole **PLAY** button.
5. Confirm: backdrop fades in, `MatchMakingModal` becomes visible, `Status` cycles through "FINDING OPPONENT.", "..", "..." at ~0.4 s/step, opponent portrait + username + rank cycle at ~0.3 s/step, player card stays static, hole info + rewards match what the Home screen showed.
6. Wait 5 s. Confirm: opponent locks on a final character (no more cycling), `Status` reads "OPPONENT FOUND" with no trailing dots, dot-cycle has stopped.
7. Tap Cancel. Confirm: modal hides (with fade), player returns to Home screen, no console errors.
8. Take a play-mode screenshot during step 6 (lock state). Save to `Docs/Specs/Active/matchmaking_modal/screenshots/<timestamp>.jpg`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. The Implementer cannot mark the task done without filling every line. The self-reviewer will reject any report with unfilled or unjustified checklist items.

- [ ] `CharacterThumbnailCard.InitializeFromTemplate(string, int)` exists, public, sets portrait/name/rarity/level/background, forces all three status icons OFF, does NOT call `CharacterManager.GetPlayerCharacter`.
- [ ] No other method on `CharacterThumbnailCard.cs` was modified (verify via diff).
- [ ] `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` exists, namespace `Golfin.UI.Matchmaking`, subclasses `Golfin.UI.Modals.ModalController`.
- [ ] `MatchmakingModalController` exposes the SerializeField inspector fields listed in Implementation §2.
- [ ] `MatchmakingModalController` exposes the Tunables fields (`searchDurationSeconds`, `opponentCycleIntervalSeconds`, `dotCycleIntervalSeconds`, `statusSearchingText`, `statusFoundText`, `fakeOpponentUsernames`, `fakeRankRange`, `fakeOpponentLevelRange`) and they appear under a "Tunables" header in the Inspector.
- [ ] `MatchmakingModalController.Open(int holeIndex = -1)` is public; `Open()` no-arg overload also exists and forwards `defaultHoleIndex`.
- [ ] Dot cycle: status text reads "FINDING OPPONENT.", "FINDING OPPONENT..", "FINDING OPPONENT..." in sequence, ~0.4 s per step (verified at default tunable).
- [ ] Opponent portrait, username, and rank cycle every ~0.3 s while searching (verified at default tunable).
- [ ] Player portrait + name + level remain unchanged for the entire search (no flicker).
- [ ] At `searchDurationSeconds` (default 5 s) the dot cycle stops, status reads exactly "OPPONENT FOUND" (no trailing dots), opponent stays locked on the last cycled character.
- [ ] Cancel button hides the modal (base ModalController fade) and returns control to the Home screen.
- [ ] Hole info (course label) reads the localized `courseNameKey` from `HoleDatabase.GetHole(currentHoleIndex)` — same value the Home screen's Next Hole panel shows for the same index.
- [ ] Reward rows (1/2/3) display the matching icon (Points/RepairKit/Ball) and `xN` amount from the same `HoleData.rewards` the Home screen reads. Empty rows are deactivated.
- [ ] `HomeScreenController.OnPlayClicked` calls `matchmakingModal.Open(currentHoleIndex)` when the reference is non-null, and falls back to the legacy `screenManager.ShowScreen(ScreenId.Loading)` only when null.
- [ ] `Assets/Scripts/UI/Matchmaking/Editor/MatchmakingModalAutoWire.cs` exists, registered as `GOLFIN/Wire/Matchmaking Modal`, wires every field listed in Implementation §5 with PASS/FAIL counts surfaced via `EditorUtility.DisplayDialog`.
- [ ] Auto-wire dialog reports ≥ 22 fields wired and 0 failures on a clean `ShellScene.unity`.
- [ ] Auto-wire also sets `HomeScreenController.matchmakingModal` to the in-scene `MatchmakingModalController` instance.
- [ ] No new asmdefs, no `.meta` files renamed, no prefab reauthored — only the 3 source files added/modified plus 1 line in HomeScreenController + 1 inspector slot wired.
- [ ] No white-box placeholders visible in the screenshot.
- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] Unity Console has no errors related to this task during the smoke test sequence.
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` — **modify**: add one new public method `InitializeFromTemplate(string charId, int displayLevel)`. No other changes.
- `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` — **create**: the new controller (subclasses `ModalController`).
- `Assets/Scripts/UI/Matchmaking/Editor/MatchmakingModalAutoWire.cs` — **create**: editor menu item that wires fields + cross-wires `HomeScreenController.matchmakingModal`.
- `Assets/Scripts/UI/HomeScreenController.cs` — **modify**: add 1 SerializeField + 1 using + edit `OnPlayClicked` body. Total diff: ≤ 8 lines added/modified.
- `Assets/Scenes/ShellScene.unity` — **modify**: a new MonoBehaviour component on the `MatchMakingModal` GameObject (the controller) plus all the SerializedProperty references the auto-wire script writes. Both kinds of edit happen via the editor — Implementer should NOT hand-edit YAML.
- `Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab` — **NOT MODIFIED**. The prefab stays as-is; the controller is added to the scene-instance, not the prefab. (If Cesar later wants to bake the controller into the prefab, that's a separate spec.)

## Out of scope (do NOT do these)

- Networking, real matchmaking, real opponent data — this is a pure cosmetic stub.
- Granting any rewards, advancing any quest, modifying any save state.
- Building a `UserData` / `PlayerProfile` class. Cesar confirmed user data "doesn't exist right now" and is deferred. Player username is hard-coded `"You"`; player rank is faked from `fakeRankRange`. Don't invent a global user-profile singleton in this task.
- Auto-closing or auto-progressing after "OPPONENT FOUND". The modal stays open until Cancel is pressed. (Cesar 2026-05-02.)
- Wiring the `mainPlayButton` on `PersistentUIManager`'s bottom nav. Only the HomeScreen Next Hole `playButton` opens this modal.
- Hooking the `League` button at the top of the modal's title area. It exists in the prefab but Cesar didn't define behaviour; leave it as a passive label.
- Refactoring `HomeScreenController.SetupRewardRow` / `HideRewardRow` into a shared helper. The duplication is intentional for now — a shared `RewardRowBinder` is a separate cleanup spec when there are 3+ call sites.
- Adding a new localization key (`MATCHMAKING_FINDING`, `MATCHMAKING_FOUND`). The status strings are inspector-tunable raw strings for the env test; localization is a follow-up spec when the matchmaking flow is real.
- Modifying the `MatchMakingModal.prefab` itself. Visual fidelity is already final.
- Modifying `ModalController.cs`. The base class's existing fade + show/hide behaviour is sufficient.
- Touching anything physics-related. The two known physics bugs (putter velocity, surface roll resistance — `📅 Roadmap` item C) are out-of-scope for this Mac env test; do not "helpfully" investigate them.
- Modifying any tests, any physics scenes, or anything outside `Assets/Scripts/UI/Matchmaking/`, `CharacterThumbnailCard.cs`, and `HomeScreenController.cs`.

## Open questions for Architect (Implementer fills if blocked)

> Surface here if anything in the spec is genuinely ambiguous. Do NOT silently invent resolutions.

(empty)
