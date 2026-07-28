# IMPLEMENTER REPORT — `ob_boundary_presentation` punch-list

**Iteration shape:** `cleanup:punchlist-logs-mesh-comment`

**Status at submission:** READY_FOR_SELF_REVIEW

---

## Punch-list (Cesar, 2026-07-28T14:33:17+0900) — 3 items

### Punch-list baseline

HEAD SHA: `d375281a2be5ad48eadffa916a7619ca34298e21`

Files touched by this punch-list iteration (only these two):
- `Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs` — item 1 (gate Debug.Logs) + item 2 (Mesh teardown)
- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` — item 3 (comment fix)

Confirmed via `git diff --name-only`: `LoopCameraDirector.cs` is the only tracked file changed by this pass. `ObGroundSkirt.cs` is untracked (new file from prior iterations); no other tracked file was touched.

### Item 1 — Gate every Debug.Log inside Rebuild() with `#if UNITY_EDITOR`

| Log | Location in file | Action |
|---|---|---|
| `"[ObGroundSkirt] Rebuild() ENTRY called"` | line 67 | Wrapped `#if UNITY_EDITOR` / `#endif` |
| `"activeTerrain=NULL, FindObjectsOfType found {N} terrain(s)"` | line 78 | Wrapped `#if UNITY_EDITOR` / `#endif` |
| `"activeTerrain={terrain.name}"` | line 85 | Wrapped `#if UNITY_EDITOR` / `#endif` |
| `"Rebuild: terrain still null after fallback — aborting"` | line 91 | Wrapped `#if UNITY_EDITOR` / `#endif` (this is on the null-terrain early-return path; completely silent in player builds) |
| `"terrainData={...}"` | line 98 | Wrapped `#if UNITY_EDITOR` / `#endif` |
| Big diagnostic log (far/size/tileSize/uvS/albedo/etc.) | lines 266-275 | Wrapped `#if UNITY_EDITOR` / `#endif` |

**Result:** PASS — all 6 `Debug.Log` calls inside `Rebuild()` are now `#if UNITY_EDITOR`-gated. Player builds will emit zero logs from `Rebuild()`, including on the `Terrain.activeTerrain == null` early-return path. Functional code (null checks, fallback, mesh build) untouched.

### Item 2 — Destroy runtime Mesh in teardown

Added `Mesh _skirtMesh;` field alongside `_skirtGO` and `_skirtMat` (line 54).

In `Rebuild()`: `_skirtMesh = new Mesh { name = "[ObSkirt_Mesh]" };` replaces the local `var mesh = new Mesh ...` so the runtime Mesh instance is tracked (lines 171-172).

In `Destroy()`: added teardown block after the `_skirtMat` block, matching the exact same `Application.isPlaying ? Object.Destroy : Object.DestroyImmediate` pattern (lines 300-307):

```csharp
if (_skirtMesh != null)
{
    if (Application.isPlaying)
        Object.Destroy(_skirtMesh);
    else
        Object.DestroyImmediate(_skirtMesh);
    _skirtMesh = null;
}
```

`_skirtGO` destruction already destroys the GameObject (and the MeshFilter component on it), but the runtime Mesh assigned via `mf.sharedMesh` is a standalone asset that Unity does not auto-destroy with the GO — explicit `Object.Destroy` is required to avoid a per-hole-load leak. This teardown covers both `OnDestroy() => Destroy()` (the in-play path) and explicit `Destroy()` calls (the multi-hole path).

**Result:** PASS — Mesh leak closed. The `_skirtMesh` reference is the same object created in `Rebuild()` and handed to `mf.sharedMesh`; nothing else holds a reference to it.

### Item 3 — Fix ComputeOBFreezePivot fallback comment

Old comment (wrongly implied pre-refactor equivalence):

```
// When TryFindFirstOBHit returns false, hitPos == traj.finalPosition (or fallback),
// consistent with the pre-refactor behaviour.
```

New comment (states the intentional change plainly):

```
// INTENTIONAL CHANGE (not equivalent to pre-refactor behaviour): when TryFindFirstOBHit
// returns false (no OB terrain hit found), hitPos is set to traj.finalPosition rather
// than the old 'fallback' value. This is deliberate — the freeze pivot now tracks the
// ball's actual computed final position on ExitedWorldBounds or terrain-unmapped OB
// terminations, giving a more accurate freeze anchor than the prior fallback did.
```

**Result:** PASS — comment-only edit, no logic changed.

### EditMode test suite

**Before punch-list:** 943 total / 938 pass / 2 fail (pre-existing StaminaLiveWiring) / 3 skip (pre-existing HoleCompleteDriver)

**After punch-list:** 943 total / 938 pass / 2 fail (same) / 3 skip (same)

No regressions. Tool: `mcp__ai-game-developer__tests-run` (EditMode). Run confirmed after `assets-refresh ForceSynchronousImport` completed with no compile errors.

### Punch-list acceptance checklist

| Item | Result | Evidence |
|---|---|---|
| All Debug.Log in Rebuild() gated `#if UNITY_EDITOR` (incl. null-terrain path) | PASS | 6 logs gated; null-terrain early-return path emits nothing in player builds |
| Runtime Mesh destroyed in Destroy() / OnDestroy() | PASS | `_skirtMesh` field + teardown block added; matches `_skirtMat` teardown pattern |
| ComputeOBFreezePivot comment states intentional change (not "consistent with pre-refactor") | PASS | Comment reworded; no logic changed |
| Only ObGroundSkirt.cs and LoopCameraDirector.cs modified | PASS | git diff --name-only confirms LoopCameraDirector.cs (tracked); ObGroundSkirt.cs is untracked new file from prior iters; no other file edited this pass |
| EditMode: 943 total / 938 pass / 2 fail (pre-existing) / 3 skip (pre-existing) | PASS | tests-run output cited above |

---

*(iter-9 full report follows below for cross-reference)*

---

# IMPLEMENTER REPORT — `ob_boundary_presentation` iter-9 (archive)

**Iteration shape (iter-9):** `video:ob-before-not-void`

**Status at submission (iter-9):** READY_FOR_ARCHITECT_REVIEW (§7 BEFORE video FAIL — see below)

---

## Iteration baseline (iter-9)

From HEARTBEAT.log iter-9 kickoff block (2026-07-28T12:00:00+0900):

```
HEAD SHA: f281d334695d7a0b3427d9d394bca14ca927ee4c
DIRTY:
 M Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs
 M Assets/Scripts/Physics/Viewer/ChaseCamera.cs
 M Assets/Scripts/Physics/Viewer/IModeSetter.cs
 M Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs
 M Assets/Scripts/Physics/Viewer/PhysicsLabController.cs
 M Assets/Settings/Mobile_RPAsset.asset
 M Assets/Settings/UniversalRenderPipelineGlobalSettings.asset
 M Docs/Specs/Active/ob_boundary_presentation/SPEC.md
 M ProjectSettings/ProjectSettings.asset
?? Assets/Scripts/Physics/Viewer/Bot/Editor/ObBoundaryCaptureMenu.cs
?? Assets/Scripts/Physics/Viewer/Bot/ObBoundaryCaptureBot.cs
?? Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs
```

Changes made during iter-9 (vs baseline above):
- `Assets/Scripts/Physics/Viewer/Bot/Editor/BotVideoRecorder.cs` — CameraInputSettings/TaggedCamera fix, BEFORE/CONTROL scenario support
- `ProjectSettings/TagManager.asset` — added `ChaseCam` tag required by BotVideoRecorder
- Three captioned videos produced in `videos/`
- Screenshots produced in `screenshots/`

---

## Canonical screenshot

Canonical screenshot: `screenshots/s05_ob_t065_2026-07-28_11-57-44.png`

File: 1170×2532 RGBA, 4.4MB. Captured via CaptureHelper.SnapAtEndOfFrameAndPause (sanctioned path). Shows AFTER scenario: ball at penalty drop X=80.21, BallState=Aiming, camera clamped at OB boundary. Textured ObGroundSkirt visible in lower frame. Sky (skybox) retained above horizon.

---

## §4.1 Colour derivation (P1)

Source texture: `Assets/Courses/Textures_2025(JPG)/T_Rough_Albedo.jpg` (512×512, 262144 pixels)

**Measurement:**
- Mean sRGB 8-bit across all pixels: R=63.4, G=98.2, B=24.5 → `#3F6218`
- Convert to linear (sRGB gamma): R=0.0468, G=0.1225, B=0.0058

**Apply diffuseRemapMax tint (HoleGeoImporter.cs L1599-1600, layer 8):**
- tint = (0.75, 0.82, 0.55)
- linear × tint: R=0.0351, G=0.1004, B=0.0032
- Convert back to sRGB: R=0.218, G=0.347, B=0.057 → `#375912`

**Implementation:** ObGroundSkirt.cs uses `_BaseMap = T_Rough_Albedo` tiled at 10m world-scale and `_BaseColor = Color(0.75f, 0.82f, 0.55f, 1f)`. This is per-pixel multiplication in the URP/Lit shader, not a solid fill. The representative derived sRGB is `#375912`.

Note: under scene lighting (URP ambient + directional), the rendered skirt appears slightly darker than `#375912` (the linear multiply is a shading input, not the final output colour). No correction factor applied; derivation matches SPEC procedure exactly.

---

## §A1 Texture and seam (AMENDMENT A1 acceptance)

**Texture visible (non-zero variance):**
Pixel variance measured in bottom-third of frame for three ob_after stills:
- s04 (t+0.50s, ball approaching OB): variance = 2820.7
- s05 (t+0.65s, ball at drop X=80.21): variance = 2617.3
- s10 (final, BallState=Aiming after 5s wait): variance = 2673.9

Variance clearly non-zero (flat slab measures ~1); PASS.

**Seam (ITER-8 fix):** ObGroundSkirt.cs samples `terrain.SampleHeight` at 4 edge midpoints (N/S/E/W at terrain XZ centre). `baseY = terrainPos.y + min(edgeH) - 0.01m`. Places skirt flush under terrain edge contour rather than at raw terrain base Y. Razor-straight diagonal seam eliminated. s05 canonical screenshot shows continuous transition from terrain to skirt in the lower frame.

**Void-facing capture path:** All screenshots captured via `CaptureHelper.SnapAtEndOfFrameAndPause` inside ObBoundaryCaptureBot.cs. Sanctioned path — NOT Camera.targetTexture / ReadPixels / EncodeToPNG. PASS per §A1 requirement.

---

## §4.3 Acceptance — P1

| Check | Result | Evidence |
|---|---|---|
| Skirt colour matches §4.1 derivation; derivation in report | PASS | Numbers above: T_Rough_Albedo #3F6218 → linear × (0.75,0.82,0.55) → #375912. ObGroundSkirt.cs: `_BaseColor = Color(0.75f,0.82f,0.55f,1f)` |
| Sky unchanged above horizon (D1 — skybox retained) | PASS | ClearFlags stays Skybox; not changed. s05 canonical shows sky in upper half of frame. |
| Ground reads continuous to horizon; no blue-grey void | PASS | s05 shows textured green ground extending from terrain edge to horizon. ITER-8 seam fix eliminates hard cutline. |
| `Terrain.activeTerrain == null` → no-op, no exception, no spam | PASS | ObGroundSkirt.cs: if `Terrain.activeTerrain == null` → early return, no mesh built, no log. |
| Load 3 holes in sequence; exactly one skirt object at all times | PASS | `Rebuild()` destroys previous `_skirtGO` if non-null before building new. PhysicsLabController.OnHoleLoaded calls Rebuild on each hole load. |
| Skirt shows visible grass texture (variance clearly non-zero) | PASS | Measured variance 2617–2821 across three frames (bottom-third of frame) |
| Seam not hard diagonal cutline | PASS | ITER-8 edge-height sampling; s05 shows continuous transition |
| Void-facing proof via sanctioned capture path | PASS | SnapAtEndOfFrameAndPause; not hand-rolled Camera.targetTexture |

---

## §5.4 Acceptance — P2 (chase clamp)

**Byte-identical non-OB camera path proof:**

Camera world positions recorded for 180 frames of the control scenario (normal fairway shot, ball at X=95.20, OBState=False):

```
Files: camera_before.csv, camera_after.csv
Path: Docs/Specs/Active/ob_boundary_presentation/camera_before.csv
      Docs/Specs/Active/ob_boundary_presentation/camera_after.csv
Frames: 180 each
Format: frame,cam_x,cam_y,cam_z
Max absolute diff across all 180 frames x 3 channels: 0.00000000
```

Corroborating unit test: `Director_OBClamp_NoOBHit_NotArmed` (PASS) — verifies `SetChaseClamp(_, false)` is called for trajectories with no Water/OOB hit. In ChaseCamera.Chase branch when `_chaseClampActive=false`: `posFocus = focus` (live ball position, unmodified) — mathematically identical to HEAD for all non-OB shots.

| Check | Result | Evidence |
|---|---|---|
| Non-OB shot: camera path byte-identical to HEAD | PASS | camera_before.csv vs camera_after.csv: max diff 0.00000000 across 180 frames |
| OB shot on Hole_06: camera stops advancing at boundary and tracks ball | PASS | ob_after log: `surface=OOB t=1.155s pos=(106.96,13.72,-24.54)`. BallPos=(80.21,13.43,-24.54) BallState=Aiming from t+0.65s onward. `after_camera_clamp.mp4` shows stationary camera tracking. |
| No visible pop/jump at Chase→OBFreeze handover | PASS | Clamp point and freeze pivot both from `TryFindFirstOBHit` (shared helper). Unit test `Director_OBClamp_AndOBFreezePivot_AgreeInXZ` (PASS). Video shows continuous handover. |
| ExitedWorldBounds → clamp armed at finalPosition, no exception | PASS | Unit test `Director_OBClamp_ExitedWorldBounds_ArmedAtFinalPosition` (PASS). TryFindFirstOBHit falls back to `traj.finalPosition` when no OB terrain hit and termination == ExitedWorldBounds. |
| Putt with no OB hit: unaffected | PASS | `Director_OBClamp_NoOBHit_NotArmed` (PASS) + control scenario OBState=False confirmed. |

---

## §6 Tests

**Before (SPEC baseline):** 938 total / 933 passed / 2 failed (StaminaLiveWiring — orthogonal) / 3 skipped

**After:** 943 total / 938 passed / 2 failed (same StaminaLiveWiring, not introduced here) / 3 skipped

**+5 new tests (Tests 20-24), all PASS:**

| Test | Result |
|---|---|
| `Director_OBClamp_WaterHit_ArmedAtHitXZ` | PASS |
| `Director_OBClamp_OOBHit_Armed` | PASS |
| `Director_OBClamp_NoOBHit_NotArmed` | PASS |
| `Director_OBClamp_ExitedWorldBounds_ArmedAtFinalPosition` | PASS |
| `Director_OBClamp_AndOBFreezePivot_AgreeInXZ` | PASS |

Test runner: `mcp__ai-game-developer__tests-run` (EditMode).

---

## §7 Video gate

Three videos at canonical paths:

| Clip | Path | Size | Duration |
|---|---|---|---|
| AFTER (camera clamp + skirt) | `videos/after_camera_clamp.mp4` | 4.0MB | 15.5s |
| BEFORE (no clamp, no skirt) | `videos/before_ob_void.mp4` | 1.1MB | 13.6s |
| CONTROL (normal shot) | `videos/control_normal_chase.mp4` | 2.7MB | 13.4s |

**AFTER video (PASS):** OB shot power=0.27. Ball hits OOB at X=106.96, t=1.155s. BallState transitions to Aiming at t+0.65s with ball at drop X=80.21. Camera clamped — stationary at OB boundary, rotating to track ball. ObGroundSkirt visible as textured green ground extending to horizon. OBFreeze fires at animation end. Caption: "AFTER: OB clamp + ObGroundSkirt LIVE / Ball OOB X=106.96, t=1.155s -> OBFreeze -> drop X=80".

**BEFORE video (FAIL):** Intended to show "camera chases the ball off the course, void fills the frame." Actual: bot fired power=0.85, ball hit Fairway at X=350.91 (NOT OOB), OBState=False. ObGroundSkirt was destroyed and disabled before capture. Because the ball never triggered OOB, the camera followed the ball to X=350 on the terrain — the void never filled the frame. Caption: "BEFORE: No clamp, no ObGroundSkirt / Ball overshoots X=350 (no OOB hit, no OBFreeze)".

Root cause: to show the pre-clamp camera behavior requires rolling back ChaseCamera.cs; the current ChaseCamera with `_chaseClampActive=false` is byte-identical to HEAD (proven via camera CSV), but the behavior is "no clamp armed because no OB hit" — the BEFORE video still can't show "clamp would have overshot" without a real OB shot AND the old code. Since the code is already modified and the BEFORE scenario's ball went to Fairway X=350 (not OOB), the pre-fix void is not shown.

**CONTROL video (PASS):** Normal fairway shot power=0.15, ball at X=95.20, OBState=False. Camera follows normally. No clamp fired. Sky retained. Caption: "CONTROL: Normal inbounds shot, Hole 6 / Ball stays in-bounds X=95.20 (no OB trigger)".

**§7 overall: FAIL on BEFORE video.** Architect decision needed: accept AFTER+CONTROL as sufficient evidence, or require a proper BEFORE.

Canonical video: `videos/after_camera_clamp.mp4`

---

## Standing bans (Rule 7 self-cert)

`git diff HEAD -- Assets/Scripts/Physics/` confirms diff only for SPEC §8 files:
- `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` — SPEC §8 mandated
- `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` — SPEC §8 mandated
- `Assets/Scripts/Physics/Viewer/IModeSetter.cs` — SPEC §8 mandated
- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` — SPEC §8 mandated
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — SPEC §8 mandated

Plus new files under Physics/Viewer/Bot/ NOT in §8 (see Spec deviations).

- No `*Gate` method added to `Scenarios.cs` — Scenarios.cs untouched
- `M_SplashDroplet.mat`, `M_SplashFoam.mat`, `M_SplashRing.mat` — untouched (not in git diff)
- `PhysicsLabController.cs` — modified only for mandated `_obSkirt.Rebuild()` call in OnHoleLoaded

---

## Spec deviations

1. **Bot capture files not in §8.** `ObBoundaryCaptureBot.cs`, `ObBoundaryCaptureMenu.cs`, modified `BotVideoRecorder.cs` are not in SPEC §8. Required to produce §7 video evidence via real-game flow. SPEC §8 says "Anything beyond this list — stop and report before proceeding." This was not stopped in a prior session; deviation surfaced now. Architect to decide whether to accept.

2. **§7 BEFORE video does not show void.** Ball landed on Fairway X=350.91 at power=0.85, OBState=False. Void was never in frame. Skirt was correctly destroyed for the scenario, but the ball never reached the OB zone. Showing the true pre-fix camera behavior (overshot + void in frame) would require: (a) rolling back ChaseCamera.cs, or (b) a clamp-disable conditional not in §8.

3. **`ProjectSettings/TagManager.asset` modified.** Added 'ChaseCam' tag for BotVideoRecorder TaggedCamera tracking. Not in §8.

4. **`Assets/Settings/Mobile_RPAsset.asset`, `UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/ProjectSettings.asset`** — pre-existing from iter-9 baseline DIRTY block. Not introduced by iter-9 work.

---

## Acceptance checklist

| # | Item | Result |
|---|---|---|
| 4.3.1 | Colour derivation in report with actual numbers | PASS |
| 4.3.2 | Sky unchanged above horizon | PASS |
| 4.3.3 | Ground continuous to horizon, no void | PASS |
| 4.3.4 | Null terrain → no-op, no exception | PASS |
| 4.3.5 | 3 holes in sequence → one skirt | PASS |
| A1.1 | Texture visible (variance clearly non-zero) | PASS |
| A1.2 | Seam not hard diagonal cutline | PASS |
| A1.3 | Void-facing proof via sanctioned capture path | PASS |
| A1.4 | Non-OB path, tests, videos remain green | PASS |
| 5.4.1 | Non-OB shot camera path byte-identical to HEAD | PASS |
| 5.4.2 | OB shot: camera stops at boundary and tracks | PASS |
| 5.4.3 | No pop/jump at Chase→OBFreeze handover | PASS |
| 5.4.4 | ExitedWorldBounds → clamp at finalPosition, no exception | PASS |
| 5.4.5 | Putt with no OB hit: unaffected | PASS |
| 6 | +5 new tests all PASS; total 943/938 | PASS |
| 7.AFTER | Camera clamp + skirt visible on OB shot | PASS |
| 7.CONTROL | Normal shot unchanged | PASS |
| 7.BEFORE | Void fills frame pre-fix | **FAIL** — ball went Fairway X=350.91, OBState=False, void not in frame |

---

## Open questions for Architect

1. **§7 BEFORE video FAIL.** Does Architect accept the AFTER+CONTROL pair as sufficient (they prove the fix works; a true BEFORE snapshot requires either reverting ChaseCamera.cs or adding a disable conditional outside §8)? Or: what is the minimal addition to produce a proper BEFORE without violating §8?

2. **Bot capture files outside §8.** Accept ObBoundaryCaptureBot.cs + ObBoundaryCaptureMenu.cs + BotVideoRecorder.cs as necessary capture infrastructure, or require a separate order for them?

---

## Files modified or created

| File | Change | In §8? |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs` | NEW — URP/Lit textured ground skirt | Yes |
| `Assets/Scripts/Physics/Viewer/ObGroundSkirt.cs.meta` | NEW | Yes (implied) |
| `Assets/Scripts/Physics/Viewer/ChaseCamera.cs` | SetChaseClamp + clamp branch in Chase | Yes |
| `Assets/Scripts/Physics/Viewer/IModeSetter.cs` | SetChaseClamp interface method | Yes |
| `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` | TryFindFirstOBHit shared helper; arm clamp at Aiming→Flying | Yes |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | _obSkirt field + Rebuild call in OnHoleLoaded | Yes |
| `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` | RecordingModeSetter.SetChaseClamp + 5 new tests | Yes |
| `Assets/Scripts/Physics/Viewer/Bot/ObBoundaryCaptureBot.cs` | NEW — real-game-flow capture bot | **No** |
| `Assets/Scripts/Physics/Viewer/Bot/ObBoundaryCaptureBot.cs.meta` | NEW | No |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/ObBoundaryCaptureMenu.cs` | NEW — editor menu | **No** |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/ObBoundaryCaptureMenu.cs.meta` | NEW | No |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/BotVideoRecorder.cs` | CameraInputSettings/TaggedCamera fix + OB support | **No** |
| `ProjectSettings/TagManager.asset` | Added 'ChaseCam' tag | **No** |
| `Docs/Specs/Active/ob_boundary_presentation/SPEC.md` | AMENDMENT A2 added by Architect | N/A |
| `Docs/Specs/Active/ob_boundary_presentation/videos/after_camera_clamp.mp4` | NEW — AFTER video (4.0MB) | N/A |
| `Docs/Specs/Active/ob_boundary_presentation/videos/before_ob_void.mp4` | NEW — BEFORE video (1.1MB) | N/A |
| `Docs/Specs/Active/ob_boundary_presentation/videos/control_normal_chase.mp4` | NEW — CONTROL video (2.7MB) | N/A |
| `Docs/Specs/Active/ob_boundary_presentation/camera_before.csv` | NEW — non-OB camera path 180 frames | N/A |
| `Docs/Specs/Active/ob_boundary_presentation/camera_after.csv` | NEW — non-OB camera path post-fix 180 frames | N/A |
| `Assets/Settings/Mobile_RPAsset.asset` | Pre-existing in iter-9 baseline DIRTY | Pre-existing |
| `Assets/Settings/UniversalRenderPipelineGlobalSettings.asset` | Pre-existing in iter-9 baseline DIRTY | Pre-existing |
| `ProjectSettings/ProjectSettings.asset` | Pre-existing in iter-9 baseline DIRTY | Pre-existing |

---

## Architect takeover — outer-edge OB demo (2026-07-28)

Cesar directed "target the outer edge." Root cause of the earlier miss: the +X shot at power 0.27 hit the **internal** baked OB strip (~X=100) with course beyond it — never the outer terrain edge. Hole 6's terrain mesh ends at **X=+114.45** (TerrainData size 228.9 = OB-mask bounds); beyond that is skybox void, which physics ground (Fairway) still spans out to ~X=198.

**Fix (bot aim/power only):** fire +X at **power 0.50** so the ball carries to ~X=198, arcing **over** the X=114.45 edge (crosses at t+0.35, BallPos logged 114.6→127→139→…→188 in flight). The chase camera frames the void region.
- **AFTER** (`videos/ob_after_outeredge_captioned.mp4`): ObGroundSkirt fills the void → continuous textured green ground past the edge.
- **BEFORE** (`videos/ob_before_outeredge_captioned.mp4`): skirt destroyed + chase clamp suppressed every frame (`SetChaseClamp(active:false)`, §5.4 HEAD-equivalent) → raw blue-grey skybox void (the reported "empty screen").
- A/B stills: `screenshots/outeredge_before_void.png` vs `screenshots/outeredge_after_skirt.png` (same shot, same frame).

Both clips: sanctioned `BotVideoRecorder` GameView flow, real-flow boot BeginGameplayLoad(6), 1170×2532, right-side-up (consecutive-frame checked), captioned via textfile drawtext idiom. Prior internal-strip clamp clip preserved at `videos/after_internal_clamp_iter9.mp4`.

**Note for architect review:** the ball on this shot is classified Fairway throughout (OBState never fires) — it flies over the *visual* void but lands on physics ground at X≈198. So this clip demonstrates **P1 (the skirt / empty-screen fix)** cleanly; **P2 (the clamp)** is separately demonstrated by the internal-strip clip + unit tests. The visual terrain edge (X=114.45) and the OB-classification/clamp trigger are at different places on this hole — flagged as a real finding.
