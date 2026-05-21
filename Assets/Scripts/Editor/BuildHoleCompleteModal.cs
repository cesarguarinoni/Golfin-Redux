using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Iteration-4 prefab builder: clones the lab HoleCompleteWidget, removes Card2
/// and next-hole elements, adds HoleCompleteModalController, wires all fields.
/// Run via GOLFIN/Dev/Build HoleCompleteModal From Lab.
/// </summary>
public static class BuildHoleCompleteModal
{
    [MenuItem("GOLFIN/Dev/Build HoleCompleteModal From Lab")]
    public static void Build()
    {
        string labPath    = "Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab";
        string outputPath = "Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab";

        var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(labPath);
        if (labPrefab == null) { Debug.LogError("[BuildModal] Lab prefab not found: " + labPath); return; }

        // Object.Instantiate creates a plain disconnected clone (not a Prefab Variant).
        // This is what we want — SaveAsPrefabAsset will then create a standalone prefab.
        var inst = Object.Instantiate(labPrefab);
        inst.name = "HoleCompleteModal";

        // Remove HoleCompleteWidget component
        var widget = inst.GetComponent<Golfin.Gameplay.UI.ShotUI.HoleCompleteWidget>();
        if (widget != null) Object.DestroyImmediate(widget);

        // Remove HoleCompleteCardWidget components from children
        foreach (var cw in inst.GetComponentsInChildren<Golfin.Gameplay.UI.ShotUI.HoleCompleteCardWidget>(true))
            Object.DestroyImmediate(cw);

        // Lab hierarchy: HoleCompleteWidget (root) → Root + DimBackground (siblings)
        // Root → Card1 + Card2 (children)
        Transform rootTr = inst.transform.Find("Root");
        if (rootTr == null) { Debug.LogError("[BuildModal] Root not found"); Object.DestroyImmediate(inst); return; }

        // DimBackground is a direct child of the ROOT GO (sibling of Root, NOT under Root)
        Transform dimBg = inst.transform.Find("DimBackground");
        if (dimBg == null) Debug.LogWarning("[BuildModal] DimBackground not found under root GO");
        else Debug.Log("[BuildModal] DimBackground found: " + dimBg.gameObject.name + " (active=" + dimBg.gameObject.activeSelf + ")");

        Transform card1 = rootTr.Find("Card1");
        if (card1 == null) { Debug.LogError("[BuildModal] Card1 not found under Root"); Object.DestroyImmediate(inst); return; }

        // Delete Card2
        Transform card2 = rootTr.Find("Card2");
        if (card2 != null) { Object.DestroyImmediate(card2.gameObject); Debug.Log("[BuildModal] Card2 deleted"); }

        // Find ContentRoot inside Card1
        Transform contentRoot = card1.Find("ContentRoot");
        if (contentRoot == null) { Debug.LogError("[BuildModal] ContentRoot not found under Card1"); Object.DestroyImmediate(inst); return; }

        // Delete next-hole-only elements from ContentRoot
        foreach (var name in new[] { "NextBody", "NextHeader", "LockedHeader" })
        {
            var child = contentRoot.Find(name);
            if (child != null) { Object.DestroyImmediate(child.gameObject); Debug.Log("[BuildModal] Deleted: " + name); }
        }

        // DarkenOverlay is a direct child of Card1 (not ContentRoot)
        var darkenOverlay = card1.Find("DarkenOverlay");
        if (darkenOverlay != null) { Object.DestroyImmediate(darkenOverlay.gameObject); Debug.Log("[BuildModal] Deleted: DarkenOverlay"); }

        // Adapt Buttons group: ReplayButton → PlayNextButton; add MenuButton
        Transform buttonsGrp = contentRoot.Find("Buttons");
        if (buttonsGrp != null)
        {
            // ReplayButton → PlayNextButton
            Transform replayBtn = buttonsGrp.Find("ReplayButton");
            if (replayBtn != null)
            {
                replayBtn.name = "PlayNextButton";
                var lbl = replayBtn.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                {
                    lbl.text = "PLAY NEXT";
                    // Enable auto-size so the label shrinks to fit the button bounds
                    lbl.enableAutoSizing  = true;
                    lbl.fontSizeMin       = 8f;
                    lbl.fontSizeMax       = lbl.fontSize > 0 ? lbl.fontSize : 24f;
                    lbl.overflowMode      = TextOverflowModes.Overflow;
                    lbl.enableWordWrapping = false;
                    // Stretch label RT to fill button so auto-size has room
                    var lblRt = lbl.GetComponent<RectTransform>();
                    if (lblRt != null)
                    {
                        lblRt.anchorMin = Vector2.zero;
                        lblRt.anchorMax = Vector2.one;
                        lblRt.sizeDelta = Vector2.zero;
                        lblRt.anchoredPosition = Vector2.zero;
                    }
                }
                Debug.Log("[BuildModal] ReplayButton renamed to PlayNextButton");
            }

            // Ensure RetryButton label
            Transform retryBtn = buttonsGrp.Find("RetryButton");
            if (retryBtn != null)
            {
                var lbl = retryBtn.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                {
                    lbl.text             = "RETRY";
                    lbl.enableAutoSizing = true;
                    lbl.fontSizeMin      = 10f;
                    lbl.fontSizeMax      = lbl.fontSize > 0 ? lbl.fontSize : 24f;
                    lbl.enableWordWrapping = false;
                    lbl.overflowMode     = TextOverflowModes.Overflow;
                    var lblRt = lbl.GetComponent<RectTransform>();
                    if (lblRt != null) { lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one; lblRt.sizeDelta = Vector2.zero; lblRt.anchoredPosition = Vector2.zero; }
                }
            }

            // Add MenuButton: clone RetryButton as silver MENU
            if (retryBtn != null)
            {
                var menuGO  = Object.Instantiate(retryBtn.gameObject, buttonsGrp);
                menuGO.name = "MenuButton";
                var lbl = menuGO.GetComponentInChildren<TMP_Text>();
                if (lbl != null)
                {
                    lbl.text             = "MENU";
                    lbl.enableAutoSizing = true;
                    lbl.fontSizeMin      = 10f;
                    lbl.fontSizeMax      = lbl.fontSize > 0 ? lbl.fontSize : 24f;
                    lbl.enableWordWrapping = false;
                    lbl.overflowMode     = TextOverflowModes.Overflow;
                    var lblRt = lbl.GetComponent<RectTransform>();
                    if (lblRt != null) { lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one; lblRt.sizeDelta = Vector2.zero; lblRt.anchoredPosition = Vector2.zero; }
                }
                // Ensure it is active
                menuGO.SetActive(true);
                Debug.Log("[BuildModal] MenuButton added by cloning RetryButton");
            }
        }

        // Ensure DimBackground starts inactive (ModalController.Show activates backdrop)
        if (dimBg   != null) dimBg.gameObject.SetActive(false);
        // Ensure Root starts inactive (ModalController.Show activates modalPanel)
        rootTr.gameObject.SetActive(false);

        // Ensure inst has Canvas at sortingOrder=900
        var canvas = inst.GetComponent<Canvas>();
        if (canvas == null) canvas = inst.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder    = 900;
        if (inst.GetComponent<GraphicRaycaster>() == null) inst.AddComponent<GraphicRaycaster>();
        if (inst.GetComponent<CanvasScaler>() == null)
        {
            var cs = inst.AddComponent<CanvasScaler>();
            cs.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080f, 1920f);
            cs.matchWidthOrHeight  = 0.5f;
        }

        // Add HoleCompleteModalController
        var ctrl = inst.AddComponent<Golfin.UI.Modals.Result.HoleCompleteModalController>();
        var so   = new SerializedObject(ctrl);
        so.Update();

        // ── ModalController base-class fields ──────────────────────────────────

        SetObjRef(so, "modalPanel", rootTr != null ? rootTr.gameObject : null);
        SetObjRef(so, "backdrop",   dimBg   != null ? dimBg.gameObject  : null);

        // ── Lab card fields ────────────────────────────────────────────────────

        var succHdr = contentRoot.Find("SuccessHeader");
        SetObjRef(so, "_successHeaderRoot", succHdr != null ? succHdr.gameObject : null);

        var failHdr = contentRoot.Find("FailedHeader");
        SetObjRef(so, "_failedHeaderRoot", failHdr != null ? failHdr.gameObject : null);

        var subhead = contentRoot.Find("Subhead");
        if (subhead != null)
        {
            var tmp = subhead.GetComponent<TMP_Text>() ?? subhead.GetComponentInChildren<TMP_Text>();
            SetObjRef(so, "_subheadText", tmp);
        }

        var currentBody = contentRoot.Find("CurrentBody");
        if (currentBody != null)
        {
            var holeMapTr = currentBody.Find("HoleMapLarge");
            if (holeMapTr != null) SetObjRef(so, "_holeMapLarge", holeMapTr.GetComponent<Image>());

            var statsTr = currentBody.Find("StatsBlockText");
            if (statsTr != null) SetObjRef(so, "_statsBlockText", statsTr.GetComponent<TMP_Text>());
        }

        var rewardsRow = contentRoot.Find("RewardsRow");
        if (rewardsRow != null)
        {
            var cg = rewardsRow.GetComponent<CanvasGroup>();
            if (cg == null) cg = rewardsRow.gameObject.AddComponent<CanvasGroup>();
            SetObjRef(so, "_rewardsCanvasGroup", cg);

            var coinTr = rewardsRow.Find("CoinReward/CountText");
            if (coinTr != null)
            {
                SetObjRef(so, "_rewardCoinText", coinTr.GetComponent<TMP_Text>());
                var t = coinTr.GetComponent<TMP_Text>();
                if (t != null) { t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; }
            }

            var repairTr = rewardsRow.Find("RepairReward/CountText");
            if (repairTr != null)
            {
                SetObjRef(so, "_rewardRepairText", repairTr.GetComponent<TMP_Text>());
                var t = repairTr.GetComponent<TMP_Text>();
                if (t != null) { t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; }
            }

            var ballTr = rewardsRow.Find("BallReward/CountText");
            if (ballTr != null)
            {
                SetObjRef(so, "_rewardBallText", ballTr.GetComponent<TMP_Text>());
                var t = ballTr.GetComponent<TMP_Text>();
                if (t != null) { t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; }
            }
        }

        if (buttonsGrp != null)
        {
            var pnTr = buttonsGrp.Find("PlayNextButton");
            if (pnTr != null) SetObjRef(so, "_playNextButton", pnTr.GetComponent<Button>());

            var rtTr = buttonsGrp.Find("RetryButton");
            if (rtTr != null) SetObjRef(so, "_retryButton", rtTr.GetComponent<Button>());

            var mnTr = buttonsGrp.Find("MenuButton");
            if (mnTr != null)
            {
                SetObjRef(so, "_menuButton",      mnTr.GetComponent<Button>());
                SetObjRef(so, "_menuButtonImage", mnTr.GetComponent<Image>());
            }
        }

        so.ApplyModifiedProperties();

        // Save prefab
        bool ok;
        PrefabUtility.SaveAsPrefabAsset(inst, outputPath, out ok);
        Object.DestroyImmediate(inst);

        Debug.Log(ok
            ? "[BuildModal] SUCCESS: HoleCompleteModal.prefab saved at " + outputPath
            : "[BuildModal] FAILED to save HoleCompleteModal.prefab");

        AssetDatabase.Refresh();
    }

    static void SetObjRef(SerializedObject so, string fieldName, Object obj)
    {
        var p = so.FindProperty(fieldName);
        if (p == null) { Debug.LogWarning("[BuildModal] Field not found: " + fieldName); return; }
        p.objectReferenceValue = obj;
        if (obj == null) Debug.LogWarning("[BuildModal] Null value for field: " + fieldName);
    }
}
// force refresh Thu May 21 14:25:00 CEST 2026 — label RT stretch fix for PLAY NEXT
