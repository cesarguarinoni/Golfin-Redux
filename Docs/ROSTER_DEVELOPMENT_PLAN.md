# Roster Screen Development Plan

**Date:** 2026-03-06  
**Status:** Phase 0 - Planning  
**Estimated Time:** 2-3 days full implementation

---

## Overview

The Roster Screen (formerly "Characters Screen") displays all characters owned by the player, their stats, rarity, and allows leveling up with Reward Points.

### Key Requirements:
- **New Stats:** Strength, Club Control, Recovery, Stamina
- **Local Database:** Character base data (will migrate to server later)
- **Player Inventory:** Track which characters player owns + their levels
- **Rarity System:** Color-coded display (Common, Rare, Epic, Legendary, etc.)
- **Level-Up System:** Requires Reward Points, shows level progress
- **UI:** Grid/list view of character cards with portraits

---

## Phase 1: Data Architecture (~2-3 hours)

### 1.1 Character Data Model

**Create: `CharacterData.cs` (ScriptableObject)**
```csharp
[CreateAssetMenu(fileName = "Character", menuName = "GOLFIN/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterId;          // Unique ID (e.g., "char_camila")
    public string characterName;        // Display name
    public Sprite portrait;             // Character portrait image
    public CharacterRarity rarity;      // Common, Rare, Epic, etc.
    
    [Header("Base Stats (Level 1)")]
    public int baseStrength;            // Power/distance
    public int baseClubControl;         // Accuracy
    public int baseRecovery;            // HP regen / bounce back
    public int baseStamina;             // Energy / endurance
    
    [Header("Leveling")]
    public int maxLevel = 50;
    public AnimationCurve levelUpCostCurve;  // Reward Points per level
    public AnimationCurve statGrowthCurve;   // Stat scaling per level
    
    [Header("Localization")]
    public string nameLocalizationKey;       // "CHAR_NAME_CAMILA"
    public string descriptionLocalizationKey; // "CHAR_DESC_CAMILA"
}

public enum CharacterRarity
{
    Common,      // Gray
    Uncommon,    // Green
    Rare,        // Blue
    Epic,        // Purple
    Legendary,   // Gold
    Mythic       // Red/Special
}
```

### 1.2 Character Database

**Create: `CharacterDatabase.cs` (ScriptableObject)**
- Similar pattern to HoleDatabase
- Holds all base CharacterData assets
- Auto-populate from Resources folder
- Editor button to refresh

**Path:** `Assets/Data/Characters/` (ScriptableObject instances)

### 1.3 Player Character Inventory

**Create: `PlayerCharacterData.cs` (Serializable class)**
```csharp
[System.Serializable]
public class PlayerCharacterData
{
    public string characterId;          // Reference to CharacterData
    public int currentLevel;            // 1 - maxLevel
    public int currentExperience;       // For future XP system
    public System.DateTime acquiredDate;
    
    // Calculated at runtime
    public int GetCurrentStrength(CharacterData baseData) { }
    public int GetCurrentClubControl(CharacterData baseData) { }
    public int GetCurrentRecovery(CharacterData baseData) { }
    public int GetCurrentStamina(CharacterData baseData) { }
    public int GetLevelUpCost(CharacterData baseData) { }
}
```

**Create: `PlayerCharacterInventory.cs` (Singleton Manager)**
- Saves/loads from PlayerPrefs (JSON serialization)
- Methods: HasCharacter(), GetCharacter(), LevelUpCharacter()
- Integration with Reward Points system

---

## Phase 2: UI Structure (~3-4 hours)

### 2.1 Screen Layout

**Hierarchy:**
```
RosterCanvas
├── BackButton (Top-left, returns to Home)
├── TitleText ("ROSTER" localized)
├── RewardPointsDisplay (Shows available points)
├── CharacterGrid (Scroll View)
│   └── Viewport
│       └── Content (Grid Layout Group)
│           ├── CharacterCard (Prefab x N)
│           ├── CharacterCard
│           └── ...
└── CharacterDetailPanel (Modal, shows on card click)
    ├── CloseButton
    ├── CharacterPortrait (Large)
    ├── CharacterName
    ├── RarityBadge
    ├── LevelDisplay ("Lv. 12")
    ├── StatsSection
    │   ├── StrengthBar (Icon + Value + Progress Bar)
    │   ├── ClubControlBar
    │   ├── RecoveryBar
    │   └── StaminaBar
    └── LevelUpButton (Shows cost, disabled if insufficient points)
```

### 2.2 CharacterCard Prefab

**Components:**
- Background (Image with rarity color)
- Portrait (Image)
- Name (TMP with LocalizedText)
- Level Badge ("Lv. X")
- Rarity Stars/Icon
- New Badge (if recently acquired)
- Button component (onClick → Show detail panel)

**Rarity Colors:**
- Common: #808080 (Gray)
- Uncommon: #00FF00 (Green)
- Rare: #0080FF (Blue)
- Epic: #8000FF (Purple)
- Legendary: #FFD700 (Gold)
- Mythic: #FF0040 (Red)

### 2.3 CharacterDetailPanel

**Modal overlay** (similar to About modal pattern):
- Backdrop (dark overlay)
- Centered panel
- Character info + stats display
- Level-up button with cost
- Close button

---

## Phase 3: Core Scripts (~4-5 hours)

### 3.1 Character Management

**Create: `CharacterManager.cs` (Singleton)**
- Manages character database
- Provides character lookup by ID
- Calculates stats at current level
- Handles level-up logic

**Key Methods:**
```csharp
public CharacterData GetCharacter(string id);
public PlayerCharacterData GetPlayerCharacter(string id);
public bool CanLevelUp(string id);
public bool LevelUpCharacter(string id);
public int CalculateStat(int baseStat, int level, AnimationCurve curve);
```

### 3.2 Roster Screen Controller

**Create: `RosterScreenController.cs`**
- Populates character grid from PlayerCharacterInventory
- Handles card click → show detail panel
- Updates UI when language changes
- Refreshes after level-up

**Key Methods:**
```csharp
private void PopulateCharacterGrid();
private void OnCharacterCardClicked(string characterId);
private void ShowCharacterDetail(string characterId);
private void OnLevelUpClicked();
private void RefreshDisplay();
```

### 3.3 UI Components

**Create: `CharacterCard.cs`**
- Displays character summary
- Updates visuals based on data
- Handles click event

**Create: `CharacterDetailPanel.cs`**
- Extends ModalController (from Phase 3)
- Shows detailed stats
- Handles level-up button
- Updates in real-time

**Create: `StatBar.cs` (Reusable component)**
- Icon + Label + Progress bar
- Smooth fill animation
- Localized stat name

---

## Phase 4: Editor Tools (~2-3 hours)

### 4.1 Character Database Builder

**Tool: `Tools → GOLFIN → Build Character Database`**
- Scans `Assets/Data/Characters/` for CharacterData
- Populates CharacterDatabase ScriptableObject
- Similar to HoleDatabase pattern

### 4.2 CSV Import Tool (Optional, for bulk data)

**Tool: `Tools → GOLFIN → Import Characters from CSV`**
- CSV format: id, name, rarity, str, control, rec, stam
- Creates CharacterData ScriptableObjects
- Assigns portraits from `Assets/Art/Characters/`

**Example CSV:**
```csv
id,name,rarity,strength,clubControl,recovery,stamina,portrait
char_camila,Camila,Rare,75,80,65,70,Camila.png
char_james,James,Epic,85,70,75,80,James.png
char_olivia,Olivia,Legendary,90,85,80,85,Olivia.png
```

### 4.3 Roster UI Builder

**Tool: `Tools → GOLFIN → Build Roster Screen`**
- One-click creation of complete hierarchy
- Grid layout with sample cards
- Detail panel with stat bars
- All references wired automatically

---

## Phase 5: Integration (~2-3 hours)

### 5.1 Reward Points System

**Integration with existing systems:**
- Reward Points displayed in Roster Screen
- Deduct points on level-up
- Show insufficient points message if needed
- Update display after transaction

**If Reward Points system doesn't exist yet:**
- Create `RewardPointsManager.cs` singleton
- Save/load from PlayerPrefs
- Methods: GetPoints(), AddPoints(), SpendPoints()

### 5.2 Navigation

**From Home Screen:**
- Characters button → Opens Roster Screen
- Back button → Returns to Home

**Data flow:**
- Load PlayerCharacterInventory on app start
- Save after each level-up
- Sync with server (Phase 6)

### 5.3 Localization

**Add to `LocalizationText.csv`:**
```csv
key,English,Japanese
ROSTER_TITLE,ROSTER,名簿
ROSTER_LEVEL,Level,レベル
ROSTER_REWARD_POINTS,Reward Points,報酬ポイント
ROSTER_LEVEL_UP,LEVEL UP,レベルアップ
ROSTER_COST,Cost,コスト
ROSTER_INSUFFICIENT_POINTS,Not enough Reward Points,報酬ポイントが不足しています
ROSTER_MAX_LEVEL,Max Level,最大レベル
ROSTER_STATS,Stats,ステータス
ROSTER_STRENGTH,Strength,ストレングス
ROSTER_CLUB_CONTROL,Club Control,クラブコントロール
ROSTER_RECOVERY,Recovery,リカバリー
ROSTER_STAMINA,Stamina,スタミナ
CHAR_NAME_CAMILA,Camila,カミラ
CHAR_NAME_JAMES,James,ジェームズ
CHAR_NAME_OLIVIA,Olivia,オリビア
```

**Use LocalizationEditorHelper:**
- Add LocalizedText to all text components
- Follow ROSTER_* naming convention

---

## Phase 6: Testing & Polish (~2-3 hours)

### 6.1 Testing Checklist

**Functional:**
- [ ] All characters display correctly in grid
- [ ] Card click opens detail panel
- [ ] Stats display correctly at each level
- [ ] Level-up button works (deducts points, increases stats)
- [ ] Level-up button disabled when insufficient points
- [ ] Level-up button disabled at max level
- [ ] Reward Points display updates after level-up
- [ ] Back button returns to Home Screen

**Visual:**
- [ ] Rarity colors match design
- [ ] Character portraits display correctly
- [ ] Stat bars fill smoothly
- [ ] Level badge shows correct number
- [ ] UI responsive on different screen sizes

**Localization:**
- [ ] All text has LocalizedText component
- [ ] English → Japanese switch updates all text
- [ ] Character names localized
- [ ] Stat names localized

### 6.2 Polish

**Animations:**
- Card fade-in when grid populates (stagger effect)
- Detail panel slide-in from bottom
- Level-up success animation (confetti/sparkle)
- Stat bars animate on level-up

**Audio:**
- Card click sound
- Level-up success sound
- Insufficient points warning sound

**Feedback:**
- Hover effect on cards (scale up slightly)
- Level-up button glow when affordable
- Toast message on successful level-up

---

## Phase 7: Future Enhancements (Post-MVP)

### 7.1 Server Integration
- Migrate from PlayerPrefs to server database
- Sync character inventory across devices
- Prevent cheating (client-side validation only for now)

### 7.2 Advanced Features
- Character customization (skins, accessories)
- Character comparison view (side-by-side stats)
- Sorting/filtering (by rarity, level, stat type)
- Search by name
- Favorite characters (pin to top)
- Character unlock animations
- Achievement system (collect all characters)

### 7.3 Gacha Integration
- Link to Gacha Screen for acquiring new characters
- Show "New Character Acquired" modal
- Add to PlayerCharacterInventory automatically

---

## Implementation Timeline

### Day 1 (Tomorrow):
- **Morning (3 hours):** Phase 1 (Data Architecture) + Phase 2 (UI Structure)
  - Create CharacterData, CharacterDatabase, PlayerCharacterInventory
  - Design UI hierarchy (on paper or in Unity)
  - Create CharacterCard prefab

- **Afternoon (4 hours):** Phase 3 (Core Scripts) + Phase 4 (Editor Tools)
  - CharacterManager, RosterScreenController
  - CharacterCard, CharacterDetailPanel, StatBar
  - Build Character Database tool
  - Build Roster Screen tool

### Day 2:
- **Morning (3 hours):** Phase 5 (Integration)
  - Reward Points system
  - Navigation
  - Localization (all keys)

- **Afternoon (3 hours):** Phase 6 (Testing & Polish)
  - Functional testing
  - Visual polish
  - Animations
  - Audio integration

### Day 3 (If needed):
- Bug fixes
- Additional polish
- Documentation updates

---

## Required Assets (To be provided)

### Character Art:
- [x] Camila.png (exists)
- [x] James.png (exists)
- [x] Olivia.png (exists)
- [ ] Additional character portraits
- [ ] Rarity badge icons (star, crown, etc.)
- [ ] Level badge background
- [ ] Stat icons (strength, control, recovery, stamina)

### UI Assets:
- [ ] Card background template (with rarity color overlay)
- [ ] Detail panel background
- [ ] Progress bar sprites (background + fill)
- [ ] Level-up button sprite
- [ ] Insufficient points icon

### Audio:
- [ ] Card click sound
- [ ] Level-up success sound
- [ ] Insufficient points warning sound

### Reference Images:
- [ ] Roster Screen mockup (to be added to `Assets/References/Roster Screen/`)
- [ ] Character card design
- [ ] Detail panel design
- [ ] Stat bar design

---

## Dependencies

### Existing Systems:
- ✅ **Localization System** (Phase 3)
- ✅ **ModalController** (Phase 3, for detail panel)
- ✅ **LocalizationEditorHelper** (Phase 3, for auto-localization)
- ⚠️ **Reward Points System** (check if exists, create if not)

### New Systems:
- Character Database (Phase 1)
- Player Character Inventory (Phase 1)
- Character Manager (Phase 3)

---

## Notes & Considerations

### Database Design:
- Use ScriptableObjects for base character data (like HoleDatabase)
- Use PlayerPrefs (JSON) for player inventory (migrate to server later)
- Separate base data from player-specific data

### Performance:
- Grid view with object pooling if > 50 characters
- Load portraits asynchronously if needed
- Cache calculated stats (recalculate only on level-up)

### Future Server Migration:
- Design data structure to easily convert to server API
- Use string IDs (not references) for serialization
- Player inventory saved as JSON (easy to send to server)

### Localization:
- Character names can be localized (use nameLocalizationKey)
- Stat names localized (ROSTER_STRENGTH, etc.)
- Use LocalizedText on all UI text from the start

### Rarity System:
- Colors defined in code (easy to tweak)
- Rarity affects: background color, border, badge, maybe glow effect
- Can extend to affect stat growth curves (Epic grows faster than Common)

### Level-Up Cost:
- Use AnimationCurve for flexible cost scaling
- Example: Linear (100, 200, 300...) or Exponential (100, 150, 225...)
- Can be different per rarity (Legendary costs more)

---

## Success Criteria

**MVP Complete When:**
- [ ] Roster screen displays all player's characters
- [ ] Character cards show portrait, name, level, rarity
- [ ] Click card → Detail panel opens with stats
- [ ] Stats display correctly based on current level
- [ ] Level-up button works (deducts Reward Points, increases stats)
- [ ] UI updates immediately after level-up
- [ ] All text is localized (English/Japanese)
- [ ] Navigation works (Home ↔ Roster)
- [ ] No critical bugs or crashes

**Ready for Production When:**
- [ ] All MVP criteria met
- [ ] Tested on multiple devices/resolutions
- [ ] Audio implemented
- [ ] Animations polished
- [ ] Performance optimized
- [ ] Server integration complete (or planned)
- [ ] Documentation complete (for future developers)

---

## Questions to Resolve Before Starting

1. **How many characters will exist in total?** (For grid sizing)
2. **Do all players start with a default character?** (Camila, James, or Olivia?)
3. **How are new characters acquired?** (Gacha only? Rewards? Events?)
4. **What is the Reward Points economy?** (How many points for typical actions?)
5. **Should level-up costs scale with rarity?** (Epic = more expensive than Common?)
6. **Is there a stat cap per level?** (E.g., max Strength = 100 at level 50?)
7. **Should characters have visual evolution?** (Different portrait at level 25, 50?)
8. **Character descriptions/lore?** (Displayed in detail panel?)
9. **Character roles/classes?** (Power hitter, Accuracy specialist, etc.?)
10. **Reference images ready?** (Need mockups to match design exactly)

---

**Created by:** Kai (Aikenken Bot)  
**For:** Cesar Guarinoni  
**Date:** 2026-03-05 → Start 2026-03-06  
**Status:** Ready to begin Phase 1!

