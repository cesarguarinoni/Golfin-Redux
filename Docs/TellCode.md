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

**C — Controls finetuning (NEXT — gates Loop v1).** Two sub-tasks, sequenced. Both block Loop v1's ball state machine (`Rolling → AtRest`):

- **C.1** Putter velocity bug — putter shoots \~100yd instead of putt-range. Likely a stat-coupling/wiring issue (StatBundle not swapping, or `PuttBaseVelocityMps` override not respected, or power scaling math wrong for putt mode). Diagnosis-first: log what `ShotInputBuilder.Build` actually returns in putt mode.
- **C.2** Surface roll resistance — ball rolls forever regardless of surface. Either `surfaces.csv` rolling-resistance values are too low across the board, or there's a units/application bug. Diagnosis-first: fire test shots on each surface, log deceleration profiles, then re-tune CSV.
- Spec for C written after the B-followups land.

**D — Gameplay Loop v1 (Roadmap §2, items 2a–2f). Single hole, lab-launched.** No menu wiring at this stage — `LabScaffold` (or a thin variant) remains the entry point. Scope per `Docs/Roadmap.md`:

1. **2a — Ball state machine:** `Aiming → Flying → Rolling → AtRest → InCup | OB`.
2. **2b — Camera transitions:** tee → flight → rest → green → cup.
3. **2c — Turn counter + shot history** (in-memory; persistence is Loop v2).
4. **2d — Hole-complete detection + result screen** (strokes, par, score).
5. **2e — Next-shot handoff:** ball at rest → re-arm controls.
6. **2f — Putter Phase 2:** in-context tuning (the deferred half of Putter P1).

Deep-dive spec written after C lands.

**E — Gameplay Loop v2 (Roadmap §3, items 3a–3e). Menu-to-menu.** Wire the existing main menu to a Hole Picker, then to a runtime version of LabScaffold so pressing Play actually starts a hole. Scope:

1. **3a — Menu wiring:** Character → Clubs → Hole → Play.
2. **3b — Hole Picker UI** — new scene/screen accessed from main menu's Play button. Lists 18 holes with thumbnails (probably greyed-out for unimported). Selects one → loads it.
3. **Runtime hole-load equivalent of** `LabScaffold` **+** `PhysicsLabHolePicker` — today's hole-load flow is editor-only via the picker EditorWindow. Need a runtime equivalent: a `GameplayScaffold` scene (lighter than LabScaffold — no debug UI/preset Fire button) that additively loads `Hole_XX_Geo.unity`, wires `ShotController`, `BallAnimator`, `ChaseCamera`, baked providers.
4. **3c — Result screen polish** (score breakdown, optional shot-replay link).
5. **3d — Next Hole / Back to Menu transitions.**
6. **3e — Save state:** persist character/clubs/score across sessions.

- Existing assets to leverage: `Mainmenu` prefab, `ShellScene.unity`, `LabScaffold.unity` (template for `GameplayScaffold`), `PhysicsLabHolePicker` (template for runtime hole picker logic).
- **Pre-condition for closing item E:** audit all menu/inventory/roster/bags/items canvases. Confirm none are authored at `1080×1920 / Match=0.5` (the bad config that A.0 cleaned up). Any new canvases for the Hole Picker / GameplayScaffold MUST use `1170×2532 / Match=0` from the start (per Blueprint §1).
- Deep-dive spec when D is settled.

---

## ✅ Architectural state (as of 2026-04-26)

**Pivot to baked-data sim:** merged to main 2026-04-25. All tests pass (BakedPivot 24/24, Phase 1–6 physics, RealHoleTerrainTests). Cesar's "ball into void" repro eliminated by construction. Sim reads `Assets/Resources/HoleData/Hole_XX/zones.json` + `heightmap.bytes`. Scene providers demoted to editor-only placement helpers.

**Phase F cleanup:** deleted `SceneGroundProvider`, `SceneSurfaceProvider`, `PhysicsMarkerRepairTool`, `MarkerAuditTool`, 8 pre-pivot diag/agreement test files, the Phase-A `WireA3DiagSinks` harness in `PhysicsLabController`, and the stale `TERRAIN_REALTEST_FIX` Active spec. **Mid-step fix:** `Physics.Runtime.SurfaceMarker` was defined inline in the deleted `SceneSurfaceProvider.cs` — extracted to its own file (`Assets/Scripts/Physics/Runtime/SurfaceMarker.cs`) to satisfy hard rule 5 + restore importer compilation. Lesson filed (`tasks/lessons.md`: grep ALL types in a file before deleting). Test gate: **198/198 EditMode PASS, 0 failed, 0 skipped, 43.5s**. Per-step commits `phase-f.{1,1b,2,3,3.5,4,4-fix,4b,5,6}` on `main` (commits `32c73935..03744859` + lessons `8b2c82fc`).

Full history in `Docs/Archive/TELLCODE_HISTORY.md`.

---

## 📌 NEXT — Controls finetuning (item C → gates Loop v1)

**No active spec yet.** Putter P1 closed 2026-05-01; B-followups (predictor perf measurement + lab-only verification gaps + housekeeping) should run before or alongside C.

**One-line goal:** Diagnose and fix the two physics issues that block Loop v1's ball state machine — putter shoots \~100 yd instead of putt-range, and ball rolls forever regardless of surface. Diagnosis-first on both; spec written after we see the logs.

**Kickoff:** TBD — Cesar to confirm whether B-followups (predictor perf, housekeeping) run first or in parallel.

**Roadmap reference:** `Docs/Roadmap.md` §1 closed; this is the gating cleanup before §2 (Loop v1).

---

## 📌 NEXT — Matchmaking Modal (Mac env test, off-roadmap)

**Spec:** `Docs/Specs/Active/matchmaking_modal/SPEC.md` — SPEC_READY 2026-05-02.

**One-line goal:** Wire fake-matchmaking behaviour onto the existing `MatchMakingModal` prefab. Tap Home screen's Next-Hole PLAY button → modal opens, "FINDING OPPONENT…" cycles dots, opponent portrait/name/rank cycles every ~0.3 s, hole + rewards mirror the Home screen's Next Hole panel, after `searchDurationSeconds` (default 5 s) the title flips to "OPPONENT FOUND" and the opponent locks. Cancel hides the modal. No networking, no save-state changes. **Off-roadmap** — first end-to-end smoke test of the Mac dev environment; runs in parallel with item C.

**Kickoff:** `Use the golfin-implementer subagent on "matchmaking_modal"`.

**Files touched:** `CharacterThumbnailCard.cs` (one new method), `HomeScreenController.cs` (1 SerializeField + 5-line edit to `OnPlayClicked`), new `MatchmakingModalController.cs` + auto-wire, `ShellScene.unity` (controller component + inspector wiring). Prefab itself is NOT modified. Nothing physics-related is touched — item C's bugs are explicitly out-of-scope.

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
