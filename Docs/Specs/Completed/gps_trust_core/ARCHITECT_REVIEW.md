# Architect Review — `gps_trust_core`

**Verdict: ARCHITECT_REVIEW_PASS** (2026-09-01, Architect / Cowork session). Ready for Cesar's approval → `DONE`.

## What was checked against the source, not the report

- `GpsSessionTracker.cs` read line by line against `gps_session_tracker.dart`: the seven constants, the AND throttle (`gapMs < 5 min && moved < 100 m`), prune order (age first, then oldest-while->100), the `SessionNear` window/radius filter, the **last-counted** walk (not previous-fix), empty → `CheckCount 1`, haversine r = 6 371 000. Faithful.
- `GpsScoreAttachment.cs`: Dart order preserved (fetch → RecordFix → SessionNear → auto-register), every failure degrades and none aborts, no HTTP call without coordinates, `ToJson()` emits exactly the Dart key set with the same presence rules (`gps_verified` always, coords only with a position, `venue_id` only when resolved, session fields only when non-null).
- `UnityClientPlatformProbe`: iOS → hardware/simulator by `deviceModel` prefix, Android, Editor → `"editor"`, else `"unknown"`. As specified.
- Commit `d5366b06a`: 30 files under `Assets/Scripts/Gps`, `Endpoints.cs` append-only (54+ / 0−), `ProjectSettings.asset` one hunk. `Golfin.Gps.asmdef` references `Golfin.Net` only — the shell-readiness rule holds.
- EditMode 2185 / 2182 pass / 0 fail / 3 pre-existing skips; 39 new tests.

## Deviations — all four ACCEPTED

1. `VenueId` as `int?` — correct; the spec's `int` would have turned "no id" into venue 0 and a false `gps_verified: true`. The spec was wrong, the code is right.
2. `%2c` lowercase — same URL (RFC 3986 §6.2.2.1); test asserts what Unity produces.
3. Play-mode probe via `script-execute` instead of a throwaway menu item — better than the spec.
4. `ActivityDto` carrying `trust_level` + `points` — harmless, both nullable, saves the next spec a pass.

## The one FAIL — accepted as-is

iOS simulator/hardware `deviceModel` probe not executed at runtime. Consistent with the standing "no device pass by default" rule; the heuristic is unit-pinned; a wrong label can only cost Trust, never grant it. **Carried into the next spec as an acceptance item** (one boot-time `Debug.Log(SystemInfo.deviceModel + " → " + Label())` on the first simulator build of `gps_checkin_screen`).

## Answers to the open questions

1. **Simulator probe** — rides along with `gps_checkin_screen`. No separate build for one log line.
2. **Second `RecordFix` call site** — `gps_checkin_screen` owns it. Every explicit location read the player triggers on the GPS screens (check-in, "locate me" on GPS Proof, venue nearby) calls `GpsSessionTracker.Instance.RecordFix` — the same rule as the Dart `CurrentLocationNotifier.setCurrentLocation`. Still no background/periodic recording (J2). This is what makes `gps_check_count ≥ 3` (K4 +20) reachable in a real round: check-in at the clubhouse, a locate mid-round, the submit.

## Next

`gps_checkin_screen` — spec to follow (Figma: `GPS / PLAYLIFE` page, GPS Proof + Hub Home frames).
