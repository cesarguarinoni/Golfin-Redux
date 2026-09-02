// gps_gifts_votes — builder for the two remaining GPS screens (Figma 14027:101843 / 14028:33534)
// plus the two modals they open. Re-runnable; overwrites the prefabs on every run, which is what
// makes THIS FILE the source of truth for their hierarchy rather than the .prefab YAML.
//
// Shapes, helpers and calibration are lifted verbatim from GpsProfilePackBuilder /
// GpsAuthExtrasBuilder / ScoreUploadScreenBuilder — same Rect() convention (top-left anchor +
// top-left pivot, so a Figma (x, y) transcribes with only a y negation), same SemiBoldSize
// correction, same "bake gradients, tint flats" rule.
#nullable enable
using System.Collections.Generic;
using System.IO;
using Golfin.UI.Polish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI.Editor
{
    public static class GpsGiftVoteBuilder
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        const string HubPrefab        = "Assets/Prefabs/UI/Gps/GpsHubScreen.prefab";
        const string PrefabGift       = "Assets/Prefabs/UI/Gps/GpsGiftScreen.prefab";
        const string PrefabVote       = "Assets/Prefabs/UI/Gps/GpsVoteScreen.prefab";
        const string PrefabGiftModal  = "Assets/Prefabs/UI/Gps/GiftSendModal.prefab";
        const string PrefabVoteModal  = "Assets/Prefabs/UI/Gps/VoteCreateModal.prefab";

        // ── Backgrounds ───────────────────────────────────────────────────────
        // Both already in the project; see make_gps_gift_vote_panels.py's header for the match.
        const string BgGift = "Assets/Art/Shop/Background - Rewards.png";
        const string BgVote = "Assets/Art/ClubsInventory/Background.png";

        // ── Baked panels (make_gps_gift_vote_panels.py) ───────────────────────
        const string SprGiftHero   = "Assets/Art/UI/Gps/S_GV_GiftHero.png";
        const string SprSupporters = "Assets/Art/UI/Gps/S_GV_Supporters.png";
        const string SprGolfers    = "Assets/Art/UI/Gps/S_GV_Golfers.png";
        const string SprBuyGifts   = "Assets/Art/UI/Gps/S_GV_BuyGifts.png";
        const string SprItemCell   = "Assets/Art/UI/Gps/S_GV_ItemCell.png";
        const string SprStoriesRow = "Assets/Art/UI/Gps/S_GV_StoriesRow.png";
        const string SprChipsRow   = "Assets/Art/UI/Gps/S_GV_ChipsRow.png";
        const string SprCardPhoto  = "Assets/Art/UI/Gps/S_GV_CardPhoto.png";
        const string SprCardSimple = "Assets/Art/UI/Gps/S_GV_CardSimple.png";
        const string SprCardMulti  = "Assets/Art/UI/Gps/S_GV_CardMulti.png";
        const string SprCardPhoto2 = "Assets/Art/UI/Gps/S_GV_CardPhoto2.png";
        const string SprPhotoGreen = "Assets/Art/UI/Gps/S_GV_PhotoGreen.png";
        const string SprPhotoBrown = "Assets/Art/UI/Gps/S_GV_PhotoBrown.png";
        const string SprChipRing   = "Assets/Art/UI/Gps/S_GV_ChipRing.png";   // 1px rim at r26
        const string SprPillRing   = "Assets/Art/UI/Gps/S_GV_PillRing.png";   // 1px rim at r19
        const string SprSeparator  = "Assets/Art/UI/Gps/S_GV_Separator.png";
        const string SprStoryNew   = "Assets/Art/UI/Gps/S_GV_StoryNew.png";

        // ── Reused atoms ──────────────────────────────────────────────────────
        const string SprPill       = "Assets/Art/Tournaments/S_PillStadium.png";   // 9-sliced capsule, border 88
        const string SprGold       = "Assets/Art/HomeScreen/Play Button.png";      // Main Buttons Gold
        const string SprSilver     = "Assets/Art/RosterScreen/ButtonCancel.png";   // Main Buttons Silver
        const string SprGoldSeg    = "Assets/Art/UI/Gps/S_SU_GoldSegment.png";     // #f3ecc2 -> #c9a94f capsule
        const string SprIconRing   = "Assets/Art/UI/Gps/S_GpsIconRing_Tile.png";   // 88px icon-ring atom
        const string SprModalPanel = "Assets/Art/UI/Gps/S_SU_ModalPanel.png";
        const string SprModalRow   = "Assets/Art/UI/Gps/S_SU_ModalRow.png";
        const string SprSearchField= "Assets/Art/UI/Gps/S_SU_SearchField.png";

        const string IcoGift       = "Assets/Art/UI/Gps/ICO_GpsGift.png";
        const string IcoHeart      = "Assets/Art/UI/Gps/ICO_GpsHeart.png";
        const string IcoStar       = "Assets/Art/UI/Gps/ICO_GpsStar.png";
        const string IcoPin        = "Assets/Art/UI/Gps/ICO_GpsPin.png";
        const string IcoSparkle    = "Assets/Art/UI/Gps/ICO_GpsSparkle.png";
        const string IcoScreenshot = "Assets/Art/UI/Gps/ICO_GpsScreenshot.png";

        /// <summary>Avatar discs, indexed [colour][size]. Colour order is the
        /// <c>profiles.avatar_color</c> enum order (pink, green, blue, gold) so an index and a
        /// colour name are interchangeable, exactly as on the Golf Profile screen.</summary>
        static string Avatar(string colour, int size)
            => "Assets/Art/UI/Gps/S_GV_Avatar" + colour + "_" + size + ".png";

        static readonly string[] AvatarColours = { "Pink", "Green", "Blue", "Gold" };

        // ── Fonts ─────────────────────────────────────────────────────────────
        const string FontSemi = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        const string FontReg  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";
        // "Rubik:Medium" on the node. The project ships SemiBold + the variable face only, so
        // Medium resolves to the variable face — a known-unequal recorded in the fidelity table,
        // identical to the three profile-pack screens.
        const string FontMed  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        /// <summary>
        /// EVERY SemiBold run is authored as <c>node_px * SemiBoldSize</c> (Build rule 4 as
        /// generalised by auth_golf_profile): the project's Rubik SemiBold face renders ~11 %
        /// larger than the face the node draws with, and the 59-for-66 button calibration is that
        /// same correction. Medium/Regular runs are NOT scaled — they measured at cap-height 1.00.
        ///
        /// <para>
        /// ⚠️ This is applied to the SMALL button label too (39 -> 34.9), which
        /// <c>ScoreUploadScreenBuilder.SmallButton</c> does not do: that helper predates the
        /// whole-face finding and still ships the node's raw 39. Recorded as a deliberate
        /// difference in the report rather than silently diverging.
        /// </para>
        /// </summary>
        const float SemiBoldSize = 59f / 66f;

        static float SB(float nodePx) => nodePx * SemiBoldSize;

        // ── Colours ───────────────────────────────────────────────────────────
        static readonly Color White      = Color.white;
        static readonly Color Gold       = Hex("#EEDC9A");
        static readonly Color Muted      = Hex("#B7C3D3");
        static readonly Color Pink       = Hex("#F07F9C");   // hero title + supporter pts
        static readonly Color PinkSoft   = Hex("#F4B8C8");   // hero sub + note
        static readonly Color Green      = Hex("#7ED488");   // YES bar + option pill 1
        static readonly Color Blue       = Hex("#6FA5E8");   // NO bar + option pill 2
        static readonly Color ChipRim    = Hex("#818EA1");
        static readonly Color ChipOnRim  = Hex("#422100");
        static readonly Color ChipOnInk  = Hex("#2A1A00");
        static readonly Color ButtonInk  = Hex("#321506");
        static readonly Color SilverInk  = Hex("#1E293B");

        // ── Translucent overlays ──────────────────────────────────────────────
        //
        // ⚠️ THESE ARE REAL ALPHAS, NOT PRE-COMPOSITED COLOURS, and that is the opposite of what
        // the first build did. `A(overlay, alpha, backdrop)` collapses a translucent fill to the
        // one OPAQUE colour it would produce over an assumed backdrop — which is right for a fill
        // whose backdrop really is constant (the Screenshot glyph over the photo gradient below),
        // and wrong for every fill on this pair of screens, because they all sit on a TRANSLUCENT
        // card over a photograph. Assuming `#0A2037` under them rendered every one of them far
        // too dark; measured against the node renders, before the fix:
        //
        //     bar track      node (109,131,149)   built (46, 64, 85)    ~64 too dark
        //     reward pill    node (172,173,144)   built (124,124, 97)   ~48 too dark
        //     row divider    node +23 over body   built  +1 over body   invisible
        //     photo gradient node ( 53, 92, 53)   built (54, 91, 53)    1.0 — the OPAQUE control
        //
        // The control is what makes it a diagnosis rather than a guess: the baked opaque gradients
        // land on the node exactly, and only the pre-composited translucencies are wrong.
        //
        // So the alpha is CORRECTED instead: Figma composites in sRGB and Unity in linear, and
        // `LinearAlpha` solves for the alpha whose LINEAR blend reproduces Figma's sRGB one. Same
        // solve as `alpha_over()` in make_score_upload_panels.py — one implementation of the idea,
        // two languages, because one runs at bake time and one at build time.

        /// <summary>What a vote card's own body measures on the node render (14028:33849) — the
        /// backdrop the bar tracks and the reward pills sit on.</summary>
        static readonly Color VoteCardBody = Hex("#5D7385");

        /// <summary>What a Gift panel's body measures on the node render (14027:102114) — the
        /// backdrop the row dividers sit on. Darker than the vote card's: a different photo.</summary>
        static readonly Color GiftPanelBody = Hex("#474F53");

        /// <summary>Bar track — node <c>bg-[rgba(255,255,255,0.15)]</c> (14028:33856).</summary>
        static readonly Color TrackBg = T(White, 0.15f, VoteCardBody);

        /// <summary>The 1px row divider — node <c>border-[rgba(255,255,255,0.12)] border-t</c>.</summary>
        static readonly Color RowRule = T(White, 0.12f, GiftPanelBody);

        /// <summary>Multi-option pill accents, in the node's own order (14028:33908..33914).</summary>
        static readonly Color[] OptionAccents = { Green, Blue, Pink, Muted };

        const float PillBorder = 88f;   // S_PillStadium / S_GV_ChipRing 9-slice border

        // ── Canvas geometry ───────────────────────────────────────────────────
        // Content Container is (96, 361, 978, 1860) on BOTH frames; every panel sits at x=10.
        const float CcX = 96f, CcY = 361f, CcW = 978f, CcH = 1860f;

        // ═══════════════════════════════════════════════════════════════════════
        // Menu entry points
        // ═══════════════════════════════════════════════════════════════════════

        [MenuItem("GOLFIN/Gps/Build Gift Screen", priority = 220)]
        public static void BuildGift()
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            GameObject modal = BuildGiftModalAsset();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = BuildGiftScreen(modal);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabGift);
                Debug.Log("[GpsGiftVoteBuilder] Built " + PrefabGift);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        [MenuItem("GOLFIN/Gps/Build Vote Screen", priority = 221)]
        public static void BuildVote()
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            GameObject modal = BuildVoteModalAsset();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                GameObject root = BuildVoteScreen(modal);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabVote);
                Debug.Log("[GpsGiftVoteBuilder] Built " + PrefabVote);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        [MenuItem("GOLFIN/Gps/Build Gift + Vote Screens", priority = 222)]
        public static void BuildAll()
        {
            BuildGift();
            BuildVote();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GPS Gift Screen — Figma 14027:101843
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject BuildGiftScreen(GameObject modalPrefab)
        {
            var root = new GameObject("GpsGiftScreen", typeof(RectTransform));
            Stretch((RectTransform)root.transform);

            var ctrl = root.AddComponent<GpsGiftScreenController>();
            var so   = new SerializedObject(ctrl);

            var bg = Rect("Background", root.transform, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgGift, White, Image.Type.Simple);

            var col = (RectTransform)Rect("ContentContainer", root.transform, CcX, CcY, CcW, CcH).transform;

            // ── Gift Hero — node 14027:102100, 958x288 ────────────────────────
            // The ONE plum panel. Its four runs are all centred in the 958, so they are authored
            // full-width and centre-aligned rather than at the node's own x — which is where the
            // flex happened to put a string of that particular length.
            GameObject hero = Card("GiftHero", col, 10, 0, 958, 288, SprGiftHero);

            // HH = icon + title as one centred unit. A HorizontalLayoutGroup, not two hard x's:
            // the Japanese title is a different width and the pair has to stay centred.
            var hh = Rect("HH", hero.transform, 0, 28, 958, 43);
            var hhLayout = hh.AddComponent<HorizontalLayoutGroup>();
            hhLayout.childAlignment = TextAnchor.MiddleCenter;
            hhLayout.spacing = 12f;
            hhLayout.childControlWidth = hhLayout.childControlHeight = true;
            hhLayout.childForceExpandWidth = hhLayout.childForceExpandHeight = false;

            var giftIcon = Rect("GiftIcon", hh.transform, 0, 0, 36, 36);
            Img(giftIcon, IcoGift, Pink);
            var iconLe = giftIcon.AddComponent<LayoutElement>();
            iconLe.minWidth = iconLe.preferredWidth = 36f;
            iconLe.minHeight = iconLe.preferredHeight = 36f;

            TMP("HeroTitle", hh.transform, 0, 0, 0, 43, "", SB(36), Pink, FontSemi,
                TextAlignmentOptions.Midline, "GPS_GIFT_HERO_TITLE");

            Set(so, "_heroSub",
                TMP("HeroSub", hero.transform, 0, 77, 958, 31, "", 26f, PinkSoft, FontMed,
                    TextAlignmentOptions.Top));
            Set(so, "_heroValue",
                TMP("HeroValue", hero.transform, 0, 114, 958, 114, "—", SB(96), White, FontSemi,
                    TextAlignmentOptions.Top));
            TMP("HeroNote", hero.transform, 0, 234, 958, 28, "", 24f, PinkSoft, FontMed,
                TextAlignmentOptions.Top, "GPS_GIFT_HERO_NOTE");

            // ── Top Supporters — node 14027:102114, 958x376 ───────────────────
            GameObject sup = Card("Supporters", col, 10, 312, 958, 376, SprSupporters);
            PanelHeader(sup.transform, "GPS_GIFT_SUPPORTERS");

            var supRows = new List<GameObject>();
            // Node rows: 96 / 96 / 104 (the last carries the panel's 20px bottom padding).
            float[] supHeights = { 96, 96, 104 };
            for (int i = 0; i < 3; i++)
            {
                GameObject row = SupporterRow(sup.transform, "Supporter" + i, 80 + i * 96,
                                              supHeights[i], i);
                supRows.Add(row);
            }
            SetArray(so, "_supporterRows", supRows);

            // ── Popular Golfers — node 14027:102146, 958x568 ──────────────────
            GameObject gol = Card("Golfers", col, 10, 712, 958, 568, SprGolfers);
            PanelHeader(gol.transform, "GPS_GIFT_POPULAR");

            var golRows = new List<GameObject>();
            var golButtons = new List<Button>();
            float[] golHeights = { 96, 96, 96, 96, 104 };
            for (int i = 0; i < 5; i++)
            {
                GameObject row = GolferRow(gol.transform, "Golfer" + i, 80 + i * 96,
                                           golHeights[i], i, out Button send);
                golRows.Add(row);
                golButtons.Add(send);
            }
            SetArray(so, "_golferRows", golRows);
            SetArray(so, "_golferSendButtons", golButtons);

            // ── Buy Gift Items — node 14027:102190, 958x312 ───────────────────
            GameObject buy = Card("BuyGifts", col, 10, 1304, 958, 312, SprBuyGifts);
            TMP("BuyTitle", buy.transform, 0, 22, 958, 40, "", SB(34), Gold, FontSemi,
                TextAlignmentOptions.Top, "GPS_GIFT_BUY_TITLE");
            TMP("BuySub", buy.transform, 0, 72, 958, 28, "", 24f, Muted, FontMed,
                TextAlignmentOptions.Top, "GPS_GIFT_BUY_SUB");

            var items = Rect("GiftItems", buy.transform, 32, 110, 894, 178);
            var itemCells = new List<GameObject>();
            var itemButtons = new List<Button>();
            for (int i = 0; i < 3; i++)
            {
                GameObject cell = ItemCell(items.transform, "Item" + i, i * 303.3333f,
                                           out Button tap);
                itemCells.Add(cell);
                itemButtons.Add(tap);
            }
            SetArray(so, "_itemCells", itemCells);
            SetArray(so, "_itemButtons", itemButtons);
            // The four category glyphs, in GpsGiftScreenController.Glyph order. Wired here so the
            // controller never loads by path — none of these live under Resources/, so a runtime
            // load would return null and the cell would render an empty ring.
            SetArray(so, "_glyphSprites", new List<Sprite>
            {
                Sprite(IcoHeart), Sprite(IcoStar), Sprite(IcoSparkle), Sprite(IcoPin),
            });

            CloneNavBar((RectTransform)root.transform, "GIFT", out Button? navGift);

            // The modal is a child of the SCREEN and authored inactive — ModalController
            // re-parents itself to the last sibling on Show, and both of its own children must
            // start inactive or UIParticle throws on every play-mode entry.
            var modal = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, root.transform);
            modal.name = "GiftSendModal";
            Set(so, "_sendModal", modal.GetComponent<GiftSendModalController>());

            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>The 80px panel header + its gradient rule. "SEE ALL ›" is authored and left
        /// INACTIVE: the node draws it, and v1 has nowhere for it to go (SPEC § Out of scope), so
        /// it exists in the hierarchy for the task that gives it a destination.</summary>
        static void PanelHeader(Transform panel, string titleKey)
        {
            var header = Rect("PanelHeader", panel, 0, 0, 958, 80);
            TMP("PanelTitle", header.transform, 32, 18, 500, 50, "", SB(42), Gold, FontSemi,
                TextAlignmentOptions.TopLeft, titleKey);
            var seeAll = TMP("SeeAll", header.transform, 656, 26.5f, 270, 33, "", 28f, Muted,
                             FontMed, TextAlignmentOptions.TopRight, "GPS_GIFT_SEE_ALL");
            seeAll.gameObject.SetActive(false);

            var sep = Rect("Separator", panel, 0, 80, 958, 2);
            Img(sep, SprSeparator, White);
        }

        /// <summary>
        /// One TOP SUPPORTERS row. Geometry is the node's for rows 2+ (rank "2" is wider than
        /// "1", so the flex puts their avatar at x=72 and their info at x=164); row 0 is authored
        /// at the same numbers rather than at its own 67/159, which is a 5px difference on one
        /// row and is what keeps every row one identical template.
        /// </summary>
        static GameObject SupporterRow(Transform parent, string name, float y, float h, int index)
        {
            GameObject row = Rect(name, parent, 0, y, 958, h);
            if (index > 0) RowDivider(row.transform);

            TMP("Rank", row.transform, 32, 30, 32, 36, (index + 1).ToString(), SB(30),
                index == 0 ? Gold : Muted, FontSemi, TextAlignmentOptions.TopLeft);

            AvatarDisc(row.transform, 72, 12, 72, index, SB(30), 18);

            TMP("Name", row.transform, 164, 16, 480, 36, "", SB(30), White, FontSemi,
                TextAlignmentOptions.TopLeft);
            TMP("Followers", row.transform, 164, 54, 480, 26, "", 22f, Muted, FontMed,
                TextAlignmentOptions.TopLeft);
            // Right-aligned into the panel's 32px padding: 926 is 958 - 32.
            TMP("Pts", row.transform, 656, 29, 270, 38, "", SB(32), Pink, FontSemi,
                TextAlignmentOptions.TopRight);
            return row;
        }

        /// <summary>One POPULAR GOLFERS row — the supporter row with a Gold-Small button where
        /// the points are (node 14027:102159, 240x54 at x=686).</summary>
        static GameObject GolferRow(Transform parent, string name, float y, float h, int index,
                                    out Button send)
        {
            GameObject row = Rect(name, parent, 0, y, 958, h);
            if (index > 0) RowDivider(row.transform);

            TMP("Rank", row.transform, 32, 30, 32, 36, (index + 1).ToString(), SB(30),
                index == 0 ? Gold : Muted, FontSemi, TextAlignmentOptions.TopLeft);

            AvatarDisc(row.transform, 72, 12, 72, index, SB(30), 18);

            TMP("Name", row.transform, 164, 16, 500, 36, "", SB(30), White, FontSemi,
                TextAlignmentOptions.TopLeft);
            TMP("Followers", row.transform, 164, 54, 500, 26, "", 22f, Muted, FontMed,
                TextAlignmentOptions.TopLeft);

            send = SmallButton(row.transform, "SendGiftButton", 686, 21, 240, true,
                               "GPS_GIFT_SEND_GIFT");
            return row;
        }

        /// <summary>The 1px top rule between rows (node <c>border-t rgba(255,255,255,0.12)</c>).
        /// A real 1px Image, NOT a UI <c>Outline</c> — Rule 21 fails Outline-as-border because it
        /// draws four offset copies rather than a stroke.</summary>
        static void RowDivider(Transform row)
        {
            var rule = Rect("Divider", row, 0, 0, 958, 1);
            Img(rule, null, RowRule);
        }

        /// <summary>
        /// An avatar disc: the icon-ring atom in one of the four <c>avatar_color</c> gradients,
        /// with the player's initial centred on it.
        ///
        /// ONE IMAGE, NOT TWO. <c>S_GV_Avatar*</c> is a FILLED circle with the rim painted over
        /// it, exactly like <c>S_GpsIconRing_Tile</c> — a colour disc placed behind it would be
        /// completely covered, which is the defect that made the Golf Profile hero render navy in
        /// all four colours.
        /// </summary>
        static void AvatarDisc(Transform parent, float x, float y, int size, int colourIndex,
                               float initialSize, float initialY)
        {
            GameObject disc = Rect("Avatar", parent, x, y, size, size);
            Img(disc, Avatar(AvatarColours[colourIndex % AvatarColours.Length], size), White);
            TMP("Initial", disc.transform, 0, initialY, size, size * 0.5f, "", initialSize, White,
                FontSemi, TextAlignmentOptions.Top);
        }

        /// <summary>One BUY GIFT ITEMS cell — node 14027:102194, 287.33x168 r28.</summary>
        static GameObject ItemCell(Transform parent, string name, float x, out Button tap)
        {
            GameObject cell = Rect(name, parent, x, 10, 287.3333f, 168);
            Img(cell, SprItemCell, White, Image.Type.Simple);
            tap = Btn(cell);

            // The icon ring is the 88px atom drawn at the node's 72 (14027:102196 carries the
            // atom's exact token pair; the scale lands its 5px stroke at 4.09 against the node's 4).
            GameObject ring = Rect("IconRing", cell.transform, 107.6667f, 16, 72, 72);
            Img(ring, SprIconRing, White);
            GameObject ico = Rect("Icon", ring.transform, 18, 18, 36, 36);
            Img(ico, IcoHeart, Pink);

            TMP("ItemName", cell.transform, 0, 94, 287.3333f, 26, "", SB(22), White, FontSemi,
                TextAlignmentOptions.Top);
            TMP("ItemPrice", cell.transform, 0, 126, 287.3333f, 28, "", SB(24), Gold, FontSemi,
                TextAlignmentOptions.Top);
            return cell;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // GPS Vote Screen — Figma 14028:33534
        // ═══════════════════════════════════════════════════════════════════════

        static GameObject BuildVoteScreen(GameObject modalPrefab)
        {
            var root = new GameObject("GpsVoteScreen", typeof(RectTransform));
            Stretch((RectTransform)root.transform);

            var ctrl = root.AddComponent<GpsVoteScreenController>();
            var so   = new SerializedObject(ctrl);

            var bg = Rect("Background", root.transform, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgVote, White, Image.Type.Simple);

            var col = (RectTransform)Rect("ContentContainer", root.transform, CcX, CcY, CcW, CcH).transform;

            // ── Stories row — node 14028:33791, 958x143 r32 ───────────────────
            GameObject stories = Card("StoriesRow", col, 10, 0, 958, 143, SprStoriesRow);

            GameObject newStory = Rect("StoryNew", stories.transform, 24, 14, 88, 115);
            GameObject newDisc = Rect("Disc", newStory.transform, 0, 0, 88, 88);
            Img(newDisc, SprStoryNew, White);
            TMP("Plus", newDisc.transform, 0, 14, 88, 52, "+", SB(44), Gold, FontSemi,
                TextAlignmentOptions.Top);
            TMP("Label", newStory.transform, 0, 94, 88, 21, "", 18f, Gold, FontMed,
                TextAlignmentOptions.Top, "GPS_VOTE_NEW");
            Set(so, "_createStoryButton", Btn(newStory));

            var storyCells = new List<GameObject>();
            for (int i = 0; i < 6; i++)
            {
                GameObject cell = Rect("Story" + i, stories.transform, 134 + i * 110, 14, 88, 115);
                AvatarDisc(cell.transform, 0, 0, 88, i, SB(37), 22);
                // NoWrap + Overflow, the builder's default — and the CONTROLLER truncates the
                // name (GpsVoteScreenController.StoryLabel).
                //
                // TMP's own Ellipsis was tried first and rendered NOTHING: it resolves the
                // ellipsis during line layout, so it needs wrapping ON, and a wrapped line at 18px
                // needs ~21.6px of height against the node's 21px box (14028:33796) — the first
                // line does not fit, so nothing is drawn at all. Keeping the node's geometry and
                // truncating the STRING is the version that both renders and stays inside its
                // 88px cell.
                TMP("Label", cell.transform, 0, 94, 88, 21, "", 18f, White, FontMed,
                    TextAlignmentOptions.Top);
                storyCells.Add(cell);
            }
            SetArray(so, "_storyCells", storyCells);

            // ── Filter chips — node 14028:33827, 958x78 r100 ──────────────────
            GameObject chips = Card("ChipsRow", col, 10, 167, 958, 78, SprChipsRow);

            // Node order is TRENDING / FRIENDS / PUBLIC / MINE. The first two have no backend in
            // v1 and are rendered non-interactive at 45 % (SPEC § Goal), so PUBLIC is the one
            // that starts selected — and the gold chip therefore moves from slot 0 to slot 2.
            var chipButtons = new List<Button>();
            var chipRoots = new List<GameObject>();
            float[] chipX = { 14, 197, 361, 510 };
            float[] chipW = { 171, 152, 137, 108 };
            string[] chipKeys = { "GPS_VOTE_TRENDING", "GPS_VOTE_FRIENDS", "GPS_VOTE_PUBLIC", "GPS_VOTE_MINE" };
            for (int i = 0; i < 4; i++)
            {
                GameObject chip = Chip(chips.transform, "Chip" + i, chipX[i], 13, chipW[i],
                                       chipKeys[i], out Button b);
                chipRoots.Add(chip);
                chipButtons.Add(b);
            }
            SetArray(so, "_chipRoots", chipRoots);
            SetArray(so, "_chipButtons", chipButtons);

            Set(so, "_createButton",
                SmallButton(chips.transform, "CreateButton", 714, 12, 230, true, "GPS_VOTE_CREATE"));

            // ── The card list ─────────────────────────────────────────────────
            // A scroll view, because /vote/list returns more cards than the 1591px the container
            // has left. The three card shapes are authored INACTIVE as templates and cloned per
            // vote; nothing about a card's size is layout-driven, so each clone is positioned by
            // the controller and the content height is summed from what it actually placed.
            GameObject listView = Rect("VoteList", col, 10, 269, 958, 1591);
            var scroll = listView.AddComponent<ScrollRect>();
            listView.AddComponent<RectMask2D>();
            GameObject content = Rect("Content", listView.transform, 0, 0, 958, 1591);
            scroll.content = (RectTransform)content.transform;
            scroll.viewport = (RectTransform)listView.transform;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Set(so, "_listContent", (RectTransform)content.transform);
            Set(so, "_listScroll", scroll);

            Set(so, "_cardPhotoTemplate",
                VoteCard(content.transform, "CardPhotoTemplate", 530, VoteCardKind.PhotoGreen));
            Set(so, "_cardPhoto2Template",
                VoteCard(content.transform, "CardPhoto2Template", 450, VoteCardKind.PhotoBrown));
            Set(so, "_cardSimpleTemplate",
                VoteCard(content.transform, "CardSimpleTemplate", 232, VoteCardKind.Simple));
            Set(so, "_cardMultiTemplate",
                VoteCard(content.transform, "CardMultiTemplate", 200, VoteCardKind.Multi));

            // The empty state gets a CARD, not a bare line of text.
            //
            // The first version was a muted #B7C3D3 run floating on the raw background, and on the
            // Vote screen that background is open sky — the message was legible only if you knew
            // it was there. It reads as one more card in the feed now, which is both visible and
            // the language the rest of the screen already speaks. Drawn at the sprite's NATIVE
            // 958x200 so its baked gradient is not squashed (the linter's `nonuniform-stretch`).
            GameObject empty = Card("EmptyPanel", listView.transform, 0, 0, 958, 200, SprCardMulti);
            Set(so, "_emptyPanel", empty);
            Set(so, "_emptyLabel",
                TMP("EmptyLabel", empty.transform, 32, 0, 894, 200, "", 26f, White, FontMed,
                    TextAlignmentOptions.Midline));
            empty.SetActive(false);

            // The 48px author discs, in avatar_color order. Handed to each cloned card so
            // VoteCardView never touches an asset path either.
            var authorAvatars = new List<Sprite>();
            foreach (string colour in AvatarColours) authorAvatars.Add(Sprite(Avatar(colour, 48)));
            SetArray(so, "_authorAvatars", authorAvatars);

            CloneNavBar((RectTransform)root.transform, null, out _);

            var modal = (GameObject)PrefabUtility.InstantiatePrefab(modalPrefab, root.transform);
            modal.name = "VoteCreateModal";
            Set(so, "_createModal", modal.GetComponent<VoteCreateModalController>());

            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        enum VoteCardKind { PhotoGreen, PhotoBrown, Simple, Multi }

        /// <summary>
        /// One vote card template, authored INACTIVE. Four shapes, one routine, because the only
        /// differences are the photo header and whether the body carries bars or option pills —
        /// the title row, the footer and the two buttons are identical in all four nodes.
        /// </summary>
        static VoteCardView VoteCard(Transform parent, string name, float h, VoteCardKind kind)
        {
            string cardSprite = kind == VoteCardKind.PhotoGreen ? SprCardPhoto
                              : kind == VoteCardKind.PhotoBrown ? SprCardPhoto2
                              : kind == VoteCardKind.Simple     ? SprCardSimple
                                                                : SprCardMulti;

            GameObject card = Card(name, parent, 0, 0, 958, h, cardSprite);
            var view = card.AddComponent<VoteCardView>();
            var so = new SerializedObject(view);

            float bodyY = 0f;

            if (kind == VoteCardKind.PhotoGreen || kind == VoteCardKind.PhotoBrown)
            {
                bool green = kind == VoteCardKind.PhotoGreen;
                float photoH = green ? 300 : 220;
                // Inset by the card's 3px border — the photo is clipped by it on the node.
                GameObject photo = Rect("Photo", card.transform, 3, 3, 952, photoH - 3);
                Img(photo, green ? SprPhotoGreen : SprPhotoBrown, White);

                if (green)
                {
                    // Only the first photo node draws the Screenshot glyph (14028:33838); the
                    // second (14029:102242) has an author strip and nothing else.
                    GameObject ico = Rect("ScreenshotIcon", photo.transform, 446, 107, 80, 80);
                    Img(ico, IcoScreenshot, A(White, 0.55f, Hex("#2B4F2C")));
                }

                float authorY = (green ? 232 : 150) - 3;
                GameObject author = Rect("Author", photo.transform, 21, authorY, 400, 48);
                AvatarDisc(author.transform, 0, 0, 48, 0, SB(20), 12);
                Set(so, "_authorAvatar", author.transform.Find("Avatar").GetComponent<Image>());
                Set(so, "_authorInitial",
                    author.transform.Find("Avatar/Initial").GetComponent<TextMeshProUGUI>());
                Set(so, "_authorName",
                    TMP("AuthorName", author.transform, 60, 8.5f, 200, 31, "", SB(26), White,
                        FontSemi, TextAlignmentOptions.TopLeft));
                Set(so, "_authorWhen",
                    TMP("When", author.transform, 268, 11, 132, 26, "", 22f, Muted, FontMed,
                        TextAlignmentOptions.TopLeft));

                bodyY = photoH;
            }

            GameObject body = Rect("VoteBody", card.transform, 0, bodyY, 958, h - bodyY);

            // ── title row ──
            float titleY = kind == VoteCardKind.Simple ? 20 : kind == VoteCardKind.Multi ? 20 : 18;
            GameObject titleRow = Rect("VoteTitleRow", body.transform, 32, titleY, 894, 38);
            Set(so, "_question",
                TMP("Question", titleRow.transform, 0, 1, 700, 36, "", SB(30), White, FontSemi,
                    TextAlignmentOptions.TopLeft));

            // The reward pill hugs its label and is pinned to the row's RIGHT edge, so a longer
            // string grows leftwards instead of off the card.
            GameObject pill = Pill(titleRow.transform, "Pill", 894, 0, 38, Gold, SB(22),
                                   out TextMeshProUGUI pillLabel, rightAligned: true);
            Set(so, "_rewardPill", pill);
            Set(so, "_rewardLabel", pillLabel);

            if (kind == VoteCardKind.Multi)
            {
                GameObject options = Rect("Options", body.transform, 32, 72, 894, 38);
                var optionPills = new List<GameObject>();
                var optionLabels = new List<TextMeshProUGUI>();
                var layout = options.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                for (int i = 0; i < 4; i++)
                {
                    GameObject p = Pill(options.transform, "Option" + i, 0, 0, 38,
                                        OptionAccents[i], SB(22), out TextMeshProUGUI lbl,
                                        placeholder: "Name 00%", authoredWidth: 151f);
                    var le = p.AddComponent<LayoutElement>();
                    le.minHeight = le.preferredHeight = 38f;
                    optionPills.Add(p);
                    optionLabels.Add(lbl);
                }
                SetArray(so, "_optionPills", optionPills);
                SetArray(so, "_optionLabels", optionLabels);
            }
            else
            {
                float barY = kind == VoteCardKind.Simple ? 70 : 68;
                Set(so, "_yesFill", BarRow(body.transform, "BarYes", barY, "GPS_VOTE_YES", Green,
                                           out TextMeshProUGUI yesPct));
                Set(so, "_yesPct", yesPct);
                Set(so, "_noFill", BarRow(body.transform, "BarNo", barY + 43, "GPS_VOTE_NO", Blue,
                                          out TextMeshProUGUI noPct));
                Set(so, "_noPct", noPct);
            }

            // ── footer ──
            float footY = kind == VoteCardKind.Simple ? 156 : kind == VoteCardKind.Multi ? 124 : 154;
            GameObject footer = Rect("VoteFooter", body.transform, 32, footY, 894, 54);
            Set(so, "_meta",
                TMP("Meta", footer.transform, 0, 13, 420, 28, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.TopLeft));

            // GIFT sits at x=422 only on the photo cards; on the other two the VOTE button is
            // alone at the right edge (664). Authored on every card and hidden where the node
            // does not draw it, so one template covers both footers.
            Button gift = SmallButton(footer.transform, "GiftButton", 422, 0, 230, false,
                                      "GPS_VOTE_GIFT");
            Set(so, "_giftButton", gift);
            Set(so, "_voteButton",
                SmallButton(footer.transform, "VoteButton", 664, 0, 230, true, "GPS_VOTE_VOTE"));
            if (kind == VoteCardKind.Simple || kind == VoteCardKind.Multi)
                gift.gameObject.SetActive(false);

            so.ApplyModifiedPropertiesWithoutUndo();
            card.SetActive(false);
            return view;
        }

        /// <summary>
        /// One YES/NO bar row — label 70w, a 16px track that fills the space between, and the
        /// percentage at the right. The FILL is driven by WIDTH, never
        /// <see cref="Image.Type.Filled"/>: Filled discards 9-slicing and renders the cap as a
        /// thin wedge (GpsUiColor.SetBarFill documents the same trap).
        /// </summary>
        static Image BarRow(Transform parent, string name, float y, string labelKey, Color fill,
                            out TextMeshProUGUI pct)
        {
            GameObject row = Rect(name, parent, 32, y, 894, 31);
            TMP("BarLabel", row.transform, 0, 0, 70, 31, "", 26f, White, FontMed,
                TextAlignmentOptions.TopLeft, labelKey);

            // Node track: x=86, w=737..754 depending on how wide the percentage run is. Authored
            // at the widest common geometry (86 -> 824) so every card's track is one length.
            GameObject track = Rect("Track", row.transform, 86, 7.5f, 738, 16);
            Img(track, SprPill, TrackBg, Image.Type.Sliced, 8f);

            // Authored at the track's FULL width, not at zero. The fill is driven by WIDTH at
            // runtime (GpsUiColor.SetBarFill), and a zero-width 9-slice collapses its caps into an
            // oval — which the linter fails and which is what a card would flash before its first
            // Bind. GpsVoteScreenController binds each clone BEFORE activating it, so the authored
            // full-width state is never on screen.
            GameObject fillGo = Rect("Fill", track.transform, 0, 0, 738, 16);
            Image fillImg = Img(fillGo, SprPill, fill, Image.Type.Sliced, 8f);

            pct = TMP("Pct", row.transform, 824, 0, 70, 31, "", SB(26), White, FontSemi,
                      TextAlignmentOptions.TopRight);
            return fillImg;
        }

        /// <summary>
        /// A content-hugging pill: a 1px accent rim over a translucent accent fill
        /// (<c>bg-[rgba(a,b,c,0.18)] border border-[accent] rounded-[100px]</c>).
        ///
        /// TWO 9-SLICED CAPSULES, NOT A BAKED SPRITE. The width is content-driven — the Japanese
        /// labels and the live option names are all different lengths — so a fixed-size bake
        /// cannot serve it. The outer capsule is the accent at full strength and the inner one is
        /// inset 1px, which leaves exactly a 1px rim showing at any width. The inner fill is a
        /// REAL alpha through <see cref="T"/>, corrected for linear compositing — see the note on
        /// <see cref="TrackBg"/> for why pre-compositing it was wrong by ~48 units.
        /// </summary>
        static GameObject Pill(Transform parent, string name, float x, float y, float h,
                               Color accent, float labelSize, out TextMeshProUGUI label,
                               bool rightAligned = false, string placeholder = "+10 pts",
                               float authoredWidth = 116f)
        {
            // authoredWidth is the node's own pill width. The ContentSizeFitter below replaces it
            // as soon as the object is active; it exists so an INACTIVE template (which is what a
            // card template is, and what the fidelity linter measures) still reports a real width
            // rather than a zero-width 9-slice whose caps read as an oval.
            GameObject pill = Rect(name, parent, x, y, authoredWidth, h);
            if (rightAligned)
            {
                var prt = (RectTransform)pill.transform;
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 1f);
                prt.pivot = new Vector2(1f, 1f);   // grows leftwards from the row's right edge
            }

            // BOTH VISUALS ARE STRETCHED CHILDREN AND THE ROOT CARRIES NO IMAGE.
            //
            // Two lessons are baked into that sentence. First, the rim must be a HOLLOW ring, not
            // a tinted capsule: the original put an OPAQUE accent capsule on the root and inset a
            // translucent one inside it, so the fill composited over its own rim instead of over
            // the card and the reward pill measured (238,220,154) — solid gold — where the node
            // has (172,173,144). Second, the root must carry NO Image at all: `Image` is an
            // ILayoutElement, and a 9-sliced sprite reports its NATIVE 176px as a preferred width,
            // which the ContentSizeFitter takes as the max against the layout group's 110 and
            // hands back a pill 60 % too wide. Moving both sprites onto ignore-layout children
            // leaves the fitter measuring only the label.
            GameObject fill = Stretched("Fill", pill.transform, h);
            Img(fill, SprPill, T(accent, 0.18f, VoteCardBody), Image.Type.Sliced, h / 2f);

            GameObject rim = Stretched("Rim", pill.transform, h);
            Img(rim, SprPillRing, accent, Image.Type.Sliced, h / 2f);

            var layout = pill.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var fitter = pill.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // A representative PLACEHOLDER, not an empty string: the pill hugs its label through a
            // ContentSizeFitter, and an empty one authors a zero-width 9-slice whose caps collapse.
            // The controller overwrites it on every bind.
            label = TMP("Label", pill.transform, 0, 0, 0, h, placeholder, labelSize, accent, FontSemi,
                        TextAlignmentOptions.Midline);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
            return pill;
        }

        /// <summary>A full-size, ignore-layout child — a visual that must not be measured by the
        /// parent's ContentSizeFitter.</summary>
        static GameObject Stretched(string name, Transform parent, float h)
        {
            GameObject go = Rect(name, parent, 0, 0, 0, h);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            return go;
        }

        /// <summary>
        /// One filter chip. Two states, both authored, and the controller toggles which is
        /// active: OFF is the 9-sliced ring (a transparent interior, so the strip shows through),
        /// ON is the gold gradient capsule with a #422100 rim.
        /// </summary>
        static GameObject Chip(Transform parent, string name, float x, float y, float w,
                               string key, out Button button)
        {
            GameObject chip = Rect(name, parent, x, y, w, 52);
            // The dead chips are dimmed as a UNIT (ring + fill + label), which only a CanvasGroup
            // does — tinting one Image would leave the other two at full strength. Authored here
            // rather than added at runtime so the controller never has to.
            chip.AddComponent<CanvasGroup>();
            button = Btn(chip);
            var img = chip.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);   // the tap target; the visuals are the two children

            GameObject off = Rect("Off", chip.transform, 0, 0, w, 52);
            Stretch((RectTransform)off.transform);
            Img(off, SprChipRing, ChipRim, Image.Type.Sliced, 26f);

            GameObject on = Rect("On", chip.transform, 0, 0, w, 52);
            Stretch((RectTransform)on.transform);
            Img(on, SprPill, ChipOnRim, Image.Type.Sliced, 26f);
            GameObject onFill = Rect("Fill", on.transform, 0, 0, w, 50);
            var ofrt = (RectTransform)onFill.transform;
            ofrt.anchorMin = Vector2.zero; ofrt.anchorMax = Vector2.one;
            ofrt.pivot = new Vector2(0.5f, 0.5f);
            ofrt.offsetMin = new Vector2(1, 1); ofrt.offsetMax = new Vector2(-1, -1);
            Img(onFill, SprGoldSeg, White, Image.Type.Sliced, 25f);
            on.SetActive(false);

            TMP("Label", chip.transform, 0, 0, w, 52, "", SB(24), White, FontSemi,
                TextAlignmentOptions.Midline, key);
            return chip;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Modals
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The SEND GIFT / BUY ITEM modal. ONE controller for both because the two flows differ
        /// only in what the header names and which service call CONFIRM makes — the recipient
        /// line, the balance line, the amount presets and the error line are shared, and a second
        /// prefab would be the same 200 lines with two strings changed.
        /// </summary>
        static GameObject BuildGiftModalAsset()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject asset;
            try
            {
                var root = new GameObject("GiftSendModal", typeof(RectTransform));
                Stretch((RectTransform)root.transform);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var ctrl = root.AddComponent<GiftSendModalController>();
                var so = new SerializedObject(ctrl);

                GameObject backdrop = Rect("Backdrop", root.transform, 0, 0, 0, 0);
                Stretch((RectTransform)backdrop.transform);
                Img(backdrop, null, ADark(Color.black, 0.6f));

                // The atom's NATIVE 978x1400. S_SU_ModalPanel is a baked gradient card, not a
                // 9-sliced one, so drawing it at another aspect stretches its r50 corners into
                // ellipses — the linter's `nonuniform-stretch`, and the same trap Rule 21 names.
                // The contents are spaced to the taller panel rather than the sprite squashed to
                // the contents.
                GameObject panel = Rect("ModalPanel", root.transform, 96, 560, 978, 1400);
                Img(panel, SprModalPanel, White, Image.Type.Simple);

                TMP("Title", panel.transform, 0, 40, 978, 60, "", SB(40), Gold, FontSemi,
                    TextAlignmentOptions.Top, "GPS_GIFT_MODAL_TITLE");

                Set(so, "_recipient",
                    TMP("Recipient", panel.transform, 40, 170, 898, 50, "", SB(34), White,
                        FontSemi, TextAlignmentOptions.Top));
                Set(so, "_balance",
                    TMP("Balance", panel.transform, 40, 246, 898, 34, "", 26f, Muted, FontMed,
                        TextAlignmentOptions.Top));

                // Amount presets — 50 / 100 / 500 / 1000 (SPEC § Client data bindings).
                var amountButtons = new List<Button>();
                var amountRoots = new List<GameObject>();
                GameObject amounts = Rect("Amounts", panel.transform, 40, 340, 898, 120);
                for (int i = 0; i < GiftSendModalController.Presets.Length; i++)
                {
                    GameObject a = Rect("Amount" + i, amounts.transform, i * 228f, 0, 214, 120);
                    Img(a, SprModalRow, White, Image.Type.Simple);
                    amountRoots.Add(a);
                    amountButtons.Add(Btn(a));
                    TMP("Label", a.transform, 0, 0, 214, 120,
                        GiftSendModalController.Presets[i].ToString(), SB(40), White, FontSemi,
                        TextAlignmentOptions.Midline);
                    GameObject sel = Rect("Selected", a.transform, 0, 0, 214, 120);
                    Stretch((RectTransform)sel.transform);
                    Img(sel, SprPill, T(Gold, 0.22f, GiftPanelBody), Image.Type.Sliced, 24f);
                    sel.transform.SetAsFirstSibling();
                    sel.SetActive(false);
                }
                SetArray(so, "_amountButtons", amountButtons);
                SetArray(so, "_amountRoots", amountRoots);

                Set(so, "_status",
                    TMP("Status", panel.transform, 40, 500, 898, 90, "", 26f, Muted, FontMed,
                        TextAlignmentOptions.Top));

                Set(so, "_confirmButton",
                    MainButton(panel.transform, "ConfirmButton", 10, 1000, true,
                               "GPS_GIFT_MODAL_CONFIRM"));
                Set(so, "_cancelButton",
                    MainButton(panel.transform, "CancelButton", 10, 1150, false,
                               "GPS_GIFT_MODAL_CANCEL"));

                Set(so, "modalPanel", panel);
                Set(so, "backdrop", backdrop);
                so.ApplyModifiedPropertiesWithoutUndo();

                // ModalController.Awake force-deactivates both; authoring them ACTIVE throws a
                // UIParticle MissingReferenceException on every play-mode entry
                // (memory: reference_modal_children_author_inactive).
                panel.SetActive(false);
                backdrop.SetActive(false);

                asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabGiftModal);
                Object.DestroyImmediate(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return asset;
        }

        /// <summary>The CREATE VOTE modal: a question field, three expiry choices, and a submit.
        /// The options are fixed YES/NO in v1, so there is no option editor.</summary>
        static GameObject BuildVoteModalAsset()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject asset;
            try
            {
                var root = new GameObject("VoteCreateModal", typeof(RectTransform));
                Stretch((RectTransform)root.transform);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var ctrl = root.AddComponent<VoteCreateModalController>();
                var so = new SerializedObject(ctrl);

                GameObject backdrop = Rect("Backdrop", root.transform, 0, 0, 0, 0);
                Stretch((RectTransform)backdrop.transform);
                Img(backdrop, null, ADark(Color.black, 0.6f));

                GameObject panel = Rect("ModalPanel", root.transform, 96, 560, 978, 1400);
                Img(panel, SprModalPanel, White, Image.Type.Simple);

                TMP("Title", panel.transform, 0, 40, 978, 60, "", SB(40), Gold, FontSemi,
                    TextAlignmentOptions.Top, "GPS_VOTE_CREATE_TITLE");

                GameObject field = Rect("QuestionField", panel.transform, 40, 170, 898, 120);
                Img(field, SprSearchField, White, Image.Type.Simple);
                var input = field.AddComponent<TMP_InputField>();
                GameObject viewport = Rect("TextArea", field.transform, 24, 0, 850, 120);
                viewport.AddComponent<RectMask2D>();
                TextMeshProUGUI placeholder =
                    TMP("Placeholder", viewport.transform, 0, 0, 850, 120, "", 28f, Muted, FontReg,
                        TextAlignmentOptions.MidlineLeft, "GPS_VOTE_CREATE_HINT");
                TextMeshProUGUI text =
                    TMP("Text", viewport.transform, 0, 0, 850, 120, "", 28f, White, FontReg,
                        TextAlignmentOptions.MidlineLeft);
                input.textViewport = (RectTransform)viewport.transform;
                input.textComponent = text;
                input.placeholder = placeholder;
                input.lineType = TMP_InputField.LineType.SingleLine;
                input.characterLimit = 120;
                input.targetGraphic = field.GetComponent<Image>();
                Set(so, "_question", input);

                var expiryButtons = new List<Button>();
                var expiryRoots = new List<GameObject>();
                GameObject expiry = Rect("Expiry", panel.transform, 40, 340, 898, 100);
                string[] expiryKeys = { "GPS_VOTE_EXPIRY_24H", "GPS_VOTE_EXPIRY_3D", "GPS_VOTE_EXPIRY_7D" };
                for (int i = 0; i < 3; i++)
                {
                    GameObject e = Rect("Expiry" + i, expiry.transform, i * 305f, 0, 288, 100);
                    Img(e, SprModalRow, White, Image.Type.Simple);
                    expiryRoots.Add(e);
                    expiryButtons.Add(Btn(e));
                    TMP("Label", e.transform, 0, 0, 288, 100, "", SB(30), White, FontSemi,
                        TextAlignmentOptions.Midline, expiryKeys[i]);
                    GameObject sel = Rect("Selected", e.transform, 0, 0, 288, 100);
                    Stretch((RectTransform)sel.transform);
                    Img(sel, SprPill, T(Gold, 0.22f, GiftPanelBody), Image.Type.Sliced, 24f);
                    sel.transform.SetAsFirstSibling();
                    sel.SetActive(false);
                }
                SetArray(so, "_expiryButtons", expiryButtons);
                SetArray(so, "_expiryRoots", expiryRoots);

                TMP("OptionsNote", panel.transform, 40, 480, 898, 34, "", 24f, Muted, FontMed,
                    TextAlignmentOptions.Top, "GPS_VOTE_CREATE_OPTIONS");

                Set(so, "_status",
                    TMP("Status", panel.transform, 40, 540, 898, 90, "", 26f, Muted, FontMed,
                        TextAlignmentOptions.Top));

                Set(so, "_submitButton",
                    MainButton(panel.transform, "SubmitButton", 10, 1000, true,
                               "GPS_VOTE_CREATE_SUBMIT"));
                Set(so, "_cancelButton",
                    MainButton(panel.transform, "CancelButton", 10, 1150, false,
                               "GPS_GIFT_MODAL_CANCEL"));

                Set(so, "modalPanel", panel);
                Set(so, "backdrop", backdrop);
                so.ApplyModifiedPropertiesWithoutUndo();

                panel.SetActive(false);
                backdrop.SetActive(false);

                asset = PrefabUtility.SaveAsPrefabAsset(root, PrefabVoteModal);
                Object.DestroyImmediate(root);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return asset;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shared helpers — identical shapes to GpsProfilePackBuilder
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Clone the hub's own GPS nav bar. Every slot is made non-interactable, because from a
        /// sub-screen the bar is CHROME — except the slot this screen is not (so the Gift screen
        /// can leave its own Gift slot lit and inert, the way the hub leaves HOME lit).
        /// </summary>
        static void CloneNavBar(RectTransform parent, string? activeSlot, out Button? activeButton)
        {
            activeButton = null;
            if (!File.Exists(HubPrefab)) { Debug.LogWarning("[GpsGiftVoteBuilder] hub prefab not found"); return; }
            GameObject hub = PrefabUtility.LoadPrefabContents(HubPrefab);
            try
            {
                Transform nav = hub.transform.Find("GpsNavBar");
                if (nav == null) { Debug.LogWarning("[GpsGiftVoteBuilder] no GpsNavBar in hub"); return; }
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
            go.AddComponent<ButtonPressFeedback>();   // Rule 11 — every new player-facing Button
            return btn;
        }

        static Sprite? Sprite(string path)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) Debug.LogError("[GpsGiftVoteBuilder] sprite not found: " + path);
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
                    Debug.LogError("[GpsGiftVoteBuilder] sprite not found: " + spritePath);
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

        /// <summary>A `Main Buttons / Gold - Small` or `Silver - Small` instance: the shared atom,
        /// 9-sliced to the node's r20, with a SemiBold label at the calibrated size.</summary>
        static Button SmallButton(Transform parent, string name, float x, float y, float w,
                                  bool gold, string key)
        {
            GameObject go = Rect(name, parent, x, y, w, 54);
            var img = Img(go, gold ? SprGold : SprSilver, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = gold ? 18f / 20f : 25f / 20f;   // both -> the node's r20
            img.raycastTarget = true;

            Button button = Btn(go);
            TMP("Label", go.transform, 0, 0, w, 54, "", SB(39),
                gold ? ButtonInk : SilverInk, FontSemi, TextAlignmentOptions.Midline, key);
            return button;
        }

        /// <summary>A full-width `Main Buttons` instance — the modal actions. Same construction
        /// as ScoreUploadScreenBuilder.MainButton, which is the calibrated one.</summary>
        static Button MainButton(Transform parent, string name, float x, float y, bool gold,
                                 string key)
        {
            GameObject row = Rect(name + "Row", parent, x, y, 958, 120);

            // 560 is a representative Main Buttons width; the ContentSizeFitter below replaces it
            // on the first layout pass. Authored non-zero for the same reason the pills are: these
            // buttons live inside a modal panel that ships INACTIVE, so nothing rebuilds their
            // layout until the modal opens.
            GameObject go = Rect(name, row.transform, 0, 0, 560, 120);
            var grt = (RectTransform)go.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 1f);
            grt.pivot = new Vector2(0.5f, 1f);
            grt.anchoredPosition = Vector2.zero;

            var img = Img(go, gold ? SprGold : SprSilver, White, Image.Type.Sliced);
            img.pixelsPerUnitMultiplier = gold ? 18f / 20f : 25f / 20f;

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(48, 48, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            Button button = Btn(go);
            // The KEY is authored as the visible text, exactly as ScoreUploadScreenBuilder does.
            // LocalizedText replaces it on the first enable — but the AUTHORED string is what the
            // ContentSizeFitter sizes the button from, and an empty one collapses the 9-slice to
            // zero width (the linter's `9slice-collapse-x`).
            var label = TMP("Label", go.transform, 0, 0, 0, 120, key, SB(66),
                            gold ? ButtonInk : SilverInk, FontSemi,
                            TextAlignmentOptions.Midline, key);
            var le = label.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = 120;
            return button;
        }

        static TextMeshProUGUI TMP(string name, Transform parent, float x, float y, float w, float h,
                                   string text, float size, Color color, string fontPath,
                                   TextAlignmentOptions align, string? localizeKey = null)
        {
            var go  = Rect(name, parent, x, y, w, h);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            tmp.fontSize = size; tmp.color = color; tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode     = TextOverflowModes.Overflow;
            tmp.text = text ?? string.Empty;
            if (localizeKey != null)
            {
                var loc   = go.AddComponent<LocalizedText>();
                var locSo = new SerializedObject(loc);
                locSo.FindProperty("key").stringValue = localizeKey;
                locSo.ApplyModifiedPropertiesWithoutUndo();
            }
            return tmp;
        }

        // ── Colour helpers ─────────────────────────────────────────────────────

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        static float S2L(float c)
        {
            c /= 255f;
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        /// <summary>Figma's sRGB composite of <paramref name="overlay"/> at
        /// <paramref name="srgbAlpha"/> over <paramref name="over"/>, returned as the OPAQUE
        /// colour Unity must store to reproduce it in linear space.</summary>
        static Color A(Color overlay, float srgbAlpha, Color over)
        {
            float a = srgbAlpha;
            return new Color(
                S2L(overlay.r * 255f) * a + over.r * (1f - a),
                S2L(overlay.g * 255f) * a + over.g * (1f - a),
                S2L(overlay.b * 255f) * a + over.b * (1f - a), 1f);
        }

        /// <summary>
        /// A GENUINELY TRANSLUCENT overlay, with the alpha corrected for Unity's linear
        /// compositing. Use this — not <see cref="A"/> — whenever what is underneath is itself
        /// translucent or varies, which on these two screens is everything except the Screenshot
        /// glyph. <paramref name="backdrop"/> only shapes the SOLVE; the result still composites
        /// against whatever is really there, so a wrong-ish backdrop costs a few units of alpha
        /// rather than the whole colour.
        /// </summary>
        static Color T(Color overlay, float srgbAlpha, Color backdrop)
            => new Color(overlay.r, overlay.g, overlay.b,
                         LinearAlpha(srgbAlpha, overlay, backdrop));

        /// <summary>
        /// The alpha whose LINEAR blend of <paramref name="overlay"/> over
        /// <paramref name="backdrop"/> lands where Figma's sRGB blend at
        /// <paramref name="srgbAlpha"/> would. Solving
        /// <c>lin(T) = a'*lin(F) + (1-a')*lin(B)</c> for a', averaged over the channels that carry
        /// information. Mirrors <c>alpha_over()</c> in make_score_upload_panels.py.
        /// </summary>
        static float LinearAlpha(float srgbAlpha, Color overlay, Color backdrop)
        {
            float total = 0f; int n = 0;
            for (int c = 0; c < 3; c++)
            {
                float f = Channel(overlay, c) * 255f;
                float b = Channel(backdrop, c) * 255f;
                float lf = S2L(f), lb = S2L(b);
                if (Mathf.Abs(lf - lb) < 1e-4f) continue;      // no information in this channel
                float t = srgbAlpha * f + (1f - srgbAlpha) * b;
                total += Mathf.Clamp01((S2L(t) - lb) / (lf - lb));
                n++;
            }
            return n == 0 ? srgbAlpha : total / n;
        }

        static float Channel(Color c, int i) => i == 0 ? c.r : i == 1 ? c.g : c.b;

        /// <summary>Genuinely translucent dark overlay — the modal backdrop, which has no known
        /// backdrop to pre-composite against.</summary>
        static Color ADark(Color c, float srgbAlpha)
            => new Color(c.r, c.g, c.b, 1f - Mathf.Pow(1f - srgbAlpha, 2.2f));

        // ── SerializedObject helpers ──────────────────────────────────────────

        static void Set(SerializedObject so, string field, Object? value)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[GpsGiftVoteBuilder] no field '" + field + "'"); return; }
            p.objectReferenceValue = value;
        }

        static void SetArray<T>(SerializedObject so, string field, List<T> values) where T : Object
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[GpsGiftVoteBuilder] no field '" + field + "'"); return; }
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
                string name   = Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        /// <summary>
        /// Force every sprite this builder loads to import AS A SPRITE.
        ///
        /// A freshly baked PNG imports as a TEXTURE, and <c>LoadAssetAtPath&lt;Sprite&gt;</c> then
        /// returns null — which renders as a white box, not as an error
        /// (memory: reference_new_png_imports_as_texture_not_sprite). The 9-sliced atoms also need
        /// their border set, which is not something a default import produces.
        /// </summary>
        static void EnsureImport()
        {
            var single = new List<string>
            {
                BgGift, BgVote,
                SprGiftHero, SprSupporters, SprGolfers, SprBuyGifts, SprItemCell,
                SprStoriesRow, SprChipsRow, SprCardPhoto, SprCardSimple, SprCardMulti,
                SprPillRing,
                SprCardPhoto2, SprPhotoGreen, SprPhotoBrown, SprSeparator, SprStoryNew,
                SprPill, SprGold, SprSilver, SprGoldSeg, SprIconRing,
                SprModalPanel, SprModalRow, SprSearchField,
                IcoGift, IcoHeart, IcoStar, IcoPin, IcoSparkle, IcoScreenshot,
            };
            foreach (string colour in AvatarColours)
                foreach (int size in new[] { 72, 88, 48 })
                    single.Add(Avatar(colour, size));

            bool dirty = false;
            foreach (string p in single)
            {
                if (!File.Exists(p)) { Debug.LogWarning("[GpsGiftVoteBuilder] missing asset " + p); continue; }
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer == null)
                {
                    AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
                    importer = AssetImporter.GetAtPath(p) as TextureImporter;
                }
                if (importer == null) continue;
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single) continue;
                importer.textureType      = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled    = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                dirty = true;
            }

            // The two rings are the only assets THIS task bakes that are 9-sliced, so they are the
            // only ones that need a border. 88 all round, matching S_PillStadium and
            // S_SU_GoldSegment.
            foreach (string ringPath in new[] { SprChipRing, SprPillRing })
            {
                if (!File.Exists(ringPath)) { Debug.LogWarning("[GpsGiftVoteBuilder] missing " + ringPath); continue; }
                var ringImporter = AssetImporter.GetAtPath(ringPath) as TextureImporter;
                if (ringImporter == null)
                {
                    AssetDatabase.ImportAsset(ringPath, ImportAssetOptions.ForceSynchronousImport);
                    ringImporter = AssetImporter.GetAtPath(ringPath) as TextureImporter;
                }
                if (ringImporter != null &&
                    (ringImporter.textureType != TextureImporterType.Sprite ||
                     ringImporter.spriteBorder != new Vector4(88, 88, 88, 88)))
                {
                    ringImporter.textureType      = TextureImporterType.Sprite;
                    ringImporter.spriteImportMode = SpriteImportMode.Single;
                    ringImporter.mipmapEnabled    = false;
                    ringImporter.alphaIsTransparency = true;
                    ringImporter.spriteBorder     = new Vector4(88, 88, 88, 88);
                    ringImporter.SaveAndReimport();
                    dirty = true;
                }
            }

            if (dirty) AssetDatabase.Refresh();
        }
    }
}
