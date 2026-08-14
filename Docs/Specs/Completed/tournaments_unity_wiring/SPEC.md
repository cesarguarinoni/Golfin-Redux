# SPEC — tournaments_unity_wiring (the game reads the schedule from the server, art and all)

**Status:** SPEC_READY
**Author:** Architect (Cowork session), 2026-08-14, from Cesar: *"Tournaments names/images are not necessarily tied to a country club. Can be brands as well. Wire Unity."*
**Parent epic:** `Docs/Specs/Active/tournaments_server_side/SPEC.md` — this is its **Phase 3 + 3b, merged** (see §1.1 for why they merged).
**Depends on:** Phase 1 (schema, APPLIED to prod, playlife `02fb177`) and Phase 2 (dashboard panel, GolfinRedux `0e5c509d0`) — both shipped.
**Repos touched:** `GolfinRedux` (client) **and** `playlife` (one new endpoint + two filter fixes).

---

## 1. Why

The schedule and the prize ladders now live in Postgres and have an admin panel, but **the game still reads `Assets/Resources/Data/tournaments.csv`**. Every dashboard edit is invisible to players until someone exports a CSV, commits it and ships a build. This phase closes that: the client fetches the schedule, and a tournament created in the dashboard — *including its artwork and its name* — reaches players on the next launch with no release.

### 1.1 Decision of record (Cesar, 2026-08-14): a tournament's identity is not its venue

> *"Tournaments names/images are not necessarily tied to a country club. Can be brands as well."*

The original Phase-3 plan resolved card art from `course_id` (`Resources/TournamentImages/{course_id}`) and deferred remote art to a later "3b". That plan quietly assumed every tournament is named and pictured after the club it is played at. It is not: a tournament can be **brand-led** — *"PUMA Summer Slam"* played at Lomond, with PUMA key art on the card.

Three consequences, and they are the spine of this spec:

1. **Remote art is not a later phase.** Bundled course photos cannot express a brand, so §5's download path ships **in this phase**. Course art demotes to the offline default beneath it.
2. **The display name must survive with no localization entry.** Today the card name is `LocalizationManager.Get(def.NameKey)` with **no fallback** (`TournamentSelectionScreenController.cs:153`), and localization keys ship inside the build. A brand tournament created in the dashboard has no key, so it would render the raw key string. The server therefore sends `title` alongside `name_key`, and the client falls back to it (§4.3). **Without this, "add a tournament without a new build" is false for its name.**
3. **`course_id` keeps exactly one job:** which venue is played, and the venue subtitle line. It never determines the name, and it determines art only as a last-resort default.

---

## 2. What exists (verified 2026-08-14, both repos + live DB)

### 2.1 Client — the load path

| Path | Note |
|---|---|
| `Assets/Scripts/TournamentsRuntime/TournamentService.cs:145` | `public static ITournamentBackend Compose()` — **the single seam.** Builds `TournamentCsvLoader`, calls `LoadTournaments()` / `LoadPrizeTables()` / `LoadBotFields()` (`:148-151`), then constructs `LocalTournamentBackend` (`:164-174`). |
| `…/TournamentService.cs:69` | `Awake()` → `:80-81` loader + prize tables → `:83 Backend = Compose()`. Lives on `ShellScene` (scene 0), `DontDestroyOnLoad` at `:78`. **Tournament data loads at boot, not on screen entry.** |
| `Assets/Scripts/Tournaments/TournamentCsvLoader.cs:42/119/183` | `LoadTournaments()` → `IReadOnlyList<TournamentDefinition>`; `LoadPrizeTables()` → `IReadOnlyDictionary<string, PrizeTable>`; `LoadBotFields()` → `IReadOnlyDictionary<string, BotFieldConfig>`. |
| `…/TournamentCsvLoader.cs:250` | `public static IReadOnlyList<string> ExpandHoleSet(string raw)` — reuse verbatim on server `hole_set`. |
| `…/TournamentCsvLoader.cs:341` | `public static bool CheckReferentialIntegrity(tournaments, prizeTables, botFields)`. |
| `…/TournamentCsvLoader.cs:435` | `private static DateTime ParseUtc(...)` — `AdjustToUniversal \| AssumeUniversal`. Match this discipline for server timestamps. |
| `Assets/Scripts/Tournaments/TournamentDefinition.cs:16-92` | 12 get-only members: `Id, NameKey, ClubId, HoleSet, StartUtc, EndUtc, ResolveDelayMinutes, EntryFeeRP, PrizeTableId, BotFieldId, SponsorKey, LeagueKey`. Positional ctor at `:80`. **No title field, no image field.** |
| `Assets/Scripts/Tournaments/PrizeBand.cs:22/53` | `PrizeBand(int rankFrom, int rankTo, long rpReward, string? itemRewardId = null)`; `PrizeTable(string prizeTableId, IReadOnlyList<PrizeBand> bands)`. |
| `Assets/Scripts/Tournaments/LocalTournamentBackend.cs:78-88` | Definitions/prize tables/bot fields are **injected**, not loaded. `:460 DeriveState(def, now)`, `EndingThreshold = 1h` (`:35`), `:491 IsResolved`. |
| `Assets/Scripts/Tournaments/TournamentEnums.cs:13` | `TournamentState { Upcoming, Open, Playing, Ending, Closed, Ended }` — unchanged by this spec. |
| `Assets/Scripts/UI/Tournaments/TournamentSelectionScreenController.cs:323-336` | `ResolveSprite(string tournamentId, int csvIndex)` — `_courseImageMap` (`:35`) then **positional** `_courseImages[csvIndex]` (`:31`). |
| `…:153 / :156-159 / :167-169` | name = `LocalizationManager.Get(def.NameKey)` (**no fallback**); venue = `"tourn.venue." + def.ClubId` with a `ClubId · N Holes` fallback; sponsor = `SponsorKey.ToUpperInvariant() + " PRESENTS"`. |
| `…:184-186` → `TournamentSelectionCard.cs:185` | The **only** sprite→card path in the project: `SetCourseImage(Sprite)` → `_tournamentImage.sprite`. |

### 2.2 Client — networking already built (reuse, do not rebuild)

`Golfin.Net` (`Assets/Scripts/Net/`): `ApiClient.Instance` (`ApiClient.cs:32`), `IEnumerator Get<T>(string url, Action<ApiResult<T>> onResult)` (`:67`), Bearer re-stamped per attempt, transient retry, **401 → refresh → replay once** (403 deliberately does not refresh). `Endpoints.cs:26 BaseUrl => RootUrl + "/api/v1"`, `:29 Health` is root-mounted. `ApiResult<T>` carries `Success / Data / StatusCode / ErrorKind / RawBody`.

Atomic-write pattern to copy: `Assets/Scripts/Economy/PendingOpsStore.cs:57-68` (`.tmp` → `File.Replace`/`File.Move`).

⚠️ **`Golfin.Tournaments.asmdef` references only `["Golfin.UI.Rankings.Core", "Golfin.Save", "Golfin.Core.Stamina"]` — it does NOT reference `Golfin.Net`, and this spec does not add it** (§3, D2).

⚠️ **There is no image-download or texture-cache code anywhere in the project.** Zero hits for `UnityWebRequestTexture` / `DownloadHandlerTexture` outside Editor tooling. §5.5 is net-new.

### 2.3 Server

- `backend/main.py:39` — `app.include_router(tournaments.router, prefix="/api/v1/tournaments")`. Routers declare no internal prefix.
- `backend/routers/tournaments.py:61` — `GET /api/v1/tournaments/active`, **no auth**, returns `{"data":[…]}`. ⚠️ It does **not** filter on `kind`, so it will start serving golfin rows to GPS clients.
- `:191` `POST /tournaments/admin/create` and `:215` `POST /tournaments/admin/weekly-open` are guarded only by an inline `if req.admin_key != settings.admin_preload_key` (non-constant-time), and `admin_create` never sets `kind` → rows default to `'gps'`.
- `:225 auto_enter_score(...)` — not a route; enters real-world GPS scores into tournaments. Its two selects (`:239`, `:248`) also do not filter `kind`.
- Envelope is `{"data": …}` written by hand in every route; errors are FastAPI's `{"detail": …}`.
- Every router makes its own `create_client(settings.supabase_url, settings.supabase_service_key)` — **service_role**; RLS on these tables has no policies, so backend-only.
- Live catalog check (2026-08-14): `tournament_prize` is `pts=null, max_per_event=2000, daily_cap=null` — the rebalanced values, matching the top prize band exactly.

---

## 3. Design decisions

**D1 — Identity is server-owned, venue is course-owned.** Display name resolves `localize(name_key)` → `title` → `slug`. Card art resolves `banner_url` → `Resources/TournamentImages/{course_id}` → placeholder. `course_id` picks the venue and the subtitle; nothing else.

**D2 — Networking stays out of `Golfin.Tournaments`.** That assembly is deliberately dependency-light. **Do not add `Golfin.Net` to `Golfin.Tournaments.asmdef`.** The fetch, the JSON mapping and the art service live in `Assets/Scripts/TournamentsRuntime/` (no asmdef → compiles into `Assembly-CSharp`, which already sees `Golfin.Net` and `Golfin.Economy` because both are `autoReferenced: true`). The tournaments core keeps receiving plain DTOs and never learns that a network exists.

**D3 — The shipped CSV is the offline fallback, not dead weight.** A cold launch with no network must behave **exactly as it does today**. Server data replaces CSV data wholesale or not at all — never a merge (a half-server/half-CSV schedule is unreproducible in a bug report). **Amended 2026-08-14 (Architect, after review):** the no-merge rule governs the schedule as a whole. A definition the player is **mid-entry in** (§4.2) is the one deliberate exception — it is carried forward with its own prize table even when that came from the CSV, and the carry-forward is logged. Nothing else crosses the boundary. Carrying it forward also means carrying it forward when the server has **dropped it entirely**: losing an entered definition makes every `GetTournament(id)` throw `KeyNotFoundException` (`LocalTournamentBackend.cs:124`), which is the signup modal, the result modal, the round handler and `SubmitHoleResult`.

**D4 — State is still derived on the client.** `DeriveState` keeps owning Upcoming/Open/Playing/Ending/Closed/Ended from `StartUtc`/`EndUtc`. `tournaments.status` is not read for golfin rows and must not be — it is deliberately not maintained (see the column comment in the Phase-1 migration).

**D5 — Bad server data fails loudly and locally.** `CheckReferentialIntegrity` runs over the mapped server data. A tournament with a dangling `bot_field_id` or an empty prize ladder is **dropped with an error log**, and the rest render. Never a silent disappearance, never a half-defined tournament.

**D6 — Remote art is a content channel into every player's device.** The client accepts a `banner_url` only if it is `https` **and** its host + path prefix match the project's Storage bucket exactly (§5.2). Anything else is refused, logged, and falls through to bundled art. This is the control that makes a free-text column safe.

---

## 4. Schedule fetch

### 4.1 New endpoint (playlife)

`GET /api/v1/tournaments/golfin` — **no auth** (same posture as `/active`; the schedule is public, and the fetch should be able to warm before any token work).

```json
{"data": {
  "fetched_at": "2026-08-14T03:00:00Z",
  "tournaments": [{
    "slug": "kasumigaseki_open", "title": "Kasumigaseki Open", "name_key": "tourn.kasumigaseki",
    "course_id": "kasumigaseki", "hole_set": "1-18",
    "start_at": "2026-08-09T00:00:00+00:00", "end_at": "2026-08-25T00:00:00+00:00",
    "resolve_delay_minutes": 30, "entry_fee_pts": 10,
    "bot_field_id": "field_major", "sponsor_name": "PUMA", "league_key": "DIAMOND",
    "banner_url": null, "bot_seed": 1001,
    "prize_bands": [{"rank_from":1,"rank_to":1,"rp_reward":2000,"item_reward_id":"trophy_major"}]
  }]
}}
```

- `where kind = 'golfin'`, ordered by `start_at`. **No `status` filter** — the client derives state, and an Ended tournament must still render its LEADERBOARD card.
- Prize bands joined into the same payload (one round trip), sorted by `rank_from`.
- ⚠️ **Also required, and small:** `/tournaments/active` (`tournaments.py:61`) gains `.eq("kind","gps")` so GPS clients never receive game rows, and `auto_enter_score`'s two selects (`:239`, `:248`) get the same filter — a real-world GPS score must never enter a golfin tournament.

### 4.2 Client fetch and cache

New `RemoteTournamentSource` in `Assets/Scripts/TournamentsRuntime/`:

- `IEnumerator FetchRoutine(Action<RemoteScheduleResult> onDone)` over `ApiClient.Instance.Get<…>`, with a new `Endpoints.TournamentsGolfin => BaseUrl + "/tournaments/golfin"`.
- On success: write the **raw response body** to `<persistentDataPath>/tournaments_schedule.json` (atomic, per `PendingOpsStore.cs:57-68`), then map.
- Source precedence: **live fetch → cached JSON → shipped CSV.** Log which one won, once, at info level, naming it.
- **Timing.** `TournamentService.Awake()` composes from cache-or-CSV **synchronously as today**, so nothing on the boot path waits on a socket. Then it kicks the fetch. When the fetch lands, recompose the backend and raise a new `TournamentService.OnScheduleChanged`; `TournamentSelectionScreenController.OnEnable` (`:92`) already rebuilds on entry, so subscribing there is enough to repaint.
- **A tournament the player has already entered must not change under them mid-session.** If `GetMyEntry(slug)` is non-null, keep the definition already in play and log the deferral; the new one applies at the next launch.

### 4.3 Mapping to the existing DTOs

`TournamentDefinition` gains **two nullable fields, appended** so the positional ctor stays a minimal diff:

- `string? Title` — the server's `title`; null for CSV rows.
- `string? BannerUrl` — the server's `banner_url`; always null for CSV rows.

| DTO member | Source |
|---|---|
| `Id` | `slug` — the client's stable key stays the slug, not the uuid |
| `NameKey` | `name_key` (may be null/empty) |
| `Title` | `title` |
| `ClubId` | `course_id` |
| `HoleSet` | `TournamentCsvLoader.ExpandHoleSet(hole_set)` — reuse, do not reimplement |
| `StartUtc` / `EndUtc` | parsed as **absolute UTC**, same discipline as `ParseUtc` (`:435`) |
| `ResolveDelayMinutes` | `resolve_delay_minutes` |
| `EntryFeeRP` | `entry_fee_pts` |
| `PrizeTableId` | **synthesized = `slug`** — server bands are per-tournament, so each tournament gets a one-entry `PrizeTable` keyed by its own slug |
| `BotFieldId` | `bot_field_id`, validated against the still-bundled `tournament_bot_fields.csv` |
| `SponsorKey` / `LeagueKey` | `sponsor_name` / `league_key` |
| `BannerUrl` | `banner_url` after the §5.2 host check; a refused URL maps to null |

**Display-name change.** `TournamentSelectionScreenController.cs:153` becomes: localize `NameKey`; if that returns empty **or echoes the key back**, use `Title`; if that is empty too, use `Id`. Apply the same ladder wherever else the tournament name is rendered — check `TournamentSignupModalController` and `TournamentResultModalController`, which already do the echo-check trick for the venue key at `:260-263` and `:180-183`.

---

## 5. Artwork

### 5.1 Resolution order — first hit wins, every step degrades safely

1. **`BannerUrl`** — downloaded, disk-cached, host-validated.
2. **`Resources/TournamentImages/{ClubId}`** — the shipped course photo.
3. **Placeholder sprite** + one warning log.

While a remote image is still downloading the card shows layer 2 immediately and swaps on arrival — **no empty rectangle, no layout jump**. Never a blank card; never another course's photograph.

### 5.2 Host allowlist (the security control)

Accept a `banner_url` only when it starts with, exactly:

```
https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/tournament-art/
```

as a single `const string` next to the fetch. Scheme, host **and** path prefix — compared on a **parsed `Uri`** (`Scheme` / `Host` / normalized `AbsolutePath`), never on the raw string. A raw `StartsWith` is defeated by `…/tournament-art/../../../rest/v1/rpc/x`, which `System.Uri` normalizes back out of the bucket before the request is sent. Anything else → refuse, `Debug.LogWarning` naming the slug and the offending host, treat `BannerUrl` as null. Rationale: `/tournaments/admin/create` still writes these tables behind a static key (§2.3), so the column is not exclusively the dashboard's.

### 5.3 Bundled art (layer 2)

- **Move** `Assets/Art/Tournaments/CourseImages/` → `Assets/Resources/TournamentImages/`. The six files are already named by `course_id` (`gotemba.png`, `hirono.png`, `kasumigaseki.png`, `kawana.jpg`, `kisarazu.png`, `lomond.png`) and `Resources.Load<Sprite>` is extension-agnostic, so **no renames**. Move the `.meta` files with them so GUIDs and import settings survive.
- Resolve as `Resources.Load<Sprite>($"TournamentImages/{def.ClubId}")`, memoised in a dictionary — do not `Resources.Load` per card per rebuild.

### 5.4 Delete the positional fallback

**`_courseImages` (`TournamentSelectionScreenController.cs:31`) and the `csvIndex` branch (`:333-334`) are deleted**, along with the `csvIndex` parameter. It is the bug, not a safety net: once the dashboard can reorder tournaments, a positional array silently reshuffles which photo lands on which card. Clear the serialized array from the scene/prefab too, so it does not linger as dead serialized data.

Keep `_courseImageMap` (`:35`) as an optional per-tournament override — and **fix its null-shadowing bug at `:330`**: it returns `mapEntry.Sprite` even when that sprite is null, so an empty map entry blanks the card instead of falling through. A null sprite must fall through to the next layer.

Add `[SerializeField] private Sprite _placeholderImage;`. If it is unwired, log a warning once and leave the card image disabled rather than showing whatever the prefab happened to ship with.

### 5.5 Download + disk cache

New `TournamentArtService` (`Assets/Scripts/TournamentsRuntime/`):

- `UnityWebRequestTexture.GetTexture(url)` → `Sprite`. Cache to `<persistentDataPath>/tournament-art/<first 16 hex of sha256(url)><ext>`.
- **`redirectLimit = 0`.** A 30x from the allowlisted origin would otherwise put third-party bytes into the texture *and* into the disk cache under an allowlisted key, where they reload every launch with no network check.
- **Refuse on `Content-Length` before buffering** (cap ~1 MB — the dashboard caps uploads at 500 KB, but the static-key admin route can write `banner_url` too). `DownloadHandlerTexture` buffers the whole body before any budget is consulted and `Prefetch` fires unattended at boot, so one oversized object is an OOM on launch with no user action.
- The sweep **skips `*.tmp`** — it runs on the same frame as `Prefetch` and would otherwise delete a staging file mid-write.
- Prefetch **and** the sweep run on **every** boot path (live, cache, CSV), not only after a successful fetch — otherwise the 50 MB bound only exists on sessions that reach the server, and Risk 2 says those are not all of them.
- **The URL is the cache key** — the dashboard writes content-hashed immutable names (`{slug}-{hash}.jpg`), so a changed image is a new URL and there is nothing to invalidate.
- In-memory sprite dictionary keyed by URL, so one texture serves every card and every screen.
- **Prefetch on schedule fetch** (boot / sign-in) so the T7 screen is warm before it is opened.
- Bounded cache: **50 MB, LRU by last access**, plus eviction of art whose tournament ended more than 30 days ago. Sweep on a background pass at boot, never mid-frame.
- Offline: cached art shows; uncached falls to layer 2.
- Coalesce concurrent requests for the same URL — one download in flight per URL.

---

## 6. Acceptance

Ordered so the headline is first. 📱 = needs a device or a real network.

1. 📱 **The brand case (the whole point).** In the dashboard create `puma_summer_slam` on Lomond, title *"PUMA Summer Slam"*, **no localization key**, upload brand art. Relaunch the game with no rebuild → the T7 card reads **PUMA Summer Slam** and shows **the uploaded art**, with *Lomond Country Club · 18 Holes* as the venue subtitle.
2. 📱 **The schedule is live.** Change a date in the dashboard → relaunch → the card's state and date line follow. No rebuild.
3. 📱 **Cold launch, airplane mode, first ever run.** T7 renders exactly as it does today from the CSV; the console names the fallback source once.
4. **Cache hit.** Second launch downloads zero images (log the hits); art still correct.
5. **Art removed.** Clear `banner_url` in the dashboard → relaunch → the card falls back to the course photo. At no point does any card show a *different* course's photo.
6. **Host allowlist.** Set `banner_url` to an off-host URL directly in SQL → the client refuses with a warning naming the host, renders bundled art, downloads nothing.
7. **Reorder.** Change tournament order in the dashboard → no photo reshuffle (the positional fallback is gone).
8. **Bad server data.** Point one tournament at `bot_field_id = 'field_nonexistent'` → that tournament is dropped with an error log; the others render normally.
9. **Mid-entry stability.** Enter a tournament, then change its prize ladder in the dashboard and force a refetch → the entered tournament does not mutate under the player; the deferral is logged.
10. **Full EditMode suite green**, swept **per assembly** — a filtered run reports `FailedTests` for the filter only while `TotalTests` counts the whole mode, so one filtered green run proves nothing. New tests required for: the JSON→DTO mapper (including `hole_set` expansion and absolute-UTC parsing), the name-fallback ladder, the host allowlist (an accept/reject table), the cache-key derivation, and `CheckReferentialIntegrity` over server data.

---

## 7. Out of scope

Entries, per-hole submission, leaderboards, server-side bot-field generation and the prize resolver — Phases 4–5 in the parent spec (`tournaments_server_side` §6b), and the open bot/human prize-rank UX question (§6b.4) needs Cesar's call before the leaderboard screen is touched. Also out: sponsor **logo** images (sponsor stays text), any new playable course, and unifying GPS tournaments with game tournaments.

---

## 8. Risks

1. **Only `lomond-country-club` has playable hole data.** The six tournament `course_id`s are card metadata; `HoleParProviderAdapter.cs:35-37` ignores `clubId` ("advisory — HoleDatabase is not club-scoped"), and the two id namespaces (`lomond` vs `lomond-country-club`) never meet. This phase does not fix that and must not pretend to — a tournament on Kawana still plays Lomond's holes. Flag it if the mapping starts to matter.
2. **Free-tier Supabase auto-pauses.** A paused project means CSV fallback — acceptable by design, but it means "no network" is not a rare path. Test it.
3. **The static-key admin routes** can still write these tables (`tournaments.py:193/219`, non-constant-time compare, `admin_create` never sets `kind`). Close them or move them behind service_role before tournaments carry anything a player would miss.
4. **Remote art is a channel into every device.** §5.2's allowlist is the only control. Do not relax it to "any https URL", and do not move the check to the server alone.
5. **CSV drift.** Once the server is authoritative, the shipped CSV is a snapshot that ages. Refresh it from the dashboard's export at each release, or the offline path shows a schedule from three builds ago.
