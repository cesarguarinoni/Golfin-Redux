# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-03-25 (session 2)

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete |
| Club Inventory | 🔧 Compare panel done, user fixing images. Phase E (modals) not started. |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Shop, Bags, Balls, Items | Not started |
| Gameplay | Not started |

## Active Work
- Club Inventory compare mode functionally complete — user dressing up visuals
- `ClubCompareRightPanelBuilder`: now preserves ClubNameText + RarityLevelRow TMP formatting (font, size, autosize, style, alignment, rect) when re-running "Build Club Compare Panel" — saves snapshot before destroy, restores after clone
- Phase E (Bag selection modal, Repair modal, Club Level Up modal) is next for Clubs
- Menu cleanup + asset folder renames done — verify in Unity
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
