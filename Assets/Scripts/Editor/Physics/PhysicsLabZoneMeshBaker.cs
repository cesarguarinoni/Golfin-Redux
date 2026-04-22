using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Editor
{
    public static class PhysicsLabZoneMeshBaker
    {
        const string k_GeneratedScene  = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_01_Geo.unity";
        const string k_LabScene        = "Assets/Scenes/Physics/PhysicsLab_Hole1.unity";
        const string k_ContainerName   = "ZoneMeshes_Physics";

        [MenuItem("GOLFIN/Physics Lab/Sync Zone Meshes (Hole 1)")]
        public static void SyncZoneMeshes()
        {
            // Save dirty scenes before we start
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[PhysicsLabBaker] Cancelled by user.");
                return;
            }

            // Open the PhysicsLab scene if it isn't already loaded
            Scene labScene = SceneManager.GetSceneByPath(k_LabScene);
            bool labWasOpen = labScene.IsValid() && labScene.isLoaded;
            if (!labWasOpen)
            {
                labScene = EditorSceneManager.OpenScene(k_LabScene, OpenSceneMode.Additive);
            }

            // Open Generated scene additively (editor-mode; no Build Profile required)
            Scene generatedScene = EditorSceneManager.OpenScene(k_GeneratedScene, OpenSceneMode.Additive);
            if (!generatedScene.IsValid())
            {
                Debug.LogError($"[PhysicsLabBaker] Could not open {k_GeneratedScene}");
                return;
            }

            // Collect all SurfaceMarker GOs from the generated scene
            var markers = new List<GameObject>();
            foreach (var root in generatedScene.GetRootGameObjects())
            {
                foreach (var sm in root.GetComponentsInChildren<SurfaceMarker>(true))
                    markers.Add(sm.gameObject);
            }
            Debug.Log($"[PhysicsLabBaker] Found {markers.Count} SurfaceMarker objects in Generated scene.");

            // Remove previous baked container from lab scene
            foreach (var root in labScene.GetRootGameObjects())
            {
                if (root.name == k_ContainerName)
                {
                    Object.DestroyImmediate(root);
                    break;
                }
            }

            // Create fresh container in lab scene
            SceneManager.SetActiveScene(labScene);
            var container = new GameObject(k_ContainerName);
            SceneManager.MoveGameObjectToScene(container, labScene);

            int copied = 0;
            foreach (var srcGO in markers)
            {
                // Duplicate GO into lab scene
                var copy = Object.Instantiate(srcGO, srcGO.transform.position, srcGO.transform.rotation, container.transform);
                copy.name = srcGO.name;

                // Disable all renderers so we get invisible-but-collideable zone meshes
                foreach (var r in copy.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

                // Ensure colliders are present and active
                bool hasCollider = copy.GetComponentInChildren<Collider>(true) != null;
                if (!hasCollider)
                {
                    var mf = copy.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var mc = copy.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                    }
                }

                copy.SetActive(true);
                copied++;
            }

            Debug.Log($"[PhysicsLabBaker] Baked {copied} zone mesh objects into {k_LabScene}.");

            // Close generated scene without saving
            EditorSceneManager.CloseScene(generatedScene, true);

            // Save lab scene
            EditorSceneManager.SaveScene(labScene);
            Debug.Log($"[PhysicsLabBaker] Saved {k_LabScene}.");

            // Close lab scene if we opened it ourselves
            if (!labWasOpen)
                EditorSceneManager.CloseScene(labScene, false);
        }
    }
}
