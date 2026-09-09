# Console sweep — every shell screen, per screen, before vs after

Run with `GOLFIN > Design Audit > Console sweep — per screen`
(`Assets/Editor/ConsoleSweepRunner.cs`). Both runs are the SAME tool on the same
machine, one Editor session apart: the tool was left untracked across a
`git checkout 36dc3d480`, so the only thing that differed between the two runs is
the code under test.

| | screens | errors | warnings | exceptions |
|---|---|---|---|---|
| before — `36dc3d480` | 17 | 1 | 4 | 0 |
| after — `f7dc82f18` | 17 | 1 | **2** | 0 |

The whole delta is one screen:

```
- MissionSelection    0     2    0
+ MissionSelection    0     0    0
```

both lines being

```
[Shimmer] missions.daily — NO HOST under MissionSelectionScreen
          (GpsPolishBuilder has not been run on this prefab)
```

which R2 removed at the source — the site, the host and both `Shimmer(...)` calls
are gone. Worth noting on its own: on this navigation path the placeholder was
already failing to find its host and warning about it twice per entry, so the
shimmer that Cesar saw sweeping in was ALSO logging a warning nobody was reading.

## The 1 error and 2 warnings that remain are the harness, not the game

All three belong to screens the sweep reaches with `ShowScreen` because they have
no player path from a fresh session, and all three say so themselves:

```
[TournamentHoleSelection] No SelectedTournamentId set.
[TournamentLeaderboard]   SelectedTournamentId is null/empty — normal nav always sets it.
[StaminaShopDetail]       Shop '' not found in catalog.
```

A tournament screen re-seated without an entered tournament, and a shop detail
re-seated without a selected shop. They are identical before and after, and they
are the correct behaviour for the state the harness put those screens in — the
right fix is a harness that seats the id first, not a screen that goes quiet about
missing state. Filed rather than silenced.

## Every screen covered

Real navigation (the player's own path): Home, GeneralShop, ModeSelection,
Inventory, Roster, SettingsOverlay, HomeReturn.

Re-seated: HoleSelection, MissionSelection, TournamentSelection,
TournamentHoleSelection, TournamentLeaderboard, Leaderboard, GachaHistory,
GachaPrizes, StaminaShopSelection, StaminaShopDetail.
