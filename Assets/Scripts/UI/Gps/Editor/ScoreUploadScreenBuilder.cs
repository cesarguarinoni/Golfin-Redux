// ─────────────────────────────────────────────────────────────────────────────
// score_upload_flow §2 — builds ScoreUploadScreen.prefab + VenuePickerModal.prefab
// from the Figma nodes, at node-exact geometry.
//
// RE-RUNNABLE AND AUTHORITATIVE. The prefabs are OUTPUT: change the node, change
// the numbers here, run the menu item again. That is the same contract as the
// other prefab builders in Assets/Scripts/UI/*/Editor, and it is what makes a
// Figma diff reviewable — the geometry is in source control as numbers, not
// buried in a 12,000-line .prefab YAML.
//
// GEOMETRY. Every rect is authored top-left-anchored so a Figma (x, y, w, h)
// transcribes verbatim: `Rect(name, parent, x, y, w, h)`. The shell canvas is
// 1170x2532 at scale 1, so Figma px ARE Unity px; only TMP font sizes convert,
// by the project's /1.2 rule (memory: feedback_shell_canvas_font_conversion).
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using Golfin.Gps.UI;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Golfin.Gps.EditorTools
{
    public static class ScoreUploadScreenBuilder
    {
        // ── assets ────────────────────────────────────────────────────────────
        const string HubPrefab      = "Assets/Prefabs/UI/Gps/GpsHubScreen.prefab";
        const string ScreenPrefab   = "Assets/Prefabs/UI/Gps/ScoreUploadScreen.prefab";
        const string ModalPrefab    = "Assets/Prefabs/UI/Gps/VenuePickerModal.prefab";

        const string SprPill        = "Assets/Art/Tournaments/S_PillStadium.png";
        const string SprGold        = "Assets/Art/HomeScreen/Play Button.png";
        const string SprSilver      = "Assets/Art/RosterScreen/ButtonCancel.png";
        const string SprRingThin    = "Assets/Art/UI/Gps/S_SU_RingThin.png";
        const string SprStepperRing = "Assets/Art/UI/Gps/S_SU_StepperRing.png";
        const string SprRingThick   = "Assets/Art/UI/Gps/S_SU_RingThick.png";
        const string SprGuide       = "Assets/Art/UI/Gps/S_SU_GuideFrame.png";
        const string SprGoldSeg     = "Assets/Art/UI/Gps/S_SU_GoldSegment.png";
        const string SprPillConfidence  = "Assets/Art/UI/Gps/S_SU_PillConfidence.png";
        const string SprPillGps         = "Assets/Art/UI/Gps/S_SU_PillGps.png";
        const string SprPillTrust       = "Assets/Art/UI/Gps/S_SU_PillTrust.png";
        const string SprPillRound       = "Assets/Art/UI/Gps/S_SU_PillRound.png";
        const string SprSegmentedTrack  = "Assets/Art/UI/Gps/S_SU_SegmentedTrack.png";

        // ── the cards, baked per node size by Docs/Scripts/make_score_upload_panels.py ──
        // Every one of these is "3px solid white border + <fill> + radius" — the recipe the nodes
        // share. They are BAKED rather than 9-sliced because a vertical gradient cannot survive
        // slicing, and every card here has a fixed node size.
        const string CardViewfinder = "Assets/Art/UI/Gps/S_SU_ViewfinderPanel.png";
        const string CardRecognition= "Assets/Art/UI/Gps/S_SU_RecognitionPanel.png";
        const string CardSummary    = "Assets/Art/UI/Gps/S_SU_SummaryPanel.png";
        const string CardHoles      = "Assets/Art/UI/Gps/S_SU_HolesPanel.png";
        const string CardLocating   = "Assets/Art/UI/Gps/S_SU_LocatingPanel.png";
        const string CardVenue      = "Assets/Art/UI/Gps/S_SU_VenueCard.png";
        const string CardFact       = "Assets/Art/UI/Gps/S_SU_FactTile.png";
        const string CardHero       = "Assets/Art/UI/Gps/S_SU_HeroGradient.png";
        const string CardCourseRow  = "Assets/Art/UI/Gps/S_SU_CourseRow.png";
        const string CardTrust      = "Assets/Art/UI/Gps/S_SU_TrustPanel.png";
        const string CardPoints     = "Assets/Art/UI/Gps/S_SU_PointsPanel.png";
        const string CardError      = "Assets/Art/UI/Gps/S_SU_ErrorStrip.png";
        const string CardShare      = "Assets/Art/UI/Gps/S_SU_ShareGradient.png";
        const string CardVote       = "Assets/Art/UI/Gps/S_SU_VotePrompt.png";
        const string CardModal      = "Assets/Art/UI/Gps/S_SU_ModalPanel.png";
        const string CardModalRow   = "Assets/Art/UI/Gps/S_SU_ModalRow.png";
        const string CardSearch     = "Assets/Art/UI/Gps/S_SU_SearchField.png";

        /// <summary>
        /// ONE BACKGROUND PER STEP, indexed by <c>Step</c>. Each frame's `Backgrounds` instance
        /// names a variant of the shared component (2080:5761 "Rankings Night", 340:2169 "Loading",
        /// 2480:5339 "Missions - New", 2092:13200 "Rankings Day", 340:2168 "Missions",
        /// 13625:37914 "Variant18"); four of the six resolved to an asset the project already has —
        /// matched by MD5 and, where Figma re-encoded, by a perceptual compare — and the two that
        /// did not were exported from the node at 1170x2532.
        /// </summary>
        static readonly string[] StepBackgrounds =
        {
            "Assets/Art/RankingsScreen/BackgroundRangkings.png",   // 1 Capture   — Rankings Night
            "Assets/Art/LoadingScreen/Loading Background.png",     // 2 Reading   — Loading
            "Assets/Art/UI/Gps/Backgrounds/BG_SU_EditScore.png",   // 3 Edit      — Missions - New
            "Assets/Art/UI/Gps/Backgrounds/BG_SU_GpsProof.png",    // 4 GPS Proof — Rankings Day
            "Assets/Resources/HoleImages/MissionsBackground.png",  // 5 Confirm   — Missions
            "Assets/Art/Shop/Background - Blurred.png",            // 6 Posted    — Variant18
        };
        const string IcoScreenshot  = "Assets/Art/UI/Gps/ICO_GpsScreenshot.png";
        const string IcoCamera      = "Assets/Art/UI/Gps/ICO_GpsCamera.png";
        const string IcoPin         = "Assets/Art/UI/Gps/ICO_GpsPin.png";
        const string IcoStar        = "Assets/Art/UI/Gps/ICO_GpsStar.png";
        const string IcoHeart       = "Assets/Art/UI/Gps/ICO_GpsHeart.png";
        const string IcoRounds      = "Assets/Art/UI/Gps/ICO_GpsRounds.png";
        const string FontSemi       = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        const string FontRegular    = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        /// <summary>
        /// Figma px → TMP fontSize. ONE TO ONE.
        ///
        /// <para>
        /// The project's usual rule is ÷1.2, and that is wrong for this screen: the canvas is
        /// 1170x2532 at scale 1 and the frames are 1170 wide, so a Figma px IS a Unity px — the
        /// geometry sweep already relies on that (33/33 rects match the node exactly). Measured on
        /// the first build, every glyph came out at 0.84x the reference: the summary "92" was 56px
        /// wide against Figma's 66, and "TOTAL" 61 against 73. 0.84 is 1/1.2.
        /// </para>
        /// </summary>
        static float F(float figmaPx) => figmaPx;

        // ── palette (Figma tokens) ────────────────────────────────────────────
        static readonly Color Gold      = Hex("#EEDC9A");
        static readonly Color GoldSoft  = Hex("#F3ECC2");
        static readonly Color Green     = Hex("#7ED488");
        static readonly Color GreenDeep = Hex("#0F3D2A");
        static readonly Color Red       = Hex("#F08080");
        static readonly Color Muted     = Hex("#B7C3D3");
        static readonly Color White     = Color.white;
        static readonly Color Navy70    = ADark("#091B33", 0.70f);
        static readonly Color Ink       = Hex("#0A0F16");
        static readonly Color PointsBg  = ADark("#3B2F0F", 0.85f);

        // Composited backdrops, MEASURED off the reference node renders — the second argument that
        // A() needs in order to solve the blend (see its note). A translucent chip's on-screen
        // colour depends on what is behind it, so each of these names the surface it sits on.
        static readonly Color BgCard    = Hex("#3A4A55");   // reading / hole-list card interior
        static readonly Color BgTrust   = Hex("#2A422C");   // Confirm step's trust panel
        static readonly Color BgStrip   = Hex("#455566");   // the step strip
        static readonly Color BgStage   = Hex("#6C7450");   // step 2's tinted reading stage
        static readonly Color BgGpsPanel= Hex("#5C748A");   // step 4's stage panel
        static readonly Color BgReadingPhoto = Hex("#256719");  // the photo under step 2's card

        static readonly Color Sep       = A(Color.white, 0.12f, BgCard);
        static readonly Color TrackBg   = A(Color.white, 0.15f, BgTrust);
        static readonly Color SegTodo   = A(Color.white, 0.25f, BgStrip);
        static readonly Color GuideIcon = Hex("#3C4A5C");
        static readonly Color ButtonInk = Hex("#321506");
        static readonly Color SilverInk = Hex("#1E293B");
        static readonly Color MintText  = Hex("#BFE8CC");
        static readonly Color MintDate  = Hex("#9FCDB0");
        static readonly Color Steel     = Hex("#818EA1");
        static readonly Color SegInk    = Hex("#2A1A00");
        static readonly Color StepperBg = A(Color.white, 0.12f, BgCard);
        static readonly Color HoleNumBg = A(Color.white, 0.15f, BgCard);

        // Circular badges. The node draws each as an OPAQUE dark disc inside a thin gold ring —
        // not as a tint of the accent colour, which is what the first build did.
        static readonly Color BadgeNavy = Hex("#112D4F");   // GPS marker disc  (measured [17 45 79])
        static readonly Color BadgeInk  = Hex("#15365B");   // Posted star disc (measured [21 54 91])
        static readonly Color BadgeRing = Hex("#B2A379");   // the ring around both

        // Half-height fills stay circles; anything else is 9-sliced through this ratio.
        const float PillBorder = 88f;   // S_PillStadium.png slice border, px

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out Color c); return c; }

        static float S2L(float c)
            => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        /// <summary>
        /// A node's sRGB alpha, converted to the alpha Unity needs to LOOK the same over
        /// <paramref name="over"/>.
        ///
        /// <para>
        /// The project renders in LINEAR colour space: Figma composites `T = a·F + (1-a)·B` on sRGB
        /// values, Unity composites the same equation on LINEAR ones. Solve the Unity alpha that
        /// lands on Figma's result — `a' = (lin(T) - lin(B)) / (lin(F) - lin(B))`, averaged over the
        /// channels that carry information (one where F and B agree constrains nothing).
        /// </para>
        /// <para>
        /// ⚠️ The answer depends on BOTH the overlay and the backdrop, which is why
        /// <paramref name="over"/> is required. <see cref="ADark"/> is the special case F≈0, where
        /// the backdrop cancels; using that formula for a LIGHT overlay — as the first build did for
        /// every white chip — inflates the alpha ~5× instead of shrinking it ~3×, and is what made
        /// the trust track, the hole badges and the steppers wash out.
        /// </para>
        /// </summary>
        static Color A(Color overlay, float srgbAlpha, Color over)
        {
            float sum = 0f; int n = 0;
            for (int c = 0; c < 3; c++)
            {
                float b = over[c], f = overlay[c];
                float lb = S2L(b), lf = S2L(f);
                if (Mathf.Abs(lf - lb) < 1e-4f) continue;
                float t = srgbAlpha * f + (1f - srgbAlpha) * b;
                sum += Mathf.Clamp01((S2L(t) - lb) / (lf - lb));
                n++;
            }
            return new Color(overlay.r, overlay.g, overlay.b, n == 0 ? srgbAlpha : sum / n);
        }

        /// <summary>
        /// The F≈0 special case of <see cref="A"/>: for a near-BLACK overlay the `a·F` term drops
        /// out and `((1-a)B)^2.2 == (1-a')B^2.2` solves to `a' = 1 - (1-a)^2.2` for any backdrop.
        /// Valid ONLY for dark overlays — see the warning on <see cref="A"/>.
        /// </summary>
        static Color ADark(Color c, float srgbAlpha)
            => new Color(c.r, c.g, c.b, 1f - Mathf.Pow(1f - srgbAlpha, 2.2f));

        static Color ADark(string hex, float srgbAlpha) => ADark(Hex(hex), srgbAlpha);

        // ═════════════════════════════════════════════════════════════════════

        [MenuItem("GOLFIN/Build/Score Upload Screen")]
        public static void Build()
        {
            EnsureSpriteImport();

            // Built in a PREVIEW scene, never in whatever the user has open: `new GameObject`
            // lands in the active scene and would leave it dirty, and a dirty scene aborts
            // `tests-run` and invites an accidental save of layout churn.
            Scene staging = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject modal = BuildVenuePicker(staging);
                GameObject screen = BuildScreen(modal);
                SceneManager.MoveGameObjectToScene(screen, staging);

                // gps_polish — the shared polish pass. Runs LAST, on the finished root, so it sees
                // every layer this builder authored (SPEC § Architecture: the additions go INTO
                // the existing builders, which stay the prefab source of truth).
                GpsPolishBuilder.Apply(screen);
                PrefabUtility.SaveAsPrefabAsset(screen, ScreenPrefab);
                Object.DestroyImmediate(screen);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(staging);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ScoreUploadScreenBuilder] built " + ScreenPrefab + " and " + ModalPrefab);
        }

        /// <summary>
        /// The five generated PNGs land as DEFAULT textures on first import, and
        /// <c>Resources/AssetDatabase.LoadAssetAtPath&lt;Sprite&gt;</c> returns null for those —
        /// the exact trap that would have shipped 20 unrenderable balls in `ball_data_wiring`.
        /// Fixed here rather than in a .meta so re-running the generator cannot undo it.
        /// </summary>
        static void EnsureSpriteImport()
        {
            string[] generated =
            {
                CardViewfinder, CardRecognition, CardSummary, CardHoles, CardLocating, CardVenue,
                CardFact, CardHero, CardCourseRow, CardTrust, CardPoints, CardError, CardShare,
                CardVote, CardModal, CardModalRow, CardSearch,
                SprRingThin, SprRingThick, SprGuide, SprGoldSeg,
                SprPillConfidence, SprPillGps, SprPillTrust, SprPillRound, SprSegmentedTrack,
                SprStepperRing,
                "Assets/Art/UI/Gps/Backgrounds/BG_SU_EditScore.png",
                "Assets/Art/UI/Gps/Backgrounds/BG_SU_GpsProof.png",
            };
            foreach (string path in generated)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) { Debug.LogError("[ScoreUploadScreenBuilder] missing " + path); continue; }
                bool needsBorder = path == SprGoldSeg &&
                                   importer.spriteBorder != new Vector4(88, 88, 88, 88);
                if (!needsBorder &&
                    importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single) continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;

                // The gold segment is the one generated sprite that is 9-SLICED. Its border must
                // match S_PillStadium's 88 so the same `ppum = 88 / radius` rule applies, and a
                // TextureImporter will not infer a border on its own.
                if (path == SprGoldSeg)
                {
                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteBorder = new Vector4(88, 88, 88, 88);
                    importer.SetTextureSettings(settings);
                }

                importer.SaveAndReimport();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Screen
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Each step's own 978x1860 content column, filled by BuildScreen.</summary>
        static readonly Transform[] StepContent = new Transform[6];

        static GameObject BuildScreen(GameObject modalPrefab)
        {
            GameObject root = new GameObject("ScoreUploadScreen", typeof(RectTransform));
            RectTransform rt = (RectTransform)root.transform;
            Stretch(rt);

            var ctrl = root.AddComponent<ScoreUploadFlowController>();

            RectTransform content = Rect("ContentContainer", rt, 96, 361, 978, 1860).GetComponent<RectTransform>();

            var so = new SerializedObject(ctrl);

            // Each step carries its OWN full-screen background — the frames do not share one, and
            // the previous build's single hub background was the most visible thing that made the
            // screens read as "not the design". The background is a child of the STEP so it appears
            // and disappears with it; it is placed first so everything else draws over it, and it
            // is anchored to the SCREEN, not the content column, since it is 1170x2532.
            var roots = new GameObject[6];
            for (int i = 0; i < 6; i++)
            {
                roots[i] = Rect("Step" + (i + 1) + "_" + StepName(i), rt, 0, 0, 1170, 2532);
                var stepRt = (RectTransform)roots[i].transform;
                Stretch(stepRt);

                GameObject bg = Rect("Background", stepRt, 0, 0, 0, 0);
                Stretch((RectTransform)bg.transform);
                Img(bg, StepBackgrounds[i], White, Image.Type.Simple);

                // The step's own content column, at the frame's Content Container box.
                GameObject col = Rect("Content", stepRt, 96, 361, 978, 1860);
                StepContent[i] = col.transform;

                roots[i].SetActive(i == 0);
            }

            BuildCapture(StepContent[0], so);
            BuildReading(StepContent[1], so);
            BuildEdit(StepContent[2], so);
            BuildGps(StepContent[3], so, modalPrefab, rt);
            BuildConfirm(StepContent[4], so);
            BuildPosted(StepContent[5], so);

            // The step roots are later siblings than ContentContainer and each carries a
            // full-screen background, so the strip was being painted OVER by whichever step was
            // active — it vanished from every frame. Move the container to the front of the draw
            // order (last sibling) BEFORE building the strip into it.
            content.SetAsLastSibling();
            BuildStepStrip(content, so);

            SetArray(so, "_stepRoots", roots);

            // The hub's own nav bar, cloned wholesale — same five slots, same sprites, same
            // geometry. Cloned rather than re-authored so a nav change lands on both screens
            // through one edit to the hub prefab plus a re-run of this builder.
            CloneNavBar(rt);

            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        static string StepName(int i)
        {
            switch (i)
            {
                case 0: return "Capture";
                case 1: return "Reading";
                case 2: return "Edit";
                case 3: return "Gps";
                case 4: return "Confirm";
                default: return "Posted";
            }
        }

        static void CloneNavBar(RectTransform parent)
        {
            GameObject hub = PrefabUtility.LoadPrefabContents(HubPrefab);
            try
            {
                Transform nav = GpsPolishBuilder.FindNavBar(hub);
                if (nav == null) { Debug.LogError("[ScoreUploadScreenBuilder] hub has no GpsNavBar"); return; }

                GameObject clone = Object.Instantiate(nav.gameObject, parent);
                clone.name = "GpsNavBar";

                // The camera slot is the one the player used to GET here, so it reads as the
                // active slot: it keeps its ring and is the only inert-looking button that is
                // deliberately non-interactable (tapping it would re-enter the screen you are on).
                Transform camera = clone.transform.Find("NavCameraButton");
                if (camera != null)
                {
                    var button = camera.GetComponent<Button>();
                    if (button != null) button.interactable = false;
                }
                // interactable=false, but NOT dimmed: Unity's default disabledColor is grey and it
                // greyed the whole nav bar, which the hub does not do. The slot has to LOOK like
                // the finished bar — same reasoning as GpsHubScreenController's inert loops.
                foreach (Button b in clone.GetComponentsInChildren<Button>(true))
                {
                    ColorBlock colors = b.colors;
                    colors.disabledColor = Color.white;
                    b.colors = colors;
                    b.interactable = false;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(hub);
            }
        }

        // ── step strip (shared, 14022:32895) ──────────────────────────────────

        static void BuildStepStrip(RectTransform content, SerializedObject so)
        {
            GameObject strip = Panel("StepStrip", content, 10, 0, 958, 94, Navy70, 28);
            Set(so, "_stepStrip", strip);

            // The left control is a real Button and is CLOSE or BACK depending on the step —
            // one widget, two labels, because they are the same affordance in the same place.
            GameObject left = Rect("StripLeftButton", strip.transform, 28, 16, 260, 40);
            Img(left, null, new Color(1, 1, 1, 0));
            Set(so, "_stripLeftButton", Btn(left));
            Set(so, "_stripLeftLabel",
                Text("StripLeftLabel", left.transform, 0, 0, 260, 40, "× CLOSE", F(28), White,
                     FontSemi, TextAlignmentOptions.MidlineLeft));

            Set(so, "_stripTitle",
                Text("StripTitle", strip.transform, 28, 16, 902, 40, "CAPTURE", F(34), Gold,
                     FontSemi, TextAlignmentOptions.Midline));
            Set(so, "_stripCounter",
                Text("StripCounter", strip.transform, 28, 16, 902, 40, "1/5", F(28), White,
                     FontSemi, TextAlignmentOptions.MidlineRight));

            GameObject segments = Rect("Segments", strip.transform, 28, 68, 902, 8);
            var segs = new Image[5];
            for (int i = 0; i < 5; i++)
            {
                GameObject seg = Rect("Seg" + (i + 1), segments.transform, i * 182.4f, 0, 172.4f, 8);
                segs[i] = Img(seg, SprPill, i == 0 ? Gold : SegTodo, Image.Type.Sliced, 4f);
            }
            SetArray(so, "_stripSegments", segs);
        }

        // ── 1 Capture (14022:32576) ───────────────────────────────────────────

        static void BuildCapture(Transform root, SerializedObject so)
        {
            GameObject panel = Card("ViewfinderPanel", root, 10, 118, 958, 1080, CardViewfinder);

            // The live feed fills the panel; the guide is what shows when there is no camera
            // (Editor, simulator, permission refused) — see ScoreUploadFlowController.StartPreview.
            GameObject preview = Rect("Preview", panel.transform, 0, 0, 958, 1080);
            var raw = preview.AddComponent<RawImage>();
            raw.color = White;
            preview.SetActive(false);
            Set(so, "_preview", raw);

            GameObject placeholder = Rect("PreviewPlaceholder", panel.transform, 0, 0, 958, 1080);
            Set(so, "_previewPlaceholder", placeholder);

            GameObject guide = Rect("ScorecardGuide", placeholder.transform, 109, 310, 740, 460);
            Img(guide, SprGuide, Gold, Image.Type.Simple);

            GameObject icon = Rect("GuideIcon", guide.transform, 310, 130, 120, 120);
            Img(icon, IcoScreenshot, GuideIcon);

            Set(so, "_captureHint",
                Text("GuideText", guide.transform, 0, 300, 740, 36, "SU_ALIGN_HINT", F(30), Muted,
                     FontRegular, TextAlignmentOptions.Midline, localizeKey: "SU_ALIGN_HINT"));

            GameObject shutterRow = Rect("ShutterRow", root, 10, 1222, 958, 262);

            // The nav bar's own camera disc at 170 — the shutter IS that affordance, so it is
            // the same sprite rather than a second drawing of the same thing.
            GameObject shutter = Rect("ShutterButton", shutterRow.transform, 394, 0, 170, 170);
            Img(shutter, "Assets/Art/UI/Gps/S_GpsNav_Camera.png", White);
            Set(so, "_shutterButton", Btn(shutter));

            GameObject sourceRow = Panel("SourceRow", shutterRow.transform, 0, 190, 958, 72, Navy70, 36);
            Set(so, "_sourceCameraButton",
                SourceItem(sourceRow.transform, "SourceCAMERA", 60, 20.5f, 149, 31, IcoCamera, "SU_SRC_CAMERA"));
            Set(so, "_sourceLibraryButton",
                SourceItem(sourceRow.transform, "SourceLIBRARY", 358, 20.5f, 151, 31, IcoScreenshot, "SU_SRC_LIBRARY"));
            Set(so, "_sourceManualButton",
                SourceItem(sourceRow.transform, "SourceMANUAL", 658, 18, 240, 36, null, "SU_SRC_MANUAL"));
        }

        static Button SourceItem(Transform parent, string name, float x, float y, float w, float h,
                                 string iconPath, string key)
        {
            GameObject go = Rect(name, parent, x, y, w, h);
            Img(go, null, new Color(1, 1, 1, 0));
            Button button = Btn(go);

            // DOCUMENTED DEVIATION: the node's third source is "✎ MANUAL ENTRY", and U+270E is in
            // neither Rubik nor the NotoSansJP fallback — it renders as a tofu box
            // (Docs/Diagnostics/_capture/score_upload/glyph_probe.png row C). No pencil sprite
            // exists in the palette either, so the label stands alone rather than shipping a box.
            float textX = iconPath != null ? 40 : 0;
            if (iconPath != null) Img(Rect("Icon", go.transform, 0, 0.5f, 30, 30), iconPath, White);

            Text("Label", go.transform, textX, 0, w - textX, h, key, F(26), White, FontRegular,
                 TextAlignmentOptions.MidlineLeft, localizeKey: key);
            return button;
        }

        // ── 2 AI Reading (14023:32666) ────────────────────────────────────────

        static void BuildReading(Transform root, SerializedObject so)
        {
            // 1139, not the node's 1045: a COURSE row is inserted under TOTAL (SPEC § Result
            // rows) because the recognition returns a course name the Figma frame has no slot for.
            Transform panel = Card("RecognitionPanel", root, 10, 118, 958, 1139, CardRecognition).transform;

            // The node rounds the stage's TOP corners into the card and leaves the BOTTOM square,
            // because the result table butts straight up against it inside the same card. A
            // 9-sliced pill rounds all four, so square the bottom back off with a plain rect.
            Color stageTint = A(Green, 0.10f, BgReadingPhoto);
            GameObject stage = Panel("ReadingState", panel, 0, 0, 958, 560, stageTint, 50);
            Img(Rect("SquareFoot", stage.transform, 0, 510, 958, 50), null, stageTint);

            GameObject stageInner = Rect("ReadingStage", stage.transform, 339, 72, 300, 300);
            Img(Rect("Halo", stageInner.transform, 0, 0, 300, 300), SprPill, A(Gold, 0.10f, BgStage));
            Img(Rect("HaloRing", stageInner.transform, 0, 0, 300, 300), SprRingThin, A(Gold, 0.35f, BgStage));

            GameObject spinner = Rect("Spinner", stageInner.transform, 40, 40, 220, 220);
            Image spinnerRing = Img(spinner, SprRingThick, Gold);
            spinnerRing.type = Image.Type.Filled;
            spinnerRing.fillMethod = Image.FillMethod.Radial360;
            spinnerRing.fillOrigin = (int)Image.Origin360.Top;
            spinnerRing.fillAmount = 0.86f;
            // Rotation pivots the WHOLE 220 square, so the pivot has to be its centre; every
            // other rect in this builder is top-left-anchored, and this is the one exception.
            var spinnerRt = (RectTransform)spinner.transform;
            Vector2 keep = spinnerRt.anchoredPosition;
            spinnerRt.pivot = new Vector2(0.5f, 0.5f);
            spinnerRt.anchoredPosition = keep + new Vector2(110, -110);
            Set(so, "_spinner", spinnerRt);
            Set(so, "_spinnerRing", spinnerRing);

            Img(Rect("StageIcon", stageInner.transform, 108, 108, 84, 84), IcoScreenshot, Gold);

            Set(so, "_readingTitle",
                Text("ReadingLabel", stage.transform, 0, 396, 958, 40, "SU_READING", F(34), Green,
                     FontSemi, TextAlignmentOptions.Midline));
            Set(so, "_readingSub",
                Text("ReadingSub", stage.transform, 0, 460, 958, 28, "SU_READING_SUB", F(24), Muted,
                     FontRegular, TextAlignmentOptions.Midline));

            Set(so, "_rowTotalValue", ResultRow(panel, "RowTOTAL", 560, 108,
                                                "SU_ROW_TOTAL", F(54), Gold, first: true));
            Set(so, "_rowCourseValue", ResultRow(panel, "RowCOURSE", 668, 94,
                                                 "SU_ROW_COURSE", F(34), White));
            Set(so, "_rowOutValue", ResultRow(panel, "RowOUT", 762, 94,
                                              "SU_ROW_OUT", F(42), White));
            Set(so, "_rowInValue", ResultRow(panel, "RowIN", 856, 94,
                                             "SU_ROW_IN", F(42), White));
            Set(so, "_rowPuttsValue", ResultRow(panel, "RowPUTTS", 950, 94,
                                                "SU_ROW_PUTTS", F(42), White));

            GameObject conf = Rect("RowCONFIDENCE", panel, 0, 1044, 958, 95);
            Img(Rect("Sep", conf.transform, 40, 0, 878, 1), SprPill, Sep, Image.Type.Sliced, 0.5f);
            Text("Key", conf.transform, 40, 25.5f, 400, 36, "SU_ROW_CONFIDENCE", F(30), Muted,
                 FontRegular, TextAlignmentOptions.MidlineLeft, localizeKey: "SU_ROW_CONFIDENCE");
            Image confPill = Pill("Pill", conf.transform, 793, 22, 125, 43, SprPillConfidence, Green);
            Set(so, "_confidencePill", confPill);
            Set(so, "_confidencePillLabel",
                Text("PillLabel", confPill.transform, 0, 0, 125, 43, "SU_CONF_HIGH", F(26), Green,
                     FontSemi, TextAlignmentOptions.Midline));

            Button gold = MainButton(root, "ConfirmScoreButton", 10, 1596, true,
                                     "SU_BTN_CONFIRM_SCORE", out TextMeshProUGUI goldLabel,
                                     localize: false);
            Set(so, "_confirmScoreButton", gold);
            Set(so, "_confirmScoreLabel", goldLabel);

            Button silver = MainButton(root, "RetakeButton", 10, 1740, false,
                                       "SU_BTN_RETAKE", out TextMeshProUGUI silverLabel,
                                       localize: false);
            Set(so, "_retakeButton", silver);
            Set(so, "_retakeLabel", silverLabel);
        }

        static TextMeshProUGUI ResultRow(Transform parent, string name, float y, float h,
                                         string keyKey, float valueSize, Color valueColor,
                                         bool first = false)
        {
            GameObject row = Rect(name, parent, 0, y, 958, h);
            // A 1px hairline between rows, not around them: the Figma rows are separated, and a
            // border on each would double up.
            if (!first) Img(Rect("Sep", row.transform, 40, 0, 878, 1), SprPill, Sep, Image.Type.Sliced, 0.5f);

            Text("Key", row.transform, 40, (h - 36) / 2f, 400, 36, keyKey, F(30), Muted, FontRegular,
                 TextAlignmentOptions.MidlineLeft, localizeKey: keyKey);

            return Text("Value", row.transform, 500, (h - 64) / 2f, 418, 64, "—", valueSize,
                        valueColor, FontSemi, TextAlignmentOptions.MidlineRight);
        }

        // ── 3 Edit Score (14024:32751 + 14035:101905) ─────────────────────────

        static void BuildEdit(Transform root, SerializedObject so)
        {
            Transform summary = Card("ScoreSummaryPanel", root, 10, 118, 958, 182, CardSummary).transform;
            GameObject stats = Rect("StatsRow", summary, 0, 0, 958, 118);

            Set(so, "_sumTotal", SummaryStat(stats.transform, "StatTOTAL", 32, "SU_SUM_TOTAL", Gold));
            Set(so, "_sumOut",   SummaryStat(stats.transform, "StatOUT", 255.5f, "SU_ROW_OUT", White));
            Set(so, "_sumIn",    SummaryStat(stats.transform, "StatIN", 479, "SU_ROW_IN", White));
            Set(so, "_sumPutts", SummaryStat(stats.transform, "StatPUTTS", 702.5f, "SU_ROW_PUTTS", White));

            GameObject toggle = Rect("HolesToggle", summary, 0, 118, 958, 64);
            GameObject segmented = Pill("Segmented", toggle.transform, 321.5f, 0, 315, 50,
                                        SprSegmentedTrack, White).gameObject;
            Set(so, "_holes18Button", ToggleSeg(segmented.transform, "Seg18", 4, 159, "SU_HOLES_18",
                                                out Image bg18, out TextMeshProUGUI lbl18));
            Set(so, "_holes18Bg", bg18);
            Set(so, "_holes18Label", lbl18);
            Set(so, "_holes9Button", ToggleSeg(segmented.transform, "Seg9", 163, 148, "SU_HOLES_9",
                                               out Image bg9, out TextMeshProUGUI lbl9));
            Set(so, "_holes9Bg", bg9);
            Set(so, "_holes9Label", lbl9);
            bg18.color = White;                          // gold gradient sprite, untinted = active
            bg9.color = new Color(1f, 1f, 1f, 0f);       // inactive half draws no fill
            lbl9.color = White;

            Transform holes = Card("HolesPanel", root, 10, 324, 958, 1193, CardHoles).transform;

            GameObject sectionOut = Rect("SectionOUT", holes, 0, 0, 958, 53);
            Text("Section", sectionOut.transform, 28, 14, 300, 33, "SU_SECTION_OUT", F(28), Gold,
                 FontSemi, TextAlignmentOptions.MidlineLeft, localizeKey: "SU_SECTION_OUT");
            Set(so, "_sectionOutTotal",
                Text("SectionTotal", sectionOut.transform, 600, 14, 330, 33, "—", F(28), Gold,
                     FontSemi, TextAlignmentOptions.MidlineRight));

            var rows = new HoleRowView[18];
            for (int i = 0; i < 9; i++) rows[i] = HoleRow(holes, i + 1, 53 + i * 60);

            GameObject sectionIn = Rect("SectionIN", holes, 0, 593, 958, 51);
            sectionIn.AddComponent<CanvasGroup>();
            Text("Section", sectionIn.transform, 28, 12, 300, 33, "SU_SECTION_IN", F(28), Gold,
                 FontSemi, TextAlignmentOptions.MidlineLeft, localizeKey: "SU_SECTION_IN");
            Set(so, "_sectionInTotal",
                Text("SectionTotal", sectionIn.transform, 600, 12, 330, 33, "—", F(28), Gold,
                     FontSemi, TextAlignmentOptions.MidlineRight));
            Set(so, "_sectionInGroup", sectionIn);

            for (int i = 9; i < 18; i++) rows[i] = HoleRow(holes, i + 1, 644 + (i - 9) * 60);
            SetArray(so, "_holeRows", rows);

            Set(so, "_totalFromHolesNote",
                Text("TotalFromHolesNote", root, 10, 1541, 958, 40, "SU_TOTAL_FROM_HOLES",
                     F(26), Muted, FontRegular, TextAlignmentOptions.Midline,
                     localizeKey: "SU_TOTAL_FROM_HOLES"));

            Set(so, "_verifyGpsButton",
                MainButton(root, "VerifyGpsButton", 10, 1740, true, "SU_BTN_VERIFY_GPS", out _));
        }

        static TextMeshProUGUI SummaryStat(Transform parent, string name, float x, string labelKey, Color valueColor)
        {
            GameObject go = Rect(name, parent, x, 14, 223.5f, 94);
            TextMeshProUGUI value = Text("Value", go.transform, 0, 0, 223.5f, 62, "—", F(52),
                                         valueColor, FontSemi, TextAlignmentOptions.Midline);
            Text("Label", go.transform, 0, 66, 223.5f, 28, labelKey, F(24), Muted, FontRegular,
                 TextAlignmentOptions.Midline, localizeKey: labelKey);
            return value;
        }

        /// <summary>
        /// One half of the 18/9 segmented control. The ACTIVE half is a gold gradient
        /// (`#f3ecc2 -> #c9a94f`) with `#2a1a00` text; the inactive half has NO fill and white
        /// text. The controller swaps `bg.color` between gold and transparent, and the label colour
        /// with it — so both are returned.
        /// </summary>
        static Button ToggleSeg(Transform parent, string name, float x, float w, string key,
                                out Image bg, out TextMeshProUGUI label)
        {
            GameObject go = Rect(name, parent, x, 4, w, 42);
            bg = Img(go, SprGoldSeg, White, Image.Type.Sliced, 21f);
            Button button = Btn(go);
            label = Text("Label", go.transform, 0, 0, w, 42, key, F(22), SegInk, FontSemi,
                         TextAlignmentOptions.Midline, localizeKey: key);
            return button;
        }

        static HoleRowView HoleRow(Transform parent, int hole, float y)
        {
            GameObject go = Rect("Hole" + hole, parent, 0, y, 958, 60);
            var view = go.AddComponent<HoleRowView>();
            var group = go.AddComponent<CanvasGroup>();

            GameObject num = Rect("HoleNum", go.transform, 28, 8, 44, 44);
            Img(num, SprPill, HoleNumBg);
            TextMeshProUGUI n = Text("N", num.transform, 0, 0, 44, 44, hole.ToString(), F(22), White,
                                     FontSemi, TextAlignmentOptions.Midline);

            TextMeshProUGUI meta = Text("Meta", go.transform, 86, 16, 642, 28, "", F(24), Muted,
                                        FontRegular, TextAlignmentOptions.MidlineLeft);

            Button minus = Stepper(go.transform, "StepperMinus", 742, "−");
            TextMeshProUGUI score = Text("Score", go.transform, 806, 8.5f, 60, 43, "–", F(36), White,
                                         FontSemi, TextAlignmentOptions.Midline);
            Button plus = Stepper(go.transform, "StepperPlus", 880, "+");

            var so = new SerializedObject(view);
            Set(so, "_number", n);
            Set(so, "_meta", meta);
            Set(so, "_score", score);
            Set(so, "_minus", minus);
            Set(so, "_plus", plus);
            Set(so, "_group", group);
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        static Button Stepper(Transform parent, string name, float x, string glyph)
        {
            // rgba(255,255,255,0.12) fill under a 2px #818ea1 rim, r25 (14035:101744).
            GameObject go = Rect(name, parent, x, 5, 50, 50);
            // The node's stepper is a rim with the row showing through, so this is ONE ring
            // sprite — the previous "Steel disc + inset tint" pair painted a solid light puck.
            Img(go, SprStepperRing, Steel);
            Button button = Btn(go);
            Text("Glyph", go.transform, 0, 0, 50, 50, glyph, F(30), White, FontSemi,
                 TextAlignmentOptions.Midline);
            return button;
        }

        // ── 4 GPS Proof (14024:33189) ─────────────────────────────────────────

        static void BuildGps(Transform root, SerializedObject so, GameObject modalPrefab, RectTransform screenRoot)
        {
            Transform panel = Card("LocatingPanel", root, 10, 118, 958, 560, CardLocating).transform;

            Image gpsPill = Pill("GpsPill", panel, 804, 28, 147, 40, SprPillGps, Green);
            Set(so, "_gpsPillBg", gpsPill);
            Set(so, "_gpsPillLabel",
                Text("PillLabel", gpsPill.transform, 0, 0, 147, 40, "SU_GPS_ON", F(24), Green, FontSemi,
                     TextAlignmentOptions.Midline));

            GameObject fix = Rect("Fix", panel, 329, 101.5f, 300, 300);
            Img(Rect("AccuracyHalo", fix.transform, 0, 0, 300, 300), SprPill, A(Green, 0.12f, BgGpsPanel));
            Img(Rect("AccuracyRing", fix.transform, 0, 0, 300, 300), SprRingThin, A(Green, 0.35f, BgGpsPanel));

            // The marker is NOT a tint of the accent: the node draws an opaque navy disc inside a
            // thin gold ring with a WHITE pin (measured disc [17 45 79], ring [178 163 121]). The
            // first build filled it deep-green with a green pin and no ring at all.
            GameObject ring = Rect("IconRing", fix.transform, 85, 85, 130, 130);
            Img(ring, SprPill, BadgeRing);
            Img(Rect("Fill", ring.transform, 5, 5, 120, 120), SprPill, BadgeNavy);
            Img(Rect("PinIcon", ring.transform, 33, 33, 64, 64), IcoPin, White);

            Set(so, "_locatingLabel",
                Text("LocatingLabel", panel, 0, 425.5f, 958, 33, "SU_LOCATING", F(28),
                     Muted, FontRegular, TextAlignmentOptions.Midline));

            GameObject found = Panel("FoundRow", root, 10, 702, 958, 64, Navy70, 24);
            Set(so, "_foundStrip", found);
            // The node draws the status dot as its own text node, but then TWO objects carry one
            // state and only one of them gets recoloured — which is how the first play-mode run
            // showed a GREEN dot next to "No golf course nearby". One label, one colour.
            Set(so, "_foundLabel",
                Text("FoundLabel", found.transform, 28, 14, 880, 36, "● ", F(30), Green,
                     FontSemi, TextAlignmentOptions.MidlineLeft));

            GameObject card = Card("VenueCard", root, 10, 790, 958, 177, CardVenue);
            Set(so, "_venueCard", card);
            Set(so, "_venueName",
                Text("VenueName", card.transform, 32, 26, 894, 47, "", F(40), Green, FontSemi,
                     TextAlignmentOptions.MidlineLeft));
            Set(so, "_venueAddress",
                Text("VenueAddress", card.transform, 32, 81, 894, 31, "", F(26), Muted, FontRegular,
                     TextAlignmentOptions.MidlineLeft));
            Set(so, "_venueWithin",
                Text("VenueWithin", card.transform, 32, 120, 894, 31, "", F(26), White, FontRegular,
                     TextAlignmentOptions.MidlineLeft));

            GameObject facts = Rect("CourseFacts", root, 10, 991, 958, 118);
            Set(so, "_factPar",   Fact(facts.transform, "FactPAR", 0, "SU_FACT_PAR"));
            Set(so, "_factYards", Fact(facts.transform, "FactYARDS", 325.333f, "SU_FACT_YARDS"));
            Set(so, "_factHoles", Fact(facts.transform, "FactHOLES", 650.667f, "SU_FACT_HOLES"));

            // "under them" in the node is off the 1860 content box once both main buttons are
            // placed, so the retry link sits directly above the pair — still the last thing
            // between the failure text and the actions, which is the point of its position.
            GameObject retry = Rect("RetryGpsButton", root, 10, 1516, 958, 56);
            Img(retry, null, new Color(1, 1, 1, 0));
            Button retryBtn = Btn(retry);
            Text("Label", retry.transform, 0, 0, 958, 56, "SU_RETRY_GPS", F(28), Gold, FontSemi,
                 TextAlignmentOptions.Midline, localizeKey: "SU_RETRY_GPS");
            retry.SetActive(false);
            Set(so, "_retryGpsButton", retryBtn);

            Set(so, "_confirmCourseButton",
                MainButton(root, "ConfirmCourseButton", 10, 1596, true,
                           "SU_BTN_CONFIRM_COURSE", out _));
            Set(so, "_chooseManuallyButton",
                MainButton(root, "ChooseManuallyButton", 10, 1740, false,
                           "SU_BTN_CHOOSE_MANUAL", out _));

            // The modal is a child of the SCREEN, not of step 4: ModalController re-parents
            // itself to the last sibling on Show, and a step root that gets deactivated
            // underneath it would take the modal down with it.
            GameObject modal = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, screenRoot);
            modal.name = "VenuePickerModal";
            Set(so, "_venuePicker", modal.GetComponent<VenuePickerModalController>());
        }

        static TextMeshProUGUI Fact(Transform parent, string name, float x, string labelKey)
        {
            GameObject go = Card(name, parent, x, 0, 307.333f, 118, CardFact);
            Text("Label", go.transform, 0, 18, 307.333f, 26, labelKey, F(22), Muted, FontRegular,
                 TextAlignmentOptions.Midline, localizeKey: labelKey);
            return Text("Value", go.transform, 0, 48, 307.333f, 52, "—", F(44), White, FontSemi,
                        TextAlignmentOptions.Midline);
        }

        // ── 5 Confirm (14024:101470) ──────────────────────────────────────────

        static void BuildConfirm(Transform root, SerializedObject so)
        {
            GameObject hero = Card("ScoreHero", root, 10, 118, 958, 386, CardHero);

            Set(so, "_heroScore",
                Text("BigScore", hero.transform, 0, 36, 958, 166, "—", F(140), White, FontSemi,
                     TextAlignmentOptions.Midline));
            Set(so, "_heroVsPar",
                Text("VsPar", hero.transform, 0, 208, 958, 40, "", F(34), MintText, FontRegular,
                     TextAlignmentOptions.Midline));

            GameObject stats = Rect("StatsRow", hero.transform, 32, 254, 894, 102);
            Set(so, "_heroOut",   HeroStat(stats.transform, "StatOUT", 60, "SU_ROW_OUT"));
            Set(so, "_heroIn",    HeroStat(stats.transform, "StatIN", 318, "SU_ROW_IN"));
            Set(so, "_heroPutts", HeroStat(stats.transform, "StatPUTTS", 576, "SU_ROW_PUTTS"));

            GameObject courseRow = Card("CourseRow", root, 10, 528, 958, 110, CardCourseRow);
            Img(Rect("RoundsIcon", courseRow.transform, 32, 35, 40, 40), IcoRounds, Gold);
            Set(so, "_confirmCourseName",
                Text("Course", courseRow.transform, 88, 20, 838, 38, "", F(32), White, FontSemi,
                     TextAlignmentOptions.MidlineLeft));
            Set(so, "_confirmDate",
                Text("Date", courseRow.transform, 88, 62, 838, 28, "", F(24), Muted, FontRegular,
                     TextAlignmentOptions.MidlineLeft));

            Transform trust = Card("TrustPanel", root, 10, 662, 958, 267, CardTrust).transform;
            Text("TrustTitle", trust, 32, 24, 500, 36, "SU_TRUST_LEVEL", F(30), Gold,
                 FontSemi, TextAlignmentOptions.MidlineLeft, localizeKey: "SU_TRUST_LEVEL");
            Set(so, "_trustPercent",
                Text("TrustPct", trust, 500, 22, 426, 40, "0%", F(34), Green, FontSemi,
                     TextAlignmentOptions.MidlineRight));

            GameObject track = Rect("Track", trust, 32, 72, 894, 16);
            Img(track, SprPill, TrackBg, Image.Type.Sliced, 8f);
            // Driven by WIDTH, not fillAmount. Image.Type.Filled discards 9-slicing outright: it
            // squashes the whole 176px capsule into a 16px-tall rect and then clips, so the left
            // cap arrives as a thin wedge instead of a round end. Anchored left so growing the
            // rect keeps BOTH caps round, which is what the node draws.
            GameObject fill = Rect("Fill", track.transform, 0, 0, 894, 16);
            var fillRt = (RectTransform)fill.transform;
            fillRt.anchorMin = new Vector2(0f, 1f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 1f);
            fillRt.anchoredPosition = Vector2.zero;
            fillRt.sizeDelta = new Vector2(0f, 16f);
            Image fillImg = Img(fill, SprPill, Green, Image.Type.Sliced, 8f);
            Set(so, "_trustFill", fillImg);
            Set(so, "_trustFillTrack", track);

            Set(so, "_chkScreenshot", Check(trust, "Check0", 100));
            Set(so, "_chkGps",        Check(trust, "Check1", 151));
            Set(so, "_chkFriend",     Check(trust, "Check2", 202));

            GameObject points = Card("PointsPanel", root, 10, 953, 958, 96, CardPoints);
            Img(Rect("StarIcon", points.transform, 32, 32, 32, 32), IcoStar, Gold);
            Text("PointsLabel", points.transform, 76, 30, 400, 36, "SU_POINTS_EARNED", F(30), Gold,
                 FontSemi, TextAlignmentOptions.MidlineLeft, localizeKey: "SU_POINTS_EARNED");
            Set(so, "_pointsValue",
                Text("PointsValue", points.transform, 500, 22, 426, 52, "+0 pts", F(44), Gold,
                     FontSemi, TextAlignmentOptions.MidlineRight));

            GameObject error = Card("PostErrorStrip", root, 10, 1596, 958, 120, CardError);
            Set(so, "_postErrorStrip", error);
            Set(so, "_postErrorLabel",
                Text("PostErrorLabel", error.transform, 24, 0, 910, 120, "", F(26), Red, FontRegular,
                     TextAlignmentOptions.Midline));
            error.SetActive(false);

            Set(so, "_postScoreButton",
                MainButton(root, "PostScoreButton", 10, 1740, true, "SU_BTN_POST_SCORE", out _));
        }

        static TextMeshProUGUI HeroStat(Transform parent, string name, float x, string labelKey)
        {
            GameObject go = Rect(name, parent, x, 20, 258, 82);
            TextMeshProUGUI value = Text("Value", go.transform, 0, 0, 258, 52, "—", F(44), White,
                                         FontSemi, TextAlignmentOptions.Midline);
            Text("Label", go.transform, 0, 56, 258, 26, labelKey, F(22), Muted, FontRegular,
                 TextAlignmentOptions.Midline, localizeKey: labelKey);
            return value;
        }

        static TextMeshProUGUI Check(Transform parent, string name, float y)
            => Text(name, parent, 32, y + 10, 894, 31, "", F(26), Muted, FontRegular,
                    TextAlignmentOptions.MidlineLeft);

        // ── 6 Posted (14024:101792) ───────────────────────────────────────────

        static void BuildPosted(Transform root, SerializedObject so)
        {
            GameObject success = Panel("SuccessBlock", root, 10, 0, 958, 320, Navy70, 40);   // flat, no border (14024:102049)
            GameObject ring = Rect("SuccessRing", success.transform, 404, 24, 150, 150);
            // Same construction as the GPS marker: an opaque gold disc with the navy inset over
            // it, which gives the node's ~6px ring. SprRingThin drew a 1px hairline instead.
            Img(ring, SprPill, BadgeRing);
            Img(Rect("Fill", ring.transform, 6, 6, 138, 138), SprPill, BadgeInk);
            Img(Rect("StarIcon", ring.transform, 39, 39, 72, 72), IcoStar, Gold);

            Text("SuccessTitle", success.transform, 0, 186, 958, 62, "SU_POSTED", F(52), Gold,
                 FontSemi, TextAlignmentOptions.Midline, localizeKey: "SU_POSTED");
            Set(so, "_postedPoints",
                Text("SuccessSub", success.transform, 0, 260, 958, 36, "", F(30), Green, FontRegular,
                     TextAlignmentOptions.Midline));

            GameObject card = Card("ShareCard", root, 109, 344, 760, 417, CardShare);

            GameObject header = Rect("CardHeader", card.transform, 32, 28, 696, 38);
            Text("Brand", header.transform, 177.5f, 2.5f, 165, 33, "SU_SHARE_BRAND", F(28), Gold,
                 FontSemi, TextAlignmentOptions.Midline, localizeKey: "SU_SHARE_BRAND");
            Image trustPill = Pill("TrustPill", header.transform, 362.5f, 0, 156, 38, SprPillTrust, Green);
            Set(so, "_shareTrust",
                Text("PillLabel", trustPill.transform, 0, 0, 156, 38, "", F(22), Green, FontSemi,
                     TextAlignmentOptions.Midline));

            Set(so, "_shareCourse",
                Text("CardCourse", card.transform, 0, 72, 760, 33, "", F(28), MintText, FontRegular,
                     TextAlignmentOptions.Midline));
            Set(so, "_shareScore",
                Text("CardScore", card.transform, 0, 111, 760, 156, "—", F(132), White, FontSemi,
                     TextAlignmentOptions.Midline));
            Set(so, "_shareVsPar",
                Text("CardPar", card.transform, 0, 273, 760, 36, "", F(30), MintText,
                     FontRegular, TextAlignmentOptions.Midline));
            Set(so, "_shareDate",
                Text("CardDate", card.transform, 0, 315, 760, 28, "", F(24), MintDate,
                     FontRegular, TextAlignmentOptions.Midline));

            Image roundPill = Pill("RoundPill", card.transform, 284, 349, 192, 40, SprPillRound, Gold);
            Set(so, "_shareRoundPill", roundPill.gameObject);
            Set(so, "_shareRound",
                Text("PillLabel", roundPill.transform, 0, 0, 192, 40, "", F(24), Gold, FontSemi,
                     TextAlignmentOptions.Midline));
            roundPill.gameObject.SetActive(false);

            GameObject vote = Card("VotePrompt", root, 10, 785, 958, 197, CardVote);
            Set(so, "_votePanel", vote);
            Img(Rect("HeartIcon", vote.transform, 32, 23, 30, 30), IcoHeart, Hex("#F07F9C"));
            Text("VoteTitle", vote.transform, 74, 22, 600, 33, "SU_VOTE_PROMPT", F(28), Muted,
                 FontRegular, TextAlignmentOptions.MidlineLeft, localizeKey: "SU_VOTE_PROMPT");
            Text("VoteQuestion", vote.transform, 32, 69, 894, 38, "SU_VOTE_QUESTION_DEFAULT", F(32),
                 White, FontSemi, TextAlignmentOptions.MidlineLeft,
                 localizeKey: "SU_VOTE_QUESTION_DEFAULT");

            GameObject voteButtons = Rect("VoteButtons", vote.transform, 32, 121, 894, 54);
            Set(so, "_createVoteButton",
                SmallButton(voteButtons.transform, "CreateVoteButton", 0, 439, true, "SU_BTN_CREATE_VOTE"));
            Set(so, "_voteSkipButton",
                SmallButton(voteButtons.transform, "SkipButton", 455, 439, false, "SU_BTN_SKIP"));

            GameObject share = Panel("ShareBlock", root, 10, 1006, 958, 215, Navy70, 28);
            Text("ShareLabel", share.transform, 0, 18, 958, 31, "SU_SHARE_TO", F(26), Muted,
                 FontRegular, TextAlignmentOptions.Midline, localizeKey: "SU_SHARE_TO");

            GameObject shareRow = Rect("ShareRow", share.transform, 226.5f, 65, 505, 130);
            var buttons = new List<Button>();
            buttons.Add(ShareItem(shareRow.transform, "ShareInstagram", 0, 109, "◎", "Instagram", null, Hex("#B0348F")));
            buttons.Add(ShareItem(shareRow.transform, "ShareX", 145, 96, "X", "X", null, Hex("#191919")));
            buttons.Add(ShareItem(shareRow.transform, "ShareTikTok", 277, 96, "♪", "TikTok", null, Hex("#090909")));
            // ⛓ is tofu in both fonts (glyph probe row D); ∞ is the nearest shipped glyph that
            // still reads as two interlocking links.
            buttons.Add(ShareItem(shareRow.transform, "ShareCopy", 409, 96, "∞", null, "SU_SHARE_COPY", Hex("#3A4858")));
            SetArray(so, "_shareButtons", buttons.ToArray());
            SetStringArray(so, "_shareNames", new[] { "Instagram", "X", "TikTok", "Copy link" });

            Set(so, "_backHomeButton",
                MainButton(root, "BackHomeButton", 10, 1740, true, "SU_BTN_BACK_HOME", out _));
        }

        /// <summary>
        /// One share target. <paramref name="disc"/> is the brand colour of the node's circle,
        /// sampled off the reference render rather than guessed — Instagram #B0348F, X #191919,
        /// TikTok #090909, Copy link #3A4858. The first build drew all four as the same neutral
        /// translucent disc, which is the one thing that made the row read as a placeholder.
        /// </summary>
        static Button ShareItem(Transform parent, string name, float x, float w, string glyph,
                                string literalName, string nameKey, Color disc)
        {
            GameObject go = Rect(name, parent, x, 0, w, 130);
            Img(go, null, new Color(1, 1, 1, 0));
            Button button = Btn(go);

            // Every share disc is ringed in gold on the node; the first build drew bare discs.
            GameObject icon = Rect("ShareIcon", go.transform, (w - 96) / 2f, 0, 96, 96);
            Img(icon, SprPill, BadgeRing);
            Img(Rect("Fill", icon.transform, 2, 2, 92, 92), SprPill, disc);
            Text("Glyph", icon.transform, 0, 0, 96, 96, glyph, F(44), White, FontSemi,
                 TextAlignmentOptions.Midline);

            Text("ShareName", go.transform, 0, 104, w, 26, nameKey ?? literalName, F(22), White,
                 FontSemi, TextAlignmentOptions.Midline, localizeKey: nameKey);
            return button;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Venue picker modal
        // ═════════════════════════════════════════════════════════════════════

        static GameObject BuildVenuePicker(Scene staging)
        {
            GameObject root = new GameObject("VenuePickerModal", typeof(RectTransform));
            Stretch((RectTransform)root.transform);
            SceneManager.MoveGameObjectToScene(root, staging);
            var ctrl = root.AddComponent<VenuePickerModalController>();

            GameObject backdrop = Rect("Backdrop", root.transform, 0, 0, 0, 0);
            Stretch((RectTransform)backdrop.transform);
            Img(backdrop, null, ADark(Color.black, 0.6f));

            GameObject panel = Card("ModalPanel", root.transform, 96, 500, 978, 1400, CardModal);
            Transform panelBody = panel.transform;

            Text("Title", panelBody, 0, 40, 978, 60, "SU_PICK_COURSE", F(40), Gold, FontSemi,
                 TextAlignmentOptions.Midline, localizeKey: "SU_PICK_COURSE");

            GameObject searchGo = Card("SearchField", panelBody, 40, 130, 898, 90, CardSearch);
            var input = searchGo.AddComponent<TMP_InputField>();
            GameObject viewport = Rect("TextArea", searchGo.transform, 24, 0, 850, 90);
            var mask = viewport.AddComponent<RectMask2D>();
            TextMeshProUGUI placeholder = Text("Placeholder", viewport.transform, 0, 0, 850, 90,
                                               "SU_SEARCH_COURSE", F(28), Muted, FontRegular,
                                               TextAlignmentOptions.MidlineLeft,
                                               localizeKey: "SU_SEARCH_COURSE");
            TextMeshProUGUI text = Text("Text", viewport.transform, 0, 0, 850, 90, "", F(28), White,
                                        FontRegular, TextAlignmentOptions.MidlineLeft);
            input.textViewport = (RectTransform)viewport.transform;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = TMP_InputField.LineType.SingleLine;

            GameObject scroll = Rect("List", panelBody, 40, 250, 898, 1000);
            var scrollRect = scroll.AddComponent<ScrollRect>();
            scroll.AddComponent<RectMask2D>();
            GameObject rows = Rect("Rows", scroll.transform, 0, 0, 898, 0);
            var layout = rows.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            var fitter = rows.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = (RectTransform)rows.transform;
            scrollRect.viewport = (RectTransform)scroll.transform;
            scrollRect.horizontal = false;

            GameObject template = Card("VenueRowTemplate", rows.transform, 0, 0, 898, 96, CardModalRow);
            Btn(template);
            var templateLayout = template.AddComponent<LayoutElement>();
            templateLayout.minHeight = 96;
            templateLayout.preferredHeight = 96;
            Text("Label", template.transform, 28, 0, 842, 96, "", F(30), White, FontRegular,
                 TextAlignmentOptions.MidlineLeft);
            template.SetActive(false);

            TextMeshProUGUI status = Text("Status", panelBody, 40, 700, 898, 60, "", F(28),
                                          Muted, FontRegular, TextAlignmentOptions.Midline);

            Button skip = MainButton(panelBody, "SkipButton", 40, 1270, false, "SU_NO_COURSE", out _);
            ((RectTransform)skip.transform).sizeDelta = new Vector2(898, 100);

            var so = new SerializedObject(ctrl);
            Set(so, "modalPanel", panel);
            Set(so, "backdrop", backdrop);
            Set(so, "_search", input);
            Set(so, "_rowsParent", (RectTransform)rows.transform);
            Set(so, "_rowTemplate", template);
            Set(so, "_statusLabel", status);
            Set(so, "_skipButton", skip);
            so.ApplyModifiedPropertiesWithoutUndo();

            // ModalController.Awake force-deactivates both, and authoring them ACTIVE throws a
            // UIParticle MissingReferenceException on every play-mode entry
            // (memory: reference_modal_children_author_inactive).
            panel.SetActive(false);
            backdrop.SetActive(false);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, ModalPrefab);
            Object.DestroyImmediate(root);
            return asset;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Primitives
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Top-left anchored + top-left pivot, so a Figma (x, y) transcribes verbatim.</summary>
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
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Button + the project's mandatory press feedback, and the raycast target Img
        /// deliberately leaves off (Rule 11: every new player-facing Button gets ButtonPressFeedback).</summary>
        static Button Btn(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            go.AddComponent<ButtonPressFeedback>();
            return button;
        }

        static Image Img(GameObject go, string spritePath, Color color,
                         Image.Type type = Image.Type.Simple, float sliceRadius = 0f)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = spritePath == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (spritePath != null && img.sprite == null)
                Debug.LogError("[ScoreUploadScreenBuilder] sprite not found: " + spritePath);
            img.color = color;
            img.type = type;
            img.raycastTarget = false;

            // S_PillStadium's 88px slice border becomes EXACTLY `sliceRadius` UI px through the
            // multiplier — that is what turns one capsule into every corner radius in the flow.
            if (type == Image.Type.Sliced && sliceRadius > 0f)
                img.pixelsPerUnitMultiplier = PillBorder / sliceRadius;

            return img;
        }

        /// <summary>
        /// A CARD: one of the baked sprites, at its node size, untinted, `Type.Simple`.
        ///
        /// <para>
        /// These carry their own 3px white border and gradient, so unlike <see cref="Panel"/> there
        /// is nothing to tint and nothing to 9-slice — the sprite IS the node. Returned rect is the
        /// node box exactly, so children use the node's own coordinates with no inset arithmetic
        /// (which is the other thing the Next Hole Panel sprite forced, and got wrong).
        /// </para>
        /// </summary>
        static GameObject Card(string name, Transform parent, float x, float y, float w, float h,
                               string spritePath)
        {
            GameObject go = Rect(name, parent, x, y, w, h);
            Img(go, spritePath, White, Image.Type.Simple);
            return go;
        }

        /// <summary>
        /// A status pill: ONE Image on a baked sprite whose alpha carries the structure — opaque
        /// in the 1px rim, 18% inside — so a single `Image.color` paints the node's
        /// `border 1px <hue>` over `bg <hue>@0.18` together, and still recolours at runtime.
        ///
        /// <para>
        /// The first build stacked a full-size OPAQUE capsule under a 1px-inset translucent one and
        /// called the outer a "border". It is not: the opaque capsule showed through everywhere, so
        /// every pill rendered as a solid blob with its label buried under it — which is what
        /// "many of the small pills have no text" was.
        /// </para>
        /// </summary>
        static Image Pill(string name, Transform parent, float x, float y, float w, float h,
                          string spritePath, Color hue)
        {
            GameObject go = Rect(name, parent, x, y, w, h);
            return Img(go, spritePath, hue, Image.Type.Simple);
        }

        /// <summary>A flat rounded fill: S_PillStadium 9-sliced to the node's radius and tinted.</summary>
        static GameObject Panel(string name, Transform parent, float x, float y, float w, float h,
                                Color fill, float radius)
        {
            GameObject go = Rect(name, parent, x, y, w, h);
            Img(go, SprPill, fill, Image.Type.Sliced, radius);
            return go;
        }

        static TextMeshProUGUI Text(string name, Transform parent, float x, float y, float w, float h,
                                    string text, float size, Color color, string fontPath,
                                    TextAlignmentOptions align, string localizeKey = null)
        {
            GameObject go = Rect(name, parent, x, y, w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.text = text ?? string.Empty;

            // Static copy rides LocalizedText (it re-resolves on a language change by itself);
            // anything the controller formats at bind time is set imperatively instead.
            if (localizeKey != null)
            {
                var loc = go.AddComponent<LocalizedText>();
                var so = new SerializedObject(loc);
                so.FindProperty("key").stringValue = localizeKey;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return tmp;
        }

        /// <summary>
        /// A bottom button. THE BUTTON HUGS ITS LABEL — it is not a full-width bar.
        ///
        /// <para>
        /// The node's Button Container is <c>px-[48px] h-[120px] shrink-0</c> inside a centring
        /// row, i.e. its width is the label plus 48 px of padding each side. The first build made
        /// it span the whole 978 column, which is the single most obvious difference on four of the
        /// six frames. Reproduced with a HorizontalLayoutGroup + ContentSizeFitter rather than a
        /// measured constant, so a longer localized label (Japanese) still hugs correctly instead
        /// of overflowing a frozen width.
        /// </para>
        /// <para>
        /// <paramref name="localize"/> false leaves the label WITHOUT a <c>LocalizedText</c>: the
        /// AI-reading step swaps its two buttons between CONFIRM/RETAKE and RETRY/ENTER MANUALLY at
        /// runtime, and a LocalizedText on them would silently restore the authored key the next
        /// time the player changed language mid-step.
        /// </para>
        /// </summary>
        static Button MainButton(Transform parent, string name, float x, float y, bool gold,
                                 string key, out TextMeshProUGUI label, bool localize = true)
        {
            // The row is the full column; the button centres inside it and sizes to its content.
            GameObject row = Rect(name + "Row", parent, x, y, 958, 120);

            GameObject go = Rect(name, row.transform, 0, 0, 0, 120);
            var grt = (RectTransform)go.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f);
            grt.pivot = new Vector2(0.5f, 1f);
            grt.anchoredPosition = Vector2.zero;

            var img = Img(go, gold ? SprGold : SprSilver, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = gold ? 18f / 20f : 25f / 20f;   // both -> r20

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

            // 59, not the node's 66. Rubik SemiBold's advances are ~12% wider than the face the
            // node renders with, so a nominal 66 hugs to a button 10% too wide on every screen
            // (measured: 546 vs 498, 714 vs 646, 630 vs 572 — a constant 1.122x, not a padding
            // offset). Calibrated against the reference RENDER, which is ground truth for visual
            // size; the arithmetic is not.
            label = Text("Label", go.transform, 0, 0, 0, 120,
                         localize ? key : LocalizationManager.Get(key), F(59),
                         gold ? ButtonInk : SilverInk, FontSemi, TextAlignmentOptions.Midline,
                         localizeKey: localize ? key : null);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 120;
            le.preferredHeight = 120;
            return button;
        }

        static Button SmallButton(Transform parent, string name, float x, float w, bool gold, string key)
        {
            GameObject go = Rect(name, parent, x, 0, w, 54);
            var img = Img(go, gold ? SprGold : SprSilver, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = gold ? 18f / 14f : 25f / 14f;   // both → r14
            img.raycastTarget = true;

            Button button = Btn(go);
            Text("Label", go.transform, 0, 0, w, 54, key, F(39),
                 gold ? ButtonInk : SilverInk, FontSemi, TextAlignmentOptions.Midline,
                 localizeKey: key);
            return button;
        }

        // ── SerializedObject helpers ──────────────────────────────────────────

        static void Set(SerializedObject so, string field, Object value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[ScoreUploadScreenBuilder] no field '" + field + "'"); return; }
            p.objectReferenceValue = value;
        }

        static void SetArray(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[ScoreUploadScreenBuilder] no field '" + field + "'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static void SetStringArray(SerializedObject so, string field, string[] values)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[ScoreUploadScreenBuilder] no field '" + field + "'"); return; }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }
}
