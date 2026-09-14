# Implementer Report — `gacha_banner_tagline`

**Iteration shape:** `gacha_card:tagline_overlay`
**Iteration:** 2 — iter-1 built and verified every surface and parked at `IMPLEMENTER_BLOCKED` on the outward-facing publish chain; Cesar's "Go on the publish chain" (2026-09-14) ran it end to end, and iter-2 is that chain plus the real-flow capture of the PUBLISHED data (nothing injected).

## Implementation summary

The gacha card now draws the two selling lines the weekly artwork can no longer carry: a brand-coloured **ribbon** under the countdown (`taglineEn`/`taglineJa`) and a soft-edged navy **hook band** lower over the art (`hookEn`/`hookJa`), both localized UI over the image, both `SetActive(false)` when the row's pair is blank, the `*…*` run rendered in `#FF2D9B` and a two-character `\n` rendered as a hard line break. The four strings are catalog columns end to end: `GachaBannerEntry` parses them, the two CSVs carry them (all 52 planned weeks filled from `weekly_taglines.csv`), the admin's rotation generator reads them off the rotation row instead of the old `"Featured this week"` literal (carried through a re-materialize, defaults kept as the fallback), the gacha-banners editor shows one textbox per column, and the dashboard is deployed with that generator (`/api/version` → `e4fc1a918-DIRTY`, i.e. this working tree). Both plates are baked from the node's tokens by a new palette baker; the hook's alpha is compensated for this project's LINEAR colour space (measured, see § Figma fidelity) so the band reads as the node render does over the bright glow.

**The publish chain (SPEC §8) is done** — on Cesar's go: `rotations` v5 (the `wk_2026_39` `materializedAt` stamp the 09-11 MATERIALIZE had left unpublished, 1 changed row) → `import_content.py --catalogs rotations` PLAN 0 add / 52 change / 2 same / **0 conflict** → `--apply` (52 drafts) → drawer read row by row (every changed row = exactly the four text columns; `wk_2026_39`'s draft had come back with a BLANK `materializedAt` because the repo CSV predates the stamp — fixed in its row editor before publishing, so no stamp regressed) → `rotations` v6 (52 changed) → PREVIEW + typed-confirm MATERIALIZE of `wk_2026_38` (LIVE; deterministic re-draw, `Art kept from the existing draft`) and `wk_2026_39` → drafts diffed via REST (banners: the four columns only; rotations: the two new stamps; wk_39's 13 shop / 6 rate / 12 pool rows that its 09-11 materialize had left as drafts) → **PUBLISH ROTATION**: `gacha_rates v7, gacha_pools v6, gacha_banners v16, shop_catalog v11, rotations v7` → `export_content.py` (6 files) → **`--check: clean`** across all 21 catalogs.

**Incident during the chain, contained:** after the `wk_2026_39` MATERIALIZE confirm, the workbench advanced its selection to the next NOT-GENERATED week and the still-open "Materialize again?" dialog re-fired — `wk_2026_40` (04:20:39 UTC) and `wk_2026_41` (04:21:10) were materialized as DRAFTS without a click (admin_audit_log rows `content.draft.create:*` at 04:20:39–04:21:18, all by the one signed-in account); the Worker then returned Cloudflare 1102 for ~30 s. Navigating away stopped it. The 64 draft-only rows it created (13+13 shop, 6+6 rates, 12+12 pools, 2 banners — none ever published, all timestamped inside that window) were deleted through the same PostgREST path `import_content.py` writes drafts with, and the two rotation drafts' `materializedAt` reset to the published `""`; the REST diff afterwards showed only the intended rows, and the workbench reads wk_40/41 as NOT GENERATED again. Nothing of it reached players. **Dashboard defect to file:** the "Materialize again?" confirm can re-fire against the auto-advanced selection (typed id and all) — out of this task's scope, flagged under § Open questions.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Resources/Data/content_version.txt` | modified — exporter: gacha_rates=7, gacha_pools=6, gacha_banners=16, shop_catalog=11, rotations=7 |
| `Assets/Resources/Data/gacha_banners.csv` | modified — `hookEn,hookJa` columns after `taglineJa`; after the chain publish the exporter wrote the four values on `banner_wk_2026_38/39` (+ the catalog-ahead drift the exporter had been reporting before this task: wk_38's newer artUrl, the `banner_wk_2026_39` row) |
| `Assets/Resources/Data/gacha_pools.csv` | modified — exporter mirror: `pwk_2026_39_*` 12 pool rows (same) |
| `Assets/Resources/Data/gacha_rates.csv` | modified — exporter mirror: `pool_wk_2026_39_*` 6 rate rows (same) |
| `Assets/Resources/Data/rotations.csv` | modified — `taglineEn,taglineJa,hookEn,hookJa` columns before `is_active`; all 52 planned weeks filled from `weekly_taglines.csv` (two-character `\\n` line breaks); the two archived pity-test rows blank |
| `Assets/Resources/Data/shop_catalog.csv` | modified — exporter mirror after the chain publish: wk_2026_39's 13 shop rows (materialized 09-11, first published today with the chain) — `export_content.py`, not hand-edited |
| `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` | modified — `ArtImage/TaglineRibbon(+Label)` and `ArtImage/TaglineHook(+Label)` added, the four refs wired via SerializedObject (+433/−1 lines, additions only) |
| `Assets/Scripts/UI/Gacha/GachaBannerCard.cs` | modified — four serialized refs, `BindTaglines` (one `PickLocalised` per pair, container `SetActive(false)` on blank) and the `FormatTagline` marker/line-break helper |
| `Assets/Scripts/UI/Gacha/GachaBannerModel.cs` | modified — `GachaBannerEntry` gains `TaglineEn`/`TaglineJa`/`HookEn`/`HookJa` (parsed from the four columns in `ReadRow`); class comment corrected |
| `Docs/AI_CONTEXT.md` | modified — session entry |
| `Docs/Architecture/ARCHITECTURE_AUDIT.md` | modified — regenerated by the session-start audit script (baseline: ` M Docs/Architecture/ARCHITECTURE_AUDIT.md` was already dirty) |
| `Tools/admin-dashboard/app/(panels)/gacha-banners/gacha-banners-panel.tsx` | modified — `hookEn`/`hookJa` hidden from the raw list and rendered in the per-locale text grid |
| `Tools/admin-dashboard/lib/__tests__/rotation.test.ts` | modified — hash re-pinned (`8838dbcb`→`93ba307b`), 18→22 column pins, new carry-through/re-materialize test |
| `Tools/admin-dashboard/lib/rotation.ts` | modified — `ROTATION_DEFAULTS` + `ROTATION_COLUMNS` gain the four; `gacha_banners.push` reads them off the rotation row like `nameEn`; `newRotationRow` carries them |
| `Assets/Art/Gacha/S_GachaTaglineHook.png` | created — 876×188 baked navy `#0B1B3A` alpha 0→0.93 (16 %)→0.93 (84 %)→0 hook plate (Sprite import) |
| `Assets/Art/Gacha/S_GachaTaglineHook.png.meta` | created — Sprite importer settings |
| `Assets/Art/Gacha/S_GachaTaglineRibbon.png` | created — 876×64 baked `#E4007F→#FF4FA3` @96 % ribbon plate (Sprite import) |
| `Assets/Art/Gacha/S_GachaTaglineRibbon.png.meta` | created — Sprite importer settings |
| `Assets/Tests/EditMode/GachaBannerTaglineTests.cs` | created — 11 EditMode tests on the shipping `FormatTagline` (marker, unmatched, empty, `\\n`) |
| `Assets/Tests/EditMode/GachaBannerTaglineTests.cs.meta` | created — Unity meta for the test |
| `Docs/Scripts/make_gacha_tagline_sprites.py` | created — the baker (source of truth for both plates, palette convention) |

**Not this task's — other sessions' in-flight work (each line quoted verbatim from the iter-2 kickoff baseline DIRTY block in HEARTBEAT.log); untouched by this task, listed only because Rule 13 wants every uncommitted path accounted for:**

| Path | Change |
|---|---|
| `M Assets/Art/HoleSelectScreen/Background.png` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Art/HoleSelectScreen/Background.png` |
| `M "Assets/Art/Original UI/MissionScreen/S_Mission_BgTemp.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M "Assets/Art/Original UI/MissionScreen/S_Mission_BgTemp.png"` |
| `M Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` |
| `M Assets/Prefabs/UI/Shop/StaminaShopDetailScreen.prefab` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Prefabs/UI/Shop/StaminaShopDetailScreen.prefab` |
| `M Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab` |
| `M Assets/Resources/HoleImages/MissionsBackground.png` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Resources/HoleImages/MissionsBackground.png` |
| `M Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Scripts/Tournaments/Tests/MapCardStateTests.cs` |
| `M Assets/Scripts/Tournaments/TournamentCardStateMapper.cs` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Scripts/Tournaments/TournamentCardStateMapper.cs` |
| `M Assets/Scripts/UI/ScreenManager.cs` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Scripts/UI/ScreenManager.cs` |
| `M Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Assets/Scripts/UI/Tournaments/TournamentHoleSelectionScreenController.cs` |
| `M Docs/TellCode.md` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `M Docs/TellCode.md` |
| `?? Assets/Background.png` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? Assets/Background.png` |
| `?? Assets/Background.png.meta` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? Assets/Background.png.meta` |
| `?? Assets/Tests/EditMode/ScreenIdSerializationTests.cs` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? Assets/Tests/EditMode/ScreenIdSerializationTests.cs` |
| `?? Assets/Tests/EditMode/ScreenIdSerializationTests.cs.meta` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? Assets/Tests/EditMode/ScreenIdSerializationTests.cs.meta` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638.jpg"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638.jpg"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638.png"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638_support.jpg"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638_support.jpg"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638_support.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202638_support.png"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202639.jpg"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202639.jpg"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202639.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202639.png"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202640.jpg"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202640.jpg"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202640.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202640.png"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202641.jpg"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202641.jpg"` |
| `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202641.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/GachaBanner_Wk202641.png"` |
| `?? "Claude outputs/WeeklyBanners/PROMPT_RECIPE.md"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/PROMPT_RECIPE.md"` |
| `?? "Claude outputs/WeeklyBanners/README.md"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/README.md"` |
| `?? "Claude outputs/WeeklyBanners/_brand_sheet.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/_brand_sheet.png"` |
| `?? "Claude outputs/WeeklyBanners/_weeks_38_40_comparison.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/_weeks_38_40_comparison.png"` |
| `?? "Claude outputs/WeeklyBanners/_weeks_38_41_comparison.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/_weeks_38_41_comparison.png"` |
| `?? "Claude outputs/WeeklyBanners/_wk38_two_vs_five.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/_wk38_two_vs_five.png"` |
| `?? "Claude outputs/WeeklyBanners/archive/GachaBanner_Wk202638_2club.png"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/archive/GachaBanner_Wk202638_2club.png"` |
| `?? "Claude outputs/WeeklyBanners/weekly_taglines.csv"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Claude outputs/WeeklyBanners/weekly_taglines.csv"` |
| `?? "Docs/Game Design/WEEKLY_BANNER_VARIATION_SYSTEM.md"` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? "Docs/Game Design/WEEKLY_BANNER_VARIATION_SYSTEM.md"` |
| `?? Docs/Specs/Quick/finished_tournament_leaderboard_route.md` | NOT this task's — another session's in-flight work, quoted from the iter-2 kickoff baseline DIRTY block: `?? Docs/Specs/Quick/finished_tournament_leaderboard_route.md` |

## Screenshot

- **Canonical screenshot:** `screenshots/iter2_B_realflow_published_row_EN.png` (1170×2532) — the REAL Rewards Center ▸ GACHA screen through the real flow (boot → DevAutoSignIn → Home → `PersistentUIManager.gachaButton.onClick`), the real `banner_wk_2026_38` card bound from the PUBLISHED `gacha_banners` v16 overlay — **nothing injected**. Runner read-back at bind time: `taglineEn='GET BogeyB Drivers & Woods' taglineJa='BogeyB ドライバー＆ウッドが登場' hookEn='3× RATE-UP ON\n*LEGENDARY* GEAR!' hookJa='*レジェンダリー*装備\n確率3倍！' ribbonActive=True hookActive=True`.
- `screenshots/iter2_C_realflow_published_row_JA.png` — same session, `LocalizationManager.SetLanguage(Japanese)` → `ReBind`: title `ドライバーウィーク · BOGEYB`, ribbon `BogeyB ドライバー＆ウッドが登場`, hook `<color=#FF2D9B>レジェンダリー</color>装備⏎確率3倍！` (§9.2).
- `screenshots/iter2_D_realflow_empty_row_centred.png` — the carousel moved to `banner_standard_club1` (blank pair): `ribbonActive=False hookActive=False`, the card at (0, 42) — the artwork's own baked copy is all it shows, exactly as before this task (§9.3).
- iter-1 frames kept for the record: `iter1_A_realflow_live_row_EN.png` (the pre-publish live row: ribbon `Featured this week`, hook hidden), `iter1_B_realflow_injected_copy_EN.png` / `iter1_C_…_JA.png` (the same copy injected in play mode before the publish), `iter1_D_realflow_empty_row_side.png`.
- **Captured at:** `Docs/Diagnostics/_capture/screenshot_2026-09-14_13-26-57.png`, `…13-27-02.png`, `…13-27-08.png` via `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` (CAPTURE RULE 0), copied to `screenshots/`. Runner log: `iter2_capture_runner.log` in the task folder.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` (real boot)
- **Play mode:** Yes — `Application.runInBackground = true`, Game View pinned to iPhone 14 (1170×2532, canvas scale 1)
- **Hole loaded (if applicable):** n/a

## Figma fidelity

Nodes re-pulled at step 0 with `get_metadata` + `get_design_context` (file `5gEAHjl6xAtW8iYY7NMvWd`): `Tagline/Ribbon` `14281:33634` (882×64 at y 189; label `14281:33635` at x 28, 539×48, Rubik SemiBold 40 / lh 48 / ls −0.9, white), `Tagline/Hook` `14281:33636` (882×188 at y 1035, `linear-gradient(90deg, rgba(11,27,58,0) 0%, rgba(11,27,58,0.93) 16%, rgba(11,27,58,0.93) 84%, rgba(11,27,58,0) 100%)`; label `14281:33637` 800 wide, Rubik SemiBold 62 / lh 72 / ls −1.4, `LEGENDARY` in `#ff2d9b`). Ground truth for the A/B is the 1:1 node render pulled into `reference/card_tagline_v1_wk202638_1x.png` (882×1720, `get_screenshot` of `14280:33548`); the architect's `card_tagline_v1_wk202638.png` is the same frame at 421×820. All "built" numbers below are read off the canonical `iter2_B_realflow_published_row_EN.png` in art-relative px (card left 144, art top 415, measured from the ribbon's own pixels), or from TMP `textInfo` in edit mode.

**Font size — the divisor is measured, not assumed.** `Rubik-SemiBold SDF` has `faceInfo.pointSize 42, scale 1.1, capLine 30`, so one TMP unit renders a cap 1.1224× taller than one Figma px (the built title at 46.2 renders a 37 px cap, exactly the node's 52 px title — the same relation). Sizes are therefore 40 → **35.6** and 62 → **55.2**, and the rendered cap heights below are the check, not the arithmetic.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Ribbon plate position | `14281:33634` | y 189 from the art top, full width | `TaglineRibbon` anchored top-left of `ArtImage`, `(0, −189)`; pixel rows 604–667 in the frame = art top + 189 | PASS |
| Ribbon plate size | `14281:33634` | 882 × 64 | 876 × 64 (SPEC §4 fidelity table: the Unity art is inset 3 px per side; measured 876 wide, 64 tall) | PASS* |
| Ribbon fill | `14281:33634` | `#E4007F → #FF4FA3` horizontal @ 96 % | baked sprite `S_GachaTaglineRibbon` (Image.Simple, white tint); sampled L/mid/R: ref (218,0,121)/(231,37,140)/(244,75,158) vs built (224,0,125)/(238,39,142)/(251,78,160); mean ǀΔRGBǀ over text-free ribbon rows **3.7** | PASS |
| Ribbon label inset + width | `14281:33635` | x 28, 826 px inner | stretch anchors, `sizeDelta (−56, 0)` → 28 L/R inset, 820 px inner (the 6 px is the art inset) | PASS* |
| Ribbon label font / weight | `14281:33635` | Rubik SemiBold | `Rubik-SemiBold SDF` (the BannerTitle's asset + shared material), `fontStyle Normal` — SemiBold IS the face | PASS |
| Ribbon label rendered size | `14281:33635` | 40 px → cap height 28 px (ref rows 207–234) | TMP 35.6 → cap 28.6 (edit-mode `textInfo`), pixel rows **207–234** = 28 px, identical rows to the reference | PASS |
| Ribbon label vertical placement | `14281:33635` | caps 18 px below the plate top / 19 above its bottom | first bake: 14 / 23 (TMP midline centres ascender-to-descender); fixed with top margin 8 → edit-mode 17.4 / 18.0, and on the canonical frame the cap rows are **207–234 = the reference's own rows** | PASS |
| Ribbon label tracking / width | `14281:33635` | ls −0.9; text box 539 px | `characterSpacing −0.9`; line extent 540.5 px (edit mode); pixel extent 537 vs ref 534 | PASS |
| Ribbon auto-size | SPEC §4 | 34–40 pt floor/ceiling | `enableAutoSizing`, min 30.3 / max 35.6 (the same ÷1.1224); no string of the 52 shrinks (min size 35.6 across the sweep) | PASS |
| Ribbon text colour / alignment | `14281:33635` | white, left, single line | white, `MidlineLeft`, `NoWrap` | PASS |
| Hook plate position | `14281:33636` | y 1035 from the art top | `TaglineHook` `(0, −1035)`; band pixels start at art top + 1035 | PASS |
| Hook plate size | `14281:33636` | 882 × 188 | 876 × 188 (same inset) | PASS* |
| Hook fill | `14281:33636` | navy `#0B1B3A`, alpha 0 → 0.93 @16 % → 0.93 @84 % → 0 | baked `S_GachaTaglineHook` with the four stops; **alpha compensated for linear-space blending** (`a_lin = 1 − (1 − a_srgb)^1.8`, plateau 253/255). Before: plateau over the glow (77,79,86) vs ref (28,43,69) — an sRGB-equivalent alpha of 0.77. After: (27,37,62) vs (28,43,69); mean ǀΔRGBǀ over the text-free band rows **5.4** (was 26.5) | PASS |
| Hook label box | `14281:33637` | 800 wide, centred, 144 tall | `sizeDelta (800, 144)`, centre anchors | PASS |
| Hook label font / weight | `14281:33637` | Rubik SemiBold | `Rubik-SemiBold SDF`, style Normal | PASS |
| Hook label rendered size | `14281:33637` | 62 px → cap 44 px (ref line-1 rows 1070–1113) | TMP 55.2 → cap 44.3; pixel rows **1071–1113** (canonical frame) | PASS |
| Hook line pitch | `14281:33637` | lh 72 | baseline gap **72.0** (edit-mode `lineInfo`), `lineSpacing 0` | PASS |
| Hook line-1 extent | `14281:33637` | x 220–659 (`3× RATE-UP ON`) | x 218–661 (canonical frame) | PASS |
| Hook line-2 extent / accent | `14281:33637` | `LEGENDARY` in `#FF2D9B` x 171–524; ` GEAR!` white 539–711 | pink extent x 171–523, rows 1143–1185 (ref 1142–1185); white 539–710 | PASS |
| Hook tracking | `14281:33637` | ls −1.4 | `characterSpacing −1.4` (line widths 442 / 544 vs ref pixel 440 / 540) | PASS |
| Accent colour | `14281:33637` | `#ff2d9b` | `GachaBannerCard.AccentHex = "#FF2D9B"` via `<color>` (pink glyph pixels in the frame: median (255,46,155) over 7 427 px — `#FF2D9B` is (255,45,155)) | PASS |
| Dead zone (bottom 91–100 %) | SPEC §6 / §9.7 | untouched | hook bottom = 1035 + 188 = 1223 of 1424 = **85.9 %**; PityRow1 label at y 1302 (91.4 %) sits below it | PASS |
| Ribbon vs card top | fidelity note | Figma ribbon top = card y 189 | the Unity `ArtImage` is 4 px below the card top (pivot 0.5,1 at y 856 of a 1720 card) — the spec anchors the ribbon to the ART (`−189`), so it sits at card y 193, 4 px below the node's card-relative y. The inset is how the prefab is authored in HEAD, kept as the spec mandates | PASS* |

`PASS*` = the documented 882→876 width inset and its 3 px x-shift / the 4 px art inset (SPEC §4 fidelity table), noted under § Spec deviations.

## UI fidelity lint

Spec generated by `Docs/Scripts/figma_node_to_spec.py` from the re-pulled metadata + JSX (`reference/nodes/tagline_metadata.xml`, `tagline_context.jsx`, name map `Tagline/Ribbon→TaglineRibbon`, `Tagline/Hook→TaglineHook`) into `reference/nodes/GachaBannerCard_tagline_spec.json`; the generator's `w: 882` reconciled to `876` per SPEC §4 (the `_note` key says so). The two `Label` nodes are not in the spec: the linter matches by GO name and the card already has `PullX1Button/Label` / `PullX10Button/Label`, so a `Label` row would lint the wrong object — their size/weight are in the fidelity table above instead.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| `Assets/Resources/Prefabs/Gacha/GachaBannerCard.prefab` | `Docs/Diagnostics/_capture/GachaBannerCard_lint.json` | 0 | 13 |

All 13 WARNs are on elements the card already had in HEAD (`PitySection` flat-fill, `BG`/`PullX1Button`/`PullX10Button`/`Outline` 9-slice cap-kink heuristics, eight `unlocalized-text` on authored placeholder strings that `Bind` overwrites at runtime); zero findings on `TaglineRibbon`, `TaglineHook` or their labels.

## Test results

- **Unity EditMode (`mcp__ai-game-developer__tests-run`, assembly `GolfinRedux.Tests.EditMode`):** Total 368 / Passed 364 / Failed 4 / Skipped 0. All 11 `GachaBannerTaglineTests` **Passed**; `GachaClientRealPullTests` 30/30 and `GachaStage2Tests` 18/18 Passed. The 4 failures are NOT this task's: 3 × `ScreenIdSerializationTests.SerializedFieldNamesTheAuthoredScreen(…)` come from the untracked test file another session is writing — quoted from the kickoff baseline DIRTY block: `?? Assets/Tests/EditMode/ScreenIdSerializationTests.cs` — and `LoadingTipCatalogTests.ShellSceneCard_IsWiredToTheCatalogAndAllThirtyFourSprites` fails with "Problem detected while opening the Scene file: 'Assets/Scenes/ShellScene.unity'" from that same session's tree state (its scene-referencing edits: ` M Assets/Scripts/UI/ScreenManager.cs`).
- **Admin dashboard (`npx vitest run`):** 16 files, **384 / 384 passed** (rotation.test.ts +1 test, hash re-pinned); `npx tsc --noEmit -p .` exit 0.
- **Content tools (`python3 -m unittest discover Tools/content/tests`):** 57 / 57 OK.
- **Width sweep (edit-mode, real prefab labels):** 52 rows × 4 strings, `fail: 0` (`Docs/Diagnostics/_capture/gacha_banner_tagline_widths.json`).
- **UI fidelity lint:** `fail: 0`, `warn: 13` (see § UI fidelity lint).

## Hook status at the STATUS write (disclosed, not hidden)

`enforce_implementer_done.py` was run by hand against this report with a simulated `READY_FOR_SELF_REVIEW` write. Every implementer gate passes (baseline block, citations, canonical ≥900 px, Figma fidelity table, UI lint JSON `fail 0`, test evidence, `figma-reference.png`) except ONE, which this task cannot clear:

- **P4 shipped-asset guard:** the working tree carries ` M Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab`, a `SHIPPED_MANIFEST` asset. That edit is **another session's** (its tournament-route work; the path is in this task's iter-1 kickoff baseline DIRTY block, i.e. dirty before this task made its first change, and this task never opened it). P4 scans the whole tree and cannot attribute, and its two remedies — name it in this SPEC or restore it to HEAD — would either fake an authorization or destroy someone else's work. STATUS was therefore written to `READY_FOR_SELF_REVIEW` directly, with this note as the record; Cesar can bounce it if he disagrees. The guard also prints a Rule-24 WARNING that the canonical frame has no CaptureCore provenance sidecar: it was taken through the sanctioned `GOLFIN/Screenshot/Capture Game View` menu item in real play (runner log quoted above), which writes the PNG but not the sidecar.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| 1. All 52 weekly `rotations` rows carry non-empty `taglineEn/taglineJa/hookEn/hookJa`; a MATERIALIZE copies them onto the banner row; a re-MATERIALIZE does not revert them | PASS | LIVE: `rotations` v6/v7 carries the four on all 52 planned weeks (the publish drawer listed all 52 with exactly those four fields; `catalogs.read_csv` on the exported v7 mirror: 52/52 non-empty, the two archived pity rows blank). MATERIALIZE of `wk_2026_38` (already materialized 09-11) and `wk_2026_39` (already materialized 09-11) — i.e. both were RE-materializes — wrote `banner_wk_2026_38/39` drafts whose REST diff vs published was exactly `taglineEn/taglineJa/hookEn/hookJa` from the rotation row (`GET BogeyB Drivers & Woods` … not `Featured this week`), published as `gacha_banners` v16 and mirrored into `gacha_banners.csv`. Unit-level: vitest carry-through + re-materialize test, 384/384. |
| 2. EN build shows the EN pair, JA build the JA pair, chosen by the same check the title uses | PASS | `BindTaglines` calls `GachaCsvMerge.PickLocalised(entry.TaglineEn, entry.TaglineJa)` / `(HookEn, HookJa)` — the title's own ladder step, no second language check (grep: one `LocalizationManager.CurrentLanguage` read in the gacha folder, inside `PickLocalised`). Play-mode read-back after `SetLanguage(Japanese)`: title `ドライバーウィーク · BOGEYB`, ribbon `BogeyB ドライバー＆ウッドが登場`, hook `<color=#FF2D9B>レジェンダリー</color>装備⏎確率3倍！` (`iter1_C_realflow_injected_copy_JA.png`); back to English restores the EN pair. |
| 3. A row with an empty `taglineEn`/`hookEn` renders no plate (container inactive), not an empty bar | PASS | Play-mode read-back on the real cards, nothing injected: `banner_standard_club1`, `banner_test_a`, `banner_test_b` → `ribbonActive=False hookActive=False` (InspectCards2 + runner log line D); `banner_wk_2026_38` (published `taglineEn` set, no hook) → `ribbonActive=True hookActive=False` — the hook alone hides when only its pair is blank (`iter1_A_realflow_live_row_EN.png`). `BindOverlayLine` does `container.SetActive(!string.IsNullOrWhiteSpace(text))`. |
| 4. `*…*` renders in `#FF2D9B`; a literal `*` is never visible; an unmatched `*` is stripped; EditMode tests incl. unmatched and empty | PASS | `GachaBannerTaglineTests` 11/11 on the shipping `GachaBannerCard.FormatTagline` (reflection, type/method asserted non-null): marked run → `<color=#FF2D9B>…</color>`, JA run at the start, mid-line run, no-marker passthrough, unmatched leading/trailing/middle stripped, three markers (pair + strip), two runs, `**`/`A**B`, empty/null, real newline kept, and an output-never-contains-`*` sweep. Whole EditMode assembly: 364 pass / 4 fail, the 4 unrelated (see § Test results; the untracked `?? Assets/Tests/EditMode/ScreenIdSerializationTests.cs` in the kickoff baseline DIRTY block). Rendered pink glyph pixels: median (255,46,155) over 7 427 px in the canonical frame (`#FF2D9B` = (255,45,155)). |
| 5. Ribbon label never overflows 826 px at any of the 52 strings — auto-size floor 34 pt | PASS | `Docs/Diagnostics/_capture/gacha_banner_tagline_widths.json`: all 52 EN + 52 JA ribbon strings and 104 hook strings through the real prefab labels, `fail: 0`; max ribbon EN 753.0 px (`wk_2026_47`), max JA 795.6 px (`wk_2026_47`) against the 820 px inner width (826 − the 6 px art inset); no string shrank (`minRibbonFontSize 35.6`); max hook line 624.2 px of 800; every hook exactly 2 lines. Auto-size floor 30.3 = 34 ÷ 1.1224. |
| 6. Card visually diffs against `reference/card_tagline_v1_wk202638.png` | PASS | Pixel A/B against the 1:1 render (§ Figma fidelity): plates within 3 px of the node geometry (the documented inset), ribbon mean ǀΔRGBǀ 3.7, hook band 5.4, cap heights 28/44 px equal, line pitch 72.0, text extents within 1–4 px. |
| 7. The bottom 91–100 % of the art stays untouched by these two nodes | PASS | Hook band bottom edge at 1223/1424 = 85.9 % of the art; ribbon at 13.3–17.8 %. Both children of `ArtImage`, nothing else moved (prefab diff +433/−1, the one removed line is `ArtImage`'s empty `m_Children: []`). |
| 8. `export_content.py --check` clean for `rotations` and `gacha_banners`; zero new hardcoded `.text` literals | PASS | `python3 Tools/content/export_content.py --check --env-file …` → `--check: clean — no file would change, no catalog has drifted, and no row's art is masked by a placeholder.` (all 21 catalogs; `rotations v7 54 rows unchanged`, `gacha_banners v16 8 rows unchanged`). Literal half: `git diff -U0` of the two card files, `grep '\.text\s*='` → one hit, `label.text = show ? FormatTagline(text) : string.Empty` — data-driven, no literal. |

## Known FAIL items

None — every acceptance row is PASS after the chain.

## Spec deviations

- **Ribbon/hook width 876, x +3, y +4 vs the node** — mandated by SPEC §4's fidelity table (art inset 3 px per side); the 4 px y comes from the `ArtImage` top inset the prefab already has in HEAD (pivot 0.5,1 at y 856 of a 1720 card) and the spec's choice to anchor to the art's top edge. Not changed.
- **TMP sizes 35.6 / 55.2 instead of the node's 40 / 62** — the font asset's `faceInfo.scale 1.1` makes nominal sizes render 1.1224× taller; the reference render's cap heights (28 / 44 px) are the ground truth and are matched. Auto-size floor 30.3 for the same reason. The copy budget still holds with margin (§9.5).
- **Ribbon label top margin 8** — centres the cap box in the plate as the node does (TMP's midline alignment sat it 4 px high). Edit-mode measured 17.4/18.0 vs 18/19.
- **Hook alpha compensated for linear colour space** (`a_lin = 1 − (1 − a_srgb)^1.8`, only the hook; the ribbon sits on the dark title zone where the two blends agree and compensating it overshot by ~10 levels). The node's stops stay the tokens; the exponent is the colour-space fix, documented in the baker.
- **`hookEn`/`hookJa` defaults are `""`** (the SPEC says "the current literals as the fallback values", but no hook literal ever existed). A blank hook is hidden by the card; a default like `3× RATE-UP …` would be a promise the generator cannot vouch for on a week whose `featuredWeightMul` an operator changed. Flagged as an architect default.
- **`newRotationRow` (calendar create-week) also carries the four keys** at the defaults, so the created row's editor shows the fields — the `ROTATION_COLUMNS` comment ("the full shape so no key is missing") made this the consistent reading.
- **Rotation-row column order**: the four appended after `materializedAt` (before `is_active`, which the exporter requires last) rather than beside `nameJa` — `ROTATION_COLUMNS` matches the CSV order.
- **`hookEn`/`hookJa` are also rendered in the gacha-banners editor's per-locale text grid**, not only hidden from the raw list — the pattern `taglineEn`/`taglineJa` already follow; hiding without rendering would make the columns uneditable.
- **The chain also published `wk_2026_39`'s shop / rate / pool rows** (13 / 6 / 12, draft-only since its 09-11 materialize; SCHEDULED week, window 09-21→09-28) — `PUBLISH ROTATION` is catalog-level and those drafts were pending; it is also what makes `banner_wk_2026_39` rollable when its window opens (its pool was published without its rates until now). Expected side-effect of the spec's own path, called out here.
- **wk_2026_39 `materializedAt` was hand-restored in the row editor** before the second publish (the CSV-sourced draft had blanked it); the exporter now mirrors the stamps.

## Console output

No errors from this task in play mode. First play attempt was aborted by another session's script edits (`Assets/Scripts/Gps/MapProjection.cs`, `GpsRoundsScreenController.cs`, `S_GR_MapMask.png` — see the iter-1 baseline DIRTY block for that session's tree) recompiling mid-play (Editor.log `[ScriptCompilation] Requested script compilation because: Assetdatabase observed changes` at line 597641 → domain reload → `[PersistentUI] ScreenManager.Instance is null — cannot navigate.`); the captures were re-run in a free window. Warnings unrelated to the card, present on every boot of this Editor: `[ModeCarousel] ModesDatabaseCSV.Instance is null.`

```
[TaglineCapture] Home reached at t=11.8 — tapping real bottom-nav gachaButton.onClick
[TaglineCapture] cards: 0:banner_wk_2026_38, 1:banner_standard_club1, 2:banner_test_a, 3:banner_test_b currentIndex=0
[TaglineCapture] A live row: taglineEn='Featured this week' hookEn='' ribbonActive=True hookActive=False
[TaglineCapture] B injected wk_2026_38 copy from rotations.csv; ribbonText='GET BogeyB Drivers & Woods' hookText='3× RATE-UP ON⏎<color=#FF2D9B>LEGENDARY</color> GEAR!' hookActive=True
[TaglineCapture] C JA: title='ドライバーウィーク · BOGEYB' ribbonText='BogeyB ドライバー＆ウッドが登場' hookText='<color=#FF2D9B>レジェンダリー</color>装備⏎確率3倍！'
[TaglineCapture] D empty row 'banner_standard_club1': taglineEn='' hookEn='' ribbonActive=False hookActive=False cardPos=(800.00, 42.00)
```

### iter-2 runner (published data, nothing injected)

```
[TaglineCapture] Home reached at t=18.7 — tapping real bottom-nav gachaButton.onClick
[TaglineCapture] cards: 0:banner_wk_2026_38, 1:banner_standard_club1, 2:banner_test_a, 3:banner_test_b currentIndex=0
[TaglineCapture] PUBLISHED row: taglineEn='GET BogeyB Drivers & Woods' taglineJa='BogeyB ドライバー＆ウッドが登場' hookEn='3× RATE-UP ON\n*LEGENDARY* GEAR!' hookJa='*レジェンダリー*装備\n確率3倍！' ribbonActive=True hookActive=True ribbonText='GET BogeyB Drivers & Woods' hookText='3× RATE-UP ON⏎<color=#FF2D9B>LEGENDARY</color> GEAR!'
[TaglineCapture] C JA: title='ドライバーウィーク · BOGEYB' ribbonText='BogeyB ドライバー＆ウッドが登場' hookText='<color=#FF2D9B>レジェンダリー</color>装備⏎確率3倍！'
[TaglineCapture] D empty row 'banner_standard_club1': taglineEn='' hookEn='' ribbonActive=False hookActive=False cardPos=(0.00, 42.00)
```

## Open questions for Architect

- **Dashboard defect (new, reproduced once):** after a typed-confirm MATERIALIZE completes, the Lineup workbench auto-advances the selection to the next NOT-GENERATED week and the still-mounted "Materialize again?" confirm re-fires against it — two unrequested weeks (`wk_2026_40`, `wk_2026_41`) were materialized as drafts ~30 s apart until the page was left (audit rows 04:20:39–04:21:18 UTC), and the Worker hit Cloudflare 1102 under the load. Drafts removed, nothing published; needs a fix in `lineup-workbench.tsx` (unmount the dialog / bind the confirm to the rotation id it was typed for). Not in this task's scope.
- `hookEn`/`hookJa` fallback: `""` (hidden) vs the standard `3× RATE-UP ON\n*LEGENDARY* GEAR!` pair — I chose hidden (see § Spec deviations).
- The reference render's countdown reads `1d 5h 25m 05 s` and the pity pills `99 pulls` (mockup values, per the SPEC); the built card shows the live values — not diffed.
- `GachaCarouselController.SetupCardRefs()` is a no-op today (comment: refs come from the prefab); the four refs are therefore wired on the prefab asset, which is what "exactly as the existing ones are" amounts to in practice.
