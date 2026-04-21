# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## ACTIVE TASK — Phase 3: Wind (steady + gusts + profile)

### Context

Phase 2.1 closed with aero LUTs in the Bearman–Harvey envelope and honest per-club tolerances. Trajectories in still air are now physically grounded. Phase 3 adds wind so carry depends on conditions, not just launch parameters — headwinds shorten, tailwinds extend, crosswinds push the ball sideways, and gusts introduce shot-to-shot variance that makes the wind gauge in the UI actually matter.

The core integration change is small: replace `velocity` with `velocity - wind_velocity` inside `AeroModel.ComputeAeroForce`. Everything else in drag/lift already works with a relative-velocity vector. The complexity is in the wind model itself (steady + gust + optional altitude profile) and in making it deterministic (seeded PRNG) so the fixed-point integrator stays reproducible.

Out of scope: turbulence fields, ridge lift, thermals, wind shear across a hole, weather changes during a round. Wind is a per-shot condition, sampled at the ball's current position and time, returning a single vector. That's it.

Reference: `Docs/PHYSICS_RESEARCH.md` Section 4 (wind model), `Docs/LESSONS_PHYSICS_AERO.md` (aero invariants to respect when touching `AeroModel`).

### Phase 2.1 invariants to respect

- Hand-rolled Q16.16 math lib stays. No `Unity.Mathematics.FixedPoint` or float sneaking in.
- `Golfin.Physics.Core` stays `noEngineReferences: true`. Wind config struct + wind sampler live in Core. CSV loading stays in Runtime.
- RK4 precision pattern: multiply before divide. `(sum * Dt) / Two`, never `sum * (Dt / Two)`.
- Aero is evaluated at each RK4 sub-step. Wind must be sampled at each sub-step too — using the position and time of that sub-step, not the start-of-step values — or the drag direction will drift across sub-steps when wind varies.
- LUT CSVs and per-club tolerances from Phase 2.1 are locked. No changes to aero_drag_lut.csv, aero_lift_lut.csv, or clubs.csv.

---

### Part A — Data types (Core)

#### `Assets/Scripts/Physics/Core/WindConfig.cs` — new

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Per-shot wind conditions. Steady base vector + optional gust envelope
    /// + optional altitude-based speed multiplier. Deterministic given seed.
    /// Pure data; loaded by PhysicsConfigLoader from Resources/Physics/wind.csv
    /// or synthesized per-shot from design-side values.
    /// </summary>
    public struct WindConfig
    {
        /// <summary>Base wind vector in world-space m/s. +X east, +Z north is the convention used elsewhere in the sim.</summary>
        public fp3 BaseVelocity;

        /// <summary>Gust amplitude as fraction of |BaseVelocity|. 0 = no gusts. 0.2 = ±20% variation.</summary>
        public fp GustAmplitude;

        /// <summary>Gust frequency in Hz — roughly how often the gust cycle oscillates. 0.3–0.8 Hz is typical.</summary>
        public fp GustFrequency;

        /// <summary>Altitude speed multiplier: wind at height Y scales as (1 + AltitudeFactor · Y / AltitudeRefMeters). 0 = no profile.</summary>
        public fp AltitudeFactor;

        /// <summary>Reference altitude in meters (typically 10m). Unused if AltitudeFactor is 0.</summary>
        public fp AltitudeRefMeters;

        /// <summary>PRNG seed for gusts. Same seed → same gust sequence → reproducible trajectories.</summary>
        public uint Seed;

        public static WindConfig Calm => new WindConfig
        {
            BaseVelocity      = fp3.Zero,
            GustAmplitude     = fp.Zero,
            GustFrequency     = fp.Zero,
            AltitudeFactor    = fp.Zero,
            AltitudeRefMeters = fp.FromInt(10),
            Seed              = 0,
        };

        public bool IsActive =>
            fpMath.Dot(BaseVelocity, BaseVelocity) > fp.Epsilon || GustAmplitude > fp.Epsilon;
    }
}
```

#### `Assets/Scripts/Physics/Core/WindModel.cs` — new

Pure-math wind sampler. No engine references, no CSV parsing.

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Samples wind velocity at a given (position, time) from a WindConfig.
    /// Deterministic: same config + same (pos, t) → same wind vector.
    ///
    /// Model: steady base vector, plus a gust envelope that sinusoidally
    /// modulates magnitude over time with seed-derived phase, plus an
    /// optional linear altitude profile.
    /// </summary>
    public static class WindModel
    {
        public static fp3 SampleWind(fp3 position, fp time, WindConfig cfg)
        {
            if (!cfg.IsActive) return fp3.Zero;

            fp3 wind = cfg.BaseVelocity;

            // Gust envelope: multiply magnitude by (1 + A · sin(2π·f·t + φ)).
            // φ is derived from seed so different seeds give different gust timing.
            if (cfg.GustAmplitude > fp.Epsilon && cfg.GustFrequency > fp.Epsilon)
            {
                fp phase = SeedToPhase(cfg.Seed);
                fp angle = fpMath.TwoPi * cfg.GustFrequency * time + phase;
                fp gust  = fp.One + cfg.GustAmplitude * fpMath.Sin(angle);
                wind = wind * gust;
            }

            // Altitude profile: wind scales linearly with Y.
            // At Y=0, multiplier is 1. At Y=AltitudeRefMeters, multiplier is 1 + AltitudeFactor.
            if (cfg.AltitudeFactor > fp.Epsilon && cfg.AltitudeRefMeters > fp.Epsilon)
            {
                fp altScale = fp.One + cfg.AltitudeFactor * (position.y / cfg.AltitudeRefMeters);
                // Clamp to prevent negative wind below ground level or absurdly high aloft.
                altScale = fpMath.Clamp(altScale, fp.Half, fp.FromInt(3));
                wind = wind * altScale;
            }

            return wind;
        }

        /// <summary>Deterministic uint-to-phase hash. Result is in [0, 2π).</summary>
        private static fp SeedToPhase(uint seed)
        {
            // Simple splitmix-style hash, then scale into [0, 2π).
            // Pure integer → fp; no float involved.
            ulong x = seed;
            x = (x ^ (x >> 16)) * 0x7FEB352Dul;
            x = (x ^ (x >> 15)) * 0x846CA68Bul;
            x = x ^ (x >> 16);
            // Fractional part: bottom 16 bits / 65536, mapped to [0, 2π).
            fp frac = fp.FromRaw((int)(x & 0xFFFFu));  // Q16.16: raw 0..65535 = 0..1.0
            return frac * fpMath.TwoPi;
        }
    }
}
```

**fpMath additions needed:** `fpMath.TwoPi`, `fpMath.Sin`. If `Sin` isn't already in the math lib, add it using a Taylor series or CORDIC. Small-angle check: for our gust frequencies (0.3–0.8 Hz) and durations (up to 10 s), the angle stays bounded; a 6-term Taylor series after range-reduction to [-π, π] is sufficient. Keep it in `Assets/Scripts/Physics/Math/fpMath.cs`; no UnityEngine.

If `fp.FromRaw(int)` doesn't exist but an equivalent does (e.g. `fp.FromRawBits`), use whatever the existing convention is. The point is: derive a deterministic sub-1.0 fraction from the seed without going through float.

---

### Part B — Thread wind through AeroModel + BallSimulation

#### `Assets/Scripts/Physics/Core/AeroModel.cs` — add relative-velocity overload

Keep the existing `ComputeAeroForce(velocity, spin, cfg)` as a wind-free wrapper. Add a new overload that takes wind:

```csharp
/// <summary>
/// Aero force under wind. Drag and lift are computed against velocity_relative =
/// ball_velocity - wind_velocity. Ball velocity is returned in Newtons as before.
/// </summary>
public static fp3 ComputeAeroForce(fp3 velocity, fp3 windVelocity, SpinState spin, AeroConfig cfg)
{
    fp3 vRel = velocity - windVelocity;
    fp speedSq = fpMath.Dot(vRel, vRel);
    if (speedSq <= fp.Epsilon) return fp3.Zero;

    fp speed = fpMath.Sqrt(speedSq);
    fp3 vRelHat = vRel / speed;

    // Drag opposes relative velocity direction.
    fp cd = (cfg.UseDragLut && cfg.DragLut.IsValid)
        ? cfg.DragLut.Evaluate(speed)
        : cfg.DragCoefficient;
    fp dragScalar = (cfg.AirDensity * cfg.BallCrossSection * cd * speedSq) * fp.Half;
    fp3 drag = vRelHat * (-dragScalar);

    if (!spin.IsSpinning) return drag;

    // Lift direction is (spin × relative_velocity_direction), perpendicular to airflow.
    fp cl;
    if (cfg.UseLiftLut && cfg.LiftLut.IsValid)
    {
        fp spinParam = (cfg.BallRadius * spin.Rate) / speed;
        cl = cfg.LiftLut.Evaluate(spinParam);
    }
    else
    {
        fp spinScale = fpMath.Clamp(spin.Rate / cfg.SpinRateReference, fp.Zero, cfg.LiftMaxMultiplier);
        cl = cfg.LiftCoefficientBase * spinScale;
    }
    if (cl <= fp.Epsilon) return drag;

    fp liftScalar = (cfg.AirDensity * cfg.BallCrossSection * cl * speedSq) * fp.Half;
    fp3 liftDir = fpMath.Cross(spin.Axis, vRelHat);
    return drag + liftDir * liftScalar;
}

// Back-compat: wind-free call forwards to the new overload with zero wind.
public static fp3 ComputeAeroForce(fp3 velocity, SpinState spin, AeroConfig cfg)
    => ComputeAeroForce(velocity, fp3.Zero, spin, cfg);
```

**Note:** `cl <= fp.Epsilon` check and the fall-through to `return drag` stay. The spin parameter S = r·ω / |v_rel| uses relative speed, which is correct — a ball moving through still-air wind sees a different effective airflow speed, and that speed is what the dimple flow regime responds to.

#### `Assets/Scripts/Physics/Core/BallSimulation.cs` — add wind-aware overload

Add a third overload signature:

```csharp
public static Trajectory Simulate(ShotInput input, IGroundProvider ground, AeroConfig aero, WindConfig wind)
```

Inside the RK4 loop, sample wind at each sub-step using the sub-step's position estimate and time:

```csharp
// At each sub-step, sample wind at (sub-step position, sub-step time) and pass it to Accel.
fp3 wind1 = WindModel.SampleWind(pos, t, wind);
fp3 k1v = Accel(vel, wind1, spin, aero);
fp3 k1p = vel;

fp3 pos2 = pos + k1p * Dt / Two;
fp3 vel2 = vel + k1v * Dt / Two;
fp3 wind2 = WindModel.SampleWind(pos2, t + Dt / Two, wind);
fp3 k2v = Accel(vel2, wind2, spin, aero);
fp3 k2p = vel2;

// ... same for k3, k4 ...
```

And `Accel` gets a wind overload:

```csharp
private static fp3 Accel(fp3 vel, fp3 wind, SpinState spin, AeroConfig cfg)
{
    fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);
    fp3 aeroForce = AeroModel.ComputeAeroForce(vel, wind, spin, cfg);
    if (cfg.BallMass <= fp.Epsilon) return gravity;
    fp3 aeroAccel = aeroForce / cfg.BallMass;
    return gravity + aeroAccel;
}
```

The existing `Simulate(input, ground, aero)` overload should become a one-liner that forwards to the wind-aware version with `WindConfig.Calm`. The existing `Simulate(input, ground)` wraps further with `AeroConfig.Vacuum`. Net effect: three overloads, each forwarding to the most general one, so Phase 1 and Phase 2 test paths are untouched.

**Precision concern:** reordering `Dt / Two` to `(k1p * Dt) / Two` for position samples too. Same multiply-before-divide discipline as velocity.

---

### Part C — Config loading (Runtime)

#### `Assets/Resources/Physics/wind.csv` — new

```csv
key,value,units,notes
base_x,0.0,m/s,east-positive steady wind component
base_y,0.0,m/s,vertical wind (updraft/downdraft); usually 0
base_z,0.0,m/s,north-positive steady wind component
gust_amplitude,0.0,dimensionless,0=calm 0.2=moderate gusts
gust_frequency,0.5,Hz,gust oscillation rate
altitude_factor,0.0,dimensionless,0=no altitude profile
altitude_ref_meters,10.0,m,altitude reference
seed,0,uint,PRNG seed for gust phase; 0=deterministic calm
```

Default values = calm. Design-side tools will overwrite these per-shot or per-hole later.

#### `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — extend

Add `LoadWindConfig()` that reads `Resources/Physics/wind.csv` and returns a `WindConfig`. Follow the same pattern as `LoadAeroConfig`:

- Tolerant of missing file → return `WindConfig.Calm` with a log warning.
- Tolerant of missing keys → use `Calm` defaults for absent keys, log which ones.
- Seed is parsed as uint; other fields as fp.

No changes to `AeroConfig` loading. Wind is its own concern.

---

### Part D — Tuning window extension

`Assets/Scripts/Editor/Physics/PhysicsTuningWindow.cs`:

1. Add a "Wind" foldout section below the existing Aero section.
2. Inside: three fp fields for BaseVelocity XYZ, a float slider for GustAmplitude (0 to 0.5), a slider for GustFrequency (0.1 to 2 Hz), an AltitudeFactor field, a Seed uint field, a "Reload wind.csv" button.
3. A small preview: compute wind at the current time and at position (0, 0, 0) and display the resulting vector. Helps designers verify they're getting the expected magnitude.
4. The existing "Run Validation" button should use `WindConfig.Calm` — wind is not part of the club-carry validation tests. Add a second button "Run Wind Test" that runs the wind-specific tests.

Keep it functional, not pretty.

---

### Part E — Tests

`Assets/Scripts/Physics/Tests/WindTests.cs` — new file. Golfin.Physics.Tests namespace.

1. **`Wind_Calm_MatchesPhase2Aero_ExactlyEqual`** — simulate a 7-iron twice: once via the wind-free `Simulate(input, ground, aero)` overload, once via the wind-aware overload with `WindConfig.Calm`. Final positions must match bit-exactly (same raw Q16.16 values). Regression gate proving wind addition didn't perturb the wind-free path.

2. **`Wind_Headwind_ReducesCarry_MonotonicallyWithSpeed`** — 7-iron, zero wind → carry A. 5 m/s headwind (BaseVelocity = (0, 0, -5), ball flies +Z) → carry B. 10 m/s headwind → carry C. Assert A > B > C, and each gap is at least 3 yards (headwind effect is real, not noise).

3. **`Wind_Tailwind_ExtendsCarry`** — same setup but +Z wind. Tailwind carry > calm carry. Require at least +3 yd.

4. **`Wind_Crosswind_ProducesLateralDrift`** — 7-iron in +Z direction, wind = (5, 0, 0). Assert `finalPosition.x` is > 2m (ball drifted east). Assert carry (|finalPosition.z|) is within 3% of calm carry (crosswind shouldn't change downrange distance much).

5. **`Wind_Gust_SeedDeterminism`** — same gust config, two runs with same seed → identical trajectories. Same gust config, different seeds → different trajectories (landing positions differ by at least 0.5m). Covers determinism regression.

6. **`Wind_Altitude_ProfileAffectsApex`** — 7-iron with AltitudeFactor = 0.5, headwind. Ball at apex (~25m) experiences stronger headwind than ball near ground. Carry should be shorter than flat-profile headwind of same surface speed. Assert at least 1 yard difference.

Tolerances are directional + magnitude-sanity, not Trackman-precise. Wind tests check that the *shape* of the effect is correct, not specific numbers. Specific numbers come from playtesting, not physics.

All existing tests must still pass. Total test count after Phase 3: 15 existing + 6 new = 21.

---

### Part F — Validation

Drive yourself with Unity-MCP:

1. Compile clean. `console-get-logs` after changes, max 5 iterations to resolve errors.
2. Run full suite: `tests-run` filter `Golfin.Physics.Tests`. All 21 pass.
3. Open `Window > Physics > Tuning`, verify wind foldout appears, click "Reload wind.csv", verify preview updates.
4. Screenshot the scene with a 10 m/s crosswind applied, Play Mode ~2s. Trajectory should visibly curve compared to calm.

### Done report

- All 21 tests pass/fail summary.
- `WindConfig` defaults loaded from `wind.csv` confirmed.
- Headwind/tailwind/crosswind magnitude table: for 7-iron at 0/5/10 m/s in each direction, report the actual carry and the actual lateral offset.
- Screenshot of crosswind trajectory.
- Any anomalies. In particular, if `Wind_Calm_MatchesPhase2Aero_ExactlyEqual` fails with non-zero bit difference, that's a blocking issue — wind threading introduced numerical drift into the wind-free path. Report rather than tuning to hide.

### DO NOT

- Modify aero LUTs, clubs.csv, or Phase 2.1 test tolerances. They're locked.
- Introduce UnityEngine imports to Core. Wind config + wind model are pure math.
- Add turbulence fields, ridge lift, thermals, or 3D wind volumes. Scalar-profile wind only.
- Use `System.Random` or `UnityEngine.Random` anywhere. Seed determinism is based on the uint seed + splitmix-style hash. The integrator must be reproducible.
- Sample wind once per outer RK4 step and reuse. Sample at each sub-step with that sub-step's (position, time) — otherwise drag direction is wrong mid-step when wind varies fast.
- Tune wind.csv values. Defaults are calm; test-time configs are built in code.

### Iteration budget

3 iterations on the code (compile + tests). If `Wind_Calm_MatchesPhase2Aero_ExactlyEqual` doesn't pass bit-exactly, stop and report — that indicates a threading issue in the wind-free path that needs architectural fix, not tuning.

---

✅ DONE: 2026-04-21 Phase 3 Wind complete — 21/21 tests pass, WindConfig + WindModel + wind.csv + PhysicsConfigLoader.LoadWindConfig() + AeroModel wind overload + BallSimulation wind-aware Simulate overload + PhysicsTuningWindow Wind foldout. Wind_Calm_MatchesPhase2Aero_ExactlyEqual passes bit-exactly. See done report in AI_CONTEXT.md.

## History Log (completed tasks, most recent first)

- ✅ **2026-04-21** Phase 2.1 closeout — LUT-mode tests split by club class with honest per-club tolerances. Driver/Iron3 at 25%, mid-irons at 15%, wedges at 8%. 15 tests pass. Lessons filed at LESSONS_PHYSICS_AERO.md. Physics baseline accepted.
- ❌ **2026-04-21 REMEDIATION v3 — ARCHITECTURE ESCALATION HIT (Rung 3)** — Bearman–Harvey Cl at driver S=0.08 physically cannot produce 275 yd carry; lift barely balances gravity at launch. 1D-BH model ceiling. Not escalating to 2D LUT. Lessons filed: `Docs/LESSONS_PHYSICS_AERO.md`.
- ⚠️ **2026-04-21 REMEDIATION v2** Seed-value error, not architecture — Cl too high at low S. Driver 23.5% short residual matched ratio of seed overshoot.
- ⚠️ **2026-04-21 REMEDIATION v1** Correctly reverted `spin_drag_factor` scope creep; incorrectly reverted `spin_decay_rate` (real physics, restored in v3).
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles); v0 tuning produced unphysical shapes. Series of remediations followed.
- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow`. `BallSimulation` calls `AeroModel` at each RK4 sub-step.
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 at dt=1/240s. **Gotcha:** `Dt/6` in Q16.16 truncates; reorder as `(sum * Dt) / 6`.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — Q16.16 fixed-point binary `heightmap.bytes`. All 18 holes baked.
- ✅ **2026-04-20** Phase 2b water shore ablation — confirmed depression-cliff cause. `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp.
- ✅ **2026-04-20** Hole Flyover Recorder — `HoleFlyoverRecorder.cs`.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix.
- ✅ **2026-04-20** Cart path junction endpoint snapping.
- ✅ **2026-04-20** Linear-slope tee skirt.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt.
- ⚠️ **2026-04-20 REVERTED** Per-layer terrain tint pass.
- ✅ **2026-04-19** Water Shore Phase 1 sampling.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo.
- ✅ **2026-04-18** Bridge Placement Tool (Unity).
- ✅ **2026-04-18** Tee border ring UV fix.

---

## Reference Docs

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/LESSONS_PHYSICS_AERO.md` — aero remediation lessons + future tightening options (read before touching aero LUTs)
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
