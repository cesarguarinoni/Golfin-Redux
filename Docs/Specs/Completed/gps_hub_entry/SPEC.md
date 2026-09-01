# SPEC — `gps_hub_entry`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Give the GPS / PLAYLIFE features a front door in the game **today**, before any feature screen exists: the Home promo banner (the strip under Mode Selection) stops opening a web URL and instead **navigates to a new GPS hub screen**, built from the approved Figma frame. The hub ships with real profile numbers, the four action tiles (inert until their specs land), and the player's own recent rounds. Everything else on the frame is present but dormant.

Decision of record (Cesar, 2026-09-01): *"To access the GPS part from the game whenever it's ready, for now use the banner under the Mode Selection in main screen. Activate the banner and instead of pointing to an URL it should point to the GPS main screen."*

## Reference

- **Figma:** file `5gEAHjl6xAtW8iYY7NMvWd`, page **GPS / PLAYLIFE**, frame **`GPS Hub - Home`** node `14011:32819`. Components used: `GPS Nav Bar Container` (`14021:32953`), `GPS Icons` set (`14019:32965`), `Nav Bar Icons` (Home / Characters / Rounds / Gift / Camera variants), `Main Buttons / Gold - Small`, the `Pop-up` panel style (`4192:31365`).
- **Node renders to drop in `reference/`:** `14011:32819` (whole frame), the hero panel `14012:32489`, the tiles row `14012:98859`, `14021:32953` (nav bar). Pull with `get_screenshot`.
- **Banner today:** `Assets/Scripts/BannersRuntime/BannerSlotBinder.cs` (`OpenLink` → `Application.OpenURL`), `BannerPolicy.cs` (`IsLinkAllowed`: https + host allowlist `golfin.io` / `golfin.world`), `HomeScreenController.OnPromoBannerClicked` (calls `binder.OpenLink()`), placement `BannerPlacement.HomePromo` served by `GET /banners` (`playlife/backend/routers/banners.py`, table `game_banners`, admin panel `Tools/admin-dashboard/app/(panels)/banners/`, link validation `Tools/admin-dashboard/lib/banner.ts::validateBannerLinkUrl`).
- **Navigation:** `ScreenManager.ShowScreen(ScreenId)` / `GoBack` (`Assets/Scripts/UI/ScreenManager.cs`), `PersistentUIManager.ShowTopBarOnly()` / `ShowBars()` / `SetUsername` (`Assets/Scripts/UI/PersistentUIManager.cs`).
- **Data already in the client:** `Golfin.Economy.PointsService.Instance.DisplayBalance` (RP), `Golfin.Auth.PlayerIdentity.DisplayName`, `Golfin.Net.ApiClient` + `Endpoints`, `Golfin.Gps` (from `gps_trust_core`).
- **Backend contracts:** `GET /api/v1/user/detail` → `{data: <profiles row>}` (`user.py:78-86`, `select("*")`; columns per `GPS_INTEGRATION_REFERENCE.md` §5 `profiles`: `display_name, handicap, best_score, avg_score, trust_level, total_points, avatar_level, avatar_xp, followers_count, following_count, activities_count`). `GET /api/v1/score/history?skip=&limit=` → `{data: [<activities rows>]}` ordered by `check_in_at` desc (`score.py:419-436`).

## Figma Fidelity (enumerate EVERY element — Rule 18)

Canvas 1170×2532; content column x=96 w=978, y=361..2221; panel style = `Pop-up`: fill gradient `#133453→#091b33`, 3 px gradient stroke `#fff→#d1d5db@0.4→#818ea1`, r=50, drop shadow 20. Gold = `#eedc9a`, muted = `#b7c3d3`, green = `#7ed488`, pink = `#f07f9c`, blue = `#6fa5e8`. Font Rubik (existing TMP asset).

| Element | Figma node | Property → value |
|---|---|---|
| Top bar | shared `PersistentUI` top bar via `ShowTopBarOnly()` | title text → `GPS_HUB_TITLE` ("GOLFIN GPS"); RP pill + gear as on Home |
| Back row | **new, not in Figma yet — added 2026-09-01 as `Back Row` in the frame** | `‹ BACK TO GAME` Rubik Medium 28 muted, left-aligned, 60 px tall strip at top of content; tap → `ScreenManager.GoBack(fallback: ScreenId.Home)` |
| Hero panel | `14012:32489` | 978×~309; avatar 140 ring (`#f3ecc2` 6 px) with initial; name Rubik SemiBold 54 gold; sub line Rubik Medium 32 muted `HC {handicap} · {followers} followers`; separator 2 px white gradient; 4 stats (value 48 / label 26 muted): `{total_points}` POINTS gold · `{best_score}` BEST white · `{trust_level}%` TRUST green · `Lv.{avatar_level}` AVATAR pink |
| How-it-works strip | `14017:32692` | 978×122, panel style r=32; 4 steps (64 ring + `GPS Icons` Camera/Sparkle/Pin/Star at 34, label Rubik Medium 24) with `›` between |
| Action tiles | `14012:98859` | 4 × `Tile` panels r=32, 88 ring + icon 48 (Screenshot / Pin / Heart / Gift), label Rubik SemiBold 26. **v1: all four `Button.interactable = false`**, opacity 1 (they are the promise, not greyed out); tap does nothing, logs `[GpsHub] tile <name> — not wired yet` |
| RECENT GIFTS panel | `14012:98880` | **present in prefab, GameObject INACTIVE in v1** (v2 feature) |
| LIVE VOTES panel | `14012:98914` | **present in prefab, GameObject INACTIVE in v1** (v2 feature) |
| FRIENDS' ROUNDS panel | `14012:98941` | **rebound as MY RECENT ROUNDS** — header `GPS_HUB_RECENT_ROUNDS`, `SEE ALL ›` hidden in v1; rows from `/score/history?limit=3`: avatar 80 with initial, `venue_name` Rubik SemiBold 30, relative date Rubik Medium 24 muted, `● Trust {trust_level}%` green 24; right: score 44, `(+{score−par})` muted — par unknown in the row → show `({score_type} holes)` instead; `BEST` gold tag on the row equal to `best_score`. Empty → panel hidden (`SetActive(false)`), no empty-state copy in v1 |
| GPS nav bar | `14021:32953` | 1170×263 at frame bottom, built INSIDE the hub prefab (the shared bottom nav is hidden by `ShowTopBarOnly()`): Home (active, gold ring) · Rounds · **camera centre 210** · Gift · Profile; **v1: only Home is interactable and it is a no-op**; the other four log `[GpsHub] nav <name> — not wired yet` |
| Background | `Backgrounds/Property 1=Test` | reuse the same background asset the Home screen already uses for this variant, or the closest existing one; do NOT import a new full-screen texture for this task |

Placeholder vs canonical: every number on the Figma frame is mock (Maya / 2,480 / 92 / 80% / Lv.8) — the prefab binds live values; the strings are canonical and go through the CSV (§Strings).

## Architecture context

- **Asmdefs:** `Golfin.Net` (append `UserDetail`, `ScoreHistory` URLs), NEW `Golfin.Social` (`Assets/Scripts/Social/Golfin.Social.asmdef`, refs `Golfin.Net` only, `precompiledReferences: Newtonsoft.Json.dll`, autoReferenced — the module the plan calls Social; starts with one service), `Assembly-CSharp` (screen, controller, banner routing).
- **Existing code touched:** `Assets/Scripts/BannersRuntime/BannerPolicy.cs`, `BannerSlotBinder.cs`, `Assets/Scripts/UI/ScreenManager.cs` (`ScreenId.GpsHub` + registration + bars rule), `Tools/admin-dashboard/lib/banner.ts` + `lib/i18n.ts`.
- **Existing code reused, untouched:** `PointsService`, `PlayerIdentity`, `LocalizationManager.Get`, `LocalizedText` binders, `PersistentUIManager`, `ModalController` (not needed), `TournamentArtService.Banners` (banner art).
- **Manager APIs used:** `ScreenManager.Instance.ShowScreen(ScreenId.GpsHub)`, `ScreenManager.Instance.GoBack(ScreenId.Home)`, `PersistentUIManager.Instance.ShowTopBarOnly()`, `PersistentUIManager.Instance.HighlightScreen(ScreenId)` + `NavTitleKeyFor` (centre title per screen), `PointsService.Instance.DisplayBalance` + `OnDisplayBalanceChanged`, `ApiClient.Instance.Get<T>`.

## Implementation

### 1. Internal banner routes — `BannerPolicy.cs`, `BannerSlotBinder.cs`

```csharp
// BannerPolicy
public const string InternalScheme = "golfin";
/// golfin://gps → ScreenId.GpsHub. The ONLY internal route today; add here, never parse ad hoc.
public static bool TryGetInternalRoute(string? url, out ScreenId screen)
{
    screen = default;
    if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
    if (!string.Equals(uri.Scheme, InternalScheme, StringComparison.Ordinal)) return false;
    switch (uri.Host) { case "gps": screen = ScreenId.GpsHub; return true; }
    return false;
}
public static bool IsLinkAllowed(string? url) => TryGetInternalRoute(url, out _) || IsExternalLinkAllowed(url);
// IsExternalLinkAllowed = the existing body, renamed. Tests: BannerPolicyTests gains
// golfin://gps → allowed+GpsHub, golfin://shop → refused, GOLFIN://GPS → allowed (Uri lower-cases),
// https://golfin.io/x still allowed, javascript: still refused.
```

`BannerSlotBinder.OpenLink()`: after the allowlist re-check, `if (BannerPolicy.TryGetInternalRoute(link, out var screen)) { ScreenManager.Instance?.ShowScreen(screen); return; }` then the existing `Application.OpenURL`. Log line `[Banners] Routing banner link for {placement} → {screen}`.

An **older build** that receives `golfin://gps` shows the artwork with the button non-interactable (`IsLinkAllowed` false there) — harmless, so the row can be activated as soon as the first build carrying this spec is on TestFlight, no `min_build` gate needed.

### 2. Dashboard — `Tools/admin-dashboard/lib/banner.ts`, `lib/i18n.ts`

`validateBannerLinkUrl`: accept `golfin://gps` exactly (case-insensitive scheme/host, no path/query) before the https branch; keep everything else. Add `INTERNAL_LINK_ROUTES = ["golfin://gps"] as const` next to `ALLOWED_LINK_HOSTS`, and extend the host-rejection message: `…or one of the in-app routes ${INTERNAL_LINK_ROUTES.join(", ")}`. `DICT` gets the message in `en` and `ja` (`ADMIN_DASHBOARD_OPS.md` §3.4). The editor placeholder becomes `https://golfin.io/campaign/august  ·  golfin://gps`. Run the vitest suite (`banner.test.ts` gains the three cases above). Deploy per `ADMIN_DASHBOARD_OPS.md` (`cf-deploy.sh`) — Architect deploys, Code prepares; say so in the report.

### 3. `Golfin.Social` — `UserService` + `UserDetailDto`

`Assets/Scripts/Social/UserService.cs` — plain C# singleton (pattern: `Golfin.Economy.PointsService`, minus the queue): `Instance` / `ConfigureForTest` / `ResetForTest`, `IEnumerator Detail(Action<ApiResult<UserDetailDto>> onResult)` → `ApiClient.Get<UserDetailDto>(Endpoints.UserDetail, …)`; caches `LastDetail` and raises `event Action<UserDetailDto> OnDetailChanged`.
`UserDetailDto` (Newtonsoft, snake_case, ALL nullable except `id`): `id, display_name, avatar_url, bio, handicap (double?), best_score (int?), avg_score (double?), trust_level (int?), total_points, activity_pts, gift_pts, avatar_level, avatar_xp, followers_count, following_count, badges_count, activities_count`. Doc-comment cites `user.py:78-86` + the `profiles` column list. `MissingMemberHandling.Ignore` (default) — `select("*")` returns more than we map.
`Endpoints.cs`: append `UserDetail => BaseUrl + "/user/detail"` and `ScoreHistory(int skip = 0, int limit = 20)` to the existing GPS section (one section, appended — nothing existing changes).
`Golfin.Gps`: add `ScoreHistoryService` next to `VenueService` (same shape): `IEnumerator History(int skip, int limit, Action<ApiResult<List<ActivityDto>>>)`. `ActivityDto` already exists (`GpsDtos.cs`) — add `score (int?)`, `score_type (string)`, `input_method`, `course_name` if absent; they are on the `activities` row per `score.py:206-230`.

### 4. `ScreenId.GpsHub` — `ScreenManager.cs`

- Add `GpsHub` to the enum with the comment `// gps_hub_entry — GPS / PLAYLIFE hub (Figma 14011:32819), reached from the Home promo banner`.
- Register the prefab the way `GeneralShop` / `GachaHistory` are registered (same `[SerializeField]` slot pattern + scene wiring in `ShellScene`; quote the exact lines in the report).
- Bars rule: `GpsHub` is NOT in the `showBars` list. Add `bool isGpsHub = screenId == ScreenId.GpsHub;` → `PersistentUIManager.Instance.ShowTopBarOnly()` (top bar with RP + gear, shared bottom nav hidden — the hub draws its own) followed by `PersistentUIManager.Instance.HighlightScreen(ScreenId.GpsHub)`. Title: `PersistentUIManager.NavTitleKeyFor(screenId)` (`PersistentUIManager.cs:~505`) already maps a screen to its centre-title key and the Home case restores the username — add `case ScreenId.GpsHub: return "GPS_HUB_TITLE";` there. No new `SetTitle` API; `SetUsername` is not the mechanism (its comment says so).
- Menu music: treat `GpsHub` as a menu screen (`isMenuScreen` true) so the theme keeps playing.
- `AuthGate`: `GpsHub` is post-auth (not in the pre-auth allowlist) — verify `AuthGate.IsScreenAllowed(ScreenId.GpsHub)` is true when signed in and false when not.
- `DemoGate`: not on the demo allowlist (GPS is not in the demo).
- Back: `GoBack()` from `GpsHub` returns to Home via the normal history push (`ShowScreen` pushes); the hub's `‹ BACK TO GAME` calls `GoBack(ScreenId.Home)`.

### 5. Prefab + controller — `Assets/Prefabs/UI/Gps/GpsHubScreen.prefab`, `Assets/Scripts/UI/Gps/GpsHubScreenController.cs`

Build the hierarchy from the Figma frame (RectTransform values from the node renders; the frame is 1170×2532 = the project's reference canvas). Use existing prefabs where they exist: the top bar is shared; the gold ring buttons mirror `NavBarButton`/`New Nav Bar Buttons` in `PersistentUI.prefab` — **clone that prefab's button object for the five hub nav buttons** rather than rebuilding the ring; the centre 210 button is the same object scaled, icon swapped. Icons: export the `GPS Icons` set from Figma as white PNG @2x into `Assets/Art/UI/Gps/` (Rounds, Gift, Camera, Screenshot, Pin, Heart, Sparkle, Star) — 8 sprites, single-sprite import, ≤ 256 px each.

`GpsHubScreenController : MonoBehaviour` (namespace `Golfin.Gps.UI`):
- `OnEnable`: subscribe `PointsService.OnDisplayBalanceChanged`, `UserService.OnDetailChanged`; bind `PlayerIdentity.DisplayName` immediately; kick `ApiClient.Instance.Run(UserService.Instance.Detail(…))` and `Run(ScoreHistoryService.Instance.History(0, 3, …))`. `OnDisable`: unsubscribe. (Event-driven UI rule.)
- Hero binding: name uppercase of `display_name` (fallback `PlayerIdentity.DisplayNameOr("PLAYER")`), initial = first char; `HC {handicap:0.0}` or `HC —` when null; `{followers_count:N0} followers`; stats as in the table. Before `/user/detail` answers, values show `—` (never `0`, never the Figma mock).
- Rounds binding: rows from `ActivityDto` list; relative date from `check_in_at` (existing helper if one exists — grep `TimeAgo`/`RelativeTime`; else a 6-line local one: `today / yesterday / N days ago / N weeks ago`); hide the panel on empty or error. Errors log once at `Warning` with the `ApiErrorKind`; no toast.
- Tiles / nav: `interactable=false` + the log lines from the table. No `ModalController` "coming soon" in v1 (Cesar hasn't asked for one; do not invent).
- `‹ BACK TO GAME` → `ScreenManager.Instance.GoBack(ScreenId.Home)`.
- Telemetry: `TelemetryService.Instance.RecordSafe("gps_hub_open", () => new … { ["source"] = "home_banner" })` on `OnEnable` (same call the gacha funnel uses, `GachaCarouselController.cs:382`). Nothing else.

### 6. Strings — `Assets/Localization/LocalizationText.csv` → importer

All player-facing text below is added to the CSV with EN **and** JA in the same commit, then `python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN, read the verdicts) → `--apply` → publish `texts` from the admin → `export_content.py --check` clean. Bind every label with the existing `LocalizedText` binder; zero new hardcoded `.text` literals.

| key | EN | JA |
|---|---|---|
| GPS_HUB_TITLE | GOLFIN GPS | GOLFIN GPS |
| GPS_HUB_BACK | ‹ BACK TO GAME | ‹ ゲームに戻る |
| GPS_HUB_SUB_FORMAT | HC {0} · {1} followers | HC {0} · フォロワー {1} |
| GPS_HUB_STAT_POINTS | POINTS | ポイント |
| GPS_HUB_STAT_BEST | BEST | ベスト |
| GPS_HUB_STAT_TRUST | TRUST | 信頼度 |
| GPS_HUB_STAT_AVATAR | AVATAR | アバター |
| GPS_HUB_HOW_1 | SCREENSHOT | スクショを貼る |
| GPS_HUB_HOW_2 | AI READS IT | AIが認識 |
| GPS_HUB_HOW_3 | GPS PROOF | GPSで証明 |
| GPS_HUB_HOW_4 | EARN PTS | ポイントGET |
| GPS_HUB_TILE_SCREENSHOT | SCREENSHOT | スクショ |
| GPS_HUB_TILE_CHECKIN | CHECK-IN | チェックイン |
| GPS_HUB_TILE_VOTE | VOTE | Vote |
| GPS_HUB_TILE_GIFT | GIFT | Gift |
| GPS_HUB_RECENT_ROUNDS | MY RECENT ROUNDS | 最近のラウンド |
| GPS_HUB_TRUST_FORMAT | ● Trust {0}% | ● 信頼度 {0}% |
| GPS_HUB_HOLES_FORMAT | ({0} holes) | ({0}ホール) |
| GPS_HUB_BEST_TAG | BEST | ベスト |
| GPS_HUB_NAV_HOME | HOME | ホーム |
| GPS_HUB_NAV_ROUNDS | ROUNDS | ラウンド |
| GPS_HUB_NAV_GIFT | GIFT | Gift |
| GPS_HUB_NAV_PROFILE | PROFILE | プロフィール |

(JA how-it-works and tile copy is Ken's own from the mockup; the rest is authored here — Cesar may re-word in the admin after publish.) If the plan reports CONFLICTS, stop and report — no `--overwrite-dirty`.

### 7. Activating the banner (Architect + Cesar, after the build is on TestFlight)

Not Code's step; recorded so the task is complete end to end. In the admin **Banners** panel: `home_promo` → upload the GOLFIN·GPS strip art (the authoring sprite on `HomeScreen.prefab` → `PromoBanner` Image — Code names the sprite asset path in the report; EN and JA are the same image for now) → `link_url = golfin://gps` → active, no schedule. Verify `GET /api/v1/banners` returns it, then on the build: tap → hub. On a pre-spec build the banner shows and is non-tappable (expected).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] `BannerPolicyTests`: `golfin://gps` → allowed + `GpsHub`; `golfin://shop` refused; `GOLFIN://GPS` allowed; `https://golfin.io/x` allowed; `javascript:alert(1)` refused — quote the test names.
- [ ] Editor play mode with a **test banner injected** (`BannerService` test seam or a scripted `RemoteBannerSource` response for `home_promo` with `link_url: golfin://gps`): tapping the Home banner shows `GpsHub`; `GoBack` returns to Home with the banner still there. Quote the `[Banners] Routing…` log line.
- [ ] Hub screen renders with shared top bar (RP pill + gear), title `GOLFIN GPS`, shared bottom nav HIDDEN, hub nav bar visible — screenshot in `screenshots/`.
- [ ] Figma fidelity table reproduced with PASS/FAIL per row against the node renders in `reference/`; the two v2 panels are inactive, the rounds panel is bound.
- [ ] Hero shows `—` before `/user/detail` answers and live values after (Editor signed in as cesar.guarinoni@…: quote the values and the `[ApiClient] GET /api/v1/user/detail → 200` line).
- [ ] Recent rounds: with the test account's history (there are real `activities` rows from the PLAYLIFE app) the panel lists up to 3; with an account that has none it is hidden — quote both.
- [ ] All 23 keys present in `LocalizationText.csv` with EN and JA; importer PLAN → APPLY log quoted; `export_content.py --check` clean; `texts` published (version quoted); zero new hardcoded `.text` literals (grep quoted).
- [ ] `AuthGate` blocks `GpsHub` when signed out (Login shown instead) — quote the `[AuthGate] blocked GpsHub` line.
- [ ] Dashboard: `npm test` green incl. the three new `banner.test.ts` cases; `validateBannerLinkUrl("golfin://gps") === null` and the editor accepts it — screenshot of the editor with the route entered.
- [ ] Telemetry: one `gps_hub_open` row in prod `telemetry_events` from one Editor open (SQL quoted, then deleted like the gacha ones).
- [ ] **Carried over from `gps_trust_core`:** on the first iOS Simulator build of this spec, boot-time `Debug.Log(SystemInfo.deviceModel + " → " + new UnityClientPlatformProbe().Label())` prints `"… → ios-simulator"` — quote the raw string. If no simulator build was made, say so explicitly.
- [ ] EditMode suite count before/after; nothing pre-existing broken.
- [ ] No white-box placeholders; all `[SerializeField]` wired; Console clean of task-related errors.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/BannersRuntime/BannerPolicy.cs`, `BannerSlotBinder.cs` — internal route
- `Assets/Scripts/TournamentsRuntime/Tests/BannerPolicyTests.cs` (`BannerLinkAllowlistTests`) — new cases
- `Assets/Scripts/UI/ScreenManager.cs` — `ScreenId.GpsHub`, registration, bars rule, menu-music rule
- `Assets/Scripts/UI/PersistentUIManager.cs` — one `case` in `NavTitleKeyFor` (+ `HighlightScreen` tolerating a screen with no bottom-nav pillar, if it does not already)
- `Assets/Scripts/Social/Golfin.Social.asmdef`, `UserService.cs`, `UserDetailDto.cs` — NEW module
- `Assets/Scripts/Gps/ScoreHistoryService.cs` — NEW; `GpsDtos.cs` — `ActivityDto` fields if missing
- `Assets/Scripts/Net/Endpoints.cs` — two URLs appended to the GPS section
- `Assets/Prefabs/UI/Gps/GpsHubScreen.prefab`, `Assets/Scripts/UI/Gps/GpsHubScreenController.cs` — NEW
- `Assets/Art/UI/Gps/*.png` — 8 icon sprites exported from Figma
- `Assets/Localization/LocalizationText.csv` — 23 rows (+ importer run)
- `Tools/admin-dashboard/lib/banner.ts`, `lib/i18n.ts`, `banner.test.ts`, `app/(panels)/banners/banner-editor.tsx` (placeholder text)
- `Assets/Scenes/ShellScene.unity` — screen registration wiring
- `Docs/AI_CONTEXT.md` — at close-out

## Smoke evidence

Editor play-mode run with the injected banner (screen transition both ways), the hub screenshot vs the Figma render, the importer/publish log, the `BannerPolicyTests` + dashboard test output, the `/user/detail` and `/score/history` log lines. Human-in-the-loop: Cesar taps the real banner on the next TestFlight build once the admin row is active (§7).

## Out of scope (do NOT do these)

- Any GPS feature screen (check-in, score upload, profile, gifts, votes) — the tiles and hub nav stay inert; `gps_checkin_screen` is next.
- A "coming soon" modal/toast on the inert tiles.
- Gifts / votes data, `/social/*`, `/gifts/*`, `/vote/*` calls.
- New backgrounds/textures; the Android mock plugin; maps; the standalone shell.
- Changing the shared bottom nav, `PersistentUI.prefab` visuals, or the Home screen beyond the banner tap.
- Adding `GPS_ERR_*` rows (next spec, with the screen that shows them).
- Activating the prod banner row (Architect + Cesar, §7) and deploying the dashboard (Architect).
