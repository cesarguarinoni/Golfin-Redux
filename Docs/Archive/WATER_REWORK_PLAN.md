# Water Rework Plan — Spec for Claude Code

> Handoff file: `Docs/TellCode.md`
> Target file: `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
> No pipeline changes needed — `water.json` contour data is fine as-is.

---

## Goal

Rework water to use flat meshes with natural shoreline edges, deeper
terrain depression, and contour-based depression (same system as
fairways/tees). Water surface should be perfectly flat per body (single
Y value), not following terrain slope per-vertex.

---

## Part 1: Change `CreateWaterMeshes()` — Flat Water Surface

Currently each water vertex samples terrain and sits at
`terrainBaseY + terrainH - 0.1f`, creating an uneven surface. Replace
with a flat mesh at a single Y per water body.

### 1A: Compute flat water Y

For each water body, compute the **minimum terrain height** across all
contour vertices. Set water surface Y to that minimum minus a small
offset so water sits just below the lowest shore point:

```csharp
// Replace current per-vertex Y sampling with:
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

### 1B: Build flat mesh with CDT triangulation

Replace the current ear-clip approach with CDT (BurstTriangulator),
same as fairways/tees. This gives interior Steiner points for a
cleaner mesh.

The water mesh should be flat — all vertices at the same Y. Don't
sample terrain per vertex. Use world-position UVs for the water
shader's tiling.

```csharp
// Convert contour to ContourPoint[] format for CDTTriangulate
// Water contour is already in the right format from water.json

// UV function: world-position based (URPWater uses world UV)
float tileSize = 10f;
System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
    new Vector2(wx / tileSize, wz / tileSize);

// CDT with 2.0m grid spacing (water doesn't need fine terrain
// conformance since it's flat, but CDT needs some interior points
// for clean triangulation of large concave shapes)
var (rawVerts, uvs, tris) = CDTTriangulate(
    water.contour, terrain, terrainBaseY, 0f, 2.0f, uvFunc);
```

**After CDT returns**, flatten all vertex Y values to `waterY`:

```csharp
for (int i = 0; i < rawVerts.Length; i++)
    rawVerts[i].y = waterY;
```

Then center the mesh (same Y=0 origin pattern as fairways):

```csharp
float cx = 0, cz = 0;
for (int i = 0; i < rawVerts.Length; i++)
{ cx += rawVerts[i].x; cz += rawVerts[i].z; }
cx /= rawVerts.Length; cz /= rawVerts.Length;
Vector3 centroid = new Vector3(cx, 0, cz);

for (int i = 0; i < rawVerts.Length; i++)
    rawVerts[i] -= centroid;
```

Check winding (same as `CreateFlatContourMesh`):

```csharp
if (tris.Length >= 3)
{
    Vector3 a = rawVerts[tris[0]];
    Vector3 b = rawVerts[tris[1]];
    Vector3 c = rawVerts[tris[2]];
    float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
    if (cross > 0)
    {
        for (int t = 0; t < tris.Length; t += 3)
        { int tmp = tris[t]; tris[t] = tris[t + 2]; tris[t + 2] = tmp; }
    }
}
```

Build mesh, create GameObject at `centroid`, attach MeshFilter,
MeshRenderer (waterMat), `AddCleanMeshCollider`, SurfaceMarker (Water).
Same pattern as current code.

### 1C: Remove old ear-clip path from `CreateWaterMeshes()`

Delete the old vertex loop that computes per-vertex `wy`, the old
`EarClipTriangulate` call, and the old mesh construction. Replace with
the CDT approach from 1B.

---

## Part 2: Move Water Depression into `DepressTerrainUnderOverlays()`

Currently water has its own depression pass inside `CreateWaterMeshes()`
that reads `zones.json`, builds `isWater` mask, does chamfer distance,
and depresses underwater cells. This is a separate system from
fairway/tee depression.

### 2A: Add water contour depression to `DepressTerrainUnderOverlays()`

After the existing fairway and tee sections, add a water section:

```csharp
// Water contours — use same flat depression as fairway/tee
// but with deeper drop + margin for shore slope
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
                    // inset=0 — depress right up to the contour edge
        }
    }
}
```

This puts water cells into the same `depress` array as fairway/tee,
so they get the standard `OverlayDepressionMeters` (0.40m) flat drop.

### 2B: Add shore slope pass after the standard depression

After the existing depression application loop (the `depressedCount`
loop), add a shore slope pass that creates a gradual ramp around
water edges. This makes the shoreline feel natural instead of a
cliff edge.

```csharp
// Shore slope: gradual ramp around water contour edges
// Uses chamfer distance from water depression cells
string waterPathShore = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPathShore))
{
    // Build a water-only mask from the depress array positions
    // that correspond to water contours
    bool[,] waterMask = new bool[hRes, hRes];

    var waterDataShore = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPathShore));
    if (waterDataShore.water != null)
    {
        foreach (var w in waterDataShore.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, waterMask,
                    hRes, terrainPos, terrainSize, 0f);
        }
    }

    // Chamfer distance from water boundary
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x - 1] + 1f);
            if (z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x + 1] + 1.414f);
        }

    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x + 1] + 1f);
            if (z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x - 1] + 1.414f);
        }

    // Apply shore slope ramp OUTSIDE water boundary
    // ShoreRadius cells get smoothstep ramp from 0 (at boundary)
    // to ShoreDepthMeters (at water edge)
    int shoreRadius = ShoreRadius; // 2 cells
    float shoreDropNorm = ShoreDepthMeters / elevRange;

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue; // skip water cells (already depressed)
            if (depress[z, x]) continue;   // skip fairway/tee cells

            float dist = distToWater[z, x];
            if (dist > 0 && dist <= shoreRadius)
            {
                // Smoothstep: full drop at boundary (dist=0),
                // zero drop at shoreRadius
                float t = 1f - (dist / shoreRadius);
                t = t * t * (3f - 2f * t); // smoothstep
                float drop = shoreDropNorm * t;
                heights[z, x] = Mathf.Max(0f,
                    heights[z, x] - drop);
            }
        }
    }
}
```

### 2C: Remove old water depression from `CreateWaterMeshes()`

Delete the entire shore slope section from `CreateWaterMeshes()` —
everything from `// ─── Shore slope pass` through the
`terrainData.SetHeights(0, 0, heights)` call and the
`Debug.Log` about shore slope.

Keep the `water.json` → Assets copy and the final log message.

---

## Part 3: Increase Shore Depression Depth

### 3A: Change `ShoreDepthMeters`

At the top of the class, change:

```csharp
public static float ShoreDepthMeters = 0.1f;
```

To:

```csharp
public static float ShoreDepthMeters = 0.4f;
```

### 3B: Increase `ShoreRadius`

Change:

```csharp
public static int ShoreRadius = 2;
```

To:

```csharp
public static int ShoreRadius = 4;
```

This gives a wider, more gradual shore slope (4 cells × ~0.3m/cell
at 2049 res ≈ 1.2m ramp width).

---

## Part 4: Update `CreateWaterMaterial()` Depth Settings

With deeper terrain depression, the depth shader can now work
properly. Update the depth range:

```csharp
// Was:
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.3f);

// Change to:
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.8f);
```

This gives the depth-based color transition more room to work with
the deeper 0.4m depression.

---

## Part 5: Terrain Position Y Adjustment

Currently `terrainGO.transform.position.y = -ShoreDepthMeters`.
Changing ShoreDepthMeters from 0.1 to 0.4 will drop the entire
terrain by 0.4m instead of 0.1m. This affects ALL overlay
positioning.

**Check:** Is `terrainBaseY` used elsewhere to compensate? Yes — every
overlay samples `terrainBaseY + terrainH`, so increasing the drop
shouldn't break anything since `SampleHeight` returns heights relative
to the terrain object. But verify this in testing.

If the terrain drops too low visually, we can decouple the terrain Y
offset from ShoreDepthMeters. Add a separate constant:

```csharp
private const float TerrainYOffset = 0.1f; // terrain base drop
```

And in `ImportHoleInternal`, use `TerrainYOffset` instead of
`ShoreDepthMeters` for terrain positioning:

```csharp
terrainGO.transform.position = new Vector3(
    -terrainX / 2f, -TerrainYOffset, -terrainZ / 2f);
```

This way shore depth can be tuned independently of terrain position.

---

## Execution Order

1. Part 5 first — decouple terrain Y from ShoreDepthMeters
2. Part 3 — bump ShoreDepthMeters + ShoreRadius
3. Part 2 — move depression into DepressTerrainUnderOverlays
4. Part 1 — rewrite CreateWaterMeshes to flat CDT
5. Part 4 — update material depth settings

---

## Verification

Re-import Hole 01: `Import > Lite > Normal > Import Hole 01 Lite`

- [ ] Water surface is perfectly flat (single Y per body)
- [ ] Water edges follow contour shape cleanly (no jaggies)
- [ ] Shore slopes gradually into water (no cliff)
- [ ] No z-fighting between water mesh and terrain
- [ ] URPWater shader shows depth coloring (shallow→deep gradient)
- [ ] Fairways, tees, bunkers, greens, cart paths unaffected
- [ ] Trees still placed correctly
- [ ] No console errors

Also test a hole with large water (Hole 12 has lake + waterway):
`Import > Lite > Normal > Import Hole 12 Lite`

---

## Do NOT Change

- Export pipeline (`export-hole.mjs`) — contour data is fine
- `CreateWaterMaterial()` shader selection (keep URPWater/Standard)
- `CreateFlatZoneMeshes()` — fairway/tee/cart path unaffected
- Bunker or green mesh generation
- Tree placement logic
- Splatmap painting
