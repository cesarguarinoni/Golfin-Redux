using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// Editor tool that creates the full GOLFIN UI scene hierarchy
/// with exact positions matching the reference designs (1170×2532).
/// Auto-wires all component references via reflection.
/// Usage: Unity menu → Tools → Create GOLFIN UI Scene
/// </summary>
public class CreateUIScreen
{
    // Reference resolution
    const float W = 1170f;
    const float H = 2532f;

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
        scaler.referenceResolution = new Vector2(W, H);
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
        Debug.Log("[GOLFIN] UI Scene created with exact layout from reference designs! ✅");
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGO SCREEN — Black bg, centered GOLFIN logo at ~38.5% Y
    // ═══════════════════════════════════════════════════════════════
    static LogoScreen CreateLogoScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("LogoScreen", parent);
        var component = screen.AddComponent<LogoScreen>();

        // Full black background
        CreateImageStretched("Background", screen.transform, Color.black);

        // Logo — centered at 50% X, 38.5% Y, ~52% width, ~5.5% height
        var logo = CreateImageAnchored("Logo", screen.transform, Color.white,
            anchorCenter: new Vector2(0.5f, 1f - 0.385f),  // anchor Y is inverted
            size: new Vector2(608f, 139f));

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // LOADING SCREEN — Bg image, ProTip card, loading bar
    // ═══════════════════════════════════════════════════════════════
    static LoadingScreen CreateLoadingScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("LoadingScreen", parent);
        var component = screen.AddComponent<LoadingScreen>();

        // Background (full bleed golf course image)
        CreateImageStretched("Background", screen.transform, new Color(0.15f, 0.25f, 0.1f));

        // ─── Pro Tip Card ─────────────────────────────────────────
        // Card: centered X, top 17.5% → bottom 58%, width 84.6%
        GameObject tipCardGO = CreateImageAnchored("ProTipCard", screen.transform,
            new Color(0.12f, 0.16f, 0.14f, 0.7f),  // semi-transparent dark
            anchorMin: new Vector2(0.077f, 1f - 0.58f),
            anchorMax: new Vector2(0.923f, 1f - 0.148f));
        var tipCard = tipCardGO.AddComponent<ProTipCard>();

        // "PRO TIP" header — centered X, Y ~19.8%
        var header = CreateTMPAnchored("Header", tipCardGO.transform, "PRO TIP",
            anchorCenter: new Vector2(0.5f, 1f - NormalizeY(501f, 375f, 1469f)),
            fontSize: 52f, alignment: TextAlignmentOptions.Center);
        SetTMPStyle(header, spacing: 4f, fontStyle: FontStyles.Bold);
        AddLocalizedText(header, "tip_header");

        // Gold divider — centered X, Y ~21.2%, width 70% of screen
        CreateImageAnchored("Divider", tipCardGO.transform,
            new Color(0.78f, 0.66f, 0.31f),  // #C8A84E gold
            anchorCenter: new Vector2(0.5f, 1f - NormalizeY(537f, 375f, 1469f)),
            size: new Vector2(819f, 3f));

        // Tip text — centered, Y ~26.5%
        var tipText = CreateTMPAnchored("TipText", tipCardGO.transform, "Tip goes here...",
            anchorCenter: new Vector2(0.5f, 1f - NormalizeY(671f, 375f, 1469f)),
            fontSize: 38f, alignment: TextAlignmentOptions.Center,
            size: new Vector2(866f, 177f));
        SetTMPStyle(tipText, fontStyle: FontStyles.Bold);

        // Tip image — centered, Y ~42%
        var tipImageGO = CreateImageAnchored("TipImage", tipCardGO.transform, Color.white,
            anchorCenter: new Vector2(0.5f, 1f - NormalizeY(1063f, 375f, 1469f)),
            size: new Vector2(491f, 456f));

        // "TAP FOR NEXT TIP" — right aligned, Y ~57%
        var tapNext = CreateTMPAnchored("TapNextText", tipCardGO.transform, "TAP FOR NEXT TIP",
            anchorCenter: new Vector2(0.8f, 1f - NormalizeY(1443f, 375f, 1469f)),
            fontSize: 24f, alignment: TextAlignmentOptions.Right,
            size: new Vector2(400f, 40f));
        SetTMPStyle(tapNext, spacing: 1f, fontStyle: FontStyles.Italic);
        AddLocalizedText(tapNext, "tip_next");

        // Wire ProTipCard
        SetPrivateField(tipCard, "headerText", header.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tipText", tipText.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tapNextText", tapNext.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tipImages", new Image[] { tipImageGO.GetComponent<Image>() });

        // ─── NOW LOADING text — centered, Y ~82.5% ───────────────
        var nowLoading = CreateTMPAnchored("NowLoadingText", screen.transform, "NOW LOADING",
            anchorCenter: new Vector2(0.5f, 1f - 0.825f),
            fontSize: 72f, alignment: TextAlignmentOptions.Center);
        SetTMPStyle(nowLoading, spacing: 3f, fontStyle: FontStyles.Bold);
        AddLocalizedText(nowLoading, "screen_loading");

        // ─── Loading Bar BG — centered, Y ~87.5%, 72% width ──────
        var barBG = CreateImageAnchored("LoadingBarBG", screen.transform,
            new Color(0.08f, 0.12f, 0.16f, 0.7f),  // dark semi-transparent
            anchorCenter: new Vector2(0.5f, 1f - 0.875f),
            size: new Vector2(842f, 32f));
        var loadingBar = barBG.AddComponent<LoadingBar>();

        // Bar fill
        var barFill = CreateImageStretched("LoadingBarFill", barBG.transform,
            new Color(0.23f, 0.38f, 0.85f));  // #3B7DD8
        barFill.GetComponent<Image>().type = Image.Type.Filled;
        barFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;

        // Bar glow
        var barGlow = CreateImageAnchored("LoadingBarGlow", barBG.transform,
            new Color(1f, 1f, 1f, 0.3f),
            anchorCenter: new Vector2(0f, 0.5f),
            size: new Vector2(20f, 25f));

        // Wire LoadingBar
        SetPrivateField(loadingBar, "fillImage", barFill.GetComponent<Image>());
        SetPrivateField(loadingBar, "glowImage", barGlow.GetComponent<Image>());

        // ─── Download progress — right of center, Y ~90% ─────────
        var downloadProgress = CreateTMPAnchored("DownloadProgress", screen.transform, "0 / 267 MB",
            anchorCenter: new Vector2(0.6f, 1f - 0.90f),
            fontSize: 28f, alignment: TextAlignmentOptions.Right,
            size: new Vector2(400f, 50f));
        SetTMPStyle(downloadProgress, fontStyle: FontStyles.Bold);

        // Wire LoadingScreen
        SetPrivateField(component, "loadingBar", loadingBar);
        SetPrivateField(component, "proTipCard", tipCard);
        SetPrivateField(component, "nowLoadingText", nowLoading.GetComponent<TextMeshProUGUI>());
        SetPrivateField(component, "downloadProgressText", downloadProgress.GetComponent<TextMeshProUGUI>());

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // SPLASH SCREEN — Game art bg, title, START & CREATE ACCOUNT
    // ═══════════════════════════════════════════════════════════════
    static SplashScreen CreateSplashScreen(Transform parent)
    {
        GameObject screen = CreateScreenPanel("SplashScreen", parent);
        var component = screen.AddComponent<SplashScreen>();

        // Background (golf course art)
        CreateImageStretched("Background", screen.transform, new Color(0.1f, 0.2f, 0.15f));

        // ─── Title Area ───────────────────────────────────────────
        GameObject titleArea = new GameObject("TitleArea");
        titleArea.transform.SetParent(screen.transform, false);
        RectTransform titleRT = titleArea.AddComponent<RectTransform>();
        // Title area spans top portion: 5.9% → 20.3% Y
        titleRT.anchorMin = new Vector2(0.15f, 1f - 0.203f);
        titleRT.anchorMax = new Vector2(0.85f, 1f - 0.059f);
        titleRT.offsetMin = Vector2.zero;
        titleRT.offsetMax = Vector2.zero;

        // "GOLFIN presents" — Y ~6.5%
        var presents = CreateTMPAnchored("PresentsText", titleArea.transform, "GOLFIN presents",
            anchorCenter: new Vector2(0.5f, 0.85f),
            fontSize: 58f, alignment: TextAlignmentOptions.Center,
            size: new Vector2(820f, 120f));
        SetTMPStyle(presents, spacing: 4f, fontStyle: FontStyles.Bold);
        AddLocalizedText(presents, "splash_presents");

        // Shield logo — centered at ~24% X, ~16.2% Y of screen
        CreateImageAnchored("ShieldLogo", titleArea.transform, Color.white,
            anchorCenter: new Vector2(0.15f, 0.55f),
            size: new Vector2(175f, 200f));

        // "The Invitational" — Y ~13%
        var subtitle = CreateTMPAnchored("SubtitleText", titleArea.transform, "The Invitational",
            anchorCenter: new Vector2(0.55f, 0.25f),
            fontSize: 100f, alignment: TextAlignmentOptions.Center,
            size: new Vector2(810f, 210f));
        SetTMPStyle(subtitle, fontStyle: FontStyles.Italic);
        AddLocalizedText(subtitle, "splash_subtitle");

        // ─── Dark gradient overlay at bottom ──────────────────────
        var gradient = CreateImageAnchored("BottomGradient", screen.transform,
            new Color(0f, 0f, 0f, 0.7f),
            anchorMin: new Vector2(0f, 0f),
            anchorMax: new Vector2(1f, 1f - 0.731f));

        // ─── START button — centered, Y ~83.5% ───────────────────
        var startBtn = CreateImageAnchored("StartButton", screen.transform,
            new Color(0.36f, 0.75f, 0.16f),  // #5CBF2A green
            anchorCenter: new Vector2(0.5f, 1f - 0.835f),
            size: new Vector2(480f, 130f));
        startBtn.AddComponent<Button>();
        startBtn.AddComponent<PressableButton>();

        var startText = CreateTMPAnchored("Text", startBtn.transform, "START",
            anchorCenter: new Vector2(0.5f, 0.5f),
            fontSize: 72f, alignment: TextAlignmentOptions.Center);
        startText.GetComponent<TextMeshProUGUI>().color = Color.white;
        SetTMPStyle(startText, spacing: 6f, fontStyle: FontStyles.Bold);
        AddLocalizedText(startText, "btn_start");

        // ─── CREATE ACCOUNT — centered, Y ~91.2%, text only ──────
        var createBtn = CreateImageAnchored("CreateAccountButton", screen.transform,
            Color.clear,  // transparent — text-only button
            anchorCenter: new Vector2(0.5f, 1f - 0.912f),
            size: new Vector2(680f, 100f));
        createBtn.AddComponent<Button>();
        createBtn.AddComponent<PressableButton>();

        var createText = CreateTMPAnchored("Text", createBtn.transform, "CREATE ACCOUNT",
            anchorCenter: new Vector2(0.5f, 0.5f),
            fontSize: 62f, alignment: TextAlignmentOptions.Center);
        createText.GetComponent<TextMeshProUGUI>().color = Color.white;
        SetTMPStyle(createText, spacing: 4f, fontStyle: FontStyles.Bold);
        AddLocalizedText(createText, "btn_create_account");

        // Wire SplashScreen
        SetPrivateField(component, "startButton", startBtn.GetComponent<PressableButton>());
        SetPrivateField(component, "createAccountButton", createBtn.GetComponent<PressableButton>());

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // HELPER METHODS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Normalize a screen-space Y pixel into a local Y within a card (0-1)</summary>
    static float NormalizeY(float screenY, float cardTopY, float cardBottomY)
    {
        return (screenY - cardTopY) / (cardBottomY - cardTopY);
    }

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

    /// <summary>Full-stretch image (fills parent)</summary>
    static GameObject CreateImageStretched(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        StretchFull(rt);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    /// <summary>Image positioned by anchor center + fixed size</summary>
    static GameObject CreateImageAnchored(string name, Transform parent, Color color,
        Vector2 anchorCenter = default, Vector2 size = default,
        Vector2 anchorMin = default, Vector2 anchorMax = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();

        if (anchorMin != default || anchorMax != default)
        {
            // Stretch between anchors
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            // Fixed size at anchor point
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.sizeDelta = size == default ? new Vector2(100f, 100f) : size;
            rt.anchoredPosition = Vector2.zero;
        }

        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    /// <summary>TMP text positioned by anchor center</summary>
    static GameObject CreateTMPAnchored(string name, Transform parent, string text,
        Vector2 anchorCenter = default, float fontSize = 36f,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        Vector2 size = default)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorCenter;
        rt.anchorMax = anchorCenter;
        rt.sizeDelta = size == default ? new Vector2(800f, 100f) : size;
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    static void SetTMPStyle(GameObject go, float spacing = 0f,
        FontStyles fontStyle = FontStyles.Normal)
    {
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;
        tmp.characterSpacing = spacing;
        tmp.fontStyle = fontStyle;
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
