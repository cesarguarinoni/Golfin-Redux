# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-04-01

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete (incl. Phase G stat diffs) |
| Club Inventory | ✅ Phases C–F complete (carousel, detail, compare, level up, repair, bags) |
| Balls Inventory | ✅ Phase H complete (carousel, detail panel, segmented stat bars, compare) |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Items Inventory | ✅ Phase I complete — carousel, detail panel, ItemManager, Item Use Modal |
| Bags Inventory | ✅ Phase J complete — carousel, detail panel, equip bag, swap/equip club modal |
| Shop | Not started |
| Gameplay | Not started |

## Session Summary (2026-04-01)
### Phase J — Bags Inventory ✅ COMPLETE
- Bags.csv expanded: `description` + `fullImage` columns, Golfin bag added (Mythic, unlocked)
- BagManager: EquippedBagSlot, EquipBag(), OnEquippedBagChanged, auto-equip slot 1
- BagCarouselController: shows unlocked bags + locked pad cards (min 6), arrows hide on 1 page
- BagThumbnailCard: thumbnail, rarity badge, equipped icon, selection scale
- BagDetailPanel: full bag image, name, description, 8-slot club grid, equip bag button
- BagClubModalController: Swap/Equip mode, filter bar, excludes clubs already in the bag
- BagClubCard: `cardTopImage` field (NOT backgroundImage — wires to CardTop, not Background container)
- Editor auto-wire: BagsContentAutoWire + BagClubModalAutoWire (GOLFIN/Wire menu)

### Key bugs fixed this session
- Prefab `SetActive(false)` in BagSelectionModalController was poisoning in-memory prefab assets → all instantiated carousel cards appeared inactive
- Carousel showed all 10 CSV bags as slots; fixed to show only unlocked bags + pad to minCardCount
- Detail panel used `BagSwapClubCard` (no script component) → `GetComponent<BagClubCard>()` returned null → cards never initialized → always showed baked-in Driver G&F portrait. Fixed: use `BagClubCard.prefab` which has the component correctly

## Next Step
- Visual polish pass on Bags screen
- Settings visual fixes
- End-to-end playtest all inventory screens

## Week Summary (2026-03-24 → 2026-03-27)
- Phase E2: Club Repair One-Tap
- Phase E3/E3b: Bag Selection Modal + CSV-driven bag slots
- Phase E4: Bag ↔ Club management
- Phase F: Level Up Modal polish (SP allocation UI)
- Phase G: Character Compare stat differences
- Phase H: Balls Inventory (full screen — data layer, carousel, detail, segmented bars, compare)

## Next Up

- Visual polish pass on Bags screen
- Settings visual fixes
- End-to-end playtest all inventory screens

## Key Lessons (accumulated)
- `GameObject.Find` misses inactive objects — use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.scene.isLoaded` in editor scripts
- `FindObjectOfType<T>()` also misses inactive — always pass `true` (includeInactive) in AutoWire scripts
- ModalController assumes root GameObject stays active; only modalPanel child is toggled
- Anchor repositioning math only works at Canvas root — position modals in editor instead

## Reference Docs
- `Docs/INVENTORY_REFERENCE.md` — patterns, file locations, APIs for all inventory screens
- `Docs/SPEC_H_BallsInventory.md` — Balls spec (completed)
- `Docs/Rules.md` — design constraints, conventions
- `Docs/Tasks.md` — current checklist
- `Docs/TellCode.md` — architect → code instructions
- `CLAUDE.md` — Claude Code session rules + project architecture
- `Docs/ARCHITECTURE_AUDIT.md` — auto-generated file tree, singletons, events

## Quick Architecture
- **CSV-first** data, **Resources.Load** for sprites, **Event-driven UI**
- **Namespaces:** `Golfin.Roster`, `Golfin.Inventory`
- **Singletons:** CharacterManager, ClubManager, BallManager, BagManager, ItemManager, ItemDatabaseCSV, RewardPointsManager, ScreenManager, PersistentUIManager, CharacterDatabaseCSV, ClubDatabaseCSV, BallDatabaseCSV, BagDatabaseCSV
- **Platform:** Windows (PowerShell)
- **Ball stats:** -10 to +10 range, no rarity, no level
