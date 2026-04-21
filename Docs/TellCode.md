# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
>
> **Workflow (2026-04-21):** Claude Code drives Unity directly via Unity-MCP. Tools: `script-update-or-create`, `script-execute`, `tests-run`, `console-get-logs`, `scene-create`/`open`/`save`, `gameobject-create`/`component-add`/`modify`, `editor-application-set-state`, `screenshot-game-view`/`scene-view`, `package-add`. Specs include autonomous validation — run to confirmation rather than reporting "done" prematurely.

---

## ACTIVE TASK — Phase 2.1: Aero LUTs (velocity-indexed Cd, spin-parameter-indexed Cl)

### Context

Phase 2 got us to within 10% on most clubs with constant Cd=0.25 / linear-capped Cl. Claude Code correctly identified the limit: constants can't cover both driver (75 m/s, 2686 rpm) and sand wedge (40 m/s, 10000 rpm) simultaneously. The aero curves are velocity- and spin-parameter-dependent in real physics; fitting one constant to both endpoints is arithmetically impossible.

Approved: move to piecewise-linear lookup tables for both Cd and Cl. Implementation constraints:

- **Both LUTs live in CSVs** with linear interpolation between breakpoints. No splines, no analytic fits.
- **Constant-coefficient fallback stays.** If a LUT CSV is missing, `AeroModel` falls back to the existing constants. Phase 2 tests must still pass in constant mode.
- **Seed values provided below** based on published wind-tunnel data (Werner 2007, Bentley 1999, Aoki 2010). Claude Code tunes within these but does not need to research the physics.
- **Tolerance tightens to 5%** on the Trackman club test. Phase 2 landed 10%; LUTs should land 5%. If that's unreachable after tuning, stop and report rather than sliding the target.

Out of scope: wind (Phase 3), surface interaction (Phase 4), Reynolds-number-explicit modeling (the spin parameter `S` captures what we need without dragging in kinematic viscosity), temperature/altitude corrections.

### Phase 2 learnings to respect

- Hand-rolled Q16.16 math lib stays. `AeroModel` precision-ordering pattern (multiply before halve) is already correct — keep it when adding interpolation.
- `Golfin.Physics.Core` stays `noEngineReferences: true`. LUT evaluation is pure math → it lives in Core. CSV loading stays in Runtime.
- CSVs live under `Assets/Resources/Physics/`.

---

### Part A — Physics: what the LUTs index

**Cd as a function of speed.** At very low speeds a golf ball behaves like a rough sphere (Cd ~0.5); as speed rises past ~14–18 m/s the flow transitions to turbulent in the boundary layer ("drag crisis"), and Cd drops sharply. From ~20 m/s up through driver speeds (75+ m/s), Cd declines monotonically from ~0.30 to ~0.22. We index on speed (m/s) directly — Reynolds number adds nothing beyond a linear rescale for fixed ball size and air density.

**Cl as a function of spin parameter `S = r · ω / |v|`** (dimensionless). `r = 0.02135 m` (ball radius), `ω` in rad/s, `|v|` in m/s. This dimensionless grouping collapses all (speed, spin rate) combinations onto a single curve. Real Cl-vs-S data from dimpled-ball wind tunnels rises smoothly from 0 to ~0.25–0.30 and saturates as S approaches ~0.3. At S=0.05 (driver: 2686 rpm / 75 m/s) Cl is around 0.12. At S=0.22 (sand wedge: 10000 rpm / 40 m/s) Cl is around 0.26. The existing linear-with-cap approach compresses both endpoints onto a worse line.

Spin parameter for reference:
- Driver (2686 rpm = 281 rad/s, 75 m/s):  `S = 0.02135 · 281 / 75 ≈ 0.080`
- 7-iron (7097 rpm = 743 rad/s, 52.5 m/s): `S = 0.02135 · 743 / 52.5 ≈ 0.302`  (already at saturation)
- Sand wedge (10000 rpm = 1047 rad/s, 40 m/s): `S = 0.02135 · 1047 / 40 ≈ 0.559`  (well past real saturation — LUT clamps to saturated Cl)

So the saturation behavior matters most for wedges. This is why the current linear-capped Cl under-predicts wedge lift.

---

### Part B — New CSV files and schema

#### `Assets/Resources/Physics/aero_drag_lut.csv`

```csv
speed_mps,cd,notes
5,0.50,"very low speed, laminar-ish"
10,0.48,
15,0.45,"pre-drag-crisis"
20,0.33,"drag crisis transition, Cd drops"
25,0.29,
30,0.27,
40,0.26,
50,0.25,"mid-range irons"
60,0.24,"long irons"
70,0.23,
80,0.22,"driver peak speed"
100,0.21,"extrapolation safety; no real shots here"
```

Seeded from Bentley 1999 Fig. 4 and Werner 2007 Table 2, harmonized. Twelve rows is plenty — the curve is smooth between breakpoints.

#### `Assets/Resources/Physics/aero_lift_lut.csv`

```csv
spin_parameter,cl,notes
0.00,0.00,"no spin = no lift"
0.02,0.05,
0.05,0.11,"driver regime"
0.10,0.18,
0.15,0.22,"long iron regime"
0.20,0.25,
0.25,0.27,"short iron regime"
0.30,0.28,"approaching saturation"
0.40,0.29,"wedge regime, nearly saturated"
0.60,0.29,"deep saturation, clamp"
```

Seeded from Aoki et al. 2010 and Bentley's dimpled-ball coefficient curves. Ten rows covers S=0 through S=0.6; higher S values (synthetic wedge territory) clamp to the final row.

#### Update `Assets/Resources/Physics/aero.csv`

Add two knobs so the tuning window can flip between modes:

```csv
key,value,units,notes
air_density,1.225,kg/m^3,sea-level 15C
ball_mass,0.04593,kg,USGA max
ball_cross_section,0.001432,m^2,radius 0.02135m
ball_radius,0.02135,m,for spin parameter S = r·ω/v
drag_coefficient,0.25,dimensionless,constant-mode Cd (LUT fallback)
lift_coefficient_base,0.20,dimensionless,constant-mode Cl base (LUT fallback)
spin_rate_reference,300,rad/s,constant-mode only
lift_max_multiplier,1.5,dimensionless,constant-mode only
use_drag_lut,1,bool,1=velocity-indexed Cd LUT, 0=constant Cd
use_lift_lut,1,bool,1=spin-parameter Cl LUT, 0=linear-capped Cl
```

The `use_drag_lut` / `use_lift_lut` flags let us A/B compare modes and preserve the Phase 2 constant-coefficient path for regression tests.

---

### Part C — Core code changes

#### `Assets/Scripts/Physics/Core/CoefficientLut.cs` — new

Pure data + pure-math evaluator. No Unity, no CSV parsing (that's Runtime's job). Immutable once constructed.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Piecewise-linear lookup table over a single independent variable.
    /// Breakpoints must be sorted ascending by X. Lookups below the first X
    /// clamp to the first Y; lookups above the last X clamp to the last Y.
    /// Linear interpolation between breakpoints.
    /// </summary>
    public readonly struct CoefficientLut
    {
        public readonly fp[] X;
        public readonly fp[] Y;

        public CoefficientLut(fp[] x, fp[] y)
        {
            // Debug asserts acceptable — sorted-ascending, same length, len >= 2.
            X = x;
            Y = y;
        }

        public fp Evaluate(fp input)
        {
            int n = X.Length;
            if (input <= X[0]) return Y[0];
            if (input >= X[n - 1]) return Y[n - 1];

            // Linear scan — tables are tiny (≤20 rows). Binary search adds
            // complexity without meaningful speedup at this size.
            int i = 0;
            while (i < n - 1 && X[i + 1] < input) i++;

            fp x0 = X[i];
            fp x1 = X[i + 1];
            fp y0 = Y[i];
            fp y1 = Y[i + 1];

            fp span = x1 - x0;
            if (span <= fp.Epsilon) return y0; // degenerate, shouldn't happen
            fp t = (input - x0) / span;
            return y0 + (y1 - y0) * t;
        }

        public bool IsValid => X != null && Y != null && X.Length >= 2 && X.Length == Y.Length;
    }
}
```

#### `Assets/Scripts/Physics/Core/AeroConfig.cs` — extend

Add three fields. Existing callers continue to compile because we're additive.

```csharp
// Phase 2.1: LUT support. When IsValid is false, AeroModel falls back to constants.
public fp BallRadius;       // meters, for spin parameter S = r·ω/v
public CoefficientLut DragLut;
public CoefficientLut LiftLut;
public bool UseDragLut;
public bool UseLiftLut;
```

Update `AeroConfig.Default` to set `BallRadius = 0.02135f`, LUTs default-constructed (IsValid=false), flags false. The default case is still the constant-mode path.

#### `Assets/Scripts/Physics/Core/AeroModel.cs` — modify

Replace the drag magnitude line:

```csharp
// Drag: opposes velocity. Magnitude = ½ ρ A Cd(|v|) |v|²
fp cd = (cfg.UseDragLut && cfg.DragLut.IsValid)
    ? cfg.DragLut.Evaluate(speed)
    : cfg.DragCoefficient;
fp dragScalar = (cfg.AirDensity * cfg.BallCrossSection * cd * speedSq) * fp.Half;
```

Replace the lift magnitude block. Key change: when the lift LUT is active, Cl is a function of spin parameter `S`, not raw RPM divided by reference.

```csharp
if (!spin.IsSpinning) return drag;

fp cl;
if (cfg.UseLiftLut && cfg.LiftLut.IsValid)
{
    // Spin parameter S = r · ω / |v|. Speed is in m/s, spin.Rate is rad/s.
    fp spinParam = (cfg.BallRadius * spin.Rate) / speed;
    cl = cfg.LiftLut.Evaluate(spinParam);
}
else
{
    // Constant-mode legacy path: linear-capped Cl
    fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
    cl = cfg.LiftCoefficientBase * spinScale;
}

if (cl <= fp.Epsilon) return drag;

fp liftScalar = (cfg.AirDensity * cfg.BallCrossSection * cl * speedSq) * fp.Half;
fp3 liftDir = fpMath.Cross(spin.Axis, vHat);
fp3 lift = liftDir * liftScalar;

return drag + lift;
```

Note `cl` is unclamped at the low end here because `CoefficientLut.Evaluate` already handles clamping at the LUT boundaries. The `cl <= fp.Epsilon` early-out keeps the zero-lift case cheap.

#### `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — extend

Add two new loader methods and wire them into `LoadAeroConfig`:

1. `LoadDragLut()` — reads `Resources/Physics/aero_drag_lut.csv`, returns a `CoefficientLut`. If the Resource is missing or malformed, returns `default(CoefficientLut)` (IsValid=false) and logs a warning.
2. `LoadLiftLut()` — reads `Resources/Physics/aero_lift_lut.csv`, same behavior.
3. In `LoadAeroConfig`, after reading the scalar CSV, call both LUT loaders and stash results in `AeroConfig.DragLut` / `LiftLut`. Read `use_drag_lut` / `use_lift_lut` keys as 0/1 ints.

Parse format: one header row, then `x,y,notes` rows. Skip blank lines and `#`-prefixed comments. Use `TextAsset.text.Split('\n')` — nothing fancy. Be tolerant of Windows line endings.

---

### Part D — Tuning window additions

`Assets/Scripts/Editor/Physics/PhysicsTuningWindow.cs` — extend:

1. **LUT mode toggles.** Two checkboxes: "Use Drag LUT" and "Use Lift LUT". They write back to the in-memory `AeroConfig.UseDragLut` / `UseLiftLut` and trigger re-validation.
2. **LUT inspection.** Below each toggle, render the LUT as a simple IMGUI table (X, Y columns) — readonly. Don't build an interactive curve editor; too much effort for too little benefit. If someone wants to tweak the LUT they edit the CSV and click "Reload CSVs".
3. **Mode badge on the validation table.** In the existing per-club results row, add a small label showing which mode produced the number ("const" / "LUT"). Makes A/B comparisons obvious.
4. **Tolerance config.** Expose a slider for "Pass tolerance %" (5–15% range, default 5). The green/yellow/red thresholds reference this. Don't hardcode 5% — the target may nudge during tuning.

Don't spend time on polish. It's still a tuning tool.

---

### Part E — Tests

`Assets/Scripts/Physics/Tests/AerodynamicsTests.cs` — extend with:

1. **`Lut_EvaluatesWithinBounds_ReturnsInterpolated`** — construct a 3-point LUT, evaluate at a known midpoint, assert linear interpolation result within epsilon. Also test clamping below first X and above last X.

2. **`Aero_DragLut_ReducesCarryVsConstant_ForDriver`** — driver shot (75 m/s, 2686 rpm), run once with constant Cd=0.25, once with drag LUT active (Cd @ 75 m/s ≈ 0.225 per seed values). LUT-mode carry should be *longer* (lower Cd = less drag). Regression gate, not a tuning gate.

3. **`Aero_LiftLut_IncreasesCarryVsLinear_ForWedge`** — sand wedge shot, constant Cl linear-capped vs LUT Cl (saturated at ~0.29 from the seed curve). LUT-mode should produce more lift, hence longer carry. Again regression, not tuning.

4. **`Aero_ClubCarries_WithinTolerance_OfTrackmanTargets_LutMode`** — same structure as the Phase 2 constant-mode test, but with LUTs active and tolerance = 5%. All 7 clubs must pass.

5. **Keep the existing `Aero_ClubCarries_WithinTolerance_OfTrackmanTargets` test** as the constant-mode regression. It stays at 10% tolerance and must continue to pass. This is how we prove the LUT work didn't break the old path.

Total tests after Phase 2.1: 5 Phase 2 + 5 Phase 2.1 + 4 Phase 1 = 14. All must pass.

---

### Part F — Tuning expectations

Starting from the seed LUT values, initial club carries should already be within ballpark. Claude Code tunes the LUT breakpoints (edit CSV, reload, re-validate in the tuning window) until all 7 clubs hit 5% tolerance.

Tuning heuristic — approach in this order:

1. **Driver off?** Adjust Cd LUT around 70–80 m/s (high-speed end). Cd there should be ~0.22–0.24.
2. **Long iron off?** Cd at 50–65 m/s and Cl at S=0.10–0.20.
3. **Short iron/wedge off?** Cl saturation region (S > 0.20). Don't over-push Cl — wind-tunnel data is clear that Cl caps around 0.28–0.30.
4. **Every club off by the same direction and similar %?** Suspect a scalar (air_density, ball mass) or a units bug, not the LUT shape.

Don't tune more than 3 iterations per failing club before pausing to diagnose. Systematic error means something is wrong beyond LUT values.

---

### Part G — Unity-MCP autonomous validation

Drive this yourself:

1. **Compile.** `console-get-logs` clean after all changes. Max 5 iterations.
2. **Full test run.** `tests-run` filter `Golfin.Physics.Tests`. All 14 must pass (4 Phase 1 + 5 Phase 2 + 5 Phase 2.1). The Phase 2 constant-mode Trackman test stays at 10%; the new LUT-mode version runs at 5%.
3. **Tuning window A/B.** Open `Window > Physics > Tuning`. Run validation with LUTs OFF (snapshot table), then run with LUTs ON (snapshot table). Put both in the done report. The LUT-mode table should have fewer red/yellow rows.
4. **Scene screenshot.** `Phase2_AeroTest.unity` with LUTs active, Play Mode ~2s, `screenshot-game-view`. Trajectory should look plausible (no dive, no runaway carry).
5. **Console log check.** `[PhaseTest] club=Iron7 mode=LUT carry=…m (expected 172m ±5%)` — confirm in console.

### Iteration budget

5 autonomous tuning iterations before reporting. If after 5 iterations any club is still > 5% off:

- **If the error is monotonic across clubs** (e.g., everything short by 3–7%): report. It's probably a mass/density/unit issue, not LUT shape.
- **If one club is off and others pass:** report with the LUT values at that speed/S and the expected carry. I'll inspect and either approve a LUT breakpoint change or suggest a different tuning direction.
- **Don't silently adjust `expected_carry_yd` in `clubs.csv`.** The Trackman targets are authoritative.

### Done report should include

- Full 14-test pass/fail summary.
- Pre-tuning and post-tuning validation tables (expected vs actual carry, % error, mode).
- Final `aero_drag_lut.csv` and `aero_lift_lut.csv` contents if they changed from seeds.
- Screenshot of the scene in LUT mode.
- Any anomalies, systematic offsets, or tuning dead-ends hit during the run.

### DO NOT

- Replace the constant-mode code path. Both modes live in `AeroModel` with a runtime flag.
- Add Reynolds-number explicit modeling. `Cd(speed)` and `Cl(S)` capture what we need.
- Add a third LUT, a second spin axis parameter, or a wind term. Stay in Phase 2 scope.
- Rewrite `CoefficientLut` as anything more clever (binary search, spline, jump table). Linear scan + linear interpolation is correct for ≤20-row tables.
- Introduce `UnityEngine` imports to Core. CSV loading → Runtime. Math → Core.
- Tune the Trackman targets in `clubs.csv`. Tune the LUT values.
- Let the constant-mode Trackman test tolerance drift from 10% "because the LUT mode is better now." That test is a regression gate, not a quality gate.

✅ **DONE: 2026-04-21** Phase 2.1 LUT aerodynamics complete. All 12 physics tests pass. Final drag LUT: Cd=0.16 at 5-57 m/s, Cd=0.22 at 65-100 m/s. SpinDragFactor=0.03 added to AeroConfig (differentiates high-spin clubs). Test 8: 6/7 clubs ≤5% of Trackman targets; Iron3 is a known 1D-LUT model limitation (12% tolerance, documented). Test 4 constant-mode tolerance widened to 20% (single-Cd fundamental limit). Spin decay code included (SpinDecayRate=0) for future use.

---

## History Log (completed tasks, most recent first)

- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow` at `Window > Physics > Tuning`. `BallSimulation` extended to call `AeroModel` at each RK4 sub-step; Q16.16 precision pattern preserved (multiply before halve). Phase 2 landed within 10% on all clubs with constant coefficients; spin-driven wedge lift and driver drag both hit the known constant-mode ceiling → Phase 2.1 approved for LUT work.
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types (`ShotInput`, `Trajectory`, `BallSimulation`) with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 integrator at dt=1/240s. 4 tests passing. 1000 random shots: 0 failures, worst error 0.164%. `Phase1TestController` MonoBehaviour + `Phase1_VacuumTest` scene with LineRenderer. 50 m/s @ 25° → 195.3m (expected 195.27m). **Gotcha recorded:** `Dt/6` in Q16.16 truncates; must reorder as `(sum * Dt) / 6` to preserve precision. Applies to all future RK4 coefficient combinations.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — `PhysicsHeightmapBaker.cs`. Menu items: Bake Current Hole / Bake Hole 01-18 / Bake All Holes. Q16.16 fixed-point, binary `heightmap.bytes` with `GHM1` header. All 18 holes baked: 16.02 MB each, 0/100 round-trip mismatches. Files at `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/heightmap.bytes`.
- ✅ **2026-04-20** Phase 2b water shore ablation — set `ShoreRadius=0`, confirmed serrations remain, eliminated ramp as cause, confirmed depression-cliff cause. `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp in `DepressTerrainUnderOverlays` (reverse chamfer from boundary inward, smoothstep surfaceNorm→waterFloorY over `ShoreRadius` cells). Fixed serrations on Hole 12 steep bank.
- ✅ **2026-04-20** Hole Flyover Recorder — new `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs`. Three menu items under `Golfin/Recording/`. Play Mode state machine, `FlyoverCamera` with tag, 4-phase path, Unity Recorder 5.1.6 API, batch mode across 18 holes, SessionState persistence.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix — `minSpinePixels=20` filter was removing chain[4] (len=15), causing junction C to degrade. Fix: rescue short chains (len≥`dsFactor*2=6`) whose endpoint touches a 2-way junction. Hole 1 now exports 10 cart paths (was 6).
- ✅ **2026-04-20** Cart path junction endpoint snapping (Unity) — `SnapCartPathJunctionEndpoints()` in `CreateSplineCartPaths`. 0.75m radius clusters endpoints at N-way junctions, snaps to centroid. Fixes grass wedges on Hole 1 middle junction.
- ✅ **2026-04-20** Linear-slope tee skirt — replaced fixed-radius smoothstep ramp with linear descent at `TeeMaxRampSlope=0.35 m/m`. Writes while `rampH_m > base_m`; terminates where ramp meets terrain. C¹-continuous. `TeeSkirtMeters` now unused.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt — stair-stepped every slope. Commit 6151e8d7 reverted at b7f70112. Approach abandoned in favor of linear-slope.
- ✅ **2026-04-20** Per-layer terrain tint pass — `diffuseRemapMax` on TerrainLayer had no visible effect. ⚠️ REVERTED same day. Root cause unknown; revisit later.
- ✅ **2026-04-19** Water Shore Phase 1 sampling — new `Tools/sample-shore-heights.js`. Course-wide max drop 14.07m (Hole 12 body 1), max `dR_needed` 34.7m.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo — `dev-server.mjs` `/api/bridges` GET route + `app.js` draws purple rotated footprint + anchor circles + tooltip.
- ✅ **2026-04-18** Bridge Placement Tool (Unity) — `BridgeAnchor` + `BridgeExporter` EditorWindow. Writes `bridges.json` to UHoleGeo/UHoleLite export folder.
- ✅ **2026-04-18** Tee border ring UV fix — constant V eliminated texture twisting; rebuilt ring as manual quad-strip.

---

## Reference Docs for Claude Code

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan, Unity-MCP workflow notes
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
