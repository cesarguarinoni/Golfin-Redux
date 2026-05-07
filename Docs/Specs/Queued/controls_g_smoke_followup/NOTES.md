# NOTES — `controls_g_smoke_followup`

> Architect pre-spec analysis. Not yet a SPEC. Implementer reads SPEC.md for work definition once locked.

## Status

`Docs/Specs/Queued/controls_g_smoke_followup/` — STATUS=`NOTES_DRAFT` 2026-05-07 19:10 JST.

Notion: [`35931e0e-9a36-81b3-a724-ef1e42678928`](https://www.notion.so/35931e0e9a3681b3a724ef1e42678928) — Phase 02. Loop v1, Order 230, P1 — High, S (half-day), Status=Next.

## What this task closes

The §2b deferred-smoke OPEN flag in `Docs/TellCode.md` was narrowed (NOT closed) by controls_g. Three cinematic camera modes still lack runtime visual confirmation:

1. **Downrange** — driver mid-flight cinematic cut at 65% horizontal carry. Camera positioned past landing zone looking back along flight line.
2. **GroundLevel preserved on putter** — putter shot during Flying state must NOT trigger Downrange cut. Load-bearing `isPutt` skip in Director.
3. **OBFreeze** — driver shot into water/OOB. Camera locks at first OB sample XZ + 5m above terrain Y, rotation tracks ball flying away.

Director logic is verified at model layer by 9 LoopCameraDirectorTests in the 240/240 PASS gate. This task confirms runtime visual rendering matches the model assertions.

## Why current smoke failed

controls_g's SmokeTestRunner2b.cs (lives in `Assets/Scripts/Physics/Viewer/`) uses 3-second timed waits to schedule captures. Per reviewer report:

- Downrange capture fired before shot reached 65% carry threshold → captured the Aiming HUD with charge ring instead.
- AtRest capture fired with no Hole_01_Geo loaded → captured an empty backdrop (LOG-confirmed AtRest, but visually inconclusive).
- Putter GroundLevel capture fired during swing-charge animation → SnapWhenStateReached subscribed for BallState.Flying entry but the swing-animation overlay was still rendering.
- OBFreeze capture: not attempted (requires Water-bordered tee setup).

Root cause: time-driven captures are fragile against shot-power and carry-distance changes. State-driven captures via `CaptureCore.SnapWhenStateReached` are correct in principle but the Director's mid-flight cinematic cut is NOT a `BallStateMachine` transition — it's a mode change inside `Flying` state, driven by `LoopCameraDirector.Update()` checking carry progress. So `SnapWhenStateReached(BallState.Flying)` fires at Flying ENTRY, not after the cinematic cut.

## Three architectural options for the fix

### Option A — extend Director with a mode-change event

Add `event Action<ChaseCamera.Mode> OnModeChanged` to `LoopCameraDirector`. Smoke runner subscribes via a new `CaptureCore.SnapWhenModeReached(MonoBehaviour owner, LoopCameraDirector dir, ChaseCamera.Mode target, string label, ...)` API.

**Pros:** Clean state-driven capture, deterministic timing, future-proof for any code that needs to observe Director mode changes (analytics, replays, debug overlays).

**Cons:** Adds public API surface to Director. New CaptureCore method needed. ~30 minutes additional dev for the event + 10 minutes for the new CaptureCore method.

### Option B — compute carry threshold + time-gate from there

Smoke runner reads `controller.LastTrajectory` after the shot fires, computes predicted carry, time-gates capture to fire when `controller.CurrentBall.position` crosses 65%+ of carry. Same math the Director itself uses in `Update()`.

**Pros:** No Director API changes. Zero new abstractions.

**Cons:** Smoke runner duplicates Director's cut-timing logic — drift risk if Director's threshold ever changes (currently 0.65f). Still time-adjacent: relies on physics tick timing matching real time at the capture moment.

### Option C — load Hole_01_Geo additively, use real environment

Smoke runner loads `Hole_01_Geo.unity` additively before firing the shot. Carry distance matches shipping environment. Timing approximates production behavior.

**Pros:** Most realistic visual evidence (real terrain, real landing). Adds proper Hole_01_Geo backdrop to the AtRest capture too — solves the empty-backdrop visual inconclusiveness.

**Cons:** Slower test (scene load + heightmap raster). Doesn't solve the underlying cinematic-cut timing problem — still need Option A or B for the actual cut moment.

### Architect recommendation: A + C

- **Option A** (Director.OnModeChanged event) is the architecturally clean cut-timing fix. Generalizes beyond smoke — replay tools, analytics, debug overlays will benefit.
- **Option C** (load Hole_01_Geo additively) is the visual-clarity fix. Real terrain backdrop makes captures self-evidently correct instead of inconclusive.

A + C together, ~1 hour combined dev. Lean: do both.

NOT B alone. The drift risk is real and the duplication is a code smell.

## Three captures needed

Each driven by `CaptureCore.SnapWhenModeReached(...)` (new API per Option A):

1. **`controls_g_followup_downrange_*.png`** — driver shot from tee on Hole_01_Geo. Capture when `LoopCameraDirector` enters `ChaseCamera.Mode.Downrange`. Must visually show: ball mid-flight, camera positioned past projected landing zone, ball framed against landing area, flight line approximately behind camera.

2. **`controls_g_followup_putter_groundlevel_*.png`** — putter shot from green on Hole_01_Geo (place-ball-near-cup convenience method). Capture when `BallStateMachine` enters `BallState.Rolling` (mid-roll, not at-rest). Must visually show: ball mid-roll, camera in GroundLevel framing (low to ground, behind ball), NO Downrange cut visible.

3. **`controls_g_followup_obfreeze_*.png`** — driver shot from a water-bordered tee setup. Need: a new lab-only "OB Test Tee" placement on Hole_01_Geo at ~10m from a Water surface. Capture when `LoopCameraDirector` enters `ChaseCamera.Mode.OBFreeze`. Must visually show: camera position frozen at first water-hit XZ, ball flying away from camera into bounds.

## Open questions for Cesar (lock before SPEC)

1. **A + C combined, or A alone first?** Architect lean: A + C combined — only saves ~20 min if A-alone first, and the visual clarity from real terrain is a real win.
2. **OB Test Tee placement.** Spec needs a known coordinate where firing a driver lands in water within ~3 seconds. Hole_01_Geo has lake/water surfaces — pick one. Architect proposed: lab-only synthetic tee at a fixed XZ near a water hazard. Cesar approval needed for which water hazard. (Or: skip OBFreeze visual entirely, leave it as a documented test-time-only mode and rely on the EditMode `Director_OnOB_FreezesAtFirstWaterHitXZ` test alone.)
3. **Test additions.** New EditMode test for `LoopCameraDirector.OnModeChanged` event firing? Architect lean: yes, one test (`Director_OnModeChange_RaisesEventWithNewMode`).
4. **Where does the new `CaptureCore.SnapWhenModeReached` API live?** Same file as `SnapWhenStateReached`. Architect default: yes.
5. **PASS gate target.** Currently 240/240. With one new Director test, target becomes 241/241. Confirm before SPEC.

## Hard rules pre-locked (carry into SPEC)

1. **Do NOT modify** `LoopCameraDirector` cinematic-cut MATH (the 65% threshold, 30m min carry, downrange framing offsets). controls_g shipped that as architecturally PASS via the 9 EditMode Director tests; this task ADDS a mode-change event, not a behavior change.
2. **Do NOT modify** `BallStateMachine`, `BallState`, `BallSimulation`, `Trajectory`, any aero CSV.
3. **Do NOT modify** any test currently in PASS state. Additive only.
4. **Smoke evidence per §2a Lessons M+N + §2b Lesson:** file persisted on disk + parallel-path Read verification + content-sanity. **Plus reviewer's controls_g lesson:** prefer state-driven captures over time-driven. The new SnapWhenModeReached API IS the state-driven correction.
5. **Captures filed under `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/`** with `controls_g_followup_*` prefix. When all three land, mark §2b deferred-smoke OPEN flag in TellCode.md as CLOSED.

## Files this task likely touches

- `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` (add `OnModeChanged` event, raise it in `SetMode`-equivalent paths)
- `Assets/Scripts/Diagnostics/Runtime/CaptureCore.cs` (add `SnapWhenModeReached` API)
- `Assets/Scripts/Physics/Viewer/SmokeTestRunner2b.cs` (rewrite to use `SnapWhenModeReached` + load Hole_01_Geo additively)
- `Assets/Scripts/Physics/Tests/LoopCameraDirectorTests.cs` (one new test for `OnModeChanged` event)
- Possibly: `Assets/Scenes/Physics/LabScaffold.unity` (re-add SmokeTestRunner2b component cleanly via Unity editor, NOT raw YAML — closes the §2b deviation #3 risk too)

## Reference

- `Docs/Specs/Active/controls_g_aero_constant_mode_crash/ARCHITECT_REVIEW.md` § "ADDENDUM — Human Architect ruling" — origin of this followup.
- `Docs/Specs/Completed/loop_v1_2b_camera_transitions/SPEC.md` § "Tests" — the Director EditMode test list this followup expands by one.
- `Docs/Specs/Active/loop_v1_2b_camera_transitions/screenshots/` — destination folder for the three captures.
- `tasks/lessons.md` — the controls_g lessons on defense-in-depth + time-driven smoke fragility.
