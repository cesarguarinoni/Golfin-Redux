# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fairway Mow Stripes + Fairway Border Ring

**Context:** Fairway, tee, and cart path are now mesh overlays with smooth
contour edges. Two things remain to match the reference look:

1. **Mow stripes** — alternating light/dark fairway bands running
   perpendicular to the tee→green axis.
2. **Fairway border ring** — a semi-rough fringe mesh wrapping around
   the fairway edge (same concept as the green collar).

---

### Task 1: Fairway Mow Stripes

**Problem:** The previous attempt produced radial stripes because the
centroid-fan triangulation creates triangles radiating from the center.
Stripe assignment per-triangle follows that radial pattern.

**Solution:** Use a **two-submesh approach** with world-space stripe
calculation. Each triangle gets assigned to either submesh 0 (light) or
submesh 1 (dark) based on its centroid's position projected onto the
stripe axis. The mesh gets a MeshRenderer with two materials.

#### Implementation

Modify `CreateFlatContourMesh` (or create a fairway-specific variant
called `CreateFairwayMesh`) in `HoleLiteImporter.cs`:

**Step 1:** Compute the stripe direction. Load back tee + green centroid
the same way the splatmap code did:

```csharp
// In CreateFlatZoneMeshes, before the fairway loop:
// Compute stripe direction (perpendicular to tee→green)
Vector2 stripeDir = new Vector2(0, 1); // default
string anchorsPath = Path.Combine(exportPath, "anchors.json");
if (File.Exists(anchorsPath))
{
    string anchJson = File.ReadAllText(anchorsPath);
    var anchWrap = JsonUtility.FromJson<AnchorArrayWrapper>(
        "{\"items\":" + anchJson + "}");
    var anchs = anchWrap.items;
    var backTee = System.Array.Find(anchs, a => a.type.Contains("back"));

    string grPath = Path.Combine(exportPath, "greens.json");
    AnchorLocal greenCenter = null;
    if (File.Exists(grPath))
    {
        var grFile = JsonUtility.FromJson<GreensFileData>(File.ReadAllText(grPath));
        if (grFile.greens != null && grFile.greens.Length > 0)
            greenCenter = grFile.greens[0].center_local;
    }

    if (backTee != null && greenCenter != null)
    {
        Vector2 teePos = new Vector2(backTee.local.z, backTee.local.x);
        Vector2 greenPos = new Vector2(greenCenter.z, greenCenter.x);
        Vector2 dir = (greenPos - teePos).normalized;
        if (dir.sqrMagnitude > 0.01f)
            stripeDir = new Vector2(-dir.y, dir.x); // perpendicular
    }
}
```

**Step 2:** In the fairway mesh creation, after building the vertices
and triangles (centroid-fan), split triangles into two lists based on
which stripe band the triangle centroid falls in:

```csharp
// After building verts[], uvs[], tris[] as before...
// worldPts[] contains the world-space positions of the contour vertices
// verts[n] = centroid at Vector3.zero (relative), actual world pos = centroid variable

float stripeWidth = 5f; // meters per stripe — same as MowStripeWidth

// Classify each triangle into light or dark based on its centroid
var lightTris = new System.Collections.Generic.List<int>();
var darkTris = new System.Collections.Generic.List<int>();

for (int t = 0; t < tris.Length; t += 3)
{
    // Get world positions of triangle vertices
    // verts are relative to centroid, so add centroid back
    Vector3 v0 = verts[tris[t]]     + centroid;
    Vector3 v1 = verts[tris[t + 1]] + centroid;
    Vector3 v2 = verts[tris[t + 2]] + centroid;

    // Triangle centroid in world space
    float tcx = (v0.x + v1.x + v2.x) / 3f;
    float tcz = (v0.z + v1.z + v2.z) / 3f;

    // Project onto stripe axis
    float proj = tcx * stripeDir.x + tcz * stripeDir.y;
    int band = Mathf.FloorToInt(proj / stripeWidth);

    if (band % 2 == 0)
    {
        lightTris.Add(tris[t]);
        lightTris.Add(tris[t + 1]);
        lightTris.Add(tris[t + 2]);
    }
    else
    {
        darkTris.Add(tris[t]);
        darkTris.Add(tris[t + 1]);
        darkTris.Add(tris[t + 2]);
    }
}

// Create mesh with 2 submeshes
mesh.subMeshCount = 2;
mesh.SetTriangles(lightTris.ToArray(), 0);
mesh.SetTriangles(darkTris.ToArray(), 1);
```

**Step 3:** Set two materials on the MeshRenderer:

```csharp
var renderer = go.AddComponent<MeshRenderer>();
renderer.sharedMaterials = new Material[] { lightFairwayMat, darkFairwayMat };
```

Where `lightFairwayMat` uses `T_Fairway_Light` and `darkFairwayMat`
uses `T_Fairway_Dark`.

**CRITICAL:** The stripe direction MUST use the same perpendicular-to-
tee→green calculation as the old splatmap code. The projection formula is:
`proj = worldX * stripeDir.x + worldZ * stripeDir.y` where
`stripeDir = perpendicular to (teePos → greenPos)`.

**CRITICAL:** The centroid-fan triangulation creates narrow pie-slice
triangles. Some triangles may span across a stripe boundary, which
means the stripe edge will follow triangle edges (slightly jagged).
This is acceptable for now — at ~200+ contour vertices, the triangles
are small enough that stripe boundaries look reasonable. If the stripe
edges look too jagged, a future pass can subdivide the mesh along stripe
boundaries, but DON'T do this now.

**Signature change:** `CreateFlatContourMesh` for fairways needs
additional parameters: `stripeDir`, `stripeWidth`, `darkFairwayMat`.
The cleanest approach is to add a dedicated `CreateFairwayMesh` method
that handles the two-submesh logic, keeping `CreateFlatContourMesh` for
tee/cart path (single material). Or add optional parameters — your call.

---

### Task 2: Fairway Border Ring (Fringe)

**Goal:** A semi-rough mesh ring (~1.5m wide) wrapping around the outside
of each fairway contour. Same concept as the green collar.

#### Implementation

The green collar in `CreateRaisedMesh` uses concentric scaled rings of
the contour polygon. For the fairway fringe, we do similar but simpler:
- **Outer ring:** offset the contour outward by `FairwayFringeMeters` (~1.5m)
- **Inner ring:** the original contour
- **Mesh:** triangle strip between outer and inner rings
- **Material:** semi-rough texture
- **Height:** flat at terrain height (same yOffset as fairway mesh)

#### Contour offset method

To expand the contour outward by a distance, compute the outward normal
at each vertex and push it out:

```csharp
/// <summary>
/// Offset a closed contour outward by a distance.
/// At each vertex, compute the average outward normal of its two edges,
/// then push the vertex along that normal.
/// </summary>
static Vector3[] OffsetContourOutward(Vector3[] contour, float distance)
{
    int n = contour.Length;
    var result = new Vector3[n];

    for (int i = 0; i < n; i++)
    {
        int prev = (i - 1 + n) % n;
        int next = (i + 1) % n;

        // Edge vectors (XZ plane)
        Vector2 e1 = new Vector2(contour[i].x - contour[prev].x,
                                  contour[i].z - contour[prev].z).normalized;
        Vector2 e2 = new Vector2(contour[next].x - contour[i].x,
                                  contour[next].z - contour[i].z).normalized;

        // Outward normals (rotate 90° CW: (x,z) → (z,-x))
        // NOTE: direction depends on winding. If contour is CCW,
        // outward is CW rotation of edge direction.
        Vector2 n1 = new Vector2(e1.y, -e1.x);
        Vector2 n2 = new Vector2(e2.y, -e2.x);

        // Average normal (handles corners smoothly)
        Vector2 avg = (n1 + n2).normalized;

        // Miter correction: push further at sharp angles so the
        // offset distance is correct at the vertex, not just along
        // the normal. miterLen = distance / cos(halfAngle)
        float dot = Vector2.Dot(n1, avg);
        float miter = (dot > 0.1f) ? distance / dot : distance;
        miter = Mathf.Min(miter, distance * 3f); // cap at 3x to prevent spikes

        result[i] = new Vector3(
            contour[i].x + avg.x * miter,
            contour[i].y, // keep same Y
            contour[i].z + avg.y * miter);
    }

    return result;
}
```

**NOTE:** The winding direction matters for the outward normal. The
contours from export are ensureCCW. In Unity after the 90° rotation,
check which direction is outward. If the fringe appears inside the
fairway instead of outside, flip the normal direction:
`Vector2 n1 = new Vector2(-e1.y, e1.x);` instead of `(e1.y, -e1.x)`.

#### Building the fringe mesh

```csharp
// After creating each fairway mesh, create its fringe ring:
Vector3[] innerRing = worldPts; // the fairway contour vertices
Vector3[] outerRing = OffsetContourOutward(worldPts, FairwayFringeMeters);

// Update Y for outer ring (sample terrain height at each outer point)
for (int i = 0; i < outerRing.Length; i++)
{
    float h = terrain.SampleHeight(new Vector3(outerRing[i].x, 0, outerRing[i].z));
    outerRing[i].y = terrainBaseY + h + yOffset;
}

int fn = innerRing.Length;
// Vertices: inner ring + outer ring (relative to centroid)
var fringeVerts = new Vector3[fn * 2];
var fringeUVs = new Vector2[fn * 2];
for (int i = 0; i < fn; i++)
{
    fringeVerts[i] = innerRing[i] - centroid;           // inner
    fringeVerts[fn + i] = outerRing[i] - centroid;       // outer
    fringeUVs[i] = new Vector2(innerRing[i].x / 6f, innerRing[i].z / 6f);
    fringeUVs[fn + i] = new Vector2(outerRing[i].x / 6f, outerRing[i].z / 6f);
}

// Triangles: quad strip between inner and outer ring
var fringeTris = new int[fn * 6];
for (int i = 0; i < fn; i++)
{
    int curr = i;
    int next = (i + 1) % fn;
    int outerCurr = fn + i;
    int outerNext = fn + next;
    int t = i * 6;
    fringeTris[t + 0] = curr;
    fringeTris[t + 1] = outerCurr;
    fringeTris[t + 2] = next;
    fringeTris[t + 3] = next;
    fringeTris[t + 4] = outerCurr;
    fringeTris[t + 5] = outerNext;
}

var fringeMesh = new Mesh();
fringeMesh.name = $"FairwayFringe_{fw.id}";
fringeMesh.vertices = fringeVerts;
fringeMesh.triangles = fringeTris;
fringeMesh.uv = fringeUVs;
fringeMesh.RecalculateNormals();
fringeMesh.RecalculateBounds();

var fringeGO = new GameObject($"FairwayFringe_{fw.id}");
fringeGO.transform.position = centroid;
fringeGO.AddComponent<MeshFilter>().sharedMesh = fringeMesh;
fringeGO.AddComponent<MeshRenderer>().sharedMaterial = semiRoughMat;
fringeGO.AddComponent<MeshCollider>().sharedMesh = fringeMesh;

var fringeMarker = fringeGO.AddComponent<Golfin.Course.SurfaceMarker>();
fringeMarker.surfaceType = Golfin.Course.SurfaceType.SemiRough;

fringeGO.transform.SetParent(fwRoot.transform);
```

Load the semi-rough material the same way as other zone materials:
```csharp
var semiRoughMat = CreateTiledMaterial(texDir, "T_Semirough_Albedo",
    "T_Semirough_Normal", dataDir, projectRoot, 6f);
```

---

### Summary of changes

**File: `HoleLiteImporter.cs`**

1. In `CreateFlatZoneMeshes`:
   - Compute `stripeDir` from anchors + greens (same formula as splatmap)
   - Create `darkFairwayMat` alongside `lightFairwayMat` (T_Fairway_Dark)
   - Create `semiRoughMat` (T_Semirough_Albedo)
   - For each fairway: call mesh creation with two-submesh stripe logic
   - For each fairway: create fringe ring mesh

2. Add `OffsetContourOutward` helper method

3. The fairway mesh creation (either modify `CreateFlatContourMesh` or
   add `CreateFairwayMesh`) needs to:
   - Accept `stripeDir`, `stripeWidth`, `darkMat` parameters
   - Split triangles into light/dark submeshes
   - Set two materials on MeshRenderer

---

### Verification

- [ ] Fairway has alternating light/dark mow stripes running perpendicular
  to tee→green direction (parallel bands, NOT radial from center)
- [ ] Semi-rough fringe ring visible around fairway edge (~1.5m wide)
- [ ] Fringe appears OUTSIDE the fairway (not inside)
- [ ] Stripe direction matches the old splatmap stripes
- [ ] Green collar still looks correct (unchanged)
- [ ] Tee and cart path meshes unaffected
- [ ] No z-fighting between fringe and fairway
- [ ] No console errors

### Do NOT

- Subdivide the mesh along stripe boundaries (accept slight jaggedness
  at stripe edges — the triangle resolution is fine for now)
- Use a custom shader for stripes
- Touch green, bunker, or water meshes
- Modify the export pipeline

---

---

### Implementation Report — Fairway Mow Stripes + Fringe Ring

**Date:** 2026-04-08

#### What was built

1. **Mow stripes** — parallel light/dark fairway bands perpendicular to tee→green axis.
2. **Fairway fringe ring** — a 0.5m semirough border inside the fairway edge.

#### Approach taken (differs from spec)

**Stripes:** The spec proposed a two-submesh approach (T_Fairway_Light + T_Fairway_Dark) with per-triangle centroid classification. This produced radial stripes because centroid-fan triangles are pie slices from center. We tried polygon band slicing (Sutherland-Hodgman clipping into stripe bands), but that bridged across concave curves. We then tried per-triangle splitting at stripe boundaries, but the winding was fragile.

**Final solution:** Cesar created a single `T_Fairway_Mix` texture containing both light and dark bands. UVs are projected onto the stripe axis (`U = dot(worldPos, stripeDir) / stripeWidth`) so the texture naturally creates parallel stripes. One material, no submeshes, no clipping.

**Triangulation:** Centroid-fan extends outside concave polygon boundaries. Replaced with **ear-clipping triangulation** (`EarClipTriangulate`) which always produces triangles within the polygon. Handles all fairway curve shapes correctly.

**Fringe:** Spec proposed semi-rough texture, 1.5m, outside the fairway. Final implementation uses `T_Semirough_Albedo`/`Normal`, 0.5m width, **inside** the fairway edge (negative offset). Uses `OffsetContourOutward` with miter correction, quad-strip mesh between edge ring and offset ring.

#### Key deviations from spec
| Spec | Actual | Reason |
|---|---|---|
| Two materials (light + dark) | Single T_Fairway_Mix | Eliminates all submesh/clipping complexity |
| Centroid-fan triangulation | Ear-clipping | Centroid-fan escapes concave curves |
| Fringe outside, 1.5m, T_Semirough | Inside, 0.5m, T_Semirough | Cesar's preference after visual testing |
| SurfaceType.SemiRough for fringe | SurfaceType.Fringe (new enum) | Distinct surface type added |

#### Files changed
- `HoleLiteImporter.cs` — `CreateFairwayMesh`, `CreateFringeRing`, `OffsetContourOutward`, `EarClipTriangulate`, `CrossXZ`, `PointInTriangleXZ`
- `SurfaceMarker.cs` — added `Fringe` to `SurfaceType` enum

#### Verification
- [x] Parallel mow stripes perpendicular to tee→green
- [x] Stripes stay within fairway contour on all curves
- [x] Semirough fringe ring visible inside fairway edge (0.5m)
- [x] Green collar unchanged
- [x] Tee and cart path meshes unaffected
- [x] No z-fighting
- [x] No console errors

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes (T_Fairway_Mix, ear-clip triangulation) + fringe ring (semirough, 0.5m inward)
✅ DONE: 2026-04-08 — Water Shore Slope
✅ DONE: 2026-04-08 — Tee Markers: FBX props
✅ DONE: 2026-04-08 — Flag + hole cup at green centroid
✅ DONE: 2026-04-08 — Terrain plastic sheen fixed via Mask Map
✅ DONE: 2026-04-08 — Texture cleanup: swap, fringe ring, blur removed, alphamap 1024, zone grid 2048
✅ DONE: 2026-04-08 — PNG + SVG zone import in Hole Viewer
✅ DONE: 2026-04-08 — Morphological close + various smoothing attempts
✅ DONE: 2026-04-08 — Re-enable normal maps (0.4 intensity) + aniso filtering (level 16) on all terrain textures
✅ DONE: 2026-04-08 — SDF-based smooth fairway border (replaced by mesh approach)
✅ DONE: 2026-04-08 — Vector contour rasterization (replaced by mesh approach)
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes with smooth edges
