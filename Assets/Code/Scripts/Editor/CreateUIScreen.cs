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
        // Home screen
        public const string HomeBackground     = "Assets/Art/UI/Backgrounds/Home.png";
        public const string AnnouncementCard   = "Assets/Art/UI/Home/announcement_card.png";
        public const string UsernameBanner     = "Assets/Art/UI/Home/username_banner.png";
        public const string GpsBanner          = "Assets/Art/UI/Home/gps_banner.png";
        public const string NextHolePanel      = "Assets/Art/UI/Home/next_hole_panel.png";
        public const string BottomNavBg        = "Assets/Art/UI/Home/bottom_nav_bg.png";
        public const string PlayButton         = "Assets/Art/UI/Home/play_button.png";
        public const string SettingsGear       = "Assets/Art/UI/Icons/settings_gear.png";
        public const string CoinGold           = "Assets/Art/UI/Icons/coin_gold.png";
        public const string GpsPin             = "Assets/Art/UI/Icons/gps_pin.png";
        public const string CharacterPlaceholder = "Assets/Art/UI/Characters/character_placeholder.png";
        public const string RewardCoin         = "Assets/Art/UI/Icons/reward_coin.png";
        public const string RewardItem         = "Assets/Art/UI/Icons/reward_item.png";
        public const string RewardBall         = "Assets/Art/UI/Icons/reward_ball.png";
        public const string NavHome            = "Assets/Art/UI/Icons/nav_home.png";
        public const string NavShop            = "Assets/Art/UI/Icons/nav_shop.png";
        public const string NavPlay            = "Assets/Art/UI/Icons/nav_play.png";
        public const string NavBag             = "Assets/Art/UI/Icons/nav_bag.png";
        public const string NavMore            = "Assets/Art/UI/Icons/nav_more.png";
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
        var homeScreen = SetupHomeScreen(canvasGO.transform);

        // ─── Wire GameBootstrap ──────────────────────────────────
        SetPrivateField(bootstrap, "logoScreen", logoScreen);
        SetPrivateField(bootstrap, "loadingScreen", loadingScreen);
        SetPrivateField(bootstrap, "splashScreen", splashScreen);
        SetPrivateField(bootstrap, "homeScreen", homeScreen);

        // ─── EventSystem ─────────────────────────────────────────
        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            GameObject es = FindOrCreate("EventSystem", root.transform);
            EnsureComponent<UnityEngine.EventSystems.EventSystem>(es);
            EnsureComponent<UnityEngine.EventSystems.StandaloneInputModule>(es);
        }

        // ─── Apply overrides from screen_values.json ─────────────
        ApplySceneValueOverrides(canvasGO.transform);

        Selection.activeGameObject = root;
        string mode = isUpdate ? "Updated" : "Created";
        Debug.Log($"[GOLFIN] {mode} UI scene successfully! ✅");
    }

    /// <summary>
    /// Reads Assets/Code/Data/screen_values.json (exported via Tools → QA → Export Scene Values)
    /// and applies saved values back to matching components.
    /// This preserves Cesar's manual Inspector tweaks across rebuilds.
    /// </summary>
    static void ApplySceneValueOverrides(Transform canvasRoot)
    {
        string path = "Assets/Code/Data/screen_values.json";
        if (!System.IO.File.Exists(path))
        {
            Debug.Log("[GOLFIN] No screen_values.json found — using code defaults.");
            return;
        }

        string json = System.IO.File.ReadAllText(path);
        int applied = 0;

        // Parse each "key": value line
        foreach (Transform screenT in canvasRoot)
        {
            ApplyOverridesToNode(screenT, screenT.name, json, ref applied);
        }

        if (applied > 0)
            Debug.Log($"[GOLFIN] Applied {applied} override(s) from screen_values.json ✅");
    }

    static void ApplyOverridesToNode(Transform t, string nodePath, string json, ref int applied)
    {
        // Try to find and apply overrides for this node
        var rt = t.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (TryGetJsonVector2(json, nodePath, "anchoredPosition", out var pos))
                { rt.anchoredPosition = pos; applied++; }
            if (TryGetJsonVector2(json, nodePath, "sizeDelta", out var size))
                { rt.sizeDelta = size; applied++; }
            if (TryGetJsonVector2(json, nodePath, "anchorMin", out var amin))
                { rt.anchorMin = amin; applied++; }
            if (TryGetJsonVector2(json, nodePath, "anchorMax", out var amax))
                { rt.anchorMax = amax; applied++; }
            if (TryGetJsonVector2(json, nodePath, "pivot", out var piv))
                { rt.pivot = piv; applied++; }
        }

        var tmp = t.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            if (TryGetJsonFloat(json, nodePath, "fontSize", out float fs))
                { tmp.fontSize = fs; applied++; }
            if (TryGetJsonFloat(json, nodePath, "characterSpacing", out float cs))
                { tmp.characterSpacing = cs; applied++; }
            if (TryGetJsonFloat(json, nodePath, "lineSpacing", out float ls))
                { tmp.lineSpacing = ls; applied++; }
            if (TryGetJsonFloat(json, nodePath, "outlineWidth", out float ow))
                { tmp.outlineWidth = ow; applied++; }
            if (TryGetJsonColor(json, nodePath, "color", out var col))
                { tmp.color = col; applied++; }
            if (TryGetJsonColor(json, nodePath, "outlineColor", out var ocol))
                { tmp.outlineColor = ocol; applied++; }
            if (TryGetJsonString(json, nodePath, "font", out string fontName))
                { TrySetFont(tmp, fontName); applied++; }
            if (TryGetJsonString(json, nodePath, "fontStyle", out string styleStr))
            {
                if (System.Enum.TryParse<FontStyles>(styleStr, out var style))
                    { tmp.fontStyle = style; applied++; }
            }
            if (TryGetJsonString(json, nodePath, "alignment", out string alignStr))
            {
                if (System.Enum.TryParse<TextAlignmentOptions>(alignStr, out var align))
                    { tmp.alignment = align; applied++; }
            }
            if (TryGetJsonString(json, nodePath, "textWrappingMode", out string wrapStr))
            {
                if (System.Enum.TryParse<TextWrappingModes>(wrapStr, out var wrap))
                    { tmp.textWrappingMode = wrap; applied++; }
            }
            if (TryGetJsonFloat(json, nodePath, "wordSpacing", out float ws))
                { tmp.wordSpacing = ws; applied++; }
            if (TryGetJsonFloat(json, nodePath, "paragraphSpacing", out float ps))
                { tmp.paragraphSpacing = ps; applied++; }
        }

        var img = t.GetComponent<Image>();
        if (img != null)
        {
            if (TryGetJsonColor(json, nodePath, "imageColor", out var ic))
                { img.color = ic; applied++; }
        }

        // Recurse
        foreach (Transform child in t)
        {
            string childPath = $"{nodePath}/{child.name}";
            ApplyOverridesToNode(child, childPath, json, ref applied);
        }
    }

    // ═══ JSON PARSING HELPERS (simple regex, no dependency) ═══

    static bool TryGetJsonVector2(string json, string nodePath, string prop, out Vector2 result)
    {
        result = Vector2.zero;
        string key = $"\"{nodePath}.{prop}\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return false;
        int bracketStart = json.IndexOf('[', idx);
        int bracketEnd = json.IndexOf(']', bracketStart);
        if (bracketStart < 0 || bracketEnd < 0) return false;
        string inner = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
        string[] parts = inner.Split(',');
        if (parts.Length != 2) return false;
        if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float x) &&
            float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float y))
        {
            result = new Vector2(x, y);
            return true;
        }
        return false;
    }

    static bool TryGetJsonFloat(string json, string nodePath, string prop, out float result)
    {
        result = 0f;
        string key = $"\"{nodePath}.{prop}\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return false;
        int colonIdx = json.IndexOf(':', idx + key.Length);
        if (colonIdx < 0) return false;
        int endIdx = json.IndexOfAny(new[] { ',', '\n', '}' }, colonIdx + 1);
        if (endIdx < 0) endIdx = json.Length;
        string val = json.Substring(colonIdx + 1, endIdx - colonIdx - 1).Trim().Trim('"');
        return float.TryParse(val, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    static bool TryGetJsonString(string json, string nodePath, string prop, out string result)
    {
        result = null;
        string key = $"\"{nodePath}.{prop}\"";
        int idx = json.IndexOf(key);
        if (idx < 0) return false;
        int firstQuote = json.IndexOf('"', json.IndexOf(':', idx + key.Length) + 1);
        if (firstQuote < 0) return false;
        int secondQuote = json.IndexOf('"', firstQuote + 1);
        if (secondQuote < 0) return false;
        result = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        return true;
    }

    static bool TryGetJsonColor(string json, string nodePath, string prop, out Color result)
    {
        result = Color.white;
        if (!TryGetJsonString(json, nodePath, prop, out string hex)) return false;
        return ColorUtility.TryParseHtmlString(hex, out result);
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

        // Ensure image is before tap-next in sibling order (VerticalLayoutGroup)
        tipImageGO.transform.SetAsLastSibling();

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
        tapNext.transform.SetAsLastSibling(); // ensure it's BELOW the tip image

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
        barBGImg.color = Color.white;  // Figma: solid white track
        TryAssignSprite(barBG, SpritePaths.LoadingBarPill);
        if (HasSprite(barBG)) barBGImg.type = Image.Type.Sliced;
        EditorUtility.SetDirty(barBGImg);
        var loadingBar = EnsureComponent<LoadingBar>(barBG);

        // Bar fill — blue rect that grows from left via RectTransform anchors
        // NOT using Image.fillAmount (doesn't work without sprites)
        var barFill = FindOrCreate("LoadingBarFill", barBG.transform);
        var barFillRT = EnsureComponent<RectTransform>(barFill);
        // Start at 0 width: anchored to left, zero width
        barFillRT.anchorMin = new Vector2(0f, 0f);
        barFillRT.anchorMax = new Vector2(0f, 1f);  // 0 width = empty bar
        barFillRT.offsetMin = Vector2.zero;
        barFillRT.offsetMax = Vector2.zero;
        var barFillImg = EnsureComponent<Image>(barFill);
        barFillImg.color = new Color(0.13f, 0.50f, 0.88f, 1f);  // blue #2080E0
        TryAssignSprite(barFill, SpritePaths.LoadingBarPill);
        if (HasSprite(barFill)) barFillImg.type = Image.Type.Sliced;
        EditorUtility.SetDirty(barFillImg);
        EditorUtility.SetDirty(barFillRT);

        // Wire LoadingBar — uses RectTransform scaling, not fillAmount
        SetPrivateField(loadingBar, "fillRect", barFillRT);
        SetPrivateField(loadingBar, "fillImage", barFillImg);
        SetPrivateField(loadingBar, "fillColor", new Color(0.13f, 0.50f, 0.88f));

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
    // HOME SCREEN
    // ═══════════════════════════════════════════════════════════════
    //
    // Reference: home_screen_ref.png (1170×2532)
    // Layout (top to bottom):
    //   Y=0:     Background (full bleed dark navy + golf course scene)
    //   Y=115:   Top bar — currency (left), settings gear (right)
    //   Y=155:   Username banner (centered)
    //   Y=240:   Announcement card (centered, paginated)
    //   Y=540:   Page dots
    //   Y=420-1650: Central character (random, NOT part of bg)
    //   Y=1560:  GOLFIN GPS banner
    //   Y=1810:  Next Hole panel
    //   Y=2332:  Bottom navigation bar (5 tabs)
    // ═══════════════════════════════════════════════════════════════
    static HomeScreen SetupHomeScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("HomeScreen", parent);
        var component = EnsureComponent<HomeScreen>(screen);

        // ─── Background ───────────────────────────────────────────
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.HomeBackground, HexColor("#0A1628"));

        // ─── Top Dark Gradient ────────────────────────────────────
        var topGrad = FindOrCreate("TopGradient", screen.transform);
        var topGradRT = EnsureComponent<RectTransform>(topGrad);
        topGradRT.anchorMin = new Vector2(0f, 1f - (350f / H));
        topGradRT.anchorMax = new Vector2(1f, 1f);
        topGradRT.offsetMin = Vector2.zero;
        topGradRT.offsetMax = Vector2.zero;
        var topGradImg = EnsureComponent<Image>(topGrad);
        topGradImg.color = new Color(0.04f, 0.08f, 0.16f, 0.7f);
        topGradImg.raycastTarget = false;

        // ─── Bottom Dark Gradient ─────────────────────────────────
        var bottomGrad = FindOrCreate("BottomGradient", screen.transform);
        var bottomGradRT = EnsureComponent<RectTransform>(bottomGrad);
        bottomGradRT.anchorMin = new Vector2(0f, 0f);
        bottomGradRT.anchorMax = new Vector2(1f, 1f - (1800f / H));
        bottomGradRT.offsetMin = Vector2.zero;
        bottomGradRT.offsetMax = Vector2.zero;
        var bottomGradImg = EnsureComponent<Image>(bottomGrad);
        bottomGradImg.color = new Color(0.04f, 0.10f, 0.18f, 0.9f);
        bottomGradImg.raycastTarget = false;

        // ─── Currency Display (Top-Left) ──────────────────────────
        // Figma: Y=115, coin icon 62×62 + text "50000"
        var currencyGroup = FindOrCreate("CurrencyGroup", screen.transform);
        var cgRT = EnsureComponent<RectTransform>(currencyGroup);
        cgRT.anchorMin = new Vector2(0f, 1f);
        cgRT.anchorMax = new Vector2(0f, 1f);
        cgRT.pivot = new Vector2(0f, 1f);
        cgRT.anchoredPosition = new Vector2(30f, -115f);
        cgRT.sizeDelta = new Vector2(230f, 65f);

        var hlg = EnsureComponent<HorizontalLayoutGroup>(currencyGroup);
        hlg.spacing = 8f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var coinIcon = FindOrCreate("CoinIcon", currencyGroup.transform);
        var coinRT = EnsureComponent<RectTransform>(coinIcon);
        coinRT.sizeDelta = new Vector2(62f, 62f);
        var coinImg = EnsureComponent<Image>(coinIcon);
        TryAssignSprite(coinIcon, SpritePaths.CoinGold, HexColor("#D4A017"));
        SetPrivateField(component, "currencyIcon", coinImg);

        var currencyText = FindOrCreate("CurrencyText", currencyGroup.transform);
        var currTextRT = EnsureComponent<RectTransform>(currencyText);
        currTextRT.sizeDelta = new Vector2(150f, 50f);
        var currTMP = EnsureComponent<TextMeshProUGUI>(currencyText);
        currTMP.text = "50,000";
        currTMP.fontSize = 36f;
        currTMP.fontStyle = FontStyles.Bold;
        currTMP.color = Color.white;
        currTMP.alignment = TextAlignmentOptions.MidlineLeft;
        TrySetFont(currTMP, "Rubik-SemiBold SDF");
        SetPrivateField(component, "currencyText", currTMP);

        // ─── Settings Gear (Top-Right) ────────────────────────────
        var settingsBtn = FindOrCreateImageAnchored("SettingsButton", screen.transform,
            anchorCenter: new Vector2(1f - (60f / W), 1f - (144f / H)),
            size: new Vector2(72f, 72f));
        TryAssignSprite(settingsBtn, SpritePaths.SettingsGear, HexColor("#C0C8D4"));
        EnsureComponent<Button>(settingsBtn);

        // ─── Username Banner (Center-Top) ─────────────────────────
        // Figma: Y=155, 660×55, centered, trapezoidal gold-bordered
        var userBanner = FindOrCreateImageAnchored("UsernameBanner", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (182f / H)),
            size: new Vector2(660f, 55f));
        TryAssignSprite(userBanner, SpritePaths.UsernameBanner,
            new Color(0.06f, 0.11f, 0.21f, 0.7f));
        var userBannerImg = userBanner.GetComponent<Image>();
        userBannerImg.type = Image.Type.Sliced;

        var usernameText = FindOrCreateTMPAnchored("UsernameText", userBanner.transform,
            "USERNAME",
            new Vector2(0.5f, 0.5f), 38f,
            TextAlignmentOptions.Center,
            new Vector2(600f, 45f));
        var userTMP = usernameText.GetComponent<TextMeshProUGUI>();
        userTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        userTMP.color = Color.white;
        userTMP.characterSpacing = 2f;
        TrySetFont(userTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(usernameText, "home_username");
        SetPrivateField(component, "usernameText", userTMP);

        // ─── Announcement Card ────────────────────────────────────
        // Figma: Y=240, 1010×290, light gray, speech bubble
        var annoCard = FindOrCreateImageAnchored("AnnouncementCard", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (385f / H)),
            size: new Vector2(1010f, 290f));
        TryAssignSprite(annoCard, SpritePaths.AnnouncementCard,
            new Color(0.85f, 0.87f, 0.90f, 0.9f));
        var annoCardImg = annoCard.GetComponent<Image>();
        annoCardImg.type = Image.Type.Sliced;

        var annoTitle = FindOrCreateTMPAnchored("AnnouncementTitle", annoCard.transform,
            "MAINTENANCE NOTICE",
            new Vector2(0.5f, 1f - (28f / 290f)), 40f,
            TextAlignmentOptions.Center,
            new Vector2(900f, 48f));
        var annoTitleTMP = annoTitle.GetComponent<TextMeshProUGUI>();
        annoTitleTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        annoTitleTMP.color = HexColor("#2C3E5A");
        TrySetFont(annoTitleTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(annoTitle, "home_maintenance_title");
        SetPrivateField(component, "announcementTitle", annoTitleTMP);

        var annoBody = FindOrCreateTMPAnchored("AnnouncementBody", annoCard.transform,
            "Scheduled server maintenance: 2025/12/31\nThe game will not be available for a short time\nduring maintenance.",
            new Vector2(0.5f, 0.35f), 32f,
            TextAlignmentOptions.Center,
            new Vector2(900f, 150f));
        var annoBodyTMP = annoBody.GetComponent<TextMeshProUGUI>();
        annoBodyTMP.fontStyle = FontStyles.Normal;
        annoBodyTMP.color = HexColor("#2C3E5A");
        TrySetFont(annoBodyTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(annoBody, "home_maintenance_body");
        SetPrivateField(component, "announcementBody", annoBodyTMP);

        // ─── Page Indicator Dots ──────────────────────────────────
        // Figma: Y=540, 3 dots, 16px each, 12px spacing
        var dotContainer = FindOrCreate("DotContainer", screen.transform);
        var dotContRT = EnsureComponent<RectTransform>(dotContainer);
        dotContRT.anchorMin = new Vector2(0.5f, 1f - (540f / H));
        dotContRT.anchorMax = new Vector2(0.5f, 1f - (540f / H));
        dotContRT.pivot = new Vector2(0.5f, 0.5f);
        dotContRT.sizeDelta = new Vector2(80f, 16f);
        dotContRT.anchoredPosition = Vector2.zero;

        var dotHLG = EnsureComponent<HorizontalLayoutGroup>(dotContainer);
        dotHLG.spacing = 12f;
        dotHLG.childAlignment = TextAnchor.MiddleCenter;
        dotHLG.childControlWidth = false;
        dotHLG.childControlHeight = false;

        for (int i = 0; i < 3; i++)
        {
            var dot = FindOrCreate($"Dot_{i}", dotContainer.transform);
            var dotRT = EnsureComponent<RectTransform>(dot);
            dotRT.sizeDelta = new Vector2(16f, 16f);
            var dotImg = EnsureComponent<Image>(dot);
            dotImg.color = i == 0
                ? new Color(1f, 1f, 1f, 1f)
                : new Color(1f, 1f, 1f, 0.4f);
        }
        SetPrivateField(component, "dotContainer", dotContainer.transform);

        // ─── Central Character ────────────────────────────────────
        // Figma: Y=420 to Y=1650, ~900×1230, centered
        // NOT part of background — random selection on load
        var character = FindOrCreateImageAnchored("CharacterDisplay", screen.transform,
            anchorCenter: new Vector2(0.48f, 1f - (1035f / H)),  // slight left offset
            size: new Vector2(900f, 1230f));
        var charImg = character.GetComponent<Image>();
        charImg.preserveAspect = true;
        charImg.raycastTarget = false;
        TryAssignSprite(character, SpritePaths.CharacterPlaceholder, new Color(1f, 1f, 1f, 0.3f));
        SetPrivateField(component, "characterImage", charImg);

        // Load character sprites from folder
        var charSprites = LoadSpritesFromFolder("Assets/Art/UI/Characters/");
        if (charSprites.Length > 0)
            SetPrivateField(component, "characterSprites", charSprites);

        // ─── GOLFIN GPS Banner ────────────────────────────────────
        // Figma: Y=1560, 1010×200
        var gpsBanner = FindOrCreateImageAnchored("GpsBanner", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (1660f / H)),
            size: new Vector2(1010f, 200f));
        TryAssignSprite(gpsBanner, SpritePaths.GpsBanner,
            new Color(0.10f, 0.13f, 0.19f, 0.85f));
        gpsBanner.GetComponent<Image>().type = Image.Type.Sliced;

        // GPS Pin icon
        var gpsPin = FindOrCreate("GpsPinIcon", gpsBanner.transform);
        var gpsPinRT = EnsureComponent<RectTransform>(gpsPin);
        gpsPinRT.anchorMin = new Vector2(0f, 0.5f);
        gpsPinRT.anchorMax = new Vector2(0f, 0.5f);
        gpsPinRT.pivot = new Vector2(0f, 0.5f);
        gpsPinRT.anchoredPosition = new Vector2(370f, 30f);
        gpsPinRT.sizeDelta = new Vector2(50f, 60f);
        TryAssignSprite(gpsPin, SpritePaths.GpsPin, HexColor("#4CAF50"));

        // GPS Title
        var gpsTitle = FindOrCreateTMPAnchored("GpsTitleText", gpsBanner.transform,
            "GOLFIN\u00B7GPS",
            new Vector2(0.5f, 0.75f), 42f,
            TextAlignmentOptions.Left,
            new Vector2(420f, 48f));
        var gpsTitleTMP = gpsTitle.GetComponent<TextMeshProUGUI>();
        gpsTitleTMP.fontStyle = FontStyles.Bold;
        gpsTitleTMP.color = Color.white;
        TrySetFont(gpsTitleTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(gpsTitle, "home_gps_title");
        SetPrivateField(component, "gpsTitleText", gpsTitleTMP);

        // GPS Subtitle
        var gpsSub = FindOrCreateTMPAnchored("GpsSubtitleText", gpsBanner.transform,
            "CHECK-IN WITH GPS",
            new Vector2(0.5f, 0.45f), 34f,
            TextAlignmentOptions.Left,
            new Vector2(440f, 40f));
        var gpsSubTMP = gpsSub.GetComponent<TextMeshProUGUI>();
        gpsSubTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        gpsSubTMP.color = Color.white;
        TrySetFont(gpsSubTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(gpsSub, "home_gps_subtitle");
        SetPrivateField(component, "gpsSubtitleText", gpsSubTMP);

        // GPS Description (multi-color handled at runtime)
        var gpsDesc = FindOrCreateTMPAnchored("GpsDescText", gpsBanner.transform,
            "EARN MORE POINTS TO POWER UP!",
            new Vector2(0.5f, 0.15f), 30f,
            TextAlignmentOptions.Left,
            new Vector2(520f, 36f));
        var gpsDescTMP = gpsDesc.GetComponent<TextMeshProUGUI>();
        gpsDescTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        gpsDescTMP.color = Color.white;
        gpsDescTMP.textWrappingMode = TextWrappingModes.NoWrap;
        TrySetFont(gpsDescTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(gpsDesc, "home_gps_desc");
        SetPrivateField(component, "gpsDescText", gpsDescTMP);

        // ─── Next Hole Panel ──────────────────────────────────────
        // Figma: Y=1810, 1010×320
        var nextHolePanel = FindOrCreateImageAnchored("NextHolePanel", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (1970f / H)),
            size: new Vector2(1010f, 320f));
        TryAssignSprite(nextHolePanel, SpritePaths.NextHolePanel,
            new Color(0.06f, 0.09f, 0.19f, 0.8f));
        nextHolePanel.GetComponent<Image>().type = Image.Type.Sliced;

        // Next Hole Header
        var nhHeader = FindOrCreateTMPAnchored("NextHoleHeader", nextHolePanel.transform,
            "NEXT HOLE",
            new Vector2(0.5f, 0.85f), 42f,
            TextAlignmentOptions.Center,
            new Vector2(320f, 48f));
        var nhHeaderTMP = nhHeader.GetComponent<TextMeshProUGUI>();
        nhHeaderTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        nhHeaderTMP.color = Color.white;
        TrySetFont(nhHeaderTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(nhHeader, "home_next_hole");
        SetPrivateField(component, "nextHoleHeader", nhHeaderTMP);

        // Course name
        var courseName = FindOrCreateTMPAnchored("CourseNameText", nextHolePanel.transform,
            "Lomond Country Club  - Hole 5",
            new Vector2(0.5f, 0.66f), 36f,
            TextAlignmentOptions.Center,
            new Vector2(900f, 44f));
        var courseNameTMP = courseName.GetComponent<TextMeshProUGUI>();
        courseNameTMP.color = HexColor("#D0D8E4");
        TrySetFont(courseNameTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(courseName, "home_course_name");
        SetPrivateField(component, "courseNameText", courseNameTMP);

        // Separator line
        var nhSeparator = FindOrCreate("NextHoleSeparator", nextHolePanel.transform);
        var nhSepRT = EnsureComponent<RectTransform>(nhSeparator);
        nhSepRT.anchorMin = new Vector2(0.05f, 0.5f);
        nhSepRT.anchorMax = new Vector2(0.95f, 0.5f);
        nhSepRT.sizeDelta = new Vector2(0f, 2f);
        nhSepRT.anchoredPosition = Vector2.zero;
        var nhSepImg = EnsureComponent<Image>(nhSeparator);
        nhSepImg.color = new Color(0.23f, 0.31f, 0.44f, 0.5f);

        // Reward icons row
        var rewardsRow = FindOrCreate("RewardsRow", nextHolePanel.transform);
        var rewardsRT = EnsureComponent<RectTransform>(rewardsRow);
        rewardsRT.anchorMin = new Vector2(0.5f, 0.32f);
        rewardsRT.anchorMax = new Vector2(0.5f, 0.32f);
        rewardsRT.pivot = new Vector2(0.5f, 0.5f);
        rewardsRT.sizeDelta = new Vector2(640f, 50f);
        rewardsRT.anchoredPosition = Vector2.zero;

        var rewardsHLG = EnsureComponent<HorizontalLayoutGroup>(rewardsRow);
        rewardsHLG.spacing = 40f;
        rewardsHLG.childAlignment = TextAnchor.MiddleCenter;
        rewardsHLG.childControlWidth = false;
        rewardsHLG.childControlHeight = false;

        string[] rewardSprites = { SpritePaths.RewardCoin, SpritePaths.RewardItem, SpritePaths.RewardBall };
        var rewardTMPs = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            var rewardGroup = FindOrCreate($"Reward_{i}", rewardsRow.transform);
            var rgRT = EnsureComponent<RectTransform>(rewardGroup);
            rgRT.sizeDelta = new Vector2(100f, 50f);

            var rgHLG = EnsureComponent<HorizontalLayoutGroup>(rewardGroup);
            rgHLG.spacing = 4f;
            rgHLG.childAlignment = TextAnchor.MiddleLeft;
            rgHLG.childControlWidth = false;
            rgHLG.childControlHeight = false;

            var rIcon = FindOrCreate($"RewardIcon_{i}", rewardGroup.transform);
            var riRT = EnsureComponent<RectTransform>(rIcon);
            riRT.sizeDelta = new Vector2(36f, 36f);
            TryAssignSprite(rIcon, rewardSprites[i], Color.white);

            var rText = FindOrCreate($"RewardText_{i}", rewardGroup.transform);
            var rtRT = EnsureComponent<RectTransform>(rText);
            rtRT.sizeDelta = new Vector2(56f, 40f);
            var rtTMP = EnsureComponent<TextMeshProUGUI>(rText);
            rtTMP.text = "x10";
            rtTMP.fontSize = 32f;
            rtTMP.fontStyle = FontStyles.Bold;
            rtTMP.color = Color.white;
            TrySetFont(rtTMP, "Rubik-SemiBold SDF");
            rewardTMPs[i] = rtTMP;
        }
        SetPrivateField(component, "rewardTexts", rewardTMPs);

        // Chevron arrow
        var chevron = FindOrCreateTMPAnchored("Chevron", rewardsRow.transform,
            ">", new Vector2(0.5f, 0.5f), 40f,
            TextAlignmentOptions.Center, new Vector2(24f, 40f));
        var chevronTMP = chevron.GetComponent<TextMeshProUGUI>();
        chevronTMP.color = HexColor("#8090A8");

        // PLAY button
        var playBtn = FindOrCreateImageAnchored("PlayButton", nextHolePanel.transform,
            anchorCenter: new Vector2(0.5f, 0.12f),
            size: new Vector2(450f, 120f));
        TryAssignSprite(playBtn, SpritePaths.PlayButton, GreenButton);
        playBtn.GetComponent<Image>().type = Image.Type.Sliced;
        EnsureComponent<Button>(playBtn);
        var playPressable = EnsureComponent<PressableButton>(playBtn);

        var playText = FindOrCreateTMPAnchored("PlayText", playBtn.transform,
            "PLAY",
            new Vector2(0.5f, 0.5f), 52f,
            TextAlignmentOptions.Center,
            new Vector2(400f, 100f));
        var playTMP = playText.GetComponent<TextMeshProUGUI>();
        playTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        playTMP.color = Color.white;
        playTMP.characterSpacing = 4f;
        TrySetFont(playTMP, "Rubik-SemiBold SDF");
        EnsureLocalizedText(playText, "home_play");
        SetPrivateField(component, "playButton", playPressable);

        // ─── Bottom Navigation Bar ────────────────────────────────
        // Figma: Y=2332, full width, 200px tall
        var bottomNav = FindOrCreate("BottomNavBar", screen.transform);
        var bnRT = EnsureComponent<RectTransform>(bottomNav);
        bnRT.anchorMin = new Vector2(0f, 0f);
        bnRT.anchorMax = new Vector2(1f, 0f);
        bnRT.pivot = new Vector2(0.5f, 0f);
        bnRT.sizeDelta = new Vector2(0f, 200f);
        bnRT.anchoredPosition = Vector2.zero;

        var bnBg = EnsureComponent<Image>(bottomNav);
        TryAssignSprite(bottomNav, SpritePaths.BottomNavBg, new Color(0.03f, 0.06f, 0.12f, 0.96f));

        var bnHLG = EnsureComponent<HorizontalLayoutGroup>(bottomNav);
        bnHLG.padding = new RectOffset(40, 40, 20, 60);  // bottom padding for safe area
        bnHLG.spacing = 0f;
        bnHLG.childAlignment = TextAnchor.MiddleCenter;
        bnHLG.childControlWidth = true;
        bnHLG.childControlHeight = true;
        bnHLG.childForceExpandWidth = true;
        bnHLG.childForceExpandHeight = false;

        string[] navNames = { "Home", "Shop", "Play", "Bag", "More" };
        string[] navSprites = { SpritePaths.NavHome, SpritePaths.NavShop, SpritePaths.NavPlay, SpritePaths.NavBag, SpritePaths.NavMore };
        string[] navLocKeys = { "home_nav_home", "home_nav_shop", "home_nav_play", "home_nav_bag", "home_nav_more" };
        PressableButton[] navButtons = new PressableButton[5];

        for (int i = 0; i < 5; i++)
        {
            var navItem = FindOrCreate($"Nav_{navNames[i]}", bottomNav.transform);
            var niRT = EnsureComponent<RectTransform>(navItem);
            niRT.sizeDelta = new Vector2(0f, 120f);

            var niLayout = EnsureComponent<LayoutElement>(navItem);
            niLayout.flexibleWidth = 1f;

            var niVLG = EnsureComponent<VerticalLayoutGroup>(navItem);
            niVLG.spacing = 4f;
            niVLG.childAlignment = TextAnchor.MiddleCenter;
            niVLG.childControlWidth = false;
            niVLG.childControlHeight = false;
            niVLG.childForceExpandWidth = false;
            niVLG.childForceExpandHeight = false;

            var navIcon = FindOrCreate($"NavIcon_{navNames[i]}", navItem.transform);
            var niconRT = EnsureComponent<RectTransform>(navIcon);
            niconRT.sizeDelta = new Vector2(60f, 60f);
            TryAssignSprite(navIcon, navSprites[i], HexColor("#8090A8"));

            var navLabel = FindOrCreate($"NavLabel_{navNames[i]}", navItem.transform);
            var nlRT = EnsureComponent<RectTransform>(navLabel);
            nlRT.sizeDelta = new Vector2(100f, 30f);
            var nlTMP = EnsureComponent<TextMeshProUGUI>(navLabel);
            nlTMP.text = navNames[i].ToUpper();
            nlTMP.fontSize = 24f;
            nlTMP.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            nlTMP.color = i == 0 ? Color.white : HexColor("#8090A8");
            nlTMP.alignment = TextAlignmentOptions.Center;
            TrySetFont(nlTMP, "Rubik-SemiBold SDF");
            EnsureLocalizedText(navLabel, navLocKeys[i]);

            EnsureComponent<Button>(navItem);
            navButtons[i] = EnsureComponent<PressableButton>(navItem);
        }

        SetPrivateField(component, "navHome", navButtons[0]);
        SetPrivateField(component, "navShop", navButtons[1]);
        SetPrivateField(component, "navPlay", navButtons[2]);
        SetPrivateField(component, "navBag", navButtons[3]);
        SetPrivateField(component, "navMore", navButtons[4]);

        return component;
    }

    /// <summary>Load all sprites from a folder path</summary>
    static Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        var sprites = new Sprite[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return sprites;
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
        tmp.overflowMode = TextOverflowModes.Truncate;

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
        tmp.overflowMode = TextOverflowModes.Truncate;

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
