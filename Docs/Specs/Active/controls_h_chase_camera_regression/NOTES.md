# controls_h — Chase camera doesn't track during flight (architect prep notes)

**Status:** QUEUED, awaiting SPEC. Architect prep written 2026-05-07 by Claude Code after Cesar reported the regression during manual play.

**Predecessor:** `controls_g_smoke_followup` (DONE, ARCHITECT_REVIEW_PASS) — closed §2b deferred smoke captures via state-driven `Director.OnModeChanged` evidence, but the smoke captures only verify mode-firing, not visual fidelity. This task is what they missed.

**Severity:** P1. Loop v1 §2b regression. The cinematic camera flow is the central feel mechanic for the loop; right now it's broken on the touch/flick path which is the only path a real player uses.

---

## 1. Symptoms (from Cesar's manual play, 2026-05-07)

| Club | Observed |
|---|---|
| Driver | Camera stays static during flight. At/around landing, camera SmoothDamps in a "quick motion" to a new position. Stays static during roll. |
| Iron (short carry) | Camera never moves during flight or roll. Only moves when the next shot fires. |

Putter shots not yet manually verified post-§2b but spec-mandated to skip mode changes (Q1'c) so likely fine.

---

## 2. Intended sequence (for reference)

From `LoopCameraDirector.cs:104-115` `ModeMap` + `ChaseCamera.cs:79-142`:

| Ball state | Driver/iron mode | What ChaseCamera does |
|---|---|---|
| Aiming → Flying | `ArmChaseForShot` (target=ball, reset behind tee) → `Chase` | smoothly tracks ball: `pos = ball - launchDir·5m + up·2.5m`, looks at ball |
| During Flying (≥65% of carry, ≥30m carry) | cinematic cut → `Downrange` (static) | snaps to a fixed point past the landing |
| Flying → Rolling | `Chase` | tracks rolling ball |
| Rolling → AtRest | `Chase` (target cleared) | settles |
| InCup | `CupZoom` (1s tween) | tween then hover |
| OB | `OBFreeze` | static pivot, look-at |

---

## 3. Root cause — order-of-operations bug in `HandleShotResolved`

The §2b refactor (commit `03e6a31e "Camera work"`) moved chase-target arming responsibility from inline `chaseCamera.SetTarget(...)` calls into the LoopCameraDirector, triggered by the SM's `Aiming → Flying` transition. **But the trigger fires too early.**

### Two shot-fire paths in `PhysicsLabController.cs`, only one was refactored

| Method | Used by | Calls `_ballSM.OnTrajectoryComputed`? | Caches `_lastShotOrigin/Dir`? | Direct `chaseCamera.SetTarget`? |
|---|---|---|---|---|
| `HandleShotResolved` (line 683) | Touch / flick — **what real players hit** | ✅ line 692 | ✅ lines 723-724 | ❌ (Director-only path) |
| `FireInternal(preset)` (line 822) | Preset buttons + smoke runners | ❌ | ❌ | ✅ lines 839-840 (legacy direct) |

The smoke runners (`SmokeTestRunner2a/2b`) used `FireInternal` and got the legacy direct chase-camera arming, which is why their captures showed mode-history transitions firing. Manual play uses `HandleShotResolved`, which is the broken path.

### Synchronous SM transition fires before HandleShotResolved finishes setting up

`BallStateMachine.OnTrajectoryComputed` at `BallStateMachine.cs:222-229` fires the first transition (Aiming → Flying) **synchronously** before returning:

```csharp
// ── Fire first transition (Aiming → Flying) synchronously ─────────
if (!Headless)
{
    var first = _pendingTransitions[0];
    _pendingTransitions.RemoveAt(0);
    State = first.Next;
    OnStateChanged?.Invoke(first);    // ← Director.HandleStateChanged runs HERE
    _prevAnimatorPlaying = true;
}
```

The remaining transitions (Flying→Rolling, Rolling→AtRest) are queued in `_pendingTransitions` and held until `Tick(animatorIsPlaying)` detects a **falling edge** of `animatorIsPlaying` — i.e., when the ball animation finishes (`BallStateMachine.cs:243-260`).

### What that means for HandleShotResolved (`PhysicsLabController.cs:683-759`)

```
689:    _previousTrajectory = trajectory;            // ✅ fresh
        ...
692:    _ballSM?.OnTrajectoryComputed(...);          // 🔥 fires Aiming→Flying synchronously
        //   ↳ Director.HandleStateChanged runs:
        //     - ArmChaseForShot(LastShotOrigin=STALE, LastShotLaunchDir=STALE,
        //                       CurrentBall=OLD ball about to be destroyed)
        //         - SetTarget(OLD ball)        ← _target = soon-to-be-destroyed ref
        //         - ResetToOrigin(STALE, STALE)
        //     - ApplyMode(Chase)
        ...
695:    ballAnimator.Play(trajectory);               // 🔥 DESTROYS old ball, spawns new one
        //   _target now points to a destroyed Transform → equates to null in Unity
        ...
723:    _lastShotOrigin    = origin;                 // ❌ updated AFTER it was needed
724:    _lastShotLaunchDir = launchDir;
```

So at the moment the Director arms the camera:
- `LastShotOrigin / LastShotLaunchDir` hold the **previous shot's** values (or zero on first shot)
- `CurrentBall` is the **old** ball (the placeholder from `PlaceAtRest()` on first shot, or the prior shot's resting ball)
- That old ball gets destroyed 3 lines later by `BallAnimator.Play()` → `_target == null` from then on

### Why the camera then behaves exactly as Cesar described

`ChaseCamera.LateUpdate` line 84:
```csharp
if (_target == null && _mode == Mode.Chase) return;
```

For the entire flight, `_target` is destroyed → comparison evaluates null → early-return → **camera doesn't move.**

**Driver — "quick motion at landing":** `LoopCameraDirector.TickCinematicCut` runs in `Update()` independently. By the next Update tick (after `HandleShotResolved` returns), `_lastShotOrigin/Dir` cache IS fresh, and the SM state IS still `Flying`. At ~65% of carry the cut fires, `setter.SetDownrangeFraming(...)` + `ApplyMode(Downrange)`. The Downrange case in `ChaseCamera.LateUpdate` does NOT need a target — it just SmoothDamps to `_downrangePos`. **That SmoothDamp from "wherever the camera was sitting idle" to the downrange point is the "quick motion" Cesar sees, and it lines up with landing because the cut threshold is 65% of carry.**

**Driver — "stays there during roll":** when the animator finishes, `Tick` drains pending transitions in one frame: Flying→Rolling (ApplyMode(Chase)), Rolling→AtRest (ApplyMode(Chase) + SetTarget(null)). No `ArmChaseForShot` re-fires (only triggers on Aiming→Flying). `_target` stays null → Chase mode early-returns → camera stays at downrange position.

**Iron — "camera never moves":** if predicted carry < 30m (`minCarryForCinematicMeters`), `TickCinematicCut` returns early at line 158 — no Downrange cut. Mode stays Chase the whole shot. Target stays null. **Nothing ever updates the camera.**

---

## 4. Why the smoke captures missed it

`SmokeTestRunner2b` used `CaptureCore.SnapWhenModeReached`, which subscribes to `Director.OnModeChanged`. That event fires whenever `ApplyMode` is called regardless of whether the camera visually responds correctly. So:

- Mode history `[Chase, Downrange, ...]` for the driver — confirms mode events fire, NOT that Chase tracks the ball
- Mode history `[]` for the putter — confirms Director correctly skips mode changes per Q1'c
- OBFreeze `[Chase, Downrange, OBFreeze]` — same: confirms event sequence, not visual fidelity

The self-reviewer's three flagged visual concerns in `controls_g_smoke_followup` (faint Downrange ball-in-flight, putter showing predictor not roll, OBFreeze frame missing water) were all **downstream symptoms of the same root cause** — the ball Transform reference fed to the camera was either stale or destroyed at the moment of capture.

This is a methodology gap worth a Lesson: **runtime mode-history evidence ≠ visual chase fidelity.** Captures that subscribe to `OnModeChanged` are sufficient to prove the dispatch table is wired, but cannot prove the camera visually behaves correctly. A real visual-verification protocol must include either (a) human-in-the-loop play-and-confirm or (b) reading the actual camera Transform position over time and asserting it tracks the ball position.

Suggested Lesson title: *"Pipeline Lesson O — `OnModeChanged` is dispatch evidence, not visual evidence. Spec verification protocols must distinguish these."*

---

## 5. Repair options

### Option A (preferred — minimal, surgical) — reorder HandleShotResolved

Move the `_lastShotOrigin/_lastShotLaunchDir` cache assignment AND `ballAnimator.Play(trajectory)` BEFORE `_ballSM.OnTrajectoryComputed(...)`:

```csharp
void HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)
{
    fp3 ballOrigin = GetCurrentOrigin(fallbackToInput: input.origin);
    var correctedInput = new ShotInput(...);

    var trajectory = RunSimFromController(correctedInput, ballMods);
    _previousTrajectory = trajectory;

    // ── §controls_h: cache + spawn ball BEFORE SM transition fires ──────
    var s0 = trajectory.samples != null && trajectory.samples.Count > 0
        ? trajectory.samples[0].position : correctedInput.origin;
    _lastShotOrigin    = new Vector3(s0.x.ToFloat(), s0.y.ToFloat(), s0.z.ToFloat());
    _lastShotLaunchDir = new Vector3(correctedInput.velocity.x.ToFloat(), 0f,
                                     correctedInput.velocity.z.ToFloat()).normalized;
    if (_lastShotLaunchDir == Vector3.zero) _lastShotLaunchDir = Vector3.right;
    _orbitCenter = _lastShotOrigin;

    trajectoryRenderer.Draw(trajectory);
    ballAnimator.Play(trajectory);  // ← spawns new ball

    // §controls_h: now the Director sees fresh cache + fresh ball when SM fires.
    _ballSM?.OnTrajectoryComputed(correctedInput.origin, trajectory, AeroCfg.BallRadius);

    // ... rest of HandleShotResolved (HUD setup, readout, etc.)
}
```

**Pros:** one-method change, tightly scoped, no SM API changes, no new events.
**Cons:** the comment in `BallStateMachine.cs:64-66` ("BEFORE BallAnimator.Play() is invoked") becomes incorrect and needs deleting/updating. We need to verify there's no reason that comment was load-bearing. Cursory check: `_prevAnimatorPlaying = true` at line 229 still primes the falling-edge correctly even if the animator is already playing when OnTrajectoryComputed runs (the next Tick sees true, no falling edge, no drain — same effect).

### Option B — defer the synchronous Aiming→Flying fire to next Tick

Remove the synchronous fire at `BallStateMachine.cs:222-229` and let `Tick()` drain the FIRST pending transition unconditionally (regardless of falling edge) when `_pendingTransitions[0].Previous == BallState.Aiming`. That way HandleShotResolved finishes setting up `_lastShotOrigin/Dir` and `ballAnimator.Play()` BEFORE the SM fires the state change on the next frame's Tick.

**Pros:** removes a subtle "synchronous side-effect inside OnTrajectoryComputed" that's easy to misuse going forward.
**Cons:** larger blast radius (touches SM core logic + 16 EditMode tests). Risks breaking headless tests that expect synchronous fire.

### Option C — re-arm chase target after BallAnimator spawns

Add an event `BallAnimator.OnBallSpawned : Action<Transform>` raised at the end of `SpawnInstance`, and have LoopCameraDirector subscribe and call `setter.SetTarget(newBall)` when fired. This re-arms the target after destruction.

**Pros:** robust against future timing reorderings.
**Cons:** adds a new event, second wiring path; doesn't address the stale `LastShotOrigin/Dir` issue (still need to fix that separately).

### Option D — kill `FireInternal`'s legacy direct chase-camera calls

Independent of A/B/C, the `FireInternal` path at `PhysicsLabController.cs:837-841` still has direct `chaseCamera.SetTarget` + `ResetToOrigin`. The §2b refactor was supposed to centralize on the Director. This works today (because `FireInternal` has its own self-contained correct ordering) but is a smell — two arming paths drifting apart is exactly how this bug class regresses again. Should be folded into A/B/C as a tidy-up.

---

## 6. Open questions for Cesar before SPEC.md gets written

1. **Repair option:** A (reorder HandleShotResolved) is the surgical fix. B (defer SM fire) is more architecturally sound but riskier. Which does Architect prefer? Recommend A + D as the SPEC scope.
2. **Visual-verification protocol:** want a Lesson O added to `Docs/Diagnostics/PIPELINE_LESSONS.md` codifying that `OnModeChanged` evidence ≠ visual-tracking evidence, AND a corresponding update to `Docs/Specs/Active/_TEMPLATE/SPEC.md` requiring future camera-related specs to include either human-confirm or position-trace verification?
3. **Test coverage:** the 9 LoopCameraDirector EditMode tests in the 241/241 gate exercise the dispatch table but not this timing bug. Add a test that drives `HandleShotResolved` end-to-end and asserts `setter.GetTarget() == ballAnimator.CurrentBall` AFTER `Play()` returns? Possible with `RecordingModeSetter` + a test BallAnimator. Would catch the regression class.
4. **Smoke runner deletion:** with `SmokeTestRunner2a_GO` and `SmokeTestRunner2b_GO` already removed from `LabScaffold.unity` (this session, post-controls_g closeout), should the `.cs` files also be deleted, or kept as auditable evidence? Recommend keeping them — they're cheap and the next regression in this area might want to re-run something equivalent.

---

## 7. Code references

| File | Lines | Purpose |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 683-759 | `HandleShotResolved` — touch/flick path with the bug |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 692 | `_ballSM.OnTrajectoryComputed` synchronous trigger |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 723-724 | `_lastShotOrigin/Dir` cache (too late) |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 822-846 | `FireInternal(preset)` — legacy preset path with direct chase-camera calls |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 837-841 | Direct `chaseCamera.SetTarget` / `ResetToOrigin` (Director-bypass smell) |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | 222-229 | Synchronous fire of first transition |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | 243-260 | Falling-edge drain of remaining transitions |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | 179-234 | `HandleStateChanged` and `ArmChaseForShot` |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | 140-175 | `TickCinematicCut` — the cut that fires Cesar's "quick motion at landing" |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | 81-84 | `LateUpdate` early-return when target null |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | 132-135 | Chase mode position formula |
| `Assets/Scripts/Physics/Viewer/BallAnimator.cs` | 43-60, 144-156 | `Play()` destroys + respawns ball Transform |

Commit that introduced the regression: `03e6a31e "Camera work"` (the `controls_g_smoke_followup` impl pass).
