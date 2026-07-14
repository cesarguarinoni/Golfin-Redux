# gacha_history Stage 0 — Cesar rejection (2026-07-14)

Cesar rejected Stage 0 iter-2 on sight. Root cause: the container panel + several elements were
**built from scratch instead of REUSING the gacha_screen atoms** (Rule 19/22 violation), which
cascaded into stacked panels, missing outline/radius, wrong background, and stray nodes.

**Re-pull node `4079:18306` (get_design_context + get_screenshot) at step 0 and match it EXACTLY.**
A/B against `reference/gacha_history_node_4079-18306.png`. Reuse real atoms; do not fabricate.

## The 14 fixes

1. **Background** — using the NON-blurred rewards bg. Use the SAME blurred background as the gacha_screen
   GACHA tab (Game Screen Content: backdrop-blur ~10 + bg rgba(0,0,0,0.1) over the rewards bg). Match gacha_screen.
2. **Dark rectangle behind each club card** — a stray dark rect sits behind the BagClubCard. Remove it.
3. **CLOSE button too wide** — narrow it to the node width (it should not span nearly the full panel).
4. **Fonts too thin AND too small** (all text EXCEPT the club card, which is fine): DRIVER G&F / RARE-Lv /
   PULLED / STANDARD CLUBS / PULLS / TICKET / GACHA HISTORY header / filter chips. Match the node's font
   WEIGHT (SemiBold/Bold per node) and SIZE. The current divisor/weight is wrong — re-verify divisor
   (Lesson AK) and set each text's weight+size from node `4079:18306` / row node `13622:21105`.
5. **Rarity letter not colored by rarity** — the R/M badge letter must be colored by rarity
   (use `RarityHelper.GetRarityColor(rarity)`), not a flat color.
6. **Ticket image wrong angle** — Cesar re-imported the correctly-angled Figma ticket at
   **`Assets/Art/Shop/S_Store_Ticket_02.png`**. Use THAT sprite AS-IS (no rotation) for the row ticket icon.
7. **Row spacing** — the 2nd row almost overlaps the separator; it must have the SAME top gap as the 1st row.
   Equal, consistent gap between/around rows.
8. **GACHA HISTORY header icon is the wrong icon** — Cesar left the correct one at
   **`Assets/Art/Shop/History Icon.png`**. Use it for the header icon (and see #14 for the top-left chip).
9. **Header icon + title not centered** — center the "🕐 GACHA HISTORY" icon+title block within the panel.
10. **Container panel built from scratch — REUSE the gacha_screen wrap panel.** Use the SAME navy panel
    (gradient #133453→#091B33, **3px white/90% outline, radius 20**) as the gacha_screen wrap panel
    (node `4049:9123` / the GachaTabContent WrapPanel — find the real sprite/treatment and reuse it). It
    currently has NO rounded edges and NO outline because it was hand-built. Cite the reused sprite GUID.
11. **Scrollbar** (expanded — Cesar 2026-07-14):
    a. Must be **INSIDE the panel** (like Hole Selection and every other panel) — not on the panel edge/outside.
    b. Must use the **SAME scrollbar design as the Hole Selection screen** — reuse that scrollbar, don't restyle.
    c. Must **auto-hide when not needed** (content fits → scrollbar hidden).
12. **Stray arrow top-right of the panel** — remove it (it is opacity-0/invisible in Figma; do not render it).
13. **Multiple stacked container panels** — there is a semi-transparent 2nd panel under the main one and a
    3rd on top. There must be exactly ONE container panel. Delete the extras.
14. **Top-left history-access icon** — it does NOT have the gacha_screen fixes (uses the wrong image) and is
    NOT grayed out. Use the SAME fixed history chip as gacha_screen (Cesar's `Assets/Art/Shop/History Icon.png`)
    and gray it out (it's the current-screen indicator / inactive state), matching how gacha_screen fixed it.

## Reuse sources (Rule 19 — cite GUIDs read off the live object)
- Container panel: gacha_screen wrap panel (navy + white outline + radius) — reuse, don't rebuild.
- Row card: BagClubCard clone (node `13622:21105`) — but STRIP the LEVEL UP / REPAIR action buttons
  (history rows are display-only per the node) and color the rarity badge by rarity.
- History icon: `Assets/Art/Shop/History Icon.png`. Ticket: `Assets/Art/Shop/S_Store_Ticket_02.png`.
- Filter row: Shop STORE-tab FilterGroup (already reused). Background: gacha_screen blurred bg.

Also from the prior gate: build 1 CLUB + 1 BALL row variant (fork 4) — the 2nd row must be a BALL card,
not a duplicate club. Strip LEVEL UP/REPAIR from the display card.

STILL STAGE 0 (prefabs/static posing), HARD-GATED. After the redo it goes through the FULL review chain
(self-review → reviewer → red-team) BEFORE surfacing to Cesar.
