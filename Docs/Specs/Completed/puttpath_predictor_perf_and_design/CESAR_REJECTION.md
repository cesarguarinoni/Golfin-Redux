# CESAR_REJECTION — `puttpath_predictor_perf_and_design`

This file logs Cesar's manual rejections after architect-pass. Each rejection
routes the task back to the implementer with STATUS = `CESAR_REJECTED`. Three
rejections recorded.

---

## Rejection 1 — iter-1 → iter-2 redirect (2026-05-22)

**Rejected verdict:** `ARCHITECT_REVIEW_PASS` (commit `a2fd9850`)
**Re-route:** Implementer iter-2 with revised SPEC

### Why rejected (summary)

Visual paradigm mismatch. Iter-1 shipped arrows on cells per the literal SPEC
text. The intended visual (L1 lock 2026-05-13: "PGA 2K style Sim positioning")
was a **warped wireframe grid** that drapes over the green surface — square in
world-XZ, bending in Y with the topology. Reference image saved as
`reference_pga2k_warped_grid.png`. Lesson U logged (`tasks/lessons.md`).

### What changed in iter-2-redirect

Data layer kept unchanged (`BakedZoneClassifier.GetPolygonAABBsForType`,
0.5m bake, finite-difference slopes, aim gating, Q3 distance cull, Q2 ramp,
Q5 heatmap, `HoleContext.OnChanged` rebake, 8-site `PhysicsLabController`
migration, EditMode bake tests). Render path swapped: arrow-instance Update()
loop → child `GreenGridMesh` GO with `MeshFilter+MeshRenderer` + new URP HLSL
shader `Assets/Shaders/PutterGreenGrid.shader` that emits world-XZ grid lines
via `frac(worldPos.xz / _CellSize)` fragment math. New
`Assets/Scenes/Physics/PhysicsLab_TestGreen.unity` with a sculpted sinusoidal
heightfield green provided the topology to validate the warp.

Resolved at commit `78945f38` (iter-2-redirect `ARCHITECT_REVIEW_PASS`).

---

## Rejection 2 — iter-2-redirect → iter-3 (2026-05-23)

**Rejected verdict:** `ARCHITECT_REVIEW_PASS` (commit `78945f38`)
**Re-route:** Implementer iter-3

### Why rejected

The reviewer-PASSed iter-2-redirect satisfies the warped-grid visual paradigm
on the synthetic `PhysicsLab_TestGreen.unity` scene (canonical capture pixel-
matches the PGA 2K reference: L4 square cells, Y warp, Q2 ramp, continuous
strokes, semi-transparent, green polygon only). Cesar approves the paradigm
but cannot approve the task until three concrete gaps close:

1. **Visible on a real production green, not just the synthetic TestGreen
   scene.** The canonical capture is on a deliberately sculpted lab scene.
   Cesar needs the grid demonstrated on a production hole's green via the
   real putter-aim gameplay path (the iter-1 architect-approved
   `PutterAimGreenReaderVisible` smoke-bot scenario on Hole 1 is the right
   vehicle). Grid appearing flat-square on a flat production green is
   expected and correct behaviour — the gate is "does it actually render in
   production flow with the new procedural-mesh render path?", not "does the
   warp show on a flat surface?".

2. **Tweakable parameters surfaced in the Inspector.** Cesar wants to tweak
   the grid look (cell size, line width, line glow, visible radius) without
   editing the shader, the material asset, or the CSV. Surface these as
   `[SerializeField]` fields on `PutterGreenReader.cs` (Inspector-editable
   per-component) and have `PutterGreenReader.Update()` push them via the
   existing `MaterialPropertyBlock` alongside `_BallPosition`. CSV remains
   the default-fallback path; the SerializeField values override CSV defaults
   at runtime when populated. This is the standard "set defaults in code,
   override per-instance in scene" Unity pattern.

3. **Bot video gate artifact, despite the kernel-panic risk.** The
   iter-2-redirect deferred the bot video per Cesar's option-A choice after
   two Mac kernel panics (heartbeat 08:19 UTC + 17:30 UTC on 2026-05-23)
   on the Unity Recorder + new HLSL shader + sculpted-mesh combo. Cesar now
   wants the video. The implementer must apply mitigations and stop early
   if the kernel-panic pattern reappears:

   - **Try low recorder settings first:** 540p (or 720p) at 30 fps. Avoid
     1170×2532 @ 60 fps which crashed twice.
   - **Try H.264 encoder, not HEVC.** macOS HEVC has documented kernel-panic
     reports under heavy load.
   - **Try producing the video on Hole 1 (real production scene) instead of
     PhysicsLab_TestGreen.** Hole 1's flat green won't show the warp but
     will validate the recorder works in this iteration's render path. If
     low-settings + H.264 + Hole 1 records cleanly, then optionally re-attempt
     on TestGreen for the warp evidence.
   - **Circuit breaker:** if any recorder attempt panics the Mac (or hangs
     Unity > 5 min without progress), set STATUS to `IMPLEMENTER_BLOCKED`,
     write a BLOCKER.md, and stop. Do NOT retry the same settings. Cesar
     can either accept the lower-fidelity video or fall back to a scripted
     frame-by-frame capture (`script-execute` + ffmpeg post-process, no
     Unity Recorder dependency).

### What's kept from iter-2-redirect (commit `03b471de`)

All iter-2-redirect work stays:
- Procedural mesh + URP HLSL shader render path (`PutterGreenReader.cs` + 
  `PutterGreenGrid.shader` + `PutterGreenGrid.mat`)
- TestGreen scene and mesh asset (kept for regression coverage on topology)
- 2 new EditMode tests added in iter-2-redirect
- Bake step + data layer (untouched since iter-1)
- The canonical iter-2-redirect screenshot in `screenshots/`

### What's added/changed in iter-3

| Item | Scope |
|---|---|
| Inspector params on `PutterGreenReader` | `[SerializeField] private float _cellSize = 0.5f;` and same pattern for `_lineWidth`, `_lineGlow`, `_visibleRadius`. Push via `MaterialPropertyBlock` in `Update()`. CSV remains fallback (load → override SerializeField if Inspector value is still the default sentinel, or just always-override CSV with SerializeField for simplicity — implementer chooses cleaner option). |
| Production-flow capture on Hole 1 | Run the iter-1 `PutterAimGreenReaderVisible` smoke-bot scenario on Hole 1 (real production path: Home → matchmaking → Hole_01_Geo). Capture via `CaptureCore.SnapAtEndOfFrameAndPause`. Save to `screenshots/iter3_warped_grid_hole1_<timestamp>.png`. The grid will be flat-square on the flat green — that's correct behaviour, not a defect. |
| Bot video | `LoopV2SmokeBot` + `BotVideoRecorder` on Hole 1 with the mitigations above (540p / 30fps / H.264 / circuit breaker on panic). Save to `videos/iter3_warped_grid_hole1_<timestamp>.mp4`. |

### Definition of redirect

Implementer reads:
1. **This file (CESAR_REJECTION.md)** — current iter-3 section
2. **`SPEC.md`** — iter-2 revision (authoritative for render-path architecture; iter-3 is additive only)
3. **`IMPLEMENTER_REPORT.md`** — iter-2-redirect close-out (most of it still applies; add iter-3 section)
4. **`ARCHITECT_REVIEW.md`** — iter-2-redirect PASS verdict (the reviewer cleared the paradigm; iter-3 adds 3 items)
5. **`tasks/lessons.md` Lesson U** — visual reference is mandatory; production-flow capture per the SPEC's Step 8

STATUS goes `ARCHITECT_REVIEW_PASS` → `CESAR_REJECTED` → next implementer run for iter-3 → `READY_FOR_SELF_REVIEW` or `READY_FOR_ARCHITECT_REVIEW` (depending on whether the video gate captures cleanly or hits the panic circuit-breaker).

### Order of work for iter-3 (recommended to minimize Mac-panic risk)

1. Add Inspector SerializeField params + MaterialPropertyBlock push (purely code; zero Unity Recorder risk).
2. Run tests-run to confirm nothing broke.
3. Capture production-flow screenshot on Hole 1 (static `CaptureCore`; no Recorder).
4. Update IMPLEMENTER_REPORT with #1 + #2 evidence.
5. **Only then** attempt the bot video with low settings (540p/30fps/H.264) on Hole 1.
6. If video #5 succeeds → done. If it panics → IMPLEMENTER_BLOCKED with the BLOCKER.md mitigation log; items #1-#4 are real progress and can ship under READY_FOR_ARCHITECT_REVIEW with the bot video FAIL routing to Cesar one more time.

---

## Rejection 3 — iter-3 → iter-4 (2026-05-25)

**Rejected verdict:** `ARCHITECT_REVIEW_PASS` (commit `8bff6bc9`)
**Re-route:** Implementer iter-4 — Z-fight defense

### Why rejected

Iter-3 closed the three iter-2 gaps cleanly (Inspector params, Hole 1 production
capture, bot video — all PASS at architect review). But Cesar's final visual
inspection (2026-05-25 ~06:00 CEST) revealed a **z-fighting defect** that the
flat Hole 1 capture and the low-res bot video both missed: the warped grid mesh
sits coplanar with the green's terrain mesh, so floating-point precision
determines which mesh wins per pixel. Result: short grid-line fragments appear
and disappear across the green; large patches clip below the terrain entirely.
Cesar captured a screenshot evidencing the defect (referenced from the SPEC's
`§Architecture §Render step` "Y-offset above terrain mesh" sub-section).

The pipeline missed it because:
- The flat Hole 1 capture had the camera high enough that perspective masked
  the fragmentation
- The 250×540 bot video resolution + 30fps smoothed over the per-pixel
  flicker
- The synthetic TestGreen sinusoidal heightfield, viewed from above in
  iter-2, also masked it (top-down angle minimizes z-fight visibility)

This is iteration 4 of the pipeline. Lessons-worth follow-up at Cesar's "Done":
add to Lesson U or sister: visual-fidelity capture for layout-affecting
rendering changes needs camera dolly coverage, not just one static frame.

### What's added in iter-4 (per SPEC `b590ebe1`)

Cesar updated `SPEC.md` at commit `b590ebe1` with a new mandatory sub-section
under `§Architecture §Render step`: **"Y-offset above terrain mesh"**. The fix:

- New `[SerializeField] float _surfaceYOffset = 0.02f;` on `PutterGreenReader`.
- Every grid-mesh vertex Y receives this offset above `SlopeCell.meshY` at
  mesh-generation time:
  `var pos = new Vector3(cell.cx, cell.meshY + _surfaceYOffset, cell.cz);`
- Default 2cm. Implementer may tune in `[0.015f, 0.03f]` based on visual gate.
- Tooltip explaining the z-fight rationale on the SerializeField (per SPEC).

A new DoD line was added to the SPEC (line 221): grid must render consistently
above terrain surface in the bot recording; **zero visible line-fragmenting
or sub-terrain clipping** across the full putter aim camera dolly range.

### What's kept from iter-3 (commit `f2edb066` + verifications at `587555c3`/`8bff6bc9`)

All iter-3 work stays:
- The 4 Inspector SerializeField params (`_cellSize`, `_lineWidth`, `_lineGlow`,
  `_visibleRadius`) — `_surfaceYOffset` is a 5th sibling field on the same component
- Hole 1 production-flow capture (will be retaken at iter-4 with the Y-offset fix)
- Bot video pipeline + mitigations (540p / 30fps / H.264 / Hole 1) — proven safe;
  re-record with the Y-offset fix in place
- The procedural-mesh + URP HLSL shader render path
- TestGreen scene + mesh asset (good for regression coverage on topology)
- 2 new EditMode tests + the bake step + the data layer

### Definition of redirect

Implementer reads:
1. **This file (CESAR_REJECTION.md)** — Rejection 3 section
2. **`SPEC.md`** — `§Architecture §Render step` "Y-offset above terrain mesh"
   sub-section (committed at `b590ebe1`) is the authoritative implementation
   guidance
3. **`IMPLEMENTER_REPORT.md`** — iter-3 section at the bottom; iter-4 will
   append below it
4. **`ARCHITECT_REVIEW.md`** — iter-3 PASS verdict (the pipeline cleared
   iter-3; iter-4 is purely additive: 1 SerializeField + 1 line in the mesh
   generation loop)
5. **`tasks/lessons.md` Lesson U** — visual-fidelity SPEC reference rule

Visual gate for iter-4: a **new bot recording** on Hole 1 (re-record the
iter-3 video with the Y-offset fix applied) showing **zero** line-fragmenting
and **zero** sub-terrain clipping at any camera angle in the putter aim dolly
range. Bot video supersedes the iter-3 video as canonical evidence. The
iter-3 video may be deleted at close-out (it shipped the same render with the
z-fight defect).

STATUS goes `ARCHITECT_REVIEW_PASS` → `CESAR_REJECTED` → next implementer
run for iter-4 → `READY_FOR_SELF_REVIEW` if the visual gate is clean.

### Order of work for iter-4

1. Add the `_surfaceYOffset` SerializeField + its Tooltip to `PutterGreenReader.cs`
   (defaults `0.02f`).
2. Apply the offset in the mesh-generation loop (one line: `cell.meshY + _surfaceYOffset`).
3. Wire the SerializeField value on the `LabScaffold.unity` AND
   `PhysicsLab_TestGreen.unity` scenes (so Cesar can tweak in Inspector).
4. Re-run `tests-run` to confirm bake tests still pass with the offset
   (mesh vertex count + plan-view alignment unchanged; only Y values shift).
5. Re-record the bot video on Hole 1 with the same mitigations (540p / 30fps /
   H.264 / Hole 1 scene) — proven safe last iter. Save to
   `videos/iter4_warped_grid_hole1_<timestamp>.mp4`.
6. Re-capture a Hole 1 production-flow screenshot at a chase-cam angle that
   would have exhibited z-fight before the fix. Save to
   `screenshots/iter4_warped_grid_hole1_<timestamp>.png`.
7. Update `IMPLEMENTER_REPORT.md` with iter-4 section: PASS for the Y-offset
   SerializeField + the no-z-fight visual gate. Cite the new screenshot + video.
8. Commit + push scoped to iter-4 paths. Set STATUS → `READY_FOR_SELF_REVIEW`.
