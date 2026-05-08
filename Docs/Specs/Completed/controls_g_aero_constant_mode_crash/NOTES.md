# NOTES — `controls_g_aero_constant_mode_crash`

> Architect pre-spec analysis. Not yet a SPEC. Implementer reads SPEC.md for work definition once locked.

## Status

`Docs/Specs/Queued/controls_g_aero_constant_mode_crash/` — STATUS=`NOTES_DRAFT` 2026-05-07 09:20 JST.

Notion: [`35931e0e-9a36-8163-a839-d5190f134f0f`](https://www.notion.so/35931e0e9a368163a839d5190f134f0f) — Phase 02. Loop v1, Order 220, P0 — Critical, S (half-day), Status=Next.

## What happened

§2b camera transitions ran clean through implementer + self-reviewer + reviewer subagent. All 9 new EditMode tests PASS, 236/236 total gate PASS. Implementer attempted live smoke captures of the 3 new modes (Downrange / putter-stays-GroundLevel / OBFreeze). All 3 captures FAILed because `BallSimulation.Simulate` crashes on every shot fire with `DivideByZeroException` in `AeroModel.ComputeAeroForce` at `AeroModel.cs:78`.

## Stack trace

```
DivideByZeroException: Attempted to divide by zero.
  at Golfin.Physics.Math.fp.op_Division (fp a, fp b) [fp.cs:32]
  at Golfin.Physics.AeroModel.ComputeAeroForce (...) [AeroModel.cs:78]
  at Golfin.Physics.BallSimulation.SimulateAirborne (...) [BallSimulation.cs:367]
  at Golfin.Physics.Viewer.PhysicsLabController.RunSimFromController (...) [PhysicsLabController.cs:787]
  at Golfin.Physics.Viewer.PhysicsLabController.HandleShotResolved (...) [PhysicsLabController.cs:688]
  at Golfin.Gameplay.Input.ShotController.CommitFlick () [ShotController.cs:265]
  at Golfin.Physics.Viewer.SmokeTestRunner2a+<RunSmokeTest>d__7.MoveNext () [SmokeTestRunner2a.cs:127]
```

## What is line 78 actually

Verified by reading `AeroModel.cs` directly 2026-05-07 09:18 JST. Lines 76-79 are the **constant-mode (non-LUT) lift branch**:

```csharp
else
{
    fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
    cl = cfg.LiftCoefficientBase * spinScale;
}
```

The divisor is `cfg.SpinRateReference`, NOT `speed`. So implementer's proposed line-29 guard (`if (speed <= fp.Epsilon) return fp3.Zero;`) is targeting an unrelated divide and would not fix the crash.

## Why is this latent

§2a's iter-4 smoke was a putter shot. Putter has `spin.Rate ≈ 0`. `SpinState.IsSpinning` becomes false. AeroModel returns at line 56 (`if (!spin.IsSpinning) return drag;`) without entering the lift branch. So the lift-branch divide-by-zero never executes for putter.

§2b's first smoke attempt was a driver shot. Driver has high spin rate. The lift branch enters. Whichever path it takes (LUT or constant-mode) needs the divisors to be non-zero. Crash on first execution.

§2b's driver shots are the **first lift-branch executions since `controls_f` closed 2026-05-06 06:47 JST**. Roughly 27 hours of latency.

## Root-cause hypotheses (likelihood order)

### Hypothesis A — `cfg.UseLiftLut` loading as `false`

**Evidence for:** Per `controls_e/f`, lift LUT is the canonical path. If we're hitting constant-mode at all, that's the regression. The whole `aero_lift_overlay.csv` machinery only runs in the LUT branch.

**How to verify:** Print `cfg.UseLiftLut` and `cfg.LiftLut.IsValid` at first lift call.

**If true:** Trace `PhysicsConfigLoader.LoadAero()` (or wherever `UseLiftLut` is set) to find what stopped writing it.

### Hypothesis B — `cfg.LiftLut.IsValid` returning `false`

**Evidence for:** `aero_lift_lut.csv` could fail to load (file missing, parse error, empty after layer-status header changes). The branch is `if (cfg.UseLiftLut && cfg.LiftLut.IsValid)`. If `IsValid` returns false, falls to constant-mode.

**How to verify:** Print `cfg.LiftLut.IsValid` and the LUT row count at first lift call.

**If true:** Inspect `LiftLut.Load()` parsing. Most likely culprit: the layer-status header rows added by controls_e/f are confusing the parser.

### Hypothesis C — `cfg.SpinRateReference` loading as `fp.Zero`

**Evidence for:** Independent of LUT path. If `aero.csv` lost the `SpinRateReference` row, or the field defaults to zero on missing key, constant-mode crashes regardless of why we entered it.

**How to verify:** Print `cfg.SpinRateReference` at first lift call.

**If true:** Inspect `aero.csv` and `PhysicsConfigLoader.LoadAero()` for the field's default.

### Hypothesis D — combination

Most likely scenario: BOTH a LUT-path failure AND a config-default failure. The system silently falls to constant-mode (A or B) and the constant-mode path has uninitialized `SpinRateReference` (C). Fix would address both layers.

## Proposed scope

This is a P0 diagnosis-and-fix task. Scope (locked candidates, not yet decided with Cesar):

**Phase A — diagnosis (~1-2 hours):**
1. Add temporary `Debug.Log` prints at AeroModel line 60 (just before lift branch entry) showing `cfg.UseLiftLut`, `cfg.LiftLut.IsValid`, `cfg.LiftLut.RowCount` (if accessible), `cfg.SpinRateReference`. Run a driver shot in lab. Read the values. Identify which hypothesis.
2. Walk back from the broken value to its source: `PhysicsConfigLoader.LoadAero()` if config issue; `LiftLut.Load()` if LUT-load issue.

**Phase B — fix (~2-3 hours):**
1. Restore the broken codepath (config default, parser, etc).
2. Audit all 3 aero divides holistically:
   - **Line 29** (`vRel/speed`): speed-underflow case, gated by line-26 `speedSq <= fp.Epsilon` epsilon but `Sqrt` may underflow further. Defense: tighter epsilon OR consider returning drag-only.
   - **Line 63** (LUT-mode `spinParam = R*Rate/speed`): same speed denominator. Verify line-26 epsilon is tight enough.
   - **Line 78** (constant-mode `Rate/SpinRateReference`): denominator is config. Defense: `AeroConfig` constructor-time assert that `SpinRateReference > fp.Zero`. Different category from line 29/63.

**Phase C — validation (~1 hour):**
1. Re-run controls_e/f tripwire test gate (211/211 PASS) to confirm fix doesn't regress driver/iron carries.
2. **Run §2b's deferred smoke captures** using `CaptureCore.SnapWhenStateReached(sm, BallState.Flying, "downrange", ...)`, `... BallState.AtRest, "putter_groundlevel", ...`, `... BallState.OB, "obfreeze", ...`. Closes §2b smoke debt as part of this task.
3. New EditMode test: `Aero_ConstantModeFallback_DoesNotCrashWithDefaultConfig` — defends against future regressions of this class.

**Estimate total:** half-day to 1 day. Fits S → M.

## Open questions for Cesar (lock before SPEC)

1. **Phase B scope ambition.** Should we add a constructor-time assert on `AeroConfig.SpinRateReference > 0` (defense in depth) or only fix the loading path? Architect lean: BOTH — defense-in-depth catches future regressions cheaply.
2. **Test additions.** Just the new fallback test, or also a tripwire test that drives a full driver shot and asserts no exception? Architect lean: BOTH — fallback test is unit, full-shot test is integration.
3. **Smoke evidence dual-purpose.** Confirm controls_g closeout includes the §2b deferred smoke captures (currently planned). Architect default: yes; this is the cleanest debt-closure path.
4. **Where in AeroModel.cs does the diagnosis log live?** Permanent (gated behind a debug flag) or strictly temporary (removed before merge)? Architect lean: temporary; removed before merge.
5. **Run order: serial vs. parallel with §2c.** Architect lean: parallel is fine — §2c is turn counter logic, doesn't share files.

## Hard rules pre-locked (carry into SPEC)

1. **Do NOT modify** `BallSimulation.cs` for this fix. The bug is in AeroModel + config-load, not in the integrator. (Same hard rule as controls_d/e/f.)
2. **Do NOT change `aero_lift_lut.csv` / `aero_drag_lut.csv` / `aero_lift_overlay.csv` / `aero_drag_overlay.csv` data values.** Those are calibrated. controls_g is about loading them correctly, not retuning them.
3. **Bit-exact 211/211 PASS gate must hold.** Any expected-value snapshot updates require Cesar approval and per-test justification.
4. **Smoke evidence per §2a Lessons M+N:** file persisted on disk + parallel-path Read verification + content-sanity. Use the new `CaptureCore.SnapWhenStateReached` API.

## Files this task likely touches

- `Assets/Scripts/Physics/Core/AeroModel.cs` (audit divides, possibly add config assert)
- `Assets/Scripts/Physics/Core/AeroConfig.cs` (possibly add constructor validation)
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` (root-cause fix to whatever stopped loading)
- `Assets/Resources/Configs/aero.csv` (verify SpinRateReference row present and non-zero)
- `Assets/Resources/Configs/aero_lift_lut.csv` (verify layer-status header doesn't break parser)
- `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` (new fallback test)
- Possibly: new `Assets/Scripts/Physics/Tests/AeroConstantModeTests.cs` for the unit test of the fallback path.

## Reference

- `Docs/Specs/Active/loop_v1_2b_camera_transitions/ARCHITECT_REVIEW.md` § "ADDENDUM — Human Architect ruling" — origin of this task.
- `Docs/Specs/Completed/controls_e_aero_overlay_pass/SPEC.md` — most recent aero work; layer-status header changes here might be the regression source.
- `Docs/Specs/Completed/controls_f_drag_calibration_audit/SPEC.md` — last touch of `AeroConfig` / `PhysicsConfigLoader`.
- `Docs/Physics/CALIBRATION_METHODOLOGY.md` — two-layer architecture frame; controls_g must preserve this.
- `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson K — Mars Climate Orbiter parallel; calibration regressions are easy to introduce silently.
