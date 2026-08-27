#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.CourseImport
{
    /// <summary>
    /// TRACKED placement record for the "StandaloneTrees" GameObjects a hole scene carries.
    ///
    /// WHY THIS EXISTS
    ///   Assets/Golf/Courses/*/Generated/*.unity is gitignored — every machine generates its own
    ///   hole scenes. Terrain trees survive that because they live in the TRACKED TerrainData
    ///   asset. Standalone trees (TreePlacer.ForceStandaloneNames — "Spruce 1", "Spruce 3") lived
    ///   ONLY in the per-machine scene, so a machine whose scene predated a placement pass ended
    ///   up colliding with trees the bake says exist but the scene does not render.
    ///   That is exactly what happened to Hole 02 on the Mac: tree_obstacles.csv carried
    ///   1,495 standalone Spruce, the local scene carried zero → 1,495 invisible colliders.
    ///
    ///   standalone_trees.csv makes that placement reproducible on any machine:
    ///     Assets/Golf/Courses/&lt;slug&gt;/Data/hole-NN-geo/standalone_trees.csv   (TRACKED)
    ///
    /// CSV FORMAT (one row per StandaloneTrees child, in sibling order)
    ///   prefab,worldX,worldY,worldZ,yawDeg,scale
    ///   Spruce 1,24.4774,4.0594,-171.5545,182.4413,0.9761
    ///
    ///   'prefab' is the prefab asset name (spaces intact — TreeObstacleBaker is what maps
    ///   "Spruce 1" → profile "Spruce_1", not this file).
    ///   Row ORDER IS LOAD-BEARING: TreeObstacleBaker harvests StandaloneTrees children in
    ///   sibling order and writes tree_obstacles.csv in that order, so Rebuild must
    ///   re-instantiate in file order for the bake to round-trip byte-identically.
    ///
    /// WRITE SITES
    ///   • Import/Standalone Trees/Export Current Hole  (manual)
    ///   • TreePlacer.PlaceTrees                        (every placement pass)
    ///   • TreeBrushTool paint / erase                  (every brush write)
    /// READ SITES
    ///   • Import/Standalone Trees/Rebuild Current Hole
    ///   • TreeObstacleBaker.ValidateAllHoles           (drift gate)
    /// </summary>
    public static class StandaloneTreeCatalog
    {
        public const string ContainerName = "StandaloneTrees";

        private const string Tag = "[StandaloneTreeCatalog]";
        private const string HeaderComment = "# Tracked standalone tree placement — see StandaloneTreeCatalog.cs";
        private const string HeaderColumns = "prefab,worldX,worldY,worldZ,yawDeg,scale";

        /// <summary>Deterministic yaw seed used when seeding a hole's CSV from an existing bake.</summary>
        public const int SeedFromBakeSeed = 20260827;

        // ── Row model ────────────────────────────────────────────────────────────

        public struct Row
        {
            public string prefab;
            public float x, y, z;
            public float yawDeg;
            public float scale;
        }

        // ── Paths ────────────────────────────────────────────────────────────────

        public static string GetCsvAssetPath(int holeNumber, string courseSlug)
            => $"Assets/Golf/Courses/{courseSlug}/Data/hole-{holeNumber:D2}-geo/standalone_trees.csv";

        public static string ToFullPath(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        // ── Menu items ───────────────────────────────────────────────────────────

        [MenuItem("Import/Standalone Trees/Export Current Hole", false, 200)]
        public static void ExportCurrentHoleMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!TryExportScene(scene, out string message))
                Debug.LogError($"{Tag} Export failed: {message}");
            else
                Debug.Log($"{Tag} {message}");
        }

        [MenuItem("Import/Standalone Trees/Export All Holes", false, 201)]
        public static void ExportAllHolesMenu()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int ok = 0, failed = 0;
            var lines = new List<string>();
            try
            {
                for (int n = 1; n <= 18; n++)
                {
                    EditorUtility.DisplayProgressBar("Exporting standalone trees", $"Hole {n:D2}/18", (n - 1) / 18f);
                    string scenePath = TreeObstacleBaker.GetGeoScenePath(n);
                    if (scenePath == null)
                    {
                        lines.Add($"  Hole {n:D2}: SKIP (no Geo scene on this machine)");
                        continue;
                    }

                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    bool success = TryExportScene(scene, out string message);
                    lines.Add($"  Hole {n:D2}: {(success ? "OK" : "FAIL")} — {message}");
                    if (success) ok++; else failed++;
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                RestoreSetup(setup);
            }

            AssetDatabase.Refresh();
            string report = $"{Tag} Export All Holes: {ok} exported, {failed} failed.\n" + string.Join("\n", lines);
            if (failed > 0) Debug.LogError(report); else Debug.Log(report);
        }

        [MenuItem("Import/Standalone Trees/Rebuild Current Hole", false, 210)]
        public static void RebuildCurrentHoleMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!TryRebuildScene(scene, out string message))
                Debug.LogError($"{Tag} Rebuild failed: {message}");
            else
                Debug.Log($"{Tag} {message}");
        }

        // ── Export ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes &lt;hole&gt;/standalone_trees.csv from the StandaloneTrees children of
        /// <paramref name="scene"/>. A hole with no standalone trees still gets a
        /// header-only file, so "file absent" always means "never exported" and never
        /// "this hole legitimately has none" — the drift gate depends on that distinction.
        /// </summary>
        public static bool TryExportScene(Scene scene, out string message)
        {
            message = "";
            if (!scene.IsValid() || !scene.isLoaded)
            {
                message = $"scene '{scene.name}' is not loaded.";
                return false;
            }

            int hole = TreeObstacleBaker.ExtractHoleNumber(scene.name);
            if (hole < 1 || hole > 18)
            {
                message = $"cannot detect hole number from scene '{scene.name}'.";
                return false;
            }

            string slug = Golfin.Course.Runtime.CourseSlugResolver.Resolve(scene.path);
            if (slug == null)
            {
                message = $"cannot resolve course slug from '{scene.path}'.";
                return false;
            }

            var rows = HarvestRows(scene);
            string assetPath = GetCsvAssetPath(hole, slug);
            WriteCsv(assetPath, rows);

            message = $"Hole {hole:D2}: exported {rows.Count} standalone tree(s) → {assetPath}";
            return true;
        }

        /// <summary>
        /// Best-effort export used by the write sites (TreePlacer / TreeBrushTool). Never throws
        /// and never blocks the caller — a placement pass that cannot write the catalog logs a
        /// warning rather than aborting the placement the user asked for.
        /// </summary>
        public static void ExportSceneQuiet(Scene scene)
        {
            try
            {
                if (TryExportScene(scene, out string message))
                    Debug.Log($"{Tag} {message}");
                else
                    Debug.LogWarning($"{Tag} standalone_trees.csv NOT updated — {message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} standalone_trees.csv NOT updated — {e.GetType().Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Harvests every StandaloneTrees child of <paramref name="scene"/>.
        /// Traversal order MIRRORS TreeObstacleBaker.HarvestScene exactly (root GOs, each root's
        /// direct children first, then the root itself) so the two agree row-for-row.
        /// </summary>
        public static List<Row> HarvestRows(Scene scene)
        {
            var rows = new List<Row>();
            foreach (var container in FindContainers(scene))
            {
                foreach (Transform child in container.transform)
                {
                    rows.Add(new Row
                    {
                        prefab  = TreeObstacleBaker.StripInstanceSuffix(child.name),
                        x       = child.position.x,
                        y       = child.position.y,
                        z       = child.position.z,
                        yawDeg  = NormalizeYaw(child.rotation.eulerAngles.y),
                        scale   = child.localScale.x,
                    });
                }
            }
            return rows;
        }

        /// <summary>
        /// All GameObjects named StandaloneTrees, in TreeObstacleBaker.HarvestScene's traversal
        /// order. Normally exactly one (a child of HoleRoot).
        /// </summary>
        public static List<GameObject> FindContainers(Scene scene)
        {
            var found = new List<GameObject>();
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                foreach (Transform child in rootGO.transform)
                    if (child.gameObject.name == ContainerName) found.Add(child.gameObject);
                if (rootGO.name == ContainerName) found.Add(rootGO);
            }
            return found;
        }

        // ── Rebuild ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Destroys every StandaloneTrees container in <paramref name="scene"/> and rebuilds one
        /// from standalone_trees.csv: every row instantiated as a prefab instance named
        /// {prefab}_{rowIndex}, in file order, parented exactly where TreePlacer parents its
        /// container (under HoleRoot when present).
        ///
        /// NOT UNDOABLE — a rebuild is thousands of objects; Undo registration at that volume
        /// costs more than it is worth. Re-run Rebuild to get back to the CSV state.
        /// </summary>
        public static bool TryRebuildScene(Scene scene, out string message)
        {
            message = "";
            if (!scene.IsValid() || !scene.isLoaded)
            {
                message = $"scene '{scene.name}' is not loaded.";
                return false;
            }

            int hole = TreeObstacleBaker.ExtractHoleNumber(scene.name);
            if (hole < 1 || hole > 18)
            {
                message = $"cannot detect hole number from scene '{scene.name}'.";
                return false;
            }

            string slug = Golfin.Course.Runtime.CourseSlugResolver.Resolve(scene.path);
            if (slug == null)
            {
                message = $"cannot resolve course slug from '{scene.path}'.";
                return false;
            }

            string assetPath = GetCsvAssetPath(hole, slug);
            var rows = ReadCsv(assetPath, out string readError);
            if (rows == null)
            {
                message = $"Hole {hole:D2}: {readError}";
                return false;
            }

            // Resolve every distinct prefab BEFORE mutating the scene — a missing prefab must
            // not leave the hole with its trees deleted and nothing put back.
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var r in rows)
            {
                if (prefabs.ContainsKey(r.prefab)) continue;
                var prefab = ResolveTreePrefab(r.prefab);
                if (prefab == null) missing.Add(r.prefab);
                else prefabs[r.prefab] = prefab;
            }
            if (missing.Count > 0)
            {
                message = $"Hole {hole:D2}: cannot resolve tree prefab(s): {string.Join(", ", missing)}. " +
                          "Nothing was changed.";
                return false;
            }

            int destroyed = 0;
            foreach (var container in FindContainers(scene))
            {
                destroyed += container.transform.childCount;
                UnityEngine.Object.DestroyImmediate(container);
            }

            var newContainer = new GameObject(ContainerName);
            SceneManager.MoveGameObjectToScene(newContainer, scene);

            var holeRoot = FindHoleRoot(scene);
            if (holeRoot != null) newContainer.transform.SetParent(holeRoot, true);

            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[r.prefab], scene);
                instance.name = $"{r.prefab}_{i}";
                instance.transform.SetParent(newContainer.transform, true);
                instance.transform.position   = new Vector3(r.x, r.y, r.z);
                instance.transform.rotation   = Quaternion.Euler(0f, r.yawDeg, 0f);
                instance.transform.localScale = Vector3.one * r.scale;

                // Same LOD normalization TreePlacer and TreeBrushTool apply to their standalone
                // instances — without it a rebuilt hole pops at different distances than a placed one.
                foreach (var lodGroup in instance.GetComponentsInChildren<LODGroup>(true))
                    TreePlacer.NormalizeLODGroup(lodGroup);

                if ((i & 255) == 0)
                    EditorUtility.DisplayProgressBar("Rebuilding standalone trees",
                        $"Hole {hole:D2}: {i}/{rows.Count}", (float)i / Mathf.Max(1, rows.Count));
            }
            EditorUtility.ClearProgressBar();

            EditorSceneManager.MarkSceneDirty(scene);

            message = $"Hole {hole:D2}: rebuilt {rows.Count} standalone tree(s) from {assetPath} " +
                      $"(destroyed {destroyed} pre-existing). Scene is dirty — SAVE IT.";
            return true;
        }

        private static Transform FindHoleRoot(Scene scene)
        {
            foreach (var rootGO in scene.GetRootGameObjects())
            {
                if (rootGO.name == "HoleRoot") return rootGO.transform;
                var t = rootGO.transform.Find("HoleRoot");
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>
        /// Resolve a prefab by asset name through TreePlacer's own palette first (so Rebuild uses
        /// exactly the asset TreePlacer would have instantiated), falling back to a project-wide
        /// search under the tree prefab folders.
        /// </summary>
        public static GameObject ResolveTreePrefab(string prefabName)
        {
            if (TreePlacer.TreePalette.Count == 0) TreePlacer.ScanPrefabs();
            foreach (var entry in TreePlacer.TreePalette)
                if (entry.name == prefabName && entry.prefab != null) return entry.prefab;

            foreach (var guid in AssetDatabase.FindAssets($"\"{prefabName}\" t:Prefab", TreePlacer.TreePrefabFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) != prefabName) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) return prefab;
            }
            return null;
        }

        // ── CSV IO ───────────────────────────────────────────────────────────────

        public static void WriteCsv(string assetPath, List<Row> rows)
        {
            string fullPath = ToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Explicit '\n', never AppendLine: a tracked file must not change wholesale just
            // because it was written on Windows instead of macOS.
            var sb = new StringBuilder();
            sb.Append(HeaderComment).Append('\n');
            sb.Append(HeaderColumns).Append('\n');
            foreach (var r in rows)
                sb.Append(FormatRow(r)).Append('\n');

            File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public static string FormatRow(Row r)
            => string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}",
                             r.prefab, r.x, r.y, r.z, r.yawDeg, r.scale);

        /// <summary>Returns null and sets <paramref name="error"/> when the file is missing or malformed.</summary>
        public static List<Row> ReadCsv(string assetPath, out string error)
        {
            error = null;
            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                error = $"{assetPath} does not exist — run Import/Standalone Trees/Export Current Hole " +
                        "on a machine whose scene is correct, and commit it.";
                return null;
            }

            var rows = new List<Row>();
            string[] lines = File.ReadAllLines(fullPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.StartsWith("prefab,", StringComparison.Ordinal)) continue;

                var parts = line.Split(',');
                if (parts.Length != 6)
                {
                    error = $"{assetPath}:{i + 1}: expected 6 columns, got {parts.Length} — \"{line}\"";
                    return null;
                }

                if (!TryF(parts[1], out float x) || !TryF(parts[2], out float y) ||
                    !TryF(parts[3], out float z) || !TryF(parts[4], out float yaw) ||
                    !TryF(parts[5], out float scale))
                {
                    error = $"{assetPath}:{i + 1}: non-numeric field — \"{line}\"";
                    return null;
                }

                rows.Add(new Row { prefab = parts[0], x = x, y = y, z = z, yawDeg = yaw, scale = scale });
            }
            return rows;
        }

        private static bool TryF(string s, out float v)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f) yaw += 360f;
            return yaw;
        }

        /// <summary>
        /// Restores a captured scene setup — but ONLY when it actually differs from what is open
        /// now. RestoreSceneManagerSetup RELOADS every scene it names, which would silently throw
        /// away another session's unsaved edits (or pop a save prompt) in the overwhelmingly
        /// common case where the additive open/close already left the setup untouched.
        /// </summary>
        public static void RestoreSetup(SceneSetup[] setup)
        {
            if (setup == null || setup.Length == 0) return;
            if (SetupMatchesOpenScenes(setup)) return;

            try { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            catch (Exception e) { Debug.LogWarning($"{Tag} could not restore scene setup: {e.Message}"); }
        }

        private static bool SetupMatchesOpenScenes(SceneSetup[] setup)
        {
            if (setup.Length != SceneManager.sceneCount) return false;
            for (int i = 0; i < setup.Length; i++)
            {
                var open = SceneManager.GetSceneAt(i);
                if (open.path != setup[i].path) return false;
                if (open.isLoaded != setup[i].isLoaded) return false;
                if (setup[i].isActive && EditorSceneManager.GetActiveScene().path != setup[i].path) return false;
            }
            return true;
        }

        // ── Seeding a hole's CSV from an existing bake ────────────────────────────

        /// <summary>
        /// Default TreePlacer sink offset (metres). Standalone trees are pushed this far BELOW the
        /// terrain surface so trunk bases do not float on slopes, so a placed tree's transform sits
        /// at (terrain height - SinkOffset) while the bake records the terrain height itself.
        /// Measured against every healthy hole: bake baseY - catalog worldY == 0.3000 on all of them.
        /// </summary>
        public const float DefaultSinkOffsetM = 0.3f;

        /// <summary>
        /// Reconstructs standalone_trees.csv for a hole whose SCENE lost its standalone trees but
        /// whose committed tree_obstacles.csv still records them (the Hole 02 drift).
        ///
        /// Bake rows carry no rotation — TreeObstacleBaker never harvested one — so yaw is
        /// regenerated deterministically from the row index.
        ///
        /// worldY = baseY - <paramref name="sinkOffsetM"/>, NOT baseY. The bake's baseY is the
        /// TERRAIN height under the tree; the tree transform itself sits sinkOffset below it (see
        /// DefaultSinkOffsetM). Seeding at baseY would leave all 1,495 trees floating 30 cm above
        /// where every other hole's trees sit. This does not affect the re-bake: TreeObstacleBaker
        /// re-derives baseY from the terrain and ignores the transform's Y entirely.
        /// </summary>
        public static bool TrySeedFromBake(int holeNumber, string courseSlug,
            IList<string> standaloneProfiles, float sinkOffsetM, out string message)
        {
            message = "";
            string bakePath = $"Assets/Resources/HoleData/{courseSlug}/Hole_{holeNumber:D2}/tree_obstacles.csv";
            string bakeFull = ToFullPath(bakePath);
            if (!File.Exists(bakeFull))
            {
                message = $"{bakePath} does not exist.";
                return false;
            }

            // profile ("Spruce_1") → prefab ("Spruce 1"). The baker's normalization is
            // name.Replace(' ', '_'); inverting it is only safe for the profiles we are told to
            // take, which is why the caller passes the list explicitly.
            var wanted = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var profile in standaloneProfiles)
            {
                string prefabName = profile.Replace('_', ' ');
                if (ResolveTreePrefab(prefabName) == null)
                {
                    message = $"profile '{profile}' maps to prefab '{prefabName}', which does not exist.";
                    return false;
                }
                wanted[profile] = prefabName;
            }

            var rows = new List<Row>();
            int rowIndex = 0;
            foreach (string raw in File.ReadAllLines(bakeFull))
            {
                string line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.StartsWith("worldX,", StringComparison.Ordinal)) continue;

                var p = line.Split(',');
                if (p.Length != 5) continue;
                string profile = p[4];
                if (!wanted.TryGetValue(profile, out string prefabName)) continue;

                if (!TryF(p[0], out float wx) || !TryF(p[1], out float wz) ||
                    !TryF(p[2], out float by) || !TryF(p[3], out float scale))
                {
                    message = $"{bakePath}: non-numeric row \"{line}\"";
                    return false;
                }

                rows.Add(new Row
                {
                    prefab = prefabName,
                    x = wx,
                    y = by - sinkOffsetM,   // bake baseY is the TERRAIN height; the tree sits sinkOffset below it
                    z = wz,
                    yawDeg = (float)(new System.Random(SeedFromBakeSeed + rowIndex).NextDouble() * 360.0),
                    scale = scale,
                });
                rowIndex++;
            }

            if (rows.Count == 0)
            {
                message = $"{bakePath} contains no rows for profiles [{string.Join(", ", standaloneProfiles)}].";
                return false;
            }

            string assetPath = GetCsvAssetPath(holeNumber, courseSlug);
            WriteCsv(assetPath, rows);
            message = $"Hole {holeNumber:D2}: seeded {rows.Count} standalone tree(s) from {bakePath} " +
                      $"(worldY = baseY - {sinkOffsetM:F2} m sink) → {assetPath}";
            return true;
        }
    }
}
#endif
