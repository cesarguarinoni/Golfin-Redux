# AI Context — Golfin Redux

**Last Updated:** 2026-03-20 by Claude (Architect)

## Current Phase: Club Inventory Phase C (Carousel + Detail Panel)

### Status

#### Phase 2 — Character Roster ✅ COMPLETE
- [x] Phase 2a: Carousel + Navigation
- [x] Phase 2b: Detail Panel (data binding, stats, bio, portraits, select)
- [x] Phase 2c: Level Up Modal (preview flow, SP allocation, confirm/cancel, RP integration)
- [x] Phase 2d: Compare & Swap (dual column, animations, swap selection)
- [x] Localization pass (all hardcoded text wired to CSV)
- [x] Status icons (selected, level-up ready, low stamina) on detail panel + carousel cards
- [x] Carousel pagination dots + arrow navigation
- [x] Resources-based sprite loading (portraits, rarities, homescreen)

#### Club Inventory Phase A — Foundation ✅ COMPLETE
- [x] ClubManager.cs, ClubDatabaseCSV.cs, ClubData.cs, PlayerClubData
- [x] Clubs.csv (6 clubs, 1 per type)
- [x] ClubThumbnailCard.cs + prefab builder
- [x] Club sprites in Resources/Clubs/Portraits and Resources/Clubs/Full

#### Club Inventory Phase B — InventoryScreen ✅ COMPLETE
- [x] InventoryScreen hierarchy, ScreenId.Inventory, nav wiring
- [x] Tab bar (CLUBS/BAGS/BALLS/ITEMS)
- [x] ClubFilterBar (8 type filters)
- [x] FilterBar visibility fix (nav bar offsets)
- [x] Archived 9 obsolete editor scripts

#### Club Inventory Phase C — Scripts complete, pending Unity run
- [x] ClubCarouselController.cs — subscribes to ClubFilterBar, fires OnClubSelected
- [x] ClubDetailPanel.cs — all 6 stats, equip toggle, localization, event wiring
- [x] ClubDetailPanelBuilder.cs — GOLFIN/Inventory/Build Club Phase C
- [x] ClubDetailPanelAutoWire.cs — GOLFIN/Inventory/Wire Club Detail Panel
- [x] 13 CLUB_ localization keys added to LocalizationText.csv
- [ ] ⚠️ Run builder + auto-wire in Unity Editor (manual step)
- [ ] ⚠️ Assign ClubThumbnailCard prefab + ClubFilterBar in Inspector
- [ ] ⚠️ Script Execution Order: ClubDatabaseCSV=-200, ClubManager=-100
- [ ] Visual polish to match reference images

#### Club Inventory Phase D — Planned
- [ ] Compare mode with stat differences (green +N / red -N)
- [ ] SWAP equipped club

#### Club Inventory Phase E — Planned
- [ ] Bag selection modal (future — direct equip to Bag 1 for now)
- [ ] Repair modal (future)

---

## AI Workflow

### Tools & Communication
- **Claude (claude.ai)** = Architect — analyzes references, writes specs, reviews architecture
- **Claude Code** = Implementer — full repo access, reads specs, edits files
- **Filesystem access** — Claude (architect) can read/write files directly at `C:\Users\cesar\GolfinRedux`
  - Can read all .cs scripts, CSV data, markdown docs
  - Can read images (under 1MB — use compressed versions)
  - Can write to Docs/ (TellCode.md, AI_CONTEXT.md, specs)
  - Cannot run Unity or PowerShell commands
- **TellCode.md** — Claude (architect) writes instructions to `Docs/TellCode.md`, Claude Code reads and executes
- **AI_CONTEXT.md** — shared memory, updated by both. Claude Code updates at session end.

### Handoff Process
1. Claude (architect) writes instructions → `Docs/TellCode.md` (direct filesystem write)
2. User tells Claude Code: "check TellCode" (two words)
3. Claude Code reads and implements
4. Claude Code marks tasks done in TellCode.md
5. Claude (architect) reads project files directly to verify — no uploads needed

### Screenshot Comparison Workflow
1. Claude Code builds/changes UI
2. User navigates to the screen in Play mode
3. Run `GOLFIN > Screenshot > Capture Game View` (saves to Assets/Screenshots/)
4. Run compression: `powershell -File Docs/compress_screenshots.ps1 "Assets/Screenshots"`
5. Claude (architect) reads compressed screenshot + reference from Assets/References/ and compares
6. Reference images must also be compressed (run compress script on References folders)
7. Max image size for filesystem read: 1MB

### No-Upload Workflow (NEW — 2026-03-20)
Claude (architect) now has direct filesystem access to the entire project. This means:
- **No more file uploads needed** — Claude reads scripts, CSVs, configs directly
- **No more copy-paste for instructions** — Claude writes TellCode.md directly
- **No more uploading screenshots** — Claude reads from Assets/Screenshots/ (compressed)
- **AI_CONTEXT.md can be updated directly** by Claude (architect)
- User's role is reduced to: navigating Unity, taking screenshots, and saying "check TellCode"

---

## Project Structure (Key Paths)

```
C:\Users\cesar\GolfinRedux\
├── Assets/
│   ├── Data/
│   │   ├── Characters.csv (12 characters)
│   │   ├── Clubs.csv (6 clubs)
│   │   ├── LevelUpCosts.csv (199 levels, universal)
│   │   └── CharacterLevelUpCosts.csv (character-specific, 89 rows)
│   │
│   ├── Resources/
│   │   ├── Characters/Homescreen/ (homescreen portraits)
│   │   ├── Clubs/Portraits/ (6 club thumbnails)
│   │   ├── Clubs/Full/ (2 + Placeholder)
│   │   ├── Portraits/FullBody/ (character full-body)
│   │   ├── Portraits/Thumbnails/ (character thumbnails)
│   │   └── Rarities/ (6 rarity backgrounds, shared)
│   │
│   ├── References/
│   │   ├── Roster Screen/ (7 reference images)
│   │   └── Inventory/ (club inventory references — to be added)
│   │
│   ├── Screenshots/ (captured via GOLFIN > Screenshot menu)
│   │
│   ├── Scripts/
│   │   ├── CharacterManager.cs (singleton)
│   │   ├── ClubManager.cs (singleton)
│   │   ├── UI/Roster/ (Data/, Editor/, Managers/, UI/)
│   │   ├── UI/Inventory/ (ClubData, ClubDatabaseCSV, ClubFilterBar, ClubThumbnailCard, InventoryScreenController)
│   │   └── UI/ (ScreenManager, PersistentUIManager, HomeScreenController, etc.)
│   │
│   ├── Localization/
│   │   └── LocalizationText.csv (key, English, Japanese)
│   │
│   └── Prefabs/UI/Roster/ (CharacterThumbnailCardGlowUp, StatBar, etc.)
│
├── Docs/
│   ├── AI_CONTEXT.md (this file)
│   ├── TellCode.md (architect → code instructions, checked each task)
│   ├── CLAUDE.md (Claude Code auto-reads at session start)
│   ├── CLUB_INVENTORY_SPEC.md (active spec)
│   ├── GAME_DESIGN_AGENT.md (for future design sprint)
│   ├── generate_audit.ps1 (architecture audit generator)
│   ├── compress_screenshots.ps1 (image compression for review)
│   └── (archived phase specs: PHASE_2B/2C/2D, LOCALIZATION_PASS)
│
├── tasks/
│   └── lessons.md
│
└── CLAUDE.md (project root — Claude Code reads at session start)
```

## Technical Notes

### Known Nav Bar Heights
```
TopBar:       321px, top-anchored
BottomNavBar: 196px, bottom-anchored
InventoryScreen: offsetMin.y=+196, offsetMax.y=-321
```

### Script Execution Order
```
CharacterDatabaseCSV:  -200
CharacterManager:      -100
Everything else:       default (0)
ClubDatabaseCSV:       needs adding (before ClubManager)
```

### Design Decisions
- CSV-first architecture (not ScriptableObjects)
- Resources.Load for sprites (no Inspector arrays)
- Event-driven UI (Action delegates, subscribe OnEnable/unsubscribe OnDisable)
- Namespace: Golfin.Roster for roster, Golfin.Inventory for clubs
- Localization: all new text uses LocalizationManager.Get("KEY")
- Rich text tags supported in localization CSV values
- Character bios stay in Characters.csv (bioJa column deferred)
- Platform: Windows (PowerShell, no bash/chmod/sed)

### Deferred Items
- Character compare stat differences (green +N / red -N) — apply when doing club compare
- Character bio Japanese translations (bioJa column)
- Full Japanese localization review by Ken
- Pagination dots styling polish
- Status icons in compare mode right panel

### Blockers
- None

### Session Startup Reminder (for Claude Code)
1. Run: `powershell -File Docs/generate_audit.ps1 > Docs/ARCHITECTURE_AUDIT.md`
2. Read: Docs/AI_CONTEXT.md, Docs/ARCHITECTURE_AUDIT.md
3. Read: Docs/TellCode.md for pending architect instructions
4. Read: Any active spec files in Docs/
