# Phase 2b: Character Detail Panel — Implementation Spec (v2)

**Author:** Claude (Architect)  
**Date:** 2026-03-16  
**For:** Claude Code implementation  
**Prerequisites:** Phase 2a complete, DetailPanel hierarchy already built in Unity  
**Visual References:** `Assets/References/Roster Screen/`

---

## Overview

The DetailPanel UI hierarchy is ALREADY BUILT in Unity under `RosterScreen > CarouselSection > DetailPanel`. All GameObjects, layout, dividers, buttons, and stat icon sprites are in place. Stats currently show placeholder "999/999" values.

**This task is purely DATA BINDING** — rewrite `CharacterDetailPanel.cs` to populate the existing UI elements from `CharacterManager` data when a carousel card is tapped.

**DO NOT recreate or restructure the hierarchy. Work with what exists.**

---

## 1. Existing Hierarchy (DO NOT MODIFY)

```
DetailPanel
├── LeftPanel
│   └── Character                    ← Image (full-body portrait)
│
└── RightPanel
    ├── CharacterNamePanel
    │   └── CharacterNameText        ← TMP (single field, use \n for first/last)
    ├── Divider
    ├── RarityPanel
    │   └── RarityRow
    │       ├── [RarityLabel]        ← TMP ("LEGENDARY", colored)
    │       ├── [CurrentLevel]       ← TMP ("Lv 160")
    │       └── [MaxLevel]           ← TMP ("/199", smaller font)
    ├── Divider
    ├── CharacterStatsPanel
    │   ├── CharacterStats1          ← Strength
    │   │   ├── StatIcon             ← Image (IconStrenght sprite, already assigned)
    │   │   ├── Name+Bar
    │   │   │   ├── StatsName        ← TMP ("STRENGHT")
    │   │   │   └── Bar              ← Image (fill bar)
    │   │   └── StatNumber           ← TMP ("999/999")
    │   ├── CharacterStats2          ← Club Control (same structure)
    │   ├── CharacterStats3          ← Recovery (same structure)
    │   └── CharacterStats4          ← Stamina (same structure)
    ├── Divider
    ├── ButtonsPanel
    │   ├── LevelUpButton            ← Button
    │   └── BoostButton              ← Button
    ├── Divider
    ├── BioPanel
    │   ├── BioHeader                ← TMP ("BIO")
    │   └── BioText                  ← TMP (paragraph)
    ├── Divider
    ├── CompareButton                ← Button
    │   └── Text (TMP)
    ├── Divider
    └── SelectButton                 ← Button
        ├── Text (TMP)
        └── Rim
```

### Important Notes:
- Stat icons are already assigned (IconStrenght, etc.) — do NOT reassign
- The stat structure is NOT using StatBar.cs prefabs — each is a manual hierarchy
  with StatIcon, StatsName, Bar (Image), and StatNumber (TMP)
- Full-body portraits are currently manually assigned to the Character Image
- CharacterNameText is ONE TMP field — use line break for first/last name
- RarityRow has 3 TMP children for rarity label, current level, and max level

---

## 2. CharacterDetailPanel.cs — Full Rewrite

**File:** `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs`  
**Namespace:** `Golfin.Roster`

Replace the entire current stub. Keep the namespace and the event subscription pattern.

### Serialized Fields

```csharp
[Header("Portrait")]
[SerializeField] private Image characterImage;           // LeftPanel > Character

[Header("Name")]
[SerializeField] private TextMeshProUGUI characterNameText; // RightPanel > CharacterNamePanel > CharacterNameText

[Header("Rarity & Level")]
[SerializeField] private TextMeshProUGUI rarityLabel;     // RarityRow child 0
[SerializeField] private TextMeshProUGUI currentLevelText; // RarityRow child 1
[SerializeField] private TextMeshProUGUI maxLevelText;     // RarityRow child 2

[Header("Stat Bars — Strength")]
[SerializeField] private TextMeshProUGUI strengthName;     // CharacterStats1 > Name+Bar > StatsName
[SerializeField] private Image strengthBar;                // CharacterStats1 > Name+Bar > Bar
[SerializeField] private TextMeshProUGUI strengthNumber;   // CharacterStats1 > StatNumber

[Header("Stat Bars — Club Control")]
[SerializeField] private TextMeshProUGUI clubControlName;
[SerializeField] private Image clubControlBar;
[SerializeField] private TextMeshProUGUI clubControlNumber;

[Header("Stat Bars — Recovery")]
[SerializeField] private TextMeshProUGUI recoveryName;
[SerializeField] private Image recoveryBar;
[SerializeField] private TextMeshProUGUI recoveryNumber;

[Header("Stat Bars — Stamina")]
[SerializeField] private TextMeshProUGUI staminaName;
[SerializeField] private Image staminaBar;
[SerializeField] private TextMeshProUGUI staminaNumber;

[Header("Buttons")]
[SerializeField] private Button levelUpButton;
[SerializeField] private Button boostButton;
[SerializeField] private Button compareButton;
[SerializeField] private Button selectButton;
[SerializeField] private TextMeshProUGUI selectButtonText;  // SelectButton > Text (TMP)

[Header("Bio")]
[SerializeField] private TextMeshProUGUI bioText;           // BioPanel > BioText

[Header("Status Icons (Optional — add when ready)")]
[SerializeField] private GameObject selectedIcon;            // Eye icon, null until added to hierarchy
[SerializeField] private GameObject lowStaminaIcon;          // Bolt icon, null until added to hierarchy

[Header("Colors")]
[SerializeField] private Color normalBarColor = new Color(0.2f, 0.6f, 1f, 1f);    // Blue
[SerializeField] private Color criticalBarColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Red
[SerializeField] private Color maxBarColor = new Color(0.2f, 1f, 0.4f, 1f);        // Green
```

### Rarity Colors (define as static or serialized)

```csharp
private static readonly Dictionary<string, Color> RarityColors = new()
{
    { "Common",    ColorUtility.TryParseHtmlString("#808080", out var c0) ? c0 : Color.gray },
    { "Uncommon",  ColorUtility.TryParseHtmlString("#4A90E2", out var c1) ? c1 : Color.blue },
    { "Rare",      ColorUtility.TryParseHtmlString("#2ECC71", out var c2) ? c2 : Color.green },
    { "Mythic",    ColorUtility.TryParseHtmlString("#F1C40F", out var c3) ? c3 : Color.yellow },
    { "Legendary", ColorUtility.TryParseHtmlString("#E74C3C", out var c4) ? c4 : Color.red },
    { "Supreme",   ColorUtility.TryParseHtmlString("#9B59B6", out var c5) ? c5 : Color.magenta },
};
```

**Note:** If `RarityHelper` in `CharacterDatabase.cs` already has a color mapping method, use that instead of duplicating. Check `CharacterDatabase.cs` for `RarityHelper` before implementing.

### Private State

```csharp
private string currentCharacterId;
private const float LOW_STAMINA_THRESHOLD = 0.25f; // 25% of max energy
```

### Lifecycle

```csharp
private void OnEnable()
{
    CarouselController.OnCharacterSelected += UpdatePanel;
    
    if (CharacterManager.Instance != null)
    {
        CharacterManager.Instance.OnCharacterLeveledUp += OnLeveledUp;
        CharacterManager.Instance.OnCharacterSelected += OnSelectionChanged;
    }
}

private void OnDisable()
{
    CarouselController.OnCharacterSelected -= UpdatePanel;
    
    if (CharacterManager.Instance != null)
    {
        CharacterManager.Instance.OnCharacterLeveledUp -= OnLeveledUp;
        CharacterManager.Instance.OnCharacterSelected -= OnSelectionChanged;
    }
}

private void Start()
{
    if (levelUpButton != null) levelUpButton.onClick.AddListener(OnLevelUpClicked);
    if (boostButton != null) boostButton.onClick.AddListener(OnBoostClicked);
    if (compareButton != null) compareButton.onClick.AddListener(OnCompareClicked);
    if (selectButton != null) selectButton.onClick.AddListener(OnSelectClicked);
}
```

### UpdatePanel(string characterId) — Main Data Binding

```csharp
private void UpdatePanel(string characterId)
{
    currentCharacterId = characterId;
    
    var playerData = CharacterManager.Instance.GetCharacterData(characterId);
    if (playerData == null) return;
    
    // --- Get template data (base stats, rarity, portrait, bio) ---
    // This depends on how CharacterManager exposes template data.
    // Likely: CharacterManager.Instance.GetCharacterTemplate(characterId)
    // or CharacterDatabaseCSV.Instance.GetCharacter(characterId)
    // Claude Code: check what methods exist on CharacterManager and CharacterDatabaseCSV.
    
    // --- Portrait ---
    // Currently manually assigned. For now, skip dynamic assignment.
    // TODO: Load from CharacterDatabaseCSV portrait array by character index
    // characterImage.sprite = templateData.portrait;
    
    // --- Name (single TMP, line break for first/last) ---
    // If CSV has separate lastName field:
    //   characterNameText.text = $"{templateData.name.ToUpper()}\n{templateData.lastName.ToUpper()}";
    // If CSV has full name "Shae O'Connell":
    //   Split on first space, or store as-is with manual line break
    // For now, use what's available in the data:
    characterNameText.text = playerData.characterId; // PLACEHOLDER — replace with real name
    
    // --- Rarity ---
    string rarity = templateData.rarity; // "Common", "Rare", "Legendary", etc.
    rarityLabel.text = rarity.ToUpper();
    if (RarityColors.TryGetValue(rarity, out Color rarityColor))
        rarityLabel.color = rarityColor;
    
    // --- Level ---
    currentLevelText.text = $"Lv {playerData.currentLevel}";
    maxLevelText.text = $"/{templateData.maxLevel}"; // or "/199"
    
    // --- Stats ---
    UpdateStatBar(strengthName, strengthBar, strengthNumber, "STRENGTH",
        playerData.currentStrength, RarityStatCaps.GetCap(rarity, "Strength"));
    
    UpdateStatBar(clubControlName, clubControlBar, clubControlNumber, "CLUB CONTROL",
        playerData.currentClubControl, RarityStatCaps.GetCap(rarity, "ClubControl"));
    
    UpdateStatBar(recoveryName, recoveryBar, recoveryNumber, "RECOVERY",
        playerData.currentRecovery, RarityStatCaps.GetCap(rarity, "Recovery"));
    
    UpdateStatBar(staminaName, staminaBar, staminaNumber, "STAMINA",
        playerData.currentStamina, RarityStatCaps.GetCap(rarity, "Stamina"));
    
    // Override stamina bar color if energy is low
    if (playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD))
    {
        staminaBar.color = criticalBarColor;
    }
    
    // --- Status Icons ---
    if (selectedIcon != null)
        selectedIcon.SetActive(playerData.isSelected);
    if (lowStaminaIcon != null)
        lowStaminaIcon.SetActive(playerData.IsStaminaLow(LOW_STAMINA_THRESHOLD));
    
    // --- Select Button ---
    UpdateSelectButton(playerData.isSelected);
    
    // --- Bio ---
    // bioText.text = templateData.bio; // When bio field is added to CSV
    bioText.text = "Bio coming soon."; // PLACEHOLDER
}
```

### Helper: UpdateStatBar

```csharp
private void UpdateStatBar(TextMeshProUGUI nameField, Image bar, TextMeshProUGUI numberField,
    string label, int currentValue, int capValue)
{
    if (nameField != null)
        nameField.text = label;
    
    if (numberField != null)
        numberField.text = $"{currentValue}/{capValue}";
    
    if (bar != null)
    {
        float fillAmount = capValue > 0 ? (float)currentValue / capValue : 0f;
        bar.fillAmount = fillAmount;
        
        if (fillAmount >= 1f)
            bar.color = maxBarColor;
        else
            bar.color = normalBarColor;
    }
}
```

### Event Handlers

```csharp
private void OnLeveledUp(string characterId)
{
    if (characterId == currentCharacterId)
        UpdatePanel(characterId);
}

private void OnSelectionChanged(string characterId)
{
    // Refresh to update SELECT/SELECTED state
    if (currentCharacterId != null)
        UpdatePanel(currentCharacterId);
}

private void UpdateSelectButton(bool isSelected)
{
    if (selectButtonText != null)
        selectButtonText.text = isSelected ? "SELECTED" : "SELECT";
    
    // Optionally change button color/style
    // The visual references show gold for both states, with SELECTED being slightly different
    // For now, just change the text
}
```

### Button Click Handlers

```csharp
private void OnLevelUpClicked()
{
    Debug.Log($"[CharacterDetailPanel] Level Up clicked for {currentCharacterId}");
    // Phase 2c: Open LevelUpModal
}

private void OnBoostClicked()
{
    Debug.Log($"[CharacterDetailPanel] Boost clicked for {currentCharacterId}");
    // Future: Open Experience Boost modal
}

private void OnCompareClicked()
{
    Debug.Log($"[CharacterDetailPanel] Compare clicked for {currentCharacterId}");
    // Phase 2d: Enter compare mode
}

private void OnSelectClicked()
{
    if (string.IsNullOrEmpty(currentCharacterId)) return;
    
    Debug.Log($"[CharacterDetailPanel] Select clicked for {currentCharacterId}");
    CharacterManager.Instance.SelectCharacter(currentCharacterId);
    // Panel will refresh via OnCharacterSelected event
}
```

---

## 3. PlayerCharacterData.cs — Add Stamina Energy

**File:** `Assets/Scripts/UI/Roster/Data/PlayerCharacterData.cs`

Add these fields after the existing `isOwned` field:

```csharp
/// <summary>
/// Current stamina energy (depletes as character plays holes).
/// Separate from the stamina STAT which is leveled via SP.
/// Drives red stamina bar indicator and low-stamina bolt icon.
/// </summary>
[System.NonSerialized]
public float currentStaminaEnergy = 100f;

[System.NonSerialized]
public float maxStaminaEnergy = 100f;

/// <summary>
/// Check if stamina energy is below threshold (0.0 to 1.0)
/// </summary>
public bool IsStaminaLow(float threshold = 0.25f)
{
    return maxStaminaEnergy > 0 && (currentStaminaEnergy / maxStaminaEnergy) < threshold;
}
```

---

## 4. Characters.csv — Add Columns

**File:** `Assets/Data/Characters.csv`

Add `lastName` and `bio` columns. Update header and all rows:

```csv
id,name,lastName,rarity,baseStrength,baseClubControl,baseRecovery,baseStamina,portraitSprite,maxLevel,bio
char_elizabeth,Elizabeth,Blackwood,Rare,8,10,7,9,Elizabeth,199,"Elizabeth Blackwood, 46, a maverick from Cornwall, earned her Ladies European Tour spot through precision. With three decades on coastal courses her impeccable control makes her the top mentor for young talents."
char_shae,Shae,O'Connell,Legendary,12,8,15,10,Shae,199,"Shae O'Connell, 23, from County Clare, earned her Ladies European Tour card through finesse and course smarts. Decades on windswept links make her an ideal mentor for players refining control."
char_james,James,,Common,6,7,6,6,James,199,"A dependable player just starting out on the tour."
char_olivia,Olivia,,Uncommon,7,8,6,7,Olivia,199,"A rising talent with solid fundamentals."
char_camila,Camila,,Rare,9,9,8,8,Camila,199,"A versatile player known for consistency."
char_alejandro,Alejandro,,Mythic,10,11,9,12,Alejandro,199,"A powerful hitter with natural talent."
char_ean,Ean,,Uncommon,7,7,7,7,Ean,199,"A balanced player with room to grow."
char_freda,Freda,,Supreme,15,12,18,14,Freda,199,"A legendary figure on the tour circuit."
char_johan,Johan,,Rare,8,10,7,10,Johan,199,"Known for his precise short game."
char_mike,Mike,,Common,6,6,7,7,Mike,199,"An enthusiastic newcomer to competitive golf."
char_richard,Richard,,Mythic,11,10,10,11,Richard,199,"A seasoned pro with decades of experience."
char_roshana,Roshana,,Legendary,13,9,14,11,Roshana,199,"A fierce competitor with an unshakeable mindset."
```

**Note:** Empty lastName fields are fine — the display code should handle this:
```csharp
string displayName = string.IsNullOrEmpty(lastName) 
    ? firstName.ToUpper() 
    : $"{firstName.ToUpper()}\n{lastName.ToUpper()}";
```

---

## 5. CharacterDatabaseCSV.cs — Update Parser

**File:** `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs`

Update the CSV parser to read the new `lastName` and `bio` columns. The exact changes depend on how the parser is currently structured — Claude Code should read the file and add:
- Parse `lastName` field (column index 2, after name)
- Parse `bio` field (last column)
- Store both on whatever data structure the parsed characters use
- Handle quoted CSV fields for bio text (contains commas)

---

## 6. Portrait Loading (Future Improvement)

Currently full-body portraits are manually assigned in the Inspector. To make them dynamic:

1. Move full-body sprites to `Assets/Sprites/Characters/FullBody/`
2. Add a `fullBodyPortraits` Sprite array to `CharacterDatabaseCSV` (same pattern as `characterPortraits`)
3. Add a `fullBodySprite` column to CSV, OR use naming convention (`{name}_fullbody`)
4. In UpdatePanel, set: `characterImage.sprite = templateData.fullBodyPortrait;`

**For now:** Skip dynamic portrait loading. The manually assigned portrait works.  
**Claude Code:** Do NOT change portrait loading unless asked.

---

## 7. Implementation Order

1. **Add stamina energy fields** to PlayerCharacterData.cs
2. **Add lastName + bio columns** to Characters.csv
3. **Update CharacterDatabaseCSV.cs** to parse new columns
4. **Rewrite CharacterDetailPanel.cs** with all serialized fields and UpdatePanel logic
5. **Wire serialized fields** in Unity Inspector (Claude Code: output a checklist of what to assign)
6. **Test** carousel selection → detail panel data binding

---

## 8. Files to Modify

| File | Action |
|------|--------|
| `CharacterDetailPanel.cs` | **Full rewrite** — data binding to existing hierarchy |
| `PlayerCharacterData.cs` | **Add** currentStaminaEnergy, maxStaminaEnergy, IsStaminaLow() |
| `Characters.csv` | **Add** lastName and bio columns |
| `CharacterDatabaseCSV.cs` | **Update** CSV parser for new columns |

## 9. Files to NOT Modify

| File | Reason |
|------|--------|
| Unity hierarchy | Already built — don't restructure |
| StatBar.cs | Not used by current hierarchy (stats are manual GameObjects) |
| CarouselController.cs | Already fires OnCharacterSelected correctly |
| Stat icon sprites | Already assigned in Inspector |

---

## 10. Inspector Wiring Checklist

After rewriting CharacterDetailPanel.cs, these fields need to be assigned in the Unity Inspector on the DetailPanel GameObject:

- [ ] `characterImage` → DetailPanel > LeftPanel > Character
- [ ] `characterNameText` → RightPanel > CharacterNamePanel > CharacterNameText
- [ ] `rarityLabel` → RightPanel > RarityPanel > RarityRow > [first TMP child]
- [ ] `currentLevelText` → RightPanel > RarityPanel > RarityRow > [second TMP child]
- [ ] `maxLevelText` → RightPanel > RarityPanel > RarityRow > [third TMP child]
- [ ] `strengthName` → CharacterStats1 > Name+Bar > StatsName
- [ ] `strengthBar` → CharacterStats1 > Name+Bar > Bar
- [ ] `strengthNumber` → CharacterStats1 > StatNumber
- [ ] `clubControlName` → CharacterStats2 > Name+Bar > StatsName
- [ ] `clubControlBar` → CharacterStats2 > Name+Bar > Bar
- [ ] `clubControlNumber` → CharacterStats2 > StatNumber
- [ ] `recoveryName` → CharacterStats3 > Name+Bar > StatsName
- [ ] `recoveryBar` → CharacterStats3 > Name+Bar > Bar
- [ ] `recoveryNumber` → CharacterStats3 > StatNumber
- [ ] `staminaName` → CharacterStats4 > Name+Bar > StatsName
- [ ] `staminaBar` → CharacterStats4 > Name+Bar > Bar
- [ ] `staminaNumber` → CharacterStats4 > StatNumber
- [ ] `levelUpButton` → RightPanel > ButtonsPanel > LevelUpButton
- [ ] `boostButton` → RightPanel > ButtonsPanel > BoostButton
- [ ] `compareButton` → RightPanel > CompareButton
- [ ] `selectButton` → RightPanel > SelectButton
- [ ] `selectButtonText` → RightPanel > SelectButton > Text (TMP)
- [ ] `bioText` → RightPanel > BioPanel > BioText
- [ ] `selectedIcon` → null (add to hierarchy later)
- [ ] `lowStaminaIcon` → null (add to hierarchy later)

---

## 11. Testing Checklist

- [ ] Tapping carousel card updates detail panel with correct character
- [ ] Name shows as "FIRSTNAME\nLASTNAME" (uppercase)
- [ ] Characters without lastName show just first name
- [ ] Rarity label shows correct text and color per rarity
- [ ] Current level shows correctly ("Lv 1")
- [ ] Max level shows ("/199")
- [ ] 4 stat bars show correct current/cap values
- [ ] Stat bar fill amounts are proportional to current/cap
- [ ] Bars are blue normally, green when stat equals cap
- [ ] Stamina bar turns red when stamina energy is low (test by setting low energy in code)
- [ ] SELECT button works — calls CharacterManager.SelectCharacter
- [ ] SELECTED text appears for the active character
- [ ] Switching characters in carousel updates all fields
- [ ] Bio text displays (placeholder or real)
- [ ] Level Up / Boost / Compare buttons log to console
- [ ] No NullReferenceExceptions in console
