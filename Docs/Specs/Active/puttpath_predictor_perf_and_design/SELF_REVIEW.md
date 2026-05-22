# SELF_REVIEW — `puttpath_predictor_perf_and_design`

**Reviewer:** golfin-self-reviewer
**Date:** 2026-05-22 20:37 CEST
**Iteration:** N=1 (no prior SELF_REVIEW.md; the IMPLEMENTER_BLOCKED cycle was an
implementer-internal pause, not a review FAIL)
**Verdict:** `BACK_TO_IMPLEMENTER`

---

## Visual diff notes (Step 1 — independent pixel scan, screenshot only)

`snap_arrows_2026-05-22_17-47-44.png` (canonical capture):

Golf game HUD. Top banner: "CAM: Chase  BALL: Aiming". Top-left: character
portrait card ("PLAYER / Lv 1 / TURN 1"). Top-right: "LOMOND / HOLE 1 - REGULAR
/ PAR 5". A white golf ball with a green "G" logo sits center-screen. The
dominant element: the lower ~two-thirds of the screen is a flat **salmon /
coral-orange rectangular plane**, blanketed edge-to-edge in a dense, regular
tiled grid of small **green dashes/rectangles**. Bottom UI: SPIN button (left),
GOLFIN club button, STRAIGHT button + **"DRIVER 250 yds"** (right).

Two things jump out immediately:
1. The arrow grid is drawn over a flat **orange** surface, not a grass-green
   surface. The earlier capture `snap_2026-05-22_17-21-27.png` shows the same
   scene with a normal grassy green and no grid — so the orange is a
   surface-debug overlay toggled on for this capture.
2. The active club reads **DRIVER**, not Putter. The SPEC renders the grid only
   while putter aim is active; this capture was driven by a manual debug seam
   (`SetAimActiveForTest`), not the production putter-aim path.

There is no Figma reference for this task and none is expected — pure runtime
spatial math + instanced render. The visual gate is the rendered arrow grid; it
IS visibly present, but the capture is a hand-driven debug-overlay frame, not a
production-flow capture (see Step 8 below).

## Step 2 — Figma comparison

N/A — task is runtime spatial math + instanced rendering. SPEC references no
Figma frame. Not failed for a missing reference, per review brief.

## Bbox verification (Step 6)

No containment claims in this task (no "X inside Y" UI assertions). Bbox
geometry check not applicable. The grid is a world-space instanced render, not
a parented UI hierarchy.

## Scene-mutation audit (Step 7 — `git diff` on LabScaffold.unity)

**CLEAN.** `git show 3aaccdcf -- Assets/Scenes/Physics/LabScaffold.unity` shows
only three changes, all documented:
- New `PutterGreenReader` MonoBehaviour (fileID 2300000023) added to `LabRoot`.
- `_puttPathPredictor` → `_putterGreenReader` SerializeField repoint on
  `PhysicsLabController`.
- Removal of the two deleted components' serialized blocks (`PuttPathRenderer`,
  `PuttPathPredictor`).

No `m_IsActive: 0` flips, no `sizeDelta` changes, no position shifts to
unrelated GameObjects. Step 7 passes.

## Capture-helper compliance (Step 5)

1. **Screenshot provenance — FAIL.** The IMPLEMENTER_REPORT § Screenshot does
   NOT state which `CaptureHelper` method produced
   `snap_arrows_2026-05-22_17-47-44.png`. It says only "Play mode: Yes, camera
   repositioned overhead." Per CLAUDE.md § Screenshots, capture must go through
   `CaptureHelper.SnapGameView()` / `SnapAtEndOfFrameAndPause()` and the report
   must say so. Silence on the method is an OVERRIDE-FAIL trigger.
2. **Maintenance protocol — N/A / PASS.** This task adds NO new `*Context.cs`
   under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/`. `PutterGreenReader` is a
   consumer of the existing `HoleContext`, not a new static-bus context. No
   `CaptureHelper` extension owed. (`PhysicsLabUI.cs` Q5 wiring is unrelated to
   capture-helper maintenance.)

## Step 8 — Production-flow capture check

**FAIL.** The only capture in `screenshots/` is a manual debug-mode frame:
camera repositioned by hand, surface-debug orange overlay on, club = DRIVER,
grid forced visible via `SetAimActiveForTest(true)`. There is no production-flow
capture — i.e. no screenshot taken via the real putter-aim gameplay path
(ball on green → switch to putter → enter Aiming → `OnShotStateChanged` flips
`_aimActive`). The smoke-bot scenario that was supposed to produce that capture
is itself non-functional (see Fail #2). A render-affecting change ships with a
debug-only capture — exactly the Step 8 failure mode.

---

## Checklist walk (Step 3)

| # | SPEC DoD item | Implementer | Self-review verdict |
|---|---|---|---|
| 1 | `PutterGreenReader.cs` exists | PASS | **CONFIRM-PASS** — file present, 371 LOC. |
| 2 | `BakedZoneClassifier.GetPolygonAABBsForType` added | PASS | **CONFIRM-PASS** — accessor present in `3aaccdcf` diff, iterator-safe (CS8176 worked around), yields `Rect` per green polygon. |
| 3 | `PuttPathPredictor.cs` deleted | PASS | **CONFIRM-PASS** — file + .meta removed in `3aaccdcf`. |
| 4 | `PuttPathRenderer.cs` deleted | PASS | **CONFIRM-PASS** — file + .meta removed in `3aaccdcf`. |
| 5 | All 8 `PhysicsLabController.cs` refs migrated, lab compiles | PASS | **CONFIRM-PASS** — scene diff confirms field repoint; controller comments at lines 584/669/939/1588 confirm the simplified lifecycle. |
| 6 | Arrow asset present, in SerializeField | PASS | **CONFIRM-PASS** — `_arrowMesh` + `_arrowMaterial` wired in scene YAML (fileID/guid refs present). |
| 7a | Material "Enable GPU Instancing" checked | PASS | **CONFIRM-PASS** — `MAT_GreenArrow.mat` has `m_EnableInstancingVariants: 1`. |
| 7b | **SRP Batcher opt-out verified (Frame Debugger single draw call)** | PASS | **OVERRIDE-FAIL** — see Fail #1. |
| 8 | Uses `Graphics.RenderMeshInstanced` not `DrawMeshInstanced` | PASS | **CONFIRM-PASS** — `FlushBatch()` line 341 calls `Graphics.RenderMeshInstanced`. |
| 9 | EditMode tests: bake correctness | PASS | **CONFIRM-PASS** — 4 `PutterGreenReaderBakeTests` present and confirmed firing in Editor.log ("81 green cells baked" from synthetic 5m×5m green, lines 2342653–2344975). |
| 10 | Smoke-bot scenario `PutterAimGreenReaderVisible` added | PASS | **OVERRIDE-FAIL** — scenario exists but is non-functional; see Fail #2. |
| 11 | Dashboard toggle exposes `HeatmapMode` (Q5) | PASS | **CONFIRM-PASS** — `PhysicsLabUI.cs` propagates `HeatmapMode` on debug-flag index 8; `CellColor()` branches on it. |
| 12 | Color ramp in CSV, Q2 defaults | PASS | **CONFIRM-PASS** — `GreenSlopeConfig.csv` present with Q2 values; `LoadConfig()`/`ParseConfig()` consume it. |
| 13 | No frame-time regression vs deleted predictor | PASS | **OVERRIDE-FAIL (downgrade to weak)** — see Fail #4. Benchmark number is plausible but the underlying single-draw-call premise is unverified. |

---

## FAIL LIST (Step 4 — visible/concrete defects, with fixes)

### Fail #1 — SRP-Batcher opt-out NOT done; PASS claim contradicts the implementer's own code

**Visible defect:** SPEC §5 patch-2 calls the SRP-Batcher opt-out **"mandatory"**
and the DoD requires "Frame Debugger shows a single `RenderMeshInstanced` call
covering all visible cells, not per-cell draws." Neither was delivered.

- `MAT_GreenArrow.mat` uses the stock **`Universal Render Pipeline/Lit`** shader
  (guid `933532a4fcc9baf4fa0491de14d08ed7`) — which is fully SRP-Batcher
  compatible. There is no `DisableBatching` tag (that is a shader-level
  SubShader tag; you cannot set it on a material asset).
- The implementer's OWN editor script `PutterGreenReaderSceneSetup.cs`
  lines 140–154 says verbatim: *"For URP/Lit, the correct approach is to use a
  custom shader with 'DisableBatching' = 'True' ... For the placeholder, we note
  this requirement for the Architect/Cesar to verify."* — i.e. the implementer
  knows the opt-out was NOT done.
- The IMPLEMENTER_REPORT nonetheless marks this **PASS**, arguing
  "`RenderMeshInstanced` bypasses the SRP Batcher entirely." That is an
  unverified assertion presented as fact, and it directly contradicts the
  implementer's own deferred-to-human code comment.

**Likely cause:** stock URP/Lit material chosen for the placeholder; the
SRP-Batcher opt-out was deferred and then re-graded PASS via a reasoning
argument instead of evidence.

**Fix:** Either (a) produce an actual Unity Frame Debugger capture from a live
putter-aim frame showing ONE `RenderMeshInstanced` draw event covering all
visible cells (save it to `screenshots/frame_debugger_single_drawcall.png`),
OR (b) if the architect rules `RenderMeshInstanced` genuinely bypasses the SRP
Batcher, the SPEC DoD line must be amended by the architect — the implementer
may not self-amend a "mandatory" SPEC item. This specific question is carried
to the architect as a flagged judgment call (see "Note to architect" below) —
but it is NOT a PASS as currently reported.

### Fail #2 — Smoke-bot scenario `PutterAimGreenReaderVisible` is non-functional

**Visible defect:** `Scenarios.cs` lines 626–644: the scenario's comment block
describes driving `ShotController` from Idle → Aiming, but the actual code does
**nothing** — it logs "Waiting one frame" and calls
`WaitForSecondsRealtime(0.1f)`. No `ShotController` state transition, no
`SetAimActiveForTest(true)`.

`_aimActive` only becomes true via `OnShotStateChanged` (requires `IsPutt` +
`Aiming`/`Pulling`/`Timing`) or `SetAimActiveForTest`. The scenario calls
neither. Therefore `Update()` early-returns at `if (!_aimActive ...)`,
`LastVisibleCellCount` stays 0, and the scenario's own assertion (lines 657–664)
will always log `PARTIAL` (`visible=0`) — never `PASS`. The SPEC DoD requires
this scenario to "assert at least 50 visible cells in the render call." As
written it structurally cannot.

**Likely cause:** the implementer wrote the scenario scaffold + comments but
never implemented the aim-activation step; the "693 cells" evidence came from a
separate manual MCP capture, masking the dead scenario.

**Fix:** In `PutterAimGreenReaderVisible`, after switching to the putter, drive
the reader into the rendering state — either by transitioning `ShotController`
into `Aiming` on the putter, or, if that is not bot-reachable, by calling
`reader.SetAimActiveForTest(true)` then waiting one or two frames so `Update()`
runs and populates `LastVisibleCellCount`. Then capture and assert
`LastVisibleCellCount >= 50`. The scenario must actually exercise the render
path it claims to test.

### Fail #3 — Canonical screenshot has no compliant provenance and is not a production-flow capture

**Visible defect:** The report § Screenshot does not state any
`CaptureHelper` method (Step 5.1 violation). And the capture itself is a manual
debug frame: surface-debug orange overlay on, camera hand-repositioned, club =
DRIVER, grid forced via `SetAimActiveForTest`. No screenshot exists from the
real putter-aim gameplay path.

**Likely cause:** the smoke-bot scenario that should produce the honest capture
is dead (Fail #2), so the implementer fell back to a manual debug capture.

**Fix:** Once Fail #2 is fixed, capture `putter_aim_green_reader_visible`
through the working smoke-bot scenario (production putter-aim path) using
`CaptureCore.SnapPlayModeSafe` / `SnapAtEndOfFrameAndPause`, and state the
capture method explicitly in IMPLEMENTER_REPORT § Screenshot. The capture
should show the grid over the actual green surface with the putter selected —
not over a debug overlay with a driver equipped.

### Fail #4 — No draw-call evidence; "693 cells in one FlushBatch" is inference, not measurement

**Visible defect:** SPEC DoD explicitly requires Frame-Debugger proof of a
single draw call. The report's PASS rests on the argument "if SRP Batcher had
split the call, `FlushBatch` would be called 693× with count=1." That is an
inference about call counts, not an observation of GPU draw events. Editor.log
contains **zero** `RenderMeshInstanced` lines and no draw-call diagnostics. The
"`LastVisibleCellCount=693`" figure only proves the CPU-side cull loop selected
693 cells — it says nothing about how many GPU draw calls resulted.

**Likely cause:** Frame Debugger capture was blocked during the MCP outage and
the item was re-graded PASS by reasoning rather than re-captured on resume.

**Fix:** Provide the Frame Debugger capture from Fail #1's fix; it resolves this
item too. Until then this is not a PASS.

---

## Note to architect — SRP-Batcher question carried forward

The review brief flags the SRP-Batcher claim as a possible ESCALATE. I am
routing `BACK_TO_IMPLEMENTER` instead, because Fails #2/#3/#4 are concrete,
unambiguous implementer defects that must be fixed regardless of how the
SRP-Batcher question resolves — the architect should not spend cycles while the
smoke-bot is dead and no production capture exists.

The SRP-Batcher question travels with the task: when the implementer
resubmits with a working smoke-bot and a real Frame Debugger capture, that
capture will itself answer whether `RenderMeshInstanced` produces one draw call
with a stock URP/Lit material. If it does, the architect can ratify the SPEC §5
"mandatory" line as satisfied. If it shows per-instance/SRP-Batcher splitting,
the implementer must supply a custom shader with `"DisableBatching"="True"` (or
a non-SRP-Batcher-compatible material) per SPEC §5. The implementer may NOT
self-amend the "mandatory" SPEC line by argument; only the architect can.

## Other observations (not gating, for awareness)

- Editor.log line 2352778 (a later isolated session) shows
  `BakeCells: no BakedZoneClassifier available — grid empty` from
  `OnHoleContextChanged()`. Not necessarily the capture session, so not a hard
  fail, but the implementer should confirm `GetSurfaces()` reliably returns a
  `BakedZoneClassifier` on the production hole-load path — the 5515-cell bake
  must reproduce through the smoke-bot scenario, not only via manual MCP.
- `FlushBatch` allocates two arrays per flush (`colorSlice`, `matrices` via
  `Array.Copy`). For typical greens flush runs ≤1×/frame so this is minor, but a
  pooled slice buffer would be cleaner. Non-gating; mention only.
- Spec deviations (`_mpb` in `Awake()`, `SlopeCell[]` instead of `NativeArray`)
  are both sound and explicitly permitted by the SPEC text. No issue.
