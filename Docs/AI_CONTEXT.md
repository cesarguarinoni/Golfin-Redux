# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-03-25 (session 2)

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete |
| Club Inventory | 🔧 Phase E1 (Club Level Up Modal) ✅ code complete, pending Unity hierarchy setup |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Shop, Bags, Balls, Items | Not started |
| Gameplay | Not started |

## Active Work
- Phase E1 (Club Level Up Modal) code complete. Next: clone LevelUpModal hierarchy in Unity, rename to ClubLevelUpModal, attach ClubLevelUpModalController, run GOLFIN/Wire/Club Level Up Modal.
- Phase E2 (Repair) uses Repair Kits not RP. E3 (Bag Selection) MAX_CLUBS_PER_BAG = 8.
- Next screen after Clubs: Settings (G-014) or Bags Inventory (G-016)

## Key Files (read as needed)
- `Docs/Rules.md` — design constraints, Figma specs, conventions
- `Docs/Tasks.md` — current checklist
- `Docs/TellCode.md` — architect → code instructions
- `CLAUDE.md` — Claude Code session rules + project architecture
- `Docs/ARCHITECTURE_AUDIT.md` — auto-generated file tree, singletons, events
- `Docs/Game Design/` — changelog, formulas, naming convention, level spreadsheets

## Quick Architecture
- **CSV-first** data, **Resources.Load** for sprites, **Event-driven UI**
- **Namespaces:** `Golfin.Roster`, `Golfin.Inventory`
- **Singletons:** CharacterManager, ClubManager, RewardPointsManager, ScreenManager, PersistentUIManager, CharacterDatabaseCSV, ClubDatabaseCSV
- **Platform:** Windows (PowerShell)
