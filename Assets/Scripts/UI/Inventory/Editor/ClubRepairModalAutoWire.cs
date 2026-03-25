#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires all serialized fields on ClubRepairModalController.
    /// Also wires the repairModal reference on ClubDetailPanel and ClubCompareController.
    ///
    /// Run: GOLFIN/Wire/Club Repair Modal
    ///
    /// Expected hierarchy (built by GOLFIN/Build/Club Repair Modal Sections):
    ///   ClubRepairModal
    ///     Backdrop
    ///     ModalPanel
    ///       HeaderSection/ClubNameText
    ///       InfoSection/RarityLevelRow/RarityLabel
    ///       InfoSection/RarityLevelRow/LevelText
    ///       DurabilitySection/DurabilityLabel
    ///       DurabilitySection/DurabilityBarRow/DurabilityBar
    ///       DurabilitySection/DurabilityBarRow/DurabilityBarPreview
    ///       DurabilitySection/DurabilityValueText
    ///       DurabilitySection/DurabilityChangeText
    ///       KitSection/KitButtonsRow/StandardKitButton
    ///       KitSection/KitButtonsRow/StandardKitButton/KitLabel
    ///       KitSection/KitButtonsRow/StandardKitButton/KitCountText
    ///       KitSection/KitButtonsRow/StandardKitButton/SelectedIndicator
    ///       KitSection/KitButtonsRow/PremiumKitButton
    ///       KitSection/KitButtonsRow/PremiumKitButton/KitLabel
    ///       KitSection/KitButtonsRow/PremiumKitButton/KitCountText
    ///       KitSection/KitButtonsRow/PremiumKitButton/SelectedIndicator
    ///       KitSection/NoKitsMessage
    ///       FooterSection/CancelButton
    ///       FooterSection/CancelButton/Text
    ///       FooterSection/ConfirmButton
    ///       FooterSection/ConfirmButton/Text
    /// </summary>
    public static class ClubRepairModalAutoWire
    {
        [MenuItem("GOLFIN/Wire/Club Repair Modal")]
        public static void Wire()
        {
            // ── Find ClubRepairModal (including inactive) ────────────────────
            var controller = Object.FindObjectOfType<ClubRepairModalController>(includeInactive: true);
            if (controller == null)
            {
                Debug.LogError("[ClubRepairAutoWire] ClubRepairModalController not found in scene. " +
                               "Create a 'ClubRepairModal' GameObject and attach ClubRepairModalController.");
                return;
            }

            var modalGO = controller.gameObject;
            var so      = new SerializedObject(controller);
            var root    = modalGO.transform;
            int wired   = 0, failed = 0;

            // ── ModalController base fields ──────────────────────────────────
            wired += WireGO(so,  "modalPanel", root, "ModalPanel",  ref failed);
            wired += WireGO(so,  "backdrop",   root, "Backdrop",    ref failed);

            // ── Club Info ────────────────────────────────────────────────────
            wired += WireTMP(so,    "clubNameText", root, "ModalPanel/HeaderSection/ClubNameText",              ref failed);
            wired += WireTMP(so,    "rarityLabel",  root, "ModalPanel/InfoSection/RarityLevelRow/RarityLabel",  ref failed);
            wired += WireTMP(so,    "levelText",    root, "ModalPanel/InfoSection/RarityLevelRow/LevelText",    ref failed);

            // ── Durability Display ───────────────────────────────────────────
            wired += WireTMP(so,    "durabilityLabel",      root, "ModalPanel/DurabilitySection/DurabilityLabel",                  ref failed);
            wired += WireImage(so,  "durabilityBar",        root, "ModalPanel/DurabilitySection/DurabilityBarRow/DurabilityBar",    ref failed);
            wired += WireImage(so,  "durabilityBarPreview", root, "ModalPanel/DurabilitySection/DurabilityBarRow/DurabilityBarPreview", ref failed);
            wired += WireTMP(so,    "durabilityValueText",  root, "ModalPanel/DurabilitySection/DurabilityValueText",              ref failed);
            wired += WireTMP(so,    "durabilityChangeText", root, "ModalPanel/DurabilitySection/DurabilityChangeText",             ref failed);

            // ── Kit Selection ────────────────────────────────────────────────
            wired += WireButton(so, "standardKitButton",    root, "ModalPanel/KitSection/KitButtonsRow/StandardKitButton",                      ref failed);
            wired += WireTMP(so,    "standardKitLabel",     root, "ModalPanel/KitSection/KitButtonsRow/StandardKitButton/KitLabel",             ref failed);
            wired += WireTMP(so,    "standardKitCountText", root, "ModalPanel/KitSection/KitButtonsRow/StandardKitButton/KitCountText",         ref failed);
            wired += WireGO(so,     "standardKitSelected",  root, "ModalPanel/KitSection/KitButtonsRow/StandardKitButton/SelectedIndicator",    ref failed);
            wired += WireButton(so, "premiumKitButton",     root, "ModalPanel/KitSection/KitButtonsRow/PremiumKitButton",                       ref failed);
            wired += WireTMP(so,    "premiumKitLabel",      root, "ModalPanel/KitSection/KitButtonsRow/PremiumKitButton/KitLabel",              ref failed);
            wired += WireTMP(so,    "premiumKitCountText",  root, "ModalPanel/KitSection/KitButtonsRow/PremiumKitButton/KitCountText",          ref failed);
            wired += WireGO(so,     "premiumKitSelected",   root, "ModalPanel/KitSection/KitButtonsRow/PremiumKitButton/SelectedIndicator",     ref failed);
            wired += WireTMP(so,    "noKitsMessage",        root, "ModalPanel/KitSection/NoKitsMessage",                                         ref failed);

            // ── Action Buttons ───────────────────────────────────────────────
            wired += WireButton(so, "cancelButton",         root, "ModalPanel/FooterSection/CancelButton",  ref failed);
            wired += WireButton(so, "confirmButton",        root, "ModalPanel/FooterSection/ConfirmButton", ref failed);

            // ── Static Labels ────────────────────────────────────────────────
            wired += WireTMP(so, "cancelButtonLabel",  root, "ModalPanel/FooterSection/CancelButton/Text",  ref failed);
            wired += WireTMP(so, "confirmButtonLabel", root, "ModalPanel/FooterSection/ConfirmButton/Text", ref failed);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // ── Wire back-references ─────────────────────────────────────────
            WireDetailPanelReference(controller, ref wired, ref failed);
            WireCompareControllerReference(controller, ref wired, ref failed);

            Debug.Log($"[ClubRepairAutoWire] Done. {wired} fields wired, {failed} failed.");
            if (failed > 0)
                Debug.LogWarning("[ClubRepairAutoWire] Some fields could not be auto-wired. Check warnings above.");
        }

        // ── Back-reference helpers ────────────────────────────────────────────

        private static void WireDetailPanelReference(ClubRepairModalController modal,
            ref int wired, ref int failed)
        {
            var detailPanel = Object.FindObjectOfType<ClubDetailPanel>(includeInactive: true);
            if (detailPanel == null)
            {
                Debug.LogWarning("[ClubRepairAutoWire] ClubDetailPanel not found. Wire 'repairModal' manually.");
                failed++;
                return;
            }

            var dpSO = new SerializedObject(detailPanel);
            var prop = dpSO.FindProperty("repairModal");
            if (prop == null)
            {
                Debug.LogWarning("[ClubRepairAutoWire] 'repairModal' not found on ClubDetailPanel. Wire manually.");
                failed++;
                return;
            }

            prop.objectReferenceValue = modal;
            dpSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(detailPanel);
            wired++;
            Debug.Log("[ClubRepairAutoWire] Wired ClubDetailPanel.repairModal.");
        }

        private static void WireCompareControllerReference(ClubRepairModalController modal,
            ref int wired, ref int failed)
        {
            var compareController = Object.FindObjectOfType<ClubCompareController>(includeInactive: true);
            if (compareController == null)
            {
                Debug.LogWarning("[ClubRepairAutoWire] ClubCompareController not found. Wire 'repairModal' manually.");
                failed++;
                return;
            }

            var ccSO = new SerializedObject(compareController);
            var prop = ccSO.FindProperty("repairModal");
            if (prop == null)
            {
                Debug.LogWarning("[ClubRepairAutoWire] 'repairModal' not found on ClubCompareController. Wire manually.");
                failed++;
                return;
            }

            prop.objectReferenceValue = modal;
            ccSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(compareController);
            wired++;
            Debug.Log("[ClubRepairAutoWire] Wired ClubCompareController.repairModal.");
        }

        // ── Wire helpers ──────────────────────────────────────────────────────

        private static int WireGO(SerializedObject so, string fieldName, Transform root,
            string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = t.gameObject;
            return 1;
        }

        private static int WireTMP(SerializedObject so, string fieldName, Transform root,
            string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] No TextMeshProUGUI at '{path}' for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = tmp;
            return 1;
        }

        private static int WireImage(SerializedObject so, string fieldName, Transform root,
            string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] No Image at '{path}' for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = img;
            return 1;
        }

        private static int WireButton(SerializedObject so, string fieldName, Transform root,
            string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            var btn = t.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[ClubRepairAutoWire] No Button at '{path}' for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = btn;
            return 1;
        }
    }
}
#endif
