#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Per-water-body shore radius editor. Saves water-shore-settings.json
    /// to the hole's export folder; HoleGeoImporter reads it during import.
    /// Open via  Hole > Water Shore Settings.
    /// </summary>
    public class WaterShoreSettingsWindow : EditorWindow
    {
        [MenuItem("Hole/Water Shore Settings")]
        public static void ShowWindow()
        {
            var w = GetWindow<WaterShoreSettingsWindow>("Water Shore Settings");
            w.minSize = new Vector2(340, 300);
        }

        private struct BodyRow { public int id; public int shoreRadius; }

        private const int DefaultRadius = 10;
        private const string FileName = "water-shore-settings.json";

        private List<BodyRow> _bodies = new List<BodyRow>();
        private string _exportPath;
        private string _holeLabel;
        private string _status;
        private bool _dirty;
        private Vector2 _scroll;

        private void OnEnable() => Reload();
        private void OnFocus()  => Reload();

        private void Reload()
        {
            string sceneName = EditorSceneManager.GetActiveScene().name;

            bool isGeo  = sceneName.IndexOf("_Geo",  System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool isFlat = sceneName.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0;
            string baseName = Regex.Replace(sceneName, "(_Geo)?(_Flat)?$", "",
                RegexOptions.IgnoreCase);

            int holeNum = -1;
            if (baseName.StartsWith("Hole_") && baseName.Length >= 7)
                int.TryParse(baseName.Substring(5, 2), out holeNum);

            if (holeNum < 1 || holeNum > 18)
            {
                _holeLabel = $"No hole scene open  ({sceneName})";
                _exportPath = null;
                _bodies.Clear();
                _status = null;
                return;
            }

            string tool       = isGeo ? "UHoleGeo" : "UHoleLite";
            string holeFolder = isFlat ? $"hole-{holeNum:D2}-flat" : $"hole-{holeNum:D2}";
            _exportPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..",
                $"Tools/{tool}/output/lomond-country-club/export", holeFolder));
            _holeLabel = $"Hole {holeNum:D2}  " +
                         $"[{(isGeo ? "Geo" : "Lite")}{(isFlat ? " Flat" : "")}]";

            string waterPath = Path.Combine(_exportPath, "water.json");
            if (!File.Exists(waterPath))
            {
                _bodies.Clear();
                _status = "water.json not found — this hole may have no water.";
                return;
            }

            var wf = JsonUtility.FromJson<WaterFileData>(File.ReadAllText(waterPath));
            if (wf?.water == null || wf.water.Length == 0)
            {
                _bodies.Clear();
                _status = "No water bodies in water.json.";
                return;
            }

            // Load existing overrides (if any)
            var saved = new Dictionary<int, int>();
            string settingsPath = Path.Combine(_exportPath, FileName);
            if (File.Exists(settingsPath))
            {
                var s = JsonUtility.FromJson<WaterShoreSettings>(File.ReadAllText(settingsPath));
                if (s?.water_bodies != null)
                    foreach (var e in s.water_bodies)
                        saved[e.id] = e.shore_radius;
            }

            _bodies.Clear();
            foreach (var w in wf.water)
                _bodies.Add(new BodyRow
                {
                    id          = w.id,
                    shoreRadius = saved.TryGetValue(w.id, out int r) ? r : DefaultRadius
                });

            _status = $"Loaded {_bodies.Count} water body(ies).";
            _dirty = false;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            GUILayout.Label(_holeLabel ?? "—", EditorStyles.boldLabel);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Refresh from Scene", GUILayout.Height(24)))
            {
                Reload();
                Repaint();
            }

            if (_bodies.Count == 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status ?? "Open a hole scene to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Shore Radius per Water Body  (heightmap cells)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Wider radius = gentler shore slope (ramp spreads over more cells).\n" +
                "Narrower = steeper slope (drops quickly at the water edge).\n" +
                "Default 10 cells ≈ 5 m.  Re-import after saving.",
                MessageType.None);

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _bodies.Count; i++)
            {
                var row = _bodies[i];
                bool isDefault = row.shoreRadius == DefaultRadius;

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Body {row.id}", GUILayout.Width(52));
                int newR = EditorGUILayout.IntSlider(row.shoreRadius, 1, 50);
                GUILayout.Label($"≈{newR * 0.5f:F1} m", GUILayout.Width(46));
                if (!isDefault)
                {
                    if (GUILayout.Button("↺", GUILayout.Width(22)))
                        newR = DefaultRadius;
                }
                else
                {
                    GUILayout.Space(26);
                }
                EditorGUILayout.EndHorizontal();

                if (newR != row.shoreRadius)
                {
                    _bodies[i] = new BodyRow { id = row.id, shoreRadius = newR };
                    _dirty = true;
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset All to 10 cells"))
            {
                for (int i = 0; i < _bodies.Count; i++)
                    _bodies[i] = new BodyRow { id = _bodies[i].id, shoreRadius = DefaultRadius };
                _dirty = true;
            }
            GUI.enabled = _dirty;
            if (GUILayout.Button("Save"))
                Save();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            if (!string.IsNullOrEmpty(_status))
            {
                var type = _dirty ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(_dirty ? _status + "  (unsaved changes)" : _status, type);
            }
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(_exportPath)) return;
            var entries = new WaterShoreEntry[_bodies.Count];
            for (int i = 0; i < _bodies.Count; i++)
                entries[i] = new WaterShoreEntry { id = _bodies[i].id, shore_radius = _bodies[i].shoreRadius };

            File.WriteAllText(
                Path.Combine(_exportPath, FileName),
                JsonUtility.ToJson(new WaterShoreSettings { water_bodies = entries }, true));

            _dirty = false;
            _status = "Saved. Re-import to apply.";
            Repaint();
        }
    }
}
#endif
