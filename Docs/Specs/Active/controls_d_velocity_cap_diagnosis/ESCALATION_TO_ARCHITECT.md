# Escalation to Architect Claude (claude.ai chat) — controls_d_velocity_cap_diagnosis

**Date:** 2026-05-05
**Escalated by:** Cesar (after reviewing the architect-review pass on Claude Code)
**Source of analysis:** Claude Code, after walking the post-fix carry numbers
**Status flip:** `ARCHITECT_REVIEW_PASS` → `ARCHITECT_REVIEW_ESCALATE`

---

## TL;DR

The Sqrt fix in this task is correct and lands cleanly (209/209 tests pass). But the re-snapshotted club carries that fall out of the corrected physics are off by **−10% (driver) to +46% (P-wedge) vs real-world Tour-pro yardages**, and the error scales monotonically with spin rate. The broken Sqrt was masking a separate, deeper bug in the lift (Magnus) model. Question for the architect: how do we want to handle the lift recalibration — block this task on it, queue it as a separate spec, or land it with a tripwire test?

---

## The carry numbers at issue

Locked into [Assets/Scripts/Physics/Tests/AerodynamicsTests.cs:34-40](../../../../Assets/Scripts/Physics/Tests/AerodynamicsTests.cs):

| Club | Inputs (real-world Tour values) | Our post-fix carry | Real PGA Tour | Δ |
|---|---|---|---|---|
| Driver | 75 m/s, 10.9°, 2686 rpm | **263 yd** | 290–305 | **−10%** |
| 7-iron | 52.5 m/s, 16.3°, 7097 rpm | **199 yd** | 172–185 | **+10–16%** |
| 9-iron | 48.5 m/s, 20°, 8647 rpm | **180 yd** | 140–155 | **+19–28%** |
| P-wedge | 46 m/s, 24°, 9300 rpm | **168 yd** | 115–135 | **+27–46%** |

Launch parameters (ball speed, launch angle, spin rate) are real Tour values — the bug is NOT in the inputs.

## Diagnostic fingerprint

- **Driver under-flies; high-spin clubs over-fly.**
- **Error scales monotonically with spin rate** (2686 → 7097 → 8647 → 9300 rpm maps to −10% → +13% → +24% → +37%).
- This pattern is the signature of **lift (Magnus) being too strong at high spin parameter S = ωr/v**, not drag, not Sqrt.

The lift LUT (`aero_lift_lut.csv`) is a 1D Bearman-Harvey curve fit. B-H is well-calibrated for one operating regime and progressively over-predicts lift outside it. Our irons/wedges live deep outside the calibration regime.

## Why this surfaced now (the masking)

Pre-Sqrt-fix, `Normalize(v)` returned a non-unit vHat with magnitude **1.64 for irons** (52.5/32) and **1.17 for driver** (75/64). That over-inflated drag along the velocity direction. For irons specifically, the over-strong drag *coincidentally cancelled* the over-strong lift, so 7-iron landed at ~172 yd by accident — an apparent pass.

The Sqrt fix removed the drag inflation. Lift over-prediction is now exposed unmasked. That's why the snapshot deltas look bad in this direction.

This is also why the previous test snapshots passing was not real validation. They were a coincidence of two bugs cancelling.

## The three options I floated to Cesar

1. **Approve Sqrt fix as-is + queue `controls_e_aero_lift_recalibration` as a fresh spec.** Cleanest separation. Risk: warning section in `PHYSICS_TUNING_TARGETS.md` is the only tripwire and is easy to miss.
2. **Loop back to implementer to extend this task** with `aero_lift_lut.csv` recalibration. Risk: scope creep — proper recalibration needs Trackman/USGA reference data and would balloon a Sqrt-fix task.
3. **Loop back with a small scope-add: a guard test** like `Aero_AllClubs_Within15PctOfTour_PerSpinRegime` that's currently `[Ignore]`-ed but exists in the assembly so the lift bug can't quietly live forever. Lower cost than option 2, higher signal than option 1.

Cesar's lean was option 3 but he wanted your call.

## Specific questions for the architect

1. Do you want the Sqrt fix and the lift recalibration treated as one task or two?
2. If two: do you want a tripwire test added to *this* task, or are you fine with `PHYSICS_TUNING_TARGETS.md` as the only signal?
3. If one: what reference data source do we calibrate against — Trackman composite Tour averages, USGA equipment-test data, or something internal?
4. Is the 1D Bearman-Harvey lift model the right long-term shape, or do we want to plan for a 2D Cl(S, Re) LUT?

## Files relevant to your decision

| File | Why it matters |
|---|---|
| [Assets/Scripts/Physics/Math/fpMath.cs](../../../../Assets/Scripts/Physics/Math/fpMath.cs) | The Sqrt fix itself (libfixmath digit-by-digit). |
| [Assets/Scripts/Physics/Tests/AerodynamicsTests.cs](../../../../Assets/Scripts/Physics/Tests/AerodynamicsTests.cs) | Re-snapshotted carry expectations (the numbers in the table above). |
| [Assets/Scripts/Physics/Tests/fpMathTests.cs](../../../../Assets/Scripts/Physics/Tests/fpMathTests.cs) | New 6-test regression suite for Sqrt. |
| `Assets/Data/aero_lift_lut.csv` | The actual lift LUT that needs recalibration. |
| `Assets/Data/aero_drag_lut.csv` | Sibling drag LUT — confirm not also miscalibrated. |
| [Docs/Physics/PHYSICS_TUNING_TARGETS.md](../../../../Docs/Physics/PHYSICS_TUNING_TARGETS.md) | Existing warning section. Decide if this is sufficient or needs a tripwire test backing it. |
| [Docs/Specs/Active/controls_d_velocity_cap_diagnosis/SPEC.md](SPEC.md) | The task spec — explicitly defers tuning to a follow-up. |
| [Docs/Specs/Active/controls_d_velocity_cap_diagnosis/IMPLEMENTER_REPORT.md](IMPLEMENTER_REPORT.md) | What the implementer claimed. |
| [Docs/Specs/Active/controls_d_velocity_cap_diagnosis/SELF_REVIEW.md](SELF_REVIEW.md) | Self-reviewer's hand-verification. |
| [Docs/Specs/Active/controls_d_velocity_cap_diagnosis/ARCHITECT_REVIEW.md](ARCHITECT_REVIEW.md) | In-pipeline reviewer's PASS verdict (which Cesar overrode by escalating). |
