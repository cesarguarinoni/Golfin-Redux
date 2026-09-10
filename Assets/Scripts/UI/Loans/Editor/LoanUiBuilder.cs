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

        // ── asset_loans_offers (Docs/Scripts/make_loan_sprites.py) ───────────
        private const string SpriteSearchField = ArtDir + "S_LoanSearchField.png";
        private const string SpriteSearchGlyph = ArtDir + "S_LoanSearchGlyph.png";
        private const string SpriteTogglePill  = ArtDir + "S_LoanTogglePill.png";
        private const string SpriteToggleKnob  = ArtDir + "S_LoanToggleKnob.png";

        /// <summary>The DAILY pill's own panel and glow, reused verbatim by the offer pill —
        /// the node is a detached copy of the same Mission Card Container.</summary>
        private const string SpritePillPanel = "Assets/Art/HomeScreen/S_DailyPillPanel.png";
        private const string SpritePillGlow  = "Assets/Art/HomeScreen/S_DailyPillGlow.png";

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

        private const string ModalPrefabPath   = "Assets/Prefabs/UI/Modals/LoanModal.prefab";
        private const string ReturnPrefabPath  = "Assets/Prefabs/UI/Modals/LoanReturnModal.prefab";
        private const string RescindPrefabPath = "Assets/Prefabs/UI/Modals/LoanRescindModal.prefab";
        private const string OfferPrefabPath   = "Assets/Prefabs/UI/Modals/LoanOfferModal.prefab";
        private const string RowPrefabPath     = "Assets/Prefabs/UI/Loans/LoanRecipientRow.prefab";

        private const string HomeScreen  = "Canvas/ScreensRoot/HomeScreen";
        private const string DailyPill   = HomeScreen + "/DailyMissionPill";
        /// <summary>
        /// ⚠️ <c>SettingsScreen</c> IS ITS OWN SCENE ROOT with its own Canvas — it is NOT under
        /// <c>Canvas/ScreensRoot</c> like every other screen. Verified against the live scene
        /// rather than assumed from the pattern the other paths follow.
        /// </summary>
        private const string SettingsSub =
            "SettingsScreen/SettingsPanel/SettingsList/UserProfileRow/UserProfileSubmenu";

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

        // ── asset_loans_offers (nodes 14261:109851 / 109994 / 107143 / 33108) ─
        /// <summary>The search field's interior — node #050F1F @ 75 %. The 2 px white @ 35 %
        /// stroke is baked into the sprite, so this tint reaches only the middle.</summary>
        private static readonly Color SearchFill  = Hex("050F1F", 0.75f);
        /// <summary>The pill label — Text Colors/Mission Font, the daily pill's own token.</summary>
        private static readonly Color PillLabel   = Hex("EEDC9A", 1f);
        private static readonly Color ToggleOn    = Hex("2775DD", 1f);
        private static readonly Color RarityGreen = Hex("50C878", 1f);   // placeholder tint only

        [MenuItem("GOLFIN/Loans/Build Loan UI")]
        public static void Build()
        {
            Log.Clear();

            try
            {
                BuildRowPrefab();
                BuildModalPrefab();
                BuildReturnPrefab();
                BuildRescindPrefab();
                BuildOfferModalPrefab();

                BuildRosterDetail();
                BuildClubDetail();
                BuildOfferPill();
                BuildSettingsRow();

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

        /// <summary>Fill the parent exactly — anchors 0..1, zero offsets. What a TMP_InputField's
        /// text and placeholder need inside a viewport that has no layout group of its own.</summary>
        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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

            // ── asset_loans_offers §3.1 — the search field (node 14261:109851) ─
            //
            // 732×88, r12, fill #050F1F @ 75 %, 2 px white @ 35 % stroke. ONE Image draws both:
            // the stroke is baked into S_LoanSearchField and the fill is this tint, so there is
            // no second graphic whose radius could drift from the first's.
            GameObject searchGo = Child(panel.transform, "SearchField");
            Fixed(searchGo, 732, 88);
            Image searchBg = AddImage(searchGo, Require<Sprite>(SpriteSearchField), SearchFill,
                                      Image.Type.Sliced);
            searchBg.pixelsPerUnitMultiplier = 1f;

            GameObject glyph = Child(searchGo.transform, "Glyph");
            SetRect(glyph, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(20, 0), new Vector2(36, 36));
            AddImage(glyph, Require<Sprite>(SpriteSearchGlyph), Color.white, Image.Type.Simple,
                     raycast: false);

            // The text area starts after the glyph plus the node's 16 gap: 20 + 36 + 16 = 72.
            GameObject textArea = Child(searchGo.transform, "TextArea");
            SetRect(textArea, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f),
                    new Vector2(36, 0), new Vector2(-92, 0));
            textArea.AddComponent<RectMask2D>();

            TextMeshProUGUI searchPlaceholder =
                Row(textArea, "Placeholder", FontRegular, 33, new Color(1, 1, 1, 0.55f),
                    TextAlignmentOptions.MidlineLeft, 640, 88);
            searchPlaceholder.enableWordWrapping = false;
            TextMeshProUGUI searchText =
                Row(textArea, "Text", FontRegular, 33, Color.white,
                    TextAlignmentOptions.MidlineLeft, 640, 88);
            searchText.enableWordWrapping = false;
            // Both fill the text area rather than sitting at a preferred size — the parent has no
            // layout group, so LayoutElement alone would leave them at whatever Fixed() wrote.
            Stretch(Rect(searchPlaceholder.gameObject));
            Stretch(Rect(searchText.gameObject));

            var input = searchGo.GetComponent<TMP_InputField>() ?? searchGo.AddComponent<TMP_InputField>();
            input.textViewport = Rect(textArea);
            input.textComponent = searchText;
            input.placeholder = searchPlaceholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = 40;
            input.targetGraphic = searchBg;
            input.fontAsset = searchText.font;
            input.pointSize = 33;

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
            // ⚠️ TRUE, and this FLIPPED in v2 (memory: reference_unity_layout_sizedelta_and_controlheight).
            //
            // In v1 this group's children were the recipient ROWS themselves — fixed 96 px each —
            // so `childControlHeight = false` was right: it left each row at its authored height.
            // In v2 the children are two SECTIONS whose heights come from their own
            // ContentSizeFitters, and `false` makes this group ignore those entirely and stack the
            // sections at whatever sizeDelta they happen to hold. Measured: the RESULTS block and
            // the PEOPLE YOU FOLLOW header drew ON TOP OF EACH OTHER, with "PEOPLE YOU FOLLOW"
            // struck through a result row. The rows keep their fixed height because THEIR parents
            // (ResultRows / FollowedRows) still set childControlHeight = false.
            clg.childControlHeight = true;
            var cfit = content.GetComponent<ContentSizeFitter>() ?? content.AddComponent<ContentSizeFitter>();
            cfit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = Rect(content);
            scroll.viewport = Rect(scrollGo);

            // ── two sections inside one scroll (asset_loans_offers §3.1) ──────
            //
            // RESULTS above, PEOPLE YOU FOLLOW below, both spawning the SAME row prefab into
            // their own parent. One scroll rather than two so a long result list and a long
            // followed list share the same 432 px viewport instead of each getting half of it.
            GameObject resultsSection = Child(content.transform, "ResultsSection");
            var rsl = resultsSection.GetComponent<VerticalLayoutGroup>()
                   ?? resultsSection.AddComponent<VerticalLayoutGroup>();
            rsl.spacing = 16;
            rsl.childAlignment = TextAnchor.UpperCenter;
            rsl.childForceExpandWidth = false;
            rsl.childForceExpandHeight = false;
            rsl.childControlWidth = true;
            rsl.childControlHeight = false;
            var rsf = resultsSection.GetComponent<ContentSizeFitter>()
                   ?? resultsSection.AddComponent<ContentSizeFitter>();
            rsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI resultsHeader =
                Row(resultsSection, "ResultsHeader", FontSemiBold, 30, SubText,
                    TextAlignmentOptions.MidlineLeft, 732, TextSlot(30));

            GameObject resultsParent = Child(resultsSection.transform, "ResultRows");
            var rpl = resultsParent.GetComponent<VerticalLayoutGroup>()
                   ?? resultsParent.AddComponent<VerticalLayoutGroup>();
            rpl.spacing = 16;
            rpl.childAlignment = TextAnchor.UpperCenter;
            rpl.childForceExpandWidth = false;
            rpl.childForceExpandHeight = false;
            rpl.childControlWidth = true;
            rpl.childControlHeight = false;
            var rpf = resultsParent.GetComponent<ContentSizeFitter>()
                   ?? resultsParent.AddComponent<ContentSizeFitter>();
            rpf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject noResultsGo = Child(resultsSection.transform, "NoResults");
            TextMeshProUGUI noResults = AddText(noResultsGo, FontRegular, 30, SubText,
                                                TextAlignmentOptions.Center);
            Fixed(noResultsGo, 732, 72);
            noResultsGo.SetActive(false);

            // Hidden until the player types — the node shows RESULTS only with a query in the box.
            resultsSection.SetActive(false);

            TextMeshProUGUI followedHeader =
                Row(content, "FollowedHeader", FontSemiBold, 30, SubText,
                    TextAlignmentOptions.MidlineLeft, 732, TextSlot(30));

            GameObject followedParent = Child(content.transform, "FollowedRows");
            var fpl = followedParent.GetComponent<VerticalLayoutGroup>()
                   ?? followedParent.AddComponent<VerticalLayoutGroup>();
            fpl.spacing = 16;
            fpl.childAlignment = TextAnchor.UpperCenter;
            fpl.childForceExpandWidth = false;
            fpl.childForceExpandHeight = false;
            fpl.childControlWidth = true;
            fpl.childControlHeight = false;
            var fpf = followedParent.GetComponent<ContentSizeFitter>()
                   ?? followedParent.AddComponent<ContentSizeFitter>();
            fpf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
            Wire(controller, "recipientParent", followedParent.transform);
            Wire(controller, "searchField", input);
            Wire(controller, "searchPlaceholder", searchPlaceholder);
            Wire(controller, "resultsSectionRoot", resultsSection);
            Wire(controller, "resultsHeader", resultsHeader);
            Wire(controller, "resultsParent", resultsParent.transform);
            Wire(controller, "noResultsRoot", noResultsGo);
            Wire(controller, "noResultsText", noResults);
            Wire(controller, "followedHeader", followedHeader);
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
        /// <summary>
        /// The height a ONE-LINE text slot needs, from its font size.
        ///
        /// <para>
        /// ⚠️ NEVER PIN A TEXT SLOT TO ITS OWN LINE BOX. Rubik's face metric is 1.185 line-box
        /// per point, so a 33 px label needs 39.1 — and a 40 px LayoutElement, which CLEARS that
        /// by 0.9 px, made TMP emit <b>zero characters</b> with <c>overflowMode = Ellipsis</c>.
        /// Not a clipped glyph, not an ellipsis: `characterCount = 0` and
        /// `renderedWidth = -4294967000` (the never-computed sentinel), on an object that
        /// reported the right text, the right rect, full alpha and active=true. The offer modal's
        /// asset name rendered as an empty band and every property said it was fine.
        /// </para>
        /// <para>
        /// The ellipsis routine needs room for the ellipsis glyph and TMP's own margins on top of
        /// the line box, and how much is not worth deriving. 1.4× is comfortably past it at every
        /// size this file uses, and routing every slot through one function means no call site can
        /// pick a hairline again. Verified by the capture bot's `chars=` dump, which is the only
        /// thing that distinguishes "renders" from "silently renders nothing".
        /// </para>
        /// </summary>
        private static float TextSlot(float fontSize) => Mathf.Ceil(fontSize * 1.4f);

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
        // asset_loans_offers — the rescind confirm prefab (§3.2)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The RETURN popup's shell with two strings swapped and one button relabelled.
        ///
        /// <para>Built by a near-copy of <see cref="BuildReturnPrefab"/> rather than by
        /// parameterising it, and that is a deliberate call: the two bodies differ (a level vs a
        /// cooldown), the two controllers differ, and a shared builder taking six flags to express
        /// "which of these two" would be harder to read than the twenty lines it saved. The
        /// SHELL — panel sprite, ppum, padding, spacing, both button sprites — is what is actually
        /// reused, and every one of those comes from the same named constant.</para>
        /// </summary>
        private static void BuildRescindPrefab()
        {
            Log.Append("== LoanRescindModal.prefab ==\n");

            var root = new GameObject("LoanRescindModal", typeof(RectTransform));
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

            GameObject rescindGo = Child(footer.transform, "RescindButton");
            SetRect(rescindGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(345, 120));
            Button rescind = MakeButton(rescindGo, Require<Sprite>(SpriteButtonGoldBig),
                                        Image.Type.Sliced, "RESCIND", 39);

            panel.SetActive(false);

            var controller = root.AddComponent<LoanRescindModalController>();
            Wire(controller, "modalPanel", panel);
            Wire(controller, "backdrop", backdrop);
            Wire(controller, "titleText", title);
            Wire(controller, "bodyText", body);
            Wire(controller, "cancelButton", cancel);
            Wire(controller, "rescindButton", rescind);
            Wire(controller, "rescindButtonText", rescind.GetComponentInChildren<TextMeshProUGUI>(true));

            PrefabUtility.SaveAsPrefabAsset(root, RescindPrefabPath);
            Object.DestroyImmediate(root);
            Log.Append("  saved ").Append(RescindPrefabPath).Append('\n');
        }

        // ═════════════════════════════════════════════════════════════════════
        // asset_loans_offers — the offer modal prefab (§3.3, node 14261:107143)
        // ═════════════════════════════════════════════════════════════════════

        private static void BuildOfferModalPrefab()
        {
            Log.Append("== LoanOfferModal.prefab ==\n");

            var root = new GameObject("LoanOfferModal", typeof(RectTransform));
            SetRect(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject backdrop = Child(root.transform, "Backdrop");
            SetRect(backdrop, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(backdrop, null, Scrim);
            backdrop.SetActive(false);

            // 780 wide, HUG height (node: VERTICAL, gap 24, pad 24).
            GameObject panel = Child(root.transform, "ModalPanel");
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(780, 900));
            Image panelImg = AddImage(panel, Require<Sprite>(SpritePopupPanel), Color.white, Image.Type.Sliced);
            panelImg.pixelsPerUnitMultiplier = 3.2f;   // border 64 ÷ 3.2 = 20 UI px, the node radius

            var vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 24, 24);
            vlg.spacing = 24;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = Row(panel, "Title", FontSemiBold, 45, Color.white,
                                        TextAlignmentOptions.Center, 732, 60);
            TextMeshProUGUI subtitle = Row(panel, "Subtitle", FontRegular, 33, Color.white,
                                           TextAlignmentOptions.Center, 732, 44);

            // ── the asset row (node 14261:107146): 732×140, r12, #050F1F @ 60 % ──
            GameObject assetRow = Child(panel.transform, "AssetRow");
            Fixed(assetRow, 732, 140);
            // The recipient row's sprite, tinted the SAME #050F1F @ 60 % — same shape (r12), same
            // fill, one atom. Its 732×96 bake is 9-sliced to 140 tall, which is why the border is
            // set below rather than left at the row's own.
            Image assetBg = AddImage(assetRow, Require<Sprite>(SpriteRow), RowFill, Image.Type.Sliced,
                                     raycast: false);
            assetBg.pixelsPerUnitMultiplier = 1f;
            var arl = assetRow.GetComponent<HorizontalLayoutGroup>() ?? assetRow.AddComponent<HorizontalLayoutGroup>();
            arl.padding = new RectOffset(20, 24, 0, 0);
            arl.spacing = 24;
            arl.childAlignment = TextAnchor.MiddleLeft;
            arl.childForceExpandWidth = false;
            arl.childForceExpandHeight = false;
            arl.childControlWidth = true;
            arl.childControlHeight = true;

            GameObject portrait = Child(assetRow.transform, "Portrait");
            Image portraitImg = AddImage(portrait, null, Color.white, Image.Type.Simple, raycast: false);
            portraitImg.preserveAspect = true;
            var pLe = portrait.GetComponent<LayoutElement>() ?? portrait.AddComponent<LayoutElement>();
            pLe.preferredWidth = 100;
            pLe.preferredHeight = 100;
            pLe.flexibleWidth = 0;

            GameObject texts = Child(assetRow.transform, "Texts");
            var tl = texts.GetComponent<VerticalLayoutGroup>() ?? texts.AddComponent<VerticalLayoutGroup>();
            // ⚠️ ZERO, not the node's 6 — and that is not a fidelity miss, it is the conversion.
            //
            // Figma's gap sits between text boxes that are TIGHT to their glyphs. A TMP slot is a
            // LINE BOX: it carries the font's internal leading above the cap and its descent below
            // the baseline whether the string uses them or not, and `TextSlot` adds a safety
            // margin on top (see its remarks — a hairline slot makes an Ellipsis text render
            // nothing at all). Adding 6 more on top of all of that separates the two lines by
            // more than the design does.
            //
            // Measured, not reasoned: glyph-bottom to glyph-top is **19 px** in the node render
            // and was **25 px** built — exactly this 6. Cesar caught it by eye on the first pass
            // ("MYTHIC is not vertically centred relative to the portrait"), which is what the
            // extra 6 looks like: the second line pushed below the portrait's midline.
            tl.spacing = 0;
            tl.childAlignment = TextAnchor.MiddleLeft;
            tl.childForceExpandWidth = false;
            tl.childForceExpandHeight = false;
            tl.childControlWidth = true;
            tl.childControlHeight = true;
            var textsLe = texts.GetComponent<LayoutElement>() ?? texts.AddComponent<LayoutElement>();
            textsLe.flexibleWidth = 1;    // FILL, per the node

            GameObject nameGo = Child(texts.transform, "AssetName");
            TextMeshProUGUI assetName = AddText(nameGo, FontSemiBold, 33, Color.white,
                                                TextAlignmentOptions.MidlineLeft);
            assetName.enableWordWrapping = false;
            assetName.overflowMode = TextOverflowModes.Ellipsis;
            var nLe = nameGo.GetComponent<LayoutElement>() ?? nameGo.AddComponent<LayoutElement>();
            nLe.preferredHeight = TextSlot(33);      // 47, not 40 — see TextSlot's remarks
            // FILL — same trap as the settings row: a label whose width comes from its own
            // (empty-at-build-time) text is measured at 0 and never recovers unless something
            // dirties the layout after the string lands.
            nLe.flexibleWidth = 1;
            // An AUTHORED sizeDelta as well, even though the layout group overwrites it at
            // runtime (measured live: 564×47). Unity's default is 100×100, and the fidelity
            // linter flags that as trap C9 — correctly: a reviewer opening the prefab sees a
            // 100 px box, and the day this element leaves a layout group it would silently BE
            // one. 564 is the column's real width (732 − 20 − 24 padding − 100 portrait − 24 gap).
            Rect(nameGo).sizeDelta = new Vector2(564, TextSlot(33));

            // The meta line is a THREE-CHILD horizontal group, not one string, because the three
            // parts have three different colours — the rarity's comes from RarityHelper at runtime
            // and cannot be a rich-text tag baked into a format string.
            GameObject meta = Child(texts.transform, "Meta");
            var ml = meta.GetComponent<HorizontalLayoutGroup>() ?? meta.AddComponent<HorizontalLayoutGroup>();
            ml.spacing = 16;
            ml.childAlignment = TextAnchor.MiddleLeft;
            ml.childForceExpandWidth = false;
            ml.childForceExpandHeight = false;
            ml.childControlWidth = true;
            ml.childControlHeight = true;
            var metaLe = meta.GetComponent<LayoutElement>() ?? meta.AddComponent<LayoutElement>();
            metaLe.preferredHeight = TextSlot(30);
            metaLe.flexibleWidth = 1;

            TextMeshProUGUI rarity = MetaChip(meta, "Rarity", FontSemiBold, RarityGreen);
            TextMeshProUGUI level  = MetaChip(meta, "Level",  FontRegular,  Color.white);
            TextMeshProUGUI days   = MetaChip(meta, "Days",   FontRegular,  SubText);

            // ── body ──────────────────────────────────────────────────────────
            TextMeshProUGUI terms = Row(panel, "Terms", FontRegular, 30, Color.white,
                                        TextAlignmentOptions.TopLeft, 732, 132);
            TextMeshProUGUI fine = Row(panel, "FinePrint", FontRegular, 26, SubText,
                                       TextAlignmentOptions.Top, 732, 40);

            // ── footer: DECLINE silver + ACCEPT gold, equal widths ────────────
            GameObject footer = Child(panel.transform, "Footer");
            Fixed(footer, 732, 120);
            var fl = footer.GetComponent<HorizontalLayoutGroup>() ?? footer.AddComponent<HorizontalLayoutGroup>();
            fl.spacing = 24;
            fl.childAlignment = TextAnchor.MiddleCenter;
            fl.childForceExpandWidth = false;
            fl.childForceExpandHeight = false;
            fl.childControlWidth = false;
            fl.childControlHeight = false;

            GameObject declineGo = Child(footer.transform, "DeclineButton");
            SetRect(declineGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(354, 120));
            Button decline = MakeButton(declineGo, Require<Sprite>(SpriteButtonSilverBig),
                                        Image.Type.Sliced, "DECLINE", 39);

            GameObject acceptGo = Child(footer.transform, "AcceptButton");
            SetRect(acceptGo, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(354, 120));
            Button accept = MakeButton(acceptGo, Require<Sprite>(SpriteButtonGoldBig),
                                       Image.Type.Sliced, "ACCEPT", 39);

            panel.SetActive(false);

            var controller = root.AddComponent<LoanOfferModalController>();
            Wire(controller, "modalPanel", panel);
            Wire(controller, "backdrop", backdrop);
            Wire(controller, "titleText", title);
            Wire(controller, "subtitleText", subtitle);
            Wire(controller, "assetPortrait", portraitImg);
            Wire(controller, "assetNameText", assetName);
            Wire(controller, "rarityText", rarity);
            Wire(controller, "levelText", level);
            Wire(controller, "daysText", days);
            Wire(controller, "termsText", terms);
            Wire(controller, "finePrintText", fine);
            Wire(controller, "declineButton", decline);
            Wire(controller, "declineButtonText", decline.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(controller, "acceptButton", accept);
            Wire(controller, "acceptButtonText", accept.GetComponentInChildren<TextMeshProUGUI>(true));

            PrefabUtility.SaveAsPrefabAsset(root, OfferPrefabPath);
            Object.DestroyImmediate(root);
            Log.Append("  saved ").Append(OfferPrefabPath).Append('\n');
        }

        /// <summary>One chip of the asset row's meta line — hug-width, no wrap.</summary>
        private static TextMeshProUGUI MetaChip(GameObject parent, string name, string font, Color color)
        {
            GameObject go = Child(parent.transform, name);
            TextMeshProUGUI tmp = AddText(go, font, 30, color, TextAlignmentOptions.MidlineLeft);
            tmp.enableWordWrapping = false;
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = TextSlot(30);
            le.flexibleWidth = 0;
            // Authored, for the same reason as AssetName above — the chips HUG at runtime, but
            // Unity's 100×100 default is what a reviewer and the linter both see in the prefab.
            Rect(go).sizeDelta = new Vector2(140, TextSlot(30));
            return tmp;
        }

        // ═════════════════════════════════════════════════════════════════════
        // the two detail panels
        // ═════════════════════════════════════════════════════════════════════

        private static GameObject Find(string path)
        {
            var go = GameObject.Find(path) ?? FindIncludingInactive(path);
            if (go == null)
                throw new System.InvalidOperationException($"Scene object not found: {path}");
            return go;
        }

        /// <summary>
        /// Walk a "/"-separated scene path through INACTIVE objects too.
        ///
        /// <para>
        /// ⚠️ <c>GameObject.Find</c> SKIPS ANYTHING INACTIVE, including every ancestor. Most of
        /// this project's screens are authored inactive — <c>SettingsScreen</c>'s whole
        /// <c>SettingsPanel</c> is, and so is every modal root — so a path that reaches into one
        /// is unfindable by the ordinary call, and the failure reads as "the object does not
        /// exist" rather than as "the object is switched off".
        /// </para>
        /// <para>
        /// Walks <c>Transform.Find</c> from the scene roots instead, which is inactive-blind in
        /// exactly the way this needs.
        /// </para>
        /// </summary>
        private static GameObject? FindIncludingInactive(string path)
        {
            string[] parts = path.Split('/');
            if (parts.Length == 0) return null;

            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != parts[0]) continue;

                Transform t = root.transform;
                for (int i = 1; i < parts.Length && t != null; i++) t = t.Find(parts[i]);
                if (t != null) return t.gameObject;
            }
            return null;
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
            EnsureGroup(dim);
            dim.SetActive(false);

            GameObject ribbon = Child(leftPanel.transform, "LoanRibbon");
            SetRect(ribbon, mid, mid, mid,
                    new Vector2(centreX - parentCentreX, (topY - 36f) - parentCentreY),
                    new Vector2(width, 72f));
            AddImage(ribbon, Require<Sprite>(SpriteRibbon), RibbonFill, Image.Type.Simple, raycast: false);
            IgnoreLayout(ribbon);
            EnsureGroup(ribbon);

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
            Wire(view, "ribbonGroup", ribbon.GetComponent<CanvasGroup>());
            Wire(view, "dimGroup", dim.GetComponent<CanvasGroup>());

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
        /// <summary>
        /// The alpha channel <c>LoanRibbonView</c>'s entrance drives (asset_loans_polish §3).
        ///
        /// <para>AUTHORED AT ALPHA 1, so the REST state of both objects is bit-for-bit what it was
        /// before the polish pass — a CanvasGroup contributes no geometry and no raycast change of
        /// its own (both Images are already <c>raycastTarget: false</c>), and the motion is the only
        /// thing that ever moves the alpha off 1.</para>
        /// </summary>
        /// <summary>
        /// Re-anchor <paramref name="rt"/> horizontally onto <paramref name="artwork"/>'s own
        /// resting geometry: the artwork's x anchors, a centre pivot, and x = width / 2.
        ///
        /// <para>Vertical anchoring is left exactly as <c>BuildRibbon</c> set it — the ribbon still
        /// takes the PANEL's top edge and the dim the PANEL's height, which is deliberate (the dim
        /// covers the info block under the artwork as well).</para>
        /// </summary>
        private static void MatchArtworkX(RectTransform rt, RectTransform artwork)
        {
            rt.anchorMin = new Vector2(artwork.anchorMin.x, rt.anchorMin.y);
            rt.anchorMax = new Vector2(artwork.anchorMax.x, rt.anchorMax.y);
            rt.pivot     = new Vector2(0.5f, rt.pivot.y);
            // `sizeDelta.x`, NOT `rect.width`. With the x anchors collapsed to a point the two are
            // the same number — but READING `rect` in edit mode makes Unity evaluate layout for the
            // whole canvas, and every LayoutGroup in ShellScene then writes its children's
            // anchoredPosition and sizeDelta. Saving after that wrote 1367 lines of anchor churn
            // across 157 unrelated objects (project_scene_save_bakes_layout_churn). `sizeDelta` is
            // the authored value and reads nothing.
            rt.anchoredPosition = new Vector2(rt.sizeDelta.x * 0.5f, rt.anchoredPosition.y);
        }

        private static void EnsureGroup(GameObject go)
        {
            // `== null` rather than `??` (CLAUDE.md Basic Rules #4).
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
        }

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
            // asset_loans_offers §3.2 — the RESCIND confirm, spawned per screen like its two
            // siblings so it inherits the same canvas and sorting.
            GameObject rescMod = SpawnModal(detail.transform.parent, RescindPrefabPath, "LoanRescindModal");

            var panel = detail.GetComponent<Golfin.Roster.CharacterDetailPanel>();
            Wire(panel, "lendButton", lend);
            Wire(panel, "lendButtonText", lend.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(panel, "loanRibbon", ribbon);
            Wire(panel, "loanModal", modal.GetComponent<LoanModalController>());
            Wire(panel, "loanReturnModal", retMod.GetComponent<LoanReturnModalController>());
            Wire(panel, "loanRescindModal", rescMod.GetComponent<LoanRescindModalController>());
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

            // ⚠️ WIDTH FROM THE ARTWORK IS NOT ENOUGH — THE X HAS TO COME FROM IT TOO.
            //
            // Cesar, 2026-09-09: "On loan overlay is not correctly over the image in clubs (it
            // spills to the left and does not reach the right border)." Measured in play mode:
            // ribbon leftD = rightD = -27.050 px against ClubImage.
            //
            // The club LeftPanel is a VerticalLayoutGroup with childAlignment UpperLeft, so the
            // 537-wide artwork sits LEFT-FLUSH in the 482.9-wide panel and overflows 54.1 px to
            // the RIGHT. `BuildRibbon` centres its bar on the anchor panel, so a 537-wide bar
            // centred on a 482.9-wide panel lands (537 - 482.9) / 2 = 27.05 px left of a 537-wide
            // artwork that is left-flush. Same width, wrong origin.
            //
            // The fix is to give the bar and the dim the ARTWORK's horizontal anchoring rather
            // than the panel's centre — left anchor, centre pivot, x = width / 2 — which is the
            // resting geometry the layout group gives ClubImage itself. Expressed against the
            // anchor rather than as a measured offset, because in EDIT MODE the group has not run
            // and ClubImage's anchoredPosition is still 0: reading its live centre here would
            // place the ribbon 268 px out. (The Roster panel is untouched: its LeftPanel has no
            // layout group and its ribbon is already flush with the portrait at leftΔ 0.000.)
            if (clubImage != null)
            {
                MatchArtworkX((RectTransform)ribbon.transform, clubImage);
                RectTransform? clubDim = FindDeep(leftPanel, "LentDim");
                if (clubDim != null) MatchArtworkX(clubDim, clubImage);
            }

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
            GameObject rescMod = SpawnModal(screen, RescindPrefabPath, "LoanRescindModal");

            var panel = detail.GetComponent<Golfin.Inventory.ClubDetailPanel>();
            Wire(panel, "lendButton", lend);
            Wire(panel, "lendButtonText", lend.GetComponentInChildren<TextMeshProUGUI>(true));
            Wire(panel, "loanRibbon", ribbon);
            Wire(panel, "loanModal", modal.GetComponent<LoanModalController>());
            Wire(panel, "loanReturnModal", retMod.GetComponent<LoanReturnModalController>());
            Wire(panel, "loanRescindModal", rescMod.GetComponent<LoanRescindModalController>());
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
        // ═════════════════════════════════════════════════════════════════════
        // asset_loans_offers — the Home offer pill (§3.3, node 14261:33108)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// CLONED FROM THE DAILY PILL, object for object (Rule 19).
        ///
        /// <para>
        /// The Figma node is a DETACHED COPY of the daily pill's own "Mission Card Container" —
        /// same panel sprite, same glow, same 122 height, same 24/16 padding, same 10 gap. So the
        /// clone here is not a convenience, it is the design: the two pills are one component with
        /// two contents, and hand-building a second one from a rounded rect would put a
        /// near-identical-but-not-identical pill next to the original, which is the single most
        /// visible kind of fidelity failure.
        /// </para>
        /// <para>
        /// <c>Instantiate</c> of the live scene object rather than a fresh hierarchy: it carries
        /// the Image sprites, the ppum, the anchors and the ButtonPressFeedback across without any
        /// of them being retyped here. The daily pill's own controller is then REMOVED from the
        /// copy (it would fetch a mission and fight for the same slot) and replaced with this
        /// pill's.
        /// </para>
        /// </summary>
        private static void BuildOfferPill()
        {
            Log.Append("== HomeScreen/LoanOfferPill ==\n");

            GameObject daily = Find(DailyPill);
            GameObject home = Find(HomeScreen);

            GameObject pill;
            Transform existing = home.transform.Find("LoanOfferPill");
            if (existing != null)
            {
                pill = existing.gameObject;
                Log.Append("  reusing existing LoanOfferPill (idempotent re-run)\n");
            }
            else
            {
                pill = Object.Instantiate(daily, home.transform);
                pill.name = "LoanOfferPill";
                // Immediately after the daily pill in the hierarchy, so the two draw in the order
                // they read.
                pill.transform.SetSiblingIndex(daily.transform.GetSiblingIndex() + 1);
                Log.Append("  cloned from ").Append(DailyPill)
                   .Append(" (panel guid=").Append(AssetDatabase.AssetPathToGUID(SpritePillPanel))
                   .Append(", glow guid=").Append(AssetDatabase.AssetPathToGUID(SpritePillGlow))
                   .Append(")\n");
            }

            // The daily pill's controller and streak flame have no meaning here.
            var stale = pill.GetComponent<Golfin.UI.Home.DailyMissionPillController>();
            if (stale != null) Object.DestroyImmediate(stale, allowDestroyingAssets: false);

            Transform flame = pill.transform.Find("StreakFlame");
            if (flame != null) Object.DestroyImmediate(flame.gameObject, allowDestroyingAssets: false);

            // The icon takes the flame's slot: 56×56 at padX 24, vertically centred in the 122.
            GameObject icon = Child(pill.transform, "Icon");
            SetRect(icon, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(24, -33), new Vector2(56, 56));
            Image iconImg = AddImage(icon, Require<Sprite>(SpriteIconInBig), Color.white,
                                     Image.Type.Simple, raycast: false);
            iconImg.preserveAspect = true;
            icon.transform.SetSiblingIndex(2);   // after Glow and Panel, before Label

            // The label is the CLONE's own — it arrived with the right font, colour and rect from
            // the daily pill. Only the size changes (the node is one step down, 39 vs 45) and the
            // LocalizedText binder goes, because this label's text is a FORMAT with the lender's
            // name in it and no static key can carry that.
            Transform labelT = pill.transform.Find("Label");
            var label = labelT != null ? labelT.GetComponent<TextMeshProUGUI>() : null;
            if (label == null)
                throw new System.InvalidOperationException("LoanOfferPill: the cloned Label is missing.");

            var binder = label.GetComponent<LocalizedText>();
            if (binder != null) Object.DestroyImmediate(binder, allowDestroyingAssets: false);

            // 39 node px in the daily pill's own ratio: it renders its 45 px node label at 40, so
            // 40 × 39/45 = 34.67. Matching the SIBLING rather than applying a divisor from
            // elsewhere is what keeps the two pills reading as one family.
            //
            // ⚠️ fontSizeMax, NOT fontSize. The clone arrives with `enableAutoSizing` ON (min 24,
            // max 40) from the daily pill, and with auto-sizing on, `fontSize` is an OUTPUT — TMP
            // overwrites it on the next layout. Setting it alone measured 32.6 immediately (the
            // fitted size for the placeholder string) and would have snapped back to the DAILY
            // pill's 40 the moment a short lender name fit, which is precisely the "one size down"
            // the node asks for, undone. Capping the range keeps the step-down AND keeps the
            // shrink-to-fit that stops a long name from clipping.
            label.enableAutoSizing = true;
            label.fontSizeMin = 24f;
            label.fontSizeMax = 34.67f;
            label.fontSize = 34.67f;
            label.color = PillLabel;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.text = "LOAN OFFER FROM KENJI";
            SetRect(labelT.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(90, -31), new Vector2(433, 60));   // 24 + 56 + 10 = 90

            var button = pill.GetComponent<Button>() ?? pill.AddComponent<Button>();
            StripPersistentClicks(button);
            if (pill.GetComponent<ButtonPressFeedback>() == null) pill.AddComponent<ButtonPressFeedback>();

            var ctrl = pill.GetComponent<Golfin.UI.Home.LoanOfferPillController>()
                    ?? pill.AddComponent<Golfin.UI.Home.LoanOfferPillController>();

            Wire(ctrl, "pillRect", pill.GetComponent<RectTransform>());
            Wire(ctrl, "glowImage", pill.transform.Find("Glow")?.GetComponent<Image>());
            Wire(ctrl, "iconImage", iconImg);
            Wire(ctrl, "labelRect", labelT.GetComponent<RectTransform>());
            Wire(ctrl, "labelText", label);
            Wire(ctrl, "tapButton", button);
            Wire(ctrl, "dailyPill", daily.GetComponent<Golfin.UI.Home.DailyMissionPillController>());
            Wire(ctrl, "offerModal", SpawnModal(Find("Canvas").transform, OfferPrefabPath,
                                                "LoanOfferModal")
                                     .GetComponent<LoanOfferModalController>());

            // Home owns the placement chain (the offer pill reads the daily pill's Y, which reads
            // the notice panel's), so HomeScreenController has to hold the reference.
            var homeCtrl = home.GetComponent<GolfinRedux.UI.HomeScreenController>();
            if (homeCtrl != null) Wire(homeCtrl, "loanOfferPill", ctrl);
            else Log.Append("  ⚠️ HomeScreenController not found — placement hook NOT wired\n");

            Log.Append("  wired LoanOfferPillController\n");
        }

        // ═════════════════════════════════════════════════════════════════════
        // asset_loans_offers — Settings ▸ User Profile ▸ LOAN OFFERS (§3.4)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Append the LOAN OFFERS row to the User Profile submenu and grow the submenu to fit.
        ///
        /// ⚠️ THE SPEC SAYS "after the DELETE ACCOUNT block" AND THAT BLOCK DOES NOT EXIST IN THE
        /// SHIPPED SCENE. The Figma frame draws EMAIL, ACCOUNT ID and DELETE ACCOUNT above this
        /// row; the built submenu has only USERNAME + the input + SAVE + a feedback line (and an
        /// inactive AccountLinkingSection). So "after DELETE ACCOUNT" is honoured as what it
        /// means — LAST in the submenu — and the discrepancy is surfaced in the report rather
        /// than papered over by building three sections this task was not asked for.
        ///
        /// <para>
        /// THE SUBMENU IS NOT A LAYOUT GROUP. Its children are absolutely positioned, so the row
        /// is placed by hand and the CONTAINER's height is grown by exactly the row's height plus
        /// its gap. That height is what <c>SettingsMenuItem</c> reads at Awake
        /// (<c>submenuHeight = 0</c> ⇒ auto-detect from <c>sizeDelta.y</c>) and what it adds to
        /// the row's <c>LayoutElement.preferredHeight</c> when expanded — which is what pushes
        /// LOG OUT and CLOSE down in the OUTER VerticalLayoutGroup. Nothing else has to be told.
        /// </para>
        /// </summary>
        private static void BuildSettingsRow()
        {
            Log.Append("== Settings/UserProfileSubmenu/LoanOffersRow ==\n");

            GameObject submenu = Find(SettingsSub);
            RectTransform submenuRect = Rect(submenu);

            // 68 title + 4 gap + 42 sub = 114, plus 10 of breathing room. Derived from TextSlot
            // rather than typed, so raising a font size cannot silently squeeze the row — which
            // is exactly what would have happened when the title went 40 → 48.
            const float RowH = 124f;
            const float GapY = 20f;

            // ── the row's top, DERIVED, so a re-run cannot drift ──────────────
            //
            // The first version computed this as "the submenu's current height" and then grew the
            // submenu — correct once, and wrong on every re-run that changed RowH, because the
            // height it read already contained the row. Measuring the OTHER children instead
            // makes the builder genuinely idempotent: the answer is the same whether the row
            // exists yet or not, and it survives the submenu gaining content later.
            float contentBottom = 0f;
            foreach (RectTransform child in submenu.transform)
            {
                if (child.name == "LoanOffersRow") continue;
                if (!child.gameObject.activeSelf) continue;
                // Children of this submenu are top-anchored with negative y; the deepest edge is
                // the most negative (y − height).
                contentBottom = Mathf.Max(contentBottom,
                                          -child.anchoredPosition.y + child.sizeDelta.y);
            }
            float top = contentBottom + GapY;

            GameObject row = Child(submenu.transform, "LoanOffersRow");
            SetRect(row, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1),
                    new Vector2(0, -top), new Vector2(-48, RowH));

            var hl = row.GetComponent<HorizontalLayoutGroup>() ?? row.AddComponent<HorizontalLayoutGroup>();
            hl.padding = new RectOffset(24, 24, 0, 0);
            hl.spacing = 24;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = true;
            hl.childControlHeight = true;

            // texts column — FILL, per the node
            GameObject texts = Child(row.transform, "Texts");
            var vl = texts.GetComponent<VerticalLayoutGroup>() ?? texts.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 4;
            vl.childAlignment = TextAnchor.MiddleLeft;
            vl.childForceExpandWidth = false;
            vl.childForceExpandHeight = false;
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            var textsLe = texts.GetComponent<LayoutElement>() ?? texts.AddComponent<LayoutElement>();
            textsLe.flexibleWidth = 1;

            GameObject titleGo = Child(texts.transform, "Title");
            // 48 — the node's number, and ALSO the family's.
            //
            // ⚠️ THIS WAS 40, ON A RATIONALE BUILT FROM THE WRONG FIELD. The claim was "the
            // sibling rows render their 48 px node labels at 40" — but 40 is those labels'
            // `sizeDelta.y`, not their `fontSize`. Every section-title Label in SettingsList
            // (User Profile, Sound Settings, Graphics, Controls, Language, Terms of Use, Privacy
            // Policy, FAQ, About, Contact Form, Log Out) is fontSize **48**, measured. So 40 made
            // this row one step SMALLER than every neighbour — the exact opposite of the
            // "reads as one family" argument used to justify it.
            //
            // The lesson generalises past this row: a rect's height and a font's size are two
            // different numbers that are often close enough to swap without looking wrong, and a
            // fidelity rationale built on the wrong one survives review until somebody enumerates
            // the siblings. Enumerate them.
            TextMeshProUGUI title = AddText(titleGo, FontSemiBold, 48, Color.white,
                                            TextAlignmentOptions.MidlineLeft);
            title.enableWordWrapping = false;
            var tLe = titleGo.GetComponent<LayoutElement>() ?? titleGo.AddComponent<LayoutElement>();
            tLe.preferredHeight = TextSlot(48);      // 68
            // FILL the column. Without this the VLG sizes the label from its PREFERRED width,
            // which is a function of its TEXT — and the text is empty until OnEnable writes it,
            // so the label is measured at width 0 and a left-aligned string has nothing to be
            // left-aligned in. Measured: rect width 0.00 before this line existed.
            tLe.flexibleWidth = 1;

            GameObject subGo = Child(texts.transform, "Subtitle");
            // 30 — the node's number. Was 25, scaled down to sit under the (wrong) 40 title.
            TextMeshProUGUI sub = AddText(subGo, FontRegular, 30, SubText,
                                          TextAlignmentOptions.MidlineLeft);
            sub.enableWordWrapping = false;
            sub.overflowMode = TextOverflowModes.Ellipsis;
            var sLe = subGo.GetComponent<LayoutElement>() ?? subGo.AddComponent<LayoutElement>();
            sLe.preferredHeight = TextSlot(30);      // 42 — and this one IS Ellipsis
            sLe.flexibleWidth = 1;      // same reason as the title above

            // the 112×60 pill + 48 knob
            GameObject toggle = Child(row.transform, "Toggle");
            var togLe = toggle.GetComponent<LayoutElement>() ?? toggle.AddComponent<LayoutElement>();
            togLe.preferredWidth = 112;
            togLe.preferredHeight = 60;
            togLe.flexibleWidth = 0;
            // Image.Type.Simple on a FULL capsule baked at final size — memory
            // reference_fixed_size_pill_capsule_sprite. 9-slicing an r=30 stadium at 60 tall
            // collapses its own corners into an oval, which is the linter's render-health FAIL.
            Image pillImg = AddImage(toggle, Require<Sprite>(SpriteTogglePill), ToggleOn,
                                     Image.Type.Simple);
            var togBtn = toggle.GetComponent<Button>() ?? toggle.AddComponent<Button>();
            togBtn.targetGraphic = pillImg;
            togBtn.transition = Selectable.Transition.None;   // the knob IS the feedback
            StripPersistentClicks(togBtn);
            if (toggle.GetComponent<ButtonPressFeedback>() == null)
                toggle.AddComponent<ButtonPressFeedback>();

            GameObject knob = Child(toggle.transform, "Knob");
            // 54 wide because the bake carries a 3 px shadow pad on every side; the DISC is 48.
            SetRect(knob, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f),
                    new Vector2(58, 0), new Vector2(54, 54));
            AddImage(knob, Require<Sprite>(SpriteToggleKnob), Color.white, Image.Type.Simple,
                     raycast: false);

            var ctrl = row.GetComponent<LoanOffersToggle>() ?? row.AddComponent<LoanOffersToggle>();
            Wire(ctrl, "titleText", title);
            Wire(ctrl, "subtitleText", sub);
            Wire(ctrl, "toggleButton", togBtn);
            Wire(ctrl, "pillImage", pillImg);
            Wire(ctrl, "knobRect", Rect(knob));

            // SET the container's height, never increment it. SettingsMenuItem reads this at
            // Awake (submenuHeight = 0 ⇒ auto-detect from sizeDelta.y) and adds it to the row's
            // LayoutElement.preferredHeight when expanded, which is what pushes LOG OUT and CLOSE
            // down in the OUTER VerticalLayoutGroup. An increment would compound on every re-run.
            float wanted = top + RowH;
            if (!Mathf.Approximately(submenuRect.sizeDelta.y, wanted))
            {
                Log.Append("  submenu height ").Append(submenuRect.sizeDelta.y)
                   .Append(" -> ").Append(wanted)
                   .Append(" (content ").Append(contentBottom)
                   .Append(" + gap ").Append(GapY).Append(" + row ").Append(RowH).Append(")\n");
                submenuRect.sizeDelta = new Vector2(submenuRect.sizeDelta.x, wanted);
            }

            EditorUtility.SetDirty(submenu);
            Log.Append("  wired LoanOffersToggle\n");
        }

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
