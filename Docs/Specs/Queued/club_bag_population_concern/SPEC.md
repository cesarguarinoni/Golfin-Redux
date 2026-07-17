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

---

## Update — cross-referenced from Order 731 (2026-07-17), for architect pickup

Order 731 (`stat_lane_offdesign_retirement`) independently confirmed this concern from the bot / live-gameplay side:

- The LIVE stat path resolves the **swing** club from `ClubContext.SelectedClubId` (`LiveStatProviderHost.cs:188`), which `ClubContextPopulator` fills from `BagManager.GetClubsInBag(1)` — i.e. the hardcoded stopgap starter bag from this concern. Confirmed live during 731: the equipped bag is **4 clubs with no wedge** (`club_driver_gf, club_wood_gf, club_iron7_mireo, club_putter_golfinx`). `MapClubTypeToLabIndex` maps Driver/Wood→0, Iron→1, A/P/S_Wedge→2, Putter→3, so this bag covers lab indices {0,1,3} — **no index 2 (wedge)**.
- Consequence for the capture/QA bot: `BotDriver.SelectShot` assumes a lab wedge (index 2) that isn't equipped, so it now resolves the desired club to the **nearest available equipped club (the Iron7)** for approaches. That is correct behavior for the current bag, but it is a symptom of the same "no save-state-driven bag" root this concern raises.
- **Coupling to the open bot-rehab order** ("Rehab solo-completion bot for Hole 1", filed 2026-07-17): Hole 1's green sits ~5.6m below the surrounding fairway lip, so approaches land on the lip and the bot can't cleanly hole out. A proper save-state bag **that includes a wedge** would de-risk that endgame (a wedge drops softer into the green depression than the Iron7). So resolving this concern partly unblocks the bot-rehab's hardest part — worth weighing when the architect scopes either order.

Still an architect decision (Cesar + architect session), not a unilateral fix. Left queued deliberately; flagged here so the architect picks it up with full context. Related wiring: user-memory `reference_bot_live_club_switch`.
