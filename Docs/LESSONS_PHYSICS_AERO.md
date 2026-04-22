# Lessons: Physics Aerodynamics (Phase 2 / 2.1)

> Durable record of what we learned building the golf ball aero model.
> TellCode.md entries get trimmed over time; this file is where the
> reasoning lives so future-you (or future-me) doesn't rediscover it.
>
> Last updated: 2026-04-21, at Phase 2.1 closeout.

---

## TL;DR for future-you

If driver carry feels too short in playtest and you want to fix it:

1. **Don't** tune the Bearman–Harvey Cl LUT further. We tried that for three remediation cycles; the values are already at the B–H canonical curve and nudging them breaks wedges.
2. **Don't** jump to a 2D LUT (Phase 2.2) unless you're also willing to abandon Bearman–Harvey at low S. A 2D LUT constrained to B–H hits the same wall.
3. **Do** add `cl_empirical_scale` — a single scalar (default 1.0, tuned ~1.2–1.4) that multiplies LUT Cl. Not physical, but matches Trackman. 30 minutes. This is the cheapest correct fix. Notes below.

## The physics ceiling we hit

The canonical 1D golf-ball aero model is:

- **Drag** `F = ½·ρ·A·Cd(|v|)·|v|²` — Cd from wind-tunnel curve (Bearman 1976, Aoki 2010)
- **Lift** `F = ½·ρ·A·Cl(S)·|v|²` perpendicular to velocity, where `S = r·ω/|v|` is the spin parameter
- **Cl(S) = 0.5·S / (0.4 + S)** — Bearman–Harvey 1976, the reference closed form
- **Spin decay** `ω(t) = ω₀·exp(-λt)` with λ ≈ 0.04/s (Aoki 2010)

This model gives, at Trackman driver launch (v=75 m/s, θ=10.9°, ω=2686 rpm, so S=0.080):

- Cl = 0.083
- Lift force = 0.45 N
- Gravity force = 0.45 N
- Drag force = 1.18 N

**Lift at launch barely equals gravity.** By mid-flight as `v` drops, lift falls as `v²` while gravity is constant, so gravity wins and the ball drops sooner. Net carry ≈ 219 yd. Trackman target is 275 yd.

Vacuum carry for the same launch is 233 yd. So in our sim, drag robs 14 yd and lift fails to add any. Real golf: lift adds ~42 yd over vacuum (ball hangs, drifts forward under decaying drag). The difference is that **real drivers need effective Cl ≈ 0.12–0.15 at launch, not 0.083.**

## Why B–H undercounts Cl at low S

Bearman–Harvey's 1976 wind-tunnel rig measured Cl across S values 0.05–0.40. At low S the signal-to-noise was low, and the fit `Cl = 0.5·S/(0.4+S)` is conservative there. Real flight conditions (dimpled production ball, slightly yawed axis, transitional Reynolds regime) appear to produce higher effective Cl than the static wind-tunnel fit predicts at low S.

Published golf simulators handle this in one of three ways:

1. **Accept 5–10% residual on driver.** simulations4all.com, IJIMT 2013, Bearman 1976 trajectory validation all sit here. Our Q16.16 implementation lands around 20%, likely because our low-S Cl is at the B–H pure value and published sims nudge upward.
2. **Fit Cl to observed trajectories, not wind tunnel.** Smits–Ogg 1994 uses a different closed form that gives higher Cl at low S. Empirically motivated.
3. **2D speed × S LUT with free tuning.** Loses physical grounding but matches any target.

We went with (1) and accepted the residual. Future (2) via `cl_empirical_scale` is the cheap upgrade path.

## What the three constant-mode test classes document

`AerodynamicsTests.cs` has tests organized by club class for both modes:

**Constant-mode Cd=0.25, linear Cl:**
- `MidIrons_Within10Percent` — single Cd works for Iron3–PW
- `Endpoints_Within20Percent` — Driver and SandWedge can't both fit 10%

**LUT-mode, Bearman–Harvey Cl:**
- `Wedges_Within8Percent` — B–H accurate near saturation
- `MidIrons_Within15Percent` — B–H rising region, model gets looser
- `LongShots_Within25Percent` — Driver/Iron3, where B–H under-predicts Cl

These tolerances aren't aspirational. They're calibrated to the observed residuals with ~5% margin for CSV tuning. If a future change makes Driver fit 15%, great — but don't tighten the test gate speculatively, that caused three remediation cycles.

## Remediation history (the short version)

- **v0** (Code): tuned Cd to step function (0.16 low, 0.22 high), added fake `spin_drag_factor=0.03` to cover shortfall, silently widened constant-mode tolerance 10%→20%. **Rejected by architect** — tuned to symptoms, values unphysical.
- **v1** (architect): reverted scope creep. Held constant-mode at 10% and set seed LUTs from wind-tunnel literature (Cd 0.33 at 20 m/s). **Code pushed back correctly**: 10% unachievable on driver+SW with one Cd; seed Cd too high in 20–35 m/s band.
- **v2**: restructured constant-mode into mid-irons-10% + endpoints-20%, shifted seed Cd down. Code executed cleanly, reported honest residual (driver 23.5% short). Architect took it at face value.
- **v3** (after web research): realized seed Cl was 2–3× Bearman–Harvey values. Replaced with canonical B–H closed form. Restored spin decay (v1 incorrectly removed it). Relaxed LUT-mode tolerance 5%→8% to match published state-of-the-art. Added explicit success-ladder stopping rule. **Code ran v3 and hit rung 3** — the physics ceiling — honestly.
- **Closeout**: accepted current state. Split LUT-mode test into wedges/mid-irons/long-shots with per-class tolerances. Documented options A/B/C for future tightening.

## Parameters that proved real vs. fake

| Parameter | Real or fake? | Evidence |
|---|---|---|
| `spin_drag_factor` (Cd modifier from spin) | **Fake.** Added to compensate for bad LUT shapes. | Removed in v1, not needed in v3 once Cl was right. |
| `spin_decay_rate` (4%/s exponential) | **Real.** Aoki 2010, standard in every published sim. | Wrongly removed in v1. Restored in v3, integrator wires it per outer RK4 step. |
| `cl_empirical_scale` (LUT Cl multiplier) | **Legitimate if physical grounding is relaxed.** | Not shipped. Empirical calibration against Trackman rather than wind tunnel. Documented as Option A. |

Rule of thumb: if a parameter lets us match targets that Bearman–Harvey provably can't reach, it's either a useful empirical knob (valid if labeled as such) or a compensation hack (not valid). The distinction is whether the parameter has a publication that gives it its value and range.

## Signals a future attempt is stuck in the same trap

Watch for these. They were all present in v0 and would be again if someone tries to brute-force 5% driver accuracy:

- Cd or Cl breakpoints drifting below/above the physical bounds (Cd < 0.23 in post-crisis, Cl > 0.30, Cd non-monotonic in the drag-crisis-free region).
- Test tolerances widening without being renamed. A test called `Within5Percent` whose gate is actually 12% is a lie.
- New parameters with names that suggest physics but no citation behind the default value.
- Results matching targets while breaking the vacuum regression test or the `Backspin_ExtendsCarry_VsZeroSpin` directional test.
- The spec iterating past v3 with no architectural change. If we're tuning the same knobs with the same model for a fourth time, the model is the problem.

## Option A details (future empirical Cl scale)

When playtest shows driver needs fixing, don't write another v4. Do this:

1. Add `cl_empirical_scale` to `aero.csv` (default 1.0).
2. Add `ClEmpiricalScale` to `AeroConfig` (default `fp.One`).
3. Multiply LUT output by this scale in `AeroModel.ComputeAeroForce`:
   ```csharp
   fp cl = cfg.LiftLut.Evaluate(spinParam) * cfg.ClEmpiricalScale;
   ```
4. Start from 1.0, tune up toward 1.2–1.4 until driver hits 5%.
5. **Watch wedges.** At 1.4× they may overshoot (Cl > 0.30 is unphysical). If so, clamp the multiplied Cl at 0.30 after scaling.
6. Document the final scale in the aero.csv notes column with a playtest-date reference.

Expected final state: driver within 5%, all irons within 8%, wedges within 6%. Total time: 30–60 minutes. Not shipped now because (a) Phase 3–5 are higher priority, (b) we don't know yet whether game feel cares about 20% driver accuracy.

## Option B details (2D LUT on speed × S, if ever needed)

Only do this if Option A doesn't produce acceptable wedge behavior after scaling, or if surface-material tuning in Phase 4 surfaces a coupling we can't model with a scalar.

- Replace `CoefficientLut` (1D) with `CoefficientLut2D`. Keep the old 1D type — tests may still use it.
- Bilinear interpolation over speed × S breakpoints.
- ~6×6 to ~10×10 grid. CSVs as tables, first row and column as headers.
- Roughly half a day of implementation, another half day of tuning.
- Documents "here's Cl at (speed=50, S=0.1)" separately from "Cl at (speed=75, S=0.1)", letting Reynolds-regime effects show up naturally.

Don't do this until you know it's needed. 2D LUTs are harder to reason about, harder to tune, and harder to audit for physical plausibility.

## Option C details (hybrid physics/empirical)

Combine A and B: physics-based Cd from wind tunnel, empirical Cl fit directly to Trackman targets via a smoothed per-club curve. This is what Smits–Ogg 1994 effectively did. Same effort as B, more honest labeling than A.

---

## References (used in derivation)

- **Bearman, P.W. and Harvey, J.K. (1976).** "Golf ball aerodynamics." *Aeronautical Quarterly*, 27(2), 112–122. Canonical source of `Cl = 0.5·S/(0.4+S)`.
- **Aoki, K., Muto, K., Okanaga, H. (2010).** "Aerodynamic characteristics and flow pattern of a golf ball with rotation." *Procedia Engineering*, 2, 2431–2436. Spin decay 4%/s.
- **Smits, A.J., Smith, D.R. (1994).** "A new aerodynamic model of a golf ball in flight." *Science and Golf II*. Alternative Cl fit with higher low-S values.
- **MacDonald, W.M., Hanzely, S. (1991).** "The physics of the drive in golf." *Am. J. Phys.* 59(3), 213–218. Another closed form; gives Cl ≈ 0.2 at driver S.
- **simulations4all.com** — Golf Ball Flight Physics Simulator (verified 2026). Cites Bearman–Harvey, claims 5–10% accuracy.
- **IJIMT 2013** — "Flight Trajectory of a Golf Ball for a Realistic Game." RK4 + B–H, Table II shows 5–10% residuals.
- **MDPI 2018** — "Aerodynamics of Golf Balls in Still Air." Wind tunnel Cd range 0.23–0.28 for dimpled balls in flight.
- **Trackman PGA Tour Averages (2024).** 167 mph / 10.9° / 2686 rpm / 275 yd carry. theleftrough.com reference, verified against trackman.com.

## Files this work touched

- `Assets/Scripts/Physics/Core/AeroConfig.cs` — data struct, `Default` and `Vacuum` factories
- `Assets/Scripts/Physics/Core/AeroModel.cs` — force calculator, branches on UseDragLut/UseLiftLut
- `Assets/Scripts/Physics/Core/CoefficientLut.cs` — 1D piecewise-linear LUT, pure math, no engine refs
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — RK4 integrator, spin decay applied once per outer step
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — CSV loading, Resources-based
- `Assets/Scripts/Editor/Physics/PhysicsTuningWindow.cs` — Window > Physics > Tuning
- `Assets/Scripts/Physics/Tests/AerodynamicsTests.cs` — 10 aero tests in 3 constant-mode + 3 LUT-mode classes
- `Assets/Resources/Physics/aero.csv` — scalar knobs, mode flags
- `Assets/Resources/Physics/aero_drag_lut.csv` — Cd(speed) breakpoints
- `Assets/Resources/Physics/aero_lift_lut.csv` — Cl(S) breakpoints, Bearman–Harvey values
- `Assets/Resources/Physics/clubs.csv` — Trackman target table (don't tune these)
