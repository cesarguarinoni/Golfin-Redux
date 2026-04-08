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
        /// <summary>DEPRECATED — replaced by FairwayFringeMeters + dilation.</summary>
        public static int FairwayFringeRadius = 2;
        /// <summary>Width of fairway fringe border in meters.</summary>
        public static float FairwayFringeMeters = 1.5f;
        /// <summary>Width of fairway mow stripes in meters.</summary>
        public static float MowStripeWidth = 5f;

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

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating bunkers...", 0.5f);
                CreateZoneMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);

                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating greens...", 0.53f);
                CreateGreenMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);


                EditorUtility.DisplayProgressBar("Importing Hole (Lite)", "Creating water...", 0.59f);
                CreateWaterMeshes(terrainData, terrainGO, holeRoot.transform, exportPath, dataDir, projectRoot, holes);

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

                var lightGO = new GameObject("Directional Light");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.84f);
                light.intensity = 1.0f;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

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
            int res = manifest.terrain.resolution;
            int actualRes = 1025;

            // Elevation range must accommodate shore depression below Y=0
            // Total range = ShoreDepthMeters + small buffer for flat terrain variation
            float elevRange = ShoreDepthMeters + 1.0f;

            // Flat terrain height normalized: maps to world Y=0
            // terrainGO.position.y = -ShoreDepthMeters, so normalizedFlat * elevRange + (-ShoreDepthMeters) = 0
            // normalizedFlat = ShoreDepthMeters / elevRange
            float normalizedFlat = ShoreDepthMeters / elevRange;

            float[,] heights = new float[actualRes, actualRes];
            for (int z = 0; z < actualRes; z++)
                for (int x = 0; x < actualRes; x++)
                    heights[z, x] = normalizedFlat;

            string terrainAssetPath = $"{dataDir}/TerrainData_Hole{holeId}.asset";
            EnsureDirectory(Path.Combine(projectRoot, Path.GetDirectoryName(terrainAssetPath)));

            // Delete stale asset on re-import to prevent CreateAsset failure
            var existingTerrain = AssetDatabase.LoadAssetAtPath<TerrainData>(terrainAssetPath);
            if (existingTerrain != null)
                AssetDatabase.DeleteAsset(terrainAssetPath);

            var terrainData = new TerrainData();
            terrainData.heightmapResolution = actualRes;
            terrainData.alphamapResolution = 1024;  // high-res splatmap for smooth zone edges
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

                    // Offset Y so marker rests on terrain (pivot is at mesh center)
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
            byte[] grid = System.Convert.FromBase64String(zonesData.grid);
            int zoneW = zonesData.source_dimensions.width;
            int zoneH = zonesData.source_dimensions.height;

            Debug.Log($"[HoleLiteImporter] Zone grid: {zoneW}x{zoneH}, {grid.Length} bytes");

            // --- Compute fairway stripe direction (tee → green in alphamap coords) ---
            string anchorsPath = Path.Combine(exportPath, "anchors.json");
            Vector2 stripeDir = new Vector2(0, 1); // default: stripes along Z
            if (File.Exists(anchorsPath))
            {
                string anchJson = File.ReadAllText(anchorsPath);
                var anchWrap = JsonUtility.FromJson<AnchorArrayWrapper>(
                    "{\"items\":" + anchJson + "}");
                var anchs = anchWrap.items;

                var backTee = System.Array.Find(anchs, a => a.type.Contains("back"));

                // Get green centroid from greens.json
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
                    // Match terrain space: 90° CCW rotation (x,z) → (z,x)
                    Vector2 teePos = new Vector2(backTee.local.z, backTee.local.x);
                    Vector2 greenPos = new Vector2(greenCenter.z, greenCenter.x);
                    Vector2 dir = (greenPos - teePos).normalized;
                    if (dir.sqrMagnitude > 0.01f)
                        stripeDir = new Vector2(-dir.y, dir.x); // perpendicular to tee→green
                }
            }

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

            // --- 2b. Override zone grid edges with smoothed vector contours ---
            string fairwayContoursPath = Path.Combine(exportPath, "fairway-contours.json");
            if (File.Exists(fairwayContoursPath))
            {
                string fcJson = File.ReadAllText(fairwayContoursPath);
                var fcData = JsonUtility.FromJson<FairwayContoursFile>(fcJson);

                if (fcData.fairways != null)
                {
                    // Clear ALL fairway pixels — re-fill only what the smooth contour covers.
                    for (int i = 0; i < resampledZones.Length; i++)
                    {
                        if (resampledZones[i] == 1)
                            resampledZones[i] = 4; // revert to rough
                    }

                    foreach (var fw in fcData.fairways)
                    {
                        if (fw.contour == null || fw.contour.Length < 3) continue;
                        int n = fw.contour.Length;

                        float[] polyAX = new float[n];
                        float[] polyAY = new float[n];
                        float terrainXSize = terrainData.size.x;
                        float terrainZSize = terrainData.size.z;

                        float bminAX = float.MaxValue, bmaxAX = float.MinValue;
                        float bminAY = float.MaxValue, bmaxAY = float.MinValue;

                        for (int i = 0; i < n; i++)
                        {
                            float worldX = fw.contour[i].z; // 90° CCW rotation
                            float worldZ = fw.contour[i].x;
                            float ax = (worldX + terrainXSize / 2f) / terrainXSize * (alphaRes - 1);
                            float ay = (worldZ + terrainZSize / 2f) / terrainZSize * (alphaRes - 1);
                            polyAX[i] = ax;
                            polyAY[i] = ay;
                            if (ax < bminAX) bminAX = ax;
                            if (ax > bmaxAX) bmaxAX = ax;
                            if (ay < bminAY) bminAY = ay;
                            if (ay > bmaxAY) bmaxAY = ay;
                        }

                        int minAXi = Mathf.Max(0, Mathf.FloorToInt(bminAX));
                        int maxAXi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAX));
                        int minAYi = Mathf.Max(0, Mathf.FloorToInt(bminAY));
                        int maxAYi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAY));

                        RasterizePolygon(polyAX, polyAY, n,
                            resampledZones, alphaRes, alphaRes, 1,
                            minAXi, minAYi, maxAXi, maxAYi);
                    }

                    Debug.Log($"[HoleLiteImporter] Rasterized {fcData.fairways.Length} " +
                              $"smooth fairway contour(s) onto alphamap");
                }
            }

            // Load tee + semi-rough contours and rasterize
            string zoneContoursPath = Path.Combine(exportPath, "zone-contours.json");
            if (File.Exists(zoneContoursPath))
            {
                string zcJson = File.ReadAllText(zoneContoursPath);
                var zcData = JsonUtility.FromJson<ZoneContoursFile>(zcJson);

                // Tee boxes (zone 10)
                if (zcData.zones != null && zcData.zones.tee != null)
                {
                    for (int i = 0; i < resampledZones.Length; i++)
                        if (resampledZones[i] == 10) resampledZones[i] = 4;

                    foreach (var region in zcData.zones.tee)
                        RasterizeContour(region, resampledZones, alphaRes, terrainData, 10);
                }

                // Semi-rough (zone 3) — overlay smooth contours on top
                if (zcData.zones != null && zcData.zones.semi_rough != null && zcData.zones.semi_rough.Length > 0)
                {
                    foreach (var region in zcData.zones.semi_rough)
                        RasterizeContour(region, resampledZones, alphaRes, terrainData, 3);
                }
            }

            // --- 3b. Fairway fringe ring (dilation-based, smooth edges from vector contours) ---
            bool[] fairwayMask = new bool[alphaRes * alphaRes];
            for (int i = 0; i < resampledZones.Length; i++)
                fairwayMask[i] = (resampledZones[i] == 1);

            float metersPerPixel = Mathf.Max(terrainData.size.x, terrainData.size.z) / alphaRes;
            int fairwayFringePx = Mathf.Max(1, Mathf.RoundToInt(FairwayFringeMeters / metersPerPixel));

            bool[] dilatedFairway = DilateMask(fairwayMask, alphaRes, alphaRes, fairwayFringePx);

            bool[] fairwayFringeMask = new bool[alphaRes * alphaRes];
            for (int i = 0; i < alphaRes * alphaRes; i++)
            {
                if (dilatedFairway[i] && !fairwayMask[i])
                {
                    int zone = resampledZones[i];
                    if (zone == 3 || zone == 4 || zone == 5)
                        fairwayFringeMask[i] = true;
                }
            }

            // --- 4. Build raw alphamap ---
            int layerCount = 8;
            float[,,] alphamap = new float[alphaRes, alphaRes, layerCount];

            float terrainSizeX = terrainData.size.x;
            float terrainSizeZ = terrainData.size.z;

            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    int idx = ay * alphaRes + ax;
                    int layer;

                    if (fringeMask[idx])
                        layer = 2; // green fringe → semi-rough
                    else if (fairwayFringeMask[idx])
                        layer = 2; // fairway fringe → semi-rough
                    else
                    {
                        int zone = resampledZones[idx];
                        layer = ZoneToLayer(zone);

                        // Mow stripes on fairway
                        if (zone == 1)
                        {
                            float worldX = ((float)ax / (alphaRes - 1)) * terrainSizeX - terrainSizeX / 2f;
                            float worldZ = ((float)ay / (alphaRes - 1)) * terrainSizeZ - terrainSizeZ / 2f;
                            float proj = worldX * stripeDir.x + worldZ * stripeDir.y;
                            int band = Mathf.FloorToInt(proj / MowStripeWidth);
                            if (band % 2 != 0)
                                layer = 7; // dark fairway stripe
                        }
                    }

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
                1  => 0,  // fairway
                2  => 3,  // green → rough (mesh handles surface, fringe still generated)
                3  => 2,  // semi_rough
                4  => 3,  // rough
                5  => 3,  // trees → rough texture
                6  => 3,  // bunker → rough (mesh handles sand surface, prevents blur glow)
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

        /// <summary>
        /// Rasterize a smooth polygon onto a byte grid.
        /// For each pixel inside the polygon, sets grid[y * w + x] = value.
        /// Uses ray-casting (point-in-polygon) test.
        /// </summary>
        static void RasterizePolygon(float[] polyX, float[] polyZ, int polyCount,
            byte[] grid, int w, int h, byte value,
            int minAX, int minAY, int maxAX, int maxAY)
        {
            for (int ay = minAY; ay <= maxAY; ay++)
            {
                for (int ax = minAX; ax <= maxAX; ax++)
                {
                    float px = ax + 0.5f;
                    float py = ay + 0.5f;
                    bool inside = false;
                    for (int i = 0, j = polyCount - 1; i < polyCount; j = i++)
                    {
                        if ((polyZ[i] > py) != (polyZ[j] > py) &&
                            px < (polyX[j] - polyX[i]) * (py - polyZ[i]) /
                                 (polyZ[j] - polyZ[i]) + polyX[i])
                        {
                            inside = !inside;
                        }
                    }
                    if (inside)
                        grid[ay * w + ax] = value;
                }
            }
        }

        /// <summary>
        /// Convert a ZoneContourRegion from local meters to alphamap coords and rasterize.
        /// </summary>
        static void RasterizeContour(ZoneContourRegion region, byte[] grid,
            int alphaRes, TerrainData terrainData, byte zoneValue)
        {
            if (region.contour == null || region.contour.Length < 3) return;
            int n = region.contour.Length;

            float terrainX = terrainData.size.x;
            float terrainZ = terrainData.size.z;

            float[] polyAX = new float[n];
            float[] polyAY = new float[n];
            float bminAX = float.MaxValue, bmaxAX = float.MinValue;
            float bminAY = float.MaxValue, bmaxAY = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                float worldX = region.contour[i].z;
                float worldZ = region.contour[i].x;
                float ax = (worldX + terrainX / 2f) / terrainX * (alphaRes - 1);
                float ay = (worldZ + terrainZ / 2f) / terrainZ * (alphaRes - 1);
                polyAX[i] = ax;
                polyAY[i] = ay;
                if (ax < bminAX) bminAX = ax;
                if (ax > bmaxAX) bmaxAX = ax;
                if (ay < bminAY) bminAY = ay;
                if (ay > bmaxAY) bmaxAY = ay;
            }

            int minAXi = Mathf.Max(0, Mathf.FloorToInt(bminAX));
            int maxAXi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAX));
            int minAYi = Mathf.Max(0, Mathf.FloorToInt(bminAY));
            int maxAYi = Mathf.Min(alphaRes - 1, Mathf.CeilToInt(bmaxAY));

            RasterizePolygon(polyAX, polyAY, n,
                grid, alphaRes, alphaRes, zoneValue,
                minAXi, minAYi, maxAXi, maxAYi);
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
                // Apply 90° CCW rotation to contour vertices (same as anchors)
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

                // Bounding box of contour (for limiting hole-grid search)
                float cMinX = float.MaxValue, cMaxX = float.MinValue;
                float cMinZ = float.MaxValue, cMaxZ = float.MinValue;
                foreach (var v in worldContour)
                {
                    if (v.x < cMinX) cMinX = v.x;
                    if (v.x > cMaxX) cMaxX = v.x;
                    if (v.y < cMinZ) cMinZ = v.y;
                    if (v.y > cMaxZ) cMaxZ = v.y;
                }

                // Cut terrain using the MESH RIM directly.
                // Instead of a separate shrunk contour, we iterate the full
                // bounding box and cut any cell whose center falls inside the
                // rim polygon scaled to 90%.  This guarantees the terrain hole
                // tracks the mesh shape — no mismatch possible.
                var cutContour = new Vector2[worldContour.Length];
                for (int i = 0; i < worldContour.Length; i++)
                {
                    cutContour[i] = new Vector2(
                        centroidX + (worldContour[i].x - centroidX) * 0.90f,
                        centroidZ + (worldContour[i].y - centroidZ) * 0.90f);
                }

                // Search the FULL bounding box — let point-in-polygon do the shaping
                int hMinX = Mathf.Clamp(Mathf.FloorToInt((cMinX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
                int hMaxX = Mathf.Clamp(Mathf.CeilToInt((cMaxX - terrainPos.x) / terrainSize.x * holesRes), 0, holesRes - 1);
                int hMinZ = Mathf.Clamp(Mathf.FloorToInt((cMinZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);
                int hMaxZ = Mathf.Clamp(Mathf.CeilToInt((cMaxZ - terrainPos.z) / terrainSize.z * holesRes), 0, holesRes - 1);

                for (int hz = hMinZ; hz <= hMaxZ; hz++)
                {
                    for (int hx = hMinX; hx <= hMaxX; hx++)
                    {
                        float cellWorldX = ((hx + 0.5f) / holesRes) * terrainSize.x + terrainPos.x;
                        float cellWorldZ = ((hz + 0.5f) / holesRes) * terrainSize.z + terrainPos.z;

                        if (IsInsideContour(cellWorldX, cellWorldZ, cutContour))
                            holes[hz, hx] = false;
                    }
                }


                // --- Generate contour-shaped mesh ---
                float surfaceY = terrainBaseY + terrain.SampleHeight(
                    new Vector3(centroidX, 0, centroidZ));

                float bowlDepth = Mathf.Max(Mathf.Min(defaultDepth, 3f), 0.5f);

                var meshGO = CreateContourMesh(bunker.id, worldContour, centroidX, centroidZ,
                    surfaceY, bowlDepth, sandMat, terrain, terrainBaseY);
                meshGO.transform.SetParent(bunkersRoot.transform);

                // Add SurfaceMarker
                var marker = meshGO.AddComponent<Golfin.Course.SurfaceMarker>();
                marker.surfaceType = Golfin.Course.SurfaceType.Bunker;
            }

            // Copy bunkers.json to Assets
            string destPath = Path.Combine(projectRoot, dataDir, "bunkers.json");
            File.Copy(bunkersPath, destPath, true);
            AssetDatabase.ImportAsset($"{dataDir}/bunkers.json");

            Debug.Log($"[HoleLiteImporter] Created {bunkersFile.bunkers.Length} contour-based bunker(s)");
        }

        private static GameObject CreateContourMesh(int id, Vector2[] contour,
            float centroidX, float centroidZ, float surfaceY, float depth,
            Material sandMat, Terrain terrain, float terrainBaseY)
        {
            int n = contour.Length;
            if (n < 3)
            {
                Debug.LogWarning($"[HoleLiteImporter] Bunker {id}: contour has < 3 vertices, skipping");
                return new GameObject($"Bunker_{id}_SKIP");
            }

            // Ring layout: rim (100%) → inner (80%) → mid (50%) → deep (20%) → center
            float[] ringScales = { 1.0f, 0.80f, 0.50f, 0.20f };
            float[] ringDepths = { 0.0f, 0.0f, depth * 0.5f, depth * 0.9f };

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

                    // Rim ring (r==0): sample terrain height for seamless edge
                    if (r == 0)
                    {
                        float terrainH = terrain.SampleHeight(new Vector3(wx, 0, wz));
                        y = (terrainBaseY + terrainH) - surfaceY + 0.02f;
                    }
                    // Inner ring (r==1): also at terrain height, no offset
                    else if (r == 1)
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

            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;

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
                collarGO.AddComponent<MeshCollider>().sharedMesh = collarMesh;
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
            surfaceGO.AddComponent<MeshCollider>().sharedMesh = surfaceMesh;
            surfaceGO.transform.SetParent(parent.transform, false);

            Debug.Log($"[HoleLiteImporter] {zoneName} {id}: {n} contour verts, " +
                      $"collar={collarMat != null}, collarScale={collarScale:F2}");

            return parent;
        }

        // ─── Water Zone Meshes (Rasterized Quad + Alpha Mask) ─────────────

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

            float waterY = 0.05f; // slightly above flat terrain

            foreach (var water in waterFile.water)
            {
                if (string.IsNullOrEmpty(water.mask) || water.mask_width < 1 || water.mask_height < 1)
                    continue;

                // Decode mask
                byte[] maskBytes = System.Convert.FromBase64String(water.mask);
                int mw = water.mask_width;
                int mh = water.mask_height;

                if (maskBytes.Length != mw * mh)
                {
                    Debug.LogWarning($"[HoleLiteImporter] Water {water.id}: mask size mismatch " +
                                     $"({maskBytes.Length} != {mw}x{mh}={mw * mh}), skipping");
                    continue;
                }

                // Apply 90° CCW rotation to bbox (same as anchors/contours)
                // Pre-rotation bbox is in (x, z) = (width_axis, length_axis)
                // 90° CCW: worldX = local.z, worldZ = local.x
                float worldMinX = water.bbox.min_z;
                float worldMaxX = water.bbox.max_z;
                float worldMinZ = water.bbox.min_x;
                float worldMaxZ = water.bbox.max_x;

                float quadW = worldMaxX - worldMinX;
                float quadH = worldMaxZ - worldMinZ;
                float centerX = (worldMinX + worldMaxX) / 2f;
                float centerZ = (worldMinZ + worldMaxZ) / 2f;

                // --- Generate SDF texture for smooth edges ---
                // Signed distance field: inside water = high alpha, outside = low,
                // boundary = 0.5. Alpha cutoff at 0.5 produces smooth curves.

                // Step 1: Find edge pixels (water pixels with non-water 4-neighbor)
                var edgePixels = new System.Collections.Generic.List<int[]>();
                for (int my = 0; my < mh; my++)
                {
                    for (int mx = 0; mx < mw; mx++)
                    {
                        if (maskBytes[my * mw + mx] != 1) continue;
                        bool isEdge =
                            (mx == 0      || maskBytes[my * mw + (mx - 1)] == 0) ||
                            (mx == mw - 1 || maskBytes[my * mw + (mx + 1)] == 0) ||
                            (my == 0      || maskBytes[(my - 1) * mw + mx] == 0) ||
                            (my == mh - 1 || maskBytes[(my + 1) * mw + mx] == 0);
                        if (isEdge)
                            edgePixels.Add(new int[] { mx, my });
                    }
                }

                // Step 2: Compute min distance to any edge pixel per pixel
                // Positive inside, negative outside. Normalize to 0-1 with 0.5 = edge.
                float sdfSpread = 3.0f; // pixels of gradient on each side of edge
                float[] sdfValues = new float[mw * mh];

                for (int my = 0; my < mh; my++)
                {
                    for (int mx = 0; mx < mw; mx++)
                    {
                        float minDist = float.MaxValue;
                        foreach (var ep in edgePixels)
                        {
                            float dx = mx - ep[0];
                            float dy = my - ep[1];
                            float dist = Mathf.Sqrt(dx * dx + dy * dy);
                            if (dist < minDist) minDist = dist;
                        }

                        bool isInside = maskBytes[my * mw + mx] == 1;
                        float signedDist = isInside ? minDist : -minDist;

                        // Map to 0-1: 0.5 = boundary, 1.0 = deep inside, 0.0 = far outside
                        float alpha = Mathf.Clamp01(0.5f + signedDist / (2f * sdfSpread));
                        sdfValues[my * mw + mx] = alpha;
                    }
                }

                // Step 3: Build texture with 90° CCW rotation
                // transpose: mask(mx, my) → tex(my, mx)
                int texW = mh;
                int texH = mw;
                var tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                Color waterColor = new Color(0.18f, 0.40f, 0.58f);

                for (int my = 0; my < mh; my++)
                {
                    for (int mx = 0; mx < mw; mx++)
                    {
                        float alpha = sdfValues[my * mw + mx];
                        int tx = my;
                        int ty = mx;
                        tex.SetPixel(tx, ty, new Color(waterColor.r, waterColor.g, waterColor.b, alpha));
                    }
                }
                tex.Apply();

                // Save texture as asset
                string texPath = $"{dataDir}/WaterMask_{water.id}.png";
                string fullTexPath = Path.Combine(projectRoot, texPath);
                File.WriteAllBytes(fullTexPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);

                AssetDatabase.ImportAsset(texPath);

                // Configure texture importer
                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer != null)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.alphaIsTransparency = true;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.maxTextureSize = 4096;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.SaveAndReimport();
                }

                var savedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                // --- Create material (alpha cutout) ---
                string matPath = $"{dataDir}/WaterSurface_{water.id}.mat";
                var existingMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (existingMat != null)
                    AssetDatabase.DeleteAsset(matPath);

                var mat = new Material(GetLitShader());
                mat.name = $"WaterSurface_{water.id}";
                mat.mainTexture = savedTex;

                // Alpha cutout mode
                mat.SetFloat("_Surface", 0); // 0 = Opaque — we use cutout via AlphaClip
                mat.SetFloat("_AlphaClip", 1);
                mat.SetFloat("_Cutoff", 0.5f);
                mat.SetFloat("_Smoothness", 0.85f);
                mat.SetFloat("_Metallic", 0.05f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = 2450; // AlphaTest queue

                AssetDatabase.CreateAsset(mat, matPath);

                // --- Create quad mesh ---
                var vertices = new Vector3[]
                {
                    new Vector3(-quadW / 2f, 0f, -quadH / 2f),
                    new Vector3( quadW / 2f, 0f, -quadH / 2f),
                    new Vector3( quadW / 2f, 0f,  quadH / 2f),
                    new Vector3(-quadW / 2f, 0f,  quadH / 2f),
                };
                var uvs = new Vector2[]
                {
                    new Vector2(0, 0),
                    new Vector2(1, 0),
                    new Vector2(1, 1),
                    new Vector2(0, 1),
                };
                var triangles = new int[] { 0, 2, 1, 0, 3, 2 };

                var mesh = new Mesh();
                mesh.name = $"WaterQuad_{water.id}";
                mesh.vertices = vertices;
                mesh.triangles = triangles;
                mesh.uv = uvs;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                var go = new GameObject($"Water_{water.id}");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;

                // MeshCollider uses the same quad — ball collision covers full bbox,
                // gameplay logic uses SurfaceMarker + zone lookup for precision
                go.AddComponent<MeshCollider>().sharedMesh = mesh;

                go.transform.position = new Vector3(centerX, waterY, centerZ);

                var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
                marker.surfaceType = Golfin.Course.SurfaceType.Water;

                go.transform.SetParent(waterRoot.transform);

                Debug.Log($"[HoleLiteImporter] Water {water.id}: quad {quadW:F1}x{quadH:F1}m, " +
                          $"mask {texW}x{texH}px, pos ({centerX:F1}, {waterY}, {centerZ:F1})");
            }

            // ─── Shore slope pass: depress terrain near water edges ──────────
            if (ShoreRadius > 0 && ShoreDepthMeters > 0f)
            {
                int hRes = terrainData.heightmapResolution;
                float[,] heights = terrainData.GetHeights(0, 0, hRes, hRes);
                float elevRange = terrainData.size.y;
                float normalizedFlat = ShoreDepthMeters / elevRange;

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

                // Apply depression
                int depressedCount = 0;
                for (int z = 0; z < hRes; z++)
                {
                    for (int x = 0; x < hRes; x++)
                    {
                        if (isWater[z, x]) continue; // don't touch water cells
                        float dist = distToWater[z, x];
                        if (dist > ShoreRadius) continue;

                        // Smoothstep falloff: 1.0 at water edge → 0.0 at radius
                        float t = dist / ShoreRadius;
                        float blend = 1f - (t * t * (3f - 2f * t));

                        // Target: water-level height in normalized space
                        // Water is at world Y=0.05, terrain base at -ShoreDepthMeters
                        // Normalized water height = (0.05 + ShoreDepthMeters) / elevRange
                        float normalizedWaterH = (0.05f + ShoreDepthMeters) / elevRange;
                        // We want shore to dip most of the way to water but not below it
                        float targetH = Mathf.Lerp(normalizedFlat, normalizedWaterH, 0.85f);

                        heights[z, x] = Mathf.Lerp(normalizedFlat, targetH, blend);
                        depressedCount++;
                    }
                }

                // Also depress water cells themselves to below water surface
                // so terrain doesn't poke through the water quad
                float normalizedUnderwater = (ShoreDepthMeters - 0.5f) / elevRange;
                // Clamp to minimum 0 (can't go below terrain bounds)
                normalizedUnderwater = Mathf.Max(0f, normalizedUnderwater);
                for (int z = 0; z < hRes; z++)
                {
                    for (int x = 0; x < hRes; x++)
                    {
                        if (isWater[z, x])
                            heights[z, x] = normalizedUnderwater;
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

            Debug.Log($"[HoleLiteImporter] Created {waterFile.water.Length} water quad(s)");
        }

        private static void EnsureDirectory(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}
#endif
