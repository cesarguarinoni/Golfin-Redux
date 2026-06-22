// map_view_aiming (Order 352) iter-5 — Editor launcher stub.
// The actual MonoBehaviour driver lives at:
//   Assets/Scripts/Gameplay/UI/ShotUI/MapViewCaptureDriver.cs
// This file is here so the Editor asmdef (Golfin.MapViewCapture.Editor) can
// reference MapViewCaptureDriver without the full runtime import tree.
// It only forwards the LaunchProgrammatic() call to the runtime class.
using System.IO;
using UnityEditor;
using UnityEngine;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.EditorTools
{
    public static class MapViewCaptureEditorLauncher
    {
        private const string MenuPath = "GOLFIN/MapView/Run Real-Input Capture (iter-5)";

        [MenuItem(MenuPath)]
        public static void RunRealInputCapture()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[MapViewCaptureEditorLauncher] Already in play mode.");
                return;
            }

            string root = Path.GetDirectoryName(Application.dataPath);
            string captureDir = Path.GetFullPath(Path.Combine(root,
                "Docs/Specs/Active/map_view_aiming/screenshots/iter5"));
            Directory.CreateDirectory(captureDir);

            MapViewCaptureDriver.Armed      = true;
            MapViewCaptureDriver.CaptureDir = captureDir;

            Debug.Log($"[MapViewCaptureEditorLauncher] Armed=true CaptureDir={captureDir}");

            // Open ShellScene if needed
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.name.Equals("ShellScene", System.StringComparison.OrdinalIgnoreCase))
            {
                string[] guids = AssetDatabase.FindAssets("ShellScene t:Scene");
                foreach (var guid in guids)
                {
                    string sp = AssetDatabase.GUIDToAssetPath(guid);
                    if (Path.GetFileNameWithoutExtension(sp)
                            .Equals("ShellScene", System.StringComparison.OrdinalIgnoreCase))
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(sp,
                            UnityEditor.SceneManagement.OpenSceneMode.Single);
                        Debug.Log($"[MapViewCaptureEditorLauncher] Opened {sp}");
                        break;
                    }
                }
            }

            EditorApplication.EnterPlaymode();
        }
    }
}
