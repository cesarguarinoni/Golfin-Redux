# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-03-25 (session 2)

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete |
| Club Inventory | ✅ Phase E1 (Club Level Up Modal) complete — Phase E2 (Repair) next |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Shop, Bags, Balls, Items | Not started |
| Gameplay | Not started |

## Active Work
- Phase E2 (Repair Modal) — spec needed. Uses Repair Kits (from Items), not RP. Needs OnClubRepaired event.
- Phase E3 (Bag Selection Modal) — spec pending. MAX_CLUBS_PER_BAG = 8.
- Next screen after Clubs: Settings (G-014) or Bags Inventory (G-016)

## Lessons from Phase E1
- ModalController assumes root GameObject stays active; only modalPanel child is toggled. Never deactivate the root.
- GameObject.Find() misses inactive objects — use FindObjectOfType<T>(includeInactive: true) in all AutoWire scripts.
- Anchor repositioning math only works when modal is at Canvas root. If modal is parented inside a screen hierarchy, position it in the editor and remove all runtime repositioning code.

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
