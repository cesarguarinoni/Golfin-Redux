# SPEC — `controls_g_smoke_followup`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Architect-locked at SPEC_READY 2026-05-07 19:25 JST.

## Goal

Close the §2b deferred-smoke OPEN flag by capturing three runtime visual confirmations of cinematic camera modes that controls_g failed to capture due to fragile timing. Three captures: (1) Downrange driver shot at 65%+ horizontal carry, (2) putter GroundLevel preserved through Flying state, (3) OBFreeze with locked pivot. State-driven via new `LoopCameraDirector.OnModeChanged` event + new `CaptureCore.SnapWhenModeReached` API. Loaded against real terrain (Hole_01_Geo for Downrange + putter, Hole_06_Geo for OBFreeze).

## Reference

- **Architect NOTES:** `Docs/Specs/Active/controls_g_smoke_followup/NOTES.md` (carries pre-spec analysis + Options A/B/C tradeoff + locked answers to Q1–Q5).
- **Origin:** `Docs/Specs/Completed/controls_g_aero_constant_mode_crash/ARCHITECT_REVIEW.md` § "ADDENDUM — Human Architect ruling" — surfaced this followup and queued the task. Reviewer's "Smoke-Runner Timed Waits" lesson is the architectural backing.
- **§2b spec:** `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SPEC.md` § "Tests" — the 9 LoopCameraDirectorTests this followup expands by one.
- **§2a Lessons M+N** in `Docs/Diagnostics/PIPELINE_LESSONS.md` — capture-on-disk verification protocol.
- **controls_g lessons** in `tasks/lessons.md`: "Defense-in-Depth Fixes Can Mask the Original Regression Site" + "Smoke-Runner Timed Waits Are Fragile Against Shot-Power and Carry Changes" — both informing this task's design.

## Background — what exists today

Verified by code walk 2026-05-07 19:20 JST.

| File | Role for this task |
|---|---|
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | §2b Director MonoBehaviour. Has private `SetMode`-equivalent mutation paths in `HandleStateChanged` and `Update` (cinematic cut). Needs new `OnModeChanged` event + raise sites. |
| `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs` | §2b CaptureCore. Already has `SnapAtEndOfFrameAndPause`, `SnapGameViewWithLabel`, `SnapWhenStateReached(MonoBehaviour owner, BallStateMachine sm, BallState target, string label, ...)`. Add `SnapWhenModeReached(MonoBehaviour owner, LoopCameraDirector dir, ChaseCamera.Mode target, string label, ...)` mirroring the existing one-shot pattern. |
| `Assets/Scripts/Physics/Viewer/SmokeTestRunner2b.cs` | controls_g's smoke runner. Currently uses fragile `WaitForSeconds(3f)` gates. **Rewrite to use SnapWhenModeReached + load Hole_01_Geo / Hole_06_Geo additively.** |
| `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` | §2b's 9 EditMode Director tests. Add 1 new test for `OnModeChanged` event. |
| `Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity` | Hole 1 scene. Used for Downrange + putter captures. Verified exists. |
| `Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity` | Hole 6 scene. Used for OBFreeze capture per Cesar lock — Hole 1's water hazard was removed long ago. Verified exists 2026-05-07 19:22 JST. |
| `Assets/Scenes/Physics/LabScaffold.unity` | Lab base scene. SmokeTestRunner2b lives here per controls_g (raw YAML edit per deviation #3 — Cesar verified clean before this task starts per kickoff TODO). |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | Owns `ballAnimator`, `ShotController`, club selection. SmokeTestRunner2b calls into this for shot setup/firing. |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | §2b enum `Mode { Chase, Overhead, GroundLevel, Downrange, CupZoom, OBFreeze }`. The `Mode` type is the param to `OnModeChanged`. |

## Locked decisions (carry forward from NOTES.md)

- **Q1 — A + C combined.** Add `Director.OnModeChanged` event + `CaptureCore.SnapWhenModeReached` API (Option A). Load `Hole_01_Geo` / `Hole_06_Geo` additively (Option C). Both shipped this task.
- **Q2 — OBFreeze on Hole_06.** Hole_01_Geo's water hazard was removed long ago. Use Hole_06_Geo for the OBFreeze capture. Implementer picks the specific water-bordered tee placement on Hole_06 that produces a driver shot landing in water within ~3 seconds; documents in IMPLEMENTER_REPORT.
- **Q3 — Add Director EditMode test for `OnModeChanged`.** One new test: `Director_OnModeChange_RaisesEventWithNewMode`. ~10 lines.
- **Q4 — `SnapWhenModeReached` lives in same `CaptureCore.cs` file as `SnapWhenStateReached`.** Same family of state-gated captures.
- **Q5 — PASS gate target: 241/241.** 240 (controls_g) + 1 new Director event test. 0 IGNORED.

## Architecture context

- **No new asmdef.** Director changes in `Golfin.Physics.Viewer`. CaptureCore additions in `Golfin.Diagnostics.Runtime`. SmokeTestRunner2b rewrite in `Golfin.Physics.Viewer`. Director test additions in `Golfin.Physics.Tests`.
- **No changes to** `Golfin.Physics.Core`, `Golfin.Physics.Stats`, `Golfin.Physics.Math`, `Golfin.Physics.Runtime`, `Golfin.Gameplay.Loop`, `Golfin.Gameplay.Input`, `Golfin.Gameplay.UI`, any aero CSV.
- **Director.OnModeChanged is observable but not load-bearing.** §2b's existing dispatch logic stays unchanged — the event is RAISED but not REQUIRED for any production code path. Removing the subscriber (e.g. SmokeTestRunner2b) leaves Director behavior identical.

## Implementation

### A — `LoopCameraDirector.OnModeChanged` event

**Location:** `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs`.

Add a public event:
```csharp
/// <summary>
/// Raised whenever the Director changes the camera mode (whether at SM state transitions
/// or at the mid-flight cinematic cut). Subscribers receive the new mode value.
/// Not load-bearing for production logic — used by smoke runners and debug tools.
/// </summary>
public event System.Action<ChaseCamera.Mode> OnModeChanged;
```

**Raise sites:** wrap every `chaseCamera.SetMode(...)` call in a helper that fires the event:
```csharp
void ApplyMode(ChaseCamera.Mode mode)
{
    chaseCamera.SetMode(mode);
    OnModeChanged?.Invoke(mode);
}
```

Replace ALL existing `chaseCamera.SetMode(mode)` and `chaseCamera.SetMode(mode.Value)` calls in Director with `ApplyMode(...)`. Audit comment list (verify against current Director source):
- `HandleStateChanged` — the dispatch-table apply at the bottom
- `Update` — the cinematic-cut promotion to `Downrange`
- Any other site `SetMode` is called

Do NOT touch `chaseCamera.SetMode` calls outside the Director (e.g. `PhysicsLabUI.CycleCamera` lab debug button — that's transient lab-only, intentionally not Director-routed).

### B — `CaptureCore.SnapWhenModeReached`

**Location:** `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs`. Add in same file as `SnapWhenStateReached` per Q4.

```csharp
/// <summary>
/// Subscribes to the Director's OnModeChanged event, snaps a frame the moment the target
/// mode is entered, then unsubscribes. Uses SnapAtEndOfFrameAndPause for the actual snap
/// so the capture is at-rest-deterministic. One-shot — fires once and unsubscribes.
/// </summary>
public static void SnapWhenModeReached(
    MonoBehaviour owner,
    Golfin.Physics.Viewer.LoopCameraDirector director,
    Golfin.Physics.Viewer.ChaseCamera.Mode target,
    string label,
    string outputPath = null)
{
    if (owner == null) throw new System.ArgumentNullException(nameof(owner));
    if (director == null) throw new System.ArgumentNullException(nameof(director));
    
    System.Action<Golfin.Physics.Viewer.ChaseCamera.Mode> handler = null;
    handler = (mode) =>
    {
        if (mode != target) return;
        director.OnModeChanged -= handler;
        SnapAtEndOfFrameAndPause(owner, label, outputPath);
    };
    director.OnModeChanged += handler;
}
```

**Asmdef impact:** `Golfin.Diagnostics.Runtime` already references `Golfin.Gameplay.Loop` for the existing `SnapWhenStateReached`. Adding a reference to `Golfin.Physics.Viewer` (for `LoopCameraDirector` + `ChaseCamera.Mode`) — verify no circular dependency. Viewer references `Golfin.Diagnostics.Runtime` (added in §2b for CaptureCore consumption from SmokeTestRunner2a). **If circular:** move just the `ChaseCamera.Mode` enum to `Golfin.Diagnostics.Runtime` (it's a pure enum with no deps), or use late binding via a `System.Action<int>` cast. Architect lean: try direct reference first; if circular, escalate `IMPLEMENTER_BLOCKED` and architect resolves.

### C — `SmokeTestRunner2b` rewrite

**Location:** `Assets/Scripts/Physics/Viewer/SmokeTestRunner2b.cs`.

Replace the existing `WaitForSeconds(3f)`-gated capture logic with state-driven captures. Three test sequences:

#### C.1 — Downrange capture sequence
1. Load `Hole_01_Geo.unity` additively via `SceneManager.LoadSceneAsync(..., LoadSceneMode.Additive)`.
2. Wait for scene loaded + `OnHoleLoaded` callback in PhysicsLabController to wire the heightmap (existing behavior).
3. Position ball at Hole_01 tee (use existing `SetupAtTee` or equivalent).
4. Set club to driver. Set power to 0.85 (high enough to fly past 65% carry threshold).
5. Get reference to `LoopCameraDirector` from scene.
6. Schedule capture: `CaptureCore.SnapWhenModeReached(this, director, ChaseCamera.Mode.Downrange, "controls_g_followup_downrange");`
7. Fire shot via `ShotController.CommitFlick`.
8. Wait for `BallStateMachine.OnShotComplete` (signals end-of-shot for cleanup, not capture timing).
9. Unload Hole_01_Geo.

#### C.2 — Putter GroundLevel-preserved capture sequence
1. Hole_01_Geo already unloaded after C.1; reload additively if needed.
2. Position ball ON the green near the cup (use existing `PlaceBallAt` / putter convenience).
3. Set club to putter. Verify `PhysicsLabController.CurrentShotIsPutt == true`.
4. Schedule capture: `CaptureCore.SnapWhenStateReached(this, ballSM, BallState.Rolling, "controls_g_followup_putter_groundlevel");`
   - Captures during Rolling (not Flying entry, which is too brief) — confirms GroundLevel framing held through the shot.
5. Fire putter shot at low power (0.2).
6. After capture fires, assert that Director's most recent mode change was NOT `Downrange` — log the captured `Director.OnModeChanged` history and verify `Downrange` does not appear during this shot. Use a recorded mode-history list filled by subscribing to OnModeChanged.
7. Wait for `OnShotComplete`.
8. Unload Hole_01_Geo.

#### C.3 — OBFreeze capture sequence
1. Load `Hole_06_Geo.unity` additively.
2. Wait for scene loaded.
3. Position ball at a water-bordered tee on Hole_06. Implementer picks the specific XZ; documents the choice in IMPLEMENTER_REPORT (e.g. "Hole_06 fairway tee at (X,Y,Z), aimed at water hazard at (X',Y',Z'), expected water-hit after ~2.5s of flight"). Use the existing `PlaceBallAt(Vector3)` API.
4. Set club to driver. Aim toward the water hazard (likely needs `AimAt(targetXZ)` or equivalent; if no such API exists, use the existing aim system with a manual yaw value).
5. Set power to 0.85.
6. Schedule capture: `CaptureCore.SnapWhenModeReached(this, director, ChaseCamera.Mode.OBFreeze, "controls_g_followup_obfreeze");`
7. Fire shot.
8. Wait for `OnShotComplete` (terminal state should be `OB`).
9. Unload Hole_06_Geo.

**Sequencing:** runs as a single coroutine that chains C.1 → C.2 → C.3, with explicit pause between sections so PNG captures aren't fighting each other for end-of-frame snap order. Use the existing `EditorApplication.isPaused` toggle pattern from §2b's smoke runner if needed.

**File output:** all three captures land in `Docs/Diagnostics/_capture/` (existing CaptureCore output path), then implementer COPIES them to `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` with the `controls_g_followup_*` prefix per §2b convention.

### D — `LoopCameraDirector` EditMode test

**Location:** `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs`. Add as a NEW test method in the existing class:

```csharp
[Test]
public void Director_OnModeChange_RaisesEventWithNewMode()
{
    // Arrange: standard director setup (mirror existing tests' factory pattern).
    var (director, modeSetter, controllerStub) = DirectorFactory.Create();
    
    var modeHistory = new System.Collections.Generic.List<ChaseCamera.Mode>();
    director.OnModeChanged += (mode) => modeHistory.Add(mode);
    
    // Act: drive a state change that triggers a mode dispatch.
    controllerStub.RaiseStateChange(BallState.Aiming, BallState.Flying);
    
    // Assert: event fired exactly once with the expected new mode.
    Assert.That(modeHistory.Count, Is.EqualTo(1),
        "OnModeChanged should fire exactly once for one state-driven mode change.");
    Assert.That(modeHistory[0], Is.EqualTo(ChaseCamera.Mode.Chase),
        "OnModeChanged should report the newly-applied mode.");
}
```

**Helper assumption:** the test factory pattern (`DirectorFactory.Create`, `RecordingModeSetter`, `controllerStub.RaiseStateChange`) already exists in §2b's test file. Use the same helpers — do NOT introduce parallel test infrastructure.

## Tests

**Test gate:** **241/241 PASS, 0 IGNORED.** Existing 240 (controls_g) + 1 new Director event test.

If the gate fails by 1 (240/241 or 240/240): implementer escalates `IMPLEMENTER_BLOCKED` immediately. NO snapshot updates without architect approval.

## Smoke evidence

Per §2a Lessons M+N + reviewer's controls_g lesson: file persisted on disk + parallel-path Read verification + content-sanity. Use `CaptureCore.SnapWhenModeReached` and `CaptureCore.SnapWhenStateReached` exclusively. NO `WaitForSeconds`-gated captures.

**Three captures with explicit verification protocol:**

For each capture, IMPLEMENTER_REPORT must include:

1. **On-disk file path** under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` with `controls_g_followup_*` prefix.
2. **File size** in bytes (must be > 0 and reasonable for a Game View capture, ~500KB–2MB range).
3. **Content-sanity description** in 1–2 sentences: what the captured frame shows visually. Specifically:
   - **Downrange:** "Driver ball mid-flight, camera positioned past projected landing zone, ball framed against the landing area, flight line behind camera."
   - **Putter GroundLevel:** "Putter ball mid-roll on green, camera in low GroundLevel framing behind ball, no Downrange cinematic visible."
   - **OBFreeze:** "Camera position frozen at first water-hit XZ, ball flying away from camera into the hazard, locked pivot visibly stationary."
4. **Director mode history** for the shot: list the sequence of `OnModeChanged` events fired during the capture sequence. For Downrange this should include `Chase` → `Downrange` (and possibly more). For Putter this should NOT include `Downrange`. For OBFreeze this should include `Chase` → `OBFreeze`.

## Definition of Done

- `LoopCameraDirector.OnModeChanged` event added; ALL `chaseCamera.SetMode` calls in Director routed through `ApplyMode` helper that raises the event.
- `CaptureCore.SnapWhenModeReached` shipped, mirrors `SnapWhenStateReached` one-shot pattern.
- `SmokeTestRunner2b` rewritten: zero `WaitForSeconds(N)` calls (where N > 0.5s for non-settling) — all capture timing state-driven.
- Hole_01_Geo additively loaded for Downrange + Putter captures.
- Hole_06_Geo additively loaded for OBFreeze capture (water-bordered tee placement chosen by implementer).
- 1 new EditMode test `Director_OnModeChange_RaisesEventWithNewMode` PASS.
- Test gate: **241/241 PASS, 0 IGNORED.**
- 3 captures filed under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` with `controls_g_followup_*` prefix; verified per § Smoke evidence above.
- §2b deferred-smoke OPEN flag in TellCode.md marked CLOSED.
- Notion entry [`35931e0e-9a36-81b3-a724-ef1e42678928`](https://www.notion.so/35931e0e9a3681b3a724ef1e42678928) flipped Status In Progress → Done with Closed=2026-05-07 (or whatever date).

## Mid-task escalation paths

- **`IMPLEMENTER_BLOCKED`** — escalate to architect if:
  - Adding `Golfin.Physics.Viewer` reference to `Golfin.Diagnostics.Runtime` creates a circular asmdef dependency. Architect resolves with one of: late-binding via `Action<int>`, moving `ChaseCamera.Mode` enum to `Golfin.Diagnostics.Runtime`, or restructuring the dispatch.
  - Test gate breaks by 1+ unexpectedly (240/241 or any pre-existing test starts failing). Architect investigates whether `OnModeChanged` raise sites accidentally double-fire or whether the new test helper pattern interferes with existing Director tests.
  - Hole_06_Geo additive load throws (collider conflicts, lighting reset, scene-load error). Architect investigates Hole_06's geometry vs Hole_01's; may revise Q2 to skip OBFreeze visual.
  - Driver shot on chosen Hole_06 tee doesn't land in water within reasonable time (>10s flight, or sails over the hazard). Implementer tries 2 alternative tee placements on Hole_06 before escalating; if all 3 fail, architect re-scopes OBFreeze or accepts skip.
- **`IMPLEMENTER_PARTIAL`** — implementer ships A + B + D + Downrange + Putter captures clean, but OBFreeze hits intractable friction (no good water-shot setup found on Hole_06). Acceptable to ship 2 of 3 captures; OBFreeze visual stays open, architect closes §2b deferred-smoke flag with 2/3 captured + EditMode-only confirmation for OBFreeze. **Do not abandon Downrange or Putter** — those are the load-bearing visuals.

## Out of scope

- **Re-tuning Director cinematic-cut math.** The 65% carry threshold, 30m min carry, downrange framing offsets are controls_g-locked via §2b's 9 EditMode tests. This task ADDS the event observer; it does not change behavior.
- **Side-cam variant** (90° to flight line) — out of scope per §2b's "Out of scope" §.
- **Fixing the masked AeroConfig zero-init mechanism** — controls_g tracked it as a documented gap; investigation belongs in a separate task if/when a similar regression appears in another config struct.
- **§2c (turn counter) integration.** §2c can subscribe to `OnModeChanged` if useful, but that's its own spec.
- **Replay tool integration.** `OnModeChanged` is designed to support future replay tools, but actual replay infrastructure is not in this task.
- **OB Test Tee as a permanent lab feature.** The water-bordered tee placement on Hole_06 is a lab-time convenience for this task's smoke run, not a checked-in scene fixture. If permanent, separate spec.

## Hard rules for implementer

1. **Do NOT change Director's cinematic-cut math, dispatch table, or behavior.** Only ADD `OnModeChanged` event + route existing `SetMode` calls through `ApplyMode` helper. The 9 LoopCameraDirectorTests must still PASS unchanged.
2. **Do NOT modify** `BallSimulation.cs`, `Trajectory.cs`, `BallStateMachine.cs`, `BallState.cs`, `AeroModel.cs`, `AeroConfig.cs`, `PhysicsConfigLoader.cs`, any aero CSV, any test currently in PASS state outside `LoopCameraDirectorTests.cs` (where the 1 new test lands).
3. **Do NOT use `WaitForSeconds(N)` for state-dependent captures.** Per reviewer's controls_g lesson and the architect-locked rule: any wait > 0.5s for a moment that depends on physics or SM state is a code smell and a SPEC violation. State-gate via `SnapWhenStateReached` or `SnapWhenModeReached`. The only allowable `WaitForSeconds` is < 0.5s for deliberate settling delays (e.g. one-frame waits between scene unload + reload).
4. **Do NOT skip OBFreeze without trying 2+ tee placements on Hole_06.** Per escalation path, only escalate if multiple attempts fail. Document each attempt's tee XZ + outcome.
5. **Do NOT modify `LabScaffold.unity` via raw YAML.** If scene edits are needed (re-wiring SmokeTestRunner2b), use Unity Editor APIs (`gameobject-create`, `gameobject-component-modify`, `scene-save` MCP tools). Per controls_g deviation #3 lesson, raw YAML edits trigger Unity reload popups that block subsequent automation.
6. **Smoke evidence per §2a Lessons M+N + reviewer's controls_g lesson.** File on disk + Read verification + content-sanity description + Director mode history. Roslyn-only / in-memory captures FAIL review.
7. **Captures filed under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/`** with `controls_g_followup_*` prefix. When all 3 land, mark §2b deferred-smoke OPEN flag in TellCode.md as CLOSED in the same commit.
8. **Bit-exact 240-test PASS gate must hold.** Adding 1 test → 241/241. If any of the 240 starts failing, escalate `IMPLEMENTER_BLOCKED` — do not "fix" the failure by editing the test or accepting a snapshot update without architect approval.
