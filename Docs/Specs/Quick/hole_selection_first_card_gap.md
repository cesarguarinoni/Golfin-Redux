# Quick spec — `hole_selection_first_card_gap`

**Filed:** 2026-09-09 (Architect via Cowork), from Cesar's device screenshot: Practice → Hole Selection
shows an empty card-sized gap above the NEXT card; the Hole 1 REPLAY card is missing "most of the
time, sometimes not".
**Not the Daily Mission pill.** `DailyMissionPillController` only touches Home and MissionSelection.

## Problem

`HoleSelectionScreenController.RebuildCards()` (`Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs`)
runs from `OnEnable` on every screen entry and does, in ONE frame:

1. `foreach (Transform child in cardsContent) Destroy(child.gameObject);` — `Destroy` is deferred to
   end of frame, so the 18 `HoleCard` instances from the PREVIOUS visit stay active children of the
   `VerticalLayoutGroup` for the rest of this frame ("corpses"). `OnDisable` only clears `_cards`,
   it never destroys them.
2. `Instantiate` 18 new cards under the same content.
3. `GpsPaintMotion.StaggerRise(this, rows)` → `LayoutRebuilder.ForceRebuildLayoutImmediate(parent)`
   (store_history §8) measures the column WITH the corpses, so new card 0 (Hole 1) is placed at slot
   19 (≈ −5,500 px). `UiMotion.Stagger` fires item 0 synchronously on its first `MoveNext`
   (inside `UiMotion.Run`), and `UiMotion.Rise` captures that `anchoredPosition.y` as `restY` and
   pins Hole 1 there when the tween ends. Rows 1–17 rise on later frames, after the corpses are gone
   and the layout group has re-laid the column, so they land correctly. Slot 0 stays empty.

Why it is intermittent:
- First entry after launch: no corpses (ShellScene has no design-time cards under `Content`) → fine.
- ModeSelection → HoleSelection is a `LayeredPush` push → `StaggerRise` is suppressed
  (`SuppressedByPush`, alpha forced to 1, no `Rise`) → fine.
- Home carousel PLAY → HoleSelection (Home moves are on the fade) on ANY re-entry → bug. This is
  the path Cesar hits.

Same bug class as store_history §8 (STORE card gap) and the `asset_loans_polish` trap recorded in
`claude/WORKFLOW_NOTES.md` ("StaggerRise after Destroying placeholder rows in the same frame
measures rest slots with the corpses still in the layout"). MissionSelection does not show it
because `push_arrival_hitch` fix 4 made it rebind-not-rebuild with a once-only sweep.

## Fix — Option A (recommended, one line)

In `RebuildCards()`, deactivate each corpse before destroying it so the layout group excludes it
from `rectChildren` immediately:

```csharp
foreach (Transform child in cardsContent)
{
    child.gameObject.SetActive(false);   // out of the layout NOW; Destroy is end-of-frame
    Destroy(child.gameObject);
}
```

Add a 2-line comment naming the trap (corpses + same-frame `StaggerRise`) so nobody "simplifies"
it back. No new fields, no scene/prefab edit, no change to the push path.

### Option B (not recommended here)
Port MissionSelection's rebind pool (`push_arrival_hitch` fix 4) to HoleSelection. Correct and
also saves the 18-instantiate allocation per entry, but it is a rewrite of `RebuildCards` for a
one-line bug. File as a deferral instead (see below).

## Polish atoms
Unchanged: `GpsPaintMotion.StaggerRise` (the §D6 arrival), `UiSelection.Bump` on tap. The fix
only makes the stagger measure the right rows.

## Acceptance

1. Device or Editor play mode: Home → carousel PLAY (Practice) → nav Home → PLAY again, ×3. Every
   time: `1 - Lomond … Hole 1` REPLAY card is visible at the top of the column, no gap; NEXT card
   expanded and centred as before. Log shows
   `[HoleSelection] holes paint(local) n=18 — staggered (first this entry)` and all 18 cards rise.
2. ModeSelection → Practice (push path) unchanged: `instant (push)`, no gap.
3. Filter pill LOMOND → YAITA → LOMOND: list repaints instantly (no stagger), no gap.
4. `GamePolishProbe` `mode card PLAY -> Practice` still passes.
5. No new `SerializeField`, no `.unity` / `.prefab` diff.

## Out of scope (→ Notion deferral)

- Rebind-not-rebuild pool for HoleSelection (Option B) — perf, not correctness.
- The hardcoded pill counts (`LOMOND 28/72`, `LADIES 18/18` …) — separate filter-wiring spec.
