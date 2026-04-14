#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Debug toolbox for hole scenes.
    /// Open via  Hole > Debug Tools.
    /// </summary>
    public class HoleDebugWindow : EditorWindow
    {
        [MenuItem("Hole/Debug Tools")]
        public static void ShowWindow()
        {
            var w = GetWindow<HoleDebugWindow>("Hole Debug");
            w.minSize = new Vector2(300, 180);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            // ---- Camera ----
            GUILayout.Label("Camera", EditorStyles.boldLabel);
            if (GUILayout.Button("Set Camera — Top View (fit terrain)", GUILayout.Height(30)))
                SetTopViewCamera();

            EditorGUILayout.Space(12);

            // ---- Capture ----
            GUILayout.Label("Capture", EditorStyles.boldLabel);
            if (GUILayout.Button("Capture Scene View", GUILayout.Height(30)))
                CaptureScene();
            EditorGUILayout.Space(4);
            if (GUILayout.Button("Capture Game View", GUILayout.Height(30)))
                CaptureGame();

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Screenshots saved to  Assets/Screenshots/<scene name>/\n" +
                "Named  <scene> – Scene/Game – yyyy-MM-dd_HH-mm-ss.png",
                MessageType.None);
        }

        // ──────────────────────────────────────────────────────────────

        private static void SetTopViewCamera()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Debug.LogWarning("[HoleDebug] No active Scene view.");
                return;
            }

            var terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("[HoleDebug] No active terrain in scene.");
                return;
            }

            var td = terrain.terrainData;
            Vector3 origin = terrain.transform.position;
            Vector3 center = origin + new Vector3(td.size.x / 2f, 0f, td.size.z / 2f);
            float viewSize = Mathf.Max(td.size.x, td.size.z) / 2f;

            // Find the flag GameObject in the scene and orient the camera so
            // the world direction from terrain center → flag maps to screen up.
            //
            // Using the flag's actual world position (instead of re-reading
            // greens.json) avoids any coordinate-mapping confusion between
            // Lite/Geo pipelines — whatever position the importer placed it
            // at is the truth.
            Vector3 screenUp = Vector3.forward; // default: +Z at top of screen
            var flag = FindFlagInScene();
            if (flag != null)
            {
                Vector3 toFlag = flag.transform.position - center;
                toFlag.y = 0f;
                if (toFlag.sqrMagnitude > 0.01f)
                {
                    // Snap to nearest cardinal so terrain edges stay parallel
                    // to the view window. Pick whichever of ±X / ±Z has the
                    // largest projection onto the flag direction.
                    if (Mathf.Abs(toFlag.x) > Mathf.Abs(toFlag.z))
                        screenUp = toFlag.x > 0 ? Vector3.right : Vector3.left;
                    else
                        screenUp = toFlag.z > 0 ? Vector3.forward : Vector3.back;
                }
            }
            else
            {
                Debug.LogWarning("[HoleDebug] No Flag_* GameObject found. Using default top-down orientation.");
            }

            // Top-down camera: forward = down, upwards = direction to flag.
            // LookRotation projects `upwards` onto the plane perpendicular to
            // `forward`, so it becomes the world direction that maps to
            // screen-up. Flag direction at screen-up ⇒ flag visible at top.
            Quaternion rotation = Quaternion.LookRotation(Vector3.down, screenUp);

            sv.orthographic = true;
            sv.LookAt(center, rotation, viewSize);
        }

        /// <summary>
        /// Finds the flag GameObject placed by the importers. Works even when
        /// the flag is nested deep in the hierarchy or inactive.
        /// </summary>
        private static GameObject FindFlagInScene()
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.isLoaded) continue;
                if (go.hideFlags != HideFlags.None) continue;
                if (go.name.StartsWith("Flag_")) return go;
            }
            return null;
        }

        // ──────────────────────────────────────────────────────────────

        private static string SceneName()
            => EditorSceneManager.GetActiveScene().name; // e.g. "Hole_01"

        private static string ScreenshotDir()
        {
            string dir = $"Assets/Screenshots/{SceneName()}";
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private static string Timestamp()
            => DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

        // ──────────────────────────────────────────────────────────────

        private static void CaptureScene()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv == null)
            {
                Debug.LogWarning("[HoleDebug] No active Scene view.");
                return;
            }

            sv.Focus();

            string name = SceneName();
            string path = $"{ScreenshotDir()}/{name} - Scene - {Timestamp()}.png";

            // Render the scene camera into a RenderTexture and read back pixels
            var cam = sv.camera;
            int w = Mathf.Max(1, (int)sv.position.width);
            int h = Mathf.Max(1, (int)sv.position.height);

            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            var prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;

            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            RenderTexture.ReleaseTemporary(rt);

            File.WriteAllBytes(path, tex.EncodeToPNG());
            DestroyImmediate(tex);

            AssetDatabase.Refresh();
            Debug.Log($"[HoleDebug] Scene captured → {path}");
        }

        private static void CaptureGame()
        {
            string name = SceneName();
            string path = $"{ScreenshotDir()}/{name} - Game - {Timestamp()}.png";

            ScreenCapture.CaptureScreenshot(path);

            // Refresh after Unity finishes writing the file
            EditorApplication.delayCall += () =>
            {
                AssetDatabase.Refresh();
                Debug.Log($"[HoleDebug] Game captured → {path}");
            };
        }
    }
}
#endif
