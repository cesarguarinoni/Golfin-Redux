# TellCode History — completed task archive

> Archive of completed/superseded TellCode task blocks and the long History Log. Kept here so `Docs/TellCode.md` stays small and focused on the active phase.
>
> **Index** (most recent first):
>
> 1. **Phase 8 Shot UI Polish — CLOSED 2026-05-01** ⭐ NEW
> 2. Phase 7 Part F-Hotfix — Ball placement robustness + automated tests (DONE 2026-04-24)
> 3. Phase 7 Part F — Putt mode + debug toggles + ball placement (DONE 2026-04-24)
> 4. PhysicsLab migrate to scaffold + multi-hole picker (DONE 2026-04-24)
> 5. Phase 7 Part E — PhysicsLab_Hole1 integration (DONE 2026-04-23)
> 6. Phase 7 Parts A–D — Shot Controls v1 input layer + cone UI (DONE 2026-04-23)
> 7. Bulletproof terrain B/B'/B'2 diagnostic chain (SUPERSEDED 2026-04-25)
> 8. Bulletproof terrain (SUPERSEDED 2026-04-24)
> 9. Ball-through-green diagnosis (SUPERSEDED 2026-04-25)
> 10. ARCHITECTURAL PIVOT to baked-data sim (DONE merged 2026-04-25)
> 11. Real-conditions terrain fall-through fix (SUPERSEDED 2026-04-25)
> 12. History Log — one-line summaries of all completed tasks (most recent first)
>
> Anything load-bearing for current work stays in `Docs/TellCode.md`. If you need detail on something old, it's here.

---

## ✅ DONE — Phase 8 Shot UI Polish — CLOSED 2026-05-01

Umbrella spec: `Docs/Specs/Completed/PHASE_8_SHOT_UI_POLISH.md` (with closing notes).

**What shipped (parts in execution order):**

- **8.1 — Cone restyle** (DONE 2026-04-27). `ConeBandPalette`, `ConeMeshGraphic` (filled grey triangle + 3 horizontal band-line quads), `TimingSlabGraphic` (trapezoidal slab travelling up the cone), `ShotConeView.SetupSlab/UpdateSlab`. Verified visually against `Docs/Reference/In-game UI/Timing Arrows.png`.
- **8.2 — Power gauge widget** (DONE 2026-04-27). `PowerGaugeGraphic` (procedural arc ring with vertex-color gradient: green→yellow→red→maroon-overpower), `PowerGaugeWidget` coordinator, `Indicator - Power.png` background, pct + yards TMP children. 11 in-game UI PNG TextureType fixes (Default → Sprite). Post-ack bug fixes: handle/cone height sync, cone base-Y alignment.
- **8.2.5 — Club Handle sprite swap + scale-with-pull** (DONE 2026-04-27). `ClubSelectionBroadcast` static event bus (avoids circular asmdef dep), `ClubHandleSpriteBinder` (caches 4 GOLFIN sprites), `PhysicsLabController.OnClubChanged`, `ShotConeView.UpdateClubHandle` localScale lerp 1.0→1.3 with PowerNormalized, `ClubHandleDragger._coneGraphic` live-read (no more hardcoded 600px).
- **8.3 — Player card + Hole card + Settings icon** (DONE redo 2026-04-28; rejected attempt 1 + redo blocks archived in spec folder). Established the **PlayerContext + PlayerContextPopulator pattern** (static bus in `Golfin.Gameplay.UI.HUD` namespace + populator in `Assembly-CSharp` side that reads `CharacterManager` + `CharacterDatabaseCSV` and writes to the static bus). Three top-of-screen widgets: PlayerCard (left), HoleCard (right), SettingsButton (top-right corner). Per-task folder: `Docs/Specs/Completed/8_3_topbar/`.
- **8.4 — Wind + Hole indicators** (DONE 2026-04-29). `WindIndicator` (top-left, second row) + `HoleIndicator` (top-right, sliding chip + fading tail). Per-hole wind data via new `windSpeedMph`, `windDirectionDegrees` columns in `Assets/Data/HoleDatabase.csv`. Pin position sourced via prefix match on `Flag_1` GO in `PhysicsLabController.OnHoleLoaded`. Three rounds total in the multi-agent pipeline (v1 had 6 FAILs, v2 closed 4, v3 fixed chip-slide hierarchy + always-visible distance-scaled tail). Ball multiplication bug also fixed (BallAnimator parents to transform + OnDestroy cleans up). Per-task folder: `Docs/Specs/Completed/8_4_indicators/`.
- **8.5 — Action button row** (expanded into A/B/C/D sub-tasks 2026-04-30):
  - **8.5.A** — CSV consolidation (clubs.csv + balls.csv → unified inventory schema). Per-task folder: `Docs/Specs/Completed/8_5_a_csv_consolidation/`.
  - **8.5.B** — Lab inventory seeder (default Driver/PuttAce in lab; populated `BagManager`/`BallManager` static busses). Per-task folder: `Docs/Specs/Completed/8_5_b_lab_inventory_seeder/`.
  - **8.5.C** — Selector redesign (hold-to-slide + tap-to-modal). Replaced original 8.6 scope. Per-task folder: `Docs/Specs/Completed/8_5_c_selector_redesign/`.
  - **8.5.D** — Central ball sprite + always-on TargetingLine. Replaced original 8.7 scope. `ShotController._aimYawRadians` now computed continuously in `PublishState` (was only at fire time). Per-task folder: `Docs/Specs/Completed/8_5_d_central_ball_targeting_line/`.
  - 2×2 SPIN/FADE-DRAW/GOLFIN/DRIVER cluster + SpinPanel from the original 8.5 spec landed in `Docs/Specs/Completed/8_5_action_buttons/`.
- **8.6 (ball+club selectors) — DELIVERED as 8.5.C.** Selector overlay shipped against Figma frame `12942:1079`.
- **8.7 (centerpiece ball + trail) — DELIVERED as 8.5.D.** Central ball UI sprite + always-on targeting line shipped against Figma frame `12941:7178`.
- **8.8 (polish/tests/smoke) — SKIPPED.** Polish folded into Loop v1 (next roadmap item, where putter HUD will exercise the same elements in real gameplay context). The 6 unit tests listed in 8.8 were nice-to-have but not load-bearing — Phase 7 already covers state machine behavior with 83 tests. The full-screen pixel-diff integration check is rolled into Putter P1 lab UI work.

**Adjacent housekeeping closed in the same window:**
- BallSimulation A3 plumbing cleanup (2026-04-27) — deleted `DiagPerStepSink`/`DiagPerStepEnabled`/`DiagStepFrame` fields + their two consumer blocks in `RunRollPhase`/`RunPuttPhase`. 198/198 PASS. Commit `238a8f67`.
- Texture Experiment Phases 1+2 (2026-04-27 → 2026-04-28) — 25 CC0 textures, 9 TerrainLayers + 7 overlay materials duplicated for `Hole_01_Experimental_Geo.unity`. Closing verdict: net positive but not promotion-ready; pure source-substitution hitting diminishing returns; next big jump is shader work. Findings + ranked future plans: `Docs/Specs/Queued/TEXTURE_EXPERIMENT_FINDINGS_AND_PLAN.md`. One immediate standalone candidate: bunker sand swap (specced in findings doc).
- capture_helper tooling (DONE 2026-04-29) — `Assets/Scripts/Editor/CaptureHelper.cs`: synchronous GameView capture via RT reflection (`GrabGameViewRT()`), Y-flip, 4 fake-state presets (Reset/MidAim/Putt/StrongWind), all 8 HUD contexts wired with real sprites. `GOLFIN > Capture` menu live. Known follow-on: `fake_state_populator_gate` — PlayerContextPopulator in LabScaffold overrides fake player name; needs a FakeStateGate flag across runtime populators.

**Pipeline lessons filed during Phase 8:**
- Anchor convention mismatch is a silent failure mode — if widget code computes canvas-space-from-left X but the RectTransform is right-anchored with right-pivot, math goes the wrong direction. Always verify anchor/pivot in the builder when reading widget coordinate assumptions.
- Self-reviewer marking behavioral items "unverifiable in static screenshot" without re-running playmode lets visible bugs through. Specs that change behavior need an explicit "rebuild scene + take fresh playmode screenshot + verify visually" gate.
- Asset-side fixes beat code-side compensations. Upside-down tail PNG was a 1-second asset fix from Cesar; proposed `localScale.y = -1` compensation would have left a confusing artifact for whoever inherits the code.
- Static-context-bus + populator + widget pattern now has 6 instances (PlayerContext, HoleContext, GameSession, ClubContext, BallContext, plus the ShotInputState publisher). Skill candidate flagged for the 7th instance (memory #23).

**Multi-agent pipeline run rate during Phase 8:** four full chains (8.4, 8.5.A, 8.5.B, 8.5.C, 8.5.D) — each going implementer → self-reviewer → architect, with architect-driven redos per round where needed. Pipeline policy locked 2026-04-30 (memory #23): architect-as-final-reviewer stays, per-role models already optimal, contract-style lighter specs to be tested cautiously on next small task, fan-out candidates flagged for Loop v1 ball state machine.

---

## ✅ DONE — Part F Hotfix: Ball placement robustness + automated test coverage — 2026-04-24

### Background

Part F shipped the placement dropdown but it's broken. Three real bugs (plus two red herrings Code chased). Revert the band-aid fixes, apply root-cause fixes, and add automated regression tests so this never regresses silently again.

### Diagnosis (authoritative — do not re-diagnose)

**Bug 1 — Green intermittent sub-surface placement.** `Fairway` GOs have a MeshCollider covering both the fairway material AND the fringe submesh (see `HoleGeoImporter.cs:4370–4378`). The fringe extends over the green's outer edge. At some green XZ points, the downward raycast from Y=500 hits the fairway+fringe MeshCollider before the green MeshCollider, or vice-versa, depending on vertex-level Y differences between the two meshes at that exact XZ. First hit wins → ball placed on whichever happened to be higher. When that's NOT the green, ball ends up at fringe-Y. Then on the next shot, sim classifies via `SceneSurfaceProvider` at ball XZ, may hit green this time, but the stored ball Y is fringe-Y which is sometimes below green-Y → ball appears to start under the visible green surface. Fully intermittent, fully consistent with the fringe-vs-green collider race.

**Bug 2 — Bunker "through terrain" is NOT a bug.** Measured data: `Bunker GO.y=10.117 snapY=8.709 diff=-1.408`. SnapY IS the bunker floor. Ball is placed correctly at bunker floor. It LOOKS "through terrain" because the surrounding terrain rim (~Y=10) occludes a ground-level chase camera view of a ball at Y=8.7. This is a camera artifact, not a placement bug. Do NOT "fix" ball Y for bunker placement. See F-Hotfix.C for the actual camera fix.

**Bug 3 — `PlacementEntries.Count = 0` mid-session.** Code's diagnosis is correct on the symptom (scene event race) but the two-event fix (adding `SceneManager.sceneLoaded`) is still fragile. Proper fix: coroutine scan on frame 2 of `PhysicsLabController.Start()`. See F-Hotfix.A.

**Bug 4 — `_useSceneProviders = False` despite hole loaded.** Same root cause as Bug 3 (event race). Fixed by the same coroutine.

**Red herring 1 — 3 stale ball clones + `_instance = null`.** Unity domain reload artifact. Not a production bug. Leave alone.

**Red herring 2 — "Heightmap doesn't include zone-mesh tops" open flag.** NOT the cause. The scaffold uses `SceneGroundProvider`, which is a live raycast — it never reads `heightmap.bytes`. The existing heightmap open flag is unrelated to this bug. Leave the flag in place for future baker work but do NOT try to fix it here.

### F-Hotfix.A — Replace fragile event binding with coroutine scan

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

Add a coroutine kicked off in `Start()`:

```csharp
IEnumerator ScanForLoadedHoleSceneAtStartup()
{
    // Wait 2 frames so any additive hole scene has finished loading.
    yield return null;
    yield return null;

    for (int i = 0; i < SceneManager.sceneCount; i++)
    {
        var scene = SceneManager.GetSceneAt(i);
        if (!scene.isLoaded) continue;
        if (scene.name.StartsWith("Hole_") && scene.name.EndsWith("_Geo"))
        {
            Debug.Log($"[PhysicsLab] Coroutine detected loaded hole scene: {scene.name}");
            OnHoleLoaded(scene.name);
            yield break;
        }
    }
    Debug.Log("[PhysicsLab] No hole scene loaded at startup — flat-ground fallback.");
}
```

**File:** `Assets/Scripts/Physics/Viewer/LabHoleBinder.cs`

- REMOVE the `SceneManager.sceneLoaded` subscription Code added in the last pass. Revert to `EditorSceneManager.sceneOpened` / `sceneClosed` only, wrapped in `#if UNITY_EDITOR`.
- These events now serve only ONE purpose: handling edit-time picker interactions (user loads/unloads a hole via `PhysicsLabHolePicker`). Play-mode startup is handled by the coroutine in A.
- `sceneClosed` should only call `OnHoleUnloaded` if the closed scene's name starts with `Hole_` AND ends with `_Geo`. Ignore all other scene close events. This prevents spurious unloads during Unity's play-mode scene reload sequence.

### F-Hotfix.B — Revert pre-snap-at-build-time hack, fix SurfaceSnap properly

**File:** `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`

Revert the pre-snap loop Code added to `BuildPlacementEntries`. Y should be resolved at *placement time* via `SurfaceSnap`, not at build time. The stored entry Y is an approximation; the raycast is the truth.

Replace the existing `SurfaceSnap(x, z, defaultY)` helper with a *type-aware* version. (See historical implementation in commit `c340e718`.)

**Call-site changes** — `PlaceBallAt(Vector3 worldPos, Course.SurfaceType? preferredType = null)`:

- Dropdown entries pass the expected `preferredType` when they have one: `Course.SurfaceType.Green` for green entries, `Bunker` for bunker, `Fairway` for fairway. Tee entries pass `Tee`. Water entries (offset onto grass) pass `null` — let first-hit win.
- `SetupAtTee()` calls `PlaceBallAt(teeMidpoint, Course.SurfaceType.Tee)`.
- `ResetToTee` / "Reset to Tee" button: `PlaceBallAt(teeMidpoint, Course.SurfaceType.Tee)`.

This fixes Bug 1 because green entries now prefer the green MeshCollider over the fringe-overlap in the fairway collider.

### F-Hotfix.C — Bunker camera (NOT placement)

Ball in bunker at floor Y is correct. Problem is chase camera at ground-level Y gets occluded by bunker rim. **Pick option 1 — automatic camera lift when ball is in depression.**

Implementation: new method `PhysicsLabController.AdjustCameraForDepression(Vector3 ballPos)` called at the end of `PlaceBallAt`. Raycasts at 4 points around the ball (±2m X, ±2m Z), finds max surrounding Y, compares to ball Y, if diff > 0.5 offsets the chase camera's follow-offset Y by the diff. Clamp offset at 3m so it doesn't go absurd.

### F-Hotfix.D — Automated regression tests

12 tests across `PlacementSnapTests.cs`, `PlacementEntriesTests.cs`, and `BallPlacementIntegrationTests.cs`. All tests must pass before closing F-Hotfix.

✅ DONE: 2026-04-24 — All 12 regression tests pass (PlacementSnapTests 6/6, PlacementEntriesTests 3/3, BallPlacementIntegrationTests 3/3). Fixed BallAnimator.DestroyInstance to use DestroyImmediate in editor. Committed and pushed.

### DO NOT (recap)

- Do NOT "fix" the bunker ball Y. It's already correct.
- Do NOT touch `HoleGeoImporter.cs`.
- Do NOT modify `SceneGroundProvider.cs`.
- Do NOT re-enable `SceneManager.sceneLoaded` subscription. Coroutine scan replaces it.
- Do NOT skip tests.

---

## ✅ DONE — Phase 7 Part F: Putt mode + debug toggles + ball placement — 2026-04-24

> Status: F.1–F.4 and F.6 shipped; F.5 had bugs handled in F-Hotfix above.

### Background

Phase 7 Parts A–E landed the swing loop. Part F closes out Phase 7 by adding: (a) putt-mode flag on `ShotController`, (b) 8 debug toggles per design §8, (c) ball-placement dropdown in the lab.

Gameplay rule (Cesar, 2026-04-24): putter is selected by the player and is only valid on the green. Driver/iron/wedge are never valid on the green. Auto-detection is NOT required — club selection drives `IsPutt`.

Architectural note: keep all putt-specific logic behind a single `if (IsPutt)` guard per behavior, with no scattered conditionals, so a future `PuttController` split is a move operation, not a rewrite.

### F.1 — Putt mode flag on ShotController

Add public property `bool IsPutt { get; set; }`. Default `false`. Settable externally; no internal auto-flip. Putt-mode effects, each gated by a single `if (IsPutt)` guard:
- Power clamp at 1.0
- Spin override to None
- Shot mode forced Straight
- Base velocity uses `controlsCfg.PuttBaseVelocityMps`
- Arrow speed multiplied by `controlsCfg.PuttArrowSpeedMultiplier`
- Per-pass degradation skipped

### F.2 — Lab club selector (manual)

Dropdown "Club" with two entries: `Driver` (default), `Putter`. On change: set `_shotController.IsPutt`, swap injected StatBundle, recompute max-carry.

### F.3 — Debug toggles

8 bool fields in new `ShotDebugFlags.cs` struct: ShowConeOutline, ShowArrowTrail, CancelOnSlowFlick, SinglePassMode, DisableOverpower, DisableConeFineTune, ForcePerfectTiming, ForcePerfectAim. Each guards a code path. Collapsible "Debug Flags" foldout in PhysicsLabUI.

### F.4 — Putt camera mode

In `SetupAtTee()` and any other ball-placement entry point, if `_shotController.IsPutt == true`, call `chaseCamera.SetMode(ChaseCamera.Mode.Ground)` (or whatever the ground-level enum value is).

### F.5 — Ball placement dropdown (handled in F-Hotfix)

Populate "Place Ball" dropdown from runtime scan of loaded hole. Tee/Green/Bunker/Fairway/Water entries. Y resolved at placement time via raycast. (See F-Hotfix above for the production-quality version.)

### F.6 — Tests

7 putt-mode tests + 1 test per debug flag + bit-exact gate.

✅ DONE: 2026-04-24 — Phase 7 Part F complete. ShotDebugFlags struct (8 flags), ShotController putt guards, ShotInputBuilder baseVelocityOverrideMps param, ShotConeView debug flag support, PhysicsLabController PlaceBallAt + placement scan + putt camera (GroundLevel). 14 new tests + 1 stale ViewerTests count fixed. 83/83 pass. Deviation: ChaseCamera.Mode.GroundLevel used (spec said Ground).

### DO NOT (recap)

- Don't split `ShotController` into a `PuttController` yet.
- Don't add auto-detection of putt mode.
- Don't redesign the cone visual for putts yet.
- Don't redesign the Spin modal.
- Don't add flag persistence across sessions.
- Don't touch `BallSimulation`.

---

## ✅ DONE — PhysicsLab: migrate to scaffold + multi-hole picker — 2026-04-24

**Status:** Validated by Cesar. Cleanup pending (Cesar to run): delete `Assets/Scenes/Physics/PhysicsLab_Hole1.unity` + `.meta` and `Assets/Scripts/Editor/Physics/PhysicsLabZoneMeshBaker.cs` + `.meta`.

### Architecture

- `LabScaffold.unity` (new, git-tracked) — LabRoot, ShotController, ShotUI_Canvas + cone hierarchy, ChaseCamera + Main Camera, BallAnimator, PhysicsLabController, PhysicsLabUI, InputSystemSource, TrajectoryRenderer. **No ground, no zones, no hole-specific refs.**
- `PhysicsLabHolePicker.cs` (new editor window) — lists all `Hole_XX_Geo.unity` under `Assets/Golf/Courses/lomond-country-club/Generated/`, "Load Hole N" button opens the selected hole additively atop `LabScaffold.unity`, "Unload" button closes it.
- **Auto tee anchor** — `PhysicsLabController.SetupAtTee()` locates a GO with `Course.SurfaceMarker` `surfaceType == Tee` via reflection.
- **Providers stay as they are.** `SceneGroundProvider` / `SceneSurfaceProvider` already raycast against whatever colliders are in the currently-loaded scenes.

✅ CODE COMPLETE: 2026-04-24 — LabScaffold.unity created + picker + binder written. Compile-verified. Awaiting Cesar validation steps 1–4 (open scaffold, load hole, confirm tee spawn + trajectory, unload). Step 5 cleanup (delete PhysicsLab_Hole1.unity + ZoneMeshBaker.cs) blocked on Cesar confirmation.

✅ SESSION COMPLETE: 2026-04-24 — PhysicsLab polish pass done.
- Tee spawn: fixed to use midpoint of TeeMarker_regular_* GOs (not SurfaceMarker tee zones).
- Lie continuation: ball fires from current lie after each shot without forced Reset.
- Club selection: InjectStatBundle() now called on preset change; PRESET picker drives club stats.
- Scene persistence: [InitializeOnLoad] + sceneOpened + delayCall auto-restores last hole when switching scenes.
- NullRef in ComputeMaxCarryYards: fixed with _configsLoaded bool + EnsureConfigsLoaded().
- Water gray: CopyHoleLighting() snapshots all RenderSettings from hole scene and writes them into LabScaffold.
- Golfin.Physics.Stats added to Viewer asmdef references.

### DO NOT (recap)

- Don't touch `HoleGeoImporter.cs`.
- Don't modify any `Hole_XX_Geo.unity` file directly.
- Don't add `Assembly-CSharp` to `Golfin.Physics.Viewer` asmdef. Use reflection.

---

## ✅ DONE — Phase 7 Part E: PhysicsLab_Hole1 integration — 2026-04-23

### Status

Parts A–D complete. State machine works, input fires, cone renders. Part E wires it into `PhysicsLab_Hole1` so Cesar can play a real shot.

### Pre-flight (resolved during impl)

- E.0.a — `heightmap.bytes` rebake: SceneGroundProvider fallback chosen (raycasts into Hole1 zone meshes; heightmap.bytes not required for runtime sim path in `PhysicsLabController.BuildGroundProvider()`).
- E.0.b — Dead `HeightProvider` field cleanup: confirmed absent from PhysicsLabController.cs.
- E.0.c — Yaw convention check on `ShotConeView.UpdateTargetingLine`: fixed to `(Mathf.Cos(yaw), 0, Mathf.Sin(yaw))`.

### Files modified

- `PhysicsLabController.cs` — HandleShotResolved, RunSimFromController, ComputeMaxCarryYards, Awake/OnDestroy wiring.
- `ShotConeView.cs` — null guards + yaw fix.
- `ConeAlphaController.cs` — null guards.
- `Golfin.Physics.Viewer.asmdef` — +Golfin.Gameplay.Input, +Golfin.Gameplay.UI refs.
- `PhysicsLab_Hole1.unity` — LabRoot gained InputSystemSource + ShotController. ShotUI_Canvas → ConeRoot → ConeMesh + ClubHandle + Arrow0-2 + PowerHUD + TargetingLine. All refs verified via script-execute.

✅ DONE: 2026-04-23 — Compile clean, all refs wired, scene saved. Cesar smoke test pending — Part E is code-complete. ComputeMaxCarryYards() simulates DefaultDriver (75 m/s, 10.9°) with FlatGround + WindConfig.Calm.

### DO NOT (recap)

- Don't re-spec Parts A–D.
- Don't add per-rarity clubs.
- Don't modify physics core.
- Don't auto-save scenes other than `PhysicsLab_Hole1`.

---

## ✅ DONE — Phase 7 Parts A–D: Shot Controls v1 (input layer + cone UI) — 2026-04-23

### Scope

Flick-based shot control system — screen-anchored semi-cone UI that the player drags down (power) and flicks up through (commit), with timing arrows and aim-fine-tune via the club's lateral position inside the cone.

**Authoritative design doc:** `Docs/Game Design/SHOT_CONTROLS_DESIGN.md`.

**Phasing:**
- **Part A** — Defaults + DefaultStatProvider + ClubStats/PutterStats presets + controls.csv + loader. Pure data layer.
- **Part B** — `ShotController` MonoBehaviour, state machine, synthetic input feeder, EditMode tests.
- **Part C** — `Shot.inputactions` asset + Input System wiring, mouse-as-touch verification.
- **Part D** — `ShotConeView` uGUI cone, club trapezoid, arrows, HUD, targeting line.

### Part A done report

✅ DONE: 2026-04-23 — All 8 files written, compile clean, ControlsConfig.Load() dumps all 21 fields correctly. DefaultDriver (Power=50 Acc=50 LR=50 Dur=100 Loft=10.9 Vel=75 Spin=2686) and DefaultPutter (Control=50 Acc=50 Wt=50 Dur=100 Loft=4 Vel=5) verified. BagManager confirmed in global namespace (Assembly-CSharp, no custom asmdef) — DefaultStatProvider always returns defaults. Golfin.Gameplay.Defaults.asmdef references both Golfin.Physics.Stats AND Golfin.Physics.Math (needed for fp in StatBundle constructor). Pushed to GitHub.

### Part B done report

✅ DONE: 2026-04-23 — 12/12 tests pass (Tests 1–10 implemented, including both optional). ShotController has zero direct BallSimulation references — only calls ShotInputBuilder.Build() and emits event. OnStateChanged fires every Tick (every frame). Spec deviation: Golfin.Gameplay.Input.asmdef references Golfin.Physics.Core (needed for ShotInput and BallPhysicsModifiers types in OnShotResolved event signature). Semantic seam preserved. Pushed to GitHub.

### Part C done report

✅ DONE: 2026-04-23 — Compile-clean. InputSystemSource correctly implements IShotInputSource (all 4 properties verified via reflection). Bootstrap calls EnhancedTouchSupport.Enable() + TouchSimulation.Enable(). ShotController [SerializeField] _inputSystemSource + Awake wiring confirmed. Golfin.Gameplay.Input.asmdef needed explicit Unity.InputSystem reference. Mouse-as-touch live verification pending Cesar manual Play-mode test. Pushed to GitHub.

### Part D done report

✅ DONE: 2026-04-23 — Part D complete. All 5 files created + ShotConeTest.unity verified in Play mode.
- Cone method: (b) MaskableGraphic subclass (ConeMeshGraphic) — triangle via OnPopulateMesh; width rebuilds cheaply via SetVerticesDirty on stat change.
- Screenshots: (1) Idle — cone ghosted at ~25% alpha, no arrows, targeting line visible; (2) Timing — full alpha, 3 stagger-phased yellow arrows traveling up cone, HUD "50% / 125 yd", targeting line above apex.
- Cone width is accuracy-driven: HalfAngleDeg = lerp(ConeHalfAngleAtAcc0Deg, ConeHalfAngleAtAcc100Deg, accNorm) from ControlsConfig.
- Deviations: (1) Driver drove to Timing state (not Pulling) due to ShotController transitioning immediately when PowerNormalized>0. Arrow display is in Timing, per spec arrows are a Timing visual. (2) HUD text showed in Idle (no state events fired when driver disabled). (3) DebugShotInputSource + ShotConeTestDriver added as test-only helpers in Golfin.Gameplay.Input assembly.
- 12/12 ShotController tests pass.

### DO NOT (recap)

- Modify `Assets/Scripts/Physics/Core/` or `Math/`. Contract is fixed.
- Modify `ShotInputBuilder.cs`.
- Touch `Assets/InputSystem_Actions.inputactions` (template).
- Bring in DOTween, UniTask, or any third-party tween library.
- Use UI Toolkit (UITK).
- Build per-rarity clubs.
- Make ShotController call `BallSimulation` directly.

---

## 📜 ARCHIVED — Bulletproof terrain B'/B'2 diagnostic chain — 2026-04-25

> Full diagnostic chain that led to the architectural pivot. Kept for reference; superseded by `Docs/Specs/Completed/SIM_BAKED_DATA_PATH.md`.

### Phase B' diagnostic — High-velocity LAUNCH from depressed surface

**B'1 task:** Write `HighVelocityLaunchDiagTests.cs`. 6 PlayMode shots in real Hole_01 with `BallSimulation.DiagPerStepEnabled = true`. ALL shots start AT the depressed surface (bunker or green centroid).

✅ DONE: 2026-04-25 — B'1 complete. Tests ran via MCP. Commits `6f9cad03` (test file) + analysis in `Docs/DIAG/realtest-20260425/Bprime-summary.md`.

**Results (7/7 pass):**

| shot | surface | club | diagFrames | minBallY | termination |
|---|---|---|---|---|---|
| 1 | Sand | Driver (+X) | 0 | 3.723 | HitOOB |
| 2 | Sand | Driver (+Z) | 0 | **-2301.558** | MaxDurationReached |
| 3 | Green | Driver (+X) | 0 | 4.567 | HitOOB |
| 4 | Green | Driver (180°) | 1935 | 0.000 | BallStopped |
| 5 | Sand | Wedge (+X) | 0 | 4.041 | HitOOB |
| 6 | Green | Putter (+X) | 335 | 19.215 | BallStopped |

**Definitive finding:** Shot 2 is the confirmed fall-through. `diagFrames=0` + `minBallY=-2301` + `MaxDurationReached(60s)` = the failure is 100% inside `SimulateAirborne`. Roll/putt phase never entered.

**Root cause:** `SimulateAirborne` detects landing via `ballY <= SampleHeight(x,z)`. In the +Z direction from the bunker at (-216, 8.89, -86), `SampleHeight` returns 0 (no collider coverage) at all XZ positions along the trajectory. The HitGround condition never fires. Ball free-falls under gravity for 60 seconds to Y=-2301.

### Phase B'2 (architecturally pivoted)

Architect spec'd two-part fix likely:
1. Change `SceneGroundProvider.SampleHeight` to return a sentinel (e.g. `fp.FromFloat(-1e6f)`) when zero hits.
2. Add Y-axis safety bound in `SimulateAirborne`.

**Cesar's described failure ("ball falls straight through the green/bunker") may differ from B'1 reproduction ("ball flies far away then falls into the void"). They may be different bugs.**

→ Pivoted to architectural fix (baked-data path) instead of B'2 tactical fix. See `SIM_BAKED_DATA_PATH.md` Completed spec.

---

## 📜 ARCHIVED — Bulletproof terrain (Phase 1–6 synthetic fix) — 2026-04-24

> Yesterday's task shipped 111/111 synthetic tests green and a 3500-shot stress run with zero fall-throughs. **The fix did not hold in real conditions** — Cesar's first two manual shots in Hole_01 PlayMode both fell through. Tests were synthetic, not real-scene. Superseded by the Real-conditions task above.

### Key takeaways

- The type-preference logic in `SceneGroundProvider.SampleHeight(3-arg)` is correct in isolation. The real failure is upstream (markers missing/broken/wrong-hierarchy in real scenes) or at a different sim seam.
- Cesar's Tee GO inspector showed THREE `Surface Marker` components on one GO: 2 valid + 1 with broken script reference (`Golfin.Physics.Runtime::Golfin.Physics.Runtime.SurfaceMarker` — malformed double-colon). HoleGeoImporter is producing zombie marker components.
- The migration tool only updates existing markers, doesn't create them.
- Generated scenes are gitignored.

### Phase 5 stress test (3500 SHOTS, 0 FALL-THROUGHS)

```
Surface        | Shots | Fall-throughs | Runtime
---------------+-------+---------------+--------
Green (putt)   |  1000 |       0       | 18.6s
Bunker (Sand)  |   500 |       0       | 6.9s
Green (land)   |   500 |       0       | 2.2s
Fairway        |  1000 |       0       | 10.8s
Rough          |   500 |       0       | 5.5s
TOTAL          |  3500 |       0       | 44.9s
```

### Files modified (line-count diff)

- `Assets/Scripts/Physics/Core/IGroundProvider.cs` — +14 lines
- `Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs` — +29 lines
- `Assets/Scripts/Physics/Core/BallSimulation.cs` — +33 lines
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — +3 lines
- `Assets/Scripts/Editor/SyncPhysicsSurfaceMarkers.cs` — new file (134 lines)
- 3 test files (synthetic geometry, retired in favor of real-scene tests)

### Restore points

- Tag: `terrain-fallthrough-pre-fix`
- Backup folder: `Docs/BACKUPS/terrain-fallthrough-20260424/`
- Commit: `c340e718`

---

## 📜 ARCHIVED — Ball-through-green diagnosis: uphill vs downhill — 2026-04-25

> Superseded by the Bulletproof terrain task. The hypothesis-ranking + instrumentation approach was folded into the Phase 2 attempt sequence.

---

## ✅ DONE (merged) — ARCHITECTURAL PIVOT to baked-data sim — 2026-04-25

### Result

Pivot merged to main. All tests pass (BakedPivot regression 24/24, Phase 1–6 physics, RealHoleTerrainTests). Cesar's "ball into void" repro eliminated by construction.

### Path taken

M0→M1→M2→M3.5 on `sim-baked-data-path` branch. Phase E ran 3/5 PASS → M5a diagnosed (Hypothesis A confirmed for Shot 2: same airborne edge-detector bug as Shot 4, just at a different geometric apex) → M5b applied the queued signed-distance level-detector fix (~5 lines in `SimulateAirborne`) → Phase 1–6 bit-exact gate passed → BakedPivot 24/24 with `[Ignore]` markers removed → Phase E re-ran clean → merged.

### Architecture state

Sim reads `Assets/Resources/HoleData/Hole_XX/zones.json` + `heightmap.bytes`. Scene providers (`SceneGroundProvider`, `SceneSurfaceProvider`) demoted to editor-only placement helpers. `Course.SurfaceMarker` MonoBehaviours retained as authoring source for `BakeZoneJsonTool`. `Physics.Runtime.SurfaceMarker` no longer load-bearing for sim; deletion is a future Phase F.

### Specs archived

- `Docs/Specs/Active/SIM_BAKED_DATA_PATH.md` → `Docs/Specs/Completed/SIM_BAKED_DATA_PATH.md`
- `Docs/Specs/Queued/AIRBORNE_GROUND_LEVEL_DETECTION.md` → `Docs/Specs/Completed/AIRBORNE_GROUND_LEVEL_DETECTION.md`

### Behavioral note for Code (recorded)

> "Three times in three days you've acted faster than the spec. We are out of patience for that pattern. The MILESTONE_N_DONE.md files exist precisely so you don't have to guess what's wanted next — write them honestly, mark FAIL/BLOCKED when something's wrong, and stop instead of guessing-and-shipping."

---

## 📜 ARCHIVED — Real-conditions terrain fall-through fix — 2026-04-25

> Superseded by architectural pivot. Phase A diagnostics ran (A1+A2+A4); Phase B tactical fix (PhysicsMarkerRepairTool) shipped 18/18 holes PASS but only fixed surface marker brokenness, not the fall-through itself. Phase B' diagnostic identified that the failure is 100% inside `SimulateAirborne` due to `SampleHeight` returning 0 outside collider coverage. → Pivot.

### Phase A done

✅ DONE: 2026-04-25 — Phase A diagnostics complete. A1+A2+A4 ran. Outputs in `Docs/DIAG/realtest-20260425/`. **Verdict: tactical fix viable.** PhysX is fully deterministic across cold loads (A4 bit-identical x3 cycles); the bug is Hole_01 has 21 of 30 GOs with zero valid Physics markers + 27 of 30 GOs with broken/zombie components (3 each, from a Roslyn migration that ran 3 times in Assembly-CSharp context).

### Phase B done

✅ DONE: 2026-04-25 — Phase B complete. All 18/18 holes PASS (0 broken components, all valid markers).

- B1 commits: `7bd58375` (attempt 1 — GameObjectUtility, returned 0), `b1b` (SerializedObject, blocked), `6394e674` (B1c — YAML pass 1, only removed fileID 1992067906), `6c5aeee7` (B1d — generalized to all no-guid m_Script refs; 110 zombie types removed per hole).
- B2 commits: HoleGeoImporter + HoleLiteImporter `CreateFlatContourMesh` now adds Physics marker at import time.
- B3: `SyncPhysicsSurfaceMarkers.cs` deleted; backward-compat menu alias kept in PhysicsMarkerRepairTool.
- All-holes run: 680 total changes, 18/18 PASS.

### B1 smoke test

✅ DONE: 2026-04-25 — Putt FROM green PASS. Wedge FROM bunker PASS. **Driver FROM green FAIL (sometimes), Driver FROM bunker FAIL (always).** Marker fix worked, but high-velocity LAUNCHES from depressed surfaces still fall through.

→ Phase B' (above), then architectural pivot.

---

## History Log (one-line completed task summaries, most recent first)

- 🚧 **2026-04-23** Phase 7 Shot Controls v1 — Parts A, B, C COMPLETE. Awaiting Part D (Cone UI). Part C 90-minute diagnostic detour: `HeightProvider.Awake()` LogError on missing heightmap.bytes → Unity Error Pause → all input symptoms looked like New Input System failure. Resolution: removed dead `HeightProvider` GO from scene. Lesson filed at `tasks/lessons.md`.
- ✅ **2026-04-22** Manual Scene Snapshot tool — 6 files + 2 asmdefs. 8/8 EditMode tests pass (1.59s). Window at `Window > Golfin > Manual Scene Snapshot`. Capture/restore of manually-placed GameObjects, terrain trees, and detail layers via stable per-prop GUIDs (`ManualPropId`). Key deviation: ManualPropId moved to runtime asmdef — editor-only types can't be added via `AddComponent`.
- ✅ **2026-04-22** Phase 6 Stat Coupling (Specialized Roles model, Option D) — 49/49 EditMode tests pass (2.85s). New assembly `Golfin.Physics.Stats` (`noEngineReferences: true`): `ClubStats`, `PutterStats`, `BallStats`, `CharacterStats`, `StatBundle`, `StatCoefficients` (14 coefficients), `StatCaps` (11 caps), `ResolvedShotModifiers`, `StatModifierResolver` (8-step resolver), `ShotInputBuilder` (returns `(ShotInput, BallPhysicsModifiers)` tuple). `BallPhysicsModifiers` struct in Core. `BallSimulation` Phase 6 8-arg overload; Phases 3/5 forward with Neutral for bit-exact backward compat. 10 new `StatResolverTests` including bit-exact gate. Tolerance fix: switched 6 tests from raw-unit to `ToFloat() ± 0.001f`. Lab integration deferred.
- ✅ **2026-04-22** Phase 5 Putt model — 35/35 tests pass (3.23s). `PuttConfig.cs` + `putt.csv` (Green 0.10/0.04, GreenCollar 0.14/0.05); `BallSimulation` 7-arg overload with `IsPutt` gate (speed<8m/s, angle<15°, surface∈{Green,GreenCollar,Tee}); seamless off-green transition; PhysicsTuningWindow Putt foldout with "Sim 3m putt" (v0=0.35→d≈3.1m, within [2.7,3.3]m). Bit-exact gate passes. RunRollPhase/RunPuttPhase still ~85% identical — no shared helper yet.
- ✅ **2026-04-21** Phase 4 Surface interaction (bounce + roll) — 29/29 tests pass. `HeightmapData`/`HeightmapLoader`/`HeightProvider`, `SurfaceType`/`ISurfaceProvider`/`SceneSurfaceProvider`/`SurfaceMarker`, `SurfaceConfig` + `surfaces.csv`, `TerrainHit` records + new `TerminationReason` values (`BallStopped`/`HitWater`/`MaxBouncesExceeded`), bounce loop with backspin Cr multiplier, `RunRollPhase` with speed²-based stop detection. Key fixes: `UnityEngine.Physics` namespace qualification, per-surface `SurfaceConfig.Default`, one-sided boundary differences in `SampleNormal`.
- ✅ **2026-04-21** Phase 3 Wind — `WindConfig`, `WindModel.SampleWind`, `fpMath.Sin`/`TwoPi`, wind.csv, tuning window integration, 6 tests. 21/21 tests pass. Seed determinism verified bit-exact.
- ✅ **2026-04-21** Phase 2.1 closeout — LUT-mode tests split by club class with honest per-club tolerances. Driver/Iron3 at 25%, mid-irons at 15%, wedges at 8%. 15 tests pass. Lessons filed at `Docs/Physics/LESSONS_PHYSICS_AERO.md`. Physics baseline accepted.
- ❌ **2026-04-21 REMEDIATION v3 — ARCHITECTURE ESCALATION HIT (Rung 3)** — Bearman–Harvey Cl at driver S=0.08 physically cannot produce 275 yd carry; lift barely balances gravity at launch. 1D-BH model ceiling. Not escalating to 2D LUT.
- ⚠️ **2026-04-21 REMEDIATION v2** Seed-value error, not architecture — Cl too high at low S. Driver 23.5% short residual matched ratio of seed overshoot.
- ⚠️ **2026-04-21 REMEDIATION v1** Correctly reverted `spin_drag_factor` scope creep; incorrectly reverted `spin_decay_rate` (real physics, restored in v3).
- ⚠️ **2026-04-21 PARTIAL** Phase 2.1 LUT architecture landed (CoefficientLut, CSV-driven LUTs, mode toggles); v0 tuning produced unphysical shapes.
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

- `Docs/README.md` — index map of what lives where in `Docs/`
- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/Physics/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan
- `Docs/Physics/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/Physics/LESSONS_PHYSICS_AERO.md` — aero remediation lessons
- `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md` — surface-marker / heightmap rationale
- `Docs/Architecture/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/Architecture/UI_HIERARCHY.md` — scene UI paths reference
- `Docs/Architecture/PATTERNS.md` — recurring patterns
- `Docs/Pipeline/ADD_HOLE.md` — hole addition procedure
- `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` — Phase 7 design source of truth
- `CLAUDE.md` — Claude Code session rules
