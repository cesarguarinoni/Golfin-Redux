using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Editor
{
    /// <summary>
    /// Loads Hole_XX_Geo.unity additively into LabScaffold for physics testing.
    /// Also loads a companion Hole_XX_Geo_Supplement.unity (if present) for
    /// manually-placed objects (water, trees, props) that survive hole reimports.
    /// </summary>
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
                var m = Regex.Match(name, @"Hole_(\d+)_Geo$");
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

            // Supplement scene status + creation.
            if (loaded != null)
            {
                string suppPath = SupplementPath(_holes[_selectedIndex].path);
                bool suppExists = File.Exists(suppPath);
                bool suppLoaded = GetLoadedSupplementName() != null;

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Supplement:", suppExists
                    ? (suppLoaded ? "loaded" : "exists (not loaded)")
                    : "none");

                if (!suppExists && GUILayout.Button("Create", GUILayout.Width(60)))
                    CreateAndLoadSupplement(suppPath);
                else if (suppExists && !suppLoaded && GUILayout.Button("Load", GUILayout.Width(60)))
                    EditorSceneManager.OpenScene(suppPath, OpenSceneMode.Additive);

                EditorGUILayout.EndHorizontal();

                if (!suppExists)
                    EditorGUILayout.HelpBox(
                        "Add water, trees, props here — survives hole reimports.\n" +
                        "Objects need MeshCollider + Physics.Runtime.SurfaceMarker.",
                        MessageType.Info);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Refresh hole list")) RefreshHoleList();
        }

        // ── Load / Unload ──────────────────────────────────────────────────────

        void LoadHole((int number, string path) hole)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // Ensure LabScaffold is open and active.
            var scaffoldScene = EditorSceneManager.GetSceneByPath(SCAFFOLD_PATH);
            if (!scaffoldScene.IsValid() || !scaffoldScene.isLoaded)
                EditorSceneManager.OpenScene(SCAFFOLD_PATH, OpenSceneMode.Single);
            else if (EditorSceneManager.GetActiveScene().path != SCAFFOLD_PATH)
                EditorSceneManager.SetActiveScene(scaffoldScene);

            // Unload any existing hole (+ supplement) before loading the new one.
            UnloadCurrentHole();

            EditorSceneManager.OpenScene(hole.path, OpenSceneMode.Additive);
            EditorPrefs.SetInt(PREF_KEY, hole.number);

            // Load companion supplement scene if one exists.
            string suppPath = SupplementPath(hole.path);
            if (File.Exists(suppPath))
            {
                EditorSceneManager.OpenScene(suppPath, OpenSceneMode.Additive);
                Debug.Log($"[PhysicsLab] Loaded supplement: {Path.GetFileName(suppPath)}");
            }

            Debug.Log($"[PhysicsLab] Loaded Hole {hole.number:D2}");

            // Notify controller immediately (edit-mode: LabHoleBinder.OnEnable won't fire).
            string holeName = Path.GetFileNameWithoutExtension(hole.path);
            scaffoldScene = EditorSceneManager.GetSceneByPath(SCAFFOLD_PATH);
            foreach (var root in scaffoldScene.GetRootGameObjects())
            {
                var ctrl = root.GetComponentInChildren<PhysicsLabController>(true);
                if (ctrl != null) { ctrl.OnHoleLoaded(holeName); break; }
            }
        }

        void UnloadCurrentHole()
        {
            // Unload supplement first (it depends on the hole scene).
            string suppName = GetLoadedSupplementName();
            if (suppName != null)
            {
                var suppScene = EditorSceneManager.GetSceneByName(suppName);
                if (suppScene.IsValid())
                    EditorSceneManager.CloseScene(suppScene, removeScene: true);
            }

            string name = GetLoadedHoleName();
            if (name == null) return;
            var scene = EditorSceneManager.GetSceneByName(name);
            if (scene.IsValid())
                EditorSceneManager.CloseScene(scene, removeScene: true);

            // Notify controller (edit-mode: LabHoleBinder.OnDisable won't fire).
            var scaffoldScene = EditorSceneManager.GetSceneByPath(SCAFFOLD_PATH);
            if (!scaffoldScene.IsValid()) return;
            foreach (var root in scaffoldScene.GetRootGameObjects())
            {
                var ctrl = root.GetComponentInChildren<PhysicsLabController>(true);
                if (ctrl != null) { ctrl.OnHoleUnloaded(); break; }
            }
        }

        void CreateAndLoadSupplement(string suppPath)
        {
            var suppScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            EditorSceneManager.SaveScene(suppScene, suppPath);
            AssetDatabase.Refresh();
            Debug.Log($"[PhysicsLab] Created supplement: {suppPath}");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        static string SupplementPath(string holePath)
        {
            string dir  = Path.GetDirectoryName(holePath);
            string stem = Path.GetFileNameWithoutExtension(holePath);
            return Path.Combine(dir, stem + "_Supplement.unity").Replace('\\', '/');
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

        string GetLoadedSupplementName()
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var s = EditorSceneManager.GetSceneAt(i);
                if (s.name.StartsWith("Hole_") && s.name.EndsWith("_Geo_Supplement"))
                    return s.name;
            }
            return null;
        }
    }
}
