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
    // Values from Figma API + visual comparison (2026-02-26)
    // Canvas: 1170×2532
    //
    // Figma structure (from API):
    //   Screen (1170×2532)
    //     └─ Background (stretched)
    //     └─ BottomGradient (dark vignette at bottom for text legibility)
    //     └─ Game Screen Content (1170×2532)
    //         └─ Content Container (978×2208, x=96, y=24)
    //             └─ Pop-up (978×variable, y=680, radius=50, BACKGROUND_BLUR r=4)
    //                 └─ Mission Title (281×120, radius=[8,8,0,0])
    //                     └─ "PRO TIP" (Rubik:600@66, #eedc9a)
    //                 └─ Separator (978×0 line)
    //                 └─ Goals Container (952×variable)
    //                     └─ Tip text (856×variable, Rubik:600@51, #ffffff)
    //                 └─ Image (806×variable, tip illustrations)
    //                 └─ Goals Container (978×78)
    //                     └─ "TAP FOR NEXT TIP" (882×54, Rubik:600@39, #ffffff)
    //         └─ "NOW LOADING" (978×123, y=2281, Rubik:600@102, #ffffff)
    //         └─ Bar + Size (978×105, y=2428)
    //             └─ Bar (978×30, radius=8, fill=#ffffff)
    //                 └─ Fill (376×30, radius=8, stroke=#000000 w=1)
    //             └─ "52.20 / 267 MB" (978×63, Rubik:600@48, #ffffff)
    // ═══════════════════════════════════════════════════════════════
    static LoadingScreen SetupLoadingScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LoadingScreen", parent);
        var component = EnsureComponent<LoadingScreen>(screen);

        // ─── Background — full stretch ────────────────────────────
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.LoadingBackground, new Color(0.15f, 0.25f, 0.1f));

        // ─── Bottom Gradient Overlay ──────────────────────────────
        // Dark vignette at bottom for text legibility (missing in previous build)
        var bottomGrad = FindOrCreate("BottomGradient", screen.transform);
        var bgRT = EnsureComponent<RectTransform>(bottomGrad);
        bgRT.anchorMin = new Vector2(0f, 0f);
        bgRT.anchorMax = new Vector2(1f, 0.25f); // bottom 25% of screen
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        var bgImg = EnsureComponent<Image>(bottomGrad);
        // Solid dark overlay; for proper gradient use a gradient sprite
        bgImg.color = new Color(0f, 0f, 0f, 0.3f);

        // ─── Content Container (centered, 978px wide) ────────────
        // Figma: x=96 from left edge, 978px wide, 2208px tall, y=24 from top
        GameObject contentContainer = FindOrCreate("ContentContainer", screen.transform);
        var ccRT = EnsureComponent<RectTransform>(contentContainer);
        ccRT.anchorMin = new Vector2(ContentMargin / W, 0f);
        ccRT.anchorMax = new Vector2(1f - ContentMargin / W, 1f);
        ccRT.offsetMin = Vector2.zero;
        ccRT.offsetMax = Vector2.zero;

        // ─── Pro Tip Card (Pop-up) ────────────────────────────────
        // Figma: 978px wide (full content container width), radius=50
        //   BACKGROUND_BLUR effect (r=4)
        //   Top edge at Y=680 from canvas top
        //   Y relative to content container: 680 - 24 = 656
        GameObject tipCardGO = FindOrCreate("ProTipCard", contentContainer.transform);
        var tipCardRT = EnsureComponent<RectTransform>(tipCardGO);
        tipCardRT.anchorMin = new Vector2(0f, 0.5f);
        tipCardRT.anchorMax = new Vector2(1f, 0.5f);  // FULL WIDTH, vertically centered anchor
        tipCardRT.pivot = new Vector2(0.5f, 0.5f);    // center pivot — card grows from center
        // Figma: card center is roughly at Y=1100 from top → offset from screen center
        // Screen center = 2532/2 = 1266, content center ≈ 1100 → offset = +166 upward
        tipCardRT.anchoredPosition = new Vector2(0f, 166f);

        var tipCardBg = EnsureComponent<Image>(tipCardGO);
        TryAssignSprite(tipCardGO, SpritePaths.CardBackground,
            new Color(0.06f, 0.10f, 0.08f, 0.80f));  // slightly more opaque than before
        tipCardBg.type = Image.Type.Sliced;
        tipCardBg.pixelsPerUnitMultiplier = 1f;
        if (HasSprite(tipCardGO)) tipCardBg.color = Color.white;

        var layout = EnsureComponent<VerticalLayoutGroup>(tipCardGO);
        // Figma: content starts ~10px in from card edge, generous vertical padding
        layout.padding = new RectOffset(48, 48, 0, 24);  // 48px horizontal = (978-882)/2
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
        //   Text: "PRO TIP", Rubik:600@66, color=#eedc9a, 249×84
        //   MUST be single line — container needs enough width
        var headerContainer = FindOrCreate("HeaderContainer", tipCardGO.transform);
        EnsureComponent<RectTransform>(headerContainer);
        var hcLE = EnsureComponent<LayoutElement>(headerContainer);
        hcLE.preferredHeight = 120f;
        hcLE.minWidth = 300f; // ensure PRO TIP doesn't wrap

        var header = FindOrCreate("Header", headerContainer.transform);
        var headerRT = EnsureComponent<RectTransform>(header);
        // Stretch to fill the container so text doesn't wrap
        headerRT.anchorMin = Vector2.zero;
        headerRT.anchorMax = Vector2.one;
        headerRT.offsetMin = Vector2.zero;
        headerRT.offsetMax = Vector2.zero;
        var headerTMP = EnsureComponent<TextMeshProUGUI>(header);
        if (string.IsNullOrEmpty(headerTMP.text) || headerTMP.text == "New Text")
            headerTMP.text = "PRO TIP";
        headerTMP.fontSize = 66f;
        headerTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        headerTMP.color = GoldAccent;  // #eedc9a
        headerTMP.alignment = TextAlignmentOptions.Center;
        headerTMP.characterSpacing = 3f;  // slightly wider tracking
        headerTMP.textWrappingMode = TextWrappingModes.NoWrap;  // PREVENT wrapping to 2 lines
        TrySetFont(headerTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(header, "tip_header");

        // ─── Separator Line ───────────────────────────────────────
        // Figma: "Separator" LINE, full 978px width
        var divider = FindOrCreateLayoutImage("Divider", tipCardGO.transform,
            GoldAccent, 2f);

        // ─── Tip Text ─────────────────────────────────────────────
        // Figma: Rubik:600@51, color=#ffffff, CENTER-aligned
        //   Inside 856px wide frame (952 container, ~48px inset each side)
        //   Supports {gold}KEYWORD{/gold} highlighting via ProTipCard
        var tipText = FindOrCreateLayoutTMP("TipText", tipCardGO.transform,
            "Tip goes here...", 51f, -1f, TextAlignmentOptions.Center);
        var tipTMP = tipText.GetComponent<TextMeshProUGUI>();
        tipTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tipTMP.color = Color.white;
        tipTMP.alignment = TextAlignmentOptions.Center;  // was left-aligned, Figma is centered
        tipTMP.characterSpacing = 0f;
        TrySetFont(tipTMP, "Rubik-SemiBold SDF");
        var tipPadding = EnsureComponent<LayoutElement>(tipText);
        tipPadding.minHeight = 156f;  // ~3 lines at 51px with spacing
        tipPadding.flexibleHeight = 0;

        // ─── Tip Image Display (single Image, swapped per tip) ──────
        // IMPORTANT: This must be a SEPARATE child GameObject, not the card itself.
        // The card's own Image is the background. This is the tip illustration.

        // Clean up old stale image objects from previous builds
        foreach (string oldName in new[] { "TipImage", "TipImage_0", "TipImage_1", "TipImage_2",
            "TipImage_3", "TipImage_4", "TipImage_5", "TipImage_6", "TipImage_7" })
        {
            var old = tipCardGO.transform.Find(oldName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        var tipImageGO = FindOrCreate("TipImageDisplay", tipCardGO.transform);
        var tipImageRT = EnsureComponent<RectTransform>(tipImageGO);
        var tipImgComponent = EnsureComponent<Image>(tipImageGO);
        tipImgComponent.color = Color.white;
        tipImgComponent.preserveAspect = true;
        tipImgComponent.raycastTarget = false;
        // LayoutElement — ProTipCard.ShowTip() sets preferredWidth/Height from sprite native size
        var tipImgLE = EnsureComponent<LayoutElement>(tipImageGO);
        tipImageGO.SetActive(false); // hidden until a sprite is assigned at runtime

        // ─── "TAP FOR NEXT TIP" ───────────────────────────────────
        // Figma: Rubik:600@39, color=#ffffff, 882×54, RIGHT-aligned
        var tapNext = FindOrCreateLayoutTMP("TapNextText", tipCardGO.transform,
            "TAP FOR NEXT TIP", 39f, 78f, TextAlignmentOptions.Right);
        var tapTMP = tapNext.GetComponent<TextMeshProUGUI>();
        tapTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tapTMP.color = Color.white;
        tapTMP.characterSpacing = 1.5f;
        TrySetFont(tapTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(tapNext, "tip_next");

        // Wire ProTipCard fields
        SetPrivateField(tipCard, "headerText", headerTMP);
        SetPrivateField(tipCard, "tipText", tipTMP);
        SetPrivateField(tipCard, "tapNextText", tapTMP);
        SetPrivateField(tipCard, "dividerImage", divider.GetComponent<Image>());
        SetPrivateField(tipCard, "tipImageDisplay", tipImgComponent);

        // Force-write the correct tip keys (overrides stale serialized values)
        string[] correctTipKeys = new string[] {
            "tip_club_bag", "tip_forecast", "tip_rarities", "tip_swing",
            "tip_accuracy", "tip_leaderboard", "tip_timing", "tip_view_switch"
        };
        SetPrivateField(tipCard, "tipKeys", correctTipKeys);

        // Auto-load tip sprites from folder if available
        var loadedSprites = LoadTipSprites();
        if (loadedSprites.Length > 0)
            SetPrivateField(tipCard, "tipSprites", loadedSprites);

        // ─── "NOW LOADING" ────────────────────────────────────────
        // Figma: "Title" text, Rubik:600@102, color=#ffffff
        //   978×123, Y=2281 from canvas top
        //   anchorY = 1 - (2281/2532) = ~0.099
        var nowLoading = FindOrCreateTMPAnchored("NowLoadingText", screen.transform,
            "NOW LOADING",
            new Vector2(0.5f, 1f - (2281f / H)),
            102f,
            TextAlignmentOptions.Center,
            new Vector2(ContentWidth, 123f));
        var nlTMP = nowLoading.GetComponent<TextMeshProUGUI>();
        nlTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        nlTMP.color = Color.white;
        nlTMP.characterSpacing = 4f;  // wider tracking per visual comparison
        TrySetFont(nlTMP, "Rubik-SemiBold SDF");
        // Add subtle drop shadow via TMP outline
        nlTMP.outlineWidth = 0.08f;
        nlTMP.outlineColor = new Color32(0, 0, 0, 80);
        EnsureLocalizedText(nowLoading, "screen_loading");

        // ─── Loading Bar ──────────────────────────────────────────
        // Figma: "Bar" frame, 978×30, Y=2428, cornerRadius=8
        //   Background: fill=#ffffff (semi-transparent track)
        //   Fill: 376×30 (progress), radius=8, stroke=#000000 w=1
        var barBG = FindOrCreateImageAnchored("LoadingBarBG", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (2428f / H)),
            size: new Vector2(ContentWidth, 30f));
        var barBGImg = barBG.GetComponent<Image>();
        barBGImg.color = new Color(1f, 1f, 1f, 0.30f);  // Figma: white track, semi-transparent
        TryAssignSprite(barBG, SpritePaths.LoadingBarPill);
        if (HasSprite(barBG)) barBGImg.type = Image.Type.Sliced;
        var loadingBar = EnsureComponent<LoadingBar>(barBG);

        // Bar fill — starts at 0 (LoadingScreen.cs animates it)
        var barFill = FindOrCreateImageStretched("LoadingBarFill", barBG.transform);
        var barFillImg = barFill.GetComponent<Image>();
        barFillImg.color = Color.white;
        barFillImg.type = Image.Type.Filled;
        barFillImg.fillMethod = Image.FillMethod.Horizontal;
        barFillImg.fillAmount = 0f;  // FIX: start at 0 (was showing partial fill)
        TryAssignSprite(barFill, SpritePaths.LoadingBarPill);

        // Bar glow — follows fill edge
        var barGlow = FindOrCreateImageAnchored("LoadingBarGlow", barBG.transform,
            anchorCenter: new Vector2(0f, 0.5f),  // starts at left, LoadingBar.cs moves it
            size: new Vector2(30f, 30f));
        if (!HasSprite(barGlow)) barGlow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);

        SetPrivateField(loadingBar, "fillImage", barFillImg);
        SetPrivateField(loadingBar, "glowImage", barGlow.GetComponent<Image>());

        // ─── Download Progress Text ───────────────────────────────
        // Figma: "52.20 / 267 MB", Rubik:600@48, color=#ffffff
        //   978×63, Y=2470 from canvas top
        //   Figma shows full-width centered text
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

        // Force-write correct tip keys on LoadingScreen too
        string[] loadingTipKeys = new string[] {
            "tip_club_bag", "tip_forecast", "tip_rarities", "tip_swing",
            "tip_accuracy", "tip_leaderboard", "tip_timing", "tip_view_switch"
        };
        SetPrivateField(component, "tipKeys", loadingTipKeys);

        return component;
    }

    // ═══════════════════════════════════════════════════════════════
    // SPLASH SCREEN
    // Figma page: "Splash Screen" — background art + START/CREATE ACCOUNT
    // Canvas: 1170×2532
    //
    // Visual comparison (2026-02-26):
    //   Title block (logo+presents+crest+invitational): composite image, top area
    //   START button: green, centered, Y≈2030 from top
    //   CREATE ACCOUNT: text-only, centered, Y≈2168 from top
    //
    // Key fixes from comparison:
    //   - "presents" → "Presents" (capitalization — handled via localization)
    //   - START button: moved up ~20px, width reduced ~10px
    //   - CREATE ACCOUNT: moved up ~20px, font +2px, letter-spacing +1px
    // ═══════════════════════════════════════════════════════════════
    static SplashScreen SetupSplashScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("SplashScreen", parent);
        var component = EnsureComponent<SplashScreen>(screen);

        // Background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.SplashBackground, new Color(0.1f, 0.2f, 0.15f));

        // ─── Title Image ──────────────────────────────────────────
        // Composite image: GOLFIN logo + "Presents" + crest + "The Invitational"
        // Positioned in upper portion, nudged down ~10px vs previous
        var titleImage = FindOrCreateImageAnchored("TitleArea", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (78f / H)),  // top edge at Y≈78 from canvas top
            size: new Vector2(ContentWidth, 365f));
        titleImage.GetComponent<Image>().preserveAspect = true;
        TryAssignSprite(titleImage, SpritePaths.SplashTitle, Color.white);
        // Pivot from top so Y positions the top edge
        var titleRT = titleImage.GetComponent<RectTransform>();
        titleRT.pivot = new Vector2(0.5f, 1f);

        // ─── START Button ─────────────────────────────────────────
        // Visual comparison: button center at Y≈2065 from canvas top
        //   Size: ~330×72 (visual), but we use the sprite-based button
        //   Green gradient: #5EC02C → #3C8E14
        //   Corner radius: ~12px
        //   Drop shadow: 3px Y offset, 5px blur, dark green
        var startBtn = FindOrCreateImageAnchored("StartButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (2065f / H)),  // Y=2065 center (was 0.835=2114)
            size: new Vector2(330f, 72f));  // was 450×120, visual match is 330×72
        var startImg = startBtn.GetComponent<Image>();
        if (!HasSprite(startBtn)) startImg.color = GreenButton;
        EnsureComponent<Button>(startBtn);
        EnsureComponent<PressableButton>(startBtn);

        var startText = FindOrCreateTMPAnchored("Text", startBtn.transform, "START",
            new Vector2(0.5f, 0.5f), 46f,  // was 66, visual match is ~46
            TextAlignmentOptions.Center,
            new Vector2(300f, 60f));
        var stTMP = startText.GetComponent<TextMeshProUGUI>();
        stTMP.color = Color.white;
        stTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        stTMP.characterSpacing = 2f;  // was 0, Figma has ~2px tracking
        TrySetFont(stTMP, "Rubik-SemiBold SDF");
        // Text stroke for depth (dark green outline like reference)
        stTMP.outlineWidth = 0.15f;
        stTMP.outlineColor = new Color32(26, 58, 10, 180);  // ~#1A3A0A
        EnsureLocalizedText(startText, "btn_start");

        // ─── CREATE ACCOUNT Button ────────────────────────────────
        // Visual comparison: text center at Y≈2168 from canvas top
        //   Text-only, no bg, white with dark stroke
        var createBtn = FindOrCreateImageAnchored("CreateAccountButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (2168f / H)),  // Y=2168 (was 0.912=2309)
            size: new Vector2(680f, 100f));
        createBtn.GetComponent<Image>().color = Color.clear;
        EnsureComponent<Button>(createBtn);
        EnsureComponent<PressableButton>(createBtn);

        var createText = FindOrCreateTMPAnchored("Text", createBtn.transform, "CREATE ACCOUNT",
            new Vector2(0.5f, 0.5f), 50f,  // was 48, Figma is ~50
            TextAlignmentOptions.Center,
            new Vector2(600f, 80f));
        var ctTMP = createText.GetComponent<TextMeshProUGUI>();
        ctTMP.color = Color.white;
        ctTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        ctTMP.characterSpacing = 3f;  // was 0, Figma has ~3px tracking
        TrySetFont(ctTMP, "Rubik-SemiBold SDF");
        // Text stroke for readability over background
        ctTMP.outlineWidth = 0.12f;
        ctTMP.outlineColor = new Color32(26, 58, 10, 200);  // ~#1A3A0A
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
