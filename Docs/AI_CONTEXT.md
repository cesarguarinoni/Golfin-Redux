# AI Context — Golfin Redux

## Current Phase: Phase 2b — Roster Detail Panel

### Status
- [x] Folder renamed from "Golfin Redux" to "GolfinRedux"
- [x] Null reference guards added to CarouselController, RosterScreenController
- [x] Carousel cards correct size (ContentSizeFitter + LayoutElement per card)
- [x] All 12 characters load in carousel (CSV-first pattern in CharacterThumbnailCard)
- [x] Card scale bounce animation (5% bigger, elastic ease-out)
- [x] Viewport expanded to prevent clipping of scaled cards
- [x] Full-body portrait loading: portraitFull CSV column + CharacterDatabaseCSV parsing + CharacterDetailPanel priority
- [x] Rarity badge background removed (Image.enabled = false) ✅ CONFIRMED FIXED
- [x] Button disabled states scaffolded (Level Up, Boost)
- [x] Button click console logging added
- [x] CS0136 duplicate maxLevel compile error fixed
- [ ] SELECT button not fully working — see below
- [ ] Button gold color on SELECT not yet implemented

### SELECT Button Status
The logic flow is correct:
- `OnSelectClicked()` → `CharacterManager.Instance.SelectCharacter(id)` ✓
- `OnCharacterSelected` event → `OnSelectionChanged()` → `UpdatePanel()` ✓
- `UpdatePanel()` → `UpdateSelectButton(playerData.isSelected)` ✓

**Missing pieces in `UpdateSelectButton()`:**
1. `selectButton.interactable = !isSelected` — was reverted by accident
2. Gold color change on button when selected — never implemented
3. Level Up / Boost interactable state was also reverted

### Next Session: Fix SELECT Button
In `CharacterDetailPanel.cs`:

1. Restore `selectButton.interactable = !isSelected` in `UpdateSelectButton()`
2. Add gold color tint to button image when selected, restore default when not
3. Restore Level Up button disabled state logic in `UpdatePanel()`:
   ```
   bool atMax = playerData.currentLevel >= maxLevel;
   bool canAfford = RewardPointsManager.Instance != null
       && RewardPointsManager.Instance.CanAfford(CharacterManager.Instance.GetLevelUpCost(characterId));
   levelUpButton.interactable = !atMax && canAfford;
   boostButton.interactable = false; // until boost system exists
   ```

### Workflow Change: Push to GitHub After Every Change
User requested: always push to GitHub after each change.

### Key Paths
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — detail panel, needs SELECT fix
- `Assets/Scripts/UI/Roster/UI/CarouselController.cs` — carousel, working
- `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` — card, working
- `Assets/Data/Characters.csv` — has portraitFull column (Elizabeth, Shae, James populated)
- `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` — parses portraitFull, has fullBodyPortraits array

### Blockers
- None blocking. SELECT button fix is next.

### What's Next
1. Fix SELECT button (interactable + gold color + button states)
2. Push to GitHub
3. Then: Phase 2c — Level Up Modal
