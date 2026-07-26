#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Course.Runtime;

namespace Golfin.CourseImport
{
    public static class PhysicsHeightmapBaker
    {
        const string CourseId = "lomond-country-club";
        const float Q16_16_SCALE = 65536f; // 2^16

        // ------------------------------------------------------------------ //
        // Menu items
        // ------------------------------------------------------------------ //

        [MenuItem("Import/Bake Physics Heightmap/Bake Current Hole", false, 200)]
        public static void BakeCurrentHole()
        {
            var active = EditorSceneManager.GetActiveScene();
            int holeNum = ExtractHoleNumber(active.name);
            if (holeNum < 1 || holeNum > 18)
            {
                EditorUtility.DisplayDialog("Bake Physics Heightmap",
                    $"Cannot detect hole number from scene '{active.name}'.\n" +
                    "Expected scene named Hole_XX, Hole_XX_Geo, Hole_XX_Flat, or Hole_XX_Geo_Flat.", "OK");
                return;
            }
            bool isFlat = active.name.IndexOf("_Flat", StringComparison.OrdinalIgnoreCase) >= 0;
            BakeActiveScene(holeNum, isFlat);
        }

        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 01", false, 210)] public static void BakeHole01() => BakeHole(1);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 02", false, 211)] public static void BakeHole02() => BakeHole(2);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 03", false, 212)] public static void BakeHole03() => BakeHole(3);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 04", false, 213)] public static void BakeHole04() => BakeHole(4);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 05", false, 214)] public static void BakeHole05() => BakeHole(5);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 06", false, 215)] public static void BakeHole06() => BakeHole(6);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 07", false, 216)] public static void BakeHole07() => BakeHole(7);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 08", false, 217)] public static void BakeHole08() => BakeHole(8);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 09", false, 218)] public static void BakeHole09() => BakeHole(9);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 10", false, 219)] public static void BakeHole10() => BakeHole(10);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 11", false, 220)] public static void BakeHole11() => BakeHole(11);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 12", false, 221)] public static void BakeHole12() => BakeHole(12);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 13", false, 222)] public static void BakeHole13() => BakeHole(13);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 14", false, 223)] public static void BakeHole14() => BakeHole(14);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 15", false, 224)] public static void BakeHole15() => BakeHole(15);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 16", false, 225)] public static void BakeHole16() => BakeHole(16);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 17", false, 226)] public static void BakeHole17() => BakeHole(17);
        [MenuItem("Import/Bake Physics Heightmap/Bake Hole 18", false, 227)] public static void BakeHole18() => BakeHole(18);

        [MenuItem("Import/Bake Physics Heightmap/Bake All Holes", false, 300)]
        public static void BakeAllHoles()
        {
            int totalHoles = 0;
            long totalBytes = 0;
            int totalMismatches = 0;

            try
            {
                for (int n = 1; n <= 18; n++)
                {
                    EditorUtility.DisplayProgressBar(
                        "Baking Physics Heightmaps",
                        $"Hole {n}/18",
                        (n - 1) / 18f);

                    string scenePath = GetGeoScenePath(n);
                    if (scenePath == null)
                    {
                        Debug.LogWarning($"[PhysicsHeightmapBaker] Hole {n}: no Geo scene found, skipping.");
                        continue;
                    }

                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                    var result = BakeActiveScene(n, isFlat: false);
                    if (result.HasValue)
                    {
                        totalHoles++;
                        totalBytes += result.Value.fileSizeBytes;
                        totalMismatches += result.Value.mismatches;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            float totalMB = totalBytes / (1024f * 1024f);
            Debug.Log($"[PhysicsHeightmapBaker] All holes done — {totalHoles} baked, " +
                      $"{totalMB:F1} MB total, {totalMismatches} round-trip mismatches.");

            if (totalBytes > 400L * 1024 * 1024)
                Debug.LogWarning($"[PhysicsHeightmapBaker] Total size {totalMB:F1} MB exceeds 400 MB threshold — consider downsampling or compression.");
        }

        // ------------------------------------------------------------------ //
        // Core bake logic
        // ------------------------------------------------------------------ //

        private static void BakeHole(int holeNumber)
        {
            var active = EditorSceneManager.GetActiveScene();
            bool isGeoScene = active.isLoaded
                && ExtractHoleNumber(active.name) == holeNumber
                && active.name.IndexOf("_Geo", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!isGeoScene)
            {
                string scenePath = GetGeoScenePath(holeNumber);
                if (scenePath == null) return;
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            BakeActiveScene(holeNumber, isFlat: false);
        }

        private struct BakeResult
        {
            public long fileSizeBytes;
            public int mismatches;
        }

        private static BakeResult? BakeActiveScene(int holeNumber, bool isFlat)
        {
            // Find terrain
            var terrainObj = GameObject.Find("Terrain");
            if (terrainObj == null)
            {
                var t = UnityEngine.Object.FindObjectOfType<Terrain>();
                if (t != null) terrainObj = t.gameObject;
            }

            var terrain = terrainObj != null ? terrainObj.GetComponent<Terrain>() : null;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError($"[PhysicsHeightmapBaker] Hole {holeNumber}: no Terrain found in scene.");
                return null;
            }

            var tData = terrain.terrainData;
            int res = tData.heightmapResolution;
            float[,] heights = tData.GetHeights(0, 0, res, res);
            Vector3 size = tData.size;
            Vector3 pos = terrain.transform.position;

            // Build Q16.16 buffer
            var buffer = new int[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    buffer[y, x] = ToQ16_16(heights[y, x] * size.y);

            // Determine output path.
            // SPEC §5.6: fail loudly at bake sites when slug is null.
            string courseSlug = CourseSlugResolver.ResolveOrThrow(
                EditorSceneManager.GetActiveScene().path,
                "PhysicsHeightmapBaker.BakeActiveScene");
            string holeFolder = isFlat ? $"hole-{holeNumber:D2}-flat" : $"hole-{holeNumber:D2}";
            string exportRoot = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..",
                $"Tools/UHoleGeo/output/{courseSlug}/export"));
            string exportPath = Path.Combine(exportRoot, holeFolder);
            string outPath = Path.Combine(exportPath, "heightmap.bytes");

            if (!Directory.Exists(exportPath))
            {
                Debug.LogWarning($"[PhysicsHeightmapBaker] Hole {holeNumber}: export folder not found: {exportPath}\n" +
                                 "Has this hole been exported from UHoleGeo?");
                return null;
            }

            // Write binary file
            using (var fs = File.Open(outPath, FileMode.Create, FileAccess.Write))
            using (var bw = new BinaryWriter(fs))
            {
                // Magic "GHM1"
                bw.Write((byte)'G');
                bw.Write((byte)'H');
                bw.Write((byte)'M');
                bw.Write((byte)'1');
                bw.Write((int)1);          // version
                bw.Write((int)res);        // resolution
                bw.Write((float)size.x);   // sizeX
                bw.Write((float)size.z);   // sizeZ
                bw.Write((float)pos.x);    // posX
                bw.Write((float)pos.y);    // posY
                bw.Write((float)pos.z);    // posZ
                bw.Write((int)1);          // format = Q16.16

                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                        bw.Write(buffer[y, x]);
            }

            long fileSize = new FileInfo(outPath).Length;
            float fileMB = fileSize / (1024f * 1024f);

            // Round-trip validation
            int mismatches = ValidateRoundTrip(outPath, heights, res, size.y);

            Debug.Log($"[PhysicsHeightmapBaker] Hole {holeNumber}: wrote {outPath} ({fileMB:F1} MB), " +
                      $"round-trip mismatches: {mismatches}/100");

            if (fileSize > 25L * 1024 * 1024)
                Debug.LogWarning($"[PhysicsHeightmapBaker] Hole {holeNumber}: file size {fileMB:F1} MB exceeds 25 MB threshold.");

            return new BakeResult { fileSizeBytes = fileSize, mismatches = mismatches };
        }

        private static int ValidateRoundTrip(string path, float[,] originalHeights, int res, float elevRange)
        {
            int mismatches = 0;
            var rng = new System.Random(42);

            using (var fs = File.OpenRead(path))
            using (var br = new BinaryReader(fs))
            {
                // Skip header (36 bytes)
                br.ReadBytes(36);

                // Read all data into array
                var readBack = new int[res, res];
                for (int y = 0; y < res; y++)
                    for (int x = 0; x < res; x++)
                        readBack[y, x] = br.ReadInt32();

                for (int i = 0; i < 100; i++)
                {
                    int y = rng.Next(res);
                    int x = rng.Next(res);
                    float orig = originalHeights[y, x] * elevRange;
                    float round = FromQ16_16(readBack[y, x]);
                    if (Math.Abs(orig - round) > 0.001f) mismatches++;
                }
            }

            if (mismatches > 0)
                Debug.LogError($"[PhysicsHeightmapBaker] Round-trip validation failed: {mismatches}/100 points exceed 1mm tolerance.");

            return mismatches;
        }

        // ------------------------------------------------------------------ //
        // Helpers
        // ------------------------------------------------------------------ //

        private static int ToQ16_16(float worldMeters) => (int)Math.Round(worldMeters * Q16_16_SCALE);
        private static float FromQ16_16(int fp) => fp / Q16_16_SCALE;

        private static int ExtractHoleNumber(string sceneName)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                sceneName, @"Hole_(\d{2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return -1;
            return int.TryParse(m.Groups[1].Value, out int n) ? n : -1;
        }

        private static string GetGeoScenePath(int holeNumber)
        {
            string geoPath = $"Assets/Golf/Courses/{CourseId}/Generated/Hole_{holeNumber:D2}_Geo.unity";
            string fullGeo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", geoPath));
            if (File.Exists(fullGeo)) return geoPath;

            string fallback = $"Assets/Golf/Courses/{CourseId}/Generated/Hole_{holeNumber:D2}.unity";
            string fullFallback = Path.GetFullPath(Path.Combine(Application.dataPath, "..", fallback));
            if (File.Exists(fullFallback))
            {
                Debug.LogWarning($"[PhysicsHeightmapBaker] Hole {holeNumber}: _Geo scene not found, using fallback {fallback}");
                return fallback;
            }

            Debug.LogWarning($"[PhysicsHeightmapBaker] Hole {holeNumber}: no scene found at {geoPath} or {fallback}");
            return null;
        }
    }
}
#endif
