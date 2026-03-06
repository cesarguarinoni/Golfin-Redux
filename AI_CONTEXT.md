# GOLFIN Redux - AI Context

**Last Updated:** 2026-03-06 14:37 JST  
**Phase:** 2a COMPLETE, 2b/2c Ready  
**Next Session:** Visual polish + Detail Panel

---

## 🎯 Current Status

### ✅ **Phase 2a: COMPLETE (2026-03-06)**
- Roster screen structure created and integrated
- CSV-driven character system (12 characters)
- Character carousel displaying 4 characters
- Navigation working (Characters button → Roster)
- CharacterThumbnailCard prefab functional
- All character data loading from CSV

### 🎨 **Current Work: Visual Polish (Cesar)**
Cesar is polishing CharacterThumbnailCard prefab visuals:
- Adjusting sizes, colors, layouts
- Using rarity background sprites from `Assets/Art/Rarities/`
- Experimenting with badges, highlights, effects

### 📋 **Next: Phase 2b (Detail Panel)**
- Create CharacterDetailPanel UI
- Wire carousel selection → detail panel
- Display full stats (4 stat bars)
- Add Level Up button (opens modal placeholder)
- Add Select button (marks character as selected)

---

## 🗂️ Project Structure

### **Key Files Created Today:**

```
Assets/
├── Data/
│   ├── Characters.csv ⭐ (12 characters, all stats)
│   └── LevelUpCosts.csv (199 levels, universal costs)
│
├── Sprites/Characters/ ⭐ (12 character portraits)
│   ├── Elizabeth.png, Shae.png, James.png, Olivia.png
│   └── Camila, Alejandro, Ean, Freda, Johan, Mike, Richard, Roshana
│
├── Art/Rarities/ ⭐ (6 rarity background sprites)
│   ├── Common.png
│   ├── Uncommon - New.png
│   ├── Rare -New.png
│   ├── Mythic - New.png
│   ├── Legendary - New.png
│   └── Supreme - New.png
│
├── Prefabs/UI/Roster/
│   ├── CharacterThumbnailCard.prefab ⭐ (auto-wired)
│   └── StatBar.prefab (created but not used yet)
│
└── Scripts/UI/Roster/
    ├── Managers/
    │   ├── CharacterManager.cs (loads from CSV first, then ScriptableObjects)
    │   ├── CharacterDatabase.cs (ScriptableObject system, legacy)
    │   ├── CharacterDatabaseCSV.cs ⭐ (CSV loader, preferred)
    │   ├── RewardPointsManager.cs
    │   └── CharacterLevelUpDatabase.cs
    │
    ├── UI/
    │   ├── RosterScreenController.cs
    │   ├── CarouselController.cs ⭐ (horizontal scroll, pagination optional)
    │   ├── CharacterThumbnailCard.cs ⭐ (card display logic)
    │   └── StatBar.cs (stat display component)
    │
    ├── Data/
    │   ├── PlayerCharacterData.cs
    │   ├── RarityStatCaps.cs (rarity-based stat caps)
    │   ├── CharacterLevelUpData.cs
    │   └── StatAllocationStrategy.cs (Manual/Automatic SP)
    │
    └── Editor/
        ├── RosterScreenBuilder.cs ⭐ (one-click hierarchy builder)
        ├── RosterPrefabBuilder.cs ⭐ (creates prefabs programmatically)
        ├── RosterMenuCleanup.cs (menu organization)
        └── RosterSystemSetupTool.cs (Phase 1 setup)
```

---

## 📊 Character Data (CSV-Driven)

### **Characters.csv Structure:**
```csv
id,name,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina,portraitSprite,maxLevel
char_elizabeth,Elizabeth,Rare,8,10,7,9,Elizabeth,199
char_shae,Shae,Legendary,12,8,15,10,Shae,199
char_james,James,Common,6,7,6,6,James,199
char_olivia,Olivia,Uncommon,7,8,6,7,Olivia,199
char_camila,Camila,Rare,9,9,8,8,Camila,199
char_alejandro,Alejandro,Mythic,10,11,9,12,Alejandro,199
char_ean,Ean,Uncommon,7,7,7,7,Ean,199
char_freda,Freda,Supreme,15,12,18,14,Freda,199
char_johan,Johan,Rare,8,10,7,10,Johan,199
char_mike,Mike,Common,6,6,7,7,Mike,199
char_richard,Richard,Mythic,11,10,10,11,Richard,199
char_roshana,Roshana,Legendary,13,9,14,11,Roshana,199
```

### **Current Player Roster:**
- First 4 characters from CSV (Elizabeth, Shae, James, Olivia)
- All start at Level 1
- Elizabeth selected by default

---

## 🎨 Rarity System

### **6 Rarity Tiers:**
```
Common    (C) - Gray   #808080 - Background: Common.png
Uncommon  (U) - Blue   #4A90E2 - Background: Uncommon - New.png
Rare      (R) - Green  #2ECC71 - Background: Rare -New.png
Mythic    (M) - Yellow #F1C40F - Background: Mythic - New.png
Legendary (L) - Red    #E74C3C - Background: Legendary - New.png
Supreme   (S) - Purple #9B59B6 - Background: Supreme - New.png
```

### **Stat Caps by Rarity:**
```
Common:    25/25/18/22
Uncommon:  28/28/19/25
Rare:      30/30/20/27
Mythic:    35/35/25/32
Legendary: 40/40/40/40
Supreme:   50/50/50/50
```

---

## 🏗️ Scene Structure

### **Hierarchy (ShellScene):**
```
Canvas
└── ScreensRoot
    ├── LogoScreen
    ├── SplashScreen
    ├── LoadingScreen
    ├── HomeScreen
    └── RosterScreen ⭐
        ├── Header
        │   ├── RewardPointsDisplay (R 50000)
        │   └── TitleText ("ROSTER")
        │
        ├── CarouselSection (has CarouselController)
        │   ├── LeftArrow (Button)
        │   ├── ScrollView (has ScrollRect)
        │   │   ├── Viewport (RectMask2D, Image MUST be transparent!)
        │   │   │   └── Content (HorizontalLayoutGroup + ContentSizeFitter)
        │   │   │       └── [CharacterThumbnailCard clones spawn here]
        │   ├── RightArrow (Button)
        │   └── PaginationDots (optional, currently disabled)
        │
        └── DetailPanel (placeholder text for now)
```

### **GameObject Requirements:**
1. **CharacterDatabaseCSV** (singleton in scene root)
   - Assign `Characters.csv` to `charactersCSV` field
   - Assign all 12 character sprites to `Character Portraits` array

2. **Viewport Image Component:**
   - **CRITICAL:** Must be transparent (alpha = 0) or disabled
   - Bug: If visible, it blocks character cards from displaying

3. **HomeScreenController:**
   - Must have `navCharactersButton` field assigned
   - Already wired to call `ShowScreen(ScreenId.Roster)`

---

## 🔧 Tools Menu Structure

```
Tools → GOLFIN → Roster
├── Build Complete Roster Screen ⭐ (one-click hierarchy builder)
├── Build Character Thumbnail Prefab ⭐ (creates card prefab)
├── Build StatBar Prefab
├── Test Phase 1 (Data)
├── Debug: List All Characters
├── Debug: Validate References
└── Data: Reset Player Progress
```

### **Key Builder Features:**
- **RosterScreenBuilder:** Creates complete hierarchy under ScreensRoot
- **RosterPrefabBuilder:** Creates prefabs with all references auto-wired
- All builders log success/failure to Console
- Force AssetDatabase save/refresh to ensure changes persist

---

## 🐛 Known Issues & Solutions

### **Issue 1: Cards Not Visible**
**Symptom:** Carousel empty, no characters show  
**Common Causes:**
1. **Viewport Image blocking** - Make Image transparent (alpha = 0) or disable
2. **Prefab not assigned** - Assign CharacterThumbnailCard.prefab to CarouselController
3. **No LayoutElement** - Cards need LayoutElement component (preferredWidth: 150, preferredHeight: 200)
4. **CharacterDatabaseCSV missing** - Create GameObject, assign CSV + sprites

### **Issue 2: Navigation Not Working**
**Symptom:** Characters button does nothing, no logs  
**Solution:** `navCharactersButton` field empty in HomeScreenController Inspector  
**Fix:** Assign button from `Canvas → PersistentUI → BottomNavBar → [Characters button]`

### **Issue 3: Characters Load as null**
**Symptom:** Cards created but no portraits/data  
**Solution:** CharacterDatabaseCSV singleton not initialized  
**Fix:** Ensure CharacterDatabaseCSV GameObject exists in scene root (not under Canvas)

### **Issue 4: Content Width Locked**
**Symptom:** Content width stuck at 660, cards positioned off-screen  
**Solution:** Missing ContentSizeFitter or HorizontalLayoutGroup misconfigured  
**Fix:** Content needs ContentSizeFitter (Horizontal Fit: Preferred Size)

---

## 📋 Phase Completion Checklist

### **✅ Phase 1: Data Architecture**
- [x] CSV economy system (LevelUpCosts.csv)
- [x] Rarity-based stat caps (RarityStatCaps.cs)
- [x] SP allocation strategy pattern
- [x] Character managers (CharacterManager, RewardPointsManager)
- [x] Automated tests + editor tools

### **✅ Phase 2a: Carousel + Navigation**
- [x] RosterScreen structure created under ScreensRoot
- [x] ScreenManager integration (Characters button works)
- [x] CSV character database (12 characters)
- [x] CharacterThumbnailCard prefab (auto-wired with LayoutElement)
- [x] Carousel displays 4 characters with portraits
- [x] Rarity colors, level badges, names showing
- [x] Navigation: Home ↔ Roster screen switching

### **⏳ Phase 2b: Detail Panel (Next)**
- [ ] CharacterDetailPanel UI structure
- [ ] StatBar prefab instances (4 stats)
- [ ] Wire carousel selection → detail panel update
- [ ] Level Up button (opens modal placeholder)
- [ ] Select button (marks character as selected)
- [ ] Bio section (localized text)

### **⏳ Phase 2c: Level-Up Modal (Future)**
- [ ] Level-Up Modal UI (overlay)
- [ ] SP allocation ([+] buttons per stat)
- [ ] Reset / Confirm / Cancel flow
- [ ] Reward Points spending
- [ ] Level-up VFX + SFX

### **⏳ Phase 2d: Character Compare (Future)**
- [ ] Split-panel comparison view
- [ ] Side-by-side stat comparison
- [ ] Swap functionality
- [ ] Highlight stat differences

---

## 🎯 Design Decisions & Patterns

### **1. CSV-First Architecture**
**Decision:** Character data in CSV, not ScriptableObjects  
**Rationale:** Easy to edit/balance, no Unity Editor needed for data entry  
**Implementation:** CharacterDatabaseCSV loads CSV at runtime, converts to runtime objects  
**Fallback:** CharacterManager tries CSV first, falls back to ScriptableObject database

### **2. Universal Level Costs**
**Decision:** One cost table for ALL characters (LevelUpCosts.csv)  
**Rationale:** Eliminates duplication, easier to balance economy  
**Implementation:** CharacterLevelUpDatabase queries by level only, not character ID

### **3. Rarity-Based Stat Caps**
**Decision:** Stat caps determined by rarity tier, hardcoded in RarityStatCaps.cs  
**Rationale:** Easy to tune balance without CSV edits, clear progression tiers  
**Implementation:** CharacterManager.GetMaxStat() queries RarityStatCaps by character rarity

### **4. Strategy Pattern for SP Allocation**
**Decision:** Swappable allocation strategy (Manual vs Automatic)  
**Rationale:** Future-proof for different game modes, testable  
**Implementation:** CharacterManager.SetAllocationStrategy() - one line to switch

### **5. Roster = Main Screen (Not Overlay)**
**Decision:** Roster managed by ScreenManager like Home/Logo, not separate overlay  
**Rationale:** Consistent navigation pattern, proper screen lifecycle  
**Implementation:** ScreenId.Roster enum, ShowScreen(ScreenId.Roster) hides other screens

### **6. Pagination Dots Optional**
**Decision:** Carousel works without pagination dots prefab  
**Rationale:** Avoids blocking functionality if prefab not ready, can add later  
**Implementation:** CarouselController checks if paginationDotPrefab != null before using

---

## 🔍 Debugging Tips

### **When Cards Don't Appear:**
1. Check Console for `[CarouselController] Populating carousel`
2. Check Console for `[CharacterManager] Initialized with X sample characters`
3. Verify Content has 4 child GameObjects (the card clones)
4. Select one card clone, check if LayoutElement exists
5. Check Viewport Image component - should be transparent or disabled
6. Scene view: Can you see cards there? If yes, it's a rendering/masking issue

### **When Navigation Broken:**
1. Check Console for `[HomeScreenController] OnNavClicked: Roster`
2. If no logs, navCharactersButton field is empty
3. If logs but no screen change, check ScreenManager._rosterScreen is assigned
4. Verify RosterScreen is under ScreensRoot, not Canvas root

### **When CSV Fails to Load:**
1. Check Console for `[CharacterDatabaseCSV] Loaded X characters from CSV`
2. If 0 characters, CSV format is wrong or file not assigned
3. Check sprite names in CSV match actual sprite names in Project
4. Verify all 12 sprites assigned to Character Portraits array

---

## 💡 Common Workflows

### **Adding a New Character:**
1. Add sprite to `Assets/Sprites/Characters/` (e.g., `NewChar.png`)
2. Edit `Characters.csv`, add new row with stats
3. Assign new sprite to CharacterDatabaseCSV in Inspector
4. Play Mode → character automatically loads

### **Changing Character Stats:**
1. Edit `Characters.csv` (change numbers)
2. Save file
3. Play Mode → changes take effect immediately (no Unity rebuild needed)

### **Adjusting Card Visuals:**
1. Open `CharacterThumbnailCard.prefab` in Project panel
2. Edit child objects (Background, Portrait, Badges, etc.)
3. Save prefab (Ctrl+S)
4. Exit Prefab Mode
5. Play Mode → all cards show new visuals

### **Testing Visual Changes (3 Methods):**
- **Method 1 (Fast Iteration):** Exit Play → Edit Prefab → Save → Exit Prefab → Play → Check
- **Method 3 (Test Instance):** Create TestCard GameObject, drag prefab as child, edit prefab, delete TestCard when done
- **Method 4 (Canvas Preview):** Select Canvas in 2D Scene view, zoom to fit, edit prefab, see at game resolution

---

## 📝 Session Summary (2026-03-06)

### **Accomplishments:**
- ✅ Created CSV-based character system (12 characters)
- ✅ Built RosterScreen hierarchy (header, carousel, detail panel stub)
- ✅ Integrated with ScreenManager (navigation working)
- ✅ Created CharacterThumbnailCard prefab (fully auto-wired)
- ✅ Characters displaying with portraits, names, rarity, levels
- ✅ Fixed 8+ critical bugs (ScrollRect, LayoutElement, Viewport Image, etc.)
- ✅ Completed Phase 2a

### **Time Spent:** ~4.5 hours (debugging + building)

### **Commits Today:** 20+ commits
- CSV system, prefab builders, bug fixes, documentation

### **Next Session Goals:**
1. Cesar polishes CharacterThumbnailCard visuals (using rarity backgrounds)
2. Create CharacterDetailPanel UI
3. Wire carousel selection → detail panel
4. Add StatBar instances (4 stats)
5. Add Level Up + Select buttons

---

## 🎯 Handoff Notes for Next Developer

### **Quick Start:**
1. Pull latest from GitHub
2. Open ShellScene in Unity
3. Check CharacterDatabaseCSV GameObject is in scene root
4. Verify 12 sprites assigned to Character Portraits array
5. Play Mode → Click Characters button → Should see 4 character cards

### **If Characters Don't Show:**
1. Select Viewport in Hierarchy
2. Disable Image component OR set alpha to 0
3. Save scene, Play Mode again

### **To Continue Development:**
- Start with Phase 2b: CharacterDetailPanel
- Reference: `Docs/ROSTER_PHASE_ANALYSIS_2026_03_06.md`
- Reference images: `Assets/References/Roster Screen/`

---

**Last Modified:** 2026-03-06 14:37 JST by Kai  
**Next Update:** After Phase 2b completion
