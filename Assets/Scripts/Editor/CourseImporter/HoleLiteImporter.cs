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
                ApplyTexture(terrainData, manifest, exportPath, dataDir, holeId, projectRoot);

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

        private static void ApplyTexture(TerrainData terrainData, HoleManifest manifest,
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

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
#endif
