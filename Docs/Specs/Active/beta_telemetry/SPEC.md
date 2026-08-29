# SPEC — `beta_telemetry`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Next week ~20 external TestFlight testers play the game live. This task adds the
telemetry pipeline that tells us, from real devices, three things we cannot learn
any other way: **do the shot controls work** (flick rejections, cancels, OB rate,
strokes vs par), **where do testers stop** (screen funnel, round abandons), and
**is it broken on their hardware** (exceptions, load time, FPS). Plus the economy
loop (points, level-ups, stamina) since the same hooks are nearly free.

Two halves, same shape as `tournaments_server_side` / `game_banners`:
1. **Unity client** — a batching `TelemetryService` that subscribes to existing
   events and POSTs to playlife-api. Almost entirely additive; ~4 one-line
   call-site insertions in existing files.
2. **Backend** — one new table (`telemetry_events`), one new router
   (`backend/routers/telemetry.py`), mounted at `/api/v1/telemetry`.

The admin dashboard panel that reads this data is a **separate spec**
(`telemetry_admin_panel`) and is NOT in scope here.

## Reference

- No Figma. This task has no UI.
- `Docs/Specs/Active/beta_telemetry/reference/EVENT_CATALOG.md` — the event
  list + payload schema below is duplicated there for the panel spec to share.

## Figma Fidelity

N/A — no UI elements.

## Architecture context

- **Asmdef boundaries affected:** none changed. New code goes in
  `Assets/Scripts/Telemetry/` in **Assembly-CSharp** (like `Demo/DemoGate`), which
  can see `Golfin.Net`, `Golfin.Gameplay.Loop`, `Golfin.Gameplay.Input` (all
  auto-referenced). **Verified 2026-08-18:** `Golfin.Gameplay.Input` is `autoReferenced: false` —
  Assembly-CSharp CANNOT see `ShotController`. The cancel/reject hooks therefore
  route through a relay: ShotController raises two new `public static` events,
  and a ~15-line `ShotTelemetryRelay` static class in `Golfin.Gameplay.UI`
  (autoReferenced: true; its asmdef already references BOTH `Golfin.Gameplay.Input`
  and `Golfin.Gameplay.Loop` — verified) subscribes and re-raises them where
  Assembly-CSharp can see. Do NOT flip Input's autoReferenced flag instead.
- **Existing code referenced (all verified in repo 2026-08-18):**
  - `Assets/Scripts/Net/ApiClient.cs` — `ApiClient.Instance`, `Post<T>(url, json, cb)`, `Run(routine)`. Bearer token, `{data:…}` envelope unwrap, retry, 401-refresh-replay all already handled here. Do not duplicate any of it.
  - `Assets/Scripts/Net/Endpoints.cs` — add `TelemetryEvents => BaseUrl + "/telemetry/events"`.
  - `Assets/Scripts/Auth/AuthService.cs` — `Session.IsAuthenticated`, `Session.UserId`; static event `SignedIn` (line 61).
  - `Assets/Scripts/UI/ScreenManager.cs` — `public static event System.Action<ScreenId>? ScreenChanged;` (line 97); `ScreenId` enum (lines 7–41).
  - `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` — `OnHoleComplete` (l.103), `OnHistoryChanged` (l.95), `ShotHistory` (l.94), `SeedSession` (l.149), `ResetSession` (l.169), `ShotRecord` struct (l.194: ShotNumber, ClubLabel, DistanceXZMeters, TerminalState, OBReason, FinalSurface, PenaltyStrokes), `IsTournament`, `TournamentId`, `CurrentHoleNumber`, `SelectedCharacterId`.
  - `Assets/Scripts/Gameplay/Loop/Session/HoleCompletionData.cs` — TerminalState, Strokes, PenaltyStrokes, HoleNumber, CompletedAtUtc.
  - `Assets/Scripts/Gameplay/Input/ShotController.cs` — `EndExternalDrag` (l.330, the `validFlick == false` branch), `CancelExternalDrag` (l.345), `LastFlickSpeedScreenHeights`.
  - `Assets/Scripts/UI/Roster/Managers/RewardPointsManager.cs` — `OnPointsChanged` (l.35).
  - `Assets/Scripts/CharacterManager.cs` — `OnCharacterLeveledUp` (l.38).
  - playlife: `backend/auth.py` (`get_current_user`), `backend/routers/banners.py` + `points.py` (router pattern), `backend/main.py` (`include_router` block, lines 22–42).
- **Manager APIs used:** listed above with line numbers.

## 1. Event catalog

Every event carries the batch-level envelope fields (§2) plus `name`, `ts`
(client UTC ISO-8601), `event_id` (client GUID), and a `payload` object:

| # | name | fired when / hook | payload |
|---|---|---|---|
| 1 | `session_start` | App boot, first `ScreenChanged` → `Logo` (or Awake of the service) | `device_model` (SystemInfo.deviceModel), `os` (SystemInfo.operatingSystem), `memory_mb` (SystemInfo.systemMemorySize), `screen` ("WxH") |
| 2 | `session_end` | `OnApplicationPause(true)` and `OnApplicationQuit` | `duration_s` (realtimeSinceStartup) |
| 3 | `screen_view` | `ScreenManager.ScreenChanged` | `screen` (enum name), `since_boot_s`. The first `Home` view's `since_boot_s` IS the boot→Home load-time metric — no separate event. |
| 4 | `round_start` | one-line call inserted at the END of `GameSession.SeedSession` | `hole`, `character_id`, `bag_slot`, `is_tournament`, `tournament_id` |
| 5 | `shot_taken` | subscribe `GameSession.OnHistoryChanged`, read `ShotHistory[^1]` | `shot_number`, `club` (ClubLabel), `distance_m` (DistanceXZMeters, 1dp), `terminal` (TerminalState), `ob_reason`, `surface` (FinalSurface), `penalty` (PenaltyStrokes), `hole`, `timing01` (slab progress at the aim latch, 2dp, **null** when the swing pushed no touch sample — bot/capture/debug), `timing_mul` (F15 power multiplier that timing cost, 2dp, 1.0 = none), `timing_band` (`"green"`/`"gold"`/`"red"`, null with `timing01` — derived client-side from the same `ControlsConfig` edges the shot paid, added by `shot_timing_telemetry` 2026-08-29) |
| 6 | `flick_rejected` | `ShotController.EndExternalDrag`, the `validFlick == false` branch, raises new `public static event Action<float> FlickRejected` (one line); reaches Assembly-CSharp via `ShotTelemetryRelay` (see Architecture context) | `speed` (LastFlickSpeedScreenHeights), `hole`, `shot_number` |
| 7 | `shot_cancelled` | `ShotController.CancelExternalDrag` raises new `public static event Action ShotCancelled` (one line); same relay | `hole`, `shot_number` |
| 8 | `hole_complete` | `GameSession.OnHoleComplete` | `hole`, `strokes`, `penalty_strokes`, `result` ("InCup"/"StrokeCap"), `duration_s` (since round_start), `fps_avg`, `fps_low` (see §4), `par` — NOTE: par comes from the hole metadata; verify the accessor on `Assets/Scripts/HoleMetadata.cs` and flag in the report if par is not cleanly reachable here — omit the field rather than inventing a lookup |
| 9 | `round_abandoned` | `ScreenManager.ScreenChanged` fires a menu screen (Home/HoleSelection/ModeSelection) while a round is active and no `hole_complete` was recorded for it. Track "round active" inside TelemetryService (set on round_start, cleared on hole_complete / next round_start). Do NOT hook `ResetSession` — verify first whether it also runs on normal completion; if it is a clean abandon-only choke point, use it instead and say so in the report | `hole`, `shots_taken`, `last_screen` |
| 10 | `client_error` | `Application.logMessageReceived`, `LogType.Exception` only | `message` (≤300 chars), `stack` (≤2000 chars), `screen` (current ScreenId). **Cap 10 per session**, dedupe by hash of message+first stack line |
| 11 | `points_changed` | `RewardPointsManager.OnPointsChanged` | `balance`, `delta` (computed vs previous callback value) |
| 12 | `level_up` | `CharacterManager.OnCharacterLeveledUp` | `character_id` |
| 13 | `sp_allocated` | NOTE: find the commit call site for `ManualSPAllocation` (Assets/Scripts/UI/Roster/Data/ManualSPAllocation.cs / CharacterManager); one-line call where SP spend is confirmed. If there is no single clean call site, skip this event and flag it in the report — do not scatter calls | `character_id`, `stat`, `amount` |

Deliberately NOT events: stamina/shop/gacha visits (covered by `screen_view`),
server-side API failures (already visible in Fly logs), positions/GPS (privacy,
and no location code ships yet per TESTFLIGHT_RUNBOOK).

## 2. Wire format + backend

### 2.1 Request

`POST /api/v1/telemetry/events` — **auth required** (`get_current_user`, same as
points; the Bearer token rides ApiClient automatically). Body:

```json
{
  "session_id": "guid-per-app-launch",
  "app_version": "1.5.7",
  "build_number": 2192,
  "platform": "iOS",
  "events": [
    { "event_id": "guid", "name": "shot_taken", "ts": "2026-08-24T09:31:04Z", "payload": { } }
  ]
}
```

Constraints server-side: ≤100 events per batch (413 above), `name` ≤64 chars,
payload ≤4KB per event (reject oversized events individually, accept the rest).
Response envelope: `{"data": {"accepted": N, "duplicates": M}}`.
`build_number`: reuse the build-stamp value that `build_version_stamp` ships —
NOTE: verify the accessor in `Assets/Scripts/UI/BuildInfo/`; fall back to
`Application.version` only if the stamp is not runtime-readable.

### 2.2 Migration — `playlife/backend/migrations/2026_08_18_telemetry_events.sql`

```sql
create table if not exists public.telemetry_events (
  id           bigint generated always as identity primary key,
  event_id     uuid not null unique,          -- client GUID; makes retries idempotent
  user_id      uuid not null,
  session_id   uuid not null,
  name         text not null,
  ts           timestamptz not null,          -- client clock
  received_at  timestamptz not null default now(),
  app_version  text,
  build_number int,
  platform     text,
  device_model text,
  os           text,
  payload      jsonb not null default '{}'::jsonb
);
create index if not exists telemetry_events_name_ts  on public.telemetry_events (name, ts desc);
create index if not exists telemetry_events_user_ts  on public.telemetry_events (user_id, ts desc);
create index if not exists telemetry_events_session  on public.telemetry_events (session_id);
alter table public.telemetry_events enable row level security;
-- no policies: service_role only (the API writes, the admin dashboard reads). Anon/authenticated get nothing.
```

`device_model` / `os` are batch-envelope fields on the wire but denormalized onto
every row on insert — the panel never has to join to find a device.

**Process (ADMIN_DASHBOARD_OPS §3.2): migration first, deploy second.** Write the
file; Cesar pastes it into the Supabase SQL editor; verify via the REST probe
(`curl "$SUPABASE_URL/rest/v1/telemetry_events?limit=1&select=*"` with the service
key) BEFORE `fly deploy`.

### 2.3 Router — `backend/routers/telemetry.py`

Follow `points.py` structurally: module-level `create_client(settings.supabase_url,
settings.supabase_service_key)`, pydantic request models, `Depends(get_current_user)`.
Insert with upsert-ignore on `event_id` (`on_conflict="event_id", ignore_duplicates`
via the supabase-py upsert, or catch the unique violation per batch — implementer's
choice, but a replayed batch must return 200, not 500). Stamp `user_id` from the
token — NEVER trust a user_id in the body. Mount in `main.py`:
`app.include_router(telemetry.router, prefix="/api/v1/telemetry", tags=["Telemetry"])`.

## 3. Unity client

New folder `Assets/Scripts/Telemetry/` (Assembly-CSharp), namespace `Golfin.Telemetry`:

- **`TelemetryService.cs`** — plain C# singleton like `ApiClient` (constructible in
  EditMode tests), plus a tiny `TelemetryBehaviour` MonoBehaviour (DontDestroyOnLoad)
  that owns the flush timer and forwards `OnApplicationPause`/`OnApplicationQuit`.
  Public surface: `TelemetryService.Instance.Record(string name, Dictionary<string,object> payload = null)`.
- **`TelemetryHooks.cs`** — one static `Install()` called once at boot (from the
  same bootstrap point that initializes other services — NOTE: identify the boot
  choke point, likely wherever `FramePacingBootstrap` or the Logo screen init
  lives, and name it in the report). Subscribes to every event in §1.
- **`TelemetryConfig.cs`** — `public const bool Enabled = true;` plus: in the
  Editor, sends are OFF unless `GOLFIN_TELEMETRY_DEBUG` is defined (so Editor
  sessions don't pollute beta data). Device builds send whenever authenticated.

Behavior rules:

1. **Never block or throw into gameplay.** Every hook body is wrapped; a telemetry
   bug must never break a shot. No allocation-heavy work per event beyond the
   payload dictionary.
2. **Batching:** in-memory queue; flush when 20 events pending OR 30s elapsed OR
   pause/quit. Queue cap 500 events, drop-oldest. Flush = serialize the drained
   batch, `ApiClient.Instance.Run(ApiClient.Instance.Post<TelemetryAck>(Endpoints.TelemetryEvents, json, cb))`.
   On failure: re-enqueue ONCE (the `event_id` unique key makes the retry safe);
   on second failure drop the batch and log once. ApiClient already retries
   transients and replays 401s — do not add another retry layer on top beyond
   this single re-enqueue.
3. **Auth gating:** if `!AuthService.Instance.Session.IsAuthenticated`, hold the
   queue (events still accumulate under the cap); flush on `AuthService.SignedIn`.
4. **Serialization:** payloads are flat string→(string|number|bool) — serialize
   with the same JSON approach the Net code already uses (NOTE: check what
   ApiClient/PointsService use — JsonUtility can't do dictionaries, so if the
   existing pattern is JsonUtility, build the batch JSON with a small hand-rolled
   writer in TelemetryService rather than importing a new JSON library).
5. **FPS sampling (§1 #8):** while a round is active, accumulate frame count /
   elapsed each frame in TelemetryBehaviour.Update (two floats, no lists), plus a
   1-second rolling worst bucket → `fps_avg`, `fps_low` on hole_complete.

Edits to existing code (each one line, plus the two event declarations):
`GameSession.SeedSession` (round_start call), `ShotController` (declare + raise
`FlickRejected` / `ShotCancelled`), `Endpoints.cs` (URL). Plus ONE new file
outside the Telemetry folder: `Assets/Scripts/Gameplay/UI/ShotUI/ShotTelemetryRelay.cs`
(Golfin.Gameplay.UI) — subscribes to the ShotController events in a static
initializer (or `[RuntimeInitializeOnLoadMethod]` — implementer verifies which
pattern survives domain-reload settings) and re-raises identical static events
that `TelemetryHooks` (Assembly-CSharp) subscribes to.

## 4. Privacy / review posture

- Nothing sent beyond: Supabase user id, device model/OS strings, gameplay data.
  No email, no name, no location, no contacts. This is standard app analytics —
  no new Info.plist usage descriptions, no App Privacy label change needed for
  TestFlight (Beta App Review does not require the privacy nutrition label).
- The endpoint is authenticated; testers can only write their own rows.

## 5. Acceptance tests

EditMode (follow `Assets/Scripts/Net/Tests/ApiClientTests.cs` harness style —
fake transport, pumped coroutines):
1. 20 queued events trigger a flush; batch JSON contains all 20 with distinct `event_id`s.
2. Timer flush at 30s with <20 events.
3. Queue cap: 501st event drops the oldest, count stays 500.
4. Failed flush re-enqueues once with the SAME event_ids; second failure drops.
5. Unauthenticated: nothing sent; `SignedIn` triggers flush.
6. Hook safety: a throwing payload builder is swallowed (logged), does not propagate.
7. client_error cap: 11th exception in a session is not enqueued.

Backend (manual, curl):
8. POST with valid token + 2 events → `{"data":{"accepted":2,"duplicates":0}}`; rows visible via REST probe.
9. Replay the same body → `accepted:0, duplicates:2` (or equivalent) — no double rows.
10. No/invalid token → 401/403, not 500. 101 events → 413.

On-device (Cesar, once TestFlight build exists):
11. Play one hole signed in; verify session_start, screen_views, round_start, N×shot_taken, hole_complete rows in Supabase with correct hole/strokes.

## 6. Out of scope

- The admin dashboard panel (separate spec `telemetry_admin_panel`).
- Dashboards/aggregation SQL, retention/cleanup jobs (20 testers ≈ trivial volume).
- Remote config / server-side kill switch, A/B anything.
- Offline persistence of the queue across app kills (in-memory only is fine for the beta).
- GPS/location, any PII.
