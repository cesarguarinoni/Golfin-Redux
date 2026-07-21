---
title: GPS App → Unity (GOLFIN) Rebuild Spec
project: GOLFIN
author: Architect (Claude) → for Cesar / Claude Code
status: Draft v1
date: 2026-07-14
supersedes: GOLFIN/05_Merge/* (those assumed Flame; GOLFIN is Unity)
---

# GPS → Unity Rebuild — Master Build Spec

## 0. Decision & Scope

**Goal:** one Unity app that is the GOLFIN game **and** the full PLAYLIFE/GPS
feature set, with the GPS UX **replicated in the GOLFIN UI system** (not
copied pixel-for-pixel from Flutter).

**Core principle — the backend does not move.** The GPS app is a thin REST
client. All real logic (score recognition via Anthropic Vision, GPS Trust
scoring, points economy, gift distribution, badge checks, IAP verification)
lives in the **FastAPI backend + Supabase**. We keep both untouched. The
Unity work is therefore three things, in order of cost:

1. **UI rebuild** in the GOLFIN UI system (the bulk — ~30 screens).
2. **Native capability wiring** (GPS + mock detection, image picker, IAP, maps).
3. **A C# client/data layer** that mirrors `endpoints.dart` + auth (mechanical).

The Flutter `data`/`controller` logic transliterates to C# almost directly.
The Flutter widget tree does **not** port — it is rebuilt.

> API contract single source of truth = `lib/resources/endpoints.dart`
> (~60 endpoints, all `Authorization: Bearer <supabase jwt>`, responses
> wrapped `{ "data": ... }`).

---

## 1. Target Architecture (client layer inside Unity)

New C# subsystem, layered onto the existing GOLFIN conventions
(`ScreenManager`, `ModalController`, `.Instance` singletons, event-driven UI).

```
GOLFIN Unity app
├─ Game (existing: CharacterManager, Roster, Clubs, Bags, Shot controls …)
└─ Services (NEW — GPS/PLAYLIFE features)
   ├─ SupabaseAuthManager   ── Supabase Auth (JWT issue/refresh, OAuth)
   ├─ ApiClient             ── UnityWebRequest + Bearer + 401-refresh + retry
   ├─ <Feature>Service      ── one per feature, mirrors Flutter data-source/repo
   ├─ DTOs (plain C#)        ── mirror freezed models; parse {data:...} envelope
   └─ Native bridges         ── Location, MockDetection, ImagePicker, IAP, Map
        │
        └─ UI: GPS screens as ScreenManager states + ModalController overlays
```

### 1.1 Infrastructure components (build first — everything depends on these)

| New C# class | Mirrors (Flutter) | Responsibility | Notes |
|---|---|---|---|
| `ApiClient` (singleton) | `core/network/api_client.dart` + `api_interceptor.dart` | GET/POST via `UnityWebRequest`, attach Bearer, `{data}` unwrap, retry on 408/conn-fail | Return a `Result<T>` mirror; async via `UniTask` or coroutines |
| `SupabaseAuthManager` (singleton) | `main.dart` init + `supabase_flutter` | email/pw, Google/Apple OAuth, session persistence, **token refresh** | See §6 — biggest infra risk |
| `Endpoints` (static) | `resources/endpoints.dart` | URL builder for ~60 routes | 1:1 transliteration |
| `ApiResult<T>` / `ApiFailure` | `core/models/result.dart`, `core/errors/failure.dart` | typed success/failure | replaces freezed `Result<T>` |
| `AnalyticsBridge` | `utils/analytics_engine.dart` | Firebase Analytics events | Firebase Unity SDK exists |

### 1.2 How it maps onto GOLFIN conventions

- **Navigation:** GPS "routes" become **`ScreenManager` states**. The Flutter
  router has 38 routes; after dropping duplicates/dead screens (see §4) the
  real set is ~30. Guard logic in `router.dart` (public vs auth-gated,
  redirect-to-login when no session) becomes a **`ScreenManager` auth gate**
  reading `SupabaseAuthManager.IsLoggedIn`.
- **Dialogs/overlays** (report/block menu, GPS fallback dialog, purchase
  result) → **`ModalController` subclasses**. The Flutter `ReportBlockMenu`
  (`moderation.dart`) is the template: a static service + a UI trigger.
- **Services** are `.Instance` singletons like `CharacterManager` /
  `RewardPointsManager`. New namespaces suggested: `Golfin.Gps`,
  `Golfin.Social`, `Golfin.Economy`, `Golfin.Net`.
- **State/UI binding:** keep the existing **event-driven pattern** (C# `Action`
  delegates, subscribe in `OnEnable` / unsubscribe in `OnDisable`). Flutter
  `StateNotifier`s become services that fire events on data change.
- **Data storage:** server data is **runtime DTOs**, not CSV and not
  ScriptableObjects. CSV-first stays for *game* data (roster, clubs); PLAYLIFE
  data is fetched live and cached in memory (mirror of how the Flutter app
  holds it). `SharedPreferences` usage (block list cache, GPS fix log) →
  `PlayerPrefs` or a small JSON file.

---

## 2. ⚠️ Critical reconciliation: Points systems

The GPS app has a **dual-currency economy** (`activity_pts` / `gift_pts` /
`total_points`) computed **server-side** and read via `/points/*`. GOLFIN
already has a **`RewardPointsManager`** singleton, and the project note says
the *"Reward Points system shared with partner app remains."*

These are very likely **the same economy** (the GPS/PLAYLIFE backend probably
*is* the partner-app points source). Before any points UI is built, decide:

- **(Recommended) `RewardPointsManager` becomes a client of `/points/*`** —
  one ledger, server-authoritative, no divergence. GOLFIN's existing local
  reward logic is re-pointed at the backend balance.
- **Keep two ledgers** — only if game points and PLAYLIFE points are
  genuinely different currencies. Then define the exchange/sync rule.

This is the single biggest architectural fork and it blocks the points,
gifts, and IAP work. Flagging for you to confirm. **(NOTE: needs the actual
`RewardPointsManager.cs` to write the precise adapter.)**

---

## 3. Native capability plan

The GPS features that are "free" in Flutter need explicit Unity solutions.

| Capability | Flutter pkg | Unity approach | Effort | Risk |
|---|---|---|---|---|
| Location fix | `geolocator` | `Input.location` (Unity) for lat/lon/accuracy | Low | Low |
| **Mock-GPS detection** (M1) | `Position.isMocked` | **Native plugin required** — Android `Location.isFromMockProvider()` / `isMock` (API 31+); iOS n/a | Med-High | **High** — core anti-cheat |
| Simulator detection (M2) | `device_info_plus` | Unity `SystemInfo.deviceModel` / `Application.isEditor`; iOS sim detectable | Low | Low |
| Screenshot pick/capture | `image_picker` | Asset (e.g. NativeGallery) or small native plugin | Low | Low |
| IAP | `in_app_purchase` | **Unity IAP** (first-class; better than Flutter here) | Low-Med | Low |
| Map (venue/course) | `google_maps_flutter` + `flutter_map` | No first-class Unity map. Options: (a) WebView tile map, (b) paid map asset, (c) defer to list-only v1 | Med-High | **High** |
| Geohash (nearby venues) | `dart_geohash` | Port a small C# geohash encoder/neighbors (or NuGet) | Low | Low |
| Deep links (OAuth, referral `?ref=`) | GoRouter + Supabase | Unity deep-link (`Application.deepLinkActivated`) + URI scheme | Med | Med |

**Recommended defaults:** Unity IAP for purchases; native mock-detection plugin
for Android (this is non-negotiable for the Trust system — see §5); maps
list-only in v1, real map in v2.

---

## 4. Feature → Unity screen mapping

Port complexity: **P** = mostly logic transliteration, **U** = UI rebuild
heavy, **N** = native work gates it. `v1` = first shippable slice.

| Feature | Backend endpoints | Native | Complexity | Slice |
|---|---|---|---|---|
| **Auth** (email/pw, Google, Apple) | Supabase Auth | Deep link | P + N | **v1** |
| **Score recognition** ★ | `/recognition/analyze`, `/recognition/history` | image picker | P + N | **v1** |
| **GPS check-in / Trust** ★ | `/activity/checkin`, `/{id}/checkout`, `/activity/history`, `/venue/auto-register` | location + **mock detect** | P + N | **v1** |
| **Score submit** ★ | `/score/submit`, `/score/stats`, `/score/history` | (uses GPS attach) | P | **v1** |
| **Points / dual currency** ★ | `/points/balance`, `/history`, `/earn` | — | P | **v1** (after §2) |
| Venue / course search | `/venue/*`, `/courses/places` | geohash (+map v2) | U | v1 (list) / v2 (map) |
| Badges | `/badges/*` | — | U | v1 |
| Ranking (5-axis) | `/user/ranking/top` | — | U | v1 |
| Profile + edit | `/user/detail`, `/user/update`, `/user/{id}/profile` | — | U | v1 |
| Gifts / item shop / inventory | `/gifts/*` | — | U | v2 |
| **Avatar growth / equip** | `/gifts/inventory`, `avatar_xp/level` | — | **U (biggest link)** | v2 — connect to GOLFIN character/cosmetic assets |
| Social feed / follow / discover | `/social/*` | — | U | v2 |
| IAP (pts store) | `/iap/catalog`, `/iap/verify-purchase`, `/iap/my-purchases` | **Unity IAP** | P + N | v2 (**implement Google verify**) |
| Moderation (report/block) | `/moderation/*` | — | P (Modal) | v2 |
| Referrals | `/referrals/*` | deep link | P | v2 |
| VOTE | `/vote/*`, `/vote-gen/active` | — | U | v3 (reframe as in-game event) |
| Tournaments | `/tournaments/*` | — | U | v3 (merge w/ game comps) |
| Settings / terms / privacy | static + settings APIs | — | U | v1 (terms/privacy), v2 (rest) |

**Drop entirely** (dead/dup/dormant in Flutter, per `07_Feature_Mapping.md`):
old `/login` `/signup`, `vote_detail` v1, `/other-profile`, `home_screen`
+ `golf_demo` (GOLFIN home replaces both), memberships/NFT, LP `signups`,
`redeem` placeholder, hardcoded Supabase-key fallback.

---

## 5. GPS Trust subsystem (the differentiator — port faithfully)

This is the anti-cheat core and it is **pure logic** — it ports to C# nearly
line-for-line. Three Flutter files become three C# classes:

| Flutter | → C# | What it does |
|---|---|---|
| `gps_session_tracker.dart` | `GpsSessionTracker` | records fixes to local store, throttles (5min/100m), prunes (12h/100 fixes), counts distinct fixes (10min gap) → `gps_check_count` + start/end coords. Haversine included. |
| `gps_trust_signals.dart` | `GpsTrustSignals` | `isMock` (native) + platform label → `gps_is_mock`, `client_platform` |
| `gps_score_attachment.dart` | `GpsScoreAttachment` | fetch position → `/venue/auto-register` → build the `/score/submit` payload fields |

**Payload the backend expects** (must be byte-identical or Trust breaks):
`gps_verified, latitude, longitude, venue_id, gps_is_mock, client_platform,
gps_check_count, gps_start_lat/lon, gps_end_lat/lon`. Backend awards Trust +20
when `gps_verified && gps_check_count >= 3`.

**The one hard dependency:** `gps_is_mock` requires the Android native
mock-detection plugin (§3). Without it, `isMock` is always false and the
Trust signal is defeated. Build this plugin early; it gates the whole
"verified round" value prop.

Storage: the fix log currently uses `SharedPreferences` JSON under key
`gps_session_fixes_v1` — replicate the exact schema (`lat,lon,t`) in
`PlayerPrefs`/file so behavior (and any in-flight data) matches.

---

## 6. Auth & session (Supabase in Unity)

Flutter gets this from `supabase_flutter` + GoRouter's `refreshListenable`.
In Unity:

- **Library:** `supabase-csharp` (community) or hand-rolled calls to the
  Supabase Auth REST endpoints (`/auth/v1/token`, `/authorize`). Hand-rolled
  is often cleaner for Unity than fighting the SDK's dependencies.
- **Email/pw:** direct REST → store `access_token` + `refresh_token`.
- **Google / Apple OAuth:** open system browser / native sheet → **deep-link
  callback** (`Application.deepLinkActivated`) captures the redirect. The
  Flutter redirect is `https://playlife-app.web.app/`; add a Unity URI scheme
  and register it as an allowed redirect in Supabase.
- **Refresh:** `ApiClient` intercepts **401 → refresh → retry once**. (Note:
  the Flutter app's own eval calls out that 401-refresh was *missing* — build
  it correctly here from day one.)
- **Guard:** `ScreenManager` refuses gated states until `IsLoggedIn`, mirroring
  `router.dart`'s `_publicRoutes` / redirect.

---

## 7. Localization

The Flutter UI strings are **hardcoded Japanese** (e.g. the report dialog and
GPS error copy in `moderation.dart` / `current_location_notifier.dart`). GOLFIN
already has a **JP + EN** system (`nameKey`/`bioKey` pattern). All rebuilt GPS
screens must route text through GOLFIN localization with **both JP and EN**
keys — do not carry the hardcoded JP strings across. Budget a pass to author EN
copy for GPS screens (the Flutter app never had it).

---

## 8. Phased build plan & effort (solo)

Effort assumes backend/Supabase reused as-is. Ranges are working weeks.

| Phase | Contents | Effort | Gate |
|---|---|---|---|
| **P0 — Infra** | `ApiClient` (+401 refresh, retry), `Endpoints`, `ApiResult`, DTO base, Analytics bridge | 1–1.5 wk | — |
| **P1 — Auth** | `SupabaseAuthManager`, OAuth deep-link, session persist, ScreenManager gate | 1.5–2 wk | deep-link + Supabase redirect |
| **P2 — Native** | Location + **mock-detect plugin**, image picker, geohash | 1.5–2.5 wk | Android mock plugin |
| **P3 — Core value (v1)** ★ | score recognition, GPS check-in/Trust, score submit, points (after §2), badges, ranking, profile, venue list, terms/privacy | 3–5 wk | §2 points decision |
| **P4 — Economy/social (v2)** | gifts/item shop/inventory, **avatar equip ↔ GOLFIN assets**, social feed/follow/discover, IAP (+Google verify), moderation, referrals, settings, venue map | 4–6 wk | avatar asset mapping |
| **P5 — Extended (v3)** | VOTE, tournaments (reframed into game) | 2–3 wk | design reframe |
| **P6 — Hardening** | store submission (both platforms), Trust QA, load/soak, EN copy | 1.5–2 wk | — |

**Totals:**
- **v1 (core, shippable):** P0–P3 ≈ **7–11 weeks**
- **Full parity:** P0–P6 ≈ **14.5–22 weeks** (~3.5–5 months)

The estimate is dominated by UI rebuild (P3/P4) and the two native risks
(mock detection, maps). The C# client/data layer is a small, predictable
fraction.

---

## 9. Risks & open decisions

1. **Points ledger fork (§2)** — must resolve before P3. *Decision needed.*
2. **Mock-GPS native plugin** — gates the Trust value prop; start in P2.
3. **Maps** — accept list-only v1, or fund a map solution? *Decision needed.*
4. **Avatar mapping** — GPS `user_inventory.is_equipped` × `gift_items.category`
   (9 slots) ↔ GOLFIN character/cosmetic system. This is the richest
   integration and needs a design session. *Decision needed.*
5. **OAuth redirect** — needs a Unity URI scheme registered in Supabase; the
   existing web redirect can stay as a fallback.
6. **Google IAP verification** is unimplemented in the backend today — must be
   finished before Android IAP ships (backend task, not Unity).
7. **Backend hardening carried over** — the app's own eval flags non-atomic
   points and no env separation. Not blockers for the port, but the shared
   backend should get the `points_atomic` / `score_submit_atomic` migrations
   (already present in `backend/migrations/`) applied.

---

## 10. Integration points needing the real Unity repo

To turn this master spec into per-feature specs that reference **actual method
names and file paths** (per your workflow), I need the GOLFIN Unity side:

- `AI_CONTEXT.md`, `ARCHITECTURE_AUDIT.md`, `Tellcode.md`, `Rules.md`, `Tasks.md`
- `ScreenManager`, `ModalController`, `RewardPointsManager`, `CharacterManager`,
  `AudioManager` source (for exact hooks and the §2 points adapter)
- The localization system entry points (key registration)
- The character/cosmetic asset model (for §9.4 avatar mapping)

Where those are unknown above, items are marked **(NOTE …)** rather than
guessed.

---

## 11. Next steps

1. **You decide:** §2 points ledger, §3 maps scope, §9.4 avatar mapping owner.
2. **Connect the GOLFIN Unity repo** (or upload `AI_CONTEXT.md`) so I can write
   the P0–P1 per-feature specs (`ApiClient`, `SupabaseAuthManager`) against real
   symbols — these are the unblockers for Claude Code.
3. I draft per-feature implementation specs in dependency order:
   `Net` (ApiClient) → `Auth` → `Gps/Trust` → `ScoreRecognition` → `Points`.
