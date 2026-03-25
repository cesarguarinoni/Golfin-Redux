#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory.Editor
{
    /// <summary>
    /// Builds / strips the ClubRepairModal hierarchy inside ClubRepairModal.
    ///
    /// Two steps in one:
    ///   1. Strip LevelUp-specific nodes from the cloned ClubLevelUpModal hierarchy
    ///      (InfoSection extra rows, LevelUpButton, SPSection, ResetButton dividers)
    ///   2. Build DurabilitySection + KitSection in their place
    ///
    /// Resulting ModalPanel structure:
    ///   HeaderSection / ClubNameText
    ///   Divider
    ///   InfoSection / RarityLevelRow / RarityLabel + LevelText
    ///   Divider
    ///   DurabilitySection / DurabilityLabel
    ///                      / DurabilityBarRow / DurabilityBar + DurabilityBarPreview
    ///                      / DurabilityValueText
    ///                      / DurabilityChangeText
    ///   Divider
    ///   KitSection / KitButtonsRow / StandardKitButton (KitLabel, KitCountText, SelectedIndicator)
    ///                              / PremiumKitButton  (KitLabel, KitCountText, SelectedIndicator)
    ///              / NoKitsMessage
    ///   Divider
    ///   FooterSection / CancelButton / Text
    ///                 / ConfirmButton / Text
    ///
    /// Run: GOLFIN/Build/Club Repair Modal Sections
    /// Safe to re-run — removes DurabilitySection and KitSection before rebuilding them.
    /// Must be run AFTER cloning ClubLevelUpModal and renaming to "ClubRepairModal".
    /// </summary>
    public static class ClubRepairModalBuilder
    {
        // Layout constants — match ClubLevelUpModal visual style
        private const float MODAL_PAD        = 16f;
        private const float SECTION_PAD      = 8f;
        private const float DIVIDER_H        = 1f;
        private const float LABEL_H          = 22f;
        private const float BAR_H            = 20f;
        private const float VALUE_H          = 20f;
        private const float BUTTON_H         = 56f;
        private const float SPACING          = 6f;

        private static readonly Color DividerColor       = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color BarBlue            = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color BarGreenPreview    = new Color(0.2f, 0.8f, 0.2f, 0.6f);
        private static readonly Color KitButtonNormal   = new Color(0.15f, 0.18f, 0.22f, 1f);
        private static readonly Color SelectedTint       = new Color(0.2f, 0.6f, 1f, 0.35f);

        [MenuItem("GOLFIN/Build/Club Repair Modal Sections")]
        public static void Run()
        {
            // ── Find or clone the ClubRepairModal ─────────────────────────────
            var repairModalGO = FindOrClone();
            if (repairModalGO == null) return;

            // ── Swap controller component ─────────────────────────────────────
            var oldCtrl = repairModalGO.GetComponent<ClubLevelUpModalController>();
            if (oldCtrl != null)
            {
                Object.DestroyImmediate(oldCtrl);
                Debug.Log("[ClubRepairModalBuilder] Removed ClubLevelUpModalController.");
            }

            var ctrl = repairModalGO.GetComponent<ClubRepairModalController>();
            if (ctrl == null)
            {
                ctrl = repairModalGO.AddComponent<ClubRepairModalController>();
                Debug.Log("[ClubRepairModalBuilder] Added ClubRepairModalController.");
            }

            var root       = repairModalGO.transform;
            var modalPanel = root.Find("ModalPanel");
            if (modalPanel == null)
            {
                Debug.LogError("[ClubRepairModalBuilder] ModalPanel not found under ClubRepairModal.");
                return;
            }

            // ── Step 1: Strip LevelUp-specific nodes ──────────────────────────
            StripLevelUpNodes(modalPanel);

            // ── Step 2: Clean up old DurabilitySection / KitSection (safe re-run) ──
            DestroyChild(modalPanel, "DurabilitySection");
            DestroyChild(modalPanel, "KitSection");

            // ── Step 3: Collect existing stable anchors ────────────────────────
            var headerSection = modalPanel.Find("HeaderSection");
            var infoSection   = modalPanel.Find("InfoSection");
            var footerSection = modalPanel.Find("FooterSection");

            if (headerSection == null || infoSection == null || footerSection == null)
            {
                Debug.LogError("[ClubRepairModalBuilder] HeaderSection / InfoSection / FooterSection missing. " +
                               "Make sure you cloned from ClubLevelUpModal.");
                return;
            }

            // ── Step 4: Build DurabilitySection ──────────────────────────────
            var durSection = BuildDurabilitySection(modalPanel);

            // ── Step 5: Build KitSection ──────────────────────────────────────
            var kitSection = BuildKitSection(modalPanel);

            // ── Step 6: Reorder ModalPanel children ───────────────────────────
            //   HeaderSection → Divider → InfoSection → Divider → DurabilitySection
            //   → Divider → KitSection → Divider → FooterSection
            ReorderModalPanel(modalPanel, headerSection, infoSection, durSection, kitSection, footerSection);

            // ── Step 7: Finalize ──────────────────────────────────────────────
            EditorUtility.SetDirty(repairModalGO);
            EditorSceneManager.MarkSceneDirty(repairModalGO.scene);

            Debug.Log("[ClubRepairModalBuilder] Done. Now run GOLFIN/Wire/Club Repair Modal to wire fields.");
        }

        // ── Find or Clone ─────────────────────────────────────────────────────

        private static GameObject FindOrClone()
        {
            // First try to find by controller
            var existing = Object.FindObjectOfType<ClubRepairModalController>(includeInactive: true);
            if (existing != null) return existing.gameObject;

            // Then try by name
            var byName = GameObject.Find("ClubRepairModal");
            if (byName != null) return byName;

            // Fall back: find an inactive one (name search misses inactive GOs)
            var allGOs = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allGOs)
            {
                if (go.name == "ClubRepairModal" && go.scene.IsValid()) return go;
            }

            // Try to clone ClubLevelUpModal
            var lvlUpCtrl = Object.FindObjectOfType<ClubLevelUpModalController>(includeInactive: true);
            if (lvlUpCtrl != null)
            {
                var clone = Object.Instantiate(lvlUpCtrl.gameObject,
                    lvlUpCtrl.transform.parent, false);
                clone.name = "ClubRepairModal";
                Debug.Log("[ClubRepairModalBuilder] Cloned ClubLevelUpModal → ClubRepairModal.");
                return clone;
            }

            Debug.LogError("[ClubRepairModalBuilder] ClubRepairModal not found and ClubLevelUpModal " +
                           "couldn't be cloned. Please clone ClubLevelUpModal manually and rename it.");
            return null;
        }

        // ── Strip ─────────────────────────────────────────────────────────────

        private static void StripLevelUpNodes(Transform modalPanel)
        {
            // Remove LevelUpButton
            DestroyChild(modalPanel, "LevelUpButton");

            // Remove SPSection (contains all stat rows, AvailableSPRow, ResetButton)
            DestroyChild(modalPanel, "SPSection");

            // Remove extra info rows from InfoSection (keep only RarityLevelRow)
            var infoSection = modalPanel.Find("InfoSection");
            if (infoSection != null)
            {
                DestroyChild(infoSection, "NextLevelRow");
                DestroyChild(infoSection, "CostRow");
                DestroyChild(infoSection, "RewardRow");
            }

            // Remove excess dividers — keep at most 2 (after HeaderSection, before FooterSection)
            // We'll rebuild all needed dividers in ReorderModalPanel
            RemoveAllDividers(modalPanel);

            Debug.Log("[ClubRepairModalBuilder] Stripped LevelUp-specific nodes.");
        }

        private static void RemoveAllDividers(Transform parent)
        {
            // Collect dividers by name; remove all — we'll add them back in the right places
            var toDestroy = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == "Divider") toDestroy.Add(child);
            }
            foreach (var t in toDestroy)
                Object.DestroyImmediate(t.gameObject);
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            if (parent == null) return;
            var child = parent.Find(childName);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
                Debug.Log($"[ClubRepairModalBuilder] Deleted '{childName}'.");
            }
        }

        // ── Build DurabilitySection ───────────────────────────────────────────

        private static Transform BuildDurabilitySection(Transform modalPanel)
        {
            var sec = MakeVLG("DurabilitySection", modalPanel, SECTION_PAD, SPACING);

            // DurabilityLabel
            MakeTMP("DurabilityLabel", sec, "DURABILITY", 14, LABEL_H, TextAlignmentOptions.Left);

            // DurabilityBarRow — two overlapping fill bars
            var barRow = MakeRect("DurabilityBarRow", sec);
            SetLayout(barRow, -1, BAR_H);  // fixed height, flexible width

            // DurabilityBar (blue, behind)
            var durBar = MakeFilledBar("DurabilityBar", barRow, BarBlue, 0);
            // DurabilityBarPreview (green, on top)
            var previewBar = MakeFilledBar("DurabilityBarPreview", barRow, BarGreenPreview, 1);
            previewBar.gameObject.SetActive(false); // hidden until a kit is selected

            // DurabilityValueText  e.g.  "55/100"
            MakeTMP("DurabilityValueText", sec, "—/—", 14, VALUE_H, TextAlignmentOptions.Left);

            // DurabilityChangeText  e.g.  "+45"  (green, shown when kit selected)
            var changeText = MakeTMP("DurabilityChangeText", sec, "", 13, VALUE_H, TextAlignmentOptions.Left);
            changeText.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            changeText.gameObject.SetActive(false);

            return sec;
        }

        // ── Build KitSection ─────────────────────────────────────────────────

        private static Transform BuildKitSection(Transform modalPanel)
        {
            var sec = MakeVLG("KitSection", modalPanel, SECTION_PAD, SPACING);

            // KitButtonsRow — side-by-side horizontal layout
            var row = MakeRect("KitButtonsRow", sec);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing            = SPACING;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.padding            = new RectOffset(0, 0, 0, 0);
            SetLayout(row, -1, BUTTON_H);

            // StandardKitButton
            BuildKitButton("StandardKitButton", row, "Standard Kit", "×5");

            // PremiumKitButton
            BuildKitButton("PremiumKitButton", row, "Premium Kit", "×2");

            // NoKitsMessage (hidden by default, shown when no kits)
            var noKits = MakeTMP("NoKitsMessage", sec, "You don't own any Repair Kits.", 12, VALUE_H, TextAlignmentOptions.Center);
            noKits.color = Color.gray;
            noKits.gameObject.SetActive(false);

            return sec;
        }

        private static void BuildKitButton(string name, Transform parent, string label, string count)
        {
            // Button container
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);

            var img = btnGO.AddComponent<Image>();
            img.color = KitButtonNormal;
            img.raycastTarget = true;

            var btn = btnGO.AddComponent<Button>();

            var vlg = btnGO.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment       = TextAnchor.MiddleCenter;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing              = 4f;
            vlg.padding              = new RectOffset(6, 6, 8, 8);

            var le = btnGO.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            // KitLabel
            MakeTMP("KitLabel", btnGO.transform, label, 12, LABEL_H, TextAlignmentOptions.Center);

            // KitCountText  e.g. "×5"
            var countTMP = MakeTMP("KitCountText", btnGO.transform, count, 16, 22, TextAlignmentOptions.Center);
            countTMP.fontStyle = FontStyles.Bold;

            // SelectedIndicator — colored overlay (start inactive)
            var selGO = new GameObject("SelectedIndicator");
            selGO.transform.SetParent(btnGO.transform, false);
            var selImg = selGO.AddComponent<Image>();
            selImg.color = SelectedTint;
            selImg.raycastTarget = false;
            var selRT = selGO.GetComponent<RectTransform>();
            selRT.anchorMin = Vector2.zero;
            selRT.anchorMax = Vector2.one;
            selRT.sizeDelta = Vector2.zero;
            var selLE = selGO.AddComponent<LayoutElement>();
            selLE.ignoreLayout = true;
            selGO.SetActive(false);
        }

        // ── Reorder ModalPanel children ───────────────────────────────────────

        private static void ReorderModalPanel(Transform modal,
            Transform header, Transform infoSec,
            Transform durSec, Transform kitSec,
            Transform footer)
        {
            // Build the desired order, inserting dividers between sections
            int idx = 0;

            header.SetSiblingIndex(idx++);
            idx = InsertDivider(modal, idx);
            infoSec.SetSiblingIndex(idx++);
            idx = InsertDivider(modal, idx);
            durSec.SetSiblingIndex(idx++);
            idx = InsertDivider(modal, idx);
            kitSec.SetSiblingIndex(idx++);
            idx = InsertDivider(modal, idx);
            footer.SetSiblingIndex(idx++);

            // Remove any leftover children past expected count (shouldn't happen, but safety)
            while (modal.childCount > idx)
                Object.DestroyImmediate(modal.GetChild(idx).gameObject);
        }

        private static int InsertDivider(Transform parent, int siblingIndex)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(siblingIndex);

            var img = go.AddComponent<Image>();
            img.color        = DividerColor;
            img.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight       = DIVIDER_H;
            le.preferredHeight = DIVIDER_H;
            le.flexibleWidth   = 1f;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.5f);
            rt.anchorMax = new Vector2(1, 0.5f);

            return siblingIndex + 1;
        }

        // ── UI Helpers ────────────────────────────────────────────────────────

        private static Transform MakeVLG(string name, Transform parent, float pad, float spacing)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            vlg.childAlignment         = TextAnchor.UpperLeft;
            vlg.spacing                = spacing;
            vlg.padding                = new RectOffset((int)pad, (int)pad, (int)pad, (int)pad);

            var csf = go.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go.transform;
        }

        private static Transform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go.transform;
        }

        private static void SetLayout(Transform t, float width, float height)
        {
            var le = t.GetComponent<LayoutElement>();
            if (le == null) le = t.gameObject.AddComponent<LayoutElement>();
            if (width  >= 0) { le.minWidth  = width;  le.preferredWidth  = width; }
            else              { le.flexibleWidth = 1f; }
            if (height >= 0) { le.minHeight = height; le.preferredHeight = height; }
        }

        private static Image MakeFilledBar(string name, Transform parent, Color color, int siblingIndex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(siblingIndex);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin  = Vector2.zero;
            rt.anchorMax  = Vector2.one;
            rt.sizeDelta  = Vector2.zero;
            rt.offsetMin  = Vector2.zero;
            rt.offsetMax  = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.type        = Image.Type.Filled;
            img.fillMethod  = Image.FillMethod.Horizontal;
            img.fillOrigin  = 0;
            img.fillAmount  = 1f;
            img.color       = color;
            img.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true; // both bars fill the parent container; layout is handled by the row

            return img;
        }

        private static TextMeshProUGUI MakeTMP(string name, Transform parent,
            string text, float fontSize, float height, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text           = text;
            tmp.fontSize       = fontSize;
            tmp.alignment      = align;
            tmp.raycastTarget  = false;
            tmp.color          = Color.white;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight       = height;
            le.preferredHeight = height;
            le.flexibleWidth   = 1f;

            return tmp;
        }
    }
}
#endif
