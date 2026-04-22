# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## ACTIVE TASK — Physics Viewer (lab scenes for visual/mechanical confirmation)

### Context

Physics is complete through Phase 5. 35 tests green. Determinism verified. But tests prove correctness, not *feel* — Cesar needs button-click visual confirmation that a 7-iron looks like a 7-iron, a ball checks on a green, wind visibly shortens a drive, and determinism shows up on screen as "same preset twice = same stop position."

This task builds three lab scenes that fire canonical shots through `BallSimulation.Simulate(...)`, render the resulting `Trajectory` as a color-coded line, and animate a ball prefab along it. No gameplay coupling — it's a lab instrument. It also closes out three deferred threads from earlier phases:

1. Phase 4 Part G (Hole 1 test scene — deferred as non-blocking)
2. Phase 5 Part G (Hole 1 putt scene — deferred as non-blocking)
3. Hole 1 `SurfaceMarker` audit + wiring (listed in Phase 4 done report, awaited Cesar's rollout decision)

Reference: `Docs/PHYSICS_RESEARCH.md` for architecture, `Docs/PHYSICS_TUNING_TARGETS.md` Section 1 for club carry targets (the numbers the presets must visibly demonstrate).

### Scope boundaries — read before starting

**In scope:**
- Three scenes under `Assets/Scenes/Physics/`: `PhysicsLab_Range.unity`, `PhysicsLab_Hole1.unity`, `PhysicsLab_Dashboard.unity`.
- New scripts under `Assets/Scripts/Physics/Viewer/`: `TrajectoryRenderer`, `BallAnimator`, `ChaseCamera`, `PhysicsLabController`, `PhysicsLabUI`, `DashboardUI`, `ShotPreset` (data), `ShotPresetCatalog` (hardcoded list).
- In-Play-Mode Unity UI Canvas with preset dropdown, camera-mode dropdown, play-rate slider, Fire / Fire&Compare / Clear / Fire×5 buttons, readout panel.
- 15 preset shots (list below). Presets 1–10 run in Range; 11–15 run in Hole 1.
- Dashboard sliders bound live to `AeroConfig`, `WindConfig`, `SurfaceConfig`, `PuttConfig`; "Reload CSVs" and "Reset to defaults" buttons.
- Ball prefab: `Assets/Art/3D/Balls/Rare/Prefabs/Pf_GOLFIN_MK2_Ball.prefab` — instantiated per shot, any Rigidbody made kinematic, any Colliders disabled (see Part B).
- `HeightProvider` + `SceneSurfaceProvider` wired on `PhysicsLab_Hole1.unity` using the existing Hole 1 `heightmap.bytes`.
- Audit Hole 1 zone meshes for missing `SurfaceMarker` components; report list. Do NOT auto-add markers — Cesar decides rollout before Hole 1 presets are validated.
- CSV hot-reload in Play Mode (Resources cache bypass helper if needed).

**Out of scope:**
- Flick-to-shoot input. UI panel only this phase.
- Aim reticle, landing-zone preview, gravity-well putter, any assist. Assists are gameplay-layer per architecture rule.
- `StatModifierResolver` integration. Lab uses raw `ShotInput` values; character stats are gameplay.
- Hole-detect / cup-fall.
- Power gauge, aim arrow, any shot-composition UI polish.
- Replay export, photo mode, trajectory screenshots as files.
- Adding `SurfaceMarker` components to Hole 1 meshes automatically.
- Touching `BallSimulation` or any Core physics code. Lab is pure consumer.

---

### Part A — `TrajectoryRenderer`

`Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs` — new. Namespace `Golfin.Physics.Viewer`.

MonoBehaviour. Takes a `Trajectory`, draws it.

- One `LineRenderer` for the main path. Width 0.08m.
- Color segments by phase — read from the sample sequence and `TerrainHits`:
  - White: airborne (before first `TerrainHit`)
  - Orange: between bounces (after first hit, before the hit where `vnOut < RollTransitionThreshold`)
  - Green: rolling (after the roll transition)
  - Cyan: putting (if termination path came through `RunPuttPhase` — detect by `TerrainHits.Count == 1 && IsStop` AND no airborne arc, i.e. all samples have `pos.y <= startPos.y + 0.5m`)
- LineRenderer supports per-vertex colors via `colorGradient` or the `SetColors` pattern; use whichever yields the fewest allocations. Prefer setting `positionCount` once and `SetPositions` with a preallocated `Vector3[]`.
- Sphere marker at each `TerrainHit.Position`. Color by `Surface`:
  - Fairway: light green · Green: bright green · GreenCollar: yellow-green · Rough: dark green · Sand: tan · CartPath: grey · Water: blue · Others: magenta ("unhandled surface" flag).
  - Markers are primitive spheres, scale 0.3m, parented to the renderer GameObject.
- One extra sphere at `Trajectory.finalPosition` — scale 0.5m, gold. The "rest" marker.
- "Ghost mode": `SetGhost(bool)` that dims the LineRenderer alpha to 0.3 and hides markers. Used by Fire&Compare.
- `Clear()` removes all markers and resets the LineRenderer.

Non-goals: no animation, no physics, no input. Pure rendering of a completed `Trajectory`.

---

### Part B — `BallAnimator`

`Assets/Scripts/Physics/Viewer/BallAnimator.cs` — new.

MonoBehaviour. Given a `Trajectory` and a ball GameObject, moves the ball along `trajectory.samples` in real time.

**Prefab handling (critical):**

The lab instantiates `Pf_GOLFIN_MK2_Ball.prefab` per shot. The prefab is authored for gameplay and may carry a `Rigidbody` and/or `Collider`s. The animator does not want PhysX touching the ball — the trajectory is pre-computed and deterministic; PhysX would fight it.

On `Awake` of the spawned instance:

```csharp
foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
    rb.isKinematic = true;
foreach (var col in instance.GetComponentsInChildren<Collider>())
    col.enabled = false;
```

Log the component inventory once at spawn: `Debug.Log($"[BallAnimator] Ball prefab components: {string.Join(", ", comps.Select(c => c.GetType().Name))}");` — Cesar wants to know what's on the prefab for reference.

**Animation:**

- `Play(Trajectory t)` starts the animation at sample[0].
- Update loop uses `Time.unscaledDeltaTime * PlayRate`, advances a `currentSimTime`, finds the bracketing samples, lerps position linearly between them. Trajectory sample rate is 240 Hz — linear lerp between adjacent samples is visually smooth.
- `PlayRate` setter: 0.25, 1.0, 4.0, or a special `Instant` mode that snaps straight to `finalPosition`.
- At end, ball parks at `finalPosition` and stays. Calling `Play(...)` again destroys the current instance and spawns a fresh one at the new trajectory's start.
- Ball scale: pass through whatever the prefab ships with. Do not rescale. Realism of ball size vs. course is not the lab's concern.
- Optional TrailRenderer: if the prefab has one, keep it enabled. If it doesn't, don't add one. Zero assumption.

**Camera coupling:** the animator exposes `public Transform CurrentBall { get; }` so `ChaseCamera` can follow it.

---

### Part C — `ChaseCamera`

`Assets/Scripts/Physics/Viewer/ChaseCamera.cs` — new.

MonoBehaviour on a Camera. Three modes selectable at runtime via a public enum `Mode { Chase, Overhead, GroundLevel }`:

- **Chase:** position = ball + offset (−8m behind initial launch direction, +3m up). Looks at ball. Offset direction is set once per shot from `ShotInput.velocity.xz` so the camera starts behind the shot direction and trails smoothly.
- **Overhead:** position directly above the ball, 40m up. Looks straight down. Good for seeing lateral drift from wind or putt curves.
- **GroundLevel:** fixed at the shot origin + 1.6m up. Does not follow the ball — watches it fly away. Good for driver shape.

Smoothing: `Vector3.SmoothDamp` with 0.15s smoothing time for position; `Quaternion.Slerp` at `10 * Time.deltaTime` for rotation. Feels tracked, not glued.

Public: `SetMode(Mode m)`, `SetTarget(Transform t)`, `ResetToOrigin(Vector3 origin, Vector3 launchDir)`.

---

### Part D — `ShotPreset` + `ShotPresetCatalog`

`Assets/Scripts/Physics/Viewer/ShotPreset.cs` — new data struct.

```csharp
public enum PresetScene { Range, Hole1, Dashboard }

public readonly struct ShotPreset
{
    public readonly string Id;              // stable, e.g. "driver_calm"
    public readonly string DisplayName;     // "Driver — calm"
    public readonly PresetScene Scene;
    public readonly fp3 Origin;             // world-space; scene builder places ball here
    public readonly fp3 Velocity;           // m/s
    public readonly SpinState Spin;
    public readonly WindConfig Wind;        // usually WindConfig.Calm; overridden for wind presets
    public readonly string Notes;           // shown in readout panel (expected carry, what it demonstrates)

    public ShotPreset(string id, string name, PresetScene scene, fp3 origin, fp3 velocity,
                      SpinState spin, WindConfig wind, string notes) { ... }
}
```

`Assets/Scripts/Physics/Viewer/ShotPresetCatalog.cs` — hardcoded static list of 15 presets. `public static IReadOnlyList<ShotPreset> All { get; }` and `public static IEnumerable<ShotPreset> ForScene(PresetScene s)`.

**Presets (exact values are starting points; tune during implementation if a preset doesn't hit its target within ±10%):**

Range scene (flat fairway, synthetic `ConstantSurfaceProvider`):

| # | Id | Velocity (m/s) | Spin | Wind | Target demo |
|---|---|---|---|---|---|
| 1 | `driver_calm` | (69.5, 18, 0) at ~15° launch | 2800 rpm backspin | Calm | ~275yd carry |
| 2 | `driver_headwind` | same | same | 10 m/s along −X | ~245yd carry |
| 3 | `driver_tailwind` | same | same | 10 m/s along +X | ~305yd carry |
| 4 | `driver_crosswind` | same | same | 10 m/s along +Z | lateral drift visible |
| 5 | `iron7_calm` | (46, 22, 0) at ~26° launch | 6500 rpm backspin | Calm | ~155yd total |
| 6 | `wedge_100_backspin` | (32, 28, 0) at ~41° launch | 9000 rpm backspin | Calm | Checks near landing |
| 7 | `wedge_100_zerospin` | same velocity | Zero spin | Calm | Rolls out; contrast with #6 |
| 8 | `cartpath_bounce` | (25, −10, 0) dropped from 10m | Zero | Calm | First bounce ≥60% drop height — requires synthetic `ConstantSurfaceProvider(CartPath)` |
| 9 | `rough_landing` | (40, 15, 0) | Zero | Calm | Ball plugs; single bounce, short roll — synthetic `ConstantSurfaceProvider(Rough)` |
| 10 | `water_terminates` | (30, 10, 0) | Zero | Calm | Sim terminates at water — synthetic `ConstantSurfaceProvider(Water)` |

Hole 1 scene (real geometry, `SceneSurfaceProvider`):

| # | Id | Origin | Velocity | Target demo |
|---|---|---|---|---|
| 11 | `putt_flat_3m` | on green, known flat spot | (0.35, 0, 0) | Stops at 3m ±0.3m |
| 12 | `putt_uphill_6m` | on green, known uphill | calibrated ~6m power | Stops short |
| 13 | `putt_downhill_6m` | same slope, opposite dir | calibrated ~6m power | Runs past |
| 14 | `putt_crossslope_6m` | on green, cross-slope area | calibrated ~6m power | Curves to low side |
| 15 | `putt_off_back` | near back of green | 3.5 m/s along +X | Green → fringe → rough transition |

**Note:** Claude Code must identify flat / sloped / back-edge positions on Hole 1's green during implementation. Open the scene, inspect the green mesh's bounds, pick reasonable points, commit the coordinates. Report coordinates in the done report so Cesar can sanity-check.

Dashboard scene runs #1 (`driver_calm`) as the tuning reference shot, scaled down to fit a 200×50m mini-range.

---

### Part E — `PhysicsLabController`

`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — new. MonoBehaviour. The scene's brain.

Responsibilities:

- Holds references to the ball prefab, `TrajectoryRenderer`, `BallAnimator`, `ChaseCamera`, ground provider, surface provider, current configs.
- On `Awake`: loads `AeroConfig` + `WindConfig` + `SurfaceConfig` + `PuttConfig` via `PhysicsConfigLoader`. Caches them.
- Builds the `ISurfaceProvider` appropriate for the scene:
  - Range scene: holds a settable `ConstantSurfaceProvider` — default `Fairway`, overridden per-preset for presets 8/9/10.
  - Hole1 scene: constructs a `SceneSurfaceProvider`.
  - Dashboard scene: `ConstantSurfaceProvider(Fairway)`.
- Builds the `IGroundProvider`:
  - Range: a flat `IGroundProvider` implementation (`FlatGroundProvider` — new utility if one doesn't exist already, returning `fp.Zero` for everything, normal (0,1,0)).
  - Hole1: `HeightProvider.Data` from the scene.
  - Dashboard: `FlatGroundProvider`.
- `Fire(ShotPreset preset)`:
  1. Apply any per-preset surface override on the provider.
  2. Build `ShotInput` from preset.
  3. Call `BallSimulation.Simulate(input, ground, aero, wind, surfaces, surfaceCfg, puttCfg)`.
  4. Hand the `Trajectory` to `TrajectoryRenderer.Draw(trajectory)` and `BallAnimator.Play(trajectory)`.
  5. Reset `ChaseCamera` to the new origin + launch direction.
  6. Compute readout metrics and publish to `PhysicsLabUI` via a `public event Action<ShotReadout> OnShotFired`.
- `FireCompare(ShotPreset preset)`: before calling `Fire`, asks `TrajectoryRenderer` to promote the current trajectory to a ghost, then fires.
- `FireRepeatability(ShotPreset preset, int count = 5)`: fires `count` times, records all `finalPosition` values, asserts bit-equality between them. Publishes a pass/fail badge to the UI.
- `Clear()`: clears renderer + destroys current ball instance.

**`ShotReadout` struct (new):**

```csharp
public struct ShotReadout
{
    public string PresetDisplayName;
    public float CarryMeters;      // distance from origin to first ground hit, XZ only
    public float TotalMeters;      // distance from origin to final stop, XZ only
    public float MaxHeightMeters;  // peak Y above origin Y
    public int   BounceCount;      // TerrainHits.Count - 1 (final stop is a hit too)
    public string TerminationReason;
    public SurfaceType FinalSurface;
    public float SimDurationSeconds;
}
```

All float conversions happen here at the boundary. The sim stays fp.

---

### Part F — `PhysicsLabUI`

`Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs` — new. Unity UI Canvas, attached in the scene.

Layout (top-left corner, fixed size 340×480, semi-transparent panel):

- **Preset dropdown** — populated from `ShotPresetCatalog.ForScene(currentScene)`. Display uses `ShotPreset.DisplayName`.
- **Camera mode dropdown** — `Chase / Overhead / GroundLevel`.
- **Play-rate slider** — discrete: 0.25× / 1× / 4× / Instant. Labeled.
- **Fire** button — calls `controller.Fire(selectedPreset)`.
- **Fire & Compare** button — calls `controller.FireCompare(selectedPreset)`.
- **Fire ×5 (determinism)** button — calls `controller.FireRepeatability(selectedPreset, 5)`.
- **Clear** button — calls `controller.Clear()`.
- **Readout panel** (below the buttons) — shows the most recent `ShotReadout`:
  ```
  Driver — calm
  Carry:    251.3 m  (274.9 yd)
  Total:    263.8 m  (288.6 yd)
  Peak:      34.2 m
  Bounces:   3
  Ended:     BallStopped on Fairway
  Duration:  6.42 s
  ```
- **Determinism badge** — small pill top-right of readout: `✓ 5/5 identical` in green, or `✗ drift detected` in red, after a Fire×5 run. Blank otherwise.
- **Notes footer** — shows `selectedPreset.Notes` so Cesar knows what each preset is supposed to demonstrate.

Use TextMeshPro for text. Standard Unity UI components otherwise. No animations, no fancy styling — readable and functional.

---

### Part G — `DashboardUI`

`Assets/Scripts/Physics/Viewer/DashboardUI.cs` — new. Dashboard scene only.

Bigger Canvas, 900×720. Left half: slider column grouped by foldout:

- **Aero** — `Cd`, `Cl` ceiling, `SpinDecayRate`, `BallMass`, `BallRadius`.
- **Wind** — base direction (two sliders — X, Z), magnitude, gust variance.
- **Surfaces** — per surface (Fairway, Green, GreenCollar, Rough, Sand, CartPath): Restitution, TangentFriction, RollingResistance, StopSpeed. Collapsed by default; expand one at a time.
- **Putt** — Green RollingResistance, Green StopSpeed, GreenCollar RollingResistance, GreenCollar StopSpeed.

Each slider has a label, a numeric field (editable), and the slider. Edits update the in-memory `*Config` immediately — no Apply button. The next Fire picks them up automatically.

Also:

- **Reload CSVs** button — calls `PhysicsConfigLoader.Load*Config()` for all four configs, overwrites the in-memory state, resets the sliders to the reloaded values.
- **Reset to defaults** button — loads `AeroConfig.Vacuum` / `WindConfig.Calm` / `SurfaceConfig.Default` / `PuttConfig.Default`.

Right half of the canvas: the mini-range preview. A small orthographic camera shows a 200m×50m strip with the trajectory drawn. Fire button at the bottom runs preset `driver_calm` at the current config state.

**CSV hot-reload caveat:** Unity caches `Resources.Load<TextAsset>(...)` results. A naive second call returns the cached asset even if the file changed on disk. `PhysicsConfigLoader.LoadSurfaceConfig()` etc. may need a wrapper that calls `Resources.UnloadAsset(asset)` before re-loading, or reads the CSV via `File.ReadAllText` during Play Mode in the Editor. If the existing loader already handles this, use it. If not, add a `ForceReload()` variant on each loader method for lab use only — do not change production load paths.

---

### Part H — Scene building

**`PhysicsLab_Range.unity`:**

- Flat ground plane, 2km × 2km, tiled fairway-green material. No colliders needed (animator is PhysX-free).
- Directional light, skybox.
- Empty GameObject `LabRoot` with `PhysicsLabController`.
- Child GameObjects: `TrajectoryRenderer` (with its LineRenderer component), `BallAnimator`, UI Canvas with `PhysicsLabUI`.
- Main camera with `ChaseCamera`.
- Origin marker at (0, 0, 0).
- 100m ruler markers along +X up to 400m, for visual scale.

**`PhysicsLab_Hole1.unity`:**

- Duplicate or additively-load Hole 1's scene geometry. Prefer duplicate — lab should not mutate the production Hole 1 scene.
- Attach `HeightProvider` to `LabRoot`, reference the Hole 1 `heightmap.bytes` TextAsset.
- Attach `SceneSurfaceProvider` (via a wrapper MonoBehaviour if it's not already a MB — construct in `Awake`).
- **Run the surface marker audit:** walk the scene, find all zone mesh roots (the contour-based overlays: greens, bunkers, water, cart paths, fairways, rough, tee). List every root that lacks a `SurfaceMarker` component. Log the list. **Do not auto-add markers.** Write the list into the done report so Cesar can decide rollout.
- Camera, UI canvas, LabRoot — same as Range.

**`PhysicsLab_Dashboard.unity`:**

- Flat mini-ground 200m × 50m.
- Fixed overhead-side camera pointed at the mini-range.
- `LabRoot` with controller + Dashboard UI canvas.

---

### Part I — Tests

`Assets/Scripts/Physics/Tests/ViewerTests.cs` — new. Namespace `Golfin.Physics.Tests`. **4 tests, EditMode.**

1. **`Viewer_PresetCatalog_AllIdsUnique`** — assert `ShotPresetCatalog.All` has distinct `Id` values.
2. **`Viewer_PresetCatalog_SceneCountsCorrect`** — assert `ForScene(Range).Count() == 10`, `ForScene(Hole1).Count() == 5`, `ForScene(Dashboard).Count() >= 1`.
3. **`Viewer_DriverCalm_CarryInExpectedRange`** — run preset `driver_calm` through `BallSimulation` directly (no scene). Assert carry distance in [240m, 280m] i.e. 262–306yd. Tolerance is wide because this test exists to catch regressions, not to pin yardage.
4. **`Viewer_FireRepeatability_IsBitExact`** — run preset `driver_calm` five times through the sim. Assert all five `finalPosition` values bit-equal (compare `.raw` fields of each `fp`).

All existing tests must still pass (Phases 1–5 = 35). Viewer adds 4. **Target: 39 tests total, 39 pass.**

---

### Part J — Unity-MCP autonomous validation

1. Compile clean after each scene / script added. `console-get-logs` max 5 iterations.
2. `tests-run` filter `Golfin.Physics.Tests`. All 39 pass.
3. Open `PhysicsLab_Range.unity` in Play Mode. Fire preset `driver_calm`. Screenshot the Game view with the trajectory line visible. Verify carry readout is in the 240–280m window.
4. Still in Range, Fire `driver_headwind` with Fire & Compare active; screenshot showing both trajectories with the headwind shot visibly shorter.
5. Still in Range, Fire ×5 on `driver_calm`. Screenshot showing `✓ 5/5 identical` badge.
6. Open `PhysicsLab_Hole1.unity`. Fire `putt_flat_3m`. Screenshot the green with the putt line + rest marker. Report final stop distance from origin.
7. Fire `putt_crossslope_6m`. Screenshot the curve.
8. Open `PhysicsLab_Dashboard.unity`. Drag Green RollingResistance to 0.25, fire the preview shot, screenshot. (If `driver_calm` is the dashboard preview, a different config change is appropriate — pick one that visibly affects the driver: e.g. drop `AeroConfig.Cd` multiplier by 50% and screenshot the longer carry.)
9. Ball prefab component inventory: include the `Debug.Log` output from the first ball spawn in the done report.
10. Hole 1 `SurfaceMarker` audit: include the missing-marker list in the done report.

### Done report

- 39-test pass/fail summary.
- Driver calm carry readout (meters + yards).
- Driver headwind delta from calm (meters).
- Fire×5 determinism result (pass/fail).
- 3m putt stop distance (should be 2.7–3.3m).
- Cross-slope putt lateral displacement (magnitude in m).
- Ball prefab component inventory (the Debug.Log line).
- Hole 1 zone meshes missing `SurfaceMarker` — full list, grouped by zone type if recognizable.
- Hole 1 putt origin coordinates chosen for presets 11–15.
- Screenshots: driver-calm trajectory, driver vs headwind (ghost compare), Fire×5 determinism badge, flat putt on green, cross-slope putt curve, dashboard CSV edit result.
- Any anomalies, preset velocities that needed tuning, or coefficients that didn't behave as the target range expected.

### DO NOT

- Modify `BallSimulation` or any file under `Assets/Scripts/Physics/Core/`. The lab is a consumer.
- Modify the physics CSVs. Dashboard mutates in-memory configs only. CSV edits are Cesar's job via the existing editor tuning window.
- Auto-add `SurfaceMarker` components to Hole 1 meshes. List missing ones in the done report.
- Duplicate the `Pf_GOLFIN_MK2_Ball.prefab`. Reference the existing prefab directly. Handle Rigidbody/Colliders defensively at runtime per Part B.
- Add flick controls, aim reticles, power gauges, or any gameplay input. Presets only.
- Extend `ShotInput` or `Trajectory`. If the readout needs a field that's not in `Trajectory` today, compute it in `ShotReadout` from the existing samples/hits.
- Build Hole 2–18 lab scenes. Hole 1 is sufficient to prove the architecture. Other holes come later (or not at all — this is a lab, not a product).
- Touch `HoleGeoImporter.cs`, terrain baking, or any course pipeline code.

### Iteration budget

- 3 iterations per preset if its carry/stop distance misses the target range by more than 15%. Tune the `ShotPreset` velocity, not the underlying `clubs.csv` or `aero.csv` — preset velocities are free parameters, club data is production.
- If a Range preset (1–10) exceeds 3 iterations without hitting target, report the divergence. Wide tolerance bands exist because the point is visual plausibility, not exact yardage pinning.
- Hole 1 presets (11–15) depend on actual green geometry. If a preset doesn't fit the chosen spot (e.g. cross-slope turns out to be too gentle to curve visibly), move the origin to a better spot on the same green before changing the velocity.

---

<!-- BEGIN ARCHIVED PHASE 5 SPEC — for reference only; superseded by ✅ history entry below -->

### Context

Phase 4 closed the airborne→bounce→roll loop. The roll integrator already does most of what a putt needs: tangent-plane projection, slope gravity, rolling resistance, stop detection. Phase 5 promotes the same primitive to a first-class shot type, with putt-specific tuning and a clean entry point gameplay can call without confusing it for a chip.

Per `Docs/PHYSICS_RESEARCH.md` Section 3 ("Putting") — the locked decision is **reuse `BallSimulation` with a fast-path collapse to 2D rolling, same `Trajectory` output, decouple later only if it gets messy.** Phase 5 implements that fast-path.

The fast-path detects "this is a putt" automatically from the input (low velocity + low launch angle + ball already on green/collar/fringe) and skips the airborne RK4 block entirely, jumping straight to a putt-tuned roll integrator. Same `Trajectory` shape comes out the other end — gameplay code doesn't need to know whether it called a putt or a chip.

### Scope boundaries — read before starting

**In scope:**
- Putt detection inside the most-general `Simulate(...)` overload. Detected from `ShotInput` + `ISurfaceProvider` lookup at origin.
- New `RunPuttPhase(...)` private method — a roll integrator tuned for putts, sharing physical structure with `RunRollPhase` but with putt-specific coefficients.
- `putt.csv` with putt-tuned per-surface coefficients (rolling resistance, stop speed). Loaded via `PhysicsConfigLoader.LoadPuttConfig()`.
- `PuttConfig` Core type and CSV loader extension.
- Putts that run off the green transition cleanly back into regular roll/bounce code (i.e. if the ball leaves green-class surfaces, it switches to `RunRollPhase` coefficients seamlessly).
- Tuning window "Putt" foldout: per-surface sliders (Green / GreenCollar / Fringe-as-Semirough) + reload button + drop-test analog ("Sim 3m putt at calibrated power").
- 6 new tests covering detection, flat-putt distance, slope curvature, off-green transition, stop, and bit-exact non-regression for non-putt shots.

**Out of scope:**
- Putt UI / aim line / green-reading helpers / gravity-well assist. All assist features are rendering layer (per architecture rule). Not this phase.
- Hole detection (ball-falls-in-cup geometry). Gameplay layer; sim just stops the ball at its resting position.
- Stimpmeter calibration as an explicit knob — for now, green stimp is implicit in `putt.csv` rolling-resistance value. Per-hole stimp variance is future work.
- Decoupling into a separate `PuttSimulation` class. Per the locked decision, only do that if Phase 5 logic exceeds ~50 LOC of branching inside `BallSimulation`. Architect Claude will review at Phase 5 closeout.
- Putt-specific spin (e.g. cut putts). Putts treat spin as zero — rolling ball, not flighted ball.
- Real-time difficulty modifiers from character stats. Stat coupling is `StatModifierResolver`'s job, not the sim's.

---

### Part A — Putt detection

Add a private static method to `BallSimulation`:

```csharp
/// <summary>
/// True when the input represents a putt: low launch (vy small relative to horizontal),
/// low total speed, and the ball is sitting on a putt-class surface
/// (Green, GreenCollar, or Tee). Tee included so practice-green test scenes
/// can putt from a tee marker without having to repaint the surface.
/// </summary>
private static bool IsPutt(ShotInput input, ISurfaceProvider surfaces)
{
    fp speedSq    = fpMath.Dot(input.velocity, input.velocity);
    fp maxSpeed   = fp.FromFloat(8.0f);          // 8 m/s ceiling — strongest realistic putt
    fp maxSpeedSq = maxSpeed * maxSpeed;
    if (speedSq > maxSpeedSq) return false;

    // Launch angle gate: vy² / |v|² <= sin²(15°) ≈ 0.067. Rearranged to avoid Sqrt.
    fp vySq = input.velocity.y * input.velocity.y;
    fp sin15Sq = fp.FromFloat(0.067f);
    if (vySq > speedSq * sin15Sq) return false;

    SurfaceType origin = surfaces.Classify(input.origin.x, input.origin.z);
    return origin == SurfaceType.Green
        || origin == SurfaceType.GreenCollar
        || origin == SurfaceType.Tee;
}
```

**Why these gates:** 8 m/s is a pro-tour lag putt's initial speed; 15° launch covers any realistic putter loft (3–4°) plus stance variation; surface gate prevents "wedge skull from the rough" being misclassified as a putt because someone hit it low. All three must be true.

The gates are deliberately conservative. False negatives (a real putt classified as a chip) just run through the full airborne path, which for a low slow shot collapses to the same roll behaviour anyway — slightly slower, no incorrectness. False positives (a chip classified as a putt) skip the airborne phase entirely and would visibly misbehave, so we err toward false negatives.

---

### Part B — Branch in the most-general overload

In the Phase 4 most-general overload (`Simulate(input, ground, aero, wind, surfaces, surfaceCfg)`), add the branch at the very top, before the airborne phase forwards to Phase 3:

```csharp
public static Trajectory Simulate(
    ShotInput input,
    IGroundProvider ground,
    AeroConfig aero,
    WindConfig wind,
    ISurfaceProvider surfaces,
    SurfaceConfig surfaceCfg)
{
    // Default putt config — callers wanting tuned values use the new overload below.
    return Simulate(input, ground, aero, wind, surfaces, surfaceCfg, PuttConfig.Default);
}

/// <summary>
/// Phase 5 entry. If the input qualifies as a putt, jump straight to a putt-tuned
/// roll integrator. Otherwise fall through to Phase 4 airborne+bounce+roll.
/// </summary>
public static Trajectory Simulate(
    ShotInput input,
    IGroundProvider ground,
    AeroConfig aero,
    WindConfig wind,
    ISurfaceProvider surfaces,
    SurfaceConfig surfaceCfg,
    PuttConfig puttCfg)
{
    if (IsPutt(input, surfaces))
    {
        var samples = new List<TrajectorySample>(capacity: 512);
        var hits    = new List<TerrainHit>();

        // Snap origin to terrain + ball radius so the integrator starts in contact.
        fp3 startPos = new fp3(
            input.origin.x,
            ground.SampleHeight(input.origin.x, input.origin.z) + aero.BallRadius,
            input.origin.z);
        // Project initial velocity onto the local tangent plane (drop any vy component).
        fp3 normal0 = (ground is HeightmapData hm)
            ? hm.SampleNormal(startPos.x, startPos.z)
            : new fp3(fp.Zero, fp.One, fp.Zero);
        fp3 startVel = input.velocity - normal0 * fpMath.Dot(input.velocity, normal0);

        samples.Add(new TrajectorySample(fp.Zero, startPos, startVel));

        return RunPuttPhase(startPos, startVel, fp.Zero,
                            ground, surfaces, surfaceCfg, puttCfg,
                            aero.BallRadius, samples, hits);
    }

    // ── Existing Phase 4 path (unchanged) ─────────────────────────────────────────
    // [the existing airborne + bounce + roll body moves here verbatim]
}
```

**Important:** the existing 6-arg overload's body moves into the new 7-arg overload. The 6-arg version becomes a thin forward to the 7-arg with `PuttConfig.Default`. This preserves the Phase 4 bit-exact gate test as long as `PuttConfig.Default` doesn't change putt-class surface coefficients vs `surfaceCfg` for non-putt shots — which it can't, because non-putt shots never enter the putt branch.

---

### Part C — `RunPuttPhase`

New private method on `BallSimulation`. Structurally near-identical to `RunRollPhase` but reads coefficients from `PuttConfig` for putt-class surfaces and falls back to `SurfaceConfig` (regular roll values) for non-putt-class surfaces — so a putt that runs off the back of the green transitions to fairway/rough resistance smoothly without a code-path swap.

```csharp
private static Trajectory RunPuttPhase(
    fp3 startPos, fp3 startVel, fp startT,
    IGroundProvider ground, ISurfaceProvider surfaces,
    SurfaceConfig surfaceCfg, PuttConfig puttCfg,
    fp ballRadius, List<TrajectorySample> samples, List<TerrainHit> hits)
{
    fp3 pos = startPos;
    fp3 vel = startVel;
    fp  t   = startT;

    fp3 gravity = new fp3(fp.Zero, Gravity, fp.Zero);

    int stopConsecutive = 0;
    const int StopStepsRequired = 10;
    fp prevSpeedSq = fp.Zero;

    int maxPuttSteps = 60 * 240;
    for (int step = 0; step < maxPuttSteps; step++)
    {
        SurfaceType surface = surfaces.Classify(pos.x, pos.z);

        // Water during a putt — ball drops into hazard.
        if (surface == SurfaceType.Water)
        {
            hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
            return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.HitWater, hits);
        }

        // Pick coefficients: putt-tuned for green family, regular roll values otherwise.
        SurfaceCoefficients coeff = IsPuttSurface(surface)
            ? puttCfg[surface]
            : surfaceCfg[surface];

        // Same physics as RunRollPhase from here.
        fp3 normal = (ground is HeightmapData hm)
            ? hm.SampleNormal(pos.x, pos.z)
            : new fp3(fp.Zero, fp.One, fp.Zero);

        vel = vel - normal * fpMath.Dot(vel, normal);
        fp3 aGravityTangent = gravity - normal * fpMath.Dot(gravity, normal);
        fp3 aResistance     = vel * (-coeff.RollingResistance);
        vel = vel + (aGravityTangent + aResistance) * Dt;

        fp3 posNext = new fp3(
            pos.x + vel.x * Dt,
            fp.Zero,
            pos.z + vel.z * Dt);
        posNext = new fp3(posNext.x,
            ground.SampleHeight(posNext.x, posNext.z) + ballRadius,
            posNext.z);

        t   = t + Dt;
        pos = posNext;
        samples.Add(new TrajectorySample(t, pos, vel));

        fp speedSq    = fpMath.Dot(vel, vel);
        fp stopThresh = coeff.StopSpeed * coeff.StopSpeed;
        if (speedSq < stopThresh && speedSq <= prevSpeedSq)
        {
            stopConsecutive++;
            if (stopConsecutive >= StopStepsRequired)
            {
                hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, surface, true));
                return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
            }
        }
        else stopConsecutive = 0;
        prevSpeedSq = speedSq;

        if (pos.x > WorldBound || pos.x < -WorldBound ||
            pos.z > WorldBound || pos.z < -WorldBound)
            return new Trajectory(samples, pos, vel, t, TerminationReason.ExitedWorldBounds, hits);
    }

    hits.Add(new TerrainHit(t, pos, vel, fp3.Zero, SurfaceType.Green, true));
    return new Trajectory(samples, pos, fp3.Zero, t, TerminationReason.BallStopped, hits);
}

private static bool IsPuttSurface(SurfaceType s)
    => s == SurfaceType.Green || s == SurfaceType.GreenCollar;
```

**Note on shared structure:** `RunRollPhase` and `RunPuttPhase` look ~85% identical. Resist the urge to extract a shared helper *during this phase* — they're going to diverge as we tune (e.g. putts may need a finer Dt, or a hole-detect probe). Phase 5 closeout will decide whether to refactor. Per Cesar's project rules: minimal diffs, no rewrites. Two near-identical methods is fine for now.

---

### Part D — `PuttConfig` and CSV loader

#### `Assets/Scripts/Physics/Core/PuttConfig.cs` — new

```csharp
using Golfin.Physics.Math;

namespace Golfin.Physics
{
    /// <summary>
    /// Putt-tuned coefficients. Indexed by SurfaceType, but only Green and GreenCollar
    /// are read by RunPuttPhase — other entries exist for hot-reload completeness and
    /// in case a future tuning pass lets putts engage on Tee/Fringe explicitly.
    /// </summary>
    public struct PuttConfig
    {
        public SurfaceCoefficients[] Coefficients;
        public SurfaceCoefficients this[SurfaceType t] => Coefficients[(int)t];

        public static PuttConfig Default
        {
            get
            {
                int n = System.Enum.GetValues(typeof(SurfaceType)).Length;
                var c = new SurfaceCoefficients[n];

                // Putts use Restitution=0 and TangentFriction=1 (no bouncing during a putt).
                // Only RollingResistance and StopSpeed matter inside RunPuttPhase.
                for (int i = 0; i < n; i++)
                    c[i] = new SurfaceCoefficients
                    {
                        Restitution       = fp.Zero,
                        TangentFriction   = fp.One,
                        RollingResistance = fp.FromFloat(0.20f),
                        StopSpeed         = fp.FromFloat(0.05f),
                    };

                // Green: faster than fairway-roll. ~Stimp 10 feel.
                c[(int)SurfaceType.Green] = new SurfaceCoefficients
                {
                    Restitution = fp.Zero, TangentFriction = fp.One,
                    RollingResistance = fp.FromFloat(0.10f),
                    StopSpeed         = fp.FromFloat(0.04f),
                };
                // GreenCollar: slightly slower than green.
                c[(int)SurfaceType.GreenCollar] = new SurfaceCoefficients
                {
                    Restitution = fp.Zero, TangentFriction = fp.One,
                    RollingResistance = fp.FromFloat(0.14f),
                    StopSpeed         = fp.FromFloat(0.05f),
                };
                return new PuttConfig { Coefficients = c };
            }
        }
    }
}
```

#### `Assets/Resources/Physics/putt.csv` — new

```csv
surface,rolling_resistance,stop_speed_mps,notes
Green,0.10,0.04,Stimp ~10 feel; canonical putting-green roll
GreenCollar,0.14,0.05,Slightly slower than green; same family
```

Only the surfaces a putt can plausibly engage with are listed; loader fills the rest from `PuttConfig.Default`. Keep the file small and obvious — the green is the only knob that really matters, and Cesar should be able to scan it in two seconds.

#### `PhysicsConfigLoader` — extend

Add `LoadPuttConfig()` matching the existing `LoadSurfaceConfig()` pattern. Missing file → `PuttConfig.Default`, missing rows → default for that surface, log warnings. Parse surface name as `Enum.TryParse<SurfaceType>(...)`.

---

### Part E — Tuning window

Add a "Putt" foldout to `PhysicsTuningWindow.cs`:

- Two rows: Green, GreenCollar — each with `RollingResistance` and `StopSpeed` sliders (Restitution and TangentFriction are forced to 0/1 in `RunPuttPhase`, so don't expose them).
- "Reload putt.csv" button.
- A "Sim 3m putt" button: builds a `ShotInput` with origin on a flat green, velocity = 1.85 m/s along +X (calibrated for ~3m on default Green). Runs the sim. Reports final stop distance from origin and final position. Quick sanity check while tuning.

Keep it functional. Nothing fancy.

---

### Part F — Tests

`Assets/Scripts/Physics/Tests/PuttTests.cs` — new. Namespace `Golfin.Physics.Tests`. **6 tests.**

1. **`Putt_Phase4Overloads_BitExact`** — run a non-putt 7-iron shot through the Phase 4 6-arg overload AND the new Phase 5 7-arg overload with `PuttConfig.Default`. Trajectories must be bit-exact identical. **Blocking gate** — proves Phase 5 didn't perturb the airborne path.

2. **`Putt_Detection_LowSlowOnGreen_IsPutt`** — build a `ShotInput` at origin on a stub `ConstantSurfaceProvider(Green)`, velocity = (2.0, 0, 0) m/s. Run the sim. Assert: trajectory has zero `TerrainHit` records of `IsStop=false` (no airborne bounce occurred — putt path was taken). Assert: termination is `BallStopped`. Assert: total samples > 50 (roll integrator ran for meaningful time, not a one-step stop).

3. **`Putt_Detection_FastFlightedShot_IsNotPutt`** — same Green stub, velocity = (40, 30, 0) m/s. Assert: trajectory has airborne samples (sample[100].position.y > 0.5m above ground at some point). Assert: at least one `TerrainHit` with `IsStop=false` was recorded. Confirms the gate didn't misfire.

4. **`Putt_FlatGreen_3m_StopsAtTarget`** — flat synthetic ground, `ConstantSurfaceProvider(Green)`, velocity calibrated to roll ~3m on default `PuttConfig`. Run sim. Assert: final position X is in [2.7m, 3.3m] from origin. Assert: final velocity magnitude < 0.05 m/s. Assert: termination is `BallStopped`. The calibration velocity is the one used by the tuning window's "Sim 3m putt" button; this test pins it.

5. **`Putt_SlopedGreen_CurvesDownhill`** — synthetic 5° heightmap tilted to +X (downhill in the direction the putt is rolling), `ConstantSurfaceProvider(Green)`. Same calibrated 3m putt velocity along +X. Assert: final X distance > 4.0m (downhill carries it further than flat). Then repeat with the heightmap rotated so slope is along +Z (cross-slope to a putt rolling along +X). Assert: |final.z - origin.z| > 0.3m (ball curved toward the low side). Magnitudes are loose — directional behaviour is what matters.

6. **`Putt_RunsOffGreenIntoFairway_TransitionsCleanly`** — synthetic surface provider that returns `Green` for `x < 5`, `Fairway` for `x ≥ 5`. Putt with strong velocity (3.5 m/s) along +X. Assert: trajectory continuous (every `samples[i+1].time - samples[i].time` ≈ Dt = 1/240s, no gaps). Assert: ball decelerates more sharply once `pos.x ≥ 5` (because `surfaceCfg.Fairway.RollingResistance` > `puttCfg.Green.RollingResistance`). Concretely: speed at the last sample where `x < 5` minus speed 0.5s later should be larger than 0.5 m/s drop.

All existing tests must still pass (Phase 1 = 4, Phase 2 = 3, Phase 2.1 = 8, Phase 3 = 6, Phase 4 = 8 → total 29). Phase 5 adds 6. **Target: 35 tests total, 35 pass.**

---

### Part G — Phase 5 test scene (deferred — manual QA)

Like Phase 4 Part G, this is non-blocking but recommended. Build `Assets/Scenes/Physics/Phase5_PuttTest.unity` on top of Hole 1 geometry. Add a controller with three buttons:

- "3m flat putt" — origin near the hole, velocity calibrated.
- "6m sloped putt" — origin further from the hole on a known slope on Hole 1's green; visualize the curve.
- "Putt off the back" — origin near the back fringe, hard putt, watch it transition off the green into rough or fringe.

LineRenderer for the trajectory, color-coded segments by surface type would be nice. Screenshots in the done report.

If time-pressed, defer the scene as Phase 4 Part G was deferred — tests cover correctness, scene is for feel.

---

### Part H — Unity-MCP autonomous validation

1. Compile clean. `console-get-logs` after each major change, max 5 iterations.
2. `tests-run` filter `Golfin.Physics.Tests`. All 35 pass.
3. `Putt_Phase4Overloads_BitExact` is the blocking gate; if it fails, stop and report — it means the putt branch leaked into non-putt paths.
4. If the Phase 5 test scene gets built, screenshot the 3m flat putt and the 6m sloped putt's curve.
5. Run "Sim 3m putt" via the tuning window's button (or `script-execute` if simpler). Report final stop distance.

### Done report

- 35-test pass/fail summary.
- Final 3m-putt stop distance with default `PuttConfig` (target: 2.7–3.3 m).
- 6m sloped-putt curve magnitude in m of lateral displacement (target: >0.3 m on a 5° cross-slope).
- Final `putt.csv` contents if any coefficients were tuned.
- Whether Part G test scene was built. If yes, screenshots.
- Any anomalies, deviations, or surprises.
- A one-line judgment: are the two `RunRollPhase` / `RunPuttPhase` methods diverging enough yet to justify a shared helper? (Architect Claude decides.)

### DO NOT

- Modify `RunRollPhase`. The Phase 4 bit-exact behaviour for non-putt shots must be preserved.
- Add a hole-detect / cup-fall probe. Gameplay layer.
- Tune Phase 4 `surfaces.csv` to compensate for putt feel. Putts use `putt.csv`, period.
- Use `UnityEngine.Random` or `System.Random` anywhere in Core.
- Apply spin during the putt phase. Putts are spin-zero by design here.
- Refactor `RunRollPhase` / `RunPuttPhase` into a shared helper during this phase. Phase 5 closeout decides; for now, two methods.
- Build Phase 6+ features (per-hole stimp variation, character-skill-modulated putt accuracy, putt-specific UI assist). All future work.

### Iteration budget

5 tuning iterations on `putt.csv` if the 3m-putt or sloped-putt tests miss tolerance. Beyond 5, report instead — we'll decide whether the tolerance is wrong or the model needs more than coefficient tuning.

<!-- END ARCHIVED PHASE 5 SPEC -->

---

<!-- BEGIN ARCHIVED PHASE 4 SPEC — for reference only; superseded by ✅ history entry below -->

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

<!-- END ARCHIVED PHASE 4 SPEC -->

---

## History Log (completed tasks, most recent first)

- ✅ **2026-04-22** Phase 5 Putt model — 35/35 tests pass (3.23s). `PuttConfig.cs` + `putt.csv` (Green 0.10/0.04, GreenCollar 0.14/0.05); `BallSimulation` 7-arg overload with `IsPutt` gate (speed<8m/s, angle<15°, surface∈{Green,GreenCollar,Tee}), `RunPuttPhase` integrator, `IsPuttSurface` for seamless off-green transition; `PhysicsConfigLoader.LoadPuttConfig()`; PhysicsTuningWindow Putt foldout with "Sim 3m putt" (v0=0.35→d≈3.1m, within [2.7,3.3]m). Bit-exact gate passes. Part G scene deferred (non-blocking). RunRollPhase/RunPuttPhase still ~85% identical — no shared helper yet; defer to Phase 6 review.
- ✅ **2026-04-21** Phase 4 Surface interaction (bounce + roll) — 29/29 tests pass. `HeightmapData`/`HeightmapLoader`/`HeightProvider`, `SurfaceType`/`ISurfaceProvider`/`SceneSurfaceProvider`/`SurfaceMarker`, `SurfaceConfig` + `surfaces.csv`, `TerrainHit` records + new `TerminationReason` values (`BallStopped`/`HitWater`/`MaxBouncesExceeded`), bounce loop with backspin Cr multiplier, `RunRollPhase` with speed²-based stop detection. Key fixes during impl: `UnityEngine.Physics` namespace qualification, per-surface `SurfaceConfig.Default`, one-sided boundary differences in `SampleNormal`. Part G test scene deferred (manual QA, non-blocking).
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
