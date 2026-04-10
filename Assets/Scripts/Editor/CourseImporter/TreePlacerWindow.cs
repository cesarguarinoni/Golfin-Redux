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
        [MenuItem("GOLFIN/Tree Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<TreePlacerWindow>("Tree Settings");
            window.minSize = new Vector2(300, 380);
        }

        private void OnGUI()
        {
            GUILayout.Label("Placement", EditorStyles.boldLabel);
            TreePlacer.MinSpacing = EditorGUILayout.FloatField("Min Spacing (m)", TreePlacer.MinSpacing);
            TreePlacer.ScaleMin = EditorGUILayout.FloatField("Scale Min", TreePlacer.ScaleMin);
            TreePlacer.ScaleMax = EditorGUILayout.FloatField("Scale Max", TreePlacer.ScaleMax);

            EditorGUILayout.Space(10);
            GUILayout.Label("Draw Distances", EditorStyles.boldLabel);
            TreePlacer.DrawDistance = EditorGUILayout.FloatField("Max Draw Distance (m)", TreePlacer.DrawDistance);
            TreePlacer.BillboardDistance = EditorGUILayout.FloatField("Billboard Distance (m)", TreePlacer.BillboardDistance);
            TreePlacer.CrossFadeLength = EditorGUILayout.FloatField("Cross Fade Length (m)", TreePlacer.CrossFadeLength);
            TreePlacer.MaxFullLODCount = EditorGUILayout.IntField("Max Full LOD Count", TreePlacer.MaxFullLODCount);

            EditorGUILayout.Space(10);
            GUILayout.Label("LOD Thresholds (screen %)", EditorStyles.boldLabel);
            TreePlacer.LOD0Threshold = EditorGUILayout.Slider("LOD 0 → 1", TreePlacer.LOD0Threshold, 0.01f, 0.5f);
            TreePlacer.LOD1Threshold = EditorGUILayout.Slider("LOD 1 → 2", TreePlacer.LOD1Threshold, 0.005f, 0.2f);
            TreePlacer.LOD2Threshold = EditorGUILayout.Slider("LOD 2 → Cull", TreePlacer.LOD2Threshold, 0.001f, 0.1f);

            EditorGUILayout.Space(15);
            EditorGUILayout.HelpBox(
                "Click 'Re-import Trees' to clear and re-place trees on the current hole " +
                "using the settings above. Changes are session-only (reset on Unity restart).",
                MessageType.Info);

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Re-import Trees", GUILayout.Height(30)))
            {
                EditorApplication.ExecuteMenuItem("GOLFIN/Import Trees (Current Hole)");
            }

            // Live-apply draw distances to active terrain without re-importing
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
