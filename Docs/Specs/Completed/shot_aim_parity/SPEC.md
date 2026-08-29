# SPEC — `shot_aim_parity`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Filed 2026-08-28 by the Architect (Cowork) after a code read of the shot input path.

## Goal

Make the ball go where the targeting line points. Today the line and the committed shot use **two different aim formulas**, so a fully deflected handle draws a line at up to ±20° while the ball launches at most ±3° off the camera heading — which the player reads as "the flick always fires centered". The upswing aim latch from `SHOT_FLICK_FIX_SPEC` is working (tests pass); the scale mismatch sits *after* it. Second, harden the latch so lateral aiming at the bottom of the cone cannot freeze the aim early. Third, make the Fade/Draw line rotate to the locked heading the shot actually uses.

Intended behaviour (Cesar, 2026-08-28): **the shot angle is the handle's position at the bottom of the swing, before the flick up**, and the line shown at that moment is the truth.

## Root cause (read before editing)

`Assets/Scripts/Gameplay/Input/ShotController.cs`

- `PublishState()` (line ~750) drives the targeting line:
  `liveAim = CameraHeadingRadians + finetune * HalfConeAngleRad()` → 5°..20° at full deflection (`ConeHalfAngleAtAcc0Deg`/`ConeHalfAngleAtAcc100Deg`, `Club.Accuracy/120`). This is the formula in `Docs/Game Design/SHOT_CONTROLS_DESIGN.md §3.3`.
- `CommitFlick()` (line ~520), Straight mode, from `fade_draw_core_wiring` Order 356 D4:
  `_aimYawRadians = CameraHeadingRadians + finetune * _config.AimNudgeRangeRad + degradYaw` → 3° at full deflection (`controls.csv` row 27).
- Median shipped club `baseAccuracy` = 48 → half-cone ≈ 11°. Line/ball ratio ≈ 3.7×. At 200 yd the line implies ~38 yd lateral; the ball moves ~10 yd.
- Fade/Draw mode: `PublishState` still rotates the line by `finetune * halfCone`; the shot fires at `FadeDrawLockedAimRad` (straight) and *curves*. The line both rotates and bends; the ball only bends.
- `FadeDrawWiringTests.StraightMode_HandleRight_NudgesAimRight` asserts the 3° nudge. No test asserts line/shot parity — that is the gap that let this ship.

Secondary: `PushTouchSample()` latches on 1 % `Screen.height` (~28 px on a 15 Pro Max) of cumulative upward travel **and never unlatches within a swing**. A thumb sliding sideways at the cone base to aim wobbles more than that; the aim silently freezes while the handle keeps moving. Not what Cesar is seeing today (the line would visibly freeze), but it will surface the moment the scale is fixed.

## Decisions (Architect, for Cesar to overrule)

- **D1 — the shot honours the cone.** The committed aim uses `finetune * HalfConeAngleRad()` in Straight mode, exactly as the line does and as the design doc says. Rationale: the cone width is defined as "aim range AND error tolerance" (§3.3, §4); a cone you cannot aim across has no reason to be wide, and it makes Club Accuracy matter for aiming. If ±20° at Accuracy 120 feels like too much, tune `ConeHalfAngleAtAcc100Deg` in `controls.csv` — one number, no code. `AimNudgeRangeRad` is **removed** (config field, loader case, CSV row).
  *Alternative (not chosen):* shrink the line to 3° instead. Same helper, one line differs.
- **D2 — one formula, one place.** `ShotController` gets a single private `AimYawFor(float finetune)` used by BOTH `PublishState` and `CommitFlick`. The parity test below is the acceptance gate.
- **D3 — latch unlatches on a new low.** In `PushTouchSample`, whenever the finger goes below `_lowestTouchY` while latched, unlatch and re-sync the aim to the live handle. A wobble then freezes aim only until the thumb settles again; a real flick never comes back down. `_reversalThreshold` stays 0.01 (no retune — the unlatch makes the threshold forgiving).
- **D4 — Fade/Draw line rotates to the locked heading only.** Falls out of D2; the bend keeps coming from `AimLineBendRenderer.FinetuneX`.

## Architecture context

- **Asmdef boundaries:** `Golfin.Gameplay.Input` (ShotController) does not reference `Golfin.Gameplay.UI`. No new cross-references.
- **Existing code:**
  - `Assets/Scripts/Gameplay/Input/ShotController.cs` — `PublishState`, `CommitFlick`, `PushTouchSample`, `SetLiveFinetune`, `HalfConeAngleRad`, `FadeDrawLockedAimRad`, `FadeDrawActive`.
  - `Assets/Scripts/Gameplay/Input/ShotInputState.cs` — `AimYawRadians` carried to the UI (unchanged).
  - `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — `UpdateTargetingLine` reads `state.AimYawRadians` (unchanged) and feeds `AimLineBendRenderer.FinetuneX = state.ConeFinetuneX` (unchanged).
  - `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleDragger.cs` — unchanged.
  - `Assets/Scripts/Gameplay/Config/ControlsConfig.cs`, `ControlsConfigLoader.cs`, `Assets/Resources/Gameplay/controls.csv` — `AimNudgeRangeRad` removed.
  - Tests: `Assets/Scripts/Gameplay/Tests/FadeDrawWiringTests.cs`, `ShotControllerFlickGateTests.cs`.
- **Physics contract unchanged:** `ShotInputBuilder.Build(..., aimYawRadians, ...)` still receives the final yaw; velocity = `(cos yaw, ·, sin yaw)`.

## Implementation

### 1. `ShotController.AimYawFor(float finetune)` — single source of truth

```csharp
/// Aim yaw WITHOUT per-pass degradation. Used by PublishState (live line) and
/// CommitFlick (+ degradYaw). If these ever disagree the line lies — see ShotAimParityTests.
private float AimYawFor(float finetune)
{
    if (!IsPutt && FadeDrawActive)
    {
        // Aim was locked when the toggle was armed (D5, Order 356). Handle = curve, not aim.
        return float.IsNaN(FadeDrawLockedAimRad) ? CameraHeadingRadians : FadeDrawLockedAimRad;
    }
    // Straight swing AND putt: handle position maps to ±halfCone (SHOT_CONTROLS_DESIGN §3.3).
    return CameraHeadingRadians + finetune * HalfConeAngleRad();
}
```

- `CommitFlick`: replace the three-branch block with
  `_aimYawRadians = AimYawFor(finetune) + degradYaw;` (keep the `finetune` local: `DebugFlags.DisableConeFineTune ? 0f : _aimFinetune`). Keep the Phase-B `fadeDrawInputFp` block as is.
- `PublishState`: `float liveAim = AimYawFor(finetune);` (same `finetune` local it already computes).
- The `LogResolution` snapshot in `CommitFlick` gains `halfCone={HalfConeAngleRad()*Mathf.Rad2Deg:F1}deg finetune={finetune:F3}` so a device log can be checked against the line by eye.

### 2. `PushTouchSample` — unlatch on a new low (D3)

Replace the "new lowest" branch:

```csharp
if (float.IsNaN(_lowestTouchY) || screenPosPx.y < _lowestTouchY)
{
    _lowestTouchY = screenPosPx.y;
    if (_aimLocked)
    {
        // The "reversal" was a wobble: the thumb came back down. Re-open the aim so
        // lateral aiming at the cone base keeps steering the line.
        _aimLocked   = false;
        _aimFinetune = _coneFinetune;
    }
    return;
}
```

Note the early `if (_debugDisableAimLock || _aimLocked) return;` above it must become `if (_debugDisableAimLock) return;` so a latched swing still tracks a new low. The latch condition below it is unchanged.

`ClubHandleDragger.OnPointerUp` already pushes the release sample before `SetExternalPower(_peakPower, _peakFinetune)` — a release is never below the bottom of the swing, so this cannot unlatch at release. No change there.

### 3. Remove `AimNudgeRangeRad`

- `ControlsConfig.cs`: delete the field and its `Default` initialiser.
- `ControlsConfigLoader.cs`: delete the `case "AimNudgeRangeRad"`.
- `controls.csv`: delete row 27 — `ControlsConfigLoader` logs a warning for unknown keys, so the row must go with the field.
- `Docs/Specs/Completed/fade_draw_core_wiring/SPEC.md` is history — do not edit. Add a one-line note to `Docs/Game Design/SHOT_CONTROLS_DESIGN.md §3.3`: "D4 3° nudge (Order 356) reverted 2026-08-28 — `shot_aim_parity`; the handle maps to ±halfCone for the shot as well as the line."

### 4. Tests

New `Assets/Scripts/Gameplay/Tests/ShotAimParityTests.cs` (EditMode, same harness as `FadeDrawWiringTests`: `InjectConfig`, `InjectStatBundle`, `OnStateChanged` capture of the last `ShotInputState`, `OnShotResolved` capture of the `ShotInput`, `DebugFlags.ForcePerfectAim = true` so `degradYaw = 0`):

1. `Straight_PublishedAimEqualsCommittedAim` — for finetune in {−1, −0.6, 0, 0.35, 1}: `BeginExternalDrag → SetExternalPower(0.8, f) → EndExternalDrag`; assert `atan2(v.z, v.x)` of the resolved velocity == last published `AimYawRadians` within 1e-3 rad, and == `CameraHeadingRadians + f * ConeHalfAngleDeg*Deg2Rad`.
2. `FadeDraw_PublishedAimIsLockedHeading` — `FadeDrawActive = true; FadeDrawLockedAimRad = 0.4f`; finetune 0.9 → published aim == committed aim == 0.4 (bend is not yaw).
3. `Putt_PublishedAimEqualsCommittedAim` — `IsPutt = true`, finetune 0.5 → parity, using the putter half-cone.
4. `Latch_ReopensWhenFingerGoesLower` — push samples down to y=300, up 5 % of `Screen.height` (latched, `IsAimLocked` true), `SetExternalPower(0.8, 0.7)` (ignored by aim), then push y=290 → `IsAimLocked` false and the next `SetExternalPower(0.8, 0.7)` publishes `ConeFinetuneX == 0.7`.
5. `Latch_HoldsThroughUpswing` — existing `ShotControllerFlickGateTests.OnceLatched_LateralMovementNoLongerSteersAim` must still pass unchanged.

Update `FadeDrawWiringTests` tests 1–2: expected delta = `_sc.ConeHalfAngleDeg * Mathf.Deg2Rad` (for the injected bundle) instead of `_cfg.AimNudgeRangeRad`. Rename to `StraightMode_HandleRight_AimsRightByHalfCone` / `..._Left...`.

### 5. Changelog

`Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` — new top entry **F14 — Straight-mode aim honours the cone (AimNudgeRangeRad removed)**: symptom, the two formulas, the 3.7× figure, D1–D4, files, tests. Same shape as F13.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `ShotAimParityTests` 1–4 pass; `ShotControllerFlickGateTests`, `FadeDrawWiringTests` (updated), `ShotControllerTests`, `ShotControllerPuttModeTests`, `PowerGaugeMarkerTests`, `MapViewAimingTests` pass — run the whole `Golfin.Gameplay.Tests` assembly, not a filter.
- [ ] `grep -rn AimNudgeRangeRad Assets/` returns nothing.
- [ ] Editor play, Hole 01, Straight mode, `LogResolution` on: pull the handle to the far right edge of the cone, hold, flick → log `aimYawRadians` − `CameraHeadingRadians` == +`halfCone` (±0.02 rad) and the ball visibly lands right of the pre-shot line's direction by the same angle. Repeat far left. Repeat centre → delta 0.
- [ ] Editor play: pull to the base, slide the thumb left↔right three times with deliberate vertical wobble, then flick from the right → the line kept steering during the slide (no early freeze) and the shot goes right.
- [ ] Editor play, Fade/Draw armed: handle at +1 → line root points at the locked heading (not rotated), bend visible; ball launches straight and fades.
- [ ] Bots unaffected: `Scenarios` smoke that drives `FireDebugShot` / `BeginExternalDrag` without touch samples still fires and lands as before (compare one carry value before/after — the yaw path for finetune 0 is byte-identical).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

Manual on-device: the three Editor-play checks repeated once on the iPhone (Cesar's call — standing rule is no device pass by default).

## Out of scope

- Timing-slab power effect → `shot_timing_power` (separate spec, run AFTER this one; it edits the same two methods).
- Flick-vector aiming (control scheme C) → design note in `Docs/Specs/Queued/flick_vector_aim_DESIGN_NOTE.md`.
- Cone width values, arrow speeds, degradation constants, fade/draw tilt, spin, map-view target marker.
- Any UI hierarchy or prefab change.

## Files this task touches

- `Assets/Scripts/Gameplay/Input/ShotController.cs` — `AimYawFor`, `CommitFlick`, `PublishState`, `PushTouchSample`, log line.
- `Assets/Scripts/Gameplay/Config/ControlsConfig.cs`, `ControlsConfigLoader.cs`, `Assets/Resources/Gameplay/controls.csv` — remove `AimNudgeRangeRad`.
- `Assets/Scripts/Gameplay/Tests/ShotAimParityTests.cs` (new), `FadeDrawWiringTests.cs` (2 expectations).
- `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md` (F14), `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` (§3.3 note), `Docs/AI_CONTEXT.md`.

## Smoke evidence

EditMode run summary (whole Gameplay.Tests assembly) + one `LogResolution` console line per manual check pasted into the report, + the F14 entry.
