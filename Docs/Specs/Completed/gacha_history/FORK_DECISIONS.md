# gacha_history — fork decisions (Cesar, 2026-07-13)

Resolves SPEC §8 forks. Node render for the A/B: `reference/gacha_history_node_4079-18306.png`
(node `4079:18306`).

1. **Screen or modal → FULL SCREEN** (`ScreenId.GachaHistory`), and **KEEP the nav bar + background
   bars** visible. The CLOSE button navigates back to the Rewards Center GACHA tab. (Cesar: "As long
   as you keep the nav and background bars, go with suggestion.")
2. **Data source → FAKE/MOCK data** this order (real pulls don't exist yet).
3. **Sub-filter → reuse the Shop's STORE-tab `FilterGroup`** (the ALL/TICKETS/CLUBS/CHARACTERS/BALLS/
   ITEMS chip row — the same one hidden on the GACHA tab in gacha_screen Stage 1). It filters history
   rows **by reward type**. All 6 segments are LIVE. **TICKETS = tickets earned from gacha play**
   (Cesar). Wire the chips to filter the mock history rows by reward category.
4. **Reward-row variants → build CLUB + BALL** row variants this order (NOT character/item yet).
   The mock shows club rows; add a ball-card row variant with mock data.
5. **Figma→TMP divisor** — implementer verifies at step 0 (Lesson AK). Apply §2/§3 geometry verbatim
   from node `4079:18306` (the spec values ARE the node).

Delivery = STAGED, prefab-first, HARD-GATED (Stage 0 prefabs only → surface → Cesar review → next).
