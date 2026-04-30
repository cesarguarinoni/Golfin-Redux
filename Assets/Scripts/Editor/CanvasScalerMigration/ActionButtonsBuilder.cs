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
/// per spec 8_5_action_buttons and 8_5_c_selector_redesign.
/// Menu: GOLFIN/Build/Build Action Buttons (8.5)
/// </summary>
public static class ActionButtonsBuilder
{
    // CONFIG SNAPSHOT (synced from Cesar's manual adjustments — update here if you change values in the scene):
    // IconArea width = 135, text width = 120, fontStyle = Bold, autoSize min=20 max=30
    // GolfinButton icon = S_Controls_Ball_GOLFIN, DriverButton icon = S_Menu_Driver_GOLFIN
    // Do NOT change these back to hardcoded fontSize=30 or width=0 or iconSprite=null.
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
        CoerceSprite("Assets/Art/In-Game UI/Icon - Up Arrow.png");
        CoerceSprite("Assets/Art/In-Game UI/Icon - Down Arrow.png");

        // ── Load sprites ───────────────────────────────────────────────────────
        Sprite btnAllSprite    = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Button - All.png");
        Sprite iconSpinSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Spin.png");
        Sprite iconFadeSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - DrawFade.png");
        Sprite iconStraSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Straight.png");
        Sprite iconUpArrow     = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Up Arrow.png");
        Sprite iconDownArrow   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Down Arrow.png");
        if (iconUpArrow   == null) Debug.LogWarning("[ActionButtonsBuilder] Icon - Up Arrow.png not found as Sprite");
        if (iconDownArrow == null) Debug.LogWarning("[ActionButtonsBuilder] Icon - Down Arrow.png not found as Sprite");

        // Default sprites for fallback (no managers active in LabScaffold)
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
        RemoveChild(canvasGo.transform, "SelectorOverlay_Ball");
        RemoveChild(canvasGo.transform, "SpinPanel");
        RemoveChild(canvasGo.transform, "OutsideClickCatcher_Selector");
        RemoveChild(canvasGo.transform, "OutsideClickCatcher_Selector_Ball");
        RemoveChild(canvasGo.transform, "OutsideClickCatcher_Spin");

        Color white     = Color.white;
        Color navyColor = HexToColor("001E39");

        // ══════════════════════════════════════════════════════════════════════
        // BUILD CARD PREFAB (built in-memory as a GO, saved as hidden prefab GO)
        // Each selector card is 145×240 (spec: 145×240).
        // ══════════════════════════════════════════════════════════════════════
        GameObject cardPrefabGo = BuildCardPrefabGo(btnAllSprite, rubikFont, white, navyColor);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD ActionButtons_Cluster
        // ══════════════════════════════════════════════════════════════════════
        var cluster = CreateRectTransform("ActionButtons_Cluster", canvasGo.transform);
        StretchFill(cluster);

        // Cluster root CanvasGroup (used by ActionButtonsRoot for shot-state disable)
        var clusterCg = cluster.gameObject.AddComponent<CanvasGroup>();
        var abRoot    = cluster.gameObject.AddComponent<ActionButtonsRoot>();

        var abRootSo = new SerializedObject(abRoot);
        abRootSo.FindProperty("_group").objectReferenceValue = clusterCg;
        abRootSo.ApplyModifiedProperties();

        // OtherButtonsFader lives on the cluster root
        var fader = cluster.gameObject.AddComponent<OtherButtonsFader>();

        // ── SPIN button (top-left, BL anchor) ─────────────────────────────────
        var spinBtnRt = BuildButton("SpinButton", cluster,
            anchorMin: Vector2.zero, anchorMax: Vector2.zero, pivot: Vector2.zero,
            anchoredPos: new Vector2(58f, 360f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: iconSpinSprite,
            primaryLabel: "SPIN", secondaryLabel: null,
            rubikFont, out var spinBtn, out var spinIconImg, out var spinPrimaryTmp, out var _);

        var spinWidget = spinBtnRt.gameObject.AddComponent<SpinButtonWidget>();
        // Per-button CanvasGroup for fader
        var spinCg = spinBtnRt.gameObject.AddComponent<CanvasGroup>();
        fader.RegisterGroup(spinCg);

        // ── FADE/DRAW button (top-right, BR anchor) ────────────────────────────
        var fadeBtnRt = BuildButton("FadeDrawButton", cluster,
            anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-58f, 360f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: iconStraSprite,
            primaryLabel: "STRAIGHT", secondaryLabel: null,
            rubikFont, out var fadeBtn, out var fadeIconImg, out var fadePrimaryTmp, out var _2);

        var fadeWidget = fadeBtnRt.gameObject.AddComponent<FadeDrawButtonWidget>();
        var fadeCg = fadeBtnRt.gameObject.AddComponent<CanvasGroup>();
        fader.RegisterGroup(fadeCg);

        // ── GOLFIN button (bottom-left, BL anchor) ────────────────────────────
        var ballBtnRt = BuildButton("GolfinButton", cluster,
            anchorMin: Vector2.zero, anchorMax: Vector2.zero, pivot: Vector2.zero,
            anchoredPos: new Vector2(58f, 96f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: defaultBallThumbnail,
            primaryLabel: "GOLFIN", secondaryLabel: "∞",
            rubikFont, out var ballBtn, out var ballIconImg, out var ballPrimaryTmp, out var ballSecTmp);

        var ballWidget = ballBtnRt.gameObject.AddComponent<BallButtonWidget>();
        var ballCg = ballBtnRt.gameObject.AddComponent<CanvasGroup>();
        fader.RegisterGroup(ballCg);

        // ── DRIVER button (bottom-right, BR anchor) ────────────────────────────
        var clubBtnRt = BuildButton("DriverButton", cluster,
            anchorMin: new Vector2(1f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(1f, 0f),
            anchoredPos: new Vector2(-58f, 96f), size: new Vector2(145f, 240f),
            bgSprite: btnAllSprite, iconSprite: defaultClubPortrait,
            primaryLabel: "DRIVER", secondaryLabel: "0 yrds",
            rubikFont, out var clubBtn, out var clubIconImg, out var clubPrimaryTmp, out var clubSecTmp);

        var clubWidget = clubBtnRt.gameObject.AddComponent<ClubButtonWidget>();
        var clubCg = clubBtnRt.gameObject.AddComponent<CanvasGroup>();
        fader.RegisterGroup(clubCg);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD OUTSIDE CLICK CATCHER for selector (full-screen transparent image)
        // ══════════════════════════════════════════════════════════════════════
        var selectorCatcherGo = new GameObject("OutsideClickCatcher_Selector");
        selectorCatcherGo.transform.SetParent(canvasGo.transform, false);
        var selectorCatcherRt = selectorCatcherGo.AddComponent<RectTransform>();
        StretchFill(selectorCatcherRt);
        var selectorCatcherImg = selectorCatcherGo.AddComponent<Image>();
        selectorCatcherImg.color = new Color(0f, 0f, 0f, 0f);
        var selectorCatcher = selectorCatcherGo.AddComponent<OutsideClickCatcher>();
        selectorCatcherGo.SetActive(false);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD SelectorOverlay (8.5.C redesign)
        // Stack: VLG spacing=34, childAlignment=LowerCenter
        // Arrows sit in 24px-padding containers, 8px gap between arrow container and cards.
        // Total layout (top to bottom):
        //   ArrowUpContainer (80×25 visible + 24px padding → 128×73 total)
        //   8px gap
        //   CardsContainer (content-size-fitter, VLG spacing=34)
        //   8px gap
        //   ArrowDownContainer (128×73 total)
        //
        // The overlay root is auto-sized by a VerticalLayoutGroup that wraps all three.
        // ══════════════════════════════════════════════════════════════════════

        var overlayGo = new GameObject("SelectorOverlay");
        overlayGo.transform.SetParent(canvasGo.transform, false);
        var overlayRt = overlayGo.AddComponent<RectTransform>();
        // Positioned for clubs (right side). Pivot=(1,0) means bottom-right corner is anchored.
        // The selected card (bottom card) bottom edge sits at y=96 (same as DriverButton bottom).
        overlayRt.anchorMin = overlayRt.anchorMax = new Vector2(1f, 0f);
        overlayRt.pivot     = new Vector2(1f, 0f);
        // 48px gap to LEFT of DriverButton. DriverButton left edge = 58+145=203px from right.
        // Y=28 so ArrowDown bottom=28, gap 8, DRIVER card bottom=96 (matches DriverButton bottom).
        overlayRt.anchoredPosition = new Vector2(-251f, 28f);
        // Width = card width. Height grows via ContentSizeFitter on the overlay root VLG.
        overlayRt.sizeDelta = new Vector2(145f, 0f);

        // Overlay root VLG stacks: ArrowUp, CardsContainer, ArrowDown
        var overlayVlg = overlayGo.AddComponent<VerticalLayoutGroup>();
        overlayVlg.spacing              = 8f;    // 8px gap between arrow containers and cards
        overlayVlg.childAlignment       = TextAnchor.LowerCenter;
        overlayVlg.childForceExpandWidth  = true;
        overlayVlg.childForceExpandHeight = false;
        overlayVlg.childControlWidth      = true;
        overlayVlg.childControlHeight     = true;
        overlayVlg.padding = new RectOffset(0, 0, 0, 0);

        var overlayCsf = overlayGo.AddComponent<ContentSizeFitter>();
        overlayCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        overlayCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── ArrowUp container (24px padding, chevron 80×25 inside) ──────────
        var arrowUpContainerRt = CreateRectTransform("ArrowUpContainer", overlayGo.transform);
        var arrowUpContainerImg = arrowUpContainerRt.gameObject.AddComponent<Image>();
        arrowUpContainerImg.color = new Color(0f, 0f, 0f, 0f);  // transparent hit area
        // LayoutElement: preferred height = 25 + 24*2 = 73
        var arrowUpContainerLe = arrowUpContainerRt.gameObject.AddComponent<LayoutElement>();
        arrowUpContainerLe.preferredHeight = 60f;
        arrowUpContainerLe.flexibleWidth   = 1f;

        // Chevron image inside (80×25, centered).
        // Icon - Straight.png points UP. preserveAspect=false fills the 80×25 rect (wide flat chevron).
        // No rotation needed — icon already points up.
        var arrowUpImgRt = CreateRectTransform("ArrowUpChevron", arrowUpContainerRt, new Vector2(80f, 50f));
        SetAnchorCenter(arrowUpImgRt);
        arrowUpImgRt.anchoredPosition = Vector2.zero;
        var arrowUpImg = arrowUpImgRt.gameObject.AddComponent<Image>();
        arrowUpImg.sprite = iconUpArrow;
        arrowUpImg.preserveAspect = true;
        arrowUpImg.color = Color.white;

        var arrowUpBtn = arrowUpContainerRt.gameObject.AddComponent<Button>();
        arrowUpBtn.targetGraphic = arrowUpContainerImg;

        // ── CardsContainer (VerticalLayoutGroup spacing=34, LowerCenter) ─────
        var cardsContainerRt = CreateRectTransform("CardsContainer", overlayGo.transform);
        var vlg = cardsContainerRt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing              = 34f;
        vlg.childAlignment       = TextAnchor.LowerCenter;
        vlg.childForceExpandWidth  = true;   // stretch cards to container width (145)
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth      = true;
        vlg.childControlHeight     = false;
        var csf = cardsContainerRt.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var cardsLe = cardsContainerRt.gameObject.AddComponent<LayoutElement>();
        cardsLe.flexibleWidth = 1f;

        // ── ArrowDown container ────────────────────────────────────────────────
        var arrowDownContainerRt = CreateRectTransform("ArrowDownContainer", overlayGo.transform);
        var arrowDownContainerImg = arrowDownContainerRt.gameObject.AddComponent<Image>();
        arrowDownContainerImg.color = new Color(0f, 0f, 0f, 0f);
        var arrowDownContainerLe = arrowDownContainerRt.gameObject.AddComponent<LayoutElement>();
        arrowDownContainerLe.preferredHeight = 60f;
        arrowDownContainerLe.flexibleWidth   = 1f;

        // Icon - Straight.png points UP; rotate 180° so ArrowDown points down.
        var arrowDownImgRt = CreateRectTransform("ArrowDownChevron", arrowDownContainerRt, new Vector2(80f, 50f));
        SetAnchorCenter(arrowDownImgRt);
        arrowDownImgRt.anchoredPosition = Vector2.zero;
        var arrowDownImg = arrowDownImgRt.gameObject.AddComponent<Image>();
        arrowDownImg.sprite = iconDownArrow;
        arrowDownImg.preserveAspect = true;
        arrowDownImg.color = Color.white;

        var arrowDownBtn = arrowDownContainerRt.gameObject.AddComponent<Button>();
        arrowDownBtn.targetGraphic = arrowDownContainerImg;

        // ── Wire SelectorOverlayWidget ──────────────────────────────────────
        var overlayWidget = overlayGo.AddComponent<SelectorOverlayWidget>();
        var overlaySo = new SerializedObject(overlayWidget);
        overlaySo.FindProperty("_root").objectReferenceValue               = overlayRt;
        overlaySo.FindProperty("_cardsContainer").objectReferenceValue     = cardsContainerRt;
        overlaySo.FindProperty("_cardPrefab").objectReferenceValue         = cardPrefabGo;
        overlaySo.FindProperty("_arrowUpContainer").objectReferenceValue   = arrowUpContainerRt;
        overlaySo.FindProperty("_arrowDownContainer").objectReferenceValue = arrowDownContainerRt;
        overlaySo.FindProperty("_arrowUp").objectReferenceValue            = arrowUpBtn;
        overlaySo.FindProperty("_arrowDown").objectReferenceValue          = arrowDownBtn;
        overlaySo.FindProperty("_outsideClickCatcher").objectReferenceValue = selectorCatcher;
        // Clubs: pivot=(1,0), position aligned to DriverButton bottom edge
        overlaySo.FindProperty("_anchoredPositionForClub").vector2Value    = new Vector2(-251f, 28f);
        overlaySo.FindProperty("_anchoredPositionForBall").vector2Value    = new Vector2(251f, 28f);
        overlaySo.ApplyModifiedProperties();

        overlayGo.SetActive(false);

        // ══════════════════════════════════════════════════════════════════════
        // BUILD SPIN PANEL
        // ══════════════════════════════════════════════════════════════════════

        var spinCatcherGo = new GameObject("OutsideClickCatcher_Spin");
        spinCatcherGo.transform.SetParent(canvasGo.transform, false);
        var spinCatcherRt = spinCatcherGo.AddComponent<RectTransform>();
        StretchFill(spinCatcherRt);
        var spinCatcherImg = spinCatcherGo.AddComponent<Image>();
        spinCatcherImg.color = new Color(0f, 0f, 0f, 0.5f);
        var spinCatcher = spinCatcherGo.AddComponent<OutsideClickCatcher>();
        spinCatcherGo.SetActive(false);

        var spinPanelGo = new GameObject("SpinPanel");
        spinPanelGo.transform.SetParent(canvasGo.transform, false);
        var spinPanelRt = spinPanelGo.AddComponent<RectTransform>();
        StretchFill(spinPanelRt);

        var ballImgRt = CreateRectTransform("BallImage", spinPanelGo.transform, new Vector2(600f, 600f));
        SetAnchorCenter(ballImgRt);
        ballImgRt.anchoredPosition = Vector2.zero;
        var ballImg = ballImgRt.gameObject.AddComponent<Image>();
        ballImg.preserveAspect = true;
        ballImg.color = white;

        var dotRt = CreateRectTransform("SpinDot", ballImgRt);
        dotRt.sizeDelta = new Vector2(60f, 60f);
        SetAnchorCenter(dotRt);
        dotRt.anchoredPosition = Vector2.zero;
        var dotImg = dotRt.gameObject.AddComponent<Image>();
        dotImg.color = new Color(1f, 0.2f, 0.2f, 1f);

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
            posImg.color = new Color(0f, 0f, 0f, 0f);
            var posBtn = posRt.gameObject.AddComponent<Button>();
            posBtn.targetGraphic = posImg;
            int captured = i;
            posBtn.onClick.AddListener(() => spinPanelWidget.SelectPosition(captured));
        }

        var coneRootT = canvasGo.transform.Find("ConeRoot");
        var coneRoot  = coneRootT != null ? coneRootT.gameObject : null;
        if (coneRoot == null) Debug.LogWarning("[ActionButtonsBuilder] ConeRoot not found — _aimingCone will be null on SpinPanelWidget.");

        var spinPanelSo = new SerializedObject(spinPanelWidget);
        spinPanelSo.FindProperty("_ballImage").objectReferenceValue        = ballImg;
        spinPanelSo.FindProperty("_spinDot").objectReferenceValue          = dotRt;
        spinPanelSo.FindProperty("_dimBackground").objectReferenceValue    = spinCatcher;
        spinPanelSo.FindProperty("_defaultBallSprite").objectReferenceValue = defaultBallThumbnail;
        spinPanelSo.FindProperty("_aimingCone").objectReferenceValue       = coneRoot;
        spinPanelSo.ApplyModifiedProperties();

        spinCatcherGo.SetActive(false);
        spinPanelGo.SetActive(false);

        // ── Wire SpinButtonWidget ──────────────────────────────────────────────
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

        // ── Wire BallButtonWidget ──────────────────────────────────────────────
        var ballBtnSo = new SerializedObject(ballWidget);
        ballBtnSo.FindProperty("_button").objectReferenceValue            = ballBtn;
        ballBtnSo.FindProperty("_iconImage").objectReferenceValue         = ballIconImg;
        ballBtnSo.FindProperty("_primaryText").objectReferenceValue       = ballPrimaryTmp;
        ballBtnSo.FindProperty("_secondaryText").objectReferenceValue     = ballSecTmp;
        ballBtnSo.FindProperty("_selectorOverlay").objectReferenceValue   = overlayWidget;
        ballBtnSo.FindProperty("_defaultThumbnail").objectReferenceValue  = defaultBallThumbnail;
        ballBtnSo.ApplyModifiedProperties();

        // ── Wire ClubButtonWidget ──────────────────────────────────────────────
        var clubBtnSo = new SerializedObject(clubWidget);
        clubBtnSo.FindProperty("_button").objectReferenceValue            = clubBtn;
        clubBtnSo.FindProperty("_iconImage").objectReferenceValue         = clubIconImg;
        clubBtnSo.FindProperty("_primaryText").objectReferenceValue       = clubPrimaryTmp;
        clubBtnSo.FindProperty("_secondaryText").objectReferenceValue     = clubSecTmp;
        clubBtnSo.FindProperty("_selectorOverlay").objectReferenceValue   = overlayWidget;
        clubBtnSo.FindProperty("_defaultPortrait").objectReferenceValue   = defaultClubPortrait;
        clubBtnSo.ApplyModifiedProperties();

        // ── Add SelectorDragRouter to DriverButton and GolfinButton ───────────

        // Driver (Club selector)
        var clubDragRouter = clubBtnRt.gameObject.AddComponent<SelectorDragRouter>();
        var clubRouterSo = new SerializedObject(clubDragRouter);
        clubRouterSo.FindProperty("_selectorOverlay").objectReferenceValue = overlayWidget;
        clubRouterSo.FindProperty("_fader").objectReferenceValue           = fader;
        clubRouterSo.FindProperty("_myCanvasGroup").objectReferenceValue   = clubCg;
        clubRouterSo.ApplyModifiedProperties();

        // Golfin/Ball selector — needs its own overlay for balls
        // Per spec: same overlay widget handles both kinds. Ball selector opens with Kind.Ball.
        // We build a second overlay for balls (left side).
        // Actually, spec says "Golfin selector (left side): mirror". They share the single overlay widget
        // but it repositions on Open(). For hold-mode, the router needs the overlay to know the kind.
        // The overlay infers kind from its pivot config: pivot.x<0.5 = Ball.
        // Solution: build a SECOND SelectorOverlay for balls with pivot=(0,0).
        var overlayGoBall = new GameObject("SelectorOverlay_Ball");
        overlayGoBall.transform.SetParent(canvasGo.transform, false);
        var overlayRtBall = overlayGoBall.AddComponent<RectTransform>();
        overlayRtBall.anchorMin = overlayRtBall.anchorMax = new Vector2(0f, 0f);
        overlayRtBall.pivot     = new Vector2(0f, 0f);
        // 48px gap to RIGHT of GolfinButton. Y=28 matches DRIVER card to button bottom.
        overlayRtBall.anchoredPosition = new Vector2(251f, 28f);
        overlayRtBall.sizeDelta = new Vector2(145f, 0f);

        var overlayVlgBall = overlayGoBall.AddComponent<VerticalLayoutGroup>();
        overlayVlgBall.spacing              = 8f;
        overlayVlgBall.childAlignment       = TextAnchor.LowerCenter;
        overlayVlgBall.childForceExpandWidth  = true;
        overlayVlgBall.childForceExpandHeight = false;
        overlayVlgBall.childControlWidth      = true;
        overlayVlgBall.childControlHeight     = true;

        var overlayCsfBall = overlayGoBall.AddComponent<ContentSizeFitter>();
        overlayCsfBall.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        overlayCsfBall.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ArrowUp container for ball overlay
        var arrowUpContBallRt = CreateRectTransform("ArrowUpContainer", overlayGoBall.transform);
        var arrowUpContBallImg = arrowUpContBallRt.gameObject.AddComponent<Image>();
        arrowUpContBallImg.color = new Color(0f, 0f, 0f, 0f);
        var arrowUpContBallLe = arrowUpContBallRt.gameObject.AddComponent<LayoutElement>();
        arrowUpContBallLe.preferredHeight = 73f;
        arrowUpContBallLe.flexibleWidth   = 1f;
        var arrowUpImgBallRt = CreateRectTransform("ArrowUpChevron", arrowUpContBallRt, new Vector2(80f, 50f));
        SetAnchorCenter(arrowUpImgBallRt);
        arrowUpImgBallRt.anchoredPosition = Vector2.zero;
        var arrowUpImgBall = arrowUpImgBallRt.gameObject.AddComponent<Image>();
        arrowUpImgBall.sprite = iconUpArrow;
        arrowUpImgBall.preserveAspect = true;
        arrowUpImgBall.color = Color.white;
        var arrowUpBtnBall = arrowUpContBallRt.gameObject.AddComponent<Button>();
        arrowUpBtnBall.targetGraphic = arrowUpContBallImg;

        // CardsContainer for ball overlay
        var cardsContBallRt = CreateRectTransform("CardsContainer", overlayGoBall.transform);
        var vlgBall = cardsContBallRt.gameObject.AddComponent<VerticalLayoutGroup>();
        vlgBall.spacing              = 34f;
        vlgBall.childAlignment       = TextAnchor.LowerCenter;
        vlgBall.childForceExpandWidth  = true;   // stretch cards to container width
        vlgBall.childForceExpandHeight = false;
        vlgBall.childControlWidth      = true;
        vlgBall.childControlHeight     = false;
        var csfBall = cardsContBallRt.gameObject.AddComponent<ContentSizeFitter>();
        csfBall.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var cardsLeBall = cardsContBallRt.gameObject.AddComponent<LayoutElement>();
        cardsLeBall.flexibleWidth = 1f;

        // ArrowDown container for ball overlay
        var arrowDownContBallRt = CreateRectTransform("ArrowDownContainer", overlayGoBall.transform);
        var arrowDownContBallImg = arrowDownContBallRt.gameObject.AddComponent<Image>();
        arrowDownContBallImg.color = new Color(0f, 0f, 0f, 0f);
        var arrowDownContBallLe = arrowDownContBallRt.gameObject.AddComponent<LayoutElement>();
        arrowDownContBallLe.preferredHeight = 73f;
        arrowDownContBallLe.flexibleWidth   = 1f;
        var arrowDownImgBallRt = CreateRectTransform("ArrowDownChevron", arrowDownContBallRt, new Vector2(80f, 50f));
        SetAnchorCenter(arrowDownImgBallRt);
        arrowDownImgBallRt.anchoredPosition = Vector2.zero;
        var arrowDownImgBall = arrowDownImgBallRt.gameObject.AddComponent<Image>();
        arrowDownImgBall.sprite = iconDownArrow;
        arrowDownImgBall.preserveAspect = true;
        arrowDownImgBall.color = Color.white;
        var arrowDownBtnBall = arrowDownContBallRt.gameObject.AddComponent<Button>();
        arrowDownBtnBall.targetGraphic = arrowDownContBallImg;

        // Build a second OutsideClickCatcher for ball overlay
        var selectorCatcherBallGo = new GameObject("OutsideClickCatcher_Selector_Ball");
        selectorCatcherBallGo.transform.SetParent(canvasGo.transform, false);
        var selectorCatcherBallRt = selectorCatcherBallGo.AddComponent<RectTransform>();
        StretchFill(selectorCatcherBallRt);
        var selectorCatcherBallImg = selectorCatcherBallGo.AddComponent<Image>();
        selectorCatcherBallImg.color = new Color(0f, 0f, 0f, 0f);
        var selectorCatcherBall = selectorCatcherBallGo.AddComponent<OutsideClickCatcher>();
        selectorCatcherBallGo.SetActive(false);

        var overlayWidgetBall = overlayGoBall.AddComponent<SelectorOverlayWidget>();
        var overlaySoBall = new SerializedObject(overlayWidgetBall);
        overlaySoBall.FindProperty("_root").objectReferenceValue               = overlayRtBall;
        overlaySoBall.FindProperty("_cardsContainer").objectReferenceValue     = cardsContBallRt;
        overlaySoBall.FindProperty("_cardPrefab").objectReferenceValue         = cardPrefabGo;
        overlaySoBall.FindProperty("_arrowUpContainer").objectReferenceValue   = arrowUpContBallRt;
        overlaySoBall.FindProperty("_arrowDownContainer").objectReferenceValue = arrowDownContBallRt;
        overlaySoBall.FindProperty("_arrowUp").objectReferenceValue            = arrowUpBtnBall;
        overlaySoBall.FindProperty("_arrowDown").objectReferenceValue          = arrowDownBtnBall;
        overlaySoBall.FindProperty("_outsideClickCatcher").objectReferenceValue = selectorCatcherBall;
        overlaySoBall.FindProperty("_anchoredPositionForClub").vector2Value    = new Vector2(-251f, 96f);
        overlaySoBall.FindProperty("_anchoredPositionForBall").vector2Value    = new Vector2(251f, 96f);
        overlaySoBall.ApplyModifiedProperties();

        overlayGoBall.SetActive(false);

        // Wire BallButtonWidget to ball overlay instead
        var ballBtnSo2 = new SerializedObject(ballWidget);
        ballBtnSo2.FindProperty("_selectorOverlay").objectReferenceValue = overlayWidgetBall;
        ballBtnSo2.ApplyModifiedProperties();

        // Golfin drag router
        var ballDragRouter = ballBtnRt.gameObject.AddComponent<SelectorDragRouter>();
        var ballRouterSo = new SerializedObject(ballDragRouter);
        ballRouterSo.FindProperty("_selectorOverlay").objectReferenceValue = overlayWidgetBall;
        ballRouterSo.FindProperty("_fader").objectReferenceValue           = fader;
        ballRouterSo.FindProperty("_myCanvasGroup").objectReferenceValue   = ballCg;
        ballRouterSo.ApplyModifiedProperties();

        // ── Add populators to LabRoot ──────────────────────────────────────────
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
            Debug.LogWarning("[ActionButtonsBuilder] LabRoot not found — populators not added.");
        }

        // ── Mark scene dirty and save ──────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[ActionButtonsBuilder] DONE — ActionButtons_Cluster (8.5.C redesign), SelectorOverlay x2, SpinPanel built and wired in LabScaffold.unity.");
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
        var go = new GameObject("SelectorCard_Prefab");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(145f, 240f);

        // CardBG
        var bgRt = CreateRectTransform("CardBG", go.transform);
        StretchFill(bgRt);
        var bgImg = bgRt.gameObject.AddComponent<Image>();
        bgImg.sprite = bgSprite;
        bgImg.type   = Image.Type.Simple;

        // IconArea (135×120, top-center, overflow allowed)
        var iconAreaRt = CreateRectTransform("IconArea", go.transform, new Vector2(135f, 120f));
        iconAreaRt.anchorMin = new Vector2(0.5f, 1f);
        iconAreaRt.anchorMax = new Vector2(0.5f, 1f);
        iconAreaRt.pivot     = new Vector2(0.5f, 1f);
        iconAreaRt.anchoredPosition = Vector2.zero;

        // Icon (stretch inside IconArea, insets 33)
        var iconRt = CreateRectTransform("Icon", iconAreaRt);
        StretchFill(iconRt);
        iconRt.offsetMin = new Vector2(33f, 0f);
        iconRt.offsetMax = new Vector2(-33f, 0f);
        var iconImg = iconRt.gameObject.AddComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.color = white;

        // PrimaryText (bottom-anchored, center anchor so width=120 takes effect)
        var priGo = new GameObject("PrimaryText");
        priGo.transform.SetParent(go.transform, false);
        var priRt = priGo.AddComponent<RectTransform>();
        priRt.anchorMin = new Vector2(0.5f, 0f);
        priRt.anchorMax = new Vector2(0.5f, 0f);
        priRt.pivot     = new Vector2(0.5f, 0f);
        priRt.anchoredPosition = new Vector2(0f, 65f);
        priRt.sizeDelta = new Vector2(120f, 36f);
        var priTmp = priGo.AddComponent<TextMeshProUGUI>();
        priTmp.enableAutoSizing = true; priTmp.fontSizeMin = 20f; priTmp.fontSizeMax = 30f;
        priTmp.fontStyle = FontStyles.Bold;
        priTmp.color     = white;
        priTmp.alignment = TextAlignmentOptions.Center;
        priTmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (font != null) priTmp.font = font;

        // SecondaryText (center anchor so width=120 takes effect)
        var secGo = new GameObject("SecondaryText");
        secGo.transform.SetParent(go.transform, false);
        var secRt = secGo.AddComponent<RectTransform>();
        secRt.anchorMin = new Vector2(0.5f, 0f);
        secRt.anchorMax = new Vector2(0.5f, 0f);
        secRt.pivot     = new Vector2(0.5f, 0f);
        secRt.anchoredPosition = new Vector2(0f, 24f);
        secRt.sizeDelta = new Vector2(120f, 36f);
        var secTmp = secGo.AddComponent<TextMeshProUGUI>();
        secTmp.enableAutoSizing = true; secTmp.fontSizeMin = 20f; secTmp.fontSizeMax = 30f;
        secTmp.fontStyle = FontStyles.Bold;
        secTmp.color     = white;
        secTmp.alignment = TextAlignmentOptions.Center;
        secTmp.richText  = true;
        if (font != null) secTmp.font = font;

        // Transparent button hit area
        var btnImgGo = new GameObject("BtnBackground");
        btnImgGo.transform.SetParent(go.transform, false);
        var btnImgRt = btnImgGo.AddComponent<RectTransform>();
        StretchFill(btnImgRt);
        var btnImg = btnImgGo.AddComponent<Image>();
        btnImg.color = new Color(0f, 0f, 0f, 0f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = btnImg;

        // Wire SelectorCardWidget
        var cardWidget = go.AddComponent<SelectorCardWidget>();
        var cardSo = new SerializedObject(cardWidget);
        cardSo.FindProperty("_button").objectReferenceValue        = btn;
        cardSo.FindProperty("_icon").objectReferenceValue          = iconImg;
        cardSo.FindProperty("_primaryText").objectReferenceValue   = priTmp;
        cardSo.FindProperty("_secondaryText").objectReferenceValue = secTmp;
        cardSo.ApplyModifiedProperties();

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

        // IconArea (width=135 per Cesar's config)
        var iconAreaRt = CreateRectTransform("IconArea", go.transform, new Vector2(135f, 120f));
        iconAreaRt.anchorMin = new Vector2(0.5f, 1f);
        iconAreaRt.anchorMax = new Vector2(0.5f, 1f);
        iconAreaRt.pivot     = new Vector2(0.5f, 1f);
        iconAreaRt.anchoredPosition = Vector2.zero;

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
        // Fixed width=120 requires center anchor (not stretch) — sizeDelta.x is ignored on stretch anchors
        priRt.anchorMin = new Vector2(0.5f, 0f);
        priRt.anchorMax = new Vector2(0.5f, 0f);
        priRt.pivot     = new Vector2(0.5f, 0f);
        priRt.anchoredPosition = new Vector2(0f, secondaryLabel != null ? 65f : 54f);
        priRt.sizeDelta = new Vector2(120f, 36f);
        primaryTmp = priGo.AddComponent<TextMeshProUGUI>();
        primaryTmp.text      = primaryLabel ?? "";
        primaryTmp.enableAutoSizing = true; primaryTmp.fontSizeMin = 20f; primaryTmp.fontSizeMax = 30f;
        primaryTmp.fontStyle = FontStyles.Bold;
        primaryTmp.color     = Color.white;
        primaryTmp.alignment = TextAlignmentOptions.Center;
        primaryTmp.textWrappingMode = TextWrappingModes.Normal;
        if (font != null) primaryTmp.font = font;

        // SecondaryText
        if (secondaryLabel != null)
        {
            var secGo = new GameObject("SecondaryText");
            secGo.transform.SetParent(go.transform, false);
            var secRt = secGo.AddComponent<RectTransform>();
            // Fixed width=120 requires center anchor (not stretch)
            secRt.anchorMin = new Vector2(0.5f, 0f);
            secRt.anchorMax = new Vector2(0.5f, 0f);
            secRt.pivot     = new Vector2(0.5f, 0f);
            secRt.anchoredPosition = new Vector2(0f, 24f);
            secRt.sizeDelta = new Vector2(120f, 36f);
            secondaryTmp = secGo.AddComponent<TextMeshProUGUI>();
            secondaryTmp.text      = secondaryLabel;
            secondaryTmp.enableAutoSizing = true; secondaryTmp.fontSizeMin = 20f; secondaryTmp.fontSizeMax = 30f;
            secondaryTmp.fontStyle = FontStyles.Bold;
            secondaryTmp.color     = Color.white;
            secondaryTmp.alignment = TextAlignmentOptions.Center;
            secondaryTmp.richText  = true;
            if (font != null) secondaryTmp.font = font;
        }
        else
        {
            secondaryTmp = null;
        }

        // Hit area button
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    static void CoerceSprite(string assetPath)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (sprite != null) return;

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
