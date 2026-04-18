#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    public class BridgeExporter : EditorWindow
    {
        [MenuItem("Window/Bridge Exporter")]
        public static void ShowWindow()
        {
            var w = GetWindow<BridgeExporter>("Bridges");
            w.minSize = new Vector2(320, 240);
        }

        private double _lastRepaint;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Bridge Exporter", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            var anchors = FindAnchorsInActiveScene();
            EditorGUILayout.LabelField($"Found {anchors.Count} BridgeAnchor(s) in scene.");

            if (anchors.Count > 0)
            {
                EditorGUILayout.Space();
                foreach (var a in anchors)
                {
                    Vector3 p = a.transform.position;
                    EditorGUILayout.LabelField(
                        $"  • {(string.IsNullOrEmpty(a.id) ? a.name : a.id)}" +
                        $"  @ ({p.x:F2}, {p.z:F2})  yaw {a.transform.eulerAngles.y:F1}°");
                }
            }

            EditorGUILayout.Space(10);

            if (GUILayout.Button("Add BridgeAnchor to Selected GameObject"))
                AddAnchorToSelected();

            EditorGUILayout.Space();

            GUI.enabled = anchors.Count > 0;
            if (GUILayout.Button("Export Bridges for Current Hole", GUILayout.Height(30)))
                ExportBridgesForCurrentHole(anchors);
            GUI.enabled = true;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Writes bridges.json to the current hole's UHoleGeo export " +
                "folder (Lite/Geo/Flat auto-detected from scene name). " +
                "UHoleGeo can read this file so cart-path splines snap to " +
                "bridge anchors.",
                MessageType.Info);
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.timeSinceStartup - _lastRepaint > 0.5)
            {
                Repaint();
                _lastRepaint = EditorApplication.timeSinceStartup;
            }
        }

        // ------------------------------------------------------------------ //

        private static List<Golfin.Course.BridgeAnchor> FindAnchorsInActiveScene()
        {
            var result = new List<Golfin.Course.BridgeAnchor>();
            var activeScene = EditorSceneManager.GetActiveScene();
            foreach (var root in activeScene.GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<Golfin.Course.BridgeAnchor>(true));
            return result;
        }

        private static void AddAnchorToSelected()
        {
            var sel = Selection.activeGameObject;
            if (sel == null)
            {
                EditorUtility.DisplayDialog("Add Bridge Anchor",
                    "Select a GameObject in the scene first.", "OK");
                return;
            }
            if (sel.GetComponent<Golfin.Course.BridgeAnchor>() != null)
            {
                EditorUtility.DisplayDialog("Add Bridge Anchor",
                    "That GameObject already has a BridgeAnchor.", "OK");
                return;
            }
            Undo.AddComponent<Golfin.Course.BridgeAnchor>(sel);
            EditorUtility.SetDirty(sel);
        }

        // ------------------------------------------------------------------ //

        [System.Serializable]
        private class BridgeDTO
        {
            public string id;
            public float x;
            public float z;
            public float y;
            public float yaw_deg;
            public float length_forward_m;
            public float length_backward_m;
            public float expected_path_width_m;
            public AnchorDTO anchor_forward;
            public AnchorDTO anchor_backward;
        }

        [System.Serializable]
        private class AnchorDTO
        {
            public float x;
            public float z;
        }

        [System.Serializable]
        private class BridgesFile
        {
            public string schema_version = "1.0.0";
            public int hole_number;
            public string flavour;
            public int bridge_count;
            public BridgeDTO[] bridges;
        }

        private static void ExportBridgesForCurrentHole(
            List<Golfin.Course.BridgeAnchor> anchors)
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            string sceneName = activeScene.name;
            string scenePath = activeScene.path ?? "";

            bool isGeo  = scenePath.IndexOf("_Geo",  System.StringComparison.OrdinalIgnoreCase) >= 0
                       || sceneName.IndexOf("_Geo",  System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFlat = scenePath.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0
                       || sceneName.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0;

            // Strip trailing _Geo / _Flat suffixes to get the base name (e.g. "Hole_07")
            string baseName = System.Text.RegularExpressions.Regex.Replace(
                sceneName, "(_Geo)?(_Flat)?$", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            int holeNumber = -1;
            if (baseName.StartsWith("Hole_") && baseName.Length >= 7)
                int.TryParse(baseName.Substring(5, 2), out holeNumber);

            if (holeNumber < 1 || holeNumber > 18)
            {
                EditorUtility.DisplayDialog("Export Bridges",
                    $"Cannot detect hole number from scene '{sceneName}'.\n" +
                    "Expected 'Hole_XX', 'Hole_XX_Geo', 'Hole_XX_Flat', " +
                    "or 'Hole_XX_Geo_Flat'.", "OK");
                return;
            }

            string flavour    = (isGeo ? "geo" : "lite") + (isFlat ? "-flat" : "");
            string toolFolder = isGeo ? "UHoleGeo" : "UHoleLite";
            string holeFolder = isFlat ? $"hole-{holeNumber:D2}-flat" : $"hole-{holeNumber:D2}";

            string exportPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    Application.dataPath, "..",
                    $"Tools/{toolFolder}/output/lomond-country-club/export",
                    holeFolder));

            if (!System.IO.Directory.Exists(exportPath))
            {
                EditorUtility.DisplayDialog("Export Bridges",
                    $"Export folder not found:\n{exportPath}\n\n" +
                    "Has this hole been exported from UHoleGeo yet?", "OK");
                return;
            }

            // Build DTOs
            var dtos = new BridgeDTO[anchors.Count];
            for (int i = 0; i < anchors.Count; i++)
            {
                var a   = anchors[i];
                Vector3 p   = a.transform.position;
                Vector3 fwd = a.transform.forward;

                Vector3 anchorF = p + fwd * a.lengthForward;
                Vector3 anchorB = p - fwd * a.lengthBackward;

                dtos[i] = new BridgeDTO
                {
                    id                    = string.IsNullOrEmpty(a.id) ? $"bridge_{i + 1}" : a.id,
                    x                     = p.x,
                    y                     = p.y,
                    z                     = p.z,
                    yaw_deg               = NormalizeYaw(a.transform.eulerAngles.y),
                    length_forward_m      = a.lengthForward,
                    length_backward_m     = a.lengthBackward,
                    expected_path_width_m = a.expectedPathWidth,
                    anchor_forward        = new AnchorDTO { x = anchorF.x, z = anchorF.z },
                    anchor_backward       = new AnchorDTO { x = anchorB.x, z = anchorB.z },
                };
            }

            var file = new BridgesFile
            {
                hole_number  = holeNumber,
                flavour      = flavour,
                bridge_count = dtos.Length,
                bridges      = dtos,
            };

            string json    = JsonUtility.ToJson(file, true);
            string outPath = System.IO.Path.Combine(exportPath, "bridges.json");
            System.IO.File.WriteAllText(outPath, json);
            Debug.Log($"[BridgeExporter] Wrote {dtos.Length} bridge(s) to {outPath}");

            // Mirror to the sibling pipeline (Geo ↔ Lite) if its folder exists
            string otherTool       = isGeo ? "UHoleLite" : "UHoleGeo";
            string otherExportPath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    Application.dataPath, "..",
                    $"Tools/{otherTool}/output/lomond-country-club/export",
                    holeFolder));

            if (System.IO.Directory.Exists(otherExportPath))
            {
                string mirrorPath = System.IO.Path.Combine(otherExportPath, "bridges.json");
                System.IO.File.WriteAllText(mirrorPath, json);
                Debug.Log($"[BridgeExporter] Mirrored to {mirrorPath}");
            }

            EditorUtility.DisplayDialog("Export Bridges",
                $"Exported {dtos.Length} bridge(s) to:\n{outPath}", "OK");
        }

        private static float NormalizeYaw(float yawDeg)
        {
            yawDeg = yawDeg % 360f;
            if (yawDeg >  180f) yawDeg -= 360f;
            if (yawDeg < -180f) yawDeg += 360f;
            return yawDeg;
        }
    }
}
#endif
