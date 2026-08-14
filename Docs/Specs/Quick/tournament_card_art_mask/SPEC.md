# QUICK SPEC — tournament_card_art_mask (round the card art's left corners with a mask, not per-image editing)

**Status:** SPEC_READY
**Author:** Architect (Cowork session), 2026-08-14, from Cesar: *"The image should have the left corners curved. To avoid having to edit each image before uploading, it would be ideal to just have a mask."*
**Size:** one prefab, no C# change expected. ~20 minutes.
**Related:** `Docs/Specs/Active/tournaments_unity_wiring/` (the remote art this exposed).

---

## 1. The problem

`tournament_image` is a 260×360 rect anchored flush to the card's top-left, sitting on top of **both** of the card's rounded left corners. Every image therefore renders square corners poking past the card's ~44 px radius. Now that art is uploaded from the dashboard, pre-rounding each file before upload is not an option — the mask has to live in the UI.

## 2. Everything needed already exists in the project

- **The sprite:** `Assets/Art/Original UI/Common/S_Common_BGCorner20Left.png` — guid `a007e88d378a6d04da972c3519543ec4`, `spriteBorder {25, 25, 0, 25}`, white silhouette with transparent corners. It rounds the **left two corners only** and leaves the right two square, which is exactly this card's shape (the image's right edge is interior, not a card edge). It is currently referenced by **zero** prefabs — it was authored for this and never used.
- **The pattern:** `Assets/Prefabs/UI/Shop/StaminaShopCard.prefab` is the same card archetype (identical 978×360 root, identical `d162244f…` sliced background) and already solves this: a `Mask` GameObject carrying a sliced `S_Common_BGCorner20*` Image with `m_ShowMaskGraphic: 0`, photo as its child. `StaminaShopHeroCard` → `HeroMask` and `StaminaMenuRow` → `Thumbnail/PhotoMask` are the same thing. Follow it; do not invent a shader (the project has no UI shaders and no soft-mask package, and adding one for this is not worth it).

## 3. The change — `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab`

On the existing **`tournament_image`** GameObject (260×360, anchored top-left, pivot 0,1 — leave its RectTransform alone):

1. **Remove the `RectMask2D`** (fileID `7799328802048983987`). It is axis-aligned clipping, it can never round a corner, it currently clips nothing, and `RectMask2D` alongside a stencil `Mask` on one object is a needless trap.
2. Re-point its **`Image`** at `S_Common_BGCorner20Left`, `m_Type: 1` (Sliced), colour white, `m_RaycastTarget: 0`, and set `m_PixelsPerUnitMultiplier` so the radius matches the card frame — **start at `0.36`** and confirm against `CardBackground` by eye. (The sprite's ~16 px radius renders at ppuMultiplier 1; effective radius = 16 ÷ ppuMultiplier, and the card's own corner is ~44 px.) A radius that is close but not equal reads worse than none — check it at 1170×2532.
3. Add a **`Mask`** component to the same object with `m_ShowMaskGraphic: 0`.
4. Add a child **`Photo`**: RectTransform stretched to fill (`anchorMin 0,0`, `anchorMax 1,1`, `sizeDelta 0,0`), `Image` Simple, `m_RaycastTarget: 0`, `m_PreserveAspect: 0` (art is authored at the card's 260×360 — do not letterbox it).
5. **Re-point `TournamentSelectionCard._tournamentImage`** (currently fileID `259090858572330456`) at the new `Photo` Image. That is a serialized-reference change in the prefab, not a code change — `SetCourseImage` (`TournamentSelectionCard.cs:185`) keeps working untouched.

## 4. Watch for

- **Stencil depth.** These cards live inside `TournamentSelectionScreen`'s scroll viewport, which uses `RectMask2D` (no stencil), so this adds the first and only stencil level — fine. Note `InventoryScreenBuilder.cs:321` records a past "stencil-buffer invisibility" bug from nesting; do not add a second `Mask` above these cards.
- **Draw calls.** A stencil `Mask` costs ~2 extra draw calls per card. Six cards is fine; if the list ever grows large this is the thing to revisit.
- **The art-absent path.** When there is no art the controller leaves the image disabled. With `ShowMaskGraphic: 0` the mask graphic never draws, so the left region correctly falls through to `CardBackground` — verify that still looks right rather than assuming it.

## 5. Acceptance

1. A card with **remote** art (`lomond_championship` currently carries `tournament-art/lomond_championship-8a7161e9de90.png`) shows top-left and bottom-left corners following the card's radius, right edge still square.
2. A card with **bundled** art (any other row) does the same.
3. A card with **no** art shows the card background in the art region, no white block, no visible mask graphic.
4. Screenshot at 1170×2532 comparing the art corner against `CardBackground`'s corner — they should be indistinguishable, not merely both rounded.
5. EditMode suite unchanged and green (this should touch no test).
