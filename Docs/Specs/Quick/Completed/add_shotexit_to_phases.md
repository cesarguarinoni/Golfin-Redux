# Quick task — `add_shotexit_to_phases`

> Follow-up from `controls_c_fix` ARCHITECT_REVIEW.md (architect note #1, non-blocking).

## What

Close the diagnostic-logger gap in `BallSimulation.cs` so that `[ShotExit]` is emitted on every termination path, including putt and roll phases. Today it only fires at the bounce-loop exit points; `RunPuttPhase` and `RunRollPhase` exits silently end the sim.

## Why

`controls_c_fix` lab validation surfaced this: the spec assumed `[ShotExit]` would log on every termination. It didn't, because `DiagShotLogger` is only called in the airborne/bounce path (BallSimulation.cs lines 184, 222, 234, 275, 310, 321). Putt/roll exits at lines 556, 571, 693, 705 bypass it. Fix once so future physics specs can rely on `[ShotExit]` as a universal termination marker.

## Where to add

In `Assets/Scripts/Physics/Core/BallSimulation.cs`, four locations. All four sites share the same payload shape — copy the format from line 274–277 (the existing `BallStopped` exit in `SimulateAirborne`) and substitute the local variables.

| Site | Line (current) | Phase | Surface arg | Termination |
|---|---|---|---|---|
| `RunRollPhase` early stop-streak exit | 556 | roll | `surface` | `TerminationReason.BallStopped` |
| `RunRollPhase` step-loop fallthrough | 571 | roll | `SurfaceType.Fairway` (matches existing TerrainHit) | `TerminationReason.BallStopped` |
| `RunPuttPhase` early stop-streak exit | 693 | putt | `surface` | `TerminationReason.BallStopped` |
| `RunPuttPhase` step-loop fallthrough | 705 | putt | `SurfaceType.Green` (matches existing TerrainHit) | `TerminationReason.BallStopped` |

Wrap each call in `#if UNITY_EDITOR` to match the existing pattern. Place it on the line immediately before the `return new Trajectory(...)`.

Template (adapt variable names to local scope):

```csharp
#if UNITY_EDITOR
if (DiagShotLogger != null)
    DiagShotLogger(
        $"[ShotExit] termination={TerminationReason.BallStopped} " +
        $"finalPos=({pos.x.ToFloat():F2},{pos.y.ToFloat():F2},{pos.z.ToFloat():F2}) " +
        $"finalT={t.ToFloat():F2}s samples={samples.Count} hits={hits.Count}");
#endif
```

Note `samples` and `hits` are the local list names in `RunRollPhase` and `RunPuttPhase` (not `samplesList`/`hitsList` like in `SimulateAirborne`). Verify the names from the surrounding code before each insertion.

## Acceptance

1. All 4 sites have the `[ShotExit]` log emission, gated on `#if UNITY_EDITOR` and `DiagShotLogger != null`.
2. Project compiles cleanly. No new warnings.
3. EditMode tests still 203/203 PASS (bit-exact gate must hold — log emission is `#if UNITY_EDITOR` so it's editor-only and shouldn't shift fp math).
4. PhysicsLab Hole1 — fire any putter shot, confirm one `[ShotExit] termination=BallStopped` line in Console after the ball stops. Fire any non-putt-gate shot that ends with roll (Driver on a flat surface), confirm one `[ShotExit] termination=BallStopped` line after roll-out.

## Out of scope

- Don't change anything else in BallSimulation.
- Don't change the C.1+C.2 stop-check tolerance window — that's the load-bearing fix from `controls_c_fix`.
- Don't refactor the existing `[ShotExit]` call sites for consistency. Just add the four new ones.
