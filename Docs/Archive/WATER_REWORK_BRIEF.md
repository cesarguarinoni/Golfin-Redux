# Water Rework — Brief for Architect

**Date:** 2026-04-14
**Commit:** `27db67a5`
**Scope:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`

## What shipped

Flat CDT water meshes with ramped underwater bed and symmetric shore slopes. Visually clean on Hole 01 and Hole 12 with no manual scene adjustments needed.

## Spec vs. final implementation — 6 deltas

Your original spec (Steps 1–6) got us ~70% of the way. The remaining 30% emerged from iterative visual testing. Each fix below corrects something that the spec either didn't anticipate or got slightly wrong.

### Fix #1 — `normalizedFlat` decoupling (required)
Spec left `normalizedFlat = ShoreDepthMeters / elevRange` while setting `terrainGO.position.y = -TerrainYOffset`. Inconsistent — flat terrain ended up at world Y = +0.3 (user had to manually drop terrain GO to -0.29). Changed `normalizedFlat` to use `TerrainYOffset`.

### Fix #2 — `TerrainYOffset` value (required)
Spec set `TerrainYOffset = 0.1f`. Combined with Fix #1 this gave only 0.1m of heightmap headroom below flat terrain — not enough to contain the 0.35m-deep water bed. Heightmap clamped to 0, water bed pinned at the terrain transform Y, water ended up only 5cm deep. **Bumped `TerrainYOffset` to 0.4f to match `ShoreDepthMeters`.** Math needs: `TerrainYOffset ≥ ShoreDepthMeters + watersurface_offset (0.05) + underwater_margin (0.3)`.

### Fix #3 — Absolute-Y water bed (major)
Spec's `DepressTerrainUnderOverlays` depressed water cells by a fixed **relative** -0.4m drop. This broke on rolling terrain: water surface is anchored to `minTerrainH`, but each water-cell's relative drop floors at `h - 0.4`. Cells where `h > minTerrainH + 0.35m` popped above water. **Refactored to absolute-Y**: each water body gets a `waterYNorm` (computed same way as `CreateWaterMeshes`), and water cells are set directly to `waterYNorm - 0.3m` regardless of original terrain. Chamfer distance transform augmented to propagate nearest-body index so shore cells also reference the right `waterYNorm`.

### Fix #4 — ShoreRadius + (rejected) blur (iteration)
Initial shore ramp had visible diagonal stair-stepping because chamfer distance produces integer-ish bands (1.0, 1.414, 2.0…) on diagonal boundaries. First attempt: widen ShoreRadius 4→10 and add 2-pass 3×3 box blur. **Blur made things worse** — it averaged shore cells with out-of-radius neighbors at origH, RAISING them 2-3cm above water mesh and producing the "floating water" look, asymmetric by proximity to depress cells. Wider radius alone was fine; blur removed.

### Fix #5 — Inverted underwater ramp (the real fix)
Even with correct shore ramp, one side kept showing a visible cliff. Root cause: **terrain mesh interpolation under the water mesh boundary**. Water mesh sits flat at `waterY`. Water-cell vertices had heights at `waterBed = waterY - 0.3m`, shore-cell vertices at `waterH = waterY`. Unity terrain linearly interpolates between vertices, so at the exact contour line (mid-cell), terrain sits ~15cm below water mesh. The edge of the flat water mesh visually hovers above that interpolated trough — especially where the contour cuts cells diagonally. **Fix:** added a second chamfer pass for `distToShore` inside the contour, then ramped water bed depth via smoothstep: flush-at-edge, 0.3m-deep in interior. Water mesh boundary now meets terrain at the same Y on all sides.

### Fix #6 — ShoreRadius tuning
Kept at 10 in final — narrower showed chamfer banding, wider served no visual purpose at 2049 heightmap res.

## Key files touched

- `HoleLiteImporter.cs` — constants, `CreateWaterMeshes`, `DepressTerrainUnderOverlays`, `CreateWaterMaterial`
- Nothing in exporter (`export-hole.mjs`) needed changes — water contour data from `water.json` was sufficient.

## Things to know going forward

1. **`TerrainYOffset` and `ShoreDepthMeters` are now coupled.** If you bump `ShoreDepthMeters`, bump `TerrainYOffset` to at least `ShoreDepthMeters + 0.35`. Consider expressing this as a derived value.

2. **Water bed is per-body.** Multiple water bodies on the same hole each get their own `waterYNorm` sampled from their own contour's min terrain height. Distance transform propagates body IDs so shore cells near body A don't pick up body B's surface Y.

3. **Interpolation-at-contour is a real class of bug.** Any future feature that sets heightmap values inside a polygon and expects a flat mesh on top to meet terrain cleanly needs an inverted ramp at the polygon boundary. Sand/bunkers probably have a milder version of this.

4. **Fairway/tee/cart path meshes still sit +0.01 above undepressed terrain baseline.** On flat terrain far from water, this is fine. If a future spec wants those overlays to flush-meet water (instead of having rough buffer between), we'll need to either: (a) clip overlay contours away from water in exporter, or (b) ramp overlay mesh vertex Y near water contour.

5. **Debugging flow that worked:** user screenshots + my geometric reasoning. Faster than adding diagnostic logs when the issue was geometric (mesh/terrain height math) rather than numerical drift.

## Verification status

- ✅ Hole 01 re-imports cleanly, water symmetric
- ✅ Hole 12 re-imports cleanly (spec's test hole)
- ✅ No console errors
- ✅ Fairways, tees, bunkers, greens, cart paths unaffected
- ⚠️ Only tested on holes 01 + 12. If any other hole has water adjacent to a fairway with <0.5m rough buffer, may still show fairway-mesh-edge cliff. Noted in "Things to know #4".
