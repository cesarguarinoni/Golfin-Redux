# SPEC — `puttpath_predictor_perf_and_design`

**Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. NOTES.md preserves the original architect scoping context.

## Status

See `STATUS.md`. Initial: **SPEC_READY pending Cesar Q-locks (5 questions in §4).** Architecture is DESIGN_LOCKED from NOTES.md (Cesar 2026-05-13); this SPEC fills in the implementation details on top of that.

## Goal

Replace the live trajectory-recomputation `PuttPathPredictor` with a **green-reading aim assist** that bakes slope vectors per green region on hole-load and renders an arrow grid while the player aims with the putter. PGA 2K-style Sim positioning — the player reads the green, no live predicted curve, no apex marker. The current predictor (a perf hog flagged in §2b) is **deleted**, not throttled.

## Locked decisions (from NOTES.md, Cesar 2026-05-13)

| # | Decision |
|---|---|
| L1 | **Sim positioning.** GOLFIN sits closer to PGA 2K than Everybody's Golf. Player reads the green; the game does not pre-compute the full putt path. |
| L2 | **Redesign only.** Skip the perf-throttle path. Replacing live full-trajectory recomputation with a baked + lightweight render makes the per-frame sim cost moot. |
| L3 | **Baked per-green-region on hole-load.** One-time bake when the hole loads. Slope vectors stored per-cell, sampled at draw time. Deterministic, low runtime cost, no per-aim recompute. |

The 5-option matrix from NOTES.md collapses to **option (b): pure grid + slope arrows.** No live predicted curve.

## Pre-flight findings (locked 2026-05-22 ~13:30 CEST)

| Check | Result |
|---|---|
| Current predictor location | `Assets/Scripts/Physics/Viewer/PuttPathPredictor.cs` (not `Assets/Scripts/UI/HUD/` as NOTES guessed — the file's own SPEC DEVIATION comment explains why: `Golfin.Physics.Math` is `autoReferenced:false`, so the predictor must live in an asmdef that already references it). **Keep the new component in `Golfin.Physics.Viewer` for the same reason.** |
| Companion renderer | `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs` (sister piece — also to be deleted) |
| Lab wiring sites | `PhysicsLabController.cs:402` (SerializeField), plus 7 call-sites at lines 193 / 433 / 454 / 585 / 599 / 675 / 949 / 1603 managing enable/disable, provider refresh, ball-transform sync, camera sync. **All 8 sites must be migrated to the new component.** |
| `HoleContext` event name | `HoleContext.OnChanged` (not `OnHoleLoaded` as NOTES guessed). Fires from `Raise()` and `Reset()`. **PutterGreenReader subscribes to `OnChanged` and rebuilds when `HoleNumber` changes** (track delta locally). |
| Green-region discovery API | `BakedZoneClassifier.Classify(fp x, fp z) → SurfaceType` returns the surface type at any point. Polygons are private (sealed class). **Strategy: add `GetPolygonAABBsForType(SurfaceType type)` accessor to `BakedZoneClassifier`** — minimal API extension, returns `IEnumerable<Rect>` (XZ rects from the existing `minX/maxX/minZ/maxZ` fields). Bake step iterates only cells inside green AABBs. |
| Height sampling at cell centers | `BakedZoneClassifier.TrySampleMeshY(fp x, fp z, out type, out y)` — gives the exact mesh Y. Two adjacent samples → slope vector via finite difference. |

## Architecture

### New component

`Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` — `MonoBehaviour` in `Golfin.Physics.Viewer` namespace.

Responsibilities:
1. Subscribe to `HoleContext.OnChanged`. On hole-number delta, rebuild the bake.
2. Subscribe to `ShotController.OnStateChanged`. Track whether putter aim is currently active (state = Aiming and current club is putter).
3. Cache slope vectors per cell in a flat array.
4. While putter aim is active, render visible cells via instanced quads (one draw call total).

### Bake step

Trigger: `HoleContext.OnChanged` where `HoleNumber` differs from cached.

Procedure:
1. Get the current `BakedZoneClassifier` from `PhysicsLabController.GetSurfaces()` (cast required — Lab returns `ISurfaceProvider`).
2. Call new `classifier.GetPolygonAABBsForType(SurfaceType.Green)` (also include `GreenCollar` if Q4=yes) → list of XZ rects.
3. For each rect, expand by half a cell, then iterate cells on a regular grid with cell size from Q1.
4. For each cell center (cx, cz):
   - Verify `classifier.Classify(cx, cz) == Green` (in case the rect overlaps non-green territory; AABB is a superset).
   - Sample height at 4 neighbour offsets `(±d, 0)` and `(0, ±d)` where `d = cellSize / 2`. Use `TrySampleMeshY` on each.
   - Compute slope vector: `slopeXZ = (h_right - h_left, h_back - h_front) / cellSize`. Magnitude in grade fraction (rise / run).
   - Store: `(centerX, centerZ, slopeX, slopeZ, magnitude, surfaceType)` in a flat `NativeArray<SlopeCell>` (or `Vector4[]` if NativeArray is asmdef-restricted).
5. Total cells expected: ~30m × 30m green at 0.5m cells = ~3600 cells. Main-thread bake under 50ms per Q5 lean.

### Render step

Triggered every frame while `_aimActive == true && _aimClub == Putter`.

Procedure:
1. For each cached cell within distance `Q3` of the current ball position AND in front of the camera:
   - Build a transformation matrix at the cell center (Y = sampled mesh height + small offset), rotated so the arrow texture's "forward" aligns with the slope vector direction (where the ball would roll).
   - Color from the magnitude → green/yellow/red ramp (Q2 thresholds).
2. Single `Graphics.DrawMeshInstanced` call per frame with all visible cells. No GameObjects per cell.

### Removal

Delete:
- `Assets/Scripts/Physics/Viewer/PuttPathPredictor.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs`
- All references in `PhysicsLabController.cs` (8 call-sites enumerated above) — replaced with `_putterGreenReader` SerializeField + simpler lifecycle (no `RefreshProviders` / `SetBallTransform` / `SetCamera` plumbing required; the reader pulls from `HoleContext` + a single `Camera` SerializeField).

### Test surface (smoke + EditMode)

- **EditMode:** unit tests for the bake step on a synthetic 5m × 5m green with a known constant slope; assert slope vectors point downhill and magnitude matches the height delta to within fp tolerance.
- **Smoke-bot:** new scenario `PutterAimGreenReaderVisible` — load Hole 1, enter putter aim on the green, capture screenshot, assert at least 50 visible cells in the render call (via test seam exposed on `PutterGreenReader`).

## §4 — Open questions for Cesar

| # | Question | Architect lean | Lock? |
|---|---|---|---|
| Q1 | **Cell size for the bake grid.** | **0.5m.** ~3600 cells per typical green, sub-50ms main-thread bake. Tune down to 0.25m if visual density looks sparse on the first Hole 1 review. | ☐ |
| Q2 | **Color ramp thresholds.** Where do green→yellow→red transitions sit? | **Green <2% grade, yellow 2–5%, red >5%.** Calibrated to USGA "tournament-fast" green target of ~3% max. Color values configurable in a CSV with reasonable defaults; not a per-hole config. | ☐ |
| Q3 | **Visible-cell culling distance.** | **10m radius around ball + camera-frustum cull.** Distance-only would be simpler; frustum adds 5 lines for a meaningful perf win when the player is at the back of a long green. | ☐ |
| Q4 | **GreenCollar included?** Or Green-only? | **Green only for v1.** GreenCollar is fringe transition territory; putters on the collar are an edge case. Adding GreenCollar later is a one-line `GetPolygonAABBsForType` call. | ☐ |
| Q5 | **Heatmap mode (P1 waiver carryover).** Original Putter P1 spec listed a heatmap mode that was never built. Does it survive as "tint each cell by magnitude in addition to drawing arrows"? | **Yes — free with arrow magnitude already computed.** Toggle on the dashboard, no separate compute cost. | ☐ |

All 5 are Architect-decidable during SPEC if Cesar prefers a single-pass lock. Recommend Cesar override only where the leans feel wrong.

## Definition of done

- [ ] `Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` exists (~150 LOC)
- [ ] `BakedZoneClassifier.GetPolygonAABBsForType(SurfaceType)` accessor added (~10 LOC)
- [ ] `PuttPathPredictor.cs` deleted
- [ ] `PuttPathRenderer.cs` deleted
- [ ] All 8 `PhysicsLabController.cs` references migrated; lab compiles clean
- [ ] Arrow asset present (colorblock placeholder is acceptable for v1 per NOTES); arrow texture path in a SerializeField
- [ ] EditMode tests: synthetic-slope bake correctness; magnitude calculation; cell-classification gating
- [ ] Smoke-bot scenario `PutterAimGreenReaderVisible` added, captures rendered grid on Hole 1
- [ ] Dashboard toggle exposes `HeatmapMode` (Q5)
- [ ] Color ramp values live in a CSV (not hardcoded), defaults per Q2
- [ ] No measurable frame-time regression vs the deleted predictor (profile capture in IMPLEMENTER_REPORT)

## Out of scope

- Per-hole green-reading authoring tools (separate task at `Docs/Specs/Queued/green_topology_and_pin_authoring/`)
- Arrow art polish (placeholder colorblock is the v1 ship)
- Slope simulation accuracy improvements (existing `BakedZoneClassifier.TrySampleMeshY` is the source of truth; we render whatever it gives us)
- Cross-club use of the grid (driver/iron get nothing — putter only)

## Pipeline

**FULL PIPELINE** recommended:
- New runtime spatial math (slope vector finite differences across baked mesh data) → Tier 3 per the project's spatial-math rule.
- Visual fidelity gate required (Cesar's eyes on arrow density, color ramp, render flicker).
- Touches the lab controller's putter-mode path, which has historically been touchy (Lesson Q's iteration-spiral was on this exact code area).

Estimate: 1.5–2 days for the full pipeline including review chain.

## Sequencing

After `save_layer_reactive_foundation` lands. The two SPECs touch zero overlapping files (Save layer is `Golfin.Save` asmdef + manager refactors; Green Reader is `Golfin.Physics.Viewer` asmdef + lab controller). Could in theory run parallel under two Code sessions per the Stage E/F precedent. Recommend serial for cleaner mental load — Save layer is the architecturally bigger lift, green reader is the visual win.
