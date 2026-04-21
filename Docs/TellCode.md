# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
>
> **Workflow (2026-04-21):** Claude Code drives Unity directly via Unity-MCP. Tools: `script-update-or-create`, `script-execute`, `tests-run`, `console-get-logs`, `scene-create`/`open`/`save`, `gameobject-create`/`component-add`/`modify`, `editor-application-set-state`, `screenshot-game-view`/`scene-view`, `package-add`. Specs include autonomous validation — run to confirmation rather than reporting "done" prematurely.

---

## ✅ DONE: 2026-04-21 Phase 2.1 REMEDIATION v2 — see end-state report below

## ACTIVE TASK — Phase 2.1 REMEDIATION v2: recalibrated LUTs, restructured tests

### Context (what I got wrong on v1)

Code's pushback on the v1 remediation was correct on both counts:

1. **The "Phase 2 passed at 10% constant-mode" history entry was aspirational, not empirical.** I held Code to a gate that single-Cd physics cannot actually pass on Driver and SandWedge. Driver at Cd=0.25 / 75 m/s cannot reach 275 yd regardless of what Cl does; the math doesn't permit it. Accepting this.

2. **The seed LUT values were drawn from static wind-tunnel literature (Werner 2007, Bentley 1999), but the Trackman targets are from on-course trajectories with backspin.** Real in-flight Cd under spin runs ~0.02–0.04 lower than static wind-tunnel values in the post-crisis regime — the same boundary-layer effects that generate Magnus lift also modestly reduce drag. My seed Cd in the 20–35 m/s landing band (0.27–0.33) was physically defensible for wind-tunnel conditions but too high for the targets we're calibrating against.

Not errors on Code's part. Category mismatch on mine.

What this task does:

- Restructures the constant-mode regression test to reflect what constant-mode can physically achieve (tight gate on mid-irons, loose gate on endpoints). Not a relaxation — an honest framing.
- Recalibrates the seed LUTs toward Trackman-consistent values while preserving physical shape constraints.
- Re-runs the LUT-mode test at 5%. Physical constraints still apply; targets are fixed.

The scope-creep revert from v1 still stands: no `spin_drag_factor`, no `spin_decay_rate`. Those were the genuine discipline issue. This remediation is *calibration*, not *discipline*.

### Status check — confirm before starting

Before doing anything, verify these reverts from v1 are in place:

- `aero.csv` has 10 rows ending at `use_lift_lut` (no `spin_drag_factor`, no `spin_decay_rate`)
- `AeroConfig.cs` has no `SpinDragFactor` or `SpinDecayRate` fields
- `AeroModel.cs` drag term is just `Cd · ½ρA|v|²`, no spin multiplier
- `BallSimulation.cs` has no spin-decay code

If all four are true, proceed. If any revert regressed, restore it first.

---

### Part A — Restructure the constant-mode regression test

The Phase 2 constant-mode test is currently a single test with one tolerance. That's the wrong shape for what constant-mode actually does — it passes cleanly on mid-irons and physically cannot pass tightly on endpoints.

**Edit `Assets/Scripts/Physics/Tests/AerodynamicsTests.cs`:**

Replace the existing `Aero_ClubCarries_WithinTolerance_OfTrackmanTargets` test with three tests:

```csharp
[Test]
public void Aero_ClubCarries_ConstantMode_MidIrons_Within10Percent()
{
    // Constant Cd=0.25 + linear-capped Cl can hit the middle of the club range.
    // Iron3 through PitchingWedge all fit within 10% on a single set of knobs.
    // Driver and SandWedge are tested separately — see _Endpoints_Within20Percent.
    var midClubs = new[] { "Iron3", "Iron5", "Iron7", "Iron9", "PitchingWedge" };
    AssertClubCarriesWithinTolerance(midClubs, useLuts: false, tolerance: 0.10);
}

[Test]
public void Aero_ClubCarries_ConstantMode_Endpoints_Within20Percent()
{
    // Driver (75 m/s, S≈0.08) and SandWedge (40 m/s, S≈0.56) span 35 m/s and 7× the
    // spin parameter. Single Cd+Cl physically cannot tune both to 10% — this is the
    // regime where Cd(v) and Cl(S) must differ, which is why LUT mode exists.
    // 20% is the honest ceiling of constant mode on these clubs.
    var endpointClubs = new[] { "Driver", "SandWedge" };
    AssertClubCarriesWithinTolerance(endpointClubs, useLuts: false, tolerance: 0.20);
}

[Test]
public void Aero_ClubCarries_LutMode_AllClubs_Within5Percent()
{
    // Velocity-indexed Cd and spin-parameter-indexed Cl can in principle fit all
    // 7 clubs inside 5% — that's the justification for the LUT work. This is the
    // real quality gate.
    var allClubs = new[] { "Driver", "Iron3", "Iron5", "Iron7", "Iron9", "PitchingWedge", "SandWedge" };
    AssertClubCarriesWithinTolerance(allClubs, useLuts: true, tolerance: 0.05);
}

// Helper — extract the existing loop into a parameterized method.
// Loads clubs.csv, filters to the given IDs, simulates each, asserts carry within tolerance.
private void AssertClubCarriesWithinTolerance(string[] clubIds, bool useLuts, double tolerance)
{
    // ... existing validation logic, filtered by clubIds, with the tolerance and mode parameters
}
```

The old `_LutMode` test becomes `_LutMode_AllClubs_Within5Percent`. It's the quality gate; it keeps the 5% bar.

**Naming signals intent:** `MidIrons_Within10Percent` is tight because mid-irons must pass tight. `Endpoints_Within20Percent` is loose because the physics doesn't allow tight. Reading the test names tells the whole story.

---

### Part B — Recalibrate seed LUTs

Replace both LUT CSVs with these revised values. These are still seeds — Code may tune within the physical constraints below — but they're calibrated to Trackman-consistent trajectories rather than static wind-tunnel tables.

**`Assets/Resources/Physics/aero_drag_lut.csv`:**

```csv
speed_mps,cd,notes
5,0.50,very low speed laminar-ish
10,0.48,
15,0.45,pre-drag-crisis
20,0.28,post-crisis onset Trackman-calibrated
25,0.25,
30,0.24,landing-zone target band
40,0.23,mid-irons cruise
50,0.23,
60,0.22,long irons
70,0.22,
80,0.22,driver peak
100,0.22,extrapolation safety
```

Changes from v1: post-crisis values shifted down ~0.03–0.05 to reflect backspin's boundary-layer effect. The 20 m/s point drops from 0.33 to 0.28. The 80 m/s point drops from 0.22 to 0.22 (unchanged — already near the floor). Mid-range lands around 0.23 instead of 0.25–0.27.

**`Assets/Resources/Physics/aero_lift_lut.csv`:**

```csv
spin_parameter,cl,notes
0.00,0.00,no spin
0.02,0.08,
0.05,0.18,approaching driver regime
0.08,0.22,driver S target Aoki 2010
0.12,0.25,
0.15,0.26,long iron regime
0.20,0.27,
0.25,0.28,short iron regime
0.30,0.28,saturation
0.40,0.29,wedge regime
0.60,0.29,deep saturation clamp
```

Changes from v1: Cl(0.05) bumped from 0.11 to 0.18, Cl(0.08) explicitly seeded at 0.22, upper half tightened into a flatter saturation plateau (0.27–0.29 instead of 0.25–0.29). Driver S ≈ 0.08 now lands on a breakpoint rather than interpolating across a steep rise.

**Physical constraints on tuning (unchanged from v1):**

- Cd monotonically non-increasing from 20 m/s onward.
- Cd at 5–15 m/s in [0.40, 0.55].
- Cd at 20–80 m/s in [0.21, 0.30].
- Cl monotonically non-decreasing.
- Cl ≤ 0.30 at any S.

These are the shape guardrails. Values within the bounds can be tuned ±0.02. Breakpoints can be added. Order cannot change.

---

### Part C — Run tests and tune within constraints

1. Compile clean. `console-get-logs` after changes, resolve any errors (max 5 iterations).
2. Run full suite: `tests-run` filter `Golfin.Physics.Tests`.

**Expected after seed replacement, before tuning:**

- All 4 Phase 1 tests: pass.
- `Aero_Off_MatchesPhase1_Within_Epsilon`: pass.
- `Aero_DragReducesCarry_MonotonicallyWithCd`: pass.
- `Aero_Backspin_ExtendsCarry_VsZeroSpin`: pass.
- `Aero_DragLut_ReducesCarryVsConstant_ForDriver`: pass (LUT Cd at 75 m/s is 0.22 vs constant 0.25 — LUT mode should carry longer).
- `Aero_LiftLut_AffectsCarry_ForWedge` (or whatever it's named — the lift LUT regression test): pass.
- `Lut_EvaluatesWithinBounds_ReturnsInterpolated`: pass.
- `Aero_ClubCarries_ConstantMode_MidIrons_Within10Percent`: should pass. Mid-irons were passing at 0.5–8.6% in Code's report.
- `Aero_ClubCarries_ConstantMode_Endpoints_Within20Percent`: should pass. Driver was 17.8% short and SW was 12.3% long — both inside 20%.
- `Aero_ClubCarries_LutMode_AllClubs_Within5Percent`: may or may not pass on seed values. This is the tuning target.

If the LUT-mode test fails, tune **only** the LUT CSVs within the physical constraints. Max 3 iterations per failing club. Pattern diagnosis:

- **All clubs still short by similar %:** bump Cd values down uniformly by 0.01. If that doesn't close the gap, something in the constant `0.5·ρ·A` chain may be wrong.
- **Long clubs short, short clubs OK:** the 20–50 m/s Cd band is still too high. Tighten that range specifically.
- **Short clubs long, long clubs OK:** Cl at high S is too high. Pull saturation down toward 0.27.
- **Driver specifically short, everything else fine:** bump Cl(0.08) — but not above 0.25 (that's the boundary where the curve gets unphysical).
- **One club anomalously off while all others are fine at 5%:** report. Could be a launch-angle or spin-input bug for that specific club row in clubs.csv, not a LUT issue.

---

### Part D — Honest end states

Any of these is a valid "done":

1. **All 7 clubs ≤5% in LUT mode, mid-irons ≤10% constant, endpoints ≤20% constant.** Ship it.
2. **6/7 clubs ≤5%, one club in 5–8% with physical LUTs.** Report the residual. I'll either widen that club's tolerance with a documented reason or approve escalation to 2D LUT as Phase 2.2.
3. **Multiple clubs stuck >5% with physical LUTs.** Report the pattern and final LUT values. We diagnose from the residual shape.

Ending with scope creep, tolerance widening, or unphysical LUTs is not a valid end state. Ending with a clear report of a physics-bounded limitation **is**.

---

### Part E — Done report

Include:

- Confirmation of the v1 reverts (spin_drag_factor, spin_decay_rate, spin decay code — all gone).
- Final `aero_drag_lut.csv` and `aero_lift_lut.csv` contents.
- All 5 physical constraints satisfied on final LUTs (explicit check).
- Test results for all three constant-mode tests (mid-irons, endpoints, LUT-mode).
- Validation table: for each club, expected vs actual carry in both constant and LUT mode, % error for each.
- Total test count passed/failed (expect 12 total in the suite after this work — 4 Phase 1 + 8 aero).
- Any residuals with diagnostic pattern.

### DO NOT

- Re-add `spin_drag_factor`, `spin_decay_rate`, or any other compensation knob.
- Tune `expected_carry_yd` in clubs.csv.
- Violate the LUT physical constraints.
- Collapse the three constant-mode tests back into one.
- Delete or rename any test. Add, don't remove.
- Widen the LUT-mode 5% tolerance or the mid-iron 10% tolerance. The 20% endpoints tolerance is already as loose as it needs to be.
- Leave dead code. If something gets added experimentally, remove it before reporting done.

### Iteration budget

5 iterations total for tuning. If the LUT-mode test can't pass cleanly after 5, stop and report honest end state (option 2 or 3 above). "Report honest residual" is a valid completion.

---

## History Log (completed tasks, most recent first)

- ⚠️ **2026-04-21 REMEDIATION v2 COMPLETE — HONEST RESIDUAL (option 3)**

  **v1 reverts confirmed:** `spin_drag_factor`, `spin_decay_rate` gone from aero.csv, AeroConfig.cs, AeroModel.cs, BallSimulation.cs. ✓

  **Test restructure done (13 total = 4 Phase 1 + 9 aero):**
  - `Aero_ClubCarries_ConstantMode_MidIrons_Within10Percent` → ✅ PASS (Iron3 5.4%, Iron5 5.7%, Iron7 0.5%, Iron9 4.0%, PW 8.6%)
  - `Aero_ClubCarries_ConstantMode_Endpoints_Within20Percent` → ✅ PASS (Driver 17.8%, SW 12.3%)
  - `Aero_ClubCarries_LutMode_AllClubs_Within5Percent` → ❌ FAIL (see below)
  - All other 10 tests pass ✓

  **Final LUTs (iteration 1, all physical constraints satisfied):**

  `aero_drag_lut.csv`: 5→0.50, 10→0.48, 15→0.45, 20→0.28, 25→0.25, 30→0.24, 40→0.22, 50→0.22, 60→0.21, 70→0.21, 80→0.21, 100→0.21. Monotonically non-increasing from 20 m/s ✓. Pre-crisis [0.40,0.55] ✓. Post-crisis [0.21,0.30] ✓.

  `aero_lift_lut.csv`: S=0.00→0.00, 0.02→0.08, 0.05→0.18, 0.08→0.22, 0.12→0.26, 0.15→0.27, 0.20→0.28, 0.25→0.29, 0.30→0.29, 0.40→0.30, 0.60→0.30. Monotonically non-decreasing ✓. Max 0.30 ✓.

  **LUT-mode validation table:**
  ```
  Driver          expected=275yd  actual=210yd  err=23.5%  FAIL
  Iron3           expected=212yd  actual=183yd  err=13.9%  FAIL
  Iron5           expected=194yd  actual=164yd  err=15.5%  FAIL
  Iron7           expected=172yd  actual=153yd  err=11.3%  FAIL
  Iron9           expected=152yd  actual=140yd  err= 8.2%  FAIL
  PitchingWedge   expected=136yd  actual=130yd  err= 4.7%  OK
  SandWedge       expected=110yd  actual=105yd  err= 4.9%  OK
  ```

  **Residual diagnosis — a physics-model finding, not a tuning failure:**

  During tuning, reducing Cd at 60–100 m/s (0.22→0.21) *worsened* Driver carry (215→210 yd). Raising Cl similarly failed to close the gap. Root cause confirmed by simulation: the Magnus lift force direction vector is Cross(spinAxis, vHat) = (0, cos θ, −sin θ) during ascent. For shallow-launch clubs (Driver 10.9°, Iron7 16.3°), the −sin θ Z-component is backward during the ascending phase. Lower Cd → faster ball → stronger Magnus force → stronger backward horizontal impulse → *less* carry despite less drag. This is why constant mode (Cd=0.25, higher drag, slower ball) consistently outperforms LUT mode for irons and driver: the drag acts as a governor on the backward Magnus component.

  This effect is not tunable away within the 1D Cd(v)/Cl(S) LUT architecture. The Trackman targets for Driver (275 yd) and the iron range require either: (a) a 2D LUT indexed on both speed and spin parameter to decouple drag from spin-induced drag, or (b) a Magnus force formulation that separates the vertical lift component from the horizontal carry component.

  **Recommendation:** Phase 2.2 — 2D LUT (speed × S) or a Magnus decomposition. The 1D model's ceiling is ~226 yd for Driver and ~171 yd for Iron7 (constant mode); LUT mode makes these *worse*.

- ⚠️ **2026-04-21 REMEDIATION v2 IN PROGRESS** — v1 remediation correctly reverted scope creep (`spin_drag_factor`, `spin_decay_rate`) but held constant-mode to an unachievable 10% gate on Driver/SW. Code's pushback was correct on two counts: (a) constant Cd=0.25 physically cannot span Driver + SW to 10%, (b) seed LUTs were wind-tunnel-literature values but Trackman targets reflect on-course trajectories with backspin-reduced Cd. v2 restructures the constant-mode test into mid-irons-10% + endpoints-20% + LUT-all-5%, and recalibrates seed LUTs downward in the post-crisis regime.
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles, test structure) but v1 tuning produced unphysical LUT shapes plus out-of-scope parameters. v1 remediation reverted the scope creep; v2 fixes the calibration.
- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow`. `BallSimulation` calls `AeroModel` at each RK4 sub-step. Landed mid-irons cleanly; Driver and SW hit the single-Cd ceiling — which was the signal that LUTs (Phase 2.1) were needed. [Historical note: the "10% on all clubs" claim in earlier log entries was aspirational — single Cd cannot physically hit Driver at 275 yd from 75 m/s. The honest ceiling is mid-irons-10% + endpoints-20%.]
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 integrator at dt=1/240s. 4 tests passing. 1000 random shots: 0 failures, worst error 0.164%. 50 m/s @ 25° → 195.3m (expected 195.27m). **Gotcha recorded:** `Dt/6` in Q16.16 truncates; must reorder as `(sum * Dt) / 6`.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — `PhysicsHeightmapBaker.cs`. Q16.16 fixed-point binary `heightmap.bytes` with `GHM1` header. All 18 holes baked: 16.02 MB each, 0/100 round-trip mismatches.
- ✅ **2026-04-20** Phase 2b water shore ablation — confirmed depression-cliff cause (Hypothesis B). `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp in `DepressTerrainUnderOverlays`. Fixed serrations on Hole 12 steep bank.
- ✅ **2026-04-20** Hole Flyover Recorder — `HoleFlyoverRecorder.cs` with 3 menu items, 4-phase path, batch mode across 18 holes.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix — rescue short chains whose endpoint touches a 2-way junction. Hole 1 now exports 10 cart paths (was 6).
- ✅ **2026-04-20** Cart path junction endpoint snapping — `SnapCartPathJunctionEndpoints()` with 0.75m radius clustering, snaps to centroid.
- ✅ **2026-04-20** Linear-slope tee skirt — linear descent at `TeeMaxRampSlope=0.35 m/m`, writes while `rampH_m > base_m`. C¹-continuous.
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
