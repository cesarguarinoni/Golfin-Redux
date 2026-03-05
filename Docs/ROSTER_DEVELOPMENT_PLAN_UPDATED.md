# Roster Screen Development Plan (UPDATED)

**Date:** 2026-03-06  
**Status:** Phase 0 - Planning (Updated after Reference Review)  
**Estimated Time:** 2-3 days full implementation

---

## Overview

Based on reference images (`Assets/References/Roster Screen/`) and Confluence documentation, the Roster Screen displays player-owned characters with detailed stats, level-up system with **SP (Stat Points) allocation**, rarity indicators, and roster management (Compare, Swap, Select).

### Key Requirements (from References):
- **New Stats:** Strength, Club Control, Recovery, Stamina
- **SP System:** Leveling rewards Stat Points that players manually allocate
- **Level-Up Cost:** Uses Reward Points (R), cost increases every 10 levels
- **Rarity System:** C (Common), U (Uncommon), R (Rare), M (Master), L (Legendary), S (Special)
- **Carousel UI:** Horizontal character thumbnails with pagination
- **Character Detail:** Full-body portrait with stats, bio, action buttons
- **Character Compare:** Side-by-side stat comparison view
- **Character Swap:** Roster slot management (SELECT/SWAP buttons)
- **SP Allocation Modal:** Level up → Spend SP → Reset/Confirm flow

---

## Design Analysis (from Reference Images)

### UI Architecture

```
┌─────────────────────────────────────────┐
│  R 50000   ROSTER              ⚙  ≡     │  ← Header
├─────────────────────────────────────────┤
│  ◄ [👤][👤][👤][👤][👤][👤] ►          │  ← Carousel
│     C   U   R   M   L   S                │    (Rarity badges)
│    Lv9 Lv5 Lv10 Lv25 Lv9 Lv20           │    (Level badges)
│  ●  ○  ○  ○                              │  ← Pagination dots
├─────────────────────────────────────────┤
│  ┌──────────┐  ELIZABETH BLACKWOOD       │  ← Character Detail
│  │  Full    │  [Selected Icon] [Boost]   │
│  │  Body    │  RARE         Lv 80/199    │
│  │ Portrait │                             │
│  │          │  ──── STATISTICS ────       │
│  └──────────┘  💪 STRENGTH    12/30      │
│                 ████░░░░░░░░░░░░          │
│                 🏌️ CLUB CONTROL 30/30     │
│                 ██████████████░░          │
│                 ⏱️ RECOVERY     15/20     │
│                 ██████████░░░░░░          │
│                 ⚡ STAMINA      22/27     │
│                 ████████████░░░░          │
│                                            │
│                 ┌──────┐ ┌──────┐         │
│                 │LEVEL │ │BOOST │         │
│                 │  UP  │ │      │         │
│                 └──────┘ └──────┘         │
│                                            │
│                 "BIO"                      │
│                 Elizabeth is a skilled... │
│                                            │
│                 ┌───────────────┐         │
│                 │    COMPARE    │         │
│                 └───────────────┘         │
│                                            │
│                 ┌───────────────┐         │
│                 │   SELECTED    │  ← Gold │
│                 └───────────────┘         │
├─────────────────────────────────────────┤
│  🏠  🃏 ┌─────┐ 🏆  👤                  │  ← Bottom Nav
│         │  ⛳  │                          │    (Center elevated)
│         └─────┘                          │
└─────────────────────────────────────────┘
```

### Level-Up Modal (SP Allocation)

```
┌────────────────────────────────────┐
│  [BACKDROP - Darkened]              │
│                                     │
│  ┌──────────────────────────────┐  │
│  │  SHAE O'CONNELL              │  │
│  │  [◎] [⬆]    LEGENDARY        │  │
│  │             Lv 160/199       │  │
│  │                               │  │
│  │  ──── LEVEL UP ────          │  │
│  │                               │  │
│  │  NEXT LEVEL    → Lv 161      │  │  ← Orange
│  │  COST           R 805        │  │
│  │  REWARD         1 SP         │  │  ← Orange
│  │                               │  │
│  │  ┌───────────────┐            │  │
│  │  │   LEVEL UP   │            │  │  ← Gold button
│  │  └───────────────┘            │  │
│  │                               │  │
│  │  AVAILABLE SP: 0 SP          │  │  ← Orange when > 0
│  │                               │  │
│  │  💪 STRENGTH      30/80 [+]  │  │
│  │     ██████░░░░░░░░░░░░       │  │
│  │                               │  │
│  │  🏌️ CLUB CONTROL  20/80 [+]  │  │
│  │     █████░░░░░░░░░░░░░       │  │
│  │                               │  │
│  │  ⏱️ RECOVERY       40/80 [+]  │  │
│  │     ██████████░░░░░░░░       │  │
│  │                               │  │
│  │  ⚡ STAMINA        27/80 [+]  │  │
│  │     ███████░░░░░░░░░░░       │  │
│  │                               │  │
│  │  ┌──────┐  ┌──────┐ ┌──────┐│  │
│  │  │RESET │  │CANCEL│ │CONFIRM││  │
│  │  └──────┘  └──────┘ └──────┘│  │
│  └──────────────────────────────┘  │
└────────────────────────────────────┘
```

### Character Compare View

```
┌─────────────────┬─────────────────┐
│  ELIZABETH      │  SHAE           │
│  BLACKWOOD      │  O'CONNELL   ◎  │
│                 │                 │
│  RARE  Lv 80   │ LEGENDARY Lv160 │
│                 │                 │
│  💪 12/30       │  💪 12/30       │
│  ████░░░        │  ████░░░        │
│                 │                 │
│  🏌️ 30/30 MAX   │  🏌️ 20/20 MAX   │
│  ████████       │  ████████       │
│                 │                 │
│  ⏱️ 15/20       │  ⏱️ 30/40       │
│  ██████░        │  ██████░        │
│                 │                 │
│  ⚡ 22/27       │  ⚡ 9/27 (RED)  │
│  ████████       │  ███ (WARN)     │
│                 │                 │
│  [LEVEL UP]     │  [LEVEL UP]     │
│  [BOOST]        │  [BOOST]        │
│                 │                 │
│  "BIO"          │  "BIO"          │
│  ...            │  ...            │
│                 │                 │
│  [CLOSE COMPARE]│  [COMPARE]      │
│                 │                 │
│  [   SWAP   ]   │  [ SELECTED ]   │
└─────────────────┴─────────────────┘
```

---

## Updated Data Architecture

### 1.1 Character Data Model

Based on actual design requirements:

```csharp
[CreateAssetMenu(fileName = "Character", menuName = "GOLFIN/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterId;              // e.g., "char_elizabeth"
    public string characterName;            // "Elizabeth Blackwood"
    public string characterNickname;        // "Elizabeth" (carousel display)
    public Sprite portraitThumbnail;        // Small (carousel)
    public Sprite portraitFull;             // Full-body (detail panel)
    public CharacterRarity rarity;          // C, U, R, M, L, S
    
    [Header("Base Stats (Level 1)")]
    public int baseStrength;                // 💪 Power/distance
    public int baseClubControl;             // 🏌️ Accuracy
    public int baseRecovery;                // ⏱️ HP regen / terrain mitigation
    public int baseStamina;                 // ⚡ Endurance
    
    [Header("Leveling Curves")]
    public int maxLevel = 199;              // From reference (Lv X/199)
    public AnimationCurve levelUpCostCurve; // R cost per level (changes every 10 levels)
    public int[] levelUpCostTable;          // Alternative: table of costs per 10-level bracket
    
    [Header("SP System")]
    public int spPerLevel = 1;              // Default 1 SP per level
    public int maxStatValue = 80;           // From reference (stats go up to /80)
    
    [Header("Visual & Audio")]
    public Color rarityColor;               // C=Gray, U=Green, R=Blue, M=Purple, L=Gold, S=Special
    public string rarityLabel;              // "COMMON", "RARE", "LEGENDARY"
    public AudioClip levelUpSound;
    public GameObject levelUpVFX;           // Particle effect
    
    [Header("Localization")]
    public string nameKey;                  // "CHAR_NAME_ELIZABETH"
    public string bioKey;                   // "CHAR_BIO_ELIZABETH"
}

public enum CharacterRarity
{
    Common,      // C - Gray
    Uncommon,    // U - Green
    Rare,        // R - Blue
    Master,      // M - Purple
    Legendary,   // L - Gold
    Special      // S - Special green
}
```

### 1.2 Player Character Data (SP System)

```csharp
[System.Serializable]
public class PlayerCharacterData
{
    public string characterId;
    public int currentLevel;                // 1 - 199
    public int totalSPEarned;               // From leveling up
    public int spentStrength;               // SP allocated to Strength
    public int spentClubControl;            // SP allocated to Club Control
    public int spentRecovery;               // SP allocated to Recovery
    public int spentStamina;                // SP allocated to Stamina
    public bool isSelected;                 // Active roster slot
    public System.DateTime acquiredDate;
    
    // Calculated properties
    public int GetAvailableSP() => totalSPEarned - GetTotalSpentSP();
    public int GetTotalSpentSP() => spentStrength + spentClubControl + spentRecovery + spentStamina;
    
    public int GetCurrentStrength(CharacterData baseData) => baseData.baseStrength + spentStrength;
    public int GetCurrentClubControl(CharacterData baseData) => baseData.baseClubControl + spentClubControl;
    public int GetCurrentRecovery(CharacterData baseData) => baseData.baseRecovery + spentRecovery;
    public int GetCurrentStamina(CharacterData baseData) => baseData.baseStamina + spentStamina;
    
    public int GetLevelUpCost(CharacterData baseData)
    {
        // Cost changes every 10 levels (from Confluence doc)
        int bracket = currentLevel / 10;
        return baseData.levelUpCostTable[bracket];
    }
}
```

---

## Phase-by-Phase Implementation Plan

## Phase 1: Data Architecture (~3 hours)

### 1.1 Create Core Data Structures
- **CharacterData.cs** - ScriptableObject with all properties from design
- **CharacterDatabase.cs** - Singleton holding all characters
- **PlayerCharacterData.cs** - Serializable player-owned character state
- **PlayerCharacterInventory.cs** - Save/load system (PlayerPrefs JSON)

### 1.2 Create Level-Up Cost Table
From Confluence: "cost changes every 10 levels"
```csharp
// Example cost table (adjust based on game economy)
int[] levelUpCostTable = {
    100,   // Levels 1-9
    150,   // Levels 10-19
    225,   // Levels 20-29
    350,   // Levels 30-39
    525,   // Levels 40-49
    800,   // Levels 50-59
    1200,  // Levels 60-69
    // ... up to level 199
};
```

### 1.3 Reward Points Manager
**Create: `RewardPointsManager.cs`**
- Singleton pattern
- Methods: GetPoints(), SpendPoints(amount), CanAfford(amount)
- Save/load from PlayerPrefs
- UI update events

---

## Phase 2: UI Structure (~4-5 hours)

### 2.1 Screen Hierarchy (Matches Reference Design)

```
RosterCanvas (Canvas)
├── Header (Top bar)
│   ├── RewardPointsDisplay (R 50000)
│   ├── TitleText ("ROSTER")
│   ├── SettingsButton (Gear icon)
│   └── FilterButton (List icon)
│
├── CharacterCarousel (Horizontal scroll)
│   ├── LeftArrowButton
│   ├── ScrollView
│   │   └── Content (Horizontal Layout Group)
│   │       ├── CharacterThumbnail (Prefab x N)
│   │       └── ...
│   ├── RightArrowButton
│   └── PaginationDots (Container)
│       ├── Dot (Active)
│       ├── Dot (Inactive)
│       └── ...
│
├── CharacterDetailPanel (Single or Compare mode)
│   ├── SingleView (Default)
│   │   ├── PortraitImage (Full-body)
│   │   ├── NameText ("ELIZABETH BLACKWOOD")
│   │   ├── StatusIcons (Selected, Boost)
│   │   ├── RarityLabel ("RARE" - color-coded)
│   │   ├── LevelText ("Lv 80/199")
│   │   ├── StatsSection
│   │   │   ├── StatBar (Strength) - Icon + Bar + Value
│   │   │   ├── StatBar (Club Control)
│   │   │   ├── StatBar (Recovery)
│   │   │   └── StatBar (Stamina)
│   │   ├── ActionButtons
│   │   │   ├── LevelUpButton
│   │   │   └── BoostButton
│   │   ├── BioSection
│   │   │   ├── BioLabel ("BIO")
│   │   │   └── BioText (Scrollable)
│   │   ├── CompareButton
│   │   └── SelectButton (SELECT / SELECTED)
│   │
│   └── CompareView (Split 50/50)
│       ├── LeftCharacterPanel (Same structure as SingleView, compact)
│       └── RightCharacterPanel (Same structure, with SWAP/SELECTED)
│
├── LevelUpModal (Overlay - from reference)
│   ├── Backdrop (Dark semi-transparent)
│   └── ModalPanel (Centered)
│       ├── CharacterHeader
│       │   ├── NameText
│       │   ├── StatusIcons
│       │   ├── RarityLabel
│       │   └── CurrentLevelText ("Lv 160/199")
│       ├── LevelUpInfoSection
│       │   ├── NextLevelText ("→ Lv 161" - Orange)
│       │   ├── CostText ("R 805")
│       │   ├── RewardText ("1 SP" - Orange)
│       │   └── LevelUpButton (Gold gradient)
│       ├── SPAllocationSection
│       │   ├── AvailableSPText ("AVAILABLE SP: 0 SP" - Orange if > 0)
│       │   ├── StatSlider (Strength) - Bar + Value + [+] button
│       │   ├── StatSlider (Club Control)
│       │   ├── StatSlider (Recovery)
│       │   └── StatSlider (Stamina)
│       └── ActionButtons
│           ├── ResetButton (Silver)
│           ├── CancelButton (Silver outline)
│           └── ConfirmButton (Gold)
│
└── BottomNavBar (Fixed footer)
    ├── HomeButton
    ├── CardsButton
    ├── PlayButton (Elevated, larger)
    ├── TournamentsButton
    └── ProfileButton
```

### 2.2 Character Thumbnail Prefab (Carousel Card)

**Components:**
- Background (Image) with rarity-color overlay
- Portrait (Image) - thumbnail sprite
- Name Label (TMP) - character nickname
- Rarity Badge (Top-left) - "C", "U", "R", "M", "L", "S"
- Level Badge (Top-right) - "Lv X"
- Selection Highlight (Border/glow when selected)
- Button component (onClick → Select character)

**Size:** Approx 150x200px (adjust for screen width / 6 cards visible)

### 2.3 StatBar Prefab (Reusable)

**Components:**
- Icon (Image) - stat icon (💪, 🏌️, ⏱️, ⚡)
- Label (TMP) - stat name (localized)
- Background Bar (Image) - dark background
- Fill Bar (Image) - blue fill (or orange/red for critical)
- Value Text (TMP) - "12/30" format
- Optional: [+] Button (for SP allocation modal)

**States:**
- Normal (Blue fill)
- Critical (Orange/Red fill) - when value < 33% of max
- Maxed (Full fill, maybe gold color)

---

## Phase 3: Core Scripts (~5-6 hours)

### 3.1 Character Manager

**Create: `CharacterManager.cs` (Singleton)**
```csharp
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }
    
    [SerializeField] private CharacterDatabase database;
    
    public CharacterData GetCharacter(string id);
    public PlayerCharacterData GetPlayerCharacter(string id);
    public List<PlayerCharacterData> GetAllOwnedCharacters();
    
    // Level-up logic
    public bool CanLevelUp(string id);
    public int GetLevelUpCost(string id);
    public void LevelUp(string id);  // Spends R, adds SP
    
    // SP allocation logic (temporary until confirmed)
    public void AllocateSP(string id, string statName, int amount);
    public void ResetSPAllocation(string id);
    public void ConfirmSPAllocation(string id);
    
    // Roster management
    public void SelectCharacter(string id);
    public void SwapCharacters(string id1, string id2);
    
    // Stat calculation
    public int GetCurrentStat(string id, string statName);
    public int GetMaxStat(string id, string statName);
}
```

### 3.2 Roster Screen Controller

**Create: `RosterScreenController.cs`**
```csharp
public class RosterScreenController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Transform carouselContent;
    [SerializeField] private GameObject characterThumbnailPrefab;
    [SerializeField] private CharacterDetailPanel detailPanel;
    [SerializeField] private LevelUpModal levelUpModal;
    [SerializeField] private TextMeshProUGUI rewardPointsText;
    
    private string currentCharacterId;
    private string compareCharacterId;  // For compare mode
    
    void Start()
    {
        PopulateCarousel();
        SelectCharacter(GetFirstOwnedCharacter());
        UpdateRewardPointsDisplay();
    }
    
    void PopulateCarousel()
    {
        var characters = CharacterManager.Instance.GetAllOwnedCharacters();
        foreach (var playerChar in characters)
        {
            var thumbnail = Instantiate(characterThumbnailPrefab, carouselContent);
            var card = thumbnail.GetComponent<CharacterThumbnailCard>();
            card.Initialize(playerChar.characterId);
            card.OnClicked += () => SelectCharacter(playerChar.characterId);
        }
        
        UpdatePaginationDots(characters.Count);
    }
    
    void SelectCharacter(string id)
    {
        currentCharacterId = id;
        detailPanel.ShowCharacter(id, false);  // false = single view
        UpdateCarouselSelection(id);
    }
    
    public void OnCompareClicked()
    {
        // Enter compare mode - show split view
        detailPanel.ShowCompare(currentCharacterId, compareCharacterId);
    }
    
    public void OnLevelUpClicked()
    {
        levelUpModal.Open(currentCharacterId);
    }
    
    public void OnSelectClicked()
    {
        CharacterManager.Instance.SelectCharacter(currentCharacterId);
        detailPanel.UpdateSelectButton(true);  // Show "SELECTED"
    }
    
    public void OnSwapClicked(string newCharacterId)
    {
        CharacterManager.Instance.SwapCharacters(currentCharacterId, newCharacterId);
        detailPanel.Refresh();
    }
    
    void UpdateRewardPointsDisplay()
    {
        int points = RewardPointsManager.Instance.GetPoints();
        rewardPointsText.text = $"R {points}";
    }
}
```

### 3.3 Level-Up Modal

**Create: `LevelUpModal.cs` (Extends ModalController from Phase 3)**
```csharp
public class LevelUpModal : ModalController
{
    [Header("Level-Up UI")]
    [SerializeField] private TextMeshProUGUI nextLevelText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button levelUpButton;
    
    [Header("SP Allocation")]
    [SerializeField] private TextMeshProUGUI availableSPText;
    [SerializeField] private SPStatSlider strengthSlider;
    [SerializeField] private SPStatSlider clubControlSlider;
    [SerializeField] private SPStatSlider recoverySlider;
    [SerializeField] private SPStatSlider staminaSlider;
    
    [Header("Action Buttons")]
    [SerializeField] private Button resetButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmButton;
    
    private string currentCharacterId;
    private int pendingLevel;
    private int pendingStrengthSP;
    private int pendingClubControlSP;
    private int pendingRecoverySP;
    private int pendingStaminaSP;
    
    public void Open(string characterId)
    {
        currentCharacterId = characterId;
        ResetPendingValues();
        UpdateUI();
        Show();  // From ModalController base class
    }
    
    public void OnLevelUpButtonClicked()
    {
        // Spend R to gain 1 level and earn 1 SP
        int cost = CharacterManager.Instance.GetLevelUpCost(currentCharacterId);
        
        if (RewardPointsManager.Instance.CanAfford(cost))
        {
            RewardPointsManager.Instance.SpendPoints(cost);
            CharacterManager.Instance.LevelUp(currentCharacterId);
            pendingLevel++;
            UpdateUI();
            PlayLevelUpEffect();
        }
    }
    
    public void OnStatPlusClicked(string statName)
    {
        // Allocate 1 SP to the stat (temporary until confirmed)
        int availableSP = GetAvailableSP();
        if (availableSP > 0)
        {
            switch (statName)
            {
                case "Strength": pendingStrengthSP++; break;
                case "ClubControl": pendingClubControlSP++; break;
                case "Recovery": pendingRecoverySP++; break;
                case "Stamina": pendingStaminaSP++; break;
            }
            UpdateUI();
        }
    }
    
    public void OnResetClicked()
    {
        // Reset all pending SP allocations
        pendingStrengthSP = 0;
        pendingClubControlSP = 0;
        pendingRecoverySP = 0;
        pendingStaminaSP = 0;
        UpdateUI();
    }
    
    public void OnConfirmClicked()
    {
        // Apply all pending SP allocations
        if (GetAvailableSP() == 0)  // Must allocate all SP
        {
            CharacterManager.Instance.ConfirmSPAllocation(currentCharacterId);
            Hide();
            OnLevelUpComplete?.Invoke(currentCharacterId);
        }
    }
    
    public void OnCancelClicked()
    {
        // Discard all changes
        CharacterManager.Instance.ResetSPAllocation(currentCharacterId);
        Hide();
    }
    
    int GetAvailableSP()
    {
        var playerChar = CharacterManager.Instance.GetPlayerCharacter(currentCharacterId);
        int totalEarned = playerChar.totalSPEarned;
        int totalSpent = pendingStrengthSP + pendingClubControlSP + pendingRecoverySP + pendingStaminaSP;
        return totalEarned - totalSpent;
    }
    
    void UpdateUI()
    {
        var playerChar = CharacterManager.Instance.GetPlayerCharacter(currentCharacterId);
        var baseData = CharacterManager.Instance.GetCharacter(currentCharacterId);
        
        // Level-up info
        nextLevelText.text = $"→ Lv {pendingLevel + 1}";
        costText.text = $"R {CharacterManager.Instance.GetLevelUpCost(currentCharacterId)}";
        rewardText.text = "1 SP";
        
        // SP allocation
        int availableSP = GetAvailableSP();
        availableSPText.text = $"AVAILABLE SP: {availableSP} SP";
        availableSPText.color = availableSP > 0 ? Color.orange : Color.white;
        
        // Update stat sliders
        strengthSlider.UpdateValue(playerChar.GetCurrentStrength(baseData) + pendingStrengthSP, baseData.maxStatValue);
        clubControlSlider.UpdateValue(playerChar.GetCurrentClubControl(baseData) + pendingClubControlSP, baseData.maxStatValue);
        recoverySlider.UpdateValue(playerChar.GetCurrentRecovery(baseData) + pendingRecoverySP, baseData.maxStatValue);
        staminaSlider.UpdateValue(playerChar.GetCurrentStamina(baseData) + pendingStaminaSP, baseData.maxStatValue);
        
        // Button states
        levelUpButton.interactable = RewardPointsManager.Instance.CanAfford(GetLevelUpCost());
        confirmButton.interactable = (availableSP == 0);  // Must allocate all SP
    }
    
    public System.Action<string> OnLevelUpComplete;
}
```

### 3.4 Character Detail Panel

**Create: `CharacterDetailPanel.cs`**
- Switches between Single View and Compare View
- Displays character stats, portrait, bio
- Handles SELECT/SWAP button states
- Integrates with CharacterManager for data

---

## Phase 4: Editor Tools (~2-3 hours)

### 4.1 Character Database Builder

**Tool: `Tools → GOLFIN → Build Character Database`**
- Scans `Assets/Data/Characters/` for CharacterData
- Populates CharacterDatabase ScriptableObject
- Validates all references (portraits, stats)

### 4.2 CSV Import Tool

**Tool: `Tools → GOLFIN → Import Characters from CSV`**
```csv
id,name,nickname,rarity,baseStr,baseCtrl,baseRec,baseStam,portraitThumb,portraitFull,maxLevel
char_elizabeth,Elizabeth Blackwood,Elizabeth,Rare,8,10,7,9,Elizabeth_thumb.png,Elizabeth_full.png,199
char_shae,Shae O'Connell,Shae,Legendary,8,6,12,8,Shae_thumb.png,Shae_full.png,199
char_james,James,James,Common,6,7,6,6,James_thumb.png,James_full.png,199
char_olivia,Olivia,Olivia,Uncommon,7,8,6,7,Olivia_thumb.png,Olivia_full.png,199
```

Creates CharacterData ScriptableObjects automatically.

### 4.3 Roster UI Builder

**Tool: `Tools → GOLFIN → Build Roster Screen`**
- One-click creation of complete hierarchy (from Phase 2.1)
- All references auto-wired
- Sample characters populated (if database exists)

---

## Phase 5: Integration (~3 hours)

### 5.1 Reward Points Integration
- Display in header (R 50000)
- Update on spend/earn
- Validation before level-up

### 5.2 Navigation
- Home Screen → Characters Button → Roster Screen
- Back Button → Returns to Home
- Bottom nav bar integration

### 5.3 Localization (Priority Keys)
```csv
key,English,Japanese
ROSTER_TITLE,ROSTER,名簿
ROSTER_LEVEL,Level,レベル
ROSTER_REWARD_POINTS,Reward Points,報酬ポイント
ROSTER_NEXT_LEVEL,Next Level,次のレベル
ROSTER_COST,Cost,コスト
ROSTER_REWARD,Reward,報酬
ROSTER_LEVEL_UP,LEVEL UP,レベルアップ
ROSTER_BOOST,BOOST,ブースト
ROSTER_COMPARE,COMPARE,比較
ROSTER_CLOSE_COMPARE,CLOSE COMPARE,比較を閉じる
ROSTER_SELECT,SELECT,選択
ROSTER_SELECTED,SELECTED,選択済み
ROSTER_SWAP,SWAP,交換
ROSTER_AVAILABLE_SP,Available SP,利用可能SP
ROSTER_RESET,RESET,リセット
ROSTER_CANCEL,CANCEL,キャンセル
ROSTER_CONFIRM,CONFIRM,確認
ROSTER_BIO,BIO,プロフィール
ROSTER_INSUFFICIENT_POINTS,Not enough Reward Points,報酬ポイントが不足しています
ROSTER_MAX_LEVEL,Max Level Reached,最大レベル到達
ROSTER_STRENGTH,STRENGTH,ストレングス
ROSTER_CLUB_CONTROL,CLUB CONTROL,クラブコントロール
ROSTER_RECOVERY,RECOVERY,リカバリー
ROSTER_STAMINA,STAMINA,スタミナ
CHAR_NAME_ELIZABETH,Elizabeth Blackwood,エリザベス・ブラックウッド
CHAR_NAME_SHAE,Shae O'Connell,シェイ・オコネル
CHAR_NAME_JAMES,James,ジェームズ
CHAR_NAME_OLIVIA,Olivia,オリビア
CHAR_BIO_ELIZABETH,Elizabeth is a skilled golfer from England...,エリザベスはイングランド出身の熟練ゴルファー...
```

---

## Phase 6: Testing & Polish (~3 hours)

### 6.1 Functional Testing
- [ ] Carousel scrolling (left/right arrows, swipe)
- [ ] Character selection updates detail panel
- [ ] Stat bars display correctly (values, fill, colors)
- [ ] Level-up button: spend R → earn SP → level increases
- [ ] SP allocation: [+] buttons work, Available SP decreases
- [ ] Reset button: clears all pending SP allocations
- [ ] Confirm button: only active when all SP allocated
- [ ] Cancel button: discards changes, closes modal
- [ ] Compare mode: shows two characters side-by-side
- [ ] Swap functionality: changes active roster slot
- [ ] Select button: gold "SELECTED" when active
- [ ] Reward Points display updates after spending

### 6.2 Visual Polish
- [ ] Rarity colors match reference (C=Gray, U=Green, R=Blue, M=Purple, L=Gold, S=Special)
- [ ] Critical stat warning (red/orange) when < 33% max
- [ ] Carousel pagination dots update
- [ ] Level-up success animation (confetti/sparkle)
- [ ] Modal backdrop darkens correctly
- [ ] Bottom nav center button elevated

### 6.3 Audio
- [ ] Card selection sound
- [ ] Level-up success sound
- [ ] SP allocation click sound
- [ ] Insufficient points warning sound
- [ ] Confirm/cancel sounds

---

## Phase 7: Advanced Features (Post-MVP)

### 7.1 Character Compare Enhancements
- Highlight superior stats (green up arrow, red down arrow)
- "Recommended" badge when one character is objectively better
- Filter characters by rarity/level for comparison

### 7.2 Roster Filters & Sorting
- Filter by rarity (C, U, R, M, L, S)
- Sort by level (high/low)
- Sort by rarity
- Sort by stat (highest Strength, etc.)
- Search by name

### 7.3 Character Unlock Animations
- Full-screen reveal when new character acquired
- Rarity-specific VFX (gold sparkles for Legendary)
- Add to roster animation

### 7.4 Server Integration
- Migrate from PlayerPrefs to server database
- Anti-cheat validation (server verifies SP allocation)
- Cloud save (sync across devices)

---

## Answers to Original Questions (Based on References)

1. **How many characters exist?** Unknown from references (assume 10-50 for MVP)
2. **Do players start with a default character?** Likely (James, Olivia, or Elizabeth)
3. **How are new characters acquired?** Gacha + Marketplace (from Confluence doc)
4. **Reward Points economy?** Level-up costs increase every 10 levels (100 → 150 → 225...)
5. **Should costs scale with rarity?** Possibly (Legendary might cost more per level)
6. **Stat cap?** 80 (from reference: "30/80", "40/80")
7. **Visual evolution?** Not in MVP (portraits are static)
8. **Character descriptions?** Yes - "BIO" section with backstory
9. **Character roles?** Implied by stat distribution (Strength-focused = Power hitter, etc.)
10. **Reference images?** ✅ Provided and analyzed!

---

## Updated Timeline

### Day 1 (Tomorrow):
**Morning (4 hours):**
- Phase 1: Data Architecture (CharacterData, Database, PlayerData, SP system)
- Phase 2 Part 1: UI Prefabs (StatBar, CharacterThumbnail)

**Afternoon (4 hours):**
- Phase 2 Part 2: Screen Hierarchy (Carousel, Detail Panel, Level-Up Modal)
- Phase 3 Part 1: CharacterManager, RosterScreenController

### Day 2:
**Morning (4 hours):**
- Phase 3 Part 2: LevelUpModal, CharacterDetailPanel
- Phase 4: Editor Tools (Database Builder, CSV Import, UI Builder)

**Afternoon (3 hours):**
- Phase 5: Integration (Reward Points, Navigation, Localization)
- Phase 6 Part 1: Functional Testing

### Day 3:
**Morning (3 hours):**
- Phase 6 Part 2: Visual Polish + Audio Integration
- Bug fixes

**Afternoon (2 hours):**
- Final testing
- Documentation updates
- Prepare for Compare/Swap features

---

## Dependencies Checklist

- ✅ **Localization System** (Phase 3)
- ✅ **ModalController** (Phase 3, for Level-Up Modal)
- ✅ **LocalizationEditorHelper** (Phase 3)
- ⚠️ **Reward Points Manager** (Create in Phase 1)
- ⚠️ **Character portraits** (Full-body + Thumbnails)
- ⚠️ **Stat icons** (💪, 🏌️, ⏱️, ⚡)
- ⚠️ **Rarity badge sprites** (C, U, R, M, L, S)
- ⚠️ **Audio clips** (level-up, click, warning)

---

## Key Differences from Original Plan

### ✅ SP (Stat Points) System Added
- Level-up now rewards SP instead of auto-increasing stats
- Players manually allocate SP via [+] buttons
- Must allocate all SP before confirming
- Reset button allows experimentation

### ✅ Level-Up Modal Detailed
- Exactly matches reference design
- Level up → Earn SP → Allocate → Confirm flow
- Cost/Reward displayed prominently
- Available SP counter (orange when > 0)

### ✅ Character Compare & Swap
- Split-panel side-by-side comparison
- Swap functionality for roster management
- Select/Selected button states

### ✅ Rarity System Refined
- 6 rarities (not 5): C, U, R, M, L, S
- Color-coded labels and backgrounds
- Affects max stat cap (80 for Legendary)

### ✅ Carousel UI
- Horizontal scrolling thumbnails
- Left/right arrows + swipe
- Pagination dots
- Rarity + Level badges on each card

---

**Created by:** Kai (Aikenken Bot)  
**Updated:** 2026-03-05 (After Reference Review)  
**For:** Cesar Guarinoni  
**Start Date:** 2026-03-06  
**Status:** Ready for Phase 1!
