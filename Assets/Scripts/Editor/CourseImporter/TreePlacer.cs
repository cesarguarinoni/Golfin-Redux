#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Describes one tree prefab available for placement.
    /// </summary>
    public class TreeEntry
    {
        public string path;       // asset path
        public string name;       // display name (filename without extension)
        public GameObject prefab; // loaded asset
        public bool enabled;      // include in placement?
        public float weight;      // relative spawn weight
    }

    /// <summary>
    /// Places trees on terrain using zone 5 mask from UHole Lite.
    /// Uses Unity Terrain tree system (automatic LOD + billboarding).
    /// </summary>
    public static class TreePlacer
    {
        // Folder scanned for tree prefabs
        public const string TreePrefabFolder =
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs";

        // Default enabled prefabs + weights (applied on first scan)
        private static readonly Dictionary<string, float> DefaultWeights =
            new Dictionary<string, float>
        {
            { "MESH_01Cedar",               3.0f },
            { "MESH_JapaneseBlack_01_Var1", 3.0f },
            { "MESH_JapaneseBlack_01",      0.5f },
            { "Mesh_Metasequoia",           2.0f },
            { "MESH_ScottishPine_01",       2.0f },
        };

        // The dynamic tree palette — populated by ScanPrefabs()
        public static List<TreeEntry> TreePalette = new List<TreeEntry>();

        // Placement settings
        public static float MinSpacing = 6f;
        public static float ScaleMin = 0.85f;
        public static float ScaleMax = 1.15f;

        // Draw distance settings
        public static float DrawDistance = 150f;
        public static float BillboardDistance = 80f;
        public static float CrossFadeLength = 20f;
        public static int MaxFullLODCount = 50;

        // LOD thresholds (screen-relative height)
        public static float LOD0Threshold = 0.15f;
        public static float LOD1Threshold = 0.05f;
        public static float LOD2Threshold = 0.01f;

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
        /// Scan the prefab folder and populate TreePalette.
        /// Preserves existing enabled/weight state for prefabs already in the list.
        /// </summary>
        public static void ScanPrefabs()
        {
            // Remember current state
            var existing = new Dictionary<string, TreeEntry>();
            foreach (var e in TreePalette)
                existing[e.path] = e;

            TreePalette.Clear();

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { TreePrefabFolder });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".prefab")) continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(assetPath);

                // Preserve previous state if it exists
                if (existing.TryGetValue(assetPath, out var prev))
                {
                    prev.prefab = prefab;
                    TreePalette.Add(prev);
                }
                else
                {
                    // New entry — use defaults if available, otherwise disabled weight 1
                    bool isDefault = DefaultWeights.TryGetValue(fileName, out float w);
                    TreePalette.Add(new TreeEntry
                    {
                        path = assetPath,
                        name = fileName,
                        prefab = prefab,
                        enabled = isDefault,
                        weight = isDefault ? w : 1f,
                    });
                }
            }

            // Sort: enabled first, then alphabetical
            TreePalette.Sort((a, b) =>
            {
                if (a.enabled != b.enabled) return a.enabled ? -1 : 1;
                return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// Get the enabled entries with weight > 0.
        /// </summary>
        public static List<TreeEntry> GetActiveEntries()
        {
            return TreePalette.Where(e => e.enabled && e.weight > 0f).ToList();
        }

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

            // ---- Ensure palette is populated ----
            if (TreePalette.Count == 0) ScanPrefabs();
            var activeEntries = GetActiveEntries();

            if (activeEntries.Count == 0)
            {
                Debug.LogError("[TreePlacer] No tree prefabs enabled!");
                return;
            }

            // ---- Register tree prototypes ----
            var prototypes = new List<TreePrototype>();
            foreach (var entry in activeEntries)
            {
                prototypes.Add(new TreePrototype { prefab = entry.prefab });
            }
            terrainData.treePrototypes = prototypes.ToArray();

            // Build cumulative weight array
            float totalWeight = 0;
            var cumulativeWeights = new float[activeEntries.Count];
            for (int i = 0; i < activeEntries.Count; i++)
            {
                totalWeight += activeEntries[i].weight;
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
            terrain.treeDistance = DrawDistance;
            terrain.treeBillboardDistance = BillboardDistance;
            terrain.treeCrossFadeLength = CrossFadeLength;
            terrain.treeMaximumFullLODCount = MaxFullLODCount;

            // ---- Normalize LODGroup thresholds across all prototypes ----
            foreach (var proto in terrainData.treePrototypes)
            {
                if (proto.prefab == null) continue;
                var lodGroup = proto.prefab.GetComponent<LODGroup>();
                if (lodGroup == null) continue;

                var lods = lodGroup.GetLODs();
                if (lods.Length >= 1) lods[0].screenRelativeTransitionHeight = LOD0Threshold;
                if (lods.Length >= 2) lods[1].screenRelativeTransitionHeight = LOD1Threshold;
                if (lods.Length >= 3) lods[2].screenRelativeTransitionHeight = LOD2Threshold;
                lodGroup.SetLODs(lods);
                lodGroup.fadeMode = LODFadeMode.CrossFade;
                lodGroup.animateCrossFading = true;
            }

            // Build per-type count summary
            var typeCounts = new int[activeEntries.Count];
            foreach (var t in trees) typeCounts[t.prototypeIndex]++;
            var summary = string.Join(", ",
                activeEntries.Select((e, i) => $"{e.name}={typeCounts[i]}"));

            Debug.Log($"[TreePlacer] Placed {trees.Count} trees " +
                $"({activeEntries.Count} types, {cellSize}m spacing, seed=42)\n  {summary}");
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
