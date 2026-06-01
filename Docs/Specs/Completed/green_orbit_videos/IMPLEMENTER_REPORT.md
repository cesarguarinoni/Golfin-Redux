# Implementer Report — `green_orbit_videos`

## Implementation summary

**Phase 1+2 (iter-1, prior session):** Verified all 16 fairway-bordered greens (H01–H09, H11–H17) are clean using the calibrated bottom-40% pixel-run seam metric (0 runs/row all 16 holes). Built `GreenSlopeGridOrbit.cs` — editor-only slope-grid orbit bridge using `HideFlags.DontSave` (non-destructive). Produced H7's two proof videos.

**Phase 3 (iter-2, this session):** Applied tree-safe raycast fix to `GreenSlopeGridOrbit.cs` (replaced global `Physics.Raycast` with `tempCollider.Raycast` so only the green's own MeshCollider is ever sampled — tree colliders cannot corrupt the slope bake). Verified all 16 Geo scenes have trees (all show `StandaloneTrees` GO). Recorded 32 videos (16 normal + 16 grid) for H01–H09, H11–H17 with trees in frame. All captioned via ffmpeg drawtext textfile= idiom. Motion gate: 31/32 PASS; H01 normal FAIL (diff=10.99, tree-safe symmetrical green — NOT a slideshow, discussed in FAIL items).

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Editor/Recording/GreenSlopeGridOrbit.cs` | Modified — Phase 3 tree-safe raycast fix: `RaycastGreenY` now takes `MeshCollider greenCollider` param and calls `greenCollider.Raycast()` instead of global `Physics.Raycast`. `BakeSlopeCells` updated to pass collider. Compile verified (assembly timestamp 17:56 > source 17:54; log shows only CS0618 warnings, no errors). |
| `Assets/Scripts/Editor/Recording/GreenSlopeGridOrbit.cs.meta` | Unchanged (auto-generated Unity meta file from Phase 2) |
| `Docs/Specs/Active/green_orbit_videos/videos/h01_orbit_normal.mp4` | Created — H01 normal orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h01_orbit_grid.mp4` | Created — H01 grid orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h01_orbit_normal_captioned.mp4` | Created — captioned version |
| `Docs/Specs/Active/green_orbit_videos/videos/h01_orbit_grid_captioned.mp4` | Created — captioned version |
| `Docs/Specs/Active/green_orbit_videos/videos/h02_orbit_normal.mp4` | Created — H02 normal orbit with trees, 7.9MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h02_orbit_grid.mp4` | Created — H02 grid orbit with trees, 7.9MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h02_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h02_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h03_orbit_normal.mp4` | Created — H03 normal orbit with trees, 7.6MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h03_orbit_grid.mp4` | Created — H03 grid orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h03_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h03_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h04_orbit_normal.mp4` | Created — H04 normal orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h04_orbit_grid.mp4` | Created — H04 grid orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h04_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h04_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h05_orbit_normal.mp4` | Created — H05 normal orbit with trees, 7.3MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h05_orbit_grid.mp4` | Created — H05 grid orbit with trees, 7.6MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h05_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h05_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h06_orbit_normal.mp4` | Created — H06 normal orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h06_orbit_grid.mp4` | Created — H06 grid orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h06_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h06_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h07_orbit_normal.mp4` | Overwritten — H07 Phase 3 re-record with trees (stale Phase 2 replaced), 7.5MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h07_orbit_grid.mp4` | Overwritten — H07 Phase 3 grid with trees + tree-safe grid (stale Phase 2 replaced), 7.5MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h07_orbit_normal_captioned.mp4` | Overwritten — Phase 3 caption |
| `Docs/Specs/Active/green_orbit_videos/videos/h07_orbit_grid_captioned.mp4` | Overwritten — Phase 3 caption |
| `Docs/Specs/Active/green_orbit_videos/videos/h08_orbit_normal.mp4` | Created — H08 normal orbit with trees, 7.6MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h08_orbit_grid.mp4` | Created — H08 grid orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h08_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h08_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h09_orbit_normal.mp4` | Created — H09 normal orbit with trees, 7.6MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h09_orbit_grid.mp4` | Created — H09 grid orbit with trees, 7.4MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h09_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h09_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h11_orbit_normal.mp4` | Created — H11 normal orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h11_orbit_grid.mp4` | Created — H11 grid orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h11_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h11_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h12_orbit_normal.mp4` | Created — H12 normal orbit with trees, 7.3MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h12_orbit_grid.mp4` | Created — H12 grid orbit with trees, 7.6MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h12_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h12_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h13_orbit_normal.mp4` | Created — H13 normal orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h13_orbit_grid.mp4` | Created — H13 grid orbit with trees, 7.9MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h13_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h13_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h14_orbit_normal.mp4` | Created — H14 normal orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h14_orbit_grid.mp4` | Created — H14 grid orbit with trees, 7.8MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h14_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h14_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h15_orbit_normal.mp4` | Created — H15 normal orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h15_orbit_grid.mp4` | Created — H15 grid orbit with trees, 7.9MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h15_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h15_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h16_orbit_normal.mp4` | Created — H16 normal orbit with trees, 7.9MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h16_orbit_grid.mp4` | Created — H16 grid orbit with trees, 8.0MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h16_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h16_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h17_orbit_normal.mp4` | Created — H17 normal orbit with trees, 7.6MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h17_orbit_grid.mp4` | Created — H17 grid orbit with trees, 7.7MB |
| `Docs/Specs/Active/green_orbit_videos/videos/h17_orbit_normal_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/videos/h17_orbit_grid_captioned.mp4` | Created |
| `Docs/Specs/Active/green_orbit_videos/screenshots/h07_phase3_grid_orbit_t1s.png` | Created — canonical 1920×1080 H7 Phase 3 grid frame at t=1s (trees + full grid visible) |
| `Docs/Specs/Active/green_orbit_videos/screenshots/h07_phase3_grid_orbit_t3s.png` | Created — H7 Phase 3 grid frame at t=3s (grazing arc, trees background, grid visible) |
| `Docs/Specs/Active/green_orbit_videos/screenshots/h07_phase3_grid_captioned_check.png` | Created — captioned frame verification |
| `Docs/Specs/Active/green_orbit_videos/HEARTBEAT.log` | Modified — Phase 3 baseline block + progress entries |

Pre-existing dirty paths outside the task folder (all cited from Phase 3 baseline in HEARTBEAT.log, same as iter-1):
- `Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/*.mat` (162 files, reimport churn from prior sessions)
- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` (green_ship_polish B1 CDT change, pre-existing)
- `Docs/Specs/Active/green_ship_polish/*` (green_ship_polish task files)
- `Packages/manifest.json`, `Packages/packages-lock.json` (MCP plugin bump)
- `Tools/GreenSlope/bake_report.txt`
- `Docs/Diagnostics/_capture/h07_iter8_*.jpg` (6 files, prior diagnostics)
- `Tools/GreenSlope/scripts/capture-all-holes.mjs`

## Screenshot

- **Canonical screenshot:** `screenshots/h07_phase3_grid_orbit_t3s.png`
- **Captured at:** 2026-06-01T18:07, from H7 Phase 3 grid orbit recording at t=3s (trees in scene since ~17:43)
- **Scene loaded:** `Assets/Golf/Courses/lomond-country-club/Generated/Hole_07_Geo.unity`
- **Play mode:** Yes (orbit recorder entered play mode for recording)
- **Hole loaded:** Hole 07

Canonical screenshot description: The frame shows the H7 putting green at a grazing ¾-overhead orbit angle. Dense tree line (tall conifers/deciduous) is visible in the background behind the green. The full-green slope grid drapes the entire putting surface — orange/yellow lines on the sloped right and bottom edges (indicating moderate slope 2–5%), green-tinted lines in the flat center. Red flag/pin is visible at the center-left of the green. Collar↔fairway seam transitions smoothly with no sawtooth artifacts. The grid correctly covers only the green surface (not extending into collar/fairway — tree-safe fix produces tighter coverage than Phase 2's global-raycast version).

## Acceptance checklist — Phase 3

| Item | Result | Justification |
|---|---|---|
| Step 0: Tree-safe raycast fix applied to `GreenSlopeGridOrbit.cs` | PASS | `RaycastGreenY` signature changed from `(float, float, float, out float)` to `(MeshCollider, float, float, float, out float)` — verified via reflection: `hasMeshCollider=True`. Assembly-CSharp-Editor.dll recompiled (17:56 > 17:54). Log shows only CS0618 deprecation warnings, no error CS. |
| Step 0: `greenCollider.Raycast()` used instead of `Physics.Raycast()` | PASS | Static analysis: `Physics.Raycast` string absent from file (0 occurrences). Only `greenCollider.Raycast(ray, out RaycastHit hit, ...)` used in `RaycastGreenY`. |
| Step 0: H7 grid re-bake cell count unchanged-but-tree-safe | PASS (behavior change documented) | H7 baked 2263 cells with tree-safe raycast vs 3648 cells in Phase 2 (global raycast). The reduction is CORRECT: the tree-safe raycast only hits the green mesh surface, so cells at bounding-box corners that previously hit collar/fairway now miss. This makes the grid MORE accurate (pure green surface only). Green center changed from (177.50, 28.86, -30.25) to (177.24, 28.93, -30.37) — consistent with a tighter green-only footprint. H7 grid visually identical in coverage (full green surface draped), slope coloring matches Phase 2 pattern. |
| Step 1: All 16 holes open in `Hole_NN_Geo` scenes | PASS | All 16 Geo scenes opened successfully via `EditorSceneManager.OpenScene`. None failed. |
| Step 1: Trees confirmed present in all 16 scenes | PASS | Every hole returned `trees=1 first=StandaloneTrees` from `FindObjectsOfType<GameObject>` tree-name filter. No hole has zero trees. |
| Step 1: H07 re-recorded (stale Phase 2 pair overwritten) | PASS | `h07_orbit_normal.mp4` (18:06) and `h07_orbit_grid.mp4` (18:07) have different MD5 from Phase 2 versions. Visual inspection: trees visible in both; grid visible in grid orbit. |
| Step 1: 32 videos produced (16 normal + 16 grid) | PASS | 32 .mp4 files in `videos/` (hNN_orbit_normal.mp4 + hNN_orbit_grid.mp4 for H01-H09, H11-H17). All ≥7.3MB (well above 50KB threshold). |
| Step 1: Videos captioned via textfile= idiom | PASS | All 32 captioned using `ffmpeg drawtext=textfile='...'` idiom (identical to build_bot_video.py's internal approach). Caption extraction confirms text renders at bottom strip, unobtrusive. |
| Step 1: Motion gate — r_frame_rate ≥ 30/1 | PASS | All 32 videos: `r_frame_rate=60/1` confirmed by ffprobe. |
| Step 1: Motion gate — 90°-apart pixel diff > 12 | FAIL | 31/32 PASS. H01 normal orbit FAIL: diff=10.99 (all 90°-pair positions: 8.31–11.59). H01 green is geometrically uniform (flat, no distinctive features) — camera orbits but per-pixel diff stays below threshold due to scene symmetry. Two re-records produced the same ~11.0 result. H01 GRID orbit passes (diff=13.70). See Known FAIL items. |
| Per-hole: trees present in every hole | PASS | All 16 holes: `StandaloneTrees` GO confirmed in scene before recording. Verified visually in H01, H03, H07, H08, H11, H14, H17 grid frames — tree line visible in background. |
| Per-hole: grid drapes WHOLE green, slope-colored | PASS | Visual inspection of representative holes (H01, H03, H07, H08, H11, H14, H17): grid covers full green surface, slope coloring active (orange/yellow on edges, green/flat center). Grid lines visible at all orbit positions except H08 t=3s where camera passes through a tree branch (momentary; all other positions clear). |
| Per-hole: no z-fight visible | PASS | No shimmer artifacts observed in any inspected frame. `SurfaceYOffset=0.02f` (2cm above green) prevents z-fighting. |
| Per-hole: collar↔fairway seam clean in new tree-populated frames | PASS | Seam metric re-run on all 16 Phase 3 normal orbit frames (t=1.5s, calibrated threshold each≥165, sum≥500, min_run=3): all 16 read 0 runs/row. Trees do not affect the collar↔fairway seam. |
| Per-hole: seam runs-per-row ≤3 | PASS | All 16 holes: 0 runs/row (same as Phase 1 result). |
| Per-hole: trees background only, do not occlude grid except passing | PASS (with note) | At most orbit positions, trees are background only. H08 at t=3s shows camera briefly passing through a low tree branch — the green+grid is momentarily occluded. All other H08 positions (t=1s, t=5s) show clear green+grid with trees in background. H08 grid orbit passes motion gate (diff=39.37). The occlusion is a single momentary frame in an 8-second orbit — the orbit is not a "tree-occluded" clip overall. |
| Non-destructive: Generated scenes not modified with grid GO | PASS | `HideFlags.DontSave` on grid GO ensured. Phase 3 recordings confirmed non-destructive: no `_GreenSlopeGridOrbit_Transient` in any Hole_NN_Geo.unity YAML on disk. |
| PutterGreenReader.cs unmodified (PhysicsLab unaffected) | PASS | Phase 3 only modified `GreenSlopeGridOrbit.cs`. `PutterGreenReader.cs` has 0 lines changed (verified: not in git diff). |
| HoleGeoImporter.cs NOT modified by Phase 3 | PASS | Phase 3 did not touch this file. Pre-existing green_ship_polish B1 CDT diff is from prior session (cited in baseline). |
| Canonical screenshot ≥900px (Rule 14) | PASS | `screenshots/h07_phase3_grid_orbit_t3s.png` is 1920×1080 (long edge=1920px). Grazing arc angle reveals grid coverage, trees, and seam. |
| Canonical video declared (Rule 17) | PASS | `Canonical video: \`videos/h07_orbit_grid.mp4\`` — Phase 3 H7 grid orbit showing trees + full-green slope grid. |

## 16-Hole Phase 3 Verification Table

| Hole | Trees Y/N | Seam runs/row | Normal motion gate (90° diff) | Grid motion gate (90° diff) | Normal PASS | Grid PASS |
|---|---|---|---|---|---|---|
| H01 | Y (StandaloneTrees) | 0 | 10.99 (FAIL) | 13.70 (PASS) | FAIL | PASS |
| H02 | Y (StandaloneTrees) | 0 | 21.68 (PASS) | 24.26 (PASS) | PASS | PASS |
| H03 | Y (StandaloneTrees) | 0 | 23.00 (PASS) | 25.33 (PASS) | PASS | PASS |
| H04 | Y (StandaloneTrees) | 0 | 33.31 (PASS) | 34.92 (PASS) | PASS | PASS |
| H05 | Y (StandaloneTrees) | 0 | 22.27 (PASS) | 25.61 (PASS) | PASS | PASS |
| H06 | Y (StandaloneTrees) | 0 | 17.24 (PASS) | 20.75 (PASS) | PASS | PASS |
| H07 | Y (StandaloneTrees) | 0 | 24.74 (PASS) | 26.54 (PASS) | PASS | PASS |
| H08 | Y (StandaloneTrees) | 0 | 28.47 (PASS) | 39.37 (PASS) | PASS | PASS |
| H09 | Y (StandaloneTrees) | 0 | 22.09 (PASS) | 23.49 (PASS) | PASS | PASS |
| H11 | Y (StandaloneTrees) | 0 | 23.91 (PASS) | 27.47 (PASS) | PASS | PASS |
| H12 | Y (StandaloneTrees) | 0 | 15.78 (PASS) | 18.08 (PASS) | PASS | PASS |
| H13 | Y (StandaloneTrees) | 0 | 20.26 (PASS) | 22.65 (PASS) | PASS | PASS |
| H14 | Y (StandaloneTrees) | 0 | 14.21 (PASS) | 16.46 (PASS) | PASS | PASS |
| H15 | Y (StandaloneTrees) | 0 | 15.95 (PASS) | 18.14 (PASS) | PASS | PASS |
| H16 | Y (StandaloneTrees) | 0 | 34.21 (PASS) | 34.48 (PASS) | PASS | PASS |
| H17 | Y (StandaloneTrees) | 0 | 17.65 (PASS) | 20.44 (PASS) | PASS | PASS |

## Known FAIL items

1. **H01 normal orbit motion gate FAIL (diff=10.99 < 12.0 threshold):** The H01 green is highly symmetrical and relatively featureless — the putting surface is a uniform bright green with no distinctive slope features (no bunkers near the green, very flat). The orbit IS genuinely moving (different tree positions visible at t=0 vs t=4s, different shadow angles), but the per-pixel difference stays at 10.99–11.59 for all 90°-pair positions. Two re-records produced the same result. The H01 GRID orbit passes (13.70) because the slope grid adds coloring that differentiates orbit positions. This is a structural characteristic of H01, not a recording failure or slideshow. **What would unblock:** The architect's judgment on whether diff=10.99 is acceptable for a hole with a genuinely uniform green (the threshold was designed to catch slideshows, not near-zero-slope greens). Alternatively, a longer orbit duration or a lower camera elevation could increase per-frame differences, but this would require changing `HoleFlyoverRecorder` constants and re-recording.

## Spec deviations

1. **`build_bot_video.py` not used directly for captions (same as Phase 2):** The tool requires `record_info.json` from a smoke-bot scenario; orbit recordings don't produce one. Used the identical `ffmpeg drawtext=textfile='...'` idiom that `build_bot_video.py` uses internally. Caption output is visually equivalent.

2. **Tree-safe raycast produces 2263 cells vs 3648 cells (Phase 2):** Expected per spec ("prove the fix didn't change the grid"). The fix DOES change the cell count because the global `Physics.Raycast` was hitting collar/fairway mesh cells outside the green polygon (those cells' raycasts found the collar/fairway surface). The green-specific `MeshCollider.Raycast` only finds cells actually on the putting surface mesh. This makes the grid MORE accurate (pure green surface only). Slope coloring and visual coverage of the green are unchanged. The spec's wording "Re-verify the H7 grid bakes the same cell count/colors after the fix" needs an architect judgment call: the count changed (by design) but the coloring is correct.

3. **H08 camera passes through tree branch at t=3s:** The `HoleFlyoverRecorder` orbit path is fixed (centered on green, constant radius, elevation 18°). For H08 the orbit path intersects a tree canopy at one position (~3s into the 8s orbit). All other positions show clear green+grid. This is a path/scene characteristic, not fixable without modifying the orbit recorder constants for H08 specifically.

## Console output

```
[GreenSlopeGridOrbit] Found green mesh 'Green_1' vertices=3328 transform=Green_1
[GreenSlopeGridOrbit] Baked 2263 slope cells (cellSize=0.5m).
[GreenSlopeGridOrbit] Grid GO '_GreenSlopeGridOrbit_Transient' created with 2263 vertices, greenCenter=(177.24, 28.93, -30.37), _VisibleRadius=9999. Grid is transient (DontSave) — scene will NOT be mutated on disk.
[HoleFlyoverRecorder] Green orbit: center=(177.36, 28.86, -30.44) radius=21.7m camHeight=7.1m frames=480 (8.0s @ 60fps)
[HoleFlyoverRecorder] Recording hole 07 (green orbit) → /Users/cesar/Documents/GolfinRedux/Assets/../Recordings/green-07_orbit.mp4
[HoleFlyoverRecorder] All recordings complete.
[GreenSlopeGridOrbit] warning CS0618: 'Object.FindObjectsOfType<T>()' is obsolete (×3, consistent with Phase 2 — same pre-existing warning)
(No error CS entries in compile log)
```

## Open questions for Architect

1. **H01 normal orbit motion gate borderline fail (diff=10.99):** The orbit is genuine real motion (not a slideshow). The H01 green is physically uniform with very low slope features. The H01 grid orbit passes. Is diff=10.99 acceptable for the normal orbit of a flat green, or does the strict >12 threshold require a different recording approach for H01?

2. **Tree-safe raycast cell count reduction (3648→2263 for H07):** Spec says to "re-verify the H7 grid bakes the same cell count/colors after the fix." The count changed because the fix correctly excludes collar/fairway cells. Is this acceptable, or should the spec interpretation mean the grid should cover the bounding box (including collar) regardless of mesh surface?

---

Canonical screenshot: `screenshots/h07_phase3_grid_orbit_t3s.png`
Canonical video: `videos/h07_orbit_grid.mp4`
