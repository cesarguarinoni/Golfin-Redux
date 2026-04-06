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
                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Reading manifest...", 0f);

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
                terrainGO.transform.position = new Vector3(-terrainX / 2f, 0f, -terrainZ / 2f);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Applying texture...", 0.4f);
                ApplySplatmap(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Building hierarchy...", 0.6f);

                var holeRoot = new GameObject("HoleRoot");
                var metadata = holeRoot.AddComponent<HoleMetadata>();
                metadata.courseId = manifest.course_id;
                metadata.holeNumber = manifest.hole_number;
                metadata.par = manifest.par;
                metadata.strokeIndex = manifest.stroke_index;
                metadata.championshipYards = manifest.championship_yards;
                metadata.reviewStatus = manifest.review_status;

                terrainGO.transform.SetParent(holeRoot.transform);

                var anchorsRoot = new GameObject("Anchors");
                anchorsRoot.transform.SetParent(holeRoot.transform);

                var terrain = terrainGO.GetComponent<Terrain>();
                foreach (var anchor in anchors)
                    PlaceAnchorMarker(anchor, terrain, terrainGO.transform, anchorsRoot.transform);

                var debugRefs = new GameObject("DebugReferences");
                debugRefs.transform.SetParent(holeRoot.transform);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Setting up camera...", 0.8f);
                CreateWalkCamera(anchors, terrain, terrainGO.transform);

                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.84f);
                light.intensity = 1.2f;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // Apply skybox
                var skyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Skybox/Sky-2.mat");
                if (skyMat != null)
                    RenderSettings.skybox = skyMat;

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

        private static TerrainData CreateTerrain(HoleManifest manifest, string exportPath,
            string dataDir, string holeId, string projectRoot, float terrainX, float terrainZ)
        {
            int res = manifest.terrain.resolution;
            float elevRange = manifest.terrain.max_elevation_m - manifest.terrain.min_elevation_m;

            string heightmapPath = Path.Combine(exportPath, manifest.terrain.heightmap_file);
            byte[] rawBytes = File.ReadAllBytes(heightmapPath);

            // Rotate heightmap 90° CCW: heights[hx, hy] instead of heights[res-1-hy, hx]
            float[,] heights = new float[res, res];
            for (int hy = 0; hy < res; hy++)
            {
                for (int hx = 0; hx < res; hx++)
                {
                    int idx = (hy * res + hx) * 2;
                    ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
                    heights[hx, hy] = val / 65535f;
                }
            }

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = res;
            terrainData.size = new Vector3(terrainX, elevRange, terrainZ);
            terrainData.SetHeights(0, 0, heights);

            string terrainAssetPath = $"{dataDir}/TerrainData_Hole{holeId}.asset";
            EnsureDirectory(Path.Combine(projectRoot, Path.GetDirectoryName(terrainAssetPath)));
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

        private static void PlaceAnchorMarker(AnchorData anchor, Terrain terrain,
            Transform terrainTransform, Transform parent)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"Anchor_{anchor.type}";
            marker.transform.localScale = new Vector3(2f, 5f, 2f);

            var renderer = marker.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));
            if (anchor.type.Contains("back")) mat.color = Color.blue;
            else if (anchor.type.Contains("regular")) mat.color = Color.green;
            else if (anchor.type.Contains("front")) mat.color = Color.white;
            else if (anchor.type.Contains("ladies")) mat.color = Color.red;
            else mat.color = Color.yellow;
            renderer.sharedMaterial = mat;

            // 90° CCW rotation: (x, z) → (-z, x) → (local.z, local.x)
            Vector3 worldPos = new Vector3(anchor.local.z, 0f, anchor.local.x);
            float terrainHeight = terrain.SampleHeight(worldPos);
            float terrainBase = terrainTransform.position.y;
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
            byte[] grid = System.Convert.FromBase64String(zonesData.grid);
            int zoneW = zonesData.source_dimensions.width;
            int zoneH = zonesData.source_dimensions.height;

            Debug.Log($"[HoleLiteImporter] Zone grid: {zoneW}x{zoneH}, {grid.Length} bytes");

            // --- 2. Resample to alphamap resolution ---
            int alphaRes = 256;
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

            // --- 3. Generate fringe ring around greens ---
            int fringeRadius = 3;
            bool[] greenMask = new bool[alphaRes * alphaRes];
            for (int i = 0; i < resampledZones.Length; i++)
                greenMask[i] = (resampledZones[i] == 2);

            bool[] dilatedGreen = DilateMask(greenMask, alphaRes, alphaRes, fringeRadius);

            bool[] fringeMask = new bool[alphaRes * alphaRes];
            for (int i = 0; i < fringeMask.Length; i++)
            {
                if (dilatedGreen[i] && !greenMask[i])
                {
                    int zone = resampledZones[i];
                    // Only place fringe on adjacent playable surfaces
                    if (zone == 1 || zone == 3 || zone == 4)
                        fringeMask[i] = true;
                }
            }

            // --- 4. Build raw alphamap ---
            int layerCount = 8;
            float[,,] alphamap = new float[alphaRes, alphaRes, layerCount];

            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    int idx = ay * alphaRes + ax;
                    int layer;

                    if (fringeMask[idx])
                        layer = 7; // fringe
                    else
                        layer = ZoneToLayer(resampledZones[idx]);

                    alphamap[ay, ax, layer] = 1.0f;
                }
            }

            // --- 5. Gaussian blur + re-normalize ---
            int blurRadius = 3;
            float sigma = blurRadius / 2.0f;

            for (int l = 0; l < layerCount; l++)
            {
                float[,] channel = ExtractChannel(alphamap, alphaRes, layerCount, l);
                float[,] blurred = GaussianBlur2D(channel, alphaRes, blurRadius, sigma);
                SetChannel(alphamap, alphaRes, layerCount, l, blurred);
            }

            // Re-normalize so weights sum to 1.0
            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    float sum = 0f;
                    for (int l = 0; l < layerCount; l++)
                        sum += alphamap[ay, ax, l];

                    if (sum > 0.001f)
                    {
                        for (int l = 0; l < layerCount; l++)
                            alphamap[ay, ax, l] /= sum;
                    }
                    else
                    {
                        alphamap[ay, ax, 3] = 1.0f; // fallback: rough
                    }
                }
            }

            // --- 6. Create TerrainLayers and apply ---
            string texDir = "Assets/Courses/Textures_2025(JPG)";

            string[] albedoNames = {
                "T_Fairway_Light",      // 0 fairway
                "T_Green_Albedo",       // 1 green
                "T_Semirough_Albedo",   // 2 semi-rough
                "T_Rough_Albedo",       // 3 rough (catch-all)
                "T_Bunker_Albedo",      // 4 bunker
                "T_Tee_Albedo",         // 5 tee
                "T_RoadAsphalt_Albedo", // 6 cart path
                "T_Fringe_Albedo",      // 7 fringe
            };
            string[] normalNames = {
                "T_Fairway_Normal",
                "T_Green_Normal",
                "T_Semirough_Normal",
                "T_Rough_Normal",
                "T_Bunker_Normal",
                "T_Tee_Normal",
                "T_RoadAsphalt_Normal",
                "T_Fringe_Normal",
            };
            float[] tileSizes = { 5f, 3f, 6f, 8f, 4f, 3f, 4f, 4f };

            var layers = new TerrainLayer[layerCount];
            EnsureDirectory(Path.Combine(projectRoot, dataDir));

            for (int i = 0; i < layerCount; i++)
            {
                layers[i] = new TerrainLayer();
                layers[i].diffuseTexture = FindTextureExact(texDir, albedoNames[i]);
                layers[i].normalMapTexture = FindTextureExact(texDir, normalNames[i]);
                layers[i].tileSize = new Vector2(tileSizes[i], tileSizes[i]);
                layers[i].tileOffset = Vector2.zero;
                layers[i].smoothness = 0f;
                layers[i].metallic = 0f;
                layers[i].normalScale = 0.3f;

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
                      $"alphamap {alphaRes}x{alphaRes}, blur radius {blurRadius}");
        }

        private static int ZoneToLayer(int zoneIndex)
        {
            return zoneIndex switch
            {
                1  => 0,  // fairway
                2  => 1,  // green
                3  => 2,  // semi_rough
                4  => 3,  // rough
                5  => 3,  // trees → rough texture
                6  => 4,  // bunker
                7  => 3,  // water → rough for now
                8  => 6,  // cart_path
                9  => 3,  // ob → rough texture
                10 => 5,  // tee_box
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
            var sphereMat = new Material(Shader.Find("Standard"));
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
                var m = new Material(Shader.Find("Standard"));
                m.color = debugColors[i];
                r.sharedMaterial = m;

                Debug.Log($"[TestZoneAlignment] {debugNames[i]} centroid: norm({cnx:F3}, {cny:F3}) → world({wx:F1}, {wz:F1}), {count}px");
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
#endif
