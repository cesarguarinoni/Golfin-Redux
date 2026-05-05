# SPEC — `controls_e_aero_overlay_pass` — Aero lift overlay calibration (Layer 2)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files. Architect's data-anchored plan + reasoning at `NOTES.md` (informational, not load-bearing).

**Created:** 2026-05-05 JST
**Architect:** Claude (claude.ai)
**Roadmap:** `Docs/Roadmap.md` §1 (Putter P1) — closing follow-up. Does NOT gate §2 (Loop v1) start; recommended to land before Loop v1 *playtest* feel.
**Notion:** [`35731e0e-9a36-8172-84e4-cdb4df5a0f81`](https://www.notion.so/35731e0e9a36817284e4cdb4df5a0f81) — `C.7 — Aero lift overlay calibration pass (Layer 2)` — P1 High → flipping to **In Progress** when this spec moves to Active/.
**Predecessor:** `controls_d_velocity_cap_diagnosis` (DONE 2026-05-05). The `[Ignore]`-tagged `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` test it added is the definition-of-done for this task.

## Status

See `STATUS.md` for current pipeline state.

## Goal

Implement a **Layer-2 corner-case overlay** for the aero lift coefficient that brings post-Sqrt-fix per-club carries within ±10% of Trackman 2025 PGA Tour averages, while preserving the existing `aero_lift_lut.csv` as faithful Bearman-Harvey 1976 transcription in its valid range.

The overlay applies a multiplicative correction to `Cl` only past the Bearman-Harvey valid range (S > 0.30) where the LUT is currently extrapolating, with a smoothstep blend across `S ∈ [0.25, 0.35]` to prevent seam discontinuity. In Layer-1-valid territory (S ≤ 0.25), the overlay multiplier is forced to 1.0 — Bearman-Harvey is trusted as-is.

After overlay calibration, the tripwire test from `controls_d` (`Aero_AllClubs_WithinTourCarryRange_PerSpinRegime`) has its `[Ignore]` attribute removed and must PASS as a regular test. New gate: 210/210 PASS instead of 209 PASS + 1 IGNORED.

## Architecture frame (locked with Cesar 2026-05-05)

GolfinRedux physics is structured as two layers:

- **Layer 1 — Core physics.** Bearman-Harvey 1976 lift LUT, drag LUT, surface k values, integrator math, fp arithmetic. Stays as faithful as possible to published real-world data. Edits to Layer 1 require a real-world citation OR a documented bug fix (like the Sqrt repair in `controls_d`).
- **Layer 2 — Corner-case overlay.** Separate file/files that apply documented corrections **only where Layer 1 is invalid** (extrapolating past published valid range) **OR** where outcomes diverge from observed Tour-pro reality. Overlay is openly designed for feel; Layer 1 is openly designed for truth.

Naming this boundary is the point. It's the universal pattern in deterministic-physics game development (PGA TOUR 2K23 dev blog: "refine the extremes"; Quora deterministic-physics consensus: "tunable for feel rather than physical realism"). Documenting it here makes the boundary auditable for future maintenance.

## Decision lock-ins (from NOTES.md Open Questions)

| Q | Lock |
|---|---|
| Trackman year | **2025 (most recent published)** |
| Calibration set | **8 clubs:** driver, 3-wood, 5-iron, 6-iron, 7-iron, 8-iron, 9-iron, PW |
| Tolerance | **±10% per club** (matches the tripwire test from `controls_d`) |
| Harness UI | **CLI-callable from Code's pipeline** (deterministic, captured in IMPLEMENTER_REPORT) **AND** a `MenuItem("GOLFIN/Physics/Run Aero Calibration Sweep")` for manual spot-checks. Both surfaces invoke the same harness method. |
| Overlay format | **Flat CSV** with one multiplier per spin-parameter row. Same parsing pattern as `aero_lift_lut.csv`. |

## Reference

- **Trackman PGA Tour 2025 averages.** Per-club ball-speed / launch-angle / spin-rate / carry. The implementer locks the exact 8-row table from Trackman's 2025 published numbers (or, if not available, from the latest Trackman annual; cite which year). The values in NOTES.md are an architect-time approximation — they need verification before the harness runs.
- **Bearman-Harvey 1976.** Already the basis of `aero_lift_lut.csv`. Published valid range: S ∈ [0.03, 0.30], Re ∈ [5×10⁴, 2×10⁵], v ≥ 13 m/s. Cited at top of `aero_lift_lut.csv` (NEW header, Step 7).
- **`controls_d_velocity_cap_diagnosis`** — predecessor task. The `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` tripwire test is in `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs`. This spec removes its `[Ignore]` attribute as the final step.
- **`Assets/Scripts/Physics/Core/CoefficientLut.cs`** — the existing piecewise-linear LUT struct. Reused unchanged for the overlay.
- **`Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.LoadLut`** — the existing CSV-loader pattern. Mirrored exactly for `LoadLiftOverlay`.
- **`Assets/Scripts/Physics/Core/AeroModel.cs:42-54`** — the lift-evaluation block. The overlay seam is inserted here.

## Architecture context

**Asmdef boundaries affected:** none. All edits are to existing files in existing assemblies plus new files in those assemblies. No asmdef edits.

**Existing code referenced (Implementer reads end-to-end before starting):**
- `Assets/Scripts/Physics/Core/AeroConfig.cs` — `AeroConfig` struct. Adds two new fields: `LiftOverlay` (CoefficientLut) and `UseLiftOverlay` (bool).
- `Assets/Scripts/Physics/Core/AeroModel.cs` — `ComputeAeroForce`. The overlay seam goes between line 47 (`cl = cfg.LiftLut.Evaluate(spinParam)`) and line 55 (`if (cl <= fp.Epsilon) return drag`).
- `Assets/Scripts/Physics/Core/CoefficientLut.cs` — generic LUT struct. **Not modified.** Reused for the overlay.
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — adds `LoadLiftOverlay()` method mirroring `LoadLiftLut()`. Adds parsing for new `aero.csv` key `use_lift_overlay`.
- `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` — the predecessor task's tripwire. The `[Ignore]` attribute is removed as the final step.
- `Assets/Resources/Physics/aero.csv` — adds one new row: `use_lift_overlay,1`.
- `Assets/Resources/Physics/aero_lift_lut.csv` — top-of-file header added (no values changed).

**No edits to:**
- `BallSimulation.cs` — the integrator. Lift overlay flows through `AeroModel.ComputeAeroForce` only.
- `fpMath.cs` or any math primitive.
- Any `.unity`, `.prefab`, scene file.
- Any `.asmdef`.
- `aero_drag_lut.csv` (separate audit, `controls_f`).
- `surfaces.csv`, `putt.csv` (already calibrated in `controls_c_fix`; only top-of-file header added in Step 7).
- `RollAndPuttTuningTests.cs` or any putt test (no spin → no overlay impact).
- The 209 existing EditMode tests (overlay defaults to no-op for S ≤ 0.25, and any test using S > 0.25 must already be either part of the calibration set or out-of-scope).

## Implementation

### Step 0 — Lock the Trackman 2025 calibration table

Open Trackman's 2025 PGA Tour averages publication (or the equivalent industry reference if Trackman 2025 is not published yet at implementation time — cite which source and year is used). Lock the following 8 rows. Use Trackman's native units in the source-of-truth table:

```
Club     | BallSpd(mph) | Launch(°) | Spin(rpm) | Carry(yd)
---------|--------------|-----------|-----------|----------
Driver   |     ?        |     ?     |     ?     |     ?
3-wood   |     ?        |     ?     |     ?     |     ?
5-iron   |     ?        |     ?     |     ?     |     ?
6-iron   |     ?        |     ?     |     ?     |     ?
7-iron   |     ?        |     ?     |     ?     |     ?
8-iron   |     ?        |     ?     |     ?     |     ?
9-iron   |     ?        |     ?     |     ?     |     ?
PW       |     ?        |     ?     |     ?     |     ?
```

**The implementer fills this table in `IMPLEMENTER_REPORT.md` § "Calibration targets" with the actual sourced values, citing the URL or document name.** The architect-time approximation in `NOTES.md` is a starting reference but MUST be verified.

If Trackman 2025 numbers differ from `NOTES.md` by more than 5% on any club, the implementer flags it but proceeds with the verified Trackman 2025 values. If they cannot be verified at all (e.g., Trackman has not published yet), use the most recent published year and cite it.

### Step 1 — Create `aero_lift_overlay.csv`

Create `Assets/Resources/Physics/aero_lift_overlay.csv` with a top-of-file Layer-2 header and a baseline all-1.000 multiplier table:

```csv
# Layer 2 — corner-case overlay. Tunable for game feel.
# This file applies a multiplicative correction to the Layer 1 lift coefficient
# (aero_lift_lut.csv, Bearman-Harvey 1976 transcription) ONLY where Layer 1 is
# extrapolating past its valid range (S > 0.30) OR where outcomes diverge from
# observed Tour-pro reality (Trackman 2025 PGA Tour averages).
# Smoothstep blend across S ∈ [0.25, 0.35] prevents seam discontinuity.
# See Docs/Physics/CALIBRATION_METHODOLOGY.md for full architecture frame.
spin_parameter,cl_multiplier,notes
0.00,1.000,Layer 1 valid; no overlay
0.20,1.000,Layer 1 valid; no overlay
0.25,1.000,Layer 1 valid; blend window starts
0.30,1.000,Bearman-Harvey upper bound; overlay starts taking effect
0.35,1.000,Blend window ends; pure overlay from here
0.40,1.000,Tuned in Step 5
0.50,1.000,Tuned in Step 5
0.60,1.000,Tuned in Step 5
```

The `1.000` placeholder values get tuned in Step 5. Comment column is informational only — the parser ignores it.

### Step 2 — Add overlay support to `AeroConfig`

Edit `Assets/Scripts/Physics/Core/AeroConfig.cs`:
- Add field: `public CoefficientLut LiftOverlay; // Cl multiplier(S). When IsValid=false OR UseLiftOverlay=false, no-op (multiplier=1.0).`
- Add field: `public bool UseLiftOverlay;`
- In `AeroConfig.Default` static constructor, set `UseLiftOverlay = false` (matches existing `UseLiftLut = false` default — overlay is opt-in via CSV).
- In any second constructor that hard-codes default values, also set `UseLiftOverlay = false`.

The overlay is independent of the lift LUT itself — `UseLiftLut=true, UseLiftOverlay=false` is a valid (and safe) configuration. The overlay only activates when both flags are true AND `LiftOverlay.IsValid`.

### Step 3 — Add overlay loader to `PhysicsConfigLoader`

Edit `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs`:

1. In the `aero.csv` key-parsing switch (around line 47), add:
```csharp
case "use_lift_overlay":      cfg.UseLiftOverlay       = (val != 0f);       break;
```

2. After `cfg.LiftLut = LoadLiftLut();` (line 53), add:
```csharp
cfg.LiftOverlay = LoadLiftOverlay();
```

3. Add a new public static method mirroring `LoadLiftLut`:
```csharp
public static CoefficientLut LoadLiftOverlay()
{
    return LoadLut("Physics/aero_lift_overlay", "spin_parameter", "cl_multiplier");
}
```

The existing `LoadLut` private helper handles parsing, header-skipping, and invalid-file fallback (returns `default(CoefficientLut)` which has `IsValid=false`).

### Step 4 — Insert overlay seam in `AeroModel`

Edit `Assets/Scripts/Physics/Core/AeroModel.cs`. Replace the lift-evaluation block (currently lines 42-54) with the overlay-aware version:

```csharp
// Lift (Magnus): ½ ρ A Cl(S) |vRel|² (ŵ × v̂rel)
// Spin parameter uses relative speed — dimple flow responds to airflow, not ground speed.
fp cl;
if (cfg.UseLiftLut && cfg.LiftLut.IsValid)
{
    fp spinParam = (cfg.BallRadius * spin.Rate) / speed;
    cl = cfg.LiftLut.Evaluate(spinParam);

    // Layer 2 corner-case overlay. Smoothstep blend across S ∈ [0.25, 0.35]
    // prevents discontinuity between Layer 1 (Bearman-Harvey valid) and overlay
    // (extrapolation territory). See Docs/Physics/CALIBRATION_METHODOLOGY.md.
    if (cfg.UseLiftOverlay && cfg.LiftOverlay.IsValid)
    {
        fp multRaw = cfg.LiftOverlay.Evaluate(spinParam);
        fp mult    = BlendOverlay(spinParam, multRaw);
        cl = cl * mult;
    }
}
else
{
    fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
    cl = cfg.LiftCoefficientBase * spinScale;
}

if (cl <= fp.Epsilon) return drag;

// ... rest unchanged
```

Add a private static helper to the same class:

```csharp
// Smoothstep-blended overlay multiplier. Returns 1.0 below S=0.25, full multiplier
// above S=0.35, smoothstep interpolation between. This preserves Layer 1
// (Bearman-Harvey) as canonical inside its valid range.
private static fp BlendOverlay(fp spinParam, fp overlayMultiplier)
{
    fp lo = fp.FromFloat(0.25f);
    fp hi = fp.FromFloat(0.35f);
    if (spinParam <= lo) return fp.One;
    if (spinParam >= hi) return overlayMultiplier;

    // Smoothstep: t² × (3 − 2t)
    fp t       = (spinParam - lo) / (hi - lo);
    fp two     = fp.FromFloat(2f);
    fp three   = fp.FromFloat(3f);
    fp smoothT = (t * t) * (three - (two * t));

    // Linear blend between 1.0 and overlayMultiplier using the smoothed t
    return fp.One + (overlayMultiplier - fp.One) * smoothT;
}
```

**Numerical note:** `fp.One`, `fp.FromFloat(2f)`, `fp.FromFloat(3f)` are the only constants used. The blend is fully fp-deterministic. The smoothstep formula is `t² × (3 − 2t)`, the standard cubic Hermite version (NOT `t³ × (6t² − 15t + 10)`, the 5th-order quintic — that's overkill).

### Step 5 — Update `aero.csv` to enable overlay

Add ONE new row to `Assets/Resources/Physics/aero.csv`:

```
use_lift_overlay,1
```

Place it adjacent to the existing `use_lift_lut,1` row (or wherever fits the file's existing convention). With this enabled, the overlay loads and applies on every shot.

### Step 6 — Build the calibration harness

Create `Assets/Scripts/Physics/Editor/AeroCalibrationHarness.cs` (NEW file, in a new `Editor/` subfolder under Physics if one doesn't exist; create it if needed). Wraps both UI surfaces around a single shared method:

```csharp
using UnityEditor;
using UnityEngine;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics;
// (other usings as needed)

namespace Golfin.Physics.Editor
{
    public static class AeroCalibrationHarness
    {
        [MenuItem("GOLFIN/Physics/Run Aero Calibration Sweep")]
        public static void RunFromMenu()
        {
            var report = RunCalibrationSweep();
            Debug.Log(report);
        }

        // Public entry point usable from CLI / pipeline / EditMode tests.
        // Returns a multi-line report string suitable for pasting into IMPLEMENTER_REPORT.md.
        public static string RunCalibrationSweep()
        {
            // 1. Load AeroConfig with current overlay settings.
            // 2. For each calibration club (8 rows from Trackman 2025), construct a
            //    ShotInput with the published launch parameters.
            // 3. Run BallSimulation.Simulate.
            // 4. Compute carry distance: 2D horizontal distance from launch point to
            //    landing point (first y-crossing below initial y).
            // 5. Compare to Trackman 2025 target. Compute error percentage.
            // 6. Format result row: "Driver: target=275yd actual=287yd err=+4.4% PASS"
            // 7. Return multi-line string with header + 8 rows + summary line.

            // Implementation details:
            // - Use the same launch-angle / ball-speed / spin-rate values as the tripwire
            //   test in AeroCalibrationTripwireTests.cs (read those values; they're the
            //   shared Trackman targets for this task).
            // - PASS = abs(error%) ≤ 10. FAIL otherwise.
            // - Summary line: "X/8 clubs PASS" + worst-error club name + worst-error %.
            // - If overlay multipliers all 1.000 (baseline), expect ~46% errors at PW —
            //   that's the diagnostic baseline.
        }
    }
}
```

The harness MUST:
- Be in an `Editor/` folder so it doesn't ship in the build (calibration is a dev tool).
- Be invokable from the menu (`GOLFIN > Physics > Run Aero Calibration Sweep`) AND callable from pipeline code via `AeroCalibrationHarness.RunCalibrationSweep()`.
- NOT modify any CSV. Read-only against the current overlay state.
- Print or return a deterministic, parseable text report. Same input → same output, every run.
- Cite the Trackman year/source in the report header.

### Step 7 — Iteratively tune `aero_lift_overlay.csv`

This is the meaty part. Run an iterative loop:

1. Run `AeroCalibrationHarness.RunCalibrationSweep()` (CLI from Code, OR menu item).
2. Read the per-club error table.
3. For each club outside ±10%:
   - Compute its spin parameter: `S = BallRadius × ω / v` (use BallRadius=0.02135m).
   - Find the overlay row(s) bracketing that S. (For an iron at S=0.30, the relevant row is `0.30,1.000` and `0.40,...`.)
   - If the club is over-carrying (actual > target), reduce the multiplier at the relevant row(s). Rule of thumb: -0.10 multiplier ≈ -8 to -12% carry at high S.
   - If the club is under-carrying (rare; possibly a row above S=0.50 over-corrected), increase the multiplier slightly.
4. Save the CSV. Re-run the harness.
5. Repeat until ALL 8 clubs are within ±10%.

Termination condition: harness reports `8/8 clubs PASS`, OR no further single-row adjustment improves the worst-club error (in which case escalate as `IMPLEMENTER_BLOCKED` — may need an additional row in the CSV at the worst-error club's S value).

Expected iteration count: 4-8 passes. Total tuning time: ~30-40 minutes if the harness runs in <1 min per pass.

**Document each iteration** in `IMPLEMENTER_REPORT.md` § "Calibration iterations":
- Iteration N: which row(s) changed, old → new value, rationale, resulting per-club error table.
- Final iteration: the all-PASS report.

### Step 8 — Verify the smoothstep seam

After multipliers are locked, run a sweep at the seam to verify no kink:

1. Construct 8 synthetic shots at the same ball-speed and launch angle but with spin rates chosen to land S at 0.20, 0.23, 0.25, 0.27, 0.30, 0.33, 0.35, 0.40.
2. Run each through `BallSimulation.Simulate`. Record carry.
3. The carry-vs-S curve should be smooth (monotonically decreasing with S past 0.25) and have no visible kink at 0.25 or 0.35.

If a kink is visible (e.g., a step of >5 yd in carry between adjacent S values), the smoothstep window is too narrow. Widen to `S ∈ [0.22, 0.38]` or `S ∈ [0.20, 0.40]` and re-run Step 7's calibration loop.

Document the seam check in `IMPLEMENTER_REPORT.md` § "Smoothstep verification" with the 8-row carry table and a one-line conclusion (`SEAM SMOOTH` or `KINK DETECTED, widened to [a, b]`).

### Step 9 — Write `Docs/Physics/CALIBRATION_METHODOLOGY.md`

Create this new file. Contents (all required):

1. **Two-layer architecture frame.** Explain Layer 1 (real physics) vs Layer 2 (corner-case overlay). Cite this spec by name as the doc that established the frame. Reference the Quora/PGA TOUR 2K23 industry-pattern justification.
2. **Trackman calibration target reference.** The 8-row Trackman 2025 (or whatever year was used) table with full citation. This is the canonical "what carries should match" reference.
3. **Bearman-Harvey valid range.** S ∈ [0.03, 0.30], Re ∈ [5×10⁴, 2×10⁵], v ≥ 13 m/s. Cite Cornell SimScience interpretation.
4. **Calibration harness usage.** Menu item path, CLI invocation pattern, output format, expected per-club PASS criteria.
5. **Smoothstep blend math.** The formula, the window `[0.25, 0.35]` (or whatever was locked in Step 8), the rationale.
6. **"When to recalibrate" section.** Explicit triggers requiring a re-run of the harness:
   - Any change to `aero.csv` (drag/lift coefficients, ball mass, ball radius, air density).
   - Any change to `aero_lift_lut.csv` or `aero_drag_lut.csv` (the Layer 1 LUTs).
   - Any change to ball-physics defaults (when ball stats become non-Neutral).
   - Any change to the integrator step size or ODE method in `BallSimulation.cs`.
   - Any change to `fpMath` primitives that affect aero computation (Sqrt, Cos, Sin, Cross, Dot).
   - Tour-pro reference data updates (Trackman annual report).
7. **Layer-1 sanctity rule.** Layer 1 LUTs may only be edited for: (a) bug fixes with documented evidence (e.g., the Sqrt fix), or (b) re-baselining against a NEW real-world data source with citation. Layer 1 is NEVER edited "to make a club feel right" — that's Layer 2's job.

Length target: 1-2 pages. This doc gets read by future-Cesar / future-Claude / future-implementer when they touch the aero stack; it should be skimmable and load-bearing without being verbose.

### Step 10 — Add Layer-status headers to other Layer-2 CSVs

Add a top-of-file comment block to each of these CSV files (the CSV parser already skips lines starting with `#`):

**`Assets/Resources/Physics/aero_lift_lut.csv`** — add at very top:
```
# Layer 1 — real physics. Bearman-Harvey 1976 transcription.
# Valid range: S ∈ [0.03, 0.30], Re ∈ [5×10⁴, 2×10⁵], v ≥ 13 m/s.
# DO NOT edit values to "make clubs feel right" — that's aero_lift_overlay.csv's job.
# Edits to this file require a real-world citation (new wind-tunnel data) OR a documented
# bug fix. See Docs/Physics/CALIBRATION_METHODOLOGY.md.
```

**`Assets/Resources/Physics/aero_drag_lut.csv`** — add at very top:
```
# Layer 1 — real physics. Bearman-Harvey 1976 transcription (drag side).
# Layer 2 audit pending: see controls_f_drag_calibration_audit.
# DO NOT edit to "make trajectories feel right" — that's a future drag overlay's job.
# See Docs/Physics/CALIBRATION_METHODOLOGY.md.
```

**`Assets/Resources/Physics/surfaces.csv`** — add at very top:
```
# Layer 2 — designable. Calibrated against in-game feel and lab observation.
# These are NOT real physics constants — they're rolling-resistance values tuned
# for gameplay. Re-tuned in controls_c_fix (2026-05-05).
# See Docs/Physics/CALIBRATION_METHODOLOGY.md.
```

**`Assets/Resources/Physics/putt.csv`** — add at very top:
```
# Layer 2 — designable. Calibrated against putt feel and Stimpmeter targets.
# These are NOT real physics constants — they're putt-specific resistance values.
# Re-tuned in controls_c_fix (2026-05-05).
# See Docs/Physics/CALIBRATION_METHODOLOGY.md.
```

(`aero_lift_overlay.csv` already has its header from Step 1.)

### Step 11 — Enable the tripwire test

Open `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs`. Remove the `[Ignore("...")]` attribute from `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime`.

The test should now PASS as a regular test (since Step 7's calibration brings all 4 clubs within ±10%). If it fails, return to Step 7 — the overlay is not yet calibrated correctly. Document the failure and which club drifted in `IMPLEMENTER_REPORT.md`.

### Step 12 — Run the full EditMode test suite

`Window > General > Test Runner > EditMode > Run All`. Expected: **210/210 PASS**, zero ignored.

If any of the 209 pre-existing tests fail, that's a regression — the overlay is leaking into Layer-1-valid territory. STOP, set STATUS to `IMPLEMENTER_BLOCKED`, and surface in IMPLEMENTER_REPORT.md. The smoothstep should ensure overlay is exactly 1.000 below S=0.25, so any failure here is a bug in the Step 4 implementation.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] Trackman 2025 (or specified year) calibration targets sourced and documented in IMPLEMENTER_REPORT.md § "Calibration targets" with citation.
- [ ] `aero_lift_overlay.csv` created with Layer-2 header and the locked multiplier values from Step 7.
- [ ] `AeroConfig.cs` has new fields `LiftOverlay` and `UseLiftOverlay`. Defaults are `false` / `IsValid=false` (no-op).
- [ ] `PhysicsConfigLoader.cs` has new `LoadLiftOverlay()` method, parses `use_lift_overlay` from `aero.csv`, and assigns `cfg.LiftOverlay`.
- [ ] `aero.csv` has new row `use_lift_overlay,1`.
- [ ] `AeroModel.cs` has the overlay seam from Step 4 (with `BlendOverlay` private helper).
- [ ] `BlendOverlay` returns exactly `fp.One` for `spinParam ≤ 0.25` (verified by inline test or by Step 12 result).
- [ ] `AeroCalibrationHarness.cs` created in `Assets/Scripts/Physics/Editor/`. MenuItem and CLI surface both work.
- [ ] Calibration sweep iterations documented in IMPLEMENTER_REPORT.md § "Calibration iterations" — every CSV row change is logged.
- [ ] Final calibration sweep reports `8/8 clubs PASS` (all within ±10% of Trackman target).
- [ ] Smoothstep seam check (Step 8) reports `SEAM SMOOTH` or documents the widened window if needed.
- [ ] `Docs/Physics/CALIBRATION_METHODOLOGY.md` written with all 7 required sections.
- [ ] Layer-status headers added to `aero_lift_lut.csv`, `aero_drag_lut.csv`, `surfaces.csv`, `putt.csv`.
- [ ] `[Ignore]` attribute removed from `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` in `AeroCalibrationTripwireTests.cs`.
- [ ] Final EditMode test gate: **210/210 PASS** (209 pre-existing + tripwire now-enabled). No ignored, no failed.
- [ ] No edits to `BallSimulation.cs`, `fpMath.cs`, `aero_drag_lut.cs`, `surfaces.csv` values, `putt.csv` values, or any of the 209 pre-existing test files.
- [ ] No new compiler warnings in Unity Console attributable to this task.

## Files this task touches

| File | Change |
|---|---|
| `Assets/Resources/Physics/aero_lift_overlay.csv` | NEW. The calibrated overlay multipliers. |
| `Assets/Resources/Physics/aero.csv` | Add `use_lift_overlay,1` row. |
| `Assets/Resources/Physics/aero_lift_lut.csv` | Add Layer-1 header (no value changes). |
| `Assets/Resources/Physics/aero_drag_lut.csv` | Add Layer-1 header (no value changes). |
| `Assets/Resources/Physics/surfaces.csv` | Add Layer-2 header (no value changes). |
| `Assets/Resources/Physics/putt.csv` | Add Layer-2 header (no value changes). |
| `Assets/Scripts/Physics/Core/AeroConfig.cs` | Add `LiftOverlay` and `UseLiftOverlay` fields. |
| `Assets/Scripts/Physics/Core/AeroModel.cs` | Add overlay seam in `ComputeAeroForce` + `BlendOverlay` helper. |
| `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` | Add `LoadLiftOverlay`, parse `use_lift_overlay`. |
| `Assets/Scripts/Physics/Editor/AeroCalibrationHarness.cs` | NEW. Calibration sweep runner. |
| `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` | Remove `[Ignore]` attribute. |
| `Docs/Physics/CALIBRATION_METHODOLOGY.md` | NEW. Two-layer architecture + harness usage doc. |

## Out of scope (do NOT do these)

- Do NOT touch `aero_drag_lut.csv` values. Drag audit is `controls_f_drag_calibration_audit` (P3, separate Notion entry).
- Do NOT touch `surfaces.csv` or `putt.csv` values (already calibrated in `controls_c_fix`).
- Do NOT touch any pre-existing test's expected values. The 209 tests stay at 209/209 PASS unchanged.
- Do NOT add a 2D Cl(S, Re) LUT. 1D + overlay is the chosen architecture.
- Do NOT extend Layer 1 with Aoki/Libii data. Future task (`controls_g`, not planned).
- Do NOT add edge-case physics (negative-lift "blow up", reverse-Magnus, knuckleballs). Out of scope.
- Do NOT change the `BallRadius` value or any other Layer-1 physics constant in `aero.csv`.
- Do NOT widen the smoothstep window past `[0.20, 0.40]` without escalating. The window is calibration-sensitive.
- Do NOT remove or alter the `controls_d` Sqrt fix in `fpMath.cs`. That's locked.
- Do NOT add dependencies on `System.Math` anywhere in the new code. Pure fp arithmetic.
- Do NOT add `using UnityEngine` to anything in the `Core` assembly. The harness is in `Editor` and can use it freely; `Core` stays Unity-free.

## Mid-task escalation paths

- **If Trackman 2025 numbers are not published / cannot be sourced:** use the most recent published year, cite which one in IMPLEMENTER_REPORT, proceed.
- **If a club cannot be brought within ±10% by single-row overlay adjustment:** add a new row at the worst-error club's S value, re-iterate. If after adding 2 rows the club still drifts, escalate as `IMPLEMENTER_BLOCKED` — may need a non-multiplicative correction (additive bias) or a smoothstep window adjustment.
- **If the smoothstep seam check shows a kink:** widen window to `[0.20, 0.40]` and re-run Step 7. If still kinked, escalate.
- **If the 209 pre-existing tests fail after the overlay is enabled:** STOP. The overlay is leaking into Layer-1-valid territory. Likely cause: `BlendOverlay` not returning exactly `fp.One` for `spinParam ≤ 0.25`. Set STATUS to `IMPLEMENTER_BLOCKED`.
- **If `Editor/` folder doesn't exist under `Physics/`:** create it. Add an `.asmdef` if needed mirroring the existing physics editor pattern (check `Assets/Scripts/Physics/Editor/Golfin.Physics.Editor.asmdef` if it exists). If creating an asmdef is needed, that's escalation territory — surface the question.
- **If MenuItem path conflicts with an existing menu:** check `GOLFIN/` namespace; if `Physics/` submenu exists, slot under it; otherwise create. Document the choice.

## Notion & roadmap administrivia (architect-side, NOT implementer's responsibility)

The architect (claude.ai chat) will, separately from the implementer pipeline:

- Flip Notion `35731e0e-9a36-8172-84e4-cdb4df5a0f81` to `In Progress` when this spec moves to Active/.
- Flip it to `Done` after Cesar's manual approval.
- Update `Docs/TellCode.md` and `Docs/AI_CONTEXT.md` to reflect this task is in flight.
- Schedule the followup `controls_f_drag_calibration_audit` review after Loop v1 §2a or §2b lands (whichever comes first).

The implementer just runs the pipeline.

## Pipeline lessons applied

From `Docs/Diagnostics/PIPELINE_LESSONS.md` and `controls_c_fix` / `controls_d` retrospectives:

- **Lesson F (architect overthinks past Cesar's diagnosis):** Cesar approved the architecture frame and 5 question locks. SPEC reflects those locked decisions; doesn't relitigate.
- **Lesson G (no thinking-aloud in specs):** scanned, none present.
- **Lesson H (architect verifies claims with sources):** Trackman / Bearman-Harvey / PGA TOUR 2K23 / IronWarrior all cited in NOTES.md and referenced here by short label. Implementer doesn't re-verify; the lock-ins were established at architect time.
- **Lesson from `controls_d`:** the `[Ignore]`-tagged tripwire test exists for a reason. It is the definition-of-done. If the calibration loop can't make it pass, that's the signal to escalate, NOT to weaken the test's tolerance.

## Why this task scopes the way it does

For posterity / future-Cesar reading this in 6 months:

The task is deliberately bounded to **lift only**. Drag could plausibly need overlay too (per `controls_f`'s Queued state), but tackling both at once muddles the calibration signal — if 7-iron carries are 12% high, is it lift over-prediction or drag under-prediction? You can't tell without isolating one. Doing lift first gives us a clean experiment: lift overlay tunes carry, then we observe whether drag-related metrics (peak height, descent angle) also need overlay. That's `controls_f`'s job.

This also keeps the spec landable in ~1 day. A combined lift+drag overlay would be 2-3x the iteration count (more variables to tune, more interactions).

The CALIBRATION_METHODOLOGY.md doc is the long-term value. Even if we never need to recalibrate again (we will), the doc means future-anyone touching aero knows what's tunable and what's not. That's the durable deliverable; the overlay multipliers themselves are just the first instantiation.
