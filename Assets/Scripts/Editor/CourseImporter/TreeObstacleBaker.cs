#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Bakes per-hole tree collision instances to Assets/Resources/HoleData/Hole_NN/tree_obstacles.csv.
    ///
    /// Harvests from three sources per scene:
    ///   1. terrain.terrainData.treeInstances  — terrain tree system (prototypeIndex → prefab name)
    ///   2. StandaloneTrees container children — GOs placed by TreePlacer
    ///   3. PaintedTrees container children    — GOs placed by TreeBrushTool
    ///
    /// Instance naming conventions:
    ///   TreePlacer standalone : {prefabName}_{n}
    ///   TreeBrushTool GO      : {prefabName}_brush_{n}
    ///   Suffix stripped via regex: remove _(brush_)?[0-9]+ at end of name.
    ///
    /// Profile name normalization: spaces in prefab names → underscores (matches tree_collision_profiles.csv).
    ///
    /// CSV format:
    ///   # bake_hash=<hex8>
    ///   worldX,worldZ,baseY,scale,profileName
    ///   ...
    /// </summary>
    public static class TreeObstacleBaker
    {
        private const string CourseId = "lomond-country-club";
        private const string StandaloneContainer = "StandaloneTrees";
        private const string PaintedContainer    = "PaintedTrees";

        // ── Menu items ───────────────────────────────────────────────────────────

        [MenuItem("Import/Bake Tree Obstacles/Bake Current Hole", false, 250)]
        public static void BakeCurrentHole()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int n = ExtractHoleNumber(scene.name);
            if (n < 1 || n > 18)
            {
                EditorUtility.DisplayDialog("Bake Tree Obstacles",
                    $"Cannot detect hole number from scene '{scene.name}'.\n" +
                    "Expected a scene named Hole_NN or Hole_NN_Geo.", "OK");
                return;
            }
            BakeActiveScene(n);
        }

        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 01", false, 260)] public static void BakeH01() => BakeHole(1);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 02", false, 261)] public static void BakeH02() => BakeHole(2);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 03", false, 262)] public static void BakeH03() => BakeHole(3);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 04", false, 263)] public static void BakeH04() => BakeHole(4);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 05", false, 264)] public static void BakeH05() => BakeHole(5);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 06", false, 265)] public static void BakeH06() => BakeHole(6);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 07", false, 266)] public static void BakeH07() => BakeHole(7);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 08", false, 267)] public static void BakeH08() => BakeHole(8);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 09", false, 268)] public static void BakeH09() => BakeHole(9);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 10", false, 269)] public static void BakeH10() => BakeHole(10);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 11", false, 270)] public static void BakeH11() => BakeHole(11);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 12", false, 271)] public static void BakeH12() => BakeHole(12);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 13", false, 272)] public static void BakeH13() => BakeHole(13);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 14", false, 273)] public static void BakeH14() => BakeHole(14);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 15", false, 274)] public static void BakeH15() => BakeHole(15);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 16", false, 275)] public static void BakeH16() => BakeHole(16);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 17", false, 276)] public static void BakeH17() => BakeHole(17);
        [MenuItem("Import/Bake Tree Obstacles/Bake Hole 18", false, 277)] public static void BakeH18() => BakeHole(18);

        [MenuItem("Import/Bake Tree Obstacles/Bake All Holes", false, 350)]
        public static void BakeAllHoles()
        {
            try
            {
                for (int n = 1; n <= 18; n++)
                {
                    EditorUtility.DisplayProgressBar("Baking Tree Obstacles", $"Hole {n}/18", (n - 1) / 18f);
                    BakeHole(n);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[TreeObstacleBaker] All 18 holes baked.");
        }

        // ── Save-hook (D5) ────────────────────────────────────────────────────────

        [InitializeOnLoadMethod]
        private static void RegisterSaveHook()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            int n = ExtractHoleNumber(scene.name);
            if (n < 1 || n > 18) return;

            // Compute hash of current scene's tree harvest.
            var rows = HarvestScene(scene, n, out string _);
            if (rows == null) return;

            string newHash = ComputeHash(rows);

            // Load existing CSV and compare its header hash.
            string csvPath = GetCsvAssetPath(n);
            string fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", csvPath));

            if (File.Exists(fullPath))
            {
                using var reader = new StreamReader(fullPath);
                string firstLine = reader.ReadLine() ?? "";
                if (firstLine.StartsWith("# bake_hash="))
                {
                    string existingHash = firstLine.Substring("# bake_hash=".Length).Trim();
                    if (existingHash == newHash)
                    {
                        Debug.Log($"[TreeObstacleBaker] Hole {n:D2}: tree hash unchanged, skip re-bake.");
                        return;
                    }
                }
            }

            // Hash changed — auto re-bake.
            Debug.Log($"[TreeObstacleBaker] Hole {n:D2}: tree state changed, auto re-baking...");
            WriteCsv(csvPath, rows, newHash, n);
        }

        // ── Core bake logic ───────────────────────────────────────────────────────

        private static void BakeHole(int n)
        {
            string scenePath = GetGeoScenePath(n);
            if (scenePath == null)
            {
                Debug.LogWarning($"[TreeObstacleBaker] Hole {n}: no Geo scene found, skipping.");
                return;
            }
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            BakeActiveScene(n);
        }

        private static void BakeActiveScene(int n)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var rows  = HarvestScene(scene, n, out string breakdown);
            if (rows == null)
            {
                Debug.LogWarning($"[TreeObstacleBaker] Hole {n:D2}: harvest returned no rows. CSV not written.");
                return;
            }

            string hash    = ComputeHash(rows);
            string csvPath = GetCsvAssetPath(n);
            WriteCsv(csvPath, rows, hash, n);
            Debug.Log($"[TreeObstacleBaker] Hole {n:D2}: baked {rows.Count} trees → {csvPath}\n{breakdown}");
        }

        // ── Harvest ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Harvests all tree instances from the active scene.
        /// Returns list of (worldX, worldZ, baseY, scale, profileName) rows, or null on fatal error.
        /// breakdown = human-readable per-source counts for the log.
        /// </summary>
        private static List<string> HarvestScene(
            UnityEngine.SceneManagement.Scene scene, int holeNumber, out string breakdown)
        {
            breakdown = "";
            var rows = new List<string>();

            int terrainCount    = 0;
            int standaloneCount = 0;
            int paintedCount    = 0;

            // ── 1. Terrain tree instances ──────────────────────────────────────
            var rootGOs = scene.GetRootGameObjects();
            Terrain terrain = null;
            foreach (var go in rootGOs)
            {
                terrain = go.GetComponentInChildren<Terrain>(true);
                if (terrain != null) break;
            }

            if (terrain != null)
            {
                var td         = terrain.terrainData;
                var protos     = td.treePrototypes;
                var instances  = td.treeInstances;
                Vector3 tPos   = terrain.transform.position;
                Vector3 tSize  = td.size;

                foreach (var ti in instances)
                {
                    if (ti.prototypeIndex < 0 || ti.prototypeIndex >= protos.Length)
                        continue;

                    string prefabName = protos[ti.prototypeIndex].prefab
                        ? protos[ti.prototypeIndex].prefab.name
                        : "unknown";
                    string profileName = NormalizeName(prefabName);

                    // Terrain trees: position is normalized [0,1], convert to world.
                    float wx = tPos.x + ti.position.x * tSize.x;
                    float wz = tPos.z + ti.position.z * tSize.z;
                    // baseY: terrain height at that XZ.
                    float by = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + tPos.y;
                    float scale = ti.widthScale;

                    rows.Add(FormatRow(wx, wz, by, scale, profileName));
                    terrainCount++;
                }
            }

            // ── 2 & 3. StandaloneTrees + PaintedTrees containers ──────────────
            foreach (var rootGO in rootGOs)
            {
                foreach (Transform child in rootGO.transform)
                {
                    HarvestContainer(child.gameObject, StandaloneContainer, terrain, rows, ref standaloneCount);
                    HarvestContainer(child.gameObject, PaintedContainer,    terrain, rows, ref paintedCount);
                }
                // Also check root-level containers (scene root → direct children).
                HarvestContainer(rootGO, StandaloneContainer, terrain, rows, ref standaloneCount);
                HarvestContainer(rootGO, PaintedContainer,    terrain, rows, ref paintedCount);
            }

            breakdown = $"terrain={terrainCount} standalone={standaloneCount} painted={paintedCount} total={rows.Count}";
            return rows.Count > 0 ? rows : null;
        }

        private static void HarvestContainer(GameObject go, string containerName,
            Terrain terrain, List<string> rows, ref int count)
        {
            if (go.name != containerName) return;

            foreach (Transform child in go.transform)
            {
                string prefabName  = StripSuffix(child.name);
                string profileName = NormalizeName(prefabName);

                Vector3 pos   = child.position;
                float   scale = child.localScale.x; // uniform scale assumed

                float baseY;
                if (terrain != null)
                {
                    Vector3 tPos = terrain.transform.position;
                    baseY = terrain.SampleHeight(new Vector3(pos.x, 0f, pos.z)) + tPos.y;
                }
                else
                {
                    baseY = pos.y; // no terrain — use GO position y as fallback
                }

                rows.Add(FormatRow(pos.x, pos.z, baseY, scale, profileName));
                count++;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static string FormatRow(float wx, float wz, float by, float scale, string profileName)
            => $"{wx:F4},{wz:F4},{by:F4},{scale:F4},{profileName}";

        /// <summary>
        /// Strip trailing _{n} or _brush_{n} suffix from a GO name.
        /// "MESH_01Cedar_12"      → "MESH_01Cedar"
        /// "Spruce 1_brush_7"     → "Spruce 1"
        /// </summary>
        private static string StripSuffix(string name)
        {
            // Remove _brush_<digits> or _<digits> at end.
            // Work right-to-left: find last '_', check if remainder is digits or "brush_digits".
            int last = name.LastIndexOf('_');
            if (last < 0) return name;

            string tail = name.Substring(last + 1);
            if (IsAllDigits(tail))
            {
                string remaining = name.Substring(0, last);
                // Check for additional _brush_ level.
                int prev = remaining.LastIndexOf('_');
                if (prev >= 0 && remaining.Substring(prev + 1) == "brush")
                    return remaining.Substring(0, prev);
                return remaining;
            }
            return name;
        }

        private static bool IsAllDigits(string s)
        {
            if (s.Length == 0) return false;
            foreach (char c in s) if (c < '0' || c > '9') return false;
            return true;
        }

        /// <summary>
        /// Normalize prefab name for profile lookup: replace spaces with underscores.
        /// "Spruce 1" → "Spruce_1", "MESH_01Cedar" → "MESH_01Cedar".
        /// </summary>
        private static string NormalizeName(string name)
            => name.Replace(' ', '_');

        /// <summary>
        /// Compute a short hex8 hash over sorted row strings for staleness detection.
        /// Sorted so that insertion-order differences don't change the hash.
        /// </summary>
        private static string ComputeHash(List<string> rows)
        {
            var sorted = new List<string>(rows);
            sorted.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            foreach (var r in sorted) sb.Append(r).Append('\n');

            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            uint hash = 2166136261u; // FNV-1a 32-bit
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= 16777619u;
            }
            return hash.ToString("x8");
        }

        private static void WriteCsv(string csvAssetPath, List<string> rows, string hash, int holeNumber)
        {
            string fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", csvAssetPath));

            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine($"# bake_hash={hash}");
            sb.AppendLine("worldX,worldZ,baseY,scale,profileName");
            foreach (var row in rows)
                sb.AppendLine(row);

            File.WriteAllText(fullPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(csvAssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[TreeObstacleBaker] Wrote {rows.Count} rows to {csvAssetPath} (hash={hash})");
        }

        private static string GetCsvAssetPath(int holeNumber)
            => $"Assets/Resources/HoleData/Hole_{holeNumber:D2}/tree_obstacles.csv";

        private static string GetGeoScenePath(int n)
        {
            string path = $"Assets/Golf/Courses/{CourseId}/Generated/Hole_{n:D2}_Geo.unity";
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
            if (File.Exists(full)) return path;
            Debug.LogWarning($"[TreeObstacleBaker] Hole {n}: no scene at {path}");
            return null;
        }

        private static int ExtractHoleNumber(string sceneName)
        {
            // Match "Hole_01", "Hole_01_Geo", "Hole_1_Geo", etc.
            int i = sceneName.IndexOf("Hole_", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return -1;
            string rest = sceneName.Substring(i + 5); // after "Hole_"
            int end = 0;
            while (end < rest.Length && char.IsDigit(rest[end])) end++;
            if (end == 0) return -1;
            return int.TryParse(rest.Substring(0, end), out int n) ? n : -1;
        }
    }
}
#endif
