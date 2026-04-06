# Lessons Learned

## Git / Version Control

### ALWAYS push after changes
User requested: push to GitHub after every change, not just on request.
Pattern: `git add <files> && git commit -m "..." && git push`

### git checkout reverts too much
**Mistake:** Used `git checkout -- <file>` to undo a specific change, but it reverted the file
to the last commit — erasing other unrelated fixes in the same file.
**Rule:** Before reverting, read the file carefully and do a targeted Edit instead.
If you must revert, cherry-pick only the specific lines that need to change.

### Revert removes multiple fixes at once
When `CharacterDetailPanel.cs` was reverted, it lost:
- `selectButton.interactable = !isSelected`
- Level Up / Boost button disabled state logic
**Rule:** Never use `git checkout` on a file that has multiple accumulated fixes.
Use `Edit` to surgically restore just the broken part.

## Unity / C# Patterns

### CS0136 — duplicate local variable in same scope
If a variable is declared at the top of a method (e.g., `int maxLevel`), don't redeclare it
in an inner block. Use the existing variable or rename.

### Singleton null guard in OnEnable/OnDisable
Always wrap event subscriptions in `if (SomeSingleton.Instance != null)` — singletons may not
be initialized when OnEnable fires during scene load.

### ContentSizeFitter required for HorizontalLayoutGroup to size content
Without `ContentSizeFitter.horizontalFit = PreferredSize`, content width collapses and cards
compress. Always add it to the Content object of a ScrollRect.

### CSV-first pattern for character data
`CharacterDatabaseCSV.Instance?.GetCharacter(id)` returns runtime data for all 12 characters.
`CharacterManager.Instance.GetCharacterTemplate(id)` returns ScriptableObject data (may only
have a subset of characters). Always try CSV first, SO as fallback.

### Viewport is the clipping boundary, not the layout group
Expanding layout group padding does NOT fix card clipping on scale-up.
The `ScrollRect.viewport` RectTransform is what clips. Expand it via `offsetMin`/`offsetMax`.
Guard with a bool (`viewportExpanded`) to prevent cumulative expansion on repeated calls.

### Image.enabled = false works for background hiding
Setting `rarityBadgeImage.enabled = false` correctly hides the background Image.
If it appears not to work, check that the SerializeField is wired to the correct Image component
in the Unity Inspector.

## UI / Design

### Gold color for selected state
Use `new Color(1f, 0.8f, 0.2f, 1f)` as gold for selected button tint.
Apply via `selectButton.GetComponent<Image>().color = goldColor`.

## Editor Fix Scripts — Use Generic Component Search, Not Hardcoded Paths

**Mistake:** FixBarImageTypes.cs used hardcoded Transform.Find() paths. When the user had reorganised the hierarchy for layout fixes, the script missed bars that had moved.

**Rule:** One-shot fix/patch editor scripts that target a component type across the scene should use `Object.FindObjectsOfType<T>()` or recursive search by component, filtered by name if needed — never hardcoded full paths. Hardcoded paths are brittle and break silently when the user adjusts the hierarchy.

**Pattern to use:**
```csharp
// Find ALL Image components in scene, filter by GameObject name
foreach (var img in Object.FindObjectsOfType<Image>())
{
    if (img.gameObject.name == "Bar" || img.gameObject.name == "BarPending")
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        EditorUtility.SetDirty(img);
    }
}
```
This survives any hierarchy reorganisation the user makes.

## ScreenManager Must Drive PersistentUIManager Bar Visibility

**Mistake:** `PersistentUIManager` had `ShowBars()`/`HideBars()` but nothing called them.
`Awake()` hides bars; there was no code to show them when navigating to Home or Roster.

**Rule:** Any screen manager that controls screen transitions MUST also call
`PersistentUIManager.Instance?.ShowBars()` / `HideBars()` in the same `ApplyScreen()` method.
Never leave bar visibility untriggered — it will silently stay hidden.

**Pattern:**
```csharp
bool showBars = screenId == ScreenId.Home || screenId == ScreenId.Roster;
if (Golfin.UI.PersistentUIManager.Instance != null)
{
    if (showBars) Golfin.UI.PersistentUIManager.Instance.ShowBars();
    else          Golfin.UI.PersistentUIManager.Instance.HideBars();
}
```

## Never Use ?? With Unity Objects — Use == null Instead

**Mistake:** Used `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()` in `GetOrAddCG`.
C#'s `??` operator uses reference equality (`ReferenceEquals`), NOT Unity's overloaded `==`.
A destroyed/missing Unity component passes `??` but throws `MissingComponentException` on access.

**Rule:** Always use `== null` / `!= null` when checking Unity `UnityEngine.Object` references.
Never use `??` or `?.` for the null-coalescing/null-conditional part of Unity object checks.

**Pattern:**
```csharp
// WRONG — ?? can miss Unity-null objects:
var cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();

// CORRECT — == null respects Unity's overloaded equality:
var cg = obj.GetComponent<CanvasGroup>();
if (cg == null) cg = obj.AddComponent<CanvasGroup>();
```

## Pre-Add CanvasGroup in Builder — Never Rely on Runtime AddComponent for Fades

**Rule:** If a GameObject will be faded (FadeIn/FadeOut via CanvasGroup), add the CanvasGroup
in the Editor builder script at build time, not lazily at runtime. Runtime AddComponent on objects
that may be inactive or mid-animation can produce stale references.

**Pattern (in builder):**
```csharp
var cg = clone.GetComponent<CanvasGroup>() ?? clone.AddComponent<CanvasGroup>();
cg.alpha = 0f;  // start transparent; FadeIn animates to 1
```

## Clone RightPanel for Compare Panel — Never Build From Scratch

**Rule:** When a compare/secondary panel must visually match an existing panel, clone it with
`Object.Instantiate(rightPanel.gameObject, parent, false)` rather than building from scratch.
Building from scratch requires duplicating every font/color/size the user set manually.
Cloning preserves all those settings automatically.

**After cloning:**
- Override the clone's RectTransform anchors to position it correctly
- Wrap all cloned children in a new empty container (CompareInfoPanel) for show/hide control
- Add ComparePlaceholder as a full-stretch overlay on top
- Strip any left-column-specific buttons from the clone's ButtonsPanel

## AutoWire Paths Must Be Verified Against Scene YAML — Don't Assume Names

**Mistake:** Assumed child names (RarityLabel, Text) without checking the actual scene YAML.
4 paths failed because real names were: RarityText, LevelPanel/LevelText, LevelPanel/LevelTextMax,
and "Text (TMP)" (not "Text").

**Rule:** For any AutoWire paths that aren't directly from CLAUDE.md documentation, grep the
ShellScene.unity for the actual `m_Name:` values. Use `m_Father` fileID cross-references to
confirm parent-child relationships before coding the paths.

## After Compare Swap — Explicitly Push New Character Into Detail Panel

**Problem:** CharacterDetailPanel.OnSelectionChanged refreshes the CURRENTLY DISPLAYED character's
button state, but never switches currentCharacterId to the newly selected character.
After a swap from compare mode, the panel kept showing the old character.

**Fix:** Add a public `ShowCharacter(string id)` method to CharacterDetailPanel that sets
`currentCharacterId` and calls `UpdatePanel`. Call it from CompareController after any swap,
AFTER CleanupAndExit() (which sets _isCompareMode = false so UpdatePanel doesn't early-return).

```csharp
// In CompareController:
private void CommitSwapAndExit(string characterId)
{
    CharacterManager.Instance.SelectCharacter(characterId);
    CleanupAndExit();  // sets _isCompareMode = false first
    GetComponent<CharacterDetailPanel>()?.ShowCharacter(characterId);
}
```

## HorizontalLayoutGroup Overrides LayoutElement Preferred Sizes for Thin Dividers

**Mistake:** Used `LayoutElement.preferredWidth = 1f` for thin divider Images inside a HLG. The HLG auto-sizes children based on `childForceExpand` and available space, overriding the preferred width entirely.

**Rule:** For absolutely-positioned overlays (dividers, indicators) inside a layout group, use `LayoutElement.ignoreLayout = true` and position manually via RectTransform anchors/sizeDelta.

**Pattern:**
```csharp
var le = divGO.AddComponent<LayoutElement>();
le.ignoreLayout = true;

var rt = divGO.GetComponent<RectTransform>();
float xPos = (float)(i + 1) / buttonCount; // normalized position between buttons
rt.anchorMin        = new Vector2(xPos, 0.15f);
rt.anchorMax        = new Vector2(xPos, 0.85f);
rt.sizeDelta        = new Vector2(1f, 0f);   // 1px wide, height from anchors
rt.anchoredPosition = Vector2.zero;
```

## FadeController GameObject May Be Inactive in Editor — Causes Missing Screen Transitions

**Mistake:** FadeController is left inactive in the scene during editing. Because `Awake()` never runs, `FadeController.Instance` stays null. ScreenManager's `FadeOutThenIn` call is skipped, and the Inventory screen either appears instantly or not at all depending on timing.

**Rule:** In `ScreenManager.Awake()`, find FadeController including inactive GameObjects and activate it before any screen transitions are attempted.

**Pattern:**
```csharp
if (FadeController.Instance == null)
{
    var fc = FindObjectOfType<FadeController>(includeInactive: true);
    if (fc != null) fc.gameObject.SetActive(true);
}
```

## Always Use New Input System — Never UnityEngine.Input

**Mistake:** Used `Input.GetKeyDown(KeyCode)` in a debug script. Project uses the New Input System package, so the legacy `UnityEngine.Input` class throws InvalidOperationException at runtime.

**Rule:** ALWAYS use `UnityEngine.InputSystem` in this project. Never use `UnityEngine.Input`.

**Pattern:**
```csharp
using UnityEngine.InputSystem;

// Key check (replaces Input.GetKeyDown):
if (Keyboard.current != null && Keyboard.current[Key.Backquote].wasPressedThisFrame) { }

// Mouse button (replaces Input.GetMouseButtonDown(0)):
if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { }
```

## Never Use Namespace `Golfin.Debug` — Shadows UnityEngine.Debug

**Mistake:** Created `WalkCamera.cs` in `namespace Golfin.Debug`. Every file with `using UnityEngine;` that calls `Debug.Log` now resolves `Debug` to `Golfin.Debug` instead of `UnityEngine.Debug`, causing 100+ compile errors across the entire project.

**Rule:** Never create a namespace called `Golfin.Debug` (or any `*.Debug` namespace). The name collides with `UnityEngine.Debug` which is used everywhere. Put debug utilities in the global namespace or a non-colliding namespace like `Golfin.DebugTools`.

## Raycast Target on Decorative Images Blocks Button Clicks

**Repeat offender.** This has happened on both the Roster DetailPanel and the Club DetailPanel.

**Symptom:** Buttons exist, look correct, but don't respond to taps/clicks.

**Cause:** A non-interactive Image component (background, rim, portrait, decorative element) has `Raycast Target = true` and sits on top of or overlaps the button in the hierarchy. It eats the click before the button receives it.

**Fix:** Disable `Raycast Target` on ALL non-interactive Image components — backgrounds, rims, portraits, dividers, icons (unless the icon IS a button). Only Buttons and interactive elements should have Raycast Target enabled.

**Prevention:** When creating any new Image component in code or Inspector, immediately set `raycastTarget = false` unless it's intentionally interactive.

## ModalController — Root Must Stay Active

**Rule:** `ModalController` expects the **root GameObject to always be active**. It only toggles the `modalPanel` child via `Show()`/`Hide()`. If the root is inactive, `Show()` still runs (called directly in code, not via Unity events) and calls `modalPanel.SetActive(true)`, but nothing renders because the parent is inactive.

**Symptom:** Console shows `[Modal] X shown` but nothing appears in the hierarchy as active.

**Fix:** Ensure the modal root GameObject is enabled in the scene. Save the scene in that state so Play mode doesn't revert it.

## GameObject.Find() Misses Inactive Objects — Use FindObjectOfType in AutoWire Scripts

**Rule:** `GameObject.Find("Name")` silently returns null for inactive GameObjects. Since modals start hidden (`ModalController.Awake()` deactivates `modalPanel`), the root may be active but if it was ever saved inactive it won't be found.

**Pattern for all AutoWire scripts:**
```csharp
// WRONG — misses inactive objects:
var go = GameObject.Find("MyModal");

// CORRECT — finds inactive too:
var controller = Object.FindObjectOfType<MyModalController>(includeInactive: true);
var go = controller?.gameObject;
```

## Modal Anchor Repositioning Only Works at Canvas Root

**Mistake:** Copied anchor-repositioning logic (world→local coord math) from `LevelUpModalController` into `ClubLevelUpModalController`. The character modal lives at the Canvas root so the math works. The club modal lives inside `InventoryScreen/ContentArea`, which has its own transform offsets — the math lands in the wrong spot and overwrites the correct inspector position every `Open()` call.

**Rule:** If a modal is parented inside a screen hierarchy (not at Canvas root), remove all runtime repositioning code. Set position in the editor; it will hold at runtime.

## Rarity Color Switch — Match Project Canonical Colors, Not Intuition

**Mistake:** In `ItemDetailPanel.GetRarityColor()`, wrote Uncommon as green and Rare as blue —
the opposite of the project standard. The project canonical colors are:
- Common    → grey-blue  `~#BFBFCC`
- Uncommon  → blue       `new Color(0.29f, 0.56f, 0.89f)`  (matches RarityHelper)
- Rare      → green      `#50C878`  `new Color(0.314f, 0.784f, 0.471f)`
- Mythic    → amber      `#FFC107`  `new Color(1.00f, 0.757f, 0.027f)`
- Legendary → orange     `new Color(1.00f, 0.65f, 0.10f)`
- Supreme   → red        `new Color(1.00f, 0.30f, 0.30f)`

**Rule:** When writing a local rarity color switch (for string-based rarities that can't use
`RarityHelper` enum), always cross-check against `RarityHelper.GetRarityColor()` in
`CharacterDatabase.cs` before writing the values. Don't assume which color maps to which rarity.

---

## Editor Scripts — Always Search Including Inactive Objects

**Mistake (repeated):** Used `GameObject.Find("DetailPanel")` and `Object.FindObjectOfType<T>()` in editor scripts. Both silently return null for inactive GameObjects, which is the normal state for screens and modals in this project.

**Rules:**
- For finding by name: use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.name == "X" && go.scene.isLoaded`
- For finding by type: use `Object.FindObjectOfType<T>(true)` (the `true` = includeInactive)

**Pattern:**
```csharp
// By name (finds inactive):
foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
    if (go.name == "DetailPanel" && go.scene.isLoaded) return go.transform;

// By type (finds inactive):
var modal = Object.FindObjectOfType<LevelUpModalController>(true);
```
