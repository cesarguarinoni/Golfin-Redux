# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-30) — Fix Club Filter Bar: 8→6 Tabs + Unified Wedges

The UI was manually changed from 8 filter tabs to 6. The 3 wedge tabs (A.WEDGES, P.WEDGES, S.WEDGES)
were unified into a single WEDGES tab. New tab layout:

**ALL | DRIVERS | WOODS | IRONS | WEDGES | PUTTERS** (6 buttons)

Two problems:
1. The filter dividers are still positioned for 8 tabs (the `filterButtons` serialized array likely still has 8 entries)
2. The index→ClubType mapping assumes 1:1 with the old 8-tab layout

### Step 1: Fix `ClubFilterBar.cs`

**File:** `Assets/Scripts/UI/Inventory/ClubFilterBar.cs`

**A) Update the doc comment** (line ~8):

Replace:
```csharp
    /// Buttons: ALL | DRIVERS | WOODS | IRONS | A.WEDGES | P.WEDGES | S.WEDGES | PUTTERS
```
With:
```csharp
    /// Buttons: ALL | DRIVERS | WOODS | IRONS | WEDGES | PUTTERS
```

**B) Update the comment on the buttonCount line** (line ~50):

Replace:
```csharp
            int buttonCount = filterButtons.Length; // expected 8
```
With:
```csharp
            int buttonCount = filterButtons.Length; // expected 6
```

**C) Add `IsWedgeFilter` property and replace `GetCurrentFilter()`:**

Replace the entire `GetCurrentFilter()` method at the bottom:
```csharp
        /// <summary>Returns null for ALL, or the active ClubType filter.</summary>
        public ClubType? GetCurrentFilter()
            => _activeIndex == 0 ? (ClubType?)null : (ClubType)(_activeIndex - 1);
```
With:
```csharp
        /// <summary>Returns null for ALL, or the primary ClubType for the active tab.</summary>
        public ClubType? GetCurrentFilter() => _activeIndex switch
        {
            0 => null,              // ALL
            1 => ClubType.Driver,
            2 => ClubType.Wood,
            3 => ClubType.Iron,
            4 => ClubType.A_Wedge,  // sentinel for unified WEDGES tab — check IsWedgeFilter
            5 => ClubType.Putter,
            _ => null
        };

        /// <summary>True when the active filter is the unified WEDGES tab (covers A/P/S wedges).</summary>
        public bool IsWedgeFilter => _activeIndex == 4;
```

### Step 2: Fix `ClubCarouselController.cs`

**File:** `Assets/Scripts/UI/Inventory/ClubCarouselController.cs`

In the `PopulateCarousel` method (around line 90-92), replace the filter query:

Replace:
```csharp
            List<PlayerClubData> clubs = filter == null
                ? ClubManager.Instance.GetAllOwnedClubs()
                : ClubManager.Instance.GetOwnedClubsOfType(filter.Value);
```
With:
```csharp
            List<PlayerClubData> clubs;
            if (filter == null)
            {
                clubs = ClubManager.Instance.GetAllOwnedClubs();
            }
            else if (filterBar != null && filterBar.IsWedgeFilter)
            {
                // Unified WEDGES tab — gather all 3 wedge types
                var a = ClubManager.Instance.GetOwnedClubsOfType(ClubType.A_Wedge);
                var p = ClubManager.Instance.GetOwnedClubsOfType(ClubType.P_Wedge);
                var s = ClubManager.Instance.GetOwnedClubsOfType(ClubType.S_Wedge);
                clubs = new List<PlayerClubData>(a.Count + p.Count + s.Count);
                clubs.AddRange(a);
                clubs.AddRange(p);
                clubs.AddRange(s);
            }
            else
            {
                clubs = ClubManager.Instance.GetOwnedClubsOfType(filter.Value);
            }
```

### Step 3: Fix the serialized `filterButtons` array in Unity

Open the ClubFilterBar component in the Inspector (it lives on the `FilterBar` GameObject under
`ContentArea > ClubsContent > FilterBar`). The `filterButtons` array must have exactly 6 entries
pointing to the 6 buttons in the new layout:

| Index | Button GameObject |
|-------|-------------------|
| 0     | ALLFilter         |
| 1     | DRIVERSFilter     |
| 2     | WOODSFilter       |
| 3     | IRONSFilter       |
| 4     | WEDGESFilter      |
| 5     | PUTTERSFilter     |

Remove any extra entries (the old A.WEDGES, P.WEDGES, S.WEDGES buttons).
If Code can't modify the serialized array programmatically, flag it for Cesar to fix in Inspector.

### Step 4: Clean up old divider GameObjects

The 7 `FilterDivider` GameObjects visible in the hierarchy were created at runtime by the old code.
They'll be gone on next Play since `InjectDividers()` recreates them. No manual cleanup needed.

### Verification
- Play the scene, go to Clubs tab
- Confirm 5 dividers appear evenly spaced between 6 tabs (not 7 dividers for 8 tabs)
- Click WEDGES tab — should show all A.Wedge, P.Wedge, and S.Wedge clubs combined
- Click each other tab — should filter correctly
- ALL tab should still show everything

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
✅ DONE: 2026-03-30 — Fix Club Filter Bar: 8→6 tabs + unified WEDGES. Updated ClubFilterBar.cs (comment, buttonCount, GetCurrentFilter switch, IsWedgeFilter property) and ClubCarouselController.cs (wedge union query). Step 3 (Inspector filterButtons array) requires manual fix by Cesar.
