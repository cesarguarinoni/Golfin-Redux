# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-27) — Phase H: Balls Inventory Screen

Full spec: `Docs/SPEC_H_BallsInventory.md`

This phase adds the Balls tab to the Inventory screen — CSV database, manager singleton,
carousel of ball cards, and a detail panel with stat bars (positive=blue, negative=red).

**Read the full spec before starting.** It contains complete code for all files.
Execute sub-tasks in this order:

### Step 1: Data Layer (H1)
1. Create `Assets/Scripts/UI/Inventory/BallData.cs` — BallDataRuntime + PlayerBallData
2. Create `Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs` — singleton CSV loader
3. Create `Assets/Scripts/BallManager.cs` — singleton, owns player ball data
4. Create `Assets/Data/Balls.csv` — 2 balls (Golfin + Putt Ace)
5. Set Script Execution Order: BallDatabaseCSV before BallManager

### Step 2: Editor Setup (H5c)
6. Create `Assets/Scripts/UI/Inventory/Editor/BallManagerSetup.cs` — menu item to create scene GOs

### Step 3: UI Scripts (H2, H3, H4)
7. Create `Assets/Scripts/UI/Inventory/BallThumbnailCard.cs` — ball card component
8. Create `Assets/Scripts/UI/Inventory/Editor/BallThumbnailCardBuilder.cs` — prefab builder
9. Create `Assets/Scripts/UI/Inventory/BallCarouselController.cs` — from ClubCarouselController, remove filter bar
10. Create `Assets/Scripts/UI/Inventory/BallDetailPanel.cs` — simplified from ClubDetailPanel

### Step 4: Auto-wire + Localization (H5b, H6)
11. Create `Assets/Scripts/UI/Inventory/Editor/BallDetailPanelAutoWire.cs`
12. Add localization keys (BALL_OWNED, BALL_INFO, BALL_POWER, BALL_REBOUND, BALL_WIND_RESISTANCE, BALL_ROLL, BALL_SPIN)

### Key Differences from Clubs
- **No rarity** — no rarity badge on cards, no rarity label in detail panel
- **No level** — level badge repurposed for quantity display (x99 or ∞)
- **No durability, no equip, no repair, no level-up** — just a COMPARE button
- **Stats range -10 to +10** — bar fill = abs(value)/10, color = blue (≥0) or orange-red (<0)
- **Quantity display:** -1 in PlayerBallData = unlimited (show ∞), otherwise show x{qty}
- **No filter bar** — flat list of all balls

### Compare Button
Wire the COMPARE button but just `Debug.Log("Compare coming soon")` for now. Phase H8 later.

### Stat Bars
Start with smooth fill bars (same Image.fillAmount approach as clubs). The segmented visual style
from the mockup is deferred to a polish pass.

---

### Reminders
- Check `Docs/SPEC_H_BallsInventory.md` for complete code listings
- Balls sprites already exist at `Resources/Balls/Thumbnails/` and `Resources/Balls/Full/`
- BallCarouselController is a copy of ClubCarouselController with filter code removed
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-27 — Phase H Balls Inventory: BallData, BallDatabaseCSV, BallManager, Balls.csv, BallThumbnailCard, BallCarouselController, BallDetailPanel, BallManagerSetup, BallDetailPanelAutoWire, 7 localization keys

✅ DONE: 2026-03-26 — Phase G Character Compare stat diff labels: CompareRightPanelDiffBuilder, CompareController diff fields/methods, CompareAutoWire diff wiring

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients, visual fixes, filter dividers, arrows, viewport, fade, level text
✅ DONE: 2026-03-25 — Club Compare Phase D: ClubCompareController, builder, auto-wire, stat differences
✅ DONE: 2026-03-24 — Project cleanup: GOLFIN menu reorganized, Art/References folders renamed PascalCase, 5 editor scripts archived
✅ DONE: 2026-03-25 — Phase E1 Club Level Up Modal: PlayerClubData SP fields, ClubManager.SetLevel/RefreshStatValues, ClubLevelUpModalController, ClubDetailPanel/ClubCompareController wired, ClubLevelUpModalAutoWire, localization keys.
✅ DONE: 2026-03-26 — Phase E2 Club Repair One-Tap: RepairKitManager singleton, ClubManager.RepairClub/OnClubRepaired, ClubDetailPanel+ClubCompareController one-tap repair, localization keys, cleanup old modal files.
✅ DONE: 2026-03-26 — Phase E3 Bag Selection Modal: BagManager singleton, BagSelectionModalController, equip buttons wired, auto-wire script, localization keys.
✅ DONE: 2026-03-26 — Phase E3b Bags CSV + Data-Driven Bag Slots: BagDatabaseCSV, BagManager CSV integration, two-prefab bag grid, ClubManager multi-club-per-bag fix, bag name labels.
✅ DONE: 2026-03-26 — Phase E4 Bag ↔ Club management (assign/unassign from bag modal).
✅ DONE: 2026-03-26 — Phase F Level Up Modal polish (SP allocation UI).
