using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// Editor tool that creates the full GOLFIN UI scene hierarchy
/// AND auto-wires all component references. No manual Inspector linking needed.
/// Usage: Unity menu → Tools → Create GOLFIN UI Scene
/// </summary>
public class CreateUIScreen
{
    [MenuItem("Tools/Create GOLFIN UI Scene")]
    public static void CreateUI()
    {
        // ─── Scene Root ──────────────────────────────────────────
        GameObject root = new GameObject("Scene Root");

        // ─── Managers ────────────────────────────────────────────
        GameObject managers = new GameObject("Managers");
        managers.transform.SetParent(root.transform, false);
        var locManager = managers.AddComponent<LocalizationManager>();
        var screenManager = managers.AddComponent<ScreenManager>();
        var bootstrap = managers.AddComponent<GameBootstrap>();

        // ─── Canvas ──────────────────────────────────────────────
        GameObject canvasGO = new GameObject("Canvas");
        canvasGO.transform.SetParent(root.transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1170, 2532);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ─── Create Screens ──────────────────────────────────────
        var logoScreen = CreateLogoScreen(canvasGO.transform);
        var loadingScreen = CreateLoadingScreen(canvasGO.transform);
        var splashScreen = CreateSplashScreen(canvasGO.transform);

        // ─── Wire GameBootstrap ──────────────────────────────────
        SetPrivateField(bootstrap, "logoScreen", logoScreen);
        SetPrivateField(bootstrap, "loadingScreen", loadingScreen);
        SetPrivateField(bootstrap, "splashScreen", splashScreen);

        // ─── EventSystem ─────────────────────────────────────────
        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            GameObject es = new GameObject("EventSystem");
            es.transform.SetParent(root.transform, false);
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Selection.activeGameObject = root;
        Debug.Log("[GOLFIN] UI Scene hierarchy created & wired successfully! ✅");
    }

    // ─── Logo Screen ─────────────────────────────────────────────
    static LogoScreen CreateLogoScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("LogoScreen", parent);
        var component = screen.AddComponent<LogoScreen>();

        CreateImage("Background", screen.transform, Color.black, stretch: true);
        CreateImage("Logo", screen.transform, Color.white, stretch: false);

        return component;
    }

    // ─── Loading Screen ──────────────────────────────────────────
    static LoadingScreen CreateLoadingScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("LoadingScreen", parent);
        var component = screen.AddComponent<LoadingScreen>();

        CreateImage("Background", screen.transform, Color.gray, stretch: true);

        // Pro Tip Card
        GameObject tipCardGO = CreateContainer("ProTipCard", screen.transform);
        var tipCard = tipCardGO.AddComponent<ProTipCard>();

        var header = CreateTMP("Header", tipCardGO.transform, "PRO TIP");
        AddLocalizedText(header, "tip_header");

        CreateImage("Divider", tipCardGO.transform, new Color(0.85f, 0.65f, 0.13f), stretch: false);

        var tipText = CreateTMP("TipText", tipCardGO.transform, "Tip goes here...");
        // No LocalizedText on TipText — ProTipCard manages it dynamically

        GameObject tipImageGO = CreateImage("TipImage", tipCardGO.transform, Color.white, stretch: false);

        var tapNext = CreateTMP("TapNextText", tipCardGO.transform, "TAP FOR NEXT TIP");
        AddLocalizedText(tapNext, "tip_next");

        // Wire ProTipCard references
        SetPrivateField(tipCard, "headerText", header.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tipText", tipText.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tapNextText", tapNext.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tipImages", new Image[] { tipImageGO.GetComponent<Image>() });

        // Loading text
        var nowLoading = CreateTMP("NowLoadingText", screen.transform, "Loading...");
        AddLocalizedText(nowLoading, "loading_text");

        // Loading bar
        GameObject barBG = CreateImage("LoadingBarBG", screen.transform, new Color(0.1f, 0.16f, 0.29f), stretch: false);
        var loadingBar = barBG.AddComponent<LoadingBar>();

        GameObject barFill = CreateImage("LoadingBarFill", barBG.transform, new Color(0.2f, 0.4f, 0.8f), stretch: true);
        barFill.GetComponent<Image>().type = Image.Type.Filled;
        barFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;

        GameObject barGlow = CreateImage("LoadingBarGlow", barBG.transform, new Color(1f, 1f, 1f, 0.3f), stretch: false);

        // Wire LoadingBar references
        SetPrivateField(loadingBar, "fillImage", barFill.GetComponent<Image>());
        SetPrivateField(loadingBar, "glowImage", barGlow.GetComponent<Image>());

        // Download progress
        var downloadProgress = CreateTMP("DownloadProgress", screen.transform, "0 / 0 MB");

        // Wire LoadingScreen references
        SetPrivateField(component, "loadingBar", loadingBar);
        SetPrivateField(component, "proTipCard", tipCard);
        SetPrivateField(component, "nowLoadingText", nowLoading.GetComponent<TextMeshProUGUI>());
        SetPrivateField(component, "downloadProgressText", downloadProgress.GetComponent<TextMeshProUGUI>());

        return component;
    }

    // ─── Splash Screen ───────────────────────────────────────────
    static SplashScreen CreateSplashScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("SplashScreen", parent);
        var component = screen.AddComponent<SplashScreen>();

        CreateImage("Background", screen.transform, new Color(0.1f, 0.3f, 0.15f), stretch: true);

        // Title Area
        GameObject titleArea = CreateContainer("TitleArea", screen.transform);
        var presents = CreateTMP("PresentsText", titleArea.transform, "GOLFIN presents");
        AddLocalizedText(presents, "splash_presents");
        CreateImage("ShieldLogo", titleArea.transform, Color.white, stretch: false);
        var subtitle = CreateTMP("SubtitleText", titleArea.transform, "The Invitational");
        AddLocalizedText(subtitle, "splash_subtitle");

        // Buttons with PressableButton
        GameObject startBtn = CreateButtonWithPressable("StartButton", screen.transform, "START", "btn_start");
        GameObject createBtn = CreateButtonWithPressable("CreateAccountButton", screen.transform, "CREATE ACCOUNT", "btn_create_account");

        // Wire SplashScreen references
        SetPrivateField(component, "startButton", startBtn.GetComponent<PressableButton>());
        SetPrivateField(component, "createAccountButton", createBtn.GetComponent<PressableButton>());

        return component;
    }

    // ─── Helpers ─────────────────────────────────────────────────

    static GameObject CreateScreenPanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        StretchFull(rt);
        panel.AddComponent<CanvasGroup>();
        Image img = panel.AddComponent<Image>();
        img.color = Color.clear;
        return panel;
    }

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

    static GameObject CreateButtonWithPressable(string name, Transform parent, string label, string locKey)
    {
        GameObject buttonGO = CreateImage(name, parent, Color.white, stretch: false);
        buttonGO.AddComponent<Button>();
        buttonGO.AddComponent<PressableButton>();

        GameObject textGO = CreateTMP("Text", buttonGO.transform, label);
        textGO.GetComponent<TextMeshProUGUI>().color = Color.black;
        AddLocalizedText(textGO, locKey);

        return buttonGO;
    }

    static void AddLocalizedText(GameObject go, string key)
    {
        var loc = go.AddComponent<LocalizedText>();
        SetPrivateField(loc, "localizationKey", key);
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Sets a private [SerializeField] via reflection.
    /// This lets us auto-wire references that are normally set in Inspector.
    /// </summary>
    static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);

        if (field != null)
        {
            field.SetValue(target, value);
            EditorUtility.SetDirty(target as Object);
        }
        else
        {
            Debug.LogWarning($"[GOLFIN] Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }
}
