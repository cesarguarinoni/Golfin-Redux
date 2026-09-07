# Implementer Report — `selector_carousel`

> **STATUS: `READY_FOR_SELF_REVIEW` (iter-2).** Cesar rejected iter-1 on sight and was right on
> every count. Everything below is now measured **in the running game** — booted through
> Home → PRACTICE → PLAY → Lomond hole 2, selectors opened through the real `DriverButton` /
> `GolfinButton` routers — not in an edit-mode harness.

## Cesar's iter-1 rejection — what was wrong and what fixed it

| Defect | Root cause | Fix | Verified (real play) |
|---|---|---|---|
| "Screenshot shows the old ball placing (at the centre)" | The canonical frame was an edit-mode LabScaffold capture. `ShotLayoutController.Apply` only runs in `OnEnable`, i.e. play mode, so the whole frame carried the pre-`shot_view_layout` authored framing. It looked plausible, so no gate caught it. | Re-shot through real navigation; plus three structural guards (below) so this cannot recur. | Sidecar records `shotLayoutApplied: true`, `ballViewportY: -303.84` — the live flick framing. |
| "The carousel and the buttons are misaligned" | `SetOpenBaselineY` set the overlay ROOT's y to the baseline, but the focus slot sits `chevron + gap + margin` ABOVE the root — so the focus card floated **+68 px** above its trigger button at runtime. Authored-frame delta was 0.00, which is what I measured and reported in iter-1; I saw the runtime 68 and filed it "pre-existing" instead of fixing it. | The baseline now means **the focus slot's bottom edge**. `PositionRoot` drops the root by `FocusSlotOffsetFromRootBottom()`, read from the live chevron `LayoutElement` + VLG spacing + `_viewportMargin` — so the club (60) and ball (73) chevrons both land right. | Focus card bottom **170.00**, `DriverButton` bottom **170.00**, delta **+0.00 px**. |
| "The glow is not centred on the selected club and doesn't fit it" | The halo PNG is 217x312 but its drawn RING is only 154x249 of that (alpha bbox (32,32)-(185,280)). Drawn at native size the ring landed 4.5 px outside the 145x240 card on every edge. | Halo sized so the RING maps to the card: `217 x 145/154 = 204.32`, `312 x 240/249 = 300.72`. | Halo centre offset **(0.00, 0.00)**; ring measures **145.0 x 240.0** vs card **145.0 x 240.0**. |
| "The carousel is not working on the ball selector (can't scroll, can't select)" | `OutsideClickCatcher_Selector_Ball` was built AFTER `SelectorOverlay_Ball`, so the full-screen catcher was a later sibling and rendered ON TOP, eating every pointer event. The club-side catcher happens to be built first, which is why only the ball side was dead. Hold mode hid it before (it hit-tests screen positions rather than raycasting); the carousel needs real raycasts, so it went fatal. | Builder pins the catcher directly beneath its overlay via `SetSiblingIndex` instead of relying on construction order. | Raycast at the ball viewport now resolves `CardsContainer` above the catcher. Drag one pitch: scroll 0 -> 1, selection `GOLFIN` -> `PAR PERFECT`. Tap a non-focus card: `PAR PERFECT` -> `FYLOE SOFT`. |
| "You left unity loaded with the scaffolding" | I left LabScaffold open with fake state injected, and a capture helper's builder re-run baked three debug panels I had hidden into the saved scene. | Editor left on ShellScene, nothing dirty, `FakeStateLock` cleared, play mode exited. Scene rebuilt from a clean checkout. | `m_IsActive` parity vs HEAD: **zero flips**, `FocusHalo` the only added object. |

I also emptied Cesar's live bag mid-session by calling `ClubContext.Reset()` while assuming edit
mode. Restored by invoking both populators' `Refresh()` (7 clubs, 20 balls).

## iter-3 — Cesar's second look

Two more, both real, both from measuring the wrong thing in iter-2.

**"The selected halo is still bigger than the club/ball portrait and is not centered (clearly more
empty space at the bottom than at the top)."** iter-2 sized and pinned the halo to the card's
**RectTransform**. But `Button - All.png` is 153x248 with its opaque art at (4,0)-(148,239): 4px
padding left/right, **none at the top and 8px at the bottom**. Stretched into the 145x240 rect that
is 0 top / 7.74 bottom — so the DRAWN card is 137.4 x 232.3 and its centre sits ~4.3px ABOVE the
rect centre. Pinning to the rect therefore made the halo both too big (145x240 ring vs 137.4x232.3
art) and 4.3px low, which reads as extra space underneath. My iter-2 "measurement" checked the halo
against my assumption about the sprite, never against the card's drawn pixels.

Halo is now derived from the card ART: ring size = art size, centred on the art centre. Constants
`CardSpriteW/H` + `CardArtL/R/T/B` in the builder carry the measured bbox so re-exported art only
needs those updated. **Measured in real play: ring vs art size delta (0.00, 0.00), centre delta
(0.00, 0.00).**

**"There is a white line over the bottom arrow... since it's infinite now, remove it (and the top
one if there is one)."** That was the buffer card's white top edge showing through the mask. With
`_viewportMargin = 36` and a 34px card gap, the buffer card's edge sat exactly 2px inside the
viewport at BOTH ends. The margin is bounded on two sides — at least ~29.4px so the halo glow is not
sheared, and strictly under the 34px gap so no buffer card shows — so it is now **28**, with ~6px of
headroom each way. **Measured in real play: every one of the six pool cards reports
`sliverThroughMask=False`; the nearest buffer edge clears the viewport by 6px at both ends.**

Viewport height follows the margin: 4x240 + 3x34 + 2x28 = **1118**.

## The three guards (Cesar: "make sure you cannot fake screens in the future")

Built into `CaptureCore`, the single sanctioned capture path, so nothing routes around them:

1. **Filename.** A frame that is not real play is written as `NOT-REAL_<label>_<ts>.png`. The prefix
   travels into every path anyone pastes, including chat.
2. **Pixels.** Red diagonal hatching is burned into the frame. Verified live: an edit-mode capture
   came back with **22.9 %** of sampled pixels hatched. Renaming the file cannot launder it.
3. **Sidecar.** Every capture writes `<file>.png.json` with `realPlay`, `playing`,
   `fakeStateLocked`, `shotLayoutApplied`, `ballViewportY` and the loaded scenes.

Gated by **Rule 24** in `.claude/hooks/enforce_implementer_done.py`: a canonical screenshot whose
name says `NOT-REAL`, or whose sidecar says `realPlay:false`, blocks the review transition. A
missing sidecar warns rather than blocking, so pre-guard tasks are not failed retroactively.
Coverage: `TestCaptureProvenance` (6 tests, all passing).

**A bug I found in my own guard while testing it:** `CaptureCore` looked up
`Golfin.EditorTools.FakeStateLock`, but the type is `Golfin.Gameplay.UI.HUD.FakeStateLock` — so the
fake-state check silently never fired. Corrected and re-verified: with the lock set, the verdict is
`EDIT MODE - NOT REAL PLAY | FAKE STATE INJECTED | SHOT LAYOUT NOT APPLIED`. That is exactly the
three-reason verdict my iter-1 frame would now receive.

## Scaffolding shows the CURRENT shot UI (Cesar's follow-up)

`ShotLayoutController` gained a static `LayoutApplied` / `LastAppliedBallY`, set by `Apply` — the
existing `Active` is set in `OnEnable` and so is play-mode-only, which is why nothing could tell a
stale scaffolding frame from a live one. New `CaptureHelper.ApplyCurrentShotLayout()` (also a menu
item, `GOLFIN/Capture/Apply Current Shot Layout`) runs `Apply` against the open scene; every
`Fake State - *` preset and both `SelectorScreenshotHelper` captures now call it. A scaffolding
render therefore shows the current framing — and if it somehow does not, guard 3 stamps the frame
`SHOT LAYOUT NOT APPLIED - STALE AUTHORED FRAMING`.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselMath.cs` | **created** — pure ring math: `Mod`, `SlotY`, `EaseOutCubic`, `SnapDuration`, K11-aware `ResolveSnapTarget`. |
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCarouselDrag.cs` | **created** — modal-mode drag handlers + release-velocity ring buffer. |
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs` | **modified** — pool + `Layout` + `SnapTo` + halo bump + wrap/rubber-band; `Populate` deleted; **baseline now means the focus slot**, with `FocusSlotOffsetFromRootBottom()`. |
| `Assets/Scripts/Gameplay/UI/ShotUI/SelectorCardWidget.cs` | **modified** — `BoundItem` + cached `Rt`. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotLayoutController.cs` | **modified** — static `LayoutApplied` / `LastAppliedBallY` so a capture can prove its framing is current. |
| `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs` | **modified** — capture provenance: NOT-REAL rename, red hatching, sidecar JSON, on all five write sites. |
| `Assets/Scripts/Editor/CaptureHelper.cs` | **modified** — `ApplyCurrentShotLayout()`, called by every Fake State preset. |
| `Assets/Scripts/Editor/SelectorScreenshotHelper.cs` | **modified** — applies the current shot layout before both captures. |
| `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` | **modified** — viewport/mask/halo/drag helper, halo ring-fit sizing, baseline authoring, ball catcher sibling pin, `SelectorCard_Prefab` root-leak sweep. |
| `Assets/Scripts/Gameplay/Tests/SelectorCarouselMathTests.cs` | **created** — 21 EditMode tests. |
| `.claude/hooks/enforce_implementer_done.py` | **modified** — Rule 24, capture provenance gate. |
| `.claude/hooks/test_enforce_implementer_done.py` | **modified** — `TestCaptureProvenance`, 6 tests. |
| `Assets/Scenes/Physics/LabScaffold.unity` | **rebuilt + saved.** +12,853 / -20,982 (net -8,129 from the leak sweep). Zero active-state flips vs HEAD. |

## Screenshot

- **Canonical:** `screenshots/00_canonical_club_ring_realplay.png` (1170x2532) — REAL PLAY, Lomond
  hole 2, sidecar `realPlay: true`, `shotLayoutApplied: true`, `ballViewportY: -303.84`.
- **Ball side:** `screenshots/02_ball_selector_realplay.png` — same provenance.

## Acceptance checklist

| Item | Result | Justification (real play unless noted) |
|---|---|---|
| EditMode `SelectorCarouselMathTests` | **PASS** | 21/21. |
| `ClubSelectionGreenGateTests` green + unmodified | **PASS** | 8/8, file byte-identical to HEAD. |
| 4 cards + halo on the selected club | **PASS** | 6 active pool cards, 4 centres inside the viewport, halo on the focus card. |
| Focus card aligned with its trigger button | **PASS** | 170.00 vs 170.00, delta **+0.00 px**. |
| Halo centred and fitting the card | **PASS (iter-3)** | vs the card's DRAWN ART: ring 137.4x232.3 on art 137.4x232.3, size delta (0.00, 0.00), centre delta (0.00, 0.00). iter-2 measured against the RectTransform and was wrong. |
| No card sliver through the mask (white line over the chevrons) | **PASS (iter-3)** | all six pool cards `sliverThroughMask=False`; nearest buffer edge clears the viewport by 6px top and bottom. |
| Ball selector scrolls | **PASS** | drag one pitch: scroll 0->1, `GOLFIN` -> `PAR PERFECT`. |
| Ball selector selects by tap | **PASS** | non-focus card tap: `PAR PERFECT` -> `FYLOE SOFT`; overlay stays open while it glides. |
| Wrap | **PASS** | 20 balls / 7 clubs, slots continuous with no gap. |
| K11 off green: putter greyed, never in focus | **PASS (measured earlier + unit-tested)** | alpha 0.50 on the gated card; `ResolveSnapTarget` never returns it across a 49x7 sweep. |
| Selection commits at snap start | **PASS** | `SelectedIndex` changed on `endDrag`, before the tween finished. |
| Capture provenance guard | **PASS** | real frames `realPlay:true`; an edit-mode frame renamed `NOT-REAL_`, 22.9% hatched, sidecar `realPlay:false`. |
| Editor left clean | **PASS** | ShellScene, not dirty, single scene, play mode exited, `FakeStateLock` cleared. |
| Scene hygiene | **PASS** | zero `m_IsActive` flips vs HEAD; `FocusHalo` the only added object. |
| Fling >1 and <=3 | **PARTIAL** | unit-tested (±3 clamp); felt behaviour is Cesar's call. |
| Chevrons / hold mode / Profiler allocation | **NOT RUN** | `SelectorDragRouter.cs` byte-identical, but the hold-mode regression pass and the Profiler reading were not run. |

## Spec deviations

1. **`_arrowStepDur` (0.15 s) added as a serialized field** — SPEC §3 requires the chevron /
   auto-scroll step to be forced to `_arrowRepeatInterval`'s value via a `durOverride`, but that
   value lives on `SelectorDragRouter`, which the SPEC forbids editing, and the chevron `onClick`
   path has no router at all. A serialized mirror on the widget was the only way to honour the
   requirement without touching the router. It is Inspector-tunable and must be kept in step with
   the router's 0.15.
2. **`SetOpenBaselineY` now subtracts `_viewportMargin`, and the builder's authored y drops 28 → −8
   (ball widget: 96 → 60).** SPEC §1 says "the overlay root VLG absorbs the +36 by growing upward",
   which is not what a bottom-pivoted CSF root does — it grows upward from a *fixed bottom*, so the
   focus card would have ended up 36 px high. The explicit −36 shift is what actually delivers the
   SPEC's stated invariant. See § Geometry.
3. **Visible-card test is arithmetic, not `RectangleContainsScreenPoint`.** SPEC §2 suggests
   `RectTransformUtility.RectangleContainsScreenPoint(_cardsViewport, centreScreen)`. `Layout()`
   already knows every card's exact bottom-left-relative y, so the test is
   `0 ≤ slotY + cardHeight/2 ≤ viewport.rect.height` — the same predicate, exact, allocation-free,
   and it works in edit mode with no canvas camera (the screenshot helpers open the overlay outside
   play mode). Horizontal containment is trivially true: the card and the viewport are both 145 wide.
4. **`ResolveSnapTarget` returns a VIRTUAL index, which for putter-only mode is not literally `7`.**
   SPEC §Acceptance says "putter-only mode … returns 7 from any scroll/velocity". The function
   returns the ring coordinate to scroll *to*, whose item is `Mod(result, n)`; from scroll 0 with a
   downward fling that is `−1`, not `+7`, and it must be, or the ring would travel the long way
   round. The test asserts `Mod(result, 8) == 7` across the sweep **and** the literal `7` from a
   standing start. Documented in the class summary and the test summary.
5. **`Wrap` is `ItemCount >= _visibleSlots + 1`, not a hardcoded 5** — same number at the default
   `_visibleSlots = 4`, but it cannot drift if the slot count is ever retuned in the Inspector.
6. **Cards are rebound only when their item changes** (or after a data change sets `_forceRebind`).
   SPEC §2's pseudocode calls `BindCard` unconditionally every pass; doing that literally would
   break the SPEC's own 0 B/frame acceptance item. K11 `SetSelectable` still runs unconditionally.

7. **Fixed a pre-existing `SelectorCard_Prefab` root leak in the builder (out of spec, 5 lines).**
   `BuildCardPrefabGo` does `new GameObject("SelectorCard_Prefab")` as a SCENE ROOT, but the
   builder's `RemoveChild` sweep only clears canvas children — so every run since the file was
   written leaked one more orphaned card hierarchy into `LabScaffold.unity`. HEAD carried **14**.
   Left alone, this task's own builder re-run would have made it 15, and the capture helpers (which
   call the builder) add one each. Swept the roots too. Net effect on the scene diff: **-8,129
   lines**. Out of spec, but it is 5 lines in a file this task already edits and it removes bloat
   this task would otherwise add.
8. **The new test file needed a delete-and-restore to enter the compilation pipeline.** Writing a
   `.cs` next to a hand-authored `.meta` registered the guid without importing the script:
   `AssetPathToGUID` returned it, `LoadAssetAtPath<MonoScript>` returned null, and
   `CompilationPipeline` listed 41 of 42 files. `Refresh`, `ImportAsset(ForceUpdate)`, an asmdef
   reimport and `RequestScriptCompilation(CleanBuildCache)` all failed to fix it;
   `DeleteAsset` + restore + `ImportAsset` fixed it instantly. No code impact — recorded because it
   will recur for anyone hand-writing metas.

## Feel constants — MANUAL ON-DEVICE VERIFICATION REQUIRED

None of these can be judged from the Editor; all are Inspector-tunable on both
`SelectorOverlay` and `SelectorOverlay_Ball` with no code change.

| Field | Default | What it changes |
|---|---|---|
| `_snapBaseDur` | 0.12 | Fixed part of every snap |
| `_snapPerItemDur` | 0.06 | Added per slot travelled |
| `_snapMinDur` | 0.15 | Floor — a 1-slot nudge cannot feel instant |
| `_snapMaxDur` | 0.35 | Ceiling — a 3-item fling cannot feel sluggish |
| `_flingLookaheadSec` | 0.12 | How much a flick "throws" the ring |
| `_flingMaxItems` | 3 | Hard cap on one flick |
| `_haloBumpPeak` / `_haloBumpDur` | 1.06 / 0.10 | The settle bump on the halo |
| `_rubberBandItems` | 0.35 | Overscroll at the ends of a short (N < 5) stack |
| `_arrowStepDur` | 0.15 | Chevron / held-arrow glide |
| `SelectorCarouselDrag._velocityWindowSec` | 0.10 | Window the release velocity is measured over |

## Known FAIL / outstanding items

Nothing this task owns is FAILING. Outstanding:

1. **Play-mode interaction set NOT RUN** — finger slide (both directions, label-updates-mid-travel),
   fling feel, chevron glide, hold-mode regression on both triggers, modal tap on focus/non-focus
   card, ball overlay at n=1/4/5, and the Profiler allocation reading. All need play mode.
2. **`PendulumSchemeDriverTests.MarkerFreezes_AtTheUpswingReversal_NotAtRelease` is red** — proven
   above to pre-date this task (unmodified file, no selector reference in `Controls/`, last touched
   by `270eec9a4 miss_grade_duff`, identical failure across two runs with different selector code).
   Surfaced for Cesar; not this task's to fix.
3. **The N<=4 last-item-selected sparse stack** — see § Open question. Behaves exactly as the SPEC
   models it; flagged because it looks wrong in the club capture helper's 4-club fixture.
