# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Derive Fringe/Border Y from Parent Mesh Edge (Not Terrain)

The fairway fringe ring and tee gradient border break on slopes
because each mesh independently samples `terrain.SampleHeight()` at
its own XZ. On a slope, the height difference between the parent
mesh edge and the child ring at a 0.5m offset exceeds the tiny
Y-offset gap (1-2cm), causing Z-fighting in both directions.

**Previous attempt (clamp) failed** because the problem is
bidirectional — sometimes the child eats the parent, sometimes the
parent eats the child, depending on slope direction.

**New approach:** Child meshes (fringe ring, tee gradient border)
should NOT sample terrain at all. Instead, each child vertex derives
its Y from the nearest parent mesh edge vertex, minus a fixed offset.
This guarantees a consistent visual layering regardless of slope.

### Concept

For each child vertex at position (cx, cz):
1. Find the nearest parent contour edge vertex (px, pz, py)
2. Set child vertex Y = py - offset (e.g. 5mm below parent)

The child mesh "drapes" from the parent edge downward, always below
the parent surface. No terrain sampling means no slope-dependent
height disagreements.

### Step 1: Modify `CreateFringeRing()`

Currently, fringe ring vertices sample terrain height:
```csharp
float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
// vertex.y = terrainBaseY + th + yOffset;
```

Change this so fringe vertices derive Y from the fairway contour
edge vertices instead. The fairway mesh's contour vertices (the
outer boundary of the CDT mesh) are the "parent edge."

**Implementation:** `CreateFringeRing` needs access to the parent
fairway mesh's contour vertices (in world space). Pass them in as
a parameter.

Change the signature to accept the parent edge data:
```csharp
private static void CreateFringeRing(
    Vector2[] contour,
    Terrain terrain, float terrainBaseY,
    // ... existing params ...
    Vector3[] parentEdgeWorldPositions,  // NEW: fairway contour verts in world space
    float parentOffset)                  // NEW: how far below parent edge (e.g. -0.008f)
```

For each fringe vertex at (wx, wz), instead of sampling terrain:
```csharp
// OLD:
// float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
// vert.y = terrainBaseY + th + fringeYOffset;

// NEW: Find nearest parent edge vertex and derive Y from it
float bestDistSq = float.MaxValue;
float parentY = 0f;
for (int p = 0; p < parentEdgeWorldPositions.Length; p++)
{
    float dx = wx - parentEdgeWorldPositions[p].x;
    float dz = wz - parentEdgeWorldPositions[p].z;
    float dSq = dx * dx + dz * dz;
    if (dSq < bestDistSq)
    {
        bestDistSq = dSq;
        parentY = parentEdgeWorldPositions[p].y;
    }
}
vert.y = parentY + parentOffset;  // e.g. parentY - 0.008f
```

Then convert to local space as before (subtract centroid).

**Building parentEdgeWorldPositions:** After creating the fairway
CDT mesh, extract the contour boundary vertices in world space.
The CDT mesh's first N vertices correspond to the input contour
points (before Steiner points are added). So:

```csharp
// After CDT mesh is built for fairway:
var fairwayWorldEdge = new Vector3[contour.Length];
for (int i = 0; i < contour.Length; i++)
{
    // CDT vertices are in local space relative to centroid
    Vector3 local = fairwayMeshVerts[i];
    fairwayWorldEdge[i] = local + fairwayCentroid;
    // fairwayWorldEdge[i].y is already the correct world Y
    // (terrainBaseY + terrainH + yOffset)
}
```

Pass `fairwayWorldEdge` and `-0.008f` to `CreateFringeRing`.

### Step 2: Modify `CreateGradientBorderRing()`

Same pattern. The tee gradient border should derive Y from the tee
mesh's contour edge vertices instead of sampling terrain.

Change `CreateGradientBorderRing` to accept parent edge data:
```csharp
private static void CreateGradientBorderRing(
    Vector2[] contour,
    Terrain terrain, float terrainBaseY,
    // ... existing params ...
    Vector3[] parentEdgeWorldPositions,
    float parentOffset)
```

Same nearest-vertex lookup for each border vertex's Y.

**Building parent edge for tees:** After `CreateFlatContourMesh`
builds the tee mesh, extract its contour vertices in world space
and pass to `CreateGradientBorderRing`.

### Step 3: Handle the Inner Edge

The fringe ring has TWO edges: the inner edge (touching the fairway)
and the outer edge (extending into rough). Both need the fix, but
they have different parent references:

- **Inner edge vertices** (at the fairway contour): derive Y from
  the fairway mesh edge. These must be BELOW the fairway surface.
- **Outer edge vertices** (offset outward): these can still sample
  terrain, OR derive from the same nearest parent edge vertex with
  a slightly larger offset. Either works since the outer edge isn't
  competing with the fairway mesh.

Simplest approach: ALL fringe vertices derive Y from nearest parent
edge vertex. The outer edge will follow the parent edge's height
profile (slightly wrong vs actual terrain) but this is invisible
because the fringe is only 0.5m wide — the terrain height difference
across 0.5m is negligible compared to the visual improvement.

Same logic applies to tee gradient border: all vertices derive from
tee edge.

### Step 4: Ensure SubdivideToTerrain Compatibility

If `SubdivideToTerrain` is called on fringe/border meshes, the new
subdivided midpoint vertices will sample terrain height — which
defeats the purpose. Two options:

**Option A (preferred):** Skip `SubdivideToTerrain` for fringe and
border meshes entirely. They're narrow rings (0.5m wide) — terrain
conformance over that distance is negligible. Just remove the
subdivision call for these meshes.

**Option B:** If subdivision is needed, modify the midpoint Y
calculation to interpolate between the two parent vertices instead
of sampling terrain.

Go with Option A unless you see that fringe/border meshes currently
call `SubdivideToTerrain` and removing it causes visible issues.

### Summary of Changes

| Method | Before | After |
|--------|--------|-------|
| `CreateFringeRing` | Samples `terrain.SampleHeight()` per vertex | Derives Y from nearest fairway edge vertex - 8mm |
| `CreateGradientBorderRing` | Samples `terrain.SampleHeight()` per vertex | Derives Y from nearest tee edge vertex - 8mm |
| Call site (fairway) | Calls `CreateFringeRing(contour, terrain, ...)` | Extracts fairway edge verts, passes to `CreateFringeRing` |
| Call site (tee) | Calls `CreateGradientBorderRing(contour, terrain, ...)` | Extracts tee edge verts, passes to `CreateGradientBorderRing` |

### What NOT to Change

- Fairway CDT mesh creation (parent mesh still samples terrain)
- Tee flat contour mesh creation (parent mesh still samples terrain)
- Green collar (different system, uses raised mesh)
- Cart path meshes
- Water, bunker meshes
- Depression system
- Y-offset constants (0.01, 0.02, 0.03)

### Verification

Re-import Hole 4 (steepest terrain):

- [ ] Fairway fringe stays visually UNDER the fairway on all slopes
- [ ] Tee border stays visually UNDER the tee on all slopes
- [ ] No Z-fighting between parent and child meshes
- [ ] On flat terrain (Hole 1), fringe/border look identical to before
- [ ] No gaps between fringe outer edge and terrain
- [ ] No console errors

Also reimport Hole 1 to confirm no regression on flatter terrain.

### Remove Previous Clamp Code

If `ClampChildVertsToParentEdge()` was added from the previous task,
it can be removed — it's superseded by this approach.

---

## Completed Tasks
✅ 2026-04-15 — Derive fringe/border Y from parent mesh edge. CreateFringeRing and CreateGradientBorderRing no longer sample terrain; each vertex finds nearest parent contour edge point and uses parentY - 8mm. Call sites build parent edge from contour + terrain at the correct yOffset.
✅ 2026-04-15 — Clamp fringe/border vertex Y (didn't fix — bidirectional Z-fighting on slopes)
✅ 2026-04-14 — Water rework complete (flat CDT mesh + contour depression + deeper shore + 6 iterations of fixes)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
