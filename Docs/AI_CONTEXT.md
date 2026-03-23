# AI Context — Golfin Redux

**Last Updated:** 2026-03-23 by Claude Code

## Current Phase: Club Inventory Phase C — Visual Polish (almost done)

### Status

#### Phase 2 — Character Roster ✅ COMPLETE

#### Club Inventory Phase A-B ✅ COMPLETE

#### New Leveling Economy ✅ COMPLETE
- Rarity-based starting/max levels for characters and clubs
- LevelUpCosts.csv: 240 levels, cost = level × 5, 1 SP/level

#### Club Inventory Phase C — Visual Polish In Progress
- [x] ClubCarouselController, ClubDetailPanel, builders, auto-wire
- [x] Carousel populates with 6 clubs, rarity backgrounds working
- [x] Detail panel shows correct club data (stats, image, info, buttons)
- [x] EQUIP button functional (Bag 1)
- [x] TextGradients utility (Gold/Silver) created
- [x] Portrait names on 2 lines (type + brand)
- [x] Screenshot tool auto-compress to JPG
- [x] User added stat icons manually
- [x] User fixed detail panel layout (height, left/right panel positioning, paddings)
- [x] User added background image + dark bottom gradient on portraits
- [x] Correct club images loading
- [x] Filter bar dividers (ignoreLayout + manual RectTransform anchors)
- [x] Carousel arrows (FixArrowSprites patcher assigns ArrowLeft/Right from Art/Roster Screen)
- [x] ClubsMainSection/Viewport sizing (FixCarouselViewport patcher)
- [x] Fade overlay active at runtime (ScreenManager.Awake activates FadeController if inactive)
- [x] Portrait level text: "Lv 10" only (both Clubs and Characters)

#### Club Inventory Phase D — Planned
- [ ] Compare mode with stat differences (green +N / red -N)

#### Club Inventory Phase E — Planned
- [ ] Bag selection modal, Repair modal

---

### AI Workflow
- **Claude (claude.ai)** = Architect — filesystem + Figma access
- **Claude Code** = Implementer
- **TellCode.md** — architect → code instructions
- **Figma** — company file key: `hXFadl4O6HGKWakiEKgZbW` (Starter plan — rate-limited, use sparingly)
- **Images consume significant session budget** — minimize image reads per session

### Font Size Ratio
**Figma ÷ 1.4 = Unity TMP size**
```
Figma 66px → Unity 47px    Figma 45px → Unity 32px
Figma 51px → Unity 36px    Figma 39px → Unity 28px
Figma 48px → Unity 34px    Figma 33px → Unity 24px
Figma 30px → Unity 21px    Figma 20px → Unity 14px
```

### Key Design Notes (learned this session)
- Don't change font sizes, paddings, or layouts without user's explicit request — user fine-tunes these manually
- For future screens: title goes in PersistentUI top bar (username area), not as a separate header
- Rim/outline images (not Outline component) are correct for gradient borders
- Tab/filter active state = gold gradient text, inactive = silver gradient (no underline indicators)
- Reuse existing sprites (arrows, dots) from Roster — don't create new ones
- Background images may need to be separate from content containers when resizing would move children

### Script Execution Order
```
CharacterDatabaseCSV:  -200
ClubDatabaseCSV:       -200
CharacterManager:      -100
ClubManager:           -100
RuntimeActiveStateManager: -300 (if created)
```

### Deferred Items
- Character compare stat differences (green +N / red -N)
- Character bio Japanese translations
- Full Japanese localization review by Ken
- Club Level Up modal, Repair modal, Bag Selection modal
- Figma plan upgrade for more MCP calls

### Blockers
- None
