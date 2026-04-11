#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    public static class HoleLiteImporter
    {
        // ─── Tunable Shore Slope Parameters ─────────────────────────
        /// <summary>Radius in heightmap cells around water to apply slope. At 1025 res, ~0.5m/cell.</summary>
        public static int ShoreRadius = 2;
        /// <summary>Maximum depth of shore depression in meters below flat terrain.</summary>
        public static float ShoreDepthMeters = 0.1f;

        // ─── Heightmap Smoothing Parameters ─────────────────────────
        private const int SmoothRadius = 8;
        private const float SmoothSigma = 4.0f;
        private const int TransitionCells = 40;
        private static readonly System.Collections.Generic.HashSet<int> PlayZones =
            new System.Collections.Generic.HashSet<int> { 1, 2, 6, 7, 8, 10 };

        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 01")] public static void Lite01() { ImportLiteHole("lomond-country-club", 1); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 02")] public static void Lite02() { ImportLiteHole("lomond-country-club", 2); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 03")] public static void Lite03() { ImportLiteHole("lomond-country-club", 3); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 04")] public static void Lite04() { ImportLiteHole("lomond-country-club", 4); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 05")] public static void Lite05() { ImportLiteHole("lomond-country-club", 5); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 06")] public static void Lite06() { ImportLiteHole("lomond-country-club", 6); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 07")] public static void Lite07() { ImportLiteHole("lomond-country-club", 7); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 08")] public static void Lite08() { ImportLiteHole("lomond-country-club", 8); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 09")] public static void Lite09() { ImportLiteHole("lomond-country-club", 9); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 10")] public static void Lite10() { ImportLiteHole("lomond-country-club", 10); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 11")] public static void Lite11() { ImportLiteHole("lomond-country-club", 11); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 12")] public static void Lite12() { ImportLiteHole("lomond-country-club", 12); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 13")] public static void Lite13() { ImportLiteHole("lomond-country-club", 13); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 14")] public static void Lite14() { ImportLiteHole("lomond-country-club", 14); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 15")] public static void Lite15() { ImportLiteHole("lomond-country-club", 15); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 16")] public static void Lite16() { ImportLiteHole("lomond-country-club", 16); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 17")] public static void Lite17() { ImportLiteHole("lomond-country-club", 17); }
        [MenuItem("GOLFIN/Import Hole (Lite)/Hole 18")] public static void Lite18() { ImportLiteHole("lomond-country-club", 18); }

        [MenuItem("GOLFIN/Import Hole (Lite)/All 18 Holes")]
        public static void LiteAll()
        {
            for (int i = 1; i <= 18; i++)
                ImportLiteHole("lomond-country-club", i);
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
                terrainGO.transform.position = new Vector3(-terrainX / 2f, -ShoreDepthMeters, -terrainZ / 2f);

                // Disable reflection probes on terrain
                var terrainComp = terrainGO.GetComponent<Terrain>();
                terrainComp.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                // Create holeRoot early so bunkers can be parented to it
                var holeRoot = new GameObject("HoleRoot");
                terrainGO.transform.SetParent(holeRoot.transform);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Applying texture...", 0.4f);
                ApplySplatmap(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);

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

                terrainData.SetHoles(0, 0, holes);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Building hierarchy...", 0.6f);

                var metadata = holeRoot.AddComponent<HoleMetadata>();
                metadata.courseId = manifest.course_id;
                metadata.holeNumber = manifest.hole_number;
                metadata.par = manifest.par;
                metadata.strokeIndex = manifest.stroke_index;
                metadata.championshipYards = manifest.championship_yards;
                metadata.reviewStatus = manifest.review_status;

                var anchorsRoot = new GameObject("Anchors");
                anchorsRoot.transform.SetParent(holeRoot.transform);

                var terrain = terrainGO.GetComponent<Terrain>();

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

                foreach (var anchor in anchors)
                    PlaceAnchorMarker(anchor, terrain, terrainGO.transform, anchorsRoot.transform,
                        hasGreenCentroid, greenCentroid);

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
            // terrainGO.position.y = -ShoreDepthMeters
            // So: normalizedFlat * elevRange + (-ShoreDepthMeters) = 0
            // normalizedFlat = ShoreDepthMeters / elevRange
            float normalizedFlat = ShoreDepthMeters / elevRange;

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

                    // Step 1: Build play-area mask
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
                            int zone = smGrid[gy * smW + gx];
                            if (PlayZones.Contains(zone))
                                isPlayArea[hz * actualRes + hx] = true;
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

                    // Blend factor: 1.0 = keep raw DEM (play area), 0.0 = fully smoothed
                    float[] blendFactor = new float[actualRes * actualRes];
                    for (int i = 0; i < blendFactor.Length; i++)
                    {
                        if (isPlayArea[i])
                            blendFactor[i] = 1.0f;
                        else if (distToPlay[i] < TransitionCells)
                            blendFactor[i] = 1.0f - distToPlay[i] / TransitionCells;
                        else
                            blendFactor[i] = 0.0f;
                    }

                    // Step 3: Gaussian blur + blend
                    float[,] smoothed = GaussianBlur2D(heights, actualRes, SmoothRadius, SmoothSigma);
                    for (int z = 0; z < actualRes; z++)
                    {
                        for (int x = 0; x < actualRes; x++)
                        {
                            float b = blendFactor[z * actualRes + x];
                            heights[z, x] = Mathf.Lerp(smoothed[z, x], heights[z, x], b);
                        }
                    }

                    Debug.Log($"[HoleLiteImporter] Heightmap smoothing applied (radius={SmoothRadius}, transition={TransitionCells} cells)");
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

        private static void PlaceAnchorMarker(AnchorData anchor,
            Terrain terrain, Transform terrainTransform, Transform parent,
            bool hasGreenCentroid, Vector3 greenCentroid)
        {
            // 90° CCW rotation: (x, z) → (-z, x) → (local.z, local.x)
            Vector3 worldPos = new Vector3(anchor.local.z, 0f, anchor.local.x);
            float terrainBase = terrainTransform.position.y;

            if (anchor.type.Contains("tee"))
            {
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

                // Compute forward direction to green and perpendicular for spacing
                Vector3 forwardDir = Vector3.forward; // default
                Vector3 perpDir = Vector3.right;      // default: space along X axis
                if (hasGreenCentroid)
                {
                    Vector3 toGreen = (greenCentroid - worldPos);
                    toGreen.y = 0f;
                    if (toGreen.sqrMagnitude > 0.01f)
                    {
                        forwardDir = toGreen.normalized;
                        perpDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
                        if (perpDir.sqrMagnitude < 0.001f)
                            perpDir = Vector3.right;
                    }
                }

                // Rotation: markers face the green
                Quaternion rotation = Quaternion.LookRotation(forwardDir, Vector3.up);

                // Place 2 markers: Left and Right, spaced 3m apart (1.5m each side)
                for (int side = 0; side < 2; side++)
                {
                    float offset = (side == 0) ? -1.5f : 1.5f;
                    string suffix = (side == 0) ? "L" : "R";

                    Vector3 markerPos = worldPos + perpDir * offset;
                    float terrainHeight = terrain.SampleHeight(markerPos);

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
            string exportPath, string dataDir, string holeId, string projectRoot)
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
            int layerCount = 8;
            float[,,] alphamap = new float[alphaRes, alphaRes, layerCount];

            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    int idx = ay * alphaRes + ax;
                    int zone = resampledZones[idx];
                    int layer = ZoneToLayer(zone);
                    alphamap[ay, ax, layer] = 1.0f;
                }
            }

            // Blur removed — fringe rings handle zone transitions.
            // GaussianBlur2D / ExtractChannel / SetChannel kept as helpers.

            // --- 5. (Zone boundary smoothing now happens at source in classify-zones.mjs) ---

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
            };
            float[] tileSizes = { 5f, 3f, 6f, 8f, 4f, 3f, 4f, 8f };

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

                string layerPath = $"{dataDir}/TerrainLayer_{albedoNames[i]}.asset";
                var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
                if (existingLayer != null)
                    AssetDatabase.DeleteAsset(layerPath);
                AssetDatabase.CreateAsset(layers[i], layerPath);
            }

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

            float greenHeight = greensFile.height_m > 0 ? greensFile.height_m : 0.15f;

            var greenMat = CreateZoneMaterial(dataDir, projectRoot,
                "GreenSurface", "T_Green_Albedo", 3f);
            var collarMat = CreateZoneMaterial(dataDir, projectRoot,
                "GreenCollar", "T_Semirough_Albedo", 4f);

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

                // Create raised mesh
                float surfaceY = terrainBaseY + terrain.SampleHeight(
                    new Vector3(centroidX, 0, centroidZ));

                var meshGO = CreateRaisedMesh(green.id, "Green", worldContour,
                    centroidX, centroidZ, surfaceY, greenHeight, greenMat,
                    terrain, terrainBaseY, collarMat, greenCollarScale);
                meshGO.transform.SetParent(greensRoot.transform);

                // Place flag at green centroid
                var flagPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Art/3D/Props/Flag/Flag.fbx");
                if (flagPrefab != null)
                {
                    var flag = Object.Instantiate(flagPrefab);
                    flag.name = $"Flag_{green.id}";
                    float flagY = surfaceY + greenHeight;
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
                    float flagY = surfaceY + greenHeight;
                    holeCup.transform.position = new Vector3(centroidX, flagY + 0.001f, centroidZ);
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

                // Find the surface child and add Green marker
                var surfaceChild = meshGO.transform.Find($"Green_{green.id}_Surface");
                if (surfaceChild != null)
                {
                    var marker = surfaceChild.gameObject.AddComponent<Golfin.Course.SurfaceMarker>();
                    marker.surfaceType = Golfin.Course.SurfaceType.Green;
                }
            }

            // Copy greens.json to Assets
            string destPath = Path.Combine(projectRoot, dataDir, "greens.json");
            File.Copy(greensPath, destPath, true);
            AssetDatabase.ImportAsset($"{dataDir}/greens.json");

            Debug.Log($"[HoleLiteImporter] Created {greensFile.greens.Length} green(s)");
        }


        private static GameObject CreateRaisedMesh(int id, string zoneName,
            Vector2[] contour, float centroidX, float centroidZ,
            float surfaceY, float height, Material surfaceMat,
            Terrain terrain, float terrainBaseY,
            Material collarMat = null, float collarScale = 1.08f)
        {
            int n = contour.Length;
            if (n < 3) return new GameObject($"{zoneName}_{id}_SKIP");

            // Parent object positioned at centroid
            var parent = new GameObject($"{zoneName}_{id}");
            parent.transform.position = new Vector3(centroidX, surfaceY, centroidZ);

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

                        float y;
                        if (collarHeightFracs[r] < 0)
                        {
                            float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                            y = (terrainBaseY + terrainH) - surfaceY + 0.02f;
                        }
                        else
                        {
                            y = height * collarHeightFracs[r];
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

                    int vi = r * n + i;
                    surfaceVerts[vi] = new Vector3(wx - centroidX, height, wz - centroidZ);
                    surfaceUVs[vi] = new Vector2(
                        (wx - minX) / extentX,
                        (wz - minZ) / extentZ);
                }
            }

            int centerIdx = surfaceVertCount - 1;
            surfaceVerts[centerIdx] = new Vector3(0, height, 0);
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

                // Sample terrain at each vertex directly — water follows the slope
                Vector3[] worldPts = new Vector3[n];
                float sumX = 0, sumY = 0, sumZ = 0;
                for (int i = 0; i < n; i++)
                {
                    float wx = water.contour[i].z;  // 90° CCW
                    float wz = water.contour[i].x;
                    float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                    float wy = terrainBaseY + terrainH - 0.1f;
                    worldPts[i] = new Vector3(wx, wy, wz);
                    sumX += wx; sumY += wy; sumZ += wz;
                }
                float centroidX = sumX / n;
                float centroidY = sumY / n;
                float centroidZ = sumZ / n;
                Vector3 centroid = new Vector3(centroidX, centroidY, centroidZ);

                // Build mesh with ear-clip triangulation
                var verts = new Vector3[n];
                var uvs = new Vector2[n];
                float tileSize = 10f; // water texture tiling
                for (int i = 0; i < n; i++)
                {
                    verts[i] = worldPts[i] - centroid;
                    uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
                }

                var tris = EarClipTriangulate(worldPts);
                if (tris == null || tris.Length < 3)
                {
                    Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: ear-clip failed, skipping");
                    continue;
                }

                var mesh = new Mesh();
                mesh.name = $"Water_{water.id}";
                mesh.vertices = verts;
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
                          $"{tris.Length / 3} tris, pos ({centroidX:F1}, {centroidY:F2}, {centroidZ:F1})");
            }

            // ─── Shore slope pass: depress terrain near water edges ──────────
            if (ShoreRadius > 0 && ShoreDepthMeters > 0f)
            {
                int hRes = terrainData.heightmapResolution;
                float[,] heights = terrainData.GetHeights(0, 0, hRes, hRes);
                float elevRange = terrainData.size.y;

                // Build water mask on the heightmap grid using zone data
                string zonesPath = Path.Combine(exportPath, "zones.json");
                bool[,] isWater = new bool[hRes, hRes];

                if (File.Exists(zonesPath))
                {
                    string zonesJson = File.ReadAllText(zonesPath);
                    var zonesData = JsonUtility.FromJson<ZonesData>(zonesJson);
                    byte[] grid = System.Convert.FromBase64String(zonesData.grid);
                    int zw = zonesData.source_dimensions.width;
                    int zh = zonesData.source_dimensions.height;

                    for (int hz = 0; hz < hRes; hz++)
                    {
                        for (int hx = 0; hx < hRes; hx++)
                        {
                            // Map heightmap cell to zone grid
                            // Heightmap uses 90° CCW rotation from zone grid
                            float normX = (float)hx / (hRes - 1);
                            float normZ = (float)hz / (hRes - 1);

                            // Reverse the 90° CCW: zone.x = normZ, zone.y = normX
                            int zx = Mathf.Clamp(Mathf.RoundToInt(normZ * (zw - 1)), 0, zw - 1);
                            int zy = Mathf.Clamp(Mathf.RoundToInt(normX * (zh - 1)), 0, zh - 1);

                            if (grid[zy * zw + zx] == 7) // 7 = water zone
                                isWater[hz, hx] = true;
                        }
                    }
                }

                // Chamfer distance transform (approximate Euclidean)
                float[,] distToWater = new float[hRes, hRes];
                for (int z = 0; z < hRes; z++)
                    for (int x = 0; x < hRes; x++)
                        distToWater[z, x] = isWater[z, x] ? 0f : float.MaxValue;

                // Forward pass
                for (int z = 0; z < hRes; z++)
                {
                    for (int x = 0; x < hRes; x++)
                    {
                        if (x > 0)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z, x - 1] + 1f);
                        if (z > 0)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z - 1, x] + 1f);
                        if (x > 0 && z > 0)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z - 1, x - 1] + 1.414f);
                        if (x < hRes - 1 && z > 0)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z - 1, x + 1] + 1.414f);
                    }
                }

                // Backward pass
                for (int z = hRes - 1; z >= 0; z--)
                {
                    for (int x = hRes - 1; x >= 0; x--)
                    {
                        if (x < hRes - 1)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z, x + 1] + 1f);
                        if (z < hRes - 1)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z + 1, x] + 1f);
                        if (x < hRes - 1 && z < hRes - 1)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z + 1, x + 1] + 1.414f);
                        if (x > 0 && z < hRes - 1)
                            distToWater[z, x] = Mathf.Min(distToWater[z, x], distToWater[z + 1, x - 1] + 1.414f);
                    }
                }

                // Shore lip disabled — only underwater depression applied
                int depressedCount = 0;

                // Depress water cells below water mesh surface
                // Water mesh is at sampleHeight - 0.05m; push terrain 0.5m below that
                float underwaterDrop = (ShoreDepthMeters + 0.15f) / elevRange;
                for (int z = 0; z < hRes; z++)
                {
                    for (int x = 0; x < hRes; x++)
                    {
                        if (isWater[z, x])
                        {
                            float currentH = heights[z, x];
                            heights[z, x] = Mathf.Max(0f, currentH - underwaterDrop);
                        }
                    }
                }

                terrainData.SetHeights(0, 0, heights);
                Debug.Log($"[HoleLiteImporter] Shore slope: depressed {depressedCount} cells, " +
                          $"radius={ShoreRadius}, depth={ShoreDepthMeters:F1}m");
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
            mat.SetFloat("_DepthEnd", 0.3f);

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

                        // Fairway mesh with mow stripes via UV-oriented texture
                        var meshGO = CreateFairwayMesh(
                            fw.id, fw.contour, terrain, terrainBaseY,
                            fairwayMat, stripeDir, stripeWidth);
                        if (meshGO != null)
                            meshGO.transform.SetParent(fwRoot.transform);

                        // Fringe ring inside fairway edge (negative = inward offset)
                        var fringeGO = CreateFringeRing(
                            fw.id, fw.contour, terrain, terrainBaseY,
                            fringeMat, -FairwayFringeMeters, 6f, fwRoot.transform);
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

                        var meshGO = CreateFlatContourMesh(
                            region.id, "Tee", region.contour,
                            terrain, terrainBaseY, teeMat, 3f,
                            Golfin.Course.SurfaceType.Tee);
                        if (meshGO != null)
                            meshGO.transform.SetParent(teeRoot.transform);

                        // Tee border ring (gradient: light inside → dark outside)
                        var borderGO = CreateGradientBorderRing(
                            region.id, "Tee", region.contour,
                            terrain, terrainBaseY, teeBorderMat,
                            1.0f,  // border width in meters
                            3f,    // UV.u tile every 3m along perimeter
                            Golfin.Course.SurfaceType.Fringe,
                            teeRoot.transform);
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
                            // Fallback to ear-clip (backward compatibility)
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

        /// <summary>
        /// Create a flat mesh from a contour polygon, positioned at terrain height.
        /// Uses centroid-fan triangulation (works for convex and mildly concave shapes).
        /// </summary>
        private static GameObject CreateFlatContourMesh(int id, string zoneName,
            ContourPoint[] contour, Terrain terrain, float terrainBaseY,
            Material mat, float tileSize, Golfin.Course.SurfaceType surfaceType)
        {
            int n = contour.Length;
            if (n < 3) return null;

            // Convert contour to world space (90° CCW rotation: worldX = z, worldZ = x)
            Vector3[] worldPts = new Vector3[n];
            float yOffset = 0.005f; // minimal offset to avoid z-fighting

            for (int i = 0; i < n; i++)
            {
                float wx = contour[i].z; // 90° CCW rotation
                float wz = contour[i].x;
                float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                worldPts[i] = new Vector3(wx, terrainBaseY + terrainH + yOffset, wz);
            }

            // Compute centroid
            float cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += worldPts[i].x;
                cz += worldPts[i].z;
            }
            cx /= n; cz /= n;
            // Sample terrain at centroid XZ instead of averaging Y (slope fix)
            float cy = terrainBaseY + terrain.SampleHeight(new Vector3(cx, 0, cz)) + yOffset;
            Vector3 centroid = new Vector3(cx, cy, cz);

            // Build mesh: vertices = contour points + centroid (all relative to centroid)
            var verts = new Vector3[n + 1];
            var uvs = new Vector2[n + 1];

            for (int i = 0; i < n; i++)
            {
                verts[i] = worldPts[i] - centroid; // relative to centroid
                uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
            }
            // Center vertex
            verts[n] = Vector3.zero; // centroid is at origin
            uvs[n] = new Vector2(cx / tileSize, cz / tileSize);

            // Triangles: fan from centroid
            var trisArr = new int[n * 3];
            for (int i = 0; i < n; i++)
            {
                trisArr[i * 3 + 0] = i;
                trisArr[i * 3 + 1] = n; // centroid
                trisArr[i * 3 + 2] = (i + 1) % n;
            }

            // Subdivide to conform to terrain surface
            var vertList = new System.Collections.Generic.List<Vector3>(verts);
            var uvList = new System.Collections.Generic.List<Vector2>(uvs);
            var triList = new System.Collections.Generic.List<int>(trisArr);
            SubdivideToTerrain(ref vertList, ref uvList, ref triList,
                centroid, terrain, terrainBaseY, tileSize, yOffset, 1.5f);

            var mesh = new Mesh();
            mesh.name = $"{zoneName}_{id}";
            mesh.vertices = vertList.ToArray();
            mesh.triangles = triList.ToArray();
            mesh.uv = uvList.ToArray();
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

        /// <summary>
        /// Create a flat mesh from a contour polygon using ear-clip triangulation.
        /// Handles concave/winding shapes like cart paths correctly.
        /// </summary>
        private static GameObject CreateEarClipContourMesh(int id, string zoneName,
            ContourPoint[] contour, Terrain terrain, float terrainBaseY,
            Material mat, float tileSize, Golfin.Course.SurfaceType surfaceType)
        {
            int n = contour.Length;
            if (n < 3) return null;

            float yOffset = 0.005f; // minimal offset to avoid z-fighting

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
            float cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            { cx += worldPts[i].x; cz += worldPts[i].z; }
            cx /= n; cz /= n;
            // Sample terrain at centroid XZ instead of averaging Y (slope fix)
            float cy = terrainBaseY + terrain.SampleHeight(new Vector3(cx, 0, cz)) + yOffset;
            Vector3 centroid = new Vector3(cx, cy, cz);

            var verts = new Vector3[n];
            var uvs = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                verts[i] = worldPts[i] - centroid;
                uvs[i] = new Vector2(worldPts[i].x / tileSize, worldPts[i].z / tileSize);
            }

            var tris = EarClipTriangulate(worldPts);
            if (tris == null || tris.Length < 3)
            {
                Debug.LogWarning($"[HoleLiteImporter] {zoneName} {id}: ear-clip failed, skipping");
                return null;
            }

            // Subdivide long triangles so mesh follows terrain curvature
            var vertList = new System.Collections.Generic.List<Vector3>(verts);
            var uvList = new System.Collections.Generic.List<Vector2>(uvs);
            var triList = new System.Collections.Generic.List<int>(tris);
            SubdivideToTerrain(ref vertList, ref uvList, ref triList,
                centroid, terrain, terrainBaseY, tileSize, yOffset, 1.5f);

            var mesh = new Mesh();
            mesh.name = $"{zoneName}_{id}";
            mesh.vertices = vertList.ToArray();
            mesh.triangles = triList.ToArray();
            mesh.uv = uvList.ToArray();
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

        /// <summary>
        /// Create a fairway mesh with mow stripes via UV orientation.
        /// Uses a single T_Fairway_Mix texture that contains both light and dark bands.
        /// UVs are oriented so that one axis runs along stripeDir (perpendicular to
        /// tee→green), making the texture bands appear as parallel mow stripes.
        /// Ear-clipping triangulation handles concave fairway shapes correctly.
        /// </summary>
        private static GameObject CreateFairwayMesh(int id, ContourPoint[] contour,
            Terrain terrain, float terrainBaseY,
            Material mat, Vector2 stripeDir, float stripeWidth)
        {
            int n = contour.Length;
            if (n < 3) return null;

            float yOffset = 0.005f; // minimal offset to avoid z-fighting

            // stripeDir is perpendicular to tee→green. Compute the parallel axis.
            Vector2 parallelDir = new Vector2(-stripeDir.y, stripeDir.x);

            // Convert contour to world space (90° CCW: worldX = z, worldZ = x)
            Vector3[] worldPts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float wx = contour[i].z;
                float wz = contour[i].x;
                float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
                worldPts[i] = new Vector3(wx, terrainBaseY + th + yOffset, wz);
            }

            // Compute centroid for mesh positioning
            float cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += worldPts[i].x;
                cz += worldPts[i].z;
            }
            cx /= n; cz /= n;
            // Sample terrain at centroid XZ instead of averaging Y (slope fix)
            float cy = terrainBaseY + terrain.SampleHeight(new Vector3(cx, 0, cz)) + yOffset;
            Vector3 centroid = new Vector3(cx, cy, cz);

            // Build vertices (contour only — no centroid vertex needed for ear clipping)
            var verts = new Vector3[n];
            var uvs = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                verts[i] = worldPts[i] - centroid;
                float wx = worldPts[i].x;
                float wz = worldPts[i].z;
                uvs[i] = new Vector2(
                    (wx * stripeDir.x + wz * stripeDir.y) / stripeWidth,
                    (wx * parallelDir.x + wz * parallelDir.y) / stripeWidth);
            }

            // Ear-clipping triangulation (handles concave polygons)
            var trisArr = EarClipTriangulate(worldPts);
            if (trisArr == null || trisArr.Length < 3) return null;

            // Subdivide to conform to terrain surface
            var vertList = new System.Collections.Generic.List<Vector3>(verts);
            var uvList = new System.Collections.Generic.List<Vector2>(uvs);
            var triList = new System.Collections.Generic.List<int>(trisArr);
            SubdivideToTerrain(ref vertList, ref uvList, ref triList,
                centroid, terrain, terrainBaseY, stripeWidth, yOffset, 1.5f);

            // Recompute ALL UVs using stripe orientation (subdivision adds verts with wrong UVs)
            for (int i = 0; i < vertList.Count; i++)
            {
                Vector3 wp = vertList[i] + centroid;
                uvList[i] = new Vector2(
                    (wp.x * stripeDir.x + wp.z * stripeDir.y) / stripeWidth,
                    (wp.x * parallelDir.x + wp.z * parallelDir.y) / stripeWidth);
            }

            var mesh = new Mesh();
            mesh.name = $"Fairway_{id}";
            mesh.vertices = vertList.ToArray();
            mesh.triangles = triList.ToArray();
            mesh.uv = uvList.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"Fairway_{id}");
            go.transform.position = centroid;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            AddCleanMeshCollider(go, mesh);

            var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
            marker.surfaceType = Golfin.Course.SurfaceType.Fairway;

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
        /// Subdivide triangles until no edge exceeds maxEdgeLength.
        /// Each new vertex is placed at the terrain-sampled height.
        /// Works in local space (vertices are relative to centroid).
        /// </summary>
        private static void SubdivideToTerrain(
            ref System.Collections.Generic.List<Vector3> verts,
            ref System.Collections.Generic.List<Vector2> uvs,
            ref System.Collections.Generic.List<int> tris,
            Vector3 centroid, Terrain terrain, float terrainBaseY,
            float tileSize, float yOffset, float maxEdgeLength)
        {
            int maxIterations = 5;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                var newTris = new System.Collections.Generic.List<int>();
                bool subdivided = false;

                for (int t = 0; t < tris.Count; t += 3)
                {
                    int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                    Vector3 v0 = verts[i0], v1 = verts[i1], v2 = verts[i2];

                    float d01 = Vector3.Distance(v0, v1);
                    float d12 = Vector3.Distance(v1, v2);
                    float d20 = Vector3.Distance(v2, v0);

                    float maxD = Mathf.Max(d01, Mathf.Max(d12, d20));
                    if (maxD <= maxEdgeLength)
                    {
                        newTris.Add(i0); newTris.Add(i1); newTris.Add(i2);
                        continue;
                    }

                    subdivided = true;

                    // Split the longest edge at its midpoint
                    int iA, iB, iC;
                    if (d01 >= d12 && d01 >= d20) { iA = i0; iB = i1; iC = i2; }
                    else if (d12 >= d01 && d12 >= d20) { iA = i1; iB = i2; iC = i0; }
                    else { iA = i2; iB = i0; iC = i1; }

                    Vector3 midLocal = (verts[iA] + verts[iB]) * 0.5f;
                    Vector3 midWorld = midLocal + centroid;
                    float th = terrain.SampleHeight(new Vector3(midWorld.x, 0, midWorld.z));
                    midLocal.y = terrainBaseY + th + yOffset - centroid.y;

                    int iMid = verts.Count;
                    verts.Add(midLocal);
                    uvs.Add(new Vector2(midWorld.x / tileSize, midWorld.z / tileSize));

                    newTris.Add(iA); newTris.Add(iMid); newTris.Add(iC);
                    newTris.Add(iMid); newTris.Add(iB); newTris.Add(iC);
                }

                tris = newTris;
                if (!subdivided) break;
            }
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

            float yOffset = 0.005f; // minimal offset to avoid z-fighting

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

            // Compute centroid for mesh positioning
            float sumX = 0, sumZ = 0;
            for (int i = 0; i < verts.Length; i++)
            {
                sumX += verts[i].x; sumZ += verts[i].z;
            }
            float centX = sumX / verts.Length;
            float centZ = sumZ / verts.Length;
            // Sample terrain at centroid XZ instead of averaging Y (slope fix)
            float centY = terrainBaseY + terrain.SampleHeight(new Vector3(centX, 0, centZ)) + yOffset;
            Vector3 centroid = new Vector3(centX, centY, centZ);

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
        /// Create a fringe ring mesh along a fairway contour edge.
        /// Positive fringeWidth = outward, negative = inward.
        /// edgeRing = original contour, offsetRing = pushed by fringeWidth.
        /// </summary>
        private static GameObject CreateFringeRing(int id, ContourPoint[] contour,
            Terrain terrain, float terrainBaseY,
            Material fringeMat, float fringeWidth, float tileSize,
            Transform parent)
        {
            int n = contour.Length;
            if (n < 3) return null;

            float yOffset = 0.007f; // slightly above fairway (0.005)

            // Convert to world space — this is the contour edge
            Vector3[] edgeRing = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float wx = contour[i].z;
                float wz = contour[i].x;
                float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                edgeRing[i] = new Vector3(wx, terrainBaseY + terrainH + yOffset, wz);
            }

            // Offset ring (inward or outward depending on sign of fringeWidth)
            Vector3[] offsetRing = OffsetContourOutward(edgeRing, fringeWidth);

            // Update Y for offset ring
            for (int i = 0; i < offsetRing.Length; i++)
            {
                float h = terrain.SampleHeight(new Vector3(offsetRing[i].x, 0, offsetRing[i].z));
                offsetRing[i].y = terrainBaseY + h + yOffset;
            }

            // Compute centroid
            float cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += edgeRing[i].x;
                cz += edgeRing[i].z;
            }
            cx /= n; cz /= n;
            // Sample terrain at centroid XZ instead of averaging Y (slope fix)
            float cy = terrainBaseY + terrain.SampleHeight(new Vector3(cx, 0, cz)) + yOffset;
            Vector3 centroid = new Vector3(cx, cy, cz);

            // Vertices: edge ring (0..n-1) + offset ring (n..2n-1)
            var fringeVerts = new Vector3[n * 2];
            var fringeUVs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                fringeVerts[i] = edgeRing[i] - centroid;
                fringeVerts[n + i] = offsetRing[i] - centroid;
                fringeUVs[i] = new Vector2(edgeRing[i].x / tileSize, edgeRing[i].z / tileSize);
                fringeUVs[n + i] = new Vector2(offsetRing[i].x / tileSize, offsetRing[i].z / tileSize);
            }

            // Triangles: quad strip. Winding depends on whether offset is inward or outward.
            // Negative fringeWidth (inward) flips the winding relative to outward.
            var fringeTris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int curr = i;
                int next = (i + 1) % n;
                int offCurr = n + i;
                int offNext = n + next;
                int t = i * 6;

                if (fringeWidth >= 0)
                {
                    // Outward: edge→next→off (CCW from above)
                    fringeTris[t + 0] = curr;
                    fringeTris[t + 1] = next;
                    fringeTris[t + 2] = offCurr;
                    fringeTris[t + 3] = next;
                    fringeTris[t + 4] = offNext;
                    fringeTris[t + 5] = offCurr;
                }
                else
                {
                    // Inward: edge→off→next (reversed winding)
                    fringeTris[t + 0] = curr;
                    fringeTris[t + 1] = offCurr;
                    fringeTris[t + 2] = next;
                    fringeTris[t + 3] = next;
                    fringeTris[t + 4] = offCurr;
                    fringeTris[t + 5] = offNext;
                }
            }

            var fringeMesh = new Mesh();
            fringeMesh.name = $"FairwayFringe_{id}";
            fringeMesh.vertices = fringeVerts;
            fringeMesh.triangles = fringeTris;
            fringeMesh.uv = fringeUVs;
            fringeMesh.RecalculateNormals();
            fringeMesh.RecalculateBounds();

            var fringeGO = new GameObject($"FairwayFringe_{id}");
            fringeGO.transform.position = centroid;
            fringeGO.AddComponent<MeshFilter>().sharedMesh = fringeMesh;
            fringeGO.AddComponent<MeshRenderer>().sharedMaterial = fringeMat;
            AddCleanMeshCollider(fringeGO, fringeMesh);

            var fringeMarker = fringeGO.AddComponent<Golfin.Course.SurfaceMarker>();
            fringeMarker.surfaceType = Golfin.Course.SurfaceType.Fringe;

            fringeGO.transform.SetParent(parent);
            return fringeGO;
        }

        /// <summary>
        /// Create a gradient border ring around a contour.
        /// UV.v encodes inner(0)→outer(1) for the gradient texture,
        /// UV.u tiles along the perimeter using arc length.
        /// </summary>
        private static GameObject CreateGradientBorderRing(int id, string label,
            ContourPoint[] contour, Terrain terrain, float terrainBaseY,
            Material mat, float borderWidth, float uTileSize,
            Golfin.Course.SurfaceType surfaceType, Transform parent)
        {
            int n = contour.Length;
            if (n < 3) return null;

            float yOffset = 0.003f; // below tee mesh (0.005)

            // Convert to world space
            Vector3[] innerRing = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float wx = contour[i].z; // 90° CCW rotation
                float wz = contour[i].x;
                float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                innerRing[i] = new Vector3(wx, terrainBaseY + terrainH + yOffset, wz);
            }

            // Pull inner ring slightly inward so it overlaps under the tee mesh (no gap)
            float inwardOverlap = 0.15f;
            Vector3[] innerRingInset = OffsetContourOutward(innerRing, -inwardOverlap);
            for (int i = 0; i < n; i++)
            {
                float h = terrain.SampleHeight(new Vector3(innerRingInset[i].x, 0, innerRingInset[i].z));
                innerRingInset[i].y = terrainBaseY + h + yOffset;
            }

            // Outer ring
            Vector3[] outerRing = OffsetContourOutward(innerRing, borderWidth);
            for (int i = 0; i < n; i++)
            {
                float h = terrain.SampleHeight(new Vector3(outerRing[i].x, 0, outerRing[i].z));
                outerRing[i].y = terrainBaseY + h + yOffset;
            }

            // Centroid
            float cx = 0, cz = 0;
            for (int i = 0; i < n; i++)
            {
                cx += innerRing[i].x; cz += innerRing[i].z;
            }
            cx /= n; cz /= n;
            // Sample terrain at centroid XZ instead of averaging Y (slope fix)
            float cy = terrainBaseY + terrain.SampleHeight(new Vector3(cx, 0, cz)) + yOffset;
            Vector3 centroid = new Vector3(cx, cy, cz);

            // Arc lengths along inner ring for UV.u tiling
            float[] arcLengths = new float[n];
            arcLengths[0] = 0f;
            for (int i = 1; i < n; i++)
            {
                float dx = innerRing[i].x - innerRing[i - 1].x;
                float dz = innerRing[i].z - innerRing[i - 1].z;
                arcLengths[i] = arcLengths[i - 1] + Mathf.Sqrt(dx * dx + dz * dz);
            }

            // Vertices & UVs
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i] = innerRingInset[i] - centroid;
                verts[n + i] = outerRing[i] - centroid;

                float u = arcLengths[i] / uTileSize;
                uvs[i] = new Vector2(0f, u);       // inner = light (u=0)
                uvs[n + i] = new Vector2(1f, u);   // outer = dark  (u=1)
            }

            // Triangles: quad strip, outward winding
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int curr = i;
                int next = (i + 1) % n;
                int offCurr = n + i;
                int offNext = n + next;
                int t = i * 6;

                tris[t + 0] = curr;
                tris[t + 1] = next;
                tris[t + 2] = offCurr;
                tris[t + 3] = next;
                tris[t + 4] = offNext;
                tris[t + 5] = offCurr;
            }

            var mesh = new Mesh();
            mesh.name = $"{label}Border_{id}";
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject($"{label}Border_{id}");
            go.transform.position = centroid;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            AddCleanMeshCollider(go, mesh);

            var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
            marker.surfaceType = surfaceType;

            go.transform.SetParent(parent);
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
