# SPEC — `spin_selector_ux`

> Authoritative spec. Implementer reads this and ONLY this for the work definition.
> Notion Order **354** (P2, Gameplay Polish, Queued). Follow-up to spin CORE (Order 414, DONE).
> Sequenced BEFORE `fade_draw_aim_line_bend` (Order 355).

## Status
See `STATUS.md`. Currently `SPEC_READY`.

## Goal
Close the three UX gaps on the in-game spin selector so the player can read and set spin clearly. (1a) opening the selector hides the small central ball so it doesn't show through. (1b) the selected-spin red dot is actually visible as a round dot. (1c) NET-NEW — spin is selected continuously by dragging the dot inside a **disc** whose radius is sized by the equipped ball's `spin` stat (better ball = bigger reachable area), with the rest grayed out. The 414 physics is NOT touched: curve strength still falls out of the selected spin vector exactly as today; this task only changes how that vector is chosen and shown.

## Decisions locked with Cesar (do NOT relitigate)
- **D1 — region shape = CIRCLE (disc).** Spin magnitude = dot distance from center / max-reach; direction = angle. Not the legacy square cross.
- **D2 — stat→radius = small floor.** `radius01 = lerp(floor, 1.0, (spin+10)/20)` with `floor ≈ 0.20` (worst ball still dials ~±0.2). `floor` is a feel knob, CSV/inspector-tunable.
- **D3 — AREA-ONLY.** The spin stat bounds the selectable area only. It does NOT scale curve strength for a given dot position — that already follows from dot position through the existing 414 builder. ZERO physics/builder changes.
- **D4 — no Figma ref.** Selector visuals (active disc, gray-out, dot) are built from this intent, not extracted. The only Figma-anchored element touched is the central ball (hidden, not restyled).
- **D5 — sequenced before Order 355** (the aim-line bend). Do not touch the aim line in this task.

## Reference
- **Figma frame:** `In-Game - Shot Tests 8` / id `4065-15676` in file `5gEAHjl6xAtW8iYY7NMvWd`.
- **Central ball (1a target):** Figma node `2714:3471` "Balls", 100x100 at (487,1245). In code = `CentralBallWidget` (scene object in `Assets/Scenes/Physics/LabScaffold.unity`).
- **Reference PNG:** none for the selector (D4). Cesar to paste the open-state screenshot into the chat as the only visual anchor for the panel; selector visuals are otherwise implementer's craft against this prose.
- **Node renders dropped to `reference/`:** none — selector is intent-based per D4.

## Figma Fidelity (Rule 18)
Only one element here is Figma-anchored; the rest are intent-based per D4.

| Element | Figma node | Property -> value |
|---|---|---|
| Central ball ("Balls") | `2714:3471` | 100x100 at (487,1245); **HIDDEN while spin selector is open**, restored on close. No restyle. |
| Active spin disc | (none, D4) | circle centered on the selector ball; visible radius = `radius01 * MaxSpinPixelRadius`; clearly delineated edge |
| Gray-out region | (none, D4) | everything outside the active disc dimmed (~50% dark overlay) and non-selectable |
| Red selection dot (`_spinDot`) | (none, D4) | 60x60, color red `(1,0.2,0.2,1)` (unchanged), **circular sprite** assigned; sits at the chosen continuous point, clamped to the active disc |

## Architecture context
- **Asmdef boundaries:** all changes live in `Golfin.Gameplay.UI` (ShotUI) + the editor builder asmdef + `Golfin.Gameplay.Defaults`/config + CSV. NO `Golfin.Physics.*` change (D3).
- **Existing code referenced:**
  - `Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs` — the selector. `Open()` currently hides only `_aimingCone`; `_spinDot` is a `[SerializeField] RectTransform`; 5 hardcoded `_positions` (center + ±220px cardinals) / `_values` (±1 unit vectors); `SelectPosition(int)` is the discrete entry point.
  - `Assets/Scripts/Gameplay/UI/ShotUI/CentralBallWidget.cs` — the small ball (1a). Toggles via `ShotController.OnStateChanged`; shown in Idle/Aiming/Pulling/Timing/Flicking.
  - `Assets/Scripts/Gameplay/UI/ShotUI/HUD/SpinContext.cs` — `static Vector2 Spin` (per-axis clamp [-1,1]); `SetSpin(Vector2)`; `Reset()`. Consumed by `ShotConeView.PushSpinToPending` -> `ShotController.PendingSpinInput`.
  - `Assets/Scripts/Gameplay/UI/ShotUI/HUD/BallContext.cs` — selected-ball holder. Has id/name/sprites; **no stat fields** (add one here).
  - `Assets/Scripts/UI/Inventory/BallData.cs` — `BallDataRuntime.spin` (int, range -10..+10).
  - `Assets/Scripts/UI/HUD/BallContextPopulator.cs` — populates `BallContext` from the selected template (and `LabInventoryStub.cs` for lab scenes). **Set the new stat here.**
  - `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` — **wires the 5 invisible spin buttons over the ball** (the discrete input path). This is what gets replaced by a single drag surface.
- **Config:** `ControlsConfig.Default` + `Assets/Resources/Data/controls.csv` — add the floor knob, mirroring how Order 414 added `SpinMagScaleSlope`/`SpinMaxTiltRad`.

## Implementation

### Part A — 1a: hide the central ball on Open (trivial)
- Add `[SerializeField] private GameObject _centralBall;` to `SpinPanelWidget`. Wire to the `CentralBallWidget` GameObject in `LabScaffold.unity`.
- `Open()`: `if (_centralBall != null) _centralBall.SetActive(false);` next to the existing `_aimingCone` hide.
- `Close()`: `if (_centralBall != null) _centralBall.SetActive(true);` next to the existing `_aimingCone` restore.
- Note: the panel is modal (dim background blocks input) and the shot stays in Aiming while it's open, so `CentralBallWidget.HandleStateChanged` will not fire to re-show it mid-open. Acceptable; do not add state-coupling.

### Part B — 1b: red dot becomes a round dot (trivial)
- The `_spinDot` Image is already enabled, active (60x60), color red `(1,0.2,0.2,1)` — it only lacks a sprite (`m_Sprite: None`), so it renders as a hard square / not as a dot.
- Assign a **circular sprite** to the `_spinDot` Image. Reuse an existing round UI sprite if one exists; otherwise add a simple white filled-circle PNG under `Assets/Resources/...` (white so the red `m_Color` tints it). Optional thin darker outline for contrast on the ball.
- Keep size 60x60 and the red color. Under Part C the dot is positioned continuously (below), not snapped to 5 points.

### Part C — 1c: continuous, stat-bounded disc selection (the pipeline piece)
1. **Data path.** Add `public static int SelectedSpinStat = 0;` to `BallContext` (+ include in `Reset()`); raise existing `OnSelectedChanged`. Set it wherever the selected ball template resolves — `BallContextPopulator` (prod) and `LabInventoryStub` (lab) — `BallContext.SelectedSpinStat = template.spin;`. Confirm the template is in scope at that site during pre-flight.
2. **Radius mapping.** In `SpinPanelWidget`, add `const float MaxSpinPixelRadius = 220f;` (matches today's ±220 extreme = spin 1.0). Compute on `Open()`:
   `float radius01 = Mathf.Lerp(floor, 1f, (BallContext.SelectedSpinStat + 10f) / 20f);`
   where `floor = ControlsConfig.Default.SpinSelectorFloorRadius01` (default `0.20f`, from `controls.csv`). Cache `float activePxRadius = radius01 * MaxSpinPixelRadius;`.
3. **Active disc + gray-out visuals.** Show a circular "active zone" indicator sized to `activePxRadius` (diameter `2*activePxRadius`), centered on the ball. Dim everything outside it (~50% dark overlay / ring). Intent-based — implementer's craft; the requirement is the active disc is clearly readable and its size visibly differs between a low-spin and a high-spin ball.
4. **Continuous input (replaces the 5 buttons).** Replace the discrete buttons (currently built by `ActionButtonsBuilder`) with a single drag surface over the ball that implements `IPointerDownHandler`/`IDragHandler`:
   - local point -> vector `pxFromCenter` (px) from disc center.
   - `if (pxFromCenter.magnitude > activePxRadius) pxFromCenter = pxFromCenter.normalized * activePxRadius;` (radial clamp to the active disc — D1/D2).
   - `_spinDot.anchoredPosition = pxFromCenter;`
   - `Vector2 value = pxFromCenter / MaxSpinPixelRadius;` -> `SpinContext.SetSpin(value);` (so a low-stat ball maxes at `radius01`, a full-stat ball reaches ±1).
   - The implementer may keep a tap-to-center affordance (drag to center = zero spin). Keep `SpinContext`'s per-axis clamp as a safety net only.
5. **Restore on Open.** `SnapDotToCurrent()` becomes: place the dot at `SpinContext.Spin * MaxSpinPixelRadius`, radially clamped to `activePxRadius` (in case a stronger ball's prior selection exceeds a weaker ball's disc).
6. **Remove the discrete path.** Retire `_positions`/`_values`/`SelectPosition(int)` (or keep `SelectPosition` only if other callers exist — grep first). Update `ActionButtonsBuilder` so it no longer wires 5 spin buttons; it wires the single drag surface instead.

## Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)
- [ ] 1a: opening the spin selector hides the central ball; closing restores it (state verified, not just asserted).
- [ ] 1b: the selected-spin dot renders as a red ROUND dot at the chosen point (screenshot).
- [ ] 1c: active disc radius visibly scales with the ball spin stat — capture a LOW-spin ball (small disc) vs a HIGH-spin ball (large disc), same scene.
- [ ] 1c: floor honored — a spin=-10 ball still has a usable (small) disc (`radius01 == floor`).
- [ ] 1c: dragging moves the dot continuously and is radially clamped to the active disc; `SpinContext.Spin == pxFromCenter / 220` within tolerance.
- [ ] 1c: region outside the active disc is grayed and non-selectable.
- [ ] D3 honored: `git diff` shows ZERO changes under `Assets/Scripts/Physics/` and no change to `ShotInputBuilder`/`ShotController` spin math.
- [ ] `BallContext.SelectedSpinStat` is populated in prod (`BallContextPopulator`) and lab (`LabInventoryStub`).
- [ ] EditMode tests: radius01 mapping (floor at -10, 1.0 at +10, lerp at 0), radial clamp, px->value mapping.
- [ ] No white-box placeholders visible in the screenshot.
- [ ] All `[SerializeField]` references wired in the Inspector (`_centralBall`, `_spinDot`, disc visuals).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## Files / hierarchy this task touches
- `Assets/Scripts/Gameplay/UI/ShotUI/SpinPanelWidget.cs` — 1a hide/restore; 1c radius calc, drag input, snap-restore; retire discrete points.
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/BallContext.cs` — add `SelectedSpinStat` (+ Reset).
- `Assets/Scripts/UI/HUD/BallContextPopulator.cs` (+ `Assets/Scripts/UI/HUD/LabInventoryStub.cs`) — set `SelectedSpinStat = template.spin`.
- `Assets/Scripts/Editor/CanvasScalerMigration/ActionButtonsBuilder.cs` — replace 5 spin buttons with one drag surface.
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` (or wherever `Default` lives) + `Assets/Resources/Data/controls.csv` — add `SpinSelectorFloorRadius01 = 0.20f`.
- `Assets/Scenes/Physics/LabScaffold.unity` — wire `_centralBall`; assign circular sprite to `_spinDot`; add active-disc/gray-out visuals under the spin panel.
- New circular sprite asset under `Assets/Resources/...` (if no existing round sprite is reused).
- New EditMode test file (e.g., `SpinSelectorMappingTests.cs`).

## Smoke evidence
### Visual-fidelity verification (Lesson O)
This is a visual task — dispatch logs alone are insufficient. Provide BOTH:
- **Human-in-the-loop play-and-confirm:** load `LabScaffold`, open the spin selector with a low-spin ball then a high-spin ball, drag the dot, confirm in `IMPLEMENTER_REPORT.md` what was seen (disc sizes differ, dot follows + clamps, central ball hidden while open). Full-size capture per `feedback_record_bot_video_full_size` (1170x2532).
- **EditMode tests** for the pure math (radius mapping, radial clamp, px->value) so the numeric contract is regression-covered independent of the visual.

## Out of scope (do NOT do these)
- The fade/draw aim-LINE bend — that is Order 355 (`fade_draw_aim_line_bend`), specced separately and sequenced after this.
- ANY change to 414 physics: `ShotInputBuilder`, `ShotController` spin math, `BallSimulation`, the Magnus/orbital-tilt model (D3).
- Putt spin (putts force zero spin — unchanged).
- The cone (`ConeMeshGraphic`) — it is the club/power zone, untouched here.
- `map_view_aiming` (Order 352, separate top-down screen).
