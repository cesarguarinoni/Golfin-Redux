# Implementer Report — `beta_telemetry`

> Built directly by the main Claude Code thread at Cesar's request (not via the subagent pipeline).
> No UI, no Figma node, no prefab → Rules 14/15/17/18/19/21 (screenshot / rejection / video /
> Figma fidelity / clone provenance / UI lint) are N/A and their sections are deleted per the
> template's own instruction.

## Implementation summary

A batching telemetry pipeline for the TestFlight beta: 12 of the 13 specced events, hooked
entirely onto signals that already existed, plus a new authenticated `POST /api/v1/telemetry/events`
on playlife-api backed by a new `telemetry_events` table. The client is a plain-C# queue
(`TelemetryService`) that flushes at 20 events / 30s / pause / quit through the existing
`ApiClient` — no new retry, auth or envelope logic — with a 500-event drop-oldest cap and exactly
one re-enqueue on failure. Every hook body runs inside `RecordSafe`, so a telemetry bug costs one
row and can never reach a shot.

**The migration has NOT been applied yet** — that is a Cesar step (see § Blocked, below), and the
endpoint must not be deployed before it lands.

## Files modified or created

### Unity — `~/Documents/GolfinRedux`

| Path | Change |
|---|---|
| `Assets/Scripts/Telemetry/Golfin.Telemetry.asmdef` | **created** — new assembly for the testable core (see § Spec deviations #1). |
| `Assets/Scripts/Telemetry/TelemetryConfig.cs` | **created** — tuning constants, event-name constants, and the Editor send gate. |
| `Assets/Scripts/Telemetry/TelemetryService.cs` | **created** — the queue: record, cap, batch, flush, single re-enqueue, auth gate, exception cap/dedupe, batch JSON. |
| `Assets/Scripts/Telemetry/TelemetryBehaviour.cs` | **created** — self-bootstrapping DontDestroyOnLoad host: flush clock, allocation-free FPS sampling, pause/quit → `session_end`. |
| `Assets/Scripts/Telemetry/Tests/Golfin.Telemetry.Tests.asmdef` | **created** — EditMode test assembly. |
| `Assets/Scripts/Telemetry/Tests/TelemetryServiceTests.cs` | **created** — 17 EditMode tests covering SPEC §5.1–§5.7. |
| `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs` | **created** — Assembly-CSharp glue; the single place every event is wired to an existing signal. |
| `Assets/Scripts/Gameplay/UI/ShotUI/ShotTelemetryRelay.cs` | **created** — assembly bridge re-raising the two ShotController signals where Assembly-CSharp can see them. |
| `Assets/Scripts/Net/Endpoints.cs` | **modified** — added `TelemetryEvents`; nothing else touched. |
| `Assets/Scripts/Gameplay/Input/ShotController.cs` | **modified** — two `public static` events declared + raised (one line each in `EndExternalDrag` / `CancelExternalDrag`). No behaviour change. |
| `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` | **modified** — `OnRoundStarted` event + a wrapped raise from `SeedSession` AND `SetCurrentHole` (see § Spec deviations #2). |
| `Docs/Specs/Active/beta_telemetry/reference/EVENT_CATALOG.md` | **modified** — appended an as-built deltas section for `telemetry_admin_panel` to read. |

### Backend — `~/Documents/playlife`

| Path | Change |
|---|---|
| `backend/migrations/2026_08_18_telemetry_events.sql` | **created** — `telemetry_events` table, 3 indexes, RLS on with no policies. **NOT YET APPLIED.** |
| `backend/routers/telemetry.py` | **created** — `POST /events`, `Depends(get_current_user)`, upsert-ignore on `event_id`, per-event lenient validation. |
| `backend/main.py` | **modified** — import + `include_router(telemetry.router, prefix="/api/v1/telemetry")`. |

## NOTE flags resolved against the codebase

Every NOTE in the spec, and what the code actually says.

| SPEC NOTE | Finding | Outcome |
|---|---|---|
| §1 #8 — par accessor | `Golfin.Gameplay.UI.HUD.HoleContext.Par` (`Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs:7`) is a plain static int in `Golfin.Gameplay.UI`, which is `autoReferenced: true` — directly visible from the hooks assembly. It is the same value `HoleCompleteModalController.cs:158` and `HoleCardWidget.cs:100` read. | **`par` IS emitted.** No lookup invented. |
| §1 #9 — is `ResetSession` a clean abandon choke point? | `GameSession.ResetSession()` has **zero production call sites** — every hit outside `GameSession.cs` is in a test file. It never fires in a real session, clean or otherwise. | **Used the `ScreenChanged` approach** the spec specified as primary. |
| §1 #13 — SP commit call site | Dead path. `StatAllocationStrategy.AllocateSP` is never invoked: `CharacterManager.cs:53` constructs a `ManualSPAllocation` into `allocationStrategy` and nothing ever calls it. `ConfirmPendingSP()` / `pendingSpent*` likewise have no callers outside `PlayerCharacterData` itself. There is no call site, clean or scattered. | **`sp_allocated` SKIPPED and flagged** — exactly the spec's stated fallback. 12 of 13 events ship. |
| §2.1 — build_number accessor | `GolfinRedux.UI.BuildInfo.AppVersion.BuildNumber` (`Assets/Scripts/UI/BuildInfo/AppVersion.cs`) reads the parenthesised int out of the baked `Resources/Data/build_stamp.txt`. It is **not** behind `GOLFIN_TESTBUILD` (only `BuildStamp.cs`, the on-screen label, is), so it is runtime-readable in any build. | **Used `AppVersion.BuildNumber`.** No `Application.version` fallback needed for the build number; `Application.version` supplies `app_version` as specced. |
| §3.4 — JSON serializer | Newtonsoft **is** available to Assembly-CSharp: `Assets/Scripts/BannersRuntime/BannerService.cs` is an Assembly-CSharp file that does `using Newtonsoft.Json;` today. The spec's "if the existing pattern is JsonUtility" premise does not hold. | **`JsonConvert.SerializeObject`.** No hand-rolled JSON writer — that branch of the NOTE was unnecessary. |
| §3 — boot choke point | The house pattern is `[RuntimeInitializeOnLoadMethod]`, used by `AuthService.cs:41` (AfterSceneLoad), `ServerBalanceSyncBehaviour.cs:51`, `BuildStamp.cs:40`, `FramePacingBootstrap.cs:31`. No shared bootstrap class exists to hook into. | **`TelemetryHooks.Install()` is `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`.** No scene edit, no prefab, no execution-order dependency. |
| § Architecture — relay | Confirmed as the spec stated: `Golfin.Gameplay.Input` is `autoReferenced: false`; `Golfin.Gameplay.UI` references it and is `autoReferenced: true`. | **Relay built as specced.** Input's flag untouched. |

## Acceptance checklist (SPEC §5)

| Item | Result | Justification |
|---|---|---|
| §5.1 — 20 queued events trigger a flush; batch JSON contains all 20 with distinct `event_id`s | PASS | `TwentyEvents_TriggerFlush_WithDistinctEventIds`: sender called exactly once, `events.Count == 20`, all 20 `event_id`s added to a `HashSet` without collision, queue drained to 0. Companion `NineteenEvents_DoNotFlush` proves the threshold is not off-by-one. |
| §5.2 — timer flush at 30s with <20 events | PASS | `TimerFlush_FiresAtInterval_WithPartialBatch`: 3 events, `Tick(29.5f)` sends nothing, `Tick(1f)` sends one batch of 3. `TimerTick_WithEmptyQueue_SendsNothing` proves an idle timer does not post empty batches. |
| §5.3 — queue cap: 501st event drops the oldest, count stays 500 | PASS | `QueueCap_DropsOldest_AndHoldsAtCap`: count is 500 before and after the 501st `Record`, and the subsequent flush's first event has `payload.i == 1` — proving `i=0` (the oldest) was the one evicted, not the newest. |
| §5.4 — failed flush re-enqueues once with the SAME event_ids; second failure drops | PASS | `FailedFlush_ReEnqueuesOnce_ThenDropsOnSecondFailure`: after failure #1 queue is back to 20; the retry's ids are `CollectionAssert.AreEquivalent` to the first attempt's; after failure #2 queue is 0 and the "Dropped 20 event(s)" warning is asserted via `LogAssert.Expect`. |
| §5.5 — unauthenticated: nothing sent; `SignedIn` triggers flush | PASS | `Unauthenticated_SendsNothing_ThenFlushesOnceAuthenticated`: 20 events queued with the auth predicate false → 0 sends, 20 still queued; flipping it true and calling `Flush()` (what the `AuthService.SignedIn` handler does) sends all 20. `AuthPredicateThatThrows_IsTreatedAsUnauthenticated_NotAsACrash` covers the throwing case. |
| §5.6 — hook safety: a throwing payload builder is swallowed, does not propagate | PASS | `ThrowingPayloadBuilder_IsSwallowed_AndQueuesNothing`: `Assert.DoesNotThrow` around a builder that throws `NullReferenceException`; warning logged, queue stays 0 (no partial event). `ThrowingScreenProvider_DoesNotBreakErrorRecording` covers the other injected delegate. |
| §5.7 — client_error cap: 11th exception in a session is not enqueued | PASS | `ClientError_CapsAtTenPerSession`: queue holds at 10 after an 11th distinct exception. Plus `ClientError_DedupesByMessageAndFirstStackLine` (two exceptions, same message + same first stack line → 1 row) and `ClientError_TruncatesMessageAndStack` (300/2000 char caps measured on the serialised payload). |
| §5.8 — POST with valid token + 2 events → `{"data":{"accepted":2,...}}`, rows visible via REST probe | PASS | Live against `playlife-api.fly.dev`: `HTTP 200 {"data":{"accepted":2,"duplicates":0,"rejected":0}}`. REST probe returned both rows with `user_id` = the token's `8e7f96ed-…`, `build_number` 2201, `device_model` denormalised onto each row, and the `shot_taken` payload intact including its `null` `ob_reason`. |
| §5.9 — replay same body → `accepted:0, duplicates:2`, no double rows | PASS | Identical body re-POSTed: `HTTP 200 {"data":{"accepted":0,"duplicates":2,"rejected":0}}` — 200, not 500. REST probe still returned exactly 2 rows, so the `event_id` unique index + `ignore_duplicates` upsert did the work. |
| §5.10 — no/invalid token → 401/403 not 500; 101 events → 413 | PASS | No token → `403 {"detail":"Not authenticated"}`; malformed JWT → `401 … token is malformed`; well-formed-but-bad-signature JWT → `401 … signature is invalid`. 101 events → `413 {"detail":"Batch too large: 101 events (max 100)"}`, and exactly 100 → `200 accepted:100`, proving the boundary is at 101 not 100. `fly logs` grep for `traceback|internal server error` = 0. |
| §5.11 — on-device: play one hole signed in, verify rows in Supabase | **MANUAL (Cesar)** | Requires a TestFlight/device build. Listed under § Manual verification required. |
| Never blocks or throws into gameplay (SPEC §3 rule 1) | PASS | Every public entry (`Record`, `RecordSafe`, `RecordException`, `Flush`, `Tick`) has a top-level try/catch; `RecordSafe` runs the payload builder *inside* its own catch; the two raise sites (`GameSession.RaiseRoundStarted`, `ShotTelemetryRelay`) wrap the invoke. Proven by §5.6 and by 1334/1334 EditMode tests passing after the two gameplay-file edits. |
| Queue capped at 500, one re-enqueue then drop (SPEC §3 rule 2) | PASS | §5.3 and §5.4 above. Batches are additionally capped at `MaxEventsPerBatch = 100` so the client can never build a body the server 413s. |
| No new retry/auth layer on top of ApiClient | PASS | `TelemetryService.PostBatch` is a single `api.Run(api.Post<TelemetryAck>(...))`. Bearer, envelope unwrap, transient retry and 401-refresh-replay are all inherited; grep of the Telemetry folder shows no retry loop, no token handling, no `{data:}` parsing. |
| Only ~4 one-line insertions into existing files | PASS | 5 lines across 3 files: `Endpoints.cs` (+1 property), `ShotController.cs` (+2 raises), `GameSession.cs` (+2 raises). Everything else is new files. |

## Test evidence

Unity EditMode, run via `mcp__ai-game-developer__tests-run`:

```
Golfin.Telemetry.Tests   17 passed, 0 failed      (17 == the [Test] count in the file)
Golfin.Gameplay.Tests   302 passed, 0 failed      (regression: GameSession edit)
Golfin.Net.Tests         18 passed, 0 failed      (regression: Endpoints edit)
FULL EditMode suite    1334 passed, 0 failed, 3 skipped   (00:01:04)
```

The 3 skips are pre-existing `HoleCompleteDriverTests` ignores carrying their own
"Stage C1: HandleShotComplete is now a no-op" messages — unrelated to this task and
present before it.

Backend pure-helper verification (`_uuid_or_none` / `_clip` / `_parse_ts` / payload sizing,
executed against the real module source since fastapi is not installed locally):

```
uuid passthrough / reject-malformed / reject-None   PASS
clip under / over / None                            PASS
ts "2026-08-18T02:38:14.565Z"  (the client's exact emitted format)   PASS
ts naive → UTC, ts unparseable → None               PASS
payload just under 4KB accepted, just over rejected PASS
realistic shot_taken payload                        well under cap
maxed client_error (300-char msg + 2000-char stack) well under cap
```

That last row is the one that matters for consistency: the largest event the client is
*capable* of emitting still fits the server's 4KB per-event limit, so the two caps agree.

## Spec deviations

1. **A new asmdef (`Golfin.Telemetry`) rather than putting everything in Assembly-CSharp.**
   The spec says "New folder `Assets/Scripts/Telemetry/` (Assembly-CSharp)" and "Asmdef
   boundaries affected: none changed". Taken literally that makes SPEC §5 impossible: a test
   asmdef cannot reference Assembly-CSharp (it is a predefined assembly), so a service living
   there has no EditMode coverage at all. Split instead: the testable core is
   `Golfin.Telemetry` (`autoReferenced: true`, so Assembly-CSharp still sees it exactly as the
   spec assumed), and the glue that genuinely needs Assembly-CSharp types (`ScreenManager`,
   `CharacterManager`, `RewardPointsManager`, `AppVersion`) is `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs`
   — the same `*Runtime` naming `BannersRuntime` / `EconomyRuntime` / `TournamentsRuntime` already use.
   No existing asmdef was modified.

2. **`round_start` also fires from `GameSession.SetCurrentHole`, not only `SeedSession`.**
   `SeedSession` runs once when a session is seeded; the PLAY NEXT path
   (`HoleCompleteModalController.cs:328`) starts every subsequent hole through `SetCurrentHole`.
   Hooking only `SeedSession` would emit `round_start` for hole 1 and nothing after it — and
   would also leave `RoundActive` stale, breaking `round_abandoned` and FPS sampling for holes
   2+. Deliberately NOT hooked into `ResetForNewHole`, which `ResetSession` also calls: a
   teardown is not a round start.

3. **`round_abandoned` also treats `TournamentHoleSelection` / `TournamentSelection` as menu
   screens**, alongside the specced Home / HoleSelection / ModeSelection. Abandoning a
   tournament round returns to a tournament screen, so the specced list would silently miss
   exactly the funnel drop-off the event exists to measure.

4. **The ack carries a third field, `rejected`.** Per-event validation is lenient by design
   (a malformed event is dropped, the rest of the batch still lands) — without a count, that
   silent drop would be invisible. `accepted` / `duplicates` are unchanged, so the specced
   response shape is a strict subset.

5. **`points_changed.delta` is `null` on the first callback of a session**, not `0` and not
   the balance. There is no previous value to diff against on the first fire, and reporting
   `delta == balance` would read as a large phantom grant in the panel.

6. **`sp_allocated` is not implemented.** Per SPEC §1 #13's own instruction — see the NOTE
   table above for the evidence that the call site does not exist.

## Known FAIL items

None. All EditMode (§5.1–§5.7) and all backend (§5.8–§5.10) acceptance items PASS.
Only §5.11 (on-device) remains, which needs a TestFlight build.

## Deployment record (completed 2026-08-18)

Executed in the mandated order (ADMIN_DASHBOARD_OPS §3.2 — migration first, deploy second):

1. **Migration applied by Cesar** in the Supabase SQL editor.
2. **Verified before deploying.** REST probe went `404 PGRST205` → `200 []`. Then every column
   checked BY NAME (the runbook's "dump the column list and check by name"): all 13 present
   (`id, event_id, user_id, session_id, name, ts, received_at, app_version, build_number,
   platform, device_model, os, payload`), with `select=bogus_col` → `400` as a tripwire proving
   the check was capable of failing rather than rubber-stamping.
3. **RLS verified holding**, not just enabled: with the ANON key, read → `200 []` (no rows
   visible) and insert → `401 {"code":"42501","message":"new row violates row-level security
   policy"}`. Testers cannot read or forge rows through PostgREST.
4. **`fly deploy`** (app `playlife-api`, region nrt), launched per §4.6's `nohup` note.
   `/health` → `200 {"status":"ok","version":"0.1.0"}`; `openapi.json` lists
   `/api/v1/telemetry/events`.
5. **Acceptance curls §5.8–§5.10 run live** — see the checklist above.

### Extra properties proven live (beyond the spec's list)

| Property | Evidence |
|---|---|
| A `user_id` in the request body is IGNORED, never honoured | POSTed a batch carrying `"user_id":"…666"`; the stored row's `user_id` is `8e7f96ed-…`, the token's. This is the security property the whole endpoint rests on. |
| One malformed event does not fail the batch | Batch of 3 (bad UUID, unparseable `ts`, one good) → `{"accepted":1,"duplicates":0,"rejected":2}`; REST probe confirms only `survivor` landed. |
| Boundary is 101, not 100 | 100 events → `200 accepted:100`; 101 → `413`. |

### Test-data cleanup

All 104 rows created by these checks were deleted by `session_id`
(`aaaaaaaa-…dead` 2, `bbbbbbbb-…dead` 100, `cccccccc-…dead` 2). Table verified empty
afterwards (`content-range: */0`, body `[]`) — the beta dataset starts clean. The
short-lived session minted for the tests (admin `generate_link` + `email_otp` verify, no
password involved, no account created) was revoked via `/auth/v1/logout` → `204`, and the
local token scratch files were removed.

## Manual / on-device verification required

| Item | Why it cannot be verified here |
|---|---|
| §5.11 — one hole played signed-in, rows in Supabase | Needs a TestFlight build on a real device with a real signed-in tester. **This is the only acceptance item left.** |
| Editor send gate behaves on device | `TelemetryConfig.DefaultSendsEnabled` is false under `UNITY_EDITOR && !GOLFIN_TELEMETRY_DEBUG` and true otherwise — the true branch only compiles in a player build, so it is unexercisable in the Editor by construction. |
| `session_end` on real iOS backgrounding | `OnApplicationPause(true)` fires on device backgrounding; the Editor does not reproduce a real suspend, and the OS may kill the app before the request lands (which is exactly why the event fires on pause and not only on quit). |
| FPS sampling numbers | `fps_avg` / `fps_low` are only meaningful over a real played hole at device frame rates. The accumulator arithmetic is deterministic, but the values are not. |

## Console output

No errors or warnings attributable to this task. `assets-refresh` produced only the
project's pre-existing `CS0618` / `CS8632` obsolete-API and nullable-annotation warnings
in `Editor/Recording/*` and `UI/Inventory/Editor/*`, none in any file this task touched.

Assembly placement verified by reflection after compile:

```
Golfin.Telemetry.TelemetryService          -> Golfin.Telemetry
Golfin.Telemetry.TelemetryBehaviour        -> Golfin.Telemetry
Golfin.Telemetry.TelemetryConfig           -> Golfin.Telemetry
GolfinRedux.TelemetryRuntime.TelemetryHooks-> Assembly-CSharp
Golfin.Gameplay.UI.ShotTelemetryRelay      -> Golfin.Gameplay.UI
ShotController.FlickRejected=True   ShotController.ShotCancelled=True
GameSession.OnRoundStarted=True
TelemetryEvents=https://playlife-api.fly.dev/api/v1/telemetry/events
```

## Working-tree note

`Docs/ADMIN_DASHBOARD_OPS.md` was already modified in the working tree before this task
started (it is in the session's opening `git status`) and belongs to the concurrent Home
Notice work. Not touched, not staged, not reverted.

## Open questions for Architect

1. **`sp_allocated` has no call site because SP allocation itself is unwired** —
   `allocationStrategy` is constructed in `CharacterManager.Awake` and never invoked. Is SP
   allocation meant to be reachable before the beta? If it gets wired up, the event is one
   `RecordSafe` call at the commit point; if not, `telemetry_admin_panel` should not expect it.
2. **`points_changed` fires on every balance callback**, including the server-sync refresh at
   boot, not only on player-visible grants. For 20 testers the volume is trivial, but the panel
   should read it as "balance observed", not "points earned".
