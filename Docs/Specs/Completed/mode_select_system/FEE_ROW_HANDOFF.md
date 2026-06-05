# Fee-row collapsed layout — RESOLVED (2026-06-05)

> **Status: FIXED in-code/in-prefab.** This was originally written as a handoff; Cesar rejected the handoff ("If you know the issue why handing it to me?") and the row-spill was fixed directly. Kept for the record / root-cause trail.

## The bug (Cesar's words)
"The card is not resizing when it has both entry fee and reward. It keeps the size of having just one of them." → the 2nd (REWARDS) row rendered just below the card's bordered box.

## Root cause (proven by runtime diagnostics)
`ModeCard.prefab › CollapsedContainer › RewardsRow` had a **`LayoutElement` pinned to `minHeight = 100, preferredHeight = 100`**.

`RewardsRow` is a `VerticalLayoutGroup` holding `RewardSlot1/2/3` (each 84px). A `LayoutElement` has higher layout priority than the VerticalLayoutGroup on the same object, so the fixed `pref=100` **overrode the content-driven height**:

- 1 active slot (e.g. Driving Range "NO ENTRY FEE"): VLG wants 84 → forced to 100. Fits, looks fine.
- 2 active slots (Practice: ENTRY FEE + REWARDS): VLG wants `84 + 6 + 84 = 174` → **forced to 100**. The 2nd slot (84px) overflowed the 100px box and rendered **below** the card.

Because `RewardsRow` was frozen at 100 for every card, `CollapsedContainer` always computed to `TitleArea(124) + 16 + Divider(2) + 16 + RewardsRow(100) + pad(24+24) = 306` — identical for 1-row and 2-row cards. That is exactly "it keeps the size of having just one of them."

Diagnostic evidence:
```
practice       (2 rows)  RewardsRow H=100 pref=100  LE[min=100 pref=100]   RewardSlot1=84 RewardSlot2=84  cardH=306
driving_range  (1 row)   RewardsRow H=100 pref=100  LE[min=100 pref=100]   RewardSlot1=84              cardH=306
```
Both 306 → frozen.

## The fix (one property)
Cleared the fixed height on `RewardsRow`'s `LayoutElement`: **`minHeight = -1, preferredHeight = -1`**. Now the VerticalLayoutGroup drives `RewardsRow` from its active slots, the height flows up through `CollapsedContainer`'s `ContentSizeFitter`, and the card grows to fit:
```
practice       (2 rows)  rrH=174  colH=380  cardH=380   ← grew
driving_range  (1 row)   rrH=84   colH=290  cardH=290   ← shrank to 1-row size
```

Applied to `Assets/Prefabs/UI/ModeSelect/ModeCard.prefab` via `PrefabUtility.LoadPrefabContents` → set `LayoutElement.minHeight/preferredHeight = -1` → `SaveAsPrefabAsset` (sanctioned editor-API path, no raw YAML). Confirmed persistent in a fresh play session.

## Home card (`ModeHomeCard.prefab`) — not affected
The home card uses a **different, safe structure**: `EntryFeeRow` and `RewardsRow` are two *separate* sibling rows, each `pref=50` holding a single label+coin. The parent VLG sums whichever rows are active, so there is no "two slots in one frozen box" problem. No change needed.

## Verification capture
`screenshots/modeselect_card_resize_fixed.png` — all four full-screen cards contain both their rows; no spill. (PRACTICE x100/x50, 1V1 NO FEE/x200, DRIVING RANGE 1-row, MISSIONS NO FEE/x200.)
