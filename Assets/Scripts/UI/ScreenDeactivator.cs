using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Golfin.UI
{
    /// <summary>
    /// Automatically deactivates all unnecessary screens before runtime.
    /// Only screens listed in activeScreenNames will remain active.
    /// </summary>
    public class ScreenDeactivator : MonoBehaviour
    {
        [Header("Screens that should be active at start")]
        [Tooltip("Add the names of GameObjects that should remain active")]
        public string[] activeScreenNames = new string[]
        {
            "LogoScreen",
            "LoadingScreen"
            // RosterScreen is managed by ScreenManager, doesn't need to be active at start
        };

        [Header("Search Settings")]
        [Tooltip("Parent object to search under (leave empty to search entire scene)")]
        public Transform searchRoot;

        [Tooltip("Tag to identify screens (leave empty to search all objects)")]
        public string screenTag = "Screen";

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                DeactivateScreensInEditor();
            }
        }

        private static void DeactivateScreensInEditor()
        {
            // Find all ScreenDeactivator instances in the scene
            ScreenDeactivator[] deactivators = FindObjectsOfType<ScreenDeactivator>();
            
            foreach (var deactivator in deactivators)
            {
                deactivator.ProcessScreens();
            }

            Debug.Log("[ScreenDeactivator] Pre-runtime screen deactivation complete");
        }

        [MenuItem("GOLFIN/Deactivate Unnecessary Screens")]
        private static void ManualDeactivate()
        {
            DeactivateScreensInEditor();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
#endif

        private void Awake()
        {
            // Also run at runtime as a safety check
            ProcessScreens();
        }

        /// <summary>
        /// Process all screens in the scene, activating/deactivating based on configuration.
        /// </summary>
        public void ProcessScreens()
        {
            GameObject[] allScreens = FindAllScreens();
            
            int deactivatedCount = 0;
            int activeCount = 0;

            foreach (GameObject screen in allScreens)
            {
                if (ShouldBeActive(screen.name))
                {
                    if (!screen.activeSelf)
                    {
                        screen.SetActive(true);
                        Debug.Log($"[ScreenDeactivator] Activated: {screen.name}");
                    }
                    activeCount++;
                }
                else
                {
                    if (screen.activeSelf)
                    {
                        screen.SetActive(false);
                        Debug.Log($"[ScreenDeactivator] Deactivated: {screen.name}");
                        deactivatedCount++;
                    }
                }
            }

            Debug.Log($"[ScreenDeactivator] Processed {allScreens.Length} screens: {activeCount} active, {deactivatedCount} deactivated");
        }

        private GameObject[] FindAllScreens()
        {
            GameObject[] screens;

            if (searchRoot != null)
            {
                // Search under specific root
                if (string.IsNullOrEmpty(screenTag))
                {
                    screens = GetAllChildrenWithPattern(searchRoot);
                }
                else
                {
                    screens = searchRoot.GetComponentsInChildren<Transform>(true)
                        .Where(t => t.CompareTag(screenTag))
                        .Select(t => t.gameObject)
                        .ToArray();
                }
            }
            else
            {
                // Search entire scene
                if (string.IsNullOrEmpty(screenTag))
                {
                    screens = FindObjectsOfType<GameObject>(true)
                        .Where(go => go.name.Contains("Screen") || go.name.Contains("Panel") || go.name.Contains("Canvas"))
                        .ToArray();
                }
                else
                {
                    screens = GameObject.FindGameObjectsWithTag(screenTag);
                }
            }

            return screens;
        }

        private GameObject[] GetAllChildrenWithPattern(Transform parent)
        {
            return parent.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name.Contains("Screen") || t.name.Contains("Panel") || t.name.Contains("Canvas"))
                .Select(t => t.gameObject)
                .ToArray();
        }

        private bool ShouldBeActive(string screenName)
        {
            foreach (string activeName in activeScreenNames)
            {
                if (screenName.Equals(activeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
