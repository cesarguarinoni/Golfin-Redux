#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Places trees on terrain using zone 5 mask from UHole Lite.
    /// Uses Unity Terrain tree system (automatic LOD + billboarding).
    /// </summary>
    public static class TreePlacer
    {
        // Tree prototypes — paths to prefabs
        private static readonly string[] TreePrefabPaths = new string[]
        {
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_01Cedar.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01_Var1.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_JapaneseBlack_01.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/Mesh_Metasequoia.prefab",
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/MESH_ScottishPine_01.prefab",
        };

        // Relative weight for each prototype (must match TreePrefabPaths length)
        // Cedar=backbone, JBlackPine_v2=main pine, JBlackPine_v1=rare accent,
        // Metasequoia=tall variety, ScottishPine=filler
        private static readonly float[] TreeWeights = { 3f, 3f, 0.5f, 2f, 2f };

        // Placement settings
        private const float MinSpacing = 6f;   // meters between trees
        private const float ScaleMin = 0.85f;
        private const float ScaleMax = 1.15f;

        // Zones to EXCLUDE from tree placement
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

            foreach (var prefabPath in TreePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[TreePlacer] Prefab not found: {prefabPath}");
                    continue;
                }
                prototypes.Add(new TreePrototype { prefab = prefab });
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

            // ---- Poisson disk sampling (grid-jitter approximation) ----
            var trees = new List<TreeInstance>();
            var rng = new System.Random(42); // fixed seed for reproducibility

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
                    // 90° CCW rotation matching splatmap pipeline:
                    // terrain X fraction → zone normY, terrain Z fraction → zone normX
                    int maskX = Mathf.Clamp(
                        Mathf.FloorToInt(nz * maskW), 0, maskW - 1);
                    int maskY = Mathf.Clamp(
                        Mathf.FloorToInt(nx * maskH), 0, maskH - 1);
                    if (mask[maskY * maskW + maskX] == 0) continue;

                    // Check zone grid — skip excluded zones
                    if (zoneGrid != null)
                    {
                        int zx = Mathf.Clamp(
                            Mathf.FloorToInt(nz * zoneW), 0, zoneW - 1);
                        int zy = Mathf.Clamp(
                            Mathf.FloorToInt(nx * zoneH), 0, zoneH - 1);
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

            // ---- Unify draw distances ----
            terrain.treeDistance = 150f;          // max draw distance (meters)
            terrain.treeBillboardDistance = 80f;  // 3D→billboard transition
            terrain.treeCrossFadeLength = 20f;    // fade band width
            terrain.treeMaximumFullLODCount = 50; // max full-detail trees

            // ---- Normalize LODGroup thresholds across all prototypes ----
            // Each prefab has different screenRelativeHeight values, causing
            // trees to pop LODs at different distances. Override them all to
            // uniform values so transitions happen at the same distance.
            foreach (var proto in terrainData.treePrototypes)
            {
                if (proto.prefab == null) continue;
                var lodGroup = proto.prefab.GetComponent<LODGroup>();
                if (lodGroup == null) continue;

                var lods = lodGroup.GetLODs();
                // Normalize to consistent thresholds
                if (lods.Length >= 1) lods[0].screenRelativeTransitionHeight = 0.15f;
                if (lods.Length >= 2) lods[1].screenRelativeTransitionHeight = 0.05f;
                if (lods.Length >= 3) lods[2].screenRelativeTransitionHeight = 0.01f;
                lodGroup.SetLODs(lods);
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
            }

            Debug.Log($"[TreePlacer] Placed {trees.Count} trees " +
                $"({prototypes.Count} types, {cellSize}m spacing, " +
                $"seed=42, draw={terrain.treeDistance}m, " +
                $"billboard={terrain.treeBillboardDistance}m)");
        }

        [MenuItem("GOLFIN/Import Trees (Current Hole)")]
        private static void ImportTreesMenuItem()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TreePlacer] No active terrain found");
                return;
            }

            // Detect hole number from active scene name (format: Hole_01)
            string sceneName = UnityEditor.SceneManagement.EditorSceneManager
                .GetActiveScene().name;
            int holeNumber = -1;
            if (sceneName.StartsWith("Hole_") && sceneName.Length >= 7)
            {
                int.TryParse(sceneName.Substring(5, 2), out holeNumber);
            }

            if (holeNumber < 1 || holeNumber > 18)
            {
                Debug.LogError($"[TreePlacer] Cannot detect hole number " +
                    $"from scene '{sceneName}' (expected Hole_XX)");
                return;
            }

            string exportPath = Path.Combine(
                Application.dataPath, "..",
                "Tools/UHoleLite/output/lomond-country-club/export",
                $"hole-{holeNumber:D2}");

            if (!Directory.Exists(exportPath))
            {
                Debug.LogError($"[TreePlacer] Export folder not found: {exportPath}");
                return;
            }

            // Clear existing trees first
            terrain.terrainData.SetTreeInstances(
                new TreeInstance[0], false);

            float terrainBaseY = terrain.transform.position.y;
            string zonesPath = Path.Combine(exportPath, "zones.json");
            PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath);

            // Save scene so trees persist
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TreePlacer] Scene saved: {scene.path}");
        }
    }
}
#endif
