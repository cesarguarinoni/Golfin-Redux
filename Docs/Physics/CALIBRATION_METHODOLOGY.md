# Aero Physics Calibration Methodology

**Established:** controls_e_aero_overlay_pass (2026-05-05)
**Updated:** controls_e_aero_overlay_pass iteration 2 (2026-05-05) — corrected Trackman targets per Lesson K, added §8
**Status:** Active — first instantiation of the two-layer architecture

---

## 1. Two-Layer Architecture Frame

GolfinRedux physics is structured as two explicit layers. The boundary between them is intentional and auditable.

**Layer 1 — Real physics.**
Bearman-Harvey 1976 wind-tunnel data for golf ball aerodynamics. Drag LUT and Lift LUT are direct transcriptions of published experimental data. Layer 1 is the simulation's "ground truth" — it only changes when new real-world data is available OR a documented bug is fixed (precedent: the Sqrt fix in `controls_d_velocity_cap_diagnosis`).

Files: `aero_lift_lut.csv`, `aero_drag_lut.csv`

**Layer 2 — Corner-case overlay.**
Separate files that apply documented corrections only where Layer 1 is extrapolating past its published valid range (S > 0.30) OR where outcomes diverge from observed Tour-pro reality. Layer 2 is openly designed for feel and tuned against Trackman Tour averages. It does not change the underlying physics model — it multiplies the output of Layer 1 where Layer 1 is least reliable.

Files: `aero_lift_overlay.csv`, `surfaces.csv`, `putt.csv`

This two-layer pattern is the industry standard in deterministic-physics game development. The PGA TOUR 2K23 dev blog describes it as "refine the extremes" — high-fidelity physics core with tunable overlays for edge cases. The pattern is discussed in Quora's deterministic-physics consensus threads under "tunable for feel rather than physical realism."

Established by this spec (`controls_e_aero_overlay_pass`). Future maintainers: the boundary is here to stay. If you want to change how a club feels, edit Layer 2. If you have new wind-tunnel data, edit Layer 1 with a citation.

---

## 2. Trackman Calibration Target Reference

**Source (corrected per Lesson K — unit-mismatch fix, 2026-05-05):**
- Primary: Trackman PGA-AVERAGES-INTERACTIVE PDF, YARDS table row
  URL: `https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf`
- Cross-verification: Maryland Golf Camps article (275/172/148/136 confirmed)
  URL: `https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html`

**Lesson K warning:** earlier iterations of this spec used wrong targets derived from a METERS table
in the same PDF. The corrected YARDS row values are canonical. All future target updates must cite
the specific table name (YARDS vs METERS) and unit explicitly in both code comments and this doc.

The tripwire test (`AeroCalibrationTripwireTests.cs`) uses 4 clubs. These are the corrected calibration targets:

| Club | Ball Speed (mph) | Ball Speed (m/s) | Launch (deg) | Spin (rpm) | Carry (yd) target | Tolerance |
|------|-----------------|-----------------|-------------|-----------|------------------|-----------|
| Driver | 167 | 75.0 | 10.9 | 2686 | 275 | ±10% = [247.5, 302.5] |
| 7-iron | 118 | 52.5 | 16.3 | 7097 | 172 | ±10% = [154.8, 189.2] |
| 9-iron | 109 | 48.5 | 20.0 | 8647 | 148 | ±10% = [133.2, 162.8] |
| PW | 103 | 46.0 | 24.0 | 9300 | 136 | ±10% = [122.4, 149.6] |

Note: The driver target (275yd) is NOT met by the current physics — driver produces ~240yd carry
(~12.7% below target). This is a Layer-1 drag-LUT issue (driver S_peak=0.08 is in Bearman-Harvey
valid range; the overlay cannot apply). Tracked in `controls_f_drag_calibration_audit`. See §8.

---

## 3. Bearman-Harvey Valid Range

The lift LUT (`aero_lift_lut.csv`) is a transcription of Bearman-Harvey 1976 wind-tunnel data. The published valid range is:

- **Spin parameter:** S = r·ω/|v| ∈ [0.03, 0.30]
- **Reynolds number:** Re ∈ [5×10⁴, 2×10⁵]
- **Ball speed:** v ≥ 13 m/s

Source: Bearman, P.W. and Harvey, J.K. (1976). "Golf ball aerodynamics." Aeronautical Quarterly, 27(2), 112-122. Interpreted via Cornell SimScience golf aerodynamics reference.

Outside this range (S > 0.30 for spinning irons/wedges), the LUT is extrapolating. Layer 2 (`aero_lift_overlay.csv`) provides the tunable correction for this extrapolation territory.

The smoothstep blend window is S ∈ [0.25, 0.35]: below 0.25, overlay is exactly 1.0 (Bearman-Harvey trusted as-is); above 0.35, full overlay multiplier applies. The blend prevents slope discontinuity at the Layer 1/Layer 2 seam.

---

## 4. Calibration Harness Usage

**Editor menu:** `GOLFIN > Physics > Run Aero Calibration Sweep`

**CLI / EditMode tests:** `AeroCalibrationHarness.RunCalibrationSweep()` — returns a multi-line string suitable for pasting into IMPLEMENTER_REPORT.md.

File location: `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs`

**Report format (v4 overlay, corrected targets):**
```
[AeroCalibrationHarness] Sweep — Trackman 2024 PGA Tour averages (PDF YARDS row, corrected per Lesson K)
  Overlay active: True, IsValid: True
  Tolerance: ±10%
  club_driver_gf         target=  275yd  actual=  240yd  err= -12.7%  S=0.080  FAIL
  club_iron7_mireo       target=  172yd  actual=  170yd  err=  -0.9%  S=0.302  PASS
  club_iron9_klyro       target=  148yd  actual=  138yd  err=  -6.7%  S=0.399  PASS
  club_pwedge_royal      target=  136yd  actual=  127yd  err=  -6.4%  S=0.452  PASS
  Summary: 3/4 clubs PASS  worst=club_driver_gf (12.7%)
```

PASS criteria: `abs(actual_carry - target_carry) / target_carry × 100 ≤ 10%`.

Note: the driver FAIL is expected and intentional — see §8.

---

## 5. Smoothstep Blend Math

The overlay multiplier is blended using the standard cubic Hermite smoothstep:

```
t = (S - 0.25) / (0.35 - 0.25)         // S in [0.25, 0.35]
smoothT = t² × (3 − 2t)                // cubic smoothstep
mult_effective = 1.0 + (mult_raw − 1.0) × smoothT
```

- For S ≤ 0.25: mult_effective = 1.0 (Layer 1 trusted as-is)
- For S ≥ 0.35: mult_effective = mult_raw (full overlay)
- For S in (0.25, 0.35): cubic blend — continuous first and second derivatives at boundaries

The blend window [0.25, 0.35] was chosen to match the Bearman-Harvey upper bound (S=0.30) with ±0.05 margin on each side. The spec prohibits widening past [0.20, 0.40] without escalation — widening changes the carry of driver-regime shots in the S=0.20-0.25 range.

Implementation: `AeroModel.BlendOverlay` in `Assets/Scripts/Physics/Core/AeroModel.cs`.

---

## 6. When to Recalibrate

Run `AeroCalibrationHarness.RunCalibrationSweep()` and verify all iron/wedge clubs pass ±10% after ANY of the following changes:

1. Any change to `aero.csv` (drag/lift coefficients, ball mass, ball radius, air density).
2. Any change to `aero_lift_lut.csv` or `aero_drag_lut.csv` (Layer 1 LUTs).
3. Any change to ball-physics defaults (when ball stats become non-Neutral).
4. Any change to the integrator step size or ODE method in `BallSimulation.cs`.
5. Any change to `fpMath` primitives that affect aero computation (Sqrt, Cos, Sin, Cross, Dot).
6. Tour-pro reference data updates (Trackman annual report) — when new Trackman data is published, update `AeroCalibrationTripwireTests.cs` targets and re-run calibration.
7. Any change to `AeroModel.cs` (including the smoothstep blend window or `BlendOverlay` implementation).

The tripwire test (`AeroCalibrationTripwireTests.Aero_MidHighSpinClubs_WithinTourCarryRange`) will catch iron/wedge regressions automatically in EditMode tests. The driver test (`Aero_Driver_KnownPending_LayerOneAudit`) is `[Ignore]`-tagged pending `controls_f_drag_calibration_audit`.

---

## 7. Layer-1 Sanctity Rule

The Layer 1 LUTs (`aero_lift_lut.csv`, `aero_drag_lut.csv`) may ONLY be edited for:

a. **Bug fixes** with documented evidence and a git commit referencing the bug (precedent: `controls_d_velocity_cap_diagnosis` — Sqrt bug in fpMath affected speed computations).
b. **Re-baselining** against a NEW real-world data source with citation (e.g., "updated to Aoki 2010 data, see [URL], publication date [date]").

Layer 1 is NEVER edited "to make a club feel right." That is Layer 2's job. This separation exists so future maintainers can trust that the physics simulation reflects real golf ball aerodynamics in its valid range, and can trace every deviation to a documented source.

If you find yourself changing a value in `aero_lift_lut.csv` to fix a calibration problem: stop. Add a row to `aero_lift_overlay.csv` instead.

---

## 8. What to Do When an In-Bearman-Harvey-Valid-Range Club Misses Target

The Layer-2 overlay applies a multiplier to Cl **only when S > 0.25** (smoothstep blend onset).
For clubs whose spin parameter is always below S=0.25 during flight (e.g., driver with S_peak≈0.08),
the overlay multiplier is exactly 1.0 regardless of `aero_lift_overlay.csv` values.

**If a low-S club misses its Trackman carry target, this is a Layer-1 issue, NOT a Layer-2 issue.**

Possible Layer-1 causes:
- Drag LUT too aggressive at high speed (most likely for driver: Cd=0.23 floor at v=75 m/s may be
  too high vs supercritical-Re golf-ball Cd ~0.18-0.22).
- Lift LUT underestimating Cl at low S (S < 0.10).
- Integrator step size introducing numerical error at high speed.

**Correct response:**
1. Open a separate audit task (e.g., `controls_f_drag_calibration_audit`) scoped to the Layer-1
   parameter in question.
2. Add an `[Ignore]`-tagged test for the failing club with a comment pointing at the audit task.
   This surfaces the known gap in the test runner UI without degrading the passing gate.
3. Do NOT widen the smoothstep blend window to cover the low-S regime — that would compromise the
   Layer 1/Layer 2 boundary.
4. Do NOT relax the ±10% tolerance for passing clubs to "compensate" for the failing club.

**Current instance:** driver carry ~240yd vs 275yd target (-12.7%). Tracked in
`controls_f_drag_calibration_audit` (P1). Test: `Aero_Driver_KnownPending_LayerOneAudit` `[Ignore]`.
