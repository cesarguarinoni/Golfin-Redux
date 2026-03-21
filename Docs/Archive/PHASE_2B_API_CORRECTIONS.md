# Phase 2b: API Corrections Addendum

**Date:** 2026-03-16  
**Context:** Based on review of actual CharacterManager.cs and CharacterDatabase.cs

Drop this file next to `PHASE_2B_DETAIL_PANEL_SPEC.md`. It overrides specific sections of the spec where the assumed API didn't match reality.

---

## CORRECTION 1: Getting Character Template Data

The spec assumes `CharacterManager.Instance.GetCharacterTemplate(characterId)` exists. It does NOT.

**CharacterManager currently exposes:**
- `GetCharacterData(string characterId)` → returns `PlayerCharacterData` (player instance)
- `GetAllOwnedCharacters()` → returns `List<PlayerCharacterData>`

**CharacterManager has a private field:**
- `private CharacterDatabase characterDatabase` — the ScriptableObject DB

**Fix — add this public method to CharacterManager.cs:**

```csharp
/// <summary>
/// Get the base character template (stats, rarity, portrait, bio key).
/// Used by detail panel, level-up modal, and compare view.
/// </summary>
public CharacterData? GetCharacterTemplate(string characterId)
{
    return characterDatabase?.GetCharacter(characterId);
}
```

---

## CORRECTION 2: Use RarityHelper (already exists)

The spec section "Helper: Rarity Colors" with `GetRarityColor` is REDUNDANT.

**Delete the GetRarityColor method from CharacterDetailPanel.cs entirely.**

Instead, use the existing utility:

```csharp
// In UpdatePanel:
rarityLabel.color = RarityHelper.GetRarityColor(templateData.rarity);
```

`RarityHelper` lives in `CharacterDatabase.cs` and is already a static class in the `Golfin.Roster` namespace. No import needed if CharacterDetailPanel is in the same namespace.

---

## CORRECTION 3: Character Name Fields

**CharacterData currently has:**
- `characterName` — e.g., "Character Name" (default)
- `characterNickname` — e.g., "Nickname"

**There is NO `lastName` field.**

**Two options for the detail panel name display (FIRST\nLAST):**

**Option A — Use characterName as full name, split at runtime:**
```csharp
// If characterName = "Elizabeth Blackwood"
string[] parts = templateData.characterName.Split(' ', 2);
string firstName = parts[0].ToUpper();
string lastName = parts.Length > 1 ? parts[1].ToUpper() : "";
characterNameText.text = $"{firstName}\n{lastName}";
```
This requires updating CSV/ScriptableObject data to store full names like "Elizabeth Blackwood".

**Option B — Add lastName field to CharacterData:**
```csharp
[Header("Identity")]
[SerializeField] public string characterLastName = "";
```
Then update CSV and the detail panel uses:
```csharp
characterNameText.text = $"{templateData.characterName.ToUpper()}\n{templateData.characterLastName.ToUpper()}";
```

**Recommendation:** Option B is cleaner. Add `characterLastName` to CharacterData.cs.

---

## CORRECTION 4: Bio Text

**CharacterData has `bioKey`** (a localization key like "CHAR_BIO_ELIZABETH") but no direct bio text.

Since localization is not wired yet, we need a fallback. Two options:

**Option A — Add a `bioText` field to CharacterData:**
```csharp
[Header("Localization")]
[SerializeField] public string nameKey = "CHAR_NAME_";
[SerializeField] public string bioKey = "CHAR_BIO_";
[SerializeField] [TextArea(3, 6)] public string bioFallbackText = "";  // Used when localization unavailable
```

**Option B — Load from CSV column:**
Add `bio` column to Characters.csv and parse in CharacterDatabaseCSV.

**Recommendation:** Option A for ScriptableObject characters, Option B if CSV is the primary data source. Since the project uses CSV-first architecture (per AI_CONTEXT.md), go with Option B — add bio column to CSV.

---

## CORRECTION 5: LoadRoster() is Empty

`CharacterManager.LoadRoster()` currently does nothing:

```csharp
private void LoadRoster()
{
    ownedCharacters.Clear();
    // Logic to load characters or initialize
}
```

This means `GetCharacterData()` will always return null because `ownedCharacters` is never populated.

**Investigate:** How does the carousel currently get character data? Likely through `CharacterDatabaseCSV` directly, bypassing `CharacterManager.ownedCharacters`. 

**Claude Code should check:**
1. Does `CarouselController` read from `CharacterManager` or `CharacterDatabaseCSV`?
2. Is there initialization code elsewhere that populates `ownedCharacters`?
3. If `ownedCharacters` is truly empty, `LoadRoster()` needs to be implemented — likely by reading from `CharacterDatabaseCSV` and creating `PlayerCharacterData` instances for each character.

**Suggested LoadRoster implementation:**

```csharp
private void LoadRoster()
{
    ownedCharacters.Clear();
    
    // Try CSV database first (preferred)
    var csvDb = CharacterDatabaseCSV.Instance;
    if (csvDb != null)
    {
        var allChars = csvDb.GetAllCharacters(); // adjust method name
        foreach (var charTemplate in allChars)
        {
            var playerData = new PlayerCharacterData(charTemplate.characterId);
            playerData.currentLevel = 1;
            // Set base stats from template
            playerData.currentStrength = charTemplate.baseStrength;
            playerData.currentClubControl = charTemplate.baseClubControl;
            playerData.currentRecovery = charTemplate.baseRecovery;
            playerData.currentStamina = charTemplate.baseStamina;
            ownedCharacters[charTemplate.characterId] = playerData;
        }
        
        // Select first character by default
        if (ownedCharacters.Count > 0)
        {
            var firstId = ownedCharacters.Keys.First();
            SelectCharacter(firstId);
        }
        
        Debug.Log($"[CharacterManager] Loaded {ownedCharacters.Count} characters from CSV");
    }
    
    OnRosterChanged?.Invoke();
}
```

**This is critical** — without this, the detail panel has no data to display.

---

## CORRECTION 6: SelectCharacter Method Missing

`CharacterManager` fires `OnCharacterSelected` event but has no `SelectCharacter()` method.

**Add to CharacterManager.cs:**

```csharp
/// <summary>
/// Set a character as the active/selected character for gameplay.
/// </summary>
public void SelectCharacter(string characterId)
{
    if (!ownedCharacters.ContainsKey(characterId))
    {
        Debug.LogWarning($"[CharacterManager] Cannot select {characterId} — not owned");
        return;
    }
    
    // Deselect previous
    if (!string.IsNullOrEmpty(selectedCharacterId) && ownedCharacters.ContainsKey(selectedCharacterId))
    {
        ownedCharacters[selectedCharacterId].isSelected = false;
    }
    
    // Select new
    selectedCharacterId = characterId;
    ownedCharacters[characterId].isSelected = true;
    
    OnCharacterSelected?.Invoke(characterId);
    Debug.Log($"[CharacterManager] Selected character: {characterId}");
}

/// <summary>
/// Get the currently selected character ID.
/// </summary>
public string GetSelectedCharacterId()
{
    return selectedCharacterId;
}
```

---

## Summary: Files to Modify (Updated)

| File | Action |
|------|--------|
| `CharacterDetailPanel.cs` | **Full rewrite** per spec, using corrections above |
| `CharacterManager.cs` | **Add** `GetCharacterTemplate()`, `SelectCharacter()`, implement `LoadRoster()` |
| `CharacterDatabase.cs` / `CharacterData` | **Add** `characterLastName` field |
| `PlayerCharacterData.cs` | **Add** stamina energy fields + `IsStaminaLow()` |
| `Characters.csv` | **Add** `lastName`, `bio` columns |
| `CharacterDatabaseCSV.cs` | **Update** CSV parser for new columns, ensure CharacterManager can read from it |
| Unity Inspector | **Wire** serialized fields to existing hierarchy objects |
