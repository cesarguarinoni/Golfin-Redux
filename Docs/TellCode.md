# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Revert Taper, Test Clean spineExt Fix

The taper approach made both junctions look bad. We need to revert it
and test whether the `spineExt→spine` fix alone resolves the original
overshoot problem. The `spineExt` bug was producing wrong tangent
directions at endpoints, which could have been the real cause of the
perpendicular overshoot all along.

### Changes in HoleLiteImporter.cs — `CreateSpineStripMesh`:

1. **Remove `taperStart` and `taperEnd` parameters** from the method
   signature. Revert to the original signature:
   ```csharp
   private static GameObject CreateSpineStripMesh(
       int id, ContourPoint[] spine, float halfWidth,
       Terrain terrain, float terrainBaseY,
       Material mat, float tileSize,
       Golfin.Course.SurfaceType surfaceType)
   ```

2. **Remove all `localHalfWidth` logic.** Delete the `taperPoints`
   block and any `localHalfWidth` variable. Use `halfWidth` directly
   for lx/lz/rx/rz calculations (which should already be the case
   after removing the taper code).

3. **Update the caller** in `CreateFlatZoneMeshes` — remove the
   `taperStart`/`taperEnd` arguments from the `CreateSpineStripMesh`
   call. Just pass the standard arguments.

4. **Keep the `spineExt→spine` fix.** Do NOT revert that. The tangent
   calculations should reference `spine[...]`, not `spineExt[...]`.

### Do NOT change:
- The `snapped_endpoints` data model (harmless, leave it for future use)
- `BuildSpinePolygon`
- Pipeline code (`export-hole.mjs`)
- Any other mesh generation code

### After Reverting

```
cd Tools/UHoleLite
node scripts/export-hole.mjs lomond-country-club 18
```

Then Unity: GOLFIN > Import Hole (Lite) > Hole 18

Check BOTH junctions:
- CP#4→CP#3 (was fine originally, broken by taper)
- CP#4→CP#2 (was overshooting originally)

If CP#4→CP#2 still overshoots after the spineExt fix, report exactly
what it looks like and we'll take a different approach.

---

## Completed Tasks
✅ 2026-04-13 — Revert taper, test clean spineExt→spine fix alone
✅ 2026-04-13 — Taper strip at T-junction endpoints (REVERTING — made both junctions worse)
✅ 2026-04-13 — spineExt→spine fix in CreateSpineStripMesh
✅ 2026-04-13 — Node.js residual ramp + boundary height propagation
✅ 2026-04-13 — Cart path depression: 3-strategy fix
✅ 2026-04-13 — Natural OB↔Rough transition
✅ 2026-04-13 — "Smooth OB" button in UHole Lite
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
