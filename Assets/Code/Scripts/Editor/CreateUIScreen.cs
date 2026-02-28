using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;

/// <summary>
/// Editor tool that creates OR updates the GOLFIN UI scene hierarchy.
/// 
/// ALL VALUES SOURCED FROM FIGMA "Golfin Game Redux" (2026-02-25)
/// via Figma REST API â€” no more guessing from screenshots.
///
/// Reference resolution: 1170Ã—2532 (iPhone Pro Max @3x)
/// Primary font: Rubik (weights 400â€“800)
/// Accent color: #eedc9a (gold)
///
/// Usage: Unity menu â†’ Tools â†’ Create GOLFIN UI Scene
///        (safe to run multiple times â€” FindOrCreate preserves sprites)
/// </summary>
public class CreateUIScreen
{
    // â•â•â• FIGMA REFERENCE RESOLUTION â•â•â•
    const float W = 1170f;
    const float H = 2532f;

    // â•â•â• FIGMA COLORS (exact hex from API) â•â•â•
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

    // â•â•â• FIGMA LAYOUT CONSTANTS (px from absoluteBoundingBox) â•â•â•
    // All positions are relative to the 1170Ã—2532 canvas.
    // Content Container: 978px wide, centered (96px margin each side)
    const float ContentWidth = 978f;
    const float ContentMargin = 96f;  // (1170-978)/2

    // â•â•â• SPRITE PATHS â•â•â•
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
        public const string TopBarBg            = "Assets/Art/UI/Home/top_bar_bg.png";
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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // MAIN ENTRY POINT
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    [MenuItem("Tools/Create GOLFIN UI Scene")]
    public static void CreateUI()
    {
        _newlyCreated.Clear();  // Reset tracking for this run
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

        // â”€â”€â”€ Managers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        GameObject managers = FindOrCreate("Managers", root.transform);
        var locManager = EnsureComponent<LocalizationManager>(managers);
        var screenManager = EnsureComponent<ScreenManager>(managers);
        var bootstrap = EnsureComponent<GameBootstrap>(managers);

        // â”€â”€â”€ Canvas â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        GameObject canvasGO = FindOrCreate("Canvas", root.transform);
        var canvas = EnsureComponent<Canvas>(canvasGO);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = EnsureComponent<CanvasScaler>(canvasGO);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(W, H);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureComponent<GraphicRaycaster>(canvasGO);

        // â”€â”€â”€ Create/Update Screens â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var logoScreen = SetupLogoScreen(canvasGO.transform);
        var loadingScreen = SetupLoadingScreen(canvasGO.transform);
        var splashScreen = SetupSplashScreen(canvasGO.transform);
        var homeScreen = SetupHomeScreen(canvasGO.transform);

        // â”€â”€â”€ Wire GameBootstrap â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        SetPrivateField(bootstrap, "logoScreen", logoScreen);
        SetPrivateField(bootstrap, "loadingScreen", loadingScreen);
        SetPrivateField(bootstrap, "splashScreen", splashScreen);
        SetPrivateField(bootstrap, "homeScreen", homeScreen);

        // â”€â”€â”€ EventSystem â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        if (!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
        {
            GameObject es = FindOrCreate("EventSystem", root.transform);
            EnsureComponent<UnityEngine.EventSystems.EventSystem>(es);
            EnsureComponent<UnityEngine.EventSystems.StandaloneInputModule>(es);
        }

        // â”€â”€â”€ Apply overrides from screen_values.json â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        ApplySceneValueOverrides(canvasGO.transform);

        Selection.activeGameObject = root;
        string mode = isUpdate ? "Updated" : "Created";
        Debug.Log($"[GOLFIN] {mode} UI scene successfully! âœ…");
    }

    /// <summary>
    /// Reads Assets/Code/Data/screen_values.json (exported via Tools â†’ QA â†’ Export Scene Values)
    /// and applies saved values back to matching components.
    /// This preserves Cesar's manual Inspector tweaks across rebuilds.
    /// </summary>
    static void ApplySceneValueOverrides(Transform canvasRoot)
    {
        string path = "Assets/Code/Data/screen_values.json";
        if (!System.IO.File.Exists(path))
        {
            Debug.Log("[GOLFIN] No screen_values.json found â€” using code defaults.");
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
            Debug.Log($"[GOLFIN] Applied {applied} override(s) from screen_values.json âœ…");
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

    // â•â•â• JSON PARSING HELPERS (simple regex, no dependency) â•â•â•

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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // LOGO SCREEN
    // Figma page: "Logo" â€” single component, black bg, centered logo
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    static LogoScreen SetupLogoScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LogoScreen", parent);
        var component = EnsureComponent<LogoScreen>(screen);

        // Full black background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        bg.GetComponent<Image>().color = Color.black;

        // Logo â€” centered horizontally and vertically
        // Figma: Logo Container is 930Ã—168 in Components page
        var logo = FindOrCreateImageAnchored("Logo", screen.transform,
            anchorCenter: new Vector2(0.5f, 0.5f),
            size: new Vector2(930f, 168f));
        TryAssignSprite(logo, SpritePaths.Logo, Color.white);
        logo.GetComponent<Image>().preserveAspect = true;

        return component;
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // LOADING SCREEN
    // Figma page: "Loading" â€” 9 variants, all share same layout structure
    // Values from Figma API + visual comparison (2026-02-26)
    // Canvas: 1170Ã—2532
    //
    // Figma structure (from API):
    //   Screen (1170Ã—2532)
    //     â””â”€ Background (stretched)
    //     â””â”€ BottomGradient (dark vignette at bottom for text legibility)
    //     â””â”€ Game Screen Content (1170Ã—2532)
    //         â””â”€ Content Container (978Ã—2208, x=96, y=24)
    //             â””â”€ Pop-up (978Ã—variable, y=680, radius=50, BACKGROUND_BLUR r=4)
    //                 â””â”€ Mission Title (281Ã—120, radius=[8,8,0,0])
    //                     â””â”€ "PRO TIP" (Rubik:600@66, #eedc9a)
    //                 â””â”€ Separator (978Ã—0 line)
    //                 â””â”€ Goals Container (952Ã—variable)
    //                     â””â”€ Tip text (856Ã—variable, Rubik:600@51, #ffffff)
    //                 â””â”€ Image (806Ã—variable, tip illustrations)
    //                 â””â”€ Goals Container (978Ã—78)
    //                     â””â”€ "TAP FOR NEXT TIP" (882Ã—54, Rubik:600@39, #ffffff)
    //         â””â”€ "NOW LOADING" (978Ã—123, y=2281, Rubik:600@102, #ffffff)
    //         â””â”€ Bar + Size (978Ã—105, y=2428)
    //             â””â”€ Bar (978Ã—30, radius=8, fill=#ffffff)
    //                 â””â”€ Fill (376Ã—30, radius=8, stroke=#000000 w=1)
    //             â””â”€ "52.20 / 267 MB" (978Ã—63, Rubik:600@48, #ffffff)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    static LoadingScreen SetupLoadingScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("LoadingScreen", parent);
        var component = EnsureComponent<LoadingScreen>(screen);

        // â”€â”€â”€ Background â€” full stretch â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.LoadingBackground, new Color(0.15f, 0.25f, 0.1f));

        // â”€â”€â”€ Bottom Gradient Overlay â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€â”€ Content Container (centered, 978px wide) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: x=96 from left edge, 978px wide, 2208px tall, y=24 from top
        GameObject contentContainer = FindOrCreate("ContentContainer", screen.transform);
        var ccRT = EnsureComponent<RectTransform>(contentContainer);
        ccRT.anchorMin = new Vector2(ContentMargin / W, 0f);
        ccRT.anchorMax = new Vector2(1f - ContentMargin / W, 1f);
        ccRT.offsetMin = Vector2.zero;
        ccRT.offsetMax = Vector2.zero;

        // â”€â”€â”€ Pro Tip Card (Pop-up) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: 978px wide (full content container width), radius=50
        //   BACKGROUND_BLUR effect (r=4)
        //   Top edge at Y=680 from canvas top
        //   Y relative to content container: 680 - 24 = 656
        GameObject tipCardGO = FindOrCreate("ProTipCard", contentContainer.transform);
        var tipCardRT = EnsureComponent<RectTransform>(tipCardGO);
        tipCardRT.anchorMin = new Vector2(0f, 0.5f);
        tipCardRT.anchorMax = new Vector2(1f, 0.5f);  // FULL WIDTH, vertically centered anchor
        tipCardRT.pivot = new Vector2(0.5f, 0.5f);    // center pivot â€” card grows from center
        // Figma: card center is roughly at Y=1100 from top â†’ offset from screen center
        // Screen center = 2532/2 = 1266, content center â‰ˆ 1100 â†’ offset = +166 upward
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

        // â”€â”€â”€ "PRO TIP" Header â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: "Mission Title" frame 281Ã—120, cornerRadius=[8,8,0,0]
        //   Text: "PRO TIP", Rubik:600@66, color=#eedc9a, 249Ã—84
        //   MUST be single line â€” container needs enough width
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

        // â”€â”€â”€ Separator Line â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: "Separator" LINE, full 978px width
        var divider = FindOrCreateLayoutImage("Divider", tipCardGO.transform,
            GoldAccent, 2f);

        // â”€â”€â”€ Tip Text â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

        // â”€â”€â”€ Tip Image Display (single Image, swapped per tip) â”€â”€â”€â”€â”€â”€
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
        // LayoutElement â€” ProTipCard.ShowTip() sets preferredWidth/Height from sprite native size
        var tipImgLE = EnsureComponent<LayoutElement>(tipImageGO);
        tipImageGO.SetActive(false); // hidden until a sprite is assigned at runtime

        // Ensure image is before tap-next in sibling order (VerticalLayoutGroup)
        tipImageGO.transform.SetAsLastSibling();

        // â”€â”€â”€ "TAP FOR NEXT TIP" â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: Rubik:600@39, color=#ffffff, 882Ã—54, RIGHT-aligned
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

        // â”€â”€â”€ "NOW LOADING" â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: "Title" text, Rubik:600@102, color=#ffffff
        //   978Ã—123, Y=2281 from canvas top
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

        // â”€â”€â”€ Loading Bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: "Bar" frame, 978Ã—30, Y=2428, cornerRadius=8
        //   Background: fill=#ffffff (semi-transparent track)
        //   Fill: 376Ã—30 (progress), radius=8, stroke=#000000 w=1
        var barBG = FindOrCreateImageAnchored("LoadingBarBG", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (2428f / H)),
            size: new Vector2(ContentWidth, 30f));
        var barBGImg = barBG.GetComponent<Image>();
        barBGImg.color = Color.white;  // Figma: solid white track
        TryAssignSprite(barBG, SpritePaths.LoadingBarPill);
        if (HasSprite(barBG)) barBGImg.type = Image.Type.Sliced;
        EditorUtility.SetDirty(barBGImg);
        var loadingBar = EnsureComponent<LoadingBar>(barBG);

        // Bar fill â€” blue rect that grows from left via RectTransform anchors
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

        // Wire LoadingBar â€” uses RectTransform scaling, not fillAmount
        SetPrivateField(loadingBar, "fillRect", barFillRT);
        SetPrivateField(loadingBar, "fillImage", barFillImg);
        SetPrivateField(loadingBar, "fillColor", new Color(0.13f, 0.50f, 0.88f));

        // Bar glow â€” follows fill edge
        var barGlow = FindOrCreateImageAnchored("LoadingBarGlow", barBG.transform,
            anchorCenter: new Vector2(0f, 0.5f),  // starts at left, LoadingBar.cs moves it
            size: new Vector2(30f, 30f));
        if (!HasSprite(barGlow)) barGlow.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.5f);

        SetPrivateField(loadingBar, "fillImage", barFillImg);
        SetPrivateField(loadingBar, "glowImage", barGlow.GetComponent<Image>());

        // â”€â”€â”€ Download Progress Text â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Figma: "52.20 / 267 MB", Rubik:600@48, color=#ffffff
        //   978Ã—63, Y=2470 from canvas top
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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SPLASH SCREEN
    // Figma page: "Splash Screen" â€” background art + START/CREATE ACCOUNT
    // Canvas: 1170Ã—2532
    //
    // Visual comparison (2026-02-26):
    //   Title block (logo+presents+crest+invitational): composite image, top area
    //   START button: green, centered, Yâ‰ˆ2030 from top
    //   CREATE ACCOUNT: text-only, centered, Yâ‰ˆ2168 from top
    //
    // Key fixes from comparison:
    //   - "presents" â†’ "Presents" (capitalization â€” handled via localization)
    //   - START button: moved up ~20px, width reduced ~10px
    //   - CREATE ACCOUNT: moved up ~20px, font +2px, letter-spacing +1px
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    static SplashScreen SetupSplashScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("SplashScreen", parent);
        var component = EnsureComponent<SplashScreen>(screen);

        // Background
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        TryAssignSprite(bg, SpritePaths.SplashBackground, new Color(0.1f, 0.2f, 0.15f));

        // â”€â”€â”€ Title Image â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Composite image: GOLFIN logo + "Presents" + crest + "The Invitational"
        // Positioned in upper portion, nudged down ~10px vs previous
        var titleImage = FindOrCreateImageAnchored("TitleArea", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (78f / H)),  // top edge at Yâ‰ˆ78 from canvas top
            size: new Vector2(ContentWidth, 365f));
        titleImage.GetComponent<Image>().preserveAspect = true;
        TryAssignSprite(titleImage, SpritePaths.SplashTitle, Color.white);
        // Pivot from top so Y positions the top edge
        var titleRT = titleImage.GetComponent<RectTransform>();
        titleRT.pivot = new Vector2(0.5f, 1f);

        // â”€â”€â”€ START Button â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Visual comparison: button center at Yâ‰ˆ2065 from canvas top
        //   Size: ~330Ã—72 (visual), but we use the sprite-based button
        //   Green gradient: #5EC02C â†’ #3C8E14
        //   Corner radius: ~12px
        //   Drop shadow: 3px Y offset, 5px blur, dark green
        var startBtn = FindOrCreateImageAnchored("StartButton", screen.transform,
            anchorCenter: new Vector2(0.5f, 1f - (2065f / H)),  // Y=2065 center (was 0.835=2114)
            size: new Vector2(330f, 72f));  // was 450Ã—120, visual match is 330Ã—72
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

        // â”€â”€â”€ CREATE ACCOUNT Button â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        // Visual comparison: text center at Yâ‰ˆ2168 from canvas top
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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // HOME SCREEN
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    //
    // Reference: home_screen_ref.png (1170Ã—2532)
    // Layout (top to bottom):
    //   Y=0:     Background (full bleed dark navy + golf course scene)
    //   Y=115:   Top bar â€” currency (left), settings gear (right)
    //   Y=155:   Username banner (centered)
    //   Y=240:   Announcement card (centered, paginated)
    //   Y=540:   Page dots
    //   Y=420-1650: Central character (random, NOT part of bg)
    //   Y=1560:  GOLFIN GPS banner
    //   Y=1810:  Next Hole panel
    //   Y=2332:  Bottom navigation bar (5 tabs)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    static HomeScreen SetupHomeScreen(Transform parent)
    {
        GameObject screen = FindOrCreateScreenPanel("HomeScreen", parent);
        var component = EnsureComponent<HomeScreen>(screen);

        // â”€â”€â”€ Background â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var bg = FindOrCreateImageStretched("Background", screen.transform);
        SetIfNew(bg, () => TryAssignSprite(bg, SpritePaths.HomeBackground, HexColor("#0A1628")));

        // â”€â”€â”€ Top Dark Gradient â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var topGrad = FindOrCreate("TopGradient", screen.transform);
        SetIfNew(topGrad, () => {
            var rt = EnsureComponent<RectTransform>(topGrad);
            rt.anchorMin = new Vector2(0f, 1f - (350f / H));
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = EnsureComponent<Image>(topGrad);
            TryAssignSprite(topGrad, SpritePaths.TopBarBg, new Color(0.04f, 0.08f, 0.16f, 0.7f));
            if (HasSprite(topGrad)) img.color = Color.white;
            img.preserveAspect = false;
            img.raycastTarget = false;
        });
        // Ensure components exist even if not new
        EnsureComponent<RectTransform>(topGrad);
        EnsureComponent<Image>(topGrad);

        // â”€â”€â”€ REMOVED: Bottom Dark Gradient â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var oldBottomGrad = screen.transform.Find("BottomGradient");
        if (oldBottomGrad != null) Object.DestroyImmediate(oldBottomGrad.gameObject);

        // â”€â”€â”€ Currency Display (Top-Left) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var currencyGroup = FindOrCreate("CurrencyGroup", screen.transform);
        SetIfNew(currencyGroup, () => {
            var rt = EnsureComponent<RectTransform>(currencyGroup);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -115f);
            rt.sizeDelta = new Vector2(250f, 62f);
            var pillImg = EnsureComponent<Image>(currencyGroup);
            pillImg.color = new Color(0.04f, 0.06f, 0.13f, 0.5f);
            TryAssignSprite(currencyGroup, SpritePaths.CardBackground, new Color(0.04f, 0.06f, 0.13f, 0.5f));
            if (HasSprite(currencyGroup))
            {
                pillImg.type = Image.Type.Simple; pillImg.preserveAspect = true;
                pillImg.color = new Color(0.04f, 0.06f, 0.13f, 0.5f);
            }
        });
        EnsureComponent<RectTransform>(currencyGroup);
        EnsureComponent<Image>(currencyGroup);

        var hlg = EnsureComponent<HorizontalLayoutGroup>(currencyGroup);
        SetIfNew(currencyGroup, () => {
            hlg.padding = new RectOffset(4, 16, 4, 4);
            hlg.spacing = 8f;
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
        });

        var coinIcon = FindOrCreate("CoinIcon", currencyGroup.transform);
        SetIfNew(coinIcon, () => {
            EnsureComponent<RectTransform>(coinIcon).sizeDelta = new Vector2(54f, 54f);
            EnsureComponent<Image>(coinIcon);
            TryAssignSprite(coinIcon, SpritePaths.CoinGold, HexColor("#D4A017"));
        });
        var coinImg = EnsureComponent<Image>(coinIcon);
        SetPrivateField(component, "currencyIcon", coinImg);

        var currencyText = FindOrCreate("CurrencyText", currencyGroup.transform);
        SetIfNew(currencyText, () => {
            EnsureComponent<RectTransform>(currencyText).sizeDelta = new Vector2(160f, 50f);
            var tmp = EnsureComponent<TextMeshProUGUI>(currencyText);
            tmp.text = component.startingCurrency.ToString();
            tmp.fontSize = 36f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        SetPrivateField(component, "currencyText", EnsureComponent<TextMeshProUGUI>(currencyText));

        // â”€â”€â”€ Settings Gear (Top-Right) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var settingsBtn = FindOrCreate("SettingsButton", screen.transform);
        SetIfNew(settingsBtn, () => {
            var rt = EnsureComponent<RectTransform>(settingsBtn);
            rt.anchorMin = new Vector2(1f - (60f / W), 1f - (144f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(72f, 72f);
            EnsureComponent<Image>(settingsBtn);
            TryAssignSprite(settingsBtn, SpritePaths.SettingsGear, HexColor("#C0C8D4"));
        });
        EnsureComponent<Image>(settingsBtn);
        EnsureComponent<Button>(settingsBtn);

        // â”€â”€â”€ Username Banner (Center-Top) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var userBanner = FindOrCreate("UsernameBanner", screen.transform);
        SetIfNew(userBanner, () => {
            var rt = EnsureComponent<RectTransform>(userBanner);
            rt.anchorMin = new Vector2(0.5f, 1f - (182f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(660f, 55f);
            var img = EnsureComponent<Image>(userBanner);
            TryAssignSprite(userBanner, SpritePaths.UsernameBanner, new Color(0.06f, 0.11f, 0.21f, 0.7f));
            img.type = Image.Type.Simple; img.preserveAspect = true;
        });
        EnsureComponent<RectTransform>(userBanner);
        EnsureComponent<Image>(userBanner);

        var usernameText = FindOrCreate("UsernameText", userBanner.transform);
        SetIfNew(usernameText, () => {
            var rt = EnsureComponent<RectTransform>(usernameText);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = EnsureComponent<TextMeshProUGUI>(usernameText);
            tmp.text = "USERNAME";
            tmp.fontSize = 38f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            tmp.color = Color.white;
            tmp.characterSpacing = 2f;
            tmp.alignment = TextAlignmentOptions.Center;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        var userTMP = EnsureComponent<TextMeshProUGUI>(usernameText);
        EnsureLocalizedText(usernameText, "home_username");
        SetPrivateField(component, "usernameText", userTMP);

        // â”€â”€â”€ Announcement Card â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var annoCard = FindOrCreate("AnnouncementCard", screen.transform);
        SetIfNew(annoCard, () => {
            var rt = EnsureComponent<RectTransform>(annoCard);
            rt.anchorMin = new Vector2(0.5f, 1f - (385f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1010f, 290f);
            var img = EnsureComponent<Image>(annoCard);
            TryAssignSprite(annoCard, SpritePaths.AnnouncementCard, new Color(0.85f, 0.87f, 0.90f, 0.9f));
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            if (HasSprite(annoCard)) img.color = Color.white;
        });
        EnsureComponent<RectTransform>(annoCard);
        EnsureComponent<Image>(annoCard);

        var annoTitle = FindOrCreate("AnnouncementTitle", annoCard.transform);
        SetIfNew(annoTitle, () => {
            var rt = EnsureComponent<RectTransform>(annoTitle);
            rt.anchorMin = new Vector2(0.5f, 1f - (28f / 290f));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 48f);
            var tmp = EnsureComponent<TextMeshProUGUI>(annoTitle);
            tmp.text = "MAINTENANCE NOTICE";
            tmp.fontSize = 40f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            tmp.color = GoldAccent;
            tmp.alignment = TextAlignmentOptions.Center;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(annoTitle, "home_maintenance_title");
        SetPrivateField(component, "announcementTitle", EnsureComponent<TextMeshProUGUI>(annoTitle));

        var annoBody = FindOrCreate("AnnouncementBody", annoCard.transform);
        SetIfNew(annoBody, () => {
            var rt = EnsureComponent<RectTransform>(annoBody);
            rt.anchorMin = new Vector2(0.5f, 0.35f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 150f);
            var tmp = EnsureComponent<TextMeshProUGUI>(annoBody);
            tmp.text = "Scheduled server maintenance: 2025/12/31\nThe game will not be available for a short time\nduring maintenance.";
            tmp.fontSize = 32f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = HexColor("#2C3E5A");
            tmp.alignment = TextAlignmentOptions.Center;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(annoBody, "home_maintenance_body");
        SetPrivateField(component, "announcementBody", EnsureComponent<TextMeshProUGUI>(annoBody));

        // â”€â”€â”€ Page Indicator Dots â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var dotContainer = FindOrCreate("DotContainer", screen.transform);
        SetIfNew(dotContainer, () => {
            var rt = EnsureComponent<RectTransform>(dotContainer);
            rt.anchorMin = new Vector2(0.5f, 1f - (540f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(80f, 16f);
            rt.anchoredPosition = Vector2.zero;
            var hlgDot = EnsureComponent<HorizontalLayoutGroup>(dotContainer);
            hlgDot.spacing = 12f;
            hlgDot.childAlignment = TextAnchor.MiddleCenter;
            hlgDot.childControlWidth = false;
            hlgDot.childControlHeight = false;
        });
        EnsureComponent<RectTransform>(dotContainer);

        for (int i = 0; i < 3; i++)
        {
            var dot = FindOrCreate($"Dot_{i}", dotContainer.transform);
            SetIfNew(dot, () => {
                EnsureComponent<RectTransform>(dot).sizeDelta = new Vector2(16f, 16f);
                EnsureComponent<Image>(dot).color = i == 0
                    ? new Color(1f, 1f, 1f, 1f)
                    : new Color(1f, 1f, 1f, 0.4f);
            });
        }
        SetPrivateField(component, "dotContainer", dotContainer.transform);

        // â”€â”€â”€ Central Character â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var character = FindOrCreate("CharacterDisplay", screen.transform);
        SetIfNew(character, () => {
            var rt = EnsureComponent<RectTransform>(character);
            rt.anchorMin = new Vector2(0.48f, 1f - (1035f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 1230f);
            var img = EnsureComponent<Image>(character);
            img.preserveAspect = true;
            img.raycastTarget = false;
            TryAssignSprite(character, SpritePaths.CharacterPlaceholder, new Color(1f, 1f, 1f, 0.3f));
        });
        var charImg = EnsureComponent<Image>(character);
        SetPrivateField(component, "characterImage", charImg);

        // Load character sprites from folder (always refresh)
        var charSprites = LoadSpritesFromFolder("Assets/Art/UI/Characters/");
        if (charSprites.Length > 0)
            SetPrivateField(component, "characterSprites", charSprites);

        // â”€â”€â”€ GOLFIN GPS Banner â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var gpsBanner = FindOrCreate("GpsBanner", screen.transform);
        SetIfNew(gpsBanner, () => {
            var rt = EnsureComponent<RectTransform>(gpsBanner);
            rt.anchorMin = new Vector2(0.5f, 1f - (1660f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1010f, 200f);
            var img = EnsureComponent<Image>(gpsBanner);
            TryAssignSprite(gpsBanner, SpritePaths.GpsBanner, new Color(0.10f, 0.13f, 0.19f, 0.85f));
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
        });
        EnsureComponent<RectTransform>(gpsBanner);
        EnsureComponent<Image>(gpsBanner);

        // GPS Pin icon
        var gpsPin = FindOrCreate("GpsPinIcon", gpsBanner.transform);
        SetIfNew(gpsPin, () => {
            var rt = EnsureComponent<RectTransform>(gpsPin);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(370f, 30f);
            rt.sizeDelta = new Vector2(50f, 60f);
            EnsureComponent<Image>(gpsPin);
            TryAssignSprite(gpsPin, SpritePaths.GpsPin, HexColor("#4CAF50"));
        });

        // GPS Title
        var gpsTitle = FindOrCreate("GpsTitleText", gpsBanner.transform);
        SetIfNew(gpsTitle, () => {
            var rt = EnsureComponent<RectTransform>(gpsTitle);
            rt.anchorMin = new Vector2(0.5f, 0.75f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 48f);
            var tmp = EnsureComponent<TextMeshProUGUI>(gpsTitle);
            tmp.text = "GOLFIN\u00B7GPS";
            tmp.fontSize = 42f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(gpsTitle, "home_gps_title");
        SetPrivateField(component, "gpsTitleText", EnsureComponent<TextMeshProUGUI>(gpsTitle));

        // GPS Subtitle
        var gpsSub = FindOrCreate("GpsSubtitleText", gpsBanner.transform);
        SetIfNew(gpsSub, () => {
            var rt = EnsureComponent<RectTransform>(gpsSub);
            rt.anchorMin = new Vector2(0.5f, 0.45f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(440f, 40f);
            var tmp = EnsureComponent<TextMeshProUGUI>(gpsSub);
            tmp.text = "CHECK-IN WITH GPS";
            tmp.fontSize = 34f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Left;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(gpsSub, "home_gps_subtitle");
        SetPrivateField(component, "gpsSubtitleText", EnsureComponent<TextMeshProUGUI>(gpsSub));

        // GPS Description
        var gpsDesc = FindOrCreate("GpsDescText", gpsBanner.transform);
        SetIfNew(gpsDesc, () => {
            var rt = EnsureComponent<RectTransform>(gpsDesc);
            rt.anchorMin = new Vector2(0.5f, 0.15f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 36f);
            var tmp = EnsureComponent<TextMeshProUGUI>(gpsDesc);
            tmp.text = "EARN MORE POINTS TO POWER UP!";
            tmp.fontSize = 30f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            tmp.color = Color.white;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment = TextAlignmentOptions.Left;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(gpsDesc, "home_gps_desc");
        SetPrivateField(component, "gpsDescText", EnsureComponent<TextMeshProUGUI>(gpsDesc));

        // â”€â”€â”€ Next Hole Panel â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var nextHolePanel = FindOrCreate("NextHolePanel", screen.transform);
        SetIfNew(nextHolePanel, () => {
            var rt = EnsureComponent<RectTransform>(nextHolePanel);
            rt.anchorMin = new Vector2(0.5f, 1f - (2010f / H));
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1010f, 400f);
            var img = EnsureComponent<Image>(nextHolePanel);
            TryAssignSprite(nextHolePanel, SpritePaths.NextHolePanel, new Color(0.06f, 0.09f, 0.19f, 0.8f));
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            if (HasSprite(nextHolePanel)) img.color = Color.white;
        });
        EnsureComponent<RectTransform>(nextHolePanel);
        EnsureComponent<Image>(nextHolePanel);

        // Next Hole Header
        var nhHeader = FindOrCreate("NextHoleHeader", nextHolePanel.transform);
        SetIfNew(nhHeader, () => {
            var rt = EnsureComponent<RectTransform>(nhHeader);
            rt.anchorMin = new Vector2(0.5f, 0.88f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(400f, 48f);
            var tmp = EnsureComponent<TextMeshProUGUI>(nhHeader);
            tmp.text = "NEXT HOLE";
            tmp.fontSize = 42f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            tmp.color = GoldAccent;
            tmp.alignment = TextAlignmentOptions.Center;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(nhHeader, "home_next_hole");
        SetPrivateField(component, "nextHoleHeader", EnsureComponent<TextMeshProUGUI>(nhHeader));

        // Course name
        var courseName = FindOrCreate("CourseNameText", nextHolePanel.transform);
        SetIfNew(courseName, () => {
            var rt = EnsureComponent<RectTransform>(courseName);
            rt.anchorMin = new Vector2(0.5f, 0.74f);
            rt.anchorMax = rt.anchorMin;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 44f);
            var tmp = EnsureComponent<TextMeshProUGUI>(courseName);
            tmp.text = "COURSE NAME - HOLE 1";
            tmp.fontSize = 36f;
            tmp.color = HexColor("#D0D8E4");
            tmp.alignment = TextAlignmentOptions.Center;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(courseName, "home_course_name");
        SetPrivateField(component, "courseNameText", EnsureComponent<TextMeshProUGUI>(courseName));

        // Separator line
        var nhSeparator = FindOrCreate("NextHoleSeparator", nextHolePanel.transform);
        SetIfNew(nhSeparator, () => {
            var rt = EnsureComponent<RectTransform>(nhSeparator);
            rt.anchorMin = new Vector2(0.05f, 0.62f);
            rt.anchorMax = new Vector2(0.95f, 0.62f);
            rt.sizeDelta = new Vector2(0f, 2f);
            rt.anchoredPosition = Vector2.zero;
            EnsureComponent<Image>(nhSeparator).color = new Color(0.23f, 0.31f, 0.44f, 0.5f);
        });

        // â”€â”€â”€ Reward icons row â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var rewardsRow = FindOrCreate("RewardsRow", nextHolePanel.transform);
        SetIfNew(rewardsRow, () => {
            var rt = EnsureComponent<RectTransform>(rewardsRow);
            rt.anchorMin = new Vector2(0.5f, 0.46f);
            rt.anchorMax = new Vector2(0.5f, 0.46f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(700f, 50f);
            rt.anchoredPosition = Vector2.zero;
            var rhlg = EnsureComponent<HorizontalLayoutGroup>(rewardsRow);
            rhlg.spacing = 30f;
            rhlg.childAlignment = TextAnchor.MiddleCenter;
            rhlg.childControlWidth = false;
            rhlg.childControlHeight = false;
        });

        string[] rewardSprites = { SpritePaths.RewardCoin, SpritePaths.RewardItem, SpritePaths.RewardBall };
        var rewardTMPs = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            var rewardGroup = FindOrCreate($"Reward_{i}", rewardsRow.transform);
            SetIfNew(rewardGroup, () => {
                EnsureComponent<RectTransform>(rewardGroup).sizeDelta = new Vector2(120f, 50f);
                var rgHLG = EnsureComponent<HorizontalLayoutGroup>(rewardGroup);
                rgHLG.spacing = 6f;
                rgHLG.childAlignment = TextAnchor.MiddleCenter;
                rgHLG.childControlWidth = false;
                rgHLG.childControlHeight = false;
            });

            var rIcon = FindOrCreate($"RewardIcon_{i}", rewardGroup.transform);
            SetIfNew(rIcon, () => {
                EnsureComponent<RectTransform>(rIcon).sizeDelta = new Vector2(36f, 36f);
                var riImg = EnsureComponent<Image>(rIcon);
                riImg.preserveAspect = true;
                riImg.raycastTarget = false;
                TryAssignSprite(rIcon, rewardSprites[i], Color.white);
            });
            EnsureComponent<Image>(rIcon);

            var rText = FindOrCreate($"RewardText_{i}", rewardGroup.transform);
            SetIfNew(rText, () => {
                EnsureComponent<RectTransform>(rText).sizeDelta = new Vector2(64f, 40f);
                var tmp = EnsureComponent<TextMeshProUGUI>(rText);
                tmp.text = "x10";
                tmp.fontSize = 32f;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                TrySetFont(tmp, "Rubik-SemiBold SDF");
            });
            rewardTMPs[i] = EnsureComponent<TextMeshProUGUI>(rText);
        }
        SetPrivateField(component, "rewardTexts", rewardTMPs);

        // Chevron ">"
        var chevron = FindOrCreate("Chevron", rewardsRow.transform);
        SetIfNew(chevron, () => {
            EnsureComponent<RectTransform>(chevron).sizeDelta = new Vector2(40f, 40f);
            var tmp = EnsureComponent<TextMeshProUGUI>(chevron);
            tmp.text = ">";
            tmp.fontSize = 36f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = HexColor("#8090A8");
            tmp.alignment = TextAlignmentOptions.Center;
        });

        // PLAY button
        var playBtn = FindOrCreate("PlayButton", nextHolePanel.transform);
        SetIfNew(playBtn, () => {
            var rt = EnsureComponent<RectTransform>(playBtn);
            rt.anchorMin = new Vector2(0.5f, 0.18f);
            rt.anchorMax = new Vector2(0.5f, 0.18f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(450f, 120f);
            var img = EnsureComponent<Image>(playBtn);
            TryAssignSprite(playBtn, SpritePaths.PlayButton, GreenButton);
            img.type = Image.Type.Simple; img.preserveAspect = true;
        });
        EnsureComponent<Button>(playBtn);
        var playPressable = EnsureComponent<PressableButton>(playBtn);

        var playText = FindOrCreate("PlayText", playBtn.transform);
        SetIfNew(playText, () => {
            var rt = EnsureComponent<RectTransform>(playText);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = EnsureComponent<TextMeshProUGUI>(playText);
            tmp.text = "PLAY";
            tmp.fontSize = 52f;
            tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            tmp.color = Color.white;
            tmp.characterSpacing = 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            TrySetFont(tmp, "Rubik-SemiBold SDF");
        });
        EnsureLocalizedText(playText, "home_play");
        SetPrivateField(component, "playButton", playPressable);

        // â”€â”€â”€ Bottom Navigation Bar â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
        var bottomNav = FindOrCreate("BottomNavBar", screen.transform);
        SetIfNew(bottomNav, () => {
            var rt = EnsureComponent<RectTransform>(bottomNav);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 180f);
            rt.anchoredPosition = Vector2.zero;
            EnsureComponent<Image>(bottomNav);
            TryAssignSprite(bottomNav, SpritePaths.BottomNavBg, new Color(0.03f, 0.06f, 0.12f, 0.96f));
            var bnHLG = EnsureComponent<HorizontalLayoutGroup>(bottomNav);
            bnHLG.padding = new RectOffset(60, 60, 10, 50);
            bnHLG.spacing = 0f;
            bnHLG.childAlignment = TextAnchor.MiddleCenter;
            bnHLG.childControlWidth = true;
            bnHLG.childControlHeight = false;
            bnHLG.childForceExpandWidth = true;
            bnHLG.childForceExpandHeight = false;
        });
        EnsureComponent<RectTransform>(bottomNav);
        EnsureComponent<Image>(bottomNav);

        string[] navNames = { "Home", "Shop", "Play", "Bag", "More" };
        string[] navSprites = { SpritePaths.NavHome, SpritePaths.NavShop, SpritePaths.NavPlay, SpritePaths.NavBag, SpritePaths.NavMore };
        PressableButton[] navButtons = new PressableButton[5];

        for (int i = 0; i < 5; i++)
        {
            var navItem = FindOrCreate($"Nav_{navNames[i]}", bottomNav.transform);
            SetIfNew(navItem, () => {
                bool isCenter = (i == 2);
                float iconSize = isCenter ? 100f : 70f;
                EnsureComponent<RectTransform>(navItem).sizeDelta = new Vector2(0f, isCenter ? 120f : 80f);
                EnsureComponent<LayoutElement>(navItem).flexibleWidth = 1f;

                var navIcon = FindOrCreate($"NavIcon_{navNames[i]}", navItem.transform);
                var niconRT = EnsureComponent<RectTransform>(navIcon);
                niconRT.anchorMin = new Vector2(0.5f, 0.5f);
                niconRT.anchorMax = new Vector2(0.5f, 0.5f);
                niconRT.pivot = new Vector2(0.5f, 0.5f);
                niconRT.sizeDelta = new Vector2(iconSize, iconSize);
                niconRT.anchoredPosition = isCenter ? new Vector2(0f, 20f) : Vector2.zero;
                var niconImg = EnsureComponent<Image>(navIcon);
                niconImg.preserveAspect = true;
                TryAssignSprite(navIcon, navSprites[i], HexColor("#8090A8"));
            });

            // Remove old text labels if present
            var oldLabel = navItem.transform.Find($"NavLabel_{navNames[i]}");
            if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
            var oldVLG = navItem.GetComponent<VerticalLayoutGroup>();
            if (oldVLG != null) Object.DestroyImmediate(oldVLG);

            // Always ensure these for wiring
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


    /// <summary>Load all sprites from a folder path, ensuring each is imported as Sprite/Single</summary>
    static Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath.TrimEnd('/') });
        var list = new System.Collections.Generic.List<Sprite>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            EnsureSpriteImport(path);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) list.Add(sprite);
        }
        list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
        return list.ToArray();
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // FIND-OR-CREATE HELPERS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    // Track which objects were just created (vs found existing) this run
    static readonly System.Collections.Generic.HashSet<int> _newlyCreated =
        new System.Collections.Generic.HashSet<int>();

    /// <summary>Returns true if the object was JUST created this run (not pre-existing in scene)</summary>
    static bool IsNew(GameObject go) => _newlyCreated.Contains(go.GetInstanceID());

    /// <summary>
    /// Set a value on a component ONLY if the owning GameObject is newly created.
    /// Existing objects keep their Inspector values untouched.
    /// </summary>
    static void SetIfNew(GameObject go, System.Action action)
    {
        if (IsNew(go)) action();
    }

    static GameObject FindOrCreate(string name, Transform parent)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        _newlyCreated.Add(go.GetInstanceID());
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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SPRITE HELPERS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    static bool HasSprite(GameObject go)
    {
        var img = go.GetComponent<Image>();
        return img != null && img.sprite != null;
    }

    /// <summary>
    /// Ensures the texture at assetPath is imported as Sprite (2D and UI), SpriteMode=Single.
    /// Must be called before LoadAssetAtPath&lt;Sprite&gt; for PNGs that haven't been configured.
    /// </summary>
    static void EnsureSpriteImport(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        bool dirty = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            dirty = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            dirty = true;
        }
        if (dirty)
        {
            importer.SaveAndReimport();
        }
    }

    static void TryAssignSprite(GameObject go, string assetPath, Color fallbackColor = default)
    {
        var img = go.GetComponent<Image>();
        if (img == null) return;

        EnsureSpriteImport(assetPath);
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
        return LoadSpritesFromFolder(SpritePaths.TipImageFolder);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // FONT HELPER
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // UTILITY
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

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
