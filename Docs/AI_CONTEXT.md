# AI Context — Golfin Redux

## Current Phase: Phase 2d — Compare & Swap ✅ COMPLETE

### Status

#### Phase 2b — Roster Detail Panel ✅ COMPLETE
- [x] Carousel fully working: correct sizes, all 12 characters, bounce animation, viewport clip fix
- [x] Full-body portrait loading via `portraitFull` CSV column
- [x] SELECT button fully working — interactable state, SELECTED text
- [x] Level Up / Boost button interactable state
- [x] Rarity badge background removed

#### Phase 2c — Level Up Modal ✅ COMPLETE
- [x] LevelUpModalController — preview-only flow, nothing commits until CONFIRM
- [x] LevelUpModalBuilder — Editor script builds hierarchy (one-time use)
- [x] LevelUpModalAutoWire — wires all fields including CharacterDetailPanel.levelUpModal
- [x] SP color preview, level text color, stat value split, RP flow verified
- [x] RewardPointsDebugPanel — backtick toggle, runtime RP debug
- [x] Modal not appearing bug fixed

#### Phase 2d — Compare & Swap ✅ COMPLETE
- [x] CompareController — state machine, carousel interception, fade/slide animation
- [x] CompareRightPanelBuilder — clones RightPanel exactly (fonts/colors/positions preserved)
- [x] CompareAutoWire — wires all 27 fields (0 failed after path fixes)
- [x] CharacterDetailPanel — IsCompareMode guard, OnCompareClicked, ShowCharacter() public method
- [x] Top/bottom bar visibility — ScreenManager.ApplyScreen() calls ShowBars/HideBars
- [x] BigRoster portrait hides immediately on compare enter (SafeSetActive, not CanvasGroup fade)
- [x] Placeholder hides immediately when right character is selected (SafeSetActive)
- [x] Stat bars force Image.Type.Filled in UpdateCompareStatRow
- [x] CanvasGroup pre-added in builder; GetOrAddCG uses == null not ?? (Unity null safety)
- [x] After swap: detail panel updates to show newly selected character (CommitSwapAndExit + ShowCharacter)

### Workflow Rules
- **Push to GitHub after every change**

### Key Files
- `Assets/Scripts/UI/Roster/UI/CompareController.cs` — compare mode controller
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — detail panel + ShowCharacter()
- `Assets/Scripts/UI/Roster/Editor/CompareRightPanelBuilder.cs` — clones RightPanel
- `Assets/Scripts/UI/Roster/Editor/CompareAutoWire.cs` — 27 fields, all paths verified against scene YAML
- `Assets/Scripts/UI/ScreenManager.cs` — screen transitions + bar visibility
- `Assets/Scripts/UI/PersistentUIManager.cs` — top bar RP display + bar show/hide
- `Assets/Scripts/Debug/RewardPointsDebugPanel.cs` — runtime RP debug panel

### Known AutoWire Paths (verified against ShellScene.unity YAML)
```
RarityRow children:
  RarityPanel/RarityRow/RarityText           (NOT RarityLabel)
  RarityPanel/RarityRow/LevelPanel/LevelText     (nested in LevelPanel)
  RarityPanel/RarityRow/LevelPanel/LevelTextMax  (nested in LevelPanel)

SelectButton text child:
  SelectButton/Text (TMP)                    (NOT "Text")
```

### Blockers
- None.

### What's Next
1. Phase 2e (TBD) — per architect spec in Docs/
2. Session startup: run `powershell -File Docs/generate_audit.ps1 > Docs/ARCHITECTURE_AUDIT.md`
