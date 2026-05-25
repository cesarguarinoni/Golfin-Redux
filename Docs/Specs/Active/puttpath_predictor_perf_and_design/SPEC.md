# SPEC — `puttpath_predictor_perf_and_design`

**Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. NOTES.md preserves the original architect scoping context.

## Status

See `STATUS.md`. **CESAR_REJECTED → ITER2_READY.** Iter-1 chain reached `ARCHITECT_REVIEW_PASS` at commit `a2fd9850` but the shipped arrow-grid visual missed the design lock L1 ("PGA 2K style") in paradigm — the intended visual is a warped wireframe grid that drapes over the green surface, not arrows on flat cells. See `CESAR_REJECTION.md` for the full reasoning; this SPEC is revised below for iter-2. Data layer survives intact; only the render path is being replaced. Architecture DESIGN_LOCKED 2026-05-13 (NOTES.md); Q-locks Cesar 2026-05-22; iter-2 revision Cesar 2026-05-22 ~18:00 CEST.

## Goal

Replace the live trajectory-recomputation `PuttPathPredictor` with a **green-reading aim assist** that bakes slope vectors per green region on hole-load and renders an arrow grid while the player aims with the putter. PGA 2K-style Sim positioning — the player reads the green, no live predicted curve, no apex marker. The current predictor (a perf hog flagged in §2b) is **deleted**, not throttled.

## Locked decisions (from NOTES.md, Cesar 2026-05-13)

| # | Decision |
|---|---|
| L1 | **Sim positioning.** GOLFIN sits closer to PGA 2K than Everybody's Golf. Player reads the green; the game does not pre-compute the full putt path. **Concretely: a warped wireframe grid that drapes over the green surface, like image 2 in the iter-2 reference set (see §Visual reference below).** Not arrows; not contour lines; a continuous grid where the lines themselves bend with the topology. |
| L2 | **Redesign only.** Skip the perf-throttle path. Replacing live full-trajectory recomputation with a baked + lightweight render makes the per-frame sim cost moot. |
| L3 | **Baked per-green-region on hole-load.** One-time bake when the hole loads. Slope vectors stored per-cell, sampled at draw time. Deterministic, low runtime cost, no per-aim recompute. |
| L4 (iter-2) | **Grid cells MUST be uniform squares in world-XZ.** 0.5m × 0.5m, lines parallel to world X and world Z axes. Cells deform in Y (drape with surface) but never in XZ footprint. From above, the grid reads as a perfect square checkerboard. |

## §Visual reference

**Reference image:** `reference_pga2k_warped_grid.png` (PGA Tour 2K green-reading grid, supplied by Cesar 2026-05-22 — see iter-2 reference set). Implementer should also web-search "PGA Tour 2K green grid" / "PGA Tour 2K putting aim grid" for additional reference frames.

**What the reference shows, in order of priority:**

1. A regular grid of square cells laid over the green. Lines run parallel to world X and world Z (you can see this clearly from the perspective foreshortening).
2. Lines BEND in Y to follow the surface topology — a slope going away from the camera makes the grid lines compress in screen-space; a hump in the middle of the green makes the lines bulge upward.
3. Lines are **continuous** — not dashed, not made of segments — they're glowing wireframe-style strokes.
4. Color varies along the grid (mostly yellow-green in the reference) reflecting slope intensity at each point.
5. The grid is semi-transparent so the grass texture is still visible underneath.
6. The grid covers the green polygon only — it does not extend onto fringe / collar / fairway.

**What the reference is NOT:**

- NOT arrows (iter-1 mistake)
- NOT contour lines / isolines (Mario Golf style — deliberately not the chosen paradigm)
- NOT a screen-space grid (would not deform with topology)
- NOT animated beads (PGA 2K's actual implementation — future polish, out of scope)


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

### Render step (REVISED iter-2 — warped wireframe mesh)

**Single mesh, single draw call, world-XZ grid lines drawn in the shader.**

#### Mesh generation

1. On hole-load (`HoleContext.OnChanged` → bake step completes), generate a **procedural triangulated mesh** covering all green cells.
2. Vertices: one per bake cell center. Position = `(cx, meshY + _surfaceYOffset, cz)` from the existing `SlopeCell.meshY` sample, where `_surfaceYOffset` is a `[SerializeField] float` on `PutterGreenReader` defaulting to **0.02f** (2cm). **MANDATORY** — see "Y-offset above terrain mesh" sub-section below.
3. Triangulation: each adjacent 2x2 block of green cells forms a quad → 2 triangles. Skip quads where any of the 4 corners isn't a baked green cell (handles polygon boundaries; non-square greens still produce square interior cells, only the perimeter is irregular).
4. **Vertex colors:** per-vertex color from baked slope magnitude via the Q2 ramp. Smooth interpolation across triangles gives the continuous color gradient like Mario Golf's tilt visualization — see L4 + Q2.
5. Mesh bounds: AABB of all green vertices (used for frustum culling — the GPU does it for free at this point).

#### Y-offset above terrain mesh

The grid mesh **must not be coplanar with the green's terrain mesh**. Without a Y-offset, every vertex sits at the exact terrain surface Y and the two meshes z-fight: floating-point precision determines per-pixel which mesh wins, and from typical chase-cam angles the grid renders in irregular fragments — short line pieces appear and disappear across the green, large patches clip below the terrain entirely. Cesar flagged this on the iter-1 build (2026-05-23 ~08:00 CEST; screenshot in `CESAR_REJECTION.md` thread on the iter-2 chat).

**Implementation:**
```csharp
[SerializeField, Tooltip("Vertical offset (meters) above the terrain mesh. Prevents z-fighting. 0.02 = 2cm, visually imperceptible from putter aim camera angles.")]
float _surfaceYOffset = 0.02f;

// In the mesh-generation loop:
var pos = new Vector3(cell.cx, cell.meshY + _surfaceYOffset, cell.cz);
```

**Why 2cm (and not larger):** small enough that from the putter aim camera angle (~4–5m from ball, low pitch) the grid reads as drawn ON the surface, not hovering above it. The mesh perimeter is the most sensitive view — from a side angle you'd see the offset as a thin lip if it's too large.

**Why 2cm (and not smaller):** 1cm has been observed insufficient on similar mobile-URP setups due to the depth buffer's 16-bit precision over the typical Lomond near/far plane ratio. 2cm gives reliable separation across the full camera dolly range used by `ChaseCamera.Mode.Chase` for putter aim. Implementer is free to tune in `[0.015f, 0.03f]` if iter-2 visuals show either residual z-fighting (raise) or visible hover at perimeter (lower) — the SerializeField makes this an inspector tweak, not a code change.

**What this does NOT replace:** distance culling (Q3 / `_BallPosition` MaterialPropertyBlock), frustum culling (mesh bounds), or alpha-fading at the cull radius. Y-offset only fixes the z-fight; the other culling paths stay as specified.

#### Shader (URP Shader Graph)

New asset: `Assets/Materials/PutterGreenGrid.shadergraph` (or `.hlsl` if Graph proves limiting). Single material `PutterGreenGrid.mat` references it.

**Inputs:**
- World-space position (from vertex shader, interpolated to fragment)
- Vertex color (from mesh, interpolated to fragment)
- `_CellSize` float (default 0.5m, matches bake)
- `_LineWidth` float (default 0.04, in world meters — ~8% of cell size for clean visibility)
- `_LineGlow` float (default 1.5, multiplier for line vs background brightness)
- `_BackgroundAlpha` float (default 0.0 — keep grass fully visible between lines)

**Fragment logic:**
```
uv_x = frac(worldPos.x / _CellSize)        // [0,1] within cell
uv_z = frac(worldPos.z / _CellSize)
edge_dist_x = min(uv_x, 1.0 - uv_x)        // distance to nearest X-axis grid line
edge_dist_z = min(uv_z, 1.0 - uv_z)
edge_dist  = min(edge_dist_x, edge_dist_z)
line_alpha = 1.0 - smoothstep(0, _LineWidth, edge_dist)   // 1.0 on line, 0.0 between
final_color = vertexColor.rgb * _LineGlow
final_alpha = max(line_alpha, _BackgroundAlpha)
```

The lines emerge from `frac(worldPos.xz / _CellSize)` — which means **they are world-XZ-aligned squares by mathematical construction.** You cannot get non-square cells from this shader. L4 is enforced by the math, not by discipline.

#### GameObject hierarchy

```
PutterGreenReader (existing MonoBehaviour, in ShellScene or PhysicsLab)
  └─ GreenGridMesh (child GO, created at runtime)
     ├─ MeshFilter   → procedural mesh (regenerated on hole-load)
     └─ MeshRenderer → PutterGreenGrid.mat
```

Child GO active state mirrors `_aimActive` (toggled in `OnShotStateChanged`). No per-frame `Update()` work after the mesh is built; the shader runs at fragment rate on visible pixels only, with frustum culling handled by Unity automatically via the mesh bounds.

#### Distance culling (Q3 — retained)

Q3 is still in effect (~10m radius around the ball). Two implementation choices:

- **(a) Vertex-color alpha gate:** bake a second per-vertex value = distance from a SerializeField'd "ball position transform" at render time. Shader fades grid alpha to 0 beyond 10m. Requires per-frame mesh.material update.
- **(b) MaterialPropertyBlock:** push the ball position into the material every frame via a `_BallPosition` Vector4 uniform. Shader computes `distance(worldPos.xz, _BallPosition.xz)` in fragment, fades alpha by smoothstep at 10m. No mesh rebuild.

**Choose (b).** Cheaper, no mesh churn, ball moves between shots anyway.

### Removal (REVISED iter-2)

From the iter-1 implementation, remove:
- `_arrowMesh` SerializeField
- `_arrowMaterial` SerializeField
- The `MaxBatch` / `_matBuf` / `_colorBuf` / `_colorV4Buf` per-frame instance buffers
- `FlushBatch()` helper
- The `Update()` body that builds per-cell TRS matrices and calls `Graphics.RenderMeshInstanced`
- The `_arrowMesh.asset` and `_arrowMaterial.mat` assets in the Resources/Materials folders (whichever ones iter-1 created)

Also delete (still in scope from iter-1):
- `Assets/Scripts/Physics/Viewer/PuttPathPredictor.cs`
- `Assets/Scripts/Gameplay/UI/ShotUI/PuttPathRenderer.cs`
- All 8 references in `PhysicsLabController.cs` (lines 193 / 402 / 433 / 454 / 585 / 599 / 675 / 949 / 1603)

### Test green (NEW iter-2 — mandatory for visual gate)

Current production greens at Lomond are all flat — the warped-grid visual is invisible on a flat surface (every line is straight). The visual gate requires a green with non-trivial topology.

**New asset: `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity`.** Sibling of the existing `PhysicsLab_Hole1.unity` / `PhysicsLab_Range.unity` / `PhysicsLab_Dashboard.unity` lab scenes. Contains:

- A standard `PhysicsLabController` with a single tee + cup
- A sculpted green mesh covering ~25m × 25m with deliberate elevation features:
  - Smooth sinusoidal undulation: `y = 0.30 * sin(x / 4.0) + 0.20 * cos(z / 3.0)` (peak-to-trough ~1.0m over 25m, giving 3–4% grade in spots)
  - Generated procedurally via a new editor utility `TestGreenMeshBuilder.cs` (`Assets/Editor/PhysicsLab/`) under menu `Window/Golfin/Build TestGreen Mesh`
  - Output: a `Mesh` asset saved to `Assets/Meshes/TestGreen_25x25.asset`
  - Triangulated on a 0.25m XZ grid (denser than the 0.5m bake so the surface is smooth, not faceted)
- `BakedZoneClassifier` configured to classify the mesh's XZ footprint as `SurfaceType.Green`
- Camera positioned for a 3/4 overhead view of the green

This scene IS the visual gate. The Implementer must capture screenshots of the warped grid on this scene and include them in IMPLEMENTER_REPORT. Production greens (Hole 1–18) capture as a sanity pass — grid should appear flat-square there because the topology is flat, which is correct behavior, not a bug.

### Test surface (smoke + EditMode) — REVISED iter-2

- **EditMode (UNCHANGED FROM ITER-1):** unit tests for the bake step on a synthetic 5m × 5m green with a known constant slope; assert slope vectors point downhill and magnitude matches the height delta to within fp tolerance. All 8 iter-1 tests survive.
- **EditMode (NEW):** `PutterGreenReader_GeneratesMeshWithCorrectVertexCount` — inject a synthetic 10x10 baked-cell grid; assert generated mesh has exactly the expected vertex + triangle count; assert mesh bounds match XZ extent; assert vertex colors at least 3 distinct values across the slope range.
- **EditMode (NEW):** `PutterGreenReader_GridIsWorldXZAligned` — generate a mesh on a synthetic slope; assert that vertex XZ positions form a regular grid (`(v.x % cellSize) approx 0` and same for z within fp tolerance). Enforces L4 in code.
- **Smoke-bot (REVISED):** new scenario `PutterAimWarpedGridOnTestGreen` — load `PhysicsLab_TestGreen.unity`, enter putter aim, capture screenshot showing the warped grid on the sculpted surface. Visual gate: lines should visibly bend over the humps/swales; grid should remain square-in-plan-view. Bot recording is the canonical visual gate artifact.
- **Smoke-bot (RETAINED):** iter-1's `PutterAimGreenReaderVisible` on Hole 1 still runs as a regression sanity — grid appears flat-square because Hole 1 green is flat. Not a failure.

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

## Definition of done (REVISED iter-2)

- [ ] `Assets/Scripts/Physics/Viewer/PutterGreenReader.cs` revised (data layer + bake step preserved; render path replaced with procedural mesh + child MeshFilter+MeshRenderer)
- [ ] **NEW: `_surfaceYOffset` SerializeField on `PutterGreenReader`** (default `0.02f`, tooltip explaining the z-fight defense) — every mesh vertex Y receives this offset above `SlopeCell.meshY` at mesh-generation time. Grid must render consistently above terrain surface in the bot recording; **zero visible line-fragmenting or sub-terrain clipping** across the full putter aim camera dolly range.
- [ ] `BakedZoneClassifier.GetPolygonAABBsForType(SurfaceType)` accessor preserved (unchanged from iter-1)
- [ ] `PuttPathPredictor.cs` deleted
- [ ] `PuttPathRenderer.cs` deleted
- [ ] All 8 `PhysicsLabController.cs` references migrated; lab compiles clean
- [ ] **NEW: `Assets/Shaders/PutterGreenGrid.shadergraph`** (or `.hlsl`) exists; emits world-XZ grid lines via `frac(worldPos.xz / _CellSize)` fragment math
- [ ] **NEW: `Assets/Materials/PutterGreenGrid.mat`** references the shader; `_CellSize=0.5`, `_LineWidth=0.04`, `_LineGlow=1.5`, `_BackgroundAlpha=0.0`
- [ ] **NEW: `Assets/Editor/PhysicsLab/TestGreenMeshBuilder.cs`** generates `Assets/Meshes/TestGreen_25x25.asset`
- [ ] **NEW: `Assets/Scenes/Physics/PhysicsLab_TestGreen.unity`** scene loads with the sculpted test green, PhysicsLabController, BakedZoneClassifier wired
- [ ] Distance culling implemented via shader `_BallPosition` MaterialPropertyBlock (option b in §Render step)
- [ ] Vertex colors derived from baked slope magnitude via Q2 ramp; heatmap mode (Q5) swaps the ramp
- [ ] EditMode tests: existing 8 from iter-1 + 2 new (`PutterGreenReader_GeneratesMeshWithCorrectVertexCount`, `PutterGreenReader_GridIsWorldXZAligned`)
- [ ] Smoke-bot scenario `PutterAimWarpedGridOnTestGreen` added; bot recording shows the warped grid on the sculpted green
- [ ] iter-1 smoke-bot scenario `PutterAimGreenReaderVisible` still passes (Hole 1 flat green case)
- [ ] Dashboard toggle exposes `HeatmapMode` (Q5)
- [ ] Color ramp values live in `Assets/Resources/Data/GreenSlopeConfig.csv` (preserved from iter-1)
- [ ] No measurable frame-time regression vs the deleted predictor (single mesh draw call, GPU does the work)
- [ ] Frame Debugger capture in IMPLEMENTER_REPORT showing exactly one draw call for the grid mesh
- [ ] Per Lesson R: every new `.cs` file ships with its `.cs.meta`. Same applies to new `.shadergraph`, `.mat`, `.asset`, `.unity` files — .meta sidecars are mandatory in the commit.

## Out of scope

- Per-hole green-reading authoring tools (separate task at `Docs/Specs/Queued/green_topology_and_pin_authoring/`)
- Sculpting the production Lomond green meshes to add real elevation — this SPEC ships the visual primitive; making real greens non-flat is its own course-content milestone
- **Animated beads** along slope-flow lines (PGA 2K23/2K25 polish) — future polish ticket, swaps the shader over the same mesh
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
