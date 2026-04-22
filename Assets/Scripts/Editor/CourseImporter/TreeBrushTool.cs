#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.CourseImport
{
    public class TreeBrushTool : EditorWindow
    {
        [MenuItem("Window/Trees/Brush Tool")]
        public static void ShowWindow()
        {
            var w = GetWindow<TreeBrushTool>("Tree Brush");
            w.minSize = new Vector2(280, 360);
        }

        [SerializeField] private bool brushEnabled;
        [SerializeField] private string activeFolder = "All";
        [SerializeField] private float brushRadius = 4f;
        [SerializeField] private int treesPerClick = 5;
        [SerializeField] private bool alignToTerrainNormal = false;
        [SerializeField] private float maxAlignTiltDeg = 15f;
        [SerializeField] private int seed = 0;

        [System.Serializable]
        public class BrushFolderSettings
        {
            public string folder;
            public float minSpacingInCluster = 1.5f;
            public float scaleMin = 0.85f;
            public float scaleMax = 1.15f;
            public float sinkOffset = 0.3f;
        }

        [System.Serializable]
        private class BrushFolderSettingsList
        {
            public List<BrushFolderSettings> items = new List<BrushFolderSettings>();
        }

        [SerializeField] private List<BrushFolderSettings> brushFolderSettings = new List<BrushFolderSettings>();

        private const string EditorPrefsKey = "Golfin.TreeBrush.FolderSettings";
        private const string PaintedContainerName = "PaintedTrees";

        // ─── Settings persistence ──────────────────────────────────────────────

        private BrushFolderSettings GetBrushFolderSettings(string folder)
        {
            if (string.IsNullOrEmpty(folder)) folder = "Root";
            var existing = brushFolderSettings.FirstOrDefault(s => s.folder == folder);
            if (existing != null) return existing;

            // Lazy-create, seed from matching TreePlacer folder settings (or globals for "All")
            FolderPlacementSettings src = (folder == "All")
                ? new FolderPlacementSettings()
                : TreePlacer.GetFolderSettings(folder);
            var created = new BrushFolderSettings
            {
                folder = folder,
                minSpacingInCluster = 1.5f,
                scaleMin = src.scaleMin,
                scaleMax = src.scaleMax,
                sinkOffset = src.sinkOffset,
            };
            brushFolderSettings.Add(created);
            SaveBrushSettings();
            return created;
        }

        private void SaveBrushSettings()
        {
            var wrapper = new BrushFolderSettingsList { items = brushFolderSettings };
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(wrapper));
        }

        private void LoadBrushSettings()
        {
            string json = EditorPrefs.GetString(EditorPrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                var wrapper = JsonUtility.FromJson<BrushFolderSettingsList>(json);
                if (wrapper?.items != null)
                    brushFolderSettings = wrapper.items;
            }
        }

        // ─── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            LoadBrushSettings();
            if (TreePlacer.TreePalette.Count == 0) TreePlacer.ScanPrefabs();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SaveBrushSettings();
        }

        // ─── Editor window UI ─────────────────────────────────────────────────

        private void OnGUI()
        {
            if (brushEnabled)
                EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height),
                    new Color(0.2f, 0.6f, 0.2f, 0.12f));

            EditorGUILayout.Space(4);

            EditorGUI.BeginChangeCheck();
            bool newEnabled = GUILayout.Toggle(brushEnabled,
                brushEnabled ? "Brush ENABLED (B)" : "Brush disabled (B)",
                "Button", GUILayout.Height(28));
            if (EditorGUI.EndChangeCheck())
            {
                brushEnabled = newEnabled;
                SceneView.RepaintAll();
            }

            if (brushEnabled && Terrain.activeTerrain == null)
                EditorGUILayout.HelpBox("No active terrain in this scene.", MessageType.Warning);

            EditorGUILayout.Space(4);

            var folderTabs = TreePlacer.GetFolderTabs();
            if (folderTabs.Count == 0)
            {
                EditorGUILayout.HelpBox("Scan prefabs in TreePlacer first.", MessageType.Info);
            }
            else
            {
                // "All" is always first; individual folders follow for fine control
                var folders = new System.Collections.Generic.List<string> { "All" };
                folders.AddRange(folderTabs);

                EditorGUILayout.BeginHorizontal();
                int folderIdx = Mathf.Max(0, folders.IndexOf(activeFolder));
                EditorGUI.BeginChangeCheck();
                folderIdx = EditorGUILayout.Popup("Folder", folderIdx, folders.ToArray());
                if (EditorGUI.EndChangeCheck())
                    activeFolder = folders[folderIdx];
                if (GUILayout.Button("Refresh", GUILayout.Width(55)))
                    TreePlacer.ScanPrefabs();
                EditorGUILayout.EndHorizontal();

                var activePrefabs = TreePlacer.TreePalette
                    .Where(e => e.enabled &&
                        (activeFolder == "All" || (e.folder ?? "Root") == activeFolder))
                    .ToList();
                if (activePrefabs.Count == 0)
                {
                    EditorGUILayout.HelpBox(
                        "No enabled prefabs — enable some in Tree Settings.",
                        MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField("Active prefabs:", EditorStyles.miniLabel);
                    foreach (var e in activePrefabs)
                        EditorGUILayout.LabelField(
                            $"  {e.name}  weight={e.weight:F1}{(e.standalone ? " (GO)" : "")}",
                            EditorStyles.miniLabel);
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Brush Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            brushRadius = EditorGUILayout.Slider("Brush Radius (m)", brushRadius, 0.5f, 20f);
            treesPerClick = EditorGUILayout.IntSlider("Trees per Click", treesPerClick, 1, 30);
            alignToTerrainNormal = EditorGUILayout.Toggle("Align to Normal", alignToTerrainNormal);
            if (alignToTerrainNormal)
                maxAlignTiltDeg = EditorGUILayout.Slider("Max Tilt (deg)", maxAlignTiltDeg, 0f, 45f);
            seed = EditorGUILayout.IntField(
                new GUIContent("Seed", "0 = random per stroke; non-zero = repeatable"), seed);
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"Brush Settings — {(activeFolder == "All" ? "all folders" : "folder: " + activeFolder)}",
                EditorStyles.boldLabel);

            if (folderTabs.Count > 0)
            {
                var bSettings = GetBrushFolderSettings(activeFolder);
                EditorGUI.BeginChangeCheck();
                bSettings.minSpacingInCluster = EditorGUILayout.Slider(
                    "Min Spacing in Cluster", bSettings.minSpacingInCluster, 0.3f, 5f);
                bSettings.scaleMin = EditorGUILayout.Slider("Scale Min", bSettings.scaleMin, 0.3f, 3f);
                bSettings.scaleMax = EditorGUILayout.Slider("Scale Max", bSettings.scaleMax, 0.3f, 3f);
                bSettings.sinkOffset = EditorGUILayout.Slider("Sink Offset (m)", bSettings.sinkOffset, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                    SaveBrushSettings();

                if (GUILayout.Button("Reset to TreePlacer Defaults"))
                {
                    // For "All", seed from global defaults; for a specific folder, use that folder's settings
                    var src = (activeFolder == "All")
                        ? new FolderPlacementSettings()
                        : TreePlacer.GetFolderSettings(activeFolder);
                    bSettings.scaleMin = src.scaleMin;
                    bSettings.scaleMax = src.scaleMax;
                    bSettings.sinkOffset = src.sinkOffset;
                    bSettings.minSpacingInCluster = 1.5f;
                    SaveBrushSettings();
                }

                EditorGUILayout.HelpBox(
                    "These settings are independent of the importer. Changing them here does NOT affect Trees > Import Trees.",
                    MessageType.None);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                "Erase removes any terrain trees in the radius, including ones placed by TreePlacer. Re-import to restore.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "Terrain trees painted by brush will be wiped by next TreePlacer import. Paint only after final import, or use standalone prefabs.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Clear Painted Trees (this scene)"))
                ClearPaintedTrees();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Hold Shift and click in Scene view to paint a cluster.\nHold Ctrl to erase trees within the brush radius.",
                MessageType.Info);
        }

        private void ClearPaintedTrees()
        {
            var container = FindPaintedContainer();
            if (container != null)
                Undo.DestroyObjectImmediate(container);

            var terrain = Terrain.activeTerrain;
            if (terrain != null)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    terrain.gameObject.scene);
        }

        // ─── Scene view interaction ───────────────────────────────────────────

        private void OnSceneGUI(SceneView sv)
        {
            if (!brushEnabled) return;
            var terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.B)
            {
                brushEnabled = !brushEnabled;
                Repaint();
                Event.current.Use();
                return;
            }

            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            var mouse = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
            RaycastHit hit;
            bool gotHit = UnityEngine.Physics.Raycast(ray, out hit, 5000f);
            if (!gotHit)
            {
                if (!RaycastTerrain(terrain, ray, out hit))
                {
                    sv.Repaint();
                    return;
                }
            }

            var exclusionPolys = TreePlacer.BuildExclusionPolygonsForActiveScene();
            bool overExcluded = TreePlacer.IsBlockedByOverlay(hit.point.x, hit.point.z, exclusionPolys);

            Handles.color =
                overExcluded         ? new Color(1f, 0.55f, 0.1f, 0.8f) :
                Event.current.shift  ? new Color(0.2f, 1f, 0.2f, 0.8f) :
                Event.current.control? new Color(1f, 0.3f, 0.3f, 0.8f) :
                                       new Color(1f, 1f, 1f, 0.5f);
            Handles.DrawWireDisc(hit.point, Vector3.up, brushRadius);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (Event.current.shift)
                {
                    PaintCluster(terrain, hit.point);
                    Event.current.Use();
                }
                else if (Event.current.control)
                {
                    EraseInRadius(terrain, hit.point);
                    Event.current.Use();
                }
            }

            if (Event.current.type == EventType.MouseMove ||
                Event.current.type == EventType.MouseDrag)
                sv.Repaint();
        }

        private bool RaycastTerrain(Terrain terrain, Ray ray, out RaycastHit hit)
        {
            hit = new RaycastHit();
            // If the terrain has a collider, Physics.Raycast already handled it
            if (terrain.GetComponent<Collider>() != null) return false;

            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = terrain.terrainData.size;
            const float stepSize = 0.5f;
            const float maxDist = 5000f;

            Vector3 prev = ray.origin;
            float prevTerrainY = tPos.y + terrain.SampleHeight(ray.origin);
            float prevRayY = ray.origin.y;

            for (float d = stepSize; d < maxDist; d += stepSize)
            {
                Vector3 p = ray.origin + ray.direction * d;
                float lx = p.x - tPos.x;
                float lz = p.z - tPos.z;
                if (lx < 0 || lx > tSize.x || lz < 0 || lz > tSize.z)
                {
                    prev = p; prevRayY = p.y;
                    continue;
                }

                float terrainY = tPos.y + terrain.SampleHeight(p);
                float rayY = p.y;

                if (rayY <= terrainY)
                {
                    float denom = (prevRayY - prevTerrainY) - (rayY - terrainY);
                    float t = Mathf.Approximately(denom, 0f) ? 0.5f :
                              (prevRayY - prevTerrainY) / denom;
                    Vector3 crossPt = Vector3.Lerp(prev, p, t);
                    crossPt.y = tPos.y + terrain.SampleHeight(crossPt);

                    float nx = Mathf.Clamp01((crossPt.x - tPos.x) / tSize.x);
                    float nz = Mathf.Clamp01((crossPt.z - tPos.z) / tSize.z);
                    hit.point = crossPt;
                    hit.normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
                    return true;
                }

                prev = p;
                prevTerrainY = terrainY;
                prevRayY = rayY;
            }
            return false;
        }

        // ─── Paint ────────────────────────────────────────────────────────────

        private void PaintCluster(Terrain terrain, Vector3 center)
        {
            var folderEntries = TreePlacer.TreePalette
                .Where(e => e.enabled && e.weight > 0f &&
                            (activeFolder == "All" || (e.folder ?? "Root") == activeFolder))
                .ToList();
            if (folderEntries.Count == 0)
            {
                Debug.LogWarning("[TreeBrush] No enabled prefabs — enable some in Tree Settings.");
                return;
            }

            var bSettings = GetBrushFolderSettings(activeFolder);
            var exclusionPolys = TreePlacer.BuildExclusionPolygonsForActiveScene();

            var rng = (seed == 0)
                ? new System.Random()
                : new System.Random(seed +
                    Mathf.FloorToInt(center.x * 31f) +
                    Mathf.FloorToInt(center.z * 53f));

            float total = 0f;
            var cum = new float[folderEntries.Count];
            for (int i = 0; i < folderEntries.Count; i++)
            {
                total += folderEntries[i].weight;
                cum[i] = total;
            }
            if (total <= 0f) return;

            var container = GetOrCreatePaintedContainer();
            Undo.RegisterCompleteObjectUndo(container, "Paint Tree Cluster");

            var placed = new List<Vector2>();
            int placedCount = 0;
            int attempts = 0;
            int maxAttempts = treesPerClick * 12;

            var newTerrainTrees = new List<TreeInstance>();
            var tPos = terrain.transform.position;
            var tSize = terrain.terrainData.size;

            while (placedCount < treesPerClick && attempts < maxAttempts)
            {
                attempts++;

                double u = rng.NextDouble();
                double v = rng.NextDouble();
                float r = brushRadius * Mathf.Sqrt((float)u);
                float ang = (float)(v * 2.0 * System.Math.PI);
                float dx = r * Mathf.Cos(ang);
                float dz = r * Mathf.Sin(ang);
                var pos2 = new Vector2(center.x + dx, center.z + dz);

                // Cluster spacing check
                bool tooClose = false;
                for (int i = 0; i < placed.Count; i++)
                {
                    if ((placed[i] - pos2).sqrMagnitude <
                        bSettings.minSpacingInCluster * bSettings.minSpacingInCluster)
                    { tooClose = true; break; }
                }
                if (tooClose) continue;

                // Terrain bounds
                float lx = pos2.x - tPos.x;
                float lz = pos2.y - tPos.z;
                if (lx < 0 || lx > tSize.x || lz < 0 || lz > tSize.z) continue;

                // Overlay exclusion
                if (TreePlacer.IsBlockedByOverlay(pos2.x, pos2.y, exclusionPolys)) continue;

                float terrainH = terrain.SampleHeight(new Vector3(pos2.x, 0, pos2.y));
                float worldY = tPos.y + terrainH - bSettings.sinkOffset;

                float roll = (float)rng.NextDouble() * total;
                int pickIdx = 0;
                for (int i = 0; i < cum.Length; i++)
                    if (roll <= cum[i]) { pickIdx = i; break; }
                var entry = folderEntries[pickIdx];

                float scale = bSettings.scaleMin +
                    (float)rng.NextDouble() * (bSettings.scaleMax - bSettings.scaleMin);
                float rotDeg = (float)rng.NextDouble() * 360f;

                if (entry.standalone)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
                    go.name = $"{entry.name}_brush_{placedCount}";
                    go.transform.SetParent(container.transform, true);
                    go.transform.position = new Vector3(pos2.x, worldY, pos2.y);

                    Vector3 up = Vector3.up;
                    if (alignToTerrainNormal)
                    {
                        Vector3 norm = SampleTerrainNormal(terrain, pos2);
                        float tilt = Vector3.Angle(Vector3.up, norm);
                        if (tilt > maxAlignTiltDeg)
                            norm = Vector3.Slerp(Vector3.up, norm, maxAlignTiltDeg / tilt);
                        up = norm;
                    }
                    go.transform.rotation = Quaternion.AngleAxis(rotDeg, Vector3.up) *
                                            Quaternion.FromToRotation(Vector3.up, up);
                    go.transform.localScale = Vector3.one * scale;

                    foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
                        TreePlacer.NormalizeLODGroup(lg);

                    Undo.RegisterCreatedObjectUndo(go, "Paint Tree");
                }
                else
                {
                    int protoIdx = EnsureTerrainPrototype(terrain, entry.prefab);
                    float ny = Mathf.Max(0f, (terrainH - bSettings.sinkOffset) / tSize.y);
                    float nx = lx / tSize.x;
                    float nz = lz / tSize.z;
                    newTerrainTrees.Add(new TreeInstance
                    {
                        position   = new Vector3(nx, ny, nz),
                        widthScale = scale,
                        heightScale = scale,
                        rotation   = rotDeg * Mathf.Deg2Rad,
                        color      = Color.white,
                        lightmapColor = Color.white,
                        prototypeIndex = protoIdx,
                    });
                }

                placed.Add(pos2);
                placedCount++;
            }

            if (placedCount == 0 && attempts >= maxAttempts)
                Debug.Log("[TreeBrush] No trees placed — entire brush radius may be over " +
                          "an excluded zone (fairway/green/tee/bunker/water/path).");

            if (newTerrainTrees.Count > 0)
            {
                var existing = terrain.terrainData.treeInstances;
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Paint Terrain Trees");
                var combined = new TreeInstance[existing.Length + newTerrainTrees.Count];
                System.Array.Copy(existing, combined, existing.Length);
                for (int i = 0; i < newTerrainTrees.Count; i++)
                    combined[existing.Length + i] = newTerrainTrees[i];
                terrain.terrainData.SetTreeInstances(combined, false);
            }

            EditorUtility.SetDirty(container);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                terrain.gameObject.scene);
        }

        // ─── Erase ────────────────────────────────────────────────────────────

        private void EraseInRadius(Terrain terrain, Vector3 center)
        {
            float r2 = brushRadius * brushRadius;
            int removedGO = 0;
            int removedTerrain = 0;

            var container = FindPaintedContainer();
            if (container != null)
            {
                var toRemove = new List<GameObject>();
                for (int i = 0; i < container.transform.childCount; i++)
                {
                    var child = container.transform.GetChild(i).gameObject;
                    var d = new Vector2(
                        child.transform.position.x - center.x,
                        child.transform.position.z - center.z);
                    if (d.sqrMagnitude <= r2) toRemove.Add(child);
                }
                foreach (var go in toRemove)
                {
                    Undo.DestroyObjectImmediate(go);
                    removedGO++;
                }
            }

            var tPos = terrain.transform.position;
            var tSize = terrain.terrainData.size;
            var instances = terrain.terrainData.treeInstances;
            var kept = new List<TreeInstance>(instances.Length);
            for (int i = 0; i < instances.Length; i++)
            {
                float wx = tPos.x + instances[i].position.x * tSize.x;
                float wz = tPos.z + instances[i].position.z * tSize.z;
                var d = new Vector2(wx - center.x, wz - center.z);
                if (d.sqrMagnitude <= r2) { removedTerrain++; continue; }
                kept.Add(instances[i]);
            }
            if (removedTerrain > 0)
            {
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Erase Terrain Trees");
                terrain.terrainData.SetTreeInstances(kept.ToArray(), false);
            }

            if (removedGO + removedTerrain > 0)
            {
                Debug.Log($"[TreeBrush] Erased {removedGO} GO + {removedTerrain} terrain trees");
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                    terrain.gameObject.scene);
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private GameObject GetOrCreatePaintedContainer()
        {
            var existing = FindPaintedContainer();
            if (existing != null) return existing;

            var go = new GameObject(PaintedContainerName);
            Undo.RegisterCreatedObjectUndo(go, "Create PaintedTrees");

            foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj.name == "HoleRoot" && obj.scene.isLoaded)
                {
                    go.transform.SetParent(obj.transform);
                    break;
                }
            }
            return go;
        }

        private static GameObject FindPaintedContainer()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.name == PaintedContainerName && go.scene.isLoaded)
                    return go;
            return null;
        }

        private static int EnsureTerrainPrototype(Terrain terrain, GameObject prefab)
        {
            var protos = terrain.terrainData.treePrototypes;
            for (int i = 0; i < protos.Length; i++)
                if (protos[i].prefab == prefab) return i;

            var newProtos = new TreePrototype[protos.Length + 1];
            System.Array.Copy(protos, newProtos, protos.Length);
            newProtos[protos.Length] = new TreePrototype { prefab = prefab };
            terrain.terrainData.treePrototypes = newProtos;
            return protos.Length;
        }

        private static Vector3 SampleTerrainNormal(Terrain terrain, Vector2 worldPos)
        {
            var tPos = terrain.transform.position;
            var tSize = terrain.terrainData.size;
            float nx = Mathf.Clamp01((worldPos.x - tPos.x) / tSize.x);
            float nz = Mathf.Clamp01((worldPos.y - tPos.z) / tSize.z);
            return terrain.terrainData.GetInterpolatedNormal(nx, nz);
        }
    }
}
#endif
