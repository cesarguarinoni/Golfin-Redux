# ARCHITECT_REVIEW — `puttpath_predictor_perf_and_design`

**Reviewer:** golfin-reviewer
**Date:** 2026-05-22 21:55 CEST
**Iteration reviewed:** iter-2 (commit `ea1690ae`)
**Verdict:** `ARCHITECT_REVIEW_PASS`

---

## Independent visual scan (Step 0 — canonical screenshot, written before reading any report)

`screenshots/putter_production_putter_hud_f746787.png`:

The screen shows a 3D golf scene: a golf ball with a green "G" logo sitting on a
green grass surface, with a putter-style club mesh (a black/dark putter head
labeled "GOLFIN") positioned behind the ball. The green surface is tiled with a
dense, evenly-spaced grid of small white/light diamond-shaped arrow markers
covering the entire putting area — these are the predictor cells, and they read
as flat directional indicators laid on the grass. A translucent cone/aim
indicator fans out from the ball toward the camera (bottom of frame) with thin
orange edge lines bounding it. The HUD shows TURN 1, PLAYER Lv 1, LOMOND /
HOLE 1 - REGULAR / PAR 4, a "0%" power dial, and four corner buttons: SPIN,
STRAIGHT, GOLFIN, and crucially the bottom-right button reads **PUTTER** with
"0 yrds" — confirming the putter is the equipped/selected club, not a driver.

This is the production-flow capture self-review fail #3 demanded. It is NOT the
iter-1 debug frame (which the self-review described as an orange surface-debug
overlay with a DRIVER equipped). The surface here is real grass-green, the club
is PUTTER, and the arrow grid is the v1 colorblock placeholder rendered on the
real green. The earlier `putter_green_arrows_production_f211760.png` (20:53,
superseded) still showed "DRIVER 250 yds" — the canonical/later capture at 21:43
is the correct one and is the one IMPLEMENTER_REPORT § Screenshot references.

## Figma side-by-side

N/A — confirmed by the review brief. This task is pure runtime spatial math +
instanced rendering. SPEC references no Figma frame; none expected. Not failed
for a missing reference.

## Bbox verification

N/A — no containment claims in SPEC or IMPLEMENTER_REPORT. The arrow grid is a
world-space `Graphics.RenderMeshInstanced` render, not a parented UI hierarchy.
No "X inside Y" assertion to verify. Self-reviewer reached the same conclusion;
confirmed independently.

## Scene-mutation audit (`git diff` / `git show`)

**CLEAN.**

- `git show 3aaccdcf -- Assets/Scenes/Physics/LabScaffold.unity` — 37 lines, all
  documented: new `PutterGreenReader` MonoBehaviour on `LabRoot`,
  `_puttPathPredictor` → `_putterGreenReader` SerializeField repoint on
  `PhysicsLabController`, removal of the two deleted components' serialized
  blocks. No `m_IsActive: 0` flips, no `sizeDelta`, no position shifts to
  unrelated GameObjects.
- `git show --stat ea1690ae` — iter-2 did NOT touch `LabScaffold.unity` at all.
  Only `Scenarios.cs`, `PutterGreenReader.cs`, a new editor helper, docs, and
  PNGs. No scene mutation in iter-2.

No capture-driven scene corruption (the iter-12 failure mode). Step 7 passes.

## Iter-2 fix verification (re-verified from code, not from the report)

**Self-review fail #2 — dead smoke-bot scenario — FIXED, confirmed.**
`git show ea1690ae -- Scenarios.cs` replaces the dead `WaitForSecondsRealtime`
placeholder with the production path: `sc.IsPutt = true; sc.BeginExternalDrag()`.
Verified the seam methods exist and the path is real:
- `ShotController.BeginExternalDrag()` (ShotController.cs:62) calls
  `PublishState()` (line 67).
- `PublishState()` (line 346) invokes `OnStateChanged` with a `ShotInputState`
  carrying `State` and `IsPutt`.
- `PutterGreenReader.OnShotStateChanged` (line 135) sets
  `_aimActive = IsPutt && (Aiming|Pulling|Timing)`. With `IsPutt=true` and
  `BeginExternalDrag()` transitioning to `Aiming`, this genuinely flips
  `_aimActive=true`.
- The scenario then waits 3 frames so `Update()` runs and populates
  `LastVisibleCellCount`, captures, asserts `>=50`, then cleans up
  (`CancelExternalDrag`) in step 8 — AFTER the assert (the iter-1 timing bug
  where cleanup ran before the read is fixed).
This is the real render path, not scaffold/comments. The `>=50` assertion can
genuinely pass; the report's `visible=1109` is consistent with a 10m-radius cull
on a 5515-cell bake.

**Self-review fail #3 — non-compliant production screenshot — FIXED, confirmed.**
Pixel scan above: real green grass, PUTTER selected, arrow grid present.
Capture method explicitly stated in IMPLEMENTER_REPORT § Screenshot:
`CaptureCore.SnapAtEndOfFrameAndPause("putter_production_putter_hud",
skipPause: true)` — a sanctioned `CaptureCore` path. Provenance compliant.

**`_mpb` domain-reload null bug — real bug, real fix, confirmed.**
`git show ea1690ae -- PutterGreenReader.cs` adds null guards in `OnEnable()`
and `Update()`. The diagnosis is sound: domain reloads in play mode reset
managed fields without re-calling `Awake()`, so `_mpb` would be null and
`FlushBatch()` would NRE silently, pinning `LastVisibleCellCount` at 0. This
explains the iter-1 `visible=0` symptom. Legitimate correctness fix found along
the way; in scope (it is the new component's own field).

## Frame Debugger adjudication (self-review fails #1 + #4 — carried-forward judgment call)

**Ruling: programmatic draw-call evidence satisfies the DoD's intent. SPEC §5
patch-2 / DoD items 7b + 13 are treated as SATISFIED. Not escalated.**

Reasoning:

1. **The DoD named the Frame Debugger as the verification *tool*; the *intent*
   of the line is "confirm a single instanced draw call, not per-cell draws."**
   The empirical question is "did the 1109 cells draw as one instanced batch or
   as 1109 individual draws?" The Frame Debugger is one way to answer it; it is
   not the only valid evidence.

2. **The Frame Debugger GUI window is established as non-automatable.** It is a
   GUI-only Editor window; the implementer's reflection approach caused an MCP
   NRE/outage and the AppleScript menu-navigation attempt failed. The brief
   explicitly instructs: do NOT route this item back — it would be an infinite
   loop. I concur.

3. **The programmatic substitute is sound and, here, stronger than a
   screenshot.** `ProfilerRecorder` draw-call delta: 32 calls without arrows,
   39 with 1109 cells → **+7 for 1109 cells**, vs **+3327 expected if drawn
   un-instanced** — a ~475× reduction. `ceil(1109/1000)=2` instanced batches ×
   ~3.5 URP passes ≈ 7 aligns precisely with the observed delta. A measured
   integer count is harder evidence of "one instanced batch, not per-cell draws"
   than a human reading a GUI panel would be.

4. **The SRP-Batcher concern in self-review fail #1 does not apply to this API.**
   `Graphics.RenderMeshInstanced` performs explicit GPU instancing via
   `RenderParams`; it is not a `MeshRenderer`-component draw, so the SRP
   Batcher's renderer-batching pipeline never sees these submissions. The
   self-reviewer's worry — that the stock URP/Lit material lacks a
   `DisableBatching` tag — is moot: there is no `MeshRenderer` for the SRP
   Batcher to batch or fail to batch. The +7 delta is the empirical
   confirmation: if the SRP Batcher were splitting these into per-instance
   draws, the delta would be in the thousands. It is 7. The DoD's "SRP Batcher
   opt-out" line was written under the assumption the arrows would be
   `MeshRenderer`-drawn; with `RenderMeshInstanced` the opt-out is structurally
   unnecessary, and the draw-call count proves the instanced path is live.

This is a tool-substitution ruling within task scope, not a design ambiguity —
hence PASS, not ESCALATE. Cesar may still eyeball the Frame Debugger at his
visual gate if he wishes; nothing blocks that, and it would only re-confirm the
+7 measurement.

## Checklist walk (Step 3 — independently re-verified)

| # | SPEC DoD item | Verdict | Evidence |
|---|---|---|---|
| 1 | `PutterGreenReader.cs` exists | PASS | File present, 374 LOC (SPEC's "~150 LOC" is a soft estimate; growth is config/CSV/render-loop/test-seam, all in scope). |
| 2 | `BakedZoneClassifier.GetPolygonAABBsForType` added | PASS | +28 lines in `3aaccdcf` diff; yields `Rect` per green polygon. |
| 3 | `PuttPathPredictor.cs` deleted | PASS | File + .meta removed in `3aaccdcf`. |
| 4 | `PuttPathRenderer.cs` deleted | PASS | File + .meta removed in `3aaccdcf`. |
| 5 | 8 `PhysicsLabController.cs` refs migrated, lab compiles | PASS | `3aaccdcf` shows 60-line controller diff; scene field repointed; IMPLEMENTER_REPORT confirms `IsCompiling:false`. |
| 6 | Arrow asset present, in SerializeField | PASS | `_arrowMesh`/`_arrowMaterial` `[SerializeField]`; `MESH_GreenArrow`/`MAT_GreenArrow` created in `3aaccdcf`. |
| 7a | Material "Enable GPU Instancing" checked | PASS | `MAT_GreenArrow.mat` YAML `m_EnableInstancingVariants: 1`. |
| 7b | SRP Batcher opt-out / single instanced draw call | PASS | See Frame Debugger adjudication above — `RenderMeshInstanced` bypasses the SRP-Batcher renderer pipeline by construction; +7 draw-call delta for 1109 cells empirically confirms one instanced batch path, not per-cell draws. |
| 8 | Uses `Graphics.RenderMeshInstanced`, not `DrawMeshInstanced` | PASS | `FlushBatch()` line 346: `Graphics.RenderMeshInstanced(rp, _arrowMesh, 0, matrices, count)`. Confirmed in source. |
| 9 | EditMode tests: bake correctness, magnitude, gating | PASS | `PutterGreenReaderBakeTests.cs` has 4 `[Test]` methods: cell-count, downhill gradient, magnitude finite-difference (1e-4 tolerance), outside-polygon exclusion. Report cites `tests-run`: 332 total / 327 passed / 3 failed / 0 skipped — the 3 failures are pre-existing `McpToolManager 'ping'` log-error failures (SaveLayer/PlacementEntries/AllImportedHoles), unrelated to this task. |
| 10 | Smoke-bot scenario `PutterAimGreenReaderVisible` added | PASS | Re-verified from `ea1690ae` Scenarios.cs diff — drives the real `ShotController` production path (`IsPutt=true; BeginExternalDrag()`), `>=50` assertion structurally reachable, `visible=1109` reported. The iter-1 dead-scaffold defect is genuinely fixed. |
| 11 | Dashboard toggle exposes `HeatmapMode` (Q5) | PASS | `public bool HeatmapMode`; `CellColor()` branches on it; `PhysicsLabUI.cs` wires debug-flag index 8. |
| 12 | Color ramp in CSV, Q2 defaults | PASS | `GreenSlopeConfig.csv` present: `GreenThreshold,0.02` / `YellowThreshold,0.05` / `CellSize,0.5` / `VisibleRadiusMeters,10.0` — exactly Q2. |
| 13 | No frame-time regression vs deleted predictor | PASS | Idle path early-returns when `_aimActive==false`. Active path is O(cells) TRS math (report: ~0.091ms iter-1 benchmark); the deleted predictor was a live O(n) physics recompute. The single-instanced-draw premise — the only thing self-review downgraded this on — is now confirmed via item 7b. |

## Test-runner verification

IMPLEMENTER_REPORT shows explicit counts: **332 total / 327 passed / 3 failed /
0 skipped**. The 3 failures are named and attributed to a pre-existing
`McpToolManager: Tool 'ping' not found` log error affecting SaveLayer,
PlacementEntries, and AllImportedHoles tests — unrelated to this task's surface.
The 4 new `PutterGreenReaderBakeTests` are confirmed present and the report cites
their bake log (`81 green cells baked` from the synthetic 5×5 green). Test
counts requirement satisfied.

## Capture-helper compliance (Step 5 backstop)

PASS. Canonical capture used `CaptureCore.SnapAtEndOfFrameAndPause` — a
sanctioned path. No new static-bus `*Context.cs` added under
`ShotUI/HUD/`, so no `CaptureHelper` fake-state extension is owed
(`PutterGreenReader` consumes the existing `HoleContext`). Self-reviewer's
maintenance-protocol N/A finding is correct.

## Non-gating cleanup (mention only — does NOT block PASS)

1. **`_capture/` litter.** iter-2 left task-named PNGs in
   `Docs/Diagnostics/_capture/` (`putter_production_putter_hud_f746787.png`,
   `putter_green_arrows_production_f211760.png`,
   `putter_final_production_proof_f698835.png`,
   `frame_debugger_prekapture_f439665.png`,
   `putter_final_safe_2026-05-22_21-40-47.png`). CLAUDE.md screenshot rule #5
   says don't litter that folder with task-specific names — copy the keeper into
   `screenshots/` and leave `_capture/` for transient `snap_*` output. These are
   untracked working-tree files; safe for Cesar to delete.
2. **Stale `screenshots/` PNGs.** `snap_2026-05-22_17-21-27.png` and
   `snap_arrows_2026-05-22_17-47-44.png` are iter-1 debug captures superseded by
   the iter-2 production captures; can be removed.
3. **`putter_green_arrows_production_f211760.png`** in `screenshots/` is the
   superseded "DRIVER" production capture; the canonical one
   (`putter_production_putter_hud_f746787.png`) is the keeper. Harmless to leave
   but tidier to remove.
4. **`FrameDebuggerCapture.cs`** committed in `ea1690ae` carries a
   `// DO NOT SHIP — remove after review` header. It is Editor-only
   (`#if UNITY_EDITOR`) and harmless, but should be deleted once Cesar has
   finished his visual gate. Flagging so it does not get forgotten.

None of these affect runtime behavior, the scene, or shipped code paths.

---

## Verdict

`ARCHITECT_REVIEW_PASS`.

All 4 self-review fails are resolved: #2 (smoke-bot) and #3 (production capture)
verified fixed from code and pixels; #1 and #4 (Frame Debugger) adjudicated —
the programmatic `ProfilerRecorder` draw-call-delta evidence (+7 for 1109 cells,
~475× below the un-instanced baseline) sufficiently proves the DoD's intent of
"one instanced draw call, not per-cell draws," and `Graphics.RenderMeshInstanced`
bypasses the SRP-Batcher renderer pipeline by construction, making the
"opt-out" line structurally moot. The Frame Debugger GUI is established as
non-automatable; routing it back would loop infinitely. Scene-mutation audit
clean. No latent issues found (the `_mpb` domain-reload NRE was itself caught and
fixed). Test counts present and the 3 failures are pre-existing and unrelated.

Ready for Cesar's final approval. Cesar may optionally eyeball the Frame Debugger
at his visual gate; it will only re-confirm the +7 measurement.
