# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`
> Full design rationale: `Docs/WATER_REWORK_PLAN.md`

---

## Current Task — Water Rework (Flat CDT Mesh + Contour Depression + Deeper Shore)

Water currently uses ear-clip triangulation with per-vertex terrain
height (uneven surface), its own separate depression system inside
`CreateWaterMeshes()`, and shallow 0.1m shore depth. Rework to use
flat CDT meshes, contour-based depression (same system as fairways),
and deeper natural shore slopes.

**Execute in this order:**

### Step 1: Decouple Terrain Y from ShoreDepthMeters

The terrain GO position uses `-ShoreDepthMeters` for Y. We're bumping
that value, so decouple them.

Add a new constant near the top of the class (next to existing
constants):

```csharp
private const float TerrainYOffset = 0.1f;
```

In `ImportHoleInternal`, find:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -ShoreDepthMeters, -terrainZ / 2f);
```

Change to:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -TerrainYOffset, -terrainZ / 2f);
```

### Step 2: Bump Shore Parameters

Change the existing values at the top of the class:

```csharp
public static int ShoreRadius = 2;
public static float ShoreDepthMeters = 0.1f;
```

To:

```csharp
public static int ShoreRadius = 4;
public static float ShoreDepthMeters = 0.4f;
```

### Step 3: Rewrite `CreateWaterMeshes()` — Flat CDT Mesh

Replace the mesh-building section inside the `foreach (var water ...)`
loop. Keep the method signature, waterRoot creation, terrain/material
setup, and the water.json copy at the end.

**New per-body logic:**

1. Compute flat water Y from minimum terrain height across contour:

```csharp
float minTerrainH = float.MaxValue;
for (int i = 0; i < n; i++)
{
    float wx = water.contour[i].z;  // 90° CCW
    float wz = water.contour[i].x;
    float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
    if (th < minTerrainH) minTerrainH = th;
}
float waterY = terrainBaseY + minTerrainH - 0.05f;
```

2. Use CDT triangulation (same helper as fairways):

```csharp
float tileSize = 10f;
System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
    new Vector2(wx / tileSize, wz / tileSize);

var (rawVerts, uvs, tris) = CDTTriangulate(
    water.contour, terrain, terrainBaseY, 0f, 2.0f, uvFunc);

if (rawVerts == null || tris == null || tris.Length < 3)
{
    Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: CDT failed");
    continue;
}
```

3. Flatten all Y to waterY:

```csharp
for (int i = 0; i < rawVerts.Length; i++)
    rawVerts[i].y = waterY;
```

4. Center mesh (Y=0 origin pattern):

```csharp
float cx = 0, cz = 0;
for (int i = 0; i < rawVerts.Length; i++)
{ cx += rawVerts[i].x; cz += rawVerts[i].z; }
cx /= rawVerts.Length; cz /= rawVerts.Length;
Vector3 centroid = new Vector3(cx, 0, cz);

for (int i = 0; i < rawVerts.Length; i++)
    rawVerts[i] -= centroid;
```

5. Check winding (same as CreateFlatContourMesh):

```csharp
if (tris.Length >= 3)
{
    Vector3 a = rawVerts[tris[0]];
    Vector3 b = rawVerts[tris[1]];
    Vector3 c = rawVerts[tris[2]];
    float cross = (b.x - a.x) * (c.z - a.z)
                - (b.z - a.z) * (c.x - a.x);
    if (cross > 0)
    {
        for (int t = 0; t < tris.Length; t += 3)
        {
            int tmp = tris[t];
            tris[t] = tris[t + 2];
            tris[t + 2] = tmp;
        }
    }
}
```

6. Build mesh + GameObject (same pattern as current, just new data):

```csharp
var mesh = new Mesh();
mesh.name = $"Water_{water.id}";
mesh.vertices = rawVerts;
mesh.triangles = tris;
mesh.uv = uvs;
mesh.RecalculateNormals();
mesh.RecalculateBounds();

var go = new GameObject($"Water_{water.id}");
go.transform.position = centroid;
go.AddComponent<MeshFilter>().sharedMesh = mesh;
go.AddComponent<MeshRenderer>().sharedMaterial = waterMat;
AddCleanMeshCollider(go, mesh);

var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
marker.surfaceType = Golfin.Course.SurfaceType.Water;
go.transform.SetParent(waterRoot.transform);
```

### Step 4: Remove Old Depression from `CreateWaterMeshes()`

Delete the entire shore slope section — everything from:

```csharp
// ─── Shore slope pass: depress terrain near water edges ──────────
```

Through:

```csharp
Debug.Log($"[HoleLiteImporter] Shore slope: depressed {depressedCount} cells, " +
          $"radius={ShoreRadius}, depth={ShoreDepthMeters:F1}m");
```

Keep the water.json copy and final log line after it.

### Step 5: Add Water Depression to `DepressTerrainUnderOverlays()`

In `DepressTerrainUnderOverlays`, after the cart path section and
before the "Apply depression" loop, add water contour marking:

```csharp
// Water contours — flat depression, no inset
string waterDepressPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterDepressPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterDepressPath));
    if (waterData.water != null)
        foreach (var w in waterData.water)
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
}
```

Then, AFTER the existing depression application loop (the one that
applies `dropNormalized` to all `depress[hz,hx]` cells) and AFTER
the cart path gradient section, add shore slope:

```csharp
// ─── Shore slope: gradual ramp around water edges ───────────
if (File.Exists(waterDepressPath))
{
    bool[,] waterMask = new bool[hRes, hRes];
    var waterDataShore = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterDepressPath));
    if (waterDataShore.water != null)
        foreach (var w in waterDataShore.water)
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, waterMask,
                    hRes, terrainPos, terrainSize, 0f);

    // Chamfer distance from water boundary
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z, x - 1] + 1f);
            if (z > 0) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z - 1, x + 1] + 1.414f);
        }

    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z, x + 1] + 1f);
            if (z < hRes - 1) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1) distToWater[z, x] = Mathf.Min(
                distToWater[z, x], distToWater[z + 1, x - 1] + 1.414f);
        }

    // Apply shore slope ramp outside water boundary
    float shoreDropNorm = ShoreDepthMeters / elevRange;
    int shoreCount = 0;
    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue;
            if (depress[z, x]) continue;

            float dist = distToWater[z, x];
            if (dist > 0 && dist <= ShoreRadius)
            {
                float t = 1f - (dist / ShoreRadius);
                t = t * t * (3f - 2f * t); // smoothstep
                float drop = shoreDropNorm * t;
                heights[z, x] = Mathf.Max(0f,
                    heights[z, x] - drop);
                shoreCount++;
            }
        }
    }
    Debug.Log($"[HoleLiteImporter] Shore slope: {shoreCount} cells, " +
              $"radius={ShoreRadius}, depth={ShoreDepthMeters:F1}m");
}
```

### Step 6: Update Water Material Depth Range

In `CreateWaterMaterial()`, change:

```csharp
mat.SetFloat("_DepthEnd", 0.3f);
```

To:

```csharp
mat.SetFloat("_DepthEnd", 0.8f);
```

### Verification

Re-import Hole 01: `Import > Lite > Normal > Import Hole 01 Lite`
Then Hole 12: `Import > Lite > Normal > Import Hole 12 Lite`

- [ ] Water surface is perfectly flat (single Y per body)
- [ ] Water edges follow contour shape (no jaggies)
- [ ] Shore slopes gradually into water (no cliff)
- [ ] No z-fighting between water mesh and terrain
- [ ] URPWater depth coloring works (shallow→deep)
- [ ] Fairways, tees, bunkers, greens, cart paths unaffected
- [ ] No console errors

### Do NOT change:
- Export pipeline (export-hole.mjs)
- CreateWaterMaterial() shader selection (keep URPWater/Standard)
- CreateFlatZoneMeshes() (fairway/tee/cart path mesh creation)
- Bunker or green mesh generation
- Tree placement logic
- Splatmap painting

---

## Completed Tasks
✅ 2026-04-14 — Fix #6: Inverted shore ramp INSIDE water contour — water bed now shallow (flush) at edge, smoothstep to 0.3m deep in interior. Eliminates "floating water" caused by terrain interpolation dipping below water mesh at contour cells (bed was uniformly deep before).
✅ 2026-04-14 — Fix #5: Removed post-ramp box blur. Blur was averaging shore cells with out-of-radius neighbors → RAISED cells near waterline → 3cm cliff causing "floating water" look, asymmetric based on proximity to fairway/depress cells. Wider ShoreRadius=10 alone is sufficient.
✅ 2026-04-14 — Fix #4: ShoreRadius 4→10 + 2-pass 3x3 box blur on shore band to eliminate diagonal stair-stepping asymmetry at water edges.
✅ 2026-04-14 — Fix #3: TerrainYOffset bumped to 0.4f to match ShoreDepthMeters. Previously only 0.1m heightmap headroom below flat → water bed clamped, shore asymmetric. Now flat terrain still at world Y=0 but with full 0.4m below for water bed + shore ramp.
✅ 2026-04-14 — Water bed absolute-Y fix: water cells anchored to per-body waterYNorm-0.3m, shore cells ramp from waterH→origH via smoothstep over ShoreRadius. Also fixed normalizedFlat to use TerrainYOffset (was ShoreDepthMeters → pushed flat baseline to +0.3).
✅ 2026-04-14 — Water rework: flat CDT mesh + contour depression + deeper shore (ShoreRadius=4, ShoreDepthMeters=0.4, TerrainYOffset decoupled, _DepthEnd=0.8)
✅ 2026-04-13 — Cart path flat depression (full width + 0.30m margin, no gradient)
✅ 2026-04-13 — Revert taper, test clean spineExt→spine fix alone
✅ 2026-04-13 — spineExt→spine fix in CreateSpineStripMesh
✅ 2026-04-13 — Node.js residual ramp + boundary height propagation
✅ 2026-04-13 — Cart path depression: 3-strategy fix
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB button
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ 2026-04-12 — Depression cliff fix
✅ 2026-04-11 — Heightmap smoothing + overlay terrain conformance
✅ 2026-04-10 — Tree placement + Bunker iterations
✅ All earlier tasks
