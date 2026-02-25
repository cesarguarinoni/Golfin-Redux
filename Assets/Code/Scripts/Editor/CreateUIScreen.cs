using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Editor tool that creates the full GOLFIN UI scene hierarchy.
/// Usage: Unity menu → Tools → Create GOLFIN UI Scene
/// </summary>
public class CreateUIScreen
{
    [MenuItem("Tools/Create GOLFIN UI Scene")]
    public static void CreateUI()
    {
        // Scene Root
        GameObject root = new GameObject("Scene Root");

        // Managers
        GameObject managers = new GameObject("Managers");
        managers.transform.SetParent(root.transform, false);
        managers.AddComponent<LocalizationManager>();
        managers.AddComponent<ScreenManager>();
        managers.AddComponent<GameBootstrap>();

        // Canvas
        GameObject canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(root.transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1170, 2532);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Create Screens
        CreateLogoScreen(canvasGO.transform);
        CreateLoadingScreen(canvasGO.transform);
        CreateSplashScreen(canvasGO.transform);

        // EventSystem (if not already in scene)
        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            GameObject es = new GameObject("EventSystem");
            es.transform.SetParent(root.transform, false);
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Selection.activeGameObject = root;
        Debug.Log("[GOLFIN] UI Scene hierarchy created successfully!");
    }

    // ─── Logo Screen ────────────────────────────────────────────
    static void CreateLogoScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("LogoScreen", parent);
        CreateImage("Background", screen.transform, Color.black, stretch: true);
        CreateImage("Logo", screen.transform, Color.white, stretch: false);
    }

    // ─── Loading Screen ─────────────────────────────────────────
    static void CreateLoadingScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("LoadingScreen", parent);
        CreateImage("Background", screen.transform, Color.gray, stretch: true);

        // Pro Tip Card
        GameObject tipCard = CreateContainer("ProTipCard", screen.transform);
        CreateTMP("Header", tipCard.transform, "PRO TIP");
        CreateImage("Divider", tipCard.transform, new Color(0.85f, 0.65f, 0.13f), stretch: false); // gold
        CreateTMP("TipText", tipCard.transform, "Tip goes here...");
        CreateImage("TipImage", tipCard.transform, Color.white, stretch: false);
        CreateTMP("TapNextText", tipCard.transform, "TAP FOR NEXT TIP");

        // Loading UI
        CreateTMP("NowLoadingText", screen.transform, "Loading...");

        GameObject barBG = CreateImage("LoadingBarBG", screen.transform, new Color(0.1f, 0.16f, 0.29f), stretch: false);
        GameObject barFill = CreateImage("LoadingBarFill", barBG.transform, new Color(0.2f, 0.4f, 0.8f), stretch: true);
        barFill.GetComponent<Image>().type = Image.Type.Filled;
        barFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;
        CreateImage("LoadingBarGlow", barBG.transform, new Color(1f, 1f, 1f, 0.3f), stretch: false);

        CreateTMP("DownloadProgress", screen.transform, "0 / 0 MB");
    }

    // ─── Splash Screen ──────────────────────────────────────────
    static void CreateSplashScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("SplashScreen", parent);
        CreateImage("Background", screen.transform, new Color(0.1f, 0.3f, 0.15f), stretch: true); // dark green

        // Title Area
        GameObject titleArea = CreateContainer("TitleArea", screen.transform);
        CreateTMP("PresentsText", titleArea.transform, "GOLFIN presents");
        CreateImage("ShieldLogo", titleArea.transform, Color.white, stretch: false);
        CreateTMP("SubtitleText", titleArea.transform, "The Invitational");

        // Buttons
        CreateButton("StartButton", screen.transform, "START");
        CreateButton("CreateAccountButton", screen.transform, "CREATE ACCOUNT");
    }

    // ─── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Creates a screen-level panel: full-stretch, CanvasGroup, transparent Image.
    /// </summary>
    static GameObject CreateScreenPanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        StretchFull(rt);
        panel.AddComponent<CanvasGroup>();
        // Transparent image so raycasts work but nothing blocks visually
        Image img = panel.AddComponent<Image>();
        img.color = Color.clear;
        return panel;
    }

    /// <summary>
    /// Creates an empty container (RectTransform only, no visuals).
    /// </summary>
    static GameObject CreateContainer(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static GameObject CreateImage(string name, Transform parent, Color color, bool stretch = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        if (stretch) StretchFull(rt);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    static GameObject CreateTMP(string name, Transform parent, string text)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 36;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return go;
    }

    static GameObject CreateButton(string name, Transform parent, string label)
    {
        GameObject buttonGO = CreateImage(name, parent, Color.white, stretch: false);
        buttonGO.AddComponent<Button>();
        // TODO: Replace with PressableButton once component is ready
        CreateTMP("Text", buttonGO.transform, label);
        buttonGO.transform.GetComponentInChildren<TextMeshProUGUI>().color = Color.black;
        return buttonGO;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
