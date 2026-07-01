# CESAR REJECTION #3 — 1v1_result_rewards_display (Stage 0, after iter-10 ARCHITECT_REVIEW_PASS)

Cesar rejected iter-10. ONE fix — vertical position of the top content block. STATUS → `CESAR_REJECTED`, route back to implementer.

## The single fix

**Lower the entire top block so the RANK→bottom-separator gap is 24px.**

The block = WINNER/LOSER labels + portrait cards + USERNAME + RANK (everything in the Portraits area, above the first horizontal separator).

Right now the gap between RANK (bottom of that block) and the separator below it is ~63px — too big. Cesar wants it to be **24px rendered**. The way to achieve it is to move the WHOLE block DOWN within the Portraits slot (as shown in Figma, where the block sits lower): the empty space currently BELOW RANK moves to ABOVE the WINNER/LOSER labels. So:
- RANK bottom → first separator = **24px** (down from ~63px).
- The freed ~39px appears at the TOP, between RESULTS and the WINNER/LOSER labels — the block sits lower overall, matching the Figma node's vertical placement.

**Do NOT** just tighten the spacing between the sub-elements (WINNER↔portrait↔USERNAME↔RANK stay as they are — those gaps were already approved). This is a reposition of the whole group downward, not an internal-spacing change.

## Implementation notes
- Mechanism: the block should be bottom-anchored within the Portraits slot so RANK lands 24px above the separator, with the slack pushed to the top. Likely: reduce the `User1Info/User2Info` VLG `padding.bottom` (or the Portraits container bottom padding) to yield a 24px RANK→sep gap, AND add the equivalent space at the TOP of the Portraits slot (top padding / spacer above the WINNER/LOSER labels) so the block is shifted down rather than the whole panel shrinking.
- Pull Figma node `13274:877` (WIN) / `13275:2628` (LOSE) and match the block's vertical position — Cesar said "as shown in figma," so the top offset should match the node's proportion.
- **24px is a rendered gap.** Measure RANK-bottom → separator with GetWorldCorners on the built runtime GO and confirm 24px ±4. Do NOT cite a VLG config value as the rendered gap (Rule 6).

## Keep intact (do NOT regress — all verified good through 4 gates + Cesar's 8-item + NEW-MATCH fixes)
- Real MMModal clone (row-1 provenance label already corrected), real portraits, side-swap, WIN=bright/LOSE=dimmed.
- All prior fixes: WINNER/LOSER Regular, Vs./USERNAME Bold, NEW MATCH Regular #321506, RANK color-split, ÷1.2 fonts.
- The internal sub-gaps (card→USERNAME, USERNAME→RANK) and the NEW MATCH 24px/24px gaps and sep→HOLE / HOLE→course gaps — all stay as-is.
- Clean 1170×2532 capture, scene-safety (Physics/Scenes/MMModal untouched).

## Process
Re-run `[MenuItem]` builder, re-capture BOTH states CLEAN 1170×2532 (verify sips + upright + no chrome), add `## Rejection follow-up` with the MEASURED RANK→separator gap (must be 24px) + the new RESULTS→WINNER top gap + same-angle citation, update fidelity gap rows with rendered px, keep Weight column + Clone provenance current, set STATUS READY_FOR_SELF_REVIEW, report both paths + the measured RANK→sep gap to me. Iteration shape: `figma-fidelity:spacing`. Stage 0 only.
