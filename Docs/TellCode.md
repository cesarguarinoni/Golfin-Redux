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
- **C.8** — Drag LUT calibration audit (driver carry blocker). NEXT after Cesar's signal. Driver carries −12.7% short of Trackman 275yd target at S=0.08 (Bearman-Harvey valid range — lift overlay correctly excludes by design). Suspect: Cd=0.23 floor at v≥30 m/s likely too high vs supercritical-Re golf-ball Cd ~0.18–0.22. Notion [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c), P1 Queued. Definition-of-done: removing `[Ignore]` from `Aero_Driver_KnownPending_LayerOneAudit` (gate goes 211 PASS + 0 IGNORED).
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

**`controls_f` is the natural next move:** Notion [`35731e0e-9a36-818d-9a4c-ee8dd9ca511c`](https://www.notion.so/35731e0e9a36818d9a4cee8dd9ca511c) `C.8 — Drag LUT calibration audit (driver carry blocker)` Queued, P1 High. Definition of done: removing `[Ignore]` from `Aero_Driver_KnownPending_LayerOneAudit` test (gate becomes 211 PASS + 0 IGNORED). Architect to write SPEC + NOTES when ready.

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
