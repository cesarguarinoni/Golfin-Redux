#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.SceneSnapshot
{
    public static class SceneSnapshotCapture
    {
        // Root name prefixes that indicate importer-generated objects
        private static readonly string[] ImporterPrefixes = { "Hole_", "Generated_", "Procedural_", "ProcGen_" };

        // Exact root names used by the importer pipeline
        private static readonly HashSet<string> ImporterExactRoots = new HashSet<string>
        {
            "Terrain", "HoleGeo", "HoleGeo_Flat", "Splines",
            "ZoneMeshes", "Greens", "Bunkers", "Water", "CartPaths",
            "Tees", "Fairways", "Rough", "Semirough", "LabRoot"
        };

        // Namespace prefixes that indicate importer-generated components
        private static readonly string[] ImporterNamespaces = { "Golfin.Course", "Golfin.Physics.Runtime" };

        public enum ClassificationResult { Manual, ImporterPrefix, ImporterExact, ImporterNamespace, ImporterExtra }

        public sealed class RootAuditEntry
        {
            public string Name;
            public ClassificationResult Result;
            public string MatchedRule;
        }

        /// <summary>Returns audit of how each scene root would be classified without committing anything.</summary>
        public static List<RootAuditEntry> AuditRoots(Scene scene, IReadOnlyList<string> extraImporterRoots)
        {
            var results = new List<RootAuditEntry>();
            foreach (var root in scene.GetRootGameObjects())
            {
                var entry = new RootAuditEntry { Name = root.name };
                ClassifyRoot(root, extraImporterRoots, out entry.Result, out entry.MatchedRule);
                results.Add(entry);
            }
            return results;
        }

        /// <summary>Captures all manually-placed content in the scene to a SceneSnapshot.</summary>
        public static SceneSnapshotData Capture(Scene scene, IReadOnlyList<string> extraImporterRoots)
        {
            var snap = new SceneSnapshotData
            {
                SchemaVersion = "1",
                SceneName = scene.name,
                CapturedAtIso = DateTime.UtcNow.ToString("o")
            };

            bool anyNewGuid = false;

            foreach (var root in scene.GetRootGameObjects())
            {
                ClassifyRoot(root, extraImporterRoots, out var result, out _);
                if (result != ClassificationResult.Manual)
                    continue;

                CollectProps(root, "", snap.Props, ref anyNewGuid);
            }

            // Terrain: captured separately (not as a GameObject entry)
            var terrain = FindTerrain(scene);
            if (terrain != null)
                snap.Terrain = CaptureTerrain(terrain);

            if (anyNewGuid)
                EditorSceneManager.MarkSceneDirty(scene);

            return snap;
        }

        /// <summary>Writes snapshot to &lt;scenePath without .unity&gt;.manual.json with a .bak backup.</summary>
        public static void Save(SceneSnapshotData snap, string scenePath)
        {
            string jsonPath = GetSnapshotPath(scenePath);
            string json = JsonUtility.ToJson(snap, true);

            if (File.Exists(jsonPath))
                File.Copy(jsonPath, jsonPath + ".bak", overwrite: true);

            File.WriteAllText(jsonPath, json);
        }

        /// <summary>Returns the snapshot JSON path for a given scene path.</summary>
        public static string GetSnapshotPath(string scenePath)
        {
            string dir = Path.GetDirectoryName(scenePath);
            string name = Path.GetFileNameWithoutExtension(scenePath);
            return Path.Combine(dir, name + ".manual.json");
        }

        /// <summary>
        /// Returns true if saving would wipe a non-empty existing snapshot.
        /// Used by the editor window to prompt confirmation.
        /// </summary>
        public static bool WouldWipeSnapshot(SceneSnapshotData incoming, string scenePath)
        {
            string path = GetSnapshotPath(scenePath);
            if (!File.Exists(path)) return false;
            if (incoming.Props.Count > 0 || incoming.Terrain != null) return false;

            try
            {
                var existing = JsonUtility.FromJson<SceneSnapshotData>(File.ReadAllText(path));
                return existing != null && (existing.Props.Count > 0 || existing.Terrain != null);
            }
            catch { return false; }
        }

        // ─── Internal helpers ─────────────────────────────────────────

        private static void ClassifyRoot(
            GameObject root,
            IReadOnlyList<string> extraRoots,
            out ClassificationResult result,
            out string matchedRule)
        {
            string name = root.name;

            foreach (var prefix in ImporterPrefixes)
            {
                if (name.StartsWith(prefix))
                {
                    result = ClassificationResult.ImporterPrefix;
                    matchedRule = $"name starts with '{prefix}'";
                    return;
                }
            }

            if (ImporterExactRoots.Contains(name))
            {
                result = ClassificationResult.ImporterExact;
                matchedRule = $"exact name '{name}'";
                return;
            }

            if (HasImporterNamespaceComponent(root))
            {
                result = ClassificationResult.ImporterNamespace;
                matchedRule = "has Golfin.Course / Golfin.Physics.Runtime component";
                return;
            }

            if (extraRoots != null)
            {
                foreach (var extra in extraRoots)
                {
                    if (!string.IsNullOrEmpty(extra) && name == extra)
                    {
                        result = ClassificationResult.ImporterExtra;
                        matchedRule = $"extra importer root '{extra}'";
                        return;
                    }
                }
            }

            result = ClassificationResult.Manual;
            matchedRule = "none — manual";
        }

        private static bool HasImporterNamespaceComponent(GameObject go)
        {
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                string ns = comp.GetType().Namespace ?? "";
                foreach (var importerNs in ImporterNamespaces)
                {
                    if (ns.StartsWith(importerNs))
                        return true;
                }
            }
            return false;
        }

        private static void CollectProps(
            GameObject go,
            string parentGuid,
            List<PropEntry> props,
            ref bool anyNewGuid)
        {
            var propId = go.GetComponent<ManualPropId>();
            if (propId == null)
            {
                propId = go.AddComponent<ManualPropId>();
                Undo.RegisterCreatedObjectUndo(propId, "Stamp ManualPropId");
            }

            bool wasEmpty = string.IsNullOrEmpty(propId.Guid);
            propId.EnsureGuid();
            if (wasEmpty)
            {
                anyNewGuid = true;
                EditorUtility.SetDirty(go);
            }

            string prefabGuid = "";
            var prefabSource = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            if (prefabSource != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(prefabSource);
                if (!string.IsNullOrEmpty(prefabPath))
                    prefabGuid = AssetDatabase.AssetPathToGUID(prefabPath);
            }

            var entry = new PropEntry
            {
                Guid = propId.Guid,
                Name = go.name,
                PrefabAssetGuid = prefabGuid,
                ParentGuid = parentGuid,
                Transform = new TransformData
                {
                    Position = go.transform.localPosition,
                    EulerAngles = go.transform.localEulerAngles,
                    LocalScale = go.transform.localScale
                },
                ActiveSelf = go.activeSelf,
                TagAndLayer = go.tag + "|" + go.layer
            };
            props.Add(entry);

            // Recurse into children
            for (int i = 0; i < go.transform.childCount; i++)
                CollectProps(go.transform.GetChild(i).gameObject, propId.Guid, props, ref anyNewGuid);
        }

        private static Terrain FindTerrain(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.GetComponentInChildren<Terrain>(true);
                if (t != null) return t;
            }
            return null;
        }

        private static TerrainSnapshot CaptureTerrain(Terrain terrain)
        {
            var td = terrain.terrainData;
            var snap = new TerrainSnapshot { TerrainObjectName = terrain.name };

            // Tree instances
            foreach (var ti in td.treeInstances)
            {
                string protoPrefabGuid = "";
                if (ti.prototypeIndex >= 0 && ti.prototypeIndex < td.treePrototypes.Length)
                {
                    var proto = td.treePrototypes[ti.prototypeIndex];
                    if (proto.prefab != null)
                    {
                        string path = AssetDatabase.GetAssetPath(proto.prefab);
                        if (!string.IsNullOrEmpty(path))
                            protoPrefabGuid = AssetDatabase.AssetPathToGUID(path);
                    }
                }
                snap.TreeInstances.Add(new TreeInstanceData
                {
                    Position = ti.position,
                    WidthScale = ti.widthScale,
                    HeightScale = ti.heightScale,
                    Rotation = ti.rotation,
                    Color = ti.color,
                    LightmapColor = ti.lightmapColor,
                    PrototypeIndex = ti.prototypeIndex,
                    PrototypePrefabGuid = protoPrefabGuid
                });
            }

            // Detail layers
            int w = td.detailWidth;
            int h = td.detailHeight;
            for (int i = 0; i < td.detailPrototypes.Length; i++)
            {
                string protoPrefabGuid = "";
                var proto = td.detailPrototypes[i];
                // Detail prototypes may use a prefab or a texture; try prefab first
                if (proto.prototype != null)
                {
                    string path = AssetDatabase.GetAssetPath(proto.prototype);
                    if (!string.IsNullOrEmpty(path))
                        protoPrefabGuid = AssetDatabase.AssetPathToGUID(path);
                }
                else if (proto.prototypeTexture != null)
                {
                    string path = AssetDatabase.GetAssetPath(proto.prototypeTexture);
                    if (!string.IsNullOrEmpty(path))
                        protoPrefabGuid = AssetDatabase.AssetPathToGUID(path);
                }

                int[,] layer = td.GetDetailLayer(0, 0, w, h, i);
                int[] flat = new int[w * h];
                for (int row = 0; row < h; row++)
                    for (int col = 0; col < w; col++)
                        flat[row * w + col] = layer[row, col];

                snap.DetailLayers.Add(new DetailLayerData
                {
                    PrototypeIndex = i,
                    PrototypePrefabGuid = protoPrefabGuid,
                    Width = w,
                    Height = h,
                    Densities = flat
                });
            }

            return snap;
        }
    }
}
#endif
