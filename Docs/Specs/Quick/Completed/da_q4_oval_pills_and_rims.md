# Quick · `da_q4_oval_pills_and_rims` — every lint FAIL in the game is one 9-slice collapse

**From:** `design_consistency_audit` § 3.7 / § 4 (fix group Q4), approved by Cesar 2026-09-06. **Est:** S.

## What is wrong (all 12 prefab FAILs + 30 live FAILs, one rule: `9slice-collapse-*`)

| Site | Sprite state | Fix |
|---|---|---|
| `TournamentSelectionCard.prefab` `FreeEntryBadge`, `PaidEntryBadge` (+ their `PillFill`) | 44 px borders on 34–38 px height, `pixelsPerUnitMultiplier 4` → oval | ppu so that border ≤ height − 2 (≈ 5.2 for 34 px) — or a stadium sprite whose border equals half the height (the `stamina_boost_shop` lesson) |
| `GeneralShopCard.prefab` same four | same | same — it is the same badge atom; fix the atom once, both prefabs follow |
| `HoleCompleteModal.prefab` `…/Buttons/PlayNextButton` | 122 px borders on 120 px height, ppu 1 | ppu 1.05 (or re-bake at 120) |
| `HoleCompleteWidget.prefab` `ReplayButton` (122/120), `PlayButton` (130/120) | same | ppu 1.05 / 1.1 |
| `TournamentCloseButton.prefab` | 122/120 | ppu 1.05 |
| **LIVE `GeneralShopScreen`** — 15 cards × `HDiv` | a **2 px** divider drawn with a 40 px-border pill sprite (`collapse-x` ×15, `collapse-y` ×15) | the divider is a hairline: null-sprite `Image` (the linter allows ≤ 3 px flat hairlines) or `Image.Type.Simple` with a plain 1×N sprite — in `GeneralShopCard.prefab`, so all 15 follow |

Full detail lines: `Docs/Diagnostics/_capture/{TournamentSelectionCard,GeneralShopCard,HoleCompleteModal,HoleCompleteWidget,TournamentCloseButton,LIVE_GeneralShopScreen}_lint.json`.

## Rules

- Prefer the ppu change when the sprite is right and only the multiplier is wrong; re-bake only if
  a ppu that clears the collapse produces a cap-kink WARN (the two rules bracket the right value).
- Rendered geometry (pill height, button size, text) does not change — this is a corner fix.
- `HDiv` ×2 siblings share one name (see `GeneralShopCard.BindTicket`'s child walk) — change the
  prefab, not the runtime.

## Done when

- `UIFidelityLinter.LintPrefab` on the 5 prefabs: **FAIL 0**, no new WARN; `LintRoot` on the live
  `GeneralShopScreen` (real navigation, catalog loaded): **FAIL 0**. Before/after table quoted.
- 1:1 crops of one entry badge (Tournaments + Shop), one HoleComplete button, the close button,
  one shop-card divider — before/after side by side; corners round, dividers 2 px.
- Rest parity vs the audit's `screenshots/{TournamentSelectionScreen,GeneralShopScreen}_sheet.png`
  left side: the only differing pixels are the corners/dividers themselves (quote the diff bbox).
- `git status`: only the 5 prefabs (+ any re-baked sprite + its baker script). EditMode green.
