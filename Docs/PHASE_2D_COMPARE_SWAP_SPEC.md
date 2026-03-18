# Phase 2d: Character Compare & Swap — Implementation Spec

**Author:** Claude (Architect)  
**Date:** 2026-03-18  
**For:** Claude Code implementation  
**Prerequisites:** Phase 2c complete (Level Up Modal working)  
**Visual References:** Character_Compare_Empty.png, Character_Compare.png, Character_Swap.png

---

## Overview

Compare mode lets the player view two characters side by side to compare stats before deciding who to select for gameplay. It transforms the detail panel from a single-character view (portrait + info) into a dual-column info view (no portraits).

The player enters compare mode from the detail panel, selects a second character from the carousel, and can then SWAP their selected character or CLOSE COMPARE to return to normal view.

---

## 1. State Machine

```
NORMAL MODE (default detail panel)
  → Full-body portrait (left) + info panel (right)
  → COMPARE button visible
  → SELECT/SELECTED button at bottom

PLAYER TAPS "COMPARE"
  → ENTER COMPARE MODE:
    1. Full-body portrait fades out
    2. Right info panel slides left to take left ~50%
    3. Right ~50% fades in with placeholder text: "TAP ON ANY OTHER CHARACTER TO COMPARE STATS"
    4. Bottom buttons change: CLOSE COMPARE + SWAP (on left column)
    5. Carousel stays interactive

PLAYER TAPS CAROUSEL CARD (while in compare mode)
  → If tapped the SAME character as left column:
    → EXIT COMPARE MODE (same as CLOSE COMPARE)
  → If tapped a DIFFERENT character:
    1. Placeholder text fades out
    2. Second character's info fades in on the right column
    3. Right column shows: name, rarity/level, 4 stats, LEVEL UP, BOOST, BIO
    4. Right column bottom: COMPARE button + SELECTED/SWAP button

PLAYER TAPS "CLOSE COMPARE"
  → EXIT COMPARE MODE:
    1. Right column fades out
    2. Left column slides back to right position
    3. Full-body portrait fades in on the left
    4. Back to normal detail panel view
    5. Buttons revert to COMPARE + SELECT/SELECTED

PLAYER TAPS "SWAP"
  → SWAP SELECTED CHARACTER:
    1. Call CharacterManager.SelectCharacter(newCharacterId)
    2. Right column (old character stats) fades out
    3. Full-body portrait of newly selected character fades in on left
    4. Left column shows newly selected character in normal detail view
    5. Exits compare mode entirely — back to normal view with new selection

PLAYER TAPS "LEVEL UP" (on either column while in compare mode)
  → Open Level Up Modal for THAT character
  → Modal should be centered over the column of the character being leveled up
  → When modal closes, refresh both columns
```

---

## 2. Layout Changes

### Normal Mode (existing — no changes)
```
DetailPanel
├── LeftPanel (full-body portrait)        ~45% width
└── RightPanel (info: name, stats, etc.)  ~55% width
```

### Compare Mode
```
DetailPanel
├── LeftColumn (info panel for character A)   ~50% width
│   ├── CharacterNameText
│   ├── RarityLevelRow
│   ├── 4 Stat Rows
│   ├── LEVEL UP + BOOST buttons
│   ├── BIO
│   ├── CLOSE COMPARE button
│   └── SELECTED or SWAP button
│
├── Divider (vertical line, 1px)
│
└── RightColumn                               ~50% width
    ├── (empty state) PlaceholderText: "TAP ON ANY OTHER CHARACTER TO COMPARE STATS"
    │
    └── (filled state) CompareRightPanel
        ├── CharacterNameText
        ├── StatusIcons (eye, bolt)
        ├── RarityLevelRow
        ├── 4 Stat Rows
        ├── LEVEL UP + BOOST buttons
        ├── BIO
        ├── COMPARE button
        └── SELECTED or SWAP button
```

### Implementation Approach

**Option A (Recommended): Reuse the existing RightPanel structure.**

Instead of building a completely new set of UI elements for compare mode:

1. The existing `RightPanel` becomes the **left column** in compare mode (it slides left)
2. Create a **CompareRightPanel** — a duplicate of RightPanel's structure — that sits hidden and activates in compare mode
3. A new `CompareController.cs` manages the mode switching, animations, and data binding for the right panel

This avoids duplicating all the data binding logic in CharacterDetailPanel.

**Option B: Build a separate ComparePanel overlay.** More isolated but more code duplication.

Go with Option A.

---

## 3. New/Modified GameObjects

### Add to DetailPanel hierarchy:

```
DetailPanel
├── LeftPanel (existing — portrait, hide in compare mode)
├── RightPanel (existing — slides left in compare mode)
│   └── ... existing children ...
│   └── CloseCompareButton (NEW — hidden in normal mode, shown in compare mode)
│   └── SwapButton (NEW — replaces CompareButton in compare mode)
│
├── CompareRightPanel (NEW — hidden by default)
│   ├── ComparePlaceholder (TMP — "TAP ON ANY OTHER CHARACTER TO COMPARE STATS")
│   └── CompareInfoPanel (hidden until second character selected)
│       ├── CompareNameText
│       ├── CompareStatusIcons
│       ├── CompareRarityLabel + CompareLevelText + CompareMaxLevelText
│       ├── CompareStrength (icon + name + bar + value)
│       ├── CompareClubControl (same structure)
│       ├── CompareRecovery (same structure)
│       ├── CompareStamina (same structure)
│       ├── CompareLevelUpButton + CompareBoostButton
│       ├── CompareBioHeader + CompareBioText
│       ├── CompareCompareButton (COMPARE button on right side)
│       └── CompareSelectButton (SELECTED or SWAP)
│
└── VerticalDivider (NEW — 1px line, hidden in normal mode)
```

---

## 4. CompareController.cs

New script attached to DetailPanel (or a child). Manages compare mode state and animations.

### Serialized Fields

```csharp
[Header("Normal Mode References")]
[SerializeField] private GameObject leftPanel;           // LeftPanel (portrait)
[SerializeField] private RectTransform rightPanel;       // RightPanel (info)

[Header("Compare Mode References")]
[SerializeField] private GameObject compareRightPanel;   // CompareRightPanel
[SerializeField] private GameObject comparePlaceholder;  // "TAP ON ANY OTHER..." text
[SerializeField] private GameObject compareInfoPanel;    // actual compare data
[SerializeField] private GameObject verticalDivider;     // divider line

[Header("Left Column Button Swap")]
[SerializeField] private Button compareButton;           // existing COMPARE button (normal mode)
[SerializeField] private Button closeCompareButton;      // CLOSE COMPARE (compare mode)
[SerializeField] private Button selectButton;            // existing SELECT/SELECTED
[SerializeField] private Button swapButton;              // SWAP button (compare mode, left column)

[Header("Right Column Info — mirror of detail panel fields")]
[SerializeField] private TextMeshProUGUI compareNameText;
[SerializeField] private TextMeshProUGUI compareRarityLabel;
[SerializeField] private TextMeshProUGUI compareLevelText;
[SerializeField] private TextMeshProUGUI compareMaxLevelText;
[SerializeField] private GameObject compareStrengthRow;
[SerializeField] private GameObject compareClubControlRow;
[SerializeField] private GameObject compareRecoveryRow;
[SerializeField] private GameObject compareStaminaRow;
[SerializeField] private Button compareLevelUpButton;
[SerializeField] private Button compareBoostButton;
[SerializeField] private TextMeshProUGUI compareBioText;
[SerializeField] private Button compareRightCompareButton;  // COMPARE button on right
[SerializeField] private Button compareRightSelectButton;   // SELECTED/SWAP on right
[SerializeField] private TextMeshProUGUI compareRightSelectButtonText;

[Header("Status Icons (Right Column)")]
[SerializeField] private GameObject compareSelectedIcon;
[SerializeField] private GameObject compareLowStaminaIcon;

[Header("Animation")]
[SerializeField] private float slideDuration = 0.3f;
[SerializeField] private float fadeDuration = 0.2f;

[Header("Level Up Modal")]
[SerializeField] private LevelUpModalController levelUpModal;
```

### Core State

```csharp
private bool isCompareMode = false;
private string leftCharacterId;    // character shown on left (entered compare from)
private string rightCharacterId;   // character shown on right (null if empty state)

// Cache the normal-mode RightPanel position for slide animation
private Vector2 normalRightPanelPosition;
private Vector2 compareLeftPosition;     // where RightPanel slides to in compare mode
```

### EnterCompareMode(string characterId)

Called when COMPARE is tapped on the detail panel:

```csharp
public void EnterCompareMode(string characterId)
{
    if (isCompareMode) return;
    
    isCompareMode = true;
    leftCharacterId = characterId;
    rightCharacterId = null;
    
    // Cache normal position
    normalRightPanelPosition = rightPanel.anchoredPosition;
    
    // 1. Fade out portrait
    StartCoroutine(FadeOut(leftPanel, fadeDuration));
    
    // 2. Slide RightPanel to left half
    StartCoroutine(SlidePanel(rightPanel, compareLeftPosition, slideDuration));
    
    // 3. Show compare right panel with placeholder
    compareRightPanel.SetActive(true);
    comparePlaceholder.SetActive(true);
    compareInfoPanel.SetActive(false);
    StartCoroutine(FadeIn(compareRightPanel, fadeDuration, slideDuration)); // delay until slide done
    
    // 4. Show vertical divider
    verticalDivider.SetActive(true);
    
    // 5. Swap buttons: hide COMPARE + SELECT, show CLOSE COMPARE + SWAP
    compareButton.gameObject.SetActive(false);
    closeCompareButton.gameObject.SetActive(true);
    
    UpdateLeftColumnButtons();
    
    Debug.Log($"[CompareController] Entered compare mode with {characterId}");
}
```

### OnCarouselSelectionInCompareMode(string characterId)

Called when a carousel card is tapped while in compare mode. Wire this up via CarouselController.OnCharacterSelected:

```csharp
public void OnCarouselSelectionInCompareMode(string characterId)
{
    if (!isCompareMode) return;
    
    // Tapped the same character as left column → exit compare mode
    if (characterId == leftCharacterId)
    {
        ExitCompareMode();
        return;
    }
    
    // Show second character on the right
    rightCharacterId = characterId;
    
    // Fade out placeholder, fade in info
    StartCoroutine(FadeOut(comparePlaceholder, fadeDuration));
    compareInfoPanel.SetActive(true);
    StartCoroutine(FadeIn(compareInfoPanel, fadeDuration, fadeDuration)); // delay after placeholder fades
    
    // Populate right column with character data
    RefreshRightColumn(characterId);
    
    Debug.Log($"[CompareController] Comparing {leftCharacterId} vs {characterId}");
}
```

### RefreshRightColumn(string characterId)

Populates the right column — same logic as CharacterDetailPanel.UpdatePanel but targeting the compare fields:

```csharp
private void RefreshRightColumn(string characterId)
{
    var playerData = CharacterManager.Instance.GetCharacterData(characterId);
    var csvChar = CharacterDatabaseCSV.Instance?.GetCharacter(characterId);
    if (playerData == null) return;
    
    // Name
    compareNameText.text = csvChar != null ? csvChar.GetDisplayName() : characterId.ToUpper();
    
    // Rarity + Level
    var rarity = csvChar?.rarity ?? CharacterRarity.Common;
    compareRarityLabel.text = rarity.ToString().ToUpper();
    compareRarityLabel.color = RarityHelper.GetRarityColor(rarity);
    compareLevelText.text = $"Lv {playerData.currentLevel}";
    compareMaxLevelText.text = $"/{CharacterManager.Instance.GetMaxLevel(characterId)}";
    
    // Stats
    var caps = RarityStatCaps.GetStatCaps(rarity);
    int baseStr = csvChar?.baseStrength ?? 0;
    int baseCc = csvChar?.baseClubControl ?? 0;
    int baseRec = csvChar?.baseRecovery ?? 0;
    int baseStam = csvChar?.baseStamina ?? 0;
    
    UpdateCompareStatRow(compareStrengthRow, baseStr + playerData.spentStrength, caps.strengthCap);
    UpdateCompareStatRow(compareClubControlRow, baseCc + playerData.spentClubControl, caps.clubControlCap);
    UpdateCompareStatRow(compareRecoveryRow, baseRec + playerData.spentRecovery, caps.recoveryCap);
    UpdateCompareStatRow(compareStaminaRow, baseStam + playerData.spentStamina, caps.staminaCap,
        forceRed: playerData.IsStaminaLow());
    
    // Bio
    compareBioText.text = csvChar?.bio ?? "";
    
    // Status icons
    if (compareSelectedIcon != null)
        compareSelectedIcon.SetActive(playerData.isSelected);
    if (compareLowStaminaIcon != null)
        compareLowStaminaIcon.SetActive(playerData.IsStaminaLow());
    
    // Right column buttons
    UpdateRightColumnButtons(characterId, playerData.isSelected);
}

private void UpdateCompareStatRow(GameObject statRow, int current, int cap, bool forceRed = false)
{
    var bar = statRow.transform.Find("Name+Bar/Bar")?.GetComponent<Image>();
    var numberText = statRow.transform.Find("StatNumber")?.GetComponent<TextMeshProUGUI>();
    // NOTE: Adjust paths to match actual hierarchy in compare panel
    
    if (bar != null)
    {
        bar.fillAmount = cap > 0 ? (float)current / cap : 0f;
        if (forceRed)
            bar.color = new Color(1f, 0.3f, 0.2f, 1f);
        else if (current >= cap)
            bar.color = new Color(0.2f, 1f, 0.4f, 1f);
        else
            bar.color = new Color(0.2f, 0.6f, 1f, 1f);
    }
    if (numberText != null)
        numberText.text = $"{current}/{cap}";
}
```

### UpdateLeftColumnButtons / UpdateRightColumnButtons

```csharp
private void UpdateLeftColumnButtons()
{
    var playerData = CharacterManager.Instance.GetCharacterData(leftCharacterId);
    bool isSelected = playerData?.isSelected ?? false;
    
    if (isSelected)
    {
        // Left character is selected → show SELECTED (gold), SWAP not needed
        selectButton.gameObject.SetActive(true);
        swapButton.gameObject.SetActive(false);
        // selectButton shows "SELECTED"
    }
    else
    {
        // Left character is NOT selected → show SWAP
        selectButton.gameObject.SetActive(false);
        swapButton.gameObject.SetActive(true);
    }
}

private void UpdateRightColumnButtons(string characterId, bool isSelected)
{
    if (isSelected)
    {
        compareRightSelectButtonText.text = "SELECTED";
        // Gold color, not interactable
        compareRightSelectButton.interactable = false;
    }
    else
    {
        compareRightSelectButtonText.text = "SWAP";
        compareRightSelectButton.interactable = true;
    }
}
```

### ExitCompareMode()

```csharp
public void ExitCompareMode()
{
    if (!isCompareMode) return;
    
    isCompareMode = false;
    rightCharacterId = null;
    
    // 1. Fade out right column
    StartCoroutine(FadeOut(compareRightPanel, fadeDuration));
    verticalDivider.SetActive(false);
    
    // 2. Slide RightPanel back to normal position
    StartCoroutine(SlidePanel(rightPanel, normalRightPanelPosition, slideDuration));
    
    // 3. Fade in portrait
    leftPanel.SetActive(true);
    StartCoroutine(FadeIn(leftPanel, fadeDuration, slideDuration)); // delay until slide done
    
    // 4. Restore buttons
    compareButton.gameObject.SetActive(true);
    closeCompareButton.gameObject.SetActive(false);
    swapButton.gameObject.SetActive(false);
    selectButton.gameObject.SetActive(true);
    
    Debug.Log("[CompareController] Exited compare mode");
}
```

### OnSwapClicked()

```csharp
private void OnSwapClicked()
{
    // Determine which character to swap TO (the non-selected one)
    string swapToId = null;
    
    var leftData = CharacterManager.Instance.GetCharacterData(leftCharacterId);
    var rightData = rightCharacterId != null ? CharacterManager.Instance.GetCharacterData(rightCharacterId) : null;
    
    if (leftData != null && !leftData.isSelected)
        swapToId = leftCharacterId;
    else if (rightData != null && !rightData.isSelected)
        swapToId = rightCharacterId;
    
    if (swapToId == null) return;
    
    // 1. Select the new character
    CharacterManager.Instance.SelectCharacter(swapToId);
    
    // 2. Exit compare mode — this slides back to normal view
    //    The detail panel will show the newly selected character
    isCompareMode = false;
    rightCharacterId = null;
    
    // 3. Fade out right column
    StartCoroutine(FadeOut(compareRightPanel, fadeDuration));
    verticalDivider.SetActive(false);
    
    // 4. Slide RightPanel back and update it with swapped character
    StartCoroutine(SlidePanel(rightPanel, normalRightPanelPosition, slideDuration));
    
    // 5. Fade in portrait of newly selected character
    leftPanel.SetActive(true);
    StartCoroutine(FadeIn(leftPanel, fadeDuration, slideDuration));
    
    // 6. Restore buttons
    compareButton.gameObject.SetActive(true);
    closeCompareButton.gameObject.SetActive(false);
    swapButton.gameObject.SetActive(false);
    selectButton.gameObject.SetActive(true);
    
    // 7. Update detail panel to show new character
    // This should happen via CharacterManager.OnCharacterSelected event
    
    Debug.Log($"[CompareController] Swapped to {swapToId}");
}
```

### OnLevelUpInCompareMode(string characterId, bool isLeftColumn)

```csharp
private void OnLevelUpInCompareMode(string characterId, bool isLeftColumn)
{
    if (levelUpModal == null) return;
    
    // Position modal over the relevant column
    // Left column: center modal over left half of screen
    // Right column: center modal over right half of screen
    // NOTE: Implementer should adjust the modal's RectTransform pivot/position
    // based on which column triggered it
    
    levelUpModal.Open(characterId);
    
    // When modal closes, refresh both columns
    // Could subscribe to modal's OnClose event or check in Update
}
```

---

## 5. Animation Helpers

```csharp
private IEnumerator FadeOut(GameObject obj, float duration)
{
    var canvasGroup = obj.GetComponent<CanvasGroup>();
    if (canvasGroup == null) canvasGroup = obj.AddComponent<CanvasGroup>();
    
    float elapsed = 0f;
    canvasGroup.alpha = 1f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        canvasGroup.alpha = 1f - (elapsed / duration);
        yield return null;
    }
    canvasGroup.alpha = 0f;
    obj.SetActive(false);
}

private IEnumerator FadeIn(GameObject obj, float duration, float delay = 0f)
{
    if (delay > 0) yield return new WaitForSeconds(delay);
    
    obj.SetActive(true);
    var canvasGroup = obj.GetComponent<CanvasGroup>();
    if (canvasGroup == null) canvasGroup = obj.AddComponent<CanvasGroup>();
    
    canvasGroup.alpha = 0f;
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        canvasGroup.alpha = elapsed / duration;
        yield return null;
    }
    canvasGroup.alpha = 1f;
}

private IEnumerator SlidePanel(RectTransform panel, Vector2 targetPos, float duration)
{
    Vector2 startPos = panel.anchoredPosition;
    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
        panel.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
        yield return null;
    }
    panel.anchoredPosition = targetPos;
}
```

---

## 6. Integration with Existing Code

### CharacterDetailPanel.cs changes:

```csharp
[SerializeField] private CompareController compareController;

private void OnCompareClicked()
{
    if (compareController != null && !string.IsNullOrEmpty(currentCharacterId))
    {
        compareController.EnterCompareMode(currentCharacterId);
    }
}
```

### CarouselController integration:

CompareController needs to intercept carousel selection when in compare mode:

```csharp
// In CompareController.OnEnable:
CarouselController.OnCharacterSelected += OnCarouselSelection;

// In CompareController.OnDisable:
CarouselController.OnCharacterSelected -= OnCarouselSelection;

private void OnCarouselSelection(string characterId)
{
    if (isCompareMode)
    {
        OnCarouselSelectionInCompareMode(characterId);
    }
    // Normal mode selection is handled by CharacterDetailPanel as before
}
```

**Important:** CompareController should subscribe BEFORE CharacterDetailPanel so it can intercept carousel events in compare mode. Or use a flag that CharacterDetailPanel checks:

```csharp
// In CharacterDetailPanel.UpdatePanel:
if (compareController != null && compareController.IsCompareMode)
    return; // Let CompareController handle it
```

---

## 7. Implementation Order

1. **Create CompareRightPanel hierarchy** — duplicate RightPanel structure, place as sibling
2. **Create CompareController.cs** — basic state management, no animations yet
3. **Wire COMPARE button** → EnterCompareMode
4. **Implement mode switching** — hide portrait, show two columns (no animation first)
5. **Implement right column data binding** — RefreshRightColumn
6. **Implement carousel interception** — tap card in compare mode fills right column
7. **Implement CLOSE COMPARE** — return to normal view
8. **Implement SWAP** — select new character, exit compare
9. **Add animations** — fade, slide
10. **Wire LEVEL UP** in compare mode → open modal over correct column

---

## 8. Files to Create/Modify

| File | Action |
|------|--------|
| `CompareController.cs` | **CREATE** — new script in `Assets/Scripts/UI/Roster/UI/` |
| `CharacterDetailPanel.cs` | **MODIFY** — add compareController reference, update OnCompareClicked |
| `CarouselController.cs` | **VERIFY** — OnCharacterSelected event accessible for CompareController |
| Unity hierarchy | **BUILD** — CompareRightPanel, VerticalDivider, CloseCompareButton, SwapButton |
| Unity Inspector | **WIRE** — all serialized fields |

---

## 9. What NOT to Build

- Stat highlighting (green/red when one character's stat is higher/lower)
- Gear comparison
- Compare from a different screen (only from Roster)
- Compare more than 2 characters
- Animation polish beyond basic fade/slide (particle effects, etc.)

---

## 10. Testing Checklist

- [ ] COMPARE button on detail panel enters compare mode
- [ ] Portrait fades out, info panel slides left
- [ ] Placeholder text shows on right: "TAP ON ANY OTHER CHARACTER TO COMPARE STATS"
- [ ] Tapping a carousel card fills the right column with that character's data
- [ ] All 4 stat bars show correctly on both columns
- [ ] Bio text shows for both characters
- [ ] Status icons show correctly (selected, low stamina)
- [ ] CLOSE COMPARE returns to normal detail view with animations
- [ ] Tapping the same character as left column exits compare mode
- [ ] SWAP selects the non-selected character and exits compare mode
- [ ] After SWAP, detail panel shows newly selected character
- [ ] LEVEL UP on either column opens the modal
- [ ] After modal closes, both columns refresh
- [ ] SELECTED button appears on the currently selected character
- [ ] SWAP button appears on the non-selected character
- [ ] RP display updates if level up happens during compare
- [ ] No errors when rapidly switching between normal and compare mode
