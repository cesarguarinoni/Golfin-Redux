# Quick — `green_putter_portrait`

**Reported:** Cesar, 2026-08-27 — "Putter auto equips on green but portrait does not change on the club selector."
**Status:** FIXED (pending Cesar's on-device confirmation).

## What was wrong

`PhysicsLabController.EnterPutterMode()` changes every part of the putting presentation — aim
cone, putter track, putt-path overlay, gauge units, hole-indicator units, club-button units, ball
selector dimming, central ball, and the `ClubSelectionBroadcast` putter-mode publish — but it
never touched the club selector's own **selection**.

`ClubButtonWidget.Refresh()` paints itself from `ClubContext.SelectedPortrait`, and nothing on the
auto-equip path was writing it. So the game switched to putting while the selector kept showing
whatever club the player last held.

## The fix

`EnterPutterMode()` now calls `SyncClubSelectorToPutter()`, which drives the selection through
`ClubContext.RequestSelection(bagIndex)` — **the same path a manual pick takes**
(→ `ClubContextPopulator.SelectByIndex` / `LabInventoryStub.SelectClubByIndex`).

Deliberately NOT `ClubContext.SelectedPortrait = putterSprite`. That would repaint the button while
leaving `SelectedClubId`, `SelectedTypeLabel`, `SelectedDistance` and `SelectedIndex` describing
the previous club — a selector that *looks* right and reads wrong to everything else on the bus.
Routing through `RequestSelection` moves all five together and raises `OnSelectedChanged` once.

`ExitPutterMode()` mirrors it with `RestoreClubSelectorAfterPutter()`, putting the player's own
club back when they leave the green.

## Two traps worth recording

1. **The bag index is not `PutterIndex`.** `PutterIndex` is a LAB club index (0..3); the bag is the
   player's equipped list and need not contain a putter at all. The putter is found by
   `bag.FindIndex(e => e.LabClubIndex == PutterIndex)`, and a bag with no putter leaves the
   selector alone with a log line rather than clamping to some other club.
2. **No recursion.** `RequestSelection` → `SelectByIndex` → `RaiseSelectedChanged` →
   `ClubButtonWidget.Refresh`, which only repaints. Nothing on that chain calls back into
   `SetClub`, so it cannot re-enter `EnterPutterMode`.

## Files

- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `SyncClubSelectorToPutter()`,
  `RestoreClubSelectorAfterPutter()`, `_bagIndexBeforePutter`, and the two call sites.

## Verification still owed

Play a hole to the green and confirm the selector portrait becomes the putter, then that it
reverts on the next tee. The bag is empty in edit mode, so this cannot be asserted statically.
