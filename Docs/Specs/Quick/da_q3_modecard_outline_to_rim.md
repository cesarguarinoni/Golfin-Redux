# Quick · `da_q3_modecard_outline_to_rim` — ModeCard border is a `UnityEngine.UI.Outline`

**From:** `design_consistency_audit` § 3.3 (fix group Q3), approved by Cesar 2026-09-06. **Est:** S.

## What is wrong

Twenty `Outline` components, all in ONE prefab family: `Assets/Prefabs/UI/ModeSelect/ModeCard.prefab`
(5 live on ModeSelection) and `ModeHomeCard.prefab` (15 live on Home's carousel). Trap C5 /
linter `outline-border`: `Outline` is a blurred offset copy, not a crisp N px border. The node
(`13026:2366`, and the palette's "card family") draws the card as **r50, 3 px WHITE border, fill
`#133453 → #091B33`, drop-shadow `0 10 10 rgba(0,0,0,0.4)`**, with the PLAY button r20 + 2 px
`#FFE48B` rim.

## Fix

- Remove every `Outline` on both prefabs (and any on their instantiated children).
- Draw the border the way every other card in the family does: a **baked card sprite** with the
  border in the sprite (`bake_card` in the GPS builders / `make_gps_hub_panels.py` pattern — or
  reuse an existing atom from `UI_ELEMENT_PALETTE.md` if one already carries r50 + 3 px white; check
  `HoleCard` and `MissionCard`, which are the same family and have **no** `Outline`). If a bake is
  needed: `Docs/Scripts/make_mode_card_panel.py`, tokens from the node, forced `Sprite` import, 9-sliced
  with border ≥ 50 px so the linter's cap-kink rule stays quiet.
- Selected/expanded state: if the expanded card carries a different rim (gold?) read it off the
  node's expanded variant before assuming — the audit's § 3.8 colour note says the expanded title
  is gold; the rim may be too.

## Done when

- `grep -c "UnityEngine.UI.Outline\|m_Script: {fileID: 11500000, guid: <Outline GUID>" Assets/Prefabs/UI/ModeSelect/*.prefab` → 0 (quote the Outline script GUID you resolved).
- Lint on both prefabs: `outline-border` WARN 0, FAIL 0, no new `flat-fill`.
- Crop of one collapsed + one expanded ModeCard beside the node crop
  (`design_consistency_audit/screenshots/ModeSelectionScreen_sheet.png` right side); border reads
  as 3 px crisp at 1:1; Home carousel still lays out identically (rest parity vs
  `game_polish_a`'s Home baseline, 0 px outside the card rims).
- Sizes are **not** touched here (that is Q7); `ButtonPressFeedback` untouched.
