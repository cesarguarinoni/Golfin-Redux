using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Golfin.Gameplay.UI.ShotUI;
using Golfin.Physics.Viewer;

/// <summary>
/// §2d iter-6: Builds the HoleCompleteWidget + HoleCompleteDriver hierarchy in LabScaffold.unity.
///
/// Menu: GOLFIN/Build/Build HoleComplete Widgets (§2d)
///
/// Iter-6 changes:
/// - Added horizontal dividers between card sections (Figma: 2px white separator lines)
/// - Rewards row: MiddleCenter alignment + childForceExpandWidth=false (was MiddleLeft → spread)
/// - Card BG: ContentSizeFitter verticalFit=PreferredSize (was hardcoded 600 → buttons clipped)
/// - Removed green-square thumbnail (Placeholder_HoleThumbnailSmall.png) — was visually broken
/// - Card 2 NextBody: hole-select-style layout (map + par label + description text)
/// - New SerializeField wiring: _holeMapLarge / _nextHoleMapLarge use placeholder at build time;
///   HoleCompleteDriver.ShowResultScreen() overrides with real maps at runtime.
/// </summary>
public static class HoleCompleteWidgetBuilder
{
    [MenuItem("GOLFIN/Build/Build HoleComplete Widgets (§2d)")]
    public static void Build()
    {
        // ── 1. Open LabScaffold.unity ────────────────────────────────────────
        string scenePath = "Assets/Scenes/Physics/LabScaffold.unity";
        var scene = SceneManager.GetSceneByPath(scenePath);
        if (!scene.isLoaded)
        {
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // ── 2. Find key scene objects ────────────────────────────────────────
        var canvas = GameObject.Find("ShotUI_Canvas");
        if (canvas == null)
        {
            Debug.LogError("[HoleCompleteWidgetBuilder] ShotUI_Canvas not found. Open LabScaffold.unity first.");
            return;
        }

        var labRoot = GameObject.Find("LabRoot");
        if (labRoot == null)
        {
            Debug.LogError("[HoleCompleteWidgetBuilder] LabRoot not found.");
            return;
        }

        var debugPanelController = GameObject.Find("DebugShotPanelController");
        if (debugPanelController == null)
            Debug.LogWarning("[HoleCompleteWidgetBuilder] DebugShotPanelController not found; HoleOutBtn wiring skipped.");

        // ── 3. Load assets ───────────────────────────────────────────────────
        AssetDatabase.Refresh();

        // §2d iteration-5: fix 9-slice borders on background/button sprites.
        FixSpriteBorder("Assets/Art/ResultScreen/Background - HoleCard.png",  50, 50, 50, 50);
        FixSpriteBorder("Assets/Art/ResultScreen/Button - Replay.png",        61, 61, 61, 61);
        FixSpriteBorder("Assets/Art/ResultScreen/Button - Retry.png",         65, 65, 65, 65);
        FixSpriteBorder("Assets/Art/ResultScreen/Button - Play.png",          65, 65, 65, 65);

        // iter-6: ensure divider sprite is imported as sprite
        FixSpriteBorder("Assets/Art/Settings/Divider.png", 0, 0, 0, 0);
        AssetDatabase.Refresh();

        Sprite holeCardBG  = LoadSprite("Assets/Art/ResultScreen/Background - HoleCard.png");
        Sprite replayBtnBG = LoadSprite("Assets/Art/ResultScreen/Button - Replay.png");
        Sprite retryBtnBG  = LoadSprite("Assets/Art/ResultScreen/Button - Retry.png");
        Sprite playBtnBG   = LoadSprite("Assets/Art/ResultScreen/Button - Play.png");
        Sprite iconCheck   = LoadSprite("Assets/Art/ResultScreen/Icon - Check.png");
        Sprite iconX       = LoadSprite("Assets/Art/ResultScreen/Icon - X.png");
        Sprite lockIcon    = LoadSprite("Assets/Art/ResultScreen/Placeholders/Placeholder_LockIcon.png");
        // iter-6: use real hole map placeholder (grey rect) instead of green thumbnail
        Sprite holeMap     = LoadSprite("Assets/Art/ResultScreen/Placeholders/Placeholder_HoleMap.png");
        Sprite darkenImg   = LoadSprite("Assets/Art/ResultScreen/Placeholders/Placeholder_Darken.png");
        Sprite coinIcon    = LoadSprite("Assets/Art/ResultScreen/Placeholders/Placeholder_RewardCoin.png");
        Sprite repairIcon  = LoadSprite("Assets/Art/ResultScreen/Placeholders/Placeholder_RewardRepair.png");
        Sprite ballIcon    = LoadSprite("Assets/Art/ResultScreen/Placeholders/Placeholder_RewardBall.png");
        // iter-6: horizontal divider — use Settings/Divider.png (horizontal thin white line)
        Sprite dividerSprite = LoadSprite("Assets/Art/Settings/Divider.png");

        TMP_FontAsset rubikSemiBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rubik-SemiBold SDF.asset");
        TMP_FontAsset rubikVar      = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rubik-VariableFont_wght SDF.asset");
        TMP_FontAsset bodyFont      = rubikVar != null ? rubikVar : rubikSemiBold;

        // ── 4. Remove existing HoleCompleteWidget if present ─────────────────
        var existing = canvas.transform.Find("HoleCompleteWidget");
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
            Debug.Log("[HoleCompleteWidgetBuilder] Removed existing HoleCompleteWidget.");
        }

        // ── 5. Build HoleCompleteWidget root ─────────────────────────────────
        var widgetGO = CreateStretchGO("HoleCompleteWidget", canvas.transform);
        widgetGO.SetActive(true); // stays active; _root child is hidden by Awake()
        var widget = widgetGO.AddComponent<HoleCompleteWidget>();

        // Overlay Canvas so this renders on top of all sibling HUD elements.
        var overlayCvs = widgetGO.AddComponent<Canvas>();
        overlayCvs.overrideSorting = true;
        overlayCvs.sortingOrder = 100;
        widgetGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // DimBackground — near-opaque black to subdue gameplay HUD.
        var dimGO = CreateStretchGO("DimBackground", widgetGO.transform);
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.92f);
        dimImg.raycastTarget = true;

        // Root — child content; Awake hides this
        var rootGO = CreateStretchGO("Root", widgetGO.transform);
        // Vertical layout for two cards stacked with gap
        var vLayout = rootGO.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(48, 48, 24, 24);
        vLayout.spacing = 24;
        vLayout.childAlignment = TextAnchor.UpperCenter;
        vLayout.childControlHeight = false;
        vLayout.childControlWidth = true;
        vLayout.childForceExpandHeight = false;
        vLayout.childForceExpandWidth = true;

        // ── 6. Build Card1 ────────────────────────────────────────────────────
        var card1GO = BuildCard("Card1", rootGO.transform,
            holeCardBG, iconCheck, iconX, lockIcon,
            holeMap, darkenImg,
            coinIcon, repairIcon, ballIcon,
            replayBtnBG, retryBtnBG, null,
            dividerSprite,
            rubikSemiBold, bodyFont,
            out var card1);

        // ── 7. Build Card2 ────────────────────────────────────────────────────
        var card2GO = BuildCard("Card2", rootGO.transform,
            holeCardBG, iconCheck, iconX, lockIcon,
            holeMap, darkenImg,
            coinIcon, repairIcon, ballIcon,
            null, null, playBtnBG,
            dividerSprite,
            rubikSemiBold, bodyFont,
            out var card2);

        // ── 8. Wire HoleCompleteWidget references ─────────────────────────────
        var widgetSO = new SerializedObject(widget);
        widgetSO.FindProperty("_root").objectReferenceValue  = rootGO;
        widgetSO.FindProperty("_dimBackground").objectReferenceValue = dimImg;
        widgetSO.FindProperty("_card1").objectReferenceValue = card1;
        widgetSO.FindProperty("_card2").objectReferenceValue = card2;
        widgetSO.ApplyModifiedProperties();

        // ── 9. Add HoleCompleteDriver to LabRoot ──────────────────────────────
        var existingDriver = labRoot.GetComponent<HoleCompleteDriver>();
        if (existingDriver != null) UnityEngine.Object.DestroyImmediate(existingDriver);
        var driver = labRoot.AddComponent<HoleCompleteDriver>();

        var driverSO = new SerializedObject(driver);
        driverSO.FindProperty("controller").objectReferenceValue = labRoot.GetComponent<Golfin.Physics.Viewer.PhysicsLabController>();
        driverSO.FindProperty("widget").objectReferenceValue     = widget;
        driverSO.ApplyModifiedProperties();

        // ── 10. Add HoleOutBtn under DebugPanel + wire DebugShotPanel ─────────
        if (debugPanelController != null)
        {
            var debugPanel = debugPanelController.transform.Find("DebugPanel");
            if (debugPanel != null)
            {
                var existingHoleOutBtn = debugPanel.Find("HoleOutBtn");
                if (existingHoleOutBtn != null)
                    UnityEngine.Object.DestroyImmediate(existingHoleOutBtn.gameObject);

                var holeOutGO = new GameObject("HoleOutBtn");
                holeOutGO.transform.SetParent(debugPanel, false);
                var holeOutRT = holeOutGO.AddComponent<RectTransform>();
                holeOutRT.sizeDelta = new Vector2(160, 40);
                var holeOutImg = holeOutGO.AddComponent<Image>();
                holeOutImg.color = new Color(1f, 0.4f, 0.0f, 1f); // orange debug color
                var holeOutBtn = holeOutGO.AddComponent<Button>();
                holeOutBtn.targetGraphic = holeOutImg;

                var holeOutTextGO = new GameObject("Text");
                holeOutTextGO.transform.SetParent(holeOutGO.transform, false);
                var holeOutTmpRT = holeOutTextGO.AddComponent<RectTransform>();
                StretchFill(holeOutTmpRT);
                var holeOutTmp = holeOutTextGO.AddComponent<TextMeshProUGUI>();
                holeOutTmp.text = "HOLE OUT";
                holeOutTmp.fontSize = 18;
                holeOutTmp.color = Color.white;
                holeOutTmp.alignment = TextAlignmentOptions.Center;
                if (rubikSemiBold != null) holeOutTmp.font = rubikSemiBold;

                // Wire DebugShotPanel
                var debugShotPanel = debugPanelController.GetComponent<DebugShotPanel>();
                if (debugShotPanel != null)
                {
                    var debugSO = new SerializedObject(debugShotPanel);
                    debugSO.FindProperty("_holeOutBtn").objectReferenceValue = holeOutBtn;
                    debugSO.FindProperty("_holeOutTriggerMB").objectReferenceValue = driver;
                    debugSO.ApplyModifiedProperties();
                    Debug.Log("[HoleCompleteWidgetBuilder] DebugShotPanel HoleOutBtn + driver wired.");
                }
                else
                {
                    Debug.LogWarning("[HoleCompleteWidgetBuilder] DebugShotPanel component not found on DebugShotPanelController.");
                }
            }
            else
            {
                Debug.LogWarning("[HoleCompleteWidgetBuilder] DebugPanel child not found under DebugShotPanelController.");
            }
        }

        // ── 11. Save scene ────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[HoleCompleteWidgetBuilder] §2d iter-6: HoleCompleteWidget + HoleCompleteDriver built and saved to LabScaffold.unity.");
    }

    // ── Card builder ─────────────────────────────────────────────────────────

    static GameObject BuildCard(
        string name, Transform parent,
        Sprite cardBG, Sprite checkIcon, Sprite xIcon, Sprite lockIcon,
        Sprite map, Sprite darken,
        Sprite coinIcon, Sprite repairIcon, Sprite ballIcon,
        Sprite replayBtnBG, Sprite retryBtnBG, Sprite playBtnBG,
        Sprite dividerSprite,
        TMP_FontAsset headingFont, TMP_FontAsset bodyFont,
        out HoleCompleteCardWidget card)
    {
        // Card root — 978px wide. iter-6: ContentSizeFitter drives height instead of hardcoded 600.
        var cardGO = new GameObject(name);
        cardGO.transform.SetParent(parent, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(978, 0); // height driven by CSF
        var le = cardGO.AddComponent<LayoutElement>();
        le.preferredWidth = 978;
        le.minHeight = 200;

        // Background image
        var bgImg = cardGO.AddComponent<Image>();
        bgImg.sprite = cardBG;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        // Vertical layout for card contents
        // iter-7 fix: childControlHeight=true so LayoutElement.preferredHeight is respected on all children
        // (was false → VLG ignored preferredHeight=8 on dividers, stretching them to fill available space)
        var layout = cardGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 0; // spacing handled by dividers + per-element padding
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ContentSizeFitter — iter-6 fix: card height auto-fits all children
        var csf = cardGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        card = cardGO.AddComponent<HoleCompleteCardWidget>();

        // ── Header section ──────────────────────────────────────────────────
        var successHeader = BuildIconTextHeader("SuccessHeader", cardGO.transform, checkIcon, "SUCCESS",
            HexToColor("50C878"), headingFont, 32f);

        var failedHeader = BuildIconTextHeader("FailedHeader", cardGO.transform, xIcon, "FAILED",
            HexToColor("D16A47"), headingFont, 32f);
        failedHeader.SetActive(false);

        var nextHeader = BuildTextOnlyHeader("NextHeader", cardGO.transform, "NEXT",
            HexToColor("EEDC9A"), headingFont, 32f);
        nextHeader.SetActive(false);

        var lockedHeader = BuildIconTextHeader("LockedHeader", cardGO.transform, lockIcon, "LOCKED",
            HexToColor("C8C8C8"), headingFont, 32f);
        lockedHeader.SetActive(false);

        // ── Subhead ─────────────────────────────────────────────────────────
        var subheadGO = new GameObject("Subhead");
        subheadGO.transform.SetParent(cardGO.transform, false);
        var subheadLE = subheadGO.AddComponent<LayoutElement>();
        subheadLE.preferredHeight = 40;
        var subheadTmp = subheadGO.AddComponent<TextMeshProUGUI>();
        subheadTmp.text = "Lomond Country Club  - Hole 1 - Par 4";
        subheadTmp.fontSize = 28;
        subheadTmp.color = Color.white;
        subheadTmp.alignment = TextAlignmentOptions.Center;
        if (headingFont != null) subheadTmp.font = headingFont;
        subheadTmp.raycastTarget = false;

        // ── Divider 1 — after header+subhead, before body ───────────────────
        BuildDivider("Divider_BelowSubhead", cardGO.transform, dividerSprite);

        // ── Current Body ─────────────────────────────────────────────────────
        var currentBodyGO = new GameObject("CurrentBody");
        currentBodyGO.transform.SetParent(cardGO.transform, false);
        var currentBodyLE = currentBodyGO.AddComponent<LayoutElement>();
        currentBodyLE.preferredHeight = 220;
        var currentBodyHLG = currentBodyGO.AddComponent<HorizontalLayoutGroup>();
        currentBodyHLG.padding = new RectOffset(8, 8, 12, 12);
        currentBodyHLG.spacing = 16;
        currentBodyHLG.childAlignment = TextAnchor.UpperLeft;
        currentBodyHLG.childControlHeight = false;
        currentBodyHLG.childControlWidth = false;
        currentBodyHLG.childForceExpandWidth = false;
        currentBodyHLG.childForceExpandHeight = false;

        // Map: 156x200 (current hole). No thumbnail — removed green-square placeholder (iter-6).
        Image mapImg = null;
        {
            var mapGO = new GameObject("HoleMapLarge");
            mapGO.transform.SetParent(currentBodyGO.transform, false);
            mapGO.AddComponent<RectTransform>().sizeDelta = new Vector2(156, 200);
            mapImg = mapGO.AddComponent<Image>();
            mapImg.sprite = map;
            mapImg.preserveAspect = true;
            mapImg.raycastTarget = false;
        }

        // Stats block
        var statsGO = new GameObject("StatsBlockText");
        statsGO.transform.SetParent(currentBodyGO.transform, false);
        var statsLE = statsGO.AddComponent<LayoutElement>();
        statsLE.preferredWidth = 500;
        statsLE.preferredHeight = 200;
        var statsTmp = statsGO.AddComponent<TextMeshProUGUI>();
        statsTmp.text = "<b>TEE OFF:</b> REGULAR\n<b>STROKES:</b> 1 (BIRDIE)\n<b>BEST:</b> —\n<b>TIME:</b> 00:00:00\n<b>BEST:</b> —";
        statsTmp.fontSize = 24;
        statsTmp.color = Color.white;
        statsTmp.alignment = TextAlignmentOptions.TopLeft;
        statsTmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        statsTmp.overflowMode = TextOverflowModes.Overflow;
        statsTmp.lineSpacing = 4f;
        if (bodyFont != null) statsTmp.font = bodyFont;
        statsTmp.raycastTarget = false;

        // ── Next Body (Card 2 hole-select style) ──────────────────────────────
        var nextBodyGO = new GameObject("NextBody");
        nextBodyGO.transform.SetParent(cardGO.transform, false);
        var nextBodyLE = nextBodyGO.AddComponent<LayoutElement>();
        nextBodyLE.preferredHeight = 220;
        nextBodyGO.SetActive(false);

        Image nextMapImg   = null;
        TMP_Text nextParTmp  = null;
        TMP_Text nextDescTmp = null;
        {
            var nextBodyHLG = nextBodyGO.AddComponent<HorizontalLayoutGroup>();
            nextBodyHLG.padding = new RectOffset(8, 8, 12, 12);
            nextBodyHLG.spacing = 16;
            nextBodyHLG.childAlignment = TextAnchor.UpperLeft;
            nextBodyHLG.childControlHeight = false;
            nextBodyHLG.childControlWidth = false;
            nextBodyHLG.childForceExpandWidth = false;
            nextBodyHLG.childForceExpandHeight = false;

            // Map: 156x200 (next hole)
            var mapGO2 = new GameObject("NextHoleMapLarge");
            mapGO2.transform.SetParent(nextBodyGO.transform, false);
            mapGO2.AddComponent<RectTransform>().sizeDelta = new Vector2(156, 200);
            nextMapImg = mapGO2.AddComponent<Image>();
            nextMapImg.sprite = map; // placeholder at build time; overridden at runtime
            nextMapImg.preserveAspect = true;
            nextMapImg.raycastTarget = false;

            // Info column: par label + description (hole-select style) — iter-6
            var infoColGO = new GameObject("NextHoleInfoCol");
            infoColGO.transform.SetParent(nextBodyGO.transform, false);
            var infoColLE = infoColGO.AddComponent<LayoutElement>();
            infoColLE.preferredWidth = 500;
            infoColLE.preferredHeight = 200;
            var infoColVLG = infoColGO.AddComponent<VerticalLayoutGroup>();
            infoColVLG.padding = new RectOffset(8, 8, 0, 0);
            infoColVLG.spacing = 12;
            infoColVLG.childAlignment = TextAnchor.UpperLeft;
            infoColVLG.childControlHeight = true;  // iter-7: must be true so LE.preferredHeight is used
            infoColVLG.childControlWidth = true;
            infoColVLG.childForceExpandWidth = true;
            infoColVLG.childForceExpandHeight = false;

            // Par label — "Par 4" (mirrors hole-select subtitle / par display)
            var parGO = new GameObject("NextHoleParText");
            parGO.transform.SetParent(infoColGO.transform, false);
            var parLE = parGO.AddComponent<LayoutElement>();
            parLE.preferredHeight = 40;
            nextParTmp = parGO.AddComponent<TextMeshProUGUI>();
            nextParTmp.text = "Par —";
            nextParTmp.fontSize = 28; // Footnote: 39px Figma / 1.4
            nextParTmp.color = HexToColor("EEDC9A"); // mission gold for par callout
            nextParTmp.fontStyle = FontStyles.Bold;
            nextParTmp.alignment = TextAlignmentOptions.TopLeft;
            nextParTmp.raycastTarget = false;
            if (headingFont != null) nextParTmp.font = headingFont;

            // Description text (hole strategy tip from localization CSV)
            var descGO = new GameObject("NextHoleDescText");
            descGO.transform.SetParent(infoColGO.transform, false);
            var descLE = descGO.AddComponent<LayoutElement>();
            descLE.preferredHeight = 148;
            nextDescTmp = descGO.AddComponent<TextMeshProUGUI>();
            nextDescTmp.text = "Next hole tip — TBD";
            nextDescTmp.fontSize = 21; // Caption_3: 30px Figma / 1.4
            nextDescTmp.color = Color.white;
            nextDescTmp.alignment = TextAlignmentOptions.TopLeft;
            nextDescTmp.textWrappingMode = TMPro.TextWrappingModes.Normal;
            nextDescTmp.overflowMode = TextOverflowModes.Overflow;
            nextDescTmp.lineSpacing = 4f;
            if (bodyFont != null) nextDescTmp.font = bodyFont;
            nextDescTmp.raycastTarget = false;
        }

        // ── Divider 2 — after body section (CurrentBody OR NextBody), before rewards ──
        // This divider is placed AFTER NextBody in VLG order, so it always appears
        // between the visible body (whichever one) and the rewards row.
        // Card 1: CurrentBody(active) → [NextBody inactive, skip] → Div2 → Rewards
        // Card 2: [CurrentBody inactive, skip] → NextBody(active) → Div2 → Rewards
        BuildDivider("Divider_BelowBody", cardGO.transform, dividerSprite);

        // ── Rewards Row ───────────────────────────────────────────────────────
        var rewardsGO = new GameObject("RewardsRow");
        rewardsGO.transform.SetParent(cardGO.transform, false);
        var rewardsLE = rewardsGO.AddComponent<LayoutElement>();
        rewardsLE.preferredHeight = 72;
        var cg = rewardsGO.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        var rewardsHLG = rewardsGO.AddComponent<HorizontalLayoutGroup>();
        rewardsHLG.spacing = 32;
        rewardsHLG.padding = new RectOffset(0, 0, 0, 0);
        // iter-6 fix: center rewards as tight cluster (was MiddleLeft + forceExpand=true → spread)
        rewardsHLG.childAlignment = TextAnchor.MiddleCenter;
        rewardsHLG.childControlHeight = false;
        rewardsHLG.childControlWidth = false;
        rewardsHLG.childForceExpandWidth  = false;
        rewardsHLG.childForceExpandHeight = false;

        TMP_Text coinTmp   = BuildRewardEntry("CoinReward",   rewardsGO.transform, coinIcon,   "x10", headingFont, 36);
        TMP_Text repairTmp = BuildRewardEntry("RepairReward", rewardsGO.transform, repairIcon, "x10", headingFont, 36);
        TMP_Text ballTmp   = BuildRewardEntry("BallReward",   rewardsGO.transform, ballIcon,   "x10", headingFont, 36);

        // ── Divider 3 — after rewards, before buttons ─────────────────────────
        BuildDivider("Divider_BelowRewards", cardGO.transform, dividerSprite);

        // ── Buttons ───────────────────────────────────────────────────────────
        var buttonsGO = new GameObject("Buttons");
        buttonsGO.transform.SetParent(cardGO.transform, false);
        var buttonsLE = buttonsGO.AddComponent<LayoutElement>();
        buttonsLE.preferredHeight = 120;
        var buttonsHLG = buttonsGO.AddComponent<HorizontalLayoutGroup>();
        buttonsHLG.childAlignment = TextAnchor.MiddleCenter;
        buttonsHLG.childControlHeight = false;
        buttonsHLG.childControlWidth = false;
        buttonsHLG.padding = new RectOffset(0, 0, 0, 0);
        buttonsHLG.childForceExpandWidth = false;
        buttonsHLG.childForceExpandHeight = false;

        // REPLAY (silver) — 348px wide (iter-5 measured from Figma reference PNG)
        Button replayBtn = BuildButton("ReplayButton", buttonsGO.transform, replayBtnBG, "REPLAY",
            HexToColor("1E293B"), headingFont, 47, new Vector2(348, 120));
        // RETRY (gold) — 307px wide
        Button retryBtn  = BuildButton("RetryButton",  buttonsGO.transform, retryBtnBG,  "RETRY",
            HexToColor("321506"), headingFont, 47, new Vector2(307, 120));
        // PLAY (gold) — 353px wide
        Button playBtn   = BuildButton("PlayButton",   buttonsGO.transform, playBtnBG,   "PLAY",
            HexToColor("321506"), headingFont, 47, new Vector2(353, 120));

        if (retryBtn != null)  retryBtn.gameObject.SetActive(false);
        if (playBtn  != null)  playBtn.gameObject.SetActive(false);

        // ── Darken Overlay ────────────────────────────────────────────────────
        var darkenGO = new GameObject("DarkenOverlay");
        darkenGO.transform.SetParent(cardGO.transform, false);
        var darkenRT = darkenGO.AddComponent<RectTransform>();
        darkenRT.anchorMin = Vector2.zero;
        darkenRT.anchorMax = Vector2.one;
        darkenRT.sizeDelta = Vector2.zero;
        darkenRT.offsetMin = Vector2.zero;
        darkenRT.offsetMax = Vector2.zero;
        var darkenImage = darkenGO.AddComponent<Image>();
        darkenImage.sprite = darken;
        darkenImage.color = new Color(0f, 0f, 0f, 0.65f);
        darkenImage.raycastTarget = false;
        darkenGO.SetActive(false);

        // ── Wire HoleCompleteCardWidget ────────────────────────────────────────
        var cardSO = new SerializedObject(card);
        cardSO.FindProperty("_successHeaderRoot").objectReferenceValue = successHeader;
        cardSO.FindProperty("_failedHeaderRoot").objectReferenceValue  = failedHeader;
        cardSO.FindProperty("_nextHeaderRoot").objectReferenceValue    = nextHeader;
        cardSO.FindProperty("_lockedHeaderRoot").objectReferenceValue  = lockedHeader;
        cardSO.FindProperty("_subheadText").objectReferenceValue       = subheadTmp;
        cardSO.FindProperty("_currentBodyRoot").objectReferenceValue   = currentBodyGO;
        // iter-6: _holeMapLarge now wired directly (no thumbnail)
        cardSO.FindProperty("_holeMapLarge").objectReferenceValue      = mapImg;
        cardSO.FindProperty("_statsBlockText").objectReferenceValue    = statsTmp;
        cardSO.FindProperty("_nextBodyRoot").objectReferenceValue      = nextBodyGO;
        cardSO.FindProperty("_nextHoleMapLarge").objectReferenceValue  = nextMapImg;
        // iter-6: wire new par + desc fields (renamed from _nextHoleTipText)
        if (nextParTmp  != null) cardSO.FindProperty("_nextHoleParText").objectReferenceValue  = nextParTmp;
        if (nextDescTmp != null) cardSO.FindProperty("_nextHoleDescText").objectReferenceValue = nextDescTmp;
        cardSO.FindProperty("_rewardsCanvasGroup").objectReferenceValue = cg;
        cardSO.FindProperty("_rewardCoinText").objectReferenceValue    = coinTmp;
        cardSO.FindProperty("_rewardRepairText").objectReferenceValue  = repairTmp;
        cardSO.FindProperty("_rewardBallText").objectReferenceValue    = ballTmp;
        if (replayBtn != null) cardSO.FindProperty("_replayButton").objectReferenceValue = replayBtn;
        if (retryBtn  != null) cardSO.FindProperty("_retryButton").objectReferenceValue  = retryBtn;
        if (playBtn   != null) cardSO.FindProperty("_playButton").objectReferenceValue   = playBtn;
        cardSO.FindProperty("_darkenOverlay").objectReferenceValue     = darkenGO;
        cardSO.ApplyModifiedProperties();

        Debug.Log($"[HoleCompleteWidgetBuilder] Card '{name}' built and wired (iter-6).");
        return cardGO;
    }

    // ── Divider builder (iter-7) ─────────────────────────────────────────────

    /// <summary>
    /// Adds a horizontal thin white separator line as a LayoutElement child.
    /// iter-7 fix: flexibleHeight=0 prevents VLG from expanding beyond preferredHeight=8.
    ///             Image.Type.Simple (not Sliced) — the 978×2 sprite has 0-px borders so
    ///             Sliced is a no-op and causes stretching artifacts.
    ///             preserveAspect=false — line is meant to fill the full card width.
    /// </summary>
    static void BuildDivider(string name, Transform parent, Sprite dividerSprite)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 8;  // thin line (~2px line + 3px padding each side)
        le.minHeight       = 4;
        le.flexibleHeight  = 0;  // iter-7: prevent VLG from expanding beyond preferredHeight
        var img = go.AddComponent<Image>();
        if (dividerSprite != null)
        {
            img.sprite = dividerSprite;
            img.type   = Image.Type.Simple;   // not Sliced: border=0, Simple is correct
        }
        img.preserveAspect = false;            // line fills full card width, no aspect clamp
        img.color          = new Color(1f, 1f, 1f, 0.35f); // subtle white, 35% alpha
        img.raycastTarget  = false;
    }

    // ── Sub-builders ─────────────────────────────────────────────────────────

    static GameObject BuildIconTextHeader(string name, Transform parent, Sprite icon, string label,
        Color textColor, TMP_FontAsset font, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 60;
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 16;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlHeight = false;
        hlg.childControlWidth = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        if (icon != null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            iconGO.AddComponent<RectTransform>().sizeDelta = new Vector2(48, 48);
            var iconImg = iconGO.AddComponent<Image>();
            iconImg.sprite = icon;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            if (name == "LockedHeader")
                iconImg.color = Color.white;
        }

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var labelRT = textGO.AddComponent<RectTransform>();
        labelRT.sizeDelta = new Vector2(0, 60);
        var csf = textGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        return go;
    }

    static GameObject BuildTextOnlyHeader(string name, Transform parent, string label,
        Color textColor, TMP_FontAsset font, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 60;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        return go;
    }

    static TMP_Text BuildRewardEntry(string name, Transform parent, Sprite icon, string text,
        TMP_FontAsset font, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>().sizeDelta = new Vector2(100, 66);
        var hlg = go.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlHeight = false;
        hlg.childControlWidth = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        if (icon != null)
        {
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(go.transform, false);
            iconGO.AddComponent<RectTransform>().sizeDelta = new Vector2(42, 42);
            var img = iconGO.AddComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        var textGO = new GameObject("CountText");
        textGO.transform.SetParent(go.transform, false);
        textGO.AddComponent<RectTransform>().sizeDelta = new Vector2(58, 66);
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        return tmp;
    }

    static Button BuildButton(string name, Transform parent, Sprite bgSprite, string label,
        Color textColor, TMP_FontAsset font, float fontSize, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = size;
        Image bg = go.AddComponent<Image>();
        if (bgSprite != null) { bg.sprite = bgSprite; bg.type = Image.Type.Sliced; }
        else bg.color = new Color(0.3f, 0.3f, 0.3f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;
        var tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = textColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        return btn;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    static GameObject CreateStretchGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        StretchFill(rt);
        return go;
    }

    static void StretchFill(RectTransform rt)
    {
        rt.anchorMin     = Vector2.zero;
        rt.anchorMax     = Vector2.one;
        rt.offsetMin     = Vector2.zero;
        rt.offsetMax     = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta     = Vector2.zero;
    }

    /// <summary>
    /// Sets the 9-slice border on a sprite asset.
    /// Only saves+reimports if the border changed.
    /// </summary>
    static void FixSpriteBorder(string path, int left, int bottom, int right, int top)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[HoleCompleteWidgetBuilder] FixSpriteBorder: no TextureImporter at {path}");
            return;
        }
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
        }
        var desiredBorder = new Vector4(left, bottom, right, top);
        if (importer.spriteBorder == desiredBorder) return;
        importer.spriteBorder = desiredBorder;
        importer.SaveAndReimport();
        Debug.Log($"[HoleCompleteWidgetBuilder] Fixed spriteBorder on {path} → L={left} B={bottom} R={right} T={top}");
    }

    static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
        }
        if (sprite == null) Debug.LogWarning($"[HoleCompleteWidgetBuilder] Sprite not found: {path}");
        return sprite;
    }

    static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) return c;
        return Color.white;
    }
}
