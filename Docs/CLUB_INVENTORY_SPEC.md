# Club Inventory Screen — Implementation Plan

**Author:** Claude (Architect)  
**Date:** 2026-03-19  
**For:** Claude Code implementation  
**Task:** G-015 Club Inventory  
**Visual References:** Clubs_Screen*.png, Bags_Selection_Screen.png

---

## Overview

The Club Inventory is a new screen within the INVENTORY system. It follows the same pattern as the Character Roster (carousel + detail panel + compare mode) but with club-specific data and some layout differences.

**What's the same as Roster:** Carousel with thumbnail cards, detail panel with image + stats, compare mode (dual column), swap/equip flow, level up, pagination dots, rarity system.

**What's different:**
- Wrapped in an INVENTORY shell with tabs (CLUBS, BAGS, BALLS, ITEMS)
- Has a sub-filter bar (ALL, DRIVERS, WOODS, IRONS, A.WEDGES, P.WEDGES, S.WEDGES, PUTTERS)
- 6 stats instead of 4 (Power, Accuracy, Lie Resistance, Loft, Durability, Distance)
- Durability is a consumable stat (like stamina energy) — has current/max and turns red when low
- Distance is a derived stat shown as "180 yd" (not a bar, just a value)
- REPAIR button instead of BOOST
- EQUIP/EQUIPPED instead of SELECT/SELECTED
- "IN BAG 1" label showing which bag the club is equipped to
- Bag selection modal (CHOOSE A BAG — grid of bag slots, most locked)
- Compare mode shows stat DIFFERENCES (+55, -60) with color coding (green positive, red negative)
- Left panel has image on TOP and INFO text BELOW (not side by side like Roster)

---

## 1. Architecture — Reuse Strategy

### Reuse directly from Roster:
- `CarouselController.cs` — same carousel logic, different prefab and data source
- `StatBar.cs` — same stat visualization component
- Pagination dots system
- Arrow navigation
- `ModalController.cs` base class for repair/bag modals
- Rarity system (`RarityHelper`, `RarityStatCaps` equivalent for clubs)
- Resources-based sprite loading pattern

### Create new (club-specific):
- `ClubManager.cs` — singleton, manages owned clubs, equipped state, bags
- `ClubDatabaseCSV.cs` — CSV loader for club data
- `ClubData` / `PlayerClubData` — dual data model (template + player instance)
- `ClubDetailPanel.cs` — detail panel with 6 stats + durability + distance
- `ClubCompareController.cs` — compare with stat differences
- `ClubThumbnailCard.cs` — carousel card for clubs
- `InventoryScreenController.cs` — tab management (CLUBS/BAGS/BALLS/ITEMS)
- `ClubFilterBar.cs` — sub-filter for club types

### Do NOT duplicate:
- Don't copy-paste CarouselController — parameterize it or make it generic
- Don't duplicate StatBar — reuse the same component
- Don't duplicate the rarity background sprite loading

---

## 2. Data Model

### Clubs.csv (new)
```csv
id,name,type,rarity,brand,basePower,baseAccuracy,baseLieResistance,baseLoft,maxDurability,baseDistance,portraitSprite,portraitFull,maxLevel,info
club_driver_gf,Driver G&F,Driver,Common,G&F,80,30,10,12,100,250,DriverGF,BigDriverGF,119,"A reliable driver with balanced power and accuracy."
club_iron9_klyro,Iron 9 Klyro,Iron,Uncommon,Klyro,60,50,20,25,80,180,Iron9Klyro,BigIron9Klyro,139,"Precision iron with excellent control."
club_iron7_mireo,Iron 7 Mireo,Iron,Rare,MireO,80,30,15,15,100,180,Iron7Mireo,BigIron7Mireo,119,"Refined by MireO, this 7-Iron delivers precision spin with added carry."
club_awedge_fyloe,A. Wedge Fyloe,A.Wedge,Mythic,FYLOE,20,30,70,60,50,70,AWedgeFyloe,BigAWedgeFyloe,159,"Powered by FYLOE, this Approach Wedge brings strong spin with bold distance."
club_pwedge_royal,P.Wedge Royal Swing,P.Wedge,Legendary,Royal Swing,40,60,50,45,75,120,PWedgeRoyal,BigPWedgeRoyal,179,"The Royal Swing pitching wedge offers supreme accuracy."
club_putter_golfinx,Putter GolfinX,Putter,Supreme,GolfinX,30,90,30,5,120,30,PutterGolfinx,BigPutterGolfinx,199,"GolfinX's flagship putter, unmatched on the green."
```

### ClubType enum
```csharp
public enum ClubType
{
    Driver,
    Wood,
    Iron,
    A_Wedge,    // Approach Wedge
    P_Wedge,    // Pitching Wedge
    S_Wedge,    // Sand Wedge
    Putter
}
```

### PlayerClubData (runtime instance)
```csharp
public class PlayerClubData
{
    public string clubId;
    public int currentLevel;
    public int currentDurability;      // depletes with use
    public int maxDurability;          // can decrease with standard repair
    public int equippedBagSlot;        // 0 = not equipped, 1-10 = bag number
    public bool isEquipped;
    
    // SP system (same pattern as characters if clubs level up with SP)
    // Or simpler: stats increase automatically per level
    public int spentPower;
    public int spentAccuracy;
    public int spentLieResistance;
    public int spentLoft;
    public int totalSPEarned;
}
```

### Club Stats (6 stats + 1 derived)
| Stat | Bar? | Notes |
|------|------|-------|
| Power | Yes | Affects max shot distance |
| Accuracy | Yes | Affects error deviation |
| Lie Resistance | Yes | Affects shots from rough terrain (was "Recovery" in GDD) |
| Loft | Yes | Affects clubface angle / launch angle |
| Durability | Yes | Current/Max, turns red when low, repairable |
| Distance | No | Derived value shown as "180 yd" — calculated from Power + other factors |

---

## 3. Screen Structure

### Inventory Shell (new top-level screen)
```
InventoryScreen
├── Header ("INVENTORY")
├── TabBar
│   ├── ClubsTab (active)
│   ├── BagsTab
│   ├── BallsTab
│   └── ItemsTab
├── ClubsContent (shown when Clubs tab active)
│   ├── FilterBar (ALL | DRIVERS | WOODS | IRONS | A.WEDGES | P.WEDGES | S.WEDGES | PUTTERS)
│   ├── CarouselSection (reuse pattern)
│   │   ├── LeftArrow / RightArrow
│   │   ├── ScrollView → Viewport → Content → ClubThumbnailCards
│   │   └── PaginationDots
│   └── ClubDetailPanel
│       ├── LeftPanel
│       │   ├── ClubImage (top — big club photo)
│       │   └── InfoSection (bottom — INFO header + description text)
│       └── RightPanel
│           ├── ClubNameText
│           ├── StatusIcons (equipped icon, level-up ready icon)
│           ├── RarityLevelRow
│           ├── StatBars (Power, Accuracy, Lie Resistance, Loft, Durability)
│           ├── DistanceRow (icon + "DISTANCE" + value + "yd")
│           ├── ButtonRow (LEVEL UP + REPAIR)
│           ├── COMPARE button
│           ├── BagLabel ("IN BAG 1" — blue text, only if equipped)
│           └── EquipButton (EQUIP / EQUIPPED)
├── BagsContent (hidden — future)
├── BallsContent (hidden — future)
└── ItemsContent (hidden — future)
```

### Key Layout Difference from Roster:
- **Roster:** Left panel = full-body portrait only. Info/bio in right panel.
- **Clubs:** Left panel = club image (top ~60%) + INFO text (bottom ~40%). Stats in right panel.

This is because clubs have more stats (6 vs 4) so the right panel needs the full height for stats + buttons.

---

## 4. Compare Mode — Stat Differences

The club compare mode shows the DIFFERENCE between the two clubs' stats on the right column. This is NOT present in the Roster compare.

Example from reference:
- Left club: POWER 80
- Right club: POWER 20, shows **"-60"** in red next to the stat name

Rules:
- Positive difference → green text, "+N" format
- Negative difference → red text, "-N" format  
- Zero difference → no label shown
- Difference = rightClubStat - leftClubStat

```csharp
private void ShowStatDifference(TextMeshProUGUI diffLabel, int leftValue, int rightValue)
{
    int diff = rightValue - leftValue;
    if (diff > 0)
    {
        diffLabel.text = $"+{diff}";
        diffLabel.color = greenColor;  // green
        diffLabel.gameObject.SetActive(true);
    }
    else if (diff < 0)
    {
        diffLabel.text = $"{diff}";
        diffLabel.color = redColor;    // red
        diffLabel.gameObject.SetActive(true);
    }
    else
    {
        diffLabel.gameObject.SetActive(false);
    }
}
```

---

## 5. Durability & Repair

Durability is unique to clubs (characters don't have it). It works differently from other stats:

- **Current Durability** depletes each time the club is used in gameplay (1 per hole)
- **Max Durability** can decrease when using standard repair kits
- **Premium Repair Kits** restore durability without lowering max
- Durability bar turns **red** when current is low (below 25% of max)
- The "+1" in the reference (Clubs_Screen_-_Repaired.png) shows durability was just restored
- When durability reaches 0, club is unusable until repaired

### Repair Button Flow:
- Tap REPAIR → opens Repair Modal (similar to Level Up Modal)
- Show current durability, max durability
- Option: Use Standard Repair Kit (restores durability, lowers max by X)
- Option: Use Premium Repair Kit (restores durability, keeps max)
- Show available repair kits count
- Confirm/Cancel

**For Phase 1 (now):** REPAIR button logs to console. Implement the modal later.

---

## 6. Equip / Bag System

- Each club can be equipped to a **bag** (Bag 1 through Bag 10, most locked initially)
- Only Bag 1 is available at start
- When equipped, the detail panel shows "IN BAG 1" in blue text
- The action button shows "EQUIPPED" (gold) if equipped, "EQUIP" (gold) if not
- Tapping EQUIP opens the **Bag Selection Modal** (CHOOSE A BAG grid)

### Bag Selection Modal (from reference):
- Grid of 10 bag slots (5x2)
- Bag 1: shows bag thumbnail + "BAG 1" + status indicator (FULL if all club slots used)
- Bags 2-10: show "LOCKED"
- CANCEL button at bottom
- Selecting a bag equips the club to that bag

**For Phase 1 (now):** Only Bag 1 exists. EQUIP directly equips to Bag 1 without showing the modal. Show "IN BAG 1" when equipped. Bag selection modal is future.

---

## 7. Filter Bar

The sub-filter bar (ALL | DRIVERS | WOODS | IRONS | etc.) filters which clubs show in the carousel.

```csharp
public class ClubFilterBar : MonoBehaviour
{
    [SerializeField] private Button[] filterButtons;  // ALL, DRIVERS, WOODS, etc.
    
    public event Action<ClubType?> OnFilterChanged;  // null = ALL
    
    private ClubType? activeFilter = null;
    
    public void OnFilterClicked(int index)
    {
        activeFilter = index == 0 ? null : (ClubType)(index - 1);
        OnFilterChanged?.Invoke(activeFilter);
        UpdateButtonHighlights();
    }
}
```

CarouselController (or ClubCarouselController) listens to filter changes and repopulates with filtered clubs.

---

## 8. Club Thumbnail Card

Similar to CharacterThumbnailCard but shows:
- Club image (portrait)
- Rarity badge (letter)
- Level badge
- Club type + brand name (e.g., "IRON 7\nMIREO")
- Status icons (equipped, needs repair, level-up ready)
- Rarity background sprite

---

## 9. Navigation Integration

The Inventory screen needs to be accessible from the bottom nav bar. Currently the nav has: Home, Gacha(?), Play, Inventory(?), Characters.

The nav button that leads to Inventory needs to be wired in `PersistentUIManager` and `ScreenManager`. Add `ScreenId.Inventory` to the enum.

---

## 10. Implementation Order

### Phase A: Data Foundation
1. Create `Clubs.csv` with 6 clubs (1 per type, skip Wood and S.Wedge for now)
2. Create `ClubDatabaseCSV.cs` — CSV loader (mirror CharacterDatabaseCSV pattern)
3. Create `ClubData` / `PlayerClubData` data classes
4. Create `ClubManager.cs` singleton — owns clubs, equip state, bag management
5. Add Script Execution Order: ClubDatabaseCSV before ClubManager

### Phase B: Inventory Shell + Navigation
6. Create InventoryScreen under ScreensRoot
7. Add `ScreenId.Inventory` to ScreenManager
8. Wire nav button (the bag/inventory icon in bottom bar)
9. Create tab bar (CLUBS active, others placeholder)
10. Create filter bar (ALL active, filtering works)

### Phase C: Carousel + Detail Panel
11. Create ClubThumbnailCard prefab (reuse rarity backgrounds)
12. Wire carousel to ClubManager data, filtered by type
13. Create ClubDetailPanel hierarchy (left: image+info, right: stats+buttons)
14. Data bind all fields
15. Wire EQUIP button (Bag 1 only for now)

### Phase D: Compare + Swap
16. Implement compare mode (reuse CompareController pattern)
17. Add stat difference labels (green +N / red -N)
18. Implement SWAP (change which club is equipped)
19. CLOSE COMPARE returns to normal view

### Phase E: Placeholders
20. LEVEL UP button → logs to console (club level up modal is future)
21. REPAIR button → logs to console (repair modal is future)
22. Bag selection modal → skip for now (EQUIP goes directly to Bag 1)

---

## 11. Files to Create

| File | Purpose |
|------|---------|
| `Assets/Data/Clubs.csv` | Club data (6 clubs) |
| `Assets/Scripts/ClubManager.cs` | Singleton — club ownership, equip, bags |
| `Assets/Scripts/UI/Inventory/ClubDatabaseCSV.cs` | CSV loader for clubs |
| `Assets/Scripts/UI/Inventory/ClubData.cs` | ClubDataRuntime + PlayerClubData |
| `Assets/Scripts/UI/Inventory/ClubDetailPanel.cs` | Detail panel controller |
| `Assets/Scripts/UI/Inventory/ClubCompareController.cs` | Compare mode with stat diffs |
| `Assets/Scripts/UI/Inventory/ClubThumbnailCard.cs` | Carousel card |
| `Assets/Scripts/UI/Inventory/ClubFilterBar.cs` | Type filter |
| `Assets/Scripts/UI/Inventory/InventoryScreenController.cs` | Tab management |
| Editor scripts for hierarchy building + auto-wiring |

---

## 12. What NOT to Build Yet

- Club Level Up Modal (future — logs to console for now)
- Repair Modal (future — logs to console for now)
- Bag Selection Modal (future — equip to Bag 1 directly)
- Bags tab content
- Balls tab content  
- Items tab content
- Club SP allocation (decide later if clubs use SP or auto-level)
- Multiple bags (only Bag 1 for now)

---

## 13. Testing Checklist

- [ ] Navigate to Inventory screen from bottom nav
- [ ] CLUBS tab is active, shows carousel with 6 clubs
- [ ] Filter bar filters clubs by type (ALL shows all, IRONS shows only irons, etc.)
- [ ] Tapping a club card shows detail panel with correct data
- [ ] All 6 stat bars display correctly (Power, Accuracy, Lie Resistance, Loft, Durability, Distance)
- [ ] Distance shows as "180 yd" value (not a bar)
- [ ] Durability shows current/max format
- [ ] Club image and INFO text show on left panel
- [ ] EQUIP button equips club to Bag 1
- [ ] EQUIPPED button shows gold when club is in a bag
- [ ] "IN BAG 1" label appears when equipped
- [ ] Compare mode works — two columns with stat differences
- [ ] Stat differences show green (+N) and red (-N) correctly
- [ ] SWAP changes equipped club
- [ ] CLOSE COMPARE returns to normal view
- [ ] LEVEL UP and REPAIR log to console
- [ ] Rarity backgrounds show correctly on carousel cards
- [ ] Pagination dots and arrow navigation work
