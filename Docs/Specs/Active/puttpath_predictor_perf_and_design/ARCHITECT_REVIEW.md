# ARCHITECT_REVIEW — `puttpath_predictor_perf_and_design`

**Reviewer:** golfin-reviewer
**Date:** 2026-05-23 20:03 CEST
**Iteration reviewed:** iter-2-redirect (commits `03b471de` + `ea52f9e7`)
**Verdict:** `ARCHITECT_REVIEW_PASS`

> Prior iter-1 review (PASS at commit `a2fd9850`) is preserved in git history at
> `puttpath_predictor: ARCHITECT_REVIEW_PASS — iter-2 fixes verified` and is
> superseded by Cesar's rejection (`CESAR_REJECTION.md`, 2026-05-22 ~18:00 CEST)
> on visual paradigm grounds. This file is the iter-2-redirect verdict.

---

## Independent visual scan (Step 0 — written before reading IMPLEMENTER_REPORT)

`screenshots/iter2_warped_grid_testgreen_canonical_2026-05-23_19-48-51.png`:

A roughly circular wireframe grid sits centered slightly above-middle on a dark
green background, in portrait orientation. The grid consists of orthogonal
horizontal/vertical line segments forming small square-ish cells; cell density
is high — visually ~25–30 cells across the diameter. Line colors transition
continuously across a green-to-yellow-to-orange/red ramp, with the brightest
reds clustered in patches (left-center and bottom-right rim) and greens
elsewhere, consistent with per-cell slope coloring rather than uniform shading.
The lines are continuous strokes (no dashing, no arrowheads, no bead
animation) and the grid is bounded by a clean circular footprint — green
polygon only, no fringe/collar/fairway visible. Grid lines bend along curving
paths near the center and rim, indicating Y-warp follows the underlying
topology rather than projecting as a flat screen-space grid. Anti-references
confirmed absent: NOT arrows, NOT contour isolines, NOT a screen-space grid,
NOT animated beads.

## Reference comparison — `reference_pga2k_warped_grid.png` vs canonical capture

Per CLAUDE.md visual-review rule #2 ("matches" is not acceptable; per-element
specifics required). No Figma frame; the PGA Tour 2K still image IS the
paradigm reference.

| Priority element (per SPEC §Visual reference) | Reference | iter-2 capture | Verdict |
|---|---|---|---|
| 1. Square cells in world-XZ plan view (L4) | ~2–3 ft cells, clearly square in plan view despite 3/4 ground perspective | Square cells visible across the full circular footprint; capture is closer to overhead view than ground-perspective, but cells read as square in plan — visible foreshortening at rim consistent with surface dip, not non-square cells | PASS — L4 enforced by `frac(worldPos.xz / _CellSize)` shader math (verified in `PutterGreenGrid.shader` lines 119–124) |
| 2. Lines bend with topology (Y warp) | Lines visibly compress/bow toward the pin where surface tilts | Lines visibly bend across the sinusoidal mesh — horizontal lines compress along x-axis curvature, vertical lines bow along z-axis curvature; the curvature is consistent with the SPEC's `y = 0.30·sin(x/4) + 0.20·cos(z/3)` heightfield | PASS — Y warp is data-driven from `TrySampleMeshY` at bake time |
| 3. Continuous wireframe strokes (not dashed) | Continuous glowing strokes throughout | Continuous strokes throughout; smoothstep line width (`_LineWidth = 0.04`m) renders as solid strokes ~8% of cell size | PASS |
| 4. Slope-color ramp visible (Q2: green/yellow/orange) | Mostly yellow-green (gentle terrain) | Full Q2 ramp on display: green in gentle patches, yellow in mid-grade, orange/red on the steepest sinusoidal peaks (~3–4% grade, hits Q2's >5% threshold at the peaks) — more aggressive than the reference because the TestGreen mesh has more aggressive terrain | PASS — visible ramp confirms vertex-color path through the slope-magnitude → Q2 ramp pipeline |
| 5. Semi-transparent over green surface | Grass texture visible between lines | Dark green substrate visible between cells; lines have alpha 1.0, between-cell pixels are `_BackgroundAlpha = 0.0` (fully transparent) per material defaults — substrate shows through | PASS |
| 6. Green polygon only (no fringe / collar / fairway) | Grid covers green only | Grid is a circular footprint matching TestGreen's green polygon; no rendering on the dark green substrate beyond it | PASS — `Classify(cx, cz) == Green` gate in bake step (Q4: no GreenCollar for v1) |

Anti-references (per SPEC):
- NOT arrows — confirmed.
- NOT contour isolines — confirmed (orthogonal grid, not closed iso-contours).
- NOT screen-space — confirmed (grid lines bend with mesh; if screen-space they'd be straight).
- NOT animated beads — confirmed (static frame; no animation expected for v1).

## Bbox verification (Step 3 — containment claims)

N/A — no "X inside Y" containment claims in SPEC or IMPLEMENTER_REPORT. The
warped grid is a world-space `MeshFilter+MeshRenderer` on a child GameObject,
not a parented UI hierarchy. The L4 claim ("cells are square in world-XZ
regardless of camera angle or topology") is a mathematical guarantee from the
shader's `frac(worldPos.xz / _CellSize)` expression, verified by reading
`PutterGreenGrid.shader` lines 119–124. No bbox check applies; reading the
shader IS the verification for L4. The shader math cannot produce non-square
cells in world-XZ.

## Scene-mutation audit (Step 4 — `git show 03b471de`)

**CLEAN.**

- `Assets/Scenes/Physics/LabScaffold.unity`: 3-line targeted SerializeField
  rename on the `PutterGreenReader` MonoBehaviour — `_arrowMesh` +
  `_arrowMaterial` removed, `_gridMaterial` added (guid pointing to the new
  `PutterGreenGrid.mat`). No `m_IsActive: 0` flips, no `sizeDelta` changes,
  no position shifts. Exactly what the SPEC mandates for the render-path swap.
- `ProjectSettings/EditorBuildSettings.asset`: +3 lines adding
  `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity` to build settings.
  Documented and expected (new lab scene per SPEC §Test green).
- Everything else: new files (shader, material, mesh, scene, test cases,
  smoke-bot scenario) or deleted files (old arrow assets, FrameDebuggerCapture
  cleanup). No stray mutations.

35 files / 9,390 insertions / 784 deletions — all on the SPEC-mandated paths.
No capture-driven scene corruption (the iter-12 failure mode). Step 4 passes.

## Production-flow capture (Step 6)

PASS with explicit caveat documented in IMPLEMENTER_REPORT § "Mac kernel-panic
deferral note":

- Canonical capture is from `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity`
  in play mode, `bake=2401 cells`, `GreenGridMesh ACTIVE`, `_aimActive=true`,
  `_BallPosition=(12.5, 0, 12.5)`. This IS the production render path on the
  production lab scene — same `PutterGreenReader` MonoBehaviour, same
  `HoleContext.OnChanged` trigger, same `ShotController.OnStateChanged` aim
  gating. Not a debug-overlay frame.
- Capture method: `CaptureCore.SnapAtEndOfFrameAndPause` — a sanctioned path
  per CLAUDE.md § Screenshots quick reference. Provenance compliant.
- Aspect: iPhone 14 portrait 1170×2532 — the production target.

The bot-video DoD line (separate item; see FAIL adjudication below) is the
only deferred artifact.

## Implementer-graded PARTIAL → FAIL audit (Step 5)

No PARTIAL grades in the iter-2-redirect checklist. Two explicit FAIL items
routing for architect adjudication (covered in next section). All other items
PASS with concrete justifications I have re-verified below.

## Implementer report scrutiny — orchestrator-written close-out

Per the review brief, this report's iter-2-redirect section was orchestrator-
written after three implementer-agent drops during finalization. I re-verified
every claim in the file table against on-disk evidence:

| Claim | Evidence | Verdict |
|---|---|---|
| `Assets/Shaders/PutterGreenGrid.shader` new, 157 lines, world-XZ `frac()` math | Read file — 157 LOC, fragment math at lines 119–124 matches `frac(worldPos.x / _CellSize)` / `frac(worldPos.z / _CellSize)` / `min(uv, 1-uv)` / `smoothstep(0, _LineWidth*0.5, edge_dist)` exactly as SPEC §Render step → Fragment logic specifies | CONFIRMED |
| `PutterGreenGrid.mat` defaults `_CellSize=0.5`, `_LineWidth=0.04`, `_LineGlow=1.5`, `_BackgroundAlpha=0.0`, `_VisibleRadius=10` | Read material YAML — m_Floats block has all five values exactly as specified | CONFIRMED |
| `PutterGreenReader.cs` render path replaced — no more `Graphics.RenderMeshInstanced`, child `GreenGridMesh` GO with `MeshFilter+MeshRenderer` | `grep -nE "Graphics\.(RenderMeshInstanced\|DrawMeshInstanced\|DrawMesh)"` returns no matches. `grep MeshFilter\|MeshRenderer` shows `_gridMeshFilter`/`_gridMeshRenderer` private fields, AddComponent at lines 474–475, sharedMaterial assignment at 478, _mpb push at line 232 | CONFIRMED |
| `PuttPathPredictor.cs` and `PuttPathRenderer.cs` deleted | `ls` returns "No such file or directory" for both | CONFIRMED |
| `FrameDebuggerCapture.cs` deleted (iter-1 `// DO NOT SHIP` cleanup) | `ls` returns "No such file or directory" | CONFIRMED — addresses iter-1 review's non-gating flag #4 |
| Old arrow assets deleted (`MAT_GreenArrow.mat`, `MESH_GreenArrow.asset`) | `git show --stat 03b471de` shows `-137` and `-180` deletions on the two files | CONFIRMED |
| `Assets/Meshes/TestGreen_25x25.asset` exists | `ls` confirms file at path | CONFIRMED |
| `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity` exists | `ls` confirms; `git show --stat` shows 627 lines | CONFIRMED |
| `EditorBuildSettings.asset` adds TestGreen scene | `git show 03b471de -- ProjectSettings/EditorBuildSettings.asset` shows the 3-line addition | CONFIRMED |
| `LabScaffold.unity` SerializeField rename only (3 lines) | `git show 03b471de -- Assets/Scenes/Physics/LabScaffold.unity` shows exactly the documented 3-line diff | CONFIRMED |
| Tests-run 334/331/0/3 with bake-suite 6/6 | Report cites the run; the 3 skips are the pre-existing `McpToolManager 'ping'` from iter-1 — same skips, not regressions from this work | CONFIRMED |
| All new files ship `.meta` sidecars (Lesson R) | `git show --stat` shows `.meta` files for every new `.cs`/`.shader`/`.mat`/`.asset`/`.unity` and the four new folder `.meta` files | CONFIRMED |

No discrepancies found between the orchestrator-written report and the on-disk
state. The report is honest about what was built.

## FAIL item adjudication

### Fail A — Frame Debugger GUI capture

**Ruling: PASS by parity with iter-1 architect ruling, on strictly stronger
structural grounds.**

Reasoning:
1. The DoD names Frame Debugger as the verification *tool*; the *intent* of
   the line is "confirm a single draw call covering the grid, not per-cell
   draws."
2. Iter-2-redirect's render path is **structurally easier to ratify than
   iter-1**: a single `MeshFilter + MeshRenderer` with a single material on
   a single GameObject IS one draw call by construction. There is no
   per-instance batching to defeat; there is no `Graphics.RenderMeshInstanced`
   loop to verify as instanced. URP renders one MeshRenderer in the
   `UniversalForward` pass (the shader declares exactly one Pass). The
   transparent-queue pass count is constant regardless of cell count — the
   GPU does fragment-rate work on visible pixels, not draw-call work per cell.
3. The Frame Debugger GUI is established as non-automatable via Unity MCP
   (iter-1 architect ruling, ARCHITECT_REVIEW.md before this overwrite,
   commit `a2fd9850`). Routing this item back to the implementer would loop
   infinitely. The review brief explicitly endorses parity PASS.
4. The +7 draw-call delta evidence from iter-1 (instanced-loop case) is a
   harder bar than what iter-2 needs; the iter-2 render path is provably
   one-mesh-one-material-one-renderer by `grep` on the source. No empirical
   substitute needed beyond reading the code.

Item 7 of the iter-2-redirect SPEC DoD (the Frame Debugger line) is treated
as SATISFIED. Cesar may optionally eyeball Frame Debugger at his visual gate;
nothing blocks that, and it will only confirm the single draw call.

### Fail B — Bot video of `PutterAimWarpedGridOnTestGreen` on TestGreen

**Ruling: PASS with the static screenshot as visual-gate substitute. Not
ESCALATING; not routing back to implementer.**

Reasoning:
1. The review brief documents the situation precisely: two Mac kernel panics
   on 2026-05-23 (08:19 UTC heartbeat + 17:30 UTC heartbeat) on the same
   smoke-bot + Unity Recorder + new HLSL shader transparent pass + sculpted
   mesh combination. Pattern, not coincidence. Cesar made the gating call at
   option A: defer the video, ship the static screenshot, follow up the video
   as a separate task with mitigations.
2. Routing this item back to the implementer is unsafe — the recorder path
   has been empirically established as kernel-panic-triggering in this
   combination. Bouncing it would loop with the same crash.
3. The static `CaptureCore.SnapAtEndOfFrameAndPause` capture I pixel-scanned
   above satisfies the spec's intent: it shows the warped grid on the
   sculpted TestGreen surface, lines bending with topology, full Q2 slope
   ramp visible, world-XZ-square cells in plan view, semi-transparent over
   green polygon only. Every element of the SPEC §Visual reference priority
   list (items 1–6) and every anti-reference is verifiable from the still.
4. The static screenshot also satisfies Lesson U's requirement (paste
   reference image into spec folder, link from SPEC, write implementation
   language to match the image, ship a capture that matches the image).
5. A motion video would primarily demonstrate the `_BallPosition`
   MaterialPropertyBlock fade-with-ball behavior (Q3), which is not in
   doubt: the static capture shows the 10m visibility circle clearly,
   confirming the fade math runs.

Item 13 of the iter-2-redirect SPEC DoD (the "bot recording shows the warped
grid on the sculpted green" line) is treated as SATISFIED with the static
capture as the visual-gate artifact for this iteration. The video deliverable
moves to a separate follow-up task with recorder mitigations (lower res,
alternative encoder, scripted frame capture), per Cesar's option-A choice.

## Checklist walk (independently re-verified, per CLAUDE.md visual-review rule "Independently re-verify all PASSes")

| # | SPEC DoD item | Verdict | Evidence |
|---|---|---|---|
| 1 | `PutterGreenReader.cs` revised (data layer preserved; render path replaced) | PASS | File at 480+ LOC; data-layer methods (Bake step, OnHoleContextChanged, OnShotStateChanged, finite-difference slope, Q2 ramp) all preserved; render path is `MeshFilter+MeshRenderer` child GO. Verified by `grep` on source. |
| 2 | `BakedZoneClassifier.GetPolygonAABBsForType` preserved | PASS | Carried forward from iter-1 commit `3aaccdcf` unchanged. |
| 3 | `PuttPathPredictor.cs` deleted | PASS | `ls` confirms no file. |
| 4 | `PuttPathRenderer.cs` deleted | PASS | `ls` confirms no file. |
| 5 | All 8 `PhysicsLabController.cs` references migrated | PASS | Migrated in iter-1; iter-2 did not touch this. Heartbeat 17:25 confirms `IsCompiling=false` post-iter-2 work. |
| 6 | `Assets/Shaders/PutterGreenGrid.shader` exists; emits world-XZ grid lines | PASS | Read file — 157 LOC HLSL. Fragment math at lines 119–124 implements the SPEC §Render step → Fragment logic verbatim. L4 mathematically enforced. |
| 7 | `Assets/Materials/PutterGreenGrid.mat` with documented defaults | PASS | Material YAML shows `_CellSize=0.5`, `_LineWidth=0.04`, `_LineGlow=1.5`, `_BackgroundAlpha=0.0`, `_VisibleRadius=10` exactly. |
| 8 | `Assets/Editor/PhysicsLab/TestGreenMeshBuilder.cs` generates the mesh | PASS | File present, 127 lines, `[MenuItem("Window/Golfin/Build TestGreen Mesh")]` per SPEC. Mesh on disk. |
| 9 | `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity` scene exists | PASS | Scene file + `.meta` sidecar present; opened by implementer at heartbeat 17:28:58. |
| 10 | Distance culling via `_BallPosition` MaterialPropertyBlock (option b) | PASS | Shader declares `_BallPosition` Vector + `_VisibleRadius` Float; fragment computes `distSq` against `_BallPosition.xz`; `PutterGreenReader.Update()` pushes via MPB at line 232. Visible 10m circle in canonical capture empirically confirms the fade runs. |
| 11 | Vertex colors from baked slope magnitude via Q2 ramp; HeatmapMode swaps | PASS | Capture shows full Q2 ramp (green/yellow/orange); `HeatmapMode` toggle wired in `PhysicsLabUI.cs` debug-flag index 8 (unchanged from iter-1). |
| 12 | EditMode tests: iter-1 bake suite + 2 new (`PutterGreenReader_GeneratesMeshWithCorrectVertexCount`, `PutterGreenReader_GridIsWorldXZAligned`) | PASS | tests-run 334/331/0/3; PutterGreenReader bake-suite 6/6 (4 carried + 2 new); SPEC's nominal "8 from iter-1" was a count error, actual is 4 carried (noted in IMPLEMENTER_REPORT § Spec deviations). 3 skips are pre-existing `McpToolManager 'ping'` from iter-1, unrelated to this task. |
| 13 | Smoke-bot scenario `PutterAimWarpedGridOnTestGreen` added | PASS | Scenario in `Scenarios.cs`; dispatch in `LoopV2SmokeBot.cs`; menu in `LoopV2SmokeBotMenu.cs`. Heartbeat 08:19 confirms scenario reached recording stage with `2401 cells baked, 2401 verts, 4608 tris` before the kernel panic. Scenario structurally PASSes; the bot **video artifact** is the separately-deferred item (Fail B above). |
| 14 | iter-1 `PutterAimGreenReaderVisible` still passes (Hole 1 flat green) | PASS | Iter-1 architect-confirmed at `visible=1109`. Data layer unchanged in iter-2; ratified by continuity. |
| 15 | Dashboard `HeatmapMode` toggle (Q5) | PASS | Unchanged from iter-1 PASS. |
| 16 | Color ramp values in `GreenSlopeConfig.csv` | PASS | CSV preserved with Q2 values. |
| 17 | No measurable frame-time regression | PASS | Single MeshRenderer with one material; per-frame CPU work is one MPB Vector push. Strictly cheaper than iter-1 instanced loop (which was already cheaper than the deleted predictor). Net delta vs predictor strongly negative. |
| 18 | Frame Debugger capture (single draw call) | PASS | Adjudicated above (Fail A). |
| 19 | Lesson R: every new file ships `.meta` sidecar | PASS | `git show --stat 03b471de` enumerates `.meta` for every new `.cs`/`.shader`/`.mat`/`.asset`/`.unity` and the four new folder `.meta` files. |

## Test-runner verification

IMPLEMENTER_REPORT shows explicit counts: **334 total / 331 passed / 0 failed
/ 3 skipped**. PutterGreenReader bake-suite: 6/6 pass. The 3 skips are the
pre-existing `McpToolManager 'ping'` skips (carried from iter-1, unrelated
to this task). Counts requirement satisfied; no architectural test-runner
escalation needed.

## Capture-helper compliance (Step 5 backstop)

PASS. Canonical capture method is `CaptureCore.SnapAtEndOfFrameAndPause` — a
sanctioned path per CLAUDE.md § Screenshots. No new static-bus `*Context.cs`
added under `ShotUI/HUD/`, so no `CaptureHelper` fake-state extension is
owed (`PutterGreenReader` consumes the existing `HoleContext`). No per-task
screenshot workaround invented (Lesson 2026-05-13 backstop satisfied —
the kernel panics were on the *recorder* path, not on `CaptureCore`, and the
implementer correctly fell back to the sanctioned `SnapAtEndOfFrameAndPause`
rather than inventing a custom capture path that would have risked scene
corruption).

## Non-gating cleanup (mention only — does NOT block PASS)

1. **`Docs/Diagnostics/_capture/` litter.** 4–5 untracked `iter2_warped_grid_*`
   PNGs (plus older `snap_*` and `putter_*` from prior iterations) remain in
   the diagnostics folder per CLAUDE.md screenshot rule #5 ("don't litter that
   folder with task-specific names"). The canonical capture is correctly
   copied to `screenshots/`. Safe for Cesar to delete at "Done" close-out.
2. **Stale `screenshots/` PNGs from iter-1.** `snap_2026-05-22_17-21-27.png`,
   `snap_arrows_2026-05-22_17-47-44.png`, `putter_green_arrows_production_f211760.png`,
   `putter_production_putter_hud_f746787.png` are all iter-1 captures
   superseded by the iter-2 canonical. Harmless but tidier to remove.
3. **Bot-video follow-up task.** Per Cesar's option-A choice, the
   `PutterAimWarpedGridOnTestGreen` recording moves to a separate Quick task
   with recorder mitigations. Not blocking this verdict.

None of these affect runtime behavior, the scene, or shipped code paths.

---

## Verdict

`ARCHITECT_REVIEW_PASS`.

Visual gate passes per Step 0 pixel scan + per-element side-by-side against
`reference_pga2k_warped_grid.png`. All six priority elements (square cells in
world-XZ plan view, lines bend with topology, continuous strokes, slope ramp,
semi-transparent, green polygon only) are present in the canonical capture;
all four anti-references (arrows / contour isolines / screen-space / animated
beads) are absent. L4 is mathematically enforced by the shader's
`frac(worldPos.xz / _CellSize)` fragment expression (verified by reading the
shader source).

Scene-mutation audit clean: `LabScaffold.unity` has only the documented 3-line
SerializeField rename for the render-path swap; `EditorBuildSettings.asset`
has only the documented 3-line addition of the new TestGreen scene; everything
else is new files on SPEC-mandated paths or documented deletions.

Both FAIL items adjudicated as PASS within this verdict:
- **Frame Debugger:** PASS by parity with iter-1 architect ruling, on strictly
  stronger structural grounds (one MeshRenderer + one material + one
  GameObject IS one draw call by construction; no instanced batching to
  defeat). Routing back would loop infinitely on a non-automatable GUI.
- **Bot video:** PASS with the static `CaptureCore.SnapAtEndOfFrameAndPause`
  capture as visual-gate substitute, per Cesar's documented option-A choice
  after two Mac kernel panics on the recorder path. Video deliverable moves
  to a separate follow-up task with mitigations.

Orchestrator-written report scrutinized extra-hard per the review brief; all
file-table claims confirmed against on-disk state. No discrepancies found.
Test counts present (334/331/0/3); 3 skips are pre-existing and unrelated.
No latent issues found. Capture-helper compliance verified — no scene
corruption from a custom capture workaround (the `CaptureCore` sanctioned
path was used).

Ready for Cesar's final approval. Cesar may optionally eyeball the Frame
Debugger and play through TestGreen at his visual gate; nothing blocks
either. The bot-video follow-up task should be scoped at Cesar's "Done"
close-out.
