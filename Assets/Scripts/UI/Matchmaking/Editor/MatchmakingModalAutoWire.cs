#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.UI.Matchmaking;
using GolfinRedux.UI;

namespace Golfin.UI.Matchmaking.Editor
{
    /// <summary>
    /// Auto-wires MatchmakingModalController fields in the active scene.
    /// Also wires HomeScreenController.matchmakingModal cross-reference.
    ///
    /// Expected hierarchy under MatchMakingModal (scene instance):
    ///   BG                                                      → backdrop
    ///   ContentArea/InfoArea/CancelButton                       → closeButton (base), cancelButton
    ///   ContentArea/InfoArea/Portraits/User1Info/CharacterThumbnailCardGlowUp → playerCard
    ///   ContentArea/InfoArea/Portraits/User1Info/Username       → playerUsernameText
    ///   ContentArea/InfoArea/Portraits/User1Info/Rank           → playerRankText
    ///   ContentArea/InfoArea/Portraits/User2Info/CharacterThumbnailCardGlowUp → opponentCard
    ///   ContentArea/InfoArea/Portraits/User2Info/Username       → opponentUsernameText
    ///   ContentArea/InfoArea/Portraits/User2Info/Rank           → opponentRankText
    ///   ContentArea/InfoArea/Status                             → statusText
    ///   ContentArea/InfoArea/HoleTitle                          → holeTitleText
    ///   ContentArea/InfoArea/HoleInfo                           → holeInfoText
    ///   ContentArea/InfoArea/Rewards/Reward Row1                → rewardRow1
    ///   ContentArea/InfoArea/Rewards/Reward Row1/Reward1Icon    → reward1Icon
    ///   ContentArea/InfoArea/Rewards/Reward Row1/Reward1Amount  → reward1Amount
    ///   ContentArea/InfoArea/Rewards/Reward Row2                → rewardRow2
    ///   ContentArea/InfoArea/Rewards/Reward Row2/Reward2Icon    → reward2Icon
    ///   ContentArea/InfoArea/Rewards/Reward Row2/Reward2Amount  → reward2Amount
    ///   ContentArea/InfoArea/Rewards/Reward Row3                → rewardRow3
    ///   ContentArea/InfoArea/Rewards/Reward Row3/Reward3Icon    → reward3Icon
    ///   ContentArea/InfoArea/Rewards/Reward Row3/Reward3Amount  → reward3Amount
    ///
    /// Run: GOLFIN/Wire/Matchmaking Modal
    /// </summary>
    public static class MatchmakingModalAutoWire
    {
        private const string HOLE_DATABASE_PATH = "Assets/Data/HoleDatabase.asset";

        [MenuItem("GOLFIN/Wire/Matchmaking Modal")]
        public static void Run()
        {
            // ── Find MatchmakingModalController ──────────────────────────────
            MatchmakingModalController modal = null;
            foreach (var m in Resources.FindObjectsOfTypeAll<MatchmakingModalController>())
            {
                if (m.gameObject.scene.isLoaded) { modal = m; break; }
            }

            if (modal == null)
            {
                Debug.LogError("[MatchmakingModalAutoWire] FAILED — MatchmakingModalController not found in the active scene. Add the component to the MatchMakingModal GameObject first, then run this again.");
                return;
            }

            var so   = new SerializedObject(modal);
            var root = modal.transform;
            int wired = 0, failed = 0;

            // ── Helpers ───────────────────────────────────────────────────────

            void Fail(string prop, string path, string reason = "path not found")
            {
                Debug.LogWarning($"[MatchmakingModalAutoWire] FAILED '{prop}' at '{path}' — {reason}. Wire manually.");
                failed++;
            }

            int WireTMP(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); return 0; }
                var c = t.GetComponent<TextMeshProUGUI>();
                if (c == null) { Fail(prop, path, "no TMP component"); return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[MatchmakingModalAutoWire] OK {prop}"); wired++; return 1;
            }

            int WireImage(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); return 0; }
                var c = t.GetComponent<Image>();
                if (c == null) { Fail(prop, path, "no Image component"); return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[MatchmakingModalAutoWire] OK {prop}"); wired++; return 1;
            }

            int WireButton(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); return 0; }
                var c = t.GetComponent<Button>();
                if (c == null) { Fail(prop, path, "no Button component"); return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[MatchmakingModalAutoWire] OK {prop}"); wired++; return 1;
            }

            int WireGameObject(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); return 0; }
                so.FindProperty(prop).objectReferenceValue = t.gameObject;
                Debug.Log($"[MatchmakingModalAutoWire] OK {prop}"); wired++; return 1;
            }

            // ── Base ModalController fields ───────────────────────────────────
            // modalPanel: wire to ContentArea (NOT the root GO — see SPEC deviation note)
            {
                var contentAreaT = root.Find("ContentArea");
                if (contentAreaT != null)
                {
                    so.FindProperty("modalPanel").objectReferenceValue = contentAreaT.gameObject;
                    Debug.Log("[MatchmakingModalAutoWire] OK modalPanel -> ContentArea"); wired++;
                }
                else
                {
                    Fail("modalPanel", "ContentArea");
                }
            }

            // backdrop
            {
                var bgT = root.Find("BG");
                if (bgT != null)
                {
                    so.FindProperty("backdrop").objectReferenceValue = bgT.gameObject;
                    Debug.Log("[MatchmakingModalAutoWire] OK backdrop -> BG"); wired++;
                }
                else
                {
                    Fail("backdrop", "BG");
                }
            }

            // closeButton (base ModalController field)
            WireButton("closeButton", "ContentArea/InfoArea/CancelButton");

            // ── Player side ───────────────────────────────────────────────────
            {
                var playerCardT = root.Find("ContentArea/InfoArea/Portraits/User1Info/CharacterThumbnailCardGlowUp");
                if (playerCardT != null)
                {
                    var card = playerCardT.GetComponent<Golfin.Roster.CharacterThumbnailCard>();
                    if (card != null)
                    {
                        so.FindProperty("playerCard").objectReferenceValue = card;
                        Debug.Log("[MatchmakingModalAutoWire] OK playerCard"); wired++;
                    }
                    else { Fail("playerCard", "ContentArea/InfoArea/Portraits/User1Info/CharacterThumbnailCardGlowUp", "no CharacterThumbnailCard"); }
                }
                else { Fail("playerCard", "ContentArea/InfoArea/Portraits/User1Info/CharacterThumbnailCardGlowUp"); }
            }

            WireTMP("playerUsernameText", "ContentArea/InfoArea/Portraits/User1Info/Username");
            WireTMP("playerRankText",     "ContentArea/InfoArea/Portraits/User1Info/Rank");

            // ── Opponent side ─────────────────────────────────────────────────
            {
                var opponentCardT = root.Find("ContentArea/InfoArea/Portraits/User2Info/CharacterThumbnailCardGlowUp");
                if (opponentCardT != null)
                {
                    var card = opponentCardT.GetComponent<Golfin.Roster.CharacterThumbnailCard>();
                    if (card != null)
                    {
                        so.FindProperty("opponentCard").objectReferenceValue = card;
                        Debug.Log("[MatchmakingModalAutoWire] OK opponentCard"); wired++;
                    }
                    else { Fail("opponentCard", "ContentArea/InfoArea/Portraits/User2Info/CharacterThumbnailCardGlowUp", "no CharacterThumbnailCard"); }
                }
                else { Fail("opponentCard", "ContentArea/InfoArea/Portraits/User2Info/CharacterThumbnailCardGlowUp"); }
            }

            WireTMP("opponentUsernameText", "ContentArea/InfoArea/Portraits/User2Info/Username");
            WireTMP("opponentRankText",     "ContentArea/InfoArea/Portraits/User2Info/Rank");

            // ── Status / hole / rewards ───────────────────────────────────────
            WireTMP("statusText",   "ContentArea/InfoArea/Status");
            WireTMP("holeTitleText","ContentArea/InfoArea/HoleTitle");
            WireTMP("holeInfoText", "ContentArea/InfoArea/HoleInfo");

            WireGameObject("rewardRow1",    "ContentArea/InfoArea/Rewards/Reward Row1");
            WireImage("reward1Icon",        "ContentArea/InfoArea/Rewards/Reward Row1/Reward1Icon");
            WireTMP("reward1Amount",        "ContentArea/InfoArea/Rewards/Reward Row1/Reward1Amount");

            WireGameObject("rewardRow2",    "ContentArea/InfoArea/Rewards/Reward Row2");
            WireImage("reward2Icon",        "ContentArea/InfoArea/Rewards/Reward Row2/Reward2Icon");
            WireTMP("reward2Amount",        "ContentArea/InfoArea/Rewards/Reward Row2/Reward2Amount");

            WireGameObject("rewardRow3",    "ContentArea/InfoArea/Rewards/Reward Row3");
            WireImage("reward3Icon",        "ContentArea/InfoArea/Rewards/Reward Row3/Reward3Icon");
            WireTMP("reward3Amount",        "ContentArea/InfoArea/Rewards/Reward Row3/Reward3Amount");

            // ── cancelButton (separate field on controller) ───────────────────
            WireButton("cancelButton", "ContentArea/InfoArea/CancelButton");

            // ── HoleDatabase asset ────────────────────────────────────────────
            {
                var holeDb = AssetDatabase.LoadAssetAtPath<HoleDatabase>(HOLE_DATABASE_PATH);
                if (holeDb != null)
                {
                    so.FindProperty("holeDatabase").objectReferenceValue = holeDb;
                    Debug.Log("[MatchmakingModalAutoWire] OK holeDatabase"); wired++;
                }
                else
                {
                    Debug.LogWarning($"[MatchmakingModalAutoWire] holeDatabase not found at '{HOLE_DATABASE_PATH}' — wire manually.");
                    failed++;
                }
            }

            // ── Reward icon sprites (copy from prefab's Image slots) ──────────
            {
                var icon1T = root.Find("ContentArea/InfoArea/Rewards/Reward Row1/Reward1Icon");
                var icon2T = root.Find("ContentArea/InfoArea/Rewards/Reward Row2/Reward2Icon");
                var icon3T = root.Find("ContentArea/InfoArea/Rewards/Reward Row3/Reward3Icon");

                Sprite sprite1 = icon1T != null ? icon1T.GetComponent<Image>()?.sprite : null;
                Sprite sprite2 = icon2T != null ? icon2T.GetComponent<Image>()?.sprite : null;
                Sprite sprite3 = icon3T != null ? icon3T.GetComponent<Image>()?.sprite : null;

                if (sprite1 != null)
                {
                    so.FindProperty("pointsIcon").objectReferenceValue = sprite1;
                    Debug.Log("[MatchmakingModalAutoWire] OK pointsIcon (from Reward1Icon sprite)"); wired++;
                }
                else { Debug.LogWarning("[MatchmakingModalAutoWire] pointsIcon: Reward1Icon has no sprite — wire manually."); failed++; }

                if (sprite2 != null)
                {
                    so.FindProperty("repairKitIcon").objectReferenceValue = sprite2;
                    Debug.Log("[MatchmakingModalAutoWire] OK repairKitIcon (from Reward2Icon sprite)"); wired++;
                }
                else { Debug.LogWarning("[MatchmakingModalAutoWire] repairKitIcon: Reward2Icon has no sprite — wire manually."); failed++; }

                if (sprite3 != null)
                {
                    so.FindProperty("ballIcon").objectReferenceValue = sprite3;
                    Debug.Log("[MatchmakingModalAutoWire] OK ballIcon (from Reward3Icon sprite)"); wired++;
                }
                else { Debug.LogWarning("[MatchmakingModalAutoWire] ballIcon: Reward3Icon has no sprite — wire manually."); failed++; }
            }

            // ── Home screen elements (hidden while modal is open) ─────────────
            // These are siblings under HomeScreen, which lives under Canvas/ScreensRoot.
            // We traverse the scene's root objects to find them — they are in a
            // different hierarchy branch from the MatchMakingModal.
            {
                GameObject homeNoticePanel = null;
                GameObject homeNextHolePanel = null;

                foreach (var rootGO in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    // Search for HomeScreen regardless of nesting depth
                    var homeScreenT = rootGO.transform.Find("Canvas/ScreensRoot/HomeScreen") ??
                                     rootGO.transform.Find("ScreensRoot/HomeScreen") ??
                                     FindChildRecursive(rootGO.transform, "HomeScreen");

                    if (homeScreenT != null)
                    {
                        var noticeT    = homeScreenT.Find("NoticePanel");
                        var nextHoleT  = homeScreenT.Find("NextHolePanel");

                        if (noticeT   != null) homeNoticePanel   = noticeT.gameObject;
                        if (nextHoleT != null) homeNextHolePanel = nextHoleT.gameObject;
                        break;
                    }
                }

                if (homeNoticePanel != null)
                {
                    so.FindProperty("homeNoticePanel").objectReferenceValue = homeNoticePanel;
                    Debug.Log("[MatchmakingModalAutoWire] OK homeNoticePanel -> HomeScreen/NoticePanel"); wired++;
                }
                else { Debug.LogWarning("[MatchmakingModalAutoWire] homeNoticePanel: HomeScreen/NoticePanel not found — wire manually."); failed++; }

                if (homeNextHolePanel != null)
                {
                    so.FindProperty("homeNextHolePanel").objectReferenceValue = homeNextHolePanel;
                    Debug.Log("[MatchmakingModalAutoWire] OK homeNextHolePanel -> HomeScreen/NextHolePanel"); wired++;
                }
                else { Debug.LogWarning("[MatchmakingModalAutoWire] homeNextHolePanel: HomeScreen/NextHolePanel not found — wire manually."); failed++; }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(modal);

            // ── Cross-wire HomeScreenController.matchmakingModal ──────────────
            HomeScreenController homeCtrl = null;
            foreach (var h in Resources.FindObjectsOfTypeAll<HomeScreenController>())
            {
                if (h.gameObject.scene.isLoaded) { homeCtrl = h; break; }
            }

            if (homeCtrl != null)
            {
                var homeSO = new SerializedObject(homeCtrl);
                homeSO.FindProperty("matchmakingModal").objectReferenceValue = modal;
                homeSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(homeCtrl);
                Debug.Log("[MatchmakingModalAutoWire] OK HomeScreenController.matchmakingModal"); wired++;
            }
            else
            {
                Debug.LogWarning("[MatchmakingModalAutoWire] HomeScreenController not found in scene — wire matchmakingModal manually.");
                failed++;
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log($"[MatchmakingModalAutoWire] Done — {wired} wired, {failed} failed." +
                (failed > 0
                    ? " Check Console for missing paths — some fields may need manual assignment."
                    : " All fields wired successfully! Save the scene (Cmd+S / Ctrl+S)."));
        }

        /// <summary>
        /// Breadth-first search for a child Transform by name.
        /// </summary>
        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
