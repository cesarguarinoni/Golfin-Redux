# AI Context — Golfin Redux

**Last Updated:** 2026-03-21 by Claude Code

## Current Phase: Club Inventory Phase C — Manual Wiring Needed

### Status

#### Phase 2 — Character Roster ✅ COMPLETE
- All sub-phases (2a-2d) complete
- Localization pass complete
- Status icons implemented
- Carousel pagination + arrows working

#### Club Inventory Phase A — Foundation ✅ COMPLETE
- ClubManager.cs, ClubDatabaseCSV.cs, ClubData.cs, Clubs.csv (6 clubs)
- ClubThumbnailCard.cs + prefab builder
- Club sprites in Resources/Clubs/

#### Club Inventory Phase B — InventoryScreen ✅ COMPLETE
- InventoryScreen hierarchy, tabs, filter bar, nav wiring
- FilterBar visibility fixed

#### New Leveling Economy ✅ COMPLETE (2026-03-21)
- CharacterManager: rarity-based starting + max levels (Common 10→39, Supreme 200→239)
- ClubManager: same rarity-based starting levels
- LevelUpCosts.csv: 240 rows (levels 1–240, cost = level × 5 RP, 1 SP each)
- CharacterLevelUpCosts.csv deleted (replaced by universal LevelUpCosts.csv)
- CharacterLevelUpDatabase parser: no limit, dictionary-based, works for 240 levels

#### Club Inventory Phase C — Code Done, Manual Wiring Needed ⚠️
Claude Code completed all Phase C code (2026-03-20):
- [x] ClubCarouselController.cs created
- [x] ClubDetailPanel.cs created (6 stats + durability + distance + equip)
- [x] ClubDetailPanelBuilder.cs + ClubDetailPanelAutoWire.cs created
- [x] Localization keys added (13 CLUB_ keys)
- [x] Guillermo ID fixed (char_alejandro → char_guillermo)
- [x] .gitignore updated (Assets/Screenshots/)

**MANUAL STEPS NEEDED (do these first tomorrow):**
1. Unity: Edit → Project Settings → Script Execution Order: ClubDatabaseCSV = -200, ClubManager = -100
2. Run: GOLFIN/Inventory/Build Club Phase C
3. Run: GOLFIN/Inventory/Wire Club Detail Panel
4. Assign ClubThumbnailCard.prefab to ClubCarouselController.clubCardPrefab in Inspector
5. Assign ClubFilterBar to ClubCarouselController.filterBar in Inspector
6. Test in Play mode: navigate to Inventory, verify carousel + detail panel

#### Club Inventory Phase D — Planned
- [ ] Compare mode with stat differences (green +N / red -N)
- [ ] SWAP equipped club

#### Club Inventory Phase E — Planned
- [ ] Bag selection modal (future)
- [ ] Repair modal (future)

---

### AI Workflow

#### Tools & Communication
- **Claude (claude.ai)** = Architect — filesystem access to C:\Users\cesar\GolfinRedux
- **Claude Code** = Implementer — full repo access
- **TellCode.md** — architect writes instructions, Code reads and executes
- **Figma** — company file accessible (key: hXFadl4O6HGKWakiEKgZbW), personal file blocked
- **Daily Report** — auto Telegram to Ken at 18:00 JST (chronological order, Japan holidays)

#### Key File Locations
```
Docs/TellCode.md          — architect → code instructions
Docs/AI_CONTEXT.md         — shared memory (this file)
Docs/CLUB_INVENTORY_SPEC.md — active spec
Docs/GAME_DESIGN_AGENT.md  — for future design sprint
Docs/Archive/              — completed phase specs
Assets/Screenshots/         — Unity captures for visual review
Assets/References/          — design reference images
C:\Users\cesar\golfin-tools\ — daily report script (outside project)
```

#### Figma Access
- Company file key: `hXFadl4O6HGKWakiEKgZbW`
- Personal file key: `5gEAHjl6xAtW8iYY7NMvWd` (BLOCKED — plan limitation)
- Use company file for all design pulls

---

### Project Structure

```
Assets/
├── Data/
│   ├── Characters.csv (12 characters)
│   ├── Clubs.csv (6 clubs)
│   └── LevelUpCosts.csv (240 levels, cost = level × 5 RP, 1 SP/level)
├── Resources/
│   ├── Characters/Homescreen/
│   ├── Clubs/Portraits/ (6 thumbnails)
│   ├── Clubs/Full/ (2 + Placeholder)
│   ├── Portraits/FullBody/ + Thumbnails/
│   └── Rarities/ (6 rarity backgrounds, shared)
├── Scripts/
│   ├── CharacterManager.cs, ClubManager.cs
│   ├── UI/Roster/ (complete)
│   ├── UI/Inventory/ (ClubData, ClubDatabaseCSV, ClubFilterBar, ClubThumbnailCard, 
│   │                   ClubCarouselController, ClubDetailPanel, InventoryScreenController)
│   └── UI/ (ScreenManager, PersistentUIManager, etc.)
├── Localization/LocalizationText.csv
└── References/ (Roster Screen/, Inventory/ — add club refs here)
```

### Script Execution Order
```
CharacterDatabaseCSV:  -200
ClubDatabaseCSV:       -200 (NEEDS SETTING)
CharacterManager:      -100
ClubManager:           -100 (NEEDS SETTING)
```

### Design Decisions
- CSV-first architecture
- Resources.Load for sprites (no Inspector arrays)
- Event-driven UI (Action delegates)
- Localization: LocalizationManager.Get("KEY") for all new text
- Platform: Windows (PowerShell)
- Use == null not ?? for Unity objects

### Font Size Ratio: Figma ÷ 1.4 = Unity TMP size
```
Figma 66px → Unity 47px  (EQUIP button)
Figma 51px → Unity 36px  (screen titles)
Figma 48px → Unity 34px  (INFO header)
Figma 45px → Unity 32px  (club name, rarity, level)
Figma 39px → Unity 28px  (button text, RP counter)
Figma 33px → Unity 24px  (stat names, stat values, body text)
Figma 30px → Unity 21px  (tab labels)
Figma 20px → Unity 14px  (filter bar labels)
```

### Figma Spacing Reference (Figma px → Unity px at ÷1.4 ratio)
```
FULL SCREEN: 1170 × 2532 Figma → ~835 × 1808 Unity equivalent
Top UI (persistent bar): 313 Figma → 224 Unity (TopBar is 321px in scene)
Bottom nav: 263 Figma → 188 Unity (BottomNavBar is 196px in scene)

INSIDE CONTENT AREA:
- Horizontal padding: 48 Figma → 34 Unity (each side)
- Tab bar to filter bar gap: 12 Figma → 9 Unity
- Filter bar to carousel gap: 12 Figma → 9 Unity
- Carousel section height: 353 Figma → 252 Unity (cards 343 → 245)
- Pagination dots row: ~24 Figma → 17 Unity
- Carousel to detail panel gap: 24 Figma → 17 Unity
- Detail panel fills remaining height

INSIDE DETAIL PANEL:
- Internal padding: 24 Figma → 17 Unity (all sides)
- Left panel width: ~46% of detail panel
- Right panel width: ~54% of detail panel
- Gap between left and right panels: 1px white border line
- Vertical gap between sections in right panel: 24 Figma → 17 Unity
- Gap between stat rows: 24 Figma → 17 Unity
- Stat icon width: 55 Figma → 39 Unity
- Stat icon height: 45 Figma → 32 Unity
- Stat bar height: 20 Figma → 14 Unity
- Stat bar gap from name: 5 Figma → 4 Unity
- Stat value column width: 115 Figma → 82 Unity
```

### Deferred Items
- Character compare stat differences (green +N / red -N)
- Character bio Japanese translations
- Full Japanese localization review by Ken
- Pagination dots styling polish
- Status icons in compare mode right panel
- Club Level Up modal
- Club Repair modal
- Bag Selection modal (grid picker)

### Blockers
- None
