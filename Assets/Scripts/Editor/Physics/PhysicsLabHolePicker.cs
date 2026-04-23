using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.Physics.Editor
{
    public class PhysicsLabHolePicker : EditorWindow
    {
        const string GEN_PATH      = "Assets/Golf/Courses/lomond-country-club/Generated";
        const string SCAFFOLD_PATH = "Assets/Scenes/Physics/LabScaffold.unity";
        const string PREF_KEY      = "Golfin.PhysicsLab.CurrentHole";

        readonly List<(int number, string path)> _holes = new List<(int, string)>();
        int _selectedIndex;

        [MenuItem("GOLFIN/Physics Lab/Hole Picker")]
        public static void Open() => GetWindow<PhysicsLabHolePicker>("Physics Lab — Hole Picker");

        void OnEnable()
        {
            RefreshHoleList();
            int saved = EditorPrefs.GetInt(PREF_KEY, 1);
            _selectedIndex = _holes.FindIndex(h => h.number == saved);
            if (_selectedIndex < 0) _selectedIndex = 0;
        }

        void RefreshHoleList()
        {
            _holes.Clear();
            if (!Directory.Exists(GEN_PATH)) return;

            foreach (var file in Directory.GetFiles(GEN_PATH, "Hole_*_Geo.unity", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                var m = Regex.Match(name, @"Hole_(\d+)_Geo");
                if (!m.Success) continue;
                int n = int.Parse(m.Groups[1].Value);
                _holes.Add((n, file.Replace('\\', '/')));
            }
            _holes.Sort((a, b) => a.number.CompareTo(b.number));
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Physics Lab — Hole Picker", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            if (_holes.Count == 0)
            {
                EditorGUILayout.HelpBox($"No Hole_XX_Geo.unity found in:\n{GEN_PATH}", MessageType.Warning);
                if (GUILayout.Button("Refresh")) RefreshHoleList();
                return;
            }

            var names = new string[_holes.Count];
            for (int i = 0; i < _holes.Count; i++)
                names[i] = $"Hole {_holes[i].number:D2}";

            _selectedIndex = EditorGUILayout.Popup("Hole", _selectedIndex, names);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Load"))
                LoadHole(_holes[_selectedIndex]);

            if (GUILayout.Button("Unload"))
                UnloadCurrentHole();

            if (GUILayout.Button("Reload"))
            {
                UnloadCurrentHole();
                LoadHole(_holes[_selectedIndex]);
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            string loaded = GetLoadedHoleName();
            EditorGUILayout.LabelField("Currently loaded:", loaded ?? "None");

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Refresh hole list")) RefreshHoleList();
        }

        void LoadHole((int number, string path) hole)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Ensure LabScaffold is open and active.
            var scaffoldScene = EditorSceneManager.GetSceneByPath(SCAFFOLD_PATH);
            if (!scaffoldScene.IsValid() || !scaffoldScene.isLoaded)
                EditorSceneManager.OpenScene(SCAFFOLD_PATH, OpenSceneMode.Single);
            else if (EditorSceneManager.GetActiveScene().path != SCAFFOLD_PATH)
                EditorSceneManager.SetActiveScene(scaffoldScene);

            // Unload any existing hole before loading the new one.
            UnloadCurrentHole();

            EditorSceneManager.OpenScene(hole.path, OpenSceneMode.Additive);
            EditorPrefs.SetInt(PREF_KEY, hole.number);
            Debug.Log($"[PhysicsLab] Loaded Hole {hole.number:D2}");
        }

        void UnloadCurrentHole()
        {
            string name = GetLoadedHoleName();
            if (name == null) return;
            var scene = EditorSceneManager.GetSceneByName(name);
            if (scene.IsValid())
                EditorSceneManager.CloseScene(scene, removeScene: true);
        }

        string GetLoadedHoleName()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo"))
                    return s.name;
            }
            return null;
        }
    }
}
