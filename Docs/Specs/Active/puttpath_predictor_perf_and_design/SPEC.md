# SPEC — `puttpath_predictor_perf_and_design`

**Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. NOTES.md preserves the original architect scoping context.

## Status

See `STATUS.md`. **PIPELINE_READY** — Q-locks recorded in §4, best-practice patches applied in §5. Architecture DESIGN_LOCKED 2026-05-13 (NOTES.md); Q-locks from Cesar 2026-05-22; fires FULL PIPELINE on Implementer kickoff.

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
2. Single `Graphics.RenderMeshInstanced` call per frame with all visible cells. No GameObjects per cell.

**Use `Graphics.RenderMeshInstanced` (Unity 2022+), NOT `Graphics.DrawMeshInstanced`.** Project is on Unity 6 + URP 17.3.0. `RenderMeshInstanced` is the modern API; the legacy `DrawMeshInstanced` has documented per-frame ImmediateRenderer queue overhead. With proper CPU-side frustum + distance culling, `RenderMeshInstanced` does 10k+ instances in 1–3 draw calls.

**Material setup.** The arrow material must have **"Enable GPU Instancing"** checked in its inspector (URP-Lit material supports this natively). Under URP, **SRP Batcher takes precedence over GPU Instancing for the same renderer** — we want GPU Instancing here, so the arrow material must opt out of SRP Batcher (set its `DisableBatching` tag in the shader, or use a non-SRP-Batcher-compatible material). Implementer flags this during the bake-step PR for Architect to verify the material choice. Without this flag, all 3600 cells will draw individually, defeating the whole render strategy.

### Removal

Delete:
- `Assets/Scripts/Physics/Viewer/PuttPathPredictor.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs`
- All references in `PhysicsLabController.cs` (8 call-sites enumerated above) — replaced with `_putterGreenReader` SerializeField + simpler lifecycle (no `RefreshProviders` / `SetBallTransform` / `SetCamera` plumbing required; the reader pulls from `HoleContext` + a single `Camera` SerializeField).

### Test surface (smoke + EditMode)

- **EditMode:** unit tests for the bake step on a synthetic 5m × 5m green with a known constant slope; assert slope vectors point downhill and magnitude matches the height delta to within fp tolerance.
- **Smoke-bot:** new scenario `PutterAimGreenReaderVisible` — load Hole 1, enter putter aim on the green, capture screenshot, assert at least 50 visible cells in the render call (via test seam exposed on `PutterGreenReader`).

## §4 — Q-LOCKS (locked by Cesar 2026-05-22 ~14:30 CEST)

| # | Question | Lock | Notes |
|---|---|---|---|
| Q1 | Bake grid cell size. | **0.5m.** | ~3600 cells per typical green; sub-50ms main-thread bake. Tune down to 0.25m if visual density looks sparse on the first Hole 1 review. |
| Q2 | Color ramp thresholds. | **<2% green / 2–5% yellow / >5% red.** | Calibrated to USGA tournament-fast green target ~3% max. CSV-configurable defaults. |
| Q3 | Visible-cell culling distance. | **10m radius around ball + camera-frustum cull.** | Distance + frustum. |
| Q4 | GreenCollar included? | **No — Green only for v1.** | Adding GreenCollar later is a one-line `GetPolygonAABBsForType` call. |
| Q5 | Heatmap mode survives? | **Yes — free with arrow magnitude already computed.** | Toggle on the dashboard, no separate compute cost. |

## §5 — Best-practice scan (locked 2026-05-22 ~14:30 CEST)

Before committing this SPEC, Architect ran a best-practice scan against current Unity-mobile rendering + golf-game green-reading literature. Two technical additions landed in §Architecture (Render step) + one design note for the polish backlog:

1. **`Graphics.RenderMeshInstanced` (Unity 2022+) over `Graphics.DrawMeshInstanced`.** Modern API, no ImmediateRenderer queue overhead. Project is on Unity 6000.3.9f1 + URP 17.3.0, so the new API is available. **Mandatory.**
2. **GPU Instancing material flag + SRP Batcher precedence note.** Under URP, SRP Batcher takes precedence over GPU Instancing for the same renderer. The arrow material must explicitly opt out of SRP Batcher to get GPU Instancing for our 3600-cell case. Without this, the render strategy collapses to per-cell draw calls. **Mandatory.**
3. **Future polish — animated beads.** Best-practice scan confirmed PGA Tour 2K23/2K25 uses **animated beads flowing along slope-flow lines** (not static arrows) for green-reading. The grid lets us know if any slopes could alter the ball's path; directions and speed of beads tell us the direction in which the green is sloping and by how much. Static arrows match GOLFIN's Sim positioning lock (L1) and are correct for v1, but the data layer (baked slope vectors per cell) supports both renderers. A future polish ticket can swap the renderer from arrows to animated beads with zero refactor to the bake step. This is **NOT a v1 deliverable** — ship arrows first, beads later if Cesar wants the 2K-flavor polish. Tracked as out-of-scope; SPEC stays arrows for v1.

All three are additive. Architecture and Q-locks unchanged.

## Definition of done

- [ ] `Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` exists (~150 LOC)
- [ ] `BakedZoneClassifier.GetPolygonAABBsForType(SurfaceType)` accessor added (~10 LOC)
- [ ] `PuttPathPredictor.cs` deleted
- [ ] `PuttPathRenderer.cs` deleted
- [ ] All 8 `PhysicsLabController.cs` references migrated; lab compiles clean
- [ ] Arrow asset present (colorblock placeholder is acceptable for v1 per NOTES); arrow texture path in a SerializeField
- [ ] **Material configured for GPU Instancing:** "Enable GPU Instancing" checked; SRP Batcher opt-out verified (Frame Debugger shows a single `RenderMeshInstanced` call covering all visible cells, not per-cell draws)
- [ ] **Uses `Graphics.RenderMeshInstanced` (Unity 2022+), not `Graphics.DrawMeshInstanced`**
- [ ] EditMode tests: synthetic-slope bake correctness; magnitude calculation; cell-classification gating
- [ ] Smoke-bot scenario `PutterAimGreenReaderVisible` added, captures rendered grid on Hole 1
- [ ] Dashboard toggle exposes `HeatmapMode` (Q5)
- [ ] Color ramp values live in a CSV (not hardcoded), defaults per Q2
- [ ] No measurable frame-time regression vs the deleted predictor (profile capture in IMPLEMENTER_REPORT)

## Out of scope

- Per-hole green-reading authoring tools (separate task at `Docs/Specs/Queued/green_topology_and_pin_authoring/`)
- Arrow art polish (placeholder colorblock is the v1 ship)
- **Animated beads** along slope-flow lines (PGA 2K23/2K25 style) — future polish ticket, swaps the renderer over the same bake data
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
