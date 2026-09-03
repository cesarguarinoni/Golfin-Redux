// gps_checkin — builder for the Rounds tab (Figma 14076:33800 list / 14077:100447 active) plus
// the two modals it opens. Re-runnable; overwrites the prefabs on every run, which is what makes
// THIS FILE the source of truth for their hierarchy rather than the .prefab YAML.
//
// Shapes, helpers and calibration are lifted verbatim from GpsGiftVoteBuilder /
// GpsProfilePackBuilder / ScoreUploadScreenBuilder — same Rect() convention (top-left anchor +
// top-left pivot, so a Figma (x, y) transcribes with only a y negation), same SemiBoldSize
// correction, same "bake gradients, tint flats" rule.
//
// THE ONE STRUCTURAL DIFFERENCE FROM EVERY OTHER GPS SCREEN, and why it is not optional.
// The two frames are the SAME stack with one slot swapped: the Category Chips (60 tall) become
// the Active Round Card (340 tall), and everything below moves down by 280. Read off the node:
//
//        list                                active
//        Status Row       y 0    h 40        Status Row        y 0     h 40
//        Category Chips   y 60   h 60        Active Round Card y 60    h 340
//        Map Panel        y 140  h 560       Map Panel         y 420   h 560
//        Sort Bar         y 720  h 40        Sort Bar          y 1000  h 40
//        Spot List Panel  y 780  h 470       Spot List Panel   y 1060  h 470
//        My Recent Rounds y 1270 h 472       (absent)
//
// Every gap in both columns is exactly 20. So the container is a VerticalLayoutGroup at
// spacing 20 with `childControlHeight = false`, and the flip is one SetActive — instead of a
// second layout, or a controller that repositions five panels and can disagree with itself.
// (C4: childForceExpand is OFF, so the 20 stays 20; C3: childControl is OFF, so each panel keeps
// the exact height baked into its sprite rather than being resized by the group.)
#nullable enable
using System.Collections.Generic;
using System.IO;
using Golfin.UI.Polish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

using Golfin.Gps.EditorTools;

namespace Golfin.Gps.UI.Editor
{
    public static class GpsRoundsBuilder
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        const string HubPrefab       = "Assets/Prefabs/UI/Gps/GpsHubScreen.prefab";
        const string PrefabRounds    = "Assets/Prefabs/UI/Gps/GpsRoundsScreen.prefab";
        const string PrefabCheckIn   = "Assets/Prefabs/UI/Gps/CheckInConfirmModal.prefab";
        const string PrefabComplete  = "Assets/Prefabs/UI/Gps/RoundCompleteModal.prefab";

        // ── Background ────────────────────────────────────────────────────────
        // Matched, not chosen: the node's `Backgrounds` plate scores 0.002 mean |dRGB| against
        // this file and 34.3 against the next candidate (make_gps_rounds_panels.py header).
        const string BgRounds = "Assets/Art/HomeScreen/Home Background.png";

        // ── Baked panels (make_gps_rounds_panels.py) ──────────────────────────
        const string SprMapPanel    = "Assets/Art/UI/Gps/S_GR_MapPanel.png";
        const string SprSpotList    = "Assets/Art/UI/Gps/S_GR_SpotList.png";
        const string SprHistory     = "Assets/Art/UI/Gps/S_GR_History.png";
        const string SprActiveCard  = "Assets/Art/UI/Gps/S_GR_ActiveCard.png";
        const string SprModalPanel  = "Assets/Art/UI/Gps/S_GR_ModalPanel.png";
        const string SprModalRing   = "Assets/Art/UI/Gps/S_GR_ModalRing.png";
        const string SprMapFallback = "Assets/Art/UI/Gps/S_GPS_MapFallback.png";
        const string SprSpotDisc    = "Assets/Art/UI/Gps/S_GR_SpotDisc.png";
        const string SprSpotRing    = "Assets/Art/UI/Gps/S_GR_SpotRing.png";
        const string SprPinFill     = "Assets/Art/UI/Gps/S_GR_PinFill.png";
        const string SprPinRim      = "Assets/Art/UI/Gps/S_GR_PinRim.png";
        const string SprPlayerDot   = "Assets/Art/UI/Gps/S_GR_PlayerDot.png";
        const string SprDot18       = "Assets/Art/UI/Gps/S_GR_Dot18.png";

        // ── Reused atoms (Build rule 9 / Rule 19 provenance) ──────────────────
        const string SprPill      = "Assets/Art/Tournaments/S_PillStadium.png";  // 9-slice, border 88
        const string SprPillRing  = "Assets/Art/UI/Gps/S_GV_PillRing.png";       // 1px rim at r19
        const string SprChipRing  = "Assets/Art/UI/Gps/S_GV_ChipRing.png";       // 1px rim at r26
        const string SprGoldSeg   = "Assets/Art/UI/Gps/S_SU_GoldSegment.png";    // #f3ecc2 -> #c9a94f
        const string SprGold      = "Assets/Art/HomeScreen/Play Button.png";     // Main Buttons Gold
        const string SprSilver    = "Assets/Art/RosterScreen/ButtonCancel.png";  // Main Buttons Silver
        const string SprSeparator = "Assets/Art/UI/Gps/S_GV_Separator.png";
        const string IcoPin       = "Assets/Art/UI/Gps/ICO_GpsPin.png";
        // The node's "DISTANCE ▾" caret. It CANNOT be a character: U+25BE is absent from
        // Rubik AND from the only global fallback (NotoSansJP), so it renders as tofu —
        // and so do ▼ / › / ⌄. Only LiberationSans has any of them and it is not in the
        // chain. The existing white chevron atom is tinted instead.
        const string IcoCaret     = "Assets/Art/Original UI/Common/S_Common_Icon_ArrowBottom.png";

        // ── Fonts ─────────────────────────────────────────────────────────────
        const string FontSemi = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        const string FontMed  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        /// <summary>Every SemiBold run is authored as <c>node_px * SemiBoldSize</c> — the project's
        /// Rubik SemiBold face renders ~11 % larger than the face the node draws with, and the
        /// 59-for-66 button calibration is that same correction. Medium/Regular runs are NOT
        /// scaled: they measured at cap-height 1.00.</summary>
        const float SemiBoldSize = 59f / 66f;

        static float SB(float nodePx) => nodePx * SemiBoldSize;

        // ── Colours ───────────────────────────────────────────────────────────
        static readonly Color White      = Color.white;
        static readonly Color Gold       = Hex("#EEDC9A");
        static readonly Color GoldSoft   = Hex("#F3ECC2");
        static readonly Color Muted      = Hex("#B7C3D3");
        static readonly Color Green      = Hex("#7ED488");
        static readonly Color Registered = Hex("#7B9B8A");
        static readonly Color Food       = Hex("#F0A050");
        static readonly Color Live       = Hex("#E5484D");
        static readonly Color ChipRim    = Hex("#818EA1");
        static readonly Color ChipOnRim  = Hex("#422100");
        static readonly Color ChipOnInk  = Hex("#2A1A00");
        static readonly Color ButtonInk  = Hex("#321506");

        /// <summary>What the Rounds panels' own bodies measure on the node renders — the backdrop
        /// every translucent overlay on this screen is solved against. Read off the two reference
        /// PNGs, NOT assumed: assuming a constant navy is what made every gps_gifts_votes
        /// translucency ~50 units too dark before it was fixed.</summary>
        static readonly Color PanelBody = Hex("#2C4257");

        /// <summary>The 1px row divider — node <c>rgba(255,255,255,0.10)</c> over the panel body.</summary>
        static readonly Color RowRule = T(White, 0.10f, PanelBody);

        const float PillBorder = 88f;

        // ── Canvas geometry ───────────────────────────────────────────────────
        const float CcX = 96f, CcY = 361f, CcW = 978f, CcH = 1860f;

        /// <summary>The one gap between every panel in the stack, on BOTH frames (see the header).</summary>
        const float StackGap = 20f;

        // ═══════════════════════════════════════════════════════════════════════
        // Menu entry point
        // ═══════════════════════════════════════════════════════════════════════

        [MenuItem("GOLFIN/Gps/Build Rounds Screen", priority = 230)]
        public static void BuildRounds()
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            GameObject checkIn  = BuildCheckInModalAsset();
            GameObject complete = BuildCompleteModalAsset();

            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = BuildRoundsScreen(checkIn, complete);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                // gps_polish — the shared polish pass, LAST, on the finished root, so it sees
                // every layer this builder authored.
                GpsPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabRounds);
                Debug.Log("[GpsRoundsBuilder] Built " + PrefabRounds);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // The screen
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject BuildRoundsScreen(GameObject checkInModal, GameObject completeModal)
        {
            var root = new GameObject("GpsRoundsScreen", typeof(RectTransform));
            Stretch((RectTransform)root.transform);

            var ctrl = root.AddComponent<GpsRoundsScreenController>();
            var so = new SerializedObject(ctrl);

            var bg = Rect("Background", root.transform, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgRounds, White, Image.Type.Simple);

            var col = (RectTransform)Rect("ContentContainer", root.transform, CcX, CcY, CcW, CcH).transform;

            // The stack (see the header). childControl OFF so each panel keeps the exact height
            // its sprite was baked at; childForceExpand OFF so the 20 px gap stays 20.
            var stack = col.gameObject.AddComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(10, 10, 0, 0);
            stack.spacing = StackGap;
            stack.childAlignment = TextAnchor.UpperLeft;
            stack.childControlWidth = stack.childControlHeight = false;
            stack.childForceExpandWidth = stack.childForceExpandHeight = false;

            BuildStatusRow(col, so);
            BuildChips(col, so);
            BuildActiveCard(col, so);
            BuildMapPanel(col, so);
            BuildSortBar(col, so);
            BuildSpotList(col, so);
            BuildHistoryPanel(col, so);

            CloneNavBar((RectTransform)root.transform);

            // Both modals are children of the SCREEN and authored inactive — ModalController
            // re-parents itself to the last sibling on Show, and both of its own children must
            // start inactive or UIParticle throws on every play-mode entry
            // (reference_modal_children_author_inactive).
            var m1 = (GameObject)PrefabUtility.InstantiatePrefab(checkInModal, root.transform);
            m1.name = "CheckInConfirmModal";
            Set(so, "_confirmModal", m1.GetComponent<CheckInConfirmModalController>());

            var m2 = (GameObject)PrefabUtility.InstantiatePrefab(completeModal, root.transform);
            m2.name = "RoundCompleteModal";
            Set(so, "_completeModal", m2.GetComponent<RoundCompleteModalController>());

            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── Status Row — node 14077:33873, 958x40 ─────────────────────────────

        static void BuildStatusRow(Transform col, SerializedObject so)
        {
            GameObject row = Rect("StatusRow", col, 0, 0, 958, 40);
            Pin(row, 40);

            Set(so, "_statusLeft",
                TMP("NearbyCount", row.transform, 8, 4, 500, 33, "", 28f, Muted, FontMed,
                    TextAlignmentOptions.TopLeft));

            // The pill is fill + rim as TWO images, because the controller re-tints them
            // independently: the fill goes to accent@0.16 and the rim to the accent itself, and
            // one tinted sprite cannot carry two alphas.
            GameObject pill = Rect("GpsStatusPill", row.transform, 778, 0, 180, 40);
            var fill = Img(pill, SprPill, T(Green, 0.16f, PanelBody), Image.Type.Sliced, 20f);
            Set(so, "_statusPillFill", fill);

            GameObject rim = Rect("Rim", pill.transform, 0, 0, 180, 40);
            Stretch((RectTransform)rim.transform);
            Set(so, "_statusPillStroke", Img(rim, SprPillRing, Green, Image.Type.Sliced, 20f));

            Set(so, "_statusPillLabel",
                TMP("Status", pill.transform, 0, 6, 180, 28, "", SB(24), Green, FontSemi,
                    TextAlignmentOptions.Top));
        }

        // ── Category Chips — node 14077:33877, 958x60, three 311.33 chips gap 12 ─

        static void BuildChips(Transform col, SerializedObject so)
        {
            GameObject row = Rect("CategoryChips", col, 0, 0, 958, 60);
            Pin(row, 60);
            Set(so, "_chipsRow", row);

            string[] keys = { "GPS_ROUNDS_CAT_GOLF", "GPS_ROUNDS_CAT_RANGE", "GPS_ROUNDS_CAT_FOOD" };
            var buttons = new List<Button>();
            var fills = new List<Image>();
            var labels = new List<TextMeshProUGUI>();

            for (int i = 0; i < 3; i++)
            {
                float x = i * (311.3333f + 12f);
                GameObject chip = Rect("Chip" + i, row.transform, x, 0, 311.3333f, 60);

                // The FILL sprite is swapped at runtime between the gold segment and the dark
                // capsule, so it is the chip's own Image and the rim rides a child.
                Image fillImg = Img(chip, i == 0 ? SprGoldSeg : SprPill,
                                    i == 0 ? White : ADark(Color.black, 0.35f),
                                    Image.Type.Sliced, 30f);
                fillImg.raycastTarget = true;
                fills.Add(fillImg);

                GameObject rim = Rect("Rim", chip.transform, 0, 0, 311.3333f, 60);
                Stretch((RectTransform)rim.transform);
                Img(rim, SprChipRing, i == 0 ? ChipOnRim : ChipRim, Image.Type.Sliced, 30f);

                buttons.Add(Btn(chip));
                labels.Add(TMP("Label", chip.transform, 0, 16, 311.3333f, 28, "", SB(24),
                               i == 0 ? ChipOnInk : White, FontSemi,
                               TextAlignmentOptions.Top, keys[i]));
            }

            SetArray(so, "_chipButtons", buttons);
            SetArray(so, "_chipFills", fills);
            SetArray(so, "_chipLabels", labels);
            Set(so, "_chipSelectedSprite", Sprite(SprGoldSeg));
            Set(so, "_chipUnselectedSprite", Sprite(SprPill));
        }

        // ── Active Round Card — node 14077:100661, 958x340, gold stroke ────────

        static void BuildActiveCard(Transform col, SerializedObject so)
        {
            GameObject card = Card("ActiveRoundCard", col, 0, 0, 958, 340, SprActiveCard);
            Pin(card, 340);
            Set(so, "_activeCard", card);

            // LIVE ROUND pill — node 14077:100704, 150x40 r100 #e5484d@0.9
            GameObject live = Rect("LivePill", card.transform, 32, 24, 150, 40);
            Img(live, SprPill, new Color(Live.r, Live.g, Live.b, 0.9f), Image.Type.Sliced, 20f);
            TMP("Label", live.transform, 0, 7, 150, 28, "", SB(22), White, FontSemi,
                TextAlignmentOptions.Top, "GPS_ROUNDS_LIVE_ROUND");

            Set(so, "_cardSince",
                TMP("Since", card.transform, 592, 30, 334, 28, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.TopRight));
            Set(so, "_cardVenue",
                TMP("Venue", card.transform, 32, 78, 700, 47, "", SB(40), Gold, FontSemi,
                    TextAlignmentOptions.TopLeft));
            Set(so, "_cardVenueSub",
                TMP("VenueSub", card.transform, 32, 130, 700, 28, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.TopLeft));

            var line = Rect("Line", card.transform, 32, 172, 894, 2);
            Img(line, SprSeparator, White);

            // Four stats, 894/4 = 223.5 each (node 14077:100710..100719).
            string[] labelKeys =
            {
                "GPS_ROUNDS_ELAPSED", "GPS_ROUNDS_PTS_EARNED",
                "GPS_ROUNDS_GPS", "GPS_ROUNDS_GPS_FIXES",
            };
            Color[] valueColours = { White, Gold, Green, White };
            string[] fields = { "_cardElapsed", "_cardPts", "_cardGps", "_cardFixes" };

            for (int i = 0; i < 4; i++)
            {
                GameObject stat = Rect("Stat" + i, card.transform, 32 + i * 223.5f, 186, 223.5f, 80);
                Set(so, fields[i],
                    TMP("Value", stat.transform, 0, 0, 223.5f, 48, "—", SB(40), valueColours[i],
                        FontSemi, TextAlignmentOptions.Top));
                TMP("Label", stat.transform, 0, 52, 223.5f, 26, "", 22f, Muted, FontMed,
                    TextAlignmentOptions.Top, labelKeys[i]);
            }

            Set(so, "_scoreUploadButton",
                WideButton(card.transform, "ScoreUploadButton", 32, 268, 430, true,
                           "GPS_ROUNDS_SCORE_UPLOAD"));
            Set(so, "_checkOutButton",
                WideButton(card.transform, "CheckOutButton", 496, 268, 430, true,
                           "GPS_ROUNDS_CHECK_OUT"));

            // Authored INACTIVE: the list state is what a player without a round sees, and it is
            // the state the screen opens in far more often.
            card.SetActive(false);
        }

        // ── Map Panel — node 14077:33884, 958x560 ─────────────────────────────

        static void BuildMapPanel(Transform col, SerializedObject so)
        {
            GameObject panel = Card("MapPanel", col, 0, 0, 958, 560, SprMapPanel);
            Pin(panel, 560);

            GameObject surface = Rect("MapSurface", panel.transform, 20, 20, 918, 420);
            var mask = surface.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            // The LIVE tile. A RawImage, not an Image: the texture arrives from
            // UnityWebRequestTexture as a Texture2D and wrapping it in a Sprite every fetch would
            // allocate one per pan.
            GameObject live = Rect("MapTile", surface.transform, 0, 0, 918, 420);
            Stretch((RectTransform)live.transform);
            var raw = live.AddComponent<RawImage>();
            raw.color = White;
            raw.raycastTarget = false;
            Set(so, "_mapSurface", raw);
            live.SetActive(false);          // nothing to draw until the first fetch answers

            // The stylised fallback sits UNDER the live tile and is visible until one arrives,
            // so the panel is never an empty hole (§C4).
            GameObject fallback = Rect("MapFallback", surface.transform, 0, 0, 918, 420);
            Stretch((RectTransform)fallback.transform);
            Set(so, "_mapFallback", Img(fallback, SprMapFallback, White, Image.Type.Simple));
            fallback.transform.SetAsFirstSibling();

            // Pins and the player dot ride a layer whose CENTRE is the tile's centre, because
            // MapProjection.Offset returns an offset from the centre.
            GameObject pins = Rect("PinLayer", surface.transform, 0, 0, 918, 420);
            var prt = (RectTransform)pins.transform;
            Stretch(prt);
            Set(so, "_pinLayer", prt);

            Set(so, "_pinTemplate", PinTemplate(pins.transform));

            GameObject dot = Centred("PlayerDot", pins.transform, 60, 60);
            Img(dot, SprPlayerDot, White);
            Set(so, "_playerDot", (RectTransform)dot.transform);
            dot.SetActive(false);

            // Recenter — node 14077:33948, 140x44 r100, inset 16 from the surface's top-right.
            GameObject re = Rect("Recenter", surface.transform, 762, 16, 140, 44);
            Img(re, SprPill, ADark(Color.black, 0.45f), Image.Type.Sliced, 22f);
            GameObject reRim = Rect("Rim", re.transform, 0, 0, 140, 44);
            Stretch((RectTransform)reRim.transform);
            Img(reRim, SprPillRing, ChipRim, Image.Type.Sliced, 22f);
            Set(so, "_recenterButton", Btn(re));
            TMP("Label", re.transform, 0, 9, 140, 26, "", SB(22), White, FontSemi,
                TextAlignmentOptions.Top, "GPS_ROUNDS_NEAR_ME");

            // Legend — node 14077:33950, at y 420 inside the 20px-padded panel.
            GameObject legend = Rect("Legend", panel.transform, 20, 440, 918, 40);
            float[] dotX = { 24, 202, 418 };
            float[] textX = { 52, 230, 446 };
            float[] textW = { 150, 188, 206 };
            Color[] dotC = { Green, Registered, Food };
            string[] legendKeys =
            {
                "GPS_ROUNDS_LEGEND_PARTNER", "GPS_ROUNDS_LEGEND_REGISTERED",
                "GPS_ROUNDS_LEGEND_FOOD",
            };
            for (int i = 0; i < 3; i++)
            {
                GameObject d = Rect("Dot" + i, legend.transform, dotX[i], 11, 18, 18);
                Img(d, SprDot18, dotC[i]);
                TMP("Legend" + i, legend.transform, textX[i], 6, textW[i], 28, "", 24f, Muted,
                    FontMed, TextAlignmentOptions.TopLeft, legendKeys[i]);
            }

            var attribution = TMP("Attribution", legend.transform, 660, 8, 234, 24, "", 20f,
                                  new Color(Muted.r, Muted.g, Muted.b, 0.7f), FontMed,
                                  TextAlignmentOptions.TopRight, "GPS_ROUNDS_MAP_ATTRIBUTION");
            Set(so, "_mapAttribution", attribution.gameObject);
            // Hidden until a REAL tile lands: Google's attribution over our own drawing would be
            // a false credit (§C4).
            attribution.gameObject.SetActive(false);
        }

        /// <summary>One map pin: a tintable fill disc with the white rim + centre over it.</summary>
        static GameObject PinTemplate(Transform parent)
        {
            GameObject pin = Centred("PinTemplate", parent, 44, 44);
            Img(pin, SprPinFill, Green);
            GameObject rim = Rect("Rim", pin.transform, 0, 0, 44, 44);
            Stretch((RectTransform)rim.transform);
            Img(rim, SprPinRim, White);
            pin.SetActive(false);
            return pin;
        }

        // ── Sort Bar — node 14077:33958, 958x40 ───────────────────────────────

        static void BuildSortBar(Transform col, SerializedObject so)
        {
            GameObject bar = Rect("SortBar", col, 0, 0, 958, 40);
            Pin(bar, 40);

            Set(so, "_sortLeft",
                TMP("SortLabel", bar.transform, 8, 6, 400, 28, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.TopLeft));

            GameObject toggle = Rect("SortToggle", bar.transform, 613, 0, 337, 40);
            Set(so, "_sortToggle", Btn(toggle));
            // The label stops 30px short so the caret sits after it, right-aligned as a pair.
            Set(so, "_sortToggleLabel",
                TMP("Label", toggle.transform, 0, 6, 307, 28, "", 24f, Gold, FontMed,
                    TextAlignmentOptions.TopRight));
            // SQUARE, even though the chevron is wide: the glyph is 48x28 inside a 72x72
            // canvas, so only a square rect renders it at its native 1.71 aspect. At
            // 22x14 the linter measured it 57% off — a visibly flattened chevron.
            GameObject caret = Rect("Caret", toggle.transform, 313, 9, 22, 22);
            Img(caret, IcoCaret, Gold);
        }

        // ── Spot List Panel — node 14077:33961, 958x470 ───────────────────────

        static void BuildSpotList(Transform col, SerializedObject so)
        {
            GameObject panel = Card("SpotListPanel", col, 0, 0, 958, 470, SprSpotList);
            Pin(panel, 470);
            Set(so, "_spotPanel", panel);
            Set(so, "_spotPanelGroup", panel.AddComponent<CanvasGroup>());

            Set(so, "_spotPanelTitle", PanelHeader(panel.transform, 210));

            GameObject rows = Rect("SpotRows", panel.transform, 0, 80, 958, 390);
            var views = new List<RoundSpotRowView>();
            for (int i = 0; i < 3; i++) views.Add(SpotRow(rows.transform, i));
            SetArray(so, "_spotRows", views);

            Set(so, "_spotEmpty",
                TMP("Empty", rows.transform, 32, 24, 894, 60, "", 26f, Muted, FontMed,
                    TextAlignmentOptions.Top));
        }

        /// <summary>One NEAR YOU row — node 14077:34004 / 34021 / 34037, all one template.</summary>
        static RoundSpotRowView SpotRow(Transform parent, int index)
        {
            GameObject row = Rect("SpotRow" + index, parent, 0, index * 130f, 958, 130);
            var view = row.AddComponent<RoundSpotRowView>();
            var so = new SerializedObject(view);

            if (index > 0)
            {
                var rule = Rect("RowSeparator", row.transform, 32, 0, 894, 1);
                Img(rule, null, RowRule);
            }

            // The icon: a navy disc with a TINTED ring over it (see the baker's header — the row
            // is one template and the ring colour is per category).
            GameObject icon = Rect("Icon", row.transform, 32, 25, 80, 80);
            Img(icon, SprSpotDisc, White);
            GameObject ringGo = Rect("Ring", icon.transform, 0, 0, 80, 80);
            Stretch((RectTransform)ringGo.transform);
            Set(so, "_iconRing", Img(ringGo, SprSpotRing, GoldSoft));
            GameObject glyph = Rect("PinIcon", icon.transform, 20, 20, 40, 40);
            Img(glyph, IcoPin, White);

            GameObject info = Rect("Info", row.transform, 132, 15, 540, 100);
            // The node's Info frame is `overflow-clip` (14077:34010), and that is the whole
            // mechanism: a real OSM address is far longer than the design's sample string and must
            // be CUT at 540px, not run under the button at 696.
            //
            // ⚠️ NOT TextOverflowModes.Ellipsis, which was tried first and is worse than the bug:
            // combined with the NoWrap this builder sets, it renders ZERO glyphs on the variable
            // font — the subtitle and distance lines disappeared entirely. RectMask2D clips at the
            // rect regardless of font, fallback font or wrapping mode.
            info.AddComponent<RectMask2D>();
            // ⚠️ ELLIPSIS, NOT OVERFLOW, ON ALL THREE INFO RUNS. The node's own strings are short
            // ("Kawagoe, Saitama · East 18H · PAR 72") and TMP's default here is Overflow, so with
            // REAL data — an OSM address like "日本、〒104-0051 東京都中央区佃２丁目２０−５ 月島医療
            // ステーション 4階" — the subtitle ran straight out of its 540px rect and underneath the
            // action button at x=696. The design never showed that string; the venues table does.
            // Authored at the FULL Info width; RoundSpotRowView narrows it to the node's 330 only
            // when the PARTNER tag is actually shown and needs that space.
            TextMeshProUGUI nameText =
                TMP("Name", info.transform, 0, 0, 540, 36, "", SB(30), White, FontSemi,
                    TextAlignmentOptions.TopLeft);
            Set(so, "_name", nameText);

            GameObject tag = Rect("PartnerTag", info.transform, 344, 4, 112, 30);
            Img(tag, SprPill, T(Green, 0.18f, PanelBody), Image.Type.Sliced, 15f);
            GameObject tagRim = Rect("Rim", tag.transform, 0, 0, 112, 30);
            Stretch((RectTransform)tagRim.transform);
            Img(tagRim, SprPillRing, Green, Image.Type.Sliced, 15f);
            TMP("Tag", tag.transform, 0, 3, 112, 24, "", SB(20), Green, FontSemi,
                TextAlignmentOptions.Top, "GPS_ROUNDS_PARTNER");
            Set(so, "_partnerTag", tag);

            TextMeshProUGUI subtitleText =
                TMP("Subtitle", info.transform, 0, 40, 540, 28, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.TopLeft);
            Set(so, "_subtitle", subtitleText);
            TextMeshProUGUI distanceText =
                TMP("Distance", info.transform, 0, 72, 540, 28, "", 24f, Green, FontMed,
                    TextAlignmentOptions.TopLeft);
            Set(so, "_distance", distanceText);


            Button action = SmallButton(row.transform, "ActionButton", 696, 38, 230, true, null);
            // The dark states' #818ea1 stroke. Authored ALWAYS and toggled by the view, because a
            // row is rebound between gold and dark on every fetch and adding an Image at runtime
            // would allocate per paint.
            GameObject actionRim = Rect("Rim", action.transform, 0, 0, 230, 54);
            Stretch((RectTransform)actionRim.transform);
            Set(so, "_actionRim", Img(actionRim, SprPillRing, ChipRim, Image.Type.Sliced, 20f));
            actionRim.transform.SetSiblingIndex(0);
            actionRim.SetActive(false);
            Set(so, "_actionButton", action);
            Set(so, "_actionFill", action.GetComponent<Image>());
            Set(so, "_actionLabel", action.transform.Find("Label").GetComponent<TextMeshProUGUI>());
            Set(so, "_goldSprite", Sprite(SprGold));
            Set(so, "_darkSprite", Sprite(SprPill));

            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // ── My Recent Rounds — node 14077:100404, 958x472 ─────────────────────

        static void BuildHistoryPanel(Transform col, SerializedObject so)
        {
            GameObject panel = Card("MyRecentRoundsPanel", col, 0, 0, 958, 472, SprHistory);
            Pin(panel, 472);
            Set(so, "_historyPanel", panel);

            PanelHeader(panel.transform, 421, "GPS_ROUNDS_MY_RECENT_ROUNDS");
            var seeAll = TMP("SeeAll", panel.transform, 730, 26.5f, 196, 33, "", 28f, Muted,
                             FontMed, TextAlignmentOptions.TopRight, "GPS_ROUNDS_ALL_ROUNDS");
            Set(so, "_historySeeAll", seeAll.gameObject);
            seeAll.gameObject.SetActive(false);

            GameObject rows = Rect("RoundRows", panel.transform, 0, 80, 958, 392);
            var views = new List<GpsHubRoundRow>();
            for (int i = 0; i < 3; i++) views.Add(HistoryRow(rows.transform, i));
            SetArray(so, "_historyRows", views);

            Set(so, "_historyEmpty",
                TMP("Empty", rows.transform, 32, 24, 894, 60, "", 26f, Muted, FontMed,
                    TextAlignmentOptions.Top));
        }

        /// <summary>
        /// One MY RECENT ROUNDS row — the HUB's own round-row atom, cloned from its prefab rather
        /// than re-authored (Rule 19 clone provenance).
        ///
        /// <para>The node calls this panel "the hub's Friends Rounds panel verbatim", and the hub's
        /// rows already carry <see cref="GpsHubRoundRow"/> with every field wired. Rebuilding them
        /// here would be a second implementation of one row that has to keep agreeing with the
        /// first — the exact shape Rule 19 exists to stop.</para>
        /// </summary>
        static GpsHubRoundRow HistoryRow(Transform parent, int index)
        {
            GameObject hub = File.Exists(HubPrefab) ? PrefabUtility.LoadPrefabContents(HubPrefab) : null;
            try
            {
                Transform? src = hub != null
                    ? FindDeep(hub.transform, "RoundRow" + index) ?? FindFirstRoundRow(hub.transform)
                    : null;
                if (src == null)
                {
                    Debug.LogError("[GpsRoundsBuilder] no round-row atom in the hub prefab — " +
                                   "MY RECENT ROUNDS cannot be cloned. Surfacing rather than " +
                                   "hand-rolling one (Rule 19).");
                    GameObject stub = Rect("RoundRow" + index, parent, 0, index * 130f, 958, 130);
                    return stub.AddComponent<GpsHubRoundRow>();
                }

                var clone = Object.Instantiate(src.gameObject, parent);
                clone.name = "RoundRow" + index;
                var rt = (RectTransform)clone.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -index * 130f);
                rt.sizeDelta = new Vector2(958f, 130f);
                var view = clone.GetComponent<GpsHubRoundRow>();
                if (view == null) view = clone.AddComponent<GpsHubRoundRow>();
                return view;
            }
            finally { if (hub != null) PrefabUtility.UnloadPrefabContents(hub); }
        }

        static Transform? FindFirstRoundRow(Transform root)
        {
            foreach (GpsHubRoundRow r in root.GetComponentsInChildren<GpsHubRoundRow>(true))
                if (r != null) return r.transform;
            return null;
        }

        static Transform? FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform? hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Modals
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject BuildCheckInModalAsset()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject asset;
            try
            {
                var root = new GameObject("CheckInConfirmModal", typeof(RectTransform));
                Stretch((RectTransform)root.transform);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

                var ctrl = root.AddComponent<CheckInConfirmModalController>();
                var so = new SerializedObject(ctrl);
                so.FindProperty("animateShow").boolValue = true;

                GameObject backdrop = Scrim(root.transform);
                GameObject panel = ModalPanel(root.transform);

                TMP("Title", panel.transform, 0, 40, 958, 56, "", SB(42), Gold, FontSemi,
                    TextAlignmentOptions.Top, "GPS_ROUNDS_CONFIRM_TITLE");

                // The pin glyph is CENTRED in the ring (Cesar's edit to the frame, 2026-09-03).
                GameObject ring = Rect("IconRing", panel.transform, 419, 120, 120, 120);
                Img(ring, SprModalRing, White);
                GameObject glyph = Rect("PinIcon", ring.transform, 40, 33.5f, 40, 53);
                Img(glyph, IcoPin, White);

                Set(so, "_venueName",
                    TMP("Venue", panel.transform, 32, 268, 894, 48, "", SB(36), White, FontSemi,
                        TextAlignmentOptions.Top));
                Set(so, "_venueSub",
                    TMP("VenueSub", panel.transform, 32, 322, 894, 32, "", 24f, Muted, FontMed,
                        TextAlignmentOptions.Top));

                string[] statKeys =
                {
                    "GPS_ROUNDS_PTS_ON_CHECKIN", "GPS_ROUNDS_PTS_ON_CHECKOUT",
                    "GPS_ROUNDS_GPS_ACCURACY",
                };
                Color[] statColours = { White, Gold, Green };
                string[] statFields = { "_statCheckInValue", "_statCheckOutValue", "_statAccuracyValue" };
                for (int i = 0; i < 3; i++)
                {
                    GameObject stat = Rect("Stat" + i, panel.transform, 32 + i * 298f, 380, 298, 110);
                    Set(so, statFields[i],
                        TMP("Value", stat.transform, 0, 0, 298, 60, "—", SB(48), statColours[i],
                            FontSemi, TextAlignmentOptions.Top));
                    TMP("Label", stat.transform, 0, 66, 298, 30, "", 22f, Muted, FontMed,
                        TextAlignmentOptions.Top, statKeys[i]);
                }

                // 790, not 830: at 830 the wrap pulls "your" up onto line 1 and the two
                // lines stop matching the node, whose line 1 ends at "finish —".
                // x=84 keeps it centred in the 958-wide panel.
                TMP("Note", panel.transform, 84, 506, 790, 76, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.Top, "GPS_ROUNDS_CONFIRM_NOTE").textWrappingMode =
                    TextWrappingModes.Normal;

                Button confirm = WideButton(panel.transform, "ConfirmButton", 32, 600, 894, true,
                                            "GPS_ROUNDS_CHECK_IN", 64);
                Set(so, "_confirmButton", confirm);
                Set(so, "_confirmLabel",
                    confirm.transform.Find("Label").GetComponent<TextMeshProUGUI>());
                Set(so, "_cancelButton",
                    DarkButton(panel.transform, "CancelButton", 32, 676, 894, "GPS_ROUNDS_CANCEL"));

                Set(so, "modalPanel", panel);
                Set(so, "backdrop", backdrop);
                so.ApplyModifiedPropertiesWithoutUndo();

                panel.SetActive(false);
                backdrop.SetActive(false);

                asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabCheckIn);
                Object.DestroyImmediate(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return asset;
        }

        static GameObject BuildCompleteModalAsset()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject asset;
            try
            {
                var root = new GameObject("RoundCompleteModal", typeof(RectTransform));
                Stretch((RectTransform)root.transform);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);

                var ctrl = root.AddComponent<RoundCompleteModalController>();
                var so = new SerializedObject(ctrl);
                so.FindProperty("animateShow").boolValue = true;

                GameObject backdrop = Scrim(root.transform);
                GameObject panel = ModalPanel(root.transform);

                Set(so, "_title",
                    TMP("Title", panel.transform, 0, 40, 958, 56, "", SB(42), Gold, FontSemi,
                        TextAlignmentOptions.Top));

                // Node 14078-33991 order is pin -> NAME -> sub, and the y values below are the
                // measured glyph bands of that render (panel-relative): ring 108, name 252,
                // sub 302, stats 366, note 470, buttons 563 / 648. The modal previously had NO
                // venue name at all and put the sub-line ABOVE the pin.
                GameObject ring = Rect("IconRing", panel.transform, 419, 108, 120, 120);
                Img(ring, SprModalRing, White);
                GameObject glyph = Rect("PinIcon", ring.transform, 40, 33.5f, 40, 53);
                Img(glyph, IcoPin, White);

                Set(so, "_venueName",
                    TMP("Venue", panel.transform, 32, 250, 894, 48, "", SB(36), White, FontSemi,
                        TextAlignmentOptions.Top));
                Set(so, "_sub",
                    TMP("Sub", panel.transform, 32, 300, 894, 32, "", 24f, Muted, FontMed,
                        TextAlignmentOptions.Top));

                string[] statKeys =
                {
                    "GPS_ROUNDS_ELAPSED", "GPS_ROUNDS_PTS_EARNED", "GPS_ROUNDS_GPS_FIXES",
                };
                Color[] statColours = { White, Gold, Green };
                string[] statFields = { "_statElapsedValue", "_statPtsValue", "_statFixesValue" };
                for (int i = 0; i < 3; i++)
                {
                    GameObject stat = Rect("Stat" + i, panel.transform, 32 + i * 298f, 366, 298, 110);
                    Set(so, statFields[i],
                        TMP("Value", stat.transform, 0, 0, 298, 60, "—", SB(48), statColours[i],
                            FontSemi, TextAlignmentOptions.Top));
                    TMP("Label", stat.transform, 0, 66, 298, 30, "", 22f, Muted, FontMed,
                        TextAlignmentOptions.Top, statKeys[i]);
                }

                var note = TMP("Note", panel.transform, 64, 470, 830, 110, "", 24f, Muted, FontMed,
                               TextAlignmentOptions.Top);
                note.textWrappingMode = TextWrappingModes.Normal;
                Set(so, "_note", note);

                Button primary = WideButton(panel.transform, "PrimaryButton", 32, 563, 894, true,
                                            null, 64);
                Set(so, "_primaryButton", primary);
                Set(so, "_primaryLabel",
                    primary.transform.Find("Label").GetComponent<TextMeshProUGUI>());

                Button secondary = DarkButton(panel.transform, "SecondaryButton", 32, 648, 894, null);
                Set(so, "_secondaryButton", secondary);
                Set(so, "_secondaryLabel",
                    secondary.transform.Find("Label").GetComponent<TextMeshProUGUI>());

                Set(so, "modalPanel", panel);
                Set(so, "backdrop", backdrop);
                so.ApplyModifiedPropertiesWithoutUndo();

                panel.SetActive(false);
                backdrop.SetActive(false);

                asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabComplete);
                Object.DestroyImmediate(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return asset;
        }

        /// <summary>The 60 % black scrim behind a modal.</summary>
        static GameObject Scrim(Transform parent)
        {
            GameObject go = Rect("Backdrop", parent, 0, 0, 1170, 2532);
            Stretch((RectTransform)go.transform);
            var img = Img(go, null, ADark(Color.black, 0.6f));
            img.raycastTarget = true;      // swallows taps outside the panel
            return go;
        }

        /// <summary>The shared 958x760 shell, centred at the node's own y.</summary>
        static GameObject ModalPanel(Transform parent)
        {
            // y = (2532 - 760) / 2 - 120 = 766; x = (1170 - 958) / 2 = 106.
            GameObject go = Rect("ModalPanel", parent, 106, 766, 958, 760);
            Img(go, SprModalPanel, White, Image.Type.Simple);
            return go;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shared helpers — identical shapes to GpsGiftVoteBuilder
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>The 80px panel header + its gradient rule.</summary>
        static TextMeshProUGUI PanelHeader(Transform panel, float titleWidth,
                                           string? titleKey = null)
        {
            var header = Rect("PanelHeader", panel, 0, 0, 958, 80);
            TextMeshProUGUI title = TMP("PanelTitle", header.transform, 32, 18, titleWidth, 50, "",
                                        SB(42), Gold, FontSemi, TextAlignmentOptions.TopLeft,
                                        titleKey);
            var sep = Rect("Separator", panel, 0, 80, 958, 2);
            Img(sep, SprSeparator, White);
            return title;
        }

        /// <summary>Pin a stack child's height so the VerticalLayoutGroup cannot resize it.
        /// Belt and braces with <c>childControlHeight = false</c>: C3/C4 both bite here.</summary>
        static void Pin(GameObject go, float height)
        {
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 958f;
            le.minHeight = le.preferredHeight = height;
        }

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

        /// <summary>A rect anchored and pivoted at its parent's CENTRE — the pins and the player
        /// dot, whose positions come back from <c>MapProjection.Offset</c> as offsets from the
        /// tile centre.</summary>
        static GameObject Centred(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
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
            go.AddComponent<ButtonPressFeedback>();   // Rule 11 — every new player-facing Button
            return btn;
        }

        static Sprite? Sprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("[GpsRoundsBuilder] sprite not found: " + path);
            return s;
        }

        static Image Img(GameObject go, string? spritePath, Color color,
                         Image.Type type = Image.Type.Simple, float sliceRadius = 0f)
        {
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            if (spritePath != null)
            {
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (img.sprite == null)
                    Debug.LogError("[GpsRoundsBuilder] sprite not found: " + spritePath);
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

        /// <summary>A `Main Buttons / Gold - Small` instance at the node's 230x54 r20.</summary>
        static Button SmallButton(Transform parent, string name, float x, float y, float w,
                                  bool gold, string? key)
        {
            GameObject go = Rect(name, parent, x, y, w, 54);
            var img = Img(go, gold ? SprGold : SprSilver, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = gold ? 18f / 20f : 25f / 20f;   // both -> the node's r20
            img.raycastTarget = true;

            Button button = Btn(go);
            TMP("Label", go.transform, 0, 0, w, 54, "", SB(39), gold ? ButtonInk : White, FontSemi,
                TextAlignmentOptions.Midline, key);
            return button;
        }

        /// <summary>A gold button at an explicit width — the card's two 430s and the modals' 894s.
        /// A fixed-size sibling of <see cref="SmallButton"/> rather than the ContentSizeFitter
        /// shape: these are laid out by the node at exact widths, and a fitter would size them
        /// from their label instead.</summary>
        static Button WideButton(Transform parent, string name, float x, float y, float w,
                                 bool gold, string? key, float h = 54f)
        {
            GameObject go = Rect(name, parent, x, y, w, h);
            var img = Img(go, gold ? SprGold : SprSilver, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = gold ? 18f / 20f : 25f / 20f;
            img.raycastTarget = true;

            Button button = Btn(go);
            TMP("Label", go.transform, 0, 0, w, h, "", SB(39), gold ? ButtonInk : White, FontSemi,
                TextAlignmentOptions.Midline, key);
            return button;
        }

        /// <summary>The modals' CANCEL / DONE — 894x64 r20, ADark(black, 0.35) with a 2px
        /// #818ea1 rim and a white SemiBold 28 label (node 14080:34292).</summary>
        static Button DarkButton(Transform parent, string name, float x, float y, float w,
                                 string? key)
        {
            GameObject go = Rect(name, parent, x, y, w, 64);
            var img = Img(go, SprPill, ADark(Color.black, 0.35f), Image.Type.Sliced, 20f);
            img.raycastTarget = true;

            GameObject rim = Rect("Rim", go.transform, 0, 0, w, 64);
            Stretch((RectTransform)rim.transform);
            Img(rim, SprPillRing, ChipRim, Image.Type.Sliced, 20f);

            Button button = Btn(go);
            TMP("Label", go.transform, 0, 0, w, 64, "", SB(28), White, FontSemi,
                TextAlignmentOptions.Midline, key);
            return button;
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

        /// <summary>Clone the hub's own GPS nav bar. Every slot is chrome here; the ROUNDS slot is
        /// left interactable and inert (it is the screen the player is standing on), exactly as
        /// the hub leaves HOME.</summary>
        static void CloneNavBar(RectTransform parent)
        {
            if (!File.Exists(HubPrefab)) { Debug.LogWarning("[GpsRoundsBuilder] hub prefab not found"); return; }
            GameObject hub = PrefabUtility.LoadPrefabContents(HubPrefab);
            try
            {
                Transform nav = GpsPolishBuilder.FindNavBar(hub);
                if (nav == null) { Debug.LogWarning("[GpsRoundsBuilder] no GpsNavBar in hub"); return; }
                var clone = Object.Instantiate(nav.gameObject, parent);
                clone.name = "GpsNavBar";
                foreach (Button b in clone.GetComponentsInChildren<Button>(true))
                {
                    var colors = b.colors; colors.disabledColor = Color.white;
                    b.colors = colors; b.interactable = false;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }

        // ── Colour helpers ─────────────────────────────────────────────────────

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static float S2L(float c)
        {
            c /= 255f;
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>A GENUINELY TRANSLUCENT overlay with the alpha corrected for Unity's linear
        /// compositing — the same solve <c>alpha_over()</c> does at bake time. Use this, never a
        /// pre-composited opaque colour, for anything sitting on a TRANSLUCENT card.</summary>
        static Color T(Color overlay, float srgbAlpha, Color backdrop)
            => new Color(overlay.r, overlay.g, overlay.b,
                         LinearAlpha(srgbAlpha, overlay, backdrop));

        static float LinearAlpha(float srgbAlpha, Color overlay, Color backdrop)
        {
            float total = 0f; int n = 0;
            for (int c = 0; c < 3; c++)
            {
                float f = Channel(overlay, c) * 255f;
                float b = Channel(backdrop, c) * 255f;
                float lf = S2L(f), lb = S2L(b);
                if (Mathf.Abs(lf - lb) < 1e-4f) continue;
                float t = srgbAlpha * f + (1f - srgbAlpha) * b;
                total += Mathf.Clamp01((S2L(t) - lb) / (lf - lb));
                n++;
            }
            return n == 0 ? srgbAlpha : total / n;
        }

        static float Channel(Color c, int i) => i == 0 ? c.r : i == 1 ? c.g : c.b;

        /// <summary>Genuinely translucent dark overlay — the scrim and the dark capsules, which
        /// have no known backdrop to pre-composite against.</summary>
        static Color ADark(Color c, float srgbAlpha)
            => new Color(c.r, c.g, c.b, 1f - Mathf.Pow(1f - srgbAlpha, 2.2f));

        // ── SerializedObject helpers ──────────────────────────────────────────

        static void Set(SerializedObject so, string field, Object? value)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[GpsRoundsBuilder] no field '" + field + "'"); return; }
            p.objectReferenceValue = value;
        }

        static void SetArray<T>(SerializedObject so, string field, List<T> values) where T : Object
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[GpsRoundsBuilder] no field '" + field + "'"); return; }
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
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

        /// <summary>Force every sprite this builder loads to import AS A SPRITE — a freshly baked
        /// PNG imports as a TEXTURE and <c>LoadAssetAtPath&lt;Sprite&gt;</c> then returns null,
        /// which renders as a white box rather than as an error
        /// (reference_new_png_imports_as_texture_not_sprite).</summary>
        static void EnsureImport()
        {
            var single = new List<string>
            {
                BgRounds,
                SprMapPanel, SprSpotList, SprHistory, SprActiveCard, SprModalPanel, SprModalRing,
                SprMapFallback, SprSpotDisc, SprSpotRing, SprPinFill, SprPinRim, SprPlayerDot,
                SprDot18,
                SprPill, SprPillRing, SprChipRing, SprGoldSeg, SprGold, SprSilver, SprSeparator,
                IcoPin,
            };

            bool dirty = false;
            foreach (string p in single)
            {
                if (!File.Exists(p)) { Debug.LogWarning("[GpsRoundsBuilder] missing asset " + p); continue; }
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(p) as TextureImporter;
                }
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                dirty = true;
            }

            if (dirty) AssetDatabase.Refresh();
        }
    }
}
