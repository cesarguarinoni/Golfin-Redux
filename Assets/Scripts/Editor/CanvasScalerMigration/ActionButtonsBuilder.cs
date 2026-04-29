using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Gameplay.UI.HUD;

/// <summary>
/// Builds the 2x2 action button cluster in LabScaffold.unity under ShotUI_Canvas,
/// per spec 8_5_action_buttons.
/// Menu: GOLFIN/Build/Build Action Buttons (8.5)
/// </summary>
public static class ActionButtonsBuilder
{
    [MenuItem("GOLFIN/Build/Build Action Buttons (8.5)")]
    public static void BuildActionButtons()
    {
        AssetDatabase.Refresh();

        // ── Load font ──────────────────────────────────────────────────────────
        TMP_FontAsset rubikFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Fonts/Rubik-VariableFont_wght SDF.asset");
        if (rubikFont == null)
            Debug.LogWarning("[ActionButtonsBuilder] Could not load Rubik-VariableFont_wght SDF.asset");

        // ── Coerce PNG imports to Sprite ───────────────────────────────────────
        CoerceSprite("Assets/Art/In-Game UI/Button - All.png");
        CoerceSprite("Assets/Art/In-Game UI/Icon - Spin.png");
        CoerceSprite("Assets/Art/In-Game UI/Icon - DrawFade.png");
        CoerceSprite("Assets/Art/In-Game UI/Icon - Straight.png");

        // ── Load sprites ───────────────────────────────────────────────────────
        Sprite btnAllSprite    = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Button - All.png");
        Sprite iconSpinSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Spin.png");
        Sprite iconFadeSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - DrawFade.png");
        Sprite iconStraSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Straight.png");

        // Default sprites for fallback (no managers active in LabScaffold)
        // Coerce these to Sprite type first — they live in Resources/ which imports as Default by default.
        CoerceSprite("Assets/Resources/Clubs/Portraits/S_Menu_Driver_GOLFIN.png");
        CoerceSprite("Assets/Resources/Balls/Thumbnails/S_Controls_Ball_GOLFIN.png");
        CoerceSprite("Assets/Resources/Balls/Full/Golfin.png");
        Sprite defaultClubPortrait  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Clubs/Portraits/S_Menu_Driver_GOLFIN.png");
        Sprite defaultBallThumbnail = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Balls/Thumbnails/S_Controls_Ball_GOLFIN.png");
        Sprite defaultBallFull      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Balls/Full/Golfin.png");
        if (defaultClubPortrait  == null) Debug.LogWarning("[ActionButtonsBuilder] Default club portrait not found: Clubs/Portraits/S_Menu_Driver_GOLFIN.png");
        if (defaultBallThumbnail == null) Debug.LogWarning("[ActionButtonsBuilder] Default ball thumbnail not found: Balls/Thumbnails/S_Controls_Ball_GOLFIN.png");
        if (defaultBallFull      == null) Debug.LogWarning("[ActionButtonsBuilder] Default ball full sprite not found: Balls/Full/Golfin.png");

        if (btnAllSprite   == null) Debug.LogWarning("[ActionButtonsBuilder] Button - All.png not found as Sprite");
        if (iconSpinSprite == null) Debug.LogWarning("[ActionButtonsBuilder] Icon - Spin.png not found as Sprite");
        if (iconFadeSprite == null) Debug.LogWarning("[ActionButtonsBuilder] Icon - DrawFade.png not found as Sprite");
        if (iconStraSprite == null) Debug.LogWarning("[ActionButtonsBuilder] Icon - Straight.png not found as Sprite");

        // ── Find ShotUI_Canvas ─────────────────────────────────────────────────
        var canvasGo = GameObject.Find("ShotUI_Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[ActionButtonsBuilder] ShotUI_Canvas not found. Open LabScaffold.unity first.");
            EditorUtility.DisplayDialog("Error", "ShotUI_Canvas not found. Open LabScaffold.unity first.", "OK");
            return;
        }
        var canvasRt = canvasGo.GetComponent<RectTransform>();

        // ── Remove existing action button GOs ──────────────────────────────────
        RemoveChild(canvasGo.transform, "ActionButtons_Cluster");
        RemoveChild(canvasGo.transform, "SelectorOverlay");
        RemoveChild(canvasGo.transform, "SpinPanel");
        RemoveChild(canvasGo.transform, "OutsideClickCatcher_Selector");
        RemoveChild(canvasGo.transform, "OutsideClickCatcher_Spin");

        Color white     = Color.white;
        Color navyColor = HexToColor("001E39");

        // ══════════════════════════════════════════════════════════════════════
        // BUILD CARD PREFAB (built in-memory as a GO, saved as hidden prefab GO)
        // Each selector card is 148×240, same skeleton as the bottom buttons.
        // ══════════════════════════════════════════════════════════════════════
        GameObject cardPrefabGo = BuildCardPrefabGo(btnAllSprite, rubikFont, white, navyColor);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD ActionButtons_Cluster
        // ══════════════════════════════════════════════════════════════════════
        var cluster = CreateRectTransform("ActionButtons_Cluster", canvasGo.transform);
        // Cluster is full-screen, anchored stretch-stretch
        StretchFill(cluster);
        // Add CanvasGroup + ActionButtonsRoot
        var clusterCg   = cluster.gameObject.AddComponent<CanvasGroup>();
        var abRoot      = cluster.gameObject.AddComponent<ActionButtonsRoot>();

        // Wire ActionButtonsRoot._group via SO
        var abRootSo = new SerializedObject(abRoot);
        abRootSo.FindProperty("_group").objectReferenceValue = clusterCg;
        // _shotController stays null in LabScaffold — widget tolerates null gracefully
        abRootSo.ApplyModifiedProperties();

        // ── SPIN button (top-left, BL anchor) ─────────────────────────────────
        var spinBtnRt = BuildButton("SpinButton", cluster,
            anchorMin: Vector2.zero, anchorMax: Vector2.zero, pivot: Vector2.zero,
            anchoredPos: new Vector2(58f, 360f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: iconSpinSprite,
            primaryLabel: "SPIN", secondaryLabel: null,
            rubikFont, out var spinBtn, out var spinIconImg, out var spinPrimaryTmp, out var _);

        var spinWidget = spinBtnRt.gameObject.AddComponent<SpinButtonWidget>();

        // ── FADE/DRAW button (top-right, BR anchor) ────────────────────────────
        var fadeBtnRt = BuildButton("FadeDrawButton", cluster,
            anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-58f, 360f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: iconStraSprite,  // starts as STRAIGHT
            primaryLabel: "STRAIGHT", secondaryLabel: null,
            rubikFont, out var fadeBtn, out var fadeIconImg, out var fadePrimaryTmp, out var _2);

        var fadeWidget = fadeBtnRt.gameObject.AddComponent<FadeDrawButtonWidget>();

        // ── GOLFIN button (bottom-left, BL anchor) ────────────────────────────
        var ballBtnRt = BuildButton("GolfinButton", cluster,
            anchorMin: Vector2.zero, anchorMax: Vector2.zero, pivot: Vector2.zero,
            anchoredPos: new Vector2(58f, 96f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: null,   // driven by BallContext at runtime
            primaryLabel: "GOLFIN", secondaryLabel: "∞",
            rubikFont, out var ballBtn, out var ballIconImg, out var ballPrimaryTmp, out var ballSecTmp);

        var ballWidget = ballBtnRt.gameObject.AddComponent<BallButtonWidget>();

        // ── DRIVER button (bottom-right, BR anchor) ────────────────────────────
        var clubBtnRt = BuildButton("DriverButton", cluster,
            anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-58f, 96f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: null,   // driven by ClubContext at runtime
            primaryLabel: "DRIVER", secondaryLabel: "0 yrds",
            rubikFont, out var clubBtn, out var clubIconImg, out var clubPrimaryTmp, out var clubSecTmp);

        var clubWidget = clubBtnRt.gameObject.AddComponent<ClubButtonWidget>();

        // ══════════════════════════════════════════════════════════════════════
        // BUILD OUTSIDE CLICK CATCHER for selector (full-screen transparent image)
        // MUST be added to canvas before the overlay so it sits below in the hierarchy
        // ══════════════════════════════════════════════════════════════════════
        var selectorCatcherGo = new GameObject("OutsideClickCatcher_Selector");
        selectorCatcherGo.transform.SetParent(canvasGo.transform, false);
        var selectorCatcherRt = selectorCatcherGo.AddComponent<RectTransform>();
        StretchFill(selectorCatcherRt);
        var selectorCatcherImg = selectorCatcherGo.AddComponent<Image>();
        selectorCatcherImg.color = new Color(0f, 0f, 0f, 0f);  // fully transparent
        var selectorCatcher = selectorCatcherGo.AddComponent<OutsideClickCatcher>();
        selectorCatcherGo.SetActive(false);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD SelectorOverlay (initially inactive, 148×744 max)
        // ══════════════════════════════════════════════════════════════════════
        var overlayGo = new GameObject("SelectorOverlay");
        overlayGo.transform.SetParent(canvasGo.transform, false);
        var overlayRt = overlayGo.AddComponent<RectTransform>();
        overlayRt.sizeDelta = new Vector2(148f, 744f);
        overlayRt.anchorMin = overlayRt.anchorMax = new Vector2(1f, 0f);
        overlayRt.pivot = new Vector2(1f, 0f);
        overlayRt.anchoredPosition = new Vector2(-58f, 348f);

        // ArrowUp (use Straight icon rotated 180 degrees = pointing up chevron fallback)
        var arrowUpRt = CreateRectTransform("ArrowUp", overlayGo.transform, new Vector2(96f, 48f));
        arrowUpRt.anchorMin = arrowUpRt.anchorMax = new Vector2(0.5f, 1f);
        arrowUpRt.pivot     = new Vector2(0.5f, 1f);
        arrowUpRt.anchoredPosition = new Vector2(0f, -24f);
        var arrowUpImg = arrowUpRt.gameObject.AddComponent<Image>();
        arrowUpImg.sprite = iconStraSprite;
        arrowUpImg.preserveAspect = true;
        arrowUpImg.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        var arrowUpBtn = arrowUpRt.gameObject.AddComponent<Button>();

        // CardsContainer (VerticalLayoutGroup)
        var cardsContainerRt = CreateRectTransform("CardsContainer", overlayGo.transform, new Vector2(148f, 0f));
        cardsContainerRt.anchorMin = cardsContainerRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardsContainerRt.pivot     = new Vector2(0.5f, 0.5f);
        cardsContainerRt.anchoredPosition = Vector2.zero;
        var vlg = cardsContainerRt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 12f;
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        var csf = cardsContainerRt.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // ArrowDown
        var arrowDownRt = CreateRectTransform("ArrowDown", overlayGo.transform, new Vector2(96f, 48f));
        arrowDownRt.anchorMin = arrowDownRt.anchorMax = new Vector2(0.5f, 0f);
        arrowDownRt.pivot     = new Vector2(0.5f, 0f);
        arrowDownRt.anchoredPosition = new Vector2(0f, 24f);
        var arrowDownImg = arrowDownRt.gameObject.AddComponent<Image>();
        arrowDownImg.sprite = iconStraSprite;
        arrowDownImg.preserveAspect = true;
        var arrowDownBtn = arrowDownRt.gameObject.AddComponent<Button>();

        // Wire SelectorOverlayWidget
        var overlayWidget = overlayGo.AddComponent<SelectorOverlayWidget>();
        var overlaySo = new SerializedObject(overlayWidget);
        overlaySo.FindProperty("_root").objectReferenceValue            = overlayRt;
        overlaySo.FindProperty("_cardsContainer").objectReferenceValue  = cardsContainerRt.transform;
        overlaySo.FindProperty("_cardPrefab").objectReferenceValue      = cardPrefabGo;
        overlaySo.FindProperty("_arrowUp").objectReferenceValue         = arrowUpBtn;
        overlaySo.FindProperty("_arrowDown").objectReferenceValue       = arrowDownBtn;
        overlaySo.FindProperty("_outsideClickCatcher").objectReferenceValue = selectorCatcher;
        overlaySo.ApplyModifiedProperties();

        overlayGo.SetActive(false);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD SPIN PANEL (initially inactive, full-screen dim + ball + dot)
        // ══════════════════════════════════════════════════════════════════════

        // Dim catcher for spin panel (full-screen transparent, below spin panel)
        var spinCatcherGo = new GameObject("OutsideClickCatcher_Spin");
        spinCatcherGo.transform.SetParent(canvasGo.transform, false);
        var spinCatcherRt = spinCatcherGo.AddComponent<RectTransform>();
        StretchFill(spinCatcherRt);
        var spinCatcherImg = spinCatcherGo.AddComponent<Image>();
        spinCatcherImg.color = new Color(0f, 0f, 0f, 0.5f);  // semi-transparent dim
        var spinCatcher = spinCatcherGo.AddComponent<OutsideClickCatcher>();
        spinCatcherGo.SetActive(false);

        // SpinPanel root
        var spinPanelGo = new GameObject("SpinPanel");
        spinPanelGo.transform.SetParent(canvasGo.transform, false);
        var spinPanelRt = spinPanelGo.AddComponent<RectTransform>();
        StretchFill(spinPanelRt);

        // Big ball image (600x600, anchored center)
        var ballImgRt = CreateRectTransform("BallImage", spinPanelGo.transform, new Vector2(600f, 600f));
        SetAnchorCenter(ballImgRt);
        ballImgRt.anchoredPosition = Vector2.zero;
        var ballImg = ballImgRt.gameObject.AddComponent<Image>();
        ballImg.preserveAspect = true;
        ballImg.color = white;

        // Spin dot (60x60, anchored center within ball)
        var dotRt = CreateRectTransform("SpinDot", ballImgRt);
        dotRt.sizeDelta = new Vector2(60f, 60f);
        SetAnchorCenter(dotRt);
        dotRt.anchoredPosition = Vector2.zero;
        var dotImg = dotRt.gameObject.AddComponent<Image>();
        dotImg.color = new Color(1f, 0.2f, 0.2f, 1f);  // red dot

        // 5 invisible position buttons wired to SelectPosition(0..4)
        // Positions: center(0), top(1), bottom(2), left(3), right(4)
        // Each button is 200×200
        Vector2[] btnPositions = {
            new Vector2(   0f,    0f),
            new Vector2(   0f,  220f),
            new Vector2(   0f, -220f),
            new Vector2(-220f,    0f),
            new Vector2( 220f,    0f),
        };
        string[] btnNames = { "SpinBtn_Center", "SpinBtn_Top", "SpinBtn_Bottom", "SpinBtn_Left", "SpinBtn_Right" };

        SpinPanelWidget spinPanelWidget = spinPanelGo.AddComponent<SpinPanelWidget>();

        for (int i = 0; i < 5; i++)
        {
            var posRt = CreateRectTransform(btnNames[i], ballImgRt, new Vector2(200f, 200f));
            SetAnchorCenter(posRt);
            posRt.anchoredPosition = btnPositions[i];
            var posImg = posRt.gameObject.AddComponent<Image>();
            posImg.color = new Color(0f, 0f, 0f, 0f);  // invisible
            var posBtn = posRt.gameObject.AddComponent<Button>();
            posBtn.targetGraphic = posImg;
            int captured = i;
            posBtn.onClick.AddListener(() => spinPanelWidget.SelectPosition(captured));
        }

        // Wire SpinPanelWidget
        var spinPanelSo = new SerializedObject(spinPanelWidget);
        spinPanelSo.FindProperty("_ballImage").objectReferenceValue        = ballImg;
        spinPanelSo.FindProperty("_spinDot").objectReferenceValue          = dotRt;
        spinPanelSo.FindProperty("_dimBackground").objectReferenceValue    = spinCatcher;
        spinPanelSo.FindProperty("_defaultBallSprite").objectReferenceValue = defaultBallThumbnail;
        spinPanelSo.ApplyModifiedProperties();

        spinCatcherGo.SetActive(false);
        spinPanelGo.SetActive(false);

        // ── Wire SpinButtonWidget → SpinPanel ──────────────────────────────────
        var spinBtnSo = new SerializedObject(spinWidget);
        spinBtnSo.FindProperty("_button").objectReferenceValue      = spinBtn;
        spinBtnSo.FindProperty("_iconImage").objectReferenceValue   = spinIconImg;
        spinBtnSo.FindProperty("_primaryText").objectReferenceValue = spinPrimaryTmp;
        spinBtnSo.FindProperty("_spinPanel").objectReferenceValue   = spinPanelWidget;
        spinBtnSo.ApplyModifiedProperties();

        // ── Wire FadeDrawButtonWidget ──────────────────────────────────────────
        var fadeBtnSo = new SerializedObject(fadeWidget);
        fadeBtnSo.FindProperty("_button").objectReferenceValue        = fadeBtn;
        fadeBtnSo.FindProperty("_iconImage").objectReferenceValue     = fadeIconImg;
        fadeBtnSo.FindProperty("_primaryText").objectReferenceValue   = fadePrimaryTmp;
        fadeBtnSo.FindProperty("_iconStraight").objectReferenceValue  = iconStraSprite;
        fadeBtnSo.FindProperty("_iconFadeDraw").objectReferenceValue  = iconFadeSprite;
        fadeBtnSo.ApplyModifiedProperties();

        // ── Wire BallButtonWidget → SelectorOverlay ────────────────────────────
        var ballBtnSo = new SerializedObject(ballWidget);
        ballBtnSo.FindProperty("_button").objectReferenceValue            = ballBtn;
        ballBtnSo.FindProperty("_iconImage").objectReferenceValue         = ballIconImg;
        ballBtnSo.FindProperty("_primaryText").objectReferenceValue       = ballPrimaryTmp;
        ballBtnSo.FindProperty("_secondaryText").objectReferenceValue     = ballSecTmp;
        ballBtnSo.FindProperty("_selectorOverlay").objectReferenceValue   = overlayWidget;
        ballBtnSo.FindProperty("_defaultThumbnail").objectReferenceValue  = defaultBallThumbnail;
        ballBtnSo.ApplyModifiedProperties();

        // ── Wire ClubButtonWidget → SelectorOverlay ────────────────────────────
        var clubBtnSo = new SerializedObject(clubWidget);
        clubBtnSo.FindProperty("_button").objectReferenceValue            = clubBtn;
        clubBtnSo.FindProperty("_iconImage").objectReferenceValue         = clubIconImg;
        clubBtnSo.FindProperty("_primaryText").objectReferenceValue       = clubPrimaryTmp;
        clubBtnSo.FindProperty("_secondaryText").objectReferenceValue     = clubSecTmp;
        clubBtnSo.FindProperty("_selectorOverlay").objectReferenceValue   = overlayWidget;
        clubBtnSo.FindProperty("_defaultPortrait").objectReferenceValue   = defaultClubPortrait;
        clubBtnSo.ApplyModifiedProperties();

        // ══════════════════════════════════════════════════════════════════════
        // ADD POPULATORS to LabRoot
        // ══════════════════════════════════════════════════════════════════════
        var labRoot = GameObject.Find("LabRoot");
        if (labRoot != null)
        {
            if (labRoot.GetComponent<Golfin.UI.HUD.ClubContextPopulator>() == null)
                labRoot.AddComponent<Golfin.UI.HUD.ClubContextPopulator>();
            if (labRoot.GetComponent<Golfin.UI.HUD.BallContextPopulator>() == null)
                labRoot.AddComponent<Golfin.UI.HUD.BallContextPopulator>();
            Debug.Log("[ActionButtonsBuilder] Populators added/verified on LabRoot.");
        }
        else
        {
            Debug.LogWarning("[ActionButtonsBuilder] LabRoot not found — populators not added. Add ClubContextPopulator + BallContextPopulator manually.");
        }

        // ── Mark scene dirty and save ──────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[ActionButtonsBuilder] DONE — ActionButtons_Cluster, SelectorOverlay, SpinPanel built and wired in LabScaffold.unity.");
    }

    /// <summary>Non-interactive version for MCP script execution (no dialog boxes).</summary>
    public static void BuildActionButtonsNoDialog()
    {
        BuildActionButtons();
    }

    // ── Internal card prefab builder ───────────────────────────────────────────

    static GameObject BuildCardPrefabGo(Sprite bgSprite, TMP_FontAsset font,
        Color white, Color navyColor)
    {
        // Build an in-scene GO that acts as the card prefab for the selector overlay.
        // It gets Instantiate()'d at runtime; builder assigns it to _cardPrefab field.
        var go = new GameObject("SelectorCard_Prefab");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(148f, 240f);

        // CardBG
        var bgRt = CreateRectTransform("CardBG", go.transform);
        StretchFill(bgRt);
        var bgImg = bgRt.gameObject.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type   = Image.Type.Simple;

        // IconArea (180x120, anchored top-center, overflow allowed, no mask)
        var iconAreaRt = CreateRectTransform("IconArea", go.transform, new Vector2(180f, 120f));
        iconAreaRt.anchorMin = new Vector2(0.5f, 1f);
        iconAreaRt.anchorMax = new Vector2(0.5f, 1f);
        iconAreaRt.pivot     = new Vector2(0.5f, 1f);
        iconAreaRt.anchoredPosition = Vector2.zero;

        // Icon inside IconArea (stretch with side insets 33,0,-33,0)
        var iconRt = CreateRectTransform("Icon", iconAreaRt);
        StretchFill(iconRt);
        iconRt.offsetMin = new Vector2(33f, 0f);
        iconRt.offsetMax = new Vector2(-33f, 0f);
        var iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = white;

        // PrimaryText (bottom stretch, anchoredPos y=65, h=36)
        var priGo = new GameObject("PrimaryText");
        priGo.transform.SetParent(go.transform, false);
        var priRt = priGo.AddComponent<RectTransform>();
        priRt.anchorMin = new Vector2(0f, 0f);
        priRt.anchorMax = new Vector2(1f, 0f);
        priRt.pivot     = new Vector2(0.5f, 0f);
        priRt.anchoredPosition = new Vector2(0f, 65f);
        priRt.sizeDelta = new Vector2(0f, 36f);
        var priTmp = priGo.AddComponent<TextMeshProUGUI>();
        priTmp.fontSize  = 30;
        priTmp.color     = white;
        priTmp.alignment = TextAlignmentOptions.Center;
        priTmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (font != null) priTmp.font = font;

        // SecondaryText (bottom stretch, anchoredPos y=24, h=36)
        var secGo = new GameObject("SecondaryText");
        secGo.transform.SetParent(go.transform, false);
        var secRt = secGo.AddComponent<RectTransform>();
        secRt.anchorMin = new Vector2(0f, 0f);
        secRt.anchorMax = new Vector2(1f, 0f);
        secRt.pivot     = new Vector2(0.5f, 0f);
        secRt.anchoredPosition = new Vector2(0f, 24f);
        secRt.sizeDelta = new Vector2(0f, 36f);
        var secTmp = secGo.AddComponent<TextMeshProUGUI>();
        secTmp.fontSize  = 30;
        secTmp.color     = white;
        secTmp.alignment = TextAlignmentOptions.Center;
        secTmp.richText  = true;
        if (font != null) secTmp.font = font;

        // Button component for tap
        var btnImgGo = new GameObject("BtnBackground");
        btnImgGo.transform.SetParent(go.transform, false);
        var btnImgRt = btnImgGo.AddComponent<RectTransform>();
        StretchFill(btnImgRt);
        var btnImg = btnImgGo.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0f); // transparent hit area
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // Wire SelectorCardWidget
        var cardWidget = go.AddComponent<SelectorCardWidget>();
        var cardSo = new SerializedObject(cardWidget);
        cardSo.FindProperty("_button").objectReferenceValue      = btn;
        cardSo.FindProperty("_icon").objectReferenceValue        = iconImg;
        cardSo.FindProperty("_primaryText").objectReferenceValue = priTmp;
        cardSo.FindProperty("_secondaryText").objectReferenceValue = secTmp;
        cardSo.ApplyModifiedProperties();

        // Hide from scene hierarchy by placing it under the canvas as inactive
        go.SetActive(false);
        return go;
    }

    // ── Button builder helper ──────────────────────────────────────────────────

    static RectTransform BuildButton(
        string name, RectTransform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size,
        Sprite bgSprite, Sprite iconSprite,
        string primaryLabel, string secondaryLabel,
        TMP_FontAsset font,
        out Button btn, out Image iconImg,
        out TMP_Text primaryTmp, out TMP_Text secondaryTmp)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = size;

        // CardBG
        var bgRt = CreateRectTransform("CardBG", go.transform);
        StretchFill(bgRt);
        var bgImg = bgRt.gameObject.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type   = Image.Type.Simple;

        // IconArea (180×120, top-center, no mask — intentional overflow)
        var iconAreaRt = CreateRectTransform("IconArea", go.transform, new Vector2(180f, 120f));
        iconAreaRt.anchorMin = new Vector2(0.5f, 1f);
        iconAreaRt.anchorMax = new Vector2(0.5f, 1f);
        iconAreaRt.pivot     = new Vector2(0.5f, 1f);
        iconAreaRt.anchoredPosition = Vector2.zero;

        // Icon (stretch-stretch inside IconArea, insets 33,0,-33,0)
        var iconRt = CreateRectTransform("Icon", iconAreaRt);
        StretchFill(iconRt);
        iconRt.offsetMin = new Vector2(33f, 0f);
        iconRt.offsetMax = new Vector2(-33f, 0f);
        iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.sprite = iconSprite;
        iconImg.preserveAspect = true;
        iconImg.color = Color.white;

        // PrimaryText
        var priGo = new GameObject("PrimaryText");
        priGo.transform.SetParent(go.transform, false);
        var priRt = priGo.AddComponent<RectTransform>();
        priRt.anchorMin = new Vector2(0f, 0f);
        priRt.anchorMax = new Vector2(1f, 0f);
        priRt.pivot     = new Vector2(0.5f, 0f);
        priRt.anchoredPosition = new Vector2(0f, secondaryLabel != null ? 65f : 54f);
        priRt.sizeDelta = new Vector2(0f, 36f);
        primaryTmp = priGo.AddComponent<TextMeshProUGUI>();
        primaryTmp.text      = primaryLabel ?? "";
        primaryTmp.fontSize  = 30;
        primaryTmp.color     = Color.white;
        primaryTmp.alignment = TextAlignmentOptions.Center;
        primaryTmp.textWrappingMode = TextWrappingModes.Normal;  // needed for FADE/DRAW two-liner
        if (font != null) primaryTmp.font = font;

        // SecondaryText (only if needed)
        if (secondaryLabel != null)
        {
            var secGo = new GameObject("SecondaryText");
            secGo.transform.SetParent(go.transform, false);
            var secRt = secGo.AddComponent<RectTransform>();
            secRt.anchorMin = new Vector2(0f, 0f);
            secRt.anchorMax = new Vector2(1f, 0f);
            secRt.pivot     = new Vector2(0.5f, 0f);
            secRt.anchoredPosition = new Vector2(0f, 24f);
            secRt.sizeDelta = new Vector2(0f, 36f);
            secondaryTmp = secGo.AddComponent<TextMeshProUGUI>();
            secondaryTmp.text      = secondaryLabel;
            secondaryTmp.fontSize  = 30;
            secondaryTmp.color     = Color.white;
            secondaryTmp.alignment = TextAlignmentOptions.Center;
            secondaryTmp.richText  = true;
            if (font != null) secondaryTmp.font = font;
        }
        else
        {
            secondaryTmp = null;
        }

        // Transparent full-card button (hit area)
        var hitGo = new GameObject("HitArea");
        hitGo.transform.SetParent(go.transform, false);
        var hitRt = hitGo.AddComponent<RectTransform>();
        StretchFill(hitRt);
        var hitImg = hitGo.AddComponent<Image>();
        hitImg.color = new Color(0f, 0f, 0f, 0f);
        btn = go.AddComponent<Button>();
        btn.targetGraphic = hitImg;

        return rt;
    }

    // ── Coerce PNG to Sprite import type ──────────────────────────────────────

    static void CoerceSprite(string assetPath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null) return;  // already imported as Sprite

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[ActionButtonsBuilder] Could not get TextureImporter for {assetPath}");
            return;
        }
        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        AssetDatabase.Refresh();
        Debug.Log($"[ActionButtonsBuilder] Coerced {assetPath} to Sprite type.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static RectTransform CreateRectTransform(string name, Transform parent, Vector2 size = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (size != default(Vector2)) rt.sizeDelta = size;
        return rt;
    }

    static void RemoveChild(Transform parent, string childName)
    {
        var child = parent.Find(childName);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
            Debug.Log($"[ActionButtonsBuilder] Removed existing {childName}");
        }
    }

    static void SetAnchorCenter(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
    }

    static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) return c;
        return Color.white;
    }
}
