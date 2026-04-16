# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fix Cart Path Depression: Flat Drop, Not Gradient

Terrain pokes through cart path mesh on concave slopes. The cause
is the **gradient ramp** in cart path depression: cells at the edge
get 0% drop, cells at the center get 100%. So the mesh edges sit
on barely-depressed terrain. On a concave slope, that terrain rises
up and pokes through.

Fairways can use gradient depression because the fringe band covers
the soft-drop edge zone. Cart paths have NO fringe — the mesh IS the
edge. Cart paths need flat depression (full drop everywhere inside
the footprint), same as fairways/tees.

### The Fix

In `HoleGeoImporter.cs`, method `DepressTerrainUnderOverlays()`,
find the cart path depression section. Currently it does:

1. Distance transform on `cartDepress` cells
2. Find `maxCartDist` (center of widest part)
3. Apply smoothstep ramp: `t = dist / maxCartDist`, edge=0%, center=100%

**Replace steps 1-3 with a flat drop** (same as fairway/tee):

```csharp
// Cart path cells: flat depression (same as fairway/tee)
int cartDepressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
{
    for (int hx = 0; hx < hRes; hx++)
    {
        if (!cartDepress[hz, hx]) continue;
        heights[hz, hx] = Mathf.Max(0f,
            heights[hz, hx] - dropNormalized);
        cartDepressedCount++;
    }
}
```

**Delete** the entire distance transform section for cart paths:
- `float[,] cartDist` allocation
- Forward/backward chamfer passes
- `maxCartDist` calculation
- The smoothstep ramp loop

Replace all of it with the simple flat loop above.

### What NOT to Change

- Fairway/tee depression (already flat, working fine)
- The `cartDepress` mask construction (spline polygon marking is correct)
- `_splineCartPathPolygons` population in `CreateSplineCartPaths()`
- Water shore depression
- Spline mesh generation

### Verification

Reimport the hole that had the splotch:

- [ ] No terrain showing through cart path mesh anywhere
- [ ] Cart path mesh still sits above terrain (yOffset = 0.01)
- [ ] No cliff/step at cart path edges (depression is under the mesh,
      invisible from above)
- [ ] Other overlays unaffected
- [ ] No console errors

✅ DONE: 2026-04-16 — Replaced gradient ramp with flat drop for cart path depression. Removed distance transform + smoothstep (~30 lines). Re-import and verify.

---

## Completed Tasks
✅ 2026-04-16 — Spline cart path depression footprint (spline polygon instead of old contour — but gradient ramp nullified the fix at edges)
✅ 2026-04-16 — Spline cart path meshes (smoother curves, keeper)
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh
✅ 2026-04-14 — Water rework complete (6 iterations)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
