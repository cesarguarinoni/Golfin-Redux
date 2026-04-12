# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Replace Ear-Clip with CDT for Fairway Meshes

**Problem:** Ear-clip triangulation produces degenerate fan patterns
on large concave fairway polygons. Long sliver triangles span the
entire fairway width, creating visible blade artifacts on slopes
(confirmed via wireframe: all triangles radiate from one vertex).

**Solution:** Replace ear-clip with Constrained Delaunay Triangulation
(CDT) using the BurstTriangulator package. CDT preserves the exact
contour boundary (no staircase) while producing well-shaped interior
triangles.

---

### Step 1: Install BurstTriangulator

Add to `Packages/manifest.json`:
```json
"com.andywiecko.burst.triangulator": "https://github.com/andywiecko/BurstTriangulator.git"
```

This is MIT-licensed. If it requires `com.unity.collections` or
`com.unity.burst` and they're not already present, add those too.
Check the package's `package.json` for dependencies.

### Step 2: New method — CDT triangulation

Add a new method in `HoleLiteImporter.cs`:

```csharp
/// <summary>
/// Constrained Delaunay Triangulation of a polygon defined by contour
/// vertices. Produces well-shaped triangles with the contour as
/// boundary constraints. Interior Steiner points are added at
/// gridSpacing intervals for terrain conformance.
/// Returns triangle indices into the combined vertex array
/// (contour vertices first, then Steiner points).
/// </summary>
using Unity.Collections;
using andywiecko.BurstTriangulator;

private static (Vector3[] verts, Vector2[] uvs, int[] tris)
    CDTTriangulate(
        ContourPoint[] contour,
        Terrain terrain, float terrainBaseY, float yOffset,
        float gridSpacing,
        System.Func<float, float, Vector2> uvFunc)
{
    int n = contour.Length;
    if (n < 3) return (null, null, null);

    // 1. Boundary vertices (2D, XZ plane after 90° CCW rotation)
    var positions2D = new System.Collections.Generic.List<double2>();
    for (int i = 0; i < n; i++)
    {
        float wx = contour[i].z; // 90° CCW rotation
        float wz = contour[i].x;
        positions2D.Add(new double2(wx, wz));
    }

    // 2. Constraint edges (closed polygon: 0→1, 1→2, ..., n-1→0)
    var constraintEdges = new System.Collections.Generic.List<int>();
    for (int i = 0; i < n; i++)
    {
        constraintEdges.Add(i);
        constraintEdges.Add((i + 1) % n);
    }

    // 3. Add interior Steiner points on a grid
    //    (for terrain conformance — without these, CDT only uses
    //    contour vertices and large interior triangles won't follow
    //    terrain undulations)
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    foreach (var pt in contour)
    {
        float wx = pt.z; float wz = pt.x;
        if (wx < minX) minX = wx; if (wx > maxX) maxX = wx;
        if (wz < minZ) minZ = wz; if (wz > maxZ) maxZ = wz;
    }

    // Build 2D contour for point-in-polygon test
    var poly2D = new Vector2[n];
    for (int i = 0; i < n; i++)
        poly2D[i] = new Vector2(contour[i].z, contour[i].x);

    for (float gx = minX + gridSpacing; gx < maxX; gx += gridSpacing)
    {
        for (float gz = minZ + gridSpacing; gz < maxZ; gz += gridSpacing)
        {
            if (IsInsideContour2D(gx, gz, poly2D))
                positions2D.Add(new double2(gx, gz));
        }
    }

    // 4. Run CDT
    using var inputPositions = new NativeArray<double2>(
        positions2D.ToArray(), Allocator.TempJob);
    using var inputConstraints = new NativeArray<int>(
        constraintEdges.ToArray(), Allocator.TempJob);

    using var triangulator = new Triangulator(Allocator.TempJob)
    {
        Input =
        {
            Positions = inputPositions,
            ConstraintEdges = inputConstraints,
        },
        Settings =
        {
            // Remove triangles outside the constrained boundary
            RestoreBoundary = true,
        }
    };

    triangulator.Run();

    var outputTriangles = triangulator.Output.Triangles;
    var outputPositions = triangulator.Output.Positions;

    // 5. Build Unity mesh arrays
    int vertCount = outputPositions.Length;
    var verts = new Vector3[vertCount];
    var uvs = new Vector2[vertCount];

    for (int i = 0; i < vertCount; i++)
    {
        float wx = (float)outputPositions[i].x;
        float wz = (float)outputPositions[i].y; // y in 2D = z in 3D
        float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
        verts[i] = new Vector3(wx, terrainBaseY + th + yOffset, wz);
        uvs[i] = uvFunc(wx, wz);
    }

    // Copy triangles
    var tris = new int[outputTriangles.Length];
    outputTriangles.CopyTo(tris);

    return (verts, uvs, tris);
}

/// <summary>
/// Point-in-polygon test using ray casting (XZ plane).
/// </summary>
private static bool IsInsideContour2D(float px, float pz, Vector2[] poly)
{
    bool inside = false;
    int n = poly.Length;
    for (int i = 0, j = n - 1; i < n; j = i++)
    {
        if (((poly[i].y > pz) != (poly[j].y > pz)) &&
            (px < (poly[j].x - poly[i].x) * (pz - poly[i].y) /
                  (poly[j].y - poly[i].y) + poly[i].x))
            inside = !inside;
    }
    return inside;
}
```

**IMPORTANT NOTE on BurstTriangulator API:** The exact API may differ
from what's shown above. Claude Code MUST check the actual installed
package source after `Step 1` to verify:
- The correct namespace (might be `andywiecko.BurstTriangulator` or
  just `BurstTriangulator`)
- Whether it uses `double2` or `float2` for positions
- The Settings property names (e.g. `RestoreBoundary` might be
  named differently)
- Whether `Triangulator` takes generic type parameter
- How Output.Triangles and Output.Positions are accessed

Read the package's Runtime/*.cs files and any README/samples to
get the correct API before writing code.

### Step 3: Replace CreateFairwayMesh

Rewrite `CreateFairwayMesh` to use CDT instead of ear-clip:

```csharp
private static GameObject CreateFairwayMesh(int id,
    ContourPoint[] contour,
    Terrain terrain, float terrainBaseY,
    Material mat, Vector2 stripeDir, float stripeWidth)
{
    float yOffset = 0.01f;
    Vector2 parallelDir = new Vector2(-stripeDir.y, stripeDir.x);

    // UV function for mow stripes
    System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
        new Vector2(
            (wx * stripeDir.x + wz * stripeDir.y) / stripeWidth,
            (wx * parallelDir.x + wz * parallelDir.y) / stripeWidth);

    var (rawVerts, uvs, tris) = CDTTriangulate(
        contour, terrain, terrainBaseY, yOffset,
        1.0f,  // gridSpacing: 1m Steiner points
        uvFunc);

    if (rawVerts == null || tris == null || tris.Length < 3)
        return null;

    // Center mesh (Y=0 origin pattern)
    float cx = 0, cz = 0;
    for (int i = 0; i < rawVerts.Length; i++)
    { cx += rawVerts[i].x; cz += rawVerts[i].z; }
    cx /= rawVerts.Length; cz /= rawVerts.Length;
    Vector3 centroid = new Vector3(cx, 0, cz);

    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i] -= centroid;

    // Check winding — CDT may output CCW, Unity needs CW
    // for front-face-up. Test first triangle:
    if (tris.Length >= 3)
    {
        Vector3 a = rawVerts[tris[0]];
        Vector3 b = rawVerts[tris[1]];
        Vector3 c = rawVerts[tris[2]];
        float cross = (b.x - a.x) * (c.z - a.z) -
                      (b.z - a.z) * (c.x - a.x);
        if (cross > 0) // CCW → flip all triangles
        {
            for (int t = 0; t < tris.Length; t += 3)
            {
                int tmp = tris[t];
                tris[t] = tris[t + 2];
                tris[t + 2] = tmp;
            }
        }
    }

    var mesh = new Mesh();
    mesh.name = $"Fairway_{id}";
    mesh.vertices = rawVerts;
    mesh.triangles = tris;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"Fairway_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = Golfin.Course.SurfaceType.Fairway;
    return go;
}
```

### Step 4: Replace CreateFlatContourMesh (tees + cart path fallback)

Same pattern — replace ear-clip + SubdivideToTerrain with CDT:

```csharp
private static GameObject CreateFlatContourMesh(int id,
    string zoneName, ContourPoint[] contour,
    Terrain terrain, float terrainBaseY,
    Material mat, float tileSize, float yOffset,
    Golfin.Course.SurfaceType surfaceType)
{
    System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
        new Vector2(wx / tileSize, wz / tileSize);

    var (rawVerts, uvs, tris) = CDTTriangulate(
        contour, terrain, terrainBaseY, yOffset,
        1.0f,  // gridSpacing
        uvFunc);

    if (rawVerts == null || tris == null || tris.Length < 3)
    {
        Debug.LogWarning(
            $"[HoleLiteImporter] {zoneName} {id}: CDT failed");
        return null;
    }

    // Center mesh (Y=0 origin pattern)
    float cx = 0, cz = 0;
    for (int i = 0; i < rawVerts.Length; i++)
    { cx += rawVerts[i].x; cz += rawVerts[i].z; }
    cx /= rawVerts.Length; cz /= rawVerts.Length;
    Vector3 centroid = new Vector3(cx, 0, cz);

    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i] -= centroid;

    // Check winding (same as fairway)
    if (tris.Length >= 3)
    {
        Vector3 a = rawVerts[tris[0]];
        Vector3 b = rawVerts[tris[1]];
        Vector3 c = rawVerts[tris[2]];
        float cross = (b.x - a.x) * (c.z - a.z) -
                      (b.z - a.z) * (c.x - a.x);
        if (cross > 0)
        {
            for (int t = 0; t < tris.Length; t += 3)
            { int tmp = tris[t]; tris[t] = tris[t+2]; tris[t+2] = tmp; }
        }
    }

    var mesh = new Mesh();
    mesh.name = $"{zoneName}_{id}";
    mesh.vertices = rawVerts;
    mesh.triangles = tris;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"{zoneName}_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;
    return go;
}
```

### Step 5: Clean up old methods

After confirming CDT works:
- `SubdivideToTerrain` — DELETE (no longer called by anything)
- `EarClipTriangulate` — KEEP for now (greens/bunkers/water may
  still use it via other code paths). Only delete if truly unused.
- `CrossXZ`, `PointInTriangleXZ` — KEEP if EarClip is kept

### Verification

1. Install package — `Packages/manifest.json` updated, no compile errors
2. Import Hole 4 (hilly) — the worst case for blades
3. **Fairway wireframe:** Well-shaped triangles throughout, NO fan
   pattern, no sliver triangles spanning the full width
4. **Fairway surface:** Smooth, conforms to terrain, no blades
5. **Tee boxes:** Same quality improvement
6. **Cart path (contour fallback):** Working
7. **Fringe ring:** Unchanged (uses ring mesh, not ear-clip)
8. **Green/bunker/water:** Unchanged (don't touch these)
9. Import Hole 1 (flat) — looks identical to before
10. Vertex count: contour verts + Steiner grid points. At 1m spacing
    on a 200m fairway bounding box: ~200×40 = ~8000 interior points
    + ~200 boundary = ~8200 total. Well within mobile budget.

### Do NOT
- Change green, bunker, or water mesh code
- Change the fringe ring or gradient border ring methods
- Change heightmap smoothing or depression code
- Change materials or coordinate transforms
- Use gridSpacing finer than 1.0m (mobile perf)

---

## Current Task — Fix One-Sided Depression Cliff

**Problem:** The terrain depression under fairway overlays only
appears on one side. Root cause: `MarkContourCells` insets the
contour by pulling each vertex toward the **centroid**. On a long
winding fairway the centroid is far from the edges, so the
centroid-pull gives proper inset on one side but barely moves
(or moves wrong) on the opposite side.

**Fix:** Replace the centroid-pull inset with proper edge-
perpendicular polygon offsetting using the existing
`OffsetContourOutward` method (negative distance = inward).

### Changes to `MarkContourCells`

Replace the current centroid-pull inset logic:

```csharp
// REMOVE this block:
float cx = 0, cz = 0;
for (int i = 0; i < n; i++) { cx += contour[i].z; cz += contour[i].x; }
cx /= n; cz /= n;

var worldContour = new Vector2[n];
// ... the loop that pulls toward centroid ...
```

Replace with proper edge-perpendicular inset:

```csharp
private static void MarkContourCells(ContourPoint[] contour,
    bool[,] depress, int hRes, Vector3 terrainPos, Vector3 terrainSize,
    float inset = -1f)
{
    if (inset < 0f) inset = DepressionInsetMeters;
    int n = contour.Length;
    if (n < 3) return;

    // Convert contour to world-space Vector3[] for OffsetContourOutward
    Vector3[] contour3D = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
        contour3D[i] = new Vector3(
            contour[i].z,  // 90° CCW rotation
            0f,
            contour[i].x);
    }

    // Edge-perpendicular inset (negative = inward)
    Vector3[] insetContour3D = OffsetContourOutward(contour3D, -inset);

    // Convert to Vector2 for point-in-polygon + compute bbox
    var worldContour = new Vector2[n];
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    for (int i = 0; i < n; i++)
    {
        float wx = insetContour3D[i].x;
        float wz = insetContour3D[i].z;
        worldContour[i] = new Vector2(wx, wz);
        if (wx < minX) minX = wx;
        if (wx > maxX) maxX = wx;
        if (wz < minZ) minZ = wz;
        if (wz > maxZ) maxZ = wz;
    }

    // Convert bbox to heightmap cell range
    int hMinX = Mathf.Clamp(Mathf.FloorToInt(
        (minX - terrainPos.x) / terrainSize.x * (hRes - 1)), 0, hRes - 1);
    int hMaxX = Mathf.Clamp(Mathf.CeilToInt(
        (maxX - terrainPos.x) / terrainSize.x * (hRes - 1)), 0, hRes - 1);
    int hMinZ = Mathf.Clamp(Mathf.FloorToInt(
        (minZ - terrainPos.z) / terrainSize.z * (hRes - 1)), 0, hRes - 1);
    int hMaxZ = Mathf.Clamp(Mathf.CeilToInt(
        (maxZ - terrainPos.z) / terrainSize.z * (hRes - 1)), 0, hRes - 1);

    // Test each cell in bbox
    for (int hz = hMinZ; hz <= hMaxZ; hz++)
    {
        for (int hx = hMinX; hx <= hMaxX; hx++)
        {
            float cellWorldX = (float)hx / (hRes - 1)
                * terrainSize.x + terrainPos.x;
            float cellWorldZ = (float)hz / (hRes - 1)
                * terrainSize.z + terrainPos.z;
            if (IsInsideContour(cellWorldX, cellWorldZ, worldContour))
                depress[hz, hx] = true;
        }
    }
}
```

The key change: instead of pulling vertices toward centroid by
`inset` meters, we use `OffsetContourOutward(contour3D, -inset)`
which pushes each vertex inward along the perpendicular to its
adjacent edges. This gives uniform inset on ALL sides.

### Verification

1. Import Hole 4 (hilly)
2. Depression should be symmetric — visible equally on both sides
   of the fairway (or ideally NOT visible because it's hidden
   under the mesh)
3. The depression edge should be ~0.7m inside the fairway contour
   (0.5m fringe + 0.2m margin), fully hidden under the fairway mesh
4. Green/bunker/water: unchanged

### Part 2: Fix cart path depression to follow spine geometry

**Problem:** Cart path depression uses the contour polygon from
`cart-paths.json`, but the actual rendered mesh uses the spine
strip (`CreateSpineStripMesh`). On slopes these diverge — the
contour depression doesn't match the rendered strip, leaving
terrain poking through on one side.

**Fix:** Build a depression polygon from the spine's left+right
edge vertices (the same geometry `CreateSpineStripMesh` computes)
and use THAT for depression marking instead of the contour.

In `DepressTerrainUnderOverlays`, replace the cart path section:

```csharp
// Cart path depression — use spine geometry, not contour
string cpPath = Path.Combine(exportPath, "cart-paths.json");
if (File.Exists(cpPath))
{
    var data = JsonUtility.FromJson<CartPathsFile>(
        File.ReadAllText(cpPath));
    if (data.cart_paths != null)
    {
        foreach (var cp in data.cart_paths)
        {
            if (cp.spine != null && cp.spine.Length >= 2)
            {
                // Build polygon from spine left+right edges
                float halfWidth = (cp.width_m > 0
                    ? cp.width_m : 2.5f) / 2f;
                var spinePoly = BuildSpinePolygon(
                    cp.spine, halfWidth);
                if (spinePoly != null)
                    MarkWorldContourCells(spinePoly, depress,
                        hRes, terrainPos, terrainSize);
            }
            else if (cp.contour != null && cp.contour.Length >= 3)
            {
                // Fallback to contour if no spine
                MarkContourCells(cp.contour, depress,
                    hRes, terrainPos, terrainSize);
            }
        }
    }
}
```

New helper methods:

```csharp
/// <summary>
/// Build a closed polygon from a spine centerline + half-width.
/// Returns left edge forward + right edge reversed = closed loop.
/// Same geometry as CreateSpineStripMesh uses.
/// </summary>
private static Vector2[] BuildSpinePolygon(
    ContourPoint[] spine, float halfWidth)
{
    int n = spine.Length;
    if (n < 2) return null;

    var left = new Vector2[n];
    var right = new Vector2[n];

    for (int i = 0; i < n; i++)
    {
        float cx = spine[i].z;  // 90° CCW
        float cz = spine[i].x;

        // Tangent
        float tx, tz;
        if (i == 0)
        { tx = spine[1].z - spine[0].z; tz = spine[1].x - spine[0].x; }
        else if (i == n - 1)
        { tx = spine[n-1].z - spine[n-2].z; tz = spine[n-1].x - spine[n-2].x; }
        else
        { tx = spine[i+1].z - spine[i-1].z; tz = spine[i+1].x - spine[i-1].x; }

        float tLen = Mathf.Sqrt(tx * tx + tz * tz);
        if (tLen > 0.001f) { tx /= tLen; tz /= tLen; }
        else { tx = 1; tz = 0; }

        // Perpendicular (same as CreateSpineStripMesh)
        float px = tz;
        float pz = -tx;

        left[i]  = new Vector2(cx - px * halfWidth,
                               cz - pz * halfWidth);
        right[i] = new Vector2(cx + px * halfWidth,
                               cz + pz * halfWidth);
    }

    // Closed polygon: left forward, then right reversed
    var poly = new Vector2[n * 2];
    for (int i = 0; i < n; i++)
        poly[i] = left[i];
    for (int i = 0; i < n; i++)
        poly[n + i] = right[n - 1 - i];

    return poly;
}

/// <summary>
/// Mark heightmap cells inside a world-space Vector2[] polygon.
/// (No contour conversion or inset — polygon is already in
/// world XZ coords at the desired boundary.)
/// </summary>
private static void MarkWorldContourCells(Vector2[] worldContour,
    bool[,] depress, int hRes, Vector3 terrainPos, Vector3 terrainSize)
{
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    foreach (var v in worldContour)
    {
        if (v.x < minX) minX = v.x;
        if (v.x > maxX) maxX = v.x;
        if (v.y < minZ) minZ = v.y;
        if (v.y > maxZ) maxZ = v.y;
    }

    int hMinX = Mathf.Clamp(Mathf.FloorToInt(
        (minX - terrainPos.x) / terrainSize.x * (hRes - 1)), 0, hRes - 1);
    int hMaxX = Mathf.Clamp(Mathf.CeilToInt(
        (maxX - terrainPos.x) / terrainSize.x * (hRes - 1)), 0, hRes - 1);
    int hMinZ = Mathf.Clamp(Mathf.FloorToInt(
        (minZ - terrainPos.z) / terrainSize.z * (hRes - 1)), 0, hRes - 1);
    int hMaxZ = Mathf.Clamp(Mathf.CeilToInt(
        (maxZ - terrainPos.z) / terrainSize.z * (hRes - 1)), 0, hRes - 1);

    for (int hz = hMinZ; hz <= hMaxZ; hz++)
    {
        for (int hx = hMinX; hx <= hMaxX; hx++)
        {
            float cellWorldX = (float)hx / (hRes - 1)
                * terrainSize.x + terrainPos.x;
            float cellWorldZ = (float)hz / (hRes - 1)
                * terrainSize.z + terrainPos.z;
            if (IsInsideContour(cellWorldX, cellWorldZ, worldContour))
                depress[hz, hx] = true;
        }
    }
}
```

### Verification

1. Import Hole 4 (hilly)
2. **Fairway depression:** Symmetric on both sides, hidden under mesh
3. **Cart path depression:** Follows the actual rendered strip exactly,
   no terrain poking through on either side
4. Cart paths without spines (contour fallback): still depressed
5. Green/bunker/water: unchanged

### Do NOT
- Change CDT or mesh creation code
- Change depression depth (OverlayDepressionMeters)
- Change CreateSpineStripMesh
- Change any other method signatures

---

## Completed Tasks
2. Create a regular grid covering the bbox at `gridSpacing` resolution
   (0.5m — fine enough to match terrain, coarse enough for mobile)
3. For each grid point, test if it's inside the contour polygon
4. For inside points: sample `terrain.SampleHeight()` at that XZ
5. Build a triangle mesh from the grid (two triangles per quad cell)
6. Only emit triangles where ALL 3 vertices are inside the contour
7. Mesh origin at Y=0, each vertex Y = `terrainBaseY + terrainH + yOffset`

```csharp
private static GameObject CreateGridDrapedMesh(
    int id, string zoneName, Vector2[] contourXZ,
    Terrain terrain, float terrainBaseY,
    Material mat, float tileSize, float yOffset,
    Golfin.Course.SurfaceType surfaceType)
{
    const float gridSpacing = 0.5f;

    // 1. Bounding box
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    foreach (var v in contourXZ)
    {
        if (v.x < minX) minX = v.x;
        if (v.x > maxX) maxX = v.x;
        if (v.y < minZ) minZ = v.y;
        if (v.y > maxZ) maxZ = v.y;
    }

    // 2. Grid dimensions
    int gridW = Mathf.CeilToInt((maxX - minX) / gridSpacing) + 1;
    int gridH = Mathf.CeilToInt((maxZ - minZ) / gridSpacing) + 1;

    // 3. Sample grid — test inside DILATED contour + sample height
    var dilatedContour = DilateContour2D(contourXZ, gridSpacing);
    bool[] inside = new bool[gridW * gridH];
    float[] heightAt = new float[gridW * gridH];

    for (int gz = 0; gz < gridH; gz++)
    {
        for (int gx = 0; gx < gridW; gx++)
        {
            float wx = minX + gx * gridSpacing;
            float wz = minZ + gz * gridSpacing;
            int idx = gz * gridW + gx;

            if (IsInsideContour(wx, wz, dilatedContour))
            {
                inside[idx] = true;
                float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
                heightAt[idx] = terrainBaseY + th + yOffset;
            }
        }
    }

    // 4. Build mesh — only emit quads where all 4 corners are inside
    // Centroid for mesh positioning (XZ only)
    float cx = (minX + maxX) * 0.5f;
    float cz = (minZ + maxZ) * 0.5f;

    var verts = new List<Vector3>();
    var uvs = new List<Vector2>();
    var tris = new List<int>();
    var vertMap = new int[gridW * gridH]; // grid index → vert index
    for (int i = 0; i < vertMap.Length; i++) vertMap[i] = -1;

    int GetOrAddVert(int gx, int gz)
    {
        int idx = gz * gridW + gx;
        if (vertMap[idx] >= 0) return vertMap[idx];
        float wx = minX + gx * gridSpacing;
        float wz = minZ + gz * gridSpacing;
        int vi = verts.Count;
        verts.Add(new Vector3(wx - cx, heightAt[idx], wz - cz));
        uvs.Add(new Vector2(wx / tileSize, wz / tileSize));
        vertMap[idx] = vi;
        return vi;
    }

    for (int gz = 0; gz < gridH - 1; gz++)
    {
        for (int gx = 0; gx < gridW - 1; gx++)
        {
            int bl = gz * gridW + gx;
            int br = gz * gridW + gx + 1;
            int tl = (gz + 1) * gridW + gx;
            int tr = (gz + 1) * gridW + gx + 1;

            // Only emit if all 4 corners inside the DILATED contour
            if (!inside[bl] || !inside[br] || !inside[tl] || !inside[tr])
                continue;

            int vBL = GetOrAddVert(gx, gz);
            int vBR = GetOrAddVert(gx + 1, gz);
            int vTL = GetOrAddVert(gx, gz + 1);
            int vTR = GetOrAddVert(gx + 1, gz + 1);

            tris.Add(vBL); tris.Add(vTL); tris.Add(vBR);
            tris.Add(vBR); tris.Add(vTL); tris.Add(vTR);
        }
    }

    if (tris.Count == 0) return null;

    var mesh = new Mesh();
    mesh.name = $"{zoneName}_{id}";
    mesh.vertices = verts.ToArray();
    mesh.triangles = tris.ToArray();
    mesh.uv = uvs.ToArray();
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"{zoneName}_{id}");
    go.transform.position = new Vector3(cx, 0, cz);
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;

    return go;
}
```

Note: `IsInsideContour` already exists in the codebase.
Note: Will need `using System.Collections.Generic;` (already present).

### Part 2 — Fairway variant with stripe UVs

For fairways, the UV computation is different (stripe-oriented).
Add an overload or parameter:

```csharp
private static GameObject CreateGridDrapedFairwayMesh(
    int id, Vector2[] contourXZ,
    Terrain terrain, float terrainBaseY,
    Material mat, Vector2 stripeDir, float stripeWidth, float yOffset)
```

Same grid logic, but UV computation uses stripe projection:
```csharp
uvs.Add(new Vector2(
    (wx * stripeDir.x + wz * stripeDir.y) / stripeWidth,
    (wx * parallelDir.x + wz * parallelDir.y) / stripeWidth));
```

### Part 3 — Wire into existing code

**Replace calls in `CreateFlatZoneMeshes`:**

1. `CreateFairwayMesh` → `CreateGridDrapedFairwayMesh`
   - Convert contour from `ContourPoint[]` to `Vector2[]` with 90° CCW
     rotation first: `new Vector2(contour[i].z, contour[i].x)`

2. `CreateFlatContourMesh` (tees) → `CreateGridDrapedMesh`
   - Same contour conversion

3. `CreateEarClipContourMesh` (cart path fallback) → `CreateGridDrapedMesh`

4. `CreateFringeRing` — Keep the ring approach BUT use grid draping:
   - Build a contour for the outer edge and inner edge
   - Use `CreateGridDrapedMesh` with the outer contour, then subtract
     the inner contour from the inside test
   - OR simpler: keep the ring mesh but set mesh origin to Y=0 and
     sample terrain per-vertex (same pattern as the green fix)
   - **Simplest:** Keep `CreateFringeRing` as-is since it already
     samples per-vertex and is a narrow strip — the faceting is
     minimal on a 0.5m-wide ring

5. `CreateGradientBorderRing` — Same as fringe: keep as-is, the ring
   is narrow enough that per-vertex sampling is fine

6. `CreateSpineStripMesh` (cart path spines) — Already samples per-vertex
   at each spine point. Keep as-is.

**Do NOT change:**
- `CreateRaisedMesh` (green) — already fixed and working
- `CreateContourMesh` (bunker bowls) — different mesh type
- Water meshes — different mesh type

### Part 4 — Remove old methods

After wiring, remove `SubdivideToTerrain` and the old
`CreateFairwayMesh`, `CreateFlatContourMesh`, `CreateEarClipContourMesh`
methods if they're no longer called anywhere.

### Verification

1. Import Hole 3 or 4 (hilly terrain)
2. **Fairway:** Perfectly smooth, follows terrain exactly like a decal
3. **Tee boxes:** Smooth, flush with terrain
4. **Cart paths (contour fallback):** Smooth
5. **Cart paths (spine):** Unchanged (already fine)
6. **Fringe/border rings:** Unchanged (already fine)
7. **Green:** Unchanged (already fixed)
8. Hole 1 (flat): Looks identical to before
9. Check vertex count in console — grid at 0.5m on a 200m fairway
   ≈ ~400×20 = ~8000 verts, well within mobile budget

### Do NOT
- Change green, bunker, or water mesh code
- Change heightmap smoothing
- Change materials or coordinate transforms
- Use gridSpacing finer than 0.5m (mobile perf)

---

## Previous Tasks (DONE)

### Problem 1: Bumpy terrain outside play area
The DEM heightmap (~8m/px at z14) upsampled from 1025→2049 creates
stair-step facets. These are very visible in rough/OB/tree areas.

### Problem 2: Hard step at play/non-play boundary
The transition between play zones and surrounding terrain has a visible
cliff/step where the DEM resolution isn't sufficient.

### Problem 3: Overlay meshes half-sunken into terrain
Fairway and tee meshes only sample terrain height at contour vertices.
Between vertices the mesh interpolates linearly while the terrain curves,
causing the mesh to clip below the terrain surface on hilly holes.

---

### Part 1 — Heightmap Smoothing (in `CreateTerrain`)

After the heightmap is loaded (the `heights[,]` array is populated),
before `terrainData.SetHeights()`, add a smoothing pass:

**Step 1: Build a play-area mask on the heightmap grid**

Load `zones.json` from the export folder (same as splatmap does).
For each heightmap cell `(hx, hz)` in `[0..actualRes)`, map it back to
the zone grid using the same 90° CCW rotation as `ApplySplatmap`:
```
float normX = (float)hx / (actualRes - 1); // terrain X fraction
float normZ = (float)hz / (actualRes - 1); // terrain Z fraction
// Reverse 90° CCW: zone.x = normZ * (zoneW-1), zone.y = normX * (zoneH-1)
int gx = Clamp(Round(normZ * (zoneW - 1)), 0, zoneW - 1);
int gy = Clamp(Round(normX * (zoneH - 1)), 0, zoneH - 1);
int zone = grid[gy * zoneW + gx];
```

Play zones (used for slope calculation — current heights are fine):
**1** (fairway), **2** (green), **6** (bunker),
**7** (water), **8** (cart_path), **10** (tee_box).

Create `bool[] isPlayArea = new bool[actualRes * actualRes]`.
Set `true` for cells matching any play zone.

Note: Play area heights don't need to be exact — they drive slope
calculation, not literal real-world elevation. But we still use them
as the anchor for the transition blend so the boundary is seamless.

**Step 2: Build a blend mask with transition band**

Dilate the play-area mask by `TransitionCells` (const = 40 cells,
~12m at 2049 res over ~600m terrain). Use a distance transform:

```csharp
float[] distToPlay = new float[actualRes * actualRes];
// Initialize: 0 for play cells, float.MaxValue for others
// Forward + backward chamfer pass (same approach as shore slope)
```

Then compute blend factor:
```csharp
float[] blendFactor = new float[actualRes * actualRes];
for each cell:
  if (isPlayArea) blendFactor = 1.0  // keep raw DEM
  else if (dist < TransitionCells)
    blendFactor = dist / TransitionCells  // 0→1 ramp
  else blendFactor = 0.0  // fully smoothed
```

**Step 3: Smooth the heightmap**

Apply a Gaussian blur to the full `heights[,]` array → `smoothedHeights[,]`.
Use the existing `GaussianBlur2D` helper (it operates on `float[,]`).
Parameters: **radius = 8, sigma = 4.0f**.

Note: `GaussianBlur2D` is a private method in `HoleLiteImporter` —
make it `internal` or just call it directly since `CreateTerrain` is
in the same class.

Extract a 2D slice from `heights` → blur it → blend:
```csharp
float[,] smoothed = GaussianBlur2D(heights, actualRes, 8, 4.0f);
for (int z = 0; z < actualRes; z++)
  for (int x = 0; x < actualRes; x++)
  {
      float b = blendFactor[z * actualRes + x];
      heights[z, x] = Mathf.Lerp(smoothed[z, x], heights[z, x], b);
  }
```

This keeps play-area heights as-is (they're already fine for slope
calculation), smoothly transitions at the boundary (no visible step),
and fully smooths outside terrain to remove DEM stair-stepping.

If `zones.json` doesn't exist (no zone data), skip smoothing entirely
(flat terrain fallback already handles this).

**Constants to add at class level:**
```csharp
private const int SmoothRadius = 8;
private const float SmoothSigma = 4.0f;
private const int TransitionCells = 40;
private static readonly HashSet<int> PlayZones =
    new HashSet<int> { 1, 2, 6, 7, 8, 10 };
```

---

### Part 2 — Overlay Mesh Terrain Conformance

**Problem:** `CreateFairwayMesh` and `CreateFlatContourMesh` (tees)
don't subdivide their meshes, so large triangles float above or sink
below the terrain between vertices.

**Fix:** After ear-clip/fan triangulation, call `SubdivideToTerrain`
(already exists and is used by `CreateEarClipContourMesh` for cart paths).

**In `CreateFairwayMesh`**, after ear-clip and before creating the Mesh:
```csharp
var vertList = new System.Collections.Generic.List<Vector3>(verts);
var uvList = new System.Collections.Generic.List<Vector2>(uvs);
var triList = new System.Collections.Generic.List<int>(tris);
SubdivideToTerrain(ref vertList, ref uvList, ref triList,
    centroid, terrain, terrainBaseY, stripeWidth, yOffset, 2.0f);
// Then use vertList/uvList/triList for the Mesh
```

Note: `SubdivideToTerrain` needs a `tileSize` param for UV computation.
For fairway, pass `stripeWidth` (the UV tiling parameter). The subdivision
computes new UVs as `worldPos / tileSize` — but fairway UVs use the
oriented stripe projection (`stripeDir`/`parallelDir`). So we need a
small tweak:

**Add a UV callback overload to `SubdivideToTerrain`**, OR just
recompute UVs for new vertices using the stripe formula:

Simplest approach — after subdivision, recompute ALL UVs using the
stripe orientation:
```csharp
for (int i = 0; i < vertList.Count; i++)
{
    Vector3 wp = vertList[i] + centroid;
    uvList[i] = new Vector2(
        (wp.x * stripeDir.x + wp.z * stripeDir.y) / stripeWidth,
        (wp.x * parallelDir.x + wp.z * parallelDir.y) / stripeWidth);
}
```

**In `CreateFlatContourMesh`** (tees), after fan triangulation:
```csharp
// Convert fan triangulation result to lists
var vertList = new System.Collections.Generic.List<Vector3>(verts);
var uvList = new System.Collections.Generic.List<Vector2>(uvs);
var triList = new System.Collections.Generic.List<int>(tris);
SubdivideToTerrain(ref vertList, ref uvList, ref triList,
    centroid, terrain, terrainBaseY, tileSize, yOffset, 2.0f);
// Then use vertList/uvList/triList for the Mesh
```

### Verification

1. Re-import any hole with elevation changes (try Hole 2, 3, or 4)
2. **Terrain outside play area:** Smooth, no stair-stepping
3. **Play/non-play boundary:** Gradual transition, no cliff/step
4. **Fairway/tee meshes:** Conform to terrain surface, no sinking
5. **Cart paths:** Should already work (already has subdivision)
6. **Greens/bunkers:** Unaffected (they use raised/bowl meshes)

### Do NOT

- Change play-zone heights in a way that breaks slope calculation
- Change zone grid resolution or zone classification
- Modify bunker, green, or water mesh code
- Change the heightmap resolution (2049)

---

## Previous Task (DONE) — Tree Placement System (v2: Mixed Mode)

**Goal:** Place trees on the terrain using zone 5 (trees) mask from
UHole Lite. Support TWO placement modes based on prefab type:

1. **Terrain trees** — prefabs WITH LODGroup → use `terrainData.treePrototypes`
   (automatic billboard LOD, batching)
2. **Standalone trees** — prefabs WITHOUT LODGroup → instantiate as
   GameObjects, sample terrain height, parent under a container

The placer auto-detects which mode to use per prefab.

**Prerequisite:** `tree-zones.json` must exist in the export folder.

### Part 1 — Data Classes

In `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs`, add
(if not already present):

```csharp
[System.Serializable]
public class TreeZonesFile
{
    public string schema_version;
    public int hole_number;
    public int mask_width;
    public int mask_height;
    public string mask_base64;
    public TreeMPP meters_per_pixel;
    public int tree_region_count;
    public TreeRegionData[] tree_regions;
}

[System.Serializable]
public class TreeMPP { public float x; public float z; }

[System.Serializable]
public class TreeRegionData
{
    public int id;
    public int pixel_count;
    public float area_m2;
    public ContourPoint[] contour;
    public AnchorLocal center_local;
    public BunkerSize size_m;
}
```

Also add to manifest: `public string tree_zones_file;`

### Part 2 — TreePlacer.cs

Create `Assets/Scripts/Editor/CourseImporter/TreePlacer.cs`

```csharp
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Golfin.Editor.CourseImporter
{
    public static class TreePlacer
    {
        // All tree prefab paths + weights.
        // Trees marked as standalone are placed as GameObjects
        // (preserves particle systems, complex hierarchies).
        // Others use the terrain tree system (billboard LOD, batching).
        private static readonly string[] TreePrefabPaths = new string[]
        {
            // Trees(2025) — terrain tree system
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_01Cedar.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01_Var1.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Mesh_Metasequoia.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_ScottishPine_01.prefab",
            // Realistic Tree (in Objects/) — standalone (has particles, LOD on child)
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Objects/Spruce/Spruce 1.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Objects/Spruce/Spruce 3.prefab",
        };

        private static readonly float[] TreeWeights =
            { 3f, 3f, 0.5f, 2f, 2f, 1.5f, 1f };

        // Indices into TreePrefabPaths that MUST be standalone GameObjects
        // (terrain tree system strips particle systems and complex hierarchies)
        private static readonly HashSet<int> ForceStandaloneIndices =
            new HashSet<int> { 5, 6 };

        private const float MinSpacing = 6f;
        private const float ScaleMin = 0.85f;
        private const float ScaleMax = 1.15f;

        private static readonly HashSet<int> ExcludeZones = new HashSet<int>
            { 1, 2, 6, 7, 8, 10 };

        public static void PlaceTrees(
            Terrain terrain, float terrainBaseY,
            string exportPath, string zonesJsonPath,
            Transform parentRoot)
        {
            string tzPath = Path.Combine(exportPath, "tree-zones.json");
            if (!File.Exists(tzPath))
            {
                Debug.Log("[TreePlacer] No tree-zones.json found, skipping");
                return;
            }

            var tzData = JsonUtility.FromJson<TreeZonesFile>(
                File.ReadAllText(tzPath));
            if (tzData.tree_region_count == 0 ||
                string.IsNullOrEmpty(tzData.mask_base64))
            {
                Debug.Log("[TreePlacer] No tree zones painted, skipping");
                return;
            }

            byte[] mask = System.Convert.FromBase64String(tzData.mask_base64);
            int maskW = tzData.mask_width;
            int maskH = tzData.mask_height;

            byte[] zoneGrid = null;
            int zoneW = 0, zoneH = 0;
            if (File.Exists(zonesJsonPath))
            {
                var zData = JsonUtility.FromJson<ZonesData>(
                    File.ReadAllText(zonesJsonPath));
                zoneGrid = System.Convert.FromBase64String(zData.grid);
                zoneW = zData.source_dimensions.width;
                zoneH = zData.source_dimensions.height;
            }

            var terrainData = terrain.terrainData;
            float tWidth = terrainData.size.x;
            float tLength = terrainData.size.z;

            // ---- Load prefabs, split by LODGroup presence ----
            var terrainProtos = new List<TreePrototype>();
            var terrainProtoIndices = new List<int>(); // index into TreePrefabPaths

            var standaloneGOs = new List<GameObject>();
            var standaloneIndices = new List<int>();

            var allPrefabs = new GameObject[TreePrefabPaths.Length];

            for (int i = 0; i < TreePrefabPaths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    TreePrefabPaths[i]);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[TreePlacer] Not found: {TreePrefabPaths[i]}");
                    continue;
                }
                allPrefabs[i] = prefab;

                bool hasLOD = prefab.GetComponent<LODGroup>() != null ||
                    prefab.GetComponentInChildren<LODGroup>() != null;
                bool forceStandalone = ForceStandaloneIndices.Contains(i);

                if (hasLOD && !forceStandalone)
                {
                    terrainProtoIndices.Add(i);
                    terrainProtos.Add(new TreePrototype { prefab = prefab });
                }
                else
                {
                    standaloneIndices.Add(i);
                    standaloneGOs.Add(prefab);
                }
            }

            // Register terrain tree prototypes
            if (terrainProtos.Count > 0)
                terrainData.treePrototypes = terrainProtos.ToArray();

            // Build combined weight array (all prefabs)
            float totalWeight = 0;
            var cumulativeWeights = new float[TreePrefabPaths.Length];
            for (int i = 0; i < TreePrefabPaths.Length; i++)
            {
                if (allPrefabs[i] == null) continue;
                float w = i < TreeWeights.Length ? TreeWeights[i] : 1f;
                totalWeight += w;
                cumulativeWeights[i] = totalWeight;
            }

            // ---- Poisson disk sampling ----
            var terrainTrees = new List<TreeInstance>();
            var rng = new System.Random(42);
            float cellSize = MinSpacing;
            int cellsX = Mathf.FloorToInt(tWidth / cellSize);
            int cellsZ = Mathf.FloorToInt(tLength / cellSize);

            // Container for standalone trees
            var standaloneRoot = new GameObject("Trees_Standalone");
            standaloneRoot.transform.SetParent(parentRoot);
            int standaloneCount = 0;

            for (int cz = 0; cz < cellsZ; cz++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    float worldX = (cx + (float)rng.NextDouble()) * cellSize;
                    float worldZ = (cz + (float)rng.NextDouble()) * cellSize;
                    float nx = worldX / tWidth;
                    float nz = worldZ / tLength;
                    if (nx < 0 || nx >= 1 || nz < 0 || nz >= 1) continue;

                    // Check mask
                    int mx = Mathf.Clamp(Mathf.FloorToInt(nx * maskW), 0, maskW - 1);
                    int my = Mathf.Clamp(Mathf.FloorToInt(nz * maskH), 0, maskH - 1);
                    if (mask[my * maskW + mx] == 0) continue;

                    // Check zone exclusion
                    if (zoneGrid != null)
                    {
                        int zx = Mathf.Clamp(Mathf.FloorToInt(nx * zoneW), 0, zoneW - 1);
                        int zy = Mathf.Clamp(Mathf.FloorToInt(nz * zoneH), 0, zoneH - 1);
                        if (ExcludeZones.Contains(zoneGrid[zy * zoneW + zx])) continue;
                    }

                    // Pick prefab (weighted)
                    float roll = (float)rng.NextDouble() * totalWeight;
                    int picked = 0;
                    for (int i = 0; i < cumulativeWeights.Length; i++)
                    {
                        if (allPrefabs[i] != null && roll <= cumulativeWeights[i])
                        { picked = i; break; }
                    }
                    if (allPrefabs[picked] == null) continue;

                    float scale = ScaleMin +
                        (float)rng.NextDouble() * (ScaleMax - ScaleMin);
                    float rotDeg = (float)rng.NextDouble() * 360f;

                    // Check if this is a terrain tree or standalone
                    int terrainIdx = terrainProtoIndices.IndexOf(picked);
                    if (terrainIdx >= 0)
                    {
                        // Terrain tree
                        terrainTrees.Add(new TreeInstance
                        {
                            position = new Vector3(nx, 0f, nz),
                            widthScale = scale,
                            heightScale = scale,
                            rotation = rotDeg * Mathf.Deg2Rad,
                            color = Color.white,
                            lightmapColor = Color.white,
                            prototypeIndex = terrainIdx,
                        });
                    }
                    else
                    {
                        // Standalone GameObject
                        float terrainH = terrain.SampleHeight(
                            new Vector3(worldX, 0, worldZ));
                        var pos = new Vector3(
                            worldX, terrainBaseY + terrainH, worldZ);

                        var inst = (GameObject)PrefabUtility.InstantiatePrefab(
                            allPrefabs[picked]);
                        inst.transform.position = pos;
                        inst.transform.rotation =
                            Quaternion.Euler(0, rotDeg, 0);
                        inst.transform.localScale = Vector3.one * scale;
                        inst.transform.SetParent(standaloneRoot.transform);
                        inst.isStatic = true;
                        standaloneCount++;
                    }
                }
            }

            // Apply terrain trees
            if (terrainTrees.Count > 0)
                terrainData.SetTreeInstances(terrainTrees.ToArray(), true);

            // Unify draw distances
            terrain.treeDistance = 150f;
            terrain.treeBillboardDistance = 80f;
            terrain.treeCrossFadeLength = 20f;
            terrain.treeMaximumFullLODCount = 50;

            // Clean up empty container
            if (standaloneCount == 0)
                Object.DestroyImmediate(standaloneRoot);

            Debug.Log($"[TreePlacer] Placed {terrainTrees.Count} terrain trees + " +
                $"{standaloneCount} standalone trees " +
                $"({MinSpacing}m spacing, seed=42)");
        }

        [MenuItem("GOLFIN/Place Trees (Current Terrain)")]
        private static void PlaceTreesMenuItem()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TreePlacer] No active terrain found");
                return;
            }

            string exportBase = "Tools/UHoleLite/output/lomond-country-club/export";
            string exportPath = null;
            for (int h = 1; h <= 18; h++)
            {
                string candidate = Path.Combine(
                    Application.dataPath, "..", exportBase, $"hole-{h:D2}");
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "tree-zones.json")))
                { exportPath = candidate; break; }
            }

            if (exportPath == null)
            {
                Debug.LogError("[TreePlacer] No tree-zones.json found");
                return;
            }

            // Clear terrain trees
            terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);

            // Clear standalone trees container
            var existing = GameObject.Find("Trees_Standalone");
            if (existing != null) Object.DestroyImmediate(existing);

            // Find parent root (the hole root object)
            Transform parentRoot = terrain.transform.parent ?? terrain.transform;

            float terrainBaseY = terrain.transform.position.y;
            string zonesPath = Path.Combine(exportPath, "zones.json");
            PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);
        }
    }
}
```

### Part 3 — Wire into HoleLiteImporter

After terrain + zone meshes, add:

```csharp
// ---- Trees ----
string zonesJsonExportPath = Path.Combine(exportPath, "zones.json");
TreePlacer.PlaceTrees(terrain, terrainBaseY, exportPath,
    zonesJsonExportPath, holeRoot.transform);
```

Note: `parentRoot` parameter is new (for standalone tree container).

### Verification

1. Re-import Hole 01
2. Console: `[TreePlacer] Placed NN terrain trees + NN standalone trees`
3. Trees(2025) trees: rendered via terrain system (billboard LOD)
4. Realistic Tree spruces: rendered as GameObjects under `Trees_Standalone`
5. All trees follow terrain height, respect zone exclusions
6. GOLFIN > Place Trees clears both types and re-places

### Do NOT

- Force non-LOD prefabs into the terrain tree system
- Change existing zone mesh or terrain code
- Modify the prefabs themselves

---

## Completed Tasks

✅ 2026-04-08 — Fairway mow stripes + fringe ring
✅ 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ 2026-04-08 — Tee border ring with gradient texture
✅ 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ 2026-04-08 — traceBorder direction-aware walk + RDP/Chaikin tuning
✅ 2026-04-09 — Water contour mesh overlay (ear-clip, opaque material)
✅ 2026-04-09 — Cart path contour mesh + min-width dilation
✅ 2026-04-09 — Water shader (URPWater/Standard, animated normals)
✅ 2026-04-09 — Heightmap .raw loader in CreateTerrain
✅ 2026-04-09 — Overlay mesh Y-offsets for DEM terrain
✅ 2026-04-09 — Cart path spine-based strip mesh
✅ 2026-04-09 — Mountain backdrop (single ring, transparent, URP)
✅ 2026-04-09 — Water mesh DEM positioning fix
✅ 2026-04-10 — Bunker v1-v5 iterations → v5 inscribed rectangle terrain hole
✅ 2026-04-10 — Tree placement v1 (terrain trees only, Trees(2025))
✅ 2026-04-10 — Tree placement v2: mixed mode (terrain + standalone), Spruce prefabs, Objects/ scan
✅ 2026-04-11 — Heightmap smoothing (play-area mask + transition blend) + overlay mesh terrain conformance (SubdivideToTerrain for fairway + tee meshes)
✅ 2026-04-11 — Increased overlay mesh Y-offsets (0.02→0.08, fringe 0.09, border 0.07) + reduced SubdivideToTerrain maxEdge 2.0→1.5
✅ 2026-04-11 — Fixed overlay mesh terrain draping: centroid Y now sampled from terrain instead of averaged from vertices (root cause: averaged Y on slopes shifts entire mesh)
✅ 2026-04-11 — Fixed green mesh slope conformance: per-vertex terrain sampling for collar + putting surface, parent Y=0, flag/cup terrain-sampled
✅ 2026-04-11 — Applied Y=0 origin pattern to all 6 overlay mesh methods (fairway, tee, cart path, spine, fringe, border), restored yOffsets to 0.02/0.03/0.01
✅ 2026-04-11 — Replaced subdivision with grid-based terrain draping (0.5m grid) for fairway, tee, and cart path fallback meshes
✅ 2026-04-11 — Grid draping v2: dilated contour (0.5m outward) + all-4-corners rule for clean edges, deleted legacy methods + SubdivideToTerrain
✅ 2026-04-11 — v3: Reverted to ear-clip + subdivision with Y=0 origin fix. Deleted grid methods entirely.
✅ 2026-04-11 — Terrain depression (5cm) under fairway/tee/cart path zones to prevent z-fighting
✅ 2026-04-11 — Terrain depression v2: contour-based (10cm), uses actual contour polygons for exact alignment
✅ 2026-04-11 — Replaced ear-clip with CDT (BurstTriangulator) for fairway, tee, and cart path meshes. Deleted SubdivideToTerrain.
✅ 2026-04-12 — Fixed one-sided depression cliff (MarkContourCells already uses OffsetContourOutward). Cart path depression now follows spine geometry (BuildSpinePolygon + MarkWorldContourCells) with contour fallback.
