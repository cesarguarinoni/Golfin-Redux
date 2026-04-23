# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## 🚩 OPEN FLAGS — read before starting any new task

> Architect-tracked open issues. Don't action without an explicit task block; just be aware they exist.

- ~~**[2026-04-22] Surface markers wired but holes not re-imported.**~~ ✅ Resolved 2026-04-22 — Cesar manually re-imported all 18 holes. Generated scenes now carry `Golfin.Physics.Runtime.SurfaceMarker` on all zone meshes.
- **[2026-04-22] Heightmap doesn't include zone-mesh tops (greens/tees).** `HeightmapData.SampleHeight` returns the depressed terrain Y; greens sit ~11cm above that (`+0.03 + GreenRaiseMeters 0.08`). Ball lands/rolls at heightmap Y, not visible mesh Y. Putts will look ~11cm sunk into the green. Surface *classification* is correct (raycast hits the mesh); the *Y* is wrong. Fix is a Phase 0.1 baker addendum — do NOT touch the runtime sim's height path. See `Docs/LESSONS_PHYSICS_SURFACE_MARKERS.md`.
- **[2026-04-22] Bunker lip submesh classification deferred.** `SceneSurfaceProvider` is submesh-blind; whole bunker mesh classifies as `Sand` regardless of `BunkerLip` submesh. Polish item, not blocking. Don't proactively fix.
- **[2026-04-22] Don't implement Code's "trees layer" proposal.** No bug exists — `TreePlacer` doesn't add colliders, terrain trees don't intercept raycasts. Audit confirmed in lessons file.
- ✅ **[2026-04-22] Phase 6 Stat Coupling — COMPLETE.** 49/49 tests pass. See History Log.

Full reasoning: `Docs/LESSONS_PHYSICS_SURFACE_MARKERS.md`.

---

## ACTIVE TASK — Phase 7: Shot Controls v1 (input layer + cone UI + lab integration)

### Context

Physics is complete through Phase 6 (`BallSimulation` + `ShotInputBuilder` + stat resolver). The gameplay input layer is the missing piece: nothing currently converts a player's touch input into the `(ShotInput, BallPhysicsModifiers)` tuple that `BallSimulation.Simulate()` consumes.

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

- ✅ **2026-04-23** Phase 7 Shot Controls v1 — (in progress, see ACTIVE TASK)

- ✅ **2026-04-22** Manual Scene Snapshot tool — 6 files + 2 asmdefs. 8/8 EditMode tests pass (1.59s). Window at `Window > Golfin > Manual Scene Snapshot`. Capture/restore of manually-placed GameObjects, terrain trees, and detail layers via stable per-prop GUIDs (`ManualPropId`). Key deviation: ManualPropId moved to `Assets/Scripts/SceneSnapshot/` (runtime asmdef) — editor-only types can't be added via `AddComponent`.

- ✅ **2026-04-22** Phase 6 Stat Coupling

```csharp
using System;
using UnityEngine;

namespace Golfin.SceneSnapshot
{
    /// <summary>
    /// Stable identifier stamped on manually-placed GameObjects by the Manual Scene
    /// Snapshot tool. The GUID is generated on first capture and persists across
    /// scene re-imports so the snapshot can find the same object on Restore.
    ///
    /// Do NOT add this manually in the inspector — it's stamped by the Capture pass.
    /// Removing it disconnects the object from snapshot tracking; the next Capture
    /// will treat it as new and assign a fresh GUID.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ManualPropId : MonoBehaviour
    {
        [SerializeField, HideInInspector] private string guid;

        public string Guid => guid;

        public void EnsureGuid()
        {
            if (string.IsNullOrEmpty(guid))
                guid = System.Guid.NewGuid().ToString("N");
        }

#if UNITY_EDITOR
        public void SetGuidEditor(string g) { guid = g; }
#endif
    }
}
```

Place under `Assets/Scripts/Editor/SceneSnapshot/` for code locality even though it's a runtime MonoBehaviour — keep the assembly definition simple, no separate runtime asmdef needed for this. If the project already has a Runtime asmdef for editor utilities, use that; otherwise put it in the default assembly.

The component is intentionally inert at runtime: no Update, no Awake side effects. It exists purely as a marker.

---

### Part B — Importer marker detection ("what counts as manually placed?")

The capture walks every root GameObject in the scene. For each, it decides: importer-generated, or manual?

**Importer-generated rules (anything matching is SKIPPED):**

1. Object name starts with one of: `Hole_`, `Generated_`, `Procedural_`, `ProcGen_`.
2. Object has a component whose namespace starts with `Golfin.Course` or `Golfin.Physics.Runtime` (e.g. legacy `SurfaceMarker`, `GreenSurfaceInfo`, `BunkerSurfaceInfo`, `HeightProvider`, generated zone-mesh wrappers).
3. Object's name matches one of these exact roots used by the current importer pipeline:
   - `Terrain` (the Unity Terrain GameObject — captured separately for trees/details, not as a GameObject)
   - `HoleGeo`, `HoleGeo_Flat`
   - `Splines` (cart path spline holders)
   - `ZoneMeshes`, `Greens`, `Bunkers`, `Water`, `CartPaths`, `Tees`, `Fairways`, `Rough`, `Semirough`
   - `LabRoot` (Physics Lab scenes)

**If none match, the object is treated as manual** — including its full child hierarchy. Capture writes the *root* manual object plus all children verbatim; Restore rebuilds the same hierarchy.

**Caveat / future-proofing:** these rules are heuristic. Add a `[SerializeField] List<string> additionalImporterRootNames` on the editor window so Cesar can extend the skip list per-project without editing code. Persist via `EditorPrefs` keyed `Golfin.SceneSnapshot.ExtraImporterRoots`.

**Audit step at start of Capture:** list every root GameObject and which bucket it landed in (manual vs importer-generated, with the rule that matched). Show this in a scrolling text area in the editor window before Capture commits, so Cesar can verify nothing important got mis-classified. Add a "Capture" button at the bottom that only enables after the audit has been run.

---

### Part C — Snapshot data model

`Assets/Scripts/Editor/SceneSnapshot/SnapshotData.cs` — new. Pure POCOs, `[Serializable]` for `JsonUtility`.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.SceneSnapshot
{
    [Serializable]
    public sealed class SceneSnapshot
    {
        public string SchemaVersion = "1";
        public string SceneName;
        public string CapturedAtIso;          // DateTime.UtcNow.ToString("o")
        public List<PropEntry>  Props  = new();
        public TerrainSnapshot  Terrain;      // null if no terrain in scene
    }

    [Serializable]
    public sealed class PropEntry
    {
        public string Guid;                   // ManualPropId.Guid
        public string Name;                   // GameObject.name (informational; Restore uses Guid)
        public string PrefabAssetGuid;        // AssetDatabase GUID of source prefab; "" if not a prefab instance
        public string ParentGuid;             // ManualPropId of parent if parent is also a manual prop; "" otherwise
        public TransformData Transform;
        public bool   ActiveSelf;
        public string TagAndLayer;            // "Untagged|0" — informational
    }

    [Serializable]
    public struct TransformData
    {
        public Vector3 Position;
        public Vector3 EulerAngles;
        public Vector3 LocalScale;
    }

    [Serializable]
    public sealed class TerrainSnapshot
    {
        public string TerrainObjectName;      // for sanity-check on restore
        public List<TreeInstanceData>   TreeInstances = new();
        public List<DetailLayerData>    DetailLayers  = new();
    }

    [Serializable]
    public struct TreeInstanceData
    {
        public Vector3 Position;              // normalized [0,1] in terrain space, matches TerrainData.treeInstances
        public float   WidthScale;
        public float   HeightScale;
        public float   Rotation;
        public Color32 Color;
        public Color32 LightmapColor;
        public int     PrototypeIndex;        // index into terrainData.treePrototypes
        public string  PrototypePrefabGuid;   // AssetDatabase GUID of treePrototypes[i].prefab — used to remap if prototype order changes
    }

    [Serializable]
    public sealed class DetailLayerData
    {
        public int    PrototypeIndex;
        public string PrototypePrefabGuid;    // for remap on restore
        public int    Width;
        public int    Height;
        public int[]  Densities;              // flattened row-major; length = Width * Height
    }
}
```

**Notes on the model:**
- `PrefabAssetGuid` lets Restore find the prefab even if its on-disk path moved. Use `AssetDatabase.AssetPathToGUID` / `GUIDToAssetPath`.
- `PrototypePrefabGuid` on tree/detail entries lets Restore remap if `terrainData.treePrototypes` reordering happened between snapshots (importer might rebuild terrain and re-add prototypes in different order).
- `ParentGuid` only populated if parent is itself a captured manual prop. If parent is the scene root or an importer-owned object, leave empty — Restore will reparent under scene root (or whatever Cesar designates as the manual-prop root).
- `Densities` is `int[]` (Unity's GetDetailLayer returns int[,]; flatten to int[] for serialization, restore via `new int[w,h]` reconstruction).
- All serializable types use `JsonUtility` directly. Avoid `Newtonsoft.Json` — it's not a default Unity dep and Cesar doesn't have it in the project (verified via `Packages/manifest.json` — Code: confirm during impl, add only if absolutely needed).

---

### Part D — Capture pass

`Assets/Scripts/Editor/SceneSnapshot/SceneSnapshotCapture.cs` — new editor static class.

```csharp
public static SceneSnapshot Capture(Scene scene, IReadOnlyList<string> extraImporterRoots);
public static void          Save(SceneSnapshot snap, string scenePath);  // writes <scenePath without .unity>.manual.json
```

Algorithm:

1. Iterate `scene.GetRootGameObjects()`.
2. For each root, run the importer-marker check (Part B). If importer → skip. If manual → recurse.
3. For each manual GameObject (root or descendant):
   - Get-or-add `ManualPropId`. Call `EnsureGuid()`. Mark scene dirty if a GUID was newly assigned.
   - Build a `PropEntry`. `PrefabAssetGuid` via `PrefabUtility.GetCorrespondingObjectFromOriginalSource(go)` → `AssetDatabase.GetAssetPath(prefab)` → `AssetDatabase.AssetPathToGUID(path)`.
   - `ParentGuid`: if parent has a `ManualPropId`, write its GUID; else "".
   - Position/euler/scale from `transform.localPosition` / `localEulerAngles` / `localScale` if parent is a manual prop, else world (`position` / `eulerAngles` / `lossyScale` — but `lossyScale` is read-only at restore, so for root-level objects use `localScale` and accept world-position drift if parented under different roots between snapshots; document this in the editor window's help text).
4. Find any `Terrain` component in the scene. If found:
   - For each `TreeInstance` in `terrainData.treeInstances`, build a `TreeInstanceData`. Resolve `PrototypePrefabGuid` from `terrainData.treePrototypes[instance.prototypeIndex].prefab`.
   - For each detail prototype `i` in `[0, terrainData.detailPrototypes.Length)`, call `terrainData.GetDetailLayer(0, 0, w, h, i)`, flatten to int[], build a `DetailLayerData`.
5. Set `CapturedAtIso = DateTime.UtcNow.ToString("o")`.
6. Save as JSON next to scene file.

**Dirtying rules:**
- After capture, the scene may have new `ManualPropId` components (and new GUIDs on existing ones). Call `EditorSceneManager.MarkSceneDirty(scene)` if any GUIDs were assigned. Do NOT auto-save the scene — Cesar saves manually so he can review.
- Use `Undo.RegisterCompleteObjectUndo(go, "Stamp ManualPropId")` for each new component so Cesar can Ctrl-Z if Capture mis-stamped something.

**Safety checks before save:**
- If the snapshot file already exists, write to `<name>.manual.json.bak` first as a backup, then overwrite the main file. One backup, no rotation — simple.
- If `Props` is empty AND `Terrain` is null AND a previous snapshot existed with non-empty data, abort with a confirmation dialog ("Capture would erase your existing snapshot — proceed?"). Prevents accidental wipes.

---

### Part E — Restore pass

`Assets/Scripts/Editor/SceneSnapshot/SceneSnapshotRestore.cs` — new editor static class.

```csharp
public static SceneSnapshot Load(string scenePath);   // reads <scenePath>.manual.json or returns null
public static RestoreReport Restore(Scene scene, SceneSnapshot snap);
```

`RestoreReport` is a struct with `Updated`, `Created`, `Skipped`, `Failed` lists (each: prop GUID + name + reason) — shown in the editor window after Restore.

Algorithm:

1. Build a dictionary of existing `ManualPropId`-bearing GameObjects in the scene, keyed by GUID.
2. **Pass 1: GameObjects.** For each `PropEntry` in `snap.Props`:
   - If GUID exists in scene → update transform + active state. Don't reparent (Cesar may have moved it intentionally). Don't replace components. Add to `Updated`.
   - If GUID not in scene:
     - Resolve prefab via `AssetDatabase.GUIDToAssetPath(PrefabAssetGuid)` → `AssetDatabase.LoadAssetAtPath<GameObject>(...)`.
     - If prefab found: `PrefabUtility.InstantiatePrefab(prefab, scene)` (Editor instantiation, not runtime). Stamp GUID via `SetGuidEditor(...)`. Set transform. Reparent under `ParentGuid`'s GameObject if that exists, else under scene root.
     - If prefab not found AND `PrefabAssetGuid` is empty: cannot restore (was never a prefab instance). Add to `Failed` with reason "non-prefab original; cannot recreate".
     - If prefab not found AND `PrefabAssetGuid` is set: prefab was deleted from project. Add to `Failed` with reason "prefab asset missing: {guid}".
     - Wrap creation in `Undo.RegisterCreatedObjectUndo`.
   - Add to `Created`.
3. **Pass 2: Terrain.** If `snap.Terrain` is non-null and a `Terrain` component exists:
   - **Trees:** rebuild `treeInstances` array. For each `TreeInstanceData`, resolve prototype index by matching `PrototypePrefabGuid` against current `terrainData.treePrototypes[i].prefab`'s asset GUID. If no match, skip with warning. Assign array via `terrainData.SetTreeInstances(arr, snapHeights: true)`.
   - **Details:** for each `DetailLayerData`, resolve prototype index by `PrototypePrefabGuid`. Reconstruct int[w,h] from flat array. Assign via `terrainData.SetDetailLayer(0, 0, idx, layer)`.
4. Mark scene dirty. Print `RestoreReport` summary to console + window.
5. Skipped: any GameObject in the scene with a `ManualPropId` GUID that doesn't appear in the snapshot — leave alone, log to `Skipped` so Cesar can audit.

**Error handling:**
- Missing snapshot file: show dialog "No snapshot found at {path}", offer Capture.
- Corrupt JSON: catch JsonUtility exceptions, show full error in window, abort.
- Schema version mismatch: if `snap.SchemaVersion != "1"`, refuse and tell the user to update the tool. Forward-compat hook for later.

---

### Part F — Editor window

`Assets/Scripts/Editor/SceneSnapshot/ManualSceneSnapshotWindow.cs` — new `EditorWindow`.

Menu: `Window > Golfin > Manual Scene Snapshot`.

Layout (top-to-bottom):
- **Active scene** label (read-only) — shows current scene path.
- **Snapshot file path** label — shows where the JSON would be written. Greyed if scene is unsaved.
- **Extra importer root names** — `ReorderableList` of strings, persisted to EditorPrefs. Cesar can add `MyCustomImporterRoot` here to extend the skip list.
- **Audit button** — runs the importer-marker check, populates a scrollable text area below with the per-root classification.
- **Capture button** — disabled until Audit has been run for the current scene. On click: runs `SceneSnapshotCapture.Capture` + `Save`, shows result count.
- Separator.
- **Snapshot summary** — if a snapshot file exists at the expected path, show: capture date, prop count, terrain trees count, detail layer count.
- **Restore button** — disabled if no snapshot file exists. On click: runs `Restore`, shows `RestoreReport` in a scrollable area below.
- **Help foldout** at bottom — short text explaining the workflow ("Capture before regenerating; Restore after"). Reference this TellCode entry implicitly: the workflow is documented in the docstrings of the static classes, not in the window.

Refresh the active-scene label on `EditorSceneManager.sceneOpened` and `sceneSaved`.

Don't make this fancy. It's a tool, not a product surface. IMGUI is fine — no UI Toolkit.

---

### Part G — Tests

`Assets/Scripts/Editor/SceneSnapshot/Tests/SceneSnapshotTests.cs` — new EditMode tests. Namespace `Golfin.SceneSnapshot.Tests`.

1. **`Snapshot_Capture_EmptySceneProducesEmptySnapshot`** — new in-memory scene with no roots. Capture. Assert `Props.Count == 0`, `Terrain == null`.
2. **`Snapshot_Capture_StampsGuidsOnManualProps`** — scene with two cube primitives parented to a "ManualRoot" empty. Capture. Assert both cubes received `ManualPropId` components with non-empty distinct GUIDs. Assert scene is dirty after capture.
3. **`Snapshot_Capture_SkipsImporterRoots`** — scene with one root named `Hole_01_Geo` and one root named `MyBridge`. Capture. Assert only `MyBridge` (and any descendants) appear in `Props`; the importer root is fully ignored.
4. **`Snapshot_Restore_UpdatesExistingPropTransform`** — capture a scene with a cube at (0,0,0). Move the cube to (10,0,0) in the scene. Restore. Assert cube returned to (0,0,0). Assert no new instances created (`RestoreReport.Created.Count == 0`, `Updated.Count == 1`).
5. **`Snapshot_Restore_AddsMissingPropFromPrefab`** — capture a scene with a prefab instance. Delete the instance from the scene. Restore. Assert one prefab was instantiated, GUID matches the snapshot, transform matches.
6. **`Snapshot_Restore_LeavesNewObjectsAlone`** — capture, then add a brand-new cube to the scene (no `ManualPropId`). Restore. Assert the new cube is still present untouched.
7. **`Snapshot_RoundTrip_JsonReadable`** — capture a 3-prop scene, serialize to a string, deserialize, compare prop count + GUIDs. Sanity check `JsonUtility` round-trips the structure correctly.
8. **`Snapshot_Terrain_TreeInstancesRoundTrip`** — synthetic terrain with two tree prototypes, three painted instances. Capture. Clear `treeInstances`. Restore. Assert three instances back, positions match within 1e-5, prototype indices remapped correctly.

If a test requires a scene on disk (Restore loads a JSON file), use `Path.GetTempFileName()` + cleanup in `[TearDown]`.

All tests should run in EditMode and complete in under 5 seconds total.

---

### Part H — Validation steps

1. Compile clean. Check `console-get-logs` after each new file added.
2. Run all EditMode tests under `Golfin.SceneSnapshot.Tests`. All 8 pass.
3. Open `Assets/Scenes/Generated/Hole_01_Geo.unity` (or whichever Hole 1 scene is current — verify path during impl). Open the snapshot window. Run Audit. Report which roots were classified manual vs importer-generated.
4. If Hole 1 has any manual props placed by Cesar: Capture, then verify the JSON file appears next to the scene with non-empty Props.
5. If no manual props exist on Hole 1 yet: drop a primitive cube under a new "ManualProps" root, Capture, delete the cube, Restore, verify cube returns at the original transform.
6. Optional terrain test if Hole 1's terrain has painted trees: capture, clear `treeInstances` via a one-line script, restore, verify trees return.

### Done report

- 8-test pass/fail summary.
- Hole 1 root-classification audit (full list: which roots → manual, which → importer, which rule matched).
- Whether the Capture/Restore round-trip worked on Hole 1 (with the test cube or with real props if any existed).
- The final JSON snapshot file path written.
- A sample of the generated JSON (first ~30 lines) so Cesar can eyeball the schema.
- Any importer roots discovered on Hole 1 that aren't in the Part B skip list — recommend adding to the static list or via the EditorPrefs extension.
- Any limitations hit (e.g. `lossyScale` round-trip drift if it came up).

✅ DONE: 2026-04-22 — All 6 files + 2 asmdefs created. 8/8 EditMode tests pass (1.59s). Window at `Window > Golfin > Manual Scene Snapshot`. Key deviation: ManualPropId moved to `Assets/Scripts/SceneSnapshot/` (runtime asmdef) — editor-only types can't be added via `AddComponent`. See `AI_CONTEXT.md` session notes.

### DO NOT

- Auto-snapshot on scene save or auto-restore on scene open. Buttons only.
- Snapshot lighting/skybox/post-processing/scene settings.
- Snapshot terrain heightmap data (importer regenerates).
- Modify `HoleGeoImporter.cs`, `HoleLiteImporter.cs`, or any importer pipeline file.
- Use `Newtonsoft.Json` — `JsonUtility` covers everything in `SnapshotData`.
- Add a "Capture All Scenes" pass yet — per-scene only.
- Replace existing `ManualPropId` GUIDs on Capture if one already exists. Idempotent — Capture must be safe to run repeatedly without churning GUIDs.
- Reparent existing matched objects on Restore. Transform + active state only.
- Auto-save the scene after Capture or Restore. Mark dirty; Cesar saves manually.

### Iteration budget

- 3 iterations on the importer-marker rules in Part B if the audit on Hole 1 mis-classifies. Beyond 3, report the mis-classified roots and we'll add them as named exceptions rather than expanding the heuristic.
- 2 iterations on the terrain detail layer round-trip if `GetDetailLayer` / `SetDetailLayer` indices give trouble — Unity's terrain API is finicky about prototype ordering.

---

## History Log (completed tasks, most recent first)

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
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
