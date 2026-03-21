# Filter Bar Visibility — Debug Report

**Date:** 2026-03-19
**Feature:** Club Inventory Phase B — FilterBar inside ClubsContent
**Status:** ⚠️ Not resolved — filter buttons exist in hierarchy but do not render in Game View

---

## What Was Built

`InventoryScreenBuilder.cs` (`GOLFIN/Build Inventory Screen`) creates this hierarchy in the scene:

```
InventoryScreen  (SetActive false — ScreenManager activates it)
├── Header          (60px, TMP "INVENTORY")
├── TabBar          (50px, 4 tab buttons + underline indicators)
└── ContentArea     (fills remainder)
    ├── ClubsContent    (active, stretch-fill)
    │   ├── FilterBar   (48px, top of ClubsContent)  ← invisible
    │   └── ClubsMainSection  (fills rest — placeholder)
    ├── BagsContent     (inactive placeholder)
    ├── BallsContent    (inactive placeholder)
    └── ItemsContent    (inactive placeholder)
```

`InventoryScreenController` manages tabs; `ClubFilterBar` manages filter buttons.

---

## Attempt 1 — ScrollRect + Mask

**Approach:** FilterBar was a `ScrollRect` container with a `Viewport` (Mask) and a `Content` child with `HorizontalLayoutGroup + ContentSizeFitter`. Buttons lived inside Content.

**Why it failed:**
Unity's `Mask` component writes to the stencil buffer using the Viewport's `Image` component. The Viewport Image was set to `Color.clear` (alpha = 0). With zero alpha, the stencil write may not occur in Unity's UI rendering pipeline, causing all content inside the mask to be invisible. This is a known Unity UI pitfall.

---

## Attempt 2 — Flat HorizontalLayoutGroup (current)

**Approach:** Removed ScrollRect/Viewport/Mask entirely. FilterBar is now a plain container with a `HorizontalLayoutGroup` (`childForceExpandWidth = true`). Buttons are direct children of the FilterBar container.

**RectTransform setup:**
```
FilterBar
  anchorMin = (0, 1)   anchorMax = (1, 1)
  pivot     = (0.5, 1)
  offsetMin = (0, -48) offsetMax = (0, 0)
  → top-anchored, 48px tall, full width of ClubsContent
```

**Each button:**
- `Image.color` = `(1,1,1,0.20)` for ALL (active), `(1,1,1,0)` for others
- TMP label: white for ALL, `(0.55, 0.55, 0.55)` for others, 10px Bold
- `childForceExpandWidth = true` distributes 8 buttons evenly

**Still not showing.** Root cause not confirmed.

---

## Suspected Remaining Causes

In rough order of likelihood:

### 1. Builder not re-run after fix
The builder is idempotent (destroys and rebuilds from scratch), but must be **manually re-run** after each code change via **GOLFIN → Build Inventory Screen**. If the old ScrollRect hierarchy is still in the scene, the fix has no effect.

**Check:** In the Hierarchy panel, expand `InventoryScreen → ContentArea → ClubsContent → FilterBar`. If you see a `Viewport` child, the old version is still there. Re-run the builder.

### 2. ClubsContent itself is not active
`InventoryScreenController.Start()` calls `ShowTab(0)` which calls `SetActive(true)` on `ClubsContent`. `Start()` is deferred until `InventoryScreen` first becomes active (because it starts as `SetActive(false)`). If there's a frame where `ClubsContent` is still inactive before `Start()` runs, the FilterBar wouldn't show.

**Check:** In Play mode, navigate to Inventory. Open the Hierarchy and inspect `ClubsContent.activeSelf` — should be `true`.

### 3. FilterBar rendered behind ClubsMainSection
`ClubsMainSection` has `anchorMin=(0,0)`, `anchorMax=(1,1)`, `offsetMax=(0,-48)`. This is intended to fill everything *below* the FilterBar. However if `offsetMax.y = -48` is being misread (positive vs negative confusion), ClubsMainSection could overlap and occlude FilterBar.

**Check:** In Play mode, temporarily call `ClubsMainSection.SetActive(false)` from the Inspector and see if FilterBar appears.

### 4. Default TMP font not assigned
If the project doesn't have a default TMP font resource assigned, TextMeshProUGUI text will render as invisible (no font = no glyphs). This would affect buttons' labels silently.

**Check:** `Edit → Project Settings → TextMesh Pro`. Verify `Default Font Asset` is assigned.

### 5. Canvas or CanvasScaler clipping
If the `Canvas` has `Pixel Perfect` enabled or a `CanvasScaler` with a reference resolution much larger than the Game View, elements at the top of the screen can be pushed out of the safe area or clipped.

**Check:** Inspect the root Canvas. Temporarily set `Canvas Scaler → UI Scale Mode` to `Constant Pixel Size` and see if anything appears.

### 6. InventoryScreen not actually active during testing
`ScreenManager.ShowScreen(ScreenId.Inventory)` must be called for `InventoryScreen.SetActive(true)`. If the nav button is not wired to `PersistentUIManager.inventoryButton`, pressing it won't trigger navigation.

**Check:** In Play mode, open the Inspector on `PersistentUIManager` and verify `inventoryButton` is assigned. Alternatively, temporarily force `InventoryScreen.SetActive(true)` in the scene file to test without navigation.

---

## Recommended Next Debug Steps

1. **Re-run the builder** — confirm no `Viewport` child exists under FilterBar
2. **Force `InventoryScreen.SetActive(true)` in the scene** (temporarily) so it's visible without needing nav
3. **Manually create a test TMP text** as a child of ClubsContent in the Editor and confirm it renders — this isolates whether the issue is with ClubsContent visibility or with FilterBar specifically
4. **Add a bright red Image** to the FilterBar container (temporarily) to confirm whether the container itself renders — if the red box shows but the buttons don't, the issue is with the HLG / button children

---

## Files Modified (Phase B)

| File | Change |
|------|--------|
| `Assets/Scripts/UI/ScreenManager.cs` | Added `ScreenId.Inventory`, static `Instance`, `_inventoryScreen` field |
| `Assets/Scripts/UI/PersistentUIManager.cs` | Implemented `NavigateTo()` to call `ScreenManager.ShowScreen()` |
| `Assets/Scripts/UI/Inventory/InventoryScreenController.cs` | New — tab management |
| `Assets/Scripts/UI/Inventory/ClubFilterBar.cs` | New — filter type selection, fires `OnFilterChanged` |
| `Assets/Scripts/UI/Inventory/Editor/InventoryScreenBuilder.cs` | New — builds full hierarchy, wires all components |
