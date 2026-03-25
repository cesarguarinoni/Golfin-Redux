# Tasks — Golfin Redux Current Checklist

Updated by both Claude (Architect) and Claude Code.

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
#### E1 — Club Level Up Modal ✅ CODE COMPLETE — pending Unity hierarchy setup
- [x] Extend PlayerClubData with SP fields (totalSPEarned, spentPower/Accuracy/LieRes/Durability, MAX_SP_PER_STAT=20)
- [x] ClubManager.SetLevel(), RefreshStatValues(), seed totalSPEarned in InitializeClubs()
- [x] ClubLevelUpModalController.cs (mirrors character LevelUpModalController)
- [x] Wire into ClubDetailPanel + ClubCompareController
- [x] ClubLevelUpModalAutoWire.cs (GOLFIN/Wire/Club Level Up Modal)
- [x] Localization keys
- [ ] Unity: clone LevelUpModal hierarchy → rename to ClubLevelUpModal → attach ClubLevelUpModalController → run GOLFIN/Wire/Club Level Up Modal

#### E2 — Repair Modal (spec pending)
- [ ] Repair costs Repair Kits (from Items), not RP
- [ ] Add OnClubRepaired event

#### E3 — Bag Selection Modal (spec pending)
- [ ] MAX_CLUBS_PER_BAG = 8

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
