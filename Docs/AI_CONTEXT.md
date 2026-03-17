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
- [x] SELECT button fully working — interactable, gold color, SELECTED text ✅
- [x] Level Up / Boost button interactable state in UpdatePanel ✅

### Workflow Change: Push to GitHub After Every Change
User requested: always push to GitHub after each change.

### Key Paths
- `Assets/Scripts/UI/Roster/UI/CharacterDetailPanel.cs` — detail panel, needs SELECT fix
- `Assets/Scripts/UI/Roster/UI/CarouselController.cs` — carousel, working
- `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` — card, working
- `Assets/Data/Characters.csv` — has portraitFull column (Elizabeth, Shae, James populated)
- `Assets/Scripts/UI/Roster/Managers/CharacterDatabaseCSV.cs` — parses portraitFull, has fullBodyPortraits array

### Blockers
- None.

### What's Next
1. Phase 2c — Level Up Modal
