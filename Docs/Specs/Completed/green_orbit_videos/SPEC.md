# SPEC — 16-hole fairway-seam verification + dual orbit videos (normal + slope grid)

**Authored:** 2026-06-01 (Architect: Cesar + Claude). Sibling to `green_ship_polish` (the B1 seat/seam fix). H10/H18 are EXCLUDED here — they are the terrain-bordered greens handled on the separate collar↔terrain track.
**Kickoff:** `Use the golfin-implementer subagent on "green_orbit_videos"`
**Scope of this run (Phases 1–2 only):** verify all 16 fairway-bordered greens are clean like H7, build the slope-grid orbit bridge, and produce H7's TWO videos as the proof-of-approach. **Do NOT mass-produce the other 15 holes' videos yet** — that's Phase 3, gated on Cesar approving the H7 grid look.

## Background (facts, verified)
- The B1 merged-mesh + CDT-hole-constraint seam (in `HoleGeoImporter.cs`, Cesar-blessed) welds the collar↔**fairway** seam. H7 is the proven-clean reference.
- Per-green diagnostic (`Docs/Specs/Active/green_ship_polish/ARCHITECT_HANDOFF_TERRAIN_SEAM.md`): **16 of 18 greens are fairway-bordered** (holes 1–9, 11–17) → they get the working CDT weld. Only H10 + H18 are terrain-bordered (separate track). So all 16 here SHOULD already be clean; this run **verifies** that and produces evidence videos.
- Putting slope grid = `Golfin/PutterGreenGrid` shader + `Assets/Materials/PutterGreenGrid.mat`, currently built by `PutterGreenReader.cs` (PhysicsLab gameplay only, ball-centered, fades at `_visibleRadius` 10 m). Slope ramp colors: `Assets/Resources/Data/GreenSlopeConfig.csv`.
- Sanctioned orbit = `HoleFlyoverRecorder.RecordCurrentGreenOrbit()` via the `/green-orbit` skill (Unity Recorder, real motion). NEVER hand-roll a PNG-stitch (Rule 17 / green-orbit skill hard rule).

## Cesar's decisions (locked)
- **Grid coverage = WHOLE GREEN.** Full slope grid draped over the entire green surface, no ball fade. (Force `_VisibleRadius` large and `_BallPosition` = green center, grid active.)
- **Build the bridge.** The grid isn't wired into the `Hole_NN_Geo` orbit scenes; build a small editor helper to render it there, then record.

## Phase 1 — Verify all 16 fairway-bordered greens are clean like H7
Holes: **1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17.** (NOT 10, NOT 18.)
For each: open `Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity`, record/extract a grazing-arc orbit frame, and confirm the collar↔fairway seam is clean.
- **Objective gate:** the self-reviewer's seam metric — max bright→dark "runs per row" along the green perimeter at a **realistic lighter-pixel threshold** (NOT pure-white — the pure-white threshold falsely read H18 as 0). Clean = **≤ 3** (H7/H9/H14 baseline). Report the per-hole number in a table.
- **Plus visual:** look at each hole's perimeter at native res. The metric backstops the eyeball; it does not replace it.
- If a hole is NOT clean: it's fairway-bordered, so the CDT weld should apply — reimport that hole via the Geo importer menu and re-check. If still not clean after reimport, **flag it and STOP for that hole** — do NOT invent a new seam mechanism (green_ship_polish Hard Rule 6 carries over: no 3rd/Nth cut/weld variation; merged-mesh/escalate only).

## Phase 2 — Build the slope-grid orbit bridge + prove on H7
Build an **editor-only** helper (e.g. menu `GOLFIN/Recording/Green Slope Grid (full green)` and a `RenderFullGreenSlopeGrid()` static method callable from `script-execute`) that, in the currently-open `Hole_NN_Geo` scene:
1. Finds the green surface mesh (under the `Greens` root; green material `GreenSurface`).
2. Builds the slope-grid mesh covering the **whole green**: 0.5 m cells over the green's XZ bounds, each cell center's Y sampled from the **green mesh surface** (raycast straight down onto the green mesh — add a temporary `MeshCollider` if needed), slope = `(h_right−h_left)/cell`, `(h_back−h_front)/cell`, vertex color from the same ramp `PutterGreenReader` uses (`GreenSlopeConfig.csv`; reuse/refactor its color logic — don't reinvent the palette).
3. Applies `Assets/Materials/PutterGreenGrid.mat`, and forces full-green visibility: `_VisibleRadius` ≥ green max-extent (e.g. 999), `_BallPosition` = green center. Grid sits ~2 cm above the surface (the existing `_surfaceYOffset`) to avoid z-fight.
4. Is **non-destructive**: the grid GO is transient for recording. Do NOT save the `Hole_NN_Geo` scene with the grid GO baked in (the Generated scenes are build artifacts). Build → record → the orbit recorder's play-mode/edit-mode cycle is fine, but ensure the scene on disk is not left mutated (no `scene-save` with the grid GO).
   - Reuse `PutterGreenReader`'s mesh-build + color code where practical (extract a shared static builder, or replicate its `SlopeCell`→mesh logic sourced from the green mesh instead of `BakedZoneClassifier`). Keep the change additive; do not break PhysicsLab putt mode.

**Prove on H7:** open `Hole_07_Geo`, render the full-green grid, run the orbit recorder, and produce H7's **two** videos:
- `videos/h07_orbit_normal.mp4` — the standard green orbit (grid OFF).
- `videos/h07_orbit_grid.mp4` — the same orbit with the full-green slope grid ON.
Frame-extract BOTH at several positions and LOOK at native res before captioning (the recurring false-clean failure on this track): the grid must drape the WHOLE green, square in plan view, colored by slope; the orbit must actually move (motion gate from the green-orbit skill: r_frame_rate ≥ 30, 90°-apart pixel diff > 12).

## Deliverables (this run)
- Phase-1 verification table: 16 holes, runs-per-row (realistic threshold) + clean Y/N + 1-line visual note each.
- The grid-orbit helper code (editor-only) — compiles, non-destructive, PhysicsLab putt mode unbroken.
- H7's two videos (`h07_orbit_normal.mp4`, `h07_orbit_grid.mp4`), captioned via `Docs/Scripts/build_bot_video.py`, motion-gated.
- Canonical screenshot ≥900px: an H7 grid frame (Rule 14).
- IMPLEMENTER_REPORT content-sanity (Lesson O): describe what each H7 frame actually shows (grid coverage, slope coloring, seam).

## Definition of done (this run)
- All 16 holes verified clean (runs-per-row ≤3 + visual); any not-clean hole flagged with evidence (not masked).
- Helper builds the full-green grid in a Geo scene non-destructively; compiles; PhysicsLab unaffected.
- H7 normal + grid orbit videos produced, motion-gated, grid drapes the whole green colored by slope.
- Report + STATUS `READY_FOR_SELF_REVIEW`. (Implementer cannot mark DONE.)

## Phase 3 — ACTIVE (H7 grid look approved by Cesar 2026-06-01; trees since added to all holes)
Cesar approved the grid coloring as-is, then **added trees to all 18 holes** (placed as scene GameObjects via `Trees > Import Trees`; Generated scenes regenerated ~Jun 1 17:43). The H7 clips from Phase 2 predate the trees → **stale**. Phase 3 produces both videos for **all 16 fairway-bordered holes INCLUDING a re-record of H7**, now with trees in frame.

Holes: **1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17** (16 holes × 2 = 32 videos). NOT 10/18.

Naming (overwrite the stale H7 pair): `hNN_orbit_normal.mp4` / `hNN_orbit_grid.mp4`. Captioned via `build_bot_video.py`, motion-gated (r_frame_rate ≥ 30/1, 90°-apart pixel diff > 12) each.

### Phase-3 required fix — tree-safe slope raycast (do FIRST, before recording)
`GreenSlopeGridOrbit.RaycastGreenY` currently uses a global `UnityEngine.Physics.Raycast`. With trees now in the scene (and possibly colliders), a downward ray near a green edge can hit a tree instead of the green → corrupt/missing slope cells. **Fix:** raycast the green's own collider specifically (e.g. `tempCollider.Raycast(ray, out hit, dist)` on the temp green MeshCollider) so only the green surface is ever sampled — trees/anything else can never corrupt the grid. Re-verify the H7 grid bakes the same cell count/colors after the fix.

### Phase-3 checks
- At the start of each hole, confirm trees are actually present in the open `Hole_NN_Geo` scene (so we don't re-ship a treeless clip); flag any hole with no trees.
- The grid must still drape the whole green, slope-colored, no z-fight, seam clean — trees are background only, must NOT occlude or recolor the grid.
- Quick seam re-confirm per hole (trees don't touch the collar↔fairway seam, but verify the runs-per-row ≤3 still holds in the new tree-populated frames).
- This is a long run (32 recordings). Circuit breakers still apply per-operation; if recording stalls on a hole, flag and continue the others where possible.

## Hard rules
1. Orbit = `HoleFlyoverRecorder` / `/green-orbit` only. No PNG-stitch.
2. Grid = the existing `PutterGreenGrid` shader/material. Do not invent a new slope viz.
3. Non-destructive: do not modify `HoleGeoImporter.cs`, the green meshes, or save the Generated scenes with the grid GO. This is verification + viz + recording only.
4. Don't break PhysicsLab putt-mode grid (`PutterGreenReader`) — additive/shared refactor only.
5. Frame-extract + LOOK at native res before any "clean"/"grid visible" claim (false-clean is the named failure on this track).
6. H10/H18 are out of scope (terrain-seam track).
