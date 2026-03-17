#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Auto-wires all serialized fields on LevelUpModalController from the
    /// hierarchy produced by LevelUpModalBuilder.
    ///
    /// Also wires CharacterDetailPanel.levelUpModal so the detail panel can
    /// open the modal when the LEVEL UP button is tapped.
    ///
    /// Run after: GOLFIN > Build Level Up Modal
    /// </summary>
    public static class LevelUpModalAutoWire
    {
        [MenuItem("GOLFIN/Wire Level Up Modal")]
        public static void Wire()
        {
            // ── Find LevelUpModal ────────────────────────────────────────────
            var modalGO = GameObject.Find("LevelUpModal");
            if (modalGO == null)
            {
                Debug.LogError("[LevelUpAutoWire] LevelUpModal not found in scene. Run GOLFIN > Build Level Up Modal first.");
                return;
            }

            var controller = modalGO.GetComponent<LevelUpModalController>();
            if (controller == null)
            {
                Debug.LogError("[LevelUpAutoWire] LevelUpModalController component not found on LevelUpModal.");
                return;
            }

            var so   = new SerializedObject(controller);
            var root = modalGO.transform;
            int wired = 0, failed = 0;

            // ── ModalController base fields ──────────────────────────────────
            wired += WireGO(so,    "modalPanel", root, "ModalPanel",  ref failed);
            wired += WireGO(so,    "backdrop",   root, "Backdrop",    ref failed);

            // ── Character Info ───────────────────────────────────────────────
            wired += WireTMP(so,   "characterNameText", root, "ModalPanel/HeaderSection/CharacterNameText", ref failed);
            wired += WireTMP(so,   "rarityLabel",       root, "ModalPanel/InfoSection/RarityLevelRow/RarityLabel",   ref failed);
            wired += WireTMP(so,   "levelText",         root, "ModalPanel/InfoSection/RarityLevelRow/LevelText",     ref failed);
            wired += WireTMP(so,   "nextLevelValue",    root, "ModalPanel/InfoSection/NextLevelRow/NextLevelValue",  ref failed);
            wired += WireTMP(so,   "costValue",         root, "ModalPanel/InfoSection/CostRow/CostValue",           ref failed);
            wired += WireTMP(so,   "rewardValue",       root, "ModalPanel/InfoSection/RewardRow/RewardValue",       ref failed);

            // ── Level Up Button ──────────────────────────────────────────────
            wired += WireButton(so, "levelUpButton", root, "ModalPanel/LevelUpButton", ref failed);

            // ── Available SP ─────────────────────────────────────────────────
            wired += WireTMP(so, "availableSPValue", root, "ModalPanel/SPSection/AvailableSPRow/AvailableSPValue", ref failed);

            // ── Stat Rows ────────────────────────────────────────────────────
            WireStatRow(so, root, "ModalPanel/SPSection/StatRow_Strength",
                "strengthBar", "strengthBarPending", "strengthValue", "strengthPending", "strengthPlusButton",
                ref wired, ref failed);

            WireStatRow(so, root, "ModalPanel/SPSection/StatRow_ClubControl",
                "clubControlBar", "clubControlBarPending", "clubControlValue", "clubControlPending", "clubControlPlusButton",
                ref wired, ref failed);

            WireStatRow(so, root, "ModalPanel/SPSection/StatRow_Recovery",
                "recoveryBar", "recoveryBarPending", "recoveryValue", "recoveryPending", "recoveryPlusButton",
                ref wired, ref failed);

            WireStatRow(so, root, "ModalPanel/SPSection/StatRow_Stamina",
                "staminaBar", "staminaBarPending", "staminaValue", "staminaPending", "staminaPlusButton",
                ref wired, ref failed);

            // ── Action Buttons ───────────────────────────────────────────────
            wired += WireButton(so, "resetButton",   root, "ModalPanel/SPSection/ResetButton",          ref failed);
            wired += WireButton(so, "cancelButton",  root, "ModalPanel/FooterSection/CancelButton",     ref failed);
            wired += WireButton(so, "confirmButton", root, "ModalPanel/FooterSection/ConfirmButton",    ref failed);
            wired += WireImage(so,  "confirmButtonImage", root, "ModalPanel/FooterSection/ConfirmButton", ref failed);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            // ── Wire CharacterDetailPanel.levelUpModal ───────────────────────
            WireDetailPanelReference(controller, ref wired, ref failed);

            Debug.Log($"[LevelUpAutoWire] Done. {wired} fields wired, {failed} failed.");

            if (failed > 0)
                Debug.LogWarning("[LevelUpAutoWire] Some fields could not be auto-wired. Check warnings above and wire manually.");
        }

        // ── Stat row helper ──────────────────────────────────────────────────

        /// <summary>
        /// Wires all 5 fields for one stat row.
        /// Expected children under rowPath:
        ///   StatBar/Bar        → barField       (blue Image)
        ///   StatBar/BarPending → barPendingField (orange Image)
        ///   StatValue          → valueField      (TMP)
        ///   PendingLabel       → pendingField    (TMP)
        ///   PlusButton         → plusButtonField (Button)
        /// </summary>
        private static void WireStatRow(SerializedObject so, Transform root, string rowPath,
            string barField, string barPendingField,
            string valueField, string pendingField, string plusButtonField,
            ref int wired, ref int failed)
        {
            wired += WireImage(so,  barField,        root, $"{rowPath}/StatBar/Bar",        ref failed);
            wired += WireImage(so,  barPendingField, root, $"{rowPath}/StatBar/BarPending", ref failed);
            wired += WireTMP(so,    valueField,      root, $"{rowPath}/StatValue",          ref failed);
            wired += WireTMP(so,    pendingField,    root, $"{rowPath}/PendingLabel",       ref failed);
            wired += WireButton(so, plusButtonField, root, $"{rowPath}/PlusButton",         ref failed);
        }

        // ── Detail Panel back-reference ──────────────────────────────────────

        private static void WireDetailPanelReference(LevelUpModalController modal,
            ref int wired, ref int failed)
        {
            var detailPanel = Object.FindObjectOfType<CharacterDetailPanel>();
            if (detailPanel == null)
            {
                Debug.LogWarning("[LevelUpAutoWire] CharacterDetailPanel not found. Wire 'levelUpModal' on it manually.");
                failed++;
                return;
            }

            var dpSO = new SerializedObject(detailPanel);
            var prop = dpSO.FindProperty("levelUpModal");
            if (prop == null)
            {
                Debug.LogWarning("[LevelUpAutoWire] 'levelUpModal' field not found on CharacterDetailPanel. Wire manually.");
                failed++;
                return;
            }

            prop.objectReferenceValue = modal;
            dpSO.ApplyModifiedProperties();
            EditorUtility.SetDirty(detailPanel);
            wired++;
            Debug.Log("[LevelUpAutoWire] Wired CharacterDetailPanel.levelUpModal.");
        }

        // ── Wire helpers (matching DetailPanelAutoWire pattern) ──────────────

        private static int WireGO(SerializedObject so, string fieldName, Transform root,
            string path, ref int failed)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }

            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
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
                Debug.LogWarning($"[LevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }

            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
                failed++; return 0;
            }

            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] No TextMeshProUGUI at '{path}' for field '{fieldName}'. Wire manually.");
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
                Debug.LogWarning($"[LevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }

            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
                failed++; return 0;
            }

            var img = t.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] No Image at '{path}' for field '{fieldName}'. Wire manually.");
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
                Debug.LogWarning($"[LevelUpAutoWire] Field '{fieldName}' not found on component.");
                failed++; return 0;
            }

            var t = root.Find(path);
            if (t == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] Path '{path}' not found for field '{fieldName}'. Wire manually.");
                failed++; return 0;
            }

            var btn = t.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[LevelUpAutoWire] No Button at '{path}' for field '{fieldName}'. Wire manually.");
                failed++; return 0;
            }

            prop.objectReferenceValue = btn;
            return 1;
        }
    }
}
#endif
