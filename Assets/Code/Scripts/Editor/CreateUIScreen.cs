using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// Editor tool that creates OR updates the GOLFIN UI scene hierarchy.
/// 
/// ALL VALUES SOURCED FROM FIGMA "Golfin Game Redux" (2026-02-25)
/// via Figma REST API — no more guessing from screenshots.
///
/// Reference resolution: 1170×2532 (iPhone Pro Max @3x)
/// Primary font: Rubik (weights 400–800)
/// Accent color: #eedc9a (gold)
///
/// Usage: Unity menu → Tools → Create GOLFIN UI Scene
///        (safe to run multiple times — FindOrCreate preserves sprites)
/// </summary>
public class CreateUIScreen
{
    // ═══ FIGMA REFERENCE RESOLUTION ═══
    const float W = 1170f;
    const float H = 2532f;

    // ═══ FIGMA COLORS (exact hex from API) ═══
    static readonly Color GoldAccent      = HexColor("#eedc9a");   // PRO TIP header, dividers
    static readonly Color DarkGold        = HexColor("#321506");   // Primary button text
    static readonly Color GoldBorder      = HexColor("#ffe48b");   // Primary button inner stroke
    static readonly Color GoldOuterStroke = HexColor("#422100");   // Primary button outer stroke
    static readonly Color GoldStrokeShadow= HexColor("#ddaf42");   // Primary text stroke
    static readonly Color SlateText       = HexColor("#1e293b");   // Secondary button text
    static readonly Color SlateBorder     = HexColor("#334155");   // Secondary button outer stroke
    static readonly Color LightBorder     = HexColor("#f7f8f9");   // Secondary button inner stroke
    static readonly Color GrayStroke      = HexColor("#cfcfcf");   // Secondary text stroke
    static readonly Color GreenButton     = HexColor("#5fb610");   // START button (Splash)

    // ═══ FIGMA LAYOUT CONSTANTS (px from absoluteBoundingBox) ═══
    // All positions are relative to the 1170×2532 canvas.
    // Content Container: 978px wide, centered (96px margin each side)
    const float ContentWidth = 978f;
    const float ContentMargin = 96f;  // (1170-978)/2

    // ═══ SPRITE PATHS ═══
    static class SpritePaths
    {
        public const string Logo             = "Assets/Art/UI/golfin_logo.png";
        public const string SplashTitle      = "Assets/Art/UI/splash_title.png";
        public const string SplashBackground = "Assets/Art/UI/splash_bg.png";
        public const string LoadingBackground= "Assets/Art/UI/loading_bg.png";
        public const string LoadingBarPill   = "Assets/Art/UI/pill_bar.png";
        public const string CardBackground   = "Assets/Art/UI/card_bg.png";
        public const string TipImageFolder   = "Assets/Art/UI/Tips/";
    }

    // ═══════════════════════════════════════════════════════════════
    // MAIN ENTRY POINT
    // ═══════════════════════════════════════════════════════════════

    [MenuItem("Tools/Create GOLFIN UI Scene")]
    public static void CreateUI()
    {
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
    // Figma page: "Logo" — single component, black bg, centered logo
    // ═══════════════════════════════════════════════════════════════
    static LogoScreen SetupLogoScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LogoScreen", parent);
        var component = EnsureComponent<LogoScreen>(screen);

        // Full black background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        bg.GetComponent<Image>().color = Color.black;

        // Logo — centered horizontally and vertically
        // Figma: Logo Container is 930×168 in Components page
        var logo = FindOrCreateImageAnchored("Logo", screen.transform,
            anchorCenter: new Vector2(0.5f, 0.5f),
            size: new Vector2(930f, 168f));
        TryAssignSprite(logo, SpritePaths.Logo, Color.white);
        logo.GetComponent<Image>().preserveAspect = true;

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // LOADING SCREEN
    // Figma page: "Loading" — 9 variants, all share same layout structure
    // Values from "Loading Screen - Leaderboards" component (first variant)
    // Canvas: 1170×2532
    // ═══════════════════════════════════════════════════════════════
    static LoadingScreen SetupLoadingScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LoadingScreen", parent);
        var component = EnsureComponent<LoadingScreen>(screen);

        // Background — full stretch
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.LoadingBackground, new Color(0.15f, 0.25f, 0.1f));

        // ─── Content Container (centered, 978px wide) ────────────
        // Figma: "Content Container" at x+96 from screen edge, 978×2208
        GameObject contentContainer = FindOrCreate("ContentContainer", screen.transform);
        var ccRT = EnsureComponent<RectTransform>(contentContainer);
        // Anchored to center-top, 978 wide
        ccRT.anchorMin = new Vector2(ContentMargin / W, 0f);
        ccRT.anchorMax = new Vector2(1f - ContentMargin / W, 1f);
        ccRT.offsetMin = Vector2.zero;
        ccRT.offsetMax = Vector2.zero;

        // ─── Pro Tip Card ─────────────────────────────────────────
        // Figma: "Pop-up" frame, 978px wide, top at Y=680 (relative to screen top)
        //   cornerRadius=50, effect=BACKGROUND_BLUR(r=4)
        //   Variable height depending on content
        GameObject tipCardGO = FindOrCreate("ProTipCard", contentContainer.transform);
        var tipCardRT = EnsureComponent<RectTransform>(tipCardGO);
        tipCardRT.anchorMin = new Vector2(0f, 1f);
        tipCardRT.anchorMax = new Vector2(1f, 1f);
        tipCardRT.pivot = new Vector2(0.5f, 1f);
        // Figma: Pop-up top edge at Y=680 from canvas top, content starts at Y=49
        // Relative to content container top: 680 - 49 = 631
        tipCardRT.anchoredPosition = new Vector2(0f, -631f);

        var tipCardBg = EnsureComponent<Image>(tipCardGO);
        TryAssignSprite(tipCardGO, SpritePaths.CardBackground,
            new Color(0.08f, 0.12f, 0.10f, 0.75f));
        tipCardBg.type = Image.Type.Sliced;
        tipCardBg.pixelsPerUnitMultiplier = 1f;
        if (HasSprite(tipCardGO)) tipCardBg.color = Color.white;

        var layout = EnsureComponent<VerticalLayoutGroup>(tipCardGO);
        layout.padding = new RectOffset(10, 10, 0, 24);
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = EnsureComponent<ContentSizeFitter>(tipCardGO);
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var tipCard = EnsureComponent<ProTipCard>(tipCardGO);

        // ─── "PRO TIP" Header ─────────────────────────────────────
        // Figma: "Mission Title" frame 281×120, cornerRadius=[8,8,0,0]
        //   Text "PRO TIP": Rubik:600@66, color=#eedc9a, 249×84
        //   Right-aligned within the card (not centered)
        var headerContainer = FindOrCreate("HeaderContainer", tipCardGO.transform);
        var hcRT = EnsureComponent<RectTransform>(headerContainer);
        var hcLE = EnsureComponent<LayoutElement>(headerContainer);
        hcLE.preferredHeight = 120f;

        var header = FindOrCreate("Header", headerContainer.transform);
        EnsureComponent<RectTransform>(header);
        var headerTMP = EnsureComponent<TextMeshProUGUI>(header);
        if (string.IsNullOrEmpty(headerTMP.text) || headerTMP.text == "New Text")
            headerTMP.text = "PRO TIP";
        headerTMP.fontSize = 66f;
        headerTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        headerTMP.color = GoldAccent;
        headerTMP.alignment = TextAlignmentOptions.Center;
        headerTMP.characterSpacing = 0f;
        TrySetFont(headerTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(header, "tip_header");

        // ─── Separator Line ───────────────────────────────────────
        // Figma: "Separator" LINE, full 978px width at Y=824
        var divider = FindOrCreateLayoutImage("Divider", tipCardGO.transform,
            GoldAccent, 2f);

        // ─── Tip Text ─────────────────────────────────────────────
        // Figma: Rubik:600@51, color=#ffffff, inside 856px wide frame
        //   with 48px padding each side from card edge (952 container, 48px inset)
        var tipText = FindOrCreateLayoutTMP("TipText", tipCardGO.transform,
            "Tip goes here...", 51f, -1f);
        var tipTMP = tipText.GetComponent<TextMeshProUGUI>();
        tipTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tipTMP.color = Color.white;
        tipTMP.characterSpacing = 0f;
        TrySetFont(tipTMP, "Rubik-SemiBold SDF");
        var tipLE = tipText.GetComponent<LayoutElement>();
        if (tipLE == null) tipLE = tipText.AddComponent<LayoutElement>();
        tipLE.flexibleHeight = 0;
        // Add padding via layout element
        var tipPadding = EnsureComponent<LayoutElement>(tipText);
        tipPadding.minHeight = 132f; // at least 2 lines at 51px

        // ─── Tip Image (optional) ─────────────────────────────────
        var tipImageGO = FindOrCreateLayoutImage("TipImage", tipCardGO.transform,
            Color.clear, 456f);
        TryAssignSprite(tipImageGO, SpritePaths.TipImageFolder + "tip_0.png");

        // ─── "TAP FOR NEXT TIP" ───────────────────────────────────
        // Figma: Rubik:600@39, color=#ffffff, 882px wide, right-aligned
        //   Inside 978×78 "Goals Container" at bottom of card
        var tapNext = FindOrCreateLayoutTMP("TapNextText", tipCardGO.transform,
            "TAP FOR NEXT TIP", 39f, 78f, TextAlignmentOptions.Right);
        var tapTMP = tapNext.GetComponent<TextMeshProUGUI>();
        tapTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tapTMP.color = Color.white;
        TrySetFont(tapTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(tapNext, "tip_next");

        // Wire ProTipCard fields
        SetPrivateField(tipCard, "headerText", headerTMP);
        SetPrivateField(tipCard, "tipText", tipTMP);
        SetPrivateField(tipCard, "tapNextText", tapTMP);
        SetPrivateField(tipCard, "dividerImage", divider.GetComponent<Image>());

        // Auto-load tip images
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
                tipImgObj.SetActive(i == 0);
            }
            SetPrivateField(tipCard, "tipImages", tipImgArray);
            var placeholder = tipCardGO.transform.Find("TipImage");
            if (placeholder != null) Object.DestroyImmediate(placeholder.gameObject);
        }
        else
        {
            SetPrivateField(tipCard, "tipImages", new Image[] { tipImageGO.GetComponent<Image>() });
        }

        // ─── "NOW LOADING" ────────────────────────────────────────
        // Figma: "Title" text, Rubik:600@102, color=#ffffff
        //   Position: 978×123, Y=2281 from canvas top
        //   Relative to screen: anchorY = 1 - (2281/2532) = 0.099
        var nowLoading = FindOrCreateTMPAnchored("NowLoadingText", screen.transform,
            "NOW LOADING",
            new Vector2(0.5f, 1f - (2281f / H)),  // Y=2281 from top
            102f,
            TextAlignmentOptions.Center,
            new Vector2(ContentWidth, 123f));
        var nlTMP = nowLoading.GetComponent<TextMeshProUGUI>();
        nlTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        nlTMP.color = Color.white;
        TrySetFont(nlTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(nowLoading, "screen_loading");

        // ─── Loading Bar ──────────────────────────────────────────
        // Figma: "Bar" frame, 978×30, Y=2428, cornerRadius=8
        //   Background: fill=#ffffff
        //   Fill rectangle: 376×30 (progress), radius=8, stroke=#000000 w=1
        var barBG = FindOrCreateImageAnchored("LoadingBarBG", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (2428f / H)),
            size: new Vector2(ContentWidth, 30f));
        var barBGImg = barBG.GetComponent<Image>();
        barBGImg.color = new Color(1f, 1f, 1f, 0.25f);
        TryAssignSprite(barBG, SpritePaths.LoadingBarPill);
        if (HasSprite(barBG)) barBGImg.type = Image.Type.Sliced;
        var loadingBar = EnsureComponent<LoadingBar>(barBG);

        // Bar fill
        var barFill = FindOrCreateImageStretched("LoadingBarFill", barBG.transform);
        var barFillImg = barFill.GetComponent<Image>();
        barFillImg.color = Color.white; // Figma fill is white
        barFillImg.type = Image.Type.Filled;
        barFillImg.fillMethod = Image.FillMethod.Horizontal;
        TryAssignSprite(barFill, SpritePaths.LoadingBarPill);

        // Bar glow
        var barGlow = FindOrCreateImageAnchored("LoadingBarGlow", barBG.transform,
            anchorCenter: new Vector2(1f, 0.5f),
            size: new Vector2(30f, 30f));
        if (!HasSprite(barGlow)) barGlow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);

        SetPrivateField(loadingBar, "fillImage", barFillImg);
        SetPrivateField(loadingBar, "glowImage", barGlow.GetComponent<Image>());

        // ─── Download Progress Text ───────────────────────────────
        // Figma: "52.20 / 267 MB", Rubik:600@48, color=#ffffff
        //   Position: 978×63, Y=2470
        var downloadProgress = FindOrCreateTMPAnchored("DownloadProgress", screen.transform,
            "0 / 267 MB",
            new Vector2(0.5f, 1f - (2470f / H)),
            48f,
            TextAlignmentOptions.Center,
            new Vector2(ContentWidth, 63f));
        var dpTMP = downloadProgress.GetComponent<TextMeshProUGUI>();
        dpTMP.fontStyle = FontStyles.Bold;
        dpTMP.color = Color.white;
        TrySetFont(dpTMP, "Rubik-SemiBold SDF");

        // Wire LoadingScreen
        SetPrivateField(component, "loadingBar", loadingBar);
        SetPrivateField(component, "proTipCard", tipCard);
        SetPrivateField(component, "nowLoadingText", nlTMP);
        SetPrivateField(component, "downloadProgressText", dpTMP);

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // SPLASH SCREEN
    // Figma page: "Splash Screen" — background art + START/CREATE ACCOUNT
    // Figma component: 1170×2532
    // Buttons use Main Buttons component (450×120, radius=20)
    // ═══════════════════════════════════════════════════════════════
    static SplashScreen SetupSplashScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("SplashScreen", parent);
        var component = EnsureComponent<SplashScreen>(screen);

        // Background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.SplashBackground, new Color(0.1f, 0.2f, 0.15f));

        // ─── Title Image ──────────────────────────────────────────
        // Figma: Title area in upper portion
        var titleImage = FindOrCreateImageAnchored("TitleArea", screen.transform,
            anchorCenter: new Vector2(0.5f, 0.87f),  // ~top 13%
            size: new Vector2(ContentWidth, 365f));
        titleImage.GetComponent<Image>().preserveAspect = true;
        TryAssignSprite(titleImage, SpritePaths.SplashTitle, Color.white);

        // ─── START Button ─────────────────────────────────────────
        // Figma: Main Buttons gold variant, 450×120, radius=20
        //   Text: Rubik:600@66, color=#321506 (dark brown)
        //   Inner stroke: #ffe48b w=2
        //   Outer stroke: #422100 w=1
        //   Drop shadow on container
        var startBtn = FindOrCreateImageAnchored("StartButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (0.835f)),
            size: new Vector2(450f, 120f));
        var startImg = startBtn.GetComponent<Image>();
        if (!HasSprite(startBtn)) startImg.color = GreenButton;
        EnsureComponent<Button>(startBtn);
        EnsureComponent<PressableButton>(startBtn);

        var startText = FindOrCreateTMPAnchored("Text", startBtn.transform, "START",
            new Vector2(0.5f, 0.5f), 66f);
        var stTMP = startText.GetComponent<TextMeshProUGUI>();
        stTMP.color = Color.white;
        stTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        stTMP.characterSpacing = 0f;
        TrySetFont(stTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(startText, "btn_start");

        // ─── CREATE ACCOUNT Button ────────────────────────────────
        // Figma: Secondary/text-only style, transparent bg
        var createBtn = FindOrCreateImageAnchored("CreateAccountButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - 0.912f),
            size: new Vector2(680f, 100f));
        createBtn.GetComponent<Image>().color = Color.clear;
        EnsureComponent<Button>(createBtn);
        EnsureComponent<PressableButton>(createBtn);

        var createText = FindOrCreateTMPAnchored("Text", createBtn.transform, "CREATE ACCOUNT",
            new Vector2(0.5f, 0.5f), 48f);
        var ctTMP = createText.GetComponent<TextMeshProUGUI>();
        ctTMP.color = Color.white;
        ctTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        ctTMP.characterSpacing = 0f;
        TrySetFont(ctTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(createText, "btn_create_account");

        // Wire SplashScreen
        SetPrivateField(component, "startButton", startBtn.GetComponent<PressableButton>());
        SetPrivateField(component, "createAccountButton", createBtn.GetComponent<PressableButton>());

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // FIND-OR-CREATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    static GameObject FindOrCreate(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

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
        Vector2 anchorCenter = default, Vector2 size = default)
    {
        GameObject go = FindOrCreate(name, parent);
        var rt = EnsureComponent<RectTransform>(go);
        rt.anchorMin = anchorCenter;
        rt.anchorMax = anchorCenter;
        rt.sizeDelta = size == default ? new Vector2(100f, 100f) : size;
        rt.anchoredPosition = Vector2.zero;
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

    static bool HasSprite(GameObject go)
    {
        var img = go.GetComponent<Image>();
        return img != null && img.sprite != null;
    }

    static void TryAssignSprite(GameObject go, string assetPath, Color fallbackColor = default)
    {
        var img = go.GetComponent<Image>();
        if (img == null) return;

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null)
        {
            img.sprite = sprite;
            img.color = Color.white;
            EditorUtility.SetDirty(go);
        }
        else if (!HasSprite(go) && fallbackColor != default)
        {
            img.color = fallbackColor;
        }
    }

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
        System.Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return sprites;
    }

    // ═══════════════════════════════════════════════════════════════
    // FONT HELPER
    // ═══════════════════════════════════════════════════════════════

    static void TrySetFont(TextMeshProUGUI tmp, string fontName)
    {
        if (string.IsNullOrEmpty(fontName)) return;
        var font = Resources.Load<TMP_FontAsset>(fontName);
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"Assets/Fonts/{fontName}.asset");
        if (font != null)
            tmp.font = font;
    }

    static void EnsureLocalizedText(GameObject go, string key)
    {
        var loc = go.GetComponent<LocalizedText>();
        if (loc == null) loc = go.AddComponent<LocalizedText>();
        SetPrivateField(loc, "localizationKey", key);
    }

    // ═══════════════════════════════════════════════════════════════
    // UTILITY
    // ═══════════════════════════════════════════════════════════════

    static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
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
