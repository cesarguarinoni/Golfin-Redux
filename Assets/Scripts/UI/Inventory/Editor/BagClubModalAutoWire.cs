#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires BagClubModalController and hooks it to BagDetailPanel.clubModal.
    ///
    /// Expected hierarchy under the BagClubModal root GO:
    ///   Backdrop                                       (GO — backdrop overlay)
    ///   ModalPanel/ModalContainer/TitleText            (TMP)
    ///   ModalPanel/ModalContainer/FilterBar            (ClubFilterBar)
    ///   ModalPanel/ModalContainer/ScrollArea           (ScrollRect)
    ///   ModalPanel/ModalContainer/ScrollArea/Viewport/GridContent  (Transform — gridParent)
    ///   ModalPanel/ModalContainer/CancelButton         (Button)
    ///
    /// Also wires:
    ///   BagDetailPanel.clubModal → BagClubModalController
    ///
    /// Prefab expected at:
    ///   Assets/Prefabs/UI/Inventory/BagClubCard.prefab
    ///
    /// Run: GOLFIN/Wire/Bag Club Modal
    /// </summary>
    public static class BagClubModalAutoWire
    {
        private const string PREFAB_CLUB_CARD = "Assets/Prefabs/UI/Inventory/BagClubCard.prefab";

        [MenuItem("GOLFIN/Wire/Bag Club Modal")]
        public static void Run()
        {
            int wired = 0, failed = 0;

            // ── Find BagClubModalController ───────────────────────────────────
            BagClubModalController? modal = null;
            foreach (var m in Resources.FindObjectsOfTypeAll<BagClubModalController>())
                if (m.gameObject.scene.isLoaded) { modal = m; break; }

            if (modal == null)
            {
                EditorUtility.DisplayDialog("Bag Club Modal Auto-Wire",
                    "BagClubModalController not found in scene.\n\n" +
                    "Add the component to your BagClubModal GO first\n" +
                    "(clone ItemUseModal, replace ItemUseModalController with BagClubModalController).", "OK");
                return;
            }

            var so   = new SerializedObject(modal);
            var root = modal.transform;

            void Fail(string prop, string path, string reason = "path not found")
                => Debug.LogWarning($"[BagClubModalAutoWire] FAILED '{prop}' at '{path}' — {reason}. Wire manually.");

            void WireTMP(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); failed++; return; }
                var c = t.GetComponent<TextMeshProUGUI>();
                if (c == null) { Fail(prop, path, "no TMP"); failed++; return; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[BagClubModalAutoWire] ✓ {prop}"); wired++;
            }

            void WireButton(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); failed++; return; }
                var c = t.GetComponent<Button>();
                if (c == null) { Fail(prop, path, "no Button"); failed++; return; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[BagClubModalAutoWire] ✓ {prop}"); wired++;
            }

            // ── Modal fields ──────────────────────────────────────────────────
            WireTMP   ("titleText",    "ModalPanel/ModalContainer/TitleText");
            WireButton("cancelButton", "ModalPanel/ModalContainer/CancelButton");

            // FilterBar
            var filterT = root.Find("ModalPanel/ModalContainer/FilterBar");
            if (filterT != null)
            {
                var filterComp = filterT.GetComponent<ClubFilterBar>();
                if (filterComp != null)
                {
                    so.FindProperty("filterBar").objectReferenceValue = filterComp;
                    Debug.Log("[BagClubModalAutoWire] ✓ filterBar"); wired++;
                }
                else { Fail("filterBar", "ModalPanel/ModalContainer/FilterBar", "no ClubFilterBar"); failed++; }
            }
            else { Fail("filterBar", "ModalPanel/ModalContainer/FilterBar"); failed++; }

            // ScrollRect
            var scrollT = root.Find("ModalPanel/ModalContainer/ScrollArea");
            if (scrollT != null)
            {
                var scrollComp = scrollT.GetComponent<ScrollRect>();
                if (scrollComp != null)
                {
                    so.FindProperty("scrollRect").objectReferenceValue = scrollComp;
                    Debug.Log("[BagClubModalAutoWire] ✓ scrollRect"); wired++;
                }
                else { Fail("scrollRect", "ModalPanel/ModalContainer/ScrollArea", "no ScrollRect"); failed++; }
            }
            else { Fail("scrollRect", "ModalPanel/ModalContainer/ScrollArea"); failed++; }

            // gridParent
            var gridT = root.Find("ModalPanel/ModalContainer/ScrollArea/Viewport/GridContent");
            if (gridT != null)
            {
                so.FindProperty("gridParent").objectReferenceValue = gridT;
                Debug.Log("[BagClubModalAutoWire] ✓ gridParent"); wired++;
            }
            else { Fail("gridParent", "ModalPanel/ModalContainer/ScrollArea/Viewport/GridContent"); failed++; }

            // clubCardPrefab
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_CLUB_CARD);
            if (cardPrefab != null)
            {
                so.FindProperty("clubCardPrefab").objectReferenceValue = cardPrefab;
                Debug.Log("[BagClubModalAutoWire] ✓ clubCardPrefab"); wired++;
            }
            else { Debug.LogWarning($"[BagClubModalAutoWire] clubCardPrefab not found at {PREFAB_CLUB_CARD} — create the prefab first."); failed++; }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(modal);

            // ── Wire BagDetailPanel.clubModal ─────────────────────────────────
            BagDetailPanel? detailPanel = null;
            foreach (var p in Resources.FindObjectsOfTypeAll<BagDetailPanel>())
                if (p.gameObject.scene.isLoaded) { detailPanel = p; break; }

            if (detailPanel != null)
            {
                var dpSO = new SerializedObject(detailPanel);
                dpSO.FindProperty("clubModal").objectReferenceValue = modal;
                dpSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(detailPanel);
                Debug.Log("[BagClubModalAutoWire] ✓ BagDetailPanel.clubModal"); wired++;
            }
            else
            {
                Debug.LogWarning("[BagClubModalAutoWire] BagDetailPanel not found — wire clubModal manually.");
                failed++;
            }

            // ── Done ──────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"{wired} fields wired, {failed} failed.\n\n" +
                (failed > 0
                    ? "Check Console for missing paths.\nFix the hierarchy, then re-run."
                    : "All fields wired!\nSave the scene (Ctrl+S) and hit Play to test.");

            Debug.Log($"[BagClubModalAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Bag Club Modal Auto-Wire", msg, "OK");
        }
    }
}
#endif
