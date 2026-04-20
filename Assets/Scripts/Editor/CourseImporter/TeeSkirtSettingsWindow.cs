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
    /// Per-tee ramp slope editor. Saves tee-skirt-settings.json to the
    /// hole's export folder; HoleGeoImporter reads it during import.
    /// Open via  Hole > Tee Skirt Settings.
    /// </summary>
    public class TeeSkirtSettingsWindow : EditorWindow
    {
        [MenuItem("Hole/Tee Skirt Settings")]
        public static void ShowWindow()
        {
            var w = GetWindow<TeeSkirtSettingsWindow>("Tee Skirt Settings");
            w.minSize = new Vector2(340, 320);
        }

        private struct TeeRow { public int id; public float slope; }

        private const float DefaultSlope = 0.35f;
        private const string FileName = "tee-skirt-settings.json";

        private List<TeeRow> _tees = new List<TeeRow>();
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
                _tees.Clear();
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

            string zcPath = Path.Combine(_exportPath, "zone-contours.json");
            if (!File.Exists(zcPath))
            {
                _tees.Clear();
                _status = "zone-contours.json not found — run UHoleGeo export first.";
                return;
            }

            var zc = JsonUtility.FromJson<ZoneContoursFile>(File.ReadAllText(zcPath));
            if (zc?.zones?.tee == null || zc.zones.tee.Length == 0)
            {
                _tees.Clear();
                _status = "No tee regions found in zone-contours.json.";
                return;
            }

            // Load existing overrides (if any)
            var saved = new Dictionary<int, float>();
            string settingsPath = Path.Combine(_exportPath, FileName);
            if (File.Exists(settingsPath))
            {
                var s = JsonUtility.FromJson<TeeSkirtSettings>(File.ReadAllText(settingsPath));
                if (s?.tee_slopes != null)
                    foreach (var e in s.tee_slopes)
                        saved[e.id] = e.slope;
            }

            _tees.Clear();
            foreach (var r in zc.zones.tee)
                _tees.Add(new TeeRow
                {
                    id    = r.id,
                    slope = saved.TryGetValue(r.id, out float ov) ? ov : DefaultSlope
                });

            _status = $"Loaded {_tees.Count} tee region(s).";
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

            if (_tees.Count == 0)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(_status ?? "Open a hole scene to begin.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8);
            GUILayout.Label("Ramp Slope per Tee  (rise / run)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Higher value = steeper ramp = shorter skirt.\n" +
                "Lower value = gentler ramp = longer skirt.\n" +
                "Default 0.35 ≈ 19°.  Re-import after saving.",
                MessageType.None);

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int i = 0; i < _tees.Count; i++)
            {
                var row = _tees[i];
                bool isDefault = Mathf.Approximately(row.slope, DefaultSlope);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Tee {row.id}", GUILayout.Width(46));
                float newSlope = EditorGUILayout.Slider(row.slope, 0.05f, 2.0f);
                if (!isDefault)
                {
                    if (GUILayout.Button("↺", GUILayout.Width(22)))
                        newSlope = DefaultSlope;
                }
                else
                {
                    GUILayout.Space(26);
                }
                EditorGUILayout.EndHorizontal();

                if (!Mathf.Approximately(newSlope, row.slope))
                {
                    _tees[i] = new TeeRow { id = row.id, slope = newSlope };
                    _dirty = true;
                }
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset All to 0.35"))
            {
                for (int i = 0; i < _tees.Count; i++)
                    _tees[i] = new TeeRow { id = _tees[i].id, slope = DefaultSlope };
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
            var entries = new TeeSkirtEntry[_tees.Count];
            for (int i = 0; i < _tees.Count; i++)
                entries[i] = new TeeSkirtEntry { id = _tees[i].id, slope = _tees[i].slope };

            File.WriteAllText(
                Path.Combine(_exportPath, FileName),
                JsonUtility.ToJson(new TeeSkirtSettings { tee_slopes = entries }, true));

            _dirty = false;
            _status = $"Saved. Re-import to apply.";
            Repaint();
        }
    }
}
#endif
