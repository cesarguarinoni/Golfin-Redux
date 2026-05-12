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
/// §2d iter-9: Builds the HoleCompleteWidget + HoleCompleteDriver hierarchy in LabScaffold.unity.
///
/// Menu: GOLFIN/Build/Build HoleComplete Widgets (§2d)
///
/// Iter-9 changes (F1-F5 from ARCHITECT_REVIEW_FAIL iter-8):
/// F1 — HUD bleed-through fix:
///   - Raised HoleCompleteWidget overlay Canvas sortingOrder from 100 → 33000 (above all HUD canvases).
///   - Added CentralBall + CentralBallWidget to by-name suppression list in HoleCompleteWidget.SuppressHUD().
/// F2/F3 — LOCKED Card 2 DarkenOverlay + rewards opacity restored:
///   - Card GO is now a FRAME (LayoutElement + CSF + BG Image) with NO VLG directly.
///   - ContentRoot child (stretch fill) holds the VLG with all content.
///   - DarkenOverlay is a sibling of ContentRoot (direct child of card GO), so stretch anchors work.
///   - BindNextHole(locked=true) sets _rewardsCanvasGroup.alpha=0.5f (was already coded; now structurally correct).
/// F4 — LOCKED Card 2 height:
///   - _cardLayoutElement wired to HoleCompleteCardWidget.
///   - BindNextHole(locked=true) sets minHeight=0; unlocked keeps minHeight=855.
///   - CSF+ContentRoot resolves short ~280-360px height for locked cards.
/// F5 — Description font size + longer placeholder:
///   - fontSize=21 (already in iter-8; preserved).
///   - Placeholder text updated to long Figma-reference tip that demonstrates 600px column wrapping.
///
/// Dress-up (iter-9 Cesar standing rule — "Always dress up the designs even if you fill them on runtime"):
///   - Card 1: real Hole 1 map sprite assigned at build time (HoleMaps/Lomond - Hole 1.png).
///             Subhead: "Lomond Country Club  - Hole 1 - Par 5"
///             Stats text: realistic multi-line with green STROKES color: "TEE OFF: REGULAR / STROKES: 4 (BIRDIE) [green] / ..."
///             Header: SuccessHeader visible by default (FailedHeader/NextHeader/LockedHeader hidden).
///   - Card 2: real Hole 2 map sprite assigned at build time (HoleMaps/Lomond - Hole 2.png).
///             Subhead: "Lomond Country Club  - Hole 2 - Par 4"
///             Description: architect's tip string (already set in BuildCard, see F5).
///             Header: NextHeader visible by default (SuccessHeader/FailedHeader/LockedHeader hidden).
///             Body: NextBody active, CurrentBody inactive (mirror of what BindNextHole(unlocked) does).
///   This is purely for Editor-mode layout preview. Runtime Show(data) still calls BindCurrentHole/BindNextHole.
///
/// Iter-7 changes (preserved):
/// F1 — Divider height fix:
///   - Card VLG: childControlHeight=true (was false) → VLG now reads LayoutElement.preferredHeight
///     on ALL children including dividers. Previously the VLG ignored preferredHeight=8 on dividers
///     and instead used their RectTransform.sizeDelta, which defaulted to 0 and caused dividers to
///     stretch to fill available card height — rendering as 30-40px bright bars obscuring content.
///   - Divider LayoutElement: flexibleHeight=0 (defense in depth — prevents VLG expansion).
///   - Divider Image: type=Simple (not Sliced — sprite has 0-px borders, Sliced was wrong type).
///   - Divider Image: preserveAspect=false (divider fills full card width, not native 978:2 aspect).
/// F2 — Card 2 description text fix:
///   - NextBody infoColVLG: childControlHeight=true (same root cause as F1 — was false).
///     Now the NextHoleDescText LayoutElement.preferredHeight=148 is respected.
///
/// Iter-6 changes (preserved):
/// - Added horizontal dividers between card sections (Figma: 2px white separator lines)
/// - Rewards row: MiddleCenter alignment + childForceExpandWidth=false
/// - Card BG: ContentSizeFitter verticalFit=PreferredSize
/// - Removed green-square thumbnail
/// - Card 2 NextBody: hole-select-style layout (map + par label + description text)
/// - HoleCompleteDriver.ShowResultScreen() overrides maps at runtime.
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

        // Dress-up sprites: real hole maps for build-time preview (iter-9 Cesar standing rule).
        // Runtime overrides these via BindCurrentHole(data.HoleMap) / BindNextHole(data.NextHoleMap).
        Sprite holeMap1 = LoadSprite("Assets/Art/In-Game UI/HoleMaps/Lomond - Hole 1.png");
        Sprite holeMap2 = LoadSprite("Assets/Art/In-Game UI/HoleMaps/Lomond - Hole 2.png");

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

        // Overlay Canvas so this renders on top of ALL HUD canvases (including
        // CameraModeDebugHUD @ 32760 and any other HUD overlay).
        // §2d iter-9 F1: use 32767 (max signed 16-bit) NOT 33000 — 33000 overflows Unity's serialized
        // short and is stored/read back as -32536, placing the canvas BELOW all HUDs.
        // CameraModeDebugCanvas is at 32760, so 32767 is the safe maximum that still beats it.
        var overlayCvs = widgetGO.AddComponent<Canvas>();
        overlayCvs.overrideSorting = true;
        overlayCvs.sortingOrder = 32767;
        // §2d F1 serialization fix: setting sortingOrder=33000 in a prior iteration caused it to be
        // serialized as a signed-16-bit overflow (-32536) and read back incorrectly. 32767 fits in
        // signed 16-bit. Use SerializedObject to force-write the value so it lands in YAML correctly.
        {
            var so = new UnityEditor.SerializedObject(overlayCvs);
            so.FindProperty("m_SortingOrder").intValue = 32767;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        widgetGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // DimBackground — near-opaque black to subdue gameplay HUD.
        // §2d iter-8: SetActive(false) at build time. HoleCompleteWidget.Show() re-enables it;
        // Hide() disables it. Previously it was always-active, dimming gameplay even when modal was hidden.
        var dimGO = CreateStretchGO("DimBackground", widgetGO.transform);
        var dimImg = dimGO.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, 0.92f);
        dimImg.raycastTarget = true;
        dimGO.SetActive(false); // §2d iter-8: default inactive — enabled by Show(), disabled by Hide()

        // Root — child content; Awake hides this
        var rootGO = CreateStretchGO("Root", widgetGO.transform);
        // §2d iter-8: MiddleCenter so the two cards cluster vertically centered on screen.
        // childControlHeight=false so each card's own ContentSizeFitter drives its height.
        // childForceExpandHeight=false prevents cards from being stretched to fill the screen.
        var vLayout = rootGO.AddComponent<VerticalLayoutGroup>();
        vLayout.padding = new RectOffset(48, 48, 24, 24);
        vLayout.spacing = 24;
        vLayout.childAlignment = TextAnchor.MiddleCenter; // iter-8: was UpperCenter → cards at top
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

        // ── Dress-up Card1 (iter-9: real content at build time for Editor preview) ──
        // Card1 shows the SUCCESS state with Hole 1 result stats.
        // Header: SUCCESS visible (already default from BuildCard).
        // Map: real Hole 1 map sprite instead of placeholder.
        // Subhead: "Lomond Country Club  - Hole 1 - Par 5" (Par 5 correction).
        // Stats: realistic multi-line with green-colored STROKES birdie line.
        {
            var contentRoot1 = card1GO.transform.Find("ContentRoot");
            if (contentRoot1 != null)
            {
                var subhead1 = contentRoot1.Find("Subhead");
                if (subhead1 != null)
                {
                    var tmp = subhead1.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "Lomond Country Club  - Hole 1 - Par 5";
                }
                var currentBody1 = contentRoot1.Find("CurrentBody");
                if (currentBody1 != null)
                {
                    var mapImg1 = currentBody1.Find("HoleMapLarge")?.GetComponent<Image>();
                    if (mapImg1 != null && holeMap1 != null) mapImg1.sprite = holeMap1;
                    var statsTmp1 = currentBody1.Find("StatsBlockText")?.GetComponent<TextMeshProUGUI>();
                    if (statsTmp1 != null)
                        statsTmp1.text = "<b>TEE OFF:</b> REGULAR\n<b>STROKES:</b> <color=#50C878>4 (BIRDIE)</color>\n<b>BEST:</b> 5 (PAR)\n<b>TIME:</b> 00:02:34\n<b>BEST:</b> 00:02:34";
                }
            }
        }

        // ── 7. Build Card2 ────────────────────────────────────────────────────
        var card2GO = BuildCard("Card2", rootGO.transform,
            holeCardBG, iconCheck, iconX, lockIcon,
            holeMap, darkenImg,
            coinIcon, repairIcon, ballIcon,
            null, null, playBtnBG,
            dividerSprite,
            rubikSemiBold, bodyFont,
            out var card2);

        // ── Dress-up Card2 (iter-9: real content at build time for Editor preview) ──
        // Card2 shows the NEXT state (unlocked) with Hole 2 info.
        // Header: NEXT visible (SuccessHeader hidden).
        // Map: real Hole 2 map sprite.
        // Subhead: "Lomond Country Club  - Hole 2 - Par 4".
        // Body: NextBody active, CurrentBody inactive (mirrors BindNextHole(unlocked)).
        // Description: architect's tip string (already set by BuildCard F5).
        {
            var contentRoot2 = card2GO.transform.Find("ContentRoot");
            if (contentRoot2 != null)
            {
                // Switch header: SuccessHeader off → NextHeader on
                var successHdr = contentRoot2.Find("SuccessHeader");
                var nextHdr    = contentRoot2.Find("NextHeader");
                if (successHdr != null) successHdr.gameObject.SetActive(false);
                if (nextHdr    != null) nextHdr.gameObject.SetActive(true);

                var subhead2 = contentRoot2.Find("Subhead");
                if (subhead2 != null)
                {
                    var tmp = subhead2.GetComponent<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "Lomond Country Club  - Hole 2 - Par 4";
                }

                // Switch body: CurrentBody off → NextBody on
                var currentBody2 = contentRoot2.Find("CurrentBody");
                var nextBody2    = contentRoot2.Find("NextBody");
                if (currentBody2 != null) currentBody2.gameObject.SetActive(false);
                if (nextBody2    != null) nextBody2.gameObject.SetActive(true);

                // Assign real Hole 2 map sprite
                if (nextBody2 != null)
                {
                    var mapImg2 = nextBody2.Find("NextHoleMapLarge")?.GetComponent<Image>();
                    if (mapImg2 != null && holeMap2 != null) mapImg2.sprite = holeMap2;
                }

                // Buttons: PLAY active, REPLAY+RETRY inactive (mirrors BindNextHole(unlocked))
                var buttons2 = contentRoot2.Find("Buttons");
                if (buttons2 != null)
                {
                    var replayBtn2 = buttons2.Find("ReplayButton");
                    var retryBtn2  = buttons2.Find("RetryButton");
                    var playBtn2   = buttons2.Find("PlayButton");
                    if (replayBtn2 != null) replayBtn2.gameObject.SetActive(false);
                    if (retryBtn2  != null) retryBtn2.gameObject.SetActive(false);
                    if (playBtn2   != null) playBtn2.gameObject.SetActive(true);
                }
            }
        }

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

        Debug.Log("[HoleCompleteWidgetBuilder] §2d iter-9: HoleCompleteWidget + HoleCompleteDriver built and saved to LabScaffold.unity.");
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
        // Card root — 978px wide. ContentSizeFitter drives height from ContentRoot children.
        // §2d iter-9: Card GO is a FRAME (LayoutElement + CSF + BG Image) with NO VLG directly.
        // Two children: ContentRoot (VLG with all content) + DarkenOverlay (stretch sibling).
        // This lets DarkenOverlay use stretch anchors to fill the card at its resolved height,
        // instead of being a VLG child (where stretch anchors are ignored).
        var cardGO = new GameObject(name);
        cardGO.transform.SetParent(parent, false);
        var cardRT = cardGO.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(978, 0); // height driven by CSF

        // §2d iter-8: minHeight=855 to match Figma card height (unlocked). iter-9 F4: locked sets to 0 at runtime.
        var le = cardGO.AddComponent<LayoutElement>();
        le.preferredWidth = 978;
        le.minHeight = 855;

        // Background image (9-slice)
        var bgImg = cardGO.AddComponent<Image>();
        bgImg.sprite = cardBG;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        // ContentSizeFitter — card height auto-fits ContentRoot preferred size
        var csf = cardGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        card = cardGO.AddComponent<HoleCompleteCardWidget>();

        // ContentRoot — stretch fill, holds the VLG with all content.
        // §2d iter-9: moved VLG from cardGO to this child so DarkenOverlay can be a true stretch sibling.
        var contentRootGO = new GameObject("ContentRoot");
        contentRootGO.transform.SetParent(cardGO.transform, false);
        var contentRootRT = contentRootGO.AddComponent<RectTransform>();
        StretchFill(contentRootRT);

        // Vertical layout for card contents (was on cardGO in iter-8, now on ContentRoot)
        // iter-7 fix: childControlHeight=true so LayoutElement.preferredHeight is respected on all children
        var layout = contentRootGO.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 0; // spacing handled by dividers + per-element padding
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ContentRoot also needs a CSF so the outer card CSF can read its preferred size.
        var contentRootCSF = contentRootGO.AddComponent<ContentSizeFitter>();
        contentRootCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Use contentRootGO as the VLG parent for all content children.
        var contentParent = contentRootGO.transform;

        // ── Header section ──────────────────────────────────────────────────
        // §2d iter-9: all content children now parented to contentParent (ContentRoot), not cardGO.
        var successHeader = BuildIconTextHeader("SuccessHeader", contentParent, checkIcon, "SUCCESS",
            HexToColor("50C878"), headingFont, 32f);

        var failedHeader = BuildIconTextHeader("FailedHeader", contentParent, xIcon, "FAILED",
            HexToColor("D16A47"), headingFont, 32f);
        failedHeader.SetActive(false);

        var nextHeader = BuildTextOnlyHeader("NextHeader", contentParent, "NEXT",
            HexToColor("EEDC9A"), headingFont, 32f);
        nextHeader.SetActive(false);

        var lockedHeader = BuildIconTextHeader("LockedHeader", contentParent, lockIcon, "LOCKED",
            HexToColor("C8C8C8"), headingFont, 32f);
        lockedHeader.SetActive(false);

        // ── Subhead ─────────────────────────────────────────────────────────
        var subheadGO = new GameObject("Subhead");
        subheadGO.transform.SetParent(contentParent, false);
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
        BuildDivider("Divider_BelowSubhead", contentParent, dividerSprite);

        // ── Current Body ─────────────────────────────────────────────────────
        var currentBodyGO = new GameObject("CurrentBody");
        currentBodyGO.transform.SetParent(contentParent, false);
        // §2d iter-8: 336px = 288px map + 24+24 py padding (matches Figma py-24 on content container).
        var currentBodyLE = currentBodyGO.AddComponent<LayoutElement>();
        currentBodyLE.preferredHeight = 336;
        // §2d iter-8: MiddleCenter so map + stats cluster centered horizontally+vertically in body row.
        // childForceExpand=false so children stay at their natural sizes and are centered as a unit.
        var currentBodyHLG = currentBodyGO.AddComponent<HorizontalLayoutGroup>();
        currentBodyHLG.padding = new RectOffset(32, 32, 24, 24); // matches Figma px-32 py-24 on content container
        currentBodyHLG.spacing = 24; // Figma gap-24
        currentBodyHLG.childAlignment = TextAnchor.MiddleCenter; // iter-8: was UpperLeft → left-aligned
        currentBodyHLG.childControlHeight = false;
        currentBodyHLG.childControlWidth = false;
        currentBodyHLG.childForceExpandWidth = false;
        currentBodyHLG.childForceExpandHeight = false;

        // Map: 156×288 per FIGMA_EXTRACT (node "Hole 1 - Map 2": 155.61×288.5 — rounded up).
        // iter-8: height corrected from 200 → 288 to match Figma.
        Image mapImg = null;
        {
            var mapGO = new GameObject("HoleMapLarge");
            mapGO.transform.SetParent(currentBodyGO.transform, false);
            mapGO.AddComponent<RectTransform>().sizeDelta = new Vector2(156, 288);
            mapImg = mapGO.AddComponent<Image>();
            mapImg.sprite = map;
            mapImg.preserveAspect = true;
            mapImg.raycastTarget = false;
        }

        // Stats block: Goals Container 500w per Figma. Give it flexible width to fill remaining space.
        var statsGO = new GameObject("StatsBlockText");
        statsGO.transform.SetParent(currentBodyGO.transform, false);
        var statsLE = statsGO.AddComponent<LayoutElement>();
        statsLE.preferredWidth = 500;
        statsLE.preferredHeight = 288; // match map height
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
        nextBodyGO.transform.SetParent(contentParent, false);
        // §2d iter-8: 336px = 288px map + 24+24 py padding (mirrors currentBodyLE for consistent card height).
        var nextBodyLE = nextBodyGO.AddComponent<LayoutElement>();
        nextBodyLE.preferredHeight = 336;
        nextBodyGO.SetActive(false);

        Image nextMapImg   = null;
        TMP_Text nextDescTmp = null;
        {
            // §2d iter-8: MiddleCenter so map + description cluster centered horizontally.
            // childForceExpand=false so children stay at natural sizes.
            var nextBodyHLG = nextBodyGO.AddComponent<HorizontalLayoutGroup>();
            nextBodyHLG.padding = new RectOffset(32, 32, 24, 24); // Figma px-32 py-24
            nextBodyHLG.spacing = 24; // Figma gap-24
            nextBodyHLG.childAlignment = TextAnchor.MiddleCenter; // iter-8: was UpperLeft → left-aligned
            nextBodyHLG.childControlHeight = false;
            nextBodyHLG.childControlWidth = false;
            nextBodyHLG.childForceExpandWidth = false;
            nextBodyHLG.childForceExpandHeight = false;

            // Map: 156×288 per Figma (next hole). iter-8: height corrected from 200 → 288.
            var mapGO2 = new GameObject("NextHoleMapLarge");
            mapGO2.transform.SetParent(nextBodyGO.transform, false);
            mapGO2.AddComponent<RectTransform>().sizeDelta = new Vector2(156, 288);
            nextMapImg = mapGO2.AddComponent<Image>();
            nextMapImg.sprite = map; // placeholder at build time; overridden at runtime
            nextMapImg.preserveAspect = true;
            nextMapImg.raycastTarget = false;

            // §2d iter-8: Info column is ONLY description text — no par label (Figma has no separate
            // "Par 4" title in Card 2 body; Par is already in the subhead line).
            // Width widened from 500 → 600px so text wraps into readable lines, not vertical noodles.
            // The remaining width after map: 978 - 64(px-32×2) - 156(map) - 24(gap) = 734px; 600 fits.
            var infoColGO = new GameObject("NextHoleInfoCol");
            infoColGO.transform.SetParent(nextBodyGO.transform, false);
            // Add RectTransform with explicit sizeDelta so the HLG (childControlHeight=false) can read the height.
            infoColGO.AddComponent<RectTransform>().sizeDelta = new Vector2(600, 288);
            var infoColLE = infoColGO.AddComponent<LayoutElement>();
            infoColLE.preferredWidth = 600; // iter-8: was 500 → widened for readable word wrap
            infoColLE.preferredHeight = 288;

            // Description text fills the full info column width with word wrap enabled.
            var descGO = new GameObject("NextHoleDescText");
            descGO.transform.SetParent(infoColGO.transform, false);
            var descRT = descGO.AddComponent<RectTransform>();
            descRT.anchorMin = Vector2.zero;
            descRT.anchorMax = Vector2.one;
            descRT.sizeDelta = Vector2.zero;
            nextDescTmp = descGO.AddComponent<TextMeshProUGUI>();
            // §2d iter-9 F5: use a longer placeholder that demonstrates column width with wrapping.
            nextDescTmp.text = "The tee shot is best aimed at the sloping area in the center of the two-tiered fairway, where the right side is wide. The landing spot of the second shot is crucial.";
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
        BuildDivider("Divider_BelowBody", contentParent, dividerSprite);

        // ── Rewards Row ───────────────────────────────────────────────────────
        var rewardsGO = new GameObject("RewardsRow");
        rewardsGO.transform.SetParent(contentParent, false);
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
        BuildDivider("Divider_BelowRewards", contentParent, dividerSprite);

        // ── Buttons ───────────────────────────────────────────────────────────
        var buttonsGO = new GameObject("Buttons");
        buttonsGO.transform.SetParent(contentParent, false);
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
        // §2d iter-8: _nextHoleParText removed from CardWidget — not wired here.
        // Only _nextHoleDescText remains for the description column.
        if (nextDescTmp != null) cardSO.FindProperty("_nextHoleDescText").objectReferenceValue = nextDescTmp;
        cardSO.FindProperty("_rewardsCanvasGroup").objectReferenceValue = cg;
        cardSO.FindProperty("_rewardCoinText").objectReferenceValue    = coinTmp;
        cardSO.FindProperty("_rewardRepairText").objectReferenceValue  = repairTmp;
        cardSO.FindProperty("_rewardBallText").objectReferenceValue    = ballTmp;
        if (replayBtn != null) cardSO.FindProperty("_replayButton").objectReferenceValue = replayBtn;
        if (retryBtn  != null) cardSO.FindProperty("_retryButton").objectReferenceValue  = retryBtn;
        if (playBtn   != null) cardSO.FindProperty("_playButton").objectReferenceValue   = playBtn;
        cardSO.FindProperty("_darkenOverlay").objectReferenceValue      = darkenGO;
        // §2d iter-9 F4: wire cardLayoutElement so BindNextHole(locked=true) can set minHeight=0
        cardSO.FindProperty("_cardLayoutElement").objectReferenceValue  = le;
        cardSO.ApplyModifiedProperties();

        Debug.Log($"[HoleCompleteWidgetBuilder] Card '{name}' built and wired (iter-9).");
        return cardGO;
    }

    // ── Divider builder (iter-8) ─────────────────────────────────────────────

    /// <summary>
    /// §2d iter-8: Canonical divider pattern from ClubCompareRightPanelBuilder.BuildDivider().
    /// DIVIDER_H = 1f (per ClubCompareRightPanelBuilder line 48).
    /// White Image at 10% alpha, no sprite, no 9-slicing.
    /// Per CESAR_REJECTION iter-7 item #6: "just copy the existing divider implementation."
    /// </summary>
    static void BuildDivider(string name, Transform parent, Sprite dividerSprite)
    {
        const float DIVIDER_H = 1f; // canonical ClubCompareRightPanelBuilder.DIVIDER_H
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = DIVIDER_H;
        le.minHeight       = DIVIDER_H;
        le.flexibleHeight  = 0;
        // Canonical pattern: plain white Image at 10% alpha, no sprite.
        go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
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
