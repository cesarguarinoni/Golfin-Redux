# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`
> Full design rationale: `Docs/CART_PATH_SPLINE_PLAN.md`

---

## Current Task — Spline-Based Cart Path Meshes (HoleGeoImporter)

Replace the current `CreateSpineStripMesh()` cart path approach with
Unity Splines. The `com.unity.splines` package (v2.8.4) is installed.

The export (`cart-paths.json`) already includes `spine` arrays — the
centerline points in local meter coords (already Z-flipped for Unity).

### Overview

For each cart path spine:
1. Create BezierKnots from spine points (with terrain Y)
2. Build a Spline with AutoSmooth tangents
3. Evaluate the spline at dense intervals (~0.5m spacing)
4. At each sample: offset left/right by halfWidth to get strip edges
5. Sample `terrain.SampleHeight()` at each edge vertex
6. Build triangle strip mesh from vertex pairs
7. Apply material, SurfaceMarker, MeshCollider

### Step 1: Add Usings

At the top of `HoleGeoImporter.cs`, add:

```csharp
using UnityEngine.Splines;
using Unity.Mathematics;
```

### Step 2: New Method — `CreateSplineCartPaths()`

Add this method to `HoleGeoImporter`. It replaces the spine strip
mesh creation for cart paths. Keep the existing contour-based CDT
mesh creation for the splatmap/depression system — only the VISIBLE
strip mesh changes.

```csharp
/// <summary>
/// Build cart path strip meshes using Unity Splines for smooth
/// curves and dense terrain-conforming vertex sampling.
/// </summary>
private static void CreateSplineCartPaths(
    TerrainData terrainData, GameObject terrainGO, Transform parent,
    string exportPath, string dataDir, string projectRoot)
{
    string cpPath = Path.Combine(exportPath, "cart-paths.json");
    if (!File.Exists(cpPath)) return;

    var cpData = JsonUtility.FromJson<CartPathsFile>(
        File.ReadAllText(cpPath));
    if (cpData.cart_paths == null || cpData.cart_paths.Length == 0) return;

    var terrain = terrainGO.GetComponent<Terrain>();
    float terrainBaseY = terrainGO.transform.position.y;

    // Cart path material (same as existing)
    var cartMat = CreateCartPathMaterial(dataDir);

    var cartRoot = new GameObject("CartPaths_Spline");
    cartRoot.transform.SetParent(parent);

    int meshCount = 0;

    foreach (var cp in cpData.cart_paths)
    {
        if (cp.spine == null || cp.spine.Length < 2) continue;

        float halfWidth = (cp.width_m > 0 ? cp.width_m : 2.5f) / 2f;
        float sampleSpacing = 0.5f; // meters between samples
        float yOffset = 0.01f;      // sit just above terrain

        // --- Build spline from spine points ---
        // Geo importer: NO 90° rotation (direct mapping)
        // spine points are already in Unity world-local coords
        var knots = new BezierKnot[cp.spine.Length];
        for (int i = 0; i < cp.spine.Length; i++)
        {
            float wx = cp.spine[i].x;
            float wz = cp.spine[i].z;
            float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
            knots[i] = new BezierKnot(
                new float3(wx, terrainBaseY + th, wz));
        }

        var spline = new Spline(knots.Length);
        for (int i = 0; i < knots.Length; i++)
            spline.Add(knots[i]);

        // AutoSmooth tangents — Bézier handles the curve smoothing
        for (int i = 0; i < spline.Count; i++)
            spline.SetTangentMode(i, TangentMode.AutoSmooth);

        // --- Evaluate spline at dense intervals ---
        float splineLength = SplineUtility.CalculateLength(spline);
        if (splineLength < 0.1f) continue;

        int sampleCount = Mathf.Max(2,
            Mathf.CeilToInt(splineLength / sampleSpacing));

        var leftVerts = new List<Vector3>();
        var rightVerts = new List<Vector3>();
        var uvs = new List<Vector2>();
        float tileSize = 4f; // UV tiling for asphalt texture

        float accumulatedDist = 0f;

        for (int s = 0; s <= sampleCount; s++)
        {
            float t = (float)s / sampleCount;
            SplineUtility.Evaluate(spline, t,
                out float3 pos, out float3 tangent, out float3 up);

            // Perpendicular direction in XZ plane
            float3 tangentFlat = math.normalize(
                new float3(tangent.x, 0, tangent.z));

            // Handle degenerate tangent (vertical segment)
            if (math.lengthsq(tangentFlat) < 0.001f)
                tangentFlat = new float3(1, 0, 0);
            else
                tangentFlat = math.normalize(tangentFlat);

            float3 right = math.cross(new float3(0, 1, 0), tangentFlat);
            right = math.normalize(right);

            float3 leftPos = pos - right * halfWidth;
            float3 rightPos = pos + right * halfWidth;

            // Re-sample terrain height at each edge vertex
            float leftH = terrain.SampleHeight(
                new Vector3(leftPos.x, 0, leftPos.z));
            float rightH = terrain.SampleHeight(
                new Vector3(rightPos.x, 0, rightPos.z));

            leftVerts.Add(new Vector3(leftPos.x,
                terrainBaseY + leftH + yOffset, leftPos.z));
            rightVerts.Add(new Vector3(rightPos.x,
                terrainBaseY + rightH + yOffset, rightPos.z));

            // UV: u = cross-path (0 left, 1 right)
            //     v = along-path (tiled by distance)
            if (s > 0)
            {
                float3 prevPos;
                SplineUtility.Evaluate(spline,
                    (float)(s - 1) / sampleCount,
                    out prevPos, out _, out _);
                accumulatedDist += math.distance(pos, prevPos);
            }
            float vCoord = accumulatedDist / tileSize;
            // Left gets u=0, right gets u=1 (added below)
        }

        if (leftVerts.Count < 2) continue;

        // --- Build triangle strip mesh ---
        int vertCount = leftVerts.Count * 2;
        var meshVerts = new Vector3[vertCount];
        var meshUVs = new Vector2[vertCount];

        // Recompute accumulated distance for UVs
        accumulatedDist = 0f;
        for (int i = 0; i < leftVerts.Count; i++)
        {
            if (i > 0)
            {
                Vector3 delta = (leftVerts[i] + rightVerts[i]) * 0.5f -
                                (leftVerts[i-1] + rightVerts[i-1]) * 0.5f;
                accumulatedDist += delta.magnitude;
            }
            float v = accumulatedDist / tileSize;

            meshVerts[i * 2] = leftVerts[i];
            meshVerts[i * 2 + 1] = rightVerts[i];
            meshUVs[i * 2] = new Vector2(0f, v);
            meshUVs[i * 2 + 1] = new Vector2(1f, v);
        }

        // Triangles: quad strip
        int quadCount = leftVerts.Count - 1;
        var tris = new int[quadCount * 6];
        for (int i = 0; i < quadCount; i++)
        {
            int bl = i * 2;
            int br = i * 2 + 1;
            int tl = i * 2 + 2;
            int tr = i * 2 + 3;

            tris[i * 6 + 0] = bl;
            tris[i * 6 + 1] = tl;
            tris[i * 6 + 2] = br;
            tris[i * 6 + 3] = br;
            tris[i * 6 + 4] = tl;
            tris[i * 6 + 5] = tr;
        }

        // Center mesh at centroid (Y=0 origin pattern)
        float cx = 0, cz = 0;
        for (int i = 0; i < meshVerts.Length; i++)
        { cx += meshVerts[i].x; cz += meshVerts[i].z; }
        cx /= meshVerts.Length;
        cz /= meshVerts.Length;
        Vector3 centroid = new Vector3(cx, 0, cz);

        for (int i = 0; i < meshVerts.Length; i++)
            meshVerts[i] -= centroid;

        // Check winding (ensure top-face normals point up)
        if (tris.Length >= 3)
        {
            Vector3 a = meshVerts[tris[0]];
            Vector3 b = meshVerts[tris[1]];
            Vector3 c = meshVerts[tris[2]];
            float cross = (b.x - a.x) * (c.z - a.z) -
                          (b.z - a.z) * (c.x - a.x);
            if (cross > 0)
            {
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int tmp = tris[i];
                    tris[i] = tris[i + 2];
                    tris[i + 2] = tmp;
                }
            }
        }

        var mesh = new Mesh();
        mesh.name = $"CartPath_Spline_{cp.id}";
        mesh.vertices = meshVerts;
        mesh.triangles = tris;
        mesh.uv = meshUVs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject($"CartPath_Spline_{cp.id}");
        go.transform.position = centroid;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = cartMat;
        AddCleanMeshCollider(go, mesh);

        var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
        marker.surfaceType = Golfin.Course.SurfaceType.CartPath;
        go.transform.SetParent(cartRoot.transform);
        meshCount++;
    }

    Debug.Log($"[HoleGeoImporter] Spline cart paths: {meshCount} meshes " +
        $"(sampling every 0.5m)");
}
```

### Step 3: Wire Up in `CreateFlatZoneMeshes()`

In `CreateFlatZoneMeshes()`, find where cart path meshes are currently
created (the `CreateSpineStripMesh` call or whatever builds the cart
path strip). Replace that section with a call to
`CreateSplineCartPaths()`.

The contour-based CDT mesh for cart paths (used for splatmap painting
and terrain depression) should STAY — only the visible strip mesh
changes. If cart paths currently use a single method for both the
CDT contour mesh and the spine strip, you'll need to:
1. Keep the CDT contour mesh for splatmap/depression
2. Replace only the spine strip with the spline mesh
3. Or: if the CDT contour mesh IS the visible mesh, replace it
   entirely with the spline mesh and keep the contour data only for
   splatmap/depression.

Look at how the code is structured and make the cleanest swap.

### Step 4: Material Helper

If `CreateCartPathMaterial()` doesn't exist as a standalone method,
extract the cart path material creation from the existing code into
a reusable method. It should return the asphalt material used for
cart paths (same material as before).

### What NOT to Change

- Contour data in cart-paths.json (still used for splatmap + depression)
- Splatmap painting of cart path texture on terrain
- Terrain depression under cart paths
- Any other zone mesh creation (fairway, tee, green, bunker, water)
- HoleLiteImporter.cs (Geo only for now — port to Lite later)

### Verification

Re-export then reimport Hole 4 via `Import > Geo > Normal`:

```bash
cd Tools/UHoleGeo
node scripts/export-hole.mjs lomond-country-club 4
```

Then in Unity: `Import > Geo > Normal > Import Hole 04 Geo`

- [ ] Cart paths follow smooth curves (no staircase/blocky edges)
- [ ] Cart paths conform to terrain on slopes (no floating/sinking)
- [ ] Consistent width along entire path (no jagged edges)
- [ ] Asphalt texture applied correctly (UV tiling looks natural)
- [ ] SurfaceMarker set to CartPath
- [ ] MeshCollider present
- [ ] Splatmap still paints under cart path (unchanged)
- [ ] Terrain depression still works (unchanged)
- [ ] No console errors
- [ ] Other zone meshes (fairway, tee, green, bunker, water) unaffected

Also test Hole 1 (longer, may have branches):
- [ ] All cart path branches render
- [ ] No gaps at branch junctions
- [ ] Smooth curves throughout

Run all 18 via `Import > Geo > Normal > Import All Holes Geo` to
verify no crashes.

---

## Completed Tasks
❌ 2026-04-16 — Spline cart paths REVERTED. Three fix attempts (centerline sampling, spline Y only, dense knots) all produced worse artifacts than the original CreateSpineStripMesh. Root issue: terrain dip artifact at one bend. Needs architect re-evaluation before another attempt.
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh (dilated CDT + constraint edges + centroid classification)
✅ 2026-04-15 — Clamp fringe/border vertex Y (didn't fix)
✅ 2026-04-15 — Parent-derived Y for fringe/border (didn't fix)
✅ 2026-04-14 — Water rework complete (6 iterations)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
