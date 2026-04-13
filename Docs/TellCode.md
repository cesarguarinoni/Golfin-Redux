# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Fix Cart Path Z-Fighting (Flat Depression + Full Width)

Cart path meshes z-fight with terrain because the current depression
uses a smoothstep gradient that barely lowers the edges. The spine
polygon is also inset 0.50m from the mesh edge, so the depression
doesn't cover the full mesh width.

**Fix:** Cart paths should use the SAME flat depression as fairways/tees
(uniform drop across entire area), and the depression polygon should
cover the full mesh width (no inset).

### Changes in HoleLiteImporter.cs — `DepressTerrainUnderOverlays`:

1. **Change the cart path spine polygon halfWidth** — remove the
   0.50m inset. Instead, use the full halfWidth PLUS a small margin
   so the depression extends slightly beyond the mesh edges:

   Change this:
   ```csharp
   float halfWidth = (cp.width_m > 0
       ? cp.width_m : 2.5f) / 2f - 0.50f;
   if (halfWidth < 0.3f) halfWidth = 0.3f;
   ```

   To this:
   ```csharp
   float halfWidth = (cp.width_m > 0
       ? cp.width_m : 2.5f) / 2f + 0.30f;
   ```

   The `+ 0.30f` extends the depression 30cm beyond the mesh edge
   on each side, ensuring no z-fighting at the boundaries.

2. **Move cart path cells into the `depress` array** instead of the
   separate `cartDepress` array. This makes them use the same flat
   uniform depression as fairways/tees.

   In the cart path section, change `cartDepress` → `depress`:
   ```csharp
   // Was: bool[,] cartDepress = new bool[hRes, hRes];
   // Now: use the existing `depress` array directly
   ```

   Replace `MarkWorldContourCells(spinePoly, cartDepress, ...)` with
   `MarkWorldContourCells(spinePoly, depress, ...)` and same for the
   contour fallback.

3. **Remove the entire cart path gradient section** — delete
   everything from the `// --- Cart path cells: distance-based
   gradual slope ---` comment through the end of the cart path
   smoothstep block (the section that computes `cartDist`,
   `maxCartDist`, and applies `cellDrop`).

4. **Remove the `cartDepress` array declaration** since it's no
   longer used.

### Summary of what this does:
- Cart path depression becomes a flat uniform 0.40m drop (same as
  fairways and tees)
- Depression covers the full mesh width + 0.30m margin
- No gradient/smoothstep — just a clean drop
- The cart path mesh sits 0.01m above the ORIGINAL terrain height,
  which is now 0.41m above the depressed terrain — more than enough
  to prevent z-fighting

### Do NOT change:
- `CreateSpineStripMesh` (the mesh itself is fine)
- `CreateFlatZoneMeshes` (mesh creation is fine)
- Fairway/tee depression logic
- Pipeline code (export-hole.mjs)
- Cart path splatmap painting logic

---

## Completed Tasks
✅ 2026-04-13 — Cart path flat depression (full width + 0.30m margin, no gradient)
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
