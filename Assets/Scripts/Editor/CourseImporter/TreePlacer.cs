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
        public bool standalone;   // true = instantiate as GameObject; false = terrain tree system
        public bool hasLODGroup;  // auto-detected from prefab
    }

    /// <summary>
    /// Places trees on terrain using zone 5 mask from UHole Lite.
    /// Mixed mode: prefabs with LODGroup on root → terrain tree system;
    /// prefabs without (or forced standalone) → instantiated GameObjects.
    /// </summary>
    public static class TreePlacer
    {
        // Folders scanned for tree prefabs
        public static readonly string[] TreePrefabFolders = new string[]
        {
            "Assets/Art/3D/Trees(2025)/Trees2025_Prefabs",
        };

        // Default enabled prefabs + weights (applied on first scan)
        private static readonly Dictionary<string, float> DefaultWeights =
            new Dictionary<string, float>
        {
            { "MESH_01Cedar",               3.0f },
            { "MESH_JapaneseBlack_01_Var1", 3.0f },
            { "MESH_JapaneseBlack_01",      0.5f },
            { "Mesh_Metasequoia",           2.0f },
            { "MESH_ScottishPine_01",       2.0f },
            { "Spruce 1",                   1.5f },
            { "Spruce 3",                   1.0f },
        };

        // Prefabs forced standalone even if they have a LODGroup
        // (particle systems, complex hierarchies that terrain trees strip)
        private static readonly HashSet<string> ForceStandaloneNames =
            new HashSet<string>
        {
            "Spruce 1", "Spruce 3",
        };

        // The dynamic tree palette — populated by ScanPrefabs()
        public static List<TreeEntry> TreePalette = new List<TreeEntry>();

        // Placement settings
        public static float MinSpacing = 6f;
        public static float ScaleMin = 0.85f;
        public static float ScaleMax = 1.15f;

        // Sink offset — pushes trees below terrain surface to prevent
        // trunk bases from floating on slopes/ledges (meters)
        public static float SinkOffset = 0.3f;

        // Draw distance settings
        public static float DrawDistance = 150f;
        public static float BillboardDistance = 80f;
        public static float CrossFadeLength = 20f;
        public static int MaxFullLODCount = 50;

        // LOD thresholds (screen-relative height)
        public static float LOD0Threshold = 0.15f;
        public static float LOD1Threshold = 0.05f;
        public static float LOD2Threshold = 0.01f;

        // Container name for standalone trees in scene hierarchy
        private const string StandaloneContainerName = "StandaloneTrees";

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
        /// Scan all prefab folders and populate TreePalette.
        /// Preserves existing enabled/weight/standalone state for prefabs already in the list.
        /// </summary>
        public static void ScanPrefabs()
        {
            var existing = new Dictionary<string, TreeEntry>();
            foreach (var e in TreePalette)
                existing[e.path] = e;

            TreePalette.Clear();

            foreach (string folder in TreePrefabFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;

                string[] guids = AssetDatabase.FindAssets("t:GameObject",
                    new[] { folder });
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (!assetPath.EndsWith(".prefab")) continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (prefab == null) continue;

                    string fileName = Path.GetFileNameWithoutExtension(assetPath);

                    // Auto-detect LODGroup on root (not children — terrain trees
                    // need it on root to work)
                    bool hasRootLOD = prefab.GetComponent<LODGroup>() != null;
                    bool forceStandalone = ForceStandaloneNames.Contains(fileName);

                    if (existing.TryGetValue(assetPath, out var prev))
                    {
                        prev.prefab = prefab;
                        prev.hasLODGroup = hasRootLOD;
                        TreePalette.Add(prev);
                    }
                    else
                    {
                        bool isDefault = DefaultWeights.TryGetValue(fileName, out float w);
                        TreePalette.Add(new TreeEntry
                        {
                            path = assetPath,
                            name = fileName,
                            prefab = prefab,
                            enabled = isDefault,
                            weight = isDefault ? w : 1f,
                            hasLODGroup = hasRootLOD,
                            standalone = forceStandalone || !hasRootLOD,
                        });
                    }
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
        /// Build exclusion polygons from all overlay mesh contours so trees
        /// don't appear on fairways, greens, bunkers, tees, water, or cart paths.
        /// Returns world-space Vector2[] polygons (x=worldX, y=worldZ).
        /// </summary>
        private static List<Vector2[]> BuildExclusionPolygons(string exportPath)
        {
            var polygons = new List<Vector2[]>();
            float margin = 1.0f; // extra margin around overlays

            // Helper: convert ContourPoint[] to world-space polygon
            // Contour coords use 90° CCW rotation: worldX = pt.z, worldZ = pt.x
            System.Func<ContourPoint[], Vector2[]> contourToWorld = (contour) =>
            {
                var poly = new Vector2[contour.Length];
                for (int i = 0; i < contour.Length; i++)
                    poly[i] = new Vector2(contour[i].z, contour[i].x);
                return poly;
            };

            // Fairway contours
            string fwPath = Path.Combine(exportPath, "fairway-contours.json");
            if (File.Exists(fwPath))
            {
                var data = JsonUtility.FromJson<FairwayContoursFile>(
                    File.ReadAllText(fwPath));
                if (data.fairways != null)
                    foreach (var fw in data.fairways)
                        if (fw.contour != null && fw.contour.Length >= 3)
                            polygons.Add(contourToWorld(fw.contour));
            }

            // Tee contours
            string zcPath = Path.Combine(exportPath, "zone-contours.json");
            if (File.Exists(zcPath))
            {
                var data = JsonUtility.FromJson<ZoneContoursFile>(
                    File.ReadAllText(zcPath));
                if (data.zones != null && data.zones.tee != null)
                    foreach (var region in data.zones.tee)
                        if (region.contour != null && region.contour.Length >= 3)
                            polygons.Add(contourToWorld(region.contour));
            }

            // Bunker contours
            string bkPath = Path.Combine(exportPath, "bunkers.json");
            if (File.Exists(bkPath))
            {
                var data = JsonUtility.FromJson<BunkersFileData>(
                    File.ReadAllText(bkPath));
                if (data.bunkers != null)
                    foreach (var bk in data.bunkers)
                        if (bk.contour != null && bk.contour.Length >= 3)
                        {
                            var poly = new Vector2[bk.contour.Length];
                            for (int i = 0; i < bk.contour.Length; i++)
                                poly[i] = new Vector2(bk.contour[i].z, bk.contour[i].x);
                            polygons.Add(poly);
                        }
            }

            // Green contours
            string grPath = Path.Combine(exportPath, "greens.json");
            if (File.Exists(grPath))
            {
                var data = JsonUtility.FromJson<GreensFileData>(
                    File.ReadAllText(grPath));
                if (data.greens != null)
                    foreach (var gr in data.greens)
                        if (gr.contour != null && gr.contour.Length >= 3)
                        {
                            var poly = new Vector2[gr.contour.Length];
                            for (int i = 0; i < gr.contour.Length; i++)
                                poly[i] = new Vector2(gr.contour[i].z, gr.contour[i].x);
                            polygons.Add(poly);
                        }
            }

            // Water contours
            string wtPath = Path.Combine(exportPath, "water.json");
            if (File.Exists(wtPath))
            {
                var data = JsonUtility.FromJson<WaterFileData>(
                    File.ReadAllText(wtPath));
                if (data.water != null)
                    foreach (var w in data.water)
                        if (w.contour != null && w.contour.Length >= 3)
                            polygons.Add(contourToWorld(w.contour));
            }

            // Cart path spine polygons (full mesh width + margin)
            string cpPath = Path.Combine(exportPath, "cart-paths.json");
            if (File.Exists(cpPath))
            {
                var data = JsonUtility.FromJson<CartPathsFile>(
                    File.ReadAllText(cpPath));
                if (data.cart_paths != null)
                {
                    foreach (var cp in data.cart_paths)
                    {
                        if (cp.spine != null && cp.spine.Length >= 2)
                        {
                            float halfWidth = (cp.width_m > 0
                                ? cp.width_m : 2.5f) / 2f + margin;
                            var spinePoly = BuildSpinePolygonForExclusion(
                                cp.spine, halfWidth);
                            if (spinePoly != null)
                                polygons.Add(spinePoly);
                        }
                        else if (cp.contour != null && cp.contour.Length >= 3)
                        {
                            polygons.Add(contourToWorld(cp.contour));
                        }
                    }
                }
            }

            return polygons;
        }

        /// <summary>
        /// Build a closed polygon from spine left+right edges (for tree exclusion).
        /// Simplified version — no subdivision needed since we only test points.
        /// Returns world-space Vector2[] (x=worldX, y=worldZ).
        /// </summary>
        private static Vector2[] BuildSpinePolygonForExclusion(
            ContourPoint[] spine, float halfWidth)
        {
            int n = spine.Length;
            if (n < 2) return null;

            var left = new Vector2[n];
            var right = new Vector2[n];

            for (int i = 0; i < n; i++)
            {
                float cx = spine[i].z;  // 90° CCW
                float cz = spine[i].x;

                float tx, tz;
                if (i == 0)
                { tx = spine[1].z - spine[0].z; tz = spine[1].x - spine[0].x; }
                else if (i == n - 1)
                { tx = spine[n-1].z - spine[n-2].z; tz = spine[n-1].x - spine[n-2].x; }
                else
                { tx = spine[i+1].z - spine[i-1].z; tz = spine[i+1].x - spine[i-1].x; }

                float tLen = Mathf.Sqrt(tx * tx + tz * tz);
                if (tLen > 0.001f) { tx /= tLen; tz /= tLen; }
                else { tx = 1; tz = 0; }

                float px = tz;
                float pz = -tx;

                left[i]  = new Vector2(cx - px * halfWidth, cz - pz * halfWidth);
                right[i] = new Vector2(cx + px * halfWidth, cz + pz * halfWidth);
            }

            var poly = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
                poly[i] = left[i];
            for (int i = 0; i < n; i++)
                poly[n + i] = right[n - 1 - i];

            return poly;
        }

        /// <summary>
        /// Ray-casting point-in-polygon test (world XZ coordinates).
        /// </summary>
        private static bool PointInPolygon(float px, float pz, Vector2[] poly)
        {
            bool inside = false;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i].x, zi = poly[i].y;
                float xj = poly[j].x, zj = poly[j].y;
                if (((zi > pz) != (zj > pz)) &&
                    (px < (xj - xi) * (pz - zi) / (zj - zi) + xi))
                    inside = !inside;
            }
            return inside;
        }

        /// <summary>
        /// Check if a point falls inside any of the exclusion polygons.
        /// </summary>
        private static bool IsInsideAnyOverlay(
            float worldX, float worldZ, List<Vector2[]> polygons)
        {
            foreach (var poly in polygons)
                if (PointInPolygon(worldX, worldZ, poly))
                    return true;
            return false;
        }

        /// <summary>
        /// Main entry point. Call after terrain is created.
        /// </summary>
        public static void PlaceTrees(
            Terrain terrain, float terrainBaseY,
            string exportPath, string zonesJsonPath,
            Transform parentRoot = null)
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

            // Build exclusion polygons from overlay mesh contours
            var exclusionPolygons = BuildExclusionPolygons(exportPath);

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

            // ---- Split into terrain vs standalone ----
            var terrainEntries = new List<TreeEntry>();
            var standaloneEntries = new List<TreeEntry>();
            // Maps from activeEntries index → terrain prototype index (or -1)
            var terrainProtoMap = new int[activeEntries.Count];

            for (int i = 0; i < activeEntries.Count; i++)
            {
                if (activeEntries[i].standalone)
                {
                    terrainProtoMap[i] = -1;
                    standaloneEntries.Add(activeEntries[i]);
                }
                else
                {
                    terrainProtoMap[i] = terrainEntries.Count;
                    terrainEntries.Add(activeEntries[i]);
                }
            }

            // Register terrain tree prototypes
            if (terrainEntries.Count > 0)
            {
                var protos = terrainEntries
                    .Select(e => new TreePrototype { prefab = e.prefab })
                    .ToArray();
                terrainData.treePrototypes = protos;
            }

            // Build cumulative weight array (over ALL active entries)
            float totalWeight = 0;
            var cumulativeWeights = new float[activeEntries.Count];
            for (int i = 0; i < activeEntries.Count; i++)
            {
                totalWeight += activeEntries[i].weight;
                cumulativeWeights[i] = totalWeight;
            }

            // Standalone container
            GameObject standaloneContainer = null;
            if (standaloneEntries.Count > 0)
            {
                standaloneContainer = new GameObject(StandaloneContainerName);
                if (parentRoot != null)
                    standaloneContainer.transform.SetParent(parentRoot);
            }

            // ---- Poisson disk sampling (grid-jitter approximation) ----
            var terrainTrees = new List<TreeInstance>();
            int standaloneCount = 0;
            var rng = new System.Random(42);
            var typeCounts = new int[activeEntries.Count];

            float cellSize = MinSpacing;
            int cellsX = Mathf.FloorToInt(tWidth / cellSize);
            int cellsZ = Mathf.FloorToInt(tLength / cellSize);

            for (int cz = 0; cz < cellsZ; cz++)
            {
                for (int cx = 0; cx < cellsX; cx++)
                {
                    float worldX = (cx + (float)rng.NextDouble()) * cellSize;
                    float worldZ = (cz + (float)rng.NextDouble()) * cellSize;

                    float nx = worldX / tWidth;
                    float nz = worldZ / tLength;
                    if (nx < 0 || nx >= 1 || nz < 0 || nz >= 1) continue;

                    // Check tree mask (90° CCW rotation matching splatmap pipeline)
                    int maskX = Mathf.Clamp(
                        Mathf.FloorToInt(nz * maskW), 0, maskW - 1);
                    int maskY = Mathf.Clamp(
                        Mathf.FloorToInt(nx * maskH), 0, maskH - 1);
                    if (mask[maskY * maskW + maskX] == 0) continue;

                    // Zone grid exclusion
                    if (zoneGrid != null)
                    {
                        int zx = Mathf.Clamp(
                            Mathf.FloorToInt(nz * zoneW), 0, zoneW - 1);
                        int zy = Mathf.Clamp(
                            Mathf.FloorToInt(nx * zoneH), 0, zoneH - 1);
                        int zone = zoneGrid[zy * zoneW + zx];
                        if (ExcludeZones.Contains(zone)) continue;
                    }

                    // Overlay mesh polygon exclusion (catches cart paths
                    // and any overlay that extends beyond zone grid pixels)
                    if (exclusionPolygons.Count > 0)
                    {
                        float absX = worldX + terrain.transform.position.x;
                        float absZ = worldZ + terrain.transform.position.z;
                        if (IsInsideAnyOverlay(absX, absZ, exclusionPolygons))
                            continue;
                    }

                    // Pick prototype (weighted random)
                    float roll = (float)rng.NextDouble() * totalWeight;
                    int entryIdx = 0;
                    for (int i = 0; i < cumulativeWeights.Length; i++)
                    {
                        if (roll <= cumulativeWeights[i])
                        {
                            entryIdx = i;
                            break;
                        }
                    }

                    float scale = ScaleMin +
                        (float)rng.NextDouble() * (ScaleMax - ScaleMin);
                    float rotDeg = (float)rng.NextDouble() * 360f;

                    // Sample terrain height
                    Vector3 worldPos = new Vector3(
                        worldX + terrain.transform.position.x,
                        0f,
                        worldZ + terrain.transform.position.z);
                    float terrainH = terrain.SampleHeight(worldPos);

                    typeCounts[entryIdx]++;

                    if (terrainProtoMap[entryIdx] >= 0)
                    {
                        // ---- Terrain tree ----
                        float ny = Mathf.Max(0f,
                            (terrainH - SinkOffset) / terrainData.size.y);
                        terrainTrees.Add(new TreeInstance
                        {
                            position = new Vector3(nx, ny, nz),
                            widthScale = scale,
                            heightScale = scale,
                            rotation = rotDeg * Mathf.Deg2Rad,
                            color = Color.white,
                            lightmapColor = Color.white,
                            prototypeIndex = terrainProtoMap[entryIdx],
                        });
                    }
                    else
                    {
                        // ---- Standalone tree ----
                        // Use Object.Instantiate (not PrefabUtility) to break
                        // the prefab link so LODGroup overrides stick.
                        var entry = activeEntries[entryIdx];
                        var instance = Object.Instantiate(entry.prefab);
                        instance.name = $"{entry.name}_{standaloneCount}";

                        float y = terrainBaseY + terrainH - SinkOffset;
                        instance.transform.position = new Vector3(
                            worldPos.x, y, worldPos.z);
                        instance.transform.rotation =
                            Quaternion.Euler(0f, rotDeg, 0f);
                        instance.transform.localScale = Vector3.one * scale;

                        if (standaloneContainer != null)
                            instance.transform.SetParent(
                                standaloneContainer.transform);

                        standaloneCount++;
                    }
                }
            }

            // Apply terrain trees
            terrainData.SetTreeInstances(terrainTrees.ToArray(), false);

            // ---- Unify draw distances ----
            terrain.treeDistance = DrawDistance;
            terrain.treeBillboardDistance = BillboardDistance;
            terrain.treeCrossFadeLength = CrossFadeLength;
            terrain.treeMaximumFullLODCount = MaxFullLODCount;

            // ---- Normalize LODGroup thresholds for terrain prototypes ----
            foreach (var proto in terrainData.treePrototypes)
            {
                if (proto.prefab == null) continue;
                var lodGroup = proto.prefab.GetComponent<LODGroup>();
                if (lodGroup == null) continue;

                NormalizeLODGroup(lodGroup);
            }

            // ---- Normalize LODGroup thresholds for standalone trees ----
            if (standaloneContainer != null)
            {
                foreach (var lodGroup in standaloneContainer
                    .GetComponentsInChildren<LODGroup>(true))
                {
                    NormalizeLODGroup(lodGroup);
                }
            }

            // Summary
            var summary = string.Join(", ",
                activeEntries.Select((e, i) =>
                    $"{e.name}={typeCounts[i]}{(e.standalone ? "(GO)" : "")}"));

            Debug.Log($"[TreePlacer] Placed {terrainTrees.Count} terrain + " +
                $"{standaloneCount} standalone = " +
                $"{terrainTrees.Count + standaloneCount} total " +
                $"({activeEntries.Count} types, {cellSize}m spacing, seed=42, " +
                $"{exclusionPolygons.Count} exclusion polygons)" +
                $"\n  {summary}");
        }

        /// <summary>
        /// Apply uniform LOD thresholds + CrossFade to a LODGroup.
        /// Works for both terrain prototype prefabs and standalone instances.
        /// </summary>
        private static void NormalizeLODGroup(LODGroup lodGroup)
        {
            var lods = lodGroup.GetLODs();
            if (lods.Length == 0) return;

            // Distribute thresholds evenly from LOD0Threshold down to LOD2Threshold
            // across ALL LOD levels. Must be monotonically decreasing or
            // SetLODs silently rejects the whole array.
            for (int i = 0; i < lods.Length; i++)
            {
                float t = (lods.Length == 1) ? 0f : (float)i / (lods.Length - 1);
                lods[i].screenRelativeTransitionHeight =
                    Mathf.Lerp(LOD0Threshold, LOD2Threshold, t);
            }

            lodGroup.SetLODs(lods);
            lodGroup.fadeMode = LODFadeMode.CrossFade;
            lodGroup.animateCrossFading = true;
        }

        /// <summary>
        /// Remove standalone tree container from the scene.
        /// Called before re-placing trees.
        /// </summary>
        public static void CleanupStandaloneTrees()
        {
            // Find all containers including inactive
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == StandaloneContainerName && go.scene.isLoaded)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        // ---- Session persistence (survives Play mode / domain reload) ----

        private const string SessionFile = "Temp/TreePlacerSession.json";

        /// <summary>
        /// Auto-save current state to Temp/ so it survives domain reload.
        /// Called by InitializeOnLoad callback.
        /// </summary>
        public static void SaveSession()
        {
            var data = BuildSavedSettings();
            string path = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), SessionFile);
            File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        /// <summary>
        /// Auto-load session state after domain reload.
        /// Called by InitializeOnLoad callback.
        /// </summary>
        public static bool LoadSession()
        {
            string path = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), SessionFile);
            if (!File.Exists(path)) return false;

            var data = JsonUtility.FromJson<SavedSettings>(
                File.ReadAllText(path));
            ApplySavedSettings(data);
            return true;
        }

        // ---- Save / Load Presets (manual) ----

        private const string SettingsFolder = "Assets/Settings/TreePresets";

        [System.Serializable]
        private class SavedEntry
        {
            public string path;
            public bool enabled;
            public float weight;
            public bool standalone;
        }

        [System.Serializable]
        private class SavedSettings
        {
            public float minSpacing;
            public float scaleMin;
            public float scaleMax;
            public float sinkOffset;
            public float drawDistance;
            public float billboardDistance;
            public float crossFadeLength;
            public int maxFullLODCount;
            public float lod0;
            public float lod1;
            public float lod2;
            public SavedEntry[] entries;
        }

        private static SavedSettings BuildSavedSettings()
        {
            return new SavedSettings
            {
                minSpacing = MinSpacing,
                scaleMin = ScaleMin,
                scaleMax = ScaleMax,
                sinkOffset = SinkOffset,
                drawDistance = DrawDistance,
                billboardDistance = BillboardDistance,
                crossFadeLength = CrossFadeLength,
                maxFullLODCount = MaxFullLODCount,
                lod0 = LOD0Threshold,
                lod1 = LOD1Threshold,
                lod2 = LOD2Threshold,
                entries = TreePalette.Select(e => new SavedEntry
                {
                    path = e.path,
                    enabled = e.enabled,
                    weight = e.weight,
                    standalone = e.standalone,
                }).ToArray(),
            };
        }

        private static void ApplySavedSettings(SavedSettings data)
        {
            MinSpacing = data.minSpacing;
            ScaleMin = data.scaleMin;
            ScaleMax = data.scaleMax;
            SinkOffset = data.sinkOffset;
            DrawDistance = data.drawDistance;
            BillboardDistance = data.billboardDistance;
            CrossFadeLength = data.crossFadeLength;
            MaxFullLODCount = data.maxFullLODCount;
            LOD0Threshold = data.lod0;
            LOD1Threshold = data.lod1;
            LOD2Threshold = data.lod2;

            if (data.entries != null)
            {
                var lookup = new Dictionary<string, SavedEntry>();
                foreach (var se in data.entries)
                    lookup[se.path] = se;

                foreach (var entry in TreePalette)
                {
                    if (lookup.TryGetValue(entry.path, out var saved))
                    {
                        entry.enabled = saved.enabled;
                        entry.weight = saved.weight;
                        entry.standalone = saved.standalone;
                    }
                }
            }
        }

        /// <summary>
        /// Save current settings to a user-chosen JSON file.
        /// </summary>
        public static void SavePreset()
        {
            string absFolder = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), SettingsFolder);
            if (!Directory.Exists(absFolder))
            {
                Directory.CreateDirectory(absFolder);
                AssetDatabase.Refresh();
            }

            string savePath = EditorUtility.SaveFilePanel(
                "Save Tree Preset", absFolder, "TreePreset", "json");
            if (string.IsNullOrEmpty(savePath)) return;

            File.WriteAllText(savePath, JsonUtility.ToJson(BuildSavedSettings(), true));
            AssetDatabase.Refresh();
            Debug.Log($"[TreePlacer] Preset saved: {Path.GetFileName(savePath)}");
        }

        /// <summary>
        /// Load settings from a user-chosen JSON file.
        /// </summary>
        public static void LoadPreset()
        {
            string absFolder = Path.Combine(
                Path.GetDirectoryName(Application.dataPath), SettingsFolder);
            if (!Directory.Exists(absFolder))
                absFolder = Path.GetDirectoryName(Application.dataPath);

            string loadPath = EditorUtility.OpenFilePanel(
                "Load Tree Preset", absFolder, "json");
            if (string.IsNullOrEmpty(loadPath)) return;

            if (!File.Exists(loadPath))
            {
                Debug.LogWarning($"[TreePlacer] File not found: {loadPath}");
                return;
            }

            // Ensure palette is populated before applying
            if (TreePalette.Count == 0) ScanPrefabs();

            var data = JsonUtility.FromJson<SavedSettings>(
                File.ReadAllText(loadPath));
            ApplySavedSettings(data);
            SaveSession(); // persist to session so Play mode doesn't lose it
            Debug.Log($"[TreePlacer] Preset loaded: {Path.GetFileName(loadPath)}");
        }

        [MenuItem("Trees/Import Trees Current Hole")]
        private static void ImportTreesMenuItem()
        {
            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[TreePlacer] No active terrain found");
                return;
            }

            string sceneName = UnityEditor.SceneManagement.EditorSceneManager
                .GetActiveScene().name;
            int holeNumber = -1;
            bool isGeo = sceneName.EndsWith("_Geo");
            string baseName = isGeo ? sceneName.Replace("_Geo", "") : sceneName;
            if (baseName.StartsWith("Hole_") && baseName.Length >= 7)
                int.TryParse(baseName.Substring(5, 2), out holeNumber);

            if (holeNumber < 1 || holeNumber > 18)
            {
                Debug.LogError($"[TreePlacer] Cannot detect hole number " +
                    $"from scene '{sceneName}' (expected Hole_XX or Hole_XX_Geo)");
                return;
            }

            string toolFolder = isGeo ? "UHoleGeo" : "UHoleLite";
            string exportPath = Path.Combine(
                Application.dataPath, "..",
                $"Tools/{toolFolder}/output/lomond-country-club/export",
                $"hole-{holeNumber:D2}");

            if (!Directory.Exists(exportPath))
            {
                Debug.LogError($"[TreePlacer] Export folder not found: {exportPath}");
                return;
            }

            // Clear terrain trees
            terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);

            // Clear standalone trees
            CleanupStandaloneTrees();

            // Find HoleRoot as parent
            Transform parentRoot = null;
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go.name == "HoleRoot" && go.scene.isLoaded)
                {
                    parentRoot = go.transform;
                    break;
                }
            }

            float terrainBaseY = terrain.transform.position.y;
            string zonesPath = Path.Combine(exportPath, "zones.json");
            PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);

            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[TreePlacer] Scene saved: {scene.path}");
        }

        [MenuItem("Trees/Import All Trees Lite")]
        private static void ImportAllTreesLiteMenuItem()
        {
            // Save current scene before switching
            var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (currentScene.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);

            string scenesDir = "Assets/Golf/Courses/lomond-country-club/Generated";
            string exportBase = Path.Combine(
                Application.dataPath, "..",
                "Tools/UHoleLite/output/lomond-country-club/export");

            if (TreePalette.Count == 0) ScanPrefabs();

            int imported = 0;
            int skipped = 0;

            for (int h = 1; h <= 18; h++)
            {
                string scenePath = $"{scenesDir}/Hole_{h:D2}.unity";
                if (!File.Exists(Path.Combine(Application.dataPath, "..",
                    scenePath)))
                {
                    Debug.Log($"[TreePlacer] Hole {h}: scene not found, skipping");
                    skipped++;
                    continue;
                }

                string exportPath = Path.Combine(exportBase, $"hole-{h:D2}");
                string tzPath = Path.Combine(exportPath, "tree-zones.json");
                if (!File.Exists(tzPath))
                {
                    Debug.Log($"[TreePlacer] Hole {h}: no tree-zones.json, skipping");
                    skipped++;
                    continue;
                }

                // Open the hole scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(scenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                var terrain = Terrain.activeTerrain;
                if (terrain == null)
                {
                    Debug.LogWarning($"[TreePlacer] Hole {h}: no active terrain");
                    skipped++;
                    continue;
                }

                // Clear existing trees
                terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
                CleanupStandaloneTrees();

                // Find HoleRoot
                Transform parentRoot = null;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.name == "HoleRoot" && go.scene.isLoaded)
                    {
                        parentRoot = go.transform;
                        break;
                    }
                }

                float terrainBaseY = terrain.transform.position.y;
                string zonesPath = Path.Combine(exportPath, "zones.json");
                PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                imported++;
            }

            Debug.Log($"[TreePlacer] Lite holes done: {imported} imported, {skipped} skipped");
        }

        [MenuItem("Trees/Import All Trees Geo")]
        private static void ImportAllTreesGeoMenuItem()
        {
            var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (currentScene.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);

            string scenesDir = "Assets/Golf/Courses/lomond-country-club/Generated";
            string exportBase = Path.Combine(
                Application.dataPath, "..",
                "Tools/UHoleGeo/output/lomond-country-club/export");

            if (TreePalette.Count == 0) ScanPrefabs();

            int imported = 0;
            int skipped = 0;

            for (int h = 1; h <= 18; h++)
            {
                string scenePath = $"{scenesDir}/Hole_{h:D2}_Geo.unity";
                if (!File.Exists(Path.Combine(Application.dataPath, "..", scenePath)))
                {
                    Debug.Log($"[TreePlacer] Geo Hole {h}: scene not found, skipping");
                    skipped++;
                    continue;
                }

                string exportPath = Path.Combine(exportBase, $"hole-{h:D2}");
                string tzPath = Path.Combine(exportPath, "tree-zones.json");
                if (!File.Exists(tzPath))
                {
                    Debug.Log($"[TreePlacer] Geo Hole {h}: no tree-zones.json, skipping");
                    skipped++;
                    continue;
                }

                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(scenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                var terrain = Terrain.activeTerrain;
                if (terrain == null)
                {
                    Debug.LogWarning($"[TreePlacer] Geo Hole {h}: no active terrain");
                    skipped++;
                    continue;
                }

                terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
                CleanupStandaloneTrees();

                Transform parentRoot = null;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.name == "HoleRoot" && go.scene.isLoaded)
                    {
                        parentRoot = go.transform;
                        break;
                    }
                }

                float terrainBaseY = terrain.transform.position.y;
                string zonesPath = Path.Combine(exportPath, "zones.json");
                PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                imported++;
            }

            Debug.Log($"[TreePlacer] Geo holes done: {imported} imported, {skipped} skipped");
        }

        [MenuItem("Trees/Import All Trees Lite Flat")]
        private static void ImportAllTreesLiteFlatMenuItem()
        {
            var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (currentScene.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);

            string scenesDir = "Assets/Golf/Courses/lomond-country-club/Generated";
            string exportBase = Path.Combine(
                Application.dataPath, "..",
                "Tools/UHoleLite/output/lomond-country-club/export");

            if (TreePalette.Count == 0) ScanPrefabs();

            int imported = 0;
            int skipped = 0;

            for (int h = 1; h <= 18; h++)
            {
                string scenePath = $"{scenesDir}/Hole_{h:D2}_Flat.unity";
                if (!File.Exists(Path.Combine(Application.dataPath, "..",
                    scenePath)))
                {
                    Debug.Log($"[TreePlacer] Lite Flat Hole {h}: scene not found, skipping");
                    skipped++;
                    continue;
                }

                string exportPath = Path.Combine(exportBase, $"hole-{h:D2}-flat");
                string tzPath = Path.Combine(exportPath, "tree-zones.json");
                if (!File.Exists(tzPath))
                {
                    Debug.Log($"[TreePlacer] Lite Flat Hole {h}: no tree-zones.json, skipping");
                    skipped++;
                    continue;
                }

                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(scenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                var terrain = Terrain.activeTerrain;
                if (terrain == null)
                {
                    Debug.LogWarning($"[TreePlacer] Lite Flat Hole {h}: no active terrain");
                    skipped++;
                    continue;
                }

                terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
                CleanupStandaloneTrees();

                Transform parentRoot = null;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.name == "HoleRoot" && go.scene.isLoaded)
                    {
                        parentRoot = go.transform;
                        break;
                    }
                }

                float terrainBaseY = terrain.transform.position.y;
                string zonesPath = Path.Combine(exportPath, "zones.json");
                PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                imported++;
            }

            Debug.Log($"[TreePlacer] Lite Flat holes done: {imported} imported, {skipped} skipped");
        }

        [MenuItem("Trees/Import All Trees Geo Flat")]
        private static void ImportAllTreesGeoFlatMenuItem()
        {
            var currentScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (currentScene.isDirty)
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(currentScene);

            string scenesDir = "Assets/Golf/Courses/lomond-country-club/Generated";
            string exportBase = Path.Combine(
                Application.dataPath, "..",
                "Tools/UHoleGeo/output/lomond-country-club/export");

            if (TreePalette.Count == 0) ScanPrefabs();

            int imported = 0;
            int skipped = 0;

            for (int h = 1; h <= 18; h++)
            {
                string scenePath = $"{scenesDir}/Hole_{h:D2}_Geo_Flat.unity";
                if (!File.Exists(Path.Combine(Application.dataPath, "..", scenePath)))
                {
                    Debug.Log($"[TreePlacer] Geo Flat Hole {h}: scene not found, skipping");
                    skipped++;
                    continue;
                }

                string exportPath = Path.Combine(exportBase, $"hole-{h:D2}-flat");
                string tzPath = Path.Combine(exportPath, "tree-zones.json");
                if (!File.Exists(tzPath))
                {
                    Debug.Log($"[TreePlacer] Geo Flat Hole {h}: no tree-zones.json, skipping");
                    skipped++;
                    continue;
                }

                var scene = UnityEditor.SceneManagement.EditorSceneManager
                    .OpenScene(scenePath,
                        UnityEditor.SceneManagement.OpenSceneMode.Single);

                var terrain = Terrain.activeTerrain;
                if (terrain == null)
                {
                    Debug.LogWarning($"[TreePlacer] Geo Flat Hole {h}: no active terrain");
                    skipped++;
                    continue;
                }

                terrain.terrainData.SetTreeInstances(new TreeInstance[0], false);
                CleanupStandaloneTrees();

                Transform parentRoot = null;
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (go.name == "HoleRoot" && go.scene.isLoaded)
                    {
                        parentRoot = go.transform;
                        break;
                    }
                }

                float terrainBaseY = terrain.transform.position.y;
                string zonesPath = Path.Combine(exportPath, "zones.json");
                PlaceTrees(terrain, terrainBaseY, exportPath, zonesPath, parentRoot);

                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                imported++;
            }

            Debug.Log($"[TreePlacer] Geo Flat holes done: {imported} imported, {skipped} skipped");
        }

        [MenuItem("Trees/Import All Trees")]
        private static void ImportAllTreesMenuItem()
        {
            ImportAllTreesLiteMenuItem();
            ImportAllTreesGeoMenuItem();
            ImportAllTreesLiteFlatMenuItem();
            ImportAllTreesGeoFlatMenuItem();
        }
    }

    /// <summary>
    /// Auto-saves TreePlacer state before Play mode and restores it
    /// after domain reload so settings survive Enter/Exit Play.
    /// </summary>
    [InitializeOnLoad]
    public static class TreePlacerSessionPersistence
    {
        static TreePlacerSessionPersistence()
        {
            // Restore session after domain reload (Play mode, recompile)
            TreePlacer.ScanPrefabs();
            if (TreePlacer.LoadSession())
            {
                // Re-sort after applying saved state
                TreePlacer.TreePalette.Sort((a, b) =>
                {
                    if (a.enabled != b.enabled) return a.enabled ? -1 : 1;
                    return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
                });
            }

            // Save session before Play mode starts
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
            {
                TreePlacer.SaveSession();
            }
        }
    }
}
#endif
