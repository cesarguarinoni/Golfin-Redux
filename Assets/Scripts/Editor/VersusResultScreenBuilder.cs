// ─────────────────────────────────────────────────────────────────────────────
// VersusResultScreenBuilder
// Editor tool: clones MatchMakingModal.prefab → VersusResultScreen.prefab
// then restructures it (add RESULTS header INSIDE panel, WINNER/LOSER labels,
// hole info, swap MatchmakingModalController → VersusResultScreenController).
// Mirrors TournamentResultModalBuilder pattern:
//   PrefabUtility.LoadPrefabContents → restructure → WireField via SerializedObject
//   → SaveAsPrefabAsset → UnloadPrefabContents
//
// Run via: MenuItem "GOLFIN/Versus/Build VersusResultScreen Prefab"
// Safe to re-run (overwrites).
//
// Source: MatchMakingModal.prefab (GUID 2bd69f22d1298854f9d7905d7375fef8)
// Dest:   Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab
//
// Source hierarchy (from InspectMMModalDeep 2026-07-01):
// MatchMakingModal
//   BG                  (Image - navy panel bg)
//   ContentArea         (VLG)
//     TitleArea         (HLG, Image) - DESTROY after extracting RESULTS label
//     InfoArea          (VLG, Image - inner panel)
//       Status          (TMP) - REPLACE with blank / remove
//       Portraits       (HLG, Image)
//         User1Info     (VLG, Image)
//           CharacterThumbnailCardGlowUp (CharacterThumbnailCard)
//           Username    (TMP)
//           Rank        (TMP)
//         UserLabel     (TMP "VS") - keep as Vs label
//         User2Info     (VLG, Image)
//           CharacterThumbnailCardGlowUp (CharacterThumbnailCard)
//           Username    (TMP)
//           Rank        (TMP)
//       Divider         (Image - separator, GUID 9e62d8f4ffd01e7468d07912ccba967a)
//       HoleTitle       (TMP) - REUSE for "HOLE" gold label
//       HoleInfo        (TMP) - REUSE for course/hole info
//       Divider         (Image - second separator)
//       Rewards         (HLG + CanvasGroup)
//         Reward Row1   (HLG, Image) { Reward1Icon, Reward1Amount }
//         Reward Row2   (HLG, Image) { Reward2Icon, Reward2Amount }
//         Reward Row3   (HLG, Image) { Reward3Icon, Reward3Amount }
//       Divider         (third separator)
//       CancelButton    (Button, Image) - REPLACE with NewMatchButton
//
// Result hierarchy after build:
// VersusResultScreen
//   BG                  (Image - navy panel bg)
//   ContentArea         (VLG)
//     InfoArea          (VLG, Image - inner panel)
//       ResultsHeader   (TMP - "RESULTS", white bold) [FIRST child, inside panel]
//       ColumnLabels    (HLG) WINNER | Spacer | LOSER
//       Portraits       (HLG)
//       Divider
//       HoleTitle
//       HoleInfo
//       Divider
//       Rewards         (HLG + CanvasGroup; icon/TMP children tinted on lose)
//       Divider
//       NewMatchButton
// ─────────────────────────────────────────────────────────────────────────────
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;
using Golfin.UI.Matchmaking;

namespace GolfinRedux.Editor.Versus
{
    public static class VersusResultScreenBuilder
    {
        // ── Source / dest paths ───────────────────────────────────────────────
        private const string SourcePrefabPath = "Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab";
        private const string DestPrefabPath   = "Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab";

        // ── Sprite GUIDs ──────────────────────────────────────────────────────
        // Gold CTA button (Button - Retry.png) — same as Tournament builder
        private const string BtnRetryGuid   = "aee5ccf2ef2d6b24ca9143186a08aa50";
        // Reward icons
        private const string RewardPtsGuid  = "e574289516ca3a340b6f3bea8fa9533a";
        private const string RewardRepGuid  = "daa7c57f705cdf04f8ad1dbef6eb02a7";
        private const string RewardBallGuid = "f7d5810099048784e8fbe582c498c4e8";

        // ── Font sizes (Figma ÷ 1.2; iter-7 — CESAR_REJECTION fix #8) ──────────────────────────
        // Shell Canvas 1170×2532 font convention: node_px ÷ 1.2 (memory feedback_shell_canvas_font_conversion)
        // ZERO inheritance from MMModal clone — every TMP has an explicit fontSize setter below.
        private const float FontHeaderSize   = 33f;   // RESULTS header     (node 13275:2331 = 39px ÷ 1.2 = 32.5 → 33f)
        private const float FontColumnLabel  = 38f;   // WINNER/LOSER/HOLE  (node 13275:2335/2358/2381 = 45px ÷ 1.2 = 37.5 → 38f)
        private const float FontCourseInfo   = 33f;   // course/hole line   (node 13275:2382 = 39px ÷ 1.2 = 32.5 → 33f)
        private const float FontUserInfo     = 25f;   // USERNAME + RANK    (node 13275:2353/2354 = 30px ÷ 1.2 = 25f)
        private const float FontRewardAmount = 43f;   // reward amounts     (node 13275:2390 = 51px ÷ 1.2 = 42.5 → 43f)
        private const float FontButtonText   = 55f;   // NEW MATCH button   (node I13275:2622;2180:1003 = 66px ÷ 1.2 = 55f — LARGEST)
        private const float FontVs           = 38f;   // "Vs." label        (node 13275:2355 = 45px ÷ 1.2 = 37.5 → 38f)

        // ── Colors ────────────────────────────────────────────────────────────
        private static readonly Color ColWhite   = Color.white;
        private static readonly Color ColGold    = new Color(0xEE/255f, 0xDC/255f, 0x9A/255f, 1f);
        private static readonly Color ColGreen   = new Color(0x50/255f, 0xC8/255f, 0x78/255f, 1f); // #50C878 (node 13274:877)
        private static readonly Color ColRed     = new Color(0xC0/255f, 0x40/255f, 0x00/255f, 1f); // #C04000 burnt orange (node 13275:2358)
        private static readonly Color ColBtnText = new Color(0x32/255f, 0x15/255f, 0x06/255f, 1f); // #321506 dark brown (node 13275:2622)

        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("GOLFIN/Versus/Build VersusResultScreen Prefab")]
        public static void BuildPrefab()
        {
            // 1. Load MatchMakingModal.prefab contents (edit mode, not instantiated in scene)
            var src = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            if (src == null)
            {
                Debug.LogError("[VRSBuilder] Could not load source prefab: " + SourcePrefabPath);
                return;
            }

            try
            {
                // 2. Rename root
                src.name = "VersusResultScreen";

                // 3. Remove MatchmakingModalController; add VersusResultScreenController
                var oldCtrl = src.GetComponent<MatchmakingModalController>();
                if (oldCtrl != null) Object.DestroyImmediate(oldCtrl);

                // Also remove any Button components on root that were part of the modal backdrop dismiss
                var rootBtn = src.GetComponent<Button>();
                if (rootBtn != null) Object.DestroyImmediate(rootBtn);

                var ctrl = src.AddComponent<VersusResultScreenController>();

                // 4. --- TITLE AREA → RESULTS HEADER INSIDE PANEL ---
                // TitleArea is ContentArea/TitleArea.
                // We extract a RESULTS TMP label, then destroy TitleArea entirely,
                // and insert the RESULTS header as the FIRST child of InfoArea (inside the navy panel).
                //
                // FIX (iter-2): previously the label was reparented to TitleArea which stayed in
                // ContentArea (outside InfoArea). Now we move it INTO ContentArea/InfoArea as
                // the first child — matching Figma nodes 13274:877 / 13275:2628 where RESULTS
                // sits inside the panel's top inset (SPEC §2 point 1).

                var infoArea = src.transform.Find("ContentArea/InfoArea");
                if (infoArea == null)
                {
                    Debug.LogError("[VRSBuilder] ContentArea/InfoArea not found.");
                    return;
                }

                // Create ResultsHeader TMP inside InfoArea (index 0 — first child)
                var resultsHeaderTMP = CreateTMPChild(infoArea.gameObject, "ResultsHeader", "RESULTS",
                    FontHeaderSize, FontStyles.Bold, ColWhite, TextAlignmentOptions.Center,
                    width: 0f, height: Mathf.RoundToInt(FontHeaderSize * 1.4f));
                // Force it to be sibling index 0 (before Status, Portraits, etc.)
                resultsHeaderTMP.transform.SetSiblingIndex(0);

                // Pin a LayoutElement so VLG respects the fixed height
                var rhLE = resultsHeaderTMP.gameObject.AddComponent<LayoutElement>();
                rhLE.minHeight       = Mathf.RoundToInt(FontHeaderSize * 1.4f);
                rhLE.preferredHeight = Mathf.RoundToInt(FontHeaderSize * 1.4f);

                // Now destroy TitleArea entirely (it's been superseded)
                var titleArea = src.transform.Find("ContentArea/TitleArea");
                if (titleArea != null) Object.DestroyImmediate(titleArea.gameObject);

                // 5. --- INFOAREA: insert WINNER/LOSER column labels above Portraits ---

                // 5a. Remove "Status" TMP (was the matchmaking status text)
                var statusGO = infoArea.Find("Status");
                if (statusGO != null) Object.DestroyImmediate(statusGO.gameObject);

                // 5b. Find Portraits GO
                var portraitsGO = infoArea.Find("Portraits");
                if (portraitsGO == null)
                {
                    Debug.LogError("[VRSBuilder] Portraits GO not found in InfoArea.");
                    return;
                }

                // 5c. Outcome labels are created per-column inside User1Info / User2Info in step 6.
                //     (Node 13274:877: label sits directly above each card in a flex-col grouping,
                //      NOT in a separate edge-spread HLG row. The ColumnLabels HLG approach was removed.)
                const float ColLabelHeight = 48f;     // ~FontColumnLabel * 1.4
                // Placeholders — populated in step 6 after user1Info/user2Info are resolved
                TextMeshProUGUI leftLabelTMP  = null;
                TextMeshProUGUI rightLabelTMP = null;

                // 5d. Pin Portraits height so VLG childForceExpandHeight cannot squash or overflow it.
                // InfoArea VLG has childForceExpandHeight=True and the fixed sizeDelta.y=1017.
                // Adding ResultsHeader (~98px) + ColumnLabels (48px) + extra VLG spacing = ~162px net.
                // Without a LayoutElement, VLG distributes ALL 1017px across all children, giving
                // Portraits potentially near-zero space (C3 trap in CLAUDE.md).
                // minHeight=425 guarantees the portraits row is at least card-height tall.
                // flexibleHeight=0 prevents VLG from growing it beyond preferredHeight.
                // Fix #4c iter-8: PortraitsHLG LE updated to 523 (= User1Info/User2Info RT.h after padBot fix)
                // so InfoArea VLG allocates the correct slot height for the portraits row.
                // Also disable childForceExpandHeight so User1Info/User2Info keep their own RT.h=523
                // rather than being forced to fill the HLG height (which previously capped them at 425px).
                var portraitsHLG = portraitsGO.GetComponent<HorizontalLayoutGroup>();
                if (portraitsHLG != null)
                    portraitsHLG.childForceExpandHeight = false; // let children use their own RT.h
                var portraitsLE = portraitsGO.gameObject.GetComponent<LayoutElement>()
                    ?? portraitsGO.gameObject.AddComponent<LayoutElement>();
                portraitsLE.minHeight       = 523f; // was 425 — now matches User1Info/User2Info RT.h
                portraitsLE.preferredHeight = 523f;
                portraitsLE.flexibleHeight  = 0f;
                // Fix iter-9c: InfoArea VLG childControlHeight=False → VLG uses RT.sizeDelta.y (not LE) as slot.
                // Must set RT.sizeDelta.y=523 directly so the physical slot matches the content height.
                var portraitsRT2 = portraitsGO.GetComponent<RectTransform>();
                if (portraitsRT2 != null)
                    portraitsRT2.sizeDelta = new Vector2(portraitsRT2.sizeDelta.x, 523f);

                // 5e. Expand InfoArea's fixed sizeDelta.y to fit the new ResultsHeader row so content
                // does not overflow the navy panel clip rect.
                // Original height = 1017.  Added: ResultsHeader (98px) + 1 extra VLG spacing entry.
                // Removed: Status (~50px) and ColumnLabels (48px, now inside portrait column stacks).
                // Net delta: +98 + spacing - 50 ≈ +58 + spacing.  Use +82 safe upper bound.
                var iaVLG = infoArea.GetComponent<VerticalLayoutGroup>();
                // Fix iter-9: InfoArea VLG childForceExpandHeight=True was stretching ALL children
                // to fill the container, overriding the Portraits LE min=523 and giving Portraits
                // only ~425px (proportional share). Must be False so each child uses its RT.sizeDelta.y
                // (or its LayoutElement min/preferred) as the authoritative slot height.
                if (iaVLG != null)
                    iaVLG.childForceExpandHeight = false;
                float extraSpacing = (iaVLG != null) ? iaVLG.spacing : 8f;
                float rhHeight = Mathf.RoundToInt(FontHeaderSize * 1.4f); // ResultsHeader height
                float netAdded = rhHeight + extraSpacing - 50f; // status removed ~50px
                if (netAdded < 0) netAdded = 0;
                var infoAreaRT = infoArea.GetComponent<RectTransform>();
                if (infoAreaRT != null)
                    infoAreaRT.sizeDelta = new Vector2(infoAreaRT.sizeDelta.x,
                        infoAreaRT.sizeDelta.y + netAdded);

                // 6. --- PORTRAITS: extract left/right card references, relabel UserLabel → Vs ---
                //     Also create per-column WINNER/LOSER outcome labels as FIRST children
                //     of each User#Info VLG — matching Figma node 13274:877's per-column layout
                //     (label directly above card, not in a separate edge-spread HLG row).
                var user1Info = portraitsGO.Find("User1Info");
                var user2Info = portraitsGO.Find("User2Info");
                var userLabel = portraitsGO.Find("UserLabel");

                // Create outcome labels inside each column — index 0 so label is above the card
                if (user1Info != null)
                {
                    // Fix #1 (CESAR_REJECTION iter-7): WINNER label → FontStyles.Normal (not Bold)
                    leftLabelTMP = CreateTMPChild(user1Info.gameObject, "LeftOutcomeLabel", "WINNER",
                        FontColumnLabel, FontStyles.Normal, ColGreen, TextAlignmentOptions.Center,
                        width: 0f, height: ColLabelHeight);
                    leftLabelTMP.transform.SetSiblingIndex(0);
                    // LayoutElement so VLG doesn't squash it
                    var le = leftLabelTMP.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = ColLabelHeight; le.preferredHeight = ColLabelHeight;

                    // Fix #4 iter-8: padBot 16→48 so RANK→sep1 gap renders ~56-72px (self-review: was only 20px)
                    var u1Vlg = user1Info.GetComponent<VerticalLayoutGroup>();
                    if (u1Vlg != null)
                    {
                        u1Vlg.spacing = 8f;
                        u1Vlg.padding = new RectOffset(0, 0, 0, 48); // 48px bottom (RANK → Portraits bottom edge)
                    }
                    // Fix #4b iter-8: set RT height to contain all content + padBot
                    // Content = 48(WINNER)+8+343(portrait)+8+30(Username)+8+30(Rank) = 475px; padBot=48 → total=523
                    // Without this, childControlHeight=False lets children overflow the container, negating padBot.
                    var u1RT = user1Info.GetComponent<RectTransform>();
                    if (u1RT != null)
                        u1RT.sizeDelta = new Vector2(u1RT.sizeDelta.x, 523f);
                }
                if (user2Info != null)
                {
                    // Fix #1 (CESAR_REJECTION iter-7): LOSER label → FontStyles.Normal (not Bold)
                    rightLabelTMP = CreateTMPChild(user2Info.gameObject, "RightOutcomeLabel", "LOSER",
                        FontColumnLabel, FontStyles.Normal, ColRed, TextAlignmentOptions.Center,
                        width: 0f, height: ColLabelHeight);
                    rightLabelTMP.transform.SetSiblingIndex(0);
                    var le = rightLabelTMP.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = ColLabelHeight; le.preferredHeight = ColLabelHeight;

                    // Fix #4 iter-8: mirror padBot 16→48
                    var u2Vlg = user2Info.GetComponent<VerticalLayoutGroup>();
                    if (u2Vlg != null)
                    {
                        u2Vlg.spacing = 8f;
                        u2Vlg.padding = new RectOffset(0, 0, 0, 48); // 48px bottom
                    }
                    // Fix #4b iter-8: mirror RT height fix for right column
                    var u2RT = user2Info.GetComponent<RectTransform>();
                    if (u2RT != null)
                        u2RT.sizeDelta = new Vector2(u2RT.sizeDelta.x, 523f);
                }

                CharacterThumbnailCard leftCard  = null;
                CharacterThumbnailCard rightCard = null;
                TextMeshProUGUI leftUsernameTMP  = null;
                TextMeshProUGUI leftRankTMP      = null;
                TextMeshProUGUI rightUsernameTMP = null;
                TextMeshProUGUI rightRankTMP     = null;

                if (user1Info != null)
                {
                    leftCard = user1Info.GetComponentInChildren<CharacterThumbnailCard>(true);
                    leftUsernameTMP = FindTMPByName(user1Info.gameObject, "Username");
                    leftRankTMP     = FindTMPByName(user1Info.gameObject, "Rank");

                    if (leftUsernameTMP != null)
                    {
                        leftUsernameTMP.text            = "USERNAME";
                        leftUsernameTMP.fontSize        = FontUserInfo;      // node 13275:2353 = 30px ÷ 1.2 = 25f
                        leftUsernameTMP.fontStyle       = FontStyles.Bold;   // Fix #3 (CESAR_REJECTION): USERNAME must be Bold
                        leftUsernameTMP.color           = ColWhite;
                        leftUsernameTMP.enableWordWrapping = false;
                        leftUsernameTMP.overflowMode    = TextOverflowModes.Overflow;
                        leftUsernameTMP.alignment       = TextAlignmentOptions.Center;
                    }
                    if (leftRankTMP != null)
                    {
                        // Fix #5 (CESAR_REJECTION): "RANK:" WHITE, only the number colored via rich text
                        leftRankTMP.text             = "RANK: <color=#50C878>#142</color>";
                        leftRankTMP.fontSize         = FontUserInfo;         // 30px ÷ 1.2 = 25f
                        leftRankTMP.fontStyle        = FontStyles.Normal;
                        leftRankTMP.color            = ColWhite;             // base color = white; rich text handles number color
                        leftRankTMP.richText         = true;
                        leftRankTMP.enableWordWrapping = false;
                        leftRankTMP.overflowMode     = TextOverflowModes.Overflow;
                        leftRankTMP.alignment        = TextAlignmentOptions.Center;
                    }
                }

                if (user2Info != null)
                {
                    rightCard = user2Info.GetComponentInChildren<CharacterThumbnailCard>(true);
                    rightUsernameTMP = FindTMPByName(user2Info.gameObject, "Username");
                    rightRankTMP     = FindTMPByName(user2Info.gameObject, "Rank");

                    if (rightUsernameTMP != null)
                    {
                        rightUsernameTMP.text            = "USERNAME";
                        rightUsernameTMP.fontSize        = FontUserInfo;     // 30px ÷ 1.2 = 25f
                        rightUsernameTMP.fontStyle       = FontStyles.Bold;  // Fix #3 (CESAR_REJECTION): USERNAME must be Bold
                        rightUsernameTMP.color           = ColWhite;
                        rightUsernameTMP.enableWordWrapping = false;
                        rightUsernameTMP.overflowMode    = TextOverflowModes.Overflow;
                        rightUsernameTMP.alignment       = TextAlignmentOptions.Center;
                    }
                    if (rightRankTMP != null)
                    {
                        // Fix #5 (CESAR_REJECTION): "RANK:" WHITE, only the number colored via rich text
                        rightRankTMP.text             = "RANK: <color=#C04000>#255</color>";
                        rightRankTMP.fontSize         = FontUserInfo;        // 30px ÷ 1.2 = 25f
                        rightRankTMP.fontStyle        = FontStyles.Normal;
                        rightRankTMP.color            = ColWhite;            // base color = white; rich text handles number color
                        rightRankTMP.richText         = true;
                        rightRankTMP.enableWordWrapping = false;
                        rightRankTMP.overflowMode     = TextOverflowModes.Overflow;
                        rightRankTMP.alignment        = TextAlignmentOptions.Center;
                    }
                }

                // Style the "VS" label (UserLabel) as "Vs."
                if (userLabel != null)
                {
                    var vsTMP = userLabel.GetComponent<TextMeshProUGUI>();
                    if (vsTMP != null)
                    {
                        vsTMP.text      = "Vs.";
                        vsTMP.fontSize  = FontVs;
                        vsTMP.fontStyle = FontStyles.Bold;
                        vsTMP.color     = ColWhite;
                        vsTMP.alignment = TextAlignmentOptions.Center;
                        vsTMP.enableWordWrapping = false;
                        vsTMP.overflowMode = TextOverflowModes.Overflow;
                    }
                }

                // 7. --- HOLE TITLE + HOLE INFO ---
                var holeTitleGO = infoArea.Find("HoleTitle");
                var holeInfoGO  = infoArea.Find("HoleInfo");
                TextMeshProUGUI holeInfoTMP = null;

                if (holeTitleGO != null)
                {
                    var htTMP = holeTitleGO.GetComponent<TextMeshProUGUI>();
                    if (htTMP != null)
                    {
                        htTMP.text      = "HOLE";
                        htTMP.fontSize  = FontColumnLabel;   // 45px ÷ 1.2 = 38f (node 13275:2381)
                        htTMP.color     = ColGold;
                        htTMP.fontStyle = FontStyles.Bold;
                        htTMP.alignment = TextAlignmentOptions.Center;
                        htTMP.enableWordWrapping = false;
                        htTMP.overflowMode = TextOverflowModes.Overflow;
                    }
                    // Fix #6 iter-8: InfoArea VLG childControlHeight=False means LE alone won't
                    // override child height — must set RT.sizeDelta.y directly.
                    // Target 46px slot so sep1→HOLE text gap renders 12-24px (was 41px with 53px slot).
                    var holeTitleRT = holeTitleGO.GetComponent<RectTransform>();
                    if (holeTitleRT != null)
                        holeTitleRT.sizeDelta = new Vector2(holeTitleRT.sizeDelta.x, 46f);
                    var holeTitleLE = holeTitleGO.GetComponent<LayoutElement>()
                        ?? holeTitleGO.gameObject.AddComponent<LayoutElement>();
                    holeTitleLE.minHeight       = 46f;
                    holeTitleLE.preferredHeight = 46f;
                }

                if (holeInfoGO != null)
                {
                    holeInfoTMP = holeInfoGO.GetComponent<TextMeshProUGUI>();
                    if (holeInfoTMP != null)
                    {
                        holeInfoTMP.text      = "Lomond Country Club  - Hole 5";
                        holeInfoTMP.fontSize  = FontCourseInfo;  // 39px ÷ 1.2 = 33f
                        holeInfoTMP.color     = ColWhite;
                        holeInfoTMP.fontStyle = FontStyles.Normal;
                        holeInfoTMP.alignment = TextAlignmentOptions.Center;
                        holeInfoTMP.enableWordWrapping = false;
                        holeInfoTMP.overflowMode = TextOverflowModes.Overflow;
                    }
                    // Pin HoleInfo RT height directly (VLG childControlHeight=False)
                    var holeInfoRT = holeInfoGO.GetComponent<RectTransform>();
                    if (holeInfoRT != null)
                        holeInfoRT.sizeDelta = new Vector2(holeInfoRT.sizeDelta.x, Mathf.RoundToInt(FontCourseInfo * 1.4f));
                    var holeInfoLE = holeInfoGO.GetComponent<LayoutElement>()
                        ?? holeInfoGO.gameObject.AddComponent<LayoutElement>();
                    holeInfoLE.minHeight       = Mathf.RoundToInt(FontCourseInfo * 1.4f); // ~46px
                    holeInfoLE.preferredHeight = Mathf.RoundToInt(FontCourseInfo * 1.4f);
                }

                // 8. --- REWARD ROW: add CanvasGroup, wire reward icon Images + amount TMPs ---
                // Dimming in ShowLose() tints _reward[1-3]Icon.color AND _reward[1-3]Amount.color
                // directly — child Image.color IS processed by Camera.Render in edit-mode capture.
                // CanvasGroup.alpha is used for runtime compositing (play mode).
                var rewardsGO = infoArea.Find("Rewards");
                CanvasGroup rewardGroup = null;
                Image r1Icon = null, r2Icon = null, r3Icon = null;
                TextMeshProUGUI r1Amt = null, r2Amt = null, r3Amt = null;

                if (rewardsGO != null)
                {
                    // Add CanvasGroup for runtime alpha (play mode)
                    rewardGroup = rewardsGO.GetComponent<CanvasGroup>();
                    if (rewardGroup == null) rewardGroup = rewardsGO.gameObject.AddComponent<CanvasGroup>();
                    rewardGroup.alpha = 1f;

                    // Wire reward icon sprites from GUIDs and find amount TMPs + icon Images
                    var ptsSprite  = LoadSprite(RewardPtsGuid,  "Reward Points");
                    var repSprite  = LoadSprite(RewardRepGuid,  "Reward Repair");
                    var ballSprite = LoadSprite(RewardBallGuid, "Reward Ball");

                    var row1 = rewardsGO.Find("Reward Row1");
                    var row2 = rewardsGO.Find("Reward Row2");
                    var row3 = rewardsGO.Find("Reward Row3");

                    if (row1 != null)
                    {
                        var iconT = row1.Find("Reward1Icon");
                        r1Icon = iconT?.GetComponent<Image>();
                        if (r1Icon != null && ptsSprite != null) r1Icon.sprite = ptsSprite;
                        r1Amt = row1.Find("Reward1Amount")?.GetComponent<TextMeshProUGUI>();
                        if (r1Amt != null)
                        {
                            r1Amt.text      = "x200";
                            r1Amt.fontSize  = FontRewardAmount;
                            r1Amt.color     = ColWhite;
                            r1Amt.fontStyle = FontStyles.Bold;
                            r1Amt.enableWordWrapping = false;
                            r1Amt.overflowMode = TextOverflowModes.Overflow;
                        }
                    }
                    if (row2 != null)
                    {
                        var iconT = row2.Find("Reward2Icon");
                        r2Icon = iconT?.GetComponent<Image>();
                        if (r2Icon != null && repSprite != null) r2Icon.sprite = repSprite;
                        r2Amt = row2.Find("Reward2Amount")?.GetComponent<TextMeshProUGUI>();
                        if (r2Amt != null)
                        {
                            r2Amt.text      = "x04";
                            r2Amt.fontSize  = FontRewardAmount;
                            r2Amt.color     = ColWhite;
                            r2Amt.fontStyle = FontStyles.Bold;
                            r2Amt.enableWordWrapping = false;
                            r2Amt.overflowMode = TextOverflowModes.Overflow;
                        }
                    }
                    if (row3 != null)
                    {
                        var iconT = row3.Find("Reward3Icon");
                        r3Icon = iconT?.GetComponent<Image>();
                        if (r3Icon != null && ballSprite != null) r3Icon.sprite = ballSprite;
                        r3Amt = row3.Find("Reward3Amount")?.GetComponent<TextMeshProUGUI>();
                        if (r3Amt != null)
                        {
                            r3Amt.text      = "x02";
                            r3Amt.fontSize  = FontRewardAmount;
                            r3Amt.color     = ColWhite;
                            r3Amt.fontStyle = FontStyles.Bold;
                            r3Amt.enableWordWrapping = false;
                            r3Amt.overflowMode = TextOverflowModes.Overflow;
                        }
                    }
                }

                // 9. --- CANCEL → NEW MATCH BUTTON ---
                var cancelBtnGO = infoArea.Find("CancelButton");
                Button newMatchBtn = null;

                if (cancelBtnGO != null)
                {
                    cancelBtnGO.gameObject.name = "NewMatchButton";
                    newMatchBtn = cancelBtnGO.GetComponent<Button>();

                    // Replace sprite with gold CTA button
                    var btnImg = cancelBtnGO.GetComponent<Image>();
                    var btnSprite = LoadSprite(BtnRetryGuid, "Button - Retry");
                    if (btnImg != null && btnSprite != null)
                    {
                        btnImg.sprite = btnSprite;
                        btnImg.type   = Image.Type.Sliced;
                    }

                    // Relabel text — dark brown per node 13275:2622 (#321506)
                    // Fix #7 (CESAR_REJECTION): NEW MATCH button text → FontStyles.Normal (not Bold)
                    var btnTMP = cancelBtnGO.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (btnTMP != null)
                    {
                        btnTMP.text      = "NEW MATCH";
                        btnTMP.fontSize  = FontButtonText;
                        btnTMP.color     = ColBtnText;
                        btnTMP.fontStyle = FontStyles.Normal; // Fix #7: Regular weight (not Bold)
                        btnTMP.alignment = TextAlignmentOptions.Center;
                        btnTMP.enableWordWrapping = false;
                        btnTMP.overflowMode = TextOverflowModes.Overflow;
                    }

                    // Add ButtonPressFeedback if not already present (CLAUDE.md rule 11)
                    if (cancelBtnGO.GetComponent<Golfin.UI.Polish.ButtonPressFeedback>() == null)
                        cancelBtnGO.gameObject.AddComponent<Golfin.UI.Polish.ButtonPressFeedback>();
                }

                // 10. --- WIRE VersusResultScreenController fields ---
                var soCtrl = new SerializedObject(ctrl);
                WireObjField(soCtrl, "_leftOutcomeLabel",  leftLabelTMP);
                WireObjField(soCtrl, "_rightOutcomeLabel", rightLabelTMP);
                if (leftCard        != null) WireObjField(soCtrl, "_leftCard",          leftCard);
                if (rightCard       != null) WireObjField(soCtrl, "_rightCard",         rightCard);
                if (leftUsernameTMP != null) WireObjField(soCtrl, "_leftUsernameText",  leftUsernameTMP);
                if (rightUsernameTMP!= null) WireObjField(soCtrl, "_rightUsernameText", rightUsernameTMP);
                if (leftRankTMP     != null) WireObjField(soCtrl, "_leftRankText",      leftRankTMP);
                if (rightRankTMP    != null) WireObjField(soCtrl, "_rightRankText",     rightRankTMP);
                if (holeInfoTMP     != null) WireObjField(soCtrl, "_holeInfoText",      holeInfoTMP);
                if (rewardGroup     != null) WireObjField(soCtrl, "_rewardRowGroup",    rewardGroup);
                // Wire icon Images for per-child Camera.Render-visible dimming
                if (r1Icon          != null) WireObjField(soCtrl, "_reward1Icon",       r1Icon);
                if (r2Icon          != null) WireObjField(soCtrl, "_reward2Icon",       r2Icon);
                if (r3Icon          != null) WireObjField(soCtrl, "_reward3Icon",       r3Icon);
                if (r1Amt           != null) WireObjField(soCtrl, "_reward1Amount",     r1Amt);
                if (r2Amt           != null) WireObjField(soCtrl, "_reward2Amount",     r2Amt);
                if (r3Amt           != null) WireObjField(soCtrl, "_reward3Amount",     r3Amt);
                if (newMatchBtn     != null) WireObjField(soCtrl, "_newMatchButton",    newMatchBtn);
                soCtrl.ApplyModifiedProperties();

                // 11. Default to WIN state for prefab preview (bright rewards, WINNER label left)
                ctrl.ShowWin();

                // 12. Save
                string dir = Path.GetDirectoryName(DestPrefabPath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                PrefabUtility.SaveAsPrefabAsset(src, DestPrefabPath);
                Debug.Log("[VRSBuilder] Saved → " + DestPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(src);
            }

            AssetDatabase.Refresh();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static TextMeshProUGUI CreateTMPChild(
            GameObject parent, string name, string text,
            float size, FontStyles style, Color color,
            TextAlignmentOptions align,
            float width = 0f, float height = -1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<CanvasRenderer>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text              = text;
            tmp.fontSize          = size;
            tmp.fontStyle         = style;
            tmp.color             = color;
            tmp.alignment         = align;
            tmp.raycastTarget     = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode      = TextOverflowModes.Overflow;
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                float h = height >= 0f ? height : Mathf.RoundToInt(size * 1.4f);
                rt.sizeDelta = new Vector2(width, h);
            }
            return tmp;
        }

        private static void WireObjField(SerializedObject so, string fieldName, Object value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning("[VRSBuilder] Field not found: " + fieldName);
                return;
            }
            prop.objectReferenceValue = value;
        }

        private static Sprite LoadSprite(string guid, string label)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[VRSBuilder] GUID not found for " + label + ": " + guid);
                return null;
            }
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.LogWarning("[VRSBuilder] Sprite not loaded for " + label + " at " + path);
            return sprite;
        }

        private static TextMeshProUGUI FindTMPByName(GameObject root, string name)
        {
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (tmp.gameObject.name == name) return tmp;
            return null;
        }
    }
}
