# Cesar Rejection — 2026-05-12 (iter 7 reject)

Iteration 7 architect-pass approved. Thoroughly rejected by Cesar after eyeballing in LabScaffold play mode.

Cesar's overall directive: **RESPECT FIGMA SIZES AND POSITIONS**. The whole thing is off-spec dimensionally.

He attached a current-implementation screenshot and the canonical Figma reference. Implementer must pull every dimension from Figma, not eyeball.

## Issues (in priority order)

### 1. DimBackground lifecycle is broken

**Symptom:** When the game starts (no HoleComplete showing), the DimBackground is ACTIVE — it dims the whole screen even though the modal is hidden. If Cesar manually deactivates it, it does NOT reactivate when `HoleCompleteWidget` is shown.

**Likely root cause:** `HoleCompleteCardWidget.cs` has no Show/Hide method that toggles `_dimBackground` — DimBackground is built as a sibling of Card1/Card2 under the `HoleCompleteWidget` parent GameObject, but nothing toggles it in code. The widget root or DimBackground GO needs to be active=false on start, and `HoleCompleteDriver.ShowResultScreen` must activate it.

**Fix:**
- Inspect `HoleCompleteWidget` (the parent of Card1/Card2/DimBackground) — should be inactive by default; `ShowResultScreen` should activate it; dismissal (REPLAY/PLAY/RETRY click) should deactivate it.
- OR if the widget root must stay active (e.g., for raycasting), toggle the DimBackground GameObject directly inside `HoleCompleteCardWidget.Show*` / `Hide*` methods.
- Build-time default: DimBackground GO should be `SetActive(false)` after build.
- Verify with a screenshot showing the modal HIDDEN + gameplay HUD visible WITHOUT any dim overlay (S1 baseline).

### 2. Panels are too short

Figma reference shows cards at **~855px tall**. Current implementation cards are visibly much shorter (~half height).

**Fix:**
- Read the exact card height from the canonical Figma frame (use `mcp__d0f20b77-*__get_design_context` / `get_metadata`). Record the node ID.
- Either remove the ContentSizeFitter and use a fixed Figma height (≈855), or set `LayoutElement.minHeight = 855` and let CSF handle overflow only.
- Update body row, info column heights to match the proportional Figma layout (taller cards have more room for the map, stats, description).

### 3. Panels are not centered on screen

Current impl shows Card 1 starting at the very top of the screen with no breathing room, Card 2 below it. Figma reference has both cards centered horizontally AND vertically within the screen.

**Fix:**
- The `HoleCompleteWidget` root should have a centered VLG / layout so the two cards cluster centered on screen.
- Per Figma, there's also a "RESULTS" title at the top of the modal area and the cards stack below it with consistent gaps. But the spec may have deferred the RESULTS title — confirm from SPEC §E. If deferred, still center the two cards vertically as a unit.
- Pull the X/Y offsets and the inter-card spacing from Figma.

### 4. Buttons (REPLAY/PLAY) still go outside the panel

In the current-impl screenshot Cesar attached, PLAY is partially below the card bottom edge. The ContentSizeFitter solution from iter-6 was supposed to fix this — clearly didn't fully land or regressed.

**Fix:**
- Check the card's VLG padding bottom — should be ≥ button height / 2 + breathing room.
- Verify the button row's `LayoutElement.preferredHeight` matches the button's actual size (currently 120).
- Verify the card BG Image is actually sized to encompass all VLG children (CSF must be applied AFTER all children have correct preferredHeight).
- If `LayoutElement.minHeight = 855` is added per item #2, that gives buffer.

### 5. Panels are not properly sliced (corner stretching)

Cesar: "If you can't fix this let me know and I'll do it manually."

The card BG is supposed to be 9-sliced (iter-5 set spriteBorder to 50,50,50,50 on `Background - HoleCard.png`). The corners should stay at 50px regardless of card size. If they're stretching, the slicing isn't being applied OR the sprite is being rendered with `Image.Type.Simple` somewhere along the line.

**Fix:**
- Verify the card Image component on the live scene has `type = Sliced` AND the sprite asset has non-zero spriteBorder.
- Investigate whether `gameobject-component-modify` or the builder is silently flipping the Image type back to Simple after build.
- Run the builder fresh, inspect the YAML for `m_Type: 1` (Sliced) on the card Image, and confirm the `.png.meta` has `spriteBorder: {x: 50, y: 50, z: 50, w: 50}`.
- If after thorough investigation the slicing still won't render correctly, STOP and report — Cesar will do it manually in the Inspector.

### 6. Dividers are wider than the others present in the game

Cesar's directive: **just copy the existing divider implementation**, don't roll a new one.

**Canonical existing divider pattern** (from `Assets/Scripts/UI/Inventory/Editor/ClubCompareRightPanelBuilder.cs` line 442):

```csharp
private static void BuildDivider(Transform parent)
{
    var go = new GameObject("Divider");
    go.transform.SetParent(parent, false);
    AddLayoutElement(go, preferredHeight: DIVIDER_H);
    go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
}
```

`DIVIDER_H` is a small const (probably 1-2px). Just a white image at 10% alpha — no sprite, no slicing.

**Fix:**
- Replace the current `BuildDivider` helper with the pattern above. Drop the `Settings/Divider.png` sprite entirely.
- Use whatever DIVIDER_H value matches Cesar's other in-game dividers (look at the const in `ClubCompareRightPanelBuilder.cs`).
- Also check `ItemUseModalBuilder.cs` (refs TopDivider / BottomDivider) for the same pattern if there's any variation.

### 7. Map and info text are not centered

In the current-impl Card 2, the map is hard left and the "Par 4 / description" column is jammed against the map with no breathing room. Figma reference shows them centered as a horizontal unit within the card body, with consistent padding.

**Fix:**
- The body row HLG should center its children horizontally as a unit (currently UpperLeft → left-aligned).
- Apply iter-4's HLG fix: `childAlignment = MiddleCenter`, `childForceExpandWidth = false`, `childForceExpandHeight = false`.
- Pull the map width AND info column width from Figma. The info column needs MORE space than current (item #8).
- Vertical centering: the map and info text should align vertically as a unit within the body row.

### 8. Info text on lower (NEXT) panel has wrong title + insufficient width

Two problems:

(a) Current impl shows a small gold "Par 4" title above the description text. Cesar says: "**title in different font size that does not exist in reference**" — Figma does NOT show a separate "Par 4" title. The Par is in the subhead ("Lomond Country Club - Hole 2 - Par 4"). Remove the redundant title.

(b) The description text column is too narrow (looks like ~5-char-wide column in the current impl, where the words wrap aggressively into vertical noodles). Figma shows a wide column with ~3-4 readable lines of normal-width text.

**Fix:**
- Remove the `_nextHoleParText` field and its TMP child entirely. Description text is now the sole content of the NextBody info column.
- Widen the description TMP RectTransform / LayoutElement.preferredWidth to match Figma (probably ~500-600px in a 930-wide card with a smaller map).
- Verify word wrap is on so the text fills naturally.

## What I want back

1. Updated screenshots (S1 hidden-aiming, S2 success-at-par, S3 failed-over-par) showing every item above resolved.
2. **S1 specifically must show NO dim overlay** — gameplay HUD fully visible with no darkening.
3. `IMPLEMENTER_REPORT.md` updated with:
   - Card height pulled from Figma (with node ID)
   - DimBackground lifecycle fix explanation
   - Confirmation the canonical `BuildDivider` pattern was adopted (file/line reference)
   - Card centering strategy
   - Info column widening with new preferredWidth
   - Removal of the rogue "Par 4" title
   - Card BG slicing verification (or escalation note if slicing can't be fixed in code)
4. STATUS → `READY_FOR_SELF_REVIEW`

## Out of scope (still don't touch)

- Header / subhead alignment (iter-4 PASS)
- HUD bleed-through (iter-2 PASS) — though the DimBackground lifecycle is in scope
- STROKES color tokens (iter-2 PASS)
- Sprite slicing on buttons (iter-5 PASS — the BUTTONS are sliced fine; only the CARD BG slicing needs verification)
- Button widths 348/307/353 (iter-5 PASS)
- PLAY button on Card 2 (Cesar confirmed correct)
- HoleCompleteDriver/ShotPipeline/cup detection (beyond DimBackground toggle wiring)

## Notes for the Implementer

Cesar attached a side-by-side: current implementation vs. Figma reference. The current implementation looks visibly different — tiny cramped cards in the top-left, no dim toggling, "Par 4" stub title, vertical-noodle wrapped description, dividers too thick. The Figma is centered, tall, breathing room everywhere, clean dividers, real description text.

Stop eyeballing. Open the canonical Figma frames listed in SPEC §E, read every dimension, record every node ID in IMPLEMENTER_REPORT. If a dimension isn't in the SPEC, ask Cesar before guessing.

If after thorough work item #5 (panel slicing) still can't be made to work in code, escalate by saying so clearly in the report — Cesar offered to fix it manually.
