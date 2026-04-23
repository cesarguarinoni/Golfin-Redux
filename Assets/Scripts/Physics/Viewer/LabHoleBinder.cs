using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Bridges EditorSceneManager additive-load events to PhysicsLabController.
    /// Attach to LabRoot alongside PhysicsLabController.
    /// Also handles Play-mode start when a hole was pre-loaded in Edit mode.
    /// </summary>
    public class LabHoleBinder : MonoBehaviour
    {
        [SerializeField] PhysicsLabController _controller;

        void OnEnable()
        {
#if UNITY_EDITOR
            EditorSceneManager.sceneOpened += OnEditorSceneOpened;
            EditorSceneManager.sceneClosed += OnEditorSceneClosed;
#endif
            // Handle Play-mode start: hole scene may already be loaded additively.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (IsHoleGeoScene(scene.name) && scene.isLoaded)
                {
                    _controller?.OnHoleLoaded(scene.name);
                    break;
                }
            }
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorSceneManager.sceneOpened -= OnEditorSceneOpened;
            EditorSceneManager.sceneClosed -= OnEditorSceneClosed;
#endif
        }

#if UNITY_EDITOR
        void OnEditorSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!IsHoleGeoScene(scene.name)) return;
            _controller?.OnHoleLoaded(scene.name);
        }

        void OnEditorSceneClosed(Scene scene)
        {
            if (!IsHoleGeoScene(scene.name)) return;
            _controller?.OnHoleUnloaded();
        }
#endif

        static bool IsHoleGeoScene(string name)
            => name != null && name.StartsWith("Hole_") && name.EndsWith("_Geo");
    }
}
