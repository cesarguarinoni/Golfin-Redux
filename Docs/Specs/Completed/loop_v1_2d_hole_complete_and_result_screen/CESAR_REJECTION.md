# Cesar Rejection — 2026-05-13 (iter 13 — DarkenOverlay corner masking)

iter-12 ARCHITECT_REVIEW_PASS approved, but:
- Cesar caught text floating above the LOCKED BG in live play and manually fixed via 144px top padding on LockedHeader's HLG (his change — preserve, don't touch).
- Cesar removed the DarkenOverlay placeholder Image (it was a semi-transparent sprite producing weak darkening) and now needs the overlay re-implemented to: (a) actually darken Card 2 significantly, (b) clip to the card's rounded corners so the dim doesn't square-out past the BG curves.

**Only ONE fix needed in iter-13: DarkenOverlay corner-curve clipping.**

## Hard constraints (same as iter-11, iter-12)

1. DO NOT run the builder.
2. DO NOT touch `HoleCompleteWidgetBuilder.cs`.
3. DO NOT touch sprites, fonts, or scene files. **ONLY** modify `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab` for the surgical DarkenOverlay fix.
4. DO NOT touch Cesar's 144px LockedHeader top padding fix — that's his manual work.
5. DO NOT reposition any GameObjects.
6. **NEW: The capture path must NOT mutate the scene file.** No `SetActive(false)` on ShotUI GOs that then saves the scene. iter-12 destroyed the scene this way. If you need a clean screenshot, raise the result Canvas sortingOrder (already at 32767 from iter-9) or use the existing `SuppressHUD()`/`RestoreHUD()` runtime path which restores in a finally block — never bake suppression into the scene state.

## The fix

The DarkenOverlay should be a child of Card2 (already is per the prefab). It needs to:
- Be a solid black (or near-black) Image
- Cover the full Card2 area when active
- **Clip to the card's rounded corners** (the BG sprite has a 50px corner radius)
- Have an opacity that visibly darkens (~0.6-0.7 — Cesar removed the previous placeholder; pick a value that looks right vs Figma)

### Recommended approach — sprite-driven rounded overlay (simplest, no Mask component needed)

Reuse the existing `Background - HoleCard.png` sprite for the DarkenOverlay:
1. On the DarkenOverlay Image component:
   - Set `sprite = Background - HoleCard.png` (same sprite as the card BG)
   - Set `type = Sliced` (preserves the 50px corner radius)
   - Set `color = (0, 0, 0, 0.65)` — solid black at 65% alpha. The sprite's rounded shape will only fill within the rounded area; corners stay clear.
2. The DarkenOverlay RectTransform should stay stretch-anchored to Card2 (anchors `(0,0)-(1,1)`, sizeDelta `(0,0)`).
3. Verify it's a sibling of ContentRoot inside Card2, and ordered AFTER ContentRoot so it renders on top.

If the sprite-driven approach doesn't visually work (e.g. the sprite has alpha gradients that tint the overlay weirdly), fall back to:

### Fallback approach — Mask component

1. Add a `Mask` component to Card2 (or to an intermediate masking container if Cesar's padding fix lives elsewhere). The Mask uses the card BG Image's alpha shape as the mask. `showMaskGraphic = true` so the BG still renders.
2. Move DarkenOverlay to be a CHILD of the masked container. Its rectangular shape gets clipped to the rounded BG alpha.
3. Set DarkenOverlay color to `(0, 0, 0, 0.65)`.

The sprite approach is cleaner — try it first.

## What I want back

1. ONE new screenshot showing the LOCKED Card 2 with:
   - Visible darkening that matches Figma reference (≈65% the brightness of Card 1)
   - Rounded corners on the dim — the dim does NOT square-out past the BG curve
2. A bbox check log per the new `tasks/lessons.md` rule: programmatically verify LockedHeader / Subhead / RewardsRow are all `Rect.Contains` inside Card2's world rect. Paste the log lines into IMPLEMENTER_REPORT.
3. `IMPLEMENTER_REPORT.md` iter-13 section with:
   - Acknowledgment of Cesar's two manual fixes (144px LockedHeader padding + DarkenOverlay placeholder removal)
   - Which approach you used (sprite-driven or Mask)
   - The bbox check log
4. STATUS → `READY_FOR_SELF_REVIEW`.

## Out of scope

- Anything outside the DarkenOverlay Image's sprite, color, and (if needed) sibling order
- Cesar's 144px LockedHeader padding — leave it
- Cesar's removal of the placeholder Image — leave it removed; you're adding the new sprite-driven overlay or the Mask approach instead
- Card 1, unlocked Card 2 NEXT, all other states
- ANY scene-level changes to `LabScaffold.unity`
