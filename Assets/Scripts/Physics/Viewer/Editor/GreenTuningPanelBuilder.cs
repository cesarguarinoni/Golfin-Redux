#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Viewer.Editor
{
    /// <summary>
    /// §2f: Editor builder for GreenTuningPanel widget in LabScaffold.unity.
    /// Run via GOLFIN > Dev > Build Green Tuning Panel.
    /// </summary>
    public static class GreenTuningPanelBuilder
    {
        [MenuItem("GOLFIN/Dev/Build Green Tuning Panel")]
        public static void Build()
        {
            string scenePath = "Assets/Scenes/Physics/LabScaffold.unity";
            var scene = EditorSceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            // Find ShotUI_Canvas
            Canvas targetCanvas = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var canvases = root.GetComponentsInChildren<Canvas>(true);
                foreach (var c in canvases)
                {
                    if (c.gameObject.name == "ShotUI_Canvas")
                    {
                        targetCanvas = c;
                        break;
                    }
                }
                if (targetCanvas != null) break;
            }

            if (targetCanvas == null)
            {
                Debug.LogError("[§2f] Could not find ShotUI_Canvas in LabScaffold.unity");
                return;
            }

            // Idempotent: remove existing GreenTuningPanel
            var existing = targetCanvas.transform.Find("GreenTuningPanel");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
                Debug.Log("[§2f] Removed existing GreenTuningPanel (idempotent rebuild)");
            }

            Font defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (defaultFont == null)
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // ── Root: GreenTuningPanel ────────────────────────────────────────
            var rootGO = new GameObject("GreenTuningPanel");
            Undo.RegisterCreatedObjectUndo(rootGO, "Create GreenTuningPanel");
            rootGO.transform.SetParent(targetCanvas.transform, false);
            var rootRT = rootGO.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.one;
            rootRT.anchorMax = Vector2.one;
            rootRT.pivot     = Vector2.one;
            rootRT.sizeDelta = Vector2.zero;

            var panelScript = rootGO.AddComponent<GreenTuningPanel>();

            // ── Toggle host: the in-game HUD SettingsButton ───────────────────
            // There is NO dedicated toggle icon any more. The panel used to own a
            // 60x60 flat-green "G" square anchored top-right, which sat directly on
            // top of the SettingsButton wheel (SettingsButton is at -58,-24 / 86x86;
            // the green square was at -20,-20 / 60x60 — a near-total overlap).
            // The wheel itself is now the toggle.
            var settingsBtnT = targetCanvas.transform.Find("SettingsButton");
            var toggleBtn    = settingsBtnT != null ? settingsBtnT.GetComponent<Button>() : null;
            if (toggleBtn == null)
            {
                Debug.LogError("[§2f] SettingsButton (with a Button component) not found under ShotUI_Canvas — " +
                               "GreenTuningPanel has no toggle host. Aborting build.");
                Undo.DestroyObjectImmediate(rootGO);
                return;
            }

            // ── PanelRoot (320×220, top-right below toggle, initially inactive)
            var panelRootGO = new GameObject("PanelRoot");
            panelRootGO.transform.SetParent(rootGO.transform, false);
            var panelRootRT = panelRootGO.AddComponent<RectTransform>();
            panelRootRT.anchorMin        = Vector2.one;
            panelRootRT.anchorMax        = Vector2.one;
            panelRootRT.pivot            = Vector2.one;
            panelRootRT.sizeDelta        = new Vector2(320f, 220f);
            panelRootRT.anchoredPosition = new Vector2(-20f, -90f);
            var panelRootImg = panelRootGO.AddComponent<Image>();
            panelRootImg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            panelRootGO.SetActive(false);

            var vlg = panelRootGO.AddComponent<VerticalLayoutGroup>();
            vlg.padding                = new RectOffset(10, 10, 10, 10);
            vlg.spacing                = 8f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panelRootGO.transform, false);
            titleGO.AddComponent<RectTransform>().sizeDelta = new Vector2(300f, 24f);
            titleGO.AddComponent<LayoutElement>().preferredHeight = 24f;
            var titleText = titleGO.AddComponent<Text>();
            titleText.text      = "GREEN TUNING";
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.fontSize  = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color     = Color.white;
            titleText.font      = defaultFont;

            // RollResistRow
            var rollRowGO  = CreateRow("RollResistRow", panelRootGO.transform);
            var rollLblText = CreateLabel("RollResistLabel", rollRowGO.transform, "Roll Resist: 0.120", defaultFont, 130f);
            var rollSlider  = CreateSlider("RollResistSlider", rollRowGO.transform);

            // StopSpeedRow
            var stopRowGO  = CreateRow("StopSpeedRow", panelRootGO.transform);
            var stopLblText = CreateLabel("StopSpeedLabel", stopRowGO.transform, "Stop Speed: 0.050 m/s", defaultFont, 130f);
            var stopSlider  = CreateSlider("StopSpeedSlider", stopRowGO.transform);

            // ResetButton
            var resetBtnGO = new GameObject("ResetButton");
            resetBtnGO.transform.SetParent(panelRootGO.transform, false);
            resetBtnGO.AddComponent<RectTransform>().sizeDelta = new Vector2(300f, 36f);
            resetBtnGO.AddComponent<LayoutElement>().preferredHeight = 36f;
            var resetBtnImg = resetBtnGO.AddComponent<Image>();
            resetBtnImg.color = new Color(0.55f, 0.15f, 0.15f, 0.9f);
            var resetBtn = resetBtnGO.AddComponent<Button>();

            var resetTextGO = new GameObject("Text");
            resetTextGO.transform.SetParent(resetBtnGO.transform, false);
            var resetTextRT = resetTextGO.AddComponent<RectTransform>();
            resetTextRT.anchorMin = Vector2.zero;
            resetTextRT.anchorMax = Vector2.one;
            resetTextRT.sizeDelta = Vector2.zero;
            resetTextRT.offsetMin = Vector2.zero;
            resetTextRT.offsetMax = Vector2.zero;
            var resetText = resetTextGO.AddComponent<Text>();
            resetText.text      = "Reset";
            resetText.alignment = TextAnchor.MiddleCenter;
            resetText.fontSize  = 14;
            resetText.color     = Color.white;
            resetText.font      = defaultFont;

            // ── Wire SerializeField references ─────────────────────────────────
            var so = new SerializedObject(panelScript);
            so.FindProperty("panelRoot").objectReferenceValue             = panelRootGO;
            so.FindProperty("toggleButton").objectReferenceValue          = toggleBtn;
            so.FindProperty("rollingResistanceSlider").objectReferenceValue = rollSlider;
            so.FindProperty("stopSpeedSlider").objectReferenceValue       = stopSlider;
            so.FindProperty("rollingResistanceLabel").objectReferenceValue = rollLblText;
            so.FindProperty("stopSpeedLabel").objectReferenceValue        = stopLblText;
            so.FindProperty("resetButton").objectReferenceValue           = resetBtn;
            so.ApplyModifiedProperties();

            // Save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[§2f] GreenTuningPanel built and LabScaffold.unity saved.");
        }

        static GameObject CreateRow(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>().sizeDelta = new Vector2(300f, 30f);
            go.AddComponent<LayoutElement>().preferredHeight = 30f;
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing                = 8f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = true;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            return go;
        }

        static Text CreateLabel(string name, Transform parent, string text, Font font, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth  = 0f;
            var t = go.AddComponent<Text>();
            t.text      = text;
            t.fontSize  = 12;
            t.color     = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.font      = font;
            return t;
        }

        static Slider CreateSlider(string name, Transform parent)
        {
            var sliderGO = new GameObject(name);
            sliderGO.transform.SetParent(parent, false);
            sliderGO.AddComponent<RectTransform>().sizeDelta = new Vector2(150f, 20f);
            var le = sliderGO.AddComponent<LayoutElement>();
            le.flexibleWidth   = 1f;
            le.preferredHeight = 20f;
            var slider = sliderGO.AddComponent<Slider>();

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(sliderGO.transform, false);
            var bgRT = bgGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.25f);
            bgRT.anchorMax = new Vector2(1f, 0.75f);
            bgRT.sizeDelta = Vector2.zero;
            bgGO.AddComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

            // Fill Area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRT.offsetMin = new Vector2(5f, 0f);
            fillAreaRT.offsetMax = new Vector2(-15f, 0f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.sizeDelta = new Vector2(10f, 0f);
            fillGO.AddComponent<Image>().color = new Color(0.3f, 0.8f, 0.3f, 1f);

            // Handle Slide Area
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10f, 0f);
            handleAreaRT.offsetMax = new Vector2(-10f, 0f);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.anchorMin = new Vector2(0f, 0f);
            handleRT.anchorMax = new Vector2(0f, 1f);
            handleRT.sizeDelta = new Vector2(20f, 0f);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;

            slider.fillRect    = fillRT;
            slider.handleRect  = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction   = Slider.Direction.LeftToRight;
            slider.value       = 0f;

            return slider;
        }
    }
}
#endif
