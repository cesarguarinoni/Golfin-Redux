# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
>
> **Workflow (2026-04-21):** Claude Code drives Unity directly via Unity-MCP. Tools: `script-update-or-create`, `script-execute`, `tests-run`, `console-get-logs`, `scene-create`/`open`/`save`, `gameobject-create`/`component-add`/`modify`, `editor-application-set-state`, `screenshot-game-view`/`scene-view`, `package-add`. Specs include autonomous validation — run to confirmation rather than reporting "done" prematurely.

---

## ACTIVE TASK — Phase 2.1 REMEDIATION v3: Bearman–Harvey seeds, spin decay restored, 8% tolerance

### Context (what v2 got wrong — my fault, not Code's)

Code's v2 execution was clean. Tests restructured, LUTs stayed within physical constraints, option-3 honest residual reported with correct diagnosis pattern (driver worst, wedges best). My review then took the residuals at face value and approved the report.

Since then I verified against published golf trajectory simulators (simulations4all.com, IJIMT 2013, Bearman & Harvey 1976, Aoki 2010, MDPI 2018). Three things are now clear:

**1. My Cl seed values were 2–3× too high across most of the curve.** The canonical published formula is Bearman–Harvey: `Cl = 0.5·S / (0.4 + S)`. Evaluated at each club's spin parameter:

| Club | S | Bearman–Harvey Cl | My v2 seed Cl | Ratio | Code's v2 residual |
|---|---|---|---|---|---|
| Driver | 0.08 | 0.083 | 0.22 | 2.65× too high | -23.5% |
| Iron3 | ~0.125 | 0.119 | 0.26 | 2.18× | -13.9% |
| Iron5 | ~0.195 | 0.164 | 0.27 | 1.65× | -15.5% |
| Iron7 | 0.302 | 0.215 | 0.28 | 1.30× | -11.3% |
| Iron9 | ~0.40 | 0.250 | 0.29 | 1.16× | -8.2% |
| PW | ~0.48 | 0.273 | 0.30 | 1.10× | -4.7% |
| SW | 0.56 | 0.292 | 0.30 | 1.03× | -4.9% |

The residual column and the ratio column sort identically — excess lift at low S made shallow-launch shots climb too high, stall, and under-carry. Wedges were fine because their S is already near Bearman–Harvey saturation. **This was a seed-value error, not an architecture error.**

The Magnus-backward-Z analysis Code did during v2 tuning was mathematically correct but wasn't the root cause. With correct Cl values, the backward-Z component during ascent is exactly what keeps the ball aloft the right amount. It's only pathological when Cl is inflated.

**2. I was wrong to delete `spin_decay_rate` as scope creep in v1.** Reviewing Aoki 2010 and the IJIMT 2013 simulator paper, spin decay (typically 4%/second exponential) is **standard** in every serious golf trajectory simulator. Over a 6-second driver flight that's a 22% spin reduction, which meaningfully lowers late-flight lift. I conflated it with `spin_drag_factor` in v1. They're different: `spin_drag_factor` was a fake parameter compensating for bad LUTs (correct revert), `spin_decay_rate` is a real physical phenomenon (incorrect revert).

**3. Published simulator accuracy bar is 5–10%, not 5%.** The simulations4all.com "Verified" note states: *"The physics model captures primary forces accurately, matching measured trajectories within 5–10% for typical launch conditions."* IJIMT 2013's Table II shows similar residuals. Bearman–Harvey's own trajectory validation against driving-machine ground truth is in the same band. **We are not going to beat Bearman and Harvey using Bearman and Harvey's model.** 8% target for LUT mode.

### Why this is the correct v3 and won't need a v4

Converging monotonically, not oscillating. Trajectory of versions:

- **v0 (Code's first cut):** tuned to symptoms, unphysical (Cd=0.16).
- **v1 (my first reaction):** textbook wind-tunnel recollection, Cl ~0.11, 5% tolerance (unachievable).
- **v2 (my recalibration):** Cl pushed higher (~0.22) to cover perceived undershoot, still textbook recollection, not derived from published formulas.
- **v3 (current):** Cl comes from the canonical Bearman–Harvey closed form `Cl = 0.5·S/(0.4+S)`. Cd holds in the published 0.23–0.28 range. Spin decay restored. Tolerance matches published state-of-the-art.

**There is no v4 direction in 1D-LUT space.** Bearman–Harvey IS the canonical 1D model. If v3 doesn't converge to 8% on all clubs, the next step is architectural (Phase 2.2, 2D LUT on speed × S), not another seed-tuning spec. The success ladder in Part G makes that escape path explicit.

### What this task does

1. Replace Cl LUT with Bearman–Harvey-derived values at each breakpoint.
2. Adjust Cd LUT to the published range (0.23–0.28 for dimpled balls in-flight).
3. Re-add `spin_decay_rate` as a real physical parameter (default 0.04 /s per Aoki 2010), with spin decay applied once per outer RK4 step.
4. Relax LUT-mode test tolerance from 5% to 8%. Mid-irons constant-mode stays 10%, endpoints stays 20%.
5. Re-run. Expected honest outcome: all 7 clubs within 8% in LUT mode.

---

### Status check — confirm before starting

Before making changes, verify v2 state:

- `aero.csv` has 10 rows ending at `use_lift_lut` (spin_drag_factor and spin_decay_rate both absent).
- `AeroConfig.cs` has no `SpinDragFactor` or `SpinDecayRate` fields.
- `AeroModel.cs` drag term is pure `Cd · ½ρA|v|²`, no spin multiplier.
- `BallSimulation.cs` has no spin-decay code.
- Constant-mode tests `MidIrons_Within10Percent` and `Endpoints_Within20Percent` exist and pass.
- LUT-mode test exists at 5% tolerance.

If any of these differ, note it in the done report but proceed.

---

### Part A — Replace lift LUT with Bearman–Harvey values

**`Assets/Resources/Physics/aero_lift_lut.csv`** — replace contents with:

```csv
spin_parameter,cl,notes
0.00,0.00,no spin
0.02,0.024,Bearman-Harvey 0.5*S/(0.4+S)
0.05,0.056,
0.08,0.083,driver regime
0.10,0.100,
0.12,0.115,
0.15,0.136,
0.20,0.167,long iron regime
0.25,0.192,
0.30,0.214,Iron7 S
0.40,0.250,Iron9 regime
0.50,0.278,PW regime
0.60,0.300,SW regime
```

These are computed directly from `Cl = 0.5·S/(0.4+S)` at each S breakpoint. Smooth, monotonically increasing, saturates near 0.30 as S → ∞. Physical by construction.

The "Cl ≤ 0.30" and monotonicity constraints are preserved. Tuning is allowed within ±0.01 per breakpoint for fine adjustment; overall shape must match Bearman–Harvey.

---

### Part B — Refine drag LUT toward published range

**`Assets/Resources/Physics/aero_drag_lut.csv`** — replace contents with:

```csv
speed_mps,cd,notes
5,0.50,very low speed laminar-ish
10,0.48,
15,0.45,pre-drag-crisis
18,0.40,drag crisis onset
22,0.28,post-crisis Bearman
26,0.25,
30,0.24,
40,0.24,mid-irons cruise
50,0.24,
60,0.24,long irons
70,0.24,
80,0.24,driver peak
100,0.24,extrapolation clamp
```

Post-crisis Cd holds at ~0.24 across 22–100 m/s rather than declining further. Matches simulations4all's 0.23–0.28 dimpled-ball range and MDPI 2018's "close to 0.2" as a floor, not below. v2's 0.21 values were below the published floor.

Constraints stay: monotonically non-increasing from 22 m/s; in [0.21, 0.30] post-crisis; in [0.40, 0.55] pre-crisis.

---

### Part C — Re-add spin decay (properly this time)

Real physical parameter, not a compensation knob. My apology is in the history log.

**`Assets/Resources/Physics/aero.csv`** — add one row (keeping all existing rows):

```csv
spin_decay_rate,0.04,1/s,exponential spin decay per Aoki 2010
```

**`Assets/Scripts/Physics/Core/AeroConfig.cs`** — add field:

```csharp
public fp SpinDecayRate;    // 1/s, spin half-life exponential; 0=no decay
```

Add to `Default`: `SpinDecayRate = fp.FromFloat(0.04f)`. Add to `Vacuum`: `SpinDecayRate = fp.Zero` (vacuum has no air friction to decay spin).

**`Assets/Scripts/Physics/Core/BallSimulation.cs`** — decay spin once per outer step, after position/velocity update, before the next step begins. NOT inside the RK4 sub-stages (k1/k2/k3/k4). Over Δt=1/240s, (1 − 0.04/240) = 0.99983 per step, stable in Q16.16.

```csharp
// Exponential spin decay: ω(t+Δt) = ω(t) · exp(-λ·Δt)
// First-order approximation safe for λ·Δt << 1.
if (cfg.SpinDecayRate > fp.Epsilon)
{
    fp decayFactor = fp.One - (cfg.SpinDecayRate * Dt);
    spin = new SpinState(spin.Axis, spin.Rate * decayFactor);
}
```

The spin state threads through the simulation as a mutable local. Initial `ShotInput.Spin` → local → decayed each outer step → passed to `AeroModel.ComputeAeroForce` at each sub-step. Standard Aoki 2010 approach.

**Do NOT** add any other spin-related parameter. No `spin_drag_factor`, no gyroscopic terms, no spin-axis tilt. Just exponential magnitude decay.

---

### Part D — Relax LUT-mode tolerance to 8%

**`Assets/Scripts/Physics/Tests/AerodynamicsTests.cs`:** rename `Aero_ClubCarries_LutMode_AllClubs_Within5Percent` to `Aero_ClubCarries_LutMode_AllClubs_Within8Percent` and set the tolerance parameter to 0.08.

XML doc comment on the test:

```csharp
/// <summary>
/// LUT-mode gate for all 7 clubs. Tolerance 8% matches the published
/// state-of-the-art for 1D-LUT golf trajectory simulators
/// (simulations4all.com cites 5-10%, IJIMT 2013 Table II similar).
/// Trackman targets are themselves tour averages with variance;
/// tighter than 8% is not achievable with a 1D Cd(v) + Cl(S) model.
/// For 5% we would need a 2D LUT (Phase 2.2) or CFD-grade aero.
/// </summary>
```

Constant-mode tests keep their tolerances: mid-irons 10%, endpoints 20%. Those are physical ceilings.

---

### Part E — Validation

1. Compile clean. `console-get-logs` after changes, max 5 iterations.
2. Run full suite: `tests-run` filter `Golfin.Physics.Tests`.

**Expected after replacing seeds and adding spin decay:**

- All 4 Phase 1 tests: pass.
- `Aero_Off_MatchesPhase1_Within_Epsilon`: pass (spin decay inactive in vacuum).
- `Aero_DragReducesCarry_MonotonicallyWithCd`: pass.
- `Aero_Backspin_ExtendsCarry_VsZeroSpin`: pass.
- `Aero_DragLut_ReducesCarryVsConstant_ForDriver`: pass (LUT Cd=0.24 < constant 0.25).
- `Aero_LiftLut_AffectsCarry_ForWedge`: pass.
- `Lut_EvaluatesWithinBounds_ReturnsInterpolated`: pass.
- `Aero_ClubCarries_ConstantMode_MidIrons_Within10Percent`: pass (unchanged from v2).
- `Aero_ClubCarries_ConstantMode_Endpoints_Within20Percent`: pass.
- `Aero_ClubCarries_LutMode_AllClubs_Within8Percent`: the target.

**Prediction:** with Bearman–Harvey Cl, refined Cd, and 4%/s spin decay, driver should land near 260–280 yd (was 210), irons should gain 10–20 yd each, wedges should stay close to v2 values (they were already close). All should fit inside 8%.

**If any club still exceeds 8% after the initial run:**

- **All clubs short by similar %:** spin decay may be too aggressive. Try 0.02/s (low end of Aoki range).
- **Only driver short, others fine:** Cl at S=0.08 may need a small upward nudge (from 0.083 to ~0.09). Do not exceed 0.10 — that's outside Bearman–Harvey.
- **Only wedges long, others fine:** spin decay helping too much at high S where flight time is shortest. Try 0.03/s.
- **Wedges long, driver short:** one knob can't fix both directions in 1D — go to the Part G decision tree.
- **Pattern you don't recognize:** stop. Report numbers. I'll diagnose.

Max 3 tuning iterations. Iteration budget is hard-capped; do not exceed.

---

### Part F — Done report

Include:

- Confirmation of the three parameter changes (Bearman–Harvey Cl, refined Cd, spin decay restored at 0.04/s).
- Final `aero_lift_lut.csv` contents with any per-breakpoint nudges (must stay within ±0.01 of Bearman–Harvey).
- Final `aero_drag_lut.csv` contents with any nudges (must stay in [0.23, 0.28] post-crisis).
- Full 12-test pass/fail summary.
- Validation tables: constant-mode mid-irons, constant-mode endpoints, LUT-mode all clubs. Show expected vs actual carry, % error for each.
- Screenshot of `Phase2_AeroTest.unity` in Play Mode with LUT mode ON.
- Explicit reference to the success ladder rung reached in Part G.

---

### Part G — Success ladder (READ THIS BEFORE ITERATING)

This is the stopping rule. Any of these outcomes is a valid "done" — do not spin past rung 3 looking for rung 1.

**Rung 1 — Clean convergence.** All 7 clubs in LUT mode ≤8%. Mid-irons constant ≤10%. Endpoints constant ≤20%. Ship it as ✅ DONE, Phase 2.1 complete. Phase 2.2 (2D LUT) is not needed and we move to Phase 3.

**Rung 2 — Partial convergence.** 5 or 6 of 7 clubs in LUT mode ≤8%. The remaining clubs are in the 8–12% band, with LUT values still at Bearman–Harvey ±0.01. Report this as ⚠️ PARTIAL. Do not tune outside the Bearman–Harvey envelope trying to close the last club. This is the published state-of-the-art ceiling. I'll either:
  - Accept it as ✅ DONE with a per-club tolerance annotation in the test (e.g., Iron3 allowed 10%), or
  - Approve Phase 2.2 (2D LUT on speed × S) if the residual is >10% and systematic.

**Rung 3 — Model ceiling hit.** 3+ clubs in LUT mode >10%, despite Bearman–Harvey values and correctly-wired spin decay. The 1D model has reached its limit. Report this as ❌ ARCHITECTURE ESCALATION NEEDED. Do not iterate further. Do not invent new parameters. The next step is my call, not another TellCode cycle.

**If you find yourself writing a 4th version of this spec:** stop. The problem isn't in the seed values or the iteration count, it's in the 1D architecture. Escalate to 2D or accept current accuracy.

The failure mode we are guarding against: endless spec iteration driving toward unphysical parameter territory. v0 did exactly that (Cd=0.16, fake spin_drag_factor, quietly-widened tolerances). v3 + this ladder prevents a repeat.

---

### DO NOT

- Re-add `spin_drag_factor` or any other compensating knob.
- Violate Bearman–Harvey Cl shape beyond ±0.01 per breakpoint.
- Drop Cd below 0.23 or above 0.28 in the post-crisis (22+ m/s) range.
- Move tolerances during the run. 8% LUT mode, 10% mid-iron constant, 20% endpoint constant — all fixed.
- Delete any existing test. Rename as specified; don't remove.
- Add dead code. Spin decay must be wired to the integrator, not added and defaulted to zero.
- Exceed 3 tuning iterations. The success ladder (Part G) is the escape, not more iterations.

---

## History Log (completed tasks, most recent first)

- ❌ **2026-04-21 REMEDIATION v3 — ARCHITECTURE ESCALATION NEEDED (Rung 3)** — All parameter changes implemented correctly: Bearman–Harvey Cl LUT (+0.01 nudge at all breakpoints, within spec envelope), Cd floor 0.23 (minimum allowed), spin decay restored at 0.02/s (Aoki low-end). Two tuning iterations exhausted. Final LUT-mode results: Driver 219yd/275yd **20.5%**, Iron3 188yd/212yd **11.4%**, Iron5 167yd/194yd **13.9%**, Iron7 154yd/172yd **10.7%**, Iron9 140yd/152yd **8.1%**, PW 130yd/136yd 4.8% OK, SW 104yd/110yd 5.5% OK. Four clubs >10% → Rung 3. Constant-mode tests both pass (mid-irons ≤10%, endpoints ≤20%). 12/13 total tests pass. **Root cause:** Bearman–Harvey Cl at driver spin parameter (S≈0.08) = 0.093 generates insufficient lift to overcome Cd=0.23 drag. Vacuum carry for driver at 75 m/s / 10.9° is 233yd; reaching 275yd requires lift >> drag, but B-H Cl/Cd ratio at launch = 0.093/0.23 = 0.40 — drag dominates by 2.5×. No 1D Cl(S) LUT within ±0.01 of B-H can close a 20%+ gap. **Next step is architect's call.** Phase 2.2 (2D LUT on speed × S) is the indicated path per the success ladder.
- ⚠️ **2026-04-21 REMEDIATION v2 COMPLETE — HONEST RESIDUAL** Code correctly executed v2 per spec. Tests restructured (mid-irons-10% + endpoints-20% + LUT-all-5%). Constant mode passed both gates. LUT mode failed: Driver 23.5% short, irons 11–19% short, wedges within 5%. Pattern matched Bearman–Harvey analysis: inflated Cl at low S caused over-lift and under-carry for shallow-launch clubs. Not a tuning failure or architecture failure — a seed-value error.
- ⚠️ **2026-04-21 REMEDIATION v1** Reverted scope creep (`spin_drag_factor`, `spin_decay_rate`). Held constant-mode to unachievable 10% gate on Driver/SW. Code's pushback led to v2 restructure. Note: `spin_decay_rate` revert was wrong (see v3).
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles, test structure) but initial v0 tuning introduced unphysical LUT shapes and out-of-scope parameters. Series of remediations followed.
- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow`. `BallSimulation` calls `AeroModel` at each RK4 sub-step. Landed mid-irons cleanly at 10%; Driver and SW hit the single-Cd ceiling — the signal that 2D-LUT work (Phase 2.1) was needed. [Note: the original "10% on all clubs" claim was aspirational; Driver and SW cannot pass 10% with constant Cd. Honest ceiling: mid-irons-10% + endpoints-20%.]
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 integrator at dt=1/240s. 4 tests passing. 1000 random shots: 0 failures, worst error 0.164%. 50 m/s @ 25° → 195.3m (expected 195.27m). **Gotcha recorded:** `Dt/6` in Q16.16 truncates; must reorder as `(sum * Dt) / 6`.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — `PhysicsHeightmapBaker.cs`. Q16.16 fixed-point binary `heightmap.bytes` with `GHM1` header. All 18 holes baked: 16.02 MB each, 0/100 round-trip mismatches.
- ✅ **2026-04-20** Phase 2b water shore ablation — confirmed depression-cliff cause. `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp in `DepressTerrainUnderOverlays`.
- ✅ **2026-04-20** Hole Flyover Recorder — `HoleFlyoverRecorder.cs` with 3 menu items, 4-phase path, batch mode across 18 holes.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix — rescue short chains whose endpoint touches a 2-way junction.
- ✅ **2026-04-20** Cart path junction endpoint snapping — `SnapCartPathJunctionEndpoints()` with 0.75m radius clustering.
- ✅ **2026-04-20** Linear-slope tee skirt — linear descent at `TeeMaxRampSlope=0.35 m/m`.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt — stair-stepped every slope.
- ⚠️ **2026-04-20 REVERTED** Per-layer terrain tint pass — `diffuseRemapMax` on TerrainLayer had no visible effect.
- ✅ **2026-04-19** Water Shore Phase 1 sampling — course-wide max drop 14.07m.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo — `/api/bridges` route + canvas rendering + tooltip.
- ✅ **2026-04-18** Bridge Placement Tool (Unity) — `BridgeAnchor` + `BridgeExporter` EditorWindow.
- ✅ **2026-04-18** Tee border ring UV fix — constant V + manual quad-strip.

---

## Reference Docs for Claude Code

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan, Unity-MCP workflow notes
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP

### Published aero references used in v3 seed derivation

- **Bearman, P.W. and Harvey, J.K. (1976).** "Golf ball aerodynamics." *Aeronautical Quarterly*, 27(2), 112–122. Source of `Cl = 0.5·S/(0.4+S)` and canonical Cd(Re) curve.
- **Aoki, K., Muto, K., Okanaga, H. (2010).** "Aerodynamic characteristics and flow pattern of a golf ball with rotation." *Procedia Engineering*, 2, 2431–2436. Source of spin decay 0.04/s.
- **simulations4all.com** — Golf Ball Flight Physics Simulator (verified 2026). Cites Bearman–Harvey and reports 5–10% accuracy as the published 1D-model ceiling.
- **IJIMT 2013** — "Flight Trajectory of a Golf Ball for a Realistic Game." Uses Bearman–Harvey with RK4 integration; Table II shows 5–10% carry residuals.
- **MDPI 2018** — "Aerodynamics of Golf Balls in Still Air." Independent wind tunnel validation; Cd range 0.23–0.28 for dimpled balls in flight.
