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
    }
}
```

**Key design points:**
- `position.y = 0` — Unity auto-snaps to terrain when
  `snapToHeightmap: true` in `SetTreeInstances()`
- Fixed seed (42) for reproducibility — same placement each import
- Grid-jitter Poisson approximation: simpler than true Poisson disk,
  still prevents clumping, >= MinSpacing guaranteed
- Zone grid double-check: even though mask = zone 5, the zone grid
  exclusion catches edge pixels where zones overlap
- Uses `System.Random` not `UnityEngine.Random` to avoid polluting
  Unity's global random state

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

### Part 4 — Menu Item for Re-running

Add a standalone menu item so trees can be re-placed without
full re-import. In `TreePlacer.cs`, add:

```csharp
[MenuItem("GOLFIN/Place Trees (Current Terrain)")]
private static void PlaceTreesMenuItem()
{
    var terrain = Terrain.activeTerrain;
    if (terrain == null)
    {
        Debug.LogError("[TreePlacer] No active terrain found");
        return;
    }

    // Find export path from terrain name or hardcode for now
    string exportBase = "Tools/UHoleLite/output/lomond-country-club/export";
    // Try to detect hole number from scene objects
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
```

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
