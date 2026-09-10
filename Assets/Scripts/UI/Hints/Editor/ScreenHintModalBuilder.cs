#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.EditorTools.Hints
{
    /// <summary>
    /// Builds <c>Assets/Prefabs/UI/Modals/ScreenHintModal.prefab</c> — the screen-hint modal
    /// (<c>screen_hints</c>, Figma page <c>Tutorial</c>: overlay kit <c>14263:39325</c>, Roster
    /// hint <c>14263:109304</c>, last-hint state <c>14266:109661</c>).
    ///
    /// <para><b>Rule 19 — clone provenance.</b> Step 1 is literally
    /// <c>AssetDatabase.CopyAsset(SchemeConfirmModal.prefab, …)</c>, the same shape
    /// <c>SchemeConfirmModalBuilder</c> uses. Kept from the copy, sprite and all: the scrim
    /// (<c>DimBackground</c>), the opaque navy <c>Background - HoleCard</c> plate
    /// (<c>064cba0b0bc85154995fa70dd470817b</c>), the gold 66 px <c>TitleText</c>, the
    /// <c>Divider</c> separator (<c>332237826c3743344947e9828762c2ae</c>), and the button row with
    /// BOTH buttons — silver <c>ButtonCancel</c> (<c>6021c639e9c124b44a06c8ccd977896f</c>) →
    /// BACK, gold <c>Button - Retry</c> (<c>aee5ccf2ef2d6b24ca9143186a08aa50</c>) → CONTINUE /
    /// CLOSE — with their <c>ButtonPressFeedback</c>, plus <c>animateShow: 1</c> and the 600
    /// sorting canvas. Deleted: <c>StepsRow</c>, <c>HowItWorksRow</c>, <c>FooterRow</c>, the
    /// <c>SchemeConfirmModalController</c> and the scrim's <c>ModalBackdropDismiss</c> (no
    /// backdrop dismiss on a hint — §2.1).</para>
    ///
    /// <para><b>The content is the loading card's own.</b> <c>TipContent</c> (its
    /// <c>CanvasGroup</c>, <c>TipText</c> with its Rubik-SemiBold face + <c>LocalizedText</c>, and
    /// <c>TipImage</c>) is <c>Object.Instantiate</c>d from ShellScene's
    /// <c>ProTipCard/TipContent</c> into the plate, so the modal and the loading screen render
    /// one tip identically and cannot drift. Only widths are re-set per the node: text 990 in
    /// the 1086 plate (48 px sides), diagram 806 wide, centred.</para>
    ///
    /// <para><b>Added:</b> <c>Counter</c>, a clone of <c>TitleText</c> (same font asset /
    /// material / gold) at the node's 45 px, right-aligned, anchored top-right of the plate 64 px
    /// in and 36 px down (<c>14263:109274</c>), <c>ignoreLayout</c>; and a <c>LayoutElement</c>
    /// on <c>Panel</c> so the controller can ease the plate height exactly as <c>ProTipCard</c>
    /// eases the card.</para>
    ///
    /// <para>Menu: <c>GOLFIN ▸ Build ▸ Screen Hint Modal</c>. Re-runnable — the copy goes to a
    /// scratch path and is saved OVER the target so the GUID (and both scene instances)
    /// survive a rebuild.</para>
    /// </summary>
    public static class ScreenHintModalBuilder
    {
        public const string SourcePrefab = "Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab";
        public const string TargetPrefab = "Assets/Prefabs/UI/Modals/ScreenHintModal.prefab";

        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";
        const string TipsCsvPath    = "Assets/Resources/Data/LoadingTips.csv";
        const string ArtFolder      = "Assets/Art/LoadingScreen";

        /// <summary>node_px → authored px for a SemiBold run: the project's SemiBold face renders
        /// ~11 % larger than the node's (SchemeConfirmModalBuilder, the Main Buttons calibration).</summary>
        const float SemiBoldSize = 59f / 66f;
        static float SB(float nodePx) => nodePx * SemiBoldSize;

        // ── Node geometry (Pop-up 14263:39326 is 1086 wide) ───────────────────
        const float PanelW        = 1086f;
        const float CounterNodePx = 45f;    // 14263:109274 Rubik SemiBold 45
        const float CounterRight  = 64f;    // right edge 64 px in from the plate's right
        const float CounterTop    = 36f;    // top 36 px down from the plate's top
        const float SideMargin    = 48f;    // body text 990 wide in 1086
        const float BodyW         = 990f;   // 14263:39335
        const float BodyTopPad    = 12f;    // 12 px top padding on the text
        const float ContentGap    = 24f;    // text → diagram 24 (14263:39327 gap 24)
        const float DiagramW      = 806f;   // 14263:109279
        const float ButtonW       = 450f;   // the Unity Main Button width — kept (428 is Figma's hug)
        const float ButtonH       = 120f;

        [MenuItem("GOLFIN/Build/Screen Hint Modal")]
        public static void Build()
        {
            string dir = Path.GetDirectoryName(TargetPrefab);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // Rule 19: the prefab IS a copy of the shipping scheme-confirm modal. Scratch path +
            // SaveAsPrefabAsset over the target keeps the target's GUID across rebuilds.
            const string scratch = "Assets/Prefabs/UI/Modals/~ScreenHintModal_scratch.prefab";
            AssetDatabase.DeleteAsset(scratch);
            if (!AssetDatabase.CopyAsset(SourcePrefab, scratch))
                throw new System.Exception("[ScreenHintModalBuilder] CopyAsset failed: " + SourcePrefab);
            AssetDatabase.ImportAsset(scratch, ImportAssetOptions.ForceUpdate);

            GameObject root = PrefabUtility.LoadPrefabContents(scratch);
            try
            {
                Populate(root);
                PrefabUtility.SaveAsPrefabAsset(root, TargetPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.DeleteAsset(scratch);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ScreenHintModalBuilder] built " + TargetPrefab + " (cloned from " + SourcePrefab + ").");
        }

        // ─────────────────────────────────────────────────────────────────────

        static void Populate(GameObject root)
        {
            root.name = "ScreenHintModal";

            // The controller replaces the clone's. Every serialized reference is re-bound below.
            foreach (var old in root.GetComponents<MonoBehaviour>().ToArray())
                if (old != null && old is SchemeConfirmModalController)
                    Object.DestroyImmediate(old);

            var ctrl = root.GetComponent<ScreenHintModalController>();
            if (ctrl == null) ctrl = root.AddComponent<ScreenHintModalController>();

            // The Canvas exists so the modal HAS a sorting scope; overrideSorting is set by
            // ScreenHintModalController.Awake (a root canvas forces it off on the asset).
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.sortingOrder = ScreenHintModalController.SortingOrder;
            if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();

            var t = root.transform;

            // ── Scrim (cloned) — NO backdrop dismiss: only CONTINUE / CLOSE close a hint ──
            var dim = t.Find("DimBackground");
            if (dim == null) throw new System.Exception("clone source lost its DimBackground");
            var dismiss = dim.GetComponent<ModalBackdropDismiss>();
            if (dismiss != null) Object.DestroyImmediate(dismiss);

            // ── Panel (cloned plate) ──────────────────────────────────────────
            var panel = (RectTransform)t.Find("Panel");
            panel.sizeDelta = new Vector2(PanelW, panel.sizeDelta.y);

            // The height-tween pin (ProTipCard's _cardLayout). -1 = the fitter owns the height.
            var panelLe = panel.GetComponent<LayoutElement>();
            if (panelLe == null) panelLe = panel.gameObject.AddComponent<LayoutElement>();
            panelLe.preferredHeight = -1f;
            panelLe.minHeight = -1f;

            // Keep: Background, TitleRow, SeparatorRow, ButtonsRow. Drop the scheme pop-up's own.
            foreach (string gone in new[] { "StepsRow", "HowItWorksRow", "FooterRow" })
            {
                var row = panel.Find(gone);
                if (row != null) Object.DestroyImmediate(row.gameObject);
            }

            var titleRow     = panel.Find("TitleRow");
            var separatorRow = panel.Find("SeparatorRow");
            var buttonsRow   = (RectTransform)panel.Find("ButtonsRow");
            if (titleRow == null || separatorRow == null || buttonsRow == null)
                throw new System.Exception("clone source lost TitleRow / SeparatorRow / ButtonsRow");

            // ── Title (cloned) — key TIP_HEADER ───────────────────────────────
            var titleText = titleRow.Find("TitleText").GetComponent<TextMeshProUGUI>();
            Key(titleText, ScreenHintModalController.TitleKey);

            // ── Counter — a clone of the title (same face / material / gold), 45 px ─
            var counter = Object.Instantiate(titleText.gameObject, panel, false);
            counter.name = "Counter";
            var counterLoc = counter.GetComponent<LocalizedText>();
            if (counterLoc != null) Object.DestroyImmediate(counterLoc);   // digits, no key
            var counterTmp = counter.GetComponent<TextMeshProUGUI>();
            counterTmp.fontSize  = SB(CounterNodePx);
            counterTmp.fontStyle = FontStyles.Normal;
            counterTmp.alignment = TextAlignmentOptions.TopRight;
            counterTmp.textWrappingMode = TextWrappingModes.NoWrap;
            counterTmp.characterSpacing = 0f;
            counterTmp.text = "1/4";
            var counterRt = (RectTransform)counter.transform;
            counterRt.anchorMin = counterRt.anchorMax = new Vector2(1f, 1f);
            counterRt.pivot = new Vector2(1f, 1f);
            counterRt.anchoredPosition = new Vector2(-CounterRight, -CounterTop);
            counterRt.sizeDelta = new Vector2(300f, SB(CounterNodePx) * 1.3f);
            var counterLe = counter.AddComponent<LayoutElement>();
            counterLe.ignoreLayout = true;

            // ── TipContent — the loading card's own subtree ───────────────────
            var tipContent = CloneTipContent(panel);
            tipContent.SetSiblingIndex(separatorRow.GetSiblingIndex() + 1);

            var tipText  = tipContent.Find("TipText").GetComponent<TextMeshProUGUI>();
            var tipImage = tipContent.Find("TipImage").GetComponent<Image>();
            var tipGroup = tipContent.GetComponent<CanvasGroup>();
            if (tipGroup == null) tipGroup = tipContent.gameObject.AddComponent<CanvasGroup>();

            // ── Buttons (cloned pair) ─────────────────────────────────────────
            buttonsRow.SetAsLastSibling();
            var back = SizeButton(buttonsRow.Find("CancelButton"),  "BackButton", ScreenHintModalController.BackKey);
            var next = SizeButton(buttonsRow.Find("ConfirmButton"), "NextButton", ScreenHintModalController.ContinueKey);
            var nextLabel = next.GetComponentInChildren<TextMeshProUGUI>(true);
            counter.transform.SetAsLastSibling();   // draws over every row; ignoreLayout keeps it out of the stack

            // ── Sprites — one entry per LoadingTips.csv row, by name ──────────
            var so = new SerializedObject(ctrl);
            // The clone's controller carried animateShow: 1 (Pop + scrim Fade); replacing the
            // component drops that serialized flag, so it is re-asserted here — the ModalPopTests
            // idiom. The class default stays false (GpsScreenTransitionTests pins it).
            so.FindProperty("animateShow").boolValue = true;
            so.FindProperty("modalPanel").objectReferenceValue  = panel.gameObject;
            so.FindProperty("backdrop").objectReferenceValue    = dim.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = null;   // no X, no cancel
            so.FindProperty("titleText").objectReferenceValue   = titleText;
            so.FindProperty("counterText").objectReferenceValue = counterTmp;
            so.FindProperty("tipContentGroup").objectReferenceValue = tipGroup;
            so.FindProperty("tipText").objectReferenceValue     = tipText;
            so.FindProperty("tipImage").objectReferenceValue    = tipImage;
            so.FindProperty("backButton").objectReferenceValue  = back;
            so.FindProperty("nextButton").objectReferenceValue  = next;
            so.FindProperty("nextLabel").objectReferenceValue   = nextLabel;
            WireSprites(so.FindProperty("tipSprites"));
            so.ApplyModifiedPropertiesWithoutUndo();

            // Author the panel INACTIVE (ModalController.Awake forces it anyway, and an active
            // modal panel in the scene throws on the first play-mode entry — the UIParticle
            // OnDisable scar). The ROOT stays active so Instance can find it.
            panel.gameObject.SetActive(false);
            dim.gameObject.SetActive(false);
            root.SetActive(true);
        }

        /// <summary>
        /// Instantiate ShellScene's <c>ProTipCard/TipContent</c> into the plate. Opens ShellScene
        /// additively if it is not already loaded, and closes it again afterwards.
        /// </summary>
        static RectTransform CloneTipContent(RectTransform panel)
        {
            Scene shell = SceneManager.GetSceneByPath(ShellScenePath);
            bool opened = false;
            if (!shell.IsValid() || !shell.isLoaded)
            {
                shell = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                ProTipCard card = null;
                foreach (var go in shell.GetRootGameObjects())
                {
                    card = go.GetComponentInChildren<ProTipCard>(true);
                    if (card != null) break;
                }
                if (card == null) throw new System.Exception("ShellScene has no ProTipCard — nothing to clone TipContent from");

                var src = card.transform.Find("TipContent");
                if (src == null) throw new System.Exception("ShellScene ProTipCard has no TipContent child");

                var clone = Object.Instantiate(src.gameObject, panel, false);
                clone.name = "TipContent";
                var rt = (RectTransform)clone.transform;
                rt.localScale = Vector3.one;

                // The loading card's column, re-margined to the node: 48 px sides, 12 px on top of
                // the text, 24 between text and diagram. The diagram hugs its own 806 width.
                var vlg = clone.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = clone.AddComponent<VerticalLayoutGroup>();
                vlg.padding = new RectOffset((int)SideMargin, (int)SideMargin, (int)BodyTopPad, 0);
                vlg.spacing = ContentGap;
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = false;
                vlg.childForceExpandHeight = false;

                var text = rt.Find("TipText");
                var tmp = text.GetComponent<TextMeshProUGUI>();
                tmp.raycastTarget = false;
                tmp.alignment = TextAlignmentOptions.Top;   // centred, as the card
                var tle = text.GetComponent<LayoutElement>();
                if (tle == null) tle = text.gameObject.AddComponent<LayoutElement>();
                tle.preferredWidth = BodyW;
                tle.minWidth = BodyW;

                var image = rt.Find("TipImage");
                var img = image.GetComponent<Image>();
                img.type = Image.Type.Simple;
                img.preserveAspect = true;
                img.raycastTarget = false;
                var ile = image.GetComponent<LayoutElement>();
                if (ile == null) ile = image.gameObject.AddComponent<LayoutElement>();
                ile.preferredWidth = DiagramW;
                ile.minWidth = DiagramW;

                return rt;
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(shell, removeScene: true);
            }
        }

        /// <summary>Rename a CLONED Main Button, size it to the Unity Main Button width and re-key
        /// its label. Image, sprite, ink colour and <c>ButtonPressFeedback</c> are the clone's.</summary>
        static Button SizeButton(Transform btn, string name, string key)
        {
            if (btn == null) throw new System.Exception("clone source lost a button");
            btn.name = name;

            var le = btn.GetComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = ButtonW;
            le.minHeight = le.preferredHeight = ButtonH;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            Key(label, key);

            return btn.GetComponent<Button>();
        }

        static void WireSprites(SerializedProperty arr)
        {
            var rows = LoadingTipCatalog.Parse(File.ReadAllText(TipsCsvPath));
            arr.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                string name = rows[i].sprite;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Path.Combine(ArtFolder, name + ".png"));
                if (sprite == null) Debug.LogWarning("[ScreenHintModalBuilder] no sprite at " + ArtFolder + "/" + name + ".png");
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = name;
                e.FindPropertyRelative("sprite").objectReferenceValue = sprite;
            }
        }

        /// <summary>Attach a <see cref="LocalizedText"/> and set its key. Nothing in this prefab
        /// carries a literal string a player can read.</summary>
        static void Key(TextMeshProUGUI tmp, string key)
        {
            var loc = tmp.GetComponent<LocalizedText>();
            if (loc == null) loc = tmp.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(loc);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
