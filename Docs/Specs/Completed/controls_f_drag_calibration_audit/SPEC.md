# SPEC — `controls_f_drag_calibration_audit` — Drag LUT calibration audit (Layer 2 sibling)

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files. Architect's data-anchored plan + reasoning at `NOTES.md` (informational, not load-bearing).

**Created:** 2026-05-05 (evening) JST
**Architect:** Claude (claude.ai)
**Roadmap:** `Docs/Roadmap.md` §1 (Putter P1) — final closing follow-up before §2 (Loop v1) starts. Closes the driver-carry gap left by `controls_e_aero_overlay_pass`.
**Notion:** [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) — `C.8 — Drag LUT calibration audit (driver carry blocker)` — P1 High → flipping to **In Progress** when this spec moves to Active/.
**Predecessor:** `controls_e_aero_overlay_pass` (DONE 2026-05-05). The `[Ignore]`-tagged `Aero_Driver_KnownPending_LayerOneAudit` test added in that task's tripwire is this spec's definition-of-done.

## Status

See `STATUS.md` for current pipeline state.

## Goal

Apply a **Layer-2 corner-case overlay to the drag coefficient** that brings driver carry within ±10% of Trackman 275yd target, while preserving the existing `aero_drag_lut.csv` as faithful Bearman-Harvey 1976 transcription in its valid range. After overlay calibration, the `Aero_Driver_KnownPending_LayerOneAudit` test (currently `[Ignore]`-tagged) PASSes as a regular test. Final gate: **211/211 PASS, 0 IGNORED.**

The architecture mirrors the lift overlay landed in `controls_e`: a new `aero_drag_overlay.csv` applies a multiplicative correction to `Cd` past Bearman-Harvey's valid Reynolds-number range, smoothstep-blended at the seam to prevent discontinuity. In Layer-1-valid territory (low ball speeds), the overlay multiplier is forced to 1.0 — Bearman-Harvey is trusted as-is.

The empirical signal: post-`controls_e`, driver carries 240.4yd vs 275yd target (−12.6%). Driver sits at S=0.08 (inside lift Bearman-Harvey valid range, so the lift overlay correctly excludes it by design) and Re≈2.17×10⁵ (just past Bearman-Harvey's published Re-valid upper bound). The miss is therefore a drag-side problem in Layer-1 extrapolation territory — exactly the corner case the two-layer architecture was designed to handle.

## Architecture frame (already locked in `controls_e`, applied here)

GolfinRedux physics is structured as two layers (formalized in `Docs/Physics/CALIBRATION_METHODOLOGY.md`):

- **Layer 1 — Core physics.** Bearman-Harvey 1976 transcription for both lift and drag LUTs, surface k values, integrator math, fp arithmetic. Stays as faithful as possible to published real-world data. Edits to Layer 1 require a real-world citation OR a documented bug fix.
- **Layer 2 — Corner-case overlay.** Separate file(s) that apply documented multiplicative corrections **only where Layer 1 is invalid** (extrapolating past published valid range) **OR** where outcomes diverge from observed Tour-pro reality. Overlay is openly designed for feel; Layer 1 stays openly designed for truth.

This task adds the **drag side** of the Layer-2 overlay. The lift side already exists from `controls_e`.

## Decision lock-ins (from NOTES.md, locked by Cesar 2026-05-05)

| Q | Lock |
|---|---|
| Seam location | **`v ∈ [45, 55]` m/s** (surgical option). Driver fully affected (>55 m/s for ~60% of flight); irons mostly unaffected (only 5-iron briefly touches the seam zone). Smoothstep blend across this 10 m/s range. |
| Iron tolerance after drag tune | **Strict ±10% per club** for all 4 calibration clubs (driver + 3 irons/wedge). Same as `controls_e`'s tripwire criterion. |
| Correction shape | **Multiplicative (Cd × m)**, mirroring the lift overlay pattern. Same `BlendOverlay`-style helper code structure. |
| Drag-crisis transition (v < 22 m/s) | **Do NOT touch.** Stays at Layer-1 Bearman-Harvey values. That region is for ball-rolling/landing physics, calibrated separately in `surfaces.csv` and `putt.csv`. |
| Trackman re-validation policy | **Document trigger in methodology, no auto-action.** When Trackman publishes their next annual (2026/2027), `CALIBRATION_METHODOLOGY.md` §6 already names this as a re-calibration trigger. We don't need a Notion entry now; we'll act when the data lands. |

## Reference

- **Trackman PGA Tour averages.** Driver target 275 yd carry @ 75 m/s ball speed, 10.9° launch, 2686 rpm. Sourced from the Trackman PDF YARDS table at `https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf`, cross-verified against `https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html`. **This value is verified per Lesson K** — same triple-checked target used in the `controls_d` tripwire and `controls_e` calibration. Do NOT re-source; trust the locked target.
- **Bearman-Harvey 1976** (Layer-1 truth). Published valid range: Re ∈ [5×10⁴, 2×10⁵] which converts to v ∈ [~18, ~70] m/s for golf-ball geometry. Steady-state Cd ≈ 0.22 in the supercritical regime, "nearly constant" past Re ~10⁵.
- **Smith et al. 2010 Kobe Univ. CFD validation:** confirms Bearman-Harvey behavior. *"Cd drops to around 0.22 at the supercritical Reynolds number (Re=1.1×10⁵)... stays at an almost constant value."* URL: `https://da.lib.kobe-u.ac.jp/da/kernel/90003493/90003493.pdf`.
- **Alam et al. 2011 multi-ball comparison** (cross-check): real Tour balls have Cd in the supercritical regime ranging 0.21–0.27 depending on dimple geometry. Our LUT's 0.23 is a defensible midpoint; tuning via overlay to ~0.21 simulates a slightly lower-drag ball model (e.g., modern Pro V1) without rewriting the published reference. URL: `https://www.researchgate.net/publication/251716774_A_study_of_golf_ball_aerodynamic_drag`.
- **Bearman-Harvey valid range upper bound** (~70 m/s = ~Re 2×10⁵). Driver at 75 m/s ball-speed sits ~7% past this upper bound. The overlay seam at v ∈ [45, 55] is well inside the published valid range, so the overlay engages on the speed regime that BH does cover at first transition, then gradually takes over at higher speeds where BH data is sparse.
- **`Docs/Physics/CALIBRATION_METHODOLOGY.md`** — the methodology doc landed in `controls_e`. This task adds a new §9 mirroring the existing §3 (lift overlay) for drag.
- **`Docs/Specs/Completed/controls_e_aero_overlay_pass/`** — predecessor task. Read SPEC.md and IMPLEMENTER_REPORT.md briefly. The architecture pattern, the harness, and the test-gate philosophy all carry over verbatim.

## Architecture context

**Asmdef boundaries affected:** none. All edits are to existing files in existing assemblies plus one new CSV file. No asmdef edits.

**Existing code referenced (Implementer reads end-to-end before starting):**

- `Assets/Scripts/Physics/Core/AeroConfig.cs` — `AeroConfig` struct. Adds two new fields: `DragOverlay` (CoefficientLut) and `UseDragOverlay` (bool). Mirror the existing `LiftOverlay` / `UseLiftOverlay` fields verbatim.
- `Assets/Scripts/Physics/Core/AeroModel.cs` — `ComputeAeroForce`. The drag overlay seam goes between line 32-33 (`cd = cfg.DragLut.Evaluate(speed)`) and line 35 (`fp dragScalar = ...`). Mirrors the existing lift-overlay seam pattern.
- `Assets/Scripts/Physics/Core/CoefficientLut.cs` — generic LUT struct. **Not modified.** Reused for the drag overlay.
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — adds `LoadDragOverlay()` method mirroring `LoadLiftOverlay()` exactly. Adds parsing for new `aero.csv` key `use_drag_overlay`.
- `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` — has the `[Ignore]`-tagged `Aero_Driver_KnownPending_LayerOneAudit` test. The `[Ignore]` attribute is removed as the final step. The other tests (`Aero_MidHighSpinClubs_WithinTourCarryRange`) MUST continue to pass — verify after each calibration iteration.
- `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` — extend (don't rewrite) to additionally print speed-bracketed drag-coefficient values per club so the implementer can see what the overlay is doing during iteration. Possibly add a second menu item for clarity.
- `Assets/Resources/Physics/aero.csv` — adds one new row: `use_drag_overlay,1`.
- `Assets/Resources/Physics/aero_drag_lut.csv` — top-of-file header updated (no value changes).

**No edits to:**
- `BallSimulation.cs` — the integrator. Drag overlay flows through `AeroModel.ComputeAeroForce` only.
- `fpMath.cs` or any math primitive.
- Any `.unity`, `.prefab`, or scene file.
- Any `.asmdef`.
- `aero_lift_lut.csv`, `aero_lift_overlay.csv` (closed in `controls_e`).
- `aero_drag_lut.csv` values (only its top-of-file header is updated).
- `surfaces.csv`, `putt.csv` values.
- The 210 active tests + 1 ignored tripwire (the only test edit is removing `[Ignore]` from `Aero_Driver_KnownPending_LayerOneAudit`).

## Implementation

### Step 0 — Trackman target (already locked, no sourcing needed)

Driver carry target: **275 yd ±10%** (range 247.5–302.5 yd). Same value as `controls_e`'s driver target. The implementer does NOT re-source this; the value is verified per Lesson K and locked across multiple tasks.

(For the iron/wedge regression check during iteration: 7-iron 172yd, 9-iron 148yd, PW 136yd — all ±10%. These are read from the existing `Aero_MidHighSpinClubs_WithinTourCarryRange` test, no separate sourcing needed.)

### Step 1 — Create `aero_drag_overlay.csv`

Create `Assets/Resources/Physics/aero_drag_overlay.csv` with a top-of-file Layer-2 header and a baseline all-1.000 multiplier table:

```csv
# Layer 2 — corner-case overlay. Tunable for game feel.
# This file applies a multiplicative correction to the Layer 1 drag coefficient
# (aero_drag_lut.csv, Bearman-Harvey 1976 transcription) ONLY where Layer 1 is
# extrapolating past its valid Reynolds-number range (v > ~70 m/s) OR where
# outcomes diverge from observed Tour-pro reality (Trackman 2024 PGA Tour driver
# carry: 275 yd; verified Trackman PDF YARDS table per Lesson K).
# Smoothstep blend across v ∈ [45, 55] m/s prevents seam discontinuity.
# See Docs/Physics/CALIBRATION_METHODOLOGY.md §9 for full architecture frame.
speed_mps,cd_multiplier,notes
0,1.000,Layer 1 valid; no overlay
22,1.000,Bearman-Harvey valid range
40,1.000,Layer 1 valid; smoothstep blend starts at 45
50,1.000,Smoothstep midpoint; tuned in Step 5
60,1.000,Iron-driver speed boundary; tuned in Step 5
70,1.000,Bearman-Harvey upper edge; tuned in Step 5
80,1.000,Driver peak operating speed; tuned in Step 5
100,1.000,Extrapolation clamp; tuned in Step 5
```

The `1.000` placeholder values get tuned in Step 5. Comment column is informational only — the parser ignores it.

### Step 2 — Add overlay support to `AeroConfig`

Edit `Assets/Scripts/Physics/Core/AeroConfig.cs`:

- After the existing `LiftOverlay` field (around line 28), add a parallel block:
```csharp
// Layer 2 drag — corner-case overlay (controls_f_drag_calibration_audit).
// Cd multiplier(speed). When IsValid=false OR UseDragOverlay=false, no-op (multiplier=1.0).
public CoefficientLut DragOverlay;
public bool UseDragOverlay;
```
- In `AeroConfig.Default` static constructor, add `UseDragOverlay = false` (matches existing `UseLiftOverlay = false` default — overlay is opt-in via CSV).
- In `AeroConfig.Vacuum` static constructor, add `UseDragOverlay = false`.
- Add a comment in the appropriate `Default` / `Vacuum` block: `// DragOverlay default-constructed (IsValid=false) — overlay is opt-in via CSV` (mirrors the existing `LiftOverlay` comment).

The overlay is independent of the drag LUT itself — `UseDragLut=true, UseDragOverlay=false` is a valid (and safe) configuration. The overlay only activates when both flags are true AND `DragOverlay.IsValid`.

### Step 3 — Add overlay loader to `PhysicsConfigLoader`

Edit `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs`:

1. In the `aero.csv` key-parsing switch (around the existing `use_lift_overlay` case), add:
```csharp
case "use_drag_overlay":      cfg.UseDragOverlay      = (val != 0f);       break;
```

2. After `cfg.LiftOverlay = LoadLiftOverlay();` (or wherever `cfg.LiftOverlay` is assigned), add:
```csharp
cfg.DragOverlay = LoadDragOverlay();
```

3. Add a new public static method mirroring `LoadLiftOverlay`:
```csharp
public static CoefficientLut LoadDragOverlay()
{
    return LoadLut("Physics/aero_drag_overlay", "speed_mps", "cd_multiplier");
}
```

The existing `LoadLut` private helper handles parsing, header-skipping, and invalid-file fallback (returns `default(CoefficientLut)` which has `IsValid=false`).

### Step 4 — Insert overlay seam in `AeroModel`

Edit `Assets/Scripts/Physics/Core/AeroModel.cs`. The existing drag block at lines 32-37 reads:

```csharp
fp cd = (cfg.UseDragLut && cfg.DragLut.IsValid)
    ? cfg.DragLut.Evaluate(speed)
    : cfg.DragCoefficient;

fp dragScalar = halfRhoV2 * cd * cfg.BallCrossSection;
fp3 drag = vRelHat * (-dragScalar);
```

Replace with the overlay-aware version:

```csharp
fp cd;
if (cfg.UseDragLut && cfg.DragLut.IsValid)
{
    cd = cfg.DragLut.Evaluate(speed);

    // Layer 2 corner-case overlay (controls_f_drag_calibration_audit).
    // Smoothstep blend across v ∈ [45, 55] m/s prevents discontinuity between
    // Layer 1 (Bearman-Harvey valid) and overlay (extrapolation territory).
    // See Docs/Physics/CALIBRATION_METHODOLOGY.md §9.
    if (cfg.UseDragOverlay && cfg.DragOverlay.IsValid)
    {
        fp multRaw = cfg.DragOverlay.Evaluate(speed);
        fp mult    = BlendDragOverlay(speed, multRaw);
        cd = cd * mult;
    }
}
else
{
    cd = cfg.DragCoefficient;
}

fp dragScalar = halfRhoV2 * cd * cfg.BallCrossSection;
fp3 drag = vRelHat * (-dragScalar);
```

Add a private static helper to the same class (place adjacent to the existing `BlendOverlay` helper, mirror its structure exactly):

```csharp
// Smoothstep-blended drag overlay multiplier. Returns 1.0 below v=45 m/s,
// full multiplier above v=55 m/s, smoothstep interpolation between.
// This preserves Layer 1 (Bearman-Harvey) as canonical inside its valid range.
private static fp BlendDragOverlay(fp speed, fp overlayMultiplier)
{
    fp lo = fp.FromFloat(45f);
    fp hi = fp.FromFloat(55f);
    if (speed <= lo) return fp.One;
    if (speed >= hi) return overlayMultiplier;

    // Smoothstep: t² × (3 − 2t)
    fp t       = (speed - lo) / (hi - lo);
    fp two     = fp.FromFloat(2f);
    fp three   = fp.FromFloat(3f);
    fp smoothT = (t * t) * (three - (two * t));

    // Linear blend between 1.0 and overlayMultiplier using the smoothed t
    return fp.One + (overlayMultiplier - fp.One) * smoothT;
}
```

**Numerical note:** `fp.One`, `fp.FromFloat(2f)`, `fp.FromFloat(3f)`, `fp.FromFloat(45f)`, `fp.FromFloat(55f)` are the only constants used. The blend is fully fp-deterministic. Same smoothstep formula as `controls_e`'s `BlendOverlay` — `t² × (3 − 2t)`, the cubic Hermite version.

### Step 5 — Update `aero.csv` to enable overlay

Add ONE new row to `Assets/Resources/Physics/aero.csv`:

```
use_drag_overlay,1
```

Place it adjacent to the existing `use_lift_overlay,1` row. With this enabled, the drag overlay loads and applies on every shot.

### Step 6 — Extend the calibration harness

Edit `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs`. The existing harness from `controls_e` already runs the 8-club sweep with current overlay state. Extend it (don't rewrite) so the per-club output also includes:

- Maximum speed reached during flight (m/s)
- Time spent above the overlay seam upper bound (v > 55 m/s) as a percentage of flight time
- Time spent below the overlay seam lower bound (v < 45 m/s) as a percentage of flight time
- Time spent in the seam (45 ≤ v ≤ 55) as a percentage of flight time

This is purely diagnostic output — it doesn't change the calibration logic. It exists so the implementer can read the harness output and confirm that the driver's overlay engagement looks right (~60% above seam) and irons aren't being unduly affected (mostly below seam).

Implementation hint: track `vMax` and `timeAboveSeam` / `timeBelowSeam` / `timeInSeam` accumulators inside the existing per-step loop. Add 4 new columns to the harness's report format. Total file addition: ~30 lines.

If the implementer prefers, add a new menu item `GOLFIN/Physics/Run Drag Calibration Sweep` that invokes the same `RunCalibrationSweep()` method but with a flag to print the verbose drag breakdown. Both menu items shouldn't duplicate code; share the underlying method.

### Step 7 — Iteratively tune `aero_drag_overlay.csv`

This is the meaty part. Run an iterative loop:

1. Run `AeroCalibrationHarness.RunCalibrationSweep()` (CLI from Code, OR menu item).
2. Read the per-club error table.
3. **Driver tune:** if driver is outside ±10% of 275yd:
   - Identify which overlay rows the driver spends time in (will be primarily 60, 70, 80 m/s rows for driver since it spends ~60% of flight at v > 55).
   - Reduce the multiplier at those rows. Rule of thumb: multiplier change of −0.05 (e.g., 1.000 → 0.950) at the 60-80 m/s rows reduces drag by ~5% in that regime, adding roughly 8-12 yd to driver carry. Architect-time prediction: final multipliers will land around **0.90 at v=80** (matches the architect's back-of-envelope ~9% drag reduction, ~25-30 yd added carry).
4. **Iron/wedge regression check:** after each driver-targeted tune, verify all 3 iron/wedge clubs are STILL within ±10% per Q2 lock. If any iron drops out:
   - Most likely cause: the overlay seam is too low; irons are being affected when they shouldn't be.
   - Fix: increase the multiplier at v=50 (the seam midpoint) back toward 1.000, OR reduce the multipliers at v=60 by less aggressively (e.g., 0.95 → 0.97).
5. **5-iron special case:** 5-iron has the highest ball speed of the irons (63 m/s). It briefly enters the seam region (45-55 m/s) and lightly grazes v > 55 near launch. Watch its error specifically; if it drifts more than ~3% from current −2.4% baseline, that's a sign overlay is leaking too much.
6. Save the CSV. Re-run the harness. Iterate.

Termination condition: harness reports `8/8 clubs PASS` AND driver passes within ±10% AND irons/wedge stay within ±10%, OR no further single-row adjustment improves the worst-club error.

Expected iteration count: 3-5 passes. Total tuning time: ~20-30 minutes if the harness runs in <30s per pass.

**Document each iteration** in `IMPLEMENTER_REPORT.md` § "Calibration iterations":
- Iteration N: which row(s) changed, old → new value, rationale, resulting per-club error table.
- Final iteration: the all-PASS report.

### Step 8 — Verify the smoothstep seam

After multipliers are locked, run a sweep at the seam to verify no kink:

1. Construct synthetic shots at the same launch angle and spin but with ball speeds chosen to peak at v = 40, 43, 45, 48, 50, 52, 55, 58, 60 m/s. Use a fixed spin rate (e.g., 4500 rpm, mid-iron territory) for all of them so the only varying parameter is speed.
2. Run each through `BallSimulation.Simulate`. Record carry distance.
3. The carry-vs-launch-speed curve should be smooth (monotonically increasing) and have no visible kink at v=45 or v=55.

If a kink is visible (e.g., a step of >3 yd in carry between adjacent ball-speed values), the smoothstep window is too narrow. Widen to `v ∈ [40, 60]` m/s and re-run Step 7's calibration loop with the wider window.

Document the seam check in `IMPLEMENTER_REPORT.md` § "Smoothstep verification" with the 9-row carry table and a one-line conclusion (`SEAM SMOOTH` or `KINK DETECTED, widened to [a, b]`).

### Step 9 — Enable the tripwire test

Open `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs`. Find the `Aero_Driver_KnownPending_LayerOneAudit` test. Remove the `[Ignore("...")]` attribute entirely. The test must now PASS as a regular `[Test]`.

The test should now pass since Step 7's calibration brings driver within ±10% of 275yd. If it fails, return to Step 7 — the overlay is not yet calibrated correctly. Document the failure and which club drifted in `IMPLEMENTER_REPORT.md`.

### Step 10 — Update `Docs/Physics/CALIBRATION_METHODOLOGY.md`

The methodology doc landed in `controls_e` with 8 sections. This task adds a **new §9** and updates **§8**.

**Add §9 — When to add a Layer-2 drag overlay:**

Mirror the existing §3 (lift overlay) format:
- Architecture restatement (Layer 1 = Bearman-Harvey, Layer 2 = corner-case correction).
- Trigger conditions: when integrated trajectory outcomes diverge from Tour-pro reality at speeds where Bearman-Harvey extrapolates past its valid Re range, AND when the gap is on the drag side (not lift, not integrator, not launch parameters).
- Smoothstep math reference back to §3.
- Worked example: this task's calibration. Driver carry was 240.4 (target 275); after 0.90× multiplier at v=80 m/s, carry lands within ±10%. Irons unaffected because seam is at v∈[45, 55].
- "When to recalibrate" delta: re-run the harness whenever Bearman-Harvey reference is updated, when ball physics modifiers are introduced, OR when Trackman publishes a new annual that diverges from current targets by more than ~3yd.

**Update §8 — What to do when an in-Bearman-Harvey-valid-range club misses target:**

Replace any TODO / "open follow-up" language with: "Closed by `controls_f_drag_calibration_audit` (2026-05-05). The answer is exactly what that task did: add a Layer-2 drag overlay at the appropriate seam, smoothstep-blended, calibrated against Trackman targets. See §9."

Length target: ~½-page added (§9). §8 update is one paragraph.

### Step 11 — Update layer-status header on `aero_drag_lut.csv`

Currently reads:
```
# Layer 1 — real physics. Bearman-Harvey 1976 transcription (drag side).
# Layer 2 audit pending: see controls_f_drag_calibration_audit.
# DO NOT edit to "make trajectories feel right" — that's a future drag overlay's job.
# See Docs/Physics/CALIBRATION_METHODOLOGY.md.
```

Replace with:
```
# Layer 1 — real physics. Bearman-Harvey 1976 transcription (drag side).
# Layer 2 overlay applied: see aero_drag_overlay.csv and CALIBRATION_METHODOLOGY.md §9.
# DO NOT edit to "make trajectories feel right" — that's aero_drag_overlay.csv's job.
# See Docs/Physics/CALIBRATION_METHODOLOGY.md.
```

(One-line update. No CSV value changes.)

### Step 12 — Run the full EditMode test suite

`Window > General > Test Runner > EditMode > Run All`. Expected: **211/211 PASS**, zero ignored.

If any of the 210 pre-existing active tests fail, that's a regression — the overlay is leaking into Layer-1-valid territory. STOP, set STATUS to `IMPLEMENTER_BLOCKED`, and surface in IMPLEMENTER_REPORT.md. The smoothstep should ensure overlay is exactly 1.000 below v=45, so any failure here is a bug in the Step 4 implementation.

If `Aero_Driver_KnownPending_LayerOneAudit` fails after `[Ignore]` removal, return to Step 7 to continue tuning. Don't re-add `[Ignore]` to make the test pass — that defeats the purpose.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] `aero_drag_overlay.csv` created with Layer-2 header (Step 1) and the locked multiplier values from Step 7's iteration.
- [ ] `AeroConfig.cs` has new fields `DragOverlay` and `UseDragOverlay`. Defaults are `false` / `IsValid=false` (no-op) in both `Default` and `Vacuum` constructors.
- [ ] `PhysicsConfigLoader.cs` has new `LoadDragOverlay()` method, parses `use_drag_overlay` from `aero.csv`, and assigns `cfg.DragOverlay`.
- [ ] `aero.csv` has new row `use_drag_overlay,1`.
- [ ] `AeroModel.cs` has the drag overlay seam from Step 4 (with `BlendDragOverlay` private helper).
- [ ] `BlendDragOverlay` returns exactly `fp.One` for `speed ≤ 45` (verified by inline test, by Step 12 result, OR by inspection — only the `<= lo` short-circuit can produce this).
- [ ] `AeroCalibrationHarness.cs` extended with vMax / time-above-seam / time-below-seam / time-in-seam columns (Step 6). Existing menu item still works; optionally a new `Run Drag Calibration Sweep` menu item added.
- [ ] Calibration sweep iterations documented in IMPLEMENTER_REPORT.md § "Calibration iterations" — every CSV row change is logged with rationale.
- [ ] Final calibration sweep reports **8/8 clubs PASS** (driver within ±10% of 275yd, irons/wedge within ±10% of their respective Trackman targets).
- [ ] Smoothstep seam check (Step 8) reports `SEAM SMOOTH` or documents the widened window if needed.
- [ ] `Docs/Physics/CALIBRATION_METHODOLOGY.md` has new §9 added (drag overlay) + §8 updated to reference §9.
- [ ] Layer-status header on `aero_drag_lut.csv` updated per Step 11.
- [ ] `[Ignore]` attribute removed from `Aero_Driver_KnownPending_LayerOneAudit` in `AeroCalibrationTripwireTests.cs`. Test now PASSes.
- [ ] Final EditMode test gate: **211/211 PASS, 0 IGNORED.** No new tests created beyond enabling the existing `Aero_Driver_KnownPending_LayerOneAudit`.
- [ ] No edits to `BallSimulation.cs`, `fpMath.cs`, `aero_lift_lut.csv`, `aero_lift_overlay.csv`, `aero_drag_lut.csv` values, `surfaces.csv`, `putt.csv`, or any of the 210 pre-existing active tests.
- [ ] No new compiler warnings in Unity Console attributable to this task.

## Files this task touches

| File | Change |
|---|---|
| `Assets/Resources/Physics/aero_drag_overlay.csv` | NEW. The calibrated drag overlay multipliers. |
| `Assets/Resources/Physics/aero.csv` | Add `use_drag_overlay,1` row. |
| `Assets/Resources/Physics/aero_drag_lut.csv` | Update Layer-1 header (no value changes). |
| `Assets/Scripts/Physics/Core/AeroConfig.cs` | Add `DragOverlay` and `UseDragOverlay` fields, mirror `LiftOverlay` pattern. |
| `Assets/Scripts/Physics/Core/AeroModel.cs` | Add drag overlay seam in `ComputeAeroForce` + `BlendDragOverlay` private helper, mirror lift overlay pattern. |
| `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` | Add `LoadDragOverlay`, parse `use_drag_overlay`. |
| `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` | Extend with speed-bracket diagnostic columns. |
| `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` | Remove `[Ignore]` from `Aero_Driver_KnownPending_LayerOneAudit`. |
| `Docs/Physics/CALIBRATION_METHODOLOGY.md` | Add §9 (drag overlay), update §8 (close the open follow-up). |

## Out of scope (do NOT do these)

- Do NOT touch `aero_drag_lut.csv` values. Only its top-of-file header.
- Do NOT touch `aero_lift_lut.csv` or `aero_lift_overlay.csv`. Lift is closed in `controls_e`.
- Do NOT touch `surfaces.csv` or `putt.csv`. Surface and putt physics are calibrated separately.
- Do NOT add additional clubs to the calibration set. The 8-club set from `controls_e` stays locked.
- Do NOT change the smoothstep window from `[45, 55]` unless Step 8 reveals a kink and widening is needed.
- Do NOT touch the v < 22 m/s drag-crisis transition region. Locked per Q4.
- Do NOT add a 2D Cd(v, S) LUT. 1D + overlay is sufficient and matches AAA-studio practice.
- Do NOT extend Layer 1 with Aoki / Libii / Smith CFD data as new rows. Future task only.
- Do NOT add edge-case physics (negative-lift "blow up", reverse-Magnus, knuckleballs).
- Do NOT change `BallRadius`, `BallMass`, `BallCrossSection`, `AirDensity`, or any other Layer-1 physics constant in `aero.csv`.
- Do NOT remove or alter the `controls_d` Sqrt fix in `fpMath.cs`. That's locked.
- Do NOT remove or alter the `controls_e` lift overlay or its tests.
- Do NOT add dependencies on `System.Math` anywhere in the new code. Pure fp arithmetic.
- Do NOT add `using UnityEngine` to anything in the `Core` assembly.

## Mid-task escalation paths

- **If iron/wedge clubs drop out of ±10% during driver tune** and no overlay row adjustment recovers them: escalate as `IMPLEMENTER_BLOCKED`. May need a different seam location or additive offset instead of multiplier (would re-open Q3). Document the specific iron's drift in IMPLEMENTER_REPORT.
- **If the smoothstep seam check shows a kink:** widen window to `[40, 60]` and re-run Step 7. If still kinked, escalate.
- **If multiplier 0.85 at v=80 doesn't close the driver gap to ±10%:** the gap isn't drag (or isn't only drag). Stop tuning. Set STATUS to `IMPLEMENTER_BLOCKED`. Suspect causes: lift in low-S regime that the lift overlay doesn't touch, integrator step size, launch parameter interpretation. Architect investigates separately.
- **If the 210 pre-existing active tests fail after the overlay is enabled:** STOP. The overlay is leaking into Layer-1-valid territory (likely `BlendDragOverlay` not returning exactly `fp.One` for `speed ≤ 45`). Set STATUS to `IMPLEMENTER_BLOCKED`.

## Notion & roadmap administrivia (architect-side, NOT implementer's responsibility)

The architect (claude.ai chat) will, separately from the implementer pipeline:

- Flip Notion `35731e0e-9a36-818d-9a4c-ee8dd9ca511c` to `In Progress` when this spec moves to Active/.
- Flip it to `Done` after Cesar's manual approval.
- Update `Docs/TellCode.md` and `Docs/AI_CONTEXT.md` to reflect this task is in flight.
- After this task closes end-to-end, the C-cluster physics work is COMPLETE and Loop v1 §2a (Ball state machine) becomes the next major spec.

The implementer just runs the pipeline.

## Pipeline lessons applied

From `Docs/Diagnostics/PIPELINE_LESSONS.md` and `controls_c_fix` / `controls_d` / `controls_e` retrospectives:

- **Lesson F (architect overthinks past Cesar's diagnosis):** Cesar locked the 5 questions explicitly. SPEC reflects those locks; doesn't relitigate.
- **Lesson G (no thinking-aloud in specs):** scanned, none present.
- **Lesson H (architect verifies claims with sources):** Trackman target (275yd) is verified per Lesson K from `controls_e`'s sourcing pass — no re-verification needed. Bearman-Harvey / Smith CFD / Alam multi-ball references are cited with URLs in NOTES.md.
- **Lesson K (unit verification):** the 275yd driver target is YARDS, verified, locked, annotated. The new overlay CSV uses `speed_mps` (consistent with the lift overlay's `spin_parameter`). Unit suffixes are consistent throughout.
- **Lesson from `controls_e`:** the calibration loop is short (3-5 iterations expected) because the architecture is symmetric to what we just built. Reuse the patterns; don't reinvent. The harness exists; extend, don't rewrite.

## Why this task scopes the way it does

For posterity / future-Cesar reading this in 6 months:

The C-cluster (controls_c through controls_f) is the gateway between "lab physics that compiles and runs" and "lab physics that produces Tour-pro carries with a documented two-layer architecture." Each task in this cluster closed a specific layer of that gap:

- `controls_c` — surface and putt rolling resistance.
- `controls_d` — fpMath.Sqrt convergence (caused the original 64-m/s velocity cap).
- `controls_e` — Layer-2 lift overlay for high-S regime (irons/wedges).
- `controls_f` (this task) — Layer-2 drag overlay for high-Re regime (driver).

After this lands, every published Tour-pro club averages within ±10% of Trackman targets in the lab simulation. Layer 1 (Bearman-Harvey) stays canonical and untouched outside its valid range. Layer 2 (overlays) is openly tunable for game-feel without polluting Layer 1. This is the AAA-studio pattern made explicit and documented in `CALIBRATION_METHODOLOGY.md`.

After this task: Loop v1 §2a (Ball state machine) is the next umbrella. The physics base is locked.
