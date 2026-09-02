// gps_profile_pack — builder for the three GPS profile screens. iter-3.
// Re-runnable; overwrites the prefabs on each run.
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
    public static class GpsProfilePackBuilder
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        const string HubPrefab     = "Assets/Prefabs/UI/Gps/GpsHubScreen.prefab";
        const string PrefabProfile = "Assets/Prefabs/UI/Gps/GpsProfileScreen.prefab";
        const string PrefabAvatar  = "Assets/Prefabs/UI/Gps/GpsAvatarScreen.prefab";
        const string PrefabBadges  = "Assets/Prefabs/UI/Gps/GpsBadgesScreen.prefab";

        // ── Shared sprites ────────────────────────────────────────────────────
        const string SprPill         = "Assets/Art/Tournaments/S_PillStadium.png";
        const string SprHero         = "Assets/Art/UI/Gps/S_HUB_HeroPanel.png";
        const string SprIconRingTile = "Assets/Art/UI/Gps/S_GpsIconRing_Tile.png"; // 88px — correct atom
        const string SprSilver       = "Assets/Art/RosterScreen/ButtonCancel.png";  // Main Buttons Silver

        // ── Baked profile sprites ─────────────────────────────────────────────
        // Backgrounds are PER SCREEN, matched to each Figma frame's own `Backgrounds` plate —
        // the same thing ScoreUploadScreenBuilder does for its six steps (:82-86). Measured against
        // all 47 project backgrounds: Badges' plate is BYTE-IDENTICAL (mean |dRGB| 0.000, max 0) to
        // the BG_SU_GpsProof.png that score_upload already added, so it is reused rather than
        // duplicated. Profile and Avatar had no match closer than 25.3 / 45.5, so their plates were
        // imported as assets the same way the BG_SU_* pair was.
        const string BgProfile          = "Assets/Art/UI/Gps/Backgrounds/BG_PROF_Profile.png";
        const string BgAvatar           = "Assets/Art/UI/Gps/Backgrounds/BG_PROF_Avatar.png";
        const string BgBadges           = "Assets/Art/UI/Gps/Backgrounds/BG_SU_GpsProof.png";
        const string SprBadgeFrame2     = "Assets/Art/UI/Gps/S_PROF_BadgeFrame2.png";  // earned, 2px
        const string SprBadgeFrame1     = "Assets/Art/UI/Gps/S_PROF_BadgeFrame1.png";  // locked, 1px
        const string SprUnlockTile      = "Assets/Art/UI/Gps/S_PROF_UnlockTile.png";
        const string SprLevelPill       = "Assets/Art/UI/Gps/S_PROF_LevelPill.png";
        const string SprUnlockPanel     = "Assets/Art/UI/Gps/S_PROF_UnlockPanel.png";
        const string SprHeroPanel       = "Assets/Art/UI/Gps/S_PROF_HeroPanel.png";
        const string SprTrustPanel      = "Assets/Art/UI/Gps/S_PROF_TrustPanel.png";
        const string SprQuickStatTile   = "Assets/Art/UI/Gps/S_PROF_QuickStatTile.png";
        const string SprGiftReceived    = "Assets/Art/UI/Gps/S_PROF_GiftTileReceived.png";
        const string SprGiftSent        = "Assets/Art/UI/Gps/S_PROF_GiftTileSent.png";
        const string SprShortcutTile    = "Assets/Art/UI/Gps/S_PROF_ShortcutTile.png";
        const string SprRecentRounds    = "Assets/Art/UI/Gps/S_PROF_RecentRoundsPanel.png";
        const string SprAvatarStage     = "Assets/Art/UI/Gps/S_PROF_AvatarStage.png";
        const string SprXpPanel         = "Assets/Art/UI/Gps/S_PROF_XpPanel.png";
        const string SprEvolutionPanel  = "Assets/Art/UI/Gps/S_PROF_EvolutionPanel.png";
        const string SprStatusPanel     = "Assets/Art/UI/Gps/S_PROF_StatusPanel.png";
        const string SprCollectionPanel = "Assets/Art/UI/Gps/S_PROF_CollectionPanel.png";
        const string SprSectionPanel    = "Assets/Art/UI/Gps/S_PROF_SectionPanel.png";

        // ── Icons ─────────────────────────────────────────────────────────────
        // auth_golf_profile §5 — the four hero avatar rings, in ColorIds order
        // (pink, green, blue, gold). Each is the icon-ring atom with the swatch gradient as its
        // fill; baked by Docs/Scripts/make_gps_auth_swatches.py.
        static readonly string[] AvatarRings =
        {
            "Assets/Art/UI/Gps/S_AUTH_AvatarRing_Pink.png",
            "Assets/Art/UI/Gps/S_AUTH_AvatarRing_Green.png",
            "Assets/Art/UI/Gps/S_AUTH_AvatarRing_Blue.png",
            "Assets/Art/UI/Gps/S_AUTH_AvatarRing_Gold.png",
        };

        const string IcoStar    = "Assets/Art/UI/Gps/ICO_GpsStar.png";
        const string IcoHeart   = "Assets/Art/UI/Gps/ICO_GpsHeart.png";
        const string IcoPin     = "Assets/Art/UI/Gps/ICO_GpsPin.png";
        const string IcoSparkle = "Assets/Art/UI/Gps/ICO_GpsSparkle.png";
        const string IcoRounds  = "Assets/Art/UI/Gps/ICO_GpsRounds.png";
        const string IcoGift    = "Assets/Art/UI/Gps/ICO_GpsGift.png";

        // ── Fonts ─────────────────────────────────────────────────────────────
        const string FontSemi = "Assets/Fonts/Rubik-SemiBold SDF.asset";
        const string FontReg  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";
        // Node calls these "Rubik:Medium". The project ships SemiBold + the variable
        // face only, so Medium resolves to the variable face (same as Regular) — the
        // weight difference is a known-unequal, recorded in the fidelity table.
        const string FontMed  = "Assets/Fonts/Rubik-VariableFont_wght SDF.asset";

        // ── Colours ───────────────────────────────────────────────────────────
        static readonly Color White     = Color.white;
        static readonly Color Gold      = Hex("#EEDC9A");
        static readonly Color Green     = Hex("#7ED488");
        static readonly Color Muted     = Hex("#B7C3D3");
        static readonly Color SilverInk = Hex("#1E293B");
        // Hero stat colours are per-column in the node (14025:33352/33355/33358/33361):
        // FOLLOWERS pink, ROUNDS white, AVATAR gold, POINTS green. The build had 3 of 4 white.
        static readonly Color StatPink  = Hex("#F07F9C");
        static readonly Color StatGreen = Hex("#7ED488");
        static readonly Color MintText  = Hex("#BFE8CC");   // node 14026:33492 rank title

        static readonly Color TrackBg   = A(White,          0.15f, Hex("#0A2037"));
        static readonly Color TrackFill = A(Hex("#EEDC9A"), 0.80f, Hex("#0A2037"));

        const float PillBorder = 88f;
        static float F(float px) => px;

        // ═══════════════════════════════════════════════════════════════════════
        // Menu entry points
        // ═══════════════════════════════════════════════════════════════════════

        [MenuItem("GOLFIN/Gps/Build Profile Screen", priority = 200)]
        public static void BuildProfile()
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = BuildProfileScreen();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                // gps_polish — the shared polish pass. Runs LAST, on the finished root, so it sees
                // every layer this builder authored (SPEC § Architecture: the additions go INTO
                // the existing builders, which stay the prefab source of truth).
                GpsPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabProfile);
                Debug.Log("[GpsProfilePackBuilder] Built " + PrefabProfile);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        [MenuItem("GOLFIN/Gps/Build Avatar Screen", priority = 201)]
        public static void BuildAvatar()
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = BuildAvatarScreen();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                // gps_polish — the shared polish pass. Runs LAST, on the finished root, so it sees
                // every layer this builder authored (SPEC § Architecture: the additions go INTO
                // the existing builders, which stay the prefab source of truth).
                GpsPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabAvatar);
                Debug.Log("[GpsProfilePackBuilder] Built " + PrefabAvatar);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        [MenuItem("GOLFIN/Gps/Build Badges Screen", priority = 202)]
        public static void BuildBadges()
        {
            EnsureImport();
            EnsureDir("Assets/Prefabs/UI/Gps");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = BuildBadgesScreen();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                // gps_polish — the shared polish pass. Runs LAST, on the finished root, so it sees
                // every layer this builder authored (SPEC § Architecture: the additions go INTO
                // the existing builders, which stay the prefab source of truth).
                GpsPolishBuilder.Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabBadges);
                Debug.Log("[GpsProfilePackBuilder] Built " + PrefabBadges);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
        }

        [MenuItem("GOLFIN/Gps/Build All GPS Profile Screens", priority = 203)]
        public static void BuildAll()
        {
            BuildProfile();
            BuildAvatar();
            BuildBadges();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Screen builders
        // ═══════════════════════════════════════════════════════════════════════

        // ── GPS Profile Screen (Figma 14025:33087) ────────────────────────────

        static GameObject BuildProfileScreen()
        {
            var root = new GameObject("GpsProfileScreen", typeof(RectTransform));
            var rt   = (RectTransform)root.transform;
            Stretch(rt);

            var ctrl = root.AddComponent<GpsProfileScreenController>();
            var so   = new SerializedObject(ctrl);

            // Background — Home Background.png sprite, Simple, white, stretch-to-fill.
            var bg = Rect("Background", rt, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgProfile, White, Image.Type.Simple);

            // Content column. NO BackRow: "back to game" belongs to the hub only (Cesar,
            // 2026-09-02), and none of the three Figma frames draws one. Avatar and Badges return
            // to Profile through the nav bar's Profile slot. Because the row is gone, every panel
            // now sits at its RAW node y — the +65 offset it used to need is deleted with it.
            var col = (RectTransform)Rect("ContentContainer", rt, 96, 361, 978, 1860).transform;

            // ── Hero panel — node 14025:33344, 958x449 (node y=0 +65 BackRow) ─
            // Node column: pt30, avatar 170, gap10, name 54, gap10, sub 28, stats row pt16.
            var hero = Card("HeroPanel", col, 10, 0, 958, 449, SprHeroPanel);

            // Avatar 170x170 at node (394, 30) — TOP of the column, NOT vertically centred.
            var avatarCircle = Rect("AvatarCircle", hero.transform, 394f, 30f, 170f, 170f);
            // auth_golf_profile §5 — ONE Image, carrying both the fill and the gold rim.
            //
            // It used to be two: a navy `AvatarBg` capsule with a `GoldRing` overlay on top. That
            // could never show an avatar colour, because S_GpsIconRing_Tile is a FILLED navy
            // circle with a gold rim (make_gps_icon_ring bakes the fill out to the outer radius),
            // not an annulus — so the disc underneath it was completely covered. The first
            // attempt at this feature swapped that hidden disc and rendered identically navy in
            // all four colours. The colour now lives on the ring's own fill, which is the same
            // atom at the same geometry with a different fill token.
            var avatarBg = Rect("GoldRing", avatarCircle.transform, 0, 0, 170, 170);
            Img(avatarBg, AvatarRings[3], White);           // gold — the null/unknown fallback
            Set(so, "_avatarDisc", avatarBg.GetComponent<Image>());
            var discSprites = so.FindProperty("_avatarDiscSprites");
            discSprites.arraySize = AvatarRings.Length;
            for (int i = 0; i < AvatarRings.Length; i++)
                discSprites.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(AvatarRings[i]);
            // Node 14025:33347: 71px, WHITE (the build had 84px gold).
            var avatarInitial = TMP("AvatarInitial", avatarCircle.transform, 0, 43, 170, 84,
                "C", F(71), White, FontSemi, TextAlignmentOptions.Top);
            Set(so, "_avatarInitial", avatarInitial);

            // Name node y=210 (54 SemiBold #eedc9a); Sub node y=284 (28 MEDIUM #b7c3d3).
            var playerName = TMP("PlayerName", hero.transform, 0, 210, 958, 64,
                "CRATILO", F(54), Gold, FontSemi, TextAlignmentOptions.Top);
            Set(so, "_playerName", playerName);
            var playerSub = TMP("PlayerSub", hero.transform, 0, 284, 958, 33,
                "@cratilo · HC 18.4 · Tokyo Golf Club", F(28), Muted, FontMed, TextAlignmentOptions.Top);
            Set(so, "_playerSub", playerSub);

            // Stats row node 14025:33350 — (40, 327), 878x98, four 219.5 columns.
            // Values are 44px and COLOUR-CODED per node; the build had 32px and 3 of 4 white.
            const float StatW = 219.5f, StatX = 40f, StatY = 327f + 16f;   // +16 = row's pt-16
            var statFollowers = TMP("StatFollowers", hero.transform, StatX, StatY, StatW, 52,
                "890", F(44), StatPink, FontSemi, TextAlignmentOptions.Top);
            TMP("LblFollowers", hero.transform, StatX, StatY + 56, StatW, 26, "",
                F(22), Muted, FontMed, TextAlignmentOptions.Top, "GPS_PROFILE_STAT_FOLLOWERS");
            Set(so, "_statFollowers", statFollowers);

            var statRounds = TMP("StatRounds", hero.transform, StatX + StatW, StatY, StatW, 52,
                "23", F(44), White, FontSemi, TextAlignmentOptions.Top);
            TMP("LblRounds", hero.transform, StatX + StatW, StatY + 56, StatW, 26, "",
                F(22), Muted, FontMed, TextAlignmentOptions.Top, "GPS_PROFILE_STAT_ROUNDS");
            Set(so, "_statRounds", statRounds);

            var statAvatar = TMP("StatAvatar", hero.transform, StatX + StatW * 2, StatY, StatW, 52,
                "Lv.12", F(44), Gold, FontSemi, TextAlignmentOptions.Top);
            TMP("LblAvatar", hero.transform, StatX + StatW * 2, StatY + 56, StatW, 26, "",
                F(22), Muted, FontMed, TextAlignmentOptions.Top, "GPS_PROFILE_STAT_AVATAR");
            Set(so, "_statAvatar", statAvatar);

            var statPoints = TMP("StatPoints", hero.transform, StatX + StatW * 3, StatY, StatW, 52,
                "2,480", F(44), StatGreen, FontSemi, TextAlignmentOptions.Top);
            TMP("LblPoints", hero.transform, StatX + StatW * 3, StatY + 56, StatW, 26, "",
                F(22), Muted, FontMed, TextAlignmentOptions.Top, "GPS_PROFILE_STAT_POINTS");
            Set(so, "_statPoints", statPoints);

            // ── Trust panel — node 14025:33363, 958x140 (node y=473 +65) ──────
            // Node paints the ✓, the title AND the percentage all in #7ed488; the build had
            // a muted title and a gold percentage, and dropped the note row.
            var trust = Card("TrustPanel", col, 10, 473, 958, 140, SprTrustPanel);
            // The localized value already carries the mark ("✓ TRUST LEVEL" / "✓ 信頼度"),
            // so there is deliberately no separate mark glyph — one rendered it twice.
            TMP("TrustLbl", trust.transform, 32, 20, 460, 36, "",
                F(30), StatGreen, FontSemi, TextAlignmentOptions.Left, "GPS_PROFILE_TRUST");
            var trustLevel = TMP("TrustLevel", trust.transform, 558, 20, 368, 40,
                "87%", F(34), StatGreen, FontSemi, TextAlignmentOptions.Right);
            Set(so, "_trustLevel", trustLevel);
            // Track: node 894x16, r8, bg white@0.15, fill #7ed488 — width-driven (see Bar()).
            var tFillImg = Bar(trust.transform, "TrustTrack", 32, 68, 894, 16, StatGreen);
            Set(so, "_trustTrackFill", tFillImg);
            TMP("TrustNote", trust.transform, 32, 92, 600, 28, "",
                F(24), Muted, FontMed, TextAlignmentOptions.Top, "GPS_PROFILE_TRUST_NOTE");

            // ── Quick stats row [10, 501, 958, 120] ───────────────────────────
            var bestTile = Card("BestTile", col, 10, 637, 307, 119, SprQuickStatTile);
            TMP("BestLbl", bestTile.transform, 0, 12, 308, 28, "",
                F(22), Muted, FontReg, TextAlignmentOptions.Center, "GPS_PROFILE_BEST");
            var statBest = TMP("StatBest", bestTile.transform, 0, 48, 308, 52,
                "89", F(42), Gold, FontSemi, TextAlignmentOptions.Center);
            Set(so, "_statBest", statBest);

            var avgTile = Card("AvgTile", col, 335.33f, 637, 307, 119, SprQuickStatTile);
            TMP("AvgLbl", avgTile.transform, 0, 12, 308, 28, "",
                F(22), Muted, FontReg, TextAlignmentOptions.Center, "GPS_PROFILE_AVERAGE");
            var statAvg = TMP("StatAvgScore", avgTile.transform, 0, 48, 308, 52,
                "96.3", F(42), White, FontSemi, TextAlignmentOptions.Center);
            Set(so, "_statAvgScore", statAvg);

            var puttsTile = Card("PuttsTile", col, 660.67f, 637, 307, 119, SprQuickStatTile);
            TMP("PuttsLbl", puttsTile.transform, 0, 12, 308, 28, "",
                F(22), Muted, FontReg, TextAlignmentOptions.Center, "GPS_PROFILE_AVG_PUTTS");
            TMP("StatPutts", puttsTile.transform, 0, 48, 308, 52,
                "—", F(42), White, FontSemi, TextAlignmentOptions.Center);

            // ── Gift totals — node 14025:33384, two 470x118 tiles, gap 18 ─────
            // Node column is `items-center`: the icon+label row AND the value are both CENTRED,
            // not left-aligned. Each tile carries a 24px Gift icon before its label, and the
            // RECEIVED tile is PINK throughout (#f07f9c) while SENT is gold (#eedc9a) — the build
            // had left-aligned text, no icon, and a gold number on the pink tile.
            var giftRcv = Card("GiftReceived", col, 10, 780, 470, 118, SprGiftReceived);
            var giftsReceived = GiftTile(giftRcv, "GiftRcv", "GPS_PROFILE_GIFTS_IN", StatPink, "17");
            Set(so, "_giftsReceived", giftsReceived);

            var giftSent = Card("GiftSent", col, 498, 780, 470, 118, SprGiftSent);
            GiftTile(giftSent, "GiftSent", "GPS_PROFILE_GIFTS_OUT", Gold, "—");

            // ── Shortcuts row [10, 801, 958, 190] — order: BADGES / GIFT SHOP / MY AVATAR ──
            // Shape A fix: shortcuts reordered (BADGES/GIFT/AVATAR not BADGES/AVATAR/GIFT)
            // Shape A fix: each shortcut gets IconRing (72px, SprIconRingTile) + icon child

            // BADGES at x=10
            var badgesShortcut = Card("BadgesShortcut", col, 10, 922, 307, 174, SprShortcutTile);
            var badgesRing = Rect("IconRing", badgesShortcut.transform, (307f - 72f) / 2f, 18, 72, 72);
            Img(badgesRing, SprIconRingTile, White, Image.Type.Simple);
            var badgesIcon = Rect("Icon", badgesRing.transform, 18, 18, 36, 36);
            Img(badgesIcon, IcoStar, White);
            TMP("BadgesLbl", badgesShortcut.transform, 0, 98, 307, 28, "",
                F(24), White, FontSemi, TextAlignmentOptions.Center, "GPS_PROFILE_SHORTCUT_BADGES");
            var badgesSubTmp = TMP("BadgesSub", badgesShortcut.transform, 0, 134, 307, 24,
                "—", F(20), Muted, FontMed, TextAlignmentOptions.Center);
            Set(so, "_badgesShortcutSub", badgesSubTmp);
            Set(so, "_badgesShortcutButton", Btn(badgesShortcut));

            // GIFT SHOP at x=330
            var giftShortcut = Card("GiftShortcut", col, 335.33f, 922, 307, 174, SprShortcutTile);
            var giftRing = Rect("IconRing", giftShortcut.transform, (307f - 72f) / 2f, 18, 72, 72);
            Img(giftRing, SprIconRingTile, White, Image.Type.Simple);
            var giftIcon = Rect("Icon", giftRing.transform, 18, 18, 36, 36);
            Img(giftIcon, IcoGift, White);
            TMP("GiftLbl", giftShortcut.transform, 0, 98, 307, 28, "",
                F(24), White, FontSemi, TextAlignmentOptions.Center, "GPS_PROFILE_SHORTCUT_SHOP");
            TMP("GiftSub", giftShortcut.transform, 0, 134, 307, 24, "",
                F(20), Muted, FontMed, TextAlignmentOptions.Center, "GPS_PROFILE_SHOP_SUB");
            Set(so, "_giftShopButton", Btn(giftShortcut));

            // MY AVATAR at x=650
            var avatarShortcut = Card("AvatarShortcut", col, 660.67f, 922, 307, 174, SprShortcutTile);
            var avatarRing = Rect("IconRing", avatarShortcut.transform, (307f - 72f) / 2f, 18, 72, 72);
            Img(avatarRing, SprIconRingTile, White, Image.Type.Simple);
            var avIcon = Rect("Icon", avatarRing.transform, 18, 18, 36, 36);
            Img(avIcon, IcoSparkle, White);
            TMP("AvatarLbl", avatarShortcut.transform, 0, 98, 307, 28, "",
                F(24), White, FontSemi, TextAlignmentOptions.Center, "GPS_PROFILE_SHORTCUT_AVATAR");
            var avatarSubTmp = TMP("AvatarSub", avatarShortcut.transform, 0, 134, 307, 24,
                "—", F(20), Muted, FontMed, TextAlignmentOptions.Center);
            Set(so, "_avatarShortcutSub", avatarSubTmp);
            Set(so, "_avatarShortcutButton", Btn(avatarShortcut));

            // ── Recent rounds panel [10, 1011, 958, 450] ──────────────────────
            var roundsPanel = Card("RecentRoundsPanel", col, 10, 1120, 958, 343, SprRecentRounds);
            Set(so, "_roundsPanel", roundsPanel);
            TMP("RoundsTitle", roundsPanel.transform, 24, 16, 910, 40, "",
                F(32), White, FontSemi, TextAlignmentOptions.MidlineLeft, "GPS_PROFILE_RECENT");

            // Empty state, mirroring the hub (GpsHubScreenController._roundsEmpty): the panel is
            // never hidden — at zero rounds it carries this line instead of collapsing.
            var roundsEmpty = TMP("RoundsEmpty", roundsPanel.transform, 32, 140, 894, 40, "",
                F(26), Muted, FontMed, TextAlignmentOptions.Center, "GPS_HUB_NO_ROUNDS");
            Set(so, "_roundsEmpty", roundsEmpty);

            var roundRowsProp = so.FindProperty("_roundRows");
            roundRowsProp!.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var rowGo = Rect($"RoundRow{i}", roundsPanel.transform, 24, 68 + i * 184, 910, 170);
                Panel("RowBg", rowGo.transform, 0, 0, 910, 170, GpsUiColor.BadgeNavy, 16);
                TMP($"RoundDate{i}", rowGo.transform, 16, 12, 400, 30, "—",
                    F(26), Muted, FontReg, TextAlignmentOptions.MidlineLeft);
                TMP($"RoundScore{i}", rowGo.transform, 850, 12, 60, 30, "—",
                    F(28), Gold, FontSemi, TextAlignmentOptions.MidlineRight);
                var rr = rowGo.AddComponent<GpsHubRoundRow>();
                rowGo.SetActive(false);
                roundRowsProp.GetArrayElementAtIndex(i).objectReferenceValue = rr;
            }

            // ── Edit Profile button [10, 1487, 958, 120] — MainButton idiom (content-hugging ~545px)
            // 958-wide row → centred 0-width child with HLG+CSF so the button hugs its label.
            var editBtnRow = Rect("EditProfileButtonRow", col, 10, 1487, 958, 120);
            var editBtnGo  = new GameObject("EditProfileButton", typeof(RectTransform));
            editBtnGo.transform.SetParent(editBtnRow.transform, false);
            var editBtnRt  = (RectTransform)editBtnGo.transform;
            editBtnRt.anchorMin = editBtnRt.anchorMax = new Vector2(0.5f, 1f);
            editBtnRt.pivot     = new Vector2(0.5f, 1f);
            editBtnRt.anchoredPosition = Vector2.zero;
            editBtnRt.sizeDelta = new Vector2(0, 120);
            var editBtnImg = Img(editBtnGo, SprSilver, White, Image.Type.Sliced);
            editBtnImg.pixelsPerUnitMultiplier = 25f / 20f;
            var editBtnHlg = editBtnGo.AddComponent<HorizontalLayoutGroup>();
            editBtnHlg.padding = new RectOffset(48, 48, 0, 0);
            editBtnHlg.childAlignment = TextAnchor.MiddleCenter;
            editBtnHlg.childControlWidth = true; editBtnHlg.childControlHeight = true;
            editBtnHlg.childForceExpandWidth = false; editBtnHlg.childForceExpandHeight = false;
            var editBtnCsf = editBtnGo.AddComponent<ContentSizeFitter>();
            editBtnCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            editBtnCsf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
            var editBtnComp = Btn(editBtnGo);
            // Inert in v1 (SPEC-approved deviation) — but Unity's DEFAULT disabledColor is
            // (200,200,200,128), i.e. alpha 128, so `interactable = false` alone renders the
            // button washed out and half-transparent. The node draws it solid. Same fix the hub
            // uses on its nav slots: opaque disabled colour, so disabling gates the TAP without
            // greying the artwork.
            var editColors = editBtnComp.colors;
            editColors.disabledColor = Color.white;
            editBtnComp.colors = editColors;
            editBtnComp.interactable = false;
            var editLabel = TMP("Label", editBtnGo.transform, 0, 0, 0, 120, "",
                F(59), SilverInk, FontSemi, TextAlignmentOptions.Midline, "GPS_PROFILE_EDIT");
            var editLabelLe = editLabel.gameObject.AddComponent<LayoutElement>();
            editLabelLe.minHeight = 120; editLabelLe.preferredHeight = 120;
            Set(so, "_editProfileButton", editBtnComp);

            CloneNavBar(rt);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── GPS My Avatar Screen (Figma 14026:33187) ──────────────────────────

        static GameObject BuildAvatarScreen()
        {
            var root = new GameObject("GpsAvatarScreen", typeof(RectTransform));
            var rt   = (RectTransform)root.transform;
            Stretch(rt);

            var ctrl = root.AddComponent<GpsAvatarScreenController>();
            var so   = new SerializedObject(ctrl);

            // Background — Home Background.png sprite, Simple, white, stretch-to-fill.
            var bg = Rect("Background", rt, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgAvatar, White, Image.Type.Simple);

            var col = (RectTransform)Rect("ContentContainer", rt, 96, 361, 978, 1860).transform;

            // ── Avatar Stage — node 14026:33444, 958x840 ─────────────────────
            // The stage OWNS the figure, the equip slots and the level row; the previous build
            // shrank it to 620 and lifted the slots and level row out into the column, which is
            // why nothing lined up with the frame.
            var stage = Card("AvatarStage", col, 10, 0, 958, 840, SprAvatarStage);

            // Figure — node 14026:33445 is literally named "Avatar Figure (Main Menu Character
            // instance)": a 560x600 frame CLIPPING a 725.4x1569.84 instance at (-82.7,-400), i.e.
            // the Home-screen character art scaled to cover and pushed up so the head sits at the
            // top. The controller sources it exactly as HomeScreenController does.
            var maskGo = Rect("AvatarMask", stage.transform, 199, 28, 560, 600);
            maskGo.AddComponent<RectMask2D>();
            // 560 / (1090/1907) = 979.8 — cover the window's width at the sprite's true aspect and
            // top-align, so the head sits at the top of the crop exactly as the node frames it.
            var figureGo = Rect("CharacterFigure", maskGo.transform, 0f, 0f, 560f, 979.8f);
            var figureImg = figureGo.AddComponent<Image>();
            figureImg.preserveAspect = false; figureImg.raycastTarget = false;
            Set(so, "_characterFigure", figureImg);

            // Equip slots — node 14026:33450, (237,644) 484x111, five 84-wide slots on a 100 pitch.
            // Ring 84 with its OWN 40px icon at (22,22); v1 renders every slot at the "off" state.
            var equipPanel = Rect("EquipSlots", stage.transform, 237, 644, 484, 111);
            string[] slotNames     = { "CAP", "SHIRT", "GLOVE", "SHOES", "CLUB" };
            string[] slotKeys      = { "GPS_AVATAR_SLOT_CAP", "GPS_AVATAR_SLOT_SHIRT",
                                       "GPS_AVATAR_SLOT_GLOVE", "GPS_AVATAR_SLOT_SHOES",
                                       "GPS_AVATAR_SLOT_CLUB" };
            string[] slotIconPaths = { IcoStar, IcoSparkle, IcoHeart, IcoPin, IcoRounds };
            for (int i = 0; i < 5; i++)
            {
                var slotGo     = Rect("Slot_" + slotNames[i], equipPanel.transform, i * 100f, 0, 84, 111);
                var slotRingGo = Rect("IconRing", slotGo.transform, 0, 0, 84, 84);
                Img(slotRingGo, SprIconRingTile, new Color(1f, 1f, 1f, 0.5f));
                var slotIconGo = Rect("Icon", slotRingGo.transform, 22, 22, 40, 40);
                Img(slotIconGo, slotIconPaths[i], new Color(1f, 1f, 1f, 0.5f));
                TMP("SlotLabel", slotGo.transform, 0, 90, 84, 21, "",
                    F(20), Muted, FontMed, TextAlignmentOptions.Top, slotKeys[i]);
            }

            // Level row — node 14026:33489, (297,771) 364x45. The node's own mock is the long
            // "AMATEUR GOLFER"; with a short rank like "ROOKIE" a fixed 364-wide row leaves the
            // pair visibly left of centre, so the pill and the title are centred as ONE group by a
            // layout so it holds for every rank and both languages.
            var levelRow = Rect("LevelRow", stage.transform, 0, 771, 958, 45);
            var lrHlg = levelRow.AddComponent<HorizontalLayoutGroup>();
            lrHlg.childAlignment = TextAnchor.MiddleCenter;
            lrHlg.spacing = 14f;
            lrHlg.childControlWidth = true;  lrHlg.childControlHeight = true;
            lrHlg.childForceExpandWidth = false; lrHlg.childForceExpandHeight = false;

            var pillGo = Rect("LevelPill", levelRow.transform, 0, 0, 99, 45);
            Img(pillGo, SprLevelPill, Gold, Image.Type.Sliced, 22f);
            var pillLe = pillGo.AddComponent<LayoutElement>();
            pillLe.minWidth = pillLe.preferredWidth = 99f;
            pillLe.minHeight = pillLe.preferredHeight = 45f;
            var levelLabel = TMP("LevelLabel", pillGo.transform, 0, 0, 99, 45,
                "Lv.12", F(28), Gold, FontSemi, TextAlignmentOptions.Center);
            Set(so, "_levelLabel", levelLabel);

            var rankLabel = TMP("RankLabel", levelRow.transform, 0, 0, 0, 45,
                "AMATEUR GOLFER", F(28), MintText, FontSemi, TextAlignmentOptions.Center);
            Set(so, "_rankLabel", rankLabel);

            // ── XP panel — node 14026:33493, 958x136 (was built 184) ──────────
            var xpPanel = Card("XpPanel", col, 10, 864, 958, 136, SprXpPanel);
            var xpFrom = TMP("XpLevelFrom", xpPanel.transform, 32, 20, 300, 36,
                "Lv.12 → Lv.13", F(30), White, FontSemi, TextAlignmentOptions.TopLeft);
            Set(so, "_xpLevelFrom", xpFrom);
            var xpHint = TMP("XpHint", xpPanel.transform, 626, 22, 300, 31,
                "2 more rounds", F(26), Gold, FontSemi, TextAlignmentOptions.TopRight);
            Set(so, "_xpHint", xpHint);
            // Track: node 894x16 r8 at (32,64) — width-driven.
            var xpFillImg = Bar(xpPanel.transform, "XpTrack", 32, 64, 894, 16, StatGreen);
            Set(so, "_xpTrackFill", xpFillImg);
            var xpFooter = TMP("XpFooter", xpPanel.transform, 32, 88, 400, 28,
                "650 / 1,000 XP", F(24), Muted, FontMed, TextAlignmentOptions.TopLeft);
            Set(so, "_xpFooter", xpFooter);
            // CTA — node 14026:33502 sits at (628,88) because its mock string is short
            // ("Play a round to level up"). Ours is longer, and a fixed 266-wide box pushed the
            // text past the panel edge. Anchor the icon+text group to the RIGHT inset (32) and let
            // it size to content, so it can never overflow in either language.
            var ctaRow = Rect("XpCta", xpPanel.transform, 32, 88, 894, 28);
            var ctaHlg = ctaRow.AddComponent<HorizontalLayoutGroup>();
            ctaHlg.childAlignment = TextAnchor.MiddleRight;
            ctaHlg.spacing = 8f;
            ctaHlg.childControlWidth = true;  ctaHlg.childControlHeight = true;
            ctaHlg.childForceExpandWidth = false; ctaHlg.childForceExpandHeight = false;
            var ctaIcon = Rect("Icon", ctaRow.transform, 0, 0, 24, 24);
            Img(ctaIcon, IcoRounds, StatGreen);
            var ctaLe = ctaIcon.AddComponent<LayoutElement>();
            ctaLe.minWidth = ctaLe.preferredWidth = 24f;
            ctaLe.minHeight = ctaLe.preferredHeight = 24f;
            TMP("XpCtaText", ctaRow.transform, 0, 0, 0, 28, "",
                F(24), StatGreen, FontMed, TextAlignmentOptions.Midline, "GPS_AVATAR_XP_CTA");

            // ── Evolution panel — node 14026:33509, 958x246 ───────────────────
            var evPanel = Card("EvolutionPanel", col, 10, 1024, 958, 246, SprEvolutionPanel);
            TMP("EvolutionTitle", evPanel.transform, 32, 18, 407, 50, "",
                F(34), Gold, FontSemi, TextAlignmentOptions.TopLeft, "GPS_AVATAR_EVOLUTION");

            // Node stage geometry: only the CURRENT stage is 88px with a 44px icon — the others are
            // 68/32. The previous build made all five 88, which erased the "you are here" marker.
            string[] stageNames = { "Beginner", "Rookie", "Amateur", "Single", "Pro" };
            string[] stageKeys  = { "GPS_AVATAR_RANK_BEGINNER", "GPS_AVATAR_RANK_ROOKIE",
                                    "GPS_AVATAR_RANK_AMATEUR", "GPS_AVATAR_RANK_SINGLE",
                                    "GPS_AVATAR_RANK_PRO" };
            int[]    stageLvls  = { 1, 5, 12, 20, 50 };
            string[] stageIcons = { IcoSparkle, IcoSparkle, IcoSparkle, IcoStar, IcoStar };
            float[]  stageX     = { 40f, 255.5f, 447f, 658.5f, 850f };
            var stagesProp = so.FindProperty("_evolutionStages");
            stagesProp!.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                // Built at the NON-current geometry for every stage (ring 68 / icon 32, labels
                // at 74 and 101, container y=90). GpsEvolutionStageView.SetState promotes whichever
                // stage is actually current to the node's 88/44 and shifts its labels — baking
                // "current" at build time left Amateur permanently mis-sized and mis-placed.
                float w = (i == 0) ? 92f : 68f;
                var stageGo = Rect("Stage_" + stageNames[i], evPanel.transform, stageX[i], 90f, w, 122f);
                var ringGo  = Rect("IconRing", stageGo.transform, (w - 68f) * 0.5f, 0, 68, 68);
                var ringImg = Img(ringGo, SprIconRingTile, Muted);
                Img(Rect("Icon", ringGo.transform, 18, 18, 32, 32), stageIcons[i], White);

                var rankTmp = TMP("RankLabel", stageGo.transform, 0, 74, w, 21, "",
                                  F(18), White, FontSemi, TextAlignmentOptions.Top, stageKeys[i]);
                var lvlTmp  = TMP("LevelLabel", stageGo.transform, 0, 101, w, 21,
                                  $"Lv.{stageLvls[i]}", F(18), Muted, FontMed, TextAlignmentOptions.Top);

                var elem = stagesProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("IconRing")!.objectReferenceValue   = ringImg;
                elem.FindPropertyRelative("LevelLabel")!.objectReferenceValue = lvlTmp;
                elem.FindPropertyRelative("RankLabel")!.objectReferenceValue  = rankTmp;
            }

            // ── Unlock panel — node 14026:33556, 958x230 at y=1294 ───────────
            // Restored (Cesar, 2026-09-02); the SPEC had it hidden in v1. The three items are the
            // DESIGN's own milestone unlocks, not user inventory — v1 has no /gifts/inventory, so
            // they render as the node draws them, gated by the level in the header.
            var unlockPanel = Card("UnlockPanel", col, 10, 1294, 958, 230, SprUnlockPanel);
            Set(so, "_unlockPanel", unlockPanel);
            // No lock glyph: Rubik has no U+1F512 and it rendered as a tofu box (probed with
            // TMP_FontAsset.HasCharacters). The node's padlock is a glyph we cannot substitute, so
            // the title carries the meaning on its own.
            var unlockTitle = TMP("UnlockTitle", unlockPanel.transform, 32, 18, 500, 33,
                "UNLOCKS AT Lv.13", F(28), Gold, FontSemi, TextAlignmentOptions.TopLeft);
            Set(so, "_unlockTitle", unlockTitle);

            string[] unlockKeys  = { "GPS_AVATAR_UNLOCK_CAP", "GPS_AVATAR_UNLOCK_AURA",
                                     "GPS_AVATAR_UNLOCK_FX" };
            string[] unlockIcons = { IcoStar, IcoSparkle, IcoHeart };
            int[]    unlockLvls  = { 13, 15, 20 };
            float[]  unlockX     = { 32f, 335.33f, 638.67f };
            for (int u = 0; u < 3; u++)
            {
                // Each unlock is its own card — node 14026:33561: r28, 3px white border,
                // pt14 pb12 px8, gap 6. The first build drew the contents with no container.
                var item = Card("Unlock" + u, unlockPanel.transform, unlockX[u], 61, 287.33f, 147,
                                SprUnlockTile);
                var ringGo = Rect("IconRing", item.transform, (287.33f - 64f) * 0.5f, 14, 64, 64);
                Img(ringGo, SprIconRingTile, new Color(1f, 1f, 1f, 0.85f));
                Img(Rect("Icon", ringGo.transform, 16, 16, 32, 32), unlockIcons[u], White);
                TMP("UnlockName", item.transform, 0, 84, 287.33f, 24, "",
                    F(20), White, FontSemi, TextAlignmentOptions.Top, unlockKeys[u]);
                TMP("UnlockLv", item.transform, 0, 114, 287.33f, 21, $"Lv.{unlockLvls[u]}",
                    F(18), Gold, FontMed, TextAlignmentOptions.Top);
            }

            // ── Status panel — node 14026:33586, (10,1548). The node has THREE rows; we render
            // the character's FOUR roster stats (documented SPEC deviation), so the panel is one
            // 48-row taller than the node's 272 and the note follows it down.
            var statusPanel = Card("StatusPanel", col, 10, 1548, 958, 320, SprStatusPanel);
            TMP("StatusTitle", statusPanel.transform, 32, 18, 400, 50, "",
                F(34), Gold, FontSemi, TextAlignmentOptions.TopLeft, "GPS_AVATAR_STATUS");

            string[] statFields      = { "_statStrengthFill",   "_statClubControlFill",
                                          "_statRecoveryFill",   "_statStaminaFill" };
            string[] statLabelFields = { "_statStrengthLabel", "_statClubControlLabel",
                                          "_statRecoveryLabel", "_statStaminaLabel" };
            string[] statKeys        = { "ROSTER_STRENGTH", "ROSTER_CLUB_CONTROL",
                                          "ROSTER_RECOVERY", "ROSTER_STAMINA" };
            for (int i = 0; i < 4; i++)
            {
                float sy = 80 + i * 48;                            // node row pitch
                TMP($"StatName{i}", statusPanel.transform, 32, sy + 10, 190, 28, "",
                    F(24), White, FontMed, TextAlignmentOptions.TopLeft, statKeys[i]);
                var barImg = Bar(statusPanel.transform, $"StatBar{i}", 238, sy + 17, 592, 14, StatGreen);
                Set(so, statFields[i], barImg);
                var statLbl = TMP($"StatLabel{i}", statusPanel.transform, 830, sy + 6, 96, 36,
                    "0/0", F(28), White, FontSemi, TextAlignmentOptions.TopRight);
                Set(so, statLabelFields[i], statLbl);
            }
            var statusNote = TMP("StatusNote", statusPanel.transform, 696, 272, 230, 24, "",
                F(20), Muted, FontMed, TextAlignmentOptions.TopRight, "GPS_AVATAR_STATUS_NOTE");
            Set(so, "_statusNote", statusNote);

            CloneNavBar(rt);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ── GPS Badges Screen (Figma 14027:33298) ─────────────────────────────

        static GameObject BuildBadgesScreen()
        {
            var root = new GameObject("GpsBadgesScreen", typeof(RectTransform));
            var rt   = (RectTransform)root.transform;
            Stretch(rt);

            var ctrl = root.AddComponent<GpsBadgesScreenController>();
            var so   = new SerializedObject(ctrl);

            // Background — Home Background.png sprite, Simple, white, stretch-to-fill.
            var bg = Rect("Background", rt, 0, 0, 1170, 2532);
            Stretch((RectTransform)bg.transform);
            Img(bg, BgBadges, White, Image.Type.Simple);

            var col = (RectTransform)Rect("ContentContainer", rt, 96, 361, 978, 1860).transform;

            // ── Collection panel — node 14027:33555, 958x139 (build had 200) ──
            var collPanel = Card("CollectionPanel", col, 10, 0, 958, 139, SprCollectionPanel);
            // Header (0,0,958,71): PL group at (32,21.5) = 32px star + title at +44; pct hard right.
            Img(Rect("CollectionStar", collPanel.transform, 32, 25, 32, 32), IcoStar, Gold);
            TMP("CollectionTitle", collPanel.transform, 76, 21, 400, 40, "",
                F(34), Gold, FontSemi, TextAlignmentOptions.TopLeft, "GPS_BADGES_COLLECTION");
            var collPct = TMP("CollectionPct", collPanel.transform, 526, 20, 400, 43,
                "33%", F(36), Gold, FontSemi, TextAlignmentOptions.TopRight);
            Set(so, "_collectionPct", collPct);

            // Track — node 894x16 r8 at (32,71) — width-driven.
            var collFillImg = Bar(collPanel.transform, "CollTrack", 32, 71, 894, 16, Gold);
            Set(so, "_collectionTrackFill", collFillImg);

            var collEarned = TMP("CollectionEarned", collPanel.transform, 32, 93, 600, 28,
                "8 / 24 badges earned", F(24), Muted, FontMed, TextAlignmentOptions.TopLeft);
            Set(so, "_collectionEarned", collEarned);

            // ── Badge sections — GOLF=8, SOCIAL=8, TRUST=4, SPECIAL=4 ───────────
            string[] secNames  = { "GOLF", "SOCIAL", "TRUST", "SPECIAL" };
            string[] secFields = { "_sectionGolf", "_sectionSocial", "_sectionTrust", "_sectionSpecial" };
            string[] secKeys   = {
                "GPS_BADGES_SEC_GOLF", "GPS_BADGES_SEC_SOCIAL",
                "GPS_BADGES_SEC_TRUST", "GPS_BADGES_SEC_SPECIAL"
            };
            string[] secIconPaths = { IcoRounds, IcoHeart, IcoPin, IcoSparkle };
            int[]    secCounts    = { 8, 8, 4, 4 };

            // Badge IDs per section (24 total)
            string[][] secBadgeIds = {
                new[]{ "first_round","break_110","break_100","break_90","break_80","streak_5","streak_10","courses_10" },
                new[]{ "first_gift_recv","first_gift_send","gifts_100","followers_100","followers_1000","followers_10000","first_vote","vote_hits_10" },
                new[]{ "first_gps","trust_80","trust_100","social_verify_5" },
                new[]{ "monthly_mvp","tournament_win","gift_king","all_badges" }
            };

            // Node section geometry (14027:33568 GOLF / :33667 SOCIAL / :33764 TRUST / :33816
            // SPECIAL): y = 163 / 585 / 1007 / 1264, i.e. 398 tall for the two-row sections and
            // 233 for the one-row ones, with a 24 gap. Grid starts at (20,62); cells are
            // 220.5x153 on a 232.5 column pitch and a 165 row pitch.
            float[] secY = { 163f, 585f, 1007f, 1264f };

            for (int i = 0; i < 4; i++)
            {
                int   cnt  = secCounts[i];
                int   rows = (cnt + 3) / 4;
                float secH = 62f + (rows == 2 ? 336f : 171f);

                var secPanel = Card($"Section_{secNames[i]}", col, 10, secY[i], 958, secH, SprSectionPanel);

                // Header — icon 28 at (32,20), title at (72,16).
                Img(Rect("SectionIcon", secPanel.transform, 32, 20, 28, 28), secIconPaths[i], Gold);
                TMP($"SectionTitle{i}", secPanel.transform, 72, 16, 500, 36, "",
                    F(30), Gold, FontSemi, TextAlignmentOptions.TopLeft, secKeys[i]);

                var cellContainer = Rect("CellContainer", secPanel.transform, 20, 62, 918, rows * 165f - 12f);
                var grid = cellContainer.AddComponent<GridLayoutGroup>();
                grid.cellSize        = new Vector2(220.5f, 153f);
                grid.spacing         = new Vector2(12, 12);
                grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 4;
                grid.childAlignment  = TextAnchor.UpperLeft;

                var secProp = so.FindProperty(secFields[i]);
                if (secProp != null)
                {
                    var containerProp = secProp.FindPropertyRelative("CellContainer");
                    if (containerProp != null)
                        containerProp.objectReferenceValue = cellContainer.transform;
                }

                // Seed correct badge count with proper IDs. First 2 per section = earned.
                for (int c = 0; c < cnt; c++)
                    SeedBadgeCell(cellContainer.transform, secBadgeIds[i][c], earned: c < 2);
            }

            // ── Badge cell prefab (runtime spawn) — SAME node metrics as SeedBadgeCell ────
            // Kept in lockstep with the seeded cell above; when these two drifted, the fidelity
            // pass measured one shape and the runtime rendered another.
            const float PCw = 220.5f, PCh = 153f;
            var cellGo = new GameObject("BadgeCellPrefab", typeof(RectTransform));
            ((RectTransform)cellGo.transform).sizeDelta = new Vector2(PCw, PCh);
            var cellCtrl = cellGo.AddComponent<BadgeCellView>();
            var cellSo   = new SerializedObject(cellCtrl);

            var cellBgGo  = Rect("Background", cellGo.transform, 0, 0, PCw, PCh);
            var cellBgImg = Img(cellBgGo, SprPill, GpsUiColor.A(White, 0.10f),
                                Image.Type.Sliced, 24f);
            WireField(cellSo, "_background", cellBgImg);

            // Frame, not a solid capsule — BadgeCellView tints this per rarity.
            var borderImg = Img(Rect("Border", cellGo.transform, 0, 0, PCw, PCh),
                                SprBadgeFrame2, Muted, Image.Type.Sliced, 24f);
            WireField(cellSo, "_border", borderImg);

            var iconRingGo  = Rect("IconRing", cellGo.transform, 80.25f, 35, 60, 60);
            var iconRingImg = Img(iconRingGo, SprIconRingTile, White, Image.Type.Simple);
            Img(Rect("Icon", iconRingGo.transform, 16, 16, 28, 28), IcoStar, White);
            WireField(cellSo, "_iconRing", iconRingImg);

            var checkTmp = TMP("Checkmark", cellGo.transform, 6, 10, 40, 21, "",
                F(18), StatGreen, FontSemi, TextAlignmentOptions.TopLeft);
            WireField(cellSo, "_checkmark", checkTmp);
            var nameTmp = TMP("NameLabel", cellGo.transform, 15.25f, 99, 190, 21, "",
                F(18), White, FontSemi, TextAlignmentOptions.Top);
            WireField(cellSo, "_nameLabel", nameTmp);
            var progTmp = TMP("ProgressLabel", cellGo.transform, 15.25f, 124, 190, 19, "",
                F(16), Muted, FontMed, TextAlignmentOptions.Top);
            WireField(cellSo, "_progressLabel", progTmp);
            var rarityTmp = TMP("RarityLabel", cellGo.transform, 6, 12, 208.5f, 17, "",
                F(14), Muted, FontSemi, TextAlignmentOptions.TopRight);
            WireField(cellSo, "_rarityLabel", rarityTmp);

            cellSo.ApplyModifiedPropertiesWithoutUndo();
            cellGo.SetActive(false);
            Set(so, "_badgeCellPrefab", cellCtrl);

            CloneNavBar(rt);
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        /// <summary>Each badge's global completion rate, as the node prints it under the name
        /// (14027:33588 etc.). Design data from the badge definitions, not user progress.</summary>
        static readonly Dictionary<string, string> BadgeTargetPct = new Dictionary<string, string>
        {
            { "first_round", "89%" },     { "break_110", "72%" },   { "break_100", "34%" },
            { "break_90", "8%" },         { "break_80", "2%" },     { "streak_5", "12%" },
            { "streak_10", "3%" },        { "courses_10", "1%" },
            { "first_gift_recv", "45%" }, { "first_gift_send", "38%" }, { "gifts_100", "4%" },
            { "followers_100", "22%" },   { "followers_1000", "3%" },   { "followers_10000", "0.1%" },
            { "first_vote", "35%" },      { "vote_hits_10", "9%" },
            { "first_gps", "52%" },       { "trust_80", "18%" },    { "trust_100", "5%" },
            { "social_verify_5", "7%" },
            { "monthly_mvp", "0.5%" },    { "tournament_win", "0.2%" }, { "gift_king", "1%" },
            { "all_badges", "0%" },
        };

        // Per-badge rarity lookup. Values: COMMON=#B7C3D3, RARE=#6fa5e8, EPIC=#b48cf0, LEGEND=Gold
        static readonly Dictionary<string, (string label, string hex)> BadgeRarity =
            new Dictionary<string, (string, string)>
            {
                // GOLF badges
                { "first_round",  ("COMMON", "#B7C3D3") },
                { "break_110",    ("COMMON", "#B7C3D3") },
                { "break_100",    ("RARE",   "#6fa5e8") },
                { "break_90",     ("EPIC",   "#b48cf0") },
                { "break_80",     ("LEGEND", "#EEDC9A") },
                { "streak_5",     ("COMMON", "#B7C3D3") },
                { "streak_10",    ("EPIC",   "#b48cf0") },
                { "courses_10",   ("LEGEND", "#EEDC9A") },
                // SOCIAL badges
                { "first_gift_recv",  ("COMMON", "#B7C3D3") },
                { "first_gift_send",  ("COMMON", "#B7C3D3") },
                { "gifts_100",        ("RARE",   "#6fa5e8") },
                { "followers_100",    ("COMMON", "#B7C3D3") },
                { "followers_1000",   ("EPIC",   "#b48cf0") },
                { "followers_10000",  ("LEGEND", "#EEDC9A") },
                { "first_vote",       ("COMMON", "#B7C3D3") },
                { "vote_hits_10",     ("RARE",   "#6fa5e8") },
                // TRUST badges
                { "first_gps",        ("COMMON", "#B7C3D3") },
                { "trust_80",         ("RARE",   "#6fa5e8") },
                { "trust_100",        ("EPIC",   "#b48cf0") },
                { "social_verify_5",  ("RARE",   "#6fa5e8") },
                // SPECIAL badges
                { "monthly_mvp",      ("LEGEND", "#EEDC9A") },
                { "tournament_win",   ("LEGEND", "#EEDC9A") },
                { "gift_king",        ("EPIC",   "#b48cf0") },
                { "all_badges",       ("LEGEND", "#EEDC9A") },
            };

        // Seeds one badge cell with correct translucency, name label, and rarity tag.
        // earned = A(white,0.10) + 2px Gold border; locked = ADark(black,0.25) + 1px #4a5a6e border.
        /// <summary>
        /// One badge cell — node 14027:33578 (earned) / :33611 (locked), 220.5x153, r24.
        ///   earned : fill rgba(255,255,255,.10), 2px border in the RARITY colour, white name
        ///   locked : fill rgba(0,0,0,.25),      1px #4a5a6e border, ring at 0.6, muted name
        /// The previous build painted a SOLID S_PillStadium across the whole cell as its "border",
        /// which covered the correctly-computed translucent fill — that is why every earned cell
        /// read as an opaque cream rectangle.
        /// </summary>
        static void SeedBadgeCell(Transform cellContainer, string badgeId, bool earned = false)
        {
            const float Cw = 220.5f, Ch = 153f;
            var go = new GameObject("BadgeCell_" + badgeId, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(cellContainer, false);
            rt.sizeDelta = new Vector2(Cw, Ch);

            // GENUINELY translucent, not pre-composited. The builder's local A(overlay, a, over)
            // solves the blend against an assumed backdrop and returns an OPAQUE colour — right for
            // a chip sitting on a known panel, wrong here: these cells sit over the screen's photo,
            // and the node specifies `rgba(255,255,255,.10)` / `rgba(0,0,0,.25)` as real alpha.
            // Pre-compositing against navy is what made every earned cell read as a solid navy box.
            Color bgFill = earned ? GpsUiColor.A(White, 0.10f)
                                  : GpsUiColor.ADark(Color.black, 0.25f);
            Img(Rect("BgFill", go.transform, 0, 0, Cw, Ch), SprPill, bgFill, Image.Type.Sliced, 24f);

            // Rarity drives the border colour on an earned cell; a locked one is always #4a5a6e.
            string rarityLabel = "";
            Color  rarityColor = Muted;
            if (BadgeRarity.TryGetValue(badgeId, out var rInfo))
            {
                rarityLabel = rInfo.label;
                rarityColor = rInfo.hex != null ? Hex(rInfo.hex) : Muted;
            }
            Img(Rect("Border", go.transform, 0, 0, Cw, Ch),
                earned ? SprBadgeFrame2 : SprBadgeFrame1,
                earned ? rarityColor : Hex("#4A5A6E"), Image.Type.Sliced, 24f);

            // Badge Top (6,10,208.5,21): ✓ hard left, rarity hard right. The rarity tag keeps its
            // colour when LOCKED too (node :33614 renders EPIC purple on a locked cell).
            if (earned)
                TMP("Checkmark", go.transform, 6, 10, 40, 21, "✓",
                    F(18), StatGreen, FontSemi, TextAlignmentOptions.TopLeft);
            TMP("RarityTag", go.transform, 6, 12, 208.5f, 17, rarityLabel,
                F(14), rarityColor, FontSemi, TextAlignmentOptions.TopRight);

            // Icon Ring (80.25,35,60,60) with a 28px star at (16,16); locked sits at 0.6 opacity.
            // Only an EARNED cell carries the rarity colour. The node greys both the ring and the
            // star on a locked cell (ring at 0.6) — tinting a locked LEGEND gold made unearned
            // badges look won.
            // S_GpsIconRing_Tile IS gold artwork, so a multiply-tint cannot turn it grey — tinting
            // with Muted just gives darker gold. The node's locked ring reads as a dim cool ring,
            // so it is dropped to 0.35 alpha over the dark fill, which is what actually reads.
            float ringA    = earned ? 1f : 0.35f;
            Color iconTint = earned ? rarityColor : Muted;
            Color ringTint = earned ? White : Muted;
            var ringGo = Rect("IconRing", go.transform, 80.25f, 35, 60, 60);
            Img(ringGo, SprIconRingTile, new Color(ringTint.r, ringTint.g, ringTint.b, ringA),
                Image.Type.Simple);
            Img(Rect("Icon", ringGo.transform, 16, 16, 28, 28), IcoStar,
                new Color(iconTint.r, iconTint.g, iconTint.b, ringA));

            // Name (15.25,99,190,21) 18 SemiBold — white earned, muted locked.
            // localizeKey, NOT a baked Get(): LocalizationManager is not initialised in the
            // editor, so resolving here stamped the raw key into the prefab.
            TMP("NameLabel", go.transform, 15.25f, 99, 190, 21, "",
                F(18), earned ? White : Muted, FontSemi, TextAlignmentOptions.Top,
                "BADGE_" + badgeId + "_NAME");

            // Pct (…,124,…,19) 16 Medium muted — the badge definition's global target_pct, read
            // off the node. Live cells get it from the DTO; the seeded grid carries the node's.
            BadgeTargetPct.TryGetValue(badgeId, out string pct);
            TMP("ProgressLabel", go.transform, 15.25f, 124, 190, 19, pct ?? "",
                F(16), Muted, FontMed, TextAlignmentOptions.Top);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shared helpers
        // ═══════════════════════════════════════════════════════════════════════

        static void CloneNavBar(RectTransform parent)
        {
            if (!File.Exists(HubPrefab)) { Debug.LogWarning("[GpsProfilePackBuilder] hub prefab not found"); return; }
            GameObject hub = PrefabUtility.LoadPrefabContents(HubPrefab);
            try
            {
                Transform nav = GpsPolishBuilder.FindNavBar(hub);
                if (nav == null) { Debug.LogWarning("[GpsProfilePackBuilder] no GpsNavBar in hub"); return; }
                var clone = UnityEngine.Object.Instantiate(nav.gameObject, parent);
                clone.name = "GpsNavBar";
                foreach (Button b in clone.GetComponentsInChildren<Button>(true))
                {
                    var colors = b.colors; colors.disabledColor = Color.white;
                    b.colors = colors; b.interactable = false;
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(hub); }
        }


        // ── Geometry ──────────────────────────────────────────────────────────

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
            go.AddComponent<ButtonPressFeedback>();
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
                    Debug.LogError("[GpsProfilePackBuilder] sprite not found: " + spritePath);
            }
            img.color = color; img.type = type; img.raycastTarget = false;
            if (type == Image.Type.Sliced && sliceRadius > 0f)
                img.pixelsPerUnitMultiplier = PillBorder / sliceRadius;
            return img;
        }

        static Image Img(RectTransform rt, string? path, Color c,
                         Image.Type t = Image.Type.Simple, float r = 0f) => Img(rt.gameObject, path, c, t, r);

        /// <summary>
        /// One gift-totals tile (node 14025:33385). A centred icon+label row over a centred value.
        /// The row uses a HorizontalLayoutGroup rather than hard-coded x offsets so the pair stays
        /// centred when the label changes width — the Japanese strings are a different length.
        /// </summary>
        static TextMeshProUGUI GiftTile(GameObject tile, string name, string labelKey, Color tint, string seed)
        {
            // LH row: 24px icon + 10px gap + 22px Medium label, centred as a unit. py18 -> y=18.
            var row = Rect(name + "LH", tile.transform, 0, 18, 470, 26);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 10f;
            hlg.childControlWidth = true;  hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            var icon = Rect(name + "Icon", row.transform, 0, 0, 24, 24);
            Img(icon, IcoGift, tint);
            var iconLe = icon.AddComponent<LayoutElement>();
            iconLe.minWidth = iconLe.preferredWidth = 24f;
            iconLe.minHeight = iconLe.preferredHeight = 24f;

            var label = TMP(name + "Lbl", row.transform, 0, 0, 0, 26, "",
                            F(22), tint, FontMed, TextAlignmentOptions.Midline, labelKey);

            // Value: 44px SemiBold in the SAME tint, centred across the tile. gap4 under the row.
            return TMP(name + "Value", tile.transform, 0, 48, 470, 52, seed,
                       F(44), tint, FontSemi, TextAlignmentOptions.Top);
        }

        /// <summary>
        /// Track + fill for a progress bar, built the way the approved score-upload trust bar is
        /// (ScoreUploadScreenBuilder:842-856): BOTH images 9-sliced, and the fill LEFT-ANCHORED at
        /// zero width so it is driven by WIDTH. Image.Type.Filled throws the 9-slice away and
        /// renders the cap as a thin wedge — the defect this replaces on all six GPS bars.
        /// </summary>
        static Image Bar(Transform parent, string name, float x, float y, float w, float h, Color fillC)
        {
            var track = Rect(name, parent, x, y, w, h);
            Img(track, SprPill, TrackBg, Image.Type.Sliced, h / 2f);
            var fill = Rect("Fill", track.transform, 0, 0, w, h);
            var frt = (RectTransform)fill.transform;
            frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
            frt.pivot = new Vector2(0f, 1f);
            frt.anchoredPosition = Vector2.zero;
            frt.sizeDelta = new Vector2(0f, h);
            return Img(fill, SprPill, fillC, Image.Type.Sliced, h / 2f);
        }

        static GameObject Card(string name, Transform parent, float x, float y, float w, float h,
                               string spritePath)
        {
            var go = Rect(name, parent, x, y, w, h);
            Img(go, spritePath, White, Image.Type.Simple);
            return go;
        }

        static GameObject Panel(string name, Transform parent, float x, float y, float w, float h,
                                Color fill, float radius)
        {
            var go = Rect(name, parent, x, y, w, h);
            Img(go, SprPill, fill, Image.Type.Sliced, radius);
            return go;
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

        static Color A(Color overlay, float srgbAlpha, Color over)
        {
            float a = srgbAlpha;
            return new Color(
                S2L(overlay.r * 255f) * a + over.r * (1f - a),
                S2L(overlay.g * 255f) * a + over.g * (1f - a),
                S2L(overlay.b * 255f) * a + over.b * (1f - a), 1f);
        }

        // ── SerializedObject helpers ──────────────────────────────────────────

        static void Set(SerializedObject so, string field, UnityEngine.Object? value)
        {
            var p = so.FindProperty(field);
            if (p == null) { Debug.LogError("[GpsProfilePackBuilder] no field '" + field + "'"); return; }
            p.objectReferenceValue = value;
        }

        static void WireField(SerializedObject so, string field, UnityEngine.Object? value)
        {
            var p = so.FindProperty(field);
            if (p != null) p.objectReferenceValue = value;
        }

        // ── Asset helpers ─────────────────────────────────────────────────────

        static void EnsureDir(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
                string name   = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        static void EnsureImport()
        {
            string[] paths = {
                SprTrustPanel, SprQuickStatTile, SprGiftReceived, SprGiftSent,
                SprShortcutTile, SprRecentRounds, SprAvatarStage, SprXpPanel,
                SprEvolutionPanel, SprStatusPanel, SprCollectionPanel, SprSectionPanel,
                SprPill, SprHero, SprIconRingTile, SprSilver,
                IcoStar, IcoHeart, IcoPin, IcoSparkle, IcoRounds, IcoGift,
                AvatarRings[0], AvatarRings[1], AvatarRings[2], AvatarRings[3],
            };
            bool dirty = false;
            foreach (var p in paths)
            {
                if (!File.Exists(p)) continue;
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
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                dirty = true;
            }
            if (dirty) AssetDatabase.Refresh();
        }
    }
}
