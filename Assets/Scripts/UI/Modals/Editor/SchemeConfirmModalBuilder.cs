#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Golfin.Gameplay.UI.Controls;
using Golfin.UI.Modals;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.EditorTools.Modals
{
    /// <summary>
    /// Builds <c>Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab</c> — the control-scheme
    /// confirm pop-up (<c>scheme_confirm_popup</c>, Figma <c>14140:35469</c> "Pop-up" inside the
    /// chosen frame <c>14140:35361</c>).
    ///
    /// <para><b>Rule 19 — clone provenance.</b> Step 1 is literally
    /// <c>AssetDatabase.CopyAsset(StartingCharacterConfirmModal.prefab, …)</c>. Nothing in the
    /// chrome is re-authored: the scrim, the navy <c>Background - HoleCard</c> plate
    /// (<c>064cba0b0bc85154995fa70dd470817b</c>), the <c>Divider</c> separator
    /// (<c>332237826c3743344947e9828762c2ae</c>), the silver <c>ButtonCancel</c>
    /// (<c>6021c639e9c124b44a06c8ccd977896f</c>) and the GOLD <c>Button - Retry</c>
    /// (<c>aee5ccf2ef2d6b24ca9143186a08aa50</c>) all arrive with the copy and are kept, sprite and
    /// all. Only the middle of the panel is rebuilt.</para>
    ///
    /// <para>The plate was CHECKED against the node render rather than assumed: sampling
    /// <c>reference/popup_pendulum.png</c> at 1:1 gives a vertical navy gradient
    /// (17,50,81) → (9,27,51) behind a 3 px (195,200,208) stroke, and
    /// <c>Background - HoleCard.png</c> is (18,51,82) → (9,27,52) behind (195,200,208). Same
    /// plate, Δ ≤ 1 per channel — so no new art is imported for this task.</para>
    ///
    /// <para><b>Geometry is the node's, in Unity px.</b> The shell canvas is 1170x2532 at scale 1,
    /// so a Figma px IS a Unity px (FIGMA_SCREEN_BUILD_PLAYBOOK § 2). Every number below is
    /// annotated with the node it came from. The panel HUGS vertically — Free Swing's line 2 wraps
    /// to three lines where Pendulum's wraps to two, and the node's own frames differ in height
    /// for exactly that reason.</para>
    ///
    /// <para><b>Fonts.</b> The project convention (GpsGiftVoteBuilder, four approved GPS screens):
    /// SemiBold runs use <c>Rubik-SemiBold SDF</c> at <c>node_px * 59/66</c>, because the project's
    /// SemiBold face renders ~11 % larger than the face the node draws with; Medium/Regular runs
    /// use the variable face at the node's raw px. Rubik Bold is not shipped, so the node's Bold
    /// step indices use the SemiBold face — recorded as a known-unequal in the fidelity table.</para>
    ///
    /// <para>Menu: <c>GOLFIN ▸ Build ▸ Scheme Confirm Modal</c>. Re-runnable: it rebuilds the
    /// prefab from the clone source every time, so the prefab is a pure function of this file.</para>
    /// </summary>
    public static class SchemeConfirmModalBuilder
    {
        public const string SourcePrefab = "Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab";
        public const string TargetPrefab = "Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab";

        const string FontSemi = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        const string FontMed  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        /// <summary>
        /// How much to thicken a <see cref="FontMed"/> run so it reads as the node's Rubik MEDIUM.
        ///
        /// <para>The project ships <c>Rubik-SemiBold SDF</c> and the variable face and nothing
        /// else, and the variable face renders at Regular — measured, not assumed: with the node
        /// render and the built capture cropped to the SAME panel-local regions, the SemiBold runs
        /// (title, HOW IT WORKS header) matched at 0.98 and 0.99 ink coverage, while the Medium
        /// runs (the three body lines, the footer) came in at 0.67 and 0.65. The SemiBold pair is
        /// the control that rules out a rendering-pipeline difference: 0.67 is weight.</para>
        ///
        /// <para>Switching those runs to the SemiBold FACE is not the fix. Rendered at the same
        /// 34 px, that face measures 2.04x the ink AND 18 % wider (874 px of copy becomes 1035),
        /// which would move every line break away from the node's. <c>_FaceDilate</c> thickens
        /// WITHOUT changing advance width — measured identical preferred widths at dilate 0.00 and
        /// 0.08 — so the run keeps the node's face, size and line breaks and only gains weight.
        /// The value is calibrated, not guessed: rendering the node's own line at 34 px over the
        /// panel navy gives ink x1.00 / x1.20 / x1.47 at dilate 0.00 / 0.08 / 0.18, and the node's
        /// Medium sits at x1.49.</para>
        ///
        /// <para>It is a MATERIAL ASSET, not a runtime <c>fontMaterial</c> instance: an instanced
        /// material does not survive into a prefab, which is why the first attempt at this changed
        /// nothing at all (re-measured: still 0.671).</para>
        /// </summary>
        const float MediumFaceDilate = 0.18f;

        const string MediumMaterial = "Assets/Fonts/Rubik-VariableFont_wght Medium SDF.mat";

        /// <summary>The dilated Medium material, created on first build and reused after.</summary>
        static Material EnsureMediumMaterial()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontMed);
            var mat  = AssetDatabase.LoadAssetAtPath<Material>(MediumMaterial);
            if (mat == null)
            {
                mat = new Material(font.material) { name = "Rubik-VariableFont_wght Medium SDF" };
                AssetDatabase.CreateAsset(mat, MediumMaterial);
            }
            mat.shader = font.material.shader;
            mat.CopyPropertiesFromMaterial(font.material);
            mat.SetFloat(ShaderUtilities.ID_FaceDilate, MediumFaceDilate);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>node_px → authored px for every SemiBold run (the Main Buttons 66→59 calibration
        /// generalised; see the class remarks).</summary>
        const float SemiBoldSize = 59f / 66f;
        static float SB(float nodePx) => nodePx * SemiBoldSize;

        // ── Colours, read off reference/popup_pendulum.png at 1:1 ─────────────
        static readonly Color Gold  = Hex("#F5D66E");   // node #f5d66e, render (245,214,110) — exact
        static readonly Color White = Color.white;
        /// <summary>The node's <c>rgba(255,255,255,0.75)</c> footer, PRE-COMPOSITED against the
        /// panel gradient at that height. The project renders in linear space, so authoring the
        /// literal 75 % alpha lands visibly off the render's own pixel — the same trap the GPS
        /// pager dots hit. Measured composite in the node render: (193,199,205).</summary>
        static readonly Color FooterGrey = Hex("#C1C7CD");
        static readonly Color CancelInk  = Hex("#1E293B");   // node I14140:35612;2182:5461
        static readonly Color ConfirmInk = Hex("#321506");   // node I14140:35614;2180:1003

        // ── Node geometry (Pop-up 14140:35469 is 1086 wide) ───────────────────
        const float PanelW      = 1086f;
        const float TitleRowH   = 119f;   // Mission Title 120 tall, less the 1 px the stroke straddles
        const float SepH        = 2f;     // Divider, centred on the node's y=120 separator line
        const float SepW        = 978f;   // Separator 14140:35474 x=54 w=978
        const float TileW       = 314f;   // Tile 14140:35478
        const float TileH       = 340f;
        const float StepGap     = 24f;    // Step x 48 / 386 / 724 → 314 + 24
        const float SideMargin  = 48f;    // Steps left 48, right 1086-(724+314)=48
        const float StepsTopPad = 35f;    // separator bottom 121 → tile top 156 (measured in the render)
        const float CapGap      = 12f;    // caption y=352 vs tile bottom 340
        const float CapH        = 40f;    // "1  PULL" 14140:35519
        const float StepsBotPad = 12f;    // Steps frame 440 tall vs step 36+392
        const float HiwTopPad   = 24f;    // Steps end 560 → HowItWorks 584
        const float HiwGap      = 14f;    // HowItWorks gap-[14px]
        const float HiwHdrH     = 43f;    // "HOW IT WORKS" 14140:35935
        const float HiwW        = 990f;   // Line 14140:35936 w=990 (1086 - 2*48)
        const float LineIdxGap  = 16f;    // Line gap-[16px]
        const float FooterTopPad= 24f;    // HowItWorks end 909 → Goals 933
        const float FooterH     = 66f;    // Goals text 14140:35608 h=66 (lineHeight 66)
        const float BtnTopPad   = 24f;    // Goals end 999 → Buttons 1023
        const float BtnGap      = 48f;    // Buttons gap-[48px]
        const float CancelW     = 450f;   // Main Buttons 14140:35612
        const float ConfirmW    = 391f;   // Main Buttons 14140:35614
        const float BtnH        = 120f;
        const float PanelBotPad = 32f;    // Buttons end 1143 → Pop-up 1175

        [MenuItem("GOLFIN/Build/Scheme Confirm Modal")]
        public static void Build()
        {
            string dir = Path.GetDirectoryName(TargetPrefab);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // ── Rule 19: the prefab IS a copy of the shipping confirm modal ────
            //
            // The copy goes to a SCRATCH path and is then saved OVER the target, instead of
            // deleting the target and copying onto it. Deleting takes the .meta with it, so the
            // next CopyAsset mints a fresh GUID and every scene instance silently becomes an
            // orphan — which is exactly what a rebuild did on this task: both ShellScene and
            // LabScaffold lost their pop-up and nothing errored. SaveAsPrefabAsset overwrites in
            // place and keeps the GUID, so this menu is safe to re-run at any time.
            const string scratch = "Assets/Prefabs/UI/Modals/~SchemeConfirmModal_scratch.prefab";
            AssetDatabase.DeleteAsset(scratch);
            if (!AssetDatabase.CopyAsset(SourcePrefab, scratch))
                throw new System.Exception("[SchemeConfirmBuilder] CopyAsset failed: " + SourcePrefab);
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
            Debug.Log("[SchemeConfirmBuilder] built " + TargetPrefab + " (cloned from " + SourcePrefab + ").");
        }

        // ─────────────────────────────────────────────────────────────────────

        static void Populate(GameObject root)
        {
            root.name = "SchemeConfirmModal";

            // The controller. Its script replaces the clone's, but every serialized reference
            // below is re-bound explicitly rather than inherited.
            foreach (var old in root.GetComponents<MonoBehaviour>().ToArray())
                if (old != null && old.GetType().Name == "StartingCharacterConfirmModalController")
                    Object.DestroyImmediate(old);

            var ctrl = root.GetComponent<SchemeConfirmModalController>();
            if (ctrl == null) ctrl = root.AddComponent<SchemeConfirmModalController>();

            // Own sorting scope so the pop-up paints over the in-game gear modal (§1.5) and the
            // persistent chrome. Authored here so it is a property of the PREFAB, not of whichever
            // scene happens to instantiate it.
            // `== null`, never `??` — a destroyed/absent Unity component is fake-null and slips
            // straight through the null-coalescing operator (CLAUDE.md Basic rule 0.4).
            // The Canvas exists so the modal HAS a sorting scope; its order and overrideSorting
            // are set by SchemeConfirmModalController.Awake, not authored here — on the prefab
            // asset this is a ROOT canvas and Unity forces overrideSorting off, so anything
            // written here is a lie that a scene instance would have to override (and lose on the
            // next rebuild).
            var canvas = root.GetComponent<Canvas>();
            if (canvas == null) canvas = root.AddComponent<Canvas>();
            canvas.sortingOrder = SchemeConfirmModalController.SortingOrder;
            if (root.GetComponent<GraphicRaycaster>() == null) root.AddComponent<GraphicRaycaster>();

            var t = root.transform;

            // ── Scrim (cloned) — tap to dismiss ───────────────────────────────
            var dim = t.Find("DimBackground");
            if (dim == null) throw new System.Exception("clone source lost its DimBackground");
            if (dim.GetComponent<ModalBackdropDismiss>() == null)
                dim.gameObject.AddComponent<ModalBackdropDismiss>();

            // ── Panel (cloned plate) ──────────────────────────────────────────
            var panel = (RectTransform)t.Find("Panel");
            panel.sizeDelta = new Vector2(PanelW, panel.sizeDelta.y);

            var panelVlg = panel.GetComponent<VerticalLayoutGroup>();
            panelVlg.padding = new RectOffset(0, 0, 0, Mathf.RoundToInt(PanelBotPad));
            panelVlg.spacing = 0f;
            panelVlg.childAlignment = TextAnchor.UpperCenter;
            panelVlg.childControlWidth = true;
            panelVlg.childControlHeight = true;
            panelVlg.childForceExpandWidth = true;
            panelVlg.childForceExpandHeight = false;

            var fitter = panel.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // Keep: Background (the cloned plate), ButtonsRow (the cloned Main Buttons pair),
            // SeparatorWrapper (the cloned Divider). Drop the starter modal's own copy.
            var background       = panel.Find("Background");
            var separatorWrapper = panel.Find("SeparatorWrapper");
            var buttonsRow       = panel.Find("ButtonsRow");
            foreach (Transform child in panel.Cast<Transform>().ToList())
                if (child != background && child != separatorWrapper && child != buttonsRow)
                    Object.DestroyImmediate(child.gameObject);

            // ── Rows, in node order ───────────────────────────────────────────
            var titleRow = Row(panel, "TitleRow", TitleRowH);
            var titleText = Label(titleRow, "TitleText", SB(66), Gold, FontSemi,
                                  TextAlignmentOptions.Center, "SETTINGS_CONTROLS_PENDULUM",
                                  trackingPx: -0.78f, trackingNodeSize: 66f);
            titleText.fontStyle = FontStyles.UpperCase;   // node draws "PENDULUM"; the key is "Pendulum"
            Stretch((RectTransform)titleText.transform, 54f, 24f, 54f, 11f);

            separatorWrapper.SetSiblingIndex(titleRow.GetSiblingIndex() + 1);
            SeparatorRow(separatorWrapper);

            var stepsRow = StepsRow(panel);
            var hiwRow   = HowItWorksRow(panel);
            var footRow  = FooterRow(panel);

            buttonsRow.SetAsLastSibling();
            var (cancel, confirm) = ButtonsRow((RectTransform)buttonsRow);

            // ── Wire the controller ───────────────────────────────────────────
            var tiles    = new Image[3];
            var captions = new TextMeshProUGUI[3];
            var lines    = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                tiles[i]    = stepsRow.Find($"Step{i + 1}/Tile{i + 1}").GetComponent<Image>();
                captions[i] = stepsRow.Find($"Step{i + 1}/Caption{i + 1}/Label{i + 1}").GetComponent<TextMeshProUGUI>();
                lines[i]    = hiwRow.Find($"Line{i + 1}/LineText{i + 1}").GetComponent<TextMeshProUGUI>();
            }

            var so = new SerializedObject(ctrl);
            so.FindProperty("modalPanel").objectReferenceValue = panel.gameObject;
            so.FindProperty("backdrop").objectReferenceValue   = dim.gameObject;
            so.FindProperty("closeButton").objectReferenceValue = null;   // CANCEL is the close
            so.FindProperty("titleText").objectReferenceValue  = titleText;
            SetArray(so, "tileImages",   tiles);
            SetArray(so, "captionTexts", captions);
            SetArray(so, "lineTexts",    lines);
            so.FindProperty("cancelButton").objectReferenceValue  = cancel;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Author the panel INACTIVE (ModalController.Awake forces it anyway, and an active
            // modal panel in the scene throws on the first play-mode entry — see the
            // UIParticle.OnDisable scar). The ROOT stays active so Instance can find it.
            panel.gameObject.SetActive(false);
            dim.gameObject.SetActive(false);
            root.SetActive(true);

            _ = footRow;
        }

        // ── Row helpers ──────────────────────────────────────────────────────

        /// <summary>A plain fixed-height row in the panel's vertical stack.</summary>
        static RectTransform Row(Transform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            return rt;
        }

        static void SeparatorRow(Transform wrapper)
        {
            wrapper.name = "SeparatorRow";
            var vlg = wrapper.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;

            var le = wrapper.GetComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = SepH;

            var line = (RectTransform)wrapper.Find("ModalSeparator");
            line.sizeDelta = new Vector2(SepW, SepH);
            var lle = line.GetComponent<LayoutElement>();
            if (lle != null) { lle.minHeight = lle.preferredHeight = SepH; lle.preferredWidth = SepW; }
        }

        static RectTransform StepsRow(Transform panel)
        {
            var rt = Row(panel, "StepsRow", 0f);
            Object.DestroyImmediate(rt.GetComponent<LayoutElement>());   // hugs its children

            var hlg = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset((int)SideMargin, (int)SideMargin,
                                         Mathf.RoundToInt(StepsTopPad), Mathf.RoundToInt(StepsBotPad));
            hlg.spacing = StepGap;
            hlg.childAlignment = TextAnchor.UpperCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            for (int i = 1; i <= 3; i++) Step(rt, i);
            return rt;
        }

        static void Step(Transform parent, int n)
        {
            var go = new GameObject("Step" + n, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = TileW;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = CapGap;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Tile — the in-game capture. Sprite is bound at Show() from SchemeConfirmContent, so
            // the prefab ships with NO sprite and the Image disabled: a null sprite drawn as a
            // white 314x340 box is exactly the fabrication the UI-fidelity linter fails on.
            var tileGo = new GameObject("Tile" + n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            tileGo.transform.SetParent(rt, false);
            var tileImg = tileGo.GetComponent<Image>();
            tileImg.sprite = null;
            tileImg.enabled = false;
            tileImg.type = Image.Type.Simple;
            tileImg.preserveAspect = false;
            var tle = tileGo.AddComponent<LayoutElement>();
            tle.preferredWidth = tle.minWidth = TileW;
            tle.preferredHeight = tle.minHeight = TileH;

            // Caption — "<n>  <LABEL>". The numeral is typography (authored here), the label is a
            // localisation key bound at Show().
            var capGo = new GameObject("Caption" + n, typeof(RectTransform));
            capGo.transform.SetParent(rt, false);
            var capLe = capGo.AddComponent<LayoutElement>();
            capLe.preferredHeight = capLe.minHeight = CapH;
            var capHlg = capGo.AddComponent<HorizontalLayoutGroup>();
            capHlg.spacing = CaptionGap;
            capHlg.childAlignment = TextAnchor.MiddleCenter;
            capHlg.childControlWidth = capHlg.childControlHeight = true;
            capHlg.childForceExpandWidth = capHlg.childForceExpandHeight = false;

            var idx = Label(capGo.transform, "Index" + n, SB(34), White, FontSemi,
                            TextAlignmentOptions.Midline, null);
            idx.text = n.ToString();
            NoWrap(idx);

            var lab = Label(capGo.transform, "Label" + n, SB(34), White, FontSemi,
                            TextAlignmentOptions.Midline, "SCHEME_POPUP_PENDULUM_STEP" + n);
            NoWrap(lab);
        }

        /// <summary>The node draws the caption as one run, <c>"1&#160;&#160;PULL"</c> — two Rubik
        /// spaces at 34 px. Authored as two runs (the numeral is typography, § 3.1) with that same
        /// advance as the gap so the composed width still matches the node's 119 px.</summary>
        const float CaptionGap = 17f;

        static RectTransform HowItWorksRow(Transform panel)
        {
            var rt = Row(panel, "HowItWorksRow", 0f);
            Object.DestroyImmediate(rt.GetComponent<LayoutElement>());

            var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)SideMargin, (int)SideMargin, Mathf.RoundToInt(HiwTopPad), 0);
            vlg.spacing = HiwGap;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var hdr = Label(rt, "HowHeader", SB(36), Gold, FontSemi,
                            TextAlignmentOptions.TopLeft, SchemeConfirmContent.HowItWorksKey);
            var hle = hdr.gameObject.AddComponent<LayoutElement>();
            hle.preferredHeight = hle.minHeight = HiwHdrH;

            for (int i = 1; i <= 3; i++)
            {
                var lineGo = new GameObject("Line" + i, typeof(RectTransform));
                lineGo.transform.SetParent(rt, false);
                var hlg = lineGo.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = LineIdxGap;
                hlg.childAlignment = TextAnchor.UpperLeft;
                hlg.childControlWidth = hlg.childControlHeight = true;
                hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

                // Rubik Bold is not in the project; the node's Bold index uses the SemiBold face.
                var idx = Label(lineGo.transform, "LineIndex" + i, SB(36), Gold, FontSemi,
                                TextAlignmentOptions.TopLeft, null);
                idx.text = i.ToString();
                NoWrap(idx);
                var ile = idx.gameObject.AddComponent<LayoutElement>();
                ile.flexibleWidth = 0f;

                var body = Label(lineGo.transform, "LineText" + i, 34f, White, FontMed,
                                 TextAlignmentOptions.TopLeft, "SCHEME_POPUP_PENDULUM_LINE" + i);
                var ble = body.gameObject.AddComponent<LayoutElement>();
                // preferredWidth 0 + flexibleWidth 1 is the idiom that makes a HorizontalLayoutGroup
                // hand this run "whatever is left of the row" instead of the width its text WANTS.
                // Left at -1 the group asks TMP for its preferred width — which for an unwrapped
                // 90-character sentence is far wider than the 990 px row, and every HOW IT WORKS
                // line ran off the right edge of the panel in play mode while still measuring
                // "990 wide" against the short placeholder key in edit mode.
                ble.preferredWidth = 0f;
                ble.flexibleWidth  = 1f;
                body.textWrappingMode = TextWrappingModes.Normal;
            }

            return rt;
        }

        static RectTransform FooterRow(Transform panel)
        {
            var rt = Row(panel, "FooterRow", 0f);
            Object.DestroyImmediate(rt.GetComponent<LayoutElement>());

            var vlg = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset((int)SideMargin, (int)SideMargin, Mathf.RoundToInt(FooterTopPad), 0);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var foot = Label(rt, "FooterText", 34f, FooterGrey, FontMed,
                             TextAlignmentOptions.Center, SchemeConfirmContent.FooterKey,
                             trackingPx: -1.29f, trackingNodeSize: 34f);
            var le = foot.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = le.minHeight = FooterH;
            return rt;
        }

        static (Button cancel, Button confirm) ButtonsRow(RectTransform row)
        {
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(0, 0, Mathf.RoundToInt(BtnTopPad), 0);
            hlg.spacing = BtnGap;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            var rowLe = row.GetComponent<LayoutElement>();
            rowLe.minWidth = rowLe.preferredWidth = -1f;
            rowLe.minHeight = rowLe.preferredHeight = BtnTopPad + BtnH;

            var cancel  = SizeButton(row.Find("CancelButton"),  CancelW,  "MODAL_CANCEL",  CancelInk);
            var confirm = SizeButton(row.Find("ConfirmButton"), ConfirmW, "MODAL_CONFIRM", ConfirmInk);
            return (cancel, confirm);
        }

        /// <summary>Resize a CLONED Main Button to the node's width and re-key its label. The
        /// Image, its sprite and its <c>ButtonPressFeedback</c> are the clone's and untouched —
        /// CONFIRM keeps <c>Button - Retry</c>, the gold variant (never Copper).</summary>
        static Button SizeButton(Transform btn, float width, string key, Color ink)
        {
            var le = btn.GetComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = width;
            le.minHeight = le.preferredHeight = BtnH;

            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            label.fontSize  = SB(66);
            label.characterSpacing = Tracking(-0.78f, 66f);   // EN/Title_2
            label.color     = ink;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.UpperCase;
            Key(label, key);

            return btn.GetComponent<Button>();
        }

        // ── Primitives ───────────────────────────────────────────────────────

        /// <summary>
        /// The node's letter-spacing, converted to TMP's unit. Figma gives tracking in PIXELS at
        /// the run's own size (<c>tracking-[-0.78px]</c> on EN/Title_2, <c>-1.29px</c> on the
        /// footer); TMP's <c>characterSpacing</c> is a PERCENTAGE of the font size. Ignoring it is
        /// the "explicit spec token rendered absent" defect Rule 18 exists for.
        /// </summary>
        static float Tracking(float px, float nodeSizePx) => px / nodeSizePx * 100f;

        static TextMeshProUGUI Label(Transform parent, string name, float size, Color colour,
                                     string fontPath, TextAlignmentOptions align, string key,
                                     float trackingPx = 0f, float trackingNodeSize = 1f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font      = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            tmp.fontSize  = size;
            tmp.color     = colour;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.richText  = true;
            if (trackingPx != 0f) tmp.characterSpacing = Tracking(trackingPx, trackingNodeSize);

            if (fontPath == FontMed) tmp.fontSharedMaterial = EnsureMediumMaterial();
            tmp.text      = key != null ? "(" + key + ")" : "";   // obvious placeholder if a key never binds

            if (key != null) Key(tmp, key);
            return tmp;
        }

        /// <summary>A run that must hug its own text inside a layout group. NO ContentSizeFitter:
        /// TMP already implements <c>ILayoutElement</c>, so the parent HLG's
        /// <c>childControlWidth</c> sizes it from the same preferred width — adding a fitter puts
        /// two drivers on one rect and the pair disagree by a frame every time the text changes.</summary>
        static void NoWrap(TextMeshProUGUI tmp)
        {
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
        }

        /// <summary>Attach a <see cref="LocalizedText"/> and set its key. NOTHING in this prefab
        /// carries a literal string a player can read — <see cref="Label"/>'s placeholder is a
        /// parenthesised key so a binding regression is obvious on sight.</summary>
        static void Key(TextMeshProUGUI tmp, string key)
        {
            var loc = tmp.GetComponent<LocalizedText>();
            if (loc == null) loc = tmp.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(loc);
            so.FindProperty("key").stringValue = key;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Stretch(RectTransform rt, float left, float top, float right, float bottom)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void SetArray(SerializedObject so, string field, Object[] values)
        {
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
#endif
