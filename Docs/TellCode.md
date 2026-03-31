# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-31) — Phase I1: Items Inventory Screen

Read the full spec: `Docs/Specs/PHASE_I1_ITEMS_SCREEN.md`

This phase creates the Items tab content (tab index 3) in the Inventory screen.
Items are consumable goods — starting with 3 tiers of Repair Kits.

### Implementation Order

**Step 1 — Data layer (create these files in order):**

1. `Assets/Data/Items.csv` — create per spec (3 rows: repairkit_common/rare/mythic)
2. `Assets/Scripts/UI/Inventory/ItemDataRuntime.cs` — data class (clone BallDataRuntime pattern)
3. `Assets/Scripts/UI/Inventory/PlayerItemData.cs` — player instance (clone PlayerBallData)
4. `Assets/Scripts/UI/Inventory/ItemDatabaseCSV.cs` — **clone** `BallDatabaseCSV.cs`, rename class,
   change sprite paths to `Items/Thumbnails` + `Items/Full`, update ParseRow for item fields
5. `Assets/Scripts/ItemManager.cs` — new singleton (no namespace), see spec for full API.
   Includes `UseBestRepairKit()` which replaces `RepairKitManager.UseBestKit()`.

**Step 2 — Migrate RepairKitManager → ItemManager:**

1. In `ClubDetailPanel.cs`, find `OnRepairClicked()` — replace `RepairKitManager.Instance.UseBestKit(...)`
   with `ItemManager.Instance.UseBestRepairKit(...)`. The return tuple changes from
   `(int newDurability, KitType kitUsed)` to `(int newDurability, string? itemUsed)`.
   Check `itemUsed != null` instead of `kitUsed != KitType.None`.
2. Same for `ClubCompareController.cs` — `OnRepairLeftClicked()` / `OnRepairRightClicked()`.
3. Search codebase for any other `RepairKitManager` references and update.
4. After all references are updated, **delete** `Assets/Scripts/RepairKitManager.cs` and
   `Assets/Scripts/Editor/RepairKitManagerSetup.cs` (and their .meta files).
5. Remove the RepairKitManager component from the Managers GameObject in the scene
   (the setup script will add ItemManager instead).

**Step 3 — UI scripts (clone from Ball equivalents):**

1. `ItemThumbnailCard.cs` — **clone** `BallThumbnailCard.cs`, rename class + methods.
   Key differences: reads `ItemDatabaseCSV`/`ItemManager`, shows rarity background
   from `Resources/Rarities/{rarity}`, shows rarity badge letter in top-left.
2. `ItemCarouselController.cs` — **clone** `BallCarouselController.cs`, rename class.
   Key differences: reads `ItemManager`, event is `OnItemSelected`, card type is
   `ItemThumbnailCard`, prefab fields renamed.
3. `ItemDetailPanel.cs` — **clone** `BallDetailPanel.cs` as starting point, then
   **heavily modify**: remove all stat bar fields/logic, add rarity/effect/proTip/
   brand/USE button fields. See spec for full SerializeField list.

**Step 4 — Editor scripts:**

1. `Assets/Scripts/UI/Inventory/Editor/ItemThumbnailCardBuilder.cs`
   - Menu: `GOLFIN/Build/Item Thumbnail Card`
   - Uses `AssetDatabase.CopyAsset()` to clone `BallThumbnailCard.prefab`
     → `Assets/Prefabs/UI/Inventory/ItemThumbnailCard.prefab`
   - Opens prefab, removes `BallThumbnailCard` component, adds `ItemThumbnailCard`
   - Preserves all child GameObjects (portrait, name, quantity badge, background, etc.)

2. `Assets/Scripts/Editor/ItemManagerSetup.cs`
   - Menu: `GOLFIN/Setup/Item Manager`
   - Creates `ItemDatabaseCSV` + `ItemManager` on Managers GO
   - Wires `itemsCSV` TextAsset from `Assets/Data/Items.csv`
   - Sets Script Execution Order: ItemDatabaseCSV = -90, ItemManager = -80

3. `Assets/Scripts/UI/Inventory/Editor/ItemDetailPanelAutoWire.cs`
   - Menu: `GOLFIN/Wire/Item Detail Panel`
   - Wires ItemDetailPanel + ItemCarouselController SerializeFields

**Step 5 — Scene hierarchy:**

Replace the "ITEMS — Coming Soon" placeholder in `ItemsContent` with the actual
carousel + detail panel hierarchy. See spec section 4B for the full tree.
The builder script or auto-wire should handle wiring.

**Step 6 — Localization keys:**

Add the 7 keys from the spec to the localization system.

### Verification
- Play → ITEMS tab → 3 cards + 3 empty slots
- Tap each card → detail panel updates
- USE button clickable (just logs for now — modal is Phase I2)
- Compare button always grayed out
- Go to Clubs → repair still works (ItemManager migration)
- Check console for no errors

---

## Previous Task (2026-03-30) — Fix Club Filter Bar: 8→6 Tabs + Unified Wedges

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

### Step 4: Fix filter buttons missing raycast targets (BUTTONS HAVE NEVER WORKED)

The filter buttons (ALLFilter, DRIVERSFilter, etc.) each have a Button component but **no Image
component** and `m_TargetGraphic: {fileID: 0}` (null). Without a Graphic with `raycastTarget = true`,
Unity's EventSystem can't detect clicks on them.

**Fix in `ClubFilterBar.cs` — add this to `Start()`, before `InjectDividers()`:**

```csharp
            EnsureButtonRaycastTargets();
```

**Add this new method:**

```csharp
        /// <summary>
        /// Ensures every filter button has an Image so the Button component can receive clicks.
        /// Adds a fully transparent Image if one is missing.
        /// </summary>
        private void EnsureButtonRaycastTargets()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                if (filterButtons[i] == null) continue;
                var go = filterButtons[i].gameObject;
                var img = go.GetComponent<Image>();
                if (img == null)
                {
                    img = go.AddComponent<Image>();
                    img.color = new Color(1f, 1f, 1f, 0f); // fully transparent
                }
                img.raycastTarget = true;

                // Wire as the Button's targetGraphic if missing
                var btn = filterButtons[i];
                if (btn.targetGraphic == null)
                    btn.targetGraphic = img;
            }
        }
```

This adds a transparent Image to each button at runtime, sets `raycastTarget = true`, and
assigns it as the Button's `targetGraphic`. This is safe to call even if an Image already exists.

### Step 5: Clean up old divider GameObjects

The 7 `FilterDivider` GameObjects visible in the hierarchy were created at runtime by the old code.
They'll be gone on next Play since `InjectDividers()` recreates them. No manual cleanup needed.

### Verification
- Play the scene, go to Clubs tab
- **Click each filter tab** — ALL, DRIVERS, WOODS, IRONS, WEDGES, PUTTERS should all respond to taps
- Confirm 5 dividers appear evenly spaced between 6 tabs (not 7 dividers for 8 tabs)
- Click WEDGES tab — should show all A.Wedge, P.Wedge, and S.Wedge clubs combined
- Click each other tab — should filter correctly
- ALL tab should still show everything
- Check console for `[ClubFilterBar] Filter set to:` log messages confirming clicks register

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
✅ DONE: 2026-03-30 — Fix filter button raycast targets: EnsureButtonRaycastTargets() added to ClubFilterBar.cs Start() — handled by Cesar directly.
✅ DONE: 2026-03-31 — Phase I1 Items Screen: Items.csv, ItemDataRuntime, PlayerItemData, ItemDatabaseCSV, ItemManager (replaces RepairKitManager), ItemThumbnailCard, ItemCarouselController, ItemDetailPanel, ItemManagerSetup, ItemThumbnailCardBuilder, ItemDetailPanelAutoWire, 7 ITEM_* localization keys. Scene hierarchy (Step 5) requires manual editor work — run GOLFIN/Setup/Item Manager + GOLFIN/Build/Item Thumbnail Card first.
