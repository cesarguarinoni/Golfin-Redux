# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Clamp Fringe/Border Vertex Y Below Parent Mesh

With steeper terrain from the monotone spline, the fairway fringe
ring and tee gradient border "invade" — their vertices on the uphill
side sit above the parent mesh, making the decorative ring visually
overpower or eat into the fairway/tee surface.

**Root cause:** Each mesh independently samples `terrain.SampleHeight()`
at its own XZ position. The fringe ring is offset outward from the
fairway contour by ~0.5m, so on a slope it samples a different
terrain height. The 1cm Y-offset gap (0.02 vs 0.03) is meaningless
against real terrain gradients.

**Fix:** After building fringe/border vertices, clamp each vertex Y
to be at most `parentEdgeY - epsilon` where `parentEdgeY` is the
parent mesh's Y at the nearest contour edge point.

### Step 1: Add Y-Clamp Helper

Add this utility method to `HoleLiteImporter`:

```csharp
/// <summary>
/// Clamp each vertex in childVerts so its world Y never exceeds the
/// interpolated Y of the nearest edge on parentVerts (which shares
/// the same contour topology). Both arrays are in local space
/// relative to their respective centroids.
///
/// parentContourCount = number of contour vertices in the parent mesh
/// (the first N vertices in parentVerts that form the outer edge).
/// childContourCount = same for child (should match parent if both
/// were built from the same contour).
/// parentCentroid, childCentroid = world-space mesh origins.
/// maxYAbove = how far above the parent edge the child is allowed
///             (negative = must be below). Use e.g. -0.005f.
/// </summary>
private static void ClampChildVertsToParentEdge(
    Vector3[] childVerts, Vector3 childCentroid,
    Vector3[] parentVerts, Vector3 parentCentroid,
    int parentContourCount, float maxYAbove)
{
    if (parentVerts == null || parentVerts.Length == 0) return;
    int parentN = Mathf.Min(parentContourCount, parentVerts.Length);

    for (int i = 0; i < childVerts.Length; i++)
    {
        // Child vertex in world space
        Vector3 childWorld = childVerts[i] + childCentroid;

        // Find the closest parent contour edge vertex (XZ only)
        float bestDistSq = float.MaxValue;
        float bestParentY = 0f;
        for (int p = 0; p < parentN; p++)
        {
            Vector3 parentWorld = parentVerts[p] + parentCentroid;
            float dx = childWorld.x - parentWorld.x;
            float dz = childWorld.z - parentWorld.z;
            float distSq = dx * dx + dz * dz;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                bestParentY = parentWorld.y;
            }
        }

        // Clamp: child vertex must not exceed parentEdgeY + maxYAbove
        float maxY = bestParentY + maxYAbove;
        float childLocalMaxY = maxY - childCentroid.y;
        if (childVerts[i].y > childLocalMaxY)
            childVerts[i].y = childLocalMaxY;
    }
}
```

### Step 2: Apply to Fairway Fringe Ring

In `CreateFlatZoneMeshes()`, after `CreateFringeRing()` returns the
fringe mesh, apply the clamp. The fairway mesh is the "parent."

Find where `CreateFringeRing` is called. It should look something
like:

```csharp
CreateFringeRing(contour, terrain, terrainBaseY, ...);
```

After the fringe GameObject is created (with its mesh and centroid),
add the clamp call. You'll need access to:
- The fairway mesh vertices (from the CDT mesh just created above)
- The fairway centroid
- The fringe mesh vertices
- The fringe centroid
- The number of contour vertices in the fairway mesh's outer edge

The fairway CDT mesh's first N vertices correspond to the contour
points (CDT adds interior Steiner points after the boundary). N =
the number of contour points fed into CDT.

```csharp
// After fringe mesh is built:
ClampChildVertsToParentEdge(
    fringeMesh.vertices, fringeCentroid,
    fairwayMesh.vertices, fairwayCentroid,
    contourPointCount,  // number of boundary vertices in fairway CDT
    -0.005f);           // fringe must be at least 5mm BELOW parent edge
// Re-assign vertices back to mesh after clamping
fringeMesh.vertices = fringeVerts;
fringeMesh.RecalculateBounds();
```

NOTE: `fringeMesh.vertices` returns a copy. You need to store it in
a local array, pass that to the clamp function, then assign it back:

```csharp
var fringeVerts = fringeMesh.vertices;
ClampChildVertsToParentEdge(
    fringeVerts, fringeCentroid,
    fairwayVerts, fairwayCentroid,
    contourPointCount, -0.005f);
fringeMesh.vertices = fringeVerts;
fringeMesh.RecalculateBounds();
```

### Step 3: Apply to Tee Gradient Border

Same pattern for `CreateGradientBorderRing()`. The tee mesh
(from `CreateFlatContourMesh`) is the parent, the gradient border
is the child.

Find where the tee gradient border is created. Apply the same clamp:

```csharp
var borderVerts = borderMesh.vertices;
ClampChildVertsToParentEdge(
    borderVerts, borderCentroid,
    teeVerts, teeCentroid,
    teeContourCount, -0.005f);
borderMesh.vertices = borderVerts;
borderMesh.RecalculateBounds();
```

### Implementation Notes

- The clamp helper does a brute-force nearest-vertex search. With
  typical contour sizes (50-200 points) and fringe vertex counts
  (~200-400), this is <100K distance checks — negligible at import
  time.

- `maxYAbove = -0.005f` means the fringe/border must always be at
  least 5mm below the nearest parent edge vertex. This guarantees
  the parent mesh renders on top even on steep slopes.

- The clamp only lowers vertices, never raises them. On flat terrain
  the fringe is already below the fairway (due to the existing
  Y-offset hierarchy), so the clamp is a no-op — no visual change
  on flat holes.

- If you can't easily access the parent mesh vertices at the call
  site, an alternative is to store them as a local variable before
  creating the child mesh. Both meshes are created in sequence
  within the same loop iteration.

### What NOT to Change

- Y-offset values (0.01, 0.02, 0.03) — keep them as-is
- Depression system (OverlayDepressionMeters)
- CDT triangulation or contour extraction
- Fairway/tee mesh creation logic
- Green collar (it uses a different raised mesh system)
- Water, bunker, or cart path meshes

### Verification

Re-import a hilly hole (try Hole 4 or whichever has the steepest
terrain currently):

- [ ] Fairway fringe stays UNDER the fairway surface on slopes
- [ ] Tee gradient border stays UNDER the tee surface on slopes
- [ ] On flat terrain, fringe/border look identical to before
- [ ] No visual gaps between fringe and fairway
- [ ] No console errors

---

## Completed Tasks
✅ 2026-04-15 — Clamp fringe/border verts below parent mesh. Added ClampChildVertsToParentEdge() helper; applied inside CreateFringeRing (parent=edgeRing) and CreateGradientBorderRing (parent=innerRingInset). maxYAbove=-0.005f.
✅ 2026-04-14 — Water rework complete (flat CDT mesh + contour depression + deeper shore + 6 iterations of fixes)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
