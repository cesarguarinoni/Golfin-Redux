#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Auto-wires all serialized fields on ClubLevelUpModalController.
    /// Also wires the levelUpModal reference on ClubDetailPanel and ClubCompareController.
    ///
    /// Run: GOLFIN/Wire/Club Level Up Modal
    ///
    /// Expects a "ClubLevelUpModal" GameObject in the scene (clone of LevelUpModal,
    /// with ClubLevelUpModalController component attached).
    /// Hierarchy mirrors the Roster LevelUpModal:
    ///   ClubLevelUpModal
    ///     Backdrop
    ///     ModalPanel
    ///       HeaderSection/ClubNameText
    ///       InfoSection/RarityLevelRow/RarityLabel
    ///       InfoSection/RarityLevelRow/LevelText
    ///       InfoSection/NextLevelRow/NextLevelLabel
    ///       InfoSection/NextLevelRow/NextLevelValue
    ///       InfoSection/CostRow/CostLabel
    ///       InfoSection/CostRow/CostValue
    ///       InfoSection/RewardRow/RewardLabel
    ///       InfoSection/RewardRow/RewardValue
    ///       LevelUpButton
    ///       LevelUpButton/Text
    ///       SPSection/AvailableSPRow/AvailableSPLabel
    ///       SPSection/AvailableSPRow/AvailableSPValue
    ///       SPSection/StatRow_Power/(StatBar/Bar, StatBar/BarPending, StatValueCurrent, StatValueMax, PendingLabel, PlusButton)
    ///       SPSection/StatRow_Accuracy/...
    ///       SPSection/StatRow_LieRes/...
    ///       SPSection/StatRow_Loft/(StatBar/Bar, StatValueCurrent, StatValueMax) — no BarPending/PendingLabel/PlusButton
    ///       SPSection/StatRow_Durability/...
    ///       SPSection/ResetButton
    ///       SPSection/ResetButton/Text
    ///       FooterSection/CancelButton
    ///       FooterSection/CancelButton/Text
    ///       FooterSection/ConfirmButton
    ///       FooterSection/ConfirmButton/Text
    /// </summary>
    public static class ClubLevelUpModalAutoWire
    {
        [MenuItem("GOLFIN/Wire/Club Level Up Modal")]
        public static void Wire()
        {
            // ── Find ClubLevelUpModal ────────────────────────────────────────
            var modalGO = GameObject.Find("ClubLevelUpModal");
            if (modalGO == null)
            {
                Debug.LogError("[ClubLevelUpAutoWire] 'ClubLevelUpModal' not found in scene. " +
                               "Clone the LevelUpModal hierarchy, rename to 'ClubLevelUpModal', " +
                               "and attach ClubLevelUpModalController.");
                return;
            }

            var controller = modalGO.GetComponent<ClubLevelUpModalController>();
            if (controller == null)
            {
                Debug.LogError("[ClubLevelUpAutoWire] ClubLevelUpModalController not found on 'ClubLevelUpModal'.");
                return;
            }

            var so   = new SerializedObject(controller);
            var root = modalGO.transform;
            int wired = 0, failed = 0;

            // ── ModalController base fields ──────────────────────────────────
            wired += WireGO(so, "modalPanel", root, "ModalPanel",  ref failed);
            wired += WireGO(so, "backdrop",   root, "Backdrop",    ref failed);

            // ── Club Info ────────────────────────────────────────────────────
            wired += WireTMP(so,    "clubNameText",   root, "ModalPanel/HeaderSection/ClubNameText",                ref failed);
            wired += WireTMP(so,    "rarityLabel",    root, "ModalPanel/InfoSection/RarityLevelRow/RarityLabel",    ref failed);
            wired += WireTMP(so,    "levelText",      root, "ModalPanel/InfoSection/RarityLevelRow/LevelText",      ref failed);
            wired += WireTMP(so,    "nextLevelValue", root, "ModalPanel/InfoSection/NextLevelRow/NextLevelValue",   ref failed);
            wired += WireTMP(so,    "costValue",      root, "ModalPanel/InfoSection/CostRow/CostValue",             ref failed);
            wired += WireTMP(so,    "rewardValue",    root, "ModalPanel/InfoSection/RewardRow/RewardValue",         ref failed);

            // ── Level Up Button ──────────────────────────────────────────────
            wired += WireButton(so, "levelUpButton", root, "ModalPanel/LevelUpButton", ref failed);

            // ── Available SP ─────────────────────────────────────────────────
            wired += WireTMP(so, "availableSPValue", root, "ModalPanel/SPSection/AvailableSPRow/AvailableSPValue", ref failed);

            // ── Allocatable Stat Rows ────────────────────────────────────────
            WireAllocatableStatRow(so, root, "ModalPanel/SPSection/StatRow_Power",
                "powerBar", "powerBarPending",
                "powerValueCurrent", "powerValueMax", "powerPending", "powerPlusButton",
                ref wired, ref failed);

            WireAllocatableStatRow(so, root, "ModalPanel/SPSection/StatRow_Accuracy",
                "accuracyBar", "accuracyBarPending",
                "accuracyValueCurrent", "accuracyValueMax", "accuracyPending", "accuracyPlusButton",
                ref wired, ref failed);

            WireAllocatableStatRow(so, root, "ModalPanel/SPSection/StatRow_LieRes",
                "lieResBar", "lieResBarPending",
                "lieResValueCurrent", "lieResValueMax", "lieResPending", "lieResPlusButton",
                ref wired, ref failed);

            // ── Fixed Stat Row — Loft (no barPending / pending label / plusButton) ──
            WireFixedStatRow(so, root, "ModalPanel/SPSection/StatRow_Loft",
                "loftBar", "loftValueCurrent", "loftValueMax",
                ref wired, ref failed);

            WireAllocatableStatRow(so, root, "ModalPanel/SPSection/StatRow_Durability",
                "durabilityBar", "durabilityBarPending",
                "durabilityValueCurrent", "durabilityValueMax", "durabilityPending", "durabilityPlusButton",
                ref wired, ref failed);

            // ── Action Buttons ───────────────────────────────────────────────
            wired += WireButton(so, "resetButton",   root, "ModalPanel/SPSection/ResetButton",       ref failed);
            wired += WireButton(so, "cancelButton",  root, "ModalPanel/FooterSection/CancelButton",  ref failed);
            wired += WireButton(so, "confirmButton", root, "ModalPanel/FooterSection/ConfirmButton", ref failed);

            // ── Static Labels ────────────────────────────────────────────────
            wired += WireTMP(so, "nextLevelLabel",     root, "ModalPanel/InfoSection/NextLevelRow/NextLevelLabel",        ref failed);
            wired += WireTMP(so, "costLabel",          root, "ModalPanel/InfoSection/CostRow/CostLabel",                  ref failed);
            wired += WireTMP(so, "rewardLabel",        root, "ModalPanel/InfoSection/RewardRow/RewardLabel",              ref failed);
            wired += WireTMP(so, "levelUpButtonLabel", root, "ModalPanel/LevelUpButton/Text",                             ref failed);
            wired += WireTMP(so, "availableSPLabel",   root, "ModalPanel/SPSection/AvailableSPRow/AvailableSPLabel",      ref failed);
            wired += WireTMP(so, "resetButtonLabel",   root, "ModalPanel/SPSection/ResetButton/Text",                    ref failed);
            wired += WireTMP(so, "cancelButtonLabel",  root, "ModalPanel/FooterSection/CancelButton/Text",               ref failed);
            wired += WireTMP(so, "confirmButtonLabel", root, "ModalPanel/FooterSection/ConfirmButton/Text",              ref failed);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // ── Wire back-references ─────────────────────────────────────────
            WireDetailPanelReference(controller, modalGO, ref wired, ref failed);
            WireCompareControllerReference(controller, ref wired, ref failed);

            Debug.Log($"[ClubLevelUpAutoWire] Done. {wired} fields wired, {failed} failed.");
            if (failed > 0)
                Debug.LogWarning("[ClubLevelUpAutoWire] Some fields could not be auto-wired. Check warnings above.");
        }

        // ── Stat row helpers ─────────────────────────────────────────────────

        private static void WireAllocatableStatRow(SerializedObject so, Transform root, string rowPath,
            string barField, string barPendingField,
            string valueCurrentField, string valueMaxField,
            string pendingField, string plusButtonField,
            ref int wired, ref int failed)
        {
            wired += WireImage(so,  barField,          root, $"{rowPath}/StatBar/Bar",         ref failed);
            wired += WireImage(so,  barPendingField,   root, $"{rowPath}/StatBar/BarPending",  ref failed);
            wired += WireTMP(so,    valueCurrentField, root, $"{rowPath}/StatValueCurrent",    ref failed);
            wired += WireTMP(so,    valueMaxField,     root, $"{rowPath}/StatValueMax",        ref failed);
            wired += WireTMP(so,    pendingField,      root, $"{rowPath}/PendingLabel",        ref failed);
            wired += WireButton(so, plusButtonField,   root, $"{rowPath}/PlusButton",          ref failed);
        }

        private static void WireFixedStatRow(SerializedObject so, Transform root, string rowPath,
            string barField, string valueCurrentField, string valueMaxField,
            ref int wired, ref int failed)
        {
            wired += WireImage(so, barField,          root, $"{rowPath}/StatBar/Bar",       ref failed);
            wired += WireTMP(so,   valueCurrentField, root, $"{rowPath}/StatValueCurrent",  ref failed);
            wired += WireTMP(so,   valueMaxField,     root, $"{rowPath}/StatValueMax",      ref failed);
        }

        // ── Back-reference helpers ────────────────────────────────────────────

        private static void WireDetailPanelReference(ClubLevelUpModalController modal,
            GameObject modalGO, ref int wired, ref int failed)
        {
            var detailPanel = Object.FindObjectOfType<ClubDetailPanel>();
            if (detailPanel == null)
            {
                Debug.LogWarning("[ClubLevelUpAutoWire] ClubDetailPanel not found. Wire 'levelUpModal' manually.");
                failed++;
                return;
            }

            var dpSO = new SerializedObject(detailPanel);
            var prop = dpSO.FindProperty("levelUpModal");
            if (prop == null)
            {
                Debug.LogWarning("[ClubLevelUpAutoWire] 'levelUpModal' not found on ClubDetailPanel. Wire manually.");
                failed++;
                return;
            }

            // Also wire the rightPanel reference if not yet set
            var rpProp = dpSO.FindProperty("rightPanel");
            if (rpProp != null && rpProp.objectReferenceValue == null)
            {
                // Try to find RightPanel under the detail panel's parent
                var dpRoot = detailPanel.transform;
                var rightPanelT = dpRoot.Find("RightPanel");
                if (rightPanelT != null)
                {
                    rpProp.objectReferenceValue = rightPanelT.GetComponent<RectTransform>();
                    wired++;
                    Debug.Log("[ClubLevelUpAutoWire] Wired ClubDetailPanel.rightPanel.");
                }
                else
                {
                    Debug.LogWarning("[ClubLevelUpAutoWire] 'RightPanel' not found under ClubDetailPanel. Wire 'rightPanel' manually.");
                    failed++;
                }
            }

            prop.objectReferenceValue = modal;
            dpSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(detailPanel);
            wired++;
            Debug.Log("[ClubLevelUpAutoWire] Wired ClubDetailPanel.levelUpModal.");
        }

        private static void WireCompareControllerReference(ClubLevelUpModalController modal,
            ref int wired, ref int failed)
        {
            var compareController = Object.FindObjectOfType<ClubCompareController>();
            if (compareController == null)
            {
                Debug.LogWarning("[ClubLevelUpAutoWire] ClubCompareController not found. Wire 'levelUpModal' manually.");
                failed++;
                return;
            }

            var ccSO = new SerializedObject(compareController);
            var prop = ccSO.FindProperty("levelUpModal");
            if (prop == null)
            {
                Debug.LogWarning("[ClubLevelUpAutoWire] 'levelUpModal' not found on ClubCompareController. Wire manually.");
                failed++;
                return;
            }

            prop.objectReferenceValue = modal;
            ccSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(compareController);
            wired++;
            Debug.Log("[ClubLevelUpAutoWire] Wired ClubCompareController.levelUpModal.");
        }

        // ── Wire helpers (matching LevelUpModalAutoWire pattern) ──────────────

        private static int WireGO(SerializedObject so, string fieldName, Transform root,
            string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
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
                Debug.LogWarning($"[ClubLevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] No TextMeshProUGUI at '{path}' for '{fieldName}'. Wire manually.");
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
                Debug.LogWarning($"[ClubLevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            var img = t.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] No Image at '{path}' for '{fieldName}'. Wire manually.");
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
                Debug.LogWarning($"[ClubLevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }
            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] Path '{path}' not found for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            var btn = t.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[ClubLevelUpAutoWire] No Button at '{path}' for '{fieldName}'. Wire manually.");
                failed++; return 0;
            }
            prop.objectReferenceValue = btn;
            return 1;
        }
    }
}
#endif
