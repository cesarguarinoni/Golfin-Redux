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
| Items Inventory | Not started |
| Shop | Not started |
| Gameplay | Not started |

## Week Summary (2026-03-24 → 2026-03-27)
- Phase E2: Club Repair One-Tap
- Phase E3/E3b: Bag Selection Modal + CSV-driven bag slots
- Phase E4: Bag ↔ Club management
- Phase F: Level Up Modal polish (SP allocation UI)
- Phase G: Character Compare stat differences
- Phase H: Balls Inventory (full screen — data layer, carousel, detail, segmented bars, compare)

## Next Up (Monday 2026-03-31)
- Items Inventory screen (simplest — flat list, quantity, use button, no stats)
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
- **Singletons:** CharacterManager, ClubManager, BallManager, BagManager, RepairKitManager, RewardPointsManager, ScreenManager, PersistentUIManager, CharacterDatabaseCSV, ClubDatabaseCSV, BallDatabaseCSV, BagDatabaseCSV
- **Platform:** Windows (PowerShell)
- **Ball stats:** -10 to +10 range, no rarity, no level
