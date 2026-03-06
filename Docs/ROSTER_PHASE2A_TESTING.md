# Roster System Phase 2a - Carousel Testing

**Date:** 2026-03-06  
**What to Test:** Character carousel, navigation, card selection  
**Est. Time:** 20 minutes

---

## 🧪 Full Testing Workflow

### Part 1: Build Carousel (2 minutes)

**1. Open Unity → ShellScene**

**2. Run Builder Tool**
- Go to **Tools → GOLFIN → Build Roster Carousel (Phase 2a)**
- Click

**3. Check Results**

**Expected:** Dialog says ✅ "Roster Carousel (Phase 2a) Built!"

**Verify in Hierarchy:**
```
ShellCanvas (existing)
├── RosterScreen (NEW)
│   ├── Header (R display, title, settings)
│   ├── Carousel (ScrollRect)
│   │   └── Viewport
│   │       └── Content (empty - will populate at runtime)
│   ├── LeftArrow (button)
│   ├── RightArrow (button)
│   └── PaginationDots (empty container)
```

**Check Components:**
- ✅ RosterScreen has `RosterScreenController`
- ✅ Carousel has `CarouselController` + `ScrollRect`
- ✅ All buttons wired (LeftArrow, RightArrow)

---

### Part 2: Add to ShellScene (3 minutes)

**1. RosterScreen needs references assigned**

Select `RosterScreen` GameObject in Hierarchy

**2. In Inspector, find `RosterScreenController`:**
- [ ] Assign a TextMeshPro object for "Reward Points Text"
  - Can reuse existing R display from TopBar, or create new one

**3. Select `Carousel` (child of RosterScreen)**

**4. In Inspector, find `CarouselController`:**
- [ ] Content Parent = `Carousel/Viewport/Content`
- [ ] Left Arrow Button = `LeftArrow` button
- [ ] Right Arrow Button = `RightArrow` button
- [ ] Pagination Dots Parent = `PaginationDots`
- [ ] Character Card Prefab = (Will create in Phase 2b, leave empty for now)
- [ ] Cards Per Page = 6
- [ ] Scroll Smoothness = 0.3

**If builder tool worked, these should already be wired.** Just verify in Inspector.

---

### Part 3: Play & Verify (5 minutes)

**1. Click Play**

**2. Check Console for:**
```
[CharacterLevelUpDatabase] Loaded 80 level-up records from CSV
[CharacterManager] Initialized with 4 sample characters
[RewardPointsManager] Loaded 50000 points
[RosterScreenController] Initializing Roster Screen
[RosterScreenController] Selected first character: char_elizabeth
[CarouselController] Populating carousel
[CarouselController] Created card for char_elizabeth
[CarouselController] Created card for char_shae
[CarouselController] Created card for char_james
[CarouselController] Created card for char_olivia
[CarouselController] Populated with 4 cards
```

**3. Visual Check (Hierarchy during Play):**
```
Content should now have 4 child objects (cards):
Content
├── [Card 1] (char_elizabeth)
├── [Card 2] (char_shae)
├── [Card 3] (char_james)
└── [Card 4] (char_olivia)
```

**4. Game View:**
- ✅ Carousel appears on screen
- ✅ 4 character cards visible (or some portion based on screen size)
- ✅ Can scroll left/right with arrows
- ✅ Arrow buttons appear disabled/enabled appropriately
- ✅ Cards have white border/highlight (selection)

---

### Part 4: Navigation Testing (5 minutes)

**1. Play mode running**

**2. Test Left Arrow:**
- [ ] Click left arrow when on page 0 → Should be disabled (no-op)
- [ ] Click left arrow when on page 1 → Should scroll smoothly
- [ ] Check Console: `[CarouselController] Selected: char_X`

**3. Test Right Arrow:**
- [ ] Click right arrow on first page → Should scroll right
- [ ] Click right arrow on last page → Should be disabled
- [ ] Verify smooth scroll animation (0.3s)

**4. Test Pagination:**
- [ ] Pagination dots should appear at bottom
- [ ] Active dot = white, inactive = gray
- [ ] Dots update as you scroll

**5. Test Card Selection:**
- [ ] Card 1 has white highlight border
- [ ] Click card 2 → Card 2 gets highlight, card 1 loses it
- [ ] Check Console: `[CarouselController] Selected: char_shae`
- [ ] Check Console: `[RosterScreenController] Character selected from carousel: char_shae`

---

### Part 5: Character Data Verification (5 minutes)

**1. Stop play, select a Card in Hierarchy**

**2. In Inspector, you won't see data yet (CharacterThumbnailCard prefab not created)**

**But you can verify in Console during play:**

```csharp
// In Console, test:
var card = CharacterManager.Instance.GetPlayerCharacter("char_elizabeth");
Debug.Log($"Elizabeth: Lv {card.currentLevel}, SP {card.totalSPEarned}");
// Expected: "Elizabeth: Lv 1, SP 0"
```

**3. All cards should show:**
- Character portrait (gray placeholder for now)
- Character name (Elizabeth, Shae, James, Olivia)
- Rarity badge (C, L, C, U)
- Rarity color (Gray, Red, Gray, Blue)
- Level (Lv 1/199, Lv 1/199, etc)

---

## ✅ Success Checklist

**Builder Tool:**
- [ ] Dialog appears
- [ ] All GameObjects created in Hierarchy
- [ ] No errors in Console

**Inspector Setup:**
- [ ] CarouselController has all references assigned
- [ ] RosterScreenController has R display assigned

**Play Mode:**
- [ ] All managers initialize (4 logs)
- [ ] 4 cards created in Content
- [ ] Carousel displays on screen
- [ ] Cards visible with character data

**Navigation:**
- [ ] Left/Right arrows scroll smoothly
- [ ] Arrow buttons disable at start/end
- [ ] Pagination dots appear and update
- [ ] Card selection works (highlight toggles)

**Console Output:**
- [ ] No errors (warnings OK)
- [ ] Selection logs appear when clicking cards
- [ ] Event flow: carousel → RosterScreenController confirmed

---

## 🐛 Troubleshooting

**Q: Builder tool says references not found**
- Check: Are all GameObjects in the same parent?
- Fix: Manually assign in inspector (Content, Arrows, Dots)

**Q: No cards appear in Carousel**
- Check Console: Did it say "Populated with 0 cards"?
- Solution: CharacterThumbnailCard prefab not assigned
- For now: This is expected (prefab will be created later)

**Q: Arrows don't scroll**
- Check: Is ScrollRect enabled on Carousel?
- Check: Does Content have HorizontalLayoutGroup?
- Fix: Rebuild using editor tool

**Q: Cards don't highlight on select**
- Check: Does CharacterThumbnailCard have selectionHighlight assigned?
- Expected behavior: Prefab will be created in Phase 2b

**Q: Pagination dots don't appear**
- Check: Is PaginationDots assigned to controller?
- Check: Does controller have paginationDotPrefab assigned?
- Expected: Dots created dynamically at runtime

---

## 📝 Next Steps (Phase 2b)

**Carousel is now functional!** What's missing:
- ❌ Character portraits (images)
- ❌ Selection highlight visuals
- ❌ Pagination dot prefab
- ❌ Smooth scroll smoothness tuning

**Phase 2b will add:**
- Detail panel (shows selected character stats, bio, buttons)
- Level-up modal
- Complete prefab template for CharacterThumbnailCard

**For now:** Carousel core logic is solid and modular. Can test navigation without visuals.

---

## 🎯 Known Behaviors

**Cascade Initialization:**
1. Builder creates RosterScreen GameObject
2. Play mode starts
3. CharacterManager initializes with 4 sample characters
4. RosterScreenController wakes up
5. CarouselController populates carousel from CharacterManager
6. Cards created with character data
7. First card auto-selected

**Smooth Scroll:**
- Left/Right arrows trigger smooth scroll (0.3s Lerp)
- ScrollRect interpolates horizontally
- Pagination dots update after scroll completes

**Event Flow:**
```
Card Clicked 
  → Card.OnClicked fires
  → Carousel.SelectCharacter() 
  → Carousel.OnCharacterSelected event fires
  → RosterScreenController.OnCarouselCharacterSelected()
  → CharacterManager.SelectCharacter()
  → CharacterManager.OnCharacterSelected event fires
  → RosterScreenController.OnCharacterSelected()
```

This pattern allows future components (Detail Panel, Modal) to subscribe to OnCharacterSelected.

---

**Created by:** Kai  
**For:** Cesar (Quality verification)  
**Estimated Test Time:** 20 minutes  
**Status:** Ready to test!
