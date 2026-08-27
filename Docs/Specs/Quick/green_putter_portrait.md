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

## Verified in Unity play mode, through the real chain (2026-08-27)

Cesar: on-device confirmation not needed, Unity is enough. So this was proven through the actual
entry point rather than asserted — `SetClub(PutterIndex)` → `OnClubChanged` → `OnClubIndexChanged`
→ `EnterPutterMode` → `SyncClubSelectorToPutter` → `ClubContext.RequestSelection` → the live
populator → the bus fields `ClubButtonWidget` paints from. Harness:
`Assets/Scripts/UI/Editor/PutterSelectorVerify.cs` (GOLFIN ▸ Quality Tiers menu).

A static assertion could have confirmed the bag scan finds the putter, but NOT that
`RequestSelection` reaches a live populator — and that second half is the part that can actually be
broken in a real scene.

Hole 06, 7-club bag:

```
BEFORE (driver)  clubId=club_driver_golfin_common type=DRIVER idx=0 portrait=S_Menu_Driver_GOLFIN
AFTER  (putter)  clubId=club_putter_golfin_common type=PUTTER idx=6 portrait=S_Menu_Putter_GOLFIN   PASS
RESTORED         clubId=club_driver_golfin_common type=DRIVER idx=0 portrait=S_Menu_Driver_GOLFIN   PASS
```

Note the putter is at bag index **6**, not `PutterIndex` (3) — the trap this fix was written around.
