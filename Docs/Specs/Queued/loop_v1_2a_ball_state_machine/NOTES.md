# Loop v1 §2a — Ball State Machine — Architect NOTES

**Status:** PRE_SPEC — awaiting Cesar lock on open questions.
**Architect (claude.ai), 2026-05-06 07:00 JST**

Roadmap §2a: `Aiming → Flying → Rolling → AtRest → InCup | OB`, with hooks for §2b camera transitions + §2c turn counter + §2d hole-complete + §2e next-shot handoff.

---

## 1. Code walk — what exists today

| File | Role |
|---|---|
| `Assets/Scripts/Physics/Core/BallSimulation.cs` | Static, **batch-mode** sim. `Simulate(input, ground, aero, wind, surfaces, surfaceCfg, puttCfg, ballMods)` runs the entire shot at internal 240 Hz and returns one `Trajectory`. No per-frame tick. |
| `Assets/Scripts/Physics/Core/Trajectory.cs` | Output: `samples[]` (time/pos/vel), `terrainHits[]` (bounces + final stop with `IsStop` flag), `finalPosition`, `finalTime`, `termination` ∈ {`HitGround`, `BallStopped`, `HitWater`, `HitOOB`, `ExitedWorldBounds`, `MaxDurationReached`, `MaxBouncesExceeded`}. No `HitCup`. |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | Existing input state machine: `Idle → Aiming → Pulling → Timing → Flicking → Resolving`. Fires `OnShotResolved(input, ballMods)` at flick commit. `CompleteShot()` re-arms to Idle. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:649` | Subscribes to `OnShotResolved`, calls `BallSimulation.Simulate`, hands trajectory to `trajectoryRenderer.Draw` + `ballAnimator.Play`, fires `OnShotFired(ShotReadout)`. |
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | Plays back `Trajectory.samples` over time; `IsPlaying` flips false at end. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs::HandleCameraOrbit` | **Today's at-rest signal**: `_prevBallPlaying && !ballAnimator.IsPlaying` → `_shotController.CompleteShot()`. This is the implicit, scattered version of what §2a will centralize. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/{Player,Hole,Wind,Ball,Club,ShotMode,Spin,GameSession}Context.cs` | Static-bus UI contexts in `Golfin.Gameplay.UI.HUD`. **HUD-shaped (display data), not gameplay-state.** Ball state machine does NOT belong here. |

## 2. Mental model adjustment

The state machine is **not** wrapping per-frame physics — it's wrapping **shot lifecycle / playback orchestration** on top of an already-deterministic batch sim.

Implications for the architectural foundations baked in §2a:
- **Replay determinism (foundation #3):** already trivially achieved — same `(seed, ShotInput, ballMods, configs)` ⇒ bit-exact `Trajectory`. SM must NOT introduce non-determinism (no `Time.deltaTime` reads inside transitions other than playback timing).
- **Headless mode for bots (foundation #5):** `BallSimulation.Simulate()` already runs without visuals. SM core must be runnable without `BallAnimator` (a "skip playback, fire AtRest immediately" path is needed for bot-pool sims).
- **Event bus (foundation #4):** SM is a natural event source — `OnStateChanged`, `OnShotComplete` — multiple listeners (camera, turn counter, currency, achievements).

## 3. Locked decisions (architect-call, will adopt unless Cesar overrides)

- **L1.** State enum, not state classes. Matches existing `ShotState` pattern; ~5 states with no per-state local data; switch-based transitions stay readable.
- **L2.** SM is **owned** by `PhysicsLabController` (and later by the gameplay-loop driver in v2). SM does **not** own `BallAnimator` / `TrajectoryRenderer` / `BallSimulation`. It's an orchestrator that consumes signals (`OnShotResolved`, `BallAnimator.IsPlaying` falling edge, plus a stub cup-detection hook) and emits state events.
- **L3.** OB is **one state with a reason payload** (`OBReason ∈ {Water, OutOfBounds, ExitedWorldBounds}`). Roadmap text "InCup | OB" reads as a binary alternative; we keep that surface. Result screen branches on the payload for penalty rules.
- **L4.** Flying/Rolling is **derived from `Trajectory.terrainHits[]`** (first non-stop hit = Flying→Rolling transition; subsequent non-stop hits = bounce → brief Flying again). No changes to `BallSimulation` or `TrajectorySample`. Layer 1 stays untouched.
- **L5.** InCup state is **reserved with a stub detector** in v1 (`ICupDetector.IsInCup(pos) ⇒ false` default impl). Real detection lands in §2d. State-machine consumers can wire `if InCup → result screen` now without blocking.

## 4. Open questions — lock before SPEC.md

### Q1. Asmdef + namespace
Where does the SM live?

- **(a)** New `Golfin.Gameplay.Loop` asmdef, namespace `Golfin.Gameplay.Loop`. References `Golfin.Physics.Core` (Trajectory) + `Golfin.Gameplay.Input` (ShotController). Clean.
- **(b)** Inside existing `Golfin.Gameplay.Input` next to `ShotController`. Fewer asmdef changes. But Input is gesture-scoped; ball lifecycle is broader.
- **(c)** Split: enum + interfaces in `Golfin.Physics.Core` (so tests + `PhysicsLabController` get the type without pulling MonoBehaviour); MonoBehaviour driver in new `Golfin.Gameplay.Loop`.

**Architect lean:** **(a)** for v1. Single new asmdef earns its keep when §2b–§2e all land here. (c) is over-engineering until something in `Physics.Tests` actually needs the enum.

### Q2. Event surface
Two competing shapes for consumers:

- **(a)** Single `event Action<BallStateChange> OnStateChanged` carrying `{ previous, next, position, surface, terminationReason? }`. Camera/turn-counter/result-screen all subscribe and filter.
- **(b)** Two events: fine-grained `OnStateChanged` (for camera/animation) + coarse one-shot `OnShotComplete(ShotResult)` fired exactly once per shot when entering AtRest/InCup/OB. Turn counter / result screen subscribe to the coarse one.

**Architect lean:** **(b)**. Coarse-channel for "shot is done" prevents subscribers from re-implementing the AtRest/InCup/OB filter independently and getting it subtly wrong. Cheap to provide both.

### Q3. Aiming-state composition vs ShotController
The §2a "Aiming" must encompass `ShotController.{Idle, Aiming, Pulling, Timing, Flicking, Resolving}`. Two ways:

- **(a)** Ball SM "Aiming" = "any time `BallAnimator.IsPlaying == false` AND ShotController hasn't yet fired `OnShotResolved`". I.e. SM doesn't read ShotController internal state. Simplest.
- **(b)** Ball SM mirrors ShotController sub-states inside Aiming (Aiming.Idle / Aiming.Pulling / etc.) — useful if §2b camera wants different transitions for "first touch" vs "drag".

**Architect lean:** **(a)** for v1. (b) duplicates ShotController state. If §2b camera needs ShotController granularity, it can subscribe to `ShotController.OnStateChanged` directly — that channel already exists.

### Q4. Cup detector seam
Roadmap §2d will detect hole-complete. §2a needs the InCup state. Lock the seam shape now:

- **(a)** `interface ICupDetector { bool IsInCup(fp3 pos, fp ballRadius); }`. SM holds a reference; injection point on `PhysicsLabController` (and later the gameplay-loop driver). Default impl: `NullCupDetector` returns false.
- **(b)** Static cup position on `HoleContext` (already exists with `PinWorld`!) + radius constant. SM reads directly. Less plumbing.

**Architect lean:** **(a)**. `HoleContext.PinWorld` is `Vector3` (Unity), SM core wants determinism (fp3). Interface keeps types clean and lets §2d swap in a real impl without touching SM.

### Q5. When does the SM check InCup?
Options:

- **(a)** At AtRest entry only (one-shot post-trajectory check). Misses the case where the ball briefly enters the cup mid-roll then exits (rare but real on flat-ish greens with low speed).
- **(b)** Stream through `Trajectory.samples` after the trajectory comes back, find the first sample with `IsInCup`, treat that timestamp as "real" InCup transition. More accurate, deterministic, free.
- **(c)** Per-frame during playback, sample ball position. Drifts from sim truth (animator interpolates between samples).

**Architect lean:** **(b)**. Free determinism, no per-frame work. Spec note: detector runs once per trajectory in `OnShotResolved` handler, before `BallAnimator.Play` starts.

### Q6. Headless / bot path
For Roadmap foundation #5 (headless bots), SM must support a "no playback" mode where we transition Aiming → AtRest immediately after `OnShotResolved` and emit `ShotComplete` synchronously. Lock:

- **(a)** Boolean `Headless` property on SM. When true, on `OnShotResolved` SM walks the full lifecycle synchronously using `Trajectory.terrainHits` only (no animator dependency).
- **(b)** Defer headless support entirely to a later spec.

**Architect lean:** **(a)**. The work is small (the SM already needs to consume `terrainHits` per L4) and locking the path now prevents a refactor later. Headless gets a one-line test in §2a's test pack.

### Q7. Re-arming ShotController
Today, `PhysicsLabController.HandleCameraOrbit` calls `_shotController.CompleteShot()` when playback ends. With SM:

- **(a)** SM emits `OnStateChanged(AtRest)` → `PhysicsLabController` subscribes and calls `CompleteShot()`. SM stays decoupled from ShotController.
- **(b)** SM holds a reference to ShotController and re-arms internally on entering AtRest/InCup/OB.

**Architect lean:** **(a)**. SM doesn't need a `Golfin.Gameplay.Input` reference; PhysicsLabController already has both. Cleaner asmdef graph.

## 5. Fan-out feasibility — verdict: **NOT a fan-out task**

Decomposition:

| File | Depends on | Parallel-safe? |
|---|---|---|
| `BallStateMachine.cs` (enum + driver) | nothing | seed file |
| `BallStateChange.cs` / `ShotResult.cs` (payload types) | enum signature | yes after seed |
| `ICupDetector.cs` + `NullCupDetector.cs` | nothing | yes |
| `Golfin.Gameplay.Loop.asmdef` | nothing | yes |
| Wire-up in `PhysicsLabController.cs` | all above | NO — integration step |
| EditMode tests | all above | NO — integration step |

Only files 2/3/4 truly parallelize (3 small files), and they all join at file 5 — which is where the integration bugs hide. Net win: roughly zero.

**Recommendation:** Run §2a as a serial single-task pipeline (implementer → self-reviewer → reviewer → Cesar). Save fan-out for §2b camera transitions, where each `ChaseCamera.Mode` (Tee / Flight / Rest / Green / Cup) can be a separate file with no overlap.

This refines, not contradicts, the user-memory entry — the gameplay loop is a fan-out CANDIDATE; the ball state machine specifically isn't a fan-out FIT.

## 6. Spec-writing checklist (after Cesar locks Q1–Q7)

- [ ] State enum + transition table (text diagram).
- [ ] `BallStateChange`, `ShotResult`, `OBReason` payload structs.
- [ ] `ICupDetector` + `NullCupDetector` (stub returns false).
- [ ] Driver class with consume signals (`OnShotResolved`, animator-ended falling edge) + emit events.
- [ ] Headless path.
- [ ] `PhysicsLabController` integration: replace `_prevBallPlaying && !isPlaying` with SM subscription; route `OnShotResolved` through SM.
- [ ] EditMode tests covering: each transition; OB sub-reasons; InCup via stub injection; bounce-induced Flying↔Rolling flicker doesn't drop events; headless path matches non-headless terminal state; determinism (fire same shot twice → identical state sequence).
- [ ] No new dependencies into `Golfin.Physics.Core`. Layer 1 stays untouched.
- [ ] Definition-of-done: existing 211/211 tests still PASS, plus N new SM tests.

## 7. Files not yet read but flagged for SPEC phase

- `Assets/Scripts/Physics/Core/SurfaceType.cs` — confirm cup/hole-related surface enum doesn't already exist.
- `Assets/Scripts/Physics/Runtime/Baked/*` — confirm cup position is (or isn't) baked into `zones.json`.
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` — confirm `PinWorld` provenance and whether a cup radius constant lives nearby.
- `Assets/Scripts/Physics/Viewer/BallAnimator.cs` — confirm `IsPlaying` falling-edge timing (one-frame delay? exact-frame?).

These are low-risk reads; will do them after Q1–Q7 are locked, before SPEC.md.
