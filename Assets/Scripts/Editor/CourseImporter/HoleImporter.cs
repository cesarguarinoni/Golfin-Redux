#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    public static class HoleImporter
    {
        [MenuItem("GOLFIN/Import Hole/Hole 01")]
        public static void ImportHole01()
        {
            ImportHole("lomond-country-club", 1);
        }

        public static void ImportHole(string courseId, int holeNumber)
        {
            string holeId = holeNumber.ToString("D2");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            // Paths
            string exportPath = Path.Combine(projectRoot, "Tools", "UHole", "output",
                courseId, "export", $"hole-{holeId}");
            string generatedDir = $"Assets/Golf/Courses/{courseId}/Generated";
            string dataDir = $"Assets/Golf/Courses/{courseId}/Data/hole-{holeId}";
            string scenePath = $"{generatedDir}/Hole_{holeId}.unity";

            // Validate export exists
            if (!Directory.Exists(exportPath))
            {
                EditorUtility.DisplayDialog("Import Error",
                    $"Export folder not found:\n{exportPath}", "OK");
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("Importing Hole", "Reading manifest...", 0f);

                // Ensure output directories exist
                EnsureDirectory(Path.Combine(projectRoot, generatedDir));
                EnsureDirectory(Path.Combine(projectRoot, dataDir));

                // 1. Read manifest
                string manifestJson = File.ReadAllText(Path.Combine(exportPath, "hole-manifest.json"));
                var manifest = JsonUtility.FromJson<HoleManifest>(manifestJson);

                // 2. Read anchors (root array — wrap for JsonUtility)
                string anchorsJson = File.ReadAllText(Path.Combine(exportPath, "anchors.json"));
                var anchorsWrapper = JsonUtility.FromJson<AnchorArrayWrapper>(
                    "{\"items\":" + anchorsJson + "}");
                var anchors = anchorsWrapper.items;

                // 3. Read aerial tiles
                string tilesJson = File.ReadAllText(Path.Combine(exportPath, "aerial-tiles.json"));
                var tilesData = JsonUtility.FromJson<AerialTilesData>(tilesJson);

                // 4. Create new scene
                EditorUtility.DisplayProgressBar("Importing Hole", "Creating scene...", 0.1f);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                // 5. Create terrain
                EditorUtility.DisplayProgressBar("Importing Hole", "Building terrain...", 0.2f);
                var terrainData = CreateTerrain(manifest, exportPath, dataDir, projectRoot);
                var terrainGO = Terrain.CreateTerrainGameObject(terrainData);
                terrainGO.name = "TerrainRoot";
                terrainGO.transform.position = new Vector3(
                    -manifest.terrain.terrain_width_m / 2f,
                    0f,
                    -manifest.terrain.terrain_length_m / 2f
                );

                // 6. Stitch and apply aerial texture
                EditorUtility.DisplayProgressBar("Importing Hole", "Stitching satellite texture...", 0.4f);
                ApplyAerialTexture(terrainData, tilesData, manifest, exportPath, dataDir, projectRoot);

                // 7. Build hierarchy
                EditorUtility.DisplayProgressBar("Importing Hole", "Building hierarchy...", 0.6f);

                // HoleRoot
                var holeRoot = new GameObject("HoleRoot");
                var metadata = holeRoot.AddComponent<HoleMetadata>();
                metadata.courseId = manifest.course_id;
                metadata.holeNumber = manifest.hole_number;
                metadata.par = manifest.par;
                metadata.strokeIndex = manifest.stroke_index;
                metadata.championshipYards = manifest.championship_yards;
                metadata.reviewStatus = manifest.review_status;

                terrainGO.transform.SetParent(holeRoot.transform);

                // Anchors
                var anchorsRoot = new GameObject("Anchors");
                anchorsRoot.transform.SetParent(holeRoot.transform);

                var terrain = terrainGO.GetComponent<Terrain>();
                foreach (var anchor in anchors)
                {
                    PlaceAnchorMarker(anchor, terrain, terrainGO.transform, anchorsRoot.transform);
                }

                // DebugReferences (empty, for future overlays)
                var debugRefs = new GameObject("DebugReferences");
                debugRefs.transform.SetParent(holeRoot.transform);

                // 8. Walk camera
                EditorUtility.DisplayProgressBar("Importing Hole", "Setting up camera...", 0.8f);
                CreateWalkCamera(anchors, terrain, terrainGO.transform);

                // 9. Directional light
                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.84f); // warm sunlight
                light.intensity = 1.2f;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

                // 10. Save scene and assets
                EditorUtility.DisplayProgressBar("Importing Hole", "Saving scene...", 0.9f);
                EditorSceneManager.SaveScene(scene, scenePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.Log($"[HoleImporter] Hole {holeId} imported to {scenePath}");
                EditorUtility.DisplayDialog("Import Complete",
                    $"Hole {holeId} imported successfully!\n\nScene: {scenePath}\n\n" +
                    $"Terrain: {manifest.terrain.terrain_width_m:F0}m x {manifest.terrain.terrain_length_m:F0}m\n" +
                    $"Anchors: {anchors.Length}\n" +
                    $"Par {manifest.par}, {manifest.championship_yards}yd",
                    "OK");
            }
            catch (System.Exception ex)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError($"[HoleImporter] Import failed: {ex}");
                EditorUtility.DisplayDialog("Import Error", ex.Message, "OK");
            }
        }

        private static TerrainData CreateTerrain(HoleManifest manifest, string exportPath,
            string dataDir, string projectRoot)
        {
            int res = manifest.terrain.resolution; // 129
            float elevRange = manifest.terrain.max_elevation_m - manifest.terrain.min_elevation_m;

            // Read heightmap.raw — uint16 big-endian
            string heightmapPath = Path.Combine(exportPath, manifest.terrain.heightmap_file);
            byte[] rawBytes = File.ReadAllBytes(heightmapPath);
            float[,] heights = new float[res, res];

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int idx = (y * res + x) * 2;
                    ushort val = (ushort)((rawBytes[idx] << 8) | rawBytes[idx + 1]);
                    // Diagnostic showed: bump at NE, red square at NW
                    // heights[row, col] — Unity docs say [z, x] but empirically
                    // col=0 maps to east (max X). Swap indices to fix.
                    heights[res - 1 - x, res - 1 - y] = val / 65535f;
                }
            }

            // Create TerrainData asset
            var terrainData = new TerrainData();
            terrainData.heightmapResolution = res;
            terrainData.size = new Vector3(
                manifest.terrain.terrain_width_m,
                elevRange,
                manifest.terrain.terrain_length_m
            );
            terrainData.SetHeights(0, 0, heights);

            // Save as asset so it persists with the scene
            string terrainAssetPath = $"{dataDir}/TerrainData_Hole01.asset";
            EnsureDirectory(Path.Combine(projectRoot, Path.GetDirectoryName(terrainAssetPath)));
            AssetDatabase.CreateAsset(terrainData, terrainAssetPath);

            return terrainData;
        }

        private static void ApplyAerialTexture(TerrainData terrainData, AerialTilesData tilesData,
            HoleManifest manifest, string exportPath, string dataDir, string projectRoot)
        {
            var tiles = tilesData.tiles;
            if (tiles == null || tiles.Length == 0)
            {
                UnityEngine.Debug.LogWarning("[HoleImporter] No aerial tiles found");
                return;
            }

            // Load all tile textures into a lookup: "x_y" → Color[256*256]
            var tilePixels = new System.Collections.Generic.Dictionary<string, Color[]>();
            foreach (var tile in tiles)
            {
                string tilePath = Path.Combine(exportPath,
                    tile.path.Replace("/", Path.DirectorySeparatorChar.ToString()));
                tilePath = Path.GetFullPath(tilePath);
                if (!File.Exists(tilePath))
                {
                    UnityEngine.Debug.LogWarning($"[HoleImporter] Tile not found: {tilePath}");
                    continue;
                }

                var tileTex = new Texture2D(256, 256);
                tileTex.LoadImage(File.ReadAllBytes(tilePath));
                tilePixels[$"{tile.x}_{tile.y}"] = tileTex.GetPixels();
                Object.DestroyImmediate(tileTex);
            }

            // Output texture resolution
            int texRes = 512;

            // Hole bounds = terrain bounds
            double holeNorth = manifest.bounds.north;
            double holeSouth = manifest.bounds.south;
            double holeEast = manifest.bounds.east;
            double holeWest = manifest.bounds.west;

            var outputTex = new Texture2D(texRes, texRes, TextureFormat.RGB24, false);
            var outputPixels = new Color[texRes * texRes];
            Color defaultColor = new Color(0.1f, 0.3f, 0.1f);

            for (int py = 0; py < texRes; py++)
            {
                for (int px = 0; px < texRes; px++)
                {
                    // Map output pixel to geographic coordinates
                    // Texture pixel (px=0, py=0) = bottom-left in Unity = (U=0, V=0)
                    // TerrainLayer: U=0 → min X (west), V=0 → min Z (north after row flip)
                    // So py=0 (bottom) = V=0 = north, py=max (top) = V=1 = south
                    float u = (float)px / (texRes - 1); // 0=west, 1=east
                    float v = (float)py / (texRes - 1); // 0=north, 1=south

                    double lon = holeWest + u * (holeEast - holeWest);
                    double lat = holeNorth - v * (holeNorth - holeSouth); // v=0→north, v=1→south

                    // Find which tile contains this lat/lon
                    Color sampled = defaultColor;
                    foreach (var tile in tiles)
                    {
                        if (lon >= tile.bounds.west && lon < tile.bounds.east &&
                            lat <= tile.bounds.north && lat > tile.bounds.south)
                        {
                            string key = $"{tile.x}_{tile.y}";
                            if (!tilePixels.ContainsKey(key)) break;

                            // Fractional position within tile
                            float fx = (float)((lon - tile.bounds.west) / (tile.bounds.east - tile.bounds.west));
                            float fy = (float)((tile.bounds.north - lat) / (tile.bounds.north - tile.bounds.south));
                            int tileCol = Mathf.Clamp((int)(fx * 256), 0, 255);
                            int tileRow = Mathf.Clamp((int)(fy * 256), 0, 255);

                            // GetPixels() is bottom-to-top: pixels[0] = south-west
                            // tileRow 0 = north = top of image = high Y in pixel array
                            int pixelIdx = (255 - tileRow) * 256 + tileCol;
                            sampled = tilePixels[key][pixelIdx];
                            break;
                        }
                    }

                    outputPixels[py * texRes + px] = sampled;
                }
            }

            outputTex.SetPixels(outputPixels);
            outputTex.Apply();

            // Save texture
            string texturePath = $"{dataDir}/aerial_hole01.png";
            string fullTexPath = Path.Combine(projectRoot, texturePath);
            EnsureDirectory(Path.GetDirectoryName(fullTexPath));
            File.WriteAllBytes(fullTexPath, outputTex.EncodeToPNG());
            Object.DestroyImmediate(outputTex);

            AssetDatabase.ImportAsset(texturePath);
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            // TerrainLayer: texture covers terrain exactly, no offset needed
            var layer = new TerrainLayer();
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.tileSize = new Vector2(terrainData.size.x, terrainData.size.z);
            layer.tileOffset = Vector2.zero;

            string layerPath = $"{dataDir}/TerrainLayer_Aerial.asset";
            AssetDatabase.CreateAsset(layer, layerPath);
            terrainData.terrainLayers = new TerrainLayer[] { layer };
        }

        private static void PlaceAnchorMarker(AnchorData anchor, Terrain terrain,
            Transform terrainTransform, Transform parent)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = $"Anchor_{anchor.type}";
            marker.transform.localScale = new Vector3(2f, 5f, 2f);

            // Color by type
            var renderer = marker.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Standard"));
            if (anchor.type.Contains("back")) mat.color = Color.blue;
            else if (anchor.type.Contains("regular")) mat.color = Color.green;
            else if (anchor.type.Contains("front")) mat.color = Color.white;
            else if (anchor.type.Contains("ladies")) mat.color = Color.red;
            else mat.color = Color.yellow;
            renderer.sharedMaterial = mat;

            // Position at local coords, sample terrain height
            Vector3 worldPos = new Vector3(anchor.local.x, 0f, anchor.local.z);
            float terrainHeight = terrain.SampleHeight(worldPos);
            float terrainBase = terrainTransform.position.y;
            marker.transform.position = new Vector3(
                worldPos.x,
                terrainBase + terrainHeight + 5f, // +5 so cylinder base sits on surface
                worldPos.z
            );

            marker.transform.SetParent(parent);
        }

        private static void CreateWalkCamera(AnchorData[] anchors, Terrain terrain,
            Transform terrainTransform)
        {
            // Remove default camera if exists
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

            // Position at back tee
            var backTee = anchors.FirstOrDefault(a => a.type.Contains("back"));
            if (backTee != null)
            {
                Vector3 pos = new Vector3(backTee.local.x, 0f, backTee.local.z);
                float terrainHeight = terrain.SampleHeight(pos);
                float terrainBase = terrainTransform.position.y;
                camGO.transform.position = new Vector3(pos.x, terrainBase + terrainHeight + 2f, pos.z);

                // Look north (toward green, which is roughly north of tees)
                camGO.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
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
