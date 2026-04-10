#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Editor window for tweaking TreePlacer parameters and re-importing trees.
    /// Open via GOLFIN > Tree Settings.
    /// </summary>
    public class TreePlacerWindow : EditorWindow
    {
        private Vector2 treeScrollPos;

        [MenuItem("GOLFIN/Tree Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<TreePlacerWindow>("Tree Settings");
            window.minSize = new Vector2(420, 520);
        }

        private void OnEnable()
        {
            if (TreePlacer.TreePalette.Count == 0)
                TreePlacer.ScanPrefabs();
        }

        private void OnGUI()
        {
            // ---- Tree Palette ----
            GUILayout.Label("Tree Palette", EditorStyles.boldLabel);

            if (GUILayout.Button("Rescan Prefab Folders", GUILayout.Height(20)))
                TreePlacer.ScanPrefabs();

            EditorGUILayout.Space(4);

            // Column headers
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("On", EditorStyles.miniLabel, GUILayout.Width(24));
            GUILayout.Label("Prefab", EditorStyles.miniLabel, GUILayout.MinWidth(140));
            GUILayout.Label("Weight", EditorStyles.miniLabel, GUILayout.Width(50));
            GUILayout.Label("GO", EditorStyles.miniLabel, GUILayout.Width(24));
            GUILayout.Label("LOD", EditorStyles.miniLabel, GUILayout.Width(28));
            EditorGUILayout.EndHorizontal();

            // Scrollable list
            treeScrollPos = EditorGUILayout.BeginScrollView(treeScrollPos,
                GUILayout.MinHeight(150), GUILayout.MaxHeight(350));

            foreach (var entry in TreePlacer.TreePalette)
            {
                EditorGUILayout.BeginHorizontal();

                entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(20));

                var nameStyle = entry.enabled ? EditorStyles.label : EditorStyles.miniLabel;
                GUILayout.Label(entry.name, nameStyle, GUILayout.MinWidth(140));

                EditorGUI.BeginDisabledGroup(!entry.enabled);
                entry.weight = EditorGUILayout.FloatField(entry.weight, GUILayout.Width(50));
                if (entry.weight < 0f) entry.weight = 0f;

                // Standalone toggle (GO = GameObject)
                entry.standalone = EditorGUILayout.Toggle(entry.standalone, GUILayout.Width(20));
                EditorGUI.EndDisabledGroup();

                // LOD indicator (read-only)
                GUILayout.Label(entry.hasLODGroup ? "\u2713" : "\u2013",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(24));

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // Summary
            var active = TreePlacer.GetActiveEntries();
            int terrainCount = 0, standaloneCount = 0;
            foreach (var e in active)
            {
                if (e.standalone) standaloneCount++;
                else terrainCount++;
            }
            EditorGUILayout.LabelField(
                $"{active.Count} enabled ({terrainCount} terrain, {standaloneCount} standalone) / {TreePlacer.TreePalette.Count} total",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(10);

            // ---- Placement ----
            GUILayout.Label("Placement", EditorStyles.boldLabel);
            TreePlacer.MinSpacing = EditorGUILayout.FloatField("Min Spacing (m)", TreePlacer.MinSpacing);
            TreePlacer.ScaleMin = EditorGUILayout.FloatField("Scale Min", TreePlacer.ScaleMin);
            TreePlacer.ScaleMax = EditorGUILayout.FloatField("Scale Max", TreePlacer.ScaleMax);
            TreePlacer.SinkOffset = EditorGUILayout.Slider("Sink Offset (m)", TreePlacer.SinkOffset, 0f, 2f);

            EditorGUILayout.Space(10);

            // ---- Draw Distances ----
            GUILayout.Label("Draw Distances", EditorStyles.boldLabel);
            TreePlacer.DrawDistance = EditorGUILayout.FloatField("Max Draw Distance (m)", TreePlacer.DrawDistance);
            TreePlacer.BillboardDistance = EditorGUILayout.FloatField("Billboard Distance (m)", TreePlacer.BillboardDistance);
            TreePlacer.CrossFadeLength = EditorGUILayout.FloatField("Cross Fade Length (m)", TreePlacer.CrossFadeLength);
            TreePlacer.MaxFullLODCount = EditorGUILayout.IntField("Max Full LOD Count", TreePlacer.MaxFullLODCount);

            EditorGUILayout.Space(10);

            // ---- LOD Thresholds ----
            GUILayout.Label("LOD Thresholds (screen %)", EditorStyles.boldLabel);
            TreePlacer.LOD0Threshold = EditorGUILayout.Slider("LOD 0 \u2192 1", TreePlacer.LOD0Threshold, 0.01f, 0.5f);
            TreePlacer.LOD1Threshold = EditorGUILayout.Slider("LOD 1 \u2192 2", TreePlacer.LOD1Threshold, 0.005f, 0.2f);
            TreePlacer.LOD2Threshold = EditorGUILayout.Slider("LOD 2 \u2192 Cull", TreePlacer.LOD2Threshold, 0.001f, 0.1f);

            EditorGUILayout.Space(15);

            // ---- Actions ----
            EditorGUILayout.HelpBox(
                "GO = standalone GameObject (particles, complex hierarchy).\n" +
                "LOD = \u2713 if prefab has LODGroup on root.\n" +
                "Prefabs without root LODGroup auto-default to standalone.",
                MessageType.Info);

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Re-import Trees", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("GOLFIN/Import Trees (Current Hole)");
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Apply Draw Distances Only"))
            {
                var terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    terrain.treeDistance = TreePlacer.DrawDistance;
                    terrain.treeBillboardDistance = TreePlacer.BillboardDistance;
                    terrain.treeCrossFadeLength = TreePlacer.CrossFadeLength;
                    terrain.treeMaximumFullLODCount = TreePlacer.MaxFullLODCount;
                    Debug.Log($"[TreePlacer] Draw distances applied: " +
                        $"draw={terrain.treeDistance}m, billboard={terrain.treeBillboardDistance}m");
                }
                else
                {
                    Debug.LogError("[TreePlacer] No active terrain found");
                }
            }
        }
    }
}
#endif
