# SPEC — `ob_boundary_presentation`

**Order:** (Notion, P2, Gameplay Polish)
**Tier:** 3 — FULL PIPELINE (visual fidelity + runtime spatial math + video gate)
**Scope:** Read-side presentation only. **No sim, no CSV, no asmdef, no `BallStateMachine` change.**
**Surfaced by:** Cesar, iPhone build 2026-07-27 — "empty screen when the ball goes OB."

---

## 1. Problem

When a shot leaves the course, the player watches a void. Two separate defects:

**P1 — the world outside the terrain is raw skybox.**
The gameplay camera clears with `m_ClearFlags: 1` (Skybox) — verified in `Assets/Scenes/Physics/LabScaffold.unity:10112`. The serialized `m_BackGroundColor` is Unity's default blue-grey with `a: 0` and is never used. Past the terrain edge there is no geometry, so the lower half of the frame is bare sky.

**P2 — the chase camera follows the ball off the map.**
`ChaseCamera.Mode.OBFreeze` already exists and works (`ChaseCamera.cs:161-169` — position pinned to `_obFreezePivot`, rotation tracks the ball), and `LoopCameraDirector.ModeMap` maps `BallState.OB → OBFreeze`. **It fires too late.**

`BallStateMachine.Tick()` drains queued transitions only on the *falling edge of the animator* — i.e. when the ball animation finishes. `Aiming→Flying` fires synchronously in `OnTrajectoryComputed`; everything after it is queued. So the camera stays in `Chase` for the whole flight, follows the ball into the void, and only snaps to `OBFreeze` after the player has already watched the empty screen.

This is why the OB camera "was never successfully implemented" — the mode is correct, the trigger point is not.

---

## 2. Locked decisions

| # | Decision | Rationale |
|---|---|---|
| **D1** | **Ground skirt, not camera `SolidColor`.** Keep `CameraClearFlags.Skybox`. | Camera `SolidColor` (the `MapViewController.cs:458-462` pattern) would kill the sky on *every* shot, not just OB shots. That is a regression on normal flight against the horizon. |
| **D2** | OB skirt colour derives from the **real OB terrain layer**, measured — not hand-picked. | See §4.1. |
| **D3** | Chase clamp is **pre-armed at `Aiming→Flying`** from the already-computed trajectory. | The trajectory is fully known at shot time (deterministic pre-sim). No state-machine retiming, no new events, no sim touch. |
| **D4** | The existing `OBFreeze` terminal mode **stays**. The clamp is an addition to `Chase`, not a replacement. | Terminal framing behaviour is already correct and video-gated by prior work; do not disturb it. |
| **D5** | Video gate on **Hole_06** (the lake). | Already the established OB scenario — `SmokeRunner2eHost.cs:184-186` fires at the Hole_06 lake for the OB→Aiming sequence. |

---

## 3. Non-goals (explicitly out of scope)

- Any change to `BallStateMachine`, `BallSimulation`, or anything under `Assets/Scripts/Physics/` **outside** `Physics/Viewer/`.
- Retiming when `BallState.OB` fires. The clamp does not depend on it.
- Fog tuning. `LabScaffold.unity` has `m_FogMode: 3`, density `0.01`, colour grey `0.5`; leave it alone this pass and note the interaction in the report if the skirt reads wrong through fog.
- The ball-trail persistence bug — separate order `ball_trail_shot_isolation`.
- Water surface rendering, splash FX, OB penalty/drop logic, HUD messaging.

---

## 4. Part 1 — OB ground skirt (P1)

### 4.1 Derive the OB colour (measure first — do NOT hand-pick)

The OB surface is fully determined by the importer. From `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`:

- line 1484 — terrain layer **8** = OB, albedo `"T_Rough_Albedo"` ("same grass as rough, tinted darker")
- line 1599 — `layers[8].diffuseRemapMin = new Vector4(0f, 0f, 0f, 0f)`
- line 1600 — `layers[8].diffuseRemapMax = new Vector4(0.75f, 0.82f, 0.55f, 1f)`

**Procedure:**
1. Load `T_Rough_Albedo` from `Assets/Courses/Textures_2025(JPG)`.
2. Compute its mean linear RGB.
3. Multiply channel-wise by `(0.75, 0.82, 0.55)` — the `diffuseRemapMax` tint.
4. Record the resulting colour in `IMPLEMENTER_REPORT.md` as both linear and sRGB hex.

That value is the skirt's base colour. **Report the number.** Do not substitute a colour that "looks close" — approximation = FAIL.

> **NOTE:** if the mean-albedo result reads noticeably lighter or darker than the rendered OB terrain under scene lighting, say so in the report with a side-by-side still and propose a correction factor. Do not silently adjust.

### 4.2 Build the skirt

New component `ObGroundSkirt.cs` in `Assets/Scripts/Physics/Viewer/` (namespace `Golfin.Physics.Viewer`). Pattern-match `WaterSplashController.cs` — a small additive presentation component in the same folder, shipped by Order 349.

Behaviour:
- Resolve the active hole terrain via `Terrain.activeTerrain` (precedent: `Assets/Scripts/Debug/WalkCamera.cs:101-104`). Null terrain ⇒ **no-op, no error** (lab/flat-ground scenes must stay byte-identical).
- Read `terrain.terrainData.size` and `terrain.transform.position` for bounds.
- Build **one** flat quad/plane mesh centred on the terrain's XZ centre, extending well past the far clip so no horizon gap is visible. Size it from the camera far clip, not a magic number — read `farClipPlane` off the chase camera and use a comfortable multiple.
- Y = terrain base Y (`terrain.transform.position.y`), nudged **down** a small epsilon so it never z-fights the terrain edge.
- Unlit material, skirt colour from §4.1, no shadow cast/receive, `lightProbeUsage = Off`, `reflectionProbeUsage = Off` (mobile perf — mirror the flags block in `BallTrailController.EnsureTrail`).
- Rebuild/reposition on hole change. Hook: **`PhysicsLabController.OnHoleLoaded(string sceneName)`** — `PhysicsLabController.cs:1503`, called by `LabHoleBinder` when a `Hole_NN_Geo` scene opens additively.
- Destroy cleanly on hole unload; must not accumulate across holes. (Ghost-object accumulation is a known repo failure mode — see the sweep in `BallAnimator.Awake`.)

### 4.3 Acceptance — P1

- [ ] Skirt colour matches the derived §4.1 value exactly; both the derivation and the final value are in the report.
- [ ] Standing at the tee on Hole_06, sky is unchanged above the horizon (skybox retained — D1).
- [ ] Looking off the course edge, the ground reads continuous to the horizon; no blue-grey void.
- [ ] `Terrain.activeTerrain == null` (LabScaffold, `PhysicsLab_TestGreen`) ⇒ no skirt, no exception, no log spam.
- [ ] Load 3 holes in sequence; exactly one skirt object exists at all times.

---

## 5. Part 2 — chase clamp at the OB limit (P2)

### 5.1 Compute the clamp point

`LoopCameraDirector` already has the exact scan needed. `ComputeOBFreezePivot` (`LoopCameraDirector.cs`) walks `traj.terrainHits` for the first hit with `hit.Surface == SurfaceType.Water || hit.Surface == SurfaceType.OOB`.

Extract that scan into a shared private helper (e.g. `TryFindFirstOBHit(Trajectory traj, out Vector3 pos)`) and have **both** `ComputeOBFreezePivot` and the new clamp path call it. Do not copy-paste the loop — the Order-731/762 copy-paste duplication is a named repo scar.

In `HandleStateChanged`, on the `Aiming→Flying` branch (which already calls `ArmChaseForShot`):
- run the scan against `ctrl.LastTrajectory`
- **hit found** ⇒ pass the clamp point to the setter
- **no OB hit** (the overwhelmingly common case) ⇒ pass "no clamp" and the camera behaves **byte-identically to today**

`TerminationReason.ExitedWorldBounds` produces no OB terrain hit. In that case fall back to `traj.finalPosition` as the clamp point, consistent with how `ComputeOBFreezePivot` already falls back.

### 5.2 Apply the clamp in `ChaseCamera`

Add to `ChaseCamera` (and therefore to `IModeSetter` — `Assets/Scripts/Physics/Viewer/IModeSetter.cs`, plus the test double in `LoopCameraDirectorTests.cs`):

```
void SetChaseClamp(Vector3 clampPoint, bool active);
```

In `RunLateUpdateLogic`, `default:` (Chase) branch only. Today:

```csharp
desiredPos = focus - _launchDir * _followDistance + Vector3.up * (_followHeight + FollowHeightOffset);
desiredRot = Quaternion.LookRotation(focus - desiredPos);
```

Change: when the clamp is active, clamp the **position focus** — not the look focus — to the clamp point measured along `_launchDir` from `_shotOrigin`:

- project both `focus` and `clampPoint` onto `_launchDir` (flat XZ) relative to `_shotOrigin`
- if the ball's projected progress exceeds the clamp's, use the clamp point for `desiredPos`
- **`desiredRot` always uses the live ball `focus`** — that is the "track it from there" half of the requirement

Result: the camera advances normally, stops dead at the OB boundary, and pans to follow the ball out. `SmoothDamp`/`Slerp` are untouched, so the stop eases rather than snapping.

Clear the clamp on `ResetToOrigin` so it cannot leak into the next shot.

### 5.3 Interaction with `OBFreeze`

Unchanged. When the animation finishes and `BallState.OB` finally fires, `ModeMap` still promotes to `OBFreeze` and `SetOBFreezePivot` still runs. Because the clamp point and the freeze pivot derive from the **same** first-OB-hit scan (§5.1), the handover is visually continuous — the camera is already sitting at that point.

### 5.4 Acceptance — P2

- [ ] Non-OB shot: camera path is **byte-identical** to HEAD. Prove it — record chase-camera world position per frame for one fixed shot before and after, and diff.
- [ ] OB shot on Hole_06: camera stops advancing at the boundary and rotates to track the ball out over the water.
- [ ] No visible pop/jump at the `Chase → OBFreeze` handover when the animation completes.
- [ ] `ExitedWorldBounds` shot (no OB terrain hit): clamps at `finalPosition`, no exception.
- [ ] Putt with no OB hit: unaffected.

---

## 6. Tests

- Extend `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs`. The `RecordingModeSetter` double must gain `SetChaseClamp`; add a `LastChaseClamp` capture alongside the existing `LastDownrangePos` / `LastCupZoomFocus` fields.
- New cases:
  1. Trajectory with a `Water` hit ⇒ clamp armed at that hit's XZ.
  2. Trajectory with an `OOB` hit ⇒ clamp armed.
  3. Trajectory with neither ⇒ clamp **not** armed.
  4. `ExitedWorldBounds` ⇒ clamp armed at `finalPosition`.
  5. Clamp point and `OBFreeze` pivot agree in XZ for the same trajectory (shared-helper regression).
- `ChaseCamera` clamp math is unit-testable via the existing `internal void FrameCamera(float dt)` seam — use it; do not `SendMessage("LateUpdate")`.
- Full EditMode suite green. Current baseline is 933/938 with 2 pre-existing `StaminaLiveWiring` failures (orthogonal) — do not "fix" those here.

---

## 7. Video gate

Real play, not a synthetic harness. Per CLAUDE.md Capture Rule 0, use the `screenshot-game-view` MCP tool / real-user flow — hand-rolled `script-execute` captures are hard-blocked by `.claude/hooks/enforce_capture_tool.py`.

**Hole_06, shot into the lake. Two clips:**

- **BEFORE** (HEAD): camera chases the ball off the course, void fills the frame.
- **AFTER**: camera stops at the boundary and tracks; ground beyond the course reads as OB grass.

Plus one **control** clip: a normal fairway shot on the same hole, before/after, demonstrating no change to standard chase framing and no loss of sky.

---

## 8. Files touched (expected)

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs` | **new** |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | `SetChaseClamp` + clamp branch in Chase |
| `Assets/Scripts/Physics/Viewer/IModeSetter.cs` | `SetChaseClamp` |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | shared OB-hit scan; arm clamp at Aiming→Flying |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | one call in `OnHoleLoaded` to build/refresh the skirt |
| `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` | stub + new cases |

Anything beyond this list — stop and report before proceeding.

---

## 9. Report

`IMPLEMENTER_REPORT.md` in this folder must contain:
1. The §4.1 colour derivation, with the actual numbers.
2. The §5.4 byte-identical non-OB camera-path diff.
3. Test counts before/after.
4. Links to the three video clips.
5. Anything that did not go to plan, stated plainly.

**Derive from the primary source; do not confirm an artifact that asserts it.**
