# SPEC — `loop_v1_2a_ball_state_machine`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-06 07:30 JST.

## Goal

Centralize the ball lifecycle into a deterministic, headless-capable state machine that downstream Loop v1 consumers (camera §2b, turn counter §2c, result screen §2d, next-shot handoff §2e) all subscribe to, instead of each re-deriving "is the ball at rest?" independently. The canonical state sequence is `Aiming → Flying → Rolling → AtRest → InCup | OB`. Layer 1 physics (`BallSimulation`, `Trajectory`, `TrajectorySample`) is **not modified** — this is an additive observer/orchestrator on top of an already-deterministic batch sim.

## Reference

- **Architect NOTES:** `Docs/Specs/Active/loop_v1_2a_ball_state_machine/NOTES.md` (carries pre-spec analysis + the seven open questions Cesar locked).
- **Roadmap entry:** `Docs/Roadmap.md` §2a.
- **No Figma / image references** — this task is logic-only, no UI surface.

## Background — what exists today

Verified by code walk 2026-05-06:

| File | Role for this task |
|---|---|
| `Assets/Scripts/Physics/Core/BallSimulation.cs` | Static batch sim. `Simulate(...)` returns one full `Trajectory` per shot at internal 240 Hz. **Do not modify.** |
| `Assets/Scripts/Physics/Core/Trajectory.cs` | Output: `samples[]`, `terrainHits[]` (each with `IsStop`), `finalPosition`, `finalVelocity`, `finalTime`, `termination` ∈ `{HitGround, BallStopped, HitWater, HitOOB, ExitedWorldBounds, MaxDurationReached, MaxBouncesExceeded}`. **Do not modify.** |
| `Assets/Scripts/Physics/Core/SurfaceType.cs` | Enum with `Fairway, Green, GreenCollar, Semirough, Rough, Tee, Sand, BunkerLip, CartPath, Water, OOB`. No `Cup` entry — confirmed. |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Existing input SM. Fires `event Action<ShotInput, BallPhysicsModifiers> OnShotResolved` at flick commit. `CompleteShot()` re-arms. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:649` | Today's flow: subscribes to `OnShotResolved` → calls `BallSimulation.Simulate` → `trajectoryRenderer.Draw(traj)` + `ballAnimator.Play(traj)` → fires `OnShotFired(ShotReadout)`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::HandleCameraOrbit` | Today's at-rest signal: `_prevBallPlaying && !ballAnimator.IsPlaying` → `_shotController.CompleteShot()`. **This block is what §2a centralizes.** |
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | Plays back `Trajectory.samples` over wall-clock time scaled by `PlayRate`. `IsPlaying` flips false in `SnapToEnd()` when `_currentSimTime >= endTime`. **No completion event — consumer must poll `IsPlaying`.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` | Has `PinWorld` (Vector3, populated from scene `Flag` GO in `OnHoleLoaded`). Will feed §2d's real cup detector — not §2a. |
| `Assets/Scripts/Physics/Runtime/Baked/ZoneData.cs` | Baked zones + OB mask. **No cup position baked today** — confirmed. Cup-related geometry is genuinely new for §2d. |

## Locked decisions (carry forward from NOTES.md)

- **L1.** State enum, not state classes.
- **L2.** SM is owned by `PhysicsLabController`. SM does NOT own `BallAnimator` / `TrajectoryRenderer` / `BallSimulation`.
- **L3.** OB is one state with an `OBReason` payload (`Water`, `OutOfBounds`, `ExitedWorldBounds`).
- **L4.** Flying/Rolling derived from `Trajectory.terrainHits[]` (first non-stop hit = Flying→Rolling; subsequent non-stop hits = bounce, brief Flying again). No changes to `BallSimulation` or `TrajectorySample`.
- **L5.** InCup state is reserved with a stub detector. Real detection lands in §2d.
- **Q1a.** New `Golfin.Gameplay.Loop` asmdef + namespace.
- **Q2b.** Both events: fine-grained `OnStateChanged(BallStateChange)` + coarse one-shot `OnShotComplete(ShotResult)`.
- **Q3a.** Ball-SM "Aiming" = "no shot in flight". SM does not mirror `ShotController` sub-states.
- **Q4a.** `ICupDetector` interface; `NullCupDetector` default returns false.
- **Q5b.** Cup detection scans `Trajectory.samples` once after sim returns, before `BallAnimator.Play` starts.
- **Q6a.** `Headless` flag walks the lifecycle synchronously without touching the animator.
- **Q7a.** SM emits `AtRest`/`InCup`/`OB` event; `PhysicsLabController` re-arms `ShotController` from the subscriber side.

## Architecture context

- **New asmdef:** `Golfin.Gameplay.Loop` at `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef`, namespace `Golfin.Gameplay.Loop`. References: `Golfin.Physics.Core`, `Golfin.Physics.Math`, `Golfin.Gameplay.Input`. `autoReferenced: true`.
- **Asmdef updated:** `Golfin.Physics.Viewer.asmdef` adds `Golfin.Gameplay.Loop` to its references list (so `PhysicsLabController` can hold the SM).
- **Asmdef updated:** `Golfin.Gameplay.Tests.asmdef` adds `Golfin.Gameplay.Loop` to its references list (so EditMode tests can drive the SM).
- **No changes to `Golfin.Physics.Core` / `Golfin.Physics.Stats` / `Golfin.Physics.Runtime` / `Golfin.Gameplay.Input` / `Golfin.Gameplay.UI`.**

## Implementation

### A. State enum + transitions

```csharp
namespace Golfin.Gameplay.Loop
{
    public enum BallState
    {
        Aiming,   // no shot in flight; player can input
        Flying,   // ball is airborne (post-flick, pre-first-ground-contact, AND between bounces)
        Rolling,  // ball is on ground in roll/putt phase
        AtRest,   // ball stopped on a non-OB surface, not in cup
        InCup,    // ball ended inside the cup geometry (per ICupDetector)
        OB,       // ball ended in water / OOB / off the world
    }

    public enum OBReason
    {
        Water,
        OutOfBounds,
        ExitedWorldBounds,
    }
}
```

**Canonical transition table** (each row = legal transition):

| From | To | Condition |
|---|---|---|
| Aiming | Flying | `OnShotResolved` fires |
| Flying | Rolling | `terrainHits[i].IsStop == false` AND velocity normal-component dropped below roll threshold (already encoded in trajectory: presence of a non-stop hit followed by roll-phase samples means roll began) |
| Rolling | Flying | next `terrainHits[i].IsStop == false` (bounce mid-roll — rare but possible if ball re-launches off uneven terrain) |
| Flying | AtRest | trajectory.termination ∈ `{BallStopped, MaxDurationReached, MaxBouncesExceeded}` AND cup-scan returned no in-cup sample AND final-surface ∉ `{Water, OOB}` |
| Rolling | AtRest | (same as above) |
| Flying | InCup | cup-scan found at least one in-cup sample (state enters at the cup-scan timestamp) |
| Rolling | InCup | (same) |
| Flying | OB | trajectory.termination ∈ `{HitWater, HitOOB, ExitedWorldBounds}`; OBReason follows the termination |
| Rolling | OB | (same) |
| AtRest | Aiming | external re-arm signal (`PhysicsLabController` calls `BallStateMachine.ReArm()` after handling AtRest) |
| InCup | Aiming | external re-arm only fires after §2d's hole-complete handler clears it (out of scope here; from §2a's POV, InCup is terminal until ReArm) |
| OB | Aiming | external re-arm only fires after §2e's penalty handler runs (out of scope here; terminal until ReArm) |

**Illegal transitions** (must throw or assert in tests): `Aiming → Rolling`, `Aiming → AtRest`, `Aiming → InCup`, `Aiming → OB`, any `→ Aiming` not via `ReArm()`.

### B. Payload structs

```csharp
namespace Golfin.Gameplay.Loop
{
    using Golfin.Physics;
    using Golfin.Physics.Math;

    public readonly struct BallStateChange
    {
        public readonly BallState Previous;
        public readonly BallState Next;
        public readonly fp3       Position;      // ball position at the transition
        public readonly SurfaceType Surface;     // surface under the ball at the transition
        public readonly OBReason? OBReason;      // populated only when Next == OB
        public readonly fp        SimTime;       // trajectory time at transition (fp.Zero for Aiming)
    }

    public readonly struct ShotResult
    {
        public readonly BallState  TerminalState;   // AtRest, InCup, or OB
        public readonly OBReason?  OBReason;        // populated only when TerminalState == OB
        public readonly fp3        StartPosition;   // origin of the shot
        public readonly fp3        EndPosition;     // resting position (or in-cup position)
        public readonly SurfaceType StartSurface;
        public readonly SurfaceType EndSurface;
        public readonly fp         SimDuration;
        public readonly int        BounceCount;     // number of non-stop terrainHits before terminal
    }
}
```

### C. Cup detector seam

```csharp
namespace Golfin.Gameplay.Loop
{
    using Golfin.Physics.Math;

    public interface ICupDetector
    {
        /// <summary>
        /// Returns true if the given world position lies inside the cup geometry.
        /// MUST be deterministic and side-effect free — called from sim-time scans.
        /// </summary>
        bool IsInCup(fp3 position, fp ballRadius);
    }

    public sealed class NullCupDetector : ICupDetector
    {
        public bool IsInCup(fp3 position, fp ballRadius) => false;
    }
}
```

### D. Driver class

```csharp
namespace Golfin.Gameplay.Loop
{
    using System;
    using Golfin.Physics;
    using Golfin.Physics.Math;
    using Golfin.Gameplay.Input;

    public sealed class BallStateMachine
    {
        public BallState State { get; private set; } = BallState.Aiming;
        public bool      Headless { get; set; } = false;

        public event Action<BallStateChange> OnStateChanged;
        public event Action<ShotResult>      OnShotComplete;

        ICupDetector _cupDetector = new NullCupDetector();
        ISurfaceProvider _surfaces;   // injected for surface-at-position lookups

        public BallStateMachine(ISurfaceProvider surfaces, ICupDetector cupDetector = null)
        {
            _surfaces    = surfaces ?? throw new ArgumentNullException(nameof(surfaces));
            _cupDetector = cupDetector ?? new NullCupDetector();
        }

        /// <summary>Swap the cup detector at runtime (e.g. when a hole loads).</summary>
        public void SetCupDetector(ICupDetector cupDetector)
            => _cupDetector = cupDetector ?? new NullCupDetector();

        /// <summary>Swap the surface provider at runtime (hole load/unload).</summary>
        public void SetSurfaceProvider(ISurfaceProvider surfaces)
            => _surfaces = surfaces ?? throw new ArgumentNullException(nameof(surfaces));

        /// <summary>
        /// Called by PhysicsLabController immediately after BallSimulation.Simulate() returns,
        /// BEFORE BallAnimator.Play() is invoked. Pre-computes the canonical transition list
        /// from the trajectory + cup scan and stores it for live polling.
        /// </summary>
        public void OnTrajectoryComputed(fp3 startPos, Trajectory trajectory) { ... }

        /// <summary>
        /// Called once per Update tick by the owner. Inspects the polled animator state and
        /// fires queued transitions when the animator finishes (non-headless path).
        /// </summary>
        public void Tick(bool animatorIsPlaying) { ... }

        /// <summary>
        /// Forces transition back to Aiming. Called by the owner after handling AtRest/OB,
        /// or by §2d after handling InCup.
        /// </summary>
        public void ReArm() { ... }
    }
}
```

### E. Lifecycle: non-headless (live playback) case

This is the default path used in the lab and (later) in real gameplay.

1. Player flicks → `ShotController.OnShotResolved(input, ballMods)` fires.
2. `PhysicsLabController.HandleShotResolved` (today's existing handler) computes `correctedInput` and calls `BallSimulation.Simulate(...)` → returns `trajectory`.
3. **NEW:** Before `ballAnimator.Play(trajectory)`, the handler calls `_ballSM.OnTrajectoryComputed(startPos, trajectory)`. Inside this method the SM:
   a. Builds an internal `_pendingTransitions: List<BallStateChange>` from the trajectory:
      - First entry: `Aiming → Flying` at `simTime = fp.Zero`, `position = startPos`, `surface = surfaces.Classify(startPos.x, startPos.z)`, `OBReason = null`.
      - For each `terrainHits[i]` with `IsStop == false`: emit `Flying → Rolling` at `time = hit.Time`, `position = hit.Position`, `surface = hit.Surface`. (Successive bounces produce alternating Flying→Rolling/Rolling→Flying entries — list captures the canonical sequence.)
      - Determine terminal state:
        - If `termination == HitWater` → terminal = `OB`, reason = `Water`.
        - If `termination == HitOOB` → terminal = `OB`, reason = `OutOfBounds`.
        - If `termination == ExitedWorldBounds` → terminal = `OB`, reason = `ExitedWorldBounds`.
        - Else, scan `trajectory.samples` for the first sample where `_cupDetector.IsInCup(sample.position, AeroConfig.BallRadius)` returns true. **Note:** `BallRadius` lookup — the SM does not have an `AeroConfig` reference; pass `ballRadius` as a parameter to `OnTrajectoryComputed(startPos, trajectory, ballRadius)` so the SM stays config-free.
        - If a cup-sample exists → terminal = `InCup`, reason = null, position = that sample's position, simTime = that sample's time.
        - Else → terminal = `AtRest`, reason = null, position = `trajectory.finalPosition`, surface = surface at finalPosition.
      - Append the terminal transition (e.g. `Rolling → AtRest`) at the appropriate time.
   b. Fires `OnStateChanged` for the FIRST transition only (`Aiming → Flying`). The remaining transitions are queued in `_pendingTransitions`.
   c. Updates `State = BallState.Flying`.
4. Owner calls `ballAnimator.Play(trajectory)`.
5. Owner calls `_ballSM.Tick(animatorIsPlaying)` once per `Update()`.
6. Inside `Tick`: when `animatorIsPlaying` falling edge is detected (was true last call, now false), the SM **drains all remaining queued transitions synchronously** (they fire in order, microseconds apart) — `OnStateChanged` fires for each, then `OnShotComplete(shotResult)` fires exactly once with the terminal payload, and `State` lands on the terminal state. This bunches Flying↔Rolling/Rolling↔Flying flicker at the end of playback rather than time-spreading them. **Rationale:** v1's only consumers are camera (wants Flight-mode during playback / Rest-mode after — coarse), turn counter (wants one increment per shot), result screen (wants one terminal payload), re-arm (wants AtRest signal). None need per-bounce timing during playback; the canonical state SEQUENCE is preserved, just compressed at the end.
7. Subscribers handle `OnShotComplete` and (in the case of `AtRest`) call `_ballSM.ReArm()` followed by `_shotController.CompleteShot()`.

### F. Lifecycle: headless case

Used by foundation-#5 bot pools — same logic, no animator.

1. Owner calls `_ballSM.OnTrajectoryComputed(startPos, trajectory, ballRadius)`. SM's `Headless == true`.
2. Because `Headless == true`, after queuing `_pendingTransitions` the SM **drains the entire list synchronously inside `OnTrajectoryComputed`**, firing `OnStateChanged` for every transition and `OnShotComplete` at the end. `State` lands on the terminal state immediately.
3. Owner does not need to call `Tick`. (Calling `Tick` in headless mode is a no-op.)

The terminal `ShotResult` MUST be byte-equal between headless and non-headless paths for the same trajectory. This is the determinism gate.

### G. Determinism rules

- The SM body MUST NOT call `Time.deltaTime`, `Time.unscaledDeltaTime`, `Random.value`, `DateTime.Now`, or any Unity API that varies per platform / per frame. The only Unity-side input is the `bool animatorIsPlaying` flag passed into `Tick()` — and that's only used to detect the falling edge that drains pending transitions.
- The SM MUST NOT modify the input `Trajectory` or any of its lists.
- All payload structs are immutable readonly structs.
- Cup-scan iteration order is the natural order of `trajectory.samples` (already deterministic).

### H. Integration: `PhysicsLabController` changes

Replace the at-rest detection block in `HandleCameraOrbit` and add SM ownership.

**1. Add field** (near other private state fields, line ~78):

```csharp
Golfin.Gameplay.Loop.BallStateMachine _ballSM;
```

**2. In `Awake()` (line ~81), AFTER `EnsureConfigsLoaded()`:**

```csharp
_ballSM = new Golfin.Gameplay.Loop.BallStateMachine(BuildSurfaceProvider(default(ShotPreset)));
_ballSM.OnShotComplete += HandleShotComplete;
```

**3. In `OnDestroy()` (line ~147), AFTER existing unsubscribes:**

```csharp
if (_ballSM != null) _ballSM.OnShotComplete -= HandleShotComplete;
```

**4. In `HandleShotResolved` (line ~649), modify around the `RunSimFromController` call:**

```csharp
var trajectory = RunSimFromController(correctedInput, ballMods);
_previousTrajectory = trajectory;

// NEW: feed the SM before playback starts
_ballSM.OnTrajectoryComputed(correctedInput.origin, trajectory, AeroCfg.BallRadius);

trajectoryRenderer.Draw(trajectory);
ballAnimator.Play(trajectory);
// ... rest unchanged
```

**5. In `OnHoleLoaded` (line ~~~ where surface providers are wired), after `TryLoadBakedProviders`:**

```csharp
if (_ballSM != null)
    _ballSM.SetSurfaceProvider(BuildSurfaceProvider(default(ShotPreset)));
```

**6. In `OnHoleUnloaded`, after the existing reset block:**

```csharp
if (_ballSM != null)
    _ballSM.SetSurfaceProvider(BuildSurfaceProvider(default(ShotPreset)));
```

**7. Add `Tick()` call to `Update()` (line ~196, currently `void Update() => HandleCameraOrbit();`):**

```csharp
void Update()
{
    bool isPlaying = ballAnimator != null && ballAnimator.IsPlaying;
    _ballSM?.Tick(isPlaying);
    HandleCameraOrbit();
}
```

**8. Modify `HandleCameraOrbit` to remove the inline at-rest signal.** The block at line ~~that does `if (_prevBallPlaying && !isPlaying) { ... _shotController?.CompleteShot(); }` is removed; the camera-target reset (`_orbitCenter = ballAnimator.CurrentBall.position; chaseCamera.SetTarget(null);`) moves into the new `HandleShotComplete` handler. `_prevBallPlaying` field is removed.

**9. Add new method:**

```csharp
void HandleShotComplete(Golfin.Gameplay.Loop.ShotResult result)
{
    // Reset camera target (was inline in HandleCameraOrbit before §2a)
    if (ballAnimator?.CurrentBall != null)
        _orbitCenter = ballAnimator.CurrentBall.position;
    if (chaseCamera != null) chaseCamera.SetTarget(null);

    // Re-arm shot controller for the next shot. §2d will gate this on
    // result.TerminalState == AtRest later; for §2a, all three terminal
    // states re-arm (lab is happy to fire from in-cup or OB positions
    // since there's no result screen yet).
    _shotController?.CompleteShot();
    _ballSM.ReArm();
}
```

**Order of operations check:** `Update()` runs `Tick(animatorIsPlaying)` BEFORE `HandleCameraOrbit()`. On the frame the animator finishes, `Tick` drains the SM, fires `OnShotComplete` synchronously, `HandleShotComplete` runs (re-arming controller + clearing target), and `HandleCameraOrbit` then sees `_shotController.IsExternalDragActive == false` and a null `chaseCamera.SetTarget`, identical to today's behavior. No frame ordering regression.

### I. Tests — `Golfin.Gameplay.Tests.asmdef`

Tests live at `Assets/Scripts/Gameplay/Tests/BallStateMachineTests.cs`. All EditMode, no PlayMode dependency. Use synthetic `Trajectory` objects (constructed by hand with `List<TrajectorySample>` and `List<TerrainHit>`) to drive the SM — no `BallSimulation.Simulate` call needed in tests, keeping them fast and deterministic.

**Required test cases:**

1. `Aiming_IsInitialState` — fresh SM has `State == BallState.Aiming`.
2. `OnTrajectoryComputed_FromAiming_TransitionsToFlying` — `OnStateChanged` fires once with `(Aiming → Flying)`, `State == Flying`. `OnShotComplete` does NOT fire.
3. `Flying_IsPlayingFalse_DrainsToAtRest` — synthetic trajectory with `BallStopped` terminating on Fairway; after `OnTrajectoryComputed` then `Tick(true)` then `Tick(false)`, the event sequence is `Flying → Rolling → AtRest` and `OnShotComplete` fires once with `TerminalState == AtRest`.
4. `Flying_HitWater_TerminalIsOBWater` — synthetic trajectory with `HitWater` termination → `OnShotComplete` fires with `TerminalState == OB`, `OBReason == Water`.
5. `Flying_HitOOB_TerminalIsOBOutOfBounds` — same as above with `HitOOB` → `OBReason.OutOfBounds`.
6. `Flying_ExitedWorldBounds_TerminalIsOBExited` — `ExitedWorldBounds` → `OBReason.ExitedWorldBounds`.
7. `CupDetector_PositiveScan_TerminalIsInCup` — inject a stub `ICupDetector` that returns true for one specific sample position; trajectory's that-sample-then-roll-on results in `TerminalState == InCup`, `EndPosition == that sample's position`.
8. `MultipleBounces_StateSequencePreserved` — synthetic trajectory with three bounces (three non-stop hits) followed by `BallStopped`; expected sequence: `Flying → Rolling → Flying → Rolling → Flying → Rolling → AtRest`.
9. `Headless_FiresAllTransitionsSynchronously` — same trajectory as #3 with `Headless = true`; all events fire inside `OnTrajectoryComputed`, no `Tick` calls needed, terminal state matches non-headless path byte-for-byte (`ShotResult` field-by-field equality).
10. `ReArm_FromAtRest_ReturnsToAiming` — after AtRest, `ReArm()` transitions to `Aiming`, fires `OnStateChanged(AtRest → Aiming)`.
11. `ReArm_FromInCup_ReturnsToAiming` — same from InCup.
12. `ReArm_FromOB_ReturnsToAiming` — same from OB.
13. `Determinism_SameTrajectoryTwice_IdenticalEventSequence` — fire the same synthetic trajectory through two SM instances; collect all `OnStateChanged` payloads as lists; assert byte-equal.
14. `IllegalTransition_AimingToRolling_Throws` — calling a hypothetical internal force-transition method (or feeding a malformed pre-Flying state to Tick) raises `InvalidOperationException`. Implementer's call: if no internal API exposes this, document the negative-test omission and explain why it's structurally impossible.
15. `NullSurfaceProvider_Throws` — `new BallStateMachine(null)` throws `ArgumentNullException`.
16. `NullCupDetector_FallsBackToNullDetector` — `new BallStateMachine(surfaces, null)` succeeds; SM uses `NullCupDetector` (test by firing a trajectory that would be in-cup if a real detector existed and asserting terminal state is AtRest).

**Existing test gate must hold:** the full test suite (currently 211/211 PASS / 0 IGNORED per `controls_f` close-out 2026-05-06) MUST still be 211 PASS + N new SM tests, 0 IGNORED, after this lands. No regressions.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured.

- [ ] New asmdef `Golfin.Gameplay.Loop` exists at `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef` with the exact references list specified.
- [ ] `BallStateMachine.cs`, `BallState.cs`, `OBReason.cs`, `BallStateChange.cs`, `ShotResult.cs`, `ICupDetector.cs`, `NullCupDetector.cs` all exist under `Assets/Scripts/Gameplay/Loop/`.
- [ ] `Golfin.Physics.Viewer.asmdef` references `Golfin.Gameplay.Loop`.
- [ ] `Golfin.Gameplay.Tests.asmdef` references `Golfin.Gameplay.Loop`.
- [ ] `PhysicsLabController.cs` changes #1–#9 in section H above all applied verbatim (or deviation flagged with justification at bottom of report).
- [ ] All 16 EditMode tests in section I are written and PASS.
- [ ] Pre-existing 211/211 test gate still holds. Total = `211 + N_new`, IGNORED = 0.
- [ ] No `Time.deltaTime` / `Time.unscaledDeltaTime` / `Random.*` / `DateTime.Now` references inside `BallStateMachine.cs` (grep confirmed in report).
- [ ] No modifications to `BallSimulation.cs`, `Trajectory.cs`, `TrajectorySample`, `TerrainHit`, `SurfaceType.cs`, `ShotController.cs`, or any `Golfin.Physics.Core` / `Golfin.Physics.Stats` / `Golfin.Gameplay.Input` source.
- [ ] Lab smoke test: open `LabScaffold.unity` + load `Hole_01_Geo` additively, fire 3 shots (driver from tee, 7-iron, putter from green). Each shot's lifecycle:
  - first frame after flick: `OnStateChanged(Aiming → Flying)` logged,
  - frame after `BallAnimator.IsPlaying` flips false: full sequence drains, `OnShotComplete` logged with correct `TerminalState`,
  - `ShotController` re-arms (next flick is accepted),
  - no errors in Console.
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

**New files:**

- `Assets/Scripts/Gameplay/Loop/Golfin.Gameplay.Loop.asmdef`
- `Assets/Scripts/Gameplay/Loop/BallState.cs`
- `Assets/Scripts/Gameplay/Loop/OBReason.cs`
- `Assets/Scripts/Gameplay/Loop/BallStateChange.cs`
- `Assets/Scripts/Gameplay/Loop/ShotResult.cs`
- `Assets/Scripts/Gameplay/Loop/ICupDetector.cs`
- `Assets/Scripts/Gameplay/Loop/NullCupDetector.cs`
- `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs`
- `Assets/Scripts/Gameplay/Tests/BallStateMachineTests.cs`

**Modified files:**

- `Assets/Scripts/Physics/Viewer/Golfin.Physics.Viewer.asmdef` — add `Golfin.Gameplay.Loop` reference.
- `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` — add `Golfin.Gameplay.Loop` reference.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — sections H1–H9 above.

**Untouched (verify in report):**

- `Assets/Scripts/Physics/Core/*` — no edits.
- `Assets/Scripts/Physics/Stats/*` — no edits.
- `Assets/Scripts/Physics/Runtime/*` — no edits.
- `Assets/Scripts/Gameplay/Input/*` — no edits.
- `Assets/Scripts/Gameplay/UI/**` — no edits.

## Out of scope (do NOT do these)

- **No camera mode work.** Camera transitions are §2b. The SM EXPOSES events; §2b CONSUMES them in a separate spec.
- **No turn counter / shot history.** §2c.
- **No result screen UI.** §2d (Cesar has Figma + image exports already).
- **No real cup detection.** §2a uses `NullCupDetector`. Real cup geometry + detector lands in §2d.
- **No ball trail / VFX / cinematic polish.** Roadmap §10.
- **No `BallSimulation` / `Trajectory` / `TrajectorySample` modifications.** Layer 1 sanctity rule.
- **No new `SurfaceType` enum entries.** Cup is a position-test, not a surface.
- **No changes to `ShotController` internals.** SM observes `OnShotResolved` and is re-armed externally via `CompleteShot()`.
- **No persistence / save layer.** §3e (Loop v2).
- **No bot logic.** The `Headless` flag is provided so future bot work can use it; no bot caller is wired in this task.
- **No removal of `TrajectoryRenderer` / `PuttPathPredictor` / `ShotConeView` from the lab.** Decision about which become gameplay aim-assists vs hide-by-default is a separate Loop v1 question (Cesar's call), not in §2a.

## Open questions for the Implementer

If anything in this spec is unclear, the implementer SHOULD escalate via `STATUS = ARCHITECT_REVIEW_ESCALATE` rather than guess. The spec author (Architect) is reachable for clarification through Cesar.

Anticipated likely escalations:

- Exact file path for tests if `Assets/Scripts/Gameplay/Tests/` already has a structure the new file should match (e.g. one-test-class-per-folder convention) — implementer can pick the closest match and document the choice in the report.
- `AeroConfig.BallRadius` lookup if the field name differs from what's referenced in section H step 4 — implementer should grep `Golfin.Physics.Core/AeroConfig.cs` for the actual property name and use that.
- Whether `BuildSurfaceProvider(default(ShotPreset))` returns a sensible default before a hole is loaded — if it returns `null` or a degenerate provider, the SM constructor throws on null. Implementer should follow the existing `PhysicsLabController` initialization order (the same provider is built in `OnHoleLoaded` and `Awake`); if the Awake-time build fails, defer SM construction to `OnHoleLoaded`/`SetupAtTee` and document the choice.
