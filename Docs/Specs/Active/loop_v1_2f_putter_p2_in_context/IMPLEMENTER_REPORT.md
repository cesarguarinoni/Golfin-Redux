# Implementer Report — `loop_v1_2f_putter_p2_in_context`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

> **Iteration:** 4 (re-run after iter-3 self-review ESCALATE — fixes S6 mechanical fire bug: ShotWait=25s insufficient for tuned putt, BallAnimator.PlayRate now set to Instant for S5/S6)

## Implementation summary

### Iter-4 targeted fix (S6 fire bug):

**Root cause (iter-3):** `putt_flat_3m` with PuttRR=0.05 produces a ~39-second roll animation (exponential decay: `v(t)=0.35·exp(-0.05t)`, stops at v<0.05). The smoke runner's `ShotWait=25s` ceiling fired first, so `OnShotComplete6` never fired. The ball was at `BALL:Flying` state at the 1.5s capture point (animation still in progress).

**Fix (iter-4):** Set `BallAnimator.PlayRate = float.MaxValue` (Instant mode) before S5/S6 fire. Instant mode calls `SnapToEnd()` synchronously inside `Play()`, teleporting the ball to its final rest position in the same frame. `OnTrajectoryComputed` then primes `_prevAnimatorPlaying=true`; the very next `Tick(false)` call fires the falling edge → `DrainPendingTransitions` → `OnShotComplete`. S5 and S6 now complete in ~0.01s each. `PlayRate` is restored to 1.0 after S6.

**Additional guard:** Explicit `BallSM.State == Aiming` gate loop before each fire (belt+suspenders; confirmed 0.000s elapsed meaning state was already correct).

**Accessor methods added to `PhysicsLabController`:** `GetBallAnimatorPlayRate()` and `SetBallAnimatorPlayRate(float)` — `internal` scope, same assembly as `SmokeRunner2fHost`.

**Results (iter-4 run 2026-05-13 17:27 JST):**
- S5 baseline (PuttRR=0.1000): rolled **2.733m** — `OnShotComplete5 fired terminal=AtRest endSurface=Green` in 0.009s
- S6 tuned (PuttRR=0.0500): rolled **5.055m** — `OnShotComplete6 fired terminal=AtRest endSurface=Green` in 0.011s
- **Delta: +2.322m** — "tuned rolls FARTHER — L9 Option B working"

### Iter-3 targeted fix (FAIL-2 / OQ-1):

The only change from iter-2 is in `GreenTuningPanel.cs`: `OnRollingResistanceChanged`, `OnStopSpeedChanged`, and `ResetToDefault` now mirror Green edits to **both** `SurfaceConfig[Green]` AND `PuttConfig[Green]` via `controller.PuttCfg` + `controller.SetPuttConfig(puttCfg)`. This follows the pattern in `DashboardUI.AddPuttSliders` / `DashboardUI.SetPutt`. `SmokeRunner2fHost.cs` updated to log `PuttRR` values and reset PuttConfig in the S5 baseline step.

**Evidence of fix:** log entry `[SmokeRunner2fHost][SurfaceCfgLog] AfterSlider: SurfRR=0.0500 SS=0.0500 PuttRR=0.0500 (L9 Option B — should=0.0500)` confirms both configs updated. Reset log `AfterReset: SurfRR=0.1200 PuttRR=0.1000` confirms reset restores both to correct defaults. S5 baseline putt rolled `2.733m` (vs iter-2's `0.598m`) — PuttConfig is now the physics driver.

### All prior fixes from iter-2 remain:

- `PutterModeSurfaceController.cs` — static `DecideTargetClub(...)→int` helper (pure logic, test seam)
- `PhysicsLabController.cs` — `PutterIndex` const, `_lastNonPutterClubIndex` field, `SetClub` interception, `HandleShotComplete` AtRest branch auto-switch with camera-skip for willFlipToPutter
- `GreenTuningPanel.cs` — two-slider widget + L9 Option B mirror (iter-3 addition)
- `LabInventoryStub.cs` — added `ClubSelectionBroadcast.OnClubChanged` mirror
- `SmokeRunner2fHost.cs` — S5/S6 comparison + L9 Option B logging (iter-3 update)
- `SmokeRunner2fMenu.cs` — editor menu with stale-scene cleanup
- `PutterModeSurfaceControllerTests.cs` — 6 EditMode tests
- `LabScaffold.unity` — GreenTuningPanel hierarchy wired via Unity MCP

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs` | Modified (iter-4) — S5/S6: set BallAnimator.PlayRate=Instant before fire, state gate before each shot, InstantShotWait=5s, restore PlayRate after S6 |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Modified (iter-4) — added `GetBallAnimatorPlayRate()` + `SetBallAnimatorPlayRate(float)` internal accessors for smoke runner |
| `Assets/Scripts/Physics/Viewer/PutterModeSurfaceController.cs` | Created — static `DecideTargetClub(...)` helper |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Modified — PutterIndex const, _lastNonPutterClubIndex, SetClub update, HandleShotComplete AtRest branch |
| `Assets/Scripts/Physics/Viewer/GreenTuningPanel.cs` | Modified (iter-3) — L9 Option B: OnRollingResistanceChanged/OnStopSpeedChanged/ResetToDefault mirror to both SurfaceConfig AND PuttConfig |
| `Assets/Scripts/Physics/Viewer/SmokeRunner2fHost.cs` | Modified (iter-3) — S5/S6 mirror PuttConfig reset, log PuttRR values, pass puttRRAfterSlider to WriteHistoryLog |
| `Assets/Scripts/Physics/Viewer/Editor/SmokeRunner2fMenu.cs` | Created — Editor menu with stale-scene cleanup |
| `Assets/Scripts/Physics/Tests/PutterModeSurfaceControllerTests.cs` | Created — 6 EditMode tests |
| `Assets/Scripts/UI/HUD/LabInventoryStub.cs` | Modified — added ClubSelectionBroadcast mirror so ClubButtonWidget refreshes on auto-switch |
| `Assets/Scenes/LabScaffold.unity` | Modified — GreenTuningPanel hierarchy under Canvas |

## Screenshot

**Canonical screenshots (iter-2 run-12, 2026-05-13 15:45 JST — S1/S2/S3/S4 still canonical; already PASS in architect review):**
- `screenshots/controls_2f_auto_enter_putter_on_green_2026-05-13_15-45-22.png` (S1 — CAM:GroundLevel + PUTTER label — PASS from iter-2)
- `screenshots/controls_2f_auto_exit_to_last_club_2026-05-13_15-45-34.png` (S2 — CAM:Chase + DRIVER label — PASS from iter-2)
- `screenshots/controls_2f_tuning_panel_open_2026-05-13_15-45-36.png` (S3 — PASS from iter-2)
- `screenshots/controls_2f_tuning_live_apply_2026-05-13_15-45-37.png` (S4 — slider moved to 0.05 — PASS from iter-2)

**New iter-4 captures (2026-05-13 17:27 JST — S5/S6 re-run with Instant PlayRate fix):**
- `screenshots/controls_2f_tuning_putt_baseline_atrest_2026-05-13_17-27-26.png` (S5 — baseline putt 2.733m, OnShotComplete5 fired in 0.009s — PASS)
- `screenshots/controls_2f_tuning_putt_fast_atrest_2026-05-13_17-27-28.png` (S6 — tuned putt 5.055m, delta +2.322m, OnShotComplete6 fired in 0.011s — PASS)
- `screenshots/controls_2f_history_log.txt` (L1 — updated with iter-4 delta; PASS)

**Scene loaded:** `LabScaffold` + `Hole_01_Geo` (additive)
**Play mode:** Yes (live coroutine, CaptureCore.SnapPlayModeSafe)

## Visual Verification (Lesson O)

### Capture 1 — `controls_2f_auto_enter_putter_on_green_2026-05-13_15-45-22.png`
- **Status bar (top):** "CAM: GroundLevel BALL: Aiming" — FAIL-3 confirmed RESOLVED from iter-2. `EnterPutterMode()` calls `chaseCamera.SetMode(GroundLevel)` as first statement.
- **Club button (bottom-right):** "PUTTER 27 mts" — ClubButtonWidget shows PUTTER after auto-enter fired
- **Scene contents:** Ball on green surface near pin (flagstick visible). Ground-level perspective with green visible, trees in background. Putt track (faint orange line) visible extending from ball toward pin.
- **Turn counter:** Turn 2 (correct)
- **Log confirmation:** `[§2f] EnterPutterMode at frame N — ChaseCamera.SetMode(GroundLevel) called`

### Capture 2 — `controls_2f_auto_exit_to_last_club_2026-05-13_15-45-34.png`
- **Status bar (top):** "CAM: Chase BALL: Aiming" — camera reverted to Chase on exit from putter
- **Club button (bottom-right):** "DRIVER 250 yds" — auto-exit reverted to Driver (index 0)
- **Scene contents:** Ball in sand bunker area. Driver/Spin/Straight/Golfin action buttons visible (non-putter UI). Trees and rough terrain visible. Normal chase-camera height.
- **Turn counter:** Turn 3 (correct)
- **Log confirmation:** `Shot2 done: club=0 endSurface=Sand`

### Capture 3 — `controls_2f_tuning_panel_open_2026-05-13_15-45-36.png`
- **Status bar:** "CAM: Chase BALL: Aiming"
- **Top-right:** GreenTuningPanel expanded — "GREEN TUNING" header, two slider tracks visible (Roll Resist near left at ~0.12, Stop Speed), red Reset button visible.
- **Club button:** "DRIVER 250 yds"
- **Starting values:** RollingResistance = 0.1200, StopSpeed = 0.0500

### Capture 4 — `controls_2f_tuning_live_apply_2026-05-13_15-45-37.png`
- **FAIL-1 RESOLVED (from iter-2):** S4 step calls `rrSlider.value = 0.05f` (slider.value API fires onValueChanged), then invokes `OnRollingResistanceChanged(0.05)` via reflection.
- **Visual:** Panel open. Slider thumb has moved from ~0.12 position to ~0.05 position (leftward).
- **L9 Option B confirmed:** Log: `SurfRR=0.0500 SS=0.0500 PuttRR=0.0500 (L9 Option B — should=0.0500)` — both configs updated.

### Capture 5 — `controls_2f_tuning_putt_baseline_atrest_2026-05-13_17-27-26.png` (iter-4, FIXED)
- **Status bar (top):** "CAM: GroundLevel BALL: Aiming" — ball is at rest after baseline putt, in putter mode.
- **Scene contents:** Ball visible on green surface in lower portion of frame (GroundLevel perspective). Orange putt track extends from ball toward pin. Flagstick partially visible at top of frame. Trees/bunker area visible in background.
- **Turn 4.** Ball position: start=(-228.00,-73.00) end=(-225.27,-73.00), **rolled 2.733m** (confirmed by `OnShotComplete5 fired terminal=AtRest endSurface=Green` in 0.009s).
- **L9 Option B evidence:** PuttRR=0.1000 (default) was set before S5 via explicit reset of both SurfaceConfig and PuttConfig. The 2.733m roll (vs iter-2's 0.598m) proves PuttConfig is now driving putt physics.
- **Club button (bottom-right):** "PUTTER 27 mts"

### Capture 6 — `controls_2f_tuning_putt_fast_atrest_2026-05-13_17-27-28.png` (iter-4, FIXED)
- **Status bar (top):** "CAM: GroundLevel BALL: Aiming" — ball is at rest after tuned putt, in putter mode.
- **Scene contents:** Ball visible on green surface in upper-middle portion of frame — clearly FARTHER from the camera/aim-origin than S5. Trees and bunker visible in upper background. Putt track extends from aim-origin (off-bottom of frame) to ball position.
- **Turn 5.** Ball position: start=(-228.00,-73.00) end=(-222.95,-73.00), **rolled 5.055m** (confirmed by `OnShotComplete6 fired terminal=AtRest endSurface=Green` in 0.011s).
- **Delta vs S5: +2.322m.** Ball in S6 is visibly positioned further from the aim-origin than S5, consistent with lower rolling resistance (0.05 vs 0.10).
- **Club button (bottom-right):** "PUTTER 27 mts"

### Artifact — `controls_2f_history_log.txt` (iter-4 FIXED)
- `L9 Option B` header confirms the design intent
- `SurfaceConfig[Green].RollingResistance=0.0500` after slider → L9 mirror working
- `PuttConfig[Green].RollingResistance=0.0500` after slider → L9 mirror working (should=0.0500)
- `SurfaceConfig[Green].RollingResistance=0.1200` after Reset → SurfaceConfig default restored
- `PuttConfig[Green].RollingResistance=0.1000` after Reset → PuttConfig default restored
- **Roll distance comparison: S5=2.733m (baseline PuttRR=0.1000), S6=5.055m (tuned PuttRR=0.0500), Delta=+2.322m** — "tuned rolls FARTHER — L9 Option B working"

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `PutterModeSurfaceController.cs` shipped with `DecideTargetClub(...)→int` | PASS | File exists at `Assets/Scripts/Physics/Viewer/PutterModeSurfaceController.cs`, API matches spec exactly |
| `PhysicsLabController` extended: `PutterIndex` const | PASS | `public static readonly int PutterIndex = LabClubs.Length - 1;` added near LabClubs declaration |
| `PhysicsLabController` extended: `_lastNonPutterClubIndex` tracked | PASS | Field initialized in Awake after configs loaded, updated in SetClub when index != PutterIndex |
| AtRest branch runs auto-switch BEFORE §2e pin-rotation | PASS | `PutterModeSurfaceController.DecideTargetClub` called first in `case BallState.AtRest:` block before pin-aim path |
| willFlipToPutter path skips `ApplyCameraYaw` | PASS | Early break after SetClub+CompleteShot+ReArm when willFlipToPutter=true |
| `GreenTuningPanel.cs` shipped with two sliders + reset + gear-toggle | PASS | File exists; two sliders (RollingResistance 0–0.5, StopSpeed 0–0.2) + toggleButton + resetButton wired in Awake |
| Live-apply via `SetSurfaceConfig` on slider change | PASS | `OnRollingResistanceChanged` calls both `controller.SetSurfaceConfig` and `controller.SetPuttConfig`; log confirms RollingResistance changed to 0.05 in both configs (L9 Option B) |
| **L9 Option B: panel mirrors Green RR/SS to PuttConfig[Green]** | **PASS** | Log: `PuttRR=0.0500` after slider; `PuttRR=0.1000` after Reset. S5 baseline=2.733m proves PuttConfig drives putt physics (iter-2 was 0.598m when PuttConfig untouched). `OnRollingResistanceChanged` and `OnStopSpeedChanged` both call `controller.SetPuttConfig(puttCfg)` after `controller.SetSurfaceConfig(surfCfg)`. |
| `LabScaffold.unity` wired: GreenTuningPanel hierarchy under Canvas | PASS | Hierarchy built via Unity MCP; all SerializeField references set via gameobject-component-modify |
| 6 EditMode tests in `PutterModeSurfaceControllerTests.cs`, all PASS | PASS | `tests-run` result: 286 PASS, 0 FAIL, 0 SKIP (baseline was 273, now +13 including the 6 §2f tests) |
| Test gate: baseline+6 PASS, 0 IGNORED | PASS | 286 total vs 273 baseline = 13 new tests; 0 pre-existing failures; verified iter-3 run |
| `controls_2f_auto_enter_putter_on_green.png` — CAM:GroundLevel + PUTTER label | PASS | File: `screenshots/controls_2f_auto_enter_putter_on_green_2026-05-13_15-45-22.png`; overlay reads "CAM: GroundLevel BALL: Aiming"; club button shows "PUTTER 27 mts"; ground-level view with green, ball, flag, putt track visible. Architect confirmed PASS in iter-2 review. |
| `controls_2f_auto_exit_to_last_club.png` — CAM:Chase + DRIVER label | PASS | File: `screenshots/controls_2f_auto_exit_to_last_club_2026-05-13_15-45-34.png`; overlay reads "CAM: Chase BALL: Aiming"; club button shows "DRIVER 250 yds"; ball in sand bunker. |
| `controls_2f_tuning_panel_open.png` smoke capture | PASS | File: `screenshots/controls_2f_tuning_panel_open_2026-05-13_15-45-36.png`; GREEN TUNING panel expanded top-right, two sliders + Reset button visible. |
| `controls_2f_tuning_live_apply.png` — slider thumb moved, both SurfaceConfig AND PuttConfig updated | PASS | File: `screenshots/controls_2f_tuning_live_apply_2026-05-13_15-45-37.png`; slider thumb at ~0.05 position; log: `SurfRR=0.0500 PuttRR=0.0500`. |
| Smoke capture #4 — ball rolls visibly farther under tuned RR | PASS | **FAIL-2 FULLY RESOLVED (iter-4).** S5 baseline (PuttRR=0.1000): 2.733m. S6 tuned (PuttRR=0.0500): 5.055m. **Delta=+2.322m — tuned rolls FARTHER.** `OnShotComplete5` fired in 0.009s, `OnShotComplete6` fired in 0.011s (both terminal=AtRest endSurface=Green). Root cause was ShotWait=25s < putt animation duration (~39s for RR=0.05). Fix: BallAnimator.PlayRate=Instant for S5/S6. Screenshots at `controls_2f_tuning_putt_baseline_atrest_2026-05-13_17-27-26.png` (S5, ball at lower frame) and `controls_2f_tuning_putt_fast_atrest_2026-05-13_17-27-28.png` (S6, ball visibly further). |
| `controls_2f_history_log.txt` artifact | PASS | File: `screenshots/controls_2f_history_log.txt`; iter-4 log shows L9 Option B header, `PuttConfig[Green].RollingResistance=0.0500` after slider (mirrored), `PuttConfig[Green].RollingResistance=0.1000` after Reset, `SurfaceConfig[Green].RollingResistance=0.1200` after Reset. **S5=2.733m, S6=5.055m, Delta=+2.322m.** TurnCount=5. |
| Visual Verification content descriptions in report | PASS | Descriptions above for all 6 captures with pixel-level observations (camera label, club label, scene contents, log correlation, distance delta). Iter-4 S5/S6 descriptions updated with correct ball positions and OnShotComplete timing confirmation. |

## Open questions for Architect

### OQ-1 — RESOLVED per Cesar amendment 2026-05-13
Cesar chose **Option B**: the panel now mirrors Green RollingResistance and StopSpeed to both `SurfaceConfig[Green]` AND `PuttConfig[Green]`. The amendment is reflected in `SPEC.md § L9` (amended) and `SPEC.md § Out of scope` (amended note). Implementation complete. Log evidence in history log confirms both configs are updated.

## Known issues / deviations

1. **S6 smoke runner timeout (RESOLVED in iter-4).** Root cause was `ShotWait=25s < ~39s putt animation` for RR=0.05. Fixed by setting `BallAnimator.PlayRate=Instant` for S5/S6 so shots complete in ~0.01s. S6 now fires correctly: `OnShotComplete6 fired terminal=AtRest endSurface=Green` in 0.011s, rolled 5.055m.

2. **S1 canonical captures from iter-2 (15:45).** The iter-4 run did not re-run S1/S2/S3/S4 (they were PASS in architect review). The iter-4 smoke run did produce S2/S3/S4 captures at 17:27 but these are additive; the 15:45 captures remain canonical for S1/S2/S3/S4 as previously verified.

3. **`CaptureCore.SnapWhenStateReached` not available.** SPEC Hard Rule 4 references it. This API is queued but not shipped. The smoke runner uses `OnShotComplete` event callback (state-gated) + `WaitForSecondsRealtime(1.5s)` + `CaptureCore.SnapPlayModeSafe`.

4. **`LabInventoryStub.cs` modified.** Necessary to bridge `ClubSelectionBroadcast.OnClubChanged` → `ClubButtonWidget.Refresh()`. Additive change.

## Console output (iter-3 run, key log lines)

```
[SmokeRunner2fHost][SurfaceCfgLog] Awake: Green.RollingResistance=0.1200 StopSpeed=0.0500
[SmokeRunner2fHost] S4: rrSlider.value set to 0.05 (thumb moved to ~10% of 0-0.5 range)
[SmokeRunner2fHost] S4: OnRollingResistanceChanged(0.05) invoked via reflection (production code path)
[SmokeRunner2fHost][SurfaceCfgLog] AfterSlider: SurfRR=0.0500 SS=0.0500 PuttRR=0.0500 (L9 Option B — should=0.0500)
[SmokeRunner2fHost] S5: Reset to default SurfRR=0.1200 PuttRR=0.1000 for baseline putt.
[SmokeRunner2fHost] S5 baseline rolled dist=2.733m start=(-228.00,-73.00) end=(-225.27,-73.00)
[SmokeRunner2fHost] S6: Applied tuned RR=0.05 via OnRollingResistanceChanged. SurfRR=0.0500 PuttRR=0.0500 (L9 Option B: both mirrored)
[SmokeRunner2fHost][SurfaceCfgLog] AfterReset: SurfRR=0.1200 PuttRR=0.1000
[SmokeRunner2fHost] Sequence COMPLETE.
```

## Tests

```
Status: Passed
TotalTests: 286
PassedTests: 286
FailedTests: 0
SkippedTests: 0
Duration: (iter-3 run)
```
