# SPEC — `loop_v1_2b_camera_transitions`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-07 JST.

## Goal

Centralize the camera lifecycle into a state-machine-driven `LoopCameraDirector` MonoBehaviour. Replace the nine scattered `chaseCamera.*` mutation sites in `PhysicsLabController` with a single subscriber to `BallStateMachine.OnStateChanged`. Add three new camera modes (`Downrange` for cinematic mid-flight cut, `CupZoom` for ball-drops-in moment, `OBFreeze` for OB tracking). Tighten chase framing. Co-ship CaptureHelper consolidation (closes both halves of the §2a OPEN FLAG). `ChaseCamera` remains a pure mode renderer with zero SM/state knowledge. Layer 1 physics untouched; this is additive UI/orchestration on top.

## Reference

- **Architect NOTES:** `Docs/Specs/Active/loop_v1_2b_camera_transitions/NOTES.md` (carries pre-spec analysis + the locked answers for Q1–Q5 and Q1'–Q5').
- **Roadmap entry:** `Docs/Roadmap.md` §2b.
- **§2a SPEC:** `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/SPEC.md` (the SM this subscribes to).
- **§2a OPEN FLAGS** in `Docs/TellCode.md` → CaptureHelper consolidation + capture-timing reliability (closed by this spec).
- **No Figma references** — this task is camera/orchestration logic, no UI surface.
- **Industry research notes** in NOTES.md §1 (PGA 2K23/EA Sports PGA Tour camera convention research) — informs cinematic cut framing.

## Background — what exists today

Verified by code walk 2026-05-07 (round 2):

| File | Role for this task |
|---|---|
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | ~80 lines, single MonoBehaviour. `enum Mode { Chase, Overhead, GroundLevel }`. `LateUpdate` switches on `_mode`, computes `desiredPos`/`desiredRot`, SmoothDamps + Slerps. Public API: `SetMode(Mode)`, `SetTarget(Transform)`, `ResetToOrigin(origin, launchDir)`, `FollowHeightOffset` setter. **Pure renderer — no SM/state knowledge.** |
| `Assets/Scripts/Gameplay/Loop/BallStateMachine.cs` | `event Action<BallStateChange> OnStateChanged` (fine, every transition) + `event Action<ShotResult> OnShotComplete` (coarse, terminal). |
| `Assets/Scripts/Gameplay/Loop/BallState.cs` | `Aiming, Flying, Rolling, AtRest, InCup, OB`. |
| `Assets/Scripts/Gameplay/Loop/BallStateChange.cs` | `{ from, to, position, surface, OBReason?, time }`. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 9 sites mutate `chaseCamera`. §2b relocates the state-driven sites; `AdjustCameraForDepression` + `HandleCameraOrbit` + Awake-time wiring stay. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabUI.cs:367` | Manual `CycleCamera(int dir)` button — **STAYS as lab debug per Cesar 2026-05-07. Director stomps overrides on next state transition; users understand transient behavior.** No code changes here. |
| `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` | Lab-only today. Hidden in gameplay scaffold by default per Q4'/L13. Disposition handled in spinoff spec `Docs/Specs/Queued/puttpath_predictor_perf_and_design/`. |
| `Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs` | Lab-debug visual; gets a `_showInGameplay` flag. |
| `Assets/Scripts/Editor/CaptureHelper.cs` | Editor-only today (per AI_CONTEXT). §2a inlined a byte-equivalent copy in `SmokeTestRunner2a`. Consolidation work below. |
| `Assets/Scripts/Physics/Core/Trajectory.cs` | Has `samples[]` + `terrainHits[]` (each carries `Surface` enum + `Position`). Source for OB-crossing detection + cinematic cut timing (predicted carry). |

## Locked decisions (carry forward from NOTES.md)

### Round 1 (architectural)
- **L1.** `LoopCameraDirector` MonoBehaviour mediates between SM and ChaseCamera.
- **L2.** Asmdef placement: stays in `Golfin.Physics.Viewer` for v1.
- **L3.** Subscribe to `OnStateChanged` (fine-grained).
- **L4.** Mode dispatch is pure data (`Dictionary<BallState, ChaseCamera.Mode>` + putt-aware override).
- **L5.** Existing SetClub putter→GroundLevel coupling stays in `PhysicsLabController.SetClub`.
- **L6.** Director wired in `PhysicsLabController.Awake` next to existing SM wiring.
- **L7.** `HandleShotResolved`'s camera calls (`SetTarget(ball)`, `ResetToOrigin`) and `HandleShotComplete`'s `SetTarget(null)` move from PhysicsLabController to Director.

### Round 2 (Cesar's directives + research-backed leans, all accepted)
- **L8 + Q1'a.** Cinematic cut at **65% of horizontal carry**. Trigger derived from `Trajectory` post-`OnTrajectoryComputed`, no SM changes.
- **Q1'b.** Downrange framing: **behind the predicted landing zone, looking back along flight line** (PGA 2K23 canonical).
- **Q1'c.** **Putts skip the cinematic cut entirely.** Director checks `isPutt` flag on Flying entry; if putt, no Downrange — putter framing held throughout shot.
- **L9 + Q3'a.** OBFreeze pivot = first OB-classified sample's XZ + 5m above terrain Y. Camera position locked, rotation tracks ball.
- **L10 + Q2'.** Chase framing retune: **5m back / 2.5m up, FOV unchanged**.
- **L11.** CupZoom = tween hover above flat circle (not dive into geometry — cup is currently flat).
- **L12 + Q4'.** No deletion of `PhysicsLabUI.CycleCamera` — kept as transient lab debug. Director is authoritative; manual overrides survive until next state transition.
- **L13.** TrajectoryRenderer gameplay-hide flag. PuttPathPredictor hidden in gameplay scaffold. ShotConeView keep on.
- **L14 + Q5.** Director is MonoBehaviour. Inspector-wires `chaseCamera`, gets `_ballSM` from PhysicsLabController via internal accessor.
- **L15 + Q5'.** Co-ship CaptureHelper consolidation — both parts (asmdef move + SM-gated API).

## Architecture context

- **No new asmdef.** Director, ChaseCamera, and all relocated calls live in `Golfin.Physics.Viewer` per L2.
- **One new asmdef for CaptureHelper consolidation:** `Assets/Scripts/Diagnostics/Runtime/Golfin.Diagnostics.Runtime.asmdef`, namespace `Golfin.Diagnostics.Runtime`. References: `Golfin.Gameplay.Loop` (for `BallStateMachine` / `BallState`). `autoReferenced: true`. References from `Golfin.Physics.Viewer` and `Golfin.EditorTools` updated.
- **No changes to:** `Golfin.Physics.Core`, `Golfin.Physics.Stats`, `Golfin.Physics.Runtime`, `Golfin.Physics.Math`, `Golfin.Gameplay.Input`, `Golfin.Gameplay.Loop`, `Golfin.Gameplay.UI`.

## Implementation

### A. New `LoopCameraDirector` MonoBehaviour

**Location:** `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`. Namespace `Golfin.Physics.Viewer`.

**Inspector fields:**
```csharp
[SerializeField] ChaseCamera chaseCamera;
[SerializeField] PhysicsLabController controller; // for accessor to _ballSM and _shotController
[Header("Cinematic")]
[SerializeField] float cinematicCutAtCarryFraction = 0.65f;   // L8
[SerializeField] float minCarryForCinematicMeters  = 30f;    // skip cut on chips/short shots
[Header("Downrange framing")]
[SerializeField] float downrangePastLandingMeters = 12f;     // distance past landing zone
[SerializeField] float downrangeHeightMeters       = 4f;
[Header("CupZoom framing")]
[SerializeField] float cupZoomHoverHeightMeters    = 2.5f;   // L11 — hovers above flat circle
[SerializeField] float cupZoomTweenSeconds         = 1.0f;
[Header("OBFreeze framing")]
[SerializeField] float obFreezeHeightAboveTerrain  = 5f;     // L9 / Q3'a
```

**State→Mode dispatch table** (private readonly):
```csharp
static readonly Dictionary<BallState, ChaseCamera.Mode?> ModeMap = new()
{
    { BallState.Aiming,  null },                   // leave whatever was set (club-driven GroundLevel survives)
    { BallState.Flying,  ChaseCamera.Mode.Chase }, // initial; cinematic cut promotes to Downrange mid-flight
    { BallState.Rolling, ChaseCamera.Mode.Chase }, // back to chase after touchdown
    { BallState.AtRest,  ChaseCamera.Mode.Chase },
    { BallState.InCup,   ChaseCamera.Mode.CupZoom },
    { BallState.OB,      ChaseCamera.Mode.OBFreeze },
};
```

**Subscription lifecycle** (Awake/OnDestroy):
```csharp
void Awake()
{
    if (controller == null) controller = GetComponentInParent<PhysicsLabController>();
    var sm = controller?.BallSM;  // new internal accessor on controller
    if (sm != null) sm.OnStateChanged += HandleStateChanged;
}
void OnDestroy()
{
    var sm = controller?.BallSM;
    if (sm != null) sm.OnStateChanged -= HandleStateChanged;
}
```

**Core handler:**
```csharp
void HandleStateChanged(BallStateChange change)
{
    // Q1'c: putts skip cinematic cut. Putt detection comes from controller's current shot mode.
    bool isPutt = controller != null && controller.CurrentShotIsPutt;
    
    // Aiming → Flying: arm chase target + reset origin (relocated from PhysicsLabController.HandleShotResolved)
    if (change.to == BallState.Flying && change.from == BallState.Aiming)
    {
        ArmChaseForShot(controller.LastShotOrigin, controller.LastShotLaunchDir, controller.CurrentBall);
    }
    
    // InCup: setup cup zoom focus before mode switch
    if (change.to == BallState.InCup)
    {
        chaseCamera.SetCupZoomFocus(change.position.ToVector3());
    }
    
    // OB: setup freeze pivot before mode switch
    if (change.to == BallState.OB)
    {
        var pivot = ComputeOBFreezePivot(change.position.ToVector3());
        chaseCamera.SetOBFreezePivot(pivot);
    }
    
    // Apply mode mapping (skip if null = "leave unchanged")
    if (ModeMap.TryGetValue(change.to, out var mode) && mode.HasValue)
    {
        // Putt-aware override: putts stay in their current putter framing through Flying/Rolling/AtRest
        if (isPutt && (change.to == BallState.Flying || change.to == BallState.Rolling || change.to == BallState.AtRest))
            return; // leave whatever putter framing is currently set
        chaseCamera.SetMode(mode.Value);
    }
    
    // Terminal states clear the chase target (relocated from PhysicsLabController.HandleShotComplete)
    if (change.to == BallState.AtRest || change.to == BallState.InCup || change.to == BallState.OB)
    {
        chaseCamera.SetTarget(null);
    }
}
```

**Cinematic cut driver** (Update or coroutine; runs only during Flying-not-putt):
```csharp
void Update()
{
    if (controller == null) return;
    if (controller.BallSM?.State != BallState.Flying) return;
    if (controller.CurrentShotIsPutt) return;
    if (chaseCamera.CurrentMode == ChaseCamera.Mode.Downrange) return; // already cut
    
    var traj = controller.LastTrajectory;
    if (traj == null) return;
    
    // Compute predicted landing carry (XZ distance from origin to first non-stop terrain hit, OR final position fallback)
    float predictedCarry = ComputePredictedCarry(traj, controller.LastShotOrigin);
    if (predictedCarry < minCarryForCinematicMeters) return;
    
    // Compute current ball XZ progress
    float currentProgress = ComputeCurrentXZProgress(controller.CurrentBall.position, controller.LastShotOrigin, controller.LastShotLaunchDir);
    
    if (currentProgress / predictedCarry >= cinematicCutAtCarryFraction)
    {
        // Configure downrange framing: position past landing zone, look back along flight line
        Vector3 landingPos = ComputeLandingPos(traj);
        Vector3 backDir = -controller.LastShotLaunchDir; // back along flight line
        Vector3 downrangePos = landingPos - controller.LastShotLaunchDir * downrangePastLandingMeters
                             + Vector3.up * downrangeHeightMeters;
        chaseCamera.SetDownrangeFraming(downrangePos, landingPos);
        chaseCamera.SetMode(ChaseCamera.Mode.Downrange);
    }
}
```

**OBFreeze pivot computation** (from L9 / Q3'a):
```csharp
Vector3 ComputeOBFreezePivot(Vector3 fallback)
{
    var traj = controller?.LastTrajectory;
    if (traj?.terrainHits == null) return fallback + Vector3.up * obFreezeHeightAboveTerrain;
    
    foreach (var hit in traj.terrainHits)
    {
        if (hit.Surface == SurfaceType.Water || hit.Surface == SurfaceType.OOB)
        {
            return new Vector3(hit.Position.x.ToFloat(),
                               hit.Position.y.ToFloat() + obFreezeHeightAboveTerrain,
                               hit.Position.z.ToFloat());
        }
    }
    
    // No water/OOB hit found — termination was ExitedWorldBounds; use the change position
    return new Vector3(fallback.x, fallback.y + obFreezeHeightAboveTerrain, fallback.z);
}
```

### B. Internal accessors on `PhysicsLabController`

Add (or surface as `internal` if not yet public):

```csharp
internal Golfin.Gameplay.Loop.BallStateMachine BallSM => _ballSM;
internal Trajectory LastTrajectory => _previousTrajectory;
internal Vector3 LastShotOrigin    => _lastShotOrigin;     // cache in HandleShotResolved
internal Vector3 LastShotLaunchDir => _lastShotLaunchDir;  // cache in HandleShotResolved
internal Transform CurrentBall      => ballAnimator?.CurrentBall;
internal bool CurrentShotIsPutt    => _shotController != null && _shotController.IsPutt;
```

`_lastShotOrigin` and `_lastShotLaunchDir` are new private fields, cached at the top of `HandleShotResolved` from the existing `origin` and `launchDir` locals (lines 700-705 in current file).

### C. Relocate camera calls from PhysicsLabController

**Remove from `HandleShotResolved`** (lines 709-713 in current file):
```csharp
// REMOVE:
if (chaseCamera != null)
{
    chaseCamera.SetTarget(ballAnimator.CurrentBall);
    chaseCamera.ResetToOrigin(origin, launchDir);
}
```
**Replaced by:** Director's `HandleStateChanged` → `ArmChaseForShot` on Aiming→Flying transition.

**Remove from `HandleShotComplete`** (line 765):
```csharp
// REMOVE:
if (chaseCamera != null) chaseCamera.SetTarget(null);
```
**Replaced by:** Director's `HandleStateChanged` → terminal-state target clear.

**Leave alone in `FireInternal`** (lines 825-829, preset shot path):
```csharp
// KEEP — preset shots don't go through the SM (per §2a accepted deviation _prevBallPlaying).
// Director coexists; preset path remains scaffold-driven.
if (chaseCamera != null)
{
    chaseCamera.SetTarget(ballAnimator.CurrentBall);
    chaseCamera.ResetToOrigin(origin, launchDir);
}
```

**Leave alone:** `AdjustCameraForDepression`, `HandleCameraOrbit`, Awake-time wiring (camera component, depth/color enable, HUD widget camera setters), `SetupAtTee`/`PlaceBallAt` putter→GroundLevel logic.

### D. New ChaseCamera modes

Extend `ChaseCamera.Mode` enum:
```csharp
public enum Mode { Chase, Overhead, GroundLevel, Downrange, CupZoom, OBFreeze }
```

**New private state:**
```csharp
Vector3 _downrangePos;       // set by SetDownrangeFraming
Vector3 _downrangeLookAt;    // set by SetDownrangeFraming
Vector3 _cupZoomFocus;       // set by SetCupZoomFocus
float   _cupZoomStartTime;   // for tween progress
Vector3 _cupZoomStartPos;    // captured on mode entry
Vector3 _obFreezePivot;      // set by SetOBFreezePivot
```

**New public API:**
```csharp
public void SetDownrangeFraming(Vector3 pos, Vector3 lookAt)
{
    _downrangePos    = pos;
    _downrangeLookAt = lookAt;
}
public void SetCupZoomFocus(Vector3 focus) => _cupZoomFocus = focus;
public void SetOBFreezePivot(Vector3 pivot) => _obFreezePivot = pivot;
```

**Mode-entry hook in `SetMode`:**
```csharp
public void SetMode(Mode m)
{
    if (m == Mode.CupZoom && _mode != Mode.CupZoom)
    {
        _cupZoomStartTime = Time.time;
        _cupZoomStartPos  = transform.position;
    }
    _mode = m;
}
```

**New `LateUpdate` cases** (added to existing switch):
```csharp
case Mode.Downrange:
    desiredPos = _downrangePos;
    desiredRot = Quaternion.LookRotation(_downrangeLookAt - desiredPos);
    break;

case Mode.CupZoom:
{
    // Tween from start position to hover-above-cup over cupZoomTweenSeconds
    float t = Mathf.Clamp01((Time.time - _cupZoomStartTime) / 1.0f); // 1.0s tween hardcoded; Director can override via inspector if needed
    Vector3 hoverPos = _cupZoomFocus + Vector3.up * 2.5f; // L11: hover, don't dive
    desiredPos = Vector3.Lerp(_cupZoomStartPos, hoverPos, EaseOutCubic(t));
    desiredRot = Quaternion.LookRotation(_cupZoomFocus - desiredPos);
    break;
}

case Mode.OBFreeze:
    desiredPos = _obFreezePivot;
    Vector3 ballPos = focus; // ball position from existing focus calculation
    desiredRot = Quaternion.LookRotation(ballPos - _obFreezePivot);
    break;
```

**Helper** (private):
```csharp
static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
```

**Existing `Chase` case retuned per L10:**
```csharp
default: // Chase
    desiredPos = focus - _launchDir * 5f + Vector3.up * (2.5f + FollowHeightOffset); // was 8f and 3f
    desiredRot = Quaternion.LookRotation(focus - desiredPos);
    break;
```

### E. TrajectoryRenderer gameplay-hide flag

Add to `Assets/Scripts/Physics/Viewer/TrajectoryRenderer.cs`:
```csharp
[Header("Visibility")]
[SerializeField] bool _showInGameplay = false;
public bool ShowInGameplay { get => _showInGameplay; set => _showInGameplay = value; }
```

In `Draw(Trajectory)`:
```csharp
public void Draw(Trajectory traj)
{
    // Existing path...
    
    // New gate: hide when not in gameplay-allowed mode
    if (!_showInGameplay && !Application.isEditor) // editor always shows for lab work
        return;
    
    // ...rest of draw logic
}
```

LabScaffold scene: `_showInGameplay = true` (or leave default — editor gate keeps it visible during lab dev). Future GameplayScaffold scene: explicit `false`.

### F. CaptureHelper consolidation — Part 1 (asmdef move)

**Create:** `Assets/Scripts/Diagnostics/Runtime/Golfin.Diagnostics.Runtime.asmdef` with:
```json
{
    "name": "Golfin.Diagnostics.Runtime",
    "rootNamespace": "Golfin.Diagnostics.Runtime",
    "references": ["Golfin.Gameplay.Loop"],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

**Move:** the `SnapAtEndOfFrameAndPause` static method (and dependencies: RT reflection, Y-flip, file write) from `Assets/Scripts/Editor/CaptureHelper.cs` to `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs`. Namespace `Golfin.Diagnostics.Runtime`. Static class.

**Editor wrapper:** `Assets/Scripts/Editor/CaptureHelper.cs` becomes a thin re-export of `Golfin.Diagnostics.Runtime.CaptureCore` methods, plus any editor-only menu items (`GOLFIN > Capture > ...`). Existing call sites in editor code keep working.

**Update `Golfin.Physics.Viewer.asmdef`** references to include `Golfin.Diagnostics.Runtime` (so SmokeTestRunner2a can call it from runtime asmdef).

**Delete** the inline byte-equivalent copy from `SmokeTestRunner2a.cs` (the duplicated capture method introduced in §2a iter-4); replace with a call to `Golfin.Diagnostics.Runtime.CaptureCore.SnapAtEndOfFrameAndPause(...)`.

### G. CaptureHelper consolidation — Part 2 (SM-gated capture API)

**Add to `Golfin.Diagnostics.Runtime.CaptureCore`:**
```csharp
/// <summary>
/// Subscribes to the SM's OnStateChanged event, snaps a frame the moment the target
/// state is entered, then unsubscribes. Uses SnapAtEndOfFrameAndPause for the actual
/// snap so the capture is at-rest-deterministic.
/// </summary>
public static void SnapWhenStateReached(
    Golfin.Gameplay.Loop.BallStateMachine sm,
    Golfin.Gameplay.Loop.BallState target,
    string label,
    string outputPath = null)
{
    if (sm == null) throw new ArgumentNullException(nameof(sm));
    
    Action<Golfin.Gameplay.Loop.BallStateChange> handler = null;
    handler = (change) =>
    {
        if (change.to != target) return;
        sm.OnStateChanged -= handler;
        SnapAtEndOfFrameAndPause(label, outputPath);
    };
    sm.OnStateChanged += handler;
}
```

This closes the §2a OPEN FLAG: future smoke tests get capture timing gated on SM state instead of on frame-count or animator-IsPlaying polling.

## Tests

**Location:** `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` (new file). Asmdef: `Golfin.Physics.Tests` (already references `Golfin.Gameplay.Loop` per §2a).

**Test seam:** Director must accept an `IModeSetter` interface in addition to direct ChaseCamera reference, so tests can verify mode transitions without instantiating a Camera GO. Define:
```csharp
public interface IModeSetter
{
    void SetMode(ChaseCamera.Mode mode);
    void SetTarget(Transform t);
    void ResetToOrigin(Vector3 origin, Vector3 launchDir);
    void SetDownrangeFraming(Vector3 pos, Vector3 lookAt);
    void SetCupZoomFocus(Vector3 focus);
    void SetOBFreezePivot(Vector3 pivot);
    ChaseCamera.Mode CurrentMode { get; }
}
```
ChaseCamera implements this (trivial — its existing API already matches). Tests use a `RecordingModeSetter` that captures all calls into a list.

**Required tests** (minimum 6):

1. **`Director_OnFlyingEntry_NonPutt_SetsChaseMode`** — fire SM Aiming→Flying with isPutt=false, assert `RecordingModeSetter` last `SetMode` call was `Chase` and `SetTarget`/`ResetToOrigin` were called.
2. **`Director_OnFlyingEntry_Putt_SkipsModeChange`** — same as above with isPutt=true, assert no `SetMode` call was made (putt framing preserved).
3. **`Director_OnInCup_SetsCupZoomMode_AndSetsCupZoomFocus`** — fire SM Rolling→InCup, assert `SetCupZoomFocus` called with the change position, then `SetMode(CupZoom)`.
4. **`Director_OnOB_FreezesAtFirstWaterHitXZ`** — construct trajectory with terrainHits = [Fairway hit at (10,0,0), Water hit at (25,0,5)], fire SM Flying→OB, assert `SetOBFreezePivot` called with (25, 5+obFreezeHeightAboveTerrain, 5).
5. **`Director_OnOB_NoWaterHit_FallsBackToChangePosition`** — trajectory with no Water/OOB hits (e.g. ExitedWorldBounds termination), assert pivot uses change.position + height offset.
6. **`Director_OnTerminalState_ClearsTarget`** — fire any of AtRest/InCup/OB, assert `SetTarget(null)` called.
7. **`Director_CinematicCut_FiresAt65PercentCarry`** — drive Update with mock controller reporting predictedCarry=100, currentProgress=70 (=0.7 ratio), assert `SetMode(Downrange)` called and `SetDownrangeFraming` set with positions consistent with downrange formula.
8. **`Director_CinematicCut_DoesNotFireOnPutt`** — same setup with isPutt=true, assert no Downrange transition.
9. **`Director_CinematicCut_DoesNotFireBelowMinCarry`** — predictedCarry=20 (< minCarryForCinematicMeters=30), assert no Downrange transition.

**Test gate:** **227/227 pre-existing PASS, 0 IGNORED. New tests additive → 235+/235+ PASS.**

## Smoke evidence (post-§2a lessons)

Per `Docs/Diagnostics/PIPELINE_LESSONS.md` Lessons M and N: any "screenshot taken" claim requires (a) file persisted to disk on parallel-path verification AND (b) a deterministic capture trigger.

Use the new `CaptureCore.SnapWhenStateReached(sm, target, label)` API for all smoke captures:

- Drive lab session, hit driver shot end-to-end on Hole_01.
- Schedule captures: `Aiming` (pre-flick), `Flying` (after cinematic cut should have fired, ~1.5s in), `Rolling`, `AtRest`.
- Verify each captured frame shows the expected camera framing (chase early, downrange after cut, chase on touchdown, settled chase at rest).
- Repeat with putter shot on green: assert no Downrange cut fired, framing stays in GroundLevel throughout.
- Repeat with shot into water (placement OB tee): assert OBFreeze fires and camera position locks at first Water hit XZ.
- All capture filenames + timestamps + on-disk file sizes reported in IMPLEMENTER_REPORT.md.

## Definition of Done

- `LoopCameraDirector` MonoBehaviour shipped, Inspector-wired in `LabScaffold.unity`, subscribed to `_ballSM.OnStateChanged`.
- Three new `ChaseCamera.Mode` values implemented: `Downrange`, `CupZoom`, `OBFreeze`. Existing modes (`Chase`, `Overhead`, `GroundLevel`) unchanged in behavior except `Chase` retuned to 5m/2.5m.
- `PhysicsLabController` relocations: `HandleShotResolved` and `HandleShotComplete` no longer call `chaseCamera.SetTarget` or `chaseCamera.ResetToOrigin`. `FireInternal` (preset path) keeps its calls. Internal accessors added per §B.
- `TrajectoryRenderer._showInGameplay` flag added; lab default keeps current behavior visible.
- `Golfin.Diagnostics.Runtime` asmdef created; `CaptureCore.SnapAtEndOfFrameAndPause` lives there; editor-side `CaptureHelper.cs` becomes thin wrapper; inline byte-equivalent copy in `SmokeTestRunner2a` replaced with call into `CaptureCore`.
- `CaptureCore.SnapWhenStateReached(sm, target, label)` API shipped.
- 9 new EditMode tests in `LoopCameraDirectorTests.cs` (per list above), all PASS.
- Test gate: 227/227 pre-existing PASS, 0 IGNORED → 236/236 PASS post-additive.
- Smoke evidence per § above: 4–6 captured frames with on-disk file paths + sizes + content-sanity descriptions in IMPLEMENTER_REPORT.md.
- Two §2a OPEN FLAGs closed in TellCode (CaptureHelper consolidation + capture-timing reliability).

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** — escalate to architect if:
  - The 227-test gate breaks unexpectedly (any test that wasn't supposed to be touched starts failing). Symptoms: bit-exact regression, unrelated test failures, NaN/Infinity. Architect investigates whether the relocation introduced an off-by-one in HandleShotResolved or whether asmdef shuffling broke a reference.
  - The cinematic cut formula produces visibly broken results in playtest (e.g. camera ends up underground, behind a tree, or facing the wrong way). Architect re-evaluates `downrangePastLandingMeters` + `downrangeHeightMeters` defaults.
  - CaptureHelper Part 1 asmdef move breaks editor menu items (`GOLFIN > Capture > ...`). Architect resequences: asmdef stays, but editor-side wrapper needs adjustment.
- **`IMPLEMENTER_PARTIAL`** — implementer ships A–E but Part 1+2 (CaptureHelper) hits unforeseen friction. Acceptable to ship §2b without CaptureHelper consolidation if blocked; OPEN FLAG stays open; architect reviews and decides whether to extract CaptureHelper into its own follow-up spec.

## Out of scope

- **PuttPathPredictor disposition** — handled in spinoff spec `Docs/Specs/Queued/puttpath_predictor_perf_and_design/`. §2b hides it in any gameplay scaffold default; lab keeps current behavior.
- **CupZoom on real 3D cup geometry** — currently flat circle. Revisit when cup geometry lands.
- **Side-cam variant** (90° to flight line) — Cesar's "side camera sometimes" was answered by the downrange framing (which IS the side-cam-ish variant when launchDir is east-west and cam goes downrange). Pure perpendicular side-cam is a v2 add for variety; not v1.
- **Per-state animation timing tuning** — CupZoom uses 1.0s tween, OBFreeze is instant freeze. Both will need playtest iteration; not part of this spec's gate.
- **Director removal of Overhead mode** — Overhead survives but isn't state-mapped. Cleanup follow-up if useless.
- **Real cup detection** — still stubbed via `NullCupDetector` from §2a. CupZoom dispatch path is dead code in v1 until §2d wires real detection.
- **OB on very-low-altitude shanks** — freeze pivot near player position will look uninspired. Acceptable v1; revisit if it feels broken.

## Hard rules for implementer

1. **Do NOT modify** `BallSimulation.cs`, `Trajectory.cs`, `TrajectorySample.cs`, `BallStateMachine.cs`, `BallState.cs`, `BallStateChange.cs`, `ShotResult.cs`, `fpMath.cs`, any CSV in `Resources/Configs/`, any test currently in PASS state outside `LoopCameraDirectorTests.cs`.
2. **Do NOT delete** `PhysicsLabUI.CycleCamera` or its button. Cesar locked: lab debug stays, Director stomps overrides on next state transition (transient behavior is correct).
3. **Do NOT widen the cinematic cut to putts.** The `isPutt` skip is load-bearing — putts are short and the cut would feel jarring.
4. **Do NOT change FOV** on the Camera component. L10 locks "5m back / 2.5m up, FOV unchanged."
5. **Do NOT introduce non-determinism** into the SM. Director uses `Time.time`/`Time.deltaTime` freely (visual layer only); SM and BallSimulation must remain deterministic.
6. **Do NOT bake-claim screenshots.** Per §2a Lesson N: every captured frame in IMPLEMENTER_REPORT requires on-disk file path + size + parallel-path Read verification. No Roslyn-only / in-memory captures pass review.
7. **Do NOT skip the `IModeSetter` test seam.** Director tests must run without instantiating a Camera GO; the interface is non-optional.
