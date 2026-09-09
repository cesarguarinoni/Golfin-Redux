// ─────────────────────────────────────────────────────────────────────────────
// asset_loans — the authoring pass, as a re-runnable builder.
//
// WHY A BUILDER AND NOT A ONE-SHOT SCRIPT. Every object this creates is checked
// for by name first and reused if it is there, so the whole menu item is
// IDEMPOTENT: re-running it after a scene revert, or after a reviewer restores
// a file from HEAD, reproduces the same hierarchy instead of a second copy of
// it. That is also what makes the work reviewable — the reviewer reads this
// rather than a diff of 4000 lines of scene YAML.
//
// CLONE PROVENANCE (Rule 19) is enforced by construction here: every reused
// atom is loaded by an explicit asset path and asserted non-null, and the run
// LOGS what it bound. A missing source stops the builder rather than falling
// back to a flat-colour fill.
//
// GOLFIN ▸ Loans ▸ Build Loan UI
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using System.Text;
using Golfin.UI.Loans;
using Golfin.UI.Polish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans.EditorTools
{
    public static class LoanUiBuilder
    {
        // ── the reused atoms, by path. Asserted, never guessed. ───────────────
        private const string ArtDir      = "Assets/Art/RosterScreen/";
        private const string SpriteRibbon        = ArtDir + "S_LoanRibbon.png";
        private const string SpriteBadge         = ArtDir + "S_LoanBadge.png";
        private const string SpriteRow           = ArtDir + "S_LoanRow.png";
        private const string SpriteRowSelected   = ArtDir + "S_LoanRowSelected.png";
        private const string SpriteIconOutBig    = ArtDir + "IconLoanOutBig.png";
        private const string SpriteIconInBig     = ArtDir + "IconLoanInBig.png";
        private const string SpriteIconOutSmall  = ArtDir + "IconLoanOutSmall.png";
        private const string SpriteIconInSmall   = ArtDir + "IconLoanInSmall.png";

        /// <summary>The SILVER SMALL button, native 235×56 — the same sprite the LEVEL UP / BOOST
        /// row above uses, which is why the new row can mirror it with no stretching at all.</summary>
        private const string SpriteButtonSilverSmall = ArtDir + "ButtonLevelUp.png";
        /// <summary>GOLD small, 9-sliced horizontally (border 25/0/25/0) — the selected duration chip.</summary>
        private const string SpriteButtonGoldSmall   = ArtDir + "ButtonLevelUpLong.png";
        /// <summary>SILVER big, already 9-sliced (border 25) in the shipped import — CANCEL.</summary>
        private const string SpriteButtonSilverBig   = ArtDir + "ButtonCancel.png";
        /// <summary>GOLD big, 9-sliced (border 25 set by this task) — LEND / RETURN.</summary>
        private const string SpriteButtonGoldBig     = ArtDir + "ButtonConfirm.png";
        /// <summary>The Figma "Pop-up" panel: #133453→#091B33 gradient + 3 px white-to-silver
        /// stroke + drop shadow, 9-sliced border 64 (UI_ELEMENT_PALETTE § Panels).</summary>
        private const string SpritePopupPanel = "Assets/Art/HomeScreen/Next Hole Panel.png";

        private const string FontSemiBold = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        private const string FontRegular  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        private const string ModalPrefabPath  = "Assets/Prefabs/UI/Modals/LoanModal.prefab";
        private const string ReturnPrefabPath = "Assets/Prefabs/UI/Modals/LoanReturnModal.prefab";
        private const string RowPrefabPath    = "Assets/Prefabs/UI/Loans/LoanRecipientRow.prefab";

        private const string RosterDetail = "Canvas/ScreensRoot/RosterScreen/DetailPanel";
        private const string ClubDetail   =
            "Canvas/ScreensRoot/InventoryScreen/ContentArea/ClubsContent/ClubsMainSection/ClubDetailPanel";

        private static readonly StringBuilder Log = new StringBuilder();

        // ── colours, from the Figma fidelity table ────────────────────────────
        private static readonly Color RibbonFill = Hex("050F1F", 0.72f);
        private static readonly Color DimFill    = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color RowFill    = Hex("050F1F", 0.60f);
        private static readonly Color Scrim      = new Color(0f, 0f, 0f, 0.50f);
        private static readonly Color Amber      = Hex("FFB847", 1f);
        private static readonly Color SubText    = Hex("BFD1E6", 1f);
        private static readonly Color ReturnGold = Hex("ED6B21", 1f);
        private static readonly Color AvatarFill = Hex("38597F", 1f);

        [MenuItem("GOLFIN/Loans/Build Loan UI")]
        public static void Build()
        {
            Log.Clear();

            try
            {
                BuildRowPrefab();
                BuildModalPrefab();
                BuildReturnPrefab();

                BuildRosterDetail();
                BuildClubDetail();

                BuildCardBadge("Assets/Prefabs/UI/Roster/CharacterThumbnailCard.prefab", isClub: false);
                BuildCardBadge("Assets/Prefabs/UI/Roster/CharacterThumbnailCardGlowUp.prefab", isClub: false);
                BuildCardBadge("Assets/Prefabs/UI/Inventory/ClubThumbnailCard.prefab", isClub: true);

                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                Debug.Log("[LoanUiBuilder] DONE\n" + Log);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[LoanUiBuilder] FAILED: {e.Message}\n{Log}\n{e.StackTrace}");
                throw;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // helpers
        // ═════════════════════════════════════════════════════════════════════

        private static Color Hex(string rgb, float a)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            c.a = a;
            return c;
        }

        /// <summary>Load a reused atom, or STOP. Rule 19: a source that cannot be found is
        /// surfaced, never silently replaced by a flat colour.</summary>
        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException(
                    $"REUSE SOURCE MISSING: {path} ({typeof(T).Name}). Nothing is built from scratch — fix the path.");
            Log.Append("  reuse ").Append(typeof(T).Name).Append(' ').Append(path)
               .Append(" guid=").Append(AssetDatabase.AssetPathToGUID(path)).Append('\n');
            return asset;
        }

        private static GameObject Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static RectTransform Rect(GameObject go) => (RectTransform)go.transform;

        private static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot,
                                    Vector2 pos, Vector2 size)
        {
            RectTransform rt = Rect(go);
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
        }

        private static Image AddImage(GameObject go, Sprite? sprite, Color color,
                                      Image.Type type = Image.Type.Simple, bool raycast = true)
        {
            Image img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            img.raycastTarget = raycast;
            return img;
        }

        private static TextMeshProUGUI AddText(GameObject go, string fontPath, float size,
                                               Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.font = Require<TMP_FontAsset>(fontPath);
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        /// <summary>
        /// A button in the shipped family. Rule 11: EVERY new player-facing Button gets
        /// <see cref="ButtonPressFeedback"/> in the same operation that adds the Button.
        /// </summary>
        private static Button MakeButton(GameObject go, Sprite sprite, Image.Type type,
                                         string label, float fontSize)
        {
            AddImage(go, sprite, Color.white, type);
            Button b = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            if (go.GetComponent<ButtonPressFeedback>() == null) go.AddComponent<ButtonPressFeedback>();

            GameObject text = Child(go.transform, "Text (TMP)");
            SetRect(text, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI tmp = AddText(text, FontSemiBold, fontSize, Hex("1E293B", 1f),
                                          TextAlignmentOptions.Center);
            tmp.text = label;
            tmp.fontStyle = FontStyles.UpperCase;
            tmp.enableWordWrapping = false;
            return b;
        }

        private static void Wire(Object target, string field, Object? value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                throw new System.InvalidOperationException(
                    $"{target.GetType().Name} has no serialized field '{field}'");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (PrefabUtility.IsPartOfPrefabInstance(target))
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            EditorUtility.SetDirty(target);
        }

        // ═════════════════════════════════════════════════════════════════════
        // the recipient row prefab
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildRowPrefab()
        {
            Log.Append("== LoanRecipientRow.prefab ==\n");
            System.IO.Directory.CreateDirectory("Assets/Prefabs/UI/Loans");

            var root = new GameObject("LoanRecipientRow", typeof(RectTransform));
            SetRect(root, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    Vector2.zero, new Vector2(732, 96));

            Image bg = AddImage(root, Require<Sprite>(SpriteRow), RowFill);
            bg.type = Image.Type.Simple;

            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 24, 0, 0);
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;

            GameObject avatar = Child(root.transform, "Avatar");
            AddImage(avatar, Require<Sprite>(SpriteBadge), AvatarFill, Image.Type.Simple, raycast: false);
            var avatarLe = avatar.GetComponent<LayoutElement>() ?? avatar.AddComponent<LayoutElement>();
            avatarLe.preferredWidth = 64;
            avatarLe.preferredHeight = 64;
            avatarLe.flexibleWidth = 0;

            GameObject nameGo = Child(root.transform, "Name");
            TextMeshProUGUI nameTmp = AddText(nameGo, FontSemiBold, 33, Color.white,
                                              TextAlignmentOptions.MidlineLeft);
            nameTmp.enableWordWrapping = false;
            nameTmp.overflowMode = TextOverflowModes.Ellipsis;
            var nameLe = nameGo.GetComponent<LayoutElement>() ?? nameGo.AddComponent<LayoutElement>();
            nameLe.flexibleWidth = 1;      // FILL, per the node

            GameObject levelGo = Child(root.transform, "Level");
            TextMeshProUGUI levelTmp = AddText(levelGo, FontRegular, 30, SubText,
                                               TextAlignmentOptions.MidlineRight);
            levelTmp.enableWordWrapping = false;
            var levelLe = levelGo.GetComponent<LayoutElement>() ?? levelGo.AddComponent<LayoutElement>();
            levelLe.flexibleWidth = 0;
            levelLe.preferredWidth = 120;

            var button = root.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.None;   // selection is a sprite swap
            root.AddComponent<ButtonPressFeedback>();

            var row = root.AddComponent<LoanRecipientRow>();
            Wire(row, "background", bg);
            Wire(row, "avatar", avatar.GetComponent<Image>());
            Wire(row, "nameText", nameTmp);
            Wire(row, "levelText", levelTmp);
            Wire(row, "button", button);
            Wire(row, "unselectedSprite", Require<Sprite>(SpriteRow));
            Wire(row, "selectedSprite", Require<Sprite>(SpriteRowSelected));

            PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
            Object.DestroyImmediate(root);
            Log.Append("  saved ").Append(RowPrefabPath).Append('\n');
        }

        // ═════════════════════════════════════════════════════════════════════
        // the lend modal prefab
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildModalPrefab()
        {
            Log.Append("== LoanModal.prefab ==\n");

            var root = new GameObject("LoanModal", typeof(RectTransform));
            SetRect(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // ⚠️ AUTHORED INACTIVE (memory: reference_modal_children_author_inactive).
            // ModalController.Awake force-deactivates both anyway; authoring them ACTIVE throws a
            // UIParticle.OnDisable MissingReferenceException on every play-mode entry.
            GameObject backdrop = Child(root.transform, "Backdrop");
            SetRect(backdrop, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(backdrop, null, Scrim);
            backdrop.SetActive(false);

            GameObject panel = Child(root.transform, "ModalPanel");
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(780, 1080));
            Image panelImg = AddImage(panel, Require<Sprite>(SpritePopupPanel), Color.white, Image.Type.Sliced);
            // border 64 sprite-px ÷ ppum 3.2 = 20 UI px, the node's radius.
            panelImg.pixelsPerUnitMultiplier = 3.2f;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 24;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;   // HUG height, per the node

            TextMeshProUGUI title = Row(panel, "Title", FontSemiBold, 45, Color.white,
                                        TextAlignmentOptions.TopLeft, 732, 60);

            GameObject durationLabelGo = Child(panel.transform, "DurationLabel");
            TextMeshProUGUI durationLabel = AddText(durationLabelGo, FontSemiBold, 39, Color.white,
                                                    TextAlignmentOptions.MidlineLeft);
            Fixed(durationLabelGo, 732, 50);

            GameObject durationRow = Child(panel.transform, "DurationRow");
            Fixed(durationRow, 732, 56);
            var drl = durationRow.GetComponent<HorizontalLayoutGroup>() ?? durationRow.AddComponent<HorizontalLayoutGroup>();
            drl.spacing = 21;
            drl.childAlignment = TextAnchor.MiddleLeft;
            drl.childForceExpandWidth = false;
            drl.childForceExpandHeight = false;
            drl.childControlWidth = false;
            drl.childControlHeight = false;

            Sprite silverSmall = Require<Sprite>(SpriteButtonSilverSmall);
            Sprite goldSmall   = Require<Sprite>(SpriteButtonGoldSmall);

            Button d1 = DurationChip(durationRow, "Days1", silverSmall);
            Button d3 = DurationChip(durationRow, "Days3", silverSmall);
            Button d7 = DurationChip(durationRow, "Days7", silverSmall);

            TextMeshProUGUI terms = Row(panel, "Terms", FontRegular, 30, Color.white,
                                        TextAlignmentOptions.TopLeft, 732, 96);

            GameObject warningGo = Child(panel.transform, "EquippedWarning");
            TextMeshProUGUI warning = AddText(warningGo, FontSemiBold, 30, Amber,
                                              TextAlignmentOptions.TopLeft);
            Fixed(warningGo, 732, 44);
            warningGo.SetActive(false);

            GameObject lendToGo = Child(panel.transform, "LendToLabel");
            TextMeshProUGUI lendTo = AddText(lendToGo, FontSemiBold, 39, Color.white,
                                             TextAlignmentOptions.MidlineLeft);
            Fixed(lendToGo, 732, 50);

            // The list: four rows at 96 + three 16 gaps = 432, and it scrolls beyond that.
            GameObject scrollGo = Child(panel.transform, "RecipientScroll");
            Fixed(scrollGo, 732, 432);
            var scroll = scrollGo.GetComponent<ScrollRect>() ?? scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            // ONE SCROLL FEEL (game_polish §C2). `ScrollFeelTests` walks every prefab in the
            // project and fails any ScrollRect off the reference, so a new one authored with
            // Unity's defaults breaks a suite the moment it is saved — which is exactly what this
            // one did. The values are READ FROM THE BUILDER that owns them rather than retyped, so
            // they cannot drift apart.
            scroll.movementType     = ScrollRect.MovementType.Elastic;
            scroll.elasticity       = Golfin.UI.Polish.EditorTools.GamePolishBuilder.ScrollElasticity;
            scroll.inertia          = true;
            scroll.decelerationRate = Golfin.UI.Polish.EditorTools.GamePolishBuilder.ScrollDeceleration;
            scroll.scrollSensitivity = Golfin.UI.Polish.EditorTools.GamePolishBuilder.ScrollSensitivity;
            var mask = scrollGo.GetComponent<RectMask2D>() ?? scrollGo.AddComponent<RectMask2D>();

            GameObject content = Child(scrollGo.transform, "Content");
            SetRect(content, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                    Vector2.zero, new Vector2(0, 0));
            var clg = content.GetComponent<VerticalLayoutGroup>() ?? content.AddComponent<VerticalLayoutGroup>();
            clg.spacing = 16;
            clg.childAlignment = TextAnchor.UpperCenter;
            clg.childForceExpandWidth = false;
            clg.childForceExpandHeight = false;
            clg.childControlWidth = true;
            clg.childControlHeight = false;
            var cfit = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
            cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = Rect(content);
            scroll.viewport = Rect(scrollGo);

            GameObject emptyGo = Child(panel.transform, "EmptyState");
            TextMeshProUGUI empty = AddText(emptyGo, FontRegular, 30, SubText,
                                            TextAlignmentOptions.Center);
            Fixed(emptyGo, 732, 96);
            emptyGo.SetActive(false);

            GameObject footer = Child(panel.transform, "Footer");
            Fixed(footer, 732, 120);
            var fl = footer.GetComponent<HorizontalLayoutGroup>() ?? footer.AddComponent<HorizontalLayoutGroup>();
            fl.spacing = 24;
            fl.childAlignment = TextAnchor.MiddleCenter;
            fl.childForceExpandWidth = false;
            fl.childForceExpandHeight = false;
            fl.childControlWidth = false;
            fl.childControlHeight = false;

            GameObject cancelGo = Child(footer.transform, "CancelButton");
            SetRect(cancelGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(354, 120));
            Button cancel = MakeButton(cancelGo, Require<Sprite>(SpriteButtonSilverBig),
                                       Image.Type.Sliced, "CANCEL", 39);

            GameObject confirmGo = Child(footer.transform, "LendButton");
            SetRect(confirmGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(354, 120));
            Button confirm = MakeButton(confirmGo, Require<Sprite>(SpriteButtonGoldBig),
                                        Image.Type.Sliced, "LEND", 39);

            GameObject spinner = Child(confirmGo.transform, "Spinner");
            SetRect(spinner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(40, 40));
            AddImage(spinner, null, new Color(1, 1, 1, 0.6f), Image.Type.Simple, raycast: false);
            spinner.SetActive(false);

            panel.SetActive(false);

            var controller = root.AddComponent<LoanModalController>();
            Wire(controller, "modalPanel", panel);
            Wire(controller, "backdrop", backdrop);
            Wire(controller, "titleText", title);
            Wire(controller, "durationLabel", durationLabel);
            Wire(controller, "days1Button", d1);
            Wire(controller, "days3Button", d3);
            Wire(controller, "days7Button", d7);
            Wire(controller, "days1Text", d1.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(controller, "days3Text", d3.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(controller, "days7Text", d7.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(controller, "durationUnselectedSprite", silverSmall);
            Wire(controller, "durationSelectedSprite", goldSmall);
            Wire(controller, "termsText", terms);
            Wire(controller, "equippedWarningRoot", warningGo);
            Wire(controller, "equippedWarningText", warning);
            Wire(controller, "lendToLabel", lendTo);
            Wire(controller, "recipientParent", content.transform);
            Wire(controller, "recipientRowPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath));
            Wire(controller, "emptyStateRoot", emptyGo);
            Wire(controller, "emptyStateText", empty);
            Wire(controller, "recipientScroll", scroll);
            Wire(controller, "cancelButton", cancel);
            Wire(controller, "confirmButton", confirm);
            Wire(controller, "confirmText", confirm.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(controller, "confirmSpinner", spinner);

            PrefabUtility.SaveAsPrefabAsset(root, ModalPrefabPath);
            Object.DestroyImmediate(root);
            Log.Append("  saved ").Append(ModalPrefabPath).Append('\n');
        }

        private static Button DurationChip(GameObject parent, string name, Sprite sprite)
        {
            GameObject go = Child(parent.transform, name);
            SetRect(go, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(230, 56));
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth = 230;
            le.preferredHeight = 56;
            // Sliced: the gold variant is 554 wide natively and the silver 235, and BOTH have to
            // render at 230 without their corner radius changing shape (C-corner distortion).
            return MakeButton(go, sprite, Image.Type.Sliced, "3 DAYS", 33);
        }

        private static TextMeshProUGUI Row(GameObject parent, string name, string font, float size,
                                           Color color, TextAlignmentOptions align, float w, float h)
        {
            GameObject go = Child(parent.transform, name);
            TextMeshProUGUI tmp = AddText(go, font, size, color, align);
            Fixed(go, w, h);
            return tmp;
        }

        /// <summary>
        /// Pin a size the parent layout group cannot argue with.
        ///
        /// <para>PIPELINE_HARDENING C3: a VerticalLayoutGroup with childControlHeight sizes its
        /// children from THEIR preferred size, and a bare RectTransform's is whatever the last
        /// thing to touch it left behind. A LayoutElement is the only thing that pins it.</para>
        /// </summary>
        private static void Fixed(GameObject go, float w, float h)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
            le.minHeight = h;
            Rect(go).sizeDelta = new Vector2(w, h);
        }

        // ═════════════════════════════════════════════════════════════════════
        // the return confirm prefab
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildReturnPrefab()
        {
            Log.Append("== LoanReturnModal.prefab ==\n");

            var root = new GameObject("LoanReturnModal", typeof(RectTransform));
            SetRect(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject backdrop = Child(root.transform, "Backdrop");
            SetRect(backdrop, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(backdrop, null, Scrim);
            backdrop.SetActive(false);

            GameObject panel = Child(root.transform, "ModalPanel");
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(810, 520));
            Image panelImg = AddImage(panel, Require<Sprite>(SpritePopupPanel), Color.white, Image.Type.Sliced);
            panelImg.pixelsPerUnitMultiplier = 3.2f;

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(48, 48, 40, 32);
            vlg.spacing = 32;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = Row(panel, "Title", FontSemiBold, 45, ReturnGold,
                                        TextAlignmentOptions.Center, 714, 60);
            TextMeshProUGUI body = Row(panel, "Body", FontRegular, 33, Color.white,
                                       TextAlignmentOptions.Center, 714, 160);

            GameObject footer = Child(panel.transform, "Footer");
            Fixed(footer, 714, 120);
            var fl = footer.GetComponent<HorizontalLayoutGroup>() ?? footer.AddComponent<HorizontalLayoutGroup>();
            fl.spacing = 24;
            fl.childAlignment = TextAnchor.MiddleCenter;
            fl.childForceExpandWidth = false;
            fl.childForceExpandHeight = false;
            fl.childControlWidth = false;
            fl.childControlHeight = false;

            GameObject cancelGo = Child(footer.transform, "CancelButton");
            SetRect(cancelGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(345, 120));
            Button cancel = MakeButton(cancelGo, Require<Sprite>(SpriteButtonSilverBig),
                                       Image.Type.Sliced, "CANCEL", 39);

            GameObject returnGo = Child(footer.transform, "ReturnButton");
            SetRect(returnGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(345, 120));
            // GOLD, not copper (Cesar, 2026-09-09).
            Button ret = MakeButton(returnGo, Require<Sprite>(SpriteButtonGoldBig),
                                    Image.Type.Sliced, "RETURN", 39);

            GameObject spinner = Child(returnGo.transform, "Spinner");
            SetRect(spinner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(40, 40));
            AddImage(spinner, null, new Color(1, 1, 1, 0.6f), Image.Type.Simple, raycast: false);
            spinner.SetActive(false);

            panel.SetActive(false);

            var controller = root.AddComponent<LoanReturnModalController>();
            Wire(controller, "modalPanel", panel);
            Wire(controller, "backdrop", backdrop);
            Wire(controller, "titleText", title);
            Wire(controller, "bodyText", body);
            Wire(controller, "cancelButton", cancel);
            Wire(controller, "returnButton", ret);
            Wire(controller, "returnButtonText", ret.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(controller, "spinner", spinner);

            PrefabUtility.SaveAsPrefabAsset(root, ReturnPrefabPath);
            Object.DestroyImmediate(root);
            Log.Append("  saved ").Append(ReturnPrefabPath).Append('\n');
        }

        // ═════════════════════════════════════════════════════════════════════
        // the two detail panels
        // ═════════════════════════════════════════════════════════════════════

        private static GameObject Find(string path)
        {
            var go = GameObject.Find(path);
            if (go == null)
                throw new System.InvalidOperationException($"Scene object not found: {path}");
            return go;
        }

        /// <summary>
        /// The ribbon + dim, built over a detail panel's Left panel.
        ///
        /// <para>
        /// SIZED AND POSITIONED FROM THE PORTRAIT, not from LeftPanel: the Figma Left panel is
        /// 537 wide and the scene's LeftPanel is 541, because the portrait Image inside it is the
        /// 537 the design measures. Anchoring to the portrait's own rect is what makes the ribbon
        /// flush with the artwork instead of 2 px proud of it on each side.
        /// </para>
        /// </summary>
        private static LoanRibbonView BuildRibbon(GameObject leftPanel, RectTransform anchorPanel,
                                                  float width)
        {
            var parent = (RectTransform)leftPanel.transform;

            // The anchor panel's centre and height, expressed in the PARENT's local space. Read
            // from world corners rather than from anchoredPosition because the two panels are
            // anchored completely differently (the Roster's portrait is a sibling at (0,0.5); the
            // club's Left panel IS the parent), and corners are the one description that is true
            // in both cases.
            var corners = new Vector3[4];
            anchorPanel.GetWorldCorners(corners);       // 0 BL, 1 TL, 2 TR, 3 BR
            Vector3 bl = parent.InverseTransformPoint(corners[0]);
            Vector3 tr = parent.InverseTransformPoint(corners[2]);
            float centreX = (bl.x + tr.x) * 0.5f;
            float centreY = (bl.y + tr.y) * 0.5f;
            float height  = tr.y - bl.y;
            float topY    = tr.y;

            var mid = new Vector2(0.5f, 0.5f);
            float parentCentreX = parent.rect.center.x;
            float parentCentreY = parent.rect.center.y;

            // ⚠️ THE CLUB PANEL'S LeftPanel IS A VerticalLayoutGroup (the Roster's is not).
            //
            // Without this, the group treats the ribbon and the dim as two more items in its
            // vertical flow: it repositions them under the club artwork and squashes ClubImage and
            // InfoSection to make room. The first capture run showed exactly that — the club's
            // buttons went to their lent state while the portrait stayed undimmed and no ribbon
            // appeared anywhere, because both had been laid out somewhere else.
            //
            // `ignoreLayout` is set on BOTH panels rather than conditionally: it is inert where
            // there is no group, and a conditional would silently stop protecting the club panel
            // the day the Roster grows one.
            GameObject dim = Child(leftPanel.transform, "LentDim");
            SetRect(dim, mid, mid, mid,
                    new Vector2(centreX - parentCentreX, centreY - parentCentreY),
                    new Vector2(width, height));
            AddImage(dim, null, DimFill, Image.Type.Simple, raycast: false);
            IgnoreLayout(dim);
            dim.SetActive(false);

            GameObject ribbon = Child(leftPanel.transform, "LoanRibbon");
            SetRect(ribbon, mid, mid, mid,
                    new Vector2(centreX - parentCentreX, (topY - 36f) - parentCentreY),
                    new Vector2(width, 72f));
            AddImage(ribbon, Require<Sprite>(SpriteRibbon), RibbonFill, Image.Type.Simple, raycast: false);
            IgnoreLayout(ribbon);

            var hl = ribbon.GetComponent<HorizontalLayoutGroup>() ?? ribbon.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(24, 24, 0, 0);
            hl.spacing = 16;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;

            GameObject icon = Child(ribbon.transform, "Icon");
            AddImage(icon, Require<Sprite>(SpriteIconOutBig), Color.white, Image.Type.Simple, raycast: false);
            var iconLe = icon.GetComponent<LayoutElement>() ?? icon.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 40;
            iconLe.preferredHeight = 40;
            iconLe.flexibleWidth = 0;

            GameObject label = Child(ribbon.transform, "Label");
            TextMeshProUGUI tmp = AddText(label, FontSemiBold, 28, Color.white,
                                          TextAlignmentOptions.MidlineLeft);
            tmp.enableWordWrapping = false;
            tmp.enableAutoSizing = true;             // ConstrainName-style shrink, one line
            tmp.fontSizeMin = 20;
            tmp.fontSizeMax = 28;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            var labelLe = label.GetComponent<LayoutElement>() ?? label.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1;

            // Ribbon must be the LAST sibling of the two so it draws OVER the dim.
            dim.transform.SetAsLastSibling();
            ribbon.transform.SetAsLastSibling();

            var view = ribbon.GetComponent<LoanRibbonView>() ?? ribbon.AddComponent<LoanRibbonView>();
            Wire(view, "ribbonRoot", ribbon);
            Wire(view, "ribbonIcon", icon.GetComponent<Image>());
            Wire(view, "ribbonLabel", tmp);
            Wire(view, "iconLoanOutBig", Require<Sprite>(SpriteIconOutBig));
            Wire(view, "iconLoanInBig", Require<Sprite>(SpriteIconInBig));
            Wire(view, "lentDim", dim);

            ribbon.SetActive(false);
            Log.Append("  ribbon ").Append(width.ToString("0")).Append("x72 at top of ")
               .Append(anchorPanel.name).Append("; dim ").Append(width.ToString("0")).Append('x')
               .Append(height.ToString("0")).Append('\n');
            return view;
        }

        /// <summary>
        /// Turn a single full-width Compare button into the COMPARE + LEND row.
        ///
        /// <para>
        /// THE EDGES ARE COPIED, NOT COMPUTED. The acceptance test is "Compare's left edge = the
        /// LEVEL UP button's left edge and Lend's right edge = BOOST's right edge, to the pixel",
        /// so the two reference buttons' corners are converted into the Compare button's own
        /// parent space and used verbatim. Deriving from widths and gaps would be right until one
        /// of the four numbers moved.
        /// </para>
        /// </summary>
        private static Button BuildLendRow(GameObject compareButton, RectTransform left,
                                           RectTransform right, string label)
        {
            RectTransform cmp = Rect(compareButton);
            var parent = (RectTransform)cmp.parent;

            float leftEdge  = EdgeX(left,  parent, 0f);   // left button's LEFT edge
            float rightEdge = EdgeX(right, parent, 1f);   // right button's RIGHT edge
            float y = cmp.anchoredPosition.y;
            float h = cmp.rect.height;
            float w = left.rect.width;                    // 235 — the row above's button width

            // Compare, resized in place.
            cmp.sizeDelta = new Vector2(w, h);
            cmp.anchoredPosition = new Vector2(LocalToAnchored(cmp, leftEdge + w * cmp.pivot.x), y);
            var cmpImg = compareButton.GetComponent<Image>();
            if (cmpImg != null)
            {
                // The 496-wide ButtonCompare sprite squeezed to 235 would distort its corner
                // radius; the row above's native 235x56 silver sprite is the same button family at
                // the right size, so no stretch happens at all.
                cmpImg.sprite = Require<Sprite>(SpriteButtonSilverSmall);
                cmpImg.type = Image.Type.Simple;
            }
            if (compareButton.GetComponent<ButtonPressFeedback>() == null)
                compareButton.AddComponent<ButtonPressFeedback>();

            // Lend, cloned from Compare so it inherits every component and style it has.
            Transform existing = cmp.parent.Find("LendButton");
            GameObject lendGo = existing != null
                ? existing.gameObject
                : Object.Instantiate(compareButton, cmp.parent);
            lendGo.name = "LendButton";

            RectTransform lend = Rect(lendGo);
            lend.anchorMin = cmp.anchorMin;
            lend.anchorMax = cmp.anchorMax;
            lend.pivot     = cmp.pivot;
            lend.sizeDelta = new Vector2(right.rect.width, h);
            lend.anchoredPosition =
                new Vector2(LocalToAnchored(lend, rightEdge - right.rect.width * (1f - lend.pivot.x)), y);
            lend.localScale = Vector3.one;

            Button lendButton = lendGo.GetComponent<Button>();
            var lendTmp = lendGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lendTmp != null) lendTmp.text = label;

            StripPersistentClicks(lendButton);

            lendGo.SetActive(true);
            Log.Append("  row: compare.left=").Append(leftEdge.ToString("0.##"))
               .Append(" lend.right=").Append(rightEdge.ToString("0.##"))
               .Append(" w=").Append(w.ToString("0.##")).Append('\n');
            return lendButton!;
        }

        /// <summary>Take an overlay OUT of its parent's layout flow. See BuildRibbon.</summary>
        private static void IgnoreLayout(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        /// <summary>The x of one edge of <paramref name="src"/>, expressed in
        /// <paramref name="space"/>'s local coordinates. <paramref name="t"/> 0 = left, 1 = right.</summary>
        private static float EdgeX(RectTransform src, RectTransform space, float t)
        {
            var corners = new Vector3[4];
            src.GetWorldCorners(corners);           // 0 BL, 1 TL, 2 TR, 3 BR
            Vector3 world = t <= 0f ? corners[0] : corners[3];
            return space.InverseTransformPoint(world).x;
        }

        /// <summary>Convert a LOCAL x (in the rect's parent space) into the anchoredPosition.x that
        /// puts the rect's pivot there — i.e. subtract the anchor reference point.</summary>
        private static float LocalToAnchored(RectTransform rt, float localX)
        {
            var parent = (RectTransform)rt.parent;
            float parentWidth = parent.rect.width;
            float anchorRefX = parent.rect.xMin + parentWidth * (rt.anchorMin.x + rt.anchorMax.x) * 0.5f;
            return localX - anchorRefX;
        }

        private static void BuildRosterDetail()
        {
            Log.Append("== Roster DetailPanel ==\n");
            GameObject detail = Find(RosterDetail);
            GameObject leftPanel = detail.transform.Find("LeftPanel").gameObject;
            // The Figma Left panel is 537x1483 — which is the PORTRAIT Image, not the 541-wide
            // LeftPanel that holds it. Anchoring to the artwork is what makes the ribbon flush.
            var portrait = (RectTransform)leftPanel.transform.Find("Character");
            LoanRibbonView ribbon = BuildRibbon(leftPanel, portrait, portrait.rect.width);

            Transform rightPanel = detail.transform.Find("RightPanel");
            var levelUp = (RectTransform)rightPanel.Find("ButtonsPanel/LevelUpButton");
            var boost   = (RectTransform)rightPanel.Find("ButtonsPanel/BoostButton");
            GameObject compare = rightPanel.Find("CompareButton").gameObject;

            Button lend = BuildLendRow(compare, levelUp, boost, "LEND");

            GameObject modal  = SpawnModal(detail.transform.parent, ModalPrefabPath, "LoanModal");
            GameObject retMod = SpawnModal(detail.transform.parent, ReturnPrefabPath, "LoanReturnModal");

            var panel = detail.GetComponent<Golfin.Roster.CharacterDetailPanel>();
            Wire(panel, "lendButton", lend);
            Wire(panel, "lendButtonText", lend.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(panel, "loanRibbon", ribbon);
            Wire(panel, "loanModal", modal.GetComponent<LoanModalController>());
            Wire(panel, "loanReturnModal", retMod.GetComponent<LoanReturnModalController>());
        }

        private static void BuildClubDetail()
        {
            Log.Append("== Club DetailPanel ==\n");
            GameObject detail = Find(ClubDetail);
            var leftPanel = (RectTransform)detail.transform.Find("LeftPanel");
            if (leftPanel == null)
                throw new System.InvalidOperationException("ClubDetailPanel/LeftPanel not found");

            // The club artwork (537 wide) OVERFLOWS its 483-wide LeftPanel, exactly as the Figma
            // node does. So the ribbon takes the ARTWORK's width and the PANEL's top edge and
            // height — width from the thing it sits on, extent from the thing it covers.
            var clubImage = (RectTransform)leftPanel.Find("ClubImage");
            float ribbonWidth = clubImage != null ? clubImage.rect.width : leftPanel.rect.width;
            LoanRibbonView ribbon = BuildRibbon(leftPanel.gameObject, leftPanel, ribbonWidth);

            Transform rightPanel = detail.transform.Find("RightPanel");
            RectTransform? buttonsPanel = FindDeep(rightPanel, "ButtonsPanel ") ?? FindDeep(rightPanel, "ButtonsPanel");
            RectTransform? compare = FindDeep(rightPanel, "CompareButton");
            if (buttonsPanel == null || compare == null)
                throw new System.InvalidOperationException(
                    $"Club RightPanel missing ButtonsPanel/CompareButton (buttons={buttonsPanel != null} compare={compare != null})");

            Button lend = BuildLendRowInLayout(compare.gameObject, buttonsPanel, "LEND");

            Transform screen = detail.transform.root.Find("ScreensRoot/InventoryScreen")
                               ?? detail.transform.parent;
            GameObject modal  = SpawnModal(screen, ModalPrefabPath, "LoanModal");
            GameObject retMod = SpawnModal(screen, ReturnPrefabPath, "LoanReturnModal");

            var panel = detail.GetComponent<Golfin.Inventory.ClubDetailPanel>();
            Wire(panel, "lendButton", lend);
            Wire(panel, "lendButtonText", lend.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(panel, "loanRibbon", ribbon);
            Wire(panel, "loanModal", modal.GetComponent<LoanModalController>());
            Wire(panel, "loanReturnModal", retMod.GetComponent<LoanReturnModalController>());
        }

        /// <summary>
        /// The club panel's variant of <see cref="BuildLendRow"/>.
        ///
        /// <para>
        /// THE TWO PANELS ARE NOT BUILT THE SAME WAY, and the difference is load-bearing. The
        /// Roster's RightPanel positions its children ABSOLUTELY (the Compare button sits at
        /// anchoredPosition (268.2, 228)); the club's RightPanel is a VerticalLayoutGroup, where
        /// every child's anchoredPosition is (0,0) and the group decides. Setting a position there
        /// would be overwritten on the next layout pass — so instead the Compare button is MOVED
        /// INTO a row container that takes its slot, and a HorizontalLayoutGroup produces the
        /// 235 + 16 + 235 the LEVEL UP / REPAIR row above already produces.
        /// </para>
        /// <para>
        /// The row's width and height are copied from that ButtonsPanel, which is what makes the
        /// acceptance test ("left edge = LEVEL UP's, right edge = REPAIR's") true by construction
        /// rather than by arithmetic.
        /// </para>
        /// </summary>
        private static Button BuildLendRowInLayout(GameObject compareButton, RectTransform buttonsPanel,
                                                   string label)
        {
            RectTransform cmp = Rect(compareButton);

            // ⚠️ IDEMPOTENCE: on a SECOND run the Compare button is already inside the row this
            // method built last time, so `cmp.parent` is the ROW, not the RightPanel. Taking the
            // parent naively would nest a second CompareLendRow inside the first — which is
            // exactly what happened on the first attempt at this. The row's home is always the
            // ButtonsPanel's parent; anchor off that instead.
            var parent = (RectTransform)buttonsPanel.parent;
            int slot = cmp.parent == parent ? cmp.GetSiblingIndex() : buttonsPanel.GetSiblingIndex() + 1;

            // The row is a CARBON COPY of the LEVEL UP / REPAIR panel above it: same rect, same
            // HorizontalLayoutGroup settings, same child sizes, no LayoutElement on any of the
            // three. That is what makes "Compare's left edge = LEVEL UP's, Lend's right edge =
            // REPAIR's" true BY CONSTRUCTION — two identical layout groups over identical children
            // in the same parent cannot place their contents differently. Deriving the positions
            // arithmetically instead would be right only until one of the numbers moved, and would
            // be wrong TODAY, because the panel is 485.7 wide and its content is 486.
            var srcHl = buttonsPanel.GetComponent<HorizontalLayoutGroup>();
            var refButton = (RectTransform)buttonsPanel.GetChild(0);
            Vector2 buttonSize = refButton.sizeDelta;

            Transform existingRow = parent.Find("CompareLendRow");
            GameObject row = existingRow != null ? existingRow.gameObject : Child(parent, "CompareLendRow");
            if (existingRow == null) row.transform.SetSiblingIndex(slot);

            RectTransform rowRt = Rect(row);
            rowRt.anchorMin = buttonsPanel.anchorMin;
            rowRt.anchorMax = buttonsPanel.anchorMax;
            rowRt.pivot     = buttonsPanel.pivot;
            rowRt.anchoredPosition = buttonsPanel.anchoredPosition;
            rowRt.sizeDelta = buttonsPanel.sizeDelta;
            rowRt.localScale = Vector3.one;

            // ButtonsPanel carries no LayoutElement, so neither may the row — one that pinned a
            // width the source does not pin is exactly how the two rows would drift apart.
            var strayRowLe = row.GetComponent<LayoutElement>();
            if (strayRowLe != null) Object.DestroyImmediate(strayRowLe, true);

            var hl = row.GetComponent<HorizontalLayoutGroup>() ?? row.AddComponent<HorizontalLayoutGroup>();
            if (srcHl != null)
            {
                hl.padding = new RectOffset(srcHl.padding.left, srcHl.padding.right,
                                            srcHl.padding.top, srcHl.padding.bottom);
                hl.spacing = srcHl.spacing;
                hl.childAlignment = srcHl.childAlignment;
                hl.childControlWidth = srcHl.childControlWidth;
                hl.childControlHeight = srcHl.childControlHeight;
                hl.childForceExpandWidth = srcHl.childForceExpandWidth;
                hl.childForceExpandHeight = srcHl.childForceExpandHeight;
            }

            // Move Compare in and size it like the buttons above. Its 496-wide sprite would
            // distort at 235, so it takes the row-above's native 235x56 silver sprite.
            cmp.SetParent(row.transform, worldPositionStays: false);
            cmp.anchorMin = refButton.anchorMin;
            cmp.anchorMax = refButton.anchorMax;
            cmp.pivot     = refButton.pivot;
            cmp.sizeDelta = buttonSize;
            cmp.localScale = Vector3.one;
            var strayCmpLe = compareButton.GetComponent<LayoutElement>();
            if (strayCmpLe != null) Object.DestroyImmediate(strayCmpLe, true);

            var cmpImg = compareButton.GetComponent<Image>();
            if (cmpImg != null)
            {
                cmpImg.sprite = Require<Sprite>(SpriteButtonSilverSmall);
                cmpImg.type = Image.Type.Simple;
            }
            if (compareButton.GetComponent<ButtonPressFeedback>() == null)
                compareButton.AddComponent<ButtonPressFeedback>();
            cmp.SetSiblingIndex(0);

            Transform existingLend = row.transform.Find("LendButton");
            GameObject lendGo = existingLend != null
                ? existingLend.gameObject
                : Object.Instantiate(compareButton, row.transform);
            lendGo.name = "LendButton";

            RectTransform lend = Rect(lendGo);
            lend.anchorMin = refButton.anchorMin;
            lend.anchorMax = refButton.anchorMax;
            lend.pivot     = refButton.pivot;
            lend.sizeDelta = buttonSize;
            lend.localScale = Vector3.one;
            var strayLendLe = lendGo.GetComponent<LayoutElement>();
            if (strayLendLe != null) Object.DestroyImmediate(strayLendLe, true);
            lend.SetSiblingIndex(1);

            Button lendButton = lendGo.GetComponent<Button>();
            var lendTmp = lendGo.GetComponentInChildren<TextMeshProUGUI>(true);
            if (lendTmp != null) lendTmp.text = label;
            StripPersistentClicks(lendButton);
            lendGo.SetActive(true);

            Log.Append("  club row copied from '").Append(buttonsPanel.name).Append("' ")
               .Append(rowRt.sizeDelta.x.ToString("0.###")).Append('x').Append(rowRt.sizeDelta.y.ToString("0.###"))
               .Append(", children ").Append(buttonSize.x.ToString("0.###")).Append('x').Append(buttonSize.y.ToString("0.###"))
               .Append(", slot ").Append(slot).Append('\n');
            return lendButton;
        }

        /// <summary>
        /// Clear a cloned Button's inherited onClick.
        ///
        /// <para>Instantiate copies the SERIALIZED persistent call list, so a LEND button cloned
        /// from COMPARE would also enter compare mode when tapped. Both the serialized array and
        /// any runtime listeners have to go.</para>
        /// </summary>
        private static void StripPersistentClicks(Button? button)
        {
            if (button == null) return;
            var so = new SerializedObject(button);
            SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls != null) { calls.ClearArray(); so.ApplyModifiedPropertiesWithoutUndo(); }
            button.onClick.RemoveAllListeners();
            EditorUtility.SetDirty(button);
        }

        private static RectTransform? FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            foreach (RectTransform t in root.GetComponentsInChildren<RectTransform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static GameObject SpawnModal(Transform parent, string prefabPath, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing.gameObject;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException("Missing prefab: " + prefabPath);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            SetRect(go, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Log.Append("  spawned ").Append(name).Append(" under ").Append(parent.name).Append('\n');
            return go;
        }

        // ═════════════════════════════════════════════════════════════════════
        // the card badges
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildCardBadge(string prefabPath, bool isClub)
        {
            Log.Append("== ").Append(prefabPath).Append(" ==\n");
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                GameObject badge = Child(root.transform, "LoanBadge");
                // TOP-LEFT at (8,8): anchor top-left, pivot top-left, y negative because UI y is up.
                SetRect(badge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                        new Vector2(8, -8), new Vector2(44, 44));
                AddImage(badge, Require<Sprite>(SpriteBadge), Color.white, Image.Type.Simple, raycast: false);

                GameObject glyph = Child(badge.transform, "Glyph");
                SetRect(glyph, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                        Vector2.zero, new Vector2(26, 26));
                AddImage(glyph, Require<Sprite>(SpriteIconOutSmall), Color.white, Image.Type.Simple, raycast: false);

                badge.transform.SetAsLastSibling();

                var view = badge.GetComponent<LoanBadgeView>() ?? badge.AddComponent<LoanBadgeView>();
                Wire(view, "badgeRoot", badge);
                Wire(view, "glyph", glyph.GetComponent<Image>());
                Wire(view, "iconLoanOutSmall", Require<Sprite>(SpriteIconOutSmall));
                Wire(view, "iconLoanInSmall", Require<Sprite>(SpriteIconInSmall));

                badge.SetActive(false);

                if (isClub)
                {
                    var card = root.GetComponent<Golfin.Inventory.ClubThumbnailCard>();
                    if (card != null) Wire(card, "loanBadge", view);
                }
                else
                {
                    var card = root.GetComponent<Golfin.Roster.CharacterThumbnailCard>();
                    if (card != null) Wire(card, "loanBadge", view);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Log.Append("  saved\n");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
