# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Bake Fringe Into Fairway Mesh (Single Mesh, No Overlap)

Three separate-mesh approaches failed on slopes (Y-offset, clamp,
parent-derivation). The fundamental problem: two overlapping meshes
at slightly different XZ positions will ALWAYS disagree on terrain
height on a slope. Z-buffer has to pick one, creating visual eating.

**Solution:** Merge the fringe INTO the fairway mesh. One mesh, one
set of vertices, one Z-buffer entry per pixel. No overlap at all.

### Concept

Currently: fairway CDT mesh (contour boundary) + separate fringe
ring mesh. Both overlap at the contour edge → Z-fight.

New: CDT mesh with the DILATED fringe contour as outer boundary.
Vertices inside the original fairway contour get fairway UVs.
Vertices between original and dilated contour get fringe UVs.
Single mesh, single material.

### Step 1: Remove `CreateFringeRing()` Call

In `CreateFlatZoneMeshes()` (or wherever the fairway mesh is built),
find where `CreateFringeRing` is called for each fairway region.
Remove the call entirely — fringe will be part of the fairway mesh.

Also remove `ClampChildVertsToParentEdge()` if it exists.

### Step 2: Dilate Fairway Contour for Outer Boundary

Before feeding the fairway contour to CDT, create a dilated copy.
The dilation offset should match the current fringe width (0.5m).

```csharp
// Dilate contour outward by fringeWidth meters
float fringeWidth = 0.5f;
Vector2[] outerContour = DilateContour(contour, fringeWidth);
```

`DilateContour` already exists or can be built from the existing
dilation logic used for bunkers/cart paths. For each vertex, offset
outward along the averaged normal of adjacent edges:

```csharp
private static Vector2[] DilateContour(Vector2[] contour, float offset)
{
    int n = contour.Length;
    var result = new Vector2[n];
    for (int i = 0; i < n; i++)
    {
        // Previous and next edges
        Vector2 prev = contour[(i - 1 + n) % n];
        Vector2 curr = contour[i];
        Vector2 next = contour[(i + 1) % n];

        // Edge normals (outward — assuming CCW winding)
        Vector2 e1 = (curr - prev).normalized;
        Vector2 n1 = new Vector2(-e1.y, e1.x);
        Vector2 e2 = (next - curr).normalized;
        Vector2 n2 = new Vector2(-e2.y, e2.x);

        // Average normal
        Vector2 avgN = (n1 + n2).normalized;

        // Handle sharp corners: limit offset to avoid spikes
        float dot = Vector2.Dot(n1, avgN);
        float scale = (dot > 0.1f) ? (1f / dot) : 1f;
        scale = Mathf.Min(scale, 3f); // cap at 3x to avoid huge spikes

        result[i] = curr + avgN * offset * scale;
    }
    return result;
}
```

If the winding is CW instead of CCW, the normals will point inward.
Check: if dilated contour is SMALLER than original, flip the normal
direction (negate offset or swap the normal cross product).

### Step 3: CDT with Outer Boundary

Feed `outerContour` (not `contour`) as the CDT boundary. This
makes the CDT triangulate the entire area including the fringe band.

The CDT call should use the dilated contour as its input polygon.
All vertices (both interior Steiner points and boundary vertices)
sample terrain height as normal.

### Step 4: UV Assignment — Fairway vs Fringe

After CDT produces the mesh, classify each vertex as "fairway" or
"fringe" by testing whether it's inside the ORIGINAL contour.

```csharp
// For each vertex in the CDT mesh:
for (int i = 0; i < verts.Length; i++)
{
    // Convert vertex to 2D (world XZ → contour space)
    Vector2 pt = new Vector2(
        verts[i].x + centroid.x,  // world X
        verts[i].z + centroid.z); // world Z

    // NOTE: contour coords use the 90° CCW mapping (x→z, z→x)
    // Make sure the point-in-polygon test uses the same space
    // as the original contour points

    if (IsInsideContour(pt.x, pt.y, originalContourWorld))
    {
        // Fairway UV: tile fairway texture
        uvs[i] = ComputeFairwayUV(verts[i], centroid, ...);
    }
    else
    {
        // Fringe UV: tile fringe texture
        uvs[i] = ComputeFringeUV(verts[i], centroid, ...);
    }
}
```

### Step 5: Material — Two Textures via UV Region

**Option A (simplest — recommended):** Use vertex colors to mark
fairway vs fringe. Set vertex color R=1 for fairway, R=0 for fringe.
Create a simple shader that lerps between two textures based on
vertex color R. The shader receives both fairway and fringe textures
as properties.

```csharp
// Set vertex colors
Color[] colors = new Color[verts.Length];
for (int i = 0; i < verts.Length; i++)
{
    bool isFairway = IsInsideContour(...);
    colors[i] = isFairway ? Color.red : Color.blue;
}
mesh.colors = colors;
```

Shader (URP Shader Graph or simple Lit variant):
- Input: _MainTex (fairway), _FringeTex (fringe), vertex color
- Output: lerp(_FringeTex, _MainTex, vertexColor.r)

**Option B (no shader change):** Use UV coordinates to map fairway
vertices to a region of a texture atlas and fringe vertices to
another region. This requires creating a combined atlas texture from
T_Fairway_Light + T_Fringe. More work for same result.

**Recommendation: Option A.** A simple vertex-color-lerp shader is
~10 lines of Shader Graph and keeps the textures separate (easier
to swap/tune). If Shader Graph feels heavy, a surface shader or
even a custom URP Lit variant works.

### Step 6: Same for Tee + Tee Border

Apply the same pattern to tees:
1. Remove `CreateGradientBorderRing()` call
2. Dilate tee contour by border width
3. CDT with dilated contour as outer boundary
4. Vertex colors: inside original = tee, outside = border
5. Same dual-texture shader (or a separate one with tee + border
   textures)

For the tee border, the "gradient" effect (current border fades from
tee to rough color) can be approximated by using vertex color alpha
as a distance-from-edge value: 1.0 at the original contour, 0.0
at the outer edge. The shader blends accordingly.

### What NOT to Change

- Green collar (uses raised mesh — different system, not broken)
- Cart path meshes (separate spline-based rework planned)
- Water meshes
- Bunker bowls
- Depression system
- Splatmap painting
- CDT triangulation logic itself (just feed it a different contour)

### What to Remove

- `CreateFringeRing()` method (or just stop calling it)
- `CreateGradientBorderRing()` method (or just stop calling it)
- `ClampChildVertsToParentEdge()` if it exists
- Any fringe/border Y-offset constants that are no longer used

### Verification

Re-import Hole 4 (steepest terrain):

- [ ] Fairway mesh includes fringe band — no separate fringe ring
- [ ] Fringe texture visible around fairway edges
- [ ] NO Z-fighting between fairway and fringe on any slope
- [ ] Tee mesh includes border band — no separate border ring
- [ ] NO Z-fighting between tee and border
- [ ] On flat terrain (Hole 1), looks identical to before
- [ ] No console errors
- [ ] All overlay meshes still intact (greens, bunkers, water, etc.)

### Notes for Implementation

- The `IsInsideContour` / point-in-polygon test already exists in the
  codebase (used for splatmap painting). Reuse it.
- CDT (BurstTriangulator) accepts any simple polygon as boundary.
  The dilated contour is still a simple polygon as long as dilation
  doesn't create self-intersections. At 0.5m dilation this should
  not happen for golf-course-scale contours.
- If CDT fails with the dilated contour (rare edge case), fall back
  to the original contour without fringe — better than crashing.

---

## NEXT Task — Spline-Based Cart Path Meshes

See `Docs/CART_PATH_SPLINE_PLAN.md` for full spec.
Prerequisites: Splines package installed + spine data in export.

---

## Completed Tasks
✅ 2026-04-15 — Bake fringe/border into parent mesh via dilated CDT. CreateFairwayMesh and CreateTeeMeshWithBorder now CDT a dilated contour; vertices classified by IsInsideContour vs the original polygon; triangles go to fairway/tee submesh or fringe/border submesh. Single mesh = no Z-fight. DEVIATION: used 2 submeshes instead of vertex-color blend shader (Option A). Hard material edge at the original contour — if visually poor, shader is follow-up.
✅ 2026-04-15 — Clamp fringe/border vertex Y (didn't fix — bidirectional)
✅ 2026-04-15 — Parent-derived Y for fringe/border (didn't fix — still bidirectional)
✅ 2026-04-14 — Water rework complete (6 iterations)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
