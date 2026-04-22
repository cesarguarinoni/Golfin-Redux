# Surface Marker Fix — Implementation Report
**Date:** 2026-04-22  
**Author:** Claude Code  
**For:** Architect Claude (claude.ai)

---

## Background

The physics system uses `SceneSurfaceProvider` (in `Golfin.Physics.Runtime`) to classify world positions to a `SurfaceType` at runtime. It does this by raycasting downward from Y=500 and calling `GetComponentInParent<Golfin.Physics.Runtime.SurfaceMarker>()` on the hit collider.

The course importers (`HoleGeoImporter`, `HoleLiteImporter`) were already adding `Golfin.Course.SurfaceMarker` to every zone mesh — but that is a different type in a different assembly. `SceneSurfaceProvider` never sees it. Result: **every zone mesh defaulted to `SurfaceType.Fairway`**, giving wrong bounce/roll/putt coefficients on greens, bunkers, water, tee, cart path, and everywhere else.

---

## Two Questions Answered

### Q1 — Do green meshes break the heightmap physics?

**Ground height: fine.** The physics heightmap (`HeightmapData`) is baked from `TerrainData.GetHeights` *after* the importer depresses the terrain under all overlay meshes. Green meshes sit at `yOffset = 0.00m` on that depression. `HeightmapData.SampleHeight` returns the correct Y — the ball lands where the mesh is.

**Surface classification: was broken.** See above — `Golfin.Course.SurfaceMarker` is invisible to `Golfin.Physics.Runtime.SceneSurfaceProvider`.

### Q2 — Scope

All zone mesh types were affected: greens, green collars, fairways, bunkers, water, tee boxes, cart paths. Trees and bridges were also raised.

---

## What Was Changed

### `Golfin.Physics.Core.asmdef` — `autoReferenced: false → true`
### `Golfin.Physics.Runtime.asmdef` — `autoReferenced: false → true`

The importers live in `Assembly-CSharp-Editor` (Unity's default editor assembly), which cannot see assemblies with `autoReferenced: false`. Setting both Core and Runtime to `autoReferenced: true` makes `SurfaceType` (Core) and `SurfaceMarker` (Runtime) visible to the importers without needing a new asmdef. Side effect: game runtime scripts in `Assembly-CSharp` now also see physics types — intentional and desirable for upcoming gameplay coupling.

---

### `HoleGeoImporter.cs` — 10 new lines

After every existing `Golfin.Course.SurfaceMarker` addition, one line was added to also add `Golfin.Physics.Runtime.SurfaceMarker` with the correct type:

| Method / Zone | Physics SurfaceType |
|---|---|
| `CreateContourMesh` — Bunker | `Sand` |
| `CreateGreenMeshCDT` — Green CDT mesh | `Green` |
| `CreateRaisedMesh` — collar GO (`{zone}_{id}_Collar`) | `GreenCollar` |
| `CreateRaisedMesh` — surface GO (`{zone}_{id}_Surface`) | `Green` *(was unmarked)* |
| `CreateWaterMeshes` — Water | `Water` |
| `CreateFairwayMesh` — Fairway | `Fairway` |
| `CreateTeeMeshWithInsetBorder` — Tee | `Tee` |
| `CreateTeeMeshFlat` — Tee | `Tee` |
| `CreateTeeMeshWithBorder` — Tee | `Tee` |
| `CreateSpineStripMesh` — CartPath spine | `CartPath` |
| `BuildJunctionFillPatches` — CartPath junction fills | `CartPath` |
| `CreateSplineCartPaths` — CartPath spline | `CartPath` |

### `HoleLiteImporter.cs` — 8 new lines (parallel to Geo)

| Method / Zone | Physics SurfaceType |
|---|---|
| `CreateContourMesh` — Bunker | `Sand` |
| `CreateGreenMeshCDT` — Green CDT mesh | `Green` |
| `CreateRaisedMesh` — collar GO | `GreenCollar` |
| `CreateRaisedMesh` — surface GO | `Green` *(was unmarked)* |
| `CreateWaterMeshes` — Water | `Water` |
| `CreateFairwayMesh` — Fairway | `Fairway` |
| `CreateTeeMeshWithBorder` — Tee | `Tee` |
| `CreateSpineStripMesh` — CartPath | `CartPath` |

---

## Key Detail: `CreateRaisedMesh` Inner Surface

This method creates two child GameObjects:
- `{zoneName}_{id}_Collar` — the sloped fringe ring around the green. Already had a Course marker (`SemiRough`). Added Physics marker `GreenCollar`.
- `{zoneName}_{id}_Surface` — the flat inner putting surface. **Had no marker at all.** Added Physics marker `Green`.

The `_Surface` GO is the flat top of the raised mesh used in the older (non-CDT) green creation path. Both GOs needed markers.

---

## What Was Not Changed (and Why)

### Trees
Tree prefab colliders can intercept the downward raycast from `SceneSurfaceProvider` in wooded rough/OOB areas, causing those positions to classify as `Fairway` (no marker found on tree trunk → default). 

**Proposed fix (not implemented — needs architect decision):**
- In `TreePlacer.cs` and `TreeBrushTool.cs`, after instantiating each tree GO, set it and all children to a dedicated layer (e.g. `"Trees"`, layer index TBD).
- Modify `SceneSurfaceProvider`'s default `layerMask` constructor parameter to exclude that layer.
- This ensures raycasts pass through tree colliders and hit the ground beneath.

**Alternative:** Add `SurfaceMarker(Rough)` to all tree prefab root GOs. This would make treed areas classify as Rough — more correct than Fairway, but requires editing all tree prefabs and doesn't reflect the actual ground surface (a tree on a fairway would become Rough instead of Fairway).

The layer exclusion approach is cleaner.

### Bridges
No bridge mesh creation exists in either importer yet. `BridgeExporter.cs` exports bridge anchor data but the importer does not yet generate physical bridge meshes. Nothing to mark. When bridge mesh generation is added to the importer, use `SurfaceType.CartPath` (hard flat surface, similar bounce/roll properties to asphalt) unless a dedicated `Bridge` surface type is warranted.

---

## Re-Import Required

Existing generated scenes (`Generated/Hole_01.unity` etc.) still carry only the old `Golfin.Course.SurfaceMarker`. They need to be regenerated via `Import > Re-import Current Hole` (or bulk import menus) to get `Golfin.Physics.Runtime.SurfaceMarker` added to their zone meshes.

---

## Test Status

39/39 physics tests pass after the asmdef changes. No regressions.

---

## Commit

`12830151` — `fix: add Physics.Runtime.SurfaceMarker to all zone meshes in both importers`
