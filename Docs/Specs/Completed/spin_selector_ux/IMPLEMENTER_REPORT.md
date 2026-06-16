# Implementer Report — `spin_selector_ux`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

> **Iteration 6** (CESAR_REJECTED #2 addressed — dim was square, white wash on ball, captures over LabScaffold).

---

## Rejection follow-up (Cesar, post-iter-5 ARCHITECT_REVIEW_PASS → iter-6)

### D-1 — Dim was a SQUARE box; must be CIRCULAR. — GONE

**Root cause:** `GenerateDonutTexture` in iter-5 only had an inner hole cutoff. Pixels between the inner hole and the image boundary (corners included) were all rendered dark at `darkAlpha=0.55`. The `_grayOutRt` is a 600×600 square Image — so the dim covered a 600×600 square with a circular hole punched in the center. The square corners of the Image were dark, making the dim look like a rectangular box.

**Fix (iter-6):** Added `outerRadiusFrac` parameter to `GenerateDonutTexture`. Pixels at distance `> outerRadiusFrac * texHalf` from center are `alpha=0` (clear). `outerRadiusFrac` is set to `visualFrac = 0.957` in `UpdateDiscVisuals()`. At 512px texture half-width = 256px, `outerRadiusPx = 0.957 × 256 ≈ 245px`. The texture corner distance is `256 × √2 ≈ 362px > 245px` — all four corners are fully transparent. Result: a true circular annulus — alpha=0 inside hole (spin-allowed), dark in the ring between hole and ball edge, alpha=0 outside the ball circle (no square box).

**Evidence (D-1 same-angle re-shoot per Rule 15):**
- `screenshots/spin_iter6_LOW_minus10_real_hole.png` (1170×2532) — at LOW, a visible circular dim ring appears with a small clear center. The dim terminates at the ball edge (circular). No square box silhouette. Background outside the ball is the real hole fairway (not darkened).
- `screenshots/spin_iter6_HIGH_plus10_real_hole.png` (1170×2532) — at HIGH, the hole and outer radius coincide (both at `visualFrac=0.957`), so the dim ring is near-zero width. The ball reads as the normal ball — essentially no dim visible. Correct per Cesar: "in the case of High, you would see the normal ball."

**Status: GONE**

### D-2 — White layer washing over ball; cut must show normal ball. — GONE

**Root cause:** In iter-5, `_activeDiscRt` (SpinActiveDisc) had a Knob sprite at `alpha=0.35`. This was a translucent white Image covering the ENTIRE disc interior (the spin-allowed region), including at HIGH spin where it covered the full ball. Result: a bright white wash over the ball that Cesar saw in every state.

**Fix (iter-6):** `UpdateDiscVisuals()` now sets `outlineImg.color = new Color(1f, 1f, 1f, 0f)` (alpha=0) on `_activeDiscRt`. The SpinActiveDisc Image is invisible. Edge delineation between the spin-allowed area and the dimmed area comes from the donut feather at the inner hole boundary (smooth alpha transition over ~4px).

**Evidence (D-2 same-angle re-shoot per Rule 15):**
- `screenshots/spin_iter6_HIGH_plus10_real_hole.png` — at HIGH the ball is visually the normal ball sprite: no white tint, no overlay. The ball texture is fully visible. The donut rim at `visualFrac=0.957` provides only a hair of feathered fade at the very outer edge.
- `screenshots/spin_iter6_LOW_minus10_real_hole.png` — inside the small clear center (the spin-allowed cut), the ball texture is clean and un-tinted. The dimmed ring outside is black at ~55% alpha.

**Status: GONE**

### D-3 — Captures taken in empty space (LabScaffold), must be a real loaded hole. — GONE

**Root cause:** iter-5 stills were captured while `LabScaffold.unity` was open as a standalone scene, NOT via the real game flow. The background showed flat gray ground/sky instead of a real hole environment.

**Fix (iter-6):** Re-shot both states (HIGH +10, LOW −10) via the real game flow:
1. ShellScene.unity opened (Single mode).
2. `HoleProgressionService.SetUnlockedOverride(1, true)` — Hole 1 unlocked.
3. `GameSession.SeedSession` called with hole=1.
4. `GameplaySceneLoader.Instance.BeginGameplayLoad(1)` — production coroutine: fade → additive LabScaffold host → additive `Hole_01_Geo`. Waited until `PhysicsLabController.IsHoleReady == true`.
5. SpinPanel opened via reflection (`BallContext.SelectedSpinStat = +10/-10`, `SpinPanelWidget.Open()` via reflection).
6. Captured via coroutine on existing `OutsideClickCatcher_Spin` MonoBehaviour using `CaptureCore.SnapAtEndOfFrameAndPause` with 5-frame settle wait at 1170×2532.

Both captures show **Hole 1 – Lomond PAR5** (trees, fairway, "HOLE 1 – REGULAR" HUD text visible). The real rendering context is present (grass, URP lighting, real hole geometry). No flat gray LabScaffold sky.

**Numerical confirmation:**
- HIGH capture: dim shape is circular (annulus visible at ball rim only), no square box, no white wash. Cut = whole ball. Scene: Hole 1.
- LOW capture: circular dim ring occupying most of ball, small central cut of clean ball visible. Cut radius confirmed as ≈ 57px (= 0.20 × 287.1px floor). Scene: Hole 1.

**Status: GONE**

---

## Rejection follow-up — prior iterations (carried forward for traceability)

### Post-iter-4 ARCHITECT_REVIEW_FAIL (un-dimmed donut hole spilled outside ball at HIGH)

Previously addressed in iter-5. Still GONE: `_grayOutRt` anchor fix (center-point, not stretch) + `BallSpriteVisualRadiusFrac=0.957` unchanged in iter-6.

### Post-iter-3 ARCHITECT_REVIEW_FAIL (_spinDot sprite null → square dot)

Previously addressed in iter-3. Still GONE: Knob sprite (`{fileID: 10913}`) assigned via `ActionButtonsBuilder`, unchanged in iter-6.

### Post-iter-2 (self-review gray-out inversion)

Previously addressed in iter-2. Still GONE: donut texture applied, unchanged in iter-6.

---

## Implementation summary

**Iter-6 changes only** (iter-1 through iter-5 changes are cumulative and unchanged):

1. **`SpinPanelWidget.cs`** — `GenerateDonutTexture` signature changed from 3 params to 4 (added `outerRadiusFrac`). Pixels at `dist > outerRadiusPx` are `alpha=0`. `UpdateDiscVisuals()` passes `outerRadiusFrac = visualFrac`. `_activeDiscRt` Image color set to `new Color(1,1,1,0)` (alpha=0, no white wash).

2. **`SpinSelectorMappingTests.cs`** — 4 new EditMode tests for circular dim geometry (§ `8. Circular dim geometry (iter-6 D-1)`): `CircularDim_OuterRadiusFrac_EqualsVisualFrac`, `CircularDim_AtHighSpin_DimRingWidthIsNearZero`, `CircularDim_AtLowSpin_DimRingWidthIsSignificant`, `CircularDim_Alpha_IsZeroOutsideBallRadius`. Total: 29 tests (was 25).

3. **Captures** — re-shot HIGH +10 and LOW −10 at 1170×2532 over Hole 1 (Lomond PAR5) via the real game flow.

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs` | **iter-6:** `GenerateDonutTexture` 4th param `outerRadiusFrac`; alpha=0 outside ball circle (D-1). `_activeDiscRt` Image `color.a = 0` (D-2). Also: iter-1–5 changes. |
| `Assets/Scripts/Gameplay/Tests/SpinSelectorMappingTests.cs` | **iter-6:** 4 new circular-dim geometry tests (D-1 invariants). Total 29 tests (was 25). Also: iter-1–5 tests. |
| `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` | **iter-5:** `BallSpriteVisualRadiusFrac = 0.957f`. **iter-1:** `SpinSelectorFloorRadius01`. Unchanged in iter-6. |
| `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` | **iter-5:** `BallSpriteVisualRadiusFrac` CSV case. **iter-1:** `SpinSelectorFloorRadius01` case. Unchanged in iter-6. |
| `Assets/Resources/Gameplay/controls.csv` | **iter-5:** `BallSpriteVisualRadiusFrac,0.957`. **iter-1:** `SpinSelectorFloorRadius01,0.20`. Unchanged in iter-6. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/BallContext.cs` | **iter-1:** `SelectedSpinStat` field + `Reset()`. Unchanged in iter-6. |
| `Assets/Scripts/UI/HUD/BallContextPopulator.cs` | **iter-1:** `BallContext.SelectedSpinStat = template.spin`. Unchanged in iter-6. |
| `Assets/Scripts/UI/HUD/LabInventoryStub.cs` | **iter-1:** `BallContext.SelectedSpinStat = template.spin` in stub path. Unchanged in iter-6. |
| `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` | **iter-1:** Replaced 5 discrete spin buttons with disc panel. **iter-3:** Fixed Knob sprite load. Unchanged in iter-6. |
| `Assets/Scenes/Physics/LabScaffold.unity` | **iter-1–3:** Rebuilt via `ActionButtonsBuilder`; all `[SerializeField]` refs wired. Unchanged in iter-6. |
| `Assets/Scripts/Gameplay/Tests/SpinSelectorMappingTests.cs.meta` | **iter-1:** Unity meta file. |
| `.claude/review_misses.log` | Pre-existing pipeline hook modification — updated by pipeline hook on ARCHITECT_REVIEW_FAIL/CESAR_REJECTED transitions. Not modified by this task's code. Cited in iter-4 baseline. |
| `Assets/Sounds/400 Sounds Pack/` (untracked) | Pre-existing Order-350 drift — cited in HEARTBEAT.log iter-1 baseline. |
| `Assets/Sounds/Hit/`, `Assets/Sounds/Land/`, `Assets/Sounds/Swing/` (untracked) | Pre-existing Order-350 drift — cited in HEARTBEAT.log iter-2 baseline. |
| `Docs/Specs/Completed/sound_effects/screenshots/` (untracked) | Pre-existing Order-350 drift — cited in HEARTBEAT.log iter-2 baseline. |

---

## Screenshot

- **Canonical screenshot:** `screenshots/spin_iter6_LOW_minus10_real_hole.png`
- **Dimensions:** 1170×2532 px (long edge 2532 ≥ 900 px — Rule 14 satisfied)
- **Capture method:** `CaptureCore.SnapAtEndOfFrameAndPause(label, null, skipPause: true)` via coroutine on `OutsideClickCatcher_Spin` MonoBehaviour, 5-frame settle wait. Path written to `Docs/Diagnostics/_capture/`, copied to task screenshots folder.
- **Scene loaded:** Hole 1 – Lomond PAR5 via real game flow (ShellScene → GameplaySceneLoader.BeginGameplayLoad(1))
- **Play mode:** Yes (`IsPlaying=true`, `IsPaused=false`)
- **When captured:** 2026-06-16 ~20:57 JST (iter-6)
- **Why LOW is canonical:** LOW shows the circular dim ring most clearly — the cut (central clear region at floor radius ≈ 57px) and the circular dark ring surrounding it are both visible. HIGH shows the correct "pristine ball" behavior but is mostly featureless. The LOW frame is the strongest evidence for D-1 (circular dim shape) and D-2 (clean cut).

Supporting frame (HIGH state): `screenshots/spin_iter6_HIGH_plus10_real_hole.png` (1170×2532) — shows normal-ball appearance at HIGH with no white wash, confirming D-2 fix and the correct behavior Cesar described: "in the case of High, you would see the normal ball."

Canonical screenshot: `screenshots/spin_iter6_LOW_minus10_real_hole.png`

---

## Figma fidelity

SPEC.md § Reference cites Figma file `5gEAHjl6xAtW8iYY7NMvWd`, node `2714:3471` ("Balls" central ball). Per D4, all other selector elements (disc, gray-out, dot) are intent-based with no Figma reference node.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Central ball ("Balls") | `2714:3471` | 100×100 at (487,1245); hidden while spin selector is open, restored on close; no restyle | `_centralBall.SetActive(false)` in `Open()`, `SetActive(true)` in `Close()`; verified at runtime: `activeSelf=False` during open. | PASS |
| Active spin disc | (none, D4) | circle centered on selector ball; visible radius = `radius01 × ball_visible_radius`; clearly delineated edge; HIGH disc must not exceed ball sprite (Cesar) | `_ballImageRadius=287.1px` (= 300 × 0.957). HIGH `_activePxRadius=287.1px` = visible ball sprite edge. LOW `_activePxRadius=57.42px` (floor 0.20×287.1). Disc visible in both captures. | PASS |
| Gray-out region | (none, D4) | everything outside active disc dimmed ~50%; non-selectable; must be CIRCULAR (Cesar D-1); cut (inside disc) = pristine ball (Cesar D-2) | **iter-6:** `GenerateDonutTexture` with `outerRadiusFrac=0.957` — alpha=0 outside ball circle (no square box). `_activeDiscRt` alpha=0 (no white wash). LOW capture shows circular dim ring with clean cut. HIGH capture shows normal ball (ring width ≈ 0). Both captured over real hole. | PASS |
| Red selection dot (`_spinDot`) | (none, D4) | 60×60, color `(1,0.2,0.2,1)`, circular sprite, clamped to active disc | 60×60 `SpinDot` Image, Knob sprite `{fileID: 10913}`. Corner-probe from iter-3: all 4 corners = background rgb, center = rgb(245,47,47). Knob unchanged in iter-6. | PASS |

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| 1a: opening the spin selector hides the central ball; closing restores it (state verified, not just asserted) | PASS | `Open()` calls `_centralBall.SetActive(false)`; `Close()` calls `SetActive(true)`. Runtime-confirmed via reflection during D-3 capture sequence: `activeSelf=False` during open. |
| 1b: the selected-spin dot renders as a red ROUND dot at the chosen point (screenshot) | PASS | Knob sprite `{fileID: 10913}` wired in scene. Corner-probe from iter-3: all 4 bbox corners background, center rgb(245,47,47). Unchanged in iter-6. Red round dot visible at ball center in both HIGH and LOW captures. |
| 1c: active disc radius visibly scales with the ball spin stat — LOW-spin ball (small disc) vs HIGH-spin ball (large disc), same scene | PASS | HIGH `_activePxRadius=287.1px` (fills entire ball), LOW `_activePxRadius=57.42px` (small center ring). Both captured over Hole 1 at 1170×2532: `screenshots/spin_iter6_LOW_minus10_real_hole.png` vs `screenshots/spin_iter6_HIGH_plus10_real_hole.png`. Visually distinct. |
| 1c: floor honored — a spin=-10 ball still has a usable (small) disc (`radius01 == floor`) | PASS | `Mathf.Lerp(0.20f, 1f, 0f) = 0.20f`. `_activePxRadius = 0.20 × 287.1 = 57.42px`. EditMode test `Radius01_AtSpinMinus10_EqualsFloor` passes. |
| 1c: dragging moves the dot continuously and is radially clamped to the active disc; disc edge → spin = ±1.0 (D3) | PASS | `ApplyDragPoint()` clamps `pxFromCenter` to `_activePxRadius`, then `SpinContext.SetSpin(pxFromCenter / _ballImageRadius)`. Disc-edge spin = `_activePxRadius / _ballImageRadius = radius01 ≤ 1.0`. D3 preserved. EditMode test `PxToValue_AtBallRadius_IsOne` passes. |
| 1c: region outside the active disc is grayed and non-selectable | PASS | Circular donut annulus (alpha=0 inside hole, alpha≈0.55 in ring, alpha=0 outside ball edge) applied to `_grayOutRt`. LOW capture confirms ring is visible and circular. Non-selectable: radial clamp prevents spin values beyond `_activePxRadius`. |
| D-1 CIRCULAR DIM: dim must be a circular annulus — no dim outside ball radius (no square box) | PASS | **iter-6 fix:** `GenerateDonutTexture` with `outerRadiusFrac = visualFrac = 0.957`. At texSize=512, outer radius=245px, corner dist=362px > 245px — corners are alpha=0. LOW capture shows circular dim ring with no square silhouette. EditMode test `CircularDim_Alpha_IsZeroOutsideBallRadius` passes. |
| D-2 CLEAN CUT: inside the cut (spin-allowed region) the ball must be pristine — zero overlay alpha | PASS | **iter-6 fix:** `_activeDiscRt` Image `color = new Color(1,1,1,0)` (alpha=0). No white overlay on ball interior. HIGH capture shows clean normal-ball appearance. LOW capture shows clean ball texture inside the small cut. |
| HIGH disc does not exceed ball sprite visual edge (Cesar rejection criterion, iter-4/5) | PASS | `_ballImageRadius=287.1px` = ball sprite visible radius (300 × 0.957). HIGH disc radius = 287.1px. Donut hole radius = 287.1px (= outer radius at HIGH). Both terminate at visible ball edge. |
| D3 honored: `git diff` shows ZERO changes under `Assets/Scripts/Physics/` and no change to `ShotInputBuilder`/`ShotController` spin math | PASS | `git status --porcelain` shows no path under `Assets/Scripts/Physics/`. All modifications are in `ShotUI/`, `Config/`, `Editor/`, `UI/HUD/`, `Resources/`, and `Scenes/`. Zero physics layer changes. |
| `BallContext.SelectedSpinStat` is populated in prod (`BallContextPopulator`) and lab (`LabInventoryStub`) | PASS | `BallContextPopulator.cs` sets `BallContext.SelectedSpinStat = template.spin`. `LabInventoryStub.cs` sets same. Unchanged from iter-1. |
| EditMode tests: radius01 mapping (floor at -10, 1.0 at +10, lerp at 0), radial clamp, px→value mapping, disc ≤ visible-ball guard, circular dim geometry (iter-6) | PASS | 29 NUnit tests in `SpinSelectorMappingTests.cs`. All 29 passed (0 failures) via `tests-run` (EditMode). 4 new iter-6 tests verify circular dim invariants: `CircularDim_OuterRadiusFrac_EqualsVisualFrac`, `CircularDim_AtHighSpin_DimRingWidthIsNearZero`, `CircularDim_AtLowSpin_DimRingWidthIsSignificant`, `CircularDim_Alpha_IsZeroOutsideBallRadius`. |
| No white-box placeholders visible in the screenshot | PASS | `_spinDot` shows Knob sprite (red round dot). `_ballImage` shows ball sprite. `_centralBall` hidden. `_grayOutRt` renders circular donut annulus. `_activeDiscRt` is invisible (alpha=0). No null-ref or placeholder textures. |
| All `[SerializeField]` references wired in the Inspector | PASS | `ActionButtonsBuilder` wires all `[SerializeField]`s via `SerializedObject`. Confirmed no null-ref errors at runtime. |
| Unity Console has no errors related to this task | PASS | Pre-existing stale `.meta` errors from `Assets/Scenes/Original/Rindo Course/` predating this task. No errors from `SpinPanelWidget`, `BallContext`, `SpinContext`, `ControlsConfig`, `ActionButtonsBuilder`, or `SpinSelectorMappingTests`. |
| Captures taken over real loaded hole (D-3, Cesar standing rule) | PASS | Both HIGH and LOW captures taken via ShellScene → `GameplaySceneLoader.BeginGameplayLoad(1)` over Hole 1 – Lomond PAR5 at 1170×2532. "HOLE 1 – REGULAR" HUD text visible in captures. NOT LabScaffold flat ground. |
| Spec deviations (if any) flagged at the bottom of the report with justification | PASS | See § Spec deviations. |

---

## Known FAIL items

None.

---

## Spec deviations

- **`MaxSpinPixelRadius=220f` constant removed — replaced by runtime-measured `_ballImageRadius`.** SPEC.md § Part C step 2 specifies `const float MaxSpinPixelRadius = 220f`. This constant was valid in the original design but produced Cesar's rejection (disc bigger than ball). The replacement computes `_ballImageRadius = RT_halfWidth × BallSpriteVisualRadiusFrac` from the actual displayed ball size. Physics contract D3 is unaffected: normalization divisor = cap value → disc edge = ±1.0.

- **Added `BallSpriteVisualRadiusFrac` constant to ControlsConfig (not in original SPEC).** Explicitly requested in ARCHITECT_REVIEW iter-4's fix direction. Tunable calibration constant, default 0.957.

- **SpinGrayOut anchors reset at runtime in Open() rather than authored in scene.** The stretch-anchor bug was in the prefab/scene. Anchors normalized in `Open()` before layout computation. Safe: `Open()` always runs before `UpdateDiscVisuals()`.

- **Gray-out uses a runtime-generated donut `Texture2D` instead of a Mask component or radial-cutout sprite.** Spec says "50% dark overlay / ring." Implemented as 512×512 RGBA32 circular-annulus texture with smooth feather. No shader, no Mask. Handles arbitrary disc radius and correctly clips to ball circle per iter-6 D-1 fix.

- **`SpinActiveDisc` kept as invisible overlay (alpha=0), not removed.** `_activeDiscRt` is still sized and positioned per `_activePxRadius` (so its `sizeDelta` tracks the disc edge), but its Image is alpha=0. The element is useful for future edge-delineation work without requiring scene rebuild.

- **`SelectPosition(int idx)` kept as `[Obsolete]` no-op stub.** Prevents compile errors from stale UnityEvent serialization.

- **`_spinDot` sprite is Unity built-in `Knob` rather than a new `Assets/Resources/...` PNG.** Functionally identical; no new art asset needed.

---

## Console output

Pre-existing errors (not introduced by this task):
```
Failed to import asset: Assets/Scenes/Original/Rindo Course/<name>.meta
(Multiple stale .meta file errors — pre-existing, cited in HEARTBEAT.log baseline)
```

No errors from `SpinPanelWidget`, `SpinContext`, `BallContext`, `ControlsConfig`, `ActionButtonsBuilder`, or `SpinSelectorMappingTests`.

---

## Open questions for Architect

None.
