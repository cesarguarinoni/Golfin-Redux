# AI Context — Golfin Redux

**Project:** GOLFIN Redux — 3D mobile golf game, Unity (C#), iOS + Android  
**Team:** Cesar (solo dev), Ken (stakeholder, daily JP+EN Telegram reports)  
**Last Updated:** 2026-03-26

## Current Status

| System | Status |
|---|---|
| Character Roster | ✅ Complete |
| Club Inventory | ✅ Phase E1 (Level Up Modal) complete — ✅ Phase E2 (Repair Modal) code complete, pending Unity hierarchy build + wire run |
| Leveling Economy | ✅ Rarity-based (Common 10→39, Supreme 200→239), cost = level × 5 |
| Settings | Needs minor visual fixes |
| Shop, Bags, Balls, Items | Not started |
| Gameplay | Not started |

## Active Work
- Phase E2 (Repair Modal) — code complete. **Next: in Unity, build the ClubRepairModal hierarchy, run GOLFIN/Setup/Repair Kit Manager, then GOLFIN/Wire/Club Repair Modal.**
- Phase E3 (Bag Selection Modal) — spec pending. MAX_CLUBS_PER_BAG = 8.
- Next screen after Clubs: Settings (G-014) or Bags Inventory (G-016)

## Phase E2 Unity Steps (still needed)
1. In Unity, clone the ClubLevelUpModal hierarchy → rename to `ClubRepairModal`
2. Strip SP section rows, replace with: DurabilitySection + KitSection (StandardKitButton, PremiumKitButton, NoKitsMessage)
3. Add `ClubRepairModalController` component to root
4. Run **GOLFIN/Setup/Repair Kit Manager** (attaches RepairKitManager to Managers GO)
5. Run **GOLFIN/Wire/Club Repair Modal** (wires all fields + repairModal refs on Detail + Compare)

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
