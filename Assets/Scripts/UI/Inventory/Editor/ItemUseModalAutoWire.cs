#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires ItemUseModalController fields and ItemDetailPanel.useModal reference.
    ///
    /// Expected hierarchy under ItemUseModal:
    ///   ModalPanel/ModalContainer/TitleText          → titleText
    ///   ModalPanel/ModalContainer/FilterBar          → filterBar (ClubFilterBar)
    ///   ModalPanel/ModalContainer/ScrollArea         → scrollRect
    ///   ModalPanel/ModalContainer/ScrollArea/Viewport/GridContent → gridParent
    ///   ModalPanel/ModalContainer/CancelButton       → cancelButton
    ///   Background                                   → backgroundImage
    ///
    /// Also wires:
    ///   ItemDetailPanel.useModal → ItemUseModalController
    ///   ItemUseModalController.clubCardPrefab → ItemUseClubCard.prefab
    ///
    /// Run: GOLFIN/Wire/Item Use Modal
    /// </summary>
    public static class ItemUseModalAutoWire
    {
        private const string CARD_PREFAB_PATH = "Assets/Prefabs/UI/Inventory/ItemUseClubCard.prefab";

        [MenuItem("GOLFIN/Wire/Item Use Modal")]
        public static void Run()
        {
            // ── Find ItemUseModalController ───────────────────────────────────
            ItemUseModalController? modal = null;
            foreach (var m in Resources.FindObjectsOfTypeAll<ItemUseModalController>())
                if (m.gameObject.scene.isLoaded) { modal = m; break; }

            if (modal == null)
            {
                EditorUtility.DisplayDialog("Auto-Wire Failed",
                    "ItemUseModalController not found in scene.\n\nRun GOLFIN/Build/Item Use Modal first.", "OK");
                return;
            }

            var so    = new SerializedObject(modal);
            var root  = modal.transform;
            int wired = 0, failed = 0;

            // ── Helpers ───────────────────────────────────────────────────────

            int WireTMP(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); failed++; return 0; }
                var c = t.GetComponent<TextMeshProUGUI>();
                if (c == null) { Fail(prop, path, "no TMP"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[ItemUseModalAutoWire] ✓ {prop}"); wired++; return 1;
            }

            int WireImage(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); failed++; return 0; }
                var c = t.GetComponent<Image>();
                if (c == null) { Fail(prop, path, "no Image"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[ItemUseModalAutoWire] ✓ {prop}"); wired++; return 1;
            }

            int WireButton(string prop, string path)
            {
                var t = root.Find(path);
                if (t == null) { Fail(prop, path); failed++; return 0; }
                var c = t.GetComponent<Button>();
                if (c == null) { Fail(prop, path, "no Button"); failed++; return 0; }
                so.FindProperty(prop).objectReferenceValue = c;
                Debug.Log($"[ItemUseModalAutoWire] ✓ {prop}"); wired++; return 1;
            }

            // ── Modal fields ──────────────────────────────────────────────────
            WireTMP   ("titleText",      "ModalPanel/ModalContainer/TitleText");
            WireImage ("backgroundImage","Background");
            WireButton("cancelButton",   "ModalPanel/ModalContainer/CancelButton");

            // FilterBar
            var filterBarT = root.Find("ModalPanel/ModalContainer/FilterBar");
            if (filterBarT != null)
            {
                var filterBarComp = filterBarT.GetComponent<ClubFilterBar>();
                if (filterBarComp != null)
                {
                    so.FindProperty("filterBar").objectReferenceValue = filterBarComp;
                    Debug.Log("[ItemUseModalAutoWire] ✓ filterBar"); wired++;
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
                    Debug.Log("[ItemUseModalAutoWire] ✓ scrollRect"); wired++;
                }
                else { Fail("scrollRect", "ModalPanel/ModalContainer/ScrollArea", "no ScrollRect"); failed++; }
            }
            else { Fail("scrollRect", "ModalPanel/ModalContainer/ScrollArea"); failed++; }

            // gridParent (GridContent Transform)
            var gridT = root.Find("ModalPanel/ModalContainer/ScrollArea/Viewport/GridContent");
            if (gridT != null)
            {
                so.FindProperty("gridParent").objectReferenceValue = gridT;
                Debug.Log("[ItemUseModalAutoWire] ✓ gridParent"); wired++;
            }
            else { Fail("gridParent", "ModalPanel/ModalContainer/ScrollArea/Viewport/GridContent"); failed++; }

            // clubCardPrefab
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CARD_PREFAB_PATH);
            if (cardPrefab != null)
            {
                so.FindProperty("clubCardPrefab").objectReferenceValue = cardPrefab;
                Debug.Log("[ItemUseModalAutoWire] ✓ clubCardPrefab"); wired++;
            }
            else
            {
                Debug.LogWarning($"[ItemUseModalAutoWire] clubCardPrefab not found at {CARD_PREFAB_PATH} — run GOLFIN/Build/Item Use Club Card Prefab first.");
                failed++;
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(modal);

            // ── Wire ItemDetailPanel.useModal ─────────────────────────────────
            ItemDetailPanel? detailPanel = null;
            foreach (var p in Resources.FindObjectsOfTypeAll<ItemDetailPanel>())
                if (p.gameObject.scene.isLoaded) { detailPanel = p; break; }

            if (detailPanel != null)
            {
                var dpSO = new SerializedObject(detailPanel);
                dpSO.FindProperty("useModal").objectReferenceValue = modal;
                dpSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(detailPanel);
                Debug.Log("[ItemUseModalAutoWire] ✓ ItemDetailPanel.useModal"); wired++;
            }
            else
            {
                Debug.LogWarning("[ItemUseModalAutoWire] ItemDetailPanel not found — wire useModal manually.");
                failed++;
            }

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            string msg = $"{wired} fields wired, {failed} failed.\n\n" +
                (failed > 0
                    ? "Check Console for missing paths."
                    : "All fields wired successfully!\n\nSave the scene (Ctrl+S).");

            Debug.Log($"[ItemUseModalAutoWire] Done — {wired} wired, {failed} failed.");
            EditorUtility.DisplayDialog("Item Use Modal Auto-Wire", msg, "OK");
        }

        private static void Fail(string prop, string path, string reason = "path not found")
            => Debug.LogWarning($"[ItemUseModalAutoWire] FAILED '{prop}' at '{path}' — {reason}. Wire manually.");
    }
}
#endif
