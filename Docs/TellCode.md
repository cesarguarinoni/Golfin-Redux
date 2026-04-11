# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Terrain Depression v2: Contour-Based

**Problem with v1:** The zone grid (~0.2m/px) has lower resolution
than the heightmap (2049, ~0.3m/cell). Zone boundaries don't align
precisely with contour meshes, causing the depression to bleed
outside the fairway. Also 5cm depression wasn't enough.

**Fix:** Use the **actual contour polygons** from the JSON files
(the same contours used to build the overlay meshes) to test which
heightmap cells to depress. This guarantees exact alignment.

**Depression depth:** Use the mesh's yOffset + a margin.
The meshes use yOffset = 0.02m, so depress by **0.10m (10cm)**.
This gives 8cm clearance — enough for terrain interpolation error.

---

### Changes to `DepressTerrainUnderOverlays`

Replace the zone-grid approach with contour-polygon point-in-polygon
testing. Load the same contour JSON files used by `CreateFlatZoneMeshes`.

```csharp
private const float OverlayDepressionMeters = 0.10f; // 10cm

private static void DepressTerrainUnderOverlays(
    TerrainData terrainData, GameObject terrainGO, string exportPath)
{
    int hRes = terrainData.heightmapResolution;
    float[,] heights = terrainData.GetHeights(0, 0, hRes, hRes);
    float elevRange = terrainData.size.y;
    float dropNormalized = OverlayDepressionMeters / elevRange;
    Vector3 terrainPos = terrainGO.transform.position;
    Vector3 terrainSize = terrainData.size;

    bool[,] depress = new bool[hRes, hRes];

    // --- Collect all contour polygons that have overlay meshes ---
    // Fairway contours
    string fwPath = Path.Combine(exportPath, "fairway-contours.json");
    if (File.Exists(fwPath))
    {
        var data = JsonUtility.FromJson<FairwayContoursFile>(
            File.ReadAllText(fwPath));
        if (data.fairways != null)
            foreach (var fw in data.fairways)
                if (fw.contour != null && fw.contour.Length >= 3)
                    MarkContourCells(fw.contour, depress,
                        hRes, terrainPos, terrainSize);
    }

    // Tee contours
    string zcPath = Path.Combine(exportPath, "zone-contours.json");
    if (File.Exists(zcPath))
    {
        var data = JsonUtility.FromJson<ZoneContoursFile>(
            File.ReadAllText(zcPath));
        if (data.zones?.tee != null)
            foreach (var region in data.zones.tee)
                if (region.contour != null && region.contour.Length >= 3)
                    MarkContourCells(region.contour, depress,
                        hRes, terrainPos, terrainSize);
    }

    // Cart path contours (use spine width, not contour)
    string cpPath = Path.Combine(exportPath, "cart-paths.json");
    if (File.Exists(cpPath))
    {
        var data = JsonUtility.FromJson<CartPathsFile>(
            File.ReadAllText(cpPath));
        if (data.cart_paths != null)
            foreach (var cp in data.cart_paths)
            {
                if (cp.contour != null && cp.contour.Length >= 3)
                    MarkContourCells(cp.contour, depress,
                        hRes, terrainPos, terrainSize);
            }
    }

    // Apply depression
    int depressedCount = 0;
    for (int hz = 0; hz < hRes; hz++)
        for (int hx = 0; hx < hRes; hx++)
            if (depress[hz, hx])
            {
                heights[hz, hx] = Mathf.Max(0f,
                    heights[hz, hx] - dropNormalized);
                depressedCount++;
            }

    terrainData.SetHeights(0, 0, heights);
    Debug.Log($"[HoleLiteImporter] Terrain depression: {depressedCount}" +
              $" cells lowered by {OverlayDepressionMeters:F2}m");
}

/// <summary>
/// Mark heightmap cells that fall inside a contour polygon.
/// Contour uses local meter coords with 90° CCW rotation applied.
/// </summary>
private static void MarkContourCells(ContourPoint[] contour,
    bool[,] depress, int hRes, Vector3 terrainPos, Vector3 terrainSize)
{
    // Convert contour to world XZ (90° CCW: worldX = z, worldZ = x)
    var worldContour = new Vector2[contour.Length];
    float minX = float.MaxValue, maxX = float.MinValue;
    float minZ = float.MaxValue, maxZ = float.MinValue;
    for (int i = 0; i < contour.Length; i++)
    {
        float wx = contour[i].z;  // 90° CCW
        float wz = contour[i].x;
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

`IsInsideContour` already exists in the codebase.

### Placement in ImportLiteHole

Add AFTER `CreateFlatZoneMeshes` and BEFORE `terrainData.SetHoles`:
```csharp
DepressTerrainUnderOverlays(terrainData, terrainGO, exportPath);
```

### Verification

1. Import Hole 4 (hilly)
2. Fairway: no z-fighting, depression exactly matches fairway shape
3. No terrain depression visible outside fairway/tee/cart path edges
4. Walk along fairway edge — terrain-to-mesh transition is clean
5. Green/bunker/water: unchanged

### Do NOT
- Use the zone grid for depression (too coarse)
- Change overlay mesh creation methods
- Change green, bunker, or water code
- Depress under green or bunker zones

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
