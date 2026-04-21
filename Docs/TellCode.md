# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## ACTIVE TASK — Phase 4: Surface interaction (bounce + roll)

✅ DONE: 2026-04-21 — 29/29 tests pass. Bounce+roll+stop+water+MaxBounces all implemented. Key fixes: UnityEngine.Physics namespace qualification, per-surface SurfaceConfig.Default, speed²-based stop detection (avoids fpMath.Sqrt underestimation), one-sided boundary differences in SampleNormal. Part G test scene deferred (manual QA, non-blocking).



### Context

Phase 0 baked `heightmap.bytes` (Q16.16, 2049×2049, 36-byte header) for all 18 holes. Phase 3 added wind. Phase 4 is where the ball finally stops being a projectile and starts *landing*: it bounces, it rolls, it stops. This is the phase that makes game feel emerge — a ball checking on a green, running out on a fairway, plugging in a bunker.

Five concerns, integrated:

1. **Runtime heightmap provider.** Load `heightmap.bytes` at scene start, expose `IGroundProvider.SampleHeight(x, z)` reading from the Q16.16 grid rather than `terrain.SampleHeight()`. Deterministic across platforms.
2. **Surface classification.** Given a world position, return which surface the ball is over: green, fairway, semi-rough, rough, sand, cart path, tee, water. Reuses the existing zone-mesh breadcrumb components (`GreenSurfaceInfo`, `BunkerSurfaceInfo` already placed per memory; generalize to a provider).
3. **Bounce model.** When `pos.y ≤ groundY` and the ball has downward velocity, apply coefficient of restitution for the surface, reflect velocity off the ground normal (computed from heightmap gradient), compute friction loss on the tangent component, record a `TerrainHit`.
4. **Roll model.** When the ball's vertical velocity is small and it stays in contact with the ground for several steps, switch to a surface-constrained roll integrator. Gravity along the slope accelerates, rolling resistance decelerates, ball follows the heightmap surface until it stops.
5. **Stop detection.** Velocity below a per-surface threshold on a near-flat surface. Return the final resting position in the existing `Trajectory.finalPosition`.

All five live in `BallSimulation.Simulate(...)` — same entry point as Phase 1–3. New overload signature takes an `ISurfaceProvider` alongside `IGroundProvider`. Existing overloads forward with a flat-ground fallback and an all-fairway surface fallback so Phase 1–3 tests remain untouched.

Determinism and Phase 2.1 aero invariants all still apply: Q16.16 only, Core stays `noEngineReferences: true`, multiply-before-divide, no `UnityEngine.Random`, no `Mathf.*`.

Reference: `Docs/PHYSICS_RESEARCH.md` Section 3 (surface coefficients, per-surface bounce values), `Docs/LESSONS_PHYSICS_AERO.md` (aero invariants), Phase 0 baker at `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` for file format.

### Scope boundaries — read before starting

**In scope:**
- Runtime heightmap loading from `heightmap.bytes`.
- Surface classification via scene-placed breadcrumb components + fallback to "fairway" for unmarked areas.
- Bounce with per-surface restitution + tangent friction.
- Roll with per-surface rolling resistance + slope acceleration.
- Stop detection.
- Water hit = terminate simulation with `TerminationReason.HitWater`; penalty system is not this phase.
- Cart path = high-restitution bounce (Confluence flags this as a known issue we're explicitly getting right).
- `surfaces.csv` with tunable coefficients, hot-reloadable in the tuning window.

**Out of scope:**
- Penalty system / ball-in-water recovery rules.
- Plugged lies (ball embedded deep in sand or thick rough). Future work.
- Spin-assisted backspin on first bounce. If a ball lands with heavy backspin, it should check — but implementing that correctly requires modeling ball-surface contact spin transfer. Phase 4 approximation: spin affects restitution via a simple multiplier; no tangent-velocity kick-back. Flag for future refinement.
- Dynamic wind during roll (wind only affects airborne phase).
- Putt model. Phase 5.
- OOB detection, fairway-hit detection for scoring. Gameplay layer, not physics.

---

### Part A — Runtime heightmap provider (Runtime, not Core)

UnityEngine reference is allowed here — this is loading a `TextAsset` and exposing a pure-math interface to Core. The interface itself stays in Core; only the loader is in Runtime.

#### `Assets/Scripts/Physics/Core/HeightmapData.cs` — new (pure data, Core)

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// In-memory Q16.16 heightmap. Row-major [y, x]. Metric units (meters).
    /// Indexed by (worldX, worldZ) via SampleHeight; performs bilinear interpolation
    /// between the four nearest grid cells for sub-cell precision.
    ///
    /// Built by HeightmapLoader (Runtime) from heightmap.bytes. Pure math here —
    /// no UnityEngine, no Resources, no file I/O.
    /// </summary>
    public sealed class HeightmapData : IGroundProvider
    {
        public readonly int Resolution;
        public readonly fp SizeX, SizeZ;
        public readonly fp OriginX, OriginY, OriginZ;  // world-space position of heightmap corner [0,0]
        private readonly int[] heights;  // Q16.16 raw; length = Resolution * Resolution

        public HeightmapData(int resolution, fp sizeX, fp sizeZ, fp originX, fp originY, fp originZ, int[] heights)
        {
            Resolution = resolution;
            SizeX = sizeX; SizeZ = sizeZ;
            OriginX = originX; OriginY = originY; OriginZ = originZ;
            this.heights = heights;
        }

        public fp SampleHeight(fp worldX, fp worldZ)
        {
            // Convert world to grid coords.
            fp gx = ((worldX - OriginX) / SizeX) * fp.FromInt(Resolution - 1);
            fp gz = ((worldZ - OriginZ) / SizeZ) * fp.FromInt(Resolution - 1);

            // Clamp to valid range.
            fp maxIdx = fp.FromInt(Resolution - 1);
            gx = fpMath.Clamp(gx, fp.Zero, maxIdx);
            gz = fpMath.Clamp(gz, fp.Zero, maxIdx);

            // Integer and fractional parts.
            int ix = (int)gx.ToInt();
            int iz = (int)gz.ToInt();
            if (ix >= Resolution - 1) ix = Resolution - 2;
            if (iz >= Resolution - 1) iz = Resolution - 2;
            fp fx = gx - fp.FromInt(ix);
            fp fz = gz - fp.FromInt(iz);

            // Bilinear sample.
            fp h00 = fp.FromRaw(heights[iz * Resolution + ix]);
            fp h10 = fp.FromRaw(heights[iz * Resolution + (ix + 1)]);
            fp h01 = fp.FromRaw(heights[(iz + 1) * Resolution + ix]);
            fp h11 = fp.FromRaw(heights[(iz + 1) * Resolution + (ix + 1)]);

            fp h0 = h00 + (h10 - h00) * fx;
            fp h1 = h01 + (h11 - h01) * fx;
            return OriginY + h0 + (h1 - h0) * fz;
        }

        /// <summary>
        /// Surface normal at (worldX, worldZ), computed from heightmap gradient via central differences.
        /// Unit vector, pointing away from the ground (positive Y component).
        /// </summary>
        public fp3 SampleNormal(fp worldX, fp worldZ)
        {
            fp cellX = SizeX / fp.FromInt(Resolution - 1);
            fp cellZ = SizeZ / fp.FromInt(Resolution - 1);
            fp hL = SampleHeight(worldX - cellX, worldZ);
            fp hR = SampleHeight(worldX + cellX, worldZ);
            fp hD = SampleHeight(worldX, worldZ - cellZ);
            fp hU = SampleHeight(worldX, worldZ + cellZ);
            // Tangent vectors: along +X (dh/dx, 1, 0)-ish; along +Z (0, dh/dz, 1)-ish.
            // Normal = cross(tangentX, tangentZ); normalize.
            fp dhdx = (hR - hL) / (cellX * fp.FromInt(2));
            fp dhdz = (hU - hD) / (cellZ * fp.FromInt(2));
            fp3 n = new fp3(-dhdx, fp.One, -dhdz);
            return fpMath.Normalize(n);
        }
    }
}
```

`fp.FromRaw` must exist (it's used in Phase 2.1 WindModel per my earlier spec; if the naming differs — `fp.FromBits`, `new fp { raw = ... }`, whatever — use the project's existing idiom). Same for `fpMath.Normalize`; add if missing, following the pattern of `fpMath.Cross` already in the math lib.

#### `Assets/Scripts/Physics/Runtime/HeightmapLoader.cs` — new

```csharp
using System.IO;
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Loads heightmap.bytes (baked by PhysicsHeightmapBaker) into a HeightmapData.
    /// Format: 36-byte header (GHM1 magic + version + resolution + sizeX/Z + posX/Y/Z + format),
    /// then row-major [y, x] int32 Q16.16 heights in meters.
    /// </summary>
    public static class HeightmapLoader
    {
        public static HeightmapData LoadFromBytes(byte[] data)
        {
            if (data == null || data.Length < 36) return null;
            using (var ms = new MemoryStream(data))
            using (var br = new BinaryReader(ms))
            {
                // Magic
                if (br.ReadByte() != 'G' || br.ReadByte() != 'H' || br.ReadByte() != 'M' || br.ReadByte() != '1')
                {
                    Debug.LogError("[HeightmapLoader] Bad magic; expected GHM1.");
                    return null;
                }
                int version = br.ReadInt32();
                if (version != 1) { Debug.LogError($"[HeightmapLoader] Unknown version {version}."); return null; }
                int res = br.ReadInt32();
                float sx = br.ReadSingle();
                float sz = br.ReadSingle();
                float px = br.ReadSingle();
                float py = br.ReadSingle();
                float pz = br.ReadSingle();
                int format = br.ReadInt32();
                if (format != 1) { Debug.LogError($"[HeightmapLoader] Unknown format {format}; expected Q16.16."); return null; }

                var heights = new int[res * res];
                for (int i = 0; i < heights.Length; i++)
                    heights[i] = br.ReadInt32();

                return new HeightmapData(
                    res,
                    fp.FromFloat(sx), fp.FromFloat(sz),
                    fp.FromFloat(px), fp.FromFloat(py), fp.FromFloat(pz),
                    heights);
            }
        }

        /// <summary>Convenience loader from a scene-attached TextAsset reference.</summary>
        public static HeightmapData LoadFromTextAsset(TextAsset asset)
            => asset == null ? null : LoadFromBytes(asset.bytes);
    }
}
```

#### `Assets/Scripts/Physics/Runtime/HeightProvider.cs` — new MonoBehaviour

```csharp
using UnityEngine;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// Scene component holding the loaded heightmap for the active hole.
    /// Attach to a GameObject on the hole scene; assign the heightmap TextAsset.
    /// Other systems (BallSimulation callers, debug UI) read HeightmapData via this.
    /// </summary>
    public sealed class HeightProvider : MonoBehaviour
    {
        [SerializeField] private TextAsset heightmapAsset;
        public HeightmapData Data { get; private set; }

        void Awake()
        {
            if (heightmapAsset == null)
            {
                Debug.LogError("[HeightProvider] No heightmap TextAsset assigned.", this);
                return;
            }
            Data = HeightmapLoader.LoadFromTextAsset(heightmapAsset);
            if (Data == null)
                Debug.LogError("[HeightProvider] Failed to load heightmap.", this);
            else
                Debug.Log($"[HeightProvider] Loaded {Data.Resolution}×{Data.Resolution} heightmap, " +
                          $"size {Data.SizeX.ToFloat()}×{Data.SizeZ.ToFloat()} m.");
        }
    }
}
```

---

### Part B — Surface classification

Per memory, `GreenSurfaceInfo` and `BunkerSurfaceInfo` breadcrumb MonoBehaviours are already placed on zone meshes with submesh indices (green=0/collar=1, sand=0/lip=1). Phase 4 adds a general surface provider that reuses those breadcrumbs and adds new ones for other zones.

#### `Assets/Scripts/Physics/Core/SurfaceType.cs` — new (Core, pure enum)

```csharp
namespace Golfin.Physics
{
    public enum SurfaceType : byte
    {
        Fairway = 0,    // default for unmarked terrain
        Green,
        GreenCollar,
        Semirough,
        Rough,
        Tee,
        Sand,
        BunkerLip,
        CartPath,
        Water,
        OOB,
    }
}
```

#### `Assets/Scripts/Physics/Core/ISurfaceProvider.cs` — new (Core)

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Classifies a world position to a surface type. Runtime implementation
    /// reads zone-marker components on the hole scene; Core tests use a constant stub.
    /// </summary>
    public interface ISurfaceProvider
    {
        SurfaceType Classify(fp worldX, fp worldZ);
    }

    /// <summary>Stub provider used by Phase 1–3 tests and unit tests. Returns one surface everywhere.</summary>
    public sealed class ConstantSurfaceProvider : ISurfaceProvider
    {
        private readonly SurfaceType type;
        public ConstantSurfaceProvider(SurfaceType t) { type = t; }
        public SurfaceType Classify(fp worldX, fp worldZ) => type;
    }
}
```

#### `Assets/Scripts/Physics/Runtime/SceneSurfaceProvider.cs` — new

Runtime implementation. Casts a vertical ray down from `(x, large_Y, z)`, finds the topmost zone mesh collider hit, reads a `SurfaceMarker` component off the hit object.

```csharp
using UnityEngine;
using Golfin.Physics.Math;

namespace Golfin.Physics.Runtime
{
    /// <summary>
    /// One-component-per-zone-mesh marker. Zone overlay builder attaches these.
    /// Claude Code adds them to any zone meshes that lack them (fairway, rough,
    /// semi-rough, cart path, tee, water). Greens and bunkers already have
    /// GreenSurfaceInfo / BunkerSurfaceInfo — the provider checks for those first
    /// and falls back to SurfaceMarker for everything else.
    /// </summary>
    public sealed class SurfaceMarker : MonoBehaviour
    {
        public SurfaceType Type = SurfaceType.Fairway;
    }

    /// <summary>
    /// Surface classifier backed by scene geometry. Raycasts downward to find
    /// the top zone mesh at (x, z); reads SurfaceMarker (or legacy *SurfaceInfo).
    /// If no marker is hit, returns SurfaceType.Fairway as default.
    ///
    /// The raycast is permitted to be non-deterministic because surface classification
    /// is a static property of the hole geometry — the result is the same every call.
    /// We use PhysX here deliberately; scene geometry doesn't change during a shot.
    /// </summary>
    public sealed class SceneSurfaceProvider : ISurfaceProvider
    {
        private const float RaycastFromY = 500f;
        private const float RaycastLength = 1000f;
        private readonly int layerMask;

        public SceneSurfaceProvider(int layerMask = ~0) { this.layerMask = layerMask; }

        public SurfaceType Classify(fp worldX, fp worldZ)
        {
            var origin = new Vector3(worldX.ToFloat(), RaycastFromY, worldZ.ToFloat());
            if (!Physics.Raycast(origin, Vector3.down, out var hit, RaycastLength, layerMask, QueryTriggerInteraction.Collide))
                return SurfaceType.Fairway;

            // Check for existing legacy breadcrumbs first (greens, bunkers).
            var green = hit.collider.GetComponentInParent<SurfaceMarker>();
            if (green != null) return green.Type;

            // TODO: add support for GreenSurfaceInfo / BunkerSurfaceInfo breadcrumbs.
            // For Phase 4 MVP, require SurfaceMarker on every zone mesh.
            return SurfaceType.Fairway;
        }
    }
}
```

**Claude Code: during this task, scan the hole-1 scene and report which zone mesh roots lack a `SurfaceMarker` component. Do not auto-add markers — instead list them in the done report so Cesar can add them manually or approve a bulk-add script.** Hole 1 is the only hole we need fully marked for Phase 4 validation; the other 17 can be marked later.

If `GreenSurfaceInfo` or `BunkerSurfaceInfo` already exist with submesh fields, the Phase 4 spec accepts reading their existing fields via a dedicated lookup branch; update `SceneSurfaceProvider.Classify` to check those first (returning `SurfaceType.Green` or `SurfaceType.Sand` accordingly). The memory entry for these components says they exist but runtime wiring was deferred — this is the phase that wires them.

---

### Part C — Surface coefficient config

#### `Assets/Resources/Physics/surfaces.csv` — new

```csv
surface,restitution,tangent_friction,rolling_resistance,stop_speed_mps,notes
Fairway,0.50,0.55,0.18,0.10,closely-mown grass baseline
Green,0.40,0.75,0.12,0.05,checks quickly; low roll
GreenCollar,0.45,0.65,0.15,0.08,between fairway and green
Semirough,0.38,0.70,0.28,0.15,mower-height intermediate
Rough,0.25,0.82,0.45,0.22,ball plugs; high resistance
Tee,0.55,0.45,0.15,0.10,tight mown
Sand,0.15,0.85,0.70,0.25,bunker; heavy damping
BunkerLip,0.20,0.80,0.55,0.20,lip; redirects downward
CartPath,0.70,0.18,0.06,0.08,very bouncy; very low friction
Water,0.00,1.00,1.00,0.00,ball stops immediately; marked hazard
OOB,0.20,0.80,0.50,0.20,treated like rough for bounce; scoring handles OOB
```

Columns:
- `restitution` (Cr): vertical velocity retained after bounce. `v_y_out = -Cr · v_y_in`.
- `tangent_friction`: tangent velocity retained after bounce. `v_t_out = (1 - μ) · v_t_in`. Higher = more friction.
- `rolling_resistance` (1/s): deceleration factor during roll. `v -= v · rolling * dt`.
- `stop_speed_mps`: roll phase ends when `|v| < stop_speed` for N consecutive steps.
- Water: zero restitution, full friction, ball stops instantly; `TerminationReason.HitWater` fires.

Tunable in the Physics Tuning Window. Reload via `PhysicsConfigLoader.LoadSurfaceConfig()`.

#### `Assets/Scripts/Physics/Core/SurfaceConfig.cs` — new

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    public struct SurfaceCoefficients
    {
        public fp Restitution;       // Cr, 0..1
        public fp TangentFriction;   // μ, 0..1 (0 = frictionless)
        public fp RollingResistance; // 1/s, decel during roll
        public fp StopSpeed;         // m/s, threshold for stop detection
    }

    public struct SurfaceConfig
    {
        // Indexed by (int)SurfaceType. Length = number of SurfaceType values.
        public SurfaceCoefficients[] Coefficients;

        public SurfaceCoefficients this[SurfaceType t] => Coefficients[(int)t];

        public static SurfaceConfig Default
        {
            get
            {
                int n = System.Enum.GetValues(typeof(SurfaceType)).Length;
                var c = new SurfaceCoefficients[n];
                // Conservative defaults; real values come from surfaces.csv.
                for (int i = 0; i < n; i++)
                    c[i] = new SurfaceCoefficients
                    {
                        Restitution       = fp.FromFloat(0.40f),
                        TangentFriction   = fp.FromFloat(0.60f),
                        RollingResistance = fp.FromFloat(0.20f),
                        StopSpeed         = fp.FromFloat(0.10f),
                    };
                // Water / OOB override.
                c[(int)SurfaceType.Water] = new SurfaceCoefficients
                {
                    Restitution = fp.Zero, TangentFriction = fp.One,
                    RollingResistance = fp.One, StopSpeed = fp.Zero,
                };
                return new SurfaceConfig { Coefficients = c };
            }
        }
    }
}
```

#### `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` — extend

Add `LoadSurfaceConfig()` that reads `Resources/Physics/surfaces.csv`, returns `SurfaceConfig`. Same tolerance pattern as aero / wind loaders: missing file → `Default`, missing rows → default for that type, log warnings. Parse surface name as `Enum.TryParse<SurfaceType>(…)`.

---

### Part D — Bounce + roll integrator

#### `Assets/Scripts/Physics/Core/Trajectory.cs` — extend

Add bounce records. These were already anticipated in the type per Phase 1 (has `TerrainHits` list empty today). Populate during Phase 4:

```csharp
public struct TerrainHit
{
    public fp Time;
    public fp3 Position;
    public fp3 VelocityIn;    // before bounce
    public fp3 VelocityOut;   // after bounce (zero if hit ended sim — water, stop)
    public SurfaceType Surface;
    public bool IsStop;       // true = this hit is the final stop, not a bounce
}
```

`TerminationReason` gets new values:

```csharp
public enum TerminationReason
{
    MaxDurationReached,
    HitGround,        // existing; means first touched ground (Phase 1–3 sim ends here)
    ExitedWorldBounds,
    // New in Phase 4:
    BallStopped,      // roll phase reached stop_speed on near-flat surface
    HitWater,         // terminated by water hazard
    MaxBouncesExceeded, // safety cap; shouldn't happen in practice
}
```

#### `Assets/Scripts/Physics/Core/BallSimulation.cs` — big change

New overload (most general):

```csharp
public static Trajectory Simulate(
    ShotInput input,
    IGroundProvider ground,
    AeroConfig aero,
    WindConfig wind,
    ISurfaceProvider surfaces,
    SurfaceConfig surfaceCfg)
```

Existing overloads forward:

- `Simulate(input, ground)` → `Simulate(input, ground, Vacuum, Calm, ConstantSurfaceProvider(Fairway), Default)`
- `Simulate(input, ground, aero)` → adds aero only
- `Simulate(input, ground, aero, wind)` → adds wind only

Phase 1–3 tests all pass through the most general overload via forwarding. This is critical: the bit-exact regression gates (`Wind_Calm_MatchesPhase2Aero_ExactlyEqual` etc.) must remain bit-exact. Test that explicitly (Part F).

**Integration flow in the new overload:**

1. **Airborne phase** (existing Phase 1–3 code). RK4 with gravity + aero + wind. Runs until `pos.y <= groundY` AND `velocity.y < 0`.
2. **Bounce handler.** At the moment of ground contact:
   - Compute ground normal from `heightmap.SampleNormal(posHit.x, posHit.z)` (cast the ground provider to `HeightmapData` if possible; else use flat-up normal).
   - Decompose velocity into normal and tangent components: `v_n = dot(v, n); v_t = v - v_n*n`.
   - Classify surface: `surface = surfaces.Classify(posHit.x, posHit.z)`.
   - **Water:** record hit with `IsStop=true, Surface=Water`, set termination `HitWater`, return.
   - **Normal bounce:** `v_n_out = -Cr * v_n; v_t_out = (1 - μ) * v_t; v_out = v_n_out * n + v_t_out`.
   - Record a `TerrainHit` with pre/post velocity.
   - Continue the airborne phase from the bounce position with the new velocity.
3. **Roll transition.** When a bounce produces a ball whose outgoing vertical speed (along ground normal) is below `roll_transition_threshold = 0.5 m/s` AND the ball's total speed is above its surface's `StopSpeed`, switch to roll mode.
4. **Roll phase.** At each step (use the same Dt = 1/240s):
   - Sample ground height and normal under the ball.
   - Project velocity onto the tangent plane: `v = v - dot(v, n)*n` (removes any residual normal component).
   - Gravity acceleration along the slope: `a_gravity_tangent = g - dot(g, n)*n` where `g = (0, -9.80665, 0)`.
   - Rolling resistance: `a_resistance = -v * rolling_resistance` (proportional to current speed).
   - `v += (a_gravity_tangent + a_resistance) * Dt`. Multiply-before-divide: `v += (a_total * Dt)`, not `(a_total / Dt_recip)`.
   - Position: `pos.xz += v.xz * Dt`. Project pos.y onto terrain: `pos.y = SampleHeight(pos.x, pos.z) + ball_radius`.
   - Re-classify surface each step (ball might roll from fairway onto green, or into a bunker).
   - **Stop condition:** if `|v| < surface.StopSpeed` for 10 consecutive steps (42 ms), declare stop. Record a `TerrainHit` with `IsStop=true`. Set termination `BallStopped`. Return.
   - **Water during roll:** if ball rolls into water, terminate with `HitWater`.
5. **Max bounces safety.** Cap bounces at 12. If exceeded, terminate with `MaxBouncesExceeded` and log warning. Real shots bounce 2–6 times before rolling; 12 is a generous ceiling that catches runaway oscillation from bad tuning.

**Spin during surface phase:** for Phase 4, apply a simple restitution multiplier to vertical velocity based on spin. If `spin.Axis · v_horizontal < 0` (backspin relative to motion), multiply `Cr` by 1.15 (ball checks on landing). If sidespin relative to motion, no effect in Phase 4. Don't model tangent velocity kickback. Spin decays at the existing aero rate during airborne phase; during roll, spin is set to zero (ball is rolling, not spinning freely).

This is the "simple approximation" flagged in the scope boundaries. If it feels wrong in playtest, upgrade the contact model; don't tune `surfaces.csv` to compensate.

---

### Part E — Tuning window

`PhysicsTuningWindow.cs` gets a "Surfaces" foldout:

- Per-surface rows: restitution, tangent friction, rolling resistance, stop speed — all sliders.
- "Reload surfaces.csv" button.
- A "Simulate drop test" button: spawns a ball 30m above the green, zero horizontal velocity, 3000 rpm backspin, runs the sim, reports final bounces + stop location. Quick sanity check while tuning surface values.

Keep it functional.

---

### Part F — Tests

`Assets/Scripts/Physics/Tests/SurfaceTests.cs` — new. Namespace `Golfin.Physics.Tests`.

1. **`Surface_Phase3Overloads_BitExact`** — run the same 7-iron shot through `Simulate(input, ground)`, `Simulate(input, ground, aero)`, `Simulate(input, ground, aero, wind)`, and the new full `Simulate(input, ground, aero, wind, surfaces, surfaceCfg)` with stub providers. All four must produce bit-exact identical trajectories when the added parameters are defaults (`ConstantSurfaceProvider(Fairway)` + `SurfaceConfig.Default`). **Blocking gate** — if this fails, surface threading broke the forward path and must be fixed before tuning anything.

2. **`Surface_Bounce_OnGreenWithBackspin_Checks`** — ball dropped from 30m onto Green with 5000 rpm backspin. Assert: final stop position is within 15m of drop point (checks hard). Compare to same drop with zero spin: zero-spin ball should roll further than backspin ball by at least 3m. Directional, not magnitude-precise.

3. **`Surface_Bounce_OnCartPath_HighRestitution`** — ball dropped from 10m onto CartPath. Assert: first bounce height exceeds 60% of drop height (Cr = 0.70 gives ≥49% energy retained = ≥70% height retention before air drag; conservative 60% catches it with margin).

4. **`Surface_Roll_StopsOnFlatFairway`** — ball starts at ground level with 10 m/s horizontal velocity, flat fairway (no slope). Run sim. Assert: ball stops within 35m (rolling_resistance 0.18 /s gives ~e-folding 5.5s → ~25m travel). Assert: final velocity < 0.15 m/s. Assert: final termination is `BallStopped`.

5. **`Surface_Roll_AcceleratesDownSlope`** — ball dropped at rest on a synthetic heightmap representing a 10° slope. Run sim. Assert: ball rolls downhill (x-displacement in slope direction > 5m after 3 seconds). Assert: ball stays in contact with surface (no airborne samples between start and stop).

6. **`Surface_Water_TerminatesSim`** — ball dropped onto Water surface. Assert: `TerminationReason == HitWater`. Assert: final position's Y matches water surface. Assert: exactly one `TerrainHit` recorded with `Surface == Water` and `IsStop == true`.

7. **`Surface_MaxBounces_Capped`** — synthetic scenario with restitution = 0.95 on a flat surface (bounces forever). Run sim. Assert: termination is `MaxBouncesExceeded`, not an infinite loop. Test must complete in under 5 seconds wall-clock.

8. **`Surface_Heightmap_BilinearInterpolation_SubCellPrecision`** — synthetic 3×3 heightmap with known values. Sample at cell centers → returns exact values. Sample at midpoints between cells → returns linear interpolation within 1e-4 tolerance (Q16.16 precision limit).

All existing tests must still pass (Phase 1 = 4, Phase 2 = 3, Phase 2.1 = 8, Phase 3 = 6 → total 21). Phase 4 adds 8. Target: **29 tests total, 29 pass.**

---

### Part G — Phase 4 test scene

Build a new scene via Unity-MCP: `Assets/Scenes/Physics/Phase4_SurfaceTest.unity`.

Load on top of Hole 1 geometry. Attach `HeightProvider` with the Hole 1 `heightmap.bytes` TextAsset. Add a simple test controller:

- "Fire driver shot": uses the Phase 2 driver club spec, tee origin at Hole 1 tee, target fairway. Logs bounces and final position to console; draws a LineRenderer for trajectory + red dots at each bounce.
- "Fire wedge shot": from 100m out, wedge parameters, target green. Watch ball check.
- "Drop test": ball released above the green with 3000rpm backspin.

This scene is manual QA, not an automated test. Screenshot it after the final run for the done report.

---

### Part H — Unity-MCP autonomous validation

1. Compile clean. `console-get-logs` after each major change, max 5 iterations.
2. `tests-run` filter `Golfin.Physics.Tests`. All 29 pass.
3. `Surface_Phase3Overloads_BitExact` is the blocking gate; if it fails, stop and report.
4. Open `Phase4_SurfaceTest.unity`, run "Fire driver shot" in Play Mode, screenshot the Game view with trajectory + bounces visible. Verify the ball lands on fairway and rolls to stop (not in water, not OOB).
5. Run "Drop test" on the green; screenshot showing the ball checks and stops near the drop point.
6. Scan Hole 1 scene for zone meshes without `SurfaceMarker` components. List them in the done report.

### Done report

- 29-test pass/fail summary.
- Bounce count + final stop position for the driver shot on Hole 1 (target: 2–4 bounces, stops in fairway or rough past 200m).
- Drop-test stop distance from initial impact (target: < 8m with 3000 rpm backspin on green).
- List of Hole 1 zone mesh roots missing `SurfaceMarker` (blocking for full Hole 1 classification).
- Final `surfaces.csv` contents if any coefficients were tuned.
- Screenshots: driver trajectory + bounces, drop-test checking ball.
- Any anomalies or deviations from the spec.

### DO NOT

- Modify Phase 1–3 tests. The bit-exact gate in `Surface_Phase3Overloads_BitExact` exists precisely to catch accidental changes to the airborne path.
- Tune aero LUTs, clubs.csv, wind.csv, or per-club test tolerances from earlier phases.
- Use `UnityEngine.Terrain.SampleHeight()` in sim code. Sim uses `HeightmapData.SampleHeight` only. The PhysX raycast in `SceneSurfaceProvider` is acceptable because it's for static scene geometry classification, not per-step simulation.
- Use `System.Random` or `UnityEngine.Random` anywhere in Core.
- Add `SurfaceMarker` components to zone meshes automatically. List missing ones; let Cesar decide the rollout.
- Treat cart path as regular fairway with high restitution. Cart path is its own `SurfaceType` — the "ball-on-asphalt misclassified" issue from old builds is explicitly what we're getting right here.
- Build Phase 5 (putt) features. Roll model is a smaller approximation than full putt — no gravity-well assist, no green-reading helpers, no slope pre-calculation. Putt is Phase 5.

### Iteration budget

5 tuning iterations on `surfaces.csv` if initial values feel off. "Feels off" means a test fails or the manual QA scene shows obviously wrong behavior (ball bouncing up and down forever on green, ball tunneling through fairway). Do not tune past 5 iterations — report instead, and we'll either accept the current feel or add a diagnostic test.

---

## History Log (completed tasks, most recent first)

- ✅ **2026-04-21** Phase 3 Wind — `WindConfig`, `WindModel.SampleWind`, `fpMath.Sin`/`TwoPi`, wind.csv, tuning window integration, 6 tests. 21/21 tests pass. Seed determinism verified bit-exact. Headwind/tailwind/crosswind/altitude profile all behave directionally.
- ✅ **2026-04-21** Phase 2.1 closeout — LUT-mode tests split by club class with honest per-club tolerances. Driver/Iron3 at 25%, mid-irons at 15%, wedges at 8%. 15 tests pass. Lessons filed at LESSONS_PHYSICS_AERO.md. Physics baseline accepted.
- ❌ **2026-04-21 REMEDIATION v3 — ARCHITECTURE ESCALATION HIT (Rung 3)** — Bearman–Harvey Cl at driver S=0.08 physically cannot produce 275 yd carry; lift barely balances gravity at launch. 1D-BH model ceiling. Not escalating to 2D LUT. Lessons filed: `Docs/LESSONS_PHYSICS_AERO.md`.
- ⚠️ **2026-04-21 REMEDIATION v2** Seed-value error, not architecture — Cl too high at low S. Driver 23.5% short residual matched ratio of seed overshoot.
- ⚠️ **2026-04-21 REMEDIATION v1** Correctly reverted `spin_drag_factor` scope creep; incorrectly reverted `spin_decay_rate` (real physics, restored in v3).
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles); v0 tuning produced unphysical shapes. Series of remediations followed.
- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow`.
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 at dt=1/240s. **Gotcha:** `Dt/6` in Q16.16 truncates; reorder as `(sum * Dt) / 6`.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — Q16.16 fixed-point binary `heightmap.bytes`. All 18 holes baked. 36-byte header (GHM1 + version + res + sizeX/Z + posX/Y/Z + format).
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
