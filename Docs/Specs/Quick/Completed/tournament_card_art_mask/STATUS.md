DONE — approved by Cesar 2026-08-15

# tournament_card_art_mask

**2026-08-15.** The card art's left corners are now masked to the card's radius. Files changed:
`Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab` plus one new sprite (below).
No C# change — `_tournamentImage` re-pointing is a serialized reference and `SetCourseImage`
(`TournamentSelectionCard.cs:185`) is untouched.

## Deviation from the spec, and why

The spec said reuse `S_Common_BGCorner20Left` (16px radius) at `pixelsPerUnitMultiplier 0.36`.
**I built that first and it was wrong — Cesar caught it on sight: "uneven and full of jaggies."**

A stencil `Mask` is 1-bit, so it cannot anti-alias. Scaling a 16px arc up to ~44px is a 2.75×
upscale, which turns every source pixel into a ~3px stair step. The measured arc showed the
signature plainly — insets ran `21,21,21 / 15,15,15,15 / 12,12,12`, runs of 3–4 identical rows,
against the card's own `33,30,28,26,24,22…` at 1–2px. That is the unevenness.

**Fix:** author the mask at final size and render it 1:1.
New sprite `Assets/Art/Original UI/Common/S_Common_BGCorner50Left.png` — 160×160, 50px left-corner
radius, 8× supersampled then box-downsampled for a clean 50% alpha crossing, border `52,52,0,52`,
PPU 100, **uncompressed** (a DXT/ASTC alpha would blockify the arc and reintroduce the jaggies).
Used `Sliced` at `pixelsPerUnitMultiplier 1`, so the corner slices land 1:1 on screen.
Result: max run length **2** (was 4).

## Radius: 50, and CardBackground did NOT need changing

Cesar: *"Corner radius is 50 in figma."* I initially reported the card frame as 44 and asked whether
to change the shared `Background - Next Hole.png` too; Cesar chose "50, and fix CardBackground too".

**That turned out to be unnecessary — my 44 was a measurement artifact.** Thresholding alpha at 128
under-reads an anti-aliased arc: my own known-50px sprite measures 43 by that method. A sub-pixel
circle fit, calibrated against that known-50 control (returns 49.2), gives:

| | radius |
|---|---|
| `Background - Next Hole.png` (CardBackground) | **50.2px** — already on spec |
| new mask sprite, as rendered on screen | **49.1px** |
| control (authored 50px) | 49.2px |

So the card frame was always 50 and the shared sprite is untouched — no ripple into HoleSelect or
anything else using it. Only the tournament card prefab and the new sprite changed.

## Acceptance

| # | Item | Result |
|---|---|---|
| 1 | Remote art card, left corners follow the card radius, right edge square | **PASS** — `lomond_championship` (`tournament-art/lomond_championship-8a7161e9de90.png`) |
| 2 | Bundled art card, same | **PASS** — gotemba / hirono / kasumigaseki |
| 3 | No-art card: card background shows, no white block, no mask graphic | **PASS** — `kisarazu_cup` driven through the real `SetCourseImage(null)` path; `Photo.enabled=false`, `showMaskGraphic=false` |
| 4 | 1170×2532 screenshot, art corner vs CardBackground corner indistinguishable | **PASS** — rendered art arc 49.1px vs card 50.2px, inside the method's ~0.8px calibration offset |
| 5 | EditMode suite unchanged and green | **PASS** — 1233 total / 1230 passed / 0 failed / 3 pre-existing skips (identical to before) |

Captured through the real player path: LOGIN → Home → TOURNAMENTS card → PLAY, all via the real
widgets' `onClick`. `Golfin.Dev.BotSessionOverride` supplied the session (editor-only, the project's
sanctioned harness path) since the auth gate needs credentials; disarmed afterwards.

## Screenshots — `screenshots/`

- `t7_all_three_states_1170x2532.png` — canonical: remote, bundled and no-art cards in one frame
- `zoom_remote_art_top_left.png` / `zoom_remote_art_bottom_left.png` — 8× corner crops
- `zoom_card_top_right_reference.png` — the card's own corner, for shape comparison
- `t7_list_remote_and_bundled_art.png` — top of the list

## Honest limitation

This is still a stencil mask, so the art edge has **no anti-aliasing** — the steps are 1px rather
than 3px, but they are steps. That matches every other stencil mask in the project
(`StaminaShopCard`, `StaminaShopHeroCard`, `StaminaMenuRow`). Genuinely smooth edges would need a
soft-mask shader/package, which the spec ruled out. If the 1px stepping bothers you at device DPI,
that is the next lever.

Also as specced: the `RectMask2D` on `tournament_image` is deleted, and a stencil `Mask` costs ~2
extra draw calls per card — fine at six, worth revisiting if the list grows large.
