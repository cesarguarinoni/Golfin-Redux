# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`
> Previous completed specs archived in: `Docs/TellCode_Archive.md`

---

## Current Task — Tree Placement System (v2: Mixed Mode)

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
