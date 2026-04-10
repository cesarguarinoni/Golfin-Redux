# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Tree Placement System

**Goal:** Automatically place trees on the terrain using zone 5
(trees) data exported from UHole Lite. Uses Unity's built-in
Terrain tree system for automatic LOD, billboarding, and batching.

**Prerequisite:** Run `node scripts/export-hole.mjs lomond-country-club 1`
first. This produces `tree-zones.json` in the export folder.

### Part 1 — Data Classes

In `Assets/Scripts/Editor/CourseImporter/HoleManifestData.cs`, add:

```csharp
// tree-zones.json — tree placement mask + regions
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
public class TreeMPP
{
    public float x;
    public float z;
}

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

Also add `tree_zones_file` to the manifest class if not present:
```csharp
public string tree_zones_file;
```

### Part 2 — TreePlacer.cs

Create `Assets/Scripts/Editor/CourseImporter/TreePlacer.cs`

This is an editor-only class called by `HoleLiteImporter` after
terrain + zone meshes are created.

```csharp
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Golfin.Editor.CourseImporter
{
    /// <summary>
    /// Places trees on terrain using zone 5 mask from UHole Lite.
    /// Uses Unity Terrain tree system (automatic LOD + billboarding).
    /// </summary>
    public static class TreePlacer
    {
        // Tree prototypes — paths to prefabs
        // These MUST have LODGroup to work well as terrain trees
        private static readonly string[] TreePrefabPaths = new string[]
        {
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_01Cedar.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_Maple.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_ScottishPine_01.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_Bush_01.prefab",
        };

        // Relative weight for each prototype (must match TreePrefabPaths length)
        // Higher weight = more of that tree type
        private static readonly float[] TreeWeights = { 3f, 2f, 2f, 2f, 1f };

        // Placement settings
        private const float MinSpacing = 6f;   // meters between trees (~50 trees per hole)
        private const float ScaleMin = 0.85f;
        private const float ScaleMax = 1.15f;

        // Zones to EXCLUDE from tree placement
        // 0=Background is OK (rough edges), 3=semi-rough OK, 4=rough OK
        private static readonly HashSet<int> ExcludeZones = new HashSet<int>
        {
            1,  // fairway
            2,  // green
            6,  // bunker
            7,  // water
            8,  // cart path
            10, // tee box
        };

        /// <summary>
        /// Main entry point. Call after terrain is created.
        /// </summary>
        public static void PlaceTrees(
            Terrain terrain, float terrainBaseY,
            string exportPath, string zonesJsonPath)
        {
            // Load tree-zones.json
            string tzPath = Path.Combine(exportPath, "tree-zones.json");
            if (!File.Exists(tzPath))
            {
                Debug.Log("[TreePlacer] No tree-zones.json found, skipping");
                return;
            }

            string tzJson = File.ReadAllText(tzPath);
            var tzData = JsonUtility.FromJson<TreeZonesFile>(tzJson);

            if (tzData.tree_region_count == 0 ||
                string.IsNullOrEmpty(tzData.mask_base64))
            {
                Debug.Log("[TreePlacer] No tree zones painted, skipping");
                return;
            }

            // Decode binary mask
            byte[] mask = System.Convert.FromBase64String(tzData.mask_base64);
            int maskW = tzData.mask_width;
            int maskH = tzData.mask_height;

            // Load zone grid for exclusion checks
            byte[] zoneGrid = null;
            int zoneW = 0, zoneH = 0;
            if (File.Exists(zonesJsonPath))
            {
                string zJson = File.ReadAllText(zonesJsonPath);
                var zData = JsonUtility.FromJson<ZonesData>(zJson);
                zoneGrid = System.Convert.FromBase64String(zData.grid);
                zoneW = zData.source_dimensions.width;
                zoneH = zData.source_dimensions.height;
            }

            var terrainData = terrain.terrainData;
            float tWidth = terrainData.size.x;
            float tLength = terrainData.size.z;

            // ---- Register tree prototypes ----
            var prototypes = new List<TreePrototype>();
            var loadedPrefabs = new List<GameObject>();

            foreach (var prefabPath in TreePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[TreePlacer] Prefab not found: {prefabPath}");
                    continue;
                }
                prototypes.Add(new TreePrototype { prefab = prefab });
                loadedPrefabs.Add(prefab);
            }

            if (prototypes.Count == 0)
            {
                Debug.LogError("[TreePlacer] No tree prefabs found!");
                return;
            }

            terrainData.treePrototypes = prototypes.ToArray();

            // Build cumulative weight array
            float totalWeight = 0;
            var cumulativeWeights = new float[prototypes.Count];
            for (int i = 0; i < prototypes.Count; i++)
            {
                float w = i < TreeWeights.Length ? TreeWeights[i] : 1f;
                totalWeight += w;
                cumulativeWeights[i] = totalWeight;
            }

            // ---- Poisson disk sampling ----
            var trees = new List<TreeInstance>();
            var rng = new System.Random(42); // fixed seed for reproducibility

            // Simple grid-based Poisson approximation:
            // Divide terrain into cells of MinSpacing, jitter each
            float cellSize = MinSpacing;
            int cellsX = Mathf.FloorToInt(tWidth / cellSize);
            int cellsZ = Mathf.FloorToInt(tLength / cellSize);

            for (int cz = 0; cz < cellsZ; cz++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    // Jittered position within cell
                    float worldX = (cx + (float)rng.NextDouble()) * cellSize;
                    float worldZ = (cz + (float)rng.NextDouble()) * cellSize;

                    // Normalized terrain coordinates (0-1)
                    float nx = worldX / tWidth;
                    float nz = worldZ / tLength;

                    if (nx < 0 || nx >= 1 || nz < 0 || nz >= 1) continue;

                    // Check tree mask
                    int maskX = Mathf.Clamp(
                        Mathf.FloorToInt(nx * maskW), 0, maskW - 1);
                    int maskY = Mathf.Clamp(
                        Mathf.FloorToInt(nz * maskH), 0, maskH - 1);
                    if (mask[maskY * maskW + maskX] == 0) continue;

                    // Check zone grid — skip excluded zones
                    if (zoneGrid != null)
                    {
                        int zx = Mathf.Clamp(
                            Mathf.FloorToInt(nx * zoneW), 0, zoneW - 1);
                        int zy = Mathf.Clamp(
                            Mathf.FloorToInt(nz * zoneH), 0, zoneH - 1);
                        int zone = zoneGrid[zy * zoneW + zx];
                        if (ExcludeZones.Contains(zone)) continue;
                    }

                    // Pick prototype (weighted random)
                    float roll = (float)rng.NextDouble() * totalWeight;
                    int protoIdx = 0;
                    for (int i = 0; i < cumulativeWeights.Length; i++)
                    {
                        if (roll <= cumulativeWeights[i])
                        {
                            protoIdx = i;
                            break;
                        }
                    }

                    // Random scale + rotation
                    float scale = ScaleMin +
                        (float)rng.NextDouble() * (ScaleMax - ScaleMin);
                    float rotation = (float)rng.NextDouble() * 360f
                        * Mathf.Deg2Rad;

                    trees.Add(new TreeInstance
                    {
                        position = new Vector3(nx, 0f, nz),
                        widthScale = scale,
                        heightScale = scale,
                        rotation = rotation,
                        color = Color.white,
                        lightmapColor = Color.white,
                        prototypeIndex = protoIdx,
                    });
                }
            }

            terrainData.SetTreeInstances(trees.ToArray(), true);

            Debug.Log($"[TreePlacer] Placed {trees.Count} trees " +
                $"({prototypes.Count} types, {cellSize}m spacing, " +
                $"seed=42)");
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

            // Find export path — check all 18 holes
            string exportBase = "Tools/UHoleLite/output/lomond-country-club/export";
            string exportPath = null;
            for (int h = 1; h <= 18; h++)
            {
                string candidate = Path.Combine(
                    Application.dataPath, "..", exportBase,
                    $"hole-{h:D2}");
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "tree-zones.json")))
                {
                    exportPath = candidate;
                    break;
                }
            }

            if (exportPath == null)
            {
                Debug.LogError("[TreePlacer] No export folder with " +
                    "tree-zones.json found");
                return;
            }

            // Clear existing trees first
            terrain.terrainData.SetTreeInstances(
                new TreeInstance[0], false);

            float terrainBaseY = terrain.transform.position.y;
            string zonesPath = Path.Combine(exportPath, "zones.json");
            PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath);
        }
    }
}
```

### Part 3 — Wire into HoleLiteImporter

In `HoleLiteImporter.cs`, in the `ImportHole()` method (or wherever
the import pipeline runs), add a call to `TreePlacer.PlaceTrees()`
**after** terrain creation and zone mesh creation.

Find the end of the import sequence (after mountains, after zone
meshes) and add:

```csharp
// ---- Trees ----
string zonesJsonExportPath = Path.Combine(exportPath, "zones.json");
TreePlacer.PlaceTrees(terrain, terrainBaseY, exportPath,
    zonesJsonExportPath);
```

Make sure to add `using Golfin.Editor.CourseImporter;` if needed
(though it's likely in the same namespace).

### Verification

1. Re-export: `node scripts/export-hole.mjs lomond-country-club 1`
2. Re-import in Unity: GOLFIN > Import Hole (Lite) > Hole 01
3. Trees should appear in the zone 5 painted areas
4. Console: `[TreePlacer] Placed NN trees (5 types, 6m spacing, seed=42)`
5. Trees should:
   - Follow terrain height (no floating, no buried)
   - Not appear on fairway, green, bunker, water, tee, cart path
   - Have varied rotation and scale
   - Show LOD transitions when zooming in/out in Scene view
6. Re-run via GOLFIN > Place Trees should clear + re-place

### Do NOT

- Place trees as standalone GameObjects (use Terrain tree system)
- Use `UnityEngine.Random` (use `System.Random` with fixed seed)
- Change any existing zone mesh or terrain code
- Change the tree prefabs themselves
- Change the export pipeline (that's in TASK.md)

---

## Previous Task — Fix Small Bunker Terrain Poke-Through (v5)

**Goal:** Bunker 7 has terrain poking through the rim ring. Fix by
using a **simple inscribed square** for the terrain hole cut instead
of tracing the contour shape. The square is smaller than the bowl
lip, so the rim always covers it. The bowl mesh still follows the
actual contour.

**Why previous approaches failed:**
- v2/v3: Skipping terrain holes doesn't work — terrain always wins
  depth test over below-terrain bowl vertices. renderQueue doesn't
  help. Collar ring = donut with empty interior.
- v4: Adaptive contour-traced cut still has grid-snap overshoot at
  convex corners of small contours.

**v5 insight:** The terrain hole doesn't need to follow the bunker
shape. It just needs to be a hole *somewhere under the bowl* so the
bowl interior is visible. A simple axis-aligned rectangle inscribed
well inside the contour is immune to grid-snap corner issues because
its edges are straight and axis-aligned — they align perfectly with
the holes grid.

### The Fix

Replace the contour-traced terrain hole cut with an **inscribed
rectangle** approach:

1. Compute the bunker's axis-aligned bounding box
2. Shrink it by a **fixed margin** (e.g. 40% on each side) to get
   a rectangle that's well inside the contour
3. Cut terrain holes using this simple rectangle (no point-in-polygon
   test needed — just min/max grid cell bounds)
4. Bowl mesh stays exactly as-is — same `CreateContourMesh`, same
   contour shape, same 4-ring bowl

The rim ring (ring 0 at 100% to ring 1 at 80%) covers the gap between
the contour edge and the inscribed rectangle. For small bunkers this
gap is larger, but the rim ring is also proportionally larger.

### Code Changes

In the `foreach (var bunker in bunkersFile.bunkers)` loop inside
`CreateZoneMeshes()`, replace the terrain hole cutting section:

```csharp
foreach (var bunker in bunkersFile.bunkers)
{
    var worldContour = new Vector2[bunker.contour.Length];
    float sumX = 0, sumZ = 0;
    for (int i = 0; i < bunker.contour.Length; i++)
    {
        float wx = bunker.contour[i].z;
        float wz = bunker.contour[i].x;
        worldContour[i] = new Vector2(wx, wz);
        sumX += wx;
        sumZ += wz;
    }
    float centroidX = sumX / worldContour.Length;
    float centroidZ = sumZ / worldContour.Length;

    // Bounding box of contour
    float cMinX = float.MaxValue, cMaxX = float.MinValue;
    float cMinZ = float.MaxValue, cMaxZ = float.MinValue;
    foreach (var v in worldContour)
    {
        if (v.x < cMinX) cMinX = v.x;
        if (v.x > cMaxX) cMaxX = v.x;
        if (v.y < cMinZ) cMinZ = v.y;
        if (v.y > cMaxZ) cMaxZ = v.y;
    }

    // ── Inscribed rectangle terrain hole ──
    // Shrink the bounding box by 40% on each side to get a rectangle
    // well inside the contour. The bowl rim covers the gap.
    float bboxW = cMaxX - cMinX;
    float bboxH = cMaxZ - cMinZ;
    float shrink = 0.40f; // 40% inset on each side
    float insetX = bboxW * shrink;
    float insetZ = bboxH * shrink;

    float rectMinX = cMinX + insetX;
    float rectMaxX = cMaxX - insetX;
    float rectMinZ = cMinZ + insetZ;
    float rectMaxZ = cMaxZ - insetZ;

    // Only cut if the rectangle is big enough (at least 2 grid cells)
    float cellSize = terrainSize.x / holesRes;
    bool canCut = (rectMaxX - rectMinX) > cellSize * 2 &&
                  (rectMaxZ - rectMinZ) > cellSize * 2;

    if (canCut)
    {
        int hMinX = Mathf.Clamp(Mathf.CeilToInt(
            (rectMinX - terrainPos.x) / terrainSize.x * holesRes),
            0, holesRes - 1);
        int hMaxX = Mathf.Clamp(Mathf.FloorToInt(
            (rectMaxX - terrainPos.x) / terrainSize.x * holesRes),
            0, holesRes - 1);
        int hMinZ = Mathf.Clamp(Mathf.CeilToInt(
            (rectMinZ - terrainPos.z) / terrainSize.z * holesRes),
            0, holesRes - 1);
        int hMaxZ = Mathf.Clamp(Mathf.FloorToInt(
            (rectMaxZ - terrainPos.z) / terrainSize.z * holesRes),
            0, holesRes - 1);

        // Simple rectangle — no point-in-polygon test needed
        for (int hz = hMinZ; hz <= hMaxZ; hz++)
            for (int hx = hMinX; hx <= hMaxX; hx++)
                holes[hz, hx] = false;

        Debug.Log($"[HoleLiteImporter] Bunker {bunker.id}: cut rect " +
                  $"({rectMaxX - rectMinX:F1}x{rectMaxZ - rectMinZ:F1}m) " +
                  $"inside bbox ({bboxW:F1}x{bboxH:F1}m)");
    }
    else
    {
        Debug.Log($"[HoleLiteImporter] Bunker {bunker.id}: too small " +
                  $"for terrain hole ({bboxW:F1}x{bboxH:F1}m), " +
                  $"bowl rim covers fully");
    }

    // ── Bowl mesh (unchanged) ──
    float surfaceY = terrainBaseY + terrain.SampleHeight(
        new Vector3(centroidX, 0, centroidZ));
    float bowlDepth = Mathf.Max(Mathf.Min(defaultDepth, 3f), 0.5f);

    var meshGO = CreateContourMesh(bunker.id, worldContour,
        centroidX, centroidZ, surfaceY, bowlDepth,
        sandMat, terrain, terrainBaseY);
    meshGO.transform.SetParent(bunkersRoot.transform);

    var marker = meshGO.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = Golfin.Course.SurfaceType.Bunker;
}
```

Note: use `CeilToInt` for min bounds and `FloorToInt` for max bounds
when converting to grid cells. This ensures the rectangle is always
*inside* the calculated area (conservative), never overshooting.

### Why This Works for All Sizes

**Large bunker** (bbox 15x35m): inscribed rect = 9x21m. Plenty of
terrain cut for the bowl to show through. Rim covers the 3m gap
on each side easily.

**Bunker 7** (bbox 5.85x5.66m): inscribed rect = 3.5x3.4m → ~1.2m
cut → might be only 2x2 grid cells, but that's enough. If too small,
`canCut` check skips the hole and the bowl rim sits entirely on
terrain with its +0.02m Y offset. Worst case for tiny bunkers:
the rim covers everything and there's no terrain hole at all.

**Key advantage:** Axis-aligned rectangle edges align perfectly with
the terrain holes grid — zero corner overshoot by definition.

### No Changes to CreateContourMesh

`CreateContourMesh` stays exactly as-is. Same 4-ring bowl, same
ring scales `{ 1.0, 0.80, 0.50, 0.20 }`, same everything.

### Verification

1. Re-import Hole 01
2. Console: each bunker shows cut rect size vs bbox size
3. Bunker 7: bowl visible, no terrain poke-through at rim
4. Large bunkers: slightly smaller terrain hole but visually identical
   (rim covers the difference)
5. No code changes to `CreateContourMesh` — only the hole cutting

### Do NOT

- Change `CreateContourMesh` (no ring scale changes needed)
- Use `IsInsideContour` for terrain hole cutting (use simple rect)
- Skip terrain holes entirely (terrain wins depth test)
- Change the bunker export pipeline
- Change other zone meshes

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

(See git history for full spec — completed 2026-04-09)

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
✅ DONE: 2026-04-10 — Hybrid bunker system v2: shorterAxis < 7m threshold (fixes Bunker 6 false positive). Shallow overlay with 0.3m central depression + sand collar ring via CreateGradientBorderRing. Bowl mode unchanged for large bunkers. Superseded by v3.
✅ DONE: 2026-04-10 — Hybrid bunker system v3: small bunkers use same CreateContourMesh bowl (shallower, max 1.5m depth) but skip terrain hole. renderQueue=2001 prevents z-fighting with terrain. No ear-clip or collar ring needed. Superseded by v4.
✅ DONE: 2026-04-10 — Bunker v4: unified approach — all bunkers use terrain hole + bowl. Replaced fixed 90% cutScale with adaptive fixed-distance inset (1.2m = 2 grid cells). Added innerRingScale param to CreateContourMesh so rim width adapts to bunker size. Small bunkers get wider rim that fully covers terrain hole edge. Superseded by v5 (rim too wide, hole too small).
✅ DONE: 2026-04-10 — Bunker v5: inscribed rectangle terrain hole (40% bbox shrink). Axis-aligned edges = zero grid-snap overshoot. CreateContourMesh reverted to fixed ring scales {1.0, 0.80, 0.50, 0.20}. Small bunkers that can't fit 2 grid cells get no terrain hole (rim covers fully).
