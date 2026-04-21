# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
>
> **Workflow update (2026-04-21):** Claude Code now drives Unity directly via Unity-MCP (https://github.com/IvanMurzak/Unity-MCP). Tools available: `script-update-or-create`, `script-execute`, `tests-run`, `console-get-logs`, `scene-create`/`open`/`save`, `gameobject-create`/`component-add`/`modify`, `editor-application-set-state`, `screenshot-game-view`/`scene-view`, `package-add`, and more. Specs below include autonomous validation criteria — run them to confirmation rather than reporting "done" and waiting for Cesar to verify.

---

## ACTIVE TASK — Phase 1: Vacuum Trajectory + Fixed-Point Math + Driving Range Scene

### Context

Phase 0 baker is done — all 18 heightmaps written. Phase 1 builds the physics substrate: a deterministic ball-trajectory integrator in pure C#, with fixed-point math, targeting the `range = v²·sin(2θ)/g` projectile equation in vacuum. No aerodynamics, no wind, no surface interaction yet — those are Phases 2–4.

See `Docs/PHYSICS_RESEARCH.md` Sections 2–3, 6 (Phase 1), 6.5 (Unity-MCP workflow), 7 (architecture), and `Docs/PHYSICS_TUNING_TARGETS.md` for downstream context. Specifically: we're building toward multiplayer-ready determinism, so **no floats, no Unity APIs, no platform math libraries inside the sim core**. Everything compiles and runs as pure .NET.

### Goal

A deterministic `Golfin.Physics.BallSimulation.Simulate(ShotInput, IGroundProvider) → Trajectory` pure-C# method that integrates ballistic motion under gravity using RK4 at `dt = 1/240s`, in Q16.16 fixed-point math. Validated against the closed-form projectile equation within 1% relative error over 1000 random inputs. Plus a minimal driving-range scene to eyeball a trajectory in Unity.

---

### Part A — Fixed-point math package

Install via Unity-MCP `package-add`:

**Package:** `com.lostpolygon.mathematics.fixedpoint`
**Git URL:** `https://github.com/asik/FixedMath.Net.git`

**If that specific package isn't available or fails,** fall back to rolling our own minimal fixed-point type as described below. The "correct" package choice matters less than the interface; Phase 2+ coefficient tuning is where the math library earns its keep.

**Preference order for the math library:**

1. `Unity.Mathematics.FixedPoint` (danielmansson's package, if available via git URL: `https://github.com/danielmansson/Unity.Mathematics.FixedPoint.git`)
2. `FixedMath.Net` (asik's, above)
3. Hand-rolled `Golfin.Physics.Math.fp` struct (see Part A.alt)

Document which one was used in a `// PHYSICS_MATH_LIB:` comment at the top of `FixedPointMath.cs` so future me can find it.

#### Part A.alt — Hand-rolled fallback

If no suitable package is found, create `Assets/Scripts/Physics/Math/fp.cs`:

```csharp
namespace Golfin.Physics.Math
{
    // Q16.16 fixed-point wrapped in long for intermediate multiply headroom.
    // Stored as long internally to avoid 32-bit multiply overflow during
    // (a.raw * b.raw) >> 16. Exposed range: ±32768.0, precision ~15μm.
    public readonly struct fp
    {
        private const int FracBits = 16;
        private const long FracScale = 1L << FracBits;
        public readonly long raw;

        private fp(long raw) { this.raw = raw; }

        public static fp FromRaw(long r) => new fp(r);
        public static fp FromInt(int i) => new fp((long)i << FracBits);
        public static fp FromFloat(float f) => new fp((long)System.Math.Round(f * FracScale));
        public static fp FromDouble(double d) => new fp((long)System.Math.Round(d * FracScale));
        public float ToFloat() => (float)raw / FracScale;
        public double ToDouble() => (double)raw / FracScale;

        public static readonly fp Zero = new fp(0);
        public static readonly fp One = new fp(FracScale);

        public static fp operator +(fp a, fp b) => new fp(a.raw + b.raw);
        public static fp operator -(fp a, fp b) => new fp(a.raw - b.raw);
        public static fp operator -(fp a) => new fp(-a.raw);
        public static fp operator *(fp a, fp b) => new fp((a.raw * b.raw) >> FracBits);
        public static fp operator /(fp a, fp b) => new fp((a.raw << FracBits) / b.raw);

        public static bool operator <(fp a, fp b) => a.raw < b.raw;
        public static bool operator >(fp a, fp b) => a.raw > b.raw;
        public static bool operator <=(fp a, fp b) => a.raw <= b.raw;
        public static bool operator >=(fp a, fp b) => a.raw >= b.raw;
        public static bool operator ==(fp a, fp b) => a.raw == b.raw;
        public static bool operator !=(fp a, fp b) => a.raw != b.raw;

        public override bool Equals(object o) => o is fp f && f.raw == raw;
        public override int GetHashCode() => raw.GetHashCode();
        public override string ToString() => ToFloat().ToString("F4");
    }

    public readonly struct fp3
    {
        public readonly fp x, y, z;
        public fp3(fp x, fp y, fp z) { this.x = x; this.y = y; this.z = z; }
        public static fp3 Zero => new fp3(fp.Zero, fp.Zero, fp.Zero);
        public static fp3 operator +(fp3 a, fp3 b) => new fp3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static fp3 operator -(fp3 a, fp3 b) => new fp3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static fp3 operator *(fp3 a, fp s) => new fp3(a.x * s, a.y * s, a.z * s);
        public static fp3 operator /(fp3 a, fp s) => new fp3(a.x / s, a.y / s, a.z / s);
        public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(x.ToFloat(), y.ToFloat(), z.ToFloat());
    }
}
```

**NOTE:** `fp3.ToVector3()` is the ONLY Unity-API reference in the math module. It's a convenience for callers bridging to rendering — it does NOT violate the "no Unity APIs in sim core" rule because the conversion lives in the math package, not in `BallSimulation`. The sim itself never calls it.

Also needed: sin/cos/sqrt helpers. For Phase 1 (vacuum, no angle-rotation) we only need `Sqrt` for the validation harness, and `Sin`/`Cos` to build initial velocity from launch angle. These live in a sibling `Assets/Scripts/Physics/Math/fpMath.cs`:

```csharp
namespace Golfin.Physics.Math
{
    public static class fpMath
    {
        // Babylonian/Newton integer sqrt on the raw long. Deterministic.
        // Used sparingly; OK to be slower than platform sqrt.
        public static fp Sqrt(fp x)
        {
            if (x.raw <= 0) return fp.Zero;
            // Work in Q16.16: result.raw² / 2^16 ≈ x.raw
            // → result.raw ≈ sqrt(x.raw * 2^16) = sqrt(x.raw) * 256
            long v = x.raw;
            long n = v << 16;  // x.raw * 2^16, might overflow for x > 32767
            // Guard: if n overflowed (v >> 48 != 0 before shift), use double fallback
            // acceptable for phase 1 — sqrt only used in validation + init, not the hot loop
            if ((v >> 48) != 0)
            {
                double d = System.Math.Sqrt(x.ToDouble());
                return fp.FromDouble(d);
            }
            long r = n;
            long prev;
            // 20 iterations is more than enough for 64-bit Newton iteration convergence.
            for (int i = 0; i < 20 && r != 0; i++)
            {
                prev = r;
                r = (r + n / r) >> 1;
                if (r == prev) break;
            }
            return fp.FromRaw(r);
        }

        // Taylor-series sin/cos. 7 terms is deterministic and adequate for phase 1
        // where sin/cos are only called at shot-setup time (not in the RK4 hot loop).
        // Angle in radians, reduced to [-π, π] first.
        private static readonly fp PI = fp.FromDouble(System.Math.PI);
        private static readonly fp TwoPI = fp.FromDouble(2.0 * System.Math.PI);

        private static fp ReduceAngle(fp a)
        {
            while (a > PI) a = a - TwoPI;
            while (a < -PI) a = a + TwoPI;
            return a;
        }

        public static fp Sin(fp a)
        {
            a = ReduceAngle(a);
            fp a2 = a * a;
            fp a3 = a2 * a;
            fp a5 = a3 * a2;
            fp a7 = a5 * a2;
            // sin(x) ≈ x - x³/6 + x⁵/120 - x⁷/5040
            return a
                - a3 / fp.FromInt(6)
                + a5 / fp.FromInt(120)
                - a7 / fp.FromInt(5040);
        }

        public static fp Cos(fp a)
        {
            a = ReduceAngle(a);
            fp a2 = a * a;
            fp a4 = a2 * a2;
            fp a6 = a4 * a2;
            // cos(x) ≈ 1 - x²/2 + x⁴/24 - x⁶/720
            return fp.One
                - a2 / fp.FromInt(2)
                + a4 / fp.FromInt(24)
                - a6 / fp.FromInt(720);
        }
    }
}
```

If you use one of the packaged fixed-point libs, skip Part A.alt and just adapt the rest of the spec to use their `fp`/`fp3`/`Sqrt`/`Sin`/`Cos` equivalents. Note any naming differences in a comment.

---

### Part B — Sim core

Create these files under `Assets/Scripts/Physics/Core/`:

**Assembly definition:** also create `Assets/Scripts/Physics/Core/Golfin.Physics.Core.asmdef` with `"noEngineReferences": true` and `"allowUnsafeCode": false`. This compile-time enforces the "no Unity APIs" rule — the sim core cannot import `UnityEngine` even accidentally. The runtime/view layer (Phase 1 Part D) lives in a separate assembly that DOES reference UnityEngine.

Asmdef content:
```json
{
    "name": "Golfin.Physics.Core",
    "rootNamespace": "Golfin.Physics",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "autoReferencedOn": false,
    "noEngineReferences": true
}
```

Also create `Assets/Scripts/Physics/Math/Golfin.Physics.Math.asmdef` — same shape but `noEngineReferences: true` only if you don't use the `fp3.ToVector3()` convenience method. If you include the Vector3 bridge, this asmdef needs UnityEngine — so split it: put `fp` and `fp3` (pure) in `Golfin.Physics.Math`, and the Vector3 extension in `Golfin.Physics.Math.Unity` which has UnityEngine referenced.

**Files in `Assets/Scripts/Physics/Core/`:**

#### `ShotInput.cs`
```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Complete deterministic input for a single shot. Everything the simulator
    /// needs to produce an identical trajectory on any platform.
    /// Phase 1 uses only origin, velocity, maxDuration. Spin / wind / surface
    /// fields are reserved for Phases 2+ and can be default-valued for now.
    /// </summary>
    public readonly struct ShotInput
    {
        public readonly fp3 origin;             // world meters, Unity axis (Y up)
        public readonly fp3 velocity;           // world m/s
        public readonly fp maxDuration;          // seconds, hard cap on integration

        // Phase 2+ fields — unused in Phase 1 but declared for ABI stability.
        public readonly fp3 spinAxis;
        public readonly fp spinRateRadPerSec;
        public readonly uint seed;

        public ShotInput(fp3 origin, fp3 velocity, fp maxDuration,
                         fp3 spinAxis = default, fp spinRateRadPerSec = default,
                         uint seed = 0)
        {
            this.origin = origin;
            this.velocity = velocity;
            this.maxDuration = maxDuration;
            this.spinAxis = spinAxis;
            this.spinRateRadPerSec = spinRateRadPerSec;
            this.seed = seed;
        }
    }
}
```

#### `Trajectory.cs`
```csharp
using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Output of BallSimulation.Simulate. Deterministic: identical inputs
    /// produce identical trajectory data bit-for-bit on any platform.
    /// </summary>
    public sealed class Trajectory
    {
        public readonly List<TrajectorySample> samples;
        public readonly fp3 finalPosition;
        public readonly fp3 finalVelocity;
        public readonly fp finalTime;
        public readonly TerminationReason termination;

        // Phase 2+ will populate this; Phase 1 leaves it empty.
        public readonly List<TerrainHit> terrainHits;

        public Trajectory(List<TrajectorySample> samples, fp3 finalPosition,
                          fp3 finalVelocity, fp finalTime, TerminationReason termination,
                          List<TerrainHit> terrainHits)
        {
            this.samples = samples;
            this.finalPosition = finalPosition;
            this.finalVelocity = finalVelocity;
            this.finalTime = finalTime;
            this.termination = termination;
            this.terrainHits = terrainHits;
        }
    }

    public readonly struct TrajectorySample
    {
        public readonly fp time;
        public readonly fp3 position;
        public readonly fp3 velocity;
        public TrajectorySample(fp time, fp3 position, fp3 velocity)
        { this.time = time; this.position = position; this.velocity = velocity; }
    }

    public readonly struct TerrainHit
    {
        public readonly fp time;
        public readonly fp3 position;
        public readonly fp3 velocityBefore;
        public readonly fp3 velocityAfter;
        public readonly int surfaceId;
        public TerrainHit(fp time, fp3 position, fp3 vBefore, fp3 vAfter, int surfaceId)
        { this.time = time; this.position = position; this.velocityBefore = vBefore;
          this.velocityAfter = vAfter; this.surfaceId = surfaceId; }
    }

    public enum TerminationReason
    {
        MaxDurationReached,
        HitGround,
        StoppedRolling,     // Phase 4+
        ExitedWorldBounds,
    }
}
```

#### `IGroundProvider.cs`
```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Abstraction over the terrain heightmap. Phase 1 uses FlatGround(y=0).
    /// Phase 4 swaps in a provider backed by the Q16.16 heightmap.bytes file
    /// baked in Phase 0.
    /// </summary>
    public interface IGroundProvider
    {
        /// <summary>World Y of the ground surface at (worldX, worldZ), meters.</summary>
        fp SampleHeight(fp worldX, fp worldZ);
    }

    public sealed class FlatGround : IGroundProvider
    {
        private readonly fp y;
        public FlatGround(fp y) { this.y = y; }
        public FlatGround() { this.y = fp.Zero; }
        public fp SampleHeight(fp worldX, fp worldZ) => y;
    }
}
```

#### `BallSimulation.cs`

The meat. Pure-C#, no Unity. RK4 integrator with vacuum-only dynamics (gravity). Ground-hit termination by interpolation between the last sample and the current sample when `position.y < ground.y`.

```csharp
using System.Collections.Generic;
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    public static class BallSimulation
    {
        // Phase 1 constants — later these move to CSV-backed config
        private static readonly fp Gravity = fp.FromDouble(-9.80665);  // m/s², Y axis
        private static readonly fp Dt = fp.One / fp.FromInt(240);       // 1/240 s
        private static readonly fp WorldBound = fp.FromInt(2000);       // ±2km safety

        /// <summary>
        /// Integrate ball flight from input.origin with input.velocity until one of:
        /// - maxDuration reached
        /// - ball y falls below ground.SampleHeight at (x, z)
        /// - position exits ±WorldBound on x or z
        ///
        /// Returns deterministic Trajectory. Same inputs → same bytes, every time,
        /// every platform.
        /// </summary>
        public static Trajectory Simulate(ShotInput input, IGroundProvider ground)
        {
            var samples = new List<TrajectorySample>(capacity: 1536);
            fp3 pos = input.origin;
            fp3 vel = input.velocity;
            fp t = fp.Zero;

            samples.Add(new TrajectorySample(t, pos, vel));

            TerminationReason termination = TerminationReason.MaxDurationReached;

            // RK4 step count = ceil(maxDuration / dt), with safety ceiling
            int maxSteps = 60 * 240;  // 60 seconds of integration hard cap
            for (int step = 0; step < maxSteps; step++)
            {
                if (t >= input.maxDuration)
                {
                    termination = TerminationReason.MaxDurationReached;
                    break;
                }

                // RK4 integration — vacuum trajectory, acceleration is constant gravity
                // so RK1/RK2/RK4 all give identical results. Use RK4 anyway to match
                // the structure Phase 2+ will need when acceleration depends on velocity.
                fp3 k1v = Accel(pos, vel);
                fp3 k1p = vel;

                fp3 pos2 = pos + k1p * (Dt / fp.FromInt(2));
                fp3 vel2 = vel + k1v * (Dt / fp.FromInt(2));
                fp3 k2v = Accel(pos2, vel2);
                fp3 k2p = vel2;

                fp3 pos3 = pos + k2p * (Dt / fp.FromInt(2));
                fp3 vel3 = vel + k2v * (Dt / fp.FromInt(2));
                fp3 k3v = Accel(pos3, vel3);
                fp3 k3p = vel3;

                fp3 pos4 = pos + k3p * Dt;
                fp3 vel4 = vel + k3v * Dt;
                fp3 k4v = Accel(pos4, vel4);
                fp3 k4p = vel4;

                fp3 posNext = pos + (k1p + k2p * fp.FromInt(2) + k3p * fp.FromInt(2) + k4p)
                              * (Dt / fp.FromInt(6));
                fp3 velNext = vel + (k1v + k2v * fp.FromInt(2) + k3v * fp.FromInt(2) + k4v)
                              * (Dt / fp.FromInt(6));
                fp tNext = t + Dt;

                // Ground hit detection — interpolate between pos and posNext
                fp groundY = ground.SampleHeight(posNext.x, posNext.z);
                if (posNext.y <= groundY && pos.y > groundY)
                {
                    // Linear interpolation to find t where y == groundY
                    fp dy = pos.y - posNext.y;           // positive (descending)
                    fp above = pos.y - groundY;          // positive
                    fp frac = dy.raw == 0 ? fp.Zero : above / dy;
                    fp3 hitPos = new fp3(
                        pos.x + (posNext.x - pos.x) * frac,
                        groundY,
                        pos.z + (posNext.z - pos.z) * frac);
                    fp3 hitVel = new fp3(
                        vel.x + (velNext.x - vel.x) * frac,
                        vel.y + (velNext.y - vel.y) * frac,
                        vel.z + (velNext.z - vel.z) * frac);
                    fp tHit = t + (tNext - t) * frac;
                    samples.Add(new TrajectorySample(tHit, hitPos, hitVel));
                    pos = hitPos; vel = hitVel; t = tHit;
                    termination = TerminationReason.HitGround;
                    break;
                }

                // World bounds
                if (posNext.x > WorldBound || posNext.x < -WorldBound ||
                    posNext.z > WorldBound || posNext.z < -WorldBound)
                {
                    termination = TerminationReason.ExitedWorldBounds;
                    samples.Add(new TrajectorySample(tNext, posNext, velNext));
                    pos = posNext; vel = velNext; t = tNext;
                    break;
                }

                pos = posNext;
                vel = velNext;
                t = tNext;
                samples.Add(new TrajectorySample(t, pos, vel));
            }

            return new Trajectory(samples, pos, vel, t, termination, new List<TerrainHit>());
        }

        /// <summary>
        /// Acceleration as a function of position and velocity.
        /// Phase 1: gravity only (position and velocity ignored for accel).
        /// Phase 2 will add drag and Magnus lift that depend on velocity.
        /// </summary>
        private static fp3 Accel(fp3 pos, fp3 vel)
        {
            return new fp3(fp.Zero, Gravity, fp.Zero);
        }
    }
}
```

**Why RK4 in Phase 1 when Euler would give identical results for constant gravity?**
Because Phase 2 adds velocity-dependent drag and Magnus lift, and we want the integrator structure baked in from day one. Writing Phase 1 as Euler and refactoring in Phase 2 is more work than writing RK4 once. Cost is trivial: ~12 extra fp ops per step × 1440 steps ≈ 17k ops, <1ms on any phone.

---

### Part C — EditMode test suite

Create `Assets/Scripts/Physics/Tests/Golfin.Physics.Tests.asmdef`:
```json
{
    "name": "Golfin.Physics.Tests",
    "rootNamespace": "Golfin.Physics.Tests",
    "references": [
        "Golfin.Physics.Core",
        "Golfin.Physics.Math",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "optionalUnityReferences": ["TestAssemblies"],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "autoReferencedOn": false,
    "noEngineReferences": false
}
```

Create `Assets/Scripts/Physics/Tests/ProjectileMathTests.cs`:

```csharp
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using System;

namespace Golfin.Physics.Tests
{
    public class ProjectileMathTests
    {
        private static readonly double g = 9.80665;

        /// <summary>
        /// Classic range equation: R = v² * sin(2θ) / g, for launch from y=0
        /// to y=0 on flat ground.
        /// </summary>
        private static double AnalyticalRange(double speed, double angleRad)
        {
            return speed * speed * System.Math.Sin(2.0 * angleRad) / g;
        }

        [Test]
        public void Simulate_Gravity_Only_MatchesAnalyticalRange_Within_1Percent()
        {
            // Deterministic PRNG — seed 12345
            var rng = new System.Random(12345);
            int failures = 0;
            double worstErrorPct = 0;
            int checkedCount = 0;

            for (int i = 0; i < 1000; i++)
            {
                // Random speed 10..80 m/s, angle 5°..80°
                double speed = 10.0 + rng.NextDouble() * 70.0;
                double angleDeg = 5.0 + rng.NextDouble() * 75.0;
                double angleRad = angleDeg * System.Math.PI / 180.0;

                // Aim along +Z in world space (standard golf "down the range")
                double vz = speed * System.Math.Cos(angleRad);
                double vy = speed * System.Math.Sin(angleRad);

                var input = new ShotInput(
                    origin: new fp3(fp.Zero, fp.Zero, fp.Zero),
                    velocity: new fp3(fp.Zero, fp.FromDouble(vy), fp.FromDouble(vz)),
                    maxDuration: fp.FromInt(30));

                var ground = new FlatGround(fp.Zero);
                var traj = BallSimulation.Simulate(input, ground);

                Assert.AreEqual(TerminationReason.HitGround, traj.termination,
                    $"Shot {i}: expected HitGround, got {traj.termination} " +
                    $"(speed={speed:F2}, angle={angleDeg:F2}°)");

                double simulatedRange = traj.finalPosition.z.ToDouble();
                double expectedRange = AnalyticalRange(speed, angleRad);
                double errorPct = System.Math.Abs(simulatedRange - expectedRange)
                                  / expectedRange * 100.0;

                if (errorPct > worstErrorPct) worstErrorPct = errorPct;
                if (errorPct > 1.0) failures++;
                checkedCount++;
            }

            UnityEngine.Debug.Log($"[ProjectileMathTests] 1000 random shots: " +
                                  $"failures (>1% error): {failures}, " +
                                  $"worst error: {worstErrorPct:F3}%");

            Assert.AreEqual(1000, checkedCount);
            Assert.AreEqual(0, failures,
                $"{failures} shots exceeded 1% range error. " +
                $"Worst error: {worstErrorPct:F3}%");
        }

        [Test]
        public void Simulate_ZeroVelocity_BallDropsAndHitsGround()
        {
            var input = new ShotInput(
                origin: new fp3(fp.Zero, fp.FromDouble(10), fp.Zero),
                velocity: fp3.Zero,
                maxDuration: fp.FromInt(30));
            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero));

            Assert.AreEqual(TerminationReason.HitGround, traj.termination);
            // t = sqrt(2h/g) = sqrt(20/9.80665) ≈ 1.4285 s
            double expected = System.Math.Sqrt(20.0 / g);
            double actual = traj.finalTime.ToDouble();
            Assert.AreEqual(expected, actual, 0.01, $"Expected drop time {expected}, got {actual}");
        }

        [Test]
        public void Simulate_IsDeterministic_SameInputsSameBytes()
        {
            var input = new ShotInput(
                origin: fp3.Zero,
                velocity: new fp3(fp.Zero, fp.FromDouble(20), fp.FromDouble(30)),
                maxDuration: fp.FromInt(30));
            var ground = new FlatGround(fp.Zero);

            var a = BallSimulation.Simulate(input, ground);
            var b = BallSimulation.Simulate(input, ground);

            Assert.AreEqual(a.samples.Count, b.samples.Count);
            for (int i = 0; i < a.samples.Count; i++)
            {
                Assert.AreEqual(a.samples[i].position.x.raw, b.samples[i].position.x.raw, $"sample {i} x mismatch");
                Assert.AreEqual(a.samples[i].position.y.raw, b.samples[i].position.y.raw, $"sample {i} y mismatch");
                Assert.AreEqual(a.samples[i].position.z.raw, b.samples[i].position.z.raw, $"sample {i} z mismatch");
                Assert.AreEqual(a.samples[i].velocity.x.raw, b.samples[i].velocity.x.raw, $"sample {i} vx mismatch");
                Assert.AreEqual(a.samples[i].velocity.y.raw, b.samples[i].velocity.y.raw, $"sample {i} vy mismatch");
                Assert.AreEqual(a.samples[i].velocity.z.raw, b.samples[i].velocity.z.raw, $"sample {i} vz mismatch");
            }
        }

        [Test]
        public void Simulate_SampleCount_IsReasonable()
        {
            // 45° launch at 30 m/s → ~4.3s flight, ~1030 samples at 240 Hz + init + hit
            var input = new ShotInput(
                origin: fp3.Zero,
                velocity: new fp3(fp.Zero, fp.FromDouble(21.213), fp.FromDouble(21.213)),
                maxDuration: fp.FromInt(30));
            var traj = BallSimulation.Simulate(input, new FlatGround(fp.Zero));

            Assert.AreEqual(TerminationReason.HitGround, traj.termination);
            Assert.GreaterOrEqual(traj.samples.Count, 1000);
            Assert.LessOrEqual(traj.samples.Count, 1100);
        }
    }
}
```

---

### Part D — Driving range test scene

New scene: `Assets/Scenes/Physics/Phase1_VacuumTest.unity`.

Build via Unity-MCP `scene-create` + `gameobject-create`:

1. **GameObject: "Ground"** — a `Cube`, scale `(200, 0.2, 400)`, position `(0, -0.1, 200)`. This places a flat ground plane from z=0 to z=400, centered on x=0. Material: any default URP Lit.
2. **GameObject: "TeeOrigin"** — empty GO at `(0, 0, 0)`. Visual reference only.
3. **GameObject: "Ball"** — a `Sphere`, scale `(0.4, 0.4, 0.4)` (slightly oversized for visibility), position `(0, 0.05, 0)`. Material: white URP Lit.
4. **GameObject: "TrajectoryLine"** — has a `LineRenderer` component:
   - `useWorldSpace = true`
   - `widthMultiplier = 0.15`
   - Material: `Sprites/Default` with color `(1, 0.4, 0.2, 1)` — bright orange
   - `positionCount` starts at 0
5. **GameObject: "PhysicsTestController"** — has a new MonoBehaviour `Phase1TestController` (Part D.1 below).
6. **Camera:** position `(-30, 15, 50)`, rotation `(15, 30, 0)` — side-angle view of the range.
7. **Directional Light** — default angle is fine.

The scene's only job is to let Cesar (and Ken, if we show Ken anything) visually sanity-check a trajectory. Tests are the real validation.

Save the scene.

#### Part D.1 — `Phase1TestController.cs`

Place at `Assets/Scripts/Physics/Runtime/Phase1TestController.cs`. Needs its own asmdef `Golfin.Physics.Runtime` referencing `Golfin.Physics.Core`, `Golfin.Physics.Math`, and UnityEngine.

```csharp
using System.Collections.Generic;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    public class Phase1TestController : MonoBehaviour
    {
        [Header("Shot Input")]
        [Range(5f, 80f)] public float launchSpeed = 50f;      // m/s
        [Range(5f, 80f)] public float launchAngleDeg = 25f;   // degrees above horizontal, along +Z

        [Header("Refs")]
        public Transform ball;
        public LineRenderer trajectoryLine;

        [Header("Playback")]
        [Range(0.1f, 3f)] public float playbackSpeed = 1f;
        public bool autoReplay = true;

        private Trajectory _trajectory;
        private float _playbackTime;

        void Start() => FireShot();

        [ContextMenu("Fire Shot")]
        public void FireShot()
        {
            float angleRad = launchAngleDeg * Mathf.Deg2Rad;
            var input = new ShotInput(
                origin: new fp3(fp.Zero, fp.FromDouble(0.05), fp.Zero),  // match ball visual
                velocity: new fp3(
                    fp.Zero,
                    fp.FromDouble(launchSpeed * Mathf.Sin(angleRad)),
                    fp.FromDouble(launchSpeed * Mathf.Cos(angleRad))),
                maxDuration: fp.FromInt(30));

            _trajectory = BallSimulation.Simulate(input, new FlatGround(fp.FromDouble(0.05)));
            _playbackTime = 0;

            Debug.Log($"[Phase1Test] Shot: speed={launchSpeed} m/s, angle={launchAngleDeg}°, " +
                      $"samples={_trajectory.samples.Count}, " +
                      $"range={_trajectory.finalPosition.z.ToFloat():F1} m, " +
                      $"flight time={_trajectory.finalTime.ToFloat():F2} s, " +
                      $"termination={_trajectory.termination}");

            // Render the full trajectory as a polyline
            trajectoryLine.positionCount = _trajectory.samples.Count;
            for (int i = 0; i < _trajectory.samples.Count; i++)
                trajectoryLine.SetPosition(i, _trajectory.samples[i].position.ToVector3());
        }

        void Update()
        {
            if (_trajectory == null || _trajectory.samples.Count == 0) return;

            _playbackTime += Time.deltaTime * playbackSpeed;
            float totalTime = _trajectory.finalTime.ToFloat();

            if (_playbackTime >= totalTime)
            {
                if (autoReplay) { _playbackTime = 0; }
                else { _playbackTime = totalTime; }
            }

            // Find the sample bracket containing _playbackTime (linear search — few samples)
            var samples = _trajectory.samples;
            int i1 = samples.Count - 1;
            for (int i = 1; i < samples.Count; i++)
            {
                if (samples[i].time.ToFloat() >= _playbackTime) { i1 = i; break; }
            }
            int i0 = System.Math.Max(0, i1 - 1);
            float t0 = samples[i0].time.ToFloat();
            float t1 = samples[i1].time.ToFloat();
            float frac = t1 > t0 ? (_playbackTime - t0) / (t1 - t0) : 0;
            Vector3 p0 = samples[i0].position.ToVector3();
            Vector3 p1 = samples[i1].position.ToVector3();
            ball.position = Vector3.Lerp(p0, p1, frac);
        }

        void OnValidate()
        {
            if (Application.isPlaying && ball != null && trajectoryLine != null)
                FireShot();
        }
    }
}
```

`fp3.ToVector3()` is defined in Part A — this MonoBehaviour is the intended consumer.

**Wire-up:** after creating the scene, assign `ball` → the Sphere GameObject's Transform, `trajectoryLine` → the TrajectoryLine GameObject's LineRenderer, via `gameobject-component-modify` or similar Unity-MCP calls.

---

### Part E — Unity-MCP autonomous validation

Drive this loop yourself; don't hand off mid-sequence:

**Step 1 — Compile check.** After writing all files, `console-get-logs`. Zero compile errors required. If errors, fix; up to 5 iterations. Common gotchas:
- Asmdef misconfiguration — missing reference, typo in assembly name
- `fp.FromDouble` vs `fp.FromFloat` confusion
- Forgetting to add `using Golfin.Physics.Math;`

**Step 2 — Run the tests.** `tests-run` targeting `Golfin.Physics.Tests` EditMode. All 4 tests must pass. Read `console-get-logs` for the `[ProjectileMathTests] 1000 random shots:` line — log the reported failure count and worst error.

If `Simulate_Gravity_Only_MatchesAnalyticalRange_Within_1Percent` fails:
- The RK4 implementation is likely wrong — not the math library.
- Check sign of gravity. Check whether `Dt/2` is computing correctly in fp. Check that `maxDuration` is large enough (30s > any realistic flight).
- If worst error is ~5–10%, the integrator is roughly correct but has a systematic bias. Check the RK4 weighted-sum coefficients.
- If errors are random (huge for some, tiny for others), fixed-point precision is insufficient somewhere — look for intermediate multiplies overflowing Q16.16 range.

If `Simulate_IsDeterministic_SameInputsSameBytes` fails, you have nondeterminism somewhere — most likely the math library. Double-check no `System.Random` is used in the sim core, no `DateTime.Now`, no floats.

**Step 3 — Load the test scene and run it.** `scene-open Phase1_VacuumTest`, `editor-application-set-state` to enter Play Mode, wait a frame, `screenshot-game-view`. The trajectory line should show an orange parabolic arc with the ball mid-flight. `console-get-logs` will show the `[Phase1Test] Shot: ...` line — confirm range is nonzero, termination is `HitGround`.

**Step 4 — Exit Play Mode** (`editor-application-set-state` → EditMode), confirm no errors linger in the console, save the scene.

**Autonomous iteration budget:** 5 attempts max. Failure mode reports should include: the failing test name(s), worst error percentage, full error/stack from `console-get-logs`, and the last few diffs attempted.

---

### Part F — Done report contents

Paste back into this chat:

1. Math library chosen (package name + version, or "hand-rolled" if Part A.alt was used).
2. Test results: `[ProjectileMathTests] 1000 random shots: failures: N, worst error: X%`. Pass/fail for each of the 4 tests.
3. One screenshot of the Phase1 test scene in Play Mode showing the orange trajectory.
4. Scene's Debug.Log output line showing speed/angle/range/time/termination for the default shot (speed=50, angle=25).
5. Files created (paths only; I know what's inside).
6. Anomalies: any asmdef errors resolved, any precision issues encountered, any Unity-MCP tool quirks worth noting.

---

### DO NOT

- Import `UnityEngine` into `Golfin.Physics.Core` or `Golfin.Physics.Math` assemblies. Enforced by asmdef `noEngineReferences: true`.
- Use `float`, `double`, `Mathf`, `UnityEngine.Random`, `System.Random`, `DateTime.Now`, `Time.deltaTime`, or any other non-deterministic source inside `BallSimulation.Simulate` or its callees.
- Add drag, lift, Magnus, wind, or surface interaction. Those are Phases 2–4.
- Read `heightmap.bytes` in Phase 1. Flat ground only. (Phase 4 writes the heightmap reader.)
- Bake any visual polish into the scene — it's a test harness, not a demo. Ugly orange line on gray ground is fine.
- Change `ShotInput`'s shape. Phase 2+ fields are already declared for ABI stability.
- Optimize anything. RK4 at 240 Hz runs in <1ms; premature optimization will obscure bugs.

---

### If stuck

- Fixed-point package install fails → use Part A.alt hand-rolled version. It's 80 lines, all tested-by-shape, low risk.
- Test fails with ~50% error → check gravity sign. Y up in Unity, gravity is negative.
- Test fails with exactly 2x error → check the RK4 weighted sum: it's `(k1 + 2k2 + 2k3 + k4) * dt/6`, not `(k1 + k2 + k3 + k4) * dt/4`.
- Compile error about `fp3` not existing → you skipped Part A.alt and the chosen package doesn't export `fp3`. Either add our own `fp3` wrapper or switch to the package's native vector type and update the consumer code.
- Unity-MCP `tests-run` says "no tests found" → asmdef `optionalUnityReferences` is probably missing `TestAssemblies`, or `includePlatforms` doesn't include Editor.

---

## History Log (completed tasks, most recent first)

- ✅ **2026-04-21** Phase 1 Vacuum Trajectory Integrator — hand-rolled Q16.16 fp/fp3/fpMath; BallSimulation RK4 240Hz; 4/4 EditMode tests pass (1000 shots, 0 failures, worst error 0.164%); Phase1_VacuumTest.unity scene; default shot 50m/s 25° → 195.3m range, 4.31s flight, HitGround. Fixed-point precision fix: `(sum * Dt) / 6` over `sum * (Dt/6)`. Files: `Assets/Scripts/Physics/Math/`, `Physics/Core/`, `Physics/Runtime/`, `Physics/Tests/`, `Assets/Scenes/Physics/Phase1_VacuumTest.unity`.

- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — `PhysicsHeightmapBaker.cs` created. Menu items: Bake Current Hole / Bake Hole 01-18 / Bake All Holes. Q16.16 fixed-point, binary `heightmap.bytes` with GHM1 header. Hole 1 baked: 16.02 MB, 0/100 round-trip mismatches. All 18 holes subsequently baked. File at `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/heightmap.bytes`.
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
