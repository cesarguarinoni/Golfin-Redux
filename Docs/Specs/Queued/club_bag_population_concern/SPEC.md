# CONCERN (for Architect review) — club bag populated as a stopgap

**Raised by:** Cesar, 2026-06-22, at `map_view_aiming` (Order 352) close-out. **Cesar wants to go through this WITH the architect** — this doc is the flag, not a unilateral fix directive.

## What was done (the stopgap)
During map_view close-out, club selection in real gameplay showed **only the Driver** and the club distance read **0.00 yds**. Two stopgap fixes were applied to make the game testable:
1. `Assets/Scripts/ClubManager.cs` — `InitializeClubs()` now hard-equips a **default starter bag** (`club_driver_gf`, `club_wood_gf`, `club_iron7_mireo`, `club_putter_golfinx`) to bag slot 1. Previously every club was seeded **owned but unequipped** (`equippedBagSlot = 0`), so `BagManager.GetClubsInBag(1)` returned empty / Driver-only.
2. `Assets/Scripts/UI/HUD/ClubContextPopulator.cs` (+ `LabInventoryStub.cs`) — `SelectedDistance` is now set from the club's CSV `baseDistance`. The intended `PhysicsLabController.PushSelectedClubDistanceToContext()` (the "real physics carry" owner) **was never implemented** — it exists only in comments — so the value stayed 0.

## The concern / question for the architect
- **Why isn't the player's bag coming from the save state?** Cesar: "we have save states so not sure why not used." The hard-coded default bag in `ClubManager.InitializeClubs` is a stopgap; the real bag/equip should presumably load from the player's saved inventory. `ClubManager.InitializeClubs` currently `ownedClubs.Clear()`s and re-seeds every Awake with no persistence — so there's no save-state-driven bag at all in this path. Where should the saved bag be loaded, and why isn't it wired into the gameplay flow?
- **`PushSelectedClubDistanceToContext` never existed.** Decide whether `SelectedDistance` should be the static CSV `baseDistance` (current stopgap) or a real per-shot physics carry, and implement the intended owner (or delete the dead comments). The map-view rings already use the real physics carry (`ShotConeView.MaxCarryYardsForMap`), independent of this label.
- When the real save-state bag + distance source land, the two stopgaps above should become fallbacks (only when no saved bag / no real carry), not the primary path.

Not blocking; route through the architect before reworking.
