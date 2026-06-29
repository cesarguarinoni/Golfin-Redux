// ─────────────────────────────────────────────────────────────────────────────
// TournamentResultModalBuilder
// Editor tool: clones TournamentSignupModal.prefab → TournamentResultModal.prefab
// then restructures it per SPEC §4.1 (single CLAIM button, RANK band, green reward).
// Run via: MenuItem "GOLFIN > Tournaments > Build TournamentResultModal Prefab"
// Called by the implementer via script-execute. Safe to re-run (overwrites).
// ─────────────────────────────────────────────────────────────────────────────
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GolfinRedux.Editor.Tournaments
{
    public static class TournamentResultModalBuilder
    {
        private const string SourcePrefabPath = "Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab";
        private const string DestPrefabPath   = "Assets/Prefabs/UI/Modals/TournamentResultModal.prefab";

        // Separator sprite (from Signup prefab separator GO)
        private const string SeparatorSpriteGuid = "9e62d8f4ffd01e7468d07912ccba967a";

        // Font sizes from live Signup prefab (to match exactly; not applying a divisor formula)
        // Figma 64px RANK — use divisor derived from title (42px→32 = 1.3125): 64/1.3125 ≈ 48.76 → round to 48
        private const float RankFontSize = 48f;

        [MenuItem("GOLFIN/Tournaments/Build TournamentResultModal Prefab")]
        public static void BuildPrefab()
        {
            // --- 1. Load source prefab contents (edit mode, not instantiated in scene)
            var src = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            if (src == null)
            {
                Debug.LogError("[TRMBuilder] Could not load source prefab: " + SourcePrefabPath);
                return;
            }

            try
            {
                // --- 2. Rename root GameObject
                src.name = "TournamentResultModal";

                // --- 3. Remove TournamentSignupModalController; add TournamentResultModalController
                var oldCtrl = src.GetComponent<GolfinRedux.UI.Tournaments.TournamentSignupModalController>();
                if (oldCtrl != null)
                    Object.DestroyImmediate(oldCtrl);

                var newCtrl = src.AddComponent<GolfinRedux.UI.Tournaments.TournamentResultModalController>();

                // --- 4. Wire the base-class modalPanel (Panel child of root)
                var panelGO = src.transform.Find("Panel");
                if (panelGO != null)
                {
                    var so = new SerializedObject(newCtrl);
                    so.FindProperty("modalPanel").objectReferenceValue = panelGO.gameObject;
                    so.ApplyModifiedProperties();
                }

                // --- 5. Wire shared fields from existing children (by well-known name paths)
                // Actual hierarchy: Panel/Content/Upper/SponsorText  etc.
                WireField(newCtrl, src, "_sponsorText", "Panel/Content/Upper/SponsorText");
                WireField(newCtrl, src, "_titleText",   "Panel/Content/Upper/TitleText");
                WireField(newCtrl, src, "_venueText",   "Panel/Content/Upper/VenueText");
                WireField(newCtrl, src, "_dateLineText","Panel/Content/Upper/DateRangeText");

                // --- 6. Remove EntryPill (Signup-only element not in Prize modal)
                RemoveChildIfExists(src, "Panel/Content/EntryRewards/EntryPill");

                // --- 6b. Remove CancelButton from ButtonsRow; rename ConfirmButton → ClaimButton
                var buttonsRow = src.transform.Find("Panel/Content/ButtonsRow");
                if (buttonsRow != null)
                {
                    var cancelBtn = buttonsRow.Find("CancelButton");
                    if (cancelBtn != null) Object.DestroyImmediate(cancelBtn.gameObject);

                    var confirmBtn = buttonsRow.Find("ConfirmButton");
                    if (confirmBtn != null)
                    {
                        confirmBtn.gameObject.name = "ClaimButton";
                        // Relabel the button text
                        var txt = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();
                        if (txt != null) txt.text = "CLAIM";

                        // Wire claimButton field
                        var btn = confirmBtn.GetComponent<Button>();
                        WireButtonField(newCtrl, btn, "_claimButton");

                        // Add ButtonPressFeedback if not already present
                        if (confirmBtn.GetComponent<Golfin.UI.Polish.ButtonPressFeedback>() == null)
                            confirmBtn.gameObject.AddComponent<Golfin.UI.Polish.ButtonPressFeedback>();
                    }
                }

                // --- 7. Insert RANK band between the existing Separator and EntryRewards
                // Find the VLG container (Panel/Content holds Upper, Separator, EntryRewards, ButtonsRow)
                var panelVLGGO = FindContentVLGParent(src);
                if (panelVLGGO != null)
                {
                    // Locate the existing Separator GO (sits between Upper and EntryRewards in Signup)
                    var existingSep = FindChildByName(panelVLGGO, "Separator");

                    // Create a second separator (clone of the first)
                    var sepSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                        AssetDatabase.GUIDToAssetPath(SeparatorSpriteGuid));

                    // RANK band container
                    var rankGO = new GameObject("RankBand");
                    rankGO.transform.SetParent(panelVLGGO.transform, false);
                    var rankRT = rankGO.AddComponent<RectTransform>();
                    rankRT.sizeDelta = new Vector2(0, 0);
                    var rankVLG = rankGO.AddComponent<VerticalLayoutGroup>();
                    rankVLG.childAlignment = TextAnchor.MiddleCenter;
                    rankVLG.childControlWidth = true;
                    rankVLG.childControlHeight = true;
                    rankVLG.childForceExpandWidth = false;
                    rankVLG.childForceExpandHeight = false;
                    rankVLG.spacing = 0;

                    // RANK text child
                    var rankTextGO = new GameObject("RankText");
                    rankTextGO.transform.SetParent(rankGO.transform, false);
                    var rankRT2 = rankTextGO.AddComponent<RectTransform>();
                    rankRT2.sizeDelta = new Vector2(0, 96); // ~64px Figma
                    rankTextGO.AddComponent<CanvasRenderer>();
                    var rankTMP = rankTextGO.AddComponent<TextMeshProUGUI>();
                    rankTMP.text = "RANK #1";
                    rankTMP.fontSize = RankFontSize;
                    rankTMP.fontStyle = FontStyles.Bold;
                    rankTMP.color = Color.white;
                    rankTMP.alignment = TextAlignmentOptions.Center;
                    rankTMP.raycastTarget = false;
                    var leRank = rankTextGO.AddComponent<LayoutElement>();
                    leRank.preferredHeight = 96;
                    leRank.minHeight = 96;

                    // Second separator
                    var sep2GO = new GameObject("Separator2");
                    sep2GO.transform.SetParent(panelVLGGO.transform, false);
                    var sep2RT = sep2GO.AddComponent<RectTransform>();
                    sep2RT.sizeDelta = new Vector2(0, 2);
                    sep2GO.AddComponent<CanvasRenderer>();
                    var sep2Img = sep2GO.AddComponent<Image>();
                    if (sepSprite != null) sep2Img.sprite = sepSprite;
                    sep2Img.raycastTarget = false;
                    var sep2LE = sep2GO.AddComponent<LayoutElement>();
                    sep2LE.minWidth = 600;
                    sep2LE.minHeight = 2;
                    sep2LE.preferredHeight = 2;
                    sep2LE.flexibleWidth = 1;

                    // Reorder: Upper, Separator, RankBand, Separator2, EntryRewards, ButtonsRow
                    // Find the EntryRewards GO index and insert RankBand + Separator2 before it
                    int entryRewardsIdx = -1;
                    for (int i = 0; i < panelVLGGO.transform.childCount; i++)
                    {
                        var child = panelVLGGO.transform.GetChild(i);
                        if (child.name == "EntryRewards")
                        {
                            entryRewardsIdx = i;
                            break;
                        }
                    }
                    if (entryRewardsIdx >= 0)
                    {
                        rankGO.transform.SetSiblingIndex(entryRewardsIdx);
                        sep2GO.transform.SetSiblingIndex(entryRewardsIdx + 1);
                    }

                    // Wire rankText field
                    WireTMPField(newCtrl, rankTMP, "_rankText");
                }

                // --- 8. Set reward text color to green #73e080 (from Figma node 13498:2090)
                // After EntryPill removal, find by name anywhere under Panel/Content
                var entryRewards = src.transform.Find("Panel/Content/EntryRewards");
                if (entryRewards != null)
                {
                    var rewardRow = entryRewards.Find("RewardRow");
                    if (rewardRow == null) rewardRow = entryRewards;
                    var rewardTMP = FindTMPByName(entryRewards.gameObject, "RewardText");
                    if (rewardTMP != null)
                    {
                        rewardTMP.color = new Color(0x73 / 255f, 0xe0 / 255f, 0x80 / 255f, 1f);
                        rewardTMP.text = "12,000 + Trophy";
                        WireTMPField(newCtrl, rewardTMP, "_rewardText");
                    }

                    var rewardCoinIcon = FindImageByName(entryRewards.gameObject, "RewardCoinIcon");
                    if (rewardCoinIcon != null)
                        WireImageField(newCtrl, rewardCoinIcon, "_rewardCoinIcon");
                }

                // --- 9. Save
                PrefabUtility.SaveAsPrefabAsset(src, DestPrefabPath);
                Debug.Log($"[TRMBuilder] Saved → {DestPrefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(src);
            }

            AssetDatabase.Refresh();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void WireField(
            GolfinRedux.UI.Tournaments.TournamentResultModalController ctrl,
            GameObject root, string fieldName, string path)
        {
            var t = FindChildByPath(root.transform, path);
            if (t == null) { Debug.LogWarning($"[TRMBuilder] Path not found: {path}"); return; }
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null) { Debug.LogWarning($"[TRMBuilder] No TMP at: {path}"); return; }
            WireTMPField(ctrl, tmp, fieldName);
        }

        private static void WireTMPField(Object target, TextMeshProUGUI tmp, string fieldName)
        {
            var so = new SerializedObject(target);
            so.FindProperty(fieldName).objectReferenceValue = tmp;
            so.ApplyModifiedProperties();
        }

        private static void WireButtonField(Object target, Button btn, string fieldName)
        {
            var so = new SerializedObject(target);
            so.FindProperty(fieldName).objectReferenceValue = btn;
            so.ApplyModifiedProperties();
        }

        private static void WireImageField(Object target, Image img, string fieldName)
        {
            var so = new SerializedObject(target);
            so.FindProperty(fieldName).objectReferenceValue = img;
            so.ApplyModifiedProperties();
        }

        private static Transform? FindChildByPath(Transform root, string path)
        {
            var parts = path.Split('/');
            Transform current = root;
            foreach (var part in parts)
            {
                current = current.Find(part);
                if (current == null) return null;
            }
            return current;
        }

        private static Transform? FindChildByName(Transform root, string name)
        {
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                var found = FindChildByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform? FindChildByName(GameObject root, string name)
            => FindChildByName(root.transform, name);

        private static TextMeshProUGUI? FindTMPByName(GameObject root, string name)
        {
            foreach (var tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (tmp.gameObject.name == name) return tmp;
            return null;
        }

        private static Image? FindImageByName(GameObject root, string name)
        {
            foreach (var img in root.GetComponentsInChildren<Image>(true))
                if (img.gameObject.name == name) return img;
            return null;
        }

        private static void RemoveChildIfExists(GameObject root, string path)
        {
            var t = FindChildByPath(root.transform, path);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static GameObject? FindContentVLGParent(GameObject root)
        {
            // Actual hierarchy: Panel/Content holds Upper, Separator, EntryRewards, ButtonsRow
            var content = root.transform.Find("Panel/Content");
            if (content != null && content.Find("Upper") != null)
                return content.gameObject;
            // Fallback: search under Panel for a VLG with Upper child
            var panel = root.transform.Find("Panel");
            if (panel == null) return null;
            foreach (Transform child in panel)
            {
                var vlg = child.GetComponent<VerticalLayoutGroup>();
                if (vlg != null && child.Find("Upper") != null)
                    return child.gameObject;
            }
            return null;
        }
    }
}
