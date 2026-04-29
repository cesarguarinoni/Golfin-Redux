# CESAR_REJECTION — capture_helper

## Date
2026-04-29

## What failed (3 items)

### 1. Screenshot captures wrong area
- **From Scene view:** captures only the bottom-left corner of the Scene — NOT the Game View.
- **From Game view:** completely black.
- **Root cause:** `ScreenCapture.CaptureScreenshotAsTexture()` reads from the OS display swap chain (screen). In the Unity Editor, the Game View renders to an internal `RenderTexture`, NOT to the swap chain. So reading the swap chain gives you Editor chrome or whatever is on the actual display at that pixel location — not the game content.
- **Required fix:** Read the GameView's internal RenderTexture directly via reflection (field name `m_RenderTexture` on the `UnityEditor.GameView` EditorWindow type). Fall back to `ScreenCapture.CaptureScreenshotAsTexture()` only if reflection fails and document the limitation.

### 2. Fake state does not update the club handle image
- **Symptom:** After `FakeMidAim`, the club action button / handle shows no change.
- **Investigation needed:** 
  - Verify `ClubButtonWidget` is present and enabled in the LabScaffold scene.
  - `ClubButtonWidget._iconImage` shows `ClubContext.SelectedPortrait ?? _defaultPortrait`. Portrait is null in fake state. Check if `_defaultPortrait` is wired in inspector — if not, the image field is empty.
  - Check if there are club icon sprites available (look in `Assets/Art/` or `Assets/Resources/`) to inject a non-null sprite.
  - If portrait sprites don't exist yet, at minimum inject something into `EquippedBag` with a TypeLabel so the text fields update — the handle "not moving at all" suggests OnSelectedChanged may not be firing or the widget is not enabled.
  - Take a screenshot BEFORE and AFTER FakeMidAim to show the diff.

### 3. Wrong portrait path
- **Used:** `Resources.Load<Sprite>("Portraits/Thumbnails/Camila")` — this loads the Roster/Rankings thumbnails.
- **Required:** `Resources.Load<Sprite>("Portraits/InGame/Camila")` — these are the in-game HUD portraits at `Assets/Resources/Portraits/InGame/`.
- Fix in both `FakeMidAim` (Camila) and `FakePutt` (Olivia).

## Implementer instructions
Fix all 3 issues and take 5 screenshot attempts showing the result. The screenshots must show the GameView content (player card, hole card, HUD widgets) — not black and not Editor chrome.
