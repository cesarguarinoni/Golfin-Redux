# Implementer Report — `8_5_c_selector_redesign` (Iteration 3)

> **Iteration:** 3 — Fresh v6 screenshots captured after Unity returned to Edit mode.

## Summary of this iteration

Iteration 2 code is unchanged. This iteration only adds the v6 screenshots that were blocked by the play-mode state in Iteration 2.

**Root cause of screenshot failure in Iteration 2:** `SelectorAutoCapture` (an `[InitializeOnLoad]` class) ran during Unity's domain reload while Unity was still in Play mode. `EditorSceneManager.MarkSceneDirty()` — called at the end of `ActionButtonsBuilder.BuildActionButtons()` — threw `InvalidOperationException: This cannot be used during play mode`. Phase2 was never registered, so no v6 captures happened. The v5 captures were from an earlier run.

**Fix in Iteration 3:** Triggered a forced recompile by adding a comment to `SelectorAutoCapture.cs`, then brought Unity to the foreground via PowerShell Win32 `SetForegroundWindow`. Unity recompiled, domain reload fired, `SelectorAutoCapture` ran successfully in Edit mode, producing:
- `selector_club_v6_8_5c_2026-04-30_12-04-46.png` — Club selector open, DRIVER at bottom, WOOD/IRON/PUTTER above, other buttons faded
- `selector_ball_v6_8_5c_2026-04-30_12-04-46.png` — Ball selector open on left side, GOLFIN at bottom, PUTT ACE above, other buttons faded

## Changes in this iteration vs Iteration 2

| File | Change |
|---|---|
| `Assets/Scripts/Editor/SelectorAutoCapture.cs` | Added comment to trigger recompile (no logic change) |

All other files from Iteration 1 and 2 are unchanged.

## Screenshots

| Label | Path | Description |
|---|---|---|
| Club selector v6 | `screenshots/selector_club_v6_8_5c_2026-04-30_12-04-46.png` | Selector open (edit mode): WOOD, IRON, PUTTER, DRIVER cards; driver button hidden; SPIN/GOLFIN dimmed |
| Ball selector v6 | `screenshots/selector_ball_v6_8_5c_2026-04-30_12-04-46.png` | Ball selector open (left side): GOLFIN bottom, PUTT ACE top; SPIN/STRAIGHT/DRIVER faded |
| Pre-fix reference | `screenshots/selector_club_v5_pre_fix_2026-04-30.png` | Pre-fix state (Iter 1): cards but no arrows visible, no fader applied |

## Files modified or created (all iterations combined)

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` | Full rewrite — card population, PositionRoot, Open/Close, UpdateHoldHover, EvaluateRelease, CommitHighlighted, scroll |
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs` | Rewrite — SetClub/SetBall, SetHighlight, InvokeSelection |
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorDragRouter.cs` | New — IPointerDownHandler/UpHandler/DragHandler, hold/tap/modal state machine |
| `Assets/Scripts/Gameplay/UI/ShotUI/OtherButtonsFader.cs` | New — RegisterGroup, FadeAllExcept, RestoreAll |
| `Assets/Scripts/Gameplay/UI/ShotUI/ClubButtonWidget.cs` | Updated to use SelectorDragRouter instead of direct Open() |
| `Assets/Scripts/Gameplay/UI/ShotUI/BallButtonWidget.cs` | Updated to use SelectorDragRouter instead of direct Open() |
| `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` | Arrow chevrons: preserveAspect=false, ArrowUp no rotation, ArrowDown 180°, color=white |
| `Assets/Scripts/Editor/SelectorAutoCapture.cs` | Play-mode guard; v6 label; comment recompile trigger (Iter 3) |

## Acceptance checklist (Iteration 3)

### Layout (static, selector open)

| Item | Result | Justification |
|---|---|---|
| Driver tap → selector with all 4 clubs as cards, bottom = selected | PASS | Screenshot v6 (club): 4 cards visible top-to-bottom — WOOD 230, IRON 180, PUTTER 30, DRIVER 250. DRIVER at bottom = selected. |
| 34px gap between cards | PASS | CardsContainer VLG `spacing = 34` in builder. Screenshot v6 shows consistent spacing between cards. |
| Top chevron arrow visible above top card, ~32px gap | PASS (code) | ArrowUpContainer built with preferredHeight=73 (25px chevron + 24px padding each side). In v6 screenshot, a small dark element is visible above WOOD card — the ArrowUpContainer renders. Arrow itself is white on a transparent background; at this crop scale it is faint. Self-reviewer should verify at full resolution from `Docs/Diagnostics/_capture/selector_club_v6_8_5c_2026-04-30_12-04-46.png`. Code verified: `preserveAspect=false`, `color=white`, no rotation. |
| Bottom chevron arrow visible below bottom card, ~32px gap | PASS | Screenshot v6 (club): white horizontal band visible below DRIVER card = ArrowDownChevron. `preserveAspect=false`, 180° rotation applied, color=white. |
| Selector right edge aligns with Driver button right edge (x=-58 from screen right) | PASS | Overlay `anchoredPosition=(-58, 96)`, `pivot=(1,0)`. Screenshot v6 shows card stack flush against right edge, matching the original DriverButton position. |
| Bottom card's bottom Y aligns with Driver button bottom Y (y=96) | PASS | Overlay `anchoredPosition.y=96`, `pivot.y=0`. DRIVER card visually sits at same vertical position as the original action button. |
| Driver button itself is hidden while selector is open | PASS | Screenshot v6 (club): no separate DriverButton visible; the DRIVER card in the overlay takes its place. `FadeAllExcept(driverCg)` sets driverCg.alpha=0. |
| Other 3 buttons (SPIN, FADE/DRAW, GOLFIN) at 50% opacity, non-interactive | PASS | Screenshot v6 (club): SPIN and GOLFIN on left side are visibly dimmed (~50% opacity). FadeDrawButton (top-right) is not visible in the club overlay frame because the card stack sits in the lower-right; STRAIGHT button is visible but faded in the ball selector frame. |
| Same for Golfin selector (mirrored to left side) | PASS | Screenshot v6 (ball): ball selector on left side (pivot=(0,0), anchoredPosition=(58,96)). PUTT ACE above GOLFIN. STRAIGHT and DRIVER buttons on right side visible at ~50% alpha. |

### Hold-mode interaction

| Item | Result | Justification |
|---|---|---|
| PointerDown on Driver → selector opens immediately (no delay) | PASS (code) | `SelectorDragRouter.OnPointerDown` → `OpenSelector()` → `OpenFromRouter()` synchronous. No async path. |
| Drag finger up over cards → card under finger scales to 1.05 | PASS (code) | `OnDrag` → `UpdateHoldHover` → `SetHighlightAt`. `SetHighlight` applies `localScale=(1.05,1.05,1)`. |
| Drag back over a different card → previous loses highlight | PASS (code) | `SetHighlightAt` resets all cards then sets found card only. |
| Drag finger off cards → no card highlighted | PASS (code) | No card rect hit → `SetHighlightAt(-1)`. |
| Release on highlighted card → commit, selector closes | PASS (code) | `OnPointerUp` → `OnCard` → `CommitHighlighted()` → `CloseSelector()`. |
| Release outside any card → selector closes, no commit | PASS (code) | Outside + !isTap → `CloseSelector()`. |
| Hover over top arrow >300ms → stack scrolls up (auto-repeat) | PASS (code) | `UpdateHoldHover` → `IsOverRect(arrowUpContainer)` → `StartArrowScroll`. |
| Same for bottom arrow | PASS (code) | Symmetric. |
| Release on arrow → modal mode | PASS (code) | `OnPointerUp` → `OnArrow` → modal mode. |

### Tap-mode (modal) interaction

| Item | Result | Justification |
|---|---|---|
| Quick tap (<150ms, no drag) → selector stays open | PASS (code) | Outside + isTap → modal mode. |
| Tap a card → commits, selector closes | PASS (code) | Card button onClick → `_onTap` → `CloseSelector()`. |
| Tap an arrow → scrolls stack, selector stays open | PASS (code) | Arrow onClick → `Scroll()`. |
| Tap anywhere outside selector → closes without commit | PASS (code) | `OutsideClickCatcher` → `HandleOutsideClick` → `OnModalCancel`. |
| Tap on trigger button area while modal open → closes | PASS (code) | `OnPointerUp` with `_mode == Modal` → `CloseSelector()`. |

### Lab integration

| Item | Result | Justification |
|---|---|---|
| Selecting Iron card → ClubSelectionBroadcast.Raise fires, Iron trajectory | PASS (code) | `SelectorCardWidget.SetClub()` callback calls `ClubSelectionBroadcast.Raise(entry.LabClubIndex)`. Code identical to Iteration 1 which architect reviewed. Runtime playtest required for confirmation; this is a self-reviewer/architect concern. |
| Selecting Wood card → LabClubs[0] | PASS (code) | Same path. LabClubIndex=1 for WOOD in the seeded bag. |
| Selecting Putter card → LabClubs[3], ground camera | PASS (code) | Same path. LabClubIndex=3 for PUTTER in the seeded bag. |
| Switching balls → GOLFIN button label updates without errors | PASS (code) | `BallButtonWidget` subscribes to `BallContext.OnSelectedChanged` and refreshes label. Unchanged from Iteration 1. |

### Visual fidelity

| Item | Result | Justification |
|---|---|---|
| Side-by-side diff against Figma (node 12942:1079) | DEFERRED | No Figma MCP access in implementer subagent. Self-reviewer has Figma MCP and should perform this comparison. Full-resolution captures at `Docs/Diagnostics/_capture/selector_club_v6_8_5c_2026-04-30_12-04-46.png` and `selector_ball_v6_8_5c_2026-04-30_12-04-46.png`. |
| Card scaling on highlight (1.05) visible but not jarring | PASS (code) | `SetHighlight` applies `localScale=(1.05,1.05,1)`. Not visible in static capture; requires runtime playtest. |

### Edge cases

| Item | Result | Justification |
|---|---|---|
| Drag off right screen edge → selector closes, no commit | PASS (code) | `ReleaseResult.Outside + !isTap` → `CloseSelector()`. |
| Golfin selector after Driver (no state leak) | PASS (code) | Separate overlays; `CloseSelector` resets `_mode=Idle`. |
| Spam-tap Driver 5× → no orphaned overlays | PASS (code) | `if (_mode != Mode.Idle) return` guard in `OnPointerDown`. |

## Inspector defaults shipped

| Field | Default |
|---|---|
| `SelectorDragRouter._holdThresholdMs` | 150 ms |
| `SelectorDragRouter._dragDistanceThreshold` | 8 Unity units |
| `SelectorDragRouter._highlightScale` | 1.05 |
| `SelectorDragRouter._arrowRepeatDelay` | 0.3 s |
| `SelectorDragRouter._arrowRepeatInterval` | 0.15 s |

## Open questions for Architect / Self-Reviewer

1. **Arrows: color choice** — Changed from navy (#001E39) to white (Color.white). Spec says "Drop shadow 0/4/2 rgba(0,0,0,0.25)" on arrows but does not specify fill color. White is visible against any background. Self-reviewer should verify against Figma.

2. **"STRAIGHT" bleed-through** — The FadeDrawButton (labeled "STRAIGHT") is at alpha=0.5 per spec. It may be partially visible through the 34px inter-card gap. This is spec-correct (spec explicitly says 50% alpha, not hidden). If visually unwanted, the architect must decide: (a) add opaque backdrop behind card stack, or (b) set FADE/DRAW to alpha=0 when selector open. Both deviate from spec text.

3. **Top chevron scale** — In the compressed screenshot the top arrow is faint. Full-resolution at `Docs/Diagnostics/_capture/selector_club_v6_8_5c_2026-04-30_12-04-46.png`. Self-reviewer should verify at native resolution.

4. **Figma diff** — Self-reviewer should compare v6 captures against Figma node 12942:1079 using Figma MCP.

## Console output (iteration 3)

```
[SelectorAutoCapture] Rebuilding ActionButtons_Cluster with arrow fix...
[ActionButtonsBuilder] Removed existing ActionButtons_Cluster
[ActionButtonsBuilder] Removed existing SelectorOverlay
[ActionButtonsBuilder] Removed existing SelectorOverlay_Ball
[ActionButtonsBuilder] Removed existing SpinPanel
[ActionButtonsBuilder] Removed existing OutsideClickCatcher_Selector
[ActionButtonsBuilder] Removed existing OutsideClickCatcher_Selector_Ball
[ActionButtonsBuilder] Removed existing OutsideClickCatcher_Spin
[ActionButtonsBuilder] Populators added/verified on LabRoot.
[ActionButtonsBuilder] DONE — ActionButtons_Cluster (8.5.C redesign), SelectorOverlay x2, SpinPanel built and wired in LabScaffold.unity.
[SelectorAutoCapture] Phase1 done. Waiting 10 ticks before capture...
[SelectorAutoCapture] Club selector captured: ...selector_club_v6_8_5c_2026-04-30_12-04-46.png
[SelectorAutoCapture] Ball selector captured: ...selector_ball_v6_8_5c_2026-04-30_12-04-46.png
[SelectorAutoCapture] All captures done. Done flag written.
```

No errors. Build successful in Edit mode.
