# Fringe / Border Submesh Baking — Lessons Learned

This doc captures what we learned solving the fairway-fringe and
tee-border Z-fighting / "eating" problem. The final solution works;
getting there required ~8 failed iterations. The lessons apply to any
future "decorative ring around a parent overlay mesh on terrain" work.

## Context

Fairway meshes get a semirough "fringe" band around their edges.
Tee meshes get a gradient "border" band. Both the fairway and tee
conform to terrain via `terrain.SampleHeight()` at their CDT vertices.

## The Core Problem

When the fringe/border is a **separate mesh** from its parent (fairway
or tee), the two meshes sample terrain independently at different XZ
positions (offset by ~0.5m). On a slope, that 0.5m offset produces
height deltas that exceed any Y-offset hierarchy (1–2cm), so the
Z-buffer has no consistent winner — you get bidirectional "eating" and
Z-fighting, where sometimes the parent renders through the child and
sometimes the reverse, depending on slope direction.

**Any two-mesh approach with independent terrain sampling will fail on
slopes. Period.**

## Things That DON'T Work (and why)

### 1. Larger Y-offset
Bumping the fringe from 0.012 to 0.05 or 0.10. Fails because on steep
terrain, the terrain-height delta across 0.5m can easily exceed 10cm.
Also: if the offset is visible as a float-gap on flat terrain.

### 2. Clamp child verts to parent edge Y (one-directional)
For each fringe vert, find the nearest parent edge vert and clamp the
fringe's Y to (parentY − ε). Fails because the problem is
**bidirectional**: sometimes the parent is lower than the fringe at a
given XZ, sometimes higher. A one-way clamp only fixes one direction.

### 3. Derive child Y from nearest parent edge (no terrain sampling)
Like #2 but all child verts derive Y from the parent's contour edge
instead of terrain. Still fails because the parent mesh's **interior**
isn't at its contour-edge Y — CDT adds Steiner points, and the parent
surface is interpolated across the triangle interior. The child's
derived Y matches the contour edge but not the parent's actual surface
a short distance inward/outward.

### 4. Submesh classification by distance-to-contour
CDT on the **original** contour, classify each triangle by distance
from its centroid to any contour edge. Triangles with dist ≤ fringe
width → fringe submesh. Fails because narrow parts of a fairway (< 1m
across) have interior points within fringe-width of BOTH edges, so the
entire cross-section becomes fringe — the fringe visibly cuts across
the fairway.

### 5. "Just make the hack more extreme"
Shrinking `inwardOverlap` and `borderWidth` to minimize the visible
problem area. Reduces severity but doesn't fix the root cause. The
artifact is still there, just smaller.

## The Solution That Works

Merge the fringe INTO the parent CDT mesh as a second submesh. Four
pieces, all required:

### 1. Dilate the contour outward by `fringeWidth`

Run CDT on the dilated shape, not the original. The fringe band
physically exists in the **dilation ring** (outside the original
contour). This makes it impossible for fringe triangles to cut across
narrow parts of the parent.

```csharp
ContourPoint[] dilatedContour = DilateContour(originalContour, fringeWidth);
```

### 2. Pass the original contour as an INTERNAL CDT constraint

Without this, triangle edges don't align with the original contour.
The submesh boundary (where triangles switch from parent to fringe)
becomes jagged — zig-zag following the CDT triangulation rather than
the smooth original contour.

Add the original contour's vertices + closing edge list as a second
constraint loop alongside the outer dilated loop:

```csharp
// In CDTTriangulate, optional param:
if (innerConstraint != null && innerConstraint.Length >= 3) {
    int innerStart = positions2D.Count;
    foreach (pt in innerConstraint) positions2D.Add(double2(pt.x, pt.z));
    for (int i = 0; i < innerConstraint.Length; i++) {
        constraintEdges.Add(innerStart + i);
        constraintEdges.Add(innerStart + ((i + 1) % innerConstraint.Length));
    }
}
```

BurstTriangulator honors internal constraint edges as mandatory edges
in the output triangulation. Result: triangle edges lie exactly along
the original contour. No triangle can straddle.

### 3. Classify triangles by CENTROID, not by vertex

Vertices ON the contour boundary (shared between parent-side and
fringe-side triangles) have ambiguous `IsInsideContour` results
because ray-cast on a boundary point is a floating-point coin flip.

Use the triangle centroid instead — guaranteed strictly interior or
exterior because the triangle has nonzero area:

```csharp
float triCx = (a.x + b.x + c.x) / 3f;
float triCz = (a.z + b.z + c.z) / 3f;
bool isParent = IsInsideContour(triCx, triCz, originalPoly);
```

### 4. Duplicate vertices at the submesh boundary for per-material UVs

The two materials need different UVs (e.g. fairway wants mow-stripe
UVs, fringe wants tile UVs). Vertices referenced by both submeshes
need two copies — same 3D position, different UV. Iterate only the
fringe-triangle verts and create a duplicate for each, remapping the
fringe triangle index list:

```csharp
foreach (int origIdx in fringeSrcTris) {
    if (!remap.ContainsKey(origIdx)) {
        remap[origIdx] = finalVerts.Count;
        finalVerts.Add(rawVerts[origIdx]);
        finalUVs.Add(computeFringeUV(rawVerts[origIdx]));
    }
    fringeTris.Add(remap[origIdx]);
}
```

Assign the mesh with `subMeshCount = 2`, both submeshes' triangle
arrays, and `MeshRenderer.sharedMaterials = { parentMat, fringeMat }`.

## Material-Specific UV Recipes

### Fairway fringe (tiled semirough texture)
Simple world-XZ tiling: `uv = new Vector2(v.x / tileSize, v.z / tileSize)`
Independent of mow-stripe direction.

### Tee border (gradient texture, light→dark)
UV.u encodes normalized distance to the original contour:
- `u = 0` at the tee edge → light side of gradient (near tee)
- `u = 1` at the dilated outer boundary → dark side

```csharp
float dist = Mathf.Sqrt(DistanceSqToContour(v.x, v.z, originalPoly));
float u = Mathf.Clamp01(dist / borderWidth);
float v = (v.x + v.z) / tileSize; // arbitrary per-perimeter tiling
```

`DistanceSqToContour` = min squared perpendicular distance from the
point to any edge segment of the polygon.

## The Biggest Trap: Editing the Wrong File

**Before making ANY change to import logic, check which importer the
user actually runs.** This codebase has parallel files with similar
structure but different conventions:

| File | Menu path | Coord convention |
|------|-----------|------------------|
| `HoleLiteImporter.cs` | `Import/Lite/*` | 90° CCW rotation: `wx = contour.z`, `wz = contour.x` |
| `HoleGeoImporter.cs` | `Import/Geo/*` | Direct mapping: `wx = contour.x`, `wz = contour.z` |

Both have: `CreateFairwayMesh`, `CreateFlatContourMesh`, `CDTTriangulate`,
`OffsetContourOutward`, `IsInsideContour`. They diverged. A fix copied
between them needs the coordinate convention swapped.

**I lost ~6 iterations fixing only HoleLiteImporter while the user
was testing via Import > Geo.** Always grep the Menu path the user
describes to find the actual entry point file.

Also: **`Import/Re-import Current Hole`** dispatches via recorded
`importType` ("Lite", "LiteFlat", "Geo", "GeoFlat") to one of these
two files. If you update one, update the other for parity.

## Other Important Technicalities

### `OffsetContourOutward` handedness
Expects a CCW polygon in world-XZ coords. The function uses a 90° CW
rotation of the edge vector to get the outward normal, relying on the
polygon being CCW in its coordinate system.

- **Lite**: contour is CCW in export space; the 90° rotation to world
  XZ preserves CCW-ness in world XZ.
- **Geo**: contour is already in world XZ (no rotation), still CCW.

So positive `offset` = outward in both. Negative = inward. Same
function works for both.

### yOffset values
- Fairway interior: `0.015f` (bumped from 0.01 — on steep slopes, 1cm
  wasn't enough to clear terrain sampling error)
- Tee interior: `0.02f`
- Green: `0.03f+` (raised mesh, different system)
- Order matters: each layer must exceed the one it sits on top of PLUS
  the worst-case terrain sampling error at its XZ extent.

### Fallback for degenerate dilation
If the dilated contour self-intersects (sharp concave corners + large
dilation), CDT may fail. Always wrap with a fallback:

```csharp
if (dilatedCDT.failed) {
    // Retry with original contour, skip fringe — a valid fairway
    // without fringe is better than a crash.
}
```

### Never use `terrain.SampleHeight` on two overlapping meshes expecting them to agree
This is the entire lesson. If two meshes need to sit flush against
each other on sloped terrain, they must **share vertices** — same CDT
output, same mesh, different submeshes. The moment they have
independent geometry, they will disagree on any slope.

## Signals That You're On The Wrong Track

These symptoms indicate you're applying a band-aid to a structural
problem rather than fixing it:

- "It's better on flat terrain but still wrong on hills"
- "Reducing the band width made it less visible"
- "Just add more Y offset"
- "Clamp the child's height to be below the parent's edge"
- "Use raycasts against the parent MeshCollider"

The last one (raycast against parent) actually works but is expensive
at import time and brittle — it's a worse version of just merging into
the same mesh.

## Reference Commits

- `1e1c0657` — Initial dilated-CDT + submesh approach (Lite, had jaggies)
- `014536de` — Port to Geo importer (found the file mismatch)
- `7c24b088` — Fix Geo's coordinate convention bug
- `ba240516` — Add internal CDT constraint + centroid classification
  (eliminates jaggies — this is the key commit)
- `60b30f34` — Tee border UV encodes distance-to-contour
- `c67c5753` — Fairway yOffset bump 0.01 → 0.015
- `23034f15` — Port all fixes back to Lite importer for Re-import parity
