# Architect answers — iter-2 open questions (2026-07-02)

Clone provenance is now GENUINE (sprite GUIDs identical to TournamentSelectionScreen — Rule 19 satisfied). iter-2 routed to review with 5 self-reported FAILs; routing it back to the implementer for iter-3 with these decisions.

## Q1 — Panel height / screen fill → FULL-CANVAS
The Figma target (`reference/frame_selection_13156.png`, node 13156) is a **full-canvas** screen, NOT a bottom-sheet. Resize the cloned panel root:
- anchors `0,0 → 1,1`, `sizeDelta 0,0`, `anchoredPosition 0,0`.
- The gold curved header stays at top and now reads **"BOOST STAMINA"** (title label re-skin).
- Bottom nav bar is the standard NavBarContainer (already present).

## Q2 — StaminaShopCard sub-object mapping → RENAME the sub-objects
Rename the cloned card sub-objects to shop semantics (`tournament_image → StorefrontImage`, `tournament_name_text → ShopNameLabel`, etc.) AND wire the `StaminaShopCard.cs` SerializeFields to them via SerializedObject. Renaming (not just blind-wiring tournament-named objects) keeps the prefab maintainable and makes the fidelity read-back unambiguous. Card content per Figma row:
- Category + " · " + City/prefecture (e.g. "COCKTAIL BAR · KAMEYAMA, MIE")
- Shop name (Noto Sans JP Bold)
- Tagline (Regular)
- Hours + "View on Maps" link
- Daily Bonus chip (e.g. "Daily Bonus +15% Recovery")
- Energy gain "+20 / +60 STA" + RP price "R 200~800"
- FEATURED chip (top-right) only when Featured=true

## Q3 — Detail screen wiring → BUILD IT (attach controller + menu rows)
Attach `StaminaShopDetailScreenController.cs` to `StaminaShopDetailScreen.prefab`, wire hero image / info card / the cloned scroll area, and instantiate `StaminaMenuRow` prefabs into the scroll content. Match `reference/frame_detail_13330.png` (node 13330). This is the bulk of iter-3.

## Also required in iter-3
- Bind real shop data — cards must show ShopCatalog data (10 shops), not tournament placeholder OPEN/SIGN UP.
- Re-skin the tab strip: region tabs + prefecture sub-tabs (kill "PLAYING | CLOSED").
- Card tap → wire `_tapButton` so `OnCardTapped` fires and navigates to Detail (currently null → broken).
- Rule 11: every player-facing Button (card tap, tabs, BUY, back) gets `Golfin.UI.Polish.ButtonPressFeedback`.
- Capture BOTH screens (selection + detail) at 1170×2532 over the real nav flow.
