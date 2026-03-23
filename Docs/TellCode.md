# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-23) — Club Inventory Visual Polish

The Club Inventory screen has all the code in place but looks very rough compared to the Figma design. The architect compared screenshots against the Figma reference and found these issues. Fix them in priority order.

### Reference: Figma Design Specs (from architect's earlier extraction)
```
TYPOGRAPHY (all Rubik font):
- Screen title: SemiBold 51px (Unity ~36), letter-spacing -1.29
- Club name: SemiBold 45px (Unity ~32), letter-spacing -0.69
- Rarity label: SemiBold 45px (Unity ~32)  
- Level: SemiBold 45px / Regular 33px for "/119"
- Stat names: Medium 33px (Unity ~24), letter-spacing 0.18
- Stat values: Bold 33px (Unity ~24)
- Button text: SemiBold 39px (Unity ~28)
- EQUIP button: SemiBold 66px (Unity ~48)
- Filter bar: SemiBold 20px (Unity ~14), letter-spacing -1.5
- Tab labels: Medium 30px (Unity ~22)
- INFO header: SemiBold 48px (Unity ~34)
- INFO body: Regular 33px (Unity ~24), line-height 39px

COLORS:
- Panel background gradient: #133453 → #091B33 (top to bottom)
- Panel border: rgba(255,255,255,0.9), 3px, rounded 20px
- Stat bar fill gradient: #5792E6 → #2775DD → #1A55A4
- Stat bar background: #182430
- Stat bar height: 20px, fully rounded (radius 20px)
- Active filter text: #EBD170 (gold)
- Inactive filter text: white
- Active tab text: gold gradient (FCF195 → D6AB42 → BB7F1D)
- Inactive tab text: silver gradient (FFFFFF → D1D5DB → 818EA1)
- EQUIP button: gold gradient (FCF195 → D6AB42 → BB7F1D), border #FFE48B
- Regular buttons: silver gradient (FFFFFF → D1D5DB → 818EA1)
- Rarity text colors: Common #7E848A, Uncommon #ABC9F5, Rare #C0EAC9, 
  Mythic #FFF5D3, Legendary #ECB5A3, Supreme #C6B8DE
- Rarity stat color (for labels like "RARE"): #50C878 (green for Rare)
- Text blue: #2775DD
- Dark blue: #001E39
```

### Priority 1: Fix Carousel (CRITICAL — no cards showing)

The carousel section is empty. Verify:
1. `ClubCarouselController` is attached to the correct GameObject
2. It has `clubCardPrefab` assigned (ClubThumbnailCard.prefab must exist)
3. It has `filterBar` reference assigned
4. `ClubDatabaseCSV` and `ClubManager` are in the scene and running (check Script Execution Order: both need -200 and -100)
5. `PopulateCarousel()` is being called — add Debug.Log if missing
6. Cards per page = 6, pagination dots should show

If the prefab doesn't exist yet, run GOLFIN/Setup Club Thumbnail Card Prefab.

### Priority 2: Fix Detail Panel Layout

The detail panel needs to match the two-panel layout from Figma:

**Left Panel (~45% width):**
- Top: Club image (full-body photo, takes ~60% of left panel height)
- Bottom: INFO section (header "INFO" + description text)
- The image and INFO should be INSIDE the dark blue panel, not floating outside

**Right Panel (~55% width):**  
- Club name at top
- Divider line
- Rarity + Level row
- Divider line
- 6 stat rows (Power, Accuracy, Lie Resistance, Loft, Durability, Distance)
- Divider line
- LEVEL UP + REPAIR buttons (side by side)
- COMPARE button (full width)
- Divider line
- EQUIP button (large, gold, full width)

**Panel styling:**
- Background: gradient #133453 → #091B33
- Border: 3px solid rgba(255,255,255,0.9), border-radius 20px
- Add shadow: 0px 4px 4px rgba(0,0,0,0.25)

### Priority 3: Fix Stat Bars

Current bars are plain rectangles. They need:
1. Bar background: #182430, height 20px, fully rounded (border-radius = height/2)
2. Bar fill: blue gradient (#5792E6 → #2775DD → #1A55A4), also fully rounded
3. Bar image type must be Filled, Horizontal, Left origin
4. Stat value text to the right of the bar (e.g., "80", "30/100" for durability)
5. Stat name text above the bar
6. Stat icon to the left of the name+bar group

### Priority 4: Fix Buttons

All buttons need proper styling:
- **Regular buttons** (LEVEL UP, REPAIR, COMPARE): silver gradient background, rounded 20px, 2px border #9FABB7
- **EQUIP button**: gold gradient background, rounded 20px, 2px border #FFE48B, larger text (48px Unity)
- **Disabled buttons**: add a dark overlay (backdrop-blur + rgba(0,0,0,0.3))
- Button text color: #1E293B (dark slate) with text-shadow 0px 1px 0px rgba(255,255,255,0.3)

### Priority 5: Fix "INVENTORY" Title and Tab Bar

- "INVENTORY" title should be centered, white, SemiBold ~36px in Unity
- Tab bar: active tab (CLUBS) should have gold gradient text, inactive tabs silver gradient
- Both should have the dark blue gradient background with rounded corners and white border
- Filter bar text sizes should be ~14px Unity, gold for active, white for inactive

### Priority 6: Club Image Data Binding

The screenshot shows the A. Wedge Fyloe image when "DRIVER G&F" is labeled. Verify that `ClubDetailPanel.UpdatePanel()` loads the correct `portraitFull` sprite for the selected club. The Driver G&F should show `Placeholder` since it doesn't have a full image yet.

### Priority 7: Divider Lines

Between sections in the right panel (after name, after rarity/level, after stats, before buttons), add thin horizontal divider lines. These are visible in Figma as subtle white lines spanning ~60% of the panel width, centered.

---

### Reminders
- Font size conversion: Figma px ÷ ~1.4 ≈ Unity TMP size (approximate — verify with one known element)
- Platform: Windows (PowerShell, no bash/chmod/sed)
- Use `== null` not `??` for Unity objects
- All new text uses `LocalizationManager.Get("KEY")`
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — Task 1-4: ScreenshotTool, compress script, CLAUDE.md update, root cleanup
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire, localization keys
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels, CharacterLevelUpCosts.csv deleted
