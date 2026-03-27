# Tasks — Golfin Redux Current Checklist

Updated by both Claude (Architect) and Claude Code.

---

## ✅ Completed: Phase H — Balls Inventory (2026-03-27)
- [x] BallManager, BallDatabaseCSV, BallData, PlayerBallData
- [x] BallCarouselController (6 slots, empty card fill)
- [x] BallDetailPanel (portrait, name, quantity, segmented stat bars)
- [x] BallSegmentedBar (20 segments, centre=0, ±10 range, red/blue halves)
- [x] BallCompareController (RightPanel clone, DiffLabels, CloseCompareButton)
- [x] All editor auto-wire + builder scripts

---

## Active: Club Inventory

### Phase C — Visual Polish
- [x] Carousel, detail panel, data binding, stat icons, backgrounds
- [ ] Filter bar dividers — verify in Unity
- [ ] Carousel arrows — verify sprites loaded
- [ ] Viewport/card sizing — verify
- [ ] Fade overlay active at runtime — verify
- [ ] Portrait level text "Lv 10" only — verify

### Phase D — Compare + Swap ✅ FUNCTIONALLY COMPLETE
- [x] ClubCompareController.cs
- [x] ClubCompareRightPanelBuilder.cs (clone approach)
- [x] ClubCompareAutoWire.cs (46/46 fields wired)
- [x] ClubDetailPanel.cs updated with compare integration
- [x] Stat differences (green +N / red -N)
- [ ] User fixing compare panel images/visual polish

### Phase E — Modals
#### E1 — Club Level Up Modal ✅ COMPLETE

#### E2 — Club Repair One-Tap ✅ COMPLETE
- [x] RepairKitManager singleton (UseBestKit auto-select: Standard ≤50%, Premium >50%)
- [x] ClubManager: OnClubRepaired event + RepairClub() method
- [x] ClubDetailPanel + ClubCompareController: one-tap repair (no modal)
- [x] Localization keys
- [x] RepairKitManager setup script
- [x] Cleanup: delete old modal files

#### E3 — Bag Selection Modal
##### E3a — Modal + BagManager ✅ COMPLETE
- [x] BagManager singleton
- [x] BagSelectionModalController (5×2 grid)
- [x] Wire Equip buttons → open modal
- [x] BagSelectionModalAutoWire editor script
- [x] BagManagerSetup editor script
- [x] Localization keys
- [x] Kai: Created BagSlotPrefab + BagSlotLockedPrefab, styled, added RarityBadge

##### E3b — Bags CSV + Data-Driven Slots (spec ready → TellCode.md)
- [ ] Bags.csv data file (id, name, rarity, thumbnail, unlocked)
- [ ] BagDatabaseCSV singleton (loads CSV, provides BagDataRuntime)
- [ ] BagManager updated to use CSV unlock data (remove hardcoded array)
- [ ] BagSelectionModalController: two prefabs (unlocked/locked), data binding (rarity bg, badge, thumbnail)
- [ ] Wire bagSlotLockedPrefab reference
- [ ] BagDatabaseCSVSetup editor script

---

## Completed This Sprint
- [x] GOLFIN menu reorganized (Build/, Wire/, Setup/, Debug/, Screenshot/, Utilities/)
- [x] Asset folders renamed PascalCase (no spaces)
- [x] Asset naming convention doc created
- [x] Docs restructured: AI_CONTEXT (tiny), Rules.md, Tasks.md
- [x] Game Design changelog + gameplay formulas proposal
- [x] Leveling economy overhaul (rarity-based)
- [x] TextGradients utility
- [x] Screenshot auto-compress

---

## Backlog (from Notion)

| ID | Task | Status |
|---|---|---|
| G-014 | Settings (visual fixes) | Minor fixes left |
| G-016 | Bags Inventory | Not started |
| G-017 | Shop | Not started |
| G-018 | Matchmaking (simulated) | Not started |
| G-020 | Result Screen (simulated) | Not started |
| G-021 | Rankings | Not started |
| G-022 | Log-in (simulated) | Not started |
| G-023 | Create user (simulated) | Not started |

---

## Deferred
- Character compare stat differences (backport from clubs)
- Character bio Japanese translations
- Full Japanese localization review by Ken
- Figma plan upgrade
