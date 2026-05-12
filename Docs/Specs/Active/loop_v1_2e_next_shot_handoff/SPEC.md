# SPEC — `loop_v1_2e_next_shot_handoff`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state. Architect-locked at SPEC_READY 2026-05-13 17:30 JST.

## Goal

Close out the Loop v1 next-shot handoff. Today the AtRest re-arm chain already fires (`PhysicsLabController.HandleShotComplete` → `_shotController.CompleteShot()` + `_ballSM.ReArm()` → SM transitions to Aiming → `HoleSessionDriver.HandleStateChanged` advances TURN). §2e adds the two missing pieces:

1. **On AtRest**, rotate the camera to face the pin (`HoleContext.PinWorld`) before snapping into the Aiming pose.
2. **On OB**, compute a safe drop point by walking the just-finished trajectory's terrain hits backward to the last non-Water/non-OOB surface contact, teleport the ball there, rotate the camera to face the pin from the drop location, and record a penalty stroke in `ShotRecord.PenaltyStrokes` so the TURN counter advances by 2 (shot + penalty) instead of 1.

This matches real-golf stroke accounting and finishes the Loop v1 ball-state lifecycle.

## Reference

- **§2a SPEC:** `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/SPEC.md` — `BallStateMachine.OnShotComplete(ShotResult)` is the trigger; `BallStateChange.Previous` is the order-independent OB signal.
- **§2b SPEC:** `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SPEC.md` — `LoopCameraDirector` owns camera modes. §2e cooperates: Director handles state→mode dispatch, §2e adjusts `_cameraYaw` for the AtRest aiming pose.
- **§2c SPEC:** `Docs/Specs/Completed/loop_v1_2c_turn_counter_and_shot_history/SPEC.md` — `HoleSessionDriver` records shots + advances TURN. §2e extends both with penalty stroke handling.
- **§2d SPEC:** `Docs/Specs/Completed/loop_v1_2d_hole_complete_and_result_screen/SPEC.md` — `RealCupDetector` + `HoleCompleteDriver` own the InCup path. §2e does NOT touch InCup; that stays §2d's flow.
- No new Figma — §2e is mechanics, not UI.

## Background — what exists today

Verified by code walk 2026-05-13 17:00 JST.

| File | Role for §2e |
|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | `HandleShotComplete(ShotResult)` (lines ~880–930) already handles AtRest/OB by calling `CompleteShot + ReArm`; on AtRest also snaps camera via `ApplyCameraYaw`. **Extend with pin-aim rotation on AtRest + OB drop teleport on OB.** Also refactors `PlaceBallAt` to share a `RepositionBallWithLookDir` private helper. |
| `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs` | Records `ShotRecord` on `OnShotComplete`, advances TURN on `OnStateChanged → Aiming` (+1). **Extend `BuildShotRecord` to set `PenaltyStrokes=1` on OB. Extend `HandleStateChanged` to add +1 more when `change.Previous == BallState.OB`.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` | `ShotRecord` struct has 8 fields. **Add `PenaltyStrokes` field via new 9-arg constructor; preserve existing 8-arg constructor as a default-zero overload.** |
| `Assets/Scripts/Physics/Core/Trajectory.cs` | `TerrainHit { Surface, Position, IsStop, ... }`. **Read-only consumer — `OBDropResolver` walks `terrainHits`.** |
| `Assets/Scripts/Physics/Core/SurfaceType.cs` | 11 enum values. **Drop resolver treats `Water` and `OOB` as unsafe; everything else is a valid drop surface.** |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` | `PinWorld` already populated by `PhysicsLabController.OnHoleLoaded` from the `Flag` GO scan. **Read-only consumer for aim rotation.** |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | Owns ChaseCamera mode transitions on SM state changes. **§2e does NOT touch the Director — Director already returns Chase mode on OB→Aiming and AtRest→Aiming.** |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `ReArm()` invokes `OnStateChanged(BallStateChange{Previous=AtRest|InCup|OB, Next=Aiming})` synchronously. **The `Previous` field is the order-independent OB signal that §2e reads in `HoleSessionDriver.HandleStateChanged`.** |
| `Assets/Scripts/Gameplay/Loop/BallStateChange.cs` | Already exposes `Previous`, `Next`, `Position`, `Surface`, `OBReason?`, `SimTime`. **No changes — §2e reads `Previous` only.** |

### Critical ordering observation (resolved)

`BallStateMachine.DrainPendingTransitions()` fires `OnStateChanged` for every transition (including terminal → AtRest/InCup/OB) **then** fires `OnShotComplete` once. On `OnShotComplete`:

1. `PhysicsLabController.HandleShotComplete` runs first (subscribed in `Awake`).
2. PLC calls `_ballSM.ReArm()` synchronously, which fires `OnStateChanged(Previous=terminal, Next=Aiming)`.
3. `HoleSessionDriver.HandleStateChanged` runs and advances TURN — **BEFORE** `HoleSessionDriver.HandleShotComplete` records the shot (which subscribes in `Start`, later in the multicast delegate list).

This ordering means HSD's TURN-advance logic cannot read `ShotHistory[-1].PenaltyStrokes`. Fortunately `BallStateChange.Previous == BallState.OB` is the same signal, available directly on the event. §2e uses that. Order-independent.

## Locked decisions

- **L1 — TurnCount semantics.** TURN advances by 1 on AtRest→Aiming, by 2 on OB→Aiming (shot + penalty), by 1 on InCup→Aiming (existing behavior; invisible behind §2d modal). Matches real-golf stroke counting: when the player addresses the ball for stroke N, TURN reads N.
- **L2 — Penalty representation.** `ShotRecord.PenaltyStrokes` (int) — 0 normally, 1 on OB. No separate "penalty ShotRecord" entry — keeps history append-only and matches scorecard semantics (one record per ball address; penalty is metadata on that shot).
- **L3 — AtRest aim rule.** On AtRest, rotate `_cameraYaw` to point at `HoleContext.PinWorld - ballPos` (XZ-only). If pin is unset (`PinWorld == Vector3.zero`) or distance < 1cm, preserve current `_cameraYaw`.
- **L4 — OB drop rule.** Walk `controller.LastTrajectory.terrainHits` from latest to earliest. The first `TerrainHit` whose `Surface` is NOT `Water` and NOT `OOB` is the drop point. If no such hit exists, fall back to `_lastShotOrigin` (player replays from where they last shot). "Drop at the last safe trajectory contact" — simpler than USGA local rule, less punitive than stroke-and-distance, 100% computable from existing data.
- **L5 — OB aim rule after drop.** Same as AtRest: rotate `_cameraYaw` to face pin from the drop position. Pass that direction into `RepositionBallWithLookDir` instead of `GetDefaultLookDirection()`'s tee→green default.
- **L6 — No OB UI.** Console log only. Banner / toast / dialog deferred to Loop v2 polish. The TURN counter visibly jumping by 2 + the ball teleporting are sufficient evidence in §2e.
- **L7 — §2e does not touch the Director.** Camera mode transitions stay owned by `LoopCameraDirector` (Aiming ↔ Chase ↔ Downrange ↔ CupZoom ↔ OBFreeze). §2e only mutates `_cameraYaw` (the orbit rotation around `_orbitCenter`) and (on OB) calls `RepositionBallWithLookDir`. No `chaseCamera.SetMode` calls in §2e.
- **L8 — OB→Aiming detection via `BallStateChange.Previous`.** Order-independent. HSD does not need to cache the last `ShotResult` or read `ShotHistory[-1]`.

## Architecture context

- **No new asmdef.** All work in `Golfin.Gameplay.UI.HUD` (`ShotRecord` extension) and `Golfin.Physics.Viewer` (new `OBDropResolver` + `AimRotationHelper` static classes, `HoleSessionDriver` + `PhysicsLabController` extensions).
- **No changes to** `Golfin.Gameplay.Loop` (read-only consumer of `OnShotComplete` / `ShotResult`), `Golfin.Physics.Core`, `Golfin.Physics.Stats`, `Golfin.Diagnostics.Runtime`, any aero CSV.
- **No new test asmdef.** New tests land in existing `Golfin.Physics.Tests`.
- **No scene changes.** No new MonoBehaviours; no Inspector wiring needed.

## Implementation

### A. Extend `ShotRecord` with `PenaltyStrokes`

**File:** `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs`

Add `PenaltyStrokes` field + new 9-arg constructor. Preserve existing 8-arg constructor as a default-zero overload that forwards to the new one.

```csharp
public readonly struct ShotRecord
{
    public readonly int    ShotNumber;
    public readonly string ClubLabel;
    public readonly Vector3 OriginPosition;
    public readonly Vector3 FinalPosition;
    public readonly float   DistanceXZMeters;
    public readonly string  TerminalState;
    public readonly string  OBReason;
    public readonly string  FinalSurface;
    public readonly int     PenaltyStrokes;  // §2e: 0 normally, 1 on OB

    // §2e: 9-arg constructor with PenaltyStrokes.
    public ShotRecord(
        int shotNumber, string clubLabel,
        Vector3 originPosition, Vector3 finalPosition,
        float distanceXZMeters,
        string terminalState, string obReason, string finalSurface,
        int penaltyStrokes)
    {
        ShotNumber       = shotNumber;
        ClubLabel        = clubLabel;
        OriginPosition   = originPosition;
        FinalPosition    = finalPosition;
        DistanceXZMeters = distanceXZMeters;
        TerminalState    = terminalState;
        OBReason         = obReason;
        FinalSurface     = finalSurface;
        PenaltyStrokes   = penaltyStrokes;
    }

    // §2c: existing 8-arg constructor preserved — forwards to new ctor with PenaltyStrokes=0.
    public ShotRecord(
        int shotNumber, string clubLabel,
        Vector3 originPosition, Vector3 finalPosition,
        float distanceXZMeters,
        string terminalState, string obReason, string finalSurface)
        : this(shotNumber, clubLabel, originPosition, finalPosition,
               distanceXZMeters, terminalState, obReason, finalSurface, 0)
    { }
}
```

### B. New `OBDropResolver` static helper

**Location:** `Assets/Scripts/Physics/Viewer/OBDropResolver.cs`. Namespace `Golfin.Physics.Viewer`.

```csharp
using UnityEngine;
using Golfin.Physics;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2e: computes the drop position when a shot ends OB.
    /// Walks the trajectory's terrain hits from latest to earliest, finds the
    /// first hit whose Surface is neither Water nor OOB, and returns that
    /// position. Falls back to the player's previous shot origin if no safe
    /// hit exists (e.g. tee shot straight into water hazard with no land touch).
    /// </summary>
    public static class OBDropResolver
    {
        public static Vector3 Resolve(Trajectory trajectory, Vector3 fallbackOrigin)
        {
            if (trajectory == null || trajectory.terrainHits == null) return fallbackOrigin;
            var hits = trajectory.terrainHits;
            for (int i = hits.Count - 1; i >= 0; i--)
            {
                var s = hits[i].Surface;
                if (s == SurfaceType.Water || s == SurfaceType.OOB) continue;
                var p = hits[i].Position;
                return new Vector3(p.x.ToFloat(), p.y.ToFloat(), p.z.ToFloat());
            }
            return fallbackOrigin;
        }
    }
}
```

### C. New `AimRotationHelper` static helper

**Location:** `Assets/Scripts/Physics/Viewer/AimRotationHelper.cs`. Namespace `Golfin.Physics.Viewer`.

```csharp
using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2e: computes the camera yaw angle needed to face a target world position
    /// from a ball position. XZ-only (Y is irrelevant for yaw).
    /// </summary>
    public static class AimRotationHelper
    {
        /// <summary>
        /// Returns yaw in radians for camera at ballPos to face pinPos.
        /// Falls back to fallbackYaw if pinPos is Vector3.zero (unset) or the
        /// XZ distance squared is less than 1e-4 (< 1cm) — too small to define
        /// a stable direction.
        /// </summary>
        public static float ComputeYawTowardPin(Vector3 ballPos, Vector3 pinPos, float fallbackYaw)
        {
            if (pinPos == Vector3.zero) return fallbackYaw;
            float dx = pinPos.x - ballPos.x;
            float dz = pinPos.z - ballPos.z;
            if (dx * dx + dz * dz < 0.0001f) return fallbackYaw;
            return Mathf.Atan2(dz, dx);
        }
    }
}
```

### D. Extend `HoleSessionDriver`

**File:** `Assets/Scripts/Physics/Viewer/HoleSessionDriver.cs`

Two changes:

**D.1 — `BuildShotRecord` sets `PenaltyStrokes` from `ShotResult.TerminalState`:**

```csharp
ShotRecord BuildShotRecord(ShotResult result)
{
    string clubLabel = "Unknown";
    if (controller != null
        && controller.CurrentClubIndex >= 0
        && controller.CurrentClubIndex < PhysicsLabController.LabClubLabels.Length)
    {
        clubLabel = PhysicsLabController.LabClubLabels[controller.CurrentClubIndex];
    }

    Vector3 origin = controller != null ? controller.LastShotOrigin : Vector3.zero;
    Vector3 finalPos = new Vector3(
        result.EndPosition.x.ToFloat(),
        result.EndPosition.y.ToFloat(),
        result.EndPosition.z.ToFloat());

    float dx = finalPos.x - origin.x;
    float dz = finalPos.z - origin.z;
    float distXZ = Mathf.Sqrt(dx * dx + dz * dz);

    string finalSurface = "Unknown";
    var traj = controller != null ? controller.LastTrajectory : null;
    if (traj != null && traj.terrainHits != null && traj.terrainHits.Count > 0)
        finalSurface = traj.terrainHits[traj.terrainHits.Count - 1].Surface.ToString();

    string obReason = result.OBReason.HasValue ? result.OBReason.Value.ToString() : null;

    // §2e: OB shots get +1 penalty stroke.
    int penaltyStrokes = (result.TerminalState == BallState.OB) ? 1 : 0;

    return new ShotRecord(
        shotNumber: GameSession.TurnCount,
        clubLabel: clubLabel,
        originPosition: origin,
        finalPosition: finalPos,
        distanceXZMeters: distXZ,
        terminalState: result.TerminalState.ToString(),
        obReason: obReason,
        finalSurface: finalSurface,
        penaltyStrokes: penaltyStrokes);
}
```

**D.2 — `HandleStateChanged` adds penalty stroke on OB→Aiming via `change.Previous`:**

```csharp
void HandleStateChanged(BallStateChange change)
{
    if (change.Next != BallState.Aiming) return;
    if (change.Previous != BallState.AtRest
     && change.Previous != BallState.InCup
     && change.Previous != BallState.OB) return;

    // §2e: OB transitions add a penalty stroke. Order-independent — reads from
    // BallStateChange.Previous, not from ShotHistory (which may not be populated
    // yet due to subscription order).
    int penalty = (change.Previous == BallState.OB) ? 1 : 0;
    GameSession.SetTurn(ComputeNextTurn(GameSession.TurnCount, penalty));
}

/// <summary>
/// §2e: pure helper for unit tests. Caller passes current turn + penalty stroke
/// count; returns the new turn value. Negative penalty clamped to 0.
/// </summary>
public static int ComputeNextTurn(int currentTurn, int penaltyStrokes)
{
    if (penaltyStrokes < 0) penaltyStrokes = 0;
    return currentTurn + 1 + penaltyStrokes;
}
```

**D.3 — New `BuildShotRecordStatic` overload accepting `penaltyStrokes`:**

```csharp
/// <summary>
/// §2e: test seam overload with penaltyStrokes parameter.
/// </summary>
public static ShotRecord BuildShotRecordStatic(
    int shotNumber, string clubLabel,
    Vector3 origin, Vector3 finalPos,
    string terminalState, string obReason, string finalSurface,
    int penaltyStrokes)
{
    float dx = finalPos.x - origin.x;
    float dz = finalPos.z - origin.z;
    float distXZ = Mathf.Sqrt(dx * dx + dz * dz);
    return new ShotRecord(shotNumber, clubLabel, origin, finalPos, distXZ,
                          terminalState, obReason, finalSurface, penaltyStrokes);
}
```

Keep the existing 7-arg `BuildShotRecordStatic` (defaults `PenaltyStrokes=0` via the 8-arg `ShotRecord` constructor).

### E. Refactor `PhysicsLabController.PlaceBallAt` → `RepositionBallWithLookDir`

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

Extract the body of `PlaceBallAt(Vector3, int?)` into a private helper that takes an explicit look direction. `PlaceBallAt` retains its public signature and behavior — it calls the helper with `GetDefaultLookDirection()`. The new OB-drop path calls the helper with the pin-facing direction.

```csharp
public void PlaceBallAt(Vector3 worldPos, int? preferredSurfaceTypeValue = null)
{
    RepositionBallWithLookDir(worldPos, preferredSurfaceTypeValue, GetDefaultLookDirection());
}

void RepositionBallWithLookDir(Vector3 worldPos, int? preferredSurfaceTypeValue, Vector3 lookDir)
{
    if (_shotController != null) _shotController.CompleteShot();

    float y   = SurfaceSnap(worldPos.x, worldPos.z, worldPos.y, preferredSurfaceTypeValue);
    Vector3 pos = new Vector3(worldPos.x, y, worldPos.z);

    _orbitCenter = pos;
    if (ballAnimator != null) ballAnimator.PlaceAtRest(pos);

    if (_shotConeView != null && ballAnimator != null)
        _shotConeView.SetBallTransform(ballAnimator.CurrentBall);
    if (_puttPathPredictor != null && ballAnimator != null)
    {
        _puttPathPredictor.SetBallTransform(ballAnimator.CurrentBall);
        _puttPathPredictor.SetCamera(chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null);
    }

    _cameraYaw = Mathf.Atan2(lookDir.z, lookDir.x);
    if (_shotController != null)
        _shotController.CameraHeadingRadians = _cameraYaw;

    Camera placeCamForApply = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
    if (placeCamForApply != null) ApplyCameraYaw(placeCamForApply);

    if (_shotController != null && _shotController.IsPutt && chaseCamera != null)
        chaseCamera.SetMode(ChaseCamera.Mode.GroundLevel);

    AdjustCameraForDepression(pos);
}
```

The public `PlaceBallAt` signature stays. The "Place Ball" dropdown in `PhysicsLabUI` is unaffected — it continues to use the tee→green look direction. The OB path uses `RepositionBallWithLookDir` directly with the pin-facing direction.

### F. Extend `PhysicsLabController.HandleShotComplete`

Modify the existing handler. Restructure as a `switch` on `result.TerminalState`. The AtRest branch gains pin-aim rotation before `ApplyCameraYaw`. The OB branch gains drop + reposition. The InCup branch stays a no-op (§2d's `HoleCompleteDriver` owns it via `RearmAfterHoleComplete`).

```csharp
void HandleShotComplete(Golfin.Gameplay.Loop.ShotResult result)
{
    Debug.Log($"[PhysicsLab][§2a] OnShotComplete: terminal={result.TerminalState}" +
              (result.OBReason.HasValue ? $" OBReason={result.OBReason.Value}" : "") +
              $" end={result.EndPosition}");

    if (ballAnimator?.CurrentBall != null)
        _orbitCenter = ballAnimator.CurrentBall.position;

    switch (result.TerminalState)
    {
        case Golfin.Gameplay.Loop.BallState.AtRest:
        {
            // §2e: rotate camera yaw to face the pin before snapping into Aiming pose.
            Vector3 ballPos = ballAnimator?.CurrentBall != null
                ? ballAnimator.CurrentBall.position
                : _orbitCenter;
            Vector3 pinPos  = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
            float   newYaw  = AimRotationHelper.ComputeYawTowardPin(ballPos, pinPos, _cameraYaw);
            if (!Mathf.Approximately(newYaw, _cameraYaw))
            {
                _cameraYaw = newYaw;
                if (_shotController != null)
                    _shotController.CameraHeadingRadians = _cameraYaw;
            }

            Camera cam = chaseCamera != null ? chaseCamera.GetComponent<Camera>() : null;
            if (cam != null) ApplyCameraYaw(cam);

            _shotController?.CompleteShot();
            _ballSM.ReArm();
            break;
        }

        case Golfin.Gameplay.Loop.BallState.OB:
        {
            // §2e: compute drop point from the just-finished trajectory.
            Vector3 dropPos = OBDropResolver.Resolve(_previousTrajectory, _lastShotOrigin);
            Vector3 pinPos  = Golfin.Gameplay.UI.HUD.HoleContext.PinWorld;
            float   newYaw  = AimRotationHelper.ComputeYawTowardPin(dropPos, pinPos, _cameraYaw);
            Vector3 lookDir = new Vector3(Mathf.Cos(newYaw), 0f, Mathf.Sin(newYaw));

            Debug.Log($"[PhysicsLab][§2e] OB drop: from end={result.EndPosition} " +
                      $"to drop={dropPos:F2} yawRad={newYaw:F3} (penalty stroke +1)");

            // Reposition ball at drop point. RepositionBallWithLookDir calls
            // _shotController.CompleteShot() internally — no need to call it again.
            RepositionBallWithLookDir(dropPos, preferredSurfaceTypeValue: null, lookDir: lookDir);

            _ballSM.ReArm();
            break;
        }

        case Golfin.Gameplay.Loop.BallState.InCup:
        {
            // §2d owns re-arm via HoleCompleteDriver / RearmAfterHoleComplete on modal close.
            // No CompleteShot/ReArm here.
            break;
        }
    }
}
```

## Tests

**Location:** `Assets/Scripts/Physics/Tests/NextShotHandoffTests.cs` (new file). Asmdef: `Golfin.Physics.Tests` (existing).

**9 required tests:**

1. **`ShotRecord_EightArgCtor_DefaultsPenaltyStrokesToZero`** — construct via 8-arg ctor → `record.PenaltyStrokes == 0`.
2. **`ShotRecord_NineArgCtor_SetsPenaltyStrokes`** — construct via 9-arg ctor with `penaltyStrokes=1` → `record.PenaltyStrokes == 1`. All other fields round-trip correctly.
3. **`OBDropResolver_FindsLastSafeFairwayHitBeforeWater`** — build a `Trajectory` with `terrainHits = [Fairway@(10,0,0), Fairway@(20,0,0), Water@(30,0,0)]` → drop at `(20,0,0)`.
4. **`OBDropResolver_SkipsWaterAndOOBHits`** — hits = `[Rough@(5,0,0), Water@(15,0,0), OOB@(25,0,0)]` → drop at `(5,0,0)`.
5. **`OBDropResolver_FallsBackToOriginWhenNoSafeHit`** — hits = `[Water@(10,0,0), Water@(20,0,0)]` + fallback = `(0,0,0)` → drop at `(0,0,0)`.
6. **`OBDropResolver_NullTrajectoryReturnsOrigin`** — `Resolve(null, (5,2,7))` returns `(5,2,7)`.
7. **`AimRotationHelper_PointsTowardPin`** — `ballPos=(0,0,0)`, `pinPos=(10,0,10)` → yaw ≈ `π/4` (within 1e-4).
8. **`AimRotationHelper_FallsBackWhenPinUnset`** — `pinPos=Vector3.zero`, `fallbackYaw=1.5f` → returns `1.5f`.
9. **`HoleSessionDriver_ComputeNextTurn_AdvancesByOnePlusPenalty`** — `ComputeNextTurn(3, 0) == 4`; `ComputeNextTurn(3, 1) == 5`; `ComputeNextTurn(3, -1) == 4` (negative clamps to 0).

**Test gate:** `N → N+9 PASS, 0 IGNORED` where N is the current baseline. Implementer records actual baseline in `IMPLEMENTER_REPORT.md` before adding new tests. If any pre-existing test breaks on the baseline run, escalate `IMPLEMENTER_BLOCKED` — do NOT "fix" by editing existing tests.

**Test isolation:** new tests only exercise static helpers and value-type ctors. No `GameSession` static state mutation. No `[SetUp]` needed.

**Trajectory construction in tests:** use the existing public `Trajectory` constructor. `TerrainHit` ctor takes `(time, position, vIn, vOut, surface, isStop)` — set unused velocity fields to `fp3.Zero` and `isStop=false` for all but the final hit. Set `Trajectory.termination = TerminationReason.HitWater` (any OB cause; the resolver doesn't read termination).

## Smoke evidence

Three captures + one log artifact filed under `Docs/Specs/Active/loop_v1_2e_next_shot_handoff/screenshots/` with `controls_2e_*` prefix.

Use `CaptureCore.SnapWhenStateReached` for state-gated captures. NO `WaitForSeconds(N)` for state-dependent moments (Lesson controls_g).

1. **`controls_2e_atrest_facing_pin.png`** — load `Hole_01_Geo` additively. Pre-shot capture: note current camera yaw. Fire a driver shot biased off-line (NOT straight at pin). Wait for `BallState.AtRest`. Post-AtRest capture: camera frame should show the pin visibly forward in view (ball→pin direction). Implementer writes 2–3 sentence content-sanity description in `IMPLEMENTER_REPORT.md § Visual Verification` per Lesson O describing what the camera does (yaw rotation visible, pin in frame).

2. **`controls_2e_ob_drop.png`** — load `Hole_06_Geo` additively (lake present). Fire a driver shot biased into the lake (use `_shotController.CameraHeadingRadians` override if needed to ensure water hit). Wait for the OB→Aiming transition (one tick after OnShotComplete). Capture: ball should be visibly on grass, NOT in water. Implementer dumps `ShotExit termination=HitWater finalPos=...` + `[§2e] OB drop: ... to drop=(...)` log lines into `IMPLEMENTER_REPORT.md` to prove the drop happened.

3. **`controls_2e_turn_counter_after_ob.png`** — same hole as #2. Before shot: TURN reads 1. After OB drop + ReArm: TURN reads 3 (1 + 1 shot + 1 penalty = 3). Camera framed on the dropped ball. Load-bearing: TURN label visibly equals "TURN 3".

4. **`controls_2e_history_log.txt`** — after capture #2 fires, write to disk a dump of `GameSession.ShotHistory[0]` showing all 9 fields. Required line: `PenaltyStrokes=1`, `TerminalState="OB"`, `OBReason="Water"` (or whichever).

### Visual-fidelity verification (Lesson O)

§2e is visual-fidelity work — camera rotation, ball teleport, TURN HUD update. Per Lesson O, mode-history captures + screenshot files alone are dispatch evidence, not visual evidence. Implementer drives the lab manually for all three cases (AtRest, OB into water, tee shot OB with no safe trajectory hit) and writes a content-sanity description in `IMPLEMENTER_REPORT.md § Visual Verification` describing what they SAW happen in live play. Cesar approves the descriptions during architect review.

## Definition of Done

- `ShotRecord` has new `PenaltyStrokes` field + new 9-arg constructor + existing 8-arg constructor preserved as default-zero forward.
- `OBDropResolver.cs` shipped with `Resolve(Trajectory, Vector3 fallback) → Vector3` API.
- `AimRotationHelper.cs` shipped with `ComputeYawTowardPin(Vector3, Vector3, float) → float` API.
- `HoleSessionDriver.BuildShotRecord` populates `PenaltyStrokes=1` on OB.
- `HoleSessionDriver.HandleStateChanged` reads `change.Previous == BallState.OB` to advance TURN by 2 on OB→Aiming. Static helper `ComputeNextTurn` shipped + tested.
- `HoleSessionDriver.BuildShotRecordStatic` has a new 8-arg overload accepting `penaltyStrokes`; existing 7-arg overload preserved.
- `PhysicsLabController.HandleShotComplete` extended: AtRest rotates yaw to face pin before `ApplyCameraYaw`; OB calls `OBDropResolver` + `RepositionBallWithLookDir` + `_ballSM.ReArm()`; InCup unchanged.
- `PhysicsLabController.PlaceBallAt(Vector3, int?)` refactored to delegate to new private `RepositionBallWithLookDir(Vector3, int?, Vector3)` helper. Public signature unchanged.
- 9 new EditMode tests in `NextShotHandoffTests.cs`, all PASS. Test gate: **N+9 PASS, 0 IGNORED**.
- 3 captures + 1 history-log artifact filed under `Docs/Specs/Active/loop_v1_2e_next_shot_handoff/screenshots/`.
- Implementer's content-sanity descriptions in `IMPLEMENTER_REPORT.md § Visual Verification` cover all three cases: AtRest-with-fairway-rest, OB-into-water, tee-shot-into-water-no-prior-hit.
- Cesar manually plays through all three cases and confirms the behaviors in live play (Lesson O human gate).

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** if:
  - Refactoring `PlaceBallAt` breaks existing "Place Ball" dropdown behavior in lab smoke. Architect investigates whether `RepositionBallWithLookDir` needs additional context the original method had.
  - The OB drop teleport produces ball-Y-below-terrain at the drop point (heightmap mismatch). Likely `SurfaceSnap` returning wrong Y because the trajectory hit's Y was raycast-sampled while `SurfaceSnap` uses a different raycast. Architect investigates.
  - Any pre-existing test starts failing. Most likely cause: `ShotRecord` constructor change broke a test that constructed via positional args. Architect investigates and re-snapshots if behavior is genuinely unchanged.
- **`IMPLEMENTER_PARTIAL`** acceptable if:
  - First two captures land clean but capture #3 (TURN counter after OB) hits friction (additive scene reload flakiness). Architect closes with note + Cesar verifies live.

## Out of scope

- **OB penalty UI** (toast, banner, modal). Console log only. Deferred to Loop v2 polish.
- **Alternative OB rules.** §2e ships ONE drop rule (last safe trajectory hit + 1 penalty). Stroke-and-distance / lateral hazard / drop-zone variants are future work.
- **Auto-enter putter mode when ball rests on green.** §2f Putter P2 owns this.
- **Surface-aware club picker on AtRest.** §2f Putter P2.
- **Drop animation.** Ball teleports instantly. Animation is Loop v2 polish.
- **Persistent score across hole reload.** §2c rule — Loop v2 / save state spec.
- **Updating §2d's result screen to display penalty strokes.** Post-§2e cleanup if needed; not in this spec.
- **Hole-finished detection or hole-out integration.** §2d's `HoleCompleteDriver` owns that; §2e leaves InCup branch untouched.
- **Modifying `LoopCameraDirector`.** §2e mutates `_cameraYaw` only. Director mode dispatch unchanged. `OBFreeze → Chase` transition on OB→Aiming already works.
- **Trajectory line lifetime.** Already off project-wide (controls_i_ball_visual_rotation closeout 2026-05-12).

## Hard rules for implementer

1. **Do NOT modify** `BallStateMachine.cs`, `BallState.cs`, `BallStateChange.cs`, `ShotResult.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, `LoopCameraDirector.cs`, `HoleCompleteDriver.cs`, `HoleCompleteWidget.cs`, `RealCupDetector.cs`, any aero CSV, or any test currently in PASS state outside `NextShotHandoffTests.cs`.
2. **Do NOT modify** `PlayerCardWidget.cs` — already subscribed to `GameSession.OnTurnChanged`. Once TURN advances, the label updates.
3. **Do NOT modify `LabScaffold.unity`** via raw YAML or otherwise. No scene changes needed for §2e (no new MonoBehaviours, no Inspector wiring). If implementer needs to verify scene state, use Unity Editor MCP read-only APIs.
4. **Do NOT use `WaitForSeconds(N)`** for state-dependent captures. State-gate via `CaptureCore.SnapWhenStateReached` per controls_g lesson.
5. **Do NOT add new asmdef.** All new files live under existing `Golfin.Physics.Viewer` and `Golfin.Gameplay.UI.HUD` assemblies.
6. **Do NOT preempt §2f.** No surface-aware club switching, no auto-putter-mode-on-green. §2e is mechanics only.
7. **Bit-exact pre-existing test gate must hold.** Adding 9 tests to baseline N → N+9. If any pre-existing test starts failing, escalate `IMPLEMENTER_BLOCKED` immediately — do NOT "fix" by editing existing tests.
8. **Visual fidelity per Lesson O.** AtRest aim rotation, OB drop teleport, and TURN counter advancement all require live-play verification by the implementer. Mode-history captures alone are dispatch evidence, not visual evidence. Implementer writes a content-sanity description for each of the 3 captures in `IMPLEMENTER_REPORT.md § Visual Verification`.
9. **Implementer-PARTIAL → FAIL default** per `Docs/Architecture/REVIEW_PIPELINE_FIXES.md`. Reviewers do not soft-pass any PARTIAL items.
10. **Bbox geometry verification for any containment claim** per REVIEW_PIPELINE_FIXES. §2e has none today (no UI elements added), but if implementer adds any during the task, programmatic MCP bbox check is required — no qualitative override.
11. **Independent pixel scan FIRST** in self-review and architect review per REVIEW_PIPELINE_FIXES — open the capture, write 3–5 sentence description of what's visible, THEN read the IMPLEMENTER_REPORT.
