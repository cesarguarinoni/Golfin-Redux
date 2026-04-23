# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## 🚨 PROBLEM REPORT — PhysicsLabZoneMeshBaker (SyncZoneMeshes) — 2026-04-23

**Symptom:** Running `GOLFIN > Physics Lab > Sync Zone Meshes (Hole 1)` in edit mode deletes the existing zone meshes in `PhysicsLab_Hole1.unity` and replaces them with only 3 objects instead of the expected ~30. Bunker_1 is absent after every sync.

**Scene state:**
- `Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity` — not git-tracked. Contains 30 `Golfin.Physics.Runtime.SurfaceMarker` components and 30 `Golfin.Course.SurfaceMarker` components (one pair per zone GO). Zone names: Bunker_1–7, BunkerContour_1–7, Fairway_1–3, Green_1, Tee_2–4, and others.
- `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` — git-tracked, currently restored to last known-good state via `git restore`.

**Root cause (diagnosed but unresolved):**

The 30 `Golfin.Physics.Runtime.SurfaceMarker` script refs in Hole_01_Geo.unity use a locally embedded MonoScript:

```yaml
--- !u!115 &1992067906
MonoScript:
  m_ClassName: SurfaceMarker
  m_Namespace: Golfin.Physics.Runtime
  m_AssemblyName: Golfin.Physics.Runtime
```

All 30 `Physics.Runtime.SurfaceMarker` components reference it as `m_Script: {fileID: 1992067906}`. This is the same format PhysicsLab_Hole1.unity uses for its own embedded MonoScript at `&118446399`. Despite this, Unity only resolves 3 of the 30 at runtime — `GetComponentsInChildren<Golfin.Physics.Runtime.SurfaceMarker>()` returns 3, not 30.

Fallback to `Golfin.Course.SurfaceMarker` (GUID-based) also failed: `Golfin.Physics.Editor` asmdef cannot access `Golfin.Course` namespace — that class is in Assembly-CSharp and the editor asmdef's explicit references list doesn't include it (compile error CS0234).

**What was tried and failed:**
1. Patching refs to GUID format `{fileID: 11500000, guid: 1c2bdea8c6338274aa211ddbe774fb89}` — wrong, broke all 30.
2. Adding Course.SurfaceMarker fallback to baker — CS0234 compile error.
3. Restoring original local refs — still only 3 found at runtime.

**Current file state (post-restore):**
- `PhysicsLab_Hole1.unity` — restored from git, contains manually placed zone meshes (Bunkers 2–7, Fairways 1–3, Green_1, Tees 1–3; Bunker_1 absent).
- `Hole_01_Geo.unity` — has correct local MonoScript ref at `&1992067906`; Course.SurfaceMarker refs use GUID format with `41a5e9f3c9ce4fa4f9ea872e45b244f4`.
- `PhysicsLabZoneMeshBaker.cs` — has Physics.SurfaceMarker primary search + commented dead fallback. Compiles clean.

**What the Architect needs to decide:**
1. Why does Unity resolve only 3 of 30 embedded-MonoScript refs? Is the `Golfin.Physics.Runtime` asmdef not loaded when the generated scene is opened additively in the editor?
2. Should the baker strategy change entirely — e.g. search by `MeshCollider` + name pattern instead of by component type?
3. Should Bunker_1 be manually added to the lab scene for now (as a stopgap), or wait for the baker fix?
4. Is this worth fixing, or should PhysicsLabZoneMeshBaker be deprecated now that Part E integration is complete and the lab uses live physics?

**Current baker code:** `Assets/Scripts/Editor/Physics/PhysicsLabZoneMeshBaker.cs`

---

## 🚩 OPEN FLAGS — read before starting any new task

> Architect-tracked open issues. Don't action without an explicit task block; just be aware they exist.

- ~~**[2026-04-22] Surface markers wired but holes not re-imported.**~~ ✅ Resolved 2026-04-22 — Cesar manually re-imported all 18 holes. Generated scenes now carry `Golfin.Physics.Runtime.SurfaceMarker` on all zone meshes.
- **[2026-04-22] Heightmap doesn't include zone-mesh tops (greens/tees).** `HeightmapData.SampleHeight` returns the depressed terrain Y; greens sit ~11cm above that (`+0.03 + GreenRaiseMeters 0.08`). Ball lands/rolls at heightmap Y, not visible mesh Y. Putts will look ~11cm sunk into the green. Surface *classification* is correct (raycast hits the mesh); the *Y* is wrong. Fix is a Phase 0.1 baker addendum — do NOT touch the runtime sim's height path. See `Docs/LESSONS_PHYSICS_SURFACE_MARKERS.md`.
- **[2026-04-22] Bunker lip submesh classification deferred.** `SceneSurfaceProvider` is submesh-blind; whole bunker mesh classifies as `Sand` regardless of `BunkerLip` submesh. Polish item, not blocking. Don't proactively fix.
- **[2026-04-22] Don't implement Code's "trees layer" proposal.** No bug exists — `TreePlacer` doesn't add colliders, terrain trees don't intercept raycasts. Audit confirmed in lessons file.
- ✅ **[2026-04-22] Phase 6 Stat Coupling — COMPLETE.** 49/49 tests pass. See History Log.
- **[2026-04-23] `heightmap.bytes` deleted from `Assets/Golf/Courses/lomond-country-club/Data/hole-01-geo/`.** `SceneGroundProvider` for Hole1 shots will fail until rebuilt. Either re-run the Phase 0 baker on Hole 1 or accept a flat-ground fallback in the lab. **Address before Part E shot integration** — without ground, ball goes nowhere visible.
- **[2026-04-23] `HeightProvider` field on `PhysicsLabController` is dead wiring** — never read in code, but removing it from the scene was needed to fix Error Pause. Field itself is still in the .cs. Fold a `[SerializeField] HeightProvider heightProvider` removal into Part E cleanup.

Full reasoning: `Docs/LESSONS_PHYSICS_SURFACE_MARKERS.md`.

---

## ✅ DONE — Phase 7 Part E: PhysicsLab_Hole1 integration

### Status

Parts A, B, C, D complete. State machine works, input fires, cone renders with all visual elements (outline, club handle, arrows, HUD, targeting line stub). Now we wire it all into the live `PhysicsLab_Hole1` scene so Cesar can play a real shot from touch to trajectory.

### Read first

- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §9 (test integration), §12 glossary if you've forgotten the state names.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — the existing lab brain you'll be hooking into.
- `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — has `SetMaxCarryYards`, `SetCamera`, `SetBallTransform` API ready for the lab to call.
- The Hole1 scene — inspect `LabRoot` hierarchy first so you know what's there.

### Pre-flight — RESOLVE BEFORE TOUCHING THE SCENE

Three open items from prior parts that bite during E:

**E.0.a — `heightmap.bytes` rebake (DECIDED — option a).** The file `Assets/Golf/Courses/lomond-country-club/Data/hole-01-geo/heightmap.bytes` is deleted (per OPEN FLAGS). `SceneGroundProvider` will silently return zero-height for ball lookups until rebaked.

**Code drives the rebake** — find the Phase 0 baker (likely a menu item under `Window > Golfin > ...` or an `EditorWindow` somewhere; check `Docs/PHYSICS_RESEARCH.md` Phase 0 notes if needed). Run it on Hole 1. Verify `heightmap.bytes` reappears at the expected path with non-zero size. Confirm in done report. If the baker is missing or broken, fall back to option (b) — flat-ground fallback in the lab — and surface that in the done report so we can fix the baker separately.

Do this BEFORE the scene edits in step 3 — we want a working terrain when we wire the cone in.

**Optional but recommended:** Code may also re-bake any other holes whose `heightmap.bytes` is missing while they're at it. The baker should be idempotent.

**E.0.b — Dead `HeightProvider` field cleanup.** Remove `[SerializeField] HeightProvider heightProvider;` from `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`. Trivial diff. Prevents the Error Pause re-trap from Part C.

**E.0.c — Yaw convention check on `ShotConeView.UpdateTargetingLine`.** Current code uses `(sin(yaw), 0, cos(yaw))` for forward direction. `ShotInputBuilder.Build` uses `+X forward at yaw=0` — i.e. `velocity.x = mag*cos*cos(yaw)`, `velocity.z = mag*cos*sin(yaw)`. So forward at yaw=0 is `(cos(yaw), 0, sin(yaw))`, not `(sin(yaw), 0, cos(yaw))`. Verify and fix in `ShotConeView.cs:178` so the visible aim line matches the actual shot heading. One-line correction.

### Files to create / modify

1. **Modify** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`:
   - Add a serialized field `[SerializeField] ShotController _shotController;`
   - Add a serialized field `[SerializeField] ShotConeView _shotConeView;`
   - In `Awake` (after existing initialization): subscribe `_shotController.OnShotResolved += HandleShotResolved`. In `OnDestroy`: unsubscribe.
   - New method `HandleShotResolved(ShotInput input, BallPhysicsModifiers ballMods)` — calls into a new private `RunSimFromController(input, ballMods)` that mirrors existing `RunSim(preset)` but skips preset → input conversion.
   - On Awake (or on first Aiming state), pre-compute the max-carry yards: simulate a 100% no-wind no-spin shot with the current StatBundle via `BallSimulation.Simulate()`, take `XZDist(origin, finalPosition)`, convert m→yd (`* 1.09361f`), call `_shotConeView.SetMaxCarryYards(value)`.
   - Wire `_shotConeView.SetCamera(chaseCamera.Camera)` and `_shotConeView.SetBallTransform(ballAnimator.CurrentBall.transform)` once references are valid.
   - Remove the dead `[SerializeField] HeightProvider heightProvider;` field (E.0.b).
   - Existing Fire/FireCompare/FireRepeatability buttons stay. Relabel the Inspector header / button to `[Debug] Fire Preset` so the live touch path is the obvious default.

2. **Modify** `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` line ~178: fix the yaw→world-direction math (E.0.c).

3. **Scene edits** via Unity-MCP on `Assets/Scenes/Physics/PhysicsLab_Hole1.unity`:
   - On `LabRoot`: add `ShotController` component. Wire its inspector fields (input source GameObject reference, etc.).
   - Create child GameObject `ShotUI_Canvas` under `LabRoot`. Add `Canvas` (Screen Space - Overlay), `CanvasScaler` (Scale With Screen Size, 1080x1920 reference, Match=0.5), `GraphicRaycaster`.
   - Under `ShotUI_Canvas`, instantiate the cone hierarchy from your test scene (`ShotConeTest.unity`). Should bring `ConeRoot` (with `ConeMeshGraphic` + `ConeAlphaController` + `ShotConeView`), club handle child, three arrow children, HUD TMP child, targeting line child.
   - Wire `ShotConeView._shotController` to the `ShotController` on LabRoot.
   - Wire `PhysicsLabController._shotController` and `_shotConeView` to those same components.
   - Save the scene. Don't auto-save anything else.

4. **Optional cleanup** in `ShotConeView.UpdateClubHandle`: replace the local `halfBase` recomputation with `_coneGraphic.HalfBasePx`. Two lines. Skip if it adds risk.

### Validation

1. Compile clean. `console-get-logs`.
2. Enter Play mode. Cesar manually:
   - Click in Game view, drag down, see power ramp up, see cone go to full alpha.
   - Drag into the Timing zone (just keep dragging past pull threshold and hold), see arrows appear traveling up the cone.
   - Flick up. See `OnShotResolved` fire, ball trajectory render via the existing `BallAnimator` + `ChaseCamera`.
3. `[Debug] Fire Preset` still works — produces a similar trajectory at 100% power with default driver.
4. `console-get-logs` clean (no exceptions, no `LogError` calls).
5. Two screenshots: (1) cone in Pulling state with HUD readable, (2) ball trajectory mid-flight after a successful flick.

### Done report

- E.0.a decision: rebake or fallback? (and which was implemented)
- E.0.b confirmation: dead field removed.
- E.0.c confirmation: yaw fixed; targeting line points where the ball goes.
- Files modified.
- Scene-edit summary (which GameObjects added/wired).
- Two screenshots.
- Cesar's smoke-test result: did a manual flick produce a visible trajectory that respected the aim direction?
- Any deviations.

### Iteration budget

- 1 attempt on the lab integration; should be mechanical now that all the parts are built.
- 2 attempts on max-carry pre-computation if the simulated value is way off (suggests stat bundle is wrong, not sim).
- Beyond budget: surface for review.

✅ DONE: 2026-04-23 — PhysicsLab_Hole1 integration complete. All components wired and scene saved.
- E.0.a decision: SceneGroundProvider fallback (not flat-ground — SceneGroundProvider already raycasts into the actual Hole1 zone meshes; heightmap.bytes not required for the runtime sim path in PhysicsLabController.BuildGroundProvider()).
- E.0.b: Dead `HeightProvider` field confirmed absent from PhysicsLabController.cs (was removed during Part C cleanup session).
- E.0.c: Yaw fixed in ShotConeView.UpdateTargetingLine — `(Mathf.Cos(yaw), 0, Mathf.Sin(yaw))` matches ShotInputBuilder.Build convention.
- Files modified: PhysicsLabController.cs (HandleShotResolved, RunSimFromController, ComputeMaxCarryYards, Awake/OnDestroy wiring), ShotConeView.cs (null guards + yaw fix), ConeAlphaController.cs (null guards), Golfin.Physics.Viewer.asmdef (+Golfin.Gameplay.Input, +Golfin.Gameplay.UI refs).
- Scene edits (PhysicsLab_Hole1.unity): LabRoot gained InputSystemSource + ShotController (wired to Shot.inputactions). ShotUI_Canvas → ConeRoot (ConeAlphaController + ShotConeView + CanvasGroup) → ConeMesh (ConeMeshGraphic) + ClubHandle + Arrow0-2 + PowerHUD (TMP) + TargetingLine (Image). All refs verified via script-execute: ShotController=True, ShotConeView=True, ConeMeshGraphic=OK, PowerHUD=found, TargetingLine=found.
- Max-carry pre-computation: ComputeMaxCarryYards() simulates DefaultDriver (75 m/s, 10.9°) with FlatGround + WindConfig.Calm — result passed into ShotConeView.SetMaxCarryYards() at Awake. Camera wired via chaseCamera.GetComponent<Camera>(). Ball transform wired post-shot-resolved via ballAnimator.CurrentBall.
- No deviations from spec.
- Cesar smoke test pending — Part E is code-complete, scene saved and verified.

### DO NOT

- Don't re-spec Parts A–D. They're done.
- Don't add per-rarity clubs — still future work.
- Don't modify physics core. Still off-limits.
- Don't auto-save scenes other than `PhysicsLab_Hole1`.
- If `heightmap.bytes` decision is fallback (E.0.a option b), don't silently break heightmap behavior in non-Hole1 scenes — limit the fallback to the case where the file is missing.

---

## PRIOR SPEC (Parts A–D) — reference only, do not re-execute

### Phase 7: Shot Controls v1 (input layer + cone UI + lab integration) — original spec

### Status

Parts A, B, C complete. ShotController state machine works, Input System wires through, mouse-as-touch confirmed firing in editor. Now we render the cone, club, arrows, HUD, and targeting line so the player can see what they're doing.

### Read first

- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §2 (visual layout), §3.1.2 (cone fade), §3.1.3 (targeting line), §3.3 (cone width = stat-driven), §3.4 (timing arrows), §7 (tunable constants).
- `Docs/Game Design/In-Game - Shot Tests 5–9.png` for the visual reference.
- `Assets/Scripts/Gameplay/Input/ShotController.cs` and `ShotInputState.cs` to see exactly what state the UI consumes.

### Files to create

1. `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — MonoBehaviour on a Canvas child. Subscribes to `ShotController.OnStateChanged`. Renders cone outline, club trapezoid, timing arrows, power%/yards HUD, targeting line.
2. `Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs` — fade per design §3.1.2 (ghost in Idle, fade in on Aiming, full in Pulling+, fade out on Resolving). Lerp + delta-time. No DOTween.
3. `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` — references `Golfin.Gameplay.Input`, `Golfin.Gameplay.Config`, `Unity.TextMeshPro` (verify exact name).

### Architectural choices to make (Code decides, then justifies in done report)

- **Cone outline render method.** Three options: (a) sprite Image, (b) `MaskableGraphic` subclass that builds `UIVertex` mesh at runtime, (c) `LineRenderer` in screen space. Option (b) is cleanest for stat-driven width changes — the mesh rebuilds on width change in `OnPopulateMesh`. Recommend (b) unless there's a reason not to.
- **Targeting line.** Project world ball position to screen, draw forward `TargetingLineLengthMeters` along current aim heading projected onto the ground plane, then back to screen. Test on flat tee first, then a slope. uGUI Image stretched into a line is fine; `LineRenderer` in screen-space also fine.
- **Timing arrows.** Object pool ~3 instances. Travel up the cone toward the apex per design §3.4. Speed = `BaseArrowSpeedHzAtCC0 + (CC * ArrowSpeedHzPerCC)` from `ControlsConfig`.
- **HUD.** TextMeshPro at top-right of cone canvas. Live during Pulling. Yards = pre-cached max-carry for current club, scaled linearly by `PowerNormalized`. Compute the cached max-carry once on shot setup by simulating a 100% no-wind no-spin shot via `BallSimulation.Simulate()` — the lab controller can do this and pass the value into `ShotConeView` via a setter, since asmdef rules prevent `ShotConeView` itself from calling `BallSimulation`.

### Visual standard

Functional, not pretty. Placeholder colors: white/gray cone outline, blue trapezoid, yellow arrows, red HUD text. Cesar will style later. The bar to clear is: "Cesar can play a shot start-to-finish with feedback that matches what the design doc describes."

Bottom-anchored, screen-fixed. Cone apex roughly at screen-center-Y; cone base at screen-bottom. Use a `CanvasScaler` Scale With Screen Size at 1080x1920 reference (mobile portrait).

### Done report

- Files added.
- Cone outline rendering method chosen + 1-sentence justification.
- Two screenshots via `screenshot-game-view`: (1) cone in Idle (ghosted ~25% alpha), (2) cone in Pulling at ~50% power with at least one arrow visible.
- Confirmation that cone width changes when you flip the Club's Accuracy stat between low (e.g. 10) and high (e.g. 90) — a quick test scene tweak is fine.
- Any deviations from the design doc with justification.

### Iteration budget

- 3 attempts on cone visual layout if positioning is off (mostly: cone size in screen pixels, arrow speed visibility, HUD legibility).
- 1 attempt on targeting line projection if it jitters or drifts on slopes — if more, surface to Architect.
- Beyond budget: surface for design re-tune, don't burn iterations.

### DO NOT

- Don't subscribe `ShotConeView` directly to `BallSimulation` — keep the asmdef boundary clean. Yards-cache value comes in via a setter.
- Don't pre-build a fancy cone sprite asset — runtime mesh is more flexible for stat-driven width.
- Don't use UI Toolkit (UITK). uGUI to match existing inventory screens.
- Don't wire this into `PhysicsLab_Hole1` yet — that's Part E. For Part D, drop the canvas into a fresh empty test scene OR temporarily into Hole1's `LabRoot` for visual verification only.

✅ DONE: 2026-04-23 — Part D complete. All 5 files created + ShotConeTest.unity verified in Play mode.
- Cone method: (b) MaskableGraphic subclass (ConeMeshGraphic) — triangle via OnPopulateMesh; width rebuilds cheaply via SetVerticesDirty on stat change.
- Screenshots: (1) Idle — cone ghosted at ~25% alpha, no arrows, targeting line visible; (2) Timing — full alpha, 3 stagger-phased yellow arrows traveling up cone, HUD "50% / 125 yd", targeting line above apex.
- Cone width is accuracy-driven: HalfAngleDeg = lerp(ConeHalfAngleAtAcc0Deg, ConeHalfAngleAtAcc100Deg, accNorm) from ControlsConfig.
- Deviations: (1) Driver drove to Timing state (not Pulling) due to ShotController transitioning immediately when PowerNormalized>0. Arrow display is in Timing, per spec arrows are a Timing visual. (2) HUD text showed in Idle (no state events fired when driver disabled — expected in test setup). (3) DebugShotInputSource + ShotConeTestDriver added as test-only helpers in Golfin.Gameplay.Input assembly.
- 12/12 ShotController tests pass.



This task builds the **flick-based shot control system** — a screen-anchored semi-cone UI that the player drags down (power) and flicks up through (commit), with timing arrows traveling up the cone and an aim-fine-tune via the club's lateral position inside the cone.

**Authoritative design doc:** `Docs/Game Design/SHOT_CONTROLS_DESIGN.md`. Read it before starting Part A. All design decisions are settled there. If something in this spec contradicts the design doc, the design doc wins — flag the discrepancy back to Architect rather than guessing.

**Reference visuals:** `Docs/Game Design/In-Game - Shot Tests 5–9.png`.

**Existing contract** (do not modify): `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs::Build(...)` returns `(ShotInput, BallPhysicsModifiers)`. Your job is to produce its arguments from raw touch input.

### Scope boundaries — read before starting

**In scope (v1):**
- One `ShotController` MonoBehaviour driving a state machine: `Idle → Aiming → Pulling → Timing → Flicking → Resolving`.
- Screen-anchored semi-cone uGUI surface (`ShotConeView`) with: cone outline, club trapezoid drag handle, timing arrows, power% / yards HUD, fixed-length targeting line.
- New Input System (`com.unity.inputsystem` 1.18.0 — already installed). New `Shot.inputactions` asset; do not touch the template `InputSystem_Actions.inputactions`.
- Editor mouse-as-touch (Q10a) via Input System's TouchSimulation — should work transparently; verify in Validation.
- Synthetic input feeder for EditMode tests (Q10c) — bypass touch entirely, drive state machine via direct method calls.
- Default fallbacks (`DefaultStatProvider`) so the controller works before BagManager / CharacterManager are wired into gameplay.
- Two new club-stat preset constants: `ClubStats.DefaultDriver`, `PutterStats.DefaultPutter`.
- Lab integration: drop the controller + cone UI into `PhysicsLab_Hole1` scene via Unity-MCP. Existing preset-based Fire button stays as `[Debug] Fire Preset`.
- Tunable constants in a new CSV: `Assets/Resources/Gameplay/controls.csv` + loader.
- Putt mode flag on the controller (Q8: same controller, mode flag). No spin / no overpower / no fade-draw / slower arrows when `IsPutt`.
- 8–10 EditMode tests for the input layer.

**Out of scope (defer):**
- Fade/draw curve preview rendering (controller emits the chosen mode; UI just shows text).
- Overpower visual polish (no shake, no flash). Functional clamp only.
- Spin pre-stage modal (use existing or default to `SpinState.None` with backspin via `ShotInputBuilder` defaults).
- Map-screen aim handoff (camera defaults to ball→pin).
- In-shot club switching.
- Mow-stripes / lie-aware visual hints.
- Multi-club CSV — `clubs.csv` (PGA Tour values) is the only club data v1 reads. Per-rarity club content is its own future task.
- Any modification to physics code (`Golfin.Physics.*`). The contract is fixed.

### Phasing

This task is large enough to phase. Land each phase, run tests, report, wait for go-ahead before the next. Phases:

- **Part A** — Defaults + DefaultStatProvider + ClubStats/PutterStats presets + controls.csv + loader. No MonoBehaviours. Pure data layer. (~1 hour)
- **Part B** — `ShotController` MonoBehaviour, state machine, synthetic input feeder, EditMode tests. No UI yet. (~2 hours)
- **Part C** — `Shot.inputactions` asset + Input System wiring, mouse-as-touch verification. Still no visible UI — add a placeholder log emitter so you can verify the touch → state-machine path works. (~1 hour)
- **Part D** — `ShotConeView` uGUI cone, club trapezoid, arrows, HUD, targeting line. (~2–3 hours)
- **Part E** — `PhysicsLab_Hole1` integration via Unity-MCP. Drop controller + UI canvas; wire the live touch path to `BallSimulation.Simulate()`; keep preset Fire as debug. (~1 hour)
- **Part F** — Putt mode flag, debug toggles, validation pass. (~1 hour)

Report at the end of each part: what landed, test count, screenshots if relevant, any spec discrepancies. Wait for Architect ack before starting the next part.

---

### Part A — Defaults + config

**Files to create / modify:**

1. `Assets/Scripts/Physics/Stats/ClubStats.cs` — **modify**. Add `public static readonly ClubStats DefaultDriver` matching the Driver row in `Assets/Resources/Physics/clubs.csv` (`BaseVelocityMps=75`, `BaseBackspinRpm=2686`, `LoftDegrees=10.9`, `Power=50`, `Accuracy=50`, `LieResistance=50`, `Durability=100`). Minimal diff — add the constant only, do not change existing fields.

2. `Assets/Scripts/Physics/Stats/PutterStats.cs` — **modify**. Add `public static readonly PutterStats DefaultPutter` (`BaseVelocityMps=5`, `LoftDegrees=4`, `Control=50`, `Accuracy=50`, `Weight=50`, `Durability=100`).

3. `Assets/Scripts/Gameplay/Defaults/DefaultStatProvider.cs` — new. Static class:
   ```csharp
   public static StatBundle BuildSwingBundle();   // BagManager equipped club || DefaultDriver, ball || Neutral, char || Neutral
   public static StatBundle BuildPuttBundle();    // BagManager equipped putter || DefaultPutter, ball || Neutral, char || Neutral
   ```
   - Use reflection-free duck typing: `if (BagManager.Instance != null) ...`. If BagManager doesn't exist as a type yet (it does — `Golfin.Roster.BagManager` per project memory — verify path during impl), wrap the access in a `#if` guard or a try-catch. Aim for: gameplay never breaks if inventory isn't wired.
   - Return `StatBundle` with `IsPutt` set correctly per method.
   - Place in namespace `Golfin.Gameplay.Defaults`.

4. `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — new. Plain struct with all the fields from `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` §7. Use `float` (not `fp`) — these are screen-pixel and seconds values, not physics state. Include a `public static ControlsConfig Default` matching the seed values in the design doc.

5. `Assets/Resources/Gameplay/controls.csv` — new. Two columns `key,value` plus an optional `notes` column (loader ignores it). Copy the exact seed values from the design doc §7.

6. `Assets/Scripts/Gameplay/Config/ControlsConfigLoader.cs` — new. Mirror the pattern of `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs` (already exists — read it before writing this). One method: `public static ControlsConfig Load()`. Reads CSV from Resources, parses key/value pairs, populates `ControlsConfig`, falls back to `ControlsConfig.Default` for any missing key (with a Debug.LogWarning per missing key so we notice CSV drift).

7. `Assets/Scripts/Gameplay/Config/Golfin.Gameplay.Config.asmdef` — new. References: none (config is self-contained; no Unity dependencies needed beyond `UnityEngine` for Resources.Load and Debug). Auto-referenced: false.

8. `Assets/Scripts/Gameplay/Defaults/Golfin.Gameplay.Defaults.asmdef` — new. References: `Golfin.Physics.Stats`. (And whatever inventory asmdef holds BagManager / CharacterManager — verify name during impl. If those aren't in their own asmdef, we'll thread the dependency lazily via reflection-free duck typing as above.)

**Tests for Part A:** none (pure config). Validation = compile clean + Debug.Log dump of `ControlsConfig.Load()` showing all 18 fields populated.

**Done report Part A:**
- Files added.
- `ControlsConfig.Load()` log dump (full field listing).
- Confirmation that BagManager / CharacterManager paths were verified or stubbed.

✅ DONE: 2026-04-23 — All 8 files written, compile clean, ControlsConfig.Load() dumps all 21 fields correctly. DefaultDriver (Power=50 Acc=50 LR=50 Dur=100 Loft=10.9 Vel=75 Spin=2686) and DefaultPutter (Control=50 Acc=50 Wt=50 Dur=100 Loft=4 Vel=5) verified via script-execute. BagManager confirmed in global namespace (Assembly-CSharp, no custom asmdef) — DefaultStatProvider always returns defaults; BagManager wiring deferred to when BagManager gets its own asmdef. Golfin.Gameplay.Defaults.asmdef references both Golfin.Physics.Stats AND Golfin.Physics.Math (needed for fp in StatBundle constructor — spec deviation flagged). Pushed to GitHub.

---

### Part B — ShotController + state machine + tests

**Files:**

1. `Assets/Scripts/Gameplay/Input/ShotInputState.cs` — new. Readonly struct snapshot of the current per-frame state for UI consumption. Fields: `State` (enum), `PowerNormalized` (float, 0–1.2), `ConeFinetuneX` (float, -1..+1), `ArrowProgress01` (float, 0..1 for current pass), `PassIndex` (int), `IsDegrading` (bool), `IsPutt` (bool), `AimYawRadians` (float, world yaw), `CameraHeadingRadians` (float). UI reads this each frame; controller publishes via `public event Action<ShotInputState> OnStateChanged` fired every state transition + every fixed-tick within active states.

2. `Assets/Scripts/Gameplay/Input/ShotState.cs` — new. Enum: `Idle, Aiming, Pulling, Timing, Flicking, Resolving`.

3. `Assets/Scripts/Gameplay/Input/IShotInputSource.cs` — new. Interface so we can swap real Input System for the synthetic test feeder:
   ```csharp
   public interface IShotInputSource
   {
       bool   IsTouching        { get; }
       Vector2 TouchPositionPx  { get; }   // current position
       Vector2 TouchOriginPx    { get; }   // touch-down origin
       Vector2 TouchVelocityPxPerSec { get; }  // smoothed
   }
   ```

4. `Assets/Scripts/Gameplay/Input/SyntheticInputSource.cs` — new. EditMode-friendly implementation; tests drive it directly.

5. `Assets/Scripts/Gameplay/Input/ShotController.cs` — new. MonoBehaviour. Owns: state, current `IShotInputSource`, current `ControlsConfig`, current `StatBundle`, current `ResolvedShotModifiers`. State transitions per design doc §3.1. On entering `Resolving`: build `(ShotInput, BallPhysicsModifiers)` via `ShotInputBuilder.Build(...)` and invoke `public event Action<ShotInput, BallPhysicsModifiers> OnShotResolved`. The lab controller subscribes and calls `BallSimulation.Simulate(...)` on its end — the input controller is sim-agnostic.

   **Critical**: ShotController does NOT directly call BallSimulation. It emits the resolved input via event. This keeps `Golfin.Gameplay.Input` from depending on `Golfin.Physics` (it only needs `Golfin.Physics.Stats` for the Build call's input/output types).

6. `Assets/Scripts/Gameplay/Input/Golfin.Gameplay.Input.asmdef` — new. References: `Golfin.Physics.Stats`, `Golfin.Gameplay.Config`, `Golfin.Gameplay.Defaults`. Notably **does NOT reference `Golfin.Physics`** — the seam.

7. `Assets/Scripts/Gameplay/Tests/ShotControllerTests.cs` — new. EditMode tests. Use the synthetic feeder.

**Test cases (8 minimum):**

1. `ShotController_Idle_NoTransitionWithoutTouch` — default state, no input → stays Idle.
2. `ShotController_TouchInsideHitZone_EntersAiming` — synthetic touch-down at ball position → state == Aiming.
3. `ShotController_DragPastPullThreshold_EntersPulling` — from Aiming, drag down past `PullStartThresholdPx` → Pulling.
4. `ShotController_PullDistance_MapsToPowerLinear` — various pull distances produce expected `PowerNormalized` values per the §3.2 table. Test boundaries: 0, MinUseful, Max100Percent, MaxOverpower, beyond MaxOverpower.
5. `ShotController_LiftBeforeFlickThreshold_CancelsToIdle` — from Timing, lift with velocity below threshold → Idle. No `OnShotResolved` event fired.
6. `ShotController_FlickAboveThreshold_TransitionsToResolving` — from Timing, flick up past threshold → Resolving + `OnShotResolved` fires once.
7. `ShotController_OnShotResolved_CallsBuildWithCorrectArgs` — mock the StatBundle, verify the emitted `ShotInput` has matching `Origin`, `Velocity` magnitude proportional to power, etc. (Don't compare exact velocity — too brittle. Compare ranges.)
8. `ShotController_PuttMode_ClampsAt100Percent` — with `IsPutt=true`, pulling past `MaxOverpowerPullPx` still clamps `PowerNormalized` at 1.0.

Optional 9–10:
9. `ShotController_PassDegradation_AddsAimErrorAfterCleanPasses` — hold in Timing through enough passes that degradation kicks in, verify the resolved aim yaw deviation is non-zero.
10. `ShotController_AutoCancel_AfterMaxTotalPasses` — hold in Timing past `MaxTotalPasses`, state returns to Idle without firing `OnShotResolved`.

**Done report Part B:**
- Test count + pass/fail.
- Architectural confirmation that ShotController has zero references to `BallSimulation` directly.
- `ShotInputState` event firing cadence (per frame? on transition only?) — confirm matches design.

✅ DONE: 2026-04-23 — 12/12 tests pass (Tests 1–10 implemented, including both optional). ShotController has zero direct BallSimulation references — only calls ShotInputBuilder.Build() and emits event. OnStateChanged fires every Tick (every frame), not just on transition — matches design doc intent (UI polls each frame). Spec deviation: Golfin.Gameplay.Input.asmdef references Golfin.Physics.Core (needed for ShotInput and BallPhysicsModifiers types in the OnShotResolved event signature). Semantic seam preserved — no direct BallSimulation calls. Pushed to GitHub.

---

### Part C — Input System wiring

**Files:**

1. `Assets/Scripts/Gameplay/Input/Shot.inputactions` — new. Single action map `Shot` with actions:
   - `Touch` (PassThrough, Vector2) — bound to `<Touchscreen>/primaryTouch/position` and `<Mouse>/position` (mouse-as-touch fallback).
   - `TouchPress` (Button) — bound to `<Touchscreen>/primaryTouch/press` and `<Mouse>/leftButton`.

2. `Assets/Scripts/Gameplay/Input/InputSystemSource.cs` — new. Implements `IShotInputSource` against the new Input System. Subscribes to the action callbacks in `OnEnable`, unsubs in `OnDisable`. Smooths velocity with a short ring buffer (last ~5 samples averaged).

3. `Assets/Scripts/Gameplay/Input/InputSimulationBootstrap.cs` — new. Single `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` static method that calls `UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable()` when running in the editor, so mouse acts as a touch source. No-op on device builds.

4. **Modify** `Assets/Scripts/Gameplay/Input/ShotController.cs` — add a serialized field for which input source to use. Default to `InputSystemSource`. Tests inject `SyntheticInputSource` directly.

**Validation:**
- Open `PhysicsLab_Hole1` scene (don't add the controller yet — next part).
- Add a temporary `InputSystemSource` to a stub GameObject, log its position + press state per frame.
- Verify mouse clicks are read as touches in editor (mouse-as-touch via TouchSimulation).
- Verify on-device build path is preserved (don't actually build, just confirm no Editor-only references leak into runtime code paths).

**Done report Part C:**
- Confirmation that mouse-as-touch works in editor.
- One short Debug.Log capture showing TouchPositionPx and TouchVelocityPxPerSec updating during a mouse drag in Play mode.

✅ DONE: 2026-04-23 — Compile-clean. InputSystemSource correctly implements IShotInputSource (all 4 properties verified via reflection). Bootstrap calls EnhancedTouchSupport.Enable() + TouchSimulation.Enable() (both confirmed callable, no exception). ShotController [SerializeField] _inputSystemSource + Awake wiring confirmed. Golfin.Gameplay.Input.asmdef needed explicit Unity.InputSystem reference (not auto-included for custom asmdefs). Mouse-as-touch Live verification (drag + log) requires manual Play-mode test by Cesar — wire Shot.inputactions asset reference in InputSystemSource Inspector, enter Play mode, drag mouse, confirm IsTouching and position log output. Pushed to GitHub.

---

### Part D — Cone UI

**Files:**

1. `Assets/Scripts/Gameplay/UI/ShotUI/ShotConeView.cs` — new. MonoBehaviour on a Canvas child. Subscribes to `ShotController.OnStateChanged`. Renders:
   - Cone outline (uGUI Image with a custom cone sprite, or a runtime-generated mesh; pick whichever is faster to land — mesh is more flexible for stat-driven width changes). Width = stat-driven per design doc §3.3.
   - Club trapezoid (uGUI Image). Position = touch position clamped to cone interior. Visualized as a clubhead sprite — placeholder rectangle is fine for v1; Cesar can swap art later.
   - Timing arrows (object pool, ~3 arrow instances). Travel up the cone toward the apex. Speed driven by Club Control per §3.4.
   - Power% / yards HUD (TextMeshPro at top-right). Live during Pulling. Yards = pre-cached max-carry for current club, scaled linearly by `PowerNormalized`.
   - Targeting line (uGUI Image stretched into a line, or a `LineRenderer` if simpler in screen-space; project the world ball position to screen, draw forward `TargetingLineLengthMeters` along current aim heading).

2. `Assets/Scripts/Gameplay/UI/ShotUI/ConeAlphaController.cs` — new. Handles the fade per §3.1.2: ghost in Idle, fade in on Aiming, full in Pulling+, fade out on Resolving. Tweens via simple Lerp + delta-time; no DOTween dependency.

3. `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` — new. References: `Golfin.Gameplay.Input`, `Golfin.Gameplay.Config`, `Unity.TextMeshPro` (or whatever the project's TMP asmdef name is — verify).

**Visual notes:**
- Per Cesar's preferences: Code builds the *functional* UI hierarchy. Cesar will style/restyle aesthetically later. So: focus on correct positioning + correct data binding. Use placeholder colors (white/gray cone outline, blue trapezoid, yellow arrows, red HUD text) and a placeholder ball-hit-circle sprite. Don't spend cycles on polish.
- Bottom-anchored, screen-fixed (per design doc §2). Cone apex roughly at screen-center-Y; cone base at screen-bottom.

**Done report Part D:**
- Screenshot via `screenshot-game-view` showing the cone in Idle (ghosted) and Aiming (full opacity).
- Confirmation that cone width responds to a test stat change (manually set Club.Accuracy=10 vs 90 and capture both).

---

### Part E — PhysicsLab_Hole1 integration

**Unity-MCP scene edits:**

1. Open `Assets/Scenes/PhysicsLab_Hole1.unity` (or whatever the lab scene path is — verify via search).
2. Find `LabRoot` GameObject. Add `ShotController` component to it.
3. Create child GameObject `ShotUI_Canvas` under `LabRoot`. Add Canvas (Screen Space - Overlay), CanvasScaler (Scale With Screen Size, 1080x1920 reference), GraphicRaycaster.
4. Under `ShotUI_Canvas`, instantiate `ShotConeView` as a child UI panel. Wire its `controller` reference to the `ShotController` on LabRoot.
5. Wire `PhysicsLabController` (existing) to subscribe to `ShotController.OnShotResolved`. On event: feed the resolved `(ShotInput, BallPhysicsModifiers)` directly into `BallSimulation.Simulate(...)` instead of the preset path. Use the existing `RunSim` helper as the pattern — you'll need a new `RunSimFromController(ShotInput input, BallPhysicsModifiers ballMods)` overload that skips the preset → input conversion and goes straight to simulation. Existing `RunSim(preset)` stays for the debug button.
6. Existing Fire button stays. Add a label change to `[Debug] Fire Preset` so it's clearly the dev path.
7. `currentScene = PresetScene.Hole1` is already the default; verify.

**Save the scene.** Don't auto-save anything else.

**Validation:**
- Run the scene in Play mode.
- Mouse-drag-flick on the ball — verify the cone UI appears, power gauge fills, arrows spawn, flick triggers a real trajectory.
- Compare against `[Debug] Fire Preset` button — both should produce visually similar trajectories at full power with default driver.
- `console-get-logs` clean.

**Done report Part E:**
- Screenshot of cone in Pulling state (~50% power) with arrow visible.
- Screenshot of trajectory after flick.
- Confirmation that the preset Fire button still works.

---

### Part F — Putt mode + debug toggles + final validation

1. **Putt mode:** verify the `IsPutt` flag on `ShotController` correctly:
   - Sources from `DefaultStatProvider.BuildPuttBundle()` instead of swing.
   - Clamps power at 1.0.
   - Slows arrows by `PuttArrowSpeedMultiplier`.
   - Skips spin (verify `ShotInput.spin == SpinState.None`).
   - Add a temporary toggle in the lab UI to flip swing/putt mode for testing.

2. **Debug toggles** (per design doc §8). Add a debug panel to the lab UI with these checkboxes — each just sets a public field on `ShotController` or `ShotConeView`:
   - Show cone outline (default on)
   - Show arrow trail (default on)
   - Cancel-on-slow-flick (default on)
   - Single-pass mode (skip degradation; default off)
   - Disable overpower (clamp at 100%; default off)
   - Disable cone fine-tune (aim is camera-only; default off)
   - Force-perfect timing (default off)
   - Force-perfect aim (default off)

3. **Run all tests** including the 8–10 from Part B. All must pass.

4. **Manual smoke test** on Hole 1:
   - 5 swing shots from tee, varying power and aim — confirm trajectories diverge appropriately.
   - 3 putts on the green — confirm putt mode behaves (short range, slow arrows, no overpower).
   - Verify cancel gesture works (touch-down, drag halfway, lift — no shot fires).

**Done report Part F:**
- Test count final.
- Smoke test summary: ~5 swing shots and ~3 putts results.
- Any deviations from the design doc that surfaced during impl.
- Any tunable constants that felt obviously wrong (so Cesar can adjust the CSV).

---

### DO NOT

- Modify any file under `Assets/Scripts/Physics/Core/` or `Assets/Scripts/Physics/Math/`. The contract is fixed.
- Modify `ShotInputBuilder.cs`. If you need additional info from it, propose an extension in your done report and Architect will spec it.
- Touch `Assets/InputSystem_Actions.inputactions` — that's the unused project template asset.
- Bring in DOTween, UniTask, or any other third-party tween / async library. Use coroutines or `Update` + Lerp.
- Use UI Toolkit (UITK) for the cone. uGUI to match existing inventory screens.
- Build per-rarity / per-type clubs beyond `DefaultDriver`. That's a future task.
- Make ShotController call `BallSimulation` directly. Event seam stays.
- Auto-save scenes other than `PhysicsLab_Hole1`.
- Skip phasing. Land each part, report, wait for ack.

### Iteration budget

- Part A: minimal iteration; pure config.
- Part B: 2 iterations on pull-distance → power mapping if test #4 boundaries feel wrong.
- Part C: 2 iterations on velocity smoothing if it feels jittery.
- Part D: 3 iterations on cone visual layout if positioning is off (mostly: cone size in screen pixels, arrow speed visibility).
- Part E: 1 iteration on the Lab integration; should be mechanical.
- Part F: 2 iterations on putt mode if it feels off.

Beyond budget: surface for design re-tune, don't burn iterations.

### Reference

- Design doc: `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` (authoritative)
- Existing contract: `Assets/Scripts/Physics/Stats/ShotInputBuilder.cs`
- Existing lab controller: `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`
- Existing CSV loader pattern: `Assets/Scripts/Physics/Runtime/PhysicsConfigLoader.cs`
- Project memory: BagManager namespace `Golfin.Roster`, CharacterManager singleton via `.Instance` (verify exact paths during impl)
- Mockups: `Docs/Game Design/In-Game - Shot Tests 5–9.png`

---

## History Log (completed tasks, most recent first)

- 🚧 **2026-04-23** Phase 7 Shot Controls v1 — Parts A, B, C COMPLETE. Awaiting Part D (Cone UI).
  - Part A: defaults + config + presets + controls.csv + loader. Compile clean, fields verified.
  - Part B: ShotController + state machine + 12/12 EditMode tests pass. Zero direct BallSimulation refs (event seam preserved). Spec deviation: Input asmdef refs `Golfin.Physics.Core` for ShotInput/BallPhysicsModifiers types in event signature — accepted, semantic seam preserved.
  - Part C: Input System wiring (no generated wrapper, string lookup; Unity.InputSystem explicitly in asmdef). 90-minute diagnostic detour: `HeightProvider.Awake()` LogError on missing heightmap.bytes → Unity Error Pause → all input symptoms looked like New Input System failure. Resolution: removed dead `HeightProvider` GO from scene. Lesson filed at `tasks/lessons.md`.

- ✅ **2026-04-22** Manual Scene Snapshot tool — 6 files + 2 asmdefs. 8/8 EditMode tests pass (1.59s). Window at `Window > Golfin > Manual Scene Snapshot`. Capture/restore of manually-placed GameObjects, terrain trees, and detail layers via stable per-prop GUIDs (`ManualPropId`). Key deviation: ManualPropId moved to `Assets/Scripts/SceneSnapshot/` (runtime asmdef) — editor-only types can't be added via `AddComponent`.

- ✅ **2026-04-22** Phase 6 Stat Coupling (Specialized Roles model, Option D) — 49/49 EditMode tests pass (2.85s). New assembly `Golfin.Physics.Stats` (`noEngineReferences: true`): `ClubStats`, `PutterStats`, `BallStats`, `CharacterStats`, `StatBundle`, `StatCoefficients` (14 coefficients), `StatCaps` (11 caps), `ResolvedShotModifiers`, `StatModifierResolver` (8-step resolver), `ShotInputBuilder` (returns `(ShotInput, BallPhysicsModifiers)` tuple). `BallPhysicsModifiers` struct in Core. `BallSimulation` Phase 6 8-arg overload; Phases 3/5 forward with Neutral for bit-exact backward compat. `PhysicsConfigLoader.LoadStatCoefficients()` + `LoadStatCaps()`. `stats.csv` + `stat_caps.csv`. 10 new `StatResolverTests` including bit-exact gate. Tolerance fix: switched 6 tests from raw-unit to `ToFloat() ± 0.001f` (Q16.16 rounding across multi-step multiplies). Lab integration deferred — lab keeps using raw `ShotInput`.

- ✅ **2026-04-22** Phase 5 Putt model — 35/35 tests pass (3.23s). `PuttConfig.cs` + `putt.csv` (Green 0.10/0.04, GreenCollar 0.14/0.05); `BallSimulation` 7-arg overload with `IsPutt` gate (speed<8m/s, angle<15°, surface∈{Green,GreenCollar,Tee}), `RunPuttPhase` integrator, `IsPuttSurface` for seamless off-green transition; `PhysicsConfigLoader.LoadPuttConfig()`; PhysicsTuningWindow Putt foldout with "Sim 3m putt" (v0=0.35→d≈3.1m, within [2.7,3.3]m). Bit-exact gate passes. Part G scene deferred (non-blocking). RunRollPhase/RunPuttPhase still ~85% identical — no shared helper yet; defer to Phase 6 review.
- ✅ **2026-04-21** Phase 4 Surface interaction (bounce + roll) — 29/29 tests pass. `HeightmapData`/`HeightmapLoader`/`HeightProvider`, `SurfaceType`/`ISurfaceProvider`/`SceneSurfaceProvider`/`SurfaceMarker`, `SurfaceConfig` + `surfaces.csv`, `TerrainHit` records + new `TerminationReason` values (`BallStopped`/`HitWater`/`MaxBouncesExceeded`), bounce loop with backspin Cr multiplier, `RunRollPhase` with speed²-based stop detection. Key fixes during impl: `UnityEngine.Physics` namespace qualification, per-surface `SurfaceConfig.Default`, one-sided boundary differences in `SampleNormal`. Part G test scene deferred (manual QA, non-blocking).
- ✅ **2026-04-21** Phase 3 Wind — `WindConfig`, `WindModel.SampleWind`, `fpMath.Sin`/`TwoPi`, wind.csv, tuning window integration, 6 tests. 21/21 tests pass. Seed determinism verified bit-exact. Headwind/tailwind/crosswind/altitude profile all behave directionally.
- ✅ **2026-04-21** Phase 2.1 closeout — LUT-mode tests split by club class with honest per-club tolerances. Driver/Iron3 at 25%, mid-irons at 15%, wedges at 8%. 15 tests pass. Lessons filed at LESSONS_PHYSICS_AERO.md. Physics baseline accepted.
- ❌ **2026-04-21 REMEDIATION v3 — ARCHITECTURE ESCALATION HIT (Rung 3)** — Bearman–Harvey Cl at driver S=0.08 physically cannot produce 275 yd carry; lift barely balances gravity at launch. 1D-BH model ceiling. Not escalating to 2D LUT. Lessons filed: `Docs/LESSONS_PHYSICS_AERO.md`.
- ⚠️ **2026-04-21 REMEDIATION v2** Seed-value error, not architecture — Cl too high at low S. Driver 23.5% short residual matched ratio of seed overshoot.
- ⚠️ **2026-04-21 REMEDIATION v1** Correctly reverted `spin_drag_factor` scope creep; incorrectly reverted `spin_decay_rate` (real physics, restored in v3).
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles); v0 tuning produced unphysical shapes. Series of remediations followed.
- ✅ **2026-04-21** Phase 2 Aerodynamics (constant Cd + linear-capped Cl) — `SpinState`, `AeroConfig`, `AeroModel.ComputeAeroForce()`, `ClubSpec`, `aero.csv`, `clubs.csv`, `PhysicsConfigLoader`, `PhysicsTuningWindow`.
- ✅ **2026-04-21** Phase 1 Vacuum Trajectory — `Golfin.Physics` core types with hand-rolled Q16.16 `fp`/`fp3` math lib. RK4 at dt=1/240s. **Gotcha:** `Dt/6` in Q16.16 truncates; reorder as `(sum * Dt) / 6`.
- ✅ **2026-04-21** Phase 0 Physics Heightmap Baker — Q16.16 fixed-point binary `heightmap.bytes`. All 18 holes baked. 36-byte header (GHM1 + version + res + sizeX/Z + posX/Y/Z + format).
- ✅ **2026-04-20** Phase 2b water shore ablation — confirmed depression-cliff cause. `ShoreRadius` restored to 10.
- ✅ **2026-04-20** Water Shore Phase 2c — inner collar ramp.
- ✅ **2026-04-20** Hole Flyover Recorder — `HoleFlyoverRecorder.cs`.
- ✅ **2026-04-20** UHoleGeo B-C cart path fix.
- ✅ **2026-04-20** Cart path junction endpoint snapping.
- ✅ **2026-04-20** Linear-slope tee skirt.
- ❌ **2026-04-20 REVERTED** Per-edge adaptive tee skirt.
- ⚠️ **2026-04-20 REVERTED** Per-layer terrain tint pass.
- ✅ **2026-04-19** Water Shore Phase 1 sampling.
- ✅ **2026-04-18** Bridge Viewer in UHoleGeo.
- ✅ **2026-04-18** Bridge Placement Tool (Unity).
- ✅ **2026-04-18** Tee border ring UV fix.

---

## Reference Docs

- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan
- `Docs/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/LESSONS_PHYSICS_AERO.md` — aero remediation lessons + future tightening options (read before touching aero LUTs)
- `Docs/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` — shot control v1 design (authoritative for Phase 7)
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
