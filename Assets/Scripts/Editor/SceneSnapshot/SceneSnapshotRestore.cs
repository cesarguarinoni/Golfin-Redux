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
    public sealed class RestoreReport
    {
        public List<ReportEntry> Updated = new List<ReportEntry>();
        public List<ReportEntry> Created = new List<ReportEntry>();
        public List<ReportEntry> Skipped = new List<ReportEntry>();
        public List<ReportEntry> Failed  = new List<ReportEntry>();
    }

    public sealed class ReportEntry
    {
        public string Guid;
        public string Name;
        public string Reason;
    }

    public static class SceneSnapshotRestore
    {
        /// <summary>Loads a snapshot from &lt;scenePath without .unity&gt;.manual.json, returns null if not found.</summary>
        public static SceneSnapshotData Load(string scenePath)
        {
            string jsonPath = SceneSnapshotCapture.GetSnapshotPath(scenePath);
            if (!File.Exists(jsonPath)) return null;

            string json = File.ReadAllText(jsonPath);
            SceneSnapshotData snap;
            try
            {
                snap = JsonUtility.FromJson<SceneSnapshotData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneSnapshot] Failed to parse snapshot JSON: {ex.Message}");
                throw;
            }

            if (snap.SchemaVersion != "1")
                throw new InvalidOperationException(
                    $"[SceneSnapshot] Unsupported schema version '{snap.SchemaVersion}'. Update the tool.");

            return snap;
        }

        /// <summary>Restores manually-placed content from snapshot into the scene.</summary>
        public static RestoreReport Restore(Scene scene, SceneSnapshotData snap)
        {
            var report = new RestoreReport();

            // Build GUID → existing GameObject map
            var guidMap = new Dictionary<string, GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                CollectGuids(root, guidMap);

            // Topological sort: entries with no ParentGuid first, then children
            var sorted = TopologicalSort(snap.Props);

            // Track GUIDs resolved during this restore (for parent lookup of new objects)
            var resolvedByGuid = new Dictionary<string, GameObject>(guidMap);

            // Pass 1: GameObjects
            foreach (var entry in sorted)
            {
                if (guidMap.TryGetValue(entry.Guid, out var existing))
                {
                    // Update transform + active state only
                    Undo.RecordObject(existing.transform, "Snapshot Restore Transform");
                    Undo.RecordObject(existing, "Snapshot Restore Active");

                    existing.transform.localPosition = entry.Transform.Position;
                    existing.transform.localEulerAngles = entry.Transform.EulerAngles;
                    existing.transform.localScale = entry.Transform.LocalScale;
                    existing.SetActive(entry.ActiveSelf);

                    EditorUtility.SetDirty(existing);
                    report.Updated.Add(new ReportEntry { Guid = entry.Guid, Name = entry.Name });
                }
                else
                {
                    // Attempt to create from prefab
                    GameObject spawned = TryInstantiatePrefab(entry, scene, resolvedByGuid, report);
                    if (spawned != null)
                    {
                        resolvedByGuid[entry.Guid] = spawned;
                        report.Created.Add(new ReportEntry { Guid = entry.Guid, Name = entry.Name });
                    }
                }
            }

            // Scene GUIDs not in snapshot → Skipped
            foreach (var kvp in guidMap)
            {
                if (!snap.Props.Any(p => p.Guid == kvp.Key))
                {
                    report.Skipped.Add(new ReportEntry
                    {
                        Guid = kvp.Key,
                        Name = kvp.Value != null ? kvp.Value.name : "(destroyed)",
                        Reason = "GUID not in snapshot (added after last capture)"
                    });
                }
            }

            // Pass 2: Terrain
            if (snap.Terrain != null)
                RestoreTerrain(scene, snap.Terrain, report);

            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log($"[SceneSnapshot] Restore complete — Updated:{report.Updated.Count} " +
                      $"Created:{report.Created.Count} Skipped:{report.Skipped.Count} Failed:{report.Failed.Count}");

            return report;
        }

        // ─── Internal helpers ────────────────────────────────────────

        private static void CollectGuids(GameObject go, Dictionary<string, GameObject> map)
        {
            var propId = go.GetComponent<ManualPropId>();
            if (propId != null && !string.IsNullOrEmpty(propId.Guid))
                map[propId.Guid] = go;

            for (int i = 0; i < go.transform.childCount; i++)
                CollectGuids(go.transform.GetChild(i).gameObject, map);
        }

        private static List<PropEntry> TopologicalSort(List<PropEntry> props)
        {
            // Roots first (no ParentGuid), then children. Multi-level handled by iterative pass.
            var result = new List<PropEntry>(props.Count);
            var remaining = new List<PropEntry>(props);
            var inResult = new HashSet<string>();

            int maxPasses = props.Count + 1;
            while (remaining.Count > 0 && maxPasses-- > 0)
            {
                var nextPass = new List<PropEntry>();
                foreach (var e in remaining)
                {
                    if (string.IsNullOrEmpty(e.ParentGuid) || inResult.Contains(e.ParentGuid))
                    {
                        result.Add(e);
                        inResult.Add(e.Guid);
                    }
                    else
                    {
                        nextPass.Add(e);
                    }
                }
                if (nextPass.Count == remaining.Count)
                {
                    // No progress — circular reference or missing parent; add remainder as-is
                    result.AddRange(nextPass);
                    break;
                }
                remaining = nextPass;
            }
            return result;
        }

        private static GameObject TryInstantiatePrefab(
            PropEntry entry,
            Scene scene,
            Dictionary<string, GameObject> resolvedByGuid,
            RestoreReport report)
        {
            if (string.IsNullOrEmpty(entry.PrefabAssetGuid))
            {
                report.Failed.Add(new ReportEntry
                {
                    Guid = entry.Guid,
                    Name = entry.Name,
                    Reason = "non-prefab original; cannot recreate"
                });
                return null;
            }

            string prefabPath = AssetDatabase.GUIDToAssetPath(entry.PrefabAssetGuid);
            if (string.IsNullOrEmpty(prefabPath))
            {
                report.Failed.Add(new ReportEntry
                {
                    Guid = entry.Guid,
                    Name = entry.Name,
                    Reason = $"prefab asset missing: {entry.PrefabAssetGuid}"
                });
                return null;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                report.Failed.Add(new ReportEntry
                {
                    Guid = entry.Guid,
                    Name = entry.Name,
                    Reason = $"prefab could not be loaded: {prefabPath}"
                });
                return null;
            }

            var spawned = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(spawned, "Snapshot Restore Create");

            // Stamp GUID
            var propId = spawned.GetComponent<ManualPropId>();
            if (propId == null)
                propId = spawned.AddComponent<ManualPropId>();
            propId.SetGuidEditor(entry.Guid);

            // Set transform
            spawned.transform.localPosition = entry.Transform.Position;
            spawned.transform.localEulerAngles = entry.Transform.EulerAngles;
            spawned.transform.localScale = entry.Transform.LocalScale;
            spawned.SetActive(entry.ActiveSelf);

            // Reparent if parent guid is resolved
            if (!string.IsNullOrEmpty(entry.ParentGuid) &&
                resolvedByGuid.TryGetValue(entry.ParentGuid, out var parent))
            {
                spawned.transform.SetParent(parent.transform, worldPositionStays: false);
            }

            EditorUtility.SetDirty(spawned);
            return spawned;
        }

        private static void RestoreTerrain(Scene scene, TerrainSnapshot terrainSnap, RestoreReport report)
        {
            Terrain terrain = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                terrain = root.GetComponentInChildren<Terrain>(true);
                if (terrain != null) break;
            }

            if (terrain == null)
            {
                Debug.LogWarning("[SceneSnapshot] Terrain snapshot present but no Terrain found in scene.");
                return;
            }

            var td = terrain.terrainData;
            Undo.RecordObject(td, "Snapshot Restore Terrain");

            // Trees: rebuild prototype-index remap
            var protoRemap = BuildTreeProtoRemap(td, terrainSnap.TreeInstances);
            var newInstances = new TreeInstance[terrainSnap.TreeInstances.Count];
            for (int i = 0; i < terrainSnap.TreeInstances.Count; i++)
            {
                var src = terrainSnap.TreeInstances[i];
                if (!protoRemap.TryGetValue(src.PrototypeIndex, out int remapped))
                {
                    Debug.LogWarning($"[SceneSnapshot] Tree prototype index {src.PrototypeIndex} " +
                                     $"(guid={src.PrototypePrefabGuid}) could not be remapped — skipping instance.");
                    continue;
                }
                newInstances[i] = new TreeInstance
                {
                    position      = src.Position,
                    widthScale    = src.WidthScale,
                    heightScale   = src.HeightScale,
                    rotation      = src.Rotation,
                    color         = src.Color,
                    lightmapColor = src.LightmapColor,
                    prototypeIndex = remapped
                };
            }
            td.SetTreeInstances(newInstances, true);

            // Detail layers
            var detailRemap = BuildDetailProtoRemap(td, terrainSnap.DetailLayers);
            foreach (var layer in terrainSnap.DetailLayers)
            {
                if (!detailRemap.TryGetValue(layer.PrototypeIndex, out int remapped))
                {
                    Debug.LogWarning($"[SceneSnapshot] Detail prototype index {layer.PrototypeIndex} " +
                                     $"(guid={layer.PrototypePrefabGuid}) could not be remapped — skipping layer.");
                    continue;
                }

                int w = layer.Width, h = layer.Height;
                int[,] grid = new int[h, w];
                for (int row = 0; row < h; row++)
                    for (int col = 0; col < w; col++)
                        grid[row, col] = layer.Densities[row * w + col];

                td.SetDetailLayer(0, 0, remapped, grid);
            }

            EditorUtility.SetDirty(td);
        }

        private static Dictionary<int, int> BuildTreeProtoRemap(TerrainData td, List<TreeInstanceData> instances)
        {
            var remap = new Dictionary<int, int>();
            var protos = td.treePrototypes;

            // Build GUID → current index
            var guidToCurrentIdx = new Dictionary<string, int>();
            for (int i = 0; i < protos.Length; i++)
            {
                if (protos[i].prefab == null) continue;
                string path = AssetDatabase.GetAssetPath(protos[i].prefab);
                if (!string.IsNullOrEmpty(path))
                    guidToCurrentIdx[AssetDatabase.AssetPathToGUID(path)] = i;
            }

            // For each snapshot proto index, find the current index
            var snapshotProtoGuids = new Dictionary<int, string>();
            foreach (var ti in instances)
                if (!snapshotProtoGuids.ContainsKey(ti.PrototypeIndex))
                    snapshotProtoGuids[ti.PrototypeIndex] = ti.PrototypePrefabGuid;

            foreach (var kvp in snapshotProtoGuids)
            {
                if (!string.IsNullOrEmpty(kvp.Value) && guidToCurrentIdx.TryGetValue(kvp.Value, out int cur))
                    remap[kvp.Key] = cur;
                else if (kvp.Key < protos.Length)
                    remap[kvp.Key] = kvp.Key; // fallback: same index
            }
            return remap;
        }

        private static Dictionary<int, int> BuildDetailProtoRemap(TerrainData td, List<DetailLayerData> layers)
        {
            var remap = new Dictionary<int, int>();
            var protos = td.detailPrototypes;

            var guidToCurrentIdx = new Dictionary<string, int>();
            for (int i = 0; i < protos.Length; i++)
            {
                string path = null;
                if (protos[i].prototype != null)
                    path = AssetDatabase.GetAssetPath(protos[i].prototype);
                else if (protos[i].prototypeTexture != null)
                    path = AssetDatabase.GetAssetPath(protos[i].prototypeTexture);
                if (!string.IsNullOrEmpty(path))
                    guidToCurrentIdx[AssetDatabase.AssetPathToGUID(path)] = i;
            }

            foreach (var layer in layers)
            {
                if (!string.IsNullOrEmpty(layer.PrototypePrefabGuid) &&
                    guidToCurrentIdx.TryGetValue(layer.PrototypePrefabGuid, out int cur))
                    remap[layer.PrototypeIndex] = cur;
                else if (layer.PrototypeIndex < protos.Length)
                    remap[layer.PrototypeIndex] = layer.PrototypeIndex;
            }
            return remap;
        }
    }
}
#endif
