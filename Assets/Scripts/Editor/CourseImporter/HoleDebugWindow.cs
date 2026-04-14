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

            // Centre over the terrain footprint (ignore Y / mountain backdrop)
            Vector3 center = origin + new Vector3(td.size.x / 2f, 0f, td.size.z / 2f);

            // viewSize = half the larger dimension → terrain fills the view
            float viewSize = Mathf.Max(td.size.x, td.size.z) / 2f;

            // Orient so the green appears at the top of the screen
            Quaternion rotation = TopDownRotationTowardGreen(center);

            sv.orthographic = true;
            sv.LookAt(center, rotation, viewSize);
        }

        /// <summary>
        /// Reads greens.json from the export folder (via HoleMetadata) and returns a
        /// top-down camera rotation that places the green at the top of the screen.
        /// Falls back to a plain top-down view if the data is unavailable.
        /// </summary>
        private static Quaternion TopDownRotationTowardGreen(Vector3 terrainCenter)
        {
            var metadata = UnityEngine.Object.FindObjectOfType<HoleMetadata>();
            if (metadata == null || string.IsNullOrEmpty(metadata.importType))
                return Quaternion.Euler(90f, 0f, 0f);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string holeId = metadata.holeNumber.ToString("D2");
            bool isLite = metadata.importType.StartsWith("Lite");
            bool isFlat = metadata.importType.EndsWith("Flat");

            string toolFolder = isLite ? "UHoleLite" : "UHoleGeo";
            string exportFolder = isFlat ? $"hole-{holeId}-flat" : $"hole-{holeId}";
            string exportPath = Path.Combine(projectRoot, "Tools", toolFolder, "output",
                metadata.courseId, "export", exportFolder);

            string greensPath = Path.Combine(exportPath, "greens.json");
            if (!File.Exists(greensPath))
                return Quaternion.Euler(90f, 0f, 0f);

            var greensFile = JsonUtility.FromJson<GreensFileData>(File.ReadAllText(greensPath));
            if (greensFile.greens == null || greensFile.greens.Length == 0)
                return Quaternion.Euler(90f, 0f, 0f);

            var gc = greensFile.greens[0].center_local;
            // Same coordinate mapping as the importers
            Vector3 greenWorld = isLite
                ? new Vector3(gc.z, 0f, gc.x)   // Lite: 90° CCW swap
                : new Vector3(gc.x, 0f, gc.z);  // Geo: direct

            Vector3 toGreen = greenWorld - terrainCenter;
            toGreen.y = 0f;
            if (toGreen.sqrMagnitude < 0.01f)
                return Quaternion.Euler(90f, 0f, 0f);

            // Camera looks straight down; screen "up" points toward the green
            return Quaternion.LookRotation(Vector3.down, toGreen.normalized);
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
