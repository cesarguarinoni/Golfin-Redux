# SPEC — `selector_carousel`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY` (2026-09-06).

## Goal

The in-game club selector and ball selector (`SelectorOverlayWidget`, the stacks that open from `DriverButton` / `GolfinButton`) currently render **every** club in the bag / every owned ball at once, and the only way to move through them is tapping the chevrons. Turn both into a **4-slot vertical carousel**: exactly four cards visible, the bottom slot (nearest the trigger button) is the **focus slot** and carries a **gold halo**, the player **slides a finger** over the stack to scroll, the stack **wraps around** (Putter → Driver), and every movement is a **smooth eased snap** — no more destroy/re-instantiate on each step. Whatever card comes to rest in the focus slot **is** the selected club/ball (the semantics the chevrons already have: `Scroll(±1)` → `ClubContext.RequestSelection`).

Cesar's decisions of record (2026-09-06): focus-slot-selects; hold-mode (press trigger, drag, release on a card) is **unchanged**; finger-slide scrolling is a **modal-mode** gesture; the ring **wraps**.

Reference: `reference/selector_before.png` (today's club selector, 7 cards + gated putter, Hole 1 tee).

## Architecture context

- **Asmdef:** `Golfin.Gameplay.UI` (`Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef`). It does NOT reference `Assembly-CSharp`, so `Golfin.UI.Polish.UiMotion` (`Assets/Scripts/UI/Polish/UiMotion.cs`, no asmdef) is **unreachable** from here. The tween in this spec is a local coroutine that copies UiMotion's three load-bearing properties (unscaled time, interruption-safe settle, final value on disable) and its ease-out cubic. Do not add an asmdef reference to fix this; do not move UiMotion. NOTE for the backlog row, not for this task.
- **Existing code (edit):**
  - `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` — owns cards, arrows, hold-hover, `Scroll(int)`, `Populate()`, `FindNextSelectableClub`, `IsClubSelectable` (K11 gate).
  - `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs` — `SetClub` / `SetBall` / `SetHighlight` / `SetSelectable` / `InvokeSelection`. Gains nothing but a `Bind` overload is allowed if you need to rebind without re-wiring the button.
  - `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` — builds `SelectorOverlay` (clubs, pivot (1,0)) and `SelectorOverlay_Ball` (balls, pivot (0,0)) into `Assets/Scenes/Physics/LabScaffold.unity`; menu `GOLFIN/Build/Build Action Buttons (8.5)`. Card prefab GO `SelectorCard_Prefab` 145×240; `CardsContainer` VLG spacing 34 → **pitch P = 274**.
- **Existing code (read, do not edit):** `SelectorDragRouter.cs` (hold/modal state machine, `_arrowRepeatDelay` 0.3 s, `_arrowRepeatInterval` 0.15 s), `ClubSelectionBroadcast.cs` (`Raise`, `IsSelectable`, `InPutterMode`, `PutterLabClubIndex`), `HUD/ClubContext.cs` / `HUD/BallContext.cs` (`SelectedIndex`, `EquippedBag` / `OwnedBalls`, `RequestSelection`, `OnSelectedChanged`, `OnBagChanged`), `OtherButtonsFader.cs`, `Assets/Scripts/Gameplay/Tests/ClubSelectionGreenGateTests.cs`.
- **Precedent to mirror (not reuse — different asmdef, horizontal, non-wrapping):** `Assets/Scripts/UI/Gacha/GachaCarouselController.cs` — `IBeginDragHandler/IDragHandler/IEndDragHandler`, continuous `_currentOffset`, snap to nearest, eases only when not dragging.
- **New asset (already in the repo, dropped by the Architect):** `Assets/Art/In-Game UI/Halo - Selector.png` — 217×312 white glow with a 4 px ring, transparent interior (the card face is never tinted). Import as **Sprite (2D and UI)**, Mesh Type Full Rect. The builder tints it `#FCF195` (the §D7 selected-state gold). Placeholder-quality by design; Robin's art drops into the same path with zero code.

## Model

Everything below is per overlay instance (club and ball are the same code, `Kind` decides the data source).

| Symbol | Meaning |
|---|---|
| `N` | item count: `ClubContext.EquippedBag.Count` or `BallContext.OwnedBalls.Count` |
| `P` | slot pitch, 274 canvas px (240 card + 34 gap). Read it from the serialized `_slotPitch`; do not hardcode |
| `_scroll` | continuous position in **items**. `round(_scroll)` = virtual index of the item in the focus slot; item index = `Mod(round(_scroll), N)` |
| `Wrap` | `N >= 5`. Below that there is no ring — the four slots already show everything, and a ring of ≤ 4 items would render the same card twice. `_scroll` is then clamped to `[0, N-1]` with a rubber-band (see §3) |
| pool slot `j` | `-1 … 4` (6 cards: 4 visible + 1 buffer above + 1 below). Card `j` shows virtual index `floor(_scroll) + j` and sits at `y_bottom = (j - frac(_scroll)) × P` inside the viewport, measured from the focus slot's bottom edge |
| focus slot | `j = 0` at rest. Its bottom edge is at the same y the bottom card has today (the overlay root's anchoring is untouched) |

Direction: **dragging the finger DOWN by P increases `_scroll` by 1** — the card above comes down into the focus slot. Chevron mapping is **unchanged**: `ScrollUp` (top chevron) = `+1`, `ScrollDown` = `-1`, exactly what `Scroll(int)` does today. NOTE: today the top chevron therefore moves the stack downward; Cesar has not asked to change it, so it stays. If it reads wrong on device it is a one-line flip in `WireArrows`, filed as a separate quick.

Selection is committed **at snap start**, not on settle: the instant a snap target is decided (`SnapTo`), call `ClubContext.RequestSelection(item)` + `ClubSelectionBroadcast.Raise(bag[item].LabClubIndex)` (clubs) or `BallContext.RequestSelection(item)` (balls), so the trigger button relabels while the card is still travelling. Re-issuing the same index is a no-op today and stays one.

## Implementation

### 1. Hierarchy (both overlays, via `ActionButtonsBuilder`)

`CardsContainer` becomes a fixed **viewport**:

- Remove its `VerticalLayoutGroup` and `ContentSizeFitter`. Keep the `LayoutElement`; set `preferredHeight = 4×240 + 3×34 + 2×36 = 1134`, `flexibleWidth = 1`. Width stays 145 via the root VLG (`childForceExpandWidth`), the halo overhangs 36 px each side and is allowed to — the mask is what clips, and it clips to the viewport rect **plus** the 36 px vertical margin, so the halo's glow survives top and bottom. (Sides: the root is 145 wide; set `RectMask2D.padding = (-36, 0, -36, 0)` so the mask rect is widened by 36 px left/right. If Unity's `padding` sign turns out to shrink rather than grow on this version, widen the viewport RectTransform by 72 instead and offset the cards by +36 in x — report which.)
- Add `RectMask2D` (it also culls pointer raycasts outside its rect — `ICanvasRaycastFilter` — so clipped buffer cards cannot be tapped).
- Add a transparent `Image` (`color (0,0,0,0)`, `raycastTarget = true`) so a drag that starts in the 34 px gap between cards is still caught.
- Add the new `SelectorCarouselDrag` component (§4).
- Child 0 (behind the cards): `FocusHalo` — `Image`, sprite `Halo - Selector`, `color #FCF195`, `raycastTarget = false`, `sizeDelta (217, 312)`, anchored so its centre is the focus slot's centre: bottom-left anchor, `anchoredPosition = (72.5, 36 + 120)`. `LayoutElement.ignoreLayout = true`.
- Children 1–6: the six pool cards, instantiated by the widget at runtime from `_cardPrefab` exactly as today (the builder does **not** pre-create them). All cards: bottom-left anchor + pivot (0,0), width 145, x = 0, y from §3.

Hold-mode geometry is unchanged for the player: the focus slot's bottom edge is where the bottom card's bottom edge is today (y = 36 inside the viewport; the overlay root VLG absorbs the +36 by growing upward — verify the DRIVER-side bottom card still sits at the same canvas y as `DriverButton`'s bottom, ±1 px, and report the number).

Serialized fields on `SelectorOverlayWidget` (wire in the builder for both overlays): `_cardsViewport` (the `CardsContainer` RT, replaces the `_cardsContainer` usage), `_focusHalo` (RT), `_carouselDrag` (`SelectorCarouselDrag`), `_slotPitch = 274`, `_visibleSlots = 4`, `_viewportMargin = 36`, `_snapBaseDur = 0.12`, `_snapPerItemDur = 0.06`, `_snapMinDur = 0.15`, `_snapMaxDur = 0.35`, `_flingLookaheadSec = 0.12`, `_flingMaxItems = 3`, `_haloBumpPeak = 1.06`, `_haloBumpDur = 0.10`, `_rubberBandItems = 0.35`.

Halo sprite import: before `LoadAssetAtPath<Sprite>`, the builder gets the `TextureImporter` for the halo path and, if `textureType != Sprite`, sets `textureType = TextureImporterType.Sprite`, `spriteImportMode = Single`, `mipmapEnabled = false` and `SaveAndReimport()` — idempotent, so re-running the builder is safe.

Re-run `GOLFIN/Build/Build Action Buttons (8.5)` once, then save `LabScaffold.unity`. ⚠️ `LabScaffold.unity` is currently **dirty from the control-scheme work** (`git status` on 2026-09-06). The builder rebuilds the whole `ActionButtons_Cluster` + both overlays + `SpinPanel`, so the scene diff will be large regardless; that is how `SelectorScreenshotHelper` already works. Commit the scene in the same commit as this task and say so in the report — do not try to hand-split hunks.

### 2. `SelectorOverlayWidget` — pool instead of Populate

Replace `Populate()`'s destroy/instantiate loop with a **6-card pool** created once per `Open`/`OpenFromRouter` if the pool is empty (`_pool.Count == 0`), and **rebound** on every layout pass:

```csharp
void Layout()   // called from Open, every frame while dragging/snapping, and after any data change
{
    int  n     = ItemCount;                       // clubs: bag.Count, balls: OwnedBalls.Count
    int  baseV = Mathf.FloorToInt(_scroll);
    float frac = _scroll - baseV;
    for (int j = -1; j <= 4; j++)
    {
        var card = _pool[j + 1];
        int v = baseV + j;
        bool show = Wrap ? true : (v >= 0 && v < n);
        card.gameObject.SetActive(show);
        if (!show) continue;
        int item = SelectorCarouselMath.Mod(v, n);
        BindCard(card, item);                     // SetClub/SetBall + SetSelectable (K11) — cheap, no alloc beyond the closure below
        card.Rt.anchoredPosition = new Vector2(0f, _viewportMargin + (j - frac) * _slotPitch);
    }
}
```

`BindCard` keeps the existing `SetClub(entry, onTap)` / `SetBall(entry, onTap)` calls and the existing K11 `SetSelectable(IsClubSelectable(entry))`. Cache one `Action` per pool card that reads the card's **current bound item** from a field (`card.BoundItem`) so binding does not allocate a new closure per frame. The onTap body is §5.

`_scroll` is initialised on open to `SelectedIndex` (float). `Wrap` per the model table. `_cards` / `_cardRts` (used by `UpdateHoldHover`, `EvaluateRelease`, `CommitHighlighted`, `SetHighlightAt`) now point at the **active pool cards whose rect centre lies inside the viewport rect** (`RectTransformUtility.RectangleContainsScreenPoint(_cardsViewport, centreScreen)`); rebuild those two lists at the end of `Layout()`. Hold-mode code above them is otherwise untouched.

### 3. Scroll, snap, tween

```csharp
public static class SelectorCarouselMath          // new file, same folder, pure, EditMode-testable
{
    public static int   Mod(int v, int n);                                   // true modulo, never negative; n<=0 → 0
    public static float SlotY(int j, float scroll, float pitch, float margin);
    public static float EaseOutCubic(float t);                               // 1 - (1-t)^3, same curve as UiMotion
    public static float SnapDuration(float deltaItems, float baseDur, float perItem, float min, float max);
    /// Nearest virtual index to (scroll + velocity*lookahead), fling clamped to ±maxFling from scroll,
    /// then walked outward in the direction of travel (or +1 when stationary) until selectable(Mod(v,n)) is true.
    /// Wrap=false additionally clamps to [0, n-1] before the walk and walks inward if the walk runs off an end.
    /// Returns fallback (the current selection's virtual index) if no item is selectable — cannot happen with
    /// the K11 rule (putter mode leaves the putter selectable) but the function must not loop forever.
    public static int   ResolveSnapTarget(float scroll, float velocityItemsPerSec, float lookaheadSec,
                                          int maxFling, int n, bool wrap, Func<int,bool> selectable, int fallback);
}
```

`SelectorOverlayWidget`:

- `SnapTo(int targetVirtual)`: commit selection (Model §), then start `SnapRoutine`: `start = _scroll`, `dur = SnapDuration(|target-start|, …)`, `t` on `Time.unscaledDeltaTime`, `_scroll = Lerp(start, target, EaseOutCubic(t))`, `Layout()` each frame; on completion `_scroll = target` (exact), then `HaloBump()`. Store the coroutine handle in `_snapRoutine`. **Interruption-safe:** any new `SnapTo` or `BeginDrag` stops the running routine first; a stopped snap does **not** jump — `_scroll` stays where it was (the drag continues from there), but the already-committed selection stands. `OnDisable` / `Close`: stop the routine and set `_scroll = round(_scroll)` so the overlay is at rest next open (Open re-syncs to `SelectedIndex` anyway).
- `Scroll(int delta)` (chevrons + hold-mode auto-scroll) becomes: `target = ResolveSnapTarget(round(_scroll) + delta …)` using `FindNextSelectableClub`'s rule; returns `false` when the target equals the current focus item (so `ArrowScrollRoutine` still stops on the green). Duration for chevron/auto-scroll steps is forced to `_arrowRepeatInterval`'s value (0.15 s) — pass a `durOverride` — so a held arrow reads as one continuous glide, not a stutter.
- `HaloBump()`: `_focusHalo.localScale` 1 → `_haloBumpPeak` → 1 over `_haloBumpDur`, unscaled, interruption-safe (a second bump restarts from 1).
- `_scroll` normalisation: after every settle, if `Wrap`, re-centre `_scroll` into `[0, n)` (`_scroll = Mod(round(_scroll), n)`) so the float never grows unbounded across a long session.
- Rubber-band (`Wrap == false` only): during a drag `_scroll` may exceed `[0, n-1]` by up to `_rubberBandItems` with the excess scaled ×0.35 (finger moves 100 px past the end, cards move 35); `EndDrag` snaps back inside.

### 4. `SelectorCarouselDrag` (new, same folder)

`MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler`, on `CardsContainer`. Holds a reference to its `SelectorOverlayWidget` (wired by the builder). Enabled **only in modal mode**: `SelectorOverlayWidget.EnterModalMode()` sets `_carouselDrag.enabled = true`; `OpenFromRouter` / `Open` / `Close` set it `false` (the legacy `Open(Kind)` path is modal from the start, so `Open` sets it `true` right after). In hold mode the pointer that opened the overlay is still down on the trigger, so no new drag can start on the container anyway — the flag is belt-and-braces.

- `OnBeginDrag`: stop any snap (`_overlay.CancelSnap()`), `_scrollAtStart = _scroll`, `_startLocalY` = pointer in `CardsContainer` local space (`RectTransformUtility.ScreenPointToLocalPointInRectangle` with the canvas camera, `null` for ScreenSpaceOverlay — same rule as `IsOverRect`). Start a small ring of `(time, scroll)` samples.
- `OnDrag`: `_overlay.SetScroll(_scrollAtStart + (_startLocalY - localY) / _slotPitch)` (finger down ⇒ +). `SetScroll` applies the rubber-band when `!Wrap`, then `Layout()`. Push a sample.
- `OnEndDrag`: velocity = (scroll now − scroll ≤ 100 ms ago) / Δt in items/s, 0 if fewer than 2 samples. `target = ResolveSnapTarget(...)` with `selectable = i => IsSelectable(i)` (clubs: `IsClubSelectable(bag[i])`; balls: always true). `_overlay.SnapTo(target)`.

Tap vs drag is separated by `EventSystem.pixelDragThreshold`: once `OnBeginDrag` fires the input module clears `eligibleForClick`, so a slide never also fires a card's `Button.onClick`, and a clean tap never enters the drag handlers. No custom threshold.

`OutsideClickCatcher_Selector*` and `OtherButtonsFader` are untouched; a drag on the viewport is consumed by the viewport's raycast target so it neither closes the overlay nor orbits the camera (`PhysicsLabController` already blocks orbit while `OtherButtonsFader` reports open).

### 5. Card tap (modal mode)

The card's `onTap` (wired through `SetClub`/`SetBall`, still guarded by K11 `IsSelectable` at invoke time):

- Tapped card **is** the focus item (`BoundItem == Mod(round(_scroll), n)`): behave exactly as today — commit (already committed, so effectively a no-op re-issue) → `_router.OnModalCommit()` or `Close()`.
- Tapped card is another visible item: `SnapTo(thatCard's virtual index)` (its `j` ≠ 0 pool slot → `floor(_scroll) + j`; shortest-way is automatic because visible cards are at most 3 slots away), and when the snap settles, `_router.OnModalCommit()` / `Close()`. Set a `_closeOnSettle` flag that `SnapRoutine` honours; a `BeginDrag` before settle clears it (the player changed their mind — stay open, selection already committed).

Hold-mode release on a card (`CommitHighlighted`) is **unchanged**: it invokes the card's selection which commits and the router closes the overlay. No animation — the overlay is gone. Do not route it through `SnapTo`.

### 6. Data changes while open

`OnBagChanged` / `OnSelectedChanged` fired by something other than this widget while it is open (e.g. `PhysicsLabController` entering putter mode is only possible between shots, when the overlay is closed — but be safe): subscribe in `OnEnable`, unsubscribe in `OnDisable`; handler = `CancelSnap(); _scroll = SelectedIndex; Layout();`. Skip it while `_snapRoutine != null` **and** the change was our own commit (compare the incoming `SelectedIndex` to the pending target).

### 7. What is deleted

`Populate()` and its `order` list, the per-step `DestroyImmediate` loop, `_cardsContainer` (replaced by `_cardsViewport`). Everything in `SelectorDragRouter.cs` stays byte-identical.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item `PASS`/`FAIL` with what was measured.

- [ ] **EditMode — `SelectorCarouselMathTests`** (new, in `Assets/Scripts/Gameplay/Tests/`, beside `ClubSelectionGreenGateTests`): `Mod(-1,8)=7`, `Mod(8,8)=0`, `Mod(3,0)=0`; `SlotY(0, 2.0, 274, 36)=36` and `SlotY(1, 2.5, 274, 36)=36+137`; `SnapDuration` clamps to `[min,max]`; `ResolveSnapTarget` — fling clamped to ±3; stationary on a gated putter walks +1; travelling −1 onto a gated putter walks past it; putter-only mode (only index 7 selectable) returns 7 from any scroll/velocity; `wrap=false, n=3` never returns outside `[0,2]`; no-selectable returns `fallback`.
- [ ] **EditMode — `ClubSelectionGreenGateTests`** still green, unmodified.
- [ ] **Play (LabScaffold, Hole 1 tee, off green):** tap `DriverButton` → overlay shows **exactly 4 cards** (count the active pool cards inside the viewport in the Hierarchy: 4, plus ≤ 2 buffer cards clipped) and the halo sits on the bottom card, which is the currently selected club. Screenshot → `screenshots/01_open_4_slots.png`.
- [ ] **Slide:** drag down ≈ one pitch and release → the card above glides into the focus slot, `DriverButton` label updates to that club **before** the card stops moving. Drag up → previous club. Both directions.
- [ ] **Fling:** a fast short flick advances more than one item and never more than 3; settles on a whole slot.
- [ ] **Wrap:** from the last bag item, sliding down brings Driver into focus; from Driver sliding up brings the last item. The sequence in the stack is continuous with no blank slot (bag ≥ 5 clubs).
- [ ] **K11 off green:** the gated putter still renders greyed at 0.5 alpha in the ring, but **no snap ever rests on it** — slide so it would land in focus; it walks to the neighbour. `DriverButton` never shows PUTTER off the green.
- [ ] **K11 on green (putter mode):** every slide/fling snaps back to the putter; chevrons no-op (`Scroll` returns false).
- [ ] **Chevrons:** tap top/bottom chevron → one animated step each, same mapping as before the task. Hold-mode arrow hover auto-scroll still glides continuously at the 0.15 s cadence.
- [ ] **Hold mode unchanged:** press `DriverButton`, drag onto a visible card, release → that club is selected and the overlay closes; drag to an arrow and release → modal mode; drag outside and release → closes. Same for `GolfinButton`.
- [ ] **Modal tap:** tap a non-focus visible card → it glides into focus, then the overlay closes with that club selected. Tap the focus card → closes immediately.
- [ ] **Ball overlay, 1 owned ball (default GOLFIN ∞):** one card, halo on it, sliding does nothing beyond the rubber-band, no exceptions. With `SelectorScreenshotHelper.CaptureBallSelector`'s 2 injected balls (GOLFIN ∞ + PUTT ACE): 2 visible, no wrap, rubber-band at both ends, sliding swaps which one is in focus. Add two more `BallEntry` rows to that helper locally (do not commit them) to check `n = 4` no-wrap, and 5 to check the ring turning on.
- [ ] **Geometry:** bottom card's bottom edge (club side) is at the same canvas y as `DriverButton`'s bottom ±1 px — report both numbers. Halo glow visible on all four sides of the focus card (not clipped).
- [ ] **No churn:** Profiler (Editor, Deep Profile off) during a 3-second continuous drag: zero `Instantiate`/`Destroy` calls from `SelectorOverlayWidget`, GC alloc per frame from `Layout` = 0 B after the first frame.
- [ ] `GOLFIN/Capture/Selector - Club (Driver side)` and `… - Ball (Golfin side)` still run to completion and their captures show the 4-slot layout (drop them in `screenshots/`).
- [ ] No new player-facing strings (grep: zero new `.text = "` literals in the touched files).
- [ ] All `[SerializeField]` references wired in both overlays; Unity Console clean of errors from this task.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` — pool + `Layout` + `SnapTo` + tween + halo bump; `Populate` removed.
- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselMath.cs` — NEW, pure.
- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselDrag.cs` — NEW, drag handlers.
- `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs` — `BoundItem` field + `Rt` cache; no behaviour change.
- `Assets/Scripts/Gameplay/Tests/SelectorCarouselMathTests.cs` — NEW.
- `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` — viewport, mask, halo, drag component, new serialized wiring, halo importer fix-up (both overlays).
- `Assets/Art/In-Game UI/Halo - Selector.png` — already present (Architect); `.meta` is generated on import.
- `Assets/Scenes/Physics/LabScaffold.unity` — rebuilt by the builder.

## Smoke evidence

Screenshots in `screenshots/` as listed; a 10–15 s screen recording of slide / fling / wrap / K11-skip in `videos/` (the Editor Game view at 1170×2532 is fine). The on-device **feel** (finger tracking latency, fling weight, snap timing) is Cesar's call — flag every timing constant in the report so he can tune them in the Inspector without a code change.

## Out of scope (do NOT do these)

- Hold-mode drag scrolling the stack (Cesar chose "hold-mode unchanged").
- Overlay open/close entrance animation — the overlay still appears instantly.
- Distance-based scale/alpha falloff on non-focus cards (Gacha-style). Cards stay 1.0 / full alpha; K11 greying is the only alpha rule.
- A reduced-motion switch (would need `UiMotion.Enabled` reachable across the asmdef boundary).
- Flipping the chevron mapping.
- The inventory-screen carousels (`ClubCarouselController`, `BallCarouselController`, …) — different screens, untouched.
- Any change to `SelectorDragRouter.cs`, `ClubContext`, `BallContext`, `ClubSelectionBroadcast`.
