# CESAR REJECTION — gacha_screen (Stage 0, after iter-3)

Cesar rejected on sight at the Stage-0 visual gate (interrupted before self-review). STATUS → `CESAR_REJECTED`.
These corrections are the **source of truth** and override the SPEC §2 token table AND any earlier reference
reading where they conflict. Re-pull Figma node `4065:6730` (file key `5gEAHjl6xAtW8iYY7NMvWd`) and match it.

**Standing rule (Rule 19 / SURFACE-DON'T-REBUILD): reuse the REAL existing components named below.
If a named source can't be located, set STATUS=IMPLEMENTER_BLOCKED and surface — do NOT hand-roll.**

## The 11 fixes (verbatim from Cesar + actionable interpretation)

1. **ENDS IN pill — clone the existing one.** "Ends in is supposed to be a pill. We already have a pill
   with time in it the Tournament Hole Selection screen. Just clone that one." → Locate the time pill on the
   Tournament Hole Selection screen and clone it for the countdown. Do NOT hand-build a pill.

2. **Guaranteed text goes directly over the banner — no blue background.** "Guaranteed text should not be on
   a blue background, it goes directly over the banner." → Remove the dark-navy background strip behind the
   "Guaranteed A-rank…/S-rank…" rows; the text composites directly on the banner art.

3. **Guaranteed-pulls counters are pills like the Rankings RP pills.** "Guaranteed pulls are pills, like the
   ones used for RPs in the Rankings Screen." → The "99 pulls" counters use the Rankings Screen RP-pill
   component. Clone that pill, don't rebuild.

4. **Whole banner + buttons inside a panel.** "The whole banner and buttons should be inside a panel (same
   blue panel we use everywhere adjusted for size)." → Wrap the banner card + PULL buttons in the standard
   blue panel used across the app (locate the shared panel sprite/prefab), sized to fit.

5. **Rules & rates button from Figma.** "Use rules and rates button from figma." → Extract the actual
   RULES & RATES button asset from the Figma node (`4055:1528` in `Rates` frame) via download_assets /
   export — do NOT use the placeholder "!" box.

6. **Rules & rates TEXT is outside the button.** "Rules and rates text is outside the button in reference." →
   The "RULES & RATES" label sits outside/beside the button, not inside it. Re-pull the node to get the exact
   text placement relative to the button.

7. **STANDARD CLUB 1 title spilling left.** "Standard club text is spilling to the left of the banner." →
   The title overflows past the banner's left edge. Contain it within the banner bounds (check anchor/pivot/
   left inset; likely spilling because it's not clipped to the panel from fix #4).

8. **History button — the Figma one, top-left between top bar and tabs.** "History button should be the one in
   figma and should be at the top left of the screen between the nav bar and the tabs." → Extract the real
   History button from Figma (`4146:79147` Rankings Container family) and position it top-LEFT, in the band
   between the top nav bar (REWARDS CENTER) and the GACHA/STORE/GIFTS tab strip. Currently it's a small box
   centered below the tabs — wrong sprite and wrong position.

9. **Each COST over its button.** "Each cost should be over the button they show the cost of and not pilled
   one atop the other." → "COST x1" sits directly above the PULL x1 button; "COST x10" directly above PULL
   x10. Currently both cost rows are stacked centrally — split them per-button.

10. **No dots — carousel has no dot indicators in the reference.** "This carousel does not have dots in the
    reference." → REMOVE the dot indicators entirely. (Overrides SPEC §2 `4049:10313-10317` and §3c dots —
    Cesar override from the real reference.)

11. **No scroll bar on this screen.** "This screen has no scroll bar." → Remove/disable any visible
    scrollbar in the GACHA tab content.

## Scope reminder
Still **STAGE 0 ONLY** (prefabs/static posing). No controllers, no GachaTicketManager, no CSV, no SaveData,
no carousel logic. Re-pull the Figma node at step 0; Cesar's corrections above win over the SPEC token table.
After the redo, surface the new canonical screenshot to Cesar at the Stage-0 hard gate.

---

## 2026-07-08 — EXACT MEASUREMENTS PROVIDED + dots resolved

The orchestrator pulled the full node `4065:6730` from Figma. **`MEASUREMENTS.md` is now the authoritative
spec** for this redo — every position/size/font/weight/color/gap + the layout tree + asset/reuse map is in
it. Work from MEASUREMENTS.md numbers, do NOT eyeball.

Key structural corrections vs the last build:
- **Wrap panel IS real** = node `4049:9123`, navy gradient `#133453→#091B33`, 3px white-90% border, radius20,
  w882, pb48. (The earlier hand-built panel was wrong.)
- **Guaranteed rows + 99-pulls pills + disclaimer belong OVER the banner art** (bottom of the art, green field) —
  they are children of the Banner (4049:10128), NOT in a strip below it.
- **Separator line (4055:1507) MUST be added** between the banner and the COST row (`reference/figma_separator.png`).
- **Only GACHA/STORE/GIFTS tab bar** on this screen — the STORE ALL/POPULAR/OFFERS + ALL/TICKETS/CLUBS filter
  rows must NOT show on Gacha.
- **History chip = silver Rankings chip + clock icon ONLY (no "HISTORY" text)** at absolute (48,252), 75×75.
- **Fonts ÷1.3, SemiBold/Medium only — never Bold.**
- **Cost order = COST → ticket icon → x1** (each cost cell centered over its PULL button).
- **RULES & RATES**: silver "!" chip (icon only) + separate "RULES & RATES" text label to the right, 15.4pt.
- **ENDS IN** = 9-sliced navy pill (reuse Tournament time pill) for proper rounded ends.

**DOTS — RESOLVED (Cesar 2026-07-08): KEEP the 5 dots (match Figma node 4049:10312).** 12px inactive /
16px active center.

---

## 2026-07-08 — iter-8 rejected on sight. SEVEN precise defects (Cesar):

1. **PULL buttons touch the panel bottom AND the sides.** Add margins: ~72px above the wrap-panel bottom
   (panel pb48 + Banner+Buttons pb24) AND horizontal insets so the buttons do NOT touch the panel's
   left/right edges (buttons sit at 42px inset within the 882 Banner+Buttons; wrap panel keeps its own edge).
2. **Banner art covers the WHOLE panel vertically.** It must END ~24px above the separator, with the navy
   wrap panel VISIBLE below the art (the gap-24 between Banner and Separator). Clip the art to the Banner;
   size the Banner so navy shows under it before the separator. (iter-8 art bleeds down over cost.)
3. **Use the STANDARD in-game separator** — `Assets/Prefabs/UI/Divider.prefab` (or the exact Divider used on
   the other Rewards Center / shop screens). NOT the `reference/figma_separator.png` I downloaded. Verify
   which divider the sibling screens use and reuse THAT.
4. **RULES & RATES text is BLACK and not centered under its icon.** Make it WHITE (#FFFFFF), 15.4pt SemiBold,
   and CENTER it directly under the "!" chip (Figma: white, text-center, w75, under the chip).
5. **The "!" rules icon is not the Figma image.** Use the real Figma "!" chip — `reference/figma_rules_chip.png`
   (node 4052:479 = silver Rankings chip with gradient "!"), or reuse the real silver Rankings chip prefab.
6. **History icon is surrounded by a flat gray square.** Use the real Figma history chip —
   `reference/figma_history_chip.png` (node 4146:79147 = silver gradient Rankings chip + clock, rounded +
   sheen), or reuse the real silver Rankings chip prefab + clock icon. NOT a flat gray box.
7. **Guaranteed text is LEFT-aligned.** Right-align it (Figma Pity Text = items-end) so the two "Guaranteed…"
   lines sit flush against the "99 pulls" pills on their right.

Keep every iter-7/8 PASS item (fonts ÷1.3 + weights, only GACHA/STORE/GIFTS tabs, dots, ticket counter,
cost order COST→icon→x1, art-fills-banner + transparent pity container, clean chrome-free capture).

---

## 2026-07-09 — iter-10 rejected. SEVEN defects (Cesar). Leftover placeholders + exact spacing:

1. **History icon has a LEFTOVER gray square** — a placeholder icon created in an EARLY iteration (the gray
   square) is still present behind/beside the real silver history chip. DELETE the leftover placeholder
   GameObject; only the real silver Rankings history chip + clock (figma_history_chip.png) must remain.
   Read back the hierarchy to prove exactly ONE history element exists.
2. **Info "!" icon has a LEFTOVER blue-bg white-"!" placeholder** — same story: an old placeholder ("!" white
   on blue background) is still there alongside the real silver "!" chip. DELETE the leftover; keep only the
   real silver Rankings "!" chip (figma_rules_chip.png, node 4052:479). Prove ONE info element exists.
3. **RULES & RATES text is not under the "!" icon.** Center the "RULES & RATES" label (15.4pt SemiBold white)
   directly UNDER the "!" chip (Figma: Rates row is below "Banner Name + !" row; RULES&RATES right-aligned,
   w75, sitting under the "!" chip at the banner's top-right).
4. **The whole banner/wrap panel is too far down.** It must start **24px below the tab strip** (Figma Content
   Container 4049:9017 gap = 24 between the tabs and the wrap panel). Reduce the tabs→banner gap to 24px.
5. **COST text overlaps the button tops + not centered over them.** Each COST cell must be **24px ABOVE** its
   PULL button (Figma Banner+Buttons: Cost frame → gap24 → Buttons frame) and horizontally **centered over**
   its button (cost cell 387 wide centered over the 387-wide button). Not overlapping.
6. **Guaranteed rows + pills + disclaimer are too far apart — gap must be 10px** (Figma Pity group 4055:2073
   gap=10; the two guaranteed lines gap=10; disclaimer sibling gap=10). Tighten to 10px.
7. **Disclaimer must be OVER the banner art too** (it's inside the Pity group inside the Banner in Figma —
   composited over the art's green field, same as the guaranteed rows). Not on the navy below.

Exact gaps to enforce (from Figma): tabs→banner 24px · cost→buttons 24px · pity internal gaps 10px ·
banner→separator 24px · separator→cost 24px · buttons→panel-bottom ~72px · buttons gap 24px · cost cells over buttons.
