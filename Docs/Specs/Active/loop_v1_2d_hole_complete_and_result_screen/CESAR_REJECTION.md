# Cesar Rejection — 2026-05-12 (iter 9 reject)

Iteration 9 architect-pass approved. Cesar pulled to PC, did manual fixes (committed as `3fb839de Results fixes`), then identified two remaining issues with the LOCKED panel.

## What Cesar did manually (now committed — preserve these going forward)

1. **Created `Assets/Prefabs/UI/Divider.prefab`** — canonical reusable Divider:
   - Size: 300×2px
   - Plain white Image (alpha 1.0)
   - Uses a specific divider sprite (guid `9e62d8f4ffd01e7468d07912ccba967a`)
   - This is the divider style Cesar wants for ALL future modals/panels.

2. **Replaced builder-generated inline dividers** with `PrefabInstance` references to the prefab. The three old `Divider_BelowSubhead` / `Divider_BelowBody` / `Divider_BelowRewards` GameObjects are gone; the prefab instances are now named `Divider`, `Divider (1)`, `Divider (2)`.

3. **Updated `Lock.png`** — larger lock icon (the iter-8/9 self-reviewer flagged it overlapping the "O" glyph in LOCKED).

4. **Updated `Background - HoleCard.png`** — re-saved, possibly with different 9-slice config (file shrunk from 267KB → 207KB).

5. **Updated Rubik font SDF atlas** — additional glyphs.

## Remaining issues (fix in iter-10)

### A. LOCKED background only covers the rewards row

The LOCKED Card 2's BACKGROUND (the navy rounded rectangle) is short — only encompasses the rewards row. The LOCKED header and subhead text render ABOVE the BG, floating outside the panel frame.

**Likely root cause** (in `HoleCompleteWidgetBuilder.cs` and `HoleCompleteCardWidget.cs`):
- F4 from iter-9 sets `_cardLayoutElement.minHeight = 0` when locked, letting CSF resolve the card height from active children.
- BUT the Card's `ContentSizeFitter` is either driven by the wrong child OR the CardBG Image is stretch-anchored to `ContentRoot` (which doesn't contain header/subhead) instead of the whole Card.
- The architect's iter-8 Figma call expected: "Card auto-sizes to (active) header + subhead + dimmed rewards stack." Currently it auto-sizes to just the rewards stack.

**Fix:** ensure the Card's CSF + CardBG Image cover header + subhead + (dividers) + rewards when locked. The header / subhead must be inside whatever the CSF and CardBG measure. Two cleaner options:
- (a) Header + subhead become children of `ContentRoot` (the VLG that drives CSF) so their height contributes.
- (b) CardBG becomes a stretch-anchored child of the Card outer GO (not of ContentRoot), so it fills whatever height CSF gives the Card.

Investigate the current structure and pick the smaller fix. The iter-8 IMPLEMENTER_REPORT described "frame pattern — DarkenOverlay is a true stretch sibling of ContentRoot, not a VLG child" — verify CardBG follows the same pattern, AND verify header/subhead are inside ContentRoot.

### B. Bottom divider (Divider_BelowRewards / "Divider (2)") should not appear when LOCKED

In the LOCKED state, the PLAY button is hidden — so the divider that normally separates rewards from the button is dangling at the bottom with nothing below it.

**Fix:** In `HoleCompleteCardWidget.BindNextHole(locked=true)`:
- Add a `[SerializeField] GameObject _dividerBelowRewards` (or repurpose an existing wiring).
- Set it `SetActive(!locked)` so it hides when locked.

The Card 1 (success/failed) state always has a button, so this divider should remain active there. Only `BindNextHole(locked)` is affected.

## Two builder-side rules to ABSORB going forward (don't undo Cesar's manual work)

### Rule 1: Use the canonical Divider prefab

When rebuilding the widget, instantiate `Assets/Prefabs/UI/Divider.prefab` via `PrefabUtility.InstantiatePrefab(prefab, parent)` rather than building inline GameObjects. This:
- Preserves Cesar's exact divider styling (size, color, sprite, alpha).
- Future divider-style changes propagate by editing the prefab.

The current `BuildDivider(name, parent, dividerSprite)` helper should be replaced with `InstantiateDividerPrefab(name, parent)`.

Add a `LoadPrefab("Assets/Prefabs/UI/Divider.prefab")` helper alongside the existing `LoadSprite` and use it.

### Rule 2: Add this to lessons.md (general for future screens)

> **Prefer reusable canonical prefabs to inline-built components.** When the project has a `Assets/Prefabs/UI/*.prefab` for a recurring component (divider, button, modal frame, stat row, etc.), builders should INSTANTIATE the prefab via `PrefabUtility.InstantiatePrefab`, not build the component inline. This:
> - Centralizes the canonical style — one edit propagates to every consumer.
> - Survives manual designer tweaks across rebakes (the prefab IS the source of truth).
> - Reduces builder code size and duplication.
>
> When no canonical prefab exists yet, ask the architect/Cesar whether one should be created before building inline.

Add this to `tasks/lessons.md` under a new section.

## What I want back

1. Iter-10 builder + scene updates:
   - LOCKED Card 2 BG now covers header + subhead + rewards (Issue A fixed)
   - Divider_BelowRewards hidden when locked (Issue B fixed)
   - Builder now instantiates `Divider.prefab` instead of building inline dividers (preserves Cesar's manual work across rebakes)
2. Fresh S3 (failed-over-par) screenshot showing the LOCKED Card 2 with:
   - Full BG covering header + subhead + rewards
   - No dangling bottom divider
3. The regression-preservation table at the top of `IMPLEMENTER_REPORT.md` must remain intact and all-Y.
4. `tasks/lessons.md` updated with the canonical-prefab rule.
5. STATUS → `READY_FOR_SELF_REVIEW`.

## Out of scope (still don't touch)

- Card BG sprite + slicing (Cesar updated it manually — keep his version)
- Lock.png (Cesar updated it — keep his version)
- Rubik SDF (Cesar updated it — keep his version)
- Iter-9 F1-F5 fixes (PASS)
- Iter-8 PASSes (panel height 855 for UNLOCKED, panel centering, dress-up, etc.)
- Iter-5 button widths/slicing
- ShotPipeline / cup detection
