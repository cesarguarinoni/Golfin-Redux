using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// Editor tool that creates OR updates the GOLFIN UI scene hierarchy.
/// 
/// Features:
///   - CREATE mode: builds entire hierarchy from scratch (first run)
///   - UPDATE mode: finds existing objects, updates layout/fonts/wiring
///     but PRESERVES manually assigned sprites and Inspector tweaks
///   - Auto-loads sprites from known asset paths
///   - Auto-wires all SerializeField references via reflection
///   - Positions match reference designs (1170×2532)
///
/// Usage: Unity menu → Tools → Create GOLFIN UI Scene
///        (safe to run multiple times)
/// </summary>
public class CreateUIScreen
{
    // Reference resolution
    const float W = 1170f;
    const float H = 2532f;

    // ─── Known sprite asset paths (auto-assigned if found) ───────
    // Place your images at these paths and they'll be assigned automatically.
    // If a file doesn't exist yet, the slot is left empty (or preserved from previous run).
    static class SpritePaths
    {
        public const string Logo             = "Assets/Art/UI/golfin_logo.png";
        public const string SplashTitle      = "Assets/Art/UI/splash_title.png";
        public const string SplashBackground = "Assets/Art/UI/splash_bg.png";
        public const string LoadingBackground = "Assets/Art/UI/loading_bg.png";
        public const string LoadingBarPill   = "Assets/Art/UI/pill_bar.png";
        // Tip images: Assets/Art/UI/Tips/tip_0.png, tip_1.png, ...
        public const string TipImageFolder   = "Assets/Art/UI/Tips/";
    }

    // ═══════════════════════════════════════════════════════════════
    // MAIN ENTRY POINT
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Tools/Create GOLFIN UI Scene")]
    public static void CreateUI()
    {
        // Check if hierarchy already exists (UPDATE mode)
        GameObject root = GameObject.Find("Scene Root");
        bool isUpdate = root != null;

        if (!isUpdate)
        {
            root = new GameObject("Scene Root");
            Debug.Log("[GOLFIN] Creating new UI scene hierarchy...");
        }
        else
        {
            Debug.Log("[GOLFIN] Updating existing UI scene (sprites preserved)...");
        }

        // ─── Managers ────────────────────────────────────────────
        GameObject managers = FindOrCreate("Managers", root.transform);
        var locManager = EnsureComponent<LocalizationManager>(managers);
        var screenManager = EnsureComponent<ScreenManager>(managers);
        var bootstrap = EnsureComponent<GameBootstrap>(managers);

        // ─── Canvas ──────────────────────────────────────────────
        GameObject canvasGO = FindOrCreate("Canvas", root.transform);
        var canvas = EnsureComponent<Canvas>(canvasGO);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = EnsureComponent<CanvasScaler>(canvasGO);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureComponent<GraphicRaycaster>(canvasGO);

        // ─── Create/Update Screens ───────────────────────────────
        var logoScreen = SetupLogoScreen(canvasGO.transform);
        var loadingScreen = SetupLoadingScreen(canvasGO.transform);
        var splashScreen = SetupSplashScreen(canvasGO.transform);

        // ─── Wire GameBootstrap ──────────────────────────────────
        SetPrivateField(bootstrap, "logoScreen", logoScreen);
        SetPrivateField(bootstrap, "loadingScreen", loadingScreen);
        SetPrivateField(bootstrap, "splashScreen", splashScreen);

        // ─── EventSystem ─────────────────────────────────────────
        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            GameObject es = FindOrCreate("EventSystem", root.transform);
            EnsureComponent<UnityEngine.EventSystems.EventSystem>(es);
            EnsureComponent<UnityEngine.EventSystems.StandaloneInputModule>(es);
        }

        Selection.activeGameObject = root;
        string mode = isUpdate ? "Updated" : "Created";
        Debug.Log($"[GOLFIN] {mode} UI scene successfully! ✅");
    }

    // ═══════════════════════════════════════════════════════════════
    // LOGO SCREEN
    // ═══════════════════════════════════════════════════════════════
    static LogoScreen SetupLogoScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LogoScreen", parent);
        var component = EnsureComponent<LogoScreen>(screen);

        // Full black background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        bg.GetComponent<Image>().color = Color.black;

        // Logo — centered at 50% X, 38.5% Y
        var logo = FindOrCreateImageAnchored("Logo", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - 0.385f),
            size: new Vector2(608f, 139f));
        TryAssignSprite(logo, SpritePaths.Logo, Color.white);

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // LOADING SCREEN
    // ═══════════════════════════════════════════════════════════════
    static LoadingScreen SetupLoadingScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LoadingScreen", parent);
        var component = EnsureComponent<LoadingScreen>(screen);

        // Background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.LoadingBackground, new Color(0.15f, 0.25f, 0.1f));

        // ─── Pro Tip Card ─────────────────────────────────────────
        GameObject tipCardGO = FindOrCreate("ProTipCard", screen.transform);
        var tipCardRT = EnsureComponent<RectTransform>(tipCardGO);
        tipCardRT.anchorMin = new Vector2(0.077f, 1f);
        tipCardRT.anchorMax = new Vector2(0.923f, 1f);
        tipCardRT.pivot = new Vector2(0.5f, 1f);
        tipCardRT.anchoredPosition = new Vector2(0f, -375f);

        var tipCardBg = EnsureComponent<Image>(tipCardGO);
        if (!HasSprite(tipCardGO)) tipCardBg.color = new Color(0.12f, 0.16f, 0.14f, 0.7f);

        var layout = EnsureComponent<VerticalLayoutGroup>(tipCardGO);
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = EnsureComponent<ContentSizeFitter>(tipCardGO);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tipCard = EnsureComponent<ProTipCard>(tipCardGO);

        // Header
        var header = FindOrCreateLayoutTMP("Header", tipCardGO.transform, "PRO TIP",
            52f, 70f);
        SetTMPStyle(header, 8f, FontStyles.Bold | FontStyles.UpperCase, "Montserrat-Bold SDF");
        EnsureLocalizedText(header, "tip_header");

        // Divider
        var divider = FindOrCreateLayoutImage("Divider", tipCardGO.transform,
            new Color(0.78f, 0.66f, 0.31f), 3f);

        // Tip text
        var tipText = FindOrCreateLayoutTMP("TipText", tipCardGO.transform, "Tip goes here...",
            38f, -1f);
        SetTMPStyle(tipText, 0f, FontStyles.Bold | FontStyles.UpperCase, "Montserrat-SemiBold SDF");

        // Tip image
        var tipImageGO = FindOrCreateLayoutImage("TipImage", tipCardGO.transform,
            Color.white, 456f);
        // Try to assign first tip image
        TryAssignSprite(tipImageGO, SpritePaths.TipImageFolder + "tip_0.png");

        // Tap next
        var tapNext = FindOrCreateLayoutTMP("TapNextText", tipCardGO.transform, "TAP FOR NEXT TIP",
            24f, 40f, TextAlignmentOptions.Right);
        SetTMPStyle(tapNext, 2f, FontStyles.Italic | FontStyles.UpperCase, "Montserrat-Italic SDF");
        EnsureLocalizedText(tapNext, "tip_next");

        // Wire ProTipCard
        SetPrivateField(tipCard, "headerText", header.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tipText", tipText.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "tapNextText", tapNext.GetComponent<TextMeshProUGUI>());
        SetPrivateField(tipCard, "dividerImage", divider.GetComponent<Image>());

        // Auto-load all tip images from folder
        var tipSprites = LoadTipSprites();
        if (tipSprites.Length > 0)
        {
            Image[] tipImgArray = new Image[tipSprites.Length];
            for (int i = 0; i < tipSprites.Length; i++)
            {
                var tipImgObj = FindOrCreateLayoutImage($"TipImage_{i}", tipCardGO.transform,
                    Color.white, 456f);
                tipImgObj.GetComponent<Image>().sprite = tipSprites[i];
                tipImgObj.GetComponent<Image>().preserveAspect = true;
                tipImgArray[i] = tipImgObj.GetComponent<Image>();
                // Hide all except first (ProTipCard toggles them)
                tipImgObj.SetActive(i == 0);
            }
            SetPrivateField(tipCard, "tipImages", tipImgArray);
            // Remove the placeholder TipImage if we have real ones
            if (tipSprites.Length > 0)
            {
                var placeholder = tipCardGO.transform.Find("TipImage");
                if (placeholder != null) Object.DestroyImmediate(placeholder.gameObject);
            }
        }
        else
        {
            SetPrivateField(tipCard, "tipImages", new Image[] { tipImageGO.GetComponent<Image>() });
        }

        // ─── NOW LOADING ──────────────────────────────────────────
        var nowLoading = FindOrCreateTMPAnchored("NowLoadingText", screen.transform, "NOW LOADING",
            new Vector2(0.5f, 1f - 0.825f), 72f);
        SetTMPStyle(nowLoading, 4f, FontStyles.Bold | FontStyles.UpperCase, "Montserrat-Black SDF");
        nowLoading.GetComponent<TextMeshProUGUI>().outlineWidth = 0.15f;
        nowLoading.GetComponent<TextMeshProUGUI>().outlineColor = new Color32(0, 0, 0, 128);
        EnsureLocalizedText(nowLoading, "screen_loading");

        // ─── Loading Bar ──────────────────────────────────────────
        var barBG = FindOrCreateImageAnchored("LoadingBarBG", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - 0.875f),
            size: new Vector2(842f, 32f));
        if (!HasSprite(barBG)) barBG.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 0.7f);
        TryAssignSprite(barBG, SpritePaths.LoadingBarPill);
        var loadingBar = EnsureComponent<LoadingBar>(barBG);

        var barFill = FindOrCreateImageStretched("LoadingBarFill", barBG.transform);
        barFill.GetComponent<Image>().color = new Color(0.23f, 0.38f, 0.85f);
        barFill.GetComponent<Image>().type = Image.Type.Filled;
        barFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;
        TryAssignSprite(barFill, SpritePaths.LoadingBarPill);

        var barGlow = FindOrCreateImageAnchored("LoadingBarGlow", barBG.transform,
            anchorCenter: new Vector2(0f, 0.5f),
            size: new Vector2(20f, 25f));
        if (!HasSprite(barGlow)) barGlow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);

        SetPrivateField(loadingBar, "fillImage", barFill.GetComponent<Image>());
        SetPrivateField(loadingBar, "glowImage", barGlow.GetComponent<Image>());

        // ─── Download progress ────────────────────────────────────
        var downloadProgress = FindOrCreateTMPAnchored("DownloadProgress", screen.transform, "0 / 267 MB",
            new Vector2(0.6f, 1f - 0.90f), 28f, TextAlignmentOptions.Right,
            new Vector2(400f, 50f));
        SetTMPStyle(downloadProgress, 0f, FontStyles.Bold, "Montserrat-Bold SDF");

        // Wire LoadingScreen
        SetPrivateField(component, "loadingBar", loadingBar);
        SetPrivateField(component, "proTipCard", tipCard);
        SetPrivateField(component, "nowLoadingText", nowLoading.GetComponent<TextMeshProUGUI>());
        SetPrivateField(component, "downloadProgressText", downloadProgress.GetComponent<TextMeshProUGUI>());

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // SPLASH SCREEN
    // ═══════════════════════════════════════════════════════════════
    static SplashScreen SetupSplashScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("SplashScreen", parent);
        var component = EnsureComponent<SplashScreen>(screen);

        // Background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.SplashBackground, new Color(0.1f, 0.2f, 0.15f));

        // Title (single combined image)
        var titleImage = FindOrCreateImageAnchored("TitleArea", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - 0.13f),
            size: new Vector2(990f, 365f));
        titleImage.GetComponent<Image>().preserveAspect = true;
        TryAssignSprite(titleImage, SpritePaths.SplashTitle, Color.white);

        // Bottom gradient
        var gradient = FindOrCreate("BottomGradient", screen.transform);
        EnsureComponent<RectTransform>(gradient);
        var gradRT = gradient.GetComponent<RectTransform>();
        gradRT.anchorMin = new Vector2(0f, 0f);
        gradRT.anchorMax = new Vector2(1f, 1f - 0.731f);
        gradRT.offsetMin = Vector2.zero;
        gradRT.offsetMax = Vector2.zero;
        var gradImg = EnsureComponent<Image>(gradient);
        if (!HasSprite(gradient)) gradImg.color = new Color(0f, 0f, 0f, 0.7f);

        // START button
        var startBtn = FindOrCreateImageAnchored("StartButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - 0.835f),
            size: new Vector2(480f, 130f));
        if (!HasSprite(startBtn)) startBtn.GetComponent<Image>().color = new Color(0.36f, 0.75f, 0.16f);
        EnsureComponent<Button>(startBtn);
        EnsureComponent<PressableButton>(startBtn);

        var startText = FindOrCreateTMPAnchored("Text", startBtn.transform, "START",
            new Vector2(0.5f, 0.5f), 72f);
        startText.GetComponent<TextMeshProUGUI>().color = Color.white;
        startText.GetComponent<TextMeshProUGUI>().outlineWidth = 0.1f;
        startText.GetComponent<TextMeshProUGUI>().outlineColor = new Color32(0, 0, 0, 100);
        SetTMPStyle(startText, 6f, FontStyles.Bold | FontStyles.UpperCase, "Montserrat-Bold SDF");
        EnsureLocalizedText(startText, "btn_start");

        // CREATE ACCOUNT button
        var createBtn = FindOrCreateImageAnchored("CreateAccountButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - 0.912f),
            size: new Vector2(680f, 100f));
        createBtn.GetComponent<Image>().color = Color.clear;
        EnsureComponent<Button>(createBtn);
        EnsureComponent<PressableButton>(createBtn);

        var createText = FindOrCreateTMPAnchored("Text", createBtn.transform, "CREATE ACCOUNT",
            new Vector2(0.5f, 0.5f), 62f);
        createText.GetComponent<TextMeshProUGUI>().color = Color.white;
        createText.GetComponent<TextMeshProUGUI>().outlineWidth = 0.1f;
        createText.GetComponent<TextMeshProUGUI>().outlineColor = new Color32(0, 0, 0, 150);
        SetTMPStyle(createText, 4f, FontStyles.Bold | FontStyles.UpperCase, "Montserrat-Bold SDF");
        EnsureLocalizedText(createText, "btn_create_account");

        // Wire SplashScreen
        SetPrivateField(component, "startButton", startBtn.GetComponent<PressableButton>());
        SetPrivateField(component, "createAccountButton", createBtn.GetComponent<PressableButton>());

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // FIND-OR-CREATE HELPERS (core of update mode)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Find existing child by name, or create new one</summary>
    static GameObject FindOrCreate(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    /// <summary>Ensure a component exists on a GameObject (add if missing)</summary>
    static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        if (comp == null) comp = go.AddComponent<T>();
        return comp;
    }

    static GameObject FindOrCreateScreenPanel(string name, Transform parent)
    {
        GameObject panel = FindOrCreate(name, parent);
        var rt = EnsureComponent<RectTransform>(panel);
        StretchFull(rt);
        EnsureComponent<CanvasGroup>(panel);
        var img = EnsureComponent<Image>(panel);
        if (!HasSprite(panel)) img.color = Color.clear;
        return panel;
    }

    static GameObject FindOrCreateImageStretched(string name, Transform parent)
    {
        GameObject go = FindOrCreate(name, parent);
        var rt = EnsureComponent<RectTransform>(go);
        StretchFull(rt);
        EnsureComponent<Image>(go);
        return go;
    }

    static GameObject FindOrCreateImageAnchored(string name, Transform parent,
        Vector2 anchorCenter = default, Vector2 size = default,
        Vector2 anchorMin = default, Vector2 anchorMax = default)
    {
        GameObject go = FindOrCreate(name, parent);
        var rt = EnsureComponent<RectTransform>(go);

        if (anchorMin != default || anchorMax != default)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
        else
        {
            rt.anchorMin = anchorCenter;
            rt.anchorMax = anchorCenter;
            rt.sizeDelta = size == default ? new Vector2(100f, 100f) : size;
            rt.anchoredPosition = Vector2.zero;
        }

        EnsureComponent<Image>(go);
        return go;
    }

    static GameObject FindOrCreateTMPAnchored(string name, Transform parent, string text,
        Vector2 anchorCenter, float fontSize,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center,
        Vector2 size = default)
    {
        GameObject go = FindOrCreate(name, parent);
        var rt = EnsureComponent<RectTransform>(go);
        rt.anchorMin = anchorCenter;
        rt.anchorMax = anchorCenter;
        rt.sizeDelta = size == default ? new Vector2(800f, 100f) : size;
        rt.anchoredPosition = Vector2.zero;

        var tmp = EnsureComponent<TextMeshProUGUI>(go);
        // Only set text if it's the default/placeholder (preserve custom edits)
        if (string.IsNullOrEmpty(tmp.text) || tmp.text == "New Text")
            tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        return go;
    }

    static GameObject FindOrCreateLayoutTMP(string name, Transform parent, string text,
        float fontSize, float preferredHeight,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject go = FindOrCreate(name, parent);
        EnsureComponent<RectTransform>(go);

        var tmp = EnsureComponent<TextMeshProUGUI>(go);
        if (string.IsNullOrEmpty(tmp.text) || tmp.text == "New Text")
            tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        var le = EnsureComponent<LayoutElement>(go);
        if (preferredHeight > 0)
            le.preferredHeight = preferredHeight;
        else
            le.flexibleHeight = 0;

        return go;
    }

    static GameObject FindOrCreateLayoutImage(string name, Transform parent,
        Color fallbackColor, float preferredHeight)
    {
        GameObject go = FindOrCreate(name, parent);
        EnsureComponent<RectTransform>(go);

        var img = EnsureComponent<Image>(go);
        if (!HasSprite(go)) img.color = fallbackColor;

        var le = EnsureComponent<LayoutElement>(go);
        le.preferredHeight = preferredHeight;

        return go;
    }

    // ═══════════════════════════════════════════════════════════════
    // SPRITE HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Check if a GameObject's Image already has a sprite assigned</summary>
    static bool HasSprite(GameObject go)
    {
        var img = go.GetComponent<Image>();
        return img != null && img.sprite != null;
    }

    /// <summary>
    /// Try to load and assign a sprite from an asset path.
    /// If sprite not found, sets fallback color (only if no sprite already assigned).
    /// If sprite IS already assigned (manually), it is PRESERVED.
    /// </summary>
    static void TryAssignSprite(GameObject go, string assetPath, Color fallbackColor = default)
    {
        var img = go.GetComponent<Image>();
        if (img == null) return;

        // Try to load sprite from asset path
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white; // Reset tint when sprite assigned
            EditorUtility.SetDirty(go);
        }
        else if (!HasSprite(go) && fallbackColor != default)
        {
            // No sprite at path AND no existing sprite → use fallback color
            img.color = fallbackColor;
        }
        // If sprite already assigned manually → do nothing (preserved!)
    }

    /// <summary>Load all tip sprites from the tip folder</summary>
    static Sprite[] LoadTipSprites()
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { SpritePaths.TipImageFolder.TrimEnd('/') });
        if (guids.Length == 0) return new Sprite[0];

        Sprite[] sprites = new Sprite[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        // Sort by name for consistent ordering
        System.Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return sprites;
    }

    // ═══════════════════════════════════════════════════════════════
    // TEXT & STYLE HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Add or update LocalizedText component</summary>
    static void EnsureLocalizedText(GameObject go, string key)
    {
        var loc = go.GetComponent<LocalizedText>();
        if (loc == null) loc = go.AddComponent<LocalizedText>();
        SetPrivateField(loc, "localizationKey", key);
    }

    static void SetTMPStyle(GameObject go, float spacing,
        FontStyles fontStyle, string fontName = null)
    {
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp == null) return;
        tmp.characterSpacing = spacing;
        tmp.fontStyle = fontStyle;

        if (!string.IsNullOrEmpty(fontName))
        {
            var font = Resources.Load<TMP_FontAsset>(fontName);
            if (font == null)
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{fontName}.asset");
            if (font != null)
                tmp.font = font;
            else
                Debug.LogWarning($"[GOLFIN] Font '{fontName}' not found. See ARCHITECTURE.md for setup.");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════

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
