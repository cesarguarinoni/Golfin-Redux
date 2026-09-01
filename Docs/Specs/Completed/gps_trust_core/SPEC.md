# SPEC — `gps_trust_core`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Starts at `SPEC_READY`.

## Goal

Port the PLAYLIFE **GPS Trust subsystem** to Unity as a new, game-free assembly **`Golfin.Gps`** — the layer every GPS/PLAYLIFE screen (check-in, score submit, venue list) will sit on. It is pure logic plus one native seam (location) and one deferred seam (Android mock detection), so it ships **with no UI, no ScreenManager state and no prefab** and is proven by EditMode tests.

Why first: `GPS_UNITY_PORT_SPEC.md` §5 calls this "the differentiator — port faithfully". The backend awards Trust from exactly the fields this module produces (`backend/routers/score.py:117-140`, `:165-186`), and a wrong constant here silently defeats the anti-cheat while every screen above it looks fine. Building it in isolation, against the Dart originals, with the constants asserted by tests, is the cheapest place to get it byte-exact.

Context of record (Cesar, 2026-09-01): the GPS features are rebuilt as ONE shared module (`Golfin.Net` / `Auth` / `Gps` / `Economy` / `Social` + DTOs) deployed two ways — inside the game and, later, as a standalone PLAYLIFE app (shell decision deferred until `Gps`/`Social` land). **Therefore `Golfin.Gps` references `Golfin.Net` only** — never `Assembly-CSharp`, never anything under `Assets/Scripts/UI`. iOS first; the Android mock plugin is a later task and this spec only leaves the seam for it.

## Reference

- **Source of truth (Dart, on this Mac):** `/Users/cesar/Documents/playlife/lib/common/presentation/controller/`
  - `gps_session_tracker.dart` — fix log, throttle/prune/count, haversine
  - `gps_trust_signals.dart` — `gps_is_mock` + `client_platform`
  - `gps_score_attachment.dart` — position → signals → session → `/venue/auto-register` → payload
  - `current_location_notifier.dart` — 10 s high-accuracy fetch, `LocationFailReason`, 4-char geohash prefix change → `/venue/nearby`
- **Backend contracts (same repo):** `backend/routers/score.py` (`ScorePostRequest`, `_verify_gps`, trust maths), `backend/routers/venue.py` (`AutoRegisterRequest` + both response shapes, `/nearby`), `backend/routers/activity.py` (`CheckInRequest`, `CheckOutRequest`).
- **Design docs:** `Docs/GPS/GPS_INTEGRATION_REFERENCE.md` §6, `Docs/GPS/GPS_UNITY_PORT_SPEC.md` §3 / §5.
- **No Figma frame.** This task has no UI; the Fidelity table is intentionally absent. Ken's v3 mockups (`https://kenken1130.github.io/playlife-mockup/v3/score-upload-light.html`, screen 4 "GPS証明") show what will consume this module — do not build it here.

## Architecture context

- **New asmdef:** `Assets/Scripts/Gps/Golfin.Gps.asmdef` — `rootNamespace: Golfin.Gps`, `references: ["Golfin.Net"]`, `overrideReferences: true`, `precompiledReferences: ["Newtonsoft.Json.dll"]`, `autoReferenced: true` (mirror `Assets/Scripts/Economy/Golfin.Economy.asmdef`).
- **New test asmdef:** `Assets/Scripts/Gps/Tests/Golfin.Gps.Tests.asmdef` — mirror `Assets/Scripts/Net/Tests/Golfin.Net.Tests.asmdef` (Editor-only, `UNITY_INCLUDE_TESTS`, references `Golfin.Gps`, `Golfin.Net`, `Golfin.Net.Tests` for `FakeHttpTransport` / `FakeAuthTokenProvider`).
  - NOTE: `Golfin.Net.Tests` has `autoReferenced: false`; referencing it from another test asmdef by name is allowed. If the compiler refuses (assembly visibility), copy `FakeHttpTransport` into `Assets/Scripts/Gps/Tests/GpsTestDoubles.cs` under `Golfin.Gps.Tests` rather than loosening `Golfin.Net.Tests`.
- **Existing code used (do not modify unless listed):**
  - `Golfin.Net.ApiClient` (`Assets/Scripts/Net/ApiClient.cs`) — `Post<T>(url, jsonBody, onResult)`, `Get<T>(url, onResult)`, `Run(IEnumerator)`, `Instance`, `ConfigureForTest`. Bearer, `{data}` unwrap, transient retry and the single 401 replay are all already there. **Do not add a second HTTP path.**
  - `Golfin.Net.ApiResult<T>` / `ApiErrorKind` (`Assets/Scripts/Net/ApiResult.cs`).
  - `Golfin.Net.Endpoints` (`Assets/Scripts/Net/Endpoints.cs`) — **modified**: add the `// ── GPS / PLAYLIFE (gps_trust_core)` section below.
  - `Golfin.Net.ApiEnvelope.TryUnwrap` — note `/venue/auto-register` can legitimately return `{"data": null, "message": …}`; `TryUnwrap<VenueAutoRegisterResult>` yields `null` Data with `Success == true`. That is the "no course nearby" branch, not an error.
  - Pattern to copy for the service shape: `Golfin.Economy.PointsService` (plain C# singleton, `Instance` / `ConfigureForTest` / `ResetForTest`, borrows `ApiClient.Run`) and `Golfin.Economy.PointsDtos` (Newtonsoft `[JsonProperty("snake_case")]` fields, one doc-comment per DTO citing the router).
- **Not touched:** `Assembly-CSharp` (`ScreenManager`, `RewardPointsManager`, `CharacterManager`), `Golfin.Auth`, `Golfin.Economy`, any prefab, any scene, `LocalizationText.csv` (see §Strings).

## Implementation

All files under `Assets/Scripts/Gps/` unless stated. Every constant below is transcribed from the Dart source; the tests in §Tests assert them, so a "tidy-up" of a number is a test failure, not a refactor.

### 1. `GpsFix` + `IGpsFixStore` (+ two implementations) — `GpsFixStore.cs`

```csharp
namespace Golfin.Gps
{
    /// One recorded location fix. Wire schema is the Dart one, byte-for-byte: {"lat":…,"lon":…,"t":<epoch ms>}.
    public sealed class GpsFix
    {
        [JsonProperty("lat")] public double Lat;
        [JsonProperty("lon")] public double Lon;
        [JsonProperty("t")]   public long   T;   // Unix epoch milliseconds
    }

    public interface IGpsFixStore
    {
        List<GpsFix> Load();          // never null; malformed/empty → empty list (Dart `_load` swallows parse errors)
        void Save(List<GpsFix> fixes);
    }

    /// Shipping store. PlayerPrefs key "gps_session_fixes_v1", value = JSON array of GpsFix.
    public sealed class PlayerPrefsGpsFixStore : IGpsFixStore { public const string PrefsKey = "gps_session_fixes_v1"; … }

    /// Tests. Also handy for the future standalone shell's editor harness.
    public sealed class InMemoryGpsFixStore : IGpsFixStore { … }
}
```

`PlayerPrefsGpsFixStore.Save` calls `PlayerPrefs.SetString` + `PlayerPrefs.Save()`. `Load` wraps `JsonConvert.DeserializeObject<List<GpsFix>>` in try/catch and returns an empty list on any exception (mirrors Dart).

### 2. `GpsSessionTracker` — `GpsSessionTracker.cs`

Instance class (not static — the Dart one is static, but we need an injectable clock and store for tests). A lazily built `Instance` uses `PlayerPrefsGpsFixStore` + `() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`.

```csharp
public sealed class GpsSessionTracker
{
    // Constants — TRANSCRIBED from gps_session_tracker.dart. Do not "round".
    public const long RetentionMs      = 12L * 60 * 60 * 1000;   // _retention 12 h
    public const int  MaxFixes         = 100;                    // _maxFixes
    public const long RecordMinGapMs   = 5L * 60 * 1000;         // _recordMinGap 5 min
    public const double RecordMinMoveM = 100.0;                  // _recordMinMoveM
    public const long CountMinGapMs    = 10L * 60 * 1000;        // _countMinGap 10 min
    public const double SessionRadiusM = 5000.0;                 // _sessionRadiusM
    public const long SessionWindowMs  = 8L * 60 * 60 * 1000;    // _sessionWindow 8 h

    public GpsSessionTracker(IGpsFixStore store, Func<long> nowMs);

    /// Dart recordFix. Skips the fix when BOTH gap < 5 min AND moved < 100 m (note the AND).
    /// Appends, prunes (> 12 h dropped, then oldest dropped while count > 100), saves.
    public void RecordFix(double lat, double lon);

    /// Dart sessionNear. Fixes within 8 h AND within 5000 m of (lat, lon), sorted by t ascending.
    /// Empty → new GpsSession { CheckCount = 1 } with null coords.
    /// Otherwise count = 1 + number of fixes whose t is ≥ 10 min after the LAST COUNTED fix (not the previous fix),
    /// start = first, end = last.
    public GpsSession SessionNear(double lat, double lon);

    /// Haversine, r = 6371000 m. Public + static so the tests and Geohash-free callers can reuse it.
    public static double HaversineM(double lat1, double lon1, double lat2, double lon2);
}

public sealed class GpsSession
{
    public int CheckCount;
    public double? StartLat, StartLon, EndLat, EndLon;
}
```

### 3. `GpsTrustSignals` + platform/mock seams — `GpsTrustSignals.cs`

```csharp
public interface IMockLocationDetector { bool IsMock(); }          // Android plugin plugs in here later
public sealed class NeverMockDetector : IMockLocationDetector { public bool IsMock() => false; }

public interface IClientPlatformProbe { string Label(); }         // 'ios' | 'ios-simulator' | 'android' | 'editor' | 'unknown'
public sealed class UnityClientPlatformProbe : IClientPlatformProbe { … }

public sealed class GpsTrustSignals
{
    public bool IsMock;            // → gps_is_mock
    public string ClientPlatform;  // → client_platform

    public static GpsTrustSignals Capture(IMockLocationDetector mock, IClientPlatformProbe platform);
}
```

`UnityClientPlatformProbe.Label()`:
- `Application.platform == RuntimePlatform.IPhonePlayer` → `"ios-simulator"` when running on the simulator, else `"ios"`.
  NOTE: Unity has no `isPhysicalDevice`. Detect the simulator with `SystemInfo.deviceModel` — on the simulator it reports the host CPU (`"x86_64"` / `"arm64"`), on hardware an `"iPhone16,2"`-style identifier. Implement as `!deviceModel.StartsWith("iPhone") && !deviceModel.StartsWith("iPad") && !deviceModel.StartsWith("iPod")` → simulator, and **verify once on the simulator + once on device in the report** (a `Debug.Log` of the raw string is enough evidence). If it proves unreliable, flag it — do not guess a different heuristic.
- `RuntimePlatform.Android` → `"android"`.
- `Application.isEditor` → `"editor"` (honest label; the server penalises only `"ios-simulator"`, see `score.py:183`).
- anything else → `"unknown"` (Dart's fallback).

The server treats `client_platform == "ios-simulator"` as mock (`score.py:183`), so the iOS simulator is already covered without the Android plugin. `NeverMockDetector` is the shipping default on every platform for now; the Android plugin task replaces the default on `RuntimePlatform.Android` only.

### 4. `ILocationProvider` + `UnityLocationProvider` — `LocationProvider.cs`

```csharp
public enum LocationFailReason { None, ServiceDisabled, PermissionDenied, PermissionDeniedForever, Timeout, Unknown }

public sealed class LocationFix { public double Lat, Lon; public float AccuracyM; public long TimestampMs; }

public sealed class LocationResult
{
    public LocationFix Fix; public LocationFailReason Reason;
    public bool Ok => Fix != null;
}

public interface ILocationProvider
{
    /// Coroutine. Invokes onResult exactly once. Never throws; every failure is a Reason.
    IEnumerator Fetch(float timeoutSeconds, Action<LocationResult> onResult);
}

public sealed class UnityLocationProvider : ILocationProvider { public const float DefaultTimeoutSeconds = 10f; … }
```

`UnityLocationProvider.Fetch`:
1. `!Input.location.isEnabledByUser` → `ServiceDisabled` on iOS/Android. (Unity does not distinguish "services off" from "permission denied" here; both surface as `isEnabledByUser == false`. Map to `PermissionDenied` when `Application.platform` is iOS and the app has previously asked — NOTE: this distinction is not observable from `Input.location`; ship `ServiceDisabled` for both and record the limitation in the report. The two strings differ only in the advice given, and the check-in screen spec will decide whether to use a native permission probe.)
2. `Input.location.Start(desiredAccuracyInMeters: 10f, updateDistanceInMeters: 5f)` — high accuracy, the Dart `LocationAccuracy.high` equivalent.
3. Poll `Input.location.status` every frame until `Running` or `Failed` or the timeout elapses (`Time.realtimeSinceStartup`, so it works while paused). `Failed` → `Unknown`; timeout → `Timeout`.
4. On `Running`: read `lastData` → `LocationFix { Lat = latitude, Lon = longitude, AccuracyM = horizontalAccuracy, TimestampMs = (long)(timestamp * 1000) }`, then `Input.location.Stop()`. **Always** `Stop()` on every exit path (battery).
5. In the Editor, `Input.location` never runs. Return `Unknown` immediately with a one-line `Debug.LogWarning` — and tests use `FakeLocationProvider` (see §Tests), never this class.

Key strings for the reasons are DEFINED here so the check-in screen binds to fixed names, but the CSV rows are NOT added by this task (§Strings):

```csharp
public static class LocationFailReasonKeys
{
    public static string For(LocationFailReason r) => r switch {
        LocationFailReason.ServiceDisabled         => "GPS_ERR_SERVICE_DISABLED",
        LocationFailReason.PermissionDenied        => "GPS_ERR_PERMISSION_DENIED",
        LocationFailReason.PermissionDeniedForever => "GPS_ERR_PERMISSION_DENIED_FOREVER",
        LocationFailReason.Timeout                 => "GPS_ERR_TIMEOUT",
        _                                          => "GPS_ERR_UNKNOWN" };
}
```

**iOS Player Setting:** `Location Usage Description` (`NSLocationWhenInUseUsageDescription`) must be non-empty or iOS kills the app on `Input.location.Start()`. Set it in `ProjectSettings/ProjectSettings.asset` (`locationUsageDescription`) to: `GOLFIN uses your location to verify rounds at the golf course you are playing.` If it is already set, leave it and quote the current value in the report.

### 5. `Geohash` — `Geohash.cs`

Port of what `dart_geohash` provides to `current_location_notifier.dart`: `Encode(lat, lon, precision = 12)` (base32 `0123456789bcdefghjkmnpqrstuvwxyz`, standard interleaved bit algorithm) and `Neighbors(hash)` returning the 8 neighbours (N, NE, E, SE, S, SW, W, NW). Plus the one helper the venue flow needs:

```csharp
/// The comma-joined prefix list for GET /venue/nearby: the 4-char cell + its 8 neighbours (Dart order: neighbors + self).
public static string NearbyPrefixes(double lat, double lon, int precision = 4);
```

NOTE: the backend also encodes geohashes (`venue.py::_geohash_encode`) — the tests must check `Encode` against a known vector that both agree on: `Encode(35.681236, 139.767125, 12) == "xn76urx6606p"` and `Encode(…, 9) == "xn76urx66"` (Tokyo Station; standard geohash — the backend `_geohash_encode` uses precision 9 and `>=` at the midpoint, so match that comparison). If the backend's `_geohash_encode` differs from standard, stop and report — the two MUST match or `/venue/nearby` returns nothing.

### 6. `VenueService` — `VenueService.cs`

Plain C# singleton over `ApiClient` (shape of `PointsService`, minus the queue).

```csharp
public sealed class VenueService
{
    public static VenueService Instance { get; }            // lazily new VenueService(ApiClient.Instance)
    public static void ConfigureForTest(VenueService s); public static void ResetForTest();
    public VenueService(ApiClient client);

    /// POST /venue/auto-register {latitude, longitude}. Success with Data == null = "no course nearby" (NOT an error).
    public IEnumerator AutoRegister(double lat, double lon, Action<ApiResult<VenueAutoRegisterResult>> onResult);

    /// GET /venue/nearby?prefixes=…&language_code=… → List<VenueDto>
    public IEnumerator Nearby(string prefixes, string languageCode, Action<ApiResult<List<VenueDto>>> onResult);

    /// GET /venue/list?language_code= → List<VenueDto>
    public IEnumerator List(string languageCode, Action<ApiResult<List<VenueDto>>> onResult);
}
```

Only the two request fields the Dart client sends (`latitude`, `longitude`) go in the auto-register body; `radius_m` / `language_code` / `preferred_name` keep their server defaults (`venue.py:116-121`).

### 7. `GpsScoreAttachment` — `GpsScoreAttachment.cs`

The one orchestrator, a straight port of `gps_score_attachment.dart::capture` + `toJson`.

```csharp
public sealed class GpsScoreAttachment
{
    public LocationFix Position;      // null when the fetch failed — the submit still goes ahead (Dart: "投稿自体は止めない")
    public LocationFailReason PositionFailReason;
    public int? VenueId; public string VenueName; public double? VenueDistanceM;
    public GpsTrustSignals Signals;   // never null
    public GpsSession Session;        // null when Position is null

    /// Coroutine: fetch (5 s — Dart default for THIS path, not the notifier's 10 s) → signals → RecordFix + SessionNear
    /// → VenueService.AutoRegister → attachment. Every step failure degrades, none aborts.
    public static IEnumerator Capture(ILocationProvider location, GpsSessionTracker tracker, GpsTrustSignals signals,
                                      VenueService venues, Action<GpsScoreAttachment> onDone, float timeoutSeconds = 5f);

    /// Convenience over the shipping singletons/defaults.
    public static IEnumerator Capture(Action<GpsScoreAttachment> onDone);

    /// The fields to MERGE into the /score/submit body. Names are ScorePostRequest's (score.py:117-140). Absent = omitted (Dart `if (…)`), never null-valued.
    public JObject ToJson();
}
```

`ToJson()` emits, in this order:

| key | value | rule |
|---|---|---|
| `gps_verified` | `Position != null && VenueId != null` | always present |
| `latitude`, `longitude` | `Position.Lat/Lon` | only when `Position != null` |
| `venue_id` | `VenueId` | only when non-null |
| `gps_is_mock` | `Signals.IsMock` | always |
| `client_platform` | `Signals.ClientPlatform` | always |
| `gps_check_count` | `Session.CheckCount` | only when `Session != null` |
| `gps_start_lat`, `gps_start_lon`, `gps_end_lat`, `gps_end_lon` | from `Session` | only when `Session != null` AND the value is non-null |

`gps_verified` is a *request* to verify; the server re-derives it (`_verify_gps`) and zeroes it on mock. Do not add fields the Dart client does not send.

### 8. `Endpoints.cs` additions (`Assets/Scripts/Net/Endpoints.cs`)

Append one section, same style as the existing ones (doc comment per URL, `BaseUrl + …`, `UnityWebRequest.EscapeURL` for query values):

```csharp
// ── GPS / PLAYLIFE (gps_trust_core) ───────────────────────────────────
public static string VenueAutoRegister => BaseUrl + "/venue/auto-register";   // POST {latitude, longitude} → {data:{venue_id,name,latitude,longitude,distance_m,created}} | {data:null,message}
public static string VenueNearby(string prefixes, string languageCode = "ja");   // GET  → {data:[VenueDto]}
public static string VenueList(string languageCode = "ja");                       // GET
public static string VenueById(int venueId, string languageCode = "ja");          // GET /venue/{id}
public static string ScoreSubmit => BaseUrl + "/score/submit";                    // POST ScorePostRequest (owned by score_submit_flow; URL registered here so the attachment's consumer exists)
public static string ActivityCheckin => BaseUrl + "/activity/checkin";            // POST {venue_id, check_in_at?}
public static string ActivityCheckout(string activityId);                          // POST /activity/{id}/checkout {check_out_at?}
public static string ActivityCancel(string activityId);                            // POST /activity/{id}/cancel
public static string ActivityHistory(int skip = 0, int limit = 20);               // GET
```

### 9. DTOs — `GpsDtos.cs`

Newtonsoft, snake_case `[JsonProperty]`, one doc comment each citing the router line:

- `VenueAutoRegisterResult { venue_id (int), name, latitude (double?), longitude (double?), distance_m (double?), created (bool) }` — `venue.py:241-249` (existing) and the insert branch.
- `VenueDto { id (int), name, sport_type, latitude, longitude, geohash, address, gps_radius_m (double?), rating (double?), phone, place_id, source }` — columns per `GPS_INTEGRATION_REFERENCE.md` §5 `venues`; mark every field nullable except `id`/`name`. NOTE: `/venue/list` and `/nearby` do `select("*")`, so unknown extra columns must not break parsing (Newtonsoft ignores them by default — keep `MissingMemberHandling.Ignore`).
- `ActivityDto` — the `activities` row returned by `/activity/checkin` (`activity.py:44-60`): `id`, `user_id`, `venue_id`, `venue_name`, `sport_type`, `check_in_at`, `check_out_at`, `status`, plus the GPS columns from `score.py:210-228` as nullable.

No `ScoreSubmitRequest` DTO in this task (the 20-field body belongs to `score_submit_flow`).

### Strings

This task adds **no player-facing text**. The five `GPS_ERR_*` keys are named in `LocationFailReasonKeys` so the check-in screen spec can add the CSV rows (EN + JA, via `Tools/content/import_content.py --catalogs texts`, per `WORKFLOW_NOTES.md`). The JA copy to carry over then is in `current_location_notifier.dart:150-158`; the EN copy will be authored in that spec. **Do not add rows to `LocalizationText.csv` here** — an unused key would fail the exporter's orphan check for no benefit.

## Tests (EditMode, `Assets/Scripts/Gps/Tests/`)

Doubles in `GpsTestDoubles.cs`: `FakeLocationProvider` (scripted `LocationResult`, records the timeout it was asked for), `FakeClock` (settable `NowMs`), `FakeMockDetector`, `FakePlatformProbe`. Reuse `Golfin.Net.Tests.FakeHttpTransport` + `FakeAuthTokenProvider` for the HTTP seams (see the asmdef NOTE in Architecture).

`GpsSessionTrackerTests`:
1. **Throttle is AND, not OR:** second fix at +4 min and +50 m → dropped; at +4 min and +150 m → kept; at +6 min and +50 m → kept.
2. **Prune by age:** a fix at −13 h is dropped on the next `RecordFix`; one at −11 h survives.
3. **Prune by count:** 101 valid fixes → the OLDEST is dropped, 100 remain.
4. **SessionNear empty:** no fixes → `CheckCount == 1`, all coords null.
5. **SessionNear filters:** fixes at 6 km or 9 h ago are excluded; 4 km & 7 h ago included.
6. **Count uses last-counted, not previous:** fixes at t=0, +6 min, +12 min, +30 min → `CheckCount == 3` (0, +12, +30), NOT 4; start = fix at 0, end = fix at +30.
7. **Wire schema:** `InMemoryGpsFixStore` round-trip serialises `{"lat":…,"lon":…,"t":…}` exactly (assert the JSON string, not just equality after parse).
8. **Malformed store:** store returning `"not json"` → `Load()` empty, `RecordFix` still succeeds.
9. **Haversine:** Tokyo Station → Shinjuku Station (35.681236,139.767125 → 35.690921,139.700258) ≈ 6,134 m ± 20 m.

`GpsTrustSignalsTests`: `Capture` copies both seams; `FakePlatformProbe("ios-simulator")` + `NeverMockDetector` → `IsMock false`, `ClientPlatform "ios-simulator"`.

`GeohashTests`: Tokyo Station encodes to `xn76urx6606p` (12) / `xn76urx66` (9); `Encode(…,4) == "xn76"`; `Neighbors("xn76")` returns 8 distinct 4-char hashes; `NearbyPrefixes` contains `"xn76"` and 9 entries total.

`GpsScoreAttachmentTests` (drive the coroutine with `while (r.MoveNext()) {}` like `ApiClientTests`):
10. **Happy path:** fake location OK, transport returns `{"data":{"venue_id":42,"name":"Tokyo GC","distance_m":12.5,"created":false}}` → `ToJson()` has all 11 keys, `gps_verified == true`, `venue_id == 42`, `gps_check_count == 1` (fresh store), start/end == the fix; the POST body sent was exactly `{"latitude":…,"longitude":…}` (assert `SentBodies[0]` parses to exactly two keys).
11. **No course nearby:** transport returns `{"data":null,"message":"No golf course found nearby. Fall back to manual selection."}` → `Success`, `VenueId == null`, `gps_verified == false`, coords still present.
12. **Location failed:** `FakeLocationProvider` → `Timeout` → no HTTP call at all (`CallCount == 0`), `ToJson()` has exactly `gps_verified:false, gps_is_mock, client_platform` (3 keys), `PositionFailReason == Timeout`.
13. **Auto-register 500:** location OK, transport 500 ×3 (transient budget is for 408/connection only, so one 500 = one call) → `VenueId == null`, `gps_verified == false`, `Session` still populated, `gps_check_count` present.
14. **Timeout passed through:** default `Capture` asks the provider for 5 s, not 10.
15. **Second capture in the same session counts:** two `Capture`s 11 min apart on the same tracker/clock → second `ToJson()` has `gps_check_count == 2`.

Add a `VenueServiceTests` pair: `Nearby` builds `…/venue/nearby?prefixes=xn76%2Cxn77&language_code=ja` (escaped comma) and unwraps a two-element array.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] `Golfin.Gps.asmdef` references ONLY `Golfin.Net`; `grep -rn "using Golfin\." Assets/Scripts/Gps --include=*.cs` shows no `Golfin.Roster` / `Golfin.UI` / `GolfinRedux` usings; quote the grep.
- [ ] Every constant in §2 matches `gps_session_tracker.dart` — quote the Dart line and the C# line side by side for all seven.
- [ ] `ToJson()` key set equals the Dart `toJson()` key set and the `ScorePostRequest` field names in `score.py:117-140` — quote the three lists.
- [ ] EditMode: all tests in §Tests pass; full suite count before/after quoted (no pre-existing test broken).
- [ ] `Encode(35.681236, 139.767125, 9) == "xn76urx66"` AND the same input through `backend/routers/venue.py::_geohash_encode` (run it with `python3 -c`, precision 9) gives the same string — quote both.
- [ ] `PlayerPrefsGpsFixStore` writes under key `gps_session_fixes_v1` (quote the constant).
- [ ] `locationUsageDescription` is set in `ProjectSettings.asset` — quote the line.
- [ ] Platform probe verified on the iOS Simulator (`"ios-simulator"`) and — if a device is at hand — on hardware (`"ios"`); quote the raw `SystemInfo.deviceModel` strings. If no device pass was possible, say so explicitly (Cesar's standing rule: no device pass by default).
- [ ] Editor play mode: calling `GpsScoreAttachment.Capture(a => Debug.Log(a.ToJson()))` from a temporary Editor menu item (delete it before commit) logs a 3-key JSON with `client_platform:"editor"` and no exception; quote the log line.
- [ ] `Endpoints.VenueNearby("xn76,xn77","ja")` returns the URL in the test above (escaped).
- [ ] No new hardcoded player-facing `.text` literal (there is no UI in this task — state "n/a, no UI" with the grep quoted).
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Assets/Scripts/Gps/Golfin.Gps.asmdef` — NEW
- `Assets/Scripts/Gps/GpsFixStore.cs`, `GpsSessionTracker.cs`, `GpsTrustSignals.cs`, `LocationProvider.cs`, `Geohash.cs`, `VenueService.cs`, `GpsScoreAttachment.cs`, `GpsDtos.cs` — NEW
- `Assets/Scripts/Gps/Tests/Golfin.Gps.Tests.asmdef`, `GpsTestDoubles.cs`, `GpsSessionTrackerTests.cs`, `GpsTrustSignalsTests.cs`, `GeohashTests.cs`, `GpsScoreAttachmentTests.cs`, `VenueServiceTests.cs` — NEW
- `Assets/Scripts/Net/Endpoints.cs` — MODIFIED (one appended section, nothing existing changed)
- `ProjectSettings/ProjectSettings.asset` — MODIFIED only if `locationUsageDescription` is empty
- `Docs/AI_CONTEXT.md` — update at close-out (`Golfin.Gps` exists; what it provides; the seams left for the Android plugin and the check-in screen)

## Smoke evidence

EditMode test run (count + names), the four grep/quote items above, and the Editor play-mode log line. No visual-fidelity evidence applies (no UI). No device pass required for DONE (Cesar's rule); the simulator probe check is the one runtime item, and it is reportable from the Xcode simulator without a device.

## Out of scope (do NOT do these)

- Any screen, prefab, `ScreenManager` state, `ModalController`, nav entry, banner change — `gps_checkin_screen` / `score_submit_flow` own those.
- `ScoreSubmitRequest` / `POST /score/submit` call, `/recognition/*`, `/activity/*` service methods beyond the URLs (URLs only, so the next spec does not touch `Endpoints.cs` again).
- The Android mock-location native plugin (`IMockLocationDetector` is the seam; a `NeverMockDetector` ships).
- A native iOS permission probe distinguishing "services off" from "denied" (documented limitation).
- Adding `GPS_ERR_*` rows to `LocalizationText.csv` (next spec, with the UI that shows them).
- Maps of any kind (list-only v1 decision, 2026-09-01).
- Touching `RewardPointsManager`, `PointsService`, `AuthService`, or any existing test.
- Background location, significant-change monitoring, or any periodic fix recording — `RecordFix` is called only from `Capture` (explicit user action), matching the Dart app's J2 rule ("does not fetch on startup").
