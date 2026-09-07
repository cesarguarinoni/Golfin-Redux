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
    // ── Selector carousel geometry (selector_carousel §1) ──────────────────────
    // The viewport is a FIXED window, not a content-sized stack: four card slots plus a 36px
    // dead margin top and bottom. The margins are what let the focus card's halo (312 tall vs
    // the card's 240) glow past the card without the RectMask2D shearing it off, while still
    // clipping the two buffer cards the pool keeps just outside the window.
    const float SelectorCardHeight     = 240f;
    const float SelectorCardGap        = 34f;
    const int   SelectorVisibleSlots   = 4;
    /// <summary>
    /// Dead space above and below the four slots. Bounded on BOTH sides:
    ///   lower bound — the halo glow hangs (haloH - cardArtH)/2 ~= 29.4px below the focus card, so
    ///                 a smaller margin would shear the glow off;
    ///   upper bound — the buffer card's edge sits <see cref="SelectorCardGap"/> = 34px beyond the
    ///                 focus slot, so a margin of 34+ lets a sliver of it through the mask. At 36
    ///                 exactly 2px showed at both ends, which is the thin white line Cesar spotted
    ///                 over the bottom chevron ("since it's infinite now, remove it").
    /// 28 sits inside [29.4-ish, 34) with headroom at both ends.
    /// </summary>
    const float SelectorViewportMargin = 28f;
    const float SelectorSlotPitch      = SelectorCardHeight + SelectorCardGap;                 // 274
    const float SelectorViewportHeight = SelectorVisibleSlots * SelectorCardHeight
                                       + (SelectorVisibleSlots - 1) * SelectorCardGap
                                       + 2f * SelectorViewportMargin;                          // 1134
    const float SelectorCardWidth      = 145f;
    // ── Halo geometry: match the card's DRAWN ART, not its RectTransform ───────
    //
    // Two sprites both have transparent padding, and BOTH matter:
    //
    //   Halo - Selector.png  217x312, ring bbox (32,32)-(185,280) => ring 154x249, centred in the
    //                        sprite (insets 32/31 both axes). The rest is glow falloff.
    //   Button - All.png     153x248, card art bbox (4,0)-(148,239) => art 145x240 with 4px
    //                        padding left/right, NONE at the top and 8px at the BOTTOM.
    //
    // Stretched into the 145x240 card rect, that bottom-only padding means the DRAWN card is
    // 137.4 x 232.3 and its centre sits ~4.3px ABOVE the rect centre. selector_carousel iter-2
    // sized and pinned the halo to the RECT, so it rendered both too large and visibly low —
    // Cesar: "still bigger than the club/ball portrait and is not centered (clearly more empty
    // space at the bottom than at the top)". Everything below is derived from those two measured
    // bboxes so re-exported art only needs the constants updated.
    const float CardSpriteW = 153f, CardSpriteH = 248f;
    const float CardArtL = 4f, CardArtR = 4f, CardArtT = 0f, CardArtB = 8f;

    /// <summary>Drawn card art size inside the 145x240 card rect.</summary>
    const float CardArtWidth  = SelectorCardWidth  * (CardSpriteW - CardArtL - CardArtR) / CardSpriteW;   // 137.42
    const float CardArtHeight = SelectorCardHeight * (CardSpriteH - CardArtT - CardArtB) / CardSpriteH;   // 232.26

    /// <summary>Centre of the drawn card art, measured from the card rect's bottom-left.</summary>
    const float CardArtCentreX = SelectorCardWidth  * (CardArtL + (CardSpriteW - CardArtL - CardArtR) * 0.5f) / CardSpriteW;                 // 72.03
    const float CardArtCentreY = SelectorCardHeight * (CardArtB + (CardSpriteH - CardArtT - CardArtB) * 0.5f) / CardSpriteH;                 // 124.35

    const float SelectorHaloRingWidth  = 154f;
    const float SelectorHaloRingHeight = 249f;
    /// <summary>Sprite size that puts the halo's RING exactly on the drawn card art.</summary>
    const float SelectorHaloWidth      = 217f * (CardArtWidth  / SelectorHaloRingWidth);    // 193.62
    const float SelectorHaloHeight     = 312f * (CardArtHeight / SelectorHaloRingHeight);   // 291.03
    const string SelectorHaloPath      = "Assets/Art/In-Game UI/Halo - Selector.png";
    const string SelectorHaloTintHex   = "FCF195";   // §D7 selected-state gold

    /// <summary>
    /// The canvas y the FOCUS SLOT's bottom edge is authored at — the same y the trigger buttons'
    /// bottom edge sits at (<c>ShotLayoutController.AuthoredClusterBaselinePx</c>). This is a
    /// BASELINE, not a root position: <c>SelectorOverlayWidget.PositionRoot</c> drops the root by
    /// the chevron + gap + viewport margin beneath the focus slot, so the selected card lines up
    /// with its button in the authored frame AND at runtime.
    /// </summary>
    const float SelectorOverlayAuthoredY = 96f;

    // CONFIG SNAPSHOT (synced from Cesar's manual adjustments — update here if you change values in the scene):
    // IconArea width = 135, text width = 120, fontStyle = Bold, autoSize min=20 max=30
    // GolfinButton icon = S_Controls_Ball_GOLFIN, DriverButton icon = S_Menu_Driver_GOLFIN
    // Do NOT change these back to hardcoded fontSize=30 or width=0 or iconSprite=null.
    //
    // THE ROW Ys BELOW (96 / 360) ARE NOT THE SHIPPED POSITIONS ANY MORE. shot_view_layout D2
    // put the bottom row, both selector overlays and the pull lane's end on ONE baseline —
    // ControlsConfig.BottomBaselinePx (170, raised further on a device with a deeper safe-area
    // inset) — and ShotLayoutController applies the difference to the whole full-stretch
    // cluster at runtime. Keep authoring 96/360 here: the delta is measured FROM them, so
    // changing them moves the buttons relative to the baseline rather than moving the baseline.
    // To move the baseline itself, edit BottomBaselinePx (and its controls.csv mirror).
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
        CoerceHaloSprite(SelectorHaloPath);

        // ── Load sprites ───────────────────────────────────────────────────────
        Sprite btnAllSprite    = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Button - All.png");
        Sprite iconSpinSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Spin.png");
        Sprite iconFadeSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - DrawFade.png");
        Sprite iconStraSprite  = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Straight.png");
        Sprite iconUpArrow     = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Up Arrow.png");
        Sprite iconDownArrow   = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Down Arrow.png");
        Sprite haloSprite      = AssetDatabase.LoadAssetAtPath<Sprite>(SelectorHaloPath);
        if (haloSprite == null) Debug.LogWarning($"[ActionButtonsBuilder] {SelectorHaloPath} not found as Sprite — focus halo will be blank.");
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

        // SelectorCard_Prefab is a SCENE ROOT, not a canvas child, so the RemoveChild sweep above
        // never touched it and every past run of this builder leaked one more copy into
        // LabScaffold (14 of them by 2026-09-07). Sweep the roots too, or the scene keeps growing
        // by one orphaned card hierarchy per rebuild.
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "SelectorCard_Prefab")
                Object.DestroyImmediate(root);

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
        // The widget re-derives this from the baseline every open; the authored value just keeps
        // the inactive object sane in the scene file. Clubs: chevron 60 + VLG gap 8 + margin 36.
        overlayRt.anchoredPosition = new Vector2(-251f, SelectorOverlayAuthoredY - (60f + 8f + SelectorViewportMargin));
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

        // ── CardsContainer — the carousel VIEWPORT (selector_carousel §1) ────
        var cardsContainerRt = BuildSelectorViewport(overlayGo.transform, haloSprite,
                                                     out RectTransform focusHaloRt,
                                                     out SelectorCarouselDrag carouselDrag);

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
        overlaySo.FindProperty("_cardsViewport").objectReferenceValue       = cardsContainerRt;
        overlaySo.FindProperty("_focusHalo").objectReferenceValue           = focusHaloRt;
        overlaySo.FindProperty("_carouselDrag").objectReferenceValue        = carouselDrag;
        overlaySo.FindProperty("_slotPitch").floatValue                     = SelectorSlotPitch;
        overlaySo.FindProperty("_visibleSlots").intValue                    = SelectorVisibleSlots;
        overlaySo.FindProperty("_viewportMargin").floatValue                = SelectorViewportMargin;
        overlaySo.FindProperty("_cardPrefab").objectReferenceValue         = cardPrefabGo;
        overlaySo.FindProperty("_arrowUpContainer").objectReferenceValue   = arrowUpContainerRt;
        overlaySo.FindProperty("_arrowDownContainer").objectReferenceValue = arrowDownContainerRt;
        overlaySo.FindProperty("_arrowUp").objectReferenceValue            = arrowUpBtn;
        overlaySo.FindProperty("_arrowDown").objectReferenceValue          = arrowDownBtn;
        overlaySo.FindProperty("_outsideClickCatcher").objectReferenceValue = selectorCatcher;
        // Clubs: pivot=(1,0), position aligned to DriverButton bottom edge (less the viewport margin)
        overlaySo.FindProperty("_anchoredPositionForClub").vector2Value    = new Vector2(-251f, SelectorOverlayAuthoredY);
        overlaySo.FindProperty("_anchoredPositionForBall").vector2Value    = new Vector2(251f, SelectorOverlayAuthoredY);
        overlaySo.ApplyModifiedProperties();

        // The drag component finds its overlay in Awake as a fallback, but wire it explicitly:
        // a serialized reference is what a reviewer can read back off the scene.
        var clubDragSo = new SerializedObject(carouselDrag);
        clubDragSo.FindProperty("_overlay").objectReferenceValue = overlayWidget;
        clubDragSo.ApplyModifiedProperties();

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

        // ── Gray-out donut (dims everything OUTSIDE the active disc) ───────────
        // SpinPanelWidget.UpdateDiscVisuals() generates a runtime donut Texture2D:
        //   - transparent hole at center (= active disc area → ball shows at full clarity)
        //   - dark (alpha 0.55) outside the hole → dims the inactive region
        // This Image is full-size (stretch-fill = same as BallImage 600x600).
        // color=white so the texture alpha drives rendering without extra tint.
        // The initial sprite is null; the donut texture is assigned in Open().
        var grayOutRt = CreateRectTransform("SpinGrayOut", ballImgRt);
        StretchFill(grayOutRt);
        var grayOutImg = grayOutRt.gameObject.AddComponent<Image>();
        grayOutImg.color = Color.white;   // texture controls per-pixel alpha
        grayOutImg.raycastTarget = false;

        // ── Active disc outline ring (thin edge cue at the disc boundary) ────
        // SpinPanelWidget sizes this to 2*activePxRadius at runtime.
        // It's a Knob-sprite Image at low alpha — acts purely as a visible border
        // around the active zone. The disc INTERIOR has no overlay (ball shows through).
        var activeDiscRt = CreateRectTransform("SpinActiveDisc", ballImgRt, new Vector2(264f, 264f));
        SetAnchorCenter(activeDiscRt);
        activeDiscRt.anchoredPosition = Vector2.zero;
        var activeDiscImg = activeDiscRt.gameObject.AddComponent<Image>();
        // Correct API for Unity built-in extra resources — NOT AssetDatabase.LoadAssetAtPath (wrong path).
        Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (knobSprite != null)
            Debug.Log("[ActionButtonsBuilder] Knob sprite loaded OK: " + knobSprite.name + " fileID=" + knobSprite.GetInstanceID());
        else
            Debug.LogError("[ActionButtonsBuilder] GetBuiltinExtraResource<Sprite>(UI/Skin/Knob.psd) returned NULL — dot will be square. Check Unity version.");
        activeDiscImg.sprite = knobSprite;
        activeDiscImg.color  = new Color(1f, 1f, 1f, 0.35f);  // thin white outline ring
        activeDiscImg.type   = Image.Type.Simple;
        activeDiscImg.preserveAspect  = false;
        activeDiscImg.raycastTarget   = false;

        // ── Gray-out mask placeholder (kept so existing serialized wiring isn't broken) ─
        // _grayOutMaskRt is no longer functionally used in iter-2; the donut texture
        // approach makes it redundant. Kept as an inert invisible GameObject.
        var grayOutMaskRt = CreateRectTransform("SpinGrayOutMask", ballImgRt, new Vector2(264f, 264f));
        SetAnchorCenter(grayOutMaskRt);
        grayOutMaskRt.anchoredPosition = Vector2.zero;
        var grayOutMaskImg = grayOutMaskRt.gameObject.AddComponent<Image>();
        grayOutMaskImg.sprite = knobSprite;
        grayOutMaskImg.color  = new Color(0f, 0f, 0f, 0.0f);  // fully transparent — inert
        grayOutMaskImg.raycastTarget = false;

        // ── Spin dot (1b: round) ────────────────────────────────────────────
        // Load circular sprite for the dot. Use built-in Knob.
        Sprite circleSprite = knobSprite;
        if (circleSprite == null)
        {
            // Fallback: load from project Resources if a white circle exists there
            circleSprite = Resources.Load<Sprite>("UI/WhiteCircle");
        }

        var dotRt = CreateRectTransform("SpinDot", ballImgRt);
        dotRt.sizeDelta = new Vector2(60f, 60f);
        SetAnchorCenter(dotRt);
        dotRt.anchoredPosition = Vector2.zero;
        var dotImg = dotRt.gameObject.AddComponent<Image>();
        dotImg.sprite = circleSprite;
        dotImg.color  = new Color(1f, 0.2f, 0.2f, 1f);  // keep existing red color
        if (circleSprite != null)
        {
            dotImg.type = Image.Type.Simple;
            dotImg.preserveAspect = false;
        }

        // ── Single drag surface (replaces 5 discrete buttons) ───────────────
        // Covers the same 600x600 area as BallImage; implements IPointerDownHandler/IDragHandler.
        // SpinPanelWidget itself now handles drag via interface — it sits on spinPanelGo (stretch fill).
        // We add a transparent hit area on BallImage to ensure raycasts are consumed there.
        var dragSurfaceRt = CreateRectTransform("SpinDragSurface", ballImgRt, new Vector2(600f, 600f));
        SetAnchorCenter(dragSurfaceRt);
        dragSurfaceRt.anchoredPosition = Vector2.zero;
        var dragSurfaceImg = dragSurfaceRt.gameObject.AddComponent<Image>();
        dragSurfaceImg.color = new Color(0f, 0f, 0f, 0f);  // transparent — just a raycast surface
        // Note: SpinPanelWidget implements IPointerDownHandler/IDragHandler and will receive events
        // from this transparent image as long as the panel has a GraphicRaycaster in the canvas.

        SpinPanelWidget spinPanelWidget = spinPanelGo.AddComponent<SpinPanelWidget>();

        // ── Find ConeRoot ──────────────────────────────────────────────────
        // Depth-search, not a direct child lookup: control_scheme_seam re-parented the whole
        // flick UI under SchemeRoot_Flick, so ConeRoot is a grandchild of the canvas now. The
        // fallback keeps working whichever scheme root it ends up under.
        var coneRootT = canvasGo.transform.Find("ConeRoot");
        if (coneRootT == null)
        {
            foreach (var t in canvasGo.GetComponentsInChildren<Transform>(true))
                if (t.name == "ConeRoot") { coneRootT = t; break; }
        }
        var coneRoot  = coneRootT != null ? coneRootT.gameObject : null;
        if (coneRoot == null) Debug.LogWarning("[ActionButtonsBuilder] ConeRoot not found — _aimingCone will be null on SpinPanelWidget.");

        // ── Find CentralBallWidget (1a: hide while spin selector is open) ──
        // CentralBallWidget lives under ShotUI_Canvas on a GO named "CentralBallWidget" or similar.
        var centralBallT = canvasGo.transform.Find("CentralBallWidget");
        // Also try "CentralBall" or scan all children for CentralBallWidget component
        GameObject centralBallGo = null;
        if (centralBallT != null)
        {
            centralBallGo = centralBallT.gameObject;
        }
        else
        {
            // Walk all immediate children of the canvas to find the CentralBallWidget component
            foreach (Transform child in canvasGo.transform)
            {
                if (child.GetComponent<Golfin.Gameplay.UI.ShotUI.CentralBallWidget>() != null)
                {
                    centralBallGo = child.gameObject;
                    break;
                }
            }
        }
        if (centralBallGo == null)
            Debug.LogWarning("[ActionButtonsBuilder] CentralBallWidget GO not found — _centralBall will be null on SpinPanelWidget. Wire manually in Inspector.");

        var spinPanelSo = new SerializedObject(spinPanelWidget);
        spinPanelSo.FindProperty("_ballImage").objectReferenceValue         = ballImg;
        spinPanelSo.FindProperty("_spinDot").objectReferenceValue           = dotRt;
        spinPanelSo.FindProperty("_dimBackground").objectReferenceValue     = spinCatcher;
        spinPanelSo.FindProperty("_defaultBallSprite").objectReferenceValue = defaultBallThumbnail;
        spinPanelSo.FindProperty("_aimingCone").objectReferenceValue        = coneRoot;
        spinPanelSo.FindProperty("_centralBall").objectReferenceValue       = centralBallGo;
        spinPanelSo.FindProperty("_activeDiscRt").objectReferenceValue      = activeDiscRt;
        spinPanelSo.FindProperty("_grayOutRt").objectReferenceValue         = grayOutRt;
        spinPanelSo.FindProperty("_grayOutMaskRt").objectReferenceValue     = grayOutMaskRt;
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
        // 48px gap to RIGHT of GolfinButton. Balls author a 73px chevron container, not 60 —
        // which is exactly why the widget derives this rather than sharing a constant.
        overlayRtBall.anchoredPosition = new Vector2(251f, SelectorOverlayAuthoredY - (73f + 8f + SelectorViewportMargin));
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

        // CardsContainer for ball overlay — same carousel viewport as the club side
        var cardsContBallRt = BuildSelectorViewport(overlayGoBall.transform, haloSprite,
                                                    out RectTransform focusHaloBallRt,
                                                    out SelectorCarouselDrag carouselDragBall);

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
        // ORDER MATTERS. This catcher is a full-screen raycast target; created here it would be a
        // LATER sibling than SelectorOverlay_Ball and would therefore render ON TOP of it and eat
        // every pointer event aimed at the ball cards — no scrolling, no selecting. The club-side
        // catcher happens to be built before its overlay and so was always fine; this one was not.
        // Put it directly beneath the ball overlay instead of relying on construction order.
        selectorCatcherBallGo.transform.SetSiblingIndex(overlayGoBall.transform.GetSiblingIndex());

        var overlayWidgetBall = overlayGoBall.AddComponent<SelectorOverlayWidget>();
        var overlaySoBall = new SerializedObject(overlayWidgetBall);
        overlaySoBall.FindProperty("_root").objectReferenceValue               = overlayRtBall;
        overlaySoBall.FindProperty("_cardsViewport").objectReferenceValue       = cardsContBallRt;
        overlaySoBall.FindProperty("_focusHalo").objectReferenceValue           = focusHaloBallRt;
        overlaySoBall.FindProperty("_carouselDrag").objectReferenceValue        = carouselDragBall;
        overlaySoBall.FindProperty("_slotPitch").floatValue                     = SelectorSlotPitch;
        overlaySoBall.FindProperty("_visibleSlots").intValue                    = SelectorVisibleSlots;
        overlaySoBall.FindProperty("_viewportMargin").floatValue                = SelectorViewportMargin;
        overlaySoBall.FindProperty("_cardPrefab").objectReferenceValue         = cardPrefabGo;
        overlaySoBall.FindProperty("_arrowUpContainer").objectReferenceValue   = arrowUpContBallRt;
        overlaySoBall.FindProperty("_arrowDownContainer").objectReferenceValue = arrowDownContBallRt;
        overlaySoBall.FindProperty("_arrowUp").objectReferenceValue            = arrowUpBtnBall;
        overlaySoBall.FindProperty("_arrowDown").objectReferenceValue          = arrowDownBtnBall;
        overlaySoBall.FindProperty("_outsideClickCatcher").objectReferenceValue = selectorCatcherBall;
        overlaySoBall.FindProperty("_anchoredPositionForClub").vector2Value    = new Vector2(-251f, SelectorOverlayAuthoredY);
        overlaySoBall.FindProperty("_anchoredPositionForBall").vector2Value    = new Vector2(251f, SelectorOverlayAuthoredY);
        overlaySoBall.ApplyModifiedProperties();

        var ballDragSo = new SerializedObject(carouselDragBall);
        ballDragSo.FindProperty("_overlay").objectReferenceValue = overlayWidgetBall;
        ballDragSo.ApplyModifiedProperties();

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

        // ── Wire ShotLayoutController (shot_view_layout §3.4) ──────────────────
        WireShotLayoutController(canvasGo, cluster, overlayWidget, overlayWidgetBall);

        // ── Re-wire ShotInProgressUiGate (same reason as the controller above) ────
        WireShotInProgressUiGate(canvasGo, clusterCg, fader, overlayWidget, overlayWidgetBall, spinPanelWidget);

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

    /// <summary>
    /// Find-and-wire every reference <c>ShotLayoutController</c> needs (shot_view_layout §3.4).
    ///
    /// <para>Wired HERE rather than by hand because this builder is the thing that deletes and
    /// re-creates the cluster and both selector overlays — three of the controller's own
    /// references — so a re-run that did not re-wire would leave the shot view stuck at the old
    /// framing with no error anywhere. Everything else is found by type or by name, and any
    /// scheme root still missing its <c>BallSpace</c> gets one, so this is also the repair path
    /// for a scene that predates the container.</para>
    /// </summary>
    static void WireShotLayoutController(GameObject canvasGo, RectTransform cluster,
                                         SelectorOverlayWidget clubSelector,
                                         SelectorOverlayWidget ballSelector)
    {
        var layout = canvasGo.GetComponent<ShotLayoutController>();
        if (layout == null) layout = canvasGo.AddComponent<ShotLayoutController>();

        var canvas     = canvasGo.GetComponent<Canvas>();
        var canvasRect = canvas != null ? canvas.rootCanvas.transform as RectTransform
                                        : canvasGo.GetComponent<RectTransform>();

        var ballWidget = canvasGo.GetComponentInChildren<CentralBallWidget>(true);
        var powerHudT  = FindDeep(canvasGo.transform, "PowerHUD");

        var so = new SerializedObject(layout);
        so.FindProperty("_canvasRect").objectReferenceValue           = canvasRect;
        so.FindProperty("_centralBall").objectReferenceValue          = ballWidget != null ? ballWidget.GetComponent<RectTransform>() : null;
        so.FindProperty("_actionButtonsCluster").objectReferenceValue = cluster;
        so.FindProperty("_clubSelector").objectReferenceValue         = clubSelector;
        so.FindProperty("_ballSelector").objectReferenceValue         = ballSelector;
        so.FindProperty("_powerHud").objectReferenceValue             = powerHudT as RectTransform;
        so.FindProperty("_pendulumLane").objectReferenceValue =
            canvasGo.GetComponentInChildren<Golfin.Gameplay.UI.Controls.Pendulum.PendulumLaneView>(true);
        so.FindProperty("_freeSwingLane").objectReferenceValue =
            canvasGo.GetComponentInChildren<Golfin.Gameplay.UI.Controls.FreeSwing.FreeSwingLaneView>(true);

        // One BallSpace per scheme root, in enum order. A root that has none yet gets one —
        // it is created empty and at offset zero, which is exactly the pre-move layout, so
        // repairing a stale scene can never move anything on its own.
        string[] rootNames = { "SchemeRoot_Flick", "SchemeRoot_Pendulum", "SchemeRoot_Needle", "SchemeRoot_FreeSwing" };
        var spaces = so.FindProperty("_ballSpaces");
        spaces.arraySize = rootNames.Length;
        for (int i = 0; i < rootNames.Length; i++)
        {
            var el   = spaces.GetArrayElementAtIndex(i);
            var root = FindDeep(canvasGo.transform, rootNames[i]) as RectTransform;
            el.FindPropertyRelative("scheme").enumValueIndex = i;      // ControlScheme is Flick,Pendulum,Needle,FreeSwing
            el.FindPropertyRelative("rect").objectReferenceValue =
                root != null ? Golfin.EditorTools.ShotUI.ShotBallSpace.Ensure(root) : null;
            if (root == null)
                Debug.LogWarning($"[ActionButtonsBuilder] {rootNames[i]} not found — its BallSpace is unwired.");
        }
        so.ApplyModifiedProperties();

        Debug.Log("[ActionButtonsBuilder] ShotLayoutController wired (canvas, ball, 4 BallSpaces, cluster, 2 selectors, PowerHUD, 2 lanes).");
    }

    /// <summary>
    /// Re-point <c>ShotInProgressUiGate</c> at the objects this builder just re-created.
    ///
    /// <para>THIS IS NOT OPTIONAL BOOKKEEPING. The gate is what hides the shot UI while the ball
    /// is in the air, and five of its references — the cluster's CanvasGroup, both selector
    /// overlays, the spin panel and the fader — are objects this builder DELETES and rebuilds.
    /// A run that did not re-wire them left every one at <c>fileID: 0</c>, and the gate then
    /// silently did nothing: the action buttons stayed visible AND tappable for the whole flight.
    /// That is precisely what selector_carousel shipped in build 2758, and Cesar found it by
    /// playing the game. <c>WireShotLayoutController</c> exists for the identical reason and
    /// carries the identical warning; the gate was simply missed when that one was written.</para>
    ///
    /// <para>The GameObject list (<c>_hideDuringShot</c>: PutterTrack, PuttPathRoot,
    /// HoleMapContainer) is deliberately NOT touched — none of those are this builder's to create,
    /// and overwriting the list would drop whatever else has been authored into it.</para>
    /// </summary>
    static void WireShotInProgressUiGate(GameObject canvasGo,
                                         CanvasGroup clusterGroup,
                                         OtherButtonsFader fader,
                                         SelectorOverlayWidget clubSelector,
                                         SelectorOverlayWidget ballSelector,
                                         SpinPanelWidget spinPanel)
    {
        var gate = Object.FindFirstObjectByType<ShotInProgressUiGate>(FindObjectsInactive.Include);
        if (gate == null)
        {
            Debug.LogWarning("[ActionButtonsBuilder] No ShotInProgressUiGate in the scene — the shot UI " +
                             "will NOT be hidden while the ball is in flight.");
            return;
        }

        var so = new SerializedObject(gate);

        // _hideGroupsDuringShot: replace the cluster's CanvasGroup in place, keeping any other
        // entry. Slot 0 is the cluster by convention; a null slot is the symptom being fixed.
        var groups = so.FindProperty("_hideGroupsDuringShot");
        int clusterSlot = -1;
        for (int i = 0; i < groups.arraySize; i++)
        {
            var el = groups.GetArrayElementAtIndex(i).objectReferenceValue;
            if (el == null || el is CanvasGroup cg && cg.gameObject.name == "ActionButtons_Cluster")
            { clusterSlot = i; break; }
        }
        if (clusterSlot < 0) { groups.arraySize++; clusterSlot = groups.arraySize - 1; }
        groups.GetArrayElementAtIndex(clusterSlot).objectReferenceValue = clusterGroup;

        so.FindProperty("_clubSelector").objectReferenceValue      = clubSelector;
        so.FindProperty("_ballSelector").objectReferenceValue      = ballSelector;
        so.FindProperty("_spinPanel").objectReferenceValue         = spinPanel;
        so.FindProperty("_actionButtonsFader").objectReferenceValue = fader;
        so.ApplyModifiedProperties();

        Debug.Log("[ActionButtonsBuilder] ShotInProgressUiGate re-wired (cluster CanvasGroup, both " +
                  "selectors, spin panel, fader) — the shot UI hides again while the ball is in flight.");
    }

    /// <summary>Depth-first find by name, inactive included — the scheme roots ship inactive and
    /// <c>Transform.Find</c> only looks one level down.</summary>
    static Transform FindDeep(Transform parent, string name)
    {
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
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
        // IMPORTANT — IconArea must have NO background Image (intentional). The Button-All
        // sprite's WHITE top half IS the design (white icon tray + navy label + gold border).
        // An opaque navy quad here masks that white top and (being hard-cornered) overflows
        // the rounded border. That regressed into the scene at Order 354 (commit 72bbb8db4)
        // and Cesar rejected it. Do NOT add an opaque IconArea background.
        // Guard: ActionButtonRenderingTests fails if an opaque IconArea bg reappears.

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
        // IMPORTANT — IconArea must have NO background Image (intentional). The Button-All
        // sprite's WHITE top half IS the design (white icon tray + navy label + gold border).
        // An opaque navy quad here masks that white top and (being hard-cornered) overflows
        // the rounded border. That regressed into the scene at Order 354 (commit 72bbb8db4)
        // and Cesar rejected it. Do NOT add an opaque IconArea background.
        // Guard: ActionButtonRenderingTests fails if an opaque IconArea bg reappears.

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

    // ── Selector carousel viewport ────────────────────────────────────────────

    /// <summary>
    /// Build one selector's <c>CardsContainer</c> as a fixed carousel viewport
    /// (selector_carousel §1). Identical for clubs and balls, so it lives here rather than
    /// twice inline: a fixed-height window, a <see cref="RectMask2D"/> widened 36px each side so
    /// the halo's glow survives, a transparent hit area so a drag that starts in the 34px gap
    /// between two cards is still caught, the focus halo behind the cards, and the drag handler.
    ///
    /// <para>The six pool cards are NOT created here — <c>SelectorOverlayWidget</c> instantiates
    /// them from <c>_cardPrefab</c> on first open, exactly as the old Populate() did.</para>
    /// </summary>
    static RectTransform BuildSelectorViewport(Transform overlayRoot, Sprite haloSprite,
                                               out RectTransform focusHaloRt,
                                               out SelectorCarouselDrag carouselDrag)
    {
        var viewportRt = CreateRectTransform("CardsContainer", overlayRoot);

        // Fixed window. No VerticalLayoutGroup and no ContentSizeFitter any more: the cards are
        // positioned by SelectorOverlayWidget.Layout() at fractional offsets, which a layout
        // group would overwrite on the next rebuild.
        var le = viewportRt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = SelectorViewportHeight;
        le.flexibleWidth   = 1f;

        // Transparent hit area FIRST so it draws behind every card.
        var hit = viewportRt.gameObject.AddComponent<Image>();
        hit.color         = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;

        // Negative padding GROWS the mask rect (x=left, y=bottom, z=right, w=top). The halo is
        // 217 wide inside a 145 wide viewport, so it needs 36px of room on each side; top and
        // bottom already have it, baked into SelectorViewportHeight.
        var mask = viewportRt.gameObject.AddComponent<RectMask2D>();
        mask.padding = new Vector4(-36f, 0f, -36f, 0f);

        // Focus halo — child 0, so it renders BEHIND the cards. Centred on the focus slot.
        focusHaloRt = CreateRectTransform("FocusHalo", viewportRt,
                                          new Vector2(SelectorHaloWidth, SelectorHaloHeight));
        focusHaloRt.anchorMin = focusHaloRt.anchorMax = Vector2.zero;   // viewport bottom-left
        focusHaloRt.pivot     = new Vector2(0.5f, 0.5f);
        focusHaloRt.anchoredPosition = new Vector2(CardArtCentreX,
                                                   SelectorViewportMargin + CardArtCentreY);
        var haloImg = focusHaloRt.gameObject.AddComponent<Image>();
        haloImg.sprite        = haloSprite;
        haloImg.type          = Image.Type.Simple;
        haloImg.color         = HexToColor(SelectorHaloTintHex);
        haloImg.raycastTarget = false;
        var haloLe = focusHaloRt.gameObject.AddComponent<LayoutElement>();
        haloLe.ignoreLayout = true;

        carouselDrag = viewportRt.gameObject.AddComponent<SelectorCarouselDrag>();
        carouselDrag.enabled = false;   // modal mode turns it on
        var dragSo = new SerializedObject(carouselDrag);
        dragSo.FindProperty("_viewport").objectReferenceValue = viewportRt;
        dragSo.ApplyModifiedProperties();

        return viewportRt;
    }

    /// <summary>
    /// Force the focus-halo PNG to import as a full-rect Sprite. Idempotent — re-running the
    /// builder on an already-correct import does nothing — so this is safe to call every build,
    /// and it is what lets Robin drop finished art at the same path with zero code changes.
    /// </summary>
    static void CoerceHaloSprite(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[ActionButtonsBuilder] Could not get TextureImporter for {assetPath}");
            return;
        }
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool alreadyRight = importer.textureType      == TextureImporterType.Sprite
                         && importer.spriteImportMode == SpriteImportMode.Single
                         && !importer.mipmapEnabled
                         && settings.spriteMeshType   == SpriteMeshType.FullRect;
        if (alreadyRight) return;

        importer.textureType      = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled    = false;
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        AssetDatabase.Refresh();
        Debug.Log($"[ActionButtonsBuilder] Coerced {assetPath} to full-rect Sprite.");
    }

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
