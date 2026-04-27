#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.CourseImport
{
    public static class BuildExperimentalHole01
    {
        const string SRC_SCENE      = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string EXP_SCENE_DIR  = "Assets/Golf/Courses/lomond-country-club/Generated/Experimental";
        const string EXP_SCENE      = "Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity";
        const string DATA_SRC       = "Assets/Golf/Courses/lomond-country-club/Data/hole-01-flat";
        const string DATA_DST       = "Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental";
        const string EXP_TEX_DIR    = "Assets/Courses/Textures_Experimental";
        const string REPORT_DIR     = "Docs/Diagnostics/texture-experiment";
        const string REPORT_PATH    = "Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md";

        static readonly string[] ExpectedTextures = new[]
        {
            "T_Green_Albedo.jpg",        "T_Green_Normal.jpg",
            "T_Fairway_Mix.jpg",         "T_Fairway_Normal.jpg",
            "T_Fairway_Light.jpg",       "T_Fairway_Dark.jpg",
            "T_Fringe_Albedo.jpg",       "T_Fringe_Normal.jpg",
            "T_Semirough_Albedo.jpg",    "T_Semirough_Normal.jpg",
            "T_Rough_Albedo.jpg",        "T_Rough_Normal.jpg",
            "T_Bunker_Albedo.jpg",       "T_Bunker_Normal.jpg",
            "T_BunkerDark_Albedo.jpg",
            "T_Tee_Albedo.jpg",          "T_Tee_Normal.jpg",
            "T_TeeDark_Albedo.jpg",      "T_TeeDark_Normal.jpg",
            "T_TeeDark_Albedo_NoBorder.jpg", "T_TeeDark_Albedo_NoBorder_Normal.jpg",
            "T_OOB_Albedo.jpg",          "T_OOB_Normal.jpg",
            "T_RoadAsphalt_Albedo.jpg",  "T_RoadAsphalt_Normal.jpg",
        };

        [MenuItem("GOLFIN/Tools/Build Hole_01 Experimental Clone")]
        public static void Build()
        {
            var report = new List<string>();
            var warnings = new List<string>();

            // ── Pre-flight ────────────────────────────────────────────────────────
            // 1. Experimental textures present?
            foreach (var fn in ExpectedTextures)
            {
                if (!File.Exists(Path.Combine(EXP_TEX_DIR, fn)))
                {
                    EditorUtility.DisplayDialog("Build Experimental Hole",
                        $"Missing experimental texture: {fn}\n\nRun Tools/TextureExperiment/prepare-textures.mjs first.",
                        "OK");
                    return;
                }
            }
            report.Add("## Pre-flight");
            report.Add($"- All {ExpectedTextures.Length} experimental textures found in `{EXP_TEX_DIR}`");

            // 2. Source scene exists?
            if (!File.Exists(SRC_SCENE))
            {
                EditorUtility.DisplayDialog("Build Experimental Hole",
                    $"Source scene not found: {SRC_SCENE}\n\nRun HoleGeoImporter on hole 01 first.",
                    "OK");
                return;
            }
            report.Add($"- Source scene: `{SRC_SCENE}` ✓");

            // 3. Output paths — delete and rebuild if they already exist (no dialog)
            bool expSceneExists  = File.Exists(EXP_SCENE);
            bool dataDstExists   = Directory.Exists(DATA_DST);
            if (expSceneExists || dataDstExists)
            {
                Debug.Log("[BuildExperimentalHole01] Output paths exist — deleting and rebuilding.");
                if (expSceneExists)
                    AssetDatabase.DeleteAsset(EXP_SCENE);
                if (dataDstExists)
                    AssetDatabase.DeleteAsset(DATA_DST);
                AssetDatabase.Refresh();
            }

            // ── Create output folders ─────────────────────────────────────────────
            EnsureFolder("Assets/Golf/Courses/lomond-country-club/Generated", "Experimental");
            EnsureFolder("Assets/Golf/Courses/lomond-country-club", "Data");
            EnsureFolder("Assets/Golf/Courses/lomond-country-club/Data", "hole-01-experimental");
            report.Add("");
            report.Add("## Folders created");
            report.Add($"- `{EXP_SCENE_DIR}`");
            report.Add($"- `{DATA_DST}`");

            // ── Duplicate scene ───────────────────────────────────────────────────
            bool copied = AssetDatabase.CopyAsset(SRC_SCENE, EXP_SCENE);
            if (!copied)
            {
                Debug.LogError($"[BuildExperimentalHole01] Failed to copy scene to {EXP_SCENE}");
                return;
            }
            AssetDatabase.ImportAsset(EXP_SCENE, ImportAssetOptions.ForceUpdate);
            report.Add("");
            report.Add("## Scene duplicated");
            report.Add($"- `{SRC_SCENE}` → `{EXP_SCENE}`");

            // ── Open experimental scene additively ────────────────────────────────
            Scene expScene = EditorSceneManager.OpenScene(EXP_SCENE, OpenSceneMode.Additive);

            // ── Find Terrain ──────────────────────────────────────────────────────
            Terrain terrain = null;
            var meshRenderers = new List<MeshRenderer>();

            foreach (var root in expScene.GetRootGameObjects())
            {
                if (terrain == null)
                    terrain = root.GetComponentInChildren<Terrain>(true);

                foreach (var mr in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (var mat in mr.sharedMaterials)
                    {
                        if (mat != null && mat.name.StartsWith("MAT_T_"))
                        {
                            meshRenderers.Add(mr);
                            break;
                        }
                    }
                }
            }

            if (terrain == null)
            {
                warnings.Add("WARNING: No Terrain component found in experimental scene.");
                Debug.LogWarning("[BuildExperimentalHole01] No Terrain found.");
            }

            // ── Duplicate + rewire TerrainData ────────────────────────────────────
            report.Add("");
            report.Add("## TerrainData & TerrainLayers");

            if (terrain != null && terrain.terrainData != null)
            {
                string srcTdPath = AssetDatabase.GetAssetPath(terrain.terrainData);
                string dstTdPath = $"{DATA_DST}/TerrainData_Hole_01_Experimental.asset";

                AssetDatabase.CopyAsset(srcTdPath, dstTdPath);
                var expTd = AssetDatabase.LoadAssetAtPath<TerrainData>(dstTdPath);
                report.Add($"- TerrainData: `{srcTdPath}` → `{dstTdPath}`");

                // Duplicate each terrain layer and swap textures
                TerrainLayer[] srcLayers = expTd.terrainLayers;
                var newLayers = new TerrainLayer[srcLayers.Length];

                for (int i = 0; i < srcLayers.Length; i++)
                {
                    var srcLayer = srcLayers[i];
                    if (srcLayer == null)
                    {
                        newLayers[i] = null;
                        warnings.Add($"WARNING: TerrainLayer slot {i} is null — skipped.");
                        continue;
                    }

                    string srcLayerPath = AssetDatabase.GetAssetPath(srcLayer);
                    string layerFileName = Path.GetFileNameWithoutExtension(srcLayerPath) + "_Experimental.asset";
                    string dstLayerPath  = $"{DATA_DST}/{layerFileName}";

                    AssetDatabase.CopyAsset(srcLayerPath, dstLayerPath);
                    var expLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dstLayerPath);

                    // Swap diffuse texture
                    if (expLayer.diffuseTexture != null)
                    {
                        string prodTexPath = AssetDatabase.GetAssetPath(expLayer.diffuseTexture);
                        string prodTexFile = Path.GetFileName(prodTexPath);
                        string expTexPath  = $"{EXP_TEX_DIR}/{prodTexFile}";
                        var expTex = AssetDatabase.LoadAssetAtPath<Texture2D>(expTexPath);
                        if (expTex != null)
                        {
                            expLayer.diffuseTexture = expTex;
                            report.Add($"  - Layer {i} albedo: `{prodTexFile}` → experimental");
                        }
                        else
                        {
                            warnings.Add($"WARNING: No experimental albedo for `{prodTexFile}` (layer {i} `{srcLayer.name}`). Left as production.");
                        }
                    }

                    // Swap normal texture
                    if (expLayer.normalMapTexture != null)
                    {
                        string prodNormPath = AssetDatabase.GetAssetPath(expLayer.normalMapTexture);
                        string prodNormFile = Path.GetFileName(prodNormPath);
                        string expNormPath  = $"{EXP_TEX_DIR}/{prodNormFile}";
                        var expNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(expNormPath);
                        if (expNorm != null)
                        {
                            expLayer.normalMapTexture = expNorm;
                            report.Add($"  - Layer {i} normal: `{prodNormFile}` → experimental");
                        }
                        else
                        {
                            warnings.Add($"WARNING: No experimental normal for `{prodNormFile}` (layer {i} `{srcLayer.name}`). Left as production.");
                        }
                    }

                    EditorUtility.SetDirty(expLayer);
                    newLayers[i] = expLayer;
                    report.Add($"  - Duplicated: `{Path.GetFileName(srcLayerPath)}` → `{layerFileName}`");
                }

                expTd.terrainLayers = newLayers;
                EditorUtility.SetDirty(expTd);

                // Repoint terrain component at duplicated data
                terrain.terrainData = expTd;
                EditorUtility.SetDirty(terrain);
            }
            else
            {
                warnings.Add("WARNING: Terrain or TerrainData null — TerrainLayer duplication skipped.");
            }

            // ── Duplicate + rewire overlay materials ──────────────────────────────
            report.Add("");
            report.Add("## Overlay materials");

            var duplicatedMats = new Dictionary<string, Material>(); // srcPath → expMat

            foreach (var mr in meshRenderers)
            {
                var srcMats  = mr.sharedMaterials;
                var newMats  = new Material[srcMats.Length];

                for (int j = 0; j < srcMats.Length; j++)
                {
                    var srcMat = srcMats[j];
                    if (srcMat == null) { newMats[j] = null; continue; }

                    string srcMatPath = AssetDatabase.GetAssetPath(srcMat);

                    if (!srcMat.name.StartsWith("MAT_T_"))
                    {
                        newMats[j] = srcMat; // not a terrain overlay — keep as-is
                        continue;
                    }

                    if (duplicatedMats.TryGetValue(srcMatPath, out var already))
                    {
                        newMats[j] = already;
                        continue;
                    }

                    string matFileName = Path.GetFileNameWithoutExtension(srcMatPath) + "_Experimental.mat";
                    string dstMatPath  = $"{DATA_DST}/{matFileName}";

                    AssetDatabase.CopyAsset(srcMatPath, dstMatPath);
                    var expMat = AssetDatabase.LoadAssetAtPath<Material>(dstMatPath);

                    // Swap _BaseMap / _MainTex
                    SwapMatTex(expMat, "_BaseMap",  report, warnings);
                    SwapMatTex(expMat, "_MainTex",  report, warnings);
                    // Swap _BumpMap
                    SwapMatTex(expMat, "_BumpMap",  report, warnings);

                    EditorUtility.SetDirty(expMat);
                    duplicatedMats[srcMatPath] = expMat;
                    newMats[j] = expMat;
                    report.Add($"  - `{Path.GetFileName(srcMatPath)}` → `{matFileName}`");
                }

                mr.sharedMaterials = newMats;
                EditorUtility.SetDirty(mr);
            }

            // Capture counts before CloseScene destroys scene objects
            int layerCount = (terrain != null && terrain.terrainData != null) ? terrain.terrainData.terrainLayers.Length : 0;

            // ── Save and unload experimental scene ────────────────────────────────
            EditorSceneManager.SaveScene(expScene, EXP_SCENE);
            EditorSceneManager.CloseScene(expScene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ── Write report ──────────────────────────────────────────────────────
            Directory.CreateDirectory(REPORT_DIR);

            var sb = new StringBuilder();
            sb.AppendLine("# HOLE01_CLONE_REPORT");
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            foreach (var line in report)
                sb.AppendLine(line);

            if (warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## Warnings");
                foreach (var w in warnings)
                    sb.AppendLine($"- {w}");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("## Warnings");
                sb.AppendLine("- None.");
            }

            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- TerrainLayers duplicated: {layerCount}");
            sb.AppendLine($"- Overlay materials duplicated: {duplicatedMats.Count}");
            sb.AppendLine($"- Warnings: {warnings.Count}");

            File.WriteAllText(REPORT_PATH, sb.ToString());

            Debug.Log($"[BuildExperimentalHole01] Done. Layers={layerCount} Mats={duplicatedMats.Count} Warnings={warnings.Count} Report={REPORT_PATH}");
        }

        static void SwapMatTex(Material mat, string slot, List<string> report, List<string> warnings)
        {
            if (!mat.HasProperty(slot)) return;
            var tex = mat.GetTexture(slot) as Texture2D;
            if (tex == null) return;

            string prodTexPath = AssetDatabase.GetAssetPath(tex);
            string prodTexFile = Path.GetFileName(prodTexPath);
            string expTexPath  = $"{EXP_TEX_DIR}/{prodTexFile}";
            var expTex = AssetDatabase.LoadAssetAtPath<Texture2D>(expTexPath);
            if (expTex != null)
            {
                mat.SetTexture(slot, expTex);
            }
            else
            {
                warnings.Add($"WARNING: No experimental texture for `{prodTexFile}` (slot {slot} on `{mat.name}`). Left as production.");
            }
        }

        static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
