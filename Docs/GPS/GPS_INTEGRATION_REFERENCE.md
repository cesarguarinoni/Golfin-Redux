# GPS App (PLAYLIFE) — Technical Integration Reference

> **Purpose.** A cold-start reference for future GOLFIN⇄GPS integration tasks. Everything here was gathered from the GPS repo (`C:\Users\cesar\GPS\playlife-main`) so a later session (or Claude Code) can act without re-reading the whole codebase.
> **Provenance.** ✅ = read directly from source this session (2026-07-14). 📄 = from the repo's own analysis docs (`GOLFIN/05_Merge/*.md`), reconstructed from migrations/specs — verify column-exact against SQL if a change depends on it.
> **Companion docs (in this project):** `GPS_UNITY_PORT_SPEC.md` (build plan) · `GOLFIN_Backend_Hosting_Options.md` (hosting decision).

---

## 1. System topology

```
Unity/Flutter client
   │  (1) login  ──────────────► Supabase Auth  (https://wmszyghwwkaptgqdunel.supabase.co)
   │                                   └─ issues JWT (access + refresh)
   │  (2) REST + "Authorization: Bearer <JWT>"
   ▼
FastAPI  "playlife-api"  (https://playlife-api.fly.dev)   ── Fly.io, region nrt (Tokyo)
   │  service_role key (bypasses RLS)
   ▼
Supabase Postgres  (same project)
   plus outbound: Anthropic Vision · Google Places · Apple verifyReceipt · Google Play API
Client also → Firebase Analytics (analytics only)
```

- **Repo name:** `playlife` · **Display name:** GOLFIN · **Version:** `0.8.0+1` ✅ (`pubspec.yaml`)
- **One backend, one Supabase project** serve everything. GOLFIN reuses both as-is — no second backend, no data migration.

---

## 2. Backend — FastAPI on Fly.io ✅

| Item | Value | Source |
|---|---|---|
| Public URL | `https://playlife-api.fly.dev` | `base_path.dart`, `fly.toml` |
| Local URL | `http://localhost:8000` | `base_path.dart` |
| Fly app name | `playlife-api` | `fly.toml` |
| Region | `nrt` (Tokyo) | `fly.toml` |
| Internal port | `8000`, `force_https = true` | `fly.toml` |
| Scaling | `auto_stop_machines = "stop"`, `auto_start_machines = true`, `min_machines_running = 0` → **scale-to-zero** (cold starts) | `fly.toml` |
| Build | `Dockerfile` | `fly.toml` |
| App title / version | "PLAYLIFE API" / `0.1.0` | `main.py` |
| Health check | `GET /health` → `{status: ok, version: 0.1.0}` | `main.py` |
| CORS | `allow_origins=["*"]`, `allow_credentials=True`, methods `*`, headers `*` ⚠️ | `main.py` |
| Auth | `auth.get_current_user` verifies Supabase JWT (service client) | 📄 `05_Database.md` |

**Runtime deps** ✅ (`requirements.txt`): `fastapi==0.115.0`, `uvicorn[standard]==0.30.0`, `anthropic==0.39.0`, `supabase==2.10.0`, `pydantic==2.9.0`, `pydantic-settings==2.5.0`, `httpx==0.27.0`, `PyJWT[crypto]==2.9.0`, `python-multipart==0.0.9`, `python-dotenv==1.0.1`.

**Routers (20)** ✅ `main.py`, all under `/api/v1/<prefix>`:
`recognition, activity, vote (voting), points, user, venue, gifts, social (followers), badges, score, dashboard, courses, auth-helper, vote-gen (vote_generator), moderation, referrals, memberships, tournaments, iap, signups`.

**Backend config settings** ✅ (`config.py`, pydantic `BaseSettings`, `.env`):
`anthropic_api_key`, `supabase_url`, `supabase_service_key`, `google_places_api_key`, `admin_preload_key` (default `CHANGE_ME_VIA_ENV_BEFORE_USE`), `apple_shared_secret`, `google_play_service_account_json`, `android_package_name` (default `com.wonderwall.playlife`).

---

## 3. Auth & identity model ✅ / 📄

- **Login is direct to Supabase Auth** (client → `…supabase.co`), which issues a **JWT** (access + refresh). Methods: Email/Password, Google OAuth, Apple OAuth. OAuth redirect: `https://playlife-app.web.app/` (Firebase Hosting). 📄 `01_GPS_Features.md`, `05_Database.md`
- **Every API call carries `Authorization: Bearer <JWT>`**, auto-attached by the Dio interceptor. Backend validates the JWT per request. ✅ `gps_score_attachment.dart` uses `ApiClient`; 📄 interceptor behavior.
- **Identification is user-level, not app-level.** There is **no API key, no client secret, no bundle-ID/app-attestation check**, and **CORS is `*`**. The server cannot distinguish the real app binary from `curl` with the same valid token. ✅ `main.py`
- **Supabase project:** ref `wmszyghwwkaptgqdunel`, URL `https://wmszyghwwkaptgqdunel.supabase.co`. Backend uses the **service_role key** (bypasses RLS, `create_client` per router). Client uses the **anon/public key** (hardcoded fallback in `main.dart` ⚠️). RLS enabled on all tables. 📄 `05_Database.md`
- **Known gaps:** some endpoints lack an auth guard; client has **no 401→refresh** flow. Add app attestation (Play Integrity / App Attest), a gateway key, and tighten CORS if true "only-my-app" identity is ever needed. 📄 `12_Evaluation.md`

**Unity port implication:** the Unity client authenticates to Supabase, then sends the JWT as a Bearer header — identical model, nothing app-specific to replicate.

---

## 4. API contract ✅

**Base-URL resolution** (`base_path.dart` → `_getBaseUrl()`):
1. `--dart-define=API_BASE_URL=…` wins if set.
2. else switch on `AppConfig.appServerMode` (= `--dart-define=ENV`, default `dev`): `dev`/`stage`/`prod` **all → `https://playlife-api.fly.dev`** (no env separation ⚠️); `local` → `http://localhost:8000`.
- `AppConfig.timeOut = 120s`, `logHttp = true` (`config.dart`).

**Response envelope:** most routers wrap payloads as `{ "data": … }`. 📄 `04_API.md`
**Client networking:** Dio singleton `ApiClient` + `DioInterceptor` — auto Bearer, retry on 408 / connection failure. 📄 (exact retry/timeout live in `core/network/api_client.dart` + `api_interceptor.dart` — **not read this session**; open them when porting the client layer.)

**Endpoint list** ✅ (`endpoints.dart`, the single source of truth):

```
User        GET  /user/detail · /user/search?q= · /user/discover · /user/{id}/profile
            POST /user/update · /user/avatar
            GET  /user/ranking/top?filter=&limit=      (5-axis leaderboard)
Recognition POST /recognition/analyze   GET /recognition/history?skip=&limit=
Activity    POST /activity/checkin · /activity/{id}/checkout · /activity/{id}/cancel
            GET  /activity/history?skip=&limit=
Venue       GET  /venue/list?language_code= · /venue/{id}?language_code=
            GET  /venue/nearby?prefixes=&language_code=   (geohash prefixes)
            POST /venue/search · /venue/auto-register
Courses     GET  /courses/places?lat=&lon=&radius_m=&language_code=   (Google Places)
Voting      POST /vote/create · /vote/{id}/cast   GET /vote/list?skip=&limit= · /vote/{id}/result
            GET  /vote-gen/active
Points      GET  /points/balance · /points/history?skip=&limit=&currency=
            POST /points/earn?action= · /points/redeem   (redeem = placeholder)
Gifts       GET  /gifts/items · /gifts/inventory · /gifts/sent · /gifts/received
            POST /gifts/send-pts · /gifts/send · /gifts/purchase · /gifts/inventory/{id}/equip
Social      POST /social/follow · /social/unfollow
            GET  /social/check/{id} · /social/{id}/followers · /social/{id}/following
            GET  /social/feed · /social/feed/global
Score       POST /score/submit   GET /score/stats · /score/history?skip=&limit=
Badges      GET  /badges/definitions · /badges/mine · /badges/user/{id} · /badges/progress
            POST /badges/check
Dashboard   GET  /dashboard/home
Moderation  POST /moderation/report · /moderation/block · /moderation/unblock
            GET  /moderation/blocks
Referrals   POST /referrals/generate-link · /referrals/claim   GET /referrals/my-stats
IAP         GET  /iap/catalog?platform=apple|google · /iap/my-purchases
            POST /iap/verify-purchase
Tournaments GET  /tournaments/active · /tournaments/{id} · /tournaments/{id}/ranking · /tournaments/{id}/my-entry
            POST /tournaments/{id}/enter
Memberships POST /memberships/link-by-email   GET /memberships/my-status   (dormant)
Auth helper POST /auth-helper/auto-confirm
Health      GET  /health
```
(All prefixed `https://playlife-api.fly.dev/api/v1`.)

---

## 5. Database — Supabase Postgres 📄 (`05_Database.md`, from migrations)

RLS on all tables (reads mostly public, writes constrained by `auth.uid()`). Backend bypasses RLS with service_role.

| Table | Key columns / notes |
|---|---|
| `profiles` | id (=auth.users), display_name, avatar_url, bio, handicap, best_score, avg_score, trust_level, **total_points, activity_pts, gift_pts**, avatar_level, avatar_xp, followers_count, following_count, badges_count, activities_count, sport_types[], invite_code, invited_by_code. Triggers: `handle_new_user`, `update_profile_stats`. |
| `venues` | id serial, name, sport_type, lat/lon, NE/SW bbox, geohash, rating, phone, **gps_radius_m**, place_id, source. |
| `activities` ★ | user_id, venue_id, venue_name, sport_type, check_in/out_at, duration, trust_level, **gps_verified, gps_check_count, gps_start/end_lat/lon, gps_is_mock, client_ip, client_platform**, social_verified, screenshot_data jsonb, points, status, visibility(public/friends/private). |
| `recognition_results` | id uuid, user_id, image_url, sport_type, extracted_data jsonb, confidence, raw_response. |
| `points_transactions` | id uuid, user_id, type, amount, **currency(activity/gift)**, description, related_activity/vote/gift/badge_id. |
| `gift_items` | name, category, tier(basic/premium), price_activity_pts, price_gift_pts, price_iap_usd, rarity, is_limited. |
| `gifts` | sender_id, receiver_id, item_id, payment_type(iap/activity_pts/sponsor), iap_amount_usd, platform_fee/playlife_fee/receiver_amount, gift_pts_awarded, status. Trigger `add_gift_pts_to_receiver`. |
| `user_inventory` | user_id, item_id, source, gift_id, gifted_by, **is_equipped**. |
| `badge_definitions` | id text, name, category(golf/social/trust/special), rarity, target_pct. 24 seeded. |
| `user_badges` | user_id, badge_id. Trigger `update_badges_count`. |
| `followers` | follower_id, following_id. Trigger `update_follower_counts`. |
| `feed_items` | user_id, feed_type, title, body, related_*_id, metadata jsonb. |
| `votes` / `vote_options` / `user_votes` | creator_id, question, vote_type, status, expires_at, sponsor_pool; UNIQUE(user_id, vote_id). |
| `reports` / `user_blocks` | moderation. |
| `iap_products` / `iap_purchases` | purchases keyed by **transaction_id UNIQUE (idempotent)**. |
| `memberships` / `tournaments` / `tournament_entries` | tournaments: tier open/sponsored/champion/exclusive, prize_pool_pts. |
| `referrals` · `pre_launch_signups` | invite tracking · LP signups. |

**Migrations live in TWO places** ⚠️ (unify when integrating):
- `supabase/migrations/` — `20260404…_initial_schema`, `20260409…_dual_currency_gifts_badges_followers`, `20260410…_privacy_and_stats`.
- `backend/migrations/` — moderation, gps_trust, iap, memberships_tournaments, referrals, venue_autoreg, signups, **`2026_06_29_points_atomic`**, **`2026_07_04_score_submit_atomic`** (fix the old points race), **`2026_07_06_seed_osm_golf_japan`** (~221 KB Japan golf-course seed), and `ALL_MIGRATIONS_2026_04_26.sql` (one-shot).

---

## 6. GPS Trust subsystem ✅ — the differentiator, port faithfully

Three pure-logic Dart files (transliterate to C# almost 1:1):

**`gps_session_tracker.dart`** — local fix log + anti-cheat counting.
- Storage: `SharedPreferences` key **`gps_session_fixes_v1`**, JSON array of `{lat, lon, t}` (t = epoch ms).
- Record throttle: skip if `<5 min` **and** `<100 m` since last fix. Retention: 12 h / max 100 fixes.
- `sessionNear(lat,lon)`: fixes within **5000 m** and **8 h** window; counts distinct fixes spaced **≥10 min** apart → `gps_check_count`, plus start/end coords. Empty → `checkCount:1`.
- Includes a haversine helper (r=6371000 m).

**`gps_trust_signals.dart`** — `isMock` = `Position.isMocked`; `platform` = `ios` / `ios-simulator` (via `device_info_plus` `isPhysicalDevice`) / `android`. → `{gps_is_mock, client_platform}`.

**`gps_score_attachment.dart`** — builds the `/score/submit` GPS fields:
1. get position → collect trust signals → `recordFix` + `sessionNear`
2. `POST /venue/auto-register {latitude, longitude}` → `{venue_id, name, distance_m}`
3. `toJson()` merges: `gps_verified` (= position≠null **and** venue_id≠null), `latitude`, `longitude`, `venue_id`, `gps_is_mock`, `client_platform`, `gps_check_count`, `gps_start_lat/lon`, `gps_end_lat/lon`.

**Backend rule:** `_verify_gps` needs coords **+** venue_id to judge distance; **Trust +20** when `gps_verified && gps_check_count ≥ 3` (K4). 📄
**Native dependency (Unity):** `Position.isMocked` has **no Unity equivalent** — needs an Android native plugin (`isFromMockProvider`/`isMock`). Without it the mock signal is always false. Build early.

**`current_location_notifier.dart`** — does **not** fetch on startup (J2); only on explicit user action. `getCurrentPosition(high, 10s)`. On new location: geohash via `dart_geohash`; when the **4-char geohash prefix changes**, fetch nearby venues (`neighbors + self` → `/venue/nearby?prefixes=`). Failure reasons enum: `serviceDisabled, permissionDenied, permissionDeniedForever, timeout, unknown` (user messages are hardcoded JP ⚠️ — route through GOLFIN JP/EN localization).

---

## 7. Points economy 📄

- **Dual currency:** `activity_pts` (earned by play) · `gift_pts` (received via gifts) · `total_points`. Server-authoritative.
- **Earn actions & amounts** (`/points/earn?action=`): `screenshot:50, gps_checkin:30, vote_cast:10, vote_hit:30, daily_login:5, game_play:10`.
- **Avatar:** level-up at `500 XP × level`.
- **Gift revenue split:** Platform 30% / PLAYLIFE 20% / Golfer 50% (recorded on `gifts`).
- **Rarity mismatch to resolve** ⚠️: GPS badges use `common/rare/epic/legend`; the GOLFIN game uses 6 tiers (`Common/Uncommon/Rare/Mythic/Legendary/Supreme`). Map or reconcile during integration.
- **Reconciliation decision (locked):** GOLFIN's `RewardPointsManager` becomes a client of `/points/*` — one shared ledger, not a second currency.

---

## 8. IAP ✅ (`iap_service.dart`)

- Plugin `in_app_purchase`; `platform` = `apple` (iOS) / `google` (Android).
- Flow: `GET /iap/catalog?platform=` → `queryProductDetails` → `buyConsumable(autoConsume:true)` → `purchaseStream` → verify → `completePurchase`.
- Verify: `POST /iap/verify-purchase` with `{platform, product_id, transaction_id, receipt_data (iOS) | purchase_token (Android), sandbox: !kReleaseMode}`. **Backend is authoritative** — pts credited only on success.
- `iap_purchases.transaction_id` UNIQUE → idempotent.
- ⚠️ **Google verification is unimplemented** (`not_implemented_yet`) — finish before Android IAP ships. Unity note: **Unity IAP** replaces the plugin, but the `/iap/verify-purchase` backend contract stays.
- Android package for Play validation: `com.wonderwall.playlife`.

---

## 9. Client app structure ✅ / 📄

- **Stack:** Flutter SDK `^3.11.4`; Riverpod (`flutter_riverpod` 2.5) + `go_router` 14.6 + `dio` 5.4 + `supabase_flutter` 2.8.4 + `firebase_core/analytics`. Maps: `google_maps_flutter` 2.10 + `flutter_map` 8.2 + `latlong2` + `dart_geohash`. `freezed`/`json_serializable`, `in_app_purchase` 3.2, `image_picker` 1.1, `device_info_plus`, `package_info_plus`, `shared_preferences`, `local_auth`, `flame` 1.36 (putt mini-game), `webview_flutter`, `flutter_dotenv`. ✅ `pubspec.yaml`
- **Architecture:** feature-first + clean arch — `features/<name>/data(model/data_source/repository)` + `presentation(controller/widgets)`; `Result<T>` (freezed) + `Failure`; `core/network` Dio client; `utils` logger + `AnalyticsEngine`. Brand color `#2DB87A`. 📄
- **Routing:** 38 `GoRoute`s, `initialLocation: /splash`. Auth guard ✅ (`router.dart`): public = `{/splash, /golfin-login, /golfin-signup, /login, /signup, /tutorial, /terms, /privacy}`; unauthenticated on a guarded route → `/golfin-login`; authenticated on a login/signup route → `/`. Refreshes on Supabase `onAuthStateChange`.
- **Core routes** (★ = core value): `/screenshot`★, `/screenshot/result`★, `/activity`★ (GPS check-in), `/post`★ (score submit) + `/post/complete`, `/points`★, `/venue`+`/venue/:id`, `/badges`, `/ranking`, `/profile`(+`/edit`,`/:userId`), `/gifts`+`/gift-history`+`/item-shop`, `/avatar`, `/feed`+`/discover`+`/followers`, `/voting`+`/vote-*`, `/settings`. Full route↔screen↔port table: `03_Screens.md` and `GPS_UNITY_PORT_SPEC.md §4`.
- **Duplicate/dead screens to drop** 📄: old `/login` `/signup`, `vote_detail` v1, `/other-profile`, `home_screen`/`golf_demo_screen`, memberships/NFT, `signups`, `redeem` placeholder.

---

## 10. External services & env vars ✅ / 📄

| Service | Used for | Key/where |
|---|---|---|
| Supabase Auth + Postgres | login/JWT + all data | client anon key; backend service_role |
| Anthropic Vision | score image recognition | backend `ANTHROPIC_API_KEY` |
| Google Places | course search, venue auto-register | backend `GOOGLE_PLACES_API_KEY` |
| Apple verifyReceipt | iOS IAP verify | backend `APPLE_SHARED_SECRET` |
| Google Play Developer API | Android IAP verify (unimpl.) | backend `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` |
| Firebase Analytics | analytics only | client (`firebase_options.dart`) |
| Firebase Hosting | OAuth redirect `playlife-app.web.app` | — |

**Client env** (`.env` / dart-define): `SUPABASE_URL`, `SUPABASE_ANON_KEY` (hardcoded fallback in `main.dart` ⚠️), `ENV` (default `dev`), `API_BASE_URL` (optional override).
**Backend env** (`.env`): `ANTHROPIC_API_KEY`, `SUPABASE_URL`, `SUPABASE_SERVICE_KEY`, `GOOGLE_PLACES_API_KEY`, `ADMIN_PRELOAD_KEY`, `APPLE_SHARED_SECRET`, `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`, `ANDROID_PACKAGE_NAME` (default `com.wonderwall.playlife`).

---

## 11. Known tech debt / risks 📄 (`12_Evaluation.md`, score 78/100)

1. **No environment separation** — dev/stage/prod all hit `playlife-api.fly.dev`.
2. **Points atomicity** — original read-modify-write race; **partially fixed** by `points_atomic` + `score_submit_atomic` migrations (confirm applied in prod).
3. **Auth gaps** — some endpoints unguarded; client has no 401→refresh.
4. **CORS `*`** + no app attestation (see §3).
5. **Layering** — ~6 screens bypass repository and call `ApiClient` directly.
6. **Secrets** — Supabase URL/anon-key fallback hardcoded in `main.dart`.
7. **Google IAP verify unimplemented.**
8. **Two migration systems** (`supabase/` vs `backend/`).
9. **Weak DI/tests** — `ApiClient` singleton, thin `test/`.

---

## 12. Key source-file map (device: `C:\Users\cesar\GPS\playlife-main\`)

| Path | Contains |
|---|---|
| `lib/resources/endpoints.dart` | **API contract — single source of truth** |
| `lib/resources/base_path.dart` | base-URL resolution (ENV/override) |
| `lib/core/config/config.dart` | `appServerMode`, timeout, logHttp |
| `lib/core/network/api_client.dart`, `api_interceptor.dart` | Dio client, Bearer, retry (**not read yet** — open when porting client) |
| `lib/common/presentation/controller/gps_session_tracker.dart` | trust fix log + counting |
| `…/gps_trust_signals.dart` | mock/platform signals |
| `…/gps_score_attachment.dart` | `/score/submit` GPS payload builder |
| `…/current_location_notifier.dart` | location fetch + nearby-venue trigger |
| `lib/features/iap/iap_service.dart` | IAP flow |
| `lib/features/moderation/moderation.dart` | report/block service + UI (static-service + widget pattern) |
| `lib/core/router.dart` | 38 routes + auth guard |
| `backend/main.py` | routers + CORS |
| `backend/config.py` | settings/env |
| `backend/fly.toml` | hosting |
| `backend/routers/*.py` | endpoint implementations (not all read) |
| `backend/migrations/*.sql`, `supabase/migrations/*.sql` | schema |
| `GOLFIN/05_Merge/*.md` | prior analysis: features, screens, API, DB, feature-mapping, evaluation, architecture |

---

*Verified from source 2026-07-14. Update this file if the backend URL, Supabase project, endpoint set, or GPS-trust constants change.*
