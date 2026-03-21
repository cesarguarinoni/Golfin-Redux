# Localization Pass — Implementation Spec

**Author:** Claude (Architect)  
**Date:** 2026-03-19  
**For:** Claude Code implementation  
**Prerequisites:** Phase 2 complete, LocalizationManager working

---

## Overview

Wire all hardcoded UI text across the Roster system (and any other screens with hardcoded text) to the localization CSV. The system already works — `LocalizationManager.Get("KEY")` returns EN/JP text. Rich text tags like `<color=#EEDC9A>` are supported in CSV values.

**Existing localization CSV:** `Assets/Localization/LocalizationText.csv`  
**Format:** `key,English,Japanese`  
**API:** `LocalizationManager.Get("KEY")` — returns string for current language

---

## 1. Key Naming Convention

```
SCREEN_ELEMENT_DETAIL

Examples:
ROSTER_TITLE              → "ROSTER"
ROSTER_LEVEL_UP           → "LEVEL UP"
ROSTER_SELECT             → "SELECT"
ROSTER_SELECTED           → "SELECTED"
ROSTER_COMPARE            → "COMPARE"
MODAL_CONFIRM             → "CONFIRM"
MODAL_CANCEL              → "CANCEL"
```

---

## 2. New Localization Keys to Add

### Roster Screen (RosterScreenController)
| Key | English | Japanese |
|-----|---------|----------|
| ROSTER_TITLE | ROSTER | ロスター |

### Detail Panel (CharacterDetailPanel)
| Key | English | Japanese |
|-----|---------|----------|
| ROSTER_STRENGTH | STRENGTH | ストレングス |
| ROSTER_CLUB_CONTROL | CLUB CONTROL | クラブコントロール |
| ROSTER_RECOVERY | RECOVERY | リカバリー |
| ROSTER_STAMINA | STAMINA | スタミナ |
| ROSTER_LEVEL_UP | LEVEL UP | レベルアップ |
| ROSTER_BOOST | BOOST | ブースト |
| ROSTER_BIO | BIO | バイオ |
| ROSTER_COMPARE | COMPARE | 比較 |
| ROSTER_SELECT | SELECT | 選択 |
| ROSTER_SELECTED | SELECTED | 選択済み |
| ROSTER_SWAP | SWAP | 交換 |

### Level Up Modal (LevelUpModalController)
| Key | English | Japanese |
|-----|---------|----------|
| MODAL_NEXT_LEVEL | NEXT LEVEL | 次のレベル |
| MODAL_COST | COST | コスト |
| MODAL_REWARD | REWARD | 報酬 |
| MODAL_LEVEL_UP | LEVEL UP | レベルアップ |
| MODAL_AVAILABLE_SP | AVAILABLE SP | 利用可能SP |
| MODAL_RESET | RESET | リセット |
| MODAL_CANCEL | CANCEL | キャンセル |
| MODAL_CONFIRM | CONFIRM | 確認 |
| MODAL_SP_SUFFIX | SP | SP |

### Compare Mode (CompareController)
| Key | English | Japanese |
|-----|---------|----------|
| COMPARE_EMPTY_PROMPT | TAP ON ANY OTHER CHARACTER TO COMPARE STATS | 他のキャラクターをタップしてステータスを比較 |
| COMPARE_CLOSE | CLOSE COMPARE | 比較を閉じる |

### Character Bios
Character bios are already in Characters.csv. For localization, we have two options:

**Option A (Recommended for now):** Keep bios in Characters.csv as English-only. Add a `bioJa` column for Japanese translations later.

**Option B (Full localization):** Move bios to the localization CSV using keys like `CHAR_BIO_ELIZABETH`, `CHAR_BIO_SHAE`, etc. CharacterDetailPanel and CompareController would use `LocalizationManager.Get(bioKey)` instead of reading from CSV directly.

**Go with Option A for now.** Bios are long text that's character-specific — they fit better in the character data CSV than the UI localization CSV. We can migrate later if needed.

### Character Names
Character names (Elizabeth, Shae, etc.) are proper nouns and stay the same in both languages. No localization needed.

### Rarity Names
| Key | English | Japanese |
|-----|---------|----------|
| RARITY_COMMON | COMMON | コモン |
| RARITY_UNCOMMON | UNCOMMON | アンコモン |
| RARITY_RARE | RARE | レア |
| RARITY_MYTHIC | MYTHIC | ミシック |
| RARITY_LEGENDARY | LEGENDARY | レジェンダリー |
| RARITY_SUPREME | SUPREME | スプリーム |

---

## 3. Implementation Pattern

For each script with hardcoded text, replace the hardcoded string with `LocalizationManager.Get()`:

### Before:
```csharp
selectButtonText.text = isSelected ? "SELECTED" : "SELECT";
```

### After:
```csharp
selectButtonText.text = isSelected 
    ? LocalizationManager.Get("ROSTER_SELECTED") 
    : LocalizationManager.Get("ROSTER_SELECT");
```

### For stat names (set once during initialization or in UpdatePanel):
```csharp
strengthName.text = LocalizationManager.Get("ROSTER_STRENGTH");
clubControlName.text = LocalizationManager.Get("ROSTER_CLUB_CONTROL");
recoveryName.text = LocalizationManager.Get("ROSTER_RECOVERY");
staminaName.text = LocalizationManager.Get("ROSTER_STAMINA");
```

### For rarity labels:
```csharp
// Instead of: rarityLabel.text = rarity.ToString().ToUpper();
// Use:
rarityLabel.text = LocalizationManager.Get($"RARITY_{rarity.ToString().ToUpper()}");
```

---

## 4. Runtime Language Switching

Each script that displays localized text should subscribe to `LocalizationManager.OnLanguageChanged` and refresh its text. Add this pattern:

```csharp
private void OnEnable()
{
    LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
    // ... other subscriptions ...
}

private void OnDisable()
{
    LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
}

private void RefreshLocalizedText()
{
    // Re-set all localized strings
    // For detail panel: call UpdatePanel(currentCharacterId) again
    // For modal: call RefreshDisplay() again
    // For compare: refresh both columns
}
```

Most scripts already have a refresh method (UpdatePanel, RefreshDisplay, etc.) — just call it when the language changes.

---

## 5. Files to Modify

| File | Action |
|------|--------|
| `LocalizationText.csv` | **ADD** all new keys from Section 2 |
| `CharacterDetailPanel.cs` | **REPLACE** hardcoded strings with `LocalizationManager.Get()`, subscribe to `OnLanguageChanged` |
| `LevelUpModalController.cs` | **REPLACE** hardcoded strings, subscribe to `OnLanguageChanged` |
| `CompareController.cs` | **REPLACE** hardcoded strings, subscribe to `OnLanguageChanged` |
| `RosterScreenController.cs` | **REPLACE** title if hardcoded |
| `CharacterThumbnailCard.cs` | **CHECK** if any text is hardcoded (level format, rarity label) |
| `RarityHelper.cs` | **OPTIONAL** — add a `GetLocalizedRarityName()` method that uses localization keys |

---

## 6. Files NOT to Modify

- `LocalizationManager.cs` — already works, no changes needed
- `Characters.csv` — bios stay here (Option A)
- `ProTipCard.cs` — tips are already localized
- `HomeScreenController.cs` — already uses localization for most text
- `SettingsController*.cs` — already localized

---

## 7. Implementation Order

1. **Add all new keys to `LocalizationText.csv`** — copy the tables from Section 2
2. **CharacterDetailPanel.cs** — largest file, most hardcoded text
3. **LevelUpModalController.cs** — modal labels and buttons
4. **CompareController.cs** — compare-specific text
5. **RosterScreenController.cs** — title only
6. **CharacterThumbnailCard.cs** — verify and fix if needed
7. **Test language switching** — change language in Settings, verify all Roster text updates

---

## 8. Testing Checklist

- [ ] All stat names show localized text (STRENGTH etc. in EN, ストレングス etc. in JP)
- [ ] All button labels localized (SELECT, LEVEL UP, COMPARE, etc.)
- [ ] Rarity names localized (RARE → レア, LEGENDARY → レジェンダリー, etc.)
- [ ] Level Up Modal labels localized (COST, REWARD, NEXT LEVEL, etc.)
- [ ] Compare mode placeholder text localized
- [ ] CLOSE COMPARE and SWAP buttons localized
- [ ] Switching language in Settings immediately updates all visible Roster text
- [ ] Rich text tags still work after localization (tips with color tags)
- [ ] No missing keys (no raw key names showing as text)
- [ ] Japanese text doesn't overflow or break layouts (check all stat bars, buttons, modal)

---

## 9. Japanese Translation Notes

The Japanese translations above are functional placeholders. Some notes:
- Rarity names use katakana transliteration (コモン, レア) which is standard for gacha games in Japan
- Button text is kept short to fit UI constraints
- ROSTER → ロスター is direct transliteration; an alternative is キャラクター一覧 (character list) — decide based on preference
- BIO → バイオ is transliteration; alternative is 経歴 (background/career)

**If Ken has preferred Japanese terminology, update the CSV with his suggestions.**
