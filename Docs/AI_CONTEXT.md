# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Kai (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-03-24

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete (carousel, detail, compare, level up, localization) |
| Club Inventory | 🔧 Phase D compare wired (46/46 fields) — play mode verify pending |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Shop, Bags, Balls, Items | Not started |
| Gameplay (3D course, shooting, physics) | Not started |

## Active Work
- Compare panel builder (clone approach) + auto-wire complete — needs play mode verification
- 5 Phase C visual fixes still pending verification (filter dividers, arrows, viewport, fade, level text)
- GOLFIN menu reorganization + Art/References folder renames done — needs Unity verify

## Key Files (read as needed, not every session)
- `Docs/Rules.md` — design constraints, Figma specs, conventions
- `Docs/Tasks.md` — current checklist for Claude Code
- `Docs/ARCHITECTURE_AUDIT.md` — auto-generated file tree, singletons, events
- `CLAUDE.md` — Claude Code session rules and project architecture
- `Docs/Game Design/` — changelog, formulas proposal, naming convention, level spreadsheets

## Quick Architecture Reference
- **CSV-first** data (not ScriptableObjects)
- **Resources.Load** for sprites (no Inspector arrays)
- **Event-driven UI** (Action delegates, OnEnable/OnDisable)
- **Namespaces:** `Golfin.Roster`, `Golfin.Inventory`
- **Singletons:** CharacterManager, ClubManager, RewardPointsManager, ScreenManager, PersistentUIManager, CharacterDatabaseCSV, ClubDatabaseCSV
- **Platform:** Windows (PowerShell)
