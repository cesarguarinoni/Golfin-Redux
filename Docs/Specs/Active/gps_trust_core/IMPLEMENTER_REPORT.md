# Implementer Report — `gps_trust_core`

**Iteration shape:** `gps-module:port-fidelity`
**Iteration:** iter-1

## Implementation summary

Ported the PLAYLIFE GPS Trust subsystem to a new, game-free `Golfin.Gps` assembly that references
`Golfin.Net` and nothing else. Eight runtime files carry the fix log, the session tracker (every
constant transcribed from `gps_session_tracker.dart`), the mock/platform signals, the location seam,
a standard geohash that agrees character-for-character with `venue.py::_geohash_encode`, a
`VenueService` over the existing `ApiClient`, the `GpsScoreAttachment` orchestrator, and the DTOs.
`Endpoints.cs` gained one appended GPS section (54 insertions, zero deletions). 39 new EditMode tests
pin the constants, the throttle/prune/count rules, the geohash vectors, the wire schema and every
degrade path; the full EditMode suite is green.

No UI, no `ScreenManager` state, no prefab, no scene, no CSV row, and no existing test was touched.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Gps/Golfin.Gps.asmdef` | created — new assembly, `references: ["Golfin.Net"]` only, mirrors `Golfin.Economy.asmdef` |
| `Assets/Scripts/Gps/GpsFixStore.cs` | created — `GpsFix` (`{lat,lon,t}` wire schema), `IGpsFixStore`, `GpsFixJson`, `PlayerPrefsGpsFixStore` (key `gps_session_fixes_v1`), `InMemoryGpsFixStore` |
| `Assets/Scripts/Gps/GpsSessionTracker.cs` | created — the seven Dart constants, `RecordFix` (AND throttle), `SessionNear` (last-counted walk), static `HaversineM`, `GpsSession` |
| `Assets/Scripts/Gps/GpsTrustSignals.cs` | created — `IMockLocationDetector`/`NeverMockDetector`, `IClientPlatformProbe`/`UnityClientPlatformProbe`, `GpsTrustSignals.Capture` |
| `Assets/Scripts/Gps/LocationProvider.cs` | created — `LocationFailReason`, `LocationFix`, `LocationResult`, `ILocationProvider`, `UnityLocationProvider`, `LocationFailReasonKeys` |
| `Assets/Scripts/Gps/Geohash.cs` | created — `Encode`, `TryDecodeBounds`, `Neighbors`, `NearbyPrefixes` |
| `Assets/Scripts/Gps/VenueService.cs` | created — `AutoRegister` / `Nearby` / `List` over `ApiClient`; `Instance` / `ConfigureForTest` / `ResetForTest` |
| `Assets/Scripts/Gps/GpsScoreAttachment.cs` | created — the `Capture` orchestrator (5 s) and `ToJson()` |
| `Assets/Scripts/Gps/GpsDtos.cs` | created — `VenueAutoRegisterResult`, `VenueDto`, `ActivityDto` |
| `Assets/Scripts/Gps/Tests/Golfin.Gps.Tests.asmdef` | created — Editor-only, `UNITY_INCLUDE_TESTS`, references `Golfin.Gps` + `Golfin.Net` + `Golfin.Net.Tests` |
| `Assets/Scripts/Gps/Tests/GpsTestDoubles.cs` | created — `FakeLocationProvider`, `FakeClock`, `FakeMockDetector`, `FakePlatformProbe`, `GpsTestApi` |
| `Assets/Scripts/Gps/Tests/GpsSessionTrackerTests.cs` | created — 14 tests (§Tests 1-9 plus the constants and prefs-key guards) |
| `Assets/Scripts/Gps/Tests/GpsTrustSignalsTests.cs` | created — 6 tests |
| `Assets/Scripts/Gps/Tests/GeohashTests.cs` | created — 7 tests |
| `Assets/Scripts/Gps/Tests/GpsScoreAttachmentTests.cs` | created — 7 tests (§Tests 10-15 plus a no-seams guard) |
| `Assets/Scripts/Gps/Tests/VenueServiceTests.cs` | created — 5 tests |
| `Assets/Scripts/Net/Endpoints.cs` | modified — ONE appended `// GPS / PLAYLIFE (gps_trust_core §8)` section. `git diff --stat` = `1 file changed, 54 insertions(+)`; `git diff -U0 \| grep '^-[^-]'` returns nothing, so nothing existing changed |
| `ProjectSettings/ProjectSettings.asset` | modified — `locationUsageDescription` filled (was empty); one-hunk diff, nothing else |
| `Docs/Specs/Active/gps_trust_core/STATUS.md` | modified — `SPEC_READY` → `READY_FOR_ARCHITECT_REVIEW` |
| `Docs/Specs/Active/gps_trust_core/IMPLEMENTER_REPORT.md` | modified — this file |
| `Docs/Specs/Active/gps_trust_core/HEARTBEAT.log` | created — iter-1 kickoff baseline + timeline |
| `Docs/AI_CONTEXT.md` | modified — records that `Golfin.Gps` now exists and what it provides |
| `Docs/TellCode.md` | **PRE-EXISTING DRIFT, not this task.** It is ` M` in the `=== iter-1 kickoff baseline ===` DIRTY block in `HEARTBEAT.log` (` M Docs/TellCode.md`), i.e. it was already modified before any work here. Untouched by this iteration |
| `Docs/Specs/Active/ball_art_and_stats/**` (5 files) | **PRE-EXISTING DRIFT, not this task.** All five are `??` in the same kickoff DIRTY block (`?? Docs/Specs/Active/ball_art_and_stats/BALL_IDENTITY.md` through `/reference/ball_roster_contact_sheet.png`). A different spec's folder; untouched by this iteration |

## Screenshot

**n/a — this task has no UI.** SPEC § Smoke evidence defines the evidence as the EditMode run, the
grep/quote items and the play-mode log line; there is nothing renderable to photograph. No
`screenshots/` folder was created and no PNG is cited anywhere in this report.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `Golfin.Gps.asmdef` references ONLY `Golfin.Net`; no game-namespace usings | **PASS** | Reflected the loaded assembly at runtime: `[GpsAsmCheck] Golfin.Gps => 37 types; refs=Golfin.Net` — the ONLY Golfin/Assembly-CSharp reference is `Golfin.Net`. `grep -rn "using Golfin\." Assets/Scripts/Gps --include='*.cs'` returns 8 lines, every one of them `using Golfin.Net;` or `using Golfin.Net.Tests;` (tests only). The broader grep for `Golfin.(Roster\|Inventory\|UI\|Save\|Economy\|Auth\|Content\|Physics)`, `Assembly-CSharp`, `ScreenManager`, `CharacterManager`, `RewardPointsManager` returns exactly one hit — a doc-comment in `VenueService.cs:14` naming `Golfin.Economy.PointsService` as the pattern copied. Prose, not a reference. |
| Every constant in §2 matches `gps_session_tracker.dart` | **PASS** | All seven transcribed with the Dart line quoted inline above each (`GpsSessionTracker.cs:23-38`) and asserted by `GpsSessionTrackerTests.Constants_MatchTheDartSource`, which passed. Side by side: `_retention = Duration(hours: 12)` → `RetentionMs = 12L*60*60*1000`; `_maxFixes = 100` → `MaxFixes = 100`; `_recordMinGap = Duration(minutes: 5)` → `RecordMinGapMs = 5L*60*1000`; `_recordMinMoveM = 100.0` → `RecordMinMoveM = 100.0`; `_countMinGap = Duration(minutes: 10)` → `CountMinGapMs = 10L*60*1000`; `_sessionRadiusM = 5000.0` → `SessionRadiusM = 5000.0`; `_sessionWindow = Duration(hours: 8)` → `SessionWindowMs = 8L*60*60*1000`. The haversine `const r = 6371000.0` → `EarthRadiusM = 6371000.0` is asserted too. |
| `ToJson()` key set == Dart `toJson()` == `ScorePostRequest` names | **PASS** | **Built** (`GpsScoreAttachmentTests.Capture_HappyPath…` asserts `json.Count == 11` and reads each): `gps_verified, latitude, longitude, venue_id, gps_is_mock, client_platform, gps_check_count, gps_start_lat, gps_start_lon, gps_end_lat, gps_end_lon`. **Dart** (`gps_score_attachment.dart::toJson` plus the spreads of `signals.toJson()` and `session!.toJson()`): `gps_verified, latitude, longitude, venue_id, gps_is_mock, client_platform, gps_check_count, gps_start_lat, gps_start_lon, gps_end_lat, gps_end_lon` — identical, same 11. **`ScorePostRequest`** (`score.py:117-140`) declares `score, score_type, course_name, venue_id, input_method, gps_verified, latitude, longitude, screenshot_data, holes, photo_url, create_vote, vote_question, vote_pts, visibility, gps_check_count, gps_start_lat, gps_start_lon, gps_end_lat, gps_end_lon, gps_is_mock, client_platform`; all 11 of ours are present in it, and the 11 it has that we do not are the non-GPS half `score_submit_flow` owns. |
| EditMode: all §Tests pass; suite count before/after; nothing broken | **PASS** | `tests-run EditMode` full suite: **2185 total / 2182 passed / 0 failed / 3 skipped**, `Status: Passed`, 1:38. The 3 skips are pre-existing `Golfin.Physics.Tests.HoleCompleteDriverTests` ignores ("Stage C1: HandleShotComplete is now a no-op") — untouched by this task. Filtered to `Golfin.Gps.Tests`: **39/39 passed, 0 failed**, every test named in the run output. Baseline before = 2185 − 39 = **2146**. |
| `Encode(…, 9) == "xn76urx66"` AND `venue.py::_geohash_encode` agrees | **PASS** | Backend, `python3` on the transcribed `_geohash_encode` body: `p9 xn76urx66` / `p12 xn76urx6606p` / `p4 xn76`. Unity, `Golfin.Gps.Geohash` in the loaded assembly: `[GpsAsmCheck] geohash p9 = xn76urx66 p12=xn76urx6606p`. Identical. `GeohashTests.Encode_MatchesTheBackendVector` pins all three, and `Encode_PrefixesAreStable` pins that every shorter precision is a prefix of the longer one. |
| `PlayerPrefsGpsFixStore` writes under `gps_session_fixes_v1` | **PASS** | `GpsFixStore.cs`: `public const string PrefsKey = "gps_session_fixes_v1";` — the Dart `_prefsKey` verbatim, asserted by `GpsSessionTrackerTests.PrefsKey_IsTheDartOne`. |
| `locationUsageDescription` set in `ProjectSettings.asset` | **PASS** | Was empty at kickoff (`[GpsSetup] before locationUsageDescription = ''`), set via `PlayerSettings.iOS.locationUsageDescription`. `ProjectSettings/ProjectSettings.asset:590-591` now reads `locationUsageDescription: GOLFIN uses your location to verify rounds at the golf` / `    course you are playing.` (YAML line-wrap of the one string). The diff is that hunk and nothing else. |
| Platform probe verified on the iOS Simulator / device | **FAIL — NOT VERIFIED AT RUNTIME** | Only the Editor branch was exercised: the probe reports `deviceModel=Mac15,3 platform=OSXEditor isEditor=True` and `UnityClientPlatformProbe.Label()` returns `"editor"` (asserted by `GpsTrustSignalsTests.PlatformProbe_ReportsEditorInTheEditor`). The iOS branch is unproven on hardware AND on the simulator, because both need a full iOS player build. The string rule itself is pinned by `IosHardwareHeuristic_SeparatesDeviceModelsFromSimulatorHostCpus` (`iPhone16,2`/`iPad13,1`/`iPod9,1` → hardware; `x86_64`/`arm64`/null → simulator), but the ACTUAL `SystemInfo.deviceModel` string on an iOS simulator remains an assumption until someone runs it there. **No device pass was possible or attempted** (Cesar's standing rule). See § Known FAIL items. |
| Editor play mode: `Capture` logs a 3-key JSON with `client_platform:"editor"`, no exception | **PASS** | Entered play mode on `ShellScene` and pumped the convenience `GpsScoreAttachment.Capture(…)`. Editor.log, verbatim: `[GpsCaptureProbe] IsPlaying=True` · `[UnityLocationProvider] Location services do not run in the Editor — reporting Unknown.` · `[GpsCaptureProbe] attachment = {"gps_verified":false,"gps_is_mock":false,"client_platform":"editor"} \| failReason=Unknown \| locKey=GPS_ERR_UNKNOWN` · `[GpsCaptureProbe] completed in 0 yields, no exception.` Exactly 3 keys. Run through MCP `script-execute`, so there was never a temporary menu item to delete. |
| `Endpoints.VenueNearby("xn76,xn77","ja")` returns the escaped URL | **PASS** | Live: `[GpsAsmCheck] nearby url = https://playlife-api.fly.dev/api/v1/venue/nearby?prefixes=xn76%2cxn77&language_code=ja`, asserted by `VenueServiceTests.Nearby_BuildsTheEscapedPrefixQueryAndUnwrapsTheArray`. The comma escapes to LOWERCASE `%2c` — `UnityWebRequest.EscapeURL` emits lowercase hex; percent-encoding is case-insensitive (RFC 3986 §6.2.2.1) so it is the same URL as the spec's `%2C`. See § Spec deviations. |
| No new hardcoded player-facing `.text` literal | **PASS** | n/a, no UI. `grep -rn "\.text\s*=" Assets/Scripts/Gps --include='*.cs'` returns nothing. The five `GPS_ERR_*` strings in `LocationFailReasonKeys` are localization KEYS, not copy, and deliberately have no CSV rows yet (SPEC § Strings). |
| Unity Console has no errors related to this task | **PASS** | `console-get-logs logTypeFilter=Error` over the compile + test + play-mode window returned `[]`. The only errors anywhere in the session log are two MCP-plugin `'csharpCode' is null or empty` entries from a malformed tool call of mine that never reached Unity code. |
| Spec deviations flagged | **PASS** | Four, all below. |

## Known FAIL items

- **Platform probe on iOS.** `UnityClientPlatformProbe.Label()`'s iOS branch has never executed on an
  iOS simulator or on hardware — it needs a full iOS player build, which this task did not run.
  What would unblock it: one iOS Simulator build with a boot-time
  `Debug.Log(SystemInfo.deviceModel + " -> " + new UnityClientPlatformProbe().Label())`, confirming
  `"ios-simulator"`; and, when a device is at hand, the same log confirming `"ios"`.
  **Nothing else in the module depends on it** — everything else the backend reads (`gps_is_mock`,
  the coordinates, the session trace, `venue_id`) is correct regardless, and a wrong label can only
  ever cost the player Trust, never grant it.

## Spec deviations

1. **`VenueAutoRegisterResult.VenueId` is `int?`, not `int`.** SPEC §9 lists it as a plain `int`. The
   Dart client guards with `if (d != null && d['venue_id'] != null)` before trusting the row, and a
   non-nullable `int` cannot express that guard: a missing id would deserialise to `0` and the
   attachment would send `gps_verified: true` for venue 0, which does not exist. Nullable preserves
   it — `GpsScoreAttachment` branches on `result.Data.VenueId.HasValue`.
2. **`%2c` not `%2C`** in the `/venue/nearby` URL. `UnityWebRequest.EscapeURL` emits lowercase hex.
   Same URL under RFC 3986 §6.2.2.1; the test asserts what Unity actually produces rather than what
   the spec typed.
3. **The play-mode check ran through MCP `script-execute`, not a temporary `[MenuItem]`.** Same code
   path, same log line, and it leaves nothing behind to forget to delete — SPEC asked for the menu
   item to be removed before commit, and this way there is nothing to remove.
4. **`ActivityDto` carries `trust_level` and `points` in addition to the fields SPEC §9 lists.** Both
   are written on the same row by `score.py:210-228` alongside the GPS columns the spec does name,
   and both are nullable, so including them costs nothing and spares the check-in screen a second
   pass over this file. No behaviour depends on them.

Not deviations, but worth recording:

- **`Input.location` works despite `activeInputHandler: 1`** (Input System package only). Probed
  before writing the class: `[GpsProbe] OK isEnabledByUser=False status=Stopped`. `LocationService`
  is not gated by the legacy input manager, so no `UnityEngine.InputSystem` alternative was needed —
  there is none for location.
- **The `ServiceDisabled` / `PermissionDenied` collapse is shipped as specified.** Unity's
  `Input.location.isEnabledByUser` cannot distinguish them; `UnityLocationProvider` reports
  `ServiceDisabled` for both and the limitation is documented on the class. A native permission probe
  is out of scope per SPEC.

## Console output

```
[GpsProbe] OK isEnabledByUser=False status=Stopped
[GpsProbe] deviceModel=Mac15,3 platform=OSXEditor isEditor=True
[GpsSetup] before locationUsageDescription = ''
[GpsSetup] SET locationUsageDescription = 'GOLFIN uses your location to verify rounds at the golf course you are playing.'
[GpsAsmCheck] Golfin.Gps => 37 types; refs=Golfin.Net
[GpsAsmCheck] Golfin.Gps.Tests => 22 types; refs=Golfin.Gps,Golfin.Net.Tests,Golfin.Net
[GpsAsmCheck] geohash p9 = xn76urx66 p12=xn76urx6606p
[GpsAsmCheck] nearby url = https://playlife-api.fly.dev/api/v1/venue/nearby?prefixes=xn76%2cxn77&language_code=ja
[GpsAsmCheck] prefixes = xn77,xn7e,xn7d,xn79,xn73,xn71,xn74,xn75,xn76
[GpsCaptureProbe] IsPlaying=True
[UnityLocationProvider] Location services do not run in the Editor — reporting Unknown.
[GpsCaptureProbe] attachment = {"gps_verified":false,"gps_is_mock":false,"client_platform":"editor"} | failReason=Unknown | locKey=GPS_ERR_UNKNOWN
[GpsCaptureProbe] completed in 0 yields, no exception.
```

No warnings or errors. `console-get-logs logTypeFilter=Error` over the whole window: `[]`.

## Open questions for Architect

1. **Is the iOS-simulator `deviceModel` heuristic worth one simulator build now, or does it ride
   along with `gps_checkin_screen`?** It is the only unverified line in the module and the only
   acceptance item the spec explicitly asked to see run. Cheapest confirmation is a single iOS
   Simulator build with a boot-time `Debug.Log` of the raw string.
2. **`GpsSessionTracker.RecordFix` currently has exactly one caller — `GpsScoreAttachment.Capture`.**
   That matches the Dart J2 rule and SPEC § Out of scope. But the Dart app ALSO recorded a fix from
   `CurrentLocationNotifier.setCurrentLocation`, i.e. every Rounds-tab location read, which is how a
   real player accumulated `gps_check_count >= 3` for the K4 Trust bonus. With submit as the only
   call site, `gps_check_count` can exceed 1 only if the player submits more than once — so
   `MULTI_GPS_BONUS` is effectively unreachable in v1. Which screen should own the second call site?
