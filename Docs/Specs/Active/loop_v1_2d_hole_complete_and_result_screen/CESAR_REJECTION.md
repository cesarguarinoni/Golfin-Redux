# Cesar Rejection — 2026-05-11 (iter 5 follow-up)

Iteration 5 architect-pass approved. After eyeballing in LabScaffold play mode, Cesar identified several remaining fidelity issues.

## Issues to fix

### 1. Dividers from Figma are missing

The Figma frames show horizontal divider lines separating the modal regions (header / subhead / body / rewards / buttons). These dividers are a recurring component used in **other modals and panels throughout the game**.

**Reusable divider art already in the project — pick what fits Figma:**
- `Assets/Art/Settings/Divider.png`
- `Assets/Art/LoadingScreen/Divider.png`
- `Assets/Art/HomeScreen/Divider.png`
- `Assets/Art/ClubsInventory/DividerVertical.png` (vertical variant)
- `Assets/Art/ClubsInventory/DividerVerticalSmall.png`

Open the canonical Figma frames and identify each divider's exact position, thickness, and color, then place equivalent `Image` elements in the builder using the appropriate sprite. Use 9-slice if the sprite has borders.

### 2. Rewards row not centered or properly spaced

Current builder (line 422–427):
```csharp
var rewardsHLG = rewardsGO.AddComponent<HorizontalLayoutGroup>();
rewardsHLG.spacing = 32;
rewardsHLG.padding = new RectOffset(32, 32, 0, 0);
rewardsHLG.childAlignment = TextAnchor.MiddleLeft;
```

`TextAnchor.MiddleLeft` left-aligns the three rewards inside a 930-wide row. Figma centers them as a unit. Also `HorizontalLayoutGroup.childForceExpandWidth` defaults to true (per the iter-4 root cause from `BuildIconTextHeader`), which may be distributing the rewards across the full width.

**Fix:**
- Set `childAlignment = TextAnchor.MiddleCenter` so the cluster centers.
- Set `childForceExpandWidth = false; childForceExpandHeight = false;` (same iter-4 pattern).
- Verify spacing matches Figma exactly (pull from canonical frame).
- The three reward entries should sit as a tight centered cluster with equal gaps between them, NOT spread across the card.

### 3. Buttons falling outside the bottom of the panel

The card height is hardcoded to 600 (line 233 `cardRT.sizeDelta = new Vector2(978, 600)`) but the actual stacked content (header + subhead + body + rewards + dividers + buttons) clearly exceeds that. Buttons render below the card BG image.

**Fix:**
- Add a `ContentSizeFitter` with `verticalFit = PreferredSize` to the card, OR
- Recalculate the explicit card height from the sum of child preferredHeights + spacing + padding, OR
- Tighten the body row height (currently 200) and other slack to bring the total under 600.

Choose the path that's least invasive but verify the final card frame visibly contains ALL children (buttons fully inside the rounded card background, with bottom padding).

### 4. Green square on the left side

Looks like a placeholder thumbnail bleeding through. Check `Assets/Art/ResultScreen/Placeholders/Placeholder_HoleThumbnailSmall.png` — if it's a flat green square, swap it for either:
- A real hole thumbnail from `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole N.png` (cropped/scaled if needed), OR
- A neutral placeholder matching the card's visual style (transparent, or a subtle silhouette).

Cesar said this looks broken — it's not subtle.

### 5. Use HoleMaps images instead of empty/placeholder maps

The builder loads `Assets/Art/ResultScreen/Placeholders/Placeholder_HoleMap.png` (line 81). Cesar wants the **actual hole map** from `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {N}.png`, where N is `HoleContext.HoleNumber`.

**Fix:**
- For the current-hole map (Card 1), load `Lomond - Hole {HoleContext.HoleNumber}.png` at runtime via `HoleCompleteWidget.Show` (the data binding path).
- For the next-hole map (Card 2), load `Lomond - Hole {HoleContext.HoleNumber + 1}.png`.
- The builder can still load a placeholder at build time; the runtime binding in `HoleCompleteWidget.Show` should override with the correct sprite based on `HoleCompleteData`. Add `holeMap` and `nextHoleMap` Sprite fields to `HoleCompleteData` if not already there, or load via `Resources`/`AssetDatabase` keyed by hole number.
- Don't hardcode — read the hole number from `HoleContext` so it works for any hole.

### 6. Card 2 should show hole-select-style info

Currently the NEXT card body has: a small thumbnail (94×94), a map (156×200), and a single tip TMP ("Next hole tip — TBD"). Cesar says: "The bottom hole should have the info from the hole select screen. Use placeholder text if there is none but occupy roughly the same space."

**What the hole select screen shows** (see `Assets/Scripts/UI/HoleSelection/HoleCardController.cs` for the canonical layout):
- Course name
- Hole number + name
- Par
- Difficulty / hazards / yardage / whatever HoleCardController displays

**Fix:**
- Look at HoleCardController to see exactly what fields are displayed.
- Mirror that info block in the Card 2 NextBody, replacing the single "tip" TMP.
- Use placeholder text where data is unavailable (e.g., "—" or "TBD") — but occupy the same space proportionally so the layout doesn't shift.

## Retracted items

Cesar's original message included: "The bottom panel should have a Play button, not a Replay one (which currently has no image)" — he retracted: "You do seem to have fix the bottom button to say Play and be golden so disregard that one."

PLAY button on Card 2 is fine.

## What I want back

1. Updated screenshots (S2 success-at-par, S3 failed-over-par) showing:
   - Dividers visible at Figma-correct positions
   - Rewards centered as a unit
   - Buttons fully inside the card frame
   - No green square anywhere
   - Real hole maps (not green placeholders) on both Card 1 and Card 2
   - Card 2 info block resembling the hole-select layout
2. `IMPLEMENTER_REPORT.md` updated with:
   - Which divider sprite was chosen and where placed
   - The new card height (or ContentSizeFitter strategy)
   - The path used to load hole-N maps (and what happens if a number is missing)
   - What fields you mirrored from HoleCardController
3. STATUS → `READY_FOR_SELF_REVIEW`

## Out of scope (do not touch)

- Header / subhead alignment (iter-4 PASS)
- HUD bleed-through (iter-2 PASS)
- STROKES color tokens (iter-2 PASS)
- Sprite slicing on existing buttons / card BG (iter-5 PASS — already 9-slice)
- Button widths (iter-5 PASS — 348/307/353 from Figma)
- HoleCompleteDriver / ShotPipeline / cup detection
