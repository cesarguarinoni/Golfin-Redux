# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.

---

## Current Task (2026-03-23) — 5 Targeted Fixes Only

The user has done extensive manual fixes. Only these 5 issues remain. Do NOT touch anything else — no font changes, no padding changes, no layout changes beyond what's listed here.

---

### Fix 1: Filter Bar Dividers — Use LayoutElement.ignoreLayout

The HorizontalLayoutGroup keeps auto-sizing the divider images and breaking them. The fix is to make dividers IGNORE the layout:

```csharp
// For each divider Image in the filter bar:
var layoutElement = divider.GetComponent<LayoutElement>();
if (layoutElement == null) layoutElement = divider.AddComponent<LayoutElement>();
layoutElement.ignoreLayout = true;
```

Then position each divider manually using RectTransform anchors between the filter buttons. Since there are 8 filter buttons evenly distributed, each divider sits at 1/8, 2/8, 3/8... of the way across:

```csharp
// Position divider N (0-indexed) between button N and N+1
// With 8 buttons, dividers go at positions 1/8, 2/8, 3/8, 4/8, 5/8, 6/8, 7/8
var rt = divider.GetComponent<RectTransform>();
float xPos = (float)(i + 1) / 8f; // normalized position
rt.anchorMin = new Vector2(xPos, 0.15f);
rt.anchorMax = new Vector2(xPos, 0.85f);
rt.sizeDelta = new Vector2(1, 0); // 1px wide, height from anchors
rt.anchoredPosition = Vector2.zero;
```

This way the dividers are positioned absolutely within the filter bar, ignoring the HLG entirely. Set divider Image color to `rgba(255, 255, 255, 0.3)`.

Do this in `ClubFilterBar.Start()` or in the builder. If divider GameObjects already exist in the hierarchy, just add `LayoutElement.ignoreLayout = true` and set their anchors. If they don't exist, create them programmatically.

---

### Fix 2: Carousel Arrow Images Missing

The user deleted the text fields and fixed the Image components on the arrow buttons, but the arrow sprites are now gone. 

1. Find what arrow sprites the **Roster** carousel uses:
```powershell
# Search for arrow-related sprite references in CarouselController or the scene
Get-Content -Path "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 500 | Select-String "arrow|Arrow"
```

2. Or check the Roster carousel hierarchy in the scene — the LeftArrow and RightArrow buttons should have Image components with sprites assigned. Find those sprite assets.

3. Look in the project for arrow sprites:
```powershell
Get-ChildItem -Path "C:\Users\cesar\GolfinRedux\Assets" -Recurse -Filter "*arrow*" | Select-Object FullName
Get-ChildItem -Path "C:\Users\cesar\GolfinRedux\Assets" -Recurse -Filter "*Arrow*" | Select-Object FullName
Get-ChildItem -Path "C:\Users\cesar\GolfinRedux\Assets" -Recurse -Filter "*chevron*" | Select-Object FullName
```

4. Once found, assign the same sprites to the Club Inventory carousel's LeftArrow and RightArrow Image components. The left arrow should be rotated 180° (or use a left-facing variant if one exists).

If no arrow sprites exist at all in the project, create simple triangle arrow sprites programmatically or log a warning. But they SHOULD exist since the Roster carousel has working arrows.

---

### Fix 3: Club Carousel Card Sizes and Viewport

The ClubsMainSection and Viewport don't match the club card sizes/positions. The cards are showing but the scroll area is wrong.

Read the Roster carousel's ScrollView setup and replicate it exactly for the Club carousel:
1. Check the Roster's `ScrollView` RectTransform (anchors, sizeDelta, pivot)
2. Check the Roster's `Viewport` RectTransform and Mask settings
3. Check the Roster's `Content` RectTransform, HorizontalLayoutGroup settings, and ContentSizeFitter
4. Check `CarouselController.cardsPerPage` and card prefab size

Then compare against the Club carousel's equivalent objects and fix any mismatches. The card prefab size should be the same between Roster and Clubs (both use the same base prefab structure with rarity backgrounds).

Key things to check:
- Viewport should NOT have an Image component that blocks visibility (user already removed one earlier)
- Content's HorizontalLayoutGroup spacing should match Roster
- Card RectTransform size should match the prefab's intended size
- ScrollView should have horizontal scroll enabled, vertical disabled

---

### Fix 4: Fade Overlay Must Be Active at Runtime

The user reports that if the Fade Overlay isn't manually turned on before runtime, the Clubs Inventory screen doesn't appear.

This is likely a `FadeController` issue. Find where `FadeController` or `ScreenManager` expects the fade overlay to be in a specific state at startup.

Check `FadeController.cs`:
- Does it assume the overlay starts active (opaque) and fades out?
- Or does it start inactive and fades in?

If the overlay must be active at start, add this to `RuntimeActiveStateManager` (Fix 8 from previous TellCode — create this script if not done yet). OR add it directly to `FadeController.Awake()`:

```csharp
private void Awake()
{
    // Ensure fade overlay starts active
    if (fadeOverlay != null && !fadeOverlay.activeSelf)
        fadeOverlay.SetActive(true);
}
```

Alternatively, this might be a ScreenManager.ApplyScreen() issue — the Inventory screen might not be getting activated properly if the fade sequence doesn't complete. Check the screen transition flow for Inventory.

---

### Fix 5: Carousel Portraits — Show Only Current Level

Club thumbnail cards currently show "Lv 10/39" but should show only "Lv 10" (no max level on the portrait card).

In `ClubThumbnailCard.Initialize()`, find where levelText is set and change:

```csharp
// CURRENT (wrong):
levelText.text = $"Lv {playerClub.currentLevel}/{template.maxLevel}";

// FIX:
levelText.text = $"Lv {playerClub.currentLevel}";
```

The max level is shown in the detail panel (Lv 10/39), not on the carousel card.

**Also check `CharacterThumbnailCard.cs`** — if it also shows max level on the portrait card, fix it the same way. Looking at the Roster screenshot, it shows "Lv 10/39" on cards too. The Figma reference for Roster shows only "Lv 10" on cards. Fix both if needed.

---

### Reminders
- Do NOT change any font sizes, paddings, or layout settings beyond what's listed above
- Platform: Windows (PowerShell, no bash/chmod/sed)
- Use `== null` not `??` for Unity objects
- Verify `using` directives before committing (Rule 0 in CLAUDE.md)
- Push to GitHub after completing

---

## Completed Tasks

✅ DONE: 2026-03-20 — ScreenshotTool, compress script, CLAUDE.md update, root cleanup
✅ DONE: 2026-03-20 — Phase C code: ClubCarouselController, ClubDetailPanel, builders, auto-wire
✅ DONE: 2026-03-21 — New leveling economy: rarity-based starting/max levels
✅ DONE: 2026-03-23 — TextGradients utility, RuntimeActiveStateManager, portrait 2-line names, screenshot auto-compress, EQUIP spacer (partial — some rolled back by user)
