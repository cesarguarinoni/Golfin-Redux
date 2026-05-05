# NOTES — `controls_f_drag_calibration_audit` — Architect working notes

> **Working draft (data-anchored plan, not yet a SPEC).** SPEC.md follows after Cesar reviews this plan and locks the open questions. Implementer reads SPEC.md, NOT this file.

**Created:** 2026-05-05 (evening) JST
**Architect:** Claude (claude.ai)
**Notion:** [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) — `C.8 — Drag LUT calibration audit (driver carry blocker)` — P1 High, S half-day, Queued.
**Predecessor:** `controls_e_aero_overlay_pass` (DONE 2026-05-05). The `[Ignore]`-tagged `Aero_Driver_KnownPending_LayerOneAudit` test added during that task is the definition-of-done for this one.

## TL;DR

After `controls_e` landed the lift overlay, three of four tripwire targets pass at ±10%. Driver is the sole exception, carrying ~12.7% short of Trackman 275yd target (240.4yd actual). Driver sits at S=0.08 — inside Bearman-Harvey lift LUT's published valid range — so the lift overlay correctly does NOT engage there by design. The miss is therefore a **drag-side issue**: at driver speeds (v≈75 m/s, Re≈2.17×10⁵), the drag LUT is sitting at Cd=0.23 across the entire 30+ m/s range, and that value may be slightly too high for our implicit ball model.

This task applies the **same two-layer pattern** we just established for lift: keep `aero_drag_lut.csv` as faithful Bearman-Harvey transcription Layer 1, add `aero_drag_overlay.csv` as a Layer-2 multiplicative correction past Bearman-Harvey's published valid Re range, blended via smoothstep at the seam. Calibrate against same Trackman 2024/2025 PGA Tour averages. Definition-of-done: `Aero_Driver_KnownPending_LayerOneAudit` test removes `[Ignore]` and PASSes. New gate: 211 PASS + 0 IGNORED.

## Empirical evidence (already captured)

From `Docs/Specs/Completed/controls_e_aero_overlay_pass/IMPLEMENTER_REPORT.md` final calibration sweep (m40=0.850):

| Club | Sim actual (yd) | Trackman target (yd) | Error % | Status |
|---|---|---|---|---|
| Driver | 240.4 | 275 | **−12.6%** | FAIL (Layer-1 territory) |
| 7-iron | 171.7 | 172 | −0.1% | PASS |
| 9-iron | 138.8 | 148 | −6.2% | PASS |
| PW | 128.3 | 136 | −5.6% | PASS |

Driver shot params (from `AeroCalibrationTripwireTests.cs`): ball speed 75 m/s, launch 10.9°, spin 2686 rpm. Spin parameter `S = r·ω/v = 0.02135 × (2686×2π/60) / 75 = 0.080`. Reynolds number `Re = ρvD/μ = 1.225 × 75 × 0.04267 / 1.81×10⁻⁵ ≈ 2.17×10⁵`.

Both S=0.08 and Re=2.17×10⁵ are **at the upper edge of Bearman-Harvey 1976's published valid range** (S∈[0.03, 0.30], Re∈[5×10⁴, 2×10⁵]). Past Re=2×10⁵, the published curve is sparse — Smith et al. CFD (Kobe Univ., 2010) documents Cd staying "nearly constant" past supercritical onset, but cite-checked values vary slightly by ball type.

## Source-code root-cause analysis

`Assets/Resources/Physics/aero_drag_lut.csv` (current, post-controls_e):

```csv
speed_mps,cd,notes
5,0.50,very low speed laminar-ish
10,0.48,
15,0.45,pre-drag-crisis
18,0.40,drag crisis onset
22,0.28,post-crisis Bearman
26,0.24,
30,0.23,minimum allowed floor
40,0.23,
50,0.23,
60,0.23,
70,0.23,
80,0.23,driver peak
100,0.23,extrapolation clamp
```

The LUT clamps to `Cd=0.23` from 30 m/s onward. Three issues:

1. **The 0.23 floor sits at the upper end of plausible real-world values.** Independent CFD (Smith et al. 2010 Kobe Univ.) and wind-tunnel data (Bearman-Harvey 1976, Choi et al. 2006) put Cd in the supercritical regime at ~0.22 for typical Tour-ball dimple geometry. Modern Tour balls (Pro V1, etc.) with more aggressive dimple patterns may be closer to **0.21**. The 0.23 we have isn't *wrong* — it's defensible — but it's at the upper end of the plausible range.
2. **No spin dependence.** Real golf-ball drag also depends on spin (more spin → slightly higher drag, ~5-15% shift). Bearman-Harvey 1976's Cd(S) curves are weak but non-zero at high S. Our LUT is purely Cd(speed), ignoring this. Driver has low S so this doesn't matter much for driver, but it's a Layer-1 gap worth flagging.
3. **Drag-crisis transition curve shape.** The drop from 0.45 (15 m/s) to 0.28 (22 m/s) is steep and may be slightly over-corrective. If the integrator spends meaningful time in this region (it shouldn't for driver — ball is fast for the duration of carry), the cumulative effect could matter. Driver's velocity decays from 75 → ~30 m/s over the trajectory, so it spends ~the last 30% of carry in the 30→50 m/s range where Cd=0.23. If we relax to 0.21 there, the carry adds back roughly 8-12 yards.

Architect-time back-of-envelope: reducing the high-speed Cd from 0.23 to **~0.21** (a ~9% drag reduction) should add **~25-30 yards** to driver carry, bringing 240→265-270yd, well inside ±10% of 275. That's the right magnitude for what we're seeing; the math works.

But this is speculative — actual response is non-linear and depends on integrated trajectory. The harness will measure the real answer.

## Proposed architecture (matches controls_e pattern)

A new file `Assets/Resources/Physics/aero_drag_overlay.csv` with the same shape as the lift overlay:

```csv
# Layer 2 — corner-case overlay. Tunable for game feel.
# This file applies a multiplicative correction to the Layer 1 drag coefficient
# (aero_drag_lut.csv, Bearman-Harvey 1976 transcription) ONLY where Layer 1 is
# extrapolating past its valid range (v > some threshold) OR where outcomes
# diverge from observed Tour-pro reality (Trackman 2024 PGA Tour averages).
# Smoothstep blend prevents seam discontinuity.
# See Docs/Physics/CALIBRATION_METHODOLOGY.md for full architecture frame.
speed_mps,cd_multiplier,notes
0,1.000,Layer 1 valid; no overlay
22,1.000,Bearman-Harvey valid range
30,1.000,Bearman-Harvey upper range still in valid territory
40,1.000,Bearman-Harvey upper bound; overlay starts taking effect (smoothstep starts)
50,?,Tuned in Step 5
60,?,Tuned in Step 5
80,?,Tuned in Step 5 (driver peak operating region)
```

The multiplier is applied after the Layer-1 evaluation:

```csharp
fp cdLayer1 = dragLut.Evaluate(speed);
if (useDragOverlay && dragOverlay.IsValid)
{
    fp multRaw = dragOverlay.EvaluateMultiplier(speed);
    fp mult    = BlendDragOverlay(speed, multRaw);
    cd = cdLayer1 * mult;
}
else
{
    cd = cdLayer1;
}
```

Smoothstep blend across `v ∈ [40, 50]` m/s (or equivalent boundary chosen from Bearman-Harvey's published valid range; still finalizing — see open question 1). Below the lower bound: pure Layer 1 (mult=1.0). Above the upper bound: pure overlay multiplier. Between: smoothstep interpolation.

**Note on the seam location.** Bearman-Harvey 1976 published Re-range is [5×10⁴, 2×10⁵], which converts to v∈[18, 70] m/s for golf-ball Re. The overlay seam should be at the upper end, around v≈45-50 m/s where the published data starts thinning. This is different from the lift overlay's seam at S∈[0.25, 0.35] because Re and S are independent dimensions; we can't reuse the lift seam directly.

## Real-world data sources

Same primary anchors as `controls_e`:

### Source 1 — Trackman PGA Tour 2024 averages (PRIMARY)

Same table as `controls_e`'s primary calibration anchor. Driver: 275yd carry @ 75 m/s ball speed, 10.9° launch, 2686 rpm. Sourced from the **YARDS table** of the Trackman PDF at `https://teeituprva.com/wp-content/uploads/2019/03/PGA-AVERAGES-INTERACTIVE.pdf`, cross-verified against Maryland Golf Camps article (`https://marylandgolfcamps.com/how-far-do-professionals-hit-each-club-golf.html`) — **per Lesson K, this transcription has been triple-checked**.

The driver row is the only one this task targets. The other 7 calibration clubs already pass at ±10% post-controls_e (some with the lift overlay engaged, some without); they're regression-guarded by the existing tripwire test and won't be the focus here, but the harness will still report them all so we can see if drag re-tune affects them.

### Source 2 — Bearman-Harvey 1976 (LAYER 1 TRUTH)

The original wind-tunnel paper. Published valid range:
- Re ∈ [5×10⁴, 2×10⁵] → v ∈ [~18, ~70] m/s for golf-ball geometry
- Steady-state Cd ≈ 0.22 in the supercritical regime, "nearly constant" past Re ~10⁵

### Source 3 — Smith et al. 2010 (Kobe Univ.) CFD validation

Independent CFD validation against Bearman-Harvey wind-tunnel data. Reports:
- *"Cd drops to around 0.22 at the supercritical Reynolds number (Re=1.1×10⁵)... stays at an almost constant value"* up to Re=1.7×10⁵
- Confirmed Bearman-Harvey behavior is correct
- URL: `https://da.lib.kobe-u.ac.jp/da/kernel/90003493/90003493.pdf`

### Source 4 — Alam et al. 2011 (multi-ball comparison, CROSS-CHECK)

Wind-tunnel measurements across multiple golf balls (Maxfli, Pro V1, etc.). Reports:
- Maxfli ball: minimum Cd ≈ 0.25 (sharper dimples)
- Other balls: range varies; "variations in drag coefficient of up to 40% arise from differences in dimple characteristics"
- URL: `https://www.researchgate.net/publication/251716774_A_study_of_golf_ball_aerodynamic_drag`

This source matters because it shows real Tour balls have **different Cd values depending on dimple geometry**, ranging roughly 0.21–0.27 in the supercritical regime. Our 0.23 is a reasonable midpoint; tuning via overlay to ~0.21 simulates a slightly lower-drag ball model (e.g., modern Pro V1 with deep aggressive dimples) without claiming we're "fixing" Bearman-Harvey.

### Source 5 — controls_e CALIBRATION_METHODOLOGY.md

The methodology doc just landed in `controls_e`. This task adds a new "When to add a Layer-2 drag overlay" subsection mirroring the existing "When to add a Layer-2 lift overlay" — same pattern, same trigger conditions, same architecture rules.

## The calibration loop

This is essentially the same loop as `controls_e`, retargeted to drag. Steps:

### Step 0 — Lock the Trackman target (ALREADY DONE)

Driver: 275yd ±10% (range 247.5–302.5yd). This came from `controls_e`'s tripwire test verbatim.

### Step 1 — Build the calibration harness extension

The existing `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` from `controls_e` already runs the full 8-club sweep with current overlay state. We extend it (don't replace) to additionally print the **per-club error vs Trackman target** in CSV-friendly form, so before/after diffs are trivial.

Then we add a one-line CLI flag (or just a separate menu item `GOLFIN/Physics/Run Drag Calibration Sweep`) that runs the same sweep but with the drag overlay enabled. Both surfaces invoke the same shared harness method.

### Step 2 — First baseline run (drag overlay disabled)

Run the harness with `aero_drag_overlay.csv` set to all 1.000 multipliers. Expected: same as `controls_e`'s final state — driver −12.6%, irons/wedge passing.

### Step 3 — Iteratively tune the drag overlay multipliers

For driver:
1. Driver operating speed range: 75 m/s at launch → ~30 m/s at landing.
2. Most impactful overlay rows are at v=50, 60, 80 m/s (where driver spends most flight time).
3. Reduce the multiplier at these rows. Rule of thumb: 0.95× multiplier at 60-80 m/s adds ~10-15 yd to driver carry.
4. Re-run the harness. Iterate until driver lands within ±10% of 275yd.

For irons/wedge:
1. They operate at 50–60 m/s ball speed; their trajectories also touch the v∈[40, 70] m/s range.
2. After driver tuning lands, **verify irons/wedge are still within ±10%**. Drag overlay may push them slightly long (less drag → longer carry) — if so, tune the lower-speed overlay rows (v=40, 50 m/s) to compensate, OR accept slight shift if it's still within ±10%.

Termination condition: harness reports `8/8 clubs PASS`, OR driver passes and irons/wedge are still within ±10%.

Expected iteration count: 3-5 passes. Total tuning time: ~20-30 minutes.

Document each iteration in `IMPLEMENTER_REPORT.md` § "Calibration iterations": which overlay row(s) changed, old → new value, rationale, resulting per-club error table.

### Step 4 — Verify the smoothstep seam

Same kind of check as `controls_e`: simulate at speeds bracketing the seam (v=35, 38, 40, 42, 45, 48, 50, 55) and verify the carry-vs-speed curve is smooth at the seam boundaries. Document in IMPLEMENTER_REPORT § "Smoothstep verification".

### Step 5 — Enable the tripwire test

Open `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs`. Remove the `[Ignore("...")]` attribute from `Aero_Driver_KnownPending_LayerOneAudit`. The test must now PASS at ±10%.

### Step 6 — Update CALIBRATION_METHODOLOGY.md

Add a new section: **"§9 — When to add a Layer-2 drag overlay"** mirroring the existing lift overlay section. Cite this task by name. Document the seam location, the rationale, the calibration target, the trigger conditions for re-calibration.

Also update §8 ("What to do when an in-Bearman-Harvey-valid-range club misses target") to reflect that the answer is no longer "open follow-up" — the answer is now "do exactly what controls_f did: add a Layer-2 drag overlay."

### Step 7 — Update layer-status header on `aero_drag_lut.csv`

Currently reads "Layer 1 — real physics. Bearman-Harvey 1976 transcription (drag side). Layer 2 audit pending: see controls_f_drag_calibration_audit." Update the second line to "Layer 2 overlay applied: see aero_drag_overlay.csv and Docs/Physics/CALIBRATION_METHODOLOGY.md §9."

## What this task does NOT do

- Does NOT introduce a 2D Cd(v, S) LUT. Same reasoning as `controls_e`'s rejection of 2D lift: 1D + overlay matches AAA-studio practice and is sufficient for our purposes.
- Does NOT extend Layer 1 with Aoki/Libii data or any other new wind-tunnel reference (would be `controls_g`, not planned).
- Does NOT touch the lift overlay (`controls_e` is closed; lift settings are locked).
- Does NOT touch drag-crisis transition shape (15 m/s → 22 m/s region). The driver doesn't spend meaningful time there. If a future ball-rolling or low-speed task needs this re-tuned, that's a separate spec.
- Does NOT add any new physics regimes (negative-lift, reverse-Magnus, knuckleball, etc.) beyond what's already in Layer 1.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| Driver overlay over-corrects: pulls 7-iron / 9-iron / PW out of ±10%. | Step 3's iron/wedge re-verify catches this. If so, tune lower-speed overlay rows down or accept shift if still in tolerance. |
| Smoothstep seam at v≈40-50 m/s creates a kink visible in trajectory plots. | Step 4 verification check. If kink visible, widen seam window (e.g., v∈[35, 55] instead of [40, 50]). |
| Layer 1 sanctity question: are we admitting Bearman-Harvey was wrong? | No. Bearman-Harvey wind-tunnel measured a typical 1976-era ball. Modern Tour balls have different dimple geometry → different Cd. The overlay simulates that ball-model difference without rewriting the published reference data. Same logic as the lift overlay. |
| Future Layer 1 changes (e.g., a fpMath update affecting integrator) silently invalidate this calibration. | Same mitigation as `controls_e`: tripwire test stays in suite forever; any Layer-1 change that breaks it forces explicit re-calibration. CALIBRATION_METHODOLOGY.md "When to recalibrate" §6 already names this trigger. |
| The 12.7% gap isn't actually drag — it's something else (lift in low-S, integrator, launch params). | If drag overlay tuning down to multiplier 0.80 (a 20% reduction; way too much) doesn't close the gap, escalate as IMPLEMENTER_BLOCKED. The math says 0.92× should do it; if we need 0.80×, drag isn't the (only) issue. |

## Estimated cost

- SPEC writing: 1 hour architect-side.
- Implementer execution: half-day (CSV creation + overlay seam in AeroModel + AeroConfig field + ConfigLoader method + harness extension + iteration + methodology doc update + header update).
- Review pipeline: 1-2 hours total.
- **Total: ~half-working-day to 1 working day.**

This is shorter than `controls_e` because the architectural pattern is already established and the harness already exists.

## Open questions for Cesar (before SPEC writing)

1. **Drag overlay seam location.** Bearman-Harvey valid range upper bound at v≈70 m/s, but driver is right at that edge. Recommend seam at **v ∈ [40, 50] m/s** so driver gets full overlay treatment and irons get partial treatment. Alternative: **v ∈ [45, 55]** (later seam, only driver fully affected, irons untouched). Lean: [40, 50] because irons are also Trackman-tuned and we'd rather have them in the overlay's blast radius than not. Cesar to confirm.

2. **Tolerance for "irons still pass after drag tune."** Strict interpretation: all 4 calibration clubs (driver + 3 irons) within ±10% after drag tune. Looser interpretation: driver within ±10%, irons within ±15% (already tighter than reality but a safety margin). Lean: keep strict ±10% since `controls_e` left them well within ±10%.

3. **Multiplier vs additive offset.** Lift overlay used multiplier (Cl × m). For drag, multiplier (Cd × m) is more standard since Cd ranges narrowly. Alternative: additive offset (Cd + δ) which has more uniform effect across speeds. Lean: multiplier (consistent with lift overlay; same code pattern in `BlendOverlay`).

4. **Should we increase Layer 1 drag at v < 22 m/s instead?** No — that region is for ball roll-out, not flight. Drag-crisis transition at 18 m/s is the boundary. Touching it would affect putts and roll-out behavior, which is calibrated separately in `surfaces.csv` and `putt.csv`. Leaving it locked.

5. **Per-Trackman-year re-validation.** When Trackman publishes their 2026 annual (sometime late 2026 / early 2027), should this overlay be re-tuned? Per CALIBRATION_METHODOLOGY.md §6 "When to recalibrate", yes — but only if the new published numbers diverge from current targets by more than ~3yd. Documented in this task's methodology update.

## Files this task is likely to touch

- `Assets/Resources/Physics/aero_drag_overlay.csv` — NEW. Layer-2 multiplier table.
- `Assets/Resources/Physics/aero.csv` — add row `use_drag_overlay,1`.
- `Assets/Resources/Physics/aero_drag_lut.csv` — update layer-status header (one line).
- `Assets/Scripts/Physics/Core/AeroConfig.cs` — add `DragOverlay` and `UseDragOverlay` fields.
- `Assets/Scripts/Physics/Core/AeroModel.cs` — add drag overlay seam in `ComputeAeroForce` + `BlendDragOverlay` private helper.
- `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — add `LoadDragOverlay()`, parse `use_drag_overlay`.
- `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` — extend (don't rewrite) to optionally print speed-ranged drag report. Possibly add second menu item.
- `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` — remove `[Ignore]` from `Aero_Driver_KnownPending_LayerOneAudit`.
- `Docs/Physics/CALIBRATION_METHODOLOGY.md` — add §9, update §8 reference.

No asmdef, no scene, no prefab. Same blast-radius shape as `controls_e`.

## Out of scope

- Changes to lift overlay (closed in controls_e).
- Touching `aero_lift_lut.csv` values.
- Changes to drag at v < 22 m/s (drag-crisis transition region).
- 2D Cd(v, S) LUT.
- Fairway/Rough/Bunker tuning (separate Notion entry, controls_c Phase B).
- Adding negative-lift / reverse-Magnus / knuckleball regimes.
