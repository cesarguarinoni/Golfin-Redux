#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Builds the LevelUpModal UI hierarchy under RosterScreen.
    /// Run GOLFIN > Wire Level Up Modal afterward to auto-wire Inspector fields.
    ///
    /// Hierarchy produced:
    ///   RosterScreen
    ///   └── LevelUpModal            ← LevelUpModalController, full-screen overlay
    ///       ├── Backdrop            ← dark semi-transparent panel
    ///       └── ModalPanel          ← VerticalLayoutGroup, centered card
    ///           ├── HeaderSection   → CharacterNameText
    ///           ├── Divider
    ///           ├── InfoSection     → RarityLevelRow / NextLevelRow / CostRow / RewardRow
    ///           ├── LevelUpButton
    ///           ├── Divider
    ///           ├── SPSection       → AvailableSPRow / 4×StatRow / ResetButton
    ///           └── FooterSection   → CancelButton / ConfirmButton
    /// </summary>
    public static class LevelUpModalBuilder
    {
        [MenuItem("GOLFIN/Build Level Up Modal")]
        public static void Build()
        {
            var rosterScreen = FindRosterScreen();
            if (rosterScreen == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "RosterScreen not found in scene.\n\n" +
                    "Make sure ShellScene is open and RosterScreen exists under Canvas/ScreensRoot.",
                    "OK");
                return;
            }

            var existing = rosterScreen.Find("LevelUpModal");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Replace Existing?",
                    "LevelUpModal already exists under RosterScreen. Replace it?",
                    "Yes, Replace", "Cancel"))
                    return;
                Object.DestroyImmediate(existing.gameObject);
            }

            var modal = BuildModal(rosterScreen);

            EditorUtility.SetDirty(modal);
            EditorSceneManager.MarkSceneDirty(modal.scene);
            Selection.activeGameObject = modal;

            EditorUtility.DisplayDialog("Done!",
                "LevelUpModal hierarchy created under RosterScreen.\n\n" +
                "Next: run GOLFIN > Wire Level Up Modal to auto-wire the Inspector fields.",
                "OK");

            Debug.Log("[LevelUpModalBuilder] Build complete.");
        }

        // ── Entry point ──────────────────────────────────────────────────────

        private static Transform FindRosterScreen()
        {
            var direct = GameObject.Find("RosterScreen");
            if (direct != null) return direct.transform;

            var canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                var nested = canvas.transform.Find("ScreensRoot/RosterScreen");
                if (nested != null) return nested;
            }
            return null;
        }

        private static GameObject BuildModal(Transform parent)
        {
            // ── LevelUpModal root ───────────────────────────────────────────
            // Full-screen so it covers the entire RosterScreen.
            // LevelUpModalController lives here.
            var root = MakeRect("LevelUpModal", parent);
            StretchFull(root.GetComponent<RectTransform>());
            var controller = root.AddComponent<LevelUpModalController>();

            // ── Backdrop ────────────────────────────────────────────────────
            // Dark overlay behind ModalPanel. Blocks clicks through the modal.
            var backdrop = MakeRect("Backdrop", root.transform);
            StretchFull(backdrop.GetComponent<RectTransform>());
            var backdropImg = backdrop.AddComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.7f);
            backdropImg.raycastTarget = true;
            backdrop.SetActive(false); // ModalController shows/hides this

            // ── ModalPanel ──────────────────────────────────────────────────
            // Centered card. CanvasGroup added by ModalController.Awake() for fade.
            // VerticalLayoutGroup stacks all sections top-to-bottom.
            var panel = MakeRect("ModalPanel", root.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            CenterAnchored(panelRect, new Vector2(500, 680));

            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(20, 20, 20, 20);
            panelLayout.spacing = 12;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = false;  // respects LayoutElement.preferredHeight
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            panel.SetActive(false); // ModalController shows/hides this

            // Wire ModalController base-class public fields now while we have references
            var so = new SerializedObject(controller);
            so.FindProperty("modalPanel").objectReferenceValue = panel;
            so.FindProperty("backdrop").objectReferenceValue = backdrop;
            so.ApplyModifiedProperties();

            // ── Sections ────────────────────────────────────────────────────
            // Heights: 60 + 2 + 120 + 50 + 2 + 280 + 56 = 570
            // + padding 40 + spacing (6 gaps × 12) 72 = 682 ≈ panel height 680
            BuildHeaderSection(panel.transform);  // preferred 60
            BuildDivider(panel.transform);         // preferred 2
            BuildInfoSection(panel.transform);     // preferred 120
            BuildLevelUpButton(panel.transform);   // preferred 50
            BuildDivider(panel.transform);         // preferred 2
            BuildSPSection(panel.transform);       // preferred 280
            BuildFooterSection(panel.transform);   // preferred 56

            return root;
        }

        // ── Sections ────────────────────────────────────────────────────────

        private static void BuildHeaderSection(Transform parent)
        {
            var header = MakeRect("HeaderSection", parent);
            LayoutEl(header, preferredHeight: 60);

            var layout = header.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            // CharacterNameText — two-line name e.g. "SHAE\nO'CONNELL"
            var nameText = MakeTMP("CharacterNameText", header.transform,
                "CHARACTER\nNAME", 20, TextAlignmentOptions.Center);
            LayoutEl(nameText.gameObject, preferredHeight: 55);
        }

        private static void BuildDivider(Transform parent)
        {
            var divider = MakeRect("Divider", parent);
            LayoutEl(divider, preferredHeight: 2);
            var img = divider.AddComponent<Image>();
            img.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        }

        private static void BuildInfoSection(Transform parent)
        {
            var info = MakeRect("InfoSection", parent);
            LayoutEl(info, preferredHeight: 120);

            var layout = info.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            // RarityLevelRow — "LEGENDARY"  "Lv 160/199"
            {
                var row = MakeHRow("RarityLevelRow", info.transform, 26);
                MakeTMP("RarityLabel", row.transform, "RARE", 17, TextAlignmentOptions.Left);
                MakeTMP("LevelText",   row.transform, "Lv 1/199", 17, TextAlignmentOptions.Right);
            }

            // NextLevelRow — "NEXT LEVEL  →  Lv 161"
            {
                var row = MakeHRow("NextLevelRow", info.transform, 22);
                MakeTMP("NextLevelLabel", row.transform, "NEXT LEVEL", 15, TextAlignmentOptions.Left);
                MakeTMP("ArrowIcon",      row.transform, "->",         15, TextAlignmentOptions.Center);
                MakeTMP("NextLevelValue", row.transform, "Lv 2",       15, TextAlignmentOptions.Right);
            }

            // CostRow — "COST" left | spacer | [RPIcon] "100" right
            {
                var row = MakeRect("CostRow", info.transform);
                LayoutEl(row, preferredHeight: 22);
                var costLayout = row.AddComponent<HorizontalLayoutGroup>();
                costLayout.childAlignment        = TextAnchor.MiddleLeft;
                costLayout.childControlWidth     = true;
                costLayout.childControlHeight    = true;
                costLayout.childForceExpandWidth  = false; // spacer takes remaining space
                costLayout.childForceExpandHeight = true;

                MakeTMP("CostLabel", row.transform, "COST", 15, TextAlignmentOptions.Left);

                // Spacer — pushes RPIcon+CostValue to the right
                var costSpacer = MakeRect("Spacer", row.transform);
                costSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;

                // RPIcon placeholder (gold square; swap sprite in Inspector)
                var iconGO = MakeRect("RPIcon", row.transform);
                iconGO.AddComponent<Image>().color = new Color(1f, 0.85f, 0.1f, 1f);
                LayoutEl(iconGO, preferredWidth: 20, preferredHeight: 20);

                MakeTMP("CostValue", row.transform, "100", 15, TextAlignmentOptions.Left);
            }

            // RewardRow — "REWARD" left | spacer | "1 SP" right
            {
                var row = MakeRect("RewardRow", info.transform);
                LayoutEl(row, preferredHeight: 22);
                var rewardLayout = row.AddComponent<HorizontalLayoutGroup>();
                rewardLayout.childAlignment        = TextAnchor.MiddleLeft;
                rewardLayout.childControlWidth     = true;
                rewardLayout.childControlHeight    = true;
                rewardLayout.childForceExpandWidth  = false;
                rewardLayout.childForceExpandHeight = true;

                MakeTMP("RewardLabel", row.transform, "REWARD", 15, TextAlignmentOptions.Left);

                // Spacer
                var rewardSpacer = MakeRect("Spacer", row.transform);
                rewardSpacer.AddComponent<LayoutElement>().flexibleWidth = 1;

                MakeTMP("RewardValue", row.transform, "1 SP", 15, TextAlignmentOptions.Left);
            }
        }

        private static void BuildLevelUpButton(Transform parent)
        {
            var go = MakeRect("LevelUpButton", parent);
            LayoutEl(go, preferredHeight: 50);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.72f, 0.2f, 1f); // gold

            go.AddComponent<Button>();

            var label = MakeTMP("Text", go.transform, "LEVEL UP", 20, TextAlignmentOptions.Center);
            label.color = Color.black;
            StretchFull(label.GetComponent<RectTransform>());
        }

        private static void BuildSPSection(Transform parent)
        {
            var sp = MakeRect("SPSection", parent);
            LayoutEl(sp, preferredHeight: 280);

            var layout = sp.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;

            // AvailableSPRow — "AVAILABLE SP  0 SP"
            {
                var row = MakeHRow("AvailableSPRow", sp.transform, 28);
                MakeTMP("AvailableSPLabel", row.transform, "AVAILABLE SP", 15, TextAlignmentOptions.Left);
                MakeTMP("AvailableSPValue", row.transform, "0 SP",         15, TextAlignmentOptions.Right);
            }

            // 4 Stat rows (same structure)
            BuildStatRow(sp.transform, "StatRow_Strength",    "STRENGTH");
            BuildStatRow(sp.transform, "StatRow_ClubControl", "CLUB CONTROL");
            BuildStatRow(sp.transform, "StatRow_Recovery",    "RECOVERY");
            BuildStatRow(sp.transform, "StatRow_Stamina",     "STAMINA");

            // ResetButton
            var resetGO = MakeRect("ResetButton", sp.transform);
            LayoutEl(resetGO, preferredHeight: 36);
            var resetImg = resetGO.AddComponent<Image>();
            resetImg.color = new Color(0.45f, 0.45f, 0.45f, 1f); // silver
            resetGO.AddComponent<Button>();
            var resetLabel = MakeTMP("Text", resetGO.transform, "RESET", 15, TextAlignmentOptions.Center);
            resetLabel.color = Color.white;
            StretchFull(resetLabel.GetComponent<RectTransform>());
        }

        /// <summary>
        /// Builds one stat row:
        ///   StatRow_X (HorizontalLayoutGroup)
        ///   ├── StatIcon        Image placeholder
        ///   ├── StatName        TMP label
        ///   ├── PendingLabel    TMP "+N" (orange, starts inactive)
        ///   ├── StatBar         container
        ///   │   ├── BarPending  Image filled (orange) — rendered BEHIND blue bar
        ///   │   └── Bar         Image filled (blue)   — rendered IN FRONT
        ///   ├── StatValue       TMP "current/cap"
        ///   └── PlusButton      gold Button [+]
        /// </summary>
        private static void BuildStatRow(Transform parent, string rowName, string statLabel)
        {
            var row = MakeRect(rowName, parent);
            LayoutEl(row, preferredHeight: 36);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            // StatIcon
            var icon = MakeRect("StatIcon", row.transform);
            icon.AddComponent<Image>().color = new Color(0.55f, 0.55f, 0.55f, 1f);
            LayoutEl(icon, preferredWidth: 28, preferredHeight: 28);

            // StatName
            var name = MakeTMP("StatName", row.transform, statLabel, 13, TextAlignmentOptions.Left);
            LayoutEl(name.gameObject, preferredWidth: 88);

            // PendingLabel "+N" — orange, hidden until there is a pending allocation
            var pending = MakeTMP("PendingLabel", row.transform, "+1", 13, TextAlignmentOptions.Left);
            pending.color = new Color(1f, 0.7f, 0.2f, 1f);
            LayoutEl(pending.gameObject, preferredWidth: 26);
            pending.gameObject.SetActive(false);

            // StatBar container
            var barContainer = MakeRect("StatBar", row.transform);
            LayoutEl(barContainer, preferredWidth: 140, preferredHeight: 14);

            // BarPending (orange) — first child → lower draw order → renders BEHIND blue bar
            var barPendingGO = MakeRect("BarPending", barContainer.transform);
            StretchFull(barPendingGO.GetComponent<RectTransform>());
            var barPendingImg = barPendingGO.AddComponent<Image>();
            barPendingImg.color = new Color(1f, 0.7f, 0.2f, 1f);
            barPendingImg.type = Image.Type.Filled;
            barPendingImg.fillMethod = Image.FillMethod.Horizontal;
            barPendingImg.fillOrigin = 0;
            barPendingImg.fillAmount = 0f;
            barPendingGO.SetActive(false); // shown only when pendingAmount > 0

            // Bar (blue) — second child → higher draw order → renders IN FRONT of orange
            var barGO = MakeRect("Bar", barContainer.transform);
            StretchFull(barGO.GetComponent<RectTransform>());
            var barImg = barGO.AddComponent<Image>();
            barImg.color = new Color(0.2f, 0.6f, 1f, 1f);
            barImg.type = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.fillOrigin = 0;
            barImg.fillAmount = 0.4f; // placeholder — overridden at runtime

            // StatValueCurrent + StatValueMax — split for different font sizes
            // e.g. "10" (larger) + "/25" (smaller), rendered side-by-side
            var valueCurrent = MakeTMP("StatValueCurrent", row.transform, "10",  13, TextAlignmentOptions.Right);
            LayoutEl(valueCurrent.gameObject, preferredWidth: 26);
            var valueMax = MakeTMP("StatValueMax", row.transform, "/25", 11, TextAlignmentOptions.Right);
            LayoutEl(valueMax.gameObject, preferredWidth: 30);

            // PlusButton
            var plusGO = MakeRect("PlusButton", row.transform);
            LayoutEl(plusGO, preferredWidth: 32, preferredHeight: 32);
            plusGO.AddComponent<Image>().color = new Color(0.85f, 0.72f, 0.2f, 1f); // gold
            plusGO.AddComponent<Button>();
            var plusLabel = MakeTMP("Text", plusGO.transform, "[+]", 14, TextAlignmentOptions.Center);
            plusLabel.color = Color.black;
            StretchFull(plusLabel.GetComponent<RectTransform>());
        }

        private static void BuildFooterSection(Transform parent)
        {
            var footer = MakeRect("FooterSection", parent);
            LayoutEl(footer, preferredHeight: 56);

            var layout = footer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // CancelButton — silver
            var cancelGO = MakeRect("CancelButton", footer.transform);
            cancelGO.AddComponent<Image>().color = new Color(0.45f, 0.45f, 0.45f, 1f);
            cancelGO.AddComponent<Button>();
            var cancelLabel = MakeTMP("Text", cancelGO.transform, "CANCEL", 17, TextAlignmentOptions.Center);
            cancelLabel.color = Color.white;
            StretchFull(cancelLabel.GetComponent<RectTransform>());

            // ConfirmButton — gray until enabled, gold when enabled (set by controller)
            var confirmGO = MakeRect("ConfirmButton", footer.transform);
            confirmGO.AddComponent<Image>().color = new Color(0.6f, 0.6f, 0.6f, 1f); // starts gray
            confirmGO.AddComponent<Button>();
            var confirmLabel = MakeTMP("Text", confirmGO.transform, "CONFIRM", 17, TextAlignmentOptions.Center);
            confirmLabel.color = Color.white;
            StretchFull(confirmLabel.GetComponent<RectTransform>());
        }

        // ── Primitive helpers ────────────────────────────────────────────────

        private static GameObject MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static TextMeshProUGUI MakeTMP(string name, Transform parent,
            string text, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = Color.white;
            tmp.alignment = alignment;
            return tmp;
        }

        /// <summary>Creates a horizontal-layout row with a fixed preferred height.</summary>
        private static GameObject MakeHRow(string name, Transform parent, float height)
        {
            var row = MakeRect(name, parent);
            LayoutEl(row, preferredHeight: height);

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void CenterAnchored(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
        }

        private static void LayoutEl(GameObject go, float preferredWidth = -1, float preferredHeight = -1)
        {
            var le = go.AddComponent<LayoutElement>();
            if (preferredWidth  >= 0) le.preferredWidth  = preferredWidth;
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
        }
    }
}
#endif
