#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.Editor.CanvasScalerMigration
{
    /// <summary>
    /// One-shot builder for the CanvasScalerTest scene used to validate the
    /// 1170x2532 / Match=0 hypothesis. See
    /// Docs/Specs/Queued/CANVAS_SCALER_FIX_PLAN.md (Step 2).
    ///
    /// Workflow:
    ///   1. GOLFIN/Canvas Scaler/Build Test Scene  -> creates the scene.
    ///   2. Open Game View, set resolution to "iPhone 14 Pro 1170x2532".
    ///   3. GOLFIN/Canvas Scaler/Set Test Config 1..4  -> apply matrix row.
    ///   4. Enter Play Mode, screenshot, measure red square pixel size.
    ///   5. Pass criteria: Config 4 (1170x2532, Match=0) yields 180x180 +/- 1 px.
    /// </summary>
    public static class CanvasScalerTestSceneBuilder
    {
        const string SceneFolder = "Assets/Scenes/Tests";
        const string ScenePath = SceneFolder + "/CanvasScalerTest.unity";
        const string ScreenshotFolder = "Assets/Scenes/Tests/Screenshots";

        const string CanvasName = "TestCanvas";
        const string SquareName = "TestSquare180";
        const string LabelName = "TestLabel";

        // -------- Scene build --------

        [MenuItem("GOLFIN/Canvas Scaler/Build Test Scene", priority = 100)]
        public static void BuildTestScene()
        {
            if (!Directory.Exists(SceneFolder)) Directory.CreateDirectory(SceneFolder);

            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog(
                        "Overwrite test scene?",
                        $"{ScenePath} already exists. Overwrite?",
                        "Overwrite", "Cancel"))
                {
                    return;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // EventSystem (so any future interaction works; not strictly needed for this test).
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            SceneManager.MoveGameObjectToScene(es, scene);

            // Canvas + Scaler + GraphicRaycaster.
            var canvasGo = new GameObject(CanvasName,
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            // Default to Config 4 (the proposed configuration) on first build.
            ApplyConfigToScaler(scaler, refRes: new Vector2(1170, 2532), match: 0f);

            // Red 180x180 square, top-left anchored, offset (48, -48).
            var square = new GameObject(SquareName, typeof(Image));
            square.transform.SetParent(canvasGo.transform, worldPositionStays: false);
            var img = square.GetComponent<Image>();
            img.color = new Color(1f, 0f, 0f, 1f);
            img.raycastTarget = false;
            var sqRt = (RectTransform)square.transform;
            sqRt.anchorMin = new Vector2(0f, 1f);
            sqRt.anchorMax = new Vector2(0f, 1f);
            sqRt.pivot     = new Vector2(0f, 1f);
            sqRt.anchoredPosition = new Vector2(48f, -48f);
            sqRt.sizeDelta = new Vector2(180f, 180f);

            // TMP label below the square.
            var label = new GameObject(LabelName, typeof(TextMeshProUGUI));
            label.transform.SetParent(canvasGo.transform, worldPositionStays: false);
            var tmp = label.GetComponent<TextMeshProUGUI>();
            tmp.text = "180x180 box (Figma 1170 ref)";
            tmp.fontSize = 30;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.raycastTarget = false;
            // Try to assign Rubik if available; fall back to TMP default silently.
            var rubik = AssetDatabase.FindAssets("Rubik t:TMP_FontAsset");
            if (rubik != null && rubik.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(rubik[0]);
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fa != null) tmp.font = fa;
            }
            var lbRt = (RectTransform)label.transform;
            lbRt.anchorMin = new Vector2(0f, 1f);
            lbRt.anchorMax = new Vector2(0f, 1f);
            lbRt.pivot     = new Vector2(0f, 1f);
            lbRt.anchoredPosition = new Vector2(48f, -240f);
            lbRt.sizeDelta = new Vector2(400f, 60f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.Log($"[CanvasScalerTest] Built scene at {ScenePath}. " +
                      "Set Game View to 1170x2532, then run /Set Test Config N/.");
            EditorUtility.RevealInFinder(ScenePath);
        }

        // -------- Matrix configs --------

        [MenuItem("GOLFIN/Canvas Scaler/Set Test Config 1 (1080x1920, Match=0.5) [BAD]", priority = 200)]
        public static void SetConfig1() => ApplyConfig(new Vector2(1080, 1920), 0.5f, "1");

        [MenuItem("GOLFIN/Canvas Scaler/Set Test Config 2 (1080x1920, Match=0)", priority = 201)]
        public static void SetConfig2() => ApplyConfig(new Vector2(1080, 1920), 0f, "2");

        [MenuItem("GOLFIN/Canvas Scaler/Set Test Config 3 (1170x2532, Match=0.5)", priority = 202)]
        public static void SetConfig3() => ApplyConfig(new Vector2(1170, 2532), 0.5f, "3");

        [MenuItem("GOLFIN/Canvas Scaler/Set Test Config 4 (1170x2532, Match=0) [PROPOSED]", priority = 203)]
        public static void SetConfig4() => ApplyConfig(new Vector2(1170, 2532), 0f, "4");

        static void ApplyConfig(Vector2 refRes, float match, string label)
        {
            var scaler = FindActiveScaler();
            if (scaler == null)
            {
                EditorUtility.DisplayDialog(
                    "Test scene not loaded",
                    $"Open {ScenePath} first (or run GOLFIN/Canvas Scaler/Build Test Scene).",
                    "OK");
                return;
            }

            ApplyConfigToScaler(scaler, refRes, match);

            // Update label so screenshots are self-describing.
            var labelGo = GameObject.Find(LabelName);
            if (labelGo != null)
            {
                var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = $"Config {label}: ref={refRes.x}x{refRes.y}, Match={match}";
                }
            }

            EditorSceneManager.MarkSceneDirty(scaler.gameObject.scene);
            Debug.Log($"[CanvasScalerTest] Applied Config {label}: ref={refRes}, match={match}.");
        }

        static void ApplyConfigToScaler(CanvasScaler scaler, Vector2 refRes, float match)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = refRes;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = match;
            scaler.referencePixelsPerUnit = 100f;
        }

        static CanvasScaler FindActiveScaler()
        {
            var go = GameObject.Find(CanvasName);
            return go != null ? go.GetComponent<CanvasScaler>() : null;
        }

        // -------- Optional: capture screenshot in Play Mode --------

        [MenuItem("GOLFIN/Canvas Scaler/Capture Screenshot (Play Mode only)", priority = 300)]
        public static void CaptureScreenshot()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Enter Play Mode first",
                    "Screenshot capture only works while the editor is in Play Mode.",
                    "OK");
                return;
            }
            if (!Directory.Exists(ScreenshotFolder)) Directory.CreateDirectory(ScreenshotFolder);

            var scaler = FindActiveScaler();
            string tag = "unknown";
            if (scaler != null)
            {
                var r = scaler.referenceResolution;
                tag = $"ref{r.x:0}x{r.y:0}_match{scaler.matchWidthOrHeight:0.##}";
            }
            string filename = $"CanvasScalerTest_{tag}_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string fullPath = Path.Combine(ScreenshotFolder, filename);
            ScreenCapture.CaptureScreenshot(fullPath);
            Debug.Log($"[CanvasScalerTest] Screenshot queued -> {fullPath} (will write next frame).");
        }
    }
}
#endif
