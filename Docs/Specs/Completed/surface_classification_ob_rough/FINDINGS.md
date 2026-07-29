# Findings — runtime surface classification: OB-past-edge + dead Rough/Semirough

**Author:** Claude Code (main thread), 2026-07-28
**Origin:** surfaced while finishing `ob_boundary_presentation` — the OB *camera clamp* never fires on a ball hit off the course edge.
**Status:** analysis only. Two separate defects with a shared root cause. For architect review; no code changed by this doc.

---

## 1. How runtime surface classification actually works (corrected)

Runtime surface type comes **only** from `BakedZoneClassifier` (`Assets/Scripts/Physics/Runtime/Baked/BakedZoneClassifier.cs`), built from the per-hole `zones.json` in `PhysicsLabController.TryLoadBakedProviders` (`PhysicsLabController.cs:1447`). The legacy scene-raycast surface provider was **removed in Phase F** (noted in `Assets/Scripts/Physics/Runtime/SurfaceMarker.cs`). **Nothing samples the terrain alphamap at play time** (no `GetAlphamaps` anywhere in runtime code).

`BakedZoneClassifier.Classify(x,z)` (`:178`) resolves in three steps:
1. **Polygon zones** (checked first) — baked by `BakeZoneJsonTool` from GameObjects that carry a `SurfaceMarker` **mesh** (`BakeZoneJsonTool.cs:182`). These are the raised/painted *zone meshes*: Green, Tee, Sand, CartPath, Fairway, Water (+ GreenCollar/BunkerLip where present).
2. **OB mask** — `if (hasObMask && IsObAt(x,z)) return OOB;` (`:193`). The mask is bit-packed, rasterized at import time from **alphamap layer 8 (OB) only** (`HoleGeoImporter.cs:1388`, `isOB = alphamap[...,8] > 0.5`), covering exactly the terrain footprint.
3. **Fallback** — `return DefaultSurface;`, and `DefaultSurface = SurfaceType.Fairway` (`:73`).

Confirmed against baked data: every hole's `zones.json` (all 18) contains only `Fairway / Green / Tee / Sand / CartPath / Water` polygon groups. **No `Rough`, `Semirough`, `GreenCollar` (as polygons), or `OOB` polygon exists in any hole.** OOB exists solely as the layer-8 mask; Rough/Semirough exist **nowhere** in the runtime data.

---

## 2. Defect A — OB past the course edge is classified Fairway (breaks penalty + camera)

`IsObAt` (`:203`) returns `false` for any point **outside the mask grid**:
```
if (ix < 0 || ix >= obMaskWidth)  return false;
if (iz < 0 || iz >= obMaskHeight) return false;
```
The mask grid == the terrain footprint. For Hole 6 that is **X ∈ [−114.45, +114.45], Z ∈ [−50.3, +50.3]** (`TerrainData_Hole06Geo` size 228.9 × 100.6; obMask `worldOrigin/size` identical). Anything past the terrain edge matches no polygon, is outside the mask, and falls through to **`DefaultSurface = Fairway`**.

Causal chain (verified): `BallSimulation` bounce loop calls `Classify` each ground contact (`BallSimulation.cs:242`); `if (surface == OOB)` terminates the shot `HitOOB` (`:257`) → `BallStateMachine` sets `BallState.OB` (`BallStateMachine.cs:156`) → OB penalty + OBFreeze, and the trajectory gets an OOB `TerrainHit` that the camera clamp (`LoopCameraDirector.TryFindFirstOBHit`) arms on. **Because the region past the edge classifies as Fairway, none of that fires** — an off-course ball is silently treated as in-play (lands on physics ground at X≈198, `surface=Fairway`, `OBState=false`).

Two consequences:
- **Gameplay:** over-hit shots that leave the course are **not penalized** (a full driver on the 168-yd par-3 Hole 6 carries to ~X=349, well past the X=114.45 edge, and counts as fairway).
- **Camera:** the OB clamp has no OOB hit to arm on → the "camera stops at OB" behaviour never triggers for a fly-off-the-edge shot. (The internal perimeter OB band at X≈99.7–100 *does* fire the clamp on a low/rolled shot, but it's inside the terrain, before the visual edge/void.)

---

## 3. Defect B — Rough and Semirough are never classified at runtime

Rough and Semirough are **not zone meshes / polygons** — they are painted into the terrain **splatmap** (alphamap layer 3 = Rough, layer 2 = Semirough; `SurfaceMarkerMap.MapCourseToPhysics` case 2/3), each terrain layer carrying a surface type in the inspector.

But the bake pipeline **only rasterizes the OB layer (8)** into runtime data (the obMask). Rough/Semirough layers are referenced in `HoleGeoImporter` **only** for the OB-boundary dilation that shapes the fringe/skirt mesh (`:1389–1412`) — they are never written to any runtime surface mask, and there are no rough/semirough zone meshes to bake as polygons. With no runtime alphamap sampling, **the classifier can never return Rough or Semirough — every such point collapses to the Fairway default.**

Quantified impact (`SurfaceConfig.Default`):

| Surface | Restitution | TangentFriction | RollingResistance | StopSpeed |
|---|---|---|---|---|
| Fairway | 0.50 | 0.55 | **0.18** | 0.10 |
| Semirough | 0.38 | 0.70 | 0.28 | 0.15 |
| Rough | 0.25 | 0.82 | **0.45** | 0.22 |

Rough has **2.5× fairway's rolling resistance** and half its restitution — it should stop a ball far sooner. Today, balls in the rough roll out exactly like fairway. The surface enum, the coefficients, and the terrain paint all exist; the **import→runtime bridge drops the layers.** Missing the fairway currently carries no ball-behaviour penalty.

---

## 4. Shared root cause

The runtime surface model is **incomplete**: it captures mesh-based zones + the single OB alphamap layer, but (a) drops the terrain-painted surface layers (Rough/Semirough) and (b) has no representation of "beyond the course." Both defects fall out of the same gap.

---

## 5. Solution options

### Option 1 — narrow OB fix (Defect A only)
- **Physics:** in `BakedZoneClassifier.Classify`, split `IsObAt`'s out-of-grid case from the unmarked-in-grid case and return `OOB` when the point is **outside the mask/terrain footprint** (~5 lines). This is the "beyond terrain → OOB" fix. Orthogonal to the in-bounds Fairway default, so in-bounds play is byte-identical; generalizes across all 18 holes (each mask == its footprint).
- **Viewer:** the sim only samples surface at *bounces*, so a ball that *flies* off the edge first registers OOB where it lands (X≈182), not at the edge. To stop the camera **at** the boundary, compute the clamp point as the ball's ground-path × footprint-edge intersection (ray/rect, ≈X=114.45), not the first OOB `TerrainHit`. Presentation-only.
- Does **not** fix Defect B.

### Option 2 — principled fix (Defects A + B, one mechanism)
Replace the OB-only `obMask` with a **full per-cell surface grid** baked from the terrain alphamap (dominant layer → `SurfaceType` per cell, matching `SurfaceMarkerMap`). Polygons still take priority (mesh zones win); cells **outside the grid → OOB**. This restores Rough/Semirough physics **and** fixes OB/camera with a single coherent surface source. Bigger bake + runtime change (grid is ~1 byte/cell vs 1 bit; re-bake all holes); full physics suite must re-run.

### Rejected
- **`DefaultSurface = OOB`:** the default actively carries *all in-bounds rough* on every hole → flipping it makes huge swaths of every course OOB. No.
- **Re-bake a larger OB mask:** per-hole, finite extent (a long enough shot still escapes), and does nothing for Rough/Semirough. No.

---

## 6. Recommendation

**Option 2** is the correct fix — it makes the runtime surface a faithful bake of the authored terrain (fixing the dead rough/semirough) and gives OB/"beyond the course" a real representation (fixing penalty + camera) from one source of truth. **Option 1** is the minimal path if only the OB/camera behaviour is in scope now, with Defect B tracked separately.

**Gameplay-balance caveat (both options):** these are correctness fixes that **change ball behaviour** — Option 1 penalizes off-course shots that are currently free; Option 2 additionally makes rough play like rough (shorter roll, harder lie). Both shift hole difficulty and need a product sign-off + a tuning pass, not just a green test suite.

**Scope note:** all of this lives in `Assets/Scripts/Physics/` core + the bake pipeline + `zones.json`, which the `ob_boundary_presentation` task was explicitly fenced out of (read-side presentation only). The empty-screen/skirt fix already delivered stands as-is; this is a **new, physics-side task.**
