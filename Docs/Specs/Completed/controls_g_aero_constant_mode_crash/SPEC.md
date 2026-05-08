# SPEC — `controls_g_aero_constant_mode_crash`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-07 09:35 JST.

## Goal

Diagnose and fix the `DivideByZeroException` at `AeroModel.cs:78` (`fpMath.Clamp(spin.Rate / cfg.SpinRateReference, ...)` in the constant-mode (non-LUT) lift branch) that crashes `BallSimulation.Simulate` on every driver-class shot. Audit all three aero divides holistically (lines 29, 63, 78). Add defense-in-depth at `AeroConfig` construction time. Re-run controls_e/f gate (211/211 PASS) to confirm fix doesn't regress driver/iron carries. Closeout includes running §2b's deferred smoke captures (Downrange / putter-stays-GroundLevel / OBFreeze) using the new `CaptureCore.SnapWhenStateReached` API — closes §2b smoke debt as part of this task.

## Reference

- **Architect NOTES:** `Docs/Specs/Active/controls_g_aero_constant_mode_crash/NOTES.md` (carries pre-spec analysis + the locked answers to Q1–Q5 and architect's verified read of `AeroConfig.Default`).
- **Origin:** `Docs/Specs/Completed/loop_v1_2b_camera_transitions/ARCHITECT_REVIEW.md` § "ADDENDUM — Human Architect ruling" — surfaced this regression and queued the task.
- **Most recent aero work:** `Docs/Specs/Completed/controls_e_aero_overlay_pass/SPEC.md`, `Docs/Specs/Completed/controls_f_drag_calibration_audit/SPEC.md` — likely regression sources via layer-status header changes or config-load shuffling.
- **Calibration methodology:** `Docs/Physics/CALIBRATION_METHODOLOGY.md` — two-layer architecture frame; controls_g must preserve this.
- **Lesson K** in `Docs/Diagnostics/PIPELINE_LESSONS.md` — calibration regressions are easy to introduce silently.

## Background — what exists today

Verified by code walk 2026-05-07 09:25 JST.

| File | Role for this task |
|---|---|
| `Assets/Scripts/Physics/Core/AeroModel.cs` | Static aero force computation. Three divides at lines 29 (`vRel/speed`), 63 (LUT `(R*Rate)/speed`), 78 (constant-mode `Rate/SpinRateReference`). Line 78 is the crash site. **Modify with surgical care.** |
| `Assets/Scripts/Physics/Core/AeroConfig.cs` | Pure data struct + `Default` and `Vacuum` static factories. **`Default.SpinRateReference = 300f`** (non-zero). **`Default.UseLiftLut = false`** — silent fallback to constant-mode if CSV doesn't opt in. |
| `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` | `LoadAeroConfig()` parses `Resources/Physics/aero.csv`, switch-case maps known keys. Calls `LoadLiftLut()` etc. for LUT files. **Likely regression source.** |
| `Assets/Resources/Physics/aero.csv` | Source of truth for `use_lift_lut`, `spin_rate_reference`, etc. **Read-verify before fix.** |
| `Assets/Resources/Physics/aero_lift_lut.csv` | Layer 1 LUT. Layer-status header rows added by controls_e/f. **Verify parser still ingests ≥2 valid data rows after header changes.** |
| `Assets/Resources/Physics/aero_drag_lut.csv` | Same architecture as lift LUT. Verify by symmetry. |
| `Assets/Resources/Physics/aero_lift_overlay.csv` | Layer 2 (controls_e). Only consumed if `UseLiftOverlay && LiftLut.IsValid`. Inert if LUT path is broken. |
| `Assets/Resources/Physics/aero_drag_overlay.csv` | Layer 2 (controls_f). Same as lift overlay. |
| `Assets/Scripts/Physics/Core/BallSimulation.cs` | Calls `AeroModel.ComputeAeroForce` from `SimulateAirborne` at line 367. **Do not modify** — same hard rule as controls_d/e/f. |
| `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` | Existing 211-test gate. Tripwire test `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` (now active, post-controls_f). |
| `Assets/Scripts/Physics/Tests/` | Existing EditMode test asmdef. New tests land here. |
| `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs` | New §2b API. `SnapWhenStateReached(MonoBehaviour owner, BallStateMachine sm, BallState target, string label, ...)`. Used in Phase C for §2b deferred smoke captures. |
| `Assets/Scripts/Physics/Viewer/SmokeTestRunner2a.cs` | Reference implementation for SM-driven smoke runner. Phase C will likely add a `SmokeTestRunner2b` or extend this with §2b-specific shot scripts. |

## Locked decisions (carry forward from NOTES.md)

- **Q1 — Phase B scope ambition:** BOTH. Fix the loading path AND add `AeroConfig` constructor-time assert that `SpinRateReference > fp.Zero`.
- **Q2 — Test additions:** BOTH. New unit fallback test (`Aero_ConstantModeFallback_DoesNotCrashWithDefaultConfig`) AND new full-shot integration tripwire (`Aero_DriverShot_DoesNotThrow`).
- **Q3 — Smoke evidence dual-purpose:** YES. controls_g closeout includes §2b's deferred Downrange / putter-stays-GroundLevel / OBFreeze captures. Closes §2b smoke debt as part of this task's validation.
- **Q4 — Diagnosis log lifecycle:** TEMPORARY. Diagnosis prints removed before merge.
- **Q5 — Run order vs §2c:** PARALLEL. §2c is turn counter logic, doesn't share files with controls_g.

## Architecture context

- **No new asmdef.** All work lives in existing `Golfin.Physics.Core` (AeroModel, AeroConfig), `Golfin.Physics.Runtime` (PhysicsConfigLoader), `Golfin.Physics.Tests`. CSV edits in `Resources/Physics/`.
- **No changes to** `Golfin.Physics.Math`, `Golfin.Physics.Stats`, `Golfin.Physics.Viewer`, `Golfin.Gameplay.Loop`, `Golfin.Gameplay.Input`, `Golfin.Gameplay.UI`, `Golfin.Diagnostics.Runtime`.
- **Golfin.Diagnostics.Runtime is consumed** by Phase C smoke runner — no edits to that asmdef.

## Implementation

### Phase A — Diagnosis (~1-2 hours)

**Goal:** identify which specific value broke and why. NO fixes in Phase A.

#### A.1 — Add temporary diagnostic prints at AeroModel lift-branch entry

**Location:** `Assets/Scripts/Physics/Core/AeroModel.cs` immediately after line 56 (`if (!spin.IsSpinning) return drag;`) and before line 58 (lift branch comments).

Use a static one-shot guard so the print fires only on the first lift call per session (avoids 240Hz log flood):

```csharp
// CONTROLS_G DIAGNOSIS — REMOVE BEFORE MERGE
if (!_diagPrintedFirstLift)
{
    _diagPrintedFirstLift = true;
    UnityEngine.Debug.Log(
        $"[CONTROLS_G][AeroModel.LiftEntry] " +
        $"UseLiftLut={cfg.UseLiftLut} " +
        $"LiftLut.IsValid={cfg.LiftLut.IsValid} " +
        $"LiftLut.RowCount={(cfg.LiftLut.IsValid ? cfg.LiftLut.XCount : 0)} " +  // see A.2 if XCount doesn't exist
        $"SpinRateReference={(float)cfg.SpinRateReference} " +
        $"LiftCoefficientBase={(float)cfg.LiftCoefficientBase} " +
        $"LiftMaxMultiplier={(float)cfg.LiftMaxMultiplier} " +
        $"UseLiftOverlay={cfg.UseLiftOverlay} " +
        $"LiftOverlay.IsValid={cfg.LiftOverlay.IsValid}");
}
```

Add the static field near the top of `AeroModel`:
```csharp
// CONTROLS_G DIAGNOSIS — REMOVE BEFORE MERGE
private static bool _diagPrintedFirstLift = false;
```

#### A.2 — If `CoefficientLut.XCount` (or equivalent row-count accessor) doesn't exist

Skip that one field in the print line. Use whatever public accessor exists on `CoefficientLut` (read its source first).

#### A.3 — Add a one-shot dump after LoadAeroConfig returns

**Location:** `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs::LoadAeroConfig` immediately before `return cfg;` at the end of the method.

```csharp
// CONTROLS_G DIAGNOSIS — REMOVE BEFORE MERGE
UnityEngine.Debug.Log(
    $"[CONTROLS_G][LoadAeroConfig] " +
    $"UseLiftLut={cfg.UseLiftLut} " +
    $"LiftLut.IsValid={cfg.LiftLut.IsValid} " +
    $"SpinRateReference={(float)cfg.SpinRateReference} " +
    $"LiftCoefficientBase={(float)cfg.LiftCoefficientBase} " +
    $"LiftMaxMultiplier={(float)cfg.LiftMaxMultiplier} " +
    $"UseDragLut={cfg.UseDragLut} " +
    $"DragLut.IsValid={cfg.DragLut.IsValid} " +
    $"UseLiftOverlay={cfg.UseLiftOverlay} " +
    $"LiftOverlay.IsValid={cfg.LiftOverlay.IsValid} " +
    $"UseDragOverlay={cfg.UseDragOverlay} " +
    $"DragOverlay.IsValid={cfg.DragOverlay.IsValid}");
```

#### A.4 — Run a driver shot in lab + capture logs

1. Open `LabScaffold.unity`, hit Play.
2. Set club to driver (default).
3. Fire one shot from tee (touch flick, 80%+ power so `IsSpinning=true` and lift branch enters).
4. Inspect Console for the two `[CONTROLS_G]` lines.
5. Capture the values into `IMPLEMENTER_REPORT.md` § "Phase A diagnosis findings".

#### A.5 — Identify the broken value

From the Console output, classify which hypothesis matches:

- **`SpinRateReference == 0`** → Hypothesis C (config value zeroed). Walk `aero.csv` row for `spin_rate_reference`; verify the row exists with non-zero value.
- **`UseLiftLut == false`** AND `aero.csv` has `use_lift_lut, 1` row → Hypothesis A (parser regression). Walk `LoadAeroConfig` switch-case for `"use_lift_lut"` key.
- **`LiftLut.IsValid == false`** → Hypothesis B (LUT load failure). Walk `LoadLut` for `aero_lift_lut`. Most likely culprit: layer-status header consuming the actual data header row, or a parse error reducing valid rows below 2.
- **Combination of above** → Hypothesis D. Fix all broken layers in Phase B.

If NONE of the above match (e.g. all values look correct but crash still fires), escalate as `IMPLEMENTER_BLOCKED` — diagnosis assumption was wrong, architect needs to re-read.

#### A.6 — Walk back to source

For whichever hypothesis matched, trace the chain:
- Open `aero.csv` and confirm row contents.
- Open `aero_lift_lut.csv` and confirm header structure + row count.
- Open `PhysicsConfigLoader.LoadAeroConfig` (for `aero.csv` issues) or `LoadLut` (for LUT issues).
- Identify the specific line of code or CSV row where the value diverges from expected.

Document findings in `IMPLEMENTER_REPORT.md` § "Phase A diagnosis findings" with file paths + line numbers + value comparisons.

### Phase B — Fix (~2-3 hours)

**Goal:** restore the broken codepath AND add defense-in-depth.

#### B.1 — Fix the broken codepath

Targeted to whichever hypothesis matched in Phase A. Examples:

- **If aero.csv missing `spin_rate_reference, 300` row:** add the row. Document why it was missing (was it deleted, never present, miswritten?).
- **If aero.csv has `use_lift_lut, 0` instead of `1`:** flip to `1`. Document.
- **If `LoadAeroConfig` switch-case missing `"use_lift_lut"` mapping:** add the case. Document.
- **If `LoadLut` parser eats the layer-status header as data:** prefix the header with `#` in the CSV (parser already skips comments) OR adjust the parser to recognize layer-status preamble. Architect lean: prefix with `#` (less code change, matches existing convention).

ONE fix per identified root cause. Don't broaden scope.

#### B.2 — Add `AeroConfig` constructor-time assert (defense-in-depth, Q1 lock)

**Location:** `Assets/Scripts/Physics/Core/AeroConfig.cs`. Add a public validation method (not a constructor — it's a struct with field initializers):

```csharp
/// <summary>
/// Throws InvalidOperationException if any field has a value that would cause
/// AeroModel.ComputeAeroForce to divide by zero. Call after LoadAeroConfig returns,
/// or in tests after constructing a custom config.
/// </summary>
public void AssertValid()
{
    if (SpinRateReference <= fp.Zero)
        throw new System.InvalidOperationException(
            $"AeroConfig.SpinRateReference must be > 0 (got {(float)SpinRateReference}). " +
            $"Constant-mode lift branch divides by this. Check Resources/Physics/aero.csv 'spin_rate_reference' row.");
    
    if (BallMass <= fp.Zero)
        throw new System.InvalidOperationException(
            $"AeroConfig.BallMass must be > 0 (got {(float)BallMass}). " +
            $"BallSimulation divides by this. Check Resources/Physics/aero.csv 'ball_mass' row.");
}
```

**Wire the call:** `PhysicsConfigLoader.LoadAeroConfig` immediately before `return cfg;`:
```csharp
cfg.AssertValid();
return cfg;
```

This catches future regressions of this exact class at config-load time, with a clear error message instead of a cryptic divide-by-zero deep in the simulation loop.

#### B.3 — Audit all three aero divides holistically

Walk each divide in `AeroModel.ComputeAeroForce` and document the safety invariant + add guards where the existing safety is too weak:

##### Line 29: `fp3 vRelHat = vRel / speed;`

**Existing safety:** Line 26 `if (speedSq <= fp.Epsilon) return fp3.Zero;` gates entry. `speed = sqrt(speedSq)`. If `speedSq > fp.Epsilon`, then `speed > sqrt(fp.Epsilon)`. In Q16.16 fixed-point, `fp.Epsilon ≈ 2^-16 ≈ 1.5e-5`, so `sqrt(Epsilon) ≈ 4e-3` — well above zero.

**Architect verdict:** existing guard is sufficient. NO new guard at line 29.

##### Line 63: `fp spinParam = (cfg.BallRadius * spin.Rate) / speed;`

**Existing safety:** same `speedSq <= fp.Epsilon` gate on line 26. Same speed denominator as line 29.

**Architect verdict:** existing guard is sufficient. NO new guard at line 63.

##### Line 78: `fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, ...);`

**Existing safety:** NONE. Denominator is a config field with no runtime guard.

**Architect verdict:** the `AeroConfig.AssertValid` call from B.2 catches this at config-load time. NO inline guard at line 78 — the assert is the right defense layer (catches the bug before sim runs, with a useful message). Inline guards would silently swallow the issue.

#### B.4 — Document audit results

Add a new comment block at the top of `AeroModel.ComputeAeroForce` (above line 22):

```csharp
// AERO DIVIDE AUDIT (controls_g, 2026-05-07):
// Line 29 (vRel/speed):                    safe via line-26 epsilon gate.
// Line 63 (LUT spinParam):                 safe via line-26 epsilon gate.
// Line 78 (constant-mode spinScale):       safe via AeroConfig.AssertValid at config-load time.
// If you add a new divide, audit it here and document the safety invariant.
```

This becomes the durable record so future-you doesn't re-litigate the same questions.

### Phase C — Validation (~1 hour)

**Goal:** confirm fix doesn't regress anything AND closes §2b smoke debt.

#### C.1 — Run the existing 211-test gate

```
Window > Test Runner > EditMode > Run All
```

Required: **211/211 PASS, 0 IGNORED.** Bit-exact gate held by controls_e/f must hold here. Any test that wasn't supposed to be touched starting to fail = `IMPLEMENTER_BLOCKED` escalation.

#### C.2 — Add new unit test: `Aero_ConstantModeFallback_DoesNotCrashWithDefaultConfig`

**Location:** `Assets/Scripts/Physics/Tests/AeroConstantModeTests.cs` (NEW file).

```csharp
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Tests
{
    public class AeroConstantModeTests
    {
        [Test]
        public void Aero_ConstantModeFallback_DoesNotCrashWithDefaultConfig()
        {
            // Arrange: simulate the regression's preconditions — UseLiftLut=false (default),
            // SpinRateReference=300 (default), spinning ball, non-zero velocity.
            var cfg = AeroConfig.Default;
            Assert.IsFalse(cfg.UseLiftLut, "Default.UseLiftLut should be false (sentinel for constant-mode path).");
            Assert.That((float)cfg.SpinRateReference, Is.GreaterThan(0f),
                "Default.SpinRateReference must be > 0 to prevent constant-mode divide-by-zero.");
            
            var velocity = new fp3(fp.FromFloat(60f), fp.FromFloat(20f), fp.Zero);
            var spin     = new SpinState
            {
                Axis = new fp3(fp.Zero, fp.Zero, fp.One),
                Rate = fp.FromFloat(300f), // 300 rad/s ≈ 2860 RPM, typical driver
            };
            
            // Act + Assert: must not throw.
            var force = AeroModel.ComputeAeroForce(velocity, fp3.Zero, spin, cfg);
            Assert.That((float)force.x, Is.Not.NaN);
            Assert.That((float)force.y, Is.Not.NaN);
            Assert.That((float)force.z, Is.Not.NaN);
        }
        
        [Test]
        public void Aero_AssertValid_ThrowsOnZeroSpinRateReference()
        {
            var cfg = AeroConfig.Default;
            cfg.SpinRateReference = fp.Zero;
            Assert.Throws<System.InvalidOperationException>(() => cfg.AssertValid());
        }
        
        [Test]
        public void Aero_AssertValid_PassesOnDefaultConfig()
        {
            var cfg = AeroConfig.Default;
            Assert.DoesNotThrow(() => cfg.AssertValid());
        }
    }
}
```

#### C.3 — Add new integration tripwire: `Aero_DriverShot_DoesNotThrow`

**Location:** `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` (existing file). Add a single new test that drives a full driver shot through `BallSimulation.Simulate` and asserts no exception:

```csharp
[Test]
public void Aero_DriverShot_DoesNotThrow()
{
    // Driver: 80 m/s ball speed, 11° launch, 2700 RPM backspin — Trackman composite Tour-pro driver.
    var cfg     = PhysicsConfigLoader.LoadAeroConfig();
    var wind    = WindConfig.Calm;
    var surface = PhysicsConfigLoader.LoadSurfaceConfig();
    
    // ... (use whatever existing test helper builds a Simulate call from these inputs;
    //      mirror an existing tripwire test's setup pattern.)
    
    Assert.DoesNotThrow(() =>
    {
        var traj = BallSimulation.Simulate(/* ... */);
        Assert.That(traj.samples.Length, Is.GreaterThan(0),
            "Trajectory should have samples; if zero, sim crashed silently.");
    });
}
```

This is the integration-level tripwire that would have caught the controls_g regression at the test layer instead of at smoke-capture time.

#### C.4 — Run §2b's deferred smoke captures (closes §2b debt, Q3 lock)

Build a `SmokeTestRunner2b.cs` (or extend SmokeTestRunner2a per architectural lean — implementer's call) that:

1. Loads `LabScaffold.unity` + `Hole_01_Geo.unity` additively.
2. Sets up driver shot from tee.
3. Schedules captures via the new §2b API:
   ```csharp
   CaptureCore.SnapWhenStateReached(this, ballSM, BallState.Aiming, "controls_g_2b_aiming");
   CaptureCore.SnapWhenStateReached(this, ballSM, BallState.Flying, "controls_g_2b_flying_chase");
   // Cinematic cut should fire ~1.5s into Flying; capture again after expected cut window.
   // NOTE: SnapWhenStateReached is one-shot per call. For mid-state captures (e.g. "Flying-after-Downrange-cut"),
   //       use a delayed second capture or Time.time-gated invoke from the runner.
   CaptureCore.SnapWhenStateReached(this, ballSM, BallState.Rolling, "controls_g_2b_rolling");
   CaptureCore.SnapWhenStateReached(this, ballSM, BallState.AtRest, "controls_g_2b_atrest");
   ```
4. Fires the shot via `ShotController.CommitFlick`.
5. Verifies on-disk file persistence + size + content-sanity per §2a Lessons M+N.

Repeat for putter shot on green (asserting GroundLevel preserved through Flying/Rolling/AtRest — no Downrange cut). Repeat for OB shot (Water-bordered tee setup; asserting OBFreeze fires and camera locks at first Water-hit XZ).

**Deliverable:** 4–6 captured frames with on-disk paths + sizes + visual descriptions in `IMPLEMENTER_REPORT.md` § "Phase C smoke evidence". Filed under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` with `controls_g_*` prefix per the §2b deferred-smoke OPEN flag.

#### C.5 — Remove all `[CONTROLS_G]` diagnosis prints (Q4 lock)

Walk the codebase, remove every `[CONTROLS_G]` log line and the `_diagPrintedFirstLift` static field. Verify with:
```
grep -rn "CONTROLS_G" Assets/Scripts/
```
Zero results required.

## Definition of Done

- Phase A diagnosis findings captured in `IMPLEMENTER_REPORT.md` with the actual logged values + identified root cause.
- Phase B fix: broken codepath restored; `AeroConfig.AssertValid` shipped + wired into `LoadAeroConfig`; aero divide audit comment block at top of `AeroModel.ComputeAeroForce`.
- Phase C tests: 211 pre-existing PASS (0 IGNORED) + 3 new `AeroConstantModeTests` PASS + 1 new `Aero_DriverShot_DoesNotThrow` tripwire PASS → **215/215 PASS, 0 IGNORED**.
- Phase C smoke: 4–6 captures landing under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` with `controls_g_*` prefix; on-disk verification + content-sanity per §2a Lessons M+N; closes §2b deferred-smoke OPEN flag.
- All `[CONTROLS_G]` diagnosis logs removed.
- Notion entry [`35931e0e-9a36-8163-a839-d5190f134f0f`](https://www.notion.so/35931e0e9a368163a839d5190f134f0f) flipped Status In Progress → Done with Closed=2026-05-07 (or whatever date).
- §2b deferred-smoke OPEN flag in TellCode.md marked CLOSED.

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** — escalate to architect if:
  - Phase A diagnosis values don't match any of the four hypotheses (A/B/C/D). Architect re-reads code and revises hypothesis set.
  - Phase B fix breaks the bit-exact 211 gate. Symptom: tests previously PASSing now FAIL with off-by-one or sign-flipped values. Architect investigates whether the fix accidentally retuned aerodynamics.
  - `AeroConfig.AssertValid` throws on the live `aero.csv` after fix. Means the fix didn't actually restore the value. Re-investigate.
  - Smoke capture fires but the captured frame doesn't show the expected camera mode (e.g. Downrange capture shows Chase). Means the cinematic cut formula needs re-tuning OR the SnapWhenStateReached gating fired at the wrong moment. Architect investigates whether the bug is in §2b's Director (re-open §2b) or in this task's smoke runner.
- **`IMPLEMENTER_PARTIAL`** — implementer ships Phase A + B clean, but Phase C smoke captures hit unforeseen friction (e.g. the SnapWhenStateReached API doesn't compose well with Director's mid-flight Downrange transition because Downrange is mode-set inside Flying state, not at a state boundary). Acceptable to ship Phase A + B as PASS_WITH_DEFERRAL, leave §2b smoke debt open, and queue a follow-up `controls_g_smoke_followup` for the smoke alone. Architect reviews and decides.

## Out of scope

- **Re-tuning aero LUTs or overlay multipliers.** controls_g is about LOADING them correctly, not changing the calibration. `aero_lift_lut.csv`, `aero_drag_lut.csv`, `aero_lift_overlay.csv`, `aero_drag_overlay.csv` data values are LOCKED.
- **fpMath repair of any kind.** `fpMath.Sqrt` was fixed in controls_d; no further fpMath work in controls_g.
- **PhysicsLabController / camera / SM changes.** Pure physics + config + tests + smoke. Camera work is §2b's; SM is §2a's.
- **PuttPathPredictor.** Separate spec at `Docs/Specs/Queued/puttpath_predictor_perf_and_design/`.
- **Reviewing whether constant-mode is the right fallback architecture.** It exists as a fallback for when the LUT is absent (e.g. fresh project, missing CSV). Don't redesign the architecture — fix the loading regression that made the fallback execute when the LUT should have been live.

## Hard rules for implementer

1. **Do NOT modify** `BallSimulation.cs`, `Trajectory.cs`, `TrajectorySample.cs`, `fpMath.cs`, `BallStateMachine.cs`, `BallState.cs`, any test currently in PASS state outside the new `AeroConstantModeTests.cs` and the one new tripwire row in `AeroCalibrationTripwireTests.cs`.
2. **Do NOT change LUT or overlay CSV data values.** `aero_lift_lut.csv`, `aero_drag_lut.csv`, `aero_lift_overlay.csv`, `aero_drag_overlay.csv` are calibration outputs from controls_e/f — modifying them would silently invalidate two days of calibration work.
3. **Do NOT add inline guards at AeroModel lines 29 or 63.** Architect-verified safe via the line-26 epsilon gate. Inline guards there would silently swallow future legitimate underflow signals.
4. **Do NOT add an inline guard at line 78.** The `AeroConfig.AssertValid` call from B.2 is the correct defense layer (catches at config-load with useful message). Inline guards would silently swallow the bug.
5. **Do NOT skip Phase A.** Diagnosis-then-fix is the entire point. Don't jump to fixing your favored hypothesis without the Console output proving it.
6. **Do NOT leave `[CONTROLS_G]` prints in the codebase.** Phase C.5 removes them all. `grep -rn "CONTROLS_G" Assets/Scripts/` MUST return zero results before merge.
7. **Smoke evidence per §2a Lessons M+N.** File persisted on disk + parallel-path Read verification + content-sanity. No Roslyn-only / in-memory captures pass review. Use the new `CaptureCore.SnapWhenStateReached` API exclusively for SM-state-gated captures.
8. **Bit-exact 211-test PASS gate must hold through Phase B.** If you suspect the fix is forcing a snapshot update (test expected values shifting), STOP and escalate `IMPLEMENTER_BLOCKED` — Cesar approves snapshot updates per-test, never bulk.
