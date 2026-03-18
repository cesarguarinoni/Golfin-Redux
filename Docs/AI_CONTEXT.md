# AI Context — Golfin Redux

## Current Phase: Phase 2d — Compare & Swap ✅ (implementation complete, pending Unity test)

### Status

#### Phase 2b — Roster Detail Panel ✅ COMPLETE
- [x] Carousel fully working: correct sizes, all 12 characters, bounce animation, viewport clip fix
- [x] Full-body portrait loading via `portraitFull` CSV column
- [x] SELECT button fully working — interactable state, SELECTED text ✅
- [x] Level Up / Boost button interactable state ✅
- [x] Rarity badge background removed ✅

#### Phase 2c — Level Up Modal ✅ COMPLETE
- [x] LevelUpModalController — preview-only flow, nothing commits until CONFIRM
- [x] LevelUpModalBuilder — Editor script builds hierarchy (one-time use)
- [x] LevelUpModalAutoWire — wires all fields including CharacterDetailPanel.levelUpModal
- [x] SP color preview (orange bar for pending levels, blue confirmed)
- [x] Level text color (blue only during preview, white on open/reset)
- [x] Stat value split (StatValueCurrent / StatValueMax separate TMPs)
- [x] RP flow verified: modal → CharacterManager → RewardPointsManager → PersistentUIManager top bar
- [x] RewardPointsDebugPanel — backtick toggle, +/- RP buttons, runtime debug
- [x] FixBarImageTypes — one-shot editor script for filled bar Images
- [x] Modal not appearing bug fixed (GetMaxLevel simplified, null guard for CharacterLevelUpDatabase)

#### Phase 2d — Compare & Swap ✅ (implementation complete)
- [x] CompareController — state machine, carousel interception, fade/slide animation
- [x] CompareRightPanelBuilder — Editor script builds compare hierarchy
- [x] CompareAutoWire — wires all CompareController fields + CharacterDetailPanel.compareController
- [x] CharacterDetailPanel — early-return guard when IsCompareMode, OnCompareClicked wired
- [x] Top/bottom bar visibility fixed — ScreenManager.ApplyScreen() calls ShowBars/HideBars

### Workflow Rules
- **Push to GitHub after every change**

### Key Files
- `Assets/Scripts/UI/Roster/UI/CompareController.cs` — compare mode controller
- `Assets/Scripts/UI/Roster/Editor/CompareRightPanelBuilder.cs` — hierarchy builder
- `Assets/Scripts/UI/Roster/Editor/CompareAutoWire.cs` — field wiring
- `Assets/Scripts/UI/ScreenManager.cs` — screen transitions + bar visibility
- `Assets/Scripts/UI/PersistentUIManager.cs` — top bar RP display + bar show/hide
- `Assets/Scripts/Debug/RewardPointsDebugPanel.cs` — runtime RP debug panel

### Blockers
- None.

### What's Next
1. Run in Unity: `GOLFIN > Build Compare Right Panel` → `GOLFIN > Wire Compare Panel`
2. Test all compare mode flows: enter, right-column selection, swap, close
3. Phase 2e (TBD) — per architect spec
