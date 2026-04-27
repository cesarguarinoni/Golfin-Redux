#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.CourseImport
{
    public static class BuildExperimentalHole01
    {
        const string SRC_SCENE         = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string EXP_SCENE_DIR     = "Assets/Golf/Courses/lomond-country-club/Generated/Experimental";
        const string EXP_SCENE         = "Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity";
        const string DATA_DST          = "Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental";
        const string SHARED_MAT_ROOT   = "Assets/Courses/Materials (Shared by courses)";
        const string SHARED_MAT_EXP    = "Assets/Courses/Materials (Shared by courses)/Experimental";
        const string EXP_TEX_DIR       = "Assets/Courses/Textures_Experimental";
        const string REPORT_DIR        = "Docs/Diagnostics/texture-experiment";
        const string REPORT_PATH       = "Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md";

        // Pattern for shared course materials.
        // Covers both MAT_-prefixed names and the per-scene legacy names BunkerSand / GreenSurface.
        static readonly Regex SharedMatPattern = new Regex(
            @"^(MAT_(Bunkers|Green|Fringe|Tee|Fairway|Rough|Semirough|Road|OOB)(_Dark)?|BunkerSand|GreenSurface)$",
            RegexOptions.Compiled);

        // All expected experimental textures (25 files)
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

        // Normal map files that need textureType=NormalMap import settings
        static readonly string[] NormalFiles = new[]
        {
            "T_Fairway_Normal.jpg",
            "T_Green_Normal.jpg",
            "T_Fringe_Normal.jpg",
            "T_Semirough_Normal.jpg",
            "T_Rough_Normal.jpg",
            "T_Bunker_Normal.jpg",
            "T_Tee_Normal.jpg",
            "T_TeeDark_Normal.jpg",
            "T_TeeDark_Albedo_NoBorder_Normal.jpg",
            "T_OOB_Normal.jpg",
            "T_RoadAsphalt_Normal.jpg",
        };

        [MenuItem("GOLFIN/Tools/Build Hole_01 Experimental Clone")]
        public static void Build()
        {
            var report   = new List<string>();
            var warnings = new List<string>();

            // ── Pre-flight ────────────────────────────────────────────────────────
            foreach (var fn in ExpectedTextures)
            {
                if (!File.Exists(Path.Combine(EXP_TEX_DIR, fn)))
                {
                    Debug.LogError($"[BuildExperimentalHole01] Missing: {EXP_TEX_DIR}/{fn} — run prepare-textures.mjs first.");
                    return;
                }
            }
            report.Add("## Pre-flight");
            report.Add($"- All {ExpectedTextures.Length} experimental textures found in `{EXP_TEX_DIR}`");

            if (!File.Exists(SRC_SCENE))
            {
                Debug.LogError($"[BuildExperimentalHole01] Source scene not found: {SRC_SCENE}");
                return;
            }
            report.Add($"- Source scene: `{SRC_SCENE}` ✓");

            // ── Pre-fetch source TerrainData path (before any cleanup) ───────────
            // The experimental scene may have a stale ref if a prior run deleted DATA_DST.
            // We record the source path here and use it as a fallback after opening the exp scene.
            string srcTerrainDataPath = null;
            {
                var srcSceneProbe = EditorSceneManager.OpenScene(SRC_SCENE, OpenSceneMode.Additive);
                foreach (var root in srcSceneProbe.GetRootGameObjects())
                {
                    var t = root.GetComponentInChildren<Terrain>(true);
                    if (t != null && t.terrainData != null)
                    {
                        srcTerrainDataPath = AssetDatabase.GetAssetPath(t.terrainData);
                        break;
                    }
                }
                EditorSceneManager.CloseScene(srcSceneProbe, true);
            }
            if (!string.IsNullOrEmpty(srcTerrainDataPath))
                report.Add($"- Source TerrainData: `{srcTerrainDataPath}`");
            else
                warnings.Add("WARNING: Could not locate TerrainData in source scene.");

            // ── Step 0: delete Phase 1 / previous run outputs ─────────────────────
            // EXP_SCENE_DIR is under Generated/ which is gitignored — use File/Directory API
            if (Directory.Exists(EXP_SCENE_DIR))
            {
                Debug.Log($"[BuildExperimentalHole01] Deleting gitignored scene dir: {EXP_SCENE_DIR}");
                Directory.Delete(EXP_SCENE_DIR, recursive: true);
            }
            // DATA_DST and SHARED_MAT_EXP are tracked — use AssetDatabase
            foreach (var p in new[] { DATA_DST, SHARED_MAT_EXP })
            {
                if (AssetDatabase.IsValidFolder(p))
                {
                    Debug.Log($"[BuildExperimentalHole01] Deleting tracked folder: {p}");
                    AssetDatabase.DeleteAsset(p);
                }
            }
            if (File.Exists(REPORT_PATH)) File.Delete(REPORT_PATH);
            AssetDatabase.Refresh();

            // ── Build filename→experimental-texture lookup ────────────────────────
            // NOTE: B.1 normal map reimport runs AFTER scene work (avoids disrupting AssetDatabase during clone)
            var texLookup = new Dictionary<string, Texture2D>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var fn in ExpectedTextures)
            {
                string assetPath = $"{EXP_TEX_DIR}/{fn}";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex != null)
                    texLookup[fn] = tex;
                else
                    warnings.Add($"WARNING: Experimental texture not loadable by AssetDatabase: `{fn}`");
            }

            // ── Create tracked output folders ─────────────────────────────────────
            // NOTE: EXP_SCENE_DIR is under Generated/ which is gitignored — don't use
            // AssetDatabase.CreateFolder for it; Directory.CreateDirectory handles it later
            // when we File.Copy the scene file after SaveScene.
            EnsureFolder("Assets/Golf/Courses/lomond-country-club", "Data");
            EnsureFolder("Assets/Golf/Courses/lomond-country-club/Data", "hole-01-experimental");
            EnsureFolder(SHARED_MAT_ROOT, "Experimental");
            report.Add("");
            report.Add("## Folders ready");
            report.Add($"- `{DATA_DST}`");
            report.Add($"- `{SHARED_MAT_EXP}`");

            // ── Open SOURCE scene additively — all swaps happen in-memory ─────────
            // WHY: opening a previously-copied experimental scene causes Unity's artifact
            // cache to serve stale/null material refs from prior runs, even after File.Copy
            // overwrites the .unity on disk. Opening the source (stable GUID, clean cache)
            // is the only reliable way to get valid in-memory refs.
            Scene expScene = EditorSceneManager.OpenScene(SRC_SCENE, OpenSceneMode.Additive);

            // ── Find Terrain + ALL MeshRenderers ──────────────────────────────────
            Terrain terrain = null;
            var allRenderers = new List<MeshRenderer>();

            foreach (var root in expScene.GetRootGameObjects())
            {
                if (terrain == null)
                    terrain = root.GetComponentInChildren<Terrain>(true);
                allRenderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(true));
            }

            // ── Heal broken TerrainData ref (stale from a prior run's cleanup) ────
            // If a previous run wrote the experimental scene and DATA_DST was later deleted,
            // terrain.terrainData will be null even though the terrain GO exists.
            if (terrain != null && terrain.terrainData == null && !string.IsNullOrEmpty(srcTerrainDataPath))
            {
                var srcTd = AssetDatabase.LoadAssetAtPath<TerrainData>(srcTerrainDataPath);
                if (srcTd != null)
                {
                    terrain.terrainData = srcTd;
                    report.Add($"- TerrainData ref healed from source: `{srcTerrainDataPath}`");
                }
                else
                {
                    warnings.Add($"WARNING: Could not load source TerrainData at `{srcTerrainDataPath}` — terrain skipped.");
                }
            }

            // ── B.1b: Duplicate + rewire TerrainData ──────────────────────────────
            report.Add("");
            report.Add("## TerrainData & TerrainLayers");

            int layerCount = 0;
            if (terrain != null && terrain.terrainData != null)
            {
                string srcTdPath = AssetDatabase.GetAssetPath(terrain.terrainData);
                string dstTdPath = $"{DATA_DST}/TerrainData_Hole_01_Experimental.asset";

                AssetDatabase.CopyAsset(srcTdPath, dstTdPath);
                var expTd = AssetDatabase.LoadAssetAtPath<TerrainData>(dstTdPath);
                report.Add($"- TerrainData: `{srcTdPath}` → `{dstTdPath}`");

                TerrainLayer[] srcLayers = expTd.terrainLayers;
                var newLayers = new TerrainLayer[srcLayers.Length];
                layerCount = srcLayers.Length;

                for (int i = 0; i < srcLayers.Length; i++)
                {
                    var srcLayer = srcLayers[i];
                    if (srcLayer == null) { newLayers[i] = null; continue; }

                    string srcLayerPath  = AssetDatabase.GetAssetPath(srcLayer);
                    string layerFileName = Path.GetFileNameWithoutExtension(srcLayerPath) + "_Experimental.asset";
                    string dstLayerPath  = $"{DATA_DST}/{layerFileName}";

                    AssetDatabase.CopyAsset(srcLayerPath, dstLayerPath);
                    var expLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(dstLayerPath);
                    // NOTE: CopyAsset preserves m_NormalScale, m_SmoothnessSource, m_MaskMapTexture, m_TileSize

                    SwapLayerTex(expLayer, texLookup, i, report, warnings);
                    EditorUtility.SetDirty(expLayer);
                    newLayers[i] = expLayer;
                    report.Add($"  - Duplicated: `{Path.GetFileName(srcLayerPath)}` → `{layerFileName}`");
                }

                expTd.terrainLayers = newLayers;
                EditorUtility.SetDirty(expTd);
                terrain.terrainData = expTd;
                EditorUtility.SetDirty(terrain);
            }
            else
            {
                warnings.Add("WARNING: Terrain or TerrainData null — TerrainLayer duplication skipped.");
            }

            // ── B.2: Walk ALL MeshRenderers — duplicate + rewire materials ────────
            report.Add("");
            report.Add("## Overlay materials");
            report.Add($"- MeshRenderers walked: {allRenderers.Count}");

            var duplicatedMats = new Dictionary<string, Material>(); // srcAssetPath → expMat
            int sharedDupCount = 0, perHoleDupCount = 0, skippedCount = 0;
            var uniqueMats = new HashSet<string>();

            foreach (var mr in allRenderers)
            {
                var srcMats = mr.sharedMaterials;
                var newMats = new Material[srcMats.Length];

                for (int j = 0; j < srcMats.Length; j++)
                {
                    var srcMat = srcMats[j];
                    if (srcMat == null) { newMats[j] = null; continue; }

                    string srcMatPath = AssetDatabase.GetAssetPath(srcMat);
                    uniqueMats.Add(srcMatPath);

                    bool isPerHole  = srcMat.name.StartsWith("MAT_T_");
                    bool isShared   = SharedMatPattern.IsMatch(srcMat.name);

                    if (!isPerHole && !isShared)
                    {
                        newMats[j] = srcMat; // not a terrain overlay — keep as-is
                        skippedCount++;
                        continue;
                    }

                    if (duplicatedMats.TryGetValue(srcMatPath, out var already))
                    {
                        newMats[j] = already;
                        continue;
                    }

                    string destDir      = isPerHole ? DATA_DST : SHARED_MAT_EXP;
                    string matFileName  = Path.GetFileNameWithoutExtension(srcMatPath) + "_Experimental.mat";
                    string dstMatPath   = $"{destDir}/{matFileName}";

                    AssetDatabase.CopyAsset(srcMatPath, dstMatPath);
                    var expMat = AssetDatabase.LoadAssetAtPath<Material>(dstMatPath);

                    // Repoint texture slots — preserve all other properties (m_Scale, tints, floats)
                    SwapMatTex(expMat, "_BaseMap", texLookup, report, warnings);
                    SwapMatTex(expMat, "_MainTex",  texLookup, report, warnings);
                    SwapMatTex(expMat, "_BumpMap",  texLookup, report, warnings);

                    EditorUtility.SetDirty(expMat);
                    duplicatedMats[srcMatPath] = expMat;
                    newMats[j] = expMat;

                    if (isShared) sharedDupCount++;
                    else          perHoleDupCount++;

                    report.Add($"  - [{(isShared ? "shared" : "per-hole")}] `{Path.GetFileName(srcMatPath)}` → `{dstMatPath}`");
                }

                mr.sharedMaterials = newMats;
                EditorUtility.SetDirty(mr);
            }

            report.Add($"- Unique materials encountered: {uniqueMats.Count}");
            report.Add($"- Materials duplicated (shared): {sharedDupCount}");
            report.Add($"- Materials duplicated (per-hole): {perHoleDupCount}");
            report.Add($"- Material slots skipped (non-terrain): {skippedCount}");

            // ── Acceptance gate: required stems ──────────────────────────────────
            var requiredStems = new[] { "Bunkers", "Green", "Fringe", "Tee", "Fairway" };
            foreach (var stem in requiredStems)
            {
                bool found = false;
                foreach (var key in duplicatedMats.Keys)
                    if (Path.GetFileNameWithoutExtension(key).Contains(stem)) { found = true; break; }
                if (!found)
                    warnings.Add($"ACCEPTANCE GATE FAIL: Material stem `{stem}` not found in duplicated set — investigate MeshRenderer discovery.");
            }

            // ── Save + unload scene ───────────────────────────────────────────────
            // AssetDatabase.SaveScene refuses to write to gitignored paths (Generated/).
            // Workaround: save to a tracked temp path, then File.Copy to the gitignored destination.
            // Don't copy the .meta — let Unity assign a fresh GUID on next Refresh.
            string tempScenePath = DATA_DST + "/_TempExperimentalScene.unity";
            bool sceneSaved = EditorSceneManager.SaveScene(expScene, tempScenePath);
            EditorSceneManager.CloseScene(expScene, true);
            AssetDatabase.SaveAssets();
            report.Add($"- Scene saved to temp: {sceneSaved}");

            if (sceneSaved && File.Exists(tempScenePath))
            {
                if (!Directory.Exists(EXP_SCENE_DIR)) Directory.CreateDirectory(EXP_SCENE_DIR);
                File.Copy(tempScenePath, EXP_SCENE, overwrite: true);
                AssetDatabase.DeleteAsset(tempScenePath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                report.Add($"- Experimental scene: `{EXP_SCENE}` (exists: {File.Exists(EXP_SCENE)})");
            }
            else
            {
                warnings.Add($"WARNING: SaveScene to temp path failed — scene not written to `{EXP_SCENE}`.");
                AssetDatabase.DeleteAsset(tempScenePath);
            }

            // ── B.1: Fix normal map import settings (after cloning, before final report) ─
            report.Add("");
            report.Add("## Normal map import fix");
            int normalsFixed = 0;
            foreach (var fn in NormalFiles)
            {
                string assetPath = $"{EXP_TEX_DIR}/{fn}";
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    warnings.Add($"WARNING: Could not get TextureImporter for `{fn}` — skipped.");
                    continue;
                }
                bool needsSave = importer.textureType != TextureImporterType.NormalMap
                              || importer.sRGBTexture;
                if (needsSave)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                    normalsFixed++;
                    report.Add($"  - `{fn}` → NormalMap, linear");
                }
            }
            report.Add($"- Normals fixed/reimported: {normalsFixed}/{NormalFiles.Length}");

            AssetDatabase.Refresh();

            // ── Write report ──────────────────────────────────────────────────────
            Directory.CreateDirectory(REPORT_DIR);

            var sb = new StringBuilder();
            sb.AppendLine("# HOLE01_CLONE_REPORT (Phase 2)");
            sb.AppendLine($"Generated: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            foreach (var line in report)
                sb.AppendLine(line);

            sb.AppendLine();
            sb.AppendLine("## Warnings");
            if (warnings.Count == 0)
                sb.AppendLine("- None.");
            else
                foreach (var w in warnings) sb.AppendLine($"- {w}");

            sb.AppendLine();
            sb.AppendLine("## Summary");
            sb.AppendLine($"- TerrainLayers duplicated: {layerCount}");
            sb.AppendLine($"- MeshRenderers walked: {allRenderers.Count}");
            sb.AppendLine($"- Overlay materials duplicated (shared): {sharedDupCount}");
            sb.AppendLine($"- Overlay materials duplicated (per-hole): {perHoleDupCount}");
            sb.AppendLine($"- Normal textures reimported as NormalMap: {normalsFixed}");
            sb.AppendLine($"- Warnings: {warnings.Count}");

            File.WriteAllText(REPORT_PATH, sb.ToString());
            Debug.Log($"[BuildExperimentalHole01] Done. Layers={layerCount} SharedMats={sharedDupCount} PerHoleMats={perHoleDupCount} Normals={normalsFixed} Warnings={warnings.Count} Report={REPORT_PATH}");
        }

        static void SwapLayerTex(TerrainLayer layer, Dictionary<string, Texture2D> lookup, int idx,
                                  List<string> report, List<string> warnings)
        {
            if (layer.diffuseTexture != null)
            {
                var exp = FindExpTex(AssetDatabase.GetAssetPath(layer.diffuseTexture), lookup);
                if (exp != null) { layer.diffuseTexture = exp; report.Add($"  - Layer {idx} albedo swapped"); }
                else warnings.Add($"WARNING: No exp albedo for layer {idx} `{layer.name}`");
            }
            if (layer.normalMapTexture != null)
            {
                var exp = FindExpTex(AssetDatabase.GetAssetPath(layer.normalMapTexture), lookup);
                if (exp != null) { layer.normalMapTexture = exp; report.Add($"  - Layer {idx} normal swapped"); }
                else warnings.Add($"WARNING: No exp normal for layer {idx} `{layer.name}`");
            }
        }

        static void SwapMatTex(Material mat, string slot, Dictionary<string, Texture2D> lookup,
                                List<string> report, List<string> warnings)
        {
            if (!mat.HasProperty(slot)) return;
            var tex = mat.GetTexture(slot) as Texture2D;
            if (tex == null) return;

            var exp = FindExpTex(AssetDatabase.GetAssetPath(tex), lookup);
            if (exp != null)
                mat.SetTexture(slot, exp);
            else
                warnings.Add($"WARNING: No exp texture for `{Path.GetFileName(AssetDatabase.GetAssetPath(tex))}` (slot {slot} on `{mat.name}`)");
        }

        static Texture2D FindExpTex(string prodPath, Dictionary<string, Texture2D> lookup)
        {
            if (string.IsNullOrEmpty(prodPath)) return null;
            string fn = Path.GetFileName(prodPath);
            lookup.TryGetValue(fn, out var result);
            return result;
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
