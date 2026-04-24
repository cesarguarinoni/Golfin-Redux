using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Bridges EditorSceneManager additive-load events to PhysicsLabController.
    /// Handles only EDIT-TIME scene picker interactions (load/unload via PhysicsLabHolePicker).
    /// Play-mode startup detection is handled by PhysicsLabController.ScanForLoadedHoleSceneAtStartup().
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
            // Only react to actual hole-geo scenes closing; ignore Unity's play-mode
            // scene reload sequence which closes and reopens the LabScaffold scene.
            if (!IsHoleGeoScene(scene.name)) return;
            _controller?.OnHoleUnloaded();
        }
#endif

        static bool IsHoleGeoScene(string name)
            => name != null && name.StartsWith("Hole_") && name.EndsWith("_Geo");
    }
}
