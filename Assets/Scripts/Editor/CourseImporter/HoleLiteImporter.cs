#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using andywiecko.BurstTriangulator;

namespace Golfin.CourseImport
{
    public static class HoleLiteImporter
    {
        // ─── Tunable Shore Slope Parameters ─────────────────────────
        /// <summary>Radius in heightmap cells around water to apply slope. At 1025 res, ~0.5m/cell.</summary>
        public static int ShoreRadius = 10;
        /// <summary>Maximum depth of shore depression in meters below flat terrain.</summary>
        public static float ShoreDepthMeters = 0.4f;

        // ─── Terrain Y offset — headroom below flat terrain for water bed.
        // Must be ≥ ShoreDepthMeters + water surface depth (0.05m) + underwater margin (0.3m)
        // so heightmap can represent the full water bed without clamping.
        private static float TerrainYOffset => ShoreDepthMeters;

        // ─── Overlay Terrain Depression ─────────────────────────────
        private const float OverlayDepressionMeters = 0.40f;
        private const float DepressionInsetMeters = 0.20f;

        // ─── Green Elevation ─────────────────────────────────────────
        /// <summary>How far the putting surface sits above the outer collar edge.</summary>
        private const float GreenRaiseMeters = 0.15f;

        // ─── Heightmap Smoothing Parameters ─────────────────────────
        private const int SmoothRadius = 16;
        private const float SmoothSigma = 32.0f;
        private const int TransitionCells = 80;
        private static readonly System.Collections.Generic.HashSet<int> PlayZones =
            new System.Collections.Generic.HashSet<int> { 1, 2, 6, 7, 8, 10 };

        [MenuItem("Import/Lite/Normal/Import Hole 01 Lite")] public static void Lite01() { ImportLiteHole("lomond-country-club", 1); }
        [MenuItem("Import/Lite/Normal/Import Hole 02 Lite")] public static void Lite02() { ImportLiteHole("lomond-country-club", 2); }
        [MenuItem("Import/Lite/Normal/Import Hole 03 Lite")] public static void Lite03() { ImportLiteHole("lomond-country-club", 3); }
        [MenuItem("Import/Lite/Normal/Import Hole 04 Lite")] public static void Lite04() { ImportLiteHole("lomond-country-club", 4); }
        [MenuItem("Import/Lite/Normal/Import Hole 05 Lite")] public static void Lite05() { ImportLiteHole("lomond-country-club", 5); }
        [MenuItem("Import/Lite/Normal/Import Hole 06 Lite")] public static void Lite06() { ImportLiteHole("lomond-country-club", 6); }
        [MenuItem("Import/Lite/Normal/Import Hole 07 Lite")] public static void Lite07() { ImportLiteHole("lomond-country-club", 7); }
        [MenuItem("Import/Lite/Normal/Import Hole 08 Lite")] public static void Lite08() { ImportLiteHole("lomond-country-club", 8); }
        [MenuItem("Import/Lite/Normal/Import Hole 09 Lite")] public static void Lite09() { ImportLiteHole("lomond-country-club", 9); }
        [MenuItem("Import/Lite/Normal/Import Hole 10 Lite")] public static void Lite10() { ImportLiteHole("lomond-country-club", 10); }
        [MenuItem("Import/Lite/Normal/Import Hole 11 Lite")] public static void Lite11() { ImportLiteHole("lomond-country-club", 11); }
        [MenuItem("Import/Lite/Normal/Import Hole 12 Lite")] public static void Lite12() { ImportLiteHole("lomond-country-club", 12); }
        [MenuItem("Import/Lite/Normal/Import Hole 13 Lite")] public static void Lite13() { ImportLiteHole("lomond-country-club", 13); }
        [MenuItem("Import/Lite/Normal/Import Hole 14 Lite")] public static void Lite14() { ImportLiteHole("lomond-country-club", 14); }
        [MenuItem("Import/Lite/Normal/Import Hole 15 Lite")] public static void Lite15() { ImportLiteHole("lomond-country-club", 15); }
        [MenuItem("Import/Lite/Normal/Import Hole 16 Lite")] public static void Lite16() { ImportLiteHole("lomond-country-club", 16); }
        [MenuItem("Import/Lite/Normal/Import Hole 17 Lite")] public static void Lite17() { ImportLiteHole("lomond-country-club", 17); }
        [MenuItem("Import/Lite/Normal/Import Hole 18 Lite")] public static void Lite18() { ImportLiteHole("lomond-country-club", 18); }

        [MenuItem("Import/Lite/Normal/Import All Holes Lite")]
        public static void LiteAll()
        {
            for (int i = 1; i <= 18; i++)
                ImportLiteHole("lomond-country-club", i);
        }

        [MenuItem("Import/Lite/Flat/Import Hole 01 Flat")] public static void LiteFlat01() { ImportLiteHoleFlat("lomond-country-club", 1); }
        [MenuItem("Import/Lite/Flat/Import Hole 02 Flat")] public static void LiteFlat02() { ImportLiteHoleFlat("lomond-country-club", 2); }
        [MenuItem("Import/Lite/Flat/Import Hole 03 Flat")] public static void LiteFlat03() { ImportLiteHoleFlat("lomond-country-club", 3); }
        [MenuItem("Import/Lite/Flat/Import Hole 04 Flat")] public static void LiteFlat04() { ImportLiteHoleFlat("lomond-country-club", 4); }
        [MenuItem("Import/Lite/Flat/Import Hole 05 Flat")] public static void LiteFlat05() { ImportLiteHoleFlat("lomond-country-club", 5); }
        [MenuItem("Import/Lite/Flat/Import Hole 06 Flat")] public static void LiteFlat06() { ImportLiteHoleFlat("lomond-country-club", 6); }
        [MenuItem("Import/Lite/Flat/Import Hole 07 Flat")] public static void LiteFlat07() { ImportLiteHoleFlat("lomond-country-club", 7); }
        [MenuItem("Import/Lite/Flat/Import Hole 08 Flat")] public static void LiteFlat08() { ImportLiteHoleFlat("lomond-country-club", 8); }
        [MenuItem("Import/Lite/Flat/Import Hole 09 Flat")] public static void LiteFlat09() { ImportLiteHoleFlat("lomond-country-club", 9); }
        [MenuItem("Import/Lite/Flat/Import Hole 10 Flat")] public static void LiteFlat10() { ImportLiteHoleFlat("lomond-country-club", 10); }
        [MenuItem("Import/Lite/Flat/Import Hole 11 Flat")] public static void LiteFlat11() { ImportLiteHoleFlat("lomond-country-club", 11); }
        [MenuItem("Import/Lite/Flat/Import Hole 12 Flat")] public static void LiteFlat12() { ImportLiteHoleFlat("lomond-country-club", 12); }
        [MenuItem("Import/Lite/Flat/Import Hole 13 Flat")] public static void LiteFlat13() { ImportLiteHoleFlat("lomond-country-club", 13); }
        [MenuItem("Import/Lite/Flat/Import Hole 14 Flat")] public static void LiteFlat14() { ImportLiteHoleFlat("lomond-country-club", 14); }
        [MenuItem("Import/Lite/Flat/Import Hole 15 Flat")] public static void LiteFlat15() { ImportLiteHoleFlat("lomond-country-club", 15); }
        [MenuItem("Import/Lite/Flat/Import Hole 16 Flat")] public static void LiteFlat16() { ImportLiteHoleFlat("lomond-country-club", 16); }
        [MenuItem("Import/Lite/Flat/Import Hole 17 Flat")] public static void LiteFlat17() { ImportLiteHoleFlat("lomond-country-club", 17); }
        [MenuItem("Import/Lite/Flat/Import Hole 18 Flat")] public static void LiteFlat18() { ImportLiteHoleFlat("lomond-country-club", 18); }

        [MenuItem("Import/Lite/Flat/Import All Holes Flat")]
        public static void LiteAllFlat()
        {
            for (int i = 1; i <= 18; i++)
                ImportLiteHoleFlat("lomond-country-club", i);
        }

        public static void ImportLiteHole(string courseId, int holeNumber)
        {
            string holeId = holeNumber.ToString("D2");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            string exportPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
                courseId, "export", $"hole-{holeId}");
            string generatedDir = $"Assets/Golf/Courses/{courseId}/Generated";
            string dataDir = $"Assets/Golf/Courses/{courseId}/Data/hole-{holeId}";
            string scenePath = $"{generatedDir}/Hole_{holeId}.unity";

            ImportHoleInternal(courseId, holeNumber, exportPath, dataDir, scenePath, "Lite");
        }

        public static void ImportLiteHoleFlat(string courseId, int holeNumber)
        {
            string holeId = holeNumber.ToString("D2");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            string exportPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
                courseId, "export", $"hole-{holeId}-flat");
            string generatedDir = $"Assets/Golf/Courses/{courseId}/Generated";
            string dataDir = $"Assets/Golf/Courses/{courseId}/Data/hole-{holeId}-flat";
            string scenePath = $"{generatedDir}/Hole_{holeId}_Flat.unity";

            ImportHoleInternal(courseId, holeNumber, exportPath, dataDir, scenePath, "LiteFlat");
        }

        private static void ImportHoleInternal(string courseId, int holeNumber,
            string exportPath, string dataDir, string scenePath, string importType = "Lite")
        {
            string holeId = holeNumber.ToString("D2");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string generatedDir = Path.GetDirectoryName(scenePath);

            if (!Directory.Exists(exportPath))
            {
                EditorUtility.DisplayDialog("Import Error",
                    $"Export folder not found:\n{exportPath}\n\nRun the UHole Lite pipeline first.", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Cleaning previous import...", 0f);
                CleanPreviousImport(dataDir, scenePath);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Reading manifest...", 0.05f);

                EnsureDirectory(Path.Combine(projectRoot, generatedDir));
                EnsureDirectory(Path.Combine(projectRoot, dataDir));

                string manifestJson = File.ReadAllText(Path.Combine(exportPath, "hole-manifest.json"));
                var manifest = JsonUtility.FromJson<HoleManifest>(manifestJson);

                if (manifest.pipeline != "uhole-lite")
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("Import Error",
                        "This is not a UHole Lite package. Use GOLFIN > Import Hole instead.", "OK");
                    return;
                }

                string anchorsJson = File.ReadAllText(Path.Combine(exportPath, "anchors.json"));
                var anchorsWrapper = JsonUtility.FromJson<AnchorArrayWrapper>(
                    "{\"items\":" + anchorsJson + "}");
                var anchors = anchorsWrapper.items;

                // Swap X/Z to rotate 90° CCW — matches UHole Lite vertical orientation
                float terrainX = manifest.terrain.terrain_length_m;
                float terrainZ = manifest.terrain.terrain_width_m;

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating scene...", 0.1f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Building terrain...", 0.2f);
                var terrainData = CreateTerrain(manifest, exportPath, dataDir, holeId, projectRoot,
                    terrainX, terrainZ);
                var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrainGO.name = "TerrainRoot";
                terrainGO.transform.position = new Vector3(-terrainX / 2f, -TerrainYOffset, -terrainZ / 2f);

                // Disable reflection probes on terrain
                var terrainComp = terrainGO.GetComponent<Terrain>();
                terrainComp.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                // Create holeRoot early so bunkers can be parented to it
                var holeRoot = new GameObject("HoleRoot");
                terrainGO.transform.SetParent(holeRoot.transform);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Applying texture...", 0.4f);
                ApplySplatmap(terrainData, manifest, exportPath, dataDir, holeId, projectRoot, terrainGO);

                // Read terrain holes once, pass to both zone methods, write once at end
                int holesRes = terrainData.holesResolution;
                bool[,] holes = terrainData.GetHoles(0, 0, holesRes, holesRes);
                Debug.Log($"[HoleLiteImporter] Terrain holes resolution: {holesRes}x{holesRes}");

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating bunkers...", 0.5f);
                CreateZoneMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating greens...", 0.53f);
                CreateGreenMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);


                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating water...", 0.59f);
                CreateWaterMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating zone meshes...", 0.62f);
                CreateFlatZoneMeshes(terrainData, terrainGO, holeRoot.transform,
                    exportPath, dataDir, projectRoot);

                // Place anchor markers BEFORE terrain depression so
                // SampleHeight returns the original undepressed surface
                var terrain = terrainGO.GetComponent<Terrain>();

                var anchorsRoot = new GameObject("Anchors");
                anchorsRoot.transform.SetParent(holeRoot.transform);

                // Load green centroid for tee marker orientation
                Vector3 greenCentroid = Vector3.zero;
                bool hasGreenCentroid = false;
                string greensPath = Path.Combine(exportPath, "greens.json");
                if (File.Exists(greensPath))
                {
                    var greensFile = JsonUtility.FromJson<GreensFileData>(File.ReadAllText(greensPath));
                    if (greensFile.greens != null && greensFile.greens.Length > 0)
                    {
                        var gc = greensFile.greens[0].center_local;
                        // Apply 90° CCW rotation: (x, z) → (z, x)
                        greenCentroid = new Vector3(gc.z, 0f, gc.x);
                        hasGreenCentroid = true;
                    }
                }

                // Load tee contours so markers can be centered on tee surface
                ZoneContourRegion[] teeRegions = null;
                string teeZcPath = Path.Combine(exportPath, "zone-contours.json");
                if (File.Exists(teeZcPath))
                {
                    var zcData = JsonUtility.FromJson<ZoneContoursFile>(
                        File.ReadAllText(teeZcPath));
                    if (zcData.zones != null)
                        teeRegions = zcData.zones.tee;
                }

                // Separate tee anchors from non-tee anchors.
                // Tee anchors are grouped by closest tee region so multiple
                // anchors sharing a region get spaced along the forward axis.
                var teeAnchors = new List<AnchorData>();
                foreach (var anchor in anchors)
                {
                    if (anchor.type.Contains("tee"))
                        teeAnchors.Add(anchor);
                    else
                        PlaceAnchorMarker(anchor, terrain, terrainGO.transform,
                            anchorsRoot.transform, hasGreenCentroid, greenCentroid, teeRegions);
                }

                // Load fairway regions so tee markers can face the closest fairway
                ZoneContourRegion[] fairwayRegions = null;
                string fwPath = Path.Combine(exportPath, "fairway-contours.json");
                if (File.Exists(fwPath))
                {
                    var fwData = JsonUtility.FromJson<FairwayContoursFile>(
                        File.ReadAllText(fwPath));
                    fairwayRegions = fwData.fairways;
                }

                PlaceTeeMarkerGroups(teeAnchors, terrain, terrainGO.transform,
                    anchorsRoot.transform, hasGreenCentroid, greenCentroid, teeRegions,
                    fairwayRegions);

                // Depress terrain under overlay meshes to prevent z-fighting
                DepressTerrainUnderOverlays(terrainData, terrainGO, exportPath);

                terrainData.SetHoles(0, 0, holes);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Building hierarchy...", 0.6f);

                var metadata = holeRoot.AddComponent<HoleMetadata>();
                metadata.courseId = manifest.course_id;
                metadata.holeNumber = manifest.hole_number;
                metadata.importType = importType;
                metadata.par = manifest.par;
                metadata.strokeIndex = manifest.stroke_index;
                metadata.championshipYards = manifest.championship_yards;
                metadata.reviewStatus = manifest.review_status;

                var debugRefs = new GameObject("DebugReferences");
                debugRefs.transform.SetParent(holeRoot.transform);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Setting up camera...", 0.8f);
                CreateWalkCamera(anchors, terrain, terrainGO.transform);

                // ---- Directional Light (Sun) ----
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;

                // Warm sunlight color — slightly less saturated than before
                light.color = new Color(1f, 0.96f, 0.88f);
                light.intensity = 1.2f;

                // Sun position: 45° altitude, 135° azimuth (SE → NW shadows)
                // Simulates mid-morning sun at Lomond CC (~34.9°N latitude)
                lightGO.transform.rotation = Quaternion.Euler(45f, 135f, 0f);

                // Shadows
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.7f;
                light.shadowBias = 0.05f;
                light.shadowNormalBias = 0.4f;
                light.shadowNearPlane = 0.2f;

                // Light mode: Mixed — allows baking later while keeping
                // real-time shadows for dynamic objects (ball, character)
                light.lightmapBakeType = LightmapBakeType.Mixed;

                // Shadow distance — covers playable area without wasting budget
                QualitySettings.shadowDistance = 100f;

                // Terrain shadow casting
                terrainComp.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;

                // URP pipeline shadow distance check
                var pipelineAsset = UnityEngine.Rendering.GraphicsSettings
                    .currentRenderPipeline
                    as UnityEngine.Rendering.Universal
                       .UniversalRenderPipelineAsset;
                if (pipelineAsset != null)
                {
                    var sdField = pipelineAsset.GetType().GetProperty(
                        "shadowDistance");
                    if (sdField != null)
                    {
                        float pipelineShadowDist = (float)sdField.GetValue(
                            pipelineAsset);
                        if (pipelineShadowDist < 100f)
                            Debug.LogWarning(
                                "[HoleLiteImporter] URP shadow distance is " +
                                $"{pipelineShadowDist}m — shadows will clip " +
                                "before 100m. Increase in URP Asset > Shadows.");
                    }
                }

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Placing mountains...", 0.85f);
                PlaceMountainBackdrop(terrain, terrainGO.transform.position.y,
                    terrainX, terrainZ, dataDir, holeRoot.transform);

                // Trees are placed separately via Trees > Import Trees (Current Hole)
                // to avoid overwriting Tree Settings palette on each hole import.

                // Apply skybox
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Skybox/Sky-2.mat");
                if (skyMat != null)
                    RenderSettings.skybox = skyMat;

                // Force-serialize all terrain modifications (splatmap, layers, holes)
                // so the disk asset matches the in-memory state
                EditorUtility.SetDirty(terrainData);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Saving scene...", 0.9f);
                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
                Debug.Log($"[HoleLiteImporter] Hole {holeId} imported — terrain {terrainX:F0}m(X) x {terrainZ:F0}m(Z)");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[HoleLiteImporter] Import failed: {ex}");
                EditorUtility.DisplayDialog("Import Error", ex.Message, "OK");
            }
        }

        private static void CleanPreviousImport(string dataDir, string scenePath)
        {
            // Delete old scene file
            if (AssetDatabase.LoadAssetAtPath<Object>(scenePath) != null)
            {
                AssetDatabase.DeleteAsset(scenePath);
                Debug.Log($"[HoleLiteImporter] Deleted old scene: {scenePath}");
            }

            // Delete all assets in the data directory
            string[] guids = AssetDatabase.FindAssets("", new[] { dataDir });
            if (guids.Length > 0)
            {
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.DeleteAsset(assetPath);
                }
                Debug.Log($"[HoleLiteImporter] Cleaned {guids.Length} old assets from {dataDir}");
            }

            AssetDatabase.Refresh();
        }

        private static TerrainData CreateTerrain(HoleManifest manifest, string exportPath,
            string dataDir, string holeId, string projectRoot, float terrainX, float terrainZ)
        {
            int rawRes = manifest.terrain.resolution; // 1025 from DEM pipeline
            int actualRes = 2049;

            // Elevation range: DEM range + shore depression headroom
            float demRange = manifest.terrain.max_elevation_m;
            if (demRange < 1f) demRange = 1f; // safety floor
            float elevRange = demRange + ShoreDepthMeters + 1.0f;

            // normalizedFlat = the height value that maps to world Y=0
            // terrainGO.position.y = -TerrainYOffset
            // So: normalizedFlat * elevRange + (-TerrainYOffset) = 0
            // normalizedFlat = TerrainYOffset / elevRange
            float normalizedFlat = TerrainYOffset / elevRange;

            // --- Read heightmap.raw (uint16be, rawRes x rawRes) ---
            float[,] heights = new float[actualRes, actualRes];
            string rawPath = Path.Combine(exportPath, "heightmap.raw");
            bool loadedRaw = false;

            if (File.Exists(rawPath))
            {
                byte[] rawBytes = File.ReadAllBytes(rawPath);
                int expectedBytes = rawRes * rawRes * 2;
                if (rawBytes.Length == expectedBytes && rawRes == actualRes)
                {
                    // Direct load — raw resolution matches terrain resolution
                    for (int y = 0; y < rawRes; y++)
                    {
                        for (int x = 0; x < rawRes; x++)
                        {
                            int idx = (y * rawRes + x) * 2;
                            ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
                            float normalized = val / 65535f;
                            heights[y, x] = normalizedFlat + normalized * (demRange / elevRange);
                        }
                    }
                    loadedRaw = true;
                    Debug.Log($"[HoleLiteImporter] Loaded heightmap.raw: {rawRes}x{rawRes}, " +
                              $"elevRange={demRange:F1}m, totalRange={elevRange:F1}m");
                }
                else if (rawBytes.Length == expectedBytes && rawRes != actualRes)
                {
                    // Mismatch: raw is a different resolution — bilinear upsample
                    var rawHeights = new float[rawRes, rawRes];
                    for (int y = 0; y < rawRes; y++)
                    {
                        for (int x = 0; x < rawRes; x++)
                        {
                            int idx = (y * rawRes + x) * 2;
                            ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
                            float normalized = val / 65535f;
                            rawHeights[y, x] = normalizedFlat + normalized * (demRange / elevRange);
                        }
                    }

                    for (int z = 0; z < actualRes; z++)
                    {
                        for (int x = 0; x < actualRes; x++)
                        {
                            float srcX = (float)x / (actualRes - 1) * (rawRes - 1);
                            float srcZ = (float)z / (actualRes - 1) * (rawRes - 1);
                            int x0 = Mathf.FloorToInt(srcX);
                            int z0 = Mathf.FloorToInt(srcZ);
                            int x1 = Mathf.Min(x0 + 1, rawRes - 1);
                            int z1 = Mathf.Min(z0 + 1, rawRes - 1);
                            float fx = srcX - x0;
                            float fz = srcZ - z0;
                            float top = rawHeights[z0, x0] + fx * (rawHeights[z0, x1] - rawHeights[z0, x0]);
                            float bot = rawHeights[z1, x0] + fx * (rawHeights[z1, x1] - rawHeights[z1, x0]);
                            heights[z, x] = top + fz * (bot - top);
                        }
                    }
                    loadedRaw = true;
                    Debug.Log($"[HoleLiteImporter] Loaded heightmap.raw: {rawRes}x{rawRes} " +
                              $"(upsampled to {actualRes}), elevRange={demRange:F1}m");
                }
                else
                {
                    Debug.LogWarning($"[HoleLiteImporter] heightmap.raw size mismatch: " +
                        $"expected {expectedBytes} bytes, got {rawBytes.Length}. Using flat terrain.");
                }
            }
            else
            {
                Debug.LogWarning($"[HoleLiteImporter] heightmap.raw not found at {rawPath}. Using flat terrain.");
            }

            if (!loadedRaw)
            {
                // Fallback: flat terrain (original behavior)
                for (int z = 0; z < actualRes; z++)
                    for (int x = 0; x < actualRes; x++)
                        heights[z, x] = normalizedFlat;
            }

            // --- Smooth heightmap outside play area ---
            if (loadedRaw)
            {
                string zonesSmPath = Path.Combine(exportPath, "zones.json");
                if (File.Exists(zonesSmPath))
                {
                    string zonesSmJson = File.ReadAllText(zonesSmPath);
                    var zonesSmData = JsonUtility.FromJson<ZonesData>(zonesSmJson);
                    byte[] smGrid = System.Convert.FromBase64String(zonesSmData.grid);
                    int smW = zonesSmData.source_dimensions.width;
                    int smH = zonesSmData.source_dimensions.height;

                    // Step 1: Build play-area mask from OB boundary
                    // Everything NOT in OB = play area (keeps raw DEM detail)
                    byte[] smObMask = null;
                    if (!string.IsNullOrEmpty(zonesSmData.ob_mask))
                        smObMask = System.Convert.FromBase64String(zonesSmData.ob_mask);

                    bool[] isPlayArea = new bool[actualRes * actualRes];
                    for (int hz = 0; hz < actualRes; hz++)
                    {
                        for (int hx = 0; hx < actualRes; hx++)
                        {
                            float normX = (float)hx / (actualRes - 1);
                            float normZ = (float)hz / (actualRes - 1);
                            // Reverse 90° CCW: zone.x = normZ, zone.y = normX
                            int gx = Mathf.Clamp(Mathf.RoundToInt(normZ * (smW - 1)), 0, smW - 1);
                            int gy = Mathf.Clamp(Mathf.RoundToInt(normX * (smH - 1)), 0, smH - 1);
                            int obIdx = gy * smW + gx;

                            if (smObMask != null && obIdx < smObMask.Length)
                                isPlayArea[hz * actualRes + hx] = (smObMask[obIdx] == 0);
                            else
                            {
                                // Fallback: use zone-based play area if no OB mask
                                int zone = smGrid[gy * smW + gx];
                                isPlayArea[hz * actualRes + hx] = PlayZones.Contains(zone);
                            }
                        }
                    }

                    // Step 2: Distance transform + blend mask
                    float[] distToPlay = new float[actualRes * actualRes];
                    for (int i = 0; i < distToPlay.Length; i++)
                        distToPlay[i] = isPlayArea[i] ? 0f : float.MaxValue;

                    // Forward pass (chamfer)
                    for (int z = 0; z < actualRes; z++)
                    {
                        for (int x = 0; x < actualRes; x++)
                        {
                            int idx = z * actualRes + x;
                            if (x > 0) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[idx - 1] + 1f);
                            if (z > 0) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[(z - 1) * actualRes + x] + 1f);
                            if (x > 0 && z > 0) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[(z - 1) * actualRes + (x - 1)] + 1.414f);
                            if (x < actualRes - 1 && z > 0) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[(z - 1) * actualRes + (x + 1)] + 1.414f);
                        }
                    }
                    // Backward pass
                    for (int z = actualRes - 1; z >= 0; z--)
                    {
                        for (int x = actualRes - 1; x >= 0; x--)
                        {
                            int idx = z * actualRes + x;
                            if (x < actualRes - 1) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[idx + 1] + 1f);
                            if (z < actualRes - 1) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[(z + 1) * actualRes + x] + 1f);
                            if (x < actualRes - 1 && z < actualRes - 1) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[(z + 1) * actualRes + (x + 1)] + 1.414f);
                            if (x > 0 && z < actualRes - 1) distToPlay[idx] = Mathf.Min(distToPlay[idx], distToPlay[(z + 1) * actualRes + (x - 1)] + 1.414f);
                        }
                    }

                    // Step 3: Gaussian blur (target for non-play areas)
                    float[,] smoothed = GaussianBlur2D(heights, actualRes, SmoothRadius, SmoothSigma);

                    // --- Build boundary height field ---
                    // For play-area cells: boundaryHeight = own height
                    // For non-play cells: propagate from nearest play-area cell
                    float[] boundaryHeight = new float[actualRes * actualRes];
                    for (int i = 0; i < boundaryHeight.Length; i++)
                        boundaryHeight[i] = isPlayArea[i]
                            ? heights[i / actualRes, i % actualRes]
                            : float.MinValue;

                    // Forward pass — propagate boundary heights outward
                    for (int z = 0; z < actualRes; z++)
                    {
                        for (int x = 0; x < actualRes; x++)
                        {
                            int idx = z * actualRes + x;
                            if (isPlayArea[idx]) continue;

                            float bestH = float.MinValue;
                            float bestD = float.MaxValue;

                            // Check neighbors closer to play area
                            if (x > 0)
                            {
                                int ni = idx - 1;
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }
                            if (z > 0)
                            {
                                int ni = (z - 1) * actualRes + x;
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }
                            if (x > 0 && z > 0)
                            {
                                int ni = (z - 1) * actualRes + (x - 1);
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }
                            if (x < actualRes - 1 && z > 0)
                            {
                                int ni = (z - 1) * actualRes + (x + 1);
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }

                            if (bestH > float.MinValue)
                                boundaryHeight[idx] = bestH;
                        }
                    }
                    // Backward pass
                    for (int z = actualRes - 1; z >= 0; z--)
                    {
                        for (int x = actualRes - 1; x >= 0; x--)
                        {
                            int idx = z * actualRes + x;
                            if (isPlayArea[idx]) continue;

                            float bestH = boundaryHeight[idx];
                            float bestD = (bestH > float.MinValue) ? distToPlay[idx] : float.MaxValue;

                            if (x < actualRes - 1)
                            {
                                int ni = idx + 1;
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }
                            if (z < actualRes - 1)
                            {
                                int ni = (z + 1) * actualRes + x;
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }
                            if (x < actualRes - 1 && z < actualRes - 1)
                            {
                                int ni = (z + 1) * actualRes + (x + 1);
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }
                            if (x > 0 && z < actualRes - 1)
                            {
                                int ni = (z + 1) * actualRes + (x - 1);
                                if (boundaryHeight[ni] > float.MinValue && distToPlay[ni] < bestD)
                                { bestD = distToPlay[ni]; bestH = boundaryHeight[ni]; }
                            }

                            if (bestH > float.MinValue)
                                boundaryHeight[idx] = bestH;
                        }
                    }

                    // Fallback: any cell still at sentinel gets normalizedFlat
                    for (int i = 0; i < boundaryHeight.Length; i++)
                        if (boundaryHeight[i] <= float.MinValue)
                            boundaryHeight[i] = normalizedFlat;

                    // Step 4: Blend — play area keeps raw, non-play ramps from
                    // boundary height to smoothed DEM
                    for (int z = 0; z < actualRes; z++)
                    {
                        for (int x = 0; x < actualRes; x++)
                        {
                            int idx = z * actualRes + x;

                            if (isPlayArea[idx])
                                continue; // Play area: keep raw DEM untouched

                            float dist = distToPlay[idx];
                            float bh = boundaryHeight[idx];
                            float demH = smoothed[z, x]; // target = blurred DEM

                            if (dist < TransitionCells)
                            {
                                // Smoothstep ramp: 0 at boundary → 1 at TransitionCells
                                float t = dist / TransitionCells;
                                t = t * t * (3f - 2f * t); // smoothstep
                                heights[z, x] = Mathf.Lerp(bh, demH, t);
                            }
                            else
                            {
                                // Beyond transition: full smoothed DEM
                                heights[z, x] = demH;
                            }
                        }
                    }

                    Debug.Log($"[HoleLiteImporter] Heightmap smoothing applied " +
                        $"(radius={SmoothRadius}, transition={TransitionCells} cells, " +
                        $"boundary-height propagation enabled)");
                }
            }

            // --- Create and save TerrainData ---
            string terrainAssetPath = $"{dataDir}/TerrainData_Hole{holeId}.asset";
            EnsureDirectory(Path.Combine(projectRoot, Path.GetDirectoryName(terrainAssetPath)));

            var existingTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainAssetPath);
            if (existingTerrain != null)
                AssetDatabase.DeleteAsset(terrainAssetPath);

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = actualRes;
            terrainData.alphamapResolution = 1024;
            terrainData.size = new Vector3(terrainX, elevRange, terrainZ);
            terrainData.SetHeights(0, 0, heights);

            AssetDatabase.CreateAsset(terrainData, terrainAssetPath);

            return terrainData;
        }

        private static void ApplyTextureIllustration(TerrainData terrainData, HoleManifest manifest,
            string exportPath, string dataDir, string holeId, string projectRoot)
        {
            string texFile = manifest.texture.file;
            string srcPath = Path.Combine(exportPath, texFile);

            // Rotate texture 90° CCW to match terrain rotation
            string texturePath = $"{dataDir}/texture_hole{holeId}.png";
            string fullTexPath = Path.Combine(projectRoot, texturePath);
            EnsureDirectory(Path.GetDirectoryName(fullTexPath));

            byte[] srcBytes = File.ReadAllBytes(srcPath);
            var srcTex = new Texture2D(2, 2);
            srcTex.LoadImage(srcBytes);
            var rotatedTex = RotateTexture90CCW(srcTex);
            File.WriteAllBytes(fullTexPath, rotatedTex.EncodeToPNG());
            Object.DestroyImmediate(srcTex);
            Object.DestroyImmediate(rotatedTex);

            AssetDatabase.ImportAsset(texturePath);

            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var layer = new TerrainLayer();
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
            layer.tileOffset = Vector2.zero;

            string layerPath = $"{dataDir}/TerrainLayer_Lite.asset";
            AssetDatabase.CreateAsset(layer, layerPath);
            terrainData.terrainLayers = new TerrainLayer[] { layer };
        }

        /// <summary>
        /// Front-to-back ordering for tee types. Lower = closer to green.
        /// Red (ladies) → White (front) → Green (regular) → Blue (back).
        /// </summary>
        private static int TeeTypeOrder(string type)
        {
            if (type.Contains("ladies"))  return 0; // Red — closest to green
            if (type.Contains("front"))   return 1; // White
            if (type.Contains("regular")) return 2; // Green
            if (type.Contains("back"))    return 3; // Blue — farthest from green
            return 4;
        }

        /// <summary>
        /// Group tee anchors by closest tee region and place each group with
        /// proper spacing along the forward axis (toward the green).
        /// When multiple tee types share a region, markers are distributed
        /// evenly and centered, ordered front-to-back (Red, White, Green, Blue).
        /// </summary>
        private static void PlaceTeeMarkerGroups(
            List<AnchorData> teeAnchors,
            Terrain terrain, Transform terrainTransform, Transform parent,
            bool hasGreenCentroid, Vector3 greenCentroid,
            ZoneContourRegion[] teeRegions,
            ZoneContourRegion[] fairwayRegions = null)
        {
            if (teeAnchors.Count == 0) return;

            // Group anchors by their closest tee region
            // Key = region index (-1 if no regions), Value = list of anchors
            var groups = new Dictionary<int, List<AnchorData>>();

            foreach (var anchor in teeAnchors)
            {
                int regionIdx = -1;
                if (teeRegions != null && teeRegions.Length > 0)
                {
                    Vector3 anchorWorld = new Vector3(anchor.local.z, 0f, anchor.local.x);
                    float bestDist = float.MaxValue;
                    for (int r = 0; r < teeRegions.Length; r++)
                    {
                        if (teeRegions[r].contour == null || teeRegions[r].contour.Length < 3)
                            continue;
                        Vector3 rc = new Vector3(teeRegions[r].center_local.z, 0f,
                                                  teeRegions[r].center_local.x);
                        float d = (rc - anchorWorld).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; regionIdx = r; }
                    }
                }

                if (!groups.ContainsKey(regionIdx))
                    groups[regionIdx] = new List<AnchorData>();
                groups[regionIdx].Add(anchor);
            }

            foreach (var kvp in groups)
            {
                var anchorsInGroup = kvp.Value;

                // Sort front-to-back: Red, White, Green, Blue
                anchorsInGroup.Sort((a, b) =>
                    TeeTypeOrder(a.type).CompareTo(TeeTypeOrder(b.type)));

                // Compute tee region centroid
                Vector3 centroid;
                if (kvp.Key >= 0 && teeRegions != null)
                {
                    var region = teeRegions[kvp.Key];
                    float cx = 0f, cz = 0f;
                    int n = region.contour.Length;
                    for (int i = 0; i < n; i++)
                    {
                        cx += region.contour[i].z; // 90° CCW
                        cz += region.contour[i].x;
                    }
                    centroid = new Vector3(cx / n, 0f, cz / n);
                }
                else
                {
                    // Fallback: average of anchor positions
                    float sx = 0, sz = 0;
                    foreach (var a in anchorsInGroup) { sx += a.local.z; sz += a.local.x; }
                    centroid = new Vector3(sx / anchorsInGroup.Count, 0f,
                                           sz / anchorsInGroup.Count);
                }

                // Pair facing: perpendicular to the direction toward the closest fairway.
                // Lite coordinate mapping: center_local.(z, x) → world (X, Z).
                Vector3 groupPerpDir = Vector3.forward; // fallback
                if (fairwayRegions != null)
                {
                    float bestFwDist = float.MaxValue;
                    Vector3 closestFwCenter = Vector3.zero;
                    bool foundFw = false;
                    foreach (var fw in fairwayRegions)
                    {
                        if (fw.contour == null || fw.contour.Length < 3) continue;
                        Vector3 fwCenter = new Vector3(fw.center_local.z, 0f, fw.center_local.x);
                        float d = (fwCenter - centroid).sqrMagnitude;
                        if (d < bestFwDist) { bestFwDist = d; closestFwCenter = fwCenter; foundFw = true; }
                    }
                    if (foundFw)
                    {
                        Vector3 toFairway = (closestFwCenter - centroid);
                        toFairway.y = 0f;
                        if (toFairway.sqrMagnitude > 0.01f)
                        {
                            Vector3 fairwayDir = toFairway.normalized;
                            Vector3 p = Vector3.Cross(Vector3.up, fairwayDir).normalized;
                            if (p.sqrMagnitude > 0.001f) groupPerpDir = p;
                        }
                    }
                }

                int count = anchorsInGroup.Count;

                if (count == 1)
                {
                    // Single marker type — place at centroid, face closest fairway
                    PlaceAnchorMarker(anchorsInGroup[0], terrain, terrainTransform,
                        parent, hasGreenCentroid, greenCentroid, teeRegions,
                        null, groupPerpDir);
                }
                else
                {
                    // Multiple marker types — spread as far apart as possible within
                    // the tee region contour, with a 3m margin from every boundary.
                    Vector3 spreadAxis = Vector3.right;
                    float rangeMin = centroid.x - (count - 1) * 2.5f;
                    float rangeLen = (count - 1) * 5f;

                    if (kvp.Key >= 0 && teeRegions != null)
                    {
                        var region = teeRegions[kvp.Key];
                        if (region.contour != null && region.contour.Length >= 3)
                        {
                            // Build world-space XZ contour (Lite: swap x<->z)
                            var pts = new Vector3[region.contour.Length];
                            for (int i = 0; i < region.contour.Length; i++)
                                pts[i] = new Vector3(region.contour[i].z, 0f, region.contour[i].x);

                            // Scan 36 directions (0–179°) — pick the axis with the
                            // longest span after a 3m inset on each end.
                            float bestAvailable = float.MinValue;
                            for (int s = 0; s < 36; s++)
                            {
                                float angle = s * Mathf.PI / 36f;
                                Vector3 axis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                                float minP = float.MaxValue, maxP = float.MinValue;
                                foreach (var pt in pts)
                                {
                                    float p = Vector3.Dot(pt, axis);
                                    if (p < minP) minP = p;
                                    if (p > maxP) maxP = p;
                                }
                                float available = maxP - minP - 6f; // 3m margin each end
                                if (available > bestAvailable)
                                {
                                    bestAvailable = available;
                                    spreadAxis = axis;
                                    rangeMin = minP + 3f;
                                    rangeLen = Mathf.Max(0f, available);
                                }
                            }
                        }
                    }

                    // g=0 = Red (front), g=last = Blue (back).
                    // Reverse t so Blue ends up at rangeMin (bottom of map).
                    for (int g = 0; g < count; g++)
                    {
                        float t = 1f - (float)g / (count - 1);
                        float proj = rangeMin + t * rangeLen;
                        float cp = Vector3.Dot(centroid, spreadAxis);
                        Vector3 pairCenter = centroid + spreadAxis * (proj - cp);

                        PlaceAnchorMarker(anchorsInGroup[g], terrain, terrainTransform,
                            parent, hasGreenCentroid, greenCentroid, teeRegions,
                            pairCenter, groupPerpDir);
                    }
                }
            }
        }

        private static void PlaceAnchorMarker(AnchorData anchor,
            Terrain terrain, Transform terrainTransform, Transform parent,
            bool hasGreenCentroid, Vector3 greenCentroid,
            ZoneContourRegion[] teeRegions = null,
            Vector3? overridePosition = null,
            Vector3? overridePerpDir = null)
        {
            // 90° CCW rotation: (x, z) → (-z, x) → (local.z, local.x)
            Vector3 worldPos = new Vector3(anchor.local.z, 0f, anchor.local.x);
            float terrainBase = terrainTransform.position.y;

            if (anchor.type.Contains("tee"))
            {
                // Use pre-computed position from PlaceTeeMarkerGroups if available;
                // otherwise fall back to closest tee region centroid.
                if (overridePosition.HasValue)
                {
                    worldPos = overridePosition.Value;
                }
                else if (teeRegions != null && teeRegions.Length > 0)
                {
                    float bestDist = float.MaxValue;
                    ZoneContourRegion bestRegion = null;
                    foreach (var region in teeRegions)
                    {
                        if (region.contour == null || region.contour.Length < 3) continue;
                        Vector3 rc = new Vector3(region.center_local.z, 0f,
                                                 region.center_local.x);
                        float d = (rc - worldPos).sqrMagnitude;
                        if (d < bestDist) { bestDist = d; bestRegion = region; }
                    }

                    if (bestRegion != null)
                    {
                        float cx = 0f, cz = 0f;
                        int n = bestRegion.contour.Length;
                        for (int i = 0; i < n; i++)
                        {
                            cx += bestRegion.contour[i].z;
                            cz += bestRegion.contour[i].x;
                        }
                        worldPos = new Vector3(cx / n, 0f, cz / n);
                    }
                }

                // Determine tee color mapping + scale correction
                // Red and Gold FBX have globalScale=1 in meta, Blue/White have 0.15
                string meshName, matName, teeLabel;
                float scaleFix = 1f;
                if (anchor.type.Contains("back"))
                {
                    meshName = "MESH_BlueTee"; matName = "MAT_BlueTee"; teeLabel = "back";
                }
                else if (anchor.type.Contains("regular"))
                {
                    meshName = "MESH_WhiteTee"; matName = "MAT_GreenTee"; teeLabel = "regular";
                }
                else if (anchor.type.Contains("front"))
                {
                    meshName = "MESH_WhiteTee"; matName = "MAT_WhiteTee"; teeLabel = "front";
                }
                else if (anchor.type.Contains("ladies"))
                {
                    meshName = "MESH_RedTee"; matName = "MAT_RedTee"; teeLabel = "ladies";
                    scaleFix = 0.15f; // Red FBX imports at 1.0 scale, others at 0.15
                }
                else
                {
                    meshName = "MESH_WhiteTee"; matName = "MAT_WhiteTee"; teeLabel = "unknown";
                }

                // Load FBX mesh and material
                var meshPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Art/3D/Props/TeeMarkers/{meshName}.fbx");
                var mat = AssetDatabase.LoadAssetAtPath<Material>(
                    $"Assets/Art/3D/Props/TeeMarkers/Materials/{matName}.mat");

                if (meshPrefab == null)
                {
                    Debug.LogWarning($"[HoleLiteImporter] FBX not found: {meshName}.fbx, falling back to cylinder");
                    PlaceDebugCylinder(anchor, worldPos, terrain, terrainBase, parent);
                    return;
                }

                // Pair faces closest fairway; perpDir comes from PlaceTeeMarkerGroups.
                Vector3 forwardDir = Vector3.forward;
                Vector3 perpDir = overridePerpDir ?? Vector3.forward;
                Quaternion rotation = Quaternion.identity;

                // Place 2 markers: Left and Right, spaced 3m apart (1.5m each side)
                for (int side = 0; side < 2; side++)
                {
                    float offset = (side == 0) ? -1.5f : 1.5f;
                    string suffix = (side == 0) ? "L" : "R";

                    Vector3 markerPos = worldPos + perpDir * offset;
                    // Sample terrain at each marker's own XZ position
                    float terrainHeight = terrain.SampleHeight(
                        new Vector3(markerPos.x, 0f, markerPos.z));

                    var markerGO = Object.Instantiate(meshPrefab);
                    markerGO.name = $"TeeMarker_{teeLabel}_{suffix}";

                    // Apply material to all renderers in the instantiated FBX
                    if (mat != null)
                    {
                        foreach (var rend in markerGO.GetComponentsInChildren<Renderer>())
                            rend.sharedMaterial = mat;
                    }

                    markerGO.transform.rotation = rotation;
                    if (scaleFix != 1f)
                        markerGO.transform.localScale = Vector3.one * scaleFix;

                    // Place at a temp position so bounds reflect actual scale
                    markerGO.transform.position = markerPos;

                    // Measure scaled bounds and lift so base sits on terrain
                    var rends = markerGO.GetComponentsInChildren<Renderer>();
                    float halfHeight = 0f;
                    if (rends.Length > 0)
                        halfHeight = rends[0].bounds.extents.y;
                    markerPos.y = terrainBase + terrainHeight + halfHeight;
                    markerGO.transform.position = markerPos;
                    markerGO.transform.SetParent(parent);
                }
            }
            else
            {
                // Non-tee anchors: keep debug cylinder approach
                PlaceDebugCylinder(anchor, worldPos, terrain, terrainBase, parent);
            }
        }

        private static void PlaceDebugCylinder(AnchorData anchor, Vector3 worldPos,
            Terrain terrain, float terrainBase, Transform parent)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"Anchor_{anchor.type}";
            marker.transform.localScale = new Vector3(2f, 5f, 2f);

            var renderer = marker.GetComponent<Renderer>();
            var mat = new Material(GetLitShader());
            mat.color = Color.yellow;
            renderer.sharedMaterial = mat;

            float terrainHeight = terrain.SampleHeight(worldPos);
            marker.transform.position = new Vector3(
                worldPos.x, terrainBase + terrainHeight + 5f, worldPos.z);

            marker.transform.SetParent(parent);
        }

        private static void CreateWalkCamera(AnchorData[] anchors, Terrain terrain,
            Transform terrainTransform)
        {
            var defaultCam = Camera.main;
            if (defaultCam != null)
                Object.DestroyImmediate(defaultCam.gameObject);

            var camGO = new GameObject("WalkCamera");
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 2000f;
            cam.fieldOfView = 75f;
            camGO.AddComponent<AudioListener>();
            camGO.AddComponent<WalkCamera>();

            var backTee = anchors.FirstOrDefault(a => a.type.Contains("back"));
            if (backTee != null)
            {
                Vector3 pos = new Vector3(backTee.local.z, 0f, backTee.local.x);
                float terrainHeight = terrain.SampleHeight(pos);
                float terrainBase = terrainTransform.position.y;
                camGO.transform.position = new Vector3(pos.x, terrainBase + terrainHeight + 2f, pos.z);
                camGO.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }
        }

        private static Texture2D RotateTexture90CCW(Texture2D orig)
        {
            int oldW = orig.width;
            int oldH = orig.height;
            int newW = oldH;
            int newH = oldW;

            var origPixels = orig.GetPixels();
            var rotatedPixels = new Color[newW * newH];

            for (int ry = 0; ry < newH; ry++)
            {
                for (int rx = 0; rx < newW; rx++)
                {
                    int origX = ry;
                    int origY = oldH - 1 - rx;
                    rotatedPixels[ry * newW + rx] = origPixels[origY * oldW + origX];
                }
            }

            var rotated = new Texture2D(newW, newH);
            rotated.SetPixels(rotatedPixels);
            rotated.Apply();
            return rotated;
        }

        // ─── Splatmap Pipeline ─────────────────────────────────────────────

        private static void ApplySplatmap(TerrainData terrainData, HoleManifest manifest,
            string exportPath, string dataDir, string holeId, string projectRoot,
            GameObject terrainGO = null)
        {
            // --- 1. Parse zone grid ---
            string zonesPath = Path.Combine(exportPath, "zones.json");
            if (!File.Exists(zonesPath))
            {
                Debug.LogWarning("[HoleLiteImporter] zones.json not found, falling back to illustration texture");
                ApplyTextureIllustration(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);
                return;
            }

            string zonesJson = File.ReadAllText(zonesPath);
            var zonesData = JsonUtility.FromJson<ZonesData>(zonesJson);
            // Prefer terrain_grid (preserves real terrain under overlays) over merged grid
            string gridSource = !string.IsNullOrEmpty(zonesData.terrain_grid)
                ? zonesData.terrain_grid : zonesData.grid;
            byte[] grid = System.Convert.FromBase64String(gridSource);
            int zoneW = zonesData.source_dimensions.width;
            int zoneH = zonesData.source_dimensions.height;

            Debug.Log($"[HoleLiteImporter] Zone grid: {zoneW}x{zoneH}, {grid.Length} bytes" +
                (!string.IsNullOrEmpty(zonesData.terrain_grid) ? " (separate terrain layer)" : " (merged)"));

            // --- 2. Resample to alphamap resolution ---
            int alphaRes = 1024;
            terrainData.alphamapResolution = alphaRes;

            byte[] resampledZones = new byte[alphaRes * alphaRes];
            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    float fx = (float)ax / (alphaRes - 1);
                    float fy = (float)ay / (alphaRes - 1);

                    // 90° CCW rotation matching heightmap/anchors:
                    // Alphamap ax → terrain X fraction → zone normY
                    // Alphamap ay → terrain Z fraction → zone normX
                    int gx = Mathf.Clamp(Mathf.RoundToInt(fy * (zoneW - 1)), 0, zoneW - 1);
                    int gy = Mathf.Clamp(Mathf.RoundToInt(fx * (zoneH - 1)), 0, zoneH - 1);

                    resampledZones[ay * alphaRes + ax] = grid[gy * zoneW + gx];
                }
            }

            // --- 3. Build raw alphamap ---
            // (Green fringe ring removed — collar mesh handles green transition)
            int layerCount = 9; // +1 for OB layer
            float[,,] alphamap = new float[alphaRes, alphaRes, layerCount];

            // Load OB mask to overlay OB texture on rough areas
            byte[] obMask = null;
            if (!string.IsNullOrEmpty(zonesData.ob_mask))
                obMask = System.Convert.FromBase64String(zonesData.ob_mask);

            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    int idx = ay * alphaRes + ax;
                    int zone = resampledZones[idx];
                    int layer = ZoneToLayer(zone);

                    // Check OB mask — if this pixel is OB and the
                    // underlying zone is rough (layer 3), use OB layer
                    if (obMask != null && layer == 3)
                    {
                        float fx = (float)ax / (alphaRes - 1);
                        float fy = (float)ay / (alphaRes - 1);
                        int gx = Mathf.Clamp(Mathf.RoundToInt(fy * (zoneW - 1)), 0, zoneW - 1);
                        int gy = Mathf.Clamp(Mathf.RoundToInt(fx * (zoneH - 1)), 0, zoneH - 1);
                        int obIdx = gy * zoneW + gx;
                        if (obIdx < obMask.Length && obMask[obIdx] != 0)
                            layer = 8; // OB layer
                    }

                    alphamap[ay, ax, layer] = 1.0f;
                }
            }

            // --- Smooth rough↔OB boundary (4px blend) ---
            // Since both layers use the same base texture (just tinted),
            // blending creates a gradual color shift — not a texture seam.
            if (obMask != null)
            {
                const int blendRadius = 4;

                // Build distance-to-boundary field at alphamap resolution
                float[] obBorderDist = new float[alphaRes * alphaRes];

                // Step 1: Find boundary pixels (rough↔OB adjacency)
                for (int i = 0; i < alphaRes * alphaRes; i++)
                    obBorderDist[i] = 99999f;

                for (int ay = 0; ay < alphaRes; ay++)
                {
                    for (int ax = 0; ax < alphaRes; ax++)
                    {
                        int idx = ay * alphaRes + ax;
                        bool isOB = alphamap[ay, ax, 8] > 0.5f;
                        bool isRough = alphamap[ay, ax, 3] > 0.5f;
                        if (!isOB && !isRough) continue;

                        // Check 4-neighbors for a rough↔OB transition
                        bool border = false;
                        if (ax > 0) {
                            bool nOB = alphamap[ay, ax-1, 8] > 0.5f;
                            bool nRough = alphamap[ay, ax-1, 3] > 0.5f;
                            if ((isOB && nRough) || (isRough && nOB)) border = true;
                        }
                        if (!border && ax < alphaRes-1) {
                            bool nOB = alphamap[ay, ax+1, 8] > 0.5f;
                            bool nRough = alphamap[ay, ax+1, 3] > 0.5f;
                            if ((isOB && nRough) || (isRough && nOB)) border = true;
                        }
                        if (!border && ay > 0) {
                            bool nOB = alphamap[ay-1, ax, 8] > 0.5f;
                            bool nRough = alphamap[ay-1, ax, 3] > 0.5f;
                            if ((isOB && nRough) || (isRough && nOB)) border = true;
                        }
                        if (!border && ay < alphaRes-1) {
                            bool nOB = alphamap[ay+1, ax, 8] > 0.5f;
                            bool nRough = alphamap[ay+1, ax, 3] > 0.5f;
                            if ((isOB && nRough) || (isRough && nOB)) border = true;
                        }
                        if (border) obBorderDist[idx] = 0f;
                    }
                }

                // Step 2: Chamfer distance transform (forward + backward)
                for (int ay = 0; ay < alphaRes; ay++)
                    for (int ax = 0; ax < alphaRes; ax++) {
                        int idx = ay * alphaRes + ax;
                        if (ax > 0) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx-1] + 1f);
                        if (ay > 0) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx-alphaRes] + 1f);
                    }
                for (int ay = alphaRes-1; ay >= 0; ay--)
                    for (int ax = alphaRes-1; ax >= 0; ax--) {
                        int idx = ay * alphaRes + ax;
                        if (ax < alphaRes-1) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx+1] + 1f);
                        if (ay < alphaRes-1) obBorderDist[idx] = Mathf.Min(obBorderDist[idx], obBorderDist[idx+alphaRes] + 1f);
                    }

                // Step 3: Blend rough↔OB in the transition zone
                for (int ay = 0; ay < alphaRes; ay++)
                {
                    for (int ax = 0; ax < alphaRes; ax++)
                    {
                        int idx = ay * alphaRes + ax;
                        float dist = obBorderDist[idx];
                        if (dist >= blendRadius) continue;

                        bool isOB = alphamap[ay, ax, 8] > 0.5f;
                        bool isRough = alphamap[ay, ax, 3] > 0.5f;
                        if (!isOB && !isRough) continue;

                        // Smoothstep falloff: 1.0 at boundary → 0.0 at blendRadius
                        float t = dist / blendRadius;
                        t = t * t * (3f - 2f * t); // smoothstep
                        float blendAmount = 1f - t;

                        // Cross-fade: mix in 40% of the other texture at the boundary
                        float mixStrength = blendAmount * 0.4f;

                        if (isOB)
                        {
                            alphamap[ay, ax, 8] = 1f - mixStrength;
                            alphamap[ay, ax, 3] = mixStrength;
                        }
                        else // isRough
                        {
                            alphamap[ay, ax, 3] = 1f - mixStrength;
                            alphamap[ay, ax, 8] = mixStrength;
                        }
                    }
                }
            }

            // --- 5. (Zone boundary smoothing now happens at source in classify-zones.mjs) ---

            // --- 5b. Paint cart path texture on terrain at road edges ---
            if (terrainGO != null)
            {
                string cpEdgePath = Path.Combine(exportPath, "cart-paths.json");
                if (File.Exists(cpEdgePath))
                {
                    var cpData = JsonUtility.FromJson<CartPathsFile>(
                        File.ReadAllText(cpEdgePath));
                    if (cpData.cart_paths != null)
                    {
                        // Build mask of cart path cells at alphamap resolution
                        bool[,] cpMask = new bool[alphaRes, alphaRes];
                        Vector3 terrainPos2 = terrainGO.transform.position;
                        Vector3 terrainSize2 = terrainData.size;

                        foreach (var cp in cpData.cart_paths)
                        {
                            if (cp.spine != null && cp.spine.Length >= 2)
                            {
                                float hw = (cp.width_m > 0 ? cp.width_m : 2.5f) / 2f;
                                // Paint slightly wider than the mesh strip so the
                                // splatmap extends beyond mesh edges as a safety margin
                                var poly = BuildSpinePolygon(cp.spine, hw + 0.2f);
                                if (poly != null)
                                {
                                    float minX2 = float.MaxValue, maxX2 = float.MinValue;
                                    float minZ2 = float.MaxValue, maxZ2 = float.MinValue;
                                    foreach (var v in poly)
                                    {
                                        if (v.x < minX2) minX2 = v.x;
                                        if (v.x > maxX2) maxX2 = v.x;
                                        if (v.y < minZ2) minZ2 = v.y;
                                        if (v.y > maxZ2) maxZ2 = v.y;
                                    }

                                    int aMinX = Mathf.Clamp(Mathf.FloorToInt(
                                        (minX2 - terrainPos2.x) / terrainSize2.x
                                        * (alphaRes - 1)), 0, alphaRes - 1);
                                    int aMaxX = Mathf.Clamp(Mathf.CeilToInt(
                                        (maxX2 - terrainPos2.x) / terrainSize2.x
                                        * (alphaRes - 1)), 0, alphaRes - 1);
                                    int aMinZ = Mathf.Clamp(Mathf.FloorToInt(
                                        (minZ2 - terrainPos2.z) / terrainSize2.z
                                        * (alphaRes - 1)), 0, alphaRes - 1);
                                    int aMaxZ = Mathf.Clamp(Mathf.CeilToInt(
                                        (maxZ2 - terrainPos2.z) / terrainSize2.z
                                        * (alphaRes - 1)), 0, alphaRes - 1);

                                    for (int ay = aMinZ; ay <= aMaxZ; ay++)
                                    {
                                        for (int ax = aMinX; ax <= aMaxX; ax++)
                                        {
                                            float cwx = (float)ax / (alphaRes - 1)
                                                * terrainSize2.x + terrainPos2.x;
                                            float cwz = (float)ay / (alphaRes - 1)
                                                * terrainSize2.z + terrainPos2.z;
                                            if (IsInsideContour(cwx, cwz, poly))
                                                cpMask[ay, ax] = true;
                                        }
                                    }
                                }
                            }
                        }

                        // Distance from outside edge (inside the mask)
                        const int edgeWidth = 1; // 1px thin edge strip

                        float[,] cpEdgeDist = new float[alphaRes, alphaRes];
                        for (int ay = 0; ay < alphaRes; ay++)
                            for (int ax = 0; ax < alphaRes; ax++)
                                cpEdgeDist[ay, ax] = cpMask[ay, ax] ? 99999f : 0f;

                        // Chamfer forward
                        for (int ay = 0; ay < alphaRes; ay++)
                            for (int ax = 0; ax < alphaRes; ax++)
                            {
                                if (ax > 0) cpEdgeDist[ay, ax] = Mathf.Min(
                                    cpEdgeDist[ay, ax], cpEdgeDist[ay, ax - 1] + 1f);
                                if (ay > 0) cpEdgeDist[ay, ax] = Mathf.Min(
                                    cpEdgeDist[ay, ax], cpEdgeDist[ay - 1, ax] + 1f);
                            }
                        // Chamfer backward
                        for (int ay = alphaRes - 1; ay >= 0; ay--)
                            for (int ax = alphaRes - 1; ax >= 0; ax--)
                            {
                                if (ax < alphaRes - 1) cpEdgeDist[ay, ax] = Mathf.Min(
                                    cpEdgeDist[ay, ax], cpEdgeDist[ay, ax + 1] + 1f);
                                if (ay < alphaRes - 1) cpEdgeDist[ay, ax] = Mathf.Min(
                                    cpEdgeDist[ay, ax], cpEdgeDist[ay + 1, ax] + 1f);
                            }

                        // Paint cart path texture under the entire strip mesh so
                        // the terrain matches if the mesh breaks at any point.
                        // Interior = 100% cart path; thin edge strip = blended.
                        int cpInteriorPainted = 0;
                        int cpEdgePainted = 0;
                        for (int ay = 0; ay < alphaRes; ay++)
                        {
                            for (int ax = 0; ax < alphaRes; ax++)
                            {
                                if (!cpMask[ay, ax]) continue;
                                float dist = cpEdgeDist[ay, ax];

                                if (dist > edgeWidth)
                                {
                                    // Interior: full cart path texture
                                    for (int l = 0; l < layerCount; l++)
                                        alphamap[ay, ax, l] = 0f;
                                    alphamap[ay, ax, 6] = 1f;
                                    cpInteriorPainted++;
                                }
                                else
                                {
                                    // Edge strip: blend cart path with existing texture
                                    float blend = 0.6f - (dist / edgeWidth) * 0.2f;

                                    int currentLayer = -1;
                                    for (int l = 0; l < layerCount; l++)
                                    {
                                        if (alphamap[ay, ax, l] > 0.5f)
                                        { currentLayer = l; break; }
                                    }
                                    if (currentLayer < 0) currentLayer = 3; // rough fallback

                                    for (int l = 0; l < layerCount; l++)
                                        alphamap[ay, ax, l] = 0f;
                                    alphamap[ay, ax, 6] = blend;
                                    alphamap[ay, ax, currentLayer] = 1f - blend;
                                    cpEdgePainted++;
                                }
                            }
                        }
                        Debug.Log($"[HoleLiteImporter] Cart path splatmap: {cpInteriorPainted} interior + {cpEdgePainted} edge cells painted");
                    }
                }
            }

            // --- 6. Create TerrainLayers and apply ---
            string texDir = "Assets/Courses/Textures_2025(JPG)";

            string[] albedoNames = {
                "T_Fairway_Light",      // 0 fairway (light mow stripe)
                "T_Green_Albedo",       // 1 green
                "T_Semirough_Albedo",   // 2 semi-rough
                "T_Rough_Albedo",       // 3 rough (catch-all)
                "T_Bunker_Albedo",      // 4 bunker
                "T_Tee_Albedo",         // 5 tee
                "T_RoadAsphalt_Albedo", // 6 cart path
                "T_Fairway_Dark",       // 7 dark fairway (mow stripes)
                "T_Rough_Albedo",       // 8 OB — same grass as rough, tinted darker
            };
            string[] normalNames = {
                "T_Fairway_Normal",
                "T_Green_Normal",
                "T_Semirough_Normal",
                "T_Rough_Normal",
                "T_Bunker_Normal",
                "T_Tee_Normal",
                "T_RoadAsphalt_Normal",
                "T_Fairway_Normal",     // 7 dark fairway (mow stripes) — same normal as light fairway
                "T_Rough_Normal",       // 8 OB — same normal as rough
            };
            float[] tileSizes = { 5f, 3f, 6f, 8f, 4f, 3f, 4f, 8f, 10f };

            var layers = new TerrainLayer[layerCount];
            EnsureDirectory(Path.Combine(projectRoot, dataDir));

            // Create a shared "matte" mask map: R=0 (no metallic), G=255 (full AO),
            // B=0 (no detail mask), A=0 (zero smoothness)
            // URP TerrainLit reads smoothness from mask map alpha when present,
            // bypassing the albedo alpha (which JPGs fill with white = plastic sheen).
            string matteMaskPath = $"{dataDir}/MatteMaskMap.png";
            string fullMattePath = Path.Combine(projectRoot, matteMaskPath);
            if (!File.Exists(fullMattePath))
            {
                var matteTex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                Color matteColor = new Color(0f, 1f, 0f, 0f);
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 4; x++)
                        matteTex.SetPixel(x, y, matteColor);
                matteTex.Apply();
                File.WriteAllBytes(fullMattePath, matteTex.EncodeToPNG());
                Object.DestroyImmediate(matteTex);
            }
            AssetDatabase.ImportAsset(matteMaskPath);

            var maskImporter = AssetImporter.GetAtPath(matteMaskPath) as TextureImporter;
            if (maskImporter != null)
            {
                maskImporter.sRGBTexture = false;
                maskImporter.textureType = TextureImporterType.Default;
                maskImporter.textureCompression = TextureImporterCompression.Uncompressed;
                maskImporter.npotScale = TextureImporterNPOTScale.None;
                maskImporter.SaveAndReimport();
            }

            var matteMask = AssetDatabase.LoadAssetAtPath<Texture2D>(matteMaskPath);

            for (int i = 0; i < layerCount; i++)
            {
                layers[i] = new TerrainLayer();
                layers[i].diffuseTexture = FindTextureExact(texDir, albedoNames[i]);
                layers[i].maskMapTexture = matteMask;

                // Force anisotropic filtering on albedo for sharp textures at grazing angles
                if (layers[i].diffuseTexture != null)
                {
                    string albedoPath = AssetDatabase.GetAssetPath(layers[i].diffuseTexture);
                    var albedoImporter = AssetImporter.GetAtPath(albedoPath) as TextureImporter;
                    if (albedoImporter != null && albedoImporter.anisoLevel < 16)
                    {
                        albedoImporter.anisoLevel = 16;
                        albedoImporter.SaveAndReimport();
                    }
                }

                // Re-enable normal maps at reduced intensity
                layers[i].normalMapTexture = FindTextureExact(texDir, normalNames[i]);
                layers[i].normalScale = 0.4f;

                // Ensure normal map is imported as NormalMap type + aniso filtering
                if (layers[i].normalMapTexture != null)
                {
                    string nrmPath = AssetDatabase.GetAssetPath(layers[i].normalMapTexture);
                    var nrmImporter = AssetImporter.GetAtPath(nrmPath) as TextureImporter;
                    if (nrmImporter != null)
                    {
                        bool needsReimport = false;
                        if (nrmImporter.textureType != TextureImporterType.NormalMap)
                        {
                            nrmImporter.textureType = TextureImporterType.NormalMap;
                            needsReimport = true;
                        }
                        if (nrmImporter.anisoLevel < 16)
                        {
                            nrmImporter.anisoLevel = 16;
                            needsReimport = true;
                        }
                        if (needsReimport)
                            nrmImporter.SaveAndReimport();
                    }
                }

                // Fairway (index 0): non-square tile to fix grain orientation on 90° rotated terrain
                layers[i].tileSize = new Vector2(tileSizes[i], tileSizes[i]);
                layers[i].tileOffset = Vector2.zero;
                layers[i].smoothness = 0f;
                layers[i].metallic = 0f;

                if (layers[i].diffuseTexture == null)
                    Debug.LogWarning($"[HoleLiteImporter] Missing texture: {albedoNames[i]}");

                // OB layer (8) reuses T_Rough texture — use distinct asset name to avoid overwriting layer 3
                string layerAssetName = (i == 8) ? "T_OB_TintedRough" : albedoNames[i];
                string layerPath = $"{dataDir}/TerrainLayer_{layerAssetName}.asset";
                var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (existingLayer != null)
                    AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layers[i], layerPath);
            }

            // Tint OB layer slightly darker (same grass, less maintained look)
            // diffuseRemapMin/Max remap the albedo RGB channels.
            // Max < 1.0 = darker. Slightly yellow-green shift = dried grass.
            layers[8].diffuseRemapMin = new Vector4(0f, 0f, 0f, 0f);
            layers[8].diffuseRemapMax = new Vector4(0.75f, 0.82f, 0.55f, 1f);

            terrainData.terrainLayers = layers;
            terrainData.SetAlphamaps(0, 0, alphamap);

            // Copy zones.json to Assets for future runtime use
            string destZonesPath = Path.Combine(projectRoot, dataDir, "zones.json");
            File.Copy(zonesPath, destZonesPath, true);
            AssetDatabase.ImportAsset($"{dataDir}/zones.json");

            Debug.Log($"[HoleLiteImporter] Splatmap applied: {layerCount} layers, " +
                      $"alphamap {alphaRes}x{alphaRes}, no blur (smoothing at source)");
        }

        private static int ZoneToLayer(int zoneIndex)
        {
            return zoneIndex switch
            {
                1  => 3,  // fairway → rough (mesh overlay handles surface)
                2  => 3,  // green → rough (mesh handles surface)
                3  => 2,  // semi_rough
                4  => 3,  // rough
                5  => 3,  // trees → rough texture
                6  => 3,  // bunker → rough (mesh handles sand surface)
                7  => 3,  // water → rough
                8  => 3,  // cart_path → rough (mesh overlay handles surface)
                9  => 3,  // ob → rough texture
                10 => 3,  // tee_box → rough (mesh overlay handles surface)
                _  => 3,  // background/unknown → rough
            };
        }

        private static bool[] DilateMask(bool[] mask, int w, int h, int radius)
        {
            bool[] result = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (mask[y * w + x])
                    {
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                if (dx * dx + dy * dy > radius * radius) continue;
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < w && ny >= 0 && ny < h)
                                    result[ny * w + nx] = true;
                            }
                        }
                    }
                }
            }
            return result;
        }

        private static float[,] ExtractChannel(float[,,] alphamap, int res, int layerCount, int layer)
        {
            float[,] channel = new float[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    channel[y, x] = alphamap[y, x, layer];
            return channel;
        }

        private static void SetChannel(float[,,] alphamap, int res, int layerCount, int layer, float[,] channel)
        {
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    alphamap[y, x, layer] = channel[y, x];
        }

        private static float[,] GaussianBlur2D(float[,] input, int res, int radius, float sigma)
        {
            int kernelSize = radius * 2 + 1;
            float[] kernel = new float[kernelSize];
            float kernelSum = 0f;
            for (int i = 0; i < kernelSize; i++)
            {
                float d = i - radius;
                kernel[i] = Mathf.Exp(-(d * d) / (2f * sigma * sigma));
                kernelSum += kernel[i];
            }
            for (int i = 0; i < kernelSize; i++)
                kernel[i] /= kernelSum;

            // Horizontal pass
            float[,] temp = new float[res, res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float sum = 0f;
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int sx = Mathf.Clamp(x + k - radius, 0, res - 1);
                        sum += input[y, sx] * kernel[k];
                    }
                    temp[y, x] = sum;
                }
            }

            // Vertical pass
            float[,] output = new float[res, res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float sum = 0f;
                    for (int k = 0; k < kernelSize; k++)
                    {
                        int sy = Mathf.Clamp(y + k - radius, 0, res - 1);
                        sum += temp[sy, x] * kernel[k];
                    }
                    output[y, x] = sum;
                }
            }

            return output;
        }

        private static Texture2D FindTextureExact(string dir, string exactName)
        {
            string[] guids = AssetDatabase.FindAssets(exactName, new[] { dir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName == exactName)
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        // ─── Debug: Test Terrain Layers ───────────────────────────────────

        [MenuItem("GOLFIN/Debug/Test Terrain Layers")]
        public static void TestTerrainLayers()
        {
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "No terrain in scene. Import a hole first.", "OK");
                return;
            }

            var terrainData = terrain.terrainData;
            string texDir = "Assets/Courses/Textures_2025(JPG)";

            var fairwayLayer = CreateTestLayer(texDir, "T_Fairway_Light", "T_Fairway_Normal", 5f);
            var roughLayer = CreateTestLayer(texDir, "T_Rough_Albedo", "T_Rough_Normal", 8f);
            var greenLayer = CreateTestLayer(texDir, "T_Green_Albedo", "T_Green_Normal", 3f);

            if (fairwayLayer == null || roughLayer == null || greenLayer == null)
            {
                Debug.LogError("[TestTerrainLayers] Could not find all textures. Check Assets/Courses/Textures_2025(JPG)/");
                return;
            }

            // Save layer assets
            string dataDir = "Assets/Golf/Courses/lomond-country-club/Data/hole-01";
            EnsureDirectory(Path.Combine(Path.GetDirectoryName(Application.dataPath), dataDir));

            SaveLayerAsset(fairwayLayer, $"{dataDir}/TestLayer_Fairway.asset");
            SaveLayerAsset(roughLayer, $"{dataDir}/TestLayer_Rough.asset");
            SaveLayerAsset(greenLayer, $"{dataDir}/TestLayer_Green.asset");

            terrainData.terrainLayers = new TerrainLayer[] { fairwayLayer, roughLayer, greenLayer };

            // Paint test pattern
            int alphaRes = terrainData.alphamapResolution;
            float[,,] alphamap = new float[alphaRes, alphaRes, 3];

            for (int y = 0; y < alphaRes; y++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    float fx = (float)x / alphaRes;
                    float fy = (float)y / alphaRes;

                    // Default: rough
                    int layer = 1;

                    // Middle third horizontal stripe: fairway
                    if (fy > 0.33f && fy < 0.66f)
                        layer = 0;

                    // Small circle at center: green
                    float dx = fx - 0.5f;
                    float dy = fy - 0.5f;
                    if (dx * dx + dy * dy < 0.02f)
                        layer = 2;

                    alphamap[y, x, layer] = 1.0f;
                }
            }

            terrainData.SetAlphamaps(0, 0, alphamap);
            Debug.Log($"[TestTerrainLayers] Applied 3 test layers to terrain (alphamap {alphaRes}x{alphaRes})");
            Debug.Log("[TestTerrainLayers] Pattern: rough (everywhere) + fairway (middle stripe) + green (center circle)");
            Debug.Log("[TestTerrainLayers] CHECK: Do textures tile well? Are sizes reasonable? Walk around in play mode.");
        }

        private static TerrainLayer CreateTestLayer(string texDir, string albedoName, string normalName, float tileSize)
        {
            var albedo = FindTextureInDir(texDir, albedoName);
            var normal = FindTextureInDir(texDir, normalName);

            if (albedo == null)
            {
                Debug.LogWarning($"Could not find texture: {albedoName} in {texDir}");
                return null;
            }

            var layer = new TerrainLayer();
            layer.diffuseTexture = albedo;
            if (normal != null)
                layer.normalMapTexture = normal;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.tileOffset = Vector2.zero;
            return layer;
        }

        private static Texture2D FindTextureInDir(string dir, string namePrefix)
        {
            string[] guids = AssetDatabase.FindAssets(namePrefix, new[] { dir });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName == namePrefix || fileName.StartsWith(namePrefix + "."))
                {
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (tex != null) return tex;
                }
            }
            return null;
        }

        private static void SaveLayerAsset(TerrainLayer layer, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(layer, path);
        }

        // ─── Debug: Test Zone Alignment ──────────────────────────────────

        [MenuItem("GOLFIN/Debug/Test Zone Alignment")]
        public static void TestZoneAlignment()
        {
            var terrain = Object.FindObjectOfType<Terrain>();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("Error", "No terrain in scene. Import a hole first.", "OK");
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // Load zones.json
            string zonesPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
                "lomond-country-club", "export", "hole-01", "zones.json");
            if (!File.Exists(zonesPath))
            {
                EditorUtility.DisplayDialog("Error", $"zones.json not found:\n{zonesPath}", "OK");
                return;
            }
            string zonesJson = File.ReadAllText(zonesPath);
            var zonesData = JsonUtility.FromJson<ZonesData>(zonesJson);

            // Load manifest for terrain dimensions
            string manifestPath = Path.Combine(projectRoot, "Tools", "UHoleLite", "output",
                "lomond-country-club", "export", "hole-01", "hole-manifest.json");
            string manifestJson = File.ReadAllText(manifestPath);
            var manifest = JsonUtility.FromJson<HoleManifest>(manifestJson);

            byte[] grid = System.Convert.FromBase64String(zonesData.grid);
            int w = zonesData.source_dimensions.width;
            int h = zonesData.source_dimensions.height;

            float terrainBase = terrain.transform.position.y;

            // ── Green centroid (zone 2) ──
            float sumX = 0, sumY = 0;
            int greenCount = 0;
            for (int gy = 0; gy < h; gy++)
            {
                for (int gx = 0; gx < w; gx++)
                {
                    if (grid[gy * w + gx] == 2)
                    {
                        sumX += gx;
                        sumY += gy;
                        greenCount++;
                    }
                }
            }

            if (greenCount == 0)
            {
                Debug.LogError("[TestZoneAlignment] No green zone pixels found!");
                return;
            }

            float centroidGX = sumX / greenCount;
            float centroidGY = sumY / greenCount;
            float normX = centroidGX / (w - 1);
            float normY = centroidGY / (h - 1);

            Debug.Log($"[TestZoneAlignment] Green centroid: grid({centroidGX:F1}, {centroidGY:F1}), " +
                      $"norm({normX:F3}, {normY:F3}), {greenCount} pixels");

            // 90° CCW transform: worldX = (normY - 0.5) * terrain_length_m
            //                    worldZ = (normX - 0.5) * terrain_width_m
            float worldX = (normY - 0.5f) * manifest.terrain.terrain_length_m;
            float worldZ = (normX - 0.5f) * manifest.terrain.terrain_width_m;

            // Clean up previous debug sphere
            var old = GameObject.Find("DEBUG_GreenCentroid");
            if (old != null) Object.DestroyImmediate(old);

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "DEBUG_GreenCentroid";
            sphere.transform.localScale = new Vector3(10f, 10f, 10f);

            float terrainHeight = terrain.SampleHeight(new Vector3(worldX, 0, worldZ));
            sphere.transform.position = new Vector3(worldX, terrainBase + terrainHeight + 10f, worldZ);

            var sphereRenderer = sphere.GetComponent<Renderer>();
            var sphereMat = new Material(GetLitShader());
            sphereMat.color = Color.magenta;
            sphereRenderer.sharedMaterial = sphereMat;

            Debug.Log($"[TestZoneAlignment] Placed DEBUG_GreenCentroid at world ({worldX:F1}, {terrainBase + terrainHeight + 10f:F1}, {worldZ:F1})");
            Debug.Log("[TestZoneAlignment] CHECK: Is the magenta sphere on or near the green area?");

            // ── Additional zone centroids: fairway(1), bunker(6), tee_box(10) ──
            int[] debugZones = { 1, 6, 10 };
            string[] debugNames = { "Fairway", "Bunker", "TeeBox" };
            Color[] debugColors = { Color.green, Color.yellow, Color.white };
            float[] debugSizes = { 8f, 6f, 6f };

            for (int i = 0; i < debugZones.Length; i++)
            {
                float sx = 0, sy = 0;
                int count = 0;
                for (int gy2 = 0; gy2 < h; gy2++)
                {
                    for (int gx2 = 0; gx2 < w; gx2++)
                    {
                        if (grid[gy2 * w + gx2] == debugZones[i])
                        {
                            sx += gx2;
                            sy += gy2;
                            count++;
                        }
                    }
                }
                if (count == 0) continue;

                float cnx = (sx / count) / (w - 1);
                float cny = (sy / count) / (h - 1);
                float wx = (cny - 0.5f) * manifest.terrain.terrain_length_m;
                float wz = (cnx - 0.5f) * manifest.terrain.terrain_width_m;

                var oldSph = GameObject.Find($"DEBUG_{debugNames[i]}Centroid");
                if (oldSph != null) Object.DestroyImmediate(oldSph);

                var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sph.name = $"DEBUG_{debugNames[i]}Centroid";
                sph.transform.localScale = Vector3.one * debugSizes[i];
                float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
                sph.transform.position = new Vector3(wx, terrainBase + th + debugSizes[i], wz);
                var r = sph.GetComponent<Renderer>();
                var m = new Material(GetLitShader());
                m.color = debugColors[i];
                r.sharedMaterial = m;

                Debug.Log($"[TestZoneAlignment] {debugNames[i]} centroid: norm({cnx:F3}, {cny:F3}) → world({wx:F1}, {wz:F1}), {count}px");
            }
        }

        private static Shader GetLitShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                Debug.LogWarning("[HoleLiteImporter] Could not find Lit or Standard shader");
            return shader;
        }

        // ─── Bunker Pipeline ──────────────────────────────────────────────

        private static bool IsInsideContour(float px, float pz, Vector2[] contour)
        {
            bool inside = false;
            for (int i = 0, j = contour.Length - 1; i < contour.Length; j = i++)
            {
                if ((contour[i].y > pz) != (contour[j].y > pz) &&
                    px < (contour[j].x - contour[i].x) * (pz - contour[i].y)
                         / (contour[j].y - contour[i].y) + contour[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static void CreateZoneMeshes(TerrainData terrainData, GameObject terrainGO,
            Transform parentRoot, string exportPath, string dataDir, string projectRoot,
            bool[,] holes)
        {
            string bunkersPath = Path.Combine(exportPath, "bunkers.json");
            if (!File.Exists(bunkersPath))
            {
                Debug.Log("[HoleLiteImporter] No bunkers.json found, skipping");
                return;
            }

            string json = File.ReadAllText(bunkersPath);
            var bunkersFile = JsonUtility.FromJson<BunkersFileData>(json);

            if (bunkersFile.bunkers == null || bunkersFile.bunkers.Length == 0)
            {
                Debug.Log("[HoleLiteImporter] No bunkers in bunkers.json");
                return;
            }

            // Check for V2 contour data
            bool hasContours = !string.IsNullOrEmpty(bunkersFile.schema_version) &&
                               bunkersFile.bunkers[0].contour != null &&
                               bunkersFile.bunkers[0].contour.Length > 0;

            if (!hasContours)
            {
                Debug.LogWarning("[HoleLiteImporter] bunkers.json has no contour data " +
                                 "(V1 format). Re-export with updated export-hole.mjs. Skipping bunkers.");
                return;
            }

            float defaultDepth = bunkersFile.depth_m > 0 ? bunkersFile.depth_m : 2.0f;

            var sandMat = CreateBunkerMaterial(dataDir, projectRoot);

            var bunkersRoot = new GameObject("Bunkers");
            bunkersRoot.transform.SetParent(parentRoot);

            var terrain = terrainGO.GetComponent<Terrain>();
            float terrainBaseY = terrainGO.transform.position.y;
            Vector3 terrainPos = terrainGO.transform.position;
            Vector3 terrainSize = terrainData.size;

            int holesRes = terrainData.holesResolution;

            foreach (var bunker in bunkersFile.bunkers)
            {
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

                // Unified: 90% inward cut for all bunkers (2049 resolution gives ~0.3m/cell precision)
                float cutScale = 0.90f;
                var cutContour = new Vector2[worldContour.Length];
                for (int i = 0; i < worldContour.Length; i++)
                {
                    cutContour[i] = new Vector2(
                        centroidX + (worldContour[i].x - centroidX) * cutScale,
                        centroidZ + (worldContour[i].y - centroidZ) * cutScale);
                }

                int hMinX = Mathf.Clamp(Mathf.FloorToInt(
                    (cMinX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
                int hMaxX = Mathf.Clamp(Mathf.CeilToInt(
                    (cMaxX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
                int hMinZ = Mathf.Clamp(Mathf.FloorToInt(
                    (cMinZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);
                int hMaxZ = Mathf.Clamp(Mathf.CeilToInt(
                    (cMaxZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);

                for (int hz = hMinZ; hz <= hMaxZ; hz++)
                {
                    for (int hx = hMinX; hx <= hMaxX; hx++)
                    {
                        float cellWorldX = ((hx + 0.5f) / holesRes)
                            * terrainSize.x + terrainPos.x;
                        float cellWorldZ = ((hz + 0.5f) / holesRes)
                            * terrainSize.z + terrainPos.z;
                        if (IsInsideContour(cellWorldX, cellWorldZ, cutContour))
                            holes[hz, hx] = false;
                    }
                }

                // ── Bowl mesh ──
                float surfaceY = terrainBaseY + terrain.SampleHeight(
                    new Vector3(centroidX, 0, centroidZ));
                float shorterAxis = Mathf.Min(bunker.size_m.x, bunker.size_m.z);
                float bowlDepth = Mathf.Clamp(Mathf.Min(defaultDepth, shorterAxis * 0.2f), 0.5f, 3f);

                var meshGO = CreateContourMesh(bunker.id, worldContour,
                    centroidX, centroidZ, surfaceY, bowlDepth,
                    sandMat, terrain, terrainBaseY, false);
                meshGO.transform.SetParent(bunkersRoot.transform);

                var marker = meshGO.AddComponent<Golfin.Course.SurfaceMarker>();
                marker.surfaceType = Golfin.Course.SurfaceType.Bunker;

                Debug.Log($"[HoleLiteImporter] Bunker {bunker.id}: unified (cut=90%, depth={bowlDepth:F1}m, axis={shorterAxis:F1}m), {bunker.contour.Length} verts");
            }

            // Copy bunkers.json to Assets
            string destPath = Path.Combine(projectRoot, dataDir, "bunkers.json");
            File.Copy(bunkersPath, destPath, true);
            AssetDatabase.ImportAsset($"{dataDir}/bunkers.json");

            Debug.Log($"[HoleLiteImporter] Created {bunkersFile.bunkers.Length} contour-based bunker(s)");
        }

        private static GameObject CreateContourMesh(int id, Vector2[] contour,
            float centroidX, float centroidZ, float surfaceY, float depth,
            Material sandMat, Terrain terrain, float terrainBaseY,
            bool useSkirt = false)
        {
            int n = contour.Length;
            if (n < 3)
            {
                Debug.LogWarning($"[HoleLiteImporter] Bunker {id}: contour has < 3 vertices, skipping");
                return new GameObject($"Bunker_{id}_SKIP");
            }

            float[] ringScales;
            float[] ringDepths;
            if (useSkirt)
            {
                // Skirt(110%) → rim(100%) → inner(80%) → mid(50%) → deep(20%) → center
                ringScales = new float[] { 1.10f, 1.0f, 0.80f, 0.50f, 0.20f };
                ringDepths = new float[] { 0.0f, 0.0f, 0.0f, depth * 0.5f, depth * 0.9f };
            }
            else
            {
                // Original: rim(100%) → inner(80%) → mid(50%) → deep(20%) → center
                ringScales = new float[] { 1.0f, 0.80f, 0.50f, 0.20f };
                ringDepths = new float[] { 0.0f, 0.0f, depth * 0.5f, depth * 0.9f };
            }

            int ringCount = ringScales.Length;
            int vertCount = n * ringCount + 1; // +1 for center
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            // Compute bounding box for UV mapping
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in contour)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minZ) minZ = v.y;
                if (v.y > maxZ) maxZ = v.y;
            }
            float extentX = Mathf.Max(maxX - minX, 0.1f);
            float extentZ = Mathf.Max(maxZ - minZ, 0.1f);

            for (int r = 0; r < ringCount; r++)
            {
                float scale = ringScales[r];
                float ringY = -ringDepths[r];

                for (int i = 0; i < n; i++)
                {
                    // Scale toward centroid
                    float wx = centroidX + (contour[i].x - centroidX) * scale;
                    float wz = centroidZ + (contour[i].y - centroidZ) * scale;

                    float y = ringY;

                    int rimIdx = useSkirt ? 1 : 0;
                    int innerIdx = useSkirt ? 2 : 1;

                    if (useSkirt && r == 0)  // Skirt: below terrain — hides hole edge
                    {
                        float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                        y = (terrainBaseY + terrainH) - surfaceY - 0.15f;
                    }
                    else if (r == rimIdx)  // Rim: at terrain height + tiny offset
                    {
                        float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                        y = (terrainBaseY + terrainH) - surfaceY + 0.02f;
                    }
                    else if (r == innerIdx)  // Inner: at terrain height
                    {
                        float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                        y = (terrainBaseY + terrainH) - surfaceY;
                    }

                    // Local space relative to mesh origin (centroid at surface)
                    float localX = wx - centroidX;
                    float localZ = wz - centroidZ;

                    int vi = r * n + i;
                    vertices[vi] = new Vector3(localX, y, localZ);
                    uvs[vi] = new Vector2(
                        (wx - minX) / extentX,
                        (wz - minZ) / extentZ);
                }
            }

            // Center vertex — bottom of bowl
            int centerIdx = vertCount - 1;
            vertices[centerIdx] = new Vector3(0, -depth, 0);
            uvs[centerIdx] = new Vector2(0.5f, 0.5f);

            // --- Triangles ---
            int triCount = n * (ringCount - 1) * 6 + n * 3;
            var triangles = new int[triCount];
            int ti = 0;

            for (int r = 0; r < ringCount - 1; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int curr = r * n + i;
                    int next = r * n + (i + 1) % n;
                    int currInner = (r + 1) * n + i;
                    int nextInner = (r + 1) * n + (i + 1) % n;

                    triangles[ti++] = curr;
                    triangles[ti++] = currInner;
                    triangles[ti++] = next;

                    triangles[ti++] = next;
                    triangles[ti++] = currInner;
                    triangles[ti++] = nextInner;
                }
            }

            // Fan from last ring to center
            int lastRingStart = (ringCount - 1) * n;
            for (int i = 0; i < n; i++)
            {
                int curr = lastRingStart + i;
                int next = lastRingStart + (i + 1) % n;

                triangles[ti++] = curr;
                triangles[ti++] = centerIdx;
                triangles[ti++] = next;
            }

            // --- Build mesh ---
            var mesh = new Mesh();
            mesh.name = $"BunkerContour_{id}";
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"Bunker_{id}");
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sandMat;

            AddCleanMeshCollider(go, mesh);

            // Position: mesh origin at centroid, at terrain surface height
            go.transform.position = new Vector3(centroidX, surfaceY, centroidZ);

            Debug.Log($"[HoleLiteImporter] Bunker {id}: {n} contour verts, " +
                      $"{ringCount} rings, {mesh.vertexCount} total verts");

            return go;
        }

        private static Material CreateBunkerMaterial(string dataDir, string projectRoot)
        {
            string matPath = $"{dataDir}/BunkerSand.mat";

            // Check if material already exists
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
                AssetDatabase.DeleteAsset(matPath);

            var mat = new Material(GetLitShader());
            mat.name = "BunkerSand";

            // Try to use the existing bunker texture
            string texDir = "Assets/Courses/Textures_2025(JPG)";
            var bunkerTex = FindTextureExact(texDir, "T_Bunker_Albedo");
            if (bunkerTex != null)
            {
                mat.mainTexture = bunkerTex;
                mat.mainTextureScale = new Vector2(2f, 2f);  // tile within the bowl
            }
            else
            {
                // Fallback: plain sand color
                mat.color = new Color(0.87f, 0.80f, 0.53f); // sandy beige
            }

            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);

            // Render both sides so bowl interior is always visible
            mat.SetFloat("_Cull", 0f);  // 0 = Off (double-sided)

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static Material CreateZoneMaterial(string dataDir, string projectRoot,
            string matName, string albedoTexName, float tileScale)
        {
            string matPath = $"{dataDir}/{matName}.mat";

            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
                AssetDatabase.DeleteAsset(matPath);

            var mat = new Material(GetLitShader());
            mat.name = matName;

            string texDir = "Assets/Courses/Textures_2025(JPG)";
            var tex = FindTextureExact(texDir, albedoTexName);
            if (tex != null)
            {
                mat.mainTexture = tex;
                mat.mainTextureScale = new Vector2(tileScale, tileScale);
            }
            else
            {
                Debug.LogWarning($"[HoleLiteImporter] Missing texture: {albedoTexName}");
                mat.color = Color.green;
            }

            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Cull", 0f);  // double-sided

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        // ─── Green Pipeline ─────────────────────────────────────────────

        private static void CreateGreenMeshes(TerrainData terrainData, GameObject terrainGO,
            Transform parentRoot, string exportPath, string dataDir, string projectRoot,
            bool[,] holes)
        {
            string greensPath = Path.Combine(exportPath, "greens.json");
            if (!File.Exists(greensPath))
            {
                Debug.Log("[HoleLiteImporter] No greens.json found, skipping");
                return;
            }

            string json = File.ReadAllText(greensPath);
            var greensFile = JsonUtility.FromJson<GreensFileData>(json);

            if (greensFile.greens == null || greensFile.greens.Length == 0)
            {
                Debug.Log("[HoleLiteImporter] No greens in greens.json");
                return;
            }

            var greenMat = CreateZoneMaterial(dataDir, projectRoot,
                "GreenSurface", "T_Green_Albedo", 3f);
            var collarMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Courses/Materials (Shared by courses)/MAT_Fringe.mat");
            if (collarMat == null)
            {
                Debug.LogWarning("[HoleLiteImporter] MAT_Fringe.mat not found, falling back to T_Semirough_Albedo");
                collarMat = CreateZoneMaterial(dataDir, projectRoot, "GreenCollar", "T_Semirough_Albedo", 4f);
            }

            // Load fairway contours to detect greens inside fairways
            var fairwayPolys = new System.Collections.Generic.List<Vector2[]>();
            string fwDetectPath = Path.Combine(exportPath, "fairway-contours.json");
            if (File.Exists(fwDetectPath))
            {
                var fwData = JsonUtility.FromJson<FairwayContoursFile>(
                    File.ReadAllText(fwDetectPath));
                if (fwData.fairways != null)
                {
                    foreach (var fw in fwData.fairways)
                    {
                        if (fw.contour == null || fw.contour.Length < 3) continue;
                        var poly = new Vector2[fw.contour.Length];
                        for (int i = 0; i < fw.contour.Length; i++)
                            poly[i] = new Vector2(fw.contour[i].z, fw.contour[i].x);
                        fairwayPolys.Add(poly);
                    }
                }
            }

            var greensRoot = new GameObject("Greens");
            greensRoot.transform.SetParent(parentRoot);

            var terrain = terrainGO.GetComponent<Terrain>();
            float terrainBaseY = terrainGO.transform.position.y;
            Vector3 terrainPos = terrainGO.transform.position;
            Vector3 terrainSize = terrainData.size;

            int holesRes = terrainData.holesResolution;

            foreach (var green in greensFile.greens)
            {
                if (green.contour == null || green.contour.Length < 3) continue;

                // Apply 90° CCW rotation to contour vertices
                var worldContour = new Vector2[green.contour.Length];
                float sumX = 0, sumZ = 0;
                for (int i = 0; i < green.contour.Length; i++)
                {
                    float wx = green.contour[i].z;
                    float wz = green.contour[i].x;
                    worldContour[i] = new Vector2(wx, wz);
                    sumX += wx;
                    sumZ += wz;
                }
                float centroidX = sumX / worldContour.Length;
                float centroidZ = sumZ / worldContour.Length;

                // Bounding box should include the collar extent
                float greenCollarScale = 1.08f;
                float cMinX = float.MaxValue, cMaxX = float.MinValue;
                float cMinZ = float.MaxValue, cMaxZ = float.MinValue;
                foreach (var v in worldContour)
                {
                    float wx = centroidX + (v.x - centroidX) * greenCollarScale;
                    float wz = centroidZ + (v.y - centroidZ) * greenCollarScale;
                    if (wx < cMinX) cMinX = wx;
                    if (wx > cMaxX) cMaxX = wx;
                    if (wz < cMinZ) cMinZ = wz;
                    if (wz > cMaxZ) cMaxZ = wz;
                }

                // Cut contour at 95% of COLLAR scale
                var cutContour = new Vector2[worldContour.Length];
                for (int i = 0; i < worldContour.Length; i++)
                {
                    cutContour[i] = new Vector2(
                        centroidX + (worldContour[i].x - centroidX) * greenCollarScale * 0.95f,
                        centroidZ + (worldContour[i].y - centroidZ) * greenCollarScale * 0.95f);
                }

                int hMinX = Mathf.Clamp(Mathf.FloorToInt((cMinX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
                int hMaxX = Mathf.Clamp(Mathf.CeilToInt((cMaxX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
                int hMinZ = Mathf.Clamp(Mathf.FloorToInt((cMinZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);
                int hMaxZ = Mathf.Clamp(Mathf.CeilToInt((cMaxZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);

                for (int hz = hMinZ; hz <= hMaxZ; hz++)
                    for (int hx = hMinX; hx <= hMaxX; hx++)
                    {
                        float cellWorldX = ((hx + 0.5f) / holesRes) * terrainSize.x + terrainPos.x;
                        float cellWorldZ = ((hz + 0.5f) / holesRes) * terrainSize.z + terrainPos.z;
                        if (IsInsideContour(cellWorldX, cellWorldZ, cutContour))
                            holes[hz, hx] = false;
                    }

                // Detect if green is inside a fairway — boost Y so collar
                // sits above the fairway mesh surface
                float yBoost = 0f;
                foreach (var fwPoly in fairwayPolys)
                {
                    if (IsInsideContour(centroidX, centroidZ, fwPoly))
                    {
                        yBoost = 0.02f; // clear fairway's 0.01m offset
                        break;
                    }
                }

                // Build ContourPoint[] for CDT (Lite importer: 90° CCW rotation already in worldContour,
                // so invert: contour.z = worldContour.x (wx), contour.x = worldContour.y (wz))
                var contourPoints = new ContourPoint[worldContour.Length];
                for (int i = 0; i < worldContour.Length; i++)
                    contourPoints[i] = new ContourPoint { x = worldContour[i].y, z = worldContour[i].x };

                const float greenYOffset = 0.03f + GreenRaiseMeters; // terrain + collar base + raise
                const float greenCollarWidth = 0.6f;
                var meshGO = CreateGreenMeshCDT(green.id, contourPoints,
                    terrain, terrainBaseY, greenMat, collarMat,
                    greenCollarWidth, collarTileSize: 4f, greenTileSize: 3f, yBoost);
                if (meshGO != null)
                    meshGO.transform.SetParent(greensRoot.transform);

                // Place flag at green centroid
                var flagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/3D/Props/Flag/Flag.fbx");
                if (flagPrefab != null)
                {
                    var flag = Object.Instantiate(flagPrefab);
                    flag.name = $"Flag_{green.id}";
                    float flagTerrainH = terrain.SampleHeight(new Vector3(centroidX, 0, centroidZ));
                    float flagY = terrainBaseY + flagTerrainH + greenYOffset + yBoost;
                    flag.transform.position = new Vector3(centroidX, flagY, centroidZ);
                    flag.transform.SetParent(greensRoot.transform);

                    // Apply flag material to all renderers
                    var flagMat = AssetDatabase.LoadAssetAtPath<Material>(
                        "Assets/Art/3D/Props/Flag/MAT_Flag.mat");
                    if (flagMat != null)
                    {
                        foreach (var rend in flag.GetComponentsInChildren<Renderer>())
                            rend.sharedMaterial = flagMat;
                    }
                    // TODO: tune scale if flag appears too big or small
                }

                // Place hole cup at flag position (flat cylinder, regulation 4.25" = 0.108m diameter)
                {
                    var holeCup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    holeCup.name = $"Hole_{green.id}";
                    float cupTerrainH = terrain.SampleHeight(new Vector3(centroidX, 0, centroidZ));
                    float cupY = terrainBaseY + cupTerrainH + greenYOffset + yBoost;
                    holeCup.transform.position = new Vector3(centroidX, cupY + 0.001f, centroidZ);
                    holeCup.transform.localScale = new Vector3(0.108f, 0.001f, 0.108f);
                    holeCup.transform.SetParent(greensRoot.transform);

                    // Remove collider (visual only)
                    var col = holeCup.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    // Apply pure black material
                    var rend = holeCup.GetComponent<Renderer>();
                    if (rend != null)
                    {
                        var blackMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        blackMat.name = "MAT_HoleCup_Black";
                        blackMat.color = Color.black;
                        rend.sharedMaterial = blackMat;
                    }
                }
            }

            // Copy greens.json to Assets
            string destPath = Path.Combine(projectRoot, dataDir, "greens.json");
            File.Copy(greensPath, destPath, true);
            AssetDatabase.ImportAsset($"{dataDir}/greens.json");

            Debug.Log($"[HoleLiteImporter] Created {greensFile.greens.Length} green(s)");
        }


        /// <summary>
        /// Green mesh with collar baked in as a second submesh via dilated CDT.
        /// Submesh 0 = putting surface (inside original contour).
        /// Submesh 1 = collar / first cut (dilation ring, outside original contour).
        /// Single mesh, shared geometry — no Z-fighting on slopes.
        /// Falls back to collar-free mesh if dilation CDT fails.
        /// Lite importer: contour uses 90° CCW rotation (contour.z = wx, contour.x = wz).
        /// </summary>
        private static GameObject CreateGreenMeshCDT(
            int id, ContourPoint[] contour,
            Terrain terrain, float terrainBaseY,
            Material greenMat, Material collarMat,
            float collarWidth, float collarTileSize, float greenTileSize,
            float yBoost)
        {
            const float yOffset = 0.03f;
            int nc = contour.Length;
            if (nc < 3) return null;

            float effectiveYOffset = yOffset + yBoost;

            System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
                new Vector2(wx / greenTileSize, wz / greenTileSize);

            ContourPoint[] dilatedContour = DilateContour(contour, collarWidth);

            // Original contour as internal CDT constraint — forces triangle edges
            // exactly along it so the green/collar boundary has no jaggies.
            var (rawVerts, uvs, tris) = CDTTriangulate(
                dilatedContour, terrain, terrainBaseY, effectiveYOffset, 1.0f, uvFunc,
                innerConstraint: contour);

            bool collarEnabled = rawVerts != null && tris != null && tris.Length >= 3;
            if (!collarEnabled)
            {
                Debug.LogWarning($"[HoleLiteImporter] Green {id}: dilated CDT failed, retrying without collar");
                (rawVerts, uvs, tris) = CDTTriangulate(
                    contour, terrain, terrainBaseY, effectiveYOffset, 1.0f, uvFunc);
                if (rawVerts == null || tris == null || tris.Length < 3)
                    return null;
            }

            // Original polygon in world XZ for centroid classification
            // (Lite: 90° CCW rotation — contour.z = wx, contour.x = wz)
            var originalPoly = new Vector2[nc];
            for (int i = 0; i < nc; i++)
                originalPoly[i] = new Vector2(contour[i].z, contour[i].x);

            // Wind triangles CW (Unity default)
            if (tris.Length >= 3)
            {
                Vector3 a = rawVerts[tris[0]], b = rawVerts[tris[1]], c = rawVerts[tris[2]];
                float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
                if (cross > 0)
                    for (int t = 0; t < tris.Length; t += 3)
                    { int tmp = tris[t]; tris[t] = tris[t + 2]; tris[t + 2] = tmp; }
            }

            // Per-vert Y raise: green interior gets full GreenRaiseMeters; collar
            // ramps smoothly from full raise at the green boundary to zero at the
            // outer collar edge. Classify by vert position (not by submesh) so
            // boundary verts (d≈0) naturally compute full raise on both copies.
            for (int i = 0; i < rawVerts.Length; i++)
            {
                float d = Mathf.Sqrt(DistanceSqToContour(rawVerts[i].x, rawVerts[i].z, originalPoly));
                float raise;
                if (IsInsideContour(rawVerts[i].x, rawVerts[i].z, originalPoly))
                {
                    raise = GreenRaiseMeters;
                }
                else
                {
                    float t = 1f - Mathf.Clamp01(d / collarWidth);
                    t = t * t * (3f - 2f * t); // smoothstep
                    raise = GreenRaiseMeters * t;
                }
                rawVerts[i].y += raise;
            }

            // Classify each triangle by its centroid (always strictly inside or outside
            // the original contour since triangles have nonzero area).
            var greenTris = new List<int>();
            var collarSrcTris = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 va = rawVerts[tris[t]], vb = rawVerts[tris[t + 1]], vc = rawVerts[tris[t + 2]];
                float triCx = (va.x + vb.x + vc.x) / 3f;
                float triCz = (va.z + vb.z + vc.z) / 3f;
                bool inside = collarEnabled ? IsInsideContour(triCx, triCz, originalPoly) : true;
                if (inside)
                { greenTris.Add(tris[t]); greenTris.Add(tris[t + 1]); greenTris.Add(tris[t + 2]); }
                else
                { collarSrcTris.Add(tris[t]); collarSrcTris.Add(tris[t + 1]); collarSrcTris.Add(tris[t + 2]); }
            }

            // Duplicate boundary verts for collar with distance-based UVs.
            // U = 1 at green edge (light side of fringe mat faces green), U = 0 at outer edge.
            // V tiles along world XZ for perimeter variety.
            var finalVerts = new List<Vector3>(rawVerts);
            var finalUVs   = new List<Vector2>(uvs);
            var vertRemap  = new Dictionary<int, int>();
            var collarTris = new List<int>(collarSrcTris.Count);
            foreach (int origIdx in collarSrcTris)
            {
                if (!vertRemap.TryGetValue(origIdx, out int newIdx))
                {
                    Vector3 src = rawVerts[origIdx];
                    float dist = Mathf.Sqrt(DistanceSqToContour(src.x, src.z, originalPoly));
                    float u = 1f - Mathf.Clamp01(dist / collarWidth);
                    float v = (src.x + src.z) / collarTileSize;
                    newIdx = finalVerts.Count;
                    finalVerts.Add(src);
                    finalUVs.Add(new Vector2(u, v));
                    vertRemap[origIdx] = newIdx;
                }
                collarTris.Add(newIdx);
            }

            // Compute centroid for GO position
            float cx = 0f, cz = 0f;
            for (int i = 0; i < rawVerts.Length; i++) { cx += rawVerts[i].x; cz += rawVerts[i].z; }
            cx /= rawVerts.Length; cz /= rawVerts.Length;
            Vector3 centroid = new Vector3(cx, 0f, cz);

            var vertsArr = finalVerts.ToArray();
            for (int i = 0; i < vertsArr.Length; i++) vertsArr[i] -= centroid;

            var mesh = new Mesh();
            mesh.name = $"Green_{id}";
            mesh.vertices = vertsArr;
            mesh.uv = finalUVs.ToArray();
            mesh.subMeshCount = collarEnabled ? 2 : 1;
            mesh.SetTriangles(greenTris.ToArray(), 0);
            if (collarEnabled) mesh.SetTriangles(collarTris.ToArray(), 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"Green_{id}");
            go.transform.position = centroid;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = collarEnabled
                ? new Material[] { greenMat, collarMat }
                : new Material[] { greenMat };
            AddCleanMeshCollider(go, mesh);

            go.AddComponent<Golfin.Course.GreenSurfaceInfo>();
            var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
            marker.surfaceType = Golfin.Course.SurfaceType.Green;

            Debug.Log($"[HoleLiteImporter] Green {id}: CDT submesh, " +
                $"greenTris={greenTris.Count / 3}, " +
                $"collarTris={collarTris.Count / 3}, collarEnabled={collarEnabled}");
            return go;
        }

        private static GameObject CreateRaisedMesh(int id, string zoneName,
            Vector2[] contour, float centroidX, float centroidZ,
            float surfaceY, float height, Material surfaceMat,
            Terrain terrain, float terrainBaseY,
            Material collarMat = null, float collarScale = 1.08f,
            float yBoost = 0f)
        {
            int n = contour.Length;
            if (n < 3) return new GameObject($"{zoneName}_{id}_SKIP");

            // Parent object positioned at centroid (Y=0, vertices carry absolute Y)
            var parent = new GameObject($"{zoneName}_{id}");
            parent.transform.position = new Vector3(centroidX, 0, centroidZ);

            // UV bounding box (use collar scale for full extent)
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in contour)
            {
                float wx = centroidX + (v.x - centroidX) * collarScale;
                float wz = centroidZ + (v.y - centroidZ) * collarScale;
                if (wx < minX) minX = wx;
                if (wx > maxX) maxX = wx;
                if (wz < minZ) minZ = wz;
                if (wz > maxZ) maxZ = wz;
            }
            float extentX = Mathf.Max(maxX - minX, 0.1f);
            float extentZ = Mathf.Max(maxZ - minZ, 0.1f);

            // ── Collar mesh (semi-rough slope around the green) ──
            // Rings: outer rim (collarScale) → contour rim (1.0) → slope (0.97) → edge (0.93)
            // outer rim and contour rim at terrain height, slope at 30%, edge at 100%
            if (collarMat != null)
            {
                float[] collarScales = { collarScale, 1.0f, 0.97f, 0.93f };
                float[] collarHeightFracs = { -1f, -1f, 0.3f, 1.0f }; // -1 = terrain height
                int collarRings = collarScales.Length;
                int collarVertCount = n * collarRings;
                var collarVerts = new Vector3[collarVertCount];
                var collarUVs = new Vector2[collarVertCount];

                for (int r = 0; r < collarRings; r++)
                {
                    float scale = collarScales[r];
                    for (int i = 0; i < n; i++)
                    {
                        float wx = centroidX + (contour[i].x - centroidX) * scale;
                        float wz = centroidZ + (contour[i].y - centroidZ) * scale;
                        float localTerrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));

                        float y;
                        if (collarHeightFracs[r] < 0)
                        {
                            // Outer/contour rim: at terrain height + small offset
                            y = terrainBaseY + localTerrainH + 0.02f + yBoost;
                        }
                        else
                        {
                            // Slope/edge rings: terrain + fraction of green height
                            y = terrainBaseY + localTerrainH
                                + height * collarHeightFracs[r] + yBoost;
                        }

                        int vi = r * n + i;
                        collarVerts[vi] = new Vector3(wx - centroidX, y, wz - centroidZ);
                        collarUVs[vi] = new Vector2(
                            (wx - minX) / extentX,
                            (wz - minZ) / extentZ);
                    }
                }

                // Triangles: quads between adjacent rings (no center fan)
                int collarTriCount = n * (collarRings - 1) * 6;
                var collarTris = new int[collarTriCount];
                int cti = 0;
                for (int r = 0; r < collarRings - 1; r++)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int curr = r * n + i;
                        int next = r * n + (i + 1) % n;
                        int currInner = (r + 1) * n + i;
                        int nextInner = (r + 1) * n + (i + 1) % n;
                        collarTris[cti++] = curr;
                        collarTris[cti++] = currInner;
                        collarTris[cti++] = next;
                        collarTris[cti++] = next;
                        collarTris[cti++] = currInner;
                        collarTris[cti++] = nextInner;
                    }
                }

                var collarMesh = new Mesh();
                collarMesh.name = $"{zoneName}Collar_{id}";
                collarMesh.vertices = collarVerts;
                collarMesh.triangles = collarTris;
                collarMesh.uv = collarUVs;
                collarMesh.RecalculateNormals();
                collarMesh.RecalculateBounds();

                var collarGO = new GameObject($"{zoneName}_{id}_Collar");
                collarGO.AddComponent<MeshFilter>().sharedMesh = collarMesh;
                collarGO.AddComponent<MeshRenderer>().sharedMaterial = collarMat;
                AddCleanMeshCollider(collarGO, collarMesh);
                collarGO.transform.SetParent(parent.transform, false);

                // SurfaceMarker for collar = SemiRough
                var collarMarker = collarGO.AddComponent<Golfin.Course.SurfaceMarker>();
                collarMarker.surfaceType = Golfin.Course.SurfaceType.SemiRough;
            }

            // ── Putting surface mesh (flat top) ──
            // Rings: edge (0.93) → inner (0.80) → center
            // All at full green height
            float[] surfaceScales = { 0.93f, 0.80f };
            int surfaceRings = surfaceScales.Length;
            int surfaceVertCount = n * surfaceRings + 1;
            var surfaceVerts = new Vector3[surfaceVertCount];
            var surfaceUVs = new Vector2[surfaceVertCount];

            for (int r = 0; r < surfaceRings; r++)
            {
                float scale = surfaceScales[r];
                for (int i = 0; i < n; i++)
                {
                    float wx = centroidX + (contour[i].x - centroidX) * scale;
                    float wz = centroidZ + (contour[i].y - centroidZ) * scale;
                    float localTerrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));

                    int vi = r * n + i;
                    surfaceVerts[vi] = new Vector3(
                        wx - centroidX,
                        terrainBaseY + localTerrainH + height + yBoost,
                        wz - centroidZ);
                    surfaceUVs[vi] = new Vector2(
                        (wx - minX) / extentX,
                        (wz - minZ) / extentZ);
                }
            }

            int centerIdx = surfaceVertCount - 1;
            float centerTerrainH = terrain.SampleHeight(new Vector3(centroidX, 0, centroidZ));
            surfaceVerts[centerIdx] = new Vector3(0, terrainBaseY + centerTerrainH + height + yBoost, 0);
            surfaceUVs[centerIdx] = new Vector2(0.5f, 0.5f);

            int surfaceTriCount = n * (surfaceRings - 1) * 6 + n * 3;
            var surfaceTris = new int[surfaceTriCount];
            int sti = 0;

            for (int r = 0; r < surfaceRings - 1; r++)
            {
                for (int i = 0; i < n; i++)
                {
                    int curr = r * n + i;
                    int next = r * n + (i + 1) % n;
                    int currInner = (r + 1) * n + i;
                    int nextInner = (r + 1) * n + (i + 1) % n;
                    surfaceTris[sti++] = curr;
                    surfaceTris[sti++] = currInner;
                    surfaceTris[sti++] = next;
                    surfaceTris[sti++] = next;
                    surfaceTris[sti++] = currInner;
                    surfaceTris[sti++] = nextInner;
                }
            }

            int lastRing = (surfaceRings - 1) * n;
            for (int i = 0; i < n; i++)
            {
                surfaceTris[sti++] = lastRing + i;
                surfaceTris[sti++] = centerIdx;
                surfaceTris[sti++] = lastRing + (i + 1) % n;
            }

            var surfaceMesh = new Mesh();
            surfaceMesh.name = $"{zoneName}Surface_{id}";
            surfaceMesh.vertices = surfaceVerts;
            surfaceMesh.triangles = surfaceTris;
            surfaceMesh.uv = surfaceUVs;
            surfaceMesh.RecalculateNormals();
            surfaceMesh.RecalculateBounds();

            var surfaceGO = new GameObject($"{zoneName}_{id}_Surface");
            surfaceGO.AddComponent<MeshFilter>().sharedMesh = surfaceMesh;
            surfaceGO.AddComponent<MeshRenderer>().sharedMaterial = surfaceMat;
            AddCleanMeshCollider(surfaceGO, surfaceMesh);
            surfaceGO.transform.SetParent(parent.transform, false);

            Debug.Log($"[HoleLiteImporter] {zoneName} {id}: {n} contour verts, " +
                      $"collar={collarMat != null}, collarScale={collarScale:F2}");

            return parent;
        }

        // ─── Water Zone Meshes (Contour Mesh Overlay) ─────────────

        private static void CreateWaterMeshes(TerrainData terrainData, GameObject terrainGO,
            Transform parentRoot, string exportPath, string dataDir, string projectRoot,
            bool[,] holes)
        {
            string waterPath = Path.Combine(exportPath, "water.json");
            if (!File.Exists(waterPath))
            {
                Debug.Log("[HoleLiteImporter] No water.json found, skipping");
                return;
            }

            string json = File.ReadAllText(waterPath);
            var waterFile = JsonUtility.FromJson<WaterFileData>(json);

            if (waterFile.water == null || waterFile.water.Length == 0)
            {
                Debug.Log("[HoleLiteImporter] No water in water.json");
                return;
            }

            var waterRoot = new GameObject("Water");
            waterRoot.transform.SetParent(parentRoot);

            Terrain terrain = terrainGO.GetComponent<Terrain>();
            float terrainBaseY = terrainGO.transform.position.y;

            Debug.Log($"[Water] terrainBaseY={terrainBaseY:F2}, terrainSize.y={terrainData.size.y:F2}");
            Debug.Log($"[Water] ShoreDepthMeters={ShoreDepthMeters:F2}");

            // Create water material (solid color, high smoothness)
            var waterMat = CreateWaterMaterial(dataDir);

            foreach (var water in waterFile.water)
            {
                if (water.contour == null || water.contour.Length < 3) continue;

                int n = water.contour.Length;

                // 1. Flat water Y from min terrain height across contour
                float minTerrainH = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float wx = water.contour[i].z;  // 90° CCW
                    float wz = water.contour[i].x;
                    float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
                    if (th < minTerrainH) minTerrainH = th;
                }
                float waterY = terrainBaseY + minTerrainH - 0.05f;

                // 2. CDT triangulation (same helper as fairways)
                float tileSize = 10f;
                System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
                    new Vector2(wx / tileSize, wz / tileSize);

                var (rawVerts, uvs, tris) = CDTTriangulate(
                    water.contour, terrain, terrainBaseY, 0f, 2.0f, uvFunc);

                if (rawVerts == null || tris == null || tris.Length < 3)
                {
                    Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: CDT failed, falling back to ear-clip");

                    // Ear-clip fallback for shapes CDT can't handle
                    // (e.g. self-intersecting contours from aggressive RDP)
                    int nc = water.contour.Length;
                    var ecPts = new Vector3[nc];
                    for (int i = 0; i < nc; i++)
                    {
                        float wx2 = water.contour[i].z;  // 90° CCW
                        float wz2 = water.contour[i].x;
                        ecPts[i] = new Vector3(wx2, waterY, wz2);
                    }
                    tris = EarClipTriangulate(ecPts);
                    if (tris == null || tris.Length < 3)
                    {
                        Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: ear-clip also failed, skipping");
                        continue;
                    }
                    rawVerts = new Vector3[nc];
                    uvs = new Vector2[nc];
                    float tileSz = 10f;
                    for (int i = 0; i < nc; i++)
                    {
                        rawVerts[i] = ecPts[i];
                        uvs[i] = new Vector2(ecPts[i].x / tileSz, ecPts[i].z / tileSz);
                    }
                }

                // 3. Flatten all Y to waterY
                for (int i = 0; i < rawVerts.Length; i++)
                    rawVerts[i].y = waterY;

                // 4. Center mesh (Y=0 origin pattern)
                float cx = 0, cz = 0;
                for (int i = 0; i < rawVerts.Length; i++)
                { cx += rawVerts[i].x; cz += rawVerts[i].z; }
                cx /= rawVerts.Length; cz /= rawVerts.Length;
                Vector3 centroid = new Vector3(cx, 0, cz);

                for (int i = 0; i < rawVerts.Length; i++)
                    rawVerts[i] -= centroid;

                // 5. Check winding
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

                // 6. Build mesh + GameObject
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

                Debug.Log($"[HoleLiteImporter] Water {water.id}: {n} contour verts, " +
                          $"{tris.Length / 3} tris, waterY={waterY:F2}");
            }

            // Copy water.json to Assets
            string destPath = Path.Combine(projectRoot, dataDir, "water.json");
            File.Copy(waterPath, destPath, true);
            AssetDatabase.ImportAsset($"{dataDir}/water.json");

            Debug.Log($"[HoleLiteImporter] Created {waterFile.water.Length} water contour mesh(es)");
        }

        private static Material CreateWaterMaterial(string dataDir)
        {
            string matPath = $"{dataDir}/WaterSurface.mat";
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
                AssetDatabase.DeleteAsset(matPath);

            // Use the URPWater shader (already in project)
            var waterShader = Shader.Find("URPWater/Standard");
            if (waterShader == null)
            {
                Debug.LogWarning("[HoleLiteImporter] URPWater/Standard shader not found! " +
                                 "Falling back to URP/Lit. Check Assets/Art/3D/Props/URPWater/");
                var fallback = new Material(GetLitShader());
                fallback.name = "WaterSurface";
                fallback.color = new Color(0.18f, 0.40f, 0.58f);
                fallback.SetFloat("_Smoothness", 0.85f);
                AssetDatabase.CreateAsset(fallback, matPath);
                return fallback;
            }

            var mat = new Material(waterShader);
            mat.name = "WaterSurface";

            // ── Render queue: transparent (required by URPWater) ──
            mat.renderQueue = 3000;

            // ── Color mode: Colors (not gradient — simpler, cheaper) ──
            mat.SetFloat("_ColorMode", 0);
            mat.EnableKeyword("_COLORMODE_COLORS");

            // Shallow water color (teal-blue, typical golf pond)
            mat.SetColor("_Color", new Color(0.15f, 0.55f, 0.65f, 1f));
            // Deep water color (dark blue-green)
            mat.SetColor("_DepthColor", new Color(0.02f, 0.12f, 0.20f, 1f));
            // Underwater tint
            mat.SetColor("_UnderWaterColor", new Color(0.1f, 0.2f, 0.25f, 0.5f));

            // Depth range: short range so water becomes opaque quickly.
            // Prevents terrain splatmap underneath from showing through
            // as brownish patches. Shore depression is only ~0.1m.
            mat.SetFloat("_DepthStart", 0f);
            mat.SetFloat("_DepthEnd", 0.8f);

            // Refraction distortion: zero. Higher values warp the terrain
            // texture underneath through the water, producing ugly patches.
            mat.SetFloat("_Distortion", 0f);

            // Specular: low smoothness + dim spec color to avoid harsh sun glints.
            // The normal map creates per-pixel angle variation; high smoothness
            // turns that into visible bright/dark wave bands from the directional light.
            mat.SetFloat("_Smoothness", 0.4f);
            mat.SetColor("_SpecColor", new Color(0.4f, 0.4f, 0.4f, 1f));

            // ── Normal map: Single mode (cheapest, one scrolling normal) ──
            mat.SetFloat("_NormalsMode", 0);
            mat.EnableKeyword("_NORMALSMODE_SINGLE");

            // Use the water normal map included with the package
            var waterNormal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/Art/3D/Props/URPWater/Demo/Textures/Water/T_Water_03_N.tga");
            if (waterNormal != null)
            {
                mat.SetTexture("_NormalMapA", waterNormal);
                // Tiling: (tilingX, tilingY, offsetX, offsetY)
                mat.SetVector("_NormalMapATilings", new Vector4(2f, 2f, 0f, 0f));
                // Speed: (SpeedA_X, SpeedA_Y, SpeedB_X, SpeedB_Y)
                mat.SetVector("_NormalMapASpeeds", new Vector4(0.7f, 0.5f, 0f, 0f));
                mat.SetFloat("_NormalMapAIntensity", 0.15f);  // Very subtle — just enough
                // to break up the mirror-flat look without creating harsh specular
                // wave patterns from the directional light.

                // Normal map B: zero speed (single mode only uses A, but zero B to be safe)
                mat.SetVector("_NormalMapBSpeeds", new Vector4(0f, 0f, 0f, 0f));
            }
            else
            {
                Debug.LogWarning("[HoleLiteImporter] T_Water_03_N.tga not found! " +
                                 "Water will lack surface ripples.");
            }

            // ── Edge fade: ON (softens where water meets shore) ──
            mat.SetFloat("_EdgeFade", 1);
            mat.EnableKeyword("_EDGEFADE_ON");
            mat.SetFloat("_EdgeSize", 0.5f); // fade over 0.5m at edges

            // ── Foam: OFF (keep it simple for now) ──
            mat.SetFloat("_Foam", 0);

            // ── Caustics: OFF ──
            mat.SetFloat("_Caustics", 0);

            // ── Scattering: OFF ──
            mat.SetFloat("_Scattering", 0);

            // ── Reflections: Probes (uses URP reflection probes, low cost) ──
            mat.SetFloat("_ReflectionMode", 2); // 0=Off, 1=CubeMap, 2=Probes, 3=RealTime
            mat.EnableKeyword("_REFLECTIONMODE_PROBES");
            mat.SetFloat("_ReflectionFresnel", 5f);
            mat.SetFloat("_ReflectionFresnelNormal", 0.05f);  // Low normal influence on reflection angle
            mat.SetFloat("_ReflectionIntensity", 0.4f);  // Toned down
            mat.SetFloat("_ReflectionDistortion", 0.05f);  // Near-zero distortion
            mat.SetFloat("_ReflectionRoughness", 0.3f);  // Rougher = softer reflections

            // ── Waves: OFF (flat pond, no Gerstner displacement) ──
            mat.SetFloat("_DisplacementMode", 0);

            // ── World UV: ON (our mesh UVs are world-position-based) ──
            mat.SetFloat("_WorldUV", 1);
            mat.EnableKeyword("_WORLD_UV");

            AssetDatabase.CreateAsset(mat, matPath);

            // Check URP depth texture requirement
            var pipelineAsset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline
                as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (pipelineAsset != null)
            {
                if (!pipelineAsset.supportsCameraDepthTexture)
                    Debug.LogWarning("[HoleLiteImporter] URP Depth Texture is OFF! " +
                        "Water edge fade and depth coloring won't work. " +
                        "Enable it: Edit > Project Settings > Graphics > URP Asset > General > Depth Texture");
                if (!pipelineAsset.supportsCameraOpaqueTexture)
                    Debug.LogWarning("[HoleLiteImporter] URP Opaque Texture is OFF! " +
                        "Water refraction/distortion won't work. " +
                        "Enable it: Edit > Project Settings > Graphics > URP Asset > General > Opaque Texture");
            }

            return mat;
        }

        // ─── Flat Zone Mesh Pipeline (Fairway, Tee, Cart Path) ─────────

        private const float FairwayFringeMeters = 0.5f;

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
                                hRes, terrainPos, terrainSize,
                                FairwayFringeMeters);
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
                            MarkContourCells(region.contour, depress,
                                hRes, terrainPos, terrainSize);
            }

            // Cart path depression — separate array for gradient ramp
            // Wider polygon (full mesh width + 0.30m margin) so gradient's
            // 0% edge is outside the mesh, preventing z-fighting at mesh edges
            bool[,] cartDepress = new bool[hRes, hRes];
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
                            // Full mesh width + 0.30m margin beyond mesh edges
                            float halfWidth = (cp.width_m > 0
                                ? cp.width_m : 2.5f) / 2f + 0.30f;
                            var spinePoly = BuildSpinePolygon(
                                cp.spine, halfWidth);
                            if (spinePoly != null)
                                MarkWorldContourCells(spinePoly, cartDepress,
                                    hRes, terrainPos, terrainSize);
                        }
                        else if (cp.contour != null && cp.contour.Length >= 3)
                        {
                            MarkContourCells(cp.contour, cartDepress,
                                hRes, terrainPos, terrainSize);
                        }
                    }
                }
            }

            // Water is handled separately below — absolute Y assignment
            string waterDepressPath = Path.Combine(exportPath, "water.json");

            // Apply depression (fairway/tee — flat drop)
            int depressedCount = 0;
            for (int hz = 0; hz < hRes; hz++)
                for (int hx = 0; hx < hRes; hx++)
                    if (depress[hz, hx])
                    {
                        heights[hz, hx] = Mathf.Max(0f,
                            heights[hz, hx] - dropNormalized);
                        depressedCount++;
                    }

            // Cart path cells: distance-based gradual slope
            // Step 1: Distance transform on cartDepress (chamfer)
            float[,] cartDist = new float[hRes, hRes];
            for (int hz = 0; hz < hRes; hz++)
                for (int hx = 0; hx < hRes; hx++)
                    cartDist[hz, hx] = cartDepress[hz, hx] ? 0f : 99999f;

            // Forward pass
            for (int hz = 0; hz < hRes; hz++)
                for (int hx = 0; hx < hRes; hx++)
                {
                    if (hx > 0) cartDist[hz, hx] = Mathf.Min(
                        cartDist[hz, hx], cartDist[hz, hx - 1] + 1f);
                    if (hz > 0) cartDist[hz, hx] = Mathf.Min(
                        cartDist[hz, hx], cartDist[hz - 1, hx] + 1f);
                }
            // Backward pass
            for (int hz = hRes - 1; hz >= 0; hz--)
                for (int hx = hRes - 1; hx >= 0; hx--)
                {
                    if (hx < hRes - 1) cartDist[hz, hx] = Mathf.Min(
                        cartDist[hz, hx], cartDist[hz, hx + 1] + 1f);
                    if (hz < hRes - 1) cartDist[hz, hx] = Mathf.Min(
                        cartDist[hz, hx], cartDist[hz + 1, hx] + 1f);
                }

            // Step 2: Find max distance (= center of widest part)
            float maxCartDist = 0f;
            for (int hz = 0; hz < hRes; hz++)
                for (int hx = 0; hx < hRes; hx++)
                    if (cartDepress[hz, hx] && cartDist[hz, hx] > maxCartDist)
                        maxCartDist = cartDist[hz, hx];

            if (maxCartDist < 1f) maxCartDist = 1f;

            // Step 3: Apply smoothstep ramp — edge gets 0% drop, center gets 100%
            int cartDepressedCount = 0;
            for (int hz = 0; hz < hRes; hz++)
            {
                for (int hx = 0; hx < hRes; hx++)
                {
                    if (!cartDepress[hz, hx]) continue;

                    float t = cartDist[hz, hx] / maxCartDist;
                    t = Mathf.Clamp01(t);
                    t = t * t * (3f - 2f * t); // smoothstep

                    float cellDrop = dropNormalized * t;
                    heights[hz, hx] = Mathf.Max(0f,
                        heights[hz, hx] - cellDrop);
                    cartDepressedCount++;
                }
            }

            depressedCount += cartDepressedCount;

            // ─── Water bed (absolute Y) + shore slope ───────────────────
            if (File.Exists(waterDepressPath))
            {
                var waterDataShore = JsonUtility.FromJson<WaterFileData>(
                    File.ReadAllText(waterDepressPath));

                if (waterDataShore.water != null && waterDataShore.water.Length > 0)
                {
                    Terrain terrainForSample = terrainGO.GetComponent<Terrain>();
                    int bodyCount = waterDataShore.water.Length;

                    // Per-body waterYNorm (terrain-local normalized height of water surface)
                    // Mirrors CreateWaterMeshes: waterY_world = terrainBaseY + minTerrainH - 0.05
                    // → normalized = (minTerrainH - 0.05) / elevRange
                    float[] waterYNormPerBody = new float[bodyCount];
                    for (int b = 0; b < bodyCount; b++)
                    {
                        var w = waterDataShore.water[b];
                        if (w.contour == null || w.contour.Length < 3)
                        { waterYNormPerBody[b] = -1f; continue; }

                        float minTerrainH = float.MaxValue;
                        for (int i = 0; i < w.contour.Length; i++)
                        {
                            float wx = w.contour[i].z;
                            float wz = w.contour[i].x;
                            float th = terrainForSample.SampleHeight(new Vector3(wx, 0, wz));
                            if (th < minTerrainH) minTerrainH = th;
                        }
                        waterYNormPerBody[b] = (minTerrainH - 0.05f) / elevRange;
                    }

                    // Per-cell body index (-1 = not water)
                    int[,] waterBodyIdx = new int[hRes, hRes];
                    for (int z = 0; z < hRes; z++)
                        for (int x = 0; x < hRes; x++)
                            waterBodyIdx[z, x] = -1;

                    for (int b = 0; b < bodyCount; b++)
                    {
                        var w = waterDataShore.water[b];
                        if (w.contour == null || w.contour.Length < 3) continue;
                        bool[,] thisBody = new bool[hRes, hRes];
                        MarkContourCells(w.contour, thisBody,
                            hRes, terrainPos, terrainSize, 0f);
                        for (int z = 0; z < hRes; z++)
                            for (int x = 0; x < hRes; x++)
                                if (thisBody[z, x]) waterBodyIdx[z, x] = b;
                    }

                    // Chamfer distance transform with nearest-body propagation
                    float[,] distToWater = new float[hRes, hRes];
                    int[,] nearestBody = new int[hRes, hRes];
                    for (int z = 0; z < hRes; z++)
                        for (int x = 0; x < hRes; x++)
                        {
                            if (waterBodyIdx[z, x] >= 0)
                            { distToWater[z, x] = 0f; nearestBody[z, x] = waterBodyIdx[z, x]; }
                            else
                            { distToWater[z, x] = float.MaxValue; nearestBody[z, x] = -1; }
                        }

                    // Forward pass
                    for (int z = 0; z < hRes; z++)
                        for (int x = 0; x < hRes; x++)
                        {
                            float d = distToWater[z, x]; int nb = nearestBody[z, x];
                            if (x > 0 && distToWater[z, x - 1] + 1f < d)
                            { d = distToWater[z, x - 1] + 1f; nb = nearestBody[z, x - 1]; }
                            if (z > 0 && distToWater[z - 1, x] + 1f < d)
                            { d = distToWater[z - 1, x] + 1f; nb = nearestBody[z - 1, x]; }
                            if (x > 0 && z > 0 && distToWater[z - 1, x - 1] + 1.414f < d)
                            { d = distToWater[z - 1, x - 1] + 1.414f; nb = nearestBody[z - 1, x - 1]; }
                            if (x < hRes - 1 && z > 0 && distToWater[z - 1, x + 1] + 1.414f < d)
                            { d = distToWater[z - 1, x + 1] + 1.414f; nb = nearestBody[z - 1, x + 1]; }
                            distToWater[z, x] = d; nearestBody[z, x] = nb;
                        }

                    // Backward pass
                    for (int z = hRes - 1; z >= 0; z--)
                        for (int x = hRes - 1; x >= 0; x--)
                        {
                            float d = distToWater[z, x]; int nb = nearestBody[z, x];
                            if (x < hRes - 1 && distToWater[z, x + 1] + 1f < d)
                            { d = distToWater[z, x + 1] + 1f; nb = nearestBody[z, x + 1]; }
                            if (z < hRes - 1 && distToWater[z + 1, x] + 1f < d)
                            { d = distToWater[z + 1, x] + 1f; nb = nearestBody[z + 1, x]; }
                            if (x < hRes - 1 && z < hRes - 1 && distToWater[z + 1, x + 1] + 1.414f < d)
                            { d = distToWater[z + 1, x + 1] + 1.414f; nb = nearestBody[z + 1, x + 1]; }
                            if (x > 0 && z < hRes - 1 && distToWater[z + 1, x - 1] + 1.414f < d)
                            { d = distToWater[z + 1, x - 1] + 1.414f; nb = nearestBody[z + 1, x - 1]; }
                            distToWater[z, x] = d; nearestBody[z, x] = nb;
                        }

                    // Inner shore distance: for water cells, distance to NEAREST shore cell.
                    // This is the inverse of distToWater. Used to ramp water bed depth
                    // so bed is shallow (flush with water mesh) at contour edge, deep in interior.
                    float[,] distToShore = new float[hRes, hRes];
                    for (int z = 0; z < hRes; z++)
                        for (int x = 0; x < hRes; x++)
                            distToShore[z, x] = (waterBodyIdx[z, x] >= 0)
                                ? float.MaxValue : 0f;

                    // Forward pass (same chamfer as above but seeded from shore cells)
                    for (int z = 0; z < hRes; z++)
                        for (int x = 0; x < hRes; x++)
                        {
                            float d = distToShore[z, x];
                            if (x > 0) d = Mathf.Min(d, distToShore[z, x - 1] + 1f);
                            if (z > 0) d = Mathf.Min(d, distToShore[z - 1, x] + 1f);
                            if (x > 0 && z > 0)
                                d = Mathf.Min(d, distToShore[z - 1, x - 1] + 1.414f);
                            if (x < hRes - 1 && z > 0)
                                d = Mathf.Min(d, distToShore[z - 1, x + 1] + 1.414f);
                            distToShore[z, x] = d;
                        }
                    // Backward pass
                    for (int z = hRes - 1; z >= 0; z--)
                        for (int x = hRes - 1; x >= 0; x--)
                        {
                            float d = distToShore[z, x];
                            if (x < hRes - 1) d = Mathf.Min(d, distToShore[z, x + 1] + 1f);
                            if (z < hRes - 1) d = Mathf.Min(d, distToShore[z + 1, x] + 1f);
                            if (x < hRes - 1 && z < hRes - 1)
                                d = Mathf.Min(d, distToShore[z + 1, x + 1] + 1.414f);
                            if (x > 0 && z < hRes - 1)
                                d = Mathf.Min(d, distToShore[z + 1, x - 1] + 1.414f);
                            distToShore[z, x] = d;
                        }

                    // Apply: water bed ramped (shallow at edge → deep interior),
                    // shore cells ramped (flush at edge → origH at ShoreRadius).
                    float underwaterMaxNorm = 0.30f / elevRange; // max depth below water
                    float bedRampRadius = ShoreRadius; // same radius for symmetry
                    int waterCellCount = 0, shoreCount = 0;
                    for (int z = 0; z < hRes; z++)
                    {
                        for (int x = 0; x < hRes; x++)
                        {
                            int body = waterBodyIdx[z, x];
                            if (body >= 0 && waterYNormPerBody[body] >= 0f)
                            {
                                float waterH = waterYNormPerBody[body];
                                // Bed depth ramps from 0 (at shore) to underwaterMaxNorm (interior)
                                float dShore = distToShore[z, x];
                                float tb = Mathf.Clamp01(dShore / bedRampRadius);
                                tb = tb * tb * (3f - 2f * tb); // smoothstep
                                float depth = underwaterMaxNorm * tb;
                                heights[z, x] = Mathf.Max(0f, waterH - depth);
                                waterCellCount++;
                                continue;
                            }
                            if (depress[z, x]) continue;

                            int nb = nearestBody[z, x];
                            if (nb < 0 || waterYNormPerBody[nb] < 0f) continue;

                            float dist = distToWater[z, x];
                            if (dist > 0 && dist <= ShoreRadius)
                            {
                                float waterH = waterYNormPerBody[nb];
                                float origH = heights[z, x];
                                // t=0 at waterline (dist=1), t=1 at ShoreRadius
                                float t = (ShoreRadius > 1)
                                    ? (dist - 1f) / (ShoreRadius - 1f)
                                    : 1f;
                                t = Mathf.Clamp01(t);
                                t = t * t * (3f - 2f * t); // smoothstep
                                float targetH = Mathf.Lerp(waterH, origH, t);
                                heights[z, x] = Mathf.Min(origH, Mathf.Max(0f, targetH));
                                shoreCount++;
                            }
                        }
                    }
                    Debug.Log($"[HoleLiteImporter] Water bed: {waterCellCount} cells ramped " +
                              $"(edge flush, {underwaterMaxNorm * elevRange:F2}m deep interior), " +
                              $"shore: {shoreCount} cells ramped (radius={ShoreRadius})");
                }
            }

            terrainData.SetHeights(0, 0, heights);
            Debug.Log($"[HoleLiteImporter] Terrain depression: {depressedCount}" +
                      $" cells lowered by {OverlayDepressionMeters:F2}m" +
                      $" (cart path gradient: {cartDepressedCount} cells)");
        }

        /// <summary>
        /// Mark heightmap cells that fall inside a contour polygon.
        /// Contour uses local meter coords with 90° CCW rotation applied.
        /// </summary>
        private static void MarkContourCells(ContourPoint[] contour,
            bool[,] depress, int hRes, Vector3 terrainPos, Vector3 terrainSize,
            float inset = -1f)
        {
            if (inset < 0f) inset = DepressionInsetMeters;

            // Convert contour to world Vector3[] with 90° CCW rotation
            int n = contour.Length;
            var contour3D = new Vector3[n];
            for (int i = 0; i < n; i++)
                contour3D[i] = new Vector3(contour[i].z, 0, contour[i].x);

            // Edge-perpendicular inset using OffsetContourOutward with negative distance
            Vector3[] insetContour = OffsetContourOutward(contour3D, -inset);

            // Build Vector2[] for point-in-polygon test + compute bbox
            var worldContour = new Vector2[insetContour.Length];
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < insetContour.Length; i++)
            {
                float wx = insetContour[i].x;
                float wz = insetContour[i].z;
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

        /// <summary>
        /// Build a closed polygon from a spine centerline + half-width.
        /// Returns left edge forward + right edge reversed = closed loop.
        /// Same geometry as CreateSpineStripMesh uses.
        ///
        /// The spine is subdivided to maxSegmentLength spacing before computing
        /// edges. This produces a polygon that closely matches the smooth mesh
        /// quad strip at splatmap resolution, avoiding wavy/jagged edges.
        /// </summary>
        private static Vector2[] BuildSpinePolygon(
            ContourPoint[] spine, float halfWidth,
            float maxSegmentLength = 0.5f)
        {
            int n = spine.Length;
            if (n < 2) return null;

            // Subdivide spine so no segment exceeds maxSegmentLength.
            // This gives the polygon enough vertices to match the smooth mesh.
            var subdiv = new List<ContourPoint>();
            subdiv.Add(spine[0]);
            for (int i = 1; i < n; i++)
            {
                float dx = spine[i].x - spine[i - 1].x;
                float dz = spine[i].z - spine[i - 1].z;
                float segLen = Mathf.Sqrt(dx * dx + dz * dz);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / maxSegmentLength));
                for (int s = 1; s <= steps; s++)
                {
                    float t = (float)s / steps;
                    subdiv.Add(new ContourPoint
                    {
                        x = spine[i - 1].x + dx * t,
                        z = spine[i - 1].z + dz * t,
                    });
                }
            }

            int sn = subdiv.Count;
            var left = new Vector2[sn];
            var right = new Vector2[sn];

            for (int i = 0; i < sn; i++)
            {
                float cx = subdiv[i].z;  // 90° CCW
                float cz = subdiv[i].x;

                // Tangent
                float tx, tz;
                if (i == 0)
                { tx = subdiv[1].z - subdiv[0].z; tz = subdiv[1].x - subdiv[0].x; }
                else if (i == sn - 1)
                { tx = subdiv[sn-1].z - subdiv[sn-2].z; tz = subdiv[sn-1].x - subdiv[sn-2].x; }
                else
                { tx = subdiv[i+1].z - subdiv[i-1].z; tz = subdiv[i+1].x - subdiv[i-1].x; }

                float tLen = Mathf.Sqrt(tx * tx + tz * tz);
                if (tLen > 0.001f) { tx /= tLen; tz /= tLen; }
                else { tx = 1; tz = 0; }

                // Perpendicular (same as CreateSpineStripMesh)
                float px = tz;
                float pz = -tx;

                left[i]  = new Vector2(cx - px * halfWidth,
                                       cz - pz * halfWidth);
                right[i] = new Vector2(cx + px * halfWidth,
                                       cz + pz * halfWidth);
            }

            // Closed polygon: left forward, then right reversed
            var poly = new Vector2[sn * 2];
            for (int i = 0; i < sn; i++)
                poly[i] = left[i];
            for (int i = 0; i < sn; i++)
                poly[sn + i] = right[sn - 1 - i];

            return poly;
        }

        /// <summary>
        /// Mark heightmap cells inside a world-space Vector2[] polygon.
        /// No contour conversion or inset — polygon is already in
        /// world XZ coords at the desired boundary.
        /// </summary>
        private static void MarkWorldContourCells(Vector2[] worldContour,
            bool[,] depress, int hRes, Vector3 terrainPos, Vector3 terrainSize)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in worldContour)
            {
                if (v.x < minX) minX = v.x;
                if (v.x > maxX) maxX = v.x;
                if (v.y < minZ) minZ = v.y;
                if (v.y > maxZ) maxZ = v.y;
            }

            int hMinX = Mathf.Clamp(Mathf.FloorToInt(
                (minX - terrainPos.x) / terrainSize.x * (hRes - 1)), 0, hRes - 1);
            int hMaxX = Mathf.Clamp(Mathf.CeilToInt(
                (maxX - terrainPos.x) / terrainSize.x * (hRes - 1)), 0, hRes - 1);
            int hMinZ = Mathf.Clamp(Mathf.FloorToInt(
                (minZ - terrainPos.z) / terrainSize.z * (hRes - 1)), 0, hRes - 1);
            int hMaxZ = Mathf.Clamp(Mathf.CeilToInt(
                (maxZ - terrainPos.z) / terrainSize.z * (hRes - 1)), 0, hRes - 1);

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

        private static void CreateFlatZoneMeshes(TerrainData terrainData,
            GameObject terrainGO, Transform parentRoot,
            string exportPath, string dataDir, string projectRoot)
        {
            string texDir = "Assets/Courses/Textures_2025(JPG)";
            var terrain = terrainGO.GetComponent<Terrain>();
            float terrainBaseY = terrainGO.transform.position.y;

            // ─── Fairway meshes ─────────────────────────────
            string fwPath = Path.Combine(exportPath, "fairway-contours.json");
            if (File.Exists(fwPath))
            {
                string json = File.ReadAllText(fwPath);
                var data = JsonUtility.FromJson<FairwayContoursFile>(json);

                if (data.fairways != null && data.fairways.Length > 0)
                {
                    var fwRoot = new GameObject("Fairways");
                    fwRoot.transform.SetParent(parentRoot);

                    // ── Compute stripe direction (perpendicular to tee→green) ──
                    Vector2 stripeDir = new Vector2(0, 1); // default fallback
                    string anchorsPath = Path.Combine(exportPath, "anchors.json");
                    if (File.Exists(anchorsPath))
                    {
                        string anchJson = File.ReadAllText(anchorsPath);
                        var anchWrap = JsonUtility.FromJson<AnchorArrayWrapper>(
                            "{\"items\":" + anchJson + "}");
                        var anchs = anchWrap.items;
                        var backTee = System.Array.Find(anchs, a => a.type.Contains("back"));

                        string grPath = Path.Combine(exportPath, "greens.json");
                        AnchorLocal greenCenter = null;
                        if (File.Exists(grPath))
                        {
                            var grFile = JsonUtility.FromJson<GreensFileData>(File.ReadAllText(grPath));
                            if (grFile.greens != null && grFile.greens.Length > 0)
                                greenCenter = grFile.greens[0].center_local;
                        }

                        if (backTee != null && greenCenter != null)
                        {
                            // Apply same 90° CCW rotation as contour points: worldX = z, worldZ = x
                            Vector2 teePos = new Vector2(backTee.local.z, backTee.local.x);
                            Vector2 greenPos = new Vector2(greenCenter.z, greenCenter.x);
                            Vector2 dir = (greenPos - teePos).normalized;
                            if (dir.sqrMagnitude > 0.01f)
                                stripeDir = new Vector2(-dir.y, dir.x); // perpendicular
                        }
                    }

                    // ── Materials ──
                    var fairwayMat = CreateTiledMaterial(texDir, "T_Fairway_Mix",
                        "T_Fairway_Normal", dataDir, 5f);
                    var fringeMat = CreateTiledMaterial(texDir, "T_Semirough_Albedo",
                        "T_Semirough_Normal", dataDir, 6f);

                    float stripeWidth = 5f;

                    foreach (var fw in data.fairways)
                    {
                        if (fw.contour == null || fw.contour.Length < 3) continue;

                        // Fairway mesh with fringe baked in as a second submesh.
                        // CDT runs on a DILATED contour; triangles outside the original
                        // contour are the fringe band. Single mesh, no overlap, no Z-fight.
                        var meshGO = CreateFairwayMesh(
                            fw.id, fw.contour, terrain, terrainBaseY,
                            fairwayMat, fringeMat, FairwayFringeMeters, 6f,
                            stripeDir, stripeWidth);
                        if (meshGO != null)
                            meshGO.transform.SetParent(fwRoot.transform);
                    }

                    Debug.Log($"[HoleLiteImporter] Created {data.fairways.Length} fairway mesh(es) with mow stripes + fringe");
                }
            }

            // ─── Tee & Cart Path meshes from zone-contours.json ─────
            string zcPath = Path.Combine(exportPath, "zone-contours.json");
            if (File.Exists(zcPath))
            {
                string json = File.ReadAllText(zcPath);
                var data = JsonUtility.FromJson<ZoneContoursFile>(json);

                // Tee meshes
                if (data.zones != null && data.zones.tee != null && data.zones.tee.Length > 0)
                {
                    var teeRoot = new GameObject("Tees");
                    teeRoot.transform.SetParent(parentRoot);

                    var teeMat = CreateTiledMaterial(texDir, "T_Tee_Albedo",
                        "T_Tee_Normal", dataDir, 3f);

                    // Tee border material (gradient texture: light inside, dark outside)
                    var teeBorderMat = new Material(GetLitShader());
                    teeBorderMat.name = "MAT_TeeBorder";
                    teeBorderMat.mainTexture = FindTextureExact(texDir, "T_TeeDark_Albedo");
                    var teeBorderNormal = FindTextureExact(texDir, "T_TeeDark_Normal");
                    if (teeBorderNormal != null)
                    {
                        teeBorderMat.SetTexture("_BumpMap", teeBorderNormal);
                        teeBorderMat.SetFloat("_BumpScale", 0.4f);
                        teeBorderMat.EnableKeyword("_NORMALMAP");
                    }
                    teeBorderMat.SetFloat("_Smoothness", 0f);
                    teeBorderMat.SetFloat("_Metallic", 0f);

                    string teeBorderMatPath = $"{dataDir}/MAT_TeeBorder.mat";
                    var existingBorderMat = AssetDatabase.LoadAssetAtPath<Material>(teeBorderMatPath);
                    if (existingBorderMat != null) AssetDatabase.DeleteAsset(teeBorderMatPath);
                    AssetDatabase.CreateAsset(teeBorderMat, teeBorderMatPath);

                    foreach (var region in data.zones.tee)
                    {
                        if (region.contour == null || region.contour.Length < 3) continue;

                        // Tee mesh with border baked in as a second submesh (dilated CDT).
                        var meshGO = CreateTeeMeshWithBorder(
                            region.id, "Tee", region.contour,
                            terrain, terrainBaseY,
                            teeMat, 3f,
                            teeBorderMat, 0.5f, 3f,
                            Golfin.Course.SurfaceType.Tee);
                        if (meshGO != null)
                            meshGO.transform.SetParent(teeRoot.transform);
                    }

                    Debug.Log($"[HoleLiteImporter] Created {data.zones.tee.Length} tee mesh(es) with border rings");
                }

            }

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
                    cpMat.SetFloat("_Cull", 0f);  // 0 = Off (double-sided rendering)

                    foreach (var region in cpData.cart_paths)
                    {
                        GameObject meshGO = null;

                        // Prefer spine strip mesh if available
                        if (region.spine != null && region.spine.Length >= 2)
                        {
                            float halfWidth = (region.width_m > 0 ? region.width_m : 2.5f) / 2f;
                            meshGO = CreateSpineStripMesh(
                                region.id, region.spine, halfWidth,
                                terrain, terrainBaseY, cpMat, 4f,
                                Golfin.Course.SurfaceType.CartPath);
                        }
                        else if (region.contour != null && region.contour.Length >= 3)
                        {
                            // Fallback to ear-clip
                            meshGO = CreateEarClipContourMesh(
                                region.id, "CartPath", region.contour,
                                terrain, terrainBaseY, cpMat, 4f,
                                Golfin.Course.SurfaceType.CartPath);
                        }

                        if (meshGO != null)
                            meshGO.transform.SetParent(cpRoot.transform);
                    }

                    Debug.Log($"[HoleLiteImporter] Created {cpData.cart_paths.Length} cart path mesh(es)");
                }
            }

            // Copy cart-paths.json to Assets
            string cpSrcPath = Path.Combine(exportPath, "cart-paths.json");
            if (File.Exists(cpSrcPath))
            {
                string cpDestPath = Path.Combine(projectRoot, dataDir, "cart-paths.json");
                File.Copy(cpSrcPath, cpDestPath, true);
                AssetDatabase.ImportAsset($"{dataDir}/cart-paths.json");
            }
        }

        // ─── CDT Triangulation ────────────────────────────────────────

        /// <summary>
        /// Constrained Delaunay Triangulation with interior Steiner points
        /// for terrain conformance. Returns world-space verts, UVs, and tris.
        /// </summary>
        private static (Vector3[] verts, Vector2[] uvs, int[] tris)
            CDTTriangulate(
                ContourPoint[] contour,
                Terrain terrain, float terrainBaseY, float yOffset,
                float gridSpacing,
                System.Func<float, float, Vector2> uvFunc,
                ContourPoint[] innerConstraint = null)
        {
            int n = contour.Length;
            if (n < 3) return (null, null, null);

            // 1. Boundary vertices (2D XZ plane after 90° CCW rotation)
            var positions2D = new System.Collections.Generic.List<double2>();
            for (int i = 0; i < n; i++)
            {
                float wx = contour[i].z; // 90° CCW
                float wz = contour[i].x;
                positions2D.Add(new double2(wx, wz));
            }

            // 2. Constraint edges (closed polygon)
            var constraintEdges = new System.Collections.Generic.List<int>();
            for (int i = 0; i < n; i++)
            {
                constraintEdges.Add(i);
                constraintEdges.Add((i + 1) % n);
            }

            // 2b. Optional internal constraint contour — forces triangle edges
            // exactly along this loop so submesh boundaries have no jaggies.
            int innerStart = positions2D.Count;
            int innerCount = 0;
            if (innerConstraint != null && innerConstraint.Length >= 3)
            {
                innerCount = innerConstraint.Length;
                for (int i = 0; i < innerCount; i++)
                {
                    float iwx = innerConstraint[i].z; // 90° CCW, same as outer
                    float iwz = innerConstraint[i].x;
                    positions2D.Add(new double2(iwx, iwz));
                }
                for (int i = 0; i < innerCount; i++)
                {
                    constraintEdges.Add(innerStart + i);
                    constraintEdges.Add(innerStart + ((i + 1) % innerCount));
                }
            }

            // 3. Interior Steiner points on a grid for terrain conformance
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var pt in contour)
            {
                float wx = pt.z; float wz = pt.x;
                if (wx < minX) minX = wx; if (wx > maxX) maxX = wx;
                if (wz < minZ) minZ = wz; if (wz > maxZ) maxZ = wz;
            }

            var poly2D = new Vector2[n];
            for (int i = 0; i < n; i++)
                poly2D[i] = new Vector2(contour[i].z, contour[i].x);

            for (float gx = minX + gridSpacing; gx < maxX; gx += gridSpacing)
            {
                for (float gz = minZ + gridSpacing; gz < maxZ; gz += gridSpacing)
                {
                    if (IsInsideContour(gx, gz, poly2D))
                        positions2D.Add(new double2(gx, gz));
                }
            }

            // 4. Run CDT
            using var inputPositions = new NativeArray<double2>(
                positions2D.ToArray(), Allocator.TempJob);
            using var inputConstraints = new NativeArray<int>(
                constraintEdges.ToArray(), Allocator.TempJob);

            using var triangulator = new Triangulator(Allocator.TempJob)
            {
                Settings =
                {
                    RestoreBoundary = true,
                },
                Input =
                {
                    Positions = inputPositions,
                    ConstraintEdges = inputConstraints,
                }
            };

            triangulator.Run();

            var outputTriangles = triangulator.Output.Triangles;
            var outputPositions = triangulator.Output.Positions;

            if (outputTriangles.Length < 3) return (null, null, null);

            // 5. Build Unity mesh arrays
            int vertCount = outputPositions.Length;
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];

            for (int i = 0; i < vertCount; i++)
            {
                float wx = (float)outputPositions[i].x;
                float wz = (float)outputPositions[i].y; // y in 2D = z in 3D
                float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
                verts[i] = new Vector3(wx, terrainBaseY + th + yOffset, wz);
                uvs[i] = uvFunc(wx, wz);
            }

            var tris = new int[outputTriangles.Length];
            for (int i = 0; i < outputTriangles.Length; i++)
                tris[i] = outputTriangles[i];

            return (verts, uvs, tris);
        }

        // ─── Overlay Mesh Methods ─────────────────────────────────────

        private static GameObject CreateFlatContourMesh(int id, string zoneName,
            ContourPoint[] contour, Terrain terrain, float terrainBaseY,
            Material mat, float tileSize, Golfin.Course.SurfaceType surfaceType)
        {
            float yOffset = 0.02f; // raised 0.01 above terrain

            System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
                new Vector2(wx / tileSize, wz / tileSize);

            var (rawVerts, uvs, tris) = CDTTriangulate(
                contour, terrain, terrainBaseY, yOffset, 1.0f, uvFunc);

            if (rawVerts == null || tris == null || tris.Length < 3)
            {
                Debug.LogWarning($"[HoleLiteImporter] {zoneName} {id}: CDT failed");
                return null;
            }

            // Center mesh (Y=0 origin pattern)
            float cx = 0, cz = 0;
            for (int i = 0; i < rawVerts.Length; i++)
            { cx += rawVerts[i].x; cz += rawVerts[i].z; }
            cx /= rawVerts.Length; cz /= rawVerts.Length;
            Vector3 centroid = new Vector3(cx, 0, cz);

            for (int i = 0; i < rawVerts.Length; i++)
                rawVerts[i] -= centroid;

            // Check winding
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

            var mesh = new Mesh();
            mesh.name = $"{zoneName}_{id}";
            mesh.vertices = rawVerts;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"{zoneName}_{id}");
            go.transform.position = centroid;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            AddCleanMeshCollider(go, mesh);

            var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
            marker.surfaceType = surfaceType;
            return go;
        }

        private static GameObject CreateEarClipContourMesh(int id, string zoneName,
            ContourPoint[] contour, Terrain terrain, float terrainBaseY,
            Material mat, float tileSize, Golfin.Course.SurfaceType surfaceType)
        {
            // Now uses CDT instead of ear-clip
            return CreateFlatContourMesh(id, zoneName, contour,
                terrain, terrainBaseY, mat, tileSize, surfaceType);
        }

        /// <summary>
        /// Squared perpendicular distance from (px,pz) to any edge segment
        /// of the closed polygon poly (poly.x=X, poly.y=Z).
        /// </summary>
        private static float DistanceSqToContour(float px, float pz, Vector2[] poly)
        {
            float best = float.MaxValue;
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                float dx = b.x - a.x, dz = b.y - a.y;
                float lenSq = dx * dx + dz * dz;
                float ex = px - a.x, ez = pz - a.y;
                float dSq;
                if (lenSq < 1e-12f) { dSq = ex * ex + ez * ez; }
                else
                {
                    float t = (ex * dx + ez * dz) / lenSq;
                    if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
                    float cxs = a.x + t * dx, czs = a.y + t * dz;
                    float fx = px - cxs, fz = pz - czs;
                    dSq = fx * fx + fz * fz;
                }
                if (dSq < best) best = dSq;
            }
            return best;
        }

        /// <summary>
        /// Dilate a ContourPoint[] (assumed CCW in export space) outward by `offset` meters.
        /// Reuses OffsetContourOutward which works in world XZ (after the 90° CCW rotation
        /// that CDT applies).
        /// </summary>
        private static ContourPoint[] DilateContour(ContourPoint[] contour, float offset)
        {
            int n = contour.Length;
            var worldXZ = new Vector3[n];
            for (int i = 0; i < n; i++)
                worldXZ[i] = new Vector3(contour[i].z, 0, contour[i].x); // 90° CCW
            Vector3[] dilated = OffsetContourOutward(worldXZ, offset);
            var result = new ContourPoint[n];
            for (int i = 0; i < n; i++)
                result[i] = new ContourPoint { x = dilated[i].z, z = dilated[i].x }; // inverse
            return result;
        }

        /// <summary>
        /// Creates the fairway mesh with an inner fringe band baked in as a second
        /// submesh. Approach: CDT runs on a contour DILATED outward by fringeWidth;
        /// vertices are classified by point-in-polygon against the ORIGINAL contour.
        /// Triangles with all 3 verts inside original → fairway submesh (mow stripe UVs).
        /// Otherwise → fringe submesh (tile UVs via duplicated verts).
        /// Single mesh, shared geometry — no Z-fighting is possible.
        /// </summary>
        private static GameObject CreateFairwayMesh(int id, ContourPoint[] contour,
            Terrain terrain, float terrainBaseY,
            Material mat, Material fringeMat, float fringeWidth, float fringeTileSize,
            Vector2 stripeDir, float stripeWidth)
        {
            int nc = contour.Length;
            if (nc < 3) return null;

            float yOffset = 0.015f; // slightly higher to avoid terrain eating fairway edges
            Vector2 parallelDir = new Vector2(-stripeDir.y, stripeDir.x);

            System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
                new Vector2(
                    (wx * stripeDir.x + wz * stripeDir.y) / stripeWidth,
                    (wx * parallelDir.x + wz * parallelDir.y) / stripeWidth);

            ContourPoint[] dilatedContour = DilateContour(contour, fringeWidth);

            // Original contour as internal CDT constraint → triangle edges land
            // exactly on it, eliminating jaggies at the fairway/fringe boundary.
            var (rawVerts, uvs, tris) = CDTTriangulate(
                dilatedContour, terrain, terrainBaseY, yOffset, 1.0f, uvFunc,
                innerConstraint: contour);

            bool fringeEnabled = rawVerts != null && tris != null && tris.Length >= 3;
            if (!fringeEnabled)
            {
                (rawVerts, uvs, tris) = CDTTriangulate(
                    contour, terrain, terrainBaseY, yOffset, 1.0f, uvFunc);
                if (rawVerts == null || tris == null || tris.Length < 3)
                    return null;
            }

            // Original polygon for classification (Lite uses 90° CCW rotation)
            var originalPoly = new Vector2[nc];
            for (int i = 0; i < nc; i++)
                originalPoly[i] = new Vector2(contour[i].z, contour[i].x);

            // Center mesh
            float cx = 0, cz = 0;
            for (int i = 0; i < rawVerts.Length; i++)
            { cx += rawVerts[i].x; cz += rawVerts[i].z; }
            cx /= rawVerts.Length; cz /= rawVerts.Length;
            Vector3 centroid = new Vector3(cx, 0, cz);

            // Winding check (world-space, before centroid subtraction)
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

            // Classify by triangle centroid (always strictly interior or exterior —
            // never on the boundary, so ray-cast is reliable).
            var fairwayTris = new System.Collections.Generic.List<int>();
            var fringeSrcTris = new System.Collections.Generic.List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 va = rawVerts[tris[t]];
                Vector3 vb = rawVerts[tris[t + 1]];
                Vector3 vc = rawVerts[tris[t + 2]];
                float triCx = (va.x + vb.x + vc.x) / 3f;
                float triCz = (va.z + vb.z + vc.z) / 3f;
                bool triInsideOriginal = fringeEnabled
                    ? IsInsideContour(triCx, triCz, originalPoly)
                    : true;
                if (triInsideOriginal)
                { fairwayTris.Add(tris[t]); fairwayTris.Add(tris[t + 1]); fairwayTris.Add(tris[t + 2]); }
                else
                { fringeSrcTris.Add(tris[t]); fringeSrcTris.Add(tris[t + 1]); fringeSrcTris.Add(tris[t + 2]); }
            }

            // Duplicate fringe-referenced verts with fringe (tile) UVs so the
            // semirough texture tiles correctly without affecting mow stripes.
            var finalVerts = new System.Collections.Generic.List<Vector3>(rawVerts);
            var finalUVs = new System.Collections.Generic.List<Vector2>(uvs);
            var vertRemap = new System.Collections.Generic.Dictionary<int, int>();
            var fringeTris = new System.Collections.Generic.List<int>(fringeSrcTris.Count);
            foreach (int origIdx in fringeSrcTris)
            {
                if (!vertRemap.TryGetValue(origIdx, out int newIdx))
                {
                    Vector3 src = rawVerts[origIdx];
                    newIdx = finalVerts.Count;
                    finalVerts.Add(src);
                    finalUVs.Add(new Vector2(src.x / fringeTileSize, src.z / fringeTileSize));
                    vertRemap[origIdx] = newIdx;
                }
                fringeTris.Add(newIdx);
            }

            // Subtract centroid (positions only)
            var vertsArr = finalVerts.ToArray();
            for (int i = 0; i < vertsArr.Length; i++)
                vertsArr[i] -= centroid;

            var mesh = new Mesh();
            mesh.name = $"Fairway_{id}";
            mesh.vertices = vertsArr;
            mesh.uv = finalUVs.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(fairwayTris.ToArray(), 0);
            mesh.SetTriangles(fringeTris.ToArray(), 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"Fairway_{id}");
            go.transform.position = centroid;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[] { mat, fringeMat };
            AddCleanMeshCollider(go, mesh);

            var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
            marker.surfaceType = Golfin.Course.SurfaceType.Fairway;
            return go;
        }

        /// <summary>
        /// Creates a tee mesh with an outer border band baked in as a second submesh.
        /// Same dilated-CDT approach as CreateFairwayMesh.
        /// </summary>
        private static GameObject CreateTeeMeshWithBorder(int id, string zoneName,
            ContourPoint[] contour, Terrain terrain, float terrainBaseY,
            Material mat, float tileSize,
            Material borderMat, float borderWidth, float borderTileSize,
            Golfin.Course.SurfaceType surfaceType)
        {
            int nc = contour.Length;
            if (nc < 3) return null;

            float yOffset = 0.02f;

            System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
                new Vector2(wx / tileSize, wz / tileSize);

            ContourPoint[] dilatedContour = DilateContour(contour, borderWidth);

            var (rawVerts, uvs, tris) = CDTTriangulate(
                dilatedContour, terrain, terrainBaseY, yOffset, 1.0f, uvFunc,
                innerConstraint: contour);

            bool borderEnabled = rawVerts != null && tris != null && tris.Length >= 3;
            if (!borderEnabled)
            {
                (rawVerts, uvs, tris) = CDTTriangulate(
                    contour, terrain, terrainBaseY, yOffset, 1.0f, uvFunc);
                if (rawVerts == null || tris == null || tris.Length < 3)
                    return null;
            }

            var originalPoly = new Vector2[nc];
            for (int i = 0; i < nc; i++)
                originalPoly[i] = new Vector2(contour[i].z, contour[i].x);

            float cx = 0, cz = 0;
            for (int i = 0; i < rawVerts.Length; i++)
            { cx += rawVerts[i].x; cz += rawVerts[i].z; }
            cx /= rawVerts.Length; cz /= rawVerts.Length;
            Vector3 centroid = new Vector3(cx, 0, cz);

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

            // Triangle classification by centroid.
            var teeTris = new System.Collections.Generic.List<int>();
            var borderSrcTris = new System.Collections.Generic.List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 va = rawVerts[tris[t]];
                Vector3 vb = rawVerts[tris[t + 1]];
                Vector3 vc = rawVerts[tris[t + 2]];
                float triCx = (va.x + vb.x + vc.x) / 3f;
                float triCz = (va.z + vb.z + vc.z) / 3f;
                bool triInsideOriginal = borderEnabled
                    ? IsInsideContour(triCx, triCz, originalPoly)
                    : true;
                if (triInsideOriginal)
                { teeTris.Add(tris[t]); teeTris.Add(tris[t + 1]); teeTris.Add(tris[t + 2]); }
                else
                { borderSrcTris.Add(tris[t]); borderSrcTris.Add(tris[t + 1]); borderSrcTris.Add(tris[t + 2]); }
            }

            // Border UVs: U = normalized distance to tee edge (light-side toward tee).
            var finalVerts = new System.Collections.Generic.List<Vector3>(rawVerts);
            var finalUVs = new System.Collections.Generic.List<Vector2>(uvs);
            var vertRemap = new System.Collections.Generic.Dictionary<int, int>();
            var borderTris = new System.Collections.Generic.List<int>(borderSrcTris.Count);
            foreach (int origIdx in borderSrcTris)
            {
                if (!vertRemap.TryGetValue(origIdx, out int newIdx))
                {
                    Vector3 src = rawVerts[origIdx];
                    float dist = Mathf.Sqrt(DistanceSqToContour(src.x, src.z, originalPoly));
                    float u = Mathf.Clamp01(dist / borderWidth);
                    float v = (src.x + src.z) / borderTileSize;
                    newIdx = finalVerts.Count;
                    finalVerts.Add(src);
                    finalUVs.Add(new Vector2(u, v));
                    vertRemap[origIdx] = newIdx;
                }
                borderTris.Add(newIdx);
            }

            var vertsArr = finalVerts.ToArray();
            for (int i = 0; i < vertsArr.Length; i++)
                vertsArr[i] -= centroid;

            var mesh = new Mesh();
            mesh.name = $"{zoneName}_{id}";
            mesh.vertices = vertsArr;
            mesh.uv = finalUVs.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(teeTris.ToArray(), 0);
            mesh.SetTriangles(borderTris.ToArray(), 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"{zoneName}_{id}");
            go.transform.position = centroid;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[] { mat, borderMat };
            AddCleanMeshCollider(go, mesh);

            var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
            marker.surfaceType = surfaceType;
            return go;
        }

        private static MeshCollider AddCleanMeshCollider(GameObject go, Mesh mesh)
        {
            // Skip meshes too small for PhysX to cook — they produce
            // "cleaning the mesh failed" errors. Bounds extent < 0.5m
            // in any axis means the shape is tiny junk from noisy source data.
            var bounds = mesh.bounds;
            if (bounds.extents.x < 0.5f || bounds.extents.z < 0.5f)
                return null;

            var mc = go.AddComponent<MeshCollider>();
            mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation
                              | MeshColliderCookingOptions.EnableMeshCleaning
                              | MeshColliderCookingOptions.WeldColocatedVertices;
            mc.sharedMesh = mesh;
            return mc;
        }

        /// <summary>
        /// Ear-clipping triangulation for a polygon in XZ plane.
        /// Works for concave polygons. Returns triangle indices into the original array.
        /// Produces CW winding (front-face up in Unity's left-handed system).
        /// </summary>
        private static int[] EarClipTriangulate(Vector3[] pts)
        {
            int n = pts.Length;
            if (n < 3) return null;

            // Build a working list of indices
            var indices = new System.Collections.Generic.List<int>(n);
            for (int i = 0; i < n; i++) indices.Add(i);

            // Determine polygon winding (signed area in XZ)
            float area = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += pts[i].x * pts[j].z - pts[j].x * pts[i].z;
            }
            // area > 0 = CCW in XZ, area < 0 = CW in XZ
            // We want CW output triangles (front-face up in Unity).
            // If polygon is CCW, we emit triangles as (prev, curr, next).
            // If polygon is CW, we emit as (next, curr, prev) to flip.
            bool isCCW = area > 0;

            var result = new System.Collections.Generic.List<int>();
            int safety = n * n; // prevent infinite loop on degenerate polygons

            while (indices.Count > 2 && safety-- > 0)
            {
                bool earFound = false;
                int count = indices.Count;

                for (int i = 0; i < count; i++)
                {
                    int prev = indices[(i - 1 + count) % count];
                    int curr = indices[i];
                    int next = indices[(i + 1) % count];

                    // Check if this vertex is convex
                    float cross = CrossXZ(pts[prev], pts[curr], pts[next]);
                    // For CCW polygon: convex vertex has positive cross (left/CCW turn)
                    // For CW polygon: convex vertex has negative cross (right/CW turn)
                    bool isConvex = isCCW ? (cross > 0) : (cross < 0);

                    // Skip degenerate near-collinear triangles (area < 0.1 m²)
                    // These cause z-fighting flicker / black faces at thin polygon tips
                    float triArea = Mathf.Abs(cross) * 0.5f;
                    if (triArea < 0.1f)
                    {
                        // Remove the vertex without emitting a triangle
                        indices.RemoveAt(i);
                        earFound = true;
                        break;
                    }

                    if (!isConvex) continue;

                    // Check that no other vertex is inside this triangle
                    bool hasPointInside = false;
                    for (int j = 0; j < count; j++)
                    {
                        int testIdx = indices[j];
                        if (testIdx == prev || testIdx == curr || testIdx == next) continue;
                        if (PointInTriangleXZ(pts[testIdx], pts[prev], pts[curr], pts[next]))
                        {
                            hasPointInside = true;
                            break;
                        }
                    }
                    if (hasPointInside) continue;

                    // This is an ear — emit triangle with CW winding (front-face up in Unity)
                    if (isCCW)
                    {
                        result.Add(next);
                        result.Add(curr);
                        result.Add(prev);
                    }
                    else
                    {
                        result.Add(prev);
                        result.Add(curr);
                        result.Add(next);
                    }

                    indices.RemoveAt(i);
                    earFound = true;
                    break;
                }

                if (!earFound)
                {
                    // Degenerate polygon — fall back to fan from first vertex
                    Debug.LogWarning($"[EarClip] No ear found with {indices.Count} vertices remaining, falling back to fan");
                    for (int i = 1; i < indices.Count - 1; i++)
                    {
                        result.Add(indices[0]);
                        result.Add(indices[i]);
                        result.Add(indices[i + 1]);
                    }
                    break;
                }
            }

            return result.ToArray();
        }

        /// <summary>Cross product of (b-a) × (c-b) in XZ plane. Positive = CCW turn.</summary>
        private static float CrossXZ(Vector3 a, Vector3 b, Vector3 c)
        {
            return (b.x - a.x) * (c.z - b.z) - (b.z - a.z) * (c.x - b.x);
        }

        /// <summary>Test if point p is inside triangle (a, b, c) in XZ plane using barycentric coords.</summary>
        private static bool PointInTriangleXZ(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            float d1 = (p.x - b.x) * (a.z - b.z) - (a.x - b.x) * (p.z - b.z);
            float d2 = (p.x - c.x) * (b.z - c.z) - (b.x - c.x) * (p.z - c.z);
            float d3 = (p.x - a.x) * (c.z - a.z) - (c.x - a.x) * (p.z - a.z);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

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

            float yOffset = 0.01f; // offset to clear terrain between sample points

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
                    tx = spine[n - 1].z - spine[n - 2].z;
                    tz = spine[n - 1].x - spine[n - 2].x;
                }
                else
                {
                    tx = spine[i + 1].z - spine[i - 1].z;
                    tz = spine[i + 1].x - spine[i - 1].x;
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
                    float dx = cx - spine[i - 1].z;
                    float dz2 = cz - spine[i - 1].x;
                    arcLength += Mathf.Sqrt(dx * dx + dz2 * dz2);
                }
                uvs[i * 2]     = new Vector2(0f, arcLength / tileSize);
                uvs[i * 2 + 1] = new Vector2(1f, arcLength / tileSize);
            }

            // Compute centroid for mesh positioning (Y=0 so vertex Y values are absolute terrain heights)
            float sumX = 0, sumZ = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                sumX += verts[i].x; sumZ += verts[i].z;
            }
            Vector3 centroid = new Vector3(sumX / verts.Length, 0, sumZ / verts.Length);

            // Make vertices relative to centroid
            for (int i = 0; i < verts.Length; i++)
                verts[i] -= centroid;

            // Triangles: quad strip
            int quadCount = n - 1;
            var tris = new int[quadCount * 6];
            for (int i = 0; i < quadCount; i++)
            {
                int bl = i * 2;
                int br = i * 2 + 1;
                int tl = (i + 1) * 2;
                int tr = (i + 1) * 2 + 1;

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


        /// <summary>
        /// Offset a closed contour outward by a distance.
        /// At each vertex, compute the average outward normal of its two edges,
        /// then push the vertex along that normal with miter correction.
        /// </summary>
        private static Vector3[] OffsetContourOutward(Vector3[] contour, float distance)
        {
            int n = contour.Length;
            var result = new Vector3[n];

            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;

                // Edge vectors (XZ plane)
                Vector2 e1 = new Vector2(contour[i].x - contour[prev].x,
                                          contour[i].z - contour[prev].z).normalized;
                Vector2 e2 = new Vector2(contour[next].x - contour[i].x,
                                          contour[next].z - contour[i].z).normalized;

                // Outward normals (rotate 90° CW: (x,z) → (z,-x))
                // Contours are CCW in export; after 90° rotation to Unity, outward = CW rotation
                Vector2 n1 = new Vector2(e1.y, -e1.x);
                Vector2 n2 = new Vector2(e2.y, -e2.x);

                // Average normal
                Vector2 avg = (n1 + n2).normalized;

                // Miter correction: push further at sharp angles
                float dot = Vector2.Dot(n1, avg);
                float miter = (dot > 0.1f) ? distance / dot : distance;
                miter = Mathf.Min(miter, distance * 3f); // cap to prevent spikes

                result[i] = new Vector3(
                    contour[i].x + avg.x * miter,
                    contour[i].y,
                    contour[i].z + avg.y * miter);
            }

            return result;
        }

        /// <summary>
        /// Create a URP Lit material with tiled albedo + normal map for zone overlays.
        /// World-space tiling is handled in the mesh UVs (divided by tileSize),
        /// so material tiling stays at (1,1).
        /// </summary>
        private static Material CreateTiledMaterial(string texDir, string albedoName,
            string normalName, string dataDir, float tileSize)
        {
            string matPath = $"{dataDir}/MAT_{albedoName}.mat";
            var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existingMat != null)
                AssetDatabase.DeleteAsset(matPath);

            var mat = new Material(GetLitShader());
            mat.name = $"MAT_{albedoName}";

            var albedo = FindTextureExact(texDir, albedoName);
            if (albedo != null)
                mat.mainTexture = albedo;

            var normal = FindTextureExact(texDir, normalName);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 0.4f);
                mat.EnableKeyword("_NORMALMAP");
            }

            // Tiling is 1:1 because mesh UVs are worldPos / tileSize
            mat.mainTextureScale = Vector2.one;
            mat.SetFloat("_Smoothness", 0f);
            mat.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // ─── Mountain Backdrop ────────────────────────────────────────────

        private static void PlaceMountainBackdrop(
            Terrain terrain, float terrainBaseY,
            float terrainX, float terrainZ,
            string dataDir, Transform parentRoot)
        {
            var mountainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/3D/Props/Vegetation/FBX/Mountains.fbx");
            if (mountainPrefab == null)
            {
                Debug.LogWarning("[HoleLiteImporter] Mountains.fbx not found");
                return;
            }

            // Measure FBX native bounds
            var renderers = mountainPrefab.GetComponentsInChildren<Renderer>();
            Bounds fbxBounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var r in renderers)
            {
                if (fbxBounds.size == Vector3.zero) fbxBounds = r.bounds;
                else fbxBounds.Encapsulate(r.bounds);
            }
            float fbxDiameter = Mathf.Max(fbxBounds.size.x, fbxBounds.size.z);

            float scale = 0.7f;

            var instance = Object.Instantiate(mountainPrefab);
            instance.name = "MountainBackdrop";

            // Center at origin, base at terrain level
            instance.transform.position = new Vector3(0, 30f, 0);
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetParent(parentRoot);

            Debug.Log($"[HoleLiteImporter] Mountain backdrop: native={fbxDiameter:F1}m, scale={scale:F3}");
        }
    }
}
#endif
