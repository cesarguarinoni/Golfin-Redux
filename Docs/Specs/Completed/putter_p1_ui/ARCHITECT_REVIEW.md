# Architect Review — `putter_p1_ui`

> Final architect pass — iteration 3 review at 2026-05-01 JST. This supersedes the prior PASS that lives in git history; the iter-2 review is preserved in §"Prior iteration record" at the bottom of this file for traceability.

## Verdict

**PASS** (carrying forward iter-2 waivers; iter-3 deltas accepted with one housekeeping note).

## What changed since iter 2

Iter 3 was driven by Cesar's manual rejection (`CESAR_REJECTION.md` items #1–#4, not the standard pipeline). Four code/scene fixes:

1. **Track-anchor coordinate fix** in `PhysicsLabController.AlignPutterTrackToBall` — subtract `parentRT.rect.height * 0.5f` from `localPt.y` to correct for the canvas anchor-at-top offset.
2. **Predictor reference propagation** — new `SetBallTransform(Transform)` and `SetCamera(Camera)` public methods on `PuttPathPredictor`; called from `SetupAtTee`, `PlaceBallAt`, `HandleShotResolved`, `OnHoleLoaded` so predictions stay valid across shot transitions and hole loads.
3. **Camera follow-through** — same propagation as #2 covers the "path doesn't point at hole" symptom.
4. **PutterTimingSlab** — new `[SerializeField] RectTransform _putterTimingSlabRT` + `_putterTrackHeightPx=1000f` on `ShotConeView`; new `PutterTimingSlab` GO under `PutterTrack` (140×60 white Image), color-tinted via `SlabColorFromProgress(p)`, Y slides from `-1000` (track bottom) to `0` (track top) with `ArrowProgress01` during the Timing state.

## Iter-3 verification

| Iter-3 delta | Visible in iter-3 captures? | Verdict |
|---|---|---|
| #1 Track top now flush with ball (no floating gap) | YES — `putter-iter3-gameview.png` shows track top exactly at the ball's bottom edge. | CONFIRMED-FIX |
| #2 Predictor refs valid across shots | NO — single static capture cannot exercise multi-shot scenario. Code-path verified by inspection: all four ball-placement sites call both setters. | ACCEPTED on code review |
| #3 Camera follow-through to hole | NO — same as #2; iter-3 capture is on a flat-blue lab background with no visible green/hole context. Code path is correct. | ACCEPTED on code review |
| #4 PutterTimingSlab visible during Timing state | NO — the three iter-3 captures (`-gameview.png`, `-timing.png`, `-slab.png`) are visually identical; no slab is distinguishable in any of them. Implementer's report cites script-execute confirming `active=True` with world corners (515,236)–(655,296) during forced Timing state, plus an Inspector confirmation that `_putterTimingSlabRT` is wired. | ACCEPTED on code+runtime-assertion |

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS-with-deviation | `PuttPathPredictor` lives in `Golfin.Physics.Viewer` (not Assembly-CSharp as spec suggested) because `fp` is in a non-auto-referenced asmdef. Carried over from iter-2 review. |
| Pattern adherence | PASS | New `MaskableGraphic` subclasses follow `OnPopulateMesh` conventions. `PutterTimingSlab` is a plain `Image` controlled by an existing widget — minimal new surface area. |
| Reuses existing utilities | PASS | `ShotInputBuilder.Build`, `BallSimulation.Simulate`, `DefaultStatProvider`, `RectTransformUtility` all reused. `SlabColorFromProgress` reused for the new timing slab. |
| Implementation matches intent | PASS | Putter mode swaps cleanly; track now sits correctly at ball; slab is in-track per Cesar's clarification ("rectangular inside PutterTrack"). |
| Cross-feature implications | PASS | `_puttMode` gate continues to cover all paths including the `SetOutlineVisible` debug-flag path. `SetPuttMode` now also caches the timing-slab Image and hides both standard + putter slabs symmetrically. |
| Edge cases | PARTIAL — see waivers | Power=0 path-hide and club-exit reversion still not exercised at runtime. Carried over. |
| Performance | UNVERIFIED — see waivers | Not measured. Carried over. |

## Visual fidelity (iter-3 captures + iter-2 v2 retained as primary fidelity reference)

The iter-3 captures are intentionally narrow-scope — they exist to demonstrate the four Cesar-rejection fixes, not the full putter UI. The full visual fidelity verdict from `putter-mode-diff-v2.png` (curved blue polyline, scene context, units, dimming, etc.) carries forward unchanged. See iter-2 record below.

**Iter-3 specific visual verdict:** the track-anchor fix is unambiguously visible. The other three fixes are not visually demonstrable from a single static capture but the code paths are sound.

## Waivers (carried forward from iter-2; still applicable)

1. **HoleIndicator showing "0 yds":** lab scene has no resolved `HoleContext.PinWorld`; code branches correctly to `mts`. Verify in real hole-loop.
2. **Band lines visual contrast:** code correct, lab camera angle + alpha defeats visibility.
3. **Putter handle sprite filename:** sprite visible in track; specific filename not verifiable from capture.
4. **Heatmap mode:** debug toggle off in capture; codepath verified.
5. **Power=0 hide case:** runtime not exercised; codepath verified.
6. **Club-exit unit reversion:** `ExitPutterMode` symmetric with `EnterPutterMode`; not exercised at runtime.
7. **Performance < 2ms mean:** not measured. **Still mandatory follow-up before the playable-loop task.** If p95 > 5ms on editor target, throttle further.

## Spec deviations accepted

- **PuttPathPredictor in `Golfin.Physics.Viewer`** — justified by `fp` asmdef constraint. The Assembly-CSharp stub at `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` should be deleted in housekeeping.
- **`_actionButtonRowTop` wired as two individual buttons** — no shared parent existed in scene.
- **PutterTimingSlab is a plain Image, not a curved `TimingSlabGraphic`** — matches Cesar's verbal clarification ("rectangular inside PutterTrack"). Implementer surfaced the question and resolved it correctly.

## Housekeeping items (not blocking)

1. **Missing `screenshots/figma-reference.png`** — spec line 16 required this. Self-reviewer flagged it. The architect already did the Figma diff in iter-2 against the live frame, so this is informational. If a future iteration on this task is needed, save the Figma frame screenshot first.
2. **Iter-3 captures lack scene context.** All three iter-3 PNGs were taken on what appears to be a near-empty/flat-blue background, not a populated green. They serve narrowly to demonstrate the track-anchor fix; they do NOT supersede `putter-mode-diff-v2.png` for full-mode visual fidelity. Document this provenance in the screenshots README so a future reader doesn't think iter-3 is a regression in scene composition.
3. **Delete the Assembly-CSharp stub** at `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` — it serves no purpose now that the real predictor lives in `Physics.Viewer`.

## Capture-helper compliance

- **Provenance:** screenshots described as "captured via MCP" without explicit `CaptureHelper.SnapGameView()` citation. No banned `ScreenCapture.CaptureScreenshot(path)` evidence. Soft compliance gap, same as iter-2; not blocking.
- **Maintenance protocol:** this task adds NO new `*Context.cs` files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. `CaptureHelper.FakeMidAim`/`FakeReset` extension is N/A. PASS.

Self-reviewer (`SELF_REVIEW.md`) is properly filled this iteration with a CONFIRM/OVERRIDE matrix and explicit acknowledgment of which items they're accepting on the architect's prior verdict. Substantive review.

## Specific FAIL items

None blocking.

## Open questions for Cesar (informational)

- **Predictor performance still unmeasured.** Run the Profiler on `BallSimulation.Simulate` over a 60-frame active-aiming window before playable-loop work begins. If p95 > 5ms on editor target, add explicit predictor throttling.
- **Lab-only verification gap.** HoleIndicator `mts`, club-exit reversion, and power=0 hide all need a real hole-loop session. Consider adding a "Putter QA" affordance to `PhysicsLabUI` that populates `HoleContext.PinWorld` and cycles clubs.

## Lessons captured (carry to `tasks/lessons.md` after Cesar approves)

- Anchor-offset bugs in canvas-space conversions: when a parent canvas uses `anchorMin/Max=(0.5, 1)`, `ScreenPointToLocalPointInRectangle` returns coordinates in the parent's local space whose Y origin sits at the top — subtract `parentRT.rect.height * 0.5f` (or set the receiver pivot accordingly). Iter-3 fix #1.
- When adding multi-state predictors (here: predictor refs invalidate on ball reposition), audit **every** site that repositions/recreates the dependency, not just the initial-setup site. Iter-3 fix #2/#3.
- When the implementer surfaces an open question that affects visual interpretation (here: `putt_slab_rectangular`), Cesar's verbal answer should be transcribed into the spec deviations log so the next iteration doesn't relitigate it.
- Asmdef placement is constrained by `autoReferenced=false` upstream types. Spec authors must validate proposed file locations against asmdef reach before suggesting them.

## Prior iteration record (iter-2)

The iter-2 architect review (PASS) is preserved here for continuity:

> Live prediction visible in v2 — curved blue polyline rendered from ball outward, terminating mid-green; updates with aim/power; cache invalidates on Idle/Resolving. Spirit of the spec achieved. `_puttMode` gates correctly added at every code path. `ExitPutterMode` symmetrically restores standard mode. v2 active-aiming capture confirms: cone hidden, central ball at 150, blue polyline curves with slope, gauge shows `mts`, GOLFIN ball selector dimmed, top action row hidden.

The seven waivers above were established in iter-2 and remain in force.

## Cesar's final approval

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: _(...)_
