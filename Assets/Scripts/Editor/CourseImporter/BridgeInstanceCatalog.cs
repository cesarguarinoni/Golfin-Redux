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
    /// TRACKED placement record for the bridge instances a hole scene carries — the bridge
    /// counterpart of <see cref="StandaloneTreeCatalog"/>, and it exists for the identical reason.
    ///
    /// WHY THIS EXISTS (not in the bridge_transplant SPEC — found while implementing it)
    ///   <c>Assets/Golf/Courses/*/Generated/*</c> is gitignored (.gitignore:111); every machine
    ///   builds its own hole scenes. The SPEC's Stage E asserts that
    ///   <c>Generated/Hole_NN_Geo.unity</c> will show up in <c>git diff --stat</c> — it cannot,
    ///   the path is ignored. So a bridge that lives ONLY in the scene reaches no other machine,
    ///   while <c>Resources/HoleData/&lt;slug&gt;/Hole_NN/zones.json</c> (which the same task
    ///   commits) DOES. That asymmetry is precisely the Hole 02 drift written up in
    ///   <c>Docs/Pipeline/TREES_AND_GENERATED_SCENES.md</c>, only inverted: physics would put a
    ///   solid Bridge deck 24 m above the water on every machine, and only this Mac would draw a
    ///   bridge under the ball.
    ///
    ///   <c>bridge_instances.json</c> closes it:
    ///     Assets/Golf/Courses/&lt;slug&gt;/Data/hole-NN-geo/bridge_instances.json   (TRACKED)
    ///
    /// NOT TO BE CONFUSED WITH <c>bridges.json</c> — that is
    /// <c>BridgeAnchor</c>/<c>BridgeExporter</c>'s cart-path spline-snapping export, a different
    /// feature that happens to share the word "bridge", explicitly out of scope for this task, and
    /// it lives in the UHoleGeo tool output, not here.
    ///
    /// FORMAT — JSON, not CSV, unlike standalone_trees.csv. A tree row is homogeneous
    /// (prefab + TRS). A bridge instance is not: hole 7's carries a scale/position override on a
    /// CHILD transform (the <c>Structure</c> branch is stretched y×4.09 and dropped 6.74 m so the
    /// piers reach the gorge floor) on top of the root's own TRS. Recording only a root TRS would
    /// silently drop that, so what is stored is Unity's own full property-modification set — the
    /// exact thing that makes the transplant lossless.
    ///
    /// WRITE SITES  • <c>BridgeTransplantTool.TransplantIntoScene</c> (automatic, every transplant)
    ///              • Import/Transplant Bridges/Export Catalog (Current Hole)   (manual)
    /// READ SITE    • Import/Transplant Bridges/Rebuild Current Hole
    /// </summary>
    public static class BridgeInstanceCatalog
    {
        private const string Tag        = "[BridgeInstanceCatalog]";
        private const string CourseId   = "lomond-country-club";

        // ── Serialized model (UnityEngine.JsonUtility — no Newtonsoft dependency) ──

        [Serializable]
        public sealed class Modification
        {
            public string targetGuid;        // GUID of the prefab asset the modified object lives in
            public long   targetFileId;      // local file id of that object inside the asset
            public string propertyPath;
            public string value;
            public string objectRefGuid;     // empty when the modification carries no object reference
            public long   objectRefFileId;
        }

        [Serializable]
        public sealed class Instance
        {
            public string name;              // preserved verbatim, " (1)" suffixes included
            public string sourcePrefabGuid;
            public string sourcePrefabPath;  // human-readable; GUID is what is resolved
            public List<Modification> modifications = new List<Modification>();
        }

        [Serializable]
        public sealed class CatalogFile
        {
            public string note = "Tracked bridge placement — see BridgeInstanceCatalog.cs. "
                               + "NOT bridges.json (that is BridgeExporter's cart-path anchor export).";
            public int    hole;
            public List<Instance> instances = new List<Instance>();
        }

        // ── Paths ────────────────────────────────────────────────────────────────

        public static string GetCatalogAssetPath(int holeNumber, string courseSlug)
            => $"Assets/Golf/Courses/{courseSlug}/Data/hole-{holeNumber:D2}-geo/bridge_instances.json";

        public static string ToFullPath(string assetPath)
            => Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));

        // ── Menu items ───────────────────────────────────────────────────────────

        [MenuItem("Import/Transplant Bridges/Export Catalog (Current Hole)", false, 320)]
        public static void ExportCurrentHoleMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!TryExportScene(scene, out string message)) Debug.LogError($"{Tag} Export failed: {message}");
            else Debug.Log($"{Tag} {message}");
        }

        [MenuItem("Import/Transplant Bridges/Rebuild Current Hole (from catalog)", false, 330)]
        public static void RebuildCurrentHoleMenu()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!TryRebuildScene(scene, out string message)) Debug.LogError($"{Tag} Rebuild failed: {message}");
            else Debug.Log($"{Tag} {message}");
        }

        // ── Export ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Writes bridge_instances.json from the <c>Bridges</c> container of <paramref name="scene"/>.
        /// A hole with no bridges still gets an empty-instances file, so "file absent" always means
        /// "never exported" and never "this hole legitimately has none" — same distinction
        /// standalone_trees.csv relies on.
        /// </summary>
        public static bool TryExportScene(Scene scene, out string message)
        {
            message = "";
            if (!scene.IsValid() || !scene.isLoaded) { message = $"scene '{scene.name}' is not loaded."; return false; }
            if (scene.path.Contains("/Video/"))      { message = $"'{scene.path}' is a Video scene — read-only source of truth."; return false; }

            int hole = TreeObstacleBaker.ExtractHoleNumber(scene.name);
            if (hole < 1 || hole > 18) { message = $"cannot detect hole number from scene '{scene.name}'."; return false; }

            string slug = Golfin.Course.Runtime.CourseSlugResolver.Resolve(scene.path);
            if (slug == null) { message = $"cannot resolve course slug from '{scene.path}'."; return false; }

            var file = new CatalogFile { hole = hole };
            GameObject container = FindContainer(scene);
            if (container != null)
            {
                foreach (Transform child in container.transform)
                {
                    var rec = Capture(child.gameObject, out string err);
                    if (rec == null) { message = $"'{child.name}': {err}"; return false; }
                    file.instances.Add(rec);
                }
            }

            string assetPath = GetCatalogAssetPath(hole, slug);
            string fullPath  = ToFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            // Explicit '\n' + no BOM: a tracked file must not churn wholesale between macOS and Windows.
            File.WriteAllText(fullPath, JsonUtility.ToJson(file, true).Replace("\r\n", "\n") + "\n",
                              new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

            message = $"Hole {hole:D2}: exported {file.instances.Count} bridge instance(s) → {assetPath}";
            return true;
        }

        /// <summary>Best-effort export used by the transplant write site. Never throws, never blocks.</summary>
        public static void ExportSceneQuiet(Scene scene)
        {
            try
            {
                if (TryExportScene(scene, out string message)) Debug.Log($"{Tag} {message}");
                else Debug.LogWarning($"{Tag} bridge_instances.json NOT updated — {message}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} bridge_instances.json NOT updated — {e.GetType().Name}: {e.Message}");
            }
        }

        private static Instance Capture(GameObject instanceRoot, out string error)
        {
            error = null;
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
            if (string.IsNullOrEmpty(assetPath)) { error = "not a prefab/model instance — nothing to record."; return null; }

            string prefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(prefabGuid)) { error = $"no GUID for '{assetPath}'."; return null; }

            var rec = new Instance { name = instanceRoot.name, sourcePrefabGuid = prefabGuid, sourcePrefabPath = assetPath };

            var mods = PrefabUtility.GetPropertyModifications(instanceRoot);
            if (mods != null)
            {
                foreach (var pm in mods)
                {
                    if (pm.target == null) continue;
                    if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pm.target, out string tg, out long tf)) continue;

                    string og = ""; long of = 0;
                    if (pm.objectReference != null)
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(pm.objectReference, out og, out of);

                    rec.modifications.Add(new Modification
                    {
                        targetGuid      = tg,
                        targetFileId    = tf,
                        propertyPath    = pm.propertyPath,
                        value           = pm.value ?? "",
                        objectRefGuid   = og ?? "",
                        objectRefFileId = of,
                    });
                }
            }
            return rec;
        }

        // ── Rebuild ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the <c>Bridges</c> container of <paramref name="scene"/> from the tracked
        /// catalog. This is the step a machine that just pulled runs — the counterpart of
        /// <c>Import/Standalone Trees/Rebuild Current Hole</c>.
        ///
        /// Deck meshes are NOT rebuilt here; run
        /// <c>Import/Transplant Bridges/Generate Deck Meshes (Current Hole)</c> after this, or
        /// simply keep the tracked <c>MESH_BridgeDeck_*.asset</c> under <c>Data/hole-NN-geo/</c>
        /// that this repo already carries. The message says which.
        /// </summary>
        public static bool TryRebuildScene(Scene scene, out string message)
        {
            message = "";
            if (!scene.IsValid() || !scene.isLoaded) { message = $"scene '{scene.name}' is not loaded."; return false; }
            if (scene.path.Contains("/Video/"))      { message = $"refusing to rebuild INTO a Video scene ('{scene.path}')."; return false; }

            int hole = TreeObstacleBaker.ExtractHoleNumber(scene.name);
            if (hole < 1 || hole > 18) { message = $"cannot detect hole number from scene '{scene.name}'."; return false; }

            string slug = Golfin.Course.Runtime.CourseSlugResolver.Resolve(scene.path);
            if (slug == null) { message = $"cannot resolve course slug from '{scene.path}'."; return false; }

            string assetPath = GetCatalogAssetPath(hole, slug);
            string fullPath  = ToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                message = $"{assetPath} does not exist — run Import/Transplant Bridges/Transplant Hole {hole:D2} "
                        + "on a machine that has the Video scenes, and commit it.";
                return false;
            }

            CatalogFile file;
            try { file = JsonUtility.FromJson<CatalogFile>(File.ReadAllText(fullPath)); }
            catch (Exception e) { message = $"{assetPath}: {e.GetType().Name}: {e.Message}"; return false; }
            if (file == null || file.instances == null) { message = $"{assetPath}: unreadable."; return false; }

            // Resolve every source prefab BEFORE mutating the scene — a missing prefab must not
            // leave the hole with its bridges deleted and nothing put back.
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (var inst in file.instances)
            {
                if (prefabs.ContainsKey(inst.sourcePrefabGuid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(inst.sourcePrefabGuid);
                var asset = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null) missing.Add($"{inst.name} ({inst.sourcePrefabPath}, guid {inst.sourcePrefabGuid})");
                else prefabs[inst.sourcePrefabGuid] = asset;
            }
            if (missing.Count > 0)
            {
                message = $"Hole {hole:D2}: cannot resolve source prefab(s): {string.Join("; ", missing)}. Nothing was changed.";
                return false;
            }

            GameObject container = FindContainer(scene);
            int destroyed = 0;
            if (container != null)
            {
                destroyed = container.transform.childCount;
                for (int i = container.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(container.transform.GetChild(i).gameObject);
            }
            else
            {
                container = new GameObject(BridgeTransplantTool.ContainerName);
                SceneManager.MoveGameObjectToScene(container, scene);
                container.transform.SetParent(null);            // scene ROOT — never under a tree container
                container.transform.position   = Vector3.zero;
                container.transform.rotation   = Quaternion.identity;
                container.transform.localScale = Vector3.one;
            }

            foreach (var inst in file.instances)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[inst.sourcePrefabGuid], scene);
                go.transform.SetParent(container.transform, worldPositionStays: false);

                var mods = new List<PropertyModification>();
                foreach (var m in inst.modifications)
                {
                    var target = ResolveObject(m.targetGuid, m.targetFileId);
                    if (target == null) continue;
                    mods.Add(new PropertyModification
                    {
                        target          = target,
                        propertyPath    = m.propertyPath,
                        value           = m.value,
                        objectReference = string.IsNullOrEmpty(m.objectRefGuid)
                                          ? null : ResolveObject(m.objectRefGuid, m.objectRefFileId),
                    });
                }
                if (mods.Count > 0) PrefabUtility.SetPropertyModifications(go, mods.ToArray());
                go.name = inst.name;
                EditorUtility.SetDirty(go);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            message = $"Hole {hole:D2}: rebuilt {file.instances.Count} bridge instance(s) from {assetPath} "
                    + $"(destroyed {destroyed} pre-existing). Now run "
                    + "Import/Transplant Bridges/Generate Deck Meshes (Current Hole), then SAVE the scene.";
            return true;
        }

        private static UnityEngine.Object ResolveObject(string guid, long fileId)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o == null) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string g, out long f)
                    && f == fileId && g == guid) return o;
            }
            return null;
        }

        public static GameObject FindContainer(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == BridgeTransplantTool.ContainerName) return root;
            return null;
        }
    }
}
#endif
