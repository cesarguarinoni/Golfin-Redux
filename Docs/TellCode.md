# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Depress Terrain Under Spline Cart Path Footprint

Terrain pokes through the spline cart path mesh on concave slopes.
The current depression uses the old contour polygon, but the spline
mesh follows a different (smoother) centerline. On curves, the
spline extends over un-depressed terrain → terrain shows through.

**Fix:** After building each spline cart path mesh, construct a
closed polygon from the mesh's left+right edge vertices and feed
it to the depression system. This ensures the depressed area exactly
matches the visible mesh footprint.

### The Approach

The spline mesh generator already has `leftVerts` and `rightVerts`
lists (the left and right edge vertices in world space). Build a
closed polygon:

```
left[0] → left[1] → ... → left[N] → right[N] → right[N-1] → ... → right[0] → close
```

This polygon traces the exact footprint of the strip mesh. Feed it
to `DepressTerrainUnderOverlays()` (or equivalent) with a small
margin so the depression extends slightly beyond the mesh edges.

### Step 1: Build Spline Strip Polygon

After building the spline mesh (after the `leftVerts`/`rightVerts`
lists are populated), construct the footprint polygon:

```csharp
// Build closed polygon from strip edges (world space)
// Left edge forward, right edge backward → closed loop
var stripPoly = new List<Vector2>();
for (int i = 0; i < leftVerts.Count; i++)
    stripPoly.Add(new Vector2(leftVerts[i].x, leftVerts[i].z));
for (int i = rightVerts.Count - 1; i >= 0; i--)
    stripPoly.Add(new Vector2(rightVerts[i].x, rightVerts[i].z));
```

### Step 2: Mark Depression Cells

In `DepressTerrainUnderOverlays()`, the cart path depression
currently uses the contour polygon from `cart-paths.json`. Change
it to use the spline strip polygons instead.

**Option A (cleanest):** Store the strip polygons during mesh
creation (as a list on the importer or pass them to the depression
method). Then in `DepressTerrainUnderOverlays`, iterate the strip
polygons instead of the contour polygons for cart path depression.

**Option B (simpler):** Do the depression inline right after
building each spline mesh, before the mesh is centered at centroid.
At that point `leftVerts`/`rightVerts` are still in world space.
Directly mark heightmap cells inside the strip polygon as depressed.

Go with whichever is cleaner given the current code structure. The
key requirement: the depressed area must match the spline mesh
footprint, NOT the old contour polygon.

### Step 3: Add Margin

Dilate the strip polygon slightly (0.15-0.20m outward) before
depression. This ensures terrain is pushed down a bit beyond the
mesh edges, preventing edge-poke-through on slopes.

If polygon dilation is complex, a simpler approach: use a slightly
wider halfWidth for the depression polygon than for the mesh itself.
E.g., mesh uses `halfWidth = 1.25m`, depression uses
`halfWidth + 0.2m = 1.45m`.

```csharp
// Build depression polygon with margin
float depMargin = 0.20f;
var depPoly = new List<Vector2>();
for (int i = 0; i < leftVertsWide.Count; i++)
    depPoly.Add(new Vector2(leftVertsWide[i].x, leftVertsWide[i].z));
for (int i = rightVertsWide.Count - 1; i >= 0; i--)
    depPoly.Add(new Vector2(rightVertsWide[i].x, rightVertsWide[i].z));
```

Where `leftVertsWide`/`rightVertsWide` are sampled at
`halfWidth + depMargin` instead of `halfWidth`. This can be done
in the same spline evaluation loop — just compute two sets of
left/right offsets.

### What NOT to Change

- Spline mesh generation (the visible mesh is working well)
- Splatmap painting (can stay contour-based for now)
- Fairway, tee, green, bunker, water depression
- HoleLiteImporter.cs

### Verification

Reimport Hole 4 (the one with the visible splotch):

- [ ] No terrain showing through cart path mesh on concave slopes
- [ ] Cart path mesh still looks smooth (unchanged)
- [ ] Depression visible in heightmap (terrain pushed down under path)
- [ ] No depression artifacts at path edges (no cliff)
- [ ] Splatmap still painted under path (unchanged)
- [ ] Other overlays unaffected
- [ ] No console errors

❌ INCOMPLETE: 2026-04-16 — Spline depression footprint fix attempted but oval artifact persists.

**What was implemented:** `_splineCartPathPolygons` collected during mesh gen (wide edge verts at halfWidth+0.20m), `DepressTerrainUnderOverlays()` uses these instead of `BuildSpinePolygon()`.

**What didn't work:** The dark oval remains unchanged after reimport. The artifact may not be a depression mismatch at all — it could be a lighting/shadow artifact from the terrain geometry below the mesh, or a genuine terrain feature that the depression system cannot mask. Needs architect re-evaluation to identify the actual cause.

**Current state:** Spline mesh code is working (smooth curves, SurfaceMarker, MeshCollider). Depression now uses spline footprint polygons. Oval spot unresolved.

---

## Completed Tasks
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
