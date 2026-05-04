# Screen Implementation Lessons

Hard-won lessons from previous screens (Roster, Inventory, HoleSelection). Read this before starting a new screen so the first iteration looks closer to the Figma reference and Cesar's polish pass survives.

Pair with: [`Docs/Architecture/PATTERNS.md`](PATTERNS.md), [`Docs/Architecture/UI_HIERARCHY.md`](UI_HIERARCHY.md), [`Docs/Game Design/ASSET_NAMING_CONVENTION.md`](../Game%20Design/ASSET_NAMING_CONVENTION.md), [`Docs/Rules.md`](../Rules.md).

---

## 1. Spec gathering (before writing any code)

**Confirm the canonical Figma frame with Cesar.** Don't guess which page/frame is the source of truth. Paste the Figma URL/frame name in the spec and ask Cesar to approve before implementing.

**Distinguish placeholder vs canonical.** Some Figma elements are placeholder copy or stand-in colours. Ask: "match exactly, or substitute project sprite/utility?"

**Inventory existing sprites before generating new ones.** Walk `Assets/Art/<ScreenName>/` and `Assets/Resources/` — Cesar usually exports the real sprites from Figma before kicking off a UI task. Generated PNGs (e.g. chevrons drawn in code) almost always get replaced later — don't ship them.

---

## 2. Prefab discipline — Cesar's polish pass wins

**Once Cesar touches the prefab or scene in the Editor, those values are authoritative.** Code only writes truly dynamic data — the parts that depend on runtime state (hole number, par, reward counts, current level, etc.).

Concrete things runtime code must NOT overwrite once the prefab is set:
- Title/header text (e.g. "NEXT" was being clobbered by a hardcoded `"PLAY HOLE"`).
- Active text colour on selected pills (Cesar's gold; code was re-applying gold and fighting it).
- Image `preserveAspect`, image type, 9-slice borders.
- Chevron / arrow active states (Cesar may have deactivated them entirely).
- `sizeDelta` on prefab roots (today's bug — see §3).
- Word-wrap settings on TMP labels.

**Pattern:** in your `Bind(...)` method, write a comment block above each mutated field explaining *why* it's a runtime write. If you can't justify it, the prefab should own that value.

```csharp
// IMPORTANT: do NOT overwrite the title at runtime. Cesar's polish pass set the
// title text manually in the prefab to match the Figma reference (e.g. "NEXT")
// — overwriting it with "PLAY HOLE" / "REPLAY HOLE" was clobbering his copy.
// Only the subtitle gets a runtime write because it carries the dynamic
// "Lomond Country Club  - Hole N - Par P" payload.
```

See [`HoleCardController.cs`](../../Assets/Scripts/UI/HoleSelection/HoleCardController.cs) for the canonical example.

---

## 3. Layout traps — parent LayoutGroups override prefab sizes

**A `HorizontalOrVerticalLayoutGroup` on the parent with `ChildControlWidth=true` or `ChildForceExpandWidth=true` will override the child prefab's `sizeDelta`.** This burned us on HoleSelection: the card prefab declared 978px, the parent forced it to fill 1042px, and runtime cards rendered too wide.

**Before instantiating a prefab as a child of any container, check the container's LayoutGroup flags:**

| Flag | What it does |
|---|---|
| `ChildControlWidth` | Layout group sizes children to its `LayoutElement.preferredWidth` (or expands) — overrides prefab `sizeDelta.x` |
| `ChildForceExpandWidth` | Stretches children to fill the layout group's full width — overrides prefab `sizeDelta.x` even harder |
| `ChildControlHeight` / `ChildForceExpandHeight` | Same for height |

**Decision matrix for prefabs that need to keep their authored size:**

```
Want children to keep prefab sizeDelta:    ChildControl* = false, ChildForceExpand* = false
Want children to fill the parent:          ChildControl* = true,  ChildForceExpand* = true
Want LayoutElement.preferredWidth to win:  ChildControl* = true,  ChildForceExpand* = false
```

**Always walk the full ancestor chain.** A grandchild's width is governed by its direct parent's flags, but a grandparent VLG with `ChildForceExpandWidth=true` will stretch its grandchild's *parent*, which then stretches the grandchild via its own anchors. Inspect every LayoutGroup from the prefab root up to the canvas.

**Verification snippet** — drop into `script-execute` to dump the layout chain:

```csharp
Transform t = card.transform;
while (t != null) {
    var v = t.GetComponent<VerticalLayoutGroup>();
    var h = t.GetComponent<HorizontalLayoutGroup>();
    if (v != null) Debug.Log($"{Path(t)} VLG forceExpandW={v.childForceExpandWidth} controlW={v.childControlWidth}");
    if (h != null) Debug.Log($"{Path(t)} HLG forceExpandW={h.childForceExpandWidth} controlW={h.childControlWidth}");
    t = t.parent;
}
```

---

## 4. Sensible interaction defaults

**Open the screen in the state the player would land on with one tap.** If the player would obviously expand "Next hole" or select "current character," do that on `OnEnable`. Don't ship a screen where the first action is "tap the obvious thing."

Concrete examples:
- HoleSelection: auto-expand the first unlocked, not-yet-played card.
- Roster: select the active character (already done — copy this pattern).
- Inventory: pre-select the equipped item per category.

After expanding/selecting, also call your existing centring routine (e.g. `CentreCardNextFrame`) so the chosen card lands in view, not at the top of the scroll.

---

## 5. Use shared utilities — don't duplicate

Before writing colour math, gradient logic, or modal scaffolding, grep the project. Things that already exist:

| Utility | Use for |
|---|---|
| [`TextGradients`](../../Assets/Scripts/Utilities/TextGradients.cs) | Gold / silver text gradients (active vs inactive vs locked pills) |
| [`RarityHelper`](../../Assets/Scripts/UI/Roster/RarityHelper.cs) | Rarity colours, single-letter labels, badge text colours |
| [`RarityStatCaps`](../../Assets/Scripts/UI/Roster/Data/RarityStatCaps.cs) | Stat caps per rarity |
| [`ModalController`](../../Assets/Scripts/UI/ModalController.cs) | Base class — fade, backdrop, show/hide |
| [`ScreenManager`](../../Assets/Scripts/UI/ScreenManager.cs) | Screen activation/deactivation with fade transitions |
| [`FadeController`](../../Assets/Scripts/UI/FadeController.cs) | Fade transitions |
| [`PersistentUIManager`](../../Assets/Scripts/UI/PersistentUIManager.cs) | Top/bottom nav bar visibility per screen |
| [`LocalizationManager`](../../Assets/Scripts/Localization/LocalizationManager.cs) | `Get("KEY")` for all user-facing text |
| `Resources.Load<Sprite>("Folder/Name")` | Data-driven sprite loading (NOT Inspector arrays) |

**If you find yourself writing colour-mixing or gradient code, stop and grep first.**

---

## 6. Background scope — at the screen root, not inside a modal

A recent commit (`e02f4fba`) had to move HoleSelection's `Background.png` from a modal panel onto the screen root. Backgrounds belong to the screen, not the modal, because:

- Modals fade in/out; the screen background should stay visible the whole time.
- A modal's `CanvasGroup.alpha` will fade the background with it if the bg is parented inside.
- Backgrounds shared across multiple modals get duplicated if each modal owns its own.

**Rule:** background art is a child of the screen root (`HoleSelectionScreen/Background`, not `HoleSelectionScreen/SomeModal/Background`).

---

## 7. Scene-wired vs prefab-wired

Screens themselves live in `Assets/Scenes/ShellScene.unity`. Repeating elements (cards, list items, modal templates) are prefabs under `Assets/Prefabs/UI/<ScreenName>/`.

**Don't recreate hierarchies via editor builder scripts if the screen is already wired in the scene.** Bind data to the existing GameObjects. Editor builder scripts are for scaffolding the *first* draft of a layout — once Cesar polishes it, the builder is exhausted.

---

## 8. Anchors + sizeDelta — know which mode you're in

```
Point anchors (AnchorMin == AnchorMax):
  sizeDelta.x = absolute width
  sizeDelta.y = absolute height
  Card prefab default: AnchorMin/Max = (0.5, 1), sizeDelta = (978, 0)

Stretch anchors (AnchorMin != AnchorMax):
  sizeDelta.x = offset from left+right anchors (subtract from parent)
  sizeDelta.y = offset from top+bottom anchors
  Container default: AnchorMin = (0,0), AnchorMax = (1,1), sizeDelta = (0,0) → fills parent
```

If a prefab needs a fixed authored width regardless of parent size, use point anchors. If it should adapt, use stretch anchors with a `LayoutElement` for hints.

---

## 9. ContentSizeFitter — the right axis only

For a vertical scroll list:
- `ContentSizeFitter.HorizontalFit = Unconstrained` (width comes from anchors)
- `ContentSizeFitter.VerticalFit = PreferredSize` (height grows with children)

Setting both axes to `PreferredSize` makes the container collapse around its children width-wise too, which usually breaks layout under a `ScrollRect.Viewport`.

---

## 10. CSV-first data, localization-first text

**Data:** character / club / hole / level-up data lives in CSV (`Assets/Data/*.csv`). Don't hardcode names, par values, reward amounts, stat numbers in scripts — extend the CSV.

**Text:** every new user-facing string uses `LocalizationManager.Get("KEY")` with both EN and JP entries added to `Assets/Localization/LocalizationTextTable.asset`. Pattern: `SCREEN_ELEMENT` (`HOLE_SELECTION_PLAY`, `MODAL_CONFIRM`).

Hardcoded strings are technical debt — they all get migrated eventually. Don't add to the pile.

---

## 11. Asset naming — follow [`ASSET_NAMING_CONVENTION.md`](../Game%20Design/ASSET_NAMING_CONVENTION.md)

Quick reference:
- `S_` sprite, `BG_` background, `ICO_` icon, `T_` texture, `MESH_` 3D model, `FX_` effect, `SFX_` sound, `MUS_` music
- No spaces in filenames or folders — PascalCase or hyphens
- Characters: `S_Char_{Name}`, `S_CharFull_{Name}`
- Clubs: `S_Club_{Type}-{Brand}`
- UI: `ICO_{Name}`, `S_Btn_{Name}_{State}`, `S_Rarity_{Name}`
- Localization keys: `{SCREEN}_{ELEMENT}` (e.g. `CLUB_POWER`)
- CSV IDs: `char_{name}`, `club_{type}_{brand}`

**Don't rename anything in `Resources/`** without updating the corresponding CSV `iconName` / `holeImageName` fields.

---

## 12. Verification — playmode + screenshot

**EditMode is not enough.** Most screens depend on runtime singletons (CharacterManager, ClubManager, HoleProgressionService) that don't exist in EditMode. Cards/items won't instantiate. Verifying in EditMode and claiming "done" is a recurring failure mode.

Required loop:
1. Enter playmode via `editor-application-set-state isPlaying=true`.
2. Wait ≥3s (≥5s if data-binding heavy) for OnEnable + first frames.
3. Activate the target screen if it isn't the default (deactivate siblings under `ScreensRoot`, activate yours).
4. `screenshot-game-view`.
5. Compare side-by-side with the Figma reference.

`CaptureHelper` and the `GOLFIN > Capture > Fake State - <preset>` menu let you capture HUD/in-shot UI without a full play loop — see CLAUDE.md "Screenshots — MANDATORY rules."

---

## 13. Single-expanded / single-selected invariant

If cards or items have an expand/collapse or selected/unselected behavior, **the parent screen controller enforces the invariant, not the card.** Each card publishes `OnCardTapped` (or similar), parent decides who's expanded/collapsed.

```csharp
private void HandleCardTapped(HoleCardController card)
{
    if (card.State == HoleCardState.Locked) return;
    if (card.State == HoleCardState.Expanded) { card.SetState(Collapsed); return; }
    foreach (var c in _cards)
        if (c != null && c != card && c.State == HoleCardState.Expanded)
            c.SetState(HoleCardState.Collapsed);
    card.SetState(HoleCardState.Expanded);
    StartCoroutine(CentreCardNextFrame(card));
}
```

This keeps the card itself dumb (just a state holder + view binder).

---

## 14. Event subscription discipline

C# `System.Action` events. Subscribe in `OnEnable`, unsubscribe in `OnDisable`. When you tear down dynamic children (e.g. in `RebuildCards`), unsubscribe from each one before destroying:

```csharp
foreach (var c in _cards) {
    if (c == null) continue;
    c.OnCardTapped -= HandleCardTapped;
    c.OnActionButtonClicked -= HandleActionClicked;
}
foreach (Transform child in cardsContent) Destroy(child.gameObject);
_cards.Clear();
```

Skipping unsubscribes silently leaks references — destroyed cards still fire their button events through ghost subscribers.

---

## 15. Don't fight Unity's null

`==` not `??` for Unity objects. `??` does a managed-null check; Unity's `==` operator handles the "destroyed but still-managed" case correctly.

```csharp
// ✅
if (myImage == null) return;
var s = sprite != null ? sprite : fallback;

// ❌  silently breaks when myImage is destroyed
if (myImage is null) return;
var s = sprite ?? fallback;
```

---

## Quick checklist before claiming a screen done

- [ ] Figma frame confirmed with Cesar (which page, which frame, placeholder vs canonical).
- [ ] Real sprites used (no generated PNGs unless Cesar approved).
- [ ] No runtime overwrites of prefab values that aren't truly dynamic.
- [ ] Parent LayoutGroups verified — every ancestor's `ChildControl*` / `ChildForceExpand*` flags reviewed.
- [ ] Sensible default interaction state (auto-expand "current," scroll-to-target, etc.).
- [ ] Shared utilities used (TextGradients, RarityHelper, ModalController, etc.).
- [ ] Background at the screen root, not nested in a modal.
- [ ] All text via `LocalizationManager.Get`, both EN+JP entries added.
- [ ] All variable data from CSV, no hardcoded strings/numbers.
- [ ] Asset names follow `ASSET_NAMING_CONVENTION.md`.
- [ ] Single-expanded invariant enforced by parent (if applicable).
- [ ] OnEnable/OnDisable subscribe/unsubscribe pairs balanced.
- [ ] Verified in playmode with screenshot, compared to Figma side-by-side.
