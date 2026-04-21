# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
>
> **Workflow (2026-04-21):** Claude Code drives Unity directly via Unity-MCP. Tools: `script-update-or-create`, `script-execute`, `tests-run`, `console-get-logs`, `scene-create`/`open`/`save`, `gameobject-create`/`component-add`/`modify`, `editor-application-set-state`, `screenshot-game-view`/`scene-view`, `package-add`. Specs include autonomous validation — run to confirmation rather than reporting "done" prematurely.

---

## ACTIVE TASK — Phase 2: Aerodynamics (drag + Magnus lift)

### Context

Phase 1 established the pure-gravity RK4 integrator and the hand-rolled Q16.16 fixed-point math. Now we add air drag and Magnus lift so ball carry distances approach real PGA Tour numbers. Phase 1's vacuum 7-iron at ~160 ft/s would carry over 250 yards — real is ~172. The missing mass is aerodynamic drag, with a partial offset from Magnus-generated lift.

Phase 2 lands:

1. A `SpinState` concept on `ShotInput` — spin axis + spin rate (rad/s), expressed in Q16.16.
2. An `AeroModel` force calculator invoked inside the RK4 step.
3. A CSV-driven coefficient table (`Assets/Resources/Physics/aero.csv`) with hot-reload in the Editor.
4. A club-parameter CSV (`Assets/Resources/Physics/clubs.csv`) so the test harness can launch at per-club speed + loft + spin and validate against Trackman averages in `PHYSICS_TUNING_TARGETS.md`.
5. A tuning EditorWindow (`Window > Physics > Tuning`) with sliders on the top-level aero knobs + a "Run Validation" button.
6. Updated `Phase1TestController` (rename to `PhaseTestController`) with a "show trajectory with and without drag" comparison mode so the effect is visually obvious.

Out of scope: wind (Phase 3), surface interaction (Phase 4), putting (Phase 5), stat modifiers (Phase 6 integration), surface-material tuning.

See `Docs/PHYSICS_RESEARCH.md` Section 3 (trajectory model) and `Docs/PHYSICS_TUNING_TARGETS.md` Sections 1 + 7 for canonical numbers.

### Phase 1 learnings to respect

- **Q16.16 integer-math gotcha:** `Dt / (fp)6` truncates — always reorder to `(sum * Dt) / (fp)6` to keep precision in the multiply. This will bite again in Phase 2. Audit every RK4 coefficient combination for this pattern.
- **Hand-rolled `fp`/`fp3` types from `Golfin.Physics.Math` stay the single source of truth.** Do NOT introduce `Unity.Mathematics.FixedPoint` as a second math lib. Extend the existing types if new operations are needed (e.g. `fpMath.Cross`, `fpMath.Exp`).
- **`Golfin.Physics.Core` asmdef remains `noEngineReferences: true`.** AeroModel, CSV loaders (when they return data structs, not when they load from disk), and all math stay engine-free.
- **Tests live in `Golfin.Physics.Tests`** — keep the pattern.

---

### Part A — Physics model

Forces at each RK4 sub-step:

```
F_total = F_gravity + F_drag + F_lift
a = F_total / m
```

**Drag:** `F_drag = -½ · ρ · A · Cd · |v| · v`  (opposes velocity, quadratic in speed)

**Lift (Magnus):** `F_lift = ½ · ρ · A · Cl · |v|² · (ŵ × v̂)`  where ŵ is the normalized spin axis.

Note the `(ŵ × v̂)` direction: for backspin (spin axis pointing left when looking down-range, i.e. `-X` for a ball traveling +Z), this produces upward lift. Sidespin tilts the axis off-horizontal and bends the flight left or right. Topspin is backspin negated — the ball dives.

Constants:

- `m = 0.04593 kg` (USGA max ball mass)
- `A = 0.001432 m²` (standard ball cross-section, radius 0.02135 m)
- `ρ = 1.225 kg/m³` (sea-level air density at 15°C)
- `Cd ≈ 0.25` starting point (Phase 2.0 uses constant; Phase 2.1 may move to LUT if tuning demands)
- `Cl ≈ 0.20` starting point, scaled by spin rate below

**Spin-to-Cl coupling:** lift scales roughly with spin rate. Use a simple linear scale up to a cap:

```
Cl_effective = Cl_base · clamp(spinRate / spinRateRef, 0, ClMaxMult)
```

Starting values: `spinRateRef = 300 rad/s` (~2865 rpm, typical driver), `ClMaxMult = 1.5`.

**Adversarial note:** real Cd/Cl are Reynolds-number-dependent and show a "drag crisis" near the transition. Constants get us ~80% realism. If driver vs wedge carries diverge badly from targets after tuning, switch to a velocity-indexed LUT in `aero.csv` (see Part C). Don't add LUT complexity preemptively — start with constants, measure, escalate only if needed.

---

### Part B — Core code changes

#### `Assets/Scripts/Physics/Math/fpMath.cs` — add if not present

Add these operations to the existing `fpMath` static class:

- `fp3 Cross(fp3 a, fp3 b)` — standard 3D cross product.
- `fp3 Normalize(fp3 v)` — returns `v / max(|v|, fp.Epsilon)`. Guard against zero-length.
- `fp Exp(fp x)` — only if needed for Phase 3 wind altitude profile; can defer. For Phase 2, skip.

Leave a comment at each new method: `// Phase 2: added for aero model.`

#### `Assets/Scripts/Physics/Core/SpinState.cs` — new

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Core
{
    /// <summary>
    /// Ball spin at the moment of impact. Axis is normalized; rate is rad/s.
    /// Zero spin → identity (axis=(0,0,1), rate=0) — use IsSpinning to check.
    /// </summary>
    public readonly struct SpinState
    {
        public readonly fp3 Axis;
        public readonly fp Rate;

        public SpinState(fp3 axis, fp rate)
        {
            Axis = axis;
            Rate = rate;
        }

        public bool IsSpinning => Rate > fp.FromRaw(1); // >0 in Q16.16

        public static SpinState None => new SpinState(new fp3(fp.Zero, fp.Zero, fp.One), fp.Zero);
    }
}
```

#### `Assets/Scripts/Physics/Core/ShotInput.cs` — extend

Add a `Spin` field of type `SpinState`. Add an overload constructor that takes spin; existing Phase 1 call sites can continue using the no-spin constructor which defaults `Spin = SpinState.None`.

**DO NOT** rename or break the existing constructor signature. Phase 1 test code still uses it.

#### `Assets/Scripts/Physics/Core/AeroConfig.cs` — new

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Core
{
    /// <summary>
    /// Aerodynamic constants loaded from aero.csv. Pure data struct —
    /// no Unity references. Loading happens in a Runtime-assembly loader.
    /// </summary>
    public struct AeroConfig
    {
        public fp AirDensity;       // kg/m³, default 1.225
        public fp BallMass;         // kg, default 0.04593
        public fp BallCrossSection; // m², default 0.001432
        public fp DragCoefficient;  // dimensionless, default 0.25
        public fp LiftCoefficientBase;  // dimensionless, default 0.20
        public fp SpinRateReference;    // rad/s, default 300
        public fp LiftMaxMultiplier;    // default 1.5

        public static AeroConfig Default => new AeroConfig
        {
            AirDensity = fp.FromFloat(1.225f),
            BallMass = fp.FromFloat(0.04593f),
            BallCrossSection = fp.FromFloat(0.001432f),
            DragCoefficient = fp.FromFloat(0.25f),
            LiftCoefficientBase = fp.FromFloat(0.20f),
            SpinRateReference = fp.FromFloat(300f),
            LiftMaxMultiplier = fp.FromFloat(1.5f),
        };
    }
}
```

#### `Assets/Scripts/Physics/Core/AeroModel.cs` — new

Pure static class, computes force contribution at a given velocity + spin + config.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Core
{
    public static class AeroModel
    {
        /// <summary>
        /// Returns the sum of drag + Magnus lift force at this instant, in Newtons.
        /// Gravity is handled separately by BallSimulation.
        /// </summary>
        public static fp3 ComputeAeroForce(fp3 velocity, SpinState spin, AeroConfig cfg)
        {
            fp speedSq = fpMath.Dot(velocity, velocity);
            if (speedSq <= fp.Epsilon) return fp3.Zero;

            fp speed = fpMath.Sqrt(speedSq);
            fp3 vHat = velocity / speed;

            // Drag: opposes velocity. Magnitude = ½ ρ A Cd |v|²
            fp dragScalar = fp.Half * cfg.AirDensity * cfg.BallCrossSection
                          * cfg.DragCoefficient * speedSq;
            fp3 drag = -vHat * dragScalar;

            if (!spin.IsSpinning) return drag;

            // Lift: ½ ρ A Cl |v|² (ŵ × v̂), Cl scaled by spin rate
            fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference,
                                        fp.Zero, cfg.LiftMaxMultiplier);
            fp clEff = cfg.LiftCoefficientBase * spinScale;
            fp liftScalar = fp.Half * cfg.AirDensity * cfg.BallCrossSection
                          * clEff * speedSq;
            fp3 liftDir = fpMath.Cross(spin.Axis, vHat);
            fp3 lift = liftDir * liftScalar;

            return drag + lift;
        }
    }
}
```

#### `Assets/Scripts/Physics/Core/BallSimulation.cs` — modify

The RK4 derivative function changes from `a = gravity` to `a = gravity + aero(v, spin, cfg) / mass`. Key implementation points:

1. `Simulate` now takes an `AeroConfig` parameter. Add an overload that uses `AeroConfig.Default` so existing Phase 1 tests still compile.
2. Inside the RK4 step, compute acceleration from the mid-step velocity at each of k1/k2/k3/k4. That means the aero term must be evaluated four times per step — this is correct and required for RK4 accuracy.
3. Watch the Q16.16 precision pattern: when combining `(k1a + 2*k2a + 2*k3a + k4a) * (Dt / fp.Six)`, reorder to `((k1a + fp.Two*k2a + fp.Two*k3a + k4a) * Dt) / fp.Six` to avoid the same truncation bug Phase 1 hit.
4. Landing detection logic is unchanged.

**Also add:** a `Termination` enum field on `Trajectory` with values `HitGround`, `TimedOut`, `BelowGround`. Phase 1 already logged a termination string — formalize it.

#### `Assets/Scripts/Physics/Core/ClubSpec.cs` — new

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics.Core
{
    /// <summary>One row of clubs.csv.</summary>
    public struct ClubSpec
    {
        public string Id;              // "Driver", "Iron7", "PitchingWedge", etc.
        public fp BallSpeedMps;        // at impact, typical PGA Tour tee-shot
        public fp LaunchAngleDeg;      // degrees above horizontal
        public fp SpinRateRpm;         // revolutions per minute (backspin positive)
        public fp ExpectedCarryYd;     // from Trackman / PHYSICS_TUNING_TARGETS §7
    }
}
```

---

### Part C — CSV-driven tuning

#### `Assets/Resources/Physics/aero.csv`

```csv
key,value,units,notes
air_density,1.225,kg/m^3,sea-level 15C
ball_mass,0.04593,kg,USGA max
ball_cross_section,0.001432,m^2,radius 0.02135m
drag_coefficient,0.25,dimensionless,constant starting point
lift_coefficient_base,0.20,dimensionless,scaled by spin in code
spin_rate_reference,300,rad/s,~2865 rpm driver baseline
lift_max_multiplier,1.5,dimensionless,cap on spin-scaled Cl
```

Simple key/value CSV — Claude Code decides the exact parser shape. It has to tolerate comments (any row starting with `#`) and the `units` + `notes` columns (ignored at load time).

#### `Assets/Resources/Physics/clubs.csv`

Cross-reference `PHYSICS_TUNING_TARGETS.md` Section 1 for the expected carry column. Starting set, covering the range from driver to wedge so tuning exercises the full curve:

```csv
id,ball_speed_mps,launch_angle_deg,spin_rate_rpm,expected_carry_yd,notes
Driver,75.0,10.9,2686,275,"PGA Tour avg"
Iron3,65.0,10.4,4404,212,
Iron5,57.0,14.1,5280,194,
Iron7,52.5,16.3,7097,172,"per TUNING §7"
Iron9,48.5,20.0,8647,152,
PitchingWedge,46.0,24.0,9300,136,
SandWedge,40.0,28.0,10000,110,
```

Ball-speed, launch, and spin values are from standard PGA Tour Trackman averages for that club. Carry targets are per `PHYSICS_TUNING_TARGETS.md` Section 1 — verify the numbers there, don't invent your own.

#### CSV loader — `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs`

Lives in Runtime asmdef (because it uses `Resources.Load`). Returns `AeroConfig` and `List<ClubSpec>`. Hot-reload hook:

```csharp
// In Editor only, subscribe to AssetDatabase.importPackageCompleted-equivalent
// or poll file-modified timestamp in the tuning window.
// For Phase 2, manual "Reload" button in the tuning window is sufficient.
```

Don't over-engineer hot-reload — a "Reload CSV" button in the tuning window covers the iteration loop fine for now.

---

### Part D — Tuning EditorWindow

`Assets/Scripts/Editor/Physics/PhysicsTuningWindow.cs` at menu path `Window > Physics > Tuning`.

Minimum viable UI:

1. **Aero section:** sliders for `DragCoefficient` (0.10 to 0.40), `LiftCoefficientBase` (0.10 to 0.35), `SpinRateReference` (100 to 500 rad/s). Edits live-apply to an in-memory `AeroConfig` (don't write back to CSV automatically — add a "Save to aero.csv" button).
2. **Clubs table:** readonly list of `ClubSpec` rows loaded from CSV with actual-carry column blank.
3. **"Run Validation" button:** fires each club's shot through `BallSimulation` with the in-memory `AeroConfig` and fills the actual-carry column. Rows within 5% of expected go green, 5–10% yellow, >10% red.
4. **"Reload CSVs" button** and **"Save aero.csv" button.**

Keep it simple — this is a tuning tool, not a product UI. Rough IMGUI layout is fine. Don't spend time on polish.

---

### Part E — Update test scene + controller

Rename `Phase1TestController` to `PhaseTestController` (preserve old name as an alias if easier). Add:

- `public bool UseAero = true;` toggle.
- `public string ClubId = "Iron7";` — dropdown backed by loaded `clubs.csv` if easy, plain string field if not.
- When `UseAero = true`, load aero from `aero.csv`, use club params from `clubs.csv`.
- When `UseAero = false`, behave exactly like Phase 1 did.
- Draw two `LineRenderer`s: yellow for "with aero", cyan for "vacuum". Toggle-able individually. This is the visual validation — the aero curve should fall obviously short of the vacuum one.

Create a proper trajectory material while you're here: `Assets/Materials/Physics/MAT_TrajectoryLine.mat`, URP Unlit, vertex color input enabled. Use it on both LineRenderers. Per-renderer color via `_BaseColor`. This replaces the magenta default Phase 1 shipped with.

Scene: `Assets/Scenes/Physics/Phase2_AeroTest.unity`. Keep Phase 1's scene untouched.

---

### Part F — Tests

`Assets/Scripts/Physics/Tests/AerodynamicsTests.cs`. Four tests at minimum:

1. **`Aero_Off_MatchesPhase1_Within_Epsilon`** — with `Cd=0, Cl=0`, verify the integrator reproduces Phase 1's vacuum result within 0.1m over a 100 m carry. Confirms we didn't break the gravity-only path.

2. **`Aero_DragReducesCarry_MonotonicallyWithCd`** — sweep Cd from 0 to 0.5 in steps of 0.05, confirm carry distance decreases monotonically at fixed launch.

3. **`Aero_Backspin_ExtendsCarry_VsZeroSpin`** — same launch speed/angle, two shots: one with 5000 rpm backspin, one with zero spin. Backspin shot should carry at least 10% farther.

4. **`Aero_ClubCarries_WithinTolerance_OfTrackmanTargets`** — iterate every row in `clubs.csv`, simulate the shot, assert actual carry is within **10%** of expected. (We'll tighten to 5% after tuning; 10% is the "it's not catastrophically broken" gate.)

Each test uses `AeroConfig.Default` unless it's specifically sweeping.

---

### Part G — Unity-MCP autonomous validation

Drive this yourself:

1. **Compile.** `console-get-logs` → zero errors after all files written. Max 5 fix iterations.
2. **Tests.** `tests-run` filter `Golfin.Physics.Tests`. All 4 new tests + all 4 Phase 1 tests must pass. Total: 8 green.
3. **Tuning window smoke.** Open `Window > Physics > Tuning`, click "Run Validation", grab the results. Attach to done report.
4. **Scene screenshot.** Open `Phase2_AeroTest.unity`, Play Mode ~2s, `screenshot-game-view`. Should show two lines: vacuum (cyan, reaching further) and aero (yellow, falling short). If they overlap exactly, something is wrong with aero application.
5. **Console log line check.** `[PhaseTest] club=Iron7 carry=...m (expected 157m ±10%)` — confirm in console after scene play.

### Iteration budget

5 autonomous iterations before reporting failure with diagnostics. Likely failure modes:

- **Club carries way off (>30% error) across the board.** → Likely unit bug (rpm vs rad/s on spin input, or m/s vs mph on ball speed). Audit conversions before adjusting coefficients.
- **Driver is close but wedges are far off, or vice versa.** → Constant coefficients don't span the velocity range. **Don't jump to LUT yet** — report the numbers and I'll decide if LUT is justified or if a single Cd/Cl tweak dials it in.
- **Q16.16 overflow in force calculation.** Drag scales with `v²`; at 75 m/s drag scalar hits ~5 N. In Q16.16 raw units that's `5 * 65536 = 327680` — fine in int32. If you see overflow, suspect an intermediate product, not the final value. Reorder the multiplication.

### Done report should include

- Test pass/fail count (expect 8/8).
- Tuning window validation table: each club's expected vs actual carry, % error.
- Screenshot showing both trajectories.
- Debug log line from scene play.
- Any NOTE comments left in code for ambiguous decisions.
- If any club is > 10% off after a reasonable tuning pass, stop and report — **do NOT silently adjust constants to make tests pass.** The tuning values are the signal we need.

### DO NOT

- Touch Phase 0 (heightmap baker) or the baked heightmap files.
- Add `Unity.Mathematics.FixedPoint` or any other fixed-point library. Hand-rolled Q16.16 stays the only math lib.
- Introduce `UnityEngine` imports into `Golfin.Physics.Core` or `Golfin.Physics.Math` assemblies. The `noEngineReferences` wall stays.
- Implement velocity-indexed Cd/Cl lookup tables in Phase 2. Constant coefficients only; escalate to LUT as a separate task if needed.
- Add wind support. That's Phase 3.
- Add surface interaction / bounce / roll. That's Phase 4.
- Quietly tune `expected_carry_yd` in clubs.csv to hide poor results. Those numbers are from `PHYSICS_TUNING_TARGETS.md` — if carries don't match, the sim is wrong, not the target.
- Delete `Phase1_VacuumTest.unity` or `Phase1TestController`. They stay as the vacuum baseline.

---

## History Log (completed tasks, most recent first)

- ✅ **2026-04-21** Phase 2 Aerodynamics — drag + Magnus lift in RK4 integrator. `fp.Half`/`fp.Epsilon`/`fpMath.Dot|Cross|Normalize|Clamp` added. `SpinState`, `AeroConfig` (Default + Vacuum), `AeroModel`, `ClubSpec` new Core structs. `ShotInput` extended with `SpinState Spin`. `BallSimulation` evaluates aero force at all 4 RK4 sub-steps; `AeroConfig.Vacuum` overload preserves Phase 1 test compatibility. `PhysicsConfigLoader` (Runtime CSV loader), `PhaseTestController` (dual yellow/cyan linerenderers), `PhysicsTuningWindow` (EditorWindow `Window > Physics > Tuning`). `AerodynamicsTests.cs` (4 tests). `Phase2_AeroTest.unity` scene. **7/8 tests pass.** Test 4 (club carry validation) fails because constant Cd/Cl coefficients cannot span Driver (8% over, OK) vs Iron3–SandWedge (22–43% over). Root cause: fixed Cl_base + capped spinScale gives irons 2× more Cl_eff than real physics; SpinRateRef=300 puts all irons at ClMaxMult cap. **Architect decision needed: velocity/spin-parameter LUT vs. adjusted constants.** Per spec: "Don't silently adjust constants — report numbers." Debug log: `[PhaseTest] club=Iron7 | aero carry=196.2m (215yd) | vacuum carry=151.4m (166yd) expected=172yd (157.3m)`.

- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types (`ShotInput`, `Trajectory`, `BallSimulation`) with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 integrator at dt=1/240s. 4 tests passing (parametric sweep, 1000-random, zero-velocity drop, determinism). 1000 random shots: 0 failures, worst error 0.164%. Phase1TestController MonoBehaviour + Phase1_VacuumTest scene with LineRenderer. 50 m/s @ 25° → 195.3m (expected 195.27m). **Gotcha recorded:** `Dt/6` in Q16.16 truncates; must reorder as `(sum * Dt) / 6` to preserve precision in the multiply before dividing. Applies to all future RK4 coefficient combinations.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — `PhysicsHeightmapBaker.cs` created. Menu items: Bake Current Hole / Bake Hole 01-18 / Bake All Holes. Q16.16 fixed-point, binary `heightmap.bytes` with `GHM1` header. Hole 1 baked: 16.02 MB, 0/100 round-trip mismatches. All 18 holes baked subsequently. File at `Tools/UHoleGeo/output/lomond-country-club/export/hole-NN/heightmap.bytes`.
- ✅ **2026-04-20** Phase 2b water shore ablation — set `ShoreRadius=0`, confirmed serrations remain, eliminated ramp as cause (Hypothesis A), confirmed depression-cliff cause (Hypothesis B). `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp in `DepressTerrainUnderOverlays` (reverse chamfer from boundary inward, smoothstep surfaceNorm→waterFloorY over `ShoreRadius` cells). Fixed serrations on Hole 12 steep bank. Water mesh kept in original position; depression handles the boundary continuity.
- ✅ **2026-04-20** Hole Flyover Recorder — new `Assets/Scripts/Editor/Recording/HoleFlyoverRecorder.cs`. Three menu items under `Golfin/Recording/`. Play Mode state machine, `FlyoverCamera` with tag, 4-phase path (drone hover → zoom in → Catmull-Rom cruise → pin orbit), Unity Recorder 5.1.6 API, batch mode across 18 holes, SessionState persistence across domain reloads.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix — `minSpinePixels=20` filter was removing chain[4] (len=15), causing junction C to degrade to 2-way and B-C link to merge. Fix: rescue short chains (len≥`dsFactor*2=6`) whose endpoint touches a 2-way junction in longChains. Hole 1 now exports 10 cart paths (was 6).
- ✅ **2026-04-20** Cart path junction endpoint snapping (Unity) — `SnapCartPathJunctionEndpoints()` in `CreateSplineCartPaths`. 0.75m radius clusters endpoints at N-way junctions, snaps to centroid. Fixes grass wedges on Hole 1 middle junction.
- ✅ **2026-04-20** Linear-slope tee skirt — replaced fixed-radius smoothstep ramp with linear descent at `TeeMaxRampSlope=0.35 m/m`. Writes while `rampH_m > base_m`; terminates where ramp meets terrain. No fixed radius, no outer cliff, C¹-continuous. `TeeSkirtMeters` now unused.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt — stair-stepped every slope. Commit 6151e8d7 reverted at b7f70112. Approach abandoned in favor of linear-slope.
- ✅ **2026-04-20** Per-layer terrain tint pass inserted in `ApplySplatmap()` (both Geo and Lite importers). ⚠️ **REVERTED same day** — `diffuseRemapMax` on TerrainLayer had no visible effect. Root cause unknown; knob/render-path may differ. Code reverted to original. Revisit when someone has time to dig into TerrainLayer internals.
- ✅ **2026-04-19** Water Shore Phase 1 sampling — new `Tools/sample-shore-heights.js`. Course-wide max drop 14.07m (Hole 12 body 1), max `dR_needed` 34.7m. Recommended `ShoreMaxRadiusMeters` = 40m. Per-hole terrain dims from `terrain-meta.json`.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo — `dev-server.mjs` `/api/bridges` GET route + bridges loaded into hole nav data. `app.js`: `loadBridges()`, `worldToNormalized()`, purple rotated footprint + forward tick + anchor circles, `hitTestBridge()` + hover tooltip, "Bridges" layer toggle, bridge count chip in hole nav.
- ✅ **2026-04-18** Bridge Placement Tool (Unity) — `BridgeAnchor` (`Golfin.Course`) marker component with gizmo. `BridgeExporter` EditorWindow at `Window > Trees > Bridge Exporter`. Auto-detects Geo/Lite/Flat from scene name, writes `bridges.json` to UHoleGeo/UHoleLite export folder, mirrors to sibling pipeline.
- ✅ **2026-04-18** Tee border ring UV fix + geometric rebuild — constant V (0.5) eliminated texture twisting on the curved ring. Additionally rebuilt ring as manual quad-strip (outer contour × inset contour by vertex index) instead of CDT-classified triangles, eliminating long diagonal spanning tris. Submesh 0 = CDT surface, submesh 1 = clean N-quad strip.

---

## Reference Docs for Claude Code

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan, Unity-MCP workflow notes (Section 6.5)
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers (carry distances, stat mappings, surface coefficients)
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe for fringe/border baked into parent mesh
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP (50+ tools reference: https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/default-mcp-tools.md)
