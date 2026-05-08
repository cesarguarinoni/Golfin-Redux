# [TellCode.md](http://TellCode.md) — Instructions from Claude (Architect) to Claude Code

> **DEPRECATION NOTE (2026-04-28):** This file is the legacy handoff channel. New active UI tasks use the multi-agent pipeline at `.claude/agents/` with per-task folders under `Docs/Specs/Active/<slug>/`. See `CLAUDE.md` § Multi-Agent Workflow for the new flow.
>
> Do not write new active tasks here — write specs into per-task folders.

> Claude Code: Read this file at the start of each task. Execute the latest instruction block. After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`. Claude (Architect) will update this file with new instructions as needed. Handoff: `Docs/TellCode.md`.
>
> **Note (2026-04-25):** `Docs/` was reorganized. Historical entries in this file or in `Docs/Archive/TELLCODE_HISTORY.md` may reference old paths:
>
> - `Docs/DIAG/...` → now `Docs/Diagnostics/...`
> - `Docs/BACKUPS/...` → now `Docs/Backups/...`
> - `Docs/PHYSICS_RESEARCH.md`, `PHYSICS_TUNING_TARGETS.md`, `LESSONS_PHYSICS_*.md` → now under `Docs/Physics/`
> - `Docs/INVENTORY_REFERENCE.md`, `UI_HIERARCHY.md`, `PATTERNS.md`, `ARCHITECTURE_AUDIT.md` → now under `Docs/Architecture/`
> - `Docs/LESSONS_FRINGE_BORDER_MESHES.md`, `BUNKER_*`, `TEE_SKIRT_*`, `ADD_HOLE.md` → now under `Docs/Pipeline/`
> - `Docs/SURFACE_MARKER_FIX_REPORT.md`, `PHASE6_STAT_COUPLING_REPORT.md`, `SPEC_PHASE6_STAT_COUPLING.md` → now under `Docs/Physics/`
> - `Docs/generate_audit.*`, `compress_screenshots.*`, `daily_report.py`, etc. → now under `Docs/Scripts/`
>
> See `Docs/README.md` for the full index map.
> ****History:** Completed task blocks and the long History Log live in `Docs/Archive/TELLCODE_HISTORY.md`. If you need detail on something old, check there first.

---

## 📅 ROADMAP — upcoming deliverables (planned 2026-04-26, updated 2026-05-01)

> Architect-tracked roadmap for the next gameplay-loop closure. Aligned with `Docs/Roadmap.md`. Order locked: A → B → C → D → E.
>
> **Canonical roadmap labels** (per `Docs/Roadmap.md`): item §1 = Putter P1, §2 = Loop v1 (single hole, lab-launched, includes Putter P2), §3 = Loop v2 (menu-to-menu, hole picker, save). Local A/B/C/D/E labels below map to those.

**A — Shot UI polish.** Wire real Figma art + sprite assets into the existing cone hierarchy + add HUD elements (player card, hole card, wind/hole indicators, power gauge, action buttons, ball/club selectors, centerpiece ball, trail). **✅ DONE 2026-05-01.** Umbrella spec archived at `Docs/Specs/Completed/PHASE_8_SHOT_UI_POLISH.md`. Original parts 8.6/8.7 delivered as 8.5.C/8.5.D; 8.8 (polish/tests/smoke) skipped — polish folded into Loop v1.

**A.0 — Canvas Scaler fix ✅ DONE 2026-04-29.** Investigation closed 2026-04-28: Figma↔Unity size mismatch root-caused to `CanvasScaler reference 1080×1920 + Match=0.5` producing a uniform \~1.31× scale factor at iPhone 12 Pro Max screens. Migration applied 2026-04-29: 7 scalers across 5 physics-lab scenes moved to `1170×2532 / Match=0`. Tooling left in tree: `Assets/Scripts/Editor/CanvasScalerMigration/` (test scene builder + migration tool, both in `GOLFIN/Canvas Scaler/` menu). Blueprint updated with new §1 "UI Coordinate System". Standing rule: **1 Figma px = 1 Unity unit at 1170 design ref — no conversion factor needed when speccing.**

**B — Putter P1 (Roadmap §1, items 1a–1d). ✅ DONE 2026-05-01.** Putter mode in lab — toggle, green-only camera, distance-only power, aim-line on green (slope arrows v1). Three iterations; iter 3 driven by Cesar rejection (track-anchor coordinate fix, predictor reference propagation across shots, predictor camera follow-through, rectangular timing slab inside PutterTrack). Architect verdict PASS with seven waivers carried forward (HoleIndicator `mts` runtime, band-line contrast, handle sprite filename, heatmap mode, power=0 hide, club-exit reversion, predictor performance unmeasured). Spec archived at `Docs/Specs/Completed/putter_p1_ui/`. QA gap analysis at `Docs/Pipeline/QA_GAPS_PUTTER_P1.md`. Phase 2 (in-context tuning) deferred to Loop v1 (Roadmap item 2f).

**B-followups — mandatory before Loop v1 closes:**
- **Predictor performance measurement.** Profiler session on `BallSimulation.Simulate` over 60 frames of active-aiming. If p95 > 5 ms on editor target, throttle.
- **Lab-only verification gap.** HoleIndicator `mts`, club-exit reversion, and power=0 path-hide all need a real hole-loop session. Consider a "Putter QA" affordance on `PhysicsLabUI` that populates `HoleContext.PinWorld` and cycles clubs.
- **Housekeeping.** Delete the Assembly-CSharp stub at `Assets/Scripts/UI/HUD/PuttPathPredictor.cs`; document iter-3 capture provenance in `screenshots/README`; capture missing `figma-reference.png`.

**C — Controls finetuning (NEXT — gates Loop v1).** Sub-tasks, sequenced. Both blockers (C.1, C.2) gate Loop v1's ball state machine (`Rolling → AtRest`); the picker rules (C.3, C.4) live in Phase 01 with the rest of the Putter cluster:

- **C.1 + C.2** ✅ DONE 2026-05-05 (`Docs/Specs/Completed/controls_c_fix/`). Phase A landed: stop-check tolerance window (`stopEpsilon = stopThresh × 0.05`) in both `RunRollPhase` + `RunPuttPhase`; `putt.csv` Green k 0.10→0.50, GreenCollar 0.14→0.40; `surfaces.csv` CartPath 0.06→0.30; 5 new EditMode tests; **203/203 PASS** bit-exact gate held. Pipeline ran end-to-end (implementer → self-reviewer FAIL iter 1 → implementer redo → reviewer PASS option a → Cesar approve). Predecessor diagnosis: `Docs/Specs/Completed/controls_c_diagnosis/`. Two Quick follow-ups also DONE: (1) added 4 `[ShotExit]` `DiagShotLogger` calls at BallSimulation.cs phase exits; (2) baked physics-lab capture rule into `CLAUDE.md` (`mcp__ai-game-developer__screenshot-game-view` does NOT refresh in same script-execute scope; mandate `CaptureHelper.SnapAtEndOfFrameAndPause` for at-rest evidence). Notion entry [`35631e0e-9a36-8176-add4-e5bc40877f0f`](https://www.notion.so/35631e0e9a368176add4e5bc40877f0f) flipped to **Done**, Closed=2026-05-05.
- **C.5** — fpMath.Sqrt convergence repair. Was originally framed as "velocity cap diagnostic" but adversarial review revealed the 64 m/s cap was a Newton-Raphson early-exit bug returning the power-of-2 initial guess, not a real velocity cap. ✅ DONE 2026-05-05 (`Docs/Specs/Completed/controls_d_velocity_cap_diagnosis/`). Replaced `fpMath.Sqrt` body with libfixmath digit-by-digit shift-and-subtract port (single-pass int64). 209 PASS + 1 IGNORED tripwire pointing at controls_e for the unmasked lift-LUT issue. Notion [`35631e0e-9a36-8133-9734-d5b4418db9f6`](https://www.notion.so/35631e0e9a3681339734d5b4418db9f6) flipped Done.
- **C.7** — Aero lift overlay calibration pass (Layer 2). Two-layer architecture frame established with Cesar 2026-05-05: Layer 1 = real physics (Bearman-Harvey 1976) kept faithful in valid range; Layer 2 = corner-case overlay tuning past published-valid range. ✅ DONE 2026-05-05 (`Docs/Specs/Completed/controls_e_aero_overlay_pass/`). Lift overlay m40=0.850, smoothstep blend S∈[0.25, 0.35], iron/wedge errors all within ±10%. New `Docs/Physics/CALIBRATION_METHODOLOGY.md` documents the two-layer pattern. Notion [`35731e0e-9a36-8172-84e4-cdb4df5a0f81`](https://www.notion.so/35731e0e9a36817284e4cdb4df5a0f81) flipped Done. Lesson K (unit-mismatch, Mars Climate Orbiter parallel) added to `Docs/Diagnostics/PIPELINE_LESSONS.md`.
- **C.8** — Drag LUT calibration audit (driver carry blocker). ✅ DONE 2026-05-06 (`Docs/Specs/Completed/controls_f_drag_calibration_audit/`). Layer-2 drag overlay landed: `aero_drag_overlay.csv` with multipliers 0.920/0.890/0.880 at v=60/70/80 m/s; smoothstep blend across v∈[45,55]. Driver carry 240→249yd (-9.5% error vs Trackman 275yd target, inside ±10% gate). Tripwire `Aero_Driver_KnownPending_LayerOneAudit` un-ignored and PASSes. CALIBRATION_METHODOLOGY.md §9 added (drag overlay) mirroring §3 (lift overlay); §8 closed. Test gate: **211/211 PASS, 0 IGNORED**. Notion [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) flipped Done. **🎉 C-cluster physics work COMPLETE.**
- **C (Phase B — Fairway/Rough/etc tuning).** Notion `35631e0e-9a36-8102-b217-d00dac3c3d92`. Queued; lands when observation numbers from `controls_c_fix` Phase A's tests give us captured values. Spec written then.
- **fpMath.Cos/Sin range-reduction repair (Phase B of fpMath).** Notion [`35731e0e-9a36-8132-96e4-cc27c4d2a734`](https://www.notion.so/35731e0e9a36813296e4cc27c4d2a734). Queued; lands after Loop v1 — quieter ~12% bug than the Sqrt cap.

**D — Gameplay Loop v1 (Roadmap §2, items 2a–2f). Single hole, lab-launched.** No menu wiring at this stage — `LabScaffold` (or a thin variant) remains the entry point. Scope per `Docs/Roadmap.md`:

1. **2a — Ball state machine:** `Aiming → Flying → Rolling → AtRest → InCup | OB`.
2. **2b — Camera transitions:** tee → flight → rest → green → cup.
3. **2c — Turn counter + shot history** (in-memory; persistence is Loop v2).
4. **2d — Hole-complete detection + result screen** (strokes, par, score).
5. **2e — Next-shot handoff:** ball at rest → re-arm controls.
6. **2f — Putter Phase 2:** in-context tuning (the deferred half of Putter P1).

Deep-dive spec written after C lands.

**E — Gameplay Loop v2 (Roadmap §3, items 3a–3e). Menu-to-menu.** Wire the existing main menu to a Hole Picker, then to a runtime version of LabScaffold so pressing Play actually starts a hole. Scope:

1. **3a — Menu wiring:** Character → Clubs → Hole → Play. **(Partially landed early via off-roadmap Mac env tasks: matchmaking_modal ✅ 2026-05-02 + hole_selection_screen 📌 NEXT.)**
2. **3b — Hole Picker UI** — **superseded by `hole_selection_screen` task** (full per-hole list with expandable cards + Lomond data + lock/played progression). Item E.3b retained as the spec for upgrading the resulting screen with per-hole thumbnails captured from Lomond website + functional filters + persistence hookup once save state lands.
3. **Runtime hole-load equivalent of** `LabScaffold` **+** `PhysicsLabHolePicker` — today's hole-load flow is editor-only via the picker EditorWindow. Need a runtime equivalent: a `GameplayScaffold` scene (lighter than LabScaffold — no debug UI/preset Fire button) that additively loads `Hole_XX_Geo.unity`, wires `ShotController`, `BallAnimator`, `ChaseCamera`, baked providers.
4. **3c — Result screen polish** (score breakdown, optional shot-replay link).
5. **3d — Next Hole / Back to Menu transitions.**
6. **3e — Save state:** persist character/clubs/score across sessions.

- Existing assets to leverage: `Mainmenu` prefab, `ShellScene.unity`, `LabScaffold.unity` (template for `GameplayScaffold`), `PhysicsLabHolePicker` (template for runtime hole picker logic), `HoleSelectionScreen` (built ahead-of-roadmap by `hole_selection_screen` task).
- **Pre-condition for closing item E:** audit all menu/inventory/roster/bags/items canvases. Confirm none are authored at `1080×1920 / Match=0.5` (the bad config that A.0 cleaned up). Any new canvases for the Hole Picker / GameplayScaffold MUST use `1170×2532 / Match=0` from the start (per Blueprint §1).
- Deep-dive spec when D is settled.

---

## ✅ Architectural state (as of 2026-04-26)

**Pivot to baked-data sim:** merged to main 2026-04-25. All tests pass (BakedPivot 24/24, Phase 1–6 physics, RealHoleTerrainTests). Cesar's "ball into void" repro eliminated by construction. Sim reads `Assets/Resources/HoleData/Hole_XX/zones.json` + `heightmap.bytes`. Scene providers demoted to editor-only placement helpers.

**Phase F cleanup:** deleted `SceneGroundProvider`, `SceneSurfaceProvider`, `PhysicsMarkerRepairTool`, `MarkerAuditTool`, 8 pre-pivot diag/agreement test files, the Phase-A `WireA3DiagSinks` harness in `PhysicsLabController`, and the stale `TERRAIN_REALTEST_FIX` Active spec. **Mid-step fix:** `Physics.Runtime.SurfaceMarker` was defined inline in the deleted `SceneSurfaceProvider.cs` — extracted to its own file (`Assets/Scripts/Physics/Runtime/SurfaceMarker.cs`) to satisfy hard rule 5 + restore importer compilation. Lesson filed (`tasks/lessons.md`: grep ALL types in a file before deleting). Test gate: **198/198 EditMode PASS, 0 failed, 0 skipped, 43.5s**. Per-step commits `phase-f.{1,1b,2,3,3.5,4,4-fix,4b,5,6}` on `main` (commits `32c73935..03744859` + lessons `8b2c82fc`).

**Mac dev environment ✅ 2026-05-02.** First end-to-end pipeline run on Mac (`matchmaking_modal` task) succeeded. Filesystem MCP + Desktop Commander + multi-agent kickoff all functional on Mac side. Cross-platform `route_subagent.py` confirmed working.

Full history in `Docs/Archive/TELLCODE_HISTORY.md`.

---

## ✅ DONE — loop_v1_2b_camera_transitions (closed end-to-end 2026-05-07 09:20 JST as PASS_WITH_DEFERRAL)

**Spec at:** `Docs/Specs/Active/loop_v1_2b_camera_transitions/`. STATUS=`ARCHITECT_REVIEW_PASS_WITH_DEFERRAL`. Move to `Completed/` on next housekeeping.

**Pipeline ran clean:** implementer → self-reviewer → reviewer subagent (ARCHITECT_REVIEW_ESCALATE — 18 PASS items + 3 FAIL items on live smoke captures blocked by pre-existing aero crash) → human Architect ruling. 9/9 new EditMode Director tests PASS, **236/236 total PASS, 0 IGNORED** (227 pre-existing + 9 additive). Notion entry [`35831e0e-9a36-81aa-b6db-cf9b781a7af0`](https://www.notion.so/35831e0e9a3681aab6dbcf9b781a7af0) flipped to **Done**, Closed=2026-05-07.

**What landed:**
- New `LoopCameraDirector` MonoBehaviour at `Assets/Scripts/Physics/Viewer/LoopCameraDirector.cs` — subscribes to `BallStateMachine.OnStateChanged`, dispatches camera modes via pure-data `Dictionary<BallState, ChaseCamera.Mode?>` map. Includes `IControllerAccessor` test seam and inner `PhysicsLabControllerAdapter`.
- Three new `ChaseCamera.Mode` values: `Downrange` (cinematic mid-flight cut at 65% of carry, behind landing zone looking back), `CupZoom` (1.0s EaseOutCubic tween hovering above flat-circle cup), `OBFreeze` (locked pivot at first OB sample XZ + 5m above terrain Y, rotation tracks ball). Existing `Chase` retuned 8m→5m back, 3m→2.5m up; FOV unchanged.
- `IModeSetter` test seam interface implemented by `ChaseCamera`. All 9 EditMode tests instantiate Director without a Camera GO using `RecordingModeSetter` + `StubControllerAccessor` + `DirectorFactory.Create` helpers.
- `PhysicsLabController` relocations: `HandleShotResolved` no longer calls `chaseCamera.SetTarget`/`ResetToOrigin` (replaced by `_lastShotOrigin`/`_lastShotLaunchDir` caching); `HandleShotComplete` no longer calls `chaseCamera.SetTarget(null)`; `FireInternal` (preset path) keeps its calls verbatim. 6 internal accessors (BallSM, LastTrajectory, LastShotOrigin, LastShotLaunchDir, CurrentBall, CurrentShotIsPutt) added.
- `TrajectoryRenderer._showInGameplay` flag with editor-or-flag visibility gate.
- New `Golfin.Diagnostics.Runtime` asmdef (`autoReferenced: true`, references `Golfin.Gameplay.Loop`). `CaptureCore` factored out of editor-side `CaptureHelper` — owns `SnapGameViewWithLabel`, `SnapAtEndOfFrameAndPause`, and the new SM-gated `SnapWhenStateReached(MonoBehaviour owner, BallStateMachine sm, BallState target, string label, ...)`. `CaptureHelper.cs` thinned to a wrapper preserving editor menu items + Fake-State presets. `SmokeTestRunner2a` inline byte-equivalent capture removed; calls `CaptureCore` directly.
- Putts skip cinematic cut entirely (Q1'c lock); load-bearing `isPutt` check on Flying entry. `PhysicsLabUI.CycleCamera` button kept as transient lab debug per Cesar lock; Director stomps overrides on next state transition.

**Two spec deviations accepted:**
1. `SnapWhenStateReached(MonoBehaviour owner, ...)` — owner-first signature is the minimal correct fix for coroutine host requirement; spec's 4-arg version was non-functional.
2. Director self-wires in own Awake via `GetComponentInParent<PhysicsLabController>()` — cleaner separation than wiring from PhysicsLabController.Awake; behavior-identical and consistent with L14.

**Two §2a OPEN FLAGs CLOSED by this spec:** CaptureHelper asmdef consolidation + capture-timing reliability — `CaptureCore.SnapWhenStateReached` is the deterministic SM-state-gated capture API the §2a flag asked for.

**Spinoff spec created:** `Docs/Specs/Queued/puttpath_predictor_perf_and_design/NOTES.md` — perf measurement + sim-vs-arcade design redesign for PuttPathPredictor. Hidden in §2b gameplay scaffold default; real disposition lands when that spec ships. NOT on Loop v1 critical path.

**Deferred smoke debt (CLOSED 2026-05-07 by `controls_g_smoke_followup`):** Visual smoke for Downrange / putter-stays-GroundLevel / OBFreeze deferred to `controls_g_aero_constant_mode_crash` closeout. Reviewer subagent surfaced 3 FAIL items on live captures because `AeroModel.ComputeAeroForce` at line 78 throws `DivideByZeroException` in the constant-mode (non-LUT) lift branch on every shot. Architect-verified: line 78 IS `spin.Rate / cfg.SpinRateReference`; implementer's proposed line-29 guard targets a different divide (`vRel/speed`) and would not fix it. Latent regression — §2a's putter shots returned at IsSpinning early-out without entering the lift branch; §2b's driver shots are the first lift-branch executions since `controls_f` closed. §2b is doing its job by surfacing the bug; not punishing §2b for finding it. controls_g spec'd at `Docs/Specs/Queued/controls_g_aero_constant_mode_crash/`.

**Architectural significance:** §2b is the second pillar of Loop v1 after §2a's BallStateMachine. §2c (turn counter) and §2d (result screen) can now subscribe to the centralized camera lifecycle instead of re-deriving "which mode should be active" independently. CaptureCore consolidation gives every future spec a deterministic SM-gated screenshot API — closes the iter-3/iter-4 false-evidence failure class systemically.

---

## ✅ DONE — controls_g_aero_constant_mode_crash (closed end-to-end 2026-05-07 19:10 JST as PASS_WITH_DEFERRAL)

**Spec at:** `Docs/Specs/Active/controls_g_aero_constant_mode_crash/`. STATUS=`ARCHITECT_REVIEW_PASS_WITH_DEFERRAL`. Move to `Completed/` on next housekeeping.

**Pipeline:** implementer Phase A (static analysis only — GUI automation could not bring editor to foreground; deviation #1 self-flagged) → Phase B (`AeroConfig.AssertValid` wired into `LoadAeroConfig` + audit comment block at top of `AeroModel.ComputeAeroForce` documenting all 3 divides) → Phase C tests (240/240 PASS, 0 IGNORED — 4 new tests: 3 in `AeroConstantModeTests.cs` + 1 new `Aero_DriverShot_DoesNotThrow` integration tripwire in `AeroCalibrationTripwireTests.cs`) → self-review skipped per CLAUDE.md hard rule #1 (IMPLEMENTER_REPORT contained 2 self-flagged FAIL items) → reviewer subagent APPROVED_WITH_DEFERRAL → human Architect ruling. Notion entry [`35931e0e`](https://www.notion.so/35931e0e9a368163a839d5190f134f0f) flipped to **Done**, Closed=2026-05-07.

**What landed:**
- `AeroConfig.AssertValid()` public method on the struct — throws `InvalidOperationException` if `SpinRateReference <= 0` or `BallMass <= 0` with descriptive error message pointing at `Resources/Physics/aero.csv`. Wired into `PhysicsConfigLoader.LoadAeroConfig` immediately before `return cfg;`.
- `AeroModel.ComputeAeroForce` audit comment block at top documenting all 3 divides: line 29 (`vRel/speed` — safe via line-26 epsilon gate), line 63 (LUT spinParam — safe via same gate), line 78 (constant-mode spinScale — safe via AeroConfig.AssertValid at config-load).
- 3 new unit tests in `AeroConstantModeTests.cs`: `Aero_ConstantModeFallback_DoesNotCrashWithDefaultConfig`, `Aero_AssertValid_ThrowsOnZeroSpinRateReference`, `Aero_AssertValid_PassesOnDefaultConfig`.
- 1 new integration tripwire `Aero_DriverShot_DoesNotThrow` — the test that would have caught controls_f's regression at controls_f closeout had it existed.
- `SmokeTestRunner2b.cs` shipped (lives in `Assets/Scripts/Physics/Viewer/`, mirrors SmokeTestRunner2a precedent) — attempted §2b deferred-smoke captures using `CaptureCore.SnapWhenStateReached`.

**Two spec deviations accepted:**
1. Phase A used static analysis instead of live Console diagnostic prints — GUI automation could not bring the editor to the foreground. Implementer's stated diagnosis ("Hypothesis C — zero-init struct") was empirically wrong: architect grep for `new AeroConfig()` and `default(AeroConfig)` across all of `Assets/` returned ZERO hits. The fix works regardless (240/240 PASS), so AssertValid backstops the symptom even though the masked mechanism is unidentified. **Lesson written to `tasks/lessons.md`** (Defense-in-Depth Fixes Can Mask the Original Regression Site).
2. `LabScaffold.unity` modified via raw YAML edit (deviation #3 in IMPLEMENTER_REPORT) because Unity was in play mode when SmokeTestRunner2b cleanup was attempted. Per `feedback_avoid_raw_scene_asset_modify.md`, this may trigger a blocking Unity reload popup. **Manual eyeball check by Cesar before merging required.**

**Two FAIL items (deferred per spec's Phase C.4 escape hatch):**
1. **Downrange visual smoke** — SmokeTestRunner2b's 3-second timed wait fired before the 0.8-power lab driver shot reached the 65%-carry cinematic cut threshold. Captured the Aiming HUD with charge ring, not Downrange. **Lesson written to `tasks/lessons.md`** (Smoke-Runner Timed Waits Are Fragile).
2. **OBFreeze visual smoke** — not attempted; requires Water-bordered tee setup not currently in LabScaffold.

**Both deferred** to followup task `controls_g_smoke_followup`. Director logic IS verified at the model layer by 9 LoopCameraDirectorTests in the 240/240 PASS gate — these are P1 evidence-of-already-tested-logic.

**Architectural significance:** `AssertValid` defense-in-depth pattern now established as the canonical response to value-type config validation. The `Aero_DriverShot_DoesNotThrow` integration tripwire is the test that would have caught controls_f's regression — carry that pattern (a one-shot "thing-doesn't-throw" tripwire per major code path) into future calibration tasks. Audit comment block in `AeroModel.cs` is the durable record so future-you doesn't re-litigate divide safety.

---

## ✅ DONE — controls_g_smoke_followup (closed end-to-end 2026-05-07 16:05 JST as ARCHITECT_REVIEW_PASS — clean, no deferral)

**Spec at:** `Docs/Specs/Completed/controls_g_smoke_followup/`. Cesar approved 2026-05-07; folder moved to Completed.

**Pipeline:** implementer (3 disclosed deviations: late-bound `SnapWhenModeReached` signature for asmdef-cycle, Putter late-fallback capture, OBFreeze heading override) → self-reviewer FORWARD_TO_ARCHITECT with 3 visual-content concerns flagged (Putter shows predictor widget, OBFreeze frame doesn't show water, Downrange ball-in-flight faint) → reviewer subagent ARCHITECT_REVIEW_PASS → human Architect ruling all 3 visual concerns ACCEPTED. Notion entry [`35931e0e-9a36-81b3-a724-ef1e42678928`](https://www.notion.so/35931e0e9a3681b3a724ef1e42678928) flipped to **Done**, Closed=2026-05-07.

**§2b deferred-smoke OPEN FLAG: CLOSED 2026-05-07.** All 3 visual confirmations captured, runtime evidence dispositive.

**What landed:**
- `LoopCameraDirector.OnModeChanged` event added; ALL `chaseCamera.SetMode` calls routed through `ApplyMode` helper (verified: 0 direct SetMode calls outside ApplyMode).
- `CaptureCore.SnapWhenModeReached` shipped via late-bound `Action<int>` overload (architect-pre-approved asmdef-cycle workaround per SPEC § escalation paths). Functionally equivalent one-shot pattern to `SnapWhenStateReached`.
- `SmokeTestRunner2b.cs` rewritten state-driven; zero `WaitForSeconds(N)` for state-dependent captures. Loads Hole_01_Geo / Hole_06_Geo additively for real-terrain backdrop.
- `IModeSetter` interface created at `Assets/Scripts/Physics/Viewer/IModeSetter.cs` (test seam abstraction for SetMode used by `RecordingModeSetter` in tests).
- 1 new EditMode test `Director_OnModeChange_RaisesEventWithNewMode` added to `LoopCameraDirectorTests.cs`. Test gate: **241/241 PASS, 0 IGNORED** (240 + 1 additive).

**Three captures filed** (both task folder + `loop_v1_2b_camera_transitions/screenshots/` per spec):
1. **Downrange** — `controls_g_followup_downrange_f291.png` (4.28MB), driver shot at 85% power on Hole_01_Geo. Mode history `[Chase, Downrange]`. Camera positioned past projected landing zone at ~65% horizontal carry.
2. **Putter GroundLevel** — `controls_g_followup_putter_groundlevel_2026-05-07_15-22-14.png` (3.97MB), putter shot at 50% power on Hole_01_Geo green. **Mode history `[]` (empty)** — dispositive proof Downrange did NOT fire. Load-bearing GroundLevel-preserved test PASS.
3. **OBFreeze** — `controls_g_followup_obfreeze_f1563.png` (4.73MB), driver shot at 50% power, heading override `2.888rad` on Hole_06_Geo (bypasses terrain ridge at x≈-22). Mode history `[Chase, Downrange, OBFreeze]`. **`ShotExit termination=HitWater finalPos=(-35.08, 7.27, -1.53)`** — ball definitively in lake bounds.

**Three architect-flagged visual concerns ruled ACCEPT:**
1. **Putter capture shows putt-path predictor widget instead of ball mid-roll.** Load-bearing assertion (no Downrange in mode history) holds. Predictor widget is a SEPARATE lab-debug surface tracked in `Docs/Specs/Queued/puttpath_predictor_perf_and_design/`. Not a Director defect. Underlying "Rolling-state-too-brief for frame-poll" is real but secondary; future fix paths: lower-power putter setup, or hook `BallStateChange` event subscription instead of state-poll.
2. **OBFreeze capture shows trees and path, no water visible.** Runtime evidence dispositive (HitWater + finalPos in lake bounds + correct mode history). Camera yaw orientation tracks ball flying away from locked pivot, which orients camera AWAY from lake toward wooded shore. That's a Director framing-intent question (should OBFreeze frame keep the hazard visible?), not a smoke-task defect. SPEC § "Out of scope" already excludes per-state animation timing tuning. **NEW forward flag added below.**
3. **Downrange capture has faint flight line, no crisp ball.** Ball at distance is genuinely small in capture; camera positioned past landing zone per Downrange contract means ball is far from camera. Mode history `[Chase, Downrange]` confirms transition. TrajectoryRenderer overlay provides secondary visual evidence. Sufficient.

**Three spec deviations accepted:**
1. `SnapWhenModeReached` late-bound `Action<int>` signature instead of typed `(LoopCameraDirector, ChaseCamera.Mode)` — pre-approved per SPEC escalation paths Q1.
2. Putter GroundLevel late-fallback capture instead of `SnapWhenStateReached(BallState.Rolling)` — Rolling state too brief; load-bearing test still holds.
3. OBFreeze heading override `CameraHeadingRadians = 2.888rad` to bypass terrain ridge at x≈-22 — documented; ball did hit water. Acceptable lab-time setup.

**Architectural significance:** `loop_v1_2b_camera_transitions` umbrella now FULLY closed end-to-end (was PASS_WITH_DEFERRAL on 2026-05-07 09:20 JST; deferred smoke debt now resolved). The Director.OnModeChanged event is the canonical observation seam for camera mode transitions — future replay tools, analytics, debug overlays subscribe to it instead of polling. CaptureCore now has both `SnapWhenStateReached` (SM transitions) and `SnapWhenModeReached` (Director transitions) — the full state-driven capture toolbox is shipped.

---

## 📌 NEXT — controls_h_chase_camera_regression (SPEC_READY 2026-05-07 17:20 JST) — BLOCKS §2c

**Folder:** `Docs/Specs/Active/controls_h_chase_camera_regression/`. STATUS=`SPEC_READY`. Tier 3 pipeline.

**Kickoff for Code:** `Use the golfin-implementer subagent on "controls_h_chase_camera_regression"`

**Notion:** [`35931e0e-9a36-8179-a1f3-cf6bde4fc340`](https://www.notion.so/35931e0e9a368179a1f3cf6bde4fc340) — P1 — High, S (half-day), Order 250, Status=In Progress.

**Why P1 + blocks §2c:** Chase camera doesn't visually track ball during touch/flick shots (Cesar manual play 2026-05-07). Driver/Iron/OB all broken. §2b smoke captures missed it because `OnModeChanged` proves dispatch fired, not that camera tracked anything — a fundamental methodology gap that this SPEC also closes. §2c subscribes to the same SM events and ships TURN counter on top of broken camera flow; fix this first.

**One-line:** Reorder `HandleShotResolved` (cache `_lastShotOrigin/Dir` + call `BallAnimator.Play()` BEFORE `_ballSM.OnTrajectoryComputed`) so the synchronous Aiming→Flying SM transition sees fresh data + the post-Play ball Transform. Kill `FireInternal`'s legacy direct chase-camera calls (route through SM/Director like touch path). Add 1 EditMode integration test asserting `setter.GetTarget() == ballAnimator.CurrentBall` after `HandleShotResolved` returns. Add new Pipeline Lesson O codifying "`OnModeChanged` is dispatch evidence, not visual evidence." Update SPEC template with visual-fidelity sub-section. Archive SmokeTestRunner2a/2b out of Assets/.

**Hard rules** (full set in SPEC.md § Hard rules):
1. Do NOT modify `BallStateMachine.cs` source logic. ONLY the docstring at lines 62-66 changes.
2. Do NOT modify `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, `BallAnimator.cs`, `ChaseCamera.cs`, `LoopCameraDirector.cs` source logic. Only PhysicsLabController.HandleShotResolved + FireInternal change in this task.
3. Do NOT add new events to BallAnimator (Option C rejected).
4. Do NOT defer the SM synchronous fire (Option B rejected).
5. Do NOT modify `LabScaffold.unity` via raw YAML — use Unity Editor MCP if SmokeTestRunner removal triggers scene reference issues.
6. **Do NOT skip the 5 manual content-sanity descriptions.** Per Lesson O — written by THIS spec, applied to THIS spec — runtime event-dispatch captures are not sufficient. Implementer must drive the lab manually for all 5 cases (Driver full-power, Iron half-power, Driver into OB lake, Putter, two consecutive shots) and write descriptions in IMPLEMENTER_REPORT § Visual Verification.
7. Do NOT use `OnModeChanged`-only captures as visual verification. That's the failure mode this spec exists to fix.
8. Bit-exact 248-test PASS gate must hold; +1 new test = 249/249 target. NO snapshot updates without architect approval.

**Definition-of-done:** HandleShotResolved reordered; FireInternal SM/Director-routed; BallStateMachine docstring updated; new EditMode integration test PASS; Lesson O written; SPEC template updated with visual-fidelity sub-section; SmokeTestRunner2a/2b moved to Docs/Specs/Completed/loop_v1_2{a,b}_*/; **249/249 PASS, 0 IGNORED**; 5 manual content-sanity descriptions + 3 file artifacts under `Docs/Specs/Active/controls_h_chase_camera_regression/screenshots/`; Cesar manually verifies the 5 cases pass.

**Estimate:** half-day. §2c stays SPEC_READY but kickoff WAITS for this fix to land. Cesar's camera tuning session also waits.

---

## ⏸️ BLOCKED ON controls_h — loop_v1_2c_turn_counter_and_shot_history (SPEC_READY 2026-05-07 21:15 JST)

**Folder:** `Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/`. STATUS=`SPEC_READY`. Tier 3 pipeline.

**Kickoff for Code:** `Use the golfin-implementer subagent on "loop_v1_2c_turn_counter_and_shot_history"` — **AFTER controls_h closes.**

**Notion:** [`35931e0e-9a36-812e-b0ca-ff6ac972d7cd`](https://www.notion.so/35931e0e9a36812eb0caff6ac972d7cd) — P1 — High, S (half-day), Order 240, Status=In Progress.

**Why blocked:** §2c subscribes to `BallStateMachine.OnShotComplete` and reads `LastTrajectory` / `LastShotOrigin` from PhysicsLabController. controls_h's reorder of `HandleShotResolved` changes WHEN those fields are valid relative to SM transitions. §2c spec was written assuming the buggy order; needs re-verification against the fixed order before kickoff. Re-verification is small (10 minutes architect work) but blocking.

**One-line:** Extend `GameSession.cs` with `ShotHistory` list + `ResetForNewHole()` method. Add new `HoleSessionDriver` MonoBehaviour (mirrors `LoopCameraDirector` precedent) that subscribes to `BallStateMachine.OnShotComplete`, builds a `ShotRecord`, appends to history, schedules `SetTurn(turn+1)` after a configurable 1.5s settling delay. Add 2 calls in `PhysicsLabController` (OnHoleLoaded after `HoleContext.Raise()`, OnHoleUnloaded after `HoleContext.Reset()`) to fire `GameSession.ResetForNewHole()`.

**Hard rules** (full set in SPEC.md § Hard rules):
1. Do NOT modify `BallStateMachine.cs`, `BallState.cs`, `ShotResult.cs`, `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, any aero CSV, any test currently in PASS state outside `HoleSessionDriverTests.cs`.
2. Do NOT modify `PlayerCardWidget.cs` — already correctly wired; touching risks breaking existing render.
3. Do NOT modify `LabScaffold.unity` via raw YAML — use Unity Editor MCP APIs (`gameobject-component-add`, `gameobject-component-modify`, `scene-save`). Per controls_g deviation #3 lesson.
4. Do NOT use `WaitForSeconds(N)` for state-dependent CAPTURES (state-gate via `SnapWhenStateReached`). The 1.5s `postShotDelaySeconds` is allowed because it's a deliberate user-facing settling delay, NOT a capture trigger.
5. Do NOT add InCup handling beyond what falls out automatically — §2d wires the real ICupDetector.
6. Do NOT proliferate static-bus files — extend `GameSession`, do not create `HoleSessionContext` / `ShotHistoryContext` / etc.
7. Smoke evidence per §2a Lessons M+N + reviewer's controls_g lesson: file persisted on disk + parallel-path Read verification + content-sanity description + Director mode history.
8. Bit-exact 241-test PASS gate must hold; +7 new tests = 248/248 target. NO snapshot updates without architect approval.

**Definition-of-done:** `GameSession` extended; `ShotRecord` struct shipped; `HoleSessionDriver` MonoBehaviour shipped + Inspector-wired in LabScaffold via Unity Editor MCP; `PhysicsLabController` calls `GameSession.ResetForNewHole()` from both `OnHoleLoaded` (after `HoleContext.Raise()`) and `OnHoleUnloaded` (after `HoleContext.Reset()`); 7 new EditMode tests; **248/248 PASS, 0 IGNORED**; 3 captures + 1 history-log artifact under `Docs/Specs/Active/loop_v1_2c_turn_counter_and_shot_history/screenshots/` with `controls_2c_*` prefix; TURN label visibly increments shot-to-shot AND resets on hole reload.

**Estimate:** half-day. Can run in parallel with PuttPathPredictor spinoff or HUD ClubContext drift triage if Cesar wants concurrent work.

---

## ✅ DONE — Matchmaking Modal (Mac env test, off-roadmap)

**Spec:** `Docs/Specs/Completed/matchmaking_modal/` (move from `Active/` on next housekeeping pass).

**Result 2026-05-02:** Wired fake-matchmaking behaviour onto the existing `MatchMakingModal` prefab. Tap Home screen's Next-Hole PLAY button → modal opens, "FINDING OPPONENT…" cycles dots, opponent portrait/name/rank cycles every ~0.3 s, hole + rewards mirror the Home screen's Next Hole panel, after `searchDurationSeconds` (default 5 s) the title flips to "OPPONENT FOUND" and the opponent locks. Cancel hides the modal. Mac pipeline working as expected. **`MatchmakingModalController.Open(int holeIndex)` is now the canonical entrypoint** for any "tap PLAY" flow — re-used by the hole_selection_screen task next.

**Files landed:** `CharacterThumbnailCard.cs` (one new method `InitializeFromTemplate`), `HomeScreenController.cs` (1 SerializeField + 5-line edit to `OnPlayClicked`), new `MatchmakingModalController.cs` + auto-wire, `ShellScene.unity` (controller component + inspector wiring). Prefab itself NOT modified.

---

## ✅ DONE — controls_c_diagnosis (2026-05-04 17:45 JST)

**Spec archived:** `Docs/Specs/Completed/controls_c_diagnosis/`. Architect verdict PASS. Diagnostic instrumentation in (4 loggers + 5 emit sites + lab wire-up); 198/198 EditMode tests green; bit-exact gate intact.

**Headline findings (collapsed from C.1+C.2 hypotheses):**
- **C.1 was misframed.** Putter pipeline is correct end-to-end: override 5 m/s, IsPutt=True, all gate clauses pass, captured velMagnitude=2.05 m/s at 41% effort. The "100 yd" symptom is rolling-resistance integration: `d_max = v₀/k` produces 17.30 m for a 41% putt on Green→Fairway transition.
- **C.2 root cause: stopConsecutive clause 2 (`speedSq <= prevSpeedSq`) intermittently fails.** Sub-mm slope re-acceleration breaks the "speed non-increasing" check on real heightmap. Counter went 0→8 over 336 steps on Shot 1; never advanced from 0 in 75s on Shot 2.
- **Bonus finding (out of scope for fix):** ShotEntry observes `|v|=64.000 m/s` when Build resolved 93.77 m/s on driver full-power. Suspiciously round number. Hard cap somewhere between Build and Phase-6 entry. Q16.16 fp doesn't overflow at 100 m/s, so it's not arithmetic. Tracked separately as Notion C.5.

**Pipeline lessons captured:**
- `[ShotExit]` absence is itself diagnostic evidence — capture missing termination tag = sim never terminated, exactly the C.2 evidence.
- Diagnostic-only specs ship without screenshots when logs are load-bearing evidence (per spec's own Step 8 wording).
- The stop-check has TWO clauses, not one. Future fix work touching `RunRollPhase` or `RunPuttPhase` must reason about both.
- `screenshot-game-view` MCP returned null on three retries; `CaptureHelper.SnapGameViewWithLabel` (project-mandated path) worked fine. Implementer subagent prompt may benefit from defaulting to CaptureHelper.

---

## ✅ DONE — controls_d_velocity_cap_diagnosis (closed end-to-end 2026-05-05)

**Spec archived:** `Docs/Specs/Completed/controls_d_velocity_cap_diagnosis/`. Pipeline ran the full loop: implementer (Sqrt fix + 6 fpMath tests + re-snapshots) → self-reviewer PASS → reviewer PASS → Cesar ESCALATE (carry numbers smelled wrong) → human Architect FAIL with single tripwire fail item → implementer redo (added `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` to `AeroCalibrationTripwireTests.cs`, `[Ignore]`-tagged) → self-reviewer PASS → reviewer PASS → Cesar approve. Test gate now **209 PASS + 1 IGNORED**. Notion entry [`35631e0e-9a36-8133-9734-d5b4418db9f6`](https://www.notion.so/35631e0e9a3681339734d5b4418db9f6) flipped to **Done**, Closed=2026-05-05.

**What landed:** `fpMath.Sqrt` body replaced with libfixmath digit-by-digit shift-and-subtract port (single-pass int64). Driver `|v|` now returns true ~103 m/s instead of capped 64; putter ~2.24 instead of capped 2.0. Bit-exact gate broke as expected; ~14 tests re-snapshotted (carries shifted to true post-Sqrt-fix values). One ignored tripwire test pointing at `controls_e_aero_overlay_pass` for the lift-LUT recalibration definition-of-done.

**Phase B (Cos/Sin) still queued:** [`35731e0e-9a36-8132-96e4-cc27c4d2a734`](https://www.notion.so/35731e0e9a36813296e4cc27c4d2a734) `C.6 — fpMath.Cos/Sin range-reduction repair (Phase B)`. Lands after Loop v1.

## ✅ DONE — loop_v1_2a_ball_state_machine (closed end-to-end 2026-05-06 14:32 JST)

**Spec archived:** `Docs/Specs/Completed/loop_v1_2a_ball_state_machine/`. Pipeline ran four iterations including a Cesar-caught iter-3 false-evidence rejection (`SmokeTestRunner2a.cs` claimed-on-disk but actually Roslyn-only). Iter 4 persisted the file at parallel paths, ran the smoke from the compiled assembly (`Type.GetType(AssemblyQualifiedName)` receipt), captured a fresh frame-218 putter-at-rest screenshot, and passed all three pipeline stages plus Cesar approval. Merged via PR #3 (`6b9385a4`).

**What landed:**
- New `Golfin.Gameplay.Loop` asmdef with `noEngineReferences: true` — pure-logic state machine, structurally guaranteed determinism (no Unity-API leakage possible), headless-bot-ready.
- 8 files in `Assets/Scripts/Gameplay/Loop/`: `BallState.cs`, `OBReason.cs`, `BallStateChange.cs`, `ShotResult.cs`, `ICupDetector.cs`, `NullCupDetector.cs`, `BallStateMachine.cs` (299 lines), asmdef.
- `BallStateMachineTests.cs` (567 lines): 16 new EditMode tests covering each transition, OB sub-reasons (Water / OutOfBounds / ExitedWorldBounds), cup detection via stub injection, bounce flicker preservation, headless-vs-non-headless determinism, `ReArm` from all terminal states, illegal-transition negative test, null-arg guards.
- `PhysicsLabController.cs` integration H1–H9 verbatim per SPEC. One architect-accepted deviation: `_prevBallPlaying` retained minimally for preset-shot orbit-reset only — preset shots don't fire `OnShotResolved` so the SM never sees them; touch-flick path goes through SM cleanly.
- `SmokeTestRunner2a.cs` (302 lines, scope-add by implementer at iter 4 to satisfy Cesar's "smoke from a committed file, not Roslyn" requirement). Lives in runtime asmdef; future cleanup candidate to move into editor-only assembly.
- New Lesson in `Docs/Diagnostics/PIPELINE_LESSONS.md` on Read-tool false-evidence: parallel-path Read + content-sanity matching is necessary evidence when the reviewer's toolkit lacks Bash/Glob (per Cesar's iter-3 rejection note).

**Test gate:** **227/227 PASS, 0 IGNORED** (211 pre-existing + 16 new SM tests).

**Architectural significance:** §2a is the spine of Loop v1. §2b camera transitions, §2c turn counter, §2d result screen, §2e next-shot handoff all subscribe to `BallStateMachine.OnStateChanged` (fine-grained) or `OnShotComplete(ShotResult)` (coarse, one-per-shot). The coarse channel prevents downstream consumers from re-implementing the AtRest/InCup/OB filter and getting it subtly wrong. Headless flag is exposed and tested but not yet wired to a caller — ready for foundation #5 (bot-pool sims) when that work begins.

**Two follow-up flags landed in OPEN FLAGS:** ClubContext static-bus drift (HUD didn't update club name across shots in iter-4 smoke); CaptureHelper asmdef consolidation + capture-timing reliability.

**Next umbrella:** **§2b — Camera transitions** (`tee → flight → rest → green → cup`). Likely fan-out candidate per memory: each `ChaseCamera.Mode` is an independent file with no scene/singleton/asmdef overlap.

## ✅ DONE — controls_f_drag_calibration_audit (closed end-to-end 2026-05-06 06:47 JST)

**Spec archived:** `Docs/Specs/Completed/controls_f_drag_calibration_audit/`. Pipeline ran clean: implementer → reviewer (ARCHITECT_REVIEW_PASS-with-caveat, since Unity MCP was unavailable to literally execute Test Runner) → Cesar manually verified `Window > Test Runner > EditMode > Run All` → **211 PASS, 0 IGNORED** confirmed. Notion entry [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) flipped to **Done**, Closed=2026-05-06.

**What landed:**
- New `aero_drag_overlay.csv` with multipliers `1.000` below v=45 m/s and `0.920 / 0.890 / 0.880` at v=60/70/80 m/s. Smoothstep blend across `v∈[45,55]` m/s prevents seam discontinuity.
- Drag overlay seam in `AeroModel.ComputeAeroForce` with `BlendDragOverlay` smoothstep helper, mirroring the lift overlay pattern from `controls_e` verbatim.
- New `AeroConfig` fields `DragOverlay` + `UseDragOverlay`; `PhysicsConfigLoader.LoadDragOverlay()` mirroring `LoadLiftOverlay()`.
- `AeroCalibrationHarness.cs` extended with vMax + time-above/below/in-seam diagnostic columns.
- `[Ignore]` removed from `Aero_Driver_KnownPending_LayerOneAudit` test — now PASSing.
- Driver carry: 240.4yd → **249.0yd** (-9.5% error vs Trackman 275yd target, inside ±10% gate). Iron/wedge errors stay tight: 7-iron -0.5%, 9-iron -6.6%, PW -6.1%.
- Driver flight breakdown: ~61% above seam (full overlay active), ~12% in seam zone, ~27% below seam (Layer 1 only). Matches spec's design exactly.
- `Docs/Physics/CALIBRATION_METHODOLOGY.md` adds **§9 (When to add a Layer-2 drag overlay)** mirroring §3 (lift overlay). §8 closed with cross-reference to §9.
- Layer-status header on `aero_drag_lut.csv` updated to point at the new overlay (no value changes).

**Smoothstep seam verified smooth:** 9-point sweep at v∈{40, 43, 45, 48, 50, 52, 55, 58, 60} shows monotonically increasing carry with rate-of-change decreasing smoothly. No kink at v=45 or v=55.

**Two cleanup notes (handled or to handle):**
- Temporary utility `Assets/Scripts/Editor/Physics/RunHarnessMenuItem.cs` was added by implementer for verification; can be deleted now (Cesar's manual cleanup or next implementer touch).
- Spec wording "8/8 clubs PASS" was carried over from controls_e (where calibration set was already 4 clubs); actual harness is 4 clubs, gate is 4/4. Future spec edits should drop the 8 reference.

**🎉 C-cluster physics work: COMPLETE.** Roadmap §1 (Putter P1) closing follow-ups all delivered:
- `controls_c_diagnosis` ✅ — stop-check + velocity-cap symptoms triaged
- `controls_c_fix` ✅ — stop-check tolerance window + Green/GreenCollar/CartPath tuning
- `controls_d_velocity_cap_diagnosis` ✅ — fpMath.Sqrt convergence repair
- `controls_e_aero_overlay_pass` ✅ — Layer-2 lift overlay + CALIBRATION_METHODOLOGY.md
- `controls_f_drag_calibration_audit` ✅ — Layer-2 drag overlay

Loop v1 §2a (Ball state machine) is the next umbrella.

## ✅ DONE — controls_e_aero_overlay_pass (closed end-to-end 2026-05-05 19:57 JST)

**Spec archived:** `Docs/Specs/Completed/controls_e_aero_overlay_pass/`. Pipeline ran the full loop including iteration 1 → IMPLEMENTER_BLOCKED escalation → architect FAIL with three items → implementer iteration 2 → self-reviewer PASS → reviewer PASS → Cesar approve. Test gate now **210 PASS + 1 IGNORED** (211 total). Notion entry [`35731e0e-9a36-8172-84e4-cdb4df5a0f81`](https://www.notion.so/35731e0e9a36817284e4cdb4df5a0f81) flipped to **Done**, Closed=2026-05-05.

**What landed:**
- New `aero_lift_overlay.csv` with m40=0.850 (architect predicted [0.80, 0.90] band; landed mid-band).
- Lift overlay seam in `AeroModel.cs` with `BlendOverlay` smoothstep helper (S∈[0.25, 0.35]).
- New `AeroConfig` fields `LiftOverlay` + `UseLiftOverlay`; `PhysicsConfigLoader.LoadLiftOverlay()` mirroring `LoadLiftLut()` pattern.
- New `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` with both CLI + `MenuItem("GOLFIN/Physics/Run Aero Calibration Sweep")` surfaces.
- Tripwire split into two tests: active `Aero_MidHighSpinClubs_WithinTourCarryRange` (PASS for iron7/iron9/PW) + `[Ignore]`-tagged `Aero_Driver_KnownPending_LayerOneAudit` referencing `controls_f` (now removed by controls_f).
- New `Docs/Physics/CALIBRATION_METHODOLOGY.md` with all 8 sections (two-layer architecture, Trackman targets, Bearman-Harvey valid range, harness usage, smoothstep math, when-to-recalibrate, Layer-1 sanctity rule, what-to-do-when-in-BH-range-club-misses).
- Layer-status headers added to `aero_lift_lut.csv` (Layer 1), `aero_drag_lut.csv` (Layer 1, audit pending), `surfaces.csv` (Layer 2), `putt.csv` (Layer 2).
- Final per-club errors: iron7 −0.1%, iron9 −6.2%, PW −5.6% — all within ±10%. Driver −12.7% intentionally tracked in controls_f (since closed).

**Lesson K written** to `Docs/Diagnostics/PIPELINE_LESSONS.md` documenting the unit-mismatch failure mode (Mars Climate Orbiter parallel; architect picked METERS table values from Trackman PDF mistaking them for YARDS).

## 📜 HISTORY — controls_f scope and rationale (kept for reference)

**Phase A scope (locked, all 5 open questions answered 2026-05-05 evening):**
- **Seam location:** `v ∈ [45, 55]` m/s (surgical — driver fully affected ~60% of flight, irons mostly unaffected, only 5-iron grazes the seam zone)
- **Iron tolerance after drag tune:** strict ±10% per club (matches `controls_e` and tripwire)
- **Correction shape:** multiplicative (Cd × m), mirrors lift overlay pattern
- **Drag-crisis transition (v<22):** untouched
- **Trackman re-validation:** documented as trigger in methodology, no auto-action

**What this implements:** New `aero_drag_overlay.csv` applies a multiplicative correction to `Cd` only past Bearman-Harvey valid Re range. Smoothstep blend across `v ∈ [45, 55]` m/s prevents seam discontinuity. In Layer-1-valid territory (v ≤ 45), overlay multiplier is forced to 1.0 — Bearman-Harvey is trusted as-is. New `LiftOverlay`-mirroring fields on `AeroConfig` (`DragOverlay`, `UseDragOverlay`), new `LoadDragOverlay()` in `PhysicsConfigLoader`, new overlay seam in `AeroModel.ComputeAeroForce` with `BlendDragOverlay` private helper. Existing `AeroCalibrationHarness.cs` extended with vMax / time-above-seam / time-below-seam diagnostic columns.

**Real-world data verified per Lesson K** (NOTES.md): Trackman 275yd driver carry target (already triple-checked from `controls_e`); Bearman-Harvey 1976 (Layer-1 truth, Cd ≈0.22 supercritical); Smith et al. 2010 Kobe Univ. CFD (validation, Cd 0.22 "nearly constant"); Alam et al. 2011 multi-ball comparison (Cd range 0.21–0.27 across Tour balls; our 0.23 LUT is plausible midpoint).

**Architect-time prediction:** final multipliers will land around **0.90 at v=80 m/s** (back-of-envelope: ~9% drag reduction in driver speed range, ~25–30 yd added carry, bringing 240→~265–270yd). Actual response measured by harness during iteration.

**Definition-of-done:** Remove `[Ignore]` from `Aero_Driver_KnownPending_LayerOneAudit` test. Final gate: **211/211 PASS, 0 IGNORED.**

**Critical deliverables beyond the overlay:** `Docs/Physics/CALIBRATION_METHODOLOGY.md` adds **§9 (When to add a Layer-2 drag overlay)** mirroring the existing §3 (lift overlay). §8 updated to close the open follow-up. Layer-status header on `aero_drag_lut.csv` updated to point at the new overlay.

**Risk profile:** very low. Architecture is symmetric to `controls_e`; harness exists; the only structural unknowns are (a) does multiplier 0.85–0.95 close the gap (architect predicts yes; if no, drag isn't the issue and we escalate), (b) does the seam at [45, 55] perturb iron carries (5-iron is the canary; predicted to stay in tolerance). Both are caught by SPEC's escalation paths.

**Sequencing:** controls_f is the **last C-cluster physics task**. After it lands clean, Loop v1 §2a (Ball state machine) is the next umbrella spec — estimated half-day to 1 day total for controls_f, leaving tomorrow free for Loop v1 phase 2 work as Cesar requested.

---

## ✅ DONE — controls_e_aero_overlay_pass (closed end-to-end 2026-05-05 19:57 JST)

**Spec archived:** `Docs/Specs/Completed/controls_e_aero_overlay_pass/`. Pipeline ran the full loop including iteration 1 → IMPLEMENTER_BLOCKED escalation → architect FAIL with three items → implementer iteration 2 → self-reviewer PASS → reviewer PASS → Cesar approve. Test gate now **210 PASS + 1 IGNORED** (211 total). Notion entry [`35731e0e-9a36-8172-84e4-cdb4df5a0f81`](https://www.notion.so/35731e0e9a36817284e4cdb4df5a0f81) flipped to **Done**, Closed=2026-05-05.

**What landed:**
- New `aero_lift_overlay.csv` with m40=0.850 (architect predicted [0.80, 0.90] band; landed mid-band).
- Lift overlay seam in `AeroModel.cs` with `BlendOverlay` smoothstep helper (S∈[0.25, 0.35]).
- New `AeroConfig` fields `LiftOverlay` + `UseLiftOverlay`; `PhysicsConfigLoader.LoadLiftOverlay()` mirroring `LoadLiftLut()` pattern.
- New `Assets/Scripts/Editor/Physics/AeroCalibrationHarness.cs` with both CLI + `MenuItem("GOLFIN/Physics/Run Aero Calibration Sweep")` surfaces.
- Tripwire split into two tests: active `Aero_MidHighSpinClubs_WithinTourCarryRange` (PASS for iron7/iron9/PW) + `[Ignore]`-tagged `Aero_Driver_KnownPending_LayerOneAudit` referencing `controls_f`.
- New `Docs/Physics/CALIBRATION_METHODOLOGY.md` with all 8 sections (two-layer architecture, Trackman targets, Bearman-Harvey valid range, harness usage, smoothstep math, when-to-recalibrate, Layer-1 sanctity rule, what-to-do-when-in-BH-range-club-misses).
- Layer-status headers added to `aero_lift_lut.csv` (Layer 1), `aero_drag_lut.csv` (Layer 1, audit pending), `surfaces.csv` (Layer 2), `putt.csv` (Layer 2).
- Final per-club errors: iron7 −0.1%, iron9 −6.2%, PW −5.6% — all within ±10%. Driver −12.7% intentionally tracked in controls_f.

**Lesson K written** to `Docs/Diagnostics/PIPELINE_LESSONS.md` documenting the unit-mismatch failure mode (Mars Climate Orbiter parallel; architect picked METERS table values from Trackman PDF mistaking them for YARDS).

**`controls_f` is the natural next move:** ✅ SPEC.md written 2026-05-05 evening, moved to Active. See "📌 NEXT — controls_f_drag_calibration_audit" block above for full state.

## 📜 HISTORY — controls_e_aero_overlay_pass kicked back to implementer (ARCHITECT_REVIEW_FAIL 2026-05-05)

**STATUS flipped from `IMPLEMENTER_BLOCKED` → `ARCHITECT_REVIEW_FAIL`** by human Architect after escalation review. Three FAIL items, all tightly scoped. Architect's full response in `Docs/Specs/Active/controls_e_aero_overlay_pass/ARCHITECT_REVIEW.md` (newly written for this iteration since pipeline reviewer didn't run).

**FAIL summary:**
1. **Correct the Trackman target values.** Architect picked unit-mismatched values from the Trackman PDF (METERS table mistaken for YARDS table). Real values verified against two independent sources: driver 290→**275**, 7-iron 175→**172**, 9-iron 145→**148**, PW 115→**136** (all yards). New `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson K written documenting the failure mode (Mars Climate Orbiter parallel).
2. **Re-tune the lift overlay against corrected targets.** The implementer's m40=0.55 was driven by chasing wrong (too-low) PW target of 115 instead of real 136. With correct targets, overlay should relax upward to ~m40=0.80–0.90 — a much smaller correction that respects more of the Bearman-Harvey curve. Healthier outcome.
3. **Split the tripwire test in two.** Cesar locked: "don't want other clubs braking the 10% rule" — so unified ±15% gate is rejected. Driver gets its own `[Ignore]`-tagged test pointing at controls_f. Irons + wedge stay tight at ±10% in `Aero_MidHighSpinClubs_WithinTourCarryRange`. Architecture stays unified; one overlay, one blend, one methodology. Driver carve-out is a TEST split, not a system split.

**Architecture decision locked with Cesar (2026-05-05):** Driver miss is a Layer-1 drag-LUT issue (Cd=0.23 floor at v≥30 m/s likely too high vs supercritical-Re golf-ball Cd ~0.18–0.22). Layer-2 lift overlay correctly excludes the S≤0.25 regime by design — the gap belongs to controls_f, not controls_e. CALIBRATION_METHODOLOGY.md gets a new section: "What to do when an in-Bearman-Harvey-valid-range club misses target" — answer: it's a Layer-1 issue, separate audit task, NOT an overlay extension. This preserves the Layer 1/Layer 2 boundary going forward.

**Notion:** controls_f entry [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) escalated **P3 → P1**, renamed "C.8 — Drag LUT calibration audit (driver carry blocker)". SPEC writing scheduled by architect once controls_e closes end-to-end.

**Path to PASS after this addendum:** implementer corrects targets → re-runs calibration (expected ~30 min) → splits tripwire → adds methodology section → documents Trackman citation properly per Lesson K → self-reviewer → reviewer subagent → Cesar approve. Final gate: **210 PASS + 1 IGNORED** (driver test ignored pending controls_f).

## 📜 HISTORY — controls_e_aero_overlay_pass implementer escalation (IMPLEMENTER_BLOCKED 2026-05-05)

(Implementer correctly identified that driver cannot be calibrated by lift overlay because it sits at S=0.08 inside Bearman-Harvey valid range. Diagnosis was right — architecture decision needed from human Architect to handle the known Layer-1 miss. See "NEXT" block above for resolution. Original IMPLEMENTER_REPORT.md preserved at `Docs/Specs/Active/controls_e_aero_overlay_pass/IMPLEMENTER_REPORT.md`.)

---

## 📜 HISTORY — controls_e_aero_overlay_pass initial SPEC writing (SPEC_READY 2026-05-05)

(SPEC was written, implementer ran iteration 1, escalated mid-pipeline to IMPLEMENTER_BLOCKED — see HISTORY blocks above for that escalation and the architect's FAIL response. SPEC content preserved below for retrospective.)

**Spec written and folder moved to Active.** `Docs/Specs/Active/controls_e_aero_overlay_pass/SPEC.md` — STATUS=SPEC_READY. Tier 3 pipeline. Notion entry [`35731e0e-9a36-8172-84e4-cdb4df5a0f81`](https://www.notion.so/35731e0e9a36817284e4cdb4df5a0f81) flipped to **In Progress** (P1 High, M 1–2 days, Order 150).

**Phase A scope (locked, 5 questions answered 2026-05-05):**
- **Trackman year:** Trackman 2024 published Tour averages, adjusted with 2025 trend updates from Trackman's blog where applicable. Trackman 2026 has NOT published a full annual report yet; implementer sources the latest available at implementation time and cites URL + date.
- **Calibration set:** 8 clubs (driver, 3-wood, 5–9 irons, PW)
- **Tolerance:** ±10% per club (matches the tripwire test from `controls_d`)
- **Harness UI:** CLI-callable from Code's pipeline AND `MenuItem("GOLFIN/Physics/Run Aero Calibration Sweep")` for manual spot-checks. Both surfaces invoke the same `AeroCalibrationHarness.RunCalibrationSweep()` method.
- **Overlay format:** Flat CSV (`spin_parameter,cl_multiplier,notes`)

**What this implements:** A new `aero_lift_overlay.csv` applies a multiplicative correction to `Cl` only past the Bearman-Harvey valid range (S > 0.30) where the LUT is currently extrapolating. Smoothstep blend across `S ∈ [0.25, 0.35]` (formula `t² × (3 − 2t)`) prevents seam discontinuity. In Layer-1-valid territory (S ≤ 0.25), overlay multiplier is forced to 1.0 — Bearman-Harvey is trusted as-is. New `LiftOverlay` field on `AeroConfig`, new `LoadLiftOverlay()` in `PhysicsConfigLoader`, new overlay seam in `AeroModel.ComputeAeroForce` with `BlendOverlay` private helper.

**Critical deliverable beyond the overlay:** `Docs/Physics/CALIBRATION_METHODOLOGY.md` (NEW) documents the two-layer architecture frame (Layer 1 = real physics, Layer 2 = corner-case overlay), Trackman calibration target reference, Bearman-Harvey valid range, harness usage, smoothstep math, "when to recalibrate" triggers, and the Layer-1-sanctity rule. Plus top-of-file Layer-status headers added to `aero_lift_lut.csv` (Layer 1), `aero_drag_lut.csv` (Layer 1, audit pending), `surfaces.csv` (Layer 2), `putt.csv` (Layer 2). The doc is the durable deliverable; overlay multipliers are just the first instantiation.

**Calibration loop:** harness runs sim with Trackman launch params for each calibration club, prints per-club error table; iterative tuning of `aero_lift_overlay.csv` multipliers until all 8 within ±10% (~30–40 min implementer time, expected 4–8 iterations). Then `[Ignore]` removed from `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime`. Final test gate: **210/210 PASS** (209 pre-existing + tripwire now-enabled).

**Files this task touches:** new `aero_lift_overlay.csv` + new `AeroCalibrationHarness.cs` + new `CALIBRATION_METHODOLOGY.md`; modified `AeroConfig.cs` / `AeroModel.cs` / `PhysicsConfigLoader.cs` / `aero.csv` (one new row); header-only edits to 4 existing CSVs; one `[Ignore]` removal in `AeroCalibrationTripwireTests.cs`. No asmdef / scene / prefab / `BallSimulation.cs` / `fpMath.cs` / 209 pre-existing test files changed.

**Critical risk:** if the 209 pre-existing tests fail after the overlay is enabled, that means the overlay is leaking into Layer-1-valid territory (likely cause: `BlendOverlay` not returning exactly `fp.One` for `spinParam ≤ 0.25`). SPEC.md § "Mid-task escalation paths" handles it via `IMPLEMENTER_BLOCKED`.

**Sibling P3 task** [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) `C.8 — Drag LUT calibration audit (Layer 2 sibling)` Queued, runs after this lands.

**Roadmap reference:** `Docs/Roadmap.md` §1 (Putter P1) closing follow-up. Does NOT gate §2 (Loop v1) start. Recommended to land before Loop v1 *playtest* feel.

---

## 📜 HISTORY — controls_d_velocity_cap_diagnosis kicked back to implementer (ARCHITECT_REVIEW_FAIL 2026-05-05)

(Final state of this iteration: implementer added the `[Ignore]`-tagged tripwire test, pipeline closed end-to-end 2026-05-05. See "DONE" block above. Original FAIL kickoff details preserved below for retrospective.)

**Status flipped from `ARCHITECT_REVIEW_PASS` → `ARCHITECT_REVIEW_ESCALATE` → `ARCHITECT_REVIEW_FAIL`** by human Architect after Cesar walked the post-fix carry numbers and surfaced the lift-LUT issue. Sqrt fix itself is correct; iron/wedge carries 10–46% above Tour-pro is the masked-by-Sqrt-bug lift-LUT extrapolation issue. Adversarial review (PGA TOUR 2K23 dev blog, Bearman-Harvey 1976 paper, Cornell SimScience golf physics, Quora deterministic-physics-engines, libfixmath, IronWarrior IL2CPP determinism repo) confirmed the lift LUT extrapolates Bearman-Harvey 1976 data past its valid range S∈[0.03, 0.30] — our wedges live at S≈0.45 in pure extrapolation territory.

**Single fail item (small scope-extension):** add `Aero_AllClubs_WithinTourCarryRange_PerSpinRegime` tripwire test to `Assets/Scripts/Physics/Tests/AeroCalibrationTripwireTests.cs` (NEW file). Tagged `[Ignore("Awaiting controls_e_aero_overlay_pass calibration. See ESCALATION_TO_ARCHITECT.md.")]`. Asserts each of 4 clubs (driver, iron7, iron9, pwedge) carries within ±10% of Trackman composite Tour-pro target. Test gate goes 209→210 with 209 PASS + 1 IGNORED. Full fail item details in `ARCHITECT_REVIEW.md` § "ADDENDUM — Human Architect override."

**Path to PASS:** implementer adds tripwire → self-reviewer confirms `[Ignore]` tag + message format → reviewer subagent re-runs review → Cesar approves.

## 📜 HISTORY — controls_e_aero_overlay_pass queued plan (drafted 2026-05-05, now in Active)

(Status: locked + moved to Active 2026-05-05 with all 5 open questions answered. See "NEXT" block above for current work definition. Original Queued plan preserved below for retrospective.)

**Notion:** [`35731e0e-9a36-8172-84e4-cdb4df5a0f81`](https://www.notion.so/35731e0e9a36817284e4cdb4df5a0f81) — `C.7 — Aero lift overlay calibration pass (Layer 2)` — P1 High, M (1–2 days), Order 150, Queued.

**Architecture frame (locked with Cesar 2026-05-05):** Two-layer aero model. Layer 1 = real physics (Bearman-Harvey 1976 transcription, kept faithful in published valid range S∈[0.03, 0.30]). Layer 2 = corner-case overlay (multiplicative `aero_lift_overlay.csv` applied past Bearman-Harvey valid range OR where outcomes diverge from Trackman Tour-pro reality). Smoothstep blend across S∈[0.25, 0.35] prevents seam discontinuity. Matches AAA-studio practice (PGA TOUR 2K23 dev blog: "refine the extremes"; Quora deterministic-physics consensus: "tunable for feel").

**Real-world data anchors:**
- **PRIMARY:** Trackman composite PGA Tour averages — 8 calibration clubs (driver, 3-wood, 5–9 irons, PW). Carry targets in NOTES.md table (e.g., 7-iron 172yd, 9-iron 148yd, PW 136yd). Tolerance ±10% per club.
- **CROSS-CHECK:** USGA equipment-test data on Cd/Cl as functions of S, Re. Used to verify Bearman-Harvey at low S still defensible.
- **LAYER 1 TRUTH:** Bearman-Harvey 1976. Valid range S∈[0.03, 0.30], Re∈[5e4, 2e5], v≥13 m/s.
- **TERTIARY:** Aoki 2010 / Libii 2012 (extends to supercritical Re, reverse-Magnus). NOT planned for ingestion; future option.

**Calibration loop (the meaty part):** Build `AeroCalibrationHarness.cs` (menu item, NOT a regular test). For each calibration club, run `BallSimulation.Simulate` with Trackman launch params, compute carry, compare to target. Iteratively tune `aero_lift_overlay.csv` multipliers until all clubs within ±10%. Expected 4–8 iterations, ~30–40 minutes total implementer time. Then enable the tripwire test from `controls_d` (remove `[Ignore]`). Definition-of-done: tripwire goes from IGNORED to PASS, test gate becomes 210/210.

**New deliverables:** `Docs/Physics/CALIBRATION_METHODOLOGY.md` (NEW) documents two-layer architecture + harness + "when to recalibrate" triggers. Top-of-file headers added to four Layer-2 CSVs (aero_lift_overlay, surfaces, putt + reaffirm aero_lift_lut as Layer 1).

**Out of scope (deferred):** Drag LUT audit (`controls_f_drag_calibration_audit`, P3 Queued, Notion `35731e0e-9a36-818d-9a4c-ee8dd9ca511c`) — may report "no overlay needed" since drag is implicitly co-tuned via the lift overlay. 2D Cl(S, Re) LUT — over-engineering. Aoki/Libii Layer-1 extension — future option only.

**5 open questions for Cesar (lock before SPEC writing):** (1) Trackman year, (2) 8-club vs 12-club calibration set, (3) tolerance ±5/10/15%, (4) harness UI location, (5) flat CSV vs Bezier overlay. Architect leans: most-recent year, 8-club, ±10%, dedicated menu item, flat CSV.

**Sequencing:** controls_d (Sqrt + tripwire) → controls_e (this) → Loop v1 §2a (Ball state machine). Phase A is NOT on Loop v1's critical path; Phase E is also not strictly blocking but should land before Loop v1 playtest feel.

---

## 📜 HISTORY — controls_d_velocity_cap_diagnosis Phase A (SPEC_READY → ARCHITECT_REVIEW_FAIL, 2026-05-05)

**Spec written and folder moved to Active.** `Docs/Specs/Active/controls_d_velocity_cap_diagnosis/SPEC.md` — implementer ran the full pipeline; reviewer subagent issued PASS; Cesar overrode to ESCALATE; human Architect overrode to FAIL with single tripwire-test fail item. See “NEXT” block above for current state.

**Phase A scope (this spec, locked, hardened by adversarial review 2026-05-05):** Replace the entire body of `fpMath.Sqrt` with a port of libfixmath's `fix16_sqrt` digit-by-digit shift-and-subtract algorithm (Wikipedia "Methods of computing square roots → Binary numeral system"), single-pass int64 version. New `Assets/Scripts/Physics/Tests/fpMathTests.cs` with 6 Sqrt assertions including regression guards for the captured 64 m/s and 2 m/s outputs. Re-snapshot affected EditMode tests (203 → 209 expected, with some subset of the original 203 having their expected values updated). Add a warning section at the top of `Docs/Physics/PHYSICS_TUNING_TARGETS.md` noting the carry/putt numbers were calibrated against the broken sqrt and need re-validation when convenient (not blocking).

**Adversarial review notes (2026-05-05, all in NOTES.md):** SPEC includes (a) bug analysis verified via independent Newton-Raphson convergence proof + cap-value calculation matching captured logs for both putter and driver shots; (b) algorithm decision verified against canonical libfixmath source + Wikipedia + Hacker's Delight (digit-by-digit chosen over Newton-fix and System.Math.Sqrt for structural robustness + integer-only-determinism preservation); (c) IronWarrior IL2CPP determinism tests confirmed System.Math.Sqrt is OK on iOS/Android but rejected to preserve project contract; (d) stale comment in current Sqrt body flagged as evidence of previous misdiagnosis ("loop only runs 20" — actually runs 40, but bug is structural in early-exit). Web sources used: PetteriAimonen/libfixmath, mitsuhiko/libfixmath, en.wikipedia.org/wiki/Methods_of_computing_square_roots, IronWarrior/UnityCrossPlatformDeterministicFloats.

**Architect working notes (informational):** `Docs/Specs/Active/controls_d_velocity_cap_diagnosis/NOTES.md` — full diagnosis journal + decision tree (Path A/B/B-narrow/C) + adversarial review section. Implementer reads SPEC.md as work definition; NOTES.md is context only.

**Files touched (Phase A):** `Assets/Scripts/Physics/Math/fpMath.cs` (Sqrt body only), new `Assets/Scripts/Physics/Tests/fpMathTests.cs`, subset of `Assets/Scripts/Physics/Tests/*.cs` (re-snapshot only, no logic change), new top section in `Docs/Physics/PHYSICS_TUNING_TARGETS.md`. No asmdef / scene / prefab / CSV changes.

**Phase B queued (separate Notion entry):** [`35731e0e-9a36-8132-96e4-cc27c4d2a734`](https://www.notion.so/35731e0e9a36813296e4cc27c4d2a734) — `C.6 — fpMath.Cos/Sin range-reduction repair (Phase B)`. Lands AFTER Loop v1; not on its critical path. Same Taylor accuracy bug surfaced in the same controls_c_diagnosis captures (~12% error at angles near ±π). Fix: extend ReduceAngle to [-π/2, π/2] using cos(π−x) / sin(π−x) identities. Same fpMathTests.cs file extends with Cos/Sin assertions in that phase.

**Critical risk:** the bit-exact 203/203 EditMode gate WILL break when this lands. SPEC.md Step 4 has the re-snapshot protocol. Implementer escalates as `IMPLEMENTER_BLOCKED` if any test produces NaN, Infinity, or sign-flipped values (genuine regression rather than re-snapshot territory).

**Roadmap reference:** This task is a closing follow-up under `Docs/Roadmap.md` §1 (Putter P1) cluster. Does NOT gate §2 (Loop v1) start — Loop v1 spec writing can begin in parallel. Phase B is fully off Loop v1's critical path.

---

## ✅ DONE — controls_c_fix Phase A (closed 2026-05-05)

**Spec archived:** `Docs/Specs/Completed/controls_c_fix/`. Pipeline ran end-to-end: implementer → self-reviewer (BACK_TO_IMPLEMENTER iter 1 on false-PASS lab-shot evidence) → implementer redo → reviewer (ARCHITECT_REVIEW_PASS option a) → Cesar approve. **203/203 PASS** bit-exact gate held.

**What landed:** stop-check tolerance window (`stopEpsilon = stopThresh × 0.05`) in both `RunRollPhase` + `RunPuttPhase`; `putt.csv` Green k 0.10→0.50, GreenCollar 0.14→0.40; `surfaces.csv` CartPath 0.06→0.30; 5 new EditMode tests in `RollAndPuttTuningTests.cs`. Notion entry [`35631e0e-9a36-8176-add4-e5bc40877f0f`](https://www.notion.so/35631e0e9a368176add4e5bc40877f0f) flipped to **Done**, Closed=2026-05-05.

**Two Quick follow-ups also DONE in same session:**
1. Added 4 `[ShotExit]` `DiagShotLogger` calls at `BallSimulation.cs` `RunRollPhase` / `RunPuttPhase` exits to close the diagnostic-logger gap surfaced during lab validation.
2. Baked physics-lab capture rule into `CLAUDE.md` Screenshots section: `mcp__ai-game-developer__screenshot-game-view` does NOT refresh between calls in same script-execute scope; future physics-lab specs MUST mandate `CaptureHelper.SnapAtEndOfFrameAndPause` for at-rest evidence.

**Roadmap impact:** `Docs/Roadmap.md` §1 (Putter P1) is now fully closed. §2 (Loop v1) is the next umbrella.

**Phase B queued (separate Notion entry):** [`35631e0e-9a36-8102-b217-d00dac3c3d92`](https://www.notion.so/35631e0e9a368102b217d00dac3c3d92) — `C — Fairway/Rough/etc tuning`. Fine-tuning that doesn't gate Loop v1; opens when we have observation numbers from Phase A's tests to feed it.

**Other follow-ups now actionable** (no longer blocked by Phase A):
- **C.5 — Velocity cap diagnostic** (the 64 m/s mystery). Notion [`35631e0e-9a36-8133-9734-d5b4418db9f6`](https://www.notion.so/35631e0e9a3681339734d5b4418db9f6). Diagnostic micro-spec, instrumentation only, mirrors `controls_c_diagnosis` pattern.
- **C.3 / C.4 — Surface-aware club picker rules.** Notion `35531e0e-9a36-811b-b5a6-c93e62e3ef25` and `35531e0e-9a36-81a4-9060-d1602ee11b5d`. Same surface read drives both — likely lands in one PR.

---

## ✅ DONE — Hole Selection Screen (Mac env test, off-roadmap)

**Spec archived:** `Docs/Specs/Completed/hole_selection_screen/`. STATUS=DONE; Cesar approved after Architect verdict PASS. Lomond-source GIFs OCR'd + translated mid-task; 18 hole cards rendering with real strategy text + lock/played progression service + dual reward sets.

**Carry-forward open flags** (already in OPEN FLAGS below): hole-image art is magenta placeholders for Holes 2–18; filter functionality deferred to a follow-up spec.

---

## 🚩 OPEN FLAGS — read before starting any new task

> Architect-tracked open issues. Don't action without an explicit task block; just be aware they exist.

- **[2026-04-28] Phase 2 experimental scene + assets remain in repo.** `Hole_01_Experimental_Geo.unity`, `hole-01-experimental/`, `Materials (Shared by courses)/Experimental/`, `Textures_Experimental/` — together ~60 MB. Keep as reference for next visual pass, OR delete on next cleanup spec. Cesar's call.
- **[2026-04-26] Stale comment in** `BallSimulation.cs:26` **(**`// SceneGroundProvider…`**).** SceneGroundProvider was deleted in Phase F. Hard rule 8 forbade touching `BallSimulation` during Phase F so the comment was left as-is. Trivial cleanup; not load-bearing. Closing via `HOUSEKEEPING_BALLSIM` spec.
- **[2026-04-26] `BallSimulation.DiagPerStepSink` field is now unwired.** `PhysicsLabController.WireA3DiagSinks` was removed in F.3.5. The field still exists in BallSimulation (untouched per hard rule 8) and is dead code; harmless. Closing via `HOUSEKEEPING_BALLSIM` spec.
- **[2026-04-26] Future housekeeping: consolidate `Physics.Runtime.SurfaceMarker` and `Course.SurfaceMarker` into one enum.** Bake tool currently reads two type systems (one for authoring in scene, one for the bake-side enum), bridged by `SurfaceMarkerMap`. Workable; a single-enum refactor would simplify the importers. Not blocking.
- **[2026-04-22] Don't implement Code's "trees layer" proposal.** No bug exists — `TreePlacer` doesn't add colliders, terrain trees don't intercept raycasts. Audit confirmed in lessons file.
- **[2026-04-29] capture_helper follow-on: `fake_state_populator_gate`.** PlayerContextPopulator in LabScaffold overrides fake player name. Needs a `FakeStateGate` flag across runtime populators so `GOLFIN > Capture > Fake State` presets aren't trampled. Non-blocking; surface when next capture session needs it.
- **[2026-05-01] Ball penetrates green when rolling onto it from the fairway.** Observed by Cesar during Putter P1 visual review: a ball rolling toward the green from a fairway lie visibly dips below the green surface as it crosses onto the green. Likely related to the documented memory item *putt model: green sits ~11cm above heightmap Y; putts visually roll below green surface without mesh-level correction* — but this case is a **fairway → green transition**, not a putt initiated on the green, so it may be a distinct seam/marker-snap issue at the fringe boundary rather than the standing putt-Y offset. No repro file yet; flag for investigation alongside Putter P1 caveats. Not blocking the next roadmap item, but should be triaged before Loop v1 ball-rest visuals.
- **[2026-05-02] Hole-image art is screaming-magenta placeholders for Holes 2–18.** `hole_selection_screen` task ships with 17 obvious-missing placeholders. Cesar captures real art from Lomond official website later — drop replacement PNGs in `Assets/Resources/HoleImages/Hole_NN.png` to cut over (no code change needed).
- **[2026-05-02] Filter functionality deferred.** Two filter rows on Hole Selection are visual-only. Functional filtering by Course / Tee is a follow-up spec. Counts (`28/72`, etc.) are hardcoded.
- **[2026-05-06] HUD ClubContext static-bus drift — club name doesn't update across shots.** Iter-4 §2a smoke screenshot showed `DRIVER 229 mts` in the bottom-right club pill while shot 3 was using the putter on the green. Architect-accepted as out-of-scope for §2a, but the same drift will be far more visible during Putter P2 (§2f, in-context tuning) where club switching is the explicit feature. Triage before §2f. Likely a missing populator wire or a stale `ClubContext.Raise()` at `SetCurrentClub` time. Adjacent concern: see CaptureHelper flag below — the iter-4 screenshot's evidence value is itself reduced because the SmokeTestRunner's inline RT capture has no synchronized "wait for shot lifecycle idle" gate, so we can't fully rule out "capture fired before HUD updated" as an alternative explanation for the drift. Real triage needs a deterministic capture trigger.
- **[2026-05-06] CaptureHelper asmdef consolidation + capture-timing reliability.** ~~Two related issues to fold into one follow-up.~~ **CLOSED 2026-05-07 by §2b.** `CaptureCore` factored into runtime-side `Golfin.Diagnostics.Runtime` asmdef; `SmokeTestRunner2a` inline duplicate removed; new `CaptureCore.SnapWhenStateReached(MonoBehaviour owner, BallStateMachine sm, BallState target, string label, ...)` API gates capture timing on SM state instead of frame-count or animator-IsPlaying polling. Both halves of original flag delivered in one spec.
- **[2026-05-07 09:20 JST] §2b deferred-smoke debt.** ~~Visual smoke evidence for Downrange / putter-stays-GroundLevel / OBFreeze deferred to `controls_g_aero_constant_mode_crash` closeout.~~ ~~Updated 2026-05-07 19:10 JST: NARROWED, NOT closed.~~ **CLOSED 2026-05-07 16:05 JST by `controls_g_smoke_followup`.** All 3 captures landed with dispositive runtime evidence: Downrange `[Chase, Downrange]`, Putter `[]` (no Downrange = GroundLevel preserved), OBFreeze `[Chase, Downrange, OBFreeze]` + `HitWater finalPos in lake bounds`. Director.OnModeChanged event + CaptureCore.SnapWhenModeReached late-bound API shipped. 241/241 PASS gate held. §2b umbrella now FULLY closed end-to-end (was PASS_WITH_DEFERRAL).
- **[2026-05-08 — forward design] Camera system future design captured.** After the controls_h iter-1–6 churn, architect-authored design notes at `Docs/Game Design/CAMERA_SYSTEM_FUTURE_DESIGN.md` codify (a) the single-writer rule, (b) why mid-flight cinematic cuts that release to Chase fail, (c) how shipped golf games handle cuts (pre-positioned static destination, action moves through frame, ONE allowed follow-move at end to ball-stop), (d) sketches for Apex Cam / Side Cam / Hole Overview Cam if cameras get touched again. Read this BEFORE designing any new camera mode — it's the lesson distillation from 5 failed iterations. Cesar's recommended next-camera priorities: optional Chase apex zoom-out (lowest-risk continuous enhancement), then Hole Overview Cam (lowest-risk new mode because it plays before any shot), then Apex Cam behind a default-OFF toggle. NOT a Notion-tracked task, just a forward-design reference.
- **[2026-05-07 16:05 JST] OBFreeze camera framing question — visible-water deferred.** OBFreeze capture from controls_g_smoke_followup verified locked-pivot mode fires correctly (mode history `[Chase, Downrange, OBFreeze]` + ShotExit `HitWater finalPos=(-35.08, 7.27, -1.53)` definitively in lake), but camera yaw rotation tracks ball flying AWAY from the locked pivot, orienting frame toward wooded shore instead of lake. Captured frame shows trees/path with no water visible. Architect ruled ACCEPT for §2b smoke closure (runtime evidence is dispositive), but flagged for post-Loop-v1 visual polish: should OBFreeze rotation logic keep the hazard in frame? Two options if revisited: (a) rotate to keep both ball AND hazard visible (split framing), (b) rotate toward hazard with ball offset to frame edge. Not on Loop v1 critical path. Tracker only — no Notion entry yet.

Full reasoning: `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md`.

---

## Reference Docs

- `Docs/Archive/TELLCODE_HISTORY.md` — completed task blocks + History Log (start here for anything older than current phase)
- `Docs/Specs/Queued/TEXTURE_EXPERIMENT_FINDINGS_AND_PLAN.md` — texture experiment findings + ranked future plans (mow stripe shader, macro variation, grain anisotropy, height blending, source pass v3)
- `Docs/README.md` — index map of what lives where in `Docs/`
- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/Roadmap.md` — full project roadmap (Putter P1 → Loop v1 → Loop v2 → Save → Rankings → Matchmaking → Shop → Gacha → Optimization → Polish → Server)
- `Docs/Architecture/RUNTIME_BLUEPRINT.md` — living runtime architecture reference (singletons, asmdefs, asset paths, static-bus + populator pattern)
- `Docs/Physics/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan
- `Docs/Physics/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/Physics/LESSONS_PHYSICS_AERO.md` — aero remediation lessons + future tightening options (read before touching aero LUTs)
- `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md` — surface-marker / heightmap rationale
- `Docs/Architecture/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/Architecture/UI_HIERARCHY.md` — scene UI paths reference
- `Docs/Architecture/PATTERNS.md` — recurring patterns across the codebase
- `Docs/Pipeline/ADD_HOLE.md` — end-to-end procedure for adding a new hole
- `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` — shot control v1 design (authoritative for Phase 7)
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
