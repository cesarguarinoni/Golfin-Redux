# NOTES — `controls_e_aero_overlay_pass` — Architect working notes

> **Working draft (data-anchored plan, not yet a SPEC).** SPEC.md follows after `controls_d_velocity_cap_diagnosis` lands and Cesar reviews this plan. Implementer reads SPEC.md, NOT this file.

**Created:** 2026-05-05 JST
**Architect:** Claude (claude.ai)
**Notion:** Will be created when this is queued (target: P1 High, Order 150, Phase `01. Putter P1`).
**Predecessor:** `controls_d_velocity_cap_diagnosis` (Sqrt fix). The tripwire test added there is the definition-of-done for this task.

## Architecture frame (locked with Cesar 2026-05-05)

- **Layer 1 — Core physics.** Bearman-Harvey 1976 lift LUT, drag LUT, surface k values, integrator math, fp arithmetic. Stays as faithful as possible to published real-world data. Edits to Layer 1 require a real-world citation OR a documented bug fix (like the Sqrt repair).
- **Layer 2 — Corner-case overlay.** A separate file/files that apply documented corrections **only where Layer 1 is invalid** (extrapolating past published range) **OR** where outcomes diverge from observed Tour-pro reality. Overlay is openly designed for feel; Layer 1 is openly designed for truth.

Boundary lives in code at the LUT-evaluator seam. The `LiftLut.Evaluate(spinParam)` call becomes `LiftLut.EvaluateBlended(spinParam, reynoldsNumber)` which internally combines Layer 1 truth + Layer 2 overlay with a smoothstep blend across the Bearman-Harvey valid-range boundary.

## Real-world data sources (the calibration anchor)

Trackman composite Tour data is the primary anchor — it's the de facto industry reference for "what real Tour pros produce" and is what AAA studios (PGA TOUR 2K23 dev blog explicitly) calibrate against. USGA equipment-test data is the secondary cross-check. Bearman-Harvey 1976 stays as the Layer 1 truth in its published range.

### Source 1 — Trackman PGA Tour averages (PRIMARY)

Trackman publishes per-club Tour averages for ball speed, launch angle, spin, peak height, descent angle, carry, and total. The numbers float a bit year-over-year but are stable to within a few yards. The composite below is what's most commonly cited in golf-instruction publications and matches PGA TOUR 2K23's dev-blog statements.

| Club | Ball speed (mph) | Launch (°) | Spin (rpm) | Peak height (yd) | Descent (°) | Carry (yd) |
|---|---|---|---|---|---|---|
| Driver | 167 | 10.9 | 2686 | 32 | 38 | 275 |
| 3-wood | 158 | 9.2 | 3655 | 30 | 43 | 243 |
| 5-iron | 142 | 14.8 | 5361 | 31 | 49 | 194 |
| 6-iron | 137 | 17.1 | 6231 | 30 | 50 | 183 |
| 7-iron | 132 | 16.3 | 7097 | 32 | 50 | 172 |
| 8-iron | 127 | 18.1 | 7998 | 31 | 51 | 160 |
| 9-iron | 120 | 20.4 | 8647 | 30 | 51 | 148 |
| PW | 113 | 24.2 | 9304 | 29 | 52 | 136 |

(Sourced from Trackman composite averages as referenced in golf-instruction publications and equivalent in the existing `Docs/Physics/PHYSICS_TUNING_TARGETS.md`. Values to be verified against current Trackman annual report by Cesar before SPEC lock.)

**Conversion note:** the existing test file uses m/s for ball speed; the table above uses mph (Trackman's native unit). 167 mph = 74.6 m/s, 132 mph = 59.0 m/s, etc. The implementer-side translation lives in the SPEC.

### Source 2 — USGA equipment-test data (CROSS-CHECK)

USGA publishes equipment-test data for ball aerodynamics under controlled conditions. Used to verify that the Trackman composite isn't an outlier — if Trackman says X and USGA says ~X, we calibrate to X. If they disagree by more than ~5%, flag for Cesar before locking the overlay.

USGA test conditions are NOT directly Tour-pro shots — they're standardized equipment validation. The cross-check is on the *aerodynamic coefficients themselves* (Cd, Cl as functions of S and Re), not on integrated outcomes. So it tells us "is the Bearman-Harvey curve still defensible at low S" rather than "is our 7-iron carry right."

### Source 3 — Bearman-Harvey 1976 (LAYER 1 TRUTH)

Already the basis of `aero_lift_lut.csv`. Published valid range:
- Spin parameter S ∈ [0.03, 0.30]
- Reynolds number Re ∈ [5×10⁴, 2×10⁵]
- Velocity ≥ 13 m/s (per Cornell SimScience interpretation)

Outside this range, Layer 1 is extrapolation — i.e., guessing. Layer 2 takes over.

### Source 4 — Aoki / Muto / Libii (TERTIARY, for reverse-Magnus and high-Re)

Multiple newer wind-tunnel studies (Aoki 2010, Libii 2012, others) extend Bearman-Harvey into the supercritical Re regime and into negative-lift conditions. These are NOT planned for ingestion in this pass — they'd be a future Layer 1 expansion (`controls_g_aero_layer1_extension` if we ever decide to). Mentioned here only so future-Cesar/future-Claude knows the option exists.

## What the overlay actually contains

A new file `Assets/Resources/Physics/aero_lift_overlay.csv` with the following structure:

```csv
spin_parameter,cl_multiplier,notes
0.00,1.000,Layer 1 valid; no overlay
0.10,1.000,Layer 1 valid; no overlay
0.20,1.000,Layer 1 valid; no overlay
0.25,1.000,Layer 1 valid; no overlay (Bearman-Harvey upper bound approaching)
0.30,1.000,Bearman-Harvey upper bound; overlay starts here
0.40,0.85,Tuned for 7-iron carry to land in 158-193 yd range
0.50,0.72,Tuned for 9-iron carry to land in 131-160 yd range
0.60,0.62,Tuned for PW carry to land in 104-127 yd range
```

(Values above are placeholders. Real values come from the iterative calibration in Step 5 below.)

The multiplier is applied to the Layer 1 lift coefficient post-evaluation:

```csharp
fp clLayer1 = liftLut.Evaluate(spinParam);
fp overlayMul = liftOverlay.EvaluateMultiplier(spinParam);  // 1.0 when no overlay applies
fp clFinal = clLayer1 * overlayMul;
```

A smoothstep blend around S=0.30 prevents discontinuity at the Layer 1 / Layer 2 seam:
- For S ≤ 0.25: pure Layer 1 (overlayMul forced to 1.0).
- For S ≥ 0.35: pure overlay multiplier from the table.
- For 0.25 < S < 0.35: smoothstep interpolation.

## The calibration loop (this is the meaty part)

This is the iterative procedure that produces the overlay multiplier values. It's the deliverable methodology for `Docs/Physics/CALIBRATION_METHODOLOGY.md`.

### Step 0 — Lock the targets

Cesar reviews the Trackman composite table above and confirms:
- The 8 clubs listed are the right calibration set.
- The carry values are the right targets.
- The ±10% tolerance window is the right acceptance criterion.

If Cesar wants tighter tolerance (e.g. ±5%), the overlay table needs more rows; if looser (e.g. ±15%), fewer. ±10% is the recommended balance.

### Step 1 — Build the calibration harness

A new EditMode test file `AeroCalibrationHarness.cs` (NOT a regular test — runs only on demand via a custom menu item like `GOLFIN > Physics > Run Aero Calibration Sweep`). It:

1. Loads the current `aero_lift_lut.csv` and `aero_lift_overlay.csv`.
2. For each of the 8 calibration clubs, runs `BallSimulation.Simulate` with the Trackman launch parameters.
3. Computes carry distance and compares to the Trackman target.
4. Prints a per-club error table: target / actual / error% / pass-fail at ±10%.
5. Optionally writes a CSV report to `Docs/Diagnostics/aero_calibration_<timestamp>.csv` for archival.

This harness is the Layer-2 equivalent of the Sqrt regression suite: it lets us re-run the calibration after any aero-related change and see if we drifted.

### Step 2 — First baseline run (Layer 2 disabled)

Run the harness with `aero_lift_overlay.csv` set to all 1.000 multipliers. This reproduces the current post-Sqrt-fix carries. Expected: driver close to target, irons/wedges 10-46% high. This is the diagnostic baseline.

### Step 3 — Iteratively tune the overlay multipliers

For each calibration club outside ±10%:
1. Identify the spin parameter S for that club's launch parameters: `S = BallRadius × ω / v`.
2. Look up the nearest overlay row(s) bracketing that S.
3. Adjust the overlay multiplier to bring carry closer to target. Rule of thumb: a multiplier change of -0.10 reduces carry by roughly 8-12% for high-S clubs (lift dominates trajectory at high S).
4. Re-run the harness. Iterate until ALL 8 clubs are within ±10%.

Termination condition: harness reports all-PASS, OR no further adjustment improves the worst-club error.

Expected iteration count based on the diagnostic numbers: 4-8 passes. Each pass is a CSV edit + harness run + read result, so ~5 minutes per pass = 30-40 minutes total.

### Step 4 — Lock the smoothstep blend

After multipliers are tuned, verify the seam at S∈[0.25, 0.35] doesn't introduce a visible discontinuity in the trajectory. Visualize by running a sweep of pseudo-clubs at S=0.20, 0.25, 0.27, 0.30, 0.33, 0.35, 0.40 and plotting carry vs S. Should be smooth, no kinks.

If there's a visible kink, widen the blend window (e.g. [0.20, 0.40] instead of [0.25, 0.35]) or move it (e.g. [0.27, 0.33]).

### Step 5 — Enable the tripwire test

The `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` test added in `controls_d`'s addendum gets `[Ignore]` removed. It must now PASS as a regular test in the suite. New gate: 210/210 PASS instead of 209 PASS + 1 IGNORED.

### Step 6 — Document everything

`Docs/Physics/CALIBRATION_METHODOLOGY.md` (NEW) gets written. Contains:
- The two-layer architecture frame.
- The Trackman composite table as the canonical target reference.
- The Bearman-Harvey valid range as the Layer 1 / Layer 2 boundary.
- The calibration harness usage instructions (menu item, output format).
- The smoothstep blend math.
- A "When to recalibrate" section listing triggers (e.g. "after any change to drag LUT", "after any ball-physics modifier reaches a non-Neutral default", "after any change to the integrator").

`Docs/Physics/PHYSICS_TUNING_TARGETS.md` warning section from `controls_d` gets updated/removed since the calibration now exists.

Top-of-file headers added to four Layer 2 CSVs as standing markers:
- `aero_lift_overlay.csv`: NEW file, header explains overlay role.
- `aero_lift_lut.csv`: header reaffirms Layer 1 status with B-H citation.
- `aero_drag_lut.csv`: header notes "Layer 1 status pending calibration audit (controls_f)".
- `surfaces.csv`, `putt.csv`: header notes "Layer 2 designable; calibrated against in-game feel, not real physics."

(The CSV header markers are cheap insurance against future-us mistaking these files for raw physics again.)

## What this task does NOT do

- Does NOT touch drag LUT (separate audit, `controls_f_drag_calibration_audit`, P3 Queued).
- Does NOT touch surface k values or putt k values (already calibrated in `controls_c_fix`; separate audit if/when needed).
- Does NOT extend Layer 1 with Aoki/Libii data (would be `controls_g_aero_layer1_extension`, not planned).
- Does NOT add a 2D Cl(S, Re) LUT. 1D + overlay is sufficient and matches AAA-studio practice.
- Does NOT promise Tour-pro accuracy at edge cases (negative-lift "blow up", reverse-Magnus, knuckleballs). Those are out-of-scope physics regimes.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| Overlay multipliers tuned to current ball-physics defaults; if ball stats become non-Neutral later (e.g. Power Ball ball type), calibration drifts. | Re-run the harness after any change to ball-physics defaults. CALIBRATION_METHODOLOGY.md "When to recalibrate" section names this trigger. |
| The 8-club calibration set leaves gaps (e.g. 4-iron, 3-iron, hybrids, lob wedge). | Acceptable for v1; add gap clubs in a follow-up if playtest reveals issues. The S range covered (0.08 to 0.45) spans the operating envelope. |
| Trackman composite values shift year-over-year. | Lock to a specific year's values (2024 or 2025 published) and note it in CALIBRATION_METHODOLOGY.md. Re-baselining is a separate task if needed. |
| Smoothstep blend hides bugs at the seam. | Step 4's sweep visualization explicitly tests for it. If a kink survives, the spec FAILs and we widen the window. |
| Future Layer 1 changes (drag LUT update, integrator tweak) silently invalidate the overlay calibration. | Tripwire test stays in the test suite forever. Any Layer 1 change that breaks the tripwire forces an explicit re-calibration decision. |

## Estimated cost

- SPEC writing: 1-2 hours architect-side.
- Implementer execution: half-day (CSV edits + harness creation + iteration + methodology doc + four CSV header additions).
- Review pipeline: another 1-2 hours total.
- **Total: ~1 working day.**

This is short enough to land before Loop v1 §2a (Ball state machine) without blocking it. Recommended sequencing: land `controls_d` (Sqrt + tripwire), land `controls_e` (this task), then start Loop v1.

## Open questions for Cesar (before SPEC writing)

1. **Trackman year:** lock to which year's published averages (2024, 2025)? Default to most recent.
2. **8-club set sufficient or expand:** add 4-iron, 3-iron, hybrid, lob wedge for a 12-club calibration set? More accurate, more iteration time.
3. **Acceptance tolerance:** stay at ±10% per club, or tighten to ±7% / loosen to ±15%?
4. **Calibration harness location:** new menu item under `GOLFIN > Physics > Run Aero Calibration Sweep`, or fold into the existing `Window > Physics > Tuning` window if one exists? (Architect to verify which surface exists before SPEC.)
5. **Overlay file format:** flat CSV as proposed, or upgrade to a piecewise polynomial spec (e.g. Bezier curve) so the overlay is smoother by construction? Flat CSV is simpler; Bezier is more elegant. Lean: flat CSV for v1.
