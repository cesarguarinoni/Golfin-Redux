# SPEC — `gps_profile_pack`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Wire the hub's **PROFILE** tab: three read-only screens from the approved Figma frames — **Profile**, **My Avatar**, **Badges** — over endpoints that already exist (`/user/detail`, `/score/stats`, `/badges/progress`). No native work, no writes. It also lights up the hub's BADGES / MY AVATAR shortcut tiles and the Profile nav button, and gives the avatar screen the player's **real game character** instead of a placeholder — the first visible link between PLAYLIFE avatar level and GOLFIN characters.

## Build rules carried over from Cesar's fixes on `gps_hub_entry` / `score_upload_flow` (NOT optional)

These are the things Cesar rejected on sight in the last two tasks. Every one applies here; the report must show each was followed.

1. **Gradients are baked from tokens, never tinted.** A tinted `S_PillStadium` / `Next Hole Panel` is one flat colour. Any node whose SVG has `fill="url(#paint0_linear…)"` gets a baked sprite from a script in `Docs/Scripts/` (`make_gps_hub_panels.py`, `make_gps_icon_ring.py` — extend them or add `make_gps_profile_panels.py` in the same style; edit the SCRIPT, never the PNG). `UI_ELEMENT_PALETTE.md` § *Baked-from-tokens sprites*.
2. **Translucency is solved in linear space against its real backdrop.** Use the builder's `A(colour, alpha, backdrop)` (and `ADark` only for near-black overlays) exactly as `ScoreUploadScreenBuilder.cs:119-144` does. No `new Color(1,1,1,0.15f)`.
3. **Circular badges/markers are navy disc in a gold ring**, not accent tints (`BadgeNavy #112D4F` + gold ring; see the step-4 GPS marker / step-6 star). The badge grid's 60 rings and the evolution-timeline rings follow this.
4. **`Main Buttons` labels are size 59** (calibrated against the render; 66 was 12 % too wide), ≤ 18 characters.
5. **Node geometry is machine-checked.** Build from a `reference/nodes/<Screen>_geometry.json` (same generator as `GpsHubScreen_geometry.json`), run the invariants audit and the UI fidelity lint; report `N sites 0 FAIL 0 GONE`, `lint fail=0`, and the per-screen mean |ΔRGB| table (photo / UI column) as in the score-upload report.
6. **`‹ BACK`, counters and small nav labels are SemiBold white** where the node says so — read the node, don't assume muted.
7. **Every new text key is PUBLISHED, not just in the CSV** (`feedback_always_publish_new_text`): CSV → importer PLAN/APPLY → publish `texts` → `export --check` clean → Unity table regenerated. A CSV-only key renders as the raw key.
8. **Screens are built by an Editor builder script** (`Assets/Scripts/UI/Gps/Editor/…Builder.cs`) that can re-run and re-seed a populated state for the fidelity pass; the prefab is its output, not hand-edited.
9. **Reuse the hub atoms**: `S_HUB_*` panels, `S_GpsIconRing_*`, `GPS Icons` sprites, the hub nav bar instance, the `Navy70` strip, `GpsHubRoundRow`. If a new atom is needed, add it to the palette doc with its baker.

## Reference

- **Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page GPS / PLAYLIFE, section *Hub tabs*: **Profile `14025:33087`**, **My Avatar `14026:33187`**, **Badges `14027:33298`**. Renders → `reference/`; node SVG/geometry → `reference/nodes/`.
- **Endpoints (playlife/backend/routers):**
  - `GET /user/detail` → `profiles` row (already `UserDetailDto` in `Golfin.Social`).
  - `GET /score/stats` → `{data:{best_score, avg_score, handicap, rounds_count, monthly:[{month, count, avg, best}]}}` (`score.py:358-416`; `avg_score` may be null; `monthly` newest-first, from `screenshot_data.score`).
  - `GET /badges/progress` → `{data:{badges:[{…badge_definitions row…, earned:bool, earned_at}], total, earned, completion_pct}}` (`badges.py:57-80`). Definitions: `id, name (JA), description (JA), category golf|social|trust|special, rarity common|rare|epic|legend, icon_url (null for all 24 seeds), target_pct (nullable), sort_order` (`20260409…dual_currency` migration `:141-190`; 24 seeds, 8 golf / 8 social / 4 trust / 4 special).
  - `GET /score/history?limit=` — already `ScoreHistoryService`.
- **Game character art:** `HomeScreenController.UpdateHomeCharacterImage` (`:232-250`) — `CharacterManager.Instance.GetSelectedCharacterId()` → `CharacterDatabaseCSV.Instance.GetCharacter(id).characterName` → `Resources.Load<Sprite>($"Characters/Homescreen/{name}")` with `Placeholder` fallback. Reuse exactly this resolution.
- **Avatar level maths (PLAYLIFE):** level-up at `500 × level` XP (`GPS_INTEGRATION_REFERENCE.md` §7); `avatar_level` / `avatar_xp` on the profile.

## Figma Fidelity (enumerate EVERY element — Rule 18)

Shared: shared top bar via `ShowTopBarOnly()` + `NavTitleKeyFor` (titles `GPS_PROFILE_TITLE` / `GPS_AVATAR_TITLE` / `GPS_BADGES_TITLE`), hub nav bar instance with **Profile slot active** on all three, `‹ BACK` strip row like the hub's (Profile → hub; Avatar/Badges → Profile). Backgrounds: reuse the hub's background asset (per-frame backgrounds are Figma-only, as before).

### Profile — `14025:33087`

| Element | Node | Property → value |
|---|---|---|
| Hero panel | Profile Hero | 170 avatar disc (initial, gold ring), name 54 gold (`display_name` upper), sub 28 muted `@{handle} · HC {handicap} · {home course}` → **v1: `HC {handicap}` + `{activities_count} rounds`** (no handle or home-course fields exist — deviation), stats row: `{followers_count}` FOLLOWERS pink · `{activities_count}` ROUNDS white · `Lv.{avatar_level}` AVATAR gold · `{total_points}` POINTS green |
| Trust panel | Trust Panel | `✓ TRUST LEVEL` green 30 + `{trust_level}%` 34, 900×16 track (green, `A(white,.15,backdrop)` base), note `GPS_PROFILE_TRUST_NOTE` 24 muted |
| Quick stats | Quick Stats | BEST `{best_score}` gold · AVERAGE `{avg_score:0.0}` · AVG PUTTS **`—` (no putts data — deviation)**; `—` for nulls |
| Gift totals | Gift Totals | GIFTS RECEIVED `{gift_pts} pts` pink / GIFTS SENT **`—`** (no sent total on the profile) — pink/gold tinted panels via baked sprites (they are flat fills at 0.85 → `ADark` is valid) |
| Shortcuts | Shortcuts | BADGES (`{earned} / {total} earned` from `/badges/progress`) → Badges screen · GIFT SHOP (`GPS_PROFILE_SHOP_SUB`) **inert, logs** · MY AVATAR (`Lv.{n} · {xp}/{next} XP`) → Avatar screen |
| Recent rounds | Recent Rounds | header + `SEE ALL ›` (**hidden v1**), rows = `GpsHubRoundRow` × 2 from `/score/history?limit=2`; hidden when empty |
| Edit profile | Main Buttons Silver `GPS_PROFILE_EDIT` | **v1 inert, `Enabled=No` variant** (profile edit = `/user/update`, own task) |

### My Avatar — `14026:33187`

| Element | Node | Property → value |
|---|---|---|
| Stage | Avatar Stage | green gradient panel (baked); **the figure is the player's selected GOLFIN character** — same sprite resolution as Home, `preserveAspect`, clipped by a 560×600 mask, head at the top like the frame; falls back to `Placeholder` |
| Equip slots | Equip Slots | CAP · SHIRT · GLOVE · SHOES · CLUB rings — **v1: all five rendered at the "off" state (ring 0.5, label muted), non-interactable** (equip = `/gifts/inventory`, v2) |
| Level row | Level Row | `Lv.{avatar_level}` gold pill + rank title from level: 1–4 BEGINNER, 5–11 ROOKIE, 12–19 AMATEUR, 20–49 SINGLE, 50+ PRO (`GPS_AVATAR_RANK_*`) |
| XP panel | XP Panel | `Lv.{n} → Lv.{n+1}`; hint `GPS_AVATAR_XP_HINT_FMT` (`{0} more rounds`) where rounds = `ceil((next − xp) / 50)` (50 = screenshot points per posted round — label it an estimate in the note); track = `xp / (500·n)`; footer `{xp} / {500·n} XP` + `GPS_AVATAR_XP_CTA` |
| Evolution | Evolution Panel | five stages BEGINNER Lv.1 · ROOKIE Lv.5 · AMATEUR Lv.12 · SINGLE Lv.20 · PRO Lv.50 — done = green icon ring, current = 88 ring with 6 px gold stroke, locked = 0.55 opacity muted |
| Unlocks | Unlock Panel | **hidden in v1** (gift items) |
| Status | Status Panel | POWER / TECHNIQUE / MENTAL bars — **v1: replaced by the selected game character's four stats** (Strength · Club Control · Recovery · Stamina from `CharacterManager` / `RarityStatCaps`, value/cap as the bar) with header `GPS_AVATAR_STATUS`; delta column hidden. This is the deliberate bridge between the two systems; document it as a deviation and screenshot it |

### Badges — `14027:33298`

| Element | Node | Property → value |
|---|---|---|
| Collection panel | Collection Panel | Star icon + `GPS_BADGES_COLLECTION` gold 34, `{completion_pct:0}%` 36 gold, 900 track gold, note `GPS_BADGES_EARNED_FMT` (`{0} / {1} badges earned`) |
| Sections | Section GOLF / SOCIAL / TRUST / SPECIAL | icon (Rounds / Heart / Pin / Sparkle) + title; badges grouped by `category`, ordered by `sort_order`; 4 per row |
| Badge cell | Badge … | earned: fill `A(white,.10,bg)`, 2 px stroke in rarity colour, `✓` green top-left; locked: `ADark(black,.25)`, 1 px `#4a5a6e`, ring at 0.6. Rarity tag 14 SemiBold: COMMON muted · RARE `#6fa5e8` · EPIC `#b48cf0` · LEGEND gold. Ring = Star icon in the navy-disc-gold-ring atom, icon tinted rarity colour when earned (`icon_url` is null on every seed — no remote icon in v1). Name 18 (EN via CSV key `BADGE_{id}_NAME`, JA from the CSV too — seeded from the DB `name`), `{target_pct}%` 16 muted or blank when null |

Placeholder vs canonical: all numbers on the frames are mock; the strings are canonical and go through the CSV.

## Architecture context

- **Asmdefs:** `Golfin.Gps` gains `ScoreStatsService` + `ScoreStats` DTO, `BadgeService` + `BadgeProgress`/`BadgeDefinition` DTOs (module code, refs `Golfin.Net` only). Screens/controllers/builder in `Assembly-CSharp` (`Golfin.Gps.UI`) — they may reference `CharacterManager`, `CharacterDatabaseCSV`, `RarityStatCaps` (game side is fine here; the module stays game-free).
- **ScreenIds:** `GpsProfile`, `GpsAvatar`, `GpsBadges` — registered like `GpsHub`, `ShowTopBarOnly` group, `NavTitleKeyFor` cases, menu music on, post-auth, not demo.
- **Hub wiring (`GpsHubScreenController`):** Profile nav slot → `GpsProfile`; BADGES shortcut → `GpsBadges`; MY AVATAR shortcut → `GpsAvatar` (the hub has no shortcuts row — those live on the Profile screen; the hub only gets the nav slot). Remove the "not wired" logs for Profile.
- **Endpoints.cs:** append `ScoreStats`, `BadgesProgress` to the GPS section.
- **Reused untouched:** `UserService` (+ `OnDetailChanged`), `ScoreHistoryService`, `GpsHubRoundRow`, `PointsService`, `LocalizedText`, `TelemetryService`.

## Implementation

1. **Module.** `ScoreStatsService.Stats(Action<ApiResult<ScoreStats>>)` and `BadgeService.Progress(Action<ApiResult<BadgeProgress>>)` — `PointsService` shape, cached `Last*` + `On*Changed`. DTOs Newtonsoft snake_case, all nullable but ids; `BadgeDefinition.icon_url` mapped but unused. EditMode tests: unwrap scripted payloads (incl. `avg_score: null`, `target_pct: null`, empty `monthly`), grouping by category preserves `sort_order`, rank-title thresholds, XP maths — the rule of record is `backend/migrations/2026_06_29_points_atomic.sql:47-49` (also `2026_08_12_points_spend_idempotency.sql:110-143`): `avatar_xp` is the **remainder within the current level**, and `while v_xp >= v_level * 500` levels up carrying the remainder. So next = `500 × avatar_level`, track = `avatar_xp / (500 × avatar_level)`; Lv.12 needs 6 000 XP (the frame's `650 / 1,000` is mock). Pin with a test.
2. **Builder** `GpsProfilePackBuilder.cs` (Editor) with three entry points, geometry-JSON driven, `A()`/`ADark()` copied from `ScoreUploadScreenBuilder` (move them to a shared `GpsUiColor` static if that is the smaller diff — one owner). Fidelity pass seeds a populated state per screen (a profile with all stats, 8/24 badges, avatar Lv.12/650) so the render compares like-for-like.
3. **Controllers** `GpsProfileScreenController`, `GpsAvatarScreenController`, `GpsBadgesScreenController` — `OnEnable` subscribe + fetch, `OnDisable` unsubscribe; `—` before data, never `0`; errors log once at Warning; re-bind on `OnLanguageChanged`. Avatar stats read `CharacterManager.Instance` selected character + `RarityStatCaps` for the bar max (NOTE the exact accessor names in the report; `PlayerCharacterData` holds the four stats).
4. **Navigation:** Profile `‹ BACK` → `GoBack(GpsHub)`; Avatar/Badges `‹ BACK` → `GoBack(GpsProfile)`; hub nav Profile slot from any GPS screen → `GpsProfile`.
5. **Telemetry:** `gps_profile_open`, `gps_avatar_open`, `gps_badges_open` via `RecordSafe`.
6. **Strings** (EN + JA, CSV → importer → **publish**): `GPS_PROFILE_TITLE, GPS_AVATAR_TITLE, GPS_BADGES_TITLE, GPS_PROFILE_SUB_FMT, GPS_PROFILE_STAT_FOLLOWERS/ROUNDS/AVATAR/POINTS, GPS_PROFILE_TRUST, GPS_PROFILE_TRUST_NOTE, GPS_PROFILE_BEST/AVERAGE/AVG_PUTTS, GPS_PROFILE_GIFTS_IN/OUT, GPS_PROFILE_SHORTCUT_BADGES/SHOP/AVATAR, GPS_PROFILE_SHOP_SUB, GPS_PROFILE_BADGES_SUB_FMT, GPS_PROFILE_AVATAR_SUB_FMT, GPS_PROFILE_RECENT, GPS_PROFILE_EDIT, GPS_AVATAR_SLOT_CAP/SHIRT/GLOVE/SHOES/CLUB, GPS_AVATAR_RANK_BEGINNER/ROOKIE/AMATEUR/SINGLE/PRO, GPS_AVATAR_LEVEL_FMT, GPS_AVATAR_NEXT_FMT, GPS_AVATAR_XP_HINT_FMT, GPS_AVATAR_XP_FMT, GPS_AVATAR_XP_CTA, GPS_AVATAR_EVOLUTION, GPS_AVATAR_STATUS, GPS_AVATAR_STATUS_NOTE, GPS_BADGES_COLLECTION, GPS_BADGES_EARNED_FMT, GPS_BADGES_SEC_GOLF/SOCIAL/TRUST/SPECIAL, GPS_BADGES_RARITY_COMMON/RARE/EPIC/LEGEND`, plus **`BADGE_{id}_NAME` × 24** (JA = the seed `name`, EN authored: First Round, Break 110, Break 100, Break 90, Break 80, 5 in a Row, 10 in a Row, 10 Courses, First Gift In, First Gift Out, 100 Gifts In, 100 Followers, 1K Followers, 10K Followers, First Vote, 10 Vote Hits, First GPS Proof, Trust 80%, Trust 100%, 5 Friend Confirms, Monthly MVP, Tournament Win, Gift Top 10, All Badges — seed ids, in order: `first_round break_110 break_100 break_90 break_80 streak_5 streak_10 courses_10 first_gift_recv first_gift_send gifts_100 followers_100 followers_1000 followers_10000 first_vote vote_hits_10 first_gps trust_80 trust_100 social_verify_5 monthly_mvp tournament_win gift_king all_badges` — `gift_king` = Gift Top 10). ~75 rows. Character stat names reuse the existing roster keys.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Build-rule compliance, one line each for rules 1–9 above (which baker, where `A()` lives, label size, geometry `N sites 0 FAIL`, lint, publish version).
- [ ] EditMode tests for the module pass; suite count before/after.
- [ ] Editor, signed in: Profile shows live `/user/detail` + `/score/stats` values (quote both log lines), Badges shows `/badges/progress` with the account's real earned set, Avatar shows the selected game character + level/XP from the profile — screenshots of all three, plus each with `—` before data.
- [ ] Navigation: hub Profile slot → Profile → Badges → back → Avatar → back → hub, and the hub nav Profile slot from Badges lands on Profile — screenshots or log lines.
- [ ] Figma fidelity per row for all three frames; deviations listed (sub-line fields, AVG PUTTS, GIFTS SENT, equip slots off, unlocks hidden, status = character stats, no remote badge icons, SEE ALL hidden, EDIT PROFILE disabled).
- [ ] Strings: all rows EN+JA, PLAN/APPLY, published version, `--check` clean, zero hardcoded literals; the 24 badge names verified against the seed ids.
- [ ] XP rule (`points_atomic.sql:47-49`, remainder-within-level, `500 × level`) pinned by a test.
- [ ] Telemetry rows seen then deleted. Console clean. `[SerializeField]` wired. Deviations flagged.

## Files / hierarchy this task touches

- `Assets/Scripts/Gps/ScoreStatsService.cs`, `BadgeService.cs`, `ProfileDtos.cs` (+ tests) — NEW
- `Assets/Scripts/Net/Endpoints.cs` — two URLs appended
- `Assets/Prefabs/UI/Gps/GpsProfileScreen.prefab`, `GpsAvatarScreen.prefab`, `GpsBadgesScreen.prefab` — NEW
- `Assets/Scripts/UI/Gps/GpsProfileScreenController.cs`, `GpsAvatarScreenController.cs`, `GpsBadgesScreenController.cs`, `BadgeCellView.cs`, `Editor/GpsProfilePackBuilder.cs` — NEW; `GpsHubScreenController.cs` — Profile slot wired
- `Docs/Scripts/make_gps_profile_panels.py` (or extensions) + `Assets/Art/UI/Gps/S_*.png` outputs; `Docs/Architecture/UI_ELEMENT_PALETTE.md` — new atoms listed
- `Assets/Scripts/UI/ScreenManager.cs`, `PersistentUIManager.cs` — three ScreenIds
- `Assets/Localization/LocalizationText.csv` — ~75 rows (+ importer + publish)
- `Docs/AI_CONTEXT.md` — at close-out

## Smoke evidence

Editor run per screen (populated + empty states), fidelity table with ΔRGB per screen, geometry/lint gate output, strings publish log, telemetry SQL.

## Out of scope (do NOT do these)

- Editing the profile (`/user/update`), avatar upload, follow/unfollow, other users' profiles (`/user/{id}/profile`), the 5-axis ranking screen.
- Equip/inventory (`/gifts/inventory`), unlock previews, the gift shop.
- Remote badge icons (`icon_url` is null on all seeds); badge detail modals.
- Any change to `CharacterManager` / roster data; the avatar screen only reads.
- The check-in / ROUNDS tab.
