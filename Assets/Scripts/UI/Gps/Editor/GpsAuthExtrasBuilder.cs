// auth_golf_profile — builder for the two post-signup auth-extras screens.
// Re-runnable; overwrites both prefabs on each run. Follows GpsProfilePackBuilder: geometry from
// the node, panel atoms from Docs/Scripts bakes, translucency solved in the baker rather than
// tinted, every string a LocalizedText key.
#nullable enable
using System.IO;
using Golfin.UI.Polish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI.Editor
{
    public static class GpsAuthExtrasBuilder
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        const string PrefabGolfProfile = "Assets/Prefabs/UI/Gps/GpsGolfProfileScreen.prefab";
        const string PrefabWelcome     = "Assets/Prefabs/UI/Gps/GpsWelcomeScreen.prefab";

        // ── Sprites ───────────────────────────────────────────────────────────
        // Both frames name `Backgrounds` variant **Splash**; matched to this plate at mean
        // |dRGB| 6.3 over the un-panelled lower two thirds of the node render (next best: 58).
        const string BgSplash = "Assets/Art/SplashScreen/Splash - Background.png";

        const string SprPanelGolf   = "Assets/Art/UI/Gps/S_AUTH_GolfProfilePanel.png";
        const string SprPanelWelc   = "Assets/Art/UI/Gps/S_AUTH_WelcomePanel.png";
        const string SprFeatureTile = "Assets/Art/UI/Gps/S_AUTH_FeatureTile.png";
        const string SprInputBox    = "Assets/Art/UI/Gps/S_AUTH_InputBox.png";
        const string SprChipOff     = "Assets/Art/UI/Gps/S_AUTH_ChipOff.png";
        const string SprChipOn      = "Assets/Art/UI/Gps/S_AUTH_ChipOn.png";
        const string SprRingWelcome = "Assets/Art/UI/Gps/S_GpsIconRing_Welcome.png";
        const string SprRingFeature = "Assets/Art/UI/Gps/S_GpsIconRing_Feature.png";
        const string SprPill        = "Assets/Art/Tournaments/S_PillStadium.png";
        const string SprGold        = "Assets/Art/HomeScreen/Play Button.png";   // Main Buttons Gold

        static readonly string[] SwatchOff =
        {
            "Assets/Art/UI/Gps/S_AUTH_SwatchPink_Off.png",
            "Assets/Art/UI/Gps/S_AUTH_SwatchGreen_Off.png",
            "Assets/Art/UI/Gps/S_AUTH_SwatchBlue_Off.png",
            "Assets/Art/UI/Gps/S_AUTH_SwatchGold_Off.png",
        };
        static readonly string[] SwatchOn =
        {
            "Assets/Art/UI/Gps/S_AUTH_SwatchPink_On.png",
            "Assets/Art/UI/Gps/S_AUTH_SwatchGreen_On.png",
            "Assets/Art/UI/Gps/S_AUTH_SwatchBlue_On.png",
            "Assets/Art/UI/Gps/S_AUTH_SwatchGold_On.png",
        };

        const string IcoScreenshot = "Assets/Art/UI/Gps/ICO_GpsScreenshot.png";
        const string IcoPin        = "Assets/Art/UI/Gps/ICO_GpsPin.png";
        const string IcoHeart      = "Assets/Art/UI/Gps/ICO_GpsHeart.png";
        const string IcoGift       = "Assets/Art/UI/Gps/ICO_GpsGift.png";
        const string IcoRounds     = "Assets/Art/UI/Gps/ICO_GpsRounds.png";

        // ── Fonts ─────────────────────────────────────────────────────────────
        const string FontSemi = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        // The node calls for Rubik:Medium; the project ships SemiBold + the variable face only,
        // so Medium resolves to the variable face — a known-unequal recorded in the fidelity
        // table, identical to the call GpsProfilePackBuilder makes.
        const string FontMed  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        // ── Colours (all read off the nodes 2026-09-02) ────────────────────────
        static readonly Color White     = Color.white;
        static readonly Color Gold      = GpsUiColor.Hex("#EEDC9A");
        static readonly Color Muted     = GpsUiColor.Hex("#B7C3D3");
        static readonly Color HintInk   = GpsUiColor.Hex("#7D8A99");   // node 14029:33920
        static readonly Color ChipInkOn = GpsUiColor.Hex("#2A1A00");   // node 14029:33914
        static readonly Color ButtonInk = GpsUiColor.Hex("#321506");   // Main Buttons Gold label
        static readonly Color ErrColor  = new Color(0.898f, 0.282f, 0.302f); // #E5484D
        /// <summary>The node's 35 %-white pager dot, pre-composited over the welcome panel.
        /// Sampled from the node render at 1:1 (103,137,158); see the comment at the Dots block
        /// for why this is an opaque colour and not an alpha.</summary>
        static readonly Color DotInactive = GpsUiColor.Hex("#67899E");

        const float PillBorder = 88f;   // S_PillStadium's 9-slice border; ppum = 88 / radius

        /// <summary>
        /// EVERY SemiBold run is authored as <c>node_px * SemiBoldSize</c>, not as the node's px.
        ///
        /// <para>
        /// The project's Rubik SemiBold face renders ~11 % larger than the face the Figma node
        /// draws with. Build rule 4 already encodes the correction for `Main Buttons` — the
        /// calibrated 59 in place of the node's 66 — but the same face is used for EVERY SemiBold
        /// run on these two screens, so the same correction applies to all of them. Measured on
        /// the node render vs the live capture before this constant existed:
        /// </para>
        /// <list type="table">
        ///   <item>Intro Title 36        width 1.109x  cap-height 1.120x</item>
        ///   <item>Welcome Title 40      width 1.103x  cap-height 1.107x</item>
        ///   <item>Feature Name 30       width 1.106x  cap-height 1.095x</item>
        ///   <item>Chip Label 24         width 1.101x  cap-height 1.059x</item>
        ///   <item>Main Button (at 59)   width 1.014x  cap-height 0.978x  &lt;- the calibrated one</item>
        /// </list>
        /// <para>
        /// 59/66 = 0.8939 is therefore not a new number: it is the existing constant, named, so
        /// the button's size falls out of the same expression as everything else
        /// (66 * 0.8939 = 59.0) and a future SemiBold run cannot forget it.
        /// The Medium/Regular runs are NOT scaled — they measured at cap-height ratio 1.000-1.04
        /// and need no correction; their ~5 % narrow width is the weight substitution recorded
        /// against <see cref="FontMed"/>.
        /// </para>
        /// </summary>
        const float SemiBoldSize = 59f / 66f;

        // ── Canvas geometry ───────────────────────────────────────────────────
        // ContentContainer is (96, 361, 978, 2111) on BOTH frames (14029:33632 / :33933) — note
        // the 2111 height, which is taller than the 1860 the three profile-pack screens use,
        // because these two run their primary action all the way down at the bottom of the frame.
        const float CcX = 96f, CcY = 361f, CcW = 978f, CcH = 2111f;

        // ═══════════════════════════════════════════════════════════════════════
        // Menu entry points
        // ═══════════════════════════════════════════════════════════════════════

        [MenuItem("GOLFIN/Gps/Build Golf Profile Screen", priority = 210)]
        public static void BuildGolfProfile() => BuildOne(PrefabGolfProfile, BuildGolfProfileScreen);

        [MenuItem("GOLFIN/Gps/Build Welcome Screen", priority = 211)]
        public static void BuildWelcome() => BuildOne(PrefabWelcome, BuildWelcomeScreen);

        [MenuItem("GOLFIN/Gps/Build All GPS Auth Extras", priority = 212)]
        public static void BuildAll()
        {
            BuildGolfProfile();
            BuildWelcome();
        }

        static void BuildOne(string prefabPath, System.Func<GameObject> build)
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = build();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log("[GpsAuthExtrasBuilder] Built " + prefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Golf Profile screen — Figma 14029:33628
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject BuildGolfProfileScreen()
        {
            var root = new GameObject("GpsGolfProfileScreen", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            Stretch(rt);

            var ctrl = root.AddComponent<GpsGolfProfileScreenController>();
            var so = new SerializedObject(ctrl);

            var bg = Rect("Background", rt, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgSplash, White);

            var col = (RectTransform)Rect("ContentContainer", rt, CcX, CcY, CcW, CcH).transform;

            // ── Golf Profile Panel, node 14029:33885 — CC(10, 0) 958x731 ──────
            var panel = Card("GolfProfilePanel", col, 10, 0, 958, 731, SprPanelGolf);
            var p = panel.transform;

            // Intro (14029:33886) at panel(40,30): title y=0 h43 SemiBold 36 gold; sub y=49 h28
            // Medium 24 muted. Both centred across the 878 content width.
            TMP("IntroTitle", p, 40, 30, 878, 43, "", 36f * SemiBoldSize, Gold, FontSemi,
                TextAlignmentOptions.Top, "GPS_GOLFPROF_TITLE_INTRO");
            TMP("IntroSub", p, 40, 79, 878, 28, "", 24f, Muted, FontMed,
                TextAlignmentOptions.Top, "GPS_GOLFPROF_SUB");

            // ── Avatar colour (14029:33889) at panel(40,133) ───────────────────
            // Colours row (14029:33890) at (193,0) within it -> panel(233,133), 492x120.
            // The four discs are POSITIONED BY THE CONTROLLER (LayoutSwatches): the selected slot
            // carries the 120px disc and the other three the 100px one, so the layout depends on
            // state and cannot be authored once. The authored values below are the node's own
            // rendered state (slot 1 selected) so the prefab reads correctly with no controller.
            var colours = Rect("Colours", p, 233, 133, 492, 120);
            var swatchButtons = new Button[4];
            var swatchImages  = new Image[4];
            var swatchInits   = new TextMeshProUGUI[4];
            float x = 0f;
            for (int i = 0; i < 4; i++)
            {
                bool on = i == 1;                        // node renders slot 1 (green) selected
                float size = on ? 120f : 100f;
                var slot = Rect("Colour" + i, colours.transform, x, on ? 0f : 10f, size, size);
                swatchImages[i] = Img(slot, on ? SwatchOn[i] : SwatchOff[i], White);
                swatchImages[i].raycastTarget = true;
                swatchButtons[i] = Btn(slot);
                swatchInits[i] = TMP("Initial", slot.transform, 0, on ? 30.5f : 25f,
                                     size, size * 0.5f, "",
                                     (on ? 50f : 42f) * SemiBoldSize, White, FontSemi,
                                     TextAlignmentOptions.Top);
                x += size + 24f;
            }
            // Colour label (14029:33903), centred under the row: panel(40, 265) full width.
            TMP("ColourLabel", p, 40, 265, 878, 28, "", 24f, Muted, FontMed,
                TextAlignmentOptions.Top, "GPS_GOLFPROF_COLOUR_LABEL");

            // ── Field NICKNAME (14029:33904) at panel(40,319) ──────────────────
            TMP("NicknameLabel", p, 40, 319, 878, 28, "", 24f, Muted, FontMed,
                TextAlignmentOptions.TopLeft, "GPS_GOLFPROF_NICKNAME");
            var nickname = Field("NicknameInput", p, 40, 355, "", "GPS_GOLFPROF_NICKNAME_HINT");

            // ── Experience (14029:33908) at panel(40,461) ──────────────────────
            TMP("ExperienceLabel", p, 40, 461, 878, 28, "", 24f, Muted, FontMed,
                TextAlignmentOptions.TopLeft, "GPS_GOLFPROF_EXPERIENCE");
            var chips = Rect("Chips", p, 40, 499, 878, 60);
            string[] chipKeys = { "GPS_GOLFPROF_EXP_BEGINNER", "GPS_GOLFPROF_EXP_INTERMEDIATE",
                                  "GPS_GOLFPROF_EXP_ADVANCED" };
            var chipButtons = new Button[3];
            var chipImages  = new Image[3];
            var chipLabels  = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                bool on = i == 1;                        // node renders INTERMEDIATE selected
                // Node x: 0 / 296.6667 / 593.3333, each 284.6667 wide (878 across, gap 12).
                float cx = i * (284.6667f + 12f);
                var chip = Rect("Chip" + i, chips.transform, cx, 0, 284.6667f, 60);
                chipImages[i] = Img(chip, on ? SprChipOn : SprChipOff, White,
                                    Image.Type.Sliced, 30f);
                chipImages[i].raycastTarget = true;
                chipButtons[i] = Btn(chip);
                chipLabels[i] = TMP("Label", chip.transform, 0, 16, 284.6667f, 28, "",
                                    24f * SemiBoldSize,
                                    on ? ChipInkOn : White, FontSemi, TextAlignmentOptions.Top,
                                    chipKeys[i]);
            }

            // ── Field HANDICAP (OPTIONAL) (14029:33917) at panel(40,585) ───────
            TMP("HandicapLabel", p, 40, 585, 878, 28, "", 24f, Muted, FontMed,
                TextAlignmentOptions.TopLeft, "GPS_GOLFPROF_HANDICAP");
            var handicap = Field("HandicapInput", p, 40, 621, "", "GPS_GOLFPROF_HANDICAP_HINT");

            // ── Error label ───────────────────────────────────────────────────
            // Not in the node — the node has no failure state — but SPEC §3 requires the Create
            // Username screen's duplicate-name treatment, which IS a red label. It sits in the
            // Spacer band directly under the panel (CC 10,755) so it can never push the panel's
            // own layout around, and starts INACTIVE.
            var err = TMP("ErrorLabel", col, 10, 755, 958, 40, "", 26f, ErrColor, FontMed,
                          TextAlignmentOptions.Top);
            err.gameObject.SetActive(false);

            // ── SAVE PROFILE (14029:33922) CC(10, 1896) 958x120 ────────────────
            Button save = MainButton(col, "SaveProfileButton", 10, 1896, "GPS_GOLFPROF_SAVE");

            // ── Skip row (14029:33927) CC(10, 2040); text 26 Medium muted, centred ──
            var skipRow = Rect("SkipRow", col, 10, 2040, 958, 71);
            var skipLbl = TMP("SkipLabel", skipRow.transform, 0, 0, 958, 31, "", 26f, Muted,
                              FontMed, TextAlignmentOptions.Top, "GPS_GOLFPROF_SKIP");
            skipLbl.raycastTarget = true;
            Button skip = Btn(skipRow);

            // ── Wire ──────────────────────────────────────────────────────────
            SetArray(so, "_swatchButtons",  swatchButtons);
            SetArray(so, "_swatchImages",   swatchImages);
            SetArray(so, "_swatchInitials", swatchInits);
            SetSprites(so, "_swatchOffSprites", SwatchOff);
            SetSprites(so, "_swatchOnSprites",  SwatchOn);
            Set(so, "_nicknameInput", nickname);
            Set(so, "_handicapInput", handicap);
            Set(so, "_errorLabel", err);
            SetArray(so, "_chipButtons", chipButtons);
            SetArray(so, "_chipImages",  chipImages);
            SetArray(so, "_chipLabels",  chipLabels);
            Set(so, "_chipOffSprite", AssetDatabase.LoadAssetAtPath<Sprite>(SprChipOff));
            Set(so, "_chipOnSprite",  AssetDatabase.LoadAssetAtPath<Sprite>(SprChipOn));
            Set(so, "_saveButton", save);
            Set(so, "_skipButton", skip);
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Welcome screen — Figma 14029:33929
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject BuildWelcomeScreen()
        {
            var root = new GameObject("GpsWelcomeScreen", typeof(RectTransform));
            var rt = (RectTransform)root.transform;
            Stretch(rt);

            var ctrl = root.AddComponent<GpsWelcomeScreenController>();
            var so = new SerializedObject(ctrl);

            var bg = Rect("Background", rt, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgSplash, White);

            var col = (RectTransform)Rect("ContentContainer", rt, CcX, CcY, CcW, CcH).transform;

            // ── Skip row (14029:34186) CC(10, 0) 958x31 — RIGHT aligned ───────
            var skipRow = Rect("SkipRow", col, 10, 0, 958, 31);
            var skipLbl = TMP("SkipLabel", skipRow.transform, 0, 0, 958, 31, "", 26f, Muted,
                              FontMed, TextAlignmentOptions.TopRight, "GPS_WELCOME_SKIP");
            skipLbl.raycastTarget = true;
            Button skip = Btn(skipRow);

            // ── Welcome panel (14029:34188) CC(10, 55) 958x385 ────────────────
            var panel = Card("WelcomePanel", col, 10, 55, 958, 385, SprPanelWelc);
            var p = panel.transform;

            // Icon ring (14029:34189) panel(404,36) 150x150; Rounds icon inset 38, 74x74.
            var ring = Rect("IconRing", p, 404, 36, 150, 150);
            Img(ring, SprRingWelcome, White);
            var ringIcon = Rect("RoundsIcon", ring.transform, 38, 38, 74, 74);
            Img(ringIcon, IcoRounds, White);

            TMP("WelcomeTitle", p, 40, 200, 878, 47, "", 40f * SemiBoldSize, Gold, FontSemi,
                TextAlignmentOptions.Top, "GPS_WELCOME_TITLE_HEAD");
            // Sub (14029:34197) is 860 wide and WRAPS to two lines — the only wrapping text on
            // either screen, so it is the one TMP that must not carry the NoWrap default.
            var sub = TMP("WelcomeSub", p, 49, 261, 860, 66, "", 28f, White, FontMed,
                          TextAlignmentOptions.Top, "GPS_WELCOME_SUB");
            sub.textWrappingMode = TextWrappingModes.Normal;

            // Dots (14029:34198) panel(420,341) 118x14 — DECORATIVE, one page. Flat fills, so
            // they are tinted capsules rather than a bake (Build rule 1 is about gradients).
            var dots = Rect("Dots", p, 420, 341, 118, 14);
            Img(Rect("Dot0", dots.transform, 0, 0, 40, 14), SprPill, Gold, Image.Type.Sliced, 7f);
            for (int i = 1; i < 4; i++)
            {
                // The node's `fill="white" fill-opacity="0.35"` (14029:34200-34202) — but written
                // as an OPAQUE pre-composited colour, not as alpha.
                //
                // GpsUiColor.A(White, 0.35f) is the WRONG helper here and the difference is not
                // subtle: it stays genuinely translucent, so Unity composites it in LINEAR light
                // while Figma composited in sRGB, and a WHITE overlay lands far too bright.
                // Measured on the first build: node dot (103,137,158) vs built (161,170,180) —
                // ~45 per channel, plainly visible as four pale dots instead of dim ones. The two
                // helpers and when to use each: FIGMA_SCREEN_BUILD_PLAYBOOK §3.
                //
                // A dot sits on a KNOWN backdrop (the welcome panel), so the honest authoring is
                // the composite the node actually renders. #67899E IS that composite, read off
                // the node render at 1:1 — an opaque tint of the same white capsule the active
                // pill uses, which needs no alpha at all.
                var dot = Rect("Dot" + i, dots.transform, 52 + (i - 1) * 26, 0, 14, 14);
                Img(dot, SprPill, DotInactive, Image.Type.Sliced, 7f);
            }

            // ── Feature grid (14029:34203) CC(10, 464) 958x474 ────────────────
            var grid = Rect("FeatureGrid", col, 10, 464, 958, 474);
            // name key, desc key, icon — SCREENSHOT / CHECK IN / VOTE / GIFT, in node order.
            string[,] features =
            {
                { "GPS_WELCOME_FEAT_SS",      "GPS_WELCOME_FEAT_SS_DESC",      IcoScreenshot },
                { "GPS_WELCOME_FEAT_CHECKIN", "GPS_WELCOME_FEAT_CHECKIN_DESC", IcoPin },
                { "GPS_WELCOME_FEAT_VOTE",    "GPS_WELCOME_FEAT_VOTE_DESC",    IcoHeart },
                { "GPS_WELCOME_FEAT_GIFT",    "GPS_WELCOME_FEAT_GIFT_DESC",    IcoGift },
            };
            for (int i = 0; i < 4; i++)
            {
                float tx = (i % 2) * 488f;      // 470 + 18 gap
                float ty = (i / 2) * 246f;      // 228 + 18 gap
                var tile = Card("Feature" + i, grid.transform, tx, ty, 470, 228, SprFeatureTile);
                var tRing = Rect("IconRing", tile.transform, 187, 26, 96, 96);
                Img(tRing, SprRingFeature, White);
                Img(Rect("Icon", tRing.transform, 24, 24, 48, 48), features[i, 2], White);
                TMP("Name", tile.transform, 0, 132, 470, 36, "", 30f * SemiBoldSize, White, FontSemi,
                    TextAlignmentOptions.Top, features[i, 0]);
                // Desc is w-[400px] centred inside the 470 tile -> x=35.
                TMP("Desc", tile.transform, 35, 178, 400, 26, "", 22f, Muted, FontMed,
                    TextAlignmentOptions.Top, features[i, 1]);
            }

            // ── GET STARTED (14029:34246) CC(10, 1991) 958x120 ────────────────
            Button start = MainButton(col, "GetStartedButton", 10, 1991, "GPS_WELCOME_GET_STARTED");

            Set(so, "_getStartedButton", start);
            Set(so, "_skipButton", skip);
            so.ApplyModifiedPropertiesWithoutUndo();

            return root;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Atoms
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// One 878x80 input (node 14029:33906 / :33919): the baked box, a masked text area, a
        /// placeholder and the live text, wired into a single-line <see cref="TMP_InputField"/>.
        ///
        /// Built the way ScoreUploadScreenBuilder's course-search field is (:1023-1041) — the
        /// project's only other authored TMP_InputField — so the two behave identically on
        /// mobile. The 28px horizontal padding is the node's own <c>px-[28px]</c>.
        /// </summary>
        static TMP_InputField Field(string name, Transform parent, float x, float y,
                                    string valueKey, string hintKey)
        {
            var box = Card(name, parent, x, y, 878, 80, SprInputBox);
            var input = box.AddComponent<TMP_InputField>();

            var viewport = Rect("TextArea", box.transform, 28, 0, 822, 80);
            viewport.AddComponent<RectMask2D>();

            var placeholder = TMP("Placeholder", viewport.transform, 0, 0, 822, 80, "", 30f,
                                  HintInk, FontMed, TextAlignmentOptions.MidlineLeft, hintKey);
            var text = TMP("Text", viewport.transform, 0, 0, 822, 80,
                           string.IsNullOrEmpty(valueKey) ? "" : valueKey, 30f, White, FontMed,
                           TextAlignmentOptions.MidlineLeft);

            input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.customCaretColor = true;
            input.caretColor = Gold;
            // Genuinely translucent on purpose, and correct as such: a selection highlight has to
            // let the glyphs under it read, and the node draws no selection state to match — so
            // unlike the pager dots this is NOT a case for a pre-composited colour.
            input.selectionColor = GpsUiColor.A(Gold, 0.35f);

            var img = box.GetComponent<Image>();
            img.raycastTarget = true;
            input.targetGraphic = img;
            return input;
        }

        /// <summary>
        /// A `Main Buttons / Gold` instance: the shared gold sprite, content-hugging, with a
        /// SIZE-59 SemiBold label (Build rule 4 — the node's 66 hugs ~12 % too wide because
        /// Rubik SemiBold's advances are wider than the face the node renders with).
        /// Same construction as ScoreUploadScreenBuilder.MainButton, which is the calibrated one.
        /// </summary>
        static Button MainButton(Transform parent, string name, float x, float y, string key)
        {
            var row = Rect(name + "Row", parent, x, y, 958, 120);

            var go = Rect(name, row.transform, 0, 0, 0, 120);
            var grt = (RectTransform)go.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f);
            grt.pivot = new Vector2(0.5f, 1f);
            grt.anchoredPosition = Vector2.zero;

            var img = Img(go, SprGold, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = 18f / 20f;   // -> r20, the node's radius

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            Button button = Btn(go);

            // 66 is the node's size; 66 * SemiBoldSize == 59, the calibrated value Build rule 4
            // names. Written as the expression so it stays tied to the same constant.
            var label = TMP("Label", go.transform, 0, 0, 0, 120, "", 66f * SemiBoldSize,
                            ButtonInk, FontSemi,
                            TextAlignmentOptions.Midline, key);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 120;
            le.preferredHeight = 120;
            return button;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shared helpers — same shapes as GpsProfilePackBuilder
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject Rect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            rt.localScale = Vector3.one;
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static Button Btn(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            else { img = go.AddComponent<Image>(); img.color = Color.clear; }
            var btn = go.AddComponent<Button>();
            go.AddComponent<ButtonPressFeedback>();   // Hard rule 11
            return btn;
        }

        static Image Img(GameObject go, string? spritePath, Color color,
                         Image.Type type = Image.Type.Simple, float sliceRadius = 0f)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            if (spritePath != null)
            {
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (img.sprite == null)
                    Debug.LogError("[GpsAuthExtrasBuilder] sprite not found: " + spritePath);
            }
            img.color = color; img.type = type; img.raycastTarget = false;
            if (type == Image.Type.Sliced && sliceRadius > 0f)
                img.pixelsPerUnitMultiplier = PillBorder / sliceRadius;
            return img;
        }

        static GameObject Card(string name, Transform parent, float x, float y, float w, float h,
                               string spritePath)
        {
            var go = Rect(name, parent, x, y, w, h);
            Img(go, spritePath, White, Image.Type.Simple);
            return go;
        }

        static TextMeshProUGUI TMP(string name, Transform parent, float x, float y, float w, float h,
                                   string text, float size, Color color, string fontPath,
                                   TextAlignmentOptions align, string? localizeKey = null)
        {
            var go = Rect(name, parent, x, y, w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.text = text ?? string.Empty;
            if (localizeKey != null)
            {
                var loc = go.AddComponent<LocalizedText>();
                var locSo = new SerializedObject(loc);
                locSo.FindProperty("key").stringValue = localizeKey;
                locSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return tmp;
        }

        // ── SerializedObject helpers ──────────────────────────────────────────

        static void Set(SerializedObject so, string field, UnityEngine.Object? value)
        {
            var pr = so.FindProperty(field);
            if (pr == null) { Debug.LogError("[GpsAuthExtrasBuilder] no field '" + field + "'"); return; }
            pr.objectReferenceValue = value;
        }

        static void SetArray(SerializedObject so, string field, UnityEngine.Object[] values)
        {
            var pr = so.FindProperty(field);
            if (pr == null) { Debug.LogError("[GpsAuthExtrasBuilder] no field '" + field + "'"); return; }
            pr.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                pr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void SetSprites(SerializedObject so, string field, string[] paths)
        {
            var sprites = new UnityEngine.Object[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            SetArray(so, field, sprites);
        }

        // ── Asset helpers ─────────────────────────────────────────────────────

        static void EnsureDir(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
                string name = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        /// <summary>
        /// A freshly-baked PNG imports as a TEXTURE, not a Sprite, so every sprite this builder
        /// loads would come back null on the first run after a bake — the "white box" failure.
        /// Force the import mode before anything is loaded.
        /// </summary>
        static void EnsureImport()
        {
            var paths = new System.Collections.Generic.List<string>
            {
                SprPanelGolf, SprPanelWelc, SprFeatureTile, SprInputBox, SprChipOff, SprChipOn,
                SprRingWelcome, SprRingFeature, SprPill, SprGold, BgSplash,
                IcoScreenshot, IcoPin, IcoHeart, IcoGift, IcoRounds,
            };
            paths.AddRange(SwatchOff);
            paths.AddRange(SwatchOn);

            bool dirty = false;
            foreach (var path in paths)
            {
                if (!File.Exists(path)) { Debug.LogError("[GpsAuthExtrasBuilder] missing " + path); continue; }
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(path) as TextureImporter;
                }
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                dirty = true;
            }
            if (dirty) AssetDatabase.Refresh();
        }
    }
}
