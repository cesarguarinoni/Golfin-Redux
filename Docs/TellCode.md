# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Fix Small Bunker Terrain Poke-Through (Hybrid Approach v2)

**Goal:** Bunker 7 (and any future small bunkers) has terrain poking
through the rim ring because the terrain hole grid (~0.6m/cell at 1024
resolution) overshoots the bowl lip on a bunker with only ~2.8m
radius. Fix by using a **hybrid approach**: small bunkers get a shallow
sand overlay with a collar ring (no terrain hole), large bunkers keep
the current bowl approach.

**v2 fixes from v1 feedback:**
- v1 used `minRadius < 4m` which incorrectly caught Bunker 6 (elongated
  shape, minRadius=2.4m but bbox=8.87×11.31m). Now uses **shorter bbox
  axis < 7m** — only catches Bunker 7 (5.85×5.66m).
- v1 was completely flat with hard edges ("looks square"). Now adds a
  **sand collar ring** around the edge and a **slight central depression**
  (shallow bowl via vertex Y offsets, no terrain hole needed).

### Root Cause

The terrain holes grid is 1024 cells for ~630m terrain = ~0.6m/cell.
At 90% cut scale, small bunkers have a rim ring too narrow to reliably
cover the grid-snapped hole boundary, causing terrain poke-through.

### The Fix: Two-Mode Bunker System

In `CreateZoneMeshes()` in `HoleLiteImporter.cs`, add a size check
for each bunker. Compute the **shorter bounding box axis** from the
exported `size_m` data. Use that to pick the mode:

**Threshold:** `shorterAxis < 7.0f` → shallow overlay mode, else → bowl mode

Hole 1 bunker sizes for reference:
- Bunkers 1-5: shorter axis 9.9–15.5m → BOWL
- Bunker 6: shorter axis 8.9m → BOWL
- Bunker 7: shorter axis 5.7m → SHALLOW OVERLAY

#### Mode A: Shallow Sand Overlay (small bunkers)

- **No terrain hole cut** — skip `SetHoles` for this bunker entirely
- Create the sand surface using `CreateEarClipContourMesh()` with
  sand material and `SurfaceType.Bunker`
- Create a **collar ring** around the bunker edge using the existing
  `CreateGradientBorderRing()` method (same pattern as tee borders)
  with a sand-colored border material. Width: 1.5m.
- Add a **shallow central depression**: after `CreateEarClipContourMesh`
  creates the mesh, modify the vertex Y values to create a gentle bowl
  shape — vertices near the center are pushed down ~0.3m, vertices at
  the edge stay at terrain height. This is purely mesh-level, no
  terrain modification.

For the depression, after the mesh is built in the returned GameObject,
get the MeshFilter, read back the vertices, and apply a radial falloff:

```csharp
// After CreateEarClipContourMesh returns meshGO:
var mf = meshGO.GetComponent<MeshFilter>();
var mesh = mf.sharedMesh;
var verts = mesh.vertices;

// Mesh verts are in local space (relative to centroid)
// Find max radius for normalization
float maxR = 0;
for (int i = 0; i < verts.Length; i++)
{
    float r = Mathf.Sqrt(verts[i].x * verts[i].x + verts[i].z * verts[i].z);
    if (r > maxR) maxR = r;
}

// Push center down, leave edges at terrain height
float shallowDepth = 0.3f;
for (int i = 0; i < verts.Length; i++)
{
    float r = Mathf.Sqrt(verts[i].x * verts[i].x + verts[i].z * verts[i].z);
    float t = maxR > 0.1f ? r / maxR : 0f; // 0=center, 1=edge
    float depression = shallowDepth * (1f - t * t); // quadratic falloff
    verts[i].y -= depression;
}

mesh.vertices = verts;
mesh.RecalculateNormals();
mesh.RecalculateBounds();

// Update collider too
var mc = meshGO.GetComponent<MeshCollider>();
if (mc != null) mc.sharedMesh = mesh;
```

#### Mode B: Bowl (large bunkers) — UNCHANGED

- Current approach: terrain hole at 90% scale, 4-ring bowl mesh
- No changes needed for bunkers with `shorterAxis >= 7.0f`

### Code Changes

In the `foreach (var bunker in bunkersFile.bunkers)` loop inside
`CreateZoneMeshes()`, replace the current body with:

```csharp
foreach (var bunker in bunkersFile.bunkers)
{
    // Apply 90° CCW rotation to contour vertices (same as anchors)
    var worldContour = new Vector2[bunker.contour.Length];
    float sumX = 0, sumZ = 0;
    for (int i = 0; i < bunker.contour.Length; i++)
    {
        float wx = bunker.contour[i].z;  // 90° CCW: worldX = local.z
        float wz = bunker.contour[i].x;  // 90° CCW: worldZ = local.x
        worldContour[i] = new Vector2(wx, wz);
        sumX += wx;
        sumZ += wz;
    }
    float centroidX = sumX / worldContour.Length;
    float centroidZ = sumZ / worldContour.Length;

    // Determine bunker mode by shorter bounding box axis
    float shorterAxis = Mathf.Min(bunker.size_m.x, bunker.size_m.z);
    bool isSmallBunker = shorterAxis < 7.0f;

    if (isSmallBunker)
    {
        // ── Mode A: Shallow sand overlay with collar ──

        // 1. Sand surface mesh (ear-clip, terrain-following)
        var meshGO = CreateEarClipContourMesh(
            bunker.id, "Bunker", bunker.contour,
            terrain, terrainBaseY, sandMat, 4f,
            Golfin.Course.SurfaceType.Bunker);

        if (meshGO != null)
        {
            // 2. Apply shallow central depression
            var mf = meshGO.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var mesh = mf.sharedMesh;
                var verts = mesh.vertices;

                float maxR = 0;
                for (int i = 0; i < verts.Length; i++)
                {
                    float r = Mathf.Sqrt(verts[i].x * verts[i].x +
                                         verts[i].z * verts[i].z);
                    if (r > maxR) maxR = r;
                }

                float shallowDepth = 0.3f;
                for (int i = 0; i < verts.Length; i++)
                {
                    float r = Mathf.Sqrt(verts[i].x * verts[i].x +
                                         verts[i].z * verts[i].z);
                    float t = maxR > 0.1f ? r / maxR : 0f;
                    float depression = shallowDepth * (1f - t * t);
                    verts[i].y -= depression;
                }

                mesh.vertices = verts;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var mc = meshGO.GetComponent<MeshCollider>();
                if (mc != null) mc.sharedMesh = mesh;
            }

            meshGO.transform.SetParent(bunkersRoot.transform);

            // 3. Sand collar ring (soft edge transition)
            //    Reuse CreateGradientBorderRing with sand material
            var collarGO = CreateGradientBorderRing(
                bunker.id, "BunkerCollar", bunker.contour,
                terrain, terrainBaseY, sandMat,
                1.5f,   // border width in meters
                4f,     // UV tile size
                Golfin.Course.SurfaceType.Bunker,
                bunkersRoot.transform);
        }

        Debug.Log($"[HoleLiteImporter] Bunker {bunker.id}: SHALLOW overlay " +
                  $"(shorterAxis={shorterAxis:F1}m < 7m, {bunker.contour.Length} verts)");
    }
    else
    {
        // ── Mode B: Bowl mesh with terrain hole (existing approach) ──

        // Bounding box of contour (for limiting hole-grid search)
        float cMinX = float.MaxValue, cMaxX = float.MinValue;
        float cMinZ = float.MaxValue, cMaxZ = float.MinValue;
        foreach (var v in worldContour)
        {
            if (v.x < cMinX) cMinX = v.x;
            if (v.x > cMaxX) cMaxX = v.x;
            if (v.y < cMinZ) cMinZ = v.y;
            if (v.y > cMaxZ) cMaxZ = v.y;
        }

        // Cut terrain hole at 90% scale
        float cutScale = 0.90f;
        var cutContour = new Vector2[worldContour.Length];
        for (int i = 0; i < worldContour.Length; i++)
        {
            cutContour[i] = new Vector2(
                centroidX + (worldContour[i].x - centroidX) * cutScale,
                centroidZ + (worldContour[i].y - centroidZ) * cutScale);
        }

        int hMinX = Mathf.Clamp(Mathf.FloorToInt((cMinX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
        int hMaxX = Mathf.Clamp(Mathf.CeilToInt((cMaxX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
        int hMinZ = Mathf.Clamp(Mathf.FloorToInt((cMinZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);
        int hMaxZ = Mathf.Clamp(Mathf.CeilToInt((cMaxZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);

        for (int hz = hMinZ; hz <= hMaxZ; hz++)
        {
            for (int hx = hMinX; hx <= hMaxX; hx++)
            {
                float cellWorldX = ((hx + 0.5f) / holesRes) * terrainSize.x + terrainPos.x;
                float cellWorldZ = ((hz + 0.5f) / holesRes) * terrainSize.z + terrainPos.z;

                if (IsInsideContour(cellWorldX, cellWorldZ, cutContour))
                    holes[hz, hx] = false;
            }
        }

        // Generate contour-shaped bowl mesh
        float surfaceY = terrainBaseY + terrain.SampleHeight(
            new Vector3(centroidX, 0, centroidZ));

        float bowlDepth = Mathf.Max(Mathf.Min(defaultDepth, 3f), 0.5f);

        var meshGO = CreateContourMesh(bunker.id, worldContour, centroidX, centroidZ,
            surfaceY, bowlDepth, sandMat, terrain, terrainBaseY);
        meshGO.transform.SetParent(bunkersRoot.transform);

        // Add SurfaceMarker
        var marker = meshGO.AddComponent<Golfin.Course.SurfaceMarker>();
        marker.surfaceType = Golfin.Course.SurfaceType.Bunker;

        Debug.Log($"[HoleLiteImporter] Bunker {bunker.id}: BOWL " +
                  $"(shorterAxis={shorterAxis:F1}m, {bunker.contour.Length} verts)");
    }
}
```

### Data Access Note

`bunker.size_m` comes from the exported JSON. Check that
`BunkerRegionData` in `HoleManifestData.cs` has `size_m` as a
`SizeData` field (it should — other region types have it). If not,
add it:

```csharp
[System.Serializable]
public class BunkerRegionData
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;
    public AnchorLocal center_local;
    public SizeData size_m;          // ← ensure this exists
    // ...
}
```

### Important Notes

1. `CreateEarClipContourMesh` handles 90° CCW rotation internally —
   pass the **original** `bunker.contour` (ContourPoint[]).

2. `CreateGradientBorderRing` also handles 90° CCW internally —
   pass `bunker.contour`.

3. The collar ring uses the same `sandMat` as the surface. This gives
   a unified sand look that feathers into the surrounding terrain.
   The gradient border ring's UV mapping (v=0 at inner, v=1 at outer)
   means the sand texture at the outer edge will look slightly
   different, providing a natural visual transition.

4. The shallow depression (0.3m) is purely vertex-level in the mesh —
   no terrain modification, no terrain hole. The center sinks below
   terrain but the collar ring covers the transition.

5. The `SubdivideToTerrain` call inside `CreateEarClipContourMesh`
   ensures enough vertices for the depression to look smooth even on
   a small bunker.

### Verification

1. Re-import Hole 01: `GOLFIN > Import Hole (Lite) > Hole 01`
2. Console should show:
   - Bunkers 1-6: `BOWL (shorterAxis=X.Xm, ...)`
   - Bunker 7: `SHALLOW overlay (shorterAxis=5.7m < 7m, ...)`
3. Bunker 7: sand pad with soft collar edges, slight central
   depression, no terrain poke-through, no z-fighting
4. Bunker 6: back to bowl mode (not affected by threshold)
5. Bunkers 1-5: unchanged bowl appearance
6. Walk around all bunkers — no visual regressions

### Do NOT

- Change `CreateContourMesh` (bowl mesh generator)
- Change `CreateEarClipContourMesh` (it already works)
- Change the bunker export pipeline (export-hole.mjs)
- Change other zone meshes (green, fairway, tee, water, cart path)
- Remove the terrain hole cutting for large bunkers
- Change `CreateGradientBorderRing` (reuse as-is)

---

## Previous Task — Fix Water Mesh Sunken Too Deep

(Completed — water now samples terrain height at each contour vertex)

### Root Cause

The `CreateWaterMeshes` method depresses terrain heights and positions
the water mesh. With the old flat terrain (elevRange ~1.1m), the shore
depression math worked. With DEM terrain (elevRange ~15-25m), the
relationship between normalized heightmap values and world-space
positions changed.

### Debug First

Add these logs at the start of `CreateWaterMeshes`:
```csharp
Debug.Log($"[Water] terrainBaseY={terrainBaseY:F2}, terrainSize.y={terrainData.size.y:F2}");
Debug.Log($"[Water] ShoreDepthMeters={ShoreDepthMeters:F2}");
// For the first water contour point:
float sampleH = terrain.SampleHeight(new Vector3(firstWaterPoint.x, 0, firstWaterPoint.z));
Debug.Log($"[Water] SampleHeight at water center={sampleH:F2}, world Y={terrainBaseY + sampleH:F2}");
```

### The Fix

The water mesh should be positioned using `terrain.SampleHeight()` at
each contour vertex, same as fairway/tee meshes. The water mesh Y
should be `terrainBaseY + terrain.SampleHeight(pos) - 0.05f` (just
slightly below terrain surface).

The shore depression (modifying terrain heightmap cells near water)
needs to account for the new elevation range. The normalized
depression amount should be:
```csharp
float normalizedDepression = ShoreDepthMeters / terrainData.size.y;
```
NOT a fixed value. Check if `CreateWaterMeshes` hardcodes any
depression values that assumed the old 1.1m elevation range.

### Key things to check in CreateWaterMeshes:

1. How is the water mesh Y position calculated?
2. Is there a hardcoded water level or depression depth?
3. Does the shore slope code use `terrainData.size.y` correctly?
4. Are the contour vertices sampling `terrain.SampleHeight()` like
   other zone meshes do?

The water mesh should work exactly like the fairway mesh — sample
terrain height at each contour point, position slightly below surface.

### Verification
1. Re-import Hole 01
2. Water should sit at terrain surface level (slightly below)
3. Shore slope should be visible around water edges
4. Water shader (URPWater) should look correct

### Do NOT
- Change the heightmap generation pipeline (generate-terrain.mjs)
- Change other zone meshes
- Change the URPWater shader settings

**Goal:** Place a single instance of `Mountains.fbx` around the terrain.
This is a pre-built ring/cylinder mesh designed to surround the playing
area like a skybox backdrop. It needs ONE instance, centered on the
terrain, scaled to fit.

### Assets

- FBX: `Assets/Art/3D/Props/Vegetation/FBX/Mountains.fbx`
- Texture: `Assets/Art/3D/Props/Vegetation/Materials/Mountain.png`
  (try this first — `LandscapesGreen.png` gets stretched)
- NOTE: `LandscapesGreen.mat` is HDRP — do NOT use it. Create a new
  URP Lit material.

### Key issue from first attempt

The first attempt placed 8 separate instances, which is wrong. The FBX
is a single ring mesh. Also `LandscapesGreen.png` stretched badly and
transparency didn't work.

### Approach

Replace the current `PlaceMountainBackdrop` method with a simpler one:

1. **Instantiate ONE Mountains.fbx** at terrain center (0, terrainBaseY, 0)
2. **Create a URP Lit material** with:
   - Albedo: `Mountain.png` (try this first, fall back to `LandscapesGreen.png`)
   - Surface Type: **Transparent** (the texture likely has alpha for sky fade)
   - Set `_Surface` = 1 (transparent)
   - Set `_Blend` = 0 (alpha blend)
   - Set render queue to 3000 (transparent)
   - Enable `_SURFACE_TYPE_TRANSPARENT` and `_ALPHABLEND_ON` keywords
   - Smoothness = 0, Metallic = 0
   - Double-sided: `_Cull` = 0 (Off) — visible from inside AND outside
3. **Scale to match terrain size**:
   - First check the FBX bounds to understand its native size
   - Scale so the ring diameter roughly matches `Mathf.Max(terrainX, terrainZ) * 1.5`
   - The scale factor = desired_diameter / fbx_native_diameter
4. **Y position**: base at `terrainBaseY` (terrain origin Y)

```csharp
private static void PlaceMountainBackdrop(
    Terrain terrain, float terrainBaseY,
    float terrainX, float terrainZ,
    string dataDir,
    Transform parentRoot)
{
    var mountainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/Art/3D/Props/Vegetation/FBX/Mountains.fbx");
    if (mountainPrefab == null)
    {
        Debug.LogWarning("[HoleLiteImporter] Mountains.fbx not found");
        return;
    }

    // Create URP transparent material
    string matPath = $"{dataDir}/MAT_Mountains.mat";
    var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if (existingMat != null) AssetDatabase.DeleteAsset(matPath);

    var mat = new Material(GetLitShader());
    mat.name = "MAT_Mountains";

    // Try Mountain.png first, fall back to LandscapesGreen.png
    var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
        "Assets/Art/3D/Props/Vegetation/Materials/Mountain.png");
    if (albedo == null)
        albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(
            "Assets/Art/3D/Props/Vegetation/Materials/LandscapesGreen.png");
    if (albedo != null) mat.mainTexture = albedo;

    // Enable transparency for sky-fade alpha
    mat.SetFloat("_Surface", 1f); // 0=Opaque, 1=Transparent
    mat.SetFloat("_Blend", 0f);   // 0=Alpha, 1=Premultiply, 2=Additive, 3=Multiply
    mat.SetFloat("_Cull", 0f);    // 0=Off (double-sided)
    mat.SetFloat("_Smoothness", 0f);
    mat.SetFloat("_Metallic", 0f);
    mat.SetFloat("_AlphaClip", 0f); // no alpha clip, smooth blend
    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    mat.EnableKeyword("_ALPHABLEND_ON");
    mat.renderQueue = 3000;
    AssetDatabase.CreateAsset(mat, matPath);

    // Instantiate single ring mesh
    var instance = Object.Instantiate(mountainPrefab);
    instance.name = "MountainBackdrop";

    // Apply material to all renderers
    foreach (var rend in instance.GetComponentsInChildren<Renderer>())
        rend.sharedMaterial = mat;

    // Check FBX bounds to determine native size
    var renderers = instance.GetComponentsInChildren<Renderer>();
    Bounds combinedBounds = new Bounds(Vector3.zero, Vector3.zero);
    bool boundsInit = false;
    foreach (var rend in renderers)
    {
        if (!boundsInit)
        {
            combinedBounds = rend.bounds;
            boundsInit = true;
        }
        else
        {
            combinedBounds.Encapsulate(rend.bounds);
        }
    }

    float nativeDiameter = Mathf.Max(combinedBounds.size.x, combinedBounds.size.z);
    float desiredDiameter = Mathf.Max(terrainX, terrainZ) * 1.5f;
    float scaleFactor = nativeDiameter > 0.01f ? desiredDiameter / nativeDiameter : 1f;

    instance.transform.position = new Vector3(0, terrainBaseY, 0);
    instance.transform.localScale = Vector3.one * scaleFactor;
    instance.transform.SetParent(parentRoot);

    Debug.Log($"[HoleLiteImporter] Mountain backdrop: native={nativeDiameter:F1}m, " +
              $"scale={scaleFactor:F2}, desired={desiredDiameter:F0}m");
}
```

### Verification

1. Re-import Hole 01
2. Mountains should form a single ring around the terrain
3. Top of mountains should fade to transparent (sky visible through)
4. Mountains visible from inside (double-sided rendering)
5. Check scale — mountains should fill the horizon without being
   absurdly large or tiny
6. Console log shows native size and scale factor for tuning

### Do NOT

- Place multiple instances (it's a single ring mesh)
- Use `LandscapesGreen.mat` (it's HDRP)
- Change terrain or zone mesh code
- Use opaque rendering (the texture has alpha for sky fade)

**Goal:** The ear-clip contour approach for cart paths doesn't work
well on sloped terrain — the mesh is a filled polygon with sparse
vertices that can't follow terrain curvature. Replace it with a
**spine-based strip mesh**: extract the path centerline, then extrude
a fixed-width ribbon along it, sampling terrain height at each point.

This produces a mesh that:
- Follows the terrain surface precisely (vertex every ~1m along the path)
- Has consistent width (no dilation artifacts)
- Handles curves and bends naturally
- Never has triangulation issues (simple quad strip)

### Part 1 — Export Side: Extract Path Spine

In `export-hole.mjs`, the cart path contour is currently a closed
polygon (outer boundary of the path). We need to convert this to a
**centerline spine** — an ordered list of points running along the
middle of the path.

**Algorithm: Medial axis from contour polygon**

For a narrow elongated polygon like a cart path, the centerline can
be approximated by:

1. Take the contour polygon vertices (already ordered CCW)
2. Split into two "sides" — the longest edge chain and the
   remaining edge chain. For a path-like shape, these correspond
   to the left edge and right edge.
3. Walk both sides simultaneously, averaging corresponding points
   to get the centerline.

A simpler alternative that works well for our case:

**Skeleton via distance transform:**
1. Build a binary mask from the cart path zone pixels (zone 8)
2. Compute distance transform (distance to nearest edge for each pixel)
3. The ridge of maximum distance = the centerline
4. Trace the ridge as an ordered point sequence
5. Simplify with RDP (same as contour pipeline)
6. Convert to local meter coordinates

But this is complex. **Even simpler — use the contour polygon
directly:**

Since the cart path contour traces around a narrow shape, the
vertices alternate between "left side" and "right side" of the
path. We can split the polygon at its two most distant points
(the endpoints of the path), giving us two edge chains. Average
corresponding points from each chain to get the spine.

**Recommended approach: Paired-edge averaging**

```javascript
function extractPathSpine(contour, pathWidthM) {
  // contour = [{x, z}, ...] in local meters, ordered CCW
  // pathWidthM = approximate width (e.g., 2.5m)
  //
  // 1. Find the two vertices farthest apart (path endpoints)
  // 2. Split contour into two chains at those vertices
  // 3. Resample both chains to equal number of points
  // 4. Average corresponding points → spine

  const n = contour.length;

  // Find the pair of vertices with maximum distance
  let maxDist = 0, iA = 0, iB = 0;
  for (let i = 0; i < n; i++) {
    for (let j = i + 1; j < n; j++) {
      const dx = contour[i].x - contour[j].x;
      const dz = contour[i].z - contour[j].z;
      const d = dx * dx + dz * dz;
      if (d > maxDist) {
        maxDist = d;
        iA = i;
        iB = j;
      }
    }
  }

  // Split into two chains: A→B (forward) and B→A (backward)
  const chainLeft = [];
  for (let i = iA; i !== iB; i = (i + 1) % n) {
    chainLeft.push(contour[i]);
  }
  chainLeft.push(contour[iB]);

  const chainRight = [];
  for (let i = iB; i !== iA; i = (i + 1) % n) {
    chainRight.push(contour[i]);
  }
  chainRight.push(contour[iA]);
  chainRight.reverse(); // so both chains go A→B

  // Resample both chains to the same number of points
  const numSpinePoints = Math.max(chainLeft.length, chainRight.length);
  const leftResampled = resampleChain(chainLeft, numSpinePoints);
  const rightResampled = resampleChain(chainRight, numSpinePoints);

  // Average corresponding points → spine
  const spine = [];
  for (let i = 0; i < numSpinePoints; i++) {
    spine.push({
      x: (leftResampled[i].x + rightResampled[i].x) / 2,
      z: (leftResampled[i].z + rightResampled[i].z) / 2,
    });
  }

  return spine;
}

function resampleChain(chain, targetCount) {
  // Compute cumulative arc lengths
  const arcLengths = [0];
  for (let i = 1; i < chain.length; i++) {
    const dx = chain[i].x - chain[i-1].x;
    const dz = chain[i].z - chain[i-1].z;
    arcLengths.push(arcLengths[i-1] + Math.sqrt(dx*dx + dz*dz));
  }
  const totalLength = arcLengths[arcLengths.length - 1];

  const result = [];
  for (let i = 0; i < targetCount; i++) {
    const targetDist = (i / (targetCount - 1)) * totalLength;

    // Find the segment containing this distance
    let seg = 0;
    while (seg < arcLengths.length - 2 && arcLengths[seg + 1] < targetDist) {
      seg++;
    }

    const segLen = arcLengths[seg + 1] - arcLengths[seg];
    const t = segLen > 0 ? (targetDist - arcLengths[seg]) / segLen : 0;

    result.push({
      x: chain[seg].x + t * (chain[seg + 1].x - chain[seg].x),
      z: chain[seg].z + t * (chain[seg + 1].z - chain[seg].z),
    });
  }

  return result;
}
```

**Export format change:** In `cart-paths.json`, add a `spine` array
alongside the existing `contour`. Keep the contour for backward
compatibility:

```json
{
  "id": 1,
  "pixel_count": 392,
  "contour": [...],
  "spine": [
    {"x": -100.5, "z": -50.2},
    {"x": -98.3, "z": -45.1},
    ...
  ],
  "width_m": 2.5,
  "center_local": {...},
  "size_m": {...}
}
```

Call `extractPathSpine()` after the existing contour extraction in
`exportHole()`, for each cart path region. Also apply RDP
simplification to the spine (epsilon 1.0) then Chaikin smoothing
(2 passes) for a smooth centerline.

### Part 2 — Unity Side: Spine Strip Mesh

In `HoleLiteImporter.cs`, replace `CreateEarClipContourMesh` usage
for cart paths with a new `CreateSpineStripMesh` method.

```csharp
/// <summary>
/// Create a strip mesh along a spine centerline with fixed width.
/// Each spine point generates two vertices (left + right of spine),
/// and each segment creates a quad (two triangles).
/// Terrain height is sampled at every vertex for precise draping.
/// </summary>
private static GameObject CreateSpineStripMesh(
    int id, ContourPoint[] spine, float halfWidth,
    Terrain terrain, float terrainBaseY,
    Material mat, float tileSize,
    Golfin.Course.SurfaceType surfaceType)
{
    int n = spine.Length;
    if (n < 2) return null;

    float yOffset = 0.04f; // small offset, mesh follows terrain closely

    // Build left/right vertex pairs along the spine
    // At each spine point, compute the perpendicular direction
    var verts = new Vector3[n * 2];
    var uvs = new Vector2[n * 2];
    float arcLength = 0;

    for (int i = 0; i < n; i++)
    {
        // 90° CCW rotation: worldX = z, worldZ = x
        float cx = spine[i].z;
        float cz = spine[i].x;

        // Tangent direction (forward along spine)
        float tx, tz;
        if (i == 0)
        {
            tx = spine[1].z - spine[0].z;
            tz = spine[1].x - spine[0].x;
        }
        else if (i == n - 1)
        {
            tx = spine[n-1].z - spine[n-2].z;
            tz = spine[n-1].x - spine[n-2].x;
        }
        else
        {
            tx = spine[i+1].z - spine[i-1].z;
            tz = spine[i+1].x - spine[i-1].x;
        }

        // Normalize tangent
        float tLen = Mathf.Sqrt(tx * tx + tz * tz);
        if (tLen > 0.001f) { tx /= tLen; tz /= tLen; }
        else { tx = 1; tz = 0; }

        // Perpendicular (rotate 90° CW in XZ plane)
        float px = tz;
        float pz = -tx;

        // Left and right positions
        float lx = cx - px * halfWidth;
        float lz = cz - pz * halfWidth;
        float rx = cx + px * halfWidth;
        float rz = cz + pz * halfWidth;

        // Sample terrain at each position
        float lh = terrain.SampleHeight(new Vector3(lx, 0, lz));
        float rh = terrain.SampleHeight(new Vector3(rx, 0, rz));

        verts[i * 2]     = new Vector3(lx, terrainBaseY + lh + yOffset, lz);
        verts[i * 2 + 1] = new Vector3(rx, terrainBaseY + rh + yOffset, rz);

        // UVs: u = 0 (left) to 1 (right), v = arc length for tiling
        if (i > 0)
        {
            float dx = cx - (spine[i-1].z); // world X
            float dz2 = cz - (spine[i-1].x); // world Z
            arcLength += Mathf.Sqrt(dx*dx + dz2*dz2);
        }
        uvs[i * 2]     = new Vector2(0f, arcLength / tileSize);
        uvs[i * 2 + 1] = new Vector2(1f, arcLength / tileSize);
    }

    // Compute centroid for mesh positioning
    float sumX = 0, sumY = 0, sumZ = 0;
    for (int i = 0; i < verts.Length; i++)
    {
        sumX += verts[i].x; sumY += verts[i].y; sumZ += verts[i].z;
    }
    Vector3 centroid = new Vector3(
        sumX / verts.Length, sumY / verts.Length, sumZ / verts.Length);

    // Make vertices relative to centroid
    for (int i = 0; i < verts.Length; i++)
        verts[i] -= centroid;

    // Triangles: quad strip
    int quadCount = n - 1;
    var tris = new int[quadCount * 6];
    for (int i = 0; i < quadCount; i++)
    {
        int bl = i * 2;       // bottom-left
        int br = i * 2 + 1;   // bottom-right
        int tl = (i+1) * 2;   // top-left
        int tr = (i+1) * 2 + 1; // top-right

        int t = i * 6;
        tris[t + 0] = bl;
        tris[t + 1] = tl;
        tris[t + 2] = br;
        tris[t + 3] = br;
        tris[t + 4] = tl;
        tris[t + 5] = tr;
    }

    var mesh = new Mesh();
    mesh.name = $"CartPath_{id}";
    mesh.vertices = verts;
    mesh.triangles = tris;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"CartPath_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;

    return go;
}
```

### Part 3 — Wire It Up

In `CreateFlatZoneMeshes`, replace the cart path section. Find the
block that creates cart path meshes from `cart-paths.json` and change
it to use spine data:

```csharp
// ─── Cart path meshes from cart-paths.json ─────
string cpPath = Path.Combine(exportPath, "cart-paths.json");
if (File.Exists(cpPath))
{
    string cpJson = File.ReadAllText(cpPath);
    var cpData = JsonUtility.FromJson<CartPathsFile>(cpJson);

    if (cpData.cart_paths != null && cpData.cart_paths.Length > 0)
    {
        var cpRoot = new GameObject("CartPaths");
        cpRoot.transform.SetParent(parentRoot);

        var cpMat = CreateTiledMaterial(texDir, "T_RoadAsphalt_Albedo",
            "T_RoadAsphalt_Normal", dataDir, 4f);
        cpMat.SetFloat("_Smoothness", 0.3f);

        foreach (var region in cpData.cart_paths)
        {
            // Prefer spine if available, fall back to ear-clip contour
            if (region.spine != null && region.spine.Length >= 2)
            {
                float halfWidth = (region.width_m > 0 ? region.width_m : 2.5f) / 2f;
                var meshGO = CreateSpineStripMesh(
                    region.id, region.spine, halfWidth,
                    terrain, terrainBaseY, cpMat, 4f,
                    Golfin.Course.SurfaceType.CartPath);
                if (meshGO != null)
                    meshGO.transform.SetParent(cpRoot.transform);
            }
            else if (region.contour != null && region.contour.Length >= 3)
            {
                // Fallback to ear-clip (backward compatibility)
                var meshGO = CreateEarClipContourMesh(
                    region.id, "CartPath", region.contour,
                    terrain, terrainBaseY, cpMat, 4f,
                    Golfin.Course.SurfaceType.CartPath);
                if (meshGO != null)
                    meshGO.transform.SetParent(cpRoot.transform);
            }
        }

        Debug.Log($"[HoleLiteImporter] Created {cpData.cart_paths.Length} cart path mesh(es)");
    }
}
```

### Part 4 — Data Classes

Add `spine` and `width_m` to `CartPathRegionData` in
`HoleManifestData.cs`:

```csharp
[System.Serializable]
public class CartPathRegionData
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;
    public ContourPoint[] spine;     // NEW: centerline spine points
    public float width_m;           // NEW: path width in meters
    public AnchorLocal center_local;
    public SizeData size_m;
    public bool dilated;
}
```

### Verification

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
   - `cart-paths.json` should now have `spine` arrays
2. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
3. Cart path should:
   - Follow terrain surface precisely (no ridges, no floating)
   - Have consistent width along its length
   - Curve smoothly through bends
   - Have clean quad-strip geometry (no triangulation artifacts)
4. Check from all angles — path hugs terrain, no gaps visible

### Do NOT

- Remove the ear-clip fallback (needed for backward compatibility)
- Change the cart path contour export (keep `contour` in JSON)
- Change other mesh types (fairway, tee, bunker, green)
- Remove `SubdivideToTerrain` or `CreateEarClipContourMesh`
  (they may be useful for other mesh types later)
- Change the DEM/heightmap pipeline

---

## Previous Task — Cart Path: Contour Mesh Overlay with Minimum Width

**Goal:** Replace splatmap-only cart paths with contour mesh overlays
(same system as fairway/water/green/bunker), plus a minimum-width
enforcement of 2.5m (standard golf cart path width). Narrow painted
regions get dilated up to 2.5m instead of being skipped.

Splatmap paint for cart paths stays (zone 8 → layer 6 in `ZoneToLayer`)
— it provides the grass-beneath-asphalt ground. The mesh overlay sits
on top, giving the path physical presence and smooth edges.

### Part 1 — Export Side (`Tools/UHoleLite/scripts/export-hole.mjs`)

Add a new function `extractCartPathContours()` that wraps
`extractZoneContours` with a **minimum-width dilation** step.

```javascript
/**
 * Extract cart path contours with minimum width enforcement.
 * If a region is narrower than minWidthPx, dilate it until it reaches
 * the minimum. This prevents thin hand-painted paths from producing
 * degenerate contours.
 *
 * @param {object} zonesData - zones.json data (grid, source_dimensions)
 * @param {object} terrainMeta - terrain-meta.json data
 * @param {number} minWidthM - minimum path width in meters (default 2.5)
 * @param {number} minPixels - minimum region size in pixels
 * @param {number} rdpEpsilon - RDP simplification epsilon
 * @param {number} smoothPasses - Chaikin smoothing passes
 */
function extractCartPathContours(zonesData, terrainMeta, minWidthM = 2.5, minPixels = 15, rdpEpsilon = 1.0, smoothPasses = 2) {
  const grid = Buffer.from(zonesData.grid, 'base64');
  const w = zonesData.source_dimensions.width;
  const h = zonesData.source_dimensions.height;
  const tw = terrainMeta.terrain_width_m;
  const tl = terrainMeta.terrain_length_m;
  const targetZone = 8; // cart path

  // Meters per pixel
  const mppX = tw / w;
  const mppY = tl / h;
  const mpp = (mppX + mppY) / 2; // average
  const minWidthPx = Math.ceil(minWidthM / mpp);

  // Step 1: Find all cart path pixels, flood-fill into regions
  const visited = new Uint8Array(w * h);
  const regions = [];

  function floodFill(startX, startY) {
    const pixels = [];
    const stack = [[startX, startY]];
    while (stack.length > 0) {
      const [x, y] = stack.pop();
      if (x < 0 || x >= w || y < 0 || y >= h) continue;
      const idx = y * w + x;
      if (visited[idx] || grid[idx] !== targetZone) continue;
      visited[idx] = 1;
      pixels.push([x, y]);
      stack.push([x + 1, y], [x - 1, y], [x, y + 1], [x, y - 1]);
    }
    return pixels;
  }

  for (let y = 0; y < h; y++) {
    for (let x = 0; x < w; x++) {
      if (grid[y * w + x] === targetZone && !visited[y * w + x]) {
        const pixels = floodFill(x, y);
        if (pixels.length >= minPixels) {
          regions.push(pixels);
        }
      }
    }
  }

  // Step 2: For each region, check width and dilate if needed
  // Then run the standard contour pipeline on the (possibly dilated) pixels
  const results = [];

  for (const originalPixels of regions) {
    // Create a local mask for this region
    let pixelSet = new Set();
    for (const [px, py] of originalPixels) {
      pixelSet.add(py * w + px);
    }

    // Estimate width: area / bounding-box-diagonal-length
    // Better approach: use the longer bbox dimension as "length",
    // then width ≈ area / length
    let minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
    for (const [px, py] of originalPixels) {
      if (px < minX) minX = px;
      if (px > maxX) maxX = px;
      if (py < minY) minY = py;
      if (py > maxY) maxY = py;
    }
    const bboxW = maxX - minX + 1;
    const bboxH = maxY - minY + 1;
    const longerAxis = Math.max(bboxW, bboxH);
    const estWidthPx = originalPixels.length / longerAxis;

    // Dilate if too narrow
    let currentPixels = originalPixels;
    if (estWidthPx < minWidthPx) {
      const dilateRadius = Math.ceil((minWidthPx - estWidthPx) / 2);
      console.log(`    Cart path region: est width ${(estWidthPx * mpp).toFixed(1)}m < ${minWidthM}m, dilating by ${dilateRadius}px`);

      // Build a mask, dilate it, extract new pixel list
      // Only dilate into non-zone pixels (rough/semi-rough/trees) — never
      // into other features like fairway, green, bunker, water, tee, OB
      const safeZones = new Set([0, 3, 4, 5, 9]); // background, semi-rough, rough, trees, OB
      const dilated = new Set(pixelSet);

      for (let r = 0; r < dilateRadius; r++) {
        const frontier = [];
        for (const key of dilated) {
          const py = Math.floor(key / w);
          const px = key % w;
          const neighbors = [[px-1,py],[px+1,py],[px,py-1],[px,py+1]];
          for (const [nx, ny] of neighbors) {
            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
            const nKey = ny * w + nx;
            if (dilated.has(nKey)) continue;
            // Only expand into safe zones
            if (safeZones.has(grid[nKey])) {
              frontier.push(nKey);
            }
          }
        }
        for (const key of frontier) {
          dilated.add(key);
        }
      }

      // Convert back to pixel array
      currentPixels = [];
      for (const key of dilated) {
        currentPixels.push([key % w, Math.floor(key / w)]);
      }
      pixelSet = dilated;
    }

    // Step 3: Run standard contour pipeline on this region
    const borderPixels = traceBorder(grid, w, h, currentPixels, targetZone);
    // NOTE: traceBorder uses borderSet based on pixelSet internally.
    // Since we may have dilated pixels that don't match the grid value,
    // we need to pass the dilated pixel list directly.
    // traceBorder's 5th param (zoneValue) is unused in the current
    // direction-aware implementation — it uses the pixelSet built from
    // the `pixels` parameter. So this should work as-is.

    // Convert to meters
    let contourMeters = borderPixels.map(([bx, by]) => ({
      x: parseFloat(((bx / (w - 1) - 0.5) * tw).toFixed(2)),
      z: parseFloat(((by / (h - 1) - 0.5) * tl).toFixed(2)),
    }));

    if (contourMeters.length < 3) continue;

    // RDP + Chaikin
    const closed = [...contourMeters, contourMeters[0]];
    let simplified = simplifyPolygon(closed, rdpEpsilon);
    if (simplified.length > 1 &&
        simplified[0].x === simplified[simplified.length - 1].x &&
        simplified[0].z === simplified[simplified.length - 1].z) {
      simplified = simplified.slice(0, -1);
    }
    contourMeters = smoothPolygon(simplified, smoothPasses);
    contourMeters = ensureCCW(contourMeters);

    // Bounding box in local meters
    const normCX = (minX + maxX) / 2 / (w - 1);
    const normCY = (minY + maxY) / 2 / (h - 1);
    const normW = (maxX - minX + 1) / w;
    const normH = (maxY - minY + 1) / h;

    results.push({
      id: results.length + 1,
      pixel_count: currentPixels.length,
      contour: contourMeters,
      center_local: {
        x: parseFloat(((normCX - 0.5) * tw).toFixed(2)),
        z: parseFloat(((normCY - 0.5) * tl).toFixed(2)),
      },
      size_m: {
        x: parseFloat((normW * tw).toFixed(2)),
        z: parseFloat((normH * tl).toFixed(2)),
      },
      center_normalized: {
        x: parseFloat(normCX.toFixed(4)),
        y: parseFloat(normCY.toFixed(4)),
      },
      size_normalized: {
        w: parseFloat(normW.toFixed(4)),
        h: parseFloat(normH.toFixed(4)),
      },
      dilated: estWidthPx < minWidthPx,
    });
  }

  // Sort by size (largest first), re-assign IDs
  results.sort((a, b) => b.pixel_count - a.pixel_count);
  results.forEach((r, i) => { r.id = i + 1; });

  return results;
}
```

Then in `exportHole()`, replace the cart path extraction in the
zone-contours section. Find:

```javascript
const cartPaths = extractZoneContours(zonesData, terrainMeta, 8, 15, 1.5, 3);
```

Replace with:

```javascript
const cartPaths = extractCartPathContours(zonesData, terrainMeta, 2.5, 15, 1.0, 2);
// 2.5m min width, 15 min pixels, RDP epsilon 1.0 (preserve narrow shape), 2 Chaikin passes
```

Also add a **separate export file** for cart paths so Unity can import
them independently (like bunkers, greens, water):

```javascript
// --- Build cart-paths.json ---
const cartPathsOutput = {
  schema_version: '1.0.0',
  hole_number: holeNumber,
  cart_path_count: cartPaths.length,
  min_width_m: 2.5,
  cart_paths: cartPaths,
};

fs.writeFileSync(
  path.join(exportDir, 'cart-paths.json'),
  JSON.stringify(cartPathsOutput, null, 2),
  'utf-8'
);

if (cartPaths.length > 0) {
  const stats = cartPaths.map(c =>
    `#${c.id}: ${c.contour.length}pts (${c.pixel_count}px${c.dilated ? ', dilated' : ''})`
  ).join(', ');
  console.log(`  Cart path contours: ${stats}`);
}
```

Update the manifest to include `cart_paths_file: 'cart-paths.json'`.

**Keep cart paths in zone-contours.json too** for backward compatibility,
but the Unity importer should read from `cart-paths.json`.

### Part 2 — Unity Side (`Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`)

Add cart path mesh creation in `CreateFlatZoneMeshes()`. After the tee
mesh section, add:

```csharp
// ─── Cart path meshes from cart-paths.json ─────
string cpPath = Path.Combine(exportPath, "cart-paths.json");
if (File.Exists(cpPath))
{
    string cpJson = File.ReadAllText(cpPath);
    var cpData = JsonUtility.FromJson<CartPathsFile>(cpJson);

    if (cpData.cart_paths != null && cpData.cart_paths.Length > 0)
    {
        var cpRoot = new GameObject("CartPaths");
        cpRoot.transform.SetParent(parentRoot);

        var cpMat = CreateTiledMaterial(texDir, "T_RoadAsphalt_Albedo",
            "T_RoadAsphalt_Normal", dataDir, 4f);
        // Override smoothness for asphalt — slightly glossy
        cpMat.SetFloat("_Smoothness", 0.3f);

        foreach (var region in cpData.cart_paths)
        {
            if (region.contour == null || region.contour.Length < 3) continue;

            // Use ear-clip (cart paths are narrow/winding = concave)
            var meshGO = CreateFairwayMesh(
                region.id, region.contour,
                terrain, terrainBaseY,
                cpMat,
                new Vector2(1, 0), // stripe dir doesn't matter for asphalt
                4f); // tile size for asphalt texture

            if (meshGO != null)
            {
                meshGO.name = $"CartPath_{region.id}";
                // Override surface marker
                var marker = meshGO.GetComponent<Golfin.Course.SurfaceMarker>();
                if (marker != null)
                    marker.surfaceType = Golfin.Course.SurfaceType.CartPath;
                meshGO.transform.SetParent(cpRoot.transform);
            }
        }

        Debug.Log($"[HoleLiteImporter] Created {cpData.cart_paths.Length} cart path mesh(es)");
    }
}
```

Alternatively, instead of reusing `CreateFairwayMesh` (which has
mow-stripe UV logic), create a simpler ear-clip mesh without stripe
UVs — just standard world-position-based tiling. You can use
`CreateFlatContourMesh` directly if it works, or make a variant
that uses ear-clip instead of centroid-fan:

```csharp
private static GameObject CreateEarClipContourMesh(int id, string zoneName,
    ContourPoint[] contour, Terrain terrain, float terrainBaseY,
    Material mat, float tileSize, Golfin.Course.SurfaceType surfaceType)
{
    int n = contour.Length;
    if (n < 3) return null;

    float yOffset = 0.015f; // between terrain and other overlays

    // 90° CCW rotation
    Vector3[] worldPts = new Vector3[n];
    for (int i = 0; i < n; i++)
    {
        float wx = contour[i].z;
        float wz = contour[i].x;
        float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
        worldPts[i] = new Vector3(wx, terrainBaseY + th + yOffset, wz);
    }

    // Centroid
    float cx = 0, cy = 0, cz = 0;
    for (int i = 0; i < n; i++)
    { cx += worldPts[i].x; cy += worldPts[i].y; cz += worldPts[i].z; }
    cx /= n; cy /= n; cz /= n;
    Vector3 centroid = new Vector3(cx, cy, cz);

    var verts = new Vector3[n];
    var uvs = new Vector2[n];
    for (int i = 0; i < n; i++)
    {
        verts[i] = worldPts[i] - centroid;
        uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
    }

    var tris = EarClipTriangulate(worldPts);
    if (tris == null || tris.Length < 3) return null;

    var mesh = new Mesh();
    mesh.name = $"{zoneName}_{id}";
    mesh.vertices = verts;
    mesh.triangles = tris;
    mesh.uv = uvs;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"{zoneName}_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    go.AddComponent<MeshCollider>().sharedMesh = mesh;

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;

    return go;
}
```

Use it:
```csharp
var meshGO = CreateEarClipContourMesh(
    region.id, "CartPath", region.contour,
    terrain, terrainBaseY, cpMat, 4f,
    Golfin.Course.SurfaceType.CartPath);
```

### Part 3 — Data Classes

Add to `HoleManifestData.cs`:

```csharp
[System.Serializable]
public class CartPathsFile
{
    public string schema_version;
    public int hole_number;
    public int cart_path_count;
    public float min_width_m;
    public CartPathRegionData[] cart_paths;
}

[System.Serializable]
public class CartPathRegionData
{
    public int id;
    public int pixel_count;
    public ContourPoint[] contour;
    public AnchorLocal center_local;
    public SizeData size_m;
    public bool dilated;
}
```

Also check that `SurfaceType` enum has a `CartPath` entry. If not, add
it (check `Assets/Scripts/Course/SurfaceMarker.cs` or similar).

### Part 4 — Keep Splatmap

**Do NOT** change `ZoneToLayer` for zone 8. Cart path splatmap paint
stays at layer 6 (T_RoadAsphalt_Albedo). The mesh overlay sits on top.
This gives natural terrain-under-asphalt and a visible edge transition.

### Verification

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
   - Should log `Cart path contours: #N: NNpts (NNpx)` or `dilated`
   - `cart-paths.json` should exist in export dir
2. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
   - Cart paths should be visible as raised asphalt mesh on terrain
   - Splatmap asphalt still visible underneath (no gap)
   - Walk along path — smooth edges, no jaggies
3. Check a hole that has narrow cart paths — verify dilation kicks in
   and the path is at least 2.5m wide

### Do NOT

- Remove cart path from splatmap pipeline (zone 8 → layer 6 stays)
- Change `traceBorder`, `simplifyPolygon`, `smoothPolygon`, `ensureCCW`
- Modify bunker, green, fairway, or water pipeline code
- Change `EarClipTriangulate`
- Skip small cart path regions — dilate them up to 2.5m instead

---

## Previous Completed Tasks

✅ DONE: 2026-04-08 — Fairway mow stripes + fringe ring
✅ DONE: 2026-04-08 — Zone overlay meshes: fairway + tee as contour meshes
✅ DONE: 2026-04-08 — Tee border ring with gradient texture
✅ DONE: 2026-04-08 — All earlier tasks (water, bunkers, greens, textures, etc.)
✅ DONE: 2026-04-08 — traceBorder replaced with direction-aware walk + RDP epsilon 3.0→1.0, Chaikin 3→2. BIG DIFF at z=50 eliminated (-5.4→-1.2m). Note: trace was not the root cause — the 22.1% diagnostic was misleading (counted interior border pixels). Real fix was RDP reduction. One BIG DIFF remains at z=-5 (narrow tip, -5.2m).
✅ DONE: 2026-04-09 — Water: replaced rasterized quad + SDF alpha mask with contour mesh overlay. Export uses extractZoneContours (zone 7, epsilon 2.0, 2 Chaikin passes). Unity importer uses ear-clip triangulation + opaque water material. Shore slope depression preserved unchanged.
✅ DONE: 2026-04-09 — Cart Path: contour mesh overlay with min-width enforcement. New extractCartPathContours() with 2.5m min-width dilation. Separate cart-paths.json export. Unity CreateEarClipContourMesh for concave paths. Splatmap layer 6 preserved underneath. Hole 1: 1 cart path region, 392pts, no dilation needed.
✅ DONE: 2026-04-09 — Water Shader: replaced URP/Lit with URPWater/Standard. Animated normals (T_Water_03_N.tga), depth-based coloring, edge fade, probe reflections. Fallback to URP/Lit if shader missing. URP depth/opaque texture warnings logged if OFF.
✅ DONE: 2026-04-09 — Load Heightmap from .raw in CreateTerrain. Reads uint16be heightmap.raw (1025x1025) from export folder, maps to terrain with DEM elevation range + shore depression headroom. Direct load when rawRes==actualRes, bilinear upsample fallback for mismatched resolutions. Flat terrain fallback preserved.
✅ DONE: 2026-04-09 — Increased overlay mesh Y-offsets for sloped DEM terrain. CreateFlatContourMesh (tee): 0.02→0.08. CreateEarClipContourMesh (cart path): 0.015→0.08. CreateFairwayMesh: 0.02→0.08. CreateFringeRing: 0.03→0.10. CreateGradientBorderRing (tee border): 0.015→0.06. Layering order preserved: border(0.06) < cart/fairway/tee(0.08) < fringe(0.10).
✅ DONE: 2026-04-09 — Cart path Y-offset increased from 0.08→0.15 in CreateEarClipContourMesh for extra clearance on curved quadratic terrain.
✅ DONE: 2026-04-09 — Cart path terrain poke-through fix: added SubdivideToTerrain helper that splits ear-clip triangles until no edge >2m, sampling terrain height at each midpoint. Y-offset reverted to 0.05m since subdivision handles curvature.
✅ DONE: 2026-04-09 — Cart path Y-offset bumped to 0.25m. SubdivideToTerrain receives yOffset as parameter, no separate value to change.
✅ DONE: 2026-04-09 — Cart path: lowered Y-offset back to 0.05m, made material double-sided (_Cull=0). Subdivision + double-sided eliminates poke-through without visible floating.
✅ DONE: 2026-04-09 — Cart path: spine-based strip mesh. Export extracts centerline via paired-edge averaging + RDP/Chaikin. Unity CreateSpineStripMesh builds quad strip along spine with terrain-sampled vertices. Ear-clip fallback preserved. Hole 1: 176 spine pts, 2.5m width.
✅ DONE: 2026-04-09 — Mountain backdrop v1: 8 instances approach (wrong — FBX is a single ring mesh).
✅ DONE: 2026-04-09 — Mountain backdrop v2: Single ring instance, centered at origin, scaled to terrain diagonal. LandscapesGreen.png opaque.
✅ DONE: 2026-04-09 — Mountain backdrop v3: Mountain.png texture (less stretching), transparent material with alpha blend, double-sided (_Cull=0), scale=terrainMax*1.5/nativeDiameter. Render queue 3000.
✅ DONE: 2026-04-09 — Water mesh: fixed sunken positioning on DEM terrain. Water now samples terrain.SampleHeight() at each contour vertex (like fairway/tee meshes) instead of fixed Y=0.05. Shore depression uses relative offsets from current height instead of absolute normalized values. Water cells depressed by (ShoreDepthMeters+0.5)/elevRange below current height.
✅ DONE: 2026-04-10 — Hybrid bunker system v1: small bunkers (minRadius < 4m) flat overlay. Superseded by v2.
✅ DONE: 2026-04-10 — Hybrid bunker system v2: shorterAxis < 7m threshold (fixes Bunker 6 false positive). Shallow overlay with 0.3m central depression + sand collar ring via CreateGradientBorderRing. Bowl mode unchanged for large bunkers.
