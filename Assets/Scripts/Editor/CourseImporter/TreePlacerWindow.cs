#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Editor window for tweaking TreePlacer parameters and re-importing trees.
    /// Open via Trees > Tree Settings.
    /// </summary>
    public class TreePlacerWindow : EditorWindow
    {
        private const string AllTab = "All";

        private Vector2 treeScrollPos;
        private string currentTab = AllTab;
        private bool goOnly;

        [MenuItem("Trees/Tree Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<TreePlacerWindow>("Tree Settings");
            window.minSize = new Vector2(420, 620);
        }

        private void OnEnable()
        {
            if (TreePlacer.TreePalette.Count == 0)
                TreePlacer.ScanPrefabs();
        }

        /// <summary>
        /// Toggle `enabled` on all prefabs that match the current tab + GO filter.
        /// On the All tab this affects every prefab matching the GO filter.
        /// </summary>
        private void SetAllInScope(bool value)
        {
            int n = 0;
            foreach (var entry in TreePlacer.TreePalette)
            {
                string entryFolder = string.IsNullOrEmpty(entry.folder) ? "Root" : entry.folder;
                if (currentTab != AllTab && entryFolder != currentTab) continue;
                if (goOnly && !entry.standalone) continue;
                entry.enabled = value;
                n++;
            }
            Debug.Log($"[TreePlacer] {(value ? "Enabled" : "Disabled")} {n} prefab(s) " +
                $"in tab '{currentTab}'{(goOnly ? " (GO only)" : "")}");
        }

        private void OnGUI()
        {
            // ---- Tabs: All + folders with prefabs ----
            var folders = TreePlacer.GetFolderTabs();
            var tabs = new List<string> { AllTab };
            tabs.AddRange(folders);

            int currentIdx = Mathf.Max(0, tabs.IndexOf(currentTab));
            int newIdx = GUILayout.Toolbar(currentIdx, tabs.ToArray());
            if (newIdx != currentIdx || currentIdx >= tabs.Count)
                currentTab = tabs[newIdx];

            EditorGUILayout.Space(4);

            // ---- Tree Palette header ----
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Tree Palette", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("All On", EditorStyles.miniButtonLeft, GUILayout.Width(55)))
                SetAllInScope(true);
            if (GUILayout.Button("All Off", EditorStyles.miniButtonRight, GUILayout.Width(55)))
                SetAllInScope(false);
            GUILayout.Space(8);
            goOnly = GUILayout.Toggle(goOnly, "GO only", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();

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

            // Scrollable palette filtered by tab + GO-only
            treeScrollPos = EditorGUILayout.BeginScrollView(treeScrollPos,
                GUILayout.MinHeight(150), GUILayout.MaxHeight(320));

            int shownCount = 0;
            foreach (var entry in TreePlacer.TreePalette)
            {
                string entryFolder = string.IsNullOrEmpty(entry.folder) ? "Root" : entry.folder;
                if (currentTab != AllTab && entryFolder != currentTab) continue;
                if (goOnly && !entry.standalone) continue;

                EditorGUILayout.BeginHorizontal();

                entry.enabled = EditorGUILayout.Toggle(entry.enabled, GUILayout.Width(20));

                var nameStyle = entry.enabled ? EditorStyles.label : EditorStyles.miniLabel;
                string label = (currentTab == AllTab)
                    ? $"{entry.name}  [{entryFolder}]"
                    : entry.name;
                GUILayout.Label(label, nameStyle, GUILayout.MinWidth(140));

                EditorGUI.BeginDisabledGroup(!entry.enabled);
                entry.weight = EditorGUILayout.FloatField(entry.weight, GUILayout.Width(50));
                if (entry.weight < 0f) entry.weight = 0f;

                entry.standalone = EditorGUILayout.Toggle(entry.standalone, GUILayout.Width(20));
                EditorGUI.EndDisabledGroup();

                GUILayout.Label(entry.hasLODGroup ? "\u2713" : "\u2013",
                    EditorStyles.centeredGreyMiniLabel, GUILayout.Width(24));

                EditorGUILayout.EndHorizontal();
                shownCount++;
            }

            EditorGUILayout.EndScrollView();

            // Summary (counts for the current filter)
            var active = TreePlacer.GetActiveEntries();
            int terrainCount = 0, standaloneCount = 0;
            foreach (var e in active)
            {
                if (e.standalone) standaloneCount++;
                else terrainCount++;
            }
            EditorGUILayout.LabelField(
                $"Showing {shownCount} / {TreePlacer.TreePalette.Count}   |   " +
                $"{active.Count} enabled ({terrainCount} terrain, {standaloneCount} standalone)",
                EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(10);

            // ---- Placement (per-tab) ----
            GUILayout.Label($"Placement — {currentTab}", EditorStyles.boldLabel);
            if (currentTab == AllTab)
            {
                EditorGUILayout.HelpBox(
                    "Placement values are per-folder. Select a folder tab to edit " +
                    "its Min Spacing, Scale range, and Sink Offset.",
                    MessageType.Info);
            }
            else
            {
                var fs = TreePlacer.GetFolderSettings(currentTab);
                fs.minSpacing = EditorGUILayout.FloatField("Min Spacing (m)", fs.minSpacing);
                fs.scaleMin = EditorGUILayout.FloatField("Scale Min", fs.scaleMin);
                fs.scaleMax = EditorGUILayout.FloatField("Scale Max", fs.scaleMax);
                fs.sinkOffset = EditorGUILayout.Slider("Sink Offset (m)", fs.sinkOffset, 0f, 2f);
            }

            EditorGUILayout.Space(10);

            // ---- Draw Distances (universal) ----
            GUILayout.Label("Draw Distances", EditorStyles.boldLabel);
            TreePlacer.DrawDistance = EditorGUILayout.FloatField("Max Draw Distance (m)", TreePlacer.DrawDistance);
            TreePlacer.BillboardDistance = EditorGUILayout.FloatField("Billboard Distance (m)", TreePlacer.BillboardDistance);
            TreePlacer.CrossFadeLength = EditorGUILayout.FloatField("Cross Fade Length (m)", TreePlacer.CrossFadeLength);
            TreePlacer.MaxFullLODCount = EditorGUILayout.IntField("Max Full LOD Count", TreePlacer.MaxFullLODCount);

            EditorGUILayout.Space(10);

            // ---- LOD Thresholds (universal) ----
            GUILayout.Label("LOD Thresholds (screen %)", EditorStyles.boldLabel);
            TreePlacer.LOD0Threshold = EditorGUILayout.Slider("LOD 0 \u2192 1", TreePlacer.LOD0Threshold, 0.01f, 0.5f);
            TreePlacer.LOD1Threshold = EditorGUILayout.Slider("LOD 1 \u2192 2", TreePlacer.LOD1Threshold, 0.005f, 0.2f);
            TreePlacer.LOD2Threshold = EditorGUILayout.Slider("LOD 2 \u2192 Cull", TreePlacer.LOD2Threshold, 0.001f, 0.1f);

            EditorGUILayout.Space(15);

            // ---- Save / Load ----
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save Preset"))
                TreePlacer.SavePreset();
            if (GUILayout.Button("Load Preset"))
            {
                TreePlacer.LoadPreset();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // ---- Info + Actions ----
            EditorGUILayout.HelpBox(
                "Tabs filter palette by folder under Trees2025_Prefabs.\n" +
                "GO only = show standalone prefabs (GO column checked).\n" +
                "Placement values are per-folder; draw distances and LOD are universal.",
                MessageType.Info);

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Re-import Trees", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("Trees/Import Trees Current Hole");
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
