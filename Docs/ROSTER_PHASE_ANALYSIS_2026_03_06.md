# Roster System - Current State Analysis & Development Plan

**Date:** 2026-03-06  
**Analyst:** Kai  
**For:** Cesar Guarinoni  
**Purpose:** Analyze current state, identify gaps, provide actionable next steps

---

## 📊 Current State Analysis

### ✅ What EXISTS (Phase 1 + Early Phase 2a)

#### **Phase 1: Data Architecture** - COMPLETE
```
Assets/Scripts/UI/Roster/
├── Data/
│   ├── CharacterLevelUpData.cs ✅
│   ├── PlayerCharacterData.cs ✅
│   ├── CharacterLevelUpDatabase.cs ✅
│   ├── RarityStatCaps.cs ✅
│   ├── StatAllocationStrategy.cs ✅
│   ├── ManualSPAllocation.cs ✅
│   └── AutomaticStatAllocation.cs ✅
├── Managers/
│   ├── CharacterManager.cs ✅
│   ├── CharacterDatabase.cs ✅
│   └── RewardPointsManager.cs ✅
├── UI/
│   ├── RosterScreenController.cs ✅ (Basic)
│   ├── CarouselController.cs ✅
│   └── CharacterThumbnailCard.cs ✅
└── Editor/
    ├── RosterSystemSetupTool.cs ✅
    ├── RosterCarouselBuilder.cs ✅
    └── RosterPhase1TestRunner.cs ✅
```

**Key accomplishments:**
- ✅ CSV-driven economy (LevelUpCosts.csv with 199 levels)
- ✅ Rarity-based stat caps (Common 25 → Supreme 50)
- ✅ SP allocation system (Strategy pattern)
- ✅ Character database architecture
- ✅ Reward Points management
- ✅ Carousel controller with placeholder support
- ✅ ScreenManager integration (Characters button works!)

---

### ❌ What's MISSING (Phase 2a Issues + Phase 2b/2c)

#### **Phase 2a Gaps:**
1. **CharacterThumbnailCard prefab** - No actual prefab created
   - CarouselController creates placeholder buttons instead
   - Missing: Portrait images, rarity badges, level labels

2. **Character Detail Panel** - Not implemented
   - RosterScreenController has no detail panel reference
   - Missing: Full-body portrait, stats display, buttons (Level Up, Compare, Select)

3. **Stats Display** - Missing components
   - No StatBar prefab
   - No stat icons (💪, 🏌️, ⏱️, ⚡)
   - No progress bar visuals

#### **Phase 2b: Not Started**
1. **Level-Up Modal** - Zero implementation
   - No modal UI structure
   - No SP allocation controls ([+] buttons)
   - No Reset/Confirm/Cancel flow

2. **Character Compare View** - Not implemented
   - No split-panel view
   - No side-by-side comparison logic

3. **Visual Assets** - Missing
   - Character portraits (full-body + thumbnails)
   - Rarity badge sprites (C, U, R, M, L, S)
   - Stat icons
   - Button sprites (Level Up, Compare, Select, etc.)

---

## 🎯 Updated Character Stats (Cesar's Clarification)

### NEW Stats System:
```
💪 Strength       - Max shot distance (Power)
🏌️ Club Control   - Shot gauge speed / accuracy zones
⏱️ Recovery        - Stamina regeneration / terrain mitigation
⚡ Stamina         - Energy to play holes
```

### OLD Stats (DEPRECATED - Remove from code):
```
❌ Strength (was)
❌ ClubControl (was)
❌ Stamina (was)
❌ StaminaRegen (was)
```

**Action Required:**
- ✅ Already updated in RarityStatCaps.cs (Strength, ClubControl, Recovery, Stamina)
- ✅ Already matches Confluence documentation
- ⚠️ Need to verify all UI displays use correct names

---

## 📋 Key Requirements from Confluence & References

### Character System:
1. **Database:** All base characters in local database (future: server)
2. **Ownership:** Players see all owned characters in roster
3. **Rarity:** 6 levels (Common, Uncommon, Rare, Mythic, Legendary, Supreme)
   - Displayed via background color
   - Shown in description text
4. **Level-Up:**
   - Requires Reward Points (R)
   - Cost increases every 10 levels
   - Rewards 1 SP per level
   - Max level: 199
5. **SP Allocation:**
   - Manual distribution via [+] buttons
   - Must allocate all SP before confirming
   - Can reset before confirming

### UI Flow (from Reference Images):
```
Roster Screen
├── Header (R display, title, settings)
├── Carousel (horizontal character cards)
│   ├── Left/Right arrows
│   ├── Character thumbnails (portrait, rarity, level)
│   └── Pagination dots
├── Detail Panel
│   ├── Full-body portrait
│   ├── Name + rarity + level
│   ├── Stats (4 bars with values)
│   ├── Bio text
│   ├── Action buttons (Level Up, Boost, Compare, Select)
│   └── SELECT / SELECTED state
├── Level-Up Modal (overlay)
│   ├── Character header
│   ├── Level-up info (next level, cost, reward)
│   ├── Level Up button
│   ├── SP allocation ([+] buttons per stat)
│   ├── Available SP counter
│   └── Reset / Cancel / Confirm buttons
└── Bottom Nav (home, gacha, play, tournaments, profile)
```

---

## 🚧 Development Plan: Phase-by-Phase Breakdown

### **Phase 2a (REVISED) - Complete Roster Screen Foundation** 
**Goal:** Finish carousel, create detail panel, integrate with ScreenManager  
**Time:** 6-8 hours

#### 2a.1: Create CharacterThumbnailCard Prefab (2 hours)
**Tasks:**
1. Create prefab: `Assets/Prefabs/UI/Roster/CharacterThumbnailCard.prefab`
2. Components:
   - Background Image (with rarity color tint)
   - Portrait Image (thumbnail sprite)
   - Name Label (TextMeshProUGUI)
   - Rarity Badge (Top-left corner - "C", "U", "R", etc.)
   - Level Badge (Top-right corner - "Lv X")
   - Selection Highlight (Border/glow effect)
   - Button component (onClick handler)
3. Script enhancements:
   - Update `CharacterThumbnailCard.cs` to populate all UI elements
   - Add rarity color mapping (Common=Gray, Uncommon=Green, Rare=Blue, Mythic=Yellow, Legendary=Red, Supreme=Purple)
   - Add selection state visual feedback
4. Wire to CarouselController:
   - Remove placeholder creation logic
   - Use real prefab instantiation

**Success Criteria:**
- ✅ Carousel shows real character cards with portraits
- ✅ Rarity colors display correctly
- ✅ Level badges show current level
- ✅ Selection highlighting works

---

#### 2a.2: Create StatBar Prefab (1 hour)
**Tasks:**
1. Create prefab: `Assets/Prefabs/UI/Roster/StatBar.prefab`
2. Components:
   - Stat Icon (Image) - 💪, 🏌️, ⏱️, ⚡
   - Stat Name Label (TextMeshProUGUI) - "STRENGTH"
   - Background Bar (Image) - dark gray
   - Fill Bar (Image) - blue gradient (red/orange for critical)
   - Value Text (TextMeshProUGUI) - "12/30"
3. Script:
   - Create `StatBar.cs`
   - `SetStatValue(int current, int max, StatType type)`
   - Auto-color fill based on percentage (< 33% = red warning)
   - Smooth fill animation

**Success Criteria:**
- ✅ StatBar displays correctly in isolation
- ✅ Fill bar animates smoothly
- ✅ Critical warning (red) shows when stat < 33% max

---

#### 2a.3: Create Character Detail Panel (3 hours)
**Tasks:**
1. Create hierarchy in ShellScene:
```
RosterScreen
├── ... (carousel above)
└── CharacterDetailPanel
    ├── PortraitContainer
    │   ├── PortraitImage (full-body)
    │   └── PortraitBorder
    ├── HeaderSection
    │   ├── NameText ("ELIZABETH BLACKWOOD")
    │   ├── StatusIcons (selected, boost)
    │   ├── RarityLabel ("RARE" - color-coded)
    │   └── LevelText ("Lv 80/199")
    ├── StatsSection
    │   ├── SectionTitle ("── STATISTICS ──")
    │   ├── StatBar_Strength
    │   ├── StatBar_ClubControl
    │   ├── StatBar_Recovery
    │   └── StatBar_Stamina
    ├── ActionButtonsSection
    │   ├── LevelUpButton
    │   └── BoostButton (Phase 2c - can be placeholder)
    ├── BioSection
    │   ├── BioLabel ("BIO")
    │   └── BioText (scrollable area)
    ├── CompareButton
    └── SelectButton (SELECT / SELECTED state)
```

2. Script:
   - Create `CharacterDetailPanel.cs`
   - `ShowCharacter(string characterId)` - populates all fields
   - `UpdateStats()` - refreshes stat bars
   - `UpdateSelectButton(bool isSelected)` - toggle SELECT/SELECTED
   - Wire button onClick events to RosterScreenController

3. Integration:
   - Wire CharacterDetailPanel reference to RosterScreenController
   - Connect CarouselController.OnCharacterSelected → DetailPanel.ShowCharacter
   - Test: Clicking carousel cards updates detail panel

**Success Criteria:**
- ✅ Detail panel shows when character selected
- ✅ All stats display correctly with bars
- ✅ Level Up button visible (functionality in Phase 2b)
- ✅ Bio text displays (localized)
- ✅ Select button shows correct state (SELECT vs SELECTED)

---

#### 2a.4: Integration & Testing (2 hours)
**Tasks:**
1. Wire complete flow:
   - Start Roster screen → Default character selected → Detail panel shows
   - Click carousel card → Detail panel updates
   - Click Select button → Character marked as selected
2. Test ScreenManager integration:
   - Home screen → Characters button → Roster screen appears
   - Roster screen → Home button → Returns to home
3. Update RosterCarouselBuilder tool:
   - Auto-wire CharacterDetailPanel reference
   - Add sample character prefab assignment
4. Create test character data:
   - At least 4 characters (Elizabeth, Shae, James, Olivia)
   - Placeholder portraits (colored rectangles with initials)
   - Varying rarities and levels

**Success Criteria:**
- ✅ Full navigation flow works
- ✅ Builder tool creates complete roster screen
- ✅ At least 4 test characters display correctly
- ✅ No console errors

---

### **Phase 2b - Level-Up Modal & SP Allocation**
**Goal:** Implement full level-up flow with SP allocation UI  
**Time:** 6-8 hours

#### 2b.1: Create Level-Up Modal Structure (2 hours)
**Tasks:**
1. Create hierarchy:
```
Canvas (Roster)
└── LevelUpModal (initially inactive)
    ├── Backdrop (dark semi-transparent overlay)
    └── ModalPanel (centered, white rounded rect)
        ├── CharacterHeader
        │   ├── PortraitSmall
        │   ├── NameText
        │   ├── StatusIcons
        │   ├── RarityLabel
        │   └── CurrentLevelText ("Lv 160/199")
        ├── LevelUpInfoSection
        │   ├── SectionTitle ("── LEVEL UP ──")
        │   ├── NextLevelText ("→ Lv 161" - Orange)
        │   ├── CostRow
        │   │   ├── CostLabel ("COST")
        │   │   └── CostValue ("R 805")
        │   ├── RewardRow
        │   │   ├── RewardLabel ("REWARD")
        │   │   └── RewardValue ("1 SP" - Orange)
        │   └── LevelUpButton (Gold gradient)
        ├── SPAllocationSection
        │   ├── AvailableSPText ("AVAILABLE SP: 0 SP")
        │   ├── StatSlider_Strength (bar + value + [+] button)
        │   ├── StatSlider_ClubControl
        │   ├── StatSlider_Recovery
        │   └── StatSlider_Stamina
        └── ActionButtons
            ├── ResetButton (Silver)
            ├── CancelButton (Silver outline)
            └── ConfirmButton (Gold, disabled if SP > 0)
```

2. Create prefab: `Assets/Prefabs/UI/Roster/LevelUpModal.prefab`

**Success Criteria:**
- ✅ Modal displays centered on screen
- ✅ Backdrop darkens background
- ✅ All UI elements visible and correctly positioned

---

#### 2b.2: Implement SP Allocation Logic (3 hours)
**Tasks:**
1. Create script: `LevelUpModal.cs`
   - `Open(string characterId)` - shows modal, loads character data
   - `OnLevelUpClicked()` - spends R, adds 1 level, adds 1 SP
   - `OnStatPlusClicked(StatType stat)` - allocates 1 SP to stat (temporary)
   - `OnResetClicked()` - clears all pending SP allocations
   - `OnConfirmClicked()` - applies SP allocation (only if all SP spent)
   - `OnCancelClicked()` - discards changes, closes modal
   - `UpdateUI()` - refreshes all text/buttons based on current state
2. Create `SPStatSlider.cs` component:
   - Same as StatBar but adds [+] button
   - Button disabled when:
     - No available SP
     - Stat already at max
   - Visual feedback on click (pulse animation)
3. Wire to CharacterManager:
   - Add temporary SP allocation cache
   - Apply on Confirm, discard on Cancel

**Success Criteria:**
- ✅ Level Up button spends R, increases level, grants SP
- ✅ [+] buttons allocate SP to stats
- ✅ Available SP counter decreases
- ✅ Confirm button only active when all SP allocated
- ✅ Reset button clears all pending allocations
- ✅ Cancel button closes without saving

---

#### 2b.3: Visual Polish & Animations (1 hour)
**Tasks:**
1. Add level-up success animation:
   - Particle effect (sparkles/confetti)
   - Sound effect (chime/fanfare)
   - Brief screen shake
2. Add SP allocation feedback:
   - [+] button pulse on click
   - Stat bar fill animation (smooth)
   - Available SP text color change (orange when > 0)
3. Add button states:
   - Hover effects (scale 1.05)
   - Disabled state (gray out, lower opacity)
   - Press effects (scale 0.95)

**Success Criteria:**
- ✅ Level-up feels rewarding (VFX + SFX)
- ✅ SP allocation is responsive (instant feedback)
- ✅ Buttons have clear visual states

---

#### 2b.4: Integration & Testing (2 hours)
**Tasks:**
1. Wire LevelUpModal to RosterScreenController:
   - Level Up button (detail panel) → Open modal
2. Test complete flow:
   - Open modal → displays character info correctly
   - Click Level Up → level increases, SP granted
   - Allocate SP → stat bars update (temporarily)
   - Click Confirm → changes saved, modal closes, detail panel refreshes
   - Click Cancel → changes discarded, modal closes
3. Edge case testing:
   - Insufficient R → Level Up button disabled
   - Max level reached → Level Up button hidden
   - Try to allocate SP beyond max → [+] button disabled

**Success Criteria:**
- ✅ Full level-up flow works end-to-end
- ✅ All edge cases handled gracefully
- ✅ No data corruption (cancel works correctly)

---

### **Phase 2c - Character Compare & Advanced Features**
**Goal:** Side-by-side comparison, swap functionality, polish  
**Time:** 4-6 hours

#### 2c.1: Character Compare View (3 hours)
**Tasks:**
1. Modify CharacterDetailPanel:
   - Add Compare Mode layout (split 50/50)
   - Left panel: Current character (condensed view)
   - Right panel: Compare character (condensed view)
   - Highlight superior stats (green up arrow, red down arrow)
2. Script enhancements:
   - `ShowCompare(string char1Id, string char2Id)`
   - Stat comparison logic (highlight differences)
   - "CLOSE COMPARE" button
3. Integration:
   - Compare button → Enter compare mode
   - Click another carousel card → Set as compare target
   - Close Compare → Return to single view

**Success Criteria:**
- ✅ Compare view shows two characters side-by-side
- ✅ Stat differences highlighted
- ✅ Can return to single view

---

#### 2c.2: Swap Functionality (1 hour)
**Tasks:**
1. Add swap logic to CharacterManager:
   - Track active roster slot (1 character selected at a time)
   - `SwapCharacters(string char1Id, string char2Id)` - exchange roster positions
2. UI:
   - SWAP button in compare view (right panel)
   - Swaps active selection to compared character
   - SELECTED badge moves

**Success Criteria:**
- ✅ Swap button exchanges active character
- ✅ SELECTED badge updates correctly

---

#### 2c.3: Final Polish (2 hours)
**Tasks:**
1. Localization:
   - All text strings use LocalizationManager
   - Japanese translations added (key file ready)
2. Audio:
   - Card selection sound
   - Level-up sound
   - SP allocation click
   - Insufficient points warning
   - Confirm/cancel sounds
3. Visual polish:
   - Rarity colors accurate (match references)
   - Smooth transitions between views
   - Loading states (if pulling character portraits from server)

**Success Criteria:**
- ✅ All text localized (EN + JP)
- ✅ Audio feedback on all interactions
- ✅ Polished, professional feel

---

## 🎨 Required Visual Assets

### Character Portraits:
- [ ] Elizabeth Blackwood (Rare) - Thumbnail + Full-body
- [ ] Shae O'Connell (Legendary) - Thumbnail + Full-body
- [ ] James (Common) - Thumbnail + Full-body
- [ ] Olivia (Uncommon) - Thumbnail + Full-body

### UI Sprites:
- [ ] Rarity badges (C, U, R, M, L, S)
- [ ] Stat icons (💪 Strength, 🏌️ Club Control, ⏱️ Recovery, ⚡ Stamina)
- [ ] Button sprites (Level Up, Boost, Compare, Select, etc.)
- [ ] Progress bar fill (blue gradient + red warning variant)

### Effects:
- [ ] Level-up particle effect (sparkles/confetti)
- [ ] Selection highlight (glow/border)
- [ ] SP allocation pulse (on [+] button click)

---

## 📝 Key Changes from Original Plan

### ✅ Stats Confirmed (No Ambiguity)
- Old plan had uncertainty about stat names
- Now confirmed: Strength, Club Control, Recovery, Stamina
- Matches Confluence documentation

### ✅ Phase 2a Scoped Correctly
- Original plan bundled too much into Phase 2a
- New plan: Phase 2a = Carousel + Detail Panel (no modal)
- Phase 2b = Level-Up Modal + SP Allocation
- Phase 2c = Compare + Swap

### ✅ ScreenManager Integration Done
- Already completed in earlier work
- Characters button → Roster screen works
- No need to redo this

### ✅ Realistic Time Estimates
- Phase 2a: 6-8 hours (not 2-3 days)
- Phase 2b: 6-8 hours
- Phase 2c: 4-6 hours
- Total: 16-22 hours (2-3 days at 8h/day)

---

## 🚀 Tomorrow's Action Plan

### Morning Session (4 hours):
1. **Create CharacterThumbnailCard prefab** (2 hours)
   - Design in Unity
   - Wire to CharacterThumbnailCard.cs
   - Test in carousel
2. **Create StatBar prefab** (1 hour)
   - Design + script
   - Test standalone
3. **Start Character Detail Panel** (1 hour)
   - Create hierarchy in ShellScene
   - Basic layout

### Afternoon Session (4 hours):
1. **Complete Character Detail Panel** (2 hours)
   - Wire all components
   - Create CharacterDetailPanel.cs script
   - Integration with RosterScreenController
2. **Testing & Bug Fixes** (2 hours)
   - End-to-end flow: Carousel → Select → Detail updates
   - ScreenManager navigation
   - Edge cases

**Goal:** Complete Phase 2a by end of day tomorrow!

---

## ❓ Questions for Cesar

Before starting tomorrow, please clarify:

1. **Character portraits:** Do we have placeholder art ready, or should I use colored rectangles with initials?
2. **Rarity colors:** Confirm colors for each rarity:
   - Common = Gray?
   - Uncommon = Green?
   - Rare = Blue?
   - Mythic = Yellow?
   - Legendary = Red?
   - Supreme = Purple?
3. **Boost button:** What does it do? (Phase 2c feature - can be placeholder for now)
4. **Compare trigger:** When user clicks Compare button, how do they select the second character? (Click another carousel card? Dropdown menu?)
5. **CSV data:** Do we have actual character base stats, or should I create test data?

---

**Status:** Ready to start Phase 2a tomorrow! 🚀  
**Priority:** CharacterThumbnailCard prefab + Detail Panel  
**Blocker:** None (can use placeholders for missing assets)
