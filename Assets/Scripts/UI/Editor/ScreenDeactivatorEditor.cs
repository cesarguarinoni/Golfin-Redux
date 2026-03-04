using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Golfin.UI
{
    /// <summary>
    /// Editor window for managing screen deactivation settings
    /// </summary>
    public class ScreenDeactivatorEditor : EditorWindow
    {
        private Vector2 scrollPosition;
        private ScreenDeactivator targetDeactivator;
        private GameObject[] allScreensInScene;
        private bool[] screenActiveStates;

        [MenuItem("GOLFIN/Screen Deactivator Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScreenDeactivatorEditor>("Screen Manager");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            RefreshScreenList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("GOLFIN Screen Deactivator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // Find or create ScreenDeactivator
            if (targetDeactivator == null)
            {
                targetDeactivator = FindObjectOfType<ScreenDeactivator>();
                
                if (targetDeactivator == null)
                {
                    EditorGUILayout.HelpBox("No ScreenDeactivator found in scene. Create one?", MessageType.Warning);
                    if (GUILayout.Button("Create ScreenDeactivator"))
                    {
                        CreateScreenDeactivator();
                    }
                    return;
                }
            }

            // Refresh button
            if (GUILayout.Button("Refresh Screen List"))
            {
                RefreshScreenList();
            }

            EditorGUILayout.Space();

            // Display all screens with checkboxes
            if (allScreensInScene != null && allScreensInScene.Length > 0)
            {
                EditorGUILayout.LabelField($"Found {allScreensInScene.Length} screens in scene:", EditorStyles.boldLabel);
                EditorGUILayout.Space();

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

                for (int i = 0; i < allScreensInScene.Length; i++)
                {
                    if (allScreensInScene[i] == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    
                    bool wasActive = screenActiveStates[i];
                    screenActiveStates[i] = EditorGUILayout.Toggle(screenActiveStates[i], GUILayout.Width(20));
                    
                    if (wasActive != screenActiveStates[i])
                    {
                        UpdateActiveScreensList();
                    }

                    EditorGUILayout.LabelField(allScreensInScene[i].name);
                    EditorGUILayout.LabelField($"({allScreensInScene[i].transform.parent?.name ?? "Root"})", EditorStyles.miniLabel);
                    
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No screens found. Make sure your screens have 'Screen', 'Panel', or 'Canvas' in their names, or set up tags.", MessageType.Info);
            }

            EditorGUILayout.Space();

            // Action buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply & Deactivate Now"))
            {
                ApplyChanges();
            }
            if (GUILayout.Button("Select All"))
            {
                SelectAll(true);
            }
            if (GUILayout.Button("Select None"))
            {
                SelectAll(false);
            }
            EditorGUILayout.EndHorizontal();

            // Current active screens display
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Currently configured active screens:", EditorStyles.boldLabel);
            if (targetDeactivator.activeScreenNames != null && targetDeactivator.activeScreenNames.Length > 0)
            {
                foreach (string screenName in targetDeactivator.activeScreenNames)
                {
                    EditorGUILayout.LabelField($"• {screenName}", EditorStyles.miniLabel);
                }
            }
            else
            {
                EditorGUILayout.LabelField("None", EditorStyles.miniLabel);
            }
        }

        private void RefreshScreenList()
        {
            if (targetDeactivator == null) return;

            // Find all potential screens (including Canvas for GOLFIN screens)
            allScreensInScene = FindObjectsOfType<GameObject>(true)
                .Where(go => go.name.Contains("Screen") || go.name.Contains("Panel") || go.name.Contains("Canvas"))
                .OrderBy(go => go.name)
                .ToArray();

            screenActiveStates = new bool[allScreensInScene.Length];

            // Check which ones are in the active list
            for (int i = 0; i < allScreensInScene.Length; i++)
            {
                screenActiveStates[i] = targetDeactivator.activeScreenNames.Contains(allScreensInScene[i].name);
            }
        }

        private void UpdateActiveScreensList()
        {
            var activeNames = new System.Collections.Generic.List<string>();
            
            for (int i = 0; i < allScreensInScene.Length; i++)
            {
                if (screenActiveStates[i] && allScreensInScene[i] != null)
                {
                    activeNames.Add(allScreensInScene[i].name);
                }
            }

            Undo.RecordObject(targetDeactivator, "Update Active Screens");
            targetDeactivator.activeScreenNames = activeNames.ToArray();
            EditorUtility.SetDirty(targetDeactivator);
        }

        private void ApplyChanges()
        {
            UpdateActiveScreensList();
            targetDeactivator.ProcessScreens();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene()
            );
            Debug.Log("[ScreenDeactivator] Changes applied!");
        }

        private void SelectAll(bool state)
        {
            for (int i = 0; i < screenActiveStates.Length; i++)
            {
                screenActiveStates[i] = state;
            }
            UpdateActiveScreensList();
        }

        private void CreateScreenDeactivator()
        {
            GameObject go = new GameObject("ScreenDeactivator");
            targetDeactivator = go.AddComponent<ScreenDeactivator>();
            Selection.activeGameObject = go;
            RefreshScreenList();
            EditorUtility.SetDirty(go);
        }
    }
}
