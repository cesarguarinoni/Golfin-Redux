# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-03-27 (end of week)

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete (incl. Phase G stat diffs) |
| Club Inventory | ✅ Phases C–F complete (carousel, detail, compare, level up, repair, bags) |
| Balls Inventory | ✅ Phase H complete (carousel, detail panel, segmented stat bars, compare) |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Items Inventory | ✅ Phase I1 + I2 complete — carousel, detail panel, ItemManager, Item Use Modal (club selection) |
| Shop | Not started |
| Gameplay | Not started |

## Session Summary (2026-03-31)
### Phase I1 (completed earlier)
- Data layer: Items.csv, ItemDataRuntime, PlayerItemData, ItemDatabaseCSV, ItemManager
- UI layer: ItemThumbnailCard, ItemCarouselController, ItemDetailPanel
- Editor scripts: ItemManagerSetup, ItemsContentBuilder, ItemRightPanelBuilder, ItemDetailPanelAutoWire
- Migrated RepairKitManager → ItemManager in ClubDetailPanel + ClubCompareController
- Added 7 ITEM_* localization keys (ITEM_RESTORES, ITEM_PRO_TIP, ITEM_INFO, ITEM_USE, ITEM_COMPARE, ITEM_OWNED, ITEM_DURABILITY)
- Fixed rarity colors: Uncommon=blue(0.29,0.56,0.89), Rare=green #50C878, Mythic=amber #FFC107

### Phase I2 (completed this session)
- ItemUseModalController — ModalController subclass, Open(itemId), BuildClubCards(filter), OnRepairKitUsed
- ItemUseClubCard — club card component (180×410) with stats, rarity badge, USE REPAIR KIT gold button
- Editor scripts: ItemUseClubCardBuilder, ItemUseModalBuilder, ItemUseModalAutoWire
- Added 3 localization keys: ITEM_SELECT_CLUB, ITEM_USE_REPAIR_KIT, ITEM_CANCEL
- Fixed compile error in ItemUseModalBuilder (removed dead soModal line with ?? Unity object violation)

## Next Step (Unity editor work — "dressing it up")
Run in order:
1. GOLFIN/Build/Item Use Club Card Prefab
2. GOLFIN/Build/Item Use Modal
3. GOLFIN/Wire/Item Use Modal
Then visual polish / layout tuning in Inspector.

## Week Summary (2026-03-24 → 2026-03-27)
- Phase E2: Club Repair One-Tap
- Phase E3/E3b: Bag Selection Modal + CSV-driven bag slots
- Phase E4: Bag ↔ Club management
- Phase F: Level Up Modal polish (SP allocation UI)
- Phase G: Character Compare stat differences
- Phase H: Balls Inventory (full screen — data layer, carousel, detail, segmented bars, compare)

## Next Up

- Visual polish on Items screen + Item Use Modal (layout, sizing, art)
- Settings visual fixes
- Review/playtest all inventory screens end-to-end

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
